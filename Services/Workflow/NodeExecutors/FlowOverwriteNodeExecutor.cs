using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;

namespace FlowMy.Services.Workflow.NodeExecutors;

internal sealed class FlowOverwriteNodeExecutor : INodeExecutor
{
    private sealed class RuntimeState
    {
        public string? LastValue { get; set; }
        public List<string> Values { get; } = new();
        public object SyncRoot { get; } = new();
    }

    private static readonly ConcurrentDictionary<string, RuntimeState> _stateByExecutionAndNode = new();

    public bool CanExecute(WorkflowNode node) => node is FlowOverwriteNode;

    public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
    {
        if (node is not FlowOverwriteNode typed) return;
        var sw = Stopwatch.StartNew();

        var key = string.IsNullOrWhiteSpace(typed.OutputKey) ? "outputKey" : typed.OutputKey.Trim();
        typed.OutputKey = key;
        typed.RebuildDynamicOutputs();

        var nodesById = env.Connections
            .SelectMany(c => new[] { c.FromNode, c.ToNode })
            .Where(n => n != null)
            .GroupBy(n => n!.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First()!, StringComparer.OrdinalIgnoreCase);

        var resolvedValues = new List<string>();
        foreach (var mapping in typed.Mappings)
        {
            if (mapping == null || string.IsNullOrWhiteSpace(mapping.SourceNodeId))
                continue;

            if (!nodesById.TryGetValue(mapping.SourceNodeId.Trim(), out var sourceNode))
                continue;

            var sourceKey = mapping.SourceOutputKey;
            if (string.IsNullOrWhiteSpace(sourceKey))
                sourceKey = sourceNode.DynamicOutputs?.FirstOrDefault()?.Key;
            if (string.IsNullOrWhiteSpace(sourceKey))
                continue;

            // Chỉ lấy dữ liệu đã có trong scoped store của lượt chạy hiện tại;
            // tránh việc source chưa chạy nhưng vẫn fallback về giá trị UI cũ.
            if (!env.RefreshOnly &&
                !string.IsNullOrWhiteSpace(env.ExecutionId) &&
                !env.Service.TryGetScopedNodeStringOutput(env.ExecutionId, sourceNode.Id, sourceKey, out var _))
            {
                continue;
            }

            resolvedValues.Add(env.Service.ResolveDynamicValueForExecution(sourceNode, sourceKey, env));
        }

        if (resolvedValues.Count == 0)
        {
            env.OnNodeCompleted?.Invoke(typed, sw.Elapsed);
            await env.TraverseOutputsAsync(node);
            return;
        }

        var runId = string.IsNullOrWhiteSpace(env.ExecutionId) ? "single" : env.ExecutionId;
        var stateKey = $"{runId}:{typed.Id}";
        var state = _stateByExecutionAndNode.GetOrAdd(stateKey, _ => new RuntimeState());

        string outputValue;
        lock (state.SyncRoot)
        {
            if (typed.AppendMode)
            {
                state.Values.AddRange(resolvedValues);
                outputValue = JsonSerializer.Serialize(state.Values);
            }
            else
            {
                state.LastValue = resolvedValues[^1];
                outputValue = state.LastValue ?? string.Empty;
            }
        }

        lock (typed.ResolvedOutputsSyncRoot)
        {
            typed.ResolvedOutputs.Clear();
            typed.ResolvedOutputs[key] = outputValue;
        }

        var outPort = typed.DynamicOutputs.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
        if (outPort != null)
            outPort.UserValueOverride = outputValue;

        if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
            env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, typed.Id, typed.ResolvedOutputs);

        env.OnNodeCompleted?.Invoke(typed, sw.Elapsed);
        await env.TraverseOutputsAsync(node);
    }
}
