using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- Shared utility methods --

    private static void EnsureLoopBodyPortsExist(LoopBodyNode bodyNode)
    {
        if (bodyNode.Ports.All(p => p.Id != "LoopBodyTop"))
        {
            bodyNode.Ports.Add(new NodePort
            {
                Id = "LoopBodyTop",
                IsInput = true,
                Position = PortPosition.Top,
                IsVisible = true,
                CanDeleteConnection = false
            });
        }

        if (bodyNode.Ports.All(p => p.Id != "LoopBodyLeft"))
        {
            bodyNode.Ports.Add(new NodePort
            {
                Id = "LoopBodyLeft",
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true
            });
        }

        if (bodyNode.Ports.All(p => p.Id != "LoopBodyRight"))
        {
            bodyNode.Ports.Add(new NodePort
            {
                Id = "LoopBodyRight",
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true
            });
        }
    }

    private static void CopyLoopBodyPortId(LoopBodyNode from, LoopBodyNode to, string portId)
    {
        var src = from.Ports.FirstOrDefault(p => p.Id == portId);
        var dst = to.Ports.FirstOrDefault(p => p.Id == portId);
        if (src == null || dst == null) return;
        dst.Id = src.Id;
    }

    private static void CopyBodyPortId(WorkflowNode from, WorkflowNode to, string portId)
    {
        var src = from.Ports.FirstOrDefault(p => p.Id == portId);
        var dst = to.Ports.FirstOrDefault(p => p.Id == portId);
        if (src == null || dst == null) return;
        dst.Id = src.Id;
    }

    // -- RESTORE (Deserialize) --

    private static void RestoreLoopNodeProperties(LoopNode loop, Dictionary<string, object> properties)
    {
        if (properties.TryGetValue("LoopType", out var typeObj))
            loop.LoopType = Enum.Parse<LoopType>(typeObj.ToString()!);
        if (properties.TryGetValue("RepeatCount", out var rc))
            loop.RepeatCount = int.Parse(rc.ToString()!);
        if (properties.TryGetValue("StartIndex", out var si))
            loop.StartIndex = int.Parse(si.ToString()!);
        if (properties.TryGetValue("EndIndex", out var ei))
            loop.EndIndex = int.Parse(ei.ToString()!);
        if (properties.TryGetValue("ArrayInputKey", out var aik))
            loop.ArrayInputKey = aik?.ToString() ?? "array";
        if (properties.TryGetValue("InputType", out var it))
        {
            if (Enum.TryParse<WorkflowDataType>(it?.ToString(), out var inputType))
                loop.InputType = inputType;
        }

        // CustomOutputMappings
        if (properties.TryGetValue("CustomOutputMappings", out var comObj) && comObj != null)
        {
            try
            {
                var json = comObj is string s ? s : (comObj is System.Text.Json.JsonElement je ? je.GetString() : null);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<LoopCustomOutputMapping>>(json);
                    if (list != null) { loop.CustomOutputMappings.Clear(); foreach (var m in list) loop.CustomOutputMappings.Add(m); }
                }
            }
            catch { }
        }
        // DataAssignments
        if (properties.TryGetValue("DataAssignments", out var daObj) && daObj != null)
        {
            try
            {
                var json = daObj is string s ? s : (daObj is System.Text.Json.JsonElement je ? je.GetString() : null);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<LoopDataAssignment>>(json);
                    if (list != null) { loop.DataAssignments.Clear(); foreach (var a in list) loop.DataAssignments.Add(a); }
                }
            }
            catch { }
        }
    }

    private static void RestoreLoopBodyNodeProperties(LoopBodyNode loopBody, Dictionary<string, object> properties)
    {
        if (properties.TryGetValue("Width", out var w) && double.TryParse(w.ToString(),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wVal))
        {
            loopBody.Width = Math.Max(100, wVal);
        }
        if (properties.TryGetValue("Height", out var h) && double.TryParse(h.ToString(),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hVal))
        {
            loopBody.Height = Math.Max(80, hVal);
        }
    }

    private void RestoreAsyncTaskNodeProperties(AsyncTaskNode asyncTaskNode, Dictionary<string, object> properties)
    {
        // Deserialize RunInParallel trÆ°á»›c (cáº§n dÃ¹ng khi táº¡o port má»›i)
        if (properties.TryGetValue("RunInParallel", out var runInParallelObj))
        {
            if (bool.TryParse(runInParallelObj?.ToString(), out var runInParallel))
            {
                asyncTaskNode.RunInParallel = runInParallel;
            }
        }

        if (properties.TryGetValue("UiPresentationMode", out var uimObj) &&
            Enum.TryParse<AsyncTaskUiPresentationMode>(uimObj?.ToString(), out var uim))
            asyncTaskNode.UiPresentationMode = uim;

        if (properties.TryGetValue("DispatchLoopType", out var dltObj) &&
            Enum.TryParse<LoopType>(dltObj?.ToString(), out var dlt))
            asyncTaskNode.DispatchLoopType = dlt;

        if (properties.TryGetValue("RepeatCount", out var atRcObj) && int.TryParse(atRcObj?.ToString(), out var atRc))
            asyncTaskNode.RepeatCount = atRc;
        if (properties.TryGetValue("StartIndex", out var atSiObj) && int.TryParse(atSiObj?.ToString(), out var atSi))
            asyncTaskNode.StartIndex = atSi;
        if (properties.TryGetValue("EndIndex", out var atEiObj) && int.TryParse(atEiObj?.ToString(), out var atEi))
            asyncTaskNode.EndIndex = atEi;
        if (properties.TryGetValue("ReadResultsInBody", out var inBodyObj) &&
            bool.TryParse(inBodyObj?.ToString(), out var inBody))
            asyncTaskNode.ReadResultsInBody = inBody;

        if (asyncTaskNode.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
            _templateFactory.ConfigureAsyncTaskLoopLikePorts(asyncTaskNode);

        // Deserialize AsyncTaskBranches (cháº¿ Ä‘á»™ nhÃ¡nh tay)
        if (asyncTaskNode.UiPresentationMode == AsyncTaskUiPresentationMode.ManualBranches &&
            properties.TryGetValue("AsyncTaskBranches", out var asyncBranchesObj))
        {
            List<Dictionary<string, object>>? branchList = null;
            if (asyncBranchesObj is string jsonStr)
            {
                try { branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonStr); } catch { }
            }
            else if (asyncBranchesObj is JsonElement je)
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
            if (branchList != null && asyncTaskNode.AsyncTaskBranches != null && branchList.Count > 0)
            {
                // Khi load, template chá»‰ cÃ³ 1 task. Náº¿u Ä‘Ã£ lÆ°u thÃªm task thÃ¬ táº¡o Ä‘á»§ task trÆ°á»›c khi restore.
                var portPosition = asyncTaskNode.AsyncTaskBranches.FirstOrDefault(b => b.Port != null)?.Port?.Position ?? PortPosition.Right;
                var executionMode = asyncTaskNode.RunInParallel ? PortExecutionMode.Parallel : PortExecutionMode.Sequential;
                while (asyncTaskNode.AsyncTaskBranches.Count < branchList.Count)
                {
                    var newBranch = new AsyncTaskBranch
                    {
                        Label = "Task",
                        CanRemove = true
                    };
                    var newPort = new NodePort
                    {
                        IsInput = false,
                        Position = portPosition,
                        IsVisible = true,
                        ExecutionMode = executionMode
                    };
                    newBranch.Port = newPort;
                    asyncTaskNode.Ports.Add(newPort);
                    asyncTaskNode.AsyncTaskBranches.Add(newBranch);
                }
                for (int i = 0; i < branchList.Count && i < asyncTaskNode.AsyncTaskBranches.Count; i++)
                {
                    var d = branchList[i];
                    var branch = asyncTaskNode.AsyncTaskBranches[i];
                    if (d.TryGetValue("Id", out var v)) branch.Id = GetStringFromJsonValue(v) ?? branch.Id;
                    if (d.TryGetValue("Label", out v)) branch.Label = GetStringFromJsonValue(v) ?? branch.Label;
                    if (d.TryGetValue("CanRemove", out v) && bool.TryParse(GetStringFromJsonValue(v), out var cr)) branch.CanRemove = cr;
                }

                // Äá»“ng bá»™ láº¡i ExecutionMode/ExecutionOrder cho toÃ n bá»™ task ports
                // theo RunInParallel Ä‘Ã£ restore Ä‘á»ƒ trÃ¡nh workflow cÅ© hoáº·c template lá»‡ch mode.
                var mode = asyncTaskNode.RunInParallel ? PortExecutionMode.Parallel : PortExecutionMode.Sequential;
                var order = 0;
                foreach (var b in asyncTaskNode.AsyncTaskBranches)
                {
                    if (b.Port == null) continue;
                    b.Port.ExecutionMode = mode;
                    b.Port.ExecutionOrder = order++;
                }
            }
        }
    }

    private static void RestoreAsyncTaskBodyNodeProperties(AsyncTaskBodyNode asyncTaskBodyPersist, Dictionary<string, object> properties)
    {
        if (properties.TryGetValue("Width", out var w) && double.TryParse(w.ToString(),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wVal))
            asyncTaskBodyPersist.Width = Math.Max(200, wVal);
        if (properties.TryGetValue("Height", out var h) && double.TryParse(h.ToString(),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hVal))
            asyncTaskBodyPersist.Height = Math.Max(200, hVal);
    }

    private static void RestoreAsyncTaskDispatchCollectNodeProperties(AsyncTaskDispatchCollectNode collectNode, Dictionary<string, object> properties)
    {
        if (properties.TryGetValue("SourceBodyNodeId", out var sbniObj))
        {
            var s = sbniObj?.ToString();
            if (!string.IsNullOrWhiteSpace(s))
                collectNode.SourceBodyNodeId = s;
        }

        if (properties.TryGetValue("SourceOutputKey", out var sokObj))
        {
            var s = sokObj?.ToString();
            if (!string.IsNullOrWhiteSpace(s))
                collectNode.SourceOutputKey = s;
        }
    }

    // -- GET (Serialize) --

    private static void GetLoopNodeProperties(LoopNode loop, Dictionary<string, object> dict)
    {
        dict["LoopType"] = loop.LoopType.ToString();
        dict["RepeatCount"] = loop.RepeatCount;
        dict["StartIndex"] = loop.StartIndex;
        dict["EndIndex"] = loop.EndIndex;
        dict["ArrayInputKey"] = loop.ArrayInputKey;
        dict["InputType"] = loop.InputType.ToString();

        if (loop.CustomOutputMappings.Count > 0)
            dict["CustomOutputMappings"] = System.Text.Json.JsonSerializer.Serialize(loop.CustomOutputMappings);
        if (loop.DataAssignments.Count > 0)
            dict["DataAssignments"] = System.Text.Json.JsonSerializer.Serialize(loop.DataAssignments);
    }

    private static void GetLoopBodyNodeProperties(LoopBodyNode loopBody, Dictionary<string, object> dict)
    {
        dict["Width"] = loopBody.Width;
        dict["Height"] = loopBody.Height;
    }

    private static void GetAsyncTaskBodyNodeProperties(AsyncTaskBodyNode asyncTaskBodyNode, Dictionary<string, object> dict)
    {
        dict["Width"] = asyncTaskBodyNode.Width;
        dict["Height"] = asyncTaskBodyNode.Height;
    }

    private static void GetAsyncTaskNodeProperties(AsyncTaskNode asyncTaskNode, Dictionary<string, object> dict)
    {
        dict["RunInParallel"] = asyncTaskNode.RunInParallel;
        dict["UiPresentationMode"] = asyncTaskNode.UiPresentationMode.ToString();
        dict["DispatchLoopType"] = asyncTaskNode.DispatchLoopType.ToString();
        dict["RepeatCount"] = asyncTaskNode.RepeatCount;
        dict["StartIndex"] = asyncTaskNode.StartIndex;
        dict["EndIndex"] = asyncTaskNode.EndIndex;
        dict["ReadResultsInBody"] = asyncTaskNode.ReadResultsInBody;

        if (asyncTaskNode.UiPresentationMode == AsyncTaskUiPresentationMode.ManualBranches
            && asyncTaskNode.AsyncTaskBranches != null && asyncTaskNode.AsyncTaskBranches.Count > 0)
        {
            var branchesJson = JsonSerializer.Serialize(asyncTaskNode.AsyncTaskBranches.Select(b => new
            {
                Id = b.Id,
                Label = b.Label,
                CanRemove = b.CanRemove
            }).ToList());
            dict["AsyncTaskBranches"] = branchesJson;
        }
    }


    private static void GetAsyncTaskDispatchCollectNodeProperties(AsyncTaskDispatchCollectNode collectNode, Dictionary<string, object> dict)
    {
        dict["SourceBodyNodeId"] = collectNode.SourceBodyNodeId ?? "";
        dict["SourceOutputKey"] = collectNode.SourceOutputKey ?? "";
    }
}
