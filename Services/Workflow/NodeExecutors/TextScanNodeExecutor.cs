using FlowMy.Models;
using FlowMy.Models.Nodes;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho TextScanNode (OCR).
    ///
    /// Chế độ ScreenCapture:
    ///   - Chụp màn hình theo toạ độ (có thể từ node nguồn hoặc đã lưu)
    ///   - Thực hiện OCR trên ảnh chụp
    ///
    /// Chế độ FromNode:
    ///   - Lấy ảnh từ output node khác (base64 hoặc BitmapImage)
    ///   - Thực hiện OCR
    ///
    /// Chế độ PathOrUrl:
    ///   - Load ảnh từ file/URL
    ///   - Thực hiện OCR
    ///
    /// Chế độ Base64:
    ///   - Decode base64 string thành ảnh
    ///   - Thực hiện OCR
    /// </summary>
    internal sealed class TextScanNodeExecutor : INodeExecutor
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, uint dpiType, out uint dpiX, out uint dpiY);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const int MDT_DEFAULT = 0;
        private const int MDT_EFFECTIVE_DPI = 0;

        public bool CanExecute(WorkflowNode node) => node is TextScanNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var textScan = (TextScanNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                BitmapImage? image = null;

                // Lấy ảnh theo chế độ nguồn
                switch (textScan.ImageSourceMode)
                {
                    case ImageSourceMode.ScreenCapture:
                        image = await GetImageFromScreenCapture(textScan, env);
                        break;
                    case ImageSourceMode.FromNode:
                        image = await GetImageFromNode(textScan, env);
                        break;
                    case ImageSourceMode.PathOrUrl:
                        image = await GetImageFromPathOrUrl(textScan, env);
                        break;
                    case ImageSourceMode.Base64:
                        image = GetImageFromBase64(textScan);
                        break;
                }

                if (image == null)
                {
                    env.OnNodeFailed?.Invoke(textScan, "Không thể tải ảnh để OCR");
                    return;
                }

                // Thực hiện OCR
                var ocrResult = await PerformOcr(image, textScan);

                // Cập nhật kết quả vào node
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    textScan.CapturedImage = image;
                    textScan.ExtractedText = ocrResult.Text;
                    textScan.ExtractedTextLines = ocrResult.Lines;
                    textScan.ExtractedWords = ocrResult.Words;
                });

                // Publish outputs
                PublishOutputs(textScan, image, ocrResult, env);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(textScan, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(textScan, sw.Elapsed);
            await env.TraverseOutputsAsync(textScan);
        }

        // ── Lấy ảnh theo các chế độ ─────────────────────────────────────────────

        private static async Task<BitmapImage?> GetImageFromScreenCapture(TextScanNode textScan, NodeExecutionEnvironment env)
        {
            int x = textScan.CaptureX, y = textScan.CaptureY, w = textScan.CaptureWidth, h = textScan.CaptureHeight;
            bool shouldCapture = false;

            // Đọc toạ độ từ node nguồn nếu được cấu hình
            if (!string.IsNullOrWhiteSpace(textScan.CoordSourceNodeId))
            {
                var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                    env.Connections,
                    textScan.CoordSourceNodeId,
                    textScan.CoordSourceOutputKey,
                    env);

                if (!string.IsNullOrWhiteSpace(raw) && raw != "—")
                {
                    var parsed = ScreenCaptureNodeExecutor.TryParseRect(raw, textScan.CoordSourceOutputKey, textScan.CaptureWidth, textScan.CaptureHeight);
                    if (parsed.HasValue)
                    {
                        (x, y, w, h) = parsed.Value;
                        shouldCapture = true;
                    }
                }
            }

            // Nếu không có input toạ độ và đã có ảnh → dùng lại
            if (!shouldCapture && textScan.CapturedImage != null)
                return textScan.CapturedImage;

            if (w <= 0 || h <= 0) return null;

            // Đưa app lên trước nếu được cấu hình
            if (!string.IsNullOrWhiteSpace(textScan.TargetProcessName))
            {
                var windows = FlowMy.Helpers.WindowHelper.GetActiveWindows();
                var match = windows.FirstOrDefault(wnd =>
                    wnd.ProcessName == textScan.TargetProcessName && wnd.Title == textScan.TargetWindowTitle)
                    ?? windows.FirstOrDefault(wnd => wnd.ProcessName == textScan.TargetProcessName);

                if (match != null)
                {
                    FlowMy.Helpers.WindowHelper.BringToFront(match.Handle);
                    await Task.Delay(150, env.CancellationToken);
                }
            }

            // Chụp màn hình
            var bitmap = await Task.Run(() => CaptureScreen(x, y, w, h));
            if (bitmap == null) return null;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                textScan.CaptureX = x;
                textScan.CaptureY = y;
                textScan.CaptureWidth = w;
                textScan.CaptureHeight = h;
            });

            return bitmap;
        }

        private static async Task<BitmapImage?> GetImageFromNode(TextScanNode textScan, NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(textScan.ImageSourceNodeId))
                return null;

            var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                env.Connections,
                textScan.ImageSourceNodeId,
                textScan.ImageSourceOutputKey,
                env);

            if (string.IsNullOrWhiteSpace(raw) || raw == "—")
                return null;

            // Kiểm tra xem raw có phải base64 không
            if (IsBase64String(raw))
            {
                return DecodeBase64ToBitmap(raw);
            }

            // Nếu không phải base64, thử load như đường dẫn file
            if (File.Exists(raw))
            {
                return await Task.Run(() => LoadBitmapFromFile(raw));
            }

            return null;
        }

        private static async Task<BitmapImage?> GetImageFromPathOrUrl(TextScanNode textScan, NodeExecutionEnvironment env)
        {
            string path = textScan.ImagePath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return null;

            BitmapImage? bitmap = null;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                bitmap = await DownloadImageAsync(path);
            }
            else if (File.Exists(path))
            {
                bitmap = await Task.Run(() => LoadBitmapFromFile(path));
            }

            return bitmap;
        }

        private static BitmapImage? GetImageFromBase64(TextScanNode textScan)
        {
            string base64 = textScan.Base64Image?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(base64)) return null;

            return DecodeBase64ToBitmap(base64);
        }

        // ── OCR ─────────────────────────────────────────────────────────────────

        private static async Task<OcrResult> PerformOcr(BitmapImage image, TextScanNode textScan)
        {
            try
            {
                // Placeholder OCR implementation
                // Windows.Media.Ocr is not available in .NET Framework WPF projects
                // To implement actual OCR, consider using:
                // - Tesseract OCR via Tesseract.Net wrapper
                // - OpenCV + ML.NET / ONNX Runtime (as mentioned in the original spec)
                // - Azure Computer Vision API
                // - Google Cloud Vision API

                // For now, return empty results
                await Task.Delay(10); // Simulate async work

                System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] OCR Mode: {textScan.OcrEngineMode}, Language: {textScan.OcrLanguage}, AutoDetect: {textScan.AutoDetectLanguage}");
                System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] OCR is not yet implemented. Please integrate Tesseract or other OCR library.");

                return new OcrResult
                {
                    Text = string.Empty,
                    Lines = string.Empty,
                    Words = new Dictionary<string, string>()
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] OCR Error: {ex.Message}");
                return new OcrResult { Text = string.Empty, Lines = string.Empty, Words = new Dictionary<string, string>() };
            }
        }

        // ── Publish outputs ───────────────────────────────────────────────────

        private static void PublishOutputs(TextScanNode textScan, BitmapImage image, OcrResult ocrResult, NodeExecutionEnvironment env)
        {
            if (textScan.DynamicOutputs == null) return;

            // Encode PNG nếu cần
            bool needBase64 = !textScan.SkipOutputs.Contains("imageBase64");
            byte[]? pngBytes = needBase64 ? TryEncodePng(image) : null;

            foreach (var port in textScan.DynamicOutputs)
            {
                var key = port.Key ?? string.Empty;
                if (textScan.SkipOutputs.Contains(key))
                {
                    port.UserValueOverride = string.Empty;
                    continue;
                }

                port.UserValueOverride = key.ToLowerInvariant() switch
                {
                    "extractedtext" => ocrResult.Text,
                    "extractedtextlines" => ocrResult.Lines,
                    "imagewidth" => image.PixelWidth.ToString(),
                    "imageheight" => image.PixelHeight.ToString(),
                    "imagebase64" => pngBytes != null ? Convert.ToBase64String(pngBytes) : string.Empty,
                    "capturex" => textScan.CaptureX.ToString(),
                    "capturey" => textScan.CaptureY.ToString(),
                    "capturewidth" => textScan.CaptureWidth.ToString(),
                    "captureheight" => textScan.CaptureHeight.ToString(),
                    "ocrlanguage" => textScan.OcrLanguage,
                    "wordcount" => ocrResult.Words.Count.ToString(),
                    _ => string.Empty
                };
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static BitmapImage? CaptureScreen(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0) return null;
            try
            {
                using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bmp);
                graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);

                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = ms;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] CaptureScreen: {ex.Message}");
                return null;
            }
        }

        private static BitmapImage? LoadBitmapFromFile(string path)
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(path, UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { return null; }
        }

        private static async Task<BitmapImage?> DownloadImageAsync(string url)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);

                using var ms = new MemoryStream(bytes);
                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = ms;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] DownloadImage: {ex.Message}");
                return null;
            }
        }

        private static BitmapImage? DecodeBase64ToBitmap(string base64)
        {
            try
            {
                // Clean base64 string (remove data URL prefix if present)
                if (base64.Contains(","))
                    base64 = base64.Substring(base64.IndexOf(",") + 1);

                var bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = ms;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] DecodeBase64: {ex.Message}");
                return null;
            }
        }

        private static bool IsBase64String(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.Contains(","))
                s = s.Substring(s.IndexOf(",") + 1);
            
            // Basic check: length should be multiple of 4 and only contain valid base64 chars
            if (s.Length % 4 != 0) return false;
            return s.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=');
        }

        private static byte[]? TryEncodePng(BitmapSource? source)
        {
            if (source == null) return null;
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                return ms.ToArray();
            }
            catch { return null; }
        }

        private class OcrResult
        {
            public string Text { get; set; } = string.Empty;
            public string Lines { get; set; } = string.Empty;
            public Dictionary<string, string> Words { get; set; } = new();
        }
    }
}
