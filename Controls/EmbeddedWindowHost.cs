using System;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace FlowMy.Controls
{
    /// <summary>
    /// WPF Control để host external Windows vào trong Flow-App.
    /// Usage:
    /// <code>
    /// var host = new EmbeddedWindowHost();
    /// host.EmbedWindow(paintHwnd); // Embed Paint window
    /// // Paint window giờ là child của Flow-App
    /// // Input routing được control bởi Flow-App
    /// </code>
    /// </summary>
    public class EmbeddedWindowHost : HwndHost
    {
        private IntPtr _embeddedWindowHandle = IntPtr.Zero;
        private IntPtr _hostWindowHandle = IntPtr.Zero;
        private bool _removeDecoration = true;

        #region P/Invoke

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        #endregion

        /// <summary>
        /// Handle của window được embed.
        /// </summary>
        public IntPtr EmbeddedWindowHandle => _embeddedWindowHandle;

        /// <summary>
        /// Có bỏ title bar và borders không.
        /// </summary>
        public bool RemoveDecoration
        {
            get => _removeDecoration;
            set => _removeDecoration = value;
        }

        /// <summary>
        /// Event được raise khi window được embed thành công.
        /// </summary>
        public event EventHandler<IntPtr>? WindowEmbedded;

        /// <summary>
        /// Event được raise khi window bị unembed.
        /// </summary>
        public event EventHandler? WindowUnembedded;

        /// <summary>
        /// Embed một external window vào control này.
        /// </summary>
        public bool EmbedWindow(IntPtr externalWindowHandle)
        {
            if (externalWindowHandle == IntPtr.Zero)
            {
                Debug.WriteLine("[EmbeddedWindowHost] ⚠️ Invalid window handle");
                return false;
            }

            if (!Helpers.WindowHostHelper.CanEmbedWindow(externalWindowHandle))
            {
                Debug.WriteLine("[EmbeddedWindowHost] ⚠️ Cannot embed this window (system window or restricted)");
                return false;
            }

            _embeddedWindowHandle = externalWindowHandle;

            Debug.WriteLine($"[EmbeddedWindowHost] Embedding window: {Helpers.WindowHostHelper.GetWindowInfo(externalWindowHandle)}");

            // If already initialized, embed now
            if (_hostWindowHandle != IntPtr.Zero)
            {
                return PerformEmbed();
            }

            // Otherwise will embed in BuildWindowCore
            return true;
        }

        /// <summary>
        /// Unembed current window và restore về trạng thái normal.
        /// </summary>
        public void UnembedWindow()
        {
            if (_embeddedWindowHandle == IntPtr.Zero)
                return;

            Debug.WriteLine("[EmbeddedWindowHost] Unembedding window...");
            Helpers.WindowHostHelper.UnembedWindow(_embeddedWindowHandle);
            
            _embeddedWindowHandle = IntPtr.Zero;
            WindowUnembedded?.Invoke(this, EventArgs.Empty);
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hostWindowHandle = hwndParent.Handle;
            Debug.WriteLine($"[EmbeddedWindowHost] BuildWindowCore: parent hwnd={_hostWindowHandle}");

            // Create a simple child window as container
            // This satisfies HwndHost's requirement for a child window
            IntPtr containerHwnd = CreateWindowEx(
                0,
                "static", // Simple static window class
                "",
                WS_CHILD | WS_VISIBLE,
                0, 0,
                (int)this.ActualWidth, (int)this.ActualHeight,
                _hostWindowHandle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            Debug.WriteLine($"[EmbeddedWindowHost] Created container window: hwnd={containerHwnd}");

            // If we have a window to embed, embed it into the container
            if (_embeddedWindowHandle != IntPtr.Zero && containerHwnd != IntPtr.Zero)
            {
                // Update host to use container
                _hostWindowHandle = containerHwnd;
                PerformEmbed();
            }

            return new HandleRef(this, containerHwnd);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x, int y,
            int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            Debug.WriteLine("[EmbeddedWindowHost] DestroyWindowCore");
            UnembedWindow();
            
            // Destroy container window
            if (hwnd.Handle != IntPtr.Zero)
            {
                DestroyWindow(hwnd.Handle);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (_embeddedWindowHandle != IntPtr.Zero && _hostWindowHandle != IntPtr.Zero)
            {
                // Resize embedded window to fill host
                int width = (int)sizeInfo.NewSize.Width;
                int height = (int)sizeInfo.NewSize.Height;
                
                Debug.WriteLine($"[EmbeddedWindowHost] Resizing embedded window to {width}x{height}");
                Helpers.WindowHostHelper.ResizeEmbeddedWindow(_embeddedWindowHandle, 0, 0, width, height);
            }
        }

        private bool PerformEmbed()
        {
            if (_embeddedWindowHandle == IntPtr.Zero || _hostWindowHandle == IntPtr.Zero)
                return false;

            bool success = Helpers.WindowHostHelper.EmbedWindow(
                _embeddedWindowHandle, 
                _hostWindowHandle, 
                _removeDecoration);

            if (success)
            {
                // Resize to fill host
                MoveWindow(_embeddedWindowHandle, 0, 0, 
                    (int)this.ActualWidth, (int)this.ActualHeight, true);

                Debug.WriteLine($"[EmbeddedWindowHost] ✅ Window embedded and resized");
                WindowEmbedded?.Invoke(this, _embeddedWindowHandle);
            }
            else
            {
                Debug.WriteLine($"[EmbeddedWindowHost] ❌ Failed to embed window");
            }

            return success;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnembedWindow();
            }
            base.Dispose(disposing);
        }
    }
}
