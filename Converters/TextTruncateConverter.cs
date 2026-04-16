using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FlowMy.Converters
{
    /// <summary>
    /// Converter để cắt ngắn text nếu quá dài
    /// </summary>
    public class TextTruncateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && parameter is string maxLengthStr)
            {
                if (int.TryParse(maxLengthStr, out int maxLength) && text.Length > maxLength)
                {
                    return text.Substring(0, maxLength) + "...";
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
