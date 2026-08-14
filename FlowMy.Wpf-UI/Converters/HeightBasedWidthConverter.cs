using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowMy.Converters;

/// <summary>
/// If Width is set, keep it. If Width is Auto and Height is set, use Height as Width.
/// Otherwise return Auto width.
/// </summary>
public sealed class HeightBasedWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var width = ReadSize(values, 0);
        var height = ReadSize(values, 1);

        if (!double.IsNaN(width))
        {
            return width;
        }

        if (!double.IsNaN(height))
        {
            return height;
        }

        return double.NaN;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static double ReadSize(object[] values, int index)
    {
        if (values == null || index >= values.Length || values[index] == null)
        {
            return double.NaN;
        }

        return values[index] switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => double.NaN
        };
    }
}
