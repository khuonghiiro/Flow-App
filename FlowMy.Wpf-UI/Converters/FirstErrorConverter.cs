using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace FlowMy.Converters
{
    public class FirstErrorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is ObservableCollection<string> errors)
                {
                    string paramStr = parameter?.ToString();

                    // Nếu parameter là "Background", trả về Brush cho background
                    if (paramStr == "Background")
                    {
                        var firstError = errors.FirstOrDefault();
                        return !string.IsNullOrWhiteSpace(firstError) ? Brushes.LightCoral : Brushes.Transparent;
                    }

                    // Mặc định trả về text của lỗi đầu tiên
                    return errors.FirstOrDefault() ?? string.Empty;
                }

                return targetType == typeof(Brush) ? Brushes.Transparent : string.Empty;
            }
            catch (Exception)
            {
                return targetType == typeof(Brush) ? Brushes.Transparent : string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}