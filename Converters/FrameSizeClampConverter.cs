using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowMy.Converters
{
    /// <summary>
    /// Clamp kích thước khung gallery: value là FrameDisplayWidth/Height, parameter là giá trị tối thiểu (double hoặc string).
    /// </summary>
    public class FrameSizeClampConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double v = 60;
            if (value is double d) v = d;
            else if (value != null && double.TryParse(value.ToString(), System.Globalization.NumberStyles.Any, culture, out var parsed)) v = parsed;
            double min = 60;
            if (parameter is double dp) min = dp;
            else if (parameter is string s && double.TryParse(s, System.Globalization.NumberStyles.Any, culture, out var p)) min = p;
            return Math.Max(min, v);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
