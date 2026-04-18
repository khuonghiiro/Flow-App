using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        var sw = Stopwatch.StartNew();
        var executionId = env.ExecutionId ?? string.Empty;
        var isParallelScopedRun =
            executionId.Contains(":dispatch-", StringComparison.Ordinal) ||
            executionId.Contains(":at-manual-", StringComparison.Ordinal);

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

            string? resolved = null;
            if (!string.IsNullOrWhiteSpace(env.ExecutionId) &&
                env.Service.TryGetScopedNodeStringOutputForLookupChain(env.ExecutionId, sourceNode.Id, sourceKey, out var scoped) &&
                !string.IsNullOrWhiteSpace(scoped))
            {
                resolved = scoped;
            }
            else if (sourceNode is InputNode)
            {
                resolved = env.Service.ResolveDynamicValueForExecution(sourceNode, sourceKey, env);
                if (string.IsNullOrWhiteSpace(resolved) || string.Equals(resolved, "—", StringComparison.Ordinal))
                    resolved = null;
            }
            else if (typed.IncludeIndirectSources)
            {
                // UI/dialog cho phép chọn nguồn gián tiếp (không nối trực tiếp). Snapshot scoped có thể thiếu
                // (vd. key null từ Code) — fallback giống ResolveDynamicValueForExecution / preview.
                // Với run song song (dispatch/manual branch), KHÔNG fallback shared runtime để tránh
                // kéo nhầm giá trị iteration cuối (trùng dữ liệu trong mảng append).
                if (!isParallelScopedRun)
                {
                    resolved = env.Service.ResolveDynamicValueForExecution(sourceNode, sourceKey, env);
                    if (string.IsNullOrWhiteSpace(resolved) || string.Equals(resolved, "—", StringComparison.Ordinal))
                        resolved = null;
                }
            }

            if (string.IsNullOrWhiteSpace(resolved))
                continue;

            resolvedValues.Add(resolved);
        }

        var runId = string.IsNullOrWhiteSpace(env.ExecutionId) ? "single" : env.ExecutionId;
        // Append: gom theo lần chạy gốc (AsyncTask mỗi vòng có :dispatch-i / nhánh :at-manual-… — cùng state một mảng).
        var stateKeyBase = typed.AppendMode
            ? WorkflowKeyValueStore.EnumerateScopedLookupExecutionIds(runId).LastOrDefault() ?? runId
            : runId;
        var stateKey = $"{stateKeyBase}:{typed.Id}";
        var state = _stateByExecutionAndNode.GetOrAdd(stateKey, _ => new RuntimeState());

        if (resolvedValues.Count == 0)
        {
            // Không có giá trị mới ở iteration này:
            // nếu AppendMode đã có state trước đó thì vẫn publish snapshot hiện tại vào scoped của execution hiện tại
            // để downstream (Output/...) không bị empty do thiếu bản ghi scoped.
            if (typed.AppendMode)
            {
                string? existingJson = null;
                lock (state.SyncRoot)
                {
                    if (state.Values.Count > 0)
                        existingJson = JsonSerializer.Serialize(state.Values);
                }

                if (!string.IsNullOrWhiteSpace(existingJson))
                {
                    lock (typed.ResolvedOutputsSyncRoot)
                    {
                        typed.ResolvedOutputs.Clear();
                        typed.ResolvedOutputs[key] = existingJson;
                    }
                    var outPortNoNew = typed.DynamicOutputs.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                    if (outPortNoNew != null)
                        outPortNoNew.UserValueOverride = existingJson;
                    if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                        env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, typed.Id, typed.ResolvedOutputs);
                }
            }

            env.OnNodeCompleted?.Invoke(typed, sw.Elapsed);
            await env.TraverseOutputsAsync(node);
            return;
        }

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
