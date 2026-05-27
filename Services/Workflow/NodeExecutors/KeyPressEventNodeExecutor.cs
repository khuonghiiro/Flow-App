using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Helpers;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class KeyPressEventNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is KeyPressEventNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var keyNode = (KeyPressEventNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var keyText = keyNode.Key;

            // ── 1. Focus app nếu được cấu hình ──
            await FocusTargetAppAsync(keyNode.TargetProcessName, keyNode.TargetWindowTitle, env.CancellationToken);

            // ── 2. Click toạ độ trước khi nhấn phím (nếu có) ──
            if (!string.IsNullOrWhiteSpace(keyNode.CoordSourceNodeId))
            {
                var (x, y) = ResolveCoordinates(keyNode, env);
                if (keyNode.ClickOnPosition)
                {
                    var mouse = env.Service.MouseInput;
                    mouse.SendMouseDownAt(x, y, Services.Interaction.MouseButton.Left);
                    if (keyNode.ClickDurationMs > 0)
                        await Task.Delay(keyNode.ClickDurationMs, env.CancellationToken);
                    mouse.SendMouseUpAt(x, y, Services.Interaction.MouseButton.Left);
                    await Task.Delay(50, env.CancellationToken);
                }
            }

            // ── 3. Nhấn phím ──
            var repeatCount = env.Service.GetRepeatCountFromDynamicInputs(keyNode, connections, env) ?? keyNode.RepeatCount;
            var delayMs = keyNode.PressDelayMs;

            if (!string.IsNullOrWhiteSpace(keyText))
            {
                try
                {
                    env.Service.KeyboardInput.SendKeyPress(keyText, repeatCount, delayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sending key press '{keyText}': {ex.Message}");
                    env.OnNodeFailed?.Invoke(keyNode, ex.Message);
                    throw;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"KeyPressEventNode: Key text is empty or null");
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(keyNode, sw.Elapsed);

            await env.Service.TraverseSingleOutputAndLegacyAsync(
                keyNode, connections, env.CancellationToken,
                env.OnEnteringNode, env.OnNodeStarted, env.OnNodeCompleted,
                env.OnNodeFailed, env.ReachableToEnd,
                env.ExecutionId, env.FlowScopeId, env.BranchId, env.ParentFlowScopeId,
                new List<string>(env.ExecutionPath));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static (int x, int y) ResolveCoordinates(KeyPressEventNode keyNode, NodeExecutionEnvironment env)
        {
            var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                env.Connections,
                keyNode.CoordSourceNodeId,
                keyNode.CoordSourceOutputKey,
                env);

            if (!string.IsNullOrWhiteSpace(raw) && raw != "—")
            {
                var parsed = TryParseCoordString(raw);
                if (parsed.HasValue) return parsed.Value;
            }
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


