using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowMy.Converters
{
    /// <summary>
    /// Chiều cao vùng ảnh = FrameDisplayHeight - (top row + bottom row nút). Top ~24, bottom ~34; trừ 58 để nút không bị che.
    /// </summary>
    public class FrameHeightToImageAreaConverter : IValueConverter
    {
        private const double TopBottomReserved = 58;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double v = 90;
            if (value is double d) v = d;
            else if (value != null && double.TryParse(value.ToString(), System.Globalization.NumberStyles.Any, culture, out var parsed)) v = parsed;
            return Math.Max(40, v - TopBottomReserved);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
