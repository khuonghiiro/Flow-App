using FlowMy.Models;
using FlowMy.Models.Nodes;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho ScreenCaptureNode.
    ///
    /// Chế độ ScreenCapture (IsScreenCaptureMode = true):
    ///   - Nếu CoordSourceNodeId được cấu hình → đọc toạ độ từ output node đó.
    ///     Format value hỗ trợ:
    ///       "x,y,w,h"          → ví dụ "100,200,640,480"
    ///       "x y w h"          → ví dụ "100 200 640 480"
    ///       "(x, y, w, h)"     → ví dụ "(100, 200, 640, 480)"
    ///       Chỉ "x,y" (2 số)   → dùng CaptureWidth/CaptureHeight từ node
    ///       Chỉ "w,h" nếu key chứa "size"/"wh"/"width"
    ///   - Nếu không có CoordSourceNodeId → dùng CaptureX/Y/Width/Height đã lưu trong node.
    ///   - Chụp màn hình vùng đó, lưu vào node.CapturedImage và publish output keys.
    ///
    /// Chế độ PathOrUrl (IsScreenCaptureMode = false):
    ///   - Đọc path/URL từ PathSourceNodeId+Key hoặc node.ImagePath.
    ///   - Load ảnh từ file/URL, lưu vào node.CapturedImage và publish output keys.
    /// </summary>
    internal sealed class ScreenCaptureNodeExecutor : INodeExecutor
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        public bool CanExecute(WorkflowNode node) => node is ScreenCaptureNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var cap = (ScreenCaptureNode)node;
            var sw  = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (cap.CaptureMode == ScreenCaptureMode.ScreenCapture)
                    await ExecuteScreenCaptureMode(cap, env);
                else
                    await ExecutePathOrUrlMode(cap, env);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(cap, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(cap, sw.Elapsed);
            await env.TraverseOutputsAsync(cap);
        }

        // ── Chế độ chụp màn hình ─────────────────────────────────────────────

        private static async Task ExecuteScreenCaptureMode(ScreenCaptureNode cap, NodeExecutionEnvironment env)
        {
            int x = cap.CaptureX, y = cap.CaptureY, w = cap.CaptureWidth, h = cap.CaptureHeight;
            bool shouldCapture = false;

            // Đọc toạ độ từ node nguồn nếu được cấu hình
            if (!string.IsNullOrWhiteSpace(cap.CoordSourceNodeId))
            {
                var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                    env.Connections,
                    cap.CoordSourceNodeId,
                    cap.CoordSourceOutputKey,
                    env);

                System.Diagnostics.Debug.WriteLine($"[ScreenCaptureNode {cap.Title}] Received raw coordinates: '{raw}' from node {cap.CoordSourceNodeId}, key {cap.CoordSourceOutputKey}");

                if (!string.IsNullOrWhiteSpace(raw) && raw != "—")
                {
                    var parsed = TryParseRect(raw, cap.CoordSourceOutputKey, cap.CaptureWidth, cap.CaptureHeight);
                    if (parsed.HasValue)
                    {
                        (x, y, w, h) = parsed.Value;
                        shouldCapture = true; // Có input toạ độ → cần chụp lại
                        System.Diagnostics.Debug.WriteLine($"[ScreenCaptureNode {cap.Title}] Parsed coordinates: x={x}, y={y}, w={w}, h={h}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ScreenCaptureNode {cap.Title}] Failed to parse coordinates from: '{raw}'");
                    }
                }
            }

            // Nếu không có input toạ độ và đã có ảnh → giữ nguyên ảnh, không chụp lại
            if (!shouldCapture && cap.CapturedImage != null)
            {
                PublishOutputs(cap, cap.CapturedImage, env);
                return;
            }

            if (w <= 0 || h <= 0) return;

            // Đưa app lên trước nếu được cấu hình
            if (!string.IsNullOrWhiteSpace(cap.TargetProcessName))
            {
                var windows = FlowMy.Helpers.WindowHelper.GetActiveWindows();
                var match = windows.FirstOrDefault(wnd =>
                    wnd.ProcessName == cap.TargetProcessName && wnd.Title == cap.TargetWindowTitle)
                    ?? windows.FirstOrDefault(wnd => wnd.ProcessName == cap.TargetProcessName);

                if (match != null)
                {
                    FlowMy.Helpers.WindowHelper.BringToFront(match.Handle);
                    // Chờ ngắn để window kịp render lên trước
                    await Task.Delay(150, env.CancellationToken);
                }
            }

            // Chụp màn hình (chạy trên background thread để không block UI)
            var bitmap = await Task.Run(() => CaptureScreen(x, y, w, h));
            if (bitmap == null) return;

            // Cập nhật node trên UI thread
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                cap.CaptureX      = x;
                cap.CaptureY      = y;
                cap.CaptureWidth  = w;
                cap.CaptureHeight = h;
                cap.CapturedImage = bitmap;
            });

            PublishOutputs(cap, bitmap, env);
        }

        // ── Chế độ Path / URL ────────────────────────────────────────────────

        private static async Task ExecutePathOrUrlMode(ScreenCaptureNode cap, NodeExecutionEnvironment env)
        {
            // Ưu tiên đọc path từ node nguồn
            string path = string.Empty;
            if (!string.IsNullOrWhiteSpace(cap.PathSourceNodeId))
            {
                var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                    env.Connections,
                    cap.PathSourceNodeId,
                    cap.PathSourceOutputKey,
                    env);
                if (!string.IsNullOrWhiteSpace(raw) && raw != "—")
                    path = raw.Trim();
            }

            if (string.IsNullOrWhiteSpace(path))
                path = cap.ImagePath?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path)) return;

            BitmapImage? bitmap = null;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // URL online → download
                bitmap = await DownloadImageAsync(path);
            }
            else if (File.Exists(path))
            {
                bitmap = await Task.Run(() => LoadBitmapFromFile(path));
            }

            if (bitmap == null) return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                cap.ImagePath     = path;
                cap.CaptureX      = 0;
                cap.CaptureY      = 0;
                cap.CaptureWidth  = bitmap.PixelWidth;
                cap.CaptureHeight = bitmap.PixelHeight;
                cap.CapturedImage = bitmap;
            });

            PublishOutputs(cap, bitmap, env);
        }

        // ── Publish output keys ───────────────────────────────────────────────

        private static void PublishOutputs(ScreenCaptureNode cap, BitmapImage bitmap, NodeExecutionEnvironment env)
        {
            if (cap.DynamicOutputs == null) return;

            // Chỉ encode PNG khi có ít nhất 1 key cần bytes (imageBase64 hoặc imageSizeBytes)
            // và key đó KHÔNG bị skip → tránh encode ảnh lớn vô ích
            bool needBase64   = !cap.SkipOutputs.Contains("imageBase64");
            bool needSizeBytes = !cap.SkipOutputs.Contains("imageSizeBytes");
            byte[]? pngBytes = (needBase64 || needSizeBytes) ? TryEncodePng(bitmap) : null;

            foreach (var port in cap.DynamicOutputs)
            {
                var key = port.Key ?? string.Empty;
                if (cap.SkipOutputs.Contains(key))
                {
                    // Clear giá trị cũ để downstream không đọc nhầm
                    port.UserValueOverride = string.Empty;
                    continue;
                }

                port.UserValueOverride = key.ToLowerInvariant() switch
                {
                    "capturex"       => cap.CaptureX.ToString(),
                    "capturey"       => cap.CaptureY.ToString(),
                    "capturewidth"   => cap.CaptureWidth.ToString(),
                    "captureheight"  => cap.CaptureHeight.ToString(),
                    "capturerect"    => $"{cap.CaptureX},{cap.CaptureY},{cap.CaptureWidth},{cap.CaptureHeight}",
                    "imagewidth"     => bitmap.PixelWidth.ToString(),
                    "imageheight"    => bitmap.PixelHeight.ToString(),
                    "imagesizebytes" => pngBytes != null ? pngBytes.Length.ToString() : string.Empty,
                    // Trả về full base64 để downstream dùng được; UI dialog tự truncate khi hiển thị
                    "imagebase64"    => pngBytes != null ? Convert.ToBase64String(pngBytes) : string.Empty,
                    _                => string.Empty
                };
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Parse chuỗi toạ độ từ output node nguồn.
        /// Hỗ trợ:
        ///   "x,y,w,h"       → 4 số phân cách bởi dấu phẩy/khoảng trắng/dấu chấm phẩy
        ///   "(x, y, w, h)"  → có ngoặc
        ///   "x,y"           → 2 số → dùng w/h từ fallback
        ///   Số đơn          → tuỳ key (captureX/Y/Width/Height)
        /// </summary>
        internal static (int x, int y, int w, int h)? TryParseRect(
            string raw, string? outputKey, int fallbackW, int fallbackH)
        {
            raw = raw.Trim().Trim('(', ')');

            // Tách theo dấu phẩy, chấm phẩy, khoảng trắng
            var parts = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Loại bỏ prefix chữ (X:, Y:, W:, H:)
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].TrimStart('X', 'x', 'Y', 'y', 'W', 'w', 'H', 'h', ':', ' ');

            if (parts.Length >= 4)
            {
                if (TryParseInt(parts[0], out var px) && TryParseInt(parts[1], out var py) &&
                    TryParseInt(parts[2], out var pw) && TryParseInt(parts[3], out var ph))
                    return (px, py, pw, ph);
            }

            if (parts.Length == 2)
            {
                if (TryParseInt(parts[0], out var a) && TryParseInt(parts[1], out var b))
                {
                    var key = (outputKey ?? string.Empty).ToLowerInvariant();
                    // Nếu key gợi ý là size/wh → (0,0,a,b); ngược lại → (a,b, fallback)
                    if (key.Contains("size") || key.Contains("wh") || key.Contains("width"))
                        return (0, 0, a, b);
                    return (a, b, fallbackW, fallbackH);
                }
            }

            if (parts.Length == 1 && TryParseInt(parts[0], out var single))
            {
                var key = (outputKey ?? string.Empty).ToLowerInvariant();
                if (key.Contains("y"))   return (0, single, fallbackW, fallbackH);
                if (key.Contains("w"))   return (0, 0, single, fallbackH);
                if (key.Contains("h"))   return (0, 0, fallbackW, single);
                return (single, 0, fallbackW, fallbackH);
            }

            return null;
        }

        private static bool TryParseInt(string s, out int result)
            => int.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);

        private static BitmapImage? CaptureScreen(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0) return null;
            try
            {
                // Xử lý DPI scaling: convert toạ độ logic sang physical
                using var g = Graphics.FromHwnd(IntPtr.Zero);
                var dpiX = g.DpiX;
                var dpiY = g.DpiY;
                // Standard DPI là 96
                var scaleX = dpiX / 96.0;
                var scaleY = dpiY / 96.0;

                var physicalX = (int)(x * scaleX);
                var physicalY = (int)(y * scaleY);
                var physicalW = (int)(width * scaleX);
                var physicalH = (int)(height * scaleY);

                System.Diagnostics.Debug.WriteLine($"[CaptureScreen] DPI: {dpiX}x{dpiY}, Scale: {scaleX:F2}x{scaleY:F2}");
                System.Diagnostics.Debug.WriteLine($"[CaptureScreen] Logical: ({x},{y},{width},{height}) -> Physical: ({physicalX},{physicalY},{physicalW},{physicalH})");

                using var bmp = new Bitmap(physicalW, physicalH, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bmp);
                graphics.CopyFromScreen(physicalX, physicalY, 0, 0, new System.Drawing.Size(physicalW, physicalH), CopyPixelOperation.SourceCopy);

                // Scale lại về kích thước gốc (logic) để đảm bảo kích thước ảnh đúng như mong đợi
                using var scaledBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using var scaledGraphics = Graphics.FromImage(scaledBmp);
                scaledGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                scaledGraphics.DrawImage(bmp, 0, 0, width, height);

                using var ms = new MemoryStream();
                scaledBmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = ms;
                img.CacheOption  = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCaptureExecutor] CaptureScreen: {ex.Message}");
                return null;
            }
        }

        private static BitmapImage? LoadBitmapFromFile(string path)
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource   = new Uri(path, UriKind.Absolute);
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
                img.CacheOption  = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCaptureExecutor] DownloadImage: {ex.Message}");
                return null;
            }
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
    }
}
