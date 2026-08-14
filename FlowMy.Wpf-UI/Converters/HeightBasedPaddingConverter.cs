using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlowMy.Converters;

/// <summary>
/// Returns original padding when Height is Auto (NaN); otherwise returns zero padding.
/// </summary>
public sealed class HeightBasedPaddingConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var height = ReadHeight(values, 0);
        var padding = ReadThickness(values, 1);

        // Height was explicitly set: ignore padding to avoid layout conflicts.
        if (!double.IsNaN(height))
        {
            return new Thickness(0);
        }

        return padding;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static double ReadHeight(object[] values, int index)
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

    private static Thickness ReadThickness(object[] values, int index)
    {
        if (values == null || index >= values.Length || values[index] == null)
        {
            return new Thickness(0);
        }

        return values[index] is Thickness thickness ? thickness : new Thickness(0);
    }
}
