using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Utils;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HttpMethod = System.Net.Http.HttpMethod;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class FileDownloadNodeExecutor : INodeExecutor
    {
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        public bool CanExecute(WorkflowNode node) => node is FileDownloadNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var n = (FileDownloadNode)node;
            var connections = env.Connections;
            var sw = Stopwatch.StartNew();
            var pinnedIncoming = env.IncomingConnection;
            var phaseSw = Stopwatch.StartNew();
            var timingLines = new List<string>();

            void Mark(string phase)
            {
                timingLines.Add($"{phase}: {phaseSw.ElapsedMilliseconds}ms");
                phaseSw.Restart();
            }

            void FinalizeTimingLog()
            {
                var totalMs = sw.ElapsedMilliseconds;
                var body = timingLines.Count == 0
                    ? "no-phase"
                    : string.Join("; ", timingLines);
                SetResolvedOutput(n, "timingLog", $"{body}; total: {totalMs}ms");
            }

            // Keep incoming "energy" active while long-running download is in progress.
            if (pinnedIncoming != null)
            {
                pinnedIncoming.IsExecutionPinned = true;
                pinnedIncoming.IsExecutionActive = true;
                env.OnEnteringNode?.Invoke(pinnedIncoming);
            }

            try
            {
                try
                {
                    SetResolvedOutput(n, "filePath", string.Empty);
                    SetResolvedOutput(n, "completed", "False");
                    SetResolvedOutput(n, "errorMessage", string.Empty);
                    SetResolvedOutput(n, "timingLog", string.Empty);

                var folder = ResolveString(
                    n.DownloadFolderPath,
                    n.FolderSourceNodeId,
                    n.FolderSourceOutputKey,
                    n,
                    connections,
                    env).Trim();
                Mark("resolve-folder");

                if (string.IsNullOrWhiteSpace(folder))
                {
                    SetError(n, "Thư mục tải về đang trống.");
                    sw.Stop();
                    FinalizeTimingLog();
                    env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                    await PublishAndTraverseAsync(n, env);
                    return;
                }

                if (!TryPrepareWritableFolder(folder, out var writableFolder, out var folderError, out var fallbackNotice))
                {
                    SetError(n, $"Không tạo được thư mục: {folderError}");
                    sw.Stop();
                    FinalizeTimingLog();
                    env.OnNodeFailed?.Invoke(n, folderError ?? "Folder not writable");
                    env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                    await PublishAndTraverseAsync(n, env);
                    return;
                }
                folder = writableFolder;
                if (!string.IsNullOrWhiteSpace(fallbackNotice))
                    timingLines.Add(fallbackNotice);
                Mark("ensure-folder");

                var curlText = ResolveString(n.CurlCommand, n.CurlSourceNodeId, n.CurlSourceOutputKey, n, connections, env).Trim();
                var urlText = ResolveString(n.DownloadUrl, n.UrlSourceNodeId, n.UrlSourceOutputKey, n, connections, env).Trim();
                Mark("resolve-inputs");

                var hasAdditionalTargets = n.SaveAdditionalOutputFiles && HasValidAdditionalOutputTargets(n);

                string? downloadUrl = null;
                HttpMethod method = HttpMethod.Get;
                string? body = null;
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                static bool LooksLikeCurl(string s)
                {
                    var t = (s ?? string.Empty).Trim();
                    return CurlParser.IsCurlCommand(t) || CurlParser.IsCurlCommand(NormalizeBoundCurlCommand(t));
                }

                static bool TryParseCurlDownload(string raw, out CurlParseResult? parsed)
                {
                    parsed = null;
                    if (string.IsNullOrWhiteSpace(raw)) return false;
                    var t = NormalizeBoundCurlCommand(raw.Trim());
                    if (!CurlParser.IsCurlCommand(t)) return false;
                    var r = CurlParser.Parse(t);
                    if (!r.IsValid || string.IsNullOrWhiteSpace(r.Url)) return false;
                    parsed = r;
                    return true;
                }

                var hadCurlShapedInput = LooksLikeCurl(curlText) || LooksLikeCurl(urlText);
                CurlParseResult? parsedCurl = null;
                string? curlParseError = null;
                if (TryParseCurlDownload(curlText, out parsedCurl) || TryParseCurlDownload(urlText, out parsedCurl))
                {
                    downloadUrl = parsedCurl!.Url;
                    method = ToSystemMethod(parsedCurl.Method);
                    foreach (var h in parsedCurl.Headers.Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.Key)))
                        headers[h.Key] = h.Value ?? string.Empty;

                    if (parsedCurl.BodyType == HttpBodyType.Raw || parsedCurl.BodyType == HttpBodyType.Json)
                        body = parsedCurl.RawBody;
                    else if (parsedCurl.BodyType == HttpBodyType.FormUrlEncoded && parsedCurl.FormData.Count > 0)
                    {
                        var parts = parsedCurl.FormData.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Key))
                            .Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value ?? "")}");
                        body = string.Join("&", parts);
                        if (!headers.ContainsKey("Content-Type"))
                            headers["Content-Type"] = "application/x-www-form-urlencoded";
                    }
                }
                else if (hadCurlShapedInput)
                {
                    // Có dạng cURL nhưng không lấy được URL — thử lấy lỗi từ lần parse cuối (ưu ô cURL rồi tới URL).
                    var probe = !string.IsNullOrWhiteSpace(curlText) && CurlParser.IsCurlCommand(NormalizeBoundCurlCommand(curlText.Trim()))
                        ? CurlParser.Parse(NormalizeBoundCurlCommand(curlText.Trim()))
                        : CurlParser.Parse(NormalizeBoundCurlCommand(urlText.Trim()));
                    curlParseError = string.IsNullOrWhiteSpace(probe.ErrorMessage) ? "Không parse được cURL." : probe.ErrorMessage;

                    if (!hasAdditionalTargets)
                    {
                        SetError(n, curlParseError);
                        sw.Stop();
                        FinalizeTimingLog();
                        env.OnNodeFailed?.Invoke(n, GetResolvedOutputString(n, "errorMessage"));
                        env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                        await PublishAndTraverseAsync(n, env);
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(downloadUrl) && !string.IsNullOrWhiteSpace(urlText))
                {
                    var plain = urlText.Trim();
                    if (!LooksLikeCurl(plain))
                        downloadUrl = plain;
                }

                Mark("parse-url-curl");
                if (string.IsNullOrWhiteSpace(downloadUrl) && !hasAdditionalTargets)
                {
                    SetError(n, "Chưa có URL tải hoặc cURL hợp lệ.");
                    sw.Stop();
                    FinalizeTimingLog();
                    env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                    await PublishAndTraverseAsync(n, env);
                    return;
                }

                string? lastAdditionalPath = null;
                var additionalErrors = new List<string>();
                if (hasAdditionalTargets)
                {
                    var fileNameSourceValueForTemplate = ResolveString(
                        string.Empty,
                        n.FileNameSourceNodeId,
                        n.FileNameSourceOutputKey,
                        n,
                        connections,
                        env);
                    var fileNameStemFromConfiguredSource = InferFilenameStemForTemplate(fileNameSourceValueForTemplate, string.Empty);

                    WriteAdditionalOutputFiles(
                        n,
                        folder,
                        connections,
                        env,
                        fileNameStemFromConfiguredSource,
                        additionalErrors,
                        out lastAdditionalPath);
                    Mark("write-additional-outputs");
                }

                string? httpSavedPath = null;
                if (!string.IsNullOrWhiteSpace(downloadUrl))
                {
                    var nameTemplate = n.FileNameTemplate;
                    var sourceFileNameRaw = ResolveString(
                        string.Empty,
                        n.FileNameSourceNodeId,
                        n.FileNameSourceOutputKey,
                        n,
                        connections,
                        env);
                    var sourceFileName = InferFilenameStemForTemplate(sourceFileNameRaw, string.Empty);
                    Mark("resolve-name-template");

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(env.CancellationToken);
                    var resp = await SendAsync(downloadUrl, method, headers, body, cts.Token);
                    Mark("send-request-and-headers");
                    if (!resp.IsSuccessStatusCode)
                    {
                        var msg = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                        if (additionalErrors.Count > 0)
                            msg += " | Lưu thêm: " + string.Join("; ", additionalErrors);
                        SetError(n, msg);
                        sw.Stop();
                        FinalizeTimingLog();
                        env.OnNodeFailed?.Invoke(n, GetResolvedOutputString(n, "errorMessage"));
                        env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                        await PublishAndTraverseAsync(n, env);
                        return;
                    }

                    var extFromResponse = GuessExtensionFromContentType(resp.Content.Headers.ContentType?.MediaType);
                    var dispName = TryGetFileNameFromContentDisposition(resp.Content.Headers.ContentDisposition?.ToString());
                    var extFromDisp = string.IsNullOrWhiteSpace(dispName) ? null : TakeReasonableExtensionOrNull(Path.GetExtension(dispName));
                    var extFromUrl = TakeReasonableExtensionOrNull(Path.GetExtension(new Uri(downloadUrl).AbsolutePath));

                    var maxBaseLen = Math.Clamp(n.MaxFileNameLength, 1, 512);
                    var finalPath = BuildOutputPath(
                        folder,
                        nameTemplate,
                        sourceFileName,
                        n.RemoveDiacriticsFromFileName,
                        maxBaseLen,
                        n.AutoIncrementIfExists,
                        extFromDisp,
                        extFromResponse,
                        extFromUrl);
                    Mark("build-output-path");

                    await using (var fs = new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    await using (var rs = await resp.Content.ReadAsStreamAsync(cts.Token))
                    {
                        await rs.CopyToAsync(fs, cts.Token);
                    }
                    Mark("download-and-write-stream");

                    finalPath = MaybeRenameWithSniffedExtension(finalPath);
                    Mark("sniff-extension");

                    if (!File.Exists(finalPath) || new FileInfo(finalPath).Length == 0)
                    {
                        var msg = "File tải về rỗng hoặc không ghi được.";
                        if (additionalErrors.Count > 0)
                            msg += " | Lưu thêm: " + string.Join("; ", additionalErrors);
                        SetError(n, msg);
                        sw.Stop();
                        FinalizeTimingLog();
                        env.OnNodeFailed?.Invoke(n, GetResolvedOutputString(n, "errorMessage"));
                        env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                        await PublishAndTraverseAsync(n, env);
                        return;
                    }
                    Mark("validate-file");
                    httpSavedPath = finalPath;
                }

                if (additionalErrors.Count > 0)
                {
                    SetError(n, string.Join("; ", additionalErrors));
                    if (!string.IsNullOrWhiteSpace(httpSavedPath))
                        SetResolvedOutput(n, "filePath", httpSavedPath);
                    else if (!string.IsNullOrWhiteSpace(lastAdditionalPath))
                        SetResolvedOutput(n, "filePath", lastAdditionalPath!);
                    sw.Stop();
                    FinalizeTimingLog();
                    env.OnNodeFailed?.Invoke(n, GetResolvedOutputString(n, "errorMessage"));
                    env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                    await PublishAndTraverseAsync(n, env);
                    return;
                }

                var primaryPath = !string.IsNullOrWhiteSpace(httpSavedPath)
                    ? httpSavedPath!
                    : (lastAdditionalPath ?? string.Empty);
                SetResolvedOutput(n, "filePath", primaryPath);
                SetResolvedOutput(n, "completed", "True");
                SetResolvedOutput(n, "errorMessage", string.Empty);
                sw.Stop();
                FinalizeTimingLog();
                env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                await PublishAndTraverseAsync(n, env);
                return;
                }
                catch (OperationCanceledException) when (!env.CancellationToken.IsCancellationRequested)
                {
                    SetError(n, "Hết thời gian chờ tải.");
                    env.OnNodeFailed?.Invoke(n, "Timeout");
                }
                catch (Exception ex)
                {
                    SetError(n, ex.Message);
                    env.OnNodeFailed?.Invoke(n, ex.Message);
                }

                sw.Stop();
                FinalizeTimingLog();
                env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                await PublishAndTraverseAsync(n, env);
            }
            finally
            {
                if (pinnedIncoming != null)
                {
                    pinnedIncoming.IsExecutionPinned = false;
                    pinnedIncoming.IsExecutionActive = false;
                    env.OnEnteringNode?.Invoke(null);
                }
            }
        }

        private static bool HasValidAdditionalOutputTargets(FileDownloadNode n)
        {
            if (n.AdditionalOutputSaves == null || n.AdditionalOutputSaves.Count == 0)
                return false;
            foreach (var e in n.AdditionalOutputSaves)
            {
                if (e == null) continue;
                if (!string.IsNullOrWhiteSpace(e.SourceNodeId) && !string.IsNullOrWhiteSpace(e.SourceOutputKey))
                    return true;
            }
            return false;
        }

        private static string NormalizeUserSaveExtension(string? saveFormat)
        {
            if (string.IsNullOrWhiteSpace(saveFormat))
                return ".txt";
            var t = saveFormat.Trim();
            if (t.StartsWith('.'))
                t = t.TrimStart('.');
            if (string.IsNullOrEmpty(t))
                return ".txt";
            t = "." + t.ToLowerInvariant();
            return IsReasonableFileExtension(t) ? t : ".txt";
        }

        private static string InferFilenameStemForTemplate(string content, string outputKey)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(content))
                {
                    // Deterministic: use exactly resolved value from selected Node+Key.
                    var candidate = content
                        .Replace("\r\n", "\n", StringComparison.Ordinal)
                        .Split('\n')
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                        ?.Trim() ?? string.Empty;

                    // If selected value is JSON/object payload, extract first scalar VALUE (not key name).
                    if ((candidate.StartsWith("{", StringComparison.Ordinal) || candidate.StartsWith("[", StringComparison.Ordinal)) &&
                        TryExtractFirstScalarValueFromJson(content, out var scalarFromJson) &&
                        !string.IsNullOrWhiteSpace(scalarFromJson))
                    {
                        candidate = scalarFromJson!;
                    }

                    candidate = candidate.Trim().Trim('"').TrimEnd(',');
                    if (!string.IsNullOrWhiteSpace(candidate) &&
                        !string.Equals(candidate, "{", StringComparison.Ordinal) &&
                        !string.Equals(candidate, "}", StringComparison.Ordinal) &&
                        !string.Equals(candidate, "[", StringComparison.Ordinal) &&
                        !string.Equals(candidate, "]", StringComparison.Ordinal))
                    {
                        var fn = Path.GetFileNameWithoutExtension(candidate);
                        if (!string.IsNullOrWhiteSpace(fn))
                            return fn;
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static bool TryExtractFirstScalarValueFromJson(string text, out string? value)
        {
            value = null;
            try
            {
                using var doc = JsonDocument.Parse(text);
                return TryExtractFirstScalarRecursive(doc.RootElement, out value);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryExtractFirstScalarRecursive(JsonElement element, out string? value)
        {
            value = null;
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    value = element.GetString();
                    return !string.IsNullOrWhiteSpace(value);
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    value = element.ToString();
                    return !string.IsNullOrWhiteSpace(value);
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (TryExtractFirstScalarRecursive(prop.Value, out value))
                            return true;
                    }
                    return false;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        if (TryExtractFirstScalarRecursive(item, out value))
                            return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
        private static void WriteAdditionalOutputFiles(
            FileDownloadNode n,
            string folder,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env,
            string? configuredFileNameStem,
            List<string> errors,
            out string? lastWrittenPath)
        {
            lastWrittenPath = null;
            var maxBaseLen = Math.Clamp(n.MaxFileNameLength, 1, 512);
            var saves = n.AdditionalOutputSaves ?? new List<FileDownloadAdditionalOutputSaveEntry>();

            foreach (var entry in saves)
            {
                if (entry == null) continue;
                if (string.IsNullOrWhiteSpace(entry.SourceNodeId) || string.IsNullOrWhiteSpace(entry.SourceOutputKey))
                    continue;

                var content = ResolveString(
                    string.Empty,
                    entry.SourceNodeId,
                    entry.SourceOutputKey,
                    n,
                    connections,
                    env);
                if (string.IsNullOrWhiteSpace(content) || content == "—")
                {
                    errors.Add($"Lưu thêm [{entry.SourceOutputKey}]: output rỗng.");
                    continue;
                }

                var nameTemplate = string.IsNullOrWhiteSpace(entry.NameTemplate)
                    ? n.FileNameTemplate
                    : entry.NameTemplate!;

                var stemHint = !string.IsNullOrWhiteSpace(configuredFileNameStem)
                    ? configuredFileNameStem!
                    : InferFilenameStemForTemplate(content, string.Empty);
                var ext = NormalizeUserSaveExtension(entry.SaveFormat);
                string? path;
                try
                {
                    path = BuildOutputPath(
                        folder,
                        nameTemplate,
                        stemHint,
                        n.RemoveDiacriticsFromFileName,
                        maxBaseLen,
                        n.AutoIncrementIfExists,
                        ext,
                        ext,
                        ext);
                }
                catch (Exception ex)
                {
                    errors.Add($"Lưu thêm [{entry.SourceOutputKey}]: {ex.Message}");
                    continue;
                }

                try
                {
                    File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    lastWrittenPath = path;
                }
                catch (Exception ex)
                {
                    errors.Add($"Lưu thêm [{entry.SourceOutputKey}]: {ex.Message}");
                }
            }
        }

        private static async Task PublishAndTraverseAsync(FileDownloadNode n, NodeExecutionEnvironment env)
        {
            n.NotifyRuntimeOutputsChanged();
            if (!string.IsNullOrWhiteSpace(env.ExecutionId))
            {
                Dictionary<string, object?> snapshot;
                lock (n.ResolvedOutputsSyncRoot)
                {
                    snapshot = new Dictionary<string, object?>(n.ResolvedOutputs, StringComparer.OrdinalIgnoreCase);
                }
                env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, n.Id, snapshot);
            }
            await env.TraverseOutputsAsync(n);
        }

        private static void SetError(FileDownloadNode n, string message)
        {
            SetResolvedOutput(n, "filePath", string.Empty);
            SetResolvedOutput(n, "completed", "False");
            SetResolvedOutput(n, "errorMessage", message ?? string.Empty);
        }

        private static void SetResolvedOutput(FileDownloadNode node, string key, object? value)
        {
            lock (node.ResolvedOutputsSyncRoot)
            {
                node.ResolvedOutputs[key] = value;
            }
        }

        private static string GetResolvedOutputString(FileDownloadNode node, string key)
        {
            lock (node.ResolvedOutputsSyncRoot)
            {
                return node.ResolvedOutputs.TryGetValue(key, out var value)
                    ? value?.ToString() ?? string.Empty
                    : string.Empty;
            }
        }

        private static async Task<HttpResponseMessage> SendAsync(
            string url,
            HttpMethod method,
            Dictionary<string, string> headers,
            string? body,
            CancellationToken ct)
        {
            using var req = new HttpRequestMessage(method, url);
            foreach (var kv in headers)
            {
                if (!req.Headers.TryAddWithoutValidation(kv.Key, kv.Value))
                {
                    req.Content ??= new StringContent(string.Empty);
                    req.Content.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }

            if (body != null && method != HttpMethod.Get && method != HttpMethod.Head)
            {
                var content = new StringContent(body, Encoding.UTF8);
                if (headers.TryGetValue("Content-Type", out var ctype) && !string.IsNullOrWhiteSpace(ctype))
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(ctype);
                req.Content = content;
            }

            return await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        }

        private static HttpMethod ToSystemMethod(FlowMy.Models.Nodes.HttpMethod m) => m switch
        {
            FlowMy.Models.Nodes.HttpMethod.POST => HttpMethod.Post,
            FlowMy.Models.Nodes.HttpMethod.PUT => HttpMethod.Put,
            FlowMy.Models.Nodes.HttpMethod.DELETE => HttpMethod.Delete,
            FlowMy.Models.Nodes.HttpMethod.PATCH => HttpMethod.Patch,
            FlowMy.Models.Nodes.HttpMethod.HEAD => HttpMethod.Head,
            FlowMy.Models.Nodes.HttpMethod.OPTIONS => HttpMethod.Options,
            _ => HttpMethod.Get
        };

        /// <summary>
        /// Max base-name length so folder + file stays within Windows MAX_PATH (259 usable characters).
        /// </summary>
        private static int GetMaxBaseCharsForFullPath(string folder, string extensionWithDot, bool autoInc)
        {
            const int maxFullPathChars = 259;
            var extLen = string.IsNullOrEmpty(extensionWithDot) ? 0 : extensionWithDot.Length;
            if (extLen > 0 && extensionWithDot![0] != '.')
                extLen++;
            // Dự phòng cho autoInc: "_9999"
            var incReserve = autoInc ? 6 : 0;
            var prefixLen = folder.Length + 1 + incReserve + extLen;
            var budget = maxFullPathChars - prefixLen;
            return budget < 1 ? 1 : Math.Min(budget, 512);
        }

        /// <summary>
        /// Truncate without splitting UTF-16 surrogate pairs (invalid names can trigger Win32 path errors).
        /// </summary>
        private static string TruncateUtf16Safe(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxChars)
                return s;
            if (maxChars <= 0)
                return "file";
            var cut = maxChars;
            if (cut < s.Length && char.IsHighSurrogate(s[cut - 1]) && char.IsLowSurrogate(s[cut]))
                cut--;
            return cut <= 0 ? "file" : s.Substring(0, cut);
        }

        private static string TrimWindowsFileNameTrailing(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            var t = s;
            while (t.Length > 0 && (t[^1] == '.' || t[^1] == ' '))
                t = t[..^1];
            return t;
        }

        /// <summary>
        /// Only treat a suffix as a file extension when it is short and has no spaces, so sentence periods
        /// in prompts are not mistaken for extensions (which would bypass MaxFileNameLength on the stem).
        /// </summary>
        private static bool IsReasonableFileExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext) || ext[0] != '.')
                return false;
            var rest = ext.AsSpan(1);
            if (rest.Length == 0 || rest.Length > 16)
                return false;
            foreach (var c in rest)
            {
                if (char.IsWhiteSpace(c))
                    return false;
                if (!char.IsAsciiLetterOrDigit(c) && c is not '+' and not '-')
                    return false;
            }
            return true;
        }

        private static string? TakeReasonableExtensionOrNull(string? ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
                return null;
            return IsReasonableFileExtension(ext) ? ext : null;
        }

        private static void SplitStemAndPlausibleExtension(string cleanFileName, out string stem, out string extOrEmpty)
        {
            var ext = Path.GetExtension(cleanFileName);
            if (IsReasonableFileExtension(ext))
            {
                extOrEmpty = ext;
                stem = Path.GetFileNameWithoutExtension(cleanFileName);
            }
            else
            {
                extOrEmpty = string.Empty;
                stem = cleanFileName;
            }
        }

        private static string? TryGuessExtensionFromFileStart(string path)
        {
            Span<byte> head = stackalloc byte[48];
            using var fs = File.OpenRead(path);
            var n = fs.Read(head);
            if (n < 12)
                return null;
            return GuessExtensionFromMagic(head[..n]);
        }

        private static string? GuessExtensionFromMagic(ReadOnlySpan<byte> b)
        {
            if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
                return ".jpg";
            if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
                return ".png";
            if (b.Length >= 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38)
                return ".gif";
            if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46)
            {
                if (b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
                    return ".webp";
                if (b[8] == 0x57 && b[9] == 0x41 && b[10] == 0x56 && b[11] == 0x45)
                    return ".wav";
                if (b[8] == 0x41 && b[9] == 0x56 && b[10] == 0x49 && b[11] == 0x20)
                    return ".avi";
            }
            if (b.Length >= 8 && b[4] == (byte)'f' && b[5] == (byte)'t' && b[6] == (byte)'y' && b[7] == (byte)'p')
                return ".mp4";
            if (b.Length >= 4 && b[0] == 0x1A && b[1] == 0x45 && b[2] == 0xDF && b[3] == 0xA3)
                return ".webm";
            if (b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46)
                return ".pdf";
            if (b.Length >= 2 && b[0] == 0x50 && b[1] == 0x4B)
                return ".zip";
            return null;
        }

        /// <summary>
        /// Rename when saved as .bin or unknown but magic bytes match a common format.
        /// </summary>
        private static string MaybeRenameWithSniffedExtension(string finalPath)
        {
            try
            {
                if (!File.Exists(finalPath))
                    return finalPath;
                if (new FileInfo(finalPath).Length < 12)
                    return finalPath;

                var extNow = Path.GetExtension(finalPath);
                var sniffed = TryGuessExtensionFromFileStart(finalPath);
                if (string.IsNullOrEmpty(sniffed))
                    return finalPath;
                if (string.Equals(extNow, sniffed, StringComparison.OrdinalIgnoreCase))
                    return finalPath;
                if (IsReasonableFileExtension(extNow) && !string.Equals(extNow, ".bin", StringComparison.OrdinalIgnoreCase))
                    return finalPath;

                var dir = Path.GetDirectoryName(finalPath)!;
                var baseName = Path.GetFileNameWithoutExtension(finalPath);
                var dest = Path.Combine(dir, baseName + sniffed);
                if (File.Exists(dest))
                {
                    for (var i = 1; i < 10_000; i++)
                    {
                        dest = Path.Combine(dir, baseName + "_" + i + sniffed);
                        if (!File.Exists(dest))
                            break;
                    }
                    if (File.Exists(dest))
                        return finalPath;
                }
                File.Move(finalPath, dest);
                return dest;
            }
            catch
            {
                return finalPath;
            }
        }

        private static string BuildOutputPath(
            string folder,
            string nameTemplate,
            string? sourceFileName,
            bool removeDiacritics,
            int maxBaseLen,
            bool autoInc,
            string? extFromDisposition,
            string? extFromContentType,
            string? extFromUrl)
        {
            var now = DateTime.Now;
            var template = (nameTemplate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(template))
                template = "download_{datetime}";
            if (!string.IsNullOrWhiteSpace(sourceFileName) &&
                template.IndexOf("{filename}", StringComparison.OrdinalIgnoreCase) < 0)
            {
                template = "{filename}_" + template;
            }

            var sourceBaseName = Path.GetFileNameWithoutExtension(sourceFileName ?? string.Empty);
            template = Regex.Replace(template, @"\{filename\}", sourceBaseName, RegexOptions.IgnoreCase);

            template = Regex.Replace(template, @"\{date\}", now.ToString("yyyy-MM-dd"), RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"\{time\}", now.ToString("HHmmss"), RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"\{datetime\}", now.ToString("yyyyMMdd_HHmmss"), RegexOptions.IgnoreCase);

            var hasIndexToken = Regex.IsMatch(template, @"\{index\}", RegexOptions.IgnoreCase);

            string ext = !string.IsNullOrWhiteSpace(extFromDisposition) ? extFromDisposition!
                : !string.IsNullOrWhiteSpace(extFromContentType) ? extFromContentType!
                : !string.IsNullOrWhiteSpace(extFromUrl) ? extFromUrl!
                : ".bin";

            if (!ext.StartsWith('.'))
                ext = "." + ext;

            string SanitizeFileName(string s)
            {
                if (removeDiacritics)
                    s = RemoveDiacritics(s);

                foreach (var c in Path.GetInvalidFileNameChars())
                    s = s.Replace(c, '_');
                s = s.Replace('\uFF1A', '_').Replace('\uFF0F', '_').Replace('\uFF3C', '_');
                s = Regex.Replace(s, @"[\u0000-\u001F]", string.Empty);
                s = Regex.Replace(s, @"\s+", " ").Trim();
                s = TrimWindowsFileNameTrailing(s);
                return NormalizeReservedName(s);
            }

            string TruncateBase(string baseName, string extForPathBudget)
            {
                if (string.IsNullOrEmpty(baseName)) return "file";
                var userCap = Math.Clamp(maxBaseLen, 1, 512);
                var pathCap = GetMaxBaseCharsForFullPath(folder, extForPathBudget, autoInc);
                var limit = Math.Min(userCap, pathCap);
                baseName = TrimWindowsFileNameTrailing(baseName);
                if (string.IsNullOrEmpty(baseName))
                    baseName = "file";
                return TruncateUtf16Safe(baseName, limit);
            }

            IEnumerable<string> IndexSuffixes()
            {
                yield return "";
                for (var i = 1; i < 10_000; i++)
                    yield return "_" + i;
            }

            if (hasIndexToken)
            {
                foreach (var suf in autoInc ? IndexSuffixes() : new[] { "" })
                {
                    var named = Regex.Replace(template, @"\{index\}", suf, RegexOptions.IgnoreCase);
                    named = SanitizeFileName(named);
                    SplitStemAndPlausibleExtension(named, out var baseName, out var templateExt);
                    var useExt = !string.IsNullOrWhiteSpace(templateExt) ? templateExt : ext;
                    baseName = TruncateBase(baseName, useExt);
                    baseName = TrimWindowsFileNameTrailing(baseName);
                    if (string.IsNullOrEmpty(baseName))
                        baseName = "file";
                    var fileName = baseName + useExt;
                    var full = Path.Combine(folder, fileName);
                    if (!autoInc || !File.Exists(full))
                        return full;
                }

                throw new IOException("Không tìm được tên file trống (quá nhiều bản trùng).");
            }

            var cleanTemplate = SanitizeFileName(template);
            SplitStemAndPlausibleExtension(cleanTemplate, out var baseFromTemplate, out var extInTemplate);
            var finalExt = !string.IsNullOrWhiteSpace(extInTemplate) ? extInTemplate : ext;
            baseFromTemplate = TruncateBase(baseFromTemplate, finalExt);
            baseFromTemplate = TrimWindowsFileNameTrailing(baseFromTemplate);
            if (string.IsNullOrEmpty(baseFromTemplate))
                baseFromTemplate = "file";

            if (!autoInc)
                return Path.Combine(folder, baseFromTemplate + finalExt);

            foreach (var suf in IndexSuffixes())
            {
                var name = suf.Length == 0 ? baseFromTemplate : baseFromTemplate + suf;
                var full = Path.Combine(folder, name + finalExt);
                if (!File.Exists(full))
                    return full;
            }

            throw new IOException("Không tìm được tên file trống (quá nhiều bản trùng).");
        }

        private static bool TryPrepareWritableFolder(string requestedFolder, out string writableFolder, out string? error, out string? fallbackNotice)
        {
            writableFolder = requestedFolder;
            error = null;
            fallbackNotice = null;

            if (TryEnsureWritableFolder(requestedFolder, out var ensureError))
                return true;

            var fallback = GetFallbackDownloadFolder();
            if (TryEnsureWritableFolder(fallback, out var fallbackError))
            {
                writableFolder = fallback;
                fallbackNotice = $"fallback-folder:{fallback}";
                return true;
            }

            error = string.IsNullOrWhiteSpace(ensureError) ? fallbackError : ensureError;
            return false;
        }

        private static bool TryEnsureWritableFolder(string folder, out string? error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(folder);
                var probeFile = Path.Combine(folder, $".FlowMy_write_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probeFile, "ok");
                File.Delete(probeFile);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string GetFallbackDownloadFolder()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                var downloads = Path.Combine(userProfile, "Downloads");
                return Path.Combine(downloads, "FlowMy");
            }
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "FlowMy", "Downloads");
        }

        private static string RemoveDiacritics(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString()
                     .Normalize(NormalizationForm.FormC)
                     .Replace('đ', 'd')
                     .Replace('Đ', 'D');
        }

        private static string NormalizeReservedName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "file";

            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CON","PRN","AUX","NUL",
                "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
                "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
            };
            return reserved.Contains(name) ? $"_{name}" : name;
        }

        private static string? GuessExtensionFromContentType(string? mediaType)
        {
            if (string.IsNullOrWhiteSpace(mediaType))
                return null;
            var mt = mediaType.ToLowerInvariant();
            var semi = mt.IndexOf(';');
            if (semi >= 0)
                mt = mt[..semi].TrimEnd();

            if (mt.Contains("jpeg") || mt == "image/jpg")
                return ".jpg";
            if (mt.Contains("png"))
                return ".png";
            if (mt.Contains("gif"))
                return ".gif";
            if (mt.Contains("webp"))
                return ".webp";
            if (mt.Contains("svg"))
                return ".svg";
            if (mt.Contains("bmp"))
                return ".bmp";
            if (mt.Contains("tif"))
                return ".tif";
            if (mt.Contains("video/quicktime") || (mt.Contains("quicktime") && mt.Contains("video")))
                return ".mov";
            if (mt.Contains("mp4"))
                return ".mp4";
            if (mt.Contains("webm"))
                return ".webm";
            if (mt.Contains("mpeg") && mt.Contains("video"))
                return ".mpeg";
            if (mt.Contains("x-msvideo") || mt.Contains("msvideo"))
                return ".avi";
            if (mt.Contains("audio/mpeg") || mt.Contains("audio/mp3") || mt == "audio/mp3")
                return ".mp3";
            if (mt.Contains("audio/mp4") || mt.Contains("audio/x-m4a"))
                return ".m4a";
            if (mt.Contains("ogg"))
                return ".ogg";
            if (mt.Contains("opus"))
                return ".opus";
            if (mt.Contains("wav"))
                return ".wav";
            if (mt.Contains("flac"))
                return ".flac";
            if (mt.Contains("json"))
                return ".json";
            if (mt.Contains("text/plain"))
                return ".txt";
            if (mt.Contains("text/html"))
                return ".html";
            if (mt.Contains("text/css"))
                return ".css";
            if (mt.Contains("javascript") || mt.Contains("/ecmascript"))
                return ".js";
            if (mt.Contains("pdf"))
                return ".pdf";
            if (mt.Contains("zip"))
                return ".zip";
            if (mt.Contains("xml"))
                return ".xml";
            if (mt.Contains("octet-stream"))
                return null;
            return null;
        }

        private static string? TryGetFileNameFromContentDisposition(string? cd)
        {
            if (string.IsNullOrWhiteSpace(cd)) return null;
            var m = Regex.Match(cd, @"filename\*?\s*=\s*UTF-8''([^;]+)|filename\s*=\s*""([^""]+)""|filename\s*=\s*([^;\s]+)", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            var raw = m.Groups[1].Success ? m.Groups[1].Value : (m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);
            try
            {
                return Uri.UnescapeDataString(raw.Trim().Trim('"'));
            }
            catch
            {
                return raw.Trim().Trim('"');
            }
        }

        private static string ResolveString(
            string staticValue,
            string? sourceNodeId,
            string? sourceOutputKey,
            FileDownloadNode current,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(sourceOutputKey))
                return staticValue ?? string.Empty;

            var source = FindSourceNode(sourceNodeId, connections, current, env.ReachableToEnd);
            if (source == null)
                return staticValue ?? string.Empty;

            string? v = null;
            foreach (var lookupRunId in WorkflowKeyValueStore.EnumerateScopedLookupExecutionIds(env.ExecutionId))
            {
                if (!env.Service.TryGetScopedNodeStringOutput(lookupRunId, source.Id, sourceOutputKey, out var scoped) || string.IsNullOrWhiteSpace(scoped))
                    continue;
                v = scoped;
                break;
            }

            // Fallback: một số node (Web, HtmlUI, ...) populate output vào DynamicOutputs bất đồng bộ
            // và không luôn publish sang scoped store ngay. Khi scoped-miss, đọc từ shared state qua service
            // để có URL/cURL chính xác thay vì fallback về chuỗi rỗng gây HTTP 400.
            if (string.IsNullOrWhiteSpace(v) || v == "—")
            {
                var shared = env.Service.ResolveDynamicValueForRun(source, sourceOutputKey, env.ExecutionId);
                if (!string.IsNullOrWhiteSpace(shared) && shared != "—")
                    v = shared;
            }

            if (v == "—" || string.IsNullOrWhiteSpace(v))
                return staticValue ?? string.Empty;
            return v;
        }

        private static WorkflowNode? FindSourceNode(
            string sourceNodeId,
            List<WorkflowConnection> connections,
            FileDownloadNode current,
            IEnumerable<WorkflowNode>? allNodes)
        {
            if (allNodes != null)
            {
                var fromAll = allNodes.FirstOrDefault(n =>
                    string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
                if (fromAll != null) return fromAll;
            }

            var upstream = connections.FirstOrDefault(c =>
                c.ToNode == current && c.FromNode != null && c.FromNode.Id == sourceNodeId);
            if (upstream?.FromNode != null) return upstream.FromNode;

            return connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeBoundCurlCommand(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var text = input.Trim();

            var extracted = TryExtractFirstStringFromJsonContainer(text);
            if (!string.IsNullOrWhiteSpace(extracted))
                text = extracted.Trim();

            try
            {
                var encoded = JsonSerializer.Serialize(text);
                var decoded = JsonSerializer.Deserialize<string>(encoded);
                return (decoded ?? text).Trim();
            }
            catch
            {
                return text;
            }
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
            catch { }
            return null;
        }
    }
}
