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
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public bool CanExecute(WorkflowNode node) => node is KeyPressEventNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var keyNode = (KeyPressEventNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var keyText = keyNode.Key;

            // ── Lưu foreground window hiện tại để quay về sau (nếu ReturnToOriginalScreen = true) ──
            IntPtr originalForegroundWindow = IntPtr.Zero;
            if (keyNode.ReturnToOriginalScreen)
            {
                originalForegroundWindow = GetForegroundWindow();
                System.Diagnostics.Debug.WriteLine($"[KeyPress] ReturnToOriginalScreen: Saved original window hwnd={originalForegroundWindow}");
            }

            try
            {
                // ── 1. Focus app nếu được cấu hình ──
                await FocusTargetAppAsync(keyNode.TargetProcessName, keyNode.TargetWindowTitle, env.CancellationToken, keyNode.UseBackgroundMode);

            // ── Get target window handle for background mode ──
            IntPtr targetHwnd = IntPtr.Zero;
            if (keyNode.UseBackgroundMode && !string.IsNullOrWhiteSpace(keyNode.TargetProcessName))
            {
                var windows = WindowHelper.GetActiveWindows();
                var match = windows.FirstOrDefault(w => w.ProcessName == keyNode.TargetProcessName && w.Title == keyNode.TargetWindowTitle)
                         ?? windows.FirstOrDefault(w => w.ProcessName == keyNode.TargetProcessName);
                if (match != null)
                    targetHwnd = match.Handle;
            }

            // ── 2. Click toạ độ trước khi nhấn phím (nếu có) ──
            bool hasCoord = !string.IsNullOrWhiteSpace(keyNode.CoordSourceNodeId) || keyNode.HasManualPosition;
            if (hasCoord)
            {
                var (x, y) = ResolveCoordinates(keyNode, env);
                if (keyNode.ClickOnPosition && (x != 0 || y != 0))
                {
                    var mouse = env.Service.MouseInput;
                    mouse.SendMouseDownAt(x, y, Services.Interaction.MouseButton.Left, false, false, false, targetHwnd: targetHwnd, mode: keyNode.BackgroundInputMode);
                    if (keyNode.ClickDurationMs > 0)
                        await Task.Delay(keyNode.ClickDurationMs, env.CancellationToken);
                    mouse.SendMouseUpAt(x, y, Services.Interaction.MouseButton.Left, false, false, false, targetHwnd: targetHwnd, mode: keyNode.BackgroundInputMode);
                    await Task.Delay(50, env.CancellationToken);
                }
            }

            // ── 3. Nhấn phím ──
            var repeatCount = env.Service.GetRepeatCountFromDynamicInputs(keyNode, connections, env) ?? keyNode.RepeatCount;
            var delayMs = GetDelayMs(keyNode.PressDelay, keyNode.DelayUnit);

            if (!string.IsNullOrWhiteSpace(keyText))
            {
                if (keyNode.IsAsync)
                {
                    // Chạy bất đồng bộ: khởi tạo task nền và không chờ
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await env.Service.KeyboardInput.SendKeyPressAsync(keyText, repeatCount, delayMs, targetHwnd, keyNode.BackgroundInputMode, env.CancellationToken);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Async key press error: {ex.Message}");
                        }
                        finally
                        {
                            await RestoreOriginalScreenAsync(keyNode, originalForegroundWindow, env.CancellationToken);
                        }
                    }, env.CancellationToken);
                }
                else
                {
                    // Chạy đồng bộ: chờ xong mới đi tiếp
                    try
                    {
                        await env.Service.KeyboardInput.SendKeyPressAsync(keyText, repeatCount, delayMs, targetHwnd, keyNode.BackgroundInputMode, env.CancellationToken);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error sending key press '{keyText}': {ex.Message}");
                        env.OnNodeFailed?.Invoke(keyNode, ex.Message);
                        throw;
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"KeyPressEventNode: Key text is empty or null");
            }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeyPressEventNode error: {ex.Message}");
                env.OnNodeFailed?.Invoke(keyNode, ex.Message);
                throw;
            }
            finally
            {
                // Nếu chạy ĐỒNG BỘ thì mới restore ở đây. Nếu chạy BẤT ĐỒNG BỘ thì restore trong Task.Run.
                if (!keyNode.IsAsync)
                {
                    await RestoreOriginalScreenAsync(keyNode, originalForegroundWindow, env.CancellationToken);
                }
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

        private static int GetDelayMs(int delay, string unit)
        {
            return (unit?.ToLower()) switch
            {
                "s" => delay * 1000,
                "m" => delay * 60000,
                "h" => delay * 3600000,
                _ => delay // "ms" hoặc default
            };
        }

        private static async Task RestoreOriginalScreenAsync(KeyPressEventNode keyNode, IntPtr originalForegroundWindow, CancellationToken ct)
        {
            if (keyNode.ReturnToOriginalScreen && originalForegroundWindow != IntPtr.Zero)
            {
                try
                {
                    await Task.Delay(100, ct);
                    SetForegroundWindow(originalForegroundWindow);
                    System.Diagnostics.Debug.WriteLine($"[KeyPress] ReturnToOriginalScreen: Restored original window hwnd={originalForegroundWindow}");
                }
                catch (Exception restoreEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[KeyPress] Failed to restore original window: {restoreEx.Message}");
                }
            }
        }

        private static (int x, int y) ResolveCoordinates(KeyPressEventNode keyNode, NodeExecutionEnvironment env)
        {
            // Ưu tiên 1: đọc từ node nguồn
            if (!string.IsNullOrWhiteSpace(keyNode.CoordSourceNodeId))
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
            }

            // Ưu tiên 2: dùng toạ độ thủ công
            if (keyNode.HasManualPosition)
                return ((int)keyNode.ManualPosition.X, (int)keyNode.ManualPosition.Y);

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


