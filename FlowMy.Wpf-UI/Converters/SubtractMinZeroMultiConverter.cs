using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowMy.Converters;

/// <summary>Returns max(0, values[0] - values[1]) for double-like inputs (e.g. line height minus trim).</summary>
public sealed class SubtractMinZeroMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var h = ReadDouble(values, 0, culture);
        var t = ReadDouble(values, 1, culture);
        return Math.Max(0d, h - t);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static double ReadDouble(object[] values, int index, CultureInfo culture)
    {
        if (values == null || index >= values.Length || values[index] == null) return 0d;
        return values[index] switch
        {
            double d => d,
            float f => f,
            int i => i,
            IConvertible c => System.Convert.ToDouble(c, culture),
            _ => 0d
        };
    }
}
