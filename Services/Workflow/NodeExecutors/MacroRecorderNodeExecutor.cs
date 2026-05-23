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
    /// Hiển thị MacroPlaybackOverlay trong suốt quá trình phát lại để user thấy tiến trình.
    /// </summary>
    internal sealed class MacroRecorderNodeExecutor : INodeExecutor
    {
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

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
                // Create and show overlay on UI thread, then await its Loaded event before
                // proceeding — this guarantees DrawingCanvas is ready and click-through is applied.
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

                // Wait for the overlay's Loaded event so DrawingCanvas is ready and
                // MakeClickThrough() has been called before we start drawing or executing actions.
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

                    // Clear visuals between cycles
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

                        // Update progress display
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
                                SetCursorPos(action.X, action.Y);
                                overlay?.MoveVirtualCursor(action.X, action.Y);
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawClick(action.X, action.Y, action.Button ?? "Left", action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                if (action.Button == "ShiftLeft")
                                {
                                    SendShiftClick(action.X, action.Y);
                                }
                                else if (Enum.TryParse<MouseButton>(action.Button, ignoreCase: true, out var mouseButton))
                                {
                                    env.Service.MouseInput.SendMouseClick(mouseButton, 1, 0);
                                }
                                else
                                {
                                    env.Service.MouseInput.SendMouseClick(MouseButton.Left, 1, 0);
                                }
                                break;

                            case "MouseDown":
                                SetCursorPos(action.X, action.Y);
                                overlay?.MoveVirtualCursor(action.X, action.Y);
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawClick(action.X, action.Y, action.Button ?? "Left", action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                env.Service.MouseInput.SendMouseDown(
                                    action.Button == "Right" ? MouseButton.Right : MouseButton.Left);
                                break;

                            case "MouseUp":
                                SetCursorPos(action.X, action.Y);
                                overlay?.MoveVirtualCursor(action.X, action.Y);
                                if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                env.Service.MouseInput.SendMouseUp(MouseButton.Left);
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
                                SetCursorPos(action.X, action.Y);
                                overlay?.MoveVirtualCursor(action.X, action.Y);
                                if (visualMode != VisualPlaybackMode.Silent)
                                    overlay?.AddTrailPoint(action.X, action.Y);
                                if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);
                                break;

                            case "MouseScroll":
                                overlay?.MoveVirtualCursor(action.X, action.Y);
                                if (visualMode == VisualPlaybackMode.Live)
                                    overlay?.DrawScroll(action.X, action.Y, action.ScrollDelta, action.SequenceNumber);
                                else if (visualMode == VisualPlaybackMode.Ghost)
                                    overlay?.RemoveGhostMarker(action.SequenceNumber);

                                SendScroll(action.X, action.Y, action.ScrollDelta);
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
                // Wait a moment so the last visual markers are visible before closing
                if (overlay != null)
                {
                    try { await Task.Delay(800); } catch { }
                }

                // Close overlay on UI thread — use InvokeAsync (awaited) to ensure it completes
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

        // ─── P/Invoke for scroll ──────────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_WHEEL     = 0x0800;
        private const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP    = 0x0004;
        private const uint KEYEVENTF_KEYDOWN     = 0x0000;
        private const uint KEYEVENTF_KEYUP       = 0x0002;
        private const byte VK_SHIFT              = 0x10;

        private static void SendScroll(int x, int y, int notches)
        {
            if (notches == 0) return;
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_WHEEL, x, y, notches * 120, IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        private static void SendShiftClick(int x, int y)
        {
            SetCursorPos(x, y);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, IntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP,   x, y, 0, IntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }
    }
}
