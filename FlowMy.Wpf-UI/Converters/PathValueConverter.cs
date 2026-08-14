using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FlowMy.Converters
{
    // <summary>
    /// Converter để lấy giá trị từ object thông qua Path
    /// </summary>
    public class PathValueConverter : IMultiValueConverter
    {
        public static readonly PathValueConverter Instance = new PathValueConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || values[0] == null || values[1] == null)
                return string.Empty;

            var obj = values[0];
            var propertyPath = values[1] as string;

            if (string.IsNullOrEmpty(propertyPath))
                return string.Empty;

            try
            {
                // Sử dụng Reflection để lấy giá trị property
                var propertyInfo = obj.GetType().GetProperty(propertyPath);
                if (propertyInfo != null)
                {
                    var value = propertyInfo.GetValue(obj);
                    return value?.ToString() ?? string.Empty;
                }

                // Nếu không tìm thấy property, thử field
                var fieldInfo = obj.GetType().GetField(propertyPath);
                if (fieldInfo != null)
                {
                    var value = fieldInfo.GetValue(obj);
                    return value?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                // Ignore errors, return empty string
            }

            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
