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
        public static bool IsVirtualLeftButtonDown { get; set; }

        private static object _coordsLock = new object();
        private static Point? _virtualScreenMousePosition;
        public static Point? VirtualScreenMousePosition
        {
            get { lock (_coordsLock) return _virtualScreenMousePosition; }
            set { lock (_coordsLock) _virtualScreenMousePosition = value; }
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

            // Empty JSON → traverse and finish without throwing
            if (string.IsNullOrWhiteSpace(macroNode.MacroDataJson))
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
                actions = JsonSerializer.Deserialize<List<MacroAction>>(macroNode.MacroDataJson, _jsonOptions);
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
                                if (!newBounds.IsEmpty && newBounds.Width > 0 && newBounds.Height > 0)
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
                            "#00000000",
                            0,
                            0,
                            0,
                            Models.BorderEffectType.None);
                            
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

            try
            {
                bool isLeftDown = false;
                bool isRightDown = false;
                IntPtr capturedHwnd = IntPtr.Zero;

                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    env.CancellationToken.ThrowIfCancellationRequested();

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
                        overlay?.UpdateProgress(cycle + 1, cycles, i + 1, actions.Count);

                        if (i > 0)
                        {
                            long delta = actions[i].Timestamp - actions[i - 1].Timestamp;
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

                                await SimulateVirtualMouseEventAsync(ax, ay, "MouseClick", action.Button, 0, overlayHwnd, capturedHwnd, isLeftDown, isRightDown);
                                
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
                                capturedHwnd = await SimulateVirtualMouseEventAsync(ax, ay, "MouseDown", action.Button, 0, overlayHwnd, capturedHwnd, isLeftDown, isRightDown);
                                break;
                            }
                            case "MouseUp":
                            {
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);

                                await SimulateVirtualMouseEventAsync(ax, ay, "MouseUp", action.Button, 0, overlayHwnd, capturedHwnd, isLeftDown, isRightDown);
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
                                await SimulateVirtualMouseEventAsync(ax, ay, "MouseMove", "", 0, overlayHwnd, capturedHwnd, isLeftDown, isRightDown);
                                break;
                            case "MouseScroll":
                            {
                                overlay?.ShowRightActionInfo("Scroll", $"Đang cuộn chuột {(action.ScrollDelta > 0 ? "lên" : "xuống")}");
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live) overlay?.DrawScroll(ax, ay, action.ScrollDelta, action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);

                                await SimulateVirtualMouseEventAsync(ax, ay, "MouseScroll", "", action.ScrollDelta, overlayHwnd, capturedHwnd, isLeftDown, isRightDown);
                                
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
                IsVirtualLeftButtonDown = false;
                VirtualScreenMousePosition = null;
                if (propHandler != null)
                {
                    macroNode.PropertyChanged -= propHandler;
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
            bool isRightDown)
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
            if (targetHwnd == IntPtr.Zero) return IntPtr.Zero;

            IntPtr topHwnd = GetAncestor(targetHwnd, GA_ROOT);
            if (topHwnd == IntPtr.Zero) topHwnd = targetHwnd;

            POINT clientPt = new POINT { X = ax, Y = ay };
            ScreenToClient(targetHwnd, ref clientPt);
            IntPtr lParam = FlowMy.Helpers.WindowHelper.MakeLParam(clientPt.X, clientPt.Y);

            bool isRight = buttonStr == "Right";

            if (actionType == "MouseMove")
            {
                int mkMv = 0;
                if (isLeftDown) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                if (isRightDown) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_RBUTTON;

                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, (IntPtr)mkMv, lParam);
            }
            else if (actionType == "MouseDown")
            {
                uint msg = isRight ? FlowMy.Helpers.WindowHelper.WM_RBUTTONDOWN : FlowMy.Helpers.WindowHelper.WM_LBUTTONDOWN;
                int wParam = isRight ? FlowMy.Helpers.WindowHelper.MK_RBUTTON : FlowMy.Helpers.WindowHelper.MK_LBUTTON;

                if (isLeftDown) wParam |= (int)FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                if (isRightDown) wParam |= (int)FlowMy.Helpers.WindowHelper.MK_RBUTTON;

                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, 0x0021 /*WM_MOUSEACTIVATE*/, topHwnd, (IntPtr)((msg << 16) | 1));
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, 0x0020 /*WM_SETCURSOR*/, targetHwnd, (IntPtr)((FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE << 16) | 1));

                int mkMv = 0;
                if (isLeftDown && isRight) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                if (isRightDown && !isRight) mkMv |= (int)FlowMy.Helpers.WindowHelper.MK_RBUTTON;

                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, (IntPtr)mkMv, lParam);
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, msg, (IntPtr)wParam, lParam);
            }
            else if (actionType == "MouseUp")
            {
                uint msg = isRight ? FlowMy.Helpers.WindowHelper.WM_RBUTTONUP : FlowMy.Helpers.WindowHelper.WM_LBUTTONUP;
                
                int wParam = 0;
                if (isLeftDown && isRight) wParam |= (int)FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                if (isRightDown && !isRight) wParam |= (int)FlowMy.Helpers.WindowHelper.MK_RBUTTON;

                int mkMv = 0;
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
                
                // Gửi thông điệp kích hoạt cho các ứng dụng yêu cầu MOUSEACTIVATE ở background
                FlowMy.Helpers.WindowHelper.SendMessage(targetHwnd, 0x0021 /*WM_MOUSEACTIVATE*/, topHwnd, (IntPtr)((0x0201 /*WM_LBUTTONDOWN*/ << 16) | 1 /*HTCLIENT*/));

                if (!isRight) IsVirtualLeftButtonDown = true;
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, IntPtr.Zero, lParam);
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, msgDown, (IntPtr)wParam, lParam);
                
                await Task.Delay(40); // Quan trọng: delay để ứng dụng kịp nhận MouseDown trước khi nhả chuột
                
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, msgUp, IntPtr.Zero, lParam);
                if (!isRight) IsVirtualLeftButtonDown = false;
            }
            else if (actionType == "MouseScroll")
            {
                int wParamScroll = (scrollDelta * 120) << 16;
                IntPtr lpScroll = FlowMy.Helpers.WindowHelper.MakeLParam(ax, ay); // WM_MOUSEWHEEL requires screen coords
                FlowMy.Helpers.WindowHelper.PostMessage(targetHwnd, FlowMy.Helpers.WindowHelper.WM_MOUSEWHEEL, (IntPtr)wParamScroll, lpScroll);
            }

            return targetHwnd;
        }

    }
}
