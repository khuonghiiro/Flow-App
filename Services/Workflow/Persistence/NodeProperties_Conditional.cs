using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreConditionalNodeProperties(WorkflowNode node, Dictionary<string, object> properties)
    {
        if (!properties.TryGetValue("ConditionalBranches", out var branchesObj)) return;

        List<Dictionary<string, object>>? branchList = null;
        if (branchesObj is string jsonStr)
        {
            try { branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonStr); } catch { }
        }
        else if (branchesObj is JsonElement je)
        {
            try
            {
                if (je.ValueKind == JsonValueKind.String)
                    branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(je.GetString() ?? "[]");
                else if (je.ValueKind == JsonValueKind.Array)
                    branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(je.GetRawText());
            }
            catch { }
        }
        if (branchList != null && node.ConditionalBranches != null && branchList.Count > 0)
        {
            // Khi load, template chỉ có if + else. Nếu đã lưu thêm "else if" thì tạo đủ nhánh trước khi restore.
            var portPosition = node.ConditionalBranches.FirstOrDefault(b => b.Port != null)?.Port?.Position ?? PortPosition.Right;
            while (node.ConditionalBranches.Count < branchList.Count)
            {
                int elseIndex = node.ConditionalBranches.FindIndex(b => b.Label == "else");
                if (elseIndex < 0) elseIndex = node.ConditionalBranches.Count;
                var newBranch = new ConditionalBranch
                {
                    Label = "else if",
                    Condition = "condition",
                    CanRemove = true
                };
                var newPort = new NodePort
                {
                    IsInput = false,
                    Position = portPosition,
                    IsVisible = true,
                    ExecutionMode = PortExecutionMode.Sequential
                };
                newBranch.Port = newPort;
                node.Ports.Add(newPort);
                node.ConditionalBranches.Insert(elseIndex, newBranch);
            }
            for (int i = 0; i < branchList.Count && i < node.ConditionalBranches.Count; i++)
            {
                var d = branchList[i];
                var branch = node.ConditionalBranches[i];
                if (d.TryGetValue("Label", out var v)) branch.Label = GetStringFromJsonValue(v) ?? branch.Label;
                if (d.TryGetValue("DisplayTitle", out v)) branch.DisplayTitle = GetStringFromJsonValue(v);
                if (d.TryGetValue("SatelliteOffsetX", out v))
                {
                    var sx = GetStringFromJsonValue(v);
                    if (double.TryParse(sx, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedSx))
                        branch.SatelliteOffsetX = parsedSx;
                    else if (double.TryParse(sx, out parsedSx))
                        branch.SatelliteOffsetX = parsedSx;
                }
                if (d.TryGetValue("SatelliteOffsetY", out v))
                {
                    var sy = GetStringFromJsonValue(v);
                    if (double.TryParse(sy, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedSy))
                        branch.SatelliteOffsetY = parsedSy;
                    else if (double.TryParse(sy, out parsedSy))
                        branch.SatelliteOffsetY = parsedSy;
                }
                if (d.TryGetValue("SatelliteInputPosition", out v) &&
                    Enum.TryParse<PortPosition>(GetStringFromJsonValue(v), out var satInPos))
                {
                    branch.SatelliteInputPosition = satInPos;
                }
                if (d.TryGetValue("LeftSourceNodeId", out v)) branch.LeftSourceNodeId = GetStringFromJsonValue(v);
                if (d.TryGetValue("LeftKey", out v)) branch.LeftKey = GetStringFromJsonValue(v);
                if (d.TryGetValue("Operator", out v) && Enum.TryParse<ConditionOperator>(GetStringFromJsonValue(v), out var op)) branch.Operator = op;
                if (d.TryGetValue("RightUseLiteralValue", out v) && bool.TryParse(GetStringFromJsonValue(v), out var ruv)) branch.RightUseLiteralValue = ruv;
                if (d.TryGetValue("RightLiteralValue", out v)) branch.RightLiteralValue = GetStringFromJsonValue(v);
                if (d.TryGetValue("RightSourceNodeId", out v)) branch.RightSourceNodeId = GetStringFromJsonValue(v);
                if (d.TryGetValue("RightKey", out v)) branch.RightKey = GetStringFromJsonValue(v);
                if (d.TryGetValue("Condition", out v)) branch.Condition = GetStringFromJsonValue(v);
                if (d.TryGetValue("CanRemove", out v) && bool.TryParse(GetStringFromJsonValue(v), out var cr)) branch.CanRemove = cr;
                if (d.TryGetValue("SubConditions", out v) && v is JsonElement se)
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(se.GetRawText());
                        if (list != null && list.Count > 0)
                        {
                            branch.SubConditions = list.Select(x =>
                            {
                                var expr = new ConditionExpression();
                                if (x.TryGetValue("LeftSourceNodeId", out var vx)) expr.LeftSourceNodeId = GetStringFromJsonValue(vx);
                                if (x.TryGetValue("LeftKey", out vx)) expr.LeftKey = GetStringFromJsonValue(vx);
                                if (x.TryGetValue("Operator", out vx) && Enum.TryParse<ConditionOperator>(GetStringFromJsonValue(vx), out var opx)) expr.Operator = opx;
                                if (x.TryGetValue("RightUseLiteralValue", out vx) && bool.TryParse(GetStringFromJsonValue(vx), out var ruvx)) expr.RightUseLiteralValue = ruvx;
                                if (x.TryGetValue("RightLiteralValue", out vx)) expr.RightLiteralValue = GetStringFromJsonValue(vx);
                                if (x.TryGetValue("RightSourceNodeId", out vx)) expr.RightSourceNodeId = GetStringFromJsonValue(vx);
                                if (x.TryGetValue("RightKey", out vx)) expr.RightKey = GetStringFromJsonValue(vx);
                                return expr;
                            }).ToList();
                        }
                    }
                    catch { }
                }
                if (d.TryGetValue("OperatorsBetween", out v) && v is JsonElement oe)
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<string>>(oe.GetRawText());
                        if (list != null && list.Count > 0)
                        {
                            branch.OperatorsBetween = list.Select(s => Enum.TryParse<LogicalOperator>(s, out var lop) ? lop : LogicalOperator.And).ToList();
                        }
                    }
                    catch { }
                }
            }
        }
    }

    // -- GET (Serialize) --

    private static void GetConditionalNodeProperties(WorkflowNode node, Dictionary<string, object> dict)
    {
        var branchesJson = JsonSerializer.Serialize(node.ConditionalBranches.Select(b => new
        {
            Label = b.Label,
            DisplayTitle = b.DisplayTitle,
            SatelliteOffsetX = b.SatelliteOffsetX,
            SatelliteOffsetY = b.SatelliteOffsetY,
            SatelliteInputPosition = b.SatelliteInputPosition.ToString(),
            LeftSourceNodeId = b.LeftSourceNodeId,
            LeftKey = b.LeftKey,
            Operator = b.Operator.ToString(),
            RightUseLiteralValue = b.RightUseLiteralValue,
            RightLiteralValue = b.RightLiteralValue,
            RightSourceNodeId = b.RightSourceNodeId,
            RightKey = b.RightKey,
            Condition = b.Condition,
            CanRemove = b.CanRemove,
            SubConditions = b.SubConditions?.Select(expr => new
            {
                LeftSourceNodeId = expr.LeftSourceNodeId,
                LeftKey = expr.LeftKey,
                Operator = expr.Operator.ToString(),
                RightUseLiteralValue = expr.RightUseLiteralValue,
                RightLiteralValue = expr.RightLiteralValue,
                RightSourceNodeId = expr.RightSourceNodeId,
                RightKey = expr.RightKey
            }).ToList(),
            OperatorsBetween = b.OperatorsBetween?.Select(o => o.ToString()).ToList()
        }).ToList());
        dict["ConditionalBranches"] = branchesJson;
    }
}
