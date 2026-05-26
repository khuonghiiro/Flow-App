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

        // ─── SendInput (hardware-level injection) ────────────────────────────────────

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private const uint INPUT_MOUSE    = 0;
        private const uint INPUT_KEYBOARD = 1;

        private const uint MOUSEEVENTF_MOVE        = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN    = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP      = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN   = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP     = 0x0010;
        private const uint MOUSEEVENTF_WHEEL       = 0x0800;
        private const uint MOUSEEVENTF_ABSOLUTE    = 0x8000;

        private const uint KEYEVENTF_KEYDOWN  = 0x0000;
        private const uint KEYEVENTF_KEYUP    = 0x0002;
        private const uint KEYEVENTF_UNICODE  = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int    dx;
            public int    dy;
            public uint   mouseData;
            public uint   dwFlags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint   dwFlags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT    mi;
            [FieldOffset(0)] public KEYBDINPUT    ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint      type;
            public INPUTUNION u;
        }

        /// <summary>
        /// Converts screen pixel coordinates to the normalised 0-65535 range required by SendInput.
        /// </summary>
        private static (int nx, int ny) ToNorm(int screenX, int screenY)
        {
            int sw = GetSystemMetrics(SM_CXSCREEN);
            int sh = GetSystemMetrics(SM_CYSCREEN);
            return ((screenX * 65535 + sw / 2) / sw,
                    (screenY * 65535 + sh / 2) / sh);
        }

        /// <summary>
        /// Injects a hardware mouse click at the given screen coordinates.
        /// The events are placed directly in the system input queue and will be
        /// processed by whichever window owns that pixel — even if that window
        /// loses focus immediately afterwards.
        /// </summary>
        public static void SendHwMouseClick(int screenX, int screenY, bool rightButton)
        {
            var (nx, ny) = ToNorm(screenX, screenY);

            uint downFlag = rightButton ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
            uint upFlag   = rightButton ? MOUSEEVENTF_RIGHTUP   : MOUSEEVENTF_LEFTUP;

            INPUT[] inputs =
            [
                new INPUT { type = INPUT_MOUSE, u = new INPUTUNION { mi = new MOUSEINPUT
                    { dx = nx, dy = ny, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE } } },
                new INPUT { type = INPUT_MOUSE, u = new INPUTUNION { mi = new MOUSEINPUT
                    { dwFlags = downFlag } } },
                new INPUT { type = INPUT_MOUSE, u = new INPUTUNION { mi = new MOUSEINPUT
                    { dwFlags = upFlag } } },
            ];
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        /// <summary>Injects a hardware mouse wheel event at the given screen position.</summary>
        public static void SendHwMouseScroll(int screenX, int screenY, int delta)
        {
            var (nx, ny) = ToNorm(screenX, screenY);
            uint wheelData = (uint)(delta * 120) << 16;

            INPUT[] inputs =
            [
                new INPUT { type = INPUT_MOUSE, u = new INPUTUNION { mi = new MOUSEINPUT
                    { dx = nx, dy = ny, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE } } },
                new INPUT { type = INPUT_MOUSE, u = new INPUTUNION { mi = new MOUSEINPUT
                    { mouseData = (uint)(delta * 120), dwFlags = MOUSEEVENTF_WHEEL } } },
            ];
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        /// <summary>Injects a hardware key press (down + up) for a single character.</summary>
        public static void SendHwChar(char c)
        {
            INPUT[] inputs =
            [
                new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT
                    { wScan = c, dwFlags = KEYEVENTF_UNICODE } } },
                new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT
                    { wScan = c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } } },
            ];
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        /// <summary>Injects a hardware virtual-key press (down + up).</summary>
        public static void SendHwVKey(ushort vk)
        {
            INPUT[] inputs =
            [
                new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT
                    { wVk = vk, dwFlags = KEYEVENTF_KEYDOWN } } },
                new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT
                    { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } },
            ];
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ignore);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static readonly IntPtr HWND_TOP    = IntPtr.Zero;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const uint SWP_NOSIZE       = 0x0001;
        public const uint SWP_NOMOVE       = 0x0002;
        public const uint SWP_NOACTIVATE   = 0x0010;
        public const uint SWP_SHOWWINDOW   = 0x0040;

        /// <summary>
        /// Brings <paramref name="hwnd"/> to the top of the Z-order WITHOUT activating it.
        /// This makes the window "visible on top" so SendInput can hit-test to it,
        /// but the titlebar stays grey (no foreground flash).
        /// </summary>
        public static void RaiseNoActivate(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Temporarily makes <paramref name="hwnd"/> topmost (above ALL other windows,
        /// including other topmost windows) so that <c>SendInput</c> reliably hit-tests
        /// to it even when the window is not the foreground window.
        /// Must be paired with <see cref="RestoreNonTopmost"/> after the input is sent.
        /// Using <c>SWP_NOACTIVATE</c> prevents titlebar activation flash.
        /// </summary>
        public static void RaiseTopmost(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Removes the always-on-top flag from <paramref name="hwnd"/>,
        /// returning it to the normal (non-topmost) Z-order layer.
        /// </summary>
        public static void RestoreNonTopmost(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Silently redirects keyboard (SendInput) to <paramref name="targetHwnd"/> via
        /// AttachThreadInput + SetFocus — without touching the foreground window.
        /// Returns the previous focus handle so you can restore it.
        /// </summary>
        public static (uint tgtThread, uint myThread, IntPtr prevFocus) SilentKeyboardFocus(IntPtr targetHwnd)
        {
            uint myThread  = GetCurrentThreadId();
            uint tgtThread = GetWindowThreadProcessId(targetHwnd, IntPtr.Zero);

            if (tgtThread != myThread)
                AttachThreadInput(myThread, tgtThread, true);

            IntPtr prevFocus = GetFocus();
            SetFocus(targetHwnd);

            return (tgtThread, myThread, prevFocus);
        }

        /// <summary>Undoes a <see cref="SilentKeyboardFocus"/> call.</summary>
        public static void SilentKeyboardFocusRestore((uint tgtThread, uint myThread, IntPtr prevFocus) token)
        {
            SetFocus(token.prevFocus);
            if (token.tgtThread != token.myThread)
                AttachThreadInput(token.myThread, token.tgtThread, false);
        }

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

        public static (IntPtr Hwnd, int ClientX, int ClientY) GetDeepestChildFromPoint(IntPtr hWndParent, int screenX, int screenY)
        {
            IntPtr current = hWndParent;
            // Start with screen coordinates, convert to client of the root window first
            POINT pt = new POINT { X = screenX, Y = screenY };
            ScreenToClient(hWndParent, ref pt);

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

        /// <summary>
        /// Temporarily makes <paramref name="targetHwnd"/> the "active" window in the Win32 input
        /// subsystem — silently, without changing the Z-order or foreground window.
        /// Returns a token you must pass to <see cref="SilentDeactivate"/> when done.
        /// </summary>
        public static (uint targetThread, uint myThread, IntPtr prevActive) SilentActivate(IntPtr targetHwnd)
        {
            uint myThread  = GetCurrentThreadId();
            uint tgtThread = GetWindowThreadProcessId(targetHwnd, IntPtr.Zero);

            // Attach our thread's input queue to the target thread so
            // SetActiveWindow() affects the target thread's active window.
            if (tgtThread != myThread)
                AttachThreadInput(myThread, tgtThread, true);

            IntPtr prevActive = GetActiveWindow();
            SetActiveWindow(targetHwnd);

            return (tgtThread, myThread, prevActive);
        }

        /// <summary>
        /// Restores the previous active window and detaches the thread-input link
        /// created by <see cref="SilentActivate"/>.
        /// </summary>
        public static void SilentDeactivate((uint targetThread, uint myThread, IntPtr prevActive) token)
        {
            if (token.prevActive != IntPtr.Zero)
                SetActiveWindow(token.prevActive);

            if (token.targetThread != token.myThread)
                AttachThreadInput(token.myThread, token.targetThread, false);
        }
    }
}
