using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho WebNode: gửi request tới ExtractUrl với cookie từ input,
    /// khi response StatusCode khớp ExtractStatusCode thì trích cookie, bearer, access_token và gán vào outputs.
    /// </summary>
    internal sealed class WebNodeExecutor : INodeExecutor
    {
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            return client;
        }

        public bool CanExecute(WorkflowNode node) => node is WebNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            Debug.WriteLine($"[WebNodeExecutor] ===== STARTING EXECUTION for node {node.Id} ({node.Title}) =====");
            var webNode = (WebNode)node;
            var connections = env.Connections;
            TaskCompletionSource<bool>? pendingOutputsTcs = null;
            // Timeout hiệu lực cho việc chờ ResponseOutputs. Mặc định lấy từ node,
            // nhưng có thể bị override bởi JS (ví dụ: marker AC_RESPONSE_TIMEOUT_MS trong script).
            var effectiveWaitTimeoutMs = webNode.ResponseOutputsWaitTimeoutMs;

            // KHÔNG reset LastCookie: giá trị có thể đã được set từ WebView2 (WebResourceResponseReceived).
            // Chỉ ghi đè khi ExtractUrl response có Set-Cookie. Nếu reset ở đây sẽ mất cookie từ toggle.
            webNode.LastBearer = null;
            webNode.LastAccessToken = null;
            webNode.RequestWake();

            // Reset chuỗi chặn request cho lần chạy node mới
            webNode.HasTriggeredBlockingChain = false;
            if (webNode.BlockingRules != null)
            {
                foreach (var br in webNode.BlockingRules)
                {
                    br.HasTriggeredParentInCurrentRun = false;
                }
            }

            // Resolve JS script: khi node X chạy đến Web, tìm mapping (Node X + Key) → chạy JS từ key đó.
            try
            {
                var incomingNodeId = env.IncomingConnection?.FromNode?.Id;
                var jsSources = webNode.JsSources ?? new List<WebJsSourceMapping>();

                WebJsSourceMapping? matchingMapping = null;
                if (!string.IsNullOrWhiteSpace(incomingNodeId))
                {
                    matchingMapping = jsSources.FirstOrDefault(m =>
                        string.Equals(m.SourceNodeId, incomingNodeId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(m.SourceOutputKey));
                }

                if (matchingMapping != null)
                {
                    var js = env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, matchingMapping.SourceNodeId!, matchingMapping.SourceOutputKey!, env);
                    if (!string.IsNullOrWhiteSpace(js) && js != "—")
                    {
                        // Cho phép JS override timeout chờ outputs bằng marker trong script, ví dụ:
                        // // AC_RESPONSE_TIMEOUT_MS=0     (không chờ)
                        // // AC_RESPONSE_TIMEOUT_MS=3000  (chờ tối đa 3s)
                        try
                        {
                            var m = Regex.Match(js, @"AC_RESPONSE_TIMEOUT_MS\s*=\s*(\d+)", RegexOptions.IgnoreCase);
                            if (m.Success && int.TryParse(m.Groups[1].Value, out var parsedMs))
                            {
                                effectiveWaitTimeoutMs = parsedMs;
                                Debug.WriteLine($"[WebNodeExecutor] Override ResponseOutputsWaitTimeoutMs from JS: {effectiveWaitTimeoutMs} ms");
                            }
                        }
                        catch { /* ignore parse errors */ }

                        webNode.PendingJavaScript = js;
                    }
                }
            }
            catch { /* ignore */ }

            var executionId = env.ExecutionId;
            var executionRun = webNode.StartExecutionRun(executionId);

            try
            {
                // Xử lý logic CHỜ ĐỢI THEO TỪNG KEY (Per-Key Timeout & Wait)
                var waitKeys = webNode.ResponseOutputs?
                    .Where(ro => ro != null && ro.WaitForCompletion && !string.IsNullOrWhiteSpace(ro.Key))
                    .ToList() ?? new List<WebResponseOutput>();

                if (waitKeys.Count > 0)
                {
                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(env.CancellationToken);
                    var waitTasks = new List<Task>();

                    foreach (var keyConfig in waitKeys)
                    {
                        var keyTask = executionRun.GetWaitTaskForKey(keyConfig.Key);
                        var timeoutMs = keyConfig.TimeoutMs;
                        var effectiveKeyTimeoutMs = timeoutMs > 0 ? timeoutMs : effectiveWaitTimeoutMs;

                        if (effectiveKeyTimeoutMs > 0)
                        {
                            // Timeout riêng cho key này (hoặc fallback theo effectiveWaitTimeoutMs)
                            var keyTimeoutTask = Task.WhenAny(keyTask, Task.Delay(effectiveKeyTimeoutMs, waitCts.Token));
                            waitTasks.Add(keyTimeoutTask);
                        }
                        else
                        {
                            // Timeout = 0: chờ tới khi hoàn thành (hoặc bị cancel flow)
                            waitTasks.Add(keyTask);
                        }
                    }

                    try
                    {
                        var keyNames = string.Join(", ", waitKeys.Select(k => $"'{k.Key}'"));
                        Debug.WriteLine($"[WebNodeExecutor] Awaiting {waitTasks.Count} checked key output completion tasks: {keyNames}");
                        await Task.WhenAll(waitTasks);
                    }
                    catch (OperationCanceledException)
                    {
                        // Flow cancellation
                    }
                    catch (Exception waitEx)
                    {
                        Debug.WriteLine($"[WebNodeExecutor] Per-key wait error: {waitEx.Message}");
                    }
                    finally
                    {
                        try { waitCts.Cancel(); } catch { }
                    }
                }

                // Đảm bảo tất cả tác vụ đọc stream/content response ngầm cho run này được xử lý xong hoàn toàn
                if (executionRun != null)
                {
                    try
                    {
                        var extractions = executionRun.PendingExtractions.ToArray();
                        if (extractions.Length > 0)
                        {
                            await Task.WhenAll(extractions);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebNodeExecutor] Pending extractions await error: {ex.Message}");
                    }
                }

                PublishScopedWebOutputs(env, webNode);
                env.OnNodeCompleted?.Invoke(webNode, default);
            }
            finally
            {
                webNode.FinishExecutionRun(executionId);
            }

            Debug.WriteLine($"[WebNodeExecutor] Calling TraverseOutputsAsync...");
            await env.TraverseOutputsAsync(webNode);
            Debug.WriteLine($"[WebNodeExecutor] ===== COMPLETED EXECUTION for node {node.Id} =====");
        }

        private static void PublishScopedWebOutputs(NodeExecutionEnvironment env, WebNode webNode)
        {
            if (env?.Service == null || webNode == null || string.IsNullOrWhiteSpace(env.ExecutionId))
                return;

            var service = env.Service;
            service.SetScopedNodeStringOutput(env.ExecutionId, webNode.Id, "cookie", webNode.LastCookie ?? string.Empty);
            service.SetScopedNodeStringOutput(env.ExecutionId, webNode.Id, "bearer", webNode.LastBearer ?? string.Empty);
            service.SetScopedNodeStringOutput(env.ExecutionId, webNode.Id, "access_token", webNode.LastAccessToken ?? string.Empty);

            var run = webNode.GetExecutionRun(env.ExecutionId);
            var sourceOutputs = run != null ? run.ResponseOutputValues : webNode.ResponseOutputValues;

            if (sourceOutputs != null)
            {
                var lockObj = run?.Lock ?? new object();
                lock (lockObj)
                {
                    foreach (var kv in sourceOutputs)
                    {
                        if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                        var key = kv.Key.Trim();
                        var val = kv.Value ?? string.Empty;

                        service.SetScopedNodeStringOutput(env.ExecutionId, webNode.Id, key, val);

                        if (webNode.DynamicOutputs != null)
                        {
                            var dyn = webNode.DynamicOutputs.FirstOrDefault(o =>
                                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                            if (dyn != null) dyn.UserValueOverride = val;
                        }
                    }
                }
            }
        }

        private static string ResolveCookie(WebNode webNode, List<WorkflowConnection> connections, NodeExecutionEnvironment env)
        {
            var input = webNode.DynamicInputs?.FirstOrDefault(o => string.Equals(o.Key, "cookie", StringComparison.OrdinalIgnoreCase));
            if (input == null) return string.Empty;

            var nodeId = input.SelectedSourceNodeId;
            var key = (input.SelectedSourceOutputKey ?? input.Key ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nodeId)) return string.Empty;

            var value = env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, nodeId, key, env);
            return value == "—" ? string.Empty : value;
        }

        /// <summary>
        /// Thay thế các biến {variable} trong URL template bằng giá trị từ input mappings.
        /// Ví dụ: "https://api.example.com/v1/projects/{name}/flowMedia:batchGenerateImages" 
        /// với input mapping có EffectiveInputKey = "name" sẽ được thay bằng giá trị từ node nguồn.
        /// </summary>
        private static string ResolveUrlTemplate(WebNode webNode, string urlTemplate, List<WorkflowConnection> connections, NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(urlTemplate))
                return urlTemplate;

            // Resolve tất cả input mappings thành dictionary: variableName -> value
            var variableValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            var mappings = webNode.InputMappings ?? new List<WebInputMapping>();
            foreach (var mapping in mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.SourceNodeId) || string.IsNullOrWhiteSpace(mapping.SourceOutputKey))
                    continue;

                var variableName = mapping.EffectiveInputKey;
                if (string.IsNullOrWhiteSpace(variableName))
                    continue;

                // Resolve giá trị từ node nguồn
                var value = env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, mapping.SourceNodeId, mapping.SourceOutputKey, env);
                
                // Xử lý giá trị "—" thành empty
                if (value == "—")
                    value = string.Empty;

                variableValues[variableName] = value;
            }

            // Thay thế {variable} trong URL template
            var pattern = @"\{([^}]+)\}";
            var result = Regex.Replace(urlTemplate, pattern, match =>
            {
                var variableName = match.Groups[1].Value.Trim();
                
                if (variableValues.TryGetValue(variableName, out var varValue))
                {
                    // URL encode giá trị để đảm bảo URL hợp lệ
                    return Uri.EscapeDataString(varValue);
                }
                
                // Variable không tìm thấy, giữ nguyên placeholder
                Debug.WriteLine($"WebNodeExecutor: Variable '{variableName}' not found in input mappings, keeping placeholder");
                return match.Value;
            });

            return result;
        }

        // FetchConfiguredResponseOutputsAsync removed: ResponseOutputs are now populated
        // directly from WebView2 (WebResourceResponseReceived) in WebNodeControl.
    }
}
