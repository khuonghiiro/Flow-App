// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
    /// <summary>Thư mục con trong Documents khi lưu workflow mặc định (không phụ thuộc thư mục chạy / bin).</summary>
    public const string DefaultWorkflowJsonFolderName = "Workflow_Json";
    private const string FlowMyRootFolderName = "FlowMy";

    private readonly FlowMy.Workflow.TemplateFactory _templateFactory;
    private readonly string _workflowsDir;
    private static readonly ConcurrentDictionary<string, CachedWorkflowJson> _workflowJsonCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CachedWorkflowJson(DateTime LastWriteUtc, string Json);

    /// <summary>Đường dẫn mặc định: Documents\FlowMy\Workflow_Json; nếu không lấy được Documents thì fallback cạnh exe.</summary>
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

            // Ctrl+S / Save button: lưu đầy đủ logic (không runtime output)
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
                    MaxDepth = 64 // Giới hạn độ sâu để tránh stack overflow
                };
                json = JsonSerializer.Serialize(dto, options);
            }
            catch (System.Text.Json.JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON serialization error: {ex.Message}\n{ex.StackTrace}");
                // Thử lại không serialize output values
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

            // NOTE: WebView2 cache copy (SaveWorkflowWebNodeCaches) chỉ chạy khi Export,
            // không chạy khi Save bình thường — cấu hình đã được lưu trong JSON.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving workflow: {ex.Message}\n{ex.StackTrace}");
            throw; 
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
                            System.Diagnostics.Debug.WriteLine($"Load: giải nén web bundle lỗi: {ex.Message}");
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
    /// Export chỉ logic (nodes, connections, properties), không có output/runtime.
    /// Dùng cho nút Export và chia sẻ file.
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

        foreach (var wNode in allNodes.OfType<WebNode>())
        {
            if (string.Equals(wNode.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(wNode.CustomCacheName) &&
                !string.Equals(wNode.CustomCacheName, "Shared", StringComparison.OrdinalIgnoreCase))
            {
                WebNodeCacheHelper.EnsureProfileExists(wNode.CustomCacheName);
            }
        }

        var dto = new WorkflowDto
        {
            Version = 2,
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

        // v2: Compact ports — bỏ hẳn nếu là standard 2-port layout (Input Left + Output Right)
        List<PortDto>? compactPorts = ports;
        if (ports.Count == 2
            && ports.Any(p => p.IsInput && p.Position == "Left")
            && ports.Any(p => !p.IsInput && p.Position == "Right")
            && ports.All(p => p.BranchIndex == null)
            && !(n.IsConditionalNode || n is AsyncTaskNode || n is LoopNode || n is LoopBodyNode || n is AsyncTaskBodyNode))
        {
            compactPorts = null; // null → auto-generate on load
        }

        // v2: Compact properties — null nếu rỗng
        var props = GetNodeProperties(n);
        if (props != null && props.Count == 0)
            props = null;

        return new NodeDto
        {
            Id = n.Id,
            Title = n.Title,
            X = n.X,
            Y = n.Y,
            Type = n.Type.ToString(),
            ColorKey = n.ColorKey,
            Properties = props,
            Ports = compactPorts,
            OutputValues = includeRuntimeOutput ? GetNodeOutputValues(n) : null
        };
    }

    private static Dictionary<string, string>? GetNodeOutputValues(WorkflowNode node)
    {
        if (node.DynamicOutputs == null || node.DynamicOutputs.Count == 0)
            return null;

        // ⚠️ CRITICAL: Không lưu output values cho InputNode và các node có property trực tiếp
        // để tránh tình trạng giá trị cũ (từ execution) override giá trị mới (từ user edit)
        if (node is InputNode)
        {
            // InputNode có property Value/ArrayValues mà user có thể sửa trực tiếp
            // Không lưu UserValueOverride để tránh conflict với giá trị mới
            return null;
        }

        // Đặc biệt xử lý WebNode: không serialize output values khi WebView2 đang chạy
        // vì có thể có các giá trị lớn hoặc phức tạp không thể serialize
        if (node is WebNode)
        {
            // Bỏ qua serialize output values cho WebNode để tránh lỗi
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
                if (string.IsNullOrWhiteSpace(value) || value == "—") continue;

                // Giới hạn độ dài giá trị để tránh serialize quá lớn (max 10KB per value)
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

            // v2: Auto-fill viewport nếu thiếu
            AutoFillViewportFromNodes(dto);

            // v2: Pre-pass: Auto-generate Node IDs nếu trống để sẵn sàng cho property/connection index lookup
            foreach (var nDto in dto.Nodes)
            {
                if (string.IsNullOrWhiteSpace(nDto.Id))
                    nDto.Id = $"Node_{nDto.Type}_{Guid.NewGuid()}";
            }

            // v2: Resolve index references trong Node Properties ("UrlSourceNodeId": "0" → actual Node ID)
            ResolvePropertyIndexReferences(dto);

            // 1. Recreate Nodes (including LoopBody placeholders)
            foreach (var nodeDto in dto.Nodes)
            {
                WorkflowNode node;
                var isLoopBodyDto = !string.IsNullOrEmpty(nodeDto.Id) &&
                                    nodeDto.Id.StartsWith("LoopBody_", StringComparison.OrdinalIgnoreCase);
                var isAsyncTaskBodyDto = !string.IsNullOrEmpty(nodeDto.Id) &&
                                         nodeDto.Id.StartsWith("AsyncTaskBody_", StringComparison.OrdinalIgnoreCase);

                // v2: Auto-generate Ports nếu thiếu
                if (nodeDto.Ports == null || nodeDto.Ports.Count == 0)
                    nodeDto.Ports = GenerateDefaultPorts(nodeDto.Type);
                else
                {
                    // Auto-fill Port IDs nếu trống
                    foreach (var p in nodeDto.Ports)
                    {
                        if (string.IsNullOrWhiteSpace(p.Id))
                            p.Id = Guid.NewGuid().ToString();
                    }
                }

                // v2: Properties null → empty dict
                nodeDto.Properties ??= new Dictionary<string, object>();

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

                // ConditionalNode và AsyncTaskNode: restore branches trước để có đủ số port trước khi restore Port IDs
                if (node.IsConditionalNode || node is AsyncTaskNode)
                {
                    RestoreNodeProperties(node, nodeDto.Properties);
                }

                // Restore Ports (Id + Position) nếu workflow có lưu lại cấu hình port
                if (nodeDto.Ports != null && nodeDto.Ports.Any())
                {
                    foreach (var portDto in nodeDto.Ports)
                    {
                        if (!Enum.TryParse<PortPosition>(portDto.Position, out var pos))
                            continue;

                        NodePort? targetPort = null;

                        // ConditionalNode/AsyncTaskNode: match input port trực tiếp (chỉ có 1 input port)
                        if (portDto.IsInput)
                        {
                            targetPort = node.Ports.FirstOrDefault(p => p.IsInput);
                        }
                        // ConditionalNode: match output port theo BranchIndex (file mới có BranchIndex).
                        // Fallback sang Index để tương thích file cũ, tránh map nhầm theo Position (vì nhiều nhánh cùng Position).
                        else if (node.IsConditionalNode && node.ConditionalBranches != null)
                        {
                            int? bi = portDto.BranchIndex;
                            if (bi.HasValue && bi.Value >= 0 && bi.Value < node.ConditionalBranches.Count)
                                targetPort = node.ConditionalBranches[bi.Value].Port;
                            else if (portDto.Index >= 0 && portDto.Index < node.ConditionalBranches.Count)
                                targetPort = node.ConditionalBranches[portDto.Index].Port;
                        }
                        // AsyncTaskNode (manual): match output port theo BranchIndex (file mới)
                        else if (node is AsyncTaskNode atn && atn.UiPresentationMode == AsyncTaskUiPresentationMode.ManualBranches && atn.AsyncTaskBranches != null)
                        {
                            int? bi = portDto.BranchIndex;
                            if (bi.HasValue && bi.Value >= 0 && bi.Value < atn.AsyncTaskBranches.Count)
                                targetPort = atn.AsyncTaskBranches[bi.Value].Port;
                        }
                        // Fallback: match theo ID, Position, hoặc Index (cho node khác hoặc file cũ)
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

                // RestoreNodeProperties đã gọi ở trên cho Conditional/AsyncTask; với các node khác gọi ở đây
                if (!node.IsConditionalNode && !(node is AsyncTaskNode))
                {
                    RestoreNodeProperties(node, nodeDto.Properties);
                }

                if (node.Type == NodeType.Start || node.Type == NodeType.End)
                {
                    FlowMy.Services.Rendering.NodeAppearanceHelper.SyncStartEndPortVisibility(node);
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
                    // ✅ Guard: đảm bảo Width/Height hợp lệ tránh lỗi 'height must be non-negative' khi import
                    loopNode.LoopBodyNode.Width = Math.Max(100, importedBody.Width);
                    loopNode.LoopBodyNode.Height = Math.Max(80, importedBody.Height);

                    EnsureLoopBodyPortsExist(loopNode.LoopBodyNode);
                    EnsureLoopBodyPortsExist(importedBody);

                    CopyLoopBodyPortId(importedBody, loopNode.LoopBodyNode, "LoopBodyTop");
                    CopyLoopBodyPortId(importedBody, loopNode.LoopBodyNode, "LoopBodyLeft");
                    CopyLoopBodyPortId(importedBody, loopNode.LoopBodyNode, "LoopBodyRight");

                    nodeMap[link.ToNodeId] = loopNode.LoopBodyNode;
                }

                // ✅ Đảm bảo LoopNode ports có đúng ID và Position sau khi restore
                // Đặc biệt quan trọng cho LoopNodeBottom và LoopNodeOut
                var loopNodeDto = dto.Nodes.FirstOrDefault(n => n.Id == loopNode.Id);
                if (loopNodeDto?.Ports != null)
                {
                    foreach (var portDto in loopNodeDto.Ports)
                    {
                        if (!Enum.TryParse<PortPosition>(portDto.Position, out var pos))
                            continue;

                        // Tìm port theo ID trước
                        var existingPort = loopNode.Ports.FirstOrDefault(p => p.Id == portDto.Id);
                        if (existingPort != null)
                        {
                            existingPort.Position = pos;
                            continue;
                        }

                        // Nếu chưa có, tìm port theo Position và Direction
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
                // ✅ Guard: đảm bảo Width/Height hợp lệ tránh lỗi 'height must be non-negative' khi import
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
                    // ✅ Ưu tiên match theo Port ID (chính xác nhất)
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

                    // ✅ Nếu không tìm thấy theo ID, chỉ fallback cho node có 1 port out duy nhất.
                    // LoopNode, ConditionalNode, AsyncTaskNode có nhiều output ports - không fallback.
                    if (fromPort == null && !(fromNode is LoopNode) && !fromNode.IsConditionalNode && !(fromNode is AsyncTaskNode))
                    {
                        fromPort = fromNode.Ports.FirstOrDefault(p => !p.IsInput);
                    }

                    if (toPort == null && !(toNode is LoopNode) && !(toNode is LoopBodyNode) && !(toNode is AsyncTaskBodyNode))
                    {
                        toPort = toNode.Ports.FirstOrDefault(p => p.IsInput);
                    }

                    // ✅ Đối với LoopNode, ConditionalNode, AsyncTaskNode, LoopBodyNode: chỉ tạo connection nếu tìm thấy đúng port theo ID
                    if (fromNode is LoopNode || toNode is LoopNode || fromNode is LoopBodyNode || toNode is LoopBodyNode
                        || fromNode is AsyncTaskBodyNode || toNode is AsyncTaskBodyNode
                        || fromNode.IsConditionalNode || fromNode is AsyncTaskNode)
                    {
                        if (fromPort == null || toPort == null)
                        {
                            // Skip connection nếu không tìm thấy đúng port cho loop nodes
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

            // ✅ Rebuild LoopNode outputs từ ListOutNodes trong LoopBody
            // Phải gọi sau khi đã có đầy đủ connections
            foreach (var loopNode in nodes.OfType<LoopNode>())
            {
                loopNode.RebuildOutputsFromLoopBody(connections, nodes);
            }

            foreach (var wNode in nodes.OfType<WebNode>())
            {
                if (string.Equals(wNode.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(wNode.CustomCacheName) &&
                    !string.Equals(wNode.CustomCacheName, "Shared", StringComparison.OrdinalIgnoreCase))
                {
                    WebNodeCacheHelper.EnsureProfileExists(wNode.CustomCacheName);
                }
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
            case VideoEditorNode videoEditorNode:
                RestoreVideoEditorNodeProperties(videoEditorNode, properties);
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

        // Shared: ReuseRoutes, DynamicInputs, Title (áp dụng cho mọi loại node)
        RestoreReuseRoutes(node, properties);
        RestoreDynamicInputProperties(node, properties);
        RestoreSharedTitleProperties(node, properties);
    }

    private static Dictionary<string, object>? GetNodeProperties(WorkflowNode node)
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
            case VideoEditorNode videoEditorNode: GetVideoEditorNodeProperties(videoEditorNode, dict); break;
        }
        GetReuseRoutes(node, dict);
        GetSharedFooterProperties(node, dict);
        GetDynamicInputProperties(node, dict);
        GetSharedTitleProperties(node, dict);

        return dict.Count == 0 ? null : dict;
    }

    // ===== v2 HELPER METHODS =====

    /// <summary>
    /// v2: Auto-fill viewport (PanX/PanY/Center/Zoom) từ bounding box của các nodes.
    /// Gọi khi các viewport fields thiếu hoặc = 0.
    /// </summary>
    private static void AutoFillViewportFromNodes(WorkflowDto dto)
    {
        if (dto.Nodes == null || dto.Nodes.Count == 0) return;

        var minX = dto.Nodes.Min(n => n.X);
        var maxX = dto.Nodes.Max(n => n.X);
        var minY = dto.Nodes.Min(n => n.Y);
        var maxY = dto.Nodes.Max(n => n.Y);

        var centerX = (minX + maxX) / 2 + 30; // +30 = nửa chiều rộng node trung bình
        var centerY = (minY + maxY) / 2 + 30;

        // Chỉ fill nếu thiếu hoặc = 0
        if (!dto.SavedViewportCenterX.HasValue || dto.SavedViewportCenterX == 0)
            dto.SavedViewportCenterX = centerX;
        if (!dto.SavedViewportCenterY.HasValue || dto.SavedViewportCenterY == 0)
            dto.SavedViewportCenterY = centerY;
        if (dto.SavedScreenWidth == null || dto.SavedScreenWidth == 0)
            dto.SavedScreenWidth = 1920;
        if (dto.SavedScreenHeight == null || dto.SavedScreenHeight == 0)
            dto.SavedScreenHeight = 1080;

        // Auto-fit zoom nếu chưa set hợp lệ
        if (dto.ZoomLevel <= 0 || dto.ZoomLevel > 3)
        {
            var spreadX = maxX - minX + 200;
            var spreadY = maxY - minY + 200;
            dto.ZoomLevel = Math.Clamp(
                Math.Min(1920.0 / Math.Max(spreadX, 1), 1080.0 / Math.Max(spreadY, 1)),
                0.3, 1.5);
        }

        // Auto-calculate Pan nếu cả hai = 0
        if (dto.PanX == 0 && dto.PanY == 0)
        {
            dto.PanX = -(dto.SavedViewportCenterX.Value * dto.ZoomLevel) + (dto.SavedScreenWidth ?? 1920) / 2.0;
            dto.PanY = -(dto.SavedViewportCenterY.Value * dto.ZoomLevel) + (dto.SavedScreenHeight ?? 1080) / 2.0;
        }
    }

    /// <summary>
    /// v2: Tạo ports mặc định cho node khi JSON thiếu Ports.
    /// InputNode chỉ có 1 Output Right, còn lại là 1 Input Left + 1 Output Right.
    /// </summary>
    private static List<PortDto> GenerateDefaultPorts(string nodeType)
    {
        // InputNode chỉ có 1 Output Right (không có Input)
        if (string.Equals(nodeType, "Input", StringComparison.OrdinalIgnoreCase))
            return new List<PortDto>
            {
                new PortDto { Id = Guid.NewGuid().ToString(), IsInput = false, Position = "Right" }
            };

        // Hầu hết node: 1 Input Left + 1 Output Right
        return new List<PortDto>
        {
            new PortDto { Id = Guid.NewGuid().ToString(), IsInput = true, Position = "Left" },
            new PortDto { Id = Guid.NewGuid().ToString(), IsInput = false, Position = "Right" }
        };
    }

    /// <summary>
    /// v2: Resolve connection references — nếu FromNodeId/ToNodeId là số nguyên ("0", "1"...),
    /// map sang Node ID thực theo thứ tự trong Nodes[].
    /// </summary>
    private static void ResolveConnectionIndexReferences(WorkflowDto dto, Dictionary<string, WorkflowNode> nodeMap)
    {
        if (dto.Connections == null || dto.Nodes == null) return;

        // Build index lookup: "0" → Nodes[0].Id, "1" → Nodes[1].Id ...
        var nodeIds = dto.Nodes.Select(n => n.Id).ToList();

        foreach (var conn in dto.Connections)
        {
            conn.FromNodeId = ResolveNodeReference(conn.FromNodeId, nodeIds);
            conn.ToNodeId = ResolveNodeReference(conn.ToNodeId, nodeIds);
        }
    }

    /// <summary>
    /// v2: Resolve property index references — nếu UrlSourceNodeId, SourceNodeId, DynIn_*_SrcNode...
    /// là số nguyên ("0", "1"...), map sang Node ID thực theo thứ tự trong Nodes[].
    /// </summary>
    private static void ResolvePropertyIndexReferences(WorkflowDto dto)
    {
        if (dto.Nodes == null) return;
        var nodeIds = dto.Nodes.Select(n => n.Id).ToList();

        foreach (var nodeDto in dto.Nodes)
        {
            if (nodeDto.Properties == null) continue;

            var keysToUpdate = new List<string>();
            foreach (var kv in nodeDto.Properties)
            {
                if (kv.Key.EndsWith("NodeId", StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.EndsWith("_SrcNode", StringComparison.OrdinalIgnoreCase))
                {
                    keysToUpdate.Add(kv.Key);
                }
            }

            foreach (var key in keysToUpdate)
            {
                if (nodeDto.Properties[key] is string val && !string.IsNullOrWhiteSpace(val))
                {
                    nodeDto.Properties[key] = ResolveNodeReference(val, nodeIds);
                }
                else if (nodeDto.Properties[key] is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    nodeDto.Properties[key] = ResolveNodeReference(je.GetString()!, nodeIds);
                }
            }
        }
    }

    /// <summary>
    /// Nếu reference là số nguyên ("0", "1"...), trả về Node ID tương ứng trong danh sách.
    /// Nếu là string (GUID), trả về nguyên.
    /// </summary>
    private static string ResolveNodeReference(string reference, List<string> nodeIds)
    {
        if (string.IsNullOrWhiteSpace(reference)) return reference;

        if (int.TryParse(reference.Trim(), out var index) && index >= 0 && index < nodeIds.Count)
        {
            return nodeIds[index];
        }

        return reference; // Đã là GUID string — giữ nguyên
    }

}
