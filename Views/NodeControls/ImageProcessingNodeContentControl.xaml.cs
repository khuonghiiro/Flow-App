using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl : UserControl
    {
        private const double IpColStar = 3.5;
        private const double OtherSiblingStars = 0.8 + 6.0 + 3.2;
        private const double BaseStarWidth = 10.0;
        private const int MagSize = 120;
        private const int MagZoom = 4;

        private readonly ImageProcessingNode _node;
        private readonly IWorkflowEditorHost _host;
        private readonly Window? _ownerWindow;
        private readonly Border? _chromeBorder;
        private readonly Grid _handleOverlay;
        private readonly Func<bool>? _isNodeResizing;
        private readonly bool _freezeScaleInWidget;

        private PropertyChangedEventHandler? _nodePropertyChanged;
        private NotifyCollectionChangedEventHandler? _cropsChangedHandler;
        private Action<ImageCropRegion>? _onCropClickForIp;

        private bool _ipColumnVisible;
        private Storyboard? _ipColumnWidthStoryboard;
        private int _widgetIpExpandLayoutAttempts;
        private double _originalMinWidthSnapshot;
        private Action<BitmapSource?>? _setIpImage;

        private bool _isPanning;
        private Point _panStart;
        private double _panOriginX, _panOriginY;

        private FrameworkElement WidthSyncTarget => (FrameworkElement?)_chromeBorder ?? this;

        public ImageProcessingNodeContentControl(
            ImageProcessingNode node,
            IWorkflowEditorHost host,
            Border? chromeBorder,
            Window? ownerWindow,
            Grid handleOverlay,
            Func<bool>? isNodeResizing = null,
            bool freezeScaleInWidget = true)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _chromeBorder = chromeBorder;
            _ownerWindow = ownerWindow;
            _handleOverlay = handleOverlay ?? throw new ArgumentNullException(nameof(handleOverlay));
            _isNodeResizing = isNodeResizing;
            _freezeScaleInWidget = freezeScaleInWidget;

            if (_chromeBorder == null)
            {
                MinWidth = 800;
                MinHeight = 600;
            }

            InitializeComponent();

            _originalMinWidthSnapshot = WidthSyncTarget.MinWidth;
            var (ipFe, setIp) = ImageProcessingNodeControl.BuildImageProcessorColumn(_node, _host);
            IpProcessorHost.Content = ipFe;
            _setIpImage = setIp;

            CropsListControl.ItemsSource = _node.Crops;
            RenderGroupsControl.ItemsSource = _node.Crops;

            ApplyGpuRenderOptions();
            ApplyHostBackground();

            UpdateColorCropButtonBackground();

            _onCropClickForIp = OnCropRegionSelectedForIp;

            _cropsChangedHandler = OnCropsCollectionChanged;
            _node.Crops.CollectionChanged += _cropsChangedHandler;

            WireScrollPanZoomMagnifier();

            AttachSubscriptions();
            SizeChanged += (_, _) => ApplyResponsiveScale();
        }

        private void ApplyGpuRenderOptions()
        {
            RenderOptions.SetBitmapScalingMode(MainImage, BitmapScalingMode.HighQuality);
            if (GpuDetectionHelper.IsGpuAvailable)
            {
                RenderOptions.SetCachingHint(MainImage, CachingHint.Unspecified);
                MainImage.CacheMode = null;
                RenderOptions.SetBitmapScalingMode(MainScrollViewer, BitmapScalingMode.Unspecified);
                RenderOptions.SetCachingHint(MainScrollViewer, CachingHint.Unspecified);
            }
            RenderOptions.SetBitmapScalingMode(MagZoomImage, BitmapScalingMode.NearestNeighbor);
        }

        private void ApplyHostBackground()
        {
            // Canvas đã có shadow plate riêng từ ImageProcessingNodeControl.
            // Widget không có plate đó, nên dùng NodeBrush trực tiếp để tránh nền trắng.
            if (_chromeBorder == null)
            {
                RootLayout.Background = _node.NodeBrush ?? new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));
                return;
            }
            RootLayout.Background = Brushes.Transparent;
        }

        private void ToggleIPColumn()
        {
            _ipColumnVisible = !_ipColumnVisible;
            bool isWidgetHost = _chromeBorder == null;

            if (isWidgetHost)
            {
                PlayWidgetIpColumnAnimation(_ipColumnVisible);
                return;
            }

            // Canvas node: giữ hành vi cũ (mở rộng/thu hẹp node theo cột IP).
            double totalStar = BaseStarWidth + IpColStar;
            if (_ipColumnVisible)
            {
                double currentContentWidth = _node.Width;
                double newWidth = currentContentWidth * totalStar / BaseStarWidth;
                IpColumnDefinition.Width = new GridLength(IpColStar, GridUnitType.Star);
                _node.Width = newWidth;
                WidthSyncTarget.Width = newWidth;
                WidthSyncTarget.MinWidth = _originalMinWidthSnapshot * totalStar / BaseStarWidth;
            }
            else
            {
                double newWidth = _node.Width * BaseStarWidth / totalStar;
                IpColumnDefinition.Width = new GridLength(0);
                _node.Width = Math.Max(_originalMinWidthSnapshot, newWidth);
                WidthSyncTarget.Width = _node.Width;
                WidthSyncTarget.MinWidth = _originalMinWidthSnapshot;
            }
        }

        private double GetWidgetIpColumnTargetPixelWidth()
        {
            double gridW = RootLayout.ActualWidth;
            if (gridW <= 0 || double.IsNaN(gridW))
                return 0;
            double starSum = OtherSiblingStars + IpColStar;
            return Math.Max(0, gridW * (IpColStar / starSum));
        }

        private void PlayWidgetIpColumnAnimation(bool expand)
        {
            _ipColumnWidthStoryboard?.Stop();
            _ipColumnWidthStoryboard = null;

            RootLayout.UpdateLayout();

            const double durationMs = 240;

            if (expand)
            {
                double targetPx = GetWidgetIpColumnTargetPixelWidth();
                if (targetPx < 0.5)
                {
                    if (_widgetIpExpandLayoutAttempts < 10)
                    {
                        _widgetIpExpandLayoutAttempts++;
                        Dispatcher.BeginInvoke(() => PlayWidgetIpColumnAnimation(true), DispatcherPriority.Loaded);
                        return;
                    }

                    _widgetIpExpandLayoutAttempts = 0;
                    IpColumnDefinition.Width = new GridLength(IpColStar, GridUnitType.Star);
                    return;
                }

                _widgetIpExpandLayoutAttempts = 0;

                IpColumnDefinition.Width = new GridLength(0);

                var anim = new GridLengthAnimation
                {
                    From = new GridLength(0),
                    To = new GridLength(targetPx),
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    FillBehavior = FillBehavior.HoldEnd,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };

                var sb = new Storyboard();
                Storyboard.SetTarget(anim, IpColumnDefinition);
                Storyboard.SetTargetProperty(anim, new PropertyPath(ColumnDefinition.WidthProperty));
                sb.Children.Add(anim);

                void OnCompleted(object? sender, EventArgs args)
                {
                    if (sender is Storyboard s)
                        s.Completed -= OnCompleted;
                    _ipColumnWidthStoryboard = null;
                    IpColumnDefinition.Width = new GridLength(IpColStar, GridUnitType.Star);
                }

                sb.Completed += OnCompleted;
                _ipColumnWidthStoryboard = sb;
                sb.Begin();
            }
            else
            {
                _widgetIpExpandLayoutAttempts = 0;

                double fromPx = IpProcessorHost.ActualWidth;
                if (fromPx < 0.5 &&
                    IpColumnDefinition.Width.GridUnitType == GridUnitType.Star &&
                    IpColumnDefinition.Width.Value > 0)
                {
                    fromPx = GetWidgetIpColumnTargetPixelWidth();
                }

                if (fromPx < 0.5)
                {
                    IpColumnDefinition.Width = new GridLength(0);
                    return;
                }

                IpColumnDefinition.Width = new GridLength(fromPx);

                var anim = new GridLengthAnimation
                {
                    From = new GridLength(fromPx),
                    To = new GridLength(0),
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    FillBehavior = FillBehavior.HoldEnd,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };

                var sb = new Storyboard();
                Storyboard.SetTarget(anim, IpColumnDefinition);
                Storyboard.SetTargetProperty(anim, new PropertyPath(ColumnDefinition.WidthProperty));
                sb.Children.Add(anim);

                void OnCompleted(object? sender, EventArgs args)
                {
                    if (sender is Storyboard s)
                        s.Completed -= OnCompleted;
                    _ipColumnWidthStoryboard = null;
                    IpColumnDefinition.Width = new GridLength(0);
                }

                sb.Completed += OnCompleted;
                _ipColumnWidthStoryboard = sb;
                sb.Begin();
            }
        }

        private void OnCropRegionSelectedForIp(ImageCropRegion reg)
        {
            if (!_ipColumnVisible)
                ToggleIPColumn();

            ImageProcessingNodeControl._currentCropRegionForIp[_node] = reg;
            if (reg.Thumbnail is BitmapSource thumb)
                _setIpImage?.Invoke(thumb);
        }

        private void OnCropsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems == null) return;
            foreach (ImageCropRegion removedReg in e.OldItems)
                ImageProcessingNodeControl.DetachCropPolygon(removedReg, ImageAreaGrid);
        }

        private void UpdateColorCropButtonBackground()
        {
            if (!ImageProcessingNodeControl._activeCropColorIndex.TryGetValue(_node, out var idx))
                idx = 0;
            var colors = ImageProcessingNodeControl._cropColors;
            idx = ((idx % colors.Length) + colors.Length) % colors.Length;
            ColorCropButton.Background = new SolidColorBrush(colors[idx]);
        }

        private void WireScrollPanZoomMagnifier()
        {
            MainScrollViewer.PreviewMouseWheel += MainScrollViewer_PreviewMouseWheel;
            MainScrollViewer.PreviewMouseLeftButtonDown += MainScrollViewer_PreviewMouseLeftButtonDown;
            MainScrollViewer.PreviewMouseLeftButtonUp += MainScrollViewer_PreviewMouseLeftButtonUp;
            MainScrollViewer.PreviewMouseMove += MainScrollViewer_PreviewMouseMove;
            MainScrollViewer.MouseLeave += MainScrollViewer_MouseLeave;
            MainScrollViewer.PreviewKeyDown += MainScrollViewer_PreviewKeyDown;
        }

        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            e.Handled = true;
            var position = e.GetPosition(MainScrollViewer);
            var oldScale = ImageZoomScale.ScaleX;
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            double newScale = oldScale * zoomFactor;
            if (newScale < 0.01) newScale = 0.01;
            if (newScale > 100.0) newScale = 100.0;
            if (Math.Abs(newScale - oldScale) < 0.0001) return;

            double extentWidth = MainScrollViewer.ExtentWidth;
            double extentHeight = MainScrollViewer.ExtentHeight;
            double relativeX = 0.5, relativeY = 0.5;
            if (extentWidth > 0 && extentHeight > 0)
            {
                relativeX = (MainScrollViewer.HorizontalOffset + position.X) / extentWidth;
                relativeY = (MainScrollViewer.VerticalOffset + position.Y) / extentHeight;
            }

            ImageZoomScale.ScaleX = newScale;
            ImageZoomScale.ScaleY = newScale;
            MainScrollViewer.UpdateLayout();

            extentWidth = MainScrollViewer.ExtentWidth;
            extentHeight = MainScrollViewer.ExtentHeight;
            if (extentWidth > 0 && extentHeight > 0)
            {
                var targetX = relativeX * extentWidth - position.X;
                var targetY = relativeY * extentHeight - position.Y;
                MainScrollViewer.ScrollToHorizontalOffset(Math.Max(0, Math.Min(targetX, extentWidth)));
                MainScrollViewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(targetY, extentHeight)));
            }
        }

        private void MainScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MainScrollViewer.Focus();

            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                e.Handled = true;
                ImageProcessingNodeControl.AddCropPointFromClick(
                    _node, MainImage, ImageZoomScale, e.GetPosition(MainImage),
                    ImageAreaGrid, CropToolButton, _onCropClickForIp);
                return;
            }

            if (MainScrollViewer.ExtentWidth <= MainScrollViewer.ViewportWidth &&
                MainScrollViewer.ExtentHeight <= MainScrollViewer.ViewportHeight)
                return;

            _isPanning = true;
            _panStart = e.GetPosition(MainScrollViewer);
            _panOriginX = MainScrollViewer.HorizontalOffset;
            _panOriginY = MainScrollViewer.VerticalOffset;
            MainScrollViewer.Cursor = Cursors.SizeAll;
            MainScrollViewer.CaptureMouse();
            e.Handled = true;
        }

        private void MainScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning) return;
            _isPanning = false;
            MainScrollViewer.Cursor = Cursors.Arrow;
            MainScrollViewer.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void MainScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var pos = e.GetPosition(MainScrollViewer);
                var dx = pos.X - _panStart.X;
                var dy = pos.Y - _panStart.Y;
                MainScrollViewer.ScrollToHorizontalOffset(_panOriginX - dx);
                MainScrollViewer.ScrollToVerticalOffset(_panOriginY - dy);
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && MainImage.Source is BitmapSource)
            {
                MainScrollViewer.Cursor = Cursors.Cross;
                MagOverlayPanel.Visibility = Visibility.Visible;
                UpdateMagnifierUi(e.GetPosition(MainImage));
            }
            else
            {
                if (MagOverlayPanel.Visibility == Visibility.Visible)
                    MagOverlayPanel.Visibility = Visibility.Collapsed;
                if (MainScrollViewer.Cursor == Cursors.Cross)
                    MainScrollViewer.Cursor = Cursors.Arrow;
            }
        }

        private void MainScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            MagOverlayPanel.Visibility = Visibility.Collapsed;
            if (MainScrollViewer.Cursor == Cursors.Cross)
                MainScrollViewer.Cursor = Cursors.Arrow;
        }

        private void MainScrollViewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                ImageProcessingNodeControl.CompleteActiveCrop(_node, CropToolButton))
            {
                e.Handled = true;
            }
        }

        private void UpdateMagnifierUi(Point imgPos)
        {
            if (MainImage.Source is not BitmapSource bmp) return;
            int px = (int)Math.Round(imgPos.X);
            int py = (int)Math.Round(imgPos.Y);
            MagCoordTextBlock.Text = $"{px}, {py}";

            int halfRegion = MagSize / (MagZoom * 2);
            int srcX = Math.Max(0, px - halfRegion);
            int srcY = Math.Max(0, py - halfRegion);
            int srcW = halfRegion * 2;
            int srcH = halfRegion * 2;
            if (srcX + srcW > bmp.PixelWidth) srcW = bmp.PixelWidth - srcX;
            if (srcY + srcH > bmp.PixelHeight) srcH = bmp.PixelHeight - srcY;
            if (srcW <= 0 || srcH <= 0) return;

            try
            {
                MagZoomImage.Source = new CroppedBitmap(bmp, new Int32Rect(srcX, srcY, srcW, srcH));
            }
            catch { /* ignore */ }
        }

        private void IpToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleIPColumn();
            e.Handled = true;
        }

        private void CommonButton_StopBubbling_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

        private void BtnOpenImage_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            ImageProcessingNodeControl.OpenImageFilePicker(_node);
        }

        private void ColorCropButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var dlg = new WinForms.ColorDialog();
                if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var c = dlg.Color;
                    var mediaColor = Color.FromArgb(255, c.R, c.G, c.B);
                    if (ImageProcessingNodeControl._cropColors.Length > 0)
                    {
                        ImageProcessingNodeControl._cropColors[0] = mediaColor;
                        ImageProcessingNodeControl._activeCropColorIndex[_node] = 0;
                    }
                    ColorCropButton.Background = new SolidColorBrush(mediaColor);
                    e.Handled = true;
                    return;
                }
            }
            catch { /* ColorDialog error → cycle */ }

            if (!ImageProcessingNodeControl._activeCropColorIndex.TryGetValue(_node, out var idx))
                idx = 0;
            idx = (idx + 1) % ImageProcessingNodeControl._cropColors.Length;
            ImageProcessingNodeControl._activeCropColorIndex[_node] = idx;
            UpdateColorCropButtonBackground();
            e.Handled = true;
        }

        private void CropToolButton_Click(object sender, RoutedEventArgs e)
        {
            ImageProcessingNodeControl._activeCropRegion[_node] = null;
            e.Handled = true;
        }

        private void CropThumbBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ImageCropRegion reg)
            {
                _onCropClickForIp?.Invoke(reg);
                e.Handled = true;
            }
        }

        private void RemoveCropItem(ImageCropRegion reg)
        {
            reg.RenderedImages.Clear();
            reg.Thumbnail = null;

            if (ImageProcessingNodeControl._currentCropRegionForIp.TryGetValue(_node, out var ipSel) &&
                ReferenceEquals(ipSel, reg))
            {
                ImageProcessingNodeControl._currentCropRegionForIp[_node] = null;
                _setIpImage?.Invoke(null);
            }

            ImageProcessingNodeControl.DetachCropPolygon(reg, ImageAreaGrid);

            _node.Crops.Remove(reg);
            if (ImageProcessingNodeControl._activeCropRegion.TryGetValue(_node, out var active) &&
                ReferenceEquals(active, reg))
            {
                ImageProcessingNodeControl._activeCropRegion[_node] = null;
                CropToolButton.IsEnabled = true;
            }
        }

        private void DeleteCropButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button b && b.DataContext is ImageCropRegion reg)
            {
                RemoveCropItem(reg);
                e.Handled = true;
            }
        }

        private void DeleteCropButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is ImageCropRegion reg)
            {
                RemoveCropItem(reg);
                e.Handled = true;
            }
        }

        private void AttachSubscriptions()
        {
            if (_node is INotifyPropertyChanged npc)
            {
                _nodePropertyChanged = OnNodePropertyChanged;
                npc.PropertyChanged += _nodePropertyChanged;
            }

            Loaded += ImageProcessingNodeContentControl_Loaded;
            Unloaded += ImageProcessingNodeContentControl_Unloaded;

            MainScrollViewer.ContextMenu = new ContextMenu { Visibility = Visibility.Collapsed };
            MainScrollViewer.PreviewMouseRightButtonDown += (_, e) =>
            {
                e.Handled = true;
                ImageProcessingNodeControl.CompleteActiveCrop(_node, CropToolButton);
            };

            if (_chromeBorder != null)
                _chromeBorder.MouseRightButtonUp += ChromeOrSelf_MouseRightButtonUp;
            else
                MouseRightButtonUp += ChromeOrSelf_MouseRightButtonUp;
        }

        private void ApplyResponsiveScale()
        {
            if (_chromeBorder == null && _freezeScaleInWidget)
            {
                TopMenuBorder.LayoutTransform = Transform.Identity;
                CropsLabelText.LayoutTransform = Transform.Identity;
                LeftMenuBorder.LayoutTransform = Transform.Identity;
                return;
            }

            double heightBaseline = WidthSyncTarget.MinHeight > 0 ? WidthSyncTarget.MinHeight : 600.0;
            var heightScaleFactor = Math.Max(0.8, Math.Min(1.8, _node.Height / heightBaseline));
            var menuHeightScale = new ScaleTransform(heightScaleFactor, heightScaleFactor);
            TopMenuBorder.LayoutTransform = menuHeightScale;
            CropsLabelText.LayoutTransform = menuHeightScale;

            double widthBaseline = WidthSyncTarget.MinWidth > 0 ? WidthSyncTarget.MinWidth : 800.0;
            const double leftMenuWidthRatio = 0.8 / 10.0;
            var leftMenuBaselineWidth = widthBaseline * leftMenuWidthRatio;
            var leftMenuCurrentWidth = _node.Width * leftMenuWidthRatio;
            var leftMenuScaleFactor = leftMenuBaselineWidth > 0
                ? leftMenuCurrentWidth / leftMenuBaselineWidth
                : 1.0;
            LeftMenuBorder.LayoutTransform = new ScaleTransform(leftMenuScaleFactor, leftMenuScaleFactor);

            var interactionScale = Math.Max(heightScaleFactor, leftMenuScaleFactor);
            ImageProcessingNodeControl.UpdateInteractionVisualScale(_handleOverlay, _node, interactionScale);
        }

        private void ChromeOrSelf_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var src = e.OriginalSource as DependencyObject;
            bool IsInside(FrameworkElement fe) =>
                src != null && fe != null && VisualTreeHelper.GetParent(src) != null && fe.IsAncestorOf(src);

            if (!IsInside(MainScrollViewer))
            {
                e.Handled = true;
                ImageProcessingNodeControl.OpenNodeDialog(_node, _host, _ownerWindow);
            }
        }

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageProcessingNode.Width) ||
                e.PropertyName == nameof(ImageProcessingNode.Height))
            {
                if (_isNodeResizing?.Invoke() == true)
                    return;
                ApplyResponsiveScale();
            }
            else if (e.PropertyName == nameof(ImageProcessingNode.InputMode) ||
                     e.PropertyName == nameof(ImageProcessingNode.ImageUrl) ||
                     e.PropertyName == nameof(ImageProcessingNode.ImageBase64) ||
                     e.PropertyName == nameof(ImageProcessingNode.ImageUrlSourceNodeId) ||
                     e.PropertyName == nameof(ImageProcessingNode.ImageUrlSourceOutputKey) ||
                     e.PropertyName == nameof(ImageProcessingNode.ImageBase64SourceNodeId) ||
                     e.PropertyName == nameof(ImageProcessingNode.ImageBase64SourceOutputKey))
            {
                _ = ImageProcessingNodeControl.UpdatePreviewAsync(
                    _node, _host, MainImage, PlaceholderTextBlock, ImageZoomScale,
                    ImageAreaGrid, MainScrollViewer, ImageTitleTextBlock,
                    _onCropClickForIp);
            }
            else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
            {
                ApplyHostBackground();
            }
        }

        private void ImageProcessingNodeContentControl_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyResponsiveScale();

            _ = ImageProcessingNodeControl.UpdatePreviewAsync(
                _node, _host, MainImage, PlaceholderTextBlock, ImageZoomScale,
                ImageAreaGrid, MainScrollViewer, ImageTitleTextBlock,
                _onCropClickForIp);
        }

        private void ImageProcessingNodeContentControl_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _ipColumnWidthStoryboard?.Stop();
                _ipColumnWidthStoryboard = null;

                if (_node is INotifyPropertyChanged npc && _nodePropertyChanged != null)
                    npc.PropertyChanged -= _nodePropertyChanged;
                if (_cropsChangedHandler != null)
                    _node.Crops.CollectionChanged -= _cropsChangedHandler;
                foreach (var reg in _node.Crops)
                    ImageProcessingNodeControl._polygonMap.Remove(reg);
                ImageProcessingNodeControl._activeCropRegion.Remove(_node);
                ImageProcessingNodeControl._currentCropRegionForIp.Remove(_node);
            }
            catch { /* ignore */ }
        }
    }
}
