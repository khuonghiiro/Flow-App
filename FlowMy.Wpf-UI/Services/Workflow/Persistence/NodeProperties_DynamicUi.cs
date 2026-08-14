// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using FlowMy.Models;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Workflow
{
    public sealed partial class FileWorkflowPersistenceService
    {
        private static void RestoreDynamicUiNodeProperties(DynamicUiNode node, Dictionary<string, object> properties)
        {
            if (properties.TryGetValue("HtmlCode", out var htmlObj) && htmlObj != null)
                node.HtmlCode = htmlObj.ToString() ?? "";
            if (properties.TryGetValue("CssCode", out var cssObj) && cssObj != null)
                node.CssCode = cssObj.ToString() ?? "";
            if (properties.TryGetValue("JsCode", out var jsObj) && jsObj != null)
                node.JsCode = jsObj.ToString() ?? "";

            if (properties.TryGetValue("WindowWidth", out var wwObj) && wwObj != null && double.TryParse(wwObj.ToString(), out var ww))
                node.WindowWidth = ww;
            if (properties.TryGetValue("WindowHeight", out var whObj) && whObj != null && double.TryParse(whObj.ToString(), out var wh))
                node.WindowHeight = wh;

            if (properties.TryGetValue("Width", out var wObj) && wObj != null && double.TryParse(wObj.ToString(), out var w))
                node.Width = w;
            if (properties.TryGetValue("Height", out var hObj) && hObj != null && double.TryParse(hObj.ToString(), out var h))
                node.Height = h;

            bool loadedMappings = false;
            if (properties.TryGetValue("InputMappings", out var imObj) && imObj != null)
            {
                var list = new List<CodeInputMapping>();
                if (imObj is JsonElement imJe)
                {
                    if (imJe.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in imJe.EnumerateArray())
                        {
                            var m = new CodeInputMapping();
                            if (e.TryGetProperty("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                            if (e.TryGetProperty("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                            if (e.TryGetProperty("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                            if (e.TryGetProperty("ShouldReExecute", out var sre))
                            {
                                if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                else if (bool.TryParse(sre.ToString(), out var bVal1)) m.ShouldReExecute = bVal1;
                            }
                            if (e.TryGetProperty("AutoRefreshEnabled", out var are))
                            {
                                if (are.ValueKind == JsonValueKind.True) m.AutoRefreshEnabled = true;
                                else if (are.ValueKind == JsonValueKind.False) m.AutoRefreshEnabled = false;
                                else if (bool.TryParse(are.ToString(), out var bVal2)) m.AutoRefreshEnabled = bVal2;
                            }
                            if (e.TryGetProperty("AutoRefreshInterval", out var ari))
                            {
                                if (ari.ValueKind == JsonValueKind.Number && ari.TryGetInt32(out var val)) m.AutoRefreshInterval = val;
                                else if (int.TryParse(ari.ToString(), out var val2)) m.AutoRefreshInterval = val2;
                            }
                            if (e.TryGetProperty("AutoRefreshUnit", out var aru))
                            {
                                var u = GetStringFromJsonValue(aru);
                                if (!string.IsNullOrWhiteSpace(u)) m.AutoRefreshUnit = u!;
                            }
                            list.Add(m);
                        }
                    }
                    else if (imJe.ValueKind == JsonValueKind.String)
                    {
                        try
                        {
                            var json = imJe.GetString();
                            if (!string.IsNullOrEmpty(json))
                            {
                                var rawList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
                                if (rawList != null)
                                {
                                    foreach (var d in rawList)
                                    {
                                        var m = new CodeInputMapping();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                        if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                        if (d.TryGetValue("ShouldReExecute", out var sre))
                                        {
                                            if (sre is JsonElement sreJe)
                                            {
                                                if (sreJe.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                                else if (sreJe.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                            }
                                            else if (sre is bool b) m.ShouldReExecute = b;
                                            else if (bool.TryParse(sre.ToString(), out var bVal3)) m.ShouldReExecute = bVal3;
                                        }
                                        if (d.TryGetValue("AutoRefreshEnabled", out var are))
                                        {
                                            if (are is JsonElement areJe)
                                            {
                                                if (areJe.ValueKind == JsonValueKind.True) m.AutoRefreshEnabled = true;
                                                else if (areJe.ValueKind == JsonValueKind.False) m.AutoRefreshEnabled = false;
                                            }
                                            else if (are is bool b) m.AutoRefreshEnabled = b;
                                            else if (bool.TryParse(are.ToString(), out var bVal4)) m.AutoRefreshEnabled = bVal4;
                                        }
                                        if (d.TryGetValue("AutoRefreshInterval", out var ari))
                                        {
                                            if (ari is JsonElement ariJe && ariJe.ValueKind == JsonValueKind.Number && ariJe.TryGetInt32(out var val)) m.AutoRefreshInterval = val;
                                            else if (int.TryParse(ari.ToString(), out var val2)) m.AutoRefreshInterval = val2;
                                        }
                                        if (d.TryGetValue("AutoRefreshUnit", out var aru)) { var u = GetStringFromJsonValue(aru); if (!string.IsNullOrWhiteSpace(u)) m.AutoRefreshUnit = u!; }
                                        list.Add(m);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                if (list.Count > 0) { node.InputMappings = list; loadedMappings = true; }
            }
            if (!loadedMappings)
            {
                var first = node.InputMappings.Count > 0 ? node.InputMappings[0] : null;
                if (first == null) { first = new CodeInputMapping(); node.InputMappings.Add(first); }
                if (properties.TryGetValue("SourceNodeId", out var snidObj))
                    first.SourceNodeId = GetStringFromJsonValue(snidObj);
                if (properties.TryGetValue("SourceOutputKey", out var sokObj))
                    first.SourceOutputKey = GetStringFromJsonValue(sokObj);
                if (properties.TryGetValue("InputKeyOverride", out var ikoObj))
                    first.InputKeyOverride = GetStringFromJsonValue(ikoObj);
            }

            if (properties.TryGetValue("Fields", out var fieldsObj) && fieldsObj != null)
            {
                try
                {
                    string? jsonStr = null;
                    if (fieldsObj is JsonElement je && je.ValueKind == JsonValueKind.String)
                    {
                        jsonStr = je.GetString();
                    }
                    else if (fieldsObj is string str)
                    {
                        jsonStr = str;
                    }
                    else if (fieldsObj is JsonElement jeArr && jeArr.ValueKind == JsonValueKind.Array)
                    {
                        jsonStr = jeArr.GetRawText();
                    }

                    if (!string.IsNullOrEmpty(jsonStr))
                    {
                        var fieldsList = JsonSerializer.Deserialize<List<DynamicUiFieldConfig>>(jsonStr);
                        if (fieldsList != null)
                        {
                            node.Fields.Clear();
                            foreach (var f in fieldsList)
                            {
                                node.Fields.Add(f);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DynamicUiPersistence] Error restoring fields: {ex.Message}");
                }
            }

            node.RebuildDynamicPorts();
        }

        private static void GetDynamicUiNodeProperties(DynamicUiNode node, Dictionary<string, object> dict)
        {
            dict["HtmlCode"] = node.HtmlCode;
            dict["CssCode"] = node.CssCode;
            dict["JsCode"] = node.JsCode;
            dict["WindowWidth"] = node.WindowWidth;
            dict["WindowHeight"] = node.WindowHeight;
            dict["Width"] = node.Width;
            dict["Height"] = node.Height;

            if (node.InputMappings != null && node.InputMappings.Count > 0)
            {
                var arr = node.InputMappings.Select(m => new Dictionary<string, object?>
                {
                    ["SourceNodeId"] = m.SourceNodeId,
                    ["SourceOutputKey"] = m.SourceOutputKey,
                    ["InputKeyOverride"] = m.InputKeyOverride,
                    ["ShouldReExecute"] = m.ShouldReExecute,
                    ["AutoRefreshEnabled"] = m.AutoRefreshEnabled,
                    ["AutoRefreshInterval"] = m.AutoRefreshInterval,
                    ["AutoRefreshUnit"] = m.AutoRefreshUnit
                }).ToList();
                dict["InputMappings"] = JsonSerializer.Serialize(arr);
            }

            try
            {
                dict["Fields"] = JsonSerializer.Serialize(node.Fields);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DynamicUiPersistence] Error saving fields: {ex.Message}");
            }
        }
    }
}
