using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Parallel-safe aggregator for AsyncTask loop-like dispatch.
    /// It reads scoped outputs from each dispatch iteration executionId:
    ///   {parentExecutionId}:dispatch-{index}
    /// and returns a single JSON object under this node's dynamic output "results":
    ///   { "0": "...", "1": "...", ... }
    /// </summary>
    internal sealed class AsyncTaskDispatchCollectNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is AsyncTaskDispatchCollectNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var collectNode = (AsyncTaskDispatchCollectNode)node;
            var connections = env.Connections;

            // Reset previous UI/runtime overrides
            var resultsOut = collectNode.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "results", StringComparison.OrdinalIgnoreCase));
            if (resultsOut != null) resultsOut.UserValueOverride = string.Empty;

            var asyncTaskNode = env.IncomingConnection?.FromNode as AsyncTaskNode
                                ?? connections
                                    .FirstOrDefault(c => c.ToNode == collectNode && c.FromNode is AsyncTaskNode)
                                    ?.FromNode as AsyncTaskNode;

            if (asyncTaskNode == null)
            {
                // Not connected properly: output empty list
                var emptyJson = "[]";
                SetResults(env, collectNode, resultsOut, emptyJson);
                await env.TraverseOutputsAsync(collectNode);
                return;
            }

            asyncTaskNode.EnsureDispatchDynamicPorts();

            // Resolve dispatch iterations (count and indexes) based on AsyncTask configuration.
            var iterations = env.Service.ResolveAsyncTaskDispatchIterations(asyncTaskNode, connections, env).ToList();

            // Resolve source body node
            WorkflowNode? sourceNode = null;
            if (!string.IsNullOrWhiteSpace(collectNode.SourceBodyNodeId))
            {
                var sid = collectNode.SourceBodyNodeId.Trim();
                sourceNode = connections
                    .SelectMany(c => new[] { c.FromNode, c.ToNode })
                    .FirstOrDefault(n => n != null && string.Equals(n.Id, sid, System.StringComparison.OrdinalIgnoreCase));
            }

            var outputKey = collectNode.SourceOutputKey?.Trim() ?? string.Empty;
            var valuesByIndex = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var (index, _) in iterations)
            {
                var iterationExecutionId = $"{env.ExecutionId}:dispatch-{index}";

                if (sourceNode == null || string.IsNullOrWhiteSpace(outputKey))
                {
                    valuesByIndex[index.ToString()] = string.Empty;
                    continue;
                }

                // Read from scoped snapshot for that iteration
                var v = env.Service.ResolveDynamicValueForRun(sourceNode, outputKey, iterationExecutionId);
                valuesByIndex[index.ToString()] = string.IsNullOrWhiteSpace(v) || v == "—" ? string.Empty : v;
            }

            var json = JsonSerializer.Serialize(valuesByIndex);
            SetResults(env, collectNode, resultsOut, json);

            await env.TraverseOutputsAsync(collectNode);
        }

        private static void SetResults(NodeExecutionEnvironment env, AsyncTaskDispatchCollectNode collectNode, WorkflowDynamicDataPort? resultsOut, string json)
        {
            if (resultsOut != null)
                resultsOut.UserValueOverride = json;

                // Set scoped snapshot so downstream nodes (executed during traversal) can read it immediately.
            env.Service.SetScopedNodeStringOutput(env.ExecutionId, collectNode.Id, "results", json);
        }
    }
}

