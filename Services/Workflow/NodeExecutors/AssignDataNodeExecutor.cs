using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Linq;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class AssignDataNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is AssignDataNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var assignNode = (AssignDataNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            foreach (var a in assignNode.Assignments)
            {
                if (string.IsNullOrWhiteSpace(a.SourceNodeId) || string.IsNullOrWhiteSpace(a.TargetNodeId))
                    continue;

                var sourceNode = connections
                    .SelectMany(c => new[] { c.FromNode, c.ToNode })
                    .FirstOrDefault(n => n != null && string.Equals(n.Id, a.SourceNodeId, StringComparison.OrdinalIgnoreCase));

                var targetNode = connections
                    .SelectMany(c => new[] { c.FromNode, c.ToNode })
                    .FirstOrDefault(n => n != null && string.Equals(n.Id, a.TargetNodeId, StringComparison.OrdinalIgnoreCase));

                if (sourceNode == null || targetNode == null)
                    continue;

                // Nếu cần, chạy lại node nguồn trước khi lấy giá trị
                if (a.RefreshSourceBeforeUse && !string.IsNullOrWhiteSpace(a.SourceOutputKey))
                {
                    await env.Service.ExecuteNodeLogicOnlyAsync(sourceNode, connections, env.CancellationToken);
                }

                // Trường hợp đặc biệt: target là StorageNode
                if (targetNode is StorageNode storageTarget)
                {
                    // Nếu TargetKey rỗng -> copy TOÀN BỘ outputs từ sourceNode sang Storage
                    if (string.IsNullOrWhiteSpace(a.TargetKey))
                    {
                        if (sourceNode.DynamicOutputs != null && sourceNode.DynamicOutputs.Count > 0)
                        {
                            foreach (var outPort in sourceNode.DynamicOutputs)
                            {
                                var outKey = outPort.Key;
                                if (string.IsNullOrWhiteSpace(outKey)) continue;

                                // Lấy giá trị hiện tại của output này
                                var v = env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, a.SourceNodeId, outKey, env);
                                // "—" là placeholder, coi như rỗng
                                if (string.Equals(v?.Trim(), "—", StringComparison.Ordinal))
                                    v = string.Empty;

                                storageTarget.SetStoredOutput(outKey, v ?? string.Empty);
                            }
                        }
                    }
                    else
                    {
                        // TargetKey có giá trị -> gán một key duy nhất vào Storage
                        var value = env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, a.SourceNodeId, a.SourceOutputKey, env);
                        if (string.Equals(value?.Trim(), "—", StringComparison.Ordinal))
                            value = string.Empty;

                        storageTarget.SetStoredOutput(a.TargetKey, value ?? string.Empty);
                    }

                    env.Service.PublishStorageOutputsToScoped(storageTarget, env.ExecutionId);
                }
                else
                {
                    // Logic cũ: gán vào node đích bình thường (yêu cầu TargetKey)
                    if (string.IsNullOrWhiteSpace(a.TargetKey))
                        continue;

                    var value = env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, a.SourceNodeId, a.SourceOutputKey, env);
                    if (string.Equals(value?.Trim(), "—", StringComparison.Ordinal))
                        value = string.Empty;

                    WorkflowExecutionService.SetDynamicValueByKey(targetNode, a.TargetKey, value ?? string.Empty);
                }
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(assignNode, sw.Elapsed);

            await env.TraverseOutputsAsync(assignNode);
        }
    }
}
