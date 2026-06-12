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
    /// <summary>ThÆ° má»¥c con trong Documents khi lÆ°u workflow máº·c Ä‘á»‹nh (khÃ´ng phá»¥ thuá»™c thÆ° má»¥c cháº¡y / bin).</summary>
    public const string DefaultWorkflowJsonFolderName = "Workflow_Json";
    private const string FlowMyRootFolderName = "FlowMy";

    private readonly FlowMy.Workflow.TemplateFactory _templateFactory;
    private readonly string _workflowsDir;
    private static readonly ConcurrentDictionary<string, CachedWorkflowJson> _workflowJsonCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CachedWorkflowJson(DateTime LastWriteUtc, string Json);

    /// <summary>ÄÆ°á»ng dáº«n máº·c Ä‘á»‹nh: Documents\FlowMy\Workflow_Json; náº¿u khÃ´ng láº¥y Ä‘Æ°á»£c Documents thÃ¬ fallback cáº¡nh exe.</summary>
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
        string? connectionLineStyle = null)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("Workflow name is required", nameof(workflowName));

        try
        {
            if (!Directory.Exists(_workflowsDir))
                Directory.CreateDirectory(_workflowsDir);

            // Ctrl+S / Save button: lÆ°u Ä‘áº§y Ä‘á»§ logic (khÃ´ng runtime output)
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
                    MaxDepth = 64 // Giá»›i háº¡n Ä‘á»™ sÃ¢u Ä‘á»ƒ trÃ¡nh stack overflow
                };
                json = JsonSerializer.Serialize(dto, options);
            }
            catch (System.Text.Json.JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON serialization error: {ex.Message}\n{ex.StackTrace}");
                // Thá»­ láº¡i khÃ´ng serialize output values
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
            throw; // Re-throw Ä‘á»ƒ caller cÃ³ thá»ƒ xá»­ lÃ½
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
                            System.Diagnostics.Debug.WriteLine($"Load: giáº£i nÃ©n web bundle lá»—i: {ex.Message}");
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
    /// Export chá»‰ logic (nodes, connections, properties), khÃ´ng cÃ³ output/runtime.
    /// DÃ¹ng cho nÃºt Export vÃ  chia sáº» file.
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

        // âš ï¸ CRITICAL: KhÃ´ng lÆ°u output values cho InputNode vÃ  cÃ¡c node cÃ³ property trá»±c tiáº¿p
        // Ä‘á»ƒ trÃ¡nh tÃ¬nh tráº¡ng giÃ¡ trá»‹ cÅ© (tá»« execution) override giÃ¡ trá»‹ má»›i (tá»« user edit)
        if (node is InputNode)
        {
            // InputNode cÃ³ property Value/ArrayValues mÃ  user cÃ³ thá»ƒ sá»­a trá»±c tiáº¿p
            // KhÃ´ng lÆ°u UserValueOverride Ä‘á»ƒ trÃ¡nh conflict vá»›i giÃ¡ trá»‹ má»›i
            return null;
        }

        // Äáº·c biá»‡t xá»­ lÃ½ WebNode: khÃ´ng serialize output values khi WebView2 Ä‘ang cháº¡y
        // vÃ¬ cÃ³ thá»ƒ cÃ³ cÃ¡c giÃ¡ trá»‹ lá»›n hoáº·c phá»©c táº¡p khÃ´ng thá»ƒ serialize
        if (node is WebNode)
        {
            // Bá» qua serialize output values cho WebNode Ä‘á»ƒ trÃ¡nh lá»—i
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
                if (string.IsNullOrWhiteSpace(value) || value == "â€”") continue;

                // Giá»›i háº¡n Ä‘á»™ dÃ i giÃ¡ trá»‹ Ä‘á»ƒ trÃ¡nh serialize quÃ¡ lá»›n (max 10KB per value)
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

                // ConditionalNode vÃ  AsyncTaskNode: restore branches trÆ°á»›c Ä‘á»ƒ cÃ³ Ä‘á»§ sá»‘ port trÆ°á»›c khi restore Port IDs
                if (node.IsConditionalNode || node is AsyncTaskNode)
                {
                    RestoreNodeProperties(node, nodeDto.Properties);
                }

                // Restore Ports (Id + Position) náº¿u workflow cÃ³ lÆ°u láº¡i cáº¥u hÃ¬nh port
                if (nodeDto.Ports != null && nodeDto.Ports.Any())
                {
                    foreach (var portDto in nodeDto.Ports)
                    {
                        if (!Enum.TryParse<PortPosition>(portDto.Position, out var pos))
                            continue;

                        NodePort? targetPort = null;

                        // ConditionalNode/AsyncTaskNode: match input port trá»±c tiáº¿p (chá»‰ cÃ³ 1 input port)
                        if (portDto.IsInput)
                        {
                            targetPort = node.Ports.FirstOrDefault(p => p.IsInput);
                        }
                        // ConditionalNode: match output port theo BranchIndex (file má»›i cÃ³ BranchIndex).
                        // Fallback sang Index Ä‘á»ƒ tÆ°Æ¡ng thÃ­ch file cÅ©, trÃ¡nh map nháº§m theo Position (vÃ¬ nhiá»u nhÃ¡nh cÃ¹ng Position).
                        else if (node.IsConditionalNode && node.ConditionalBranches != null)
                        {
                            int? bi = portDto.BranchIndex;
                            if (bi.HasValue && bi.Value >= 0 && bi.Value < node.ConditionalBranches.Count)
                                targetPort = node.ConditionalBranches[bi.Value].Port;
                            else if (portDto.Index >= 0 && portDto.Index < node.ConditionalBranches.Count)
                                targetPort = node.ConditionalBranches[portDto.Index].Port;
                        }
                        // AsyncTaskNode (manual): match output port theo BranchIndex (file má»›i)
                        else if (node is AsyncTaskNode atn && atn.UiPresentationMode == AsyncTaskUiPresentationMode.ManualBranches && atn.AsyncTaskBranches != null)
                        {
                            int? bi = portDto.BranchIndex;
                            if (bi.HasValue && bi.Value >= 0 && bi.Value < atn.AsyncTaskBranches.Count)
                                targetPort = atn.AsyncTaskBranches[bi.Value].Port;
                        }
                        // Fallback: match theo ID, Position, hoáº·c Index (cho node khÃ¡c hoáº·c file cÅ©)
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

                // RestoreNodeProperties Ä‘Ã£ gá»i á»Ÿ trÃªn cho Conditional/AsyncTask; vá»›i cÃ¡c node khÃ¡c gá»i á»Ÿ Ä‘Ã¢y
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
                    // âœ… Guard: Ä‘áº£m báº£o Width/Height há»£p lá»‡ trÃ¡nh lá»—i 'height must be non-negative' khi import
                    loopNode.LoopBodyNode.Width = Math.Max(100, importedBody.Width);
                    loopNode.LoopBodyNode.Height = Math.Max(80, importedBody.Height);

                    EnsureLoopBodyPortsExist(loopNode.LoopBodyNode);
                    EnsureLoopBodyPortsExist(importedBody);

                    CopyLoopBodyPortId(importedBody, loopNode.LoopBodyNode, "LoopBodyTop");
                    CopyLoopBodyPortId(importedBody, loopNode.LoopBodyNode, "LoopBodyLeft");
                    CopyLoopBodyPortId(importedBody, loopNode.LoopBodyNode, "LoopBodyRight");

                    nodeMap[link.ToNodeId] = loopNode.LoopBodyNode;
                }

                // âœ… Äáº£m báº£o LoopNode ports cÃ³ Ä‘Ãºng ID vÃ  Position sau khi restore
                // Äáº·c biá»‡t quan trá»ng cho LoopNodeBottom vÃ  LoopNodeOut
                var loopNodeDto = dto.Nodes.FirstOrDefault(n => n.Id == loopNode.Id);
                if (loopNodeDto?.Ports != null)
                {
                    foreach (var portDto in loopNodeDto.Ports)
                    {
                        if (!Enum.TryParse<PortPosition>(portDto.Position, out var pos))
                            continue;

                        // TÃ¬m port theo ID trÆ°á»›c
                        var existingPort = loopNode.Ports.FirstOrDefault(p => p.Id == portDto.Id);
                        if (existingPort != null)
                        {
                            existingPort.Position = pos;
                            continue;
                        }

                        // Náº¿u chÆ°a cÃ³, tÃ¬m port theo Position vÃ  Direction
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
                // âœ… Guard: Ä‘áº£m báº£o Width/Height há»£p lá»‡ trÃ¡nh lá»—i 'height must be non-negative' khi import
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

                    // âœ… Æ¯u tiÃªn match theo Port ID (chÃ­nh xÃ¡c nháº¥t)
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

                    // âœ… Náº¿u khÃ´ng tÃ¬m tháº¥y theo ID, chá»‰ fallback cho node cÃ³ 1 port out duy nháº¥t.
                    // LoopNode, ConditionalNode, AsyncTaskNode cÃ³ nhiá»u output ports - khÃ´ng fallback.
                    if (fromPort == null && !(fromNode is LoopNode) && !fromNode.IsConditionalNode && !(fromNode is AsyncTaskNode))
                    {
                        fromPort = fromNode.Ports.FirstOrDefault(p => !p.IsInput);
                    }

                    if (toPort == null && !(toNode is LoopNode) && !(toNode is LoopBodyNode) && !(toNode is AsyncTaskBodyNode))
                    {
                        toPort = toNode.Ports.FirstOrDefault(p => p.IsInput);
                    }

                    // âœ… Äá»‘i vá»›i LoopNode, ConditionalNode, AsyncTaskNode, LoopBodyNode: chá»‰ táº¡o connection náº¿u tÃ¬m tháº¥y Ä‘Ãºng port theo ID
                    if (fromNode is LoopNode || toNode is LoopNode || fromNode is LoopBodyNode || toNode is LoopBodyNode
                        || fromNode is AsyncTaskBodyNode || toNode is AsyncTaskBodyNode
                        || fromNode.IsConditionalNode || fromNode is AsyncTaskNode)
                    {
                        if (fromPort == null || toPort == null)
                        {
                            // Skip connection náº¿u khÃ´ng tÃ¬m tháº¥y Ä‘Ãºng port cho loop nodes
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

            // âœ… Rebuild LoopNode outputs tá»« ListOutNodes trong LoopBody
            // Pháº£i gá»i sau khi Ä‘Ã£ cÃ³ Ä‘áº§y Ä‘á»§ connections
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

        // KeyPressEventNode deserialization
        if (node is KeyPressEventNode keyPressNode)
        {
            RestoreKeyPressEventNodeProperties(keyPressNode, properties);
        }
        // HotkeyPressEventNode deserialization
        else if (node is HotkeyPressEventNode hotkeyPressNode)
        {
            RestoreHotkeyPressEventNodeProperties(hotkeyPressNode, properties);
        }
        // StringSplitNode deserialization
        else if (node is StringSplitNode stringSplitNode)
        {
            RestoreStringSplitNodeProperties(stringSplitNode, properties);
        }

        if (node is LoopNode loop)
        {
            RestoreLoopNodeProperties(loop, properties);
        }
        else if (node is MouseEventNode mouseNode)
        {
            RestoreMouseEventNodeProperties(mouseNode, properties);
        }
        else if (node is ScreenPositionPickerNode pos)
        {
            RestoreScreenPositionPickerNodeProperties(pos, properties);
        }
        else if (node is ScreenCaptureNode cap)
        {
            RestoreScreenCaptureNodeProperties(cap, properties);
        }
        else if (node is TextScanNode textScan)
        {
            RestoreTextScanNodeProperties(textScan, properties);
        }
        else if (node is LoopBodyNode loopBody)
        {
            RestoreLoopBodyNodeProperties(loopBody, properties);
        }
        else if (node is AsyncTaskNode asyncTaskNode)
        {
            RestoreAsyncTaskNodeProperties(asyncTaskNode, properties);
        }
        else if (node is AsyncTaskBodyNode asyncTaskBodyPersist)
        {
            RestoreAsyncTaskBodyNodeProperties(asyncTaskBodyPersist, properties);
        }
        else if (node is EmbedApplicationNode embedApp)
        {
            RestoreEmbedApplicationNodeProperties(embedApp, properties);
        }
        else if (node is StorageNode storageNode)
        {
            RestoreStorageNodeProperties(storageNode, properties);
        }
        else if (node is AsyncTaskDispatchCollectNode collectNode)
        {
            RestoreAsyncTaskDispatchCollectNodeProperties(collectNode, properties);
        }
        else if (node.IsConditionalNode)
        {
            RestoreConditionalNodeProperties(node, properties);
        }
        else if (node is InputNode inputNode)
        {
            RestoreInputNodeProperties(inputNode, properties);
        }
        else if (node is DelayNode delayNode)
        {
            RestoreDelayNodeProperties(delayNode, properties);
        }
        else if (node is CallbackNode callbackNode)
        {
            RestoreCallbackNodeProperties(callbackNode, properties);
        }
        else if (node is ListOutNode listOutNode)
        {
            RestoreListOutNodeProperties(listOutNode, properties);
        }
        else if (node is AssignDataNode assignDataNode)
        {
            RestoreAssignDataNodeProperties(assignDataNode, properties);
        }
        else if (node is MediaGalleryNode mediaGalleryNode)
        {
            RestoreMediaGalleryNodeProperties(mediaGalleryNode, properties);
        }
        else if (node is ImageProcessingNode imageNode)
        {
            RestoreImageProcessingNodeProperties(imageNode, properties);
        }
        else if (node is VideoProcessingNode videoNode)
        {
            RestoreVideoProcessingNodeProperties(videoNode, properties);
        }
        else if (node is DataFetcherNode fetcherNode)
        {
            RestoreDataFetcherNodeProperties(fetcherNode, properties);
        }
        else if (node is WebNode webNode)
        {
            RestoreWebNodeProperties(webNode, properties);
        }
        else if (node is CodeNode codeNode)
        {
            RestoreCodeNodeProperties(codeNode, properties);
        }
        else if (node is FolderNode folderNode)
        {
            RestoreFolderNodeProperties(folderNode, properties);
        }
        else if (node is HtmlUiNode htmlUiNode)
        {
            RestoreHtmlUiNodeProperties(htmlUiNode, properties);
        }
        else if (node is FileDownloadNode fdNode)
        {
            RestoreFileDownloadNodeProperties(fdNode, properties);
        }
        else if (node is FolderFilePathsNode ffpNode)
        {
            RestoreFolderFilePathsNodeProperties(ffpNode, properties);
        }
        else if (node is KeyValueBridgeNode kvNode)
        {
            RestoreKeyValueBridgeNodeProperties(kvNode, properties);
        }
        else if (node is FlowOverwriteNode flowOverwriteNode)
        {
            RestoreFlowOverwriteNodeProperties(flowOverwriteNode, properties);
        }
        else if (node is GitSourceNode gitSourceNode)
        {
            RestoreGitSourceNodeProperties(gitSourceNode, properties);
        }
        else if (node is BodyContainerNode bodyContainerNode)
        {
            RestoreBodyContainerNodeProperties(bodyContainerNode, properties);
        }
        else if (node is OutputNode outputNode)
        {
            RestoreOutputNodeProperties(outputNode, properties);
        }
        else if (node is MacroRecorderNode macroRecorderNode)
        {
            RestoreMacroRecorderNodeProperties(macroRecorderNode, properties);
        }
        else if (node is BorderHighlightNode borderHighlightNode)
        {
            RestoreBorderHighlightNodeProperties(borderHighlightNode, properties);
        }
        else if (node is NotificationNode notificationNode)
        {
            RestoreNotificationNodeProperties(notificationNode, properties);
        }
        else if (node is HttpRequestNode httpRequestNode)
        {
            RestoreHttpRequestNodeProperties(httpRequestNode, properties);
        }

        // Shared: ReuseRoutes, DynamicInputs, Title (Ã¡p dá»¥ng cho má»i loáº¡i node)
        RestoreReuseRoutes(node, properties);
        RestoreDynamicInputProperties(node, properties);
        RestoreSharedTitleProperties(node, properties);
    }

    private static Dictionary<string, object> GetNodeProperties(WorkflowNode node)
    {
        var dict = new Dictionary<string, object>();

        // Shared header properties (all nodes)
        GetSharedHeaderProperties(node, dict);

        // KeyPressEventNode serialization
        if (node is KeyPressEventNode kp)
        {
            GetKeyPressEventNodeProperties(kp, dict);
        }
        // HotkeyPressEventNode serialization
        else if (node is HotkeyPressEventNode hk)
        {
            GetHotkeyPressEventNodeProperties(hk, dict);
        }
        // StringSplitNode serialization
        else if (node is StringSplitNode stringSplit)
        {
            GetStringSplitNodeProperties(stringSplit, dict);
        }

        if (node is LoopNode loop)
        {
            GetLoopNodeProperties(loop, dict);
        }
        else if (node is MouseEventNode mouseNode)
        {
            GetMouseEventNodeProperties(mouseNode, dict);
        }
        else if (node is ScreenPositionPickerNode pos)
        {
            GetScreenPositionPickerNodeProperties(pos, dict);
        }
        else if (node is ScreenCaptureNode cap)
        {
            GetScreenCaptureNodeProperties(cap, dict);
        }
        else if (node is TextScanNode textScan)
        {
            GetTextScanNodeProperties(textScan, dict);
        }
        else if (node is LoopBodyNode loopBody)
        {
            GetLoopBodyNodeProperties(loopBody, dict);
        }
        else if (node is AsyncTaskBodyNode asyncTaskBodyNode)
        {
            GetAsyncTaskBodyNodeProperties(asyncTaskBodyNode, dict);
        }
        else if (node is AsyncTaskNode asyncTaskNode)
        {
            GetAsyncTaskNodeProperties(asyncTaskNode, dict);
        }
        else if (node is AsyncTaskDispatchCollectNode collectNode)
        {
            GetAsyncTaskDispatchCollectNodeProperties(collectNode, dict);
        }
        else if (node.IsConditionalNode && node.ConditionalBranches != null && node.ConditionalBranches.Count > 0)
        {
            GetConditionalNodeProperties(node, dict);
        }
        else if (node is InputNode inputNode)
        {
            GetInputNodeProperties(inputNode, dict);
        }
        else if (node is DelayNode delayNode)
        {
            GetDelayNodeProperties(delayNode, dict);
        }
        else if (node is CallbackNode callbackNode)
        {
            GetCallbackNodeProperties(callbackNode, dict);
        }
        else if (node is ListOutNode listOutNode)
        {
            GetListOutNodeProperties(listOutNode, dict);
        }
        else if (node is AssignDataNode assignDataNode)
        {
            GetAssignDataNodeProperties(assignDataNode, dict);
        }
        else if (node is MediaGalleryNode mediaGalleryNode)
        {
            GetMediaGalleryNodeProperties(mediaGalleryNode, dict);
        }
        else if (node is ImageProcessingNode imageNode)
        {
            GetImageProcessingNodeProperties(imageNode, dict);
        }
        else if (node is VideoProcessingNode videoNode)
        {
            GetVideoProcessingNodeProperties(videoNode, dict);
        }
        else if (node is DataFetcherNode fetcherNode)
        {
            GetDataFetcherNodeProperties(fetcherNode, dict);
        }
        else if (node is FileDownloadNode fdNode)
        {
            GetFileDownloadNodeProperties(fdNode, dict);
        }
        else if (node is FolderFilePathsNode ffpNode)
        {
            GetFolderFilePathsNodeProperties(ffpNode, dict);
        }
        else if (node is KeyValueBridgeNode kvNode)
        {
            GetKeyValueBridgeNodeProperties(kvNode, dict);
        }
        else if (node is FlowOverwriteNode flowOverwriteNode)
        {
            GetFlowOverwriteNodeProperties(flowOverwriteNode, dict);
        }
        else if (node is GitSourceNode gitSourceNode)
        {
            GetGitSourceNodeProperties(gitSourceNode, dict);
        }
        else if (node is BodyContainerNode bodyContainerNode)
        {
            GetBodyContainerNodeProperties(bodyContainerNode, dict);
        }
        else if (node is WebNode webNode)
        {
            GetWebNodeProperties(webNode, dict);
        }
        else if (node is CodeNode codeNode)
        {
            GetCodeNodeProperties(codeNode, dict);
        }
        else if (node is FolderNode folderNode)
        {
            GetFolderNodeProperties(folderNode, dict);
        }
        else if (node is HtmlUiNode htmlUiNode)
        {
            GetHtmlUiNodeProperties(htmlUiNode, dict);
        }
        else if (node is HttpRequestNode httpRequestNode)
        {
            GetHttpRequestNodeProperties(httpRequestNode, dict);
        }
        else if (node is OutputNode outputNode)
        {
            GetOutputNodeProperties(outputNode, dict);
        }
        else if (node is MacroRecorderNode macroNode)
        {
            GetMacroRecorderNodeProperties(macroNode, dict);
        }
        else if (node is BorderHighlightNode borderHighlightNode)
        {
            GetBorderHighlightNodeProperties(borderHighlightNode, dict);
        }
        else if (node is NotificationNode notificationNode)
        {
            GetNotificationNodeProperties(notificationNode, dict);
        }
        else if (node is EmbedApplicationNode embedApp)
        {
            GetEmbedApplicationNodeProperties(embedApp, dict);
        }
        else if (node is StorageNode storageNode)
        {
            GetStorageNodeProperties(storageNode, dict);
        }

        // Shared: ReuseRoutes, Footer, DynamicInputs, Title (áp dụng cho mọi loại node)
        GetReuseRoutes(node, dict);
        GetSharedFooterProperties(node, dict);
        GetDynamicInputProperties(node, dict);
        GetSharedTitleProperties(node, dict);

        return dict;
    }

}
