// ========================================================================================
// SkiaSharp-based FX engine — partial class for ImageProcessingNodeContentControl.
// Replaces slow ImageMagick calls with SkiaSharp equivalents for supported effects,
// and adds new SkiaSharp-exclusive effects.
// ========================================================================================
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl
    {
        // ═══════════════════════════════════════════════════════
        // Set of effect names handled by SkiaSharp instead of ImageMagick
        // ═══════════════════════════════════════════════════════
        private static readonly HashSet<string> _skiaFxEffects = new(StringComparer.Ordinal)
        {
            // Blur/Sharpen
            "GaussianBlur", "Blur", "Sharpen", "UnsharpMask",
            // Color
            "Grayscale", "BrightnessUp", "BrightnessDown", "GammaCorrect",
            "SaturationUp", "SaturationDown", "ContrastUp", "ContrastDown",
            "Negate", "SepiaTone", "Posterize", "Threshold",
            // Transform
            "Pixelate",
            // New SkiaSharp-only
            "SkiaDropShadow", "SkiaColorMatrix", "SkiaHueRotate",
            "SkiaDilate", "SkiaErode", "SkiaLighting", "SkiaBlendMode"
        };

        /// <summary>Checks whether the named effect should be processed via SkiaSharp.</summary>
        private static bool IsSkiaSharpEffect(string effectName) => _skiaFxEffects.Contains(effectName);

        // ═══════════════════════════════════════════════════════
        // Main SkiaSharp effect dispatcher (runs on background thread)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Apply a SkiaSharp-based effect to raw BGRA pixel data.
        /// Returns the processed pixel data (same dimensions unless transform changes size).
        /// </summary>
        private static byte[] ApplySkiaSharpEffect(byte[] srcPixels, int w, int h, string effectName, Dictionary<string, double>? p, CancellationToken token)
        {
            double P(string key, double def) => p != null && p.TryGetValue(key, out var v) ? v : def;
            int PI(string key, int def) => (int)P(key, def);

            // Convert BGRA byte[] → SKBitmap
            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var srcBitmap = new SKBitmap(info);
            unsafe
            {
                fixed (byte* ptr = srcPixels)
                {
                    srcBitmap.InstallPixels(info, (IntPtr)ptr, info.RowBytes);
                }
            }
            // We need a copy since InstallPixels doesn't own the memory
            using var bitmap = srcBitmap.Copy();

            token.ThrowIfCancellationRequested();

            // Dispatch effect
            SKBitmap result = effectName switch
            {
                "GaussianBlur" => SkiaBlur(bitmap, P("Sigma", 1.5)),
                "Blur" => SkiaBlur(bitmap, P("Sigma", 2)),
                "Sharpen" => SkiaSharpen(bitmap, P("Sigma", 1)),
                "UnsharpMask" => SkiaUnsharpMask(bitmap, P("Sigma", 1), P("Amount", 1)),
                "Grayscale" => SkiaColorMatrixFilter(bitmap, GrayscaleMatrix),
                "BrightnessUp" => SkiaBrightness(bitmap, P("Brightness", 15) / 100.0),
                "BrightnessDown" => SkiaBrightness(bitmap, -P("Brightness", 15) / 100.0),
                "GammaCorrect" => SkiaGammaCorrect(bitmap, P("Gamma", 1.5)),
                "SaturationUp" => SkiaSaturation(bitmap, P("Saturation", 140) / 100.0),
                "SaturationDown" => SkiaSaturation(bitmap, P("Saturation", 60) / 100.0),
                "ContrastUp" => SkiaContrast(bitmap, 0.15),
                "ContrastDown" => SkiaContrast(bitmap, -P("Contrast", 15) / 100.0),
                "Negate" => SkiaColorMatrixFilter(bitmap, NegateMatrix),
                "SepiaTone" => SkiaColorMatrixFilter(bitmap, SepiaMatrix(P("Threshold", 80) / 100.0)),
                "Posterize" => SkiaPosterize(bitmap, PI("Levels", 4)),
                "Threshold" => SkiaThreshold(bitmap, P("Percent", 50) / 100.0),
                "Pixelate" => SkiaPixelate(bitmap, PI("BlockSize", 8)),
                // New SkiaSharp-only effects
                "SkiaDropShadow" => SkiaDropShadow(bitmap, P("OffsetX", 5), P("OffsetY", 5), P("SigmaX", 4), P("SigmaY", 4), PI("ShadowAlpha", 128)),
                "SkiaColorMatrix" => SkiaCustomColorMatrix(bitmap,
                    (float)P("R_R", 1), (float)P("R_G", 0), (float)P("R_B", 0), (float)P("R_A", 0), (float)P("R_Bias", 0),
                    (float)P("G_R", 0), (float)P("G_G", 1), (float)P("G_B", 0), (float)P("G_A", 0), (float)P("G_Bias", 0),
                    (float)P("B_R", 0), (float)P("B_G", 0), (float)P("B_B", 1), (float)P("B_A", 0), (float)P("B_Bias", 0),
                    (float)P("A_R", 0), (float)P("A_G", 0), (float)P("A_B", 0), (float)P("A_A", 1), (float)P("A_Bias", 0)),
                "SkiaHueRotate" => SkiaHueRotate(bitmap, P("Degrees", 90)),
                "SkiaDilate" => SkiaMorphology(bitmap, PI("RadiusX", 2), PI("RadiusY", 2), isDilate: true),
                "SkiaErode" => SkiaMorphology(bitmap, PI("RadiusX", 2), PI("RadiusY", 2), isDilate: false),
                "SkiaLighting" => SkiaDistantLighting(bitmap, P("Azimuth", 225), P("Elevation", 45), P("SpecularExponent", 8), P("SpecularConstant", 0.7)),
                "SkiaBlendMode" => SkiaBlendModeEffect(bitmap, PI("BlendModeIndex", 0), PI("OverlayR", 128), PI("OverlayG", 128), PI("OverlayB", 128), PI("OverlayA", 100)),
                _ => bitmap.Copy() // fallback: return unchanged copy
            };

            token.ThrowIfCancellationRequested();

            // Convert result back to BGRA byte[]
            byte[] output;
            int rw = result.Width, rh = result.Height;
            if (rw == w && rh == h)
            {
                output = new byte[w * 4 * h];
                var resultInfo = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                result.GetPixelSpan().CopyTo(output);
            }
            else
            {
                // Size changed — fit into original layer size
                output = new byte[w * 4 * h];
                int copyW = Math.Min(w, rw);
                int copyH = Math.Min(h, rh);
                var srcSpan = result.GetPixelSpan();
                for (int y = 0; y < copyH; y++)
                {
                    srcSpan.Slice(y * rw * 4, copyW * 4).CopyTo(output.AsSpan(y * w * 4, copyW * 4));
                }
            }

            if (!ReferenceEquals(result, bitmap))
                result.Dispose();

            return output;
        }

        // ═══════════════════════════════════════════════════════
        // Helper: Render bitmap through an SKPaint with image filter
        // ═══════════════════════════════════════════════════════

        private static SKBitmap RenderWithFilter(SKBitmap src, SKImageFilter filter)
        {
            var info = new SKImageInfo(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var dst = new SKBitmap(info);
            using var canvas = new SKCanvas(dst);
            using var paint = new SKPaint { ImageFilter = filter };
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(src, 0, 0, paint);
            return dst;
        }

        private static SKBitmap RenderWithColorFilter(SKBitmap src, SKColorFilter colorFilter)
        {
            var info = new SKImageInfo(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var dst = new SKBitmap(info);
            using var canvas = new SKCanvas(dst);
            using var paint = new SKPaint { ColorFilter = colorFilter };
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(src, 0, 0, paint);
            return dst;
        }

        // ═══════════════════════════════════════════════════════
        // BLUR / SHARPEN
        // ═══════════════════════════════════════════════════════

        private static SKBitmap SkiaBlur(SKBitmap src, double sigma)
        {
            float s = Math.Max(0.1f, (float)sigma);
            using var filter = SKImageFilter.CreateBlur(s, s);
            return RenderWithFilter(src, filter);
        }

        private static SKBitmap SkiaSharpen(SKBitmap src, double sigma)
        {
            // Unsharp mask approach: sharpen = original + (original - blur) * amount
            float s = Math.Max(0.5f, (float)sigma);
            using var blurFilter = SKImageFilter.CreateBlur(s, s);
            var blurred = RenderWithFilter(src, blurFilter);

            var info = src.Info;
            var result = new SKBitmap(info);
            var srcSpan = src.GetPixelSpan();
            var blurSpan = blurred.GetPixelSpan();
            var dstBytes = new byte[info.RowBytes * info.Height];

            for (int i = 0; i < srcSpan.Length; i++)
            {
                int diff = srcSpan[i] - blurSpan[i];
                dstBytes[i] = (byte)Math.Clamp(srcSpan[i] + diff, 0, 255);
            }

            unsafe
            {
                fixed (byte* ptr = dstBytes)
                {
                    result.InstallPixels(info, (IntPtr)ptr, info.RowBytes);
                }
            }
            var copy = result.Copy();
            result.Dispose();
            blurred.Dispose();
            return copy;
        }

        private static SKBitmap SkiaUnsharpMask(SKBitmap src, double sigma, double amount)
        {
            float s = Math.Max(0.5f, (float)sigma);
            float a = Math.Max(0.1f, (float)amount);
            using var blurFilter = SKImageFilter.CreateBlur(s, s);
            var blurred = RenderWithFilter(src, blurFilter);

            var info = src.Info;
            var result = new SKBitmap(info);
            var srcSpan = src.GetPixelSpan();
            var blurSpan = blurred.GetPixelSpan();
            var dstBytes = new byte[info.RowBytes * info.Height];

            for (int i = 0; i < srcSpan.Length; i++)
            {
                int diff = srcSpan[i] - blurSpan[i];
                dstBytes[i] = (byte)Math.Clamp(srcSpan[i] + (int)(diff * a), 0, 255);
            }

            unsafe
            {
                fixed (byte* ptr = dstBytes)
                {
                    result.InstallPixels(info, (IntPtr)ptr, info.RowBytes);
                }
            }
            var copy = result.Copy();
            result.Dispose();
            blurred.Dispose();
            return copy;
        }

        // ═══════════════════════════════════════════════════════
        // COLOR MATRIX FILTERS
        // ═══════════════════════════════════════════════════════

        // Grayscale matrix (BT.601 luma coefficients)
        private static readonly float[] GrayscaleMatrix = new float[]
        {
            0.299f, 0.587f, 0.114f, 0, 0,
            0.299f, 0.587f, 0.114f, 0, 0,
            0.299f, 0.587f, 0.114f, 0, 0,
            0,      0,      0,      1, 0
        };

        // Negate (invert) matrix
        private static readonly float[] NegateMatrix = new float[]
        {
            -1, 0,  0,  0, 255,
            0, -1,  0,  0, 255,
            0,  0, -1,  0, 255,
            0,  0,  0,  1, 0
        };

        // Sepia tone matrix
        private static float[] SepiaMatrix(double intensity)
        {
            float t = (float)Math.Clamp(intensity, 0, 1);
            float nt = 1f - t;
            return new float[]
            {
                nt + t * 0.393f, t * 0.769f,      t * 0.189f,      0, 0,
                t * 0.349f,      nt + t * 0.686f,  t * 0.168f,      0, 0,
                t * 0.272f,      t * 0.534f,       nt + t * 0.131f, 0, 0,
                0,               0,                0,               1, 0
            };
        }

        private static SKBitmap SkiaColorMatrixFilter(SKBitmap src, float[] matrix)
        {
            using var cf = SKColorFilter.CreateColorMatrix(matrix);
            return RenderWithColorFilter(src, cf);
        }

        // ═══════════════════════════════════════════════════════
        // BRIGHTNESS / CONTRAST / SATURATION / GAMMA
        // ═══════════════════════════════════════════════════════

        private static SKBitmap SkiaBrightness(SKBitmap src, double amount)
        {
            float b = (float)(amount * 255);
            float[] matrix = new float[]
            {
                1, 0, 0, 0, b,
                0, 1, 0, 0, b,
                0, 0, 1, 0, b,
                0, 0, 0, 1, 0
            };
            using var cf = SKColorFilter.CreateColorMatrix(matrix);
            return RenderWithColorFilter(src, cf);
        }

        private static SKBitmap SkiaContrast(SKBitmap src, double amount)
        {
            // Contrast: scale around midpoint (128)
            float c = 1f + (float)amount;
            float t = 128f * (1f - c);
            float[] matrix = new float[]
            {
                c, 0, 0, 0, t,
                0, c, 0, 0, t,
                0, 0, c, 0, t,
                0, 0, 0, 1, 0
            };
            using var cf = SKColorFilter.CreateColorMatrix(matrix);
            return RenderWithColorFilter(src, cf);
        }

        private static SKBitmap SkiaSaturation(SKBitmap src, double factor)
        {
            // Saturation matrix using luminance weights
            float s = (float)factor;
            float sr = (1f - s) * 0.299f;
            float sg = (1f - s) * 0.587f;
            float sb = (1f - s) * 0.114f;
            float[] matrix = new float[]
            {
                sr + s, sg,     sb,     0, 0,
                sr,     sg + s, sb,     0, 0,
                sr,     sg,     sb + s, 0, 0,
                0,      0,      0,      1, 0
            };
            using var cf = SKColorFilter.CreateColorMatrix(matrix);
            return RenderWithColorFilter(src, cf);
        }

        private static SKBitmap SkiaGammaCorrect(SKBitmap src, double gamma)
        {
            // Build gamma lookup table
            float g = (float)(1.0 / Math.Max(0.01, gamma));
            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                lut[i] = (byte)Math.Clamp((int)(Math.Pow(i / 255.0, g) * 255.0 + 0.5), 0, 255);
            }
            using var cf = SKColorFilter.CreateTable(null, lut, lut, lut);
            return RenderWithColorFilter(src, cf);
        }

        // ═══════════════════════════════════════════════════════
        // POSTERIZE / THRESHOLD
        // ═══════════════════════════════════════════════════════

        private static SKBitmap SkiaPosterize(SKBitmap src, int levels)
        {
            levels = Math.Clamp(levels, 2, 256);
            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                int step = (int)((double)i / 255 * (levels - 1) + 0.5);
                lut[i] = (byte)(step * 255 / (levels - 1));
            }
            using var cf = SKColorFilter.CreateTable(null, lut, lut, lut);
            return RenderWithColorFilter(src, cf);
        }

        private static SKBitmap SkiaThreshold(SKBitmap src, double threshold)
        {
            byte thresh = (byte)(threshold * 255);
            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                // Convert to luminance first using avg, then threshold
                lut[i] = (byte)(i >= thresh ? 255 : 0);
            }
            // First convert to grayscale, then apply threshold
            using var grayCf = SKColorFilter.CreateColorMatrix(GrayscaleMatrix);
            var gray = RenderWithColorFilter(src, grayCf);
            using var threshCf = SKColorFilter.CreateTable(null, lut, lut, lut);
            var result = RenderWithColorFilter(gray, threshCf);
            gray.Dispose();
            return result;
        }

        // ═══════════════════════════════════════════════════════
        // PIXELATE
        // ═══════════════════════════════════════════════════════

        private static SKBitmap SkiaPixelate(SKBitmap src, int blockSize)
        {
            blockSize = Math.Max(2, blockSize);
            int w = src.Width, h = src.Height;
            int smallW = Math.Max(1, w / blockSize);
            int smallH = Math.Max(1, h / blockSize);

            // Scale down
            var smallInfo = new SKImageInfo(smallW, smallH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var small = src.Resize(smallInfo, SKFilterQuality.Low);

            // Scale back up with nearest neighbor
            var resultInfo = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            var result = new SKBitmap(resultInfo);
            using var canvas = new SKCanvas(result);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.None }; // Nearest neighbor
            canvas.DrawBitmap(small, new SKRect(0, 0, w, h), paint);
            return result;
        }

        // ═══════════════════════════════════════════════════════
        // NEW: DROP SHADOW
        // ═══════════════════════════════════════════════════════

        private static SKBitmap SkiaDropShadow(SKBitmap src, double offsetX, double offsetY, double sigmaX, double sigmaY, int shadowAlpha)
        {
            int w = src.Width, h = src.Height;
            // Expand canvas to accommodate shadow
            int expandX = (int)(Math.Abs(offsetX) + sigmaX * 3);
            int expandY = (int)(Math.Abs(offsetY) + sigmaY * 3);
            int newW = w + expandX * 2;
            int newH = h + expandY * 2;

            var resultInfo = new SKImageInfo(newW, newH, SKColorType.Bgra8888, SKAlphaType.Premul);
            var result = new SKBitmap(resultInfo);
            using var canvas = new SKCanvas(result);
            canvas.Clear(SKColors.Transparent);

            using var shadowFilter = SKImageFilter.CreateDropShadow(
                (float)offsetX, (float)offsetY,
                (float)sigmaX, (float)sigmaY,
                new SKColor(0, 0, 0, (byte)Math.Clamp(shadowAlpha, 0, 255)));

            using var paint = new SKPaint { ImageFilter = shadowFilter };
            canvas.DrawBitmap(src, expandX, expandY, paint);

            // If result is bigger than original, crop to original size centered
            if (newW != w || newH != h)
            {
                var cropped = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
                using var cropCanvas = new SKCanvas(cropped);
                cropCanvas.Clear(SKColors.Transparent);
                cropCanvas.DrawBitmap(result, -expandX, -expandY);
                result.Dispose();
                return cropped;
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════
        // NEW: CUSTOM COLOR MATRIX
        // ═══════════════════════════════════════════════════════

        private static SKBitmap SkiaCustomColorMatrix(SKBitmap src,
            float rr, float rg, float rb, float ra, float rBias,
            float gr, float gg, float gb, float ga, float gBias,
            float br, float bg, float bb, float ba, float bBias,
            float ar, float ag, float ab, float aa, float aBias)
        {
            float[] matrix = new float[]
            {
                rr, rg, rb, ra, rBias,
                gr, gg, gb, ga, gBias,
                br, bg, bb, ba, bBias,
                ar, ag, ab, aa, aBias
            };
            using var cf = SKColorFilter.CreateColorMatrix(matrix);
            return RenderWithColorFilter(src, cf);
        }

        // ═══════════════════════════════════════════════════════
        // NEW: HUE ROTATE
        // ═══════════════════════════════════════════════════════

        private static SKBitmap SkiaHueRotate(SKBitmap src, double degrees)
        {
            // Hue rotation in RGB space using rotation matrix
            double rad = degrees * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);

            // Rotation around the (1,1,1) axis in RGB space
            float a00 = 0.213f + cos * 0.787f - sin * 0.213f;
            float a01 = 0.715f - cos * 0.715f - sin * 0.715f;
            float a02 = 0.072f - cos * 0.072f + sin * 0.928f;

            float a10 = 0.213f - cos * 0.213f + sin * 0.143f;
            float a11 = 0.715f + cos * 0.285f + sin * 0.140f;
            float a12 = 0.072f - cos * 0.072f - sin * 0.283f;

            float a20 = 0.213f - cos * 0.213f - sin * 0.787f;
            float a21 = 0.715f - cos * 0.715f + sin * 0.715f;
            float a22 = 0.072f + cos * 0.928f + sin * 0.072f;

            float[] matrix = new float[]
            {
                a00, a01, a02, 0, 0,
                a10, a11, a12, 0, 0,
                a20, a21, a22, 0, 0,
                0,   0,   0,   1, 0
            };
            using var cf = SKColorFilter.CreateColorMatrix(matrix);
            return RenderWithColorFilter(src, cf);
        }

        // ═══════════════════════════════════════════════════════
        // NEW: MORPHOLOGY (DILATE / ERODE)
        // ═══════════════════════════════════════════════════════

        private static SKBitmap SkiaMorphology(SKBitmap src, int radiusX, int radiusY, bool isDilate)
        {
            radiusX = Math.Max(1, radiusX);
            radiusY = Math.Max(1, radiusY);
            using var filter = isDilate
                ? SKImageFilter.CreateDilate(radiusX, radiusY)
                : SKImageFilter.CreateErode(radiusX, radiusY);
            return RenderWithFilter(src, filter);
        }

        // ═══════════════════════════════════════════════════════
        // NEW: DISTANT LIGHTING (Specular)
        // ═══════════════════════════════════════════════════════

        private static SKBitmap SkiaDistantLighting(SKBitmap src, double azimuth, double elevation, double specularExponent, double specularConstant)
        {
            // Use the source as the bump-map input
            using var srcImage = SKImage.FromBitmap(src);
            using var srcFilter = SKImageFilter.CreateImage(srcImage);

            using var lightFilter = SKImageFilter.CreateDistantLitSpecular(
                new SKPoint3((float)azimuth, (float)elevation, 1f),
                SKColors.White,
                surfaceScale: 1f,
                ks: (float)specularConstant,
                shininess: (float)specularExponent,
                input: srcFilter);

            // Composite: multiply lighting on top of original
            var info = new SKImageInfo(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var result = new SKBitmap(info);
            using var canvas = new SKCanvas(result);
            canvas.Clear(SKColors.Transparent);

            // Draw original
            canvas.DrawBitmap(src, 0, 0);

            // Draw lighting overlay with Screen blend
            using var lightPaint = new SKPaint
            {
                ImageFilter = lightFilter,
                BlendMode = SKBlendMode.Screen
            };
            canvas.DrawBitmap(src, 0, 0, lightPaint);

            return result;
        }

        // ═══════════════════════════════════════════════════════
        // NEW: BLEND MODE
        // ═══════════════════════════════════════════════════════

        private static readonly SKBlendMode[] AvailableBlendModes = new[]
        {
            SKBlendMode.Multiply,
            SKBlendMode.Screen,
            SKBlendMode.Overlay,
            SKBlendMode.Darken,
            SKBlendMode.Lighten,
            SKBlendMode.ColorDodge,
            SKBlendMode.ColorBurn,
            SKBlendMode.HardLight,
            SKBlendMode.SoftLight,
            SKBlendMode.Difference,
            SKBlendMode.Exclusion,
            SKBlendMode.Hue,
            SKBlendMode.Saturation,
            SKBlendMode.Color,
            SKBlendMode.Luminosity
        };

        internal static readonly string[] BlendModeNames = new[]
        {
            "Multiply", "Screen", "Overlay", "Darken", "Lighten",
            "Color Dodge", "Color Burn", "Hard Light", "Soft Light",
            "Difference", "Exclusion", "Hue", "Saturation", "Color", "Luminosity"
        };

        private static SKBitmap SkiaBlendModeEffect(SKBitmap src, int blendModeIndex, int overlayR, int overlayG, int overlayB, int overlayAlpha)
        {
            blendModeIndex = Math.Clamp(blendModeIndex, 0, AvailableBlendModes.Length - 1);
            var mode = AvailableBlendModes[blendModeIndex];
            byte alpha = (byte)Math.Clamp(overlayAlpha * 255 / 100, 0, 255);

            var info = new SKImageInfo(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var result = new SKBitmap(info);
            using var canvas = new SKCanvas(result);
            canvas.Clear(SKColors.Transparent);

            // Draw original
            canvas.DrawBitmap(src, 0, 0);

            // Draw color overlay with blend mode
            using var paint = new SKPaint
            {
                Color = new SKColor((byte)Math.Clamp(overlayR, 0, 255), (byte)Math.Clamp(overlayG, 0, 255), (byte)Math.Clamp(overlayB, 0, 255), alpha),
                BlendMode = mode
            };
            canvas.DrawRect(0, 0, src.Width, src.Height, paint);

            return result;
        }
    }
}
