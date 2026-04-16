using System.Windows;
using System.Windows.Media;
using OxyPlot;

namespace FlowMy.Helpers
{
    public static class ColorPaletteHelper
    {
        // Hàm làm nhạt màu (brighten) 20% dựa trên màu đậm
        private static OxyColor Lighten(OxyColor darkColor)
        {
            byte r = (byte)(darkColor.R + (255 - darkColor.R) * 0.2);
            byte g = (byte)(darkColor.G + (255 - darkColor.G) * 0.2);
            byte b = (byte)(darkColor.B + (255 - darkColor.B) * 0.2);
            return OxyColor.FromRgb(r, g, b);
        }

        // Danh sách màu đậm
        private static readonly OxyColor[] DarkColors = new[]
        {
            OxyColor.FromRgb(56, 142, 60),   // Xanh lá
            OxyColor.FromRgb(30, 136, 229),  // Xanh dương
            OxyColor.FromRgb(216, 27, 96),   // Hồng
            OxyColor.FromRgb(255, 112, 67),  // Cam nhạt
            OxyColor.FromRgb(253, 216, 53),  // Vàng
            OxyColor.FromRgb(103, 58, 183),  // Tím
            OxyColor.FromRgb(0, 151, 167),   // Cyan
            OxyColor.FromRgb(255, 87, 34),   // Cam đỏ

            OxyColor.FromRgb(121, 85, 72),    // Nâu đất
            OxyColor.FromRgb(0, 188, 212),    // Xanh ngọc
            OxyColor.FromRgb(63, 81, 181),    // Indigo
            OxyColor.FromRgb(244, 67, 54),    // Đỏ tươi
            OxyColor.FromRgb(139, 195, 74),   // Xanh lá non
            OxyColor.FromRgb(0, 172, 193),    // Xanh biển đậm
            OxyColor.FromRgb(255, 193, 7),    // Vàng đậm
            OxyColor.FromRgb(156, 39, 176),   // Tím sáng

            OxyColor.FromRgb(255, 235, 59),   // Vàng sáng
            OxyColor.FromRgb(205, 220, 57),   // Vàng chanh
            OxyColor.FromRgb(0, 200, 83),     // Xanh lá sáng
            OxyColor.FromRgb(100, 221, 23),   // Xanh lá chuối
            OxyColor.FromRgb(41, 182, 246),   // Xanh baby
            OxyColor.FromRgb(3, 169, 244),    // Xanh nước biển
            OxyColor.FromRgb(126, 87, 194),   // Tím nhạt
            OxyColor.FromRgb(233, 30, 99),    // Hồng đậm
            OxyColor.FromRgb(255, 152, 0),    // Cam sáng
            OxyColor.FromRgb(255, 202, 40),   // Vàng nghệ
        };

        // Danh sách màu light/dark cặp
        public static readonly (OxyColor light, OxyColor dark)[] ColorPairs =
            DarkColors.Select(dark => (Lighten(dark), dark)).ToArray();

        /// <summary>
        /// Chỉ lấy màu đậm (cho text xám).
        /// </summary>
        public static OxyColor GetDarkColor(int index)
            => ColorPairs[index % ColorPairs.Length].dark;

        /// <summary>
        /// Chỉ lấy màu nhạt (cho text đen).
        /// </summary>
        public static OxyColor GetLightColor(int index)
            => ColorPairs[index % ColorPairs.Length].light;

        #region Lấy màu từ từ ResourceDictionary

        /// <summary>
        /// Lấy màu từ ResourceDictionary
        /// </summary>
        /// <param name="keyColor">x:key name từ ResourceDictionary</param>
        /// <returns></returns>
        public static Color GetColorKeyResource(string keyColor)
        {
            var brush = (SolidColorBrush)Application.Current.Resources[keyColor];
            Color color = brush.Color;
            return color;
        }

        /// <summary>
        /// Đổi màu từ Color sang OxyColor
        /// </summary>
        /// <param name="keyColor">x:key name từ ResourceDictionary</param>
        /// <returns></returns>
        public static OxyColor ToOxyColorFromKeyResource(string keyColor)
        {
            Color mediaColor = GetColorKeyResource(keyColor);
            // Chuyển sang OxyColor
            OxyColor oxyColor = ToOxyColor(mediaColor);
            return oxyColor;
        }

        #endregion

        #region Color Conversion Utilities

        /// <summary>
        /// Chuyển đổi từ WPF Color sang OxyColor
        /// </summary>
        public static OxyColor ToOxyColor(Color wpfColor)
        {
            return OxyColor.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
        }

        /// <summary>
        /// Chuyển đổi từ SolidColorBrush sang OxyColor
        /// </summary>
        public static OxyColor ToOxyColor(SolidColorBrush brush)
        {
            var color = brush.Color;
            return OxyColor.FromArgb(color.A, color.R, color.G, color.B);
        }

        /// <summary>
        /// Chuyển đổi từ OxyColor sang WPF Color
        /// </summary>
        public static Color ToWpfColor(OxyColor oxyColor)
        {
            return Color.FromArgb(oxyColor.A, oxyColor.R, oxyColor.G, oxyColor.B);
        }

        /// <summary>
        /// Chuyển đổi từ OxyColor sang SolidColorBrush
        /// </summary>
        public static SolidColorBrush ToSolidColorBrush(OxyColor oxyColor)
        {
            return new SolidColorBrush(ToWpfColor(oxyColor));
        }

        #endregion
    }
}
