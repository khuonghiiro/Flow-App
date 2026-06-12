using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // ===== RESTORE: Shared props applied to ALL nodes =====

    private static void RestoreSharedNodeProperties(WorkflowNode node, Dictionary<string, object> properties)
    {
        if (properties.TryGetValue("RunMode", out var runModeObj) &&
            Enum.TryParse<FlowRunMode>(runModeObj?.ToString(), out var parsedRunMode))
        {
            node.RunMode = parsedRunMode;
        }
        if (properties.TryGetValue("AutoRunIntervalValue", out var autoValObj) &&
            double.TryParse(autoValObj?.ToString(), out var autoVal))
        {
            node.AutoRunIntervalValue = autoVal;
        }
        if (properties.TryGetValue("AutoRunIntervalUnit", out var autoUnitObj) &&
            Enum.TryParse<AutoRunIntervalUnit>(autoUnitObj?.ToString(), out var autoUnit))
        {
            node.AutoRunIntervalUnit = autoUnit;
        }

        if (properties.TryGetValue("AutoScopeVisualPadding", out var aspObj) &&
            double.TryParse(aspObj?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var asp))
            node.AutoScopeVisualPadding = asp;
        if (properties.TryGetValue("AutoScopeFrameX", out var asfx) &&
            double.TryParse(asfx?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fx))
            node.AutoScopeFrameX = fx;
        if (properties.TryGetValue("AutoScopeFrameY", out var asfy) &&
            double.TryParse(asfy?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fy))
            node.AutoScopeFrameY = fy;
        if (properties.TryGetValue("AutoScopeFrameWidth", out var asfw) &&
            double.TryParse(asfw?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fw))
            node.AutoScopeFrameWidth = fw;
        if (properties.TryGetValue("AutoScopeFrameHeight", out var asfh) &&
            double.TryParse(asfh?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fh))
            node.AutoScopeFrameHeight = fh;

        if (properties.TryGetValue("EndBehavior", out var endBehaviorObj) &&
            Enum.TryParse<EndNodeBehavior>(endBehaviorObj?.ToString(), out var parsedEndBehavior))
        {
            node.EndBehavior = parsedEndBehavior;
        }

        if (properties.TryGetValue("DiamondSharpness", out var sharpObj) &&
            Enum.TryParse<DiamondSharpness>(sharpObj?.ToString(), out var parsedSharpness))
        {
            node.DiamondSharpness = parsedSharpness;
        }

        if (properties.TryGetValue("ConditionalVisualMode", out var conditionalVisualModeObj) &&
            Enum.TryParse<ConditionalVisualMode>(conditionalVisualModeObj?.ToString(), out var parsedConditionalVisualMode))
        {
            node.ConditionalVisualMode = parsedConditionalVisualMode;
        }
    }

    // ===== RESTORE: ReuseRoutes (áp dụng chung cho mọi loại node) =====

    private static void RestoreReuseRoutes(WorkflowNode node, Dictionary<string, object> properties)
    {
        if (!properties.TryGetValue("ReuseRoutes", out var rrObj) || rrObj == null) return;

        try
        {
            List<NodeReuseRoute>? parsed = null;

            if (rrObj is string s && !string.IsNullOrWhiteSpace(s))
            {
                parsed = JsonSerializer.Deserialize<List<NodeReuseRoute>>(s);
            }
            else if (rrObj is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    var s2 = je.GetString();
                    if (!string.IsNullOrWhiteSpace(s2))
                    {
                        parsed = JsonSerializer.Deserialize<List<NodeReuseRoute>>(s2);
                    }
                }
                else if (je.ValueKind == JsonValueKind.Array)
                {
                    parsed = JsonSerializer.Deserialize<List<NodeReuseRoute>>(je.GetRawText());
                }
            }

            if (parsed != null)
            {
                node.ReuseRoutes = parsed;
            }
        }
        catch
        {
            // Nếu parse lỗi thì bỏ qua, không chặn load workflow
        }
    }

    // ===== RESTORE: DynamicInputs (áp dụng chung cho mọi loại node) =====

    private static void RestoreDynamicInputProperties(WorkflowNode node, Dictionary<string, object> properties)
    {
        if (node.DynamicInputs == null || node.DynamicInputs.Count == 0) return;

        foreach (var inp in node.DynamicInputs)
        {
            var key = inp.Key ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)) continue;

            if (properties.TryGetValue($"DynIn_{key}_SrcNode", out var srcNodeObj))
            {
                var s = srcNodeObj?.ToString();
                inp.SelectedSourceNodeId = string.IsNullOrWhiteSpace(s) ? null : s;
            }

            if (properties.TryGetValue($"DynIn_{key}_SrcKey", out var srcKeyObj))
            {
                var k = srcKeyObj?.ToString();
                inp.SelectedSourceOutputKey = string.IsNullOrWhiteSpace(k) ? null : k;
            }

            if (properties.TryGetValue($"DynIn_{key}_UserKey", out var userKeyObj))
            {
                var uk = userKeyObj?.ToString();
                inp.UserKeyOverride = string.IsNullOrWhiteSpace(uk) ? null : uk;
            }

            if (properties.TryGetValue($"DynIn_{key}_UserValue", out var userValObj))
            {
                var uv = userValObj?.ToString();
                inp.UserValueOverride = string.IsNullOrWhiteSpace(uv) ? null : uv;
            }

            if (properties.TryGetValue($"DynIn_{key}_ConvType", out var ctObj))
            {
                var ct = ctObj?.ToString();
                if (!string.IsNullOrWhiteSpace(ct) &&
                    Enum.TryParse<WorkflowDataType>(ct, out var parsed))
                {
                    inp.ConvertType = parsed;
                }
            }
        }
    }

    // ===== RESTORE: Shared title properties (all nodes) =====

    private static void RestoreSharedTitleProperties(WorkflowNode node, Dictionary<string, object> properties)
    {
        if (properties.TryGetValue("TitleDisplayMode", out var sharedTdmObj) &&
            Enum.TryParse<TitleDisplayMode>(sharedTdmObj?.ToString(), out var sharedTdm))
            node.TitleDisplayMode = sharedTdm;

        if (properties.TryGetValue("TitleColorMode", out var sharedTcmObj) &&
            Enum.TryParse<TitleColorMode>(sharedTcmObj?.ToString(), out var sharedTcm))
            node.TitleColorMode = sharedTcm;

        if (properties.TryGetValue("TitleColorKey", out var sharedTckObj))
            node.TitleColorKey = sharedTckObj?.ToString();
    }

    // ===== GET: Shared header properties (all nodes) =====

    private static void GetSharedHeaderProperties(WorkflowNode node, Dictionary<string, object> dict)
    {
        if (!string.IsNullOrEmpty(node.Condition)) dict["Condition"] = node.Condition;
        if (!string.IsNullOrEmpty(node.Key)) dict["Key"] = node.Key;
        if (node.MouseEvent.HasValue) dict["MouseEvent"] = node.MouseEvent.Value.ToString();
        if (!string.IsNullOrEmpty(node.TargetElement)) dict["TargetElement"] = node.TargetElement;

        // FloatingWidget config (áp dụng cho mọi node type)
        if (node.FloatingWidget != null)
        {
            try
            {
                dict["FloatingWidget"] = JsonSerializer.Serialize(node.FloatingWidget);
            }
            catch { /* ignore */ }
        }
    }

    // ===== GET: ReuseRoutes (tái sử dụng flow + line style) cho mọi loại node =====

    private static void GetReuseRoutes(WorkflowNode node, Dictionary<string, object> dict)
    {
        if (node.ReuseRoutes == null || node.ReuseRoutes.Count == 0) return;

        try
        {
            var routesJson = JsonSerializer.Serialize(node.ReuseRoutes);
            dict["ReuseRoutes"] = routesJson;
        }
        catch
        {
            // best-effort, không chặn save nếu serialize lỗi
        }
    }

    // ===== GET: Shared footer properties (all nodes) =====

    private static void GetSharedFooterProperties(WorkflowNode node, Dictionary<string, object> dict)
    {
        if (!string.IsNullOrWhiteSpace(node.FlowScopeKey))
            dict["FlowScopeKey"] = node.FlowScopeKey;
        dict["RunMode"] = node.RunMode.ToString();
        dict["AutoRunIntervalValue"] = node.AutoRunIntervalValue;
        dict["AutoRunIntervalUnit"] = node.AutoRunIntervalUnit.ToString();
        dict["AutoScopeVisualPadding"] = node.AutoScopeVisualPadding.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["AutoScopeFrameX"] = node.AutoScopeFrameX.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["AutoScopeFrameY"] = node.AutoScopeFrameY.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["AutoScopeFrameWidth"] = node.AutoScopeFrameWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["AutoScopeFrameHeight"] = node.AutoScopeFrameHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["EndBehavior"] = node.EndBehavior.ToString();
        dict["DiamondSharpness"] = node.DiamondSharpness.ToString();
        if (node.IsConditionalNode)
            dict["ConditionalVisualMode"] = node.ConditionalVisualMode.ToString();
    }

    // ===== GET: DynamicInputs (all nodes) =====

    private static void GetDynamicInputProperties(WorkflowNode node, Dictionary<string, object> dict)
    {
        if (node.DynamicInputs == null || node.DynamicInputs.Count == 0) return;

        foreach (var inp in node.DynamicInputs)
        {
            var key = inp.Key ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)) continue;

            if (!string.IsNullOrWhiteSpace(inp.SelectedSourceNodeId))
                dict[$"DynIn_{key}_SrcNode"] = inp.SelectedSourceNodeId!;

            if (!string.IsNullOrWhiteSpace(inp.SelectedSourceOutputKey))
                dict[$"DynIn_{key}_SrcKey"] = inp.SelectedSourceOutputKey!;

            if (!string.IsNullOrWhiteSpace(inp.UserKeyOverride))
                dict[$"DynIn_{key}_UserKey"] = inp.UserKeyOverride!;

            if (!string.IsNullOrWhiteSpace(inp.UserValueOverride))
                dict[$"DynIn_{key}_UserValue"] = inp.UserValueOverride!;

            dict[$"DynIn_{key}_ConvType"] = inp.ConvertType.ToString();
        }
    }

    // ===== GET: Shared title properties (all nodes) =====

    private static void GetSharedTitleProperties(WorkflowNode node, Dictionary<string, object> dict)
    {
        dict["TitleDisplayMode"] = node.TitleDisplayMode.ToString();
        dict["TitleColorMode"] = node.TitleColorMode.ToString();
        if (!string.IsNullOrEmpty(node.TitleColorKey))
            dict["TitleColorKey"] = node.TitleColorKey;
    }
}
