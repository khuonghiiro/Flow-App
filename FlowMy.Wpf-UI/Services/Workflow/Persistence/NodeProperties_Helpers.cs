using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    private static string? GetStringFromJsonValue(object? v)
    {
        if (v == null) return null;
        try
        {
            if (v is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    return je.GetString();
                }
                else if (je.ValueKind == JsonValueKind.Null || je.ValueKind == JsonValueKind.Undefined)
                {
                    return null;
                }
                else
                {
                    return je.ToString();
                }
            }
            return v.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting string from JSON value: {ex.Message}");
            return null;
        }
    }

    private static string? TryEncodeBitmapSourceToPngBase64(BitmapSource? source)
    {
        if (source == null) return null;
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? TryDecodePngBase64ToBitmapImage(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            ms.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Deserialize HttpKeyValuePair list from JSON string or JsonElement.
    /// </summary>
    private static List<HttpKeyValuePair>? DeserializeHttpKeyValuePairs(object? obj)
    {
        if (obj == null) return null;

        try
        {
            string? jsonString = null;

            if (obj is string str)
            {
                jsonString = str;
            }
            else if (obj is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    jsonString = jsonElement.GetString();
                }
                else if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    jsonString = jsonElement.GetRawText();
                }
            }

            if (string.IsNullOrWhiteSpace(jsonString))
                return null;

            var data = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonString);
            if (data == null) return null;

            return data.Select(d => new HttpKeyValuePair
            {
                Key = d.TryGetValue("Key", out var k) ? k?.ToString() ?? string.Empty : string.Empty,
                Value = d.TryGetValue("Value", out var v) ? v?.ToString() ?? string.Empty : string.Empty,
                IsEnabled = d.TryGetValue("IsEnabled", out var ie) && bool.TryParse(ie?.ToString(), out var enabled) ? enabled : true,
                SourceNodeId = d.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() : null,
                SourceOutputKey = d.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() : null
            }).ToList();
        }
        catch
        {
            return null;
        }
    }
}
