using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreStringSplitNodeProperties(StringSplitNode stringSplitNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("RegexPattern", out var regexObj))
                stringSplitNode.RegexPattern = regexObj?.ToString() ?? @"\r?\n";
            if (properties.TryGetValue("OutputKey", out var outputKeyObj))
                stringSplitNode.OutputKey = outputKeyObj?.ToString() ?? "ListItems";

    }

    private static void RestoreEmbedApplicationNodeProperties(EmbedApplicationNode embedApp, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("ProcessName", out var pnObj))
                embedApp.ProcessName = pnObj?.ToString() ?? string.Empty;
            
            if (properties.TryGetValue("ProcessId", out var pidObj) && int.TryParse(pidObj?.ToString(), out var pid))
                embedApp.ProcessId = pid;
            
            // Parse IntPtr từ string (Int64)
            if (properties.TryGetValue("WindowHandle", out var whObj) && long.TryParse(whObj?.ToString(), out var whLong))
                embedApp.WindowHandle = new IntPtr(whLong);
            
            if (properties.TryGetValue("WindowTitle", out var wtObj))
                embedApp.WindowTitle = wtObj?.ToString() ?? string.Empty;
            
            if (properties.TryGetValue("EmbeddedWidth", out var ewObj) && double.TryParse(ewObj?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ew))
                embedApp.EmbeddedWidth = ew;
            
            if (properties.TryGetValue("EmbeddedHeight", out var ehObj) && double.TryParse(ehObj?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var eh))
                embedApp.EmbeddedHeight = eh;
            
            if (properties.TryGetValue("IsActive", out var iaObj) && bool.TryParse(iaObj?.ToString(), out var ia))
                embedApp.IsActive = ia;
            
            if (properties.TryGetValue("ShowBorder", out var sbObj) && bool.TryParse(sbObj?.ToString(), out var sb))
                embedApp.ShowBorder = sb;
            
            if (properties.TryGetValue("AllowInteraction", out var aiObj) && bool.TryParse(aiObj?.ToString(), out var ai))
                embedApp.AllowInteraction = ai;
            
            if (properties.TryGetValue("AutoRefresh", out var arObj) && bool.TryParse(arObj?.ToString(), out var ar))
                embedApp.AutoRefresh = ar;
            
            if (properties.TryGetValue("RefreshRate", out var rrObj2) && int.TryParse(rrObj2?.ToString(), out var rr))
                embedApp.RefreshRate = rr;
            
            if (properties.TryGetValue("CaptureMode", out var cmObj) && Enum.TryParse<EmbedCaptureMode>(cmObj?.ToString(), out var cm))
                embedApp.CaptureMode = cm;
            
            if (properties.TryGetValue("HasEmbeddedWindow", out var hewObj) && bool.TryParse(hewObj?.ToString(), out var hew))
                embedApp.HasEmbeddedWindow = hew;
            
            embedApp.RebuildDynamicOutputs();
    }

    private static void RestoreKeyValueBridgeNodeProperties(KeyValueBridgeNode kvNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("mode", out var modeObj) && modeObj != null)
            {
                var m = modeObj.ToString()?.Trim();
                kvNode.IsPassKeyMode = !string.Equals(m, "get", StringComparison.OrdinalIgnoreCase);
            }
            if (properties.TryGetValue("IsPassKeyMode", out var ipkmObj) && ipkmObj != null && bool.TryParse(ipkmObj.ToString(), out var ipkm))
                kvNode.IsPassKeyMode = ipkm;
            if (properties.TryGetValue("key", out var keyObj))
                kvNode.KvChannelKey = keyObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("KvChannelKey", out var kchObj))
                kvNode.KvChannelKey = kchObj?.ToString() ?? kvNode.KvChannelKey;
            if (properties.TryGetValue("selectedSourceNodeId", out var ssnObj))
                kvNode.SelectedSourceBridgeNodeId = ssnObj?.ToString();
            if (properties.TryGetValue("SelectedSourceBridgeNodeId", out var ssbObj))
                kvNode.SelectedSourceBridgeNodeId = ssbObj?.ToString() ?? kvNode.SelectedSourceBridgeNodeId;
            if (properties.TryGetValue("interval", out var intObj) && intObj != null && int.TryParse(intObj.ToString(), out var intv))
                kvNode.PollIntervalValue = intv;
            if (properties.TryGetValue("PollIntervalValue", out var pivObj) && pivObj != null && int.TryParse(pivObj.ToString(), out var piv))
                kvNode.PollIntervalValue = piv;
            if (properties.TryGetValue("intervalUnit", out var iuObj) && iuObj != null &&
                Enum.TryParse<KeyValueBridgePollUnit>(iuObj.ToString(), out var iu))
                kvNode.PollIntervalUnit = iu;
            if (properties.TryGetValue("PollIntervalUnit", out var piuObj) && piuObj != null &&
                Enum.TryParse<KeyValueBridgePollUnit>(piuObj.ToString(), out var piu))
                kvNode.PollIntervalUnit = piu;

            if (properties.TryGetValue("EnableDataCleanup", out var edcObj) && edcObj != null &&
                bool.TryParse(edcObj.ToString(), out var edc))
                kvNode.EnableDataCleanup = edc;
            if (properties.TryGetValue("CleanupTargetBridgeNodeId", out var ctbnObj))
                kvNode.CleanupTargetBridgeNodeId = ctbnObj?.ToString();
            if (properties.TryGetValue("CleanupTargetKey", out var ctkObj))
                kvNode.CleanupTargetKey = ctkObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CleanupClearAllNodeData", out var ccandObj) && ccandObj != null &&
                bool.TryParse(ccandObj.ToString(), out var ccand))
                kvNode.CleanupClearAllNodeData = ccand;
            if (properties.TryGetValue("CleanupArrayFilterField", out var caffObj))
                kvNode.CleanupArrayFilterField = caffObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CleanupArrayFilterValue", out var cafvObj))
                kvNode.CleanupArrayFilterValue = cafvObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CleanupRemoveAllMatchedArrayItems", out var cramObj) && cramObj != null &&
                bool.TryParse(cramObj.ToString(), out var cram))
                kvNode.CleanupRemoveAllMatchedArrayItems = cram;
            if (properties.TryGetValue("CleanupTriggerSourceNodeId", out var ctsnObj))
                kvNode.CleanupTriggerSourceNodeId = ctsnObj?.ToString();
            if (properties.TryGetValue("CleanupTriggerSourceOutputKey", out var ctskObj))
                kvNode.CleanupTriggerSourceOutputKey = ctskObj?.ToString();
            if (properties.TryGetValue("CleanupTriggerExpectedValue", out var ctevObj))
                kvNode.CleanupTriggerExpectedValue = ctevObj?.ToString() ?? "true";
            if (properties.TryGetValue("CleanupKeySourceNodeId", out var cksnObj))
                kvNode.CleanupKeySourceNodeId = cksnObj?.ToString();
            if (properties.TryGetValue("CleanupKeySourceOutputKey", out var ckskObj))
                kvNode.CleanupKeySourceOutputKey = ckskObj?.ToString();
            if (properties.TryGetValue("CleanupFilterFieldSourceNodeId", out var cffsnObj))
                kvNode.CleanupFilterFieldSourceNodeId = cffsnObj?.ToString();
            if (properties.TryGetValue("CleanupFilterFieldSourceOutputKey", out var cffskObj))
                kvNode.CleanupFilterFieldSourceOutputKey = cffskObj?.ToString();
            if (properties.TryGetValue("CleanupFilterValueSourceNodeId", out var cfvsnObj))
                kvNode.CleanupFilterValueSourceNodeId = cfvsnObj?.ToString();
            if (properties.TryGetValue("CleanupFilterValueSourceOutputKey", out var cfvskObj))
                kvNode.CleanupFilterValueSourceOutputKey = cfvskObj?.ToString();

            if (properties.TryGetValue("AdditionalAppendSources", out var aasObj))
            {
                List<KeyValueBridgeAppendSource>? parsedAppendSources = null;

                if (aasObj is string aasJson)
                {
                    try
                    {
                        var rawList = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(aasJson);
                        if (rawList != null)
                        {
                            parsedAppendSources = rawList
                                .Select(item => new KeyValueBridgeAppendSource
                                {
                                    SourceNodeId = item.TryGetValue("SourceNodeId", out var nodeIdObj)
                                        ? nodeIdObj?.ToString() ?? string.Empty
                                        : string.Empty,
                                    SourceOutputKey = item.TryGetValue("SourceOutputKey", out var outputKeyObj)
                                        ? outputKeyObj?.ToString()
                                        : null
                                })
                                .Where(x => !string.IsNullOrWhiteSpace(x.SourceNodeId))
                                .ToList();
                        }
                    }
                    catch
                    {
                        try
                        {
                            parsedAppendSources = JsonSerializer.Deserialize<List<KeyValueBridgeAppendSource>>(aasJson)?
                                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                                .Select(x => new KeyValueBridgeAppendSource
                                {
                                    SourceNodeId = x.SourceNodeId?.Trim() ?? string.Empty,
                                    SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                                })
                                .ToList();
                        }
                        catch
                        {
                            // Ignore invalid AdditionalAppendSources format.
                        }
                    }
                }
                else if (aasObj is JsonElement aasJe)
                {
                    try
                    {
                        if (aasJe.ValueKind == JsonValueKind.String)
                        {
                            var s = aasJe.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                var rawList = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(s);
                                if (rawList != null)
                                {
                                    parsedAppendSources = rawList
                                        .Select(item => new KeyValueBridgeAppendSource
                                        {
                                            SourceNodeId = item.TryGetValue("SourceNodeId", out var nodeIdObj)
                                                ? nodeIdObj?.ToString() ?? string.Empty
                                                : string.Empty,
                                            SourceOutputKey = item.TryGetValue("SourceOutputKey", out var outputKeyObj)
                                                ? outputKeyObj?.ToString()
                                                : null
                                        })
                                        .Where(x => !string.IsNullOrWhiteSpace(x.SourceNodeId))
                                        .ToList();
                                }
                            }
                        }
                        else if (aasJe.ValueKind == JsonValueKind.Array)
                        {
                            parsedAppendSources = JsonSerializer.Deserialize<List<KeyValueBridgeAppendSource>>(aasJe.GetRawText())?
                                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                                .Select(x => new KeyValueBridgeAppendSource
                                {
                                    SourceNodeId = x.SourceNodeId?.Trim() ?? string.Empty,
                                    SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                                })
                                .ToList();
                        }
                    }
                    catch
                    {
                        // Ignore invalid AdditionalAppendSources format.
                    }
                }

                if (parsedAppendSources != null)
                    kvNode.AdditionalAppendSources = parsedAppendSources;
            }

            kvNode.RebuildDataPorts();
            kvNode.RefreshFlowPortsVisibility();
    }

    private static void RestoreGitSourceNodeProperties(GitSourceNode gitSourceNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("RepoUrl", out var repoUrlObj))
                gitSourceNode.RepoUrl = repoUrlObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("LocalPath", out var localPathObj))
                gitSourceNode.LocalPath = localPathObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("Branch", out var branchObj))
                gitSourceNode.Branch = branchObj?.ToString() ?? "main";
            if (properties.TryGetValue("DisplayName", out var displayNameObj))
                gitSourceNode.DisplayName = displayNameObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("IconKey", out var iconKeyObj))
                gitSourceNode.IconKey = iconKeyObj?.ToString() ?? "git-alt brands";
            if (properties.TryGetValue("IconColorKey", out var iconColorObj))
                gitSourceNode.IconColorKey = iconColorObj?.ToString() ?? "White";
            if (properties.TryGetValue("TooltipText", out var tooltipObj))
                gitSourceNode.TooltipText = tooltipObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ContextMenuDescription", out var ctxDescObj))
                gitSourceNode.ContextMenuDescription = ctxDescObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("VscodiumPath", out var vscodiumObj))
                gitSourceNode.VscodiumPath = vscodiumObj?.ToString() ?? "vscodium";
            if (properties.TryGetValue("LastCommitHash", out var commitObj))
                gitSourceNode.LastCommitHash = commitObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("LastPullTime", out var pullTimeObj))
                gitSourceNode.LastPullTime = pullTimeObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("AutoOpenOnExecute", out var autoOpenObj) &&
                autoOpenObj != null && bool.TryParse(autoOpenObj.ToString(), out var autoOpen))
                gitSourceNode.AutoOpenOnExecute = autoOpen;
            if (properties.TryGetValue("CommandText", out var cmdObj))
                gitSourceNode.CommandText = cmdObj?.ToString() ?? string.Empty;
    }

    private static void RestoreBodyContainerNodeProperties(BodyContainerNode bodyContainerNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("BodyWidth", out var widthObj) &&
                widthObj != null &&
                double.TryParse(widthObj.ToString(), out var bodyWidth))
            {
                bodyContainerNode.BodyWidth = bodyWidth;
            }
            if (properties.TryGetValue("BodyHeight", out var heightObj) &&
                heightObj != null &&
                double.TryParse(heightObj.ToString(), out var bodyHeight))
            {
                bodyContainerNode.BodyHeight = bodyHeight;
            }
            if (properties.TryGetValue("BodyBackgroundColorHex", out var bgObj))
                bodyContainerNode.BodyBackgroundColorHex = bgObj?.ToString() ?? bodyContainerNode.BodyBackgroundColorHex;
            if (properties.TryGetValue("BodyBorderColorHex", out var borderObj))
                bodyContainerNode.BodyBorderColorHex = borderObj?.ToString() ?? bodyContainerNode.BodyBorderColorHex;
            if (properties.TryGetValue("UseUnifiedColors", out var unifiedObj) &&
                unifiedObj != null &&
                bool.TryParse(unifiedObj.ToString(), out var unified))
            {
                bodyContainerNode.UseUnifiedColors = unified;
            }
            if (properties.TryGetValue("BackgroundOpacityPercent", out var opacityObj) &&
                opacityObj != null &&
                double.TryParse(opacityObj.ToString(), out var opacity))
            {
                bodyContainerNode.BackgroundOpacityPercent = opacity;
            }
            if (properties.TryGetValue("LockInnerNodes", out var lockObj) &&
                lockObj != null &&
                bool.TryParse(lockObj.ToString(), out var lockInner))
            {
                bodyContainerNode.LockInnerNodes = lockInner;
            }
            if (properties.TryGetValue("BorderOpacityPercent", out var borderOpacityObj) &&
                borderOpacityObj != null &&
                double.TryParse(borderOpacityObj.ToString(), out var borderOpacity))
            {
                bodyContainerNode.BorderOpacityPercent = borderOpacity;
            }
            if (properties.TryGetValue("BorderThickness", out var borderThicknessObj) &&
                borderThicknessObj != null &&
                double.TryParse(borderThicknessObj.ToString(), out var borderThickness))
            {
                bodyContainerNode.BorderThickness = borderThickness;
            }
            if (properties.TryGetValue("BorderDashSpacing", out var borderDashSpacingObj) &&
                borderDashSpacingObj != null &&
                double.TryParse(borderDashSpacingObj.ToString(), out var borderDashSpacing))
            {
                bodyContainerNode.BorderDashSpacing = borderDashSpacing;
            }
            if (properties.TryGetValue("BorderDashStyle", out var borderDashStyleObj) &&
                borderDashStyleObj != null &&
                Enum.TryParse<BorderDashStyle>(borderDashStyleObj.ToString(), out var borderDashStyle))
            {
                bodyContainerNode.BorderDashStyle = borderDashStyle;
            }
            if (properties.TryGetValue("IconOpacityPercent", out var iconOpacityObj) &&
                iconOpacityObj != null &&
                double.TryParse(iconOpacityObj.ToString(), out var iconOpacity))
            {
                bodyContainerNode.IconOpacityPercent = iconOpacity;
            }
            if (properties.TryGetValue("LockCanvasSize", out var lockCanvasSizeObj) &&
                lockCanvasSizeObj != null &&
                bool.TryParse(lockCanvasSizeObj.ToString(), out var lockCanvasSize))
            {
                bodyContainerNode.LockCanvasSize = lockCanvasSize;
            }
            if (properties.TryGetValue("LockedZoomLevel", out var lockedZoomLevelObj) &&
                lockedZoomLevelObj != null &&
                double.TryParse(lockedZoomLevelObj.ToString(), out var lockedZoomLevel))
            {
                bodyContainerNode.LockedZoomLevel = lockedZoomLevel;
            }
            if (properties.TryGetValue("LockedX", out var lockedXObj) &&
                lockedXObj != null &&
                double.TryParse(lockedXObj.ToString(), out var lockedX))
            {
                bodyContainerNode.LockedX = lockedX;
            }
            if (properties.TryGetValue("LockedY", out var lockedYObj) &&
                lockedYObj != null &&
                double.TryParse(lockedYObj.ToString(), out var lockedY))
            {
                bodyContainerNode.LockedY = lockedY;
            }
    }

    private static void RestoreOutputNodeProperties(OutputNode outputNode, Dictionary<string, object> properties)
    {
            // Deserialize OutputKey
            if (properties.TryGetValue("OutputKey", out var outputKeyObj))
                outputNode.OutputKey = outputKeyObj?.ToString() ?? "output";

            // Deserialize FormatString
            if (properties.TryGetValue("FormatString", out var formatStrObj))
                outputNode.FormatString = formatStrObj?.ToString() ?? string.Empty;

            // Deserialize SaveToClipboard
            if (properties.TryGetValue("SaveToClipboard", out var saveToClipboardObj) &&
                bool.TryParse(saveToClipboardObj?.ToString(), out var saveToClipboard))
                outputNode.SaveToClipboard = saveToClipboard;

            // Deserialize InputVariables
            if (properties.TryGetValue("InputVariables", out var variablesObj))
            {
                List<InputVariable>? parsedVariables = null;

                if (variablesObj is string jsonVariables)
                {
                    try
                    {
                        var variableData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonVariables);
                        if (variableData != null)
                        {
                            parsedVariables = variableData.Select(v => new InputVariable
                            {
                                VariableKey = v.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? string.Empty : string.Empty,
                                SourceNodeId = v.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                                SourceOutputKey = v.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                            }).ToList();
                        }
                    }
                    catch
                    {
                        // Try alternative format (direct deserialize)
                        try
                        {
                            parsedVariables = JsonSerializer.Deserialize<List<InputVariable>>(jsonVariables);
                        }
                        catch { }
                    }
                }
                else if (variablesObj is JsonElement jsonElement)
                {
                    try
                    {
                        if (jsonElement.ValueKind == JsonValueKind.String)
                        {
                            var jsonString = jsonElement.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonString))
                            {
                                var variableData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonString);
                                if (variableData != null)
                                {
                                    parsedVariables = variableData.Select(v => new InputVariable
                                    {
                                        VariableKey = v.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? string.Empty : string.Empty,
                                        SourceNodeId = v.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                                        SourceOutputKey = v.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                                    }).ToList();
                                }
                            }
                        }
                        else if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            parsedVariables = JsonSerializer.Deserialize<List<InputVariable>>(jsonElement.GetRawText());
                        }
                    }
                    catch { }
                }

                if (parsedVariables != null)
                {
                    outputNode.InputVariables = parsedVariables;
                    outputNode.RebuildDynamicOutputs();
                }
            }

    }

    private static void RestoreMacroRecorderNodeProperties(MacroRecorderNode macroRecorderNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("OutputKey", out var mkObj))
                macroRecorderNode.OutputKey = mkObj?.ToString() ?? "macroData";
            if (properties.TryGetValue("MacroDataJson", out var mdjObj))
                macroRecorderNode.MacroDataJson = mdjObj?.ToString() ?? "";
            if (properties.TryGetValue("PlaybackMode", out var pmObj) &&
                Enum.TryParse<MacroPlaybackMode>(pmObj?.ToString(), out var pm))
                macroRecorderNode.PlaybackMode = pm;
            if (properties.TryGetValue("RepeatIntervalMs", out var rimObj) &&
                int.TryParse(rimObj?.ToString(), out var rim))
                macroRecorderNode.RepeatIntervalMs = Math.Max(0, rim);
            if (properties.TryGetValue("RepeatCount", out var mrcObj) &&
                int.TryParse(mrcObj?.ToString(), out var mrc))
                macroRecorderNode.RepeatCount = Math.Max(1, mrc);
            if (properties.TryGetValue("VisualPlaybackMode", out var vpmObj) &&
                Enum.TryParse<VisualPlaybackMode>(vpmObj?.ToString(), out var vpm))
                macroRecorderNode.VisualPlaybackMode = vpm;
            if (properties.TryGetValue("ShowMouseTrail", out var smtObj) &&
                bool.TryParse(smtObj?.ToString(), out var smt))
                macroRecorderNode.ShowMouseTrail = smt;
            if (properties.TryGetValue("CountdownSeconds", out var csObj) &&
                int.TryParse(csObj?.ToString(), out var cs))
                macroRecorderNode.CountdownSeconds = Math.Max(0, Math.Min(10, cs));
            if (properties.TryGetValue("StayOnTargetAfterExecution", out var staeObj) &&
                bool.TryParse(staeObj?.ToString(), out var stae))
                macroRecorderNode.StayOnTargetAfterExecution = stae;
            if (properties.TryGetValue("ExecutionMode", out var emObj) &&
                Enum.TryParse<MacroExecutionMode>(emObj?.ToString(), out var em))
                macroRecorderNode.ExecutionMode = em;
            if (properties.TryGetValue("TargetProcessName", out var tpnObj))
                macroRecorderNode.TargetProcessName = tpnObj?.ToString() ?? "";
            if (properties.TryGetValue("TargetWindowTitle", out var twtObj))
                macroRecorderNode.TargetWindowTitle = twtObj?.ToString() ?? "";
    }

    private static void RestoreBorderHighlightNodeProperties(BorderHighlightNode borderHighlightNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("BorderColorHex", out var bchObj))
                borderHighlightNode.BorderColorHex = bchObj?.ToString() ?? "#00D2FF";
            if (properties.TryGetValue("BorderThickness", out var btObj) &&
                int.TryParse(btObj?.ToString(), out var bt))
                borderHighlightNode.BorderThickness = Math.Max(1, Math.Min(10, bt));
            if (properties.TryGetValue("GradientSize", out var gsObj) &&
                int.TryParse(gsObj?.ToString(), out var gs))
                borderHighlightNode.GradientSize = Math.Max(5, Math.Min(50, gs));
            if (properties.TryGetValue("Opacity", out var opObj) &&
                double.TryParse(opObj?.ToString(), out var op))
                borderHighlightNode.Opacity = Math.Max(0.1, Math.Min(1.0, op));
            if (properties.TryGetValue("EffectType", out var etObj) &&
                Enum.TryParse<BorderEffectType>(etObj?.ToString(), out var et))
                borderHighlightNode.EffectType = et;
            if (properties.TryGetValue("HighlightMode", out var hmObj) &&
                Enum.TryParse<BorderHighlightMode>(hmObj?.ToString(), out var hm))
                borderHighlightNode.HighlightMode = hm;
            if (properties.TryGetValue("TargetProcessName", out var tpnObj2))
                borderHighlightNode.TargetProcessName = tpnObj2?.ToString() ?? "";
            if (properties.TryGetValue("TargetWindowTitle", out var twtObj2))
                borderHighlightNode.TargetWindowTitle = twtObj2?.ToString() ?? "";
            if (properties.TryGetValue("DurationMs", out var dmObj) &&
                int.TryParse(dmObj?.ToString(), out var dm))
                borderHighlightNode.DurationMs = Math.Max(0, dm);
            if (properties.TryGetValue("DurationUnit", out var duObj) &&
                Enum.TryParse<DurationUnit>(duObj?.ToString(), out var du))
                borderHighlightNode.DurationUnit = du;
            if (properties.TryGetValue("WaitForCompletion", out var wfcObj) &&
                bool.TryParse(wfcObj?.ToString(), out var wfc))
                borderHighlightNode.WaitForCompletion = wfc;
            if (properties.TryGetValue("SelectedWindowJson", out var swjObj))
                borderHighlightNode.SelectedWindowJson = swjObj?.ToString() ?? "";
            if (properties.TryGetValue("NodesToDisableJson", out var ntdObj))
                borderHighlightNode.NodesToDisableJson = ntdObj?.ToString() ?? "[]";
            if (properties.TryGetValue("TargetProcessId", out var tpidObj) &&
                uint.TryParse(tpidObj?.ToString(), out var tpid))
                borderHighlightNode.TargetProcessId = tpid;
    }

    private static void RestoreNotificationNodeProperties(NotificationNode notificationNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("DefaultDurationSeconds", out var durObj) &&
                int.TryParse(durObj?.ToString(), out var dur))
            {
                notificationNode.DefaultDurationSeconds = dur;
            }

            // Title properties — required for all nodes
            if (properties.TryGetValue("TitleDisplayMode", out var tdm) &&
                Enum.TryParse<TitleDisplayMode>(tdm?.ToString(), out var tdmVal))
                notificationNode.TitleDisplayMode = tdmVal;
            if (properties.TryGetValue("TitleColorMode", out var tcm) &&
                Enum.TryParse<TitleColorMode>(tcm?.ToString(), out var tcmVal))
                notificationNode.TitleColorMode = tcmVal;
            if (properties.TryGetValue("TitleColorKey", out var tck))
                notificationNode.TitleColorKey = tck?.ToString();

            // TitleInput
            if (properties.TryGetValue("TitleInput", out var titleInputObj) && titleInputObj != null)
            {
                try
                {
                    Dictionary<string, object?>? dict = null;

                    if (titleInputObj is string titleJson && !string.IsNullOrWhiteSpace(titleJson))
                    {
                        dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(titleJson);
                    }
                    else if (titleInputObj is JsonElement titleJe)
                    {
                        if (titleJe.ValueKind == JsonValueKind.String)
                        {
                            var jsonStr = titleJe.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonStr))
                                dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonStr);
                        }
                        else if (titleJe.ValueKind == JsonValueKind.Object)
                        {
                            dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(titleJe.GetRawText());
                        }
                    }

                    if (dict != null)
                    {
                        notificationNode.TitleInput = new InputVariable
                        {
                            VariableKey = dict.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? "title" : "title",
                            SourceNodeId = dict.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                            SourceOutputKey = dict.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                        };
                    }
                }
                catch { }
            }

            // ContentInput
            if (properties.TryGetValue("ContentInput", out var contentInputObj) && contentInputObj != null)
            {
                try
                {
                    Dictionary<string, object?>? dict = null;

                    if (contentInputObj is string contentJson && !string.IsNullOrWhiteSpace(contentJson))
                    {
                        dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(contentJson);
                    }
                    else if (contentInputObj is JsonElement contentJe)
                    {
                        if (contentJe.ValueKind == JsonValueKind.String)
                        {
                            var jsonStr = contentJe.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonStr))
                                dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonStr);
                        }
                        else if (contentJe.ValueKind == JsonValueKind.Object)
                        {
                            dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(contentJe.GetRawText());
                        }
                    }

                    if (dict != null)
                    {
                        notificationNode.ContentInput = new InputVariable
                        {
                            VariableKey = dict.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? "content" : "content",
                            SourceNodeId = dict.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                            SourceOutputKey = dict.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                        };
                    }
                }
                catch { }
            }

            // DurationInput
            if (properties.TryGetValue("DurationInput", out var durationInputObj) && durationInputObj != null)
            {
                try
                {
                    Dictionary<string, object?>? dict = null;

                    if (durationInputObj is string durationJson && !string.IsNullOrWhiteSpace(durationJson))
                    {
                        dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(durationJson);
                    }
                    else if (durationInputObj is JsonElement durationJe)
                    {
                        if (durationJe.ValueKind == JsonValueKind.String)
                        {
                            var jsonStr = durationJe.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonStr))
                                dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonStr);
                        }
                        else if (durationJe.ValueKind == JsonValueKind.Object)
                        {
                            dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(durationJe.GetRawText());
                        }
                    }

                    if (dict != null)
                    {
                        notificationNode.DurationInput = new InputVariable
                        {
                            VariableKey = dict.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? "duration" : "duration",
                            SourceNodeId = dict.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                            SourceOutputKey = dict.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                        };
                    }
                }
                catch { }
            }

            if (properties.TryGetValue("StaticTitle", out var staticTitleObj))
            {
                notificationNode.StaticTitle = staticTitleObj?.ToString() ?? string.Empty;
            }

            if (properties.TryGetValue("StaticContent", out var staticContentObj))
            {
                notificationNode.StaticContent = staticContentObj?.ToString() ?? string.Empty;
            }

            if (properties.TryGetValue("ToastTitleColorKey", out var toastTitleColorKeyObj))
            {
                notificationNode.ToastTitleColorKey = toastTitleColorKeyObj?.ToString();
            }

            if (properties.TryGetValue("ToastContentColorKey", out var toastContentColorKeyObj))
            {
                notificationNode.ToastContentColorKey = toastContentColorKeyObj?.ToString();
            }

            if (properties.TryGetValue("ToastBackgroundColorKey", out var toastBackgroundColorKeyObj))
            {
                notificationNode.ToastBackgroundColorKey = toastBackgroundColorKeyObj?.ToString();
            }

            if (properties.TryGetValue("ToastBackgroundOpacity", out var toastOpacityObj) &&
                double.TryParse(toastOpacityObj?.ToString(), out var parsedOpacity))
            {
                notificationNode.ToastBackgroundOpacity = parsedOpacity;
            }
    }

    private static void RestoreHttpRequestNodeProperties(HttpRequestNode httpRequestNode, Dictionary<string, object> properties)
    {
            // Deserialize basic properties
            if (properties.TryGetValue("Url", out var urlObj))
                httpRequestNode.Url = urlObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("HttpMethod", out var methodObj))
            {
                if (Enum.TryParse<Models.Nodes.HttpMethod>(methodObj?.ToString(), out var method))
                    httpRequestNode.HttpMethod = method;
            }
            if (properties.TryGetValue("AuthType", out var authTypeObj))
            {
                if (Enum.TryParse<HttpAuthType>(authTypeObj?.ToString(), out var authType))
                    httpRequestNode.AuthType = authType;
            }
            if (properties.TryGetValue("BodyType", out var bodyTypeObj))
            {
                if (Enum.TryParse<HttpBodyType>(bodyTypeObj?.ToString(), out var bodyType))
                    httpRequestNode.BodyType = bodyType;
            }
            if (properties.TryGetValue("TimeoutSeconds", out var timeoutObj) &&
                int.TryParse(timeoutObj?.ToString(), out var timeout))
                httpRequestNode.TimeoutSeconds = timeout;

            // Deserialize URL dynamic binding
            if (properties.TryGetValue("UrlSourceNodeId", out var urlSrcNodeObj))
                httpRequestNode.UrlSourceNodeId = urlSrcNodeObj?.ToString();
            if (properties.TryGetValue("UrlSourceOutputKey", out var urlSrcKeyObj))
                httpRequestNode.UrlSourceOutputKey = urlSrcKeyObj?.ToString();

            // Deserialize Body
            if (properties.TryGetValue("RawBody", out var rawBodyObj))
                httpRequestNode.RawBody = rawBodyObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("BodySourceNodeId", out var bodySrcNodeObj))
                httpRequestNode.BodySourceNodeId = bodySrcNodeObj?.ToString();
            if (properties.TryGetValue("BodySourceOutputKey", out var bodySrcKeyObj))
                httpRequestNode.BodySourceOutputKey = bodySrcKeyObj?.ToString();

            // Deserialize Auth
            if (properties.TryGetValue("AuthUsername", out var authUserObj))
                httpRequestNode.AuthUsername = authUserObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("AuthPassword", out var authPassObj))
                httpRequestNode.AuthPassword = authPassObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("AuthToken", out var authTokenObj))
                httpRequestNode.AuthToken = authTokenObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("TokenSourceNodeId", out var tokenSrcNodeObj))
                httpRequestNode.TokenSourceNodeId = tokenSrcNodeObj?.ToString();
            if (properties.TryGetValue("TokenSourceOutputKey", out var tokenSrcKeyObj))
                httpRequestNode.TokenSourceOutputKey = tokenSrcKeyObj?.ToString();
            if (properties.TryGetValue("ApiKeyName", out var apiKeyNameObj))
                httpRequestNode.ApiKeyName = apiKeyNameObj?.ToString() ?? "X-API-Key";
            if (properties.TryGetValue("ApiKeyValue", out var apiKeyValueObj))
                httpRequestNode.ApiKeyValue = apiKeyValueObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ApiKeyValueSourceNodeId", out var apiKeyValSrcNodeObj))
                httpRequestNode.ApiKeyValueSourceNodeId = apiKeyValSrcNodeObj?.ToString();
            if (properties.TryGetValue("ApiKeyValueSourceOutputKey", out var apiKeyValSrcKeyObj))
                httpRequestNode.ApiKeyValueSourceOutputKey = apiKeyValSrcKeyObj?.ToString();
            if (properties.TryGetValue("ApiKeyInHeader", out var apiKeyInHeaderObj) &&
                bool.TryParse(apiKeyInHeaderObj?.ToString(), out var apiKeyInHeader))
                httpRequestNode.ApiKeyInHeader = apiKeyInHeader;

            // Deserialize Headers
            if (properties.TryGetValue("Headers", out var headersObj))
            {
                var headers = DeserializeHttpKeyValuePairs(headersObj);
                if (headers != null)
                {
                    httpRequestNode.Headers.Clear();
                    foreach (var h in headers)
                        httpRequestNode.Headers.Add(h);
                }
            }

            // Deserialize QueryParams
            if (properties.TryGetValue("QueryParams", out var paramsObj))
            {
                var queryParams = DeserializeHttpKeyValuePairs(paramsObj);
                if (queryParams != null)
                {
                    httpRequestNode.QueryParams.Clear();
                    foreach (var p in queryParams)
                        httpRequestNode.QueryParams.Add(p);
                }
            }

            // Deserialize FormData
            if (properties.TryGetValue("FormData", out var formDataObj))
            {
                var formData = DeserializeHttpKeyValuePairs(formDataObj);
                if (formData != null)
                {
                    httpRequestNode.FormData.Clear();
                    foreach (var f in formData)
                        httpRequestNode.FormData.Add(f);
                }
            }

            // cURL binding (NEW)
            if (properties.TryGetValue("CurlSourceNodeId", out var curlSrcNodeObj))
                httpRequestNode.CurlSourceNodeId = curlSrcNodeObj?.ToString();
            if (properties.TryGetValue("CurlSourceOutputKey", out var curlSrcKeyObj))
                httpRequestNode.CurlSourceOutputKey = curlSrcKeyObj?.ToString();

            // Deserialize Anti-bot / bypass (libcurl)
            if (properties.TryGetValue("UseCurl", out var useCurlObj) &&
                bool.TryParse(useCurlObj?.ToString(), out var useCurl))
            {
                httpRequestNode.UseCurl = useCurl;
            }
            if (properties.TryGetValue("CurlPath", out var curlPathObj))
            {
                httpRequestNode.CurlPath = curlPathObj?.ToString() ?? string.Empty;
            }
            if (properties.TryGetValue("ImpersonateBrowser", out var impObj))
            {
                httpRequestNode.ImpersonateBrowser = impObj?.ToString() ?? string.Empty;
            }
            if (properties.TryGetValue("AutoAppendCurlWriteOut", out var autoWriteOutObj) &&
                bool.TryParse(autoWriteOutObj?.ToString(), out var autoWriteOut))
            {
                httpRequestNode.AutoAppendCurlWriteOut = autoWriteOut;
            }
    }

    // -- GET (Serialize) --

    private static void GetStringSplitNodeProperties(StringSplitNode stringSplit, Dictionary<string, object> dict)
    {
            dict["RegexPattern"] = stringSplit.RegexPattern ?? @"\r?\n";
            dict["OutputKey"] = stringSplit.OutputKey ?? "ListItems";

    }

    private static void GetEmbedApplicationNodeProperties(EmbedApplicationNode embedApp, Dictionary<string, object> dict)
    {
            dict["ProcessName"] = embedApp.ProcessName ?? string.Empty;
            dict["ProcessId"] = embedApp.ProcessId;
            // IntPtr cần convert sang string để serialize JSON
            dict["WindowHandle"] = embedApp.WindowHandle.ToInt64().ToString();
            dict["WindowTitle"] = embedApp.WindowTitle ?? string.Empty;
            dict["EmbeddedWidth"] = embedApp.EmbeddedWidth;
            dict["EmbeddedHeight"] = embedApp.EmbeddedHeight;
            dict["IsActive"] = embedApp.IsActive;
            dict["ShowBorder"] = embedApp.ShowBorder;
            dict["AllowInteraction"] = embedApp.AllowInteraction;
            dict["AutoRefresh"] = embedApp.AutoRefresh;
            dict["RefreshRate"] = embedApp.RefreshRate;
            dict["CaptureMode"] = embedApp.CaptureMode.ToString();
            dict["HasEmbeddedWindow"] = embedApp.HasEmbeddedWindow;
    }

    private static void GetKeyValueBridgeNodeProperties(KeyValueBridgeNode kvNode, Dictionary<string, object> dict)
    {
            dict["mode"] = kvNode.IsPassKeyMode ? "pass" : "get";
            dict["IsPassKeyMode"] = kvNode.IsPassKeyMode;
            dict["key"] = kvNode.KvChannelKey ?? string.Empty;
            dict["KvChannelKey"] = kvNode.KvChannelKey ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(kvNode.SelectedSourceBridgeNodeId))
                dict["selectedSourceNodeId"] = kvNode.SelectedSourceBridgeNodeId;
            dict["SelectedSourceBridgeNodeId"] = kvNode.SelectedSourceBridgeNodeId ?? string.Empty;
            dict["interval"] = kvNode.PollIntervalValue;
            dict["PollIntervalValue"] = kvNode.PollIntervalValue;
            dict["intervalUnit"] = kvNode.PollIntervalUnit.ToString();
            dict["PollIntervalUnit"] = kvNode.PollIntervalUnit.ToString();

            dict["EnableDataCleanup"] = kvNode.EnableDataCleanup;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupTargetBridgeNodeId))
                dict["CleanupTargetBridgeNodeId"] = kvNode.CleanupTargetBridgeNodeId;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupTargetKey))
                dict["CleanupTargetKey"] = kvNode.CleanupTargetKey;
            dict["CleanupClearAllNodeData"] = kvNode.CleanupClearAllNodeData;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupArrayFilterField))
                dict["CleanupArrayFilterField"] = kvNode.CleanupArrayFilterField;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupArrayFilterValue))
                dict["CleanupArrayFilterValue"] = kvNode.CleanupArrayFilterValue;
            dict["CleanupRemoveAllMatchedArrayItems"] = kvNode.CleanupRemoveAllMatchedArrayItems;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupTriggerSourceNodeId))
                dict["CleanupTriggerSourceNodeId"] = kvNode.CleanupTriggerSourceNodeId;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupTriggerSourceOutputKey))
                dict["CleanupTriggerSourceOutputKey"] = kvNode.CleanupTriggerSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupTriggerExpectedValue))
                dict["CleanupTriggerExpectedValue"] = kvNode.CleanupTriggerExpectedValue;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupKeySourceNodeId))
                dict["CleanupKeySourceNodeId"] = kvNode.CleanupKeySourceNodeId;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupKeySourceOutputKey))
                dict["CleanupKeySourceOutputKey"] = kvNode.CleanupKeySourceOutputKey;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupFilterFieldSourceNodeId))
                dict["CleanupFilterFieldSourceNodeId"] = kvNode.CleanupFilterFieldSourceNodeId;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupFilterFieldSourceOutputKey))
                dict["CleanupFilterFieldSourceOutputKey"] = kvNode.CleanupFilterFieldSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupFilterValueSourceNodeId))
                dict["CleanupFilterValueSourceNodeId"] = kvNode.CleanupFilterValueSourceNodeId;
            if (!string.IsNullOrWhiteSpace(kvNode.CleanupFilterValueSourceOutputKey))
                dict["CleanupFilterValueSourceOutputKey"] = kvNode.CleanupFilterValueSourceOutputKey;
            if (kvNode.AdditionalAppendSources != null && kvNode.AdditionalAppendSources.Count > 0)
            {
                var appendSourcesJson = JsonSerializer.Serialize(kvNode.AdditionalAppendSources
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                    .Select(x => new
                    {
                        SourceNodeId = x.SourceNodeId.Trim(),
                        SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                    })
                    .ToList());
                dict["AdditionalAppendSources"] = appendSourcesJson;
            }
    }

    private static void GetGitSourceNodeProperties(GitSourceNode gitSourceNode, Dictionary<string, object> dict)
    {
            dict["RepoUrl"] = gitSourceNode.RepoUrl;
            dict["LocalPath"] = gitSourceNode.LocalPath;
            dict["Branch"] = gitSourceNode.Branch;
            dict["DisplayName"] = gitSourceNode.DisplayName;
            dict["IconKey"] = gitSourceNode.IconKey;
            dict["IconColorKey"] = gitSourceNode.IconColorKey;
            dict["TooltipText"] = gitSourceNode.TooltipText;
            dict["ContextMenuDescription"] = gitSourceNode.ContextMenuDescription;
            dict["VscodiumPath"] = gitSourceNode.VscodiumPath;
            dict["LastCommitHash"] = gitSourceNode.LastCommitHash;
            dict["LastPullTime"] = gitSourceNode.LastPullTime;
            dict["AutoOpenOnExecute"] = gitSourceNode.AutoOpenOnExecute;
            dict["CommandText"] = gitSourceNode.CommandText;
    }

    private static void GetBodyContainerNodeProperties(BodyContainerNode bodyContainerNode, Dictionary<string, object> dict)
    {
            dict["BodyWidth"] = bodyContainerNode.BodyWidth;
            dict["BodyHeight"] = bodyContainerNode.BodyHeight;
            dict["BodyBackgroundColorHex"] = bodyContainerNode.BodyBackgroundColorHex;
            dict["BodyBorderColorHex"] = bodyContainerNode.BodyBorderColorHex;
            dict["UseUnifiedColors"] = bodyContainerNode.UseUnifiedColors;
            dict["BackgroundOpacityPercent"] = bodyContainerNode.BackgroundOpacityPercent;
            dict["LockInnerNodes"] = bodyContainerNode.LockInnerNodes;
            dict["BorderOpacityPercent"] = bodyContainerNode.BorderOpacityPercent;
            dict["BorderThickness"] = bodyContainerNode.BorderThickness;
            dict["BorderDashSpacing"] = bodyContainerNode.BorderDashSpacing;
            dict["BorderDashStyle"] = bodyContainerNode.BorderDashStyle.ToString();
            dict["IconOpacityPercent"] = bodyContainerNode.IconOpacityPercent;
            dict["LockCanvasSize"] = bodyContainerNode.LockCanvasSize;
            dict["LockedZoomLevel"] = bodyContainerNode.LockedZoomLevel;
            dict["LockedX"] = bodyContainerNode.LockedX;
            dict["LockedY"] = bodyContainerNode.LockedY;

    }

    private static void GetOutputNodeProperties(OutputNode outputNode, Dictionary<string, object> dict)
    {
            // Serialize OutputKey
            if (!string.IsNullOrWhiteSpace(outputNode.OutputKey))
                dict["OutputKey"] = outputNode.OutputKey;

            // Serialize FormatString
            if (!string.IsNullOrWhiteSpace(outputNode.FormatString))
                dict["FormatString"] = outputNode.FormatString;

            // Serialize SaveToClipboard
            dict["SaveToClipboard"] = outputNode.SaveToClipboard.ToString();

            // Serialize InputVariables
            if (outputNode.InputVariables != null && outputNode.InputVariables.Count > 0)
            {
                var variablesJson = JsonSerializer.Serialize(outputNode.InputVariables.Select(v => new
                {
                    VariableKey = v.VariableKey,
                    SourceNodeId = v.SourceNodeId,
                    SourceOutputKey = v.SourceOutputKey
                }).ToList());
                dict["InputVariables"] = variablesJson;
            }


    }

    private static void GetMacroRecorderNodeProperties(MacroRecorderNode macroNode, Dictionary<string, object> dict)
    {
            dict["OutputKey"] = macroNode.OutputKey ?? "macroData";
            dict["MacroDataJson"] = macroNode.MacroDataJson ?? "";
            dict["PlaybackMode"] = macroNode.PlaybackMode.ToString();
            dict["RepeatIntervalMs"] = macroNode.RepeatIntervalMs;
            dict["RepeatCount"] = macroNode.RepeatCount;
            dict["VisualPlaybackMode"] = macroNode.VisualPlaybackMode.ToString();
            dict["ShowMouseTrail"] = macroNode.ShowMouseTrail;
            dict["CountdownSeconds"] = macroNode.CountdownSeconds;
            dict["StayOnTargetAfterExecution"] = macroNode.StayOnTargetAfterExecution;
            dict["ExecutionMode"] = macroNode.ExecutionMode.ToString();
            if (!string.IsNullOrEmpty(macroNode.TargetProcessName))
                dict["TargetProcessName"] = macroNode.TargetProcessName;
            if (!string.IsNullOrEmpty(macroNode.TargetWindowTitle))
                dict["TargetWindowTitle"] = macroNode.TargetWindowTitle;
    }

    private static void GetBorderHighlightNodeProperties(BorderHighlightNode borderHighlightNode, Dictionary<string, object> dict)
    {
            dict["BorderColorHex"] = borderHighlightNode.BorderColorHex ?? "#00D2FF";
            dict["BorderThickness"] = borderHighlightNode.BorderThickness;
            dict["GradientSize"] = borderHighlightNode.GradientSize;
            dict["Opacity"] = borderHighlightNode.Opacity;
            dict["EffectType"] = borderHighlightNode.EffectType.ToString();
            dict["HighlightMode"] = borderHighlightNode.HighlightMode.ToString();
            if (!string.IsNullOrEmpty(borderHighlightNode.TargetProcessName))
                dict["TargetProcessName"] = borderHighlightNode.TargetProcessName;
            if (!string.IsNullOrEmpty(borderHighlightNode.TargetWindowTitle))
                dict["TargetWindowTitle"] = borderHighlightNode.TargetWindowTitle;
            dict["DurationMs"] = borderHighlightNode.DurationMs;
            dict["DurationUnit"] = borderHighlightNode.DurationUnit.ToString();
            dict["WaitForCompletion"] = borderHighlightNode.WaitForCompletion;
            if (!string.IsNullOrEmpty(borderHighlightNode.SelectedWindowJson))
                dict["SelectedWindowJson"] = borderHighlightNode.SelectedWindowJson;
            dict["NodesToDisableJson"] = borderHighlightNode.NodesToDisableJson ?? "[]";
            if (borderHighlightNode.TargetProcessId != 0)
                dict["TargetProcessId"] = borderHighlightNode.TargetProcessId;
    }

    private static void GetNotificationNodeProperties(NotificationNode notificationNode, Dictionary<string, object> dict)
    {

            dict["DefaultDurationSeconds"] = notificationNode.DefaultDurationSeconds;

            // Title properties — required for all nodes
            dict["TitleDisplayMode"] = notificationNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = notificationNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(notificationNode.TitleColorKey))
                dict["TitleColorKey"] = notificationNode.TitleColorKey;

            if (notificationNode.TitleInput != null)
            {
                var json = JsonSerializer.Serialize(new
                {
                    VariableKey = notificationNode.TitleInput.VariableKey,
                    SourceNodeId = notificationNode.TitleInput.SourceNodeId,
                    SourceOutputKey = notificationNode.TitleInput.SourceOutputKey
                });
                dict["TitleInput"] = json;
            }

            if (notificationNode.ContentInput != null)
            {
                var json = JsonSerializer.Serialize(new
                {
                    VariableKey = notificationNode.ContentInput.VariableKey,
                    SourceNodeId = notificationNode.ContentInput.SourceNodeId,
                    SourceOutputKey = notificationNode.ContentInput.SourceOutputKey
                });
                dict["ContentInput"] = json;
            }

            if (notificationNode.DurationInput != null)
            {
                var json = JsonSerializer.Serialize(new
                {
                    VariableKey = notificationNode.DurationInput.VariableKey,
                    SourceNodeId = notificationNode.DurationInput.SourceNodeId,
                    SourceOutputKey = notificationNode.DurationInput.SourceOutputKey
                });
                dict["DurationInput"] = json;
            }

            if (!string.IsNullOrWhiteSpace(notificationNode.StaticTitle))
                dict["StaticTitle"] = notificationNode.StaticTitle;

            if (!string.IsNullOrWhiteSpace(notificationNode.StaticContent))
                dict["StaticContent"] = notificationNode.StaticContent;

            if (!string.IsNullOrWhiteSpace(notificationNode.ToastTitleColorKey))
                dict["ToastTitleColorKey"] = notificationNode.ToastTitleColorKey;

            if (!string.IsNullOrWhiteSpace(notificationNode.ToastContentColorKey))
                dict["ToastContentColorKey"] = notificationNode.ToastContentColorKey;

            if (!string.IsNullOrWhiteSpace(notificationNode.ToastBackgroundColorKey))
                dict["ToastBackgroundColorKey"] = notificationNode.ToastBackgroundColorKey;

            dict["ToastBackgroundOpacity"] = notificationNode.ToastBackgroundOpacity;
    }

    private static void GetHttpRequestNodeProperties(HttpRequestNode httpRequestNode, Dictionary<string, object> dict)
    {
            // Serialize basic properties

            dict["Url"] = httpRequestNode.Url ?? string.Empty;
            dict["HttpMethod"] = httpRequestNode.HttpMethod.ToString();
            dict["AuthType"] = httpRequestNode.AuthType.ToString();
            dict["BodyType"] = httpRequestNode.BodyType.ToString();
            dict["TimeoutSeconds"] = httpRequestNode.TimeoutSeconds;

            // Serialize URL dynamic binding
            if (!string.IsNullOrWhiteSpace(httpRequestNode.UrlSourceNodeId))
                dict["UrlSourceNodeId"] = httpRequestNode.UrlSourceNodeId;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.UrlSourceOutputKey))
                dict["UrlSourceOutputKey"] = httpRequestNode.UrlSourceOutputKey;

            // Serialize Body
            if (!string.IsNullOrWhiteSpace(httpRequestNode.RawBody))
                dict["RawBody"] = httpRequestNode.RawBody;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.BodySourceNodeId))
                dict["BodySourceNodeId"] = httpRequestNode.BodySourceNodeId;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.BodySourceOutputKey))
                dict["BodySourceOutputKey"] = httpRequestNode.BodySourceOutputKey;

            // Serialize Auth
            if (!string.IsNullOrWhiteSpace(httpRequestNode.AuthUsername))
                dict["AuthUsername"] = httpRequestNode.AuthUsername;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.AuthPassword))
                dict["AuthPassword"] = httpRequestNode.AuthPassword;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.AuthToken))
                dict["AuthToken"] = httpRequestNode.AuthToken;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.TokenSourceNodeId))
                dict["TokenSourceNodeId"] = httpRequestNode.TokenSourceNodeId;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.TokenSourceOutputKey))
                dict["TokenSourceOutputKey"] = httpRequestNode.TokenSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ApiKeyName))
                dict["ApiKeyName"] = httpRequestNode.ApiKeyName;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ApiKeyValue))
                dict["ApiKeyValue"] = httpRequestNode.ApiKeyValue;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ApiKeyValueSourceNodeId))
                dict["ApiKeyValueSourceNodeId"] = httpRequestNode.ApiKeyValueSourceNodeId;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ApiKeyValueSourceOutputKey))
                dict["ApiKeyValueSourceOutputKey"] = httpRequestNode.ApiKeyValueSourceOutputKey;
            dict["ApiKeyInHeader"] = httpRequestNode.ApiKeyInHeader;

            // Serialize Headers
            if (httpRequestNode.Headers != null && httpRequestNode.Headers.Count > 0)
            {
                var headersJson = JsonSerializer.Serialize(httpRequestNode.Headers.Select(h => new
                {
                    Key = h.Key,
                    Value = h.Value,
                    IsEnabled = h.IsEnabled,
                    SourceNodeId = h.SourceNodeId,
                    SourceOutputKey = h.SourceOutputKey
                }).ToList());
                dict["Headers"] = headersJson;
            }

            // Serialize QueryParams
            if (httpRequestNode.QueryParams != null && httpRequestNode.QueryParams.Count > 0)
            {
                var paramsJson = JsonSerializer.Serialize(httpRequestNode.QueryParams.Select(p => new
                {
                    Key = p.Key,
                    Value = p.Value,
                    IsEnabled = p.IsEnabled,
                    SourceNodeId = p.SourceNodeId,
                    SourceOutputKey = p.SourceOutputKey
                }).ToList());
                dict["QueryParams"] = paramsJson;
            }

            // Serialize FormData
            if (httpRequestNode.FormData != null && httpRequestNode.FormData.Count > 0)
            {
                var formDataJson = JsonSerializer.Serialize(httpRequestNode.FormData.Select(f => new
                {
                    Key = f.Key,
                    Value = f.Value,
                    IsEnabled = f.IsEnabled,
                    SourceNodeId = f.SourceNodeId,
                    SourceOutputKey = f.SourceOutputKey
                }).ToList());
                dict["FormData"] = formDataJson;
            }

            // cURL binding (NEW)
            if (!string.IsNullOrEmpty(httpRequestNode.CurlSourceNodeId))
                dict["CurlSourceNodeId"] = httpRequestNode.CurlSourceNodeId;
            if (!string.IsNullOrEmpty(httpRequestNode.CurlSourceOutputKey))
                dict["CurlSourceOutputKey"] = httpRequestNode.CurlSourceOutputKey;

            // Serialize Anti-bot / bypass (libcurl)
            dict["UseCurl"] = httpRequestNode.UseCurl;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.CurlPath))
                dict["CurlPath"] = httpRequestNode.CurlPath;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ImpersonateBrowser))
                dict["ImpersonateBrowser"] = httpRequestNode.ImpersonateBrowser;
            dict["AutoAppendCurlWriteOut"] = httpRequestNode.AutoAppendCurlWriteOut;
    }

}
