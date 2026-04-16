using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FlowMy.Views.Overlays
{
    public partial class ToastWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private static readonly List<ToastWindow> _openToasts = new();

        public ToastWindow(
            string title,
            string message,
            int durationSeconds,
            System.Windows.Media.Brush titleBrush,
            System.Windows.Media.Brush contentBrush,
            System.Windows.Media.Brush backgroundBrush)
        {
            InitializeComponent();

            TitleText.Text = string.IsNullOrWhiteSpace(title) ? "Notification" : title;
            MessageText.Text = message ?? string.Empty;

            TitleText.Foreground = titleBrush;
            MessageText.Foreground = contentBrush;

            if (Content is Border border)
            {
                border.Background = backgroundBrush;
            }

            Loaded += ToastWindow_Loaded;
            Closed += ToastWindow_Closed;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSeconds < 1 ? 1 : durationSeconds)
            };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                Close();
            };
        }

        private void ToastWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_openToasts.Contains(this))
            {
                _openToasts.Add(this);
            }

            RepositionToasts();
            _timer.Start();
        }

        private void ToastWindow_Closed(object? sender, EventArgs e)
        {
            if (_openToasts.Contains(this))
            {
                _openToasts.Remove(this);
                RepositionToasts();
            }
        }

        private static void RepositionToasts()
        {
            try
            {
                var workArea = SystemParameters.WorkArea;
                const double margin = 16;
                const double spacing = 8;

                double currentTop = workArea.Top + margin;

                foreach (var toast in _openToasts)
                {
                    if (!toast.IsLoaded) continue;

                    // Ensure size is measured
                    if (double.IsNaN(toast.Width) || double.IsNaN(toast.Height) || toast.Height == 0)
                    {
                        toast.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        toast.Arrange(new Rect(toast.DesiredSize));
                    }

                    toast.Left = workArea.Right - toast.Width - margin;
                    toast.Top = currentTop;

                    currentTop += toast.Height + spacing;
                }
            }
            catch
            {
            }
        }
    }
}

