using System;
using System.Windows;
using System.Windows.Threading;
using FlowMy.Views.Overlays;

namespace FlowMy.Services.Interaction
{
    /// <summary>
    /// Service đơn giản để hiển thị toast notification trong ứng dụng.
    /// </summary>
    public static class ToastNotificationService
    {
        public static void ShowToast(
            string title,
            string message,
            int durationSeconds,
            string? titleColorKey = null,
            string? contentColorKey = null,
            string? backgroundColorKey = null,
            double? backgroundOpacity = null)
        {
            if (durationSeconds < 1) durationSeconds = 1;

            Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                // Default brushes
                var defaultTitleBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                var defaultContentBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)); // #E5E7EB
                var defaultBackgroundBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)); // #0F172A

                var titleBrush = ResolveBrush(titleColorKey, defaultTitleBrush);
                var contentBrush = ResolveBrush(contentColorKey, defaultContentBrush);
                var backgroundBrush = ResolveBrush(backgroundColorKey, defaultBackgroundBrush);

                double opacity = backgroundOpacity ?? 0.85;
                if (opacity < 0.0) opacity = 0.0;
                if (opacity > 1.0) opacity = 1.0;

                backgroundBrush = backgroundBrush.Clone();
                backgroundBrush.Opacity = opacity;

                var toast = new ToastWindow(title, message, durationSeconds, titleBrush, contentBrush, backgroundBrush)
                {
                    Owner = Application.Current.MainWindow
                };
                toast.Show();
            }));
        }

        private static System.Windows.Media.Brush ResolveBrush(string? key, System.Windows.Media.Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback;

            try
            {
                if (key == "LimeGreen")
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);

                // Hex hoặc các định dạng màu hợp lệ (#RRGGBB, #AARRGGBB, v.v.)
                if (key.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                {
                    var converter = new System.Windows.Media.BrushConverter();
                    var brush = converter.ConvertFromString(key) as System.Windows.Media.Brush;
                    if (brush != null)
                        return brush;
                }

                var resource = Application.Current.TryFindResource(key);
                if (resource is System.Windows.Media.Brush b)
                    return b;

                // Nếu key trỏ tới *Brush trong theme (vd: PrimaryBrush), đã thử ở trên.
                // Nếu người dùng nhập Color resource (vd: PrimaryColor), WPF có thể trả về Color.
                if (resource is System.Windows.Media.Color c)
                    return new System.Windows.Media.SolidColorBrush(c);
            }
            catch
            {
            }

            return fallback;
        }
    }
}

