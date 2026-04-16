using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Windows;
using System.Windows.Threading;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class LoopNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is LoopNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var loopNode = (LoopNode)node;
            var connections = env.Connections;
            var cancellationToken = env.CancellationToken;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // ✅ Start timing UI riêng cho LoopNode (chạy song song với loop body)
            // Không dùng global timer của Visualizer vì nó bị override bởi nodes trong body
            DispatcherTimer? loopTimingTimer = null;
            try
            {
                StartLoopNodeTiming(loopNode, sw, out loopTimingTimer);
            }
            catch { /* Ignore UI errors */ }

            // Ensure LoopBody ports exist (import/legacy case)
            WorkflowExecutionService.EnsureLoopBodyPortsExist(loopNode.LoopBodyNode);

            // ✅ Default wire: LoopNodeBottom -> LoopBodyTop (UI đã auto-wire, nhưng runtime cần chắc chắn có)
            var loopBottomPort = loopNode.Ports.FirstOrDefault(p => p.Id == "LoopNodeBottom");
            var bodyTopPort = loopNode.LoopBodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyTop");
            var bodyLeftPort = loopNode.LoopBodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyLeft");
            var loopOutPort = loopNode.Ports.FirstOrDefault(p => p.Id == "LoopNodeOut");

            WorkflowConnection? loopToBodyConn = null;
            if (loopBottomPort != null && bodyTopPort != null)
            {
                loopToBodyConn = connections.FirstOrDefault(c =>
                    c.FromNode == loopNode &&
                    c.ToNode == loopNode.LoopBodyNode &&
                    (ReferenceEquals(c.FromPort, loopBottomPort) ||
                     (c.FromPort != null && string.Equals(c.FromPort.Id, loopBottomPort.Id, StringComparison.OrdinalIgnoreCase))) &&
                    (ReferenceEquals(c.ToPort, bodyTopPort) ||
                     (c.ToPort != null && string.Equals(c.ToPort.Id, bodyTopPort.Id, StringComparison.OrdinalIgnoreCase))));

                // Nếu chưa có trong list (chưa render UI / chưa save) thì tạo runtime-connection để execution chạy đúng.
                if (loopToBodyConn == null)
                {
                    loopToBodyConn = new WorkflowConnection
                    {
                        FromNode = loopNode,
                        ToNode = loopNode.LoopBodyNode,
                        FromPort = loopBottomPort,
                        ToPort = bodyTopPort,
                        IsDeleteVisible = false
                    };
                    connections.Add(loopToBodyConn);
                }

                // Sync lại DefaultConnection trên LoopNode nếu cần (để UI có thể reuse khi đang chạy)
                if (loopNode.DefaultConnection == null)
                {
                    loopNode.DefaultConnection = loopToBodyConn;
                }
            }

            // ✅ Cache ListOutNodes trong LoopBody để resolve value nhanh hơn
            // Và sync outputs từ ListOutNodes sang LoopNode
            CacheAndSyncListOutNodes(loopNode, connections);

            try
            {
                foreach (var (index, item) in env.Service.ResolveLoopIterations(loopNode, connections, env))
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Expose index/item for nodes inside body (DynamicInputs resolve via UserValueOverride)
                    WorkflowExecutionService.SetLoopRuntimeOutputs(loopNode, index, item);

                    // ✅ Gán dữ liệu trong vòng lặp: từ (SourceNodeId, SourceOutputKey) sang (TargetNodeId, TargetKey)
                    foreach (var a in loopNode.DataAssignments)
                    {
                        if (string.IsNullOrWhiteSpace(a.SourceNodeId) || string.IsNullOrWhiteSpace(a.TargetNodeId) || string.IsNullOrWhiteSpace(a.TargetKey)) continue;
                        var value = env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, a.SourceNodeId, a.SourceOutputKey, env);
                        var targetNode = connections
                            .SelectMany(c => new[] { c.FromNode, c.ToNode })
                            .FirstOrDefault(n => n != null && string.Equals(n.Id, a.TargetNodeId, StringComparison.OrdinalIgnoreCase));
                        if (targetNode != null)
                        {
                            if (targetNode is StorageNode st)
                            {
                                st.SetStoredOutput(a.TargetKey, value ?? string.Empty);
                                env.Service.PublishStorageOutputsToScoped(st, env.ExecutionId);
                            }
                            else
                                WorkflowExecutionService.SetDynamicValueByKey(targetNode, a.TargetKey, value ?? string.Empty);
                        }
                    }

                    try
                    {
                        // ✅ Truyền năng lượng từ LoopNodeBottom -> LoopBodyTop, rồi để LoopBody tự chạy (entry từ LoopBodyLeft/Top)
                        if (loopToBodyConn != null)
                        {
                            // ✅ Pin energy on Loop -> LoopBody while the body is running
                            loopToBodyConn.IsExecutionPinned = true;

                            using var iterationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                            // Waiter will cancel iterationCts when return is reached (hard stop)
                            var iterationBranchScope = $"{env.BranchId}:loop-{index}";
                            var (returnTask, cleanup) = env.Service.BeginAwaitLoopBodyReturn(
                                loopNode.LoopBodyNode,
                                env.ExecutionId,
                                cancellationToken,
                                hardStopCts: iterationCts,
                                branchScope: iterationBranchScope);

                            try
                            {
                                // Tìm các entry connections cho LoopBody: ưu tiên từ LoopBodyLeft, nếu không có thì dùng LoopBodyTop.
                                var entryConnections = new List<WorkflowConnection>();
                                if (bodyLeftPort != null)
                                {
                                    entryConnections = env.Service.GetConnectionsFromPortIncludingLegacy(
                                        bodyLeftPort, loopNode.LoopBodyNode, connections);
                                }
                                if (entryConnections.Count == 0 && bodyTopPort != null)
                                {
                                    entryConnections = env.Service.GetConnectionsFromPortIncludingLegacy(
                                        bodyTopPort, loopNode.LoopBodyNode, connections);
                                }

                                // Nếu không có entry nào, coi như body không làm gì trong iteration này.
                                if (entryConnections.Count > 0)
                                {
                                    // IMPORTANT: LoopBody graph thường không dẫn tới End, nên tắt reachable-to-End pruning cho body.
                                    var noPrune = new HashSet<WorkflowNode>();
                                    var tasks = new List<Task>();

                                    foreach (var conn in entryConnections)
                                    {
                                        if (conn.ToNode != null)
                                        {
                                            tasks.Add(env.Service.ExecuteNodeAsync(
                                                conn.ToNode,
                                                connections,
                                                iterationCts.Token,
                                                env.OnEnteringNode,
                                                env.OnNodeStarted,
                                                env.OnNodeCompleted,
                                                env.OnNodeFailed,
                                                conn,
                                                noPrune,
                                                executionId: env.ExecutionId,
                                                flowScopeId: env.FlowScopeId,
                                                branchId: iterationBranchScope,
                                                parentFlowScopeId: env.ParentFlowScopeId));
                                        }
                                    }

                                    try
                                    {
                                        await Task.WhenAll(tasks);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        // If user cancelled outer token -> propagate
                                        if (cancellationToken.IsCancellationRequested) throw;
                                        // If hard-stop cancel came from LoopBodyRight -> treat as normal completion
                                        if (!returnTask.IsCompletedSuccessfully) throw;
                                    }
                                }
                            }
                            finally
                            {
                                cleanup.Dispose();
                                // Unpin after body finishes (or errors)
                                loopToBodyConn.IsExecutionPinned = false;
                                // Ensure it is turned off visually even if ActiveExecutionConnection doesn't change afterwards
                                loopToBodyConn.IsExecutionActive = false;
                                env.OnEnteringNode?.Invoke(null);
                            }

                            // ✅ Bắt buộc phải có đường return về LoopBodyRight mới coi là body xong.
                            if (!returnTask.IsCompletedSuccessfully)
                            {
                                throw new InvalidOperationException(
                                    "Node body chưa có đường return về port 'Port Body Right'. " +
                                    "Hãy nối node kết thúc trong body vào 'Port Body Right' để kết thúc iteration.");
                            }

                            // ✅ Cập nhật custom output overrides từ body nodes (output có SelectedSourceNodeId)
                            if (loopNode.DynamicOutputs != null)
                            {
                                foreach (var o in loopNode.DynamicOutputs.Where(o => !string.IsNullOrWhiteSpace(o.SelectedSourceNodeId)))
                                {
                                    var val = env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, o.SelectedSourceNodeId, o.SelectedSourceOutputKey, env);
                                    o.UserValueOverride = val ?? string.Empty;
                                }
                            }
                        }
                    }
                    catch (WorkflowExecutionService.LoopContinueException)
                    {
                        continue; // skip iteration
                    }
                    catch (WorkflowExecutionService.LoopBreakException)
                    {
                        break; // exit loop
                    }
                }
            }
            finally
            {
                sw.Stop();
                
                // ✅ Stop timing UI riêng của LoopNode và hiển thị tổng thời gian (luôn chạy dù có exception)
                try { StopLoopNodeTiming(loopNode, sw.Elapsed, loopTimingTimer); } catch { /* Ignore UI errors */ }
                
                // ✅ Gọi OnNodeCompleted để trigger UpdateNodeExecutionResults và hiển thị toggle results
                try { env.OnNodeCompleted?.Invoke(loopNode, sw.Elapsed); } catch { /* Ignore UI errors */ }
            }

            // After loop finishes, continue via LoopNodeOut
            if (loopOutPort != null && !cancellationToken.IsCancellationRequested)
            {
                var nextConnections = env.Service.GetConnectionsFromPortIncludingLegacy(loopOutPort, loopNode, connections);
                foreach (var conn in nextConnections)
                {
                    if (conn.ToNode != null)
                    {
                        // ✅ Check if this is a return connection to parent LoopBody (nested loop case)
                        // When nested loop E finishes and connects to LoopBodyRight of parent loop B,
                        // we should signal return instead of executing LoopBodyNode B
                        if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                        {
                            env.Service.SignalLoopBodyReturn(conn, env.ExecutionId, env.BranchId);
                            continue;
                        }
                        await env.ExecuteNextAsync(conn.ToNode, conn);
                    }
                }
            }
        }

        /// <summary>
        /// Start timing UI riêng cho LoopNode (chạy song song với loop body).
        /// Timer này không bị ảnh hưởng bởi nodes trong body.
        /// </summary>
        private static void StartLoopNodeTiming(LoopNode loopNode, System.Diagnostics.Stopwatch sw, out DispatcherTimer? timer)
        {
            timer = null;
            
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                // Hiển thị container
                if (loopNode.ExecutionStatusContainerUI != null)
                    loopNode.ExecutionStatusContainerUI.Visibility = Visibility.Visible;

                // Set initial text
                if (loopNode.ExecutionStatusTextUI != null)
                    loopNode.ExecutionStatusTextUI.Text = $"⏳ 0.00s{BuildFlowBadge(loopNode)}";
            }));

            // Tạo timer riêng cho LoopNode
            DispatcherTimer? localTimer = null;
            dispatcher.Invoke(() =>
            {
                localTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(80)
                };
                localTimer.Tick += (_, __) =>
                {
                    if (loopNode.ExecutionStatusTextUI != null)
                    {
                        var sec = sw.Elapsed.TotalSeconds;
                        loopNode.ExecutionStatusTextUI.Text = $"⏳ {sec:0.00}s{BuildFlowBadge(loopNode)}";
                    }
                };
                localTimer.Start();
            });
            
            timer = localTimer;
        }

        /// <summary>
        /// Stop timing UI của LoopNode và hiển thị tổng thời gian.
        /// </summary>
        private static void StopLoopNodeTiming(LoopNode loopNode, TimeSpan elapsed, DispatcherTimer? timer)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                // Stop timer
                timer?.Stop();

                // Hiển thị kết quả
                if (loopNode.ExecutionStatusContainerUI != null)
                    loopNode.ExecutionStatusContainerUI.Visibility = Visibility.Visible;
                    
                if (loopNode.ExecutionStatusTextUI != null)
                    loopNode.ExecutionStatusTextUI.Text = $"✅ {elapsed.TotalSeconds:0.00}s{BuildFlowBadge(loopNode)}";
            }));
        }

        private static string BuildFlowBadge(WorkflowNode node)
        {
            var scope = node.LastFlowScopeId;
            var branch = node.LastBranchId;
            if (string.IsNullOrWhiteSpace(scope) && string.IsNullOrWhiteSpace(branch))
            {
                return string.Empty;
            }

            return $" [{scope ?? "-"}|{branch ?? "-"}]";
        }

        /// <summary>
        /// Cache ListOutNodes trong LoopBody và sync outputs từ ListOutNodes sang LoopNode.
        /// </summary>
        private static void CacheAndSyncListOutNodes(LoopNode loopNode, List<WorkflowConnection> connections)
        {
            var body = loopNode.LoopBodyNode;
            if (body == null) return;

            // Tìm tất cả nodes trong LoopBody cluster
            var visited = new HashSet<WorkflowNode> { body };
            var queue = new Queue<WorkflowNode>();
            queue.Enqueue(body);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                var neighbors = connections
                    .Where(c => c.FromNode == current || c.ToNode == current)
                    .Select(c => c.FromNode == current ? c.ToNode : c.FromNode)
                    .Where(n => n != null);

                foreach (var neighbor in neighbors)
                {
                    // Bỏ qua LoopNode cha để không lan ra ngoài qua default connection
                    if (ReferenceEquals(neighbor, loopNode)) continue;

                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Loại bỏ chính LoopBodyNode
            visited.Remove(body);

            // Tìm tất cả ListOutNodes trong LoopBody và cache
            var listOutNodes = visited.OfType<ListOutNode>().ToList();
            loopNode.CachedListOutNodes = listOutNodes;

            // Sync outputs từ ListOutNodes sang LoopNode (nếu chưa có)
            var defaultOutputKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "index", "item" };
            
            foreach (var listOutNode in listOutNodes)
            {
                if (listOutNode.DynamicOutputs == null) continue;

                foreach (var output in listOutNode.DynamicOutputs)
                {
                    // Kiểm tra xem LoopNode đã có output key này chưa
                    var existingOutput = loopNode.DynamicOutputs.FirstOrDefault(o =>
                        string.Equals(o.Key, output.Key, StringComparison.OrdinalIgnoreCase));

                    if (existingOutput == null && !defaultOutputKeys.Contains(output.Key))
                    {
                        // Thêm output mới từ ListOutNode
                        var outputType = output.OutputType ?? output.ConvertType;
                        loopNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                        {
                            Key = output.Key,
                            DisplayName = output.DisplayName ?? output.Key,
                            OutputType = outputType,
                            ConvertType = outputType,
                            IsMultiple = output.IsMultiple,
                            IsUserAdded = true
                        });
                    }
                }
            }
        }
    }
}


