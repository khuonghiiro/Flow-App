// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Utils
{
    /// <summary>
    /// Wrapper toàn bộ hiệu ứng ImageMagick (Magick.NET) cho editor.
    /// Mỗi method nhận WriteableBitmap → xử lý → trả WriteableBitmap mới.
    /// </summary>
    public static class MagickEffects
    {
        // ══════ CONVERSION HELPERS ══════

        private static MagickImage BitmapToMagick(WriteableBitmap bmp)
        {
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            int stride = w * 4;
            var pixels = new byte[stride * h];
            bmp.CopyPixels(pixels, stride, 0);

            var settings = new MagickReadSettings
            {
                Width = (uint)w,
                Height = (uint)h,
                Format = MagickFormat.Bgra,
                Depth = 8
            };
            return new MagickImage(pixels, settings);
        }

        private static WriteableBitmap MagickToBitmap(MagickImage img)
        {
            img.Alpha(AlphaOption.Set);
            var outPixels = img.ToByteArray(MagickFormat.Bgra);
            int w = (int)img.Width, h = (int)img.Height;
            int stride = w * 4;

            var result = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, w, h), outPixels, stride, 0);
            return result;
        }

        private static WriteableBitmap Apply(WriteableBitmap src, Action<MagickImage> effect)
        {
            using var img = BitmapToMagick(src);
            effect(img);
            return MagickToBitmap(img);
        }

        // ══════════════════════════════════════════════
        //  BLUR / SHARPEN
        // ══════════════════════════════════════════════

        public static WriteableBitmap GaussianBlur(WriteableBitmap src, double radius = 3, double sigma = 1.5)
            => Apply(src, img => img.GaussianBlur(radius, sigma));

        public static WriteableBitmap MotionBlur(WriteableBitmap src, double radius = 8, double sigma = 4, double angle = 0)
            => Apply(src, img => img.MotionBlur(radius, sigma, angle));

        public static WriteableBitmap RadialBlur(WriteableBitmap src, double angle = 5)
            => Apply(src, img => img.RotationalBlur(angle));

        public static WriteableBitmap Sharpen(WriteableBitmap src, double radius = 0, double sigma = 1)
            => Apply(src, img => img.Sharpen(radius, sigma));

        public static WriteableBitmap UnsharpMask(WriteableBitmap src, double radius = 2, double sigma = 1, double amount = 1, double threshold = 0.05)
            => Apply(src, img => img.UnsharpMask(radius, sigma, amount, threshold));

        public static WriteableBitmap AdaptiveBlur(WriteableBitmap src, double radius = 0, double sigma = 1)
            => Apply(src, img => img.AdaptiveBlur(radius, sigma));

        public static WriteableBitmap AdaptiveSharpen(WriteableBitmap src, double radius = 0, double sigma = 1)
            => Apply(src, img => img.AdaptiveSharpen(radius, sigma));

        // ══════════════════════════════════════════════
        //  ARTISTIC EFFECTS
        // ══════════════════════════════════════════════

        public static WriteableBitmap OilPaint(WriteableBitmap src, double radius = 4, double sigma = 1)
            => Apply(src, img => img.OilPaint(radius, sigma));

        public static WriteableBitmap Charcoal(WriteableBitmap src, double radius = 2, double sigma = 1)
            => Apply(src, img => img.Charcoal(radius, sigma));

        public static WriteableBitmap Sketch(WriteableBitmap src, double radius = 2, double sigma = 1, double angle = 0)
            => Apply(src, img => img.Sketch(radius, sigma, angle));

        public static WriteableBitmap Emboss(WriteableBitmap src, double radius = 0, double sigma = 1)
            => Apply(src, img => img.Emboss(radius, sigma));

        public static WriteableBitmap Vignette(WriteableBitmap src, double radius = 0, double sigma = 10, int x = 10, int y = 10)
            => Apply(src, img => img.Vignette(radius, sigma, x, y));

        public static WriteableBitmap Swirl(WriteableBitmap src, double degrees = 60)
            => Apply(src, img => img.Swirl(degrees));

        public static WriteableBitmap Wave(WriteableBitmap src, double amplitude = 5, double length = 50)
            => Apply(src, img =>
            {
                img.Wave(PixelInterpolateMethod.Bilinear, amplitude, length);
                img.Trim();
            });

        public static WriteableBitmap Spread(WriteableBitmap src, double radius = 4)
            => Apply(src, img => img.Spread(radius));

        public static WriteableBitmap Implode(WriteableBitmap src, double amount = 0.3)
            => Apply(src, img => img.Implode(amount, PixelInterpolateMethod.Bilinear));

        public static WriteableBitmap Shade(WriteableBitmap src, double azimuth = 30, double elevation = 30)
            => Apply(src, img => img.Shade(azimuth, elevation));

        public static WriteableBitmap Stegano(WriteableBitmap src) // Pixelate-like effect
            => Apply(src, img => img.Scale(new Percentage(10), new Percentage(10)));

        public static WriteableBitmap Pixelate(WriteableBitmap src, int blockSize = 8)
            => Apply(src, img =>
            {
                int w = (int)img.Width, h = (int)img.Height;
                img.Scale((uint)Math.Max(1, w / blockSize), (uint)Math.Max(1, h / blockSize));
                img.Sample((uint)w, (uint)h);
            });

        // ══════════════════════════════════════════════
        //  EDGE DETECTION
        // ══════════════════════════════════════════════

        public static WriteableBitmap EdgeDetect(WriteableBitmap src, double radius = 1)
            => Apply(src, img => img.Edge(radius));

        public static WriteableBitmap CannyEdge(WriteableBitmap src, double radius = 0, double sigma = 1, double lowPct = 10, double highPct = 30)
            => Apply(src, img => img.CannyEdge(radius, sigma, new Percentage(lowPct), new Percentage(highPct)));

        // ══════════════════════════════════════════════
        //  COLOR ADJUSTMENTS
        // ══════════════════════════════════════════════

        public static WriteableBitmap Posterize(WriteableBitmap src, int levels = 4)
            => Apply(src, img => img.Posterize(levels));

        public static WriteableBitmap Solarize(WriteableBitmap src, double threshold = 50)
            => Apply(src, img => img.Solarize(new Percentage(threshold)));

        public static WriteableBitmap AutoLevel(WriteableBitmap src)
            => Apply(src, img => img.AutoLevel());

        public static WriteableBitmap AutoGamma(WriteableBitmap src)
            => Apply(src, img => img.AutoGamma());

        public static WriteableBitmap Equalize(WriteableBitmap src)
            => Apply(src, img => img.Equalize());

        public static WriteableBitmap Normalize(WriteableBitmap src)
            => Apply(src, img => img.Normalize());

        public static WriteableBitmap Negate(WriteableBitmap src)
            => Apply(src, img => img.Negate());

        public static WriteableBitmap SepiaTone(WriteableBitmap src, double threshold = 80)
            => Apply(src, img => img.SepiaTone(new Percentage(threshold)));

        public static WriteableBitmap Grayscale(WriteableBitmap src)
            => Apply(src, img => img.Grayscale());

        public static WriteableBitmap BrightnessContrast(WriteableBitmap src, double brightness = 10, double contrast = 10)
            => Apply(src, img => img.BrightnessContrast(new Percentage(brightness), new Percentage(contrast)));

        public static WriteableBitmap GammaCorrect(WriteableBitmap src, double gamma = 1.5)
            => Apply(src, img => img.GammaCorrect(gamma));

        public static WriteableBitmap Modulate(WriteableBitmap src, double brightness = 100, double saturation = 150, double hue = 100)
            => Apply(src, img => img.Modulate(new Percentage(brightness), new Percentage(saturation), new Percentage(hue)));

        public static WriteableBitmap Level(WriteableBitmap src, double blackPoint = 10, double whitePoint = 90, double gamma = 1.0)
            => Apply(src, img => img.Level(new Percentage(blackPoint), new Percentage(whitePoint), gamma));

        public static WriteableBitmap Tint(WriteableBitmap src, byte r = 255, byte g = 220, byte b = 180)
            => Apply(src, img => img.Colorize(new MagickColor(r, g, b), new Percentage(25)));

        // ══════════════════════════════════════════════
        //  NOISE
        // ══════════════════════════════════════════════

        public static WriteableBitmap AddNoiseGaussian(WriteableBitmap src, double attenuate = 1.0)
            => Apply(src, img => img.AddNoise(NoiseType.Gaussian, attenuate));

        public static WriteableBitmap AddNoiseImpulse(WriteableBitmap src, double attenuate = 1.0)
            => Apply(src, img => img.AddNoise(NoiseType.Impulse, attenuate));

        public static WriteableBitmap AddNoiseLaplacian(WriteableBitmap src, double attenuate = 1.0)
            => Apply(src, img => img.AddNoise(NoiseType.Laplacian, attenuate));

        public static WriteableBitmap Denoise(WriteableBitmap src)
            => Apply(src, img => img.Enhance());

        public static WriteableBitmap MedianFilter(WriteableBitmap src, int radius = 2)
            => Apply(src, img => img.MedianFilter((uint)radius));

        public static WriteableBitmap ReduceNoise(WriteableBitmap src, int order = 2)
            => Apply(src, img => img.ReduceNoise((uint)order));

        // ══════════════════════════════════════════════
        //  MORPHOLOGY & STRUCTURE
        // ══════════════════════════════════════════════

        public static WriteableBitmap Dilate(WriteableBitmap src, int radius = 1)
            => Apply(src, img => img.Morphology(new MorphologySettings { Method = MorphologyMethod.Dilate, Kernel = Kernel.Diamond, Iterations = radius }));

        public static WriteableBitmap Erode(WriteableBitmap src, int radius = 1)
            => Apply(src, img => img.Morphology(new MorphologySettings { Method = MorphologyMethod.Erode, Kernel = Kernel.Diamond, Iterations = radius }));

        public static WriteableBitmap Opening(WriteableBitmap src, int radius = 1)
            => Apply(src, img => img.Morphology(new MorphologySettings { Method = MorphologyMethod.Open, Kernel = Kernel.Diamond, Iterations = radius }));

        public static WriteableBitmap Closing(WriteableBitmap src, int radius = 1)
            => Apply(src, img => img.Morphology(new MorphologySettings { Method = MorphologyMethod.Close, Kernel = Kernel.Diamond, Iterations = radius }));

        // ══════════════════════════════════════════════
        //  TRANSFORM / DISTORTION
        // ══════════════════════════════════════════════

        public static WriteableBitmap Deskew(WriteableBitmap src, double threshold = 40)
            => Apply(src, img => img.Deskew(new Percentage(threshold)));

        public static WriteableBitmap Flop(WriteableBitmap src)
            => Apply(src, img => img.Flop());

        public static WriteableBitmap Flip(WriteableBitmap src)
            => Apply(src, img => img.Flip());

        public static WriteableBitmap AutoOrient(WriteableBitmap src)
            => Apply(src, img => img.AutoOrient());

        public static WriteableBitmap Trim(WriteableBitmap src)
            => Apply(src, img => img.Trim());

        // ══════════════════════════════════════════════
        //  NEW EFFECTS
        // ══════════════════════════════════════════════

        public static WriteableBitmap BlueShift(WriteableBitmap src, double factor = 1.5)
            => Apply(src, img => img.BlueShift(factor));

        public static WriteableBitmap Kuwahara(WriteableBitmap src, double radius = 2, double sigma = 1)
            => Apply(src, img => img.Kuwahara(radius, sigma));

        public static WriteableBitmap LocalContrast(WriteableBitmap src, double radius = 10, double strength = 5)
            => Apply(src, img => img.LocalContrast(radius, new Percentage(strength)));

        public static WriteableBitmap LinearStretch(WriteableBitmap src, double blackPct = 1, double whitePct = 1)
            => Apply(src, img => img.LinearStretch(new Percentage(blackPct), new Percentage(whitePct)));

        public static WriteableBitmap SigmoidalContrast(WriteableBitmap src, double contrast = 3, double midpoint = 50)
            => Apply(src, img => img.SigmoidalContrast(contrast, new Percentage(midpoint)));

        public static WriteableBitmap Threshold(WriteableBitmap src, double percent = 50)
            => Apply(src, img => img.Threshold(new Percentage(percent)));

        public static WriteableBitmap WhiteThreshold(WriteableBitmap src, double percent = 80)
            => Apply(src, img => img.WhiteThreshold(new Percentage(percent)));

        public static WriteableBitmap BlackThreshold(WriteableBitmap src, double percent = 20)
            => Apply(src, img => img.BlackThreshold(new Percentage(percent)));

        public static WriteableBitmap RotationalBlurFull(WriteableBitmap src, double angle = 10)
            => Apply(src, img => img.RotationalBlur(angle));

        public static WriteableBitmap Quantize(WriteableBitmap src, int colors = 16)
            => Apply(src, img => img.Quantize(new QuantizeSettings { Colors = (uint)colors }));

        public static WriteableBitmap Polaroid(WriteableBitmap src, double angle = 0)
            => Apply(src, img => img.Polaroid("FlowMy", angle, PixelInterpolateMethod.Bilinear));

        public static WriteableBitmap AddNoiseLaplacianDispatch(WriteableBitmap src, double attenuate = 1.0)
            => Apply(src, img => img.AddNoise(NoiseType.Laplacian, attenuate));


        /// <summary>Lấy tất cả effect names được hỗ trợ (dùng cho switch dispatch).</summary>
        public static readonly string[] AllEffectNames = new[]
        {
            // Blur
            "GaussianBlur", "MotionBlur", "RadialBlur", "AdaptiveBlur", "RotationalBlurFull",
            // Sharpen
            "Sharpen", "UnsharpMask", "AdaptiveSharpen",
            // Artistic
            "OilPaint", "Charcoal", "Sketch", "Emboss", "Vignette", "Pixelate",
            "Kuwahara", "Polaroid",
            // Distortion
            "Swirl", "Wave", "Spread", "Implode", "Shade",
            // Edge
            "EdgeDetect", "CannyEdge",
            // Brightness/Color
            "BrightnessUp", "BrightnessDown", "GammaCorrect", "LocalContrast",
            "SaturationUp", "SaturationDown", "Tint", "SigmoidalContrast",
            "Level", "LinearStretch", "BlueShift",
            // Tone/Style
            "Posterize", "Solarize", "SepiaTone", "Grayscale", "Negate",
            "Threshold", "WhiteThreshold", "BlackThreshold", "Quantize",
            // Auto Enhance
            "AutoLevel", "AutoGamma", "Equalize", "Normalize",
            // Noise
            "AddNoiseGaussian", "AddNoiseImpulse", "AddNoiseLaplacian",
            "Denoise", "MedianFilter", "ReduceNoise",
            // Morphology
            "Dilate", "MorphErode", "Opening", "Closing",
            // Transform
            "Deskew", "Trim", "AutoOrient", "Flop", "Flip",
        };

        /// <summary>Dispatch effect by name.</summary>
        public static WriteableBitmap? ApplyByName(WriteableBitmap src, string effectName)
            => ApplyByName(src, effectName, null);

        /// <summary>Dispatch effect by name, with optional parameter overrides.</summary>
        public static WriteableBitmap? ApplyByName(WriteableBitmap src, string effectName, Dictionary<string, double>? p)
        {
            double P(string key, double def) => p != null && p.TryGetValue(key, out var v) ? v : def;
            int PI(string key, int def) => (int)P(key, def);

            return effectName switch
            {
                // Blur
                "GaussianBlur"       => GaussianBlur(src, P("Radius", 3), P("Sigma", 1.5)),
                "MotionBlur"         => MotionBlur(src, P("Radius", 8), P("Sigma", 4), P("Angle", 0)),
                "RadialBlur"         => RadialBlur(src, P("Angle", 5)),
                "AdaptiveBlur"       => AdaptiveBlur(src, P("Radius", 0), P("Sigma", 1)),
                "RotationalBlurFull" => RotationalBlurFull(src, P("Angle", 10)),
                // Sharpen
                "Sharpen"            => Sharpen(src, P("Radius", 0), P("Sigma", 1)),
                "UnsharpMask"        => UnsharpMask(src, P("Radius", 2), P("Sigma", 1), P("Amount", 1), P("Threshold", 0.05)),
                "AdaptiveSharpen"    => AdaptiveSharpen(src, P("Radius", 0), P("Sigma", 1)),
                // Artistic
                "OilPaint"           => OilPaint(src, P("Radius", 4), P("Sigma", 1)),
                "Charcoal"           => Charcoal(src, P("Radius", 2), P("Sigma", 1)),
                "Sketch"             => Sketch(src, P("Radius", 2), P("Sigma", 1), P("Angle", 0)),
                "Emboss"             => Emboss(src, P("Radius", 0), P("Sigma", 1)),
                "Vignette"           => Vignette(src, P("Radius", 0), P("Sigma", 10), PI("X", 10), PI("Y", 10)),
                "Pixelate"           => Pixelate(src, PI("BlockSize", 8)),
                "Kuwahara"           => Kuwahara(src, P("Radius", 2), P("Sigma", 1)),
                "Polaroid"           => Polaroid(src, P("Angle", 0)),
                // Distortion
                "Swirl"              => Swirl(src, P("Degrees", 60)),
                "Wave"               => Wave(src, P("Amplitude", 5), P("Length", 50)),
                "Spread"             => Spread(src, P("Radius", 4)),
                "Implode"            => Implode(src, P("Amount", 0.3)),
                "Shade"              => Shade(src, P("Azimuth", 30), P("Elevation", 30)),
                // Edge
                "EdgeDetect"         => EdgeDetect(src, P("Radius", 1)),
                "CannyEdge"          => CannyEdge(src, P("Radius", 0), P("Sigma", 1), P("LowPct", 10), P("HighPct", 30)),
                // Brightness/Color
                "BrightnessUp"       => BrightnessContrast(src, P("Brightness", 15), P("Contrast", 0)),
                "BrightnessDown"     => BrightnessContrast(src, P("Brightness", -15), P("Contrast", 0)),
                "GammaCorrect"       => GammaCorrect(src, P("Gamma", 1.5)),
                "LocalContrast"      => LocalContrast(src, P("Radius", 10), P("Strength", 5)),
                "SaturationUp"       => Modulate(src, P("Brightness", 100), P("Saturation", 140), P("Hue", 100)),
                "SaturationDown"     => Modulate(src, P("Brightness", 100), P("Saturation", 60), P("Hue", 100)),
                "Tint"               => Tint(src),
                "SigmoidalContrast"  => SigmoidalContrast(src, P("Contrast", 3), P("Midpoint", 50)),
                "Level"              => Level(src, P("BlackPoint", 10), P("WhitePoint", 90), P("Gamma", 1.0)),
                "LinearStretch"      => LinearStretch(src, P("BlackPct", 1), P("WhitePct", 1)),
                "BlueShift"          => BlueShift(src, P("Factor", 1.5)),
                // Tone/Style
                "Posterize"          => Posterize(src, PI("Levels", 4)),
                "Solarize"           => Solarize(src, P("Threshold", 50)),
                "SepiaTone"          => SepiaTone(src, P("Threshold", 80)),
                "Grayscale"          => Grayscale(src),
                "Negate"             => Negate(src),
                "Threshold"          => Threshold(src, P("Percent", 50)),
                "WhiteThreshold"     => WhiteThreshold(src, P("Percent", 80)),
                "BlackThreshold"     => BlackThreshold(src, P("Percent", 20)),
                "Quantize"           => Quantize(src, PI("Colors", 16)),
                // Auto Enhance
                "AutoLevel"          => AutoLevel(src),
                "AutoGamma"          => AutoGamma(src),
                "Equalize"           => Equalize(src),
                "Normalize"          => Normalize(src),
                // Noise
                "AddNoiseGaussian"   => AddNoiseGaussian(src, P("Attenuate", 1)),
                "AddNoiseImpulse"    => AddNoiseImpulse(src, P("Attenuate", 1)),
                "AddNoiseLaplacian"  => AddNoiseLaplacianDispatch(src, P("Attenuate", 1)),
                "Denoise"            => Denoise(src),
                "MedianFilter"       => MedianFilter(src, PI("Radius", 2)),
                "ReduceNoise"        => ReduceNoise(src, PI("Order", 2)),
                // Morphology
                "Dilate"             => Dilate(src, PI("Radius", 1)),
                "MorphErode"         => Erode(src, PI("Radius", 1)),
                "Opening"            => Opening(src, PI("Radius", 1)),
                "Closing"            => Closing(src, PI("Radius", 1)),
                // Transform
                "Deskew"             => Deskew(src, P("Threshold", 40)),
                "Trim"               => Trim(src),
                "AutoOrient"         => AutoOrient(src),
                "Flop"               => Flop(src),
                "Flip"               => Flip(src),
                _ => null
            };
        }
    }
}
