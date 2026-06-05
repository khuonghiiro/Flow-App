using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FlowMy.Helpers
{
    /// <summary>
    /// Window Hosting Helper - Embed external windows vào WPF container.
    /// Cho phép host bất kỳ window nào (Paint, Notepad, Browser...) vào trong Flow-App.
    /// Khi đó Flow-App control được activation, focus, và input routing.
    /// </summary>
    public static class WindowHostHelper
    {
        #region P/Invoke

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr hwnd, EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        private const uint WM_THEMECHANGED = 0x031A;
        private const uint RDW_INVALIDATE  = 0x0001;
        private const uint RDW_ERASE       = 0x0004;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_UPDATENOW   = 0x0100;
        private const uint RDW_FRAME       = 0x0400;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // Window styles
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;

        private const int WS_EX_DLGMODALFRAME = 0x00000001;
        private const int WS_EX_WINDOWEDGE    = 0x00000100;
        // WS_EX_TOOLWINDOW (0x80) KHÔNG strip – một số app dùng nó cho toolbar rendering

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        #endregion

        /// <summary>
        /// Embed một external window vào container (WPF HwndHost hoặc bất kỳ window nào).
        /// </summary>
        /// <param name="childHwnd">Handle của window muốn embed (Paint, Notepad...)</param>
        /// <param name="parentHwnd">Handle của container window (Flow-App)</param>
        /// <param name="removeDecoration">Có bỏ title bar, borders không</param>
        /// <returns>True nếu thành công</returns>
        public static bool EmbedWindow(IntPtr childHwnd, IntPtr parentHwnd, bool removeDecoration = true)
        {
            if (childHwnd == IntPtr.Zero || parentHwnd == IntPtr.Zero)
                return false;

            try
            {
                Debug.WriteLine($"[WindowHost] Embedding window hwnd={childHwnd} into parent={parentHwnd}");

                // 1. Change window style to WS_CHILD (required for parenting)
                int style = GetWindowLong(childHwnd, GWL_STYLE);
                
                if (removeDecoration)
                {
                    // Remove title bar, borders, system menu
                    style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
                    style &= ~WS_POPUP;
                }
                
                style |= WS_CHILD;
                style |= WS_VISIBLE;

                SetWindowLong(childHwnd, GWL_STYLE, style);

                // 2. Remove extended styles gây vấn đề với child windows
                //    KHÔNG strip WS_EX_TOOLWINDOW – nhiều app cần nó để render toolbar/ribbon
                int exStyle = GetWindowLong(childHwnd, GWL_EXSTYLE);
                exStyle &= ~(WS_EX_DLGMODALFRAME | WS_EX_WINDOWEDGE);
                SetWindowLong(childHwnd, GWL_EXSTYLE, exStyle);

                // 3. Apply style changes
                SetWindowPos(childHwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

                // 4. Set parent (embed the window)
                IntPtr result = SetParent(childHwnd, parentHwnd);
                if (result == IntPtr.Zero)
                {
                    Debug.WriteLine($"[WindowHost] ⚠️ SetParent failed for hwnd={childHwnd}");
                    return false;
                }

                Debug.WriteLine($"[WindowHost] ✅ Window embedded successfully. Previous parent was hwnd={result}");

                // 5. Show the embedded window
                ShowWindow(childHwnd, SW_SHOW);

                // 6. Force theme reload trên app và toàn bộ child controls
                //    (fix: toolbar trong suốt, text trắng khi embed app như Paint)
                Task.Delay(80).ContinueWith(_ => ForceThemeRefresh(childHwnd));

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowHost] ❌ Embed error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gửi WM_THEMECHANGED đến app và tất cả child windows để buộc re-render theme brushes.
        /// Fix: controls trong suốt / text trắng sau khi embed.
        /// </summary>
        private static void ForceThemeRefresh(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                // Gửi WM_THEMECHANGED cho root + toàn bộ children
                SendMessage(hwnd, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                EnumChildWindows(hwnd, (child, _) =>
                {
                    SendMessage(child, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);

                // Redraw toàn bộ cây window
                RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
                    RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW | RDW_FRAME);

                Debug.WriteLine("[WindowHost] ✅ ForceThemeRefresh done");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowHost] ForceThemeRefresh error: {ex.Message}");
            }
        }

        /// <summary>
        /// Resize embedded window to fit container bounds.
        /// </summary>
        public static bool ResizeEmbeddedWindow(IntPtr childHwnd, int x, int y, int width, int height)
        {
            if (childHwnd == IntPtr.Zero)
                return false;

            try
            {
                bool result = MoveWindow(childHwnd, x, y, width, height, true);
                Debug.WriteLine($"[WindowHost] MoveWindow to ({x},{y}) size=({width}x{height}) result={result}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowHost] Resize error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unparent và restore window về trạng thái ban đầu.
        /// </summary>
        public static bool UnembedWindow(IntPtr childHwnd)
        {
            if (childHwnd == IntPtr.Zero)
                return false;

            try
            {
                Debug.WriteLine($"[WindowHost] Unembedding window hwnd={childHwnd}");

                // 1. Remove parent (restore to desktop)
                SetParent(childHwnd, IntPtr.Zero);

                // 2. Restore window style to normal popup window
                int style = GetWindowLong(childHwnd, GWL_STYLE);
                style &= ~WS_CHILD;
                style |= WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_SYSMENU;
                SetWindowLong(childHwnd, GWL_STYLE, style);

                // 3. Apply changes
                SetWindowPos(childHwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);

                // 4. Show as normal window
                ShowWindow(childHwnd, SW_RESTORE);

                Debug.WriteLine($"[WindowHost] ✅ Window unembedded successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowHost] Unembed error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get window info for debugging.
        /// </summary>
        public static string GetWindowInfo(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return "Invalid handle";

            try
            {
                var sb = new System.Text.StringBuilder(256);
                GetWindowText(hwnd, sb, 256);
                string title = sb.ToString();

                GetWindowThreadProcessId(hwnd, out uint processId);
                string processName = "";
                try
                {
                    var process = Process.GetProcessById((int)processId);
                    processName = process.ProcessName;
                }
                catch { }

                GetWindowRect(hwnd, out RECT rect);
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                return $"Hwnd={hwnd}, Title=\"{title}\", Process={processName} (PID={processId}), Size={width}x{height}";
            }
            catch (Exception ex)
            {
                return $"Error getting info: {ex.Message}";
            }
        }

        /// <summary>
        /// Check if a window can be embedded (some system windows cannot be embedded).
        /// </summary>
        public static bool CanEmbedWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            try
            {
                // Check if it's a system window or special window type
                GetWindowThreadProcessId(hwnd, out uint processId);
                
                // Cannot embed windows from system processes
                var process = Process.GetProcessById((int)processId);
                string processName = process.ProcessName.ToLower();
                
                // Blacklist
                string[] cantEmbed = new[]
                {
                    "explorer",     // Windows Explorer
                    "taskmgr",      // Task Manager
                    "dwm",          // Desktop Window Manager
                    "csrss",        // Client/Server Runtime
                    "winlogon",     // Windows Logon
                    "services"      // Services
                };

                foreach (var name in cantEmbed)
                {
                    if (processName.Contains(name))
                    {
                        Debug.WriteLine($"[WindowHost] Cannot embed system window: {processName}");
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Focus and bring a window to foreground.
        /// </summary>
        public static bool FocusWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            try
            {
                Debug.WriteLine($"[WindowHost] Focusing window hwnd={hwnd}");

                // If minimized, restore it
                if (IsIconic(hwnd))
                {
                    ShowWindow(hwnd, SW_RESTORE);
                }

                // Bring to foreground
                bool success = SetForegroundWindow(hwnd);
                
                if (success)
                {
                    Debug.WriteLine($"[WindowHost] ✅ Window focused successfully");
                }
                else
                {
                    Debug.WriteLine($"[WindowHost] ⚠️ SetForegroundWindow failed");
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowHost] Focus error: {ex.Message}");
                return false;
            }
        }
    }
}
