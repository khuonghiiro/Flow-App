using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlowMy.Converters
{
    public class PlaceholderVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 4)
                return Visibility.Collapsed;

            var selectedItemsCount = values[0] as int? ?? 0;
            var selectedText = values[1] as string ?? string.Empty;
            var searchText = values[2] as string ?? string.Empty;
            var isDropDownOpen = values[3] as bool? ?? false;

            // Show placeholder when:
            // - No items selected
            // - No selected text
            // - No search text (or dropdown is not open)
            bool shouldShowPlaceholder = selectedItemsCount == 0 &&
                                       string.IsNullOrEmpty(selectedText) &&
                                       (string.IsNullOrEmpty(searchText) || !isDropDownOpen);

            return shouldShowPlaceholder ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
