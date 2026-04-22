using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utils;
using FlowMy.Services.Workflow;
using FlowMy.Views.Overlays;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public static class HtmlUiNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50;
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();
        /// <summary>Lưu X,Y,W,H trước khi phóng to khung nhìn — mỗi border một mục.</summary>
        private static readonly System.Collections.Generic.Dictionary<Border, (double x, double y, double w, double h)> _viewportExpandRestore = new();
        private static readonly FontFamily ViewportExpandIconFont = new("Segoe MDL2 Assets");
        // ✅ Throttling cho SyncWebViewPosition để tránh gọi quá nhiều lần khi drag node
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _webViewSyncTimers = new();
        private const int WebViewSyncThrottleMs = 16; // ~60fps để mượt mà khi drag
        // Tránh init ồ ạt nhiều WebView2 cùng lúc gây đơ khi load workflow lớn.
        private static readonly System.Threading.SemaphoreSlim _webView2InitGate = new(1, 1);

        /// <summary>Thư mục gợi ý để resolve tên file video (không có đường dẫn đầy đủ) an toàn trong resolve_playable_ref.</summary>
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
                catch { /* ignore */ }
            }

            TryAdd(Environment.CurrentDirectory);
            try
            {
                var cur = Path.GetFullPath(Environment.CurrentDirectory);
                var p1 = Directory.GetParent(cur)?.FullName;
                TryAdd(p1);
                if (!string.IsNullOrEmpty(p1))
                    TryAdd(Directory.GetParent(p1)?.FullName);
            }
            catch { /* ignore */ }

            TryAdd(AppContext.BaseDirectory);

            try
            {
                var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                TryAdd(Path.Combine(profile, "Downloads"));
                TryAdd(Path.Combine(profile, "Downloads", "Workflow_Downloads"));
                TryAdd(Path.Combine(profile, "Downloads", "Workflow_Downloads", "Videos"));
                TryAdd(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            }
            catch { /* ignore */ }

            try
            {
                var envExtra = Environment.GetEnvironmentVariable("FlowMy_MEDIA_SEARCH_EXTRA");
                if (!string.IsNullOrWhiteSpace(envExtra))
                {
                    foreach (var part in envExtra.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        TryAdd(part);
                }
            }
            catch { /* ignore */ }

            return JsonSerializer.Serialize(set.ToList());
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

        public static Border CreateBorder(HtmlUiNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var border = new Border
            {
                // Đảm bảo Width/Height luôn hợp lệ để tránh lỗi HwndHost khi chuyển workflow giữa các máy
                Width = Math.Max(600, node.Width),
                Height = Math.Max(600, node.Height),
                MinWidth = 600,
                MinHeight = 600,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node,
                CacheMode = null
            };

            GpuOptimizationHelper.ApplyToBorder(border);

            var grid = new Grid();
            // Top: Auto, Middle: *, Bottom: Auto — để WebView2 dãn tối đa, top/bottom chỉ cao theo nội dung.
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });               // Top: auto
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Middle (WebView2): *
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });               // Bottom: auto

            GpuOptimizationHelper.ApplyToElement(grid);

            var webView = new WebView2
            {
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(webView, 1);

            // ✅ Khai báo sớm để các lambda/closure trong PropertyChanged có thể capture
            WebView2? _webViewTab1 = null;
            bool _isTab1WebViewVisible = false; // track whether Tab1 WebView2 should be showing
            TabControl? _tabControl = null;
            TextBox? _addressBar = null;
            TextBlock? topBarText = null;          // gán sau khi tạo TextBlock
            Grid? topBarChromeRow = null;          // hàng tiêu đề + nút phóng to (chung LayoutTransform)
            DispatcherTimer? _webTabAutoRefreshTimer = null; // gán khi start auto-refresh
            Border? bottomBarCapture = null;       // gán sau khi bottomBar tạo xong, capture vào viewportExpandBtn.Click
            DispatcherTimer? _topBarStatusResetTimer = null;
            bool _tab2ProcessFailed = false;
            bool _tab1ProcessFailed = false;

            CoreWebView2? TryGetCoreSafe(WebView2? target)
            {
                if (target == null) return null;
                try
                {
                    return target.CoreWebView2;
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }

            void AttachProcessFailedHandler(WebView2 target, bool isTab1)
            {
                var core = TryGetCoreSafe(target);
                if (core == null) return;

                core.ProcessFailed += (_, args) =>
                {
                    if (isTab1) _tab1ProcessFailed = true;
                    else _tab2ProcessFailed = true;

                    try
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[HtmlUiNode] WebView2 process failed (tab={(isTab1 ? "1" : "2")}): {args.ProcessFailedKind}");
                    }
                    catch { }

                    try
                    {
                        target.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { target.Visibility = Visibility.Collapsed; } catch { }
                        }), DispatcherPriority.Normal);
                    }
                    catch { }
                };
            }

            // ✅ Cache HTML content để tránh rebuild mỗi lần (tối ưu performance)
            string? _cachedHtmlContent = null;
            string? _cachedHtmlCode = null;
            string? _cachedCssCode = null;
            string? _cachedJsCode = null;
            string? _cachedOfflineAssetsKey = null;  // cache key cho offline assets
            System.Collections.Generic.Dictionary<string, string>? _cachedInputValues = null;
            /// <summary>File xem trước Tab2 khi HTML quá lớn (WebView2 giới hạn ~2MB cho data:/NavigateToString).</summary>
            string? _tab2PreviewTempFile = null;
            // Host map theo từng thư mục để tránh remap đè nhau (localfiles.local chỉ map được 1 folder/lần).
            var _localHostByFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Reverse map: host → folder, dùng bởi WebResourceRequested để serve file trực tiếp.
            var _localFolderByHost = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // WebResourceRequested có thể chạy thread khác; Dictionary không thread-safe → lock khi đọc/ghi.
            var _localHostMapSync = new object();

            static string BuildLocalHostForFolder(string folderPath)
            {
                var key = (folderPath ?? string.Empty).Trim().ToLowerInvariant();
                var hash = Math.Abs(key.GetHashCode()).ToString("x8");
                return $"localfiles-{hash}.local";
            }

            async Task<string> EnsureLocalHostMappingAsync(string folderPath)
            {
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
                        // Reverse map: WebResourceRequested dùng để serve file khi mapping chưa propagate
                        _localFolderByHost[localHost] = fullFolder;
                    }
                }

                bool mappingOk = false;
                string mappingErr = string.Empty;
                await webView.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var c = TryGetCoreSafe(webView);
                        if (c == null)
                        {
                            mappingErr = "CoreWebView2 not initialized.";
                            return;
                        }
                        c.SetVirtualHostNameToFolderMapping(
                            localHost,
                            fullFolder,
                            CoreWebView2HostResourceAccessKind.Allow);
                        mappingOk = true;
                    }
                    catch (Exception exMap)
                    {
                        mappingErr = exMap.Message;
                    }
                });

                if (!mappingOk)
                    throw new InvalidOperationException($"Virtual host mapping failed: {mappingErr}");


                return localHost;
            }

            // ✅ Auto-refresh timers: mỗi input mapping được bật auto-refresh sẽ có 1 DispatcherTimer riêng
            var _autoRefreshTimers = new System.Collections.Generic.Dictionary<string, DispatcherTimer>();
            DispatcherTimer? _sleepModeTimer = null;
            var _isSleepModeActive = false;

            static int CalcSleepIdleMs(HtmlUiNode n)
            {
                var val = Math.Max(1, n.SleepIdleTimeoutValue);
                var unit = (n.SleepIdleTimeoutUnit ?? "s").Trim();
                return unit switch
                {
                    "ms" => Math.Max(50, val),
                    "min" or "phút" => Math.Max(1, val) * 60000,
                    _ => Math.Max(1, val) * 1000
                };
            }

            void StopSleepModeTimer()
            {
                _sleepModeTimer?.Stop();
                _sleepModeTimer = null;
            }

            // Track last UI/flow activity to determine "idle"
            var lastActivityUtc = DateTime.UtcNow;
            void MarkActivity()
            {
                lastActivityUtc = DateTime.UtcNow;
                if (node.EnableSleepMode && _isSleepModeActive)
                {
                    _ = webView.Dispatcher.BeginInvoke(new Action(async () => await WakeRuntimeAsync()), DispatcherPriority.Background);
                }
            }

            async Task EnterSleepModeAsync()
            {
                if (!node.EnableSleepMode || _isSleepModeActive) return;
                if (node.PendingReadDom || node.PendingAsyncDataPush) return;

                _isSleepModeActive = true;
                StopSleepModeTimer();

                try
                {
                    StopAutoRefreshTimers();
                    StopWebTabAutoRefreshTimer();
                }
                catch { }

                try
                {
                    var core = TryGetCoreSafe(webView);
                    if (core != null)
                        core.Navigate("about:blank");
                }
                catch { }

                try
                {
                    var tab1Core = TryGetCoreSafe(_webViewTab1);
                    if (tab1Core != null)
                        tab1Core.Navigate("about:blank");
                }
                catch { }

                try { webView.Visibility = Visibility.Collapsed; } catch { }
                try { if (_webViewTab1 != null) _webViewTab1.Visibility = Visibility.Collapsed; } catch { }
            }

            async Task WakeRuntimeAsync()
            {
                if (!node.EnableSleepMode)
                {
                    _isSleepModeActive = false;
                    return;
                }

                StopSleepModeTimer();
                _isSleepModeActive = false;

                try { webView.Visibility = Visibility.Visible; } catch { }
                try { if (_webViewTab1 != null) _webViewTab1.Visibility = Visibility.Visible; } catch { }

                await ReloadHtmlAsync();

                try
                {
                    var tab1Core = TryGetCoreSafe(_webViewTab1);
                    if (tab1Core != null && node.UseWebTab)
                    {
                        var currentUrl = tab1Core.Source;
                        if (string.IsNullOrWhiteSpace(currentUrl) || string.Equals(currentUrl, "about:blank", StringComparison.OrdinalIgnoreCase))
                        {
                            var targetUrl = node.WebTabUrl;
                            if (!string.IsNullOrWhiteSpace(targetUrl))
                                tab1Core.Navigate(targetUrl);
                        }
                    }
                }
                catch { }

                StartAutoRefreshTimers();
                RestartWebTabAutoRefreshTimer();
                RestartSleepModeTimer();
            }

            void RestartSleepModeTimer()
            {
                if (!node.EnableSleepMode) return;

                StopSleepModeTimer();
                _sleepModeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                _sleepModeTimer.Tick += async (_, _) =>
                {
                    try
                    {
                        if (!node.EnableSleepMode) { StopSleepModeTimer(); return; }
                        var idleMs = CalcSleepIdleMs(node);
                        var idleFor = (DateTime.UtcNow - lastActivityUtc).TotalMilliseconds;
                        if (idleFor >= idleMs)
                            await EnterSleepModeAsync();
                    }
                    catch { }
                };
                _sleepModeTimer.Start();
            }

            // ✅ Sequential task chain cho JS injection Tab2→Tab1 (__tab1_exec_seq)
            Task _tab1SeqTask = Task.CompletedTask;

            string GetTopBarBaseTitle() => node.UseWebTab ? "Web + HTML UI" : "HTML UI";
            void SetTopBarStatus(string statusText, bool autoReset = false, int resetMs = 1200)
            {
                try
                {
                    if (topBarText == null) return;
                    var composed = string.IsNullOrWhiteSpace(statusText)
                        ? GetTopBarBaseTitle()
                        : $"{GetTopBarBaseTitle()} · {statusText}";
                    topBarText.Text = composed;

                    if (_topBarStatusResetTimer != null)
                    {
                        _topBarStatusResetTimer.Stop();
                        _topBarStatusResetTimer = null;
                    }

                    if (autoReset)
                    {
                        _topBarStatusResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(resetMs) };
                        _topBarStatusResetTimer.Tick += (_, _) =>
                        {
                            try
                            {
                                if (_topBarStatusResetTimer != null)
                                {
                                    _topBarStatusResetTimer.Stop();
                                    _topBarStatusResetTimer = null;
                                }
                                if (topBarText != null) topBarText.Text = GetTopBarBaseTitle();
                            }
                            catch { }
                        };
                        _topBarStatusResetTimer.Start();
                    }
                }
                catch { }
            }

            async Task EnsureCoreWebView2ThrottledAsync(WebView2 target, CoreWebView2Environment env)
            {
                await _webView2InitGate.WaitAsync();
                try
                {
                    await target.EnsureCoreWebView2Async(env);
                }
                finally
                {
                    _webView2InitGate.Release();
                }
            }

            // Helper: resolve giá trị của 1 mapping cụ thể (không dùng cache)
            string ResolveSingleInputValue(CodeInputMapping mapping)
            {
                if (host?.ViewModel == null) return string.Empty;
                var allNodes = host.ViewModel.Nodes;
                var connections = host.ViewModel.Connections;
                WorkflowNode? sourceNode = null;
                if (!string.IsNullOrWhiteSpace(mapping.SourceNodeId))
                {
                    sourceNode = allNodes?.FirstOrDefault(n =>
                        string.Equals(n.Id, mapping.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                    if (sourceNode == null && connections != null)
                    {
                        var conn = connections.FirstOrDefault(c =>
                            c.ToNode == node && c.FromNode != null &&
                            string.Equals(c.FromNode.Id, mapping.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                        sourceNode = conn?.FromNode;
                    }
                }
                if (sourceNode == null) return string.Empty;
                var key = string.IsNullOrWhiteSpace(mapping.SourceOutputKey) ? null : mapping.SourceOutputKey.Trim();
                if (string.IsNullOrWhiteSpace(key) && sourceNode.DynamicOutputs?.Count > 0)
                    key = sourceNode.DynamicOutputs[0].Key ?? "output";
                var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key ?? "output");
                if (string.Equals(value?.Trim(), "—", StringComparison.OrdinalIgnoreCase)) value = string.Empty;
                return value ?? string.Empty;
            }

            // Dừng tất cả auto-refresh timers
            void StopAutoRefreshTimers()
            {
                foreach (var t in _autoRefreshTimers.Values) t.Stop();
                _autoRefreshTimers.Clear();
            }

            // Khởi động auto-refresh timers từ InputMappings hiện tại
            void StartAutoRefreshTimers()
            {
                StopAutoRefreshTimers();
                try { if (webView.CoreWebView2 == null) return; }
                catch (ObjectDisposedException) { return; }
                catch { return; }
                var mappings = node.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
                foreach (var m in mappings)
                {
                    if (!m.AutoRefreshEnabled) continue;
                    var intervalMs = m.AutoRefreshUnit switch
                    {
                        "s" => m.AutoRefreshInterval * 1000,
                        "min" => m.AutoRefreshInterval * 60000,
                        _ => m.AutoRefreshInterval  // "ms"
                    };
                    intervalMs = Math.Max(100, intervalMs); // tối thiểu 100ms
                    var mapping = m; // capture
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
                    timer.Tick += async (s2, _) =>
                    {
                        CoreWebView2? core2;
                        try { core2 = webView.CoreWebView2; }
                        catch (ObjectDisposedException)
                        {
                            // WebView2 đã bị dispose → dừng timer ngay, không crash
                            (s2 as DispatcherTimer)?.Stop();
                            return;
                        }
                        if (core2 == null) return;
                        try
                        {
                            var value = ResolveSingleInputValue(mapping);
                            var jsKey = System.Text.Json.JsonSerializer.Serialize(mapping.EffectiveInputKey);
                            var jsVal = System.Text.Json.JsonSerializer.Serialize(value);
                            await core2.ExecuteScriptAsync(
                                $"if(typeof window.__acPush==='function') window.__acPush({jsKey},{jsVal});");
                        }
                        catch (ObjectDisposedException)
                        {
                            (s2 as DispatcherTimer)?.Stop();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"HTML UI auto-refresh push error: {ex.Message}");
                        }
                    };
                    timer.Start();
                    _autoRefreshTimers[m.EffectiveInputKey] = timer;
                }
            }

            // Function để resolve input values từ mappings (giống CodeNodeExecutor)
            System.Collections.Generic.Dictionary<string, string> ResolveInputValues()
            {
                var result = new System.Collections.Generic.Dictionary<string, string>();

                if (host?.ViewModel == null) return result;

                var mappings = node.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
                if (mappings.Count == 0) return result;

                var connections = host.ViewModel.Connections;
                var allNodes = host.ViewModel.Nodes;

                foreach (var m in mappings)
                {
                    WorkflowNode? sourceNode = null;

                    // Tìm source node từ SourceNodeId
                    if (!string.IsNullOrWhiteSpace(m.SourceNodeId))
                    {
                        sourceNode = allNodes?.FirstOrDefault(n =>
                            string.Equals(n.Id, m.SourceNodeId, StringComparison.OrdinalIgnoreCase));

                        if (sourceNode == null && connections != null)
                        {
                            var conn = connections.FirstOrDefault(c =>
                                c.ToNode == node && c.FromNode != null &&
                                string.Equals(c.FromNode.Id, m.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                            sourceNode = conn?.FromNode;
                        }
                    }

                    string inputValue = string.Empty;
                    if (sourceNode != null)
                    {
                        var key = string.IsNullOrWhiteSpace(m.SourceOutputKey) ? null : m.SourceOutputKey.Trim();
                        // Khi SourceOutputKey trống: dùng key đầu tiên của DynamicOutputs
                        if (string.IsNullOrWhiteSpace(key) && sourceNode.DynamicOutputs != null && sourceNode.DynamicOutputs.Count > 0)
                            key = sourceNode.DynamicOutputs[0].Key ?? "output";
                        inputValue = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key ?? "output");
                        // "—" là placeholder khi không có giá trị → dùng chuỗi rỗng
                        if (string.Equals(inputValue?.Trim(), "—", StringComparison.OrdinalIgnoreCase))
                            inputValue = string.Empty;
                    }

                    var varName = m.EffectiveInputKey;
                    if (string.IsNullOrWhiteSpace(varName)) varName = "input";

                    result[varName] = inputValue ?? string.Empty;
                }

                return result;
            }

            // Function để replace {variableName} trong text với resolved values
            string ReplaceVariables(string text, System.Collections.Generic.Dictionary<string, string> variableValues)
            {
                if (string.IsNullOrEmpty(text) || variableValues.Count == 0) return text;

                // Dùng regex để tìm {variableName} và replace với giá trị
                var regex = new System.Text.RegularExpressions.Regex(@"\{([^}]+)\}");
                return regex.Replace(text, match =>
                {
                    var variableName = match.Groups[1].Value.Trim();
                    if (variableValues.TryGetValue(variableName, out var value) && value != null)
                    {
                        // Escape HTML entities nếu cần (chỉ cho HTML, không escape cho JS/CSS)
                        // Nhưng vì đây là replace chung, ta sẽ return raw value
                        // User có thể tự escape trong code nếu cần
                        return value;
                    }
                    // Nếu không tìm thấy variable, giữ nguyên {variableName}
                    return match.Value;
                });
            }

            // Gộp JS từ nhiều tab con (được lưu với marker // [FLOW_JS_TAB:n]) thành 1 payload runtime.
            string NormalizeRuntimeJsCode(string jsCode)
            {
                if (string.IsNullOrWhiteSpace(jsCode)) return string.Empty;
                const string markerPrefix = "// [FLOW_JS_TAB:";
                if (jsCode.IndexOf(markerPrefix, StringComparison.Ordinal) < 0)
                    return jsCode;

                var regex = new System.Text.RegularExpressions.Regex(
                    @"^\s*//\s*\[FLOW_JS_TAB:(\d+)(?:\|P:(\d+))?(?:\|T:(.*?))?\]\s*$",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                var matches = regex.Matches(jsCode).Cast<System.Text.RegularExpressions.Match>().ToList();
                if (matches.Count == 0)
                    return jsCode;

                var parts = new System.Collections.Generic.List<string>();
                for (int i = 0; i < matches.Count; i++)
                {
                    var start = matches[i].Index + matches[i].Length;
                    var end = i + 1 < matches.Count ? matches[i + 1].Index : jsCode.Length;
                    var chunk = jsCode.Substring(start, end - start).Trim();
                    if (!string.IsNullOrWhiteSpace(chunk))
                        parts.Add(chunk);
                }

                if (parts.Count == 0) return string.Empty;
                return string.Join("\n\n/* --- FLOW_JS_TAB_SPLIT --- */\n\n", parts);
            }

            // Function để build HTML từ các tab (với cache và variable replacement)
            string BuildHtmlContent()
            {
                var html = node.HtmlCode ?? "<!DOCTYPE html>\n<html>\n<head>\n    <meta charset=\"UTF-8\">\n    <title>HTML UI</title>\n</head>\n<body>\n    <div>HTML UI</div>\n</body>\n</html>";
                var css = node.CssCode ?? string.Empty;
                var js = NormalizeRuntimeJsCode(node.JsCode ?? string.Empty);

                // ✅ Resolve input values từ mappings
                var inputValues = ResolveInputValues();

                // ✅ Kiểm tra cache: chỉ rebuild nếu code hoặc input values thay đổi
                var inputValuesChanged = !System.Linq.Enumerable.SequenceEqual(
                    _cachedInputValues?.OrderBy(kv => kv.Key) ?? System.Linq.Enumerable.Empty<System.Collections.Generic.KeyValuePair<string, string>>(),
                    inputValues.OrderBy(kv => kv.Key));

                // Offline assets cache key: file + enabled + mtime — nếu chỉ dùng tên file, F5 vẫn trả HTML cũ sau khi tải/ghi đè file trên đĩa.
                var offlineAssetsKey = string.Join("|", (node.OfflineAssets ?? new System.Collections.Generic.List<FlowMy.Models.HtmlOfflineAsset>())
                    .Select(a =>
                    {
                        var fn = a.LocalFileName ?? string.Empty;
                        long tick = 0;
                        try
                        {
                            var path = HtmlOfflineAssetService.GetLocalFilePath(fn);
                            if (File.Exists(path)) tick = File.GetLastWriteTimeUtc(path).Ticks;
                        }
                        catch { /* giữ tick = 0 */ }
                        return $"{fn}:{a.IsEnabled}:{tick}";
                    }));

                if (_cachedHtmlContent != null &&
                    _cachedHtmlCode == html &&
                    _cachedCssCode == css &&
                    _cachedJsCode == js &&
                    _cachedOfflineAssetsKey == offlineAssetsKey &&
                    !inputValuesChanged)
                {
                    return _cachedHtmlContent;
                }

                // ✅ Replace variables trong HTML, CSS, JS với resolved values
                html = ReplaceVariables(html, inputValues);
                css = ReplaceVariables(css, inputValues);
                js = ReplaceVariables(js, inputValues);

                // Nếu HTML không có <head>, thêm vào
                if (!html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
                {
                    html = html.Replace("<html>", "<html>\n<head>\n    <meta charset=\"UTF-8\">\n    <title>HTML UI</title>\n</head>", StringComparison.OrdinalIgnoreCase);
                }

                // Inject CSS vào <head>
                if (!string.IsNullOrWhiteSpace(css))
                {
                    var cssTag = $"\n    <style>\n{css}\n    </style>";
                    if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                    {
                        html = html.Replace("</head>", cssTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
                    {
                        html = html.Replace("<head>", "<head>" + cssTag, StringComparison.OrdinalIgnoreCase);
                    }
                }

                // ── Inject __acAsync runtime TRƯỚC user JS để window.__acAsync.onReceive() hoạt động ──
                {
                    var asyncRuntimeTag = @"
    <script>
    (function() {
      var _data = {};
      var _keyCallbacks = {};
      var _allCallbacks = [];
      window.__acAsync = {
        data: _data,
        onReceive: function(keyOrFn, fn) {
          if (typeof keyOrFn === 'function') {
            _allCallbacks.push(keyOrFn);
          } else if (typeof keyOrFn === 'string' && typeof fn === 'function') {
            if (!_keyCallbacks[keyOrFn]) _keyCallbacks[keyOrFn] = [];
            _keyCallbacks[keyOrFn].push(fn);
          }
        }
      };
      window.__acAsyncPush = function(key, value) {
        _data[key] = value;
        var cbs = _keyCallbacks[key];
        if (cbs) {
          for (var i = 0; i < cbs.length; i++) {
            try { cbs[i](value); } catch (e) { console.error('__acAsync callback error:', e); }
          }
        }
        for (var j = 0; j < _allCallbacks.length; j++) {
          try { _allCallbacks[j](JSON.parse(JSON.stringify(_data))); } catch (e) { console.error('__acAsync allData error:', e); }
        }
      };
    })();
    </script>";
                    if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                        html = html.Replace("</head>", asyncRuntimeTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                    else if (html.Contains("<body", StringComparison.OrdinalIgnoreCase))
                        html = System.Text.RegularExpressions.Regex.Replace(html, @"(<body\b[^>]*>)", "$1" + asyncRuntimeTag, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    else
                        html = asyncRuntimeTag + html;
                }

                // Inject JS vào trước </body> hoặc vào <head> nếu không có body
                if (!string.IsNullOrWhiteSpace(js))
                {
                    var jsTag = $"\n    <script>\n{js}\n    </script>";
                    if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                    {
                        html = html.Replace("</body>", jsTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
                    {
                        html = html.Replace("</head>", jsTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        html += jsTag;
                    }
                }

                // ✅ Inject offline assets (CSS trước, JS sau)
                // Inline content vào <style>/<script> để tương thích với data: URI navigation.
                // CSS offline có thể chứa @font-face src dạng data: (ví dụ Google Fonts đã tải qua HtmlOfflineAssetService — không cần url tương đối tới .woff2).
                var enabledAssets = (node.OfflineAssets ?? new System.Collections.Generic.List<FlowMy.Models.HtmlOfflineAsset>())
                    .Where(a => a.IsEnabled && !string.IsNullOrWhiteSpace(a.LocalFileName));

                foreach (var asset in enabledAssets)
                {
                    var content = HtmlOfflineAssetService.GetInlineContent(asset.LocalFileName);
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    var safeName = System.Security.SecurityElement.Escape(asset.Title ?? asset.LocalFileName);

                    if (string.Equals(asset.AssetType, "css", StringComparison.OrdinalIgnoreCase))
                    {
                        var cssTag = $"\n    <style>/* [offline] {safeName} */\n{content}\n    </style>";
                        if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                            html = html.Replace("</head>", cssTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                        else
                            html = "<style>" + content + "</style>" + html;
                    }
                    else // js
                    {
                        var jsTag = $"\n    <script>/* [offline] {safeName} */\n{content}\n    </script>";
                        if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                            html = html.Replace("</body>", jsTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
                        else if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                            html = html.Replace("</head>", jsTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                        else
                            html += jsTag;
                    }
                }

                // ✅ Lưu cache
                _cachedHtmlCode = node.HtmlCode ?? string.Empty;
                _cachedCssCode = node.CssCode ?? string.Empty;
                _cachedJsCode = node.JsCode ?? string.Empty;
                _cachedOfflineAssetsKey = offlineAssetsKey;
                _cachedInputValues = new System.Collections.Generic.Dictionary<string, string>(inputValues);
                _cachedHtmlContent = html;

                return html;
            }

            // Function để reload HTML vào WebView2
            async Task ReloadHtmlAsync()
            {
                try
                {
                    SetTopBarStatus("Preparing HTML...", false);
                    CoreWebView2? core;
                    try { core = webView.CoreWebView2; }
                    catch (ObjectDisposedException) { return; }
                    catch { return; }
                    if (core == null) return;

                    var htmlContent = BuildHtmlContent();

                    // Inject helper JS: acSubmit() -> host đọc DOM theo Params
                    // Helper này được append cuối HTML để user có thể gọi trực tiếp trong onclick.
                    var mediaRootsJson = BuildMediaSearchRootsJson();
                    var helperScript = $@"
<script>
  window.__acMediaSearchRoots = {mediaRootsJson};

  function acSubmit() {{
    if (window.chrome && window.chrome.webview) {{
      window.chrome.webview.postMessage({{ type: 'submit' }});
    }}
  }}
  
  function acStartWorkflow() {{
    if (window.chrome && window.chrome.webview) {{
      window.chrome.webview.postMessage({{ type: 'startWorkflow' }});
    }}
  }}

  // Download by raw curl command (host-side execution)
  function acDownloadByCurl(curlCommand, fileName, downloadKey) {{
    try {{
      if (!(window.chrome && window.chrome.webview)) return;
      window.chrome.webview.postMessage({{
        type: 'download_curl',
        curl: curlCommand || '',
        fileName: fileName || '',
        downloadKey: downloadKey || ''
      }});
    }} catch (_) {{}}
  }}

  // Resolve local file path to an internal https://*.local URL playable in WebView2.
  function acResolveLocalPath(localPath, requestId) {{
    try {{
      if (!(window.chrome && window.chrome.webview)) return;
      window.chrome.webview.postMessage({{
        type: 'resolve_local_path',
        path: localPath || '',
        requestId: requestId || ''
      }});
    }} catch (_) {{}}
  }}

  // Tìm file media theo URL ảo + các thư mục gợi ý (xem BuildMediaSearchRootsJson trên host).
  function acResolvePlayableRef(url, requestId) {{
    try {{
      if (!(window.chrome && window.chrome.webview)) return;
      var roots = [];
      try {{
        if (Array.isArray(window.__acMediaSearchRoots)) roots = window.__acMediaSearchRoots;
      }} catch (_) {{}}
      window.chrome.webview.postMessage({{
        type: 'resolve_playable_ref',
        url: url || '',
        requestId: requestId || '',
        searchRoots: roots
      }});
    }} catch (_) {{}}
  }}

  // Open native image picker on host and return absolute paths + data URLs.
  function acPickImageFiles(requestId) {{
    try {{
      if (!(window.chrome && window.chrome.webview)) return;
      window.chrome.webview.postMessage({{
        type: 'pick_image_files',
        requestId: requestId || ''
      }});
    }} catch (_) {{}}
  }}

  // ✅ Catch F5 / Ctrl+R to reload via C# (capture mode for better reliability)
  function __acHandleReloadHotkey(e) {{
    var isReloadKey = e && (e.key === 'F5' || (e.ctrlKey && (e.key === 'r' || e.key === 'R')));
    if (!isReloadKey) return;
    try {{ e.preventDefault(); }} catch (_) {{}}
    try {{ e.stopPropagation(); }} catch (_) {{}}
    try {{ e.stopImmediatePropagation(); }} catch (_) {{}}

    var sentToHost = false;
    try {{
      if (window.chrome && window.chrome.webview) {{
        window.chrome.webview.postMessage({{ type: 'reload' }});
        sentToHost = true;
      }}
    }} catch (_) {{}}

    // Fallback: if host bridge is unavailable, force browser-level reload.
    if (!sentToHost) {{
      try {{ window.location.reload(); }} catch (_) {{}}
    }}
  }}
  window.addEventListener('keydown', __acHandleReloadHotkey, true);
  document.addEventListener('keydown', __acHandleReloadHotkey, true);
</script>";
                    if (htmlContent.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                        htmlContent = htmlContent.Replace("</body>", helperScript + "\n</body>", StringComparison.OrdinalIgnoreCase);
                    else
                        htmlContent += helperScript;

                    // Inject CSP meta cho HTML UI preview nếu user chưa tự khai báo CSP.
                    // Mục tiêu: cho phép load video/image từ domain trả kết quả (assets.grok.com).
                    if (!htmlContent.Contains("Content-Security-Policy", StringComparison.OrdinalIgnoreCase))
                    {
                        var cspMeta =
                            "\n<meta http-equiv=\"Content-Security-Policy\" " +
                            "content=\"default-src 'self' https: data: blob:; " +
                            "img-src 'self' https: data: blob:; " +
                            "media-src 'self' https: data: blob:; " +
                            "connect-src 'self' https: wss:; " +
                            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https:; " +
                            "style-src 'self' 'unsafe-inline' https:; " +
                            "font-src 'self' https: data:; frame-src 'self' https:;\">" +
                            "\n";

                        if (htmlContent.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                            htmlContent = htmlContent.Replace("</head>", cspMeta + "</head>", StringComparison.OrdinalIgnoreCase);
                        else if (htmlContent.Contains("<head>", StringComparison.OrdinalIgnoreCase))
                            htmlContent = htmlContent.Replace("<head>", "<head>" + cspMeta, StringComparison.OrdinalIgnoreCase);
                        else if (htmlContent.Contains("<html>", StringComparison.OrdinalIgnoreCase))
                            htmlContent = htmlContent.Replace("<html>", "<html><head>" + cspMeta + "</head>", StringComparison.OrdinalIgnoreCase);
                        else
                            htmlContent = "<head>" + cspMeta + "</head>" + htmlContent;
                    }

                    // Data URI / NavigateToString bị giới hạn ~2MB (IPC) → HTML lớn (ví dụ font base64) ghi file tạm.
                    if (_tab2PreviewTempFile != null)
                    {
                        try { File.Delete(_tab2PreviewTempFile); } catch { /* ignore */ }
                        _tab2PreviewTempFile = null;
                    }

                    // NOTE:
                    // Avoid data: navigation because WebView applies a very strict default CSP
                    // (default-src 'none') that blocks remote media/image URLs.
                    const int MaxNavigateToStringUtf8Bytes = 1_900_000;
                    if (Encoding.UTF8.GetByteCount(htmlContent) <= MaxNavigateToStringUtf8Bytes)
                    {
                        SetTopBarStatus("Navigating...", false);
                        core.NavigateToString(htmlContent);
                    }
                    else
                    {
                        // file:// có origin đặc biệt (unique origin), dễ gây lỗi cross-origin tự thân.
                        // Dùng virtual host mapping để serve file tạm qua HTTPS nội bộ của WebView2.
                        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FlowMy_HtmlUiPreview");
                        Directory.CreateDirectory(tempDir);
                        var fileName = $"preview_{Guid.NewGuid():N}.html";
                        var path = System.IO.Path.Combine(tempDir, fileName);
                        File.WriteAllText(path, htmlContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                        _tab2PreviewTempFile = path;

                        const string virtualHost = "htmlui.local";
                        core.SetVirtualHostNameToFolderMapping(
                            virtualHost,
                            tempDir,
                            CoreWebView2HostResourceAccessKind.Allow);
                        SetTopBarStatus("Navigating (mapped host)...", false);
                        core.Navigate($"https://{virtualHost}/{Uri.EscapeDataString(fileName)}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Node/dialog vừa đóng hoặc WebView2 vừa bị dispose: bỏ qua reload này.
                }
                catch (Exception ex)
                {
                    SetTopBarStatus("Load failed", true, 2500);
                    System.Diagnostics.Debug.WriteLine($"Error reloading HTML: {ex.Message}");
                }
            }

            async Task RepushAsyncDataHistoryAsync(WebView2? targetWebView, int delayMs = 120)
            {
                try
                {
                    var coreForRepush = TryGetCoreSafe(targetWebView);
                    if (coreForRepush == null) return;

                    if (delayMs > 0)
                        await Task.Delay(delayMs);

                    if (node.AsyncDataReplayBuffer != null && node.AsyncDataReplayBuffer.Count > 0)
                    {
                        var replayItems = node.AsyncDataReplayBuffer.ToArray();
                        foreach (var item in replayItems)
                        {
                            var jsKey = System.Text.Json.JsonSerializer.Serialize(item.Key);
                            var jsVal = System.Text.Json.JsonSerializer.Serialize(item.Value);
                            await coreForRepush.ExecuteScriptAsync(
                                $"if(typeof window.__acAsyncPush==='function') window.__acAsyncPush({jsKey},{jsVal});");
                        }
                        return;
                    }

                    // Fallback cũ: chỉ có giá trị cuối theo key.
                    if (node.AsyncDataCache?.Count > 0)
                    {
                        foreach (var kvp in node.AsyncDataCache)
                        {
                            var jsKey = System.Text.Json.JsonSerializer.Serialize(kvp.Key);
                            var jsVal = System.Text.Json.JsonSerializer.Serialize(kvp.Value);
                            await coreForRepush.ExecuteScriptAsync(
                                $"if(typeof window.__acAsyncPush==='function') window.__acAsyncPush({jsKey},{jsVal});");
                        }
                    }
                }
                catch (Exception exRepush)
                {
                    System.Diagnostics.Debug.WriteLine($"HTML UI async data re-push error: {exRepush.Message}");
                }
            }

            // Đọc DOM theo cấu hình Params và cập nhật outputs cho node
            async Task UpdateOutputsFromDomAsync()
            {
                try
                {
                    var core = webView.CoreWebView2;
                    if (core == null) return;

                    var paramsText = node.ParamsCode ?? string.Empty;
                    var lines = paramsText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                    foreach (var rawLine in lines)
                    {
                        var line = rawLine?.Trim();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (line.StartsWith("//") || line.StartsWith("#")) continue;

                        // Hỗ trợ "key: selector" hoặc "key = selector"
                        string[] parts;
                        if (line.Contains(":"))
                            parts = line.Split(new[] { ':' }, 2);
                        else if (line.Contains("="))
                            parts = line.Split(new[] { '=' }, 2);
                        else
                            continue;

                        if (parts.Length != 2) continue;
                        var key = parts[0].Trim();
                        var selector = parts[1].Trim();
                        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(selector)) continue;

                        // Xây JS đọc value từ selector
                        var jsSelector = selector.Replace("\\", "\\\\").Replace("\"", "\\\"");
                        var script = $@"
(function() {{
  try {{
    var sel = ""{jsSelector}"";
    var el = document.querySelector(sel);
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
                            resultJson = await core.ExecuteScriptAsync(script);
                        }
                        catch
                        {
                            continue;
                        }

                        // WebView2 trả JSON: null, "string", number,...
                        string? value = null;
                        try
                        {
                            // Nếu là "null" hoặc null thì bỏ qua
                            if (!string.IsNullOrWhiteSpace(resultJson) && !string.Equals(resultJson, "null", StringComparison.OrdinalIgnoreCase))
                            {
                                value = JsonSerializer.Deserialize<string>(resultJson);
                            }
                        }
                        catch
                        {
                            // Nếu không parse được như string thì lưu raw JSON
                            value = resultJson;
                        }

                        if (value == null) continue;

                        // Gán vào outputs runtime
                        node.ResolvedOutputs[key] = value;
                        if (node.DynamicOutputs != null)
                        {
                            var dyn = node.DynamicOutputs.FirstOrDefault(o =>
                                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                            if (dyn != null)
                                dyn.UserValueOverride = value;
                        }
                    }

                    // Yêu cầu host sync panel để UI thấy output mới
                    host.RequestSyncDataPanels(immediate: false);

                    // Đồng bộ Execution Results (Result toggle) nếu execution visualizer đang hoạt động
                    try
                    {
                        var vm = host.ViewModel;
                        if (vm != null)
                        {
                            var field = typeof(FlowMy.ViewModels.WorkflowEditorViewModel)
                                .GetField("_executionVisualizer",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (field?.GetValue(vm) is FlowMy.Services.Workflow.IWorkflowExecutionVisualizer visualizer)
                            {
                                visualizer.RefreshSavedOutputs(new[] { node });
                            }
                        }
                    }
                    catch (Exception exVis)
                    {
                        System.Diagnostics.Debug.WriteLine($"HTML UI RefreshSavedOutputs error: {exVis.Message}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"HTML UI UpdateOutputsFromDomAsync error: {ex.Message}");
                }
            }

            // Listen model changes để reload khi code thay đổi
            if (node is INotifyPropertyChanged npcNode)
            {
                npcNode.PropertyChanged += async (_, e) =>
                {
                    if (e.PropertyName == nameof(HtmlUiNode.HtmlCode) ||
                        e.PropertyName == nameof(HtmlUiNode.CssCode) ||
                        e.PropertyName == nameof(HtmlUiNode.JsCode) ||
                        e.PropertyName == nameof(HtmlUiNode.ParamsCode) ||
                        e.PropertyName == nameof(HtmlUiNode.InputMappings) ||
                        e.PropertyName == nameof(HtmlUiNode.OfflineAssets))
                    {
                        // ✅ Clear cache khi code hoặc input mappings thay đổi
                        _cachedHtmlContent = null;
                        _cachedHtmlCode = null;
                        _cachedCssCode = null;
                        _cachedJsCode = null;
                        _cachedInputValues = null;
                        // ✅ Khi InputMappings thay đổi: restart auto-refresh timers theo cấu hình mới
                        if (e.PropertyName == nameof(HtmlUiNode.InputMappings))
                            webView.Dispatcher.BeginInvoke(new Action(StartAutoRefreshTimers), DispatcherPriority.Normal);
                        // Khi AutoReloadOnDialogClose = false: không tự reload (user tự F5 / bật lại checkbox).
                        // OfflineAssets cũng chỉ inject khi reload — không reload ngoài cờ này.
                        if (node.AutoReloadOnDialogClose)
                        {
                            webView.Dispatcher.BeginInvoke(new Action(async () =>
                            {
                                await ReloadHtmlAsync();
                            }), DispatcherPriority.Normal);
                        }
                    }
                    // Executor trigger đọc DOM khi chạy workflow
                    else if (e.PropertyName == nameof(HtmlUiNode.PendingReadDom) && node.PendingReadDom)
                    {
                        // Khi widget đang mở, để FloatingWidgetWindow đọc DOM thực tế của widget.
                        if (FlowMy.Services.FloatingWidgetManager.Instance.IsWidgetOpen(node.Id))
                        {
#if DEBUG
                            System.Diagnostics.Debug.WriteLine(
                                $"[HtmlUiNodeControl:{node.Id}] Skip canvas PendingReadDom because widget is open.");
#endif
                            return;
                        }

                        // Dùng InvokeAsync để đảm bảo await được thực thi đúng cách
                        _ = webView.Dispatcher.InvokeAsync(async () =>
                        {
                            try
                            {
                                MarkActivity();
                                await WakeRuntimeAsync();
                                await UpdateOutputsFromDomAsync();
                            }
                            finally
                            {
                                // Reset flag sau khi đọc xong để executor biết đã xong
                                node.PendingReadDom = false;
                                RestartSleepModeTimer();
                            }
                        }, DispatcherPriority.Normal);
                    }
                    // ✅ Executor trigger push async data vào WebView2
                    else if (e.PropertyName == nameof(HtmlUiNode.PendingAsyncDataPush) && node.PendingAsyncDataPush)
                    {
                        // Khi widget đang mở, ưu tiên để FloatingWidgetWindow drain queue + push vào runtime widget.
                        // Nếu canvas drain trước thì widget sẽ không còn data để nhận (__acAsync không cập nhật).
                        if (FlowMy.Services.FloatingWidgetManager.Instance.IsWidgetOpen(node.Id))
                        {
#if DEBUG
                            System.Diagnostics.Debug.WriteLine(
                                $"[HtmlUiNodeControl:{node.Id}] Skip canvas PendingAsyncDataPush because widget is open.");
#endif
                            return;
                        }

                        _ = webView.Dispatcher.InvokeAsync(async () =>
                        {
                            try
                            {
                                MarkActivity();
                                await WakeRuntimeAsync();
                                var core = TryGetCoreSafe(webView);
                                if (core == null) return;

                                // Drain tất cả items từ queue (thread-safe)
                                var items = new System.Collections.Generic.List<(string Key, string Value)>();
                                while (node.PendingAsyncPushQueue.TryDequeue(out var item))
                                {
                                    items.Add(item);
                                }

                                if (items.Count == 0) return;

                                foreach (var kvp in items)
                                {
                                    var jsKey = System.Text.Json.JsonSerializer.Serialize(kvp.Key);
                                    var jsVal = System.Text.Json.JsonSerializer.Serialize(kvp.Value);
                                    await core.ExecuteScriptAsync(
                                        $"if(typeof window.__acAsyncPush==='function') window.__acAsyncPush({jsKey},{jsVal});");
                                }
                            }
                            catch (Exception exPush)
                            {
                                System.Diagnostics.Debug.WriteLine($"HTML UI AsyncDataPush error: {exPush.Message}");
                            }
                            finally
                            {
                                node.PendingAsyncDataPush = false;
                                RestartSleepModeTimer();
                            }
                        }, DispatcherPriority.Normal);
                    }
                    // ✅ Load cookie vào Tab1 WebView2: immediate nếu đã sẵn sàng, defer nếu đang khởi tạo
                    else if (e.PropertyName == nameof(HtmlUiNode.PendingCookieText) &&
                             !string.IsNullOrWhiteSpace(node.PendingCookieText))
                    {
                        webView.Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            try
                            {
                                MarkActivity();
                                await WakeRuntimeAsync();
                                var tab1Core = TryGetCoreSafe(_webViewTab1);
                                if (tab1Core != null)
                                {
                                    // Tab1 đã sẵn sàng → apply ngay và clear
                                    var cookieText = node.PendingCookieText;
                                    node.PendingCookieText = null;
                                    if (!string.IsNullOrWhiteSpace(cookieText))
                                    {
                                        var url = await SetCookiesFromTextAsync(tab1Core, cookieText);
                                        if (!string.IsNullOrWhiteSpace(url))
                                        {
                                            node.WebTabUrl = url;
                                            tab1Core.Navigate(url);
                                            if (_addressBar != null) _addressBar.Text = url;
                                        }
                                    }
                                }
                                // else: Tab1 chưa init (CoreWebView2 = null) → giữ PendingCookieText
                                // → EnsureCoreWebView2Async completion (line ~1130) sẽ pick up và apply
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[HtmlUiNode] Load cookie error: {ex.Message}");
                            }
                            finally
                            {
                                RestartSleepModeTimer();
                            }
                        }), DispatcherPriority.Normal);
                    }
                    // ✅ UseWebTab thay đổi runtime → rebuild grid row[1]
                    else if (e.PropertyName == nameof(HtmlUiNode.UseWebTab))
                    {
                        webView.Dispatcher.BeginInvoke(new Action(() => RebuildMiddleContent()), DispatcherPriority.Normal);
                    }
                    // ✅ WebTab auto-refresh settings thay đổi → restart timer
                    else if (e.PropertyName == nameof(HtmlUiNode.WebTabAutoRefreshEnabled) ||
                             e.PropertyName == nameof(HtmlUiNode.WebTabAutoRefreshInterval) ||
                             e.PropertyName == nameof(HtmlUiNode.WebTabAutoRefreshUnit))
                    {
                        webView.Dispatcher.BeginInvoke(new Action(() => RestartWebTabAutoRefreshTimer()), DispatcherPriority.Normal);
                    }
                    else if (e.PropertyName == nameof(HtmlUiNode.WakeRequestToken))
                    {
                        webView.Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            MarkActivity();
                            await WakeRuntimeAsync();
                        }), DispatcherPriority.Normal);
                    }
                    else if (e.PropertyName == nameof(HtmlUiNode.EnableSleepMode) ||
                             e.PropertyName == nameof(HtmlUiNode.SleepIdleTimeoutValue) ||
                             e.PropertyName == nameof(HtmlUiNode.SleepIdleTimeoutUnit))
                    {
                        webView.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            MarkActivity();
                            RestartSleepModeTimer();
                        }), DispatcherPriority.Background);
                    }
                };
            }


            if (GpuDetectionHelper.IsGpuAvailable)
            {
                RenderOptions.SetBitmapScalingMode(webView, BitmapScalingMode.Unspecified);
                RenderOptions.SetCachingHint(webView, CachingHint.Unspecified);
                webView.CacheMode = null;
            }

            // Helper: resolve cookie text từ WebTabCookieSource node
            string ResolveWebTabCookieText()
            {
                if (string.IsNullOrWhiteSpace(node.WebTabCookieSourceNodeId)) return string.Empty;
                var vm = host?.ViewModel;
                if (vm?.Nodes == null) return string.Empty;
                var sourceNode = vm.Nodes.FirstOrDefault(n =>
                    string.Equals(n.Id, node.WebTabCookieSourceNodeId, StringComparison.OrdinalIgnoreCase));
                if (sourceNode == null) return string.Empty;
                var key = string.IsNullOrWhiteSpace(node.WebTabCookieSourceOutputKey)
                    ? (sourceNode.DynamicOutputs?.Count > 0 ? sourceNode.DynamicOutputs[0].Key : null)
                    : node.WebTabCookieSourceOutputKey;
                if (string.IsNullOrWhiteSpace(key)) return string.Empty;
                return NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key) ?? string.Empty;
            }

            // Stop auto-refresh timer cho Tab1
            void StopWebTabAutoRefreshTimer()
            {
                _webTabAutoRefreshTimer?.Stop();
                _webTabAutoRefreshTimer = null;
            }

            // Restart auto-refresh timer Tab1 theo node.WebTabAutoRefreshEnabled/Interval/Unit
            void RestartWebTabAutoRefreshTimer()
            {
                StopWebTabAutoRefreshTimer();
                if (!node.UseWebTab || !node.WebTabAutoRefreshEnabled) return;

                var intervalMs = node.WebTabAutoRefreshUnit switch
                {
                    "s" => node.WebTabAutoRefreshInterval * 1000,
                    "min" => node.WebTabAutoRefreshInterval * 60000,
                    _ => node.WebTabAutoRefreshInterval // "ms"
                };
                intervalMs = Math.Max(500, intervalMs);

                _webTabAutoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
                _webTabAutoRefreshTimer.Tick += async (_, _) =>
                {
                    try
                    {
                        var tab1Core = TryGetCoreSafe(_webViewTab1);
                        if (tab1Core == null) return;
                        var cookieText = ResolveWebTabCookieText();
                        if (string.IsNullOrWhiteSpace(cookieText)) return;
                        var url = await SetCookiesFromTextAsync(tab1Core, cookieText);
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            node.WebTabUrl = url;
                            tab1Core.Navigate(url);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HtmlUiNode] WebTab auto-refresh error: {ex.Message}");
                    }
                };
                _webTabAutoRefreshTimer.Start();
            }

            // ====== RebuildMiddleContent: xây dựng lại nội dung row[1] của grid ======
            // Được gọi khi UseWebTab thay đổi runtime
            void RebuildMiddleContent()
            {
                // 1. Xoá tất cả children của grid row[1]
                var row1Children = grid.Children
                    .OfType<UIElement>()
                    .Where(c => Grid.GetRow(c) == 1)
                    .ToList();
                foreach (var c in row1Children)
                    grid.Children.Remove(c);

                // 2. Dispose Tab1 WebView2 cũ nếu có
                if (_webViewTab1 != null)
                {
                    try { if (_webViewTab1.CoreWebView2 != null) _webViewTab1.CoreWebView2.Navigate("about:blank"); } catch { }
                    try { _webViewTab1.Dispose(); } catch { }
                    _webViewTab1 = null;
                }
                _tabControl = null;
                _addressBar = null;
                StopWebTabAutoRefreshTimer();

                // 3. Đảm bảo webView (Tab2) được detach khỏi parent cũ
                if (webView.Parent is Panel oldParent)
                    oldParent.Children.Remove(webView);

                if (node.UseWebTab)
                {
                    // Tạo lại Tab1 WebView2
                    _webViewTab1 = new WebView2 { Visibility = Visibility.Collapsed };

                    // ── Address bar with lock icon + search suggestion popup ──
                    var urlPill = new Border
                    {
                        CornerRadius = new CornerRadius(12),
                        Background = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(6, 0, 4, 0),
                        Height = 26,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var urlPillInner = new Grid();
                    urlPillInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    urlPillInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var lockIcon = new TextBlock { Text = "🔒", FontSize = 10, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), Opacity = 0.85 };
                    Grid.SetColumn(lockIcon, 0);
                    urlPillInner.Children.Add(lockIcon);
                    _addressBar = new TextBox
                    {
                        Text = node.WebTabUrl ?? string.Empty,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = Brushes.White,
                        CaretBrush = Brushes.White,
                        SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 100, 180, 255)),
                        FontSize = 11,
                        Padding = new Thickness(0, 1, 0, 1),
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(_addressBar, 1);
                    urlPillInner.Children.Add(_addressBar);
                    urlPill.Child = urlPillInner;
                    _addressBar.GotFocus += (_, _) => urlPill.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 100, 180, 255));
                    _addressBar.LostFocus += (_, _) => urlPill.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

                    var navBtn = new Button
                    {
                        Content = "↵",
                        Width = 24,
                        Height = 24,
                        Margin = new Thickness(4, 0, 0, 0),
                        Padding = new Thickness(0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                        Foreground = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand
                    };

                    Action doNav = () =>
                    {
                        var tab1Core = TryGetCoreSafe(_webViewTab1);
                        if (tab1Core == null) return;
                        var input = _addressBar?.Text?.Trim() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(input)) return;

                        // Smart navigation: protocol / domain / keyword
                        string navTarget;
                        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            navTarget = input;
                        }
                        else if (!input.Contains(' ') && input.Contains('.'))
                        {
                            navTarget = "https://" + input;
                        }
                        else
                        {
                            navTarget = "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
                        }
                        node.WebTabUrl = navTarget;
                        tab1Core.Navigate(navTarget);
                    };
                    navBtn.Click += (_, _) => doNav();

                    // ── Google Suggest Popup ──
                    var suggestListBox = new ListBox
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                        Foreground = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                        BorderThickness = new Thickness(1),
                        MaxHeight = 200,
                        FontSize = 11,
                        Padding = new Thickness(0)
                    };
                    ScrollViewer.SetHorizontalScrollBarVisibility(suggestListBox, ScrollBarVisibility.Disabled);
                    var sItemStyle = new Style(typeof(ListBoxItem));
                    sItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
                    sItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
                    sItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
                    sItemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
                    sItemStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
                    var sHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                    sHover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 100, 180, 255))));
                    sItemStyle.Triggers.Add(sHover);
                    suggestListBox.ItemContainerStyle = sItemStyle;
                    var suggestPopup = new Popup
                    {
                        PlacementTarget = urlPill,
                        Placement = PlacementMode.Bottom,
                        StaysOpen = false,
                        AllowsTransparency = true,
                        PopupAnimation = PopupAnimation.Fade,
                        Child = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(0, 0, 8, 8),
                            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 3, Opacity = 0.5 },
                            Child = suggestListBox
                        }
                    };
                    urlPill.SizeChanged += (_, _) => suggestPopup.Width = urlPill.ActualWidth;

                    // Search suggest: fetch from Google Suggest API
                    DispatcherTimer? suggestDebounce = null;
                    var _suggestHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                    async void FetchSuggest(string q)
                    {
                        q = q.Trim();
                        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) { suggestPopup.IsOpen = false; return; }
                        if (q.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || q.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { suggestPopup.IsOpen = false; return; }
                        try
                        {
                            var url2 = $"https://suggestqueries.google.com/complete/search?client=firefox&q={Uri.EscapeDataString(q)}";
                            var json = await _suggestHttp.GetStringAsync(url2);
                            var arr = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                            if (arr.ValueKind != System.Text.Json.JsonValueKind.Array || arr.GetArrayLength() < 2) { suggestPopup.IsOpen = false; return; }
                            var items = arr[1];
                            suggestListBox.Items.Clear();
                            foreach (var item in items.EnumerateArray()) { var s = item.GetString(); if (!string.IsNullOrWhiteSpace(s)) suggestListBox.Items.Add(s); }
                            suggestPopup.IsOpen = suggestListBox.Items.Count > 0 && _addressBar!.IsFocused;
                        }
                        catch { suggestPopup.IsOpen = false; }
                    }

                    _addressBar.TextChanged += (_, _) =>
                    {
                        suggestDebounce?.Stop();
                        suggestDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                        suggestDebounce.Tick += (__, ___) => { suggestDebounce.Stop(); FetchSuggest(_addressBar.Text); };
                        suggestDebounce.Start();
                    };
                    _addressBar.PreviewKeyDown += (s, e) =>
                    {
                        if (suggestPopup.IsOpen)
                        {
                            if (e.Key == Key.Down) { e.Handled = true; var i = suggestListBox.SelectedIndex; if (i < suggestListBox.Items.Count - 1) suggestListBox.SelectedIndex = i + 1; return; }
                            if (e.Key == Key.Up) { e.Handled = true; var i = suggestListBox.SelectedIndex; if (i > 0) suggestListBox.SelectedIndex = i - 1; return; }
                            if (e.Key == Key.Escape) { suggestPopup.IsOpen = false; e.Handled = true; return; }
                            if (e.Key == Key.Enter && suggestListBox.SelectedItem is string sel) { e.Handled = true; _addressBar.Text = sel; suggestPopup.IsOpen = false; doNav(); return; }
                        }
                        if (e.Key == Key.Enter || e.Key == Key.Return) { e.Handled = true; suggestPopup.IsOpen = false; doNav(); }
                    };
                    _addressBar.LostFocus += (_, _) => _addressBar.Dispatcher.BeginInvoke(new Action(() => { suggestPopup.IsOpen = false; }), DispatcherPriority.Background);
                    suggestListBox.PreviewMouseLeftButtonDown += (s, e) =>
                    {
                        var el = e.OriginalSource as DependencyObject;
                        while (el != null && !(el is ListBoxItem)) el = VisualTreeHelper.GetParent(el);
                        if (el is ListBoxItem lbi && lbi.Content is string picked) { e.Handled = true; _addressBar.Text = picked; suggestPopup.IsOpen = false; _addressBar.Focus(); doNav(); }
                    };

                    // ── Address bar row: [backBtn] [fwdBtn] [urlPill*] [reloadBtn] [goBtn] ──
                    var backBtn = new Button
                    {
                        Content = "◀",
                        Width = 24,
                        Height = 24,
                        FontSize = 11,
                        Padding = new Thickness(0),
                        Margin = new Thickness(0, 0, 2, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand,
                        ToolTip = "Quay lại",
                        Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                        Foreground = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                        BorderThickness = new Thickness(1)
                    };
                    var fwdBtn = new Button
                    {
                        Content = "▶",
                        Width = 24,
                        Height = 24,
                        FontSize = 11,
                        Padding = new Thickness(0),
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand,
                        ToolTip = "Tiến",
                        Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                        Foreground = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                        BorderThickness = new Thickness(1)
                    };
                    var reloadBtn = new Button
                    {
                        Content = "⟳",
                        Width = 24,
                        Height = 24,
                        FontSize = 13,
                        Padding = new Thickness(0),
                        Margin = new Thickness(4, 0, 2, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand,
                        ToolTip = "Tải lại (F5)",
                        Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                        Foreground = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                        BorderThickness = new Thickness(1)
                    };
                    backBtn.Click += (_, _) => { try { if (_webViewTab1?.CoreWebView2?.CanGoBack == true) _webViewTab1.CoreWebView2.GoBack(); } catch { } };
                    fwdBtn.Click += (_, _) => { try { if (_webViewTab1?.CoreWebView2?.CanGoForward == true) _webViewTab1.CoreWebView2.GoForward(); } catch { } };
                    reloadBtn.Click += (_, _) => { try { _webViewTab1?.CoreWebView2?.Reload(); } catch { } };

                    // Progress bar
                    var progressBar1 = new ProgressBar
                    {
                        Height = 2,
                        IsIndeterminate = true,
                        Visibility = Visibility.Collapsed,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xA8, 0xFF)),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0)
                    };

                    // Simple 2-row grid: row0=controls row1=progressBar
                    var addrPanel = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)) };
                    addrPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    addrPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    // Controls row: [back][fwd][urlPill*][reload][go]
                    var ctrlRow = new Grid { Margin = new Thickness(4, 3, 4, 3) };
                    ctrlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });         // back
                    ctrlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });         // fwd
                    ctrlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // urlPill (stretches)
                    ctrlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });         // reload
                    ctrlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });         // go (navBtn)
                    Grid.SetColumn(backBtn, 0); Grid.SetColumn(fwdBtn, 1);
                    Grid.SetColumn(urlPill, 2); Grid.SetColumn(reloadBtn, 3); Grid.SetColumn(navBtn, 4);
                    ctrlRow.Children.Add(backBtn); ctrlRow.Children.Add(fwdBtn);
                    ctrlRow.Children.Add(urlPill); ctrlRow.Children.Add(reloadBtn); ctrlRow.Children.Add(navBtn);

                    Grid.SetRow(ctrlRow, 0); Grid.SetRow(progressBar1, 1);
                    addrPanel.Children.Add(ctrlRow); addrPanel.Children.Add(progressBar1);

                    // Wire progress bar to navigation state
                    // (done inside wvInit.Loaded after core1 is ready — captures progressBar1 closure)

                    // Tab1 layout: addrPanel (row0 Auto) + _webViewTab1 (row1 *)
                    var tab1Container = new Grid();
                    tab1Container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    tab1Container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    Grid.SetRow(addrPanel, 0); Grid.SetRow(_webViewTab1, 1);
                    tab1Container.Children.Add(addrPanel); tab1Container.Children.Add(_webViewTab1);

                    var tab2Container = new Grid();
                    tab2Container.Children.Add(webView);

                    // ── Outer layout: tabBarGrid (row0 Auto) + contentHost (row1 *) ──
                    var outerGrid = new Grid();
                    outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    var tabBarGrid = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)) };
                    tabBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    tabBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var initialActiveTabBg = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                    var initialInactiveTabBg = new SolidColorBrush(Color.FromRgb(0x12, 0x1C, 0x2B));
                    var initialActiveFg = new SolidColorBrush(Colors.White);
                    var initialInactiveFg = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0xA8));

                    var btnTab1 = new Border
                    {
                        Background = initialActiveTabBg,
                        Cursor = Cursors.Hand,
                        Padding = new Thickness(8, 5, 8, 5),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
                        BorderThickness = new Thickness(0, 0, 1, 2),
                        Child = new TextBlock { Text = "🌐 Web", Foreground = initialActiveFg, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11, FontWeight = FontWeights.SemiBold }
                    };
                    var btnTab2 = new Border
                    {
                        Background = initialInactiveTabBg,
                        Cursor = Cursors.Hand,
                        Padding = new Thickness(8, 5, 8, 5),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = new TextBlock { Text = "🖥️ HTML UI", Foreground = initialInactiveFg, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11, FontWeight = FontWeights.SemiBold }
                    };
                    Grid.SetColumn(btnTab1, 0); Grid.SetColumn(btnTab2, 1);
                    tabBarGrid.Children.Add(btnTab1); tabBarGrid.Children.Add(btnTab2);
                    Grid.SetRow(tabBarGrid, 0); outerGrid.Children.Add(tabBarGrid);

                    var contentHost = new Grid();
                    Grid.SetRow(contentHost, 1); outerGrid.Children.Add(contentHost);
                    // Extension-mode: Tab1 always fills content area; Tab2 is always Collapsed
                    // but kept in the visual tree so webView (HTML UI) can initialise normally.
                    tab1Container.Visibility = Visibility.Visible;
                    tab2Container.Visibility = Visibility.Collapsed;
                    _isTab1WebViewVisible = true;
                    contentHost.Children.Add(tab1Container);
                    contentHost.Children.Add(tab2Container); // invisible host for webView init

                    // ✅ Dual WebView2 tab switching: Tab1=browser (_webViewTab1), Tab2=HTML UI (webView)
                    var accentColor = Color.FromRgb(0x38, 0xBD, 0xF8); // cyan accent
                    var activeTabBg = Color.FromRgb(0x0F, 0x62, 0xA8); // stronger active fill
                    var inactiveColor = Color.FromRgb(0x12, 0x1C, 0x2B);
                    var activeTextColor = Colors.White;
                    var inactiveTextColor = Color.FromRgb(0x7D, 0x8E, 0xA3);
                    var hoverBg = Color.FromRgb(0x26, 0x33, 0x45);
                    int _selectedTabIndex = 0;
                    void ApplyTabVisualState(int? hoverTab = null)
                    {
                        bool isTab1 = _selectedTabIndex == 0;

                        // Base selected/unselected visual
                        var tab1Bg = isTab1 ? activeTabBg : inactiveColor;
                        var tab2Bg = isTab1 ? inactiveColor : activeTabBg;

                        // Hover only affects inactive tab; selected tab keeps strong active state
                        if (hoverTab == 0 && !isTab1) tab1Bg = hoverBg;
                        if (hoverTab == 1 && isTab1) tab2Bg = hoverBg;

                        btnTab1.Background = new SolidColorBrush(tab1Bg);
                        btnTab2.Background = new SolidColorBrush(tab2Bg);
                        btnTab1.BorderBrush = new SolidColorBrush(isTab1 ? accentColor : Color.FromRgb(0x2D, 0x3A, 0x4F));
                        btnTab2.BorderBrush = new SolidColorBrush(!isTab1 ? accentColor : Color.FromRgb(0x2D, 0x3A, 0x4F));
                        btnTab1.BorderThickness = new Thickness(0, isTab1 ? 1 : 0, 1, isTab1 ? 3 : 1);
                        btnTab2.BorderThickness = new Thickness(0, !isTab1 ? 1 : 0, 0, !isTab1 ? 3 : 1);
                        if (btnTab1.Child is TextBlock tb1) tb1.Foreground = new SolidColorBrush(isTab1 ? activeTextColor : inactiveTextColor);
                        if (btnTab2.Child is TextBlock tb2) tb2.Foreground = new SolidColorBrush(!isTab1 ? activeTextColor : inactiveTextColor);
                    }

                    void SelectTab(int idx)
                    {
                        _selectedTabIndex = idx;
                        if (idx == 0) // Tab1: Browser WebView
                        {
                            tab1Container.Visibility = Visibility.Visible;
                            tab2Container.Visibility = Visibility.Collapsed;
                            _isTab1WebViewVisible = true;
                            if (_webViewTab1 != null) _webViewTab1.Visibility = Visibility.Visible;
                        }
                        else // Tab2: HTML UI (webView)
                        {
                            tab2Container.Visibility = Visibility.Visible;
                            webView.Visibility = Visibility.Visible;
                            tab1Container.Visibility = Visibility.Collapsed;
                            _isTab1WebViewVisible = false;
                        }

                        ApplyTabVisualState();
                    }
                    // ── Tab buttons: use PreviewMouseLeftButtonDown+Handled to beat the node drag handler ──
                    void SetupTabBtn(Border btn, int idx)
                    {
                        btn.MouseEnter += (_, _) =>
                        {
                            ApplyTabVisualState(idx);
                        };
                        btn.MouseLeave += (_, _) =>
                        {
                            ApplyTabVisualState();
                        };
                        // ✅ PreviewMouseLeftButtonDown: fires BEFORE node's drag MouseDown handler, e.Handled stops drag
                        btn.PreviewMouseLeftButtonDown += (_, e) =>
                        {
                            e.Handled = true;   // prevent node drag
                            SelectTab(idx);
                        };
                    }
                    SetupTabBtn(btnTab1, 0);
                    SetupTabBtn(btnTab2, 1);
                    ApplyTabVisualState();
                    // Remove old MouseLeftButtonUp handlers (were set prior, now superseded)
                    // (no-op since they weren't hooked yet — SelectTab wired only via SetupTabBtn)

                    Grid.SetRow(outerGrid, 1);
                    grid.Children.Add(outerGrid);

                    // ✅ Scale tab bar + address bar immediately (fix scaling lost after reload)
                    var baseline = border.MinHeight > 0 ? border.MinHeight : 200.0;
                    var rawScale = baseline > 0 ? node.Height / baseline : 1.0;
                    var initScale = Math.Max(1.0, rawScale);
                    tabBarGrid.LayoutTransform = new ScaleTransform(initScale, initScale);
                    addrPanel.LayoutTransform = new ScaleTransform(initScale, initScale);

                    // Use dummy TabControl Tag to store both scalable grids
                    _tabControl = new TabControl { Tag = (tabBarGrid, addrPanel) };

                    // Init Tab1 WebView2
                    var wvInit = _webViewTab1;
                    wvInit.Loaded += async (_, _) =>
                    {
                        try
                        {
                            var env = await WebView2EnvironmentManager.GetSharedEnvironmentAsync();
                            await EnsureCoreWebView2ThrottledAsync(wvInit, env);
                            var core1 = TryGetCoreSafe(wvInit);
                            if (core1 == null) return;
                            AttachProcessFailedHandler(wvInit, isTab1: true);
                            await WebCookiePortableBridge.TryConsumeAndApplyAsync(core1.CookieManager);
                            core1.NavigationCompleted += (_, _) =>
                            {
                                var url = core1.Source;
                                if (!string.IsNullOrWhiteSpace(url) && url != "about:blank")
                                {
                                    node.WebTabUrl = url;
                                    wvInit.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        if (_addressBar != null && _addressBar.Text != url) _addressBar.Text = url;
                                        // Update lock icon: https = locked, http = unlocked
                                        if (lockIcon != null) lockIcon.Text = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "🔒" : "🔓";
                                        SyncWebView1Position();
                                    }), DispatcherPriority.Normal);
                                }
                            };
                            if (!string.IsNullOrWhiteSpace(node.WebTabUrl)) core1.Navigate(node.WebTabUrl);
                            else core1.Navigate("https://www.google.com");
                            // Wire progress bar
                            core1.NavigationStarting += (_, _) => wvInit.Dispatcher.BeginInvoke(new Action(() => progressBar1.Visibility = Visibility.Visible), DispatcherPriority.Normal);
                            core1.NavigationCompleted += (_, _) => wvInit.Dispatcher.BeginInvoke(new Action(() => progressBar1.Visibility = Visibility.Collapsed), DispatcherPriority.Normal);
                            // ✅ Tab1: chỉ track navigation state để progress bar + cookie load
                            // (Không cần overlay nữa — Tab2/webView là separate WebView2)
                            // Check PendingCookieText
                            if (!string.IsNullOrWhiteSpace(node.PendingCookieText))
                            {
                                var ct = node.PendingCookieText; node.PendingCookieText = null;
                                var navUrl = await SetCookiesFromTextAsync(core1, ct);
                                if (!string.IsNullOrWhiteSpace(navUrl)) { node.WebTabUrl = navUrl; core1.Navigate(navUrl); if (_addressBar != null) _addressBar.Text = navUrl; }
                            }
                            // Check source node cookie
                            if (string.IsNullOrWhiteSpace(node.PendingCookieText))
                            {
                                var ct2 = ResolveWebTabCookieText();
                                if (!string.IsNullOrWhiteSpace(ct2))
                                {
                                    var navUrl2 = await SetCookiesFromTextAsync(core1, ct2);
                                    if (!string.IsNullOrWhiteSpace(navUrl2)) { node.WebTabUrl = navUrl2; core1.Navigate(navUrl2); if (_addressBar != null) _addressBar.Text = navUrl2; }
                                }
                            }
                            wvInit.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                // Only show if Tab1 is selected
                                if (tab1Container.Visibility == Visibility.Visible)
                                {
                                    wvInit.Visibility = Visibility.Visible;
                                    _isTab1WebViewVisible = true;
                                }
                                SyncWebView1Position();
                            }), DispatcherPriority.Loaded);
                            RestartWebTabAutoRefreshTimer();
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HtmlUiNode] RebuildTab1 init error: {ex.Message}"); }
                    };
                }
                else
                {
                    // Mode cũ: chỉ webView
                    Grid.SetRow(webView, 1);
                    grid.Children.Add(webView);
                }

                // Cập nhật topBarText
                SetTopBarStatus(string.Empty, false);
            }


            // ── SyncWebView1Position: sync Tab1 WebView2 (HwndHost) position after pan/zoom/drag ──
            void SyncWebView1Position()
            {
                try
                {
                    if (NodeChrome.IsZooming || host.IsPanning || host.DraggedNode == node) return;
                    var wv1 = _webViewTab1;
                    if (wv1 == null || wv1.ActualWidth <= 0 || wv1.ActualHeight <= 0) return;
                    wv1.InvalidateMeasure(); wv1.InvalidateArrange(); wv1.InvalidateVisual();
                    if (wv1.Parent is FrameworkElement p1) p1.InvalidateArrange();
                }
                catch { }
            }

            // ── BuildHtmlUiOverlayScript: body-swap mode ──
            // Tab2 hides the page's original body and shows HTML UI full-page.
            // JS runs in Tab1's page context → can read DOM, window vars, etc.
            string BuildHtmlUiOverlayScript(HtmlUiNode n)
            {
                try
                {
                    // ✅ Apply {variable} substitution cho overlay (giống ReloadHtmlAsync trong single-tab mode)
                    var inputValues = ResolveInputValues();
                    var htmlResolved = ReplaceVariables(n.HtmlCode ?? string.Empty, inputValues);
                    var cssResolved = ReplaceVariables(n.CssCode ?? string.Empty, inputValues);
                    var jsResolved = ReplaceVariables(NormalizeRuntimeJsCode(n.JsCode ?? string.Empty), inputValues);

                    // Use JSON serialisation for safe escaping of arbitrary content
                    var htmlJson = System.Text.Json.JsonSerializer.Serialize(htmlResolved);
                    var cssJson = System.Text.Json.JsonSerializer.Serialize(cssResolved);
                    var jsJson = System.Text.Json.JsonSerializer.Serialize(jsResolved);

                    return $@"(function() {{
  var HTMLUI_PANEL = '__htmlui_panel__';
  var BODY_WRAP    = '__htmlui_body_wrap__';
  var htmlCode = {htmlJson};
  var cssCode  = {cssJson};
  var jsCode   = {jsJson};

  function init() {{
    // Remove stale elements from a previous injection
    var ep = document.getElementById(HTMLUI_PANEL); if (ep) ep.remove();
    var ew = document.getElementById(BODY_WRAP);
    if (ew) {{ while (ew.firstChild) document.body.insertBefore(ew.firstChild, ew); ew.remove(); }}

    // Wrap all visible body children so we can hide/show them as one unit
    var wrapper = document.createElement('div');
    wrapper.id = BODY_WRAP;
    wrapper.style.display = 'block';
    var kids = Array.from(document.body.childNodes).filter(function(n) {{
      return n.nodeType === 1 && n.tagName !== 'SCRIPT' && n.tagName !== 'STYLE';
    }});
    kids.forEach(function(c) {{ wrapper.appendChild(c); }});
    document.body.appendChild(wrapper);

    // Full-page HTML UI panel (hidden by default, same as Tab1 page)
    var panel = document.createElement('div');
    panel.id = HTMLUI_PANEL;
    panel.style.cssText = 'position:fixed;inset:0;width:100%;height:100%;overflow:auto;display:none;box-sizing:border-box;';
    if (cssCode) {{ var s=document.createElement('style'); s.textContent=cssCode; panel.appendChild(s); }}
    if (htmlCode) {{ var d=document.createElement('div'); d.style.cssText='width:100%;min-height:100%;'; d.innerHTML=htmlCode; panel.appendChild(d); }}
    document.body.appendChild(panel);

    // JS runs in page head — shares window scope with the web page
    if (jsCode) {{ try {{ var sc=document.createElement('script'); sc.textContent=jsCode; document.head.appendChild(sc); }} catch(e) {{}} }}

    // API called by WPF SelectTab via ExecuteScriptAsync
    window.__htmlui_show   = function() {{ wrapper.style.display='none';  panel.style.display='block'; }};
    window.__htmlui_hide   = function() {{ panel.style.display='none';   wrapper.style.display='block'; }};
    window.__htmlui_toggle = function() {{ panel.style.display==='none' ? window.__htmlui_show() : window.__htmlui_hide(); }};
  }}

  if (document.readyState === 'loading') {{ document.addEventListener('DOMContentLoaded', init); }}
  else {{ init(); }}
}})();";
                }
                catch { return string.Empty; }
            }

            void SyncWebViewPosition()
            {
                try
                {
                    // ✅ Chỉ skip sync khi đang zoom hoặc pan canvas, KHÔNG skip khi đang drag node
                    if (NodeChrome.IsZooming || host.IsPanning)
                        return;

                    // ✅ Đảm bảo WebView2 có kích thước hợp lệ trước khi sync (tránh lỗi HwndHost)
                    if (webView.ActualWidth <= 0 || webView.ActualHeight <= 0)
                        return;

                    // ✅ Chỉ dùng Invalidate* thay vì UpdateLayout() để tránh block UI thread
                    // UpdateLayout() sẽ được gọi tự động bởi WPF layout system
                    webView.InvalidateMeasure();
                    webView.InvalidateArrange();
                    webView.InvalidateVisual();

                    if (webView.Parent is FrameworkElement parent)
                    {
                        parent.InvalidateArrange();
                        parent.InvalidateVisual();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Sync WebView2 error: {ex.Message}");
                }
            }

            // ✅ Throttled version của SyncWebViewPosition để tránh gọi quá nhiều lần khi drag node
            void ThrottledSyncWebViewPosition()
            {
                if (!_webViewSyncTimers.TryGetValue(border, out var timer))
                {
                    timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(WebViewSyncThrottleMs) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        SyncWebViewPosition();
                    };
                    _webViewSyncTimers[border] = timer;
                }
                timer.Stop();
                timer.Start();
            }

            // ✅ Lưu base zoom level (mặc định 1.0 cho HTML UI) để tính toán zoom ngược với canvas zoom
            const double baseWebViewZoom = 1.0;

            // Function để set zoom cho WebView2 dựa trên canvas zoom + cấu hình theo node
            void UpdateWebViewZoomForCanvasZoom()
            {
                try
                {
                    var core = webView.CoreWebView2;
                    if (core == null) return;

                    // Tính toán zoom
                    double canvasZoom = host.ZoomLevel;
                    double webViewZoom;

                    // Nếu node đã có cấu hình CssZoom (lưu riêng cho node này), dùng trực tiếp
                    if (node.CssZoom > 0)
                    {
                        webViewZoom = node.CssZoom;
                    }
                    else
                    {
                        // Mặc định: zoom ngược với canvas để giữ tỉ lệ
                        webViewZoom = baseWebViewZoom / Math.Max(canvasZoom, 0.0001);
                    }

                    // Set zoom qua CSS
                    var script = $@"
                        (function() {{
                            document.body.style.zoom = '{webViewZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}';
                            if (!document.body.style.zoom) {{
                                // Fallback: dùng transform scale nếu zoom không được hỗ trợ
                                document.body.style.transform = 'scale({webViewZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)})';
                                document.body.style.transformOrigin = 'top left';
                            }}
                        }})();
                    ";
                    core.ExecuteScriptAsync(script);

                    // Cập nhật lại CssZoom cho node để khi Ctrl+S sẽ lưu zoom riêng cho node này
                    node.CssZoom = webViewZoom;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi update HTML UI WebView2 zoom: {ex.Message}");
                }
            }

            EventHandler? scaleChangedHandler = (_, _) =>
            {
                if (NodeChrome.IsZooming)
                {
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    // Also hide Tab1 WebView2 during zoom
                    if (_webViewTab1 != null && _webViewTab1.Visibility == Visibility.Visible)
                        _webViewTab1.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (webView.Visibility != Visibility.Visible)
                        webView.Visibility = Visibility.Visible;

                    // ✅ Update WebView2 zoom để giữ tỉ lệ với canvas zoom
                    UpdateWebViewZoomForCanvasZoom();

                    SyncWebViewPosition();
                    // Restore Tab1 WebView2 visible after zoom (only if Tab1 was selected)
                    if (_webViewTab1 != null && _isTab1WebViewVisible && _webViewTab1.Visibility == Visibility.Collapsed)
                        _webViewTab1.Visibility = Visibility.Visible;
                    SyncWebView1Position();
                    // ✅ Clip WebView2 HWND để không render trên toolbar/sidebar/minimap
                    // WebView2AirspaceClipper.UpdateClipping(webView, host);
                }
            };
            var scaleDescriptor = DependencyPropertyDescriptor.FromProperty(ScaleTransform.ScaleXProperty, typeof(ScaleTransform));
            scaleDescriptor?.AddValueChanged(host.ScaleTransform, scaleChangedHandler);

            EventHandler? translateChangedHandler = (_, _) =>
            {
                // ✅ Chỉ xử lý khi đang pan canvas, KHÔNG xử lý khi đang drag node để tránh lag
                if (host.IsPanning)
                {
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    // Also hide Tab1 WebView2 during pan
                    if (_webViewTab1 != null && _webViewTab1.Visibility == Visibility.Visible)
                        _webViewTab1.Visibility = Visibility.Collapsed;
                }
                else if (host.DraggedNode != node)
                {
                    // Chỉ sync khi không pan và không drag node này
                    if (webView.Visibility != Visibility.Visible)
                    {
                        webView.Visibility = Visibility.Visible;
                        SyncWebViewPosition();
                    }
                    // Restore Tab1 after pan ends (only if Tab1 was selected)
                    if (_webViewTab1 != null && _isTab1WebViewVisible && _webViewTab1.Visibility == Visibility.Collapsed)
                    {
                        _webViewTab1.Visibility = Visibility.Visible;
                        SyncWebView1Position();
                    }
                    // ✅ Clip WebView2 HWND để không render trên toolbar/sidebar/minimap
                    // WebView2AirspaceClipper.UpdateClipping(webView, host);
                }
            };
            var translateXDescriptor = DependencyPropertyDescriptor.FromProperty(TranslateTransform.XProperty, typeof(TranslateTransform));
            var translateYDescriptor = DependencyPropertyDescriptor.FromProperty(TranslateTransform.YProperty, typeof(TranslateTransform));
            translateXDescriptor?.AddValueChanged(host.TranslateTransform, translateChangedHandler);
            translateYDescriptor?.AddValueChanged(host.TranslateTransform, translateChangedHandler);

            EventHandler? renderingHandler = (_, _) =>
            {
                if (NodeChrome.IsZooming)
                    return;

                // Ẩn WebView2 khi đang panning hoặc dragging node để tránh lag
                if (host.IsPanning || host.DraggedNode == node)
                {
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    if (_webViewTab1 != null && _webViewTab1.Visibility == Visibility.Visible)
                        _webViewTab1.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Hiển thị lại và sync WebView2 sau khi dừng pan/drag
                    if (webView.Visibility != Visibility.Visible)
                    {
                        webView.Visibility = Visibility.Visible;
                        SyncWebViewPosition();
                    }
                    if (_webViewTab1 != null && _isTab1WebViewVisible && _webViewTab1.Visibility == Visibility.Collapsed)
                    {
                        _webViewTab1.Visibility = Visibility.Visible;
                        SyncWebView1Position();
                    }
                }
            };
            System.Windows.Media.CompositionTarget.Rendering += renderingHandler;

            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Top
            };
            topBarChromeRow = new Grid();
            topBarChromeRow.VerticalAlignment = VerticalAlignment.Top;
            topBarChromeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBarChromeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBarText = new TextBlock
            {
                Text = node.UseWebTab ? "Web + HTML UI" : "HTML UI",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(topBarText, 0);
            var viewportExpandBtn = new Button
            {
                Content = "\uE740",
                FontFamily = ViewportExpandIconFont,
                ToolTip = "Phóng to vừa khung nhìn",
                Width = 28,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(6, 0, 0, 0),
                FontSize = 14,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(viewportExpandBtn, 1);
            // Không dùng PreviewMouseLeftButtonDown + e.Handled — sẽ chặn Click của Button.
            // DragDropHandler đã bỏ qua kéo node khi OriginalSource là ButtonBase.
            viewportExpandBtn.Click += (_, _) => ToggleNodeViewportExpand(node, border, host, viewportExpandBtn, bottomBarCapture);
            topBarChromeRow.Children.Add(topBarText);
            topBarChromeRow.Children.Add(viewportExpandBtn);
            topBar.Child = topBarChromeRow;
            Grid.SetRow(topBar, 0);
            grid.Children.Add(topBar);

            // ====== Khởi tạo nội dung row[1] (Tab1 + Tab2 hoặc chỉ webView) ======
            // Dùng RebuildMiddleContent() để tránh duplicate code với handler UseWebTab thay đổi runtime
            RebuildMiddleContent();



            var webViewForInit = webView;
            webViewForInit.Loaded += async (s, e) =>
            {
                try
                {
                    CoreWebView2Environment? env = null;
                    try
                    {
                        // Ưu tiên dùng CoreWebView2Environment dùng chung (pre-init)
                        env = await WebView2EnvironmentManager.GetSharedEnvironmentAsync();
                    }
                    catch (Exception envEx)
                    {
                        // Nếu shared env lỗi (ví dụ warm-up fail), fallback về CreateAsync như cũ
                        System.Diagnostics.Debug.WriteLine($"Shared WebView2 env (HTML UI) error, fallback per-node: {envEx.Message}");

                        var cachePathFallback = WebNodeCacheHelper.GetSharedRuntimeCachePath();
                        var optionsFallback = new CoreWebView2EnvironmentOptions();

                        if (GpuDetectionHelper.IsGpuAvailable)
                        {
                            var gpuArgs = new StringBuilder();
                            gpuArgs.Append("--enable-gpu-rasterization ");
                            gpuArgs.Append("--enable-zero-copy ");
                            gpuArgs.Append("--enable-features=VaapiVideoDecoder ");
                            gpuArgs.Append("--ignore-gpu-blacklist ");
                            gpuArgs.Append("--enable-accelerated-2d-canvas ");
                            gpuArgs.Append("--enable-accelerated-video-decode ");

                            optionsFallback.AdditionalBrowserArguments = gpuArgs.ToString();
                        }
                        else
                        {
                            optionsFallback.AdditionalBrowserArguments = "--disable-gpu";
                        }

                        env = await CoreWebView2Environment.CreateAsync(null, cachePathFallback, optionsFallback);
                    }

                    await EnsureCoreWebView2ThrottledAsync(webViewForInit, env);

                    var core = TryGetCoreSafe(webViewForInit);
                    if (core == null)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: CoreWebView2 is null after EnsureCoreWebView2Async");
                        return;
                    }
                    AttachProcessFailedHandler(webViewForInit, isTab1: false);

                    // ── WebResourceRequested: serve localfiles-*.local files directly ────────────
                    // SetVirtualHostNameToFolderMapping chỉ có hiệu lực với NAVIGATION MỚI (F5/reload),
                    // không áp dụng cho request trong page đang active → lần đầu load video bị
                    // ERR_NAME_NOT_RESOLVED. WebResourceRequested intercept TRƯỚC DNS resolution →
                    // serve file ngay lập tức từ _localFolderByHost dict, không cần mapping propagate.
                    core.AddWebResourceRequestedFilter(
                        "https://localfiles-*.local/*",
                        CoreWebView2WebResourceContext.All);
                    core.WebResourceRequested += (_, reqArgs) =>
                    {
                        try
                        {
                            if (!Uri.TryCreate(reqArgs.Request.Uri, UriKind.Absolute, out var reqUri)) return;
                            var hostName = reqUri.Host ?? string.Empty;
                            if (!hostName.StartsWith("localfiles-", StringComparison.OrdinalIgnoreCase) ||
                                !hostName.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return;
                            string? mappedFolder;
                            lock (_localHostMapSync)
                            {
                                _localFolderByHost.TryGetValue(hostName, out mappedFolder);
                            }
                            if (string.IsNullOrEmpty(mappedFolder)) return;

                            // Decode path; AbsolutePath không bao gồm query string.
                            // Browser (WebView2) có thể double-encode URL: %20 (space) → %2520 trong HTTP request.
                            // Double-unescape để handle cả single (%20→space) và double (%2520→%20→space) encoding.
                            var encodedPath = (reqUri.AbsolutePath ?? string.Empty).TrimStart('/');
                            var rawPath = Uri.UnescapeDataString(Uri.UnescapeDataString(encodedPath));
                            if (string.IsNullOrWhiteSpace(rawPath)) return;

                            var fullFilePath = Path.GetFullPath(Path.Combine(mappedFolder, rawPath));
                            // Security: chỉ serve file dưới registered folder
                            if (!fullFilePath.StartsWith(mappedFolder, StringComparison.OrdinalIgnoreCase)) return;
                            if (!File.Exists(fullFilePath)) return;

                            var mimeType = GetLocalFileMimeType(fullFilePath);
                            var fileLen = new FileInfo(fullFilePath).Length;

                            // Check Range header (cần cho video seeking)
                            string? rangeHdr = null;
                            try
                            {
                                foreach (var h in reqArgs.Request.Headers)
                                {
                                    if (string.Equals(h.Key, "Range", StringComparison.OrdinalIgnoreCase))
                                    { rangeHdr = h.Value; break; }
                                }
                            }
                            catch { }

                            Stream fs = File.OpenRead(fullFilePath);
                            int statusCode; string statusText; string respHeaders;

                            if (rangeHdr != null &&
                                TryParseByteRange(rangeHdr, fileLen, out var rStart, out var rEnd))
                            {
                                var rLen = rEnd - rStart + 1;
                                fs = new LimitedReadStream(fs, rStart, rLen);
                                statusCode = 206; statusText = "Partial Content";
                                respHeaders =
                                    $"Content-Type: {mimeType}\r\nContent-Length: {rLen}\r\n" +
                                    $"Content-Range: bytes {rStart}-{rEnd}/{fileLen}\r\nAccept-Ranges: bytes";
                            }
                            else
                            {
                                statusCode = 200; statusText = "OK";
                                respHeaders =
                                    $"Content-Type: {mimeType}\r\nContent-Length: {fileLen}\r\nAccept-Ranges: bytes";
                            }

                            reqArgs.Response = webViewForInit.CoreWebView2.Environment
                                .CreateWebResourceResponse(fs, statusCode, statusText, respHeaders);
                        }
                        catch { }
                    };

                    // ✅ Xử lý F5 / Ctrl+R để reload HTML và refresh params
                    // Note: AcceleratorKeyPressed không có sẵn trên CoreWebView2 class ở version này hoặc cần cast sang Controller.
                    // Thay vào đó, ta inject JS để bắt keydown và gửi message về C#.

                    // Lắng nghe message từ JS (acSubmit hoặc custom)
                    core.WebMessageReceived += async (_, args) =>
                    {
                        try
                        {
                            var json = args.WebMessageAsJson;
                            if (string.IsNullOrWhiteSpace(json))
                            {
                                await UpdateOutputsFromDomAsync();
                                return;
                            }

                            var trimmedJson = json.Trim();
                            if (!trimmedJson.StartsWith("{"))
                            {
                                // Non-object payload → fallback đọc DOM theo Params
                                await UpdateOutputsFromDomAsync();
                                return;
                            }

                            // Thử parse payload object: { result: '...', otherKey: '...' }
                            try
                            {
                                using var doc = JsonDocument.Parse(trimmedJson);
                                var root = doc.RootElement;

                                if (root.ValueKind != JsonValueKind.Object)
                                {
                                    await UpdateOutputsFromDomAsync();
                                    return;
                                }

                                // Kiểm tra có field 'type' không
                                string? messageType = null;
                                if (root.TryGetProperty("type", out var typeProp) &&
                                    typeProp.ValueKind == JsonValueKind.String)
                                {
                                    messageType = typeProp.GetString();
                                }

                                // ✅ Xử lý message: reload (từ F5/Ctrl+R JS)
                                if (string.Equals(messageType, "reload", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Reload code & params
                                    await ReloadHtmlAsync();

                                    // Re-push cached async data after reload
                                    await RepushAsyncDataHistoryAsync(webView, 200);
                                    return;
                                }

                                // Xử lý message đặc biệt: startWorkflow
                                if (string.Equals(messageType, "startWorkflow", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Trigger StartTestCommand từ ViewModel
                                    try
                                    {
                                        var vm = host.ViewModel;
                                        if (vm != null)
                                        {
                                            // Gọi StartTestCommand thông qua reflection
                                            var vmType = vm.GetType();
                                            var startTestMethod = vmType.GetMethod("StartTest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                            if (startTestMethod != null)
                                            {
                                                // Execute trên UI thread
                                                _ = webView.Dispatcher.InvokeAsync(async () =>
                                                {
                                                    try
                                                    {
                                                        var task = startTestMethod.Invoke(vm, null) as Task;
                                                        if (task != null)
                                                            await task;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        System.Diagnostics.Debug.WriteLine($"HTML UI StartWorkflow error: {ex.Message}");
                                                    }
                                                }, DispatcherPriority.Normal);
                                            }
                                            else
                                            {
                                                // Fallback: tìm StartTestCommand và execute
                                                var commandProp = vmType.GetProperty("StartTestCommand", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                if (commandProp?.GetValue(vm) is System.Windows.Input.ICommand cmd && cmd.CanExecute(null))
                                                {
                                                    _ = webView.Dispatcher.InvokeAsync(() =>
                                                    {
                                                        cmd.Execute(null);
                                                    }, DispatcherPriority.Normal);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"HTML UI StartWorkflow error: {ex.Message}");
                                    }
                                    return; // Không xử lý outputs cho message này
                                }

                                // Download video by executing provided curl command in host.
                                if (string.Equals(messageType, "download_curl", StringComparison.OrdinalIgnoreCase))
                                {
                                    string? curlCmd = null;
                                    string? desiredFileName = null;
                                    string? downloadKey = null;
                                    if (root.TryGetProperty("curl", out var curlProp) && curlProp.ValueKind == JsonValueKind.String)
                                        curlCmd = curlProp.GetString();
                                    if (root.TryGetProperty("fileName", out var fnProp) && fnProp.ValueKind == JsonValueKind.String)
                                        desiredFileName = fnProp.GetString();
                                    if (root.TryGetProperty("downloadKey", out var dkProp) && dkProp.ValueKind == JsonValueKind.String)
                                        downloadKey = dkProp.GetString();

                                    if (string.IsNullOrWhiteSpace(curlCmd))
                                        return;

                                    _ = Task.Run(async () =>
                                    {
                                        bool ok = false;
                                        string outPath = string.Empty;
                                        string errMsg = string.Empty;
                                        string localUrl = string.Empty;
                                        string savedFileName = string.Empty;
                                        try
                                        {
                                            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                                            var targetDir = System.IO.Path.Combine(userProfile, "Downloads", "Workflow_Downloads", "Videos");
                                            Directory.CreateDirectory(targetDir);

                                            var safeName = string.IsNullOrWhiteSpace(desiredFileName)
                                                ? $"video_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.mp4"
                                                : desiredFileName.Trim();
                                            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                                                safeName = safeName.Replace(c, '_');
                                            if (!safeName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                                                safeName += ".mp4";
                                            savedFileName = safeName;

                                            outPath = System.IO.Path.Combine(targetDir, safeName);

                                            // Parse curl text and download via HttpClient directly
                                            // to avoid cmd/shell escaping issues (\r\n, quotes, ^, etc.).
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
                                            var headers = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                            foreach (System.Text.RegularExpressions.Match hm in headerMatches)
                                            {
                                                var hv = hm.Groups[1].Value;
                                                var idx = hv.IndexOf(':');
                                                if (idx <= 0) continue;
                                                var hk = hv.Substring(0, idx).Trim();
                                                var vv = hv.Substring(idx + 1).Trim();
                                                if (!string.IsNullOrWhiteSpace(hk))
                                                    headers[hk] = vv;
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
                                            {
                                                await rs.CopyToAsync(fs);
                                            }

                                            ok = File.Exists(outPath) && new FileInfo(outPath).Length > 0;
                                            if (!ok) errMsg = "Downloaded file is empty.";
                                            if (ok)
                                            {
                                                try
                                                {
                                                    var dlHost = await EnsureLocalHostMappingAsync(targetDir);
                                                    localUrl = $"https://{dlHost}/{Uri.EscapeDataString(savedFileName)}";
                                                }
                                                catch { }
                                            }
                                        }
                                        catch (Exception exDl)
                                        {
                                            ok = false;
                                            errMsg = exDl.Message;
                                        }

                                        try
                                        {
                                            var payload = JsonSerializer.Serialize(new
                                            {
                                                ok,
                                                path = outPath,
                                                error = errMsg,
                                                key = downloadKey ?? string.Empty,
                                                localUrl
                                            });
                                            var jsNotify =
                                                "window.dispatchEvent(new CustomEvent('__ac_curl_download_done',{detail:" +
                                                payload +
                                                "}));";
                                            await webView.Dispatcher.InvokeAsync(async () =>
                                            {
                                                try
                                                {
                                                    var c = webView.CoreWebView2;
                                                    if (c != null) await c.ExecuteScriptAsync(jsNotify);
                                                }
                                                catch { }
                                            });
                                        }
                                        catch { }
                                    });
                                    return;
                                }

                                if (string.Equals(messageType, "pick_image_files", StringComparison.OrdinalIgnoreCase))
                                {
                                    string? requestId = null;
                                    if (root.TryGetProperty("requestId", out var reqIdProp) && reqIdProp.ValueKind == JsonValueKind.String)
                                        requestId = reqIdProp.GetString();

                                    _ = Task.Run(async () =>
                                    {
                                        bool ok = false;
                                        string errMsg = string.Empty;
                                        var filesPayload = new System.Collections.Generic.List<object>();
                                        try
                                        {
                                            var picked = await webView.Dispatcher.InvokeAsync(() =>
                                            {
                                                var dlg = new Microsoft.Win32.OpenFileDialog
                                                {
                                                    Title = "Chọn ảnh upload",
                                                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.svg;*.tif;*.tiff;*.ico|All Files|*.*",
                                                    Multiselect = true,
                                                    CheckFileExists = true,
                                                    CheckPathExists = true
                                                };
                                                var owner = Window.GetWindow(webView);
                                                var rs = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                                                return (rs == true) ? (dlg.FileNames ?? Array.Empty<string>()) : Array.Empty<string>();
                                            });

                                            foreach (var path in picked)
                                            {
                                                try
                                                {
                                                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                                                    var full = Path.GetFullPath(path);
                                                    var bytes = await File.ReadAllBytesAsync(full);
                                                    if (bytes == null || bytes.Length == 0) continue;
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
                                                catch { }
                                            }
                                            ok = filesPayload.Count > 0;
                                        }
                                        catch (Exception exPick)
                                        {
                                            ok = false;
                                            errMsg = exPick.Message;
                                        }

                                        try
                                        {
                                            var payload = JsonSerializer.Serialize(new
                                            {
                                                ok,
                                                requestId = requestId ?? string.Empty,
                                                files = filesPayload,
                                                error = errMsg
                                            });
                                            var jsNotify =
                                                "window.dispatchEvent(new CustomEvent('__ac_image_files_picked',{detail:" +
                                                payload +
                                                "}));";
                                            await webView.Dispatcher.InvokeAsync(async () =>
                                            {
                                                try
                                                {
                                                    var c = webView.CoreWebView2;
                                                    if (c != null) await c.ExecuteScriptAsync(jsNotify);
                                                }
                                                catch { }
                                            });
                                        }
                                        catch { }
                                    });
                                    return;
                                }

                                // Resolve an absolute local file path to internal virtual-host URL.
                                // Dùng localfiles.local — hostname duy nhất đã được WebView2 intercept ổn định.
                                if (string.Equals(messageType, "resolve_local_path", StringComparison.OrdinalIgnoreCase))
                                {
                                    string? localPath = null;
                                    string? requestId = null;
                                    if (root.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
                                        localPath = pathProp.GetString();
                                    if (root.TryGetProperty("requestId", out var reqIdProp) && reqIdProp.ValueKind == JsonValueKind.String)
                                        requestId = reqIdProp.GetString();

                                    _ = Task.Run(async () =>
                                    {
                                        bool ok = false;
                                        string localUrl = string.Empty;
                                        string errMsg = string.Empty;
                                        try
                                        {
                                            if (string.IsNullOrWhiteSpace(localPath))
                                                throw new InvalidOperationException("Path is empty.");
                                            if (!System.IO.Path.IsPathRooted(localPath))
                                                throw new InvalidOperationException("Path must be absolute.");
                                            if (!File.Exists(localPath))
                                                throw new FileNotFoundException("File not found.", localPath);

                                            var fullPath = System.IO.Path.GetFullPath(localPath);
                                            var folder = System.IO.Path.GetDirectoryName(fullPath) ?? string.Empty;
                                            var fileName = System.IO.Path.GetFileName(fullPath);
                                            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(fileName))
                                                throw new InvalidOperationException("Invalid local file path.");

                                            var localHost = await EnsureLocalHostMappingAsync(folder);
                                            localUrl = $"https://{localHost}/{Uri.EscapeDataString(fileName)}";
                                            ok = true;
                                        }
                                        catch (Exception exResolve)
                                        {
                                            ok = false;
                                            errMsg = exResolve.Message;
                                            System.Diagnostics.Debug.WriteLine($"[HtmlUiNode] resolve_local_path error: {exResolve.Message}");
                                        }

                                        try
                                        {
                                            var payload = JsonSerializer.Serialize(new
                                            {
                                                ok,
                                                requestId = requestId ?? string.Empty,
                                                path = localPath ?? string.Empty,
                                                localUrl,
                                                error = errMsg
                                            });
                                            var jsNotify =
                                                "window.dispatchEvent(new CustomEvent('__ac_local_path_resolved',{detail:" +
                                                payload +
                                                "}));";
                                            await webView.Dispatcher.InvokeAsync(async () =>
                                            {
                                                try
                                                {
                                                    var c = webView.CoreWebView2;
                                                    if (c != null) await c.ExecuteScriptAsync(jsNotify);
                                                }
                                                catch { }
                                            });
                                        }
                                        catch { }
                                    });
                                    return;
                                }

                                // Map https://localfiles.local/... hoặc https://downloads.local/... → file thật dưới Workflow_Downloads
                                // (trường hợp workflow chỉ đẩy URL ảo mà không có absolute path → chưa gọi resolve_local_path).
                                if (string.Equals(messageType, "resolve_playable_ref", StringComparison.OrdinalIgnoreCase))
                                {
                                    string? refUrl = null;
                                    string? playableRefRequestId = null;
                                    if (root.TryGetProperty("url", out var refUrlProp) && refUrlProp.ValueKind == JsonValueKind.String)
                                        refUrl = refUrlProp.GetString();
                                    if (root.TryGetProperty("requestId", out var playableReqProp) && playableReqProp.ValueKind == JsonValueKind.String)
                                        playableRefRequestId = playableReqProp.GetString();

                                    _ = Task.Run(async () =>
                                    {
                                        bool ok = false;
                                        string localUrl = string.Empty;
                                        string errMsg = string.Empty;
                                        string? resolvedPath = null;
                                        try
                                        {
                                            if (string.IsNullOrWhiteSpace(refUrl))
                                                throw new InvalidOperationException("URL is empty.");
                                            if (!Uri.TryCreate(refUrl.Trim(), UriKind.Absolute, out var uri))
                                                throw new InvalidOperationException("Invalid URL.");
                                            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                                                throw new InvalidOperationException("Unsupported URL scheme.");

                                            var host = uri.Host ?? string.Empty;
                                            if (!(string.Equals(host, "localfiles.local", StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(host, "downloads.local", StringComparison.OrdinalIgnoreCase) ||
                                                  host.StartsWith("localfiles-", StringComparison.OrdinalIgnoreCase)))
                                                throw new InvalidOperationException("Not an internal playable reference URL.");

                                            var rawPath = Uri.UnescapeDataString((uri.AbsolutePath ?? string.Empty).TrimStart('/'));
                                            if (string.IsNullOrWhiteSpace(rawPath))
                                                throw new InvalidOperationException("Empty path.");
                                            foreach (var segment in rawPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
                                            {
                                                if (segment is "." or "..")
                                                    throw new InvalidOperationException("Invalid path segment.");
                                            }

                                            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                                            var downloadsRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(userProfile, "Downloads"));
                                            var wfRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(downloadsRoot, "Workflow_Downloads"));

                                            bool IsUnderRoot(string fileFull, string rootFull)
                                            {
                                                try
                                                {
                                                    var f = System.IO.Path.GetFullPath(fileFull).TrimEnd(System.IO.Path.DirectorySeparatorChar);
                                                    var r = System.IO.Path.GetFullPath(rootFull).TrimEnd(System.IO.Path.DirectorySeparatorChar);
                                                    return f.Equals(r, StringComparison.OrdinalIgnoreCase) ||
                                                           f.StartsWith(r + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                                                }
                                                catch { return false; }
                                            }

                                            var allowBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                            void AddAllowBase(string? p)
                                            {
                                                if (string.IsNullOrWhiteSpace(p)) return;
                                                try
                                                {
                                                    var full = System.IO.Path.GetFullPath(p);
                                                    if (Directory.Exists(full)) allowBases.Add(full);
                                                }
                                                catch { /* ignore */ }
                                            }

                                            AddAllowBase(downloadsRoot);
                                            AddAllowBase(wfRoot);
                                            if (root.TryGetProperty("searchRoots", out var srArr) && srArr.ValueKind == JsonValueKind.Array)
                                            {
                                                foreach (var el in srArr.EnumerateArray())
                                                {
                                                    if (el.ValueKind == JsonValueKind.String)
                                                        AddAllowBase(el.GetString());
                                                }
                                            }

                                            bool IsUnderAnyAllowed(string fileFull)
                                            {
                                                foreach (var b in allowBases)
                                                {
                                                    if (IsUnderRoot(fileFull, b)) return true;
                                                }
                                                return false;
                                            }

                                            var rel = rawPath.Replace('/', System.IO.Path.DirectorySeparatorChar);
                                            string? fullPath = null;
                                            if (Directory.Exists(wfRoot))
                                            {
                                                var tryPaths = new[]
                                                {
                                                    System.IO.Path.Combine(wfRoot, rel),
                                                    System.IO.Path.Combine(wfRoot, "Videos", rel)
                                                };
                                                foreach (var tp in tryPaths)
                                                {
                                                    var cand = System.IO.Path.GetFullPath(tp);
                                                    if (File.Exists(cand) && IsUnderAnyAllowed(cand)) { fullPath = cand; break; }
                                                }
                                            }

                                            var baseName = System.IO.Path.GetFileName(rel);
                                            if (string.IsNullOrWhiteSpace(baseName))
                                                throw new FileNotFoundException("Could not locate file.", rawPath);

                                            if (fullPath == null && Directory.Exists(wfRoot))
                                            {
                                                try
                                                {
                                                    var hits = Directory.GetFiles(wfRoot, baseName, SearchOption.AllDirectories);
                                                    if (hits.Length > 0)
                                                    {
                                                        var h = System.IO.Path.GetFullPath(hits[0]);
                                                        if (IsUnderAnyAllowed(h)) fullPath = h;
                                                    }
                                                }
                                                catch (Exception scanEx)
                                                {
                                                    throw new FileNotFoundException("Could not locate file under Workflow_Downloads.", rawPath, scanEx);
                                                }
                                            }

                                            if (fullPath == null && Directory.Exists(downloadsRoot))
                                            {
                                                var flat = System.IO.Path.GetFullPath(System.IO.Path.Combine(downloadsRoot, baseName));
                                                if (File.Exists(flat) && IsUnderAnyAllowed(flat)) fullPath = flat;
                                            }

                                            // Các thư mục làm việc / repo (searchRoots + allowBases): tìm file phẳng hoặc đệ quy nông
                                            if (fullPath == null)
                                            {
                                                foreach (var baseDir in allowBases)
                                                {
                                                    if (string.Equals(baseDir, downloadsRoot, StringComparison.OrdinalIgnoreCase) ||
                                                        string.Equals(baseDir, wfRoot, StringComparison.OrdinalIgnoreCase))
                                                        continue;
                                                    try
                                                    {
                                                        var cand = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, baseName));
                                                        if (File.Exists(cand) && IsUnderAnyAllowed(cand))
                                                        {
                                                            fullPath = cand;
                                                            break;
                                                        }
                                                    }
                                                    catch { /* ignore */ }

                                                    try
                                                    {
                                                        var hits2 = Directory.GetFiles(baseDir, baseName, SearchOption.TopDirectoryOnly);
                                                        if (hits2.Length > 0)
                                                        {
                                                            var h = System.IO.Path.GetFullPath(hits2[0]);
                                                            if (IsUnderAnyAllowed(h)) { fullPath = h; break; }
                                                        }
                                                    }
                                                    catch { /* ignore */ }
                                                }
                                            }

                                            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                                                throw new FileNotFoundException("Video file not found (Downloads, Workflow_Downloads, hoặc thư mục gợi ý từ host).", rawPath);

                                            if (!IsUnderAnyAllowed(fullPath))
                                                throw new UnauthorizedAccessException("Resolved path is outside allowed search folders.");

                                            var folder = System.IO.Path.GetDirectoryName(fullPath) ?? string.Empty;
                                            var fileName = System.IO.Path.GetFileName(fullPath);
                                            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(fileName))
                                                throw new InvalidOperationException("Invalid resolved path.");

                                            var localHost = await EnsureLocalHostMappingAsync(folder);

                                            localUrl = $"https://{localHost}/{Uri.EscapeDataString(fileName)}";
                                            resolvedPath = fullPath;
                                            ok = true;
                                        }
                                        catch (Exception exRef)
                                        {
                                            ok = false;
                                            errMsg = exRef.Message;
                                        }

                                        try
                                        {
                                            var payload = JsonSerializer.Serialize(new
                                            {
                                                ok,
                                                requestId = playableRefRequestId ?? string.Empty,
                                                path = resolvedPath ?? string.Empty,
                                                localUrl,
                                                error = errMsg
                                            });
                                            var jsNotify =
                                                "window.dispatchEvent(new CustomEvent('__ac_local_path_resolved',{detail:" +
                                                payload +
                                                "}));";
                                            await webView.Dispatcher.InvokeAsync(async () =>
                                            {
                                                try
                                                {
                                                    var c = webView.CoreWebView2;
                                                    if (c != null) await c.ExecuteScriptAsync(jsNotify);
                                                }
                                                catch { }
                                            });
                                        }
                                        catch { }
                                    });
                                    return;
                                }

                                // ✅ Handle Tab2→Tab1 JS execution
                                if (string.Equals(messageType, "tab1_exec", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (root.TryGetProperty("js", out var jsProp2) && jsProp2.ValueKind == JsonValueKind.String)
                                    {
                                        var jsToRun = jsProp2.GetString() ?? string.Empty;
                                        var execMode = root.TryGetProperty("mode", out var modeProp)
                                            ? (modeProp.GetString() ?? "par") : "par";
                                        var wv1 = _webViewTab1;
                                        if (wv1?.CoreWebView2 != null && !string.IsNullOrWhiteSpace(jsToRun))
                                        {
                                            if (string.Equals(execMode, "seq", StringComparison.OrdinalIgnoreCase))
                                            {
                                                // Sequential: chạy tuần tự - request2 phải đợi request1 xong
                                                var jsCapture = jsToRun;
                                                var wv1Capture = wv1;
                                                _tab1SeqTask = _tab1SeqTask
                                                    .ContinueWith(_ =>
                                                    {
                                                        try
                                                        {
                                                            // Always marshal to Tab1 UI thread
                                                            return wv1Capture.Dispatcher.InvokeAsync(async () =>
                                                            {
                                                                try
                                                                {
                                                                    var c1Local = wv1Capture.CoreWebView2;
                                                                    if (c1Local != null) await c1Local.ExecuteScriptAsync(jsCapture);
                                                                }
                                                                catch { }
                                                            }).Task;
                                                        }
                                                        catch
                                                        {
                                                            return Task.CompletedTask;
                                                        }
                                                    }).Unwrap();
                                            }
                                            else
                                            {
                                                // Parallel: chạy song song, không đợi nhau
                                                try
                                                {
                                                    _ = wv1.Dispatcher.InvokeAsync(async () =>
                                                    {
                                                        try
                                                        {
                                                            var c1Local = wv1.CoreWebView2;
                                                            if (c1Local != null) await c1Local.ExecuteScriptAsync(jsToRun);
                                                        }
                                                        catch { }
                                                    });
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                    return;
                                }

                                // ✅ Handle Tab2→Tab1 JS execution (return value)
                                if (string.Equals(messageType, "tab1_exec_ret", StringComparison.OrdinalIgnoreCase))
                                {
                                    string? jobId = null;
                                    if (root.TryGetProperty("jobId", out var jobIdProp) && jobIdProp.ValueKind == JsonValueKind.String)
                                    {
                                        jobId = jobIdProp.GetString();
                                    }

                                    if (string.IsNullOrWhiteSpace(jobId))
                                        return;

                                    try { System.Diagnostics.Debug.WriteLine($"[HtmlUiNode][tab1_exec_ret] start jobId={jobId}"); } catch { }

                                    // Allow Tab2 to control how long we poll Tab1 for job completion.
                                    // This avoids Tab2 timing out first while C# still polls (leads to lastJob=null).
                                    int timeoutMsLocal = 90000;
                                    if (root.TryGetProperty("timeoutMs", out var timeoutProp))
                                    {
                                        try
                                        {
                                            if (timeoutProp.ValueKind == JsonValueKind.Number)
                                            {
                                                timeoutMsLocal = (int)Math.Round(timeoutProp.GetDouble());
                                            }
                                            else if (timeoutProp.ValueKind == JsonValueKind.String)
                                            {
                                                var s = timeoutProp.GetString();
                                                if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var parsed))
                                                    timeoutMsLocal = parsed;
                                            }
                                        }
                                        catch { /* ignore */ }
                                    }

                                    timeoutMsLocal = Math.Max(5000, Math.Min(600000, timeoutMsLocal));
                                    var pollTimeout = TimeSpan.FromMilliseconds(Math.Max(1000, timeoutMsLocal - 1000));

                                    async Task RejectToTab2Async(string jobIdLocal, string message)
                                    {
                                        try
                                        {
                                            var jobIdJsonLocal = JsonSerializer.Serialize(jobIdLocal);
                                            var errJsonLocal = JsonSerializer.Serialize(message);
                                            await webViewForInit.Dispatcher.InvokeAsync(async () =>
                                            {
                                                try
                                                {
                                                    await core.ExecuteScriptAsync(
                                                        $"window.__tab1_job_reject && window.__tab1_job_reject({jobIdJsonLocal}, {errJsonLocal});");
                                                }
                                                catch { }
                                            });
                                        }
                                        catch { }
                                    }

                                    if (root.TryGetProperty("js", out var jsProp2) && jsProp2.ValueKind == JsonValueKind.String)
                                    {
                                        var jsToRun = jsProp2.GetString() ?? string.Empty;
                                        var execMode = root.TryGetProperty("mode", out var modeProp)
                                            ? (modeProp.GetString() ?? "par") : "par";

                                        var wv1 = _webViewTab1;
                                        if (string.IsNullOrWhiteSpace(jsToRun))
                                        {
                                            _ = RejectToTab2Async(jobId, "tab1_exec_ret: missing js");
                                            return;
                                        }

                                        if (wv1?.CoreWebView2 != null)
                                        {
                                            var jobIdJson = JsonSerializer.Serialize(jobId);

                                            // WebView2 ExecuteScriptAsync KHÔNG await Promise.
                                            // Nếu user chạy `(async()=>{ ... })();` thì kết quả sẽ là `{}` (Promise object).
                                            // => Wrap thành job async ghi kết quả vào window.__ac_tab1_jobs[jobId] rồi poll lấy kết quả.
                                            async Task ExecAndReturnAsync(WebView2 wv1Local, string scriptLocal)
                                            {
                                                try
                                                {
                                                    try { System.Diagnostics.Debug.WriteLine($"[HtmlUiNode][tab1_exec_ret] ExecAndReturnAsync jobId={jobId} len={scriptLocal?.Length ?? 0}"); } catch { }

                                                    // IMPORTANT: do not interpolate raw JS into our wrapper script.
                                                    // Raw JS may contain quotes/newlines/backslashes and break the wrapper, causing "no payload" timeouts.
                                                    // Instead, JSON-encode it and eval inside Tab1.
                                                    var scriptJson = JsonSerializer.Serialize(scriptLocal ?? string.Empty);
                                                    // 1) Start job in Tab1 (sync return)
                                                    var startScript = $@"
(function() {{
  try {{
    var jobId = {jobIdJson};
    window.__ac_tab1_jobs = window.__ac_tab1_jobs || {{}};
    // payload=null => chưa xong; khi xong sẽ set payload là JSON string
    window.__ac_tab1_jobs[jobId] = {{ payload: null, ts: Date.now() }};
    (async function() {{
      try {{
        var r = await (0, eval)({scriptJson});
        window.__ac_tab1_jobs[jobId].payload = JSON.stringify({{ ok: true, result: r }});
        window.__ac_tab1_jobs[jobId].ts = Date.now();
      }} catch (e) {{
        window.__ac_tab1_jobs[jobId].payload = JSON.stringify({{ ok: false, error: (e && e.message) ? e.message : String(e) }});
        window.__ac_tab1_jobs[jobId].ts = Date.now();
      }}
    }})();
    return '__TAB1_JOB_STARTED__';
  }} catch (e2) {{
    return 'start_error: ' + ((e2 && e2.message) ? e2.message : String(e2));
  }}
}})();";

                                                    // Sometimes ExecuteScriptAsync can return null while the page is navigating/loading.
                                                    // So we retry a few times until the job object actually exists in Tab1.
                                                    string? startRaw = "null";
                                                    var startAttemptStart = DateTime.UtcNow;
                                                    for (int attempt = 0; attempt < 25; attempt++)
                                                    {
                                                        startRaw = await await wv1Local.Dispatcher.InvokeAsync(() =>
                                                        {
                                                            var c1Local = wv1Local.CoreWebView2;
                                                            return c1Local == null ? Task.FromResult("null") : c1Local.ExecuteScriptAsync(startScript);
                                                        });

                                                        try
                                                        {
                                                            if (attempt == 0 || attempt == 10 || attempt == 24)
                                                            {
                                                                var sr = (startRaw ?? "null");
                                                                if (sr.Length > 160) sr = sr.Substring(0, 160) + "...";
                                                                System.Diagnostics.Debug.WriteLine($"[HtmlUiNode][tab1_exec_ret] jobId={jobId} startRaw(attempt={attempt})={sr}");
                                                            }
                                                        }
                                                        catch { }

                                                        // If start_error => fail fast
                                                        if (!string.IsNullOrWhiteSpace(startRaw) &&
                                                            startRaw.Contains("start_error", StringComparison.OrdinalIgnoreCase))
                                                        {
                                                            var errJson = JsonSerializer.Serialize(startRaw);
                                                            await webViewForInit.Dispatcher.InvokeAsync(async () =>
                                                            {
                                                                try
                                                                {
                                                                    await core.ExecuteScriptAsync(
                                                                        $"window.__tab1_job_reject && window.__tab1_job_reject({jobIdJson}, {errJson});");
                                                                }
                                                                catch { }
                                                            });
                                                            return;
                                                        }

                                                        // Check if job object exists
                                                        var existsScript =
                                                            $"(function(){{ try{{ var jobId={jobIdJson}; var j=(window.__ac_tab1_jobs && window.__ac_tab1_jobs[jobId]) ? window.__ac_tab1_jobs[jobId] : null; return j ? '1' : '0'; }}catch(e){{ return '0'; }} }})();";
                                                        var existsRaw = await await wv1Local.Dispatcher.InvokeAsync(() =>
                                                        {
                                                            var c1Local = wv1Local.CoreWebView2;
                                                            return c1Local == null ? Task.FromResult("0") : c1Local.ExecuteScriptAsync(existsScript);
                                                        });

                                                        if (!string.IsNullOrWhiteSpace(existsRaw) && existsRaw.Contains("1", StringComparison.Ordinal))
                                                            break;

                                                        // Give Tab1 time to finish navigating/loading
                                                        await Task.Delay(200);

                                                        // Avoid spending too much time here (leave room for polling payload)
                                                        if (DateTime.UtcNow - startAttemptStart > TimeSpan.FromMilliseconds(4000))
                                                            break;
                                                    }

                                                    if (!string.IsNullOrWhiteSpace(startRaw) &&
                                                        startRaw.Contains("start_error", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        var errJson = JsonSerializer.Serialize(startRaw);
                                                        await webViewForInit.Dispatcher.InvokeAsync(async () =>
                                                        {
                                                            try
                                                            {
                                                                await core.ExecuteScriptAsync(
                                                                    $"window.__tab1_job_reject && window.__tab1_job_reject({jobIdJson}, {errJson});");
                                                            }
                                                            catch { }
                                                        });
                                                        return;
                                                    }

                                                    // If startScript truly failed, usually we get "start_error".
                                                    // Sometimes ExecuteScriptAsync may return null even if the script executed (no return value captured),
                                                    // so: only reject early when startRaw is non-empty AND does NOT include our marker.
                                                    // Otherwise keep polling for window.__ac_tab1_jobs[jobId].payload.
                                                    var startRawTrim = startRaw?.Trim();
                                                    if (!string.IsNullOrWhiteSpace(startRawTrim) &&
                                                        !startRawTrim.Equals("null", StringComparison.OrdinalIgnoreCase) &&
                                                        !startRawTrim.Contains("__TAB1_JOB_STARTED__", StringComparison.Ordinal))
                                                    {
                                                        string startRawPreview = startRawTrim ?? "null";
                                                        if (startRawPreview.Length > 220)
                                                            startRawPreview = startRawPreview.Substring(0, 220) + "...";
                                                        await RejectToTab2Async(jobId, "tab1_exec_ret: startScript missing marker (startRaw=" + startRawPreview + ")");
                                                        return;
                                                    }

                                                    // 2) Poll result from Tab1
                                                    // Important: chỉ coi là "xong" khi payload != null (không dùng truthy để tránh bị đánh nhầm).
                                                    var pollScript = $"(function(){{ try{{ var jobId={jobIdJson}; var j=(window.__ac_tab1_jobs && window.__ac_tab1_jobs[jobId]) ? window.__ac_tab1_jobs[jobId] : null; return (j && j.payload !== null && j.payload !== undefined) ? j.payload : null; }}catch(e){{ return null; }} }})();";
                                                    string? pollRaw = "null";
                                                    var start = DateTime.UtcNow;
                                                    while (DateTime.UtcNow - start < pollTimeout)
                                                    {
                                                        pollRaw = await await wv1Local.Dispatcher.InvokeAsync(() =>
                                                        {
                                                            var c1Local = wv1Local.CoreWebView2;
                                                            return c1Local == null ? Task.FromResult("null") : c1Local.ExecuteScriptAsync(pollScript);
                                                        });

                                                        if (!string.IsNullOrWhiteSpace(pollRaw) &&
                                                            !string.Equals(pollRaw, "null", StringComparison.OrdinalIgnoreCase))
                                                            break;

                                                        await Task.Delay(150);
                                                    }

                                                    try
                                                    {
                                                        var pr = (pollRaw ?? "null");
                                                        if (pr.Length > 200) pr = pr.Substring(0, 200) + "...";
                                                        System.Diagnostics.Debug.WriteLine($"[HtmlUiNode][tab1_exec_ret] jobId={jobId} pollRaw={pr}");
                                                    }
                                                    catch { }

                                                    if (string.IsNullOrWhiteSpace(pollRaw) ||
                                                        string.Equals(pollRaw, "null", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        var errJson = JsonSerializer.Serialize("Tab1 job timeout (no payload)");
                                                        await webViewForInit.Dispatcher.InvokeAsync(async () =>
                                                        {
                                                            try
                                                            {
                                                                await core.ExecuteScriptAsync(
                                                                    $"window.__tab1_job_reject && window.__tab1_job_reject({jobIdJson}, {errJson});");
                                                            }
                                                            catch { }
                                                        });
                                                        return;
                                                    }

                                                    var rawJson = pollRaw;

                                                    // Notify back to Tab2 (Tab2 UI thread)
                                                    await webViewForInit.Dispatcher.InvokeAsync(async () =>
                                                    {
                                                        try
                                                        {
                                                            try { System.Diagnostics.Debug.WriteLine($"[HtmlUiNode][tab1_exec_ret] resolve->Tab2 jobId={jobId}"); } catch { }
                                                            await core.ExecuteScriptAsync(
                                                                $"window.__tab1_job_resolve && window.__tab1_job_resolve({jobIdJson}, {rawJson});");
                                                        }
                                                        catch { }
                                                    });
                                                }
                                                catch (Exception ex)
                                                {
                                                    var errJson = JsonSerializer.Serialize(ex.Message);
                                                    try
                                                    {
                                                        try { System.Diagnostics.Debug.WriteLine($"[HtmlUiNode][tab1_exec_ret] reject->Tab2 jobId={jobId} err={ex.Message}"); } catch { }
                                                        await webViewForInit.Dispatcher.InvokeAsync(async () =>
                                                        {
                                                            try
                                                            {
                                                                await core.ExecuteScriptAsync(
                                                                    $"window.__tab1_job_reject && window.__tab1_job_reject({jobIdJson}, {errJson});");
                                                            }
                                                            catch { }
                                                        });
                                                    }
                                                    catch { }
                                                }
                                            }

                                            if (string.Equals(execMode, "seq", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var jsCapture = jsToRun;
                                                var wv1Capture = wv1;
                                                _tab1SeqTask = _tab1SeqTask
                                                    .ContinueWith(_ =>
                                                    {
                                                        try { return ExecAndReturnAsync(wv1Capture, jsCapture); }
                                                        catch { return Task.CompletedTask; }
                                                    }).Unwrap();
                                            }
                                            else
                                            {
                                                try { _ = ExecAndReturnAsync(wv1, jsToRun); } catch { }
                                            }
                                        }
                                        else
                                        {
                                            // Tab1 chưa ready → reject ngay để Tab2 khỏi timeout
                                            _ = RejectToTab2Async(jobId, "Tab1 chưa sẵn sàng (CoreWebView2=null). Hãy bật UseWebTab và chờ Tab1 load xong.");
                                        }
                                    }
                                    else
                                    {
                                        _ = RejectToTab2Async(jobId, "tab1_exec_ret: missing js property");
                                    }

                                    return;
                                }

                                // Xử lý message type: submit
                                bool isSubmitType = string.Equals(messageType, "submit", StringComparison.OrdinalIgnoreCase);
                                if (isSubmitType)
                                {
                                    // Có 'type: submit' → dùng Params để đọc DOM (cách 1)
                                    await UpdateOutputsFromDomAsync();
                                }
                                else
                                {
                                    // Không có 'type: submit' → coi toàn bộ keys là outputs (cách 2: JS tự map)
                                    bool hasOutputs = false;
                                    foreach (var prop in root.EnumerateObject())
                                    {
                                        var key = prop.Name?.Trim();
                                        if (string.IsNullOrWhiteSpace(key)) continue;
                                        if (string.Equals(key, "type", StringComparison.OrdinalIgnoreCase)) continue; // Bỏ qua type nếu có

                                        string valueStr = "";
                                        try
                                        {
                                            if (prop.Value.ValueKind == JsonValueKind.String)
                                                valueStr = prop.Value.GetString() ?? "";
                                            else if (prop.Value.ValueKind == JsonValueKind.Null)
                                                valueStr = "";
                                            else
                                                valueStr = prop.Value.GetRawText() ?? "";
                                        }
                                        catch
                                        {
                                            valueStr = prop.Value.ToString() ?? "";
                                        }

                                        node.ResolvedOutputs[key] = valueStr;
                                        if (node.DynamicOutputs != null)
                                        {
                                            var dyn = node.DynamicOutputs.FirstOrDefault(o =>
                                                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                                            if (dyn != null)
                                                dyn.UserValueOverride = valueStr;
                                        }
                                        hasOutputs = true;
                                    }

                                    if (hasOutputs)
                                    {
                                        // Sync UI & Execution Results
                                        host.RequestSyncDataPanels(immediate: false);
                                        try
                                        {
                                            var vm = host.ViewModel;
                                            if (vm != null)
                                            {
                                                var field = typeof(FlowMy.ViewModels.WorkflowEditorViewModel)
                                                    .GetField("_executionVisualizer",
                                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                                if (field?.GetValue(vm) is FlowMy.Services.Workflow.IWorkflowExecutionVisualizer visualizer)
                                                {
                                                    visualizer.RefreshSavedOutputs(new[] { node });
                                                }
                                            }
                                        }
                                        catch (Exception exVis)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"HTML UI RefreshSavedOutputs (payload) error: {exVis.Message}");
                                        }
                                    }
                                    else
                                    {
                                        // Không có outputs trong payload → fallback đọc DOM theo Params
                                        await UpdateOutputsFromDomAsync();
                                    }
                                }
                            }
                            catch (Exception parseEx)
                            {
                                // Lỗi parse JSON → fallback đọc DOM theo Params
                                System.Diagnostics.Debug.WriteLine($"HTML UI WebMessageReceived parse error: {parseEx.Message}");
                                await UpdateOutputsFromDomAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"HTML UI WebMessageReceived error: {ex.Message}");
                        }
                    };

                    // ✅ Inject window.__ac TRƯỚC khi page script chạy
                    // AddScriptToExecuteOnDocumentCreatedAsync đảm bảo script chạy TRƯỚC mọi script của trang
                    // (NavigationCompleted thì quá muộn – trang đã load script rồi)
                    const string acHelperScript = @"
(function() {
  if (window.__acHelperReady) return;
  window.__acHelperReady = true;
  window.__ac = { live: {}, _callbacks: [] };
  window.__ac.onUpdate = function() {
    var args = Array.prototype.slice.call(arguments);
    var cb = args[args.length - 1];
    if (typeof cb !== 'function') return;
    var keys = args.slice(0, -1);
    window.__ac._callbacks.push({ keys: keys, cb: cb });
    // Call once immediately with current values (handles late subscribers like app.js).
    try {
      var vals0 = keys.length > 0
        ? keys.map(function(k) { return window.__ac.live[k]; })
        : [window.__ac.live];
      setTimeout(function() { try { cb.apply(null, vals0); } catch(e) {} }, 0);
    } catch(e) {}
  };
  window.__acPush = function(key, value) {
    window.__ac.live[key] = value;
    var cbs = window.__ac._callbacks || [];
    for (var i = 0; i < cbs.length; i++) {
      var sub = cbs[i];
      try {
        if (sub.keys.length === 0 || sub.keys.indexOf(key) >= 0) {
          var vals = sub.keys.length > 0
            ? sub.keys.map(function(k) { return window.__ac.live[k]; })
            : [window.__ac.live];
          sub.cb.apply(null, vals);
        }
      } catch(e) {}
    }
  };
  window.__acPushAll = function(obj) {
    Object.keys(obj).forEach(function(k) { window.__ac.live[k] = obj[k]; });
    var cbs = window.__ac._callbacks || [];
    for (var i = 0; i < cbs.length; i++) {
      var sub = cbs[i];
      try {
        var vals = sub.keys.length > 0
          ? sub.keys.map(function(k) { return window.__ac.live[k]; })
          : [window.__ac.live];
        sub.cb.apply(null, vals);
      } catch(e) {}
    }
  };
})();
";
                    try
                    {
                        await core.AddScriptToExecuteOnDocumentCreatedAsync(acHelperScript);
                    }
                    catch (Exception acEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"HTML UI AddScriptToExecuteOnDocumentCreated error: {acEx.Message}");
                    }

                    // ✅ Inject window.__acAsync — namespace riêng cho Async Data Receiver
                    const string acAsyncScript = @"
(function() {
  if (window.__acAsyncReady) return;
  window.__acAsyncReady = true;
  window.__acAsync = { data: {}, _callbacks: [] };
  window.__acAsync.onReceive = function() {
    var args = Array.prototype.slice.call(arguments);
    var cb = args[args.length - 1];
    if (typeof cb !== 'function') return;
    if (args.length === 1) {
      // onReceive(function(allData) { ... }) — nhận tất cả
      window.__acAsync._callbacks.push({ keys: [], cb: cb });
      setTimeout(function() { try { cb(window.__acAsync.data); } catch(e) {} }, 0);
    } else {
      // onReceive('key1', 'key2', function(v1, v2) { ... })
      var keys = args.slice(0, -1);
      window.__acAsync._callbacks.push({ keys: keys, cb: cb });
      var vals0 = keys.map(function(k) { return window.__acAsync.data[k]; });
      setTimeout(function() { try { cb.apply(null, vals0); } catch(e) {} }, 0);
    }
  };
  window.__acAsyncPush = function(key, value) {
    window.__acAsync.data[key] = value;
    var cbs = window.__acAsync._callbacks || [];
    for (var i = 0; i < cbs.length; i++) {
      var sub = cbs[i];
      try {
        if (sub.keys.length === 0) {
          sub.cb(window.__acAsync.data);
        } else if (sub.keys.indexOf(key) >= 0) {
          var vals = sub.keys.map(function(k) { return window.__acAsync.data[k]; });
          sub.cb.apply(null, vals);
        }
      } catch(e) {}
    }
  };
  window.__acAsyncPushAll = function(obj) {
    Object.keys(obj).forEach(function(k) { window.__acAsync.data[k] = obj[k]; });
    var cbs = window.__acAsync._callbacks || [];
    for (var i = 0; i < cbs.length; i++) {
      var sub = cbs[i];
      try {
        var vals = sub.keys.length > 0
          ? sub.keys.map(function(k) { return window.__acAsync.data[k]; })
          : [window.__acAsync.data];
        sub.cb.apply(null, vals);
      } catch(e) {}
    }
  };
})();
";
                    try
                    {
                        await core.AddScriptToExecuteOnDocumentCreatedAsync(acAsyncScript);
                    }
                    catch (Exception acAsyncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"HTML UI AddScript __acAsync error: {acAsyncEx.Message}");
                    }

                    // ✅ Inject Tab2→Tab1 JS bridge: gửi JS từ Tab2 (webView) để chạy trong Tab1 (_webViewTab1)
                    var tab1BridgeScript = @"(function() {
  if (window.__tab1BridgeReady) return;
  window.__tab1BridgeReady = true;

  // Cờ để Tab2 biết có thể sử dụng Tab1 WebView2 hay không
  window.__tab1_available = " + (node.UseWebTab ? "true" : "false") + @";
  // Sequential: request2 đợi request1 hoàn thành rồi mới chạy
  window.__tab1_exec_seq = function(js) {
    if (window.chrome && window.chrome.webview)
      window.chrome.webview.postMessage({ type: 'tab1_exec', js: js, mode: 'seq' });
  };
  // Parallel: các request chạy đồng thời, không đợi nhau
  window.__tab1_exec_par = function(js) {
    if (window.chrome && window.chrome.webview)
      window.chrome.webview.postMessage({ type: 'tab1_exec', js: js, mode: 'par' });
  };

  // Sequential (return): chạy trong Tab1 và trả kết quả về Tab2 qua __tab1_job_resolve/__tab1_job_reject
  window.__tab1_exec_seq_ret = function(jobId, js, timeoutMs) {
    if (window.chrome && window.chrome.webview)
      window.chrome.webview.postMessage({ type: 'tab1_exec_ret', jobId: jobId, js: js, mode: 'seq', timeoutMs: timeoutMs });
  };
  // Parallel (return)
  window.__tab1_exec_par_ret = function(jobId, js, timeoutMs) {
    if (window.chrome && window.chrome.webview)
      window.chrome.webview.postMessage({ type: 'tab1_exec_ret', jobId: jobId, js: js, mode: 'par', timeoutMs: timeoutMs });
  };
})();";
                    try { await core.AddScriptToExecuteOnDocumentCreatedAsync(tab1BridgeScript); } catch { }

                    // Track trạng thái load để user biết reload đã chạy hay chưa.
                    core.NavigationStarting += (_, _) =>
                    {
                        webViewForInit.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            SetTopBarStatus("Loading page...", false);
                        }), DispatcherPriority.Background);
                    };

                    // NavigationCompleted chỉ dùng để start auto-refresh timers sau khi page đã load
                    core.NavigationCompleted += (_, navArgs) =>
                    {
                        if (navArgs.IsSuccess)
                        {
                            StartAutoRefreshTimers();
                            // ✅ Re-push async data cache khi page reload (F5) để JS nhận lại dữ liệu đã có
                            webViewForInit.Dispatcher.BeginInvoke(new Action(async () =>
                            {
                                SetTopBarStatus("Load done", true, 1000);
                                try
                                {
                                    await RepushAsyncDataHistoryAsync(webViewForInit, 100);
                                }
                                catch (Exception repushEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"HTML UI async data re-push error: {repushEx.Message}");
                                }
                            }), DispatcherPriority.Background);
                        }
                        else
                        {
                            webViewForInit.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                SetTopBarStatus("Load failed", true, 2500);
                            }), DispatcherPriority.Background);
                        }
                    };

                    // Load HTML từ các tab
                    await WebCookiePortableBridge.TryConsumeAndApplyAsync(core.CookieManager);
                    await ReloadHtmlAsync();

                    webViewForInit.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        webViewForInit.Visibility = Visibility.Visible;
                        // ✅ Set zoom dựa trên canvas zoom hiện tại (để giữ tỉ lệ)
                        UpdateWebViewZoomForCanvasZoom();
                    }), DispatcherPriority.Loaded);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"HTML UI WebView2 init error: {ex.Message}");
                }
                finally
                {
                    if (webViewForInit.Dispatcher.CheckAccess())
                    {
                        webViewForInit.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        webViewForInit.Dispatcher.Invoke(() =>
                        {
                            webViewForInit.Visibility = Visibility.Visible;
                        });
                    }
                }
            };

            // ✅ Dừng tất cả auto-refresh timers khi WebView2 bị remove khỏi visual tree
            // (ví dụ: node bị xóa, workflow đóng) để tránh ObjectDisposedException
            webView.Unloaded += (_, _) =>
            {
                StopAutoRefreshTimers();
                StopSleepModeTimer();
            };

            var bottomBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Bottom
            };

            // Grid bottom: cột trái = zoom control, cột phải = text mô tả
            var bottomGrid = new Grid();
            bottomGrid.VerticalAlignment = VerticalAlignment.Bottom;
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Zoom panel (bottom-left)
            var zoomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var zoomLabel = new TextBlock
            {
                Text = "Zoom:",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var zoomComboBox = new ComboBox
            {
                Width = 70,
                Height = 20,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            void AddZoomItem(double factor)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{factor * 100:0}%",
                    Tag = factor
                };
                zoomComboBox.Items.Add(item);
            }

            // Danh sách preset zoom, được thêm theo thứ tự tăng dần để UI đẹp
            zoomComboBox.Items.Clear();
            AddZoomItem(0.05);
            AddZoomItem(0.10);
            AddZoomItem(0.20);
            AddZoomItem(0.40);
            AddZoomItem(0.50);
            AddZoomItem(0.75);
            AddZoomItem(1.00);
            AddZoomItem(1.25);
            AddZoomItem(1.50);
            AddZoomItem(2.00);

            // TextBox hiển thị phần trăm hiện tại
            var zoomTextBox = new TextBox
            {
                Width = 55,
                Height = 20,
                Margin = new Thickness(0, 0, 4, 0),
                IsReadOnly = true,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            void ApplyHtmlZoom(double z)
            {
                // Giới hạn zoom khi apply từ UI: min 0.1, max 5.0
                z = Math.Max(0.1, Math.Min(5.0, z));
                if (z <= 0) return;
                try
                {
                    node.CssZoom = z;
                    var coreLocal = webView.CoreWebView2;
                    if (coreLocal != null)
                    {
                        var script = $@"
                            (function() {{
                                document.body.style.zoom = '{z.ToString(System.Globalization.CultureInfo.InvariantCulture)}';
                                if (!document.body.style.zoom) {{
                                    document.body.style.transform = 'scale({z.ToString(System.Globalization.CultureInfo.InvariantCulture)})';
                                    document.body.style.transformOrigin = 'top left';
                                }}
                            }})();
                        ";
                        coreLocal.ExecuteScriptAsync(script);
                    }

                    // Cập nhật textbox hiển thị %
                    zoomTextBox.Text = $"{z * 100:0.#}%";

                    // Đồng bộ lại selection của ComboBox nếu trùng preset
                    foreach (var item in zoomComboBox.Items.OfType<ComboBoxItem>())
                    {
                        if (item.Tag is double d && Math.Abs(d - z) < 0.0001)
                        {
                            zoomComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi Apply HTML UI zoom: {ex.Message}");
                }
            }

            zoomComboBox.SelectionChanged += (s, e) =>
            {
                if (zoomComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is double z)
                {
                    ApplyHtmlZoom(z);
                }
            };

            double GetCurrentZoom()
            {
                if (node.CssZoom > 0) return node.CssZoom;
                return 1.0;
            }

            double GetNextPreset(double current, bool increase)
            {
                // Chỉ dùng preset trong khoảng [0.1, 5.0] cho nút +/-.
                var presets = zoomComboBox.Items.OfType<ComboBoxItem>()
                    .Select(i => i.Tag)
                    .OfType<double>()
                    .Where(v => v >= 0.1 && v <= 5.0)
                    .OrderBy(v => v)
                    .ToList();
                if (presets.Count == 0) return current;

                if (increase)
                {
                    foreach (var p in presets)
                        if (p - current > 0.0001) return p;
                    return presets.Last();
                }
                else
                {
                    for (int i = presets.Count - 1; i >= 0; i--)
                        if (current - presets[i] > 0.0001) return presets[i];
                    return presets.First();
                }
            }

            var minusButton = new Button
            {
                Content = "−",
                Width = 22,
                Height = 20,
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            minusButton.Click += (s, e) =>
            {
                var current = GetCurrentZoom();
                var next = GetNextPreset(current, increase: false);
                ApplyHtmlZoom(next);
            };

            var plusButton = new Button
            {
                Content = "+",
                Width = 22,
                Height = 20,
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            plusButton.Click += (s, e) =>
            {
                var current = GetCurrentZoom();
                var next = GetNextPreset(current, increase: true);
                ApplyHtmlZoom(next);
            };

            // Chọn giá trị khởi đầu theo CssZoom / mặc định 100%
            double initialZoom = node.CssZoom > 0 ? node.CssZoom : 1.0;
            foreach (var item in zoomComboBox.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is double d && Math.Abs(d - initialZoom) < 0.0001)
                {
                    zoomComboBox.SelectedItem = item;
                    break;
                }
            }
            if (zoomComboBox.SelectedItem == null)
            {
                var customItem = new ComboBoxItem
                {
                    Content = $"{initialZoom * 100:0}%",
                    Tag = initialZoom
                };
                zoomComboBox.Items.Add(customItem);
                zoomComboBox.SelectedItem = customItem;
            }

            // Khởi tạo text hiển thị zoom ban đầu
            zoomTextBox.Text = $"{initialZoom * 100:0.#}%";

            zoomPanel.Children.Add(zoomLabel);
            zoomPanel.Children.Add(minusButton);
            zoomPanel.Children.Add(zoomComboBox);
            zoomPanel.Children.Add(plusButton);
            zoomPanel.Children.Add(zoomTextBox);
            Grid.SetColumn(zoomPanel, 0);
            bottomGrid.Children.Add(zoomPanel);

            var bottomText = new TextBlock
            {
                Text = "HTML UI • Chuột phải để mở cấu hình",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(bottomText, 1);
            bottomGrid.Children.Add(bottomText);

            bottomBar.Child = bottomGrid;
            Grid.SetRow(bottomBar, 2);
            grid.Children.Add(bottomBar);
            bottomBarCapture = bottomBar; // gán để lambda viewportExpandBtn.Click capture đúng tham chiếu

            var handleOverlay = new Grid();
            AddResizeHandle(handleOverlay, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 2, 0, 0));

            var outerGrid = new Grid();
            outerGrid.Children.Add(grid);
            outerGrid.Children.Add(handleOverlay);

            GpuOptimizationHelper.ApplyToElement(outerGrid);

            border.Child = outerGrid;

            bool isResizing = false;
            ResizeDirection currentDir = ResizeDirection.None;
            Point resizeStart = default;
            double origW = 0, origH = 0, origX = 0, origY = 0;

            border.PreviewMouseDown += (s, e) =>
            {
                if (e.OriginalSource is Ellipse handle && handle.Tag is ResizeDirection dir)
                {
                    isResizing = true;
                    currentDir = dir;
                    resizeStart = e.GetPosition(border.Parent as UIElement);
                    // Dùng ActualWidth/ActualHeight (kích thước render thực tế) thay vì node.Width/Height
                    // Tránh trường hợp node.Height < MinHeight (từ dữ liệu cũ) gây dead zone khi drag
                    origW = border.ActualWidth > 0 ? border.ActualWidth : Math.Max(border.MinWidth, node.Width);
                    origH = border.ActualHeight > 0 ? border.ActualHeight : Math.Max(border.MinHeight, node.Height);
                    origX = node.X;
                    origY = node.Y;
                    border.CaptureMouse();
                    e.Handled = true;
                }
            };

            border.PreviewMouseMove += (s, e) =>
            {
                if (!isResizing) return;
                var pos = e.GetPosition(border.Parent as UIElement);
                var dx = pos.X - resizeStart.X;
                var dy = pos.Y - resizeStart.Y;
                double newX = origX, newY = origY, newW = origW, newH = origH;
                var minH = border.MinHeight > 0 ? border.MinHeight : 200.0;
                var minW = border.MinWidth > 0 ? border.MinWidth : 280.0;
                switch (currentDir)
                {
                    case ResizeDirection.BottomRight:
                        newW = Math.Max(minW, origW + dx);
                        newH = Math.Max(minH, origH + dy);
                        break;
                    case ResizeDirection.TopLeft:
                        newW = Math.Max(minW, origW - dx);
                        newH = Math.Max(minH, origH - dy);
                        newX = origX + (origW - newW);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.TopRight:
                        newW = Math.Max(minW, origW + dx);
                        newH = Math.Max(minH, origH - dy);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.BottomLeft:
                        newW = Math.Max(minW, origW - dx);
                        newH = Math.Max(minH, origH + dy);
                        newX = origX + (origW - newW);
                        break;
                    case ResizeDirection.Right:
                        newW = Math.Max(minW, origW + dx);
                        break;
                    case ResizeDirection.Left:
                        newW = Math.Max(minW, origW - dx);
                        newX = origX + (origW - newW);
                        break;
                    case ResizeDirection.Bottom:
                        newH = Math.Max(minH, origH + dy);
                        break;
                    case ResizeDirection.Top:
                        newH = Math.Max(minH, origH - dy);
                        newY = origY + (origH - newH);
                        break;
                }
                node.Width = newW;
                node.Height = newH;
                node.X = newX;
                node.Y = newY;
                border.Width = newW;
                border.Height = newH;
                if (host.WorkflowCanvas != null)
                {
                    Canvas.SetLeft(border, newX);
                    Canvas.SetTop(border, newY);
                }
                e.Handled = true;
            };

            border.PreviewMouseUp += (s, e) =>
            {
                if (isResizing) { isResizing = false; border.ReleaseMouseCapture(); e.Handled = true; }
            };

            // WebView2 (HwndHost) có thể gọi SetCapture() trên Win32 HWND của nó, làm mất WPF mouse capture
            // Khi đang resize, recapture ngay lập tức để đảm bảo PreviewMouseMove tiếp tục nhận events
            border.LostMouseCapture += (s, e) =>
            {
                if (isResizing)
                    border.CaptureMouse();
            };

            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "HTML UI",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTitleBrush(node),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            void ApplyHtmlUiTopBarExpandedLook(bool expanded)
            {
                if (expanded)
                {
                    // Chỉ hơi gọn hơn bình thường; scale chính nằm ở LayoutTransform (tránh 0.5 + font bé = “bé tẹo”).
                    topBar.Padding = new Thickness(6, 3, 6, 3);
                    topBarText.FontSize = 11;
                    viewportExpandBtn.Height = 20;
                    viewportExpandBtn.Width = 26;
                    viewportExpandBtn.FontSize = 12;
                    viewportExpandBtn.Margin = new Thickness(5, 0, 0, 0);
                }
                else
                {
                    topBar.Padding = new Thickness(6, 4, 6, 4);
                    topBarText.FontSize = 12;
                    viewportExpandBtn.Height = 24;
                    viewportExpandBtn.Width = 28;
                    viewportExpandBtn.FontSize = 14;
                    viewportExpandBtn.Margin = new Thickness(6, 0, 0, 0);
                }
            }

            void RefreshHtmlUiChromeScale()
            {
                var heightBaseline = border.MinHeight > 0 ? border.MinHeight : 200.0;
                var rawScale = heightBaseline > 0 ? node.Height / heightBaseline : 1.0;
                var topBottomScaleFactor = Math.Max(1.0, rawScale);

                // Top bar + tab + địa chỉ luôn cùng factor với bottom/handle (kể cả phóng to viewport).
                // Trước đây khi expanded cố định ~0.78 và tab=1 trong khi factor theo Height có thể >>1 → topbar nhìn như vài px.
                var chromeScale = new ScaleTransform(topBottomScaleFactor, topBottomScaleFactor);
                if (topBarChromeRow != null)
                    topBarChromeRow.LayoutTransform = chromeScale;
                if (_tabControl?.Tag is (Grid tabBarG, Grid addrPanelG))
                {
                    tabBarG.LayoutTransform = chromeScale;
                    addrPanelG.LayoutTransform = chromeScale;
                }

                bottomGrid.LayoutTransform = new ScaleTransform(topBottomScaleFactor, topBottomScaleFactor);
                UpdateInteractionVisualScale(handleOverlay, node, topBottomScaleFactor);
            }

            border.Focusable = true;
            border.FocusVisualStyle = null;

            bool isHovering = false;
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = node.Title ?? "HTML UI";
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitlePosition(titleTextBlock, border, host);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(HtmlUiNode.TitleDisplayMode))
                    {
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    }
                    else if (e.PropertyName == nameof(HtmlUiNode.TitleColorMode) || e.PropertyName == nameof(HtmlUiNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(HtmlUiNode.IsViewportExpanded))
                    {
                        ApplyHtmlUiTopBarExpandedLook(node.IsViewportExpanded);
                        RefreshHtmlUiChromeScale();
                    }
                    else if (e.PropertyName == nameof(HtmlUiNode.Width) || e.PropertyName == nameof(HtmlUiNode.Height))
                    {
                        if (s == node && !isResizing)
                        {
                            border.Width = node.Width;
                            border.Height = node.Height;
                        }

                        // Scale UI elements ở topBar và bottomBar theo Height
                        // Dùng công thức tuyệt đối: scale = node.Height / heightBaseline
                        // Chạy cả khi đang resize (PropertyChanged do PreviewMouseMove trigger) để scale liên tục
                        if (e.PropertyName == nameof(HtmlUiNode.Height))
                        {
                            RefreshHtmlUiChromeScale();
                        }
                    }
                };
            }

            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (isHovering) border.Focus(); }));
            };
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            };

            // Keyboard Port Position: Arrow = Port IN, Shift+Arrow = Port OUT
            border.PreviewKeyDown += (s, e) =>
            {
                if (!isHovering) return;
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PortPosition? newPos = e.Key switch
                {
                    Key.Left  => PortPosition.Left,
                    Key.Up    => PortPosition.Top,
                    Key.Right => PortPosition.Right,
                    Key.Down  => PortPosition.Bottom,
                    _ => null
                };
                if (newPos == null) return;
                e.Handled = true;
                ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
            };

            var visibilityDescriptor = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                    titleTextBlock.Visibility = Visibility.Collapsed;
                else
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            });

            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                ApplyHtmlUiTopBarExpandedLook(node.IsViewportExpanded);
                RefreshHtmlUiChromeScale();
            };
            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);
            border.Unloaded += (s, e) =>
            {
                try
                {
                    // ✅ Dừng tất cả auto-refresh timers
                    StopAutoRefreshTimers();
                    StopSleepModeTimer();

                    try
                    {
                        if (webView.CoreWebView2 != null)
                            webView.CoreWebView2.Navigate("about:blank");
                    }
                    catch { }
                    try { webView.Dispose(); } catch { }

                    if (_titleUpdateTimers.TryGetValue(border, out var t)) { t.Stop(); _titleUpdateTimers.Remove(border); }
                    _titleUpdatedAfterZoom.Remove(border);
                    _viewportExpandRestore.Remove(border);
                    // ✅ Cleanup WebView sync timer
                    if (_webViewSyncTimers.TryGetValue(border, out var syncTimer)) { syncTimer.Stop(); _webViewSyncTimers.Remove(border); }
                    // ✅ Cleanup Tab1 WebView2 và auto-refresh timer khi UseWebTab
                    StopWebTabAutoRefreshTimer();
                    if (_webViewTab1 != null)
                    {
                        try { if (_webViewTab1.CoreWebView2 != null) _webViewTab1.CoreWebView2.Navigate("about:blank"); } catch { }
                        try { _webViewTab1.Dispose(); } catch { }
                    }
                    scaleDescriptor?.RemoveValueChanged(host.ScaleTransform, scaleChangedHandler);
                    translateXDescriptor?.RemoveValueChanged(host.TranslateTransform, translateChangedHandler);
                    translateYDescriptor?.RemoveValueChanged(host.TranslateTransform, translateChangedHandler);
                    System.Windows.Media.CompositionTarget.Rendering -= renderingHandler;
                    if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
                        host.WorkflowCanvas.Children.Remove(titleTextBlock);
                    if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                        node.TitleTextBlockUI = null;
                }
                catch { }
            };

            border.LayoutUpdated += (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    return;
                }

                bool isZooming = NodeChrome.IsZooming;

                if (isZooming)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    _titleUpdatedAfterZoom[border] = false;
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    return;
                }

                // ✅ Return sớm khi đang drag node để tránh chạy logic không cần thiết (giống WebNodeControl)
                // Ẩn WebView2 khi đang drag để tránh lag (giống WebNodeControl)
                bool isPanningOrDragging = host.DraggedNode == node || host.IsPanning;

                if (isPanningOrDragging)
                {
                    // ✅ Ẩn WebView2 khi đang pan/drag để tránh lag (giống WebNodeControl)
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    return; // Return sớm để tránh chạy logic không cần thiết (giống WebNodeControl)
                }

                if (!_titleUpdatedAfterZoom.TryGetValue(border, out var up) || !up)
                {
                    _titleUpdatedAfterZoom[border] = true;
                    if (webView.Visibility != Visibility.Visible)
                        webView.Visibility = Visibility.Visible;
                    SyncWebViewPosition();

                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    if (titleTextBlock.Visibility == Visibility.Visible)
                        UpdateTitlePosition(titleTextBlock, border, host);
                }
                else
                {
                    // ✅ Hiển thị lại và sync WebView2 sau khi dừng pan/drag (giống WebNodeControl)
                    if (webView.Visibility != Visibility.Visible)
                    {
                        webView.Visibility = Visibility.Visible;
                        SyncWebViewPosition();
                    }
                }

                if (titleTextBlock.Visibility == Visibility.Visible)
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
            };

            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                // ✅ Clear DraggedNode ngay lập tức khi chuột phải để WebView2 không bị ẩn
                if (node.Border != null && node.Border.IsMouseCaptured)
                    node.Border.ReleaseMouseCapture();
                host.DraggedNode = null;
                if (host.ViewModel != null)
                    host.ViewModel.SelectedNode = null;
                OpenNodeDialog(node, host, ownerWindow);
            };

            RestartSleepModeTimer();

            // Mark activity on common interactions so "idle" works as expected.
            border.MouseDown += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };
            border.MouseMove += (_, _) => { MarkActivity(); };
            border.MouseWheel += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };
            webView.PreviewMouseDown += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };
            webView.PreviewMouseWheel += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };

            return border;
        }

        private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = direction,
                Cursor = GetCursorForResizeDirection(direction),
                CacheMode = null
            };

            GpuOptimizationHelper.ApplyToShape(handle);

            grid.Children.Add(handle);
        }

        private static void UpdateInteractionVisualScale(Grid handleOverlay, WorkflowNode node, double rawScale)
        {
            // Tăng nhẹ để resize handles/ports dễ nhìn khi node được phóng to.
            var visualScale = Math.Max(1.0, Math.Min(2.8, rawScale * 1.2));

            if (handleOverlay != null)
            {
                foreach (var child in handleOverlay.Children)
                {
                    if (child is Ellipse handle && handle.Tag is ResizeDirection)
                    {
                        handle.RenderTransformOrigin = new Point(0.5, 0.5);
                        handle.RenderTransform = new ScaleTransform(visualScale, visualScale);
                    }
                }
            }

            if (node?.Ports != null)
            {
                foreach (var p in node.Ports)
                {
                    if (p?.PortUI is FrameworkElement portUi)
                    {
                        portUi.RenderTransformOrigin = new Point(0.5, 0.5);
                        portUi.RenderTransform = new ScaleTransform(visualScale, visualScale);
                    }
                }
            }
        }

        private static Cursor GetCursorForResizeDirection(ResizeDirection direction)
        {
            return direction switch
            {
                ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
                ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
                _ => Cursors.Arrow
            };
        }

        private static Brush GetTitleBrush(HtmlUiNode node)
        {
            if (node.TitleColorMode != TitleColorMode.CustomColor || string.IsNullOrEmpty(node.TitleColorKey) || node.TitleColorKey == "NodeColor")
                return node.NodeBrush;
            if (node.TitleColorKey == "LimeGreen") return new SolidColorBrush(Colors.LimeGreen);
            var brush = Application.Current.TryFindResource(node.TitleColorKey) as Brush;
            return brush ?? node.NodeBrush;
        }

        private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering)
        {
            return mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };
        }

        private static void UpdateTitleVisibility(TextBlock tb, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
        {
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible) { tb.Visibility = Visibility.Collapsed; return; }
            tb.Visibility = GetTitleVisibility(mode, isHovering);
        }

        private static void ThrottledUpdateTitlePosition(TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (!_titleUpdateTimers.TryGetValue(border, out var timer))
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
                timer.Tick += (s, e) => { timer.Stop(); UpdateTitlePosition(tb, border, host); };
                _titleUpdateTimers[border] = timer;
            }
            timer.Stop();
            timer.Start();
        }

        private static void UpdateTitlePosition(TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (host.WorkflowCanvas == null || !host.WorkflowCanvas.Children.Contains(tb)) return;
            if (border == null) return;

            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);

            // Fallback: lấy từ node nếu Canvas position chưa được set
            if (double.IsNaN(left) && border.Tag is WorkflowNode n) left = n.X;
            if (double.IsNaN(top) && border.Tag is WorkflowNode n2) top = n2.Y;

            // Fallback cuối cùng: dùng 0 nếu vẫn không có giá trị
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            // Measure nếu chưa có kích thước
            if (tb.ActualWidth == 0 || tb.ActualHeight == 0)
            {
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                tb.Arrange(new Rect(tb.DesiredSize));
            }

            // Tính toán và set position
            var titleLeft = left + (border.ActualWidth / 2) - (tb.ActualWidth / 2);
            var titleTop = top - tb.ActualHeight - 4;

            Canvas.SetLeft(tb, titleLeft);
            Canvas.SetTop(tb, titleTop);

            // Force update để đảm bảo hiển thị ngay lập tức
            tb.InvalidateVisual();
        }

        private static Rect GetWorkflowViewportCanvasRect(IWorkflowEditorHost host)
        {
            var sv = host.ScrollViewer;
            if (sv == null) return Rect.Empty;
            try { sv.UpdateLayout(); } catch { /* ignore */ }
            double scrollX = sv.HorizontalOffset;
            double scrollY = sv.VerticalOffset;
            double vw = sv.ViewportWidth > 1 ? sv.ViewportWidth : sv.ActualWidth;
            double vh = sv.ViewportHeight > 1 ? sv.ViewportHeight : sv.ActualHeight;
            if (vw < 1 || vh < 1) return Rect.Empty;
            double z = host.ScaleTransform?.ScaleX ?? 1.0;
            if (z <= 0.0001) z = 1.0;
            double tx = host.TranslateTransform?.X ?? 0;
            double ty = host.TranslateTransform?.Y ?? 0;
            double canvasLeft = (scrollX - tx) / z;
            double canvasTop = (scrollY - ty) / z;
            double canvasW = vw / z;
            double canvasH = vh / z;
            if (double.IsNaN(canvasLeft) || double.IsInfinity(canvasLeft) ||
                double.IsNaN(canvasTop) || double.IsInfinity(canvasTop)) return Rect.Empty;
            return new Rect(canvasLeft, canvasTop, canvasW, canvasH);
        }

        private static void SetViewportExpandButtonState(Button btn, Border border)
        {
            bool expanded = _viewportExpandRestore.ContainsKey(border);
            btn.FontFamily = ViewportExpandIconFont;
            btn.Content = expanded ? "\uE73F" : "\uE740";
            btn.ToolTip = expanded
                ? "Thu nhỏ về kích thước và vị trí ban đầu"
                : "Phóng to vừa khung nhìn";
        }

        private static void ToggleNodeViewportExpand(HtmlUiNode node, Border border, IWorkflowEditorHost host, Button btn, Border? bottomBar = null)
        {
            // Nếu đang phóng to → thu nhỏ về kích thước trước khi phóng
            if (_viewportExpandRestore.TryGetValue(border, out var saved))
            {
                node.X = saved.x;
                node.Y = saved.y;
                node.Width = saved.w;
                node.Height = saved.h;
                border.Width = saved.w;
                border.Height = saved.h;
                _viewportExpandRestore.Remove(border);
                host.UpdateNodePosition(node, saved.x, saved.y);
                host.UpdateCanvasSize();
                node.IsViewportExpanded = false;
                if (host is WorkflowEditorWindow win)
                    win.SetViewportExpandedUiHidden(false);
                // Hiện lại footer khi thu nhỏ
                if (bottomBar != null)
                    bottomBar.Visibility = Visibility.Visible;
                SetViewportExpandButtonState(btn, border);
                return;
            }

            // Khi phóng to: tắt chế độ nghỉ để tránh node bị sleep khi đang focus/đang xem.
            if (node.EnableSleepMode)
            {
                node.EnableSleepMode = false;
                node.RequestWake();
            }

            node.IsViewportExpanded = true;
            if (host is WorkflowEditorWindow win0)
                win0.SetViewportExpandedUiHidden(true);
            // Ẩn footer khi phóng to để WebView2 chạm đáy
            if (bottomBar != null)
                bottomBar.Visibility = Visibility.Collapsed;

            bool TryExpandToViewport()
            {
                var r = GetWorkflowViewportCanvasRect(host);
                if (r.IsEmpty || r.Width < 1 || r.Height < 1) return false;
                _viewportExpandRestore[border] = (node.X, node.Y, node.Width, node.Height);
                var minW = border.MinWidth > 0 ? border.MinWidth : 1;
                var minH = border.MinHeight > 0 ? border.MinHeight : 1;
                var w = Math.Max(r.Width, minW);
                var h = Math.Max(r.Height, minH);
                node.X = r.Left;
                node.Y = r.Top;
                node.Width = w;
                node.Height = h;
                border.Width = w;
                border.Height = h;
                host.UpdateNodePosition(node, r.Left, r.Top);
                host.UpdateCanvasSize();
                SetViewportExpandButtonState(btn, border);
                return true;
            }

            if (TryExpandToViewport()) return;
            host.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (_viewportExpandRestore.ContainsKey(border)) return;
                if (!TryExpandToViewport())
                {
                    // Expand thất bại → hiện lại footer và rollback trạng thái
                    if (bottomBar != null)
                        bottomBar.Visibility = Visibility.Visible;
                    if (host is WorkflowEditorWindow win1)
                        win1.SetViewportExpandedUiHidden(false);
                }
            }));
        }

        private static void OpenNodeDialog(HtmlUiNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                if (node.Border != null && node.Border.IsMouseCaptured)
                    node.Border.ReleaseMouseCapture();
                host.DraggedNode = null;
                if (host.ViewModel != null)
                    host.ViewModel.SelectedNode = null;

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();
                var dialog = new HtmlUiNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);

                // ✅ Đảm bảo WebView2 được hiển thị lại ngay sau khi dialog mở
                // Dùng Dispatcher.BeginInvoke với priority cao để đảm bảo chạy ngay lập tức
                host.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Tìm WebView2 trong border và hiển thị lại
                    if (node.Border?.Child is Grid outerGrid && outerGrid.Children.Count > 0)
                    {
                        if (outerGrid.Children[0] is Grid innerGrid && innerGrid.Children.Count > 1)
                        {
                            if (innerGrid.Children[1] is Microsoft.Web.WebView2.Wpf.WebView2 webView)
                            {
                                webView.Visibility = Visibility.Visible;
                            }
                        }
                    }
                }), DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            if (host is WorkflowEditorWindow window)
            {
                var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager) return manager;
            }
            return new NodeDialogManager();
        }

        /// <summary>
        /// Parse cookie text và set vào Tab1 WebView2. Hỗ trợ:
        /// - Raw cookie string (name=value; name2=value2)
        /// - JSON object: {"url":"...", "cookies":[{name,value,domain,...}]}
        /// - JSON array: [{name,value,domain,...}, ...]
        /// - Netscape format
        /// Trả về URL (http/https) nếu tìm thấy trong cookie text, null nếu không có.
        /// </summary>
        private static async Task<string?> SetCookiesFromTextAsync(CoreWebView2 coreWebView2, string cookieText)
        {
            if (string.IsNullOrWhiteSpace(cookieText)) return null;

            string? extractedUrl = null;
            var cookieManager = coreWebView2.CookieManager;
            var lines = cookieText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            try
            {
                // 1. Tìm URL trong cookie text (plain text URL)
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
                        {
                            extractedUrl = trimmed;
                            break;
                        }
                    }
                }

                var trimmedText = cookieText.Trim();

                // Format 1: JSON object {"url":"...", "cookies":[...]}
                if (trimmedText.StartsWith("{"))
                {
                    try
                    {
                        var jsonRoot = JsonSerializer.Deserialize<JsonElement>(trimmedText);
                        if (jsonRoot.ValueKind == JsonValueKind.Object)
                        {
                            if (jsonRoot.TryGetProperty("url", out var urlProp))
                            {
                                var url = urlProp.GetString();
                                if (!string.IsNullOrWhiteSpace(url)) extractedUrl = url;
                            }
                            if (jsonRoot.TryGetProperty("cookies", out var cookiesProp) &&
                                cookiesProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var cookieObj in cookiesProp.EnumerateArray())
                                {
                                    try
                                    {
                                        var name = cookieObj.TryGetProperty("name", out var n) ? n.GetString() : null;
                                        var value = cookieObj.TryGetProperty("value", out var v) ? v.GetString() : null;
                                        var domain = cookieObj.TryGetProperty("domain", out var d) ? d.GetString() : null;
                                        var path = cookieObj.TryGetProperty("path", out var p) ? p.GetString() : "/";
                                        var secure = cookieObj.TryGetProperty("secure", out var s) && s.GetBoolean();
                                        var httpOnly = cookieObj.TryGetProperty("httpOnly", out var h) && h.GetBoolean();
                                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(domain)) continue;
                                        var cookie = cookieManager.CreateCookie(name, value ?? "", domain, path ?? "/");
                                        cookie.IsSecure = secure;
                                        cookie.IsHttpOnly = httpOnly;
                                        // session=false → persistent cookie; set Expires tu expirationTime (unix seconds)
                                        var isSession = !cookieObj.TryGetProperty("session", out var sess) || sess.GetBoolean();
                                        if (!isSession && cookieObj.TryGetProperty("expirationTime", out var exp) && exp.TryGetInt64(out var expSecs))
                                            cookie.Expires = DateTimeOffset.FromUnixTimeSeconds(expSecs).UtcDateTime;
                                        cookieManager.AddOrUpdateCookie(cookie);
                                    }
                                    catch { }
                                }
                                return extractedUrl;
                            }
                        }
                    }
                    catch { }
                }

                // Format 2: JSON array [{name,value,domain,...}, ...]
                if (trimmedText.StartsWith("["))
                {
                    try
                    {
                        var cookiesJson = JsonSerializer.Deserialize<JsonElement>(cookieText);
                        if (cookiesJson.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cookieObj in cookiesJson.EnumerateArray())
                            {
                                try
                                {
                                    var name = cookieObj.TryGetProperty("name", out var n) ? n.GetString() : null;
                                    var value = cookieObj.TryGetProperty("value", out var v) ? v.GetString() : null;
                                    var domain = cookieObj.TryGetProperty("domain", out var d) ? d.GetString() : null;
                                    var path = cookieObj.TryGetProperty("path", out var p) ? p.GetString() : "/";
                                    var secure = cookieObj.TryGetProperty("secure", out var s) && s.GetBoolean();
                                    var httpOnly = cookieObj.TryGetProperty("httpOnly", out var h) && h.GetBoolean();
                                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(domain)) continue;
                                    var cookie = cookieManager.CreateCookie(name, value ?? "", domain, path ?? "/");
                                    cookie.IsSecure = secure;
                                    cookie.IsHttpOnly = httpOnly;
                                    // session=false → persistent cookie; set Expires tu expirationTime (unix seconds)
                                    var isSession2 = !cookieObj.TryGetProperty("session", out var sess2) || sess2.GetBoolean();
                                    if (!isSession2 && cookieObj.TryGetProperty("expirationTime", out var exp2) && exp2.TryGetInt64(out var expSecs2))
                                        cookie.Expires = DateTimeOffset.FromUnixTimeSeconds(expSecs2).UtcDateTime;
                                    cookieManager.AddOrUpdateCookie(cookie);
                                }
                                catch { }
                            }
                            return extractedUrl;
                        }
                    }
                    catch { }
                }

                // Format 3: Netscape cookie format (tab-separated)
                var firstLine = lines.Length > 0 ? lines[0] : string.Empty;
                if (firstLine.Contains("\t") || firstLine.StartsWith("# Netscape", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || string.IsNullOrWhiteSpace(trimmed)) continue;
                        var parts = trimmed.Split('\t');
                        if (parts.Length < 6) continue;
                        try
                        {
                            var domain = parts[0].TrimStart('.');
                            var path = parts[2];
                            var secure = parts[3].Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            var name = parts[5];
                            var value = parts.Length > 6 ? parts[6] : string.Empty;
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(domain)) continue;
                            var cookie = cookieManager.CreateCookie(name, value, domain, path);
                            cookie.IsSecure = secure;
                            cookieManager.AddOrUpdateCookie(cookie);
                        }
                        catch { }
                    }
                    return extractedUrl;
                }

                // Format 4: Raw cookie string (name=value; name2=value2)
                // Cần extractedUrl để biết domain
                if (!string.IsNullOrWhiteSpace(extractedUrl) && Uri.TryCreate(extractedUrl, UriKind.Absolute, out var uri))
                {
                    var domain = uri.Host;
                    // Lọc bỏ dòng URL khỏi cookie text
                    var cookiePairs = string.Join("; ", lines
                        .Where(l => !l.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        .Select(l => l.Trim()));
                    foreach (var pair in cookiePairs.Split(';'))
                    {
                        var kv = pair.Trim();
                        if (string.IsNullOrWhiteSpace(kv)) continue;
                        var idx = kv.IndexOf('=');
                        if (idx <= 0) continue;
                        var name = kv.Substring(0, idx).Trim();
                        var value = kv.Substring(idx + 1).Trim();
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        try
                        {
                            var cookie = cookieManager.CreateCookie(name, value, domain, "/");
                            cookieManager.AddOrUpdateCookie(cookie);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HtmlUiNode] SetCookiesFromTextAsync error: {ex.Message}");
            }

            return await Task.FromResult(extractedUrl);
        }

        // ── Local file serving helpers (used by WebResourceRequested handler) ──────────────

        private static string GetLocalFileMimeType(string filePath)
        {
            var ext = (Path.GetExtension(filePath) ?? string.Empty).ToLowerInvariant();
            return ext switch
            {
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".ogg" => "video/ogg",
                ".ogv" => "video/ogg",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                ".mkv" => "video/x-matroska",
                ".ts" => "video/mp2t",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        private static bool TryParseByteRange(string headerValue, long fileLen,
            out long start, out long end)
        {
            start = 0; end = fileLen - 1;
            try
            {
                var val = headerValue?.Trim() ?? string.Empty;
                if (!val.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return false;
                var range = val.Substring(6).Trim();
                var dashIdx = range.IndexOf('-');
                if (dashIdx < 0) return false;
                var startStr = range.Substring(0, dashIdx).Trim();
                var endStr = range.Substring(dashIdx + 1).Trim();
                if (string.IsNullOrEmpty(startStr))
                {
                    // bytes=-N: last N bytes
                    if (!long.TryParse(endStr, out var suffix)) return false;
                    start = Math.Max(0, fileLen - suffix);
                    end = fileLen - 1;
                }
                else
                {
                    if (!long.TryParse(startStr, out start)) return false;
                    if (!string.IsNullOrEmpty(endStr) && long.TryParse(endStr, out var parsedEnd))
                        end = Math.Min(parsedEnd, fileLen - 1);
                }
                if (start < 0 || start > end || end >= fileLen) return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Seekable range-aware stream wrapper for HTTP 206 Partial Content responses.
        /// WebView2's ManagedIStream (COM IStream) calls Seek() to query size and navigate;
        /// all properties and seek operations are range-relative (position 0 = rangeStart in file).
        /// </summary>
        private sealed class LimitedReadStream : Stream
        {
            private readonly Stream _inner;
            private readonly long _rangeStart;
            private readonly long _rangeLength;
            private long _rangePosition; // 0-based within the range

            public LimitedReadStream(Stream inner, long rangeStart, long rangeLength)
            {
                _inner = inner;
                _rangeStart = rangeStart;
                _rangeLength = rangeLength;
                _rangePosition = 0;
                inner.Seek(rangeStart, SeekOrigin.Begin); // position at range start
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;  // Required by WebView2 ManagedIStream
            public override bool CanWrite => false;
            public override long Length => _rangeLength;
            public override long Position
            {
                get => _rangePosition;
                set
                {
                    var clamped = Math.Max(0, Math.Min(value, _rangeLength));
                    _inner.Seek(_rangeStart + clamped, SeekOrigin.Begin);
                    _rangePosition = clamped;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var remaining = _rangeLength - _rangePosition;
                if (remaining <= 0) return 0;
                var toRead = (int)Math.Min(count, remaining);
                var read = _inner.Read(buffer, offset, toRead);
                _rangePosition += read;
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPos;
                switch (origin)
                {
                    case SeekOrigin.Begin:   newPos = offset; break;
                    case SeekOrigin.Current: newPos = _rangePosition + offset; break;
                    case SeekOrigin.End:     newPos = _rangeLength + offset; break;
                    default: throw new ArgumentOutOfRangeException(nameof(origin));
                }
                newPos = Math.Max(0, Math.Min(newPos, _rangeLength));
                _inner.Seek(_rangeStart + newPos, SeekOrigin.Begin);
                _rangePosition = newPos;
                return _rangePosition;
            }

            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }

        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;
            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);
            if (port == null || port.Position == newPosition) return;
            port.Position = newPosition;
            host.UpdatePortsPositionOnSide(node, newPosition);
            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                }
                catch { }
            }
        }
    }
}