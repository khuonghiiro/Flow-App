using ImageMagick;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Utils
{
    /// <summary>
    /// Wrapper các hiệu ứng ImageMagick (Magick.NET) cho editor.
    /// Mỗi method nhận WriteableBitmap → xử lý → trả WriteableBitmap mới.
    /// </summary>
    public static class MagickEffects
    {
        // ══════ CONVERSION HELPERS ══════

        /// <summary>Chuyển WriteableBitmap (BGRA32) → MagickImage.</summary>
        private static MagickImage BitmapToMagick(WriteableBitmap bmp)
        {
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            int stride = w * 4;
            var pixels = new byte[stride * h];
            bmp.CopyPixels(pixels, stride, 0);

            // WPF BGRA → Magick BGRA
            var settings = new MagickReadSettings
            {
                Width = (uint)w,
                Height = (uint)h,
                Format = MagickFormat.Bgra,
                Depth = 8
            };
            return new MagickImage(pixels, settings);
        }

        /// <summary>Chuyển MagickImage → WriteableBitmap (BGRA32).</summary>
        private static WriteableBitmap MagickToBitmap(MagickImage img)
        {
            // Ensure BGRA byte order
            img.Alpha(AlphaOption.Set);
            var outPixels = img.ToByteArray(MagickFormat.Bgra);
            int w = (int)img.Width, h = (int)img.Height;
            int stride = w * 4;

            var result = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, w, h), outPixels, stride, 0);
            return result;
        }

        /// <summary>Áp dụng effect lên WriteableBitmap, trả WriteableBitmap mới.</summary>
        private static WriteableBitmap Apply(WriteableBitmap src, Action<MagickImage> effect)
        {
            using var img = BitmapToMagick(src);
            effect(img);
            return MagickToBitmap(img);
        }

        // ══════ BLUR / SHARPEN ══════

        /// <summary>Gaussian Blur với radius tuỳ chỉnh.</summary>
        public static WriteableBitmap GaussianBlur(WriteableBitmap src, double radius = 3, double sigma = 1.5)
            => Apply(src, img => img.GaussianBlur(radius, sigma));

        /// <summary>Motion Blur — làm mờ theo hướng.</summary>
        public static WriteableBitmap MotionBlur(WriteableBitmap src, double radius = 8, double sigma = 4, double angle = 0)
            => Apply(src, img => img.MotionBlur(radius, sigma, angle));

        /// <summary>Sharpen (làm sắc nét).</summary>
        public static WriteableBitmap Sharpen(WriteableBitmap src, double radius = 0, double sigma = 1)
            => Apply(src, img => img.Sharpen(radius, sigma));

        /// <summary>Unsharp Mask — tăng độ nét chuyên nghiệp.</summary>
        public static WriteableBitmap UnsharpMask(WriteableBitmap src, double radius = 2, double sigma = 1, double amount = 1, double threshold = 0.05)
            => Apply(src, img => img.UnsharpMask(radius, sigma, amount, threshold));

        // ══════ ARTISTIC EFFECTS ══════

        /// <summary>Oil Paint — hiệu ứng tranh sơn dầu.</summary>
        public static WriteableBitmap OilPaint(WriteableBitmap src, double radius = 4, double sigma = 1)
            => Apply(src, img => img.OilPaint(radius, sigma));

        /// <summary>Charcoal — hiệu ứng phác hoạ than chì.</summary>
        public static WriteableBitmap Charcoal(WriteableBitmap src, double radius = 2, double sigma = 1)
            => Apply(src, img => img.Charcoal(radius, sigma));

        /// <summary>Sketch — hiệu ứng phác thảo bút chì.</summary>
        public static WriteableBitmap Sketch(WriteableBitmap src, double radius = 2, double sigma = 1, double angle = 0)
            => Apply(src, img => img.Sketch(radius, sigma, angle));

        /// <summary>Emboss — tạo hiệu ứng nổi 3D.</summary>
        public static WriteableBitmap Emboss(WriteableBitmap src, double radius = 0, double sigma = 1)
            => Apply(src, img => img.Emboss(radius, sigma));

        /// <summary>Vignette — viền tối xung quanh.</summary>
        public static WriteableBitmap Vignette(WriteableBitmap src, double radius = 0, double sigma = 10, int x = 10, int y = 10)
            => Apply(src, img => img.Vignette(radius, sigma, x, y));

        /// <summary>Swirl — xoáy ảnh quanh tâm.</summary>
        public static WriteableBitmap Swirl(WriteableBitmap src, double degrees = 60)
            => Apply(src, img => img.Swirl(degrees));

        /// <summary>Wave — hiệu ứng sóng.</summary>
        public static WriteableBitmap Wave(WriteableBitmap src, double amplitude = 5, double length = 50)
            => Apply(src, img =>
            {
                img.Wave(PixelInterpolateMethod.Bilinear, amplitude, length);
                // Trim transparent border do wave tạo ra
                img.Trim();
            });

        /// <summary>Spread — phân tán pixel ngẫu nhiên.</summary>
        public static WriteableBitmap Spread(WriteableBitmap src, double radius = 4)
            => Apply(src, img => img.Spread(radius));

        // ══════ EDGE DETECTION ══════

        /// <summary>Edge Detection — phát hiện cạnh.</summary>
        public static WriteableBitmap EdgeDetect(WriteableBitmap src, double radius = 1)
            => Apply(src, img => img.Edge(radius));

        /// <summary>Canny Edge Detection — phát hiện cạnh chính xác.</summary>
        public static WriteableBitmap CannyEdge(WriteableBitmap src, double radius = 0, double sigma = 1, double lowThreshold = 10, double highThreshold = 30)
            => Apply(src, img => img.CannyEdge(radius, sigma, new Percentage(lowThreshold), new Percentage(highThreshold)));

        // ══════ COLOR ADJUSTMENTS ══════

        /// <summary>Posterize — giảm số mức màu.</summary>
        public static WriteableBitmap Posterize(WriteableBitmap src, int levels = 4)
            => Apply(src, img => img.Posterize(levels));

        /// <summary>Solarize — hiệu ứng phơi sáng quá mức.</summary>
        public static WriteableBitmap Solarize(WriteableBitmap src, double threshold = 50)
            => Apply(src, img => img.Solarize(new Percentage(threshold)));

        /// <summary>Auto Level — tự động cân bằng sáng.</summary>
        public static WriteableBitmap AutoLevel(WriteableBitmap src)
            => Apply(src, img => img.AutoLevel());

        /// <summary>Auto Gamma — tự động cân bằng gamma.</summary>
        public static WriteableBitmap AutoGamma(WriteableBitmap src)
            => Apply(src, img => img.AutoGamma());

        /// <summary>Equalize — cân bằng histogram.</summary>
        public static WriteableBitmap Equalize(WriteableBitmap src)
            => Apply(src, img => img.Equalize());

        /// <summary>Normalize — bình thường hoá dải sáng.</summary>
        public static WriteableBitmap Normalize(WriteableBitmap src)
            => Apply(src, img => img.Normalize());

        /// <summary>Negate — đảo ngược màu (giống Invert nhưng qua Magick).</summary>
        public static WriteableBitmap Negate(WriteableBitmap src)
            => Apply(src, img => img.Negate());

        /// <summary>Sepia Tone — màu hoài cổ qua Magick.</summary>
        public static WriteableBitmap SepiaTone(WriteableBitmap src, double threshold = 80)
            => Apply(src, img => img.SepiaTone(new Percentage(threshold)));

        // ══════ NOISE ══════

        /// <summary>Add Noise — thêm nhiễu.</summary>
        public static WriteableBitmap AddNoise(WriteableBitmap src, NoiseType type = NoiseType.Gaussian, double attenuate = 1.0)
            => Apply(src, img => img.AddNoise(type, attenuate));

        /// <summary>Denoise — giảm nhiễu (Enhanced).</summary>
        public static WriteableBitmap Denoise(WriteableBitmap src)
            => Apply(src, img => img.Enhance());

        /// <summary>Median Filter — lọc nhiễu median.</summary>
        public static WriteableBitmap MedianFilter(WriteableBitmap src, int radius = 2)
            => Apply(src, img => img.MedianFilter((uint)radius));

        // ══════ TRANSFORM ══════

        /// <summary>Implode — co kéo vào tâm.</summary>
        public static WriteableBitmap Implode(WriteableBitmap src, double amount = 0.3)
            => Apply(src, img => img.Implode(amount, PixelInterpolateMethod.Bilinear));

        /// <summary>Shade — tạo bóng 3D.</summary>
        public static WriteableBitmap Shade(WriteableBitmap src, double azimuth = 30, double elevation = 30)
            => Apply(src, img =>
            {
                img.Shade(azimuth, elevation);
                // Re-colorize vì Shade tạo ảnh grayscale
                img.Colorize(MagickColors.Gray, new Percentage(0));
            });
    }
}
