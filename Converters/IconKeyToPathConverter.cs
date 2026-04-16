using System.Globalization;
using System.Windows.Data;

namespace FlowMy.Converters
{
    public class IconKeyToPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Ưu tiên parameter trước, nếu không có thì dùng value
            var iconKey = (parameter as string) ?? (value as string);

            if (!string.IsNullOrEmpty(iconKey))
            {
                // Lấy đường dẫn từ key
                string iconPath = IconResources.GetIconPath(iconKey);
                if (!string.IsNullOrEmpty(iconPath))
                {
                    return new Uri(iconPath, UriKind.RelativeOrAbsolute);
                }
            }

            // Trả về icon mặc định nếu không tìm thấy
            return new Uri("/Assets/Icons/circle2.svg", UriKind.RelativeOrAbsolute);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}