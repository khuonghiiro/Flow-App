// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
            bool isParallel = WorkflowExecutionService.IsParallelScopedRun(env.ExecutionId);

            // Reset previous results — only for non-parallel runs.
            // In parallel mode, each thread writes to scoped store via PublishScopedHttpOutputs,
            // so mutating shared node.Last* causes cross-thread data corruption.
            if (!isParallel)
            {
                httpNode.LastStatusCode = null;
                httpNode.LastResponseBody = null;
                httpNode.LastResponseHeaders = null;
                httpNode.LastIsSuccess = null;
                httpNode.LastErrorMessage = null;
                httpNode.LastResponseTimeMs = null;
            }

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
                FlowMy.Utils.CurlParseResult? parsedCurlResult = null;
                bool isBoundFromCurlSource = !string.IsNullOrWhiteSpace(httpNode.CurlSourceNodeId) && 
                                             !string.IsNullOrWhiteSpace(httpNode.CurlSourceOutputKey);

                // 1. Priority 1: Check if cURL command is bound from another node
                if (isBoundFromCurlSource)
                {
                    var curlCommand = await ResolveStringValueAsync(
                        "", 
                        httpNode.CurlSourceNodeId, 
                        httpNode.CurlSourceOutputKey, 
                        connections, 
                        httpNode,
                        env,
                        allNodesForLookup);
                    
                    if (string.IsNullOrWhiteSpace(curlCommand))
                    {
                        throw new InvalidOperationException("Dynamic cURL binding is configured but value could not be resolved or is empty after waiting. Cannot fallback to static cURL.");
                    }

                    // Replace dynamic variable placeholders {item}, {token}, {variable} in curlCommand if present
                    curlCommand = ReplaceVariablePlaceholdersInText(curlCommand, connections, httpNode, env);
                    curlCommand = NormalizeBoundCurlCommand(curlCommand);
                    
                    if (!string.IsNullOrWhiteSpace(curlCommand) && IsCurlCommand(curlCommand))
                    {
                        boundRawCurlCommand = curlCommand;
                        
                        // Check if the command is a stream request (thread-local flag only)
                        var isStreamCurl = curlCommand.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);
                        
                        if (TryParseCurlForExecution(curlCommand, out var parsedCurl, out var pHeaders, out var pQueryParams, out var pFormData, out string errorMsg))
                        {
                            parsedCurlResult = parsedCurl;
                            if (isStreamCurl)
                            {
                                // Only set shared node flag for non-parallel (single-node UI test)
                                if (!isParallel) httpNode.IsStream = true;
                                boundRawCurlCommand = null;
                            }

                            // Populate thread-local snapshots from parsed cURL while preserving configured item-level bindings
                            headersSnapshot = MergeParsedAndConfiguredKeyValues(pHeaders, httpNode.Headers);
                            queryParamsSnapshot = MergeParsedAndConfiguredKeyValues(pQueryParams, httpNode.QueryParams);
                            formDataSnapshot = MergeParsedAndConfiguredKeyValues(pFormData, httpNode.FormData);

                            // Apply to shared node object only when not running in parallel dispatch (UI single-node test)
                            if (!isParallel)
                            {
                                ApplyParsedCurlToNode(httpNode, parsedCurl!);
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Could not parse bound cURL into structured fields: {errorMsg}. Will execute raw bound cURL command.");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Dynamic cURL binding failed: resolved value is not a valid cURL command. Value: {curlCommand}");
                    }
                }

                // Effective execution properties (parsed cURL taking base priority, falling back to static node config)
                var effectiveHttpMethod = parsedCurlResult != null ? parsedCurlResult.Method : httpNode.HttpMethod;
                var effectiveBodyType = parsedCurlResult != null ? parsedCurlResult.BodyType : httpNode.BodyType;
                var effectiveRawBody = parsedCurlResult != null ? (parsedCurlResult.RawBody ?? string.Empty) : httpNode.RawBody;
                var effectiveAuthType = parsedCurlResult != null ? parsedCurlResult.AuthType : httpNode.AuthType;
                var effectiveAuthUsername = parsedCurlResult != null ? parsedCurlResult.AuthUsername : httpNode.AuthUsername;
                var effectiveAuthPassword = parsedCurlResult != null ? parsedCurlResult.AuthPassword : httpNode.AuthPassword;
                var effectiveAuthToken = parsedCurlResult != null ? parsedCurlResult.AuthToken : httpNode.AuthToken;

                // 2. Priority 2: Resolve URL (dynamic URL binding overrides base parsed/static URL)
                string url;
                if (!string.IsNullOrWhiteSpace(httpNode.UrlSourceNodeId) && !string.IsNullOrWhiteSpace(httpNode.UrlSourceOutputKey))
                {
                    var dynamicUrl = await ResolveStringValueAsync(
                        httpNode.Url,
                        httpNode.UrlSourceNodeId,
                        httpNode.UrlSourceOutputKey,
                        connections,
                        httpNode,
                        env,
                        allNodesForLookup);
                    url = !string.IsNullOrWhiteSpace(dynamicUrl) ? dynamicUrl : (parsedCurlResult?.Url ?? httpNode.Url);
                }
                else if (parsedCurlResult != null && !string.IsNullOrWhiteSpace(parsedCurlResult.Url))
                {
                    url = parsedCurlResult.Url;
                }
                else
                {
                    url = await ResolveStringValueAsync(
                        httpNode.Url,
                        httpNode.UrlSourceNodeId,
                        httpNode.UrlSourceOutputKey,
                        connections,
                        httpNode,
                        env,
                        allNodesForLookup);
                }

                if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(boundRawCurlCommand))
                {
                    throw new InvalidOperationException("URL is empty or could not be resolved");
                }

                // Build query parameters (resolving any individual query parameter bindings)
                var queryParams = new List<KeyValuePair<string, string>>();
                foreach (var param in queryParamsSnapshot.Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key)))
                {
                    var value = await ResolveKeyValuePairValueAsync(param, connections, httpNode, env, allNodesForLookup);
                    queryParams.Add(new KeyValuePair<string, string>(param.Key, value));
                }

                // Add API Key as query param if configured (resolve value from binding when set)
                if (effectiveAuthType == HttpAuthType.ApiKey && !httpNode.ApiKeyInHeader && !string.IsNullOrWhiteSpace(httpNode.ApiKeyName))
                {
                    var resolvedApiKeyValue = await ResolveStringValueAsync(
                        httpNode.ApiKeyValue ?? string.Empty,
                        httpNode.ApiKeyValueSourceNodeId,
                        httpNode.ApiKeyValueSourceOutputKey,
                        connections,
                        httpNode,
                        env,
                        allNodesForLookup);
                    queryParams.Add(new KeyValuePair<string, string>(httpNode.ApiKeyName, resolvedApiKeyValue));
                }

                // Append query params to URL
                if (queryParams.Count > 0 && !string.IsNullOrWhiteSpace(url))
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

                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Executing {effectiveHttpMethod} {url}");

                // Resolve headers and body using active effective properties and snapshots
                var headersAndBody = await ResolveHeadersAndBodyAsync(
                    httpNode,
                    connections,
                    env,
                    headersSnapshot,
                    formDataSnapshot,
                    effectiveHttpMethod,
                    effectiveBodyType,
                    effectiveRawBody,
                    effectiveAuthType,
                    effectiveAuthUsername,
                    effectiveAuthPassword,
                    effectiveAuthToken,
                    allNodesForLookup);
                var resolvedHeaders = headersAndBody.resolvedHeaders;
                var resolvedBody = headersAndBody.resolvedBody;

                // Auto-detect stream based on request headers (thread-local decision)
                var isStream = httpNode.IsStream;
                if (!isStream)
                {
                    if (resolvedHeaders.TryGetValue("Accept", out var acceptHeader) &&
                        acceptHeader.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
                    {
                        isStream = true;
                        if (!isParallel) httpNode.IsStream = true;
                    }
                }
                // For stream cURL detected above
                if (parsedCurlResult != null && boundRawCurlCommand == null && 
                    !isStream && httpNode.IsStream)
                {
                    isStream = true;
                }

                bool effectiveUseCurl = httpNode.UseCurl || boundRawCurlCommand != null || parsedCurlResult != null;

                // ⚡ IMPORTANT: Sub-methods (ExecuteViaCurlAsync, ExecuteRawCurlCommandAsync, ExecuteStream*)
                // have their own finally blocks that call OnNodeCompleted + TraverseOutputsAsync.
                // So when delegating to them, we must NOT call those again at the bottom of this method.
                if (isStream)
                {
                    if (effectiveUseCurl)
                    {
                        await ExecuteStreamViaCurlExeAsync(httpNode, url, resolvedHeaders, resolvedBody, connections, env, sw);
                    }
                    else
                    {
                        await ExecuteStreamViaHttpClientAsync(httpNode, url, resolvedHeaders, resolvedBody, connections, env, sw);
                    }
                    // Sub-methods already called OnNodeCompleted + TraverseOutputsAsync
                    return;
                }

                if (effectiveUseCurl)
                {
                    // Prefer structured execution via ExecuteViaCurlAsync whenever parsed parameters or URL are available,
                    // avoiding Windows cmd.exe single-quote header corruption.
                    // Only fall back to ExecuteRawCurlCommandAsync if structured parsing failed completely.
                    if (parsedCurlResult == null && !string.IsNullOrWhiteSpace(boundRawCurlCommand))
                    {
                        await ExecuteRawCurlCommandAsync(httpNode, boundRawCurlCommand, connections, env, sw);
                        // Sub-method already called OnNodeCompleted + TraverseOutputsAsync
                        return;
                    }

                    await ExecuteViaCurlAsync(httpNode, url, resolvedHeaders, resolvedBody, connections, env, sw);
                    // Sub-method already called OnNodeCompleted + TraverseOutputsAsync
                    return;
                }

                // Create HTTP request
                var method = GetHttpMethod(effectiveHttpMethod);
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
                    if (!isParallel) httpNode.IsStream = true;
                }

                if (isStreamResponse)
                {
                    await ProcessStreamResponseAsync(httpNode, response, resolvedHeaders, connections, env, sw, cts.Token);
                    // ProcessStreamResponseAsync calls OnNodeCompleted + TraverseOutputsAsync
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
                    // ExecuteViaCurlAsync calls OnNodeCompleted + TraverseOutputsAsync
                    return;
                }

                // Generate cURL command with resolved values
                var curlCmd = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);

                // Set results on node (safe for single-threaded path, scoped store handles parallel)
                if (!isParallel)
                {
                    httpNode.LastCurlCommand = curlCmd;
                    httpNode.LastStatusCode = (int)response.StatusCode;
                    httpNode.LastResponseBody = responseBody;
                    httpNode.LastResponseHeaders = responseHeaders;
                    httpNode.LastIsSuccess = response.IsSuccessStatusCode;
                    httpNode.LastErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                    httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                }
                PublishScopedHttpOutputs(
                    env, httpNode,
                    (int)response.StatusCode,
                    responseBody,
                    responseHeaders,
                    response.IsSuccessStatusCode,
                    response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    sw.ElapsedMilliseconds,
                    curlCmd);

                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Response {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Response body length: {responseBody?.Length ?? 0}");
            }
            // Timeout / lỗi kết nối HTTP: đánh dấu lỗi nhưng KHÔNG dừng toàn bộ workflow
            catch (TaskCanceledException ex) when (ex.CancellationToken != env.CancellationToken)
            {
                sw.Stop();
                var curlCmd = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);
                var errMsg = $"Request timeout after {httpNode.TimeoutSeconds} seconds";
                if (!isParallel)
                {
                    httpNode.LastCurlCommand = curlCmd;
                    httpNode.LastIsSuccess = false;
                    httpNode.LastErrorMessage = errMsg;
                    httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                }
                PublishScopedHttpOutputs(
                    env, httpNode,
                    null, null, null,
                    false, errMsg,
                    sw.ElapsedMilliseconds, curlCmd);
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Timeout - {errMsg}");
            }
            // Các lỗi runtime khác (trừ khi là huỷ thủ công) cũng chỉ set lỗi và cho phép workflow đi tiếp
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not TaskCanceledException)
            {
                sw.Stop();
                var curlCmd = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);
                if (!isParallel)
                {
                    httpNode.LastCurlCommand = curlCmd;
                    httpNode.LastIsSuccess = false;
                    httpNode.LastErrorMessage = ex.Message;
                    httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                }
                PublishScopedHttpOutputs(
                    env, httpNode,
                    null, null, null,
                    false, ex.Message,
                    sw.ElapsedMilliseconds, curlCmd);
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Error - {ex.Message}");
            }

            // Only call OnNodeCompleted + TraverseOutputsAsync for the HttpClient path.
            // Sub-methods (ExecuteViaCurlAsync, ExecuteRawCurlCommandAsync, ExecuteStream*) 
            // already call these in their own finally blocks and return before reaching here.
            env.OnNodeCompleted?.Invoke(httpNode, sw.Elapsed);
            await env.TraverseOutputsAsync(httpNode);
        }

        private static string CleanHeaderOrTokenValue(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var trimmed = input.Trim();
            if ((trimmed.StartsWith("'") && trimmed.EndsWith("'")) ||
                (trimmed.StartsWith("\"") && trimmed.EndsWith("\"")))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }
            return trimmed;
        }

        private async Task<(Dictionary<string, string> resolvedHeaders, string? resolvedBody)> ResolveHeadersAndBodyAsync(
            HttpRequestNode httpNode,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env,
            List<HttpKeyValuePair> headersSnapshot,
            List<HttpKeyValuePair> formDataSnapshot,
            HttpMethod effectiveHttpMethod,
            HttpBodyType effectiveBodyType,
            string? effectiveRawBody,
            HttpAuthType effectiveAuthType,
            string? effectiveAuthUsername,
            string? effectiveAuthPassword,
            string? effectiveAuthToken,
            IEnumerable<WorkflowNode>? allNodesForLookup = null)
        {
            var resolvedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1. Resolve headers from the active headers snapshot (dynamic or cURL-parsed)
            foreach (var header in headersSnapshot.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key)))
            {
                var val = await ResolveKeyValuePairValueAsync(header, connections, httpNode, env, allNodesForLookup);
                resolvedHeaders[header.Key] = CleanHeaderOrTokenValue(val);
            }

            // 2. Add auth headers based on effectiveAuthType
            switch (effectiveAuthType)
            {
                case HttpAuthType.Basic:
                    if (!string.IsNullOrWhiteSpace(effectiveAuthUsername) && !resolvedHeaders.ContainsKey("Authorization"))
                    {
                        var u = CleanHeaderOrTokenValue(effectiveAuthUsername);
                        var p = CleanHeaderOrTokenValue(effectiveAuthPassword);
                        var credentials = Convert.ToBase64String(
                            Encoding.UTF8.GetBytes($"{u}:{p}"));
                        resolvedHeaders["Authorization"] = $"Basic {credentials}";
                    }
                    break;
                case HttpAuthType.Bearer:
                    var rawToken = await ResolveStringValueAsync(effectiveAuthToken ?? string.Empty, httpNode.TokenSourceNodeId, httpNode.TokenSourceOutputKey, connections, httpNode, env, allNodesForLookup);
                    var token = CleanHeaderOrTokenValue(rawToken);
                    if (!string.IsNullOrWhiteSpace(token) && !resolvedHeaders.ContainsKey("Authorization"))
                    {
                        resolvedHeaders["Authorization"] = $"Bearer {token}";
                    }
                    break;
                case HttpAuthType.ApiKey:
                    if (httpNode.ApiKeyInHeader && !string.IsNullOrWhiteSpace(httpNode.ApiKeyName))
                    {
                        var apiKeyVal = await ResolveStringValueAsync(httpNode.ApiKeyValue ?? string.Empty, httpNode.ApiKeyValueSourceNodeId, httpNode.ApiKeyValueSourceOutputKey, connections, httpNode, env, allNodesForLookup);
                        resolvedHeaders[httpNode.ApiKeyName] = CleanHeaderOrTokenValue(apiKeyVal);
                    }
                    break;
            }

            // 3. Clean quotes from existing Authorization header if present
            if (resolvedHeaders.TryGetValue("Authorization", out var authHeaderVal))
            {
                var cleanedAuth = CleanHeaderOrTokenValue(authHeaderVal);
                if (cleanedAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var tokenPart = CleanHeaderOrTokenValue(cleanedAuth.Substring(7));
                    resolvedHeaders["Authorization"] = $"Bearer {tokenPart}";
                }
                else
                {
                    resolvedHeaders["Authorization"] = cleanedAuth;
                }
            }

            // 4. Default User-Agent if missing
            if (!resolvedHeaders.ContainsKey("User-Agent"))
            {
                resolvedHeaders["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
            }

            // 5. Resolve body based on effectiveHttpMethod and effectiveBodyType
            string? resolvedBody = null;
            if (effectiveHttpMethod != HttpMethod.GET && effectiveHttpMethod != HttpMethod.HEAD)
            {
                switch (effectiveBodyType)
                {
                    case HttpBodyType.Raw:
                        resolvedBody = await ResolveStringValueAsync(effectiveRawBody ?? string.Empty, httpNode.BodySourceNodeId, httpNode.BodySourceOutputKey, connections, httpNode, env, allNodesForLookup);
                        break;
                    case HttpBodyType.Json:
                        var jsonBody = await ResolveStringValueAsync(effectiveRawBody ?? string.Empty, httpNode.BodySourceNodeId, httpNode.BodySourceOutputKey, connections, httpNode, env, allNodesForLookup);
                        resolvedBody = EscapeJsonStringValues(jsonBody, connections, httpNode, env);
                        if (!resolvedHeaders.ContainsKey("Content-Type"))
                            resolvedHeaders["Content-Type"] = "application/json";
                        break;
                    case HttpBodyType.FormUrlEncoded:
                    case HttpBodyType.FormData:
                        var formParts = new List<string>();
                        foreach (var item in formDataSnapshot.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Key)))
                        {
                            var resolvedVal = await ResolveKeyValuePairValueAsync(item, connections, httpNode, env, allNodesForLookup);
                            formParts.Add($"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(resolvedVal)}");
                        }
                        resolvedBody = string.Join("&", formParts);
                        if (!resolvedHeaders.ContainsKey("Content-Type"))
                        {
                            resolvedHeaders["Content-Type"] = effectiveBodyType == HttpBodyType.FormUrlEncoded
                                ? "application/x-www-form-urlencoded"
                                : "multipart/form-data";
                        }
                        break;
                }
            }

            return (resolvedHeaders, resolvedBody);
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
            bool isParallel = WorkflowExecutionService.IsParallelScopedRun(env.ExecutionId);
            try
            {
                Debug.WriteLine($"[HttpNode-Curl] {httpNode.HttpMethod} {url}, Headers={resolvedHeaders.Count}, UseCurl=true");

                var curlResult = await CurlNativeExecutor.ExecuteAsync(httpNode, url, resolvedHeaders, resolvedBody, env.CancellationToken);
                sw.Stop();

                var curlCmd = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);
                var statusCode = curlResult.StatusCode;
                var body = curlResult.Body;
                var headers = curlResult.Headers.Count > 0 ? curlResult.Headers : new Dictionary<string, string>();
                var isSuccess = curlResult.IsSuccess;
                var errorMsg = curlResult.IsSuccess ? null : curlResult.ErrorMessage;
                var timeMs = curlResult.ElapsedMs > 0 ? curlResult.ElapsedMs : sw.ElapsedMilliseconds;

                if (!isParallel)
                {
                    httpNode.LastCurlCommand = curlCmd;
                    httpNode.LastStatusCode = statusCode;
                    httpNode.LastResponseBody = body;
                    httpNode.LastResponseHeaders = headers;
                    httpNode.LastIsSuccess = isSuccess;
                    httpNode.LastErrorMessage = errorMsg;
                    httpNode.LastResponseTimeMs = timeMs;
                }
                PublishScopedHttpOutputs(env, httpNode, statusCode, body, headers, isSuccess, errorMsg, timeMs, curlCmd);

                Debug.WriteLine($"[HttpNode-Curl] Backend={curlResult.Backend}, Status={statusCode}, Time={timeMs}ms");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sw.Stop();
                var curlCmd = HttpRequestCurlGenerator.GenerateCurlCommand(httpNode, connections);
                if (!isParallel)
                {
                    httpNode.LastCurlCommand = curlCmd;
                    httpNode.LastIsSuccess = false;
                    httpNode.LastErrorMessage = ex.Message;
                    httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                }
                PublishScopedHttpOutputs(env, httpNode, null, null, null, false, ex.Message, sw.ElapsedMilliseconds, curlCmd);
                Debug.WriteLine($"[HttpNode-Curl] Error: {ex.Message}");
            }
            finally
            {
                env.OnNodeCompleted?.Invoke(httpNode, sw.Elapsed);
                await env.TraverseOutputsAsync(httpNode);
            }
        }

        /// <summary>
        /// Resolve auth thành headers dict cho curl bypass mode.

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
        /// Resolve giá trị của một variable từ connections hoặc node outputs trong env.
        /// </summary>
        private string? ResolveVariableValue(
            string variableKey,
            List<WorkflowConnection> connections,
            HttpRequestNode currentNode,
            NodeExecutionEnvironment env)
        {
            // Search in ReachableToEnd first (includes AsyncTaskNode, InputNode, CodeNode, etc.)
            if (env?.ReachableToEnd != null)
            {
                foreach (var node in env.ReachableToEnd)
                {
                    if (node == currentNode) continue;
                    var val = env.Service.ResolveDynamicValueForExecution(node, variableKey, env);
                    if (val != null && val != "—" && !string.IsNullOrWhiteSpace(val))
                    {
                        return val;
                    }
                }
            }

            // Fallback: search all nodes connected in graph
            var upstreamNodes = connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .Where(n => n != null && n != currentNode)
                .Distinct()
                .ToList();

            foreach (var node in upstreamNodes)
            {
                var value = env.Service.ResolveDynamicValueForExecution(node!, variableKey, env);
                if (value != null && value != "—" && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private string ReplaceVariablePlaceholdersInText(
            string text,
            List<WorkflowConnection> connections,
            HttpRequestNode currentNode,
            NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.Contains('{'))
                return text;

            var pattern = @"\{([^{}]+)\}";
            return System.Text.RegularExpressions.Regex.Replace(text, pattern, match =>
            {
                var variableKey = match.Groups[1].Value.Trim();
                var value = ResolveVariableValue(variableKey, connections, currentNode, env);
                if (value != null && value != "—")
                {
                    return value;
                }
                return match.Value;
            });
        }

        private static List<HttpKeyValuePair> MergeParsedAndConfiguredKeyValues(
            List<HttpKeyValuePair> parsedItems,
            IEnumerable<HttpKeyValuePair> configuredItems)
        {
            if (configuredItems == null || !configuredItems.Any()) return parsedItems;
            var list = new List<HttpKeyValuePair>();
            foreach (var p in parsedItems)
            {
                // Try to match configured item by Key to preserve SourceNodeId and SourceOutputKey
                var matched = configuredItems.FirstOrDefault(c => string.Equals(c.Key, p.Key, StringComparison.OrdinalIgnoreCase));
                if (matched != null && (!string.IsNullOrWhiteSpace(matched.SourceNodeId) || !string.IsNullOrWhiteSpace(matched.SourceOutputKey)))
                {
                    list.Add(new HttpKeyValuePair
                    {
                        Key = p.Key,
                        Value = p.Value,
                        IsEnabled = p.IsEnabled,
                        SourceNodeId = matched.SourceNodeId,
                        SourceOutputKey = matched.SourceOutputKey
                    });
                }
                else
                {
                    list.Add(p);
                }
            }
            return list;
        }

        /// <summary>
        /// Resolve a string value that may have dynamic binding from another node.
        /// </summary>
        private async Task<string> ResolveStringValueAsync(
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
            var sourceNode = FindSourceNode(sourceNodeId, connections, currentNode, allNodesForLookup ?? env?.ReachableToEnd);
            if (sourceNode == null)
            {
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Source node {sourceNodeId} not found");
                return staticValue ?? string.Empty;
            }

            // Wait/retry logic for async parallel tasks where the source node might still be running
            string value = "—";
            int retryCount = 0;
            while (retryCount < 100) // Wait up to 10 seconds
            {
                value = env.Service.ResolveDynamicValueForExecution(sourceNode, sourceOutputKey, env);
                if (value != "—")
                {
                    break;
                }
                await Task.Delay(100, env.CancellationToken);
                retryCount++;
            }

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

        private async Task<string> ResolveKeyValuePairValueAsync(
            HttpKeyValuePair kvp,
            List<WorkflowConnection> connections,
            HttpRequestNode currentNode,
            NodeExecutionEnvironment env,
            IEnumerable<WorkflowNode>? allNodesForLookup = null)
        {
            // If no dynamic binding, return static value
            if (string.IsNullOrWhiteSpace(kvp.SourceNodeId) || string.IsNullOrWhiteSpace(kvp.SourceOutputKey))
                return kvp.Value ?? string.Empty;

            // Find source node
            var sourceNode = FindSourceNode(kvp.SourceNodeId, connections, currentNode, allNodesForLookup ?? env?.ReachableToEnd);
            if (sourceNode == null)
            {
                Debug.WriteLine($"[HttpNode][{env.ExecutionId}] Source node {kvp.SourceNodeId} not found for '{kvp.Key}', falling back to static value");
                return kvp.Value ?? string.Empty;
            }

            // Wait/retry logic for async parallel tasks
            string value = "—";
            int retryCount = 0;
            while (retryCount < 100) // Wait up to 10 seconds
            {
                value = env.Service.ResolveDynamicValueForExecution(sourceNode, kvp.SourceOutputKey, env);
                if (value != "—")
                {
                    break;
                }
                await Task.Delay(100, env.CancellationToken);
                retryCount++;
            }

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

        private static bool TryParseCurlForExecution(
            string curlCommand,
            out FlowMy.Utils.CurlParseResult? result,
            out List<HttpKeyValuePair> headers,
            out List<HttpKeyValuePair> queryParams,
            out List<HttpKeyValuePair> formData,
            out string errorMsg)
        {
            headers = new List<HttpKeyValuePair>();
            queryParams = new List<HttpKeyValuePair>();
            formData = new List<HttpKeyValuePair>();
            result = null;

            try
            {
                var parsed = FlowMy.Utils.CurlParser.Parse(curlCommand);
                if (!parsed.IsValid || string.IsNullOrWhiteSpace(parsed.Url))
                {
                    errorMsg = parsed.ErrorMessage ?? "Parsed cURL has no URL";
                    return false;
                }

                result = parsed;
                if (parsed.Headers != null)
                {
                    foreach (var h in parsed.Headers)
                        headers.Add(new HttpKeyValuePair { Key = h.Key, Value = h.Value, IsEnabled = h.IsEnabled });
                }
                if (parsed.QueryParams != null)
                {
                    foreach (var p in parsed.QueryParams)
                        queryParams.Add(new HttpKeyValuePair { Key = p.Key, Value = p.Value, IsEnabled = p.IsEnabled });
                }
                if (parsed.FormData != null)
                {
                    foreach (var f in parsed.FormData)
                        formData.Add(new HttpKeyValuePair { Key = f.Key, Value = f.Value, IsEnabled = f.IsEnabled });
                }

                errorMsg = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        private static void ApplyParsedCurlToNode(HttpRequestNode node, FlowMy.Utils.CurlParseResult result)
        {
            lock (node)
            {
                node.Url = result.Url;
                node.HttpMethod = result.Method;

                if (result.Headers != null && result.Headers.Count > 0)
                {
                    node.Headers.Clear();
                    foreach (var h in result.Headers)
                        node.Headers.Add(new HttpKeyValuePair { Key = h.Key, Value = h.Value, IsEnabled = h.IsEnabled });
                }

                if (result.QueryParams != null && result.QueryParams.Count > 0)
                {
                    node.QueryParams.Clear();
                    foreach (var p in result.QueryParams)
                        node.QueryParams.Add(new HttpKeyValuePair { Key = p.Key, Value = p.Value, IsEnabled = p.IsEnabled });
                }

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

                node.BodyType = result.BodyType;
                if (result.BodyType == HttpBodyType.Raw || result.BodyType == HttpBodyType.Json)
                {
                    node.RawBody = result.RawBody ?? string.Empty;
                }
                else if (result.BodyType == HttpBodyType.FormData || result.BodyType == HttpBodyType.FormUrlEncoded)
                {
                    node.FormData.Clear();
                    foreach (var f in result.FormData)
                        node.FormData.Add(new HttpKeyValuePair { Key = f.Key, Value = f.Value, IsEnabled = f.IsEnabled });
                }
            }
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
            bool isParallel = WorkflowExecutionService.IsParallelScopedRun(env.ExecutionId);
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

                var statusCode = curlResult.StatusCode;
                var body = curlResult.Body;
                var headers = curlResult.Headers.Count > 0 ? curlResult.Headers : new Dictionary<string, string>();
                var isSuccess = curlResult.IsSuccess;
                var errorMsg = curlResult.IsSuccess ? null : curlResult.ErrorMessage;
                var timeMs = curlResult.ElapsedMs > 0 ? curlResult.ElapsedMs : sw.ElapsedMilliseconds;

                if (!isParallel)
                {
                    httpNode.LastCurlCommand = rawCurlCommand;
                    httpNode.LastStatusCode = statusCode;
                    httpNode.LastResponseBody = body;
                    httpNode.LastResponseHeaders = headers;
                    httpNode.LastIsSuccess = isSuccess;
                    httpNode.LastErrorMessage = errorMsg;
                    httpNode.LastResponseTimeMs = timeMs;
                }
                PublishScopedHttpOutputs(env, httpNode, statusCode, body, headers, isSuccess, errorMsg, timeMs, rawCurlCommand);

                Debug.WriteLine($"[HttpNode-CurlRaw] Backend={curlResult.Backend}, Status={statusCode}, Time={timeMs}ms");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sw.Stop();
                if (!isParallel)
                {
                    httpNode.LastCurlCommand = rawCurlCommand;
                    httpNode.LastIsSuccess = false;
                    httpNode.LastErrorMessage = ex.Message;
                    httpNode.LastResponseTimeMs = sw.ElapsedMilliseconds;
                }
                PublishScopedHttpOutputs(env, httpNode, null, null, null, false, ex.Message, sw.ElapsedMilliseconds, rawCurlCommand);
                Debug.WriteLine($"[HttpNode-CurlRaw] Error: {ex.Message}");
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
