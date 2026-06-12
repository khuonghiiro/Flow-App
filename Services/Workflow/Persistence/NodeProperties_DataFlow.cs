using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreInputNodeProperties(InputNode inputNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("InputKey", out var keyObj))
                inputNode.Key = keyObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("InputValue", out var valueObj))
                inputNode.Value = valueObj?.ToString() ?? string.Empty;

            if (!properties.TryGetValue("InputDataType", out var typeObj))
            {
                properties.TryGetValue("WorkflowDataType", out typeObj);
            }

            if (typeObj != null)
            {
                var typeStr = typeObj.ToString();
                if (!string.IsNullOrWhiteSpace(typeStr) &&
                    Enum.TryParse<WorkflowDataType>(typeStr, out var parsedType))
                {
                    inputNode.DataType = parsedType;
                }
            }

            // Ensure DynamicOutputs is initialized for InputNode so that downstream
            // dialogs (WebNode, CodeNode, etc.) can list this node as a data source.
            // Older workflows may not have DynamicOutputs serialized.
            if (inputNode.DynamicOutputs == null || inputNode.DynamicOutputs.Count == 0)
            {
                var outputKey = string.IsNullOrWhiteSpace(inputNode.Key) ? "Input" : inputNode.Key;
                inputNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = outputKey,
                    DisplayName = "Value",
                    IsMultiple = false,
                    OutputType = inputNode.DataType,
                    ConvertType = inputNode.DataType
                });
            }

            if (properties.TryGetValue("InputArrayValues", out var arrayValuesObj))
            {
                List<string>? parsedArray = null;

                if (arrayValuesObj is string jsonArray)
                {
                    try
                    {
                        parsedArray = JsonSerializer.Deserialize<List<string>>(jsonArray);
                    }
                    catch { }
                }
                else if (arrayValuesObj is JsonElement jsonElement)
                {
                    try
                    {
                        if (jsonElement.ValueKind == JsonValueKind.String)
                        {
                            var jsonString = jsonElement.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonString))
                            {
                                parsedArray = JsonSerializer.Deserialize<List<string>>(jsonString);
                            }
                        }
                        else if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            parsedArray = JsonSerializer.Deserialize<List<string>>(jsonElement.GetRawText());
                        }
                    }
                    catch { }
                }
                else if (arrayValuesObj is List<object> list)
                {
                    parsedArray = list.Select(x => x?.ToString() ?? string.Empty).ToList();
                }

                if (parsedArray != null && inputNode.IsArrayType)
                {
                    inputNode.ArrayValues = parsedArray;
                }
            }

    }

    private static void RestoreDelayNodeProperties(DelayNode delayNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("DelayMilliseconds", out var delayObj) &&
                int.TryParse(delayObj?.ToString(), out var delayMs))
            {
                delayNode.DelayMilliseconds = delayMs;
            }

            // UI display settings (optional - older workflows may not have these)
            if (properties.TryGetValue("DelayUnit", out var unitObj))
            {
                var unitStr = unitObj?.ToString();
                if (!string.IsNullOrWhiteSpace(unitStr) &&
                    Enum.TryParse<DelayTimeUnit>(unitStr, out var parsedUnit))
                {
                    delayNode.DelayUnit = parsedUnit;
                }
            }

            if (properties.TryGetValue("DelayValue", out var valObj))
            {
                var valStr = valObj?.ToString();
                if (!string.IsNullOrWhiteSpace(valStr) &&
                    double.TryParse(valStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.CurrentCulture, out var parsedVal))
                {
                    delayNode.DelayValue = parsedVal;
                }
            }
            else
            {
                // Fallback: derive display value from milliseconds with current unit (default = seconds)
                var multiplier = delayNode.DelayUnit switch
                {
                    DelayTimeUnit.Milliseconds => 1d,
                    DelayTimeUnit.Seconds => 1000d,
                    DelayTimeUnit.Minutes => 60_000d,
                    DelayTimeUnit.Hours => 3_600_000d,
                    _ => 1000d
                };
                delayNode.DelayValue = multiplier <= 0 ? 0 : delayNode.DelayMilliseconds / multiplier;
            }

            if (properties.TryGetValue("TimingMode", out var tmObj) &&
                Enum.TryParse<DelayTimingMode>(tmObj?.ToString(), out var tm))
            {
                delayNode.TimingMode = tm;
            }

            if (properties.TryGetValue("RandomMinValue", out var rminObj) &&
                double.TryParse(rminObj?.ToString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var rmin))
            {
                delayNode.RandomMinValue = rmin;
            }

            if (properties.TryGetValue("RandomMaxValue", out var rmaxObj) &&
                double.TryParse(rmaxObj?.ToString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var rmax))
            {
                delayNode.RandomMaxValue = rmax;
            }

            if (properties.TryGetValue("DelaySourceNodeId", out var dsnObj))
                delayNode.DelaySourceNodeId = dsnObj?.ToString() ?? string.Empty;

            if (properties.TryGetValue("DelaySourceOutputKey", out var dskObj))
                delayNode.DelaySourceOutputKey = dskObj?.ToString() ?? string.Empty;
    }

    private static void RestoreCallbackNodeProperties(CallbackNode callbackNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("TargetNodeId", out var targetObj))
            {
                callbackNode.TargetNodeId = targetObj?.ToString() ?? string.Empty;
            }

            if (properties.TryGetValue("MaxCallbackCount", out var maxCountObj) &&
                int.TryParse(maxCountObj?.ToString(), out var maxCount))
            {
                callbackNode.MaxCallbackCount = maxCount;
            }

            if (properties.TryGetValue("FlowBehavior", out var flowBehaviorObj))
            {
                var flowBehaviorStr = flowBehaviorObj?.ToString();
                if (!string.IsNullOrWhiteSpace(flowBehaviorStr) &&
                    Enum.TryParse<CallbackFlowBehavior>(flowBehaviorStr, out var parsedBehavior))
                {
                    callbackNode.FlowBehavior = parsedBehavior;
                }
            }

            callbackNode.SyncPortsForBehavior();
    }

    private static void RestoreListOutNodeProperties(ListOutNode listOutNode, Dictionary<string, object> properties)
    {
            // Deserialize OutputMappings
            if (properties.TryGetValue("OutputMappings", out var mappingsObj))
            {
                List<OutputMapping>? parsedMappings = null;

                if (mappingsObj is string jsonMappings)
                {
                    try
                    {
                        var mappingData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonMappings);
                        if (mappingData != null)
                        {
                            parsedMappings = mappingData.Select(m => new OutputMapping
                            {
                                NewKey = m.TryGetValue("NewKey", out var nk) ? nk?.ToString() ?? string.Empty : string.Empty,
                                SourceNodeId = m.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                                SourceOutputKey = m.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                            }).ToList();
                        }
                    }
                    catch
                    {
                        // Try alternative format (array of objects)
                        try
                        {
                            var mappingData = JsonSerializer.Deserialize<List<OutputMapping>>(jsonMappings);
                            parsedMappings = mappingData;
                        }
                        catch { }
                    }
                }
                else if (mappingsObj is JsonElement jsonElement)
                {
                    try
                    {
                        if (jsonElement.ValueKind == JsonValueKind.String)
                        {
                            var jsonString = jsonElement.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonString))
                            {
                                var mappingData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonString);
                                if (mappingData != null)
                                {
                                    parsedMappings = mappingData.Select(m => new OutputMapping
                                    {
                                        NewKey = m.TryGetValue("NewKey", out var nk) ? nk?.ToString() ?? string.Empty : string.Empty,
                                        SourceNodeId = m.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                                        SourceOutputKey = m.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                                    }).ToList();
                                }
                            }
                        }
                        else if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            parsedMappings = JsonSerializer.Deserialize<List<OutputMapping>>(jsonElement.GetRawText());
                        }
                    }
                    catch { }
                }

                if (parsedMappings != null)
                {
                    listOutNode.OutputMappings = parsedMappings;
                    listOutNode.RebuildDynamicOutputs();
                }
            }

    }

    private static void RestoreAssignDataNodeProperties(AssignDataNode assignDataNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("Assignments", out var assignObj) && assignObj != null)
            {
                try
                {
                    var json = assignObj is string s ? s : (assignObj is JsonElement je ? je.GetString() : null);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = JsonSerializer.Deserialize<List<AssignDataAssignment>>(json);
                        if (list != null)
                        {
                            assignDataNode.Assignments.Clear();
                            foreach (var a in list) assignDataNode.Assignments.Add(a);
                        }
                    }
                }
                catch { }
            }

    }

    private static void RestoreStorageNodeProperties(StorageNode storageNode, Dictionary<string, object> properties)
    {
            // StoredOutputs
            if (properties.TryGetValue("StoredOutputs", out var soObj) && soObj != null)
            {
                try
                {
                    storageNode.StoredOutputs.Clear();

                    if (soObj is JsonElement soJe && soJe.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in soJe.EnumerateObject())
                        {
                            storageNode.StoredOutputs[p.Name] = p.Value.ValueKind == JsonValueKind.Null
                                ? null
                                : p.Value.ToString();
                        }
                    }
                    else if (soObj is IDictionary<string, object?> soDict)
                    {
                        foreach (var kv in soDict)
                        {
                            storageNode.StoredOutputs[kv.Key] = kv.Value?.ToString();
                        }
                    }
                }
                catch
                {
                    // ignore – best effort
                }
            }

            // OutputKeys -> rebuild DynamicOutputs
            if (properties.TryGetValue("OutputKeys", out var okObj) && okObj != null)
            {
                try
                {
                    List<string>? keys = null;
                    if (okObj is JsonElement okJe)
                    {
                        if (okJe.ValueKind == JsonValueKind.Array)
                        {
                            keys = new List<string>();
                            foreach (var item in okJe.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    var s = item.GetString();
                                    if (!string.IsNullOrWhiteSpace(s))
                                        keys.Add(s);
                                }
                            }
                        }
                    }
                    else if (okObj is IEnumerable<string> keyEnum)
                    {
                        keys = keyEnum.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
                    }

                    if (keys != null && keys.Count > 0)
                    {
                        storageNode.DynamicOutputs.Clear();
                        foreach (var k in keys)
                        {
                            storageNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                            {
                                Key = k,
                                DisplayName = k,
                                IsMultiple = false,
                                OutputType = WorkflowDataType.String
                            });
                        }
                    }

                    // Sync StoredOutputs -> UserValueOverride
                    foreach (var kv in storageNode.StoredOutputs)
                    {
                        var output = storageNode.DynamicOutputs.FirstOrDefault(o =>
                            string.Equals(o.Key, kv.Key, StringComparison.OrdinalIgnoreCase));
                        if (output != null)
                        {
                            output.UserValueOverride = kv.Value;
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            // SourceNodeId / SourceOutputKey
            if (properties.TryGetValue("SourceNodeId", out var snObj))
            {
                var s = snObj?.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                    storageNode.SourceNodeId = s;
            }
            if (properties.TryGetValue("SourceOutputKey", out var sokObj))
            {
                var s = sokObj?.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                    storageNode.SourceOutputKey = s;
            }

            // IsInputMode
            if (properties.TryGetValue("IsInputMode", out var isInputModeObj))
            {
                if (isInputModeObj is bool isInputMode)
                {
                    storageNode.IsInputMode = isInputMode;
                }
                else if (bool.TryParse(isInputModeObj?.ToString(), out var parsed))
                {
                    storageNode.IsInputMode = parsed;
                }
            }

            // Update port visibility based on IsInputMode after loading
            foreach (var port in storageNode.Ports)
            {
                bool shouldShowPort = storageNode.IsInputMode
                    ? port.IsInput  // IsInputMode = true: chỉ hiện port IN
                    : !port.IsInput; // IsInputMode = false: chỉ hiện port OUT
                port.IsVisible = shouldShowPort;
            }
    }

    private static void RestoreDataFetcherNodeProperties(DataFetcherNode fetcherNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("SourceNodeId", out var snObj))
                fetcherNode.SourceNodeId = snObj?.ToString();
            if (properties.TryGetValue("SourceOutputKey", out var sokObj))
                fetcherNode.SourceOutputKey = sokObj?.ToString();
            if (properties.TryGetValue("WaitForWebNodeLoad", out var wfwnlObj) && wfwnlObj != null &&
                bool.TryParse(wfwnlObj.ToString(), out var wfwnl))
                fetcherNode.WaitForWebNodeLoad = wfwnl;
            if (properties.TryGetValue("EnableTimer", out var etObj) && etObj != null &&
                bool.TryParse(etObj.ToString(), out var et))
                fetcherNode.EnableTimer = et;
            if (properties.TryGetValue("TimerIntervalValue", out var tivObj) && tivObj != null &&
                int.TryParse(tivObj.ToString(), out var tiv) && tiv > 0)
                fetcherNode.TimerIntervalValue = tiv;
            if (properties.TryGetValue("TimerUnit", out var tuObj) && tuObj != null)
                fetcherNode.TimerUnit = tuObj.ToString() ?? "s";
            if (properties.TryGetValue("EnableRealtime", out var erObj) && erObj != null &&
                bool.TryParse(erObj.ToString(), out var er))
                fetcherNode.EnableRealtime = er;
            if (properties.TryGetValue("EnableDataReadyScan", out var edrsObj) && edrsObj != null &&
                bool.TryParse(edrsObj.ToString(), out var edrs))
                fetcherNode.EnableDataReadyScan = edrs;
            if (properties.TryGetValue("DataReadyScanIntervalValue", out var drsivObj) && drsivObj != null &&
                int.TryParse(drsivObj.ToString(), out var drsiv) && drsiv > 0)
                fetcherNode.DataReadyScanIntervalValue = drsiv;
            if (properties.TryGetValue("DataReadyScanUnit", out var drsuObj) && drsuObj != null)
                fetcherNode.DataReadyScanUnit = drsuObj.ToString() ?? "s";
            if (properties.TryGetValue("DataReadyScanKeys", out var drskObj) && drskObj != null)
            {
                try
                {
                    if (drskObj is string jsonKeys && !string.IsNullOrWhiteSpace(jsonKeys))
                    {
                        var keys = JsonSerializer.Deserialize<List<string>>(jsonKeys);
                        if (keys != null)
                            fetcherNode.DataReadyScanKeys = keys;
                    }
                    else if (drskObj is JsonElement drskEl && drskEl.ValueKind == JsonValueKind.Array)
                    {
                        var keys = JsonSerializer.Deserialize<List<string>>(drskEl.GetRawText());
                        if (keys != null)
                            fetcherNode.DataReadyScanKeys = keys;
                    }
                    else if (drskObj is JsonElement drskElStr && drskElStr.ValueKind == JsonValueKind.String)
                    {
                        var jsonKeys2 = drskElStr.GetString();
                        if (!string.IsNullOrWhiteSpace(jsonKeys2))
                        {
                            var keys = JsonSerializer.Deserialize<List<string>>(jsonKeys2);
                            if (keys != null)
                                fetcherNode.DataReadyScanKeys = keys;
                        }
                    }
                }
                catch { }
            }

    }

    private static void RestoreFlowOverwriteNodeProperties(FlowOverwriteNode flowOverwriteNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("OutputKey", out var outputKeyObj))
                flowOverwriteNode.OutputKey = outputKeyObj?.ToString() ?? "outputKey";
            if (properties.TryGetValue("AppendMode", out var appendObj) &&
                appendObj != null &&
                bool.TryParse(appendObj.ToString(), out var appendMode))
            {
                flowOverwriteNode.AppendMode = appendMode;
            }
            if (properties.TryGetValue("IncludeIndirectSources", out var includeIndirectObj) &&
                includeIndirectObj != null &&
                bool.TryParse(includeIndirectObj.ToString(), out var includeIndirect))
            {
                flowOverwriteNode.IncludeIndirectSources = includeIndirect;
            }
            if (properties.TryGetValue("Mappings", out var mappingsObj))
            {
                List<FlowOverwriteMapping>? parsedMappings = null;
                try
                {
                    if (mappingsObj is string mappingsJson && !string.IsNullOrWhiteSpace(mappingsJson))
                        parsedMappings = JsonSerializer.Deserialize<List<FlowOverwriteMapping>>(mappingsJson);
                    else if (mappingsObj is JsonElement mappingsElement)
                    {
                        if (mappingsElement.ValueKind == JsonValueKind.String)
                        {
                            var json = mappingsElement.GetString();
                            if (!string.IsNullOrWhiteSpace(json))
                                parsedMappings = JsonSerializer.Deserialize<List<FlowOverwriteMapping>>(json);
                        }
                        else if (mappingsElement.ValueKind == JsonValueKind.Array)
                        {
                            parsedMappings = JsonSerializer.Deserialize<List<FlowOverwriteMapping>>(mappingsElement.GetRawText());
                        }
                    }
                }
                catch { }

                if (parsedMappings != null)
                {
                    flowOverwriteNode.Mappings = parsedMappings
                        .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                        .Select(x => new FlowOverwriteMapping
                        {
                            SourceNodeId = x.SourceNodeId.Trim(),
                            SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                        })
                        .ToList();
                }
            }

            flowOverwriteNode.RebuildDynamicOutputs();
    }

    // -- GET (Serialize) --

    private static void GetInputNodeProperties(InputNode inputNode, Dictionary<string, object> dict)
    {
            if (!string.IsNullOrWhiteSpace(inputNode.Key))
                dict["InputKey"] = inputNode.Key;
            if (!string.IsNullOrWhiteSpace(inputNode.Value))
                dict["InputValue"] = inputNode.Value;
            dict["InputDataType"] = inputNode.DataType.ToString();

            if (inputNode.IsArrayType && inputNode.ArrayValues != null)
            {
                dict["InputArrayValues"] = JsonSerializer.Serialize(inputNode.ArrayValues);
            }

    }

    private static void GetDelayNodeProperties(DelayNode delayNode, Dictionary<string, object> dict)
    {
            dict["DelayMilliseconds"] = delayNode.DelayMilliseconds;
            dict["DelayValue"] = delayNode.DelayValue;
            dict["DelayUnit"] = delayNode.DelayUnit.ToString();

            dict["TimingMode"] = delayNode.TimingMode.ToString();
            dict["RandomMinValue"] = delayNode.RandomMinValue;
            dict["RandomMaxValue"] = delayNode.RandomMaxValue;
            if (!string.IsNullOrWhiteSpace(delayNode.DelaySourceNodeId))
                dict["DelaySourceNodeId"] = delayNode.DelaySourceNodeId;
            if (!string.IsNullOrWhiteSpace(delayNode.DelaySourceOutputKey))
                dict["DelaySourceOutputKey"] = delayNode.DelaySourceOutputKey;
    }

    private static void GetCallbackNodeProperties(CallbackNode callbackNode, Dictionary<string, object> dict)
    {
            if (!string.IsNullOrWhiteSpace(callbackNode.TargetNodeId))
                dict["TargetNodeId"] = callbackNode.TargetNodeId;
            dict["MaxCallbackCount"] = callbackNode.MaxCallbackCount;
            dict["FlowBehavior"] = callbackNode.FlowBehavior.ToString();

    }

    private static void GetListOutNodeProperties(ListOutNode listOutNode, Dictionary<string, object> dict)
    {
            // Serialize OutputMappings
            if (listOutNode.OutputMappings != null && listOutNode.OutputMappings.Count > 0)
            {
                var mappingsJson = JsonSerializer.Serialize(listOutNode.OutputMappings.Select(m => new
                {
                    NewKey = m.NewKey,
                    SourceNodeId = m.SourceNodeId,
                    SourceOutputKey = m.SourceOutputKey
                }).ToList());
                dict["OutputMappings"] = mappingsJson;
            }


    }

    private static void GetAssignDataNodeProperties(AssignDataNode assignDataNode, Dictionary<string, object> dict)
    {
            if (assignDataNode.Assignments.Count > 0)
                dict["Assignments"] = JsonSerializer.Serialize(assignDataNode.Assignments);

    }

    private static void GetStorageNodeProperties(StorageNode storageNode, Dictionary<string, object> dict)
    {

            if (!string.IsNullOrWhiteSpace(storageNode.SourceNodeId))
                dict["SourceNodeId"] = storageNode.SourceNodeId!;
            if (!string.IsNullOrWhiteSpace(storageNode.SourceOutputKey))
                dict["SourceOutputKey"] = storageNode.SourceOutputKey!;

            // IsInputMode
            dict["IsInputMode"] = storageNode.IsInputMode;

            // Lưu StoredOutputs – các giá trị đã được gán vào node
            if (storageNode.StoredOutputs.Count > 0)
            {
                dict["StoredOutputs"] = storageNode.StoredOutputs.ToDictionary(
                    kv => kv.Key,
                    kv => (object?)kv.Value ?? string.Empty);
            }

            // Lưu danh sách OutputKeys hiện tại để khôi phục cấu trúc outputs
            if (storageNode.DynamicOutputs != null && storageNode.DynamicOutputs.Count > 0)
            {
                var keys = storageNode.DynamicOutputs
                    .Where(o => !string.IsNullOrWhiteSpace(o.Key))
                    .Select(o => o.Key!)
                    .ToList();
                dict["OutputKeys"] = keys;
            }
    }

    private static void GetDataFetcherNodeProperties(DataFetcherNode fetcherNode, Dictionary<string, object> dict)
    {
            if (!string.IsNullOrWhiteSpace(fetcherNode.SourceNodeId))
                dict["SourceNodeId"] = fetcherNode.SourceNodeId;
            if (!string.IsNullOrWhiteSpace(fetcherNode.SourceOutputKey))
                dict["SourceOutputKey"] = fetcherNode.SourceOutputKey;
            dict["WaitForWebNodeLoad"] = fetcherNode.WaitForWebNodeLoad;
            dict["EnableTimer"] = fetcherNode.EnableTimer;
            dict["TimerIntervalValue"] = fetcherNode.TimerIntervalValue;
            dict["TimerUnit"] = fetcherNode.TimerUnit;
            dict["EnableRealtime"] = fetcherNode.EnableRealtime;
            dict["EnableDataReadyScan"] = fetcherNode.EnableDataReadyScan;
            dict["DataReadyScanIntervalValue"] = fetcherNode.DataReadyScanIntervalValue;
            dict["DataReadyScanUnit"] = fetcherNode.DataReadyScanUnit;
            dict["DataReadyScanKeys"] = JsonSerializer.Serialize(
                (fetcherNode.DataReadyScanKeys ?? new List<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
            dict["RunSourceNodeFirst"] = fetcherNode.RunSourceNodeFirst;

    }

    private static void GetFlowOverwriteNodeProperties(FlowOverwriteNode flowOverwriteNode, Dictionary<string, object> dict)
    {
            dict["OutputKey"] = string.IsNullOrWhiteSpace(flowOverwriteNode.OutputKey) ? "outputKey" : flowOverwriteNode.OutputKey.Trim();
            dict["AppendMode"] = flowOverwriteNode.AppendMode;
            dict["IncludeIndirectSources"] = flowOverwriteNode.IncludeIndirectSources;

            if (flowOverwriteNode.Mappings != null && flowOverwriteNode.Mappings.Count > 0)
            {
                dict["Mappings"] = JsonSerializer.Serialize(flowOverwriteNode.Mappings
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                    .Select(x => new
                    {
                        SourceNodeId = x.SourceNodeId.Trim(),
                        SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                    })
                    .ToList());
            }
    }

}
