using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowMy.Converters
{
    public static class DatePickerBehavior
    {
        public static readonly DependencyProperty SetTodayOnEnterProperty =
            DependencyProperty.RegisterAttached(
                "SetTodayOnEnter",
                typeof(bool),
                typeof(DatePickerBehavior),
                new PropertyMetadata(false, OnSetTodayOnEnterChanged));

        public static bool GetSetTodayOnEnter(DependencyObject obj)
        {
            return (bool)obj.GetValue(SetTodayOnEnterProperty);
        }

        public static void SetSetTodayOnEnter(DependencyObject obj, bool value)
        {
            obj.SetValue(SetTodayOnEnterProperty, value);
        }

        private static void OnSetTodayOnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var datePicker = d as DatePicker;
            if (datePicker == null) return;

            if ((bool)e.NewValue)
            {
                datePicker.PreviewKeyDown += DatePicker_PreviewKeyDown;
            }
            else
            {
                datePicker.PreviewKeyDown -= DatePicker_PreviewKeyDown;
            }
        }

        private static void DatePicker_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var datePicker = sender as DatePicker;
                if (datePicker != null)
                {
                    // If DatePicker is empty, set to today's date
                    if (!datePicker.SelectedDate.HasValue)
                    {
                        datePicker.SelectedDate = DateTime.Today;
                        e.Handled = true;
                    }
                    else
                    {
                        // Move focus to next control
                        datePicker.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
