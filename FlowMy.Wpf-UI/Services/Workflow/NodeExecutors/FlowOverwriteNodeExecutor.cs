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
        public List<object?> Values { get; } = new();
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
        // ScopeToFlowExecution:
        // - True (Mặc định): Gom tất cả kết quả từ các luồng con/vòng lặp về luồng cha gốc (Root ExecutionId) để luồng cha thu thập đầy đủ mảng.
        // - False: Cô lập theo riêng từng luồng con (Current ExecutionId).
        var stateKeyBase = (typed.AppendMode && typed.ScopeToFlowExecution)
            ? (WorkflowKeyValueStore.EnumerateScopedLookupExecutionIds(runId).LastOrDefault() ?? runId)
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
                    {
                        var localOutputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            [key] = existingJson,
                        };
                        env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, typed.Id, localOutputs);
                    }
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
                foreach (var resolved in resolvedValues)
                {
                    if (string.IsNullOrWhiteSpace(resolved)) continue;

                    var trimmed = resolved.Trim();
                    bool parsed = false;

                    if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(trimmed);
                            var root = doc.RootElement.Clone();

                            if (typed.FlattenArrayValues && root.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in root.EnumerateArray())
                                {
                                    state.Values.Add(item.Clone());
                                }
                            }
                            else
                            {
                                state.Values.Add(root);
                            }
                            parsed = true;
                        }
                        catch
                        {
                            parsed = false;
                        }
                    }

                    if (!parsed)
                    {
                        state.Values.Add(resolved);
                    }
                }
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

        // Publish từ snapshot cục bộ thay vì typed.ResolvedOutputs (shared giữa các dispatch song song).
        // Nếu publish từ shared, PublishDictionaryOutputsToScopedStore duyệt dict sau khi mở khóa,
        // nên dispatch khác có thể đã ghi đè [key] → scoped(dispatch-X) nhận nhầm giá trị dispatch-Y.
        // Đã quan sát trong thực tế: 4/5 dispatch của FlowOverwrite đọc trùng một giá trị (xem
        // examples/log3.json); Output 2/Output 3 tính đúng trong scoped riêng, chỉ riêng bước publish
        // bị race nên scoped của FlowOverwrite bị trộn.
        if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
        {
            var localOutputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [key] = outputValue,
            };
            env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, typed.Id, localOutputs);
        }

        env.OnNodeCompleted?.Invoke(typed, sw.Elapsed);
        await env.TraverseOutputsAsync(node);
    }
}
