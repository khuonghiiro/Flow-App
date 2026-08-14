using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FlowMy.Converters
{
    // Converter để chuyển đổi tên màu thành Resource
    public class ColorThemeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string colorTheme = value?.ToString() ?? "Primary";
            string state = parameter?.ToString() ?? "";

            // Tạo resource key: {Theme}{State}Brush
            // Ví dụ: PrimaryBrush, PrimaryHoverBrush, TextOnPrimaryBrush
            string resourceKey = state == "TextOn"
                ? $"TextOn{colorTheme}Brush"
                : $"{colorTheme}{state}Brush";

            // Tìm resource
            var resource = Application.Current.TryFindResource(resourceKey);
            if (resource != null)
                return resource;

            // Fallback về Primary nếu không tìm thấy
            string fallbackKey = state == "TextOn"
                ? "TextOnPrimaryBrush"
                : $"Primary{state}Brush";
            return Application.Current.TryFindResource(fallbackKey) ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
