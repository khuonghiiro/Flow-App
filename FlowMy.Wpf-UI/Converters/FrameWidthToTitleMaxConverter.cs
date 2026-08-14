using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowMy.Converters
{
    /// <summary>
    /// MaxWidth cho title trong ô gallery = FrameDisplayWidth - 8, tối thiểu 20.
    /// </summary>
    public class FrameWidthToTitleMaxConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double v = 120;
            if (value is double d) v = d;
            else if (value != null && double.TryParse(value.ToString(), System.Globalization.NumberStyles.Any, culture, out var parsed)) v = parsed;
            return Math.Max(20, v - 8);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
