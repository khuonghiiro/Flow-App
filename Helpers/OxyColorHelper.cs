using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace FlowMy.Helpers
{
    public static class OxyColorHelper
    {
        /// <summary>
        /// Áp dụng theme colors cho PlotModel
        /// </summary>
        public static void ApplyThemeToPlotModel(PlotModel plotModel)
        {
            if (plotModel == null) return;

            plotModel.Background = OxyColors.Transparent;
            plotModel.TextColor = GetOxyAxisColor();
            plotModel.PlotAreaBorderColor = GetOxyGridColor();

            // Update axes colors
            foreach (var axis in plotModel.Axes)
            {
                axis.TextColor = GetOxyAxisColor();
                axis.TitleColor = GetOxyAxisColor();
                axis.TicklineColor = GetOxyAxisColor();
                axis.MajorGridlineColor = GetOxyGridColor();
                axis.MinorGridlineColor = GetOxyGridColor();
            }

            // Update legend colors
            foreach (var legend in plotModel.Legends)
            {
                legend.LegendTextColor = GetOxyAxisColor();
                legend.LegendBackground = OxyColors.Transparent;
                legend.LegendBorder = OxyColors.Transparent;
            }
        }

        /// <summary>
        /// Trả về màu chữ (đen hoặc trắng) sao cho đủ contrast với background.
        /// </summary>
        public static OxyColor GetCategoryTextColor(int index)
        {
            var bg = GetCategoryColor(index);
            // Tính luminance theo WCAG
            double r = bg.R / 255.0, g = bg.G / 255.0, b = bg.B / 255.0;
            Func<double, double> lin = c => (c <= 0.03928) ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            double L = 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
            // Nếu nền tối (L < 0.5) → dùng trắng, ngược lại dùng đen
            return (L < 0.5) ? OxyColors.White : OxyColors.Black;
        }

        #region Color Helper Methods for OxyPlot

        public static OxyColor GetCategoryColor(int index)
        {
            return DynamicColorHelper.IsDarkTheme() ? ColorPaletteHelper.GetDarkColor(index) : ColorPaletteHelper.GetLightColor(index);
        }

        public static OxyColor GetPerformanceColor(int index)
        {
            return DynamicColorHelper.IsDarkTheme() ? ColorPaletteHelper.GetDarkColor(index + 5) : ColorPaletteHelper.GetLightColor(index + 5);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Tạo màu với độ trong suốt
        /// </summary>
        public static SolidColorBrush CreateTransparentBrush(Color baseColor, byte alpha)
        {
            return new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        }

        /// <summary>
        /// Tạo OxyColor với độ trong suốt
        /// </summary>
        public static OxyColor CreateTransparentOxyColor(OxyColor baseColor, byte alpha)
        {
            return OxyColor.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
        }

        /// <summary>
        /// Làm sáng/tối màu
        /// </summary>
        public static Color AdjustBrightness(Color color, float factor)
        {
            return Color.FromRgb(
                (byte)Math.Max(0, Math.Min(255, color.R * factor)),
                (byte)Math.Max(0, Math.Min(255, color.G * factor)),
                (byte)Math.Max(0, Math.Min(255, color.B * factor))
            );
        }

        /// <summary>
        /// Làm sáng/tối OxyColor
        /// </summary>
        public static OxyColor AdjustBrightness(OxyColor color, float factor)
        {
            return OxyColor.FromRgb(
                (byte)Math.Max(0, Math.Min(255, color.R * factor)),
                (byte)Math.Max(0, Math.Min(255, color.G * factor)),
                (byte)Math.Max(0, Math.Min(255, color.B * factor))
            );
        }

        /// <summary>
        /// Lấy màu tương phản
        /// </summary>
        public static SolidColorBrush GetContrastColor(Color backgroundColor)
        {
            var luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            return new SolidColorBrush(luminance > 0.5 ? Colors.Black : Colors.White);
        }

        /// <summary>
        /// Lấy OxyColor tương phản
        /// </summary>
        public static OxyColor GetContrastOxyColor(OxyColor backgroundColor)
        {
            var luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            return luminance > 0.5 ? OxyColors.Black : OxyColors.White;
        }

        /// <summary>
        /// Tạo color palette cho charts
        /// </summary>
        public static OxyColor[] CreateColorPalette(int count, bool isDark = false)
        {
            var baseColors = isDark
                ? OxyColorHelper.GetOxyQuizCategoryColors()
                : GetOxyQuizCategoryColors();

            var palette = new OxyColor[count];

            for (int i = 0; i < count; i++)
            {
                var baseIndex = i % baseColors.Length;
                var baseColor = baseColors[baseIndex];

                // Điều chỉnh độ sáng nếu cần nhiều màu hơn
                if (i >= baseColors.Length)
                {
                    var factor = 1.0f - (i / baseColors.Length * 0.2f);
                    palette[i] = AdjustBrightness(baseColor, factor);
                }
                else
                {
                    palette[i] = baseColor;
                }
            }

            return palette;
        }

        /// <summary>
        /// Tạo gradient OxyColor
        /// </summary>
        public static OxyColor[] CreateGradient(OxyColor startColor, OxyColor endColor, int steps)
        {
            var gradient = new OxyColor[steps];

            for (int i = 0; i < steps; i++)
            {
                var ratio = (float)i / (steps - 1);
                var r = (byte)(startColor.R + (endColor.R - startColor.R) * ratio);
                var g = (byte)(startColor.G + (endColor.G - startColor.G) * ratio);
                var b = (byte)(startColor.B + (endColor.B - startColor.B) * ratio);
                var a = (byte)(startColor.A + (endColor.A - startColor.A) * ratio);

                gradient[i] = OxyColor.FromArgb(a, r, g, b);
            }

            return gradient;
        }

        #endregion


        #region Advanced Color Operations

        /// <summary>
        /// Blend hai màu với nhau
        /// </summary>
        public static OxyColor BlendColors(OxyColor color1, OxyColor color2, float ratio)
        {
            ratio = Math.Max(0, Math.Min(1, ratio));

            var r = (byte)(color1.R * (1 - ratio) + color2.R * ratio);
            var g = (byte)(color1.G * (1 - ratio) + color2.G * ratio);
            var b = (byte)(color1.B * (1 - ratio) + color2.B * ratio);
            var a = (byte)(color1.A * (1 - ratio) + color2.A * ratio);

            return OxyColor.FromArgb(a, r, g, b);
        }

        /// <summary>
        /// Tạo màu ngẫu nhiên trong khoảng cho trước
        /// </summary>
        public static OxyColor GenerateRandomColor(Random random = null)
        {
            random ??= new Random();

            if (DynamicColorHelper.IsDarkTheme())
            {
                // Màu sáng hơn cho dark theme
                return OxyColor.FromRgb(
                    (byte)random.Next(100, 255),
                    (byte)random.Next(100, 255),
                    (byte)random.Next(100, 255)
                );
            }
            else
            {
                // Màu tối hơn cho light theme
                return OxyColor.FromRgb(
                    (byte)random.Next(50, 200),
                    (byte)random.Next(50, 200),
                    (byte)random.Next(50, 200)
                );
            }
        }

        /// <summary>
        /// Tạo màu complementary
        /// </summary>
        public static OxyColor GetComplementaryColor(OxyColor baseColor)
        {
            return OxyColor.FromRgb(
                (byte)(255 - baseColor.R),
                (byte)(255 - baseColor.G),
                (byte)(255 - baseColor.B)
            );
        }

        /// <summary>
        /// Tạo màu analogous (các màu gần nhau trên color wheel)
        /// </summary>
        public static OxyColor[] GetAnalogousColors(OxyColor baseColor, int count = 3)
        {
            var colors = new OxyColor[count];
            colors[0] = baseColor;

            // Convert to HSV for easier manipulation
            ColorToHsv(baseColor, out double h, out double s, out double v);

            for (int i = 1; i < count; i++)
            {
                var newH = (h + (i * 30)) % 360; // 30 degrees apart
                colors[i] = HsvToColor(newH, s, v);
            }

            return colors;
        }

        /// <summary>
        /// Convert RGB to HSV
        /// </summary>
        private static void ColorToHsv(OxyColor color, out double hue, out double saturation, out double value)
        {
            var r = color.R / 255.0;
            var g = color.G / 255.0;
            var b = color.B / 255.0;

            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;

            // Hue
            if (delta == 0)
                hue = 0;
            else if (max == r)
                hue = 60 * (((g - b) / delta) % 6);
            else if (max == g)
                hue = 60 * (((b - r) / delta) + 2);
            else
                hue = 60 * (((r - g) / delta) + 4);

            if (hue < 0) hue += 360;

            // Saturation
            saturation = max == 0 ? 0 : delta / max;

            // Value
            value = max;
        }

        /// <summary>
        /// Convert HSV to RGB
        /// </summary>
        private static OxyColor HsvToColor(double hue, double saturation, double value)
        {
            var c = value * saturation;
            var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            var m = value - c;

            double r, g, b;

            if (hue < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (hue < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (hue < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (hue < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (hue < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }

            return OxyColor.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }

        #endregion

        #region Performance Optimization

        private static readonly Dictionary<string, OxyColor> _colorCache = new Dictionary<string, OxyColor>();
        private static bool _isDarkThemeCache;
        private static DateTime _lastThemeCheck = DateTime.MinValue;
        private static readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Get cached theme state
        /// </summary>
        private static bool GetCachedThemeState()
        {
            if (DateTime.Now - _lastThemeCheck > _cacheTimeout)
            {
                _isDarkThemeCache = DynamicColorHelper.IsDarkTheme();
                _lastThemeCheck = DateTime.Now;
                _colorCache.Clear(); // Clear color cache when theme might have changed
            }

            return _isDarkThemeCache;
        }

        /// <summary>
        /// Get cached color
        /// </summary>
        private static OxyColor GetCachedColor(string key, Func<OxyColor> colorProvider)
        {
            var cacheKey = $"{key}_{GetCachedThemeState()}";

            if (!_colorCache.TryGetValue(cacheKey, out var color))
            {
                color = colorProvider();
                _colorCache[cacheKey] = color;
            }

            return color;
        }

        /// <summary>
        /// Clear color cache (call when theme changes)
        /// </summary>
        public static void ClearColorCache()
        {
            _colorCache.Clear();
            _lastThemeCheck = DateTime.MinValue;
        }

        #endregion

         #region OxyPlot Colors - For Chart Library

        /// <summary>
        /// Màu OxyPlot cho Pie Chart (Quiz Categories)
        /// </summary>
        public static OxyColor[] GetOxyQuizCategoryColors()
        {
            if (DynamicColorHelper.IsDarkTheme())
            {
                return new OxyColor[]
                {
                    OxyColor.FromRgb(255, 107, 107),  // Coral Red
                    OxyColor.FromRgb(78, 205, 196),   // Turquoise  
                    OxyColor.FromRgb(255, 206, 84),   // Sunny Yellow
                    OxyColor.FromRgb(162, 155, 254),  // Lavender
                    OxyColor.FromRgb(255, 159, 67),   // Orange
                    OxyColor.FromRgb(116, 185, 255)   // Sky Blue
                };
            }
            else
            {
                return new OxyColor[]
                {
                    OxyColor.FromRgb(231, 76, 60),    // Alizarin
                    OxyColor.FromRgb(26, 188, 156),   // Turquoise Dark
                    OxyColor.FromRgb(241, 196, 15),   // Sun Flower
                    OxyColor.FromRgb(142, 68, 173),   // Wisteria
                    OxyColor.FromRgb(230, 126, 34),   // Orange Dark
                    OxyColor.FromRgb(52, 152, 219)    // Peter River
                };
            }
        }

        /// <summary>
        /// Màu OxyPlot cho axes và text
        /// </summary>
        public static OxyColor GetOxyAxisColor()
        {
            return DynamicColorHelper.IsDarkTheme()
                ? OxyColor.FromRgb(200, 200, 200)   // Light gray for text on dark background
                : OxyColor.FromRgb(64, 64, 64);     // Dark gray for text on light background
        }

        /// <summary>
        /// Màu OxyPlot cho grid lines
        /// </summary>
        public static OxyColor GetOxyGridColor()
        {
            return DynamicColorHelper.IsDarkTheme()
                ? OxyColor.FromArgb(50, 200, 200, 200)  // Semi-transparent light gray
                : OxyColor.FromArgb(50, 64, 64, 64);    // Semi-transparent dark gray
        }

        #endregion

        #region Top Performers Colors

        /// <summary>
        /// Màu cho rank của top performers
        /// </summary>
        public static SolidColorBrush GetRankColor(int rank)
        {
            return rank switch
            {
                1 => new SolidColorBrush(Color.FromRgb(255, 215, 0)),    // Gold
                2 => new SolidColorBrush(Color.FromRgb(192, 192, 192)),  // Silver
                3 => new SolidColorBrush(Color.FromRgb(205, 127, 50)),   // Bronze
                _ => DynamicColorHelper.IsDarkTheme()
                    ? new SolidColorBrush(Color.FromRgb(108, 117, 125))  // Blue Grey - Dark
                    : new SolidColorBrush(Color.FromRgb(69, 90, 100))    // Darker Blue Grey - Light
            };
        }

        /// <summary>
        /// Màu avatar cho top performers
        /// </summary>
        public static SolidColorBrush GetAvatarColor(int rank)
        {
            if (DynamicColorHelper.IsDarkTheme())
            {
                return rank switch
                {
                    1 => new SolidColorBrush(Color.FromRgb(255, 215, 0)),    // Gold
                    2 => new SolidColorBrush(Color.FromRgb(192, 192, 192)),  // Silver
                    3 => new SolidColorBrush(Color.FromRgb(205, 127, 50)),   // Bronze
                    4 => new SolidColorBrush(Color.FromRgb(52, 152, 219)),   // Peter River
                    5 => new SolidColorBrush(Color.FromRgb(46, 204, 113)),   // Emerald
                    6 => new SolidColorBrush(Color.FromRgb(155, 89, 182)),   // Amethyst
                    _ => new SolidColorBrush(Color.FromRgb(230, 126, 34))    // Orange
                };
            }
            else
            {
                return rank switch
                {
                    1 => new SolidColorBrush(Color.FromRgb(241, 196, 15)),   // Sun Flower
                    2 => new SolidColorBrush(Color.FromRgb(149, 165, 166)),  // Concrete
                    3 => new SolidColorBrush(Color.FromRgb(171, 97, 35)),    // Dark Bronze
                    4 => new SolidColorBrush(Color.FromRgb(41, 128, 185)),   // Belize Hole
                    5 => new SolidColorBrush(Color.FromRgb(39, 174, 96)),    // Nephritis
                    6 => new SolidColorBrush(Color.FromRgb(142, 68, 173)),   // Wisteria
                    _ => new SolidColorBrush(Color.FromRgb(211, 84, 0))      // Dark Orange
                };
            }
        }

        #endregion
    }
}
