using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FlowMy.Behaviors
{
    /// <summary>
    /// Tải ảnh từ URL (internet hoặc local) bất đồng bộ với caching.
    /// Sử dụng cache từ MediaGalleryNodeControl để tránh tải lại nhiều lần.
    /// </summary>
    public static class AsyncUrlImageBehavior
    {

        public static readonly DependencyProperty UrlProperty = DependencyProperty.RegisterAttached(
            "Url", typeof(string), typeof(AsyncUrlImageBehavior),
            new PropertyMetadata(null, OnUrlChanged));

        public static string? GetUrl(DependencyObject obj) => (string?)obj.GetValue(UrlProperty);
        public static void SetUrl(DependencyObject obj, string? value) => obj.SetValue(UrlProperty, value);

        private static void OnUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not System.Windows.Controls.Image img) return;
            var url = (string?)e.NewValue;
            if (string.IsNullOrWhiteSpace(url))
            {
                img.Source = null;
                return;
            }
            LoadImageAsync(img, url.Trim());
        }

        private static void LoadImageAsync(System.Windows.Controls.Image img, string url)
        {
            // Sử dụng cache từ MediaGalleryNodeControl để tránh tải lại nhiều lần
            try
            {
                var bitmap = FlowMy.Views.NodeControls.MediaGalleryNodeControl.GetOrCreateCachedBitmap(url);
                
                if (bitmap.IsDownloading)
                {
                    // Đợi ảnh load xong
                    EventHandler? downloadCompleted = null;
                    downloadCompleted = (s, e) =>
                    {
                        bitmap.DownloadCompleted -= downloadCompleted;
                        img.Dispatcher.BeginInvoke(() =>
                        {
                            img.Source = bitmap;
                        }, DispatcherPriority.Background);
                    };
                    bitmap.DownloadCompleted += downloadCompleted;
                    
                    // Xử lý lỗi
                    EventHandler<System.Windows.Media.ExceptionEventArgs>? downloadFailed = null;
                    downloadFailed = (s, e) =>
                    {
                        bitmap.DownloadCompleted -= downloadCompleted;
                        bitmap.DownloadFailed -= downloadFailed;
                        // Không set source nếu fail
                    };
                    bitmap.DownloadFailed += downloadFailed;
                }
                else
                {
                    // Ảnh đã load xong (cached hoặc local)
                    img.Source = bitmap;
                }
            }
            catch { /* ignore */ }
        }
    }
}
