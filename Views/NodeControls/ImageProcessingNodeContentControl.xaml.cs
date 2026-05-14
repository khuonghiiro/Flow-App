using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
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

                double leftStarMul = _widgetExpandedFullscreen ? 0.68 : 0.92;
                RootLayout.ColumnDefinitions[0].Width = new GridLength(0.6 * leftStarMul, GridUnitType.Star);

                var identity = Transform.Identity;
                TopMenuBorder.LayoutTransform = identity;
                RightMenuBorder.LayoutTransform = identity;
                IpProcessorHost.LayoutTransform = identity;
                LeftMenuBorder.LayoutTransform = identity;
                PlaceholderTextBlock.LayoutTransform = identity;
                CropsLabelText.LayoutTransform = identity;
                RenderLabelText.LayoutTransform = identity;

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
            RightMenuBorder.LayoutTransform = new ScaleTransform(ipTextScaleFactor, ipTextScaleFactor);
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
