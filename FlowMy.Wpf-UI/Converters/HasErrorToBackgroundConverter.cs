using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FlowMy.Converters
{
    public class HasErrorToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // value là object từ Dictionary[key] hoặc từ ObservableCollection[index]
                if (value == null)
                    return Brushes.Transparent;

                // Nếu là string lỗi → tô nền
                if (value is string error)
                {
                    return !string.IsNullOrWhiteSpace(error) ? Brushes.LightCoral : Brushes.Transparent;
                }

                return Brushes.Transparent;
            }
            catch (Exception)
            {
                // Nếu có lỗi gì thì trả về trong suốt
                return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}