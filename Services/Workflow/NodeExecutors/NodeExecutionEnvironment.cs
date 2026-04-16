using FlowMy.Models;
using FlowMy.Models.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Gói toàn bộ context khi thực thi 1 node (connections, token, callback, v.v.).
    /// Các executor dùng env để:
    /// - gọi sang node tiếp theo qua ExecuteNextAsync()
    /// - truy cập helper/ dịch vụ thông qua Service.
    /// </summary>
    public sealed class NodeExecutionEnvironment
    {
        public WorkflowExecutionService Service { get; }
        public List<WorkflowConnection> Connections { get; }
        public CancellationToken CancellationToken { get; }
        public Action<WorkflowConnection?>? OnEnteringNode { get; }
        public Action<WorkflowNode, WorkflowConnection?>? OnNodeStarted { get; }
        public Action<WorkflowNode, TimeSpan>? OnNodeCompleted { get; }
        public Action<WorkflowNode, string>? OnNodeFailed { get; }
        public WorkflowConnection? IncomingConnection { get; }
        public HashSet<WorkflowNode> ReachableToEnd { get; }

        /// <summary>
        /// Id duy nhất cho một lần chạy (một lượt từ entry Start / chạy từ node / lane AutoScheduled).
        /// Mọi bước trong cùng luồng dùng chung Id này (kể cả Callback đã truyền tiếp từ env).
        /// Hai lần chạy đồng thời trên cùng graph → hai ExecutionId khác nhau; output Code JS được lưu theo Id để downstream trong luồng đó đọc đúng bản ghi.
        /// </summary>
        public string ExecutionId { get; }
        public string FlowScopeId { get; }
        public string BranchId { get; }
        public string? ParentFlowScopeId { get; }

        /// <summary>
        /// Khi true: ExecuteNextAsync không chạy node tiếp theo (chỉ chạy logic node hiện tại, dùng cho refresh nguồn).
        /// </summary>
        public bool RefreshOnly { get; }

        /// <summary>
        /// DEPRECATED: This flag is no longer used for blocking traversal.
        /// Loop detection is now handled via ExecutionPath tracking.
        /// Kept for backward compatibility but always treated as false.
        /// </summary>
        public bool IsReuseRouteTerminal { get; }

        /// <summary>
        /// Tracks the execution path to detect infinite loops.
        /// Each entry is a node ID that has been executed in this workflow run.
        /// </summary>
        public List<string> ExecutionPath { get; }

        /// <summary>
        /// Maximum number of nodes that can be executed before considering it an infinite loop.
        /// </summary>
        private const int MAX_EXECUTION_DEPTH = 100;

        public NodeExecutionEnvironment(
            WorkflowExecutionService service,
            List<WorkflowConnection> connections,
            CancellationToken cancellationToken,
            Action<WorkflowConnection?>? onEnteringNode,
            Action<WorkflowNode, WorkflowConnection?>? onNodeStarted,
            Action<WorkflowNode, TimeSpan>? onNodeCompleted,
            Action<WorkflowNode, string>? onNodeFailed,
            WorkflowConnection? incomingConnection,
            HashSet<WorkflowNode> reachableToEnd,
            bool refreshOnly = false,
            bool isReuseRouteTerminal = false,
            List<string>? executionPath = null,
            string? executionId = null,
            string? flowScopeId = null,
            string? branchId = null,
            string? parentFlowScopeId = null)
        {
            Service = service ?? throw new ArgumentNullException(nameof(service));
            Connections = connections ?? throw new ArgumentNullException(nameof(connections));
            CancellationToken = cancellationToken;
            OnEnteringNode = onEnteringNode;
            OnNodeStarted = onNodeStarted;
            OnNodeCompleted = onNodeCompleted;
            OnNodeFailed = onNodeFailed;
            IncomingConnection = incomingConnection;
            ReachableToEnd = reachableToEnd ?? throw new ArgumentNullException(nameof(reachableToEnd));
            RefreshOnly = refreshOnly;
            IsReuseRouteTerminal = isReuseRouteTerminal;
            ExecutionPath = executionPath ?? new List<string>();
            ExecutionId = string.IsNullOrWhiteSpace(executionId)
                ? Guid.NewGuid().ToString("N")
                : executionId;
            FlowScopeId = string.IsNullOrWhiteSpace(flowScopeId)
                ? "main"
                : flowScopeId;
            BranchId = string.IsNullOrWhiteSpace(branchId)
                ? "main"
                : branchId;
            ParentFlowScopeId = parentFlowScopeId;
        }

        /// <summary>
        /// Tạo env mới với IncomingConnection khác (dùng khi nhảy sang node tiếp theo).
        /// </summary>
        public NodeExecutionEnvironment WithIncoming(WorkflowConnection? incoming)
        {
            return new NodeExecutionEnvironment(
                service: Service,
                connections: Connections,
                cancellationToken: CancellationToken,
                onEnteringNode: OnEnteringNode,
                onNodeStarted: OnNodeStarted,
                onNodeCompleted: OnNodeCompleted,
                onNodeFailed: OnNodeFailed,
                incomingConnection: incoming,
                reachableToEnd: ReachableToEnd,
                refreshOnly: RefreshOnly,
                isReuseRouteTerminal: false,
                executionPath: ExecutionPath,
                executionId: ExecutionId,
                flowScopeId: FlowScopeId,
                branchId: BranchId,
                parentFlowScopeId: ParentFlowScopeId);
        }

        public NodeExecutionEnvironment ForkForBranch(string? branchName, string? flowScopeIdOverride = null, string? parentFlowScopeIdOverride = null)
        {
            var resolvedBranch = string.IsNullOrWhiteSpace(branchName)
                ? Guid.NewGuid().ToString("N")
                : branchName;
            var resolvedScope = string.IsNullOrWhiteSpace(flowScopeIdOverride) ? FlowScopeId : flowScopeIdOverride;
            var resolvedParent = parentFlowScopeIdOverride ?? ParentFlowScopeId;

            return new NodeExecutionEnvironment(
                service: Service,
                connections: Connections,
                cancellationToken: CancellationToken,
                onEnteringNode: OnEnteringNode,
                onNodeStarted: OnNodeStarted,
                onNodeCompleted: OnNodeCompleted,
                onNodeFailed: OnNodeFailed,
                incomingConnection: IncomingConnection,
                reachableToEnd: ReachableToEnd,
                refreshOnly: RefreshOnly,
                isReuseRouteTerminal: false,
                executionPath: new List<string>(ExecutionPath),
                executionId: ExecutionId,
                flowScopeId: resolvedScope,
                branchId: resolvedBranch,
                parentFlowScopeId: resolvedParent);
        }

        /// <summary>
        /// Thực thi node tiếp theo, bảo toàn toàn bộ context còn lại.
        /// Khi RefreshOnly = true thì không chạy tiếp (dùng cho refresh nguồn trong AssignData).
        /// <param name="isReuseRouteTerminal">DEPRECATED: No longer used. Loop detection is handled via ExecutionPath.</param>
        /// </summary>
        public Task ExecuteNextAsync(WorkflowNode node, WorkflowConnection? viaConnection, bool isReuseRouteTerminal = false)
        {
            if (RefreshOnly)
                return Task.CompletedTask;
            
            // Pass a COPY of execution path to avoid polluting the current path
            // when parallel branches are executed
            return Service.ExecuteNodeAsync(
                node,
                Connections,
                CancellationToken,
                OnEnteringNode,
                OnNodeStarted,
                OnNodeCompleted,
                OnNodeFailed,
                viaConnection,
                ReachableToEnd,
                false, // isReuseRouteTerminal is deprecated
                new List<string>(ExecutionPath), // Pass copy of execution path
                ExecutionId,
                FlowScopeId,
                BranchId,
                ParentFlowScopeId);
        }

        /// <summary>
        /// Centralized helper: traverse output connections of a node with full ReuseRoute routing support.
        /// Handles: IsReuseRouteTerminal check, ReuseRoute routing (filter to configured outgoing node),
        /// port-based connections, legacy connections (FromPort == null), and LoopBody return connections.
        /// 
        /// All executors should call this instead of duplicating the output traversal logic.
        /// </summary>
        public async Task TraverseOutputsAsync(WorkflowNode node)
        {
            // ── Pre-traverse: mirror outputs to scoped store NGAY LÚC NÀY ──
            // Trong parallel mode (AsyncTask), nhiều iteration dùng chung node object.
            // Nếu chờ đến ExecuteNodeAsync.finally mới mirror, iteration khác có thể đã overwrite
            // shared state (OutputText, ResolvedOutputs). Nên mirror TRƯỚC khi traverse downstream.
            if (!RefreshOnly && !string.IsNullOrWhiteSpace(ExecutionId))
            {
                try { Service.PreTraverseMirrorToScopedStore(node, ExecutionId); }
                catch { /* best-effort */ }
            }

            if (node.Type == NodeType.End &&
                (node.EndBehavior == EndNodeBehavior.StopCurrentFlow || node.EndBehavior == EndNodeBehavior.EmitResultOnly))
            {
                return;
            }

            // System.Diagnostics.Debug.WriteLine($"[TraverseOutputs] Node: {node.Id} ({node.Title})");
            
            // Add current node to execution path for loop detection
            ExecutionPath.Add(node.Id);

            // Check for infinite loop
            if (ExecutionPath.Count > MAX_EXECUTION_DEPTH)
            {
                var recentPath = string.Join(" → ", ExecutionPath.TakeLast(10).Select(id => 
                {
                    var n = Connections.SelectMany(c => new[] { c.FromNode, c.ToNode })
                                      .FirstOrDefault(x => x?.Id == id);
                    return n != null ? $"{n.Title} ({id.Substring(0, 8)}...)" : id;
                }));

                var errorMsg = $"⚠️ Infinite loop detected!\n\n" +
                              $"Execution has passed through {ExecutionPath.Count} nodes.\n" +
                              $"Recent path (last 10 nodes):\n{recentPath}\n\n" +
                              $"Please check your workflow connections and ReuseRoutes configuration.";
                
                // System.Diagnostics.Debug.WriteLine($"[TraverseOutputs] ❌ LOOP DETECTED! Path count: {ExecutionPath.Count}");
                // System.Diagnostics.Debug.WriteLine($"[TraverseOutputs] Recent path: {recentPath}");
                
                OnNodeFailed?.Invoke(node, errorMsg);
                return;
            }


            var connections = Connections;

            // Tìm output port chính
            var outputPort = node.Ports?.FirstOrDefault(p => !p.IsInput && p.IsVisible);
            // System.Diagnostics.Debug.WriteLine($"[TraverseOutputs] Output port found: {outputPort != null}, Service: {Service != null}");
            
            var traversedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            static string BuildTraverseKey(WorkflowConnection c)
            {
                var fromId = c.FromNode?.Id ?? string.Empty;
                var toId = c.ToNode?.Id ?? string.Empty;
                var toPortId = c.ToPort?.Id ?? string.Empty;
                // Ignore FromPort so normal + legacy (FromPort=null) copies of same edge are executed once.
                return $"{fromId}|{toId}|{toPortId}";
            }

            if (outputPort != null && Service != null)
            {
                var baseNextConnections = Service.GetConnectionsFromPort(outputPort, node, connections)
                                                 .ToList();
                // System.Diagnostics.Debug.WriteLine($"[TraverseOutputs] Found {baseNextConnections.Count} connections from output port");

                // Kiểm tra ReuseRoutes: nếu có cấu hình "tái sử dụng flow" thì ưu tiên route theo incoming node
                var incomingFromNode = IncomingConnection?.FromNode;
                if (incomingFromNode != null && node.ReuseRoutes != null && node.ReuseRoutes.Count > 0)
                {
                    var route = node.ReuseRoutes
                        .FirstOrDefault(r =>
                            !string.IsNullOrWhiteSpace(r.IncomingNodeId) &&
                            !string.IsNullOrWhiteSpace(r.OutgoingNodeId) &&
                            string.Equals(r.IncomingNodeId, incomingFromNode.Id, StringComparison.OrdinalIgnoreCase));

                    if (route != null)
                    {
                        var filtered = baseNextConnections
                            .Where(c => c.ToNode != null &&
                                        string.Equals(c.ToNode.Id, route.OutgoingNodeId, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        // Nếu filter ra được connection hợp lệ thì dùng route này.
                        // Loop detection is now handled via ExecutionPath tracking.
                        if (filtered.Count > 0)
                        {
                            foreach (var conn in filtered)
                            {
                                if (conn.ToNode != null)
                                {
                                    if (!traversedKeys.Add(BuildTraverseKey(conn)))
                                        continue;
                                    if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                                    {
                                        Service.SignalLoopBodyReturn(conn, ExecutionId, BranchId);
                                        continue;
                                    }
                                    await ExecuteNextAsync(conn.ToNode, conn).ConfigureAwait(false);
                                }
                            }
                            // System.Diagnostics.Debug.WriteLine($"[TraverseOutputs] ✓ Executed ReuseRoute connections, stopping normal traversal");
                            return; // Đã xử lý ReuseRoute, không chạy block bên dưới
                        }
                        else
                        {
                            // System.Diagnostics.Debug.WriteLine($"[TraverseOutputs] ReuseRoute filter returned 0 connections");
                        }
                    }
                }

                // Không có ReuseRoute match → đi tiếp tất cả connections như bình thường
                // ⚠️ ƯU TIÊN: luôn chạy StorageNode trước các node khác khi cùng xuất phát từ 1 node nguồn.
                var storageFirst = baseNextConnections
                    .Where(c => c.ToNode is StorageNode)
                    .ToList();
                var nonStorage = baseNextConnections
                    .Where(c => c.ToNode is not StorageNode)
                    .ToList();

                var orderedConnections = storageFirst.Concat(nonStorage).ToList();

                // System.Diagnostics.Debug.WriteLine($"[TraverseOutputs] Traversing {orderedConnections.Count} normal connections (Storage first)...");
                foreach (var conn in orderedConnections)
                {
                    if (conn.ToNode != null)
                    {
                        if (!traversedKeys.Add(BuildTraverseKey(conn)))
                            continue;
                        if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                        {
                            Service.SignalLoopBodyReturn(conn, ExecutionId, BranchId);
                            continue;
                        }
                        // System.Diagnostics.Debug.WriteLine($"[TraverseOutputs] → Executing next node: {conn.ToNode.Id} ({conn.ToNode.Title})");
                        await ExecuteNextAsync(conn.ToNode, conn).ConfigureAwait(false);
                    }
                }
            }

            // Backward compatibility: connections chưa có FromPort/ToPort
            var legacyNext = connections
                .Where(c => c.FromNode == node && c.FromPort == null)
                .ToList();

            // ⚠️ Với legacy connections (không có FromPort), cũng ưu tiên StorageNode trước.
            var legacyStorageFirst = legacyNext
                .Where(c => c.ToNode is StorageNode)
                .ToList();
            var legacyNonStorage = legacyNext
                .Where(c => c.ToNode is not StorageNode)
                .ToList();
            var orderedLegacy = legacyStorageFirst.Concat(legacyNonStorage).ToList();

            foreach (var conn in orderedLegacy)
            {
                if (conn.ToNode != null)
                {
                    if (!traversedKeys.Add(BuildTraverseKey(conn)))
                        continue;
                    if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                    {
                        Service.SignalLoopBodyReturn(conn, ExecutionId, BranchId);
                        continue;
                    }
                    await ExecuteNextAsync(conn.ToNode, conn).ConfigureAwait(false);
                }
            }
        }
    }
}


