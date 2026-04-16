using FlowMy.Models.Nodes;
using CurlThin;
using CurlThin.Enums;
using CurlThin.SafeHandles;
using System.Diagnostics;
using System.IO;
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
                    commandForExec = EnsureOutputMarkers(commandForExec);
                    commandForExec = EscapeCurlWriteOutForCmd(commandForExec);
                }

                var tempCmdPath = Path.Combine(Path.GetTempPath(), $"ac_rawcurl_{Guid.NewGuid():N}.cmd");
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

                    var output = await outputTask;
                    var stderrText = await errorTask;

                    ParseCurlOutput(output, stderrText, result);
                    result.ElapsedMs = sw.ElapsedMilliseconds;
                    result.Backend = "curl.exe raw command";
                }
                finally
                {
                    KillProcessTreeSafe(process);
                    try
                    {
                        if (File.Exists(tempCmdPath))
                            File.Delete(tempCmdPath);
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

                    // Collect response body & headers
                    var responseBodyBuilder = new StringBuilder();
                    var responseHeaderBuilder = new StringBuilder();

                    // DataHandler delegate: UIntPtr(IntPtr data, UIntPtr size, UIntPtr nmemb, IntPtr userdata)
                    DataHandler writeHandler = (data, size, nmemb, _) =>
                    {
                        var length = (int)((ulong)size * (ulong)nmemb);
                        var bytes = new byte[length];
                        Marshal.Copy(data, bytes, 0, length);
                        responseBodyBuilder.Append(Encoding.UTF8.GetString(bytes));
                        return (UIntPtr)((ulong)size * (ulong)nmemb);
                    };

                    DataHandler headerHandler = (data, size, nmemb, _) =>
                    {
                        var length = (int)((ulong)size * (ulong)nmemb);
                        var bytes = new byte[length];
                        Marshal.Copy(data, bytes, 0, length);
                        responseHeaderBuilder.Append(Encoding.UTF8.GetString(bytes));
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

                    result.StatusCode = httpCode;
                    result.Body = responseBodyBuilder.ToString();
                    result.Headers = ParseHeaderBlock(responseHeaderBuilder.ToString());
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
            string? tempConfigPath = null;
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

                if (!string.IsNullOrEmpty(body) &&
                    node.HttpMethod != Models.Nodes.HttpMethod.GET &&
                    node.HttpMethod != Models.Nodes.HttpMethod.HEAD)
                {
                    tempBodyPath = Path.Combine(Path.GetTempPath(), $"ac_curl_body_{Guid.NewGuid():N}.txt");
                    await File.WriteAllTextAsync(tempBodyPath, body, Encoding.UTF8, ct);
                }

                tempConfigPath = Path.Combine(Path.GetTempPath(), $"ac_curl_cfg_{Guid.NewGuid():N}.cfg");
                var configContent = BuildCurlConfig(node, url, headers, tempBodyPath);
                await File.WriteAllTextAsync(tempConfigPath, configContent, Encoding.UTF8, ct);

                var args = $"--config \"{tempConfigPath}\"";
                Debug.WriteLine($"[CurlExe] {curlPath} --config \"{tempConfigPath}\"");

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

                var output = await outputTask;
                var stderrText = await errorTask;

                ParseCurlOutput(output, stderrText, result);
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
                TryDeleteFile(tempConfigPath);
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

        private static string BuildCurlArgs(HttpRequestNode node, string url, Dictionary<string, string> headers, string? body)
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

            // Output: body + header block + status code sentinel
            sb.Append(" -D -");
            sb.Append(" -w \"\\n<<<CURL_STATUS>>>%{http_code}<<<END>>>\"");

            sb.Append($" \"{url.Replace("\"", "\\\"")}\"");

            return sb.ToString();
        }

        private static string BuildCurlConfig(HttpRequestNode node, string url, Dictionary<string, string> headers, string? bodyFilePath)
        {
            var lines = new List<string>
            {
                "silent",
                "show-error",
                "location",
                "insecure",
                "compressed",
                $"max-time = {node.TimeoutSeconds}",
                $"request = \"{EscapeCurlConfigValue(node.HttpMethod.ToString().ToUpperInvariant())}\""
            };

            if (!string.IsNullOrWhiteSpace(node.ImpersonateBrowser) && IsCurlImpersonate(node.CurlPath))
            {
                lines.Add($"impersonate = \"{EscapeCurlConfigValue(node.ImpersonateBrowser)}\"");
            }

            foreach (var h in headers)
            {
                lines.Add($"header = \"{EscapeCurlConfigValue($"{h.Key}: {h.Value}")}\"");
            }

            if (!string.IsNullOrWhiteSpace(bodyFilePath))
            {
                lines.Add($"data-binary = \"@{EscapeCurlConfigValue(bodyFilePath)}\"");
            }

            // Keep parser contract: status sentinel must exist in stdout.
            lines.Add("dump-header = \"-\"");
            lines.Add("write-out = \"\\n<<<CURL_STATUS>>>%{http_code}<<<END>>>\"");
            lines.Add($"url = \"{EscapeCurlConfigValue(url)}\"");

            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        private static string EscapeCurlConfigValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void TryDeleteFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // Best-effort cleanup.
            }
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

        private static string EnsureOutputMarkers(string command)
        {
            var normalized = command.Trim();
            if (normalized.Contains("<<<CURL_STATUS>>>", StringComparison.Ordinal))
            {
                return normalized;
            }

            // Append status sentinel so ParseCurlOutput can extract HTTP code reliably.
            // Keep command as-is otherwise for maximum fidelity.
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
        // -------------------------------------------------------
        // P/INVOKE: curl_slist (CurlNative.Curl không tồn tại trong 0.0.7)
        // -------------------------------------------------------

        [DllImport("libcurl", EntryPoint = "curl_slist_append", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SlistAppend(IntPtr slist, string data);

        [DllImport("libcurl", EntryPoint = "curl_slist_free_all", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SlistFreeAll(IntPtr slist);
    }
}