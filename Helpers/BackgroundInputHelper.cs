using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;

namespace FlowMy.Helpers
{
    /// <summary>
    /// Background Input Helper - Gửi input đến window không cần active (giống AnyDesk/TeamViewer).
    /// Hỗ trợ 3 chế độ:
    /// 1. DirectMessage: Gửi WM_* message trực tiếp (nhanh nhất, ít tương thích)
    /// 2. SilentActivation: Dùng SendInput + silent activation (tương thích cao)
    /// 3. UIAutomation: Dùng UI Automation framework (chậm nhất, tương thích cao nhất)
    /// </summary>
    public static class BackgroundInputHelper
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT pt);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const int SW_RESTORE = 9;
        private const int SW_SHOWNOACTIVATE = 4;

        // Windows Messages
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_MBUTTONDOWN = 0x0207;
        private const uint WM_MBUTTONUP = 0x0208;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_MOUSEWHEEL = 0x020A;

        // SendInput constants
        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        #endregion

        /// <summary>
        /// Chế độ gửi input - từ tương thích thấp (nhanh) đến cao (chậm)
        /// </summary>
        public enum InputMode
        {
            /// <summary>Gửi WM_* message trực tiếp - nhanh nhất, ít tương thích (không hoạt động với game/DirectX)</summary>
            DirectMessage,

            /// <summary>SendInput + silent activation - cân bằng giữa tốc độ và tương thích</summary>
            SilentActivation,

            /// <summary>SendInput + foreground activation - giống user thật nhưng làm gián đoạn</summary>
            ForegroundActivation,

            /// <summary>
            /// Kernel-level injection qua Interception driver - hoạt động với MỌI app kể cả game, browser, UWP.
            /// Không cần app active, không gián đoạn user.
            /// YÊU CẦU: Interception driver phải được cài (cần admin lần đầu).
            /// </summary>
            InterceptionDriver,

            /// <summary>Tự động chọn chế độ phù hợp (ưu tiên InterceptionDriver nếu có)</summary>
            Auto
        }

        #region Public API

        /// <summary>
        /// Gửi chuỗi text đến window chỉ định (không cần active).
        /// </summary>
        /// <param name="targetHwnd">Handle của window đích</param>
        /// <param name="text">Chuỗi text cần gửi</param>
        /// <param name="mode">Chế độ gửi input (mặc định Auto)</param>
        /// <param name="delayMs">Delay giữa các ký tự (ms)</param>
        public static bool SendText(IntPtr targetHwnd, string text, InputMode mode = InputMode.Auto, int delayMs = 10)
        {
            if (targetHwnd == IntPtr.Zero || string.IsNullOrEmpty(text))
                return false;

            // Auto: ưu tiên InterceptionDriver nếu có, fallback DirectMessage
            if (mode == InputMode.Auto)
                mode = InterceptionInputHelper.IsAvailable() ? InputMode.InterceptionDriver : InputMode.DirectMessage;

            try
            {
                switch (mode)
                {
                    case InputMode.InterceptionDriver:
                        foreach (char c in text)
                        {
                            short vkScan = VkKeyScanChar(c);
                            if (vkScan != -1)
                            {
                                ushort vk = (ushort)(vkScan & 0xFF);
                                InterceptionInputHelper.SendKey(vk, delayMs);
                            }
                        }
                        return true;

                    case InputMode.DirectMessage:
                        return SendTextDirectMessage(targetHwnd, text, delayMs);

                    case InputMode.SilentActivation:
                        return SendTextSilentActivation(targetHwnd, text, delayMs);

                    case InputMode.ForegroundActivation:
                        return SendTextForegroundActivation(targetHwnd, text, delayMs);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundInputHelper] SendText error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Nhấn phím đến window chỉ định (không cần active).
        /// </summary>
        /// <param name="targetHwnd">Handle của window đích</param>
        /// <param name="vkCode">Virtual key code (VK_*)</param>
        /// <param name="mode">Chế độ gửi input</param>
        public static bool SendKey(IntPtr targetHwnd, ushort vkCode, InputMode mode = InputMode.Auto)
        {
            if (targetHwnd == IntPtr.Zero)
                return false;

            if (mode == InputMode.Auto)
                mode = InterceptionInputHelper.IsAvailable() ? InputMode.InterceptionDriver : InputMode.DirectMessage;

            try
            {
                switch (mode)
                {
                    case InputMode.InterceptionDriver:
                        return InterceptionInputHelper.SendKey(vkCode);

                    case InputMode.DirectMessage:
                        return SendKeyDirectMessage(targetHwnd, vkCode);

                    case InputMode.SilentActivation:
                        return SendKeySilentActivation(targetHwnd, vkCode);

                    case InputMode.ForegroundActivation:
                        return SendKeyForegroundActivation(targetHwnd, vkCode);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundInputHelper] SendKey error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Chỉ nhấn xuống (mouse down) — không thả. Dùng cùng với SendMouseUp để tạo click có hold.
        /// </summary>
        public static bool SendMouseDown(IntPtr targetHwnd, int screenX, int screenY, string button = "Left", InputMode mode = InputMode.Auto)
        {
            if (targetHwnd == IntPtr.Zero) return false;
            if (mode == InputMode.Auto)
                mode = InterceptionInputHelper.IsAvailable() ? InputMode.InterceptionDriver : InputMode.DirectMessage;

            try
            {
                POINT pt = new POINT { X = screenX, Y = screenY };
                ScreenToClient(targetHwnd, ref pt);
                IntPtr lParam = MakeLParam(pt.X, pt.Y);

                uint downMsg = button switch
                {
                    "Right"  => WM_RBUTTONDOWN,
                    "Middle" => WM_MBUTTONDOWN,
                    _        => WM_LBUTTONDOWN
                };

                if (mode == InputMode.InterceptionDriver)
                    return InterceptionInputHelper.SendMouseDown(screenX, screenY, button);
                else if (mode == InputMode.SilentActivation)
                {
                    SetWindowPos(targetHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    PostMessage(targetHwnd, downMsg, IntPtr.Zero, lParam);
                    SetWindowPos(targetHwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
                else
                {
                    PostMessage(targetHwnd, downMsg, IntPtr.Zero, lParam);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundInputHelper] SendMouseDown error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Chỉ thả chuột (mouse up) — dùng sau SendMouseDown.
        /// </summary>
        public static bool SendMouseUp(IntPtr targetHwnd, int screenX, int screenY, string button = "Left", InputMode mode = InputMode.Auto)
        {
            if (targetHwnd == IntPtr.Zero) return false;
            if (mode == InputMode.Auto)
                mode = InterceptionInputHelper.IsAvailable() ? InputMode.InterceptionDriver : InputMode.DirectMessage;

            try
            {
                POINT pt = new POINT { X = screenX, Y = screenY };
                ScreenToClient(targetHwnd, ref pt);
                IntPtr lParam = MakeLParam(pt.X, pt.Y);

                uint upMsg = button switch
                {
                    "Right"  => WM_RBUTTONUP,
                    "Middle" => WM_MBUTTONUP,
                    _        => WM_LBUTTONUP
                };

                if (mode == InputMode.InterceptionDriver)
                    return InterceptionInputHelper.SendMouseUp(screenX, screenY, button);
                else if (mode == InputMode.SilentActivation)
                {
                    SetWindowPos(targetHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    PostMessage(targetHwnd, upMsg, IntPtr.Zero, lParam);
                    SetWindowPos(targetHwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
                else
                {
                    PostMessage(targetHwnd, upMsg, IntPtr.Zero, lParam);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundInputHelper] SendMouseUp error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Nhấn chuột tại vị trí chỉ định trên window (không cần active).
        /// </summary>
        /// <param name="targetHwnd">Handle của window đích</param>
        /// <param name="screenX">Tọa độ X trên màn hình</param>
        /// <param name="screenY">Tọa độ Y trên màn hình</param>
        /// <param name="button">Nút chuột: "Left", "Right", "Middle"</param>
        /// <param name="mode">Chế độ gửi input</param>
        public static bool SendMouseClick(IntPtr targetHwnd, int screenX, int screenY, string button = "Left", InputMode mode = InputMode.Auto)
        {
            if (targetHwnd == IntPtr.Zero)
                return false;

            if (mode == InputMode.Auto)
                mode = InterceptionInputHelper.IsAvailable() ? InputMode.InterceptionDriver : InputMode.DirectMessage;

            try
            {
                switch (mode)
                {
                    case InputMode.InterceptionDriver:
                        return InterceptionInputHelper.SendMouseClick(screenX, screenY, button);

                    case InputMode.DirectMessage:
                        return SendMouseClickDirectMessage(targetHwnd, screenX, screenY, button);

                    case InputMode.SilentActivation:
                        return SendMouseClickSilentActivation(targetHwnd, screenX, screenY, button);

                    case InputMode.ForegroundActivation:
                        return SendMouseClickForegroundActivation(targetHwnd, screenX, screenY, button);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundInputHelper] SendMouseClick error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cuộn chuột tại vị trí chỉ định trên window (không cần active).
        /// </summary>
        public static bool SendMouseScroll(IntPtr targetHwnd, int screenX, int screenY, int delta, InputMode mode = InputMode.Auto)
        {
            if (targetHwnd == IntPtr.Zero)
                return false;

            if (mode == InputMode.Auto)
                mode = InterceptionInputHelper.IsAvailable() ? InputMode.InterceptionDriver : InputMode.DirectMessage;

            try
            {
                switch (mode)
                {
                    case InputMode.InterceptionDriver:
                        return InterceptionInputHelper.SendMouseScroll(screenX, screenY, delta);

                    case InputMode.DirectMessage:
                    case InputMode.SilentActivation:
                    case InputMode.ForegroundActivation:
                        return SendMouseScrollDirectMessage(targetHwnd, screenX, screenY, delta);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundInputHelper] SendMouseScroll error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region DirectMessage Implementation

        private static bool SendTextDirectMessage(IntPtr hWnd, string text, int delayMs)
        {
            foreach (char c in text)
            {
                // Gửi WM_CHAR cho mỗi ký tự
                PostMessage(hWnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                if (delayMs > 0)
                    Thread.Sleep(delayMs);
            }
            return true;
        }

        private static bool SendKeyDirectMessage(IntPtr hWnd, ushort vkCode)
        {
            uint scanCode = MapVirtualKey(vkCode, 0); // MAPVK_VK_TO_VSC
            IntPtr lParam = MakeLParam(1, (int)scanCode, 0, 0);

            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)vkCode, lParam);
            Thread.Sleep(50);
            PostMessage(hWnd, WM_KEYUP, (IntPtr)vkCode, lParam);
            return true;
        }

        private static bool SendMouseClickDirectMessage(IntPtr hWnd, int screenX, int screenY, string button)
        {
            // Convert screen to client coordinates
            POINT pt = new POINT { X = screenX, Y = screenY };
            ScreenToClient(hWnd, ref pt);

            IntPtr lParam = MakeLParam(pt.X, pt.Y);

            uint downMsg = button switch
            {
                "Right" => WM_RBUTTONDOWN,
                "Middle" => WM_MBUTTONDOWN,
                _ => WM_LBUTTONDOWN
            };

            uint upMsg = button switch
            {
                "Right" => WM_RBUTTONUP,
                "Middle" => WM_MBUTTONUP,
                _ => WM_LBUTTONUP
            };

            PostMessage(hWnd, downMsg, IntPtr.Zero, lParam);
            Thread.Sleep(50);
            PostMessage(hWnd, upMsg, IntPtr.Zero, lParam);
            return true;
        }

        private static bool SendMouseScrollDirectMessage(IntPtr hWnd, int screenX, int screenY, int delta)
        {
            POINT pt = new POINT { X = screenX, Y = screenY };
            ScreenToClient(hWnd, ref pt);

            IntPtr lParam = MakeLParam(pt.X, pt.Y);
            IntPtr wParam = MakeLParam(0, delta * 120); // WHEEL_DELTA = 120

            PostMessage(hWnd, WM_MOUSEWHEEL, wParam, lParam);
            return true;
        }

        #endregion

        #region SilentActivation Implementation

        private static bool SendTextSilentActivation(IntPtr hWnd, string text, int delayMs)
        {
            // Đưa window lên top (không activate để không gây flash)
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            try
            {
                // Silent focus để SendInput đi đến đúng window
                var token = WindowHelper.SilentKeyboardFocus(hWnd);

                try
                {
                    foreach (char c in text)
                    {
                        WindowHelper.SendHwChar(c);
                        if (delayMs > 0)
                            Thread.Sleep(delayMs);
                    }
                }
                finally
                {
                    WindowHelper.SilentKeyboardFocusRestore(token);
                }
            }
            finally
            {
                // Restore non-topmost
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }

            return true;
        }

        private static bool SendKeySilentActivation(IntPtr hWnd, ushort vkCode)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            try
            {
                var token = WindowHelper.SilentKeyboardFocus(hWnd);
                try
                {
                    WindowHelper.SendHwVKey(vkCode);
                }
                finally
                {
                    WindowHelper.SilentKeyboardFocusRestore(token);
                }
            }
            finally
            {
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }

            return true;
        }

        private static bool SendMouseClickSilentActivation(IntPtr hWnd, int screenX, int screenY, string button)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            try
            {
                bool isRight = button == "Right";
                WindowHelper.SendHwMouseClick(screenX, screenY, isRight);
            }
            finally
            {
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }

            return true;
        }

        #endregion

        #region ForegroundActivation Implementation

        private static bool SendTextForegroundActivation(IntPtr hWnd, string text, int delayMs)
        {
            IntPtr prevForeground = GetForegroundWindow();

            // Đưa window lên foreground
            ActivateWindow(hWnd);
            Thread.Sleep(100); // Đợi window active

            try
            {
                foreach (char c in text)
                {
                    WindowHelper.SendHwChar(c);
                    if (delayMs > 0)
                        Thread.Sleep(delayMs);
                }
            }
            finally
            {
                // Restore previous foreground
                if (prevForeground != IntPtr.Zero)
                {
                    Thread.Sleep(50);
                    SetForegroundWindow(prevForeground);
                }
            }

            return true;
        }

        private static bool SendKeyForegroundActivation(IntPtr hWnd, ushort vkCode)
        {
            IntPtr prevForeground = GetForegroundWindow();

            ActivateWindow(hWnd);
            Thread.Sleep(100);

            try
            {
                WindowHelper.SendHwVKey(vkCode);
            }
            finally
            {
                if (prevForeground != IntPtr.Zero)
                {
                    Thread.Sleep(50);
                    SetForegroundWindow(prevForeground);
                }
            }

            return true;
        }

        private static bool SendMouseClickForegroundActivation(IntPtr hWnd, int screenX, int screenY, string button)
        {
            IntPtr prevForeground = GetForegroundWindow();

            ActivateWindow(hWnd);
            Thread.Sleep(100);

            try
            {
                bool isRight = button == "Right";
                WindowHelper.SendHwMouseClick(screenX, screenY, isRight);
            }
            finally
            {
                if (prevForeground != IntPtr.Zero)
                {
                    Thread.Sleep(50);
                    SetForegroundWindow(prevForeground);
                }
            }

            return true;
        }

        #endregion

        #region Helper Methods

        private static IntPtr MakeLParam(int loWord, int hiWord)
        {
            return (IntPtr)((hiWord << 16) | (loWord & 0xFFFF));
        }

        private static IntPtr MakeLParam(int loWord, int hiWord, int bit24, int bit31)
        {
            return (IntPtr)((hiWord << 16) | (loWord & 0xFFFF));
        }

        private static void ActivateWindow(IntPtr hWnd)
        {
            if (IsIconic(hWnd))
                ShowWindow(hWnd, SW_RESTORE);
            else if (!IsWindowVisible(hWnd))
                ShowWindow(hWnd, SW_SHOWNOACTIVATE);

            SetForegroundWindow(hWnd);
        }

        /// <summary>
        /// Test xem window có hỗ trợ DirectMessage không (trả về true nếu nên dùng DirectMessage).
        /// </summary>
        public static bool TestDirectMessageSupport(IntPtr hWnd)
        {
            // Heuristic: game/DirectX thường không hỗ trợ DirectMessage
            // Kiểm tra class name để phát hiện
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            string cls = className.ToString().ToLower();

            // Các class không hỗ trợ DirectMessage
            string[] unsupportedClasses = { "unitywindowclass", "unrealwindow", "cryengine", "d3d" };
            foreach (string unsupported in unsupportedClasses)
            {
                if (cls.Contains(unsupported))
                    return false;
            }

            return true;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern short VkKeyScanW(char ch);

        private static short VkKeyScanChar(char ch) => VkKeyScanW(ch);

        #endregion
    }
}
