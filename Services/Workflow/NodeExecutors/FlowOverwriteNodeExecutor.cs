using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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

        var key = string.IsNullOrWhiteSpace(typed.OutputKey) ? "outputKey" : typed.OutputKey.Trim();
        typed.OutputKey = key;
        typed.RebuildDynamicOutputs();

        var incoming = env.IncomingConnection?.FromNode;
        if (incoming == null)
        {
            await env.TraverseOutputsAsync(node);
            return;
        }

        var mapping = typed.Mappings.FirstOrDefault(m =>
            !string.IsNullOrWhiteSpace(m.SourceNodeId) &&
            string.Equals(m.SourceNodeId, incoming.Id, StringComparison.OrdinalIgnoreCase));
        if (mapping == null)
        {
            await env.TraverseOutputsAsync(node);
            return;
        }

        var outputKey = mapping.SourceOutputKey;
        if (string.IsNullOrWhiteSpace(outputKey))
            outputKey = incoming.DynamicOutputs?.FirstOrDefault()?.Key;
        if (string.IsNullOrWhiteSpace(outputKey))
        {
            await env.TraverseOutputsAsync(node);
            return;
        }

        var resolved = env.Service.ResolveDynamicValueForExecution(incoming, outputKey, env);
        var runId = string.IsNullOrWhiteSpace(env.ExecutionId) ? "single" : env.ExecutionId;
        var stateKey = $"{runId}:{typed.Id}";
        var state = _stateByExecutionAndNode.GetOrAdd(stateKey, _ => new RuntimeState());

        string outputValue;
        lock (state.SyncRoot)
        {
            if (typed.AppendMode)
            {
                state.Values.Add(resolved);
                outputValue = JsonSerializer.Serialize(state.Values);
            }
            else
            {
                state.LastValue = resolved;
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

        await env.TraverseOutputsAsync(node);
    }
}
