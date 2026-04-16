using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Collections.Generic;
using System.Linq;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Map node KeyValueBridge đích → một hoặc nhiều bucket <c>kv:…</c> trong <see cref="WorkflowKeyValueStore"/> (Get đọc từ Pass; Pass ghi <c>kv:&lt;PassId&gt;</c>).
/// </summary>
public static class KeyValueBridgeKvScopeHelper
{
    /// <summary>
    /// <list type="bullet">
    /// <item>Đích là <b>Get</b>: <c>kv:&lt;GetId&gt;</c> + <c>kv:&lt;Pass nguồn&gt;</c> nếu có chọn nguồn Pass.</item>
    /// <item>Đích là <b>Pass</b>: <c>kv:&lt;PassId&gt;</c> + mọi <c>kv:&lt;GetId&gt;</c> đang có <see cref="KeyValueBridgeNode.SelectedSourceBridgeNodeId"/> trỏ tới Pass đó (scope phụ Get).</item>
    /// </list>
    /// </summary>
    public static List<string> ResolveKvRunIdsForTarget(string? targetBridgeNodeId, IEnumerable<WorkflowNode>? allNodes)
    {
        if (string.IsNullOrWhiteSpace(targetBridgeNodeId))
            return new List<string>();

        var tid = targetBridgeNodeId.Trim();
        var all = allNodes?.ToList() ?? new List<WorkflowNode>();
        var bridge = all.OfType<KeyValueBridgeNode>()
            .FirstOrDefault(b => string.Equals(b.Id, tid, StringComparison.OrdinalIgnoreCase));

        var result = new List<string> { $"kv:{tid}" };

        if (bridge == null)
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (bridge.IsPassKeyMode)
        {
            foreach (var g in all.OfType<KeyValueBridgeNode>())
            {
                if (g.IsPassKeyMode) continue;
                if (!string.Equals(g.SelectedSourceBridgeNodeId?.Trim(), tid, StringComparison.OrdinalIgnoreCase))
                    continue;
                result.Add($"kv:{g.Id.Trim()}");
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (!string.IsNullOrWhiteSpace(bridge.SelectedSourceBridgeNodeId))
        {
            var passId = bridge.SelectedSourceBridgeNodeId.Trim();
            if (!string.Equals(passId, tid, StringComparison.OrdinalIgnoreCase))
                result.Add($"kv:{passId}");
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
