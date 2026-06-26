using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho ShowInputMsgNode: hiển thị popup HTML UI tại vị trí chuột và dừng luồng đợi user nhập dữ liệu.
    /// </summary>
    internal sealed class ShowInputMsgNodeExecutor : INodeExecutor
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ShowInputMsgPopupWindow> _windowCache = new();

        public bool CanExecute(WorkflowNode node) => node is ShowInputMsgNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var showInputMsgNode = (ShowInputMsgNode)node;

            // Clear outputs cũ
            showInputMsgNode.ResolvedOutputs.Clear();

            try
            {
                var tcs = new TaskCompletionSource<bool>();

                // Build HTML content using current execution environment mappings
                var htmlContent = BuildHtmlContent(showInputMsgNode, env);

                // Dispatch popup window creation and show to UI thread
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        var popup = _windowCache.GetOrAdd(showInputMsgNode.Id, id =>
                        {
                            var win = new ShowInputMsgPopupWindow(showInputMsgNode);
                            Application.Current.Exit += (s, e) =>
                            {
                                try
                                {
                                    win.ForceClose();
                                }
                                catch { }
                            };
                            return win;
                        });

                        popup.UpdateNode(showInputMsgNode);
                        var result = await popup.PrepareAndShowAsync(htmlContent);
                        tcs.TrySetResult(result);
                    }
                    catch (Exception popupEx)
                    {
                        tcs.TrySetException(popupEx);
                    }
                });

                // Wait for the popup window submission or closing
                using (env.CancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    await tcs.Task;
                }

                // Push dynamic outputs to execution store if successfully submitted
                if (!string.IsNullOrWhiteSpace(env.ExecutionId) && showInputMsgNode.ResolvedOutputs.Count > 0)
                {
                    var snapshot = new System.Collections.Generic.Dictionary<string, object?>(
                        showInputMsgNode.ResolvedOutputs, StringComparer.OrdinalIgnoreCase);
                    env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, showInputMsgNode.Id, snapshot);
                }

                env.OnNodeCompleted?.Invoke(showInputMsgNode, default);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowInputMsgNodeExecutor error: {ex.Message}");
                showInputMsgNode.ResolvedOutputs["error"] = ex.Message;
                env.OnNodeFailed?.Invoke(showInputMsgNode, ex.Message);
                throw;
            }

            await env.TraverseOutputsAsync(showInputMsgNode).ConfigureAwait(false);
        }

        private static string ResolveSingleInputValue(ShowInputMsgNode node, CodeInputMapping mapping, NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(mapping.SourceNodeId)) return string.Empty;

            var allNodes = env.Connections?
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .Where(n => n != null)
                .Select(n => n!)
                .GroupBy(n => n.Id)
                .Select(g => g.First())
                .ToList();

            var sourceNode = allNodes?.FirstOrDefault(n =>
                string.Equals(n.Id, mapping.SourceNodeId, StringComparison.OrdinalIgnoreCase));

            if (sourceNode == null) return string.Empty;

            var key = string.IsNullOrWhiteSpace(mapping.SourceOutputKey) ? null : mapping.SourceOutputKey.Trim();
            if (string.IsNullOrWhiteSpace(key) && sourceNode.DynamicOutputs != null && sourceNode.DynamicOutputs.Count > 0)
                key = sourceNode.DynamicOutputs[0].Key ?? "output";

            var value = env.Service.ResolveDynamicValueForExecution(sourceNode, key ?? "output", env);
            if (string.Equals(value?.Trim(), "—", StringComparison.OrdinalIgnoreCase)) value = string.Empty;
            return value ?? string.Empty;
        }

        private static System.Collections.Generic.Dictionary<string, string> ResolveInputValues(ShowInputMsgNode node, NodeExecutionEnvironment env)
        {
            var result = new System.Collections.Generic.Dictionary<string, string>();
            var mappings = node.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
            foreach (var m in mappings)
            {
                var value = ResolveSingleInputValue(node, m, env);
                var varName = m.EffectiveInputKey;
                if (string.IsNullOrWhiteSpace(varName)) varName = "input";
                result[varName] = value ?? string.Empty;
            }
            return result;
        }

        private static string ReplaceVariables(string text, System.Collections.Generic.Dictionary<string, string> variableValues)
        {
            if (string.IsNullOrEmpty(text) || variableValues.Count == 0) return text;
            var regex = new System.Text.RegularExpressions.Regex(@"\{([^}]+)\}");
            return regex.Replace(text, match =>
            {
                var variableName = match.Groups[1].Value.Trim();
                if (variableValues.TryGetValue(variableName, out var value) && value != null)
                {
                    return value;
                }
                return match.Value;
            });
        }

        private static string BuildHtmlContent(ShowInputMsgNode node, NodeExecutionEnvironment env)
        {
            var html = node.HtmlCode ?? "";
            var css = node.CssCode ?? "";
            var js = node.JsCode ?? "";

            var inputValues = ResolveInputValues(node, env);
            html = ReplaceVariables(html, inputValues);
            css = ReplaceVariables(css, inputValues);
            js = ReplaceVariables(js, inputValues);

            if (!html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
            {
                html = html.Replace("<html>", "<html>\n<head>\n    <meta charset=\"UTF-8\">\n    <title>Nhập Dữ Liệu</title>\n</head>", StringComparison.OrdinalIgnoreCase);
            }

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

            var helperScript = @"
<script>
  function hostSubmit() {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ type: 'submit' });
    }
  }
  window.hostSubmit = hostSubmit;
</script>";

            if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</body>", helperScript + "\n</body>", StringComparison.OrdinalIgnoreCase);
            else
                html += helperScript;

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
            var enabledAssets = (node.OfflineAssets ?? new System.Collections.Generic.List<FlowMy.Models.HtmlOfflineAsset>())
                .Where(a => a.IsEnabled && !string.IsNullOrWhiteSpace(a.LocalFileName));

            foreach (var asset in enabledAssets)
            {
                var content = FlowMy.Services.Utils.HtmlOfflineAssetService.GetInlineContent(asset.LocalFileName);
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

            return html;
        }

        internal static (double x, double y) GetMousePositionWpf()
        {
            GetCursorPos(out var pt);
            double dpiX = 1.0;
            double dpiY = 1.0;
            
            var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) 
                               ?? Application.Current.MainWindow;
            if (activeWindow != null)
            {
                var source = PresentationSource.FromVisual(activeWindow);
                if (source?.CompositionTarget != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }
            }
            
            return (pt.X / dpiX, pt.Y / dpiY);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }
    }

    /// <summary>
    /// Popup Window hosting WebView2 at mouse position.
    /// </summary>
    public class ShowInputMsgPopupWindow : Window
    {
        private readonly WebView2 _webView;
        private TaskCompletionSource<bool>? _tcs;
        private ShowInputMsgNode _node;
        private readonly Task _webViewInitTask;
        private bool _isForceClosing = false;

        public ShowInputMsgPopupWindow(ShowInputMsgNode node)
        {
            _node = node;

            Title = "Nhập Dữ Liệu";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.Manual;
            Width = node.Width;
            Height = node.Height;

            // Sleek dark border matching dark mode aesthetics
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e293b")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true
            };

            var shadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                BlurRadius = 15,
                ShadowDepth = 3,
                Opacity = 0.5
            };
            border.Effect = shadow;

            _webView = new WebView2();
            WebView2AirspaceClipper.ApplyRoundedCorners(_webView, 12);

            border.Child = _webView;
            Content = border;

            // Start initializing WebView2 asynchronously
            _webViewInitTask = InitializeWebViewInternalAsync();

            // Esc key hides the window and rejects
            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    Hide();
                    _tcs?.TrySetResult(false);
                }
            };

            // Hide on click outside (when window is deactivated)
            Deactivated += (s, e) =>
            {
                Hide();
                _tcs?.TrySetResult(false);
            };

            Closed += (s, e) =>
            {
                _tcs?.TrySetResult(false);
                try
                {
                    _webView.Dispose();
                }
                catch { }
            };
        }

        public void UpdateNode(ShowInputMsgNode node)
        {
            _node = node;
        }

        public void ForceClose()
        {
            _isForceClosing = true;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isForceClosing)
            {
                base.OnClosing(e);
                return;
            }
            e.Cancel = true;
            Hide();
            _tcs?.TrySetResult(false);
        }

        public async Task<bool> PrepareAndShowAsync(string htmlContent)
        {
            _tcs = new TaskCompletionSource<bool>();

            try
            {
                await _webViewInitTask;

                await Dispatcher.InvokeAsync(() =>
                {
                    _webView.CoreWebView2.NavigateToString(htmlContent);
                }, System.Windows.Threading.DispatcherPriority.Background);

                Width = _node.Width;
                Height = _node.Height;

                var mousePos = ShowInputMsgNodeExecutor.GetMousePositionWpf();
                Left = mousePos.x;
                Top = mousePos.y;

                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                if (Left + Width > screenWidth)
                    Left = screenWidth - Width;
                if (Top + Height > screenHeight)
                    Top = screenHeight - Height;
                if (Left < 0) Left = 0;
                if (Top < 0) Top = 0;

                Show();
                Activate();
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }

            return await _tcs.Task;
        }

        private async Task InitializeWebViewInternalAsync()
        {
            var env = await WebView2EnvironmentManager.GetSharedEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(env);
            
            _webView.CoreWebView2.Profile.PreferredColorScheme = Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Dark;
            _webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }

        private async void CoreWebView2_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var rawJson = e.WebMessageAsJson;
                if (!string.IsNullOrEmpty(rawJson))
                {
                    if (rawJson.Trim('"') == "submit")
                    {
                        await HandleSubmitAsync();
                        return;
                    }

                    using (var doc = System.Text.Json.JsonDocument.Parse(rawJson))
                    {
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (doc.RootElement.TryGetProperty("type", out var typeProp) && 
                                string.Equals(typeProp.GetString(), "submit", StringComparison.OrdinalIgnoreCase))
                            {
                                await HandleSubmitAsync();
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error receiving web message: {ex.Message}");
            }
        }

        private async Task HandleSubmitAsync()
        {
            await UpdateOutputsFromDomAsync();
            _tcs?.TrySetResult(true);
            Hide();
        }

        private async Task UpdateOutputsFromDomAsync()
        {
            try
            {
                var core = _webView.CoreWebView2;
                if (core == null) return;

                var paramsText = _node.ParamsCode ?? string.Empty;
                var lines = paramsText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                foreach (var rawLine in lines)
                {
                    var line = rawLine?.Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("//") || line.StartsWith("#")) continue;

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

                    string? value = null;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(resultJson) && !string.Equals(resultJson, "null", StringComparison.OrdinalIgnoreCase))
                        {
                            value = System.Text.Json.JsonSerializer.Deserialize<string>(resultJson);
                        }
                    }
                    catch
                    {
                        value = resultJson;
                    }

                    if (value == null) continue;

                    _node.ResolvedOutputs[key] = value;
                    if (_node.DynamicOutputs != null)
                    {
                        var dyn = _node.DynamicOutputs.FirstOrDefault(o =>
                            string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                        if (dyn != null)
                            dyn.UserValueOverride = value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowInputMsgPopupWindow UpdateOutputsFromDomAsync error: {ex.Message}");
            }
        }
    }
}
