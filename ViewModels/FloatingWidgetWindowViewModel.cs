using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utils;
using System.IO;
using System.Text;

namespace FlowMy.ViewModels;

public sealed class FloatingWidgetWindowViewModel
{
    private readonly WorkflowNode _node;
    private readonly IWorkflowEditorHost _host;

    public FloatingWidgetWindowViewModel(WorkflowNode node, IWorkflowEditorHost host)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public string BuildContentSignature(HtmlUiNode node)
    {
        var inputs = ResolveInputValues(node);
        var sb = new StringBuilder();
        sb.Append(node.HtmlCode ?? string.Empty).Append('|')
          .Append(node.CssCode ?? string.Empty).Append('|')
          .Append(node.JsCode ?? string.Empty).Append('|')
          .Append(node.ParamsCode ?? string.Empty).Append('|');

        foreach (var kv in inputs.OrderBy(k => k.Key, StringComparer.Ordinal))
            sb.Append(kv.Key).Append('=').Append(kv.Value ?? string.Empty).Append(';');

        // Include offline assets state so widget reloads when tab changes.
        var offlineKey = string.Join("|", (node.OfflineAssets ?? new List<FlowMy.Models.HtmlOfflineAsset>())
            .Select(a =>
            {
                var fn = a.LocalFileName ?? string.Empty;
                long tick = 0;
                try
                {
                    var path = HtmlOfflineAssetService.GetLocalFilePath(fn);
                    if (File.Exists(path)) tick = File.GetLastWriteTimeUtc(path).Ticks;
                }
                catch { }
                return $"{fn}:{a.AssetType}:{a.IsEnabled}:{tick}";
            }));
        sb.Append('|').Append(offlineKey);

        return sb.ToString();
    }

    public string BuildHtmlForWidget(HtmlUiNode htmlNode, string bridgeJs)
    {
        var html = htmlNode.HtmlCode ?? "<!DOCTYPE html><html><body><div>Widget</div></body></html>";
        var css = htmlNode.CssCode ?? string.Empty;
        var js = htmlNode.JsCode ?? string.Empty;

        var inputValues = ResolveInputValues(htmlNode);
        html = ReplaceVariables(html, inputValues);
        css = ReplaceVariables(css, inputValues);
        js = ReplaceVariables(js, inputValues);

        if (!html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
            html = html.Replace("<html>", "<html>\n<head>\n<meta charset=\"UTF-8\">\n</head>", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(css))
        {
            var cssTag = $"\n<style>\n{css}\n</style>";
            if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</head>", cssTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
            else
                html = cssTag + html;
        }

        // Inject offline assets from Html UI dialog Tab 2.
        var enabledAssets = (htmlNode.OfflineAssets ?? new List<FlowMy.Models.HtmlOfflineAsset>())
            .Where(a => a.IsEnabled && !string.IsNullOrWhiteSpace(a.LocalFileName))
            .ToList();

        foreach (var asset in enabledAssets.Where(a => string.Equals(a.AssetType, "css", StringComparison.OrdinalIgnoreCase)))
        {
            var content = HtmlOfflineAssetService.GetInlineContent(asset.LocalFileName);
            if (string.IsNullOrWhiteSpace(content)) continue;
            var safeName = System.Security.SecurityElement.Escape(asset.Title ?? asset.LocalFileName);
            var cssTag = $"\n<style>/* [offline] {safeName} */\n{content}\n</style>";
            if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</head>", cssTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
            else
                html = cssTag + html;
        }

        if (!string.IsNullOrWhiteSpace(bridgeJs))
        {
            if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</head>", bridgeJs + "\n</head>", StringComparison.OrdinalIgnoreCase);
            else
                html = bridgeJs + html;
        }

        foreach (var asset in enabledAssets.Where(a => !string.Equals(a.AssetType, "css", StringComparison.OrdinalIgnoreCase)))
        {
            var content = HtmlOfflineAssetService.GetInlineContent(asset.LocalFileName);
            if (string.IsNullOrWhiteSpace(content)) continue;
            var safeName = System.Security.SecurityElement.Escape(asset.Title ?? asset.LocalFileName);
            var jsTag = $"\n<script>/* [offline] {safeName} */\n{content}\n</script>";
            if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</body>", jsTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
            else if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</head>", jsTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
            else
                html += jsTag;
        }

        if (!string.IsNullOrWhiteSpace(js))
        {
            var jsTag = $"\n<script>\n{js}\n</script>";
            if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</body>", jsTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
            else
                html += jsTag;
        }

        return html;
    }

    public Dictionary<string, string> ResolveInputValues(HtmlUiNode htmlNode)
    {
        var result = new Dictionary<string, string>();
        if (_host.ViewModel == null) return result;

        var mappings = htmlNode.InputMappings ?? new List<CodeInputMapping>();
        var allNodes = _host.ViewModel.Nodes;

        foreach (var mapping in mappings)
        {
            WorkflowNode? sourceNode = null;
            if (!string.IsNullOrWhiteSpace(mapping.SourceNodeId))
            {
                sourceNode = allNodes?.FirstOrDefault(n =>
                    string.Equals(n.Id, mapping.SourceNodeId, StringComparison.OrdinalIgnoreCase));
            }

            var inputValue = string.Empty;
            if (sourceNode != null)
            {
                var key = string.IsNullOrWhiteSpace(mapping.SourceOutputKey)
                    ? sourceNode.DynamicOutputs?.FirstOrDefault()?.Key ?? "output"
                    : mapping.SourceOutputKey.Trim();
                inputValue = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key);
                if (string.Equals(inputValue?.Trim(), "—", StringComparison.OrdinalIgnoreCase))
                    inputValue = string.Empty;
            }

            var varName = mapping.EffectiveInputKey;
            if (string.IsNullOrWhiteSpace(varName)) varName = "input";
            result[varName] = inputValue ?? string.Empty;
        }

        return result;
    }

    public IReadOnlyList<(string Key, string Selector)> ParseParams(HtmlUiNode htmlNode)
    {
        var result = new List<(string Key, string Selector)>();
        var paramsText = htmlNode.ParamsCode ?? string.Empty;
        var lines = paramsText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (var raw in lines)
        {
            var line = raw?.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("//") || line.StartsWith("#")) continue;

            string[] parts;
            if (line.Contains(":")) parts = line.Split(new[] { ':' }, 2);
            else if (line.Contains("=")) parts = line.Split(new[] { '=' }, 2);
            else continue;

            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var selector = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(selector)) continue;
            result.Add((key, selector));
        }

        return result;
    }

    private static string ReplaceVariables(string text, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(text) || vars.Count == 0) return text;
        var regex = new System.Text.RegularExpressions.Regex(@"\{([^}]+)\}");
        return regex.Replace(text, match =>
        {
            var name = match.Groups[1].Value.Trim();
            return vars.TryGetValue(name, out var value) ? value ?? string.Empty : match.Value;
        });
    }
}
