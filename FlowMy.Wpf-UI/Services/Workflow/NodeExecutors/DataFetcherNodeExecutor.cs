// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho DataFetcherNode.
    /// - Khi SourceOutputKey có giá trị: lấy 1 key cụ thể, gán vào output port cùng tên.
    /// - Khi SourceOutputKey null/trống: copy TOÀN BỘ DynamicOutputs từ node nguồn.
    /// - Nếu node nguồn là WebNode và WaitForWebNodeLoad = true: chờ PendingOutputsTcs.
    /// </summary>
    internal sealed class DataFetcherNodeExecutor : INodeExecutor
    {
        /// <summary>
        /// Fired whenever any node completes execution (via NodeExecutionEnvironment.TraverseOutputsAsync).
        /// DataFetcherNodeControl subscribes to this for Realtime mode:
        /// khi source node hoàn thành → DataFetcher tự động lấy giá trị mới.
        /// </summary>
        public static event Action<WorkflowNode>? AnyNodeCompleted;

        /// <summary>Called by NodeExecutionEnvironment after each node executes its outputs traversal.</summary>
        public static void NotifyNodeCompleted(WorkflowNode node)
            => AnyNodeCompleted?.Invoke(node);

        public bool CanExecute(WorkflowNode node) => node is DataFetcherNode;


        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var fetcherNode = (DataFetcherNode)node;
            var sw = Stopwatch.StartNew();

            try
            {
                await FetchValueAsync(fetcherNode, env).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(node, ex.Message);
                throw;
            }
            finally
            {
                sw.Stop();
                env.OnNodeCompleted?.Invoke(node, sw.Elapsed);
            }

            await env.TraverseOutputsAsync(fetcherNode);
        }

        /// <summary>
        /// Lấy dữ liệu từ node nguồn và gán vào DynamicOutputs của DataFetcherNode.
        /// </summary>
        internal static async Task FetchValueAsync(DataFetcherNode fetcherNode, NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(fetcherNode.SourceNodeId))
            {
                Debug.WriteLine("[DataFetcherNodeExecutor] SourceNodeId chưa cấu hình → bỏ qua.");
                return;
            }

            Debug.WriteLine($"[DataFetcherNodeExecutor] FetchValueAsync: SourceNodeId='{fetcherNode.SourceNodeId}', " +
                $"ReachableToEnd.Count={env.ReachableToEnd?.Count ?? 0}, " +
                $"Connections.Count={env.Connections?.Count ?? 0}, " +
                $"RefreshOnly={env.RefreshOnly}, ExecutionId={env.ExecutionId}");

            var sourceNode = FindSourceNode(fetcherNode.SourceNodeId, env);
            if (sourceNode == null)
            {
                Debug.WriteLine($"[DataFetcherNodeExecutor] ❌ Không tìm thấy node nguồn: {fetcherNode.SourceNodeId}");
                return;
            }

            Debug.WriteLine($"[DataFetcherNodeExecutor] ✅ Tìm thấy source node: '{sourceNode.Title}' (Id={sourceNode.Id}), " +
                $"DynamicOutputs.Count={sourceNode.DynamicOutputs?.Count ?? 0}");

            // Nếu nguồn là WebNode và cần chờ load xong
            if (fetcherNode.WaitForWebNodeLoad && sourceNode is WebNode webNode)
                await WaitForWebNodeLoadAsync(webNode, env.CancellationToken).ConfigureAwait(false);

            // ── RunSourceNodeFirst: chạy lại node nguồn (như nhấn ▶) để lấy dữ liệu mới nhất ──
            if (fetcherNode.RunSourceNodeFirst)
            {
                try
                {
                    Debug.WriteLine($"[DataFetcherNodeExecutor] RunSourceNodeFirst → chạy lại '{sourceNode.Title}'");

                    // Tập hợp tất cả nodes từ connections để làm allNodesForLookup
                    var allNodes = env.Connections
                        .SelectMany(c => new WorkflowNode?[] { c.FromNode, c.ToNode })
                        .OfType<WorkflowNode>()
                        .Distinct()
                        .ToList();

                    // Source node có thể không nằm trong connections (standalone node)
                    // → thêm vào allNodes để ExecuteNodeLogicOnlyAsync tìm được
                    if (!allNodes.Any(n => string.Equals(n.Id, sourceNode.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        allNodes.Add(sourceNode);
                        Debug.WriteLine($"[DataFetcherNodeExecutor] RunSourceNodeFirst: Thêm standalone source '{sourceNode.Title}' vào allNodes");
                    }

                    // Chạy logic node nguồn (không traverse output, không ảnh hưởng flow)
                    await env.Service.ExecuteNodeLogicOnlyAsync(
                        sourceNode,
                        env.Connections,
                        env.CancellationToken,
                        allNodesForLookup: allNodes)
                        .ConfigureAwait(false);

                    Debug.WriteLine($"[DataFetcherNodeExecutor] RunSourceNodeFirst xong → tiếp tục lấy data");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DataFetcherNodeExecutor] RunSourceNodeFirst lỗi: {ex.Message}");
                    // Không throw — vẫn tiếp tục fetch bằng dữ liệu hiện có
                }
            }

            if (!string.IsNullOrWhiteSpace(fetcherNode.SourceOutputKey))
            {
                // ── Chế độ 1: lấy 1 key cụ thể ──
                var value = env.Service.ResolveDynamicValueForExecution(sourceNode, fetcherNode.SourceOutputKey, env);
                if (value == "—") value = string.Empty;

                Debug.WriteLine($"[DataFetcherNodeExecutor] Chế độ 1: Fetch key='{fetcherNode.SourceOutputKey}' từ '{sourceNode.Title}': value='{value}' (length={value?.Length ?? 0})");
                EnsureOutputPort(fetcherNode, fetcherNode.SourceOutputKey, value);
                if (!string.IsNullOrWhiteSpace(env.ExecutionId))
                {
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, fetcherNode.Id, fetcherNode.SourceOutputKey.Trim(), value);
                }
            }
            else
            {
                // ── Chế độ 2: copy TOÀN BỘ outputs từ node nguồn ──
                if (sourceNode.DynamicOutputs == null || sourceNode.DynamicOutputs.Count == 0)
                {
                    Debug.WriteLine($"[DataFetcherNodeExecutor] ❌ Chế độ 2: Node nguồn '{sourceNode.Title}' không có dynamic output.");
                    return;
                }

                Debug.WriteLine($"[DataFetcherNodeExecutor] Chế độ 2: Copy {sourceNode.DynamicOutputs.Count} outputs từ '{sourceNode.Title}'");

                // Xóa sạch các port cũ (kể cả port "value" mặc định từ các workflow cũ)
                // để chỉ giữ lại đúng những keys thực tế từ node nguồn.
                fetcherNode.DynamicOutputs.Clear();

                foreach (var srcOutput in sourceNode.DynamicOutputs)
                {
                    var val = env.Service.ResolveDynamicValueForExecution(sourceNode, srcOutput.Key, env);
                    if (val == "—") val = string.Empty;

                    Debug.WriteLine($"[DataFetcherNodeExecutor]   Copy '{srcOutput.Key}' = '{val}' (length={val?.Length ?? 0})");
                    EnsureOutputPort(fetcherNode, srcOutput.Key, val, srcOutput.DisplayName);
                    if (!string.IsNullOrWhiteSpace(env.ExecutionId) && !string.IsNullOrWhiteSpace(srcOutput.Key))
                    {
                        env.Service.SetScopedNodeStringOutput(env.ExecutionId, fetcherNode.Id, srcOutput.Key.Trim(), val);
                    }
                }
            }
        }

        /// <summary>
        /// Tạo hoặc cập nhật dynamic output port của DataFetcherNode.
        /// </summary>
        private static void EnsureOutputPort(DataFetcherNode node, string? key, string? value, string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (node.DynamicOutputs == null) return;

            var port = node.DynamicOutputs.FirstOrDefault(p =>
                string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

            if (port == null)
            {
                port = new WorkflowDynamicDataPort
                {
                    Key         = key,
                    DisplayName = displayName ?? key,
                    IsMultiple  = false,
                    OutputType  = WorkflowDataType.String,
                    ConvertType = WorkflowDataType.String
                };
                // Thêm vào UI thread-safe (executor chạy trong Task nhưng DynamicOutputs có thể được bound)
                node.DynamicOutputs.Add(port);
            }
            else if (!string.IsNullOrWhiteSpace(displayName))
            {
                port.DisplayName = displayName;
            }

            port.UserValueOverride = value ?? string.Empty;
        }

        private static WorkflowNode? FindSourceNode(string sourceNodeId, NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId)) return null;

            // 1) Tìm trong ReachableToEnd trước — khi chạy standalone (RunSingleNodeAsync),
            //    reachableToEnd = all workflow nodes (vì allNodesForLookup được truyền đầy đủ).
            //    Đây là cách chắc chắn nhất, không phụ thuộc vào flow connections.
            var fromReachable = env.ReachableToEnd?
                .FirstOrDefault(n => string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
            if (fromReachable != null)
            {
                Debug.WriteLine($"[FindSourceNode] ✅ Tìm thấy trong ReachableToEnd: '{fromReachable.Title}'");
                return fromReachable;
            }

            // 2) Fallback: tìm trong TẤT CẢ nodes có trong connections (cả upstream lẫn downstream)
            var fromConnections = env.Connections?
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .Where(n => n != null)
                .Distinct()
                .FirstOrDefault(n => string.Equals(n!.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
            if (fromConnections != null)
            {
                Debug.WriteLine($"[FindSourceNode] ✅ Tìm thấy trong Connections: '{fromConnections.Title}'");
                return fromConnections;
            }

            // 3) Fallback: tìm trong TẤT CẢ nodes trên canvas (tất cả WorkflowEditorWindow đang mở)
            //    Source node có thể không nằm trong bất kỳ connection nào (standalone node).
            Debug.WriteLine($"[FindSourceNode] ⚠️ Không tìm thấy trong ReachableToEnd/Connections, thử fallback ViewModel.Nodes...");
            WorkflowNode? fromViewModel = null;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                void GetFromCanvas()
                {
                    var app = System.Windows.Application.Current;
                    if (app == null) return;

                    // Tìm trong TẤT CẢ cửa sổ đang mở (không chỉ MainWindow)
                    foreach (System.Windows.Window window in app.Windows)
                    {
                        if (window is FlowMy.Views.WorkflowEditorWindow win &&
                            win.ViewModel?.Nodes != null)
                        {
                            var found = win.ViewModel.Nodes
                                .FirstOrDefault(n => string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
                            if (found != null)
                            {
                                fromViewModel = found;
                                Debug.WriteLine($"[FindSourceNode] ✅ Tìm thấy trong ViewModel.Nodes: '{found.Title}'");
                                return;
                            }
                        }
                    }
                    Debug.WriteLine($"[FindSourceNode] ❌ Không tìm thấy trong bất kỳ WorkflowEditorWindow nào");
                }

                try
                {
                    if (dispatcher.CheckAccess()) GetFromCanvas();
                    else dispatcher.Invoke(GetFromCanvas);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FindSourceNode] ❌ Dispatcher.Invoke lỗi: {ex.Message}");
                }
            }

            return fromViewModel;
        }

        /// <summary>
        /// Public helper: lấy value từ 1 source node, cập nhật DynamicOutputs của DataFetcherNode.
        /// Dùng cho Timer và Realtime (không có NodeExecutionEnvironment).
        /// </summary>
        public static void FetchValueFromNode(DataFetcherNode fetcherNode, WorkflowNode sourceNode)
        {
            if (!string.IsNullOrWhiteSpace(fetcherNode.SourceOutputKey))
            {
                var val = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, fetcherNode.SourceOutputKey);
                if (val == "—") val = string.Empty;
                EnsureOutputPort(fetcherNode, fetcherNode.SourceOutputKey, val,
                    sourceNode.DynamicOutputs?.FirstOrDefault(p =>
                        string.Equals(p.Key, fetcherNode.SourceOutputKey, StringComparison.OrdinalIgnoreCase))
                        ?.DisplayName);
            }
            else if (sourceNode.DynamicOutputs != null)
            {
                foreach (var output in sourceNode.DynamicOutputs)
                {
                    var val = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, output.Key);
                    if (val == "—") val = string.Empty;
                    EnsureOutputPort(fetcherNode, output.Key, val, output.DisplayName);
                }
            }
        }

        /// <summary>
        /// Chờ WebNode hoàn thành load trang. Timeout: 30 giây.
        /// </summary>
        private static async Task WaitForWebNodeLoadAsync(WebNode webNode, CancellationToken cancellationToken)
        {
            var tcs = webNode.PendingOutputsTcs;
            if (tcs == null || tcs.Task.IsCompleted)
            {
                // WebNode đã hoàn thành load/gom dữ liệu hoặc không trong trạng thái đứng chờ -> Không block!
                return;
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await tcs.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
