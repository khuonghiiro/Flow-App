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
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const int SM_XVIRTUALSCREEN  = 76;
        private const int SM_YVIRTUALSCREEN  = 77;

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

        private const int INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_MOVE         = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN     = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP       = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN    = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP      = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN   = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP     = 0x0040;
        private const uint MOUSEEVENTF_WHEEL        = 0x0800;
        private const uint MOUSEEVENTF_ABSOLUTE     = 0x8000;
        private const uint MOUSEEVENTF_VIRTUALDESK  = 0x4000;

        private const uint WHEEL_DELTA = 120;

        #endregion

        // ─── Coordinate helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Convert screen pixel coordinates to the normalized 0–65535 range
        /// required by MOUSEEVENTF_ABSOLUTE across the full virtual desktop.
        /// </summary>
        private static (int nx, int ny) ToAbsoluteCoords(int screenX, int screenY)
        {
            int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);

            // Clamp to virtual screen bounds
            int px = Math.Max(vx, Math.Min(screenX, vx + vw - 1));
            int py = Math.Max(vy, Math.Min(screenY, vy + vh - 1));

            int nx = (int)(((long)(px - vx) * 65535 + vw / 2) / vw);
            int ny = (int)(((long)(py - vy) * 65535 + vh / 2) / vh);
            return (nx, ny);
        }

        // ─── Absolute-position input (for macro playback — does NOT move real cursor) ──

        /// <summary>
        /// Inject a mouse click at absolute screen coordinates WITHOUT moving the real cursor.
        /// Uses MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK so the event is delivered
        /// at the target position regardless of where the physical cursor is.
        /// </summary>
        public void SendMouseClickAt(int screenX, int screenY, MouseButton button,
                                     int repeatCount = 1, double holdDurationSeconds = 0)
        {
            if (repeatCount < 1) repeatCount = 1;
            if (holdDurationSeconds < 0) holdDurationSeconds = 0;

            var (nx, ny) = ToAbsoluteCoords(screenX, screenY);
            uint downFlag = AbsDownFlag(button);
            uint upFlag   = AbsUpFlag(button);

            for (int i = 0; i < repeatCount; i++)
            {
                SendAbsoluteEvent(nx, ny, downFlag);

                if (holdDurationSeconds > 0)
                    Thread.Sleep((int)(holdDurationSeconds * 1000));

                SendAbsoluteEvent(nx, ny, upFlag);

                if (i < repeatCount - 1)
                    Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Inject a mouse-down at absolute screen coordinates WITHOUT moving the real cursor.
        /// </summary>
        public void SendMouseDownAt(int screenX, int screenY, MouseButton button)
        {
            var (nx, ny) = ToAbsoluteCoords(screenX, screenY);
            SendAbsoluteEvent(nx, ny, AbsDownFlag(button));
        }

        /// <summary>
        /// Inject a mouse-up at absolute screen coordinates WITHOUT moving the real cursor.
        /// </summary>
        public void SendMouseUpAt(int screenX, int screenY, MouseButton button)
        {
            var (nx, ny) = ToAbsoluteCoords(screenX, screenY);
            SendAbsoluteEvent(nx, ny, AbsUpFlag(button));
        }

        /// <summary>
        /// Inject a scroll event at absolute screen coordinates WITHOUT moving the real cursor.
        /// </summary>
        public void SendMouseScrollAt(int screenX, int screenY, int notches)
        {
            if (notches == 0) return;
            var (nx, ny) = ToAbsoluteCoords(screenX, screenY);
            int delta = notches * (int)WHEEL_DELTA;

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx        = nx,
                    dy        = ny,
                    dwFlags   = MOUSEEVENTF_WHEEL | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                    mouseData = (uint)delta,
                    time      = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void SendAbsoluteEvent(int nx, int ny, uint actionFlag)
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx        = nx,
                    dy        = ny,
                    dwFlags   = actionFlag | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                    mouseData = 0,
                    time      = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        private static uint AbsDownFlag(MouseButton button) => button switch
        {
            MouseButton.Left   => MOUSEEVENTF_LEFTDOWN,
            MouseButton.Right  => MOUSEEVENTF_RIGHTDOWN,
            MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
            _ => throw new ArgumentException($"Invalid button for MouseDown: {button}")
        };

        private static uint AbsUpFlag(MouseButton button) => button switch
        {
            MouseButton.Left   => MOUSEEVENTF_LEFTUP,
            MouseButton.Right  => MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
            _ => throw new ArgumentException($"Invalid button for MouseUp: {button}")
        };

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