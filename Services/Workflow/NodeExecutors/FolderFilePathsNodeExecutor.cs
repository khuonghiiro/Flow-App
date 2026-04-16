using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class FolderFilePathsNodeExecutor : INodeExecutor
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tif", ".tiff", ".heic", ".avif"
        };

        public bool CanExecute(WorkflowNode node) => node is FolderFilePathsNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var n = (FolderFilePathsNode)node;
            var connections = env.Connections;
            var sw = Stopwatch.StartNew();

            try
            {
                env.CancellationToken.ThrowIfCancellationRequested();

                SetResolvedOutput(n, "paths", "[]");
                SetResolvedOutput(n, "count", "0");
                SetResolvedOutput(n, "errorMessage", string.Empty);

                if (n.RefreshFolderSourceNodeBeforeUse &&
                    !string.IsNullOrWhiteSpace(n.FolderSourceNodeId) &&
                    !string.Equals(n.FolderSourceNodeId, n.Id, StringComparison.OrdinalIgnoreCase))
                {
                    var sourceToRefresh = FindSourceNode(n.FolderSourceNodeId, connections, n, env.ReachableToEnd);
                    if (sourceToRefresh != null)
                    {
                        await env.Service.ExecuteNodeLogicOnlyAsync(
                            sourceToRefresh,
                            connections,
                            env.CancellationToken,
                            allNodesForLookup: env.ReachableToEnd?.ToList());
                        if (!string.IsNullOrWhiteSpace(env.ExecutionId))
                            env.Service.RefreshScopedOutputsFromNodeRuntime(sourceToRefresh, env.ExecutionId);
                        DataFetcherNodeExecutor.NotifyNodeCompleted(sourceToRefresh);
                    }
                }

                var folder = ResolveString(
                    n.FolderPath,
                    n.FolderSourceNodeId,
                    n.FolderSourceOutputKey,
                    n,
                    connections,
                    env).Trim();

                if (string.IsNullOrWhiteSpace(folder))
                {
                    SetResolvedOutput(n, "errorMessage", "Chưa có đường dẫn thư mục.");
                    sw.Stop();
                    env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                    await PublishAndTraverseAsync(n, env);
                    return;
                }

                if (!Directory.Exists(folder))
                {
                    SetResolvedOutput(n, "errorMessage", $"Thư mục không tồn tại: {folder}");
                    sw.Stop();
                    env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                    await PublishAndTraverseAsync(n, env);
                    return;
                }

                var option = n.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var filterExts = BuildExtensionFilterSet(n.ExtensionFilterText, n.ExtensionTags);
                var readExts = ParseExtensionTokens(n.ReadContentExtensionsText);

                IEnumerable<string> enumerate = Directory.EnumerateFiles(folder, "*.*", option);
                if (filterExts.Count > 0)
                {
                    enumerate = enumerate.Where(p =>
                    {
                        var ext = Path.GetExtension(p);
                        return !string.IsNullOrEmpty(ext) && filterExts.Contains(NormalizeExt(ext));
                    });
                }

                var orderedPaths = enumerate
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                List<string> outputItems;
                if (!n.ReadFileContents)
                {
                    outputItems = orderedPaths;
                }
                else
                {
                    outputItems = new List<string>(orderedPaths.Count);
                    foreach (var path in orderedPaths)
                    {
                        env.CancellationToken.ThrowIfCancellationRequested();
                        var ext = NormalizeExt(Path.GetExtension(path));
                        if (readExts.Count == 0 || !readExts.Contains(ext))
                        {
                            outputItems.Add(path);
                            continue;
                        }

                        try
                        {
                            outputItems.Add(ReadFileAsPayload(path, ext));
                        }
                        catch (Exception ex)
                        {
                            outputItems.Add($"[read error: {path}] {ex.Message}");
                        }
                    }
                }

                var json = JsonSerializer.Serialize(outputItems);
                SetResolvedOutput(n, "paths", json);
                SetResolvedOutput(n, "count", outputItems.Count.ToString());
                SetResolvedOutput(n, "errorMessage", string.Empty);

                sw.Stop();
                env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                await PublishAndTraverseAsync(n, env);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                throw;
            }
            catch (Exception ex)
            {
                SetResolvedOutput(n, "paths", "[]");
                SetResolvedOutput(n, "count", "0");
                SetResolvedOutput(n, "errorMessage", ex.Message);
                sw.Stop();
                env.OnNodeFailed?.Invoke(n, ex.Message);
                env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                await PublishAndTraverseAsync(n, env);
            }
        }

        private static string ReadFileAsPayload(string path, string extNorm)
        {
            var bytes = File.ReadAllBytes(path);
            if (ImageExtensions.Contains(extNorm))
                return Convert.ToBase64String(bytes);

            if (string.Equals(extNorm, ".txt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".md", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".log", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".xml", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".csv", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".htm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".css", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".js", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extNorm, ".svg", StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.UTF8.GetString(bytes);
            }

            try
            {
                var text = Encoding.UTF8.GetString(bytes);
                var bad = 0;
                foreach (var ch in text)
                {
                    if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')
                        bad++;
                    if (bad > 8) break;
                }
                if (bad > 8)
                    return Convert.ToBase64String(bytes);
                return text;
            }
            catch
            {
                return Convert.ToBase64String(bytes);
            }
        }

        private static HashSet<string> BuildExtensionFilterSet(string filterText, IEnumerable<string> tags)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tags)
            {
                var n = NormalizeExtToken(t);
                if (!string.IsNullOrEmpty(n)) set.Add(n);
            }
            foreach (var t in SplitFilterText(filterText))
            {
                var n = NormalizeExtToken(t);
                if (!string.IsNullOrEmpty(n)) set.Add(n);
            }
            return set;
        }

        private static IEnumerable<string> SplitFilterText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            foreach (var part in text.Split(new[] { ',', ';', '|', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Trim();
                if (p.Length > 0) yield return p;
            }
        }

        private static string NormalizeExtToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return string.Empty;
            var t = token.Trim();
            if (t.StartsWith("*.", StringComparison.Ordinal)) t = t[1..];
            if (t.StartsWith('*')) t = t.TrimStart('*');
            if (!t.StartsWith('.')) t = "." + t.TrimStart('.');
            return NormalizeExt(t);
        }

        private static string NormalizeExt(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
            return ext.Trim().ToLowerInvariant();
        }

        private static HashSet<string> ParseExtensionTokens(string? text)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in SplitFilterText(text))
            {
                var n = NormalizeExtToken(t);
                if (!string.IsNullOrEmpty(n)) set.Add(n);
            }
            return set;
        }

        private static async Task PublishAndTraverseAsync(FolderFilePathsNode n, NodeExecutionEnvironment env)
        {
            n.NotifyRuntimeOutputsChanged();
            if (!string.IsNullOrWhiteSpace(env.ExecutionId))
            {
                Dictionary<string, object?> snapshot;
                lock (n.ResolvedOutputsSyncRoot)
                {
                    snapshot = new Dictionary<string, object?>(n.ResolvedOutputs, StringComparer.OrdinalIgnoreCase);
                }
                env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, n.Id, snapshot);
            }
            await env.TraverseOutputsAsync(n);
        }

        private static void SetResolvedOutput(FolderFilePathsNode node, string key, object? value)
        {
            lock (node.ResolvedOutputsSyncRoot)
            {
                node.ResolvedOutputs[key] = value;
            }
        }

        private static string ResolveString(
            string staticValue,
            string? sourceNodeId,
            string? sourceOutputKey,
            FolderFilePathsNode current,
            List<WorkflowConnection> connections,
            NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(sourceOutputKey))
                return staticValue ?? string.Empty;

            var source = FindSourceNode(sourceNodeId, connections, current, env.ReachableToEnd);
            if (source == null)
                return staticValue ?? string.Empty;

            var v = env.Service.ResolveDynamicValueForExecution(source, sourceOutputKey, env);
            if (v == "—" || string.IsNullOrWhiteSpace(v))
                return staticValue ?? string.Empty;
            return v;
        }

        private static WorkflowNode? FindSourceNode(
            string sourceNodeId,
            List<WorkflowConnection> connections,
            FolderFilePathsNode current,
            IEnumerable<WorkflowNode>? allNodes)
        {
            if (allNodes != null)
            {
                var fromAll = allNodes.FirstOrDefault(n =>
                    string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
                if (fromAll != null) return fromAll;
            }

            var upstream = connections.FirstOrDefault(c =>
                c.ToNode == current && c.FromNode != null && c.FromNode.Id == sourceNodeId);
            if (upstream?.FromNode != null) return upstream.FromNode;

            return connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
