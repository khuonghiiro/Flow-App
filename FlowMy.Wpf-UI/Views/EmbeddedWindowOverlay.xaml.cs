using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using FlowMy.Helpers;

namespace FlowMy.Views
{
    /// <summary>
    /// Floating overlay window để embed external apps.
    /// Overlay này:
    /// - Giữ nguyên vị trí và kích thước của app gốc
    /// - Không làm gián đoạn user
    /// - Control app để automation
    /// - Auto-unembed khi đóng
    /// </summary>
    public partial class EmbeddedWindowOverlay : Window
    {
        private IntPtr _embeddedWindowHandle = IntPtr.Zero;
        private RECT _originalWindowRect;
        private bool _isMinimized = false;

        #region P/Invoke

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        #endregion

        public IntPtr EmbeddedWindowHandle => _embeddedWindowHandle;

        public EmbeddedWindowOverlay()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Activate overlay (và embedded window sẽ nhận input).
        /// Call trước khi send input.
        /// </summary>
        public void ActivateForInput()
        {
            try
            {
                // Activate overlay window
                this.Activate();
                this.Focus();
                
                // Also try SetForegroundWindow
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                SetForegroundWindow(hwnd);
                
                // Small delay để window activation complete
                System.Threading.Thread.Sleep(50);
                
                System.Diagnostics.Debug.WriteLine("[EmbeddedWindowOverlay] ✅ Activated for input");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmbeddedWindowOverlay] Activate error: {ex.Message}");
            }
        }

        /// <summary>
        /// Embed external window và position overlay tại vị trí của window đó.
        /// </summary>
        public bool EmbedWindowAtOriginalPosition(IntPtr windowHandle, string windowTitle = "")
        {
            if (windowHandle == IntPtr.Zero)
                return false;

            try
            {
                // Get original window position and size
                if (!GetWindowRect(windowHandle, out _originalWindowRect))
                {
                    System.Diagnostics.Debug.WriteLine("[EmbeddedWindowOverlay] Failed to get window rect");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[EmbeddedWindowOverlay] Original window rect: " +
                    $"({_originalWindowRect.Left},{_originalWindowRect.Top}) " +
                    $"{_originalWindowRect.Width}x{_originalWindowRect.Height}");

                // Set overlay size to match original window (plus header)
                this.Width = _originalWindowRect.Width;
                this.Height = _originalWindowRect.Height + 28; // +28 for header bar

                // Position overlay at original window location
                this.Left = _originalWindowRect.Left;
                this.Top = _originalWindowRect.Top - 28; // Offset for header

                // Update title
                if (!string.IsNullOrEmpty(windowTitle))
                {
                    TitleText.Text = $"Controlling: {windowTitle}";
                }

                // Show overlay first
                this.Show();

                // Small delay to ensure overlay is rendered
                System.Threading.Thread.Sleep(100);

                // Embed the window
                bool success = WindowHost.EmbedWindow(windowHandle);

                if (success)
                {
                    _embeddedWindowHandle = windowHandle;
                    System.Diagnostics.Debug.WriteLine($"[EmbeddedWindowOverlay] ✅ Embedded window at original position");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[EmbeddedWindowOverlay] ❌ Failed to embed window");
                    this.Close();
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmbeddedWindowOverlay] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unembed và close overlay.
        /// </summary>
        public void UnembedAndClose()
        {
            try
            {
                if (_embeddedWindowHandle != IntPtr.Zero)
                {
                    WindowHost.UnembedWindow();
                    _embeddedWindowHandle = IntPtr.Zero;
                    System.Diagnostics.Debug.WriteLine("[EmbeddedWindowOverlay] ✅ Unembedded window");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmbeddedWindowOverlay] Unembed error: {ex.Message}");
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging overlay by header
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isMinimized)
            {
                // Restore
                this.Height = _originalWindowRect.Height + 28;
                WindowHost.Visibility = Visibility.Visible;
                MinimizeButton.Content = "−";
                _isMinimized = false;
            }
            else
            {
                // Minimize to header only
                this.Height = 28;
                WindowHost.Visibility = Visibility.Collapsed;
                MinimizeButton.Content = "□";
                _isMinimized = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            UnembedAndClose();
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            UnembedAndClose();
            base.OnClosing(e);
        }
    }
}
