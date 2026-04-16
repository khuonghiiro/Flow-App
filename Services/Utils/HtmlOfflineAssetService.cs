using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FlowMy.Services.Utils
{
    /// <summary>
    /// Service quản lý thư viện JS/CSS offline cho HtmlUiNode.
    /// Lưu tất cả assets vào %AppData%\FlowMy\HtmlUiAssets\.
    /// </summary>
    public static class HtmlOfflineAssetService
    {
        private static readonly string _assetsFolder;
        private static readonly HttpClient _httpClient;

        /// <summary>Bắt url(...) trong CSS: nội dung có thể là URL tuyệt đối hoặc tương đối (Bootstrap Icons, v.v.).</summary>
        private static readonly Regex CssUrlRegex = new(
            @"url\(\s*(?:(['""])(?<quoted>(?:(?!\1).)*)\1|(?<unquoted>[^)'""\s]+))\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static HtmlOfflineAssetService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _assetsFolder = Path.Combine(appData, "FlowMy", "HtmlUiAssets");
            Directory.CreateDirectory(_assetsFolder);

            // Một số môi trường (máy công ty) có proxy/SSL inspection → cần dùng proxy hệ thống.
            // Đồng thời ép TLS hiện đại để tránh "SSL connect could not be established" khi OS mặc định bị giới hạn.
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                Proxy = WebRequest.DefaultWebProxy,
                UseProxy = true,
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            };

            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FlowMy", "1.0"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows)"));
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        private static string FlattenExceptionMessage(Exception ex)
        {
            try
            {
                var msgs = new List<string>();
                Exception? cur = ex;
                var depth = 0;
                while (cur != null && depth++ < 6)
                {
                    var msg = cur.Message?.Trim();
                    if (!string.IsNullOrWhiteSpace(msg) && !msgs.Contains(msg))
                        msgs.Add(msg);
                    cur = cur.InnerException;
                }
                return string.Join(" | ", msgs);
            }
            catch
            {
                return ex.Message;
            }
        }

        /// <summary>Trả về thư mục lưu assets (tự tạo nếu chưa có).</summary>
        public static string GetAssetsFolder()
        {
            Directory.CreateDirectory(_assetsFolder);
            return _assetsFolder;
        }

        /// <summary>Trả về full path của file asset theo tên file.</summary>
        public static string GetLocalFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;
            return Path.Combine(_assetsFolder, fileName);
        }

        /// <summary>Kiểm tra file asset có tồn tại trên disk không.</summary>
        public static bool AssetExists(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            return File.Exists(GetLocalFilePath(fileName));
        }

        /// <summary>
        /// Tải file từ URL và lưu vào thư mục assets.
        /// Trả về tên file thực sự đã lưu (có thể khác nếu trùng tên).
        /// </summary>
        public static async Task<string> DownloadAssetAsync(
            string url,
            string fileName,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL không được để trống.", nameof(url));
            if (string.IsNullOrWhiteSpace(fileName)) fileName = GuessFileNameFromUrl(url);

            // Làm sạch tên file
            fileName = SanitizeFileName(fileName);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "asset_" + DateTime.Now.Ticks + ".js";

            // Stylesheet (Google Fonts, Bootstrap Icons CDN, …): url() tương đối hoặc tuyệt đối — khi inline vào HtmlUi
            // (data:/file) không resolve được sang CDN → tải mọi url() và nhúng data URI.
            if (IsGoogleFontsStylesheetUrl(url) || ShouldEmbedCssResourcesForOffline(url, fileName))
                return await DownloadStylesheetWithEmbeddedResourcesAsync(url, fileName, progress, cancellationToken);

            var targetPath = GetLocalFilePath(fileName);

            progress?.Report($"Đang kết nối: {url}");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException hre)
            {
                // Thường là SSL/proxy/DNS. Trả message có inner exception để user biết nguyên nhân thật.
                throw new InvalidOperationException($"Không tải được URL: {url}. Chi tiết: {FlattenExceptionMessage(hre)}", hre);
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalRead += bytesRead;
                progress?.Report($"Đã tải: {FormatBytes(totalRead)}");
            }

            progress?.Report($"✓ Hoàn thành ({FormatBytes(totalRead)})");
            return fileName;
        }

        /// <summary>True nếu URL là stylesheet API của Google Fonts (css2 hoặc css).</summary>
        public static bool IsGoogleFontsStylesheetUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Contains("fonts.googleapis.com/css", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True nên tải CSS và nhúng mọi url() thành data URI (font/ảnh nhỏ) để HtmlUi inline hoạt động.</summary>
        public static bool ShouldEmbedCssResourcesForOffline(string url, string fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName) && fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                return true;
            try
            {
                var u = new Uri(url);
                var seg = u.Segments.Length > 0 ? u.Segments[^1].TrimEnd('/') : "";
                if (seg.Contains(".css", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Tải stylesheet, resolve mọi <c>url(...)</c> (tuyệt đối hoặc tương đối so với URL file CSS), tải binary và ghi CSS với <c>data:</c>.
        /// </summary>
        public static async Task<string> DownloadStylesheetWithEmbeddedResourcesAsync(
            string cssUrl,
            string fileName,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cssUrl)) throw new ArgumentException("URL không được để trống.", nameof(cssUrl));
            if (string.IsNullOrWhiteSpace(fileName)) fileName = GuessFileNameFromUrl(cssUrl);
            fileName = SanitizeFileName(fileName);
            if (!fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                fileName += ".css";

            var stylesheetUri = new Uri(cssUrl);

            progress?.Report($"Đang tải stylesheet: {cssUrl}");
            using var cssResponse = await _httpClient.GetAsync(cssUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            cssResponse.EnsureSuccessStatusCode();
            var css = await cssResponse.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(css)) throw new InvalidOperationException("Stylesheet trả về nội dung rỗng.");

            var matches = CssUrlRegex.Matches(css).Cast<Match>().Where(m => m.Success).ToList();
            var absoluteToReplacement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var uniqueResourceUris = new List<Uri>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in matches)
            {
                var raw = m.Groups["quoted"].Success ? m.Groups["quoted"].Value : m.Groups["unquoted"].Value;
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (!TryResolveCssResourceUrl(raw.Trim(), stylesheetUri, out var resourceUri)) continue;
                var key = resourceUri!.AbsoluteUri;
                if (!seenKeys.Add(key)) continue;
                uniqueResourceUris.Add(resourceUri);
            }

            for (var i = 0; i < uniqueResourceUris.Count; i++)
            {
                var resourceUri = uniqueResourceUris[i];
                progress?.Report($"Đang tải tài nguyên CSS {i + 1}/{uniqueResourceUris.Count}...");

                using var resp = await _httpClient.GetAsync(resourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                resp.EnsureSuccessStatusCode();
                var len = resp.Content.Headers.ContentLength;
                if (len is > 12 * 1024 * 1024)
                    throw new InvalidOperationException($"Tài nguyên quá lớn ({len} bytes).");

                var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
                if (bytes.Length > 12 * 1024 * 1024)
                    throw new InvalidOperationException("Tài nguyên quá lớn sau khi tải.");

                var mime = GuessMimeTypeForUrl(resourceUri, resp.Content.Headers.ContentType?.MediaType);
                var b64 = Convert.ToBase64String(bytes);
                absoluteToReplacement[resourceUri.AbsoluteUri] = $"url(\"data:{mime};base64,{b64}\")";
            }

            foreach (var m in matches.OrderByDescending(x => x.Index))
            {
                var raw = m.Groups["quoted"].Success ? m.Groups["quoted"].Value : m.Groups["unquoted"].Value;
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (!TryResolveCssResourceUrl(raw.Trim(), stylesheetUri, out var resourceUri)) continue;
                if (!absoluteToReplacement.TryGetValue(resourceUri!.AbsoluteUri, out var replacement)) continue;
                css = css.Substring(0, m.Index) + replacement + css.Substring(m.Index + m.Length);
            }

            css = "/* FlowMy: url() trong CSS đã nhúng offline (data URI). */\n" + css;

            var targetPath = GetLocalFilePath(fileName);
            await File.WriteAllTextAsync(targetPath, css, cancellationToken);
            progress?.Report($"✓ Hoàn tất stylesheet → {fileName}");
            return fileName;
        }

        /// <inheritdoc cref="DownloadStylesheetWithEmbeddedResourcesAsync"/>
        public static Task<string> DownloadGoogleFontsCssWithEmbeddedFontsAsync(
            string cssUrl,
            string fileName,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
            => DownloadStylesheetWithEmbeddedResourcesAsync(cssUrl, fileName, progress, cancellationToken);

        private static bool TryResolveCssResourceUrl(string raw, Uri stylesheetUri, out Uri? absolute)
        {
            absolute = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            raw = raw.Trim();
            if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
            if (raw.StartsWith('#')) return false;
            try
            {
                if (raw.StartsWith("//", StringComparison.Ordinal))
                {
                    absolute = new Uri("https:" + raw);
                    return true;
                }
                if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    absolute = new Uri(raw);
                    return true;
                }
                absolute = new Uri(stylesheetUri, raw);
                return true;
            }
            catch
            {
                absolute = null;
                return false;
            }
        }

        private static string GuessMimeTypeForUrl(Uri resourceUri, string? headerMime)
        {
            if (!string.IsNullOrWhiteSpace(headerMime) &&
                !string.Equals(headerMime, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return headerMime!;
            var path = resourceUri.AbsolutePath.ToLowerInvariant();
            if (path.Contains(".woff2")) return "font/woff2";
            if (path.Contains(".woff")) return "font/woff";
            if (path.Contains(".ttf")) return "font/ttf";
            if (path.Contains(".otf")) return "font/otf";
            if (path.EndsWith(".svg")) return "image/svg+xml";
            if (path.EndsWith(".png")) return "image/png";
            if (path.EndsWith(".gif")) return "image/gif";
            if (path.EndsWith(".webp")) return "image/webp";
            if (path.EndsWith(".jpg") || path.EndsWith(".jpeg")) return "image/jpeg";
            return "application/octet-stream";
        }

        /// <summary>
        /// Copy file từ đường dẫn local vào thư mục assets.
        /// Trả về tên file đã copy (tên gốc giữ nguyên).
        /// </summary>
        public static string CopyAssetFromFile(string sourcePath)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Không tìm thấy file.", sourcePath);

            var fileName = SanitizeFileName(Path.GetFileName(sourcePath));
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "asset_" + DateTime.Now.Ticks;

            var targetPath = GetLocalFilePath(fileName);
            File.Copy(sourcePath, targetPath, overwrite: true);
            return fileName;
        }

        /// <summary>
        /// Đọc nội dung text của file asset để inline vào HTML.
        /// Trả về string rỗng nếu file không tồn tại.
        /// </summary>
        public static string GetInlineContent(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;
            var path = GetLocalFilePath(fileName);
            if (!File.Exists(path)) return string.Empty;
            try { return File.ReadAllText(path); }
            catch { return string.Empty; }
        }

        /// <summary>Đoán tên file từ URL (lấy segment cuối).</summary>
        public static string GuessFileNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var lastSegment = uri.Segments[^1].TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(lastSegment) && lastSegment.Contains('.'))
                    return lastSegment;
            }
            catch { }
            return "asset_" + DateTime.Now.Ticks + ".js";
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name.Trim();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024.0:F2} MB";
        }
    }
}
