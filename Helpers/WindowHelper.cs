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
        public const int SW_RESTORE = 9;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SW_SHOWNA = 8;
        public const int SW_SHOW = 5;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr ChildWindowFromPointEx(IntPtr hwndParent, POINT pt, uint uFlags);

        [DllImport("user32.dll")]
        public static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref POINT lpPoints, uint cPoints);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width  => Right  - Left;
            public int Height => Bottom - Top;
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

        public static (IntPtr Hwnd, int ClientX, int ClientY) GetDeepestChildFromPoint(IntPtr hWndParent, int x, int y)
        {
            IntPtr current = hWndParent;
            POINT pt = new POINT { X = x, Y = y };
            
            // CWP_SKIPINVISIBLE = 0x0001
            // CWP_SKIPDISABLED = 0x0002
            // CWP_SKIPTRANSPARENT = 0x0004
            uint flags = 0x0001 | 0x0002 | 0x0004;

            while (true)
            {
                IntPtr child = ChildWindowFromPointEx(current, pt, flags);
                if (child == IntPtr.Zero || child == current)
                    break;

                MapWindowPoints(current, child, ref pt, 1);
                current = child;
            }

            return (current, pt.X, pt.Y);
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

        public static void BringToFront(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            if (IsIconic(hwnd))
                ShowWindow(hwnd, SW_RESTORE);
            else
                ShowWindow(hwnd, SW_SHOW);
            SetForegroundWindow(hwnd);
        }

        public static void BringToFront(Window window)
        {
            if (window == null) return;

            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;

            if (IsIconic(hwnd))
                ShowWindow(hwnd, SW_RESTORE);
            else
                ShowWindow(hwnd, SW_SHOW);
            SetForegroundWindow(hwnd);
        }
    }
}
