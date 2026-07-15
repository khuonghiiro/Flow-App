// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS:
// DO NOT ADD MORE LOGIC OR CODE DIRECTLY INTO THIS FILE (ImageProcessingNodeContentControl.xaml.cs)!
// This file is already very large and complex. Any new features, tools, or major logical changes
// must be created in separate partial class files (e.g., ImageProcessingNodeContentControl.MyNewFeature.cs)
// to keep the codebase modular, maintainable, and easy to refactor.
// ========================================================================================
using FlowMy.Controls;
using SkiaSharp;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl : UserControl
    {
        private const double IpColStar = 3.5;
        private const double OtherSiblingStars = 0.6 + 6.2 + 3.2;
        private const double BaseStarWidth = 10.0;
        private const double ChromeScaleCapWidth = 1920.0;
        private const double ChromeScaleCapHeight = 1080.0;
        private const double ChromeScaleGamma = 0.52;
        private const double ChromeScaleMin = 0.88;
        private const double ChromeScaleMax = 1.32;
        private const int MagSize = 120;
        private const int MagZoom = 4;
        private EditorLayer? _editingTextLayer;

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
        private Uri? _ipToggleGlyphHiddenUri;
        private Uri? _ipToggleGlyphVisibleUri;
        private double _originalMinWidthSnapshot;
        private Action<BitmapSource?>? _setIpImage;
        /// <summary>Widget: trạng thái phóng full work area (FloatingWidgetWindow._isWidgetMaximized), không phải WPF WindowState.</summary>
        private bool _widgetExpandedFullscreen;

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
                MinWidth = 0;
                MinHeight = 0;
            }

            InitializeComponent();
            EditorPanel.SetNodeAndHost(_node, _host);
            this.Loaded += (s, e) => { InitializeFxDots(); InitializeBrushPresetListBoxItems(); };
            this.Unloaded += (s, e) => { CommitPendingMoveTranslation(); CommitBrushDrawingSession(); };
            MainImage.SizeChanged += (s, e) =>
            {
                if (CropOverlayCanvas != null && CropOverlayCanvas.Visibility == Visibility.Visible)
                {
                    UpdateCropOverlayDisplay();
                }
            };
            EditorPanel.DocumentModified += OnEditorDocumentModified;
            EditorPanel.TextPropertiesChanged += EditorPanel_TextPropertiesChanged;
            EditorPanel.BrushPropertiesChanged += EditorPanel_BrushPropertiesChanged;
            EditorPanel.ActiveLayerChanged += EditorPanel_ActiveLayerChanged;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            this.PreviewKeyDown += ImageProcessingNodeContentControl_PreviewKeyDown;
            this.PreviewKeyUp += ImageProcessingNodeContentControl_PreviewKeyUp;

            var iconConv = new IconKeyToPathConverter();
            var culture = CultureInfo.InvariantCulture;
            _ipToggleGlyphHiddenUri = iconConv.Convert(null, typeof(Uri), "angles-right sharp-solid", culture) as Uri;
            _ipToggleGlyphVisibleUri = iconConv.Convert(null, typeof(Uri), "angles-left sharp-solid", culture) as Uri;
            SyncIpToggleIcon();

            _originalMinWidthSnapshot = WidthSyncTarget.MinWidth;
            var (ipFe, setIp) = ImageProcessingNodeControl.BuildImageProcessorColumn(
                _node,
                _host,
                preventScaleUp: false);
            IpProcessorHost.Content = ipFe;
            _setIpImage = setIp;

            // CropsListControl.ItemsSource = _node.Crops;
            // RenderGroupsControl.ItemsSource = _node.Crops;

            ApplyGpuRenderOptions();
            ApplyHostBackground();

            // Khởi tạo OpenCL + load FX cache trên background thread (không block UI)
            if (!_openClInitialized)
            {
                _openClInitialized = true;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        // Load cached FX params trước (nhẹ, file nhỏ)
                        FlowMy.Utils.FxConfigCache.LoadFromFile();

                        // Cấu hình thư mục cache OpenCL để GPU không phải biên dịch lại kernel mỗi lần chạy
                        try
                        {
                            string cacheDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "MagickOpenCL");
                            if (!System.IO.Directory.Exists(cacheDir))
                            {
                                System.IO.Directory.CreateDirectory(cacheDir);
                            }
                            System.Environment.SetEnvironmentVariable("MAGICK_OPENCL_CACHE_DIR", cacheDir);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MagickOpenCL] Set CacheDirectory failed: {ex.Message}");
                        }

                        // OpenCL init (có thể chậm tùy GPU driver)
                        ImageMagick.OpenCL.IsEnabled = true;
                        var devices = ImageMagick.OpenCL.Devices;
                        if (devices != null)
                        {
                            foreach (var dev in devices)
                            {
                                dev.IsEnabled = true;
                                System.Diagnostics.Debug.WriteLine($"[MagickOpenCL] GPU: {dev.Name} (v{dev.Version})");
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"[MagickOpenCL] Enabled={ImageMagick.OpenCL.IsEnabled}");

                        // Cấu hình Magick.NET sử dụng đa luồng CPU
                        ImageMagick.ResourceLimits.Thread = (ulong)Environment.ProcessorCount;
                        System.Diagnostics.Debug.WriteLine($"[MagickConfig] Threads={ImageMagick.ResourceLimits.Thread}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MagickOpenCL] Init failed: {ex.Message}");
                    }
                });
            }

            UpdateColorCropButtonBackground();

            _onCropClickForIp = OnCropRegionSelectedForIp;

            _cropsChangedHandler = OnCropsCollectionChanged;
            _node.Crops.CollectionChanged += _cropsChangedHandler;

            WireScrollPanZoomMagnifier();

            AttachSubscriptions();
            SizeChanged += (_, _) => ApplyResponsiveScale();

            // Sync mode toggle visual state
            SyncModeButtonStyles();
            SwitchToMode(_node.ProcessingMode);
        }

        private static bool _openClInitialized;

        /// <summary>Gọi từ FloatingWidgetWindow khi bật/tắt phóng to widget (work area).</summary>
        public void SyncWidgetExpandedFullscreen(bool expandedFullscreen)
        {
            _widgetExpandedFullscreen = expandedFullscreen;
            if (_chromeBorder == null && _freezeScaleInWidget)
                ApplyResponsiveScale();
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
            }
            else
            {
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

            SyncIpToggleIcon();
        }

        private void SyncIpToggleIcon()
        {
            if (IpToggleIcon == null) return;
            IpToggleIcon.Source = _ipColumnVisible ? _ipToggleGlyphVisibleUri : _ipToggleGlyphHiddenUri;
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
            // ColorCropButton đã được ẩn đi, không cần cập nhật UI background
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
            if (MainImage != null)
            {
                RenderOptions.SetBitmapScalingMode(MainImage, newScale > 1.001 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            }
            MainScrollViewer.UpdateLayout();

            if (CropOverlayCanvas != null && CropOverlayCanvas.Visibility == Visibility.Visible)
            {
                UpdateCropOverlayDisplay();
            }

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
            if (e.OriginalSource is DependencyObject dep)
            {
                var parent = dep;
                while (parent != null)
                {
                    if (parent == TextMoveContainer)
                    {
                        return; // Let child elements inside text box overlay handle mouse down natively
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            MainScrollViewer.Focus();

            if (EditorPanel != null)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Transform" || tool == "CropCanvas") return;
            }

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                if (_isSpacePressed)
                {
                    _isPanning = true;
                    _panStart = e.GetPosition(MainScrollViewer);
                    _panOriginX = MainScrollViewer.HorizontalOffset;
                    _panOriginY = MainScrollViewer.VerticalOffset;
                    MainScrollViewer.Cursor = Cursors.SizeAll;
                    MainScrollViewer.CaptureMouse();
                    e.Handled = true;
                    return;
                }

                // If not space-pressed, left click inside MainImage delegates to manual tools (including Move)
                var clickPos = e.GetPosition(MainImage);
                if (clickPos.X >= 0 && clickPos.X <= MainImage.ActualWidth &&
                    clickPos.Y >= 0 && clickPos.Y <= MainImage.ActualHeight)
                {
                    HandleManualEditorMouseDown(e);
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    e.Handled = true;
                    ImageProcessingNodeControl.AddCropPointFromClick(
                        _node, MainImage, ImageZoomScale, e.GetPosition(MainImage),
                        ImageAreaGrid, null, _onCropClickForIp);
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
        }

        private void MainScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (EditorPanel != null)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Transform" || tool == "CropCanvas") return;
            }

            if (_isDrawingPixels || _isSelecting || _isMovingLayer)
            {
                HandleManualEditorMouseUp();
                e.Handled = true;
                return;
            }

            if (!_isPanning) return;
            _isPanning = false;
            MainScrollViewer.Cursor = Cursors.Arrow;
            MainScrollViewer.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void MainScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            UpdateBrushCursorPosition();
            UpdateEyedropperCursorPosition(e);

            if (EditorPanel != null)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Transform" || tool == "CropCanvas") return;
            }

            if (_isDrawingPixels || _isSelecting || _isMovingLayer)
            {
                HandleManualEditorMouseMove(e);
                e.Handled = true;
                return;
            }

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

        private void UpdateBrushCursorPosition()
        {
            if (BrushPreviewCursor == null || MainImage == null || _node.EditorDoc == null) return;

            // Clear any dynamically added brush shape elements first
            if (BrushCursorCanvas != null)
            {
                for (int i = BrushCursorCanvas.Children.Count - 1; i >= 0; i--)
                {
                    if (BrushCursorCanvas.Children[i] != BrushPreviewCursor && BrushCursorCanvas.Children[i] != BrushCrosshairCursor)
                    {
                        BrushCursorCanvas.Children.RemoveAt(i);
                    }
                }
            }

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                string tool = EditorPanel.ActiveToolName;
                if ((tool == "Brush" || tool == "Eraser") && MainImage.Source != null)
                {
                    var imgPos = Mouse.GetPosition(MainImage);
                    bool inside = imgPos.X >= 0 && imgPos.X <= MainImage.ActualWidth &&
                                  imgPos.Y >= 0 && imgPos.Y <= MainImage.ActualHeight;

                    if (inside && !_isSpacePressed && !_isPanning)
                    {
                        if (MainImage.Cursor != Cursors.None)
                        {
                            MainImage.Cursor = Cursors.None;
                        }

                        double scaleX = 1.0; // Avoid double-scaling as parent element is already scaled by ImageZoomScale
                        double radius = (EditorPanel.BrushSize * scaleX) / 2.0;
                        double diameter = radius * 2;

                        var containerPos = Mouse.GetPosition(ImageContainer);

                        // Check if cursor visual size on screen is small (threshold: visual diameter <= 10 pixels)
                        bool showCrosshair = (EditorPanel.BrushSize * ImageZoomScale.ScaleX) <= 10.0;
                        if (BrushCrosshairCursor != null)
                        {
                            if (showCrosshair)
                            {
                                Canvas.SetLeft(BrushCrosshairCursor, containerPos.X);
                                Canvas.SetTop(BrushCrosshairCursor, containerPos.Y);

                                if (CrosshairScale != null)
                                {
                                    double invScale = 1.0 / ImageZoomScale.ScaleX;
                                    CrosshairScale.ScaleX = invScale;
                                    CrosshairScale.ScaleY = invScale;
                                }

                                BrushCrosshairCursor.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                BrushCrosshairCursor.Visibility = Visibility.Collapsed;
                            }
                        }

                        double hardness = EditorPanel.BrushHardness / 100.0;
                        double flow = EditorPanel.BrushFlow / 100.0;
                        Color brushColor = _node.EditorDoc.ForegroundColor;

                        double imgRadius = EditorPanel.BrushSize / 2.0;

                        if (_currentBrushPreset == BrushPreset.Chalk)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 6.0;
                            foreach (var offset in ChalkPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * (offset.size * 0.15);
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 180), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.Spray)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 6.0;
                            foreach (var offset in SprayPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * 0.25;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                double distRatio = Math.Sqrt(offset.x * offset.x + offset.y * offset.y);
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 200 * (1.0 - distRatio * 0.5)), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.Scatter)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 6.0;
                            foreach (var offset in ScatterPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * offset.scale;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.Flat)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            var rect = new Rectangle
                            {
                                Width = diameter,
                                Height = Math.Max(2.0, diameter / 3.0),
                                Stroke = Brushes.White,
                                StrokeThickness = 1
                            };
                            rect.Effect = new System.Windows.Media.Effects.DropShadowEffect
                            {
                                BlurRadius = 1,
                                ShadowDepth = 0,
                                Color = Colors.Black,
                                Opacity = 0.8
                            };
                            Canvas.SetLeft(rect, containerPos.X - radius);
                            Canvas.SetTop(rect, containerPos.Y - radius / 3.0);
                            BrushCursorCanvas.Children.Add(rect);
                        }
                        else if (_currentBrushPreset == BrushPreset.RoundSoft)
                        {
                            BrushPreviewCursor.Width = diameter;
                            BrushPreviewCursor.Height = diameter;
                            Canvas.SetLeft(BrushPreviewCursor, containerPos.X - radius);
                            Canvas.SetTop(BrushPreviewCursor, containerPos.Y - radius);

                            var radialBrush = new RadialGradientBrush();
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255 * 0.5), brushColor.R, brushColor.G, brushColor.B), 0.4));
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));

                            BrushPreviewCursor.Fill = radialBrush;
                            BrushPreviewCursor.OpacityMask = null;
                            BrushPreviewCursor.Visibility = Visibility.Visible;
                        }
                        else if (_currentBrushPreset == BrushPreset.Pencil)
                        {
                            double pencilRadius = Math.Min(radius, Math.Max(1.5 * scaleX, radius * 0.5));
                            double pencilDiameter = pencilRadius * 2;
                            BrushPreviewCursor.Width = pencilDiameter;
                            BrushPreviewCursor.Height = pencilDiameter;
                            Canvas.SetLeft(BrushPreviewCursor, containerPos.X - pencilRadius);
                            Canvas.SetTop(BrushPreviewCursor, containerPos.Y - pencilRadius);

                            BrushPreviewCursor.Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B));
                            BrushPreviewCursor.OpacityMask = null;
                            BrushPreviewCursor.Visibility = Visibility.Visible;
                        }
                        else if (_currentBrushPreset == BrushPreset.Airbrush)
                        {
                            double airRadius = radius * 1.8;
                            double airDiameter = airRadius * 2;
                            BrushPreviewCursor.Width = airDiameter;
                            BrushPreviewCursor.Height = airDiameter;
                            Canvas.SetLeft(BrushPreviewCursor, containerPos.X - airRadius);
                            Canvas.SetTop(BrushPreviewCursor, containerPos.Y - airRadius);

                            var radialBrush = new RadialGradientBrush();
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255 * 0.25), brushColor.R, brushColor.G, brushColor.B), 0.3));
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));

                            BrushPreviewCursor.Fill = radialBrush;
                            BrushPreviewCursor.OpacityMask = null;
                            BrushPreviewCursor.Visibility = Visibility.Visible;
                        }
                        else if (_currentBrushPreset == BrushPreset.Splatter)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 6.0;
                            foreach (var offset in SplatterPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * offset.size * 0.4;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * offset.opacity * 255), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.Charcoal)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 4.0;
                            foreach (var offset in CharcoalPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * offset.size * 0.45;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * offset.opacity * 200), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.OilBrush)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 3.5;
                            foreach (var offset in OilBrushPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * offset.size * 0.25;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 200), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else // RoundHard
                        {
                            BrushPreviewCursor.Width = diameter;
                            BrushPreviewCursor.Height = diameter;
                            Canvas.SetLeft(BrushPreviewCursor, containerPos.X - radius);
                            Canvas.SetTop(BrushPreviewCursor, containerPos.Y - radius);

                            var radialBrush = new RadialGradientBrush();
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));

                            double stopOffset = radius > 0.001 ? Math.Min(hardness, Math.Max(0.0, (radius - scaleX) / radius)) : hardness;
                            if (stopOffset < 0.99)
                            {
                                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), stopOffset));
                                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                            }
                            else
                            {
                                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.99));
                                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                            }

                            BrushPreviewCursor.Fill = radialBrush;
                            BrushPreviewCursor.OpacityMask = null;
                            BrushPreviewCursor.Visibility = Visibility.Visible;
                        }
                        return;
                    }
                }
            }

            if (MainImage.Cursor == Cursors.None)
            {
                MainImage.Cursor = null; // Restore default cursor
            }
            BrushPreviewCursor.Visibility = Visibility.Collapsed;
            if (BrushCrosshairCursor != null)
            {
                BrushCrosshairCursor.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateEyedropperCursorPosition(MouseEventArgs e)
        {
            if (EyedropperPreviewContainer == null || MainImage == null || _node.EditorDoc == null) return;

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Eyedropper" && MainImage.Source != null)
                {
                    var imgPos = Mouse.GetPosition(MainImage);
                    bool inside = imgPos.X >= 0 && imgPos.X <= MainImage.ActualWidth &&
                                  imgPos.Y >= 0 && imgPos.Y <= MainImage.ActualHeight;

                    if (inside && !_isSpacePressed && !_isPanning)
                    {
                        var activeLayer = _node.EditorDoc.ActiveLayer;
                        if (activeLayer != null)
                        {
                            double scaleX = activeLayer.Width / MainImage.ActualWidth;
                            double scaleY = activeLayer.Height / MainImage.ActualHeight;
                            int px = Math.Clamp((int)(imgPos.X * scaleX), 0, activeLayer.Width - 1);
                            int py = Math.Clamp((int)(imgPos.Y * scaleY), 0, activeLayer.Height - 1);

                            try
                            {
                                var stride = 4;
                                var singlePixel = new byte[4];
                                activeLayer.Bitmap.CopyPixels(new Int32Rect(px, py, 1, 1), singlePixel, stride, 0);
                                Color sampledColor = Color.FromArgb(singlePixel[3], singlePixel[2], singlePixel[1], singlePixel[0]);

                                // Set background colors of the split ring preview
                                EyedropperNewColorBorder.Background = new SolidColorBrush(sampledColor);
                                EyedropperOldColorBorder.Background = new SolidColorBrush(_node.EditorDoc.ForegroundColor);

                                // Set Hex Text representation
                                if (sampledColor.A == 0)
                                {
                                    EyedropperHexText.Text = "Transparent";
                                }
                                else
                                {
                                    EyedropperHexText.Text = $"#{sampledColor.R:X2}{sampledColor.G:X2}{sampledColor.B:X2}";
                                }

                                // Move preview container to cursor
                                var containerPos = Mouse.GetPosition(ImageContainer);
                                Canvas.SetLeft(EyedropperPreviewContainer, containerPos.X);
                                Canvas.SetTop(EyedropperPreviewContainer, containerPos.Y);

                                if (EyedropperScaleTransform != null && ImageZoomScale != null)
                                {
                                    double invScale = 1.0 / Math.Max(0.01, ImageZoomScale.ScaleX);
                                    EyedropperScaleTransform.ScaleX = invScale;
                                    EyedropperScaleTransform.ScaleY = invScale;
                                }

                                EyedropperPreviewContainer.Visibility = Visibility.Visible;
                                return;
                            }
                            catch { /* ignore */ }
                        }
                    }
                }
            }

            EyedropperPreviewContainer.Visibility = Visibility.Collapsed;
        }

        private void MainScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            MagOverlayPanel.Visibility = Visibility.Collapsed;
            if (MainScrollViewer.Cursor == Cursors.Cross)
                MainScrollViewer.Cursor = Cursors.Arrow;
            if (BrushPreviewCursor != null)
                BrushPreviewCursor.Visibility = Visibility.Collapsed;
            if (BrushCrosshairCursor != null)
                BrushCrosshairCursor.Visibility = Visibility.Collapsed;
            if (EyedropperPreviewContainer != null)
                EyedropperPreviewContainer.Visibility = Visibility.Collapsed;
        }

        private void MainScrollViewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                ImageProcessingNodeControl.CompleteActiveCrop(_node, null))
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
            OpenImageAndAddTab();
        }

        private void BtnOpenImageTab_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            OpenImageAndAddTab();
        }

        private void OpenImageAndAddTab()
        {
            // Lưu state tab hiện tại trước khi mở ảnh mới
            if (_activeTabData != null)
            {
                SaveCurrentTabState();
            }

            // Mở file picker
            var oldUrl = _node.ImageUrl;
            ImageProcessingNodeControl.OpenImageFilePicker(_node);

            // Nếu user chọn ảnh mới (URL thay đổi) → tạo tab mới
            if (_node.ImageUrl != oldUrl && !string.IsNullOrWhiteSpace(_node.ImageUrl))
            {
                // Tạo EditorDoc mới cho ảnh mới
                _node.EditorDoc = null;

                // Đánh dấu cần tạo tab mới (không phải update tab cũ)
                _activeTabData = null;
            }
            // Tab sẽ được tạo/sync bởi SyncActiveTab sau khi UpdatePreviewAsync hoàn tất
        }

        #region IMAGE TAB STRIP (Photoshop-style) — with per-tab state

        /// <summary>Per-tab state lưu trữ ảnh + editor doc riêng cho mỗi tab.</summary>
        private class ImageTabData
        {
            public string Title { get; set; } = "";
            public string ImageUrl { get; set; } = "";
            public ImageInputMode InputMode { get; set; }
            public string ImageBase64 { get; set; } = "";
            public EditorDocument? EditorDoc { get; set; }
            public BitmapSource? CachedMainImage { get; set; }
            public Border? TabBorder { get; set; }

            // Layout & size states
            public double ImageWidth { get; set; } = double.NaN;
            public double ImageHeight { get; set; } = double.NaN;
            public double ZoomScaleX { get; set; } = 1.0;
            public double ZoomScaleY { get; set; } = 1.0;
            public double ScrollHorizontalOffset { get; set; } = 0.0;
            public double ScrollVerticalOffset { get; set; } = 0.0;

            // Selection state
            public Geometry? ActiveSelectionGeometry { get; set; }
            public bool IsSelecting { get; set; }
            public Point SelectionStartPoint { get; set; }
            public Rect? SelectionRect { get; set; }
            public System.Collections.Generic.List<Point> SelectionPoints { get; set; } = new();
            public bool[,]? CachedSelectionMask { get; set; }
            public int CachedSelectionStartX { get; set; }
            public int CachedSelectionStartY { get; set; }
            public int CachedSelectionEndX { get; set; }
            public int CachedSelectionEndY { get; set; }
            public bool HasCachedSelectionMask { get; set; }
        }

        private readonly List<ImageTabData> _tabs = new();
        private ImageTabData? _activeTabData;
        private bool _isSwitchingTab;

        /// <summary>Kiểm tra title thay đổi và sync vào tab strip.</summary>
        private void SyncActiveTab()
        {
            if (ImageTitleTextBlock == null || ImageTabStrip == null) return;
            string title = ImageTitleTextBlock.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(title) || title == "Chưa có ảnh" || title == "Đang tải ảnh...") return;
            if (_isSwitchingTab) return;

            if (_activeTabData != null)
            {
                // Cập nhật title của tab đang active
                _activeTabData.Title = title;
                if (_activeTabData.TabBorder != null)
                {
                    _activeTabData.TabBorder.Tag = title;
                    // Cập nhật TextBlock title trong tab
                    if (_activeTabData.TabBorder.Child is DockPanel dp)
                    {
                        foreach (var c in dp.Children)
                        {
                            if (c is TextBlock tb && tb.FontSize == 10)
                            {
                                tb.Text = title;
                                break;
                            }
                        }
                    }
                }
                // Lưu state hiện tại
                SaveCurrentTabState();
                return;
            }

            // Chưa có tab nào → tạo tab mới
            var tabData = new ImageTabData { Title = title };
            
            // Clear selection so the new tab starts fresh
            ClearSelection();

            SaveStateToTabData(tabData);
            var tabBorder = BuildTabItem(tabData);
            tabData.TabBorder = tabBorder;
            _tabs.Add(tabData);
            ImageTabStrip.Children.Add(tabBorder);
            _activeTabData = tabData;
            HighlightActiveTab();
        }

        private void SaveCurrentTabState()
        {
            if (_activeTabData == null) return;
            ForceClearDrawingOverlay();
            SaveStateToTabData(_activeTabData);
        }

        private void SaveStateToTabData(ImageTabData data)
        {
            data.ImageUrl = _node.ImageUrl ?? "";
            data.InputMode = _node.InputMode;
            data.ImageBase64 = _node.ImageBase64 ?? "";
            data.EditorDoc = _node.EditorDoc;
            data.CachedMainImage = MainImage.Source as BitmapSource;
            data.ImageWidth = MainImage.Width;
            data.ImageHeight = MainImage.Height;
            data.ZoomScaleX = ImageZoomScale.ScaleX;
            data.ZoomScaleY = ImageZoomScale.ScaleY;
            data.ScrollHorizontalOffset = MainScrollViewer.HorizontalOffset;
            data.ScrollVerticalOffset = MainScrollViewer.VerticalOffset;

            // Selection state
            data.ActiveSelectionGeometry = _activeSelectionGeometry;
            data.IsSelecting = _isSelecting;
            data.SelectionStartPoint = _selectionStartPoint;
            data.SelectionRect = _selectionRect;
            data.SelectionPoints = _selectionPoints.ToList();
            data.CachedSelectionMask = _cachedSelectionMask;
            data.CachedSelectionStartX = _cachedSelectionStartX;
            data.CachedSelectionStartY = _cachedSelectionStartY;
            data.CachedSelectionEndX = _cachedSelectionEndX;
            data.CachedSelectionEndY = _cachedSelectionEndY;
            data.HasCachedSelectionMask = _hasCachedSelectionMask;
        }

        private void LoadTabState(ImageTabData data)
        {
            _isSwitchingTab = true;
            try
            {
                // Khôi phục node state
                _node.ImageUrl = data.ImageUrl;
                _node.InputMode = data.InputMode;
                _node.ImageBase64 = data.ImageBase64;
                _node.EditorDoc = data.EditorDoc;

                // Khôi phục ảnh hiển thị
                if (data.CachedMainImage != null)
                {
                    MainImage.Source = data.CachedMainImage;
                    PlaceholderTextBlock.Visibility = Visibility.Collapsed;
                }

                // Restore image dimensions and layout scale
                MainImage.Width = data.ImageWidth;
                MainImage.Height = data.ImageHeight;
                ImageZoomScale.ScaleX = data.ZoomScaleX;
                ImageZoomScale.ScaleY = data.ZoomScaleY;

                // Sync editor panel
                if (_node.EditorDoc != null)
                {
                    EditorPanel.SetDocument(_node.EditorDoc);
                }

                // Cập nhật title
                ImageTitleTextBlock.Text = data.Title;

                // Khôi phục selection state
                _activeSelectionGeometry = data.ActiveSelectionGeometry;
                _isSelecting = data.IsSelecting;
                _selectionStartPoint = data.SelectionStartPoint;
                _selectionRect = data.SelectionRect;
                _selectionPoints.Clear();
                if (data.SelectionPoints != null)
                {
                    _selectionPoints.AddRange(data.SelectionPoints);
                }
                _cachedSelectionMask = data.CachedSelectionMask;
                _cachedSelectionStartX = data.CachedSelectionStartX;
                _cachedSelectionStartY = data.CachedSelectionStartY;
                _cachedSelectionEndX = data.CachedSelectionEndX;
                _cachedSelectionEndY = data.CachedSelectionEndY;
                _hasCachedSelectionMask = data.HasCachedSelectionMask;

                _activeTabData = data;

                // Redraw selection for this tab
                UpdatePolygonDisplay();

                // Restore scroll positions after layout updates
                MainScrollViewer.UpdateLayout();
                MainScrollViewer.ScrollToHorizontalOffset(data.ScrollHorizontalOffset);
                MainScrollViewer.ScrollToVerticalOffset(data.ScrollVerticalOffset);
            }
            finally
            {
                _isSwitchingTab = false;
            }
        }

        private Border BuildTabItem(ImageTabData tabData)
        {
            var tab = new Border
            {
                Tag = tabData.Title,
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Padding = new Thickness(8, 2, 4, 2),
                Margin = new Thickness(0, 0, 1, 0),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0, 0, 0, 2),
                BorderBrush = System.Windows.Media.Brushes.Transparent
            };

            var dock = new DockPanel { LastChildFill = true };

            // Close button (×)
            var closeBtn = new TextBlock
            {
                Text = "×",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x86, 0x96)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            closeBtn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                CloseTab(tabData);
            };
            closeBtn.MouseEnter += (s, _) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
            closeBtn.MouseLeave += (s, _) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x86, 0x96));
            DockPanel.SetDock(closeBtn, Dock.Right);
            dock.Children.Add(closeBtn);

            // Tab title
            var titleTb = new TextBlock
            {
                Text = tabData.Title,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEF)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 140
            };
            dock.Children.Add(titleTb);

            tab.Child = dock;

            // Click to switch tab
            tab.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                SwitchToTab(tabData);
            };

            return tab;
        }

        private void SwitchToTab(ImageTabData tabData)
        {
            if (ReferenceEquals(tabData, _activeTabData)) return;

            // Lưu state tab hiện tại
            SaveCurrentTabState();

            // Load state tab được chọn
            LoadTabState(tabData);
            HighlightActiveTab();
        }

        private void HighlightActiveTab()
        {
            var accentColor = Color.FromRgb(0x00, 0xCF, 0xFF);
            var transparentBrush = System.Windows.Media.Brushes.Transparent;
            var activeBg = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            var inactiveBg = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));

            foreach (var td in _tabs)
            {
                if (td.TabBorder == null) continue;
                bool isActive = ReferenceEquals(td, _activeTabData);
                td.TabBorder.Background = isActive ? activeBg : inactiveBg;
                td.TabBorder.BorderBrush = isActive ? new SolidColorBrush(accentColor) : transparentBrush;
            }
        }

        private void CloseTab(ImageTabData tabData)
        {
            if (tabData.TabBorder != null)
                ImageTabStrip.Children.Remove(tabData.TabBorder);
            _tabs.Remove(tabData);

            if (ReferenceEquals(tabData, _activeTabData))
            {
                _activeTabData = null;
                if (_tabs.Count > 0)
                {
                    SwitchToTab(_tabs[_tabs.Count - 1]);
                }
                else
                {
                    // Không còn tab nào
                    MainImage.Source = null;
                    PlaceholderTextBlock.Visibility = Visibility.Visible;
                    PlaceholderTextBlock.Text = "Chưa có ảnh";
                    ImageTitleTextBlock.Text = "Chưa có ảnh";
                }
            }
            HighlightActiveTab();
        }

        /// <summary>UpdatePreviewAsync rồi sync tab title + tạo EditorDoc nếu cần.</summary>
        private async System.Threading.Tasks.Task UpdatePreviewAndSyncTab()
        {
            if (!_isSwitchingTab)
            {
                ForceClearDrawingOverlay();
                _node.EditorDoc = null;
                _activeTabData = null;
            }

            await ImageProcessingNodeControl.UpdatePreviewAsync(
                _node, _host, MainImage, PlaceholderTextBlock, ImageZoomScale,
                ImageAreaGrid, MainScrollViewer, ImageTitleTextBlock,
                _onCropClickForIp);

            // Tạo EditorDoc cho ảnh mới nếu chưa có (quan trọng cho new tabs)
            if (_node.EditorDoc == null && MainImage.Source is BitmapSource bmp)
            {
                _node.EditorDoc = Models.ImageEditor.EditorDocument.FromBitmapSource(bmp);
                EditorPanel.SetDocument(_node.EditorDoc);
            }

            SyncActiveTab();
        }

        #endregion

        private void ColorCropButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void CropToolButton_Click(object sender, RoutedEventArgs e)
        {
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
                ImageProcessingNodeControl.CompleteActiveCrop(_node, null);
            };

            if (_chromeBorder != null)
                _chromeBorder.MouseRightButtonUp += ChromeOrSelf_MouseRightButtonUp;
            else
                MouseRightButtonUp += ChromeOrSelf_MouseRightButtonUp;
        }

        private double _lastWidgetIpDipScale = -1.0;

        private const double BaseTitleFontSize = 11;
        private const double BasePlaceholderFontSize = 16;
        private const double BaseCropLabelFontSize = 11;
        /// <summary>Widget: nhãn “Danh sách ảnh cắt” / “Ảnh render” to hơn một chút so với chỉ typoMul.</summary>
        private const double WidgetSectionLabelFontBoost = 1.2;
        /// <summary>Widget: order/check/delete + tiêu đề nhóm render trong list.</summary>
        private const double WidgetListTemplateFontBoost = 1.12;
        /// <summary>Widget: nhân thêm lên ipDip trước khi build cột Image Processor (font/size DIP).</summary>
        private const double WidgetIpColumnScaleBoost = 1.1;
        private const double WidgetIpColumnScaleMin = 0.94;
        private const double WidgetIpColumnScaleMax = 1.68;

        private static void PutFontResource(ResourceDictionary r, string key, double px) =>
            r[key] = px;

        private void ApplyWidgetTypographyResources(double typoMul)
        {
            double m = typoMul * WidgetListTemplateFontBoost;
            int F(double b) => Math.Max(1, (int)Math.Round(b * m));
            PutFontResource(Resources, "WidgetCropOrderFontSize", F(10));
            PutFontResource(Resources, "WidgetCropCheckFontSize", F(8));
            PutFontResource(Resources, "WidgetCropDeleteFontSize", F(9));
            PutFontResource(Resources, "WidgetRenderGroupTitleFontSize", F(9));
        }

        private void RebuildWidgetImageProcessorIfNeeded(double widgetIpDip)
        {
            if (_chromeBorder != null || !_freezeScaleInWidget) return;
            if (Math.Abs(widgetIpDip - _lastWidgetIpDipScale) < 0.06 && IpProcessorHost.Content != null)
                return;
            _lastWidgetIpDipScale = widgetIpDip;
            var (fe, setIp) = ImageProcessingNodeControl.BuildImageProcessorColumn(
                _node, _host, preventScaleUp: false, widgetDipScale: widgetIpDip, dipNativeLayout: true);
            IpProcessorHost.Content = fe;
            _setIpImage = setIp;
            SyncIpToggleIcon();
        }

        private static double CurvedChromeScale(double effDimension, double baseline)
        {
            if (baseline <= 0 || effDimension <= 0) return 1.0;
            var ratio = effDimension / baseline;
            var s = Math.Pow(ratio, ChromeScaleGamma);
            return Math.Max(ChromeScaleMin, Math.Min(ChromeScaleMax, s));
        }

        private void ApplyResponsiveScale()
        {
            if (_chromeBorder == null && _freezeScaleInWidget)
            {
                // LeftMenuViewbox.StretchDirection = StretchDirection.DownOnly;

                double widgetW = ActualWidth > 1 ? ActualWidth : WidthSyncTarget.ActualWidth;
                double widgetH = ActualHeight > 1 ? ActualHeight : WidthSyncTarget.ActualHeight;
                if (widgetW <= 1) widgetW = 1000;
                if (widgetH <= 1) widgetH = 650;

                bool ipOpen = IpColumnDefinition.Width.GridUnitType == GridUnitType.Star
                               && IpColumnDefinition.Width.Value > 0.0001;
                double denom = OtherSiblingStars + (ipOpen ? IpColStar : 0);
                double ipPxApprox = ipOpen && widgetW > 20
                    ? widgetW * (IpColStar / denom)
                    : 260.0;
                double ipDipRaw = Math.Clamp(ipPxApprox / 260.0, 0.92, 1.55);
                double ipDip = Math.Clamp(ipDipRaw * WidgetIpColumnScaleBoost, WidgetIpColumnScaleMin, WidgetIpColumnScaleMax);
                ipDip = Math.Round(ipDip * 8) / 8.0;

                double hScale = CurvedChromeScale(Math.Min(widgetH, ChromeScaleCapHeight), 640.0);
                double wScale = CurvedChromeScale(Math.Min(widgetW, ChromeScaleCapWidth), 900.0);
                double typoMul = Math.Clamp(Math.Max(hScale, wScale), 0.92, 1.38);
                typoMul = Math.Round(typoMul * 8) / 8.0;
                if (_widgetExpandedFullscreen)
                    typoMul = Math.Max(typoMul, 1.04);

                RootLayout.ColumnDefinitions[0].Width = GridLength.Auto;

                var identity = Transform.Identity;
                TopMenuBorder.LayoutTransform = identity;
                // RightMenuBorder.LayoutTransform = identity;
                IpProcessorHost.LayoutTransform = identity;
                // LeftMenuBorder.LayoutTransform = identity;
                PlaceholderTextBlock.LayoutTransform = identity;
                // CropsLabelText.LayoutTransform = identity;
                // RenderLabelText.LayoutTransform = identity;
                EditorPanel.LayoutTransform = new ScaleTransform(typoMul, typoMul);

                var formatMode = typoMul == 1.0 ? TextFormattingMode.Display : TextFormattingMode.Ideal;
                TextOptions.SetTextFormattingMode(EditorPanel, formatMode);
                // TextOptions.SetTextFormattingMode(RightMenuBorder, formatMode);
                TextOptions.SetTextFormattingMode(IpProcessorHost, formatMode);

                int Sz(double b) => Math.Max(1, (int)Math.Round(b * typoMul));
                double leftBtnMul = typoMul * (_widgetExpandedFullscreen ? 0.78 : 1.0);
                int B(double x) => Math.Max(1, (int)Math.Round(x * leftBtnMul));

                ImageTitleTextBlock.FontSize = Sz(BaseTitleFontSize);
                PlaceholderTextBlock.FontSize = Sz(BasePlaceholderFontSize);
                // CropsLabelText.FontSize = Sz(BaseCropLabelFontSize * WidgetSectionLabelFontBoost);
                // RenderLabelText.FontSize = Sz(BaseCropLabelFontSize * WidgetSectionLabelFontBoost);
                MagCoordTextBlock.FontSize = Sz(9);

                // ColorCropButton.Width = B(30);
                // ColorCropButton.Height = B(30);

                IpToggleButton.Width = B(28);
                IpToggleButton.Height = B(22);
                IpToggleIcon.Width = B(14);
                IpToggleIcon.Height = B(14);

                ApplyWidgetTypographyResources(typoMul);
                RebuildWidgetImageProcessorIfNeeded(ipDip);

                ImageProcessingNodeControl.UpdateInteractionVisualScale(_handleOverlay, _node, typoMul);
                return;
            }

            // LeftMenuViewbox.StretchDirection = StretchDirection.Both;
            RootLayout.ColumnDefinitions[0].Width = new GridLength(0.6, GridUnitType.Star);

            PutFontResource(Resources, "WidgetCropOrderFontSize", 10);
            PutFontResource(Resources, "WidgetCropCheckFontSize", 8);
            PutFontResource(Resources, "WidgetCropDeleteFontSize", 9);
            PutFontResource(Resources, "WidgetRenderGroupTitleFontSize", 9);

            ImageTitleTextBlock.FontSize = BaseTitleFontSize;
            PlaceholderTextBlock.FontSize = BasePlaceholderFontSize;
            // CropsLabelText.FontSize = BaseCropLabelFontSize;
            // RenderLabelText.FontSize = BaseCropLabelFontSize;
            MagCoordTextBlock.FontSize = 9;

            // CropToolButton.Width = 30;
            // CropToolButton.Height = 30;
            // CropToolButton.FontSize = 16;
            // ColorCropButton.Width = 30;
            // ColorCropButton.Height = 30;
            IpToggleButton.Width = 28;
            IpToggleButton.Height = 22;
            IpToggleIcon.Width = 14;
            IpToggleIcon.Height = 14;

            double heightBaseline = WidthSyncTarget.MinHeight > 0 ? WidthSyncTarget.MinHeight : 600.0;
            double widthBaseline = WidthSyncTarget.MinWidth > 0 ? WidthSyncTarget.MinWidth : 800.0;

            double effH = Math.Min(_node.Height, ChromeScaleCapHeight);
            double effW = Math.Min(_node.Width, ChromeScaleCapWidth);

            double heightScaleFactor = CurvedChromeScale(effH, heightBaseline);
            double widthScaleFactor = CurvedChromeScale(effW, widthBaseline);
            double ipBaselineWidth = widthBaseline * (IpColStar / (OtherSiblingStars + IpColStar));
            double ipCurrentWidth = effW * (IpColStar / (OtherSiblingStars + IpColStar));
            double ipTextScaleFactor = CurvedChromeScale(ipCurrentWidth, ipBaselineWidth);

            var topBarScale = new ScaleTransform(heightScaleFactor, heightScaleFactor);
            TopMenuBorder.LayoutTransform = topBarScale;
            IpProcessorHost.LayoutTransform = topBarScale;
            // var ipScale = new ScaleTransform(ipTextScaleFactor, ipTextScaleFactor);
            // RightMenuBorder.LayoutTransform = ipScale;
            // TextOptions.SetTextFormattingMode(RightMenuBorder, TextFormattingMode.Ideal);
            TextOptions.SetTextFormattingMode(IpProcessorHost, TextFormattingMode.Ideal);

            // Tính toán tỉ lệ co dãn của EditorPanel để vừa khít chiều rộng cột 2 (220px)
            double editorScaleVal = Math.Max(0.5, (_node.Width * 0.32) / 220.0);
            EditorPanel.LayoutTransform = new ScaleTransform(editorScaleVal, editorScaleVal);
            TextOptions.SetTextFormattingMode(EditorPanel, TextFormattingMode.Ideal);

            var canvasIpTextTransform = new ScaleTransform(ipTextScaleFactor, ipTextScaleFactor);
            PlaceholderTextBlock.LayoutTransform = canvasIpTextTransform;
            // CropsLabelText.LayoutTransform = Transform.Identity;
            // RenderLabelText.LayoutTransform = Transform.Identity;

            // LeftMenuBorder.LayoutTransform = new ScaleTransform(widthScaleFactor, widthScaleFactor);

            var interactionScale = Math.Max(Math.Max(heightScaleFactor, widthScaleFactor), ipTextScaleFactor);
            ImageProcessingNodeControl.UpdateInteractionVisualScale(_handleOverlay, _node, interactionScale);
        }

        private void ChromeOrSelf_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var src = e.OriginalSource as DependencyObject;
            bool IsInside(FrameworkElement? fe) =>
                src != null && fe != null && VisualTreeHelper.GetParent(src) != null && fe.IsAncestorOf(src);

            if (!IsInside(MainScrollViewer) &&
                // !IsInside(LeftMenuBorder) &&
                !IsInside(EditorToolbox) &&
                !IsInside(EditorPanel) &&
                !IsInside(IpProcessorHost))
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
                if (!_isSwitchingTab)
                    _ = UpdatePreviewAndSyncTab();
            }
            else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
            {
                ApplyHostBackground();
            }
        }

        private void ImageProcessingNodeContentControl_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyResponsiveScale();
            SyncIpToggleIcon();

            // Warm up SkiaSharp on a background thread to prevent first-draw lag
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var info = new SkiaSharp.SKImageInfo(10, 10, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                    using (var surface = SkiaSharp.SKSurface.Create(info))
                    {
                        var canvas = surface.Canvas;
                        canvas.Clear(SkiaSharp.SKColors.Transparent);
                    }
                }
                catch { }
            });

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                SyncFromEditorPanelBrushProperties();
            }

            _ = LoadPreviewAndSyncEditor();
        }

        private async System.Threading.Tasks.Task LoadPreviewAndSyncEditor()
        {
            await UpdatePreviewAndSyncTab();

            // Sau khi ảnh đã load xong, sync vào EditorDoc nếu đang ở mode Manual
            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual
                && MainImage.Source is BitmapSource bmp)
            {
                if (_node.EditorDoc != null
                    && (_node.EditorDoc.Width != bmp.PixelWidth || _node.EditorDoc.Height != bmp.PixelHeight))
                {
                    // Kích thước ảnh khác doc → tạo lại document mới từ ảnh thực
                    _node.EditorDoc = Models.ImageEditor.EditorDocument.FromBitmapSource(bmp);
                    EditorPanel.SetDocument(_node.EditorDoc);
                }
                else if (_node.EditorDoc != null)
                {
                    // Cùng kích thước → giữ nguyên layers, chỉ re-composite
                    OnEditorDocumentModified();
                }
            }
        }

        private void ImageProcessingNodeContentControl_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                CommitKeyMoveSession();

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
        // ═══════ MODE SWITCHING (AI ↔ Editor) ═══════

        // private void BtnModeAI_Click(object sender, RoutedEventArgs e)
        // {
        //     SwitchToMode(Models.Nodes.ImageProcessingMode.AI);
        //     e.Handled = true;
        // }
        // 
        // private void BtnModeEditor_Click(object sender, RoutedEventArgs e)
        // {
        //     SwitchToMode(Models.Nodes.ImageProcessingMode.Manual);
        //     e.Handled = true;
        // }

        private void SwitchToMode(Models.Nodes.ImageProcessingMode mode)
        {
            // Luôn cưỡng ép chạy ở chế độ chỉnh sửa thủ công (Editor/Manual) vì cơ chế AI đã chuyển sang LayerAiDialog
            _node.ProcessingMode = Models.Nodes.ImageProcessingMode.Manual;

            // Ẩn AI panels, hiện Editor
            // RightMenuBorder.Visibility = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Visible;
            // LeftMenuBorder.Visibility = Visibility.Collapsed;
            EditorToolbox.Visibility = Visibility.Visible;

            // Tắt cột Image Processor nếu đang mở
            if (_ipColumnVisible)
            {
                ToggleIPColumn();
            }

            // Tạo EditorDocument nếu chưa có
            EnsureEditorDocument();

            // Sync toolbox visual state với active tool
            SyncToolboxHighlight();
            SyncToolboxColors();
            SyncModeButtonStyles();
        }

        private void EnsureEditorDocument()
        {
            ClearSelection();
            var imgSource = MainImage.Source as BitmapSource;

            if (_node.EditorDoc != null)
            {
                if (imgSource != null)
                {
                    // Kiểm tra kích thước: nếu ảnh khác doc → tạo lại từ ảnh thực
                    if (_node.EditorDoc.Width != imgSource.PixelWidth
                        || _node.EditorDoc.Height != imgSource.PixelHeight)
                    {
                        _node.EditorDoc = Models.ImageEditor.EditorDocument.FromBitmapSource(imgSource);
                    }
                    // Nếu cùng kích thước → giữ nguyên mọi layer, KHÔNG overwrite.
                    // Pixel data đã được user chỉnh sửa trên tất cả layers, phải bảo toàn.
                }
                EditorPanel.SetDocument(_node.EditorDoc);
                _node.EditorDoc.PropertyChanged -= EditorDoc_PropertyChanged;
                _node.EditorDoc.PropertyChanged += EditorDoc_PropertyChanged;
                // Re-composite để canvas hiển thị composite mới nhất
                OnEditorDocumentModified();
                return;
            }

            // Tạo document từ ảnh hiện tại (nếu có) hoặc tạo blank
            if (imgSource != null)
            {
                _node.EditorDoc = Models.ImageEditor.EditorDocument.FromBitmapSource(imgSource);
            }
            else
            {
                _node.EditorDoc = Models.ImageEditor.EditorDocument.CreateBlank(800, 600);
            }

            EditorPanel.SetDocument(_node.EditorDoc);
            _node.EditorDoc.PropertyChanged -= EditorDoc_PropertyChanged;
            _node.EditorDoc.PropertyChanged += EditorDoc_PropertyChanged;
        }

        private bool _compositeScheduled;

        private void OnEditorDocumentModified()
        {
            if (_node.EditorDoc == null) return;

            // Always ensure PropertyChanged handler is registered on the active document and swatches are synced
            _node.EditorDoc.PropertyChanged -= EditorDoc_PropertyChanged;
            _node.EditorDoc.PropertyChanged += EditorDoc_PropertyChanged;
            SyncToolboxColors();

            // Coalesce: nếu đã có schedule thì không cần thêm
            if (_compositeScheduled) return;
            _compositeScheduled = true;

            // Defer composite sang cuối vòng lặp Dispatcher để không block UI
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {
                _compositeScheduled = false;
                if (_node.EditorDoc == null) return;

                try
                {
                    var composite = _node.EditorDoc.Composite();
                    MainImage.Source = composite;
                    MainImage.Width = _node.EditorDoc.Width;
                    MainImage.Height = _node.EditorDoc.Height;
                }
                catch (Exception ex)
                {
                    try
                    {
                        System.IO.File.WriteAllText(@"d:\_DuAn\App_Desktop\workflows\Flow-My\composite_error.txt", "Composite error:\n" + ex.ToString());
                    }
                    catch { }
                }
                finally
                {
                    // Hide active layer drawing overlay seamlessly after composite rendering has completed
                    ActiveLayerDrawingOverlay.Visibility = Visibility.Collapsed;
                    ActiveLayerDrawingOverlay.Source = null;
                }

                try
                {
                    UpdateTransformOverlayDisplay();
                }
                catch (Exception ex)
                {
                    try
                    {
                        System.IO.File.WriteAllText(@"d:\_DuAn\App_Desktop\workflows\Flow-My\composite_error.txt", "UpdateOverlay error:\n" + ex.ToString());
                    }
                    catch { }
                }
            }));
        }

        private void SyncModeButtonStyles()
        {
            bool isAI = _node.ProcessingMode == Models.Nodes.ImageProcessingMode.AI;

            // Active: accent bg + white text; Inactive: dark + muted
            var activeBg = new SolidColorBrush(Color.FromArgb(0x40, 0x4f, 0xff, 0xb0));
            var activeFg = new SolidColorBrush(Color.FromRgb(0xdd, 0xe3, 0xef));
            var inactiveBg = new SolidColorBrush(Color.FromArgb(0x18, 0xff, 0xff, 0xff));
            var inactiveFg = new SolidColorBrush(Color.FromRgb(0x5a, 0x60, 0x72));

            // BtnModeAI.Background = isAI ? activeBg : inactiveBg;
            // BtnModeAI.Foreground = isAI ? activeFg : inactiveFg;
            // BtnModeEditor.Background = isAI ? inactiveBg : activeBg;
            // BtnModeEditor.Foreground = isAI ? inactiveFg : activeFg;

            // AI mode có IP toggle, Editor mode ẩn nó
            IpToggleButton.Visibility = isAI ? Visibility.Visible : Visibility.Collapsed;
        }
        #region EDITOR TOOLBOX (Left vertical strip)

        private readonly Dictionary<string, Border> _toolboxBorders = new();

        private void InitToolboxBorders()
        {
            _toolboxBorders["Brush"] = TbxBrush;
            _toolboxBorders["Eraser"] = TbxEraser;
            _toolboxBorders["Fill"] = TbxFill;
            _toolboxBorders["Eyedropper"] = TbxEyedropper;
            _toolboxBorders["Move"] = TbxMove;
            _toolboxBorders["Text"] = TbxText;
            _toolboxBorders["Selection"] = TbxSelectionActive;
            _toolboxBorders["Lasso"] = TbxSelectionActive;
            _toolboxBorders["PolyLasso"] = TbxSelectionActive;
            _toolboxBorders["MagicWand"] = TbxSmartSelectionActive;
            _toolboxBorders["QuickSelection"] = TbxSmartSelectionActive;
            _toolboxBorders["ObjectSelection"] = TbxSmartSelectionActive;
            _toolboxBorders["CropCanvas"] = TbxCropActive;
            _toolboxBorders["Slice"] = TbxCropActive;
            _toolboxBorders["SliceSelect"] = TbxCropActive;
            _toolboxBorders["Transform"] = TbxCropActive;
        }

        private void EditorToolbox_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string toolName)
            {
                CommitKeyMoveSession();

                // If switching away from Text tool, commit active text editing!
                if (toolName != "Text" && _editingTextLayer != null)
                {
                    CommitActiveText();
                }

                // Delegate to EditorPanel's tool selection (keeps both in sync)
                EditorPanel.SelectToolByName(toolName);
                SyncToolboxHighlight();

                // If switching to Text tool, and the active layer is a text layer, enter editing mode!
                if (toolName == "Text" && _node.EditorDoc != null && _node.EditorDoc.ActiveLayer != null && _node.EditorDoc.ActiveLayer.IsTextLayer)
                {
                    EnterTextEditingMode(_node.EditorDoc.ActiveLayer);
                }

                e.Handled = true;
            }
        }



        private void EditorToolbox_FgColor_Click(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var color = PickColorDialog(_node.EditorDoc.ForegroundColor);
            if (color.HasValue)
            {
                _node.EditorDoc.ForegroundColor = color.Value;
                SyncToolboxColors();
            }
            e.Handled = true;
        }

        private void EditorToolbox_BgColor_Click(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var color = PickColorDialog(_node.EditorDoc.BackgroundColor);
            if (color.HasValue)
            {
                _node.EditorDoc.BackgroundColor = color.Value;
                SyncToolboxColors();
            }
            e.Handled = true;
        }

        private static Color? PickColorDialog(Color initial)
        {
            using var dlg = new WinForms.ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B),
                FullOpen = true,
                AnyColor = true
            };
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                var c = dlg.Color;
                return Color.FromArgb(c.A, c.R, c.G, c.B);
            }
            return null;
        }

        private void SyncToolboxHighlight()
        {
            if (_toolboxBorders.Count == 0) InitToolboxBorders();

            string activeTool = EditorPanel.ActiveToolName;

            // Auto-commit transform session if switching away from Transform tool
            if (activeTool != "Transform" && _transformSessionActive)
            {
                CommitTransformSession();
            }

            var activeBg = new SolidColorBrush(Color.FromArgb(0x30, 0x4f, 0xff, 0xb0));
            var activeBorder = new SolidColorBrush(Color.FromRgb(0x4f, 0xff, 0xb0));

            // 1. Clear all highlights first
            foreach (var border in _toolboxBorders.Values)
            {
                border.Background = Brushes.Transparent;
                border.BorderBrush = Brushes.Transparent;
            }

            // 2. Set active highlight
            if (_toolboxBorders.TryGetValue(activeTool, out var activeBorderElement) && activeBorderElement != null)
            {
                activeBorderElement.Background = activeBg;
                activeBorderElement.BorderBrush = activeBorder;
            }

            EditorPanel.UpdatePanelVisibilities(activeTool);
            UpdateTopOptionsBar(activeTool);

            if (activeTool != "Text" && TextMoveContainer != null && TextMoveContainer.Visibility == Visibility.Visible)
            {
                CommitActiveText();
            }

            UpdatePolygonDisplay();
            UpdateTransformOverlayDisplay(); // Ensure transform bounding box updates immediately when active tool changes!
        }

        private void SyncToolboxColors()
        {
            if (_node.EditorDoc == null) return;

            var fgBrush = new SolidColorBrush(_node.EditorDoc.ForegroundColor);
            var bgBrush = new SolidColorBrush(_node.EditorDoc.BackgroundColor);

            TbxFgColor.Background = fgBrush;
            TbxBgColor.Background = bgBrush;

            if (OptFgColorSwatch != null)
                OptFgColorSwatch.Background = fgBrush;
            if (OptBgColorSwatch != null)
                OptBgColorSwatch.Background = bgBrush;

            string hex = $"#{_node.EditorDoc.ForegroundColor.R:X2}{_node.EditorDoc.ForegroundColor.G:X2}{_node.EditorDoc.ForegroundColor.B:X2}";
            if (OptColorHexInput != null && OptColorHexInput.Text != hex)
            {
                OptColorHexInput.Text = hex;
            }
        }

        private void OptColorHexInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyColorFromHexInput();
        }

        private void OptColorHexInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyColorFromHexInput();
                MainScrollViewer.Focus();
                e.Handled = true;
            }
        }

        private void ApplyColorFromHexInput()
        {
            if (_node.EditorDoc == null || OptColorHexInput == null) return;
            try
            {
                var text = OptColorHexInput.Text.Trim();
                if (string.IsNullOrEmpty(text)) return;
                if (!text.StartsWith("#")) text = "#" + text;
                var color = (Color)ColorConverter.ConvertFromString(text);
                if (_node.EditorDoc.ForegroundColor != color)
                {
                    _node.EditorDoc.ForegroundColor = color;
                }
            }
            catch { /* ignore invalid hex format */ }
        }

        private void OptFgColorSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var color = PickColorDialog(_node.EditorDoc.ForegroundColor);
            if (color.HasValue)
            {
                _node.EditorDoc.ForegroundColor = color.Value;
            }
            e.Handled = true;
        }

        private void OptBgColorSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var color = PickColorDialog(_node.EditorDoc.BackgroundColor);
            if (color.HasValue)
            {
                _node.EditorDoc.BackgroundColor = color.Value;
            }
            e.Handled = true;
        }

        private void EditorToolbox_SwapColors_Click(object sender, RoutedEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var temp = _node.EditorDoc.ForegroundColor;
            _node.EditorDoc.ForegroundColor = _node.EditorDoc.BackgroundColor;
            _node.EditorDoc.BackgroundColor = temp;
        }

        private void EditorDoc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Models.ImageEditor.EditorDocument.ForegroundColor) ||
                e.PropertyName == nameof(Models.ImageEditor.EditorDocument.BackgroundColor))
            {
                SyncToolboxColors();
            }
        }

        // ═══════ MAGICK.NET EFFECTS (Async + Loading + ESC Cancel) ═══════

        private CancellationTokenSource? _fxCts;
        private bool _isFxRunning;
        private bool _isSpacePressed;

        // ── Parameter definitions for configurable effects ──
        private record FxParamDef(string Name, double Default, double Min, double Max, double Step = 1);

        private static readonly Dictionary<string, FxParamDef[]> _fxParamMap = new()
        {
            // Blur
            ["GaussianBlur"] = new[] { new FxParamDef("Radius", 3, 0, 30), new FxParamDef("Sigma", 1.5, 0.1, 15, 0.1) },
            ["MotionBlur"] = new[] { new FxParamDef("Radius", 8, 0, 40), new FxParamDef("Sigma", 4, 0.1, 20, 0.1), new FxParamDef("Angle", 0, -180, 180) },
            ["RadialBlur"] = new[] { new FxParamDef("Angle", 5, 0, 45) },
            ["AdaptiveBlur"] = new[] { new FxParamDef("Radius", 0, 0, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            ["Blur"] = new[] { new FxParamDef("Radius", 5, 0, 30), new FxParamDef("Sigma", 2, 0.1, 15, 0.1) },
            // Sharpen
            ["Sharpen"] = new[] { new FxParamDef("Radius", 0, 0, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            ["UnsharpMask"] = new[] { new FxParamDef("Radius", 2, 0, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1), new FxParamDef("Amount", 1, 0, 5, 0.1), new FxParamDef("Threshold", 0.05, 0, 1, 0.01) },
            ["AdaptiveSharpen"] = new[] { new FxParamDef("Radius", 0, 0, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            ["Kuwahara"] = new[] { new FxParamDef("Radius", 3, 1, 15), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            // Artistic
            ["OilPaint"] = new[] { new FxParamDef("Radius", 4, 1, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            ["Charcoal"] = new[] { new FxParamDef("Radius", 2, 0, 10), new FxParamDef("Sigma", 1, 0.1, 5, 0.1) },
            ["Sketch"] = new[] { new FxParamDef("Radius", 2, 0, 10), new FxParamDef("Sigma", 1, 0.1, 5, 0.1), new FxParamDef("Angle", 0, -180, 180) },
            ["Emboss"] = new[] { new FxParamDef("Radius", 0, 0, 10), new FxParamDef("Sigma", 1, 0.1, 5, 0.1) },
            ["Vignette"] = new[] { new FxParamDef("Sigma", 10, 1, 50) },
            ["Swirl"] = new[] { new FxParamDef("Degrees", 60, -360, 360) },
            ["Wave"] = new[] { new FxParamDef("Amplitude", 5, 1, 50), new FxParamDef("Length", 50, 5, 300) },
            ["Spread"] = new[] { new FxParamDef("Radius", 4, 1, 30) },
            ["Implode"] = new[] { new FxParamDef("Amount", 0.3, -1, 1, 0.05) },
            ["Explode"] = new[] { new FxParamDef("Amount", 0.3, 0, 1, 0.05) },
            ["Shade"] = new[] { new FxParamDef("Azimuth", 30, 0, 360), new FxParamDef("Elevation", 30, 0, 90) },
            ["Pixelate"] = new[] { new FxParamDef("BlockSize", 8, 2, 64) },
            ["Polaroid"] = new[] { new FxParamDef("Angle", 0, -30, 30) },
            ["Frame"] = new[] { new FxParamDef("Size", 6, 1, 20) },
            ["Raise"] = new[] { new FxParamDef("Size", 8, 1, 20) },
            // Edge
            ["EdgeDetect"] = new[] { new FxParamDef("Radius", 1, 0, 10) },
            ["CannyEdge"] = new[] { new FxParamDef("Radius", 0, 0, 10), new FxParamDef("Sigma", 1, 0.1, 5, 0.1), new FxParamDef("LowPct", 10, 0, 100), new FxParamDef("HighPct", 30, 0, 100) },
            ["Threshold"] = new[] { new FxParamDef("Percent", 50, 0, 100) },
            ["AdaptiveThreshold"] = new[] { new FxParamDef("Width", 10, 3, 30), new FxParamDef("Height", 10, 3, 30), new FxParamDef("Bias", 0, -10, 10, 0.5) },
            // Color / Brightness
            ["BrightnessUp"] = new[] { new FxParamDef("Brightness", 15, 1, 100) },
            ["BrightnessDown"] = new[] { new FxParamDef("Brightness", 15, 1, 100) },
            ["GammaCorrect"] = new[] { new FxParamDef("Gamma", 1.5, 0.1, 5, 0.1) },
            ["SaturationUp"] = new[] { new FxParamDef("Saturation", 140, 101, 300) },
            ["SaturationDown"] = new[] { new FxParamDef("Saturation", 60, 0, 99) },
            ["Posterize"] = new[] { new FxParamDef("Levels", 4, 2, 20) },
            ["Solarize"] = new[] { new FxParamDef("Threshold", 50, 0, 100) },
            ["SepiaTone"] = new[] { new FxParamDef("Threshold", 80, 0, 100) },
            ["SigmoidalContrastUp"] = new[] { new FxParamDef("Contrast", 3, 0.5, 20, 0.5), new FxParamDef("Midpoint", 50, 0, 100) },
            ["LinearStretch"] = new[] { new FxParamDef("BlackPct", 1, 0, 20, 0.5), new FxParamDef("WhitePct", 1, 0, 20, 0.5) },
            ["BlueShift"] = new[] { new FxParamDef("Factor", 1.5, 0.5, 3, 0.1) },
            ["QuantizeColors"] = new[] { new FxParamDef("Colors", 16, 2, 256) },
            ["ContrastDown"] = new[] { new FxParamDef("Contrast", 15, 1, 50) },
            // Noise
            ["AddNoiseGaussian"] = new[] { new FxParamDef("Attenuate", 1, 0.1, 10, 0.1) },
            ["AddNoiseImpulse"] = new[] { new FxParamDef("Attenuate", 1, 0.1, 10, 0.1) },
            ["MedianFilter"] = new[] { new FxParamDef("Radius", 2, 1, 10) },
            ["ReduceNoise"] = new[] { new FxParamDef("Order", 2, 1, 10) },
            // Morphology
            ["Dilate"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["MorphErode"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["Opening"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["Closing"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["EdgeIn"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["EdgeOut"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["TopHat"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["BottomHat"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            // Transform
            ["Deskew"] = new[] { new FxParamDef("Threshold", 40, 0, 100) },
            ["Shear"] = new[] { new FxParamDef("X", 15, -45, 45), new FxParamDef("Y", 0, -45, 45) },
            ["Roll"] = new[] { new FxParamDef("X", 50, -200, 200), new FxParamDef("Y", 50, -200, 200) },
            ["Shave"] = new[] { new FxParamDef("Pixels", 10, 1, 100) },
        };

        /// <summary>Show dark-themed parameter dialog. Returns null if cancelled.</summary>
        private Dictionary<string, double>? ShowFxParamDialog(string effectName, FxParamDef[] paramDefs)
        {
            var win = new Window
            {
                Title = effectName,
                Width = 340,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x0a, 0x0c, 0x10)),
                Foreground = System.Windows.Media.Brushes.White,
                WindowStyle = WindowStyle.ToolWindow,
            };

            var stack = new StackPanel { Margin = new Thickness(14) };

            // Title
            stack.Children.Add(new TextBlock
            {
                Text = effectName,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4f, 0xff, 0xb0)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Load cached values (last-used) nếu có
            var cachedParams = FlowMy.Utils.FxConfigCache.Get(effectName);

            var sliders = new Dictionary<string, Slider>();

            foreach (var p in paramDefs)
            {
                var dp = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

                var lbl = new TextBlock
                {
                    Text = p.Name,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x90, 0x96, 0xa8)),
                    FontSize = 10,
                    Width = 80,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(lbl, Dock.Left);
                dp.Children.Add(lbl);

                var valBorder = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x11, 0x13, 0x18)),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(4, 1, 4, 1),
                    MinWidth = 42,
                };
                // Dùng cached value nếu có, nếu không dùng default
                double initialValue = p.Default;
                if (cachedParams != null && cachedParams.TryGetValue(p.Name, out var cached))
                    initialValue = Math.Max(p.Min, Math.Min(p.Max, cached));

                var valText = new TextBlock
                {
                    Text = initialValue.ToString(p.Step < 1 ? "F2" : "F0"),
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x00, 0xcf, 0xff)),
                    FontSize = 9,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                valBorder.Child = valText;
                DockPanel.SetDock(valBorder, Dock.Right);
                dp.Children.Add(valBorder);

                var slider = new Slider
                {
                    Minimum = p.Min,
                    Maximum = p.Max,
                    Value = initialValue,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 6, 0),
                    TickFrequency = p.Step,
                    IsSnapToTickEnabled = p.Step >= 1,
                    SmallChange = p.Step,
                    LargeChange = p.Step * 5,
                };
                var capturedP = p; // capture for closure
                slider.ValueChanged += (_, args) =>
                {
                    valText.Text = args.NewValue.ToString(capturedP.Step < 1 ? "F2" : "F0");
                };
                dp.Children.Add(slider);
                sliders[p.Name] = slider;

                stack.Children.Add(dp);
            }

            // Buttons row
            Dictionary<string, double>? result = null;
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var btnApply = new Button
            {
                Content = "✓ Apply",
                Padding = new Thickness(14, 5, 14, 5),
                FontSize = 10,
                Cursor = Cursors.Hand,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4f, 0xff, 0xb0)),
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x0a, 0x0c, 0x10)),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(4, 0, 0, 0),
            };
            btnApply.Click += (_, __) =>
            {
                result = new Dictionary<string, double>();
                foreach (var kv in sliders)
                    result[kv.Key] = kv.Value.Value;

                // Lưu vào cache để lần sau mở lên sẽ dùng value này
                FlowMy.Utils.FxConfigCache.Set(effectName, result);

                win.DialogResult = true;
                win.Close();
            };

            var btnCancel = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(10, 5, 10, 5),
                FontSize = 10,
                Cursor = Cursors.Hand,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x25, 0x29, 0x32)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x3a, 0x3e, 0x4a)),
                BorderThickness = new Thickness(1),
            };
            btnCancel.Click += (_, __) => { win.DialogResult = false; win.Close(); };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnApply);
            stack.Children.Add(btnPanel);

            win.Content = stack;
            win.ShowDialog();
            return result;
        }

        private Dictionary<string, double>? _lastFxParams;

        private async void MagickEffect_Click(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null || _isFxRunning) return;
            CommitBrushDrawingSession();
            var layer = _node.EditorDoc.ActiveLayer;
            if (layer == null || layer.IsLocked || !layer.IsVisible) return;

            if (sender is not Border border || border.Tag is not string effectName)
                return;

            e.Handled = true;

            // Intercept rotation and flip preset effects to perform canvas rotation and layer flips directly
            if (effectName == "Rotate90" || effectName == "Rotate180" || effectName == "Rotate270")
            {
                double angle = effectName switch
                {
                    "Rotate90" => 90,
                    "Rotate180" => 180,
                    "Rotate270" => 270,
                    _ => 0
                };
                if (angle != 0)
                {
                    _node.EditorDoc.RotateCanvas(angle);
                    OnEditorDocumentModified();
                    UpdateTransformOverlayDisplay();
                    return;
                }
            }
            else if (effectName == "Flop" || effectName == "Flip")
            {
                FlipActiveLayerImmediate(horizontal: effectName == "Flop");
                return;
            }

            // Show parameter dialog if effect has configurable params
            Dictionary<string, double>? fxParams = null;
            if (_fxParamMap.TryGetValue(effectName, out var paramDefs))
            {
                fxParams = ShowFxParamDialog(effectName, paramDefs);
                if (fxParams == null) return; // User cancelled
            }
            _lastFxParams = fxParams;

            // Snapshot old pixels for undo (on UI thread) — single copy, reused as source
            int w = layer.Width, h = layer.Height;
            int stride = w * 4;
            var oldPixels = new byte[stride * h];
            layer.Bitmap.CopyPixels(oldPixels, stride, 0);

            // Show loading
            _isFxRunning = true;
            _fxCts = new CancellationTokenSource();
            FxLoadingText.Text = $"Đang xử lý: {effectName}...";
            FxLoadingOverlay.Visibility = Visibility.Visible;

            // Listen for ESC
            PreviewKeyDown += FxEscHandler;

            byte[]? newPixels = null;
            try
            {
                var token = _fxCts.Token;

                // Run Magick effect on background thread
                // oldPixels is used as source directly (no extra copy needed)
                newPixels = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    var settings = new ImageMagick.MagickReadSettings
                    {
                        Width = (uint)w,
                        Height = (uint)h,
                        Format = ImageMagick.MagickFormat.Bgra,
                        Depth = 8
                    };
                    using var img = new ImageMagick.MagickImage(oldPixels, settings);

                    token.ThrowIfCancellationRequested();

                    // Apply effect
                    bool isTransformOrWarp = effectName.StartsWith("Rotate") || 
                                             effectName == "Flop" || 
                                             effectName == "Flip" || 
                                             effectName == "Deskew" || 
                                             effectName == "Trim" || 
                                             effectName == "AutoOrient" || 
                                             effectName == "Shear" || 
                                             effectName == "Roll" || 
                                             effectName == "Shave" || 
                                             effectName == "Magnify" || 
                                             effectName == "Swirl" || 
                                             effectName == "Wave" || 
                                             effectName == "Implode" || 
                                             effectName == "Explode" || 
                                             effectName == "Polaroid";

                    bool useTiles = !isTransformOrWarp && ImageProcessingOptimization.ShouldUseTileProcessing((int)img.Width, (int)img.Height);
                    ImageMagick.MagickImage resultImg;
                    if (useTiles)
                    {
                        resultImg = ImageProcessingOptimization.ProcessLargeImageInTiles(img, (tile) => {
                            ApplyMagickEffectToImage(tile, effectName, fxParams);
                        }, progress: null, cancellationToken: token).GetAwaiter().GetResult();
                    }
                    else
                    {
                        ApplyMagickEffectToImage(img, effectName, fxParams);
                        resultImg = img;
                    }

                    token.ThrowIfCancellationRequested();

                    // Convert back to raw BGRA
                    resultImg.Alpha(ImageMagick.AlphaOption.Set);
                    int rw = (int)resultImg.Width, rh = (int)resultImg.Height;

                    byte[]? finalBytes = null;
                    // Fast path: sizes match → return raw bytes directly
                    if (rw == w && rh == h)
                    {
                        finalBytes = resultImg.ToByteArray(ImageMagick.MagickFormat.Bgra);
                    }
                    else
                    {
                        // Sizes differ: copy into output matching layer size
                        var resultBytes = resultImg.ToByteArray(ImageMagick.MagickFormat.Bgra);
                        var output = new byte[stride * h];
                        int copyW = Math.Min(w, rw);
                        int copyH = Math.Min(h, rh);
                        int rStride = rw * 4;
                        for (int y = 0; y < copyH; y++)
                        {
                            Buffer.BlockCopy(resultBytes, y * rStride, output, y * stride, copyW * 4);
                        }
                        finalBytes = output;
                    }

                    if (useTiles) resultImg.Dispose();
                    return finalBytes;
                }, token);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Magick effect '{effectName}' cancelled by user");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Magick effect '{effectName}' failed: {ex.Message}");
            }
            finally
            {
                PreviewKeyDown -= FxEscHandler;
                FxLoadingOverlay.Visibility = Visibility.Collapsed;
                _isFxRunning = false;
                _fxCts?.Dispose();
                _fxCts = null;
            }

            if (newPixels == null) return;

            // Apply to layer
            layer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), newPixels, stride, 0);
            layer.InvalidateThumbnail();

            // Undo command
            var cmd = new Models.ImageEditor.Commands.PixelEditCommand(layer, oldPixels, newPixels);
            _node.EditorDoc.History.Execute(cmd);

            OnEditorDocumentModified();
        }

        /// <summary>Dispatch Magick effect to MagickImage (runs on background thread).</summary>
        private static void ApplyMagickEffectToImage(ImageMagick.MagickImage img, string effectName, Dictionary<string, double>? p = null)
        {
            double P(string key, double def) => p != null && p.TryGetValue(key, out var v) ? v : def;
            int PI(string key, int def) => (int)P(key, def);

            switch (effectName)
            {
                // Blur/Sharpen
                case "GaussianBlur": img.GaussianBlur(P("Radius", 3), P("Sigma", 1.5)); break;
                case "Blur": img.Blur(P("Radius", 5), P("Sigma", 2)); break;
                case "MotionBlur": img.MotionBlur(P("Radius", 8), P("Sigma", 4), P("Angle", 0)); break;
                case "RadialBlur": img.RotationalBlur(P("Angle", 5)); break;
                case "AdaptiveBlur": img.AdaptiveBlur(P("Radius", 0), P("Sigma", 1)); break;
                case "Sharpen": img.Sharpen(P("Radius", 0), P("Sigma", 1)); break;
                case "UnsharpMask": img.UnsharpMask(P("Radius", 2), P("Sigma", 1), P("Amount", 1), P("Threshold", 0.05)); break;
                case "AdaptiveSharpen": img.AdaptiveSharpen(P("Radius", 0), P("Sigma", 1)); break;
                case "Kuwahara": img.Kuwahara(P("Radius", 3), P("Sigma", 1)); break;

                // Artistic
                case "OilPaint": img.OilPaint(P("Radius", 4), P("Sigma", 1)); break;
                case "Charcoal": img.Charcoal(P("Radius", 2), P("Sigma", 1)); break;
                case "Sketch": img.Sketch(P("Radius", 2), P("Sigma", 1), P("Angle", 0)); break;
                case "Emboss": img.Emboss(P("Radius", 0), P("Sigma", 1)); break;
                case "Vignette": img.Vignette(0, P("Sigma", 10), 10, 10); break;
                case "Swirl": img.Swirl(P("Degrees", 60)); break;
                case "Wave": img.Wave(ImageMagick.PixelInterpolateMethod.Bilinear, P("Amplitude", 5), P("Length", 50)); img.Trim(); break;
                case "Spread": img.Spread(P("Radius", 4)); break;
                case "Implode": img.Implode(P("Amount", 0.3), ImageMagick.PixelInterpolateMethod.Bilinear); break;
                case "Shade": img.Shade(P("Azimuth", 30), P("Elevation", 30)); break;
                case "Pixelate":
                    int bs = PI("BlockSize", 8);
                    int pw = (int)img.Width, ph = (int)img.Height;
                    img.Scale((uint)Math.Max(1, pw / bs), (uint)Math.Max(1, ph / bs));
                    img.Sample((uint)pw, (uint)ph);
                    break;
                case "Polaroid": img.Polaroid("FlowMy", P("Angle", 0), ImageMagick.PixelInterpolateMethod.Bilinear); break;
                case "Frame": var fs = PI("Size", 6); img.Frame((uint)fs, (uint)fs, 2, 2); break;
                case "Explode": img.Implode(-P("Amount", 0.3), ImageMagick.PixelInterpolateMethod.Bilinear); break;
                case "Raise": img.Raise(PI("Size", 8)); break;

                // Edge
                case "EdgeDetect": img.Edge(P("Radius", 1)); break;
                case "CannyEdge": img.CannyEdge(P("Radius", 0), P("Sigma", 1), new ImageMagick.Percentage(P("LowPct", 10)), new ImageMagick.Percentage(P("HighPct", 30))); break;
                case "Threshold": img.Threshold(new ImageMagick.Percentage(P("Percent", 50))); break;
                case "AdaptiveThreshold": img.AdaptiveThreshold((uint)PI("Width", 10), (uint)PI("Height", 10), new ImageMagick.Percentage(P("Bias", 0.0))); break;
                case "OrderedDither": img.OrderedDither("o8x8"); break;

                // Color
                case "Posterize": img.Posterize(PI("Levels", 4)); break;
                case "Solarize": img.Solarize(new ImageMagick.Percentage(P("Threshold", 50))); break;
                case "AutoLevel": img.AutoLevel(); break;
                case "AutoGamma": img.AutoGamma(); break;
                case "Equalize": img.Equalize(); break;
                case "Normalize": img.Normalize(); break;
                case "Negate": img.Negate(); break;
                case "SepiaTone": img.SepiaTone(new ImageMagick.Percentage(P("Threshold", 80))); break;
                case "Grayscale": img.Grayscale(); break;
                case "BrightnessUp": img.BrightnessContrast(new ImageMagick.Percentage(P("Brightness", 15)), new ImageMagick.Percentage(0)); break;
                case "BrightnessDown": img.BrightnessContrast(new ImageMagick.Percentage(-P("Brightness", 15)), new ImageMagick.Percentage(0)); break;
                case "GammaCorrect": img.GammaCorrect(P("Gamma", 1.5)); break;
                case "SaturationUp": img.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(P("Saturation", 140)), new ImageMagick.Percentage(100)); break;
                case "SaturationDown": img.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(P("Saturation", 60)), new ImageMagick.Percentage(100)); break;
                case "Tint": img.Colorize(new ImageMagick.MagickColor(255, 220, 180), new ImageMagick.Percentage(25)); break;
                case "ContrastUp": img.Contrast(); break;
                case "ContrastDown": img.BrightnessContrast(new ImageMagick.Percentage(0), new ImageMagick.Percentage(-P("Contrast", 15))); break;
                case "BlueShift": img.BlueShift(P("Factor", 1.5)); break;
                case "LinearStretch": img.LinearStretch(new ImageMagick.Percentage(P("BlackPct", 1)), new ImageMagick.Percentage(P("WhitePct", 1))); break;
                case "QuantizeColors": img.Quantize(new ImageMagick.QuantizeSettings { Colors = (uint)PI("Colors", 16) }); break;
                case "SigmoidalContrastUp": img.SigmoidalContrast(P("Contrast", 3.0), new ImageMagick.Percentage(P("Midpoint", 50))); break;

                // Noise
                case "AddNoiseGaussian": img.AddNoise(ImageMagick.NoiseType.Gaussian, P("Attenuate", 1.0)); break;
                case "AddNoiseImpulse": img.AddNoise(ImageMagick.NoiseType.Impulse, P("Attenuate", 1.0)); break;
                case "Denoise": img.Enhance(); break;
                case "Despeckle": img.Despeckle(); break;
                case "MedianFilter": img.MedianFilter((uint)PI("Radius", 2)); break;
                case "ReduceNoise": img.ReduceNoise((uint)PI("Order", 2)); break;

                // Morphology
                case "Dilate": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Dilate, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "MorphErode": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Erode, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "Opening": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Open, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "Closing": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Close, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "EdgeIn": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.EdgeIn, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "EdgeOut": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.EdgeOut, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "TopHat": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.TopHat, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "BottomHat": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.BottomHat, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;

                // Transform
                case "Deskew": img.Deskew(new ImageMagick.Percentage(P("Threshold", 40))); break;
                case "Trim": img.Trim(); break;
                case "AutoOrient": img.AutoOrient(); break;
                case "Rotate90": img.Rotate(90); break;
                case "Rotate180": img.Rotate(180); break;
                case "Rotate270": img.Rotate(270); break;
                case "Flop": img.Flop(); break;
                case "Flip": img.Flip(); break;
                case "Shear": img.Shear(P("X", 15), P("Y", 0)); break;
                case "Roll": img.Roll(PI("X", 50), PI("Y", 50)); break;
                case "Shave": var sv = PI("Pixels", 10); img.Shave((uint)sv, (uint)sv); break;
                case "Magnify": img.Magnify(); break;
                case "Minify": img.Minify(); break;
            }
        }

        public class FxToolItem
        {
            public string Name { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string IconKey { get; set; } = string.Empty;
            public string TextIcon { get; set; } = string.Empty;

            public string ToolTipText => $"{DisplayName} ({Description})";

            public System.Windows.Visibility SvgVisibility => string.IsNullOrEmpty(TextIcon) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            public System.Windows.Visibility TextVisibility => string.IsNullOrEmpty(TextIcon) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        private Border? _activeGroupBorder;

        private void GroupToolBtn_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Chặn nổi bọt lên parent để không mở ImageProcessingNodeDialog

            if (sender is not Border border) return;

            // Nếu popup đang mở cho chính nút này, đóng nó lại (tạo hiệu ứng toggle)
            if (FxGroupPopup.IsOpen && FxGroupPopup.PlacementTarget == border)
            {
                FxGroupPopup.IsOpen = false;
                return;
            }

            // Bỏ highlight nút đang active khác trước khi đổi sang nút mới
            if (_activeGroupBorder != null && _activeGroupBorder != border)
            {
                _activeGroupBorder.Background = System.Windows.Media.Brushes.Transparent;
                _activeGroupBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            }

            _activeGroupBorder = border;

            // Lấy danh sách hiệu ứng thuộc nhóm tương ứng
            var list = GetFxGroupItems(border.Name);
            if (list == null || list.Count == 0) return;

            FxPopupItemsControl.ItemsSource = list;

            // Định vị và hiển thị Popup
            FxGroupPopup.PlacementTarget = border;
            FxGroupPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            FxGroupPopup.HorizontalOffset = 6;
            FxGroupPopup.VerticalOffset = -4;

            // Highlight nút này với viền xanh ngọc nhạt và nền xám trắng mờ (Active state)
            border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            border.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4fffb0"));

            FxGroupPopup.IsOpen = true;
        }

        private void FxGroupPopup_Closed(object? sender, EventArgs e)
        {
            if (_activeGroupBorder != null)
            {
                // Reset style về mặc định (Transparent)
                _activeGroupBorder.Background = System.Windows.Media.Brushes.Transparent;
                _activeGroupBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
                _activeGroupBorder = null;
            }
        }

        private List<FxToolItem> GetFxGroupItems(string borderName)
        {
            var items = new List<FxToolItem>();
            switch (borderName)
            {
                case "TbxBlurActive":
                    items.Add(new FxToolItem { Name = "GaussianBlur", DisplayName = "Gaussian Blur", Description = "Làm mịn ảnh, giảm nhiễu hạt độ chi tiết cao", IconKey = "droplet duotone" });
                    items.Add(new FxToolItem { Name = "Blur", DisplayName = "Standard Blur", Description = "Làm mờ ảnh cơ bản nhanh chóng", IconKey = "water duotone" });
                    items.Add(new FxToolItem { Name = "MotionBlur", DisplayName = "Motion Blur", Description = "Làm mờ chuyển động theo một góc nhất định", IconKey = "wind duotone" });
                    items.Add(new FxToolItem { Name = "RadialBlur", DisplayName = "Radial Blur", Description = "Làm mờ xoay quanh tâm ảnh", IconKey = "arrows-spin duotone" });
                    items.Add(new FxToolItem { Name = "AdaptiveBlur", DisplayName = "Adaptive Blur", Description = "Làm mờ bảo toàn các đường biên sắc nét", IconKey = "cloud duotone" });
                    items.Add(new FxToolItem { Name = "Sharpen", DisplayName = "Sharpen", Description = "Tăng cường độ sắc nét cho ảnh", IconKey = "diamond duotone" });
                    items.Add(new FxToolItem { Name = "UnsharpMask", DisplayName = "Unsharp Mask", Description = "Lọc sắc nét nâng cao có kiểm soát", IconKey = "gem duotone" });
                    items.Add(new FxToolItem { Name = "AdaptiveSharpen", DisplayName = "Adaptive Sharpen", Description = "Tăng sắc nét bảo toàn chi tiết phẳng", IconKey = "bolt duotone" });
                    items.Add(new FxToolItem { Name = "Kuwahara", DisplayName = "Kuwahara Filter", Description = "Lọc nghệ thuật Kuwahara làm mịn ảnh giữ cạnh", IconKey = "arrows-to-circle duotone" });
                    break;

                case "TbxArtActive":
                    items.Add(new FxToolItem { Name = "OilPaint", DisplayName = "Oil Paint", Description = "Hiệu ứng tranh sơn dầu nghệ thuật", IconKey = "palette duotone" });
                    items.Add(new FxToolItem { Name = "Charcoal", DisplayName = "Charcoal Drawing", Description = "Hiệu ứng phác thảo tranh than củi", IconKey = "pencil duotone" });
                    items.Add(new FxToolItem { Name = "Sketch", DisplayName = "Sketch Pencil", Description = "Vẽ phác thảo bút chì nghệ thuật", IconKey = "pen-nib duotone" });
                    items.Add(new FxToolItem { Name = "Emboss", DisplayName = "Emboss", Description = "Dập nổi 3D các chi tiết góc cạnh", IconKey = "cube duotone" });
                    items.Add(new FxToolItem { Name = "Vignette", DisplayName = "Vignette Frame", Description = "Làm mờ viền ngoài tạo chiều sâu", IconKey = "circle duotone" });
                    items.Add(new FxToolItem { Name = "Swirl", DisplayName = "Swirl", Description = "Hiệu ứng xoắn nước tại trung tâm", IconKey = "hurricane duotone" });
                    items.Add(new FxToolItem { Name = "Wave", DisplayName = "Wave", Description = "Tạo hiệu ứng gợn sóng uốn lượn", IconKey = "water duotone" });
                    items.Add(new FxToolItem { Name = "Spread", DisplayName = "Spread", Description = "Phân tán ngẫu nhiên các điểm ảnh xung quanh", IconKey = "sparkles duotone" });
                    items.Add(new FxToolItem { Name = "Implode", DisplayName = "Implode", Description = "Hút các điểm ảnh vào trung tâm", IconKey = "compress duotone" });
                    items.Add(new FxToolItem { Name = "Shade", DisplayName = "Shade", Description = "Hiệu ứng đổ bóng chiếu sáng 3D", IconKey = "mountain duotone" });
                    items.Add(new FxToolItem { Name = "Pixelate", DisplayName = "Pixelate", Description = "Chia nhỏ ảnh thành các ô pixel vuông", IconKey = "grid duotone" });
                    items.Add(new FxToolItem { Name = "Polaroid", DisplayName = "Polaroid Frame", Description = "Hiệu ứng khung ảnh Polaroid nghệ thuật cổ điển", IconKey = "pen-to-square duotone" });
                    items.Add(new FxToolItem { Name = "Frame", DisplayName = "3D Frame Border", Description = "Khung viền nổi 3D trang trí xung quanh ảnh", IconKey = "clone-plus duotone" });
                    items.Add(new FxToolItem { Name = "Explode", DisplayName = "Explode Zoom", Description = "Hiệu ứng phồng nở phóng to từ tâm ảnh", IconKey = "burst duotone" });
                    items.Add(new FxToolItem { Name = "Raise", DisplayName = "Raise Bevel", Description = "Tạo gờ viền nổi 3D xung quanh ảnh", IconKey = "cube duotone" });
                    break;

                case "TbxEdgeActive":
                    items.Add(new FxToolItem { Name = "EdgeDetect", DisplayName = "Edge Detect", Description = "Phát hiện biên ảnh cơ bản", IconKey = "object-group duotone" });
                    items.Add(new FxToolItem { Name = "CannyEdge", DisplayName = "Canny Edge", Description = "Bộ lọc biên Canny cao cấp độ chính xác cao", IconKey = "bullseye duotone" });
                    items.Add(new FxToolItem { Name = "Threshold", DisplayName = "Threshold (Binarize)", Description = "Chuyển ảnh nhị phân theo ngưỡng cố định 50%", IconKey = "table-cells-lock duotone" });
                    items.Add(new FxToolItem { Name = "AdaptiveThreshold", DisplayName = "Adaptive Threshold", Description = "Ngưỡng thích nghi bảo toàn chi tiết nét chữ", IconKey = "table-cells-header-unlock duotone" });
                    items.Add(new FxToolItem { Name = "OrderedDither", DisplayName = "Halftone Dither", Description = "Hiệu ứng nghệ thuật Halftone hạt chấm báo", IconKey = "chart-tree-map duotone" });
                    break;

                case "TbxColorActive":
                    items.Add(new FxToolItem { Name = "AutoLevel", DisplayName = "Auto Level", Description = "Tự động cân bằng histogram của ảnh", IconKey = "sliders duotone" });
                    items.Add(new FxToolItem { Name = "AutoGamma", DisplayName = "Auto Gamma", Description = "Tự động sửa lỗi gamma của ảnh", IconKey = "sun duotone" });
                    items.Add(new FxToolItem { Name = "Equalize", DisplayName = "Equalize Histogram", Description = "San phẳng phân bố độ sáng", IconKey = "bars duotone" });
                    items.Add(new FxToolItem { Name = "Normalize", DisplayName = "Normalize Color", Description = "Tối ưu hóa độ tương phản toàn phần", IconKey = "chart-bar duotone" });
                    items.Add(new FxToolItem { Name = "Negate", DisplayName = "Invert (Negate)", Description = "Đảo ngược màu sắc (màu âm bản)", IconKey = "circle-half-stroke duotone" });
                    items.Add(new FxToolItem { Name = "Posterize", DisplayName = "Posterize", Description = "Giảm số lượng màu tạo hiệu ứng poster", IconKey = "swatchbook duotone" });
                    items.Add(new FxToolItem { Name = "Solarize", DisplayName = "Solarize", Description = "Đảo ngược các vùng sáng quá ngưỡng", IconKey = "explosion duotone" });
                    items.Add(new FxToolItem { Name = "SepiaTone", DisplayName = "Sepia Tone", Description = "Màu ảnh hoài cổ úa vàng", IconKey = "image duotone" });
                    items.Add(new FxToolItem { Name = "Grayscale", DisplayName = "Grayscale", Description = "Chuyển đổi thành ảnh đen trắng", IconKey = "moon duotone" });
                    items.Add(new FxToolItem { Name = "BrightnessUp", DisplayName = "Brightness +", Description = "Tăng độ sáng ảnh (+15%)", IconKey = "brightness duotone" });
                    items.Add(new FxToolItem { Name = "BrightnessDown", DisplayName = "Brightness -", Description = "Giảm độ sáng ảnh (-15%)", IconKey = "eclipse duotone" });
                    items.Add(new FxToolItem { Name = "GammaCorrect", DisplayName = "Gamma Correct", Description = "Điều chỉnh độ tương phản trung gian", IconKey = "circle-half-stroke duotone" });
                    items.Add(new FxToolItem { Name = "SaturationUp", DisplayName = "Saturation +", Description = "Tăng rực rỡ màu sắc (+40%)", IconKey = "rainbow duotone" });
                    items.Add(new FxToolItem { Name = "SaturationDown", DisplayName = "Saturation -", Description = "Giảm độ rực màu sắc (-40%)", IconKey = "filter duotone" });
                    items.Add(new FxToolItem { Name = "Tint", DisplayName = "Tint (Colorize)", Description = "Áp sắc màu ấm cho bức ảnh", IconKey = "feather duotone" });
                    items.Add(new FxToolItem { Name = "ContrastUp", DisplayName = "Contrast +", Description = "Tăng tương phản giữa sáng và tối", IconKey = "circle-half duotone" });
                    items.Add(new FxToolItem { Name = "ContrastDown", DisplayName = "Contrast -", Description = "Giảm tương phản giữa sáng và tối", IconKey = "circle duotone" });
                    items.Add(new FxToolItem { Name = "BlueShift", DisplayName = "Blue Shift", Description = "Chuyển tông màu đêm sang ánh sáng xanh", IconKey = "circle-moon duotone" });
                    items.Add(new FxToolItem { Name = "LinearStretch", DisplayName = "Linear Contrast Stretch", Description = "Kéo dãn tương phản tuyến tính tự động", IconKey = "arrows-up-down duotone" });
                    items.Add(new FxToolItem { Name = "QuantizeColors", DisplayName = "Quantize Retro (16c)", Description = "Giảm số lượng màu tối đa còn 16 màu", IconKey = "swatchbook duotone" });
                    items.Add(new FxToolItem { Name = "SigmoidalContrastUp", DisplayName = "Sigmoid Contrast +", Description = "Tăng độ tương phản mịn màng hình chữ S", IconKey = "sliders duotone" });
                    break;

                case "TbxNoiseActive":
                    items.Add(new FxToolItem { Name = "AddNoiseGaussian", DisplayName = "Noise (Gaussian)", Description = "Thêm nhiễu ngẫu nhiên Gauss hạt mịn", IconKey = "signal duotone" });
                    items.Add(new FxToolItem { Name = "AddNoiseImpulse", DisplayName = "Noise (Impulse)", Description = "Thêm nhiễu muối tiêu (Impulse)", IconKey = "burst duotone" });
                    items.Add(new FxToolItem { Name = "Denoise", DisplayName = "Denoise (Enhance)", Description = "Lọc mịn giảm nhiễu hạt cơ bản", IconKey = "broom duotone" });
                    items.Add(new FxToolItem { Name = "Despeckle", DisplayName = "Despeckle", Description = "Khử nhiễu đốm đốm nâng cao", IconKey = "wand duotone" });
                    items.Add(new FxToolItem { Name = "MedianFilter", DisplayName = "Median Filter", Description = "Bộ lọc trung vị khử nhiễu muối tiêu", IconKey = "shield duotone" });
                    items.Add(new FxToolItem { Name = "ReduceNoise", DisplayName = "Reduce Noise", Description = "Giảm nhiễu bảo toàn cấu trúc cạnh", IconKey = "wand-magic-sparkles duotone" });
                    break;

                case "TbxMorphActive":
                    items.Add(new FxToolItem { Name = "Dilate", DisplayName = "Dilate (Phình)", Description = "Giãn nở vùng sáng của ảnh", IconKey = "expand duotone" });
                    items.Add(new FxToolItem { Name = "MorphErode", DisplayName = "Erode (Co)", Description = "Thu hẹp vùng sáng của ảnh", IconKey = "compress duotone" });
                    items.Add(new FxToolItem { Name = "Opening", DisplayName = "Opening", Description = "Co trước giãn sau (xoá nhiễu sáng nhỏ)", IconKey = "atom duotone" });
                    items.Add(new FxToolItem { Name = "Closing", DisplayName = "Closing", Description = "Giãn trước co sau (lấp lỗ trống tối nhỏ)", IconKey = "fingerprint duotone" });
                    items.Add(new FxToolItem { Name = "EdgeIn", DisplayName = "Edge In", Description = "Phát hiện biên trong vùng đối tượng", IconKey = "crop duotone" });
                    items.Add(new FxToolItem { Name = "EdgeOut", DisplayName = "Edge Out", Description = "Phát hiện biên ngoài vùng đối tượng", IconKey = "expand duotone" });
                    items.Add(new FxToolItem { Name = "TopHat", DisplayName = "Top Hat", Description = "Chiết xuất các chi tiết sáng nhỏ trên nền tối", IconKey = "sparkles duotone" });
                    items.Add(new FxToolItem { Name = "BottomHat", DisplayName = "Bottom Hat", Description = "Chiết xuất các lỗ/chi tiết tối trên nền sáng", IconKey = "circle duotone" });
                    break;

                case "TbxXFormActive":
                    items.Add(new FxToolItem { Name = "Deskew", DisplayName = "Deskew", Description = "Tự động chỉnh ảnh bị nghiêng thẳng lại", IconKey = "clock-rotate-left duotone" });
                    items.Add(new FxToolItem { Name = "Trim", DisplayName = "Trim", Description = "Tự động xén các vùng viền thừa", IconKey = "crop duotone" });
                    items.Add(new FxToolItem { Name = "AutoOrient", DisplayName = "Auto Orient", Description = "Tự động xoay ảnh theo EXIF orientation", IconKey = "compass duotone" });
                    items.Add(new FxToolItem { Name = "Rotate90", DisplayName = "Rotate 90°", Description = "Xoay ảnh 90 độ theo chiều kim đồng hồ", IconKey = "rotate duotone", TextIcon = "90°" });
                    items.Add(new FxToolItem { Name = "Rotate180", DisplayName = "Rotate 180°", Description = "Xoay ảnh ngược đầu 180 độ", IconKey = "arrows-repeat duotone", TextIcon = "180°" });
                    items.Add(new FxToolItem { Name = "Rotate270", DisplayName = "Rotate 270°", Description = "Xoay ảnh 270 độ", IconKey = "arrows-spin duotone", TextIcon = "270°" });
                    items.Add(new FxToolItem { Name = "Flop", DisplayName = "Horizontal Flip", Description = "Lật ảnh đối xứng ngang", IconKey = "arrows-left-right duotone" });
                    items.Add(new FxToolItem { Name = "Flip", DisplayName = "Vertical Flip", Description = "Lật ảnh đối xứng dọc", IconKey = "arrows-up-down-left-right duotone" });
                    items.Add(new FxToolItem { Name = "Shear", DisplayName = "Shear (15°)", Description = "Nghiêng xiên hình ảnh góc 15 độ", IconKey = "angles-right duotone" });
                    items.Add(new FxToolItem { Name = "Roll", DisplayName = "Roll Offset (50px)", Description = "Dịch cuộn tuần hoàn ảnh 50px sang đối diện", IconKey = "computer-mouse-scrollwheel duotone" });
                    items.Add(new FxToolItem { Name = "Shave", DisplayName = "Shave Margins (10px)", Description = "Xén bớt một dải 10px viền ngoài", IconKey = "eraser duotone" });
                    items.Add(new FxToolItem { Name = "Magnify", DisplayName = "Magnify (Zoom x2)", Description = "Phóng đại kích thước ảnh lên gấp đôi chất lượng cao", IconKey = "magnifying-glass-plus duotone-regular" });
                    items.Add(new FxToolItem { Name = "Minify", DisplayName = "Minify (Shrink /2)", Description = "Thu nhỏ kích thước ảnh đi một nửa sắc nét", IconKey = "magnifying-glass-minus duotone-regular" });
                    break;
            }
            return items;
        }

        private void FxPopupItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is FxToolItem selectedItem)
            {
                if (_activeGroupBorder != null)
                {
                    // 1. Cập nhật Tag của nút cha (chứa tên effect)
                    _activeGroupBorder.Tag = selectedItem.Name;

                    // 2. Cập nhật ToolTip (bao gồm mô tả tiếng Việt trong ngoặc)
                    _activeGroupBorder.ToolTip = selectedItem.ToolTipText;

                    UpdateConfigDotVisibility(_activeGroupBorder, selectedItem.Name);

                    // 3. Cập nhật icon của nút cha
                    if (_activeGroupBorder.Child is Grid grid)
                    {
                        var svg = grid.Children[0] as SvgViewboxEx;
                        var txt = grid.Children[1] as TextBlock;

                        if (svg != null && txt != null)
                        {
                            if (!string.IsNullOrEmpty(selectedItem.TextIcon))
                            {
                                txt.Text = selectedItem.TextIcon;
                                txt.Visibility = System.Windows.Visibility.Visible;
                                svg.Visibility = System.Windows.Visibility.Collapsed;
                            }
                            else
                            {
                                var converter = new IconKeyToPathConverter();
                                svg.Source = (Uri)converter.Convert(null, typeof(Uri), selectedItem.IconKey, null);
                                svg.Visibility = System.Windows.Visibility.Visible;
                                txt.Visibility = System.Windows.Visibility.Collapsed;
                            }
                        }
                    }
                }

                // Đóng Popup
                FxGroupPopup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void InitializeFxDots()
        {
            var fxBorders = new[] { TbxBlurActive, TbxArtActive, TbxEdgeActive, TbxColorActive, TbxNoiseActive, TbxMorphActive, TbxXFormActive };
            foreach (var border in fxBorders)
            {
                if (border != null && border.Tag is string effectName)
                {
                    UpdateConfigDotVisibility(border, effectName);
                }
            }
        }

        private void UpdateConfigDotVisibility(Border border, string effectName)
        {
            if (border == null) return;
            if (border.Child is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is System.Windows.Shapes.Ellipse el && el.Name != null && el.Name.EndsWith("_ConfigDot"))
                    {
                        bool hasConfig = _fxParamMap.ContainsKey(effectName);
                        el.Visibility = hasConfig ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    }
                }
            }
        }

        private void FxEscHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _fxCts != null)
            {
                _fxCts.Cancel();
                e.Handled = true;
            }
        }

        #endregion

        // [Moved to ImageProcessingNodeContentControl.BrushDrawing.cs]
        // Mouse drawing engine, brush presets, and drawing utilities reside in the separate partial class file.

        #region INTERACTIVE TEXT TOOL LOGIC

        private bool _isDraggingTextContainer = false;
        private Point _textDragStartMousePos;
        private Thickness _textDragStartMargin;



        private void UpdateTopOptionsBar(string activeTool)
        {
            if (TopOptionsBar == null) return;

            bool hasOptions = (activeTool == "Brush" || activeTool == "Eraser" || activeTool == "Text" ||
                               activeTool == "Selection" || activeTool == "Lasso" || activeTool == "PolyLasso" ||
                               activeTool == "Eyedropper" || activeTool == "MagicWand" || activeTool == "QuickSelection" ||
                               activeTool == "ObjectSelection" || activeTool == "CropCanvas" || activeTool == "Slice" ||
                               activeTool == "SliceSelect" || activeTool == "Move" || activeTool == "Transform");

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual && hasOptions)
            {
                TopOptionsBar.Visibility = Visibility.Visible;
                OptBrushPanel.Visibility = (activeTool == "Brush" || activeTool == "Eraser" || activeTool == "QuickSelection") ? Visibility.Visible : Visibility.Collapsed;
                OptTextPanel.Visibility = (activeTool == "Text") ? Visibility.Visible : Visibility.Collapsed;
                OptSelectionPanel.Visibility = (activeTool == "Selection" || activeTool == "Lasso" || activeTool == "PolyLasso" ||
                                                activeTool == "MagicWand" || activeTool == "QuickSelection" || activeTool == "ObjectSelection") ? Visibility.Visible : Visibility.Collapsed;
                if (OptSelectionPanel.Visibility == Visibility.Visible)
                {
                    UpdateSelModeVisuals();
                }

                OptCropPanel.Visibility = (activeTool == "CropCanvas") ? Visibility.Visible : Visibility.Collapsed;
                if (activeTool == "CropCanvas")
                {
                    StartCropMode();
                }
                else if (CropOverlayCanvas != null)
                {
                    CropOverlayCanvas.Visibility = Visibility.Collapsed;
                }

                if (OptMovePanel != null)
                {
                    OptMovePanel.Visibility = (activeTool == "Move") ? Visibility.Visible : Visibility.Collapsed;
                    if (activeTool != "Move")
                    {
                        CommitPendingMoveTranslation();
                    }
                }

                if (OptTransformPanel != null)
                {
                    OptTransformPanel.Visibility = (activeTool == "Transform") ? Visibility.Visible : Visibility.Collapsed;
                    if (activeTool == "Transform")
                    {
                        UpdateTransformOverlayDisplay();
                    }
                    else
                    {
                        if (_transformSessionActive)
                        {
                            CancelTransformSession();
                        }
                        if (TransformOverlayCanvas != null) TransformOverlayCanvas.Visibility = Visibility.Collapsed;
                        if (TransformPreviewImage != null) TransformPreviewImage.Visibility = Visibility.Collapsed;
                    }
                }

                OptSlicePanel.Visibility = (activeTool == "Slice" || activeTool == "SliceSelect") ? Visibility.Visible : Visibility.Collapsed;

                if (SlicesCanvas != null)
                {
                    bool showSlices = (activeTool == "Slice" || activeTool == "SliceSelect");
                    SlicesCanvas.Visibility = (showSlices && _slices.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
                    if (showSlices) UpdateSlicesDisplay();
                }

                if (OptColorPanel != null)
                {
                    OptColorPanel.Visibility = (activeTool == "Brush" || activeTool == "Eyedropper") ? Visibility.Visible : Visibility.Collapsed;
                }

                // Sync initial brush properties
                if (activeTool == "Brush" || activeTool == "Eraser")
                {
                    SyncFromEditorPanelBrushProperties();
                }

                if (activeTool == "Brush" || activeTool == "Eyedropper")
                {
                    SyncToolboxColors();
                }

                if (activeTool == "Text")
                {
                    if (OptTextSize != null && OptTextSize.Value != EditorPanel.TextFontSize)
                        OptTextSize.Value = EditorPanel.TextFontSize;
                    if (OptTextColorSwatch != null)
                        OptTextColorSwatch.Background = new SolidColorBrush(EditorPanel.TextColor);
                    if (OptBtnTextColor != null)
                        OptBtnTextColor.Text = $"#{EditorPanel.TextColor.R:X2}{EditorPanel.TextColor.G:X2}{EditorPanel.TextColor.B:X2}";
                }
            }
            else
            {
                TopOptionsBar.Visibility = Visibility.Collapsed;
            }
        }

        private void EditorPanel_BrushPropertiesChanged(object? sender, EventArgs e)
        {
            SyncFromEditorPanelBrushProperties();
            UpdateBrushCursorPosition();
        }

        // [Moved to ImageProcessingNodeContentControl.BrushDrawing.cs]
        // Brush setting event handlers and preview methods reside in the separate partial class file.

        private void OptTextSizeInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyDirectTextSize();
        }

        private void OptTextSizeInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyDirectTextSize();
                e.Handled = true;
            }
        }

        private void ApplyDirectTextSize()
        {
            if (OptTextSizeInput == null || EditorPanel == null || EditorPanel.SliderTextFontSize == null) return;
            if (double.TryParse(OptTextSizeInput.Text, out double size))
            {
                size = Math.Clamp(size, 6, 200);
                if (EditorPanel.SliderTextFontSize.Value != size)
                {
                    EditorPanel.SliderTextFontSize.Value = size;
                }
            }
            OptTextSizeInput.Text = $"{(int)EditorPanel.TextFontSize}";
        }

        private void TextAlign_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string align)
            {
                if (EditorPanel != null)
                {
                    EditorPanel.TextAlignment = align;
                }
            }
            e.Handled = true;
        }

        private void SyncTopTextAlignButtons()
        {
            if (BtnTextAlignLeft == null || BtnTextAlignCenter == null || BtnTextAlignRight == null || EditorPanel == null) return;
            
            var activeBg = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0xcf, 0xff));
            var activeBorder = new SolidColorBrush(Color.FromRgb(0x00, 0xcf, 0xff));
            var normalBorder = new SolidColorBrush(Color.FromRgb(0x35, 0x39, 0x45));

            BtnTextAlignLeft.Background = Brushes.Transparent;
            BtnTextAlignLeft.BorderBrush = normalBorder;
            BtnTextAlignCenter.Background = Brushes.Transparent;
            BtnTextAlignCenter.BorderBrush = normalBorder;
            BtnTextAlignRight.Background = Brushes.Transparent;
            BtnTextAlignRight.BorderBrush = normalBorder;

            string align = EditorPanel.TextAlignment;
            if (align == "Left")
            {
                BtnTextAlignLeft.Background = activeBg;
                BtnTextAlignLeft.BorderBrush = activeBorder;
            }
            else if (align == "Center")
            {
                BtnTextAlignCenter.Background = activeBg;
                BtnTextAlignCenter.BorderBrush = activeBorder;
            }
            else if (align == "Right")
            {
                BtnTextAlignRight.Background = activeBg;
                BtnTextAlignRight.BorderBrush = activeBorder;
            }
        }

        private void OptTextColorSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OptBtnTextColor_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }

        private void OptTextSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (EditorPanel != null && EditorPanel.SliderTextFontSize != null && EditorPanel.SliderTextFontSize.Value != e.NewValue)
                EditorPanel.SliderTextFontSize.Value = e.NewValue;
        }

        private void OptFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OptFontFamily == null || OptFontFamily.SelectedItem is not ComboBoxItem item) return;
            string family = item.Content.ToString()!;
            if (EditorPanel != null && EditorPanel.CmbFontFamily != null)
            {
                foreach (ComboBoxItem rightItem in EditorPanel.CmbFontFamily.Items)
                {
                    if (rightItem.Content.ToString() == family)
                    {
                        if (EditorPanel.CmbFontFamily.SelectedItem != rightItem)
                            EditorPanel.CmbFontFamily.SelectedItem = rightItem;
                        break;
                    }
                }
            }
        }

        private void OptFontStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OptFontStyle == null || OptFontStyle.SelectedItem is not ComboBoxItem item) return;
            string style = item.Content.ToString()!;
            if (EditorPanel != null && EditorPanel.CmbFontStyle != null)
            {
                foreach (ComboBoxItem rightItem in EditorPanel.CmbFontStyle.Items)
                {
                    if (rightItem.Content.ToString() == style)
                    {
                        if (EditorPanel.CmbFontStyle.SelectedItem != rightItem)
                            EditorPanel.CmbFontStyle.SelectedItem = rightItem;
                        break;
                    }
                }
            }
        }

        private void OptBtnTextColor_Click(object sender, EventArgs e)
        {
            if (EditorPanel != null && EditorPanel.BtnTextColor != null)
                EditorPanel.BtnTextColor.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private void OptDeselect_Click(object sender, RoutedEventArgs e) => ClearSelection();
        private void OptCopy_Click(object sender, RoutedEventArgs e) => CopyActiveSelection();
        private void OptPaste_Click(object sender, RoutedEventArgs e) => PasteSelectionAsLayer();
        private void OptDelete_Click(object sender, RoutedEventArgs e) => DeleteSelectionContent();

        private void EditorPanel_TextPropertiesChanged(object? sender, EventArgs e)
        {
            if (TextMoveContainer.Visibility == Visibility.Visible && _node.EditorDoc != null)
            {
                var activeLayer = _node.EditorDoc.ActiveLayer;
                if (activeLayer != null && activeLayer.IsTextLayer)
                {
                    TextEditorBox.FontSize = EditorPanel.TextFontSize;
                    TextEditorBox.Foreground = new SolidColorBrush(EditorPanel.TextColor);
                    TextEditorBox.CaretBrush = TextEditorBox.Foreground;
                    TextEditorBox.FontFamily = new FontFamily(EditorPanel.TextFontFamily);
                    TextEditorBox.FontWeight = EditorPanel.TextFontStyle == "Bold" ? FontWeights.Bold : FontWeights.Normal;
                    TextEditorBox.FontStyle = EditorPanel.TextFontStyle == "Italic" ? FontStyles.Italic : FontStyles.Normal;
                    TextEditorBox.TextAlignment = EditorPanel.TextAlignment == "Center" ? TextAlignment.Center : 
                                                 EditorPanel.TextAlignment == "Right" ? TextAlignment.Right : TextAlignment.Left;
                }
            }

            if (OptTextSize != null && OptTextSize.Value != EditorPanel.TextFontSize)
                OptTextSize.Value = EditorPanel.TextFontSize;
            if (OptTextSizeInput != null && OptTextSizeInput.Text != $"{(int)EditorPanel.TextFontSize}")
                OptTextSizeInput.Text = $"{(int)EditorPanel.TextFontSize}";
            if (OptTextColorSwatch != null)
                OptTextColorSwatch.Background = new SolidColorBrush(EditorPanel.TextColor);
            if (OptBtnTextColor != null)
                OptBtnTextColor.Text = $"#{EditorPanel.TextColor.R:X2}{EditorPanel.TextColor.G:X2}{EditorPanel.TextColor.B:X2}";

            if (OptFontFamily != null)
            {
                foreach (ComboBoxItem item in OptFontFamily.Items)
                {
                    if (item.Content.ToString() == EditorPanel.TextFontFamily)
                    {
                        if (OptFontFamily.SelectedItem != item)
                            OptFontFamily.SelectedItem = item;
                        break;
                    }
                }
            }
            if (OptFontStyle != null)
            {
                foreach (ComboBoxItem item in OptFontStyle.Items)
                {
                    if (item.Content.ToString() == EditorPanel.TextFontStyle)
                    {
                        if (OptFontStyle.SelectedItem != item)
                            OptFontStyle.SelectedItem = item;
                        break;
                    }
                }
            }
            SyncTopTextAlignButtons();
        }

        private void EditorPanel_ActiveLayerChanged(object? sender, EventArgs e)
        {
            CommitPendingMoveTranslation();
            CommitBrushDrawingSession();
            
            // Auto commit transform session on active layer change!
            if (_transformSessionActive)
            {
                CommitTransformSession();
            }

            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;

            // Commit the previously editing text layer if switching away from it!
            if (_editingTextLayer != null && _editingTextLayer != activeLayer)
            {
                CommitActiveText();
            }

            if (activeLayer != null && activeLayer.IsTextLayer && EditorPanel.ActiveToolName == "Text")
            {
                EnterTextEditingMode(activeLayer);
            }
            else
            {
                TextMoveContainer.Visibility = Visibility.Collapsed;
            }

            UpdateTransformOverlayDisplay();
        }

        private void EnterTextEditingMode(EditorLayer activeLayer)
        {
            if (activeLayer == null || !activeLayer.IsTextLayer) return;

            _editingTextLayer = activeLayer;

            // Sync side panel inputs to match this text layer
            EditorPanel.SetTextProperties(
                activeLayer.TextFontSize,
                activeLayer.TextColor,
                activeLayer.TextFontFamily,
                activeLayer.TextFontStyle,
                activeLayer.TextAlignment
            );

            // Initialize overlay bounding box
            TextMoveContainer.Margin = new Thickness(activeLayer.TextX, activeLayer.TextY, 0, 0);
            TextBoundingBorder.Width = activeLayer.TextWidth;
            TextBoundingBorder.Height = activeLayer.TextHeight;
            TextEditorBox.Text = activeLayer.TextContent;

            // Sync overlay look
            TextEditorBox.FontSize = activeLayer.TextFontSize;
            TextEditorBox.Foreground = new SolidColorBrush(activeLayer.TextColor);
            TextEditorBox.CaretBrush = TextEditorBox.Foreground;
            TextEditorBox.FontFamily = new FontFamily(activeLayer.TextFontFamily);
            TextEditorBox.FontWeight = activeLayer.TextFontStyle == "Bold" ? FontWeights.Bold : FontWeights.Normal;
            TextEditorBox.FontStyle = activeLayer.TextFontStyle == "Italic" ? FontStyles.Italic : FontStyles.Normal;
            TextEditorBox.TextAlignment = activeLayer.TextAlignment == "Center" ? TextAlignment.Center : 
                                          activeLayer.TextAlignment == "Right" ? TextAlignment.Right : TextAlignment.Left;

            // Hide the text layer during editing so it doesn't double-render
            activeLayer.IsTempHidden = true;
            OnEditorDocumentModified();

            TextMoveContainer.Visibility = Visibility.Visible;
            TextEditorBox.Focus();
            TextEditorBox.SelectAll();
        }

        private void TextToolbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var parent = dep;
                while (parent != null)
                {
                    if (parent == TextEditorBox)
                    {
                        return; // Let the TextBox handle the click (placing caret, selecting text)
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            var element = sender as FrameworkElement;
            if (element == null) return;

            _isDraggingTextContainer = true;
            _textDragStartMousePos = e.GetPosition(MainScrollViewer);
            _textDragStartMargin = TextMoveContainer.Margin;
            element.CaptureMouse();
            e.Handled = true;
        }

        private void TextToolbar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingTextContainer) return;
            var element = sender as FrameworkElement;
            if (element == null) return;

            var currentPos = e.GetPosition(MainScrollViewer);
            double dx = currentPos.X - _textDragStartMousePos.X;
            double dy = currentPos.Y - _textDragStartMousePos.Y;

            double scale = ImageZoomScale.ScaleX;
            if (scale <= 0) scale = 1.0;

            double newLeft = _textDragStartMargin.Left + (dx / scale);
            double newTop = _textDragStartMargin.Top + (dy / scale);

            // Clamp container bounds loosely
            newLeft = Math.Clamp(newLeft, -1000, MainImage.ActualWidth + 1000);
            newTop = Math.Clamp(newTop, -1000, MainImage.ActualHeight + 1000);

            TextMoveContainer.Margin = new Thickness(newLeft, newTop, 0, 0);
            e.Handled = true;
        }

        private void TextToolbar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingTextContainer)
            {
                _isDraggingTextContainer = false;
                var element = sender as FrameworkElement;
                element?.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void TextResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double scale = ImageZoomScale.ScaleX;
            if (scale <= 0) scale = 1.0;

            double dw = e.HorizontalChange / scale;
            double dh = e.VerticalChange / scale;

            if (double.IsNaN(TextBoundingBorder.Width) || TextBoundingBorder.Width <= 0)
                TextBoundingBorder.Width = TextBoundingBorder.ActualWidth;
            if (double.IsNaN(TextBoundingBorder.Height) || TextBoundingBorder.Height <= 0)
                TextBoundingBorder.Height = TextBoundingBorder.ActualHeight;

            TextBoundingBorder.Width = Math.Max(100, TextBoundingBorder.Width + dw);
            TextBoundingBorder.Height = Math.Max(40, TextBoundingBorder.Height + dh);
            e.Handled = true;
        }

        private void TextEditorBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CommitActiveText();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelActiveText();
                e.Handled = true;
            }
        }

        private void TextEditorBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void RedrawTextLayer(EditorLayer layer)
        {
            if (layer == null || !layer.IsTextLayer) return;
            // Clear bitmap so it stays transparent (drawing is done dynamically in Composite)
            layer.Clear();
        }

        private void CommitActiveText()
        {
            var textLayer = _editingTextLayer;
            if (textLayer == null || !textLayer.IsTextLayer) return;

            string text = TextEditorBox.Text;
            if (string.IsNullOrEmpty(text) || text == "Nhập chữ...")
            {
                CancelActiveText();
                return;
            }

            // Snapshot old metadata
            string oldText = textLayer.TextContent;
            double oldX = textLayer.TextX;
            double oldY = textLayer.TextY;
            double oldW = textLayer.TextWidth;
            double oldH = textLayer.TextHeight;
            double oldSize = textLayer.TextFontSize;
            Color oldColor = textLayer.TextColor;
            string oldFamily = textLayer.TextFontFamily;
            string oldStyle = textLayer.TextFontStyle;
            string oldAlign = textLayer.TextAlignment;

            // Save new metadata
            string newText = text;
            double newX = TextMoveContainer.Margin.Left;
            double newY = TextMoveContainer.Margin.Top;
            double newW = TextBoundingBorder.Width;
            double newH = TextBoundingBorder.Height;
            double newSize = EditorPanel.TextFontSize;
            Color newColor = EditorPanel.TextColor;
            string newFamily = EditorPanel.TextFontFamily;
            string newStyle = EditorPanel.TextFontStyle;
            string newAlign = EditorPanel.TextAlignment;

            // Execute TextEditCommand
            if (_node.EditorDoc != null)
            {
                var cmd = new TextEditCommand(
                    textLayer,
                    RedrawTextLayer,
                    oldText, oldX, oldY, oldW, oldH, oldSize, oldColor, oldFamily, oldStyle, oldAlign,
                    newText, newX, newY, newW, newH, newSize, newColor, newFamily, newStyle, newAlign
                );
                _node.EditorDoc.History.Execute(cmd);
            }

            TextMoveContainer.Visibility = Visibility.Collapsed;
            textLayer.IsTempHidden = false; // Restore visibility
            textLayer.InvalidateThumbnail();
            _editingTextLayer = null;
            OnEditorDocumentModified();
        }

        private void CancelActiveText()
        {
            TextMoveContainer.Visibility = Visibility.Collapsed;
            var layer = _editingTextLayer;
            if (layer != null && layer.IsTextLayer)
            {
                layer.IsTempHidden = false; // Restore visibility
                if (layer.TextContent == "Nhập chữ..." || string.IsNullOrEmpty(layer.TextContent))
                {
                    if (_node.EditorDoc != null)
                    {
                        _node.EditorDoc.Layers.Remove(layer);
                        EditorPanel.RefreshLayersList();
                    }
                }
                else
                {
                    RedrawTextLayer(layer);
                }
            }
            _editingTextLayer = null;
        }

        #endregion
    }
}
