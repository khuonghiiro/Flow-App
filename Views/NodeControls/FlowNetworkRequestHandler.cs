using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CefSharp;
using CefSharp.Handler;
using CefSharp.ResponseFilter;

namespace FlowMy.Views.NodeControls
{
    public class FlowNetworkRequestHandler : RequestHandler
    {
        private readonly FlowResourceRequestHandler _resourceHandler;

        public FlowNetworkRequestHandler(object node, FlowMy.Services.Interaction.IWorkflowEditorHost? host = null)
        {
            _resourceHandler = new FlowResourceRequestHandler(node, host);
        }

        protected override IResourceRequestHandler GetResourceRequestHandler(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            bool isNavigation,
            bool isDownload,
            string requestInitiator,
            ref bool disableDefaultHandling)
        {
            return _resourceHandler;
        }

        public static void ToggleDevTools(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            if (chromiumWebBrowser is System.Windows.Threading.DispatcherObject dispatcherObj)
            {
                dispatcherObj.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var b = browser ?? chromiumWebBrowser?.GetBrowser();
                        var host = b?.GetHost();
                        if (host != null)
                        {
                            if (host.HasDevTools)
                            {
                                host.CloseDevTools();
                                return;
                            }

                            var windowInfo = new WindowInfo();
                            windowInfo.SetAsPopup(IntPtr.Zero, "DevTools - Chromium");
                            windowInfo.Width = 1080;
                            windowInfo.Height = 760;
                            windowInfo.X = 120;
                            windowInfo.Y = 120;
                            host.ShowDevTools(windowInfo);
                            return;
                        }

                        if (chromiumWebBrowser != null && chromiumWebBrowser.IsBrowserInitialized)
                        {
                            chromiumWebBrowser.ShowDevTools();
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DevTools] Toggle error: {ex.Message}");
                    }
                }));
            }
        }
    }

    public class FlowResourceRequestHandler : ResourceRequestHandler
    {
        private readonly object _node;
        private readonly FlowMy.Services.Interaction.IWorkflowEditorHost? _host;
        private readonly Dictionary<ulong, MemoryStream> _responseStreams = new();

        public FlowResourceRequestHandler(object node, FlowMy.Services.Interaction.IWorkflowEditorHost? host = null)
        {
            _node = node;
            _host = host;
        }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, string> _requestExecutionMap = new();



        protected override IResponseFilter? GetResourceResponseFilter(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response)
        {
            // Bỏ qua filter cho các URL nội bộ của Chrome/DevTools để Chromium tự xử lý native 100%
            if (request.Url != null && (
                request.Url.StartsWith("chrome-devtools://", StringComparison.OrdinalIgnoreCase) ||
                request.Url.StartsWith("devtools://", StringComparison.OrdinalIgnoreCase) ||
                request.Url.StartsWith("chrome://", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var memoryStream = new MemoryStream();
            _responseStreams[request.Identifier] = memoryStream;
            return new SafeStreamResponseFilter(memoryStream);
        }

        protected override void OnResourceLoadComplete(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response,
            UrlRequestStatus status,
            long receivedContentLength)
        {
            if (_responseStreams.TryGetValue(request.Identifier, out var stream))
            {
                try
                {
                    byte[] data;
                    lock (stream)
                    {
                        data = stream.ToArray();
                    }
                    string bodyText = Encoding.UTF8.GetString(data);

                    var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (request.Headers != null)
                    {
                        foreach (string key in request.Headers.AllKeys)
                        {
                            if (!string.IsNullOrEmpty(key))
                                requestHeaders[key] = request.Headers[key] ?? string.Empty;
                        }
                    }

                    var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (response != null && response.Headers != null)
                    {
                        foreach (string key in response.Headers.AllKeys)
                        {
                            if (!string.IsNullOrEmpty(key))
                                responseHeaders[key] = response.Headers[key] ?? string.Empty;
                        }
                    }

                    string? postData = null;
                    if (request.PostData != null && request.PostData.Elements.Count > 0)
                    {
                        var sb = new StringBuilder();
                        foreach (var el in request.PostData.Elements)
                        {
                            if (el.Type == PostDataElementType.Bytes && el.Bytes != null)
                            {
                                sb.Append(Encoding.UTF8.GetString(el.Bytes));
                            }
                        }
                        postData = sb.ToString();
                    }

                    string targetUrl = request.Url ?? string.Empty;
                    string requestMethod = request.Method ?? "GET";

                    string? targetExecutionId = null;
                    if (_requestExecutionMap.TryRemove(request.Identifier, out var mappedExecId))
                    {
                        targetExecutionId = mappedExecId;
                    }

                    if (_node is FlowMy.Models.Nodes.WebNode webNode)
                    {
                        webNode.ProcessInterceptedNetworkResponse(
                            targetUrl,
                            requestMethod,
                            requestHeaders,
                            responseHeaders,
                            postData,
                            bodyText,
                            response != null ? (int)response.StatusCode : 200,
                            targetExecutionId,
                            _host);

                        if (_host != null)
                        {
                            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    _host.RequestSyncDataPanels(immediate: false);
                                    if (webNode.SyncLiveOutputsToResults)
                                    {
                                        var vm = _host.ViewModel;
                                        if (vm != null)
                                        {
                                            var field = typeof(FlowMy.ViewModels.WorkflowEditorViewModel)
                                                .GetField("_executionVisualizer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                            if (field?.GetValue(vm) is FlowMy.Services.Workflow.IWorkflowExecutionVisualizer visualizer)
                                            {
                                                visualizer.RefreshSavedOutputs(new[] { webNode });
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[FlowResourceRequestHandler] Live result sync error: {ex.Message}");
                                }
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }

                        // Real-time cookie extraction for active URL
                        if (!string.IsNullOrWhiteSpace(targetUrl) &&
                            (targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        {
                            System.Threading.Tasks.Task.Run(async () =>
                            {
                                try
                                {
                                    ICookieManager? cookieMgr;
                                    if (string.Equals(webNode.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase) &&
                                        !string.IsNullOrWhiteSpace(webNode.CustomCacheName))
                                    {
                                        var rc = FlowMy.Services.Workflow.CefSharpEnvironmentManager.CreateProfileRequestContext(webNode.CustomCacheName.Trim());
                                        cookieMgr = rc.GetCookieManager(null);
                                    }
                                    else
                                    {
                                        cookieMgr = Cef.GetGlobalCookieManager();
                                    }

                                    if (cookieMgr != null)
                                    {
                                        var cookies = await cookieMgr.VisitUrlCookiesAsync(targetUrl, includeHttpOnly: true);
                                        if (cookies != null && cookies.Count > 0)
                                        {
                                            var cookieStr = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                                            if (!string.IsNullOrWhiteSpace(cookieStr))
                                            {
                                                webNode.LastCookie = cookieStr;
                                                webNode.UpdateResponseOutputValue("cookie", cookieStr, isList: false);

                                                if (_host != null && webNode.SyncLiveOutputsToResults)
                                                {
                                                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                                                    {
                                                        try
                                                        {
                                                            _host.RequestSyncDataPanels(immediate: false);
                                                            var vm = _host.ViewModel;
                                                            if (vm != null)
                                                            {
                                                                var field = typeof(FlowMy.ViewModels.WorkflowEditorViewModel)
                                                                    .GetField("_executionVisualizer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                                                if (field?.GetValue(vm) is FlowMy.Services.Workflow.IWorkflowExecutionVisualizer visualizer)
                                                                {
                                                                    visualizer.RefreshSavedOutputs(new[] { webNode });
                                                                }
                                                            }
                                                        }
                                                        catch { }
                                                    }), System.Windows.Threading.DispatcherPriority.Background);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch { }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FlowResourceRequestHandler] Error: {ex.Message}");
                }
                finally
                {
                    lock (stream)
                    {
                        try { stream.Dispose(); } catch { }
                    }
                    _responseStreams.Remove(request.Identifier);
                }
            }
        }

        protected override CefReturnValue OnBeforeResourceLoad(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IRequestCallback callback)
        {
            if (_node is FlowMy.Models.Nodes.WebNode webNode)
            {
                string targetUrl = request.Url ?? string.Empty;
                string requestMethod = request.Method ?? "GET";

                string? activeExecutionId = null;
                var activeRuns = webNode.GetActiveExecutionRuns();
                if (activeRuns.Count > 0)
                {
                    activeExecutionId = activeRuns.LastOrDefault()?.ExecutionId;
                }

                if (!string.IsNullOrEmpty(activeExecutionId))
                {
                    _requestExecutionMap[request.Identifier] = activeExecutionId;
                }

                var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (request.Headers != null)
                {
                    foreach (string key in request.Headers.AllKeys)
                    {
                        if (!string.IsNullOrEmpty(key))
                            requestHeaders[key] = request.Headers[key] ?? string.Empty;
                    }
                }

                string? postDataText = null;
                if (request.PostData != null && request.PostData.Elements.Count > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var el in request.PostData.Elements)
                    {
                        if (el.Type == PostDataElementType.Bytes && el.Bytes != null)
                        {
                            sb.Append(Encoding.UTF8.GetString(el.Bytes));
                        }
                    }
                    postDataText = sb.ToString();
                }

                // Trích xuất tức thì (Immediate extraction) ngay khi khởi tạo request cho cURL/Headers/Params/Payload
                webNode.ProcessInterceptedNetworkRequest(
                    targetUrl,
                    requestMethod,
                    requestHeaders,
                    postDataText,
                    activeExecutionId);

                // 1. Chặn request nếu khớp BlockingRules
                if (webNode.ShouldBlockRequest(targetUrl, requestMethod))
                {
                    System.Diagnostics.Debug.WriteLine($"[FlowResourceRequestHandler] 🚫 Cancelled/Blocked request: {targetUrl}");
                    return CefReturnValue.Cancel;
                }

                // 2. Thay request nếu khớp RequestInterceptRules
                if (webNode.RequestInterceptRules != null && webNode.RequestInterceptRules.Count > 0)
                {
                    foreach (var rule in webNode.RequestInterceptRules)
                    {
                        if (rule == null || string.IsNullOrWhiteSpace(rule.MatchUrlPattern)) continue;

                        if (FlowMy.Models.Nodes.WebNode.UrlMatchesPattern(targetUrl, rule.MatchUrlPattern))
                        {
                            string? newUrl = null;
                            if (rule.ReplaceUrlWithNodeKey)
                            {
                                var resolved = ResolveNodeOutputString(rule.ReplaceUrlSourceNodeId, rule.ReplaceUrlSourceOutputKey);
                                if (!string.IsNullOrWhiteSpace(resolved) && resolved != "—")
                                    newUrl = resolved;
                            }
                            else if (!string.IsNullOrWhiteSpace(rule.ReplaceUrlValue))
                            {
                                newUrl = rule.ReplaceUrlValue.Trim();
                            }

                            if (!string.IsNullOrWhiteSpace(newUrl))
                            {
                                request.Url = newUrl;
                                System.Diagnostics.Debug.WriteLine($"[FlowResourceRequestHandler] 🔄 Replaced URL: {targetUrl} -> {newUrl}");
                            }

                            string? newBody = ResolveNodeOutputString(rule.ReplaceBodySourceNodeId, rule.ReplaceBodySourceOutputKey);
                            if (string.IsNullOrWhiteSpace(newBody) && !string.IsNullOrWhiteSpace(rule.ReplaceBodyValue))
                            {
                                newBody = rule.ReplaceBodyValue;
                            }

                            if (!string.IsNullOrWhiteSpace(newBody))
                            {
                                var postData = request.PostData ?? new PostData();
                                postData.Elements.Clear();
                                var element = postData.CreatePostDataElement();
                                element.Bytes = Encoding.UTF8.GetBytes(newBody);
                                postData.AddElement(element);
                                request.PostData = postData;
                                System.Diagnostics.Debug.WriteLine($"[FlowResourceRequestHandler] 🔄 Replaced Body for: {targetUrl}");
                            }
                        }
                    }
                }
            }

            return base.OnBeforeResourceLoad(chromiumWebBrowser, browser, frame, request, callback);
        }

        private string? ResolveNodeOutputString(string? nodeId, string? key)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key)) return null;
            try
            {
                if (_host?.ViewModel?.WorkflowExecutionService != null)
                {
                    var service = _host.ViewModel.WorkflowExecutionService;
                    if (service.TryGetScopedNodeStringOutputForLookupChain(null, nodeId, key, out var val) &&
                        !string.IsNullOrWhiteSpace(val) && val != "—")
                    {
                        return val;
                    }
                }
                if (_host?.ViewModel?.Nodes != null)
                {
                    var srcNode = _host.ViewModel.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                    if (srcNode is FlowMy.Models.Nodes.WebNode wn && wn.ResponseOutputValues.TryGetValue(key, out var wVal))
                        return wVal;
                    if (srcNode?.DynamicOutputs != null)
                    {
                        var dyn = srcNode.DynamicOutputs.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
                        if (dyn != null && !string.IsNullOrWhiteSpace(dyn.UserValueOverride))
                            return dyn.UserValueOverride;
                    }
                }
            }
            catch { }
            return null;
        }
    }

    public class FlowKeyboardHandler : CefSharp.Handler.KeyboardHandler
    {
        protected override bool OnPreKeyEvent(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            KeyType type,
            int windowsKeyCode,
            int nativeKeyCode,
            CefEventFlags modifiers,
            bool isSystemKey,
            ref bool isKeyboardShortcut)
        {
            if (type == KeyType.RawKeyDown || type == KeyType.KeyDown)
            {
                bool ctrlPressed = modifiers.HasFlag(CefEventFlags.ControlDown);
                bool shiftPressed = modifiers.HasFlag(CefEventFlags.ShiftDown);

                // 123 = F12 (DevTools)
                if (windowsKeyCode == 123)
                {
                    if (type == KeyType.RawKeyDown)
                    {
                        FlowNetworkRequestHandler.ToggleDevTools(chromiumWebBrowser, browser);
                    }
                    return true;
                }

                // 116 = F5 or Ctrl+R (Reload)
                if (windowsKeyCode == 116 || (ctrlPressed && windowsKeyCode == 82))
                {
                    if (chromiumWebBrowser is System.Windows.Threading.DispatcherObject dispatcherObj)
                    {
                        dispatcherObj.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { browser.Reload(shiftPressed); } catch { }
                        }));
                    }
                    return true;
                }
            }

            return base.OnPreKeyEvent(chromiumWebBrowser, browser, type, windowsKeyCode, nativeKeyCode, modifiers, isSystemKey, ref isKeyboardShortcut);
        }
    }

    public class FlowContextMenuHandler : CefSharp.Handler.ContextMenuHandler
    {
        private const int ShowDevToolsCommandId = 26501;

        protected override void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            base.OnBeforeContextMenu(chromiumWebBrowser, browser, frame, parameters, model);
            model.AddSeparator();
            model.AddItem((CefMenuCommand)ShowDevToolsCommandId, "Inspect Element / DevTools (F12)");
        }

        protected override bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            if ((int)commandId == ShowDevToolsCommandId)
            {
                FlowNetworkRequestHandler.ToggleDevTools(chromiumWebBrowser, browser);
                return true;
            }
            return base.OnContextMenuCommand(chromiumWebBrowser, browser, frame, parameters, commandId, eventFlags);
        }

        public static void OpenDevToolsSafely(IWebBrowser chromiumWebBrowser, IBrowser browser, int x = 0, int y = 0)
        {
            if (chromiumWebBrowser is System.Windows.Threading.DispatcherObject dispatcherObj)
            {
                dispatcherObj.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var host = browser?.GetHost();
                        if (host != null)
                        {
                            var windowInfo = new WindowInfo();
                            windowInfo.SetAsPopup(host.GetWindowHandle(), "DevTools - Chromium");
                            host.ShowDevTools(windowInfo, x, y);
                            return;
                        }
                    }
                    catch { }

                    try
                    {
                        chromiumWebBrowser.ShowDevTools();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ShowDevTools error: {ex.Message}");
                    }
                }));
            }
        }
    }

    public class SafeStreamResponseFilter : IResponseFilter
    {
        private readonly MemoryStream _destinationStream;
        private bool _isDisposed;

        public SafeStreamResponseFilter(MemoryStream destinationStream)
        {
            _destinationStream = destinationStream;
        }

        public FilterStatus Filter(Stream? dataIn, out long dataInRead, Stream? dataOut, out long dataOutWritten)
        {
            dataInRead = 0;
            dataOutWritten = 0;

            if (dataIn == null || dataOut == null)
            {
                return FilterStatus.Done;
            }

            try
            {
                // Dùng buffer cố định 64KB (không dùng dataIn.Length vì CefStreamAdapter không hỗ trợ Length)
                byte[] buffer = new byte[65536];
                int read = dataIn.Read(buffer, 0, buffer.Length);
                dataInRead = read;

                if (read > 0)
                {
                    // 1. Luôn ghi vào dataOut trước để trình duyệt (và DevTools) nhận đủ dữ liệu gốc 100%
                    dataOut.Write(buffer, 0, read);
                    dataOutWritten = read;

                    // 2. Sao chép phụ vào MemoryStream của WebNode (nếu lỗi cũng không ảnh hưởng dataOut)
                    try
                    {
                        if (!_isDisposed)
                        {
                            lock (_destinationStream)
                            {
                                if (!_isDisposed && _destinationStream.CanWrite)
                                {
                                    _destinationStream.Write(buffer, 0, read);
                                }
                            }
                        }
                    }
                    catch { }
                }

                return FilterStatus.Done;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeStreamResponseFilter] Exception suppressed: {ex.Message}");
                return FilterStatus.Done;
            }
        }

        public bool InitFilter()
        {
            return true;
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}
