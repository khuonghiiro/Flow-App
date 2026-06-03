using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using FlowMy.Models;
using FlowMy.Models.Enums;
using FlowMy.Models.Nodes;
using FlowMy.Helpers;
using FlowMy.Services.Interaction;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class HotkeyPressEventNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is HotkeyPressEventNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var hotkeyNode = (HotkeyPressEventNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var hotkeyText = hotkeyNode.Key;

            // ── 1. Focus app nếu được cấu hình ──
            await FocusTargetAppAsync(hotkeyNode.TargetProcessName, hotkeyNode.TargetWindowTitle, env.CancellationToken, hotkeyNode.UseBackgroundMode);

            // ── 2. Xử lý theo chế độ TriggerMode ──
            if (hotkeyNode.TriggerMode == HotkeyTriggerModeEnum.Listen)
            {
                // Chế độ Listen: Chờ người dùng nhấn phím đúng
                await ExecuteListenModeAsync(hotkeyNode, env, hotkeyText);
            }
            else
            {
                // Chế độ Send: Workflow tự động nhấn phím (logic hiện tại)
                await ExecuteSendModeAsync(hotkeyNode, env, hotkeyText, connections);
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(hotkeyNode, sw.Elapsed);

            await env.Service.TraverseSingleOutputAndLegacyAsync(
                hotkeyNode, connections, env.CancellationToken,
                env.OnEnteringNode, env.OnNodeStarted, env.OnNodeCompleted,
                env.OnNodeFailed, env.ReachableToEnd,
                env.ExecutionId, env.FlowScopeId, env.BranchId, env.ParentFlowScopeId,
                new List<string>(env.ExecutionPath));
        }

        private async Task ExecuteListenModeAsync(HotkeyPressEventNode hotkeyNode, NodeExecutionEnvironment env, string? hotkeyText)
        {
            if (string.IsNullOrWhiteSpace(hotkeyText))
            {
                System.Diagnostics.Debug.WriteLine($"HotkeyPressEventNode (Listen mode): Hotkey text is empty or null");
                return;
            }

            try
            {
                // Sử dụng GlobalKeyboardHookService để chờ người dùng nhấn phím đúng
                // Service này đã được tối ưu hóa để hạn chế sử dụng RAM trong thời gian chờ
                var keyboardHook = env.Service.GlobalKeyboardHook;
                if (keyboardHook == null)
                {
                    throw new InvalidOperationException("GlobalKeyboardHookService không khả dụng");
                }

                // Chờ người dùng nhấn tổ hợp phím đúng
                // Task này sẽ hoàn thành khi người dùng nhấn đúng phím, hoặc bị hủy bởi cancellation token
                var pressedHotkey = await keyboardHook.WaitForHotkeyAsync(hotkeyText, env.CancellationToken);

                System.Diagnostics.Debug.WriteLine($"HotkeyPressEventNode (Listen mode): User pressed '{pressedHotkey}'");
            }
            catch (OperationCanceledException)
            {
                // Được hủy bởi cancellation token (workflow stop)
                System.Diagnostics.Debug.WriteLine($"HotkeyPressEventNode (Listen mode): Operation cancelled");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HotkeyPressEventNode (Listen mode): Error waiting for hotkey '{hotkeyText}': {ex.Message}");
                env.OnNodeFailed?.Invoke(hotkeyNode, ex.Message);
                throw;
            }
        }

        private async Task ExecuteSendModeAsync(HotkeyPressEventNode hotkeyNode, NodeExecutionEnvironment env, string? hotkeyText, IReadOnlyList<WorkflowConnection> connections)
        {
            // ── Get target window handle for background mode ──
            IntPtr targetHwnd = IntPtr.Zero;
            if (hotkeyNode.UseBackgroundMode && !string.IsNullOrWhiteSpace(hotkeyNode.TargetProcessName))
            {
                var windows = WindowHelper.GetActiveWindows();
                var match = windows.FirstOrDefault(w => w.ProcessName == hotkeyNode.TargetProcessName && w.Title == hotkeyNode.TargetWindowTitle)
                         ?? windows.FirstOrDefault(w => w.ProcessName == hotkeyNode.TargetProcessName);
                if (match != null)
                    targetHwnd = match.Handle;
            }

            // ── Click toạ độ trước khi nhấn hotkey (nếu có) ──
            bool hasCoord = !string.IsNullOrWhiteSpace(hotkeyNode.CoordSourceNodeId) || hotkeyNode.HasManualPosition;
            if (hasCoord)
            {
                var (x, y) = ResolveCoordinates(hotkeyNode, env);
                if (hotkeyNode.ClickOnPosition && (x != 0 || y != 0))
                {
                    var mouse = env.Service.MouseInput;
                    mouse.SendMouseDownAt(x, y, Services.Interaction.MouseButton.Left, false, false, false, targetHwnd: targetHwnd, mode: hotkeyNode.BackgroundInputMode);
                    if (hotkeyNode.ClickDurationMs > 0)
                        await Task.Delay(hotkeyNode.ClickDurationMs, env.CancellationToken);
                    mouse.SendMouseUpAt(x, y, Services.Interaction.MouseButton.Left, false, false, false, targetHwnd: targetHwnd, mode: hotkeyNode.BackgroundInputMode);
                    await Task.Delay(50, env.CancellationToken);
                }
            }

            // ── Nhấn hotkey ──
            var repeatCount = env.Service.GetRepeatCountFromDynamicInputs(hotkeyNode, connections, env) ?? hotkeyNode.RepeatCount;
            var delayMs = hotkeyNode.PressDelayMs;

            if (!string.IsNullOrWhiteSpace(hotkeyText))
            {
                try
                {
                    env.Service.KeyboardInput.SendHotkeyPress(hotkeyText, repeatCount, delayMs, targetHwnd, hotkeyNode.BackgroundInputMode);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sending hotkey press '{hotkeyText}': {ex.Message}");
                    env.OnNodeFailed?.Invoke(hotkeyNode, ex.Message);
                    throw;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"HotkeyPressEventNode: Hotkey text is empty or null");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static (int x, int y) ResolveCoordinates(HotkeyPressEventNode hotkeyNode, NodeExecutionEnvironment env)
        {
            // Ưu tiên 1: đọc từ node nguồn
            if (!string.IsNullOrWhiteSpace(hotkeyNode.CoordSourceNodeId))
            {
                var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                    env.Connections,
                    hotkeyNode.CoordSourceNodeId,
                    hotkeyNode.CoordSourceOutputKey,
                    env);

                if (!string.IsNullOrWhiteSpace(raw) && raw != "—")
                {
                    var parsed = TryParseCoordString(raw);
                    if (parsed.HasValue) return parsed.Value;
                }
            }

            // Ưu tiên 2: dùng toạ độ thủ công
            if (hotkeyNode.HasManualPosition)
                return ((int)hotkeyNode.ManualPosition.X, (int)hotkeyNode.ManualPosition.Y);

            // Ưu tiên 3: toạ độ chuột hiện tại
            GetCursorPos(out var pt);
            return (pt.X, pt.Y);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

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

        private static async Task FocusTargetAppAsync(string processName, string windowTitle, CancellationToken ct, bool useBackgroundMode = false)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;
            var windows = WindowHelper.GetActiveWindows();
            var match = windows.FirstOrDefault(w => w.ProcessName == processName && w.Title == windowTitle)
                     ?? windows.FirstOrDefault(w => w.ProcessName == processName);
            if (match != null)
            {
                // Only activate if not in background mode
                if (!useBackgroundMode)
                {
                    WindowHelper.BringToFront(match.Handle);
                    await Task.Delay(150, ct);
                }
            }
        }
    }
}


