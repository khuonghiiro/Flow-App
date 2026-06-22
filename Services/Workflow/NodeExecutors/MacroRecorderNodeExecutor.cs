using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views.Overlays;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho MacroRecorderNode.
    /// Minimize FlowMy → countdown → lưu target HWND → phát lại với SetForegroundWindow trước mỗi click.
    /// </summary>
    internal sealed class MacroRecorderNodeExecutor : INodeExecutor
    {
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        /// <summary>
        /// Chuyển đổi tọa độ action về screen coords thực tế tại thời điểm playback.
        /// - Nếu action có RelX/RelY (TargetApp mode): scale theo client rect hiện tại của target window.
        /// - Nếu không: dùng X/Y gốc (Free mode).
        /// </summary>
        private static (int screenX, int screenY) ResolveScreenCoords(MacroAction action, IntPtr targetHwnd, bool isTargetApp)
        {
            if (isTargetApp && targetHwnd != IntPtr.Zero
                && (action.RelX > 0 || action.RelY > 0)
                && GetClientRect(targetHwnd, out RECT cr)
                && cr.Right > 0 && cr.Bottom > 0)
            {
                // Scale relative coords to current client size
                int clientX = (int)(action.RelX * cr.Right);
                int clientY = (int)(action.RelY * cr.Bottom);

                // Convert client → screen
                var pt = new POINT { X = clientX, Y = clientY };
                ClientToScreen(targetHwnd, ref pt);
                return (pt.X, pt.Y);
            }

            return (action.X, action.Y);
        }

        /// <summary>
        /// Đưa targetHwnd lên foreground một cách đáng tin cậy bằng AttachThreadInput.
        /// SetForegroundWindow thông thường bị Windows chặn từ background thread.
        /// </summary>
        private static void ForceForeground(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero || !IsWindow(targetHwnd)) return;

            var fgHwnd = GetForegroundWindow();
            if (fgHwnd == targetHwnd) return; // đã là foreground

            uint fgThread  = GetWindowThreadProcessId(fgHwnd, out _);
            uint myThread  = GetCurrentThreadId();
            uint tgtThread = GetWindowThreadProcessId(targetHwnd, out _);

            // Attach current thread và target thread vào foreground thread
            bool attached1 = fgThread != myThread  && AttachThreadInput(myThread,  fgThread, true);
            bool attached2 = fgThread != tgtThread && AttachThreadInput(tgtThread, fgThread, true);

            SetForegroundWindow(targetHwnd);

            if (attached1) AttachThreadInput(myThread,  fgThread, false);
            if (attached2) AttachThreadInput(tgtThread, fgThread, false);
        }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // ─── SendInput keyboard helpers ───────────────────────────────────────────

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
        /// Gửi một key (hoặc combo "Ctrl+C") qua SendInput — hoạt động với mọi app kể cả trình duyệt.
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
                    // Single char — use Unicode injection
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
                // Single printable char — use VkKeyScan to get VK + shift state
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
        /// Map tên key (từ GetKeyName trong recorder) → VK code.
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
            // Digits 0–9
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            // Letters A–Z (VK = uppercase ASCII)
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
            // F-keys F1–F24
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

        public bool CanExecute(WorkflowNode node) => node is MacroRecorderNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var macroNode = (MacroRecorderNode)node;
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
            catch (JsonException ex)
            {
                env.OnNodeFailed?.Invoke(macroNode, ex.Message);
                throw;
            }

            if (actions == null || actions.Count == 0)
            {
                sw.Stop();
                env.OnNodeCompleted?.Invoke(macroNode, sw.Elapsed);
                await env.TraverseOutputsAsync(node);
                return;
            }

            int cycles = macroNode.PlaybackMode == MacroPlaybackMode.Once ? 1 : macroNode.RepeatCount;
            var visualMode = macroNode.VisualPlaybackMode;

            // ── Show playback overlay on UI thread (only for Live / Ghost modes) ──
            MacroPlaybackOverlay? overlay = null;
            var dispatcher = Application.Current?.Dispatcher;
            if (visualMode != VisualPlaybackMode.Silent && dispatcher != null)
            {
                Task? loadedTask = null;
                dispatcher.Invoke(() =>
                {
                    try
                    {
                        overlay = new MacroPlaybackOverlay();
                        // TargetApp: tell overlay NOT to go fullscreen in Loaded
                        if (macroNode.ExecutionMode == MacroExecutionMode.TargetApp)
                            overlay.PrepareForTargetMode();
                        loadedTask = overlay.WhenLoaded;
                        overlay.Show();
                        overlay.Activate();
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
                    catch { /* timeout — proceed anyway */ }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[MacroExecutor] visualMode={visualMode}, overlay={overlay?.GetType().Name ?? "null"}, actions={actions.Count}");

            // ── Minimize FlowMy + countdown + lưu target HWND ────────────────────
            var mainWindow = dispatcher != null
                ? await dispatcher.InvokeAsync(() => Application.Current?.MainWindow)
                : null;

            // Minimize main window trước
            if (dispatcher != null)
            {
                dispatcher.Invoke(() =>
                {
                    if (mainWindow != null)
                        mainWindow.WindowState = WindowState.Minimized;
                });
            }

            // Countdown (overlay vẫn hiển thị để user thấy)
            if (macroNode.CountdownSeconds > 0 && overlay != null)
            {
                await overlay.ShowCountdownAsync(macroNode.CountdownSeconds, mainWindow: null); // đã minimize rồi
            }
            else
            {
                await Task.Delay(300); // đủ để Windows xử lý minimize
            }

            // Lưu HWND
            IntPtr targetHwnd = IntPtr.Zero;
            bool isTargetApp = macroNode.ExecutionMode == MacroExecutionMode.TargetApp;
            if (isTargetApp)
            {
                var windows = FlowMy.Helpers.WindowHelper.GetActiveWindows();

                // Ưu tiên match chính xác (title + process) — dùng khi tab không đổi
                var match = windows.FirstOrDefault(w =>
                    w.ProcessName == macroNode.TargetProcessName &&
                    w.Title == macroNode.TargetWindowTitle);

                // Fallback: match chỉ theo ProcessName — xử lý trường hợp tab trình duyệt đổi
                // hoặc title cửa sổ thay đổi sau khi ghi
                if (match == null)
                    match = windows.FirstOrDefault(w => w.ProcessName == macroNode.TargetProcessName);

                if (match != null)
                {
                    targetHwnd = match.Handle;
                    System.Diagnostics.Debug.WriteLine($"[MacroExecutor] Resolved TargetApp HWND=0x{targetHwnd:X} ({match.ProcessName} | {match.Title})");
                }
                else
                {
                    throw new Exception($"Không tìm thấy ứng dụng đích '{macroNode.TargetWindowTitle}' ({macroNode.TargetProcessName})");
                }
            }
            else
            {
                targetHwnd = GetForegroundWindow();
                System.Diagnostics.Debug.WriteLine($"[MacroExecutor] Free Mode targetHwnd=0x{targetHwnd:X}");
            }

            // ── TargetApp mode: bring target to front first, then position overlay ─
            if (isTargetApp && targetHwnd != IntPtr.Zero)
            {
                // Bring target app to foreground BEFORE measuring its rect for the overlay.
                // This ensures the window is restored (not minimized) and at its real size.
                ForceForeground(targetHwnd);
                await Task.Delay(150); // let Windows finish restoring/resizing the window
            }

            if (isTargetApp && overlay != null && targetHwnd != IntPtr.Zero && dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    // alwaysVisible:true → overlay luôn hiển thị trong suốt quá trình phát lại
                    // (không ẩn khi app đích mất focus, chỉ theo dõi vị trí cửa sổ)
                    overlay.PositionOverTargetAfterLoad(targetHwnd, alwaysVisible: true);
                }, DispatcherPriority.Normal);
                System.Diagnostics.Debug.WriteLine($"[MacroExecutor] Overlay positioned over target HWND=0x{targetHwnd:X}");
            }

            try
            {
                // Track mouse button states for correct WM_MOUSEMOVE params
                bool isLeftDown = false;
                bool isRightDown = false;

                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    env.CancellationToken.ThrowIfCancellationRequested();

                    if (cycle > 0)
                    {
                        overlay?.ClearVisuals();
                        if (macroNode.RepeatIntervalMs > 0)
                            await Task.Delay(macroNode.RepeatIntervalMs, env.CancellationToken);
                    }

                    // Ghost mode: pre-draw all markers before starting execution
                    if (visualMode == VisualPlaybackMode.Ghost && overlay != null)
                    {
                        await dispatcher!.InvokeAsync(() =>
                        {
                            overlay.PreDrawGhostMarkers(actions);
                        }, DispatcherPriority.Normal);
                    }

                    for (int i = 0; i < actions.Count; i++)
                    {
                        env.CancellationToken.ThrowIfCancellationRequested();

                        overlay?.UpdateProgress(cycle + 1, cycles, i + 1, actions.Count);

                        // Delay by delta timestamp (skip first action)
                        if (i > 0)
                        {
                            long delta = actions[i].Timestamp - actions[i - 1].Timestamp;
                            if (delta > 0)
                            {
                                int delayMs = (int)Math.Min(delta, int.MaxValue);
                                await Task.Delay(delayMs, env.CancellationToken);
                            }
                        }

                        var action = actions[i];
                        
                        // Resolve actual screen coords (handles TargetApp resize scaling)
                        var (ax, ay) = ResolveScreenCoords(action, targetHwnd, isTargetApp);

                        // Đảm bảo target app luôn là foreground trước mỗi action (bỏ qua nếu background mode).
                        // SendInput chỉ hoạt động khi target là foreground window, nhưng PostMessage không cần.
                        // Chỉ gọi khi cần (tránh gọi liên tục làm chậm).
                        if (!macroNode.UseBackgroundMode && targetHwnd != IntPtr.Zero && IsWindow(targetHwnd)
                            && GetForegroundWindow() != targetHwnd)
                        {
                            ForceForeground(targetHwnd);
                            await Task.Delay(40, env.CancellationToken);
                        }

                        // Cập nhật trạng thái hiển thị các phím Modifier đang giữ trên Overlay
                        overlay?.UpdateHeldModifiers(action.ShiftHeld, action.CtrlHeld, action.AltHeld);

                        switch (action.Type)
                        {
                            case "MouseClick":
                            {
                                string hint = BuildClickHint(action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                string desc = GetActionDescription("MouseClick", action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                
                                overlay?.ShowRightActionInfo(hint, desc);
                                
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawClick(ax, ay, action.Button == "Right", action.SequenceNumber, hint);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);
                                
                                if (isTargetApp)
                                {
                                    // PostMessage thuần túy: không chiếm chuột thật, không kích hoạt app
                                    var (childClick, cxClick, cyClick) = FlowMy.Helpers.WindowHelper.GetDeepestChildFromPoint(targetHwnd, ax, ay);                                    IntPtr lpClick = FlowMy.Helpers.WindowHelper.MakeLParam(cxClick, cyClick);
                                    uint btnDown = action.Button == "Right" ? FlowMy.Helpers.WindowHelper.WM_RBUTTONDOWN : FlowMy.Helpers.WindowHelper.WM_LBUTTONDOWN;
                                    uint btnUp   = action.Button == "Right" ? FlowMy.Helpers.WindowHelper.WM_RBUTTONUP   : FlowMy.Helpers.WindowHelper.WM_LBUTTONUP;
                                    int mkClick = 0;
                                    if (action.Button == "Right") mkClick |= FlowMy.Helpers.WindowHelper.MK_RBUTTON;
                                    else mkClick |= FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                                    // WM_MOUSEMOVE trước để target biết vị trí cursor
                                    FlowMy.Helpers.WindowHelper.PostMessage(childClick, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, IntPtr.Zero, lpClick);
                                    FlowMy.Helpers.WindowHelper.PostMessage(childClick, btnDown, (IntPtr)mkClick, lpClick);
                                    FlowMy.Helpers.WindowHelper.PostMessage(childClick, btnUp, IntPtr.Zero, lpClick);
                                }
                                else
                                {
                                    var btn = action.Button == "Right" ? MouseButton.Right : MouseButton.Left;
                                    env.Service.MouseInput.SendMouseClickAt(
                                        action.X, action.Y, btn,
                                        shiftHeld: action.ShiftHeld,
                                        ctrlHeld:  action.CtrlHeld,
                                        altHeld:   action.AltHeld);
                                }
                                
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
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawClick(ax, ay, action.Button == "Right", action.SequenceNumber, hint);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                if (isTargetApp)
                                {
                                    // PostMessage thuần túy — không SilentActivate
                                    var (childHwnd2, clientX2, clientY2) = FlowMy.Helpers.WindowHelper.GetDeepestChildFromPoint(targetHwnd, ax, ay);
                                    IntPtr lParam2 = FlowMy.Helpers.WindowHelper.MakeLParam(clientX2, clientY2);
                                    uint msgDown2 = action.Button == "Right" ? FlowMy.Helpers.WindowHelper.WM_RBUTTONDOWN : FlowMy.Helpers.WindowHelper.WM_LBUTTONDOWN;
                                    int mk2 = 0;
                                    if (isLeftDown) mk2 |= FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                                    if (isRightDown) mk2 |= FlowMy.Helpers.WindowHelper.MK_RBUTTON;
                                    FlowMy.Helpers.WindowHelper.PostMessage(childHwnd2, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, (IntPtr)mk2, lParam2);
                                    FlowMy.Helpers.WindowHelper.PostMessage(childHwnd2, msgDown2, (IntPtr)mk2, lParam2);
                                }
                                else
                                {
                                    var btnDown = action.Button == "Right" ? MouseButton.Right : MouseButton.Left;
                                    env.Service.MouseInput.SendMouseDownAt(
                                        ax, ay, btnDown,
                                        shiftHeld: action.ShiftHeld,
                                        ctrlHeld:  action.CtrlHeld,
                                        altHeld:   action.AltHeld);
                                }
                                // Không ẩn panel — giữ hiển thị cho đến khi MouseUp
                                break;
                            }

                            case "MouseUp":
                            {
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                
                                if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                if (isTargetApp)
                                {
                                    // PostMessage thuần túy — không RaiseNoActivate, không SilentActivate
                                    var (childHwndUp, clientXUp, clientYUp) = FlowMy.Helpers.WindowHelper.GetDeepestChildFromPoint(targetHwnd, ax, ay);
                                    IntPtr lParamUp = FlowMy.Helpers.WindowHelper.MakeLParam(clientXUp, clientYUp);
                                    uint msgUpMsg = action.Button == "Right" ? FlowMy.Helpers.WindowHelper.WM_RBUTTONUP : FlowMy.Helpers.WindowHelper.WM_LBUTTONUP;
                                    int mkUp = 0;
                                    if (isLeftDown) mkUp |= FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                                    if (isRightDown) mkUp |= FlowMy.Helpers.WindowHelper.MK_RBUTTON;
                                    FlowMy.Helpers.WindowHelper.PostMessage(childHwndUp, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, (IntPtr)mkUp, lParamUp);
                                    FlowMy.Helpers.WindowHelper.PostMessage(childHwndUp, msgUpMsg, IntPtr.Zero, lParamUp);
                                }
                                else
                                {
                                    var btnUp = action.Button == "Right" ? MouseButton.Right : MouseButton.Left;
                                    env.Service.MouseInput.SendMouseUpAt(
                                        ax, ay, btnUp,
                                        shiftHeld: action.ShiftHeld,
                                        ctrlHeld:  action.CtrlHeld,
                                        altHeld:   action.AltHeld);
                                }
                                
                                await Task.Delay(50);
                                overlay?.ShowRightActionInfo(null, null);
                                break;
                            }
                            case "KeyPress":
                                if (!string.IsNullOrWhiteSpace(action.Key))
                                {
                                    string keyDisplay = action.Key;
                                    string desc = $"Đang nhấn phím {action.Key}";
                                    
                                    overlay?.ShowRightActionInfo(keyDisplay, desc);
                                    
                                    if (visualMode == VisualPlaybackMode.Live)
                                        overlay?.DrawKeyPress(ax, ay, action.Key, action.SequenceNumber);
                                    else if (visualMode == VisualPlaybackMode.Ghost)
                                        overlay?.RemoveGhostMarker(action.SequenceNumber);

                                    if (isTargetApp)
                                    {
                                        // Trình duyệt và nhiều app hiện đại không nhận PostMessage keyboard.
                                        // Dùng SendInput (hardware-level) — ForceForeground đã được gọi ở đầu loop.
                                        SendKeyViaSendInput(action.Key);
                                    }
                                    else
                                    {
                                        // Free mode: target đã là foreground (đảm bảo bởi check đầu loop)
                                        await Task.Delay(30); // let focus settle

                                        System.Diagnostics.Debug.WriteLine($"[MacroExecutor] SendInput KeyPress: '{action.Key}' isCombo={action.Key.Contains('+')}");

                                        // Use SendHotkeyPress for combos (e.g. "Ctrl+C"), SendKeyPress for single keys
                                        if (action.Key.Contains('+'))
                                            env.Service.KeyboardInput.SendHotkeyPress(action.Key, 1, 0);
                                        else
                                            env.Service.KeyboardInput.SendKeyPress(action.Key, 1, 0);
                                    }
                                    
                                    await Task.Delay(50);
                                    overlay?.ShowRightActionInfo(null, null);
                                }
                                break;

                            case "MouseMove":
                                if (isTargetApp)
                                {
                                    var (childHwndMv, clientXMv, clientYMv) = FlowMy.Helpers.WindowHelper.GetDeepestChildFromPoint(targetHwnd, ax, ay);
                                    IntPtr lParamMv = FlowMy.Helpers.WindowHelper.MakeLParam(clientXMv, clientYMv);
                                    int mkMv = 0;
                                    if (isLeftDown) mkMv |= FlowMy.Helpers.WindowHelper.MK_LBUTTON;
                                    if (isRightDown) mkMv |= FlowMy.Helpers.WindowHelper.MK_RBUTTON;
                                    FlowMy.Helpers.WindowHelper.PostMessage(childHwndMv, FlowMy.Helpers.WindowHelper.WM_MOUSEMOVE, (IntPtr)mkMv, lParamMv);
                                }
                                // Always update visual cursor regardless of mode
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: false);
                                if (visualMode != VisualPlaybackMode.Silent)
                                    overlay?.AddTrailPoint(ax, ay);
                                if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);
                                break;

                            case "MouseScroll":
                            {
                                string scrollDir = action.ScrollDelta > 0 ? "lên" : "xuống";
                                string desc = $"Đang cuộn chuột {scrollDir}";
                                
                                overlay?.ShowRightActionInfo("Scroll", desc);
                                
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawScroll(ax, ay, action.ScrollDelta, action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                if (isTargetApp)
                                {
                                    var (childScroll, cxScroll, cyScroll) = FlowMy.Helpers.WindowHelper.GetDeepestChildFromPoint(targetHwnd, ax, ay);
                                    int wParamScroll = (action.ScrollDelta * 120) << 16;
                                    // lParam: screen coords (WM_MOUSEWHEEL uses screen, not client)
                                    IntPtr lpScroll = FlowMy.Helpers.WindowHelper.MakeLParam(ax, ay);
                                    FlowMy.Helpers.WindowHelper.PostMessage(childScroll, FlowMy.Helpers.WindowHelper.WM_MOUSEWHEEL, (IntPtr)wParamScroll, lpScroll);
                                }
                                else
                                {
                                    env.Service.MouseInput.SendMouseScrollAt(ax, ay, action.ScrollDelta);
                                }
                                
                                await Task.Delay(50);
                                overlay?.ShowRightActionInfo(null, null);
                                break;
                            }

                            default:
                                System.Diagnostics.Debug.WriteLine(
                                    $"MacroRecorderNodeExecutor: Unknown action type '{action.Type}', skipping.");
                                break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MacroRecorderNodeExecutor error: {ex.Message}");
                env.OnNodeFailed?.Invoke(macroNode, ex.Message);
                throw;
            }
            finally
            {
                // Safety: release all modifier keys in case a MouseDown with modifiers
                // was recorded but MouseUp was never reached (e.g. workflow cancelled)
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

                // Restore main window sau khi macro xong (chỉ khi StayOnTargetAfterExecution = false)
                if (dispatcher != null && !macroNode.StayOnTargetAfterExecution)
                {
                    dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            var win = Application.Current?.MainWindow;
                            if (win != null && win.WindowState == WindowState.Minimized)
                            {
                                win.WindowState = WindowState.Normal;
                                win.Activate();
                            }
                        }
                        catch { }
                    });
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

        // ─── Helpers ─────────────────────────────────────────────────────────────

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
    }
}
