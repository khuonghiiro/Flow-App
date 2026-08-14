using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FlowMy.Converters
{
    public class PercentageToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] != null && values[1] != null)
            {
                try
                {
                    double percentage = System.Convert.ToDouble(values[0]);
                    double containerWidth = System.Convert.ToDouble(values[1]);

                    if (containerWidth > 0 && percentage >= 0)
                    {
                        double resultWidth = (percentage / 100.0) * containerWidth;
                        return Math.Max(0, Math.Min(containerWidth, resultWidth));
                    }
                }
                catch
                {
                    return 0.0;
                }
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
