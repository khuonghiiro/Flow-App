using FlowMy.Models.Nodes;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FlowMy.Services.Workflow
{
    public sealed partial class FileWorkflowPersistenceService
    {
        private static void RestoreVideoEditorNodeProperties(VideoEditorNode node, Dictionary<string, object> properties)
        {
            if (properties.TryGetValue("DisplayMode", out var dm) && dm != null)
            {
                if (Enum.TryParse<VideoEditorDisplayMode>(dm.ToString(), out var mode))
                    node.DisplayMode = mode;
                else if (int.TryParse(dm.ToString(), out var modeInt))
                    node.DisplayMode = (VideoEditorDisplayMode)modeInt;
            }

            if (properties.TryGetValue("SourceNodeId", out var sid)) node.SourceNodeId = sid?.ToString() ?? string.Empty;
            if (properties.TryGetValue("SourceOutputKey", out var sok)) node.SourceOutputKey = sok?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CustomKey", out var ck)) node.CustomKey = ck?.ToString() ?? string.Empty;
            if (properties.TryGetValue("InputVideoUrl", out var ivu)) node.InputVideoUrl = ivu?.ToString() ?? string.Empty;

            if (properties.TryGetValue("TrimEnabled", out var te) && te != null && bool.TryParse(te.ToString(), out var bTe)) node.TrimEnabled = bTe;
            if (properties.TryGetValue("TrimStartTime", out var tst)) node.TrimStartTime = tst?.ToString() ?? "00:00:00.000";
            if (properties.TryGetValue("TrimEndTime", out var tet)) node.TrimEndTime = tet?.ToString() ?? "00:00:10.000";

            if (properties.TryGetValue("Brightness", out var br) && br != null && double.TryParse(br.ToString(), out var dBr)) node.Brightness = dBr;
            if (properties.TryGetValue("Contrast", out var co) && co != null && double.TryParse(co.ToString(), out var dCo)) node.Contrast = dCo;
            if (properties.TryGetValue("Saturation", out var sa) && sa != null && double.TryParse(sa.ToString(), out var dSa)) node.Saturation = dSa;
            if (properties.TryGetValue("Gamma", out var ga) && ga != null && double.TryParse(ga.ToString(), out var dGa)) node.Gamma = dGa;
            if (properties.TryGetValue("Hue", out var hu) && hu != null && double.TryParse(hu.ToString(), out var dHu)) node.Hue = dHu;
            if (properties.TryGetValue("FilterPreset", out var fp)) node.FilterPreset = fp?.ToString() ?? "None";

            if (properties.TryGetValue("ScaleEnabled", out var se) && se != null && bool.TryParse(se.ToString(), out var bSe)) node.ScaleEnabled = bSe;
            if (properties.TryGetValue("TargetWidth", out var tw) && tw != null && int.TryParse(tw.ToString(), out var iTw)) node.TargetWidth = iTw;
            if (properties.TryGetValue("TargetHeight", out var th) && th != null && int.TryParse(th.ToString(), out var iTh)) node.TargetHeight = iTh;
            if (properties.TryGetValue("Speed", out var sp) && sp != null && double.TryParse(sp.ToString(), out var dSp)) node.Speed = dSp;
            if (properties.TryGetValue("RotateFlip", out var rf)) node.RotateFlip = rf?.ToString() ?? "None";

            if (properties.TryGetValue("WatermarkEnabled", out var we) && we != null && bool.TryParse(we.ToString(), out var bWe)) node.WatermarkEnabled = bWe;
            if (properties.TryGetValue("WatermarkText", out var wt)) node.WatermarkText = wt?.ToString() ?? string.Empty;
            if (properties.TryGetValue("WatermarkImagePath", out var wip)) node.WatermarkImagePath = wip?.ToString() ?? string.Empty;
            if (properties.TryGetValue("WatermarkPosition", out var wp)) node.WatermarkPosition = wp?.ToString() ?? "BottomRight";

            if (properties.TryGetValue("AudioMode", out var am)) node.AudioMode = am?.ToString() ?? "Keep";
            if (properties.TryGetValue("AudioVolume", out var av) && av != null && double.TryParse(av.ToString(), out var dAv)) node.AudioVolume = dAv;

            if (properties.TryGetValue("ExportMode", out var em)) node.ExportMode = em?.ToString() ?? "Video";
            if (properties.TryGetValue("ExportFps", out var ef) && ef != null && double.TryParse(ef.ToString(), out var dEf)) node.ExportFps = dEf;
            if (properties.TryGetValue("ExportFormat", out var efm)) node.ExportFormat = efm?.ToString() ?? "mp4";
            if (properties.TryGetValue("OutputFolderPath", out var ofp)) node.OutputFolderPath = ofp?.ToString() ?? string.Empty;

            if (properties.TryGetValue("OutputKeys", out var okObj) && okObj != null)
            {
                try
                {
                    string? json = okObj is string s ? s
                        : okObj is JsonElement je && je.ValueKind == JsonValueKind.String ? je.GetString()
                        : okObj is JsonElement je2 && je2.ValueKind == JsonValueKind.Array ? je2.GetRawText()
                        : null;
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var keys = JsonSerializer.Deserialize<List<string>>(json);
                        if (keys?.Count > 0) { node.OutputKeys = keys; }
                    }
                }
                catch { }
            }
        }

        private static void GetVideoEditorNodeProperties(VideoEditorNode node, Dictionary<string, object> dict)
        {
            dict["DisplayMode"] = (int)node.DisplayMode;
            dict["SourceNodeId"] = node.SourceNodeId;
            dict["SourceOutputKey"] = node.SourceOutputKey;
            dict["CustomKey"] = node.CustomKey;
            dict["InputVideoUrl"] = node.InputVideoUrl;

            dict["TrimEnabled"] = node.TrimEnabled;
            dict["TrimStartTime"] = node.TrimStartTime;
            dict["TrimEndTime"] = node.TrimEndTime;

            dict["Brightness"] = node.Brightness;
            dict["Contrast"] = node.Contrast;
            dict["Saturation"] = node.Saturation;
            dict["Gamma"] = node.Gamma;
            dict["Hue"] = node.Hue;
            dict["FilterPreset"] = node.FilterPreset;

            dict["ScaleEnabled"] = node.ScaleEnabled;
            dict["TargetWidth"] = node.TargetWidth;
            dict["TargetHeight"] = node.TargetHeight;
            dict["Speed"] = node.Speed;
            dict["RotateFlip"] = node.RotateFlip;

            dict["WatermarkEnabled"] = node.WatermarkEnabled;
            dict["WatermarkText"] = node.WatermarkText;
            dict["WatermarkImagePath"] = node.WatermarkImagePath;
            dict["WatermarkPosition"] = node.WatermarkPosition;

            dict["AudioMode"] = node.AudioMode;
            dict["AudioVolume"] = node.AudioVolume;

            dict["ExportMode"] = node.ExportMode;
            dict["ExportFps"] = node.ExportFps;
            dict["ExportFormat"] = node.ExportFormat;
            dict["OutputFolderPath"] = node.OutputFolderPath;

            if (node.OutputKeys?.Count > 0)
                dict["OutputKeys"] = JsonSerializer.Serialize(node.OutputKeys);
        }
    }
}
