using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Views.Overlays;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho BorderHighlightNode.
    /// Hiển thị viền sáng màn hình với cấu hình màu, độ dày, hiệu ứng.
    /// Có thể tắt các node BorderHighlight khác trước khi chạy.
    /// </summary>
    internal sealed class BorderHighlightNodeExecutor : INodeExecutor
    {
        // Static dictionary để track các overlay đang active theo node ID
        private static readonly Dictionary<string, BorderHighlightOverlay> _activeOverlays = new();

        public bool CanExecute(WorkflowNode node) => node is BorderHighlightNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var borderNode = (BorderHighlightNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // ── Tắt các node BorderHighlight khác nếu được cấu hình ────────────────
            await DisableSelectedNodesAsync(borderNode, env);

            // ── Tạo và hiển thị overlay ───────────────────────────────────────────
            BorderHighlightOverlay? overlay = null;
            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher != null)
            {
                Task? loadedTask = null;
                dispatcher.Invoke(() =>
                {
                    try
                    {
                        overlay = new BorderHighlightOverlay();
                        overlay.Configure(
                            borderNode.BorderColorHex,
                            borderNode.BorderThickness,
                            borderNode.GradientSize,
                            borderNode.Opacity,
                            borderNode.EffectType
                        );
                        loadedTask = overlay.WhenLoaded;
                        overlay.Show();
                        overlay.Activate();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] overlay.Show failed: {ex}");
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

            // ── Định vị overlay theo chế độ ───────────────────────────────────────
            IntPtr targetHwnd = IntPtr.Zero;
            if (overlay != null && dispatcher != null)
            {
                if (borderNode.HighlightMode == BorderHighlightMode.TargetApp)
                {
                    // Tìm target window từ SelectedWindowJson hoặc từ TargetProcessName/TargetWindowTitle (fallback)
                    WindowInfo? targetWindow = null;
                    if (!string.IsNullOrWhiteSpace(borderNode.SelectedWindowJson))
                    {
                        try
                        {
                            targetWindow = JsonSerializer.Deserialize<WindowInfo>(borderNode.SelectedWindowJson);
                        }
                        catch { }
                    }

                    if (targetWindow != null)
                    {
                        targetHwnd = targetWindow.Handle;
                    }
                    else
                    {
                        // Fallback: tìm theo ProcessName/Title
                        var windows = WindowHelper.GetActiveWindows();
                        var match = windows.FirstOrDefault(w =>
                            w.ProcessName == borderNode.TargetProcessName &&
                            w.Title == borderNode.TargetWindowTitle);

                        if (match == null)
                            match = windows.FirstOrDefault(w => w.ProcessName == borderNode.TargetProcessName);

                        if (match != null)
                            targetHwnd = match.Handle;
                    }

                    if (targetHwnd != IntPtr.Zero)
                    {
                        await dispatcher.InvokeAsync(() =>
                        {
                            overlay.PositionOverTarget(targetHwnd);
                        }, DispatcherPriority.Normal);
                    }
                }
                // Fullscreen mode - overlay đã maximize trong Loaded handler
            }

            // ── Track overlay để có thể tắt sau này ───────────────────────────────
            if (overlay != null)
            {
                _activeOverlays[borderNode.Id] = overlay;
            }

            // ── Parallel mode: chạy node tiếp theo ngay lập tức ────────────────────
            if (!borderNode.WaitForCompletion)
            {
                // Bắt đầu task để tắt overlay sau DurationMs
                if (borderNode.DurationMs > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(borderNode.DurationMs, env.CancellationToken);
                            if (dispatcher != null)
                            {
                                dispatcher.Invoke(() =>
                                {
                                    if (_activeOverlays.TryGetValue(borderNode.Id, out var ov))
                                    {
                                        ov.StopAnimation();
                                        ov.Close();
                                        _activeOverlays.Remove(borderNode.Id);
                                    }
                                });
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch { }
                    }, env.CancellationToken);
                }

                sw.Stop();
                env.OnNodeCompleted?.Invoke(borderNode, sw.Elapsed);
                await env.TraverseOutputsAsync(node);
                return;
            }

            // ── Sequential mode: chờ hết duration trước khi chạy node tiếp theo ─────
            if (borderNode.DurationMs > 0)
            {
                await Task.Delay(borderNode.DurationMs, env.CancellationToken);
            }

            // ── Tắt overlay sau khi hết thời gian ─────────────────────────────────
            if (overlay != null && dispatcher != null)
            {
                dispatcher.Invoke(() =>
                {
                    overlay.StopAnimation();
                    overlay.Close();
                });
                _activeOverlays.Remove(borderNode.Id);
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(borderNode, sw.Elapsed);
            await env.TraverseOutputsAsync(node);
        }

        /// <summary>
        /// Tắt các overlay của các node BorderHighlight được chọn.
        /// </summary>
        private async Task DisableSelectedNodesAsync(BorderHighlightNode currentNode, NodeExecutionEnvironment env)
        {
            List<string>? nodeIdsToDisable = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(currentNode.NodesToDisableJson))
                {
                    nodeIdsToDisable = JsonSerializer.Deserialize<List<string>>(currentNode.NodesToDisableJson);
                }
            }
            catch { }

            if (nodeIdsToDisable == null || nodeIdsToDisable.Count == 0)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            await dispatcher.InvokeAsync(() =>
            {
                foreach (var nodeId in nodeIdsToDisable)
                {
                    if (_activeOverlays.TryGetValue(nodeId, out var overlay))
                    {
                        try
                        {
                            overlay.StopAnimation();
                            overlay.Close();
                            _activeOverlays.Remove(nodeId);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Failed to disable overlay for node {nodeId}: {ex}");
                        }
                    }
                }
            }, DispatcherPriority.Normal);
        }

        /// <summary>
        /// Tắt overlay của một node cụ thể (được gọi bởi executor khác).
        /// </summary>
        public static void DisableOverlay(string nodeId)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.Invoke(() =>
            {
                if (_activeOverlays.TryGetValue(nodeId, out var overlay))
                {
                    try
                    {
                        overlay.StopAnimation();
                        overlay.Close();
                        _activeOverlays.Remove(nodeId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Failed to disable overlay for node {nodeId}: {ex}");
                    }
                }
            });
        }

        /// <summary>
        /// Cleanup khi workflow dừng.
        /// </summary>
        public static void CleanupAll()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.Invoke(() =>
            {
                foreach (var overlay in _activeOverlays.Values)
                {
                    try
                    {
                        overlay.StopAnimation();
                        overlay.Close();
                    }
                    catch { }
                }
                _activeOverlays.Clear();
            });
        }
    }
}
