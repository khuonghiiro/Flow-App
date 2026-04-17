using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utilities;
using FlowMy.ViewModels;
using FlowMy.Workflow;
using FlowMy.Properties;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShapesPath = System.Windows.Shapes.Path;
using System.Windows.Threading;
using static FlowMy.Services.Rendering.ConnectionRenderer;

namespace FlowMy.Views
{
    /// <summary>
    /// Interaction logic for WorkflowEditorWindow.xaml
    /// </summary>
    public partial class WorkflowEditorWindow : Window
    {
        public WorkflowEditorViewModel? ViewModel => DataContext as WorkflowEditorViewModel;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly WorkflowEditorEventService _eventService;

        private WorkflowNode? _draggedNode;
        private Point _dragOffset;
        private WorkflowNode? _connectingFromNode;
        private ShapesPath? _tempLine;
        private WorkflowConnection? _selectedConnection; // ⭐ Connection đang được chọn/hover để xóa bằng phím Delete

        // Zoom và Pan
        private double _zoomLevel = 1.0;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 3.0;
        private const double ZoomStep = 0.1;
        private Point _panStartPoint;
        private bool _isPanning = false;

        // Animation cho connection lines
        private bool _isAnimationEnabled = true;
        private ConnectionAnimationDisplayMode _connectionAnimationDisplayMode = ConnectionAnimationDisplayMode.Animated;
        private DispatcherTimer? _executionHighlightThrottleTimer;
        private WorkflowConnection? _pendingExecutionHighlightConnection;
        private bool _executionHighlightDirty;

        // Grid pattern
        private string _currentGridType = "None";

        // Drag from template
        private bool _isDraggingFromTemplate = false;
        private string? _draggingNodeType = null;
        private Border? _dragGhost = null; // Ghost preview khi đang kéo

        // Z-index management
        private readonly ZIndexManager _zIndexManager;

        // Connection line color mode
        private ConnectionColorMode _connectionColorMode = ConnectionColorMode.NodeColor;
        private Color _customConnectionColor = Colors.LimeGreen;
        private string _customConnectionColorKey = "LimeGreen";

        // Execution energy color mode (for active connection)
        private ConnectionEnergyColorMode _connectionEnergyColorMode = ConnectionEnergyColorMode.FollowLineColor;
        private Color _customEnergyColor = Color.FromRgb(255, 215, 0); // Gold
        private string _customEnergyColorKey = "Gold";
        private double _energyDotGap = Settings.Default.EnergyDotGap <= 0 ? 8.0 : Settings.Default.EnergyDotGap;
        private double _energyDotThicknessExtra = Settings.Default.EnergyDotThicknessExtra == 0 ? 0.8 : Settings.Default.EnergyDotThicknessExtra;
        private string _energyDotText = Settings.Default.EnergyDotText ?? string.Empty; // nếu set text => thay line animation bằng text chạy
        private bool _energyDotTextRotate = Settings.Default.EnergyDotTextRotate;
        private double _energyRunSpeed = Settings.Default.EnergyRunSpeed <= 0 ? 1.0 : Settings.Default.EnergyRunSpeed; // multiplier
        private double _energyTextSpinSeconds = Settings.Default.EnergyTextSpinSeconds <= 0 ? 0.7 : Settings.Default.EnergyTextSpinSeconds; // seconds / vòng 360
        private bool _energyMeteorMode;
        private bool _isApplyingEnergyMenuState = false;
        
        // UI chrome hide/show when expanding a node to viewport
        private bool _isViewportExpandedUiHidden = false;
        private Visibility _prevLeftMenuBorderVisibility = Visibility.Visible;
        private Visibility _prevTopToolbarBorderVisibility = Visibility.Visible;
        private Visibility _prevWorkflowManagementPanelVisibility = Visibility.Visible;
        private Visibility _prevExecutionPanelVisibility = Visibility.Visible;
        private Visibility _prevPersistencePanelVisibility = Visibility.Visible;
        private Visibility _prevNodePaletteExpandButtonVisibility = Visibility.Collapsed;
        private ScrollBarVisibility _prevCanvasHScroll = ScrollBarVisibility.Hidden;
        private ScrollBarVisibility _prevCanvasVScroll = ScrollBarVisibility.Hidden;
        private GridLength _prevLeftMenuColumnWidth = new GridLength(200);
        private double _prevLeftMenuColumnMinWidth = 150;
        private double _prevLeftMenuColumnMaxWidth = 400;
        private GridLength _prevSplitterColumnWidth = new GridLength(5);
        private double _prevSplitterColumnMinWidth = 5;
        private double _prevSplitterColumnMaxWidth = 5;

        private ConnectionLineStyle _connectionLineStyle = ConnectionLineStyle.Bezier;

        // GPU Settings
        private bool _gpuEnabled = Settings.Default.GpuEnabled;
        private GpuRenderQuality _gpuRenderQuality = (GpuRenderQuality)Settings.Default.GpuRenderQuality;

        // When enabled, cache node borders (BitmapCache) and replace connection/energy animation
        // with a small spinner on each executing node.
        private bool _cacheNodeEnabled = false;
        private bool _nodeSpinnerArcMode = true;
        private bool _nodeSpinnerMultiColor = false;
        private double _nodeSpinnerSize = 26.0;
        private bool _nodeSpinnerScaleWithNode = false;
        private double _nodeSpinnerSizeRatio = 0.32;
        private string _nodeSpinnerShape = "Circle";
        private string _nodeSpinnerPosition = "TopRight";
        private double _nodeSpinnerStrokeThickness = 3.2;
        private double _nodeSpinnerSpinSeconds = 1.1;
        private bool _nodeSpinnerBlinkBackground = false;
        private string _nodeSpinnerBlinkBackgroundColorKey = "WarningBrush";
        private string _nodeSpinnerBlinkMode = "Soft";
        private double _nodeSpinnerBlinkIntensity = 0.65;
        
        private enum CanvasDisplayMode
        {
            ShowAll = 0,
            ViewportOnly = 1
        }

        private CanvasDisplayMode _canvasDisplayMode = CanvasDisplayMode.ShowAll;

        // Injected services
        private readonly FlowMy.Services.Layout.AutoLayoutService _layoutService;
        private readonly FlowMy.Services.Utilities.MinimapService _minimapService;
        private readonly GridPatternService _gridPatternService;
        private readonly ColorThemeService _colorThemeService;

        private readonly IConnectionRenderer _connectionRenderer;
        private readonly ConnectionHandler _connectionHandler;
        private readonly DragDropHandler _dragDropHandler;
        private readonly ZoomPanHandler _zoomPanHandler;
        private readonly INodeRenderer _nodeRenderer;
        private readonly PortRenderer _portRenderer;
        private readonly ConditionalNodeRenderer _conditionalNodeRenderer;
        private readonly AsyncTaskNodeRenderer _asyncTaskNodeRenderer;
        private readonly ScreenPositionNodeRenderer _screenPositionNodeRenderer;
        private readonly ViewportCullingService _viewportCullingService;
        private readonly NodeDialogManager _nodeDialogManager;

        // Kéo thả panel nổi hiển thị node đang chạy
        private bool _isDraggingExecutionPanel;
        private Point _executionPanelDragStart;
        private double _executionPanelStartLeft;
        private double _executionPanelStartTop;

        // Auto-scheduled Start scopes
        private DispatcherTimer? _autoStartSchedulerTimer;
        private readonly Dictionary<string, DateTime> _autoStartNextRunAt = new();
        private readonly HashSet<string> _autoStartRunningIds = new();
        private readonly Dictionary<string, Border> _autoStartScopeBorders = new();
        private readonly Dictionary<string, HashSet<string>> _autoStartScopeNodeIds = new();
        private bool _isDraggingAutoScope;
        private string? _draggingAutoScopeStartId;
        private Point _autoScopeDragStart;
        private readonly Dictionary<string, Point> _autoScopeNodeStartPositions = new();
        private Point _autoScopeFrameCanvasAtDragStart;

        public WorkflowEditorWindow(
            WorkflowEditorViewModel viewModel,
            IWorkflowEditorHostAccessor hostAccessor,
            WorkflowEditorEventService eventService,
            ZIndexManager zIndexManager,
            FlowMy.Services.Layout.AutoLayoutService layoutService,
            FlowMy.Services.Utilities.MinimapService minimapService,
            GridPatternService gridPatternService,
            ColorThemeService colorThemeService,
            FlowMy.Workflow.TemplateFactory templateFactory,
            IConnectionRenderer connectionRenderer,
            ConnectionHandler connectionHandler,
            DragDropHandler dragDropHandler,
            ZoomPanHandler zoomPanHandler,
            INodeRenderer nodeRenderer,
            PortRenderer portRenderer,
            ConditionalNodeRenderer conditionalNodeRenderer,
            AsyncTaskNodeRenderer asyncTaskNodeRenderer,
            ScreenPositionNodeRenderer screenPositionNodeRenderer,
            ViewportCullingService viewportCullingService,
            NodeDialogManager nodeDialogManager)
        {
            InitializeComponent();

            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            _zIndexManager = zIndexManager ?? throw new ArgumentNullException(nameof(zIndexManager));

            _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
            _minimapService = minimapService ?? throw new ArgumentNullException(nameof(minimapService));
            _gridPatternService = gridPatternService ?? throw new ArgumentNullException(nameof(gridPatternService));
            _colorThemeService = colorThemeService ?? throw new ArgumentNullException(nameof(colorThemeService));
            _templateFactory = templateFactory ?? throw new ArgumentNullException(nameof(templateFactory));

            _connectionRenderer = connectionRenderer ?? throw new ArgumentNullException(nameof(connectionRenderer));
            _connectionHandler = connectionHandler ?? throw new ArgumentNullException(nameof(connectionHandler));
            _dragDropHandler = dragDropHandler ?? throw new ArgumentNullException(nameof(dragDropHandler));
            _zoomPanHandler = zoomPanHandler ?? throw new ArgumentNullException(nameof(zoomPanHandler));
            _nodeRenderer = nodeRenderer ?? throw new ArgumentNullException(nameof(nodeRenderer));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _conditionalNodeRenderer = conditionalNodeRenderer ?? throw new ArgumentNullException(nameof(conditionalNodeRenderer));
            _asyncTaskNodeRenderer = asyncTaskNodeRenderer ?? throw new ArgumentNullException(nameof(asyncTaskNodeRenderer));
            _screenPositionNodeRenderer = screenPositionNodeRenderer ?? throw new ArgumentNullException(nameof(screenPositionNodeRenderer));
            _viewportCullingService = viewportCullingService ?? throw new ArgumentNullException(nameof(viewportCullingService));
            _nodeDialogManager = nodeDialogManager ?? throw new ArgumentNullException(nameof(nodeDialogManager));

            InitializeServices();
            SetupEventHandlers();
        }

        private void InitializeServices()
        {
            // Assign host for scoped services
            _hostAccessor.Host = this;
            
            // Load GPU settings
            LoadGpuSettings();
        }
        
        /// <summary>
        /// Load GPU settings từ user preferences
        /// </summary>
        private void LoadGpuSettings()
        {
            _gpuEnabled = Settings.Default.GpuEnabled;
            _gpuRenderQuality = (GpuRenderQuality)Settings.Default.GpuRenderQuality;
            
            // Validate quality value
            if (_gpuRenderQuality < GpuRenderQuality.Low || _gpuRenderQuality > GpuRenderQuality.Best)
            {
                _gpuRenderQuality = GpuRenderQuality.Medium;
            }
            
            // Apply settings
            ApplyGpuSettings();
        }
        
        /// <summary>
        /// Apply GPU settings to canvas và re-apply cho tất cả nodes/connections
        /// Tối ưu: Chỉ re-apply khi cần thiết để tránh lag
        /// </summary>
        private void ApplyGpuSettings()
        {
            OptimizeCanvasForGPU();
            
            // Re-apply GPU settings cho tất cả nodes và connections
            // Sử dụng Dispatcher.BeginInvoke để không block UI thread
            if (ViewModel != null && WorkflowCanvas != null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    // Re-apply cho tất cả nodes (không đang drag)
                    foreach (var node in ViewModel.Nodes)
                    {
                        if (node.Border != null && node != _draggedNode)
                        {
                            // Chỉ apply cache cho nodes không di chuyển
                            GpuOptimizationHelper.ApplyToBorder(node.Border, isDragging: false, forceCache: _cacheNodeEnabled);
                            
                            // CRITICAL: Re-apply drop shadow effect based on quality
                            node.Border.Effect = GpuOptimizationHelper.CreateDropShadowEffect();
                        }
                        
                        // Re-apply cho ports
                        foreach (var port in node.Ports.Where(p => p.PortUI != null))
                        {
                            var portShape = PortRenderer.GetActualPortShape(port.PortUI);
                            if (portShape != null)
                            {
                                GpuOptimizationHelper.ApplyToShape(portShape);
                            }
                        }
                    }
                    
                    // Re-apply cho tất cả connections
                    foreach (var conn in ViewModel.Connections)
                    {
                        if (conn.LineUI != null)
                        {
                            GpuOptimizationHelper.ApplyToPath(conn.LineUI, allowCache: !_isAnimationEnabled);
                            
                            // Re-apply drop shadow for arrow head
                            if (conn.LineUI.Tag is ConnectionTag tag && tag.ArrowHead != null)
                            {
                                // Keep arrow head crisp without shadow.
                                tag.ArrowHead.Effect = null;
                            }
                        }
                        if (conn.HitArea != null)
                        {
                            GpuOptimizationHelper.ApplyToPath(conn.HitArea, allowCache: false);
                        }
                        if (conn.EnergyUI != null)
                        {
                            GpuOptimizationHelper.ApplyToPath(conn.EnergyUI, allowCache: false);
                        }
                        // Re-apply drop shadow for energy ball
                        if (conn.EnergyBallUI != null)
                        {
                            conn.EnergyBallUI.Effect = GpuOptimizationHelper.CreateDropShadowEffect();
                        }
                        // Re-apply drop shadow for energy text
                        if (conn.EnergyTextUI != null)
                        {
                            conn.EnergyTextUI.Effect = GpuOptimizationHelper.CreateDropShadowEffect();
                        }
                        // Re-apply drop shadow for delete button
                        if (conn.DeleteButton != null)
                        {
                            conn.DeleteButton.Effect = GpuOptimizationHelper.CreateDropShadowEffect();
                        }
                    }
                }));
            }
        }

        /// <summary>
        /// Đặt vị trí mặc định cho panel node đang chạy: bên phải và giữa chiều cao canvas.
        /// </summary>
        private void ExecutionFloatingPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement panel) return;
            if (panel.Parent is not FrameworkElement canvas) return;

            // Lấy kích thước thực tế
            var canvasWidth = canvas.ActualWidth;
            var canvasHeight = canvas.ActualHeight;
            var panelWidth = panel.ActualWidth;
            var panelHeight = panel.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0 || panelWidth <= 0 || panelHeight <= 0)
                return;

            // Căn phải giữa chiều cao, cách mép phải 24px
            var left = Math.Max(0, canvasWidth - panelWidth - 24);
            var top = Math.Max(0, (canvasHeight - panelHeight) / 2);

            Canvas.SetLeft(panel, left);
            Canvas.SetTop(panel, top);
        }

        private void ExecutionFloatingPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement panel) return;
            if (panel.Parent is not FrameworkElement canvas) return;

            _isDraggingExecutionPanel = true;
            _executionPanelDragStart = e.GetPosition(canvas);

            var currentLeft = Canvas.GetLeft(panel);
            var currentTop = Canvas.GetTop(panel);
            if (double.IsNaN(currentLeft)) currentLeft = 0;
            if (double.IsNaN(currentTop)) currentTop = 0;

            _executionPanelStartLeft = currentLeft;
            _executionPanelStartTop = currentTop;

            panel.CaptureMouse();
            e.Handled = true;
        }

        private void ExecutionFloatingPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingExecutionPanel) return;
            if (sender is not FrameworkElement panel) return;
            if (panel.Parent is not FrameworkElement canvas) return;

            var pos = e.GetPosition(canvas);
            var dx = pos.X - _executionPanelDragStart.X;
            var dy = pos.Y - _executionPanelDragStart.Y;

            var newLeft = _executionPanelStartLeft + dx;
            var newTop = _executionPanelStartTop + dy;

            // Giới hạn trong vùng canvas
            var maxLeft = Math.Max(0, canvas.ActualWidth - panel.ActualWidth);
            var maxTop = Math.Max(0, canvas.ActualHeight - panel.ActualHeight);

            newLeft = Math.Min(Math.Max(0, newLeft), maxLeft);
            newTop = Math.Min(Math.Max(0, newTop), maxTop);

            Canvas.SetLeft(panel, newLeft);
            Canvas.SetTop(panel, newTop);
        }

        private void ExecutionFloatingPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingExecutionPanel) return;
            if (sender is not FrameworkElement panel) return;

            _isDraggingExecutionPanel = false;
            panel.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void RunningNodeItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not WorkflowNode node) return;

            TryScrollToNodePreserveZoom(node);
            e.Handled = true;
        }

        /// <summary>
        /// Di chuyển viewport đến node được chọn mà vẫn giữ nguyên zoom hiện tại.
        /// </summary>
        private void TryScrollToNodePreserveZoom(WorkflowNode node)
        {
            if (node == null) return;
            if (ScrollViewer == null || TranslateTransform == null || ScaleTransform == null) return;

            // Chặn trường hợp sau khi đóng dialog, WPF có thể bắn mouse-up xuống panel/canvas bên dưới
            // khiến viewport "pan/nhảy" không mong muốn.
            if (_nodeDialogManager != null)
            {
                if (_nodeDialogManager.IsDialogOpen) return;
                var lastClosed = _nodeDialogManager.LastDialogClosedUtc;
                if (lastClosed != DateTime.MinValue &&
                    (DateTime.UtcNow - lastClosed).TotalMilliseconds < 800)
                    return;
            }

            // Optional: chọn node trong UI (nếu có binding/logic selection)
            try
            {
                if (ViewModel != null) ViewModel.SelectedNode = node;
            }
            catch { }

            // Ưu tiên kích thước thực tế của UI node để center chuẩn hơn
            var w = node.Border?.ActualWidth ?? 0;
            var h = node.Border?.ActualHeight ?? 0;
            if (w <= 1) w = 280;
            if (h <= 1) h = 160;

            var zoom = ScaleTransform.ScaleX;
            if (zoom <= 0.0001) zoom = 1.0;

            // Center theo tâm node trong toạ độ canvas (unscaled)
            var targetCanvasX = node.X + (w / 2);
            var targetCanvasY = node.Y + (h / 2);

            // Convert từ canvas coord -> content coord trong ScrollViewer (đã tính translate và zoom)
            var targetContentX = (targetCanvasX * zoom) + TranslateTransform.X;
            var targetContentY = (targetCanvasY * zoom) + TranslateTransform.Y;

            // Viewport size
            var viewportW = ScrollViewer.ViewportWidth;
            var viewportH = ScrollViewer.ViewportHeight;
            if (viewportW <= 1) viewportW = ScrollViewer.ActualWidth;
            if (viewportH <= 1) viewportH = ScrollViewer.ActualHeight;

            // Scroll sao cho target nằm giữa viewport
            var scrollX = targetContentX - (viewportW / 2);
            var scrollY = targetContentY - (viewportH / 2);

            if (double.IsNaN(scrollX) || double.IsInfinity(scrollX)) scrollX = 0;
            if (double.IsNaN(scrollY) || double.IsInfinity(scrollY)) scrollY = 0;

            // Scroll phải chạy sau layout để ViewportWidth/Height ổn định
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    ScrollViewer.ScrollToHorizontalOffset(Math.Max(0, scrollX));
                    ScrollViewer.ScrollToVerticalOffset(Math.Max(0, scrollY));
                }
                catch { }
            }));
        }
        
        /// <summary>
        /// Property để binding với CheckBox
        /// </summary>
        public bool GpuEnabled
        {
            get => _gpuEnabled;
            set
            {
                if (_gpuEnabled != value)
                {
                    _gpuEnabled = value;
                    Settings.Default.GpuEnabled = value;
                    Settings.Default.Save();
                    ApplyGpuSettings();
                }
            }
        }
        
        /// <summary>
        /// Property để binding với ComboBox
        /// </summary>
        public GpuRenderQuality GpuRenderQuality
        {
            get => _gpuRenderQuality;
            set
            {
                if (_gpuRenderQuality != value)
                {
                    _gpuRenderQuality = value;
                    Settings.Default.GpuRenderQuality = (int)value;
                    Settings.Default.Save();
                    ApplyGpuSettings();
                }
            }
        }

        private void SetupEventHandlers()
        {
            // Dùng PreviewKeyDown (tunneling) thay vì KeyDown để bắt Ctrl+C/V kể cả khi focus đang ở ComboBox (workflow selector)
            PreviewKeyDown += WorkflowEditorWindow_PreviewKeyDown;
            Focusable = true;

            // Đóng dialog khi window mất focus (ẩn app hoặc chuyển sang app khác)
            Deactivated += WorkflowEditorWindow_Deactivated;
            // Đóng dialog khi window bị minimize
            StateChanged += WorkflowEditorWindow_StateChanged;
            
            // Responsive layout khi thay đổi kích thước window
            SizeChanged += WorkflowEditorWindow_SizeChanged;

            if (ViewModel != null)
            {
                ViewModel.Nodes.CollectionChanged += Nodes_CollectionChanged;
                ViewModel.Connections.CollectionChanged += Connections_CollectionChanged;
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                ViewModel.SyncViewStateBeforeSave = SyncViewStateToViewModel;
                _eventService.InitialRender();
            }

            Loaded += (s, e) =>
            {
                OptimizeCanvasForGPU();
                
                // Initialize GPU settings UI
                InitializeGpuSettingsUI();
                ApplyCanvasDisplayMode(_canvasDisplayMode, forceRefresh: false);

                InitializeMinimap();
                InitializeZoomPan();
                ApplyResponsiveInitialZoom(); // Áp dụng zoom tương ứng màn hình (chỉ fresh workflow)
                ScrollToCenter();

                // Initialize theme from saved preference BEFORE drawing grid
                _colorThemeService?.LoadThemePreference();
                UpdateThemeToggleIcon();
                InitializeThemeSelector();

                // Default grid type if user has no saved preference yet
                _currentGridType = "Dots";
                UpdateGridPattern(); // Use current theme's CanvasGridBrush
                ApplyCanvasToolbarPreferences();

                // Subscribe to theme changed event
                if (_colorThemeService != null)
                {
                    _colorThemeService.ThemeChanged += (sender, args) =>
                    {
                        // Update icon when theme changes
                        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                        {
                            UpdateThemeToggleIcon();
                            UpdateGridPattern(); // Refresh grid pattern colors
                        }));
                    };
                }

                // Setup scroll viewer events để trigger viewport culling
                if (ScrollViewer != null)
                {
                    ScrollViewer.ScrollChanged += (sender, args) =>
                    {
                        _viewportCullingService?.OnViewportChanged();
                    };
                }

                // Force initial viewport culling update
                _viewportCullingService?.ForceUpdate();
                EnsureAutoStartSchedulerStarted();
                RefreshAutoStartScopeBorders();

                InitializeNodePaletteFromSettings();
            };

            SetConnectionAnimationDisplayMode(ConnectionAnimationDisplayMode.Animated);
        }

        // ─── THEME SELECTOR ───────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo ThemeSelector ComboBox với theme đang dùng
        /// </summary>
        private void InitializeThemeSelector()
        {
            if (ThemeSelector == null) return;

            var currentTheme = _colorThemeService?.CurrentTheme ?? "Light";

            // Suppress SelectionChanged during init
            ThemeSelector.SelectionChanged -= ThemeSelector_SelectionChanged;
            foreach (ComboBoxItem item in ThemeSelector.Items)
            {
                if (item.Tag?.ToString() == currentTheme)
                {
                    ThemeSelector.SelectedItem = item;
                    break;
                }
            }
            ThemeSelector.SelectionChanged += ThemeSelector_SelectionChanged;
        }

        /// <summary>
        /// Khi user chọn theme từ ComboBox
        /// </summary>
        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
            {
                var themeName = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(themeName) && _colorThemeService != null)
                {
                    // Preserve current viewport anchor to avoid "jump" after theme switch
                    var hOff = ScrollViewer?.HorizontalOffset ?? 0;
                    var vOff = ScrollViewer?.VerticalOffset ?? 0;
                    var zoom = _zoomLevel;
                    var tx = TranslateTransform?.X ?? 0;
                    var ty = TranslateTransform?.Y ?? 0;

                    _colorThemeService.LoadTheme(themeName);
                    // Ensure canvas grid color is refreshed immediately after theme switch.
                    UpdateGridPattern();

                    // After resources are swapped, force restore viewport + un-stuck WebView2 visibility.
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                    {
                        try
                        {
                            // Defensive: clear transient interaction states that can keep WebView2 hidden.
                            _isPanning = false;
                            _draggedNode = null;
                            try { WorkflowCanvas?.ReleaseMouseCapture(); } catch { }
                            NodeChrome.SetZoomingState(false);

                            // Restore transforms if any got reset by re-templating
                            _zoomLevel = zoom;
                            if (ScaleTransform != null)
                            {
                                ScaleTransform.ScaleX = zoom;
                                ScaleTransform.ScaleY = zoom;
                            }
                            if (TranslateTransform != null)
                            {
                                TranslateTransform.X = tx;
                                TranslateTransform.Y = ty;
                            }

                            if (ScrollViewer != null)
                            {
                                ScrollViewer.UpdateLayout();
                                ScrollViewer.ScrollToHorizontalOffset(hOff);
                                ScrollViewer.ScrollToVerticalOffset(vOff);
                            }

                            // Viewport culling bounds likely changed after theme re-measure
                            _viewportCullingService?.ForceUpdate();

                            // Recreate WebView-based node visuals to avoid stale HwndHost state
                            // on old nodes after resource dictionary/theme swap.
                            RebuildWebViewNodesAfterThemeSwitch();

                            // Theme/resource swaps can temporarily unload node visuals and remove
                            // execution indicators from the canvas. Re-attach them after layout settles.
                            if (ViewModel?.Nodes != null)
                            {
                                NodeChrome.RefreshExecutionIndicators(ViewModel.Nodes, this);
                            }

                            // WebView2 (HwndHost) can get stuck hidden after a global re-template.
                            // Best-effort: clear any clip region and re-show if it was left collapsed.
                            FixWebView2AfterThemeSwitch();

                            // Run one more pass on idle to catch controls recreated during layout cycle.
                            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                            {
                                FixWebView2AfterThemeSwitch();
                                if (ViewModel?.Nodes != null)
                                {
                                    NodeChrome.RefreshExecutionIndicators(ViewModel.Nodes, this);
                                }
                            }));
                        }
                        catch { }
                    }));
                }
            }
        }

        private void FixWebView2AfterThemeSwitch()
        {
            try
            {
                if (WorkflowCanvas == null) return;

                foreach (var wv in FindVisualChildren<WebView2>(WorkflowCanvas))
                {
                    // Clear any stale Win32 region (defensive)
                    try { WebView2AirspaceClipper.ClearClipping(wv); } catch { }

                    // If a WebView2 got stuck Collapsed during theme switch, show it back.
                    // Parent containers can still keep it hidden if it's not the active tab.
                    if (wv.Visibility == Visibility.Collapsed)
                    {
                        wv.Visibility = Visibility.Visible;
                    }
                }
            }
            catch { }
        }

        private void RebuildWebViewNodesAfterThemeSwitch()
        {
            try
            {
                if (ViewModel == null || WorkflowCanvas == null) return;

                var targets = ViewModel.Nodes
                    .Where(n => n.Type == NodeType.Web || n.Type == NodeType.HtmlUi)
                    .ToList();

                if (targets.Count == 0) return;

                foreach (var node in targets)
                {
                    NodeRendererService.RemoveNode(node, WorkflowCanvas);
                    RenderNode(node);
                }

                // Ports/visuals changed, so refresh connections geometry and hit areas.
                RenderAllConnections();
                _viewportCullingService?.ForceUpdate();
            }
            catch { }
        }



        private void WorkflowEditorWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                if (_nodeDialogManager != null && _nodeDialogManager.IsDialogOpen)
                {
                    _nodeDialogManager.CloseCurrentDialog();
                }
            }
        }

        /// <summary>
        /// Đóng dialog khi window mất focus (ẩn app hoặc chuyển sang app khác)
        /// Chỉ đóng khi window thực sự mất focus, không đóng khi click vào dialog
        /// </summary>
        private void WorkflowEditorWindow_Deactivated(object? sender, EventArgs e)
        {
            // Không auto-close dialog khi window deactivated.
            // Mở MessageBox/OpenFileDialog/SaveFileDialog có thể làm window tạm mất active,
            // nếu đóng dialog ở đây sẽ gây hiện tượng app tụt/ẩn sau modal.
            // Việc đóng dialog khi minimize vẫn được xử lý tại WorkflowEditorWindow_StateChanged.
        }

        /// <summary>
        /// Xử lý responsive layout khi thay đổi kích thước window
        /// </summary>
        private void WorkflowEditorWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (e == null) return;

            var windowWidth = e.NewSize.Width;
            var windowHeight = e.NewSize.Height;
            
            // Breakpoints cho responsive design
            const double smallScreenWidth = 1200;
            const double smallScreenHeight = 700;

            // Xử lý Topbar - ẩn text khi màn hình nhỏ
            var isSmallScreen = windowWidth < smallScreenWidth;
            var isVerySmallScreen = windowWidth < 1000;
            var isSmallHeight = windowHeight < smallScreenHeight;

            // Workflow Management Panel
            if (WorkflowLabel != null)
            {
                WorkflowLabel.Visibility = isVerySmallScreen ? Visibility.Collapsed : Visibility.Visible;
            }

            if (WorkflowSelector != null)
            {
                WorkflowSelector.Width = isVerySmallScreen ? 150 : (isSmallScreen ? 180 : 220);
            }

            // Execution Panel
            if (StartTestText != null)
            {
                StartTestText.Visibility = isSmallScreen ? Visibility.Collapsed : Visibility.Visible;
            }

            if (StopTestText != null)
            {
                StopTestText.Visibility = isSmallScreen ? Visibility.Collapsed : Visibility.Visible;
            }

            if (StartExecutionButton != null)
            {
                StartExecutionButton.Width = isSmallScreen ? double.NaN : 150;
                StartExecutionButton.MinWidth = isSmallScreen ? 50 : 120;
            }

            if (StopExecutionButton != null)
            {
                StopExecutionButton.Width = isSmallScreen ? double.NaN : 150;
                StopExecutionButton.MinWidth = isSmallScreen ? 50 : 120;
            }

            // Persistence Panel
            if (SaveText != null)
            {
                SaveText.Visibility = isSmallScreen ? Visibility.Collapsed : Visibility.Visible;
            }

            // Import/Export/ExportWebBundle buttons now icon-only — no text to hide/show

            if (SaveButton != null)
            {
                SaveButton.Width = isSmallScreen ? double.NaN : 100;
                SaveButton.MinWidth = isSmallScreen ? 40 : 80;
            }

            // Minimap - thu nhỏ khi màn hình nhỏ
            if (MinimapBorder != null)
            {
                if (isSmallScreen || isSmallHeight)
                {
                    MinimapBorder.Width = 150;
                    MinimapBorder.Height = 100;
                }
                else
                {
                    MinimapBorder.Width = 200;
                    MinimapBorder.Height = 150;
                }
            }

            // Left Menu - có thể thu gọn khi màn hình nhỏ
            if (LeftMenuColumn != null && isVerySmallScreen)
            {
                // Giữ nguyên MinWidth, nhưng có thể điều chỉnh nếu cần
                // LeftMenuColumn.Width = new GridLength(150);
            }
        }

        /// <summary>
        /// Khi user phóng to một node vừa khung nhìn: ẩn menu trái và các panel chứa button.
        /// </summary>
        /// <remarks>
        /// Node controls sẽ gọi method này thông qua host (WorkflowEditorWindow).
        /// </remarks>
        public void SetViewportExpandedUiHidden(bool hidden)
        {
            // Avoid re-entrancy and repeated saves.
            if (_isViewportExpandedUiHidden == hidden) return;
            _isViewportExpandedUiHidden = hidden;

            if (hidden)
            {
                StopNodePaletteWidthAnimation(applyFinal: false);

                // Save current visibilities once.
                _prevLeftMenuBorderVisibility = LeftMenuBorder?.Visibility ?? Visibility.Visible;
                _prevTopToolbarBorderVisibility = TopToolbarBorder?.Visibility ?? Visibility.Visible;
                _prevWorkflowManagementPanelVisibility = WorkflowManagementPanel?.Visibility ?? Visibility.Visible;
                _prevExecutionPanelVisibility = ExecutionPanel?.Visibility ?? Visibility.Visible;
                _prevPersistencePanelVisibility = PersistencePanel?.Visibility ?? Visibility.Visible;
                if (ScrollViewer != null)
                {
                    _prevCanvasHScroll = ScrollViewer.HorizontalScrollBarVisibility;
                    _prevCanvasVScroll = ScrollViewer.VerticalScrollBarVisibility;
                }

                _prevNodePaletteExpandButtonVisibility = NodePaletteExpandButton?.Visibility ?? Visibility.Collapsed;

                // Collapse left menu + splitter columns so canvas feels fullscreen.
                if (LeftMenuBorder?.Parent is Grid grid && grid.ColumnDefinitions.Count >= 2)
                {
                    var leftCol = grid.ColumnDefinitions[0];
                    var splitterCol = grid.ColumnDefinitions[1];

                    _prevLeftMenuColumnWidth = leftCol.Width;
                    _prevLeftMenuColumnMinWidth = leftCol.MinWidth;
                    _prevLeftMenuColumnMaxWidth = leftCol.MaxWidth;

                    _prevSplitterColumnWidth = splitterCol.Width;
                    _prevSplitterColumnMinWidth = splitterCol.MinWidth;
                    _prevSplitterColumnMaxWidth = splitterCol.MaxWidth;

                    leftCol.Width = new GridLength(0);
                    leftCol.MinWidth = 0;
                    leftCol.MaxWidth = 0;

                    splitterCol.Width = new GridLength(0);
                    splitterCol.MinWidth = 0;
                    splitterCol.MaxWidth = 0;
                }

                if (LeftMenuBorder != null) LeftMenuBorder.Visibility = Visibility.Collapsed;
                if (NodePaletteExpandButton != null) NodePaletteExpandButton.Visibility = Visibility.Collapsed;
                if (TopToolbarBorder != null) TopToolbarBorder.Visibility = Visibility.Collapsed;
                if (WorkflowManagementPanel != null) WorkflowManagementPanel.Visibility = Visibility.Collapsed;
                if (ExecutionPanel != null) ExecutionPanel.Visibility = Visibility.Collapsed;
                if (PersistencePanel != null) PersistencePanel.Visibility = Visibility.Collapsed;

                // Hide canvas scrollbars (bottom/right)
                if (ScrollViewer != null)
                {
                    ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }
            }
            else
            {
                // Restore visibilities.
                if (LeftMenuBorder != null) LeftMenuBorder.Visibility = _prevLeftMenuBorderVisibility;
                if (NodePaletteExpandButton != null) NodePaletteExpandButton.Visibility = _prevNodePaletteExpandButtonVisibility;
                if (TopToolbarBorder != null) TopToolbarBorder.Visibility = _prevTopToolbarBorderVisibility;
                if (WorkflowManagementPanel != null) WorkflowManagementPanel.Visibility = _prevWorkflowManagementPanelVisibility;
                if (ExecutionPanel != null) ExecutionPanel.Visibility = _prevExecutionPanelVisibility;
                if (PersistencePanel != null) PersistencePanel.Visibility = _prevPersistencePanelVisibility;

                // Restore canvas scrollbars
                if (ScrollViewer != null)
                {
                    ScrollViewer.HorizontalScrollBarVisibility = _prevCanvasHScroll;
                    ScrollViewer.VerticalScrollBarVisibility = _prevCanvasVScroll;
                }

                // Restore columns width.
                if (LeftMenuBorder?.Parent is Grid grid && grid.ColumnDefinitions.Count >= 2)
                {
                    var leftCol = grid.ColumnDefinitions[0];
                    var splitterCol = grid.ColumnDefinitions[1];

                    leftCol.Width = _prevLeftMenuColumnWidth;
                    leftCol.MinWidth = _prevLeftMenuColumnMinWidth;
                    leftCol.MaxWidth = _prevLeftMenuColumnMaxWidth;

                    splitterCol.Width = _prevSplitterColumnWidth;
                    splitterCol.MinWidth = _prevSplitterColumnMinWidth;
                    splitterCol.MaxWidth = _prevSplitterColumnMaxWidth;
                }
            }
        }

        private void GridType_None_Click(object sender, RoutedEventArgs e) => SetGridType("None");
        private void GridType_Dots_Click(object sender, RoutedEventArgs e) => SetGridType("Dots");
        private void GridType_Lines_Click(object sender, RoutedEventArgs e) => SetGridType("Lines");

        private void SetGridType(string type)
        {
            _currentGridType = type;
            UpdateGridPattern();
            var current = CanvasToolbarPreferencesStore.Load() ?? new CanvasToolbarPreferences();
            current.GridType = _currentGridType;
            current.CanvasDisplayMode = _canvasDisplayMode == CanvasDisplayMode.ViewportOnly ? "ViewportOnly" : "ShowAll";
            current.CullingPerformanceProfile = _viewportCullingService?.PerformanceProfile switch
            {
                ViewportCullingService.CullingPerformanceProfile.Low => "Low",
                ViewportCullingService.CullingPerformanceProfile.High => "High",
                _ => "Normal"
            };
            current.ConnectionLineStyle = _connectionLineStyle.ToString();
            current.ConnectionAnimationMode = _connectionAnimationDisplayMode.ToString();
            current.ConnectionColorMode = _connectionColorMode.ToString();
            current.CustomConnectionColorKey = _customConnectionColorKey;
            CanvasToolbarPreferencesStore.Save(current);
        }

        /// <summary>
        /// Sync zoom/pan từ host (UI thực tế) xuống ViewModel trước khi Save.
        /// Viewport = (ScrollViewer.Offset - TranslateTransform) / zoom. Khi restore ta set Scroll=0,
        /// nên phải lưu effectivePan = Translate - Scroll để khi restore hiển thị đúng.
        /// </summary>
        private void SyncViewStateToViewModel()
        {
            if (ViewModel == null || ScaleTransform == null || TranslateTransform == null || ScrollViewer == null) return;
            ViewModel.ZoomLevel = ScaleTransform.ScaleX;
            ViewModel.PanX = TranslateTransform.X - ScrollViewer.HorizontalOffset;
            ViewModel.PanY = TranslateTransform.Y - ScrollViewer.VerticalOffset;
            var center = GetViewportCenter();
            ViewModel.SavedViewportCenterX = center.X;
            ViewModel.SavedViewportCenterY = center.Y;
            ViewModel.SavedScreenWidth = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            ViewModel.SavedScreenHeight = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
        }

        private void WorkflowEditorWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _eventService.HandleKeyDown(e);
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _eventService.HandleViewModelPropertyChanged(e);

            // Khi bắt đầu load workflow (IsLoading -> true) dọn UI cũ để tránh port/line rác.
            if (e.PropertyName == nameof(WorkflowEditorViewModel.IsLoading))
            {
                if (ViewModel?.IsLoading == true)
                {
                    ClearVisualsForReload();
                }
                else if (ViewModel?.IsLoading == false)
                {
                    // Sau khi load xong: khôi phục zoom/pan đã lưu (effectivePan = Translate - Scroll).
                    // - Ở đây khôi phục ngay zoom/pan đã lưu để tránh trạng thái “hiển thị sai rồi nhảy lại”.
                    // Khôi phục zoom/pan SAU KHI layout sẵn sàng (1 frame Loaded, không cần ApplicationIdle → load nhanh hơn)
                    // Dùng Fit to View thay vì RestoreViewState: PanX/PanY dương thường hiển thị vùng trống.
                    // Fit to View đảm bảo mở lại luôn thấy canvas, nodes, có thể di chuyển.
                    var zoom = ViewModel.ZoomLevel;
                    var panX = ViewModel.PanX;
                    var panY = ViewModel.PanY;
                    var savedW = ViewModel.SavedScreenWidth;
                    var savedH = ViewModel.SavedScreenHeight;
                    var savedCenterX = ViewModel.SavedViewportCenterX;
                    var savedCenterY = ViewModel.SavedViewportCenterY;
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                    {
                        try
                        {
                            ZoomPanHandlerService.RestoreViewState(zoom, panX, panY);
                            // Giữ behavior cũ ổn định: không re-apply responsive scale sau load
                            // để tránh viewport bị nhảy ngoài vùng người dùng đã lưu.
                            _viewportCullingService?.ForceUpdate();
                            UpdateMinimap();
                            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                            {
                                _eventService.SetConnectionAnimationDisplayMode(GetCurrentConnectionAnimationDisplayMode());
                            ApplyCanvasToolbarPreferences();
                            }));

                            // Sau khi khôi phục view: nếu có node đang được đánh dấu phóng to thì ẩn chrome (left menu + top bar)
                            try
                            {
                                if (ViewModel?.Nodes != null)
                                {
                                    bool anyExpanded = ViewModel.Nodes.Any(n =>
                                        (n is FlowMy.Models.Nodes.HtmlUiNode h && h.IsViewportExpanded) ||
                                        (n is FlowMy.Models.Nodes.WebNode w && w.IsViewportExpanded));
                                    SetViewportExpandedUiHidden(anyExpanded);
                                }
                                else
                                {
                                    SetViewportExpandedUiHidden(false);
                                }
                            }
                            catch
                            {
                                // best effort; không để crash load workflow
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error restoring view state after load: {ex.Message}");
                        }
                    }));
                }
            }

            // Đồng bộ ConnectionLineStyle từ ViewModel xuống host khi load workflow
            if (e.PropertyName == nameof(WorkflowEditorViewModel.ConnectionLineStyle))
            {
                try
                {
                    if (ViewModel != null)
                    {
                        _connectionLineStyle = ViewModel.ConnectionLineStyle;
                        UpdateAllConnectionPaths();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error applying connection line style from ViewModel: {ex.Message}");
                }
            }

            // Highlight connection "đang truyền năng lượng" khi workflow chạy
            if (e.PropertyName == nameof(WorkflowEditorViewModel.ActiveExecutionConnection))
            {
                QueueExecutionConnectionHighlightUpdate();
            }

            // Bắt đầu chạy: sau khi Dispatcher từng bị nghẽn, storyboard dash đôi khi không còn bám IsExecutionActive — làm mới toàn bộ rồi áp lại cạnh đang active.
            if (e.PropertyName == nameof(WorkflowEditorViewModel.IsExecuting) && ViewModel?.IsExecuting == true)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (ViewModel == null) return;
                    ConnectionRendererService.UpdateAllConnectionAnimations(ViewModel.Connections);
                    // Đồng bộ anchor với VM trước khi bỏ qua null-transient (tránh giữ cạnh cũ khi bắt đầu phiên mới).
                    if (ViewModel.ActiveExecutionConnection == null)
                        _executionActiveConnection = null;
                    QueueExecutionConnectionHighlightUpdate();
                }));
            }

            // Kết thúc chạy: ShowExecutionPathNodes đã Collapsed node ngoài path; culling thường return sớm nếu viewport không đổi → cần refresh ngay.
            if (e.PropertyName == nameof(WorkflowEditorViewModel.IsExecuting) && ViewModel?.IsExecuting == false)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    ApplyExecutionConnectionHighlight(null);
                    _viewportCullingService?.ForceUpdate();
                }));
            }

        }

        private bool ShouldApplyResponsiveAfterLoad(double savedWidth, double savedHeight)
        {
            if (savedWidth <= 0 || savedHeight <= 0) return true;
            var currentW = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            var currentH = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            return Math.Abs(currentW - savedWidth) > 2 || Math.Abs(currentH - savedHeight) > 2;
        }

        private void CenterViewportToCanvasPoint(double canvasX, double canvasY)
        {
            if (ScrollViewer == null || ScaleTransform == null || TranslateTransform == null) return;
            var viewportW = ScrollViewer.ViewportWidth > 1 ? ScrollViewer.ViewportWidth : ScrollViewer.ActualWidth;
            var viewportH = ScrollViewer.ViewportHeight > 1 ? ScrollViewer.ViewportHeight : ScrollViewer.ActualHeight;
            var contentX = (canvasX * ScaleTransform.ScaleX) + TranslateTransform.X;
            var contentY = (canvasY * ScaleTransform.ScaleY) + TranslateTransform.Y;
            var scrollX = Math.Max(0, contentX - (viewportW / 2));
            var scrollY = Math.Max(0, contentY - (viewportH / 2));
            ScrollViewer.ScrollToHorizontalOffset(scrollX);
            ScrollViewer.ScrollToVerticalOffset(scrollY);
        }

        private void QueueExecutionConnectionHighlightUpdate()
        {
            _pendingExecutionHighlightConnection = ViewModel?.ActiveExecutionConnection;
            _executionHighlightDirty = true;

            if (_executionHighlightThrottleTimer == null)
            {
                _executionHighlightThrottleTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(20) // ~50 FPS: đủ mượt, giảm bão update khi stop.
                };
                _executionHighlightThrottleTimer.Tick += (_, __) =>
                {
                    if (!_executionHighlightDirty)
                    {
                        _executionHighlightThrottleTimer?.Stop();
                        return;
                    }

                    _executionHighlightDirty = false;
                    ApplyExecutionConnectionHighlight(_pendingExecutionHighlightConnection);
                };
            }

            if (!_executionHighlightThrottleTimer.IsEnabled)
                _executionHighlightThrottleTimer.Start();
        }

        /// <summary>
        /// Khi workflow load xong: kích hoạt nút Fit to View (giống user nhấn button) sau khi layout ổn định.
        /// </summary>
        private void FitToViewAfterRender()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (FitToViewButton != null && FitToViewButton.IsLoaded)
                    FitToViewButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            }));
        }

        #region Collection Change Handlers

        private void Nodes_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _eventService.HandleNodesCollectionChanged(e);
            RefreshAutoStartScopeBorders();
        }

        private void Connections_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _eventService.HandleConnectionsCollectionChanged(e);
            RefreshAutoStartScopeBorders();
        }

        private void EnsureAutoStartSchedulerStarted()
        {
            if (_autoStartSchedulerTimer != null)
            {
                if (!_autoStartSchedulerTimer.IsEnabled)
                {
                    _autoStartSchedulerTimer.Start();
                }
                return;
            }

            _autoStartSchedulerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _autoStartSchedulerTimer.Tick += AutoStartSchedulerTick;
            _autoStartSchedulerTimer.Start();
        }

        private void AutoStartSchedulerTick(object? sender, EventArgs e)
        {
            if (ViewModel == null) return;

            RefreshAutoStartScopeBorders();

            var autoStarts = ViewModel.GetAutoScheduledStartNodes();
            var autoIds = autoStarts.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var key in _autoStartNextRunAt.Keys.Where(k => !autoIds.Contains(k)).ToList())
            {
                _autoStartNextRunAt.Remove(key);
                _autoStartRunningIds.Remove(key);
            }

            var now = DateTime.UtcNow;
            foreach (var start in autoStarts)
            {
                if (_autoStartRunningIds.Contains(start.Id))
                {
                    continue;
                }

                if (!_autoStartNextRunAt.TryGetValue(start.Id, out var nextAt))
                {
                    _autoStartNextRunAt[start.Id] = now;
                    nextAt = now;
                }

                if (now < nextAt)
                {
                    continue;
                }

                var intervalMs = ViewModel.GetAutoRunIntervalMilliseconds(start);
                _autoStartNextRunAt[start.Id] = now.AddMilliseconds(intervalMs);
                _ = RunAutoScheduledStartAsync(start);
            }
        }

        private async Task RunAutoScheduledStartAsync(WorkflowNode startNode)
        {
            if (ViewModel == null) return;
            if (!_autoStartRunningIds.Add(startNode.Id)) return;
            try
            {
                await ViewModel.RunAutoScheduledLaneAsync(startNode);
            }
            catch
            {
                // best effort scheduler
            }
            finally
            {
                _autoStartRunningIds.Remove(startNode.Id);
            }
        }

        private void RefreshAutoStartScopeBorders()
        {
            if (ViewModel == null || WorkflowCanvas == null) return;

            var autoStarts = ViewModel.GetAutoScheduledStartNodes();
            var activeIds = autoStarts.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var obsolete in _autoStartScopeBorders.Keys.Where(k => !activeIds.Contains(k)).ToList())
            {
                if (_autoStartScopeBorders.TryGetValue(obsolete, out var oldBorder) &&
                    WorkflowCanvas.Children.Contains(oldBorder))
                {
                    WorkflowCanvas.Children.Remove(oldBorder);
                }
                _autoStartScopeBorders.Remove(obsolete);
                _autoStartScopeNodeIds.Remove(obsolete);
            }

            foreach (var start in autoStarts)
            {
                var scopeNodes = CollectAutoScopeNodes(start);
                _autoStartScopeNodeIds[start.Id] = scopeNodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var manualFrame = start.AutoScopeFrameWidth >= 120 && start.AutoScopeFrameHeight >= 120;
                if (!scopeNodes.Any() && !manualFrame)
                {
                    if (_autoStartScopeBorders.TryGetValue(start.Id, out var removeBorder) &&
                        WorkflowCanvas.Children.Contains(removeBorder))
                    {
                        WorkflowCanvas.Children.Remove(removeBorder);
                    }
                    _autoStartScopeBorders.Remove(start.Id);
                    _autoStartScopeNodeIds.Remove(start.Id);
                    continue;
                }

                var (x, y, w, h) = GetAutoScopeBorderLayout(start, scopeNodes);

                if (!_autoStartScopeBorders.TryGetValue(start.Id, out var border))
                {
                    border = CreateAutoScopeBorder(start.Id);
                    _autoStartScopeBorders[start.Id] = border;
                    WorkflowCanvas.Children.Add(border);
                }

                border.Width = w;
                border.Height = h;
                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                Panel.SetZIndex(border, 10);
            }
        }

        private static (double x, double y, double w, double h) GetAutoScopeBorderLayout(WorkflowNode start, List<WorkflowNode> scopeNodes)
        {
            const double minSide = 120d;
            var pad = Math.Max(8d, start.AutoScopeVisualPadding);

            if (start.AutoScopeFrameWidth >= minSide && start.AutoScopeFrameHeight >= minSide)
                return (start.AutoScopeFrameX, start.AutoScopeFrameY, start.AutoScopeFrameWidth, start.AutoScopeFrameHeight);

            if (scopeNodes == null || scopeNodes.Count == 0)
                return (0, 0, minSide, minSide);

            var bounds = ComputeNodesBounds(scopeNodes);
            var x = bounds.X - pad;
            var y = bounds.Y - pad;
            var w = Math.Max(minSide, bounds.Width + pad * 2);
            var h = Math.Max(minSide, bounds.Height + pad * 2);
            return (x, y, w, h);
        }

        private Border CreateAutoScopeBorder(string startNodeId)
        {
            var grid = new Grid();
            BuildAutoScopeBorderInnerGrid(grid);

            var border = new Border
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(10),
                Tag = startNodeId,
                Child = grid
            };
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);
            border.Opacity = 0.95;

            border.MouseLeftButtonDown += AutoScopeBorder_MouseLeftButtonDown;
            border.MouseMove += AutoScopeBorder_MouseMove;
            border.MouseLeftButtonUp += AutoScopeBorder_MouseLeftButtonUp;

            WireAutoScopeBorderInteractions(border, startNodeId);
            return border;
        }

        private void AutoScopeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || ViewModel == null) return;
            if (border.Tag is not string startId || string.IsNullOrWhiteSpace(startId)) return;
            if (e.OriginalSource is System.Windows.Shapes.Ellipse) return;

            _autoScopeFrameCanvasAtDragStart = new Point(Canvas.GetLeft(border), Canvas.GetTop(border));

            _isDraggingAutoScope = true;
            _draggingAutoScopeStartId = startId;
            _autoScopeDragStart = e.GetPosition(WorkflowCanvas);
            _autoScopeNodeStartPositions.Clear();

            if (_autoStartScopeNodeIds.TryGetValue(startId, out var nodeIds))
            {
                foreach (var node in EnumerateWorkflowNodesInAutoScopeForDrag(ViewModel, nodeIds))
                    _autoScopeNodeStartPositions[node.Id] = new Point(node.X, node.Y);
            }

            border.CaptureMouse();
            e.Handled = true;
        }

        private void AutoScopeBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (_autoScopeIsResizing) return;
            if (!_isDraggingAutoScope || ViewModel == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (string.IsNullOrEmpty(_draggingAutoScopeStartId)) return;
            if (!_autoStartScopeNodeIds.TryGetValue(_draggingAutoScopeStartId, out var scopeIds)) return;

            var pos = e.GetPosition(WorkflowCanvas);
            var dx = pos.X - _autoScopeDragStart.X;
            var dy = pos.Y - _autoScopeDragStart.Y;

            foreach (var node in EnumerateWorkflowNodesInAutoScopeForDrag(ViewModel, scopeIds))
            {
                if (!_autoScopeNodeStartPositions.TryGetValue(node.Id, out var startPos)) continue;
                var nx = startPos.X + dx;
                var ny = startPos.Y + dy;
                ViewModel.UpdateNodePosition(node, nx, ny);
                NodeRendererService.UpdateNodePosition(node, nx, ny);
            }

            var startNode = ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, _draggingAutoScopeStartId, StringComparison.OrdinalIgnoreCase) && n.Type == NodeType.Start);
            if (startNode != null && startNode.AutoScopeFrameWidth >= 120 && startNode.AutoScopeFrameHeight >= 120)
            {
                startNode.AutoScopeFrameX = _autoScopeFrameCanvasAtDragStart.X + dx;
                startNode.AutoScopeFrameY = _autoScopeFrameCanvasAtDragStart.Y + dy;
            }

            if (ViewModel.Connections != null)
            {
                foreach (var conn in ViewModel.Connections)
                {
                    UpdateConnectionPath(conn);
                }
            }

            RefreshAutoStartScopeBorders();
            e.Handled = true;
        }

        private void AutoScopeBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.IsMouseCaptured)
            {
                border.ReleaseMouseCapture();
            }
            _isDraggingAutoScope = false;
            _draggingAutoScopeStartId = null;
            _autoScopeNodeStartPositions.Clear();
            e.Handled = true;
        }

        private List<WorkflowNode> CollectAutoScopeNodes(WorkflowNode startNode)
        {
            var result = new List<WorkflowNode>();
            if (ViewModel == null) return result;

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var q = new Queue<WorkflowNode>();
            visited.Add(startNode.Id);
            q.Enqueue(startNode);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur is not LoopBodyNode)
                {
                    result.Add(cur);
                }

                var next = ViewModel.Connections
                    .Where(c => c.FromNode == cur && c.ToNode != null)
                    .Select(c => c.ToNode);

                foreach (var n in next)
                {
                    if (n == null) continue;
                    if (n.Type == NodeType.Start && !ReferenceEquals(n, startNode)) continue;
                    if (!visited.Add(n.Id)) continue;
                    q.Enqueue(n);
                }
            }

            AppendLoopBodyNodesForAutoScope(result);
            return result;
        }

        /// <summary>
        /// BFS bỏ qua khi add LoopBodyNode vào list (chỉ dùng làm cầu nối).
        /// Khi kéo khung scope auto phải di chuyển cả container body cùng Loop diamond, không tách liên kết.
        /// </summary>
        private static void AppendLoopBodyNodesForAutoScope(List<WorkflowNode> scopeNodes)
        {
            var ids = new HashSet<string>(scopeNodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var n in scopeNodes.ToList())
            {
                if (n is not LoopNode ln || ln.LoopBodyNode == null) continue;
                if (ids.Add(ln.LoopBodyNode.Id))
                    scopeNodes.Add(ln.LoopBodyNode);
            }
        }

        /// <summary>
        /// LoopBodyNode thường không có trong <see cref="WorkflowEditorViewModel.Nodes"/>; vẫn phải kéo theo khi kéo khung scope auto.
        /// </summary>
        private static IEnumerable<WorkflowNode> EnumerateWorkflowNodesInAutoScopeForDrag(
            WorkflowEditorViewModel vm,
            HashSet<string> nodeIds)
        {
            if (vm == null) yield break;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in vm.Nodes)
            {
                if (!nodeIds.Contains(n.Id)) continue;
                if (seen.Add(n.Id))
                    yield return n;
            }

            foreach (var ln in vm.Nodes.OfType<LoopNode>())
            {
                if (ln.LoopBodyNode == null || !nodeIds.Contains(ln.LoopBodyNode.Id)) continue;
                if (seen.Add(ln.LoopBodyNode.Id))
                    yield return ln.LoopBodyNode;
            }
        }

        private void ClampNodeDragToAutoScheduledScope(WorkflowNode? draggedNode, ref double newX, ref double newY)
        {
            if (draggedNode == null || ViewModel == null || WorkflowCanvas == null) return;
            if (draggedNode.Type == NodeType.Start || draggedNode.Type == NodeType.End) return;

            const double innerPad = 4d;
            double? accMinX = null, accMinY = null, accMaxX = null, accMaxY = null;

            foreach (var start in ViewModel.GetAutoScheduledStartNodes())
            {
                if (!_autoStartScopeNodeIds.TryGetValue(start.Id, out var ids)) continue;
                if (!ids.Contains(draggedNode.Id)) continue;
                if (!_autoStartScopeBorders.TryGetValue(start.Id, out var border)) continue;

                GetNodeOuterSizeForAutoScopeClamp(draggedNode, out var nw, out var nh);

                var bl = Canvas.GetLeft(border) + innerPad;
                var bt = Canvas.GetTop(border) + innerPad;
                var br = bl + border.Width - 2 * innerPad;
                var bb = bt + border.Height - 2 * innerPad;

                var maxX = br - nw;
                var maxY = bb - nh;
                if (maxX < bl) maxX = bl;
                if (maxY < bt) maxY = bt;

                accMinX = accMinX.HasValue ? Math.Max(accMinX.Value, bl) : bl;
                accMinY = accMinY.HasValue ? Math.Max(accMinY.Value, bt) : bt;
                accMaxX = accMaxX.HasValue ? Math.Min(accMaxX.Value, maxX) : maxX;
                accMaxY = accMaxY.HasValue ? Math.Min(accMaxY.Value, maxY) : maxY;
            }

            if (!accMinX.HasValue) return;

            newX = Math.Max(accMinX.Value, Math.Min(accMaxX!.Value, newX));
            newY = Math.Max(accMinY!.Value, Math.Min(accMaxY!.Value, newY));
        }

        private static void GetNodeOuterSizeForAutoScopeClamp(WorkflowNode node, out double w, out double h)
        {
            if (node is LoopNode)
            {
                w = 100;
                h = 100;
                return;
            }

            if (node is LoopBodyNode body)
            {
                w = body.Width;
                h = body.Height;
                return;
            }

            if (node.Border != null)
            {
                w = node.Border.ActualWidth > 0 ? node.Border.ActualWidth : (node.Border.Width > 0 ? node.Border.Width : 150d);
                h = node.Border.ActualHeight > 0 ? node.Border.ActualHeight : (node.Border.Height > 0 ? node.Border.Height : 90d);
                return;
            }

            w = 150;
            h = 90;
        }

        private static Rect ComputeNodesBounds(IEnumerable<WorkflowNode> nodes)
        {
            var list = nodes.ToList();
            if (list.Count == 0) return Rect.Empty;

            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;

            foreach (var n in list)
            {
                double w, h;
                if (n is LoopBodyNode lb)
                {
                    w = lb.Width;
                    h = lb.Height;
                }
                else if (n is LoopNode)
                {
                    w = 100d;
                    h = 100d;
                }
                else
                {
                    w = n.Border?.ActualWidth > 0 ? n.Border.ActualWidth : (n.Border?.Width > 0 ? n.Border.Width : 150d);
                    h = n.Border?.ActualHeight > 0 ? n.Border.ActualHeight : (n.Border?.Height > 0 ? n.Border.Height : 90d);
                }

                minX = Math.Min(minX, n.X);
                minY = Math.Min(minY, n.Y);
                maxX = Math.Max(maxX, n.X + w);
                maxY = Math.Max(maxY, n.Y + h);
            }

            if (double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(maxX) || double.IsInfinity(maxY))
                return Rect.Empty;

            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }

        #endregion

        /// <summary>
        /// GPU-friendly render options for canvases với hardware acceleration tối ưu
        /// Tránh ghost effects bằng cách không dùng BitmapCache cho elements di chuyển
        /// Đảm bảo coordinate mapping chính xác cho ports và connections
        /// Sử dụng user settings để điều chỉnh chất lượng render
        /// </summary>
        private void OptimizeCanvasForGPU()
        {
            if (WorkflowCanvas == null || GridCanvas == null) return;

            // Luôn áp dụng layout rounding và snaps to device pixels (tốt cho cả CPU và GPU)
            void applyBasic(Canvas c)
            {
                c.UseLayoutRounding = true;
                c.SnapsToDevicePixels = true;
            }

            applyBasic(WorkflowCanvas);
            applyBasic(GridCanvas);

            // Chỉ áp dụng GPU optimization khi có GPU và được bật trong settings
            if (_gpuEnabled && GpuDetectionHelper.IsGpuAvailable)
            {
                // Lấy BitmapScalingMode và EdgeMode từ quality setting
                var scalingMode = GpuRenderQualityHelper.GetBitmapScalingMode(_gpuRenderQuality);
                var edgeMode = GpuRenderQualityHelper.GetEdgeMode(_gpuRenderQuality);
                var useLayoutRounding = GpuRenderQualityHelper.ShouldUseLayoutRounding(_gpuRenderQuality);
                var snapToDevicePixels = GpuRenderQualityHelper.ShouldSnapToDevicePixels(_gpuRenderQuality);
                
                void applyGpuOptimization(Canvas c)
                {
                    // Áp dụng layout rounding và snaps to device pixels dựa trên quality
                    c.UseLayoutRounding = useLayoutRounding;
                    c.SnapsToDevicePixels = snapToDevicePixels;
                    
                    // Enable hardware acceleration với quality đã chọn
                    RenderOptions.SetBitmapScalingMode(c, scalingMode);
                    // Dùng EdgeMode dựa trên quality - Unspecified = anti-aliasing (mịn màng)
                    RenderOptions.SetEdgeMode(c, edgeMode);
                    // Không cache canvas để tránh ghost effects khi di chuyển nodes
                    RenderOptions.SetCachingHint(c, CachingHint.Unspecified);
                    c.CacheMode = null;
                    
                    // Force hardware rendering
                    c.ClipToBounds = false; // Đã có trong XAML, đảm bảo không clip
                    
                    // Invalidate để đảm bảo render mới
                    c.InvalidateVisual();
                    c.InvalidateArrange();
                    c.InvalidateMeasure();
                }

                applyGpuOptimization(WorkflowCanvas);
                applyGpuOptimization(GridCanvas);

                if (ScrollViewer != null)
                {
                    // Tối ưu ScrollViewer cho GPU với quality đã chọn
                    RenderOptions.SetBitmapScalingMode(ScrollViewer, scalingMode);
                    RenderOptions.SetCachingHint(ScrollViewer, CachingHint.Unspecified);
                    ScrollViewer.CacheMode = null;
                    
                    // Enable hardware scrolling
                    ScrollViewer.UseLayoutRounding = true;
                    ScrollViewer.SnapsToDevicePixels = true;
                }
            }
            else
            {
                // Nếu GPU bị tắt hoặc không có GPU, vẫn áp dụng preset chất lượng (Low/Medium/High/Best)
                // nhưng không bật cache GPU, để CPU cũng hưởng lợi từ cấu hình giống "game settings".
                var quality = _gpuRenderQuality;
                var scalingMode = GpuRenderQualityHelper.GetBitmapScalingMode(quality);
                var edgeMode = GpuRenderQualityHelper.GetEdgeMode(quality);
                var useLayoutRounding = GpuRenderQualityHelper.ShouldUseLayoutRounding(quality);
                var snapToDevicePixels = GpuRenderQualityHelper.ShouldSnapToDevicePixels(quality);

                void applyCpuQuality(Canvas c)
                {
                    c.UseLayoutRounding = useLayoutRounding;
                    c.SnapsToDevicePixels = snapToDevicePixels;
                    RenderOptions.SetBitmapScalingMode(c, scalingMode);
                    RenderOptions.SetEdgeMode(c, edgeMode);
                    RenderOptions.SetCachingHint(c, CachingHint.Unspecified);
                    c.CacheMode = null;
                }
                
                applyCpuQuality(WorkflowCanvas);
                applyCpuQuality(GridCanvas);
                
                if (ScrollViewer != null)
                {
                    RenderOptions.SetBitmapScalingMode(ScrollViewer, scalingMode);
                    RenderOptions.SetCachingHint(ScrollViewer, CachingHint.Unspecified);
                    ScrollViewer.CacheMode = null;
                    ScrollViewer.UseLayoutRounding = true;
                    ScrollViewer.SnapsToDevicePixels = true;
                }
            }
        }

        // Note: Code đã được di chuyển vào các file partial class trong thư mục WorkflowEditor/
        // - NodeRenderer.cs: Node Rendering
        // - ConnectionRenderer.cs: Connection Rendering (với cải tiến Bezier, arrow head, orthogonal với rounded corners)
        // - DragDropHandler.cs: Drag & Drop Handlers
        // - ConnectionHandler.cs: Connection Handlers
        // - ZoomPanHandler.cs: Zoom & Pan
        // - MinimapManager.cs: Minimap
        // - GridManager.cs: Grid Manager
        // - TemplateNodeHandler.cs: Template Node Handler
    }
}
