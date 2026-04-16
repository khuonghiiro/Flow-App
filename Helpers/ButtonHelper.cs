using System.Windows;

namespace FlowMy.Helpers
{
    // Attached Property để định nghĩa ColorTheme
    public static class ButtonHelper
    {
        public static readonly DependencyProperty ColorThemeProperty =
            DependencyProperty.RegisterAttached(
                "ColorTheme",
                typeof(string),
                typeof(ButtonHelper),
                new PropertyMetadata("Primary"));

        public static string GetColorTheme(DependencyObject obj)
        {
            return (string)obj.GetValue(ColorThemeProperty);
        }

        public static void SetColorTheme(DependencyObject obj, string value)
        {
            obj.SetValue(ColorThemeProperty, value);
        }
    }

}
