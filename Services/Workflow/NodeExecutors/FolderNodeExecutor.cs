using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using System.IO;
using System.Text.RegularExpressions;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho FolderNode: resolve key từ inputs, thay thế {DateTime.*} và {key} trong SubPathTemplate, tạo folder, output folder + fullPath.
    /// </summary>
    internal sealed class FolderNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is FolderNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var folderNode = (FolderNode)node;
            var connections = env.Connections;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var batchOutputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                var rootFolder = ResolveRootFolderPath(folderNode);
                batchOutputs["folder"] = rootFolder;

                if (string.IsNullOrWhiteSpace(rootFolder))
                {
                    batchOutputs["fullPath"] = string.Empty;
                    if (!string.IsNullOrWhiteSpace(env.ExecutionId))
                        env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, folderNode.Id, batchOutputs);
                    folderNode.ResolvedOutputs.Clear();
                    foreach (var kv in batchOutputs)
                        folderNode.ResolvedOutputs[kv.Key] = kv.Value;
                    sw.Stop();
                    env.OnNodeCompleted?.Invoke(folderNode, sw.Elapsed);
                    await ContinueToNextAsync(folderNode, env, connections);
                    return;
                }

                var subPath = folderNode.SubPathTemplate?.Trim() ?? string.Empty;

                var keyValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (folderNode.KeyValueInputs != null)
                {
                    foreach (var kv in folderNode.KeyValueInputs)
                    {
                        var effectiveKey = kv.EffectiveKey;
                        if (string.IsNullOrWhiteSpace(effectiveKey)) continue;

                        WorkflowNode? sourceNode = null;
                        var upstream = connections.FirstOrDefault(c =>
                            c.ToNode == folderNode && c.FromNode != null && c.FromNode.Id == kv.SourceNodeId);
                        sourceNode = upstream?.FromNode;
                        if (sourceNode == null)
                        {
                            sourceNode = connections
                                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                                .FirstOrDefault(n => n != null && n.Id == kv.SourceNodeId);
                        }

                        var value = sourceNode != null
                            ? (env.Service.ResolveDynamicValueForExecution(sourceNode, kv.SourceOutputKey ?? "", env) ?? "")
                            : "";
                        if (value == "—") value = "";
                        keyValues[effectiveKey] = value ?? "";
                    }
                }

                var now = DateTime.Now;
                subPath = ReplacePlaceholders(subPath, keyValues, now);
                subPath = subPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                subPath = subPath.Trim(Path.DirectorySeparatorChar, ' ', '\t');

                var fullPath = string.IsNullOrWhiteSpace(subPath)
                    ? rootFolder
                    : Path.Combine(rootFolder, subPath);

                try
                {
                    if (!string.IsNullOrWhiteSpace(fullPath) && !Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"FolderNode: CreateDirectory failed: {ex.Message}");
                    env.OnNodeFailed?.Invoke(folderNode, ex.Message);
                    throw;
                }

                batchOutputs["fullPath"] = fullPath ?? "";

                if (!string.IsNullOrWhiteSpace(env.ExecutionId))
                    env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, folderNode.Id, batchOutputs);
                folderNode.ResolvedOutputs.Clear();
                foreach (var kv in batchOutputs)
                    folderNode.ResolvedOutputs[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FolderNode error: {ex.Message}");
                folderNode.ResolvedOutputs["folder"] = folderNode.RootFolderPath ?? "";
                folderNode.ResolvedOutputs["fullPath"] = "";
                env.OnNodeFailed?.Invoke(folderNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(folderNode, sw.Elapsed);
            await ContinueToNextAsync(folderNode, env, connections);
        }

        private static string ReplacePlaceholders(string template, Dictionary<string, string> keyValues, DateTime now)
        {
            var s = template;

            // Thời gian: dạng ngắn {YYYY}, {MM}, {DD}, {HH}, {mm}, {ss}
            s = Regex.Replace(s, @"\{YYYY\}", now.Year.ToString(), RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\{YY\}", (now.Year % 100).ToString("D2"), RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\{MM\}", now.Month.ToString("D2"), RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\{DD\}", now.Day.ToString("D2"), RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\{HH\}", now.Hour.ToString("D2"), RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\{mm\}", now.Minute.ToString("D2"), RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\{ss\}", now.Second.ToString("D2"), RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\{yyyyMMdd\}", now.ToString("yyyyMMdd"), RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\{yyyy-MM-dd\}", now.ToString("yyyy-MM-dd"), RegexOptions.IgnoreCase);

            foreach (var kv in keyValues)
            {
                s = Regex.Replace(s, Regex.Escape("{" + kv.Key + "}"), kv.Value ?? "", RegexOptions.IgnoreCase);
            }

            return s;
        }

        private static async Task ContinueToNextAsync(FolderNode folderNode, NodeExecutionEnvironment env, IEnumerable<WorkflowConnection> connections)
        {
            await env.TraverseOutputsAsync(folderNode);
        }

        private static string ResolveRootFolderPath(FolderNode folderNode)
        {
            var preset = folderNode.RootFolderPresetKey?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(preset))
            {
                return preset switch
                {
                    "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "videos" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    "downloads" => ResolveDownloadsFolder(),
                    _ => folderNode.RootFolderPath?.Trim() ?? string.Empty
                };
            }
            return folderNode.RootFolderPath?.Trim() ?? string.Empty;
        }

        private static string ResolveDownloadsFolder()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile))
                return Path.Combine(profile, "Downloads");
            return string.Empty;
        }
    }
}
