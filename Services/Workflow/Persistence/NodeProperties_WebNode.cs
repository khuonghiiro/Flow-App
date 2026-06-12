using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreWebNodeProperties(WebNode webNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("Width", out var wObj) && wObj != null && double.TryParse(wObj.ToString(), out var w))
            {
                // Đảm bảo Width luôn >= 280 để tránh lỗi HwndHost khi chuyển workflow giữa các máy
                webNode.Width = Math.Max(280, w);
            }
            if (properties.TryGetValue("Height", out var hObj) && hObj != null && double.TryParse(hObj.ToString(), out var h))
            {
                // Đảm bảo Height luôn >= 200 để tránh lỗi HwndHost khi chuyển workflow giữa các máy
                webNode.Height = Math.Max(200, h);
            }
            if (properties.TryGetValue("ExtractUrl", out var euObj))
                webNode.ExtractUrl = euObj?.ToString() ?? "";
            if (properties.TryGetValue("ExtractRequestMethod", out var ermObj))
                webNode.ExtractRequestMethod = ermObj?.ToString() ?? "GET";
            if (properties.TryGetValue("ExtractStatusCode", out var escObj))
                webNode.ExtractStatusCode = escObj?.ToString() ?? "200";
            // Timeout chờ outputs từ WebView2 (ms)
            if (properties.TryGetValue("ResponseOutputsWaitTimeoutMs", out var rowtObj) && rowtObj != null &&
                int.TryParse(rowtObj.ToString(), out var rowt) && rowt >= 0)
                webNode.ResponseOutputsWaitTimeoutMs = rowt;
            // Wait mode (ALL / ANY)
            if (properties.TryGetValue("ResponseOutputsWaitMode", out var rowmObj) && rowmObj != null &&
                Enum.TryParse<FlowMy.Models.Nodes.WebOutputsWaitMode>(rowmObj.ToString(), out var rowm))
                webNode.ResponseOutputsWaitMode = rowm;
            if (properties.TryGetValue("BlockingRules", out var brObj) && brObj != null)
            {
                try
                {
                    webNode.BlockingRules.Clear();
                    JsonElement? brJe = null;
                    if (brObj is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.Array) brJe = je;
                        else if (je.ValueKind == JsonValueKind.String)
                        {
                            var s = je.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) brJe = JsonSerializer.Deserialize<JsonElement>(s);
                        }
                    }
                    else if (brObj is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        brJe = JsonSerializer.Deserialize<JsonElement>(s);
                    }

                    if (brJe.HasValue && brJe.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in brJe.Value.EnumerateArray())
                        {
                            var r = new WebBlockingRule();
                            if (e.TryGetProperty("UrlPattern", out var up)) r.UrlPattern = GetStringFromJsonValue(up);

                            // Method (optional in older workflows)
                            if (e.TryGetProperty("Method", out var m))
                                r.Method = GetStringFromJsonValue(m) ?? "All";
                            else
                                r.Method = "All";

                            // Child rules (new format)
                            if (e.TryGetProperty("ChildRules", out var crJe) && crJe.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var childEl in crJe.EnumerateArray())
                                {
                                    var child = new WebBlockingChildRule();
                                    if (childEl.TryGetProperty("UrlPattern", out var cup))
                                        child.UrlPattern = GetStringFromJsonValue(cup) ?? string.Empty;
                                    if (childEl.TryGetProperty("Method", out var cm))
                                        child.Method = GetStringFromJsonValue(cm) ?? "All";
                                    else
                                        child.Method = "All";

                                    if (!string.IsNullOrWhiteSpace(child.UrlPattern))
                                        r.ChildRules.Add(child);
                                }
                            }

                            webNode.BlockingRules.Add(r);
                        }
                    }
                }
                catch { }
            }

            if (properties.TryGetValue("EnableSleepMode", out var esmObj) && esmObj != null &&
                bool.TryParse(esmObj.ToString(), out var esm))
                webNode.EnableSleepMode = esm;
            if (properties.TryGetValue("SleepIdleTimeoutValue", out var sitvObj) && sitvObj != null &&
                int.TryParse(sitvObj.ToString(), out var sitv))
                webNode.SleepIdleTimeoutValue = sitv;
            if (properties.TryGetValue("SleepIdleTimeoutUnit", out var situObj) && situObj != null)
                webNode.SleepIdleTimeoutUnit = situObj.ToString() ?? webNode.SleepIdleTimeoutUnit;
            if (properties.TryGetValue("SyncLiveOutputsToResults", out var sloObj) && sloObj != null &&
                bool.TryParse(sloObj.ToString(), out var slo))
                webNode.SyncLiveOutputsToResults = slo;

            // Auto-reload timer
            if (properties.TryGetValue("AutoReloadEnabled", out var areObj) && areObj != null &&
                bool.TryParse(areObj.ToString(), out var areVal))
                webNode.AutoReloadEnabled = areVal;
            if (properties.TryGetValue("AutoReloadIntervalValue", out var arivObj) && arivObj != null)
            {
                if (arivObj is JsonElement arivJe)
                {
                    try { webNode.AutoReloadIntervalValue = arivJe.GetDouble(); } catch { }
                }
                else if (double.TryParse(arivObj.ToString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var arivD))
                    webNode.AutoReloadIntervalValue = arivD;
            }
            if (properties.TryGetValue("AutoReloadIntervalUnit", out var ariuObj) && ariuObj != null)
            {
                var u = ariuObj.ToString();
                if (!string.IsNullOrWhiteSpace(u)) webNode.AutoReloadIntervalUnit = u!;
            }

            // Block all requests after first match
            if (properties.TryGetValue("BlockAllRequestsAfterFirstMatch", out var baaObj) && baaObj != null &&
                bool.TryParse(baaObj.ToString(), out var baaVal))
                webNode.BlockAllRequestsAfterFirstMatch = baaVal;

            // Restore per-domain CSS zoom if available
            if (properties.TryGetValue("Web_LastHost", out var whObj))
                webNode.LastHost = GetStringFromJsonValue(whObj);
            if (properties.TryGetValue("Web_CssZoom", out var wzObj))
            {
                if (wzObj is JsonElement jeZoom)
                {
                    try { webNode.CssZoom = jeZoom.GetDouble(); } catch { }
                }
                else if (double.TryParse(wzObj?.ToString(), out var z) && z > 0)
                {
                    webNode.CssZoom = z;
                }
            }

            // JS injection (nhiều Node+Key -> WebView2) – migrate từ format cũ nếu có
            if (properties.TryGetValue("JsSources", out var jsArrObj) && jsArrObj != null)
            {
                try
                {
                    var list = new List<WebJsSourceMapping>();
                    if (jsArrObj is JsonElement jsJe)
                    {
                        if (jsJe.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var e in jsJe.EnumerateArray())
                            {
                                var m = new WebJsSourceMapping();
                                if (e.TryGetProperty("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                if (e.TryGetProperty("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                // AutoTimer fields (optional, backward-compatible)
                                if (e.TryGetProperty("AutoTimerEnabled", out var ate))
                                {
                                    if (ate.ValueKind == JsonValueKind.True) m.AutoTimerEnabled = true;
                                    else if (ate.ValueKind == JsonValueKind.False) m.AutoTimerEnabled = false;
                                    else if (ate.ValueKind == JsonValueKind.String && bool.TryParse(ate.GetString(), out var b)) m.AutoTimerEnabled = b;
                                }
                                if (e.TryGetProperty("AutoTimerIntervalValue", out var ativ))
                                {
                                    if (ativ.ValueKind == JsonValueKind.Number && ativ.TryGetDouble(out var dv)) m.AutoTimerIntervalValue = dv;
                                    else if (ativ.ValueKind == JsonValueKind.String && double.TryParse(ativ.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dsv)) m.AutoTimerIntervalValue = dsv;
                                }
                                if (e.TryGetProperty("AutoTimerIntervalUnit", out var atiu))
                                {
                                    var u = GetStringFromJsonValue(atiu);
                                    if (!string.IsNullOrWhiteSpace(u)) m.AutoTimerIntervalUnit = u!;
                                }
                                if (!string.IsNullOrWhiteSpace(m.SourceNodeId) && !string.IsNullOrWhiteSpace(m.SourceOutputKey))
                                    list.Add(m);
                            }
                        }
                        else if (jsJe.ValueKind == JsonValueKind.String)
                        {
                            var s = jsJe.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                var arr = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(s);
                                if (arr != null)
                                {
                                    foreach (var d in arr)
                                    {
                                        var m = new WebJsSourceMapping();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                        if (d.TryGetValue("AutoTimerEnabled", out var ate))
                                        {
                                            if (ate.ValueKind == JsonValueKind.True) m.AutoTimerEnabled = true;
                                            else if (ate.ValueKind == JsonValueKind.False) m.AutoTimerEnabled = false;
                                            else if (ate.ValueKind == JsonValueKind.String && bool.TryParse(ate.GetString(), out var b)) m.AutoTimerEnabled = b;
                                        }
                                        if (d.TryGetValue("AutoTimerIntervalValue", out var ativ))
                                        {
                                            if (ativ.ValueKind == JsonValueKind.Number && ativ.TryGetDouble(out var dv)) m.AutoTimerIntervalValue = dv;
                                            else if (ativ.ValueKind == JsonValueKind.String && double.TryParse(ativ.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dsv)) m.AutoTimerIntervalValue = dsv;
                                        }
                                        if (d.TryGetValue("AutoTimerIntervalUnit", out var atiu)) { var u = GetStringFromJsonValue(atiu); if (!string.IsNullOrWhiteSpace(u)) m.AutoTimerIntervalUnit = u!; }
                                        if (!string.IsNullOrWhiteSpace(m.SourceNodeId) && !string.IsNullOrWhiteSpace(m.SourceOutputKey))
                                            list.Add(m);
                                    }
                                }
                            }
                        }
                    }
                    if (list.Count > 0)
                        webNode.JsSources = list;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error deserializing WebNode.JsSources: {ex.Message}"); }
            }
            // Backward compat: migrate JsSourceNodeId + JsSourceOutputKey
            if ((webNode.JsSources == null || webNode.JsSources.Count == 0) &&
                properties.TryGetValue("JsSourceNodeId", out var jsnObj) && properties.TryGetValue("JsSourceOutputKey", out var jskObj))
            {
                var nodeId = jsnObj?.ToString();
                var key = jskObj?.ToString();
                if (!string.IsNullOrWhiteSpace(nodeId) && !string.IsNullOrWhiteSpace(key))
                {
                    webNode.JsSources = new List<WebJsSourceMapping>
                    {
                        new WebJsSourceMapping { SourceNodeId = nodeId, SourceOutputKey = key }
                    };
                }
            }

            // Deserialize InputMappings (giống CodeNode nhưng dùng WebInputMapping)
            if (properties.TryGetValue("InputMappings", out var imObj) && imObj != null)
            {
                try
                {
                    var list = new List<WebInputMapping>();
                    if (imObj is JsonElement imJe)
                    {
                        if (imJe.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var e in imJe.EnumerateArray())
                            {
                                var m = new WebInputMapping();
                                if (e.TryGetProperty("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                if (e.TryGetProperty("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                if (e.TryGetProperty("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                list.Add(m);
                            }
                        }
                        else if (imJe.ValueKind == JsonValueKind.String)
                        {
                            var s = imJe.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                var arr = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(s);
                                if (arr != null)
                                {
                                    foreach (var d in arr)
                                    {
                                        var m = new WebInputMapping();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = sni?.ToString();
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = sok?.ToString();
                                        if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = iko?.ToString();
                                        list.Add(m);
                                    }
                                }
                            }
                        }
                    }
                    else if (imObj is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        var arr = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(s);
                        if (arr != null)
                        {
                            foreach (var d in arr)
                            {
                                var m = new WebInputMapping();
                                if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = sni?.ToString();
                                if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = sok?.ToString();
                                if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = iko?.ToString();
                                list.Add(m);
                            }
                        }
                    }

                    if (list.Count > 0)
                        webNode.InputMappings = list;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deserializing WebNode.InputMappings: {ex.Message}\n{ex.StackTrace}");
                }
            }
            if (properties.TryGetValue("RequestInterceptRules", out var rirObj) && rirObj != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"=== Deserializing RequestInterceptRules for WebNode ===");
                    System.Diagnostics.Debug.WriteLine($"RequestInterceptRules value type: {rirObj.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"RequestInterceptRules value: {rirObj}");

                    webNode.RequestInterceptRules.Clear();
                    JsonElement? rirJe = null;
                    if (rirObj is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.Array)
                        {
                            rirJe = je;
                            System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is JsonElement array");
                        }
                        else if (je.ValueKind == JsonValueKind.String)
                        {
                            var rirStr = je.GetString();
                            if (!string.IsNullOrWhiteSpace(rirStr))
                            {
                                System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is JsonElement string: {rirStr.Substring(0, Math.Min(100, rirStr.Length))}...");
                                try
                                {
                                    var parsed = JsonSerializer.Deserialize<JsonElement>(rirStr);
                                    if (parsed.ValueKind == JsonValueKind.Array)
                                    {
                                        rirJe = parsed;
                                        System.Diagnostics.Debug.WriteLine($"✓ Successfully parsed JSON string to JsonElement array");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"⚠ Parsed JSON is not an array (ValueKind: {parsed.ValueKind})");
                                    }
                                }
                                catch (System.Text.Json.JsonException ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize RequestInterceptRules JSON string: {ex.Message}\n{ex.StackTrace}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error parsing RequestInterceptRules JSON string: {ex.Message}\n{ex.StackTrace}");
                                }
                            }
                        }
                    }
                    else if (rirObj is string rirStr && !string.IsNullOrWhiteSpace(rirStr))
                    {
                        System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is JSON string: {rirStr.Substring(0, Math.Min(100, rirStr.Length))}...");
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<JsonElement>(rirStr);
                            if (parsed.ValueKind == JsonValueKind.Array)
                            {
                                rirJe = parsed;
                                System.Diagnostics.Debug.WriteLine($"✓ Successfully parsed JSON string to JsonElement array");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠ Parsed JSON is not an array (ValueKind: {parsed.ValueKind})");
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize RequestInterceptRules JSON string: {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Error parsing RequestInterceptRules JSON string: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ RequestInterceptRules is unsupported type/value (type: {rirObj.GetType().Name})");
                    }

                    if (rirJe.HasValue)
                    {
                        try
                        {
                            int count = 0;
                            foreach (var e in rirJe.Value.EnumerateArray())
                            {
                                try
                                {
                                    var r = new WebRequestInterceptRule();
                                    if (e.TryGetProperty("MatchUrlPattern", out var mup)) r.MatchUrlPattern = GetStringFromJsonValue(mup);
                                    if (e.TryGetProperty("ReplaceUrlValue", out var ruv)) r.ReplaceUrlValue = GetStringFromJsonValue(ruv);
                                    if (e.TryGetProperty("ReplaceUrlSourceNodeId", out var rusni)) r.ReplaceUrlSourceNodeId = GetStringFromJsonValue(rusni);
                                    if (e.TryGetProperty("ReplaceUrlSourceOutputKey", out var rusok)) r.ReplaceUrlSourceOutputKey = GetStringFromJsonValue(rusok);
                                    if (e.TryGetProperty("ReplaceUrlWithNodeKey", out var ruwnk))
                                    {
                                        if (ruwnk.ValueKind == JsonValueKind.True || ruwnk.ValueKind == JsonValueKind.False)
                                            r.ReplaceUrlWithNodeKey = ruwnk.GetBoolean();
                                        else if (bool.TryParse(GetStringFromJsonValue(ruwnk), out var ruwnkBool))
                                            r.ReplaceUrlWithNodeKey = ruwnkBool;
                                    }
                                    if (e.TryGetProperty("ReplaceParamsValue", out var rpv)) r.ReplaceParamsValue = GetStringFromJsonValue(rpv);
                                    if (e.TryGetProperty("ReplaceParamsSourceNodeId", out var rpsni)) r.ReplaceParamsSourceNodeId = GetStringFromJsonValue(rpsni);
                                    if (e.TryGetProperty("ReplaceParamsSourceOutputKey", out var rpsok)) r.ReplaceParamsSourceOutputKey = GetStringFromJsonValue(rpsok);
                                    if (e.TryGetProperty("ReplaceBodyValue", out var rbv)) r.ReplaceBodyValue = GetStringFromJsonValue(rbv);
                                    if (e.TryGetProperty("ReplaceBodySourceNodeId", out var rbsni)) r.ReplaceBodySourceNodeId = GetStringFromJsonValue(rbsni);
                                    if (e.TryGetProperty("ReplaceBodySourceOutputKey", out var rbsok)) r.ReplaceBodySourceOutputKey = GetStringFromJsonValue(rbsok);

                                    System.Diagnostics.Debug.WriteLine($"Deserialized RequestInterceptRule [{count}]: MatchUrl='{r.MatchUrlPattern}', ReplaceUrl='{r.ReplaceUrlValue}', ReplaceUrlWithNodeKey={r.ReplaceUrlWithNodeKey}");
                                    webNode.RequestInterceptRules.Add(r);
                                    count++;
                                }
                                catch (System.Text.Json.JsonException ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize RequestInterceptRule item at index {count}: {ex.Message}\n{ex.StackTrace}");
                                    // Continue with next item
                                    count++;
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error deserializing RequestInterceptRule item at index {count}: {ex.Message}\n{ex.StackTrace}");
                                    // Continue with next item
                                    count++;
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"✓ Successfully deserialized {count} RequestInterceptRules. Collection now has {webNode.RequestInterceptRules.Count} items");
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to enumerate RequestInterceptRules array: {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Error processing RequestInterceptRules array: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ rirJe is null or has no value");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error deserializing RequestInterceptRules: {ex.Message}\n{ex.StackTrace}");
                    // Continue - don't crash the entire workflow load
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"RequestInterceptRules property not found or null in Properties");
            }

            // Deserialize ResponseOutputs
            if (properties.TryGetValue("ResponseOutputs", out var roObj) && roObj != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"=== Deserializing ResponseOutputs for WebNode ===");
                    System.Diagnostics.Debug.WriteLine($"ResponseOutputs value type: {roObj.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"ResponseOutputs value: {roObj}");

                    webNode.ResponseOutputs.Clear();
                    JsonElement? roJe = null;
                    if (roObj is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.Array)
                        {
                            roJe = je;
                            System.Diagnostics.Debug.WriteLine($"ResponseOutputs is JsonElement array");
                        }
                        else if (je.ValueKind == JsonValueKind.String)
                        {
                            var roStr = je.GetString();
                            if (!string.IsNullOrWhiteSpace(roStr))
                            {
                                System.Diagnostics.Debug.WriteLine($"ResponseOutputs is JsonElement string: {roStr.Substring(0, Math.Min(100, roStr.Length))}...");
                                try
                                {
                                    var parsed = JsonSerializer.Deserialize<JsonElement>(roStr);
                                    if (parsed.ValueKind == JsonValueKind.Array)
                                    {
                                        roJe = parsed;
                                        System.Diagnostics.Debug.WriteLine($"✓ Successfully parsed JSON string to JsonElement array");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"⚠ Parsed JSON is not an array (ValueKind: {parsed.ValueKind})");
                                    }
                                }
                                catch (System.Text.Json.JsonException ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize ResponseOutputs JSON string: {ex.Message}\n{ex.StackTrace}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error parsing ResponseOutputs JSON string: {ex.Message}\n{ex.StackTrace}");
                                }
                            }
                        }
                    }
                    else if (roObj is string roStr && !string.IsNullOrWhiteSpace(roStr))
                    {
                        System.Diagnostics.Debug.WriteLine($"ResponseOutputs is JSON string: {roStr.Substring(0, Math.Min(100, roStr.Length))}...");
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<JsonElement>(roStr);
                            if (parsed.ValueKind == JsonValueKind.Array)
                            {
                                roJe = parsed;
                                System.Diagnostics.Debug.WriteLine($"✓ Successfully parsed JSON string to JsonElement array");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠ Parsed JSON is not an array (ValueKind: {parsed.ValueKind})");
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize ResponseOutputs JSON string: {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Error parsing ResponseOutputs JSON string: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ ResponseOutputs is unsupported type/value (type: {roObj.GetType().Name})");
                    }

                    if (roJe.HasValue)
                    {
                        try
                        {
                            int count = 0;
                            foreach (var e in roJe.Value.EnumerateArray())
                            {
                                try
                                {
                                    var ro = new WebResponseOutput();
                                    if (e.TryGetProperty("Key", out var k)) ro.Key = GetStringFromJsonValue(k);
                                    if (e.TryGetProperty("Url", out var u)) ro.Url = GetStringFromJsonValue(u);
                                    if (e.TryGetProperty("RequestMethod", out var rm)) ro.RequestMethod = GetStringFromJsonValue(rm);
                                    if (string.IsNullOrWhiteSpace(ro.RequestMethod)) ro.RequestMethod = "GET";
                                    if (e.TryGetProperty("ExtractType", out var et)) ro.ExtractType = GetStringFromJsonValue(et);
                                    if (string.IsNullOrWhiteSpace(ro.ExtractType)) ro.ExtractType = "Response";
                                    // WaitForCompletion (optional, backward compatible)
                                    if (e.TryGetProperty("WaitForCompletion", out var wfc))
                                    {
                                        var wfcStr = GetStringFromJsonValue(wfc);
                                        if (bool.TryParse(wfcStr, out var wfcBool))
                                            ro.WaitForCompletion = wfcBool;
                                    }

                                    System.Diagnostics.Debug.WriteLine($"Deserialized ResponseOutput [{count}]: Key='{ro.Key}', Url='{ro.Url}', Method='{ro.RequestMethod}', ExtractType='{ro.ExtractType}'");
                                    webNode.ResponseOutputs.Add(ro);
                                    count++;
                                }
                                catch (System.Text.Json.JsonException ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize ResponseOutput item at index {count}: {ex.Message}\n{ex.StackTrace}");
                                    // Continue with next item
                                    count++;
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error deserializing ResponseOutput item at index {count}: {ex.Message}\n{ex.StackTrace}");
                                    // Continue with next item
                                    count++;
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"✓ Successfully deserialized {count} ResponseOutputs. Collection now has {webNode.ResponseOutputs.Count} items");

                            // Rebuild DynamicOutputs sau khi load ResponseOutputs
                            webNode.RebuildResponseOutputs();
                            System.Diagnostics.Debug.WriteLine($"✓ RebuildResponseOutputs() called");
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to enumerate ResponseOutputs array: {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Error processing ResponseOutputs array: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ roJe is null or has no value");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error deserializing ResponseOutputs: {ex.Message}\n{ex.StackTrace}");
                    // Continue - don't crash the entire workflow load
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ResponseOutputs property not found or null in Properties");
            }
    }

    // -- GET (Serialize) --

    private static void GetWebNodeProperties(WebNode webNode, Dictionary<string, object> dict)
    {
            // Update bindings trong dialog nếu đang mở để đảm bảo giá trị được cập nhật trước khi serialize
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Attempting to update bindings before serialize ===");

                // Tìm tất cả WorkflowEditorWindow đang mở (có thể có nhiều window)
                var allWindows = Application.Current?.Windows.OfType<Views.WorkflowEditorWindow>().ToList();
                if (allWindows != null && allWindows.Count > 0)
                {
                    foreach (var window in allWindows)
                    {
                        try
                        {
                            var field = typeof(Views.WorkflowEditorWindow).GetField("_nodeDialogManager",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (field?.GetValue(window) is Services.Interaction.NodeDialogManager dialogManager)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found NodeDialogManager, calling UpdateAllBindingsIfWebNodeDialog()");
                                dialogManager.UpdateAllBindingsIfWebNodeDialog();
                                System.Diagnostics.Debug.WriteLine($"✓ UpdateAllBindingsIfWebNodeDialog() called successfully");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error accessing NodeDialogManager from window: {ex.Message}");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No WorkflowEditorWindow found in Application.Current.Windows");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error updating bindings before serialize: {ex.Message}\n{ex.StackTrace}");
                // Continue - không block serialize
            }

            try
            {
                dict["Width"] = webNode.Width;
                dict["Height"] = webNode.Height;
                if (!string.IsNullOrEmpty(webNode.ExtractUrl))
                    dict["ExtractUrl"] = webNode.ExtractUrl;
                if (!string.IsNullOrEmpty(webNode.ExtractRequestMethod))
                    dict["ExtractRequestMethod"] = webNode.ExtractRequestMethod;
                if (!string.IsNullOrEmpty(webNode.ExtractStatusCode))
                    dict["ExtractStatusCode"] = webNode.ExtractStatusCode;
                if (webNode.BlockingRules != null && webNode.BlockingRules.Count > 0)
                {
                    var brList = webNode.BlockingRules.Select(r =>
                    {
                        var ruleDict = new Dictionary<string, object>
                        {
                            ["UrlPattern"] = r.UrlPattern,
                            ["Method"] = r.Method ?? "All"  // Method cho URL cha
                        };

                        // Serialize child rules nếu có
                        if (r.ChildRules != null && r.ChildRules.Count > 0)
                        {
                            var childList = r.ChildRules.Select(c => new Dictionary<string, object>
                            {
                                ["UrlPattern"] = c.UrlPattern,
                                ["Method"] = c.Method ?? "All"
                            }).ToList();

                            ruleDict["ChildRules"] = childList;
                        }

                        return ruleDict;
                    }).ToList();

                    dict["BlockingRules"] = JsonSerializer.Serialize(brList);
                }
                dict["SyncLiveOutputsToResults"] = webNode.SyncLiveOutputsToResults;

                // Output waiting behavior (timeout + mode)
                dict["ResponseOutputsWaitTimeoutMs"] = webNode.ResponseOutputsWaitTimeoutMs;
                dict["ResponseOutputsWaitMode"] = webNode.ResponseOutputsWaitMode.ToString();

                // JS injection (nhiều Node+Key -> WebView2)
                if (webNode.JsSources != null && webNode.JsSources.Count > 0)
                {
                    var arr = webNode.JsSources.Select(m => new Dictionary<string, object?>
                    {
                        ["SourceNodeId"] = m.SourceNodeId,
                        ["SourceOutputKey"] = m.SourceOutputKey,
                        ["AutoTimerEnabled"] = m.AutoTimerEnabled,
                        ["AutoTimerIntervalValue"] = m.AutoTimerIntervalValue,
                        ["AutoTimerIntervalUnit"] = m.AutoTimerIntervalUnit
                    }).ToList();
                    dict["JsSources"] = JsonSerializer.Serialize(arr);
                }

                // Serialize InputMappings (giống CodeNode nhưng dùng WebInputMapping)
                if (webNode.InputMappings != null && webNode.InputMappings.Count > 0)
                {
                    var arr = webNode.InputMappings.Select(m => new Dictionary<string, string?>
                    {
                        ["SourceNodeId"] = m.SourceNodeId,
                        ["SourceOutputKey"] = m.SourceOutputKey,
                        ["InputKeyOverride"] = m.InputKeyOverride
                    }).ToList();
                    dict["InputMappings"] = JsonSerializer.Serialize(arr);
                }

                // Auto-reload timer
                dict["AutoReloadEnabled"] = webNode.AutoReloadEnabled;
                dict["EnableSleepMode"] = webNode.EnableSleepMode;
                dict["SleepIdleTimeoutValue"] = webNode.SleepIdleTimeoutValue;
                dict["SleepIdleTimeoutUnit"] = webNode.SleepIdleTimeoutUnit;
                dict["AutoReloadIntervalValue"] = webNode.AutoReloadIntervalValue;
                dict["AutoReloadIntervalUnit"] = webNode.AutoReloadIntervalUnit;

                // Block all requests after first match
                dict["BlockAllRequestsAfterFirstMatch"] = webNode.BlockAllRequestsAfterFirstMatch;

                // Serialize per-domain CSS zoom for WebNode
                if (!string.IsNullOrWhiteSpace(webNode.LastHost))
                    dict["Web_LastHost"] = webNode.LastHost;
                if (webNode.CssZoom > 0)
                    dict["Web_CssZoom"] = webNode.CssZoom;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error serializing WebNode basic properties: {ex.Message}\n{ex.StackTrace}");
                // Continue - don't crash
            }

            // Serialize RequestInterceptRules - chỉ serialize khi có items và không null
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Serializing RequestInterceptRules for WebNode ===");
                System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is null: {webNode.RequestInterceptRules == null}");
                System.Diagnostics.Debug.WriteLine($"RequestInterceptRules Count: {webNode.RequestInterceptRules?.Count ?? 0}");

                if (webNode.RequestInterceptRules != null && webNode.RequestInterceptRules.Count > 0)
                {
                    var arr = new List<Dictionary<string, object>>();
                    int index = 0;
                    foreach (var r in webNode.RequestInterceptRules)
                    {
                        try
                        {
                            // Lấy giá trị trực tiếp từ object để đảm bảo không bị mất
                            var matchUrl = r?.MatchUrlPattern ?? "";
                            var replaceUrl = r?.ReplaceUrlValue ?? "";
                            var replaceUrlNodeId = r?.ReplaceUrlSourceNodeId ?? "";
                            var replaceUrlKey = r?.ReplaceUrlSourceOutputKey ?? "";
                            var replaceUrlWithNodeKey = r?.ReplaceUrlWithNodeKey ?? false;
                            var replaceParams = r?.ReplaceParamsValue ?? "";
                            var replaceParamsNodeId = r?.ReplaceParamsSourceNodeId ?? "";
                            var replaceParamsKey = r?.ReplaceParamsSourceOutputKey ?? "";
                            var replaceBody = r?.ReplaceBodyValue ?? "";
                            var replaceBodyNodeId = r?.ReplaceBodySourceNodeId ?? "";
                            var replaceBodyKey = r?.ReplaceBodySourceOutputKey ?? "";

                            System.Diagnostics.Debug.WriteLine($"[{index}] RequestInterceptRule - MatchUrl='{matchUrl}', ReplaceUrl='{replaceUrl}', ReplaceUrlWithNodeKey={replaceUrlWithNodeKey}");

                            var ruleDict = new Dictionary<string, object>();
                            ruleDict["MatchUrlPattern"] = matchUrl;
                            ruleDict["ReplaceUrlValue"] = replaceUrl;
                            ruleDict["ReplaceUrlSourceNodeId"] = replaceUrlNodeId;
                            ruleDict["ReplaceUrlSourceOutputKey"] = replaceUrlKey;
                            ruleDict["ReplaceUrlWithNodeKey"] = replaceUrlWithNodeKey;
                            ruleDict["ReplaceParamsValue"] = replaceParams;
                            ruleDict["ReplaceParamsSourceNodeId"] = replaceParamsNodeId;
                            ruleDict["ReplaceParamsSourceOutputKey"] = replaceParamsKey;
                            ruleDict["ReplaceBodyValue"] = replaceBody;
                            ruleDict["ReplaceBodySourceNodeId"] = replaceBodyNodeId;
                            ruleDict["ReplaceBodySourceOutputKey"] = replaceBodyKey;
                            arr.Add(ruleDict);
                            index++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error serializing RequestInterceptRule item at index {index}: {ex.Message}\n{ex.StackTrace}");
                            // Continue with next item
                            index++;
                        }
                    }

                    if (arr.Count > 0)
                    {
                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = false,
                            MaxDepth = 32
                        };
                        var json = JsonSerializer.Serialize(arr, options);
                        dict["RequestInterceptRules"] = json;
                        System.Diagnostics.Debug.WriteLine($"✓ Successfully serialized {arr.Count} RequestInterceptRules to JSON: {json}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Warning: RequestInterceptRules collection has {webNode.RequestInterceptRules.Count} items but arr is empty!");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is null or empty (Count: {webNode.RequestInterceptRules?.Count ?? 0})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error serializing RequestInterceptRules: {ex.Message}\n{ex.StackTrace}");
                // Continue - don't crash the save operation
            }

            // Serialize ResponseOutputs - serialize tất cả items (kể cả rỗng) để đảm bảo cấu hình được lưu
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Serializing ResponseOutputs for WebNode ===");
                System.Diagnostics.Debug.WriteLine($"ResponseOutputs is null: {webNode.ResponseOutputs == null}");
                System.Diagnostics.Debug.WriteLine($"ResponseOutputs Count: {webNode.ResponseOutputs?.Count ?? 0}");

                if (webNode.ResponseOutputs != null && webNode.ResponseOutputs.Count > 0)
                {
                    var responseOutputsArr = new List<Dictionary<string, string>>();
                    int index = 0;
                    foreach (var ro in webNode.ResponseOutputs)
                    {
                        try
                        {
                            // Lấy giá trị trực tiếp từ object để đảm bảo không bị mất
                            var key = ro?.Key ?? "";
                            var url = ro?.Url ?? "";
                            var method = ro?.RequestMethod ?? "GET";
                            var extractType = ro?.ExtractType ?? "Response";
                            var waitForCompletion = ro?.WaitForCompletion ?? false;

                            // Debug log để kiểm tra giá trị
                            System.Diagnostics.Debug.WriteLine($"[{index}] ResponseOutput - Key='{key}', Url='{url}', Method='{method}', ExtractType='{extractType}'");

                            // Serialize tất cả items, kể cả khi Key hoặc Url rỗng (để user có thể chỉnh sửa sau)
                            var itemDict = new Dictionary<string, string>
                            {
                                ["Key"] = key,
                                ["Url"] = url,
                                ["RequestMethod"] = method,
                                ["ExtractType"] = extractType,
                                ["WaitForCompletion"] = waitForCompletion ? "true" : "false"
                            };
                            responseOutputsArr.Add(itemDict);
                            index++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error serializing ResponseOutput item at index {index}: {ex.Message}\n{ex.StackTrace}");
                            // Continue with next item
                            index++;
                        }
                    }

                    // Luôn serialize nếu có items (kể cả rỗng) để đảm bảo cấu hình được lưu
                    if (responseOutputsArr.Count > 0)
                    {
                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = false,
                            MaxDepth = 32
                        };
                        var json = JsonSerializer.Serialize(responseOutputsArr, options);
                        dict["ResponseOutputs"] = json;
                        System.Diagnostics.Debug.WriteLine($"✓ Successfully serialized {responseOutputsArr.Count} ResponseOutputs to JSON: {json}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Warning: ResponseOutputs collection has {webNode.ResponseOutputs.Count} items but responseOutputsArr is empty!");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ResponseOutputs is null or empty (Count: {webNode.ResponseOutputs?.Count ?? 0})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error serializing ResponseOutputs: {ex.Message}\n{ex.StackTrace}");
                // Continue - don't crash the save operation
            }
    }

}
