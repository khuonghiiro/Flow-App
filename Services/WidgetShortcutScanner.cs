using FlowMy.Models;
using FlowMy.Services.Workflow;
using FlowMy.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FlowMy.Services
{
    /// <summary>
    /// Quét nhanh thư mục workflows JSON để lấy danh sách các floating widget đã cấu hình
    /// (IsEnabled = true). Dùng cho MainWindow launcher — không load toàn bộ workflow.
    /// </summary>
    public static class WidgetShortcutScanner
    {
        public sealed class ScanResult
        {
            public List<WidgetShortcutItem> Items { get; } = new();
            /// <summary>Số widget đã cấu hình (IsEnabled=true).</summary>
            public int EnabledCount => Items.Count(i => i.IsEnabled);
            public int TotalCount => Items.Count;
        }

        public static ScanResult Scan()
        {
            var result = new ScanResult();
            string dir;
            try { dir = FileWorkflowPersistenceService.GetDefaultWorkflowsDirectory(); }
            catch { return result; }

            if (!Directory.Exists(dir)) return result;

            string[] files;
            try { files = Directory.GetFiles(dir, "*.json"); }
            catch { return result; }

            foreach (var path in files)
            {
                try
                {
                    var workflowName = Path.GetFileNameWithoutExtension(path);
                    var json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("Nodes", out var nodesEl) ||
                        nodesEl.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var nodeEl in nodesEl.EnumerateArray())
                    {
                        if (!nodeEl.TryGetProperty("Properties", out var propsEl) ||
                            propsEl.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        if (!propsEl.TryGetProperty("FloatingWidget", out var widgetEl))
                            continue;

                        var widgetJson = widgetEl.ValueKind == JsonValueKind.String
                            ? widgetEl.GetString()
                            : widgetEl.GetRawText();
                        if (string.IsNullOrWhiteSpace(widgetJson)) continue;

                        FloatingWidgetConfig? cfg = null;
                        try { cfg = JsonSerializer.Deserialize<FloatingWidgetConfig>(widgetJson); }
                        catch { continue; }
                        if (cfg == null || !cfg.IsEnabled) continue;

                        string nodeId = nodeEl.TryGetProperty("Id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
                        string nodeTitle = nodeEl.TryGetProperty("Title", out var tEl) ? tEl.GetString() ?? string.Empty : string.Empty;

                        result.Items.Add(new WidgetShortcutItem
                        {
                            WorkflowName = workflowName,
                            NodeId = nodeId,
                            NodeTitle = nodeTitle,
                            WidgetName = cfg.WidgetName,
                            IconKey = cfg.IdleIconText ?? string.Empty,
                            IsEnabled = true
                        });
                    }
                }
                catch
                {
                    // Bỏ qua file lỗi
                }
            }

            return result;
        }
    }
}
