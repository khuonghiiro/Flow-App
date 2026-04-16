using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace FlowMy.Helpers
{
    public static class AdditionalColorHelpers
    {
        #region Animation Colors

        /// <summary>
        /// Tạo màu fade in/out cho animations
        /// </summary>
        public static SolidColorBrush GetFadeColor(double opacity = 0.5)
        {
            var isDark = DynamicColorHelper.IsDarkTheme();
            var baseColor = isDark ? Colors.White : Colors.Black;

            return new SolidColorBrush(Color.FromArgb(
                (byte)(255 * opacity),
                baseColor.R,
                baseColor.G,
                baseColor.B
            ));
        }

        /// <summary>
        /// Tạo màu highlight cho hover effects
        /// </summary>
        public static SolidColorBrush GetHoverHighlightColor()
        {
            return DynamicColorHelper.IsDarkTheme()
                ? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))  // Semi-transparent white
                : new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));       // Semi-transparent black
        }

        #endregion

        #region Status Colors

        /// <summary>
        /// Màu cho các trạng thái online/offline
        /// </summary>
        public static SolidColorBrush GetOnlineStatusColor(bool isOnline)
        {
            if (isOnline)
            {
                return DynamicColorHelper.IsDarkTheme()
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // Green - Dark
                    : new SolidColorBrush(Color.FromRgb(25, 135, 84)); // Darker Green - Light
            }
            else
            {
                return DynamicColorHelper.IsDarkTheme()
                    ? new SolidColorBrush(Color.FromRgb(158, 158, 158)) // Grey - Dark
                    : new SolidColorBrush(Color.FromRgb(108, 117, 125)); // Darker Grey - Light
            }
        }

        /// <summary>
        /// Màu cho difficulty levels
        /// </summary>
        public static SolidColorBrush GetDifficultyColor(string difficulty)
        {
            var isDark = DynamicColorHelper.IsDarkTheme();

            return difficulty.ToLower() switch
            {
                "easy" or "dễ" => isDark
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // Green
                    : new SolidColorBrush(Color.FromRgb(25, 135, 84)),  // Darker Green

                "medium" or "trung bình" => isDark
                    ? new SolidColorBrush(Color.FromRgb(255, 193, 7))   // Yellow
                    : new SolidColorBrush(Color.FromRgb(255, 143, 0)),  // Darker Yellow

                "hard" or "khó" => isDark
                    ? new SolidColorBrush(Color.FromRgb(244, 67, 54))   // Red
                    : new SolidColorBrush(Color.FromRgb(198, 40, 40)),  // Darker Red

                _ => isDark
                    ? new SolidColorBrush(Color.FromRgb(158, 158, 158)) // Default Grey
                    : new SolidColorBrush(Color.FromRgb(108, 117, 125))
            };
        }

        #endregion

        #region Score Colors

        /// <summary>
        /// Màu dựa trên điểm số (0-10)
        /// </summary>
        public static SolidColorBrush GetScoreColor(double score)
        {
            var isDark = DynamicColorHelper.IsDarkTheme();

            return score switch
            {
                >= 9.0 => isDark ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : new SolidColorBrush(Color.FromRgb(25, 135, 84)),      // Excellent - Green
                >= 8.0 => isDark ? new SolidColorBrush(Color.FromRgb(139, 195, 74)) : new SolidColorBrush(Color.FromRgb(104, 159, 56)),   // Good - Light Green
                >= 7.0 => isDark ? new SolidColorBrush(Color.FromRgb(255, 193, 7)) : new SolidColorBrush(Color.FromRgb(251, 140, 0)),     // Fair - Yellow
                >= 6.0 => isDark ? new SolidColorBrush(Color.FromRgb(255, 152, 0)) : new SolidColorBrush(Color.FromRgb(230, 81, 0)),      // Average - Orange
                _ => isDark ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) : new SolidColorBrush(Color.FromRgb(198, 40, 40))           // Poor - Red
            };
        }

        /// <summary>
        /// Màu progress bar dựa trên phần trăm hoàn thành
        /// </summary>
        public static SolidColorBrush GetProgressColor(double percentage)
        {
            var isDark = DynamicColorHelper.IsDarkTheme();

            return percentage switch
            {
                >= 80 => isDark ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : new SolidColorBrush(Color.FromRgb(25, 135, 84)),      // High - Green
                >= 60 => isDark ? new SolidColorBrush(Color.FromRgb(139, 195, 74)) : new SolidColorBrush(Color.FromRgb(104, 159, 56)),   // Medium-High - Light Green
                >= 40 => isDark ? new SolidColorBrush(Color.FromRgb(255, 193, 7)) : new SolidColorBrush(Color.FromRgb(251, 140, 0)),     // Medium - Yellow
                >= 20 => isDark ? new SolidColorBrush(Color.FromRgb(255, 152, 0)) : new SolidColorBrush(Color.FromRgb(230, 81, 0)),      // Low - Orange
                _ => isDark ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) : new SolidColorBrush(Color.FromRgb(198, 40, 40))           // Very Low - Red
            };
        }

        #endregion

        #region Priority Colors

        /// <summary>
        /// Màu cho mức độ ưu tiên
        /// </summary>
        public static SolidColorBrush GetPriorityColor(string priority)
        {
            var isDark = DynamicColorHelper.IsDarkTheme();

            return priority.ToLower() switch
            {
                "high" or "cao" or "urgent" or "khẩn cấp" => isDark
                    ? new SolidColorBrush(Color.FromRgb(244, 67, 54))   // Red
                    : new SolidColorBrush(Color.FromRgb(198, 40, 40)),  // Darker Red

                "medium" or "trung bình" or "normal" or "bình thường" => isDark
                    ? new SolidColorBrush(Color.FromRgb(255, 152, 0))   // Orange
                    : new SolidColorBrush(Color.FromRgb(230, 81, 0)),   // Darker Orange

                "low" or "thấp" => isDark
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // Green
                    : new SolidColorBrush(Color.FromRgb(25, 135, 84)),  // Darker Green

                _ => isDark
                    ? new SolidColorBrush(Color.FromRgb(158, 158, 158)) // Default Grey
                    : new SolidColorBrush(Color.FromRgb(108, 117, 125))
            };
        }

        #endregion

        #region Category Colors

        /// <summary>
        /// Bộ màu cho categories (có thể mở rộng thêm)
        /// </summary>
        public static SolidColorBrush[] GetExtendedCategoryColors()
        {
            var isDark = DynamicColorHelper.IsDarkTheme();

            if (isDark)
            {
                return new SolidColorBrush[]
                {
                    new SolidColorBrush(Color.FromRgb(255, 99, 132)),   // Pink
                    new SolidColorBrush(Color.FromRgb(54, 162, 235)),   // Blue
                    new SolidColorBrush(Color.FromRgb(255, 206, 86)),   // Yellow
                    new SolidColorBrush(Color.FromRgb(75, 192, 192)),   // Teal
                    new SolidColorBrush(Color.FromRgb(153, 102, 255)),  // Purple
                    new SolidColorBrush(Color.FromRgb(255, 159, 64)),   // Orange
                    new SolidColorBrush(Color.FromRgb(199, 199, 199)),  // Grey
                    new SolidColorBrush(Color.FromRgb(83, 102, 255)),   // Indigo
                    new SolidColorBrush(Color.FromRgb(255, 99, 255)),   // Magenta
                    new SolidColorBrush(Color.FromRgb(99, 255, 132)),   // Light Green
                };
            }
            else
            {
                return new SolidColorBrush[]
                {
                    new SolidColorBrush(Color.FromRgb(220, 53, 69)),    // Darker Pink
                    new SolidColorBrush(Color.FromRgb(13, 110, 253)),   // Darker Blue
                    new SolidColorBrush(Color.FromRgb(255, 193, 7)),    // Darker Yellow
                    new SolidColorBrush(Color.FromRgb(32, 201, 151)),   // Darker Teal
                    new SolidColorBrush(Color.FromRgb(111, 66, 193)),   // Darker Purple
                    new SolidColorBrush(Color.FromRgb(253, 126, 20)),   // Darker Orange
                    new SolidColorBrush(Color.FromRgb(108, 117, 125)),  // Darker Grey
                    new SolidColorBrush(Color.FromRgb(54, 69, 191)),    // Darker Indigo
                    new SolidColorBrush(Color.FromRgb(214, 51, 132)),   // Darker Magenta
                    new SolidColorBrush(Color.FromRgb(40, 167, 69)),    // Darker Light Green
                };
            }
        }

        /// <summary>
        /// Lấy màu theo index từ bộ màu mở rộng
        /// </summary>
        public static SolidColorBrush GetCategoryColorByIndex(int index)
        {
            var colors = GetExtendedCategoryColors();
            return colors[index % colors.Length];
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Tạo màu với độ trong suốt
        /// </summary>
        public static SolidColorBrush CreateColorWithOpacity(Color baseColor, double opacity)
        {
            return new SolidColorBrush(Color.FromArgb(
                (byte)(255 * Math.Max(0, Math.Min(1, opacity))),
                baseColor.R,
                baseColor.G,
                baseColor.B
            ));
        }

        /// <summary>
        /// Làm sáng màu (cho hover effect)
        /// </summary>
        public static SolidColorBrush LightenColor(SolidColorBrush originalBrush, double factor = 0.2)
        {
            var color = originalBrush.Color;
            var r = Math.Min(255, color.R + (int)(255 * factor));
            var g = Math.Min(255, color.G + (int)(255 * factor));
            var b = Math.Min(255, color.B + (int)(255 * factor));

            return new SolidColorBrush(Color.FromArgb(color.A, (byte)r, (byte)g, (byte)b));
        }

        /// <summary>
        /// Làm tối màu (cho pressed effect)
        /// </summary>
        public static SolidColorBrush DarkenColor(SolidColorBrush originalBrush, double factor = 0.2)
        {
            var color = originalBrush.Color;
            var r = Math.Max(0, color.R - (int)(255 * factor));
            var g = Math.Max(0, color.G - (int)(255 * factor));
            var b = Math.Max(0, color.B - (int)(255 * factor));

            return new SolidColorBrush(Color.FromArgb(color.A, (byte)r, (byte)g, (byte)b));
        }

        #endregion
    }
}
