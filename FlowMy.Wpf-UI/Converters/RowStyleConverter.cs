using FlowMy.Helpers;
using FlowMy.Interfaces;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FlowMy.Converters
{
    public class RowStyleBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IRowStyleData style && !string.IsNullOrEmpty(style.Background))
            {
                // Thử resolve từ Application Resources trước với null check
                try
                {
                    var resource = Application.Current?.TryFindResource(style.Background);
                    if (resource is SolidColorBrush brush)
                        return brush;

                    if (resource is Color color)
                        return new SolidColorBrush(color);
                }
                catch (Exception ex)
                {
                    // Log exception if needed
                    System.Diagnostics.Debug.WriteLine($"Resource lookup failed for {style.Background}: {ex.Message}");
                }

                // Fallback: nếu không phải resource key, thử parse trực tiếp
                try
                {
                    return DynamicColorHelper.GetResourceBrush(style.Background, "TransparentBrush");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DynamicColorHelper failed for {style.Background}: {ex.Message}");
                    // Final fallback
                    return new SolidColorBrush(Colors.Transparent);
                }
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RowStyleForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IRowStyleData style && !string.IsNullOrEmpty(style.Foreground))
            {
                try
                {
                    var resource = Application.Current?.TryFindResource(style.Foreground);
                    if (resource is SolidColorBrush brush)
                        return brush;

                    if (resource is Color color)
                        return new SolidColorBrush(color);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Resource lookup failed for {style.Foreground}: {ex.Message}");
                }

                try
                {
                    return DynamicColorHelper.GetResourceBrush(style.Foreground, "TextBrush");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DynamicColorHelper failed for {style.Foreground}: {ex.Message}");
                    return new SolidColorBrush(Colors.Black);
                }
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RowStyleHoverBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IRowStyleData style && !string.IsNullOrEmpty(style.HoverBackground))
            {
                var resource = Application.Current?.FindResource(style.HoverBackground);
                if (resource is SolidColorBrush brush)
                    return brush;

                if (resource is Color color)
                    return new SolidColorBrush(color);

                try
                {
                    return DynamicColorHelper.GetResourceBrush(style.HoverBackground, "DataGridRowHoverBackground");
                }
                catch
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
                }
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter cho Selected Background
    public class RowStyleSelectedBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IRowStyleData style && !string.IsNullOrEmpty(style.SelectedBackground))
            {
                var resource = Application.Current?.FindResource(style.SelectedBackground);
                if (resource is SolidColorBrush brush) return brush;
                if (resource is Color color) return new SolidColorBrush(color);

                try
                {

                    return DynamicColorHelper.GetResourceBrush(style.SelectedBackground, "DataGridSelectionBackground");
                }
                catch
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
                }
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter cho FontWeight
    public class RowStyleFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IRowStyleData style && !string.IsNullOrEmpty(style.FontWeight))
            {
                return style.FontWeight switch
                {
                    "Bold" => FontWeights.Bold,
                    "SemiBold" => FontWeights.SemiBold,
                    "Light" => FontWeights.Light,
                    _ => FontWeights.Normal
                };
            }
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter cho StyleType enum
    public class StyleTypeToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StyleTypeEnum styleType)
            {
                return styleType switch
                {
                    StyleTypeEnum.Success => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E8")),
                    StyleTypeEnum.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3CD")),
                    StyleTypeEnum.Danger => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8D7DA")),
                    StyleTypeEnum.Info => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1ECF1")),
                    _ => new SolidColorBrush(Colors.Transparent)
                };
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RowStyleHoverForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IRowStyleData style && !string.IsNullOrEmpty(style.HoverForeground))
            {
                var resource = Application.Current?.FindResource(style.HoverForeground);
                if (resource is SolidColorBrush brush) return brush;
                if (resource is Color color) return new SolidColorBrush(color);

                try
                {
                    return DynamicColorHelper.GetResourceBrush(style.HoverForeground, "TextBrush");
                }
                catch
                {
                    return new SolidColorBrush(Colors.Black);
                }
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RowStyleSelectedForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IRowStyleData style && !string.IsNullOrEmpty(style.SelectedForeground))
            {
                var resource = Application.Current?.FindResource(style.SelectedForeground);
                if (resource is SolidColorBrush brush) return brush;
                if (resource is Color color) return new SolidColorBrush(color);

                try
                {
                    return DynamicColorHelper.GetResourceBrush(style.SelectedForeground, "DataGridSelectedTextBrush");
                }
                catch
                {
                    return new SolidColorBrush(Colors.White);
                }
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
