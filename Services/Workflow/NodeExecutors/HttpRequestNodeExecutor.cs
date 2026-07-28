using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utils;
using FlowMy.Utils;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using System.IO;
using HttpMethod = FlowMy.Models.Nodes.HttpMethod;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho HttpRequestNode.
    /// Thực hiện HTTP request với các cấu hình từ node (URL, Method, Headers, Params, Body, Auth).
    /// </summary>
    internal sealed class HttpRequestNodeExecutor : INodeExecutor
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public bool CanExecute(WorkflowNode node) => node is HttpRequestNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var httpNode = (HttpRequestNode)node;
            var connections = env.Connections;
            var allNodesForLookup = env.ReachableToEnd;
            var sw = Stopwatch.StartNew();
            string? boundRawCurlCommand = null;

            // Reset previous results
            httpNode.LastStatusCode = null;
            httpNode.LastResponseBody = null;
            httpNode.LastResponseHeaders = null;
            httpNode.LastIsSuccess = null;
            httpNode.LastErrorMessage = null;
            httpNode.LastResponseTimeMs = null;

            // ⚡ THREAD-SAFE SNAPSHOTS: Snapshot collections trước bất kỳ enumeration nào.
            // Khi nhiều async dispatch tasks chạy song song trên cùng một node instance,
            // việc enumerate ObservableCollection trực tiếp gây lỗi "Collection was modified".
            List<HttpKeyValuePair> headersSnapshot;
            List<HttpKeyValuePair> queryParamsSnapshot;
            List<HttpKeyValuePair> formDataSnapshot;
            lock (httpNode)
            {
                headersSnapshot = httpNode.Headers.ToList();
                queryParamsSnapshot = httpNode.QueryParams.ToList();
                formDataSnapshot = httpNode.FormData.ToList();
            }

            try
            {
                // 1. Check if cURL command is bound from another node (highest priority)
                if (!string.IsNullOrWhiteSpace(httpNode.CurlSourceNodeId) && 
                    !string.IsNullOrWhiteSpace(httpNode.CurlSourceOutputKey))
                {
                    var curlCommand = ResolveStringValue(
                        "", 
                        httpNode.CurlSourceNodeId, 
                        httpNode.CurlSourceOutputKey, 
                        connections, 
                        httpNode,
                        env,
                        allNodesForLookup);
                    curlCommand = NormalizeBoundCurlCommand(curlCommand);
                    
                    if (!string.IsNullOrWhiteSpace(curlCommand) && IsCurlCommand(curlCommand))
                    {
                        boundRawCurlCommand = curlCommand;
                        
                        // Check if the command is a stream request
                        var isStreamCurl = curlCommand.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);
                        
                        if (isStreamCurl)
                        {
                            // If it is a stream, we MUST parse and apply it to node config so we can stream it!
                            if (ParseAndApplyCurl(httpNode, curlCommand, out string errorMsg))
                            {
                                httpNode.IsStream = true;
                                boundRawCurlCommand = null;
                                lock (httpNode)
                                {
                                    headersSnapshot = httpNode.Headers.ToList();
                                    queryParamsSnapshot = httpNode.QueryParams.ToList();
                                    formDataSnapshot = httpNode.FormData.ToList();
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Failed to parse bound streaming cURL: {errorMsg}");
                            }
                        }
                        else
                        {
                            var willRunRawCurl = httpNode.UseCurl && httpNode.AutoAppendCurlWriteOut;
                            if (ParseAndApplyCurl(httpNode, curlCommand, out string errorMsg))
                            {
                                lock (httpNode)
                                {
                                    headersSnapshot = httpNode.Headers.ToList();
                                    queryParamsSnapshot = httpNode.QueryParams.ToList();
                                    formDataSnapshot = httpNode.FormData.ToList();
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Failed to parse bound cURL: {errorMsg}");
                            }
                        }
                    }
                }

                // 2. Resolve URL (static or dynamic binding)
                var url = ResolveStringValue(
                    httpNode.Url,
                    httpNode.UrlSourceNodeId,
                    httpNode.UrlSourceOutputKey,
                    connections,
                    httpNode,
                    env);

                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new InvalidOperationException("URL is empty or could not be resolved");
                }

                // Build query parameters
                var queryParams = new List<KeyValuePair<string, string>>();
                foreach (var param in queryParamsSnapshot.Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key)))
                {
                    var value = ResolveKeyValuePairValue(param, connections, httpNode, env);
                    queryParams.Add(new KeyValuePair<string, string>(param.Key, value));
                }

                // Add API Key as query param if configured (resolve value from binding when set)
                if (httpNode.AuthType == HttpAuthType.ApiKey && !httpNode.ApiKeyInHeader && !string.IsNullOrWhiteSpace(httpNode.ApiKeyName))
                {
                    var resolvedApiKeyValue = ResolveStringValue(
                        httpNode.ApiKeyValue ?? string.Empty,
                        httpNode.ApiKeyValueSourceNodeId,
                        httpNode.ApiKeyValueSourceOutputKey,
                        connections,
                        httpNode,
                        env);
                    queryParams.Add(new KeyValuePair<string, string>(httpNode.ApiKeyName, resolvedApiKeyValue));
                }

                // Append query params to URL
                if (queryParams.Count > 0)
                {
                    var uriBuilder = new UriBuilder(url);
                    var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                    foreach (var param in queryParams)
                    {
                        query[param.Key] = param.Value;
                    }
                    uriBuilder.Query = query.ToString();
                    url = uriBuilder.ToString();
                }

                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Executing {httpNode.HttpMethod} {url}");

                // Resolve headers and body using shared helper
                ResolveHeadersAndBody(httpNode, connections, env, formDataSnapshot, out var resolvedHeaders, out var resolvedBody);

                // Auto-detect stream based on request headers
                var isStream = httpNode.IsStream;
                if (!isStream)
                {
                    if (resolvedHeaders.TryGetValue("Accept", out var acceptHeader) &&
                        acceptHeader.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
                    {
                        isStream = true;
                        httpNode.IsStream = true;
                    }
                }

                if (isStream)
                {
                    if (httpNode.UseCurl)
                    {
                        await ExecuteStreamViaCurlExeAsync(httpNode, url, resolvedHeaders, resolvedBody, connections, env, sw);
                    }
                    else
                    {
                        await ExecuteStreamViaHttpClientAsync(httpNode, url, resolvedHeaders, resolvedBody, connections, env, sw);
                    }
                    return;
                }

                // ⚡ BYPASS MODE: dùng libcurl (CurlThin/curl.exe) thay HttpClient
                if (httpNode.UseCurl)
                {
                    // Raw cURL mode toggle:
                    // - enabled  => run bound cURL command directly (cmd/curl.exe style)
                    // - disabled => keep old logic (parse/build request then execute via curl backends)
                    if (httpNode.AutoAppendCurlWriteOut && !string.IsNullOrWhiteSpace(boundRawCurlCommand))
                    {
                        await ExecuteRawCurlCommandAsync(httpNode, boundRawCurlCommand, connections, env, sw);
                        return;
                    }

                    await ExecuteViaCurlAsync(httpNode, url, resolvedHeaders, resolvedBody, connections, env, sw);
                    return;
                }

                // Create HTTP request
                var method = GetHttpMethod(httpNode.HttpMethod);
                using var request = new HttpRequestMessage(method, url);

                // Add headers
                foreach (var header in resolvedHeaders)
                {
                    try
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Failed to add header {header.Key}: {ex.Message}");
                    }
                }

                // Add body
                if (resolvedBody != null)
                {
                    var contentType = resolvedHeaders.TryGetValue("Content-Type", out var ctHeader) ? ctHeader : "application/json";
                    request.Content = new StringContent(resolvedBody, Encoding.UTF8, contentType);
                }

                // Set timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(env.CancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(httpNode.TimeoutSeconds));

                // Execute request with ResponseHeadersRead completion option
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                // Auto-detect response stream at runtime
                var isStreamResponse = false;
                if (response.Headers.TransferEncodingChunked == true || 
                    (response.Content.Headers.ContentType?.MediaType?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (response.Headers.Connection.Any(c => string.Equals(c, "keep-alive", StringComparison.OrdinalIgnoreCase)) && 
                     response.Headers.TransferEncoding.Any(te => string.Equals(te.Value, "chunked", StringComparison.OrdinalIgnoreCase))))
                {
                    isStreamResponse = true;
                    httpNode.IsStream = true;
                }

                if (isStreamResponse)
                {
                    await ProcessStreamResponseAsync(httpNode, response, resolvedHeaders, connections, env, sw, cts.Token);
                    return;
                }

                sw.Stop();

                // Read response
                var responseBody = await response.Content.ReadAsStringAsync();
                var responseHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                // Anti-bot can appear either as:
                // - HTTP 403 directly, or
                // - JSON body with anti-bot markers/code=7 while status may still be 200.
                // In both cases, retry via cURL backend to better match cURL/Postman behavior.
                if ((int)response.StatusCode == 403 || LooksLikeAntiBotResponse(responseBody))
                {
                    Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Anti-bot/403 detected from HttpClient, retrying via cURL backend");
                    var curlRetrySw = Stopwatch.StartNew();
                    await ExecuteViaCurlAsync(httpNode, url, resolvedHeaders, resolvedBody, connections, env, curlRetrySw);
                    return;
                }

                // Generate cURL command with resolved values
                httpNode.LastCurlCommand = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);

                // Set results on node
                httpNode.LastStatusCode = (int)response.StatusCode;
                httpNode.LastResponseBody = responseBody;
                httpNode.LastResponseHeaders = responseHeaders;
                httpNode.LastIsSuccess = response.IsSuccessStatusCode;
                httpNode.LastErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                PublishScopedHttpOutputs(
                    env, httpNode,
                    httpNode.LastStatusCode,
                    responseBody,
                    responseHeaders,
                    httpNode.LastIsSuccess,
                    httpNode.LastErrorMessage,
                    httpNode.LastResponseTimeMs,
                    httpNode.LastCurlCommand);

                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Response {httpNode.LastStatusCode} in {httpNode.LastResponseTimeMs}ms");
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Response body length: {responseBody?.Length ?? 0}");
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] cURL command generated");
            }
            // Timeout / lỗi kết nối HTTP: đánh dấu lỗi nhưng KHÔNG dừng toàn bộ workflow
            catch (TaskCanceledException ex) when (ex.CancellationToken != env.CancellationToken)
            {
                sw.Stop();
                // Generate cURL even on timeout/error
                httpNode.LastCurlCommand = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);
                httpNode.LastIsSuccess = false;
                httpNode.LastErrorMessage = $"Request timeout after {httpNode.TimeoutSeconds} seconds";
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                PublishScopedHttpOutputs(
                    env, httpNode,
                    null,
                    null,
                    null,
                    httpNode.LastIsSuccess,
                    httpNode.LastErrorMessage,
                    httpNode.LastResponseTimeMs,
                    httpNode.LastCurlCommand);
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Timeout - {httpNode.LastErrorMessage}");
                // HTTP node exposes errors via output keys (errorMessage/isSuccess),
                // so we intentionally do not mark node as "failed" badge here.
            }
            // Các lỗi runtime khác (trừ khi là huỷ thủ công) cũng chỉ set lỗi và cho phép workflow đi tiếp
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not TaskCanceledException)
            {
                sw.Stop();
                // Generate cURL even on error
                httpNode.LastCurlCommand = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);
                httpNode.LastIsSuccess = false;
                httpNode.LastErrorMessage = ex.Message;
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                PublishScopedHttpOutputs(
                    env, httpNode,
                    null,
                    null,
                    null,
                    httpNode.LastIsSuccess,
                    httpNode.LastErrorMessage,
                    httpNode.LastResponseTimeMs,
                    httpNode.LastCurlCommand);
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Error - {ex.Message}");
                // HTTP node exposes errors via output keys (errorMessage/isSuccess),
                // so we intentionally do not mark node as "failed" badge here.
            }

            env.OnNodeCompleted?.Invoke(httpNode, sw.Elapsed);

            await env.TraverseOutputsAsync(httpNode);
        }

        private void ResolveHeadersAndBody(
            HttpRequestNode httpNode,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env,
            List<HttpKeyValuePair> formDataSnapshot,
            out Dictionary<string, string> resolvedHeaders,
            out string? resolvedBody)
        {
            List<HttpKeyValuePair> headersSnapshot;
            lock (httpNode)
            {
                headersSnapshot = httpNode.Headers.ToList();
            }

            resolvedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headersSnapshot.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key)))
            {
                resolvedHeaders[header.Key] = ResolveKeyValuePairValue(header, connections, httpNode, env);
            }

            // Add auth headers
            AddAuthToHeaders(resolvedHeaders, httpNode, connections, env);

            // Default User-Agent to bypass simple blocks
            if (!resolvedHeaders.ContainsKey("User-Agent"))
            {
                resolvedHeaders["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
            }

            resolvedBody = null;
            if (httpNode.HttpMethod != HttpMethod.GET && httpNode.HttpMethod != HttpMethod.HEAD)
            {
                switch (httpNode.BodyType)
                {
                    case HttpBodyType.Raw:
                        resolvedBody = ResolveStringValue(httpNode.RawBody, httpNode.BodySourceNodeId, httpNode.BodySourceOutputKey, connections, httpNode, env);
                        break;
                    case HttpBodyType.Json:
                        var jsonBody = ResolveStringValue(httpNode.RawBody, httpNode.BodySourceNodeId, httpNode.BodySourceOutputKey, connections, httpNode, env);
                        resolvedBody = EscapeJsonStringValues(jsonBody, connections, httpNode, env);
                        if (!resolvedHeaders.ContainsKey("Content-Type"))
                            resolvedHeaders["Content-Type"] = "application/json";
                        break;
                    case HttpBodyType.FormUrlEncoded:
                        var formParts = new List<string>();
                        foreach (var item in formDataSnapshot.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Key)))
                            formParts.Add($"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(ResolveKeyValuePairValue(item, connections, httpNode, env))}");
                        resolvedBody = string.Join("&", formParts);
                        if (!resolvedHeaders.ContainsKey("Content-Type"))
                            resolvedHeaders["Content-Type"] = "application/x-www-form-urlencoded";
                        break;
                }
            }
        }

        /// <summary>
        /// Thực hiện HTTP request qua libcurl (CurlThin/curl.exe bypass).
        /// </summary>
        private async Task ExecuteViaCurlAsync(
            HttpRequestNode httpNode,
            string url,
            Dictionary<string, string> resolvedHeaders,
            string? resolvedBody,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env,
            Stopwatch sw)
        {
            try
            {
                Debug.WriteLine($"[HttpNode-Curl] {httpNode.HttpMethod} {url}, Headers={resolvedHeaders.Count}, UseCurl=true");

                var curlResult = await CurlNativeExecutor.ExecuteAsync(httpNode, url, resolvedHeaders, resolvedBody, env.CancellationToken);
                sw.Stop();

                // Generate cURL command for display
                httpNode.LastCurlCommand = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);

                httpNode.LastStatusCode = curlResult.StatusCode;
                httpNode.LastResponseBody = curlResult.Body;
                httpNode.LastResponseHeaders = curlResult.Headers.Count > 0 ? curlResult.Headers : new Dictionary<string, string>();
                httpNode.LastIsSuccess = curlResult.IsSuccess;
                httpNode.LastErrorMessage = curlResult.IsSuccess ? null : curlResult.ErrorMessage;
                httpNode.LastResponseTimeMs = curlResult.ElapsedMs > 0 ? curlResult.ElapsedMs : sw.ElapsedMilliseconds;
                PublishScopedHttpOutputs(
                    env, httpNode,
                    httpNode.LastStatusCode,
                    httpNode.LastResponseBody,
                    httpNode.LastResponseHeaders,
                    httpNode.LastIsSuccess,
                    httpNode.LastErrorMessage,
                    httpNode.LastResponseTimeMs,
                    httpNode.LastCurlCommand);

                Debug.WriteLine($"[HttpNode-Curl] Backend={curlResult.Backend}, Status={curlResult.StatusCode}, Time={httpNode.LastResponseTimeMs}ms");

                // Keep workflow moving; HTTP errors are surfaced in outputs only.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sw.Stop();
                httpNode.LastCurlCommand = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);
                httpNode.LastIsSuccess = false;
                httpNode.LastErrorMessage = ex.Message;
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                PublishScopedHttpOutputs(
                    env, httpNode,
                    null,
                    null,
                    null,
                    httpNode.LastIsSuccess,
                    httpNode.LastErrorMessage,
                    httpNode.LastResponseTimeMs,
                    httpNode.LastCurlCommand);
                Debug.WriteLine($"[HttpNode-Curl] Error: {ex.Message}");
                // Keep workflow moving; HTTP errors are surfaced in outputs only.
            }
            finally
            {
                env.OnNodeCompleted?.Invoke(httpNode, sw.Elapsed);
                await env.TraverseOutputsAsync(httpNode);
            }
        }

        /// <summary>
        /// Resolve auth thành headers dict cho curl bypass mode.
        /// </summary>
        private void AddAuthToHeaders(Dictionary<string, string> headers, HttpRequestNode httpNode, List<WorkflowConnection> connections, NodeExecutionEnvironment env)
        {
            switch (httpNode.AuthType)
            {
                case HttpAuthType.Basic:
                    if (!string.IsNullOrWhiteSpace(httpNode.AuthUsername))
                    {
                        var credentials = Convert.ToBase64String(
                            System.Text.Encoding.UTF8.GetBytes($"{httpNode.AuthUsername}:{httpNode.AuthPassword ?? string.Empty}"));
                        headers["Authorization"] = $"Basic {credentials}";
                    }
                    break;
                case HttpAuthType.Bearer:
                    var token = ResolveStringValue(httpNode.AuthToken ?? string.Empty, httpNode.TokenSourceNodeId, httpNode.TokenSourceOutputKey, connections, httpNode, env);
                    if (!string.IsNullOrWhiteSpace(token))
                        headers["Authorization"] = $"Bearer {token}";
                    break;
                case HttpAuthType.ApiKey:
                    if (httpNode.ApiKeyInHeader && !string.IsNullOrWhiteSpace(httpNode.ApiKeyName))
                    {
                        var apiKeyVal = ResolveStringValue(httpNode.ApiKeyValue ?? string.Empty, httpNode.ApiKeyValueSourceNodeId, httpNode.ApiKeyValueSourceOutputKey, connections, httpNode, env);
                        headers[httpNode.ApiKeyName] = apiKeyVal;
                    }
                    break;
            }
        }

        private static System.Net.Http.HttpMethod GetHttpMethod(HttpMethod method)
        {
            return method switch
            {
                HttpMethod.GET => System.Net.Http.HttpMethod.Get,
                HttpMethod.POST => System.Net.Http.HttpMethod.Post,
                HttpMethod.PUT => System.Net.Http.HttpMethod.Put,
                HttpMethod.DELETE => System.Net.Http.HttpMethod.Delete,
                HttpMethod.PATCH => System.Net.Http.HttpMethod.Patch,
                HttpMethod.HEAD => System.Net.Http.HttpMethod.Head,
                HttpMethod.OPTIONS => System.Net.Http.HttpMethod.Options,
                _ => System.Net.Http.HttpMethod.Get
            };
        }

        private void AddAuthentication(HttpRequestMessage request, HttpRequestNode httpNode, List<WorkflowConnection> connections, NodeExecutionEnvironment env)
        {
            switch (httpNode.AuthType)
            {
                case HttpAuthType.Basic:
                    if (!string.IsNullOrWhiteSpace(httpNode.AuthUsername))
                    {
                        var credentials = Convert.ToBase64String(
                            Encoding.UTF8.GetBytes($"{httpNode.AuthUsername}:{httpNode.AuthPassword ?? string.Empty}"));
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    }
                    break;

                case HttpAuthType.Bearer:
                    var bearerToken = ResolveStringValue(
                        httpNode.AuthToken ?? string.Empty,
                        httpNode.TokenSourceNodeId,
                        httpNode.TokenSourceOutputKey,
                        connections,
                        httpNode,
                        env);
                    if (!string.IsNullOrWhiteSpace(bearerToken))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                    }
                    break;

                case HttpAuthType.ApiKey:
                    if (httpNode.ApiKeyInHeader && !string.IsNullOrWhiteSpace(httpNode.ApiKeyName))
                    {
                        var apiKeyValue = ResolveStringValue(
                            httpNode.ApiKeyValue ?? string.Empty,
                            httpNode.ApiKeyValueSourceNodeId,
                            httpNode.ApiKeyValueSourceOutputKey,
                            connections,
                            httpNode,
                            env);
                        request.Headers.TryAddWithoutValidation(httpNode.ApiKeyName, apiKeyValue);
                    }
                    // Query param is handled in URL building
                    break;
            }
        }

        private void AddBody(HttpRequestMessage request, HttpRequestNode httpNode, List<WorkflowConnection> connections, NodeExecutionEnvironment env, List<HttpKeyValuePair> formDataSnapshot)
        {
            if (httpNode.HttpMethod == HttpMethod.GET || httpNode.HttpMethod == HttpMethod.HEAD)
            {
                return; // GET and HEAD typically don't have body
            }

            switch (httpNode.BodyType)
            {
                case HttpBodyType.Raw:
                    var rawBody = ResolveStringValue(
                        httpNode.RawBody,
                        httpNode.BodySourceNodeId,
                        httpNode.BodySourceOutputKey,
                        connections,
                        httpNode,
                        env);
                    if (!string.IsNullOrEmpty(rawBody))
                    {
                        request.Content = new StringContent(rawBody, Encoding.UTF8, "text/plain");
                    }
                    break;

                case HttpBodyType.Json:
                    var jsonBody = ResolveStringValue(
                        httpNode.RawBody,
                        httpNode.BodySourceNodeId,
                        httpNode.BodySourceOutputKey,
                        connections,
                        httpNode,
                        env);
                    if (!string.IsNullOrEmpty(jsonBody))
                    {
                        // ⚠️ CRITICAL: JSON-escape các giá trị được replace từ variables (như {base64})
                        // để tránh lỗi khi base64 string chứa ký tự đặc biệt làm hỏng JSON structure
                        jsonBody = EscapeJsonStringValues(jsonBody, connections, httpNode, env);
                        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    }
                    break;

                case HttpBodyType.FormUrlEncoded:
                    var formData = new List<KeyValuePair<string, string>>();
                    foreach (var item in formDataSnapshot.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Key)))
                    {
                        var value = ResolveKeyValuePairValue(item, connections, httpNode, env);
                        formData.Add(new KeyValuePair<string, string>(item.Key, value));
                    }
                    if (formData.Count > 0)
                    {
                        request.Content = new FormUrlEncodedContent(formData);
                    }
                    break;

                case HttpBodyType.FormData:
                    var multipartContent = new MultipartFormDataContent();
                    foreach (var item in formDataSnapshot.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Key)))
                    {
                        var value = ResolveKeyValuePairValue(item, connections, httpNode, env);
                        multipartContent.Add(new StringContent(value), item.Key);
                    }
                    if (formDataSnapshot.Any(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Key)))
                    {
                        request.Content = multipartContent;
                    }
                    break;
            }
        }

        /// <summary>
        /// Escape JSON string values trong JSON body khi replace variables.
        /// Tìm các placeholder {variable} và replace với giá trị đã được JSON-escape.
        /// </summary>
        private string EscapeJsonStringValues(
            string jsonBody,
            List<WorkflowConnection> connections,
            HttpRequestNode currentNode,
            NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(jsonBody))
                return jsonBody;

            // Pattern để tìm {variable} trong JSON string
            // Dùng [^{}]+ để tránh match nhầm với nested braces trong JSON
            var pattern = @"\{([^{}]+)\}";
            var result = System.Text.RegularExpressions.Regex.Replace(jsonBody, pattern, match =>
            {
                var variableKey = match.Groups[1].Value.Trim();
                
                // Tìm giá trị từ các node connections
                var value = ResolveVariableValue(variableKey, connections, currentNode, env);
                
                if (value == null)
                {
                    // Variable không tìm thấy, giữ nguyên placeholder
                    Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Variable '{variableKey}' not found in JSON body, keeping placeholder");
                    return match.Value;
                }

                // JSON-escape giá trị bằng cách serialize nó như một string
                // Điều này sẽ tự động escape các ký tự đặc biệt như ", \, \n, etc.
                var escapedValue = System.Text.Json.JsonSerializer.Serialize(value);
                
                // Bỏ dấu ngoặc kép ở đầu và cuối vì chúng ta chỉ cần escaped string value
                // (không cần quotes vì đã có trong JSON structure)
                if (escapedValue.StartsWith("\"") && escapedValue.EndsWith("\""))
                {
                    escapedValue = escapedValue.Substring(1, escapedValue.Length - 2);
                }
                
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Replaced '{match.Value}' with JSON-escaped value (length: {escapedValue.Length})");
                return escapedValue;
            });

            return result;
        }

        /// <summary>
        /// Resolve giá trị của một variable từ connections hoặc node outputs.
        /// </summary>
        private string? ResolveVariableValue(
            string variableKey,
            List<WorkflowConnection> connections,
            HttpRequestNode currentNode,
            NodeExecutionEnvironment env)
        {
            // Tìm trong tất cả các upstream nodes
            var upstreamNodes = connections
                .Where(c => c.ToNode == currentNode && c.FromNode != null)
                .Select(c => c.FromNode!)
                .Distinct()
                .ToList();

            foreach (var node in upstreamNodes)
            {
                var value = env.Service.ResolveDynamicValueForExecution(node, variableKey, env);
                if (value != null && value != "—" && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolve a string value that may have dynamic binding from another node.
        /// </summary>
        private string ResolveStringValue(
            string staticValue,
            string? sourceNodeId,
            string? sourceOutputKey,
            List<WorkflowConnection> connections,
            HttpRequestNode currentNode,
            NodeExecutionEnvironment env,
            IEnumerable<WorkflowNode>? allNodesForLookup = null)
        {
            // If no dynamic binding, return static value
            if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(sourceOutputKey))
            {
                return staticValue ?? string.Empty;
            }

            // Find source node (with broader lookup so indirect nodes are also found)
            var sourceNode = FindSourceNode(sourceNodeId, connections, currentNode, allNodesForLookup);
            if (sourceNode == null)
            {
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Source node {sourceNodeId} not found");
                return staticValue ?? string.Empty;
            }

            // Resolve value from source node
            var value = env.Service.ResolveDynamicValueForExecution(sourceNode, sourceOutputKey, env);
            if (value == "—" || string.IsNullOrWhiteSpace(value))
            {
                return staticValue ?? string.Empty;
            }

            return value;
        }

        private static string NormalizeBoundCurlCommand(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var text = input.Trim();

            // If value comes from KeyValueBridge or JSON snapshot
            var extracted = TryExtractFirstStringFromJsonContainer(text);
            if (!string.IsNullOrWhiteSpace(extracted))
                text = extracted.Trim();

            // Decode escaped JSON string artifacts (\" \r\n \uXXXX ...)
            text = DecodeJsonEscapes(text).Trim();

            // Strip enclosing quotes (single, double, or backticks)
            if ((text.StartsWith("\"") && text.EndsWith("\"")) ||
                (text.StartsWith("'") && text.EndsWith("'")) ||
                (text.StartsWith("`") && text.EndsWith("`")))
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }

            return text;
        }

        private static string? TryExtractFirstStringFromJsonContainer(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (!(text.StartsWith("{", StringComparison.Ordinal) && text.EndsWith("}", StringComparison.Ordinal)))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        return prop.Value.GetString();

                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                                return item.GetString();
                        }
                    }
                }
            }
            catch
            {
                // ignore parse errors, treat as plain text
            }

            return null;
        }

        private static string DecodeJsonEscapes(string input)
        {
            if (string.IsNullOrEmpty(input) || input.IndexOf('\\') < 0) return input;
            try
            {
                var encoded = JsonSerializer.Serialize(input);
                var decoded = JsonSerializer.Deserialize<string>(encoded);
                return decoded ?? input;
            }
            catch
            {
                return input;
            }
        }

        private string ResolveKeyValuePairValue(
            HttpKeyValuePair kvp,
            List<WorkflowConnection> connections,
            HttpRequestNode currentNode,
            NodeExecutionEnvironment env)
        {
            // If no dynamic binding, return static value
            if (string.IsNullOrWhiteSpace(kvp.SourceNodeId) || string.IsNullOrWhiteSpace(kvp.SourceOutputKey))
                return kvp.Value ?? string.Empty;

            // Find source node
            var sourceNode = FindSourceNode(kvp.SourceNodeId, connections, currentNode);
            if (sourceNode == null)
            {
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Source node {kvp.SourceNodeId} not found for '{kvp.Key}', falling back to static value");
                return kvp.Value ?? string.Empty;
            }

            // Resolve value from source node
            var value = env.Service.ResolveDynamicValueForExecution(sourceNode, kvp.SourceOutputKey, env);
            if (value == "—" || string.IsNullOrWhiteSpace(value))
                return kvp.Value ?? string.Empty;

            return value;
        }

        /// <summary>
        /// Find a source node by ID from connections or graph.
        /// allNodesForLookup (ReachableToEnd) is searched first for the most reliable result.
        /// </summary>
        private static WorkflowNode? FindSourceNode(
            string sourceNodeId,
            List<WorkflowConnection> connections,
            HttpRequestNode currentNode,
            IEnumerable<WorkflowNode>? allNodesForLookup = null)
        {
            // 1. Search in allNodesForLookup (ReachableToEnd) first — most reliable
            if (allNodesForLookup != null)
            {
                var fromAll = allNodesForLookup.FirstOrDefault(n =>
                    string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
                if (fromAll != null) return fromAll;
            }

            // 2. Try direct connection
            var upstreamConnection = connections
                .FirstOrDefault(c =>
                    c.ToNode == currentNode &&
                    c.FromNode != null &&
                    c.FromNode.Id == sourceNodeId);

            if (upstreamConnection?.FromNode != null)
            {
                return upstreamConnection.FromNode;
            }

            // 3. Fallback: find node by ID anywhere in connection graph
            return connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if text is a cURL command.
        /// </summary>
        private bool IsCurlCommand(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var trimmed = text.Trim().Trim('"', '\'', '`', '\r', '\n');
            if (trimmed.StartsWith("curl", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.Length == 4) return true;
                char next = trimmed[4];
                if (char.IsWhiteSpace(next) || next == '.' || next == '-' || next == '\r' || next == '\n')
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Parse cURL command and apply to node.
        /// Returns true if parsing was successful.
        /// </summary>
        private bool ParseAndApplyCurl(HttpRequestNode node, string curlCommand, out string errorMsg)
        {
            try
            {
                var result = CurlParser.Parse(curlCommand);
                if (!result.IsValid)
                {
                    errorMsg = result.ErrorMessage;
                    Debug.WriteLine($"[HttpRequestNode] cURL parse failed: {errorMsg}");
                    return false;
                }
                
                // Validate essential fields
                if (string.IsNullOrWhiteSpace(result.Url))
                {
                    errorMsg = "Parsed cURL has no URL";
                    Debug.WriteLine($"[HttpRequestNode] cURL parse error: {errorMsg}");
                    return false;
                }
                
                Debug.WriteLine($"[HttpRequestNode] Parsed URL: {result.Url}");
                Debug.WriteLine($"[HttpRequestNode] Parsed Method: {result.Method}");
                Debug.WriteLine($"[HttpRequestNode] Parsed Headers: {result.Headers?.Count ?? 0}");
                Debug.WriteLine($"[HttpRequestNode] Parsed AuthType: {result.AuthType}");
                Debug.WriteLine($"[HttpRequestNode] Parsed BodyType: {result.BodyType}");
                
                // Apply parsed values to node under lock to avoid concurrent writes when
                // AsyncTask dispatch executes the same HttpRequestNode in parallel.
                lock (node)
                {
                    node.Url = result.Url;
                    node.HttpMethod = result.Method;

                    // Update headers
                    if (result.Headers != null && result.Headers.Count > 0)
                    {
                        node.Headers.Clear();
                        foreach (var h in result.Headers)
                        {
                            node.Headers.Add(new HttpKeyValuePair
                            {
                                Key = h.Key,
                                Value = h.Value,
                                IsEnabled = h.IsEnabled
                            });
                        }
                    }

                    // Update query params
                    if (result.QueryParams != null && result.QueryParams.Count > 0)
                    {
                        node.QueryParams.Clear();
                        foreach (var p in result.QueryParams)
                        {
                            node.QueryParams.Add(new HttpKeyValuePair
                            {
                                Key = p.Key,
                                Value = p.Value,
                                IsEnabled = p.IsEnabled
                            });
                        }
                    }

                    // Update auth
                    node.AuthType = result.AuthType;
                    if (result.AuthType == HttpAuthType.Basic)
                    {
                        node.AuthUsername = result.AuthUsername;
                        node.AuthPassword = result.AuthPassword;
                    }
                    else if (result.AuthType == HttpAuthType.Bearer)
                    {
                        node.AuthToken = result.AuthToken;
                    }

                    // Update body
                    node.BodyType = result.BodyType;
                    if (result.BodyType == HttpBodyType.Raw || result.BodyType == HttpBodyType.Json)
                    {
                        node.RawBody = result.RawBody ?? string.Empty;
                    }
                    else if (result.BodyType == HttpBodyType.FormData || result.BodyType == HttpBodyType.FormUrlEncoded)
                    {
                        node.FormData.Clear();
                        foreach (var f in result.FormData)
                        {
                            node.FormData.Add(new HttpKeyValuePair
                            {
                                Key = f.Key,
                                Value = f.Value,
                                IsEnabled = f.IsEnabled
                            });
                        }
                    }
                }
                
                errorMsg = "";
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        private async Task ExecuteRawCurlCommandAsync(
            HttpRequestNode httpNode,
            string rawCurlCommand,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env,
            Stopwatch sw)
        {
            try
            {
                var curlResult = await CurlNativeExecutor.ExecuteRawCommandAsync(httpNode, rawCurlCommand, env.CancellationToken);

                // If raw execution resulted in HTTP 0 (connection error or execution failure), try fallback via ParseAndApply execute
                if (curlResult.StatusCode == 0 && IsCurlCommand(rawCurlCommand))
                {
                    var parsed = CurlParser.Parse(rawCurlCommand);
                    if (parsed.IsValid)
                    {
                        Debug.WriteLine($"[HttpNode-CurlRaw] Raw command returned HTTP 0, trying fallback parsed execution to {parsed.Url}");
                        var parsedHeaders = parsed.Headers.ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);
                        await ExecuteViaCurlAsync(httpNode, parsed.Url, parsedHeaders, parsed.RawBody, connections, env, sw);
                        return;
                    }
                }

                sw.Stop();

                httpNode.LastCurlCommand = rawCurlCommand;
                httpNode.LastStatusCode = curlResult.StatusCode;
                httpNode.LastResponseBody = curlResult.Body;
                httpNode.LastResponseHeaders = curlResult.Headers.Count > 0 ? curlResult.Headers : new Dictionary<string, string>();
                httpNode.LastIsSuccess = curlResult.IsSuccess;
                httpNode.LastErrorMessage = curlResult.IsSuccess ? null : curlResult.ErrorMessage;
                httpNode.LastResponseTimeMs = curlResult.ElapsedMs > 0 ? curlResult.ElapsedMs : sw.ElapsedMilliseconds;
                PublishScopedHttpOutputs(
                    env, httpNode,
                    httpNode.LastStatusCode,
                    httpNode.LastResponseBody,
                    httpNode.LastResponseHeaders,
                    httpNode.LastIsSuccess,
                    httpNode.LastErrorMessage,
                    httpNode.LastResponseTimeMs,
                    httpNode.LastCurlCommand);

                Debug.WriteLine($"[HttpNode-CurlRaw] Backend={curlResult.Backend}, Status={curlResult.StatusCode}, Time={httpNode.LastResponseTimeMs}ms");

                // Keep workflow moving; HTTP errors are surfaced in outputs only.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sw.Stop();
                httpNode.LastCurlCommand = rawCurlCommand;
                httpNode.LastIsSuccess = false;
                httpNode.LastErrorMessage = ex.Message;
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                PublishScopedHttpOutputs(
                    env, httpNode,
                    null,
                    null,
                    null,
                    httpNode.LastIsSuccess,
                    httpNode.LastErrorMessage,
                    httpNode.LastResponseTimeMs,
                    httpNode.LastCurlCommand);
                Debug.WriteLine($"[HttpNode-CurlRaw] Error: {ex.Message}");
                // Keep workflow moving; HTTP errors are surfaced in outputs only.
            }
            finally
            {
                env.OnNodeCompleted?.Invoke(httpNode, sw.Elapsed);
                await env.TraverseOutputsAsync(httpNode);
            }
        }

        private static void PublishScopedHttpOutputs(
            string executionId,
            WorkflowExecutionService service,
            HttpRequestNode node,
            int? statusCode,
            string? responseBody,
            Dictionary<string, string>? responseHeaders,
            bool? isSuccess,
            string? errorMessage,
            long? responseTimeMs,
            string? curlCommand)
        {
            if (service == null || string.IsNullOrWhiteSpace(executionId) || node == null) return;
            // Always publish all known keys into scoped store for this execution.
            // This prevents fallback to shared node.Last* values from another parallel branch.
            service.SetScopedNodeStringOutput(executionId, node.Id, "statuscode",
                statusCode.HasValue ? statusCode.Value.ToString() : string.Empty);
            service.SetScopedNodeStringOutput(executionId, node.Id, "responsebody",
                responseBody ?? string.Empty);
            service.SetScopedNodeStringOutput(executionId, node.Id, "responseheaders",
                responseHeaders != null && responseHeaders.Count > 0
                    ? JsonSerializer.Serialize(responseHeaders)
                    : string.Empty);
            service.SetScopedNodeStringOutput(executionId, node.Id, "issuccess",
                isSuccess.HasValue ? isSuccess.Value.ToString() : string.Empty);
            service.SetScopedNodeStringOutput(executionId, node.Id, "errormessage",
                errorMessage ?? string.Empty);
            service.SetScopedNodeStringOutput(executionId, node.Id, "responsetimems",
                responseTimeMs.HasValue ? responseTimeMs.Value.ToString() : string.Empty);
            service.SetScopedNodeStringOutput(executionId, node.Id, "curl",
                curlCommand ?? string.Empty);
        }

        private static void PublishScopedHttpOutputs(
            NodeExecutionEnvironment env,
            HttpRequestNode node,
            int? statusCode,
            string? responseBody,
            Dictionary<string, string>? responseHeaders,
            bool? isSuccess,
            string? errorMessage,
            long? responseTimeMs,
            string? curlCommand)
        {
            PublishScopedHttpOutputs(
                env?.ExecutionId ?? string.Empty,
                env?.Service!,
                node,
                statusCode,
                responseBody,
                responseHeaders,
                isSuccess,
                errorMessage,
                responseTimeMs,
                curlCommand);
        }

        private async Task ExecuteStreamViaHttpClientAsync(
            HttpRequestNode httpNode,
            string url,
            Dictionary<string, string> resolvedHeaders,
            string? resolvedBody,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env,
            Stopwatch sw)
        {
            try
            {
                var method = GetHttpMethod(httpNode.HttpMethod);
                using var request = new HttpRequestMessage(method, url);

                foreach (var h in resolvedHeaders)
                {
                    try
                    {
                        request.Headers.TryAddWithoutValidation(h.Key, h.Value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[HttpNode-Stream][{env.ExecutionId}] Failed to add header {h.Key}: {ex.Message}");
                    }
                }

                if (resolvedBody != null)
                {
                    var contentType = resolvedHeaders.TryGetValue("Content-Type", out var ctHeader) ? ctHeader : "application/json";
                    request.Content = new StringContent(resolvedBody, Encoding.UTF8, contentType);
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(env.CancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(httpNode.TimeoutSeconds));

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                await ProcessStreamResponseAsync(httpNode, response, resolvedHeaders, connections, env, sw, cts.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sw.Stop();
                httpNode.LastIsSuccess = false;
                httpNode.LastErrorMessage = ex.Message;
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;

                PublishScopedHttpOutputs(
                    env.ExecutionId,
                    env.Service,
                    httpNode,
                    httpNode.LastStatusCode,
                    null,
                    httpNode.LastResponseHeaders,
                    false,
                    ex.Message,
                    sw.ElapsedMilliseconds,
                    httpNode.LastCurlCommand);

                Debug.WriteLine($"[HttpNode-Stream][{env.ExecutionId}] Error - {ex.Message}");
                env.OnNodeCompleted?.Invoke(httpNode, sw.Elapsed);
            }
        }

        private async Task ProcessStreamResponseAsync(
            HttpRequestNode httpNode,
            HttpResponseMessage response,
            Dictionary<string, string> resolvedHeaders,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env,
            Stopwatch sw,
            CancellationToken token)
        {
            var service = env.Service;

            try
            {
                var responseHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                httpNode.LastStatusCode = (int)response.StatusCode;
                httpNode.LastResponseHeaders = responseHeaders;
                httpNode.LastIsSuccess = response.IsSuccessStatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}. Details: {errorBody}");
                }

                // Read response stream
                using var stream = await response.Content.ReadAsStreamAsync(token);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                int chunkIndex = 0;
                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(token);
                    if (line == null) continue;

                    chunkIndex++;
                    var streamExecutionId = $"{env.ExecutionId}:stream-{chunkIndex}";

                    // Publish chunk outputs to this streamExecutionId
                    PublishScopedHttpOutputs(
                        streamExecutionId,
                        service,
                        httpNode,
                        (int)response.StatusCode,
                        line,
                        responseHeaders,
                        response.IsSuccessStatusCode,
                        null,
                        sw.ElapsedMilliseconds,
                        httpNode.LastCurlCommand);

                    // Traverse downstream for this chunk asynchronously without blocking the stream reader
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await TraverseDownstreamForStreamAsync(httpNode, env, streamExecutionId, chunkIndex);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[HttpNode-Stream] Downstream error: {ex.Message}");
                        }
                    });
                }

                sw.Stop();
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                httpNode.LastErrorMessage = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sw.Stop();
                httpNode.LastIsSuccess = false;
                httpNode.LastErrorMessage = ex.Message;
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;

                PublishScopedHttpOutputs(
                    env.ExecutionId,
                    service,
                    httpNode,
                    httpNode.LastStatusCode,
                    null,
                    httpNode.LastResponseHeaders,
                    false,
                    ex.Message,
                    sw.ElapsedMilliseconds,
                    httpNode.LastCurlCommand);

                Debug.WriteLine($"[HttpNode-Stream][{env.ExecutionId}] Error - {ex.Message}");
            }
            finally
            {
                env.OnNodeCompleted?.Invoke(httpNode, sw.Elapsed);
            }
        }

        private static string BuildCurlStreamArgs(
            HttpRequestNode node,
            string url,
            Dictionary<string, string> headers,
            string? bodyInputFilePath,
            string headerOutputPath)
        {
            var sb = new StringBuilder();

            sb.Append("-s");
            sb.Append(" --location");
            sb.Append(" --insecure");
            sb.Append(" --compressed");
            sb.Append($" --max-time {node.TimeoutSeconds}");

            if (!string.IsNullOrWhiteSpace(node.ImpersonateBrowser) && CurlNativeExecutor.IsCurlImpersonate(node.CurlPath))
                sb.Append($" --impersonate {node.ImpersonateBrowser}");

            sb.Append($" -X {node.HttpMethod.ToString().ToUpperInvariant()}");

            foreach (var h in headers)
            {
                var escaped = h.Value.Replace("\"", "\\\"");
                sb.Append($" -H \"{h.Key}: {escaped}\"");
            }

            if (!string.IsNullOrEmpty(bodyInputFilePath) && File.Exists(bodyInputFilePath))
            {
                sb.Append($" --data-binary \"@{bodyInputFilePath.Replace("\"", "\\\"")}\"");
            }

            sb.Append($" -D \"{headerOutputPath.Replace("\"", "\\\"")}\"");

            sb.Append($" \"{url.Replace("\"", "\\\"")}\"");

            return sb.ToString();
        }

        private async Task ExecuteStreamViaCurlExeAsync(
            HttpRequestNode httpNode,
            string url,
            Dictionary<string, string> resolvedHeaders,
            string? resolvedBody,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env,
            Stopwatch sw)
        {
            var service = env.Service;
            Process? process = null;
            string? tempHeaderPath = null;
            string? tempBodyInputPath = null;

            try
            {
                var curlPath = CurlNativeExecutor.FindCurlExe(httpNode.CurlPath);
                if (string.IsNullOrEmpty(curlPath))
                {
                    throw new InvalidOperationException("curl.exe không tìm thấy. Hãy cài curl hoặc chỉ định CurlPath trong settings.");
                }

                tempHeaderPath = Path.Combine(Path.GetTempPath(), $"ac_curl_headers_{Guid.NewGuid():N}.txt");

                if (!string.IsNullOrEmpty(resolvedBody) &&
                    httpNode.HttpMethod != Models.Nodes.HttpMethod.GET &&
                    httpNode.HttpMethod != Models.Nodes.HttpMethod.HEAD)
                {
                    tempBodyInputPath = Path.Combine(Path.GetTempPath(), $"ac_curl_input_{Guid.NewGuid():N}.tmp");
                    await File.WriteAllTextAsync(tempBodyInputPath, resolvedBody, Encoding.UTF8, env.CancellationToken);
                }

                var args = BuildCurlStreamArgs(httpNode, url, resolvedHeaders, tempBodyInputPath, tempHeaderPath);
                Debug.WriteLine($"[CurlExe-Stream] {curlPath} {args.Substring(0, Math.Min(200, args.Length))}...");

                process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = curlPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                process.Start();

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(env.CancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(httpNode.TimeoutSeconds + 30));

                var reader = process.StandardOutput;
                int chunkIndex = 0;
                var responseHeaders = new Dictionary<string, string>();
                var statusCode = 200;

                while (!reader.EndOfStream && !timeoutCts.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(timeoutCts.Token);
                    if (line == null) continue;

                    chunkIndex++;
                    var streamExecutionId = $"{env.ExecutionId}:stream-{chunkIndex}";

                    if (chunkIndex == 1)
                    {
                        try
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                if (File.Exists(tempHeaderPath) && new FileInfo(tempHeaderPath).Length > 0)
                                    break;
                                await Task.Delay(100, timeoutCts.Token);
                            }

                            if (File.Exists(tempHeaderPath))
                            {
                                var headerBlock = await File.ReadAllTextAsync(tempHeaderPath, Encoding.UTF8, timeoutCts.Token);
                                responseHeaders = CurlNativeExecutor.ParseHeaderBlock(headerBlock);
                                statusCode = CurlNativeExecutor.TryGetStatusCodeFromHeaderBlock(headerBlock);
                                if (statusCode == 0) statusCode = 200;
                            }
                        }
                        catch (Exception exHeaders)
                        {
                            Debug.WriteLine($"[CurlExe-Stream] Error reading headers: {exHeaders.Message}");
                        }
                    }

                    // Publish chunk outputs to this streamExecutionId
                    PublishScopedHttpOutputs(
                        streamExecutionId,
                        service,
                        httpNode,
                        statusCode,
                        line,
                        responseHeaders,
                        statusCode >= 200 && statusCode < 300,
                        null,
                        sw.ElapsedMilliseconds,
                        null);

                    // Traverse downstream for this chunk asynchronously without blocking the stream reader
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await TraverseDownstreamForStreamAsync(httpNode, env, streamExecutionId, chunkIndex);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[HttpNode-Stream] Downstream error: {ex.Message}");
                        }
                    });
                }

                await process.WaitForExitAsync(timeoutCts.Token);
                sw.Stop();

                if (File.Exists(tempHeaderPath))
                {
                    try
                    {
                        var headerBlock = await File.ReadAllTextAsync(tempHeaderPath, Encoding.UTF8, timeoutCts.Token);
                        responseHeaders = CurlNativeExecutor.ParseHeaderBlock(headerBlock);
                        statusCode = CurlNativeExecutor.TryGetStatusCodeFromHeaderBlock(headerBlock);
                        if (statusCode == 0) statusCode = 200;
                    }
                    catch { }
                }

                httpNode.LastStatusCode = statusCode;
                httpNode.LastResponseHeaders = responseHeaders;
                httpNode.LastIsSuccess = statusCode >= 200 && statusCode < 300;
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                httpNode.LastErrorMessage = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sw.Stop();
                httpNode.LastIsSuccess = false;
                httpNode.LastErrorMessage = ex.Message;
                httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;

                PublishScopedHttpOutputs(
                    env.ExecutionId,
                    service,
                    httpNode,
                    httpNode.LastStatusCode,
                    null,
                    httpNode.LastResponseHeaders,
                    false,
                    ex.Message,
                    sw.ElapsedMilliseconds,
                    null);

                Debug.WriteLine($"[CurlExe-Stream][{env.ExecutionId}] Error - {ex.Message}");
            }
            finally
            {
                CurlNativeExecutor.KillProcessTreeSafe(process);
                CurlNativeExecutor.TryDeleteFile(tempHeaderPath);
                CurlNativeExecutor.TryDeleteFile(tempBodyInputPath);
                env.OnNodeCompleted?.Invoke(httpNode, sw.Elapsed);
            }
        }

        private static async Task TraverseDownstreamForStreamAsync(
            HttpRequestNode httpNode,
            NodeExecutionEnvironment env,
            string streamExecutionId,
            int chunkIndex)
        {
            var connections = env.Connections;
            var service = env.Service;

            // Find output ports & connections
            var outputPort = httpNode.Ports?.FirstOrDefault(p => !p.IsInput && p.IsVisible);
            var baseNextConnections = outputPort != null
                ? service.GetConnectionsFromPort(outputPort, httpNode, connections).ToList()
                : new List<WorkflowConnection>();

            var legacyNext = connections
                .Where(c => c.FromNode == httpNode && c.FromPort == null)
                .ToList();

            var allNext = baseNextConnections.Concat(legacyNext).ToList();

            var tasks = new List<Task>();
            foreach (var conn in allNext)
            {
                if (conn.ToNode == null) continue;

                if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                {
                    service.SignalLoopBodyReturn(conn, streamExecutionId, $"{env.BranchId}:stream-{chunkIndex}");
                    continue;
                }

                // Run downstream path for this chunk
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await service.ExecuteNodeAsync(
                            conn.ToNode,
                            connections,
                            env.CancellationToken,
                            env.OnEnteringNode,
                            env.OnNodeStarted,
                            env.OnNodeCompleted,
                            env.OnNodeFailed,
                            conn,
                            env.ReachableToEnd,
                            false,
                            new List<string>(env.ExecutionPath) { httpNode.Id },
                            streamExecutionId,
                            env.FlowScopeId,
                            $"{env.BranchId}:stream-{chunkIndex}",
                            env.ParentFlowScopeId);
                    }
                    catch (Exception ex)
                    {
                        env.OnNodeFailed?.Invoke(conn.ToNode, $"Stream execution error: {ex.Message}");
                    }
                }));
            }

            try
            {
                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
            finally
            {
                service.ClearScopedOutputsForRun(streamExecutionId);
            }
        }

        private static void PublishScopedHttpOutputsDeprecated(
            NodeExecutionEnvironment env,
            HttpRequestNode node,
            int? statusCode,
            string? responseBody,
            Dictionary<string, string>? responseHeaders,
            bool? isSuccess,
            string? errorMessage,
            long? responseTimeMs,
            string? curlCommand)
        {
            if (env == null || env.Service == null || string.IsNullOrWhiteSpace(env.ExecutionId) || node == null) return;
            var service = env.Service;
            // Always publish all known keys into scoped store for this execution.
            // This prevents fallback to shared node.Last* values from another parallel branch.
            service.SetScopedNodeStringOutput(env.ExecutionId, node.Id, "statuscode",
                statusCode.HasValue ? statusCode.Value.ToString() : string.Empty);
            service.SetScopedNodeStringOutput(env.ExecutionId, node.Id, "responsebody",
                responseBody ?? string.Empty);
            service.SetScopedNodeStringOutput(env.ExecutionId, node.Id, "responseheaders",
                responseHeaders != null && responseHeaders.Count > 0
                    ? JsonSerializer.Serialize(responseHeaders)
                    : string.Empty);
            service.SetScopedNodeStringOutput(env.ExecutionId, node.Id, "issuccess",
                isSuccess.HasValue ? isSuccess.Value.ToString() : string.Empty);
            service.SetScopedNodeStringOutput(env.ExecutionId, node.Id, "errormessage",
                errorMessage ?? string.Empty);
            service.SetScopedNodeStringOutput(env.ExecutionId, node.Id, "responsetimems",
                responseTimeMs.HasValue ? responseTimeMs.Value.ToString() : string.Empty);
            service.SetScopedNodeStringOutput(env.ExecutionId, node.Id, "curl",
                curlCommand ?? string.Empty);
        }

        private static bool LooksLikeAntiBotResponse(string? responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return false;
            }

            if (responseBody.IndexOf("anti-bot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (!doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    return false;
                }

                var hasAntiBotText = false;
                if (errorElement.TryGetProperty("message", out var messageElement))
                {
                    var message = messageElement.GetString();
                    hasAntiBotText = !string.IsNullOrWhiteSpace(message) &&
                                     message.IndexOf("anti-bot", StringComparison.OrdinalIgnoreCase) >= 0;
                }

                var hasCode7 = errorElement.TryGetProperty("code", out var codeElement) &&
                               codeElement.ValueKind == JsonValueKind.Number &&
                               codeElement.TryGetInt32(out var code) &&
                               code == 7;

                return hasAntiBotText || hasCode7;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
