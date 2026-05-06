using FlowMy.Models.Nodes;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Globalization;

namespace FlowMy.Controls
{
    public partial class OverlayItemControl : UserControl
    {
        private enum DragMode
        {
            None,
            Move,
            ResizeNW,
            ResizeN,
            ResizeNE,
            ResizeE,
            ResizeSE,
            ResizeS,
            ResizeSW,
            ResizeW,
            Rotate
        }

        private DragMode _dragMode = DragMode.None;
        private Point _startPoint;
        private double _startX;
        private double _startY;
        private double _startWidth;
        private double _startHeight;
        private double _startRotation;
        private bool _isEditingText;

        public OverlayItemControl()
        {
            InitializeComponent();
            Loaded += (_, _) => RefreshViewFromItem();
            SizeChanged += (_, _) =>
            {
                if (Item != null && string.Equals(Item.Type, "text", StringComparison.OrdinalIgnoreCase))
                    AutoFitTextContent();
            };

            MouseLeftButtonDown += OnMoveStart;
            MouseMove += OnMove;
            MouseLeftButtonUp += OnMoveEnd;
            MouseDoubleClick += OnMouseDoubleClick;

            RegisterHandle(HandleNW, DragMode.ResizeNW);
            RegisterHandle(HandleN, DragMode.ResizeN);
            RegisterHandle(HandleNE, DragMode.ResizeNE);
            RegisterHandle(HandleE, DragMode.ResizeE);
            RegisterHandle(HandleSE, DragMode.ResizeSE);
            RegisterHandle(HandleS, DragMode.ResizeS);
            RegisterHandle(HandleSW, DragMode.ResizeSW);
            RegisterHandle(HandleW, DragMode.ResizeW);
            RegisterHandle(RotateThumb, DragMode.Rotate);

            TextEditor.LostKeyboardFocus += (_, _) => CommitTextEdit();
            TextEditor.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
                {
                    CommitTextEdit();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelTextEdit();
                    e.Handled = true;
                }
            };
        }

        public event EventHandler? ItemChanged;
        public event EventHandler? ItemSelected;

        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.Register(nameof(Item), typeof(OverlayItem), typeof(OverlayItemControl),
                new PropertyMetadata(null, OnItemChanged));

        public static readonly DependencyProperty ParentSurfaceWidthProperty =
            DependencyProperty.Register(nameof(ParentSurfaceWidth), typeof(double), typeof(OverlayItemControl),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty ParentSurfaceHeightProperty =
            DependencyProperty.Register(nameof(ParentSurfaceHeight), typeof(double), typeof(OverlayItemControl),
                new PropertyMetadata(1.0));

        public OverlayItem? Item
        {
            get => (OverlayItem?)GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        public double ParentSurfaceWidth
        {
            get => (double)GetValue(ParentSurfaceWidthProperty);
            set => SetValue(ParentSurfaceWidthProperty, value);
        }

        public double ParentSurfaceHeight
        {
            get => (double)GetValue(ParentSurfaceHeightProperty);
            set => SetValue(ParentSurfaceHeightProperty, value);
        }

        private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((OverlayItemControl)d).RefreshViewFromItem();
        }

        public void RefreshViewFromItem()
        {
            if (Item == null) return;

            Opacity = Item.Opacity;
            RenderTransform = new RotateTransform(Item.Rotation, ActualWidth / 2, ActualHeight / 2);
            Visibility = Item.IsVisible ? Visibility.Visible : Visibility.Collapsed;

            SelectionBorder.Visibility = Item.IsSelected ? Visibility.Visible : Visibility.Collapsed;
            RotateThumb.Visibility = Item.IsSelected ? Visibility.Visible : Visibility.Collapsed;
            RotateConnector.Visibility = Item.IsSelected ? Visibility.Visible : Visibility.Collapsed;
            var handlesVisibility = Item.IsSelected ? Visibility.Visible : Visibility.Collapsed;
            HandleNW.Visibility = handlesVisibility;
            HandleN.Visibility = handlesVisibility;
            HandleNE.Visibility = handlesVisibility;
            HandleE.Visibility = handlesVisibility;
            HandleSE.Visibility = handlesVisibility;
            HandleS.Visibility = handlesVisibility;
            HandleSW.Visibility = handlesVisibility;
            HandleW.Visibility = handlesVisibility;

            var type = (Item.Type ?? string.Empty).ToLowerInvariant();
            if (type == "image" || type == "logo")
            {
                ImageContent.Visibility = Visibility.Visible;
                TextContent.Visibility = Visibility.Collapsed;
                if (File.Exists(Item.Source))
                {
                    ImageContent.Source = new BitmapImage(new Uri(Item.Source, UriKind.Absolute));
                }
                else
                {
                    ImageContent.Source = null;
                }
            }
            else
            {
                ImageContent.Visibility = Visibility.Collapsed;
                TextContent.Visibility = _isEditingText ? Visibility.Collapsed : Visibility.Visible;
                TextContent.Text = Item.Source;
                TextContent.FontFamily = new FontFamily(string.IsNullOrWhiteSpace(Item.FontFamily) ? "Arial" : Item.FontFamily);
                TextContent.TextAlignment = (Item.TextAlignment ?? "Left").Trim().ToLowerInvariant() switch
                {
                    "center" => TextAlignment.Center,
                    "right" => TextAlignment.Right,
                    _ => TextAlignment.Left
                };
                try
                {
                    TextContent.Foreground = (Brush)new BrushConverter().ConvertFromString(string.IsNullOrWhiteSpace(Item.FontColor) ? "White" : Item.FontColor);
                }
                catch
                {
                    TextContent.Foreground = Brushes.White;
                }
                AutoFitTextContent();
            }
        }

        private void AutoFitTextContent()
        {
            if (Item == null) return;

            var availableW = Math.Max(1, ActualWidth - 8);
            var availableH = Math.Max(1, ActualHeight - 8);
            var text = TextContent.Text ?? string.Empty;
            var baseSize = Math.Max(8, Item.FontSize * (Math.Max(1, ParentSurfaceHeight) / 1080.0));
            var typeface = new Typeface(TextContent.FontFamily, TextContent.FontStyle, TextContent.FontWeight, TextContent.FontStretch);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var fit = baseSize;

            for (var size = baseSize; size >= 6; size -= 0.5)
            {
                var ft = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    size,
                    Brushes.Black,
                    dpi)
                {
                    MaxTextWidth = availableW,
                    MaxTextHeight = availableH,
                    Trimming = TextTrimming.None,
                    TextAlignment = TextContent.TextAlignment
                };

                if (ft.Height <= availableH && ft.Width <= availableW)
                {
                    fit = size;
                    break;
                }
            }

            TextContent.FontSize = fit;
        }

        private void RegisterHandle(Thumb thumb, DragMode mode)
        {
            thumb.Background = Brushes.White;
            thumb.BorderBrush = new SolidColorBrush(Color.FromRgb(124, 107, 248));
            thumb.BorderThickness = new Thickness(1);
            thumb.DragStarted += (_, _) => BeginDrag(mode, Mouse.GetPosition(this));
            thumb.DragDelta += (_, e) => ApplyDrag(mode, e.HorizontalChange, e.VerticalChange, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            thumb.DragCompleted += (_, _) => EndDrag();
        }

        private void OnMoveStart(object sender, MouseButtonEventArgs e)
        {
            if (_isEditingText || Item == null || Item.IsLocked) return;
            BeginDrag(DragMode.Move, e.GetPosition(this));
            Item.IsSelected = true;
            ItemSelected?.Invoke(this, EventArgs.Empty);
            CaptureMouse();
            e.Handled = true;
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (_dragMode != DragMode.Move || Item == null || !IsMouseCaptured) return;
            var current = e.GetPosition(this);
            var dx = current.X - _startPoint.X;
            var dy = current.Y - _startPoint.Y;
            ApplyDrag(DragMode.Move, dx, dy, false);
            _startPoint = current;
        }

        private void OnMoveEnd(object sender, MouseButtonEventArgs e)
        {
            if (_dragMode == DragMode.None) return;
            EndDrag();
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
        }

        private void BeginDrag(DragMode mode, Point startPoint)
        {
            if (Item == null || Item.IsLocked) return;
            _dragMode = mode;
            _startPoint = startPoint;
            _startX = Item.X;
            _startY = Item.Y;
            _startWidth = Item.Width;
            _startHeight = Item.Height;
            _startRotation = Item.Rotation;
            Item.IsSelected = true;
            ItemSelected?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyDrag(DragMode mode, double deltaX, double deltaY, bool keepAspect)
        {
            if (Item == null || Item.IsLocked) return;
            var parentW = Math.Max(1, ParentSurfaceWidth);
            var parentH = Math.Max(1, ParentSurfaceHeight);
            var ndx = deltaX / parentW;
            var ndy = deltaY / parentH;

            switch (mode)
            {
                case DragMode.Move:
                    Item.X = Math.Clamp(Item.X + ndx, 0, Math.Max(0, 1 - Item.Width));
                    Item.Y = Math.Clamp(Item.Y + ndy, 0, Math.Max(0, 1 - Item.Height));
                    break;
                case DragMode.ResizeSE:
                    ResizeTo(_startX, _startY, _startWidth + ndx, _startHeight + ndy, keepAspect);
                    break;
                case DragMode.ResizeE:
                    ResizeTo(_startX, _startY, _startWidth + ndx, _startHeight, keepAspect);
                    break;
                case DragMode.ResizeS:
                    ResizeTo(_startX, _startY, _startWidth, _startHeight + ndy, keepAspect);
                    break;
                case DragMode.ResizeNW:
                    ResizeTo(_startX + ndx, _startY + ndy, _startWidth - ndx, _startHeight - ndy, keepAspect);
                    break;
                case DragMode.ResizeN:
                    ResizeTo(_startX, _startY + ndy, _startWidth, _startHeight - ndy, keepAspect);
                    break;
                case DragMode.ResizeNE:
                    ResizeTo(_startX, _startY + ndy, _startWidth + ndx, _startHeight - ndy, keepAspect);
                    break;
                case DragMode.ResizeSW:
                    ResizeTo(_startX + ndx, _startY, _startWidth - ndx, _startHeight + ndy, keepAspect);
                    break;
                case DragMode.ResizeW:
                    ResizeTo(_startX + ndx, _startY, _startWidth - ndx, _startHeight, keepAspect);
                    break;
                case DragMode.Rotate:
                    var center = new Point(ActualWidth / 2, ActualHeight / 2);
                    var angle = Math.Atan2(_startPoint.Y - center.Y, _startPoint.X - center.X) * 180 / Math.PI;
                    Item.Rotation = _startRotation + angle;
                    break;
            }

            ItemChanged?.Invoke(this, EventArgs.Empty);
            RefreshViewFromItem();
        }

        private void ResizeTo(double x, double y, double width, double height, bool keepAspect)
        {
            if (Item == null) return;
            var minSize = 0.02;
            var nextWidth = Math.Clamp(width, minSize, 1);
            var nextHeight = Math.Clamp(height, minSize, 1);
            if (keepAspect && _startHeight > 0)
            {
                var ratio = _startWidth / _startHeight;
                nextHeight = Math.Clamp(nextWidth / Math.Max(0.01, ratio), minSize, 1);
            }

            Item.X = Math.Clamp(x, 0, 1 - nextWidth);
            Item.Y = Math.Clamp(y, 0, 1 - nextHeight);
            Item.Width = nextWidth;
            Item.Height = nextHeight;
        }

        private void EndDrag()
        {
            _dragMode = DragMode.None;
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Item == null || Item.Type != "text") return;
            StartTextEdit();
            e.Handled = true;
        }

        private void StartTextEdit()
        {
            if (Item == null) return;
            _isEditingText = true;
            TextEditor.Text = Item.Source;
            TextEditor.Visibility = Visibility.Visible;
            TextContent.Visibility = Visibility.Collapsed;
            TextEditor.Focus();
            TextEditor.SelectAll();
        }

        private void CommitTextEdit()
        {
            if (Item == null || !_isEditingText) return;
            Item.Source = TextEditor.Text ?? string.Empty;
            _isEditingText = false;
            TextEditor.Visibility = Visibility.Collapsed;
            TextContent.Visibility = Visibility.Visible;
            ItemChanged?.Invoke(this, EventArgs.Empty);
            RefreshViewFromItem();
        }

        private void CancelTextEdit()
        {
            _isEditingText = false;
            TextEditor.Visibility = Visibility.Collapsed;
            TextContent.Visibility = Visibility.Visible;
            RefreshViewFromItem();
        }
    }
}
