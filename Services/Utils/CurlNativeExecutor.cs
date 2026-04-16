using FlowMy.Models.Nodes;
using CurlThin;
using CurlThin.Enums;
using CurlThin.SafeHandles;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using static CurlThin.CurlNative.Easy;

namespace FlowMy.Services.Utils
{
    /// <summary>
    /// Kết quả trả về từ curl request (native hay subprocess).
    /// </summary>
    public class CurlResult
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string Body { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public long ElapsedMs { get; set; }
        /// <summary>Backend đã dùng: "CurlThin (libcurl native)" hay "curl.exe subprocess".</summary>
        public string Backend { get; set; } = "unknown";
    }

    /// <summary>
    /// Executor bypass anti-bot dùng 2 backend:
    /// 1. CurlThin (P/Invoke libcurl.dll) - primary
    /// 2. curl.exe subprocess - fallback tự động
    /// </summary>
    public static class CurlNativeExecutor
    {
        // ⚠️ CurlThin yêu cầu Init() global một lần
        private static bool _curlThinInitialized = false;
        private static bool _curlThinAvailable = false;
        private static readonly object _initLock = new();

        static CurlNativeExecutor()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        // -------------------------------------------------------
        // PUBLIC ENTRY POINT
        // -------------------------------------------------------

        public static async Task<CurlResult> ExecuteAsync(
            HttpRequestNode node,
            string resolvedUrl,
            Dictionary<string, string> resolvedHeaders,
            string? resolvedBody,
            CancellationToken ct = default)
        {
            // Thử CurlThin P/Invoke trước
            if (TryInitCurlThin())
            {
                try
                {
                    var result = await ExecuteViaCurlThinAsync(node, resolvedUrl, resolvedHeaders, resolvedBody, ct);
                    result.Backend = "CurlThin (libcurl native)";

                    // Some anti-bot providers detect CurlThin/libcurl fingerprint.
                    // If blocked, retry with curl.exe to better match real cURL behavior.
                    if (!LooksLikeAntiBotBlocked(result))
                    {
                        return result;
                    }

                    Debug.WriteLine("[CurlNative] CurlThin response looks anti-bot blocked, retrying via curl.exe");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CurlNative] CurlThin failed → fallback curl.exe: {ex.Message}");
                }
            }

            // Fallback: curl.exe subprocess
            var fallback = await ExecuteViaCurlExeAsync(node, resolvedUrl, resolvedHeaders, resolvedBody, ct);
            fallback.Backend = "curl.exe subprocess";
            return fallback;
        }

        /// <summary>
        /// Execute a raw cURL command as-is (best fidelity with copied cURL from browser/Postman).
        /// Uses cmd.exe + temp .cmd file to preserve caret escaping and multiline continuation.
        /// </summary>
        public static async Task<CurlResult> ExecuteRawCommandAsync(
            HttpRequestNode node,
            string rawCurlCommand,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            var result = new CurlResult();
            Process? process = null;
            string? tempCmdPath = null;
            string? tempHeaderPath = null;
            string? tempBodyPath = null;

            try
            {
                if (string.IsNullOrWhiteSpace(rawCurlCommand))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Raw cURL command is empty.";
                    return result;
                }

                var curlPath = FindCurlExe(node.CurlPath);
                if (string.IsNullOrWhiteSpace(curlPath))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "curl.exe không tìm thấy. Hãy cài curl hoặc chỉ định CurlPath trong node settings.";
                    return result;
                }

                // Ensure response can be parsed into status/body consistently.
                var commandForExec = rawCurlCommand.Trim();
                if (node.AutoAppendCurlWriteOut)
                {
                    tempHeaderPath = Path.Combine(Path.GetTempPath(), $"ac_rawcurl_headers_{Guid.NewGuid():N}.txt");
                    tempBodyPath = Path.Combine(Path.GetTempPath(), $"ac_rawcurl_body_{Guid.NewGuid():N}.bin");
                    commandForExec = EnsureOutputMarkers(commandForExec, tempHeaderPath, tempBodyPath);
                    commandForExec = EscapeCurlWriteOutForCmd(commandForExec);
                }

                tempCmdPath = Path.Combine(Path.GetTempPath(), $"ac_rawcurl_{Guid.NewGuid():N}.cmd");
                await File.WriteAllTextAsync(tempCmdPath, "@echo off\r\n" + commandForExec + "\r\n", Encoding.UTF8, ct);

                try
                {
                    process = new Process();
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/d /s /c \"\"{tempCmdPath}\"\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    process.Start();

                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(node.TimeoutSeconds + 10));

                    var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                    var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

                    await process.WaitForExitAsync(timeoutCts.Token);
                    sw.Stop();

                    var stdoutText = await outputTask;
                    var stderrText = await errorTask;

                    if (!string.IsNullOrWhiteSpace(tempBodyPath))
                    {
                        ParseCurlOutputFromFiles(stdoutText, stderrText, tempHeaderPath, tempBodyPath, result);
                    }
                    else
                    {
                        ParseCurlOutput(stdoutText, stderrText, result);
                    }

                    result.ElapsedMs = sw.ElapsedMilliseconds;
                    result.Backend = "curl.exe raw command";
                }
                finally
                {
                    KillProcessTreeSafe(process);
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(tempCmdPath) && File.Exists(tempCmdPath))
                            File.Delete(tempCmdPath);
                        if (!string.IsNullOrWhiteSpace(tempHeaderPath) && File.Exists(tempHeaderPath))
                            File.Delete(tempHeaderPath);
                        if (!string.IsNullOrWhiteSpace(tempBodyPath) && File.Exists(tempBodyPath))
                            File.Delete(tempBodyPath);
                    }
                    catch
                    {
                        // Best effort cleanup.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Request bị huỷ hoặc timeout.";
                result.ElapsedMs = sw.ElapsedMilliseconds;
                result.Backend = "curl.exe raw command";
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ElapsedMs = sw.ElapsedMilliseconds;
                result.Backend = "curl.exe raw command";
            }

            return result;
        }

        // -------------------------------------------------------
        // BACKEND 1: CurlThin (P/Invoke libcurl native)
        // -------------------------------------------------------

        private static bool TryInitCurlThin()
        {
            lock (_initLock)
            {
                if (_curlThinInitialized) return _curlThinAvailable;
                _curlThinInitialized = true;
                try
                {
                    CurlNative.Init();
                    _curlThinAvailable = true;
                    Debug.WriteLine("[CurlNative] CurlThin initialized OK");
                }
                catch (Exception ex)
                {
                    _curlThinAvailable = false;
                    Debug.WriteLine($"[CurlNative] CurlThin init failed: {ex.Message}");
                }
                return _curlThinAvailable;
            }
        }

        private static Task<CurlResult> ExecuteViaCurlThinAsync(
            HttpRequestNode node,
            string url,
            Dictionary<string, string> headers,
            string? body,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var result = new CurlResult();

                var easy = CurlNative.Easy.Init();
                if (easy.IsInvalid)
                    throw new InvalidOperationException("curl_easy_init() returned NULL");

                IntPtr slist = IntPtr.Zero;

                try
                {
                    // URL
                    CurlNative.Easy.SetOpt(easy, CURLoption.URL, url);

                    // Follow redirects
                    CurlNative.Easy.SetOpt(easy, CURLoption.FOLLOWLOCATION, 1);

                    // Timeout
                    CurlNative.Easy.SetOpt(easy, CURLoption.TIMEOUT, node.TimeoutSeconds);

                    // Bypass SSL cert
                    CurlNative.Easy.SetOpt(easy, CURLoption.SSL_VERIFYPEER, 0);
                    CurlNative.Easy.SetOpt(easy, CURLoption.SSL_VERIFYHOST, 0);

                    // Auto-decompress (gzip, br)
                    CurlNative.Easy.SetOpt(easy, CURLoption.ACCEPT_ENCODING, "");

                    // HTTP Method
                    SetHttpMethodCurlThin(easy, node.HttpMethod, body);

                    // Build headers slist
                    foreach (var h in headers)
                    {
                        slist = SlistAppend(slist, $"{h.Key}: {h.Value}");
                    }
                    if (slist != IntPtr.Zero)
                        CurlNative.Easy.SetOpt(easy, CURLoption.HTTPHEADER, slist);

                    // Body
                    if (!string.IsNullOrEmpty(body) &&
                        node.HttpMethod != Models.Nodes.HttpMethod.GET &&
                        node.HttpMethod != Models.Nodes.HttpMethod.HEAD)
                    {
                        CurlNative.Easy.SetOpt(easy, CURLoption.COPYPOSTFIELDS, body);
                        CurlNative.Easy.SetOpt(easy, CURLoption.POSTFIELDSIZE, Encoding.UTF8.GetByteCount(body));
                    }

                    // Collect response body & headers as raw bytes so we can decode once
                    // with the final charset instead of corrupting split UTF-8 characters.
                    using var responseBodyStream = new MemoryStream();
                    using var responseHeaderStream = new MemoryStream();

                    // DataHandler delegate: UIntPtr(IntPtr data, UIntPtr size, UIntPtr nmemb, IntPtr userdata)
                    DataHandler writeHandler = (data, size, nmemb, _) =>
                    {
                        var length = (int)((ulong)size * (ulong)nmemb);
                        var bytes = new byte[length];
                        Marshal.Copy(data, bytes, 0, length);
                        responseBodyStream.Write(bytes, 0, bytes.Length);
                        return (UIntPtr)((ulong)size * (ulong)nmemb);
                    };

                    DataHandler headerHandler = (data, size, nmemb, _) =>
                    {
                        var length = (int)((ulong)size * (ulong)nmemb);
                        var bytes = new byte[length];
                        Marshal.Copy(data, bytes, 0, length);
                        responseHeaderStream.Write(bytes, 0, bytes.Length);
                        return (UIntPtr)((ulong)size * (ulong)nmemb);
                    };

                    CurlNative.Easy.SetOpt(easy, CURLoption.WRITEFUNCTION, writeHandler);
                    CurlNative.Easy.SetOpt(easy, CURLoption.HEADERFUNCTION, headerHandler);

                    // Execute
                    var code = CurlNative.Easy.Perform(easy);
                    sw.Stop();

                    if (code != CURLcode.OK)
                        throw new Exception($"curl error {code}: {CurlNative.Easy.StrError(code)}");

                    // Get status code
                    CurlNative.Easy.GetInfo(easy, CURLINFO.RESPONSE_CODE, out int httpCode);

                    var responseHeaders = ParseHeaderBlock(DecodeHeaderBytes(responseHeaderStream.ToArray()));

                    result.StatusCode = httpCode;
                    result.Body = DecodeResponseBody(responseBodyStream.ToArray(), responseHeaders);
                    result.Headers = responseHeaders;
                    result.IsSuccess = httpCode is >= 200 and < 300;
                    result.ErrorMessage = result.IsSuccess ? string.Empty : $"HTTP {httpCode}";
                    result.ElapsedMs = sw.ElapsedMilliseconds;

                    return result;
                }
                finally
                {
                    if (slist != IntPtr.Zero)
                        SlistFreeAll(slist);
                    easy.Dispose();
                }
            }, ct);
        }

        private static void SetHttpMethodCurlThin(SafeEasyHandle easy, Models.Nodes.HttpMethod method, string? body)
        {
            switch (method)
            {
                case Models.Nodes.HttpMethod.POST:
                    CurlNative.Easy.SetOpt(easy, CURLoption.POST, 1);
                    break;
                case Models.Nodes.HttpMethod.PUT:
                    CurlNative.Easy.SetOpt(easy, CURLoption.UPLOAD, 1);
                    break;
                case Models.Nodes.HttpMethod.DELETE:
                    CurlNative.Easy.SetOpt(easy, CURLoption.CUSTOMREQUEST, "DELETE");
                    break;
                case Models.Nodes.HttpMethod.PATCH:
                    CurlNative.Easy.SetOpt(easy, CURLoption.CUSTOMREQUEST, "PATCH");
                    break;
                case Models.Nodes.HttpMethod.HEAD:
                    CurlNative.Easy.SetOpt(easy, CURLoption.NOBODY, 1);
                    break;
                case Models.Nodes.HttpMethod.OPTIONS:
                    CurlNative.Easy.SetOpt(easy, CURLoption.CUSTOMREQUEST, "OPTIONS");
                    break;
                    // GET = default, không cần set
            }
        }

        // -------------------------------------------------------
        // BACKEND 2: curl.exe subprocess (fallback)
        // -------------------------------------------------------

        private static async Task<CurlResult> ExecuteViaCurlExeAsync(
            HttpRequestNode node,
            string url,
            Dictionary<string, string> headers,
            string? body,
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var result = new CurlResult();
            Process? process = null;
            string? tempHeaderPath = null;
            string? tempBodyPath = null;

            try
            {
                var curlPath = FindCurlExe(node.CurlPath);
                if (string.IsNullOrEmpty(curlPath))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "curl.exe không tìm thấy. Hãy cài curl hoặc chỉ định CurlPath trong node settings.";
                    return result;
                }

                tempHeaderPath = Path.Combine(Path.GetTempPath(), $"ac_curl_headers_{Guid.NewGuid():N}.txt");
                tempBodyPath = Path.Combine(Path.GetTempPath(), $"ac_curl_body_{Guid.NewGuid():N}.bin");
                var args = BuildCurlArgs(node, url, headers, body, tempHeaderPath, tempBodyPath);
                Debug.WriteLine($"[CurlExe] {curlPath} {args.Substring(0, Math.Min(200, args.Length))}...");

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

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(node.TimeoutSeconds + 5));

                var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

                await process.WaitForExitAsync(timeoutCts.Token);
                sw.Stop();

                var stdoutText = await outputTask;
                var stderrText = await errorTask;

                ParseCurlOutputFromFiles(stdoutText, stderrText, tempHeaderPath, tempBodyPath, result);
                result.ElapsedMs = sw.ElapsedMilliseconds;

                Debug.WriteLine($"[CurlExe] Exit={process.ExitCode}, Status={result.StatusCode}");
            }
            catch (OperationCanceledException)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Request bị huỷ hoặc timeout.";
                result.ElapsedMs = sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ElapsedMs = sw.ElapsedMilliseconds;
            }
            finally
            {
                KillProcessTreeSafe(process);
                TryDeleteFile(tempHeaderPath);
                TryDeleteFile(tempBodyPath);
            }

            return result;
        }

        private static void KillProcessTreeSafe(Process? process)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // best effort
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }

        private static string BuildCurlArgs(
            HttpRequestNode node,
            string url,
            Dictionary<string, string> headers,
            string? body,
            string headerOutputPath,
            string bodyOutputPath)
        {
            var sb = new StringBuilder();

            sb.Append("-s");
            sb.Append(" --location");
            sb.Append(" --insecure");
            sb.Append(" --compressed");
            sb.Append($" --max-time {node.TimeoutSeconds}");

            // Browser impersonation (chỉ dùng nếu là curl-impersonate)
            if (!string.IsNullOrWhiteSpace(node.ImpersonateBrowser) && IsCurlImpersonate(node.CurlPath))
                sb.Append($" --impersonate {node.ImpersonateBrowser}");

            // Method
            sb.Append($" -X {node.HttpMethod.ToString().ToUpperInvariant()}");

            // Headers
            foreach (var h in headers)
            {
                var escaped = h.Value.Replace("\"", "\\\"");
                sb.Append($" -H \"{h.Key}: {escaped}\"");
            }

            // Body
            if (!string.IsNullOrEmpty(body) &&
                node.HttpMethod != Models.Nodes.HttpMethod.GET &&
                node.HttpMethod != Models.Nodes.HttpMethod.HEAD)
            {
                var escapedBody = body.Replace("\\", "\\\\").Replace("\"", "\\\"");
                sb.Append($" --data \"{escapedBody}\"");
            }

            // Output raw body to file so non-UTF8 payloads and split multibyte chars
            // are decoded correctly after the process exits.
            sb.Append($" -D \"{headerOutputPath.Replace("\"", "\\\"")}\"");
            sb.Append($" -o \"{bodyOutputPath.Replace("\"", "\\\"")}\"");
            sb.Append(" -w \"<<<CURL_STATUS>>>%{http_code}<<<END>>>\"");

            sb.Append($" \"{url.Replace("\"", "\\\"")}\"");

            return sb.ToString();
        }

        private static void ParseCurlOutput(string rawOutput, string stderr, CurlResult result)
        {
            const string separator = "<<<CURL_STATUS>>>";
            const string endMarker = "<<<END>>>";

            try
            {
                var statusStart = rawOutput.LastIndexOf(separator, StringComparison.Ordinal);
                if (statusStart < 0)
                {
                    // Fallback mode: raw command without -w marker.
                    ParseCurlOutputWithoutMarker(rawOutput, stderr, result);
                    return;
                }

                var statusEnd = rawOutput.IndexOf(endMarker, statusStart, StringComparison.Ordinal);
                var statusStr = rawOutput.Substring(
                    statusStart + separator.Length,
                    statusEnd > 0 ? statusEnd - statusStart - separator.Length : 3);

                int.TryParse(statusStr.Trim(), out int statusCode);

                var fullBody = rawOutput.Substring(0, statusStart).TrimEnd('\n', '\r');

                var headerBodySep = fullBody.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerBodySep < 0) headerBodySep = fullBody.IndexOf("\n\n", StringComparison.Ordinal);

                string headerBlock;
                string responseBody;

                if (headerBodySep >= 0)
                {
                    headerBlock = fullBody.Substring(0, headerBodySep);
                    responseBody = fullBody.Substring(headerBodySep + (fullBody[headerBodySep] == '\r' ? 4 : 2)).TrimStart('\r', '\n');
                }
                else
                {
                    headerBlock = string.Empty;
                    responseBody = fullBody;
                }

                result.StatusCode = statusCode;
                result.Body = responseBody;
                result.Headers = ParseHeaderBlock(headerBlock);
                result.IsSuccess = statusCode is >= 200 and < 300;
                
                if (statusCode == 0)
                {
                    result.ErrorMessage = !string.IsNullOrWhiteSpace(stderr) 
                        ? $"Lỗi kết nối (HTTP 0): {stderr.Trim()}" 
                        : "Lỗi kết nối (HTTP 0): Không nhận được phản hồi từ server (Vui lòng kiểm tra lại URL hoặc kết nối mạng).";
                }
                else
                {
                    result.ErrorMessage = result.IsSuccess ? string.Empty : $"HTTP {statusCode}";
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Parse error: {ex.Message}";
                result.Body = rawOutput;
            }
        }

        private static void ParseCurlOutputFromFiles(
            string rawOutput,
            string stderr,
            string? headerPath,
            string? bodyPath,
            CurlResult result)
        {
            const string separator = "<<<CURL_STATUS>>>";
            const string endMarker = "<<<END>>>";

            try
            {
                var statusCode = 0;
                var statusStart = rawOutput.LastIndexOf(separator, StringComparison.Ordinal);
                if (statusStart >= 0)
                {
                    var statusEnd = rawOutput.IndexOf(endMarker, statusStart, StringComparison.Ordinal);
                    var statusStr = rawOutput.Substring(
                        statusStart + separator.Length,
                        statusEnd > 0 ? statusEnd - statusStart - separator.Length : 3);
                    int.TryParse(statusStr.Trim(), out statusCode);
                }

                var headerBlock = ReadHeaderFile(headerPath);
                var responseHeaders = ParseHeaderBlock(headerBlock);
                var bodyBytes = ReadBodyFile(bodyPath);

                result.StatusCode = statusCode == 0 ? TryGetStatusCodeFromHeaderBlock(headerBlock) : statusCode;
                result.Body = DecodeResponseBody(bodyBytes, responseHeaders);
                result.Headers = responseHeaders;
                result.IsSuccess = result.StatusCode is >= 200 and < 300;

                if (result.StatusCode == 0)
                {
                    result.ErrorMessage = !string.IsNullOrWhiteSpace(stderr)
                        ? $"Lỗi kết nối (HTTP 0): {stderr.Trim()}"
                        : "Lỗi kết nối (HTTP 0): Không nhận được phản hồi từ server (Vui lòng kiểm tra lại URL hoặc kết nối mạng).";
                }
                else
                {
                    result.ErrorMessage = result.IsSuccess ? string.Empty : $"HTTP {result.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Parse error: {ex.Message}";
                result.Body = rawOutput;
            }
        }

        // -------------------------------------------------------
        // HELPERS
        // -------------------------------------------------------

        private static bool IsCurlImpersonate(string? customPath)
        {
            try
            {
                var curlPath = FindCurlExe(customPath);
                if (string.IsNullOrEmpty(curlPath)) return false;

                // curl-impersonate thường có "impersonate" trong tên file
                if (Path.GetFileNameWithoutExtension(curlPath)
                        .Contains("impersonate", StringComparison.OrdinalIgnoreCase))
                    return true;

                // Hoặc kiểm tra --help có chứa "--impersonate" không
                using var p = new Process();
                p.StartInfo = new ProcessStartInfo
                {
                    FileName = curlPath,
                    Arguments = "--help all",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                p.Start();
                var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(3000);
                return output.Contains("--impersonate", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string? FindCurlExe(string? customPath)
        {
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                return customPath;

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                var trim = dir.Trim();
                foreach (var name in new[] { "curl-impersonate.exe", "curl.exe" })
                {
                    var candidate = Path.Combine(trim, name);
                    if (File.Exists(candidate)) return candidate;
                }
            }

            var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
            if (File.Exists(sys32)) return sys32;

            var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
            foreach (var name in new[] { "curl-impersonate.exe", "curl.exe" })
            {
                var candidate = Path.Combine(appDir, name);
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        private static Dictionary<string, string> ParseHeaderBlock(string headerBlock)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(headerBlock)) return dict;

            foreach (var line in headerBlock.Split('\n'))
            {
                var trimmed = line.Trim('\r', ' ');
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("HTTP/")) continue;

                var colon = trimmed.IndexOf(':');
                if (colon <= 0) continue;

                dict[trimmed.Substring(0, colon).Trim()] = trimmed.Substring(colon + 1).Trim();
            }

            return dict;
        }

        private static string EnsureOutputMarkers(string command, string? headerOutputPath = null, string? bodyOutputPath = null)
        {
            var normalized = command.Trim();
            if (normalized.Contains("<<<CURL_STATUS>>>", StringComparison.Ordinal))
            {
                return normalized;
            }

            // Append status sentinel so ParseCurlOutput can extract HTTP code reliably.
            // Keep command as-is otherwise for maximum fidelity.
            if (!string.IsNullOrWhiteSpace(headerOutputPath) && !string.IsNullOrWhiteSpace(bodyOutputPath))
            {
                return normalized +
                       $" -D \"{headerOutputPath.Replace("\"", "\\\"")}\"" +
                       $" -o \"{bodyOutputPath.Replace("\"", "\\\"")}\"" +
                       " -w \"<<<CURL_STATUS>>>%{http_code}<<<END>>>\"";
            }

            return normalized + " -D - -w \"\\n<<<CURL_STATUS>>>%{http_code}<<<END>>>\"";
        }

        private static string EscapeCurlWriteOutForCmd(string command)
        {
            // In cmd.exe/.cmd files, '%' is environment variable syntax.
            // curl -w "%{http_code}" must be written as "%%{http_code}".
            // Also handle accidental "{http_code}" (missing %) variants.
            return command
                .Replace("%%{http_code}", "__AC_HTTP_CODE_ESCAPED__")
                .Replace("%{http_code}", "%%{http_code}")
                .Replace("{http_code}", "%%{http_code}")
                .Replace("__AC_HTTP_CODE_ESCAPED__", "%%{http_code}");
        }

        private static void ParseCurlOutputWithoutMarker(string rawOutput, string stderr, CurlResult result)
        {
            var fullBody = (rawOutput ?? string.Empty).TrimEnd('\n', '\r');
            var headerBodySep = fullBody.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerBodySep < 0) headerBodySep = fullBody.IndexOf("\n\n", StringComparison.Ordinal);

            string headerBlock;
            string responseBody;

            if (headerBodySep >= 0)
            {
                headerBlock = fullBody.Substring(0, headerBodySep);
                responseBody = fullBody.Substring(headerBodySep + (fullBody[headerBodySep] == '\r' ? 4 : 2)).TrimStart('\r', '\n');
            }
            else
            {
                headerBlock = string.Empty;
                responseBody = fullBody;
            }

            var statusCode = TryGetStatusCodeFromHeaderBlock(headerBlock);
            result.StatusCode = statusCode;
            result.Body = responseBody;
            result.Headers = ParseHeaderBlock(headerBlock);
            result.IsSuccess = statusCode is >= 200 and < 300;
            result.ErrorMessage = result.IsSuccess
                ? string.Empty
                : (!string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : $"HTTP {statusCode}");
        }

        private static int TryGetStatusCodeFromHeaderBlock(string headerBlock)
        {
            if (string.IsNullOrWhiteSpace(headerBlock))
            {
                return 0;
            }

            var lines = headerBlock
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (!line.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[1], out var code))
                {
                    return code;
                }
            }

            return 0;
        }

        private static bool LooksLikeAntiBotBlocked(CurlResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Body))
            {
                return false;
            }

            if (result.Body.IndexOf("anti-bot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            try
            {
                using var doc = JsonDocument.Parse(result.Body);
                if (!doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    return false;
                }

                var hasCode7 = errorElement.TryGetProperty("code", out var codeElement) &&
                               codeElement.ValueKind == JsonValueKind.Number &&
                               codeElement.TryGetInt32(out var code) &&
                               code == 7;

                if (hasCode7)
                {
                    return true;
                }

                if (errorElement.TryGetProperty("message", out var messageElement))
                {
                    var msg = messageElement.GetString();
                    return !string.IsNullOrWhiteSpace(msg) &&
                           msg.IndexOf("anti-bot", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (JsonException)
            {
                // Not JSON, ignore.
            }

            return false;
        }

        private static string ReadHeaderFile(string? headerPath)
        {
            if (string.IsNullOrWhiteSpace(headerPath) || !File.Exists(headerPath))
            {
                return string.Empty;
            }

            return DecodeHeaderBytes(File.ReadAllBytes(headerPath));
        }

        private static byte[] ReadBodyFile(string? bodyPath)
        {
            if (string.IsNullOrWhiteSpace(bodyPath) || !File.Exists(bodyPath))
            {
                return Array.Empty<byte>();
            }

            return File.ReadAllBytes(bodyPath);
        }

        private static string DecodeHeaderBytes(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            return Encoding.Latin1.GetString(bytes);
        }

        private static string DecodeResponseBody(byte[] bytes, Dictionary<string, string>? headers)
        {
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            var decompressed = DecompressIfNeeded(bytes, headers);
            var encoding = ResolveResponseEncoding(decompressed, headers);
            return encoding.GetString(decompressed);
        }

        private static Encoding ResolveResponseEncoding(byte[] bytes, Dictionary<string, string>? headers)
        {
            var bomEncoding = TryGetBomEncoding(bytes, out _);
            if (bomEncoding != null)
            {
                return bomEncoding;
            }

            var headerEncoding = TryGetEncodingFromHeaders(headers);
            if (headerEncoding != null)
            {
                return headerEncoding;
            }

            try
            {
                _ = new UTF8Encoding(false, true).GetString(bytes);
                return Encoding.UTF8;
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1252);
            }
        }

        private static Encoding? TryGetEncodingFromHeaders(Dictionary<string, string>? headers)
        {
            if (headers == null || !headers.TryGetValue("Content-Type", out var contentType) || string.IsNullOrWhiteSpace(contentType))
            {
                return null;
            }

            const string charsetToken = "charset=";
            var charsetIndex = contentType.IndexOf(charsetToken, StringComparison.OrdinalIgnoreCase);
            if (charsetIndex < 0)
            {
                return null;
            }

            var charset = contentType.Substring(charsetIndex + charsetToken.Length)
                .Split(';', StringSplitOptions.TrimEntries)[0]
                .Trim()
                .Trim('"', '\'');

            if (string.IsNullOrWhiteSpace(charset))
            {
                return null;
            }

            try
            {
                return Encoding.GetEncoding(charset);
            }
            catch
            {
                return null;
            }
        }

        private static Encoding? TryGetBomEncoding(byte[] bytes, out int bomLength)
        {
            bomLength = 0;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                bomLength = 3;
                return Encoding.UTF8;
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                bomLength = 2;
                return Encoding.Unicode;
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                bomLength = 2;
                return Encoding.BigEndianUnicode;
            }

            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                bomLength = 4;
                return Encoding.UTF32;
            }

            return null;
        }

        private static byte[] DecompressIfNeeded(byte[] bytes, Dictionary<string, string>? headers)
        {
            if (bytes.Length == 0)
            {
                return bytes;
            }

            var encodingHeader = headers != null && headers.TryGetValue("Content-Encoding", out var ce)
                ? ce
                : null;

            var encoding = (encodingHeader ?? string.Empty).ToLowerInvariant();

            try
            {
                if (encoding.Contains("gzip") || LooksLikeGzip(bytes))
                {
                    using var input = new MemoryStream(bytes);
                    using var gzip = new GZipStream(input, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    gzip.CopyTo(output);
                    return output.ToArray();
                }

                if (encoding.Contains("deflate"))
                {
                    using var input = new MemoryStream(bytes);
                    using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    deflate.CopyTo(output);
                    return output.ToArray();
                }

#if NET6_0_OR_GREATER
                if (encoding.Contains("br"))
                {
                    using var input = new MemoryStream(bytes);
                    using var br = new BrotliStream(input, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    br.CopyTo(output);
                    return output.ToArray();
                }
#endif
            }
            catch
            {
                // Nếu giải nén fail thì trả lại bytes gốc.
            }

            return bytes;
        }

        private static bool LooksLikeGzip(byte[] bytes)
        {
            return bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B;
        }

        private static void TryDeleteFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // best effort
            }
        }
        // -------------------------------------------------------
        // P/INVOKE: curl_slist (CurlNative.Curl không tồn tại trong 0.0.7)
        // -------------------------------------------------------

        [DllImport("libcurl", EntryPoint = "curl_slist_append", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SlistAppend(IntPtr slist, string data);

        [DllImport("libcurl", EntryPoint = "curl_slist_free_all", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SlistFreeAll(IntPtr slist);
    }
}