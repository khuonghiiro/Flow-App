using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Converters
{
    /// <summary>
    /// Chuyển chuỗi URL (local hoặc internet) thành ImageSource. Ưu tiên URL đầu; nếu không tải được thử URL thứ hai (fallback).
    /// Parameter có thể là fallback URL (string) hoặc "|" nối nhiều URL.
    /// </summary>
    public class UrlToImageSourceConverter : IValueConverter
    {
        private const int DecodePixelWidthThumb = 280;

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var url = value as string;
            if (string.IsNullOrWhiteSpace(url)) url = parameter as string;
            if (string.IsNullOrWhiteSpace(url)) return null;
            var urls = new[] { url.Trim() };
            if (parameter is string paramStr && paramStr.Contains("|"))
                urls = paramStr.Split('|');
            else if (parameter is string fallback && !string.IsNullOrWhiteSpace(fallback))
                urls = new[] { url.Trim(), fallback.Trim() };
            foreach (var u in urls)
            {
                if (string.IsNullOrWhiteSpace(u)) continue;
                var src = TryLoadImage(u);
                if (src != null) return src;
            }
            return null;
        }

        private static BitmapImage? TryLoadImage(string url)
        {
            try
            {
                var kind = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? UriKind.Absolute : UriKind.RelativeOrAbsolute;
                var uri = new Uri(url, kind);
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = uri;
                img.DecodePixelWidth = DecodePixelWidthThumb;
                img.CacheOption = BitmapCacheOption.OnDemand;
                img.CreateOptions = BitmapCreateOptions.None;
                img.EndInit();
                return img;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
