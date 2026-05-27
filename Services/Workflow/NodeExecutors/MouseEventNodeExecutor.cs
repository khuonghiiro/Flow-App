using System.Globalization;
using System.Linq;
using System.Threading;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Helpers;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class MouseEventNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is MouseEventNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var mouseNode = (MouseEventNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // ── 1. Focus app nếu được cấu hình ──
                await FocusTargetAppAsync(mouseNode.TargetProcessName, mouseNode.TargetWindowTitle, env.CancellationToken);

                // ── 2. Resolve toạ độ từ node nguồn hoặc thủ công ──
                bool hasCoord = !string.IsNullOrWhiteSpace(mouseNode.CoordSourceNodeId) || mouseNode.HasManualPosition;
                int cx = 0, cy = 0;
                if (hasCoord)
                    (cx, cy) = ResolveCoordinates(mouseNode, env);

                // Parse mouse button
                if (!Enum.TryParse<MouseButton>(mouseNode.MouseButton, out var button))
                    button = MouseButton.Left;

                // ── 3. Click tại toạ độ trước khi thực hiện (nếu được bật và có toạ độ) ──
                if (hasCoord && mouseNode.ClickOnPosition)
                {
                    env.Service.MouseInput.SendMouseDownAt(cx, cy, MouseButton.Left);
                    if (mouseNode.ClickDurationMs > 0)
                        await Task.Delay(mouseNode.ClickDurationMs, env.CancellationToken);
                    env.Service.MouseInput.SendMouseUpAt(cx, cy, MouseButton.Left);
                    await Task.Delay(50, env.CancellationToken);
                }

                // ── 4. Thực thi mouse event ──
                if (button == MouseButton.ScrollUp || button == MouseButton.ScrollDown)
                {
                    var scrollSpeed = mouseNode.ScrollSpeed;
                    if (hasCoord)
                        env.Service.MouseInput.SendMouseScrollAt(cx, cy, button == MouseButton.ScrollUp ? 1 : -1);
                    else
                        env.Service.MouseInput.SendMouseScroll(button, scrollSpeed);
                }
                else
                {
                    var repeatCount = env.Service.GetRepeatCountFromDynamicInputs(mouseNode, connections, env) ?? mouseNode.RepeatCount;
                    var holdDuration = mouseNode.HoldDuration;

                    if (hasCoord)
                    {
                        // Click tại toạ độ cụ thể
                        for (int i = 0; i < repeatCount; i++)
                        {
                            env.CancellationToken.ThrowIfCancellationRequested();
                            env.Service.MouseInput.SendMouseDownAt(cx, cy, button);
                            if (holdDuration > 0)
                                await Task.Delay((int)(holdDuration * 1000), env.CancellationToken);
                            env.Service.MouseInput.SendMouseUpAt(cx, cy, button);
                            if (i < repeatCount - 1)
                                await Task.Delay(50, env.CancellationToken);
                        }
                    }
                    else
                    {
                        env.Service.MouseInput.SendMouseClick(button, repeatCount, holdDuration);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MouseEvent error: {ex.Message}");
                env.OnNodeFailed?.Invoke(mouseNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(mouseNode, sw.Elapsed);

            await env.TraverseOutputsAsync(mouseNode);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static (int x, int y) ResolveCoordinates(MouseEventNode mouseNode, NodeExecutionEnvironment env)
        {
            // Ưu tiên: đọc từ node nguồn
            if (!string.IsNullOrWhiteSpace(mouseNode.CoordSourceNodeId))
            {
                var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                    env.Connections,
                    mouseNode.CoordSourceNodeId,
                    mouseNode.CoordSourceOutputKey,
                    env);

                if (!string.IsNullOrWhiteSpace(raw) && raw != "—")
                {
                    var parsed = TryParseCoordString(raw);
                    if (parsed.HasValue) return parsed.Value;
                }
            }

            // Fallback: dùng toạ độ thủ công
            if (mouseNode.HasManualPosition)
                return ((int)mouseNode.ManualPosition.X, (int)mouseNode.ManualPosition.Y);

            return (0, 0);
        }

        private static (int x, int y)? TryParseCoordString(string raw)
        {
            raw = raw.Trim().Trim('(', ')');
            var parts = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var xStr = parts[0].Trim().TrimStart('X', 'x', ':', ' ');
                var yStr = parts[1].Trim().TrimStart('Y', 'y', ':', ' ');
                if (int.TryParse(xStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var px) &&
                    int.TryParse(yStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var py))
                    return (px, py);
            }
            if (int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var single))
                return (single, 0);
            return null;
        }

        private static async Task FocusTargetAppAsync(string processName, string windowTitle, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;
            var windows = WindowHelper.GetActiveWindows();
            var match = windows.FirstOrDefault(w => w.ProcessName == processName && w.Title == windowTitle)
                     ?? windows.FirstOrDefault(w => w.ProcessName == processName);
            if (match != null)
            {
                WindowHelper.BringToFront(match.Handle);
                await Task.Delay(150, ct);
            }
        }
    }
}


