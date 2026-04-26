using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow;
using FlowMy.ViewModels;
using FlowMy.Views.NodeControls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FlowMy.Views.Overlays;

/// <summary>
/// Floating widget window — hiển thị node bên ngoài màn hình dưới dạng overlay.
/// Hai trạng thái: Collapsed (idle shape) và Expanded (WebView2 content).
/// Hỗ trợ drag, snap-to-edge, auto-collapse, always-on-top, resize.
/// </summary>
public partial class FloatingWidgetWindow : Window
{
    // ── Dependencies ──
    private readonly WorkflowNode _node;
    private readonly IWorkflowEditorHost _host;
    private readonly FloatingWidgetWindowViewModel _viewModel;
    private FloatingWidgetConfig Config => _node.FloatingWidget!;

    // ── State ──
    private bool _isExpanded;
    private bool _isSlideHidden;    // Widget đã trượt vào cạnh (ẩn 1 phần)
    private bool _isDragging;
    private bool _isWidgetMaximized;
    private Rect _restoreExpandedBounds = Rect.Empty;
    /// <summary>True khi chuột trái đã xuống nhưng chưa xác định click hay drag.</summary>
    private bool _pendingInteraction;
    private Point _dragStartPoint;
    private double _dragStartLeft;
    private double _dragStartTop;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private UIElement? _dragSource;

    /// <summary>
    /// Khoảng cách tối thiểu (px) phải kéo chuột trước khi chuyển click → drag.
    /// Dưới threshold này: mouse up = click (mở/thu widget).
    /// </summary>
    private const double DragThreshold = 6.0;

    // ── Timers ──
    private DispatcherTimer? _idleTimer;
    private DispatcherTimer? _titleBarHideTimer;

    // ── WebView2 ──
    private WebView2? _webView;
    private bool _webViewInitialized;
    private bool _webViewContentLoaded;
    private VideoProcessingNodeContentControl? _videoNodeContent;
    private bool _htmlRuntimeReady;
    private string? _lastContentSignature;
    private readonly List<(string SessionId, string Key, string Value)> _pendingAsyncBuffer = new();
    private readonly List<(string SessionId, string Key, string Value)> _hiddenAsyncBacklog = new();
    private const int AsyncFlushBatchSize = 48;
    private const int AsyncFlushMaxPerCycle = 384;
    private bool _isDeferredPendingFlushQueued;
    private readonly Dictionary<string, string> _localHostByFolder = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _localFolderByHost = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _localHostMapSync = new();

    /// <summary>Mini toolbar ngoài widget (đóng + thu nhỏ) thay cho Popup.</summary>
    private Window? _titleRevealHost;
    private StackPanel? _titleRevealActionsPanel;
    private Button? _titleRevealCloseButton;
    private Button? _titleRevealCollapseButton;
    private Button? _titleRevealStartButton;
    private Button? _titleRevealStopButton;
    private Border? _titleRevealStartBadge;
    private TextBlock? _titleRevealStartBadgeText;
    private Border? _titleRevealStopBadge;
    private TextBlock? _titleRevealStopBadgeText;
    private Popup? _titleRevealStopSessionsPopup;
    private StackPanel? _titleRevealStopSessionsPanel;
    private bool _isTitleRevealStopPopupHovering;
    private Button? _titleRevealOutsideCollapseToggleButton;
    private Button? _titleRevealPinToggleButton;
    private bool _suppressOutsideCollapseOnce;
    private INotifyCollectionChanged? _manualRunSessionsNotify;
    private INotifyPropertyChanged? _workflowVmNotify;
    private bool _isStopPopupHovering;
    private DateTime _lastTaskbarToggleUtc = DateTime.MinValue;
    private bool _taskbarToggleQueued;

    // ── Slide animation state ──
    private double _slideOriginalLeft;
    private double _slideOriginalTop;

    public FloatingWidgetWindow(WorkflowNode node, IWorkflowEditorHost host)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _viewModel = new FloatingWidgetWindowViewModel(_node, _host);

        InitializeComponent();
        DataContext = _viewModel;

        // Apply config to window
        Topmost = Config.AlwaysOnTop;
        ShowInTaskbar = Config.ShowInTaskbar;

        // Set title (ưu tiên WidgetName, fallback node.Title)
        TitleText.Text = ResolveDisplayTitle(node);

        // Window position
        WindowStartupLocation = WindowStartupLocation.Manual;
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        Activated += (_, _) =>
        {
            UpdateOutsideCollapseToggleButtonState();
            ReassertTopmostIfNeeded();
        };
        LocationChanged += (_, _) => UpdateTitleRevealButtonPlacement();
        SizeChanged += (_, _) => UpdateTitleRevealButtonPlacement();
        Deactivated += FloatingWidgetWindow_Deactivated;

        // Listen for node title changes
        if (_node is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += Node_PropertyChanged;
        }
    }

    // ═══════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTaskbarVisualIdentity();
        UpdateTitleMaxRestoreVisualState();

        // Apply idle shape
        ApplyIdleShape();

        // Set initial position
        SetInitialPosition();

        // Start in collapsed state (idle shape)
        ShowCollapsedState();

        // Start idle timer
        StartIdleTimer();

        // If HtmlUiNode/WebNode, pre-initialize WebView2 in background so initial window show is not blocked.
        if (_node is HtmlUiNode || _node is WebNode)
        {
            _ = InitWebView2Async();
        }
        else if (_node is VideoProcessingNode)
        {
            EnsureNativeNodeContent();
        }

        AttachManualRunObservers();
        UpdateRunButtonsState();
    }

    private void SetInitialPosition()
    {
        var workArea = GetTargetWorkArea();

        if (Config.SavedX.HasValue && Config.SavedY.HasValue)
        {
            Left = Config.SavedX.Value;
            Top = Config.SavedY.Value;
        }
        else
        {
            // Default: right side, vertically centered
            Left = workArea.Right - Config.IdleSize - Config.SnapMargin;
            Top = workArea.Top + (workArea.Height - Config.IdleSize) / 2.0;
        }

        ClampToWorkArea();
    }

    private Rect GetTargetWorkArea()
    {
        // Multi-monitor support using SystemParameters
        // TODO: When MonitorIndex >= 0 or ShowOnAllMonitors, use per-monitor bounds
        return SystemParameters.WorkArea;
    }

    // ═══════════════════════════════════════════
    //  IDLE SHAPE
    // ═══════════════════════════════════════════

    private void ApplyIdleShape()
    {
        var size = Config.IdleSize;
        var iconValue = Config.IdleIconText;

        HideAllIdleShapes();

        // Xác định nội dung icon: ưu tiên SVG theo icon key, fallback text/emoji.
        bool isSvgKey = !string.IsNullOrWhiteSpace(iconValue)
                        && FlowMy.IconResources.IconExists(iconValue);
        string? svgPath = isSvgKey ? FlowMy.IconResources.GetIconPath(iconValue) : null;

        // Circle
        ApplySlotIcon(IdleCircleSvg, IdleIcon, iconValue, svgPath, size * 0.42, size * 0.5);
        // Diamond (nhỏ hơn do đã xoay)
        ApplySlotIcon(IdleDiamondSvg, IdleDiamondIcon, iconValue, svgPath, size * 0.33, size * 0.42);
        // Square
        ApplySlotIcon(IdleSquareSvg, IdleSquareIcon, iconValue, svgPath, size * 0.42, size * 0.5);
        // RoundedSquare
        ApplySlotIcon(IdleRoundedSvg, IdleRoundedIcon, iconValue, svgPath, size * 0.42, size * 0.5);
        // Edge-dock square
        var sq = Config.EdgeDockSquareSize;
        ApplySlotIcon(EdgeDockSvg, EdgeDockIcon, iconValue, svgPath, sq * 0.55, sq * 0.6);

        switch (Config.IdleShape)
        {
            case WidgetIdleShape.Circle:
                IdleCircle.Width = size;
                IdleCircle.Height = size;
                IdleCircle.CornerRadius = new CornerRadius(size / 2);
                IdleCircle.Visibility = Visibility.Visible;
                break;

            case WidgetIdleShape.Diamond:
                var dSize = size * 0.85;
                IdleDiamond.Width = dSize;
                IdleDiamond.Height = dSize;
                IdleDiamond.Visibility = Visibility.Visible;
                break;

            case WidgetIdleShape.Square:
                IdleSquare.Width = size;
                IdleSquare.Height = size;
                IdleSquare.Visibility = Visibility.Visible;
                break;

            case WidgetIdleShape.RoundedSquare:
                IdleRoundedSquare.Width = size;
                IdleRoundedSquare.Height = size;
                IdleRoundedSquare.CornerRadius = new CornerRadius(size * 0.2);
                IdleRoundedSquare.Visibility = Visibility.Visible;
                break;
        }

        // Apply opacity
        IdleContainer.Opacity = Config.IdleOpacity;
        ConfigureRippleLayersForCurrentShape();
        ApplyIdleChrome();
    }

    private void HideAllIdleShapes()
    {
        IdleCircle.Visibility = Visibility.Collapsed;
        IdleDiamond.Visibility = Visibility.Collapsed;
        IdleSquare.Visibility = Visibility.Collapsed;
        IdleRoundedSquare.Visibility = Visibility.Collapsed;
        EdgeDockSquare.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Màu nền / icon idle từ hex hoặc theme; glow và ripple đồng bộ với nền.
    /// </summary>
    private void ApplyIdleChrome()
    {
        var defBg = Application.Current.TryFindResource("PrimaryBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED));
        var defFg = Application.Current.TryFindResource("TextOnPrimaryBrush") as Brush ?? Brushes.White;

        var bg = ParseHexBrush(Config.IdleBackgroundColor) ?? defBg;
        var fg = ParseHexBrush(Config.IdleForegroundColor) ?? defFg;

        var glowColor = BrushToGlowColor(bg);

        void Paint(Border b, double blur, double glowOpacity)
        {
            b.Background = bg;
            b.Effect = new DropShadowEffect
            {
                Color = glowColor,
                BlurRadius = blur,
                ShadowDepth = 0,
                Opacity = glowOpacity
            };
        }

        Paint(IdleCircle, 10, 0.22);
        Paint(IdleDiamond, 9, 0.2);
        Paint(IdleSquare, 10, 0.2);
        Paint(IdleRoundedSquare, 10, 0.2);
        Paint(EdgeDockSquare, 10, 0.2);

        void PaintIcon(Controls.SvgViewboxEx svg, TextBlock tb)
        {
            svg.Fill = fg;
            tb.Foreground = fg;
        }

        PaintIcon(IdleCircleSvg, IdleIcon);
        PaintIcon(IdleDiamondSvg, IdleDiamondIcon);
        PaintIcon(IdleSquareSvg, IdleSquareIcon);
        PaintIcon(IdleRoundedSvg, IdleRoundedIcon);
        PaintIcon(EdgeDockSvg, EdgeDockIcon);

        var ring = new SolidColorBrush(glowColor);
        RippleLayer1.BorderBrush = ring;
        RippleLayer2.BorderBrush = ring;
    }

    private static SolidColorBrush? ParseHexBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex.Trim());
            return new SolidColorBrush(color);
        }
        catch
        {
            return null;
        }
    }

    private static Color BrushToGlowColor(Brush brush)
    {
        if (brush is SolidColorBrush scb)
            return scb.Color;
        return Color.FromRgb(0x7C, 0x3A, 0xED);
    }

    /// <summary>
    /// Chuyển idle shape sang dạng ô vuông nhỏ (EdgeDockSquare) khi bám cạnh.
    /// </summary>
    private void ApplyEdgeDockSquareShape()
    {
        HideAllIdleShapes();
        var sq = Config.EdgeDockSquareSize;
        EdgeDockSquare.Width = sq;
        EdgeDockSquare.Height = sq;
        EdgeDockSquare.Visibility = Visibility.Visible;
        IdleContainer.Opacity = Math.Max(0.85, Config.IdleOpacity);
        ConfigureRippleLayersForCurrentShape();
        ApplyIdleChrome();
        ApplyIdleAnimation();
    }

    /// <summary>
    /// Cập nhật 1 cặp (SvgViewboxEx + TextBlock) cho một shape:
    /// Nếu svgPath có giá trị thì hiển thị SVG, ngược lại hiển thị text/emoji.
    /// </summary>
    private static void ApplySlotIcon(
        FlowMy.Controls.SvgViewboxEx svg,
        TextBlock text,
        string? rawIconValue,
        string? svgPath,
        double textFontSize,
        double svgSize)
    {
        if (svg == null || text == null) return;

        if (!string.IsNullOrEmpty(svgPath))
        {
            svg.Source = new System.Uri(svgPath, System.UriKind.RelativeOrAbsolute);
            svg.Width = svgSize;
            svg.Height = svgSize;
            svg.Visibility = Visibility.Visible;
            text.Visibility = Visibility.Collapsed;
        }
        else
        {
            svg.Source = null!;
            svg.Visibility = Visibility.Collapsed;
            text.Text = string.IsNullOrWhiteSpace(rawIconValue) ? "⚡" : rawIconValue;
            text.FontSize = textFontSize;
            text.Visibility = Visibility.Visible;
        }
    }

    // ═══════════════════════════════════════════
    //  STATE TRANSITIONS
    // ═══════════════════════════════════════════

    private void ShowCollapsedState()
    {
        _isExpanded = false;
        SetTitleRevealOpen(false);

        // Size — nếu đang dock ở dạng ô vuông nhỏ thì dùng EdgeDockSquareSize,
        // ngược lại dùng IdleSize (có padding cho diamond rotation).
        if (_isSlideHidden && Config.EdgeDockAsSquare)
        {
            var (ew, eh, _, _) = GetEdgeDockSquareWindowMetrics();
            Width = ew;
            Height = eh;
            ApplyEdgeDockSquareShape();
        }
        else
        {
            var dims = GetCollapsedWindowMetrics();
            Width = dims.WindowWidth;
            Height = dims.WindowHeight;
            ApplyIdleShape();
        }

        IdleContainer.Visibility = Visibility.Visible;
        ExpandedContainer.Visibility = Visibility.Collapsed;

        // Hide WebView2 to save resources
        if (_webView != null)
            _webView.Visibility = Visibility.Collapsed;
        if (_videoNodeContent != null)
            _videoNodeContent.Visibility = Visibility.Collapsed;

        ApplyIdleAnimation();
    }

    public void ExpandWidget()
    {
        if (_isExpanded) return;

        // Ghi nhớ edge dock trước khi xóa state slide (RestoreFromSlide sẽ reset _dockedEdge).
        var dockEdge = _isSlideHidden ? _dockedEdge : WidgetSnapEdge.None;
        var wasDockedAsSquare = _isSlideHidden && Config.EdgeDockAsSquare;

        // Nếu đang bám cạnh:
        //   - EdgeDockAsSquare=true → KHÔNG restore vị trí cũ, expand ngay từ vị trí dock hiện tại
        //     (vị trí sẽ được tính lại để neo vào cạnh, không khuất).
        //   - EdgeDockAsSquare=false → restore vị trí trước khi dock rồi expand như thường.
        if (_isSlideHidden)
        {
            if (Config.EdgeDockAsSquare)
            {
                // Chỉ xóa state flag, GIỮ lại Left/Top hiện tại để expand neo vào cạnh.
                _isSlideHidden = false;
            }
            else
            {
                RestoreFromSlide(animate: false);
            }
        }

        _isExpanded = true;
        MarkActivity();

        // Size (px hoặc tỉ lệ theo work area)
        var (w, h) = ResolveExpandedSize();
        Width = w;
        Height = h;

        IdleContainer.Visibility = Visibility.Collapsed;
        ExpandedContainer.Visibility = Visibility.Visible;

        // Show/hide resize grip
        ResizeGrip.Visibility = Config.AllowResize ? Visibility.Visible : Visibility.Collapsed;
        ApplyTitleBarBehavior(forceShowTitleBar: false);

        // Show WebView2
        if (_webView != null)
        {
            _webView.Visibility = Visibility.Visible;
            if (_node is HtmlUiNode htmlNode)
            {
                _ = ReloadContentAsync();
                _ = Dispatcher.InvokeAsync(async () =>
                {
                        // Chủ động drain mỗi lần expand để không phụ thuộc hoàn toàn vào event PendingAsyncDataPush
                        // (event có thể đã fire trước khi widget kịp subscribe).
                        DrainPendingAsyncQueueToBuffer(htmlNode);
                        MoveHiddenBacklogToPending();
                    var flushedCount = await FlushBufferedAsyncDataToWidgetAsync(htmlNode);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(
                        $"[FloatingWidget:{htmlNode.Id}] Expand flush flushedCount={flushedCount}, bufferCount={_pendingAsyncBuffer.Count}");
#endif
                }, DispatcherPriority.Background);
            }
            else if (!_webViewContentLoaded)
            {
                _ = ReloadContentAsync();
            }
        }
        else if (_node is VideoProcessingNode)
        {
            EnsureNativeNodeContent();
            if (_videoNodeContent != null)
                _videoNodeContent.Visibility = Visibility.Visible;
        }

        // Đặt lại vị trí theo cạnh dock (expanded body không bị khuất ra ngoài màn).
        if (wasDockedAsSquare && dockEdge != WidgetSnapEdge.None)
        {
            var area = GetTargetWorkArea();
            var margin = Math.Max(0, Config.SnapMargin);
            var (nl, nt) = ComputeDockPosition(dockEdge, area, w, h, margin);
            Left = nl;
            Top = nt;
            // Reset dockedEdge sau khi đã dùng tính toán vị trí
            _dockedEdge = WidgetSnapEdge.None;
        }

        // Clamp cuối để đảm bảo luôn trong work area
        ClampToWorkArea();

        // Fade-in animation
        AnimateExpandFadeIn();
        UpdateTitleMaxRestoreVisualState();
    }

    private void CollapseWidget()
    {
        if (!_isExpanded) return;

        // Save expanded size back to config theo đúng mode.
        // Nếu đang maximize tạm thời, giữ kích thước restore làm "kích thước gốc".
        var effectiveWidth = _isWidgetMaximized && _restoreExpandedBounds.Width > 0
            ? _restoreExpandedBounds.Width
            : Width;
        var effectiveHeight = _isWidgetMaximized && _restoreExpandedBounds.Height > 0
            ? _restoreExpandedBounds.Height
            : Height;

        _isWidgetMaximized = false;
        if (_restoreExpandedBounds.Width > 0 && _restoreExpandedBounds.Height > 0)
        {
            Left = _restoreExpandedBounds.Left;
            Top = _restoreExpandedBounds.Top;
        }
        _restoreExpandedBounds = Rect.Empty;
        UpdateTitleMaxRestoreVisualState();

        if (Config.UseRatioSize)
        {
            var area = GetTargetWorkArea();
            if (area.Width > 0) Config.WidthRatio = Math.Max(0.05, Math.Min(1.0, effectiveWidth / area.Width));
            if (area.Height > 0) Config.HeightRatio = Math.Max(0.05, Math.Min(1.0, effectiveHeight / area.Height));
        }
        else
        {
            Config.ExpandedWidth = effectiveWidth;
            Config.ExpandedHeight = effectiveHeight;
        }

        ShowCollapsedState();

        // If snap-to-edge is enabled, snap after collapse
        if (Config.SnapToEdge)
            SnapToNearestEdge();
    }

    // ═══════════════════════════════════════════
    //  DRAG & MOVE
    // ═══════════════════════════════════════════

    /// <summary>
    /// Ghi nhận mouse-down — CHƯA bật drag. Drag chỉ bật khi chuột di chuyển vượt DragThreshold.
    /// Nếu mouse-up mà chưa qua threshold → coi là click (mở/thu widget).
    /// </summary>
    private void BeginInteraction(MouseButtonEventArgs e)
    {
        _pendingInteraction = true;
        _isDragging = false;
        _dragStartPoint = PointToScreen(e.GetPosition(this));
        _dragStartLeft = Left;
        _dragStartTop = Top;
        _dragSource = e.Source as UIElement;
        _dragSource?.CaptureMouse();
    }

    private void ContinueDrag(MouseEventArgs e)
    {
        if (!_pendingInteraction) return;

        var currentPoint = PointToScreen(e.GetPosition(this));
        var dx = currentPoint.X - _dragStartPoint.X;
        var dy = currentPoint.Y - _dragStartPoint.Y;

        // Chỉ chuyển sang drag khi vượt threshold → tránh drag không mong muốn khi user chỉ click.
        if (!_isDragging)
        {
            if (Math.Abs(dx) < DragThreshold && Math.Abs(dy) < DragThreshold) return;

            if (Config.LockPosition || !Config.AllowDrag)
            {
                // Không cho phép drag → hủy pending để mouse-up xử lý như click.
                return;
            }

            _isDragging = true;
            if (_isSlideHidden)
            {
                // Bắt đầu kéo từ trạng thái dock ẩn: lộ full shape + khôi phục hình/animation gốc.
                RevealDockedWidgetFully(restoreOriginalShape: true);
            }
        }

        Left = _dragStartLeft + dx;
        Top = _dragStartTop + dy;
    }

    /// <summary>
    /// Mouse-up: nếu đã drag thật sự → kết thúc drag. Nếu chưa drag → trả về "đây là click".
    /// </summary>
    /// <returns>True nếu đây là click thật (chưa drag).</returns>
    private bool EndInteraction(MouseButtonEventArgs e)
    {
        bool wasClick = _pendingInteraction && !_isDragging;

        _pendingInteraction = false;
        _dragSource?.ReleaseMouseCapture();
        _dragSource = null;

        if (_isDragging)
        {
            _isDragging = false;
            ClampToWorkArea();
            if (Config.SnapToEdge && !_isExpanded) SnapToNearestEdge();
            SavePosition();
        }
        MarkActivity();
        return wasClick;
    }

    // Idle shape drag handlers
    private void Idle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _pendingInteraction = false;
            ExpandWidget();
            e.Handled = true;
            return;
        }
        BeginInteraction(e);
    }

    private void Idle_MouseMove(object sender, MouseEventArgs e) => ContinueDrag(e);

    private void Idle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var wasClick = EndInteraction(e);
        if (wasClick)
        {
            // Single click (không drag) → mở widget.
            ExpandWidget();
        }
    }

    private void Idle_MouseEnter(object sender, MouseEventArgs e)
    {
        MarkActivity();
        if (!_isSlideHidden) return;
        RevealDockedWidgetFully();
    }

    // Title bar drag handlers
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _pendingInteraction = false;
            CollapseWidget();
            e.Handled = true;
            return;
        }
        BeginInteraction(e);
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e) => ContinueDrag(e);

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Với title bar: chỉ drag, không toggle khi click đơn (có nút thu nhỏ; đóng qua menu chuột phải).
        EndInteraction(e);
    }

    private void TitleBar_MouseEnter(object sender, MouseEventArgs e)
    {
        MarkActivity();
        if (TitleBar.Visibility != Visibility.Visible)
        {
            ShowTitleBarTemporarily();
            return;
        }
        RestartTitleBarHideTimer();
    }

    private void TitleBarRevealButton_Click(object sender, RoutedEventArgs e)
    {
        MarkActivity();
        if (_isExpanded)
            CollapseWidget();
    }

    // ═══════════════════════════════════════════
    //  SNAP TO EDGE
    // ═══════════════════════════════════════════

    private void SnapToNearestEdge()
    {
        var workArea = GetTargetWorkArea();
        var cx = Left + Width / 2;
        var cy = Top + Height / 2;
        var margin = Config.SnapMargin;
        var (padX, padY) = GetCollapsedRipplePadding();

        // Calculate distances to each edge
        var distLeft = cx - workArea.Left;
        var distRight = workArea.Right - cx;
        var distTop = cy - workArea.Top;
        var distBottom = workArea.Bottom - cy;

        var minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        var targetLeft = Left;
        var targetTop = Top;

        if (Math.Abs(minDist - distLeft) < 0.01)
            targetLeft = workArea.Left + margin - padX;
        else if (Math.Abs(minDist - distRight) < 0.01)
            targetLeft = workArea.Right - Width - margin + padX;
        else if (Math.Abs(minDist - distTop) < 0.01)
            targetTop = workArea.Top + margin - padY;
        else
            targetTop = workArea.Bottom - Height - margin + padY;

        // Animate snap
        AnimateMoveTo(targetLeft, targetTop, 200);
    }

    // ═══════════════════════════════════════════
    //  EDGE DOCK (thay cho SlideToEdge ẩn 1 phần cũ)
    // ═══════════════════════════════════════════

    /// <summary>Cạnh mà widget đang dock vào. Dùng để clamp expand & place lại square.</summary>
    private WidgetSnapEdge _dockedEdge = WidgetSnapEdge.None;

    /// <summary>
    /// Bám widget vào cạnh màn hình gần nhất sau khi idle.
    /// - Có thể đổi sang ô vuông nhỏ (EdgeDockAsSquare=true).
    /// - Widget sẽ ẩn một phần vào cạnh theo SlideHidePercent.
    /// Hover vào chỉ lộ đầy đủ hình (không auto expand).
    /// </summary>
    private void SlideToEdge()
    {
        if (_isSlideHidden || _isExpanded) return;

        _slideOriginalLeft = Left;
        _slideOriginalTop = Top;
        _isSlideHidden = true;

        // Xác định cạnh gần nhất (4 cạnh)
        var workArea = GetTargetWorkArea();
        var cx = Left + Width / 2;
        var cy = Top + Height / 2;
        var distLeft = cx - workArea.Left;
        var distRight = workArea.Right - cx;
        var distTop = cy - workArea.Top;
        var distBottom = workArea.Bottom - cy;
        var minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        if (Math.Abs(minDist - distLeft) < 0.01) _dockedEdge = WidgetSnapEdge.Left;
        else if (Math.Abs(minDist - distRight) < 0.01) _dockedEdge = WidgetSnapEdge.Right;
        else if (Math.Abs(minDist - distTop) < 0.01) _dockedEdge = WidgetSnapEdge.Top;
        else _dockedEdge = WidgetSnapEdge.Bottom;

        // Đổi visual trước khi tính position để có kích thước mới chính xác
        if (Config.EdgeDockAsSquare)
        {
            ApplyEdgeDockSquareShape();
            var (ew, eh, _, _) = GetEdgeDockSquareWindowMetrics();
            Width = ew;
            Height = eh;
        }

        var margin = Math.Max(0, Config.SnapMargin);
        var (tl, tt) = ComputeDockPosition(_dockedEdge, workArea, Width, Height, margin);

        // Ẩn một phần widget vào cạnh
        var hide = Math.Max(0, Math.Min(0.95, Config.SlideHidePercent));
        var hideX = Width * hide;
        var hideY = Height * hide;
        switch (_dockedEdge)
        {
            case WidgetSnapEdge.Left: tl -= hideX; break;
            case WidgetSnapEdge.Right: tl += hideX; break;
            case WidgetSnapEdge.Top: tt -= hideY; break;
            case WidgetSnapEdge.Bottom: tt += hideY; break;
        }
        AnimateMoveTo(tl, tt, 300);
    }

    /// <summary>
    /// Khi hover lúc đang ẩn một phần ở cạnh, kéo widget ra vị trí full-visible ở đúng cạnh đó.
    /// Không mở expanded view.
    /// </summary>
    private void RevealDockedWidgetFully(bool restoreOriginalShape = true)
    {
        if (!_isSlideHidden) return;
        var area = GetTargetWorkArea();
        var margin = Math.Max(0, Config.SnapMargin);
        double w = Width;
        double h = Height;
        if (restoreOriginalShape && Config.EdgeDockAsSquare)
        {
            var dims = GetCollapsedWindowMetrics();
            w = dims.WindowWidth;
            h = dims.WindowHeight;
            Width = w;
            Height = h;
            ApplyIdleShape();
            ApplyIdleAnimation();
        }

        var (tl, tt) = ComputeDockPosition(_dockedEdge, area, w, h, margin);
        _isSlideHidden = false;
        AnimateMoveTo(tl, tt, 180);
    }

    /// <summary>
    /// Tính vị trí (left, top) để widget nằm TRỌN sát cạnh (không khuất ra ngoài work area).
    /// Giữ nguyên trục còn lại (Y cho cạnh trái/phải, X cho cạnh trên/dưới) trong phạm vi work area.
    /// </summary>
    private (double Left, double Top) ComputeDockPosition(
        WidgetSnapEdge edge, Rect workArea, double w, double h, double margin)
    {
        var (padX, padY) = GetCollapsedRipplePadding();
        double l = Left, t = Top;
        switch (edge)
        {
            case WidgetSnapEdge.Left:
                l = workArea.Left + margin - padX;
                t = Math.Max(workArea.Top + margin - padY, Math.Min(workArea.Bottom - h - margin + padY, Top));
                break;
            case WidgetSnapEdge.Right:
                l = workArea.Right - w - margin + padX;
                t = Math.Max(workArea.Top + margin - padY, Math.Min(workArea.Bottom - h - margin + padY, Top));
                break;
            case WidgetSnapEdge.Top:
                t = workArea.Top + margin - padY;
                l = Math.Max(workArea.Left + margin - padX, Math.Min(workArea.Right - w - margin + padX, Left));
                break;
            case WidgetSnapEdge.Bottom:
                t = workArea.Bottom - h - margin + padY;
                l = Math.Max(workArea.Left + margin - padX, Math.Min(workArea.Right - w - margin + padX, Left));
                break;
        }
        return (l, t);
    }

    /// <summary>
    /// Hủy trạng thái dock — đổi visual về idle shape gốc, trả về vị trí trước khi dock
    /// (hoặc neo sát cạnh nếu vị trí gốc đã mất hiệu lực).
    /// </summary>
    private void RestoreFromSlide(bool animate)
    {
        if (!_isSlideHidden) return;
        _isSlideHidden = false;

        // Đổi visual trở lại idle shape bình thường & set size tương ứng
        var dims = GetCollapsedWindowMetrics();
        Width = dims.WindowWidth;
        Height = dims.WindowHeight;
        ApplyIdleShape();

        _dockedEdge = WidgetSnapEdge.None;

        if (animate)
            AnimateMoveTo(_slideOriginalLeft, _slideOriginalTop, 250);
        else
        {
            Left = _slideOriginalLeft;
            Top = _slideOriginalTop;
        }
        ClampToWorkArea();
    }

    // ═══════════════════════════════════════════
    //  IDLE DETECTION & TIMERS
    // ═══════════════════════════════════════════

    private void MarkActivity()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    private void StartIdleTimer()
    {
        StopIdleTimer();
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _idleTimer.Tick += IdleTimer_Tick;
        _idleTimer.Start();
    }

    private void StopIdleTimer()
    {
        _idleTimer?.Stop();
        _idleTimer = null;
    }

    private void IdleTimer_Tick(object? sender, EventArgs e)
    {
        if (Config.PinnedNoAutoHide) return;
        var idleSeconds = (DateTime.UtcNow - _lastActivityUtc).TotalSeconds;

        // Auto-collapse when idle
        if (Config.AutoCollapseWhenIdle && _isExpanded && idleSeconds >= Config.IdleTimeoutSeconds)
        {
            CollapseWidget();
        }

        // Slide to edge when idle (in collapsed state)
        if (Config.SlideToEdgeWhenIdle && !_isExpanded && !_isSlideHidden && !_isDragging
            && idleSeconds >= Config.IdleTimeoutSeconds + 2) // +2s sau khi collapse
        {
            SlideToEdge();
        }
    }

    private void StartTitleBarHideTimer()
    {
        StopTitleBarHideTimer();
        if (!Config.ShowTitleBar || !Config.AutoHideTitleBar) return;

        _titleBarHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Config.TitleBarHideTimeoutSeconds)
        };
        _titleBarHideTimer.Tick += (s, e) =>
        {
            StopTitleBarHideTimer();
            if (_isExpanded && Config.AutoHideTitleBar)
            {
                TitleBar.Visibility = Visibility.Collapsed;
                SetTitleRevealOpen(true);
                UpdateTitleRevealButtonPlacement();
            }
        };
        _titleBarHideTimer.Start();
    }

    private void StopTitleBarHideTimer()
    {
        _titleBarHideTimer?.Stop();
        _titleBarHideTimer = null;
    }

    private void RestartTitleBarHideTimer()
    {
        if (Config.AutoHideTitleBar && Config.ShowTitleBar)
            StartTitleBarHideTimer();
    }

    private void ApplyTitleBarBehavior(bool forceShowTitleBar = false)
    {
        if (!_isExpanded)
        {
            TitleBar.Visibility = Visibility.Collapsed;
            SetTitleRevealOpen(false);
            StopTitleBarHideTimer();
            return;
        }

        if (!Config.ShowTitleBar)
        {
            TitleBar.Visibility = Visibility.Collapsed;
            SetTitleRevealOpen(true);
            StopTitleBarHideTimer();
            UpdateTitleRevealButtonPlacement();
            return;
        }

        TitleBar.Visibility = Visibility.Visible;
        SetTitleRevealOpen(false);
        UpdateTitleRevealButtonPlacement();

        if (Config.AutoHideTitleBar && !forceShowTitleBar)
            StartTitleBarHideTimer();
        else
            StopTitleBarHideTimer();
    }

    private void ShowTitleBarTemporarily()
    {
        if (!_isExpanded || !Config.ShowTitleBar) return;
        TitleBar.Visibility = Visibility.Visible;
        SetTitleRevealOpen(false);
        UpdateTitleRevealButtonPlacement();
        RestartTitleBarHideTimer();
    }

    private void EnsureTitleRevealHost()
    {
        if (_titleRevealHost != null) return;

        var closeBtn = new Button
        {
            Focusable = true,
            Width = 22,
            Height = 22,
            Content = "x",
            FontSize = 13,
            Style = (Style)TryFindResource("DangerButton"),
            BorderThickness = new Thickness(1),
            ToolTip = "Đóng widget",
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 2, 0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        closeBtn.Click += TitleRevealCloseButton_Click;

        var collapseBtn = new Button
        {
            Focusable = true,
            Width = 22,
            Height = 22,
            Content = "-",
            FontSize = 14,
            Style = (Style)TryFindResource("PrimaryButton"),
            BorderThickness = new Thickness(1),
            ToolTip = "Thu nhỏ về biểu tượng",
            Padding = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        collapseBtn.Click += TitleBarRevealButton_Click;

        var startBtn = new Button
        {
            Focusable = true,
            Width = 22,
            Height = 22,
            FontSize = 10,
            BorderThickness = new Thickness(1),
            ToolTip = "Chạy workflow",
            Padding = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        var startIcon = new TextBlock
        {
            Text = "▶",
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var startBadgeText = new TextBlock
        {
            Text = "0",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush?)TryFindResource("TextOnDangerBrush") ?? Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var startBadge = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = (Brush?)TryFindResource("DangerBrush") ?? Brushes.Red,
            BorderBrush = (Brush?)TryFindResource("TextOnDangerBrush") ?? Brushes.White,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -10, -10, 0),
            Visibility = Visibility.Collapsed,
            Child = startBadgeText
        };
        startBtn.Content = new Grid
        {
            Children =
            {
                startIcon,
                startBadge
            }
        };
        if (TryFindResource("PrimaryButton") is Style startStyle)
            startBtn.Style = startStyle;
        startBtn.Click += (_, __) => StartWorkflowFromWidget();

        var stopBtn = new Button
        {
            Focusable = true,
            Width = 22,
            Height = 22,
            FontSize = 10,
            BorderThickness = new Thickness(1),
            ToolTip = "Dừng tất cả workflow đang chạy",
            Padding = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        var stopIcon = new TextBlock
        {
            Text = "⏹️",
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var stopBadgeText = new TextBlock
        {
            Text = "0",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush?)TryFindResource("TextOnDangerBrush") ?? Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var stopBadge = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = (Brush?)TryFindResource("DangerBrush") ?? Brushes.Red,
            BorderBrush = (Brush?)TryFindResource("TextOnDangerBrush") ?? Brushes.White,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -10, -10, 0),
            Visibility = Visibility.Collapsed,
            Child = stopBadgeText
        };
        stopBtn.Content = new Grid
        {
            Children =
            {
                stopIcon,
                stopBadge
            }
        };
        if (TryFindResource("DangerButton") is Style stopStyle)
            stopBtn.Style = stopStyle;
        stopBtn.Click += (_, __) => ShowTitleRevealStopSessionMenu(stopBtn);
        stopBtn.MouseEnter += (_, __) => ShowTitleRevealStopSessionMenu(stopBtn);
        stopBtn.MouseLeave += (_, __) =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!_isTitleRevealStopPopupHovering && _titleRevealStopSessionsPopup?.IsMouseOver != true)
                {
                    if (_titleRevealStopSessionsPopup != null)
                        _titleRevealStopSessionsPopup.IsOpen = false;
                }
            }));
        };

        var outsideCollapseBtn = new Button
        {
            Focusable = true,
            Width = 22,
            Height = 22,
            Content = "◌",
            FontSize = 11,
            BorderThickness = new Thickness(1),
            ToolTip = "Bật/tắt: click ra ngoài thì tự thu nhỏ",
            Padding = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        if (TryFindResource("SecondaryButton") is Style secondaryStyle)
            outsideCollapseBtn.Style = secondaryStyle;
        outsideCollapseBtn.Click += OutsideCollapseToggleButton_Click;

        var pinBtn = new Button
        {
            Focusable = true,
            Width = 22,
            Height = 22,
            Content = "📌",
            FontSize = 11,
            BorderThickness = new Thickness(1),
            ToolTip = "Ghim: không tự ẩn theo thời gian",
            Padding = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        if (TryFindResource("SecondaryButton") is Style pinStyle)
            pinBtn.Style = pinStyle;
        pinBtn.Click += PinToggleButton_Click;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        row.Children.Add(closeBtn);
        row.Children.Add(collapseBtn);
        row.Children.Add(startBtn);
        row.Children.Add(stopBtn);
        row.Children.Add(outsideCollapseBtn);
        row.Children.Add(pinBtn);

        var panel = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            // Chừa vùng top/right để badge (đặt margin âm) không bị host window cắt mất.
            Padding = new Thickness(0, 10, 10, 0),
            Child = row,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };

        var host = new Window
        {
            Owner = this,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowActivated = false,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Focusable = false,
            MinWidth = 0,
            MinHeight = 0,
            Content = panel
        };

        _titleRevealCloseButton = closeBtn;
        _titleRevealCollapseButton = collapseBtn;
        _titleRevealStartButton = startBtn;
        _titleRevealStopButton = stopBtn;
        _titleRevealStartBadge = startBadge;
        _titleRevealStartBadgeText = startBadgeText;
        _titleRevealStopBadge = stopBadge;
        _titleRevealStopBadgeText = stopBadgeText;
        _titleRevealOutsideCollapseToggleButton = outsideCollapseBtn;
        _titleRevealPinToggleButton = pinBtn;
        _titleRevealActionsPanel = row;
        _titleRevealHost = host;
        UpdateOutsideCollapseToggleButtonState();
        UpdatePinToggleButtonState();
        UpdateRunButtonsState();
    }

    private void ApplyTitleRevealHostBounds()
    {
        if (_titleRevealHost == null || !_isExpanded) return;

        var area = GetTargetWorkArea();
        // Use window bounds directly to avoid placement drift on side-docked cases.
        var widgetWidth = ActualWidth > 0 ? ActualWidth : Width;
        var widgetHeight = ActualHeight > 0 ? ActualHeight : Height;
        var widgetLeft = Left;
        var widgetTop = Top;
        var widgetRight = widgetLeft + widgetWidth;
        var widgetBottom = widgetTop + widgetHeight;

        var distLeft = Math.Abs(widgetLeft - area.Left);
        var distRight = Math.Abs(area.Right - widgetRight);
        var distTop = Math.Abs(widgetTop - area.Top);
        var distBottom = Math.Abs(area.Bottom - widgetBottom);

        double sl, st;
        var nearestSideDist = Math.Min(distLeft, distRight);
        var nearestTopBottomDist = Math.Min(distTop, distBottom);
        var verticalDock = nearestSideDist <= nearestTopBottomDist;
        var dockLeft = distLeft <= distRight;
        var dockTop = distTop <= distBottom;
        if (_titleRevealActionsPanel != null) _titleRevealActionsPanel.Orientation = verticalDock ? Orientation.Vertical : Orientation.Horizontal; // B1: chốt orientation trước khi đo (thêm nút mới mà quên bước này sẽ giữ layout cũ và tạo khoảng hở sai).
        if (_titleRevealCloseButton != null) _titleRevealCloseButton.Margin = verticalDock ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 2, 0); // B2: margin phải đổi theo orientation; dọc dùng Bottom, ngang dùng Right.
        if (_titleRevealCollapseButton != null) _titleRevealCollapseButton.Margin = verticalDock ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 2, 0); // B2: áp cùng quy tắc spacing cho nút "-" để kích thước đo không lệch.
        if (_titleRevealStartButton != null) _titleRevealStartButton.Margin = verticalDock ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 2, 0); // B2: nút mới phải đổi margin theo orientation để không làm lệch phép đo dock trái/phải.
        if (_titleRevealStopButton != null) _titleRevealStopButton.Margin = verticalDock ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 2, 0); // B2: đồng bộ spacing để loại trừ khoảng thừa khi widget nằm sát cạnh.
        if (_titleRevealOutsideCollapseToggleButton != null) _titleRevealOutsideCollapseToggleButton.Margin = verticalDock ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 2, 0); // B2: mọi nút mới đều phải có margin theo orientation, nếu không ActualWidth/Height thay đổi bất định.
        if (_titleRevealPinToggleButton != null) _titleRevealPinToggleButton.Margin = verticalDock ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 2, 0); // B2: đồng bộ nút pin với nhóm để tránh chênh 1-2px sau khi thêm feature.
        _titleRevealActionsPanel?.UpdateLayout();

        const double sideGap = 5;      // yêu cầu UI: cách cạnh widget 5px ở case trái/phải
        const double topBottomGap = 3; // giữ margin nhỏ cho case top/bottom
        var btnW = _titleRevealActionsPanel?.ActualWidth ?? 0; // B4: ưu tiên đo từ panel thật (chứa toàn bộ nút) thay vì đo từng button/window để không bị sai do template/padding.
        var btnH = _titleRevealActionsPanel?.ActualHeight ?? 0; // B4: chiều cao thực sau layout là nguồn chuẩn cho neo trái/phải/top/bottom.
        if ((btnW <= 0 || btnH <= 0) && _titleRevealActionsPanel != null)
        {
            // First-open case: panel may not be visible yet, so use DesiredSize from explicit measure.
            _titleRevealActionsPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = _titleRevealActionsPanel.DesiredSize;
            if (btnW <= 0) btnW = desired.Width;
            if (btnH <= 0) btnH = desired.Height;
        }
        if (btnW <= 0) btnW = _titleRevealHost.ActualWidth > 0 ? _titleRevealHost.ActualWidth : _titleRevealHost.Width; // B5: fallback khi frame đầu chưa đo xong.
        if (btnH <= 0) btnH = _titleRevealHost.ActualHeight > 0 ? _titleRevealHost.ActualHeight : _titleRevealHost.Height; // B5: fallback tương ứng cho height.
        if (btnW <= 0) btnW = verticalDock ? 22 : 68; // B6: fallback cuối cùng để thuật toán không nhận 0 và nhảy vị trí.
        if (btnH <= 0) btnH = verticalDock ? 68 : 22; // B6: luôn có kích thước tối thiểu hợp lý theo orientation.
        var edgePad = 8.0;
#if DEBUG
        Debug.WriteLine($"[RevealToolbar] orientation={(verticalDock ? "Vertical" : "Horizontal")} size={btnW:0.#}x{btnH:0.#} widgetBounds=({widgetLeft:0.#},{widgetTop:0.#})-({widgetRight:0.#},{widgetBottom:0.#})");
#endif

        if (verticalDock)
        {
            st = dockTop ? widgetTop + edgePad : Math.Max(widgetTop + edgePad, widgetBottom - btnH - edgePad);
            if (dockLeft)
                // Sát cạnh trái màn hình -> đặt ngay ngoài cạnh phải widget.
                sl = widgetRight + sideGap;
            else
                // Sát cạnh phải màn hình -> đặt ngay ngoài cạnh trái widget.
                sl = widgetLeft - btnW - sideGap;
        }
        else
        {
            sl = dockLeft ? widgetLeft + edgePad : Math.Max(widgetLeft + edgePad, widgetRight - btnW - edgePad);
            if (dockTop)
                // Sát cạnh trên màn hình -> đặt nút ở ngoài cạnh dưới widget.
                st = widgetBottom + topBottomGap;
            else
                // Sát cạnh dưới màn hình -> đặt nút ở ngoài cạnh trên widget.
                st = widgetTop - btnH - topBottomGap;
        }

        // Giữ nút trong work area nhưng vẫn ưu tiên nằm ngoài widget.
        sl = Math.Max(area.Left, Math.Min(sl, area.Right - btnW));
        st = Math.Max(area.Top, Math.Min(st, area.Bottom - btnH));

        _titleRevealHost.Left = Math.Round(sl);
        _titleRevealHost.Top = Math.Round(st);
    }

    private void UpdateTitleRevealButtonPlacement()
    {
        EnsureTitleRevealHost();
        if (_titleRevealHost == null) return;
        ApplyTitleRevealHostBounds();
    }

    private void SetTitleRevealOpen(bool isOpen)
    {
        // Nút cạnh widget được điều khiển độc lập với mode title bar.
        // Nếu bật checkbox và widget đang expanded thì luôn hiện nút này.
        var mustShowByConfig = Config.ShowSideActionButton && _isExpanded;
        if (!mustShowByConfig)
        {
            _titleRevealHost?.Hide();
            return;
        }

        isOpen = true;

        EnsureTitleRevealHost();
        if (_titleRevealHost == null) return;

        _titleRevealHost.Owner = this;
        _titleRevealHost.Topmost = Topmost;
        UpdateOutsideCollapseToggleButtonState();
        UpdatePinToggleButtonState();

        ApplyTitleRevealHostBounds();
        _titleRevealHost.Show();
        _titleRevealHost.Topmost = Topmost;
        Dispatcher.BeginInvoke(new Action(ApplyTitleRevealHostBounds), DispatcherPriority.Loaded);
        Dispatcher.BeginInvoke(new Action(ApplyTitleRevealHostBounds), DispatcherPriority.Render);
    }

    private void SyncTitleRevealHostTopmost()
    {
        if (_titleRevealHost != null)
            _titleRevealHost.Topmost = Topmost;
    }

    private void UpdateOutsideCollapseToggleButtonState()
    {
        var enabled = Config.CollapseWhenClickOutsideExpanded && !Config.PinnedNoAutoHide;
        var tooltip = enabled
            ? "Đang bật: click ra ngoài sẽ tự thu nhỏ"
            : (Config.PinnedNoAutoHide
                ? "Đang tắt do chế độ ghim"
                : "Đang tắt: click ra ngoài không tự thu nhỏ");
        var styleKey = enabled ? "PrimaryButton" : "SecondaryButton";

        if (_titleRevealOutsideCollapseToggleButton != null)
        {
            _titleRevealOutsideCollapseToggleButton.Content = enabled ? "◉" : "◌";
            _titleRevealOutsideCollapseToggleButton.ToolTip = tooltip;
            _titleRevealOutsideCollapseToggleButton.IsEnabled = !Config.PinnedNoAutoHide;
            if (TryFindResource(styleKey) is Style revealStyle)
                _titleRevealOutsideCollapseToggleButton.Style = revealStyle;
        }

        if (TitleOutsideCollapseToggleBtn != null)
        {
            if (TitleOutsideCollapseToggleIcon != null)
                TitleOutsideCollapseToggleIcon.Text = enabled ? "◉" : "◌";
            TitleOutsideCollapseToggleBtn.ToolTip = tooltip;
            TitleOutsideCollapseToggleBtn.IsEnabled = !Config.PinnedNoAutoHide;
            var titleStyleKey = enabled ? "WidgetTitleActionButtonActive" : "WidgetTitleWindowButton";
            if (TryFindResource(titleStyleKey) is Style titleStyle)
                TitleOutsideCollapseToggleBtn.Style = titleStyle;
        }
    }

    private void UpdatePinToggleButtonState()
    {
        var pinned = Config.PinnedNoAutoHide;
        var tooltip = pinned
            ? "Đang ghim: không tự ẩn theo thời gian"
            : "Bật ghim: không tự ẩn theo thời gian";
        var styleKey = pinned ? "PrimaryButton" : "SecondaryButton";

        if (_titleRevealPinToggleButton != null)
        {
            _titleRevealPinToggleButton.Content = pinned ? "📍" : "📌";
            _titleRevealPinToggleButton.ToolTip = tooltip;
            if (TryFindResource(styleKey) is Style revealStyle)
                _titleRevealPinToggleButton.Style = revealStyle;
        }

        if (TitlePinToggleBtn != null)
        {
            if (TitlePinToggleIcon != null)
                TitlePinToggleIcon.Text = pinned ? "📍" : "📌";
            TitlePinToggleBtn.ToolTip = tooltip;
            var titleStyleKey = pinned ? "WidgetTitleActionButtonActive" : "WidgetTitleWindowButton";
            if (TryFindResource(titleStyleKey) is Style titleStyle)
                TitlePinToggleBtn.Style = titleStyle;
        }
    }

    private void OutsideCollapseToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressOutsideCollapseOnce();
        MarkActivity();
        Config.CollapseWhenClickOutsideExpanded = !Config.CollapseWhenClickOutsideExpanded;
        if (Config.CollapseWhenClickOutsideExpanded)
            Config.PinnedNoAutoHide = false;
        UpdateOutsideCollapseToggleButtonState();
        UpdatePinToggleButtonState();
        PersistWidgetConfigBestEffort();
    }

    private void PinToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressOutsideCollapseOnce();
        MarkActivity();
        Config.PinnedNoAutoHide = !Config.PinnedNoAutoHide;
        if (Config.PinnedNoAutoHide)
        {
            Config.AutoCollapseWhenIdle = false;
            Config.SlideToEdgeWhenIdle = false;
            Config.CollapseWhenClickOutsideExpanded = false; // yêu cầu: ép về ◌
        }
        UpdatePinToggleButtonState();
        UpdateOutsideCollapseToggleButtonState();
        PersistWidgetConfigBestEffort();
    }

    private void SuppressOutsideCollapseOnce()
    {
        _suppressOutsideCollapseOnce = true;
        Dispatcher.BeginInvoke(new Action(() => _suppressOutsideCollapseOnce = false), DispatcherPriority.Background);
    }

    private void FloatingWidgetWindow_Deactivated(object? sender, EventArgs e)
    {
        ReassertTopmostIfNeeded();

        if (!_isExpanded) return;
        if (Config.PinnedNoAutoHide) return;
        if (!Config.CollapseWhenClickOutsideExpanded) return;
        if (_suppressOutsideCollapseOnce) return;
        if (_isDragging || _pendingInteraction) return;
        if (_titleRevealHost?.IsMouseOver == true) return;

        CollapseWidget();
    }

    private void PersistWidgetConfigBestEffort()
    {
        try
        {
            _host?.ViewModel?.SaveWorkflowSilently();
        }
        catch { }
    }

    private void TitleRevealCloseButton_Click(object sender, RoutedEventArgs e)
    {
        SavePosition();
        Close();
    }

    // ═══════════════════════════════════════════
    //  RESIZE
    // ═══════════════════════════════════════════

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!Config.AllowResize) return;

        var (minW, minH, maxW, maxH) = ResolveSizeBounds();
        var newW = Math.Max(minW, Math.Min(maxW, Width + e.HorizontalChange));
        var newH = Math.Max(minH, Math.Min(maxH, Height + e.VerticalChange));

        Width = newW;
        Height = newH;

        MarkActivity();
    }

    /// <summary>
    /// Trả về kích thước expanded: nếu UseRatioSize thì tính theo work area (đã clamp min/max).
    /// Ngược lại dùng ExpandedWidth/ExpandedHeight (px).
    /// </summary>
    private (double Width, double Height) ResolveExpandedSize()
    {
        if (!Config.UseRatioSize)
        {
            return (
                Math.Max(Config.MinExpandedWidth, Math.Min(Config.MaxExpandedWidth, Config.ExpandedWidth)),
                Math.Max(Config.MinExpandedHeight, Math.Min(Config.MaxExpandedHeight, Config.ExpandedHeight))
            );
        }

        var area = GetTargetWorkArea();
        var w = Math.Round(area.Width * Config.WidthRatio);
        var h = Math.Round(area.Height * Config.HeightRatio);

        // Clamp theo min/max ratio bounds cũng tính theo work area
        var minW = area.Width * Config.MinWidthRatio;
        var minH = area.Height * Config.MinHeightRatio;
        var maxW = area.Width * Config.MaxWidthRatio;
        var maxH = area.Height * Config.MaxHeightRatio;

        return (
            Math.Max(minW, Math.Min(maxW, w)),
            Math.Max(minH, Math.Min(maxH, h))
        );
    }

    /// <summary>Trả về (minW, minH, maxW, maxH) tính theo mode hiện tại (px hoặc ratio).</summary>
    private (double MinW, double MinH, double MaxW, double MaxH) ResolveSizeBounds()
    {
        if (!Config.UseRatioSize)
        {
            return (Config.MinExpandedWidth, Config.MinExpandedHeight, Config.MaxExpandedWidth, Config.MaxExpandedHeight);
        }

        var area = GetTargetWorkArea();
        return (
            area.Width * Config.MinWidthRatio,
            area.Height * Config.MinHeightRatio,
            area.Width * Config.MaxWidthRatio,
            area.Height * Config.MaxHeightRatio
        );
    }

    // ═══════════════════════════════════════════
    //  WEBVIEW2 (cho HtmlUiNode)
    // ═══════════════════════════════════════════

    private async Task InitWebView2Async()
    {
        if (_webViewInitialized) return;

        try
        {
            _webView = new WebView2
            {
                Visibility = Visibility.Collapsed,
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 24, 24, 27)
            };

            var contentGrid = ContentArea.Child as Grid;
            if (contentGrid == null)
            {
                contentGrid = new Grid();
                ContentArea.Child = contentGrid;
            }
            contentGrid.Children.Add(_webView);

            // Get shared WebView2 environment
            var env = await WebView2EnvironmentManager.GetSharedEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(env);

            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                try
                {
                    // Inject runtime bridge ở document-start để luôn có hostLivePush/hostAsyncPush,
                    // kể cả khi HTML widget có cấu trúc phức tạp hoặc nhiều tab JS con.
                    await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildRuntimeBridgeBootstrapJs());
                }
                catch { }

                // Handle web messages from HTML (hostSubmit, hostStart)
                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                _webView.CoreWebView2.NavigationCompleted += (_, _) =>
                {
                    _ = Dispatcher.InvokeAsync(async () =>
                    {
                        _htmlRuntimeReady = true;
                        if (_node is HtmlUiNode htmlNode)
                        {
                            // Drain ngay sau navigation complete để bắt dữ liệu đến sớm trước khi runtime ready.
                            DrainPendingAsyncQueueToBuffer(htmlNode);
                            MoveHiddenBacklogToPending();
                            var flushedCount = await FlushBufferedAsyncDataToWidgetAsync(htmlNode);
#if DEBUG
                            System.Diagnostics.Debug.WriteLine(
                                $"[FloatingWidget:{htmlNode.Id}] NavigationCompleted flush flushedCount={flushedCount}, bufferCount={_pendingAsyncBuffer.Count}");
#endif
                        }
                    }, DispatcherPriority.Background);
                };

                _webViewInitialized = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FloatingWidget] WebView2 init error: {ex.Message}");
        }
    }

    private void EnsureNativeNodeContent()
    {
        if (_videoNodeContent != null || _node is not VideoProcessingNode videoNode) return;
        try
        {
            var contentGrid = ContentArea.Child as Grid;
            if (contentGrid == null)
            {
                contentGrid = new Grid();
                ContentArea.Child = contentGrid;
            }

            _videoNodeContent = new VideoProcessingNodeContentControl(videoNode)
            {
                Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed
            };
            contentGrid.Children.Add(_videoNodeContent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FloatingWidget] Native content init error: {ex.Message}");
        }
    }

    private async Task ReloadContentAsync()
    {
        if (!_webViewInitialized || _webView?.CoreWebView2 == null) return;

        try
        {
            if (_node is HtmlUiNode htmlNode)
            {
                var signature = _viewModel.BuildContentSignature(htmlNode);
                if (_webViewContentLoaded && string.Equals(_lastContentSignature, signature, StringComparison.Ordinal))
                    return;

                var html = BuildHtmlForWidget(htmlNode);
                _htmlRuntimeReady = false;

                // Check size limit for NavigateToString (2MB)
                if (Encoding.UTF8.GetByteCount(html) > 1_800_000)
                {
                    // Use temp file
                    var tmpFile = Path.Combine(Path.GetTempPath(), $"widget_{_node.Id}_{Guid.NewGuid():N}.html");
                    await File.WriteAllTextAsync(tmpFile, html, Encoding.UTF8);
                    _webView.CoreWebView2.Navigate(new Uri(tmpFile).AbsoluteUri);
                }
                else
                {
                    _webView.CoreWebView2.NavigateToString(html);
                }
                _lastContentSignature = signature;
                _webViewContentLoaded = true;
            }
            else if (_node is WebNode webNode)
            {
                var targetUrl = webNode.ExtractUrl;
                if (string.IsNullOrWhiteSpace(targetUrl))
                    targetUrl = "about:blank";

                var signature = $"web:{targetUrl}";
                if (_webViewContentLoaded && string.Equals(_lastContentSignature, signature, StringComparison.Ordinal))
                    return;

                _webView.CoreWebView2.Navigate(targetUrl);
                _lastContentSignature = signature;
                _webViewContentLoaded = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FloatingWidget] ReloadContent error: {ex.Message}");
        }
    }

    public async void RefreshContent()
    {
        if (_isExpanded && _webViewInitialized)
        {
            await ReloadContentAsync();
        }
    }

    /// <summary>
    /// Áp dụng lại toàn bộ config runtime cho widget đang mở để đồng bộ ngay sau khi user đổi trong dialog.
    /// </summary>
    public void ApplyConfigChanges()
    {
        try
        {
            if (Config.PinnedNoAutoHide)
            {
                Config.AutoCollapseWhenIdle = false;
                Config.SlideToEdgeWhenIdle = false;
                Config.CollapseWhenClickOutsideExpanded = false;
            }

            Topmost = Config.AlwaysOnTop;
            ShowInTaskbar = Config.ShowInTaskbar;
            ApplyTaskbarVisualIdentity();
            ReassertTopmostIfNeeded();
            SyncTitleRevealHostTopmost();
            EnsureTitleRevealHost();
            UpdateOutsideCollapseToggleButtonState();
            UpdatePinToggleButtonState();
            TitleText.Text = ResolveDisplayTitle(_node);

            if (_isExpanded)
            {
                var (w, h) = ResolveExpandedSize();
                Width = w;
                Height = h;
                ResizeGrip.Visibility = Config.AllowResize ? Visibility.Visible : Visibility.Collapsed;
                ApplyIdleChrome();
                ApplyTitleBarBehavior(forceShowTitleBar: false);
            }
            else
            {
                // Nếu đang collapsed thì apply lại shape/animation/opacity tức thì.
                ShowCollapsedState();
                if (Config.SnapToEdge) SnapToNearestEdge();
            }

            ClampToWorkArea();
            SavePosition();
            UpdateTitleMaxRestoreVisualState();
        }
        catch { }
    }

    private string BuildHtmlForWidget(HtmlUiNode htmlNode)
    {
        var bridgeJs = BuildBridgeJs();
        return _viewModel.BuildHtmlForWidget(htmlNode, bridgeJs);
    }

    private void DrainPendingAsyncQueueToBuffer(HtmlUiNode node)
    {
        var strictFinalSync = IsStrictFinalSyncEnabled();
        var isFlowExecuting = _host?.ViewModel?.IsExecuting == true
                              || (_host?.ViewModel?.ManualExecutionRunsInFlight ?? 0) > 0;
        var items = new List<(string SessionId, string Key, string Value)>();
        while (node.PendingAsyncPushQueue.TryDequeue(out var item))
            items.Add(item);

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[FloatingWidget:{node.Id}] Drain queue count={items.Count}, keys=[{string.Join(", ", items.Select(i => i.Key))}]");
#endif

        if (items.Count == 0) return;

        foreach (var kvp in items)
        {
            if (IsPlaceholderValue(kvp.Value)) continue;

            var sessionId = kvp.SessionId ?? "session:unknown";
            var key = kvp.Key ?? string.Empty;
            var value = kvp.Value ?? string.Empty;

            // Non-strict mode: trong lúc flow còn chạy thì giữ data lại,
            // chỉ flush sau khi flow kết thúc để result/UI đi theo nhịp flow.
            if (!strictFinalSync && isFlowExecuting)
            {
                _hiddenAsyncBacklog.Add((sessionId, key, value));
                continue;
            }

            // Ưu tiên realtime: nếu runtime đã sẵn sàng thì vẫn đẩy ngay kể cả widget đang ẩn/collapse.
            // Điều này giúp trạng thái flow trên canvas đồng bộ với runtime, tránh lag "đã chạy xong nhưng UI còn running".
            if (_webViewInitialized && _htmlRuntimeReady)
            {
                _pendingAsyncBuffer.Add((sessionId, key, value));
                continue;
            }

            // Runtime chưa sẵn sàng thì tạm giữ backlog để không mất dữ liệu.
            _hiddenAsyncBacklog.Add((sessionId, key, value));
        }
#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[FloatingWidget:{node.Id}] Buffer after drain count={_pendingAsyncBuffer.Count}, hiddenBacklog={_hiddenAsyncBacklog.Count}");
#endif
    }

    private void MoveHiddenBacklogToPending(int maxTransfer = 5000)
    {
        if (_hiddenAsyncBacklog.Count == 0) return;
        var transferCount = Math.Min(maxTransfer, _hiddenAsyncBacklog.Count);
        for (int i = 0; i < transferCount; i++)
        {
            _pendingAsyncBuffer.Add(_hiddenAsyncBacklog[i]);
        }
        _hiddenAsyncBacklog.RemoveRange(0, transferCount);
    }

    private void UpsertPendingAsyncBuffer(string sessionId, string key, string value)
    {
        for (int i = _pendingAsyncBuffer.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_pendingAsyncBuffer[i].SessionId, sessionId, StringComparison.Ordinal) &&
                string.Equals(_pendingAsyncBuffer[i].Key, key, StringComparison.Ordinal))
            {
                _pendingAsyncBuffer.RemoveAt(i);
            }
        }
        _pendingAsyncBuffer.Add((sessionId, key, value));
    }

    private async Task<int> FlushBufferedAsyncDataToWidgetAsync(HtmlUiNode node)
    {
        if (_webView?.CoreWebView2 == null) return 0;
        if (!_htmlRuntimeReady) return 0;
        if (_pendingAsyncBuffer.Count == 0) return 0;

        var takeCount = Math.Min(_pendingAsyncBuffer.Count, AsyncFlushMaxPerCycle);
        var snapshot = _pendingAsyncBuffer.Take(takeCount).ToList();
        _pendingAsyncBuffer.RemoveRange(0, takeCount);

        var flushed = 0;
        for (int i = 0; i < snapshot.Count; i += AsyncFlushBatchSize)
        {
            var batch = snapshot.Skip(i).Take(AsyncFlushBatchSize).ToList();
            if (batch.Count == 0) continue;
            if (await PushBatchToWidgetRuntimeAsync(batch, node.Id))
            {
                flushed += batch.Count;
            }
            else
            {
                // Runtime chưa thực sự sẵn sàng: trả batch lại buffer để retry sau.
                _pendingAsyncBuffer.InsertRange(0, batch);
                break;
            }
        }
#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[FloatingWidget:{node.Id}] Flush buffered flushedCount={flushed}, bufferRemaining={_pendingAsyncBuffer.Count}");
#endif
        return flushed;
    }

    private static bool IsPlaceholderValue(string? value)
    {
        var s = value?.Trim();
        return string.Equals(s, "—", StringComparison.Ordinal);
    }

    private async Task PushKeyValueToWidgetRuntimeAsync(string key, string value, string nodeIdForLog)
    {
        if (_webView?.CoreWebView2 == null) return;
        if (!_htmlRuntimeReady)
        {
            var sessionId = _pendingAsyncBuffer.Count > 0
                ? _pendingAsyncBuffer[^1].SessionId
                : "session:unknown";
            _pendingAsyncBuffer.Add((sessionId, key ?? string.Empty, value ?? string.Empty));
#if DEBUG
            System.Diagnostics.Debug.WriteLine(
                $"[FloatingWidget:{nodeIdForLog}] Runtime not ready => rebuffer key='{key}', bufferCount={_pendingAsyncBuffer.Count}");
#endif
            return;
        }

        var jsKey = JsonSerializer.Serialize(key ?? string.Empty);
        var jsVal = JsonSerializer.Serialize(value ?? string.Empty);
        var inspect = await _webView.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
  try {{
    // Ensure tối thiểu bridge runtime trước khi push data
    window.hostLive = window.hostLive || {{}};
    window.hostLive.values = window.hostLive.values || {{}};
    if (typeof window.hostLivePush !== 'function') {{
      window.hostLive._subs = window.hostLive._subs || {{}};
      window.hostLive._allSubs = window.hostLive._allSubs || [];
      window.hostLivePush = function(key, value) {{
        window.hostLive = window.hostLive || {{}};
        window.hostLive.values = window.hostLive.values || {{}};
        window.hostLive.values[key] = value;
      }};
    }}
    if (!window.hostAsyncReady) {{
      window.hostAsyncReady = true;
      var _data = {{}};
      var _keyCallbacks = {{}};
      var _allCallbacks = [];
      window.hostAsync = {{
        values: _data,
        on: function(keyOrFn, fn) {{
          if (typeof keyOrFn === 'function') {{
            _allCallbacks.push(keyOrFn);
          }} else if (typeof keyOrFn === 'string' && typeof fn === 'function') {{
            if (!_keyCallbacks[keyOrFn]) _keyCallbacks[keyOrFn] = [];
            _keyCallbacks[keyOrFn].push(fn);
          }}
        }}
      }};
      window.hostAsyncPush = function(key, value) {{
        _data[key] = value;
        var cbs = _keyCallbacks[key];
        if (cbs) {{
          for (var i = 0; i < cbs.length; i++) {{
            try {{ cbs[i](value); }} catch (_) {{}}
          }}
        }}
        for (var j = 0; j < _allCallbacks.length; j++) {{
          try {{ _allCallbacks[j](JSON.parse(JSON.stringify(_data))); }} catch (_) {{}}
        }}
      }};
    }} else if (typeof window.hostAsyncPush !== 'function') {{
      window.hostAsync = window.hostAsync || {{ values: {{}} }};
      window.hostAsyncPush = function(key, value) {{
        window.hostAsync.values = window.hostAsync.values || {{}};
        window.hostAsync.values[key] = value;
      }};
    }}

    if (typeof window.hostAsyncPush === 'function') window.hostAsyncPush({jsKey}, {jsVal});
    if (typeof window.hostLivePush === 'function') window.hostLivePush({jsKey}, {jsVal});
    var asyncVal = (window.hostAsync && window.hostAsync.values) ? window.hostAsync.values[{jsKey}] : undefined;
    var liveVal = (window.hostLive && window.hostLive.values) ? window.hostLive.values[{jsKey}] : undefined;
    return JSON.stringify({{
      hasAsyncPush: typeof window.hostAsyncPush === 'function',
      hasLivePush: typeof window.hostLivePush === 'function',
      asyncValue: asyncVal,
      liveValue: liveVal
    }});
  }} catch (e) {{
    return JSON.stringify({{ error: String(e && e.message ? e.message : e) }});
  }}
}})();");

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[FloatingWidget:{nodeIdForLog}] JS push key='{key}' value='{value}' inspect={inspect}");
#endif

        // Nếu runtime bridge vẫn chưa sẵn sàng, không drop data — trả lại buffer để thử lại tick sau.
        var hasAsyncPush = false;
        var hasLivePush = false;
        try
        {
            // ExecuteScriptAsync trả về JSON-encoded string, ví dụ:
            // "\"{\\\"hasAsyncPush\\\":true,\\\"hasLivePush\\\":true}\""
            // => cần Deserialize<string> trước khi parse JSON object.
            var decoded = JsonSerializer.Deserialize<string>(inspect);
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                using var doc = JsonDocument.Parse(decoded);
                var root = doc.RootElement;
                if (root.TryGetProperty("hasAsyncPush", out var asyncEl) && asyncEl.ValueKind == JsonValueKind.True)
                    hasAsyncPush = true;
                if (root.TryGetProperty("hasLivePush", out var liveEl) && liveEl.ValueKind == JsonValueKind.True)
                    hasLivePush = true;
            }
        }
        catch
        {
            // keep default false/false
        }

        if (!hasAsyncPush && !hasLivePush)
        {
            var sessionId = _pendingAsyncBuffer.Count > 0
                ? _pendingAsyncBuffer[^1].SessionId
                : "session:unknown";
            UpsertPendingAsyncBuffer(sessionId, key ?? string.Empty, value ?? string.Empty);
            _htmlRuntimeReady = false;
        }
    }

    private async Task<bool> PushBatchToWidgetRuntimeAsync(
        IReadOnlyList<(string SessionId, string Key, string Value)> batch,
        string nodeIdForLog)
    {
        if (_webView?.CoreWebView2 == null) return false;
        if (!_htmlRuntimeReady) return false;
        if (batch.Count == 0) return true;

        var payload = batch.Select(x => new[] { x.Key ?? string.Empty, x.Value ?? string.Empty }).ToList();
        var jsPayload = JsonSerializer.Serialize(payload);
        var inspect = await _webView.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
  try {{
    window.hostLive = window.hostLive || {{}};
    window.hostLive.values = window.hostLive.values || {{}};
    if (typeof window.hostLivePush !== 'function') {{
      window.hostLive._subs = window.hostLive._subs || {{}};
      window.hostLive._allSubs = window.hostLive._allSubs || [];
      window.hostLivePush = function(key, value) {{
        window.hostLive = window.hostLive || {{}};
        window.hostLive.values = window.hostLive.values || {{}};
        window.hostLive.values[key] = value;
      }};
    }}
    if (!window.hostAsyncReady) {{
      window.hostAsyncReady = true;
      var _data = {{}};
      var _keyCallbacks = {{}};
      var _allCallbacks = [];
      window.hostAsync = {{
        values: _data,
        on: function(keyOrFn, fn) {{
          if (typeof keyOrFn === 'function') {{
            _allCallbacks.push(keyOrFn);
          }} else if (typeof keyOrFn === 'string' && typeof fn === 'function') {{
            if (!_keyCallbacks[keyOrFn]) _keyCallbacks[keyOrFn] = [];
            _keyCallbacks[keyOrFn].push(fn);
          }}
        }}
      }};
      window.hostAsyncPush = function(key, value) {{
        _data[key] = value;
        var cbs = _keyCallbacks[key];
        if (cbs) for (var i = 0; i < cbs.length; i++) {{ try {{ cbs[i](value); }} catch (_) {{}} }}
        for (var j = 0; j < _allCallbacks.length; j++) {{ try {{ _allCallbacks[j](JSON.parse(JSON.stringify(_data))); }} catch (_) {{}} }}
      }};
    }} else if (typeof window.hostAsyncPush !== 'function') {{
      window.hostAsync = window.hostAsync || {{ values: {{}} }};
      window.hostAsyncPush = function(key, value) {{
        window.hostAsync.values = window.hostAsync.values || {{}};
        window.hostAsync.values[key] = value;
      }};
    }}

    var payload = {jsPayload};
    var pushed = 0;
    for (var k = 0; k < payload.length; k++) {{
      var row = payload[k];
      var key = row[0];
      var value = row[1];
      if (typeof window.hostAsyncPush === 'function') window.hostAsyncPush(key, value);
      if (typeof window.hostLivePush === 'function') window.hostLivePush(key, value);
      pushed++;
    }}

    return JSON.stringify({{
      hasAsyncPush: typeof window.hostAsyncPush === 'function',
      hasLivePush: typeof window.hostLivePush === 'function',
      pushed: pushed
    }});
  }} catch (e) {{
    return JSON.stringify({{ error: String(e && e.message ? e.message : e) }});
  }}
}})();");

        var hasAsyncPush = false;
        var hasLivePush = false;
        try
        {
            var decoded = JsonSerializer.Deserialize<string>(inspect);
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                using var doc = JsonDocument.Parse(decoded);
                var root = doc.RootElement;
                if (root.TryGetProperty("hasAsyncPush", out var asyncEl) && asyncEl.ValueKind == JsonValueKind.True)
                    hasAsyncPush = true;
                if (root.TryGetProperty("hasLivePush", out var liveEl) && liveEl.ValueKind == JsonValueKind.True)
                    hasLivePush = true;
            }
        }
        catch
        {
            hasAsyncPush = false;
            hasLivePush = false;
        }

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[FloatingWidget:{nodeIdForLog}] JS batch push count={batch.Count}, inspect={inspect}");
#endif

        if (!hasAsyncPush && !hasLivePush)
        {
            _htmlRuntimeReady = false;
            return false;
        }
        return true;
    }

    private void QueueDeferredPendingFlush(HtmlUiNode htmlNode)
    {
        if (_isDeferredPendingFlushQueued) return;
        _isDeferredPendingFlushQueued = true;
        _ = Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                for (var rounds = 0; rounds < 10; rounds++)
                {
                    DrainPendingAsyncQueueToBuffer(htmlNode);
                    MoveHiddenBacklogToPending();
                    if (_htmlRuntimeReady)
                        await FlushBufferedAsyncDataToWidgetAsync(htmlNode);

                    var hasRemaining = !htmlNode.PendingAsyncPushQueue.IsEmpty
                                       || _hiddenAsyncBacklog.Count > 0
                                       || _pendingAsyncBuffer.Count > 0;
                    if (!hasRemaining) break;
                    await Task.Delay(8);
                }
            }
            catch { }
            finally
            {
                _isDeferredPendingFlushQueued = false;
            }
        }, DispatcherPriority.Background);
    }

    private static string BuildRuntimeBridgeBootstrapJs()
    {
        return @"
(function() {
  try {
    window.hostLive = window.hostLive || {};
    window.hostLive.values = window.hostLive.values || {};
    window.hostLive._subs = window.hostLive._subs || {};
    window.hostLive._allSubs = window.hostLive._allSubs || [];
    window.hostLive.on = window.hostLive.on || function(key, cb) {
      if (typeof cb !== 'function') return;
      if (!this._subs[key]) this._subs[key] = [];
      this._subs[key].push(cb);
      try { cb(this.values[key]); } catch (_) {}
    };
    window.hostLive.onAll = window.hostLive.onAll || function(cb) {
      if (typeof cb !== 'function') return;
      this._allSubs.push(cb);
      try { cb(JSON.parse(JSON.stringify(this.values || {}))); } catch (_) {}
    };
    window.hostLivePush = function(key, value) {
      this.hostLive = this.hostLive || {};
      this.hostLive.values = this.hostLive.values || {};
      this.hostLive._subs = this.hostLive._subs || {};
      this.hostLive._allSubs = this.hostLive._allSubs || [];
      this.hostLive.values[key] = value;
      var list = this.hostLive._subs[key] || [];
      for (var i = 0; i < list.length; i++) { try { list[i](value); } catch (_) {} }
      for (var j = 0; j < this.hostLive._allSubs.length; j++) { try { this.hostLive._allSubs[j](JSON.parse(JSON.stringify(this.hostLive.values))); } catch (_) {} }
    };

    if (!window.hostAsyncReady) {
      window.hostAsyncReady = true;
      var _data = {};
      var _keyCallbacks = {};
      var _allCallbacks = [];
      window.hostAsync = {
        values: _data,
        on: function(keyOrFn, fn) {
          if (typeof keyOrFn === 'function') {
            _allCallbacks.push(keyOrFn);
          } else if (typeof keyOrFn === 'string' && typeof fn === 'function') {
            if (!_keyCallbacks[keyOrFn]) _keyCallbacks[keyOrFn] = [];
            _keyCallbacks[keyOrFn].push(fn);
          }
        }
      };
      window.hostAsyncPush = function(key, value) {
        _data[key] = value;
        var cbs = _keyCallbacks[key];
        if (cbs) {
          for (var i = 0; i < cbs.length; i++) { try { cbs[i](value); } catch (_) {} }
        }
        for (var j = 0; j < _allCallbacks.length; j++) {
          try { _allCallbacks[j](JSON.parse(JSON.stringify(_data))); } catch (_) {}
        }
      };
    } else if (typeof window.hostAsyncPush !== 'function') {
      window.hostAsync = window.hostAsync || { values: {} };
      window.hostAsyncPush = function(key, value) {
        window.hostAsync.values = window.hostAsync.values || {};
        window.hostAsync.values[key] = value;
      };
    }
  } catch (_) {}
})();";
    }

    private string BuildBridgeJs()
    {
        var mediaRootsJson = BuildMediaSearchRootsJson();
        var script = @"
// ── Widget Bridge JS ──
window.hostMediaSearchRoots = __AC_MEDIA_ROOTS__;
function hostSubmit() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ __widgetAction: 'submit', type: 'submit' });
    }
}
function hostStart() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ __widgetAction: 'startWorkflow', type: 'startWorkflow' });
    }
}
function hostResolvePath(localPath, requestId) {
    try {
      if (!(window.chrome && window.chrome.webview)) return;
      window.chrome.webview.postMessage({
        type: 'resolve_local_path',
        path: localPath || '',
        requestId: requestId || ''
      });
    } catch (_) {}
}
function hostCurl(curlCommand, fileName, downloadKey) {
    try {
      if (!(window.chrome && window.chrome.webview)) return;
      window.chrome.webview.postMessage({
        type: 'download_curl',
        curl: curlCommand || '',
        fileName: fileName || '',
        downloadKey: downloadKey || ''
      });
    } catch (_) {}
}
function hostPickImages(requestId) {
    try {
      if (!(window.chrome && window.chrome.webview)) return;
      window.chrome.webview.postMessage({
        type: 'pick_image_files',
        requestId: requestId || ''
      });
    } catch (_) {}
}
function hostResolveRef(url, requestId) {
    try {
      if (!(window.chrome && window.chrome.webview)) return;
      var roots = [];
      try {
        if (Array.isArray(window.hostMediaSearchRoots)) roots = window.hostMediaSearchRoots;
      } catch (_) {}
      window.chrome.webview.postMessage({
        type: 'resolve_playable_ref',
        url: url || '',
        requestId: requestId || '',
        searchRoots: roots
      });
    } catch (_) {}
}
// Expose only new API names
window.hostSubmit = hostSubmit;
window.hostStart = hostStart;
window.hostResolvePath = hostResolvePath;
window.hostCurl = hostCurl;
window.hostPickImages = hostPickImages;
window.hostResolveRef = hostResolveRef;

window.hostLive = window.hostLive || {};
window.hostLive.values = window.hostLive.values || {};
window.hostLive._subs = window.hostLive._subs || {};
window.hostLive._allSubs = window.hostLive._allSubs || [];
window.hostLive.on = function() {
  var args = Array.prototype.slice.call(arguments);
  var cb = args[args.length - 1];
  if (typeof cb !== 'function') return;
  if (args.length === 1 && typeof args[0] === 'function') {
    window.hostLive._allSubs.push(args[0]);
    return;
  }
  var keys = args.slice(0, -1).map(function(k){ return String(k); });
  var token = { keys: keys, cb: cb };
  keys.forEach(function(k){
    window.hostLive._subs[k] = window.hostLive._subs[k] || [];
    window.hostLive._subs[k].push(token);
  });
};
window.hostLivePush = function(key, value) {
  window.hostLive.values[key] = value;
  var keySubs = window.hostLive._subs[String(key)] || [];
  for (var i = 0; i < keySubs.length; i++) {
    var sub = keySubs[i];
    try {
      var vals = sub.keys.map(function(k){ return window.hostLive.values[k]; });
      sub.cb.apply(null, vals);
    } catch(e) {}
  }
  for (var j = 0; j < window.hostLive._allSubs.length; j++) {
    try { window.hostLive._allSubs[j](window.hostLive.values); } catch(e) {}
  }
};

// Async receiver runtime (tương thích HtmlUiNode Async Data tab)
(function(){
  if (window.hostAsyncReady) return;
  window.hostAsyncReady = true;
  var _data = {};
  var _keyCallbacks = {};
  var _allCallbacks = [];
  window.hostAsync = {
    values: _data,
    on: function(keyOrFn, fn) {
      if (typeof keyOrFn === 'function') {
        _allCallbacks.push(keyOrFn);
      } else if (typeof keyOrFn === 'string' && typeof fn === 'function') {
        if (!_keyCallbacks[keyOrFn]) _keyCallbacks[keyOrFn] = [];
        _keyCallbacks[keyOrFn].push(fn);
      }
    }
  };
  window.hostAsyncPush = function(key, value) {
    _data[key] = value;
    var cbs = _keyCallbacks[key];
    if (cbs) {
      for (var i = 0; i < cbs.length; i++) {
        try { cbs[i](value); } catch(e) {}
      }
    }
    for (var j = 0; j < _allCallbacks.length; j++) {
      try { _allCallbacks[j](JSON.parse(JSON.stringify(_data))); } catch(e) {}
    }
  };
})();
window.hostAsync = window.hostAsync || {};
window.hostAsync.on = window.hostAsync.on || function(){};
window.hostAsync.values = window.hostAsync.values || {};
";
        return script.Replace("__AC_MEDIA_ROOTS__", mediaRootsJson);
    }

    private Dictionary<string, string> ResolveInputValues(HtmlUiNode htmlNode)
    {
        return _viewModel.ResolveInputValues(htmlNode);
    }

    private static string ReplaceVariables(string text, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(text) || vars.Count == 0) return text;
        var regex = new System.Text.RegularExpressions.Regex(@"\{([^}]+)\}");
        return regex.Replace(text, match =>
        {
            var name = match.Groups[1].Value.Trim();
            return vars.TryGetValue(name, out var value) ? value ?? string.Empty : match.Value;
        });
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("__widgetAction", out var actionEl))
            {
                var action = actionEl.GetString();
                switch (action)
                {
                    case "submit":
                        HandleSubmit(root);
                        break;
                    case "startWorkflow":
                        _ = Dispatcher.InvokeAsync(async () => await HandleStartWorkflowAsync(), DispatcherPriority.Background);
                        break;
                }
            }
            else if (root.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
            {
                var type = typeEl.GetString();
                JsonElement rootSnapshot;
                switch (type)
                {
                    case "submit":
                        HandleSubmit(root);
                        break;
                    case "startWorkflow":
                        _ = Dispatcher.InvokeAsync(async () => await HandleStartWorkflowAsync(), DispatcherPriority.Background);
                        break;
                    case "reload":
                        _ = Dispatcher.InvokeAsync(async () => await ReloadContentAsync(), DispatcherPriority.Background);
                        break;
                    case "resolve_local_path":
                        rootSnapshot = root.Clone();
                        _ = Dispatcher.InvokeAsync(async () => await HandleResolveLocalPathAsync(rootSnapshot), DispatcherPriority.Background);
                        break;
                    case "download_curl":
                        rootSnapshot = root.Clone();
                        _ = Dispatcher.InvokeAsync(async () => await HandleDownloadByCurlAsync(rootSnapshot), DispatcherPriority.Background);
                        break;
                    case "pick_image_files":
                        rootSnapshot = root.Clone();
                        _ = Dispatcher.InvokeAsync(async () => await HandlePickImageFilesAsync(rootSnapshot), DispatcherPriority.Background);
                        break;
                    case "resolve_playable_ref":
                        rootSnapshot = root.Clone();
                        _ = Dispatcher.InvokeAsync(async () => await HandleResolvePlayableRefAsync(rootSnapshot), DispatcherPriority.Background);
                        break;
                    default:
                        HandleGenericOutputs(root);
                        break;
                }
            }
            else
            {
                // Generic postMessage → treat as outputs
                HandleGenericOutputs(root);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FloatingWidget] WebMessage error: {ex.Message}");
        }

        MarkActivity();
    }

    private void HandleSubmit(JsonElement root)
    {
        if (_node is HtmlUiNode htmlNode)
        {
            _ = Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await UpdateOutputsFromDomAsync(htmlNode);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FloatingWidget] Submit read params error: {ex.Message}");
                }
            }, DispatcherPriority.Background);
        }
    }

    private async Task UpdateOutputsFromDomAsync(HtmlUiNode htmlNode)
    {
        if (_webView?.CoreWebView2 == null) return;

        var mappings = _viewModel.ParseParams(htmlNode);
        foreach (var (key, selector) in mappings)
        {
            var jsSelector = selector.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var script = $@"
(function() {{
  try {{
    var el = document.querySelector(""{jsSelector}"");
    if (!el) return null;
    if (typeof el.value !== 'undefined') return el.value;
    if (el.textContent) return el.textContent;
    return null;
  }} catch (e) {{
    return null;
  }}
}})();";

            string resultJson;
            try
            {
                resultJson = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch
            {
                continue;
            }

            string? value = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(resultJson) &&
                    !string.Equals(resultJson, "null", StringComparison.OrdinalIgnoreCase))
                {
                    value = JsonSerializer.Deserialize<string>(resultJson);
                }
            }
            catch
            {
                value = resultJson;
            }

            if (value == null) continue;
            htmlNode.ResolvedOutputs[key] = value;
            var dyn = htmlNode.DynamicOutputs?.FirstOrDefault(o =>
                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            if (dyn != null)
                dyn.UserValueOverride = value;
        }

        _host.RequestSyncDataPanels(immediate: false);
    }

    private async Task HandleStartWorkflowAsync()
    {
        try
        {
            // Đảm bảo param DOM được cập nhật trước khi kick workflow.
            if (_node is HtmlUiNode htmlNode)
            {
                await UpdateOutputsFromDomAsync(htmlNode);
            }

            StartWorkflowFromWidget();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FloatingWidget] StartWorkflow error: {ex.Message}");
        }
    }

    private async Task HandleResolveLocalPathAsync(JsonElement root)
    {
        if (_webView?.CoreWebView2 == null) return;

        var localPath = root.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : string.Empty;
        var requestId = root.TryGetProperty("requestId", out var r) && r.ValueKind == JsonValueKind.String
            ? r.GetString()
            : string.Empty;

        var ok = false;
        var localUrl = string.Empty;
        var error = string.Empty;

        try
        {
            if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
            {
                var full = Path.GetFullPath(localPath);
                var folder = Path.GetDirectoryName(full) ?? string.Empty;
                var fileName = Path.GetFileName(full);
                var localHost = await EnsureLocalHostMappingAsync(folder);
                localUrl = $"https://{localHost}/{Uri.EscapeDataString(fileName)}";
                ok = true;
            }
            else
            {
                error = "File not found";
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        var detailJson = JsonSerializer.Serialize(new
        {
            requestId = requestId ?? string.Empty,
            ok,
            localUrl,
            localPath = localPath ?? string.Empty,
            error
        });
        var script =
            "window.dispatchEvent(new CustomEvent('hostPathResolved',{detail:" + detailJson + "}));";
        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch { }
    }

    private async Task HandleDownloadByCurlAsync(JsonElement root)
    {
        if (_webView?.CoreWebView2 == null) return;

        var curlCmd = root.TryGetProperty("curl", out var curlProp) && curlProp.ValueKind == JsonValueKind.String
            ? curlProp.GetString()
            : string.Empty;
        var desiredFileName = root.TryGetProperty("fileName", out var fnProp) && fnProp.ValueKind == JsonValueKind.String
            ? fnProp.GetString()
            : string.Empty;
        var downloadKey = root.TryGetProperty("downloadKey", out var dkProp) && dkProp.ValueKind == JsonValueKind.String
            ? dkProp.GetString()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(curlCmd)) return;

        var ok = false;
        var outPath = string.Empty;
        var errMsg = string.Empty;
        var localUrl = string.Empty;
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var targetDir = Path.Combine(userProfile, "Downloads", "Workflow_Downloads", "Videos");
            Directory.CreateDirectory(targetDir);

            var safeName = string.IsNullOrWhiteSpace(desiredFileName)
                ? $"video_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.mp4"
                : desiredFileName.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            if (!safeName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                safeName += ".mp4";

            outPath = Path.Combine(targetDir, safeName);

            var raw = curlCmd ?? string.Empty;
            raw = raw.Replace("\\r\\n", "\n").Replace("\\n", "\n").Replace("\\r", "\n");
            raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");
            raw = raw.Replace("\\\"", "\"");
            raw = raw.Replace("^\n", " ").Replace("^\"", "\"").Replace("^^", "^");

            var mLoc1 = System.Text.RegularExpressions.Regex.Match(raw, @"--location\s+'([^']+)'", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var mLoc2 = System.Text.RegularExpressions.Regex.Match(raw, "--location\\s+\"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var url = mLoc1.Success ? mLoc1.Groups[1].Value : (mLoc2.Success ? mLoc2.Groups[1].Value : string.Empty);
            if (string.IsNullOrWhiteSpace(url))
            {
                var mUrl = System.Text.RegularExpressions.Regex.Match(raw, @"https?://[^\s'""\\]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (mUrl.Success) url = mUrl.Value;
            }
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Cannot parse URL from curl.");

            url = url.Replace("\\/", "/").Trim();
            var headerMatches = System.Text.RegularExpressions.Regex.Matches(raw, @"--header\s+'([^']*)'", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Text.RegularExpressions.Match hm in headerMatches)
            {
                var hv = hm.Groups[1].Value;
                var idx = hv.IndexOf(':');
                if (idx <= 0) continue;
                var hk = hv[..idx].Trim();
                var vv = hv[(idx + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(hk)) headers[hk] = vv;
            }

            using var handler = new System.Net.Http.HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
                UseCookies = false
            };
            using var client = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("accept", "*/*");
            foreach (var kv in headers)
            {
                if (!req.Headers.TryAddWithoutValidation(kv.Key, kv.Value))
                {
                    req.Content ??= new System.Net.Http.StringContent(string.Empty);
                    req.Content.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }

            using var resp = await client.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");

            await using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            await using (var rs = await resp.Content.ReadAsStreamAsync())
                await rs.CopyToAsync(fs);

            ok = File.Exists(outPath) && new FileInfo(outPath).Length > 0;
            if (!ok) errMsg = "Downloaded file is empty.";
            if (ok)
            {
                var dlHost = await EnsureLocalHostMappingAsync(targetDir);
                localUrl = $"https://{dlHost}/{Uri.EscapeDataString(Path.GetFileName(outPath))}";
            }
        }
        catch (Exception ex)
        {
            ok = false;
            errMsg = ex.Message;
        }

        var payload = JsonSerializer.Serialize(new
        {
            ok,
            path = outPath,
            error = errMsg,
            key = downloadKey ?? string.Empty,
            localUrl
        });
        await DispatchJsEventAsync("hostCurlDone", payload);
    }

    private async Task HandlePickImageFilesAsync(JsonElement root)
    {
        if (_webView?.CoreWebView2 == null) return;

        var requestId = root.TryGetProperty("requestId", out var reqIdProp) && reqIdProp.ValueKind == JsonValueKind.String
            ? reqIdProp.GetString()
            : string.Empty;

        var filesPayload = new List<object>();
        var ok = false;
        var errMsg = string.Empty;

        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn ảnh upload",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.svg;*.tif;*.tiff;*.ico|All Files|*.*",
                Multiselect = true,
                CheckFileExists = true,
                CheckPathExists = true
            };
            var owner = Window.GetWindow(_webView);
            var rs = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
            var picked = rs == true ? (dlg.FileNames ?? Array.Empty<string>()) : Array.Empty<string>();

            foreach (var path in picked)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                var full = Path.GetFullPath(path);
                var bytes = await File.ReadAllBytesAsync(full);
                if (bytes.Length == 0) continue;
                var mime = GuessImageMimeType(full);
                var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                filesPayload.Add(new
                {
                    name = Path.GetFileName(full),
                    path = full,
                    size = bytes.LongLength,
                    dataUrl
                });
            }
            ok = filesPayload.Count > 0;
        }
        catch (Exception ex)
        {
            ok = false;
            errMsg = ex.Message;
        }

        var payload = JsonSerializer.Serialize(new
        {
            ok,
            requestId = requestId ?? string.Empty,
            files = filesPayload,
            error = errMsg
        });
        await DispatchJsEventAsync("hostImagesPicked", payload);
    }

    private async Task HandleResolvePlayableRefAsync(JsonElement root)
    {
        if (_webView?.CoreWebView2 == null) return;

        var refUrl = root.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String
            ? urlProp.GetString()
            : string.Empty;
        var requestId = root.TryGetProperty("requestId", out var reqProp) && reqProp.ValueKind == JsonValueKind.String
            ? reqProp.GetString()
            : string.Empty;

        var ok = false;
        var localUrl = string.Empty;
        var errMsg = string.Empty;
        var resolvedPath = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(refUrl))
                throw new InvalidOperationException("URL is empty.");

            if (!Uri.TryCreate(refUrl.Trim(), UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Invalid URL.");

            var rawPath = Uri.UnescapeDataString((uri.AbsolutePath ?? string.Empty).TrimStart('/'));
            var baseName = Path.GetFileName(rawPath.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(baseName))
                throw new InvalidOperationException("Invalid file name.");

            var searchRoots = new List<string>();
            if (root.TryGetProperty("searchRoots", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                        searchRoots.Add(el.GetString()!);
                }
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloadsRoot = Path.Combine(userProfile, "Downloads");
            var wfRoot = Path.Combine(downloadsRoot, "Workflow_Downloads");
            searchRoots.Add(downloadsRoot);
            searchRoots.Add(wfRoot);
            searchRoots.Add(Path.Combine(wfRoot, "Videos"));

            string? found = null;
            foreach (var rootDir in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir)) continue;
                var direct = Path.Combine(rootDir, baseName);
                if (File.Exists(direct)) { found = direct; break; }
                try
                {
                    var hit = Directory.GetFiles(rootDir, baseName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(hit)) { found = hit; break; }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(found) || !File.Exists(found))
                throw new FileNotFoundException("Video file not found from playable ref.", rawPath);

            resolvedPath = Path.GetFullPath(found);
            var folder = Path.GetDirectoryName(resolvedPath) ?? string.Empty;
            var fileName = Path.GetFileName(resolvedPath);
            var localHost = await EnsureLocalHostMappingAsync(folder);
            localUrl = $"https://{localHost}/{Uri.EscapeDataString(fileName)}";
            ok = true;
        }
        catch (Exception ex)
        {
            ok = false;
            errMsg = ex.Message;
        }

        var payload = JsonSerializer.Serialize(new
        {
            ok,
            requestId = requestId ?? string.Empty,
            path = resolvedPath ?? string.Empty,
            localUrl,
            error = errMsg
        });
        // Keep same event as HtmlUiNodeControl for compatibility.
        await DispatchJsEventAsync("hostPathResolved", payload);
    }

    private async Task DispatchJsEventAsync(string eventName, string detailJson)
    {
        if (_webView?.CoreWebView2 == null) return;
        var script = $"window.dispatchEvent(new CustomEvent('{eventName}',{{detail:{detailJson}}}));";
        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch { }
    }

    private static string GuessImageMimeType(string? filePath)
    {
        var ext = (Path.GetExtension(filePath ?? string.Empty) ?? string.Empty).Trim().ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".tif" or ".tiff" => "image/tiff",
            ".ico" => "image/x-icon",
            _ => "image/jpeg"
        };
    }

    private static string BuildMediaSearchRootsJson()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void TryAdd(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            try
            {
                var full = Path.GetFullPath(p);
                if (Directory.Exists(full)) set.Add(full);
            }
            catch { }
        }

        TryAdd(Environment.CurrentDirectory);
        TryAdd(AppContext.BaseDirectory);
        try
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            TryAdd(Path.Combine(profile, "Downloads"));
            TryAdd(Path.Combine(profile, "Downloads", "Workflow_Downloads"));
            TryAdd(Path.Combine(profile, "Downloads", "Workflow_Downloads", "Videos"));
            TryAdd(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        }
        catch { }

        return JsonSerializer.Serialize(set.ToList());
    }

    private static string BuildLocalHostForFolder(string folderPath)
    {
        var key = (folderPath ?? string.Empty).Trim().ToLowerInvariant();
        var hash = Math.Abs(key.GetHashCode()).ToString("x8");
        return $"localfiles-{hash}.local";
    }

    private async Task<string> EnsureLocalHostMappingAsync(string folderPath)
    {
        if (_webView?.CoreWebView2 == null)
            throw new InvalidOperationException("WebView2 not initialized.");
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new InvalidOperationException("Folder path is empty.");

        var fullFolder = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullFolder))
            throw new DirectoryNotFoundException($"Mapped folder not found: {fullFolder}");

        string localHost;
        lock (_localHostMapSync)
        {
            if (!_localHostByFolder.TryGetValue(fullFolder, out localHost!))
            {
                localHost = BuildLocalHostForFolder(fullFolder);
                _localHostByFolder[fullFolder] = localHost;
                _localFolderByHost[localHost] = fullFolder;
            }
        }

        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            localHost,
            fullFolder,
            CoreWebView2HostResourceAccessKind.Allow);

        return localHost;
    }

    private void HandleGenericOutputs(JsonElement root)
    {
        if (_node is HtmlUiNode htmlNode)
        {
            foreach (var prop in root.EnumerateObject())
            {
                htmlNode.ResolvedOutputs[prop.Name] = prop.Value.ToString();
            }
        }
    }

    // ═══════════════════════════════════════════
    //  BUTTON HANDLERS
    // ═══════════════════════════════════════════

    private void CollapseBtn_Click(object sender, RoutedEventArgs e)
    {
        CollapseWidget();
    }

    private void TitleCloseBtn_Click(object sender, RoutedEventArgs e)
    {
        SavePosition();
        Close();
    }

    private void TitleMaxRestoreBtn_Click(object sender, RoutedEventArgs e)
    {
        ToggleExpandedMaximizeRestore();
    }

    private void StartWorkflowBtn_Click(object sender, RoutedEventArgs e)
    {
        StartWorkflowFromWidget();
    }

    private void StopWorkflowBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!StopSessionsPopup.IsOpen)
        {
            OpenStopSessionsPopup();
        }
    }

    private void StopWorkflowBtn_MouseEnter(object sender, MouseEventArgs e)
    {
        OpenStopSessionsPopup();
    }

    private void StopWorkflowBtn_MouseLeave(object sender, MouseEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!_isStopPopupHovering && !StopSessionsPopup.IsMouseOver)
            {
                StopSessionsPopup.IsOpen = false;
            }
        }));
    }

    private void StopSessionsPopup_MouseEnter(object sender, MouseEventArgs e)
    {
        _isStopPopupHovering = true;
    }

    private void StopSessionsPopup_MouseLeave(object sender, MouseEventArgs e)
    {
        _isStopPopupHovering = false;
        StopSessionsPopup.IsOpen = false;
    }

    private void StartWorkflowFromWidget()
    {
        try
        {
            var vm = _host?.ViewModel;
            if (vm?.StartTestCommand?.CanExecute(null) == true)
            {
                vm.StartTestCommand.Execute(null);
            }
            UpdateRunButtonsState();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FloatingWidget] Start button error: {ex.Message}");
        }
    }

    private void OpenStopSessionsPopup()
    {
        BuildStopSessionsButtons();
        StopSessionsPopup.IsOpen = StopSessionsButtonsPanel.Children.Count > 0;
    }

    private void BuildStopSessionsButtons()
    {
        StopSessionsButtonsPanel.Children.Clear();
        var sessions = GetManualSessionsSnapshot();
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            var btn = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(i == sessions.Count - 1 ? 0 : 4, 0, 0, 0),
                Content = (i + 1).ToString(),
                Tag = session.SessionId,
                ToolTip = session.LineText,
                Padding = new Thickness(0),
                FontSize = 10
            };
            if (TryFindResource("DangerButton") is Style dangerStyle)
                btn.Style = dangerStyle;
            btn.Click += StopOneSessionBtn_Click;
            StopSessionsButtonsPanel.Children.Add(btn);
        }
    }

    private void StopOneSessionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string sessionId) return;
        _host?.ViewModel?.CancelManualRunSession(sessionId);
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            BuildStopSessionsButtons();
            UpdateRunButtonsState();
            if (StopSessionsButtonsPanel.Children.Count == 0)
                StopSessionsPopup.IsOpen = false;
        }));
    }

    private List<ManualWorkflowRunSessionViewModel> GetManualSessionsSnapshot()
    {
        var vm = _host?.ViewModel;
        if (vm?.ManualRunSessions == null) return new List<ManualWorkflowRunSessionViewModel>();
        return vm.ManualRunSessions.ToList();
    }

    private void AttachManualRunObservers()
    {
        var vm = _host?.ViewModel;
        if (vm == null) return;
        if (_manualRunSessionsNotify != null || _workflowVmNotify != null) return;

        _manualRunSessionsNotify = vm.ManualRunSessions;
        _manualRunSessionsNotify.CollectionChanged += ManualRunSessions_CollectionChanged;
        _workflowVmNotify = vm;
        _workflowVmNotify.PropertyChanged += WorkflowVm_PropertyChanged;
    }

    private void DetachManualRunObservers()
    {
        if (_manualRunSessionsNotify != null)
            _manualRunSessionsNotify.CollectionChanged -= ManualRunSessions_CollectionChanged;
        _manualRunSessionsNotify = null;
        if (_workflowVmNotify != null)
            _workflowVmNotify.PropertyChanged -= WorkflowVm_PropertyChanged;
        _workflowVmNotify = null;
    }

    private void ManualRunSessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            UpdateRunButtonsState();
            if (StopSessionsPopup.IsOpen)
                BuildStopSessionsButtons();
        }));
    }

    private void WorkflowVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkflowEditorViewModel.ManualExecutionRunsInFlight), StringComparison.Ordinal))
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateRunButtonsState();
            }
            else
            {
                Dispatcher.Invoke(UpdateRunButtonsState, DispatcherPriority.Send);
            }

            if ((_host?.ViewModel?.ManualExecutionRunsInFlight ?? 0) == 0)
                TriggerFinalDataFlushAfterExecution();
            return;
        }

        if (string.Equals(e.PropertyName, nameof(WorkflowEditorViewModel.IsExecuting), StringComparison.Ordinal)
            && _host?.ViewModel?.IsExecuting == false)
        {
            TriggerFinalDataFlushAfterExecution();
        }
    }

    private void TriggerFinalDataFlushAfterExecution()
    {
        if (_node is not HtmlUiNode htmlNode) return;
        _ = Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                // Runtime code đã kết thúc: flush nhịp cuối để result trên UI chốt giá trị mới nhất.
                DrainPendingAsyncQueueToBuffer(htmlNode);
                MoveHiddenBacklogToPending();
                if (_htmlRuntimeReady)
                {
                    for (var i = 0; i < 4; i++)
                    {
                        var flushed = await FlushBufferedAsyncDataToWidgetAsync(htmlNode);
                        DrainPendingAsyncQueueToBuffer(htmlNode);
                        MoveHiddenBacklogToPending();
                        if (flushed == 0 && htmlNode.PendingAsyncPushQueue.IsEmpty && _pendingAsyncBuffer.Count == 0)
                            break;
                        await Task.Delay(4);
                    }
                }
            }
            catch { }
        }, DispatcherPriority.Send);
    }

    private static bool IsStrictFinalSyncEnabled()
    {
        try
        {
            return CanvasToolbarPreferencesStore.Load()?.StrictFinalSyncEnabled ?? true;
        }
        catch
        {
            return true;
        }
    }

    private void UpdateRunButtonsState()
    {
        var runCount = _host?.ViewModel?.ManualExecutionRunsInFlight ?? 0;
        var runCountText = runCount > 99 ? "99+" : runCount.ToString();
        var badgeVisibility = runCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (StartRunCountText != null) StartRunCountText.Text = runCountText;
        if (StopRunCountText != null) StopRunCountText.Text = runCountText;
        if (StartRunCountBadge != null) StartRunCountBadge.Visibility = badgeVisibility;
        if (StopRunCountBadge != null) StopRunCountBadge.Visibility = badgeVisibility;
        if (StopWorkflowBtn != null) StopWorkflowBtn.IsEnabled = runCount > 0;

        if (_titleRevealStartButton != null)
            _titleRevealStartButton.IsEnabled = true;
        if (_titleRevealStopButton != null)
            _titleRevealStopButton.IsEnabled = runCount > 0;
        if (_titleRevealStartBadgeText != null) _titleRevealStartBadgeText.Text = runCountText;
        if (_titleRevealStopBadgeText != null) _titleRevealStopBadgeText.Text = runCountText;
        if (_titleRevealStartBadge != null) _titleRevealStartBadge.Visibility = badgeVisibility;
        if (_titleRevealStopBadge != null) _titleRevealStopBadge.Visibility = badgeVisibility;
    }

    private void ShowTitleRevealStopSessionMenu(Button anchorButton)
    {
        var sessions = GetManualSessionsSnapshot();
        if (sessions.Count == 0) return;

        EnsureTitleRevealStopSessionsPopup(anchorButton);
        if (_titleRevealStopSessionsPanel == null || _titleRevealStopSessionsPopup == null) return;

        _titleRevealStopSessionsPanel.Children.Clear();
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            var idx = i + 1;
            var btn = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(i == sessions.Count - 1 ? 0 : 4, 0, 4, 0),
                Content = idx.ToString(),
                Tag = session.SessionId,
                ToolTip = session.LineText,
                Padding = new Thickness(0),
                FontSize = 10
            };
            if (TryFindResource("DangerButton") is Style dangerStyle)
                btn.Style = dangerStyle;
            btn.Click += (_, __) =>
            {
                if (btn.Tag is string sessionId)
                {
                    _host?.ViewModel?.CancelManualRunSession(sessionId);
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        UpdateRunButtonsState();
                        ShowTitleRevealStopSessionMenu(anchorButton);
                    }));
                }
            };
            _titleRevealStopSessionsPanel.Children.Add(btn);
        }

        _titleRevealStopSessionsPopup.PlacementTarget = anchorButton;
        _titleRevealStopSessionsPopup.IsOpen = _titleRevealStopSessionsPanel.Children.Count > 0;
    }

    private void EnsureTitleRevealStopSessionsPopup(Button anchorButton)
    {
        if (_titleRevealStopSessionsPopup != null) return;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        var border = new Border
        {
            Background = TryFindResource("CardColor") as Brush ?? Brushes.Black,
            BorderBrush = TryFindResource("BorderColor") as Brush ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6),
            Child = panel
        };
        border.MouseEnter += (_, __) => _isTitleRevealStopPopupHovering = true;
        border.MouseLeave += (_, __) =>
        {
            _isTitleRevealStopPopupHovering = false;
            if (_titleRevealStopSessionsPopup != null)
                _titleRevealStopSessionsPopup.IsOpen = false;
        };

        _titleRevealStopSessionsPanel = panel;
        _titleRevealStopSessionsPopup = new Popup
        {
            PlacementTarget = anchorButton,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = border
        };
    }

    // ═══════════════════════════════════════════
    //  CONTEXT MENU (chuột phải)
    // ═══════════════════════════════════════════

    private void ContextMenu_Opening(object sender, ContextMenuEventArgs e)
    {
        // Hủy pending click/drag để không mở widget khi mở context menu
        if (_pendingInteraction)
        {
            _pendingInteraction = false;
            _dragSource?.ReleaseMouseCapture();
            _dragSource = null;
        }

        if (sender is not FrameworkElement fe || fe.ContextMenu == null) return;
        var menu = fe.ContextMenu;

        foreach (var obj in menu.Items)
        {
            if (obj is not MenuItem mi || mi.Tag is not string tag) continue;
            switch (tag)
            {
                case "expand":
                    mi.IsEnabled = !_isExpanded;
                    mi.Visibility = _isExpanded ? Visibility.Collapsed : Visibility.Visible;
                    break;
                case "collapse":
                    mi.IsEnabled = _isExpanded;
                    mi.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case "topmost":
                    mi.IsChecked = Topmost;
                    break;
            }
        }
    }

    private void ContextExpand_Click(object sender, RoutedEventArgs e) => ExpandWidget();
    private void ContextCollapse_Click(object sender, RoutedEventArgs e) => CollapseWidget();

    private void ContextReload_Click(object sender, RoutedEventArgs e)
    {
        if (!_isExpanded) ExpandWidget();
        RefreshContent();
    }

    private void ContextTopmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        if (_node.FloatingWidget != null)
            _node.FloatingWidget.AlwaysOnTop = Topmost;
        SyncTitleRevealHostTopmost();
    }

    private void ContextClose_Click(object sender, RoutedEventArgs e)
    {
        SavePosition();
        Close();
    }

    private void ToggleExpandedMaximizeRestore()
    {
        if (!_isExpanded) return;
        var area = GetTargetWorkArea();
        var margin = Math.Max(0, Config.SnapMargin);

        if (!_isWidgetMaximized)
        {
            _restoreExpandedBounds = new Rect(Left, Top, Width, Height);
            Left = area.Left + margin;
            Top = area.Top + margin;
            Width = Math.Max(220, area.Width - (margin * 2));
            Height = Math.Max(160, area.Height - (margin * 2));
            _isWidgetMaximized = true;
        }
        else
        {
            if (_restoreExpandedBounds.Width > 0 && _restoreExpandedBounds.Height > 0)
            {
                Left = _restoreExpandedBounds.Left;
                Top = _restoreExpandedBounds.Top;
                Width = _restoreExpandedBounds.Width;
                Height = _restoreExpandedBounds.Height;
            }
            _isWidgetMaximized = false;
        }

        ClampToWorkArea();
        UpdateTitleRevealButtonPlacement();
        UpdateTitleMaxRestoreVisualState();
    }

    private void UpdateTitleMaxRestoreVisualState()
    {
        if (TitleMaxRestoreBtn == null || TitleMaxRestoreIcon == null) return;
        TitleMaxRestoreIcon.Text = _isWidgetMaximized ? "❐" : "▢";
        TitleMaxRestoreBtn.ToolTip = _isWidgetMaximized
            ? "Khôi phục kích thước widget"
            : "Phóng to widget";
    }

    // ═══════════════════════════════════════════
    //  ANIMATIONS
    // ═══════════════════════════════════════════

    private void ApplyIdleAnimation()
    {
        StopIdleAnimation();
        switch (Config.IdleAnimation)
        {
            case WidgetIdleAnimation.None:
                return;
            case WidgetIdleAnimation.Ripple:
                AnimateRipple();
                return;
            case WidgetIdleAnimation.Heartbeat:
            default:
                AnimateHeartbeat();
                return;
        }
    }

    private void StopIdleAnimation()
    {
        try
        {
            IdleScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            IdleScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            IdleDiamondScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            IdleDiamondScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            RippleScale1.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            RippleScale1.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            RippleScale2.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            RippleScale2.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            RippleLayer1.BeginAnimation(OpacityProperty, null);
            RippleLayer2.BeginAnimation(OpacityProperty, null);
            RippleLayer1.Visibility = Visibility.Collapsed;
            RippleLayer2.Visibility = Visibility.Collapsed;
        }
        catch { }
    }

    private Border? GetVisibleIdleShape()
    {
        if (IdleCircle.Visibility == Visibility.Visible) return IdleCircle;
        if (IdleDiamond.Visibility == Visibility.Visible) return IdleDiamond;
        if (IdleSquare.Visibility == Visibility.Visible) return IdleSquare;
        if (IdleRoundedSquare.Visibility == Visibility.Visible) return IdleRoundedSquare;
        if (EdgeDockSquare.Visibility == Visibility.Visible) return EdgeDockSquare;
        return null;
    }

    private void AnimateHeartbeat()
    {
        try
        {
            // Keep pulse amplitude small to avoid subpixel blur/jitter on transparent overlay windows.
            var scale = new DoubleAnimation(1.0, 1.06, TimeSpan.FromMilliseconds(420))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            if (IdleCircle.Visibility == Visibility.Visible)
            {
                IdleScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
                IdleScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
                return;
            }
            if (IdleDiamond.Visibility == Visibility.Visible)
            {
                IdleDiamondScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
                IdleDiamondScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
                return;
            }

            var active = GetVisibleIdleShape();
            if (active == null) return;
            if (active.RenderTransform is not ScaleTransform st)
            {
                st = new ScaleTransform(1, 1);
                active.RenderTransform = st;
                active.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            st.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }
        catch { }
    }

    private void AnimateRipple()
    {
        try
        {
            ConfigureRippleLayersForCurrentShape();
            RippleLayer1.Visibility = Visibility.Visible;
            RippleLayer2.Visibility = Visibility.Visible;

            var s1 = new DoubleAnimation(1.0, 2.15, TimeSpan.FromMilliseconds(1300))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            var s2 = new DoubleAnimation(1.0, 2.55, TimeSpan.FromMilliseconds(1300))
            {
                BeginTime = TimeSpan.FromMilliseconds(350),
                RepeatBehavior = RepeatBehavior.Forever
            };
            var o1 = new DoubleAnimation(0.5, 0.0, TimeSpan.FromMilliseconds(1300))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            var o2 = new DoubleAnimation(0.4, 0.0, TimeSpan.FromMilliseconds(1300))
            {
                BeginTime = TimeSpan.FromMilliseconds(350),
                RepeatBehavior = RepeatBehavior.Forever
            };

            RippleScale1.BeginAnimation(ScaleTransform.ScaleXProperty, s1);
            RippleScale1.BeginAnimation(ScaleTransform.ScaleYProperty, s1);
            RippleScale2.BeginAnimation(ScaleTransform.ScaleXProperty, s2);
            RippleScale2.BeginAnimation(ScaleTransform.ScaleYProperty, s2);
            RippleLayer1.BeginAnimation(OpacityProperty, o1);
            RippleLayer2.BeginAnimation(OpacityProperty, o2);
        }
        catch { }
    }

    private void ConfigureRippleLayersForCurrentShape()
    {
        var active = GetVisibleIdleShape();
        if (active == null) return;

        var w = active.Width > 0 ? active.Width : Config.IdleSize;
        var h = active.Height > 0 ? active.Height : Config.IdleSize;
        var cr = active.CornerRadius;
        if (active == IdleCircle)
            cr = new CornerRadius(Math.Min(w, h) / 2.0);
        else if (active == IdleRoundedSquare)
            cr = new CornerRadius(Math.Min(w, h) * 0.2);
        RippleLayer1.Width = w;
        RippleLayer1.Height = h;
        RippleLayer2.Width = w;
        RippleLayer2.Height = h;
        RippleLayer1.HorizontalAlignment = HorizontalAlignment.Center;
        RippleLayer1.VerticalAlignment = VerticalAlignment.Center;
        RippleLayer2.HorizontalAlignment = HorizontalAlignment.Center;
        RippleLayer2.VerticalAlignment = VerticalAlignment.Center;
        RippleLayer1.BorderThickness = new Thickness(ResolveRippleStrokeThickness(active));
        RippleLayer2.BorderThickness = new Thickness(ResolveRippleStrokeThickness(active));
        RippleLayer1.CornerRadius = cr;
        RippleLayer2.CornerRadius = cr;
        RippleLayer1.Clip = BuildRippleClip(active, w, h, cr);
        RippleLayer2.Clip = BuildRippleClip(active, w, h, cr);
    }

    private static Geometry BuildRippleClip(Border active, double w, double h, CornerRadius cornerRadius)
    {
        if (active.Name == "IdleDiamond")
        {
            var figure = new PathFigure { StartPoint = new Point(w / 2.0, 0), IsClosed = true, IsFilled = true };
            figure.Segments.Add(new LineSegment(new Point(w, h / 2.0), true));
            figure.Segments.Add(new LineSegment(new Point(w / 2.0, h), true));
            figure.Segments.Add(new LineSegment(new Point(0, h / 2.0), true));
            return new PathGeometry(new[] { figure });
        }

        if (active.Name == "IdleCircle")
            return new EllipseGeometry(new Point(w / 2.0, h / 2.0), w / 2.0, h / 2.0);

        if (cornerRadius.TopLeft > 0.01 || cornerRadius.TopRight > 0.01 || cornerRadius.BottomLeft > 0.01 || cornerRadius.BottomRight > 0.01)
            return new RectangleGeometry(new Rect(0, 0, w, h), cornerRadius.TopLeft, cornerRadius.TopLeft);

        return new RectangleGeometry(new Rect(0, 0, w, h));
    }

    private static double ResolveRippleStrokeThickness(Border active)
    {
        if (active.Name == "IdleCircle") return 1.35;
        if (active.Name == "IdleDiamond") return 1.25;
        if (active.Name == "IdleRoundedSquare") return 1.4;
        return 1.45;
    }

    private static double ResolveRippleStrokeThickness(WidgetIdleShape shape)
    {
        return shape switch
        {
            WidgetIdleShape.Circle => 1.35,
            WidgetIdleShape.Diamond => 1.25,
            WidgetIdleShape.RoundedSquare => 1.4,
            _ => 1.45
        };
    }

    private void AnimateExpandFadeIn()
    {
        try
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ExpandedContainer.BeginAnimation(OpacityProperty, fadeIn);
        }
        catch { }
    }

    private void AnimateMoveTo(double targetLeft, double targetTop, int durationMs)
    {
        try
        {
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // WPF Window.Left/Top don't support direct animation, so use a timer approach
            var startLeft = Left;
            var startTop = Top;
            var startTime = DateTime.UtcNow;

            var moveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
            moveTimer.Tick += (s, e) =>
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var progress = Math.Min(1.0, elapsed / durationMs);

                // Cubic ease out
                var eased = 1 - Math.Pow(1 - progress, 3);

                Left = startLeft + (targetLeft - startLeft) * eased;
                Top = startTop + (targetTop - startTop) * eased;

                if (progress >= 1.0)
                {
                    moveTimer.Stop();
                    Left = targetLeft;
                    Top = targetTop;
                }
            };
            moveTimer.Start();
        }
        catch { }
    }

    // ═══════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════

    private void ClampToWorkArea()
    {
        var workArea = GetTargetWorkArea();
        var (padX, padY) = GetCollapsedRipplePadding();
        var minLeft = workArea.Left - padX;
        var maxLeft = workArea.Right - Width + padX;
        var minTop = workArea.Top - padY;
        var maxTop = workArea.Bottom - Height + padY;

        if (Left < minLeft) Left = minLeft;
        if (Top < minTop) Top = minTop;
        if (Left > maxLeft) Left = maxLeft;
        if (Top > maxTop) Top = maxTop;
    }

    private (double PadX, double PadY) GetCollapsedRipplePadding()
    {
        if (_isExpanded || Config.IdleAnimation != WidgetIdleAnimation.Ripple)
            return (0, 0);
        if (EdgeDockSquare.Visibility == Visibility.Visible)
        {
            var (_, _, padX, padY) = GetEdgeDockSquareWindowMetrics();
            return (padX, padY);
        }

        var dims = GetCollapsedWindowMetrics();
        return (dims.PadX, dims.PadY);
    }

    /// <summary>
    /// Kích thước cửa sổ khi dock dạng ô vuông nhỏ; nếu idle animation là Ripple thì mở rộng
    /// giống <see cref="GetCollapsedWindowMetrics"/> để sóng không bị clip.
    /// </summary>
    private (double WindowWidth, double WindowHeight, double PadX, double PadY) GetEdgeDockSquareWindowMetrics()
    {
        var sq = Config.EdgeDockSquareSize;
        var coreWindowSize = sq + 8.0;
        var windowSize = coreWindowSize;

        if (Config.IdleAnimation == WidgetIdleAnimation.Ripple)
        {
            var rippleStroke = ResolveRippleStrokeThickness(WidgetIdleShape.Square);
            var rippleOuter = sq * 2.55 + rippleStroke * 2 + 8.0;
            windowSize = Math.Max(coreWindowSize, rippleOuter);
        }

        var pad = Math.Max(0, (windowSize - coreWindowSize) / 2.0);
        return (windowSize, windowSize, pad, pad);
    }

    private (double WindowWidth, double WindowHeight, double PadX, double PadY) GetCollapsedWindowMetrics()
    {
        var size = Config.IdleSize;
        var coreVisualSize = Config.IdleShape == WidgetIdleShape.Diamond ? size * 1.4 : size;
        var coreWindowSize = coreVisualSize + 8.0; // glow padding
        var windowSize = coreWindowSize;

        if (Config.IdleAnimation == WidgetIdleAnimation.Ripple)
        {
            var rippleBase = Config.IdleShape == WidgetIdleShape.Diamond ? size * 0.85 : size;
            var rippleStroke = ResolveRippleStrokeThickness(Config.IdleShape);
            var rippleOuter = rippleBase * 2.55 + (rippleStroke * 2) + 8.0;
            windowSize = Math.Max(coreWindowSize, rippleOuter);
        }

        var pad = Math.Max(0, (windowSize - coreWindowSize) / 2.0);
        return (windowSize, windowSize, pad, pad);
    }

    private void SavePosition()
    {
        Config.SavedX = Left;
        Config.SavedY = Top;
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowNode.Title))
        {
            Dispatcher.BeginInvoke(() =>
            {
                TitleText.Text = ResolveDisplayTitle(_node);
            });
        }

        if (_node is HtmlUiNode htmlNode)
        {
            if (e.PropertyName is nameof(HtmlUiNode.HtmlCode)
                or nameof(HtmlUiNode.CssCode)
                or nameof(HtmlUiNode.JsCode)
                or nameof(HtmlUiNode.ParamsCode)
                or nameof(HtmlUiNode.InputMappings)
                or nameof(HtmlUiNode.OfflineAssets))
            {
                _webViewContentLoaded = false;
                _lastContentSignature = null;
                if (_isExpanded && _webViewInitialized)
                    _ = ReloadContentAsync();
            }
            else if (e.PropertyName == nameof(HtmlUiNode.PendingAsyncDataPush) && htmlNode.PendingAsyncDataPush)
            {
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
#if DEBUG
                        System.Diagnostics.Debug.WriteLine(
                            $"[FloatingWidget:{htmlNode.Id}] PendingAsyncDataPush received. webViewInitialized={_webViewInitialized}, isExpanded={_isExpanded}");
#endif
                        if (_webViewInitialized)
                        {
                            var rounds = 0;
                            while (true)
                            {
                                DrainPendingAsyncQueueToBuffer(htmlNode);
                                // Ưu tiên widget realtime: runtime sẵn sàng là flush ngay, kể cả đang collapse/ẩn.
                                if (_htmlRuntimeReady)
                                {
                                    var flushedCount = await FlushBufferedAsyncDataToWidgetAsync(htmlNode);
#if DEBUG
                                    System.Diagnostics.Debug.WriteLine(
                                        $"[FloatingWidget:{htmlNode.Id}] Pending loop round={rounds}, flushedCount={flushedCount}, queueEmpty={htmlNode.PendingAsyncPushQueue.IsEmpty}, bufferCount={_pendingAsyncBuffer.Count}");
#endif
                                }

                                // Có thể có item mới enqueue đúng lúc đang flush; lặp thêm 1-2 vòng để không hụt item cuối.
                                if (htmlNode.PendingAsyncPushQueue.IsEmpty && _pendingAsyncBuffer.Count == 0) break;
                                if (++rounds >= 8) break;
                                await Task.Yield();
                            }
                        }
                        else
                        {
                            // WebView chưa sẵn sàng: vẫn phải giữ data để mở widget lại không bị mất.
                            DrainPendingAsyncQueueToBuffer(htmlNode);
                        }
                    }
                    catch { }
                    finally
                    {
                        htmlNode.PendingAsyncDataPush = false;
                        if (!htmlNode.PendingAsyncPushQueue.IsEmpty
                            || _hiddenAsyncBacklog.Count > 0
                            || _pendingAsyncBuffer.Count > 0)
                        {
                            QueueDeferredPendingFlush(htmlNode);
                        }
#if DEBUG
                        System.Diagnostics.Debug.WriteLine(
                            $"[FloatingWidget:{htmlNode.Id}] PendingAsyncDataPush reset to false.");
#endif
                    }
                }, DispatcherPriority.Normal);
            }
            else if (e.PropertyName == nameof(HtmlUiNode.PendingReadDom) && htmlNode.PendingReadDom)
            {
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        if (_webViewInitialized)
                        {
                            await UpdateOutputsFromDomAsync(htmlNode);
                        }
                    }
                    catch { }
                    finally
                    {
                        htmlNode.PendingReadDom = false;
                    }
                }, DispatcherPriority.Normal);
            }
        }
    }

    private static string ResolveDisplayTitle(WorkflowNode node)
    {
        var custom = node.FloatingWidget?.WidgetName;
        if (!string.IsNullOrWhiteSpace(custom)) return custom;
        return string.IsNullOrWhiteSpace(node.Title) ? "Widget" : node.Title;
    }

    // ═══════════════════════════════════════════
    //  CLEANUP
    // ═══════════════════════════════════════════

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        StopIdleTimer();
        StopTitleBarHideTimer();

        // Cleanup WebView2
        try
        {
            if (_webView?.CoreWebView2 != null)
                _webView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            _webView?.Dispose();
            _webView = null;
        }
        catch { }

        // Unsubscribe from node changes
        if (_node is INotifyPropertyChanged npc)
            npc.PropertyChanged -= Node_PropertyChanged;

        // Save config
        SavePosition();
    }

    protected override void OnClosed(EventArgs e)
    {
        DetachManualRunObservers();
        try
        {
            _titleRevealHost?.Hide();
            _titleRevealHost?.Close();
        }
        catch { }
        _titleRevealHost = null;
        _titleRevealActionsPanel = null;
        _titleRevealCloseButton = null;
        _titleRevealCollapseButton = null;
        _titleRevealStartButton = null;
        _titleRevealStopButton = null;
        _titleRevealStartBadge = null;
        _titleRevealStartBadgeText = null;
        _titleRevealStopBadge = null;
        _titleRevealStopBadgeText = null;
        _titleRevealStopSessionsPanel = null;
        _titleRevealStopSessionsPopup = null;
        _titleRevealOutsideCollapseToggleButton = null;
        _titleRevealPinToggleButton = null;

        base.OnClosed(e);
    }

    private void ReassertTopmostIfNeeded()
    {
        if (!Config.AlwaysOnTop) return;
        try
        {
            if (!Topmost) Topmost = true;
            // WPF đôi lúc mất z-order khi có window khác vừa chuyển trạng thái.
            // Toggle nhẹ để kéo widget về topmost layer ổn định hơn.
            Topmost = false;
            Topmost = true;
            SyncTitleRevealHostTopmost();
        }
        catch { }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyTaskbarAppIdBestEffort();
        try
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                EnsureTaskbarToggleWindowStyle(source.Handle);
                source.AddHook(WndProc);
            }
        }
        catch { }
    }

    private void ApplyTaskbarAppIdBestEffort()
    {
        if (!Config.ShowInTaskbar) return;
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            var workflowName = _host?.ViewModel?.CurrentWorkflowName ?? "Workflow";
            var widgetName = string.IsNullOrWhiteSpace(Config.WidgetName) ? _node.Title : Config.WidgetName;
            var safeWorkflow = SanitizeAppIdPart(workflowName);
            var safeNode = SanitizeAppIdPart(_node.Id);
            var safeWidget = SanitizeAppIdPart(widgetName);
            SetWindowAppId(hwnd, $"FlowMy.Widget.{safeWorkflow}.{safeNode}.{safeWidget}");
        }
        catch { }
    }

    private void ApplyTaskbarVisualIdentity()
    {
        try
        {
            var accentBrush = ResolveWidgetAccentBrush();
            var iconBrush = ResolveWidgetIconBrush();
            Icon = BuildTaskbarIcon(accentBrush, iconBrush, Config.TaskbarIconShape, Config.TaskbarIconSize);
        }
        catch { }
    }

    private Brush ResolveWidgetAccentBrush()
    {
        if (ParseHexBrush(Config.IdleBackgroundColor) is SolidColorBrush idleBackground && idleBackground.Color.A > 0)
            return idleBackground;

        if (_node.NodeBrush is SolidColorBrush nb && nb.Color.A > 0)
            return nb;

        if (!string.IsNullOrWhiteSpace(_node.ColorKey) && TryFindResource(_node.ColorKey) is SolidColorBrush rb)
            return rb;

        if (TryFindResource("PrimaryBrush") is SolidColorBrush pb)
            return pb;

        return Brushes.DodgerBlue;
    }

    private Brush ResolveWidgetIconBrush()
    {
        if (ParseHexBrush(Config.IdleForegroundColor) is SolidColorBrush idleForeground && idleForeground.Color.A > 0)
            return idleForeground;
        if (TryFindResource("TextOnPrimaryBrush") is SolidColorBrush textOnPrimary)
            return textOnPrimary;
        return Brushes.White;
    }

    private ImageSource BuildTaskbarIcon(Brush accentBrush, Brush iconBrush, WidgetIdleShape shape, double iconSize)
    {
        const int size = 64;
        var radius = Math.Max(8, Math.Min(28, iconSize));
        var center = new Point(size / 2d, size / 2d);
        var iconColor = iconBrush is SolidColorBrush iconSolid ? iconSolid.Color : Colors.White;
        var iconName = Config.IdleIconText;
        var iconDrawing = FlowMy.IconResources.IconExists(iconName)
            ? FlowMy.IconResources.GetSvgImage(iconName, iconColor)?.Drawing
            : null;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size), 0, 0);
            var pen = new Pen(iconBrush, 3);
            switch (shape)
            {
                case WidgetIdleShape.Square:
                {
                    var half = radius;
                    dc.DrawRectangle(accentBrush, pen, new Rect(center.X - half, center.Y - half, half * 2, half * 2));
                    break;
                }
                case WidgetIdleShape.RoundedSquare:
                {
                    var half = radius;
                    var rect = new Rect(center.X - half, center.Y - half, half * 2, half * 2);
                    var corner = Math.Max(4, half * 0.28);
                    dc.DrawRoundedRectangle(accentBrush, pen, rect, corner, corner);
                    break;
                }
                case WidgetIdleShape.Diamond:
                {
                    var half = radius;
                    var geo = new StreamGeometry();
                    using (var g = geo.Open())
                    {
                        g.BeginFigure(new Point(center.X, center.Y - half), true, true);
                        g.LineTo(new Point(center.X + half, center.Y), true, false);
                        g.LineTo(new Point(center.X, center.Y + half), true, false);
                        g.LineTo(new Point(center.X - half, center.Y), true, false);
                    }
                    geo.Freeze();
                    dc.DrawGeometry(accentBrush, pen, geo);
                    break;
                }
                default:
                    dc.DrawEllipse(accentBrush, pen, center, radius, radius);
                    break;
            }

            if (iconDrawing != null)
            {
                var iconSide = Math.Max(12, radius * 1.2);
                var iconRect = new Rect(center.X - iconSide / 2d, center.Y - iconSide / 2d, iconSide, iconSide);
                dc.DrawDrawing(new DrawingGroup
                {
                    Transform = new MatrixTransform(
                        iconRect.Width / Math.Max(1, iconDrawing.Bounds.Width), 0,
                        0, iconRect.Height / Math.Max(1, iconDrawing.Bounds.Height),
                        iconRect.X - iconDrawing.Bounds.X * (iconRect.Width / Math.Max(1, iconDrawing.Bounds.Width)),
                        iconRect.Y - iconDrawing.Bounds.Y * (iconRect.Height / Math.Max(1, iconDrawing.Bounds.Height))),
                    Children = { iconDrawing }
                });
            }
        }

        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;
    private const int SC_RESTORE = 0xF120;
    private static readonly TimeSpan TaskbarToggleDebounce = TimeSpan.FromMilliseconds(280);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        try
        {
            if (msg == WM_SYSCOMMAND)
            {
                int command = wParam.ToInt32() & 0xFFF0;
                // Taskbar click có thể đi qua SC_MINIMIZE hoặc SC_RESTORE tùy trạng thái cửa sổ.
                // Funnel cả 2 vào cùng một toggle path + debounce để tránh miss click.
                if (command == SC_MINIMIZE || command == SC_RESTORE)
                {
                    RequestTaskbarToggle();
                    handled = true;
                }
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    private void RequestTaskbarToggle()
    {
        if (_taskbarToggleQueued) return;

        var now = DateTime.UtcNow;
        if (now - _lastTaskbarToggleUtc < TaskbarToggleDebounce)
            return;

        _taskbarToggleQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _taskbarToggleQueued = false;
            var at = DateTime.UtcNow;
            if (at - _lastTaskbarToggleUtc < TaskbarToggleDebounce)
                return;

            _lastTaskbarToggleUtc = at;
            ToggleWidgetFromTaskbar();
        }), DispatcherPriority.Normal);
    }

    private void ToggleWidgetFromTaskbar()
    {
        // Taskbar chỉ là "remote button" cho logic mở/thu gốc.
        // Không để taskbar trở thành nguồn state riêng gây lệch side/reveal host.
        try
        {
            if (_isExpanded)
            {
                CollapseWidget();
            }
            else
            {
                if (_isSlideHidden)
                {
                    // Đang dock ẩn: lộ phần idle trước khi expand để chuyển trạng thái mượt.
                    RevealDockedWidgetFully(restoreOriginalShape: false);
                }
                ExpandWidget();
            }

            Activate();
            Focus();
        }
        catch { }
    }

    private const int GWL_STYLE = -16;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private static void EnsureTaskbarToggleWindowStyle(IntPtr hwnd)
    {
        try
        {
            var style = GetWindowLong(hwnd, GWL_STYLE);
            style |= WS_SYSMENU | WS_MINIMIZEBOX;
            SetWindowLong(hwnd, GWL_STYLE, style);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
        catch { }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    private static string SanitizeAppIdPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "x";
        var trimmed = value.Trim();
        var safe = Regex.Replace(trimmed, "[^A-Za-z0-9._-]+", "_");
        return string.IsNullOrWhiteSpace(safe) ? "x" : safe;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    private static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };

    private static void SetWindowAppId(IntPtr hwnd, string appId)
    {
        var iid = IID_IPropertyStore;
        var hr = SHGetPropertyStoreForWindow(hwnd, ref iid, out var store);
        if (hr != 0 || store == null) return;
        try
        {
            var key = PKEY_AppUserModel_ID;
            var pv = new PROPVARIANT { vt = 31, pwszVal = Marshal.StringToCoTaskMemUni(appId) };
            try
            {
                store.SetValue(ref key, ref pv);
                store.Commit();
            }
            finally
            {
                if (pv.pwszVal != IntPtr.Zero) Marshal.FreeCoTaskMem(pv.pwszVal);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pwszVal;
        public IntPtr padding;
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }
}
