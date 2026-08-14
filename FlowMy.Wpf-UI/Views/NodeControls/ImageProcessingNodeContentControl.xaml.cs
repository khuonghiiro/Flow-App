// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS & DEVELOPERS:
// DO NOT ALLOW ANY FILE IN THIS COMPONENT TO EXCEED ~1500 LINES OF CODE!
// To maintain readability, ease of testing, and modularity:
// - If a file grows larger than ~1500 lines, you MUST split/separate the logic into a new
//   partial class file (e.g., ImageProcessingNodeContentControl.<FeatureName>.cs).
// - Always place distinct features, tools, or event groupings in their respective files.
// - Ensure comments and documentation remain clean and structured.
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
        private System.Threading.CancellationTokenSource? _fxCts;
        private bool _isFxRunning;
        private bool _isSpacePressed;
        private double _lastCenterImageWidth = 0;
        private double _lastCenterImageHeight = 0;
        private bool _hasUserCentered = false;

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

        private void IpToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleIPColumn();
        }

        private void CommonButton_StopBubbling_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

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
            MainScrollViewer.SizeChanged += (s, e) =>
            {
                if (!_hasUserCentered && e.NewSize.Width > 0 && _lastCenterImageWidth > 0)
                {
                    CenterImageOnCanvas(_lastCenterImageWidth, _lastCenterImageHeight);
                }
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
                TopOptionsBar.LayoutTransform = identity;
                EditorToolbox.LayoutTransform = identity;
                IpProcessorHost.LayoutTransform = identity;
                PlaceholderTextBlock.LayoutTransform = identity;
                EditorPanel.LayoutTransform = new ScaleTransform(typoMul, typoMul);

                var formatMode = typoMul == 1.0 ? TextFormattingMode.Display : TextFormattingMode.Ideal;
                TextOptions.SetTextFormattingMode(EditorPanel, formatMode);
                TextOptions.SetTextFormattingMode(IpProcessorHost, formatMode);
                TextOptions.SetTextFormattingMode(TopMenuBorder, formatMode);
                TextOptions.SetTextFormattingMode(TopOptionsBar, formatMode);
                TextOptions.SetTextFormattingMode(EditorToolbox, formatMode);

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

                var widgetPopupScale = new ScaleTransform(typoMul, typoMul);
                UpdatePopupsLayoutScale(widgetPopupScale);

                ImageProcessingNodeControl.UpdateInteractionVisualScale(_handleOverlay, _node, typoMul);
                return;
            }

            // EditorToolbox tự co dãn vừa đủ chiều rộng theo icon (GridLength.Auto)
            RootLayout.ColumnDefinitions[0].Width = GridLength.Auto;

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

            double editorScaleVal = Math.Max(0.5, (_node.Width * 0.32) / 220.0);
            var editorScale = new ScaleTransform(editorScaleVal, editorScaleVal);

            // Gán tỉ lệ co dãn tương đương cho TopMenuBorder, TopOptionsBar và EditorToolbox giống như EditorPanel khi ở workflow
            TopMenuBorder.LayoutTransform = editorScale;
            TopOptionsBar.LayoutTransform = editorScale;
            EditorToolbox.LayoutTransform = editorScale;
            TextOptions.SetTextFormattingMode(TopMenuBorder, TextFormattingMode.Ideal);
            TextOptions.SetTextFormattingMode(TopOptionsBar, TextFormattingMode.Ideal);
            TextOptions.SetTextFormattingMode(EditorToolbox, TextFormattingMode.Ideal);

            var topBarScale = new ScaleTransform(heightScaleFactor, heightScaleFactor);
            IpProcessorHost.LayoutTransform = topBarScale;
            TextOptions.SetTextFormattingMode(IpProcessorHost, TextFormattingMode.Ideal);

            EditorPanel.LayoutTransform = editorScale;
            TextOptions.SetTextFormattingMode(EditorPanel, TextFormattingMode.Ideal);

            UpdatePopupsLayoutScale(editorScale);

            var canvasIpTextTransform = new ScaleTransform(ipTextScaleFactor, ipTextScaleFactor);
            PlaceholderTextBlock.LayoutTransform = canvasIpTextTransform;
            // CropsLabelText.LayoutTransform = Transform.Identity;
            // RenderLabelText.LayoutTransform = Transform.Identity;

            // LeftMenuBorder.LayoutTransform = new ScaleTransform(widthScaleFactor, widthScaleFactor);

            var interactionScale = Math.Max(Math.Max(heightScaleFactor, widthScaleFactor), ipTextScaleFactor);
            ImageProcessingNodeControl.UpdateInteractionVisualScale(_handleOverlay, _node, interactionScale);
        }

        private void UpdatePopupsLayoutScale(Transform popupScale)
        {
            void ConfigurePopupChild(FrameworkElement? child)
            {
                if (child == null) return;
                child.LayoutTransform = popupScale;
                TextOptions.SetTextFormattingMode(child, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(child, TextRenderingMode.Auto);
                RenderOptions.SetClearTypeHint(child, ClearTypeHint.Enabled);
                RenderOptions.SetBitmapScalingMode(child, BitmapScalingMode.HighQuality);
            }

            ConfigurePopupChild(BrushSettingsPopup?.Child as FrameworkElement);
            ConfigurePopupChild(FxGroupPopup?.Child as FrameworkElement);
            ConfigurePopupChild(SelectionGroupPopup?.Child as FrameworkElement);
            ConfigurePopupChild(EditorPanel?.LayerActionPopup?.Child as FrameworkElement);
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

            if (EditorPanel != null)
            {
                string activeTool = EditorPanel.ActiveToolName;
                if (activeTool == "Brush" || activeTool == "Eraser")
                {
                    Dispatcher.BeginInvoke(new Action(() => MainScrollViewer?.Focus()), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }

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
    }
}
