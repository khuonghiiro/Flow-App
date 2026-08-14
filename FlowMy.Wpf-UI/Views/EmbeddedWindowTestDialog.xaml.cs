using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using FlowMy.Helpers;

namespace FlowMy.Views
{
    public partial class EmbeddedWindowTestDialog : Window, INotifyPropertyChanged
    {
        private IntPtr _selectedWindowHandle = IntPtr.Zero;
        private bool _hasSelectedWindow = false;
        private bool _isEmbedded = false;

        #region P/Invoke for Window Picking

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT pt);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion

        public EmbeddedWindowTestDialog()
        {
            InitializeComponent();
            DataContext = this;
            LogEvent("✅ Embedded Window Test Dialog initialized");
            LogEvent("📋 Click 'Pick Window' to select an app to embed");
        }

        #region Properties

        public bool HasSelectedWindow
        {
            get => _hasSelectedWindow;
            set
            {
                _hasSelectedWindow = value;
                OnPropertyChanged();
            }
        }

        public bool IsEmbedded
        {
            get => _isEmbedded;
            set
            {
                _isEmbedded = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Event Handlers

        private void PickWindow_Click(object sender, RoutedEventArgs e)
        {
            LogEvent("🎯 Click on the window you want to embed...");
            StatusText.Text = "Waiting for window selection...";
            StatusText.Foreground = System.Windows.Media.Brushes.Orange;

            // Hide this window temporarily
            this.WindowState = WindowState.Minimized;

            // Wait a bit for user to see the target window
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Get window under cursor
                    POINT pt;
                    GetCursorPos(out pt);
                    var hwnd = WindowFromPoint(pt);

                    if (hwnd == IntPtr.Zero)
                    {
                        LogEvent("❌ No window found at cursor position");
                        StatusText.Text = "Failed to pick window";
                        StatusText.Foreground = System.Windows.Media.Brushes.Red;
                    }
                    else
                    {
                        _selectedWindowHandle = hwnd;
                        string info = WindowHostHelper.GetWindowInfo(hwnd);
                        LogEvent($"✅ Selected: {info}");
                        SelectedWindowText.Text = info;
                        StatusText.Text = "Window selected - Click 'Embed'";
                        StatusText.Foreground = System.Windows.Media.Brushes.Green;
                        HasSelectedWindow = true;
                    }

                    // Restore this window
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                });
            });
        }

        private void EmbedWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWindowHandle == IntPtr.Zero)
            {
                LogEvent("⚠️ No window selected");
                return;
            }

            LogEvent($"🚀 Embedding window hwnd={_selectedWindowHandle}...");

            if (!WindowHostHelper.CanEmbedWindow(_selectedWindowHandle))
            {
                LogEvent("❌ Cannot embed this window (system window or restricted)");
                MessageBox.Show(
                    "Cannot embed this window.\n\n" +
                    "System windows (Explorer, Task Manager) cannot be embedded.\n" +
                    "Try with Paint, Notepad, Calculator, or your own apps.",
                    "Cannot Embed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            bool success = WindowHost.EmbedWindow(_selectedWindowHandle);

            if (success)
            {
                LogEvent("✅ Window embedded successfully!");
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                WindowHost.Visibility = Visibility.Visible;
                StatusText.Text = "Window embedded - Try interacting!";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                IsEmbedded = true;
            }
            else
            {
                LogEvent("❌ Failed to embed window");
                StatusText.Text = "Embed failed";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void UnembedWindow_Click(object sender, RoutedEventArgs e)
        {
            LogEvent("↩️ Unembedding window...");
            WindowHost.UnembedWindow();
            
            PlaceholderPanel.Visibility = Visibility.Visible;
            WindowHost.Visibility = Visibility.Collapsed;
            StatusText.Text = "Window unembedded";
            StatusText.Foreground = System.Windows.Media.Brushes.Blue;
            IsEmbedded = false;
            
            LogEvent("✅ Window restored to normal state");
        }

        private void WindowHost_Embedded(object? sender, IntPtr hwnd)
        {
            LogEvent($"📌 WindowEmbedded event: hwnd={hwnd}");
        }

        private void WindowHost_Unembedded(object? sender, EventArgs e)
        {
            LogEvent("📌 WindowUnembedded event");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Unembed before closing
            if (IsEmbedded)
            {
                UnembedWindow_Click(sender, e);
            }
            Close();
        }

        #endregion

        #region Helper Methods

        private void LogEvent(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            EventLog.Text += $"[{timestamp}] {message}\n";
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Make sure to unembed before closing
            if (IsEmbedded)
            {
                WindowHost.UnembedWindow();
            }
            base.OnClosing(e);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
