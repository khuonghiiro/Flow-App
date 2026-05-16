using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FlowMy.Services.Interaction;

namespace FlowMy.Services.Rendering
{
    /// <summary>
    /// Helper tạo hiệu ứng Liquid Glass (Kính Lỏng) cho node border.
    /// Khi NodeAppearanceMode = "LiquidGlass", node sẽ hiển thị nền bán trong suốt
    /// với gradient tint nhẹ từ màu gốc, viền trắng mờ và glow shadow.
    /// </summary>
    public static class LiquidGlassHelper
    {
        /// <summary>
        /// Kiểm tra xem host hiện tại có đang ở chế độ Liquid Glass không.
        /// </summary>
        public static bool IsLiquidGlassMode(IWorkflowEditorHost host)
        {
            return string.Equals(host.NodeAppearanceMode, "LiquidGlass", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kiểm tra theme hiện tại có phải theme sáng không.
        /// Theme sáng: Light, SoftLight, Modern → icon/text dùng màu đen.
        /// Theme tối: Dark, SoftDark, Dracula, Monokai, Night → icon/text dùng màu trắng.
        /// </summary>
        public static bool IsCurrentThemeLight()
        {
            try
            {
                // Detect theme bằng cách check WindowBackgroundBrush luminance
                // Theme sáng có nền sáng, theme tối có nền tối
                var bgBrush = Application.Current?.TryFindResource("WindowBackgroundBrush") as SolidColorBrush;
                if (bgBrush != null)
                {
                    var c = bgBrush.Color;
                    var luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                    return luminance > 0.5;
                }

                // Fallback: check CanvasBackgroundBrush
                var canvasBrush = Application.Current?.TryFindResource("CanvasBackgroundBrush") as SolidColorBrush;
                if (canvasBrush != null)
                {
                    var c = canvasBrush.Color;
                    var luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                    return luminance > 0.5;
                }

                return false;
            }
            catch { return false; }
        }

        /// <summary>
        /// Lấy màu icon/text phù hợp cho Liquid Glass theo theme hiện tại.
        /// Theme sáng → đen, theme tối → trắng.
        /// </summary>
        public static Color GetGlassIconColor()
        {
            return IsCurrentThemeLight() ? Colors.Black : Colors.White;
        }

        /// <summary>
        /// Lấy Brush icon/text phù hợp cho Liquid Glass theo theme hiện tại.
        /// </summary>
        public static Brush GetGlassIconBrush()
        {
            return new SolidColorBrush(GetGlassIconColor());
        }

        /// <summary>
        /// Áp dụng hiệu ứng Liquid Glass lên một Border đã có sẵn.
        /// Tự động detect node màu sáng và tăng contrast.
        /// </summary>
        public static void ApplyToExistingBorder(Border border, Color baseColor)
        {
            // Detect nếu màu quá sáng (trắng, vàng nhạt, v.v.) → tăng tint để nhìn rõ trên nền sáng
            var isLightColor = IsLightColor(baseColor);

            // Nền gradient bán trong suốt
            border.Background = CreateGlassBackground(baseColor);

            // Viền: nếu màu sáng → dùng viền tối hơn để tạo contrast
            border.BorderBrush = isLightColor
                ? new SolidColorBrush(Color.FromArgb(120, 100, 100, 100))
                : CreateGlassBorderBrush();
            border.BorderThickness = new Thickness(1.5);

            // Shadow: glow từ màu node + drop shadow nhẹ để tạo chiều sâu
            border.Effect = CreateGlassEffect(baseColor, isLightColor);
        }

        /// <summary>
        /// Tạo Brush nền Liquid Glass từ màu gốc (trạng thái bình thường).
        /// </summary>
        public static Brush CreateGlassBackground(Color baseColor)
        {
            var isLight = IsLightColor(baseColor);

            byte alphaTop = isLight ? (byte)100 : (byte)80;
            byte alphaBottom = isLight ? (byte)55 : (byte)35;

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            gradient.GradientStops.Add(new GradientStop(
                Color.FromArgb(alphaTop, baseColor.R, baseColor.G, baseColor.B), 0.0));
            gradient.GradientStops.Add(new GradientStop(
                Color.FromArgb(alphaBottom, baseColor.R, baseColor.G, baseColor.B), 0.7));
            gradient.GradientStops.Add(new GradientStop(
                Color.FromArgb((byte)(alphaTop + 20), 255, 255, 255), 1.0));

            return gradient;
        }

        /// <summary>
        /// Tạo Brush nền Liquid Glass khi hover (sáng hơn, rõ hơn).
        /// </summary>
        public static Brush CreateGlassHoverBackground(Color baseColor)
        {
            var isLight = IsLightColor(baseColor);

            byte alphaTop = isLight ? (byte)140 : (byte)120;
            byte alphaBottom = isLight ? (byte)80 : (byte)60;

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            gradient.GradientStops.Add(new GradientStop(
                Color.FromArgb(alphaTop, baseColor.R, baseColor.G, baseColor.B), 0.0));
            gradient.GradientStops.Add(new GradientStop(
                Color.FromArgb(alphaBottom, baseColor.R, baseColor.G, baseColor.B), 0.6));
            gradient.GradientStops.Add(new GradientStop(
                Color.FromArgb((byte)(alphaTop + 30), 255, 255, 255), 1.0));

            return gradient;
        }

        /// <summary>
        /// Tạo BorderBrush cho Liquid Glass (trạng thái bình thường).
        /// </summary>
        public static Brush CreateGlassBorderBrush()
        {
            return new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
        }

        /// <summary>
        /// Tạo BorderBrush cho Liquid Glass khi hover (sáng hơn).
        /// </summary>
        public static Brush CreateGlassHoverBorderBrush()
        {
            return new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
        }

        /// <summary>
        /// Tạo Effect (glow + shadow) cho Liquid Glass.
        /// </summary>
        public static Effect CreateGlassEffect(Color baseColor, bool isLightColor = false)
        {
            return new DropShadowEffect
            {
                Color = isLightColor
                    ? Color.FromRgb(
                        (byte)System.Math.Max(0, baseColor.R - 60),
                        (byte)System.Math.Max(0, baseColor.G - 60),
                        (byte)System.Math.Max(0, baseColor.B - 60))
                    : baseColor,
                BlurRadius = 22,
                ShadowDepth = 2,
                Opacity = isLightColor ? 0.45 : 0.5,
                Direction = 270
            };
        }

        /// <summary>
        /// Tạo text foreground phù hợp cho Liquid Glass.
        /// Theme sáng → đen + shadow trắng, theme tối → trắng + shadow đen.
        /// </summary>
        public static void ApplyGlassTextStyle(TextBlock textBlock)
        {
            var isLightTheme = IsCurrentThemeLight();
            textBlock.Foreground = new SolidColorBrush(isLightTheme ? Colors.Black : Colors.White);
            textBlock.Effect = new DropShadowEffect
            {
                Color = isLightTheme ? Colors.White : Colors.Black,
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = isLightTheme ? 0.8 : 0.6
            };
        }

        /// <summary>
        /// Lấy Color từ Brush (hỗ trợ SolidColorBrush và LinearGradientBrush).
        /// </summary>
        public static Color GetColorFromBrush(Brush brush)
        {
            if (brush is SolidColorBrush scb) return scb.Color;
            if (brush is LinearGradientBrush lgb && lgb.GradientStops.Count > 0)
                return lgb.GradientStops[0].Color;
            return Colors.Gray;
        }

        /// <summary>
        /// Kiểm tra xem màu có "sáng" không (luminance cao).
        /// </summary>
        private static bool IsLightColor(Color color)
        {
            var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            return luminance > 0.65;
        }
    }
}
