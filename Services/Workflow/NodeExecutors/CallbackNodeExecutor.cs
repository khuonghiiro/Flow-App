using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Linq;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho CallbackNode - cho phép chạy lại workflow từ một node đã chọn.
    /// Giới hạn số lần callback để tránh vòng lặp vô hạn.
    /// </summary>
    internal sealed class CallbackNodeExecutor : INodeExecutor
    {
        // Outer key = env.ExecutionId (per run / parallel lane). Inner key = CallbackNode.Id.
        // Do not key by ExecutionPath.First() — that is a node id (often the same Start across runs).
        private static readonly Dictionary<string, Dictionary<string, int>> _callbackCounters = new();
        private static readonly object _lock = new object();

        public bool CanExecute(WorkflowNode node) => node is CallbackNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var callbackNode = (CallbackNode)node;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Validate target node
                if (string.IsNullOrWhiteSpace(callbackNode.TargetNodeId))
                {
                    var errorMsg = "❌ Callback node error: Chưa chọn node callback.";
                    env.OnNodeFailed?.Invoke(callbackNode, errorMsg);
                    sw.Stop();
                    return;
                }

                // Find target node
                var targetNode = env.Connections
                    .SelectMany(c => new[] { c.FromNode, c.ToNode })
                    .FirstOrDefault(n => n?.Id == callbackNode.TargetNodeId);

                if (targetNode == null)
                {
                    var errorMsg = $"❌ Callback node error: Không tìm thấy node target '{callbackNode.TargetNodeId}'.";
                    env.OnNodeFailed?.Invoke(callbackNode, errorMsg);
                    sw.Stop();
                    return;
                }

                var runKey = string.IsNullOrWhiteSpace(env.ExecutionId)
                    ? Guid.NewGuid().ToString("N")
                    : env.ExecutionId;

                lock (_lock)
                {
                    if (!_callbackCounters.ContainsKey(runKey))
                        _callbackCounters[runKey] = new Dictionary<string, int>();

                    var currentCount = _callbackCounters[runKey].GetValueOrDefault(callbackNode.Id, 0);

                    System.Diagnostics.Debug.WriteLine($"[CallbackNodeExecutor] Node: {callbackNode.Id}, RunKey(ExecutionId): {runKey}, CurrentCount: {currentCount}, MaxCount: {callbackNode.MaxCallbackCount}");

                    if (currentCount >= callbackNode.MaxCallbackCount)
                    {
                        var errorMsg = $"⚠️ Callback node đã đạt giới hạn {callbackNode.MaxCallbackCount} lần. Dừng workflow để tránh vòng lặp vô hạn.\n\n" +
                                     $"Callback node: {callbackNode.Title} ({callbackNode.Id.Substring(0, 8)}...)\n" +
                                     $"Target node: {targetNode.Title} ({targetNode.Id.Substring(0, 8)}...)\n" +
                                     $"Số lần callback: {currentCount}/{callbackNode.MaxCallbackCount}";

                        env.OnNodeFailed?.Invoke(callbackNode, errorMsg);
                        sw.Stop();

                        _callbackCounters.Remove(runKey);

                        return;
                    }

                    _callbackCounters[runKey][callbackNode.Id] = currentCount + 1;
                    System.Diagnostics.Debug.WriteLine($"[CallbackNodeExecutor] ✓ Callback count incremented to {currentCount + 1}");
                }

                sw.Stop();
                env.OnNodeCompleted?.Invoke(callbackNode, sw.Elapsed);

                // Execute callback - jump to target node and restart workflow from there
                System.Diagnostics.Debug.WriteLine($"[CallbackNodeExecutor] 🔄 Callback to node: {targetNode.Title} ({targetNode.Id})");
                
                // ExecutionPath = visited node ids (loop guard). Callback jump: fresh path from target.
                var newExecutionPath = new List<string>();
                
                // Execute target node with fresh execution context
                await env.Service.ExecuteNodeAsync(
                    targetNode,
                    env.Connections,
                    env.CancellationToken,
                    env.OnEnteringNode,
                    env.OnNodeStarted,
                    env.OnNodeCompleted,
                    env.OnNodeFailed,
                    null, // no incoming connection for callback
                    env.ReachableToEnd,
                    false,
                    newExecutionPath,
                    executionId: env.ExecutionId,
                    flowScopeId: env.FlowScopeId,
                    branchId: env.BranchId,
                    parentFlowScopeId: env.ParentFlowScopeId);

                if (callbackNode.FlowBehavior == CallbackFlowBehavior.JumpThenContinue)
                {
                    callbackNode.SyncPortsForBehavior();
                    await env.TraverseOutputsAsync(callbackNode);
                }

                // Counters keyed by runKey only increase (no per-jump decrement).
            }
            catch (Exception ex)
            {
                var errorMsg = $"❌ Callback node error: {ex.Message}";
                env.OnNodeFailed?.Invoke(callbackNode, errorMsg);
                throw;
            }
        }

        /// <summary>
        /// Static method để reset callback counters (dùng khi bắt đầu workflow execution mới)
        /// </summary>
        public static void ResetCallbackCounters()
        {
            lock (_lock)
            {
                _callbackCounters.Clear();
                System.Diagnostics.Debug.WriteLine($"[CallbackNodeExecutor] ✓ Reset all callback counters");
            }
        }

        /// <summary>
        /// Static method để reset callback counter cho một execution cụ thể
        /// </summary>
        public static void ResetCallbackCounters(string executionId)
        {
            lock (_lock)
            {
                if (_callbackCounters.ContainsKey(executionId))
                {
                    _callbackCounters.Remove(executionId);
                    System.Diagnostics.Debug.WriteLine($"[CallbackNodeExecutor] ✓ Reset callback counters for executionId: {executionId}");
                }
            }
        }
    }
}
