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

        protected override IResponseFilter? GetResourceResponseFilter(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response)
        {
            var memoryStream = new MemoryStream();
            _responseStreams[request.Identifier] = memoryStream;
            return new StreamResponseFilter(memoryStream);
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
                    byte[] data = stream.ToArray();
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

                    if (_node is FlowMy.Models.Nodes.WebNode webNode)
                    {
                        webNode.ProcessInterceptedNetworkResponse(
                            targetUrl,
                            requestMethod,
                            requestHeaders,
                            responseHeaders,
                            postData,
                            bodyText,
                            response != null ? (int)response.StatusCode : 200);

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
                    stream.Dispose();
                    _responseStreams.Remove(request.Identifier);
                }
            }
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
            // 123 = F12 key code
            if (windowsKeyCode == 123 && (type == KeyType.RawKeyDown || type == KeyType.KeyDown))
            {
                if (chromiumWebBrowser is System.Windows.Threading.DispatcherObject dispatcherObj)
                {
                    dispatcherObj.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            chromiumWebBrowser.ShowDevTools();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"ShowDevTools F12 error: {ex.Message}");
                        }
                    }));
                }
                return true; // Handled F12
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
                if (chromiumWebBrowser is System.Windows.Threading.DispatcherObject dispatcherObj)
                {
                    dispatcherObj.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            browser.ShowDevTools(null, parameters.XCoord, parameters.YCoord);
                        }
                        catch
                        {
                            chromiumWebBrowser.ShowDevTools();
                        }
                    }));
                }
                return true;
            }
            return base.OnContextMenuCommand(chromiumWebBrowser, browser, frame, parameters, commandId, eventFlags);
        }
    }
}
