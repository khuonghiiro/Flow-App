using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlowMy.Converters
{
    public class PageButtonStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && Application.Current.TryFindResource(
                isActive ? "ActivePageButtonStyle" : "PaginationButtonStyle") is Style style)
            {
                return style;
            }
            return Application.Current.FindResource("PaginationButtonStyle");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
