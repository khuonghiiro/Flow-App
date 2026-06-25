using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho ActionCanVasNode.
    /// Minimize FlowMy â†’ countdown â†’ lÆ°u target HWND â†’ phÃ¡t láº¡i vá»›i SetForegroundWindow trÆ°á»›c má»—i click.
    /// </summary>
    internal sealed class ActionCanVasNodeExecutor : INodeExecutor
    {
        private static readonly Dictionary<string, MacroPlaybackOverlay> _activePlaybackOverlays = new();

        public static bool IsVirtualLeftButtonDown { get; set; }
        public static bool IsPlaybackActive { get; set; }
        public static Microsoft.Web.WebView2.Wpf.WebView2? ActiveVirtualCdpWebView { get; set; }
        public static bool IsVirtualEventDispatching { get; set; }
        public static Point CanvasLayoutOffset { get; set; } = new Point(250, 60);

        // ─── Direct drag state — track internally, không phụ thuộc mouse capture ───
        private static FlowMy.Models.WorkflowNode? _directDragNode;
        private static Point _directDragOffset;

        // ─── HitElement cache — dùng khi HitTest fail (window deactivated) ───
        private static UIElement? _lastWpfHitElement;
        // ─── MacroNode đang execute — dùng để exclude khỏi direct manipulation ───
        private static FlowMy.Models.WorkflowNode? _executingMacroNode;
        public static FlowMy.Models.WorkflowNode? ExecutingMacroNode => _executingMacroNode;

        private static object _coordsLock = new object();
        private static Point? _virtualScreenMousePosition;
        public static Point? VirtualScreenMousePosition
        {
            get { lock (_coordsLock) return _virtualScreenMousePosition; }
            set { lock (_coordsLock) _virtualScreenMousePosition = value; }
        }

        private static Point? _virtualWindowMousePosition;
        public static Point? VirtualWindowMousePosition
        {
            get { lock (_coordsLock) return _virtualWindowMousePosition; }
            set { lock (_coordsLock) _virtualWindowMousePosition = value; }
        }

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private const uint GW_HWNDNEXT = 2;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        private const uint GA_ROOT = 2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref POINT lpPoints, uint cPoints);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_MOUSEWHEEL = 0x020A;
        private const uint MK_LBUTTON = 0x0001;
        private const uint MK_RBUTTON = 0x0002;

        private static IntPtr GetRealTargetHwnd(int x, int y, IntPtr overlayHwnd)
        {
            IntPtr hwnd = WindowFromPoint(new POINT { X = x, Y = y });
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;

            // Chỉ bỏ qua overlay của chúng ta khi chạy thao tác trên canvas.
            // Không bỏ qua process của FlowMy (myPid) để post message trực tiếp vào canvas của FlowMy.
            if (overlayHwnd != IntPtr.Zero && hwnd == overlayHwnd)
            {
                IntPtr current = hwnd;
                while (current != IntPtr.Zero)
                {
                    current = GetWindow(current, GW_HWNDNEXT);
                    if (current != IntPtr.Zero && IsWindowVisible(current))
                    {
                        GetWindowRect(current, out RECT rect);
                        if (x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom)
                        {
                            if (current != overlayHwnd)
                            {
                                hwnd = current;
                                break;
                            }
                        }
                    }
                }
            }

            if (hwnd != IntPtr.Zero)
            {
                var deepest = FlowMy.Helpers.WindowHelper.GetDeepestChildFromPoint(hwnd, x, y);
                if (deepest.Hwnd != IntPtr.Zero && deepest.Hwnd != hwnd)
                {
                    return deepest.Hwnd;
                }
            }

            return hwnd;
        }

        private static (int screenX, int screenY) ResolveScreenCoords(MacroAction action, Rect bounds)
        {
            if (!bounds.IsEmpty)
            {
                double rx = Math.Clamp(action.RelX, 0.0, 1.0);
                double ry = Math.Clamp(action.RelY, 0.0, 1.0);
                
                // If RelX/RelY are both 0, but X and Y are non-zero, it is relative offset format.
                if (action.RelX == 0 && action.RelY == 0 && (action.X != 0 || action.Y != 0))
                {
                    int cx = (int)Math.Clamp(bounds.Left + action.X, bounds.Left, bounds.Right);
                    int cy = (int)Math.Clamp(bounds.Top + action.Y, bounds.Top, bounds.Bottom);
                    return (cx, cy);
                }
                else
                {
                    int clientX = (int)(rx * bounds.Width);
                    int clientY = (int)(ry * bounds.Height);
                    return ((int)bounds.Left + clientX, (int)bounds.Top + clientY);
                }
            }
            return (action.X, action.Y);
        }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // â”€â”€â”€ SendInput keyboard helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [StructLayout(LayoutKind.Sequential)]
        private struct SENDINPUT_INPUT
        {
            public uint type;
            public SENDINPUT_UNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SENDINPUT_UNION
        {
            [FieldOffset(0)] public SENDINPUT_KI ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SENDINPUT_KI
        {
            public ushort wVk;
            public ushort wScan;
            public uint   dwFlags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD    = 1;
        private const uint KEYEVENTF_KEYUP   = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, SENDINPUT_INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        /// <summary>
        /// Gá»­i má»™t key (hoáº·c combo "Ctrl+C") qua SendInput â€” hoáº¡t Ä‘á»™ng vá»›i má»i app ká»ƒ cáº£ trÃ¬nh duyá»‡t.
        /// </summary>
        private static void SendKeyViaSendInput(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            // Combo: "Ctrl+C", "Shift+Alt+F4", v.v.
            if (key.Contains('+'))
            {
                var parts = key.Split('+');
                string mainKey = parts[^1];
                bool ctrl  = parts.Any(p => p.Equals("Ctrl",  StringComparison.OrdinalIgnoreCase));
                bool alt   = parts.Any(p => p.Equals("Alt",   StringComparison.OrdinalIgnoreCase));
                bool shift = parts.Any(p => p.Equals("Shift", StringComparison.OrdinalIgnoreCase));

                var inputs = new List<SENDINPUT_INPUT>();

                // Press modifiers
                if (ctrl)  inputs.Add(MakeKeyInput(0x11, false)); // VK_CONTROL
                if (alt)   inputs.Add(MakeKeyInput(0x12, false)); // VK_MENU
                if (shift) inputs.Add(MakeKeyInput(0x10, false)); // VK_SHIFT

                ushort mainVk = KeyNameToVk(mainKey);
                if (mainVk != 0)
                {
                    inputs.Add(MakeKeyInput(mainVk, false));
                    inputs.Add(MakeKeyInput(mainVk, true));
                }
                else if (mainKey.Length == 1)
                {
                    // Single char â€” use Unicode injection
                    inputs.Add(MakeUnicodeInput(mainKey[0], false));
                    inputs.Add(MakeUnicodeInput(mainKey[0], true));
                }

                // Release modifiers (reverse order)
                if (shift) inputs.Add(MakeKeyInput(0x10, true));
                if (alt)   inputs.Add(MakeKeyInput(0x12, true));
                if (ctrl)  inputs.Add(MakeKeyInput(0x11, true));

                if (inputs.Count > 0)
                    SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<SENDINPUT_INPUT>());
                return;
            }

            // Single key
            ushort vk = KeyNameToVk(key);
            if (vk != 0)
            {
                var inputs = new[]
                {
                    MakeKeyInput(vk, false),
                    MakeKeyInput(vk, true)
                };
                SendInput(2, inputs, Marshal.SizeOf<SENDINPUT_INPUT>());
            }
            else if (key.Length == 1)
            {
                // Single printable char â€” use VkKeyScan to get VK + shift state
                short vkScan = VkKeyScan(key[0]);
                if (vkScan != -1)
                {
                    ushort charVk    = (ushort)(vkScan & 0xFF);
                    bool   needShift = (vkScan & 0x100) != 0;
                    var inputs = new List<SENDINPUT_INPUT>();
                    if (needShift) inputs.Add(MakeKeyInput(0x10, false));
                    inputs.Add(MakeKeyInput(charVk, false));
                    inputs.Add(MakeKeyInput(charVk, true));
                    if (needShift) inputs.Add(MakeKeyInput(0x10, true));
                    SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<SENDINPUT_INPUT>());
                }
                else
                {
                    // Fallback: Unicode injection
                    var inputs = new[]
                    {
                        MakeUnicodeInput(key[0], false),
                        MakeUnicodeInput(key[0], true)
                    };
                    SendInput(2, inputs, Marshal.SizeOf<SENDINPUT_INPUT>());
                }
            }
        }

        private static SENDINPUT_INPUT MakeKeyInput(ushort vk, bool keyUp) => new SENDINPUT_INPUT
        {
            type = INPUT_KEYBOARD,
            u = new SENDINPUT_UNION
            {
                ki = new SENDINPUT_KI
                {
                    wVk    = vk,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
                }
            }
        };

        private static SENDINPUT_INPUT MakeUnicodeInput(char c, bool keyUp) => new SENDINPUT_INPUT
        {
            type = INPUT_KEYBOARD,
            u = new SENDINPUT_UNION
            {
                ki = new SENDINPUT_KI
                {
                    wVk    = 0,
                    wScan  = c,
                    dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0)
                }
            }
        };

        /// <summary>
        /// Map tên key (từ GetKeyName trong recorder) -> VK code.
        /// Bao gồm đầy đủ: phím số, chữ, F-keys, navigation, numpad, ký tự đặc biệt.
        /// </summary>
        private static ushort KeyNameToVk(string name) => name switch
        {
            // Navigation / editing
            "Backspace" => 0x08,
            "Tab"       => 0x09,
            "Enter"     => 0x0D,
            "Pause"     => 0x13,
            "CapsLock"  => 0x14,
            "Escape"    => 0x1B,
            "Space"     => 0x20,
            "PageUp"    => 0x21,
            "PageDown"  => 0x22,
            "End"       => 0x23,
            "Home"      => 0x24,
            "←"         => 0x25,
            "↑"         => 0x26,
            "→"         => 0x27,
            "↓"         => 0x28,
            "PrtSc"     => 0x2C,
            "Insert"    => 0x2D,
            "Delete"    => 0x2E,
            // Digits 0-9
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            // Letters A-Z (VK = uppercase ASCII)
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45,
            "F" => 0x46, "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A,
            "K" => 0x4B, "L" => 0x4C, "M" => 0x4D, "N" => 0x4E, "O" => 0x4F,
            "P" => 0x50, "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
            "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58, "Y" => 0x59,
            "Z" => 0x5A,
            // Numpad
            "Num0" => 0x60, "Num1" => 0x61, "Num2" => 0x62, "Num3" => 0x63,
            "Num4" => 0x64, "Num5" => 0x65, "Num6" => 0x66, "Num7" => 0x67,
            "Num8" => 0x68, "Num9" => 0x69,
            "Num*" => 0x6A, "Num+" => 0x6B, "Num-" => 0x6D, "Num." => 0x6E, "Num/" => 0x6F,
            // F-keys F1-F24
            "F1"  => 0x70, "F2"  => 0x71, "F3"  => 0x72, "F4"  => 0x73,
            "F5"  => 0x74, "F6"  => 0x75, "F7"  => 0x76, "F8"  => 0x77,
            "F9"  => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "F13" => 0x7C, "F14" => 0x7D, "F15" => 0x7E, "F16" => 0x7F,
            "F17" => 0x80, "F18" => 0x81, "F19" => 0x82, "F20" => 0x83,
            "F21" => 0x84, "F22" => 0x85, "F23" => 0x86, "F24" => 0x87,
            // Lock keys
            "NumLock"    => 0x90,
            "ScrollLock" => 0x91,
            // OEM punctuation (US layout)
            ";" => 0xBA, "=" => 0xBB, "," => 0xBC, "-" => 0xBD,
            "." => 0xBE, "/" => 0xBF, "`" => 0xC0,
            "[" => 0xDB, "\\" => 0xDC, "]" => 0xDD, "'" => 0xDE,
            _   => 0
        };

        private async Task<Rect> GetNodeBoundsAsync(ActionCanVasNode macroNode)
        {
            Rect bounds = Rect.Empty;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    if (macroNode.Border != null)
                    {
                        var pt = macroNode.Border.PointToScreen(new Point(0, 0));
                        var ptBottomRight = macroNode.Border.PointToScreen(new Point(macroNode.Border.ActualWidth, macroNode.Border.ActualHeight));
                        bounds = new Rect(pt.X, pt.Y, ptBottomRight.X - pt.X, ptBottomRight.Y - pt.Y);
                    }
                }, System.Windows.Threading.DispatcherPriority.Normal);
            }
            return bounds;
        }

        public bool CanExecute(WorkflowNode node) => node is ActionCanVasNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var macroNode = (ActionCanVasNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 1. Lấy danh sách thao tác (multi-action)
            var actionItems = macroNode.GetMacroActionItems();
            MacroActionItem? selectedItem = null;

            // 2. Tìm thao tác phù hợp dựa trên incoming connection (tái sử dụng flow)
            var incomingFromNode = env.IncomingConnection?.FromNode;
            if (incomingFromNode != null && macroNode.ReuseRoutes != null && macroNode.ReuseRoutes.Count > 0)
            {
                var route = macroNode.ReuseRoutes
                    .FirstOrDefault(r => string.Equals(r.IncomingNodeId, incomingFromNode.Id, StringComparison.OrdinalIgnoreCase));
                if (route != null && !string.IsNullOrWhiteSpace(route.MacroActionId))
                {
                    selectedItem = actionItems.FirstOrDefault(a => string.Equals(a.Id, route.MacroActionId, StringComparison.OrdinalIgnoreCase));
                }
            }

            // 3. Fallback: Nếu không match route hoặc không tìm thấy, dùng default macro action
            if (selectedItem == null && !string.IsNullOrWhiteSpace(macroNode.DefaultMacroActionId))
            {
                selectedItem = actionItems.FirstOrDefault(a => string.Equals(a.Id, macroNode.DefaultMacroActionId, StringComparison.OrdinalIgnoreCase));
            }

            // 4. Fallback: Dùng item đầu tiên trong danh sách
            if (selectedItem == null && actionItems.Count > 0)
            {
                selectedItem = actionItems[0];
            }

            // 5. Lấy macro JSON
            string macroJson = selectedItem?.MacroDataJson ?? macroNode.MacroDataJson;

            // Empty JSON → traverse and finish without throwing
            if (string.IsNullOrWhiteSpace(macroJson))
            {
                sw.Stop();
                env.OnNodeCompleted?.Invoke(macroNode, sw.Elapsed);
                await env.TraverseOutputsAsync(node);
                return;
            }

            // Parse JSON
            List<MacroAction>? actions;
            try
            {
                actions = JsonSerializer.Deserialize<List<MacroAction>>(macroJson, _jsonOptions);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi đọc dữ liệu Macro JSON: {ex.Message}");
            }
            if (actions == null || actions.Count == 0)
            {
                sw.Stop();
                env.OnNodeCompleted?.Invoke(macroNode, sw.Elapsed);
                await env.TraverseOutputsAsync(node);
                return;
            }

            // ─── Lấy bounds của ActionCanvas trên màn hình ───
            Rect bounds = await GetNodeBoundsAsync(macroNode);

            if (bounds.IsEmpty || bounds.Width == 0 || bounds.Height == 0)
            {
                throw new Exception("Không thể xác định vị trí của ActionCanvas trên màn hình.");
            }

            int cycles = macroNode.PlaybackMode == MacroPlaybackMode.Once ? 1 : macroNode.RepeatCount;
            var visualMode = macroNode.VisualPlaybackMode;

            object boundsLock = new object();
            Rect sharedBounds = bounds;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            Window? editorWindow = null;
            IntPtr editorHwnd = IntPtr.Zero;
            POINT lastValidClientOrigin = new POINT { X = 0, Y = 0 };

            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    editorWindow = Window.GetWindow(macroNode.Border);
                    if (editorWindow == null) editorWindow = Application.Current?.MainWindow;
                    if (editorWindow != null)
                    {
                        editorHwnd = new System.Windows.Interop.WindowInteropHelper(editorWindow).Handle;
                        ClientToScreen(editorHwnd, ref lastValidClientOrigin);

                        if (editorWindow is FlowMy.Services.Interaction.IWorkflowEditorHost directHost)
                        {
                            try
                            {
                                var tx = directHost.TranslateTransform?.X ?? 0;
                                var ty = directHost.TranslateTransform?.Y ?? 0;
                                var origin = directHost.WorkflowCanvas.TranslatePoint(new Point(0, 0), editorWindow);
                                CanvasLayoutOffset = new Point(origin.X - tx, origin.Y - ty);
                            }
                            catch { }
                        }
                    }
                }, DispatcherPriority.Normal);
            }

            MacroPlaybackOverlay? overlay = null;
            IntPtr overlayHwnd = IntPtr.Zero;
            if (visualMode != VisualPlaybackMode.Silent && dispatcher != null)
            {
                Task? loadedTask = null;
                dispatcher.Invoke(() =>
                {
                    try
                    {
                        overlay = new MacroPlaybackOverlay();
                        if (editorWindow != null)
                        {
                            overlay.Owner = editorWindow;
                            overlay.SetOwnerWindow(editorWindow);
                        }
                        overlay.PrepareForTargetMode();
                        overlay.PositionOverBounds(bounds);
                        loadedTask = overlay.WhenLoaded;

                        bool isClosed = false;
                        var borderRef = macroNode.Border;
                        bool isRepositioning = false;
                        EventHandler? layoutUpdatedHandler = null;
                        layoutUpdatedHandler = (s, e) =>
                        {
                            if (isClosed || borderRef == null || isRepositioning) return;
                            try
                            {
                                isRepositioning = true;
                                var pt = borderRef.PointToScreen(new Point(0, 0));
                                var ptBottomRight = borderRef.PointToScreen(new Point(borderRef.ActualWidth, borderRef.ActualHeight));
                                var newBounds = new Rect(pt.X, pt.Y, ptBottomRight.X - pt.X, ptBottomRight.Y - pt.Y);
                                if (!newBounds.IsEmpty && newBounds.Width > 0 && newBounds.Height > 0 && pt.X > -10000 && pt.Y > -10000)
                                {
                                    bool changed = false;
                                    lock (boundsLock)
                                    {
                                        if (Math.Abs(newBounds.Left - sharedBounds.Left) > 0.1 ||
                                            Math.Abs(newBounds.Top - sharedBounds.Top) > 0.1 ||
                                            Math.Abs(newBounds.Width - sharedBounds.Width) > 0.1 ||
                                            Math.Abs(newBounds.Height - sharedBounds.Height) > 0.1)
                                        {
                                            sharedBounds = newBounds;
                                            changed = true;
                                        }
                                    }
                                    if (changed)
                                    {
                                        overlay.PositionOverBounds(newBounds);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MacroExecutor] LayoutUpdated reposition failed: {ex}");
                            }
                            finally
                            {
                                isRepositioning = false;
                            }
                        };

                        if (borderRef != null)
                        {
                            borderRef.LayoutUpdated += layoutUpdatedHandler;
                        }

                        overlay.Closed += (s, e) =>
                        {
                            isClosed = true;
                            if (borderRef != null && layoutUpdatedHandler != null)
                            {
                                try
                                {
                                    borderRef.LayoutUpdated -= layoutUpdatedHandler;
                                }
                                catch { }
                            }
                        };

                        overlay.Show();
                        overlayHwnd = new System.Windows.Interop.WindowInteropHelper(overlay).Handle;
                        lock (_activePlaybackOverlays)
                        {
                            _activePlaybackOverlays[macroNode.Id] = overlay;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MacroExecutor] overlay.Show failed: {ex}");
                        overlay = null;
                        loadedTask = null;
                    }
                }, DispatcherPriority.Normal);

                if (loadedTask != null)
                {
                    try { await loadedTask.WaitAsync(TimeSpan.FromSeconds(3)); }
                    catch { /* timeout */ }

                    await dispatcher.InvokeAsync(() =>
                    {
                        Rect currentBounds;
                        lock (boundsLock)
                        {
                            currentBounds = sharedBounds;
                        }
                        overlay?.PositionOverBounds(currentBounds);
                        overlay?.ConfigureBorder(
                            macroNode.PlaybackBorderColorHex,
                            macroNode.PlaybackBorderThickness,
                            macroNode.PlaybackGradientSize,
                            macroNode.PlaybackOpacity,
                            macroNode.PlaybackEffectType);
                            
                        overlay?.SetCompactMode(true);
                        overlay?.SetInfoVisibility(macroNode.ShowPlaybackInfo);
                        FlowMy.Views.NodeControls.ActionCanVasNodeControl.StartPlaybackEffect(macroNode);
                    }, DispatcherPriority.Normal);
                }
            }

            // Đảm bảo cửa sổ chứa editor đang hiển thị
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    var win = Window.GetWindow(macroNode.Border);
                    if (win == null) win = Application.Current?.MainWindow;
                    if (win != null)
                    {
                        if (win.WindowState == WindowState.Minimized) win.WindowState = WindowState.Normal;
                        win.Activate();
                    }
                }, DispatcherPriority.Normal);
            }

            System.ComponentModel.PropertyChangedEventHandler? propHandler = null;
            if (overlay != null)
            {
                propHandler = (s, e) =>
                {
                    if (e.PropertyName == nameof(ActionCanVasNode.ShowPlaybackInfo))
                    {
                        overlay.SetInfoVisibility(macroNode.ShowPlaybackInfo);
                    }
                };
                macroNode.PropertyChanged += propHandler;
            }

            _executingMacroNode = macroNode;
            IsPlaybackActive = true;
            if (dispatcher != null && editorWindow != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    if (editorWindow is FlowMy.Services.Interaction.IWorkflowEditorHost host)
                    {
                        host.DraggedNode = null;
                    }
                }, DispatcherPriority.Normal);
            }

            try
            {
                bool isLeftDown = false;
                bool isRightDown = false;
                IntPtr capturedHwnd = IntPtr.Zero;

                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    env.CancellationToken.ThrowIfCancellationRequested();
                    if (!IsPlaybackActive) throw new OperationCanceledException("Playback stopped.");

                    if (cycle > 0)
                    {
                        overlay?.ClearVisuals();
                        if (macroNode.RepeatIntervalMs > 0)
                            await Task.Delay(macroNode.RepeatIntervalMs, env.CancellationToken);
                    }

                    if (visualMode == VisualPlaybackMode.Ghost && overlay != null)
                    {
                        await dispatcher!.InvokeAsync(() => { overlay.PreDrawGhostMarkers(actions); }, DispatcherPriority.Normal);
                    }

                    for (int i = 0; i < actions.Count; i++)
                    {
                        env.CancellationToken.ThrowIfCancellationRequested();
                        if (!IsPlaybackActive) throw new OperationCanceledException("Playback stopped.");
                        overlay?.UpdateProgress(cycle + 1, cycles, i + 1, actions.Count);

                        // ─── Skip logic — bỏ qua hoàn toàn action nếu checkbox được check ───
                        if (selectedItem != null)
                        {
                            string skipActType = actions[i].Type;
                            if (skipActType == "MouseMove" && selectedItem.SkipMouseMove) continue;
                            if (skipActType == "KeyPress" && selectedItem.SkipKeyPress) continue;
                            if ((skipActType == "MouseClick" || skipActType == "MouseDown" || skipActType == "MouseUp") && selectedItem.SkipMouseClick) continue;
                            if (skipActType == "MouseScroll" && selectedItem.SkipMouseScroll) continue;
                        }

                        if (i > 0)
                        {
                            long delta = actions[i].Timestamp - actions[i - 1].Timestamp;

                            // Apply speed override based on action type
                            if (selectedItem != null)
                            {
                                int overrideDelay = -1;
                                string actType = actions[i].Type;
                                if (actType == "MouseMove") overrideDelay = selectedItem.MouseMoveDelayMs;
                                else if (actType == "KeyPress") overrideDelay = selectedItem.KeyPressDelayMs;
                                else if (actType == "MouseClick" || actType == "MouseDown" || actType == "MouseUp") overrideDelay = selectedItem.MouseClickDelayMs;
                                else if (actType == "MouseScroll") overrideDelay = selectedItem.MouseScrollDelayMs;

                                if (overrideDelay >= 0)
                                {
                                    delta = overrideDelay;
                                }
                            }

                            if (delta > 0) await Task.Delay((int)Math.Min(delta, int.MaxValue), env.CancellationToken);
                        }

                        var action = actions[i];
                        
                        // ─── Cập nhật bounds liên tục để bám theo node nếu canvas bị di chuyển ───
                        Rect currentBounds;
                        lock (boundsLock)
                        {
                            currentBounds = sharedBounds;
                        }
                        bounds = currentBounds;

                        if (editorHwnd != IntPtr.Zero && !IsIconic(editorHwnd))
                        {
                            POINT currentOrigin = new POINT { X = 0, Y = 0 };
                            ClientToScreen(editorHwnd, ref currentOrigin);
                            if (currentOrigin.X > -10000 && currentOrigin.Y > -10000)
                            {
                                lastValidClientOrigin = currentOrigin;
                            }

                            if (editorWindow is FlowMy.Services.Interaction.IWorkflowEditorHost directHost)
                            {
                                try
                                {
                                    var tx = directHost.TranslateTransform?.X ?? 0;
                                    var ty = directHost.TranslateTransform?.Y ?? 0;
                                    var origin = directHost.WorkflowCanvas.TranslatePoint(new Point(0, 0), editorWindow);
                                    CanvasLayoutOffset = new Point(origin.X - tx, origin.Y - ty);
                                }
                                catch { }
                            }
                        }

                        var (ax, ay) = ResolveScreenCoords(action, bounds);

                        overlay?.UpdateHeldModifiers(action.ShiftHeld, action.CtrlHeld, action.AltHeld);

                        switch (action.Type)
                        {
                            case "MouseClick":
                            {
                                string hint = BuildClickHint(action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                string desc = GetActionDescription("MouseClick", action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                overlay?.ShowRightActionInfo(hint, desc);
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live) overlay?.DrawClick(ax, ay, action.Button == "Right", action.SequenceNumber, hint);
                                else if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);

                                await SimulateVirtualMouseEventAsync(ax, ay, "MouseClick", action.Button, 0, overlayHwnd, capturedHwnd, isLeftDown, isRightDown, editorWindow, editorHwnd, lastValidClientOrigin, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                
                                await Task.Delay(50);
                                overlay?.ShowRightActionInfo(null, null);
                                break;
                            }
                            case "MouseDown":
                            {
                                string hint = BuildClickHint(action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                string desc = GetActionDescription("MouseDown", action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                overlay?.ShowRightActionInfo(hint, desc);
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live) overlay?.DrawClick(ax, ay, action.Button == "Right", action.SequenceNumber, hint);
                                else if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);

                                if (action.Button == "Right") isRightDown = true;
                                else
                                {
                                    isLeftDown = true;
                                    IsVirtualLeftButtonDown = true;
                                }
                                capturedHwnd = await SimulateVirtualMouseEventAsync(ax, ay, "MouseDown", action.Button, 0, overlayHwnd, capturedHwnd, isLeftDown, isRightDown, editorWindow, editorHwnd, lastValidClientOrigin, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                break;
                            }
                            case "MouseUp":
                            {
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);

                                await SimulateVirtualMouseEventAsync(ax, ay, "MouseUp", action.Button, 0, overlayHwnd, capturedHwnd, isLeftDown, isRightDown, editorWindow, editorHwnd, lastValidClientOrigin, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                capturedHwnd = IntPtr.Zero;
                                if (action.Button == "Right") isRightDown = false;
                                else
                                {
                                    isLeftDown = false;
                                    IsVirtualLeftButtonDown = false;
                                }
                                
                                await Task.Delay(50);
                                overlay?.ShowRightActionInfo(null, null);
                                break;
                            }
                            case "KeyPress":
                                if (!string.IsNullOrWhiteSpace(action.Key))
                                {
                                    overlay?.ShowRightActionInfo(action.Key, $"Đang nhấn phím {action.Key}");
                                    if (visualMode == VisualPlaybackMode.Live) overlay?.DrawKeyPress(ax, ay, action.Key, action.SequenceNumber);
                                    else if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);

                                    await Task.Delay(30);
                                    if (action.Key.Contains('+')) env.Service.KeyboardInput.SendHotkeyPress(action.Key, 1, 0);
                                    else env.Service.KeyboardInput.SendKeyPress(action.Key, 1, 0);
                                    
                                    await Task.Delay(50);
                                    overlay?.ShowRightActionInfo(null, null);
                                }
                                break;
                            case "MouseMove":
                                // Do not move the real mouse to allow background processing
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: false);
                                if (visualMode != VisualPlaybackMode.Silent) overlay?.AddTrailPoint(ax, ay);
                                if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);
                                await SimulateVirtualMouseEventAsync(ax, ay, "MouseMove", "", 0, overlayHwnd, capturedHwnd, isLeftDown, isRightDown, editorWindow, editorHwnd, lastValidClientOrigin, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                break;
                            case "MouseScroll":
                            {
                                overlay?.ShowRightActionInfo("Scroll", $"Đang cuộn chuột {(action.ScrollDelta > 0 ? "lên" : "xuống")}");
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live) overlay?.DrawScroll(ax, ay, action.ScrollDelta, action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);

                                await SimulateVirtualMouseEventAsync(ax, ay, "MouseScroll", "", action.ScrollDelta, overlayHwnd, capturedHwnd, isLeftDown, isRightDown, editorWindow, editorHwnd, lastValidClientOrigin, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                
                                await Task.Delay(50);
                                overlay?.ShowRightActionInfo(null, null);
                                break;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(macroNode, ex.Message);
                throw;
            }
            finally
            {
                IsPlaybackActive = false;
                IsVirtualLeftButtonDown = false;
                ActiveVirtualCdpWebView = null;
                VirtualScreenMousePosition = null;
                VirtualWindowMousePosition = null;
                _directDragNode = null;
                _lastWpfHitElement = null;
                _executingMacroNode = null;
                if (propHandler != null)
                {
                    macroNode.PropertyChanged -= propHandler;
                }

                if (overlay != null)
                {
                    lock (_activePlaybackOverlays)
                    {
                        _activePlaybackOverlays.Remove(macroNode.Id);
                    }
                }

                if (dispatcher != null)
                {
                    dispatcher.Invoke(() =>
                    {
                        FlowMy.Views.NodeControls.ActionCanVasNodeControl.StopPlaybackEffect(macroNode);
                    }, DispatcherPriority.Normal);
                }

                env.Service.MouseInput.ReleaseAllModifiers();
                env.Service.KeyboardInput.ReleaseAllModifiers();
                if (overlay != null)
                {
                    try { await Task.Delay(800); } catch { }
                }

                if (overlay != null && dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        try { overlay.Close(); } catch { }
                    }, DispatcherPriority.Normal);
                }
            }

            // Publish output
            if (!string.IsNullOrWhiteSpace(macroNode.OutputKey) && !string.IsNullOrWhiteSpace(env.ExecutionId))
            {
                env.Service.SetScopedNodeStringOutput(
                    env.ExecutionId, macroNode.Id,
                    macroNode.OutputKey.Trim(), macroNode.MacroDataJson);
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(macroNode, sw.Elapsed);
            await env.TraverseOutputsAsync(node);
        }

        public static void CleanupAll()
        {
            IsPlaybackActive = false;
            _executingMacroNode = null;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            lock (_activePlaybackOverlays)
            {
                foreach (var overlay in _activePlaybackOverlays.Values)
                {
                    dispatcher.Invoke(() =>
                    {
                        try
                        {
                            overlay.Close();
                        }
                        catch { }
                    });
                }
                _activePlaybackOverlays.Clear();
            }
        }

        // â”€â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Tạo label hiển thị cho tooltip: "Chuột trái", "Chuột phải", "Ctrl + Chuột trái", v.v.
        /// </summary>
        private static string BuildClickHint(string? button, bool shift, bool ctrl, bool alt)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (ctrl)  parts.Add("Ctrl");
            if (alt)   parts.Add("Alt");
            if (shift) parts.Add("Shift");
            parts.Add(button == "Right" ? "Chuột phải" : "Chuột trái");
            return string.Join(" + ", parts);
        }

        /// <summary>
        /// Tạo mô tả hành động cho floating tooltip.
        /// </summary>
        private static string GetActionDescription(string actionType, string? button, bool shift, bool ctrl, bool alt)
        {
            string modifiers = "";
            var modParts = new System.Collections.Generic.List<string>();
            if (ctrl)  modParts.Add("Ctrl");
            if (alt)   modParts.Add("Alt");
            if (shift) modParts.Add("Shift");
            if (modParts.Count > 0)
                modifiers = string.Join("+", modParts) + " + ";

            string buttonName = button == "Right" ? "chuột phải" : "chuột trái";

            return actionType switch
            {
                "MouseClick" => $"Đang nhấn {modifiers}{buttonName}",
                "MouseDown"  => $"Đang giữ {modifiers}{buttonName}",
                _            => $"Đang thao tác {modifiers}{buttonName}"
            };
        }
        
        private async Task<IntPtr> SimulateVirtualMouseEventAsync(
            int ax, int ay, 
            string actionType, 
            string buttonStr, 
            int scrollDelta, 
            IntPtr overlayHwnd, 
            IntPtr capturedHwnd,
            bool isLeftDown,
            bool isRightDown,
            Window? editorWindow,
            IntPtr editorHwnd,
            POINT lastValidClientOrigin,
            bool shiftHeld = false,
            bool ctrlHeld = false,
            bool altHeld = false)
        {
            VirtualScreenMousePosition = new Point(ax, ay);
            IntPtr targetHwnd = capturedHwnd;
            if (targetHwnd != IntPtr.Zero && !IsWindow(targetHwnd))
            {
                targetHwnd = IntPtr.Zero;
            }
            if (targetHwnd == IntPtr.Zero)
            {
                targetHwnd = GetRealTargetHwnd(ax, ay, overlayHwnd);
            }

            // Constrain targetHwnd to editorHwnd or its children
            if (editorHwnd != IntPtr.Zero)
            {
                bool isSelfOrChildOfEditor = (targetHwnd == editorHwnd) || (GetAncestor(targetHwnd, GA_ROOT) == editorHwnd);
                if (!isSelfOrChildOfEditor)
                {
                    if (!IsIconic(editorHwnd))
                    {
                        var deepest = FlowMy.Helpers.WindowHelper.GetDeepestChildFromPoint(editorHwnd, ax, ay);
                        targetHwnd = deepest.Hwnd != IntPtr.Zero ? deepest.Hwnd : editorHwnd;
                    }
                    else
                    {
                        targetHwnd = editorHwnd;
                    }
                }
            }

            if (targetHwnd == IntPtr.Zero) targetHwnd = editorHwnd;
            if (targetHwnd == IntPtr.Zero) return IntPtr.Zero;

            IntPtr topHwnd = GetAncestor(targetHwnd, GA_ROOT);
            if (topHwnd == IntPtr.Zero) topHwnd = targetHwnd;

            bool isOffScreen = false;
            if (editorHwnd != IntPtr.Zero)
            {
                POINT origin = new POINT { X = 0, Y = 0 };
                ClientToScreen(editorHwnd, ref origin);
                if (origin.X <= -10000 || origin.Y <= -10000)
                {
                    isOffScreen = true;
                }
            }

            POINT clientPt;
            if (editorHwnd != IntPtr.Zero && (IsIconic(editorHwnd) || isOffScreen))
            {
                clientPt = new POINT { X = ax - lastValidClientOrigin.X, Y = ay - lastValidClientOrigin.Y };
                if (targetHwnd != editorHwnd)
                {
                    MapWindowPoints(editorHwnd, targetHwnd, ref clientPt, 1);
                }
            }
            else
            {
                clientPt = new POINT { X = ax, Y = ay };
                ScreenToClient(targetHwnd, ref clientPt);
            }
            IntPtr lParam = FlowMy.Helpers.WindowHelper.MakeLParam(clientPt.X, clientPt.Y);

            bool handledByCdp = false;

            // Direct WPF Event Dispatching
            if (editorWindow != null)
            {
                var dispatcher = editorWindow.Dispatcher;
                if (dispatcher != null)
                {
                    Func<Task> uiTask = async () =>
                    {
                        try
                        {
                            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(editorWindow);
                            double dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                            double dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                            POINT editorPt;
                            if (isOffScreen || IsIconic(editorHwnd))
                            {
                                editorPt = new POINT { X = ax - lastValidClientOrigin.X, Y = ay - lastValidClientOrigin.Y };
                            }
                            else
                            {
                                editorPt = new POINT { X = ax, Y = ay };
                                ScreenToClient(editorHwnd, ref editorPt);
                            }

                            var wpfPoint = new Point(editorPt.X, editorPt.Y);
                            var hitTestPoint = new Point(editorPt.X / dpiScaleX, editorPt.Y / dpiScaleY);
                            VirtualWindowMousePosition = wpfPoint;
                            UIElement? hitElement = null;
                            bool hitTestSucceeded = false;
                            try
                            {
                                System.Windows.Media.VisualTreeHelper.HitTest(editorWindow,
                                    null,
                                    new System.Windows.Media.HitTestResultCallback(result =>
                                    {
                                        if (result.VisualHit is UIElement elem && elem.IsVisible)
                                        {
                                            hitElement = elem;
                                            return System.Windows.Media.HitTestResultBehavior.Stop;
                                        }
                                        return System.Windows.Media.HitTestResultBehavior.Continue;
                                    }),
                                    new System.Windows.Media.PointHitTestParameters(hitTestPoint));
                                hitTestSucceeded = hitElement != null;
                            }
                            catch (Exception htEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MacroExecutor] HitTest EXCEPTION: {htEx.Message}");
                            }

                            // Cache hitElement: khi HitTest fail (window mất focus),
                            // dùng element cache từ lần hit thành công trước.
                            if (hitElement != null)
                                _lastWpfHitElement = hitElement;
                            else if (_lastWpfHitElement != null)
                                hitElement = _lastWpfHitElement;

                            bool isWindowActive = editorWindow.IsActive;
                            System.Diagnostics.Debug.WriteLine($"[MacroExecutor] {actionType} wpfPt=({wpfPoint.X:F0},{wpfPoint.Y:F0}) hitTest={hitTestSucceeded} cached={hitElement == _lastWpfHitElement && !hitTestSucceeded} hitElem={hitElement?.GetType().Name ?? "NULL"} windowActive={isWindowActive}");

                            Microsoft.Web.WebView2.Wpf.WebView2? webView = ActiveVirtualCdpWebView;
                            bool isNativeHost = webView != null;

                            if (webView == null)
                            {
                                if (hitElement != null)
                                {
                                    System.Windows.DependencyObject current = hitElement;
                                    while (current != null)
                                    {
                                        if (current is Microsoft.Web.WebView2.Wpf.WebView2 wv2)
                                        {
                                            webView = wv2;
                                            isNativeHost = true;
                                            break;
                                        }
                                        if (current is System.Windows.Interop.HwndHost || current.GetType().Name.Contains("WebView2"))
                                        {
                                            isNativeHost = true;
                                            if (current is Microsoft.Web.WebView2.Wpf.WebView2 v) webView = v;
                                        }
                                        current = System.Windows.Media.VisualTreeHelper.GetParent(current) ?? (current as System.Windows.FrameworkElement)?.Parent;
                                    }
                                }

                                // Fallback target matching: Nếu cửa sổ không hoạt động hoặc hitTest trả về null,
                                // dùng DPI-scaled coordinates để tìm WebView2 node chứa toạ độ chuột simulated.
                                if (webView == null && editorWindow is FlowMy.Services.Interaction.IWorkflowEditorHost directHost)
                                {
                                    var vm = directHost.ViewModel;
                                    if (vm != null)
                                    {
                                        var scale = directHost.ScaleTransform?.ScaleX ?? 1.0;
                                        var txVal = directHost.TranslateTransform?.X ?? 0;
                                        var tyVal = directHost.TranslateTransform?.Y ?? 0;


                                        double logicalClientX = wpfPoint.X / dpiScaleX;
                                        double logicalClientY = wpfPoint.Y / dpiScaleY;

                                        double canvasX = (logicalClientX - CanvasLayoutOffset.X - txVal) / scale;
                                        double canvasY = (logicalClientY - CanvasLayoutOffset.Y - tyVal) / scale;

                                        foreach (var n in vm.Nodes)
                                        {
                                            if (n.Border == null) continue;
                                            if (n == _executingMacroNode) continue;
                                            if (n.Type == FlowMy.Models.NodeType.ActionCanVas) continue;

                                            if (n.Type == FlowMy.Models.NodeType.Web || 
                                                n.Type == FlowMy.Models.NodeType.HtmlUi ||
                                                n.Type == FlowMy.Models.NodeType.EmbedApplication)
                                            {
                                                double nw = n.Border.ActualWidth > 0 ? n.Border.ActualWidth : 600;
                                                double nh = n.Border.ActualHeight > 0 ? n.Border.ActualHeight : 600;
                                                if (canvasX >= n.X && canvasX <= n.X + nw &&
                                                    canvasY >= n.Y && canvasY <= n.Y + nh)
                                                {
                                                    var wvControl = FindWebView2InVisualTree(n.Border);
                                                    if (wvControl != null)
                                                    {
                                                        webView = wvControl;
                                                        EnsureWebViewActiveInBackground(webView);
                                                        isNativeHost = true;
                                                        System.Diagnostics.Debug.WriteLine($"[MacroExecutor] ActiveVirtualCdpWebView fallback match node: {n.Title ?? n.Id} at ({canvasX:F0},{canvasY:F0})");
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (webView != null && webView.CoreWebView2 != null)
                            {
                                handledByCdp = true;
                                try 
                                {
                                    Point wvPoint;
                                    // Robust manual calculation to ensure 100% identical coordinates
                                    // between active and background/minimized states.
                                    if (editorWindow is FlowMy.Services.Interaction.IWorkflowEditorHost directHost)
                                    {
                                        var scale = directHost.ScaleTransform?.ScaleX ?? 1.0;
                                        var txVal = directHost.TranslateTransform?.X ?? 0;
                                        var tyVal = directHost.TranslateTransform?.Y ?? 0;
                                        
                                        FlowMy.Models.WorkflowNode? hostNode = null;
                                        var vm = directHost.ViewModel;
                                        if (vm != null)
                                        {
                                            System.Windows.DependencyObject current = webView;
                                            while (current != null)
                                            {
                                                if (current is System.Windows.Controls.Border borderVal)
                                                {
                                                    var match = vm.Nodes.FirstOrDefault(n => n.Border == borderVal);
                                                    if (match != null)
                                                    {
                                                        hostNode = match;
                                                        break;
                                                    }
                                                }
                                                current = System.Windows.Media.VisualTreeHelper.GetParent(current) ?? (current as System.Windows.FrameworkElement)?.Parent;
                                            }
                                        }
                                        
                                        if (hostNode != null)
                                        {
                                            double webViewOffsetX = 0;
                                            double webViewOffsetY = 0;
                                            try
                                            {
                                                var zeroInWebView = webView.TransformToAncestor(hostNode.Border).Transform(new Point(0, 0));
                                                webViewOffsetX = zeroInWebView.X;
                                                webViewOffsetY = zeroInWebView.Y;
                                            }
                                            catch
                                            {
                                                webViewOffsetX = 0;
                                                if (hostNode.Type == FlowMy.Models.NodeType.EmbedApplication)
                                                    webViewOffsetY = 32;
                                                else
                                                    webViewOffsetY = 38;
                                            }
                                            
                                            double webViewOffsetLogicalX = CanvasLayoutOffset.X + txVal + scale * (hostNode.X + webViewOffsetX);
                                            double webViewOffsetLogicalY = CanvasLayoutOffset.Y + tyVal + scale * (hostNode.Y + webViewOffsetY);
                                            wvPoint = new Point(wpfPoint.X - webViewOffsetLogicalX, wpfPoint.Y - webViewOffsetLogicalY);
                                        }
                                        else
                                        {
                                            wvPoint = wpfPoint;
                                        }
                                    }
                                    else
                                    {
                                        wvPoint = wpfPoint;
                                    }
                                    string cdpType = "";
                                    string cdpButton = "none";
                                    int cdpButtons = 0;
                                    int clickCount = 0;

                                    if (isLeftDown) cdpButtons |= 1;
                                    if (isRightDown) cdpButtons |= 2;
                                    
                                    if (actionType == "MouseMove") {
                                        cdpType = "mouseMoved";
                                        cdpButton = "none";
                                        clickCount = 0;
                                    } else if (actionType == "MouseDown") {
                                        cdpType = "mousePressed";
                                        cdpButton = buttonStr.ToLower();
                                        clickCount = 1;
                                        if (cdpButton == "left") cdpButtons |= 1;
                                        if (cdpButton == "right") cdpButtons |= 2;
                                    } else if (actionType == "MouseUp") {
                                        cdpType = "mouseReleased";
                                        cdpButton = buttonStr.ToLower();
                                        clickCount = 1;
                                        if (cdpButton == "left") cdpButtons &= ~1;
                                        if (cdpButton == "right") cdpButtons &= ~2;
                                    }

                                    int modifiers = 0;
                                    if (shiftHeld) modifiers |= 8;
                                    if (ctrlHeld) modifiers |= 2;
                                    if (altHeld) modifiers |= 1;

                                    if (actionType == "MouseDown" && webView != null)
                                    {
                                        ActiveVirtualCdpWebView = webView;
                                    }

                                    if (actionType == "MouseDown" || actionType == "MouseClick")
                                    {
                                        // Gửi mouseMoved trước để đồng bộ hover/focus
                                        var hoverPayload = new { type = "mouseMoved", x = wvPoint.X, y = wvPoint.Y, button = "none", buttons = 0, clickCount = 0, modifiers = modifiers };
                                        await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", System.Text.Json.JsonSerializer.Serialize(hoverPayload));
                                        await Task.Delay(15);
                                    }

                                    if (actionType == "MouseClick")
                                    {
                                        var btn = buttonStr.ToLower();
                                        int downButtons = btn == "left" ? 1 : 2;
                                        var downPayload = new { type = "mousePressed", x = wvPoint.X, y = wvPoint.Y, button = btn, buttons = downButtons, clickCount = 1, modifiers = modifiers };
                                        await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", System.Text.Json.JsonSerializer.Serialize(downPayload));
                                        
                                        await Task.Delay(40);
                                        
                                        var upPayload = new { type = "mouseReleased", x = wvPoint.X, y = wvPoint.Y, button = btn, buttons = 0, clickCount = 1, modifiers = modifiers };
                                        await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", System.Text.Json.JsonSerializer.Serialize(upPayload));
                                        
                                        ActiveVirtualCdpWebView = null;
                                    }
                                    else if (actionType == "MouseScroll")
                                    {
                                        var scrollPayload = new { type = "mouseWheel", x = wvPoint.X, y = wvPoint.Y, deltaX = 0, deltaY = -scrollDelta * 120, modifiers = modifiers };
                                        await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", System.Text.Json.JsonSerializer.Serialize(scrollPayload));
                                    }
                                    else
                                    {
                                        var payload = new { type = cdpType, x = wvPoint.X, y = wvPoint.Y, button = cdpButton, buttons = cdpButtons, clickCount = clickCount, modifiers = modifiers };
                                        await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", System.Text.Json.JsonSerializer.Serialize(payload));
                                        
                                        if (actionType == "MouseUp")
                                        {
                                            ActiveVirtualCdpWebView = null;
                                        }
                                    }

                                    // Always bubble up MouseUp to WPF to clear any stuck DragDropHandler state
                                    if (actionType == "MouseUp" || actionType == "MouseClick")
                                    {
                                        try {
                                            var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, buttonStr == "Right" ? System.Windows.Input.MouseButton.Right : System.Windows.Input.MouseButton.Left)
                                            {
                                                RoutedEvent = UIElement.MouseUpEvent,
                                                Source = hitElement ?? editorWindow
                                            };
                                            (hitElement ?? editorWindow).RaiseEvent(args);
                                        } catch { }
                                    }
                                }
                                catch (Exception cdpEx) { System.Diagnostics.Debug.WriteLine($"CDP Error: {cdpEx}"); }
                            }
                            else if (hitElement != null && !isNativeHost)
                            {
                                var time = Environment.TickCount;

                                // ─── Strategy 1: Direct Node Manipulation ───
                                // Bypass hoàn toàn WPF event system cho drag operations.
                                // RaiseEvent dùng Mouse.PrimaryDevice → e.GetPosition() trả vị trí chuột thật (sai).
                                // hitElement thường là canvas Rectangle → event bubble không đến node Border.
                                bool directHandled = false;
                                if (actionType == "MouseDown" || actionType == "MouseMove" || actionType == "MouseUp")
                                {
                                    if (editorWindow is FlowMy.Services.Interaction.IWorkflowEditorHost directHost)
                                    {
                                        var vm = directHost.ViewModel;
                                        if (vm != null)
                                        {
                                            var scale = directHost.ScaleTransform?.ScaleX ?? 1.0;
                                            var txVal = directHost.TranslateTransform?.X ?? 0;
                                            var tyVal = directHost.TranslateTransform?.Y ?? 0;
                                            double logicalClientX = wpfPoint.X / dpiScaleX;
                                            double logicalClientY = wpfPoint.Y / dpiScaleY;
                                            double canvasX = (logicalClientX - CanvasLayoutOffset.X - txVal) / scale;
                                            double canvasY = (logicalClientY - CanvasLayoutOffset.Y - tyVal) / scale;

                                            if (actionType == "MouseDown" && buttonStr != "Right")
                                            {
                                                // Tìm node tại vị trí (exclude macroNode)
                                                FlowMy.Models.WorkflowNode? foundNode = null;
                                                foreach (var n in vm.Nodes)
                                                {
                                                    if (n.Border == null) continue;
                                                    if (n == _executingMacroNode) continue;
                                                    // Skip ActionCanVas nodes — KHÔNG BAO GIỜ drag ActionCanVas 
                                                    // trong lúc playback (nó chính là node đang execute)
                                                    if (n.Type == FlowMy.Models.NodeType.ActionCanVas) continue;
                                                    // Skip nodes chứa WebView2 — interactions trên WebView2 
                                                    // phải đi qua CDP/PostMessage, không phải drag node
                                                    if (n.Type == FlowMy.Models.NodeType.Web || 
                                                        n.Type == FlowMy.Models.NodeType.HtmlUi ||
                                                        n.Type == FlowMy.Models.NodeType.EmbedApplication) continue;
                                                    double nw = n.Border.ActualWidth > 0 ? n.Border.ActualWidth : 150;
                                                    double nh = n.Border.ActualHeight > 0 ? n.Border.ActualHeight : 80;
                                                    if (canvasX >= n.X && canvasX <= n.X + nw &&
                                                        canvasY >= n.Y && canvasY <= n.Y + nh)
                                                    {
                                                        foundNode = n;
                                                    }
                                                }
                                                if (foundNode != null)
                                                {
                                                    _directDragNode = foundNode;
                                                    _directDragOffset = new Point(canvasX - foundNode.X, canvasY - foundNode.Y);
                                                    vm.SelectedNode = foundNode;
                                                    try { directHost.ZIndexManager.SelectNode(foundNode); } catch { }
                                                    try { directHost.ZIndexManager.DragNode(foundNode); } catch { }
                                                    directHandled = true;
                                                    System.Diagnostics.Debug.WriteLine($"[DirectManip] MouseDown on node: {foundNode.Title ?? foundNode.Id} at ({canvasX:F0},{canvasY:F0})");
                                                }
                                            }
                                            else if (actionType == "MouseMove" && _directDragNode != null)
                                            {
                                                double newX = Math.Round(canvasX - _directDragOffset.X);
                                                double newY = Math.Round(canvasY - _directDragOffset.Y);
                                                directHost.UpdateNodePosition(_directDragNode, newX, newY);
                                                if (_directDragNode.Border != null)
                                                {
                                                    System.Windows.Controls.Canvas.SetLeft(_directDragNode.Border, newX);
                                                    System.Windows.Controls.Canvas.SetTop(_directDragNode.Border, newY);
                                                }
                                                if (_directDragNode.Ports != null)
                                                {
                                                    foreach (var pos in _directDragNode.Ports
                                                        .Where(p => p.IsVisible)
                                                        .Select(p => p.Position)
                                                        .Distinct())
                                                    {
                                                        directHost.UpdatePortsPositionOnSide(_directDragNode, pos);
                                                    }
                                                }
                                                foreach (var conn in vm.Connections
                                                    .Where(c => c.FromNode == _directDragNode || c.ToNode == _directDragNode)
                                                    .ToList())
                                                {
                                                    directHost.UpdateConnectionPath(conn);
                                                }
                                                directHandled = true;
                                            }
                                            else if (actionType == "MouseUp" && _directDragNode != null)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[DirectManip] MouseUp, releasing node: {_directDragNode.Title ?? _directDragNode.Id}");
                                                _directDragNode = null;
                                                try { directHost.UpdateMinimap(); } catch { }
                                                try { directHost.UpdateCanvasSize(); } catch { }
                                                directHandled = true;
                                            }
                                        }
                                    }
                                }

                                // ─── Strategy 2: RaiseEvent fallback cho non-drag operations ───
                                if (!directHandled)
                                {
                                    IsVirtualEventDispatching = true;
                                    try
                                    {
                                        if (actionType == "MouseMove")
                                        {
                                            var args = new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, time)
                                            {
                                                RoutedEvent = UIElement.MouseMoveEvent,
                                                Source = hitElement
                                            };
                                            hitElement.RaiseEvent(args);
                                        }
                                        else if (actionType == "MouseDown")
                                        {
                                            var button = buttonStr == "Right" ? System.Windows.Input.MouseButton.Right : System.Windows.Input.MouseButton.Left;
                                            var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, time, button)
                                            {
                                                RoutedEvent = UIElement.MouseDownEvent,
                                                Source = hitElement
                                            };
                                            hitElement.RaiseEvent(args);
                                        }
                                        else if (actionType == "MouseUp")
                                        {
                                            var button = buttonStr == "Right" ? System.Windows.Input.MouseButton.Right : System.Windows.Input.MouseButton.Left;
                                            var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, time, button)
                                            {
                                                RoutedEvent = UIElement.MouseUpEvent,
                                                Source = hitElement
                                            };
                                            hitElement.RaiseEvent(args);
                                        }
                                        else if (actionType == "MouseClick")
                                        {
                                            var button = buttonStr == "Right" ? System.Windows.Input.MouseButton.Right : System.Windows.Input.MouseButton.Left;
                                            var downArgs = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, time, button)
                                            {
                                                RoutedEvent = UIElement.MouseDownEvent,
                                                Source = hitElement
                                            };
                                            hitElement.RaiseEvent(downArgs);

                                            var upArgs = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, time + 10, button)
                                            {
                                                RoutedEvent = UIElement.MouseUpEvent,
                                                Source = hitElement
                                            };
                                            hitElement.RaiseEvent(upArgs);
                                        }
                                        else if (actionType == "MouseScroll")
                                        {
                                            var args = new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, time, scrollDelta * 120)
                                            {
                                                RoutedEvent = UIElement.MouseWheelEvent,
                                                Source = hitElement
                                            };
                                            hitElement.RaiseEvent(args);
                                        }
                                    }
                                    finally { IsVirtualEventDispatching = false; }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MacroExecutor] WPF direct event dispatch failed: {ex}");
                        }
                    };
                    
                    await dispatcher.InvokeAsync(uiTask, System.Windows.Threading.DispatcherPriority.Normal).Task.Unwrap();
                }
            }

            bool isRight = buttonStr == "Right";

            if (actionType == "MouseDown" && !isRight) IsVirtualLeftButtonDown = true;
            if ((actionType == "MouseUp" || actionType == "MouseClick") && !isRight) IsVirtualLeftButtonDown = false;

            if (handledByCdp) return targetHwnd;

            if (actionType == "MouseMove")
            {
                int mkMv = 0x4000;
                if (isLeftDown) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                if (isRightDown) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_RBUTTON;

                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, (IntPtr)mkMv, lParam);
            }
            else if (actionType == "MouseDown")
            {
                uint msg = isRight ? FlowMy.Helpers.WindowHelper.WM_RBUTTONDOWN : FlowMy.Helpers.WindowHelper.WM_LBUTTONDOWN;
                int wParam = isRight ? FlowMy.Helpers.WindowHelper.MK_RBUTTON : FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                wParam |= 0x4000;

                if (!isRight) IsVirtualLeftButtonDown = true;

                if (isLeftDown) wParam |= (int)FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                if (isRightDown) wParam |= (int)FlowMy.Helpers.WindowHelper.MK_RBUTTON;

                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, 0x0021 /*WM_MOUSEACTIVATE*/, topHwnd, (IntPtr)((msg << 16) | 1));
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, 0x0020 /*WM_SETCURSOR*/, targetHwnd, (IntPtr)((FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE << 16) | 1));

                int mkMv = 0x4000;
                if (isLeftDown && isRight) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                if (isRightDown && !isRight) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_RBUTTON;

                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, (IntPtr)mkMv, lParam);
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, msg, (IntPtr)wParam, lParam);
            }
            else if (actionType == "MouseUp")
            {
                uint msg = isRight ? FlowMy.Helpers.WindowHelper.WM_RBUTTONUP : FlowMy.Helpers.WindowHelper.WM_LBUTTONUP;
                
                int wParam = 0x4000;
                
                if (!isRight) IsVirtualLeftButtonDown = false;
                if (isLeftDown && isRight) wParam |= (int)FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                if (isRightDown && !isRight) wParam |= (int)FlowMy.Helpers.WindowHelper.MK_RBUTTON;

                int mkMv = 0x4000;
                if (isLeftDown) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                if (isRightDown) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_RBUTTON;

                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, (IntPtr)mkMv, lParam);
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, msg, (IntPtr)wParam, lParam);
            }
            else if (actionType == "MouseClick")
            {
                uint msgDown = isRight ? FlowMy.Helpers.WindowHelper.WM_RBUTTONDOWN : FlowMy.Helpers.WindowHelper.WM_LBUTTONDOWN;
                uint msgUp = isRight ? FlowMy.Helpers.WindowHelper.WM_RBUTTONUP : FlowMy.Helpers.WindowHelper.WM_LBUTTONUP;
                int wParam = isRight ? FlowMy.Helpers.WindowHelper.MK_RBUTTON : FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                wParam |= 0x4000;
                
                // Gửi thông điệp kích hoạt cho các ứng dụng yêu cầu MOUSEACTIVATE ở background
                FlowMy.Helpers.WindowHelper.SendMessage(targetHwnd, 0x0021 /*WM_MOUSEACTIVATE*/, topHwnd, (IntPtr)((0x0201 /*WM_LBUTTONDOWN*/ << 16) | 1 /*HTCLIENT*/));

                if (!isRight) IsVirtualLeftButtonDown = true;
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, (IntPtr)0x4000, lParam);
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, msgDown, (IntPtr)wParam, lParam);
                
                await Task.Delay(40); // Quan trọng: delay để ứng dụng kịp nhận MouseDown trước khi nhả chuột
                
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, msgUp, (IntPtr)0x4000, lParam);
                if (!isRight) IsVirtualLeftButtonDown = false;
            }
            else if (actionType == "MouseScroll")
            {
                int wParamScroll = ((scrollDelta * 120) << 16) | 0x4000;
                IntPtr lpScroll = FlowMy.Helpers.WindowHelper.MakeLParam(ax, ay); // WM_MOUSEWHEEL requires screen coords
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, FlowMy.Helpers.WindowHelper.WM_MOUSEWHEEL, (IntPtr)wParamScroll, lpScroll);
            }

            return targetHwnd;
        }

        // ═══════════════════════════════════════════════════════════════
        // DIRECT NODE MANIPULATION — bypass WPF mouse events hoàn toàn
        // Hoạt động khi app mất focus vì:
        //   1. Dùng DispatcherPriority.Normal (không bị deprioritize)
        //   2. Tính canvas coords từ ScaleTransform/TranslateTransform
        //      (không cần TranslatePoint/HitTest)
        //   3. Tự track drag state (không cần IsMouseCaptured)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Thử thao tác trực tiếp node qua code.
        /// Return true nếu đã xử lý (skip SimulateVirtualMouseEventAsync).
        /// </summary>
        private static async Task<bool> TryDirectNodeManipulationAsync(
            Window editorWindow,
            Point wpfPoint,
            string actionType,
            string buttonStr,
            Dispatcher dispatcher)
        {
            if (!(editorWindow is FlowMy.Services.Interaction.IWorkflowEditorHost host))
                return false;

            bool handled = false;

            await dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var vm = host.ViewModel;
                    if (vm == null) return;
                    // ── Tính canvas coords thủ công (không cần TranslatePoint) ──
                    var scale = host.ScaleTransform?.ScaleX ?? 1.0;
                    var tx = host.TranslateTransform?.X ?? 0;
                    var ty = host.TranslateTransform?.Y ?? 0;
                    double canvasX = (wpfPoint.X - tx) / scale;
                    double canvasY = (wpfPoint.Y - ty) / scale;

                    if (actionType == "MouseDown" && buttonStr != "Right")
                    {
                        // ── Tìm node tại vị trí bằng position matching ──
                        FlowMy.Models.WorkflowNode? foundNode = null;
                        foreach (var node in vm.Nodes)
                        {
                            if (node.Border == null) continue;
                            // Exclude macroNode đang execute
                            if (node == _executingMacroNode) continue;
                            double nw = node.Border.ActualWidth > 0 ? node.Border.ActualWidth : 150;
                            double nh = node.Border.ActualHeight > 0 ? node.Border.ActualHeight : 80;
                            if (canvasX >= node.X && canvasX <= node.X + nw &&
                                canvasY >= node.Y && canvasY <= node.Y + nh)
                            {
                                foundNode = node;
                                // Không break — node sau có thể overlap và có ZIndex cao hơn
                            }
                        }

                        if (foundNode != null)
                        {
                            _directDragNode = foundNode;
                            _directDragOffset = new Point(canvasX - foundNode.X, canvasY - foundNode.Y);
                            vm.SelectedNode = foundNode;
                            try { host.ZIndexManager.SelectNode(foundNode); } catch { }
                            handled = true;
                        }
                    }
                    else if (actionType == "MouseMove" && _directDragNode != null)
                    {
                        // ── Direct position update ──
                        double newX = Math.Round(canvasX - _directDragOffset.X);
                        double newY = Math.Round(canvasY - _directDragOffset.Y);

                        host.UpdateNodePosition(_directDragNode, newX, newY);

                        if (_directDragNode.Border != null)
                        {
                            System.Windows.Controls.Canvas.SetLeft(_directDragNode.Border, newX);
                            System.Windows.Controls.Canvas.SetTop(_directDragNode.Border, newY);
                        }

                        // Update ports
                        if (_directDragNode.Ports != null)
                        {
                            foreach (var pos in _directDragNode.Ports
                                .Where(p => p.IsVisible)
                                .Select(p => p.Position)
                                .Distinct())
                            {
                                host.UpdatePortsPositionOnSide(_directDragNode, pos);
                            }
                        }

                        // Update connections
                        foreach (var conn in vm.Connections
                            .Where(c => c.FromNode == _directDragNode || c.ToNode == _directDragNode)
                            .ToList())
                        {
                            host.UpdateConnectionPath(conn);
                        }

                        handled = true;
                    }
                    else if (actionType == "MouseUp" && _directDragNode != null)
                    {
                        // ── Direct drag end ──
                        _directDragNode = null;
                        try
                        {
                            host.UpdateMinimap();
                            host.UpdateCanvasSize();
                        }
                        catch { }
                        handled = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DirectManip] Error: {ex.Message}");
                }
            }, DispatcherPriority.Normal);

            return handled;
        }

        /// <summary>
        /// Tìm WorkflowNode từ UIElement bằng cách walk up visual tree.
        /// </summary>
        private static FlowMy.Models.WorkflowNode? FindNodeFromElement(
            UIElement? element,
            FlowMy.ViewModels.WorkflowEditorViewModel viewModel)
        {
            if (element == null) return null;
            try
            {
                System.Windows.DependencyObject? current = element;
                while (current != null)
                {
                    if (current is System.Windows.Controls.Border border &&
                        border.Tag is FlowMy.Models.WorkflowNode node)
                    {
                        if (viewModel.Nodes.Contains(node))
                            return node;
                    }
                    current = System.Windows.Media.VisualTreeHelper.GetParent(current)
                              ?? (current as FrameworkElement)?.Parent;
                }
            }
            catch { }
            return null;
        }

        private static Microsoft.Web.WebView2.Wpf.WebView2? FindWebView2InVisualTree(System.Windows.DependencyObject parent)
        {
            if (parent == null) return null;
            if (parent is Microsoft.Web.WebView2.Wpf.WebView2 wv2)
            {
                return wv2;
            }
            int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                var result = FindWebView2InVisualTree(child);
                if (result != null) return result;
            }
            return null;
        }

        private static void EnsureWebViewActiveInBackground(Microsoft.Web.WebView2.Wpf.WebView2 webView)
        {
            try
            {
                if (webView.CoreWebView2 != null)
                {
                    var isSuspendedProp = webView.CoreWebView2.GetType().GetProperty("IsSuspended");
                    if (isSuspendedProp != null)
                    {
                        bool isSuspended = (bool)isSuspendedProp.GetValue(webView.CoreWebView2);
                        if (isSuspended)
                        {
                            var resumeMethod = webView.CoreWebView2.GetType().GetMethod("Resume");
                            resumeMethod?.Invoke(webView.CoreWebView2, null);
                            System.Diagnostics.Debug.WriteLine("[MacroExecutor] CoreWebView2 was suspended. Called Resume().");
                        }
                    }
                }

                var type = typeof(Microsoft.Web.WebView2.Wpf.WebView2);
                var field = type.GetField("_coreWebView2Controller", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            ?? type.GetField("_controller", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var controller = field.GetValue(webView);
                    if (controller != null)
                    {
                        var isVisibleProp = controller.GetType().GetProperty("IsVisible");
                        if (isVisibleProp != null && isVisibleProp.CanWrite)
                        {
                            bool currentVal = (bool)isVisibleProp.GetValue(controller);
                            if (!currentVal)
                            {
                                isVisibleProp.SetValue(controller, true);
                                System.Diagnostics.Debug.WriteLine("[MacroExecutor] Forced CoreWebView2Controller.IsVisible = true in background");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MacroExecutor] Failed to force WebView2 active: {ex.Message}");
            }
        }
    }
}

