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
    private Point _dragStartPoint;
    private double _dragStartLeft;
    private double _dragStartTop;
    private DateTime _lastActivityUtc = DateTime.UtcNow;

    // ── Timers ──
    private DispatcherTimer? _idleTimer;
    private DispatcherTimer? _titleBarHideTimer;

    // ── WebView2 ──
    private WebView2? _webView;
    private bool _webViewInitialized;

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

        // Set title
        TitleText.Text = string.IsNullOrWhiteSpace(node.Title) ? "Widget" : node.Title;

        // Window position
        WindowStartupLocation = WindowStartupLocation.Manual;
        Loaded += OnLoaded;

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

        // If HtmlUiNode, pre-initialize WebView2
        if (_node is HtmlUiNode)
        {
            await InitWebView2Async();
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
        var icon = Config.IdleIconText;

        // Hide all shapes first
        IdleCircle.Visibility = Visibility.Collapsed;
        IdleDiamond.Visibility = Visibility.Collapsed;
        IdleSquare.Visibility = Visibility.Collapsed;
        IdleRoundedSquare.Visibility = Visibility.Collapsed;

        switch (Config.IdleShape)
        {
            case WidgetIdleShape.Circle:
                IdleCircle.Width = size;
                IdleCircle.Height = size;
                IdleCircle.CornerRadius = new CornerRadius(size / 2);
                IdleIcon.Text = icon;
                IdleIcon.FontSize = size * 0.42;
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
    }

    // ═══════════════════════════════════════════
    //  STATE TRANSITIONS
    // ═══════════════════════════════════════════

    private void ShowCollapsedState()
    {
        _isExpanded = false;

        // Size
        var size = Config.IdleSize;
        // Add padding for diamond rotation
        var windowSize = Config.IdleShape == WidgetIdleShape.Diamond ? size * 1.4 : size;
        Width = windowSize + 8;  // padding for glow effect
        Height = windowSize + 8;

        IdleContainer.Visibility = Visibility.Visible;
        ExpandedContainer.Visibility = Visibility.Collapsed;

        // Hide WebView2 to save resources
        if (_webView != null)
            _webView.Visibility = Visibility.Collapsed;

        // Pulse animation on idle shape
        AnimateIdlePulse();
    }

    public void ExpandWidget()
    {
        if (_isExpanded) return;

        // If slide hidden, restore position first
        if (_isSlideHidden) RestoreFromSlide(animate: false);

        _isExpanded = true;
        MarkActivity();

        // Size
        Width = Config.ExpandedWidth;
        Height = Config.ExpandedHeight;

        IdleContainer.Visibility = Visibility.Collapsed;
        ExpandedContainer.Visibility = Visibility.Visible;

        // Show/hide resize grip
        ResizeGrip.Visibility = Config.AllowResize ? Visibility.Visible : Visibility.Collapsed;

        // Show/hide title bar
        TitleBar.Visibility = Config.ShowTitleBar ? Visibility.Visible : Visibility.Collapsed;

        // Start title bar auto-hide timer
        if (Config.AutoHideTitleBar && Config.ShowTitleBar)
            StartTitleBarHideTimer();

        // Show WebView2
        if (_webView != null)
        {
            _webView.Visibility = Visibility.Visible;
            _ = ReloadContentAsync();
        }

        // Clamp to work area
        ClampToWorkArea();

        // Fade-in animation
        AnimateExpandFadeIn();
    }

    private void CollapseWidget()
    {
        if (!_isExpanded) return;

        // Save expanded size back to config
        Config.ExpandedWidth = Width;
        Config.ExpandedHeight = Height;

        ShowCollapsedState();

        // If snap-to-edge is enabled, snap after collapse
        if (Config.SnapToEdge)
            SnapToNearestEdge();
    }

    // ═══════════════════════════════════════════
    //  DRAG & MOVE
    // ═══════════════════════════════════════════

    private void StartDrag(MouseButtonEventArgs e)
    {
        if (Config.LockPosition || !Config.AllowDrag) return;

        _isDragging = true;
        _dragStartPoint = PointToScreen(e.GetPosition(this));
        _dragStartLeft = Left;
        _dragStartTop = Top;
        ((UIElement)e.Source).CaptureMouse();

        // If slide hidden, restore during drag
        if (_isSlideHidden)
            RestoreFromSlide(animate: false);
    }

    private void ContinueDrag(MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = PointToScreen(e.GetPosition(this));
        Left = _dragStartLeft + (currentPoint.X - _dragStartPoint.X);
        Top = _dragStartTop + (currentPoint.Y - _dragStartPoint.Y);
    }

    private void EndDrag(MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ((UIElement)e.Source).ReleaseMouseCapture();

        ClampToWorkArea();

        // Snap to edge
        if (Config.SnapToEdge && !_isExpanded)
            SnapToNearestEdge();

        // Save position
        SavePosition();
        MarkActivity();
    }

    // Idle shape drag handlers
    private void Idle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double click → expand
            ExpandWidget();
            e.Handled = true;
            return;
        }
        StartDrag(e);
    }

    private void Idle_MouseMove(object sender, MouseEventArgs e) => ContinueDrag(e);
    private void Idle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            // Single click (no drag) → expand
            ExpandWidget();
        }
        EndDrag(e);
    }

    private void Idle_MouseEnter(object sender, MouseEventArgs e)
    {
        MarkActivity();
        if (_isSlideHidden)
            RestoreFromSlide(animate: true);
    }

    // Title bar drag handlers
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            CollapseWidget();
            e.Handled = true;
            return;
        }
        StartDrag(e);
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e) => ContinueDrag(e);
    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndDrag(e);

    private void TitleBar_MouseEnter(object sender, MouseEventArgs e)
    {
        MarkActivity();
        // Show title bar if hidden
        if (TitleBar.Opacity < 1)
        {
            var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
            TitleBar.BeginAnimation(OpacityProperty, anim);
        }
        RestartTitleBarHideTimer();
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

        // Calculate distances to each edge
        var distLeft = cx - workArea.Left;
        var distRight = workArea.Right - cx;
        var distTop = cy - workArea.Top;
        var distBottom = workArea.Bottom - cy;

        var minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        var targetLeft = Left;
        var targetTop = Top;

        if (Math.Abs(minDist - distLeft) < 0.01)
            targetLeft = workArea.Left + margin;
        else if (Math.Abs(minDist - distRight) < 0.01)
            targetLeft = workArea.Right - Width - margin;
        else if (Math.Abs(minDist - distTop) < 0.01)
            targetTop = workArea.Top + margin;
        else
            targetTop = workArea.Bottom - Height - margin;

        // Animate snap
        AnimateMoveTo(targetLeft, targetTop, 200);
    }

    // ═══════════════════════════════════════════
    //  SLIDE TO EDGE (IDLE HIDE)
    // ═══════════════════════════════════════════

    private void SlideToEdge()
    {
        if (_isSlideHidden || _isExpanded) return;

        var workArea = GetTargetWorkArea();
        var cx = Left + Width / 2;
        var hideAmount = Width * Config.SlideHidePercent;

        _slideOriginalLeft = Left;
        _slideOriginalTop = Top;
        _isSlideHidden = true;

        double targetLeft;
        if (cx < workArea.Left + workArea.Width / 2)
        {
            // Slide to left edge
            targetLeft = workArea.Left - hideAmount;
        }
        else
        {
            // Slide to right edge
            targetLeft = workArea.Right - Width + hideAmount;
        }

        AnimateMoveTo(targetLeft, Top, 350);
    }

    private void RestoreFromSlide(bool animate)
    {
        if (!_isSlideHidden) return;
        _isSlideHidden = false;

        if (animate)
            AnimateMoveTo(_slideOriginalLeft, _slideOriginalTop, 250);
        else
        {
            Left = _slideOriginalLeft;
            Top = _slideOriginalTop;
        }
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
        _titleBarHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Config.TitleBarHideTimeoutSeconds)
        };
        _titleBarHideTimer.Tick += (s, e) =>
        {
            StopTitleBarHideTimer();
            if (_isExpanded && Config.AutoHideTitleBar)
            {
                // Fade out title bar
                var anim = new DoubleAnimation(0.15, TimeSpan.FromMilliseconds(500));
                TitleBar.BeginAnimation(OpacityProperty, anim);
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

    // ═══════════════════════════════════════════
    //  RESIZE
    // ═══════════════════════════════════════════

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!Config.AllowResize) return;

        var newW = Math.Max(Config.MinExpandedWidth, Math.Min(Config.MaxExpandedWidth, Width + e.HorizontalChange));
        var newH = Math.Max(Config.MinExpandedHeight, Math.Min(Config.MaxExpandedHeight, Height + e.VerticalChange));

        Width = newW;
        Height = newH;

        MarkActivity();
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

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        // Save position
        SavePosition();
        Close();
    }

    // ═══════════════════════════════════════════
    //  ANIMATIONS
    // ═══════════════════════════════════════════

    private void AnimateIdlePulse()
    {
        try
        {
            var scaleUp = new DoubleAnimation(1.0, 1.08, TimeSpan.FromMilliseconds(800))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            IdleScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUp);
            IdleScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUp);
        }
        catch { }
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
        if (Left < workArea.Left) Left = workArea.Left;
        if (Top < workArea.Top) Top = workArea.Top;
        if (Left + Width > workArea.Right) Left = workArea.Right - Width;
        if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
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
                TitleText.Text = string.IsNullOrWhiteSpace(_node.Title) ? "Widget" : _node.Title;
            });
        }
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
}
