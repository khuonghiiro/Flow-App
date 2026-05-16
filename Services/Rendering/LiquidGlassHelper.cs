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
        /// Áp dụng hiệu ứng Liquid Glass lên một Border đã có sẵn.
        /// Gọi sau khi tạo border với background = NodeBrush (solid).
        /// </summary>
        /// <param name="border">Border của node</param>
        /// <param name="baseColor">Màu gốc của node (từ NodeBrush)</param>
        public static void ApplyToExistingBorder(Border border, Color baseColor)
        {
            // Nền gradient bán trong suốt
            border.Background = new LinearGradientBrush(
                Color.FromArgb(80, baseColor.R, baseColor.G, baseColor.B),
                Color.FromArgb(35, baseColor.R, baseColor.G, baseColor.B),
                45.0);

            // Viền trắng mờ (glass edge)
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            border.BorderThickness = new Thickness(1.2);

            // Glow shadow từ màu node
            border.Effect = new DropShadowEffect
            {
                Color = baseColor,
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.35,
                Direction = 0
            };
        }

        /// <summary>
        /// Tạo Brush nền Liquid Glass từ màu gốc.
        /// </summary>
        public static Brush CreateGlassBackground(Color baseColor)
        {
            return new LinearGradientBrush(
                Color.FromArgb(80, baseColor.R, baseColor.G, baseColor.B),
                Color.FromArgb(35, baseColor.R, baseColor.G, baseColor.B),
                45.0);
        }

        /// <summary>
        /// Tạo BorderBrush cho Liquid Glass.
        /// </summary>
        public static Brush CreateGlassBorderBrush()
        {
            return new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
        }

        /// <summary>
        /// Tạo Effect (glow) cho Liquid Glass.
        /// </summary>
        public static Effect CreateGlassEffect(Color baseColor)
        {
            return new DropShadowEffect
            {
                Color = baseColor,
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.35,
                Direction = 0
            };
        }

        /// <summary>
        /// Tạo text foreground phù hợp cho Liquid Glass (trắng + shadow nhẹ).
        /// </summary>
        public static void ApplyGlassTextStyle(TextBlock textBlock)
        {
            textBlock.Foreground = new SolidColorBrush(Colors.White);
            textBlock.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = 0.5
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
    }
}
