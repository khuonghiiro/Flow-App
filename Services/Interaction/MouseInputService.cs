using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FlowMy.Services.Interaction
{
    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        ScrollUp,
        ScrollDown
    }

    public class MouseInputService
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
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

        private const int INPUT_MOUSE             = 0;
        private const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP     = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP   = 0x0040;
        private const uint MOUSEEVENTF_WHEEL      = 0x0800;
        private const uint WHEEL_DELTA            = 120;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP   = 0x0002;
        private const byte VK_SHIFT          = 0x10;
        private const byte VK_CONTROL        = 0x11;
        private const byte VK_MENU           = 0x12; // Alt

        #endregion

        private POINT _savedPosBeforeDrag;

        // ─── Cursor visibility (dùng khi macro playback) ─────────────────────────

        /// <summary>
        /// Ẩn con trỏ hệ thống. Gọi trước khi macro bắt đầu phát lại.
        /// ShowCursor dùng reference count — gọi Hide 1 lần thì phải Show 1 lần.
        /// </summary>
        public void HideSystemCursor()
        {
            // ShowCursor(false) decrements ref count; keep calling until < 0
            int count = ShowCursor(false);
            while (count >= 0)
                count = ShowCursor(false);
        }

        /// <summary>
        /// Hiện lại con trỏ hệ thống. Gọi sau khi macro kết thúc.
        /// </summary>
        public void ShowSystemCursor()
        {
            // ShowCursor(true) increments ref count; keep calling until >= 0
            int count = ShowCursor(true);
            while (count < 0)
                count = ShowCursor(true);
        }

        // ─── Macro playback: lưu vị trí chuột → di chuyển → thực thi → trả về ────

        /// <summary>
        /// Click tại tọa độ action, sau đó trả chuột về vị trí cũ của user.
        /// </summary>
        public void SendMouseClickAt(int screenX, int screenY, MouseButton button,
                                     int repeatCount = 1, double holdDurationSeconds = 0,
                                     bool shiftHeld = false, bool ctrlHeld = false, bool altHeld = false)
        {
            if (repeatCount < 1) repeatCount = 1;
            if (holdDurationSeconds < 0) holdDurationSeconds = 0;

            GetCursorPos(out POINT saved);
            SetCursorPos(screenX, screenY);

            for (int i = 0; i < repeatCount; i++)
            {
                if (ctrlHeld)  keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
                if (altHeld)   keybd_event(VK_MENU,    0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
                if (shiftHeld) keybd_event(VK_SHIFT,   0, KEYEVENTF_KEYDOWN, IntPtr.Zero);

                SendRawDown(button);
                Thread.Sleep(20);
                SendRawUp(button);

                if (shiftHeld) keybd_event(VK_SHIFT,   0, KEYEVENTF_KEYUP, IntPtr.Zero);
                if (altHeld)   keybd_event(VK_MENU,    0, KEYEVENTF_KEYUP, IntPtr.Zero);
                if (ctrlHeld)  keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, IntPtr.Zero);

                if (i < repeatCount - 1) Thread.Sleep(50);
            }

            SetCursorPos(saved.X, saved.Y);
        }

        /// <summary>Lưu vị trí chuột hiện tại trước khi drag.</summary>
        public void SaveCursorPos() => GetCursorPos(out _savedPosBeforeDrag);

        public void SendMouseDownAt(int screenX, int screenY, MouseButton button,
                                    bool shiftHeld = false, bool ctrlHeld = false, bool altHeld = false)
        {
            // Di chuyển chuột thật đến vị trí action
            GetCursorPos(out POINT saved);
            SetCursorPos(screenX, screenY);
            Thread.Sleep(10); // Đợi Windows xử lý SetCursorPos
            
            // Nhấn modifier keys
            if (ctrlHeld)  keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
            if (altHeld)   keybd_event(VK_MENU,    0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
            if (shiftHeld) keybd_event(VK_SHIFT,   0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
            
            // Nhấn chuột xuống
            SendRawDown(button);
            Thread.Sleep(10);
            
            // Lưu vị trí để MouseUp trả về
            _savedPosBeforeDrag = saved;
        }

        public void SendMouseUpAt(int screenX, int screenY, MouseButton button,
                                  bool shiftHeld = false, bool ctrlHeld = false, bool altHeld = false)
        {
            // Di chuyển chuột thật đến vị trí thả
            SetCursorPos(screenX, screenY);
            Thread.Sleep(10);
            
            // Thả chuột
            SendRawUp(button);
            Thread.Sleep(10);
            
            // Luôn đảm bảo thả modifier keys để tránh bị kẹt (ví dụ: user nhả Shift trước khi nhả chuột khi ghi)
            keybd_event(VK_SHIFT,   0, KEYEVENTF_KEYUP, IntPtr.Zero);
            keybd_event(VK_MENU,    0, KEYEVENTF_KEYUP, IntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            
            // Trả chuột về vị trí user ban đầu
            SetCursorPos(_savedPosBeforeDrag.X, _savedPosBeforeDrag.Y);
        }

        /// <summary>
        /// Scroll tại tọa độ action, sau đó trả chuột về vị trí cũ.
        /// </summary>
        public void SendMouseScrollAt(int screenX, int screenY, int notches)
        {
            if (notches == 0) return;
            GetCursorPos(out POINT saved);
            SetCursorPos(screenX, screenY);
            int delta = notches * (int)WHEEL_DELTA;
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi   = new MOUSEINPUT { dwFlags = MOUSEEVENTF_WHEEL, mouseData = (uint)delta }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
            SetCursorPos(saved.X, saved.Y);
        }

        /// <summary>
        /// Giải phóng tất cả modifier keys (Shift/Ctrl/Alt) để tránh bị dính phím
        /// sau khi workflow kết thúc hoặc bị hủy giữa chừng.
        /// </summary>
        public void ReleaseAllModifiers()
        {
            keybd_event(VK_SHIFT,   0, KEYEVENTF_KEYUP, IntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            keybd_event(VK_MENU,    0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }

        private static uint SendRawDown(MouseButton button)
        {
            uint flag = button switch
            {
                MouseButton.Right  => MOUSEEVENTF_RIGHTDOWN,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
                _                  => MOUSEEVENTF_LEFTDOWN
            };
            var input = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = flag } };
            return SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        private static uint SendRawUp(MouseButton button)
        {
            uint flag = button switch
            {
                MouseButton.Right  => MOUSEEVENTF_RIGHTUP,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
                _                  => MOUSEEVENTF_LEFTUP
            };
            var input = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = flag } };
            return SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Click chuột (nhấn + thả)
        /// </summary>
        public void SendMouseClick(MouseButton button, int repeatCount = 1, double holdDurationSeconds = 0)
        {
            if (repeatCount < 1) repeatCount = 1;
            if (holdDurationSeconds < 0) holdDurationSeconds = 0;

            for (int i = 0; i < repeatCount; i++)
            {
                if (button == MouseButton.ScrollUp || button == MouseButton.ScrollDown)
                {
                    SendMouseScroll(button, 1);
                }
                else
                {
                    SendMouseDown(button);

                    if (holdDurationSeconds > 0)
                    {
                        Thread.Sleep((int)(holdDurationSeconds * 1000));
                    }

                    SendMouseUp(button);
                }

                // Delay ngắn giữa các lần click
                if (i < repeatCount - 1)
                {
                    Thread.Sleep(50);
                }
            }
        }

        /// <summary>
        /// Giữ chuột (chỉ nhấn xuống, không thả)
        /// </summary>
        public void SendMouseDown(MouseButton button)
        {
            uint flag = button switch
            {
                MouseButton.Left => MOUSEEVENTF_LEFTDOWN,
                MouseButton.Right => MOUSEEVENTF_RIGHTDOWN,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
                _ => throw new ArgumentException($"Invalid button for MouseDown: {button}")
            };

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dwFlags = flag,
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Thả chuột (chỉ thả, không nhấn)
        /// </summary>
        public void SendMouseUp(MouseButton button)
        {
            uint flag = button switch
            {
                MouseButton.Left => MOUSEEVENTF_LEFTUP,
                MouseButton.Right => MOUSEEVENTF_RIGHTUP,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
                _ => throw new ArgumentException($"Invalid button for MouseUp: {button}")
            };

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dwFlags = flag,
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Lăn chuột (scroll)
        /// </summary>
        public void SendMouseScroll(MouseButton direction, int scrollAmount = 1)
        {
            if (direction != MouseButton.ScrollUp && direction != MouseButton.ScrollDown)
            {
                throw new ArgumentException("Direction must be ScrollUp or ScrollDown");
            }

            if (scrollAmount < 1) scrollAmount = 1;

            int delta = direction == MouseButton.ScrollUp 
                ? (int)WHEEL_DELTA 
                : -(int)WHEEL_DELTA;

            for (int i = 0; i < scrollAmount; i++)
            {
                var input = new INPUT
                {
                    type = INPUT_MOUSE,
                    mi = new MOUSEINPUT
                    {
                        dwFlags = MOUSEEVENTF_WHEEL,
                        dx = 0,
                        dy = 0,
                        mouseData = (uint)delta,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                };

                SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));

                // Delay ngắn giữa các lần scroll
                if (i < scrollAmount - 1)
                {
                    Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// Giữ chuột trong khoảng thời gian (async)
        /// </summary>
        public async Task HoldMouseAsync(MouseButton button, double durationSeconds, CancellationToken cancellationToken = default)
        {
            if (durationSeconds <= 0) return;
            if (button == MouseButton.ScrollUp || button == MouseButton.ScrollDown)
            {
                throw new ArgumentException("Cannot hold scroll buttons");
            }

            SendMouseDown(button);

            try
            {
                await Task.Delay((int)(durationSeconds * 1000), cancellationToken);
            }
            finally
            {
                SendMouseUp(button);
            }
        }
    }
}