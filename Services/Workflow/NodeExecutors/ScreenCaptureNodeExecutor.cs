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

            // Đọc toạ độ từ node nguồn nếu được cấu hình
            if (!string.IsNullOrWhiteSpace(cap.CoordSourceNodeId))
            {
                var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                    env.Connections,
                    cap.CoordSourceNodeId,
                    cap.CoordSourceOutputKey,
                    env);

                if (!string.IsNullOrWhiteSpace(raw) && raw != "—")
                {
                    var parsed = TryParseRect(raw, cap.CoordSourceOutputKey, cap.CaptureWidth, cap.CaptureHeight);
                    if (parsed.HasValue)
                        (x, y, w, h) = parsed.Value;
                }
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

            // Encode PNG một lần để dùng cho cả imageBase64 và imageSizeBytes
            byte[]? pngBytes = null;
            if (!cap.SkipOutputs.Contains("imageBase64") || !cap.SkipOutputs.Contains("imageSizeBytes"))
                pngBytes = TryEncodePng(bitmap);

            foreach (var port in cap.DynamicOutputs)
            {
                var key = port.Key ?? string.Empty;
                if (cap.SkipOutputs.Contains(key)) { port.UserValueOverride = string.Empty; continue; }

                port.UserValueOverride = key.ToLowerInvariant() switch
                {
                    "capturex"      => cap.CaptureX.ToString(),
                    "capturey"      => cap.CaptureY.ToString(),
                    "capturewidth"  => cap.CaptureWidth.ToString(),
                    "captureheight" => cap.CaptureHeight.ToString(),
                    "capturerect"   => $"{cap.CaptureX},{cap.CaptureY},{cap.CaptureWidth},{cap.CaptureHeight}",
                    "imagewidth"    => bitmap.PixelWidth.ToString(),
                    "imageheight"   => bitmap.PixelHeight.ToString(),
                    "imagesizebytes"=> pngBytes != null ? pngBytes.Length.ToString() : string.Empty,
                    "imagebase64"   => pngBytes != null ? Convert.ToBase64String(pngBytes) : string.Empty,
                    _               => string.Empty
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
                using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using var g   = Graphics.FromImage(bmp);
                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);

                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
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
