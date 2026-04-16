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
        private static readonly HttpClient _httpClient = new HttpClient();

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

            // Nếu node có cấu hình ResponseOutputs thì chuẩn bị TCS để chờ WebView2 populate outputs.
            // WebNodeControl (WebResourceResponseReceived / ExtractFromBlockedRequest) sẽ TrySetResult(true)
            // sau khi đã đẩy đủ dữ liệu tương ứng vào ResponseOutputValues.
            // - Nếu bất kỳ ResponseOutput nào có WaitForCompletion = true → chỉ đợi những key đó.
            // - Nếu không có output nào đánh dấu WaitForCompletion → đợi tất cả outputs (giữ tương thích cũ).
            // - Nếu effectiveWaitTimeoutMs <= 0 → không chờ (bỏ qua TCS).
            if (webNode.ResponseOutputs != null && webNode.ResponseOutputs.Count > 0 && effectiveWaitTimeoutMs != 0)
            {
                pendingOutputsTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                webNode.PendingOutputsTcs = pendingOutputsTcs;
            }

            // Reset previous response outputs
            try { webNode.ResponseOutputValues?.Clear(); } catch { }

            var cookie = ResolveCookie(webNode, connections, env);
            var urlTemplate = webNode.ExtractUrl?.Trim() ?? string.Empty;
            
            // Thay thế các biến {variable} trong URL template bằng giá trị từ input mappings
            var url = ResolveUrlTemplate(webNode, urlTemplate, connections, env);
            
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[WebNodeExecutor] ❌ FAILED: ExtractUrl is empty or invalid: '{url}'");
                env.OnNodeFailed?.Invoke(webNode, "ExtractUrl is empty or invalid.");
                return;
            }
            
            var methodStr = webNode.ExtractRequestMethod?.Trim();

            if (string.IsNullOrWhiteSpace(methodStr)) methodStr = "GET";
            var method = methodStr.ToUpperInvariant() switch
            {
                "POST" => System.Net.Http.HttpMethod.Post,
                "PUT" => System.Net.Http.HttpMethod.Put,
                "DELETE" => System.Net.Http.HttpMethod.Delete,
                "PATCH" => new System.Net.Http.HttpMethod("PATCH"),
                "HEAD" => System.Net.Http.HttpMethod.Head,
                "OPTIONS" => System.Net.Http.HttpMethod.Options,
                _ => System.Net.Http.HttpMethod.Get
            };

            if (!int.TryParse(webNode.ExtractStatusCode?.Trim(), out var expectedStatus))
                expectedStatus = 200;

            Debug.WriteLine($"[WebNodeExecutor] ✓ URL valid: {url}");
            Debug.WriteLine($"[WebNodeExecutor] ✓ Method: {methodStr}, Expected status: {expectedStatus}");

            try
            {
                using var request = new HttpRequestMessage(method, url);
                if (!string.IsNullOrWhiteSpace(cookie))
                    request.Headers.TryAddWithoutValidation("Cookie", cookie);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(env.CancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(60));
                using var response = await _httpClient.SendAsync(request, cts.Token);

                if (response.StatusCode != (System.Net.HttpStatusCode)expectedStatus)
                {
                    Debug.WriteLine($"[WebNodeExecutor] ❌ FAILED: Status code mismatch. Expected {expectedStatus}, got {(int)response.StatusCode}");
                    env.OnNodeFailed?.Invoke(webNode, $"Expected status {expectedStatus}, got {(int)response.StatusCode}");
                    return;
                }
                
                Debug.WriteLine($"[WebNodeExecutor] ✓ Status code matched: {(int)response.StatusCode}");

                // Cookie: CHỈ ghi đè khi ExtractUrl response có Set-Cookie.
                // Nếu không có, giữ nguyên LastCookie từ WebView2 (WebResourceResponseReceived).
                var setCookie = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
                    ? string.Join("; ", setCookies) : null;
                if (!string.IsNullOrWhiteSpace(setCookie))
                    webNode.LastCookie = setCookie;

                // Bearer: từ Authorization response header (một số API trả Bearer trong header)
                if (response.Headers.TryGetValues("Authorization", out var authHeaders))
                {
                    var auth = authHeaders.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        webNode.LastBearer = auth.Substring(7).Trim();
                }

                var body = await response.Content.ReadAsStringAsync(env.CancellationToken);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("access_token", out var at))
                            webNode.LastAccessToken = at.GetString();
                        else if (root.TryGetProperty("accessToken", out var at2))
                            webNode.LastAccessToken = at2.GetString();
                        else if (root.TryGetProperty("token", out var tok))
                            webNode.LastAccessToken = tok.GetString();
                    }
                    catch { }
                }

                // Không gọi lại các ResponseOutputs bằng HttpClient nữa.
                // ResponseOutputs bây giờ được cập nhật real-time từ WebView2 (WebResourceResponseReceived).

                Debug.WriteLine($"[WebNodeExecutor] ✓ Request successful, invoking OnNodeCompleted");
                env.OnNodeCompleted?.Invoke(webNode, default);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebNodeExecutor] ❌ EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                Debug.WriteLine($"[WebNodeExecutor] Stack trace: {ex.StackTrace}");
                env.OnNodeFailed?.Invoke(webNode, ex.Message);

                // Nếu request thất bại thì không còn ý nghĩa chờ outputs nữa.
                if (pendingOutputsTcs != null && !pendingOutputsTcs.Task.IsCompleted)
                {
                    try { pendingOutputsTcs.TrySetResult(false); } catch { }
                }
                return;
            }

            // Chờ WebView2 populate các ResponseOutputs (nếu có) trước khi traverse các node sau.
            // Tránh tình trạng node sau chạy quá sớm khi WebNode chưa kịp nhận dữ liệu từ WebView2.
            if (pendingOutputsTcs != null)
            {
                try
                {
                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(env.CancellationToken);
                    // Timeout bảo vệ để không bị treo workflow nếu vì lý do nào đó WebView2 không trả về.
                    var waitMs = effectiveWaitTimeoutMs;
                    if (waitMs <= 0)
                    {
                        // Không chờ nếu timeout = 0
                        pendingOutputsTcs.TrySetResult(false);
                    }
                    else
                    {
                        waitCts.CancelAfter(TimeSpan.FromMilliseconds(waitMs));
                        var _ = await Task.WhenAny(pendingOutputsTcs.Task, Task.Delay(Timeout.Infinite, waitCts.Token));
                        // Nếu bị hủy / timeout thì vẫn tiếp tục workflow, chỉ là outputs có thể rỗng.
                    }
                }
                catch (OperationCanceledException)
                {
                    // Bị hủy do workflow cancel/timeout → tiếp tục thoát bình thường.
                }
                finally
                {
                    // Dọn state runtime
                    webNode.PendingOutputsTcs = null;
                }
            }

            PublishScopedWebOutputs(env, webNode);

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

            if (webNode.ResponseOutputValues != null)
            {
                foreach (var kv in webNode.ResponseOutputValues)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                    service.SetScopedNodeStringOutput(env.ExecutionId, webNode.Id, kv.Key.Trim(), kv.Value ?? string.Empty);
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
