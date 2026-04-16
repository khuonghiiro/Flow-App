using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FlowMy.Services.Workflow.NodeExecutors;

internal sealed class KeyValueBridgeNodeExecutor : INodeExecutor
{
    private const string AllKeysTitle = "Lấy tất cả";
    public bool CanExecute(WorkflowNode node) => node is KeyValueBridgeNode;

    public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
    {
        if (node is not KeyValueBridgeNode bridge) return;

        // Ensure writers can be discovered via LastExecutionId in RefreshOnly(single-node) mode.
        if (env.RefreshOnly && bridge.IsPassKeyMode)
            bridge.LastExecutionId = env.ExecutionId;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var traverseAfterExecution = false;
        try
        {
            if (bridge.EnableDataCleanup)
            {
                await ExecuteCleanupAsync(bridge, env).ConfigureAwait(false);
                traverseAfterExecution = !env.RefreshOnly;
            }
            else
            {
            if (bridge.IsPassKeyMode)
            {
                await ExecutePassAsync(bridge, env).ConfigureAwait(false);
                // RunSingleNode/refresh should not trigger downstream traversal.
                traverseAfterExecution = !env.RefreshOnly;
            }
            else
            {
                var pollMs = GetPollMs(bridge);
                // In workflow execution (RefreshOnly=false), Get mode must be one-shot so node can complete.
                // Continuous polling is reserved for RefreshOnly/UI single-node runs.
                traverseAfterExecution = !env.RefreshOnly;
                await ExecuteGetAsync(bridge, env, pollMs).ConfigureAwait(false);
            }
            }
        }
        finally
        {
            sw.Stop();
            env.OnNodeCompleted?.Invoke(bridge, sw.Elapsed);
        }

        if (traverseAfterExecution)
            await env.TraverseOutputsAsync(bridge).ConfigureAwait(false);
    }

    private static async Task ExecuteCleanupAsync(KeyValueBridgeNode node, NodeExecutionEnvironment env)
    {
        var validationMessage = ValidateCleanupBinding(node);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            PublishCleanupMessage(node, env, validationMessage);
            return;
        }

        var triggerCheck = CheckCleanupTrigger(node, env);
        if (!triggerCheck.IsReady)
        {
            PublishCleanupMessage(node, env, triggerCheck.Message ?? "Cleanup skipped: trigger is not ready");
            return;
        }
        if (!triggerCheck.IsMatched)
        {
            PublishCleanupMessage(node, env, triggerCheck.Message ?? "Cleanup skipped: trigger value not matched");
            return;
        }

        var targetNodeId = !string.IsNullOrWhiteSpace(node.CleanupTargetBridgeNodeId)
            ? node.CleanupTargetBridgeNodeId!.Trim()
            : node.Id;
        var allNodes = EnumerateWorkflowNodes(env);
        var kvRunIds = KeyValueBridgeKvScopeHelper.ResolveKvRunIdsForTarget(targetNodeId, allNodes);

        var dynamicKey = ResolveDynamicCleanupValue(node.CleanupKeySourceNodeId, node.CleanupKeySourceOutputKey, env);
        var effectiveKey = !string.IsNullOrWhiteSpace(dynamicKey)
            ? dynamicKey.Trim()
            : (node.CleanupTargetKey?.Trim() ?? string.Empty);
        var dynamicFilterValue = ResolveDynamicCleanupValue(node.CleanupFilterValueSourceNodeId, node.CleanupFilterValueSourceOutputKey, env);
        var effectiveFilterValue = !string.IsNullOrWhiteSpace(dynamicFilterValue)
            ? dynamicFilterValue.Trim()
            : (node.CleanupArrayFilterValue ?? string.Empty);
        var dynamicFilterField = ResolveDynamicCleanupValue(node.CleanupFilterFieldSourceNodeId, node.CleanupFilterFieldSourceOutputKey, env);
        var effectiveFilterField = !string.IsNullOrWhiteSpace(dynamicFilterField)
            ? dynamicFilterField.Trim()
            : (node.CleanupArrayFilterField ?? string.Empty);

        string message;
        if (node.CleanupClearAllNodeData)
        {
            var removed = 0;
            foreach (var rid in kvRunIds)
                removed += WorkflowKeyValueStore.ClearRunKeys(rid);
            message = removed > 0
                ? (kvRunIds.Count > 1
                    ? $"Cleanup OK: removed all keys ({removed}) across {kvRunIds.Count} KV scopes (Get + Pass nguồn)"
                    : $"Cleanup OK: removed all keys ({removed})")
                : "Cleanup: no data to clear";
        }
        else
        {
            var key = effectiveKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                message = "Cleanup skipped: empty key";
            }
            else if (!string.IsNullOrWhiteSpace(effectiveFilterField) &&
                     !string.IsNullOrWhiteSpace(effectiveFilterValue))
            {
                var totalRemoved = 0;
                int? lastNeg = null;
                foreach (var rid in kvRunIds)
                {
                    var removedItems = WorkflowKeyValueStore.RemoveArrayItemsByJsonField(
                        rid,
                        key,
                        effectiveFilterField,
                        effectiveFilterValue,
                        node.CleanupRemoveAllMatchedArrayItems);
                    if (removedItems > 0)
                        totalRemoved += removedItems;
                    else if (removedItems < 0)
                        lastNeg = removedItems;
                }

                message = totalRemoved switch
                {
                    > 0 => kvRunIds.Count > 1
                        ? $"Cleanup OK: removed {totalRemoved} item(s) in key '{key}' (mọi scope liên quan)"
                        : $"Cleanup OK: removed {totalRemoved} item(s) in key '{key}'",
                    _ => lastNeg == -1
                        ? $"Cleanup: key '{key}' is not array-like data"
                        : $"Cleanup: no matched item in key '{key}'"
                };
            }
            else
            {
                var removedAny = false;
                foreach (var rid in kvRunIds)
                    removedAny |= WorkflowKeyValueStore.RemoveKey(rid, key);
                message = removedAny
                    ? (kvRunIds.Count > 1
                        ? $"Cleanup OK: removed key '{key}' from all related KV scopes"
                        : $"Cleanup OK: removed key '{key}'")
                    : $"Cleanup: key '{key}' not found";
            }
        }

        PublishCleanupMessage(node, env, message);

        // Cập nhật UI/output các node Get đọc đúng scope vừa xóa (kể cả khi chạy workflow, không chỉ RefreshOnly).
        // ExecuteNodeLogicOnlyAsync luôn dùng RefreshOnly=true → ExecuteGetAsync phải ghi nhận store rỗng/null, không return sớm.
        var allNodesForLookup = allNodes;
        var relatedGetNodes = allNodes
            .OfType<KeyValueBridgeNode>()
            .Where(n => !n.IsPassKeyMode)
            .Where(n => kvRunIds.Contains(ResolveKvRunIdForGet(n, env)))
            .GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        foreach (var getNode in relatedGetNodes)
        {
            if (env.CancellationToken.IsCancellationRequested) break;
            await env.Service.ExecuteNodeLogicOnlyAsync(
                getNode,
                env.Connections,
                env.CancellationToken,
                allNodesForLookup: allNodesForLookup).ConfigureAwait(false);
        }

        // Node Pass: output "key" vẫn cache dict cũ sau khi ClearRunKeys — đồng bộ lại từ store.
        foreach (var rid in kvRunIds)
        {
            if (env.CancellationToken.IsCancellationRequested) break;
            var scopeId = rid.StartsWith("kv:", StringComparison.OrdinalIgnoreCase) && rid.Length > 3
                ? rid.AsSpan(3).ToString()
                : rid;
            var passNode = allNodes.OfType<KeyValueBridgeNode>()
                .FirstOrDefault(n =>
                    n.IsPassKeyMode &&
                    string.Equals(n.Id, scopeId, StringComparison.OrdinalIgnoreCase));
            if (passNode != null)
                SyncPassBridgeKeyOutputFromStore(passNode);
        }
    }

    /// <summary>Sau khi xóa KV: cập nhật output cổng "key" trên node Pass từ store hiện tại (tránh cache JSON cũ).</summary>
    private static void SyncPassBridgeKeyOutputFromStore(KeyValueBridgeNode passNode)
    {
        if (!passNode.IsPassKeyMode) return;
        var kvRunId = $"kv:{passNode.Id}";
        var all = WorkflowKeyValueStore.GetAllSnapshots(kvRunId);
        var channelKey = passNode.KvChannelKey?.Trim() ?? string.Empty;

        string dictJson;
        if (all.Count == 0)
            dictJson = "{}";
        else if (!string.IsNullOrWhiteSpace(channelKey))
        {
            var raw = WorkflowKeyValueStore.GetSnapshot(kvRunId, channelKey);
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { [channelKey] = raw };
            dictJson = JsonSerializer.Serialize(dict);
        }
        else
            dictJson = SerializeAllSnapshotsStable(all);

        lock (passNode.ResolvedOutputsSyncRoot)
        {
            passNode.ResolvedOutputs.Clear();
            passNode.ResolvedOutputs["key"] = dictJson;
        }

        var keyPort = passNode.DynamicOutputs?.FirstOrDefault(o =>
            string.Equals(o.Key, "key", StringComparison.OrdinalIgnoreCase));
        if (keyPort != null)
            keyPort.UserValueOverride = dictJson;
    }

    private static List<WorkflowNode> EnumerateWorkflowNodes(NodeExecutionEnvironment env)
    {
        var list = new List<WorkflowNode>();
        if (env.ReachableToEnd != null)
        {
            foreach (var n in env.ReachableToEnd)
                if (n != null) list.Add(n);
        }

        if (env.Connections != null)
        {
            foreach (var c in env.Connections)
            {
                if (c.FromNode != null) list.Add(c.FromNode);
                if (c.ToNode != null) list.Add(c.ToNode);
            }
        }

        return list
            .GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static string? ValidateCleanupBinding(KeyValueBridgeNode node)
    {
        if (string.IsNullOrWhiteSpace(node.CleanupTargetBridgeNodeId))
            return "Cleanup skipped: missing target KeyValueBridge node";

        var hasDynamicKeyBinding = !string.IsNullOrWhiteSpace(node.CleanupKeySourceNodeId) &&
                                   !string.IsNullOrWhiteSpace(node.CleanupKeySourceOutputKey);
        if (!node.CleanupClearAllNodeData &&
            string.IsNullOrWhiteSpace(node.CleanupTargetKey) &&
            !hasDynamicKeyBinding)
            return "Cleanup skipped: missing key to delete";

        var hasDynamicFilterValueBinding = !string.IsNullOrWhiteSpace(node.CleanupFilterValueSourceNodeId) &&
                                      !string.IsNullOrWhiteSpace(node.CleanupFilterValueSourceOutputKey);
        var hasDynamicFilterFieldBinding = !string.IsNullOrWhiteSpace(node.CleanupFilterFieldSourceNodeId) &&
                                           !string.IsNullOrWhiteSpace(node.CleanupFilterFieldSourceOutputKey);
        if ((!string.IsNullOrWhiteSpace(node.CleanupArrayFilterField) || hasDynamicFilterFieldBinding) &&
            string.IsNullOrWhiteSpace(node.CleanupArrayFilterValue) &&
            !hasDynamicFilterValueBinding)
            return "Cleanup skipped: missing filter value";

        if (string.IsNullOrWhiteSpace(node.CleanupTriggerSourceNodeId) ||
            string.IsNullOrWhiteSpace(node.CleanupTriggerSourceOutputKey))
            return "Cleanup skipped: missing trigger node/key binding";

        return null;
    }

    private static (bool IsReady, bool IsMatched, string? Message) CheckCleanupTrigger(KeyValueBridgeNode node, NodeExecutionEnvironment env)
    {
        var srcId = node.CleanupTriggerSourceNodeId?.Trim();
        var srcKey = node.CleanupTriggerSourceOutputKey?.Trim();
        if (string.IsNullOrWhiteSpace(srcId) || string.IsNullOrWhiteSpace(srcKey))
            return (false, false, "Cleanup skipped: trigger binding is empty");

        var srcNode = env.ReachableToEnd?.FirstOrDefault(n =>
                string.Equals(n.Id, srcId, StringComparison.OrdinalIgnoreCase))
            ?? env.Connections.SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && string.Equals(n.Id, srcId, StringComparison.OrdinalIgnoreCase));
        if (srcNode == null)
            return (false, false, $"Cleanup skipped: trigger node '{srcId}' not found");

        var raw = env.Service.ResolveDynamicValueForExecution(srcNode, srcKey, env);
        if (string.IsNullOrWhiteSpace(raw))
            return (false, false, $"Cleanup skipped: trigger key '{srcKey}' has no value");

        var expected = string.IsNullOrWhiteSpace(node.CleanupTriggerExpectedValue)
            ? "true"
            : node.CleanupTriggerExpectedValue.Trim();
        var matched = IsTriggerMatched(raw, expected);
        return matched
            ? (true, true, $"Cleanup trigger matched ({srcKey}={raw.Trim()})")
            : (true, false, $"Cleanup skipped: trigger value '{raw.Trim()}' != '{expected}'");
    }

    private static string? ResolveDynamicCleanupValue(string? sourceNodeId, string? sourceOutputKey, NodeExecutionEnvironment env)
    {
        var srcId = sourceNodeId?.Trim();
        var outKey = sourceOutputKey?.Trim();
        if (string.IsNullOrWhiteSpace(srcId) || string.IsNullOrWhiteSpace(outKey))
            return null;

        var srcNode = env.ReachableToEnd?.FirstOrDefault(n =>
                string.Equals(n.Id, srcId, StringComparison.OrdinalIgnoreCase))
            ?? env.Connections.SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && string.Equals(n.Id, srcId, StringComparison.OrdinalIgnoreCase));
        if (srcNode == null) return null;

        var raw = env.Service.ResolveDynamicValueForExecution(srcNode, outKey, env);
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().Trim('"');
    }

    /// <summary>
    /// Kỳ vọng "true"/"1": coi trigger khớp nếu giá trị thực là truthy (true, 1, yes, số khác 0…).
    /// Kỳ vọng "false"/"0": coi khớp nếu falsy (false, 0, no, số 0…).
    /// </summary>
    private static bool IsTriggerMatched(string rawValue, string expectedValue)
    {
        var expected = string.IsNullOrWhiteSpace(expectedValue)
            ? "true"
            : expectedValue.Trim().Trim('"');

        if (IsTruthyExpectedToken(expected))
            return IsTruthyTriggerRaw(rawValue);

        if (IsFalsyExpectedToken(expected))
            return IsFalsyTriggerRaw(rawValue);

        var raw = rawValue.Trim().Trim('"');
        return string.Equals(raw, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTruthyExpectedToken(string expected) =>
        string.Equals(expected, "true", StringComparison.OrdinalIgnoreCase) || expected == "1";

    private static bool IsFalsyExpectedToken(string expected) =>
        string.Equals(expected, "false", StringComparison.OrdinalIgnoreCase) || expected == "0";

    private static bool IsTruthyTriggerRaw(string rawValue)
    {
        var r = rawValue.Trim().Trim('"');
        if (string.Equals(r, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(r, "yes", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(r, "on", StringComparison.OrdinalIgnoreCase)) return true;
        if (r == "1") return true;
        if (int.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i != 0;
        if (double.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return Math.Abs(d) > double.Epsilon;
        return false;
    }

    private static bool IsFalsyTriggerRaw(string rawValue)
    {
        var r = rawValue.Trim().Trim('"');
        if (string.Equals(r, "false", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(r, "no", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(r, "off", StringComparison.OrdinalIgnoreCase)) return true;
        if (r == "0") return true;
        if (int.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i == 0;
        if (double.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return Math.Abs(d) < double.Epsilon;
        return false;
    }

    private static void PublishCleanupMessage(KeyValueBridgeNode node, NodeExecutionEnvironment env, string message)
    {
        lock (node.ResolvedOutputsSyncRoot)
        {
            node.ResolvedOutputs.Clear();
            node.ResolvedOutputs["value"] = message;
        }
        var outPort = node.DynamicOutputs.FirstOrDefault(o =>
            string.Equals(o.Key, "value", StringComparison.OrdinalIgnoreCase));
        if (outPort != null)
            outPort.UserValueOverride = message;

        if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
            env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, node.Id, node.ResolvedOutputs);
    }

    private static async Task ExecutePassAsync(KeyValueBridgeNode node, NodeExecutionEnvironment env)
    {
        // values to append:
        // - primary "Value (append)" mapping (legacy/current)
        // - additional append sources configured in dialog
        var values = new List<string>();
        var primary = NormalizeBridgeValue(ResolveKeyIn(node, env));
        if (!string.IsNullOrWhiteSpace(primary) && !string.Equals(primary.Trim(), "_", StringComparison.Ordinal))
            values.Add(primary);
        values.AddRange(ResolveAdditionalKeyIns(node, env)
            .Select(NormalizeBridgeValue)
            .Where(v => !string.IsNullOrWhiteSpace(v) && !string.Equals(v!.Trim(), "_", StringComparison.Ordinal))!
            .Select(v => v!));
        values = values
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // channel key for store grouping (from "Key Identifier In" if configured, otherwise fallback to KvChannelKey)
        var channelKey = DecodeEscapedUnicodeLiterals(ResolveKvChannelKeyIn(node, env));
        if (string.IsNullOrWhiteSpace(channelKey))
            channelKey = node.KvChannelKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(channelKey))
        {
            PublishCleanupMessage(node, env, "KeyValueBridge skipped: missing Key Identifier");
            return;
        }

        if (values.Count == 0)
        {
            PublishCleanupMessage(node, env, "KeyValueBridge skipped: no append value resolved");
            return;
        }

        lock (node.ResolvedOutputsSyncRoot)
        {
            node.ResolvedOutputs.Clear();
            node.ResolvedOutputs["key"] = "{}"; // placeholder until we append to store below
        }

        var keyPort = node.DynamicOutputs.FirstOrDefault(o =>
            string.Equals(o.Key, "key", StringComparison.OrdinalIgnoreCase));
        if (keyPort != null)
            keyPort.UserValueOverride = "{}";

        // Append the value into KV store.
        // If this is a standalone/single-node run (RefreshOnly=true), we also need to refresh
        // related Get-mode nodes because RefreshOnly mode does NOT traverse downstream nodes.
        // Use a stable KV scope id so:
        // - parallel AsyncTask branches (same node id) append into the same bucket
        // - Get-mode timers can keep reading after the workflow traversal ends
        // NOTE: this may mix values if multiple workflow runs use the same node id concurrently.
        var kvRunId = $"kv:{node.Id}";
        if (!string.IsNullOrWhiteSpace(kvRunId))
        {
            // Append thread-safe so multiple parallel iterations end up in one list for the same channel key.
            foreach (var v in values)
                WorkflowKeyValueStore.Append(kvRunId, channelKey, v);

            // For UI / downstream, expose a dict snapshot: { "<channelKey>": [values...] }
            var raw = WorkflowKeyValueStore.GetSnapshot(kvRunId, channelKey);
            var dict = new System.Collections.Generic.Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [channelKey] = raw
            };
            var dictJson = JsonSerializer.Serialize(dict);

            lock (node.ResolvedOutputsSyncRoot)
            {
                node.ResolvedOutputs["key"] = dictJson;
            }

            if (keyPort != null)
                keyPort.UserValueOverride = dictJson;

            if (!env.RefreshOnly)
                env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, node.Id, node.ResolvedOutputs);

            // Refresh related Get-mode KeyValueBridge nodes when we're running Pass in standalone mode.
            if (env.RefreshOnly)
            {
                // Note: env.ReachableToEnd is built from all nodes when called by RunSingleNodeAsync.
                // So we can reliably find Get nodes even if there are no flow connections traversed.
                var getNodes = env.ReachableToEnd
                    .OfType<KeyValueBridgeNode>()
                    .Where(n => !n.IsPassKeyMode)
                    .Where(n =>
                        string.Equals(n.KvChannelKey?.Trim(), AllKeysTitle, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(n.SelectedSourceBridgeNodeId?.Trim(), node.Id, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(n.KvChannelKey) &&
                         string.Equals(n.KvChannelKey.Trim(), channelKey, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (getNodes.Count > 0)
                {
                    var allNodesForLookup = env.ReachableToEnd.ToList();
                    foreach (var getNode in getNodes)
                    {
                        if (env.CancellationToken.IsCancellationRequested) break;
                        await env.Service.ExecuteNodeLogicOnlyAsync(
                            getNode,
                            env.Connections,
                            env.CancellationToken,
                            allNodesForLookup: allNodesForLookup).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private static string ResolveKvRunIdForGet(KeyValueBridgeNode getNode, NodeExecutionEnvironment env)
    {
        if (env == null) return string.Empty;
        // Always read from the stable KV scope written by the selected Pass bridge.
        if (!string.IsNullOrWhiteSpace(getNode.SelectedSourceBridgeNodeId))
            return $"kv:{getNode.SelectedSourceBridgeNodeId.Trim()}";

        // Fallback: if user didn't select a Pass bridge, use Get node id as its own scope.
        return $"kv:{getNode.Id}";
    }

    private static KeyValueBridgeNode? ResolveSelectedPassBridgeNode(KeyValueBridgeNode self, NodeExecutionEnvironment env)
    {
        // Kept for backward compatibility with earlier logic; not required for stable scope now.
        if (string.IsNullOrWhiteSpace(self.SelectedSourceBridgeNodeId)) return null;
        var passId = self.SelectedSourceBridgeNodeId.Trim();
        return env.ReachableToEnd?
                   .FirstOrDefault(n =>
                       n is KeyValueBridgeNode &&
                       string.Equals(n.Id, passId, StringComparison.OrdinalIgnoreCase)) as KeyValueBridgeNode
               ?? env.Connections
                   .SelectMany(c => new[] { c.FromNode, c.ToNode })
                   .FirstOrDefault(n =>
                       n is KeyValueBridgeNode &&
                       string.Equals(n.Id, passId, StringComparison.OrdinalIgnoreCase)) as KeyValueBridgeNode;
    }

    private static async Task ExecuteGetAsync(KeyValueBridgeNode node, NodeExecutionEnvironment env, int pollMs)
    {
        var storeKey = node.KvChannelKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(storeKey))
            storeKey = ResolveKeyFromSelectedPassNode(node, env) ?? string.Empty;
        var kvRunId = ResolveKvRunIdForGet(node, env);

        // Run one-shot in two cases:
        // - PollInterval <= 0 (legacy one-shot behavior)
        // - Normal workflow traversal (RefreshOnly=false): avoid blocking flow forever.
        if (pollMs <= 0 || !env.RefreshOnly)
        {
            if (string.Equals(storeKey, AllKeysTitle, StringComparison.OrdinalIgnoreCase))
            {
                var all = WorkflowKeyValueStore.GetAllSnapshots(kvRunId);
                var displayAll = SerializeAllSnapshotsStable(all);

                lock (node.ResolvedOutputsSyncRoot)
                {
                    node.ResolvedOutputs.Clear();
                    node.ResolvedOutputs["value"] = displayAll;
                }

                var allValuePort = node.DynamicOutputs.FirstOrDefault(o =>
                    string.Equals(o.Key, "value", StringComparison.OrdinalIgnoreCase));
                if (allValuePort != null)
                    allValuePort.UserValueOverride = displayAll;

                if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                    env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, node.Id, node.ResolvedOutputs);

                return;
            }

            // Read one key (default)
            object? raw = string.IsNullOrWhiteSpace(storeKey)
                ? null
                : WorkflowKeyValueStore.GetSnapshot(kvRunId, storeKey);

            // Expose as dict snapshot: { "<storeKey>": rawOrList } (raw can be null)
            var dict = new System.Collections.Generic.Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [storeKey] = raw
            };
            var display = JsonSerializer.Serialize(dict);

            lock (node.ResolvedOutputsSyncRoot)
            {
                node.ResolvedOutputs.Clear();
                node.ResolvedOutputs["value"] = display;
            }

            var valuePort = node.DynamicOutputs.FirstOrDefault(o =>
                string.Equals(o.Key, "value", StringComparison.OrdinalIgnoreCase));
            if (valuePort != null)
                valuePort.UserValueOverride = display;

            if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, node.Id, node.ResolvedOutputs);

            return;
        }

        // RefreshOnly (single-node run): poll for bounded attempts, update output, and then return.
        if (env.RefreshOnly)
        {
            // Single-node mode (RunSingleNodeCommand / internal refresh):
            // just read current snapshot once, update output, and traverse downstream
            // so consumers refresh immediately.
            if (string.Equals(storeKey, AllKeysTitle, StringComparison.OrdinalIgnoreCase))
            {
                var all = WorkflowKeyValueStore.GetAllSnapshots(kvRunId);
                var displayAll = SerializeAllSnapshotsStable(all);

                lock (node.ResolvedOutputsSyncRoot)
                {
                    node.ResolvedOutputs.Clear();
                    node.ResolvedOutputs["value"] = displayAll;
                }

                var allValuePort = node.DynamicOutputs.FirstOrDefault(o =>
                    string.Equals(o.Key, "value", StringComparison.OrdinalIgnoreCase));
                if (allValuePort != null)
                    allValuePort.UserValueOverride = displayAll;

                return;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(storeKey))
                    return;

                object? raw = WorkflowKeyValueStore.GetSnapshot(kvRunId, storeKey);

                var dict = new System.Collections.Generic.Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    [storeKey] = raw
                };
                var display = JsonSerializer.Serialize(dict);

                lock (node.ResolvedOutputsSyncRoot)
                {
                    node.ResolvedOutputs.Clear();
                    node.ResolvedOutputs["value"] = display;
                }

                var valuePort = node.DynamicOutputs.FirstOrDefault(o =>
                    string.Equals(o.Key, "value", StringComparison.OrdinalIgnoreCase));
                if (valuePort != null)
                    valuePort.UserValueOverride = display;

                return;
            }
        }

        // Polling interval > 0:
        // Keep polling and, when data becomes available / changes, re-trigger downstream nodes
        // so they always consume the latest KV snapshot while parallel AsyncTask branches are still running.
        string? lastEmittedDisplay = null;
        while (!env.CancellationToken.IsCancellationRequested)
        {
            bool shouldEmit = false;
            string? nextDisplay = null;

            // "Lấy tất cả"
            if (string.Equals(storeKey, AllKeysTitle, StringComparison.OrdinalIgnoreCase))
            {
                var all = WorkflowKeyValueStore.GetAllSnapshots(kvRunId);
                if (all.Count > 0)
                {
                    shouldEmit = true;
                    nextDisplay = SerializeAllSnapshotsStable(all);
                }
            }
            else
            {
                // Read one key (default)
                object? raw = string.IsNullOrWhiteSpace(storeKey)
                    ? null
                    : WorkflowKeyValueStore.GetSnapshot(kvRunId, storeKey);

                // raw == null => key not available yet (or appended null; in that case we won't emit).
                if (raw != null)
                {
                    shouldEmit = true;
                    var dict = new System.Collections.Generic.Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        [storeKey] = raw
                    };
                    nextDisplay = JsonSerializer.Serialize(dict);
                }
            }

            // Emit only when display string changes to avoid re-trigger storms.
            if (shouldEmit && !string.IsNullOrWhiteSpace(nextDisplay) &&
                !string.Equals(nextDisplay, lastEmittedDisplay, StringComparison.Ordinal))
            {
                lock (node.ResolvedOutputsSyncRoot)
                {
                    node.ResolvedOutputs.Clear();
                    node.ResolvedOutputs["value"] = nextDisplay;
                }

                var valuePort = node.DynamicOutputs.FirstOrDefault(o =>
                    string.Equals(o.Key, "value", StringComparison.OrdinalIgnoreCase));
                if (valuePort != null)
                    valuePort.UserValueOverride = nextDisplay;

                if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                    env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, node.Id, node.ResolvedOutputs);

                // Important: create a fresh execution environment so repeated traversals don't
                // artificially grow ExecutionPath and trip the infinite-loop guard.
                await TraverseOutputsWithFreshExecutionPathAsync(node, env).ConfigureAwait(false);
                lastEmittedDisplay = nextDisplay;
            }

            try
            {
                await Task.Delay(pollMs, env.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task TraverseOutputsWithFreshExecutionPathAsync(KeyValueBridgeNode node, NodeExecutionEnvironment env)
    {
        var freshEnv = new NodeExecutionEnvironment(
            service: env.Service,
            connections: env.Connections,
            cancellationToken: env.CancellationToken,
            onEnteringNode: env.OnEnteringNode,
            onNodeStarted: env.OnNodeStarted,
            onNodeCompleted: env.OnNodeCompleted,
            onNodeFailed: env.OnNodeFailed,
            incomingConnection: env.IncomingConnection,
            reachableToEnd: env.ReachableToEnd,
            // We must allow downstream execution even if the parent invocation is "RefreshOnly"
            // (single-node dialog / internal re-exec). Otherwise ExecuteNextAsync is a no-op.
            refreshOnly: false,
            isReuseRouteTerminal: env.IsReuseRouteTerminal,
            executionPath: new System.Collections.Generic.List<string>(),
            executionId: env.ExecutionId,
            flowScopeId: env.FlowScopeId,
            branchId: env.BranchId,
            parentFlowScopeId: env.ParentFlowScopeId);

        await freshEnv.TraverseOutputsAsync(node).ConfigureAwait(false);
    }

    private static string SerializeAllSnapshotsStable(IReadOnlyDictionary<string, object?> all)
    {
        if (all.Count == 0) return "{}";

        // Stable key ordering to prevent unnecessary downstream re-triggers.
        var ordered = all
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

        return JsonSerializer.Serialize(ordered);
    }

    private static int GetPollMs(KeyValueBridgeNode node)
    {
        if (node.PollIntervalValue <= 0) return 0;
        return node.PollIntervalUnit switch
        {
            KeyValueBridgePollUnit.Seconds => node.PollIntervalValue * 1000,
            KeyValueBridgePollUnit.Minutes => node.PollIntervalValue * 60_000,
            _ => node.PollIntervalValue
        };
    }

    private static string? ResolveKeyIn(KeyValueBridgeNode self, NodeExecutionEnvironment env)
    {
        var input = self.DynamicInputs.FirstOrDefault(i =>
            string.Equals(i.Key, "keyIn", StringComparison.OrdinalIgnoreCase));
        if (input == null || string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
            return null;

        var upstream = ResolveSourceNodePreferCurrentPath(self, env, input.SelectedSourceNodeId, input.SelectedSourceOutputKey);

        if (upstream == null) return null;
        var outKey = input.SelectedSourceOutputKey;
        if (string.IsNullOrWhiteSpace(outKey))
        {
            // Nếu combobox output key bị ẩn (vì upstream chỉ có 1 output key) thì SelectedSourceOutputKey có thể null.
            // Trường hợp này: lấy dynamic output key đầu tiên của upstream để resolve đúng.
            outKey = upstream.DynamicOutputs?.FirstOrDefault()?.Key ?? input.Key;
        }
        return env.Service.ResolveDynamicValueForExecution(upstream, outKey ?? "key", env);
    }

    private static IEnumerable<string?> ResolveAdditionalKeyIns(KeyValueBridgeNode self, NodeExecutionEnvironment env)
    {
        if (self?.AdditionalAppendSources == null || self.AdditionalAppendSources.Count == 0)
            yield break;

        var allNodes = env.Connections
            .SelectMany(c => new[] { c.FromNode, c.ToNode })
            .Where(n => n != null)
            .Distinct()
            .ToList();
        var pathByDistance = BuildExecutionPathFromIncoming(self, env);

        foreach (var src in self.AdditionalAppendSources)
        {
            if (src == null || string.IsNullOrWhiteSpace(src.SourceNodeId)) continue;
            WorkflowNode? upstream = null;
            var selectedId = src.SourceNodeId.Trim();

            // 1) Preferred: selected source node, but only when it is really in the current execution path.
            if (pathByDistance.ContainsKey(selectedId))
            {
                upstream = allNodes.FirstOrDefault(n => string.Equals(n!.Id, selectedId, StringComparison.OrdinalIgnoreCase));
            }

            // 2) Fallback: selected source exists globally but is not on this running branch -> pick node on
            // current path that can provide the same output key, so we don't read stale data from other branch.
            var requestedOutputKey = string.IsNullOrWhiteSpace(src.SourceOutputKey)
                ? null
                : src.SourceOutputKey!.Trim();
            if (upstream == null && pathByDistance.Count > 0)
            {
                upstream = pathByDistance
                    .OrderBy(kv => kv.Value)
                    .Select(kv => allNodes.FirstOrDefault(n => string.Equals(n!.Id, kv.Key, StringComparison.OrdinalIgnoreCase)))
                    .FirstOrDefault(n =>
                        n != null &&
                        n.DynamicOutputs != null &&
                        n.DynamicOutputs.Count > 0 &&
                        (string.IsNullOrWhiteSpace(requestedOutputKey) ||
                         n.DynamicOutputs.Any(o =>
                             !string.IsNullOrWhiteSpace(o.Key) &&
                             string.Equals(o.Key, requestedOutputKey, StringComparison.OrdinalIgnoreCase))));
            }

            // 3) Last fallback: keep previous behavior for legacy cases where path cannot be determined.
            if (upstream == null)
            {
                upstream = allNodes.FirstOrDefault(n => string.Equals(n!.Id, selectedId, StringComparison.OrdinalIgnoreCase));
            }
            if (upstream == null) continue;

            var outKey = src.SourceOutputKey;
            if (string.IsNullOrWhiteSpace(outKey))
                outKey = upstream.DynamicOutputs?.FirstOrDefault()?.Key ?? "key";

            yield return env.Service.ResolveDynamicValueForExecution(upstream, outKey, env);
        }
    }

    private static Dictionary<string, int> BuildExecutionPathFromIncoming(KeyValueBridgeNode self, NodeExecutionEnvironment env)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var incoming = env?.IncomingConnection?.FromNode;
        if (incoming == null || env?.Connections == null) return result;

        var queue = new Queue<(WorkflowNode Node, int Distance)>();
        queue.Enqueue((incoming, 1));
        result[incoming.Id] = 1;

        while (queue.Count > 0)
        {
            var (current, distance) = queue.Dequeue();
            var predecessors = env.Connections
                .Where(c => c.ToNode == current && c.FromNode != null && c.FromNode != self)
                .Select(c => c.FromNode!)
                .Distinct()
                .ToList();

            foreach (var prev in predecessors)
            {
                if (string.IsNullOrWhiteSpace(prev.Id)) continue;
                if (result.ContainsKey(prev.Id)) continue;
                result[prev.Id] = distance + 1;
                queue.Enqueue((prev, distance + 1));
            }
        }

        return result;
    }

    private static string? ResolveKvChannelKeyIn(KeyValueBridgeNode self, NodeExecutionEnvironment env)
    {
        var input = self.DynamicInputs.FirstOrDefault(i =>
            string.Equals(i.Key, "kvChannelKeyIn", StringComparison.OrdinalIgnoreCase));
        if (input == null || string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
            return null;

        var upstream = ResolveSourceNodePreferCurrentPath(self, env, input.SelectedSourceNodeId, input.SelectedSourceOutputKey);

        if (upstream == null) return null;

        var outKey = input.SelectedSourceOutputKey;
        if (string.IsNullOrWhiteSpace(outKey))
        {
            outKey = upstream.DynamicOutputs?.FirstOrDefault()?.Key ?? input.Key;
        }
        return env.Service.ResolveDynamicValueForExecution(upstream, outKey ?? "key", env);
    }

    private static WorkflowNode? ResolveSourceNodePreferCurrentPath(
        KeyValueBridgeNode self,
        NodeExecutionEnvironment env,
        string selectedSourceNodeId,
        string? selectedOutputKey)
    {
        var selectedId = selectedSourceNodeId?.Trim();
        if (string.IsNullOrWhiteSpace(selectedId)) return null;

        var allNodes = env.Connections
            .SelectMany(c => new[] { c.FromNode, c.ToNode })
            .Where(n => n != null)
            .Distinct()
            .ToList();

        var pathByDistance = BuildExecutionPathFromIncoming(self, env);
        WorkflowNode? upstream = null;

        // Preferred selected node only when it belongs to current branch path.
        if (pathByDistance.ContainsKey(selectedId))
        {
            upstream = allNodes.FirstOrDefault(n => string.Equals(n!.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        }

        // Fallback to nearest node in current path that can provide selected output key.
        var requestedOutputKey = string.IsNullOrWhiteSpace(selectedOutputKey) ? null : selectedOutputKey.Trim();
        if (upstream == null && pathByDistance.Count > 0)
        {
            upstream = pathByDistance
                .OrderBy(kv => kv.Value)
                .Select(kv => allNodes.FirstOrDefault(n => string.Equals(n!.Id, kv.Key, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(n =>
                    n != null &&
                    n.DynamicOutputs != null &&
                    n.DynamicOutputs.Count > 0 &&
                    (string.IsNullOrWhiteSpace(requestedOutputKey) ||
                     n.DynamicOutputs.Any(o =>
                         !string.IsNullOrWhiteSpace(o.Key) &&
                         string.Equals(o.Key, requestedOutputKey, StringComparison.OrdinalIgnoreCase))));
        }

        // Legacy fallback.
        return upstream ?? allNodes.FirstOrDefault(n => string.Equals(n!.Id, selectedId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveKeyFromSelectedPassNode(KeyValueBridgeNode self, NodeExecutionEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(self.SelectedSourceBridgeNodeId)) return null;
        var n = env.ReachableToEnd?.FirstOrDefault(x =>
                     string.Equals(x.Id, self.SelectedSourceBridgeNodeId, StringComparison.OrdinalIgnoreCase))
                ?? env.Connections.SelectMany(c => new[] { c.FromNode, c.ToNode })
                    .FirstOrDefault(x => x != null && string.Equals(x.Id, self.SelectedSourceBridgeNodeId, StringComparison.OrdinalIgnoreCase));
        if (n is not KeyValueBridgeNode kb) return null;

        // Resolve "effective" channel key the same way ExecutePass does:
        // prefer mapping input kvChannelKeyIn; fallback to kb.KvChannelKey textbox.
        var effective = DecodeEscapedUnicodeLiterals(ResolveKvChannelKeyIn(kb, env));
        if (!string.IsNullOrWhiteSpace(effective))
            return effective.Trim();

        return kb.KvChannelKey?.Trim();
    }

    private static string? DecodeEscapedUnicodeLiterals(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Some upstream nodes emit JSON-escaped text (\", \r\n, \uXXXX).
        // Run a small unescape pass (max 2 rounds) to handle single/double-escaped values safely.
        var current = input;
        for (var round = 0; round < 2; round++)
        {
            var next = DecodeSingleEscapePass(current);
            if (string.Equals(next, current, StringComparison.Ordinal))
                break;
            current = next;
        }

        return current;
    }

    private static string DecodeSingleEscapePass(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (input.IndexOf('\\') < 0) return input;

        var sb = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '\\' && i + 1 < input.Length)
            {
                var e = input[i + 1];
                switch (e)
                {
                    case '"': sb.Append('\"'); i++; continue;
                    case '\\': sb.Append('\\'); i++; continue;
                    case '/': sb.Append('/'); i++; continue;
                    case 'b': sb.Append('\b'); i++; continue;
                    case 'f': sb.Append('\f'); i++; continue;
                    case 'n': sb.Append('\n'); i++; continue;
                    case 'r': sb.Append('\r'); i++; continue;
                    case 't': sb.Append('\t'); i++; continue;
                    case 'u':
                    case 'U':
                        if (i + 5 < input.Length)
                        {
                            var hex = input.Substring(i + 2, 4);
                            if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp))
                            {
                                sb.Append((char)cp);
                                i += 5;
                                continue;
                            }
                        }
                        break;
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string? NormalizeBridgeValue(string? input)
    {
        var decoded = DecodeEscapedUnicodeLiterals(input);
        if (string.IsNullOrWhiteSpace(decoded)) return decoded;

        // Common case from Output node: {"<guid>":"<real payload>"}
        // Unwrap only this strict shape to avoid changing normal JSON object payloads.
        try
        {
            var trimmed = decoded.Trim();
            if (!(trimmed.StartsWith("{", StringComparison.Ordinal) &&
                  trimmed.EndsWith("}", StringComparison.Ordinal)))
                return decoded;

            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return decoded;

            using var e = doc.RootElement.EnumerateObject();
            if (!e.MoveNext()) return decoded;
            var first = e.Current;
            if (e.MoveNext()) return decoded; // more than one property -> keep original

            if (!Guid.TryParse(first.Name, out _)) return decoded;
            if (first.Value.ValueKind != JsonValueKind.String) return decoded;

            return DecodeEscapedUnicodeLiterals(first.Value.GetString());
        }
        catch
        {
            return decoded;
        }
    }
}
