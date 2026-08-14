using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho ListOutNode.
    /// Resolve values từ các source nodes dựa trên OutputMappings và lưu vào ResolvedOutputs.
    /// </summary>
    internal sealed class ListOutNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is ListOutNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var listOutNode = (ListOutNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var batchOutputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                // Process each output mapping
                foreach (var mapping in listOutNode.OutputMappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.NewKey) ||
                        string.IsNullOrWhiteSpace(mapping.SourceNodeId) ||
                        string.IsNullOrWhiteSpace(mapping.SourceOutputKey))
                    {
                        continue;
                    }

                    // Find source node
                    WorkflowNode? sourceNode = null;

                    // Try direct connection first
                    var upstreamConnection = connections
                        .FirstOrDefault(c =>
                            c.ToNode == listOutNode &&
                            c.FromNode != null &&
                            c.FromNode.Id == mapping.SourceNodeId);

                    sourceNode = upstreamConnection?.FromNode;

                    // Fallback: find node by ID in graph (for LoopBody scenarios)
                    if (sourceNode == null)
                    {
                        sourceNode = connections
                            .SelectMany(c => new[] { c.FromNode, c.ToNode })
                            .FirstOrDefault(n => n != null && n.Id == mapping.SourceNodeId);
                    }

                    // Resolve value from source node
                    if (sourceNode != null)
                    {
                        var value = env.Service.ResolveDynamicValueForExecution(sourceNode, mapping.SourceOutputKey, env);
                        batchOutputs[mapping.NewKey] = value;

                        System.Diagnostics.Debug.WriteLine($"ListOutNode: Mapped {mapping.SourceNodeId}.{mapping.SourceOutputKey} -> {mapping.NewKey} = {value}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"ListOutNode: Source node not found: {mapping.SourceNodeId}");
                        batchOutputs[mapping.NewKey] = null;
                    }
                }

                // Publish scoped outputs ngay tại đây để parallel dispatch không bị ghi đè chéo.
                if (!string.IsNullOrWhiteSpace(env.ExecutionId) && batchOutputs.Count > 0)
                {
                    env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, listOutNode.Id, batchOutputs);
                }

                // Update shared runtime outputs cho UI/debug.
                listOutNode.ResolvedOutputs.Clear();
                foreach (var kv in batchOutputs)
                {
                    listOutNode.ResolvedOutputs[kv.Key] = kv.Value;
                }

                // Update DynamicOutputs with resolved values for downstream consumption
                foreach (var output in listOutNode.DynamicOutputs)
                {
                    if (listOutNode.ResolvedOutputs.TryGetValue(output.Key, out var resolvedValue))
                    {
                        // Store resolved value in a way that downstream nodes can access
                        // This is handled by NodeDataPanelService.ResolveDynamicValueByKey
                    }
                }

                System.Diagnostics.Debug.WriteLine($"ListOutNode: Resolved {listOutNode.ResolvedOutputs.Count} outputs");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ListOutNode error: {ex.Message}");
                listOutNode.ResolvedOutputs.Clear();
                env.OnNodeFailed?.Invoke(listOutNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(listOutNode, sw.Elapsed);

            await env.TraverseOutputsAsync(listOutNode);
        }
    }
}

