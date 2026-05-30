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
                        
                        // Chuẩn bị target mode trước khi show (tránh maximize fullscreen)
                        if (borderNode.HighlightMode == BorderHighlightMode.TargetApp)
                            overlay.PrepareForTargetMode();
                        
                        overlay.Configure(
                            borderNode.BorderColorHex,
                            borderNode.BorderThickness,
                            borderNode.GradientSize,
                            borderNode.Opacity,
                            borderNode.EffectType
                        );
                        loadedTask = overlay.WhenLoaded;
                        
                        // Trong TargetApp mode, không show ngay - chỉ show khi target app active
                        if (borderNode.HighlightMode != BorderHighlightMode.TargetApp)
                        {
                            overlay.Show();
                            overlay.Activate();
                        }
                        else
                        {
                            // Show nhưng ẩn ngay lập tức, timer sẽ show khi target app active
                            overlay.Show();
                            overlay.Hide();
                        }
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
                    // Tìm target window bằng ProcessId (ưu tiên) hoặc ProcessName/Title (fallback)
                    IntPtr foundHwnd = IntPtr.Zero;
                    
                    // Ưu tiên dùng ProcessId nếu có
                    if (borderNode.TargetProcessId != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Using ProcessId: {borderNode.TargetProcessId}");
                        var windows = WindowHelper.GetActiveWindows();
                        var match = windows.FirstOrDefault(w => w.ProcessId == borderNode.TargetProcessId);
                        if (match != null)
                        {
                            foundHwnd = match.Handle;
                            System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Found window by ProcessId: ProcessName={match.ProcessName}, Title={match.Title}, Handle={match.Handle}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] No window found with ProcessId={borderNode.TargetProcessId}");
                        }
                    }
                    
                    // Fallback: tìm theo ProcessName/Title
                    if (foundHwnd == IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] ProcessId not available or not found, using fallback");
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Looking for: ProcessName={borderNode.TargetProcessName}, Title={borderNode.TargetWindowTitle}");
                        
                        var windows = WindowHelper.GetActiveWindows();
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Found {windows.Count} active windows");
                        
                        foreach (var w in windows.Take(10))
                        {
                            System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor]   - {w.ProcessName} | {w.Title}");
                        }
                        
                        var match = windows.FirstOrDefault(w =>
                            w.ProcessName == borderNode.TargetProcessName &&
                            w.Title == borderNode.TargetWindowTitle);

                        if (match == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Exact match not found, trying ProcessName only");
                            match = windows.FirstOrDefault(w => w.ProcessName == borderNode.TargetProcessName);
                        }

                        if (match != null)
                        {
                            foundHwnd = match.Handle;
                            System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Found match: ProcessName={match.ProcessName}, Title={match.Title}, Handle={match.Handle}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] No match found for ProcessName={borderNode.TargetProcessName}");
                        }
                    }
                    
                    targetHwnd = foundHwnd;

                    if (targetHwnd != IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Target window handle: {targetHwnd}");
                        await dispatcher.InvokeAsync(() =>
                        {
                            // PositionOverTarget sẽ start timer và set target handle
                            overlay.PositionOverTarget(targetHwnd);
                            // Sau đó mới ẩn - timer sẽ show khi target app active
                            overlay.Hide();
                            System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Overlay hidden, timer should show when target app is active");
                        }, DispatcherPriority.Normal);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlightExecutor] Target window handle is ZERO - cannot position overlay");
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
