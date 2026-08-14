using System.Windows;
using System.Windows.Media;

namespace FlowMy.Helpers
{
    public static class DynamicColorHelper
    {
        #region Theme Detection

        /// <summary>
        /// Lấy tên theme hiện tại
        /// </summary>
        /// <returns></returns>
        public static string CurrentTheme()
        {
            var themeName = FlowMy.Properties.Settings.Default.AppTheme ?? "LightTheme";
            return themeName;
        }
        /// <summary>
        /// Kiểm tra theme hiện tại (true = Dark, false = Light)
        /// </summary>
        public static bool IsDarkTheme()
        {
            try
            {
                if (CurrentTheme() != null && CurrentTheme() == "DarkTheme")
                {
                    return true;
                }

                return false;
            }
            catch
            {
                // Default to light theme if detection fails
                return false;
            }
        }

        public static bool IsDarkThemeV2()
        {
            try
            {
                var brush = Application.Current?.Resources["WindowBackgroundBrush"] as SolidColorBrush;
                if (brush != null)
                {
                    // Tính độ sáng của màu (luminance)
                    var color = brush.Color;
                    var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
                    return luminance < 0.5; // Nếu độ sáng < 50% thì là dark theme
                }

                // Fallback: kiểm tra theo màu system
                var systemColors = SystemColors.WindowBrush;
                var sysColor = ((SolidColorBrush)systemColors).Color;
                var sysLuminance = (0.299 * sysColor.R + 0.587 * sysColor.G + 0.114 * sysColor.B) / 255;
                return sysLuminance < 0.5;
            }
            catch
            {
                // Default to light theme if detection fails
                return false;
            }
        }

        /// <summary>
        /// Lấy màu từ resource hiện tại
        /// </summary>
        public static SolidColorBrush GetResourceBrush(string resourceKey, Color fallbackColor)
        {
            try
            {
                return Application.Current?.Resources[resourceKey] as SolidColorBrush
                    ?? new SolidColorBrush(fallbackColor);
            }
            catch
            {
                return new SolidColorBrush(fallbackColor);
            }
        }

        public static SolidColorBrush GetResourceBrush(string resourceKey, string resourceKey2)
        {
            try
            {
                return Application.Current?.Resources[resourceKey] as SolidColorBrush
                    ?? Application.Current?.Resources[resourceKey2] as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(211, 211, 211));
            }
            catch
            {
                return Application.Current?.Resources[resourceKey2] as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(211, 211, 211));
            }
        }

        private static Color? GetColorFromTheme(string resourceKey)
        {
            try
            {
                var brush = Application.Current.TryFindResource(resourceKey) as SolidColorBrush;
                return brush?.Color;
            }
            catch
            {
                return null;
            }
        }

        private static Brush? GetBrushFromTheme(string resourceKey)
        {
            try
            {
                return Application.Current.TryFindResource(resourceKey) as Brush;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Theme Update Helper

        /// <summary>
        /// Cập nhật tất cả màu sắc khi theme thay đổi
        /// </summary>
        public static void UpdateThemeColors()
        {
            // Trigger property change notifications for all color-dependent properties
            // This can be called when theme changes to refresh all UI elements

            // Fire events to notify ViewModels about theme change
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Event được fired khi theme thay đổi
        /// </summary>
        public static event EventHandler ThemeChanged;

        #endregion

        #region Theme Manager Integration

        /// <summary>
        /// Đăng ký listener cho theme change events
        /// </summary>
        public static void RegisterThemeChangeListener(EventHandler handler)
        {
            ThemeChanged += handler;
        }

        /// <summary>
        /// Hủy đăng ký listener cho theme change events
        /// </summary>
        public static void UnregisterThemeChangeListener(EventHandler handler)
        {
            ThemeChanged -= handler;
        }

        /// <summary>
        /// Trigger theme changed event manually
        /// </summary>
        public static void NotifyThemeChanged()
        {
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        #endregion

    }
}