using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Helpers
{
    public sealed class ImageProcessorRenderResult
    {
        public BitmapSource Bitmap { get; set; } = null!;
        public int SrcW { get; set; }
        public int SrcH { get; set; }
        public int OutW { get; set; }
        public int OutH { get; set; }
        public int PadX { get; set; }
        public int PadY { get; set; }
        public double RatioW { get; set; }
        public double RatioH { get; set; }
    }

    public static class ImageProcessorHelper
    {
        private const int LAND_W = 1920, LAND_H = 1080;
        private const int PORT_W = 1080, PORT_H = 1920;

        public static ImageProcessorRenderResult? Render(BitmapSource src, bool isVertical, int scale)
        {
            if (src == null) return null;

            int srcW = src.PixelWidth, srcH = src.PixelHeight;
            int stdW = isVertical ? PORT_W : LAND_W;
            int stdH = isVertical ? PORT_H : LAND_H;

            // Tạo canvas nhỏ nhất có tỉ lệ chuẩn (16:9 hoặc 9:16) vừa đủ chứa ảnh gốc.
            // Bước 1: Tính tỉ lệ chiều cao → suy ra chiều rộng kỳ vọng.
            //         Nếu chiều rộng kỳ vọng < srcW (ảnh rộng hơn tỉ lệ chuẩn)
            //         → khớp theo width, tính lại height từ tỉ lệ chuẩn.
            //         Ngược lại → khớp theo height.
            // Bước 2: Ảnh gốc nằm giữa canvas, phần thừa là nền đen.
            double srcAspect = (double)srcW / srcH;
            double stdAspect = (double)stdW / stdH;

            int canvasW, canvasH;
            if (srcAspect >= stdAspect)
            {
                // Ảnh rộng hơn tỉ lệ chuẩn → khớp theo width, thêm đen trên/dưới
                canvasW = srcW;
                canvasH = (int)Math.Ceiling((double)srcW * stdH / stdW);
            }
            else
            {
                // Ảnh cao hơn tỉ lệ chuẩn → khớp theo height, thêm đen trái/phải
                canvasH = srcH;
                canvasW = (int)Math.Ceiling((double)srcH * stdW / stdH);
            }

            // Nhân scale
            int outW = canvasW * scale;
            int outH = canvasH * scale;
            int drawW = srcW * scale;
            int drawH = srcH * scale;

            // Căn giữa ảnh trên canvas
            int offsetX = (outW - drawW) / 2;
            int offsetY = (outH - drawH) / 2;

            // Tỉ lệ canvas so với khung chuẩn 1920×1080 (hoặc 1080×1920)
            double ratioW = Math.Round((double)canvasW / stdW, 4);
            double ratioH = Math.Round((double)canvasH / stdH, 4);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, outW, outH));
                dc.DrawImage(src, new Rect(offsetX, offsetY, drawW, drawH));
            }

            var rtb = new RenderTargetBitmap(outW, outH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();

            return new ImageProcessorRenderResult
            {
                Bitmap = rtb,
                SrcW = srcW, SrcH = srcH,
                OutW = outW, OutH = outH,
                PadX = offsetX, PadY = offsetY,
                RatioW = ratioW, RatioH = ratioH
            };
        }

        public static void SaveBitmap(BitmapSource bitmap, string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            BitmapEncoder encoder = (ext == ".jpg" || ext == ".jpeg")
                ? new JpegBitmapEncoder { QualityLevel = 95 }
                : (BitmapEncoder)new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fs = new FileStream(filePath, FileMode.Create);
            encoder.Save(fs);
        }

        public static string ToBase64(BitmapSource bitmap)
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bitmap));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return Convert.ToBase64String(ms.ToArray());
        }

        public static string ToDataUri(BitmapSource bitmap)
            => "data:image/png;base64," + ToBase64(bitmap);

        public static BitmapSource? FromBase64(string base64)
        {
            try
            {
                string data = base64.Contains(',') ? base64.Split(',')[1] : base64;
                byte[] bytes = Convert.FromBase64String(data);
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public static BitmapSource? LoadThumbnail(string path, int maxWidth)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.DecodePixelWidth = maxWidth;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public static BitmapSource? LoadFullRes(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public static List<string> RenderBatch(
            List<string> imagePaths, bool isVertical, int scale,
            Action<int, int>? onProgress = null)
        {
            var results = new List<string>();
            for (int i = 0; i < imagePaths.Count; i++)
            {
                try
                {
                    var bmp = LoadFullRes(imagePaths[i]);
                    var r = bmp != null ? Render(bmp, isVertical, scale) : null;
                    results.Add(r != null ? ToBase64(r.Bitmap) : "");
                }
                catch { results.Add(""); }

                onProgress?.Invoke(i + 1, imagePaths.Count);
            }
            return results;
        }
    }
}
