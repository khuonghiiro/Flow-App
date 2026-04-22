using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FlowMy.ViewModels;

/// <summary>
/// Helper VM cho FloatingWidgetWindow: tách logic build HTML/resolve input/parse params.
/// </summary>
public sealed class FloatingWidgetWindowViewModel
{
    private readonly WorkflowNode _node;
    private readonly IWorkflowEditorHost _host;

    public FloatingWidgetWindowViewModel(WorkflowNode node, IWorkflowEditorHost host)
    {
        _node = node;
        _host = host;
    }

    public string BuildContentSignature(HtmlUiNode node)
    {
        var vars = ResolveInputValues(node);
        var offlineSig = BuildOfflineAssetsSignature(node);
        return string.Join("|",
            node.HtmlCode ?? string.Empty,
            node.CssCode ?? string.Empty,
            node.JsCode ?? string.Empty,
            node.ParamsCode ?? string.Empty,
            offlineSig,
            string.Join(";", vars.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")));
    }

    public string BuildHtmlForWidget(HtmlUiNode htmlNode, string bridgeJs)
    {
        var vars = ResolveInputValues(htmlNode);
        var html = ReplaceVariables(htmlNode.HtmlCode ?? string.Empty, vars);
        var css = ReplaceVariables(htmlNode.CssCode ?? string.Empty, vars);
        var js = ReplaceVariables(htmlNode.JsCode ?? string.Empty, vars);

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        if (!string.IsNullOrWhiteSpace(css))
        {
            sb.AppendLine("  <style>");
            sb.AppendLine(css);
            sb.AppendLine("  </style>");
        }

        foreach (var asset in htmlNode.OfflineAssets ?? new List<HtmlOfflineAsset>())
        {
            if (asset == null || !asset.IsEnabled) continue;
            if (!string.Equals(asset.AssetType, "css", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrWhiteSpace(asset.LocalFileName)) continue;

            try
            {
                var path = HtmlOfflineAssetService.GetLocalFilePath(asset.LocalFileName);
                if (!File.Exists(path)) continue;
                var content = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(content)) continue;
                sb.AppendLine("  <style>");
                sb.AppendLine(content);
                sb.AppendLine("  </style>");
            }
            catch
            {
                // best-effort
            }
        }

        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(html);
        sb.AppendLine("  <script>");
        sb.AppendLine(bridgeJs ?? string.Empty);
        sb.AppendLine("  </script>");

        foreach (var asset in htmlNode.OfflineAssets ?? new List<HtmlOfflineAsset>())
        {
            if (asset == null || !asset.IsEnabled) continue;
            if (!string.Equals(asset.AssetType, "js", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrWhiteSpace(asset.LocalFileName)) continue;

            try
            {
                var path = HtmlOfflineAssetService.GetLocalFilePath(asset.LocalFileName);
                if (!File.Exists(path)) continue;
                var content = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(content)) continue;
                sb.AppendLine("  <script>");
                sb.AppendLine(content);
                sb.AppendLine("  </script>");
            }
            catch
            {
                // best-effort
            }
        }

        if (!string.IsNullOrWhiteSpace(js))
        {
            sb.AppendLine("  <script>");
            sb.AppendLine(js);
            sb.AppendLine("  </script>");
        }
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    public Dictionary<string, string> ResolveInputValues(HtmlUiNode htmlNode)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (htmlNode.InputMappings == null || htmlNode.InputMappings.Count == 0) return vars;
        var vm = _host.ViewModel;
        if (vm == null) return vars;

        foreach (var map in htmlNode.InputMappings)
        {
            if (map == null) continue;
            var key = map.EffectiveInputKey;
            if (string.IsNullOrWhiteSpace(key)) continue;
            string value = string.Empty;

            if (!string.IsNullOrWhiteSpace(map.SourceNodeId))
            {
                var src = vm.Nodes?.FirstOrDefault(n => n.Id == map.SourceNodeId);
                if (src != null)
                {
                    var outKey = string.IsNullOrWhiteSpace(map.SourceOutputKey)
                        ? (src.DynamicOutputs != null && src.DynamicOutputs.Count > 0 ? src.DynamicOutputs[0].Key : "output")
                        : map.SourceOutputKey;
                    value = NodeDataPanelService.ResolveDynamicValueByKey(src, outKey ?? "output");
                    if (string.Equals(value?.Trim(), "—", StringComparison.OrdinalIgnoreCase))
                        value = string.Empty;
                }
            }
            vars[key] = value;
        }
        return vars;
    }

    public List<(string Key, string Selector)> ParseParams(HtmlUiNode htmlNode)
    {
        var result = new List<(string Key, string Selector)>();
        var text = htmlNode.ParamsCode ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            var idx = line.IndexOf(':');
            if (idx <= 0 || idx >= line.Length - 1) continue;
            var key = line[..idx].Trim();
            var selector = line[(idx + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(selector)) continue;
            result.Add((key, selector));
        }
        return result;
    }

    public string ReplaceVariables(string text, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(text) || vars.Count == 0) return text;
        var output = text;
        foreach (var kv in vars)
            output = output.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return output;
    }

    private static string BuildOfflineAssetsSignature(HtmlUiNode node)
    {
        if (node.OfflineAssets == null || node.OfflineAssets.Count == 0) return string.Empty;
        return string.Join(";", node.OfflineAssets
            .Where(a => a != null)
            .Select(a => $"{a.IsEnabled}:{a.AssetType}:{a.LocalFileName}:{a.SourceUrl}"));
    }
}
