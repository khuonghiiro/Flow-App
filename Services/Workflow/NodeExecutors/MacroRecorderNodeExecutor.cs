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
    /// Executor cho MacroRecorderNode: phát lại chuỗi thao tác chuột/bàn phím đã ghi.
    /// Dùng MOUSEEVENTF_ABSOLUTE để inject input tại tọa độ tuyệt đối — KHÔNG di chuyển
    /// con trỏ hệ thống, cho phép user dùng chuột thật độc lập trong khi macro chạy.
    /// </summary>
    internal sealed class MacroRecorderNodeExecutor : INodeExecutor
    {
        // ─── Shift+Click via keybd_event (không cần SetCursorPos) ────────────────
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP   = 0x0002;
        private const byte VK_SHIFT          = 0x10;

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
                                // Move virtual cursor (sync) then inject click at absolute coords
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawClick(action.X, action.Y, action.Button ?? "Left", action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                if (action.Button == "ShiftLeft")
                                    SendShiftClickAt(action.X, action.Y, env.Service.MouseInput);
                                else if (Enum.TryParse<MouseButton>(action.Button, ignoreCase: true, out var mb))
                                    env.Service.MouseInput.SendMouseClickAt(action.X, action.Y, mb);
                                else
                                    env.Service.MouseInput.SendMouseClickAt(action.X, action.Y, MouseButton.Left);
                                break;

                            case "MouseDown":
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawClick(action.X, action.Y, action.Button ?? "Left", action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                env.Service.MouseInput.SendMouseDownAt(action.X, action.Y,
                                    action.Button == "Right" ? MouseButton.Right : MouseButton.Left);
                                break;

                            case "MouseUp":
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                env.Service.MouseInput.SendMouseUpAt(action.X, action.Y, MouseButton.Left);
                                break;

                            case "KeyPress":
                                if (!string.IsNullOrWhiteSpace(action.Key))
                                {
                                    if (visualMode == VisualPlaybackMode.Live)
                                        overlay?.DrawKeyPress(action.X, action.Y, action.Key, action.SequenceNumber);
                                    else if (visualMode == VisualPlaybackMode.Ghost)
                                        overlay?.RemoveGhostMarker(action.SequenceNumber);

                                    env.Service.KeyboardInput.SendKeyPress(action.Key, 1, 0);
                                }
                                break;

                            case "MouseMove":
                                // MouseMove: only update virtual cursor + trail, no real cursor move
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: false);
                                if (visualMode != VisualPlaybackMode.Silent)
                                    overlay?.AddTrailPoint(action.X, action.Y);
                                if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);
                                break;

                            case "MouseScroll":
                                overlay?.MoveVirtualCursor(action.X, action.Y, syncBeforeAction: true);
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawScroll(action.X, action.Y, action.ScrollDelta, action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                env.Service.MouseInput.SendMouseScrollAt(action.X, action.Y, action.ScrollDelta);
                                break;

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

        // ─── Shift+Click at absolute coords (no SetCursorPos) ────────────────────

        private static void SendShiftClickAt(int x, int y, MouseInputService mouse)
        {
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
            mouse.SendMouseDownAt(x, y, MouseButton.Left);
            mouse.SendMouseUpAt(x, y, MouseButton.Left);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }
    }
}
