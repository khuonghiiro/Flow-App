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

            // Lưu HWND của app target (foreground window sau khi FlowMy đã minimize)
            IntPtr targetHwnd = GetForegroundWindow();
            System.Diagnostics.Debug.WriteLine($"[MacroExecutor] targetHwnd=0x{targetHwnd:X}");

            try
            {
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
                        switch (action.Type)
                        {
                            case "MouseClick":
                            {
                                string hint = BuildClickHint(action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                string desc = GetActionDescription("MouseClick", action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: true);
                                overlay?.ShowRightActionInfo(hint, desc);
                                
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawClick(action.X, action.Y, action.Button ?? "Left", action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                if (targetHwnd != IntPtr.Zero && IsWindow(targetHwnd))
                                    ForceForeground(targetHwnd);

                                var btn = action.Button == "Right" ? MouseButton.Right : MouseButton.Left;
                                env.Service.MouseInput.SendMouseClickAt(
                                    action.X, action.Y, btn,
                                    shiftHeld: action.ShiftHeld,
                                    ctrlHeld:  action.CtrlHeld,
                                    altHeld:   action.AltHeld);
                                
                                await Task.Delay(50); // Đợi click hoàn tất
                                overlay?.ShowRightActionInfo(null, null);
                                break;
                            }

                            case "MouseDown":
                            {
                                string hint = BuildClickHint(action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                string desc = GetActionDescription("MouseDown", action.Button, action.ShiftHeld, action.CtrlHeld, action.AltHeld);
                                
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: true);
                                overlay?.ShowRightActionInfo(hint, desc);
                                
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawClick(action.X, action.Y, action.Button ?? "Left", action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                if (targetHwnd != IntPtr.Zero && IsWindow(targetHwnd))
                                    ForceForeground(targetHwnd);

                                var btnDown = action.Button == "Right" ? MouseButton.Right : MouseButton.Left;
                                env.Service.MouseInput.SendMouseDownAt(
                                    action.X, action.Y, btnDown,
                                    shiftHeld: action.ShiftHeld,
                                    ctrlHeld:  action.CtrlHeld,
                                    altHeld:   action.AltHeld);
                                // Không ẩn panel — giữ hiển thị cho đến khi MouseUp
                                break;
                            }

                            case "MouseUp":
                            {
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: true);
                                
                                if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                var btnUp = action.Button == "Right" ? MouseButton.Right : MouseButton.Left;
                                env.Service.MouseInput.SendMouseUpAt(
                                    action.X, action.Y, btnUp,
                                    shiftHeld: action.ShiftHeld,
                                    ctrlHeld:  action.CtrlHeld,
                                    altHeld:   action.AltHeld);
                                
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
                                        overlay?.DrawKeyPress(action.X, action.Y, action.Key, action.SequenceNumber);
                                    else if (visualMode == VisualPlaybackMode.Ghost)
                                        overlay?.RemoveGhostMarker(action.SequenceNumber);

                                    // Ensure target window has focus BEFORE sending keys
                                    if (targetHwnd != IntPtr.Zero && IsWindow(targetHwnd))
                                    {
                                        ForceForeground(targetHwnd);
                                        await Task.Delay(30); // let focus settle
                                    }

                                    System.Diagnostics.Debug.WriteLine($"[MacroExecutor] KeyPress: '{action.Key}' isCombo={action.Key.Contains('+')}");

                                    // Use SendHotkeyPress for combos (e.g. "Ctrl+C"), SendKeyPress for single keys
                                    if (action.Key.Contains('+'))
                                        env.Service.KeyboardInput.SendHotkeyPress(action.Key, 1, 0);
                                    else
                                        env.Service.KeyboardInput.SendKeyPress(action.Key, 1, 0);
                                    
                                    await Task.Delay(50);
                                    overlay?.ShowRightActionInfo(null, null);
                                }
                                break;

                            case "MouseMove":
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: false);
                                if (visualMode != VisualPlaybackMode.Silent)
                                    overlay?.AddTrailPoint(action.X, action.Y);
                                if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);
                                break;

                            case "MouseScroll":
                            {
                                string scrollDir = action.ScrollDelta > 0 ? "lên" : "xuống";
                                string desc = $"Đang cuộn chuột {scrollDir}";
                                
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: true);
                                overlay?.ShowRightActionInfo("Scroll", desc);
                                
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawScroll(action.X, action.Y, action.ScrollDelta, action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                if (targetHwnd != IntPtr.Zero && IsWindow(targetHwnd))
                                    ForceForeground(targetHwnd);
                                env.Service.MouseInput.SendMouseScrollAt(action.X, action.Y, action.ScrollDelta);
                                
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

                // Restore main window sau khi macro xong
                if (dispatcher != null)
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
