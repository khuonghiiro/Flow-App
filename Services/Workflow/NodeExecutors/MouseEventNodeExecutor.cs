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
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public bool CanExecute(WorkflowNode node) => node is MouseEventNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var mouseNode = (MouseEventNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // ── Lưu foreground window hiện tại để quay về sau (nếu ReturnToOriginalScreen = true) ──
            IntPtr originalForegroundWindow = IntPtr.Zero;
            if (mouseNode.ReturnToOriginalScreen)
            {
                originalForegroundWindow = GetForegroundWindow();
                System.Diagnostics.Debug.WriteLine($"[MouseEvent] ReturnToOriginalScreen: Saved original window hwnd={originalForegroundWindow}");
            }

            try
            {
                // ── Validation ────────────────────────────────────────────────
                if (mouseNode.UseBackgroundMode && string.IsNullOrWhiteSpace(mouseNode.TargetProcessName))
                {
                    var errorMsg = "Background Mode được bật nhưng chưa chọn ứng dụng đích. Vui lòng chọn app hoặc tắt Background Mode.";
                    System.Diagnostics.Debug.WriteLine($"[MouseEvent] Validation failed: {errorMsg}");
                    env.OnNodeFailed?.Invoke(mouseNode, errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }

                // ── 1. Focus app nếu được cấu hình (bỏ qua nếu background mode) ──
                await FocusTargetAppAsync(mouseNode.TargetProcessName, mouseNode.TargetWindowTitle, env.CancellationToken, mouseNode.UseBackgroundMode);

                // ── 2. Lấy handle window đích khi dùng background mode ──
                IntPtr targetHwnd = IntPtr.Zero;
                string bgModeStatus = "disabled";
                
                if (mouseNode.UseBackgroundMode && !string.IsNullOrWhiteSpace(mouseNode.TargetProcessName))
                {
                    var windows = WindowHelper.GetActiveWindows();
                    var match = windows.FirstOrDefault(w => w.ProcessName == mouseNode.TargetProcessName && w.Title == mouseNode.TargetWindowTitle)
                             ?? windows.FirstOrDefault(w => w.ProcessName == mouseNode.TargetProcessName);
                    if (match != null)
                    {
                        targetHwnd = match.Handle;
                        bgModeStatus = $"enabled - hwnd={targetHwnd} mode={mouseNode.BackgroundInputMode}";
                        
                        // Validate Interception Driver nếu được chọn
                        if (mouseNode.BackgroundInputMode == BackgroundInputHelper.InputMode.InterceptionDriver 
                            && !InterceptionInputHelper.IsDriverInstalled())
                        {
                            System.Diagnostics.Debug.WriteLine($"[MouseEvent] WARNING: Interception Driver selected but not installed - will fallback");
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[MouseEvent] Background mode: Found target window hwnd={targetHwnd} process={mouseNode.TargetProcessName} title={mouseNode.TargetWindowTitle}");
                    }
                    else
                    {
                        bgModeStatus = "enabled but target window NOT FOUND";
                        System.Diagnostics.Debug.WriteLine($"[MouseEvent] Background mode: Target window NOT FOUND process={mouseNode.TargetProcessName} title={mouseNode.TargetWindowTitle}");
                        System.Diagnostics.Debug.WriteLine($"[MouseEvent] Available windows: {string.Join(", ", windows.Select(w => $"{w.ProcessName}:{w.Title}"))}");
                    }
                }
                else if (mouseNode.UseBackgroundMode)
                {
                    bgModeStatus = "enabled but NO TARGET PROCESS";
                    System.Diagnostics.Debug.WriteLine($"[MouseEvent] Background mode enabled but no target process specified");
                }
                
                System.Diagnostics.Debug.WriteLine($"[MouseEvent] Background Mode Status: {bgModeStatus}");

                // ── 3. Resolve toạ độ từ node nguồn hoặc thủ công ──
                bool hasCoord = !string.IsNullOrWhiteSpace(mouseNode.CoordSourceNodeId) || mouseNode.HasManualPosition;
                int cx = 0, cy = 0;
                if (hasCoord)
                {
                    (cx, cy) = ResolveCoordinates(mouseNode, env);
                    System.Diagnostics.Debug.WriteLine($"[MouseEvent] Resolved coordinates: X={cx}, Y={cy} (hasManualPos={mouseNode.HasManualPosition}, sourceNode={mouseNode.CoordSourceNodeId})");
                }

                // Parse mouse button
                if (!Enum.TryParse<MouseButton>(mouseNode.MouseButton, out var button))
                    button = MouseButton.Left;

                // ── 4. Click tại toạ độ trước khi thực hiện (nếu được bật và có toạ độ) ──
                if (hasCoord && mouseNode.ClickOnPosition)
                {
                    System.Diagnostics.Debug.WriteLine($"[MouseEvent] ClickOnPosition: Clicking at X={cx}, Y={cy} button=Left duration={mouseNode.ClickDurationMs}ms hwnd={targetHwnd} mode={mouseNode.BackgroundInputMode}");
                    env.Service.MouseInput.SendMouseDownAt(cx, cy, MouseButton.Left, false, false, false, targetHwnd, mouseNode.BackgroundInputMode);
                    if (mouseNode.ClickDurationMs > 0)
                        await Task.Delay(mouseNode.ClickDurationMs, env.CancellationToken);
                    env.Service.MouseInput.SendMouseUpAt(cx, cy, MouseButton.Left, false, false, false, targetHwnd, mouseNode.BackgroundInputMode);
                    await Task.Delay(50, env.CancellationToken);
                }

                // ── 5. Thực thi mouse event ──
                if (button == MouseButton.ScrollUp || button == MouseButton.ScrollDown)
                {
                    var scrollSpeed = mouseNode.ScrollSpeed;
                    if (hasCoord)
                    {
                        if (mouseNode.UseBackgroundMode && targetHwnd != IntPtr.Zero)
                        {
                            // Background scroll dùng BackgroundInputHelper
                            int delta = button == MouseButton.ScrollUp ? scrollSpeed : -scrollSpeed;
                            BackgroundInputHelper.SendMouseScroll(targetHwnd, cx, cy, delta, mouseNode.BackgroundInputMode);
                        }
                        else
                        {
                            env.Service.MouseInput.SendMouseScrollAt(cx, cy, button == MouseButton.ScrollUp ? 1 : -1);
                        }
                    }
                    else
                    {
                        env.Service.MouseInput.SendMouseScroll(button, scrollSpeed);
                    }
                }
                else
                {
                    var repeatCount = env.Service.GetRepeatCountFromDynamicInputs(mouseNode, connections, env) ?? mouseNode.RepeatCount;
                    var holdDuration = mouseNode.HoldDuration;

                    if (hasCoord)
                    {
                        for (int i = 0; i < repeatCount; i++)
                        {
                            env.CancellationToken.ThrowIfCancellationRequested();
                            env.Service.MouseInput.SendMouseDownAt(cx, cy, button, false, false, false, targetHwnd, mouseNode.BackgroundInputMode);
                            if (holdDuration > 0)
                                await Task.Delay((int)(holdDuration * 1000), env.CancellationToken);
                            env.Service.MouseInput.SendMouseUpAt(cx, cy, button, false, false, false, targetHwnd, mouseNode.BackgroundInputMode);
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
            finally
            {
                // ── Quay trở lại foreground window ban đầu (nếu ReturnToOriginalScreen = true) ──
                if (mouseNode.ReturnToOriginalScreen && originalForegroundWindow != IntPtr.Zero)
                {
                    try
                    {
                        await Task.Delay(100, env.CancellationToken); // Chờ một chút cho action hoàn tất
                        SetForegroundWindow(originalForegroundWindow);
                        System.Diagnostics.Debug.WriteLine($"[MouseEvent] ReturnToOriginalScreen: Restored original window hwnd={originalForegroundWindow}");
                    }
                    catch (Exception restoreEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MouseEvent] Failed to restore original window: {restoreEx.Message}");
                    }
                }
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(mouseNode, sw.Elapsed);

            await env.TraverseOutputsAsync(mouseNode);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static (int x, int y) ResolveCoordinates(MouseEventNode mouseNode, NodeExecutionEnvironment env)
        {
            // Ưu tiên 1: đọc từ node nguồn
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

            // Ưu tiên 2: dùng toạ độ thủ công
            if (mouseNode.HasManualPosition)
                return ((int)mouseNode.ManualPosition.X, (int)mouseNode.ManualPosition.Y);

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
                // Chỉ đưa app lên foreground khi không dùng background mode
                if (!useBackgroundMode)
                {
                    WindowHelper.BringToFront(match.Handle);
                    await Task.Delay(150, ct);
                }
            }
        }
    }
}
