using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;

namespace FlowMy.Services.Workflow.NodeExecutors;

internal sealed class KeyScopedNodeExecutor : INodeExecutor
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<string>>> Accum =
       new(StringComparer.Ordinal);

    public static void ClearStoreForExecution(string? executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId)) return;
        var prefix = executionId + "\0";
        foreach (var key in Accum.Keys.ToList())
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                Accum.TryRemove(key, out _);
        }
    }

    private static string BucketKey(string? executionId, string nodeId)
        => $"{executionId ?? string.Empty}\0{nodeId}";

    private static ConcurrentDictionary<string, List<string>> GetOrCreateBucket(string? executionId, string nodeId)
        => Accum.GetOrAdd(BucketKey(executionId, nodeId), _ => new ConcurrentDictionary<string, List<string>>(StringComparer.Ordinal));

    public bool CanExecute(WorkflowNode node) => node is KeyScopedNode;

    public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
    {
        if (node is not KeyScopedNode kn) return;
        if (kn.IsWriteMode)
            ExecuteWrite(kn, env);
        else
            await ExecuteReadAsync(kn, env).ConfigureAwait(false);
    }

    private static void ExecuteWrite(KeyScopedNode node, NodeExecutionEnvironment env)
    {
        var userKey = ResolveDynamicInputValue(node, "key", env);
        if (string.IsNullOrWhiteSpace(userKey))
            userKey = node.StaticKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userKey))
            userKey = "_";

        var val = ResolveDynamicInputValue(node, "value", env) ?? string.Empty;

        var bucket = GetOrCreateBucket(env.ExecutionId, node.Id);
        var list = bucket.GetOrAdd(userKey, _ => new List<string>());
        lock (list)
        {
            list.Add(val);
        }

        node.LastWrittenKey = userKey;
        node.LastWrittenValue = val;
        node.StoreJson = SerializeBucket(bucket);
        node.SyncStoreToDynamicOutput();
    }

    private static async Task ExecuteReadAsync(KeyScopedNode node, NodeExecutionEnvironment env)
    {
        var ms = GetDelayMs(node);
        if (ms > 0)
            await Task.Delay(ms, env.CancellationToken).ConfigureAwait(false);

        if (Accum.TryGetValue(BucketKey(env.ExecutionId, node.Id), out var bucket))
            node.StoreJson = SerializeBucket(bucket);
        else
            node.StoreJson = "{}";

        node.SyncStoreToDynamicOutput();
    }

    private static int GetDelayMs(KeyScopedNode node)
    {
        if (node.PollTimeValue <= 0) return 0;
        return node.PollUnit switch
        {
            KeyScopedPollUnit.Seconds => node.PollTimeValue * 1000,
            KeyScopedPollUnit.Minutes => node.PollTimeValue * 60_000,
            _ => node.PollTimeValue
        };
    }

    private static string SerializeBucket(ConcurrentDictionary<string, List<string>> bucket)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kv in bucket)
        {
            lock (kv.Value)
            {
                if (kv.Value.Count == 0) continue;
                if (kv.Value.Count == 1)
                    dict[kv.Key] = kv.Value[0];
                else
                    dict[kv.Key] = kv.Value.ToList();
            }
        }
        return JsonSerializer.Serialize(dict);
    }

    private static string? ResolveDynamicInputValue(KeyScopedNode self, string inputKey, NodeExecutionEnvironment env)
    {
        var input = self.DynamicInputs.FirstOrDefault(i => string.Equals(i.Key, inputKey, StringComparison.OrdinalIgnoreCase));
        if (input == null || string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
            return null;

        var connections = env.Connections;
        var upstream = connections.FirstOrDefault(c =>
            c.ToNode == self &&
            c.FromNode != null &&
            c.FromNode.Id == input.SelectedSourceNodeId)?.FromNode;

        if (upstream == null)
        {
            upstream = connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && n.Id == input.SelectedSourceNodeId);
        }

        if (upstream == null) return null;
        var outKey = string.IsNullOrWhiteSpace(input.SelectedSourceOutputKey) ? input.Key : input.SelectedSourceOutputKey;
        return env.Service.ResolveDynamicValueForExecution(upstream, outKey ?? inputKey, env);
    }
}
