using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
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
            EditorPanel.DocumentModified += OnEditorDocumentModified;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

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

            // Sync mode toggle visual state
            SyncModeButtonStyles();
            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
                SwitchToMode(Models.Nodes.ImageProcessingMode.Manual);
        }

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

            // Hỗ trợ vẽ/xoá/tô màu thủ công
            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool != "Move")
                {
                    HandleManualEditorMouseDown(e);
                    e.Handled = true;
                    return;
                }
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
            if (_isDrawingPixels)
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
            if (_isDrawingPixels)
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
                LeftMenuViewbox.StretchDirection = StretchDirection.DownOnly;

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
                RightMenuBorder.LayoutTransform = identity;
                IpProcessorHost.LayoutTransform = identity;
                LeftMenuBorder.LayoutTransform = identity;
                PlaceholderTextBlock.LayoutTransform = identity;
                CropsLabelText.LayoutTransform = identity;
                RenderLabelText.LayoutTransform = identity;
                EditorPanel.LayoutTransform = new ScaleTransform(typoMul, typoMul);
                
                var formatMode = typoMul == 1.0 ? TextFormattingMode.Display : TextFormattingMode.Ideal;
                TextOptions.SetTextFormattingMode(EditorPanel, formatMode);
                TextOptions.SetTextFormattingMode(RightMenuBorder, formatMode);
                TextOptions.SetTextFormattingMode(IpProcessorHost, formatMode);

                int Sz(double b) => Math.Max(1, (int)Math.Round(b * typoMul));
                double leftBtnMul = typoMul * (_widgetExpandedFullscreen ? 0.78 : 1.0);
                int B(double x) => Math.Max(1, (int)Math.Round(x * leftBtnMul));

                ImageTitleTextBlock.FontSize = Sz(BaseTitleFontSize);
                PlaceholderTextBlock.FontSize = Sz(BasePlaceholderFontSize);
                CropsLabelText.FontSize = Sz(BaseCropLabelFontSize * WidgetSectionLabelFontBoost);
                RenderLabelText.FontSize = Sz(BaseCropLabelFontSize * WidgetSectionLabelFontBoost);
                MagCoordTextBlock.FontSize = Sz(9);

                BtnOpenImage.Width = B(30);
                BtnOpenImage.Height = B(30);
                BtnOpenImage.FontSize = B(16);
                CropToolButton.Width = B(30);
                CropToolButton.Height = B(30);
                CropToolButton.FontSize = B(16);
                ColorCropButton.Width = B(30);
                ColorCropButton.Height = B(30);

                IpToggleButton.Width = B(28);
                IpToggleButton.Height = B(22);
                IpToggleIcon.Width = B(14);
                IpToggleIcon.Height = B(14);

                ApplyWidgetTypographyResources(typoMul);
                RebuildWidgetImageProcessorIfNeeded(ipDip);

                ImageProcessingNodeControl.UpdateInteractionVisualScale(_handleOverlay, _node, typoMul);
                return;
            }

            LeftMenuViewbox.StretchDirection = StretchDirection.Both;
            RootLayout.ColumnDefinitions[0].Width = new GridLength(0.6, GridUnitType.Star);

            PutFontResource(Resources, "WidgetCropOrderFontSize", 10);
            PutFontResource(Resources, "WidgetCropCheckFontSize", 8);
            PutFontResource(Resources, "WidgetCropDeleteFontSize", 9);
            PutFontResource(Resources, "WidgetRenderGroupTitleFontSize", 9);

            ImageTitleTextBlock.FontSize = BaseTitleFontSize;
            PlaceholderTextBlock.FontSize = BasePlaceholderFontSize;
            CropsLabelText.FontSize = BaseCropLabelFontSize;
            RenderLabelText.FontSize = BaseCropLabelFontSize;
            MagCoordTextBlock.FontSize = 9;

            BtnOpenImage.Width = 30;
            BtnOpenImage.Height = 30;
            BtnOpenImage.FontSize = 16;
            CropToolButton.Width = 30;
            CropToolButton.Height = 30;
            CropToolButton.FontSize = 16;
            ColorCropButton.Width = 30;
            ColorCropButton.Height = 30;
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
            var ipScale = new ScaleTransform(ipTextScaleFactor, ipTextScaleFactor);
            RightMenuBorder.LayoutTransform = ipScale;
            TextOptions.SetTextFormattingMode(RightMenuBorder, TextFormattingMode.Ideal);
            TextOptions.SetTextFormattingMode(IpProcessorHost, TextFormattingMode.Ideal);
            
            // Tính toán tỉ lệ co dãn của EditorPanel để vừa khít chiều rộng cột 2 (220px)
            double editorScaleVal = Math.Max(0.5, (_node.Width * 0.32) / 220.0);
            EditorPanel.LayoutTransform = new ScaleTransform(editorScaleVal, editorScaleVal);
            TextOptions.SetTextFormattingMode(EditorPanel, TextFormattingMode.Ideal);

            var canvasIpTextTransform = new ScaleTransform(ipTextScaleFactor, ipTextScaleFactor);
            PlaceholderTextBlock.LayoutTransform = canvasIpTextTransform;
            CropsLabelText.LayoutTransform = Transform.Identity;
            RenderLabelText.LayoutTransform = Transform.Identity;

            LeftMenuBorder.LayoutTransform = new ScaleTransform(widthScaleFactor, widthScaleFactor);

            var interactionScale = Math.Max(Math.Max(heightScaleFactor, widthScaleFactor), ipTextScaleFactor);
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
            SyncIpToggleIcon();

            _ = LoadPreviewAndSyncEditor();
        }

        private async System.Threading.Tasks.Task LoadPreviewAndSyncEditor()
        {
            await ImageProcessingNodeControl.UpdatePreviewAsync(
                _node, _host, MainImage, PlaceholderTextBlock, ImageZoomScale,
                ImageAreaGrid, MainScrollViewer, ImageTitleTextBlock,
                _onCropClickForIp);

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

        private void BtnModeAI_Click(object sender, RoutedEventArgs e)
        {
            SwitchToMode(Models.Nodes.ImageProcessingMode.AI);
            e.Handled = true;
        }

        private void BtnModeEditor_Click(object sender, RoutedEventArgs e)
        {
            SwitchToMode(Models.Nodes.ImageProcessingMode.Manual);
            e.Handled = true;
        }

        private void SwitchToMode(Models.Nodes.ImageProcessingMode mode)
        {
            _node.ProcessingMode = mode;

            if (mode == Models.Nodes.ImageProcessingMode.AI)
            {
                // Hiện AI panels, ẩn Editor
                RightMenuBorder.Visibility = Visibility.Visible;
                EditorPanel.Visibility = Visibility.Collapsed;
                LeftMenuBorder.Visibility = Visibility.Visible;
                EditorToolbox.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Ẩn AI panels, hiện Editor
                RightMenuBorder.Visibility = Visibility.Collapsed;
                EditorPanel.Visibility = Visibility.Visible;
                LeftMenuBorder.Visibility = Visibility.Collapsed;
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
            }

            SyncModeButtonStyles();
        }

        private void EnsureEditorDocument()
        {
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
        }

        private void OnEditorDocumentModified()
        {
            // Re-composite layers → hiển thị lên MainImage
            if (_node.EditorDoc == null) return;

            try
            {
                var composite = _node.EditorDoc.Composite();
                MainImage.Source = composite;
            }
            catch { /* ignore composite errors */ }
        }

        private void SyncModeButtonStyles()
        {
            bool isAI = _node.ProcessingMode == Models.Nodes.ImageProcessingMode.AI;

            // Active: accent bg + white text; Inactive: dark + muted
            var activeBg = new SolidColorBrush(Color.FromArgb(0x40, 0x4f, 0xff, 0xb0));
            var activeFg = new SolidColorBrush(Color.FromRgb(0xdd, 0xe3, 0xef));
            var inactiveBg = new SolidColorBrush(Color.FromArgb(0x18, 0xff, 0xff, 0xff));
            var inactiveFg = new SolidColorBrush(Color.FromRgb(0x5a, 0x60, 0x72));

            BtnModeAI.Background = isAI ? activeBg : inactiveBg;
            BtnModeAI.Foreground = isAI ? activeFg : inactiveFg;
            BtnModeEditor.Background = isAI ? inactiveBg : activeBg;
            BtnModeEditor.Foreground = isAI ? inactiveFg : activeFg;

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
            _toolboxBorders["Selection"] = TbxSelection;
        }

        private void EditorToolbox_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string toolName)
            {
                // Delegate to EditorPanel's tool selection (keeps both in sync)
                EditorPanel.SelectToolByName(toolName);
                SyncToolboxHighlight();
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
            var activeBg = new SolidColorBrush(Color.FromArgb(0x30, 0x4f, 0xff, 0xb0));
            var activeBorder = new SolidColorBrush(Color.FromRgb(0x4f, 0xff, 0xb0));

            foreach (var (name, border) in _toolboxBorders)
            {
                if (name == activeTool)
                {
                    border.Background = activeBg;
                    border.BorderBrush = activeBorder;
                }
                else
                {
                    border.Background = Brushes.Transparent;
                    border.BorderBrush = Brushes.Transparent;
                }
            }
        }

        private void SyncToolboxColors()
        {
            if (_node.EditorDoc == null) return;
            TbxFgColor.Background = new SolidColorBrush(_node.EditorDoc.ForegroundColor);
            TbxBgColor.Background = new SolidColorBrush(_node.EditorDoc.BackgroundColor);
        }

        // ═══════ MAGICK.NET EFFECTS (Async + Loading + ESC Cancel) ═══════

        private CancellationTokenSource? _fxCts;
        private bool _isFxRunning;

        private async void MagickEffect_Click(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null || _isFxRunning) return;
            var layer = _node.EditorDoc.ActiveLayer;
            if (layer == null || layer.IsLocked || !layer.IsVisible) return;

            if (sender is not Border border || border.Tag is not string effectName)
                return;

            e.Handled = true;

            // Snapshot old pixels for undo (on UI thread)
            int w = layer.Width, h = layer.Height;
            int stride = w * 4;
            var oldPixels = new byte[stride * h];
            layer.Bitmap.CopyPixels(oldPixels, stride, 0);

            // Copy bitmap data for background processing
            var srcPixels = new byte[stride * h];
            Array.Copy(oldPixels, srcPixels, oldPixels.Length);

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

                // Run Magick effect on background thread (raw pixels → MagickImage → raw pixels)
                newPixels = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    // Construct MagickImage from raw pixels
                    var settings = new ImageMagick.MagickReadSettings
                    {
                        Width = (uint)w,
                        Height = (uint)h,
                        Format = ImageMagick.MagickFormat.Bgra,
                        Depth = 8
                    };
                    using var img = new ImageMagick.MagickImage(srcPixels, settings);

                    token.ThrowIfCancellationRequested();

                    // Apply effect via dispatch
                    ApplyMagickEffectToImage(img, effectName);

                    token.ThrowIfCancellationRequested();

                    // Convert back to pixels
                    img.Alpha(ImageMagick.AlphaOption.Set);
                    var resultBytes = img.ToByteArray(ImageMagick.MagickFormat.Bgra);
                    int rw = (int)img.Width, rh = (int)img.Height;

                    // Build output matching original layer size
                    var output = new byte[stride * h];
                    int copyW = Math.Min(w, rw);
                    int copyH = Math.Min(h, rh);
                    int rStride = rw * 4;
                    for (int y = 0; y < copyH; y++)
                    {
                        Array.Copy(resultBytes, y * rStride, output, y * stride, copyW * 4);
                    }
                    return output;
                }, token);
            }
            catch (OperationCanceledException)
            {
                // User pressed ESC
                System.Diagnostics.Debug.WriteLine($"Magick effect '{effectName}' cancelled by user");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Magick effect '{effectName}' failed: {ex.Message}");
            }
            finally
            {
                // Hide loading
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
        private static void ApplyMagickEffectToImage(ImageMagick.MagickImage img, string effectName)
        {
            switch (effectName)
            {
                // Blur/Sharpen
                case "GaussianBlur":     img.GaussianBlur(3, 1.5); break;
                case "Blur":             img.Blur(5, 2); break;
                case "MotionBlur":       img.MotionBlur(8, 4, 0); break;
                case "RadialBlur":       img.RotationalBlur(5); break;
                case "AdaptiveBlur":     img.AdaptiveBlur(0, 1); break;
                case "Sharpen":          img.Sharpen(0, 1); break;
                case "UnsharpMask":      img.UnsharpMask(2, 1, 1, 0.05); break;
                case "AdaptiveSharpen":  img.AdaptiveSharpen(0, 1); break;
                // Artistic
                case "OilPaint":         img.OilPaint(4, 1); break;
                case "Charcoal":         img.Charcoal(2, 1); break;
                case "Sketch":           img.Sketch(2, 1, 0); break;
                case "Emboss":           img.Emboss(0, 1); break;
                case "Vignette":         img.Vignette(0, 10, 10, 10); break;
                case "Swirl":            img.Swirl(60); break;
                case "Wave":             img.Wave(ImageMagick.PixelInterpolateMethod.Bilinear, 5, 50); img.Trim(); break;
                case "Spread":           img.Spread(4); break;
                case "Implode":          img.Implode(0.3, ImageMagick.PixelInterpolateMethod.Bilinear); break;
                case "Shade":            img.Shade(30, 30); break;
                case "Pixelate":
                    int pw = (int)img.Width, ph = (int)img.Height;
                    img.Scale((uint)Math.Max(1, pw / 8), (uint)Math.Max(1, ph / 8));
                    img.Sample((uint)pw, (uint)ph);
                    break;
                // Edge
                case "EdgeDetect":       img.Edge(1); break;
                case "CannyEdge":        img.CannyEdge(0, 1, new ImageMagick.Percentage(10), new ImageMagick.Percentage(30)); break;
                // Color
                case "Posterize":        img.Posterize(4); break;
                case "Solarize":         img.Solarize(new ImageMagick.Percentage(50)); break;
                case "AutoLevel":        img.AutoLevel(); break;
                case "AutoGamma":        img.AutoGamma(); break;
                case "Equalize":         img.Equalize(); break;
                case "Normalize":        img.Normalize(); break;
                case "Negate":           img.Negate(); break;
                case "SepiaTone":        img.SepiaTone(new ImageMagick.Percentage(80)); break;
                case "Grayscale":        img.Grayscale(); break;
                case "BrightnessUp":     img.BrightnessContrast(new ImageMagick.Percentage(15), new ImageMagick.Percentage(0)); break;
                case "BrightnessDown":   img.BrightnessContrast(new ImageMagick.Percentage(-15), new ImageMagick.Percentage(0)); break;
                case "GammaCorrect":     img.GammaCorrect(1.5); break;
                case "SaturationUp":     img.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(140), new ImageMagick.Percentage(100)); break;
                case "SaturationDown":   img.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(60), new ImageMagick.Percentage(100)); break;
                case "Tint":             img.Colorize(new ImageMagick.MagickColor(255, 220, 180), new ImageMagick.Percentage(25)); break;
                case "ContrastUp":       img.Contrast(); break;
                case "ContrastDown":     img.BrightnessContrast(new ImageMagick.Percentage(0), new ImageMagick.Percentage(-15)); break;
                case "BlueShift":        img.BlueShift(); break;
                // Noise
                case "AddNoiseGaussian": img.AddNoise(ImageMagick.NoiseType.Gaussian, 1.0); break;
                case "AddNoiseImpulse":  img.AddNoise(ImageMagick.NoiseType.Impulse, 1.0); break;
                case "Denoise":          img.Enhance(); break;
                case "Despeckle":        img.Despeckle(); break;
                case "MedianFilter":     img.MedianFilter(2); break;
                case "ReduceNoise":      img.ReduceNoise(2); break;
                // Morphology
                case "Dilate":           img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Dilate, Kernel = ImageMagick.Kernel.Diamond, Iterations = 1 }); break;
                case "MorphErode":       img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Erode, Kernel = ImageMagick.Kernel.Diamond, Iterations = 1 }); break;
                case "Opening":          img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Open, Kernel = ImageMagick.Kernel.Diamond, Iterations = 1 }); break;
                case "Closing":          img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Close, Kernel = ImageMagick.Kernel.Diamond, Iterations = 1 }); break;
                // Transform
                case "Deskew":           img.Deskew(new ImageMagick.Percentage(40)); break;
                case "Trim":             img.Trim(); break;
                case "AutoOrient":       img.AutoOrient(); break;
                case "Rotate90":         img.Rotate(90); break;
                case "Rotate180":        img.Rotate(180); break;
                case "Rotate270":        img.Rotate(270); break;
                case "Flop":             img.Flop(); break;
                case "Flip":             img.Flip(); break;
            }
        }

        public class FxToolItem
        {
            public string Name { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string IconKey { get; set; } = string.Empty;
        }

        private Border? _activeGroupBorder;

        private void GroupToolBtn_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Chặn nổi bọt lên parent để không mở ImageProcessingNodeDialog
            
            if (sender is not Border border) return;
            
            _activeGroupBorder = border;
            
            // Lấy danh sách hiệu ứng thuộc nhóm tương ứng
            var list = GetFxGroupItems(border.Name);
            if (list == null || list.Count == 0) return;
            
            FxPopupItemsControl.ItemsSource = list;
            
            // Định vị và hiển thị Popup
            FxGroupPopup.PlacementTarget = border;
            FxGroupPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            FxGroupPopup.HorizontalOffset = 4;
            FxGroupPopup.VerticalOffset = -8;
            FxGroupPopup.IsOpen = true;
        }

        private List<FxToolItem> GetFxGroupItems(string borderName)
        {
            var items = new List<FxToolItem>();
            switch (borderName)
            {
                case "TbxBlurActive":
                    items.Add(new FxToolItem { Name = "GaussianBlur", DisplayName = "Gaussian Blur", Description = "Làm mịn ảnh, giảm nhiễu hạt độ chi tiết cao", IconKey = "droplet duotone" });
                    items.Add(new FxToolItem { Name = "Blur", DisplayName = "Standard Blur", Description = "Làm mờ ảnh cơ bản nhanh chóng", IconKey = "droplet duotone" });
                    items.Add(new FxToolItem { Name = "MotionBlur", DisplayName = "Motion Blur", Description = "Làm mờ chuyển động theo một góc nhất định", IconKey = "wind duotone" });
                    items.Add(new FxToolItem { Name = "RadialBlur", DisplayName = "Radial Blur", Description = "Làm mờ xoay quanh tâm ảnh", IconKey = "arrows-spin duotone" });
                    items.Add(new FxToolItem { Name = "AdaptiveBlur", DisplayName = "Adaptive Blur", Description = "Làm mờ bảo toàn các đường biên sắc nét", IconKey = "cloud duotone" });
                    items.Add(new FxToolItem { Name = "Sharpen", DisplayName = "Sharpen", Description = "Tăng cường độ sắc nét cho ảnh", IconKey = "diamond duotone" });
                    items.Add(new FxToolItem { Name = "UnsharpMask", DisplayName = "Unsharp Mask", Description = "Lọc sắc nét nâng cao có kiểm soát", IconKey = "gem duotone" });
                    items.Add(new FxToolItem { Name = "AdaptiveSharpen", DisplayName = "Adaptive Sharpen", Description = "Tăng sắc nét bảo toàn chi tiết phẳng", IconKey = "bolt duotone" });
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
                    break;

                case "TbxEdgeActive":
                    items.Add(new FxToolItem { Name = "EdgeDetect", DisplayName = "Edge Detect", Description = "Phát hiện biên ảnh cơ bản", IconKey = "vector-square duotone" });
                    items.Add(new FxToolItem { Name = "CannyEdge", DisplayName = "Canny Edge", Description = "Bộ lọc biên Canny cao cấp độ chính xác cao", IconKey = "bullseye duotone" });
                    break;

                case "TbxColorActive":
                    items.Add(new FxToolItem { Name = "AutoLevel", DisplayName = "Auto Level", Description = "Tự động cân bằng histogram của ảnh", IconKey = "sliders duotone" });
                    items.Add(new FxToolItem { Name = "AutoGamma", DisplayName = "Auto Gamma", Description = "Tự động sửa lỗi gamma của ảnh", IconKey = "sun duotone" });
                    items.Add(new FxToolItem { Name = "Equalize", DisplayName = "Equalize Histogram", Description = "San phẳng phân bố độ sáng", IconKey = "bars duotone" });
                    items.Add(new FxToolItem { Name = "Normalize", DisplayName = "Normalize Color", Description = "Tối ưu hóa độ tương phản toàn phần", IconKey = "chart-bar duotone" });
                    items.Add(new FxToolItem { Name = "Negate", DisplayName = "Invert (Negate)", Description = "Đảo ngược màu sắc (màu âm bản)", IconKey = "circle-half duotone" });
                    items.Add(new FxToolItem { Name = "Posterize", DisplayName = "Posterize", Description = "Giảm số lượng màu tạo hiệu ứng poster", IconKey = "swatchbook duotone" });
                    items.Add(new FxToolItem { Name = "Solarize", DisplayName = "Solarize", Description = "Đảo ngược các vùng sáng quá ngưỡng", IconKey = "explosion duotone" });
                    items.Add(new FxToolItem { Name = "SepiaTone", DisplayName = "Sepia Tone", Description = "Màu ảnh hoài cổ úa vàng", IconKey = "image duotone" });
                    items.Add(new FxToolItem { Name = "Grayscale", DisplayName = "Grayscale", Description = "Chuyển đổi thành ảnh đen trắng", IconKey = "moon duotone" });
                    items.Add(new FxToolItem { Name = "BrightnessUp", DisplayName = "Brightness +", Description = "Tăng độ sáng ảnh (+15%)", IconKey = "brightness duotone" });
                    items.Add(new FxToolItem { Name = "BrightnessDown", DisplayName = "Brightness -", Description = "Giảm độ sáng ảnh (-15%)", IconKey = "eclipse duotone" });
                    items.Add(new FxToolItem { Name = "GammaCorrect", DisplayName = "Gamma Correct", Description = "Điều chỉnh độ tương phản trung gian", IconKey = "adjust duotone" });
                    items.Add(new FxToolItem { Name = "SaturationUp", DisplayName = "Saturation +", Description = "Tăng rực rỡ màu sắc (+40%)", IconKey = "rainbow duotone" });
                    items.Add(new FxToolItem { Name = "SaturationDown", DisplayName = "Saturation -", Description = "Giảm độ rực màu sắc (-40%)", IconKey = "filter duotone" });
                    items.Add(new FxToolItem { Name = "Tint", DisplayName = "Tint (Colorize)", Description = "Áp sắc màu ấm cho bức ảnh", IconKey = "feather duotone" });
                    items.Add(new FxToolItem { Name = "ContrastUp", DisplayName = "Contrast +", Description = "Tăng tương phản giữa sáng và tối", IconKey = "circle-half duotone" });
                    items.Add(new FxToolItem { Name = "ContrastDown", DisplayName = "Contrast -", Description = "Giảm tương phản giữa sáng và tối", IconKey = "circle-half duotone" });
                    items.Add(new FxToolItem { Name = "BlueShift", DisplayName = "Blue Shift", Description = "Chuyển tông màu đêm sang ánh sáng xanh", IconKey = "moon duotone" });
                    break;

                case "TbxNoiseActive":
                    items.Add(new FxToolItem { Name = "AddNoiseGaussian", DisplayName = "Noise (Gaussian)", Description = "Thêm nhiễu ngẫu nhiên Gauss hạt mịn", IconKey = "signal duotone" });
                    items.Add(new FxToolItem { Name = "AddNoiseImpulse", DisplayName = "Noise (Impulse)", Description = "Thêm nhiễu muối tiêu (Impulse)", IconKey = "burst duotone" });
                    items.Add(new FxToolItem { Name = "Denoise", DisplayName = "Denoise (Enhance)", Description = "Lọc mịn giảm nhiễu hạt cơ bản", IconKey = "broom duotone" });
                    items.Add(new FxToolItem { Name = "Despeckle", DisplayName = "Despeckle", Description = "Khử nhiễu đốm đốm nâng cao", IconKey = "broom duotone" });
                    items.Add(new FxToolItem { Name = "MedianFilter", DisplayName = "Median Filter", Description = "Bộ lọc trung vị khử nhiễu muối tiêu", IconKey = "shield duotone" });
                    items.Add(new FxToolItem { Name = "ReduceNoise", DisplayName = "Reduce Noise", Description = "Giảm nhiễu bảo toàn cấu trúc cạnh", IconKey = "wand duotone" });
                    break;

                case "TbxMorphActive":
                    items.Add(new FxToolItem { Name = "Dilate", DisplayName = "Dilate (Phình)", Description = "Giãn nở vùng sáng của ảnh", IconKey = "expand duotone" });
                    items.Add(new FxToolItem { Name = "MorphErode", DisplayName = "Erode (Co)", Description = "Thu hẹp vùng sáng của ảnh", IconKey = "compress duotone" });
                    items.Add(new FxToolItem { Name = "Opening", DisplayName = "Opening", Description = "Co trước giãn sau (xoá nhiễu sáng nhỏ)", IconKey = "atom duotone" });
                    items.Add(new FxToolItem { Name = "Closing", DisplayName = "Closing", Description = "Giãn trước co sau (lấp lỗ trống tối nhỏ)", IconKey = "fingerprint duotone" });
                    break;

                case "TbxXFormActive":
                    items.Add(new FxToolItem { Name = "Deskew", DisplayName = "Deskew", Description = "Tự động chỉnh ảnh bị nghiêng thẳng lại", IconKey = "rotate duotone" });
                    items.Add(new FxToolItem { Name = "Trim", DisplayName = "Trim", Description = "Tự động xén các vùng viền thừa", IconKey = "crop duotone" });
                    items.Add(new FxToolItem { Name = "AutoOrient", DisplayName = "Auto Orient", Description = "Tự động xoay ảnh theo EXIF orientation", IconKey = "compass duotone" });
                    items.Add(new FxToolItem { Name = "Rotate90", DisplayName = "Rotate 90°", Description = "Xoay ảnh 90 độ theo chiều kim đồng hồ", IconKey = "rotate duotone" });
                    items.Add(new FxToolItem { Name = "Rotate180", DisplayName = "Rotate 180°", Description = "Xoay ảnh ngược đầu 180 độ", IconKey = "rotate duotone" });
                    items.Add(new FxToolItem { Name = "Rotate270", DisplayName = "Rotate 270°", Description = "Xoay ảnh 270 độ", IconKey = "rotate duotone" });
                    items.Add(new FxToolItem { Name = "Flop", DisplayName = "Horizontal Flip", Description = "Lật ảnh đối xứng ngang", IconKey = "arrows-left-right duotone" });
                    items.Add(new FxToolItem { Name = "Flip", DisplayName = "Vertical Flip", Description = "Lật ảnh đối xứng dọc", IconKey = "arrows-up-down-left-right duotone" });
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

                    // 2. Cập nhật ToolTip
                    _activeGroupBorder.ToolTip = selectedItem.DisplayName;

                    // 3. Cập nhật icon của nút cha
                    if (_activeGroupBorder.Child is SvgViewboxEx svg)
                    {
                        var converter = new IconKeyToPathConverter();
                        svg.Source = (Uri)converter.Convert(null, typeof(Uri), selectedItem.IconKey, null);
                    }
                }
                
                // Đóng Popup
                FxGroupPopup.IsOpen = false;
                e.Handled = true;
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

        #region MOUSE DRAWING ENGINE FOR IMAGE EDITOR MODE

        private bool _isDrawingPixels;
        private byte[]? _tempDrawingPixels;
        private Point _lastDrawingPixelPoint;
        private byte[]? _oldPixelsForUndo;

        private void HandleManualEditorMouseDown(MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null || activeLayer.IsLocked || !activeLayer.IsVisible) return;

            string tool = EditorPanel.ActiveToolName;
            var clickPos = e.GetPosition(MainImage);

            if (clickPos.X < 0 || clickPos.X > MainImage.ActualWidth ||
                clickPos.Y < 0 || clickPos.Y > MainImage.ActualHeight)
                return;

            double scaleX = activeLayer.Width / MainImage.ActualWidth;
            double scaleY = activeLayer.Height / MainImage.ActualHeight;
            int px = (int)(clickPos.X * scaleX);
            int py = (int)(clickPos.Y * scaleY);

            if (tool == "Eyedropper")
            {
                PickColorWithEyedropper(px, py);
                return;
            }

            if (tool == "Fill")
            {
                int stride = activeLayer.Width * 4;
                var oldPixels = new byte[stride * activeLayer.Height];
                activeLayer.Bitmap.CopyPixels(oldPixels, stride, 0);

                var tempPixels = new byte[stride * activeLayer.Height];
                Array.Copy(oldPixels, tempPixels, oldPixels.Length);

                FloodFill(tempPixels, activeLayer.Width, activeLayer.Height, px, py, _node.EditorDoc.ForegroundColor);

                activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, activeLayer.Width, activeLayer.Height), tempPixels, stride, 0);
                activeLayer.InvalidateThumbnail();

                var newPixels = new byte[stride * activeLayer.Height];
                activeLayer.Bitmap.CopyPixels(newPixels, stride, 0);

                var cmd = new PixelEditCommand(activeLayer, oldPixels, newPixels);
                _node.EditorDoc.History.Execute(cmd);
                OnEditorDocumentModified();
                return;
            }

            if (tool == "Brush" || tool == "Eraser")
            {
                _isDrawingPixels = true;
                _lastDrawingPixelPoint = new Point(px, py);

                int stride = activeLayer.Width * 4;
                _oldPixelsForUndo = new byte[stride * activeLayer.Height];
                activeLayer.Bitmap.CopyPixels(_oldPixelsForUndo, stride, 0);

                _tempDrawingPixels = new byte[stride * activeLayer.Height];
                Array.Copy(_oldPixelsForUndo, _tempDrawingPixels, _oldPixelsForUndo.Length);

                bool isEraser = (tool == "Eraser");
                double radius = EditorPanel.BrushSize;
                double hardness = EditorPanel.BrushHardness;
                double flow = EditorPanel.BrushFlow;
                Color color = _node.EditorDoc.ForegroundColor;

                DrawBrushCircle(_tempDrawingPixels, activeLayer.Width, activeLayer.Height, px, py, radius, hardness, flow, color, isEraser);

                activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, activeLayer.Width, activeLayer.Height), _tempDrawingPixels, stride, 0);
                activeLayer.InvalidateThumbnail();
                OnEditorDocumentModified();

                MainScrollViewer.CaptureMouse();
            }
        }

        private void HandleManualEditorMouseMove(MouseEventArgs e)
        {
            if (!_isDrawingPixels || _node.EditorDoc == null || _tempDrawingPixels == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            string tool = EditorPanel.ActiveToolName;
            var mousePos = e.GetPosition(MainImage);

            double scaleX = activeLayer.Width / MainImage.ActualWidth;
            double scaleY = activeLayer.Height / MainImage.ActualHeight;
            int px = (int)(mousePos.X * scaleX);
            int py = (int)(mousePos.Y * scaleY);

            bool isEraser = (tool == "Eraser");
            double radius = EditorPanel.BrushSize;
            double hardness = EditorPanel.BrushHardness;
            double flow = EditorPanel.BrushFlow;
            Color color = _node.EditorDoc.ForegroundColor;

            var currentPoint = new Point(px, py);
            DrawBrushLine(_tempDrawingPixels, activeLayer.Width, activeLayer.Height, _lastDrawingPixelPoint, currentPoint, radius, hardness, flow, color, isEraser);
            _lastDrawingPixelPoint = currentPoint;

            int stride = activeLayer.Width * 4;
            activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, activeLayer.Width, activeLayer.Height), _tempDrawingPixels, stride, 0);
            activeLayer.InvalidateThumbnail();
            OnEditorDocumentModified();
        }

        private void HandleManualEditorMouseUp()
        {
            if (!_isDrawingPixels) return;
            _isDrawingPixels = false;
            MainScrollViewer.ReleaseMouseCapture();

            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null || _oldPixelsForUndo == null) return;

            int stride = activeLayer.Width * 4;
            var newPixels = new byte[stride * activeLayer.Height];
            activeLayer.Bitmap.CopyPixels(newPixels, stride, 0);

            var cmd = new PixelEditCommand(activeLayer, _oldPixelsForUndo, newPixels);
            _node.EditorDoc.History.Execute(cmd);

            _tempDrawingPixels = null;
            _oldPixelsForUndo = null;

            OnEditorDocumentModified();
        }

        private void DrawBrushCircle(byte[] pixels, int width, int height, double cx, double cy, double radius, double hardness, double flow, Color color, bool isEraser)
        {
            int startX = Math.Max(0, (int)Math.Floor(cx - radius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + radius));
            int startY = Math.Max(0, (int)Math.Floor(cy - radius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + radius));

            double r2 = radius * radius;
            double innerRadius = radius * (hardness / 100.0);
            double flowMul = flow / 100.0;

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width * 4;
                double dy = y - cy;
                double dy2 = dy * dy;

                for (int x = startX; x <= endX; x++)
                {
                    double dx = x - cx;
                    double dist2 = dx * dx + dy2;

                    if (dist2 <= r2)
                    {
                        double dist = Math.Sqrt(dist2);
                        double pixelOpacity = 1.0;
                        if (dist > innerRadius)
                        {
                            if (radius - innerRadius > 0.001)
                            {
                                pixelOpacity = 1.0 - (dist - innerRadius) / (radius - innerRadius);
                            }
                            else
                            {
                                pixelOpacity = 0.0;
                            }
                        }

                        double brushAlpha = pixelOpacity * flowMul;
                        if (brushAlpha <= 0) continue;

                        int pixelOffset = rowOffset + x * 4;

                        if (isEraser)
                        {
                            byte oldA = pixels[pixelOffset + 3];
                            byte newA = (byte)Math.Clamp(oldA * (1.0 - brushAlpha), 0, 255);
                            pixels[pixelOffset + 3] = newA;
                        }
                        else
                        {
                            byte bB = pixels[pixelOffset];
                            byte bG = pixels[pixelOffset + 1];
                            byte bR = pixels[pixelOffset + 2];
                            byte bA = pixels[pixelOffset + 3];

                            double srcA = color.A / 255.0 * brushAlpha;
                            double dstA = bA / 255.0;
                            double outA = srcA + dstA * (1.0 - srcA);

                            if (outA > 0)
                            {
                                byte outR = (byte)Math.Clamp(((color.R * srcA) + (bR * dstA * (1.0 - srcA))) / outA, 0, 255);
                                byte outG = (byte)Math.Clamp(((color.G * srcA) + (bG * dstA * (1.0 - srcA))) / outA, 0, 255);
                                byte outB = (byte)Math.Clamp(((color.B * srcA) + (bB * dstA * (1.0 - srcA))) / outA, 0, 255);

                                pixels[pixelOffset] = outB;
                                pixels[pixelOffset + 1] = outG;
                                pixels[pixelOffset + 2] = outR;
                                pixels[pixelOffset + 3] = (byte)(outA * 255.0);
                            }
                        }
                    }
                }
            }
        }

        private void DrawBrushLine(byte[] pixels, int width, int height, Point p1, Point p2, double radius, double hardness, double flow, Color color, bool isEraser)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);

            if (len == 0)
            {
                DrawBrushCircle(pixels, width, height, p1.X, p1.Y, radius, hardness, flow, color, isEraser);
                return;
            }

            double step = Math.Max(1.0, radius / 4.0);
            for (double d = 0; d <= len; d += step)
            {
                double cx = p1.X + (dx * d / len);
                double cy = p1.Y + (dy * d / len);
                DrawBrushCircle(pixels, width, height, cx, cy, radius, hardness, flow, color, isEraser);
            }
            DrawBrushCircle(pixels, width, height, p2.X, p2.Y, radius, hardness, flow, color, isEraser);
        }

        private void FloodFill(byte[] pixels, int width, int height, int startX, int startY, Color fillColor)
        {
            int stride = width * 4;
            int offset = startY * stride + startX * 4;
            byte targetB = pixels[offset];
            byte targetG = pixels[offset + 1];
            byte targetR = pixels[offset + 2];
            byte targetA = pixels[offset + 3];

            byte fillB = fillColor.B;
            byte fillG = fillColor.G;
            byte fillR = fillColor.R;
            byte fillA = fillColor.A;

            if (targetB == fillB && targetG == fillG && targetR == fillR && targetA == fillA)
                return;

            var queue = new System.Collections.Generic.Queue<Point>();
            queue.Enqueue(new Point(startX, startY));

            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                int x = (int)p.X;
                int y = (int)p.Y;

                if (x < 0 || x >= width || y < 0 || y >= height) continue;

                int currentOffset = y * stride + x * 4;
                if (pixels[currentOffset] == targetB &&
                    pixels[currentOffset + 1] == targetG &&
                    pixels[currentOffset + 2] == targetR &&
                    pixels[currentOffset + 3] == targetA)
                {
                    pixels[currentOffset] = fillB;
                    pixels[currentOffset + 1] = fillG;
                    pixels[currentOffset + 2] = fillR;
                    pixels[currentOffset + 3] = fillA;

                    queue.Enqueue(new Point(x + 1, y));
                    queue.Enqueue(new Point(x - 1, y));
                    queue.Enqueue(new Point(x, y + 1));
                    queue.Enqueue(new Point(x, y - 1));
                }
            }
        }

        private void PickColorWithEyedropper(int px, int py)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            if (px >= 0 && px < activeLayer.Width && py >= 0 && py < activeLayer.Height)
            {
                var stride = activeLayer.Width * 4;
                var singlePixel = new byte[4];
                activeLayer.Bitmap.CopyPixels(new Int32Rect(px, py, 1, 1), singlePixel, stride, 0);

                Color picked = Color.FromArgb(singlePixel[3], singlePixel[2], singlePixel[1], singlePixel[0]);
                _node.EditorDoc.ForegroundColor = picked;
            }
        }

        #endregion
    }
}
