using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowMy.Converters
{
    /// <summary>
    /// Checks whether the given string value is a valid SVG icon key
    /// (exists in IconResources). Returns true if it is an SVG icon,
    /// false otherwise (emoji/text).
    /// </summary>
    public class StringStartsWithConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                return IconResources.IconExists(str);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
