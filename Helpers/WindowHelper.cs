using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows;

namespace FlowMy.Helpers
{
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName => $"[{ProcessName}] {Title}";

        public override bool Equals(object? obj) => obj is WindowInfo info && Handle == info.Handle;
        public override int GetHashCode() => Handle.GetHashCode();
    }

    public static class WindowHelper
    {
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // Win32 Messages
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;
        public const uint WM_CHAR = 0x0102;
        
        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP = 0x0202;
        public const uint WM_RBUTTONDOWN = 0x0204;
        public const uint WM_RBUTTONUP = 0x0205;
        public const uint WM_MOUSEMOVE = 0x0200;
        public const uint WM_MOUSEWHEEL = 0x020A;

        public const int MK_LBUTTON = 0x0001;
        public const int MK_RBUTTON = 0x0002;

        public static IntPtr MakeLParam(int loWord, int hiWord)
        {
            return (IntPtr)((hiWord << 16) | (loWord & 0xFFFF));
        }

        public static List<WindowInfo> GetActiveWindows()
        {
            var windows = new List<WindowInfo>();

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    int length = GetWindowTextLength(hWnd);
                    if (length > 0)
                    {
                        var builder = new StringBuilder(length + 1);
                        GetWindowText(hWnd, builder, builder.Capacity);
                        string title = builder.ToString();

                        // Lọc một số ứng dụng system không mong muốn
                        if (title == "Program Manager" || title == "Settings" || title == "Microsoft Text Input Application")
                            return true;

                        GetWindowThreadProcessId(hWnd, out uint processId);
                        string processName = "Unknown";
                        try
                        {
                            var process = System.Diagnostics.Process.GetProcessById((int)processId);
                            processName = process.ProcessName;
                        }
                        catch { }

                        windows.Add(new WindowInfo
                        {
                            Handle = hWnd,
                            Title = title,
                            ProcessName = processName
                        });
                    }
                }
                return true; // continue enumeration
            }, IntPtr.Zero);

            return windows.OrderBy(w => w.ProcessName).ThenBy(w => w.Title).ToList();
        }

        public static void BringToFront(Window window)
        {
            if (window == null) return;

            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;

            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
    }
}
