using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class ImageProcessingNodeExecutor : INodeExecutor
    {
        private static readonly HttpClient _http = CreateHttpClient();
        
        private static HttpClient CreateHttpClient()
        {
            // Tạo HttpClientHandler với SSL validation bypass để xử lý các website không có SSL hợp lệ
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // Bỏ qua SSL validation errors để có thể tải ảnh từ các website không có SSL hợp lệ
                    return true;
                }
            };
            
            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
        }

        public bool CanExecute(WorkflowNode node) => node is ImageProcessingNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var imageNode = (ImageProcessingNode)node;
            var sw = Stopwatch.StartNew();

            try
            {
                // Lưu ExecutionId của lần chạy workflow hiện tại vào node ảnh
                // để UI/logic khác có thể biết kết quả nào thuộc lần chạy nào.
                imageNode.LastExecutionId = env.ExecutionId;

                // Clear UserValueOverride cho các key bị skip để tránh giá trị cũ từ lần chạy trước
                // vẫn bị resolve bởi NodeDataPanelService/downstream nodes.
                if (imageNode.SkipOutputs != null && imageNode.SkipOutputs.Count > 0 && imageNode.DynamicOutputs != null)
                {
                    foreach (var port in imageNode.DynamicOutputs)
                    {
                        if (imageNode.SkipOutputs.Contains(port.Key ?? string.Empty))
                            port.UserValueOverride = string.Empty;
                    }
                }

                // Resolve input và lưu original path
                var inputResult = await ResolveInputImageBytesAsync(imageNode, env).ConfigureAwait(false);
                byte[] inputBytes = inputResult.Bytes;
                string? originalPath = inputResult.OriginalPath;
                bool isUrlInput = inputResult.IsUrlInput;
                
                if (inputBytes.Length == 0)
                    throw new InvalidOperationException("Không có dữ liệu ảnh để xử lý.");

                // Temp files
                var tempDir = Path.Combine(Path.GetTempPath(), "FlowMy_ImageProcessing");
                Directory.CreateDirectory(tempDir);
                var inputPath = Path.Combine(tempDir, $"in_{Guid.NewGuid():N}.png");
                var outputPath = Path.Combine(tempDir, $"out_{Guid.NewGuid():N}.png");

                // Chỉ ghi vào temp nếu cần (URL hoặc base64 hoặc có filter)
                var filter = imageNode.FfmpegFilter?.Trim();
                bool needsTempFile = isUrlInput || imageNode.InputMode == ImageInputMode.Base64 || !string.IsNullOrWhiteSpace(filter);
                
                if (needsTempFile)
                {
                    await File.WriteAllBytesAsync(inputPath, inputBytes, env.CancellationToken).ConfigureAwait(false);
                }

                byte[] outBytes;
                string finalImagePath;

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    // Has filter → process through FFmpeg → luôn dùng outputPath (temp)
                    var ok = await TryRunFfmpegAsync(inputPath, outputPath, filter, preferGpu: imageNode.PreferGpu, env.CancellationToken)
                        .ConfigureAwait(false);
                    if (!ok)
                        throw new InvalidOperationException("FFmpeg xử lý ảnh thất bại (không tìm thấy ffmpeg hoặc tham số không hợp lệ).");

                    outBytes = await File.ReadAllBytesAsync(outputPath, env.CancellationToken).ConfigureAwait(false);
                    finalImagePath = outputPath; // Dùng temp path sau khi xử lý
                }
                else
                {
                    // No filter → pass through input image directly
                    outBytes = inputBytes;
                    
                    // Quyết định path: nếu có original path (file local) và không phải URL → dùng original
                    // Nếu là URL hoặc base64 → dùng temp path
                    if (!string.IsNullOrWhiteSpace(originalPath) && !isUrlInput && File.Exists(originalPath))
                    {
                        finalImagePath = originalPath; // Dùng path gốc
                    }
                    else if (needsTempFile && File.Exists(inputPath))
                    {
                        finalImagePath = inputPath; // Dùng temp path
                    }
                    else
                    {
                        // Fallback: tạo temp file nếu chưa có
                        await File.WriteAllBytesAsync(inputPath, inputBytes, env.CancellationToken).ConfigureAwait(false);
                        finalImagePath = inputPath;
                    }
                }
                var outBase64 = Convert.ToBase64String(outBytes);

                SetOutput(imageNode, "imagePath", finalImagePath);
                SetOutput(imageNode, "imageBase64", outBase64);

                // Tạo danh sách base64 cho từng vùng crop đã được định nghĩa
                var cropList = await GenerateCropBase64ListAsync(imageNode, inputBytes, env.CancellationToken).ConfigureAwait(false);
                SetOutput(imageNode, "cropListBase64", JsonSerializer.Serialize(cropList));

                // Thêm các key mới: cropBase64 (đã được set từ Image Processor), aspectRatio, promptSize, prompt
                // cropBase64 được set từ Image Processor khi nhấn "Bắt đầu", không set ở đây
                SetOutput(imageNode, "promptSize", imageNode.PromptSize.ToString());
                
                // aspectRatio: dùng từ IsVerticalMode của node
                string aspectRatio = imageNode.IsVerticalMode ? "9:16" : "16:9";
                SetOutput(imageNode, "aspectRatio", aspectRatio);
                
                // prompt: text từ Image Processor
                SetOutput(imageNode, "prompt", imageNode.ProcessorPrompt ?? string.Empty);

                // executionId: id duy nhất cho lần chạy này, dùng để map ảnh render về đúng crop
                SetOutput(imageNode, "executionId", env.ExecutionId);
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(node, ex.Message);
                throw;
            }
            finally
            {
                sw.Stop();
                env.OnNodeCompleted?.Invoke(node, sw.Elapsed);
            }

            await env.TraverseOutputsAsync(imageNode);
        }

        private static void SetOutput(ImageProcessingNode node, string key, string value)
        {
            // Kiểm tra SkipOutputs: nếu key bị skip thì không set output
            if (node.SkipOutputs != null && node.SkipOutputs.Contains(key))
                return;

            var port = node.DynamicOutputs?.FirstOrDefault(o =>
                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            if (port != null)
            {
                port.UserValueOverride = value ?? string.Empty;
            }
        }

        private sealed class InputImageResult
        {
            public byte[] Bytes { get; set; } = Array.Empty<byte>();
            public string? OriginalPath { get; set; }
            public bool IsUrlInput { get; set; }
        }

        private static async Task<InputImageResult> ResolveInputImageBytesAsync(
            ImageProcessingNode node, 
            NodeExecutionEnvironment env)
        {
            var result = new InputImageResult();
            string? resolved = null;

            if (node.InputMode == ImageInputMode.Base64)
            {
                resolved = ResolveFromNodeIfAny(env, node.ImageBase64SourceNodeId, node.ImageBase64SourceOutputKey)
                           ?? node.ImageBase64;
                result.Bytes = DecodeBase64Image(resolved);
                return result;
            }

            resolved = ResolveFromNodeIfAny(env, node.ImageUrlSourceNodeId, node.ImageUrlSourceOutputKey)
                       ?? node.ImageUrl;
            resolved = (resolved ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolved))
                return result;

            if (resolved.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                resolved = new Uri(resolved).LocalPath;

            if (resolved.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                resolved.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                result.IsUrlInput = true;
                result.OriginalPath = resolved; // Lưu URL để biết là URL input
                result.Bytes = await _http.GetByteArrayAsync(resolved, env.CancellationToken).ConfigureAwait(false);
                return result;
            }

            // Local file path
            if (File.Exists(resolved))
            {
                result.OriginalPath = resolved; // Lưu path gốc để dùng sau
                result.Bytes = await File.ReadAllBytesAsync(resolved, env.CancellationToken).ConfigureAwait(false);
                return result;
            }

            return result;
        }

        private static string? ResolveFromNodeIfAny(NodeExecutionEnvironment env, string? nodeId, string? key)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key)) return null;
            WorkflowNode? src = null;

            if (env.ReachableToEnd != null && env.ReachableToEnd.Count > 0)
            {
                src = env.ReachableToEnd.FirstOrDefault(n =>
                    string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            }

            src ??= env.Connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));

            if (src == null) return null;
            var value = env.Service.ResolveDynamicValueForExecution(src, key, env);
            if (string.IsNullOrWhiteSpace(value) || value == "—") return null;
            return value;
        }

        private static byte[] DecodeBase64Image(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return Array.Empty<byte>();
            base64 = base64.Trim();
            var comma = base64.IndexOf(',');
            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
                base64 = base64[(comma + 1)..];

            base64 = new string(base64.Where(c => !char.IsWhiteSpace(c)).ToArray());
            try { return Convert.FromBase64String(base64); }
            catch { return Array.Empty<byte>(); }
        }

        /// <summary>
        /// Tạo danh sách base64 PNG cho mỗi vùng crop đã định nghĩa trong node.
        /// Mỗi phần tử trong list tương ứng với 1 ImageCropRegion (theo thứ tự trong node.Crops).
        /// Crop theo bounding box polygon (rectangle bao quanh). Các region thiếu điểm bị bỏ qua.
        /// </summary>
        private static Task<List<string>> GenerateCropBase64ListAsync(
            ImageProcessingNode node,
            byte[] inputBytes,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var result = new List<string>();

                if (node.Crops == null || node.Crops.Count == 0 || inputBytes.Length == 0)
                    return result;

                // Lọc các region hợp lệ (≥ 3 điểm, bounding box > 0)
                var validCrops = node.Crops
                    .Where(r => r.Points.Count >= 3 && r.BoundingBox.Width > 0 && r.BoundingBox.Height > 0)
                    .ToList();
                if (validCrops.Count == 0) return result;

                try
                {
                    using var ms = new MemoryStream(inputBytes);
                    using var srcBitmap = new Bitmap(ms);

                    int imgW = srcBitmap.Width;
                    int imgH = srcBitmap.Height;

                    foreach (var region in validCrops)
                    {
                        ct.ThrowIfCancellationRequested();

                        var bb = region.BoundingBox;
                        int x = Math.Max(0, (int)Math.Round(bb.X));
                        int y = Math.Max(0, (int)Math.Round(bb.Y));
                        int w = Math.Min((int)Math.Round(bb.Width), imgW - x);
                        int h = Math.Min((int)Math.Round(bb.Height), imgH - y);

                        if (w <= 0 || h <= 0) continue;

                        try
                        {
                            var base64 = CropRegionToBase64(srcBitmap, region, x, y, w, h);
                            result.Add(base64);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ImageProcessingNodeExecutor] Crop region failed: {ex.Message}");
                            // Thêm chuỗi rỗng để giữ đúng thứ tự với node.Crops
                            result.Add(string.Empty);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImageProcessingNodeExecutor] GenerateCropBase64List failed: {ex.Message}");
                }

                return result;
            }, ct);
        }

        /// <summary>
        /// Cắt ảnh theo bounding box của region và áp polygon mask để chỉ giữ pixel bên trong polygon.
        /// Trả về chuỗi base64 PNG.
        /// </summary>
        private static string CropRegionToBase64(
            Bitmap srcBitmap,
            ImageCropRegion region,
            int x, int y, int w, int h)
        {
            // Tạo bitmap crop mới với nền trong suốt
            using var cropBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(cropBitmap);

            g.Clear(Color.Transparent);

            // Xây dựng polygon mask (toạ độ tương đối với bounding box crop)
            var pts = region.Points
                .Select(p => new PointF((float)(p.X - x), (float)(p.Y - y)))
                .ToArray();

            if (pts.Length >= 3)
            {
                // Clip vùng vẽ theo polygon
                using var clipPath = new System.Drawing.Drawing2D.GraphicsPath();
                clipPath.AddPolygon(pts);
                g.SetClip(clipPath);
            }

            // Vẽ phần ảnh gốc trong bounding box lên bitmap mới
            g.DrawImage(srcBitmap,
                destRect: new Rectangle(0, 0, w, h),
                srcX: x, srcY: y, srcWidth: w, srcHeight: h,
                srcUnit: GraphicsUnit.Pixel);

            // Encode sang PNG rồi base64
            using var outMs = new MemoryStream();
            cropBitmap.Save(outMs, ImageFormat.Png);
            return Convert.ToBase64String(outMs.ToArray());
        }

        private static async Task<bool> TryRunFfmpegAsync(
            string inputPath,
            string outputPath,
            string? filter,
            bool preferGpu,
            CancellationToken ct)
        {
            // Attempt GPU-ish run first (best-effort). For images this may be a no-op depending on build.
            if (preferGpu)
            {
                var okGpu = await RunFfmpegOnceAsync(inputPath, outputPath, filter, withHwAccelAuto: true, ct).ConfigureAwait(false);
                if (okGpu) return true;
            }

            return await RunFfmpegOnceAsync(inputPath, outputPath, filter, withHwAccelAuto: false, ct).ConfigureAwait(false);
        }

        private static async Task<bool> RunFfmpegOnceAsync(
            string inputPath,
            string outputPath,
            string? filter,
            bool withHwAccelAuto,
            CancellationToken ct)
        {
            var args = "-y -hide_banner -loglevel error ";
            if (withHwAccelAuto)
                args += "-hwaccel auto ";
            args += $"-i \"{inputPath}\" ";
            if (!string.IsNullOrWhiteSpace(filter))
                args += $"-vf \"{filter}\" ";
            args += $"\"{outputPath}\"";

            // Ưu tiên ffmpeg.exe trong thư mục Ffmpeg/ cạnh exe, fallback sang ffmpeg trong PATH.
            string exePath;
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var localFfmpeg = Path.Combine(baseDir, "Ffmpeg", "ffmpeg.exe");
                exePath = File.Exists(localFfmpeg) ? localFfmpeg : "ffmpeg";
            }
            catch
            {
                exePath = "ffmpeg";
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            try
            {
                using var p = Process.Start(psi);
                if (p == null) return false;

                var stderrTask = p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync(ct).ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);

                if (p.ExitCode != 0)
                {
                    Debug.WriteLine("ffmpeg failed: " + stderr);
                    return false;
                }

                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ffmpeg exception: " + ex.Message);
                return false;
            }
        }
    }
}


