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
using Tesseract;

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

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
            IntPtr hdcSource, int xSrc, int ySrc, int rop);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const int MDT_DEFAULT = 0;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int SRCCOPY = 0x00CC0020;
        private const uint PW_RENDERFULLCONTENT = 0x00000002;

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
                    case ImageSourceMode.ManualRegion:
                        image = await GetImageFromManualRegion(textScan, env);
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

        private static async Task<BitmapImage?> GetImageFromManualRegion(TextScanNode textScan, NodeExecutionEnvironment env)
        {
            // ManualRegion mode: chụp theo vùng đã lưu trong node, không đọc từ input node
            int x = textScan.CaptureX, y = textScan.CaptureY, w = textScan.CaptureWidth, h = textScan.CaptureHeight;

            if (w <= 0 || h <= 0)
            {
                System.Diagnostics.Debug.WriteLine("[TextScanNodeExecutor] ManualRegion: vùng chụp chưa được cấu hình (width hoặc height = 0)");
                return null;
            }

            BitmapImage? bitmap = null;

            // Nếu dùng background mode và có target window → chụp trực tiếp từ window
            if (textScan.UseBackgroundMode && !string.IsNullOrWhiteSpace(textScan.TargetProcessName))
            {
                var windows = FlowMy.Helpers.WindowHelper.GetActiveWindows();
                var match = windows.FirstOrDefault(wnd =>
                    wnd.ProcessName == textScan.TargetProcessName && wnd.Title == textScan.TargetWindowTitle)
                    ?? windows.FirstOrDefault(wnd => wnd.ProcessName == textScan.TargetProcessName);

                if (match != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[TextScanNode ManualRegion] Background capture from window: {match.Title}");
                    
                    // Chụp vùng từ window (x,y là offset trong window)
                    bitmap = await Task.Run(() => CaptureWindow(match.Handle, x, y, w, h));
                }
            }
            else
            {
                // Mode bình thường: activate window rồi chụp màn hình
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

                // Chụp màn hình theo vùng đã lưu
                bitmap = await Task.Run(() => CaptureScreen(x, y, w, h));
            }

            return bitmap;
        }

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

            BitmapImage? bitmap = null;

            // Nếu dùng background mode và có target window → chụp trực tiếp từ window
            if (textScan.UseBackgroundMode && !string.IsNullOrWhiteSpace(textScan.TargetProcessName))
            {
                var windows = FlowMy.Helpers.WindowHelper.GetActiveWindows();
                var match = windows.FirstOrDefault(wnd =>
                    wnd.ProcessName == textScan.TargetProcessName && wnd.Title == textScan.TargetWindowTitle)
                    ?? windows.FirstOrDefault(wnd => wnd.ProcessName == textScan.TargetProcessName);

                if (match != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[TextScanNode ScreenCapture] Background capture from window: {match.Title}");
                    
                    // Chụp vùng từ window (x,y là offset trong window)
                    bitmap = await Task.Run(() => CaptureWindow(match.Handle, x, y, w, h));
                }
            }
            else
            {
                // Mode bình thường: activate window rồi chụp màn hình
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
                bitmap = await Task.Run(() => CaptureScreen(x, y, w, h));
            }

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
                if (textScan.OcrEngineMode == OcrEngineMode.Tesseract)
                {
                    return await PerformTesseractOcr(image, textScan);
                }

                // WindowsOcr và OpenCvMlNet đã bị xóa vì không hỗ trợ trong .NET Framework WPF
                return new OcrResult { Text = string.Empty, Lines = string.Empty, Words = new Dictionary<string, string>() };
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] OCR Error: {ex.Message}");
                return new OcrResult { Text = string.Empty, Lines = string.Empty, Words = new Dictionary<string, string>() };
            }
        }

        private static async Task<OcrResult> PerformTesseractOcr(BitmapImage image, TextScanNode textScan)
        {
            try
            {
                // Convert BitmapImage to byte array
                byte[] imageBytes;
                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(ms);
                    imageBytes = ms.ToArray();
                }

                // Determine tessdata path
                string tessdataPath = string.IsNullOrWhiteSpace(textScan.TessdataPath)
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata")
                    : textScan.TessdataPath;

                // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] Tessdata path: {tessdataPath}");

                // Ensure tessdata directory exists
                if (!Directory.Exists(tessdataPath))
                {
                    // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] Tessdata directory not found: {tessdataPath}");
                    return new OcrResult { Text = string.Empty, Lines = string.Empty, Words = new Dictionary<string, string>() };
                }

                // List available .traineddata files
                // var trainedDataFiles = Directory.GetFiles(tessdataPath, "*.traineddata");
                // // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] Found {trainedDataFiles.Length} .traineddata files in tessdata directory");
                // foreach (var file in trainedDataFiles)
                // {
                //     // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] - {Path.GetFileNameWithoutExtension(file)}");
                // }

                // Map language code to Tesseract format
                string langCode;
                if (textScan.AutoDetectLanguage)
                {
                    // Use selected languages for auto-detection
                    if (textScan.SelectedLanguages != null && textScan.SelectedLanguages.Count > 0)
                    {
                        langCode = string.Join("+", textScan.SelectedLanguages);
                    }
                    else
                    {
                        // Fallback to default languages
                        langCode = "eng+vie";
                    }
                    // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] Auto-detect language enabled, using: {langCode}");
                }
                else
                {
                    langCode = textScan.OcrLanguage?.ToLowerInvariant() ?? "eng";
                    // Common mappings
                    langCode = langCode switch
                    {
                        "en" => "eng",
                        "vi" => "vie",
                        "ja" => "jpn",
                        "ko" => "kor",
                        "zh-hans" => "chi_sim",
                        "zh-hant" => "chi_tra",
                        _ => langCode
                    };
                    // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] Language code: {langCode}");
                }

                // Check if the language file(s) exist
                if (textScan.AutoDetectLanguage)
                {
                    // Check all language files for auto-detection
                    var langCodes = langCode.Split('+');
                    var missingFiles = new List<string>();
                    foreach (var code in langCodes)
                    {
                        string langFile = Path.Combine(tessdataPath, $"{code}.traineddata");
                        if (!File.Exists(langFile))
                        {
                            missingFiles.Add($"{code}.traineddata");
                        }
                    }
                    if (missingFiles.Count > 0)
                    {
                        // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] Missing language files: {string.Join(", ", missingFiles)}");
                        return new OcrResult { Text = string.Empty, Lines = string.Empty, Words = new Dictionary<string, string>() };
                    }
                }
                else
                {
                    // Check single language file
                    string langFile = Path.Combine(tessdataPath, $"{langCode}.traineddata");
                    if (!File.Exists(langFile))
                    {
                        // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] Language file not found: {langFile}");
                        return new OcrResult { Text = string.Empty, Lines = string.Empty, Words = new Dictionary<string, string>() };
                    }
                }

                // Convert TesseractPageSegMode to Tesseract.PageSegMode
                var pageSegMode = textScan.TesseractPageSegMode switch
                {
                    TesseractPageSegMode.OsdOnly => PageSegMode.OsdOnly,
                    TesseractPageSegMode.AutoOsd => PageSegMode.AutoOsd,
                    TesseractPageSegMode.Auto => PageSegMode.Auto,
                    TesseractPageSegMode.SingleColumn => PageSegMode.SingleColumn,
                    TesseractPageSegMode.SingleBlockVertText => PageSegMode.SingleBlockVertText,
                    TesseractPageSegMode.SingleBlock => PageSegMode.SingleBlock,
                    TesseractPageSegMode.SingleLine => PageSegMode.SingleLine,
                    TesseractPageSegMode.SingleWord => PageSegMode.SingleWord,
                    TesseractPageSegMode.CircleWord => PageSegMode.CircleWord,
                    TesseractPageSegMode.SingleChar => PageSegMode.SingleChar,
                    TesseractPageSegMode.SparseText => PageSegMode.SparseText,
                    TesseractPageSegMode.SparseTextOsd => PageSegMode.SparseTextOsd,
                    TesseractPageSegMode.RawLine => PageSegMode.RawLine,
                    _ => PageSegMode.Auto
                };

                // Convert TesseractEngineMode to Tesseract.EngineMode
                var engineMode = textScan.TesseractEngineMode switch
                {
                    TesseractEngineMode.Lstm => EngineMode.LstmOnly,
                    TesseractEngineMode.Default => EngineMode.Default,
                    _ => EngineMode.Default
                };

                // Run OCR on background thread
                return await Task.Run(() =>
                {
                    try
                    {
                        using (var engine = new TesseractEngine(tessdataPath, langCode, engineMode))
                        {
                            engine.SetVariable("tessedit_pageseg_mode", ((int)pageSegMode).ToString());

                            using (var pix = Pix.LoadFromMemory(imageBytes))
                            {
                                using (var page = engine.Process(pix))
                                {
                                    var text = page.GetText();
                                    var lines = new StringBuilder();
                                    var words = new Dictionary<string, string>();

                                    using (var iter = page.GetIterator())
                                    {
                                        iter.Begin();
                                        do
                                        {
                                            if (lines.Length > 0)
                                                lines.AppendLine();
                                            lines.Append(iter.GetText(PageIteratorLevel.TextLine));

                                            // Get word-level information
                                            using (var wordIter = page.GetIterator())
                                            {
                                                wordIter.Begin();
                                                do
                                                {
                                                    var wordText = wordIter.GetText(PageIteratorLevel.Word);
                                                    if (!string.IsNullOrWhiteSpace(wordText))
                                                    {
                                                        if (wordIter.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
                                                        {
                                                            var boundsStr = $"[{rect.X1},{rect.Y1},{rect.X2},{rect.Y2}]";
                                                            words[wordText.Trim()] = boundsStr;
                                                        }
                                                    }
                                                } while (wordIter.Next(PageIteratorLevel.Word));
                                            }
                                        } while (iter.Next(PageIteratorLevel.TextLine));
                                    }

                                    return new OcrResult
                                    {
                                        Text = text?.Trim() ?? string.Empty,
                                        Lines = lines.ToString(),
                                        Words = words
                                    };
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] Tesseract Error: {ex.Message}");
                        // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] StackTrace: {ex.StackTrace}");
                        return new OcrResult { Text = string.Empty, Lines = string.Empty, Words = new Dictionary<string, string>() };
                    }
                });
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] Tesseract OCR Error: {ex.Message}");
                // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] StackTrace: {ex.StackTrace}");
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

        /// <summary>
        /// Chụp window cụ thể không cần active (dùng PrintWindow API)
        /// PrintWindow tốt hơn BitBlt vì nó yêu cầu window tự render vào DC, tránh vấn đề ảnh đen.
        /// </summary>
        private static BitmapImage? CaptureWindow(IntPtr hWnd, int x, int y, int width, int height)
        {
            if (hWnd == IntPtr.Zero || width <= 0 || height <= 0) return null;

            // Lấy kích thước toàn bộ window
            if (!GetWindowRect(hWnd, out RECT rect))
            {
                System.Diagnostics.Debug.WriteLine("[CaptureWindow] Failed to get window rect");
                return null;
            }

            int fullWidth = rect.Right - rect.Left;
            int fullHeight = rect.Bottom - rect.Top;

            if (fullWidth <= 0 || fullHeight <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"[CaptureWindow] Invalid window size: {fullWidth}x{fullHeight}");
                return null;
            }

            IntPtr hdcScreen = IntPtr.Zero;
            IntPtr hdcMemory = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;

            try
            {
                // Tạo DC và bitmap cho toàn bộ window
                hdcScreen = GetDC(IntPtr.Zero);
                hdcMemory = CreateCompatibleDC(hdcScreen);
                hBitmap = CreateCompatibleBitmap(hdcScreen, fullWidth, fullHeight);
                hOld = SelectObject(hdcMemory, hBitmap);

                // PrintWindow: yêu cầu window tự render vào DC
                // PW_RENDERFULLCONTENT (0x2) = render cả client area lẫn non-client area
                bool success = PrintWindow(hWnd, hdcMemory, PW_RENDERFULLCONTENT);

                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine("[CaptureWindow] PrintWindow failed, trying without PW_RENDERFULLCONTENT");
                    // Thử lại không có flag
                    success = PrintWindow(hWnd, hdcMemory, 0);
                }

                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine("[CaptureWindow] PrintWindow failed completely");
                    return null;
                }

                // Convert GDI bitmap → System.Drawing.Bitmap
                var fullBitmap = Image.FromHbitmap(hBitmap);

                // Nếu cần crop vùng nhỏ hơn
                Bitmap finalBitmap;
                if (x != 0 || y != 0 || width != fullWidth || height != fullHeight)
                {
                    // Đảm bảo không crop quá boundary
                    int cropX = Math.Max(0, Math.Min(x, fullWidth - 1));
                    int cropY = Math.Max(0, Math.Min(y, fullHeight - 1));
                    int cropW = Math.Min(width, fullWidth - cropX);
                    int cropH = Math.Min(height, fullHeight - cropY);

                    if (cropW > 0 && cropH > 0)
                    {
                        var cropRect = new Rectangle(cropX, cropY, cropW, cropH);
                        finalBitmap = fullBitmap.Clone(cropRect, fullBitmap.PixelFormat);
                        fullBitmap.Dispose();
                    }
                    else
                    {
                        finalBitmap = fullBitmap;
                    }
                }
                else
                {
                    finalBitmap = fullBitmap;
                }

                // Convert → BitmapImage
                using var ms = new MemoryStream();
                finalBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = ms;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();

                finalBitmap.Dispose();
                return img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CaptureWindow] Error: {ex.Message}");
                return null;
            }
            finally
            {
                if (hOld != IntPtr.Zero) SelectObject(hdcMemory, hOld);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (hdcMemory != IntPtr.Zero) DeleteDC(hdcMemory);
                if (hdcScreen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hdcScreen);
            }
        }

        private static BitmapImage? CaptureScreen(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0) return null;
            try
            {
                using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bmp);
                graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);

                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
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
                // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] CaptureScreen: {ex.Message}");
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
                // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] DownloadImage: {ex.Message}");
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
                // System.Diagnostics.Debug.WriteLine($"[TextScanNodeExecutor] DecodeBase64: {ex.Message}");
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
