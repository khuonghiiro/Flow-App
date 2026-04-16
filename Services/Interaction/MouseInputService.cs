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
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        private const uint WHEEL_DELTA = 120;

        #endregion

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