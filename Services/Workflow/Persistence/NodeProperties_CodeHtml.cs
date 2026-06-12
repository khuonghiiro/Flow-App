using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreCodeNodeProperties(CodeNode codeNode, Dictionary<string, object> properties)
    {

            var loadedMappings = false;
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
                                else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                            }
                            list.Add(m);
                        }
                    }
                    else if (imJe.ValueKind == JsonValueKind.String)
                    {
                        var str = imJe.GetString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            try
                            {
                                var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(str);
                                if (parsed != null)
                                    foreach (var d in parsed)
                                    {
                                        var m = new CodeInputMapping();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                        if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                        if (d.TryGetValue("ShouldReExecute", out var sre))
                                        {
                                            if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                            else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                            else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                                        }
                                        list.Add(m);
                                    }
                            }
                            catch { }
                        }
                    }
                }
                else if (imObj is string imStr && !string.IsNullOrEmpty(imStr))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(imStr);
                        if (parsed != null)
                            foreach (var d in parsed)
                            {
                                var m = new CodeInputMapping();
                                if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                if (d.TryGetValue("ShouldReExecute", out var sre))
                                {
                                    if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                    else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                    else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                                }
                                list.Add(m);
                            }
                    }
                    catch { }
                }
                if (list.Count > 0) { codeNode.InputMappings = list; loadedMappings = true; }
            }
            if (!loadedMappings)
            {
                var first = codeNode.InputMappings.Count > 0 ? codeNode.InputMappings[0] : null;
                if (first == null) { first = new CodeInputMapping(); codeNode.InputMappings.Add(first); }
                if (properties.TryGetValue("SourceNodeId", out var snidObj))
                    first.SourceNodeId = GetStringFromJsonValue(snidObj);
                if (properties.TryGetValue("SourceOutputKey", out var sokObj))
                    first.SourceOutputKey = GetStringFromJsonValue(sokObj);
                if (properties.TryGetValue("InputKeyOverride", out var ikoObj))
                    first.InputKeyOverride = GetStringFromJsonValue(ikoObj);
            }
            if (properties.TryGetValue("ScriptCode", out var scObj))
                codeNode.ScriptCode = scObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("OutputKeys", out var okObj))
            {
                List<string>? keys = null;
                if (okObj is string jsonKeys)
                {
                    try { keys = JsonSerializer.Deserialize<List<string>>(jsonKeys); } catch { }
                }
                else if (okObj is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Array)
                    {
                        try { keys = JsonSerializer.Deserialize<List<string>>(je.GetRawText()); } catch { }
                    }
                    else if (je.ValueKind == JsonValueKind.String)
                    {
                        try { keys = JsonSerializer.Deserialize<List<string>>(je.GetString() ?? "[]"); } catch { }
                    }
                }
                if (keys != null)
                    codeNode.OutputKeys = keys;
                codeNode.RebuildDynamicOutputs();
            }
    }

    private static void RestoreHtmlUiNodeProperties(HtmlUiNode htmlUiNode, Dictionary<string, object> properties)
    {

            if (properties.TryGetValue("EnableSleepMode", out var hsmObj) && hsmObj != null &&
                bool.TryParse(hsmObj.ToString(), out var hsm))
                htmlUiNode.EnableSleepMode = hsm;
            if (properties.TryGetValue("SleepIdleTimeoutValue", out var hsitvObj) && hsitvObj != null &&
                int.TryParse(hsitvObj.ToString(), out var hsitv))
                htmlUiNode.SleepIdleTimeoutValue = hsitv;
            if (properties.TryGetValue("SleepIdleTimeoutUnit", out var hsituObj) && hsituObj != null)
                htmlUiNode.SleepIdleTimeoutUnit = hsituObj.ToString() ?? htmlUiNode.SleepIdleTimeoutUnit;
            // Restore HtmlUi specific zoom if present
            if (properties.TryGetValue("HtmlUi_CorrectedZoom", out var zoomObj) || properties.TryGetValue("HtmlUi_CssZoom", out zoomObj))
            {
                if (zoomObj != null)
                {
                    if (zoomObj is JsonElement je)
                    {
                        try { htmlUiNode.CssZoom = je.GetDouble(); } catch { }
                    }
                    else
                    {
                        if (double.TryParse(zoomObj.ToString(), out var z))
                            htmlUiNode.CssZoom = z;
                    }
                }
            }

            var loadedMappings = false;
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
                                else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                            }
                            if (e.TryGetProperty("AutoRefreshEnabled", out var are))
                            {
                                if (are.ValueKind == JsonValueKind.True) m.AutoRefreshEnabled = true;
                                else if (are.ValueKind == JsonValueKind.False) m.AutoRefreshEnabled = false;
                                else if (bool.TryParse(are.ToString(), out var b)) m.AutoRefreshEnabled = b;
                            }
                            if (e.TryGetProperty("AutoRefreshInterval", out var ari))
                            {
                                if (ari.ValueKind == JsonValueKind.Number && ari.TryGetInt32(out var iv)) m.AutoRefreshInterval = iv;
                                else if (int.TryParse(ari.ToString(), out var iv2)) m.AutoRefreshInterval = iv2;
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
                        var str = imJe.GetString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            try
                            {
                                var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(str);
                                if (parsed != null)
                                    foreach (var d in parsed)
                                    {
                                        var m = new CodeInputMapping();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                        if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                        if (d.TryGetValue("ShouldReExecute", out var sre))
                                        {
                                            if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                            else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                            else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                                        }
                                        if (d.TryGetValue("AutoRefreshEnabled", out var are))
                                        {
                                            if (are.ValueKind == JsonValueKind.True) m.AutoRefreshEnabled = true;
                                            else if (are.ValueKind == JsonValueKind.False) m.AutoRefreshEnabled = false;
                                            else if (bool.TryParse(are.ToString(), out var b)) m.AutoRefreshEnabled = b;
                                        }
                                        if (d.TryGetValue("AutoRefreshInterval", out var ari) && ari.ValueKind == JsonValueKind.Number && ari.TryGetInt32(out var ariv)) m.AutoRefreshInterval = ariv;
                                        if (d.TryGetValue("AutoRefreshUnit", out var aru)) { var u = GetStringFromJsonValue(aru); if (!string.IsNullOrWhiteSpace(u)) m.AutoRefreshUnit = u!; }
                                        list.Add(m);
                                    }
                            }
                            catch { }
                        }
                    }
                }
                else if (imObj is string imStr && !string.IsNullOrEmpty(imStr))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(imStr);
                        if (parsed != null)
                            foreach (var d in parsed)
                            {
                                var m = new CodeInputMapping();
                                if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                if (d.TryGetValue("ShouldReExecute", out var sre))
                                {
                                    if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                    else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                    else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                                }
                                if (d.TryGetValue("AutoRefreshEnabled", out var are))
                                {
                                    if (are.ValueKind == JsonValueKind.True) m.AutoRefreshEnabled = true;
                                    else if (are.ValueKind == JsonValueKind.False) m.AutoRefreshEnabled = false;
                                    else if (bool.TryParse(are.ToString(), out var b)) m.AutoRefreshEnabled = b;
                                }
                                if (d.TryGetValue("AutoRefreshInterval", out var ari) && ari.ValueKind == JsonValueKind.Number && ari.TryGetInt32(out var ariv)) m.AutoRefreshInterval = ariv;
                                if (d.TryGetValue("AutoRefreshUnit", out var aru)) { var u = GetStringFromJsonValue(aru); if (!string.IsNullOrWhiteSpace(u)) m.AutoRefreshUnit = u!; }
                                list.Add(m);
                            }
                    }
                    catch { }
                }
                if (list.Count > 0) { htmlUiNode.InputMappings = list; loadedMappings = true; }
            }
            if (!loadedMappings)
            {
                var first = htmlUiNode.InputMappings.Count > 0 ? htmlUiNode.InputMappings[0] : null;
                if (first == null) { first = new CodeInputMapping(); htmlUiNode.InputMappings.Add(first); }
                if (properties.TryGetValue("SourceNodeId", out var snidObj))
                    first.SourceNodeId = GetStringFromJsonValue(snidObj);
                if (properties.TryGetValue("SourceOutputKey", out var sokObj))
                    first.SourceOutputKey = GetStringFromJsonValue(sokObj);
                if (properties.TryGetValue("InputKeyOverride", out var ikoObj))
                    first.InputKeyOverride = GetStringFromJsonValue(ikoObj);
            }
            if (properties.TryGetValue("HtmlCode", out var htmlObj))
                htmlUiNode.HtmlCode = htmlObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("JsCode", out var jsObj))
                htmlUiNode.JsCode = jsObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CssCode", out var cssObj))
                htmlUiNode.CssCode = cssObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ParamsCode", out var paramsObj))
                htmlUiNode.ParamsCode = paramsObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("OutputKeys", out var okObj))
            {
                List<string>? keys = null;
                if (okObj is string jsonKeys)
                {
                    try { keys = JsonSerializer.Deserialize<List<string>>(jsonKeys); } catch { }
                }
                else if (okObj is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Array)
                    {
                        try { keys = JsonSerializer.Deserialize<List<string>>(je.GetRawText()); } catch { }
                    }
                    else if (je.ValueKind == JsonValueKind.String)
                    {
                        try { keys = JsonSerializer.Deserialize<List<string>>(je.GetString() ?? "[]"); } catch { }
                    }
                }
                if (keys != null)
                    htmlUiNode.OutputKeys = keys;
                htmlUiNode.RebuildDynamicOutputs();
            }
            if (properties.TryGetValue("Width", out var widthObj))
            {
                if (double.TryParse(widthObj?.ToString(), out var width))
                    htmlUiNode.Width = Math.Max(280, width);
            }
            if (properties.TryGetValue("Height", out var heightObj))
            {
                if (double.TryParse(heightObj?.ToString(), out var height))
                    htmlUiNode.Height = Math.Max(200, height);
            }
            // ── WebTab properties ──
            if (properties.TryGetValue("UseWebTab", out var uwtObj) && uwtObj != null && bool.TryParse(uwtObj.ToString(), out var uwt))
                htmlUiNode.UseWebTab = uwt;
            if (properties.TryGetValue("WebTabUrl", out var wtuObj))
                htmlUiNode.WebTabUrl = wtuObj?.ToString();
            if (properties.TryGetValue("WebTabCookieSourceNodeId", out var wtcsnObj))
                htmlUiNode.WebTabCookieSourceNodeId = wtcsnObj?.ToString();
            if (properties.TryGetValue("WebTabCookieSourceOutputKey", out var wtcsokObj))
                htmlUiNode.WebTabCookieSourceOutputKey = wtcsokObj?.ToString();
            if (properties.TryGetValue("WebTabAutoRefreshEnabled", out var wtareObj) && wtareObj != null && bool.TryParse(wtareObj.ToString(), out var wtare))
                htmlUiNode.WebTabAutoRefreshEnabled = wtare;
            if (properties.TryGetValue("WebTabAutoRefreshInterval", out var wtariObj) && wtariObj != null && int.TryParse(wtariObj.ToString(), out var wtari))
                htmlUiNode.WebTabAutoRefreshInterval = wtari;
            if (properties.TryGetValue("WebTabAutoRefreshUnit", out var wtaruObj) && wtaruObj != null)
                htmlUiNode.WebTabAutoRefreshUnit = wtaruObj.ToString() ?? htmlUiNode.WebTabAutoRefreshUnit;
            // ── Offline Assets (JS/CSS libraries) ──
            if (properties.TryGetValue("OfflineAssets", out var oaObj) && oaObj != null)
            {
                try
                {
                    var rawJson = oaObj is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : oaObj.ToString();

                    if (!string.IsNullOrWhiteSpace(rawJson))
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(rawJson);
                        if (parsed != null)
                        {
                            var list = new List<FlowMy.Models.HtmlOfflineAsset>();
                            foreach (var d in parsed)
                            {
                                var asset = new FlowMy.Models.HtmlOfflineAsset();
                                if (d.TryGetValue("Id", out var idEl)) asset.Id = GetStringFromJsonValue(idEl) ?? asset.Id;
                                if (d.TryGetValue("Title", out var titleEl)) asset.Title = GetStringFromJsonValue(titleEl) ?? string.Empty;
                                if (d.TryGetValue("Description", out var descEl)) asset.Description = GetStringFromJsonValue(descEl) ?? string.Empty;
                                if (d.TryGetValue("SourceUrl", out var urlEl)) asset.SourceUrl = GetStringFromJsonValue(urlEl) ?? string.Empty;
                                if (d.TryGetValue("LocalFileName", out var fnEl)) asset.LocalFileName = GetStringFromJsonValue(fnEl) ?? string.Empty;
                                if (d.TryGetValue("AssetType", out var typeEl)) asset.AssetType = GetStringFromJsonValue(typeEl) ?? "js";
                                if (d.TryGetValue("IsEnabled", out var enabledEl))
                                {
                                    if (enabledEl.ValueKind == JsonValueKind.True) asset.IsEnabled = true;
                                    else if (enabledEl.ValueKind == JsonValueKind.False) asset.IsEnabled = false;
                                    else if (bool.TryParse(enabledEl.ToString(), out var b)) asset.IsEnabled = b;
                                }
                                list.Add(asset);
                            }
                            htmlUiNode.OfflineAssets = list;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deserializing OfflineAssets: {ex.Message}");
                }
            }
            // ── AsyncDataSources (Async Data Receiver) ──
            if (properties.TryGetValue("AsyncDataSources", out var adsObj) && adsObj != null)
            {
                try
                {
                    var rawJson = adsObj is JsonElement adsJe
                        ? (adsJe.ValueKind == JsonValueKind.String ? adsJe.GetString() : adsJe.GetRawText())
                        : adsObj.ToString();

                    if (!string.IsNullOrWhiteSpace(rawJson))
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(rawJson);
                        if (parsed != null)
                        {
                            var list = new List<AsyncDataSource>();
                            foreach (var d in parsed)
                            {
                                var ads = new AsyncDataSource();
                                if (d.TryGetValue("SourceNodeId", out var sni)) ads.SourceNodeId = GetStringFromJsonValue(sni) ?? string.Empty;
                                if (d.TryGetValue("SourceOutputKey", out var sok)) ads.SourceOutputKey = GetStringFromJsonValue(sok) ?? string.Empty;
                                if (d.TryGetValue("ReceiverKey", out var rk)) ads.ReceiverKey = GetStringFromJsonValue(rk) ?? string.Empty;
                                list.Add(ads);
                            }
                            htmlUiNode.AsyncDataSources = list;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deserializing AsyncDataSources: {ex.Message}");
                }
            }
    }

    // -- GET (Serialize) --

    private static void GetCodeNodeProperties(CodeNode codeNode, Dictionary<string, object> dict)
    {

            if (codeNode.InputMappings != null && codeNode.InputMappings.Count > 0)
            {
                var arr = codeNode.InputMappings.Select(m => new Dictionary<string, object?>
                {
                    ["SourceNodeId"] = m.SourceNodeId,
                    ["SourceOutputKey"] = m.SourceOutputKey,
                    ["InputKeyOverride"] = m.InputKeyOverride,
                    ["ShouldReExecute"] = m.ShouldReExecute
                }).ToList();
                dict["InputMappings"] = JsonSerializer.Serialize(arr);
            }
            if (!string.IsNullOrEmpty(codeNode.ScriptCode))
                dict["ScriptCode"] = codeNode.ScriptCode;
            if (codeNode.OutputKeys != null)
                dict["OutputKeys"] = JsonSerializer.Serialize(codeNode.OutputKeys);
    }

    private static void GetHtmlUiNodeProperties(HtmlUiNode htmlUiNode, Dictionary<string, object> dict)
    {

            if (htmlUiNode.InputMappings != null && htmlUiNode.InputMappings.Count > 0)
            {
                var arr = htmlUiNode.InputMappings.Select(m => new Dictionary<string, object?>
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
            if (!string.IsNullOrEmpty(htmlUiNode.HtmlCode))
                dict["HtmlCode"] = htmlUiNode.HtmlCode;
            if (!string.IsNullOrEmpty(htmlUiNode.JsCode))
                dict["JsCode"] = htmlUiNode.JsCode;
            if (!string.IsNullOrEmpty(htmlUiNode.CssCode))
                dict["CssCode"] = htmlUiNode.CssCode;
            if (!string.IsNullOrEmpty(htmlUiNode.ParamsCode))
                dict["ParamsCode"] = htmlUiNode.ParamsCode;
            if (htmlUiNode.OutputKeys != null && htmlUiNode.OutputKeys.Count > 0)
                dict["OutputKeys"] = JsonSerializer.Serialize(htmlUiNode.OutputKeys);
            dict["Width"] = htmlUiNode.Width;
            dict["Height"] = htmlUiNode.Height;
            if (htmlUiNode.CssZoom > 0)
                dict["HtmlUi_CssZoom"] = htmlUiNode.CssZoom;
            dict["EnableSleepMode"] = htmlUiNode.EnableSleepMode;
            dict["SleepIdleTimeoutValue"] = htmlUiNode.SleepIdleTimeoutValue;
            dict["SleepIdleTimeoutUnit"] = htmlUiNode.SleepIdleTimeoutUnit;
            // ── WebTab properties ──
            dict["UseWebTab"] = htmlUiNode.UseWebTab;
            if (!string.IsNullOrEmpty(htmlUiNode.WebTabUrl))
                dict["WebTabUrl"] = htmlUiNode.WebTabUrl;
            if (!string.IsNullOrEmpty(htmlUiNode.WebTabCookieSourceNodeId))
                dict["WebTabCookieSourceNodeId"] = htmlUiNode.WebTabCookieSourceNodeId;
            if (!string.IsNullOrEmpty(htmlUiNode.WebTabCookieSourceOutputKey))
                dict["WebTabCookieSourceOutputKey"] = htmlUiNode.WebTabCookieSourceOutputKey;
            dict["WebTabAutoRefreshEnabled"] = htmlUiNode.WebTabAutoRefreshEnabled;
            dict["WebTabAutoRefreshInterval"] = htmlUiNode.WebTabAutoRefreshInterval;
            dict["WebTabAutoRefreshUnit"] = htmlUiNode.WebTabAutoRefreshUnit;
            // ── Offline Assets (JS/CSS libraries) ──
            if (htmlUiNode.OfflineAssets != null && htmlUiNode.OfflineAssets.Count > 0)
            {
                var assetsJson = JsonSerializer.Serialize(htmlUiNode.OfflineAssets.Select(a => new
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    SourceUrl = a.SourceUrl,
                    LocalFileName = a.LocalFileName,
                    AssetType = a.AssetType,
                    IsEnabled = a.IsEnabled
                }).ToList());
                dict["OfflineAssets"] = assetsJson;
            }
            // ── AsyncDataSources (Async Data Receiver) ──
            if (htmlUiNode.AsyncDataSources != null && htmlUiNode.AsyncDataSources.Count > 0)
            {
                var adsJson = JsonSerializer.Serialize(htmlUiNode.AsyncDataSources.Select(a => new
                {
                    SourceNodeId = a.SourceNodeId,
                    SourceOutputKey = a.SourceOutputKey,
                    ReceiverKey = a.ReceiverKey
                }).ToList());
                dict["AsyncDataSources"] = adsJson;
            }
    }

}
