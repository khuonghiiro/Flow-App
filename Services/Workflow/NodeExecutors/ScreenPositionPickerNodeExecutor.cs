using FlowMy.Models;
using FlowMy.Services.Interaction;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho ScreenPositionPickerNode:
    /// 1. Giải toạ độ (ưu tiên node nguồn, fallback SelectedPosition)
    /// 2. Thực hiện hành động chuột tại toạ độ đó
    ///    - LeftClick / RightClick: mỗi lần = nhấn xuống → giữ HoldDurationMs → nhả → lặp ClickCount lần
    ///    - ScrollUp / ScrollDown: mỗi lần = 1 notch → chờ ScrollIntervalMs → lặp ScrollCount lần
    /// 3. Traverse sang node tiếp theo
    /// </summary>
    internal sealed class ScreenPositionPickerNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is ScreenPositionPickerNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var posNode = (ScreenPositionPickerNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // ── 1. Giải toạ độ ──
                var (x, y) = ResolveCoordinates(posNode, env);

                // ── 2. Thực hiện hành động ──
                switch (posNode.MouseAction)
                {
                    case ScreenPositionMouseAction.LeftClick:
                        await PerformClickAsync(env, x, y, MouseButton.Left,
                            posNode.ClickCount, posNode.HoldDurationMs,
                            env.CancellationToken);
                        break;

                    case ScreenPositionMouseAction.RightClick:
                        await PerformClickAsync(env, x, y, MouseButton.Right,
                            posNode.ClickCount, posNode.HoldDurationMs,
                            env.CancellationToken);
                        break;

                    case ScreenPositionMouseAction.ScrollUp:
                        await PerformScrollAsync(env, x, y,
                            notchDirection: 1,
                            posNode.ScrollCount, posNode.ScrollIntervalMs,
                            env.CancellationToken);
                        break;

                    case ScreenPositionMouseAction.ScrollDown:
                        await PerformScrollAsync(env, x, y,
                            notchDirection: -1,
                            posNode.ScrollCount, posNode.ScrollIntervalMs,
                            env.CancellationToken);
                        break;

                    case ScreenPositionMouseAction.None:
                    default:
                        // Không làm gì — chỉ publish toạ độ rồi đi tiếp
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(posNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(posNode, sw.Elapsed);

            await env.TraverseOutputsAsync(posNode);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Giải toạ độ
        // ─────────────────────────────────────────────────────────────────────

        private static (int x, int y) ResolveCoordinates(ScreenPositionPickerNode posNode, NodeExecutionEnvironment env)
        {
            // Ưu tiên: đọc từ node nguồn nếu được cấu hình
            if (!string.IsNullOrWhiteSpace(posNode.CoordSourceNodeId))
            {
                var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                    env.Connections,
                    posNode.CoordSourceNodeId,
                    posNode.CoordSourceOutputKey,
                    env);

                if (!string.IsNullOrWhiteSpace(raw) && raw != "—")
                {
                    // Thử parse "X: 123, Y: 456" hoặc "123,456" hoặc "123"
                    var parsed = TryParseCoordString(raw, posNode.CoordSourceOutputKey);
                    if (parsed.HasValue)
                        return parsed.Value;
                }
            }

            // Fallback: dùng toạ độ đã chọn thủ công
            return ((int)posNode.SelectedPosition.X, (int)posNode.SelectedPosition.Y);
        }

        /// <summary>
        /// Parse chuỗi toạ độ từ output node nguồn.
        /// Hỗ trợ: "X: 123, Y: 456" | "123,456" | "123" (chỉ x hoặc y tuỳ key)
        /// </summary>
        private static (int x, int y)? TryParseCoordString(string raw, string? outputKey)
        {
            raw = raw.Trim();

            // Dạng "X: 123, Y: 456" hoặc "123, 456"
            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var xStr = parts[0].Trim().TrimStart('X', 'x', ':', ' ');
                var yStr = parts[1].Trim().TrimStart('Y', 'y', ':', ' ');
                if (int.TryParse(xStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var px) &&
                    int.TryParse(yStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var py))
                    return (px, py);
            }

            // Dạng số đơn — xác định là x hay y theo key
            if (int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var single))
            {
                var key = (outputKey ?? string.Empty).Trim().ToLowerInvariant();
                if (key == "y") return (0, single);
                return (single, 0); // mặc định là x
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Click: mỗi lần = nhấn → giữ HoldDurationMs → nhả
        // ─────────────────────────────────────────────────────────────────────

        private static async Task PerformClickAsync(
            NodeExecutionEnvironment env,
            int x, int y,
            MouseButton button,
            int clickCount,
            int holdDurationMs,
            CancellationToken ct)
        {
            if (clickCount < 1) clickCount = 1;
            if (holdDurationMs < 0) holdDurationMs = 0;

            var mouse = env.Service.MouseInput;

            for (int i = 0; i < clickCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                // Di chuyển chuột đến toạ độ, nhấn xuống
                mouse.SendMouseDownAt(x, y, button);

                // Giữ trong HoldDurationMs ms rồi nhả
                if (holdDurationMs > 0)
                    await Task.Delay(holdDurationMs, ct);

                mouse.SendMouseUpAt(x, y, button);

                // Khoảng nghỉ ngắn giữa các lần click (trừ lần cuối)
                if (i < clickCount - 1)
                    await Task.Delay(50, ct);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Scroll: mỗi lần = 1 notch → chờ ScrollIntervalMs → lặp ScrollCount lần
        // ─────────────────────────────────────────────────────────────────────

        private static async Task PerformScrollAsync(
            NodeExecutionEnvironment env,
            int x, int y,
            int notchDirection,   // +1 = up, -1 = down
            int scrollCount,
            int scrollIntervalMs,
            CancellationToken ct)
        {
            if (scrollCount < 1) scrollCount = 1;
            if (scrollIntervalMs < 0) scrollIntervalMs = 0;

            var mouse = env.Service.MouseInput;

            for (int i = 0; i < scrollCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                // Lăn 1 notch tại toạ độ đã chọn
                mouse.SendMouseScrollAt(x, y, notchDirection);

                // Chờ ScrollIntervalMs trước lần tiếp theo (kể cả lần cuối để đúng timing)
                if (scrollIntervalMs > 0)
                    await Task.Delay(scrollIntervalMs, ct);
            }
        }
    }
}
