using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Models.Persistence;
using FlowMy.Services.Rendering;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService : IWorkflowPersistenceService
{
    /// <summary>ThÃ†Â° mÃ¡Â»Â¥c con trong Documents khi lÃ†Â°u workflow mÃ¡ÂºÂ·c Ã„â€˜Ã¡Â»â€¹nh (khÃƒÂ´ng phÃ¡Â»Â¥ thuÃ¡Â»â„¢c thÃ†Â° mÃ¡Â»Â¥c chÃ¡ÂºÂ¡y / bin).</summary>
    public const string DefaultWorkflowJsonFolderName = "Workflow_Json";
    private const string FlowMyRootFolderName = "FlowMy";

    private readonly FlowMy.Workflow.TemplateFactory _templateFactory;
    private readonly string _workflowsDir;
    private static readonly ConcurrentDictionary<string, CachedWorkflowJson> _workflowJsonCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CachedWorkflowJson(DateTime LastWriteUtc, string Json);

    /// <summary>Ã„ÂÃ†Â°Ã¡Â»Âng dÃ¡ÂºÂ«n mÃ¡ÂºÂ·c Ã„â€˜Ã¡Â»â€¹nh: Documents\FlowMy\Workflow_Json; nÃ¡ÂºÂ¿u khÃƒÂ´ng lÃ¡ÂºÂ¥y Ã„â€˜Ã†Â°Ã¡Â»Â£c Documents thÃƒÂ¬ fallback cÃ¡ÂºÂ¡nh exe.</summary>
    public static string GetDefaultWorkflowsDirectory()
    {
        try
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(docs))
                return Path.Combine(docs, FlowMyRootFolderName, DefaultWorkflowJsonFolderName);
        }
        catch
        {
            // ignored
        }

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultWorkflowJsonFolderName);
    }

    public FileWorkflowPersistenceService(FlowMy.Workflow.TemplateFactory templateFactory)
    {
        _templateFactory = templateFactory ?? throw new ArgumentNullException(nameof(templateFactory));
        _workflowsDir = GetDefaultWorkflowsDirectory();
    }

    public IReadOnlyList<string> GetAllWorkflowNames()
    {
        try
        {
            if (!Directory.Exists(_workflowsDir))
            {
                return Array.Empty<string>();
            }

            var files = Directory.GetFiles(_workflowsDir, "*.json");
            return files
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public void Save(
        string workflowName,
        IEnumerable<WorkflowNode> nodes,
        IEnumerable<WorkflowConnection> connections,
        double zoomLevel = 1.0,
        double panX = 0.0,
        double panY = 0.0,
        double? savedScreenWidth = null,
        double? savedScreenHeight = null,
        double? savedViewportCenterX = null,
        double? savedViewportCenterY = null,
        bool isZoomLocked = false,
        string? connectionLineStyle = null)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("Workflow name is required", nameof(workflowName));

        try
        {
            if (!Directory.Exists(_workflowsDir))
                Directory.CreateDirectory(_workflowsDir);

            // Ctrl+S / Save button: lÃ†Â°u Ã„â€˜Ã¡ÂºÂ§y Ã„â€˜Ã¡Â»Â§ logic (khÃƒÂ´ng runtime output)
            WorkflowDto? dto = null;
            try
            {
                dto = BuildWorkflowDto(
                    workflowName,
                    nodes,
                    connections,
                    includeRuntimeOutput: false,
                    zoomLevel,
                    panX,
                    panY,
                    savedScreenWidth,
                    savedScreenHeight,
                    savedViewportCenterX,
                    savedViewportCenterY,
                    isZoomLocked,
                    connectionLineStyle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error building workflow DTO: {ex.Message}\n{ex.StackTrace}");
                throw;
            }

            string json;
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    MaxDepth = 64 // GiÃ¡Â»â€ºi hÃ¡ÂºÂ¡n Ã„â€˜Ã¡Â»â„¢ sÃƒÂ¢u Ã„â€˜Ã¡Â»Æ’ trÃƒÂ¡nh stack overflow
                };
                json = JsonSerializer.Serialize(dto, options);
            }
            catch (System.Text.Json.JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON serialization error: {ex.Message}\n{ex.StackTrace}");
                // ThÃ¡Â»Â­ lÃ¡ÂºÂ¡i khÃƒÂ´ng serialize output values
                dto = BuildWorkflowDto(
                    workflowName,
                    nodes,
                    connections,
                    includeRuntimeOutput: false,
                    zoomLevel,
                    panX,
                    panY,
                    savedScreenWidth,
                    savedScreenHeight,
                    savedViewportCenterX,
                    savedViewportCenterY,
                    isZoomLocked,
                    connectionLineStyle);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    MaxDepth = 64
                };
                json = JsonSerializer.Serialize(dto, options);
            }

            var fileName = $"{workflowName}.json";
            var filePath = Path.Combine(_workflowsDir, fileName);

            File.WriteAllText(filePath, json);
            File.SetAttributes(filePath, FileAttributes.Normal);
            _workflowJsonCache[filePath] = new CachedWorkflowJson(File.GetLastWriteTimeUtc(filePath), json);

            WebNodeCacheHelper.SaveWorkflowWebNodeCaches(_workflowsDir, workflowName, nodes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving workflow: {ex.Message}\n{ex.StackTrace}");
            throw; // Re-throw Ã„â€˜Ã¡Â»Æ’ caller cÃƒÂ³ thÃ¡Â»Æ’ xÃ¡Â»Â­ lÃƒÂ½
        }
    }

    public WorkflowLoadResult? Load(string workflowName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workflowName)) return null;

            var path = Path.Combine(_workflowsDir, $"{workflowName}.json");
            if (!File.Exists(path)) return null;

            var fileLastWriteUtc = File.GetLastWriteTimeUtc(path);
            var json = TryGetCachedWorkflowJson(path, fileLastWriteUtc) ?? File.ReadAllText(path);
            _workflowJsonCache[path] = new CachedWorkflowJson(fileLastWriteUtc, json);
            var result = ImportFromJson(json);
            if (result != null)
            {
                WebNodeCacheHelper.RestoreWorkflowWebNodeCaches(_workflowsDir, workflowName, result.Nodes);
                WebNodeCacheHelper.RestoreWorkflowSharedWebProfile(_workflowsDir, workflowName);
                WebNodeCacheHelper.RestoreHtmlOfflineAssetsBundle(_workflowsDir, workflowName);

                if (!string.IsNullOrWhiteSpace(result.PortableWebBundleFileName))
                {
                    var zipFull = Path.Combine(Path.GetDirectoryName(path)!, result.PortableWebBundleFileName);
                    if (File.Exists(zipFull))
                    {
                        try
                        {
                            PortableWebBundleZipService.ExtractAndRestore(zipFull, result.Nodes);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Load: giÃ¡ÂºÂ£i nÃƒÂ©n web bundle lÃ¡Â»â€”i: {ex.Message}");
                        }
                    }
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading workflow: {ex.Message}");
            return null;
        }
    }

    private static string? TryGetCachedWorkflowJson(string filePath, DateTime fileLastWriteUtc)
    {
        if (_workflowJsonCache.TryGetValue(filePath, out var cache) &&
            cache.LastWriteUtc == fileLastWriteUtc)
        {
            return cache.Json;
        }

        return null;
    }

    /// <summary>
    /// Export chÃ¡Â»â€° logic (nodes, connections, properties), khÃƒÂ´ng cÃƒÂ³ output/runtime.
    /// DÃƒÂ¹ng cho nÃƒÂºt Export vÃƒÂ  chia sÃ¡ÂºÂ» file.
    /// </summary>
    public string ExportToJson(
        string workflowName,
        IEnumerable<WorkflowNode> nodes,
        IEnumerable<WorkflowConnection> connections,
        double zoomLevel = 1.0,
        double panX = 0.0,
        double panY = 0.0,
        double? savedScreenWidth = null,
        double? savedScreenHeight = null,
        double? savedViewportCenterX = null,
        double? savedViewportCenterY = null,
        bool isZoomLocked = false,
        string? connectionLineStyle = null,
        string? portableWebBundleFileName = null,
        bool includeRuntimeOutput = false,
        WorkflowExportOptionsDto? exportOptions = null,
        string? embeddedPortableWebBundleBase64 = null)
    {
        var dto = BuildWorkflowDto(
            workflowName,
            nodes,
            connections,
            includeRuntimeOutput,
            zoomLevel,
            panX,
            panY,
            savedScreenWidth,
            savedScreenHeight,
            savedViewportCenterX,
            savedViewportCenterY,
            isZoomLocked,
            connectionLineStyle,
            portableWebBundleFileName,
            exportOptions,
            embeddedPortableWebBundleBase64);
        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    private WorkflowDto BuildWorkflowDto(
        string workflowName,
        IEnumerable<WorkflowNode> nodes,
        IEnumerable<WorkflowConnection> connections,
        bool includeRuntimeOutput,
        double zoomLevel = 1.0,
        double panX = 0.0,
        double panY = 0.0,
        double? savedScreenWidth = null,
        double? savedScreenHeight = null,
        double? savedViewportCenterX = null,
        double? savedViewportCenterY = null,
        bool isZoomLocked = false,
        string? connectionLineStyle = null,
        string? portableWebBundleFileName = null,
        WorkflowExportOptionsDto? exportOptions = null,
        string? embeddedPortableWebBundleBase64 = null)
    {
        var orderedNodes = OrderNodesForExport(nodes.ToList(), connections.ToList());

        var allNodes = orderedNodes
            .Concat(orderedNodes.OfType<LoopNode>().Where(l => l.LoopBodyNode != null).Select(l => l.LoopBodyNode))
            .Concat(orderedNodes.OfType<AsyncTaskNode>().Where(a => a.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch && a.AsyncTaskBodyNode != null).Select(a => a.AsyncTaskBodyNode!))
            .GroupBy(n => n.Id)
            .Select(g => g.First())
            .ToList();

        var dto = new WorkflowDto
        {
            Name = workflowName,
            ZoomLevel = zoomLevel,
            PanX = panX,
            PanY = panY,
            SavedScreenWidth = savedScreenWidth,
            SavedScreenHeight = savedScreenHeight,
            SavedViewportCenterX = savedViewportCenterX,
            SavedViewportCenterY = savedViewportCenterY,
            IsZoomLocked = isZoomLocked,
            ConnectionLineStyle = string.IsNullOrWhiteSpace(connectionLineStyle)
                ? "Bezier"
                : connectionLineStyle,
            PortableWebBundleFileName = string.IsNullOrWhiteSpace(portableWebBundleFileName)
                ? null
                : portableWebBundleFileName.Trim(),
            ExportOptions = exportOptions,
            EmbeddedPortableWebBundleBase64 = string.IsNullOrWhiteSpace(embeddedPortableWebBundleBase64)
                ? null
                : embeddedPortableWebBundleBase64,
            Nodes = allNodes.Select(n => BuildNodeDto(n, includeRuntimeOutput)).ToList(),
            Connections = connections.Select(c => new ConnectionDto
            {
                FromNodeId = c.FromNode.Id,
                ToNodeId = c.ToNode.Id,
                FromPortId = c.FromPort?.Id,
                ToPortId = c.ToPort?.Id
            }).ToList()
        };

        return dto;
    }

    private static NodeDto BuildNodeDto(WorkflowNode n, bool includeRuntimeOutput)
    {
        var ports = new List<PortDto>();
        if (n.IsConditionalNode && n.ConditionalBranches != null)
        {
            var inputPort = n.Ports.FirstOrDefault(p => p.IsInput);
            if (inputPort != null)
                ports.Add(new PortDto { Id = inputPort.Id, IsInput = true, Position = inputPort.Position.ToString(), Index = 0 });
            for (int i = 0; i < n.ConditionalBranches.Count; i++)
            {
                var branch = n.ConditionalBranches[i];
                if (branch.Port != null)
                    ports.Add(new PortDto { Id = branch.Port.Id, IsInput = false, Position = branch.Port.Position.ToString(), Index = i, BranchIndex = i });
            }
        }
        else if (n is AsyncTaskNode atn && atn.UiPresentationMode == AsyncTaskUiPresentationMode.ManualBranches && atn.AsyncTaskBranches != null)
        {
            var inputPort = n.Ports.FirstOrDefault(p => p.IsInput);
            if (inputPort != null)
                ports.Add(new PortDto { Id = inputPort.Id, IsInput = true, Position = inputPort.Position.ToString(), Index = 0 });
            for (int i = 0; i < atn.AsyncTaskBranches.Count; i++)
            {
                var branch = atn.AsyncTaskBranches[i];
                if (branch.Port != null)
                    ports.Add(new PortDto { Id = branch.Port.Id, IsInput = false, Position = branch.Port.Position.ToString(), Index = i, BranchIndex = i });
            }
        }
        else
        {
            ports = n.Ports.Select(p => new PortDto
            {
                Id = p.Id,
                IsInput = p.IsInput,
                Position = p.Position.ToString(),
                Index = n.Ports.Where(p2 => p2.Position == p.Position && p2.IsInput == p.IsInput).ToList().IndexOf(p)
            }).ToList();
        }
        return new NodeDto
        {
            Id = n.Id,
            Title = n.Title,
            X = n.X,
            Y = n.Y,
            Type = n.Type.ToString(),
            ColorKey = n.ColorKey,
            Properties = GetNodeProperties(n),
            Ports = ports,
            OutputValues = includeRuntimeOutput ? GetNodeOutputValues(n) : null
        };
    }

    private static Dictionary<string, string>? GetNodeOutputValues(WorkflowNode node)
    {
        if (node.DynamicOutputs == null || node.DynamicOutputs.Count == 0)
            return null;

        // Ã¢Å¡Â Ã¯Â¸Â CRITICAL: KhÃƒÂ´ng lÃ†Â°u output values cho InputNode vÃƒÂ  cÃƒÂ¡c node cÃƒÂ³ property trÃ¡Â»Â±c tiÃ¡ÂºÂ¿p
        // Ã„â€˜Ã¡Â»Æ’ trÃƒÂ¡nh tÃƒÂ¬nh trÃ¡ÂºÂ¡ng giÃƒÂ¡ trÃ¡Â»â€¹ cÃ…Â© (tÃ¡Â»Â« execution) override giÃƒÂ¡ trÃ¡Â»â€¹ mÃ¡Â»â€ºi (tÃ¡Â»Â« user edit)
        if (node is InputNode)
        {
            // InputNode cÃƒÂ³ property Value/ArrayValues mÃƒÂ  user cÃƒÂ³ thÃ¡Â»Æ’ sÃ¡Â»Â­a trÃ¡Â»Â±c tiÃ¡ÂºÂ¿p
            // KhÃƒÂ´ng lÃ†Â°u UserValueOverride Ã„â€˜Ã¡Â»Æ’ trÃƒÂ¡nh conflict vÃ¡Â»â€ºi giÃƒÂ¡ trÃ¡Â»â€¹ mÃ¡Â»â€ºi
            return null;
        }

        // Ã„ÂÃ¡ÂºÂ·c biÃ¡Â»â€¡t xÃ¡Â»Â­ lÃƒÂ½ WebNode: khÃƒÂ´ng serialize output values khi WebView2 Ã„â€˜ang chÃ¡ÂºÂ¡y
        // vÃƒÂ¬ cÃƒÂ³ thÃ¡Â»Æ’ cÃƒÂ³ cÃƒÂ¡c giÃƒÂ¡ trÃ¡Â»â€¹ lÃ¡Â»â€ºn hoÃ¡ÂºÂ·c phÃ¡Â»Â©c tÃ¡ÂºÂ¡p khÃƒÂ´ng thÃ¡Â»Æ’ serialize
        if (node is WebNode)
        {
            // BÃ¡Â»Â qua serialize output values cho WebNode Ã„â€˜Ã¡Â»Æ’ trÃƒÂ¡nh lÃ¡Â»â€”i
            return null;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var output in node.DynamicOutputs)
        {
            try
            {
                var key = output.Key?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key)) continue;

                var value = NodeDataPanelService.ResolveDynamicValueByKey(node, key);
                if (string.IsNullOrWhiteSpace(value) || value == "Ã¢â‚¬â€") continue;

                // GiÃ¡Â»â€ºi hÃ¡ÂºÂ¡n Ã„â€˜Ã¡Â»â„¢ dÃƒÂ i giÃƒÂ¡ trÃ¡Â»â€¹ Ã„â€˜Ã¡Â»Æ’ trÃƒÂ¡nh serialize quÃƒÂ¡ lÃ¡Â»â€ºn (max 10KB per value)
                //const int maxValueLength = 10 * 1024;
                //if (value.Length > maxValueLength)
                //{
                //    value = value.Substring(0, maxValueLength) + "... (truncated)";
                //}

                dict[key] = value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting output value for key '{output.Key}': {ex.Message}");
                // Continue with next output
            }
        }

        return dict.Count == 0 ? null : dict;
    }

    public WorkflowLoadResult? ImportFromJson(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<WorkflowDto>(json);
            if (dto == null) return null;

            var nodes = new List<WorkflowNode>();
            var connections = new List<WorkflowConnection>();
            var nodeMap = new Dictionary<string, WorkflowNode>();

            var importedName = dto.Name?.Trim();
            if (string.IsNullOrWhiteSpace(importedName))
            {
                importedName = $"Imported_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            // 1. Recreate Nodes (including LoopBody placeholders)
            foreach (var nodeDto in dto.Nodes)
            {
                WorkflowNode node;
                var isLoopBodyDto = !string.IsNullOrEmpty(nodeDto.Id) &&
                                    nodeDto.Id.StartsWith("LoopBody_", StringComparison.OrdinalIgnoreCase);
                var isAsyncTaskBodyDto = !string.IsNullOrEmpty(nodeDto.Id) &&
                                         nodeDto.Id.StartsWith("AsyncTaskBody_", StringComparison.OrdinalIgnoreCase);
                if (isLoopBodyDto)
                {
                    node = new LoopBodyNode();
                    EnsureLoopBodyPortsExist((LoopBodyNode)node);
                }
                else if (isAsyncTaskBodyDto)
                {
                    node = new AsyncTaskBodyNode();
                    WorkflowExecutionService.EnsureAsyncTaskBodyPortsExist((AsyncTaskBodyNode)node);
                }
                else
                {
                    node = _templateFactory.Create(nodeDto.Type, nodeDto.X, nodeDto.Y);
                }

                node.Id = nodeDto.Id;
                node.Title = nodeDto.Title;
                node.X = nodeDto.X;
                node.Y = nodeDto.Y;
                node.ColorKey = nodeDto.ColorKey;

                // ConditionalNode vÃƒÂ  AsyncTaskNode: restore branches trÃ†Â°Ã¡Â»â€ºc Ã„â€˜Ã¡Â»Æ’ cÃƒÂ³ Ã„â€˜Ã¡Â»Â§ sÃ¡Â»â€˜ port trÃ†Â°Ã¡Â»â€ºc khi restore Port IDs
                if (node.IsConditionalNode || node is AsyncTaskNode)
                {
                    RestoreNodeProperties(node, nodeDto.Properties);
                }

                // Restore Ports (Id + Position) nÃ¡ÂºÂ¿u workflow cÃƒÂ³ lÃ†Â°u lÃ¡ÂºÂ¡i cÃ¡ÂºÂ¥u hÃƒÂ¬nh port
                if (nodeDto.Ports != null && nodeDto.Ports.Any())
                {
                    foreach (var portDto in nodeDto.Ports)
                    {
                        if (!Enum.TryParse<PortPosition>(portDto.Position, out var pos))
                            continue;

                        NodePort? targetPort = null;

                        // ConditionalNode/AsyncTaskNode: match input port trÃ¡Â»Â±c tiÃ¡ÂºÂ¿p (chÃ¡Â»â€° cÃƒÂ³ 1 input port)
                        if (portDto.IsInput)
                        {
                            targetPort = node.Ports.FirstOrDefault(p => p.IsInput);
                        }
                        // ConditionalNode: match output port theo BranchIndex (file mÃ¡Â»â€ºi cÃƒÂ³ BranchIndex).
                        // Fallback sang Index Ã„â€˜Ã¡Â»Æ’ tÃ†Â°Ã†Â¡ng thÃƒÂ­ch file cÃ…Â©, trÃƒÂ¡nh map nhÃ¡ÂºÂ§m theo Position (vÃƒÂ¬ nhiÃ¡Â»Âu nhÃƒÂ¡nh cÃƒÂ¹ng Position).
                        else if (node.IsConditionalNode && node.ConditionalBranches != null)
                        {
                            int? bi = portDto.BranchIndex;
                            if (bi.HasValue && bi.Value >= 0 && bi.Value < node.ConditionalBranches.Count)
                                targetPort = node.ConditionalBranches[bi.Value].Port;
                            else if (portDto.Index >= 0 && portDto.Index < node.ConditionalBranches.Count)
                                targetPort = node.ConditionalBranches[portDto.Index].Port;
                        }
                        // AsyncTaskNode (manual): match output port theo BranchIndex (file mÃ¡Â»â€ºi)
                        else if (node is AsyncTaskNode atn && atn.UiPresentationMode == AsyncTaskUiPresentationMode.ManualBranches && atn.AsyncTaskBranches != null)
                        {
                            int? bi = portDto.BranchIndex;
                            if (bi.HasValue && bi.Value >= 0 && bi.Value < atn.AsyncTaskBranches.Count)
                                targetPort = atn.AsyncTaskBranches[bi.Value].Port;
                        }
                        // Fallback: match theo ID, Position, hoÃ¡ÂºÂ·c Index (cho node khÃƒÂ¡c hoÃ¡ÂºÂ·c file cÃ…Â©)
                        if (targetPort == null)
                        {
                            var portById = node.Ports.FirstOrDefault(p => p.Id == portDto.Id);
                            if (portById != null && portById.IsInput == portDto.IsInput)
                                targetPort = portById;
                            else
                            {
                                var portByPosition = node.Ports.Where(p => p.IsInput == portDto.IsInput && p.Position == pos).FirstOrDefault();
                                if (portByPosition != null)
                                    targetPort = portByPosition;
                                else
                                {
                                    var portsSameDirection = node.Ports.Where(p => p.IsInput == portDto.IsInput).ToList();
                                    targetPort = (portDto.Index >= 0 && portDto.Index < portsSameDirection.Count)
                                        ? portsSameDirection[portDto.Index]
                                        : portsSameDirection.FirstOrDefault();
                                }
                            }
                        }

                        if (targetPort != null)
                        {
                            targetPort.Id = portDto.Id;
                            targetPort.Position = pos;
                        }
                    }
                }

                // RestoreNodeProperties Ã„â€˜ÃƒÂ£ gÃ¡Â»Âi Ã¡Â»Å¸ trÃƒÂªn cho Conditional/AsyncTask; vÃ¡Â»â€ºi cÃƒÂ¡c node khÃƒÂ¡c gÃ¡Â»Âi Ã¡Â»Å¸ Ã„â€˜ÃƒÂ¢y
                if (!node.IsConditionalNode && !(node is AsyncTaskNode))
                {
                    RestoreNodeProperties(node, nodeDto.Properties);
                }

                nodeMap[node.Id] = node;

                if (node is not LoopBodyNode && node is not AsyncTaskBodyNode)
                {
                    nodes.Add(node);
                }
            }

            // 1.5 Attach LoopBody to its parent Loop using connections
            foreach (var loopNode in nodes.OfType<LoopNode>())
            {
                var link = dto.Connections.FirstOrDefault(c => c.FromNodeId == loopNode.Id && c.ToNodeId.StartsWith("LoopBody_"));
                if (link != null && nodeMap.TryGetValue(link.ToNodeId, out var bodyNode) && bodyNode is LoopBodyNode importedBody)
                {
                    loopNode.LoopBodyNode.Id = importedBody.Id;
                    loopNode.LoopBodyNode.Title = importedBody.Title;
                    loopNode.LoopBodyNode.X = importedBody.X;
                    loopNode.LoopBodyNode.Y = importedBody.Y;
                    // Ã¢Å“â€¦ Guard: Ã„â€˜Ã¡ÂºÂ£m bÃ¡ÂºÂ£o Width/Height hÃ¡Â»Â£p lÃ¡Â»â€¡ trÃƒÂ¡nh lÃ¡Â»â€”i 'height must be non-negative' khi import
                    loopNode.LoopBodyNode.Width = Math.Max(100, importedBody.Width);
                    loopNode.LoopBodyNode.Height = Math.Max(80, importedBody.Height);

                    EnsureLoopBodyPortsExist(loopNode.LoopBodyNode);
                    EnsureLoopBodyPortsExist(importedBody);

                    CopyLoopBodyPortId(importedBody, loopNode.LoopBodyNode, "LoopBodyTop");
                    CopyLoopBodyPortId(importedBody, loopNode.LoopBodyNode, "LoopBodyLeft");
                    CopyLoopBodyPortId(importedBody, loopNode.LoopBodyNode, "LoopBodyRight");

                    nodeMap[link.ToNodeId] = loopNode.LoopBodyNode;
                }

                // Ã¢Å“â€¦ Ã„ÂÃ¡ÂºÂ£m bÃ¡ÂºÂ£o LoopNode ports cÃƒÂ³ Ã„â€˜ÃƒÂºng ID vÃƒÂ  Position sau khi restore
                // Ã„ÂÃ¡ÂºÂ·c biÃ¡Â»â€¡t quan trÃ¡Â»Âng cho LoopNodeBottom vÃƒÂ  LoopNodeOut
                var loopNodeDto = dto.Nodes.FirstOrDefault(n => n.Id == loopNode.Id);
                if (loopNodeDto?.Ports != null)
                {
                    foreach (var portDto in loopNodeDto.Ports)
                    {
                        if (!Enum.TryParse<PortPosition>(portDto.Position, out var pos))
                            continue;

                        // TÃƒÂ¬m port theo ID trÃ†Â°Ã¡Â»â€ºc
                        var existingPort = loopNode.Ports.FirstOrDefault(p => p.Id == portDto.Id);
                        if (existingPort != null)
                        {
                            existingPort.Position = pos;
                            continue;
                        }

                        // NÃ¡ÂºÂ¿u chÃ†Â°a cÃƒÂ³, tÃƒÂ¬m port theo Position vÃƒÂ  Direction
                        var portByPos = loopNode.Ports
                            .FirstOrDefault(p => p.IsInput == portDto.IsInput && p.Position == pos);
                        if (portByPos != null)
                        {
                            portByPos.Id = portDto.Id;
                            portByPos.Position = pos;
                        }
                    }
                }
            }

            // 1.55 Attach AsyncTaskBody to parent AsyncTask (loop-like)
            foreach (var asyncTaskNode in nodes.OfType<AsyncTaskNode>())
            {
                if (asyncTaskNode.UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch || asyncTaskNode.AsyncTaskBodyNode == null)
                    continue;

                var link = dto.Connections.FirstOrDefault(c =>
                    c.FromNodeId == asyncTaskNode.Id &&
                    c.ToNodeId.StartsWith("AsyncTaskBody_", StringComparison.OrdinalIgnoreCase));
                if (link == null) continue;
                if (!nodeMap.TryGetValue(link.ToNodeId, out var rawBody) || rawBody is not AsyncTaskBodyNode importedAsyncBody)
                    continue;

                var officialBody = asyncTaskNode.AsyncTaskBodyNode;
                officialBody.Id = importedAsyncBody.Id;
                officialBody.Title = importedAsyncBody.Title;
                officialBody.X = importedAsyncBody.X;
                officialBody.Y = importedAsyncBody.Y;
                // Ã¢Å“â€¦ Guard: Ã„â€˜Ã¡ÂºÂ£m bÃ¡ÂºÂ£o Width/Height hÃ¡Â»Â£p lÃ¡Â»â€¡ trÃƒÂ¡nh lÃ¡Â»â€”i 'height must be non-negative' khi import
                officialBody.Width = Math.Max(200, importedAsyncBody.Width);
                officialBody.Height = Math.Max(200, importedAsyncBody.Height);
                officialBody.ParentAsyncTaskNode = asyncTaskNode;

                WorkflowExecutionService.EnsureAsyncTaskBodyPortsExist(officialBody);
                WorkflowExecutionService.EnsureAsyncTaskBodyPortsExist(importedAsyncBody);

                CopyBodyPortId(importedAsyncBody, officialBody, "LoopBodyTop");
                CopyBodyPortId(importedAsyncBody, officialBody, "LoopBodyLeft");
                CopyBodyPortId(importedAsyncBody, officialBody, "LoopBodyRight");

                nodeMap[link.ToNodeId] = officialBody;
            }

            // 2. Recreate Connections
            foreach (var connDto in dto.Connections)
            {
                if (nodeMap.TryGetValue(connDto.FromNodeId, out var fromNode) &&
                    nodeMap.TryGetValue(connDto.ToNodeId, out var toNode))
                {
                    if (fromNode is LoopBodyNode fromBody) EnsureLoopBodyPortsExist(fromBody);
                    if (toNode is LoopBodyNode toBody) EnsureLoopBodyPortsExist(toBody);
                    if (fromNode is AsyncTaskBodyNode fromAtBody) WorkflowExecutionService.EnsureAsyncTaskBodyPortsExist(fromAtBody);
                    if (toNode is AsyncTaskBodyNode toAtBody) WorkflowExecutionService.EnsureAsyncTaskBodyPortsExist(toAtBody);

                    // Ã¢Å“â€¦ Ã†Â¯u tiÃƒÂªn match theo Port ID (chÃƒÂ­nh xÃƒÂ¡c nhÃ¡ÂºÂ¥t)
                    NodePort? fromPort = null;
                    NodePort? toPort = null;

                    if (!string.IsNullOrEmpty(connDto.FromPortId))
                    {
                        fromPort = fromNode.Ports.FirstOrDefault(p => p.Id == connDto.FromPortId);
                    }

                    if (!string.IsNullOrEmpty(connDto.ToPortId))
                    {
                        toPort = toNode.Ports.FirstOrDefault(p => p.Id == connDto.ToPortId);
                    }

                    // Ã¢Å“â€¦ NÃ¡ÂºÂ¿u khÃƒÂ´ng tÃƒÂ¬m thÃ¡ÂºÂ¥y theo ID, chÃ¡Â»â€° fallback cho node cÃƒÂ³ 1 port out duy nhÃ¡ÂºÂ¥t.
                    // LoopNode, ConditionalNode, AsyncTaskNode cÃƒÂ³ nhiÃ¡Â»Âu output ports - khÃƒÂ´ng fallback.
                    if (fromPort == null && !(fromNode is LoopNode) && !fromNode.IsConditionalNode && !(fromNode is AsyncTaskNode))
                    {
                        fromPort = fromNode.Ports.FirstOrDefault(p => !p.IsInput);
                    }

                    if (toPort == null && !(toNode is LoopNode) && !(toNode is LoopBodyNode) && !(toNode is AsyncTaskBodyNode))
                    {
                        toPort = toNode.Ports.FirstOrDefault(p => p.IsInput);
                    }

                    // Ã¢Å“â€¦ Ã„ÂÃ¡Â»â€˜i vÃ¡Â»â€ºi LoopNode, ConditionalNode, AsyncTaskNode, LoopBodyNode: chÃ¡Â»â€° tÃ¡ÂºÂ¡o connection nÃ¡ÂºÂ¿u tÃƒÂ¬m thÃ¡ÂºÂ¥y Ã„â€˜ÃƒÂºng port theo ID
                    if (fromNode is LoopNode || toNode is LoopNode || fromNode is LoopBodyNode || toNode is LoopBodyNode
                        || fromNode is AsyncTaskBodyNode || toNode is AsyncTaskBodyNode
                        || fromNode.IsConditionalNode || fromNode is AsyncTaskNode)
                    {
                        if (fromPort == null || toPort == null)
                        {
                            // Skip connection nÃ¡ÂºÂ¿u khÃƒÂ´ng tÃƒÂ¬m thÃ¡ÂºÂ¥y Ã„â€˜ÃƒÂºng port cho loop nodes
                            continue;
                        }
                    }

                    if (fromPort != null && toPort != null)
                    {
                        var connection = new WorkflowConnection
                        {
                            FromNode = fromNode,
                            ToNode = toNode,
                            FromPort = fromPort,
                            ToPort = toPort
                        };
                        connections.Add(connection);
                    }
                }
            }

            // Ã¢Å“â€¦ Rebuild LoopNode outputs tÃ¡Â»Â« ListOutNodes trong LoopBody
            // PhÃ¡ÂºÂ£i gÃ¡Â»Âi sau khi Ã„â€˜ÃƒÂ£ cÃƒÂ³ Ã„â€˜Ã¡ÂºÂ§y Ã„â€˜Ã¡Â»Â§ connections
            foreach (var loopNode in nodes.OfType<LoopNode>())
            {
                loopNode.RebuildOutputsFromLoopBody(connections, nodes);
            }

            return new WorkflowLoadResult
            {
                Name = importedName,
                Nodes = nodes,
                Connections = connections,
                ZoomLevel = dto.ZoomLevel,
                PanX = dto.PanX,
                PanY = dto.PanY,
                SavedScreenWidth = dto.SavedScreenWidth,
                SavedScreenHeight = dto.SavedScreenHeight,
                SavedViewportCenterX = dto.SavedViewportCenterX,
                SavedViewportCenterY = dto.SavedViewportCenterY,
                IsZoomLocked = dto.IsZoomLocked,
                ConnectionLineStyle = dto.ConnectionLineStyle,
                PortableWebBundleFileName = string.IsNullOrWhiteSpace(dto.PortableWebBundleFileName)
                    ? null
                    : dto.PortableWebBundleFileName.Trim()
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error importing workflow: {ex.Message}");
            return null;
        }
    }

    private static List<WorkflowNode> OrderNodesForExport(
        List<WorkflowNode> nodes,
        List<WorkflowConnection> connections)
    {
        var ordered = new List<WorkflowNode>();
        var visited = new HashSet<string>();

        var startNodes = nodes.Where(n => n.Type == NodeType.Start).ToList();
        var endNodes = nodes.Where(n => n.Type == NodeType.End).ToList();

        void Visit(WorkflowNode node)
        {
            if (!visited.Add(node.Id)) return;
            ordered.Add(node);

            var nextNodes = connections
                .Where(c => c.FromNode.Id == node.Id)
                .Select(c => c.ToNode)
                .Where(n => !visited.Contains(n.Id))
                .ToList();

            foreach (var nxt in nextNodes)
            {
                Visit(nxt);
            }
        }

        foreach (var s in startNodes)
            Visit(s);

        foreach (var e in endNodes)
            Visit(e);

        foreach (var node in nodes)
            Visit(node);

        return ordered;
    }


    private void RestoreNodeProperties(WorkflowNode node, Dictionary<string, object> properties)
    {
        if (properties == null) return;

        foreach (var prop in properties)
        {
            var value = prop.Value?.ToString();
            if (value == null) continue;

            switch (prop.Key)
            {
                case "Condition": node.Condition = value; break;
                case "Key": node.Key = value; break;
                case "MouseEvent":
                    if (Enum.TryParse<MouseEventType>(value, out var me)) node.MouseEvent = me;
                    break;
                case "TargetElement": node.TargetElement = value; break;
                case "FlowScopeKey": node.FlowScopeKey = value; break;
                case "FloatingWidget":
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            var cfg = JsonSerializer.Deserialize<FloatingWidgetConfig>(value);
                            if (cfg != null) node.FloatingWidget = cfg;
                        }
                    }
                    catch { /* ignore malformed */ }
                    break;
                case "RepeatCount":
                    if (int.TryParse(value, out var rc))
                    {
                        if (node is KeyPressEventNode kp) kp.RepeatCount = rc;
                        else if (node is HotkeyPressEventNode hk) hk.RepeatCount = rc;
                    }
                    break;
            }
        }

        // Shared properties (all nodes)
        RestoreSharedNodeProperties(node, properties);

        switch (node)
        {
            case KeyPressEventNode keyPressNode:
                RestoreKeyPressEventNodeProperties(keyPressNode, properties);
                break;
            case HotkeyPressEventNode hotkeyPressNode:
                RestoreHotkeyPressEventNodeProperties(hotkeyPressNode, properties);
                break;
            case StringSplitNode stringSplitNode:
                RestoreStringSplitNodeProperties(stringSplitNode, properties);
                break;
            case LoopNode loop:
                RestoreLoopNodeProperties(loop, properties);
                break;
            case MouseEventNode mouseNode:
                RestoreMouseEventNodeProperties(mouseNode, properties);
                break;
            case ScreenPositionPickerNode pos:
                RestoreScreenPositionPickerNodeProperties(pos, properties);
                break;
            case ScreenCaptureNode cap:
                RestoreScreenCaptureNodeProperties(cap, properties);
                break;
            case TextScanNode textScan:
                RestoreTextScanNodeProperties(textScan, properties);
                break;
            case LoopBodyNode loopBody:
                RestoreLoopBodyNodeProperties(loopBody, properties);
                break;
            case AsyncTaskNode asyncTaskNode:
                RestoreAsyncTaskNodeProperties(asyncTaskNode, properties);
                break;
            case AsyncTaskBodyNode asyncTaskBodyPersist:
                RestoreAsyncTaskBodyNodeProperties(asyncTaskBodyPersist, properties);
                break;
            case EmbedApplicationNode embedApp:
                RestoreEmbedApplicationNodeProperties(embedApp, properties);
                break;
            case StorageNode storageNode:
                RestoreStorageNodeProperties(storageNode, properties);
                break;
            case KeyScopedNode keyScopedNode:
                RestoreKeyScopedNodeProperties(keyScopedNode, properties);
                break;
            case AsyncTaskDispatchCollectNode collectNode:
                RestoreAsyncTaskDispatchCollectNodeProperties(collectNode, properties);
                break;
            case WorkflowNode n when n.IsConditionalNode:
                RestoreConditionalNodeProperties(n, properties);
                break;
            case InputNode inputNode:
                RestoreInputNodeProperties(inputNode, properties);
                break;
            case DelayNode delayNode:
                RestoreDelayNodeProperties(delayNode, properties);
                break;
            case CallbackNode callbackNode:
                RestoreCallbackNodeProperties(callbackNode, properties);
                break;
            case ListOutNode listOutNode:
                RestoreListOutNodeProperties(listOutNode, properties);
                break;
            case AssignDataNode assignDataNode:
                RestoreAssignDataNodeProperties(assignDataNode, properties);
                break;
            case MediaGalleryNode mediaGalleryNode:
                RestoreMediaGalleryNodeProperties(mediaGalleryNode, properties);
                break;
            case ImageProcessingNode imageNode:
                RestoreImageProcessingNodeProperties(imageNode, properties);
                break;
            case VideoProcessingNode videoNode:
                RestoreVideoProcessingNodeProperties(videoNode, properties);
                break;
            case DataFetcherNode fetcherNode:
                RestoreDataFetcherNodeProperties(fetcherNode, properties);
                break;
            case WebNode webNode:
                RestoreWebNodeProperties(webNode, properties);
                break;
            case CodeNode codeNode:
                RestoreCodeNodeProperties(codeNode, properties);
                break;
            case FolderNode folderNode:
                RestoreFolderNodeProperties(folderNode, properties);
                break;
            case HtmlUiNode htmlUiNode:
                RestoreHtmlUiNodeProperties(htmlUiNode, properties);
                break;
            case ShowInputMsgNode showInputMsgNode:
                RestoreShowInputMsgNodeProperties(showInputMsgNode, properties);
                break;
            case DynamicUiNode dynamicUiNode:
                RestoreDynamicUiNodeProperties(dynamicUiNode, properties);
                break;
            case FileDownloadNode fdNode:
                RestoreFileDownloadNodeProperties(fdNode, properties);
                break;
            case FolderFilePathsNode ffpNode:
                RestoreFolderFilePathsNodeProperties(ffpNode, properties);
                break;
            case KeyValueBridgeNode kvNode:
                RestoreKeyValueBridgeNodeProperties(kvNode, properties);
                break;
            case FlowOverwriteNode flowOverwriteNode:
                RestoreFlowOverwriteNodeProperties(flowOverwriteNode, properties);
                break;
            case GitSourceNode gitSourceNode:
                RestoreGitSourceNodeProperties(gitSourceNode, properties);
                break;
            case BodyContainerNode bodyContainerNode:
                RestoreBodyContainerNodeProperties(bodyContainerNode, properties);
                break;
            case OutputNode outputNode:
                RestoreOutputNodeProperties(outputNode, properties);
                break;
            case MacroRecorderNode macroRecorderNode:
                RestoreMacroRecorderNodeProperties(macroRecorderNode, properties);
                break;
            case ActionCanVasNode actionCanVasNode:
                RestoreActionCanVasNodeProperties(actionCanVasNode, properties);
                break;
            case BorderHighlightNode borderHighlightNode:
                RestoreBorderHighlightNodeProperties(borderHighlightNode, properties);
                break;
            case NotificationNode notificationNode:
                RestoreNotificationNodeProperties(notificationNode, properties);
                break;
            case HttpRequestNode httpRequestNode:
                RestoreHttpRequestNodeProperties(httpRequestNode, properties);
                break;
        }

        // Shared: ReuseRoutes, DynamicInputs, Title (ÃƒÂ¡p dÃ¡Â»Â¥ng cho mÃ¡Â»Âi loÃ¡ÂºÂ¡i node)
        RestoreReuseRoutes(node, properties);
        RestoreDynamicInputProperties(node, properties);
        RestoreSharedTitleProperties(node, properties);
    }

    private static Dictionary<string, object> GetNodeProperties(WorkflowNode node)
    {
        var dict = new Dictionary<string, object>();

        // Shared header properties (all nodes)
        GetSharedHeaderProperties(node, dict);

        switch (node)
        {
            case KeyPressEventNode kp: GetKeyPressEventNodeProperties(kp, dict); break;
            case HotkeyPressEventNode hk: GetHotkeyPressEventNodeProperties(hk, dict); break;
            case StringSplitNode stringSplit: GetStringSplitNodeProperties(stringSplit, dict); break;
            case LoopNode loop: GetLoopNodeProperties(loop, dict); break;
            case MouseEventNode mouseNode: GetMouseEventNodeProperties(mouseNode, dict); break;
            case ScreenPositionPickerNode pos: GetScreenPositionPickerNodeProperties(pos, dict); break;
            case ScreenCaptureNode cap: GetScreenCaptureNodeProperties(cap, dict); break;
            case TextScanNode textScan: GetTextScanNodeProperties(textScan, dict); break;
            case LoopBodyNode loopBody: GetLoopBodyNodeProperties(loopBody, dict); break;
            case AsyncTaskBodyNode asyncTaskBodyNode: GetAsyncTaskBodyNodeProperties(asyncTaskBodyNode, dict); break;
            case AsyncTaskNode asyncTaskNode: GetAsyncTaskNodeProperties(asyncTaskNode, dict); break;
            case AsyncTaskDispatchCollectNode collectNode: GetAsyncTaskDispatchCollectNodeProperties(collectNode, dict); break;
            case WorkflowNode n when n.IsConditionalNode && n.ConditionalBranches != null && n.ConditionalBranches.Count > 0: GetConditionalNodeProperties(n, dict); break;
            case InputNode inputNode: GetInputNodeProperties(inputNode, dict); break;
            case DelayNode delayNode: GetDelayNodeProperties(delayNode, dict); break;
            case CallbackNode callbackNode: GetCallbackNodeProperties(callbackNode, dict); break;
            case ListOutNode listOutNode: GetListOutNodeProperties(listOutNode, dict); break;
            case AssignDataNode assignDataNode: GetAssignDataNodeProperties(assignDataNode, dict); break;
            case MediaGalleryNode mediaGalleryNode: GetMediaGalleryNodeProperties(mediaGalleryNode, dict); break;
            case ImageProcessingNode imageNode: GetImageProcessingNodeProperties(imageNode, dict); break;
            case VideoProcessingNode videoNode: GetVideoProcessingNodeProperties(videoNode, dict); break;
            case DataFetcherNode fetcherNode: GetDataFetcherNodeProperties(fetcherNode, dict); break;
            case FileDownloadNode fdNode: GetFileDownloadNodeProperties(fdNode, dict); break;
            case FolderFilePathsNode ffpNode: GetFolderFilePathsNodeProperties(ffpNode, dict); break;
            case KeyValueBridgeNode kvNode: GetKeyValueBridgeNodeProperties(kvNode, dict); break;
            case FlowOverwriteNode flowOverwriteNode: GetFlowOverwriteNodeProperties(flowOverwriteNode, dict); break;
            case GitSourceNode gitSourceNode: GetGitSourceNodeProperties(gitSourceNode, dict); break;
            case BodyContainerNode bodyContainerNode: GetBodyContainerNodeProperties(bodyContainerNode, dict); break;
            case WebNode webNode: GetWebNodeProperties(webNode, dict); break;
            case CodeNode codeNode: GetCodeNodeProperties(codeNode, dict); break;
            case FolderNode folderNode: GetFolderNodeProperties(folderNode, dict); break;
            case HtmlUiNode htmlUiNode: GetHtmlUiNodeProperties(htmlUiNode, dict); break;
            case ShowInputMsgNode showInputMsgNode: GetShowInputMsgNodeProperties(showInputMsgNode, dict); break;
            case DynamicUiNode dynamicUiNode: GetDynamicUiNodeProperties(dynamicUiNode, dict); break;
            case HttpRequestNode httpRequestNode: GetHttpRequestNodeProperties(httpRequestNode, dict); break;
            case OutputNode outputNode: GetOutputNodeProperties(outputNode, dict); break;
            case MacroRecorderNode macroNode: GetMacroRecorderNodeProperties(macroNode, dict); break;
            case ActionCanVasNode actionNode: GetActionCanVasNodeProperties(actionNode, dict); break;
            case BorderHighlightNode borderHighlightNode: GetBorderHighlightNodeProperties(borderHighlightNode, dict); break;
            case NotificationNode notificationNode: GetNotificationNodeProperties(notificationNode, dict); break;
            case EmbedApplicationNode embedApp: GetEmbedApplicationNodeProperties(embedApp, dict); break;
            case StorageNode storageNode: GetStorageNodeProperties(storageNode, dict); break;
            case KeyScopedNode keyScopedNode: GetKeyScopedNodeProperties(keyScopedNode, dict); break;
        }
        GetReuseRoutes(node, dict);
        GetSharedFooterProperties(node, dict);
        GetDynamicInputProperties(node, dict);
        GetSharedTitleProperties(node, dict);

        return dict;
    }

}
