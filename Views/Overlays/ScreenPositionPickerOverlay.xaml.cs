using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Interaction logic for ScreenPositionPickerOverlay.xaml
    /// </summary>
    public partial class ScreenPositionPickerOverlay : Window
    {
        // P/Invoke để chặn mouse click không ảnh hưởng desktop
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private readonly LowLevelMouseProc _mouseProc;
        private IntPtr _mouseHook = IntPtr.Zero;

        public Point? SelectedPosition { get; private set; }
        private const int CROSSHAIR_SIZE = 40; // Kích thước dấu +

        public ScreenPositionPickerOverlay()
        {
            InitializeComponent();

            // Hook mouse để chặn click
            _mouseProc = MouseHookCallback;

            Loaded += (s, e) =>
            {
                // Set hook khi window loaded
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc,
                        GetModuleHandle(curModule?.ModuleName ?? string.Empty), 0);
                }
            };

            Closed += (s, e) =>
            {
                // Unhook khi đóng
                if (_mouseHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }
            };
        }

        /// <summary>
        /// Mouse hook callback - chặn tất cả mouse clicks
        /// </summary>
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                // Lấy tọa độ chuột hiện tại
                var mousePos = MousePosition;

                // Set kết quả và đóng window
                Dispatcher.Invoke(() =>
                {
                    SelectedPosition = new Point(mousePos.X, mousePos.Y);
                    DialogResult = true;
                    Close();
                });

                // Chặn event - không cho desktop xử lý
                return (IntPtr)1;
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        /// <summary>
        /// Lấy vị trí chuột toàn màn hình
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private Point MousePosition
        {
            get
            {
                GetCursorPos(out POINT point);
                return new Point(point.X, point.Y);
            }
        }

        /// <summary>
        /// Cập nhật crosshair khi chuột di chuyển
        /// </summary>
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);

            // Đường ngang
            Canvas.SetLeft(HorizontalLine, pos.X - CROSSHAIR_SIZE);
            Canvas.SetTop(HorizontalLine, pos.Y);
            HorizontalLine.X2 = CROSSHAIR_SIZE * 2;

            // Đường dọc
            Canvas.SetLeft(VerticalLine, pos.X);
            Canvas.SetTop(VerticalLine, pos.Y - CROSSHAIR_SIZE);
            VerticalLine.Y2 = CROSSHAIR_SIZE * 2;

            // Tâm
            Canvas.SetLeft(CenterDot, pos.X - 4);
            Canvas.SetTop(CenterDot, pos.Y - 4);

            // Cập nhật text tọa độ (screen coordinates)
            var screenPos = MousePosition;
            CoordinateText.Text = $"X: {(int)screenPos.X}, Y: {(int)screenPos.Y}";
        }

        /// <summary>
        /// Xử lý click chuột trái (fallback - chủ yếu dùng hook)
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var screenPos = MousePosition;
            SelectedPosition = new Point(screenPos.X, screenPos.Y);
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Nhấn ESC để hủy
        /// </summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SelectedPosition = null;
                DialogResult = false;
                Close();
            }
        }
    }
}

