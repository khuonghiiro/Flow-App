using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Workflow;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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
    private FloatingWidgetConfig Config => _node.FloatingWidget!;

    // ── State ──
    private bool _isExpanded;
    private bool _isSlideHidden;    // Widget đã trượt vào cạnh (ẩn 1 phần)
    private bool _isDragging;
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

    /// <summary>Mini toolbar ngoài widget (đóng + thu nhỏ) thay cho Popup.</summary>
    private Window? _titleRevealHost;
    private StackPanel? _titleRevealActionsPanel;
    private Button? _titleRevealCloseButton;
    private Button? _titleRevealCollapseButton;
    private Button? _titleRevealOutsideCollapseToggleButton;
    private Button? _titleRevealPinToggleButton;
    private bool _suppressOutsideCollapseOnce;

    // ── Slide animation state ──
    private double _slideOriginalLeft;
    private double _slideOriginalTop;

    public FloatingWidgetWindow(WorkflowNode node, IWorkflowEditorHost host)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        InitializeComponent();

        // Apply config to window
        Topmost = Config.AlwaysOnTop;
        ShowInTaskbar = Config.ShowInTaskbar;

        // Set title (ưu tiên WidgetName, fallback node.Title)
        TitleText.Text = ResolveDisplayTitle(node);

        // Window position
        WindowStartupLocation = WindowStartupLocation.Manual;
        Loaded += OnLoaded;
        Activated += (_, _) => UpdateOutsideCollapseToggleButtonState();
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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Apply idle shape
        ApplyIdleShape();

        // Set initial position
        SetInitialPosition();

        // Start in collapsed state (idle shape)
        ShowCollapsedState();

        // Start idle timer
        StartIdleTimer();

        // If HtmlUiNode, pre-initialize WebView2 in background so initial window show is not blocked.
        if (_node is HtmlUiNode)
        {
            _ = InitWebView2Async();
        }
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
            _ = ReloadContentAsync();
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
    }

    private void CollapseWidget()
    {
        if (!_isExpanded) return;

        // Save expanded size back to config theo đúng mode
        if (Config.UseRatioSize)
        {
            var area = GetTargetWorkArea();
            if (area.Width > 0) Config.WidthRatio = Math.Max(0.05, Math.Min(1.0, Width / area.Width));
            if (area.Height > 0) Config.HeightRatio = Math.Max(0.05, Math.Min(1.0, Height / area.Height));
        }
        else
        {
            Config.ExpandedWidth = Width;
            Config.ExpandedHeight = Height;
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
        row.Children.Add(outsideCollapseBtn);
        row.Children.Add(pinBtn);

        var panel = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
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
        _titleRevealOutsideCollapseToggleButton = outsideCollapseBtn;
        _titleRevealPinToggleButton = pinBtn;
        _titleRevealActionsPanel = row;
        _titleRevealHost = host;
        UpdateOutsideCollapseToggleButtonState();
        UpdatePinToggleButtonState();
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
        if (_titleRevealOutsideCollapseToggleButton != null) _titleRevealOutsideCollapseToggleButton.Margin = verticalDock ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 2, 0); // B2: mọi nút mới đều phải có margin theo orientation, nếu không ActualWidth/Height thay đổi bất định.
        if (_titleRevealPinToggleButton != null) _titleRevealPinToggleButton.Margin = verticalDock ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 2, 0); // B2: đồng bộ nút pin với nhóm để tránh chênh 1-2px sau khi thêm feature.
        _titleRevealActionsPanel?.UpdateLayout();

        const double sideGap = 5;      // yêu cầu UI: cách cạnh widget 5px ở case trái/phải
        const double topBottomGap = 3; // giữ margin nhỏ cho case top/bottom
        var btnW = _titleRevealActionsPanel?.ActualWidth ?? 0; // B4: ưu tiên đo từ panel thật (chứa toàn bộ nút) thay vì đo từng button/window để không bị sai do template/padding.
        var btnH = _titleRevealActionsPanel?.ActualHeight ?? 0; // B4: chiều cao thực sau layout là nguồn chuẩn cho neo trái/phải/top/bottom.
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
        if (!isOpen)
        {
            _titleRevealHost?.Hide();
            return;
        }

        if (!_isExpanded) return;

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
    }

    private void SyncTitleRevealHostTopmost()
    {
        if (_titleRevealHost != null)
            _titleRevealHost.Topmost = Topmost;
    }

    private void UpdateOutsideCollapseToggleButtonState()
    {
        if (_titleRevealOutsideCollapseToggleButton == null) return;
        var enabled = Config.CollapseWhenClickOutsideExpanded && !Config.PinnedNoAutoHide;
        _titleRevealOutsideCollapseToggleButton.Content = enabled ? "◉" : "◌";
        _titleRevealOutsideCollapseToggleButton.ToolTip = enabled
            ? "Đang bật: click ra ngoài sẽ tự thu nhỏ"
            : (Config.PinnedNoAutoHide
                ? "Đang tắt do chế độ ghim"
                : "Đang tắt: click ra ngoài không tự thu nhỏ");
        _titleRevealOutsideCollapseToggleButton.IsEnabled = !Config.PinnedNoAutoHide;

        var styleKey = enabled ? "PrimaryButton" : "SecondaryButton";
        if (TryFindResource(styleKey) is Style style)
            _titleRevealOutsideCollapseToggleButton.Style = style;
    }

    private void UpdatePinToggleButtonState()
    {
        if (_titleRevealPinToggleButton == null) return;
        var pinned = Config.PinnedNoAutoHide;
        _titleRevealPinToggleButton.Content = pinned ? "📍" : "📌";
        _titleRevealPinToggleButton.ToolTip = pinned
            ? "Đang ghim: không tự ẩn theo thời gian"
            : "Bật ghim: không tự ẩn theo thời gian";

        var styleKey = pinned ? "PrimaryButton" : "SecondaryButton";
        if (TryFindResource(styleKey) is Style style)
            _titleRevealPinToggleButton.Style = style;
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

                // Handle web messages from HTML (acSubmit, acStartWorkflow)
                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                _webViewInitialized = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FloatingWidget] WebView2 init error: {ex.Message}");
        }
    }

    private async Task ReloadContentAsync()
    {
        if (!_webViewInitialized || _webView?.CoreWebView2 == null) return;

        try
        {
            if (_node is HtmlUiNode htmlNode)
            {
                var html = BuildHtmlForWidget(htmlNode);

                // Inject bridge JS
                var bridgeJs = BuildBridgeJs();
                if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                    html = html.Replace("</body>", bridgeJs + "\n</body>", StringComparison.OrdinalIgnoreCase);
                else
                    html += bridgeJs;

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
        }
        catch { }
    }

    private string BuildHtmlForWidget(HtmlUiNode htmlNode)
    {
        var html = htmlNode.HtmlCode ?? "<!DOCTYPE html><html><body><div>Widget</div></body></html>";
        var css = htmlNode.CssCode ?? string.Empty;
        var js = htmlNode.JsCode ?? string.Empty;

        // Resolve input values
        var inputValues = ResolveInputValues(htmlNode);

        // Replace variables
        html = ReplaceVariables(html, inputValues);
        css = ReplaceVariables(css, inputValues);
        js = ReplaceVariables(js, inputValues);

        // Ensure <head>
        if (!html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
            html = html.Replace("<html>", "<html>\n<head>\n<meta charset=\"UTF-8\">\n</head>", StringComparison.OrdinalIgnoreCase);

        // Inject CSS
        if (!string.IsNullOrWhiteSpace(css))
        {
            var cssTag = $"\n<style>\n{css}\n</style>";
            if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</head>", cssTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
        }

        // Inject JS
        if (!string.IsNullOrWhiteSpace(js))
        {
            var jsTag = $"\n<script>\n{js}\n</script>";
            if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</body>", jsTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
            else
                html += jsTag;
        }

        return html;
    }

    private string BuildBridgeJs()
    {
        return @"
<script>
// ── Widget Bridge JS ──
function acSubmit() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ __widgetAction: 'submit' });
    }
}
function acStartWorkflow() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ __widgetAction: 'startWorkflow' });
    }
}
// Override __ac if needed
window.__ac = window.__ac || {};
window.__ac.submit = acSubmit;
window.__ac.startWorkflow = acStartWorkflow;
</script>";
    }

    private Dictionary<string, string> ResolveInputValues(HtmlUiNode htmlNode)
    {
        var result = new Dictionary<string, string>();
        if (_host?.ViewModel == null) return result;

        var mappings = htmlNode.InputMappings ?? new List<CodeInputMapping>();
        var allNodes = _host.ViewModel.Nodes;

        foreach (var m in mappings)
        {
            WorkflowNode? sourceNode = null;
            if (!string.IsNullOrWhiteSpace(m.SourceNodeId))
            {
                sourceNode = allNodes?.FirstOrDefault(n =>
                    string.Equals(n.Id, m.SourceNodeId, StringComparison.OrdinalIgnoreCase));
            }

            string inputValue = string.Empty;
            if (sourceNode != null)
            {
                var key = string.IsNullOrWhiteSpace(m.SourceOutputKey) ? null : m.SourceOutputKey.Trim();
                if (string.IsNullOrWhiteSpace(key) && sourceNode.DynamicOutputs?.Count > 0)
                    key = sourceNode.DynamicOutputs[0].Key ?? "output";

                inputValue = Services.Rendering.NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key ?? "output");
                if (string.Equals(inputValue?.Trim(), "—", StringComparison.OrdinalIgnoreCase))
                    inputValue = string.Empty;
            }

            var varName = m.EffectiveInputKey;
            if (string.IsNullOrWhiteSpace(varName)) varName = "input";
            result[varName] = inputValue ?? string.Empty;
        }

        return result;
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
                        HandleStartWorkflow();
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
            // Trigger read DOM via PendingReadDom
            htmlNode.PendingReadDom = true;
        }
    }

    private void HandleStartWorkflow()
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                _host?.ViewModel?.StartTestCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FloatingWidget] StartWorkflow error: {ex.Message}");
            }
        });
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
            var scale = new DoubleAnimation(1.0, 1.1, TimeSpan.FromMilliseconds(380))
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
        _titleRevealOutsideCollapseToggleButton = null;
        _titleRevealPinToggleButton = null;

        base.OnClosed(e);
    }
}
