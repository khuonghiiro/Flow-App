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
            var sw = Stopwatch.StartNew();
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

            webNode.RebuildResponseOutputs();
            var executionId = env.ExecutionId;
            webNode.CurrentExecutingExecutionId = executionId;
            var executionRun = webNode.StartExecutionRun(executionId);

            try
            {
                // LOG CHI TIẾT TẤT CẢ CÁC OUTPUTS TRONG DIALOG
                var allOutputsLog = webNode.ResponseOutputs != null
                    ? string.Join("; ", webNode.ResponseOutputs.Select(o => $"[Key='{o.Key}', Wait={o.WaitForCompletion}, IsList={o.IsList}, Timeout={o.TimeoutMs}ms]"))
                    : "None";
                Debug.WriteLine($"[WebNodeExecutor][DIAG] Node '{webNode.Title}' ({webNode.Id}) All Configured Outputs ({webNode.ResponseOutputs?.Count ?? 0}): {allOutputsLog}");

                // Xử lý logic CHỜ ĐỢI THEO TỪNG KEY (Per-Key Timeout & Wait)
                var waitKeys = webNode.ResponseOutputs?
                    .Where(ro => ro != null && ro.WaitForCompletion && !string.IsNullOrWhiteSpace(ro.Key))
                    .ToList() ?? new List<WebResponseOutput>();

                Debug.WriteLine($"[WebNodeExecutor][DIAG] Checked WAIT keys count: {waitKeys.Count}");

                if (waitKeys.Count > 0)
                {
                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(env.CancellationToken);
                    var waitTasks = new List<Task>();

                    foreach (var keyConfig in waitKeys)
                    {
                        // Kiểm tra dữ liệu hiện có để unblock keyTask trong 0ms nếu đã đủ targetCount / có value
                        bool alreadyCompleted = executionRun.CheckAndSignalIfKeyCompleted(webNode, keyConfig, env.Service, null);

                        var keyTask = executionRun.GetWaitTaskForKey(keyConfig.Key);
                        var timeoutMs = keyConfig.TimeoutMs;
                        var effectiveKeyTimeoutMs = timeoutMs > 0 ? timeoutMs : effectiveWaitTimeoutMs;

                        // Nếu không cài TimeoutMs cụ thể (>0) và node không có global timeout, dùng mặc định 30000ms (30s) safety timeout để tránh chờ vô hạn
                        if (effectiveKeyTimeoutMs <= 0 && (keyConfig.WaitForCompletion || keyConfig.IsList))
                        {
                            effectiveKeyTimeoutMs = 30000;
                        }

                        Debug.WriteLine($"[WebNodeExecutor][DIAG] Wait key '{keyConfig.Key}': IsCompletedImmediately={alreadyCompleted}, EffectiveTimeout={effectiveKeyTimeoutMs}ms");

                        if (effectiveKeyTimeoutMs > 0)
                        {
                            // Timeout riêng cho key này (hoặc fallback 30s safety timeout).
                            var keyTimeoutTask = Task.WhenAny(keyTask, Task.Delay(effectiveKeyTimeoutMs, waitCts.Token));
                            waitTasks.Add(keyTimeoutTask);
                        }
                        else
                        {
                            // Timeout = 0: chờ tới khi hoàn thành (hoặc bị cancel flow)
                            waitTasks.Add(keyTask);
                        }
                    }

                    // Lên lịch fallback debounce (2500ms): cho phép các response trong mảng truyền đủ 3 item trước khi ngắt chờ ngầm
                    executionRun.ScheduleDebounceCompletion(2500);

                    var waitSw = Stopwatch.StartNew();
                    try
                    {
                        var keyNames = string.Join(", ", waitKeys.Select(k => $"'{k.Key}'"));
                        Debug.WriteLine($"[WebNodeExecutor][DIAG] >>> START AWAITING {waitTasks.Count} wait tasks: {keyNames} at {DateTime.Now:HH:mm:ss.fff}");
                        await Task.WhenAll(waitTasks);
                        waitSw.Stop();
                        Debug.WriteLine($"[WebNodeExecutor][DIAG] <<< FINISHED AWAITING wait tasks in {waitSw.ElapsedMilliseconds}ms at {DateTime.Now:HH:mm:ss.fff}");
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine($"[WebNodeExecutor][DIAG] Await cancelled after {waitSw.ElapsedMilliseconds}ms");
                    }
                    catch (Exception waitEx)
                    {
                        Debug.WriteLine($"[WebNodeExecutor][DIAG] Per-key wait error after {waitSw.ElapsedMilliseconds}ms: {waitEx.Message}");
                    }
                    finally
                    {
                        try { waitCts.Cancel(); } catch { }
                    }
                }

                // Xả chờ cho PendingOutputsTcs lập tức sau khi các checked key đã gom xong (tránh kẹt DataFetcherNode / downstream nodes)
                webNode.CancelPendingOutputsDebounce();
                webNode.PendingOutputsTcs?.TrySetResult(true);

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
                sw.Stop();
                env.OnNodeCompleted?.Invoke(webNode, sw.Elapsed);
            }
            finally
            {
                webNode.FinishExecutionRun(executionId);
            }

            Debug.WriteLine($"[WebNodeExecutor] Calling TraverseOutputsAsync...");
            await env.TraverseOutputsAsync(webNode);
            Debug.WriteLine($"[WebNodeExecutor] ===== COMPLETED EXECUTION for node {node.Id} in {sw.Elapsed.TotalSeconds:0.00}s =====");
        }

        private static void PublishScopedWebOutputs(NodeExecutionEnvironment env, WebNode webNode)
        {
            if (env?.Service == null || webNode == null || string.IsNullOrWhiteSpace(env.ExecutionId))
                return;

            var service = env.Service;

            // 1. Publish cookie, bearer, access_token khi có giá trị thực sự
            if (!string.IsNullOrWhiteSpace(webNode.LastCookie))
                service.SetScopedNodeStringOutput(env.ExecutionId, webNode.Id, "cookie", webNode.LastCookie);
            if (!string.IsNullOrWhiteSpace(webNode.LastBearer))
                service.SetScopedNodeStringOutput(env.ExecutionId, webNode.Id, "bearer", webNode.LastBearer);
            if (!string.IsNullOrWhiteSpace(webNode.LastAccessToken))
                service.SetScopedNodeStringOutput(env.ExecutionId, webNode.Id, "access_token", webNode.LastAccessToken);

            // 2. Gom tất cả outputs: Ưu tiên lấy từ luồng run hiện tại (độc lập 100% cho từng lượt chạy executionId)
            var mergedOutputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var run = webNode.GetExecutionRun(env.ExecutionId);
            if (run != null)
            {
                lock (run.Lock)
                {
                    foreach (var kv in run.ResponseOutputValues)
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value) && kv.Value != "[]")
                        {
                            mergedOutputs[kv.Key.Trim()] = kv.Value;
                        }
                    }
                }
            }

            // Fallback sang master ResponseOutputValues nếu luồng run chưa có key đó
            lock (webNode.ResponseOutputValues)
            {
                foreach (var kv in webNode.ResponseOutputValues)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value) && kv.Value != "[]" && !mergedOutputs.ContainsKey(kv.Key.Trim()))
                    {
                        mergedOutputs[kv.Key.Trim()] = kv.Value;
                    }
                }
            }

            // 3. Khởi tạo giá trị mặc định cho tất cả các key đã cấu hình trong ResponseOutputs
            if (webNode.ResponseOutputs != null)
            {
                foreach (var ro in webNode.ResponseOutputs)
                {
                    if (ro == null || string.IsNullOrWhiteSpace(ro.Key)) continue;
                    var k = ro.Key.Trim();
                    if (!mergedOutputs.ContainsKey(k))
                    {
                        mergedOutputs[k] = ro.IsList ? "[]" : string.Empty;
                    }
                }
            }

            // 4. Lưu toàn bộ dữ liệu output key vào WorkflowExecutionService theo ExecutionId của luồng (biến tạm theo luồng)
            foreach (var kv in mergedOutputs)
            {
                service.SetScopedNodeStringOutput(env.ExecutionId, webNode.Id, kv.Key, kv.Value);

                if (webNode.DynamicOutputs != null)
                {
                    var dyn = webNode.DynamicOutputs.FirstOrDefault(o =>
                        string.Equals(o.Key, kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (dyn != null) dyn.UserValueOverride = kv.Value;
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
