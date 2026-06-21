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

        private static (int screenX, int screenY) ResolveScreenCoords(MacroAction action, Rect bounds)
        {
            if (!bounds.IsEmpty)
            {
                if (action.RelX != 0 || action.RelY != 0)
                {
                    double rx = Math.Clamp(action.RelX, 0.0, 1.0);
                    double ry = Math.Clamp(action.RelY, 0.0, 1.0);
                    int clientX = (int)(rx * bounds.Width);
                    int clientY = (int)(ry * bounds.Height);
                    return ((int)bounds.Left + clientX, (int)bounds.Top + clientY);
                }
                else
                {
                    // Fallback if RelX/RelY is exactly 0 or missing, but still clamp within bounds
                    int cx = (int)Math.Clamp(action.X, bounds.Left, bounds.Right);
                    int cy = (int)Math.Clamp(action.Y, bounds.Top, bounds.Bottom);
                    return (cx, cy);
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
        /// Map tÃªn key (tá»« GetKeyName trong recorder) â†’ VK code.
        /// Bao gá»“m Ä‘áº§y Ä‘á»§: phÃ­m sá»‘, chá»¯, F-keys, navigation, numpad, kÃ½ tá»± Ä‘áº·c biá»‡t.
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
            "â†"         => 0x25,
            "â†‘"         => 0x26,
            "â†’"         => 0x27,
            "â†“"         => 0x28,
            "PrtSc"     => 0x2C,
            "Insert"    => 0x2D,
            "Delete"    => 0x2E,
            // Digits 0â€“9
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            // Letters Aâ€“Z (VK = uppercase ASCII)
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
            // F-keys F1â€“F24
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

        public bool CanExecute(WorkflowNode node) => node is ActionCanVasNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var macroNode = (ActionCanVasNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Empty JSON â†’ traverse and finish without throwing
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
                actions = JsonSerializer.Deserialize<List<MacroAction>>(macroNode.MacroDataJson);
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
            Rect bounds = Rect.Empty;
            var dispatcher = Application.Current?.Dispatcher;
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
                }, DispatcherPriority.Normal);
            }

            if (bounds.IsEmpty || bounds.Width == 0 || bounds.Height == 0)
            {
                throw new Exception("Không thể xác định vị trí của ActionCanvas trên màn hình.");
            }

            int cycles = macroNode.PlaybackMode == MacroPlaybackMode.Once ? 1 : macroNode.RepeatCount;
            var visualMode = macroNode.VisualPlaybackMode;

            MacroPlaybackOverlay? overlay = null;
            if (visualMode != VisualPlaybackMode.Silent && dispatcher != null)
            {
                Task? loadedTask = null;
                dispatcher.Invoke(() =>
                {
                    try
                    {
                        overlay = new MacroPlaybackOverlay();
                        overlay.PrepareForTargetMode();
                        loadedTask = overlay.WhenLoaded;
                        overlay.Show();
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
                        overlay?.PositionOverBounds(bounds);
                        overlay?.ConfigureBorder(
                            macroNode.PlaybackBorderColorHex,
                            macroNode.PlaybackBorderThickness,
                            macroNode.PlaybackGradientSize,
                            macroNode.PlaybackOpacity,
                            macroNode.PlaybackEffectType);
                    }, DispatcherPriority.Normal);
                }
            }

            // Đảm bảo cửa sổ chính đang hiển thị
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    var win = Application.Current?.MainWindow;
                    if (win != null)
                    {
                        if (win.WindowState == WindowState.Minimized) win.WindowState = WindowState.Normal;
                        win.Activate();
                    }
                }, DispatcherPriority.Normal);
            }

            try
            {
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
                                
                                var btn = action.Button == "Right" ? MouseButton.Right : MouseButton.Left;
                                env.Service.MouseInput.SendMouseClickAt((int)ax, (int)ay, btn, 1, 0.05, action.ShiftHeld, action.CtrlHeld, action.AltHeld, restoreCursor: false);
                                
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

                                var btnDown = action.Button == "Right" ? MouseButton.Right : MouseButton.Left;
                                env.Service.MouseInput.SendMouseDownAt(ax, ay, btnDown, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                break;
                            }
                            case "MouseUp":
                            {
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);

                                var btnUp = action.Button == "Right" ? MouseButton.Right : MouseButton.Left;
                                env.Service.MouseInput.SendMouseUpAt(ax, ay, btnUp, action.ShiftHeld, action.CtrlHeld, action.AltHeld, restoreCursor: false);
                                
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
                                env.Service.MouseInput.MoveCursorTo(ax, ay);
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: false);
                                if (visualMode != VisualPlaybackMode.Silent) overlay?.AddTrailPoint(ax, ay);
                                if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);
                                break;
                            case "MouseScroll":
                            {
                                overlay?.ShowRightActionInfo("Scroll", $"Đang cuộn chuột {(action.ScrollDelta > 0 ? "lên" : "xuống")}");
                                overlay?.MoveVirtualCursor(ax, ay, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live) overlay?.DrawScroll(ax, ay, action.ScrollDelta, action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost) overlay?.RemoveGhostMarker(action.SequenceNumber);

                                env.Service.MouseInput.SendMouseScrollAt(ax, ay, action.ScrollDelta);
                                
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
        /// Táº¡o label hiá»ƒn thá»‹ cho tooltip: "Chuá»™t trÃ¡i", "Chuá»™t pháº£i", "Ctrl + Chuá»™t trÃ¡i", v.v.
        /// </summary>
        private static string BuildClickHint(string? button, bool shift, bool ctrl, bool alt)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (ctrl)  parts.Add("Ctrl");
            if (alt)   parts.Add("Alt");
            if (shift) parts.Add("Shift");
            parts.Add(button == "Right" ? "Chuá»™t pháº£i" : "Chuá»™t trÃ¡i");
            return string.Join(" + ", parts);
        }

        /// <summary>
        /// Táº¡o mÃ´ táº£ hÃ nh Ä‘á»™ng cho floating tooltip.
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

            string buttonName = button == "Right" ? "chuá»™t pháº£i" : "chuá»™t trÃ¡i";

            return actionType switch
            {
                "MouseClick" => $"Äang nháº¥n {modifiers}{buttonName}",
                "MouseDown"  => $"Äang giá»¯ {modifiers}{buttonName}",
                _            => $"Äang thao tÃ¡c {modifiers}{buttonName}"
            };
        }
    }
}
