// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls;
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

                using (env.CancellationToken.Register(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            if (_windowCache.TryGetValue(showInputMsgNode.Id, out var win))
                            {
                                System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Cancellation requested. Hiding window for node {showInputMsgNode.Id}");
                                win.RestorePreviousForegroundWindow();
                                win.Hide();
                            }
                        }
                        catch { }
                    });
                    tcs.TrySetCanceled();
                }))
                {
                    // Dispatch popup window creation and show to UI thread asynchronously
                    _ = Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            var popup = _windowCache.GetOrAdd(showInputMsgNode.Id, id =>
                            {
                                System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Creating new popup window for node {id}");
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
                            System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Exception inside Dispatcher delegate: {popupEx}");
                            tcs.TrySetException(popupEx);
                        }
                    });

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

            var offlineCss = new System.Text.StringBuilder();
            var offlineJs = new System.Text.StringBuilder();

            var enabledAssets = (node.OfflineAssets ?? new System.Collections.Generic.List<FlowMy.Models.HtmlOfflineAsset>())
                .Where(a => a.IsEnabled && !string.IsNullOrWhiteSpace(a.LocalFileName));

            foreach (var asset in enabledAssets)
            {
                var content = FlowMy.Services.Utils.HtmlOfflineAssetService.GetInlineContent(asset.LocalFileName);
                if (string.IsNullOrWhiteSpace(content)) continue;

                var safeName = System.Security.SecurityElement.Escape(asset.Title ?? asset.LocalFileName);

                if (string.Equals(asset.AssetType, "css", StringComparison.OrdinalIgnoreCase))
                {
                    offlineCss.AppendLine($"/* [offline] {safeName} */");
                    offlineCss.AppendLine(content);
                }
                else // js
                {
                    offlineJs.AppendLine($"/* [offline] {safeName} */");
                    offlineJs.AppendLine(content);
                }
            }

            if (html.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(css))
                {
                    var cssTag = $"\n    <style>\n{css}\n    </style>";
                    if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                        html = html.Replace("</head>", cssTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                    else if (html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
                        html = html.Replace("<head>", "<head>" + cssTag, StringComparison.OrdinalIgnoreCase);
                }

                if (offlineCss.Length > 0)
                {
                    var offlineCssTag = $"\n    <style>\n{offlineCss}\n    </style>";
                    if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                        html = html.Replace("</head>", offlineCssTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrWhiteSpace(js))
                {
                    var jsTag = $"\n    <script>\n{js}\n    </script>";
                    if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                        html = html.Replace("</body>", jsTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
                    else
                        html += jsTag;
                }

                if (offlineJs.Length > 0)
                {
                    var offlineJsTag = $"\n    <script>\n{offlineJs}\n    </script>";
                    if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                        html = html.Replace("</body>", offlineJsTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
                }

                return html;
            }

            var fullDoc = $@"<!DOCTYPE html>
<html window-frame=""none"">
<head>
    <meta charset=""utf-8"">
    <title>Nhập Dữ Liệu</title>
    <style>
        html, body {{
            margin: 0;
            padding: 0;
            width: 100%;
            height: 100%;
            background-color: #0f172a;
            color: #f8fafc;
            font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            overflow: auto;
            box-sizing: border-box;
        }}
        {css}
        {offlineCss}
    </style>
</head>
<body>
    {html}
    <script>
        {offlineJs}
        {js}
    </script>
</body>
</html>";

            return fullDoc;
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
}
