using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Models.Persistence;
using FlowMy.Services.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed class FileWorkflowPersistenceService : IWorkflowPersistenceService
{
    /// <summary>Thư mục con trong Documents khi lưu workflow mặc định (không phụ thuộc thư mục chạy / bin).</summary>
    public const string DefaultWorkflowJsonFolderName = "Workflow_Json";

    private readonly FlowMy.Workflow.TemplateFactory _templateFactory;
    private readonly string _workflowsDir;

    /// <summary>Đường dẫn mặc định: Documents\Workflow_Json; nếu không lấy được Documents thì fallback cạnh exe.</summary>
    public static string GetDefaultWorkflowsDirectory()
    {
        try
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(docs))
                return Path.Combine(docs, DefaultWorkflowJsonFolderName);
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

            WebNodeCacheHelper.SaveWorkflowWebNodeCaches(_workflowsDir, workflowName, nodes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving workflow: {ex.Message}\n{ex.StackTrace}");
            throw; // Re-throw để caller có thể xử lý
        }
    }

    public WorkflowLoadResult? Load(string workflowName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workflowName)) return null;

            var path = Path.Combine(_workflowsDir, $"{workflowName}.json");
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
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
        string? connectionLineStyle = null,
        string? portableWebBundleFileName = null)
    {
        var dto = BuildWorkflowDto(
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
            connectionLineStyle,
            portableWebBundleFileName);
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
        string? portableWebBundleFileName = null)
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
                    if (fromNode is LoopBodyNode fromBody) EnsureLoopBodyPortsExist(fromBody);
                    if (toNode is LoopBodyNode toBody) EnsureLoopBodyPortsExist(toBody);
                    if (fromNode is AsyncTaskBodyNode fromAtBody) WorkflowExecutionService.EnsureAsyncTaskBodyPortsExist(fromAtBody);
                    if (toNode is AsyncTaskBodyNode toAtBody) WorkflowExecutionService.EnsureAsyncTaskBodyPortsExist(toAtBody);

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

    private static void EnsureLoopBodyPortsExist(LoopBodyNode bodyNode)
    {
        if (bodyNode.Ports.All(p => p.Id != "LoopBodyTop"))
        {
            bodyNode.Ports.Add(new NodePort
            {
                Id = "LoopBodyTop",
                IsInput = true,
                Position = PortPosition.Top,
                IsVisible = true,
                CanDeleteConnection = false
            });
        }

        if (bodyNode.Ports.All(p => p.Id != "LoopBodyLeft"))
        {
            bodyNode.Ports.Add(new NodePort
            {
                Id = "LoopBodyLeft",
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true
            });
        }

        if (bodyNode.Ports.All(p => p.Id != "LoopBodyRight"))
        {
            bodyNode.Ports.Add(new NodePort
            {
                Id = "LoopBodyRight",
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true
            });
        }
    }

    private static void CopyLoopBodyPortId(LoopBodyNode from, LoopBodyNode to, string portId)
    {
        var src = from.Ports.FirstOrDefault(p => p.Id == portId);
        var dst = to.Ports.FirstOrDefault(p => p.Id == portId);
        if (src == null || dst == null) return;
        dst.Id = src.Id;
    }

    private static void CopyBodyPortId(WorkflowNode from, WorkflowNode to, string portId)
    {
        var src = from.Ports.FirstOrDefault(p => p.Id == portId);
        var dst = to.Ports.FirstOrDefault(p => p.Id == portId);
        if (src == null || dst == null) return;
        dst.Id = src.Id;
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
                case "RepeatCount":
                    if (int.TryParse(value, out var rc))
                    {
                        if (node is KeyPressEventNode kp) kp.RepeatCount = rc;
                        else if (node is HotkeyPressEventNode hk) hk.RepeatCount = rc;
                    }
                    break;
            }
        }

        if (properties.TryGetValue("RunMode", out var runModeObj) &&
            Enum.TryParse<FlowRunMode>(runModeObj?.ToString(), out var parsedRunMode))
        {
            node.RunMode = parsedRunMode;
        }
        if (properties.TryGetValue("AutoRunIntervalValue", out var autoValObj) &&
            double.TryParse(autoValObj?.ToString(), out var autoVal))
        {
            node.AutoRunIntervalValue = autoVal;
        }
        if (properties.TryGetValue("AutoRunIntervalUnit", out var autoUnitObj) &&
            Enum.TryParse<AutoRunIntervalUnit>(autoUnitObj?.ToString(), out var autoUnit))
        {
            node.AutoRunIntervalUnit = autoUnit;
        }

        if (properties.TryGetValue("AutoScopeVisualPadding", out var aspObj) &&
            double.TryParse(aspObj?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var asp))
            node.AutoScopeVisualPadding = asp;
        if (properties.TryGetValue("AutoScopeFrameX", out var asfx) &&
            double.TryParse(asfx?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fx))
            node.AutoScopeFrameX = fx;
        if (properties.TryGetValue("AutoScopeFrameY", out var asfy) &&
            double.TryParse(asfy?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fy))
            node.AutoScopeFrameY = fy;
        if (properties.TryGetValue("AutoScopeFrameWidth", out var asfw) &&
            double.TryParse(asfw?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fw))
            node.AutoScopeFrameWidth = fw;
        if (properties.TryGetValue("AutoScopeFrameHeight", out var asfh) &&
            double.TryParse(asfh?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fh))
            node.AutoScopeFrameHeight = fh;

        if (properties.TryGetValue("EndBehavior", out var endBehaviorObj) &&
            Enum.TryParse<EndNodeBehavior>(endBehaviorObj?.ToString(), out var parsedEndBehavior))
        {
            node.EndBehavior = parsedEndBehavior;
        }

        if (properties.TryGetValue("DiamondSharpness", out var sharpObj) &&
            Enum.TryParse<DiamondSharpness>(sharpObj?.ToString(), out var parsedSharpness))
        {
            node.DiamondSharpness = parsedSharpness;
        }

        if (properties.TryGetValue("ConditionalVisualMode", out var conditionalVisualModeObj) &&
            Enum.TryParse<ConditionalVisualMode>(conditionalVisualModeObj?.ToString(), out var parsedConditionalVisualMode))
        {
            node.ConditionalVisualMode = parsedConditionalVisualMode;
        }

        // KeyPressEventNode deserialization
        if (node is KeyPressEventNode keyPressNode)
        {
            if (properties.TryGetValue("PressDelayMs", out var pdObj) && int.TryParse(pdObj?.ToString(), out var pd))
                keyPressNode.PressDelayMs = pd;
            // Deserialize TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                if (Enum.TryParse<TitleDisplayMode>(tdmObj?.ToString(), out var tdm))
                    keyPressNode.TitleDisplayMode = tdm;
            }
            // Deserialize TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                if (Enum.TryParse<TitleColorMode>(tcmObj?.ToString(), out var tcm))
                    keyPressNode.TitleColorMode = tcm;
            }
            // Deserialize TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                keyPressNode.TitleColorKey = tckObj?.ToString();
            }
        }
        // HotkeyPressEventNode deserialization
        else if (node is HotkeyPressEventNode hotkeyPressNode)
        {
            if (properties.TryGetValue("PressDelayMs", out var pdObj) && int.TryParse(pdObj?.ToString(), out var pd))
                hotkeyPressNode.PressDelayMs = pd;
            // Deserialize TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                if (Enum.TryParse<TitleDisplayMode>(tdmObj?.ToString(), out var tdm))
                    hotkeyPressNode.TitleDisplayMode = tdm;
            }
            // Deserialize TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                if (Enum.TryParse<TitleColorMode>(tcmObj?.ToString(), out var tcm))
                    hotkeyPressNode.TitleColorMode = tcm;
            }
            // Deserialize TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                hotkeyPressNode.TitleColorKey = tckObj?.ToString();
            }
        }
        // StringSplitNode deserialization
        else if (node is StringSplitNode stringSplitNode)
        {
            if (properties.TryGetValue("RegexPattern", out var regexObj))
                stringSplitNode.RegexPattern = regexObj?.ToString() ?? @"\r?\n";
            if (properties.TryGetValue("OutputKey", out var outputKeyObj))
                stringSplitNode.OutputKey = outputKeyObj?.ToString() ?? "ListItems";
            // Deserialize TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                if (Enum.TryParse<TitleDisplayMode>(tdmObj?.ToString(), out var tdm))
                    stringSplitNode.TitleDisplayMode = tdm;
            }
            // Deserialize TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                if (Enum.TryParse<TitleColorMode>(tcmObj?.ToString(), out var tcm))
                    stringSplitNode.TitleColorMode = tcm;
            }
            // Deserialize TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                stringSplitNode.TitleColorKey = tckObj?.ToString();
            }
        }

        if (node is LoopNode loop)
        {
            if (properties.TryGetValue("LoopType", out var typeObj))
                loop.LoopType = Enum.Parse<LoopType>(typeObj.ToString()!);
            if (properties.TryGetValue("RepeatCount", out var rc))
                loop.RepeatCount = int.Parse(rc.ToString()!);
            if (properties.TryGetValue("StartIndex", out var si))
                loop.StartIndex = int.Parse(si.ToString()!);
            if (properties.TryGetValue("EndIndex", out var ei))
                loop.EndIndex = int.Parse(ei.ToString()!);
            if (properties.TryGetValue("ArrayInputKey", out var aik))
                loop.ArrayInputKey = aik?.ToString() ?? "array";
            if (properties.TryGetValue("InputType", out var it))
            {
                if (Enum.TryParse<WorkflowDataType>(it?.ToString(), out var inputType))
                    loop.InputType = inputType;
            }
            // Deserialize TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                if (Enum.TryParse<TitleDisplayMode>(tdmObj?.ToString(), out var tdm))
                    loop.TitleDisplayMode = tdm;
            }
            // Deserialize TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                if (Enum.TryParse<TitleColorMode>(tcmObj?.ToString(), out var tcm))
                    loop.TitleColorMode = tcm;
            }
            // Deserialize TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                loop.TitleColorKey = tckObj?.ToString();
            }
            // CustomOutputMappings
            if (properties.TryGetValue("CustomOutputMappings", out var comObj) && comObj != null)
            {
                try
                {
                    var json = comObj is string s ? s : (comObj is System.Text.Json.JsonElement je ? je.GetString() : null);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<List<LoopCustomOutputMapping>>(json);
                        if (list != null) { loop.CustomOutputMappings.Clear(); foreach (var m in list) loop.CustomOutputMappings.Add(m); }
                    }
                }
                catch { }
            }
            // DataAssignments
            if (properties.TryGetValue("DataAssignments", out var daObj) && daObj != null)
            {
                try
                {
                    var json = daObj is string s ? s : (daObj is System.Text.Json.JsonElement je ? je.GetString() : null);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<List<LoopDataAssignment>>(json);
                        if (list != null) { loop.DataAssignments.Clear(); foreach (var a in list) loop.DataAssignments.Add(a); }
                    }
                }
                catch { }
            }
        }
        else if (node is MouseEventNode mouseNode)
        {
            if (properties.TryGetValue("MouseButton", out var btn))
                mouseNode.MouseButton = btn?.ToString() ?? "Left";

            if (properties.TryGetValue("RepeatCount", out var rep) && int.TryParse(rep?.ToString(), out var repVal))
                mouseNode.RepeatCount = repVal;

            if (properties.TryGetValue("HoldDuration", out var hold) && double.TryParse(hold?.ToString(), out var holdVal))
                mouseNode.HoldDuration = holdVal;

            if (properties.TryGetValue("ScrollSpeed", out var speed) && int.TryParse(speed?.ToString(), out var speedVal))
                mouseNode.ScrollSpeed = speedVal;

            // Deserialize TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                if (Enum.TryParse<TitleDisplayMode>(tdmObj?.ToString(), out var tdm))
                    mouseNode.TitleDisplayMode = tdm;
            }
            // Deserialize TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                if (Enum.TryParse<TitleColorMode>(tcmObj?.ToString(), out var tcm))
                    mouseNode.TitleColorMode = tcm;
            }
            // Deserialize TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                mouseNode.TitleColorKey = tckObj?.ToString();
            }
        }
        else if (node is ScreenPositionPickerNode pos)
        {
            if (properties.TryGetValue("X_Pos", out var x) && properties.TryGetValue("Y_Pos", out var y))
            {
                pos.SelectedPosition = new Point(double.Parse(x.ToString()!), double.Parse(y.ToString()!));
            }
            if (properties.TryGetValue("HasPosition", out var hp))
                pos.HasPosition = bool.Parse(hp.ToString()!);
        }
        else if (node is ScreenCaptureNode cap)
        {
            if (properties.TryGetValue("CaptureX", out var cx))
                cap.CaptureX = int.Parse(cx.ToString()!);
            if (properties.TryGetValue("CaptureY", out var cy))
                cap.CaptureY = int.Parse(cy.ToString()!);
            if (properties.TryGetValue("CaptureWidth", out var cw))
                cap.CaptureWidth = int.Parse(cw.ToString()!);
            if (properties.TryGetValue("CaptureHeight", out var ch))
                cap.CaptureHeight = int.Parse(ch.ToString()!);

            if (properties.TryGetValue("CapturedImageBase64", out var b64Obj))
            {
                var b64 = b64Obj?.ToString();
                var restored = TryDecodePngBase64ToBitmapImage(b64);
                if (restored != null)
                {
                    cap.CapturedImage = restored;
                }
            }
        }
        else if (node is LoopBodyNode loopBody)
        {
            if (properties.TryGetValue("Width", out var w) && double.TryParse(w.ToString(),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wVal))
            {
                // Đảm bảo Width luôn hợp lệ để tránh lỗi rendering (kể cả khi locale dùng dấu phẩy thập phân)
                loopBody.Width = Math.Max(100, wVal);
            }
            if (properties.TryGetValue("Height", out var h) && double.TryParse(h.ToString(),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hVal))
            {
                // Đảm bảo Height luôn hợp lệ để tránh lỗi rendering (kể cả khi locale dùng dấu phẩy thập phân)
                loopBody.Height = Math.Max(80, hVal);
            }
        }
        else if (node is AsyncTaskNode asyncTaskNode)
        {
            // Deserialize RunInParallel trước (cần dùng khi tạo port mới)
            if (properties.TryGetValue("RunInParallel", out var runInParallelObj))
            {
                if (bool.TryParse(runInParallelObj?.ToString(), out var runInParallel))
                {
                    asyncTaskNode.RunInParallel = runInParallel;
                }
            }

            if (properties.TryGetValue("UiPresentationMode", out var uimObj) &&
                Enum.TryParse<AsyncTaskUiPresentationMode>(uimObj?.ToString(), out var uim))
                asyncTaskNode.UiPresentationMode = uim;

            if (properties.TryGetValue("DispatchLoopType", out var dltObj) &&
                Enum.TryParse<LoopType>(dltObj?.ToString(), out var dlt))
                asyncTaskNode.DispatchLoopType = dlt;

            if (properties.TryGetValue("RepeatCount", out var atRcObj) && int.TryParse(atRcObj?.ToString(), out var atRc))
                asyncTaskNode.RepeatCount = atRc;
            if (properties.TryGetValue("StartIndex", out var atSiObj) && int.TryParse(atSiObj?.ToString(), out var atSi))
                asyncTaskNode.StartIndex = atSi;
            if (properties.TryGetValue("EndIndex", out var atEiObj) && int.TryParse(atEiObj?.ToString(), out var atEi))
                asyncTaskNode.EndIndex = atEi;
            if (properties.TryGetValue("ReadResultsInBody", out var inBodyObj) &&
                bool.TryParse(inBodyObj?.ToString(), out var inBody))
                asyncTaskNode.ReadResultsInBody = inBody;

            if (asyncTaskNode.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
                _templateFactory.ConfigureAsyncTaskLoopLikePorts(asyncTaskNode);

            // Deserialize AsyncTaskBranches (chế độ nhánh tay)
            if (asyncTaskNode.UiPresentationMode == AsyncTaskUiPresentationMode.ManualBranches &&
                properties.TryGetValue("AsyncTaskBranches", out var asyncBranchesObj))
            {
                List<Dictionary<string, object>>? branchList = null;
                if (asyncBranchesObj is string jsonStr)
                {
                    try { branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonStr); } catch { }
                }
                else if (asyncBranchesObj is JsonElement je)
                {
                    try
                    {
                        if (je.ValueKind == JsonValueKind.String)
                            branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(je.GetString() ?? "[]");
                        else if (je.ValueKind == JsonValueKind.Array)
                            branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(je.GetRawText());
                    }
                    catch { }
                }
                if (branchList != null && asyncTaskNode.AsyncTaskBranches != null && branchList.Count > 0)
                {
                    // Khi load, template chỉ có 1 task. Nếu đã lưu thêm task thì tạo đủ task trước khi restore.
                    var portPosition = asyncTaskNode.AsyncTaskBranches.FirstOrDefault(b => b.Port != null)?.Port?.Position ?? PortPosition.Right;
                    var executionMode = asyncTaskNode.RunInParallel ? PortExecutionMode.Parallel : PortExecutionMode.Sequential;
                    while (asyncTaskNode.AsyncTaskBranches.Count < branchList.Count)
                    {
                        var newBranch = new AsyncTaskBranch
                        {
                            Label = "Task",
                            CanRemove = true
                        };
                        var newPort = new NodePort
                        {
                            IsInput = false,
                            Position = portPosition,
                            IsVisible = true,
                            ExecutionMode = executionMode
                        };
                        newBranch.Port = newPort;
                        asyncTaskNode.Ports.Add(newPort);
                        asyncTaskNode.AsyncTaskBranches.Add(newBranch);
                    }
                    for (int i = 0; i < branchList.Count && i < asyncTaskNode.AsyncTaskBranches.Count; i++)
                    {
                        var d = branchList[i];
                        var branch = asyncTaskNode.AsyncTaskBranches[i];
                        if (d.TryGetValue("Id", out var v)) branch.Id = GetStringFromJsonValue(v) ?? branch.Id;
                        if (d.TryGetValue("Label", out v)) branch.Label = GetStringFromJsonValue(v) ?? branch.Label;
                        if (d.TryGetValue("CanRemove", out v) && bool.TryParse(GetStringFromJsonValue(v), out var cr)) branch.CanRemove = cr;
                    }

                    // Đồng bộ lại ExecutionMode/ExecutionOrder cho toàn bộ task ports
                    // theo RunInParallel đã restore để tránh workflow cũ hoặc template lệch mode.
                    var mode = asyncTaskNode.RunInParallel ? PortExecutionMode.Parallel : PortExecutionMode.Sequential;
                    var order = 0;
                    foreach (var b in asyncTaskNode.AsyncTaskBranches)
                    {
                        if (b.Port == null) continue;
                        b.Port.ExecutionMode = mode;
                        b.Port.ExecutionOrder = order++;
                    }
                }
            }
        }
        else if (node is AsyncTaskBodyNode asyncTaskBodyPersist)
        {
            if (properties.TryGetValue("Width", out var w) && double.TryParse(w.ToString(),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wVal))
                asyncTaskBodyPersist.Width = Math.Max(200, wVal);
            if (properties.TryGetValue("Height", out var h) && double.TryParse(h.ToString(),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hVal))
                asyncTaskBodyPersist.Height = Math.Max(200, hVal);
        }
        else if (node is StorageNode storageNode)
        {
            // TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var sTdmObj) &&
                Enum.TryParse<TitleDisplayMode>(sTdmObj?.ToString(), out var sTdm))
            {
                storageNode.TitleDisplayMode = sTdm;
            }

            // TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var sTcmObj) &&
                Enum.TryParse<TitleColorMode>(sTcmObj?.ToString(), out var sTcm))
            {
                storageNode.TitleColorMode = sTcm;
            }

            // TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var sTckObj))
            {
                storageNode.TitleColorKey = sTckObj?.ToString();
            }

            // StoredOutputs
            if (properties.TryGetValue("StoredOutputs", out var soObj) && soObj != null)
            {
                try
                {
                    storageNode.StoredOutputs.Clear();

                    if (soObj is JsonElement soJe && soJe.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in soJe.EnumerateObject())
                        {
                            storageNode.StoredOutputs[p.Name] = p.Value.ValueKind == JsonValueKind.Null
                                ? null
                                : p.Value.ToString();
                        }
                    }
                    else if (soObj is IDictionary<string, object?> soDict)
                    {
                        foreach (var kv in soDict)
                        {
                            storageNode.StoredOutputs[kv.Key] = kv.Value?.ToString();
                        }
                    }
                }
                catch
                {
                    // ignore – best effort
                }
            }

            // OutputKeys -> rebuild DynamicOutputs
            if (properties.TryGetValue("OutputKeys", out var okObj) && okObj != null)
            {
                try
                {
                    List<string>? keys = null;
                    if (okObj is JsonElement okJe)
                    {
                        if (okJe.ValueKind == JsonValueKind.Array)
                        {
                            keys = new List<string>();
                            foreach (var item in okJe.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    var s = item.GetString();
                                    if (!string.IsNullOrWhiteSpace(s))
                                        keys.Add(s);
                                }
                            }
                        }
                    }
                    else if (okObj is IEnumerable<string> keyEnum)
                    {
                        keys = keyEnum.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
                    }

                    if (keys != null && keys.Count > 0)
                    {
                        storageNode.DynamicOutputs.Clear();
                        foreach (var k in keys)
                        {
                            storageNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                            {
                                Key = k,
                                DisplayName = k,
                                IsMultiple = false,
                                OutputType = WorkflowDataType.String
                            });
                        }
                    }

                    // Sync StoredOutputs -> UserValueOverride
                    foreach (var kv in storageNode.StoredOutputs)
                    {
                        var output = storageNode.DynamicOutputs.FirstOrDefault(o =>
                            string.Equals(o.Key, kv.Key, StringComparison.OrdinalIgnoreCase));
                        if (output != null)
                        {
                            output.UserValueOverride = kv.Value;
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            // SourceNodeId / SourceOutputKey
            if (properties.TryGetValue("SourceNodeId", out var snObj))
            {
                var s = snObj?.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                    storageNode.SourceNodeId = s;
            }
            if (properties.TryGetValue("SourceOutputKey", out var sokObj))
            {
                var s = sokObj?.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                    storageNode.SourceOutputKey = s;
            }

            // IsInputMode
            if (properties.TryGetValue("IsInputMode", out var isInputModeObj))
            {
                if (isInputModeObj is bool isInputMode)
                {
                    storageNode.IsInputMode = isInputMode;
                }
                else if (bool.TryParse(isInputModeObj?.ToString(), out var parsed))
                {
                    storageNode.IsInputMode = parsed;
                }
            }

            // Update port visibility based on IsInputMode after loading
            foreach (var port in storageNode.Ports)
            {
                bool shouldShowPort = storageNode.IsInputMode 
                    ? port.IsInput  // IsInputMode = true: chỉ hiện port IN
                    : !port.IsInput; // IsInputMode = false: chỉ hiện port OUT
                port.IsVisible = shouldShowPort;
            }
        }
        else if (node is AsyncTaskDispatchCollectNode collectNode)
        {
            if (properties.TryGetValue("SourceBodyNodeId", out var sbniObj))
            {
                var s = sbniObj?.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                    collectNode.SourceBodyNodeId = s;
            }

            if (properties.TryGetValue("SourceOutputKey", out var sokObj))
            {
                var s = sokObj?.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                    collectNode.SourceOutputKey = s;
            }
        }
        else if (node.IsConditionalNode && properties.TryGetValue("ConditionalBranches", out var branchesObj))
        {
            List<Dictionary<string, object>>? branchList = null;
            if (branchesObj is string jsonStr)
            {
                try { branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonStr); } catch { }
            }
            else if (branchesObj is JsonElement je)
            {
                try
                {
                    if (je.ValueKind == JsonValueKind.String)
                        branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(je.GetString() ?? "[]");
                    else if (je.ValueKind == JsonValueKind.Array)
                        branchList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(je.GetRawText());
                }
                catch { }
            }
            if (branchList != null && node.ConditionalBranches != null && branchList.Count > 0)
            {
                // Khi load, template chỉ có if + else. Nếu đã lưu thêm "else if" thì tạo đủ nhánh trước khi restore.
                var portPosition = node.ConditionalBranches.FirstOrDefault(b => b.Port != null)?.Port?.Position ?? PortPosition.Right;
                while (node.ConditionalBranches.Count < branchList.Count)
                {
                    int elseIndex = node.ConditionalBranches.FindIndex(b => b.Label == "else");
                    if (elseIndex < 0) elseIndex = node.ConditionalBranches.Count;
                    var newBranch = new ConditionalBranch
                    {
                        Label = "else if",
                        Condition = "condition",
                        CanRemove = true
                    };
                    var newPort = new NodePort
                    {
                        IsInput = false,
                        Position = portPosition,
                        IsVisible = true,
                        ExecutionMode = PortExecutionMode.Sequential
                    };
                    newBranch.Port = newPort;
                    node.Ports.Add(newPort);
                    node.ConditionalBranches.Insert(elseIndex, newBranch);
                }
                for (int i = 0; i < branchList.Count && i < node.ConditionalBranches.Count; i++)
                {
                    var d = branchList[i];
                    var branch = node.ConditionalBranches[i];
                    if (d.TryGetValue("Label", out var v)) branch.Label = GetStringFromJsonValue(v) ?? branch.Label;
                    if (d.TryGetValue("DisplayTitle", out v)) branch.DisplayTitle = GetStringFromJsonValue(v);
                    if (d.TryGetValue("SatelliteOffsetX", out v))
                    {
                        var sx = GetStringFromJsonValue(v);
                        if (double.TryParse(sx, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedSx))
                            branch.SatelliteOffsetX = parsedSx;
                        else if (double.TryParse(sx, out parsedSx))
                            branch.SatelliteOffsetX = parsedSx;
                    }
                    if (d.TryGetValue("SatelliteOffsetY", out v))
                    {
                        var sy = GetStringFromJsonValue(v);
                        if (double.TryParse(sy, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedSy))
                            branch.SatelliteOffsetY = parsedSy;
                        else if (double.TryParse(sy, out parsedSy))
                            branch.SatelliteOffsetY = parsedSy;
                    }
                    if (d.TryGetValue("SatelliteInputPosition", out v) &&
                        Enum.TryParse<PortPosition>(GetStringFromJsonValue(v), out var satInPos))
                    {
                        branch.SatelliteInputPosition = satInPos;
                    }
                    if (d.TryGetValue("LeftSourceNodeId", out v)) branch.LeftSourceNodeId = GetStringFromJsonValue(v);
                    if (d.TryGetValue("LeftKey", out v)) branch.LeftKey = GetStringFromJsonValue(v);
                    if (d.TryGetValue("Operator", out v) && Enum.TryParse<ConditionOperator>(GetStringFromJsonValue(v), out var op)) branch.Operator = op;
                    if (d.TryGetValue("RightUseLiteralValue", out v) && bool.TryParse(GetStringFromJsonValue(v), out var ruv)) branch.RightUseLiteralValue = ruv;
                    if (d.TryGetValue("RightLiteralValue", out v)) branch.RightLiteralValue = GetStringFromJsonValue(v);
                    if (d.TryGetValue("RightSourceNodeId", out v)) branch.RightSourceNodeId = GetStringFromJsonValue(v);
                    if (d.TryGetValue("RightKey", out v)) branch.RightKey = GetStringFromJsonValue(v);
                    if (d.TryGetValue("Condition", out v)) branch.Condition = GetStringFromJsonValue(v);
                    if (d.TryGetValue("CanRemove", out v) && bool.TryParse(GetStringFromJsonValue(v), out var cr)) branch.CanRemove = cr;
                    if (d.TryGetValue("SubConditions", out v) && v is JsonElement se)
                    {
                        try
                        {
                            var list = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(se.GetRawText());
                            if (list != null && list.Count > 0)
                            {
                                branch.SubConditions = list.Select(x =>
                                {
                                    var expr = new ConditionExpression();
                                    if (x.TryGetValue("LeftSourceNodeId", out var vx)) expr.LeftSourceNodeId = GetStringFromJsonValue(vx);
                                    if (x.TryGetValue("LeftKey", out vx)) expr.LeftKey = GetStringFromJsonValue(vx);
                                    if (x.TryGetValue("Operator", out vx) && Enum.TryParse<ConditionOperator>(GetStringFromJsonValue(vx), out var opx)) expr.Operator = opx;
                                    if (x.TryGetValue("RightUseLiteralValue", out vx) && bool.TryParse(GetStringFromJsonValue(vx), out var ruv)) expr.RightUseLiteralValue = ruv;
                                    if (x.TryGetValue("RightLiteralValue", out vx)) expr.RightLiteralValue = GetStringFromJsonValue(vx);
                                    if (x.TryGetValue("RightSourceNodeId", out vx)) expr.RightSourceNodeId = GetStringFromJsonValue(vx);
                                    if (x.TryGetValue("RightKey", out vx)) expr.RightKey = GetStringFromJsonValue(vx);
                                    return expr;
                                }).ToList();
                            }
                        }
                        catch { }
                    }
                    if (d.TryGetValue("OperatorsBetween", out v) && v is JsonElement oe)
                    {
                        try
                        {
                            var list = JsonSerializer.Deserialize<List<string>>(oe.GetRawText());
                            if (list != null && list.Count > 0)
                            {
                                branch.OperatorsBetween = list.Select(s => Enum.TryParse<LogicalOperator>(s, out var lop) ? lop : LogicalOperator.And).ToList();
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        else if (node is InputNode inputNode)
        {
            if (properties.TryGetValue("InputKey", out var keyObj))
                inputNode.Key = keyObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("InputValue", out var valueObj))
                inputNode.Value = valueObj?.ToString() ?? string.Empty;

            if (!properties.TryGetValue("InputDataType", out var typeObj))
            {
                properties.TryGetValue("WorkflowDataType", out typeObj);
            }

            if (typeObj != null)
            {
                var typeStr = typeObj.ToString();
                if (!string.IsNullOrWhiteSpace(typeStr) &&
                    Enum.TryParse<WorkflowDataType>(typeStr, out var parsedType))
                {
                    inputNode.DataType = parsedType;
                }
            }

            // Ensure DynamicOutputs is initialized for InputNode so that downstream
            // dialogs (WebNode, CodeNode, etc.) can list this node as a data source.
            // Older workflows may not have DynamicOutputs serialized.
            if (inputNode.DynamicOutputs == null || inputNode.DynamicOutputs.Count == 0)
            {
                var outputKey = string.IsNullOrWhiteSpace(inputNode.Key) ? "Input" : inputNode.Key;
                inputNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = outputKey,
                    DisplayName = "Value",
                    IsMultiple = false,
                    OutputType = inputNode.DataType,
                    ConvertType = inputNode.DataType
                });
            }

            if (properties.TryGetValue("InputArrayValues", out var arrayValuesObj))
            {
                List<string>? parsedArray = null;

                if (arrayValuesObj is string jsonArray)
                {
                    try
                    {
                        parsedArray = JsonSerializer.Deserialize<List<string>>(jsonArray);
                    }
                    catch { }
                }
                else if (arrayValuesObj is JsonElement jsonElement)
                {
                    try
                    {
                        if (jsonElement.ValueKind == JsonValueKind.String)
                        {
                            var jsonString = jsonElement.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonString))
                            {
                                parsedArray = JsonSerializer.Deserialize<List<string>>(jsonString);
                            }
                        }
                        else if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            parsedArray = JsonSerializer.Deserialize<List<string>>(jsonElement.GetRawText());
                        }
                    }
                    catch { }
                }
                else if (arrayValuesObj is List<object> list)
                {
                    parsedArray = list.Select(x => x?.ToString() ?? string.Empty).ToList();
                }

                if (parsedArray != null && inputNode.IsArrayType)
                {
                    inputNode.ArrayValues = parsedArray;
                }
            }

            // Deserialize TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                if (Enum.TryParse<TitleDisplayMode>(tdmObj?.ToString(), out var tdm))
                    inputNode.TitleDisplayMode = tdm;
            }
            // Deserialize TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                if (Enum.TryParse<TitleColorMode>(tcmObj?.ToString(), out var tcm))
                    inputNode.TitleColorMode = tcm;
            }
            // Deserialize TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                inputNode.TitleColorKey = tckObj?.ToString();
            }
        }
        else if (node is DelayNode delayNode)
        {
            if (properties.TryGetValue("DelayMilliseconds", out var delayObj) &&
                int.TryParse(delayObj?.ToString(), out var delayMs))
            {
                delayNode.DelayMilliseconds = delayMs;
            }

            // UI display settings (optional - older workflows may not have these)
            if (properties.TryGetValue("DelayUnit", out var unitObj))
            {
                var unitStr = unitObj?.ToString();
                if (!string.IsNullOrWhiteSpace(unitStr) &&
                    Enum.TryParse<DelayTimeUnit>(unitStr, out var parsedUnit))
                {
                    delayNode.DelayUnit = parsedUnit;
                }
            }

            if (properties.TryGetValue("DelayValue", out var valObj))
            {
                var valStr = valObj?.ToString();
                if (!string.IsNullOrWhiteSpace(valStr) &&
                    double.TryParse(valStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.CurrentCulture, out var parsedVal))
                {
                    delayNode.DelayValue = parsedVal;
                }
            }
            else
            {
                // Fallback: derive display value from milliseconds with current unit (default = seconds)
                var multiplier = delayNode.DelayUnit switch
                {
                    DelayTimeUnit.Milliseconds => 1d,
                    DelayTimeUnit.Seconds => 1000d,
                    DelayTimeUnit.Minutes => 60_000d,
                    DelayTimeUnit.Hours => 3_600_000d,
                    _ => 1000d
                };
                delayNode.DelayValue = multiplier <= 0 ? 0 : delayNode.DelayMilliseconds / multiplier;
            }

            if (properties.TryGetValue("TitleColorMode", out var tcmObj) &&
                tcmObj != null &&
                Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
            {
                delayNode.TitleColorMode = tcm;
            }

            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                delayNode.TitleColorKey = tckObj?.ToString();
            }

            if (properties.TryGetValue("TitleDisplayMode", out var tdmDelayObj) &&
                Enum.TryParse<TitleDisplayMode>(tdmDelayObj?.ToString(), out var tdmDelay))
            {
                delayNode.TitleDisplayMode = tdmDelay;
            }

            if (properties.TryGetValue("TimingMode", out var tmObj) &&
                Enum.TryParse<DelayTimingMode>(tmObj?.ToString(), out var tm))
            {
                delayNode.TimingMode = tm;
            }

            if (properties.TryGetValue("RandomMinValue", out var rminObj) &&
                double.TryParse(rminObj?.ToString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var rmin))
            {
                delayNode.RandomMinValue = rmin;
            }

            if (properties.TryGetValue("RandomMaxValue", out var rmaxObj) &&
                double.TryParse(rmaxObj?.ToString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var rmax))
            {
                delayNode.RandomMaxValue = rmax;
            }

            if (properties.TryGetValue("DelaySourceNodeId", out var dsnObj))
                delayNode.DelaySourceNodeId = dsnObj?.ToString() ?? string.Empty;

            if (properties.TryGetValue("DelaySourceOutputKey", out var dskObj))
                delayNode.DelaySourceOutputKey = dskObj?.ToString() ?? string.Empty;
        }
        else if (node is CallbackNode callbackNode)
        {
            if (properties.TryGetValue("TargetNodeId", out var targetObj))
            {
                callbackNode.TargetNodeId = targetObj?.ToString() ?? string.Empty;
            }

            if (properties.TryGetValue("MaxCallbackCount", out var maxCountObj) &&
                int.TryParse(maxCountObj?.ToString(), out var maxCount))
            {
                callbackNode.MaxCallbackCount = maxCount;
            }

            if (properties.TryGetValue("TitleDisplayMode", out var tdmCallbackObj))
            {
                var tdmStr = tdmCallbackObj?.ToString();
                if (!string.IsNullOrWhiteSpace(tdmStr) &&
                    Enum.TryParse<TitleDisplayMode>(tdmStr, out var parsedTdm))
                {
                    callbackNode.TitleDisplayMode = parsedTdm;
                }
            }

            if (properties.TryGetValue("TitleColorMode", out var tcmCallbackObj) &&
                tcmCallbackObj != null &&
                Enum.TryParse<TitleColorMode>(tcmCallbackObj.ToString(), out var tcmCallback))
            {
                callbackNode.TitleColorMode = tcmCallback;
            }

            if (properties.TryGetValue("TitleColorKey", out var tckCallbackObj))
            {
                callbackNode.TitleColorKey = tckCallbackObj?.ToString();
            }

            if (properties.TryGetValue("FlowBehavior", out var flowBehaviorObj))
            {
                var flowBehaviorStr = flowBehaviorObj?.ToString();
                if (!string.IsNullOrWhiteSpace(flowBehaviorStr) &&
                    Enum.TryParse<CallbackFlowBehavior>(flowBehaviorStr, out var parsedBehavior))
                {
                    callbackNode.FlowBehavior = parsedBehavior;
                }
            }

            callbackNode.SyncPortsForBehavior();
        }
        else if (node is ListOutNode listOutNode)
        {
            // Deserialize OutputMappings
            if (properties.TryGetValue("OutputMappings", out var mappingsObj))
            {
                List<OutputMapping>? parsedMappings = null;

                if (mappingsObj is string jsonMappings)
                {
                    try
                    {
                        var mappingData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonMappings);
                        if (mappingData != null)
                        {
                            parsedMappings = mappingData.Select(m => new OutputMapping
                            {
                                NewKey = m.TryGetValue("NewKey", out var nk) ? nk?.ToString() ?? string.Empty : string.Empty,
                                SourceNodeId = m.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                                SourceOutputKey = m.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                            }).ToList();
                        }
                    }
                    catch
                    {
                        // Try alternative format (array of objects)
                        try
                        {
                            var mappingData = JsonSerializer.Deserialize<List<OutputMapping>>(jsonMappings);
                            parsedMappings = mappingData;
                        }
                        catch { }
                    }
                }
                else if (mappingsObj is JsonElement jsonElement)
                {
                    try
                    {
                        if (jsonElement.ValueKind == JsonValueKind.String)
                        {
                            var jsonString = jsonElement.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonString))
                            {
                                var mappingData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonString);
                                if (mappingData != null)
                                {
                                    parsedMappings = mappingData.Select(m => new OutputMapping
                                    {
                                        NewKey = m.TryGetValue("NewKey", out var nk) ? nk?.ToString() ?? string.Empty : string.Empty,
                                        SourceNodeId = m.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                                        SourceOutputKey = m.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                                    }).ToList();
                                }
                            }
                        }
                        else if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            parsedMappings = JsonSerializer.Deserialize<List<OutputMapping>>(jsonElement.GetRawText());
                        }
                    }
                    catch { }
                }

                if (parsedMappings != null)
                {
                    listOutNode.OutputMappings = parsedMappings;
                    listOutNode.RebuildDynamicOutputs();
                }
            }

            // Deserialize TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                var tdmStr = tdmObj?.ToString();
                if (!string.IsNullOrWhiteSpace(tdmStr) &&
                    Enum.TryParse<TitleDisplayMode>(tdmStr, out var titleDisplayMode))
                {
                    listOutNode.TitleDisplayMode = titleDisplayMode;
                }
            }

            // Deserialize TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                if (Enum.TryParse<TitleColorMode>(tcmObj?.ToString(), out var tcm))
                    listOutNode.TitleColorMode = tcm;
            }

            // Deserialize TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                listOutNode.TitleColorKey = tckObj?.ToString();
            }
        }
        else if (node is AssignDataNode assignDataNode)
        {
            if (properties.TryGetValue("Assignments", out var assignObj) && assignObj != null)
            {
                try
                {
                    var json = assignObj is string s ? s : (assignObj is JsonElement je ? je.GetString() : null);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = JsonSerializer.Deserialize<List<AssignDataAssignment>>(json);
                        if (list != null)
                        {
                            assignDataNode.Assignments.Clear();
                            foreach (var a in list) assignDataNode.Assignments.Add(a);
                        }
                    }
                }
                catch { }
            }
            if (properties.TryGetValue("TitleColorMode", out var tcmObj) && tcmObj != null
                && Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
                assignDataNode.TitleColorMode = tcm;
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
                assignDataNode.TitleColorKey = tckObj?.ToString();
        }
        else if (node is MediaGalleryNode mediaGalleryNode)
        {
            if (properties.TryGetValue("Width", out var wObj) && wObj != null && double.TryParse(wObj.ToString(), out var w) && w >= 200)
                mediaGalleryNode.Width = w;
            if (properties.TryGetValue("Height", out var hObj) && hObj != null && double.TryParse(hObj.ToString(), out var h) && h >= 180)
                mediaGalleryNode.Height = h;
            if (properties.TryGetValue("FrameDisplayWidth", out var fdwObj) && fdwObj != null && double.TryParse(fdwObj.ToString(), out var fdw) && fdw >= 60)
                mediaGalleryNode.FrameDisplayWidth = fdw;
            if (properties.TryGetValue("FrameDisplayHeight", out var fdhObj) && fdhObj != null && double.TryParse(fdhObj.ToString(), out var fdh) && fdh >= 40)
                mediaGalleryNode.FrameDisplayHeight = fdh;
            if (properties.TryGetValue("TitleKeyTemplate", out var tktObj))
                mediaGalleryNode.TitleKeyTemplate = tktObj?.ToString() ?? "";
            if (properties.TryGetValue("ImageUrlKeyTemplate", out var iukObj))
                mediaGalleryNode.ImageUrlKeyTemplate = iukObj?.ToString() ?? "";
            if (properties.TryGetValue("VideoUrlKeyTemplate", out var vukObj))
                mediaGalleryNode.VideoUrlKeyTemplate = vukObj?.ToString() ?? "";
            if (properties.TryGetValue("GroupArrayKey", out var gakObj))
                mediaGalleryNode.GroupArrayKey = gakObj?.ToString() ?? "";
            if (properties.TryGetValue("GroupTitleKey", out var gtkObj))
                mediaGalleryNode.GroupTitleKey = gtkObj?.ToString() ?? "";
            if (properties.TryGetValue("GroupItemsKey", out var gikObj))
                mediaGalleryNode.GroupItemsKey = gikObj?.ToString() ?? "";
            if (properties.TryGetValue("FolderSaveImages", out var fsiObj))
                mediaGalleryNode.FolderSaveImages = fsiObj?.ToString() ?? "";
            if (properties.TryGetValue("FolderSourceNodeId", out var fsidObj))
                mediaGalleryNode.FolderSourceNodeId = fsidObj?.ToString();
            if (properties.TryGetValue("FolderSourceOutputKey", out var fsokObj))
                mediaGalleryNode.FolderSourceOutputKey = fsokObj?.ToString();
            if (properties.TryGetValue("FolderSaveVideos", out var fsvObj))
                mediaGalleryNode.FolderSaveVideos = fsvObj?.ToString() ?? "";
            if (properties.TryGetValue("FolderSourceNodeIdVideo", out var fsvidObj))
                mediaGalleryNode.FolderSourceNodeIdVideo = fsvidObj?.ToString();
            if (properties.TryGetValue("FolderSourceOutputKeyVideo", out var fsvokObj))
                mediaGalleryNode.FolderSourceOutputKeyVideo = fsvokObj?.ToString();
            if (properties.TryGetValue("JsonSourceNodeId", out var jsidObj))
                mediaGalleryNode.JsonSourceNodeId = jsidObj?.ToString();
            if (properties.TryGetValue("JsonSourceOutputKey", out var jsokObj))
                mediaGalleryNode.JsonSourceOutputKey = jsokObj?.ToString();
            if (properties.TryGetValue("ItemClickPreviewMode", out var icpmObj) && icpmObj != null && Enum.TryParse<ItemClickPreviewMode>(icpmObj.ToString(), out var icpm))
                mediaGalleryNode.ItemClickPreviewMode = icpm;
            if (properties.TryGetValue("DisplayMode", out var dmObj) && dmObj != null && Enum.TryParse<GalleryDisplayMode>(dmObj.ToString(), out var dm))
                mediaGalleryNode.DisplayMode = dm;
            if (properties.TryGetValue("TitleColorMode", out var tcmObj) && tcmObj != null && Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
                mediaGalleryNode.TitleColorMode = tcm;
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
                mediaGalleryNode.TitleColorKey = tckObj?.ToString();
            if (properties.TryGetValue("CanReexecuteSourceNode", out var crsnObj) && crsnObj != null &&
                bool.TryParse(crsnObj.ToString(), out var crsn))
                mediaGalleryNode.CanReexecuteSourceNode = crsn;
        }
        else if (node is ImageProcessingNode imageNode)
        {
            if (properties.TryGetValue("Width", out var wObj) && wObj != null && double.TryParse(wObj.ToString(), out var w) && w >= 260)
                imageNode.Width = w;
            if (properties.TryGetValue("Height", out var hObj) && hObj != null && double.TryParse(hObj.ToString(), out var h) && h >= 200)
                imageNode.Height = h;

            if (properties.TryGetValue("InputMode", out var imObj) && imObj != null &&
                Enum.TryParse<ImageInputMode>(imObj.ToString(), out var im))
                imageNode.InputMode = im;

            if (properties.TryGetValue("CropMode", out var cmObj) && cmObj != null &&
                Enum.TryParse<ImageCropMode>(cmObj.ToString(), out var cropM))
                imageNode.CropMode = cropM;

            if (properties.TryGetValue("ImageUrl", out var urlObj))
                imageNode.ImageUrl = urlObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ImageUrlSourceNodeId", out var usnObj))
                imageNode.ImageUrlSourceNodeId = usnObj?.ToString();
            if (properties.TryGetValue("ImageUrlSourceOutputKey", out var uskObj))
                imageNode.ImageUrlSourceOutputKey = uskObj?.ToString();

            if (properties.TryGetValue("ImageBase64", out var b64Obj))
                imageNode.ImageBase64 = b64Obj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ImageBase64SourceNodeId", out var bsnObj))
                imageNode.ImageBase64SourceNodeId = bsnObj?.ToString();
            if (properties.TryGetValue("ImageBase64SourceOutputKey", out var bskObj))
                imageNode.ImageBase64SourceOutputKey = bskObj?.ToString();

            if (properties.TryGetValue("PreferGpu", out var pgObj) && pgObj != null &&
                bool.TryParse(pgObj.ToString(), out var pg))
                imageNode.PreferGpu = pg;
            if (properties.TryGetValue("FfmpegFilter", out var ffObj))
                imageNode.FfmpegFilter = ffObj?.ToString() ?? string.Empty;

            if (properties.TryGetValue("CroppedFolderPath", out var cfpObj))
                imageNode.CroppedFolderPath = cfpObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CroppedFolderSourceNodeId", out var cfsnObj))
                imageNode.CroppedFolderSourceNodeId = cfsnObj?.ToString();
            if (properties.TryGetValue("CroppedFolderSourceOutputKey", out var cfskObj))
                imageNode.CroppedFolderSourceOutputKey = cfskObj?.ToString();

            // Image Processor settings
            if (properties.TryGetValue("PromptSize", out var psObj) && psObj != null &&
                int.TryParse(psObj.ToString(), out var ps) && ps >= 1 && ps <= 4)
                imageNode.PromptSize = ps;
            if (properties.TryGetValue("ProcessorPrompt", out var ppObj))
                imageNode.ProcessorPrompt = ppObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("IsVerticalMode", out var ivmObj) && ivmObj != null &&
                bool.TryParse(ivmObj.ToString(), out var ivm))
                imageNode.IsVerticalMode = ivm;

            // Render node config
            if (properties.TryGetValue("RenderNodeId", out var rnObj))
                imageNode.RenderNodeId = rnObj?.ToString();
            if (properties.TryGetValue("RenderNodeOutputKey", out var rnkObj))
                imageNode.RenderNodeOutputKey = rnkObj?.ToString();

            // SkipOutputs
            if (properties.TryGetValue("SkipOutputs", out var soObj) && soObj != null)
            {
                try
                {
                    string? soJson = null;
                    if (soObj is string s) soJson = s;
                    else if (soObj is JsonElement je)
                        soJson = je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText();
                    if (!string.IsNullOrWhiteSpace(soJson))
                    {
                        var list = JsonSerializer.Deserialize<List<string>>(soJson);
                        if (list != null)
                        {
                            imageNode.SkipOutputs = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
                catch { }
            }

            // Deserialize danh sách vùng crop
            if (properties.TryGetValue("Crops", out var cropsObj) && cropsObj != null)
            {
                try
                {
                    string? cropsJson = null;
                    if (cropsObj is string s)
                        cropsJson = s;
                    else if (cropsObj is JsonElement je)
                    {
                        cropsJson = je.ValueKind == JsonValueKind.String
                            ? je.GetString()
                            : je.GetRawText();
                    }

                    if (!string.IsNullOrWhiteSpace(cropsJson))
                    {
                        var cropsList = JsonSerializer.Deserialize<List<JsonElement>>(cropsJson);
                        if (cropsList != null)
                        {
                            imageNode.Crops.Clear();
                            foreach (var cropEl in cropsList)
                            {
                                var region = new Models.Nodes.ImageCropRegion();

                                if (cropEl.TryGetProperty("Id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                                    region.Id = idEl.GetString() ?? region.Id;

                                if (cropEl.TryGetProperty("ColorHex", out var chEl) && chEl.ValueKind == JsonValueKind.String)
                                {
                                    var hex = chEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(hex))
                                        region.ColorHex = hex;
                                }

                                if (cropEl.TryGetProperty("IsVisible", out var ivEl) && ivEl.ValueKind == JsonValueKind.True || (cropEl.TryGetProperty("IsVisible", out ivEl) && ivEl.ValueKind == JsonValueKind.False))
                                    region.IsVisible = ivEl.GetBoolean();

                                if (cropEl.TryGetProperty("IsOutlineOnly", out var ioEl) && (ioEl.ValueKind == JsonValueKind.True || ioEl.ValueKind == JsonValueKind.False))
                                    region.IsOutlineOnly = ioEl.GetBoolean();

                                if (cropEl.TryGetProperty("SavedPath", out var spEl) && spEl.ValueKind == JsonValueKind.String)
                                    region.SavedPath = spEl.GetString();

                                if (cropEl.TryGetProperty("CropName", out var cnEl) && cnEl.ValueKind == JsonValueKind.String)
                                    region.CropName = cnEl.GetString() ?? string.Empty;

                                // Khôi phục Order
                                if (cropEl.TryGetProperty("Order", out var orderEl) && orderEl.TryGetInt32(out var orderVal))
                                    region.Order = orderVal;

                                // Khôi phục điểm polygon
                                if (cropEl.TryGetProperty("Points", out var ptEl) && ptEl.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var ptItem in ptEl.EnumerateArray())
                                    {
                                        if (ptItem.ValueKind == JsonValueKind.Array)
                                        {
                                            var arr = ptItem.EnumerateArray().ToList();
                                            if (arr.Count >= 2 &&
                                                arr[0].TryGetDouble(out var px) &&
                                                arr[1].TryGetDouble(out var py))
                                            {
                                                region.Points.Add(new System.Windows.Point(px, py));
                                            }
                                        }
                                    }
                                }

                                // Cập nhật BoundingBox từ Points
                                if (region.Points.Count > 0)
                                {
                                    var minX = region.Points.Min(p => p.X);
                                    var maxX = region.Points.Max(p => p.X);
                                    var minY = region.Points.Min(p => p.Y);
                                    var maxY = region.Points.Max(p => p.Y);
                                    region.BoundingBox = new System.Windows.Rect(minX, minY,
                                        Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
                                }

                                imageNode.Crops.Add(region);
                            }
                        }
                    }
                }
                catch { /* Không crash khi đọc crops - bỏ qua nếu lỗi format */ }
            }

            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj) && tdmObj != null &&
                Enum.TryParse<TitleDisplayMode>(tdmObj.ToString(), out var tdm))
                imageNode.TitleDisplayMode = tdm;

            if (properties.TryGetValue("TitleColorMode", out var tcmObj) && tcmObj != null &&
                Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
                imageNode.TitleColorMode = tcm;

            if (properties.TryGetValue("TitleColorKey", out var tckObj))
                imageNode.TitleColorKey = tckObj?.ToString();
        }
        else if (node is DataFetcherNode fetcherNode)
        {
            if (properties.TryGetValue("SourceNodeId", out var snObj))
                fetcherNode.SourceNodeId = snObj?.ToString();
            if (properties.TryGetValue("SourceOutputKey", out var sokObj))
                fetcherNode.SourceOutputKey = sokObj?.ToString();
            if (properties.TryGetValue("WaitForWebNodeLoad", out var wfwnlObj) && wfwnlObj != null &&
                bool.TryParse(wfwnlObj.ToString(), out var wfwnl))
                fetcherNode.WaitForWebNodeLoad = wfwnl;
            if (properties.TryGetValue("EnableTimer", out var etObj) && etObj != null &&
                bool.TryParse(etObj.ToString(), out var et))
                fetcherNode.EnableTimer = et;
            if (properties.TryGetValue("TimerIntervalValue", out var tivObj) && tivObj != null &&
                int.TryParse(tivObj.ToString(), out var tiv) && tiv > 0)
                fetcherNode.TimerIntervalValue = tiv;
            if (properties.TryGetValue("TimerUnit", out var tuObj) && tuObj != null)
                fetcherNode.TimerUnit = tuObj.ToString() ?? "s";
            if (properties.TryGetValue("EnableRealtime", out var erObj) && erObj != null &&
                bool.TryParse(erObj.ToString(), out var er))
                fetcherNode.EnableRealtime = er;
            if (properties.TryGetValue("EnableDataReadyScan", out var edrsObj) && edrsObj != null &&
                bool.TryParse(edrsObj.ToString(), out var edrs))
                fetcherNode.EnableDataReadyScan = edrs;
            if (properties.TryGetValue("DataReadyScanIntervalValue", out var drsivObj) && drsivObj != null &&
                int.TryParse(drsivObj.ToString(), out var drsiv) && drsiv > 0)
                fetcherNode.DataReadyScanIntervalValue = drsiv;
            if (properties.TryGetValue("DataReadyScanUnit", out var drsuObj) && drsuObj != null)
                fetcherNode.DataReadyScanUnit = drsuObj.ToString() ?? "s";
            if (properties.TryGetValue("DataReadyScanKeys", out var drskObj) && drskObj != null)
            {
                try
                {
                    if (drskObj is string jsonKeys && !string.IsNullOrWhiteSpace(jsonKeys))
                    {
                        var keys = JsonSerializer.Deserialize<List<string>>(jsonKeys);
                        if (keys != null)
                            fetcherNode.DataReadyScanKeys = keys;
                    }
                    else if (drskObj is JsonElement drskEl && drskEl.ValueKind == JsonValueKind.Array)
                    {
                        var keys = JsonSerializer.Deserialize<List<string>>(drskEl.GetRawText());
                        if (keys != null)
                            fetcherNode.DataReadyScanKeys = keys;
                    }
                    else if (drskObj is JsonElement drskElStr && drskElStr.ValueKind == JsonValueKind.String)
                    {
                        var jsonKeys2 = drskElStr.GetString();
                        if (!string.IsNullOrWhiteSpace(jsonKeys2))
                        {
                            var keys = JsonSerializer.Deserialize<List<string>>(jsonKeys2);
                            if (keys != null)
                                fetcherNode.DataReadyScanKeys = keys;
                        }
                    }
                }
                catch { }
            }
            if (properties.TryGetValue("TitleDisplayMode", out var tdmFetcherObj) && tdmFetcherObj != null &&
                Enum.TryParse<TitleDisplayMode>(tdmFetcherObj.ToString(), out var tdmFetcher))
                fetcherNode.TitleDisplayMode = tdmFetcher;
            if (properties.TryGetValue("TitleColorMode", out var tcmFetcherObj) && tcmFetcherObj != null &&
                Enum.TryParse<TitleColorMode>(tcmFetcherObj.ToString(), out var tcmFetcher))
                fetcherNode.TitleColorMode = tcmFetcher;
            if (properties.TryGetValue("TitleColorKey", out var tckFetcherObj))
                fetcherNode.TitleColorKey = tckFetcherObj?.ToString();
        }
            else if (node is WebNode webNode)
        {
            if (properties.TryGetValue("Width", out var wObj) && wObj != null && double.TryParse(wObj.ToString(), out var w))
            {
                // Đảm bảo Width luôn >= 280 để tránh lỗi HwndHost khi chuyển workflow giữa các máy
                webNode.Width = Math.Max(280, w);
            }
            if (properties.TryGetValue("Height", out var hObj) && hObj != null && double.TryParse(hObj.ToString(), out var h))
            {
                // Đảm bảo Height luôn >= 200 để tránh lỗi HwndHost khi chuyển workflow giữa các máy
                webNode.Height = Math.Max(200, h);
            }
            if (properties.TryGetValue("ExtractUrl", out var euObj))
                webNode.ExtractUrl = euObj?.ToString() ?? "";
            if (properties.TryGetValue("ExtractRequestMethod", out var ermObj))
                webNode.ExtractRequestMethod = ermObj?.ToString() ?? "GET";
            if (properties.TryGetValue("ExtractStatusCode", out var escObj))
                webNode.ExtractStatusCode = escObj?.ToString() ?? "200";
            // Timeout chờ outputs từ WebView2 (ms)
            if (properties.TryGetValue("ResponseOutputsWaitTimeoutMs", out var rowtObj) && rowtObj != null &&
                int.TryParse(rowtObj.ToString(), out var rowt) && rowt >= 0)
                webNode.ResponseOutputsWaitTimeoutMs = rowt;
            // Wait mode (ALL / ANY)
            if (properties.TryGetValue("ResponseOutputsWaitMode", out var rowmObj) && rowmObj != null &&
                Enum.TryParse<FlowMy.Models.Nodes.WebOutputsWaitMode>(rowmObj.ToString(), out var rowm))
                webNode.ResponseOutputsWaitMode = rowm;
            if (properties.TryGetValue("BlockingRules", out var brObj) && brObj != null)
            {
                try
                {
                    webNode.BlockingRules.Clear();
                    JsonElement? brJe = null;
                    if (brObj is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.Array) brJe = je;
                        else if (je.ValueKind == JsonValueKind.String)
                        {
                            var s = je.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) brJe = JsonSerializer.Deserialize<JsonElement>(s);
                        }
                    }
                    else if (brObj is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        brJe = JsonSerializer.Deserialize<JsonElement>(s);
                    }

                    if (brJe.HasValue && brJe.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in brJe.Value.EnumerateArray())
                        {
                            var r = new WebBlockingRule();
                            if (e.TryGetProperty("UrlPattern", out var up)) r.UrlPattern = GetStringFromJsonValue(up);

                            // Method (optional in older workflows)
                            if (e.TryGetProperty("Method", out var m))
                                r.Method = GetStringFromJsonValue(m) ?? "All";
                            else
                                r.Method = "All";

                            // Child rules (new format)
                            if (e.TryGetProperty("ChildRules", out var crJe) && crJe.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var childEl in crJe.EnumerateArray())
                                {
                                    var child = new WebBlockingChildRule();
                                    if (childEl.TryGetProperty("UrlPattern", out var cup))
                                        child.UrlPattern = GetStringFromJsonValue(cup) ?? string.Empty;
                                    if (childEl.TryGetProperty("Method", out var cm))
                                        child.Method = GetStringFromJsonValue(cm) ?? "All";
                                    else
                                        child.Method = "All";

                                    if (!string.IsNullOrWhiteSpace(child.UrlPattern))
                                        r.ChildRules.Add(child);
                                }
                            }

                            webNode.BlockingRules.Add(r);
                        }
                    }
                }
                catch { }
            }
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj) && tdmObj != null && Enum.TryParse<TitleDisplayMode>(tdmObj.ToString(), out var tdm))
                webNode.TitleDisplayMode = tdm;
            if (properties.TryGetValue("TitleColorMode", out var tcmObj) && tcmObj != null && Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
                webNode.TitleColorMode = tcm;
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
                webNode.TitleColorKey = tckObj?.ToString();
            if (properties.TryGetValue("EnableSleepMode", out var esmObj) && esmObj != null &&
                bool.TryParse(esmObj.ToString(), out var esm))
                webNode.EnableSleepMode = esm;
            if (properties.TryGetValue("SleepIdleTimeoutValue", out var sitvObj) && sitvObj != null &&
                int.TryParse(sitvObj.ToString(), out var sitv))
                webNode.SleepIdleTimeoutValue = sitv;
            if (properties.TryGetValue("SleepIdleTimeoutUnit", out var situObj) && situObj != null)
                webNode.SleepIdleTimeoutUnit = situObj.ToString() ?? webNode.SleepIdleTimeoutUnit;
            if (properties.TryGetValue("SyncLiveOutputsToResults", out var sloObj) && sloObj != null &&
                bool.TryParse(sloObj.ToString(), out var slo))
                webNode.SyncLiveOutputsToResults = slo;

            // Auto-reload timer
            if (properties.TryGetValue("AutoReloadEnabled", out var areObj) && areObj != null &&
                bool.TryParse(areObj.ToString(), out var areVal))
                webNode.AutoReloadEnabled = areVal;
            if (properties.TryGetValue("AutoReloadIntervalValue", out var arivObj) && arivObj != null)
            {
                if (arivObj is JsonElement arivJe)
                {
                    try { webNode.AutoReloadIntervalValue = arivJe.GetDouble(); } catch { }
                }
                else if (double.TryParse(arivObj.ToString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var arivD))
                    webNode.AutoReloadIntervalValue = arivD;
            }
            if (properties.TryGetValue("AutoReloadIntervalUnit", out var ariuObj) && ariuObj != null)
            {
                var u = ariuObj.ToString();
                if (!string.IsNullOrWhiteSpace(u)) webNode.AutoReloadIntervalUnit = u!;
            }

            // Block all requests after first match
            if (properties.TryGetValue("BlockAllRequestsAfterFirstMatch", out var baaObj) && baaObj != null &&
                bool.TryParse(baaObj.ToString(), out var baaVal))
                webNode.BlockAllRequestsAfterFirstMatch = baaVal;

            // Restore per-domain CSS zoom if available
            if (properties.TryGetValue("Web_LastHost", out var whObj))
                webNode.LastHost = GetStringFromJsonValue(whObj);
            if (properties.TryGetValue("Web_CssZoom", out var wzObj))
            {
                if (wzObj is JsonElement jeZoom)
                {
                    try { webNode.CssZoom = jeZoom.GetDouble(); } catch { }
                }
                else if (double.TryParse(wzObj?.ToString(), out var z) && z > 0)
                {
                    webNode.CssZoom = z;
                }
            }

            // JS injection (nhiều Node+Key -> WebView2) – migrate từ format cũ nếu có
            if (properties.TryGetValue("JsSources", out var jsArrObj) && jsArrObj != null)
            {
                try
                {
                    var list = new List<WebJsSourceMapping>();
                    if (jsArrObj is JsonElement jsJe)
                    {
                        if (jsJe.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var e in jsJe.EnumerateArray())
                            {
                                var m = new WebJsSourceMapping();
                                if (e.TryGetProperty("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                if (e.TryGetProperty("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                // AutoTimer fields (optional, backward-compatible)
                                if (e.TryGetProperty("AutoTimerEnabled", out var ate))
                                {
                                    if (ate.ValueKind == JsonValueKind.True) m.AutoTimerEnabled = true;
                                    else if (ate.ValueKind == JsonValueKind.False) m.AutoTimerEnabled = false;
                                    else if (ate.ValueKind == JsonValueKind.String && bool.TryParse(ate.GetString(), out var b)) m.AutoTimerEnabled = b;
                                }
                                if (e.TryGetProperty("AutoTimerIntervalValue", out var ativ))
                                {
                                    if (ativ.ValueKind == JsonValueKind.Number && ativ.TryGetDouble(out var dv)) m.AutoTimerIntervalValue = dv;
                                    else if (ativ.ValueKind == JsonValueKind.String && double.TryParse(ativ.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dsv)) m.AutoTimerIntervalValue = dsv;
                                }
                                if (e.TryGetProperty("AutoTimerIntervalUnit", out var atiu))
                                {
                                    var u = GetStringFromJsonValue(atiu);
                                    if (!string.IsNullOrWhiteSpace(u)) m.AutoTimerIntervalUnit = u!;
                                }
                                if (!string.IsNullOrWhiteSpace(m.SourceNodeId) && !string.IsNullOrWhiteSpace(m.SourceOutputKey))
                                    list.Add(m);
                            }
                        }
                        else if (jsJe.ValueKind == JsonValueKind.String)
                        {
                            var s = jsJe.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                var arr = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(s);
                                if (arr != null)
                                {
                                    foreach (var d in arr)
                                    {
                                        var m = new WebJsSourceMapping();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                        if (d.TryGetValue("AutoTimerEnabled", out var ate))
                                        {
                                            if (ate.ValueKind == JsonValueKind.True) m.AutoTimerEnabled = true;
                                            else if (ate.ValueKind == JsonValueKind.False) m.AutoTimerEnabled = false;
                                            else if (ate.ValueKind == JsonValueKind.String && bool.TryParse(ate.GetString(), out var b)) m.AutoTimerEnabled = b;
                                        }
                                        if (d.TryGetValue("AutoTimerIntervalValue", out var ativ))
                                        {
                                            if (ativ.ValueKind == JsonValueKind.Number && ativ.TryGetDouble(out var dv)) m.AutoTimerIntervalValue = dv;
                                            else if (ativ.ValueKind == JsonValueKind.String && double.TryParse(ativ.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dsv)) m.AutoTimerIntervalValue = dsv;
                                        }
                                        if (d.TryGetValue("AutoTimerIntervalUnit", out var atiu)) { var u = GetStringFromJsonValue(atiu); if (!string.IsNullOrWhiteSpace(u)) m.AutoTimerIntervalUnit = u!; }
                                        if (!string.IsNullOrWhiteSpace(m.SourceNodeId) && !string.IsNullOrWhiteSpace(m.SourceOutputKey))
                                            list.Add(m);
                                    }
                                }
                            }
                        }
                    }
                    if (list.Count > 0)
                        webNode.JsSources = list;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error deserializing WebNode.JsSources: {ex.Message}"); }
            }
            // Backward compat: migrate JsSourceNodeId + JsSourceOutputKey
            if ((webNode.JsSources == null || webNode.JsSources.Count == 0) &&
                properties.TryGetValue("JsSourceNodeId", out var jsnObj) && properties.TryGetValue("JsSourceOutputKey", out var jskObj))
            {
                var nodeId = jsnObj?.ToString();
                var key = jskObj?.ToString();
                if (!string.IsNullOrWhiteSpace(nodeId) && !string.IsNullOrWhiteSpace(key))
                {
                    webNode.JsSources = new List<WebJsSourceMapping>
                    {
                        new WebJsSourceMapping { SourceNodeId = nodeId, SourceOutputKey = key }
                    };
                }
            }

            // Deserialize InputMappings (giống CodeNode nhưng dùng WebInputMapping)
            if (properties.TryGetValue("InputMappings", out var imObj) && imObj != null)
            {
                try
                {
                    var list = new List<WebInputMapping>();
                    if (imObj is JsonElement imJe)
                    {
                        if (imJe.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var e in imJe.EnumerateArray())
                            {
                                var m = new WebInputMapping();
                                if (e.TryGetProperty("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                if (e.TryGetProperty("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                if (e.TryGetProperty("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                list.Add(m);
                            }
                        }
                        else if (imJe.ValueKind == JsonValueKind.String)
                        {
                            var s = imJe.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                var arr = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(s);
                                if (arr != null)
                                {
                                    foreach (var d in arr)
                                    {
                                        var m = new WebInputMapping();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = sni?.ToString();
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = sok?.ToString();
                                        if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = iko?.ToString();
                                        list.Add(m);
                                    }
                                }
                            }
                        }
                    }
                    else if (imObj is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        var arr = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(s);
                        if (arr != null)
                        {
                            foreach (var d in arr)
                            {
                                var m = new WebInputMapping();
                                if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = sni?.ToString();
                                if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = sok?.ToString();
                                if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = iko?.ToString();
                                list.Add(m);
                            }
                        }
                    }

                    if (list.Count > 0)
                        webNode.InputMappings = list;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deserializing WebNode.InputMappings: {ex.Message}\n{ex.StackTrace}");
                }
            }
            if (properties.TryGetValue("RequestInterceptRules", out var rirObj) && rirObj != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"=== Deserializing RequestInterceptRules for WebNode ===");
                    System.Diagnostics.Debug.WriteLine($"RequestInterceptRules value type: {rirObj.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"RequestInterceptRules value: {rirObj}");
                    
                    webNode.RequestInterceptRules.Clear();
                    JsonElement? rirJe = null;
                    if (rirObj is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.Array)
                        {
                            rirJe = je;
                            System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is JsonElement array");
                        }
                        else if (je.ValueKind == JsonValueKind.String)
                        {
                            var rirStr = je.GetString();
                            if (!string.IsNullOrWhiteSpace(rirStr))
                            {
                                System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is JsonElement string: {rirStr.Substring(0, Math.Min(100, rirStr.Length))}...");
                                try
                                {
                                    var parsed = JsonSerializer.Deserialize<JsonElement>(rirStr);
                                    if (parsed.ValueKind == JsonValueKind.Array)
                                    {
                                        rirJe = parsed;
                                        System.Diagnostics.Debug.WriteLine($"✓ Successfully parsed JSON string to JsonElement array");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"⚠ Parsed JSON is not an array (ValueKind: {parsed.ValueKind})");
                                    }
                                }
                                catch (System.Text.Json.JsonException ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize RequestInterceptRules JSON string: {ex.Message}\n{ex.StackTrace}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error parsing RequestInterceptRules JSON string: {ex.Message}\n{ex.StackTrace}");
                                }
                            }
                        }
                    }
                    else if (rirObj is string rirStr && !string.IsNullOrWhiteSpace(rirStr))
                    {
                        System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is JSON string: {rirStr.Substring(0, Math.Min(100, rirStr.Length))}...");
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<JsonElement>(rirStr);
                            if (parsed.ValueKind == JsonValueKind.Array)
                            {
                                rirJe = parsed;
                                System.Diagnostics.Debug.WriteLine($"✓ Successfully parsed JSON string to JsonElement array");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠ Parsed JSON is not an array (ValueKind: {parsed.ValueKind})");
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize RequestInterceptRules JSON string: {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Error parsing RequestInterceptRules JSON string: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ RequestInterceptRules is unsupported type/value (type: {rirObj.GetType().Name})");
                    }
                    
                    if (rirJe.HasValue)
                    {
                        try
                        {
                            int count = 0;
                            foreach (var e in rirJe.Value.EnumerateArray())
                            {
                                try
                                {
                                    var r = new WebRequestInterceptRule();
                                    if (e.TryGetProperty("MatchUrlPattern", out var mup)) r.MatchUrlPattern = GetStringFromJsonValue(mup);
                                    if (e.TryGetProperty("ReplaceUrlValue", out var ruv)) r.ReplaceUrlValue = GetStringFromJsonValue(ruv);
                                    if (e.TryGetProperty("ReplaceUrlSourceNodeId", out var rusni)) r.ReplaceUrlSourceNodeId = GetStringFromJsonValue(rusni);
                                    if (e.TryGetProperty("ReplaceUrlSourceOutputKey", out var rusok)) r.ReplaceUrlSourceOutputKey = GetStringFromJsonValue(rusok);
                                    if (e.TryGetProperty("ReplaceUrlWithNodeKey", out var ruwnk))
                                    {
                                        if (ruwnk.ValueKind == JsonValueKind.True || ruwnk.ValueKind == JsonValueKind.False)
                                            r.ReplaceUrlWithNodeKey = ruwnk.GetBoolean();
                                        else if (bool.TryParse(GetStringFromJsonValue(ruwnk), out var ruwnkBool))
                                            r.ReplaceUrlWithNodeKey = ruwnkBool;
                                    }
                                    if (e.TryGetProperty("ReplaceParamsValue", out var rpv)) r.ReplaceParamsValue = GetStringFromJsonValue(rpv);
                                    if (e.TryGetProperty("ReplaceParamsSourceNodeId", out var rpsni)) r.ReplaceParamsSourceNodeId = GetStringFromJsonValue(rpsni);
                                    if (e.TryGetProperty("ReplaceParamsSourceOutputKey", out var rpsok)) r.ReplaceParamsSourceOutputKey = GetStringFromJsonValue(rpsok);
                                    if (e.TryGetProperty("ReplaceBodyValue", out var rbv)) r.ReplaceBodyValue = GetStringFromJsonValue(rbv);
                                    if (e.TryGetProperty("ReplaceBodySourceNodeId", out var rbsni)) r.ReplaceBodySourceNodeId = GetStringFromJsonValue(rbsni);
                                    if (e.TryGetProperty("ReplaceBodySourceOutputKey", out var rbsok)) r.ReplaceBodySourceOutputKey = GetStringFromJsonValue(rbsok);
                                    
                                    System.Diagnostics.Debug.WriteLine($"Deserialized RequestInterceptRule [{count}]: MatchUrl='{r.MatchUrlPattern}', ReplaceUrl='{r.ReplaceUrlValue}', ReplaceUrlWithNodeKey={r.ReplaceUrlWithNodeKey}");
                                    webNode.RequestInterceptRules.Add(r);
                                    count++;
                                }
                                catch (System.Text.Json.JsonException ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize RequestInterceptRule item at index {count}: {ex.Message}\n{ex.StackTrace}");
                                    // Continue with next item
                                    count++;
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error deserializing RequestInterceptRule item at index {count}: {ex.Message}\n{ex.StackTrace}");
                                    // Continue with next item
                                    count++;
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"✓ Successfully deserialized {count} RequestInterceptRules. Collection now has {webNode.RequestInterceptRules.Count} items");
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to enumerate RequestInterceptRules array: {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Error processing RequestInterceptRules array: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ rirJe is null or has no value");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error deserializing RequestInterceptRules: {ex.Message}\n{ex.StackTrace}");
                    // Continue - don't crash the entire workflow load
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"RequestInterceptRules property not found or null in Properties");
            }
            
            // Deserialize ResponseOutputs
            if (properties.TryGetValue("ResponseOutputs", out var roObj) && roObj != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"=== Deserializing ResponseOutputs for WebNode ===");
                    System.Diagnostics.Debug.WriteLine($"ResponseOutputs value type: {roObj.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"ResponseOutputs value: {roObj}");
                    
                    webNode.ResponseOutputs.Clear();
                    JsonElement? roJe = null;
                    if (roObj is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.Array)
                        {
                            roJe = je;
                            System.Diagnostics.Debug.WriteLine($"ResponseOutputs is JsonElement array");
                        }
                        else if (je.ValueKind == JsonValueKind.String)
                        {
                            var roStr = je.GetString();
                            if (!string.IsNullOrWhiteSpace(roStr))
                            {
                                System.Diagnostics.Debug.WriteLine($"ResponseOutputs is JsonElement string: {roStr.Substring(0, Math.Min(100, roStr.Length))}...");
                                try
                                {
                                    var parsed = JsonSerializer.Deserialize<JsonElement>(roStr);
                                    if (parsed.ValueKind == JsonValueKind.Array)
                                    {
                                        roJe = parsed;
                                        System.Diagnostics.Debug.WriteLine($"✓ Successfully parsed JSON string to JsonElement array");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"⚠ Parsed JSON is not an array (ValueKind: {parsed.ValueKind})");
                                    }
                                }
                                catch (System.Text.Json.JsonException ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize ResponseOutputs JSON string: {ex.Message}\n{ex.StackTrace}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error parsing ResponseOutputs JSON string: {ex.Message}\n{ex.StackTrace}");
                                }
                            }
                        }
                    }
                    else if (roObj is string roStr && !string.IsNullOrWhiteSpace(roStr))
                    {
                        System.Diagnostics.Debug.WriteLine($"ResponseOutputs is JSON string: {roStr.Substring(0, Math.Min(100, roStr.Length))}...");
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<JsonElement>(roStr);
                            if (parsed.ValueKind == JsonValueKind.Array)
                            {
                                roJe = parsed;
                                System.Diagnostics.Debug.WriteLine($"✓ Successfully parsed JSON string to JsonElement array");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠ Parsed JSON is not an array (ValueKind: {parsed.ValueKind})");
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize ResponseOutputs JSON string: {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Error parsing ResponseOutputs JSON string: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ ResponseOutputs is unsupported type/value (type: {roObj.GetType().Name})");
                    }
                    
                    if (roJe.HasValue)
                    {
                        try
                        {
                            int count = 0;
                            foreach (var e in roJe.Value.EnumerateArray())
                            {
                                try
                                {
                                    var ro = new WebResponseOutput();
                                    if (e.TryGetProperty("Key", out var k)) ro.Key = GetStringFromJsonValue(k);
                                    if (e.TryGetProperty("Url", out var u)) ro.Url = GetStringFromJsonValue(u);
                                    if (e.TryGetProperty("RequestMethod", out var rm)) ro.RequestMethod = GetStringFromJsonValue(rm);
                                    if (string.IsNullOrWhiteSpace(ro.RequestMethod)) ro.RequestMethod = "GET";
                                    if (e.TryGetProperty("ExtractType", out var et)) ro.ExtractType = GetStringFromJsonValue(et);
                                    if (string.IsNullOrWhiteSpace(ro.ExtractType)) ro.ExtractType = "Response";
                                    // WaitForCompletion (optional, backward compatible)
                                    if (e.TryGetProperty("WaitForCompletion", out var wfc))
                                    {
                                        var wfcStr = GetStringFromJsonValue(wfc);
                                        if (bool.TryParse(wfcStr, out var wfcBool))
                                            ro.WaitForCompletion = wfcBool;
                                    }
                                    
                                    System.Diagnostics.Debug.WriteLine($"Deserialized ResponseOutput [{count}]: Key='{ro.Key}', Url='{ro.Url}', Method='{ro.RequestMethod}', ExtractType='{ro.ExtractType}'");
                                    webNode.ResponseOutputs.Add(ro);
                                    count++;
                                }
                                catch (System.Text.Json.JsonException ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Failed to deserialize ResponseOutput item at index {count}: {ex.Message}\n{ex.StackTrace}");
                                    // Continue with next item
                                    count++;
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error deserializing ResponseOutput item at index {count}: {ex.Message}\n{ex.StackTrace}");
                                    // Continue with next item
                                    count++;
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"✓ Successfully deserialized {count} ResponseOutputs. Collection now has {webNode.ResponseOutputs.Count} items");
                            
                            // Rebuild DynamicOutputs sau khi load ResponseOutputs
                            webNode.RebuildResponseOutputs();
                            System.Diagnostics.Debug.WriteLine($"✓ RebuildResponseOutputs() called");
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to enumerate ResponseOutputs array: {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Error processing ResponseOutputs array: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ roJe is null or has no value");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error deserializing ResponseOutputs: {ex.Message}\n{ex.StackTrace}");
                    // Continue - don't crash the entire workflow load
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ResponseOutputs property not found or null in Properties");
            }
        }
        else if (node is CodeNode codeNode)
        {
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj) && tdmObj != null && Enum.TryParse<TitleDisplayMode>(tdmObj.ToString(), out var tdm))
                codeNode.TitleDisplayMode = tdm;
            if (properties.TryGetValue("TitleColorMode", out var tcmObj) && tcmObj != null && Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
                codeNode.TitleColorMode = tcm;
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
                codeNode.TitleColorKey = tckObj?.ToString();
            var loadedMappings = false;
            if (properties.TryGetValue("InputMappings", out var imObj) && imObj != null)
            {
                var list = new List<CodeInputMapping>();
                if (imObj is JsonElement imJe)
                {
                    if (imJe.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in imJe.EnumerateArray())
                        {
                            var m = new CodeInputMapping();
                            if (e.TryGetProperty("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                            if (e.TryGetProperty("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                            if (e.TryGetProperty("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                            if (e.TryGetProperty("ShouldReExecute", out var sre))
                            {
                                if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                            }
                            list.Add(m);
                        }
                    }
                    else if (imJe.ValueKind == JsonValueKind.String)
                    {
                        var str = imJe.GetString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            try
                            {
                                var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(str);
                                if (parsed != null)
                                    foreach (var d in parsed)
                                    {
                                        var m = new CodeInputMapping();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                        if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                        if (d.TryGetValue("ShouldReExecute", out var sre))
                                        {
                                            if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                            else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                            else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                                        }
                                        list.Add(m);
                                    }
                            }
                            catch { }
                        }
                    }
                }
                else if (imObj is string imStr && !string.IsNullOrEmpty(imStr))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(imStr);
                        if (parsed != null)
                            foreach (var d in parsed)
                            {
                                var m = new CodeInputMapping();
                                if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                if (d.TryGetValue("ShouldReExecute", out var sre))
                                {
                                    if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                    else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                    else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                                }
                                list.Add(m);
                            }
                    }
                    catch { }
                }
                if (list.Count > 0) { codeNode.InputMappings = list; loadedMappings = true; }
            }
            if (!loadedMappings)
            {
                var first = codeNode.InputMappings.Count > 0 ? codeNode.InputMappings[0] : null;
                if (first == null) { first = new CodeInputMapping(); codeNode.InputMappings.Add(first); }
                if (properties.TryGetValue("SourceNodeId", out var snidObj))
                    first.SourceNodeId = GetStringFromJsonValue(snidObj);
                if (properties.TryGetValue("SourceOutputKey", out var sokObj))
                    first.SourceOutputKey = GetStringFromJsonValue(sokObj);
                if (properties.TryGetValue("InputKeyOverride", out var ikoObj))
                    first.InputKeyOverride = GetStringFromJsonValue(ikoObj);
            }
            if (properties.TryGetValue("ScriptCode", out var scObj))
                codeNode.ScriptCode = scObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("OutputKeys", out var okObj))
            {
                List<string>? keys = null;
                if (okObj is string jsonKeys)
                {
                    try { keys = JsonSerializer.Deserialize<List<string>>(jsonKeys); } catch { }
                }
                else if (okObj is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Array)
                    {
                        try { keys = JsonSerializer.Deserialize<List<string>>(je.GetRawText()); } catch { }
                    }
                    else if (je.ValueKind == JsonValueKind.String)
                    {
                        try { keys = JsonSerializer.Deserialize<List<string>>(je.GetString() ?? "[]"); } catch { }
                    }
                }
                if (keys != null)
                    codeNode.OutputKeys = keys;
                codeNode.RebuildDynamicOutputs();
            }
        }
        else if (node is FolderNode folderNode)
        {
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj) && tdmObj != null && Enum.TryParse<TitleDisplayMode>(tdmObj.ToString(), out var tdm))
                folderNode.TitleDisplayMode = tdm;
            if (properties.TryGetValue("TitleColorMode", out var tcmObj) && tcmObj != null && Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
                folderNode.TitleColorMode = tcm;
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
                folderNode.TitleColorKey = tckObj?.ToString();
            if (properties.TryGetValue("RootFolderPath", out var rfpObj))
                folderNode.RootFolderPath = rfpObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("RootFolderPresetKey", out var rfpkObj))
                folderNode.RootFolderPresetKey = rfpkObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("SubPathTemplate", out var sptObj))
                folderNode.SubPathTemplate = sptObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("KeyValueInputs", out var kviObj) && kviObj != null)
            {
                var list = new List<FolderKeyValueInput>();
                if (kviObj is JsonElement kviJe)
                {
                    if (kviJe.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in kviJe.EnumerateArray())
                        {
                            var kv = new FolderKeyValueInput();
                            if (e.TryGetProperty("SourceNodeId", out var sni)) kv.SourceNodeId = GetStringFromJsonValue(sni);
                            if (e.TryGetProperty("SourceOutputKey", out var sok)) kv.SourceOutputKey = GetStringFromJsonValue(sok);
                            if (e.TryGetProperty("ValueConfirm", out var vc)) kv.ValueConfirm = GetStringFromJsonValue(vc);
                            list.Add(kv);
                        }
                    }
                    else if (kviJe.ValueKind == JsonValueKind.String)
                    {
                        var str = kviJe.GetString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            try
                            {
                                var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(str);
                                if (parsed != null)
                                    foreach (var d in parsed)
                                    {
                                        var kv = new FolderKeyValueInput();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) kv.SourceNodeId = GetStringFromJsonValue(sni);
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) kv.SourceOutputKey = GetStringFromJsonValue(sok);
                                        if (d.TryGetValue("ValueConfirm", out var vc)) kv.ValueConfirm = GetStringFromJsonValue(vc);
                                        list.Add(kv);
                                    }
                            }
                            catch { }
                        }
                    }
                }
                else if (kviObj is string kviStr && !string.IsNullOrEmpty(kviStr))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(kviStr);
                        if (parsed != null)
                            foreach (var d in parsed)
                            {
                                var kv = new FolderKeyValueInput();
                                if (d.TryGetValue("SourceNodeId", out var sni)) kv.SourceNodeId = GetStringFromJsonValue(sni);
                                if (d.TryGetValue("SourceOutputKey", out var sok)) kv.SourceOutputKey = GetStringFromJsonValue(sok);
                                if (d.TryGetValue("ValueConfirm", out var vc)) kv.ValueConfirm = GetStringFromJsonValue(vc);
                                list.Add(kv);
                            }
                    }
                    catch { }
                }
                if (list.Count > 0)
                {
                    folderNode.KeyValueInputs.Clear();
                    foreach (var kv in list)
                        folderNode.KeyValueInputs.Add(kv);
                }
            }
        }
        else if (node is HtmlUiNode htmlUiNode)
        {
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj) && tdmObj != null && Enum.TryParse<TitleDisplayMode>(tdmObj.ToString(), out var tdm))
                htmlUiNode.TitleDisplayMode = tdm;
            if (properties.TryGetValue("TitleColorMode", out var tcmObj) && tcmObj != null && Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
                htmlUiNode.TitleColorMode = tcm;
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
                htmlUiNode.TitleColorKey = tckObj?.ToString();
            if (properties.TryGetValue("EnableSleepMode", out var hsmObj) && hsmObj != null &&
                bool.TryParse(hsmObj.ToString(), out var hsm))
                htmlUiNode.EnableSleepMode = hsm;
            if (properties.TryGetValue("SleepIdleTimeoutValue", out var hsitvObj) && hsitvObj != null &&
                int.TryParse(hsitvObj.ToString(), out var hsitv))
                htmlUiNode.SleepIdleTimeoutValue = hsitv;
            if (properties.TryGetValue("SleepIdleTimeoutUnit", out var hsituObj) && hsituObj != null)
                htmlUiNode.SleepIdleTimeoutUnit = hsituObj.ToString() ?? htmlUiNode.SleepIdleTimeoutUnit;
            // Restore HtmlUi specific zoom if present
            if (properties.TryGetValue("HtmlUi_CorrectedZoom", out var zoomObj) || properties.TryGetValue("HtmlUi_CssZoom", out zoomObj))
            {
                if (zoomObj != null)
                {
                    if (zoomObj is JsonElement je)
                    {
                        try { htmlUiNode.CssZoom = je.GetDouble(); } catch { }
                    }
                    else
                    {
                        if (double.TryParse(zoomObj.ToString(), out var z))
                            htmlUiNode.CssZoom = z;
                    }
                }
            }
            
            var loadedMappings = false;
            if (properties.TryGetValue("InputMappings", out var imObj) && imObj != null)
            {
                var list = new List<CodeInputMapping>();
                if (imObj is JsonElement imJe)
                {
                    if (imJe.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in imJe.EnumerateArray())
                        {
                            var m = new CodeInputMapping();
                            if (e.TryGetProperty("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                            if (e.TryGetProperty("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                            if (e.TryGetProperty("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                            if (e.TryGetProperty("ShouldReExecute", out var sre))
                            {
                                if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                            }
                            if (e.TryGetProperty("AutoRefreshEnabled", out var are))
                            {
                                if (are.ValueKind == JsonValueKind.True) m.AutoRefreshEnabled = true;
                                else if (are.ValueKind == JsonValueKind.False) m.AutoRefreshEnabled = false;
                                else if (bool.TryParse(are.ToString(), out var b)) m.AutoRefreshEnabled = b;
                            }
                            if (e.TryGetProperty("AutoRefreshInterval", out var ari))
                            {
                                if (ari.ValueKind == JsonValueKind.Number && ari.TryGetInt32(out var iv)) m.AutoRefreshInterval = iv;
                                else if (int.TryParse(ari.ToString(), out var iv2)) m.AutoRefreshInterval = iv2;
                            }
                            if (e.TryGetProperty("AutoRefreshUnit", out var aru))
                            {
                                var u = GetStringFromJsonValue(aru);
                                if (!string.IsNullOrWhiteSpace(u)) m.AutoRefreshUnit = u!;
                            }
                            list.Add(m);
                        }
                    }
                    else if (imJe.ValueKind == JsonValueKind.String)
                    {
                        var str = imJe.GetString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            try
                            {
                                var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(str);
                                if (parsed != null)
                                    foreach (var d in parsed)
                                    {
                                        var m = new CodeInputMapping();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                        if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                        if (d.TryGetValue("ShouldReExecute", out var sre))
                                        {
                                            if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                            else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                            else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                                        }
                                        if (d.TryGetValue("AutoRefreshEnabled", out var are))
                                        {
                                            if (are.ValueKind == JsonValueKind.True) m.AutoRefreshEnabled = true;
                                            else if (are.ValueKind == JsonValueKind.False) m.AutoRefreshEnabled = false;
                                            else if (bool.TryParse(are.ToString(), out var b)) m.AutoRefreshEnabled = b;
                                        }
                                        if (d.TryGetValue("AutoRefreshInterval", out var ari) && ari.ValueKind == JsonValueKind.Number && ari.TryGetInt32(out var ariv)) m.AutoRefreshInterval = ariv;
                                        if (d.TryGetValue("AutoRefreshUnit", out var aru)) { var u = GetStringFromJsonValue(aru); if (!string.IsNullOrWhiteSpace(u)) m.AutoRefreshUnit = u!; }
                                        list.Add(m);
                                    }
                            }
                            catch { }
                        }
                    }
                }
                else if (imObj is string imStr && !string.IsNullOrEmpty(imStr))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(imStr);
                        if (parsed != null)
                            foreach (var d in parsed)
                            {
                                var m = new CodeInputMapping();
                                if (d.TryGetValue("SourceNodeId", out var sni)) m.SourceNodeId = GetStringFromJsonValue(sni);
                                if (d.TryGetValue("SourceOutputKey", out var sok)) m.SourceOutputKey = GetStringFromJsonValue(sok);
                                if (d.TryGetValue("InputKeyOverride", out var iko)) m.InputKeyOverride = GetStringFromJsonValue(iko);
                                if (d.TryGetValue("ShouldReExecute", out var sre))
                                {
                                    if (sre.ValueKind == JsonValueKind.True) m.ShouldReExecute = true;
                                    else if (sre.ValueKind == JsonValueKind.False) m.ShouldReExecute = false;
                                    else if (bool.TryParse(sre.ToString(), out var b)) m.ShouldReExecute = b;
                                }
                                if (d.TryGetValue("AutoRefreshEnabled", out var are))
                                {
                                    if (are.ValueKind == JsonValueKind.True) m.AutoRefreshEnabled = true;
                                    else if (are.ValueKind == JsonValueKind.False) m.AutoRefreshEnabled = false;
                                    else if (bool.TryParse(are.ToString(), out var b)) m.AutoRefreshEnabled = b;
                                }
                                if (d.TryGetValue("AutoRefreshInterval", out var ari) && ari.ValueKind == JsonValueKind.Number && ari.TryGetInt32(out var ariv)) m.AutoRefreshInterval = ariv;
                                if (d.TryGetValue("AutoRefreshUnit", out var aru)) { var u = GetStringFromJsonValue(aru); if (!string.IsNullOrWhiteSpace(u)) m.AutoRefreshUnit = u!; }
                                list.Add(m);
                            }
                    }
                    catch { }
                }
                if (list.Count > 0) { htmlUiNode.InputMappings = list; loadedMappings = true; }
            }
            if (!loadedMappings)
            {
                var first = htmlUiNode.InputMappings.Count > 0 ? htmlUiNode.InputMappings[0] : null;
                if (first == null) { first = new CodeInputMapping(); htmlUiNode.InputMappings.Add(first); }
                if (properties.TryGetValue("SourceNodeId", out var snidObj))
                    first.SourceNodeId = GetStringFromJsonValue(snidObj);
                if (properties.TryGetValue("SourceOutputKey", out var sokObj))
                    first.SourceOutputKey = GetStringFromJsonValue(sokObj);
                if (properties.TryGetValue("InputKeyOverride", out var ikoObj))
                    first.InputKeyOverride = GetStringFromJsonValue(ikoObj);
            }
            if (properties.TryGetValue("HtmlCode", out var htmlObj))
                htmlUiNode.HtmlCode = htmlObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("JsCode", out var jsObj))
                htmlUiNode.JsCode = jsObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CssCode", out var cssObj))
                htmlUiNode.CssCode = cssObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ParamsCode", out var paramsObj))
                htmlUiNode.ParamsCode = paramsObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("OutputKeys", out var okObj))
            {
                List<string>? keys = null;
                if (okObj is string jsonKeys)
                {
                    try { keys = JsonSerializer.Deserialize<List<string>>(jsonKeys); } catch { }
                }
                else if (okObj is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Array)
                    {
                        try { keys = JsonSerializer.Deserialize<List<string>>(je.GetRawText()); } catch { }
                    }
                    else if (je.ValueKind == JsonValueKind.String)
                    {
                        try { keys = JsonSerializer.Deserialize<List<string>>(je.GetString() ?? "[]"); } catch { }
                    }
                }
                if (keys != null)
                    htmlUiNode.OutputKeys = keys;
                htmlUiNode.RebuildDynamicOutputs();
            }
            if (properties.TryGetValue("Width", out var widthObj))
            {
                if (double.TryParse(widthObj?.ToString(), out var width))
                    htmlUiNode.Width = Math.Max(280, width);
            }
            if (properties.TryGetValue("Height", out var heightObj))
            {
                if (double.TryParse(heightObj?.ToString(), out var height))
                    htmlUiNode.Height = Math.Max(200, height);
            }
            // ── WebTab properties ──
            if (properties.TryGetValue("UseWebTab", out var uwtObj) && uwtObj != null && bool.TryParse(uwtObj.ToString(), out var uwt))
                htmlUiNode.UseWebTab = uwt;
            if (properties.TryGetValue("WebTabUrl", out var wtuObj))
                htmlUiNode.WebTabUrl = wtuObj?.ToString();
            if (properties.TryGetValue("WebTabCookieSourceNodeId", out var wtcsnObj))
                htmlUiNode.WebTabCookieSourceNodeId = wtcsnObj?.ToString();
            if (properties.TryGetValue("WebTabCookieSourceOutputKey", out var wtcsokObj))
                htmlUiNode.WebTabCookieSourceOutputKey = wtcsokObj?.ToString();
            if (properties.TryGetValue("WebTabAutoRefreshEnabled", out var wtareObj) && wtareObj != null && bool.TryParse(wtareObj.ToString(), out var wtare))
                htmlUiNode.WebTabAutoRefreshEnabled = wtare;
            if (properties.TryGetValue("WebTabAutoRefreshInterval", out var wtariObj) && wtariObj != null && int.TryParse(wtariObj.ToString(), out var wtari))
                htmlUiNode.WebTabAutoRefreshInterval = wtari;
            if (properties.TryGetValue("WebTabAutoRefreshUnit", out var wtaruObj) && wtaruObj != null)
                htmlUiNode.WebTabAutoRefreshUnit = wtaruObj.ToString() ?? htmlUiNode.WebTabAutoRefreshUnit;
            // ── Offline Assets (JS/CSS libraries) ──
            if (properties.TryGetValue("OfflineAssets", out var oaObj) && oaObj != null)
            {
                try
                {
                    var rawJson = oaObj is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : oaObj.ToString();

                    if (!string.IsNullOrWhiteSpace(rawJson))
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(rawJson);
                        if (parsed != null)
                        {
                            var list = new List<FlowMy.Models.HtmlOfflineAsset>();
                            foreach (var d in parsed)
                            {
                                var asset = new FlowMy.Models.HtmlOfflineAsset();
                                if (d.TryGetValue("Id", out var idEl)) asset.Id = GetStringFromJsonValue(idEl) ?? asset.Id;
                                if (d.TryGetValue("Title", out var titleEl)) asset.Title = GetStringFromJsonValue(titleEl) ?? string.Empty;
                                if (d.TryGetValue("Description", out var descEl)) asset.Description = GetStringFromJsonValue(descEl) ?? string.Empty;
                                if (d.TryGetValue("SourceUrl", out var urlEl)) asset.SourceUrl = GetStringFromJsonValue(urlEl) ?? string.Empty;
                                if (d.TryGetValue("LocalFileName", out var fnEl)) asset.LocalFileName = GetStringFromJsonValue(fnEl) ?? string.Empty;
                                if (d.TryGetValue("AssetType", out var typeEl)) asset.AssetType = GetStringFromJsonValue(typeEl) ?? "js";
                                if (d.TryGetValue("IsEnabled", out var enabledEl))
                                {
                                    if (enabledEl.ValueKind == JsonValueKind.True) asset.IsEnabled = true;
                                    else if (enabledEl.ValueKind == JsonValueKind.False) asset.IsEnabled = false;
                                    else if (bool.TryParse(enabledEl.ToString(), out var b)) asset.IsEnabled = b;
                                }
                                list.Add(asset);
                            }
                            htmlUiNode.OfflineAssets = list;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deserializing OfflineAssets: {ex.Message}");
                }
            }
            // ── AsyncDataSources (Async Data Receiver) ──
            if (properties.TryGetValue("AsyncDataSources", out var adsObj) && adsObj != null)
            {
                try
                {
                    var rawJson = adsObj is JsonElement adsJe
                        ? (adsJe.ValueKind == JsonValueKind.String ? adsJe.GetString() : adsJe.GetRawText())
                        : adsObj.ToString();

                    if (!string.IsNullOrWhiteSpace(rawJson))
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(rawJson);
                        if (parsed != null)
                        {
                            var list = new List<AsyncDataSource>();
                            foreach (var d in parsed)
                            {
                                var ads = new AsyncDataSource();
                                if (d.TryGetValue("SourceNodeId", out var sni)) ads.SourceNodeId = GetStringFromJsonValue(sni) ?? string.Empty;
                                if (d.TryGetValue("SourceOutputKey", out var sok)) ads.SourceOutputKey = GetStringFromJsonValue(sok) ?? string.Empty;
                                if (d.TryGetValue("ReceiverKey", out var rk)) ads.ReceiverKey = GetStringFromJsonValue(rk) ?? string.Empty;
                                list.Add(ads);
                            }
                            htmlUiNode.AsyncDataSources = list;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deserializing AsyncDataSources: {ex.Message}");
                }
            }
        }
        else if (node is DataFetcherNode fetcherNodeRestore)
        {
            if (properties.TryGetValue("SourceNodeId", out var snObj))
                fetcherNodeRestore.SourceNodeId = snObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("SourceOutputKey", out var sokObj))
                fetcherNodeRestore.SourceOutputKey = sokObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("WaitForWebNodeLoad", out var wwObj) && wwObj != null && bool.TryParse(wwObj.ToString(), out var ww))
                fetcherNodeRestore.WaitForWebNodeLoad = ww;
            if (properties.TryGetValue("EnableTimer", out var etObj) && etObj != null && bool.TryParse(etObj.ToString(), out var et))
                fetcherNodeRestore.EnableTimer = et;
            if (properties.TryGetValue("TimerIntervalValue", out var tivObj) && tivObj != null && double.TryParse(tivObj.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tiv))
                fetcherNodeRestore.TimerIntervalValue = (int)tiv;
            if (properties.TryGetValue("TimerUnit", out var tuObj) && tuObj != null)
                fetcherNodeRestore.TimerUnit = tuObj.ToString() ?? fetcherNodeRestore.TimerUnit;
            if (properties.TryGetValue("EnableRealtime", out var erObj) && erObj != null && bool.TryParse(erObj.ToString(), out var er))
                fetcherNodeRestore.EnableRealtime = er;
            if (properties.TryGetValue("EnableDataReadyScan", out var edrsObj) && edrsObj != null && bool.TryParse(edrsObj.ToString(), out var edrs))
                fetcherNodeRestore.EnableDataReadyScan = edrs;
            if (properties.TryGetValue("DataReadyScanIntervalValue", out var drsivObj) && drsivObj != null &&
                double.TryParse(drsivObj.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var drsiv))
                fetcherNodeRestore.DataReadyScanIntervalValue = (int)drsiv;
            if (properties.TryGetValue("DataReadyScanUnit", out var drsuObj) && drsuObj != null)
                fetcherNodeRestore.DataReadyScanUnit = drsuObj.ToString() ?? fetcherNodeRestore.DataReadyScanUnit;
            if (properties.TryGetValue("DataReadyScanKeys", out var drskObj) && drskObj != null)
            {
                try
                {
                    if (drskObj is string jsonKeys && !string.IsNullOrWhiteSpace(jsonKeys))
                    {
                        var keys = JsonSerializer.Deserialize<List<string>>(jsonKeys);
                        if (keys != null)
                            fetcherNodeRestore.DataReadyScanKeys = keys;
                    }
                    else if (drskObj is JsonElement drskEl && drskEl.ValueKind == JsonValueKind.Array)
                    {
                        var keys = JsonSerializer.Deserialize<List<string>>(drskEl.GetRawText());
                        if (keys != null)
                            fetcherNodeRestore.DataReadyScanKeys = keys;
                    }
                    else if (drskObj is JsonElement drskElStr && drskElStr.ValueKind == JsonValueKind.String)
                    {
                        var jsonKeys2 = drskElStr.GetString();
                        if (!string.IsNullOrWhiteSpace(jsonKeys2))
                        {
                            var keys = JsonSerializer.Deserialize<List<string>>(jsonKeys2);
                            if (keys != null)
                                fetcherNodeRestore.DataReadyScanKeys = keys;
                        }
                    }
                }
                catch { }
            }
            if (properties.TryGetValue("RunSourceNodeFirst", out var rsnfObj) && rsnfObj != null && bool.TryParse(rsnfObj.ToString(), out var rsnf))
                fetcherNodeRestore.RunSourceNodeFirst = rsnf;
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj) && tdmObj != null && Enum.TryParse<TitleDisplayMode>(tdmObj.ToString(), out var tdm))
                fetcherNodeRestore.TitleDisplayMode = tdm;
            if (properties.TryGetValue("TitleColorMode", out var tcmObj) && tcmObj != null && Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
                fetcherNodeRestore.TitleColorMode = tcm;
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
                fetcherNodeRestore.TitleColorKey = tckObj?.ToString();
        }
        else if (node is FileDownloadNode fdRestore)
        {
            if (properties.TryGetValue("TitleDisplayMode", out var tdmFd) && tdmFd != null && Enum.TryParse<TitleDisplayMode>(tdmFd.ToString(), out var tdmF))
                fdRestore.TitleDisplayMode = tdmF;
            if (properties.TryGetValue("TitleColorMode", out var tcmFd) && tcmFd != null && Enum.TryParse<TitleColorMode>(tcmFd.ToString(), out var tcmF))
                fdRestore.TitleColorMode = tcmF;
            if (properties.TryGetValue("TitleColorKey", out var tckFd))
                fdRestore.TitleColorKey = tckFd?.ToString();
            if (properties.TryGetValue("FileNameTemplate", out var fnt))
                fdRestore.FileNameTemplate = fnt?.ToString() ?? fdRestore.FileNameTemplate;
            if (properties.TryGetValue("MaxFileNameLength", out var mfnl) && mfnl != null && int.TryParse(mfnl.ToString(), out var mfn) && mfn >= 1)
                fdRestore.MaxFileNameLength = mfn;
            if (properties.TryGetValue("AutoIncrementIfExists", out var aii) && aii != null && bool.TryParse(aii.ToString(), out var aib))
                fdRestore.AutoIncrementIfExists = aib;
            if (properties.TryGetValue("RemoveDiacriticsFromFileName", out var rdfn) && rdfn != null && bool.TryParse(rdfn.ToString(), out var removeDia))
                fdRestore.RemoveDiacriticsFromFileName = removeDia;
            if (properties.TryGetValue("DownloadUrl", out var du))
                fdRestore.DownloadUrl = du?.ToString() ?? string.Empty;
            if (properties.TryGetValue("UrlSourceNodeId", out var usn))
                fdRestore.UrlSourceNodeId = usn?.ToString();
            if (properties.TryGetValue("UrlSourceOutputKey", out var usk))
                fdRestore.UrlSourceOutputKey = usk?.ToString();
            if (properties.TryGetValue("CurlCommand", out var cc))
                fdRestore.CurlCommand = cc?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CurlSourceNodeId", out var csn))
                fdRestore.CurlSourceNodeId = csn?.ToString();
            if (properties.TryGetValue("CurlSourceOutputKey", out var csk))
                fdRestore.CurlSourceOutputKey = csk?.ToString();
            if (properties.TryGetValue("DownloadFolderPath", out var dfp))
                fdRestore.DownloadFolderPath = dfp?.ToString() ?? string.Empty;
            if (properties.TryGetValue("FolderSourceNodeId", out var fsn))
                fdRestore.FolderSourceNodeId = fsn?.ToString();
            if (properties.TryGetValue("FolderSourceOutputKey", out var fsk))
                fdRestore.FolderSourceOutputKey = fsk?.ToString();
            if (properties.TryGetValue("FileNameSourceNodeId", out var nfsn))
                fdRestore.FileNameSourceNodeId = nfsn?.ToString();
            if (properties.TryGetValue("FileNameSourceOutputKey", out var nfsk))
                fdRestore.FileNameSourceOutputKey = nfsk?.ToString();
            if (properties.TryGetValue("SaveAdditionalOutputFiles", out var saof) && saof != null && bool.TryParse(saof.ToString(), out var saofB))
                fdRestore.SaveAdditionalOutputFiles = saofB;
            if (properties.TryGetValue("AdditionalOutputDefaultNameTemplate", out var aodnt))
                fdRestore.AdditionalOutputDefaultNameTemplate = string.IsNullOrWhiteSpace(aodnt?.ToString()) ? null : aodnt.ToString();
            if (properties.TryGetValue("AdditionalOutputSaves", out var aosObj) && aosObj != null)
            {
                try
                {
                    var raw = aosObj is JsonElement je && je.ValueKind == JsonValueKind.String
                        ? je.GetString()
                        : aosObj.ToString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var list = JsonSerializer.Deserialize<List<FileDownloadAdditionalOutputSaveEntry>>(raw);
                        if (list != null)
                            fdRestore.AdditionalOutputSaves = list;
                    }
                }
                catch { }
            }
        }
        else if (node is FolderFilePathsNode ffpRestore)
        {
            if (properties.TryGetValue("TitleDisplayMode", out var tdmFfp) && tdmFfp != null && Enum.TryParse<TitleDisplayMode>(tdmFfp.ToString(), out var tdmFfpV))
                ffpRestore.TitleDisplayMode = tdmFfpV;
            if (properties.TryGetValue("TitleColorMode", out var tcmFfp) && tcmFfp != null && Enum.TryParse<TitleColorMode>(tcmFfp.ToString(), out var tcmFfpV))
                ffpRestore.TitleColorMode = tcmFfpV;
            if (properties.TryGetValue("TitleColorKey", out var tckFfp))
                ffpRestore.TitleColorKey = tckFfp?.ToString();
            if (properties.TryGetValue("FolderPath", out var fpObj))
                ffpRestore.FolderPath = fpObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("FolderSourceNodeId", out var fsnFfp))
                ffpRestore.FolderSourceNodeId = fsnFfp?.ToString();
            if (properties.TryGetValue("FolderSourceOutputKey", out var fskFfp))
                ffpRestore.FolderSourceOutputKey = fskFfp?.ToString();
            if (properties.TryGetValue("RefreshFolderSourceNodeBeforeUse", out var rfsn) && rfsn != null && bool.TryParse(rfsn.ToString(), out var rfsnB))
                ffpRestore.RefreshFolderSourceNodeBeforeUse = rfsnB;
            if (properties.TryGetValue("IncludeSubfolders", out var isub) && isub != null && bool.TryParse(isub.ToString(), out var isubB))
                ffpRestore.IncludeSubfolders = isubB;
            if (properties.TryGetValue("ExtensionFilterText", out var eft))
                ffpRestore.ExtensionFilterText = eft?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ExtensionTags", out var etObj) && etObj != null)
            {
                try
                {
                    if (etObj is string etJson && !string.IsNullOrWhiteSpace(etJson))
                    {
                        var tags = JsonSerializer.Deserialize<List<string>>(etJson);
                        if (tags != null)
                        {
                            ffpRestore.ExtensionTags.Clear();
                            foreach (var t in tags.Where(x => !string.IsNullOrWhiteSpace(x)))
                                ffpRestore.ExtensionTags.Add(t.Trim());
                        }
                    }
                    else if (etObj is JsonElement etJe && etJe.ValueKind == JsonValueKind.Array)
                    {
                        var tags = JsonSerializer.Deserialize<List<string>>(etJe.GetRawText());
                        if (tags != null)
                        {
                            ffpRestore.ExtensionTags.Clear();
                            foreach (var t in tags.Where(x => !string.IsNullOrWhiteSpace(x)))
                                ffpRestore.ExtensionTags.Add(t.Trim());
                        }
                    }
                }
                catch { /* ignore */ }
            }
            if (properties.TryGetValue("ReadFileContents", out var rfc) && rfc != null && bool.TryParse(rfc.ToString(), out var rfcb))
                ffpRestore.ReadFileContents = rfcb;
            if (properties.TryGetValue("ReadContentExtensionsText", out var rce))
                ffpRestore.ReadContentExtensionsText = string.IsNullOrWhiteSpace(rce?.ToString()) ? ".txt" : rce.ToString()!;
        }
        else if (node is KeyValueBridgeNode kvRestore)
        {
            if (properties.TryGetValue("mode", out var modeObj) && modeObj != null)
            {
                var m = modeObj.ToString()?.Trim();
                kvRestore.IsPassKeyMode = !string.Equals(m, "get", StringComparison.OrdinalIgnoreCase);
            }
            if (properties.TryGetValue("IsPassKeyMode", out var ipkmObj) && ipkmObj != null && bool.TryParse(ipkmObj.ToString(), out var ipkm))
                kvRestore.IsPassKeyMode = ipkm;
            if (properties.TryGetValue("key", out var keyObj))
                kvRestore.KvChannelKey = keyObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("KvChannelKey", out var kchObj))
                kvRestore.KvChannelKey = kchObj?.ToString() ?? kvRestore.KvChannelKey;
            if (properties.TryGetValue("selectedSourceNodeId", out var ssnObj))
                kvRestore.SelectedSourceBridgeNodeId = ssnObj?.ToString();
            if (properties.TryGetValue("SelectedSourceBridgeNodeId", out var ssbObj))
                kvRestore.SelectedSourceBridgeNodeId = ssbObj?.ToString() ?? kvRestore.SelectedSourceBridgeNodeId;
            if (properties.TryGetValue("interval", out var intObj) && intObj != null && int.TryParse(intObj.ToString(), out var intv))
                kvRestore.PollIntervalValue = intv;
            if (properties.TryGetValue("PollIntervalValue", out var pivObj) && pivObj != null && int.TryParse(pivObj.ToString(), out var piv))
                kvRestore.PollIntervalValue = piv;
            if (properties.TryGetValue("intervalUnit", out var iuObj) && iuObj != null &&
                Enum.TryParse<KeyValueBridgePollUnit>(iuObj.ToString(), out var iu))
                kvRestore.PollIntervalUnit = iu;
            if (properties.TryGetValue("PollIntervalUnit", out var piuObj) && piuObj != null &&
                Enum.TryParse<KeyValueBridgePollUnit>(piuObj.ToString(), out var piu))
                kvRestore.PollIntervalUnit = piu;
            if (properties.TryGetValue("TitleDisplayMode", out var tdmKvbObj) && tdmKvbObj != null &&
                Enum.TryParse<TitleDisplayMode>(tdmKvbObj.ToString(), out var tdmKvb))
                kvRestore.TitleDisplayMode = tdmKvb;
            if (properties.TryGetValue("TitleColorMode", out var tcmKvbObj) && tcmKvbObj != null &&
                Enum.TryParse<TitleColorMode>(tcmKvbObj.ToString(), out var tcmKvb))
                kvRestore.TitleColorMode = tcmKvb;
            if (properties.TryGetValue("TitleColorKey", out var tckKvbObj))
                kvRestore.TitleColorKey = tckKvbObj?.ToString();

            if (properties.TryGetValue("EnableDataCleanup", out var edcObj) && edcObj != null &&
                bool.TryParse(edcObj.ToString(), out var edc))
                kvRestore.EnableDataCleanup = edc;
            if (properties.TryGetValue("CleanupTargetBridgeNodeId", out var ctbnObj))
                kvRestore.CleanupTargetBridgeNodeId = ctbnObj?.ToString();
            if (properties.TryGetValue("CleanupTargetKey", out var ctkObj))
                kvRestore.CleanupTargetKey = ctkObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CleanupClearAllNodeData", out var ccandObj) && ccandObj != null &&
                bool.TryParse(ccandObj.ToString(), out var ccand))
                kvRestore.CleanupClearAllNodeData = ccand;
            if (properties.TryGetValue("CleanupArrayFilterField", out var caffObj))
                kvRestore.CleanupArrayFilterField = caffObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CleanupArrayFilterValue", out var cafvObj))
                kvRestore.CleanupArrayFilterValue = cafvObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CleanupRemoveAllMatchedArrayItems", out var cramObj) && cramObj != null &&
                bool.TryParse(cramObj.ToString(), out var cram))
                kvRestore.CleanupRemoveAllMatchedArrayItems = cram;
            if (properties.TryGetValue("CleanupTriggerSourceNodeId", out var ctsnObj))
                kvRestore.CleanupTriggerSourceNodeId = ctsnObj?.ToString();
            if (properties.TryGetValue("CleanupTriggerSourceOutputKey", out var ctskObj))
                kvRestore.CleanupTriggerSourceOutputKey = ctskObj?.ToString();
            if (properties.TryGetValue("CleanupTriggerExpectedValue", out var ctevObj))
                kvRestore.CleanupTriggerExpectedValue = ctevObj?.ToString() ?? "true";
            if (properties.TryGetValue("CleanupKeySourceNodeId", out var cksnObj))
                kvRestore.CleanupKeySourceNodeId = cksnObj?.ToString();
            if (properties.TryGetValue("CleanupKeySourceOutputKey", out var ckskObj))
                kvRestore.CleanupKeySourceOutputKey = ckskObj?.ToString();
            if (properties.TryGetValue("CleanupFilterFieldSourceNodeId", out var cffsnObj))
                kvRestore.CleanupFilterFieldSourceNodeId = cffsnObj?.ToString();
            if (properties.TryGetValue("CleanupFilterFieldSourceOutputKey", out var cffskObj))
                kvRestore.CleanupFilterFieldSourceOutputKey = cffskObj?.ToString();
            if (properties.TryGetValue("CleanupFilterValueSourceNodeId", out var cfvsnObj))
                kvRestore.CleanupFilterValueSourceNodeId = cfvsnObj?.ToString();
            if (properties.TryGetValue("CleanupFilterValueSourceOutputKey", out var cfvskObj))
                kvRestore.CleanupFilterValueSourceOutputKey = cfvskObj?.ToString();

            if (properties.TryGetValue("AdditionalAppendSources", out var aasObj))
            {
                List<KeyValueBridgeAppendSource>? parsedAppendSources = null;

                if (aasObj is string aasJson)
                {
                    try
                    {
                        var rawList = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(aasJson);
                        if (rawList != null)
                        {
                            parsedAppendSources = rawList
                                .Select(item => new KeyValueBridgeAppendSource
                                {
                                    SourceNodeId = item.TryGetValue("SourceNodeId", out var nodeIdObj)
                                        ? nodeIdObj?.ToString() ?? string.Empty
                                        : string.Empty,
                                    SourceOutputKey = item.TryGetValue("SourceOutputKey", out var outputKeyObj)
                                        ? outputKeyObj?.ToString()
                                        : null
                                })
                                .Where(x => !string.IsNullOrWhiteSpace(x.SourceNodeId))
                                .ToList();
                        }
                    }
                    catch
                    {
                        try
                        {
                            parsedAppendSources = JsonSerializer.Deserialize<List<KeyValueBridgeAppendSource>>(aasJson)?
                                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                                .Select(x => new KeyValueBridgeAppendSource
                                {
                                    SourceNodeId = x.SourceNodeId?.Trim() ?? string.Empty,
                                    SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                                })
                                .ToList();
                        }
                        catch
                        {
                            // Ignore invalid AdditionalAppendSources format.
                        }
                    }
                }
                else if (aasObj is JsonElement aasJe)
                {
                    try
                    {
                        if (aasJe.ValueKind == JsonValueKind.String)
                        {
                            var s = aasJe.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                var rawList = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(s);
                                if (rawList != null)
                                {
                                    parsedAppendSources = rawList
                                        .Select(item => new KeyValueBridgeAppendSource
                                        {
                                            SourceNodeId = item.TryGetValue("SourceNodeId", out var nodeIdObj)
                                                ? nodeIdObj?.ToString() ?? string.Empty
                                                : string.Empty,
                                            SourceOutputKey = item.TryGetValue("SourceOutputKey", out var outputKeyObj)
                                                ? outputKeyObj?.ToString()
                                                : null
                                        })
                                        .Where(x => !string.IsNullOrWhiteSpace(x.SourceNodeId))
                                        .ToList();
                                }
                            }
                        }
                        else if (aasJe.ValueKind == JsonValueKind.Array)
                        {
                            parsedAppendSources = JsonSerializer.Deserialize<List<KeyValueBridgeAppendSource>>(aasJe.GetRawText())?
                                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                                .Select(x => new KeyValueBridgeAppendSource
                                {
                                    SourceNodeId = x.SourceNodeId?.Trim() ?? string.Empty,
                                    SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                                })
                                .ToList();
                        }
                    }
                    catch
                    {
                        // Ignore invalid AdditionalAppendSources format.
                    }
                }

                if (parsedAppendSources != null)
                    kvRestore.AdditionalAppendSources = parsedAppendSources;
            }

            kvRestore.RebuildDataPorts();
            kvRestore.RefreshFlowPortsVisibility();
        }
        else if (node is FlowOverwriteNode flowOverwriteRestore)
        {
            if (properties.TryGetValue("OutputKey", out var outputKeyObj))
                flowOverwriteRestore.OutputKey = outputKeyObj?.ToString() ?? "outputKey";
            if (properties.TryGetValue("AppendMode", out var appendObj) &&
                appendObj != null &&
                bool.TryParse(appendObj.ToString(), out var appendMode))
            {
                flowOverwriteRestore.AppendMode = appendMode;
            }
            if (properties.TryGetValue("IncludeIndirectSources", out var includeIndirectObj) &&
                includeIndirectObj != null &&
                bool.TryParse(includeIndirectObj.ToString(), out var includeIndirect))
            {
                flowOverwriteRestore.IncludeIndirectSources = includeIndirect;
            }
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj) &&
                tdmObj != null &&
                Enum.TryParse<TitleDisplayMode>(tdmObj.ToString(), out var tdm))
            {
                flowOverwriteRestore.TitleDisplayMode = tdm;
            }
            if (properties.TryGetValue("TitleColorMode", out var tcmObj) &&
                tcmObj != null &&
                Enum.TryParse<TitleColorMode>(tcmObj.ToString(), out var tcm))
            {
                flowOverwriteRestore.TitleColorMode = tcm;
            }
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
                flowOverwriteRestore.TitleColorKey = tckObj?.ToString();

            if (properties.TryGetValue("Mappings", out var mappingsObj))
            {
                List<FlowOverwriteMapping>? parsedMappings = null;
                try
                {
                    if (mappingsObj is string mappingsJson && !string.IsNullOrWhiteSpace(mappingsJson))
                        parsedMappings = JsonSerializer.Deserialize<List<FlowOverwriteMapping>>(mappingsJson);
                    else if (mappingsObj is JsonElement mappingsElement)
                    {
                        if (mappingsElement.ValueKind == JsonValueKind.String)
                        {
                            var json = mappingsElement.GetString();
                            if (!string.IsNullOrWhiteSpace(json))
                                parsedMappings = JsonSerializer.Deserialize<List<FlowOverwriteMapping>>(json);
                        }
                        else if (mappingsElement.ValueKind == JsonValueKind.Array)
                        {
                            parsedMappings = JsonSerializer.Deserialize<List<FlowOverwriteMapping>>(mappingsElement.GetRawText());
                        }
                    }
                }
                catch { }

                if (parsedMappings != null)
                {
                    flowOverwriteRestore.Mappings = parsedMappings
                        .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                        .Select(x => new FlowOverwriteMapping
                        {
                            SourceNodeId = x.SourceNodeId.Trim(),
                            SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                        })
                        .ToList();
                }
            }

            flowOverwriteRestore.RebuildDynamicOutputs();
        }
        else if (node is OutputNode outputNode)
        {
            // Deserialize OutputKey
            if (properties.TryGetValue("OutputKey", out var outputKeyObj))
                outputNode.OutputKey = outputKeyObj?.ToString() ?? "output";
            
            // Deserialize FormatString
            if (properties.TryGetValue("FormatString", out var formatStrObj))
                outputNode.FormatString = formatStrObj?.ToString() ?? string.Empty;
            
            // Deserialize InputVariables
            if (properties.TryGetValue("InputVariables", out var variablesObj))
            {
                List<InputVariable>? parsedVariables = null;

                if (variablesObj is string jsonVariables)
                {
                    try
                    {
                        var variableData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonVariables);
                        if (variableData != null)
                        {
                            parsedVariables = variableData.Select(v => new InputVariable
                            {
                                VariableKey = v.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? string.Empty : string.Empty,
                                SourceNodeId = v.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                                SourceOutputKey = v.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                            }).ToList();
                        }
                    }
                    catch
                    {
                        // Try alternative format (direct deserialize)
                        try
                        {
                            parsedVariables = JsonSerializer.Deserialize<List<InputVariable>>(jsonVariables);
                        }
                        catch { }
                    }
                }
                else if (variablesObj is JsonElement jsonElement)
                {
                    try
                    {
                        if (jsonElement.ValueKind == JsonValueKind.String)
                        {
                            var jsonString = jsonElement.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonString))
                            {
                                var variableData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonString);
                                if (variableData != null)
                                {
                                    parsedVariables = variableData.Select(v => new InputVariable
                                    {
                                        VariableKey = v.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? string.Empty : string.Empty,
                                        SourceNodeId = v.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                                        SourceOutputKey = v.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                                    }).ToList();
                                }
                            }
                        }
                        else if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            parsedVariables = JsonSerializer.Deserialize<List<InputVariable>>(jsonElement.GetRawText());
                        }
                    }
                    catch { }
                }

                if (parsedVariables != null)
                {
                    outputNode.InputVariables = parsedVariables;
                    outputNode.RebuildDynamicOutputs();
                }
            }
            
            // Deserialize TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                var tdmStr = tdmObj?.ToString();
                if (!string.IsNullOrWhiteSpace(tdmStr) &&
                    Enum.TryParse<TitleDisplayMode>(tdmStr, out var titleDisplayMode))
                {
                    outputNode.TitleDisplayMode = titleDisplayMode;
                }
            }

            // Deserialize TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                if (Enum.TryParse<TitleColorMode>(tcmObj?.ToString(), out var tcm))
                    outputNode.TitleColorMode = tcm;
            }

            // Deserialize TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                outputNode.TitleColorKey = tckObj?.ToString();
            }
        }
        else if (node is NotificationNode notificationNode)
        {
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                var tdmStr = tdmObj?.ToString();
                if (!string.IsNullOrWhiteSpace(tdmStr) &&
                    Enum.TryParse<TitleDisplayMode>(tdmStr, out var titleDisplayMode))
                {
                    notificationNode.TitleDisplayMode = titleDisplayMode;
                }
            }

            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                var tcmStr = tcmObj?.ToString();
                if (!string.IsNullOrWhiteSpace(tcmStr) &&
                    Enum.TryParse<TitleColorMode>(tcmStr, out var titleColorMode))
                {
                    notificationNode.TitleColorMode = titleColorMode;
                }
            }

            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                notificationNode.TitleColorKey = tckObj?.ToString();
            }

            if (properties.TryGetValue("DefaultDurationSeconds", out var durObj) &&
                int.TryParse(durObj?.ToString(), out var dur))
            {
                notificationNode.DefaultDurationSeconds = dur;
            }

            // TitleInput
            if (properties.TryGetValue("TitleInput", out var titleInputObj) && titleInputObj is string titleJson)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(titleJson);
                    if (dict != null)
                    {
                        notificationNode.TitleInput = new InputVariable
                        {
                            VariableKey = dict.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? "title" : "title",
                            SourceNodeId = dict.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                            SourceOutputKey = dict.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                        };
                    }
                }
                catch
                {
                }
            }

            // ContentInput
            if (properties.TryGetValue("ContentInput", out var contentInputObj) && contentInputObj is string contentJson)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(contentJson);
                    if (dict != null)
                    {
                        notificationNode.ContentInput = new InputVariable
                        {
                            VariableKey = dict.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? "content" : "content",
                            SourceNodeId = dict.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                            SourceOutputKey = dict.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                        };
                    }
                }
                catch
                {
                }
            }

            // DurationInput
            if (properties.TryGetValue("DurationInput", out var durationInputObj) && durationInputObj is string durationJson)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(durationJson);
                    if (dict != null)
                    {
                        notificationNode.DurationInput = new InputVariable
                        {
                            VariableKey = dict.TryGetValue("VariableKey", out var vk) ? vk?.ToString() ?? "duration" : "duration",
                            SourceNodeId = dict.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                            SourceOutputKey = dict.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                        };
                    }
                }
                catch
                {
                }
            }

            if (properties.TryGetValue("StaticTitle", out var staticTitleObj))
            {
                notificationNode.StaticTitle = staticTitleObj?.ToString() ?? string.Empty;
            }

            if (properties.TryGetValue("StaticContent", out var staticContentObj))
            {
                notificationNode.StaticContent = staticContentObj?.ToString() ?? string.Empty;
            }

            if (properties.TryGetValue("ToastTitleColorKey", out var toastTitleColorKeyObj))
            {
                notificationNode.ToastTitleColorKey = toastTitleColorKeyObj?.ToString();
            }

            if (properties.TryGetValue("ToastContentColorKey", out var toastContentColorKeyObj))
            {
                notificationNode.ToastContentColorKey = toastContentColorKeyObj?.ToString();
            }

            if (properties.TryGetValue("ToastBackgroundColorKey", out var toastBackgroundColorKeyObj))
            {
                notificationNode.ToastBackgroundColorKey = toastBackgroundColorKeyObj?.ToString();
            }

            if (properties.TryGetValue("ToastBackgroundOpacity", out var toastOpacityObj) &&
                double.TryParse(toastOpacityObj?.ToString(), out var parsedOpacity))
            {
                notificationNode.ToastBackgroundOpacity = parsedOpacity;
            }
        }


        // Deserialize ReuseRoutes (áp dụng chung cho mọi loại node)
        if (properties.TryGetValue("ReuseRoutes", out var rrObj) && rrObj != null)
        {
            try
            {
                List<NodeReuseRoute>? parsed = null;

                if (rrObj is string s && !string.IsNullOrWhiteSpace(s))
                {
                    parsed = JsonSerializer.Deserialize<List<NodeReuseRoute>>(s);
                }
                else if (rrObj is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.String)
                    {
                        var s2 = je.GetString();
                        if (!string.IsNullOrWhiteSpace(s2))
                        {
                            parsed = JsonSerializer.Deserialize<List<NodeReuseRoute>>(s2);
                        }
                    }
                    else if (je.ValueKind == JsonValueKind.Array)
                    {
                        parsed = JsonSerializer.Deserialize<List<NodeReuseRoute>>(je.GetRawText());
                    }
                }

                if (parsed != null)
                {
                    node.ReuseRoutes = parsed;
                }
            }
            catch
            {
                // Nếu parse lỗi thì bỏ qua, không chặn load workflow
            }
        }

        if (node.DynamicInputs != null && node.DynamicInputs.Count > 0)
        {
            foreach (var inp in node.DynamicInputs)
            {
                var key = inp.Key ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (properties.TryGetValue($"DynIn_{key}_SrcNode", out var srcNodeObj))
                {
                    var s = srcNodeObj?.ToString();
                    inp.SelectedSourceNodeId = string.IsNullOrWhiteSpace(s) ? null : s;
                }

                if (properties.TryGetValue($"DynIn_{key}_SrcKey", out var srcKeyObj))
                {
                    var k = srcKeyObj?.ToString();
                    inp.SelectedSourceOutputKey = string.IsNullOrWhiteSpace(k) ? null : k;
                }

                if (properties.TryGetValue($"DynIn_{key}_UserKey", out var userKeyObj))
                {
                    var uk = userKeyObj?.ToString();
                    inp.UserKeyOverride = string.IsNullOrWhiteSpace(uk) ? null : uk;
                }

                if (properties.TryGetValue($"DynIn_{key}_UserValue", out var userValObj))
                {
                    var uv = userValObj?.ToString();
                    inp.UserValueOverride = string.IsNullOrWhiteSpace(uv) ? null : uv;
                }

                if (properties.TryGetValue($"DynIn_{key}_ConvType", out var ctObj))
                {
                    var ct = ctObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(ct) &&
                        Enum.TryParse<WorkflowDataType>(ct, out var parsed))
                    {
                        inp.ConvertType = parsed;
                    }
                }
            }
        }
        
        // WebNode deserialization
        if (node is WebNode webNodeRestore)
        {
            // Deserialize BlockingRules
            if (properties.TryGetValue("BlockingRules", out var brObj))
            {
                try
                {
                    List<Dictionary<string, object>>? parsedRules = null;
                    
                    if (brObj is string brJson && !string.IsNullOrWhiteSpace(brJson))
                    {
                        parsedRules = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(brJson);
                    }
                    else if (brObj is JsonElement brElement)
                    {
                        if (brElement.ValueKind == JsonValueKind.String)
                        {
                            var jsonString = brElement.GetString();
                            if (!string.IsNullOrWhiteSpace(jsonString))
                            {
                                parsedRules = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonString);
                            }
                        }
                        else if (brElement.ValueKind == JsonValueKind.Array)
                        {
                            parsedRules = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(brElement.GetRawText());
                        }
                    }
                    
                    if (parsedRules != null && parsedRules.Count > 0)
                    {
                        webNodeRestore.BlockingRules.Clear();
                        foreach (var ruleDict in parsedRules)
                        {
                            var rule = new WebBlockingRule();
                            
                            if (ruleDict.TryGetValue("UrlPattern", out var urlObj))
                                rule.UrlPattern = urlObj?.ToString() ?? string.Empty;
                            
                            if (ruleDict.TryGetValue("Method", out var methodObj))
                                rule.Method = methodObj?.ToString() ?? "All";
                            else
                                rule.Method = "All"; // Default for old workflows without Method
                            
                            // Deserialize ChildRules nếu có
                            if (ruleDict.TryGetValue("ChildRules", out var childRulesObj))
                            {
                                try
                                {
                                    List<Dictionary<string, object>>? childRulesList = null;
                                    
                                    // Handle different formats
                                    if (childRulesObj is List<object> childRulesListObj)
                                    {
                                        childRulesList = childRulesListObj
                                            .Where(c => c is Dictionary<string, object>)
                                            .Cast<Dictionary<string, object>>()
                                            .ToList();
                                    }
                                    else if (childRulesObj is JsonElement childRulesJe)
                                    {
                                        if (childRulesJe.ValueKind == JsonValueKind.Array)
                                        {
                                            childRulesList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(childRulesJe.GetRawText());
                                        }
                                        else if (childRulesJe.ValueKind == JsonValueKind.String)
                                        {
                                            var childRulesJson = childRulesJe.GetString();
                                            if (!string.IsNullOrWhiteSpace(childRulesJson))
                                            {
                                                childRulesList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(childRulesJson);
                                            }
                                        }
                                    }
                                    else if (childRulesObj is string childRulesJson && !string.IsNullOrWhiteSpace(childRulesJson))
                                    {
                                        childRulesList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(childRulesJson);
                                    }
                                    
                                    // Parse child rules
                                    if (childRulesList != null && childRulesList.Count > 0)
                                    {
                                        foreach (var childRuleDict in childRulesList)
                                        {
                                            var childRule = new WebBlockingChildRule();
                                            
                                            if (childRuleDict.TryGetValue("UrlPattern", out var childUrlObj))
                                                childRule.UrlPattern = childUrlObj?.ToString() ?? string.Empty;
                                            
                                            if (childRuleDict.TryGetValue("Method", out var childMethodObj))
                                                childRule.Method = childMethodObj?.ToString() ?? "All";
                                            else
                                                childRule.Method = "All";
                                            
                                            if (!string.IsNullOrWhiteSpace(childRule.UrlPattern))
                                                rule.ChildRules.Add(childRule);
                                        }
                                    }
                                }
                                catch (Exception childEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error deserializing ChildRules: {childEx.Message}");
                                    // Continue - don't break rule loading
                                }
                            }
                            
                            webNodeRestore.BlockingRules.Add(rule);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deserializing BlockingRules: {ex.Message}");
                    // Continue - don't break workflow loading
                }
            }
        }
        
        else if (node is HttpRequestNode httpRequestNode)
        {
            // Deserialize TitleDisplayMode
            if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
            {
                if (Enum.TryParse<TitleDisplayMode>(tdmObj?.ToString(), out var tdm))
                    httpRequestNode.TitleDisplayMode = tdm;
            }

            // Deserialize TitleColorMode
            if (properties.TryGetValue("TitleColorMode", out var tcmObj))
            {
                if (Enum.TryParse<TitleColorMode>(tcmObj?.ToString(), out var tcm))
                    httpRequestNode.TitleColorMode = tcm;
            }

            // Deserialize TitleColorKey
            if (properties.TryGetValue("TitleColorKey", out var tckObj))
            {
                httpRequestNode.TitleColorKey = tckObj?.ToString();
            }
            
            // Deserialize basic properties
            if (properties.TryGetValue("Url", out var urlObj))
                httpRequestNode.Url = urlObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("HttpMethod", out var methodObj))
            {
                if (Enum.TryParse<Models.Nodes.HttpMethod>(methodObj?.ToString(), out var method))
                    httpRequestNode.HttpMethod = method;
            }
            if (properties.TryGetValue("AuthType", out var authTypeObj))
            {
                if (Enum.TryParse<HttpAuthType>(authTypeObj?.ToString(), out var authType))
                    httpRequestNode.AuthType = authType;
            }
            if (properties.TryGetValue("BodyType", out var bodyTypeObj))
            {
                if (Enum.TryParse<HttpBodyType>(bodyTypeObj?.ToString(), out var bodyType))
                    httpRequestNode.BodyType = bodyType;
            }
            if (properties.TryGetValue("TimeoutSeconds", out var timeoutObj) && 
                int.TryParse(timeoutObj?.ToString(), out var timeout))
                httpRequestNode.TimeoutSeconds = timeout;
            
            // Deserialize URL dynamic binding
            if (properties.TryGetValue("UrlSourceNodeId", out var urlSrcNodeObj))
                httpRequestNode.UrlSourceNodeId = urlSrcNodeObj?.ToString();
            if (properties.TryGetValue("UrlSourceOutputKey", out var urlSrcKeyObj))
                httpRequestNode.UrlSourceOutputKey = urlSrcKeyObj?.ToString();
            
            // Deserialize Body
            if (properties.TryGetValue("RawBody", out var rawBodyObj))
                httpRequestNode.RawBody = rawBodyObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("BodySourceNodeId", out var bodySrcNodeObj))
                httpRequestNode.BodySourceNodeId = bodySrcNodeObj?.ToString();
            if (properties.TryGetValue("BodySourceOutputKey", out var bodySrcKeyObj))
                httpRequestNode.BodySourceOutputKey = bodySrcKeyObj?.ToString();
            
            // Deserialize Auth
            if (properties.TryGetValue("AuthUsername", out var authUserObj))
                httpRequestNode.AuthUsername = authUserObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("AuthPassword", out var authPassObj))
                httpRequestNode.AuthPassword = authPassObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("AuthToken", out var authTokenObj))
                httpRequestNode.AuthToken = authTokenObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("TokenSourceNodeId", out var tokenSrcNodeObj))
                httpRequestNode.TokenSourceNodeId = tokenSrcNodeObj?.ToString();
            if (properties.TryGetValue("TokenSourceOutputKey", out var tokenSrcKeyObj))
                httpRequestNode.TokenSourceOutputKey = tokenSrcKeyObj?.ToString();
            if (properties.TryGetValue("ApiKeyName", out var apiKeyNameObj))
                httpRequestNode.ApiKeyName = apiKeyNameObj?.ToString() ?? "X-API-Key";
            if (properties.TryGetValue("ApiKeyValue", out var apiKeyValueObj))
                httpRequestNode.ApiKeyValue = apiKeyValueObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ApiKeyValueSourceNodeId", out var apiKeyValSrcNodeObj))
                httpRequestNode.ApiKeyValueSourceNodeId = apiKeyValSrcNodeObj?.ToString();
            if (properties.TryGetValue("ApiKeyValueSourceOutputKey", out var apiKeyValSrcKeyObj))
                httpRequestNode.ApiKeyValueSourceOutputKey = apiKeyValSrcKeyObj?.ToString();
            if (properties.TryGetValue("ApiKeyInHeader", out var apiKeyInHeaderObj) &&
                bool.TryParse(apiKeyInHeaderObj?.ToString(), out var apiKeyInHeader))
                httpRequestNode.ApiKeyInHeader = apiKeyInHeader;
            
            // Deserialize Headers
            if (properties.TryGetValue("Headers", out var headersObj))
            {
                var headers = DeserializeHttpKeyValuePairs(headersObj);
                if (headers != null)
                {
                    httpRequestNode.Headers.Clear();
                    foreach (var h in headers)
                        httpRequestNode.Headers.Add(h);
                }
            }
            
            // Deserialize QueryParams
            if (properties.TryGetValue("QueryParams", out var paramsObj))
            {
                var queryParams = DeserializeHttpKeyValuePairs(paramsObj);
                if (queryParams != null)
                {
                    httpRequestNode.QueryParams.Clear();
                    foreach (var p in queryParams)
                        httpRequestNode.QueryParams.Add(p);
                }
            }
            
            // Deserialize FormData
            if (properties.TryGetValue("FormData", out var formDataObj))
            {
                var formData = DeserializeHttpKeyValuePairs(formDataObj);
                if (formData != null)
                {
                    httpRequestNode.FormData.Clear();
                    foreach (var f in formData)
                        httpRequestNode.FormData.Add(f);
                }
            }

            // cURL binding (NEW)
            if (properties.TryGetValue("CurlSourceNodeId", out var curlSrcNodeObj))
                httpRequestNode.CurlSourceNodeId = curlSrcNodeObj?.ToString();
            if (properties.TryGetValue("CurlSourceOutputKey", out var curlSrcKeyObj))
                httpRequestNode.CurlSourceOutputKey = curlSrcKeyObj?.ToString();

            // Deserialize Anti-bot / bypass (libcurl)
            if (properties.TryGetValue("UseCurl", out var useCurlObj) &&
                bool.TryParse(useCurlObj?.ToString(), out var useCurl))
            {
                httpRequestNode.UseCurl = useCurl;
            }
            if (properties.TryGetValue("CurlPath", out var curlPathObj))
            {
                httpRequestNode.CurlPath = curlPathObj?.ToString() ?? string.Empty;
            }
            if (properties.TryGetValue("ImpersonateBrowser", out var impObj))
            {
                httpRequestNode.ImpersonateBrowser = impObj?.ToString() ?? string.Empty;
            }
            if (properties.TryGetValue("AutoAppendCurlWriteOut", out var autoWriteOutObj) &&
                bool.TryParse(autoWriteOutObj?.ToString(), out var autoWriteOut))
            {
                httpRequestNode.AutoAppendCurlWriteOut = autoWriteOut;
            }
        }
    }

    private static Dictionary<string, object> GetNodeProperties(WorkflowNode node)
    {
        var dict = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(node.Condition)) dict["Condition"] = node.Condition;
        if (!string.IsNullOrEmpty(node.Key)) dict["Key"] = node.Key;
        if (node.MouseEvent.HasValue) dict["MouseEvent"] = node.MouseEvent.Value.ToString();
        if (!string.IsNullOrEmpty(node.TargetElement)) dict["TargetElement"] = node.TargetElement;

        // KeyPressEventNode serialization
        if (node is KeyPressEventNode kp)
        {
            if (kp.RepeatCount != 1)
                dict["RepeatCount"] = kp.RepeatCount;
            if (kp.PressDelayMs != 50)
                dict["PressDelayMs"] = kp.PressDelayMs;
            // Serialize TitleDisplayMode, TitleColorMode, TitleColorKey
            dict["TitleDisplayMode"] = kp.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = kp.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(kp.TitleColorKey))
                dict["TitleColorKey"] = kp.TitleColorKey;
        }
        // HotkeyPressEventNode serialization
        else if (node is HotkeyPressEventNode hk)
        {
            if (hk.RepeatCount != 1)
                dict["RepeatCount"] = hk.RepeatCount;
            if (hk.PressDelayMs != 50)
                dict["PressDelayMs"] = hk.PressDelayMs;
            // Serialize TitleDisplayMode, TitleColorMode, TitleColorKey
            dict["TitleDisplayMode"] = hk.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = hk.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(hk.TitleColorKey))
                dict["TitleColorKey"] = hk.TitleColorKey;
        }
        // StringSplitNode serialization
        else if (node is StringSplitNode stringSplit)
        {
            dict["RegexPattern"] = stringSplit.RegexPattern ?? @"\r?\n";
            dict["OutputKey"] = stringSplit.OutputKey ?? "ListItems";
            // Serialize TitleDisplayMode, TitleColorMode, TitleColorKey
            dict["TitleDisplayMode"] = stringSplit.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = stringSplit.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(stringSplit.TitleColorKey))
                dict["TitleColorKey"] = stringSplit.TitleColorKey;
        }

        if (node is LoopNode loop)
        {
            dict["LoopType"] = loop.LoopType.ToString();
            dict["RepeatCount"] = loop.RepeatCount;
            dict["StartIndex"] = loop.StartIndex;
            dict["EndIndex"] = loop.EndIndex;
            dict["ArrayInputKey"] = loop.ArrayInputKey;
            dict["InputType"] = loop.InputType.ToString();
            dict["TitleDisplayMode"] = loop.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = loop.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(loop.TitleColorKey))
                dict["TitleColorKey"] = loop.TitleColorKey;
            if (loop.CustomOutputMappings.Count > 0)
                dict["CustomOutputMappings"] = System.Text.Json.JsonSerializer.Serialize(loop.CustomOutputMappings);
            if (loop.DataAssignments.Count > 0)
                dict["DataAssignments"] = System.Text.Json.JsonSerializer.Serialize(loop.DataAssignments);
        }
        else if (node is MouseEventNode mouseNode)
        {
            dict["MouseButton"] = mouseNode.MouseButton;
            dict["RepeatCount"] = mouseNode.RepeatCount;
            dict["HoldDuration"] = mouseNode.HoldDuration;
            dict["ScrollSpeed"] = mouseNode.ScrollSpeed;
            // Serialize TitleDisplayMode, TitleColorMode, TitleColorKey
            dict["TitleDisplayMode"] = mouseNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = mouseNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(mouseNode.TitleColorKey))
                dict["TitleColorKey"] = mouseNode.TitleColorKey;
        }
        else if (node is ScreenPositionPickerNode pos)
        {
            dict["X_Pos"] = pos.SelectedPosition.X;
            dict["Y_Pos"] = pos.SelectedPosition.Y;
            dict["HasPosition"] = pos.HasPosition;
        }
        else if (node is ScreenCaptureNode cap)
        {
            dict["CaptureX"] = cap.CaptureX;
            dict["CaptureY"] = cap.CaptureY;
            dict["CaptureWidth"] = cap.CaptureWidth;
            dict["CaptureHeight"] = cap.CaptureHeight;

            var b64 = TryEncodeBitmapSourceToPngBase64(cap.CapturedImage);
            if (!string.IsNullOrWhiteSpace(b64))
            {
                dict["CapturedImageBase64"] = b64;
            }
        }
        else if (node is LoopBodyNode loopBody)
        {
            dict["Width"] = loopBody.Width;
            dict["Height"] = loopBody.Height;
        }
        else if (node is AsyncTaskBodyNode asyncTaskBodyNode)
        {
            dict["Width"] = asyncTaskBodyNode.Width;
            dict["Height"] = asyncTaskBodyNode.Height;
        }
        else if (node is AsyncTaskNode asyncTaskNode)
        {
            dict["RunInParallel"] = asyncTaskNode.RunInParallel;
            dict["UiPresentationMode"] = asyncTaskNode.UiPresentationMode.ToString();
            dict["DispatchLoopType"] = asyncTaskNode.DispatchLoopType.ToString();
            dict["RepeatCount"] = asyncTaskNode.RepeatCount;
            dict["StartIndex"] = asyncTaskNode.StartIndex;
            dict["EndIndex"] = asyncTaskNode.EndIndex;
            dict["ReadResultsInBody"] = asyncTaskNode.ReadResultsInBody;

            if (asyncTaskNode.UiPresentationMode == AsyncTaskUiPresentationMode.ManualBranches
                && asyncTaskNode.AsyncTaskBranches != null && asyncTaskNode.AsyncTaskBranches.Count > 0)
            {
                var branchesJson = JsonSerializer.Serialize(asyncTaskNode.AsyncTaskBranches.Select(b => new
                {
                    Id = b.Id,
                    Label = b.Label,
                    CanRemove = b.CanRemove
                }).ToList());
                dict["AsyncTaskBranches"] = branchesJson;
            }
        }
        else if (node is AsyncTaskDispatchCollectNode collectNode)
        {
            dict["SourceBodyNodeId"] = collectNode.SourceBodyNodeId ?? "";
            dict["SourceOutputKey"] = collectNode.SourceOutputKey ?? "";
        }
        else if (node.IsConditionalNode && node.ConditionalBranches != null && node.ConditionalBranches.Count > 0)
        {
            var branchesJson = JsonSerializer.Serialize(node.ConditionalBranches.Select(b => new
            {
                Label = b.Label,
                DisplayTitle = b.DisplayTitle,
                SatelliteOffsetX = b.SatelliteOffsetX,
                SatelliteOffsetY = b.SatelliteOffsetY,
                SatelliteInputPosition = b.SatelliteInputPosition.ToString(),
                LeftSourceNodeId = b.LeftSourceNodeId,
                LeftKey = b.LeftKey,
                Operator = b.Operator.ToString(),
                RightUseLiteralValue = b.RightUseLiteralValue,
                RightLiteralValue = b.RightLiteralValue,
                RightSourceNodeId = b.RightSourceNodeId,
                RightKey = b.RightKey,
                Condition = b.Condition,
                CanRemove = b.CanRemove,
                SubConditions = b.SubConditions?.Select(expr => new
                {
                    LeftSourceNodeId = expr.LeftSourceNodeId,
                    LeftKey = expr.LeftKey,
                    Operator = expr.Operator.ToString(),
                    RightUseLiteralValue = expr.RightUseLiteralValue,
                    RightLiteralValue = expr.RightLiteralValue,
                    RightSourceNodeId = expr.RightSourceNodeId,
                    RightKey = expr.RightKey
                }).ToList(),
                OperatorsBetween = b.OperatorsBetween?.Select(o => o.ToString()).ToList()
            }).ToList());
            dict["ConditionalBranches"] = branchesJson;
        }
        else if (node is InputNode inputNode)
        {
            if (!string.IsNullOrWhiteSpace(inputNode.Key))
                dict["InputKey"] = inputNode.Key;
            if (!string.IsNullOrWhiteSpace(inputNode.Value))
                dict["InputValue"] = inputNode.Value;
            dict["InputDataType"] = inputNode.DataType.ToString();

            if (inputNode.IsArrayType && inputNode.ArrayValues != null)
            {
                dict["InputArrayValues"] = JsonSerializer.Serialize(inputNode.ArrayValues);
            }

            // Serialize TitleDisplayMode, TitleColorMode, TitleColorKey
            dict["TitleDisplayMode"] = inputNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = inputNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(inputNode.TitleColorKey))
                dict["TitleColorKey"] = inputNode.TitleColorKey;
        }
        else if (node is DelayNode delayNode)
        {
            dict["DelayMilliseconds"] = delayNode.DelayMilliseconds;
            dict["DelayValue"] = delayNode.DelayValue;
            dict["DelayUnit"] = delayNode.DelayUnit.ToString();
            dict["TitleDisplayMode"] = delayNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = delayNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(delayNode.TitleColorKey))
                dict["TitleColorKey"] = delayNode.TitleColorKey;
            dict["TimingMode"] = delayNode.TimingMode.ToString();
            dict["RandomMinValue"] = delayNode.RandomMinValue;
            dict["RandomMaxValue"] = delayNode.RandomMaxValue;
            if (!string.IsNullOrWhiteSpace(delayNode.DelaySourceNodeId))
                dict["DelaySourceNodeId"] = delayNode.DelaySourceNodeId;
            if (!string.IsNullOrWhiteSpace(delayNode.DelaySourceOutputKey))
                dict["DelaySourceOutputKey"] = delayNode.DelaySourceOutputKey;
        }
        else if (node is CallbackNode callbackNode)
        {
            if (!string.IsNullOrWhiteSpace(callbackNode.TargetNodeId))
                dict["TargetNodeId"] = callbackNode.TargetNodeId;
            dict["MaxCallbackCount"] = callbackNode.MaxCallbackCount;
            dict["FlowBehavior"] = callbackNode.FlowBehavior.ToString();
            dict["TitleDisplayMode"] = callbackNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = callbackNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(callbackNode.TitleColorKey))
                dict["TitleColorKey"] = callbackNode.TitleColorKey;
        }
        else if (node is ListOutNode listOutNode)
        {
            // Serialize OutputMappings
            if (listOutNode.OutputMappings != null && listOutNode.OutputMappings.Count > 0)
            {
                var mappingsJson = JsonSerializer.Serialize(listOutNode.OutputMappings.Select(m => new
                {
                    NewKey = m.NewKey,
                    SourceNodeId = m.SourceNodeId,
                    SourceOutputKey = m.SourceOutputKey
                }).ToList());
                dict["OutputMappings"] = mappingsJson;
            }
            
            // Serialize TitleDisplayMode
            dict["TitleDisplayMode"] = listOutNode.TitleDisplayMode.ToString();

            // Serialize TitleColorMode and TitleColorKey
            dict["TitleColorMode"] = listOutNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(listOutNode.TitleColorKey))
                dict["TitleColorKey"] = listOutNode.TitleColorKey;
        }
        else if (node is AssignDataNode assignDataNode)
        {
            if (assignDataNode.Assignments.Count > 0)
                dict["Assignments"] = JsonSerializer.Serialize(assignDataNode.Assignments);
            dict["TitleColorMode"] = assignDataNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(assignDataNode.TitleColorKey))
                dict["TitleColorKey"] = assignDataNode.TitleColorKey;
        }
        else if (node is MediaGalleryNode mediaGalleryNode)
        {
            dict["Width"] = mediaGalleryNode.Width;
            dict["Height"] = mediaGalleryNode.Height;
            dict["FrameDisplayWidth"] = mediaGalleryNode.FrameDisplayWidth;
            dict["FrameDisplayHeight"] = mediaGalleryNode.FrameDisplayHeight;
            if (!string.IsNullOrEmpty(mediaGalleryNode.TitleKeyTemplate))
                dict["TitleKeyTemplate"] = mediaGalleryNode.TitleKeyTemplate;
            if (!string.IsNullOrEmpty(mediaGalleryNode.ImageUrlKeyTemplate))
                dict["ImageUrlKeyTemplate"] = mediaGalleryNode.ImageUrlKeyTemplate;
            if (!string.IsNullOrEmpty(mediaGalleryNode.VideoUrlKeyTemplate))
                dict["VideoUrlKeyTemplate"] = mediaGalleryNode.VideoUrlKeyTemplate;
            if (!string.IsNullOrEmpty(mediaGalleryNode.GroupArrayKey))
                dict["GroupArrayKey"] = mediaGalleryNode.GroupArrayKey;
            if (!string.IsNullOrEmpty(mediaGalleryNode.GroupTitleKey))
                dict["GroupTitleKey"] = mediaGalleryNode.GroupTitleKey;
            if (!string.IsNullOrEmpty(mediaGalleryNode.GroupItemsKey))
                dict["GroupItemsKey"] = mediaGalleryNode.GroupItemsKey;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSaveImages))
                dict["FolderSaveImages"] = mediaGalleryNode.FolderSaveImages;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSourceNodeId))
                dict["FolderSourceNodeId"] = mediaGalleryNode.FolderSourceNodeId;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSourceOutputKey))
                dict["FolderSourceOutputKey"] = mediaGalleryNode.FolderSourceOutputKey;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSaveVideos))
                dict["FolderSaveVideos"] = mediaGalleryNode.FolderSaveVideos;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSourceNodeIdVideo))
                dict["FolderSourceNodeIdVideo"] = mediaGalleryNode.FolderSourceNodeIdVideo;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSourceOutputKeyVideo))
                dict["FolderSourceOutputKeyVideo"] = mediaGalleryNode.FolderSourceOutputKeyVideo;
            if (!string.IsNullOrEmpty(mediaGalleryNode.JsonSourceNodeId))
                dict["JsonSourceNodeId"] = mediaGalleryNode.JsonSourceNodeId;
            if (!string.IsNullOrEmpty(mediaGalleryNode.JsonSourceOutputKey))
                dict["JsonSourceOutputKey"] = mediaGalleryNode.JsonSourceOutputKey;
            dict["ItemClickPreviewMode"] = mediaGalleryNode.ItemClickPreviewMode.ToString();
            dict["DisplayMode"] = mediaGalleryNode.DisplayMode.ToString();
            dict["TitleColorMode"] = mediaGalleryNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(mediaGalleryNode.TitleColorKey))
                dict["TitleColorKey"] = mediaGalleryNode.TitleColorKey;
            dict["CanReexecuteSourceNode"] = mediaGalleryNode.CanReexecuteSourceNode;
        }
        else if (node is ImageProcessingNode imageNode)
        {
            dict["Width"] = imageNode.Width;
            dict["Height"] = imageNode.Height;
            dict["InputMode"] = imageNode.InputMode.ToString();
            dict["CropMode"] = imageNode.CropMode.ToString();

            if (!string.IsNullOrWhiteSpace(imageNode.ImageUrl))
                dict["ImageUrl"] = imageNode.ImageUrl;
            if (!string.IsNullOrWhiteSpace(imageNode.ImageUrlSourceNodeId))
                dict["ImageUrlSourceNodeId"] = imageNode.ImageUrlSourceNodeId;
            if (!string.IsNullOrWhiteSpace(imageNode.ImageUrlSourceOutputKey))
                dict["ImageUrlSourceOutputKey"] = imageNode.ImageUrlSourceOutputKey;

            if (!string.IsNullOrWhiteSpace(imageNode.ImageBase64))
                dict["ImageBase64"] = imageNode.ImageBase64;
            if (!string.IsNullOrWhiteSpace(imageNode.ImageBase64SourceNodeId))
                dict["ImageBase64SourceNodeId"] = imageNode.ImageBase64SourceNodeId;
            if (!string.IsNullOrWhiteSpace(imageNode.ImageBase64SourceOutputKey))
                dict["ImageBase64SourceOutputKey"] = imageNode.ImageBase64SourceOutputKey;

            dict["PreferGpu"] = imageNode.PreferGpu;
            if (!string.IsNullOrWhiteSpace(imageNode.FfmpegFilter))
                dict["FfmpegFilter"] = imageNode.FfmpegFilter;

            if (!string.IsNullOrWhiteSpace(imageNode.CroppedFolderPath))
                dict["CroppedFolderPath"] = imageNode.CroppedFolderPath;
            if (!string.IsNullOrWhiteSpace(imageNode.CroppedFolderSourceNodeId))
                dict["CroppedFolderSourceNodeId"] = imageNode.CroppedFolderSourceNodeId;
            if (!string.IsNullOrWhiteSpace(imageNode.CroppedFolderSourceOutputKey))
                dict["CroppedFolderSourceOutputKey"] = imageNode.CroppedFolderSourceOutputKey;

            // Serialize danh sách vùng crop (polygon points + state)
            if (imageNode.Crops != null && imageNode.Crops.Count > 0)
            {
                var cropsData = imageNode.Crops.Select(r => new
                {
                    Id = r.Id,
                    Order = r.Order,
                    ColorHex = r.ColorHex,
                    Points = r.Points.Select(p => new[] { p.X, p.Y }).ToList(),
                    IsVisible = r.IsVisible,
                    IsOutlineOnly = r.IsOutlineOnly,
                    SavedPath = r.SavedPath ?? string.Empty,
                    CropName = r.CropName ?? string.Empty
                }).ToList();
                dict["Crops"] = JsonSerializer.Serialize(cropsData);
            }

            // Image Processor settings
            dict["PromptSize"] = imageNode.PromptSize;
            if (!string.IsNullOrWhiteSpace(imageNode.ProcessorPrompt))
                dict["ProcessorPrompt"] = imageNode.ProcessorPrompt;
            dict["IsVerticalMode"] = imageNode.IsVerticalMode;

            // Render node config
            if (!string.IsNullOrWhiteSpace(imageNode.RenderNodeId))
                dict["RenderNodeId"] = imageNode.RenderNodeId;
            if (!string.IsNullOrWhiteSpace(imageNode.RenderNodeOutputKey))
                dict["RenderNodeOutputKey"] = imageNode.RenderNodeOutputKey;

            // SkipOutputs
            if (imageNode.SkipOutputs != null && imageNode.SkipOutputs.Count > 0)
                dict["SkipOutputs"] = JsonSerializer.Serialize(imageNode.SkipOutputs.ToList());

            dict["TitleDisplayMode"] = imageNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = imageNode.TitleColorMode.ToString();
            if (!string.IsNullOrWhiteSpace(imageNode.TitleColorKey))
                dict["TitleColorKey"] = imageNode.TitleColorKey;
        }
        else if (node is DataFetcherNode fetcherNodeSer)
        {
            if (!string.IsNullOrWhiteSpace(fetcherNodeSer.SourceNodeId))
                dict["SourceNodeId"] = fetcherNodeSer.SourceNodeId;
            if (!string.IsNullOrWhiteSpace(fetcherNodeSer.SourceOutputKey))
                dict["SourceOutputKey"] = fetcherNodeSer.SourceOutputKey;
            dict["WaitForWebNodeLoad"] = fetcherNodeSer.WaitForWebNodeLoad;
            dict["EnableTimer"] = fetcherNodeSer.EnableTimer;
            dict["TimerIntervalValue"] = fetcherNodeSer.TimerIntervalValue;
            dict["TimerUnit"] = fetcherNodeSer.TimerUnit;
            dict["EnableRealtime"] = fetcherNodeSer.EnableRealtime;
            dict["EnableDataReadyScan"] = fetcherNodeSer.EnableDataReadyScan;
            dict["DataReadyScanIntervalValue"] = fetcherNodeSer.DataReadyScanIntervalValue;
            dict["DataReadyScanUnit"] = fetcherNodeSer.DataReadyScanUnit;
            dict["DataReadyScanKeys"] = JsonSerializer.Serialize(
                (fetcherNodeSer.DataReadyScanKeys ?? new List<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
            dict["RunSourceNodeFirst"] = fetcherNodeSer.RunSourceNodeFirst;
            dict["TitleDisplayMode"] = fetcherNodeSer.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = fetcherNodeSer.TitleColorMode.ToString();
            if (!string.IsNullOrWhiteSpace(fetcherNodeSer.TitleColorKey))
                dict["TitleColorKey"] = fetcherNodeSer.TitleColorKey;
        }
        else if (node is FileDownloadNode fdSer)
        {
            dict["TitleDisplayMode"] = fdSer.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = fdSer.TitleColorMode.ToString();
            if (!string.IsNullOrWhiteSpace(fdSer.TitleColorKey))
                dict["TitleColorKey"] = fdSer.TitleColorKey;
            dict["FileNameTemplate"] = fdSer.FileNameTemplate ?? string.Empty;
            dict["MaxFileNameLength"] = fdSer.MaxFileNameLength;
            dict["AutoIncrementIfExists"] = fdSer.AutoIncrementIfExists;
            dict["RemoveDiacriticsFromFileName"] = fdSer.RemoveDiacriticsFromFileName;
            dict["DownloadUrl"] = fdSer.DownloadUrl ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fdSer.UrlSourceNodeId))
                dict["UrlSourceNodeId"] = fdSer.UrlSourceNodeId;
            if (!string.IsNullOrWhiteSpace(fdSer.UrlSourceOutputKey))
                dict["UrlSourceOutputKey"] = fdSer.UrlSourceOutputKey;
            dict["CurlCommand"] = fdSer.CurlCommand ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fdSer.CurlSourceNodeId))
                dict["CurlSourceNodeId"] = fdSer.CurlSourceNodeId;
            if (!string.IsNullOrWhiteSpace(fdSer.CurlSourceOutputKey))
                dict["CurlSourceOutputKey"] = fdSer.CurlSourceOutputKey;
            dict["DownloadFolderPath"] = fdSer.DownloadFolderPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fdSer.FolderSourceNodeId))
                dict["FolderSourceNodeId"] = fdSer.FolderSourceNodeId;
            if (!string.IsNullOrWhiteSpace(fdSer.FolderSourceOutputKey))
                dict["FolderSourceOutputKey"] = fdSer.FolderSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(fdSer.FileNameSourceNodeId))
                dict["FileNameSourceNodeId"] = fdSer.FileNameSourceNodeId;
            if (!string.IsNullOrWhiteSpace(fdSer.FileNameSourceOutputKey))
                dict["FileNameSourceOutputKey"] = fdSer.FileNameSourceOutputKey;
            dict["SaveAdditionalOutputFiles"] = fdSer.SaveAdditionalOutputFiles;
            if (!string.IsNullOrWhiteSpace(fdSer.AdditionalOutputDefaultNameTemplate))
                dict["AdditionalOutputDefaultNameTemplate"] = fdSer.AdditionalOutputDefaultNameTemplate;
            dict["AdditionalOutputSaves"] = JsonSerializer.Serialize(
                fdSer.AdditionalOutputSaves ?? new List<FileDownloadAdditionalOutputSaveEntry>());
        }
        else if (node is FolderFilePathsNode ffpSer)
        {
            dict["TitleDisplayMode"] = ffpSer.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = ffpSer.TitleColorMode.ToString();
            if (!string.IsNullOrWhiteSpace(ffpSer.TitleColorKey))
                dict["TitleColorKey"] = ffpSer.TitleColorKey;
            dict["FolderPath"] = ffpSer.FolderPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(ffpSer.FolderSourceNodeId))
                dict["FolderSourceNodeId"] = ffpSer.FolderSourceNodeId;
            if (!string.IsNullOrWhiteSpace(ffpSer.FolderSourceOutputKey))
                dict["FolderSourceOutputKey"] = ffpSer.FolderSourceOutputKey;
            dict["RefreshFolderSourceNodeBeforeUse"] = ffpSer.RefreshFolderSourceNodeBeforeUse;
            dict["IncludeSubfolders"] = ffpSer.IncludeSubfolders;
            dict["ExtensionFilterText"] = ffpSer.ExtensionFilterText ?? string.Empty;
            dict["ExtensionTags"] = JsonSerializer.Serialize(
                (ffpSer.ExtensionTags ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
            dict["ReadFileContents"] = ffpSer.ReadFileContents;
            dict["ReadContentExtensionsText"] = ffpSer.ReadContentExtensionsText ?? ".txt";
        }
        else if (node is KeyValueBridgeNode kvbSer)
        {
            dict["mode"] = kvbSer.IsPassKeyMode ? "pass" : "get";
            dict["IsPassKeyMode"] = kvbSer.IsPassKeyMode;
            dict["key"] = kvbSer.KvChannelKey ?? string.Empty;
            dict["KvChannelKey"] = kvbSer.KvChannelKey ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(kvbSer.SelectedSourceBridgeNodeId))
                dict["selectedSourceNodeId"] = kvbSer.SelectedSourceBridgeNodeId;
            dict["SelectedSourceBridgeNodeId"] = kvbSer.SelectedSourceBridgeNodeId ?? string.Empty;
            dict["interval"] = kvbSer.PollIntervalValue;
            dict["PollIntervalValue"] = kvbSer.PollIntervalValue;
            dict["intervalUnit"] = kvbSer.PollIntervalUnit.ToString();
            dict["PollIntervalUnit"] = kvbSer.PollIntervalUnit.ToString();
            dict["TitleDisplayMode"] = kvbSer.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = kvbSer.TitleColorMode.ToString();
            if (!string.IsNullOrWhiteSpace(kvbSer.TitleColorKey))
                dict["TitleColorKey"] = kvbSer.TitleColorKey;

            dict["EnableDataCleanup"] = kvbSer.EnableDataCleanup;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupTargetBridgeNodeId))
                dict["CleanupTargetBridgeNodeId"] = kvbSer.CleanupTargetBridgeNodeId;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupTargetKey))
                dict["CleanupTargetKey"] = kvbSer.CleanupTargetKey;
            dict["CleanupClearAllNodeData"] = kvbSer.CleanupClearAllNodeData;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupArrayFilterField))
                dict["CleanupArrayFilterField"] = kvbSer.CleanupArrayFilterField;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupArrayFilterValue))
                dict["CleanupArrayFilterValue"] = kvbSer.CleanupArrayFilterValue;
            dict["CleanupRemoveAllMatchedArrayItems"] = kvbSer.CleanupRemoveAllMatchedArrayItems;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupTriggerSourceNodeId))
                dict["CleanupTriggerSourceNodeId"] = kvbSer.CleanupTriggerSourceNodeId;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupTriggerSourceOutputKey))
                dict["CleanupTriggerSourceOutputKey"] = kvbSer.CleanupTriggerSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupTriggerExpectedValue))
                dict["CleanupTriggerExpectedValue"] = kvbSer.CleanupTriggerExpectedValue;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupKeySourceNodeId))
                dict["CleanupKeySourceNodeId"] = kvbSer.CleanupKeySourceNodeId;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupKeySourceOutputKey))
                dict["CleanupKeySourceOutputKey"] = kvbSer.CleanupKeySourceOutputKey;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupFilterFieldSourceNodeId))
                dict["CleanupFilterFieldSourceNodeId"] = kvbSer.CleanupFilterFieldSourceNodeId;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupFilterFieldSourceOutputKey))
                dict["CleanupFilterFieldSourceOutputKey"] = kvbSer.CleanupFilterFieldSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupFilterValueSourceNodeId))
                dict["CleanupFilterValueSourceNodeId"] = kvbSer.CleanupFilterValueSourceNodeId;
            if (!string.IsNullOrWhiteSpace(kvbSer.CleanupFilterValueSourceOutputKey))
                dict["CleanupFilterValueSourceOutputKey"] = kvbSer.CleanupFilterValueSourceOutputKey;
            if (kvbSer.AdditionalAppendSources != null && kvbSer.AdditionalAppendSources.Count > 0)
            {
                var appendSourcesJson = JsonSerializer.Serialize(kvbSer.AdditionalAppendSources
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                    .Select(x => new
                    {
                        SourceNodeId = x.SourceNodeId.Trim(),
                        SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                    })
                    .ToList());
                dict["AdditionalAppendSources"] = appendSourcesJson;
            }
        }
        else if (node is FlowOverwriteNode flowOverwriteSer)
        {
            dict["OutputKey"] = string.IsNullOrWhiteSpace(flowOverwriteSer.OutputKey) ? "outputKey" : flowOverwriteSer.OutputKey.Trim();
            dict["AppendMode"] = flowOverwriteSer.AppendMode;
            dict["IncludeIndirectSources"] = flowOverwriteSer.IncludeIndirectSources;
            dict["TitleDisplayMode"] = flowOverwriteSer.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = flowOverwriteSer.TitleColorMode.ToString();
            if (!string.IsNullOrWhiteSpace(flowOverwriteSer.TitleColorKey))
                dict["TitleColorKey"] = flowOverwriteSer.TitleColorKey;
            if (flowOverwriteSer.Mappings != null && flowOverwriteSer.Mappings.Count > 0)
            {
                dict["Mappings"] = JsonSerializer.Serialize(flowOverwriteSer.Mappings
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                    .Select(x => new
                    {
                        SourceNodeId = x.SourceNodeId.Trim(),
                        SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                    })
                    .ToList());
            }
        }
        else if (node is WebNode webNodeForSerialize)
        {
            // Update bindings trong dialog nếu đang mở để đảm bảo giá trị được cập nhật trước khi serialize
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Attempting to update bindings before serialize ===");
                
                // Tìm tất cả WorkflowEditorWindow đang mở (có thể có nhiều window)
                var allWindows = Application.Current?.Windows.OfType<Views.WorkflowEditorWindow>().ToList();
                if (allWindows != null && allWindows.Count > 0)
                {
                    foreach (var window in allWindows)
                    {
                        try
                        {
                            var field = typeof(Views.WorkflowEditorWindow).GetField("_nodeDialogManager",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (field?.GetValue(window) is Services.Interaction.NodeDialogManager dialogManager)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found NodeDialogManager, calling UpdateAllBindingsIfWebNodeDialog()");
                                dialogManager.UpdateAllBindingsIfWebNodeDialog();
                                System.Diagnostics.Debug.WriteLine($"✓ UpdateAllBindingsIfWebNodeDialog() called successfully");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error accessing NodeDialogManager from window: {ex.Message}");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No WorkflowEditorWindow found in Application.Current.Windows");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error updating bindings before serialize: {ex.Message}\n{ex.StackTrace}");
                // Continue - không block serialize
            }
            
            try
            {
                dict["Width"] = webNodeForSerialize.Width;
                dict["Height"] = webNodeForSerialize.Height;
                if (!string.IsNullOrEmpty(webNodeForSerialize.ExtractUrl))
                    dict["ExtractUrl"] = webNodeForSerialize.ExtractUrl;
                if (!string.IsNullOrEmpty(webNodeForSerialize.ExtractRequestMethod))
                    dict["ExtractRequestMethod"] = webNodeForSerialize.ExtractRequestMethod;
                if (!string.IsNullOrEmpty(webNodeForSerialize.ExtractStatusCode))
                    dict["ExtractStatusCode"] = webNodeForSerialize.ExtractStatusCode;
                if (webNodeForSerialize.BlockingRules != null && webNodeForSerialize.BlockingRules.Count > 0)
                {
                    var brList = webNodeForSerialize.BlockingRules.Select(r =>
                    {
                        var ruleDict = new Dictionary<string, object>
                        {
                            ["UrlPattern"] = r.UrlPattern,
                            ["Method"] = r.Method ?? "All"  // Method cho URL cha
                        };

                        // Serialize child rules nếu có
                        if (r.ChildRules != null && r.ChildRules.Count > 0)
                        {
                            var childList = r.ChildRules.Select(c => new Dictionary<string, object>
                            {
                                ["UrlPattern"] = c.UrlPattern,
                                ["Method"] = c.Method ?? "All"
                            }).ToList();

                            ruleDict["ChildRules"] = childList;
                        }

                        return ruleDict;
                    }).ToList();

                    dict["BlockingRules"] = JsonSerializer.Serialize(brList);
                }
                dict["SyncLiveOutputsToResults"] = webNodeForSerialize.SyncLiveOutputsToResults;
                dict["TitleDisplayMode"] = webNodeForSerialize.TitleDisplayMode.ToString();
                dict["TitleColorMode"] = webNodeForSerialize.TitleColorMode.ToString();
                if (!string.IsNullOrEmpty(webNodeForSerialize.TitleColorKey))
                    dict["TitleColorKey"] = webNodeForSerialize.TitleColorKey;

                // Output waiting behavior (timeout + mode)
                dict["ResponseOutputsWaitTimeoutMs"] = webNodeForSerialize.ResponseOutputsWaitTimeoutMs;
                dict["ResponseOutputsWaitMode"] = webNodeForSerialize.ResponseOutputsWaitMode.ToString();

                // JS injection (nhiều Node+Key -> WebView2)
                if (webNodeForSerialize.JsSources != null && webNodeForSerialize.JsSources.Count > 0)
                {
                    var arr = webNodeForSerialize.JsSources.Select(m => new Dictionary<string, object?>
                    {
                        ["SourceNodeId"] = m.SourceNodeId,
                        ["SourceOutputKey"] = m.SourceOutputKey,
                        ["AutoTimerEnabled"] = m.AutoTimerEnabled,
                        ["AutoTimerIntervalValue"] = m.AutoTimerIntervalValue,
                        ["AutoTimerIntervalUnit"] = m.AutoTimerIntervalUnit
                    }).ToList();
                    dict["JsSources"] = JsonSerializer.Serialize(arr);
                }

                // Serialize InputMappings (giống CodeNode nhưng dùng WebInputMapping)
                if (webNodeForSerialize.InputMappings != null && webNodeForSerialize.InputMappings.Count > 0)
                {
                    var arr = webNodeForSerialize.InputMappings.Select(m => new Dictionary<string, string?>
                    {
                        ["SourceNodeId"] = m.SourceNodeId,
                        ["SourceOutputKey"] = m.SourceOutputKey,
                        ["InputKeyOverride"] = m.InputKeyOverride
                    }).ToList();
                    dict["InputMappings"] = JsonSerializer.Serialize(arr);
                }

                // Auto-reload timer
                dict["AutoReloadEnabled"] = webNodeForSerialize.AutoReloadEnabled;
                dict["EnableSleepMode"] = webNodeForSerialize.EnableSleepMode;
                dict["SleepIdleTimeoutValue"] = webNodeForSerialize.SleepIdleTimeoutValue;
                dict["SleepIdleTimeoutUnit"] = webNodeForSerialize.SleepIdleTimeoutUnit;
                dict["AutoReloadIntervalValue"] = webNodeForSerialize.AutoReloadIntervalValue;
                dict["AutoReloadIntervalUnit"] = webNodeForSerialize.AutoReloadIntervalUnit;

                // Block all requests after first match
                dict["BlockAllRequestsAfterFirstMatch"] = webNodeForSerialize.BlockAllRequestsAfterFirstMatch;

                // Serialize per-domain CSS zoom for WebNode
                if (!string.IsNullOrWhiteSpace(webNodeForSerialize.LastHost))
                    dict["Web_LastHost"] = webNodeForSerialize.LastHost;
                if (webNodeForSerialize.CssZoom > 0)
                    dict["Web_CssZoom"] = webNodeForSerialize.CssZoom;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error serializing WebNode basic properties: {ex.Message}\n{ex.StackTrace}");
                // Continue - don't crash
            }
            
            // Serialize RequestInterceptRules - chỉ serialize khi có items và không null
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Serializing RequestInterceptRules for WebNode ===");
                System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is null: {webNodeForSerialize.RequestInterceptRules == null}");
                System.Diagnostics.Debug.WriteLine($"RequestInterceptRules Count: {webNodeForSerialize.RequestInterceptRules?.Count ?? 0}");
                
                if (webNodeForSerialize.RequestInterceptRules != null && webNodeForSerialize.RequestInterceptRules.Count > 0)
                {
                    var arr = new List<Dictionary<string, object>>();
                    int index = 0;
                    foreach (var r in webNodeForSerialize.RequestInterceptRules)
                    {
                        try
                        {
                            // Lấy giá trị trực tiếp từ object để đảm bảo không bị mất
                            var matchUrl = r?.MatchUrlPattern ?? "";
                            var replaceUrl = r?.ReplaceUrlValue ?? "";
                            var replaceUrlNodeId = r?.ReplaceUrlSourceNodeId ?? "";
                            var replaceUrlKey = r?.ReplaceUrlSourceOutputKey ?? "";
                            var replaceUrlWithNodeKey = r?.ReplaceUrlWithNodeKey ?? false;
                            var replaceParams = r?.ReplaceParamsValue ?? "";
                            var replaceParamsNodeId = r?.ReplaceParamsSourceNodeId ?? "";
                            var replaceParamsKey = r?.ReplaceParamsSourceOutputKey ?? "";
                            var replaceBody = r?.ReplaceBodyValue ?? "";
                            var replaceBodyNodeId = r?.ReplaceBodySourceNodeId ?? "";
                            var replaceBodyKey = r?.ReplaceBodySourceOutputKey ?? "";
                            
                            System.Diagnostics.Debug.WriteLine($"[{index}] RequestInterceptRule - MatchUrl='{matchUrl}', ReplaceUrl='{replaceUrl}', ReplaceUrlWithNodeKey={replaceUrlWithNodeKey}");
                            
                            var ruleDict = new Dictionary<string, object>();
                            ruleDict["MatchUrlPattern"] = matchUrl;
                            ruleDict["ReplaceUrlValue"] = replaceUrl;
                            ruleDict["ReplaceUrlSourceNodeId"] = replaceUrlNodeId;
                            ruleDict["ReplaceUrlSourceOutputKey"] = replaceUrlKey;
                            ruleDict["ReplaceUrlWithNodeKey"] = replaceUrlWithNodeKey;
                            ruleDict["ReplaceParamsValue"] = replaceParams;
                            ruleDict["ReplaceParamsSourceNodeId"] = replaceParamsNodeId;
                            ruleDict["ReplaceParamsSourceOutputKey"] = replaceParamsKey;
                            ruleDict["ReplaceBodyValue"] = replaceBody;
                            ruleDict["ReplaceBodySourceNodeId"] = replaceBodyNodeId;
                            ruleDict["ReplaceBodySourceOutputKey"] = replaceBodyKey;
                            arr.Add(ruleDict);
                            index++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error serializing RequestInterceptRule item at index {index}: {ex.Message}\n{ex.StackTrace}");
                            // Continue with next item
                            index++;
                        }
                    }
                    
                    if (arr.Count > 0)
                    {
                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = false,
                            MaxDepth = 32
                        };
                        var json = JsonSerializer.Serialize(arr, options);
                        dict["RequestInterceptRules"] = json;
                        System.Diagnostics.Debug.WriteLine($"✓ Successfully serialized {arr.Count} RequestInterceptRules to JSON: {json}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Warning: RequestInterceptRules collection has {webNodeForSerialize.RequestInterceptRules.Count} items but arr is empty!");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"RequestInterceptRules is null or empty (Count: {webNodeForSerialize.RequestInterceptRules?.Count ?? 0})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error serializing RequestInterceptRules: {ex.Message}\n{ex.StackTrace}");
                // Continue - don't crash the save operation
            }
            
            // Serialize ResponseOutputs - serialize tất cả items (kể cả rỗng) để đảm bảo cấu hình được lưu
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Serializing ResponseOutputs for WebNode ===");
                System.Diagnostics.Debug.WriteLine($"ResponseOutputs is null: {webNodeForSerialize.ResponseOutputs == null}");
                System.Diagnostics.Debug.WriteLine($"ResponseOutputs Count: {webNodeForSerialize.ResponseOutputs?.Count ?? 0}");
                
                if (webNodeForSerialize.ResponseOutputs != null && webNodeForSerialize.ResponseOutputs.Count > 0)
                {
                    var responseOutputsArr = new List<Dictionary<string, string>>();
                    int index = 0;
                    foreach (var ro in webNodeForSerialize.ResponseOutputs)
                    {
                        try
                        {
                            // Lấy giá trị trực tiếp từ object để đảm bảo không bị mất
                            var key = ro?.Key ?? "";
                            var url = ro?.Url ?? "";
                            var method = ro?.RequestMethod ?? "GET";
                            var extractType = ro?.ExtractType ?? "Response";
                            var waitForCompletion = ro?.WaitForCompletion ?? false;
                            
                            // Debug log để kiểm tra giá trị
                            System.Diagnostics.Debug.WriteLine($"[{index}] ResponseOutput - Key='{key}', Url='{url}', Method='{method}', ExtractType='{extractType}'");
                            
                            // Serialize tất cả items, kể cả khi Key hoặc Url rỗng (để user có thể chỉnh sửa sau)
                            var itemDict = new Dictionary<string, string>
                            {
                                ["Key"] = key,
                                ["Url"] = url,
                                ["RequestMethod"] = method,
                                ["ExtractType"] = extractType,
                                ["WaitForCompletion"] = waitForCompletion ? "true" : "false"
                            };
                            responseOutputsArr.Add(itemDict);
                            index++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error serializing ResponseOutput item at index {index}: {ex.Message}\n{ex.StackTrace}");
                            // Continue with next item
                            index++;
                        }
                    }
                    
                    // Luôn serialize nếu có items (kể cả rỗng) để đảm bảo cấu hình được lưu
                    if (responseOutputsArr.Count > 0)
                    {
                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = false,
                            MaxDepth = 32
                        };
                        var json = JsonSerializer.Serialize(responseOutputsArr, options);
                        dict["ResponseOutputs"] = json;
                        System.Diagnostics.Debug.WriteLine($"✓ Successfully serialized {responseOutputsArr.Count} ResponseOutputs to JSON: {json}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Warning: ResponseOutputs collection has {webNodeForSerialize.ResponseOutputs.Count} items but responseOutputsArr is empty!");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ResponseOutputs is null or empty (Count: {webNodeForSerialize.ResponseOutputs?.Count ?? 0})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error serializing ResponseOutputs: {ex.Message}\n{ex.StackTrace}");
                // Continue - don't crash the save operation
            }
        }
        else if (node is CodeNode codeNode)
        {
            dict["TitleDisplayMode"] = codeNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = codeNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(codeNode.TitleColorKey))
                dict["TitleColorKey"] = codeNode.TitleColorKey;
            if (codeNode.InputMappings != null && codeNode.InputMappings.Count > 0)
            {
                var arr = codeNode.InputMappings.Select(m => new Dictionary<string, object?>
                {
                    ["SourceNodeId"] = m.SourceNodeId,
                    ["SourceOutputKey"] = m.SourceOutputKey,
                    ["InputKeyOverride"] = m.InputKeyOverride,
                    ["ShouldReExecute"] = m.ShouldReExecute
                }).ToList();
                dict["InputMappings"] = JsonSerializer.Serialize(arr);
            }
            if (!string.IsNullOrEmpty(codeNode.ScriptCode))
                dict["ScriptCode"] = codeNode.ScriptCode;
            if (codeNode.OutputKeys != null)
                dict["OutputKeys"] = JsonSerializer.Serialize(codeNode.OutputKeys);
        }
        else if (node is FolderNode folderNode)
        {
            dict["TitleDisplayMode"] = folderNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = folderNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(folderNode.TitleColorKey))
                dict["TitleColorKey"] = folderNode.TitleColorKey;
            if (!string.IsNullOrEmpty(folderNode.RootFolderPath))
                dict["RootFolderPath"] = folderNode.RootFolderPath;
            if (!string.IsNullOrEmpty(folderNode.RootFolderPresetKey))
                dict["RootFolderPresetKey"] = folderNode.RootFolderPresetKey;
            if (!string.IsNullOrEmpty(folderNode.SubPathTemplate))
                dict["SubPathTemplate"] = folderNode.SubPathTemplate;
            if (folderNode.KeyValueInputs != null && folderNode.KeyValueInputs.Count > 0)
            {
                var arr = folderNode.KeyValueInputs.Select(kv => new Dictionary<string, string?>
                {
                    ["SourceNodeId"] = kv.SourceNodeId,
                    ["SourceOutputKey"] = kv.SourceOutputKey,
                    ["ValueConfirm"] = kv.ValueConfirm
                }).ToList();
                dict["KeyValueInputs"] = JsonSerializer.Serialize(arr);
            }
        }
        else if (node is HtmlUiNode htmlUiNode)
        {
            dict["TitleDisplayMode"] = htmlUiNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = htmlUiNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(htmlUiNode.TitleColorKey))
                dict["TitleColorKey"] = htmlUiNode.TitleColorKey;
            if (htmlUiNode.InputMappings != null && htmlUiNode.InputMappings.Count > 0)
            {
                var arr = htmlUiNode.InputMappings.Select(m => new Dictionary<string, object?>
                {
                    ["SourceNodeId"] = m.SourceNodeId,
                    ["SourceOutputKey"] = m.SourceOutputKey,
                    ["InputKeyOverride"] = m.InputKeyOverride,
                    ["ShouldReExecute"] = m.ShouldReExecute,
                    ["AutoRefreshEnabled"] = m.AutoRefreshEnabled,
                    ["AutoRefreshInterval"] = m.AutoRefreshInterval,
                    ["AutoRefreshUnit"] = m.AutoRefreshUnit
                }).ToList();
                dict["InputMappings"] = JsonSerializer.Serialize(arr);
            }
            if (!string.IsNullOrEmpty(htmlUiNode.HtmlCode))
                dict["HtmlCode"] = htmlUiNode.HtmlCode;
            if (!string.IsNullOrEmpty(htmlUiNode.JsCode))
                dict["JsCode"] = htmlUiNode.JsCode;
            if (!string.IsNullOrEmpty(htmlUiNode.CssCode))
                dict["CssCode"] = htmlUiNode.CssCode;
            if (!string.IsNullOrEmpty(htmlUiNode.ParamsCode))
                dict["ParamsCode"] = htmlUiNode.ParamsCode;
            if (htmlUiNode.OutputKeys != null && htmlUiNode.OutputKeys.Count > 0)
                dict["OutputKeys"] = JsonSerializer.Serialize(htmlUiNode.OutputKeys);
            dict["Width"] = htmlUiNode.Width;
            dict["Height"] = htmlUiNode.Height;
            if (htmlUiNode.CssZoom > 0)
                dict["HtmlUi_CssZoom"] = htmlUiNode.CssZoom;
            dict["EnableSleepMode"] = htmlUiNode.EnableSleepMode;
            dict["SleepIdleTimeoutValue"] = htmlUiNode.SleepIdleTimeoutValue;
            dict["SleepIdleTimeoutUnit"] = htmlUiNode.SleepIdleTimeoutUnit;
            // ── WebTab properties ──
            dict["UseWebTab"] = htmlUiNode.UseWebTab;
            if (!string.IsNullOrEmpty(htmlUiNode.WebTabUrl))
                dict["WebTabUrl"] = htmlUiNode.WebTabUrl;
            if (!string.IsNullOrEmpty(htmlUiNode.WebTabCookieSourceNodeId))
                dict["WebTabCookieSourceNodeId"] = htmlUiNode.WebTabCookieSourceNodeId;
            if (!string.IsNullOrEmpty(htmlUiNode.WebTabCookieSourceOutputKey))
                dict["WebTabCookieSourceOutputKey"] = htmlUiNode.WebTabCookieSourceOutputKey;
            dict["WebTabAutoRefreshEnabled"] = htmlUiNode.WebTabAutoRefreshEnabled;
            dict["WebTabAutoRefreshInterval"] = htmlUiNode.WebTabAutoRefreshInterval;
            dict["WebTabAutoRefreshUnit"] = htmlUiNode.WebTabAutoRefreshUnit;
            // ── Offline Assets (JS/CSS libraries) ──
            if (htmlUiNode.OfflineAssets != null && htmlUiNode.OfflineAssets.Count > 0)
            {
                var assetsJson = JsonSerializer.Serialize(htmlUiNode.OfflineAssets.Select(a => new
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    SourceUrl = a.SourceUrl,
                    LocalFileName = a.LocalFileName,
                    AssetType = a.AssetType,
                    IsEnabled = a.IsEnabled
                }).ToList());
                dict["OfflineAssets"] = assetsJson;
            }
            // ── AsyncDataSources (Async Data Receiver) ──
            if (htmlUiNode.AsyncDataSources != null && htmlUiNode.AsyncDataSources.Count > 0)
            {
                var adsJson = JsonSerializer.Serialize(htmlUiNode.AsyncDataSources.Select(a => new
                {
                    SourceNodeId = a.SourceNodeId,
                    SourceOutputKey = a.SourceOutputKey,
                    ReceiverKey = a.ReceiverKey
                }).ToList());
                dict["AsyncDataSources"] = adsJson;
            }
        }
        else if (node is HttpRequestNode httpRequestNode)
        {
            // Serialize basic properties
            dict["TitleDisplayMode"] = httpRequestNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = httpRequestNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(httpRequestNode.TitleColorKey))
                dict["TitleColorKey"] = httpRequestNode.TitleColorKey;
            dict["Url"] = httpRequestNode.Url ?? string.Empty;
            dict["HttpMethod"] = httpRequestNode.HttpMethod.ToString();
            dict["AuthType"] = httpRequestNode.AuthType.ToString();
            dict["BodyType"] = httpRequestNode.BodyType.ToString();
            dict["TimeoutSeconds"] = httpRequestNode.TimeoutSeconds;
            
            // Serialize URL dynamic binding
            if (!string.IsNullOrWhiteSpace(httpRequestNode.UrlSourceNodeId))
                dict["UrlSourceNodeId"] = httpRequestNode.UrlSourceNodeId;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.UrlSourceOutputKey))
                dict["UrlSourceOutputKey"] = httpRequestNode.UrlSourceOutputKey;
            
            // Serialize Body
            if (!string.IsNullOrWhiteSpace(httpRequestNode.RawBody))
                dict["RawBody"] = httpRequestNode.RawBody;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.BodySourceNodeId))
                dict["BodySourceNodeId"] = httpRequestNode.BodySourceNodeId;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.BodySourceOutputKey))
                dict["BodySourceOutputKey"] = httpRequestNode.BodySourceOutputKey;
            
            // Serialize Auth
            if (!string.IsNullOrWhiteSpace(httpRequestNode.AuthUsername))
                dict["AuthUsername"] = httpRequestNode.AuthUsername;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.AuthPassword))
                dict["AuthPassword"] = httpRequestNode.AuthPassword;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.AuthToken))
                dict["AuthToken"] = httpRequestNode.AuthToken;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.TokenSourceNodeId))
                dict["TokenSourceNodeId"] = httpRequestNode.TokenSourceNodeId;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.TokenSourceOutputKey))
                dict["TokenSourceOutputKey"] = httpRequestNode.TokenSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ApiKeyName))
                dict["ApiKeyName"] = httpRequestNode.ApiKeyName;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ApiKeyValue))
                dict["ApiKeyValue"] = httpRequestNode.ApiKeyValue;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ApiKeyValueSourceNodeId))
                dict["ApiKeyValueSourceNodeId"] = httpRequestNode.ApiKeyValueSourceNodeId;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ApiKeyValueSourceOutputKey))
                dict["ApiKeyValueSourceOutputKey"] = httpRequestNode.ApiKeyValueSourceOutputKey;
            dict["ApiKeyInHeader"] = httpRequestNode.ApiKeyInHeader;
            
            // Serialize Headers
            if (httpRequestNode.Headers != null && httpRequestNode.Headers.Count > 0)
            {
                var headersJson = JsonSerializer.Serialize(httpRequestNode.Headers.Select(h => new
                {
                    Key = h.Key,
                    Value = h.Value,
                    IsEnabled = h.IsEnabled,
                    SourceNodeId = h.SourceNodeId,
                    SourceOutputKey = h.SourceOutputKey
                }).ToList());
                dict["Headers"] = headersJson;
            }
            
            // Serialize QueryParams
            if (httpRequestNode.QueryParams != null && httpRequestNode.QueryParams.Count > 0)
            {
                var paramsJson = JsonSerializer.Serialize(httpRequestNode.QueryParams.Select(p => new
                {
                    Key = p.Key,
                    Value = p.Value,
                    IsEnabled = p.IsEnabled,
                    SourceNodeId = p.SourceNodeId,
                    SourceOutputKey = p.SourceOutputKey
                }).ToList());
                dict["QueryParams"] = paramsJson;
            }
            
            // Serialize FormData
            if (httpRequestNode.FormData != null && httpRequestNode.FormData.Count > 0)
            {
                var formDataJson = JsonSerializer.Serialize(httpRequestNode.FormData.Select(f => new
                {
                    Key = f.Key,
                    Value = f.Value,
                    IsEnabled = f.IsEnabled,
                    SourceNodeId = f.SourceNodeId,
                    SourceOutputKey = f.SourceOutputKey
                }).ToList());
                dict["FormData"] = formDataJson;
            }

            // cURL binding (NEW)
            if (!string.IsNullOrEmpty(httpRequestNode.CurlSourceNodeId))
                dict["CurlSourceNodeId"] = httpRequestNode.CurlSourceNodeId;
            if (!string.IsNullOrEmpty(httpRequestNode.CurlSourceOutputKey))
                dict["CurlSourceOutputKey"] = httpRequestNode.CurlSourceOutputKey;

            // Serialize Anti-bot / bypass (libcurl)
            dict["UseCurl"] = httpRequestNode.UseCurl;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.CurlPath))
                dict["CurlPath"] = httpRequestNode.CurlPath;
            if (!string.IsNullOrWhiteSpace(httpRequestNode.ImpersonateBrowser))
                dict["ImpersonateBrowser"] = httpRequestNode.ImpersonateBrowser;
            dict["AutoAppendCurlWriteOut"] = httpRequestNode.AutoAppendCurlWriteOut;
        }
        else if (node is OutputNode outputNode)
        {
            // Serialize OutputKey
            if (!string.IsNullOrWhiteSpace(outputNode.OutputKey))
                dict["OutputKey"] = outputNode.OutputKey;
            
            // Serialize FormatString
            if (!string.IsNullOrWhiteSpace(outputNode.FormatString))
                dict["FormatString"] = outputNode.FormatString;
            
            // Serialize InputVariables
            if (outputNode.InputVariables != null && outputNode.InputVariables.Count > 0)
            {
                var variablesJson = JsonSerializer.Serialize(outputNode.InputVariables.Select(v => new
                {
                    VariableKey = v.VariableKey,
                    SourceNodeId = v.SourceNodeId,
                    SourceOutputKey = v.SourceOutputKey
                }).ToList());
                dict["InputVariables"] = variablesJson;
            }
            
            // Serialize TitleDisplayMode
            dict["TitleDisplayMode"] = outputNode.TitleDisplayMode.ToString();

            // Serialize TitleColorMode and TitleColorKey
            dict["TitleColorMode"] = outputNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(outputNode.TitleColorKey))
                dict["TitleColorKey"] = outputNode.TitleColorKey;
        }
        else if (node is NotificationNode notificationNode)
        {
            dict["TitleDisplayMode"] = notificationNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = notificationNode.TitleColorMode.ToString();
            if (!string.IsNullOrEmpty(notificationNode.TitleColorKey))
                dict["TitleColorKey"] = notificationNode.TitleColorKey;

            dict["DefaultDurationSeconds"] = notificationNode.DefaultDurationSeconds;

            if (notificationNode.TitleInput != null)
            {
                var json = JsonSerializer.Serialize(new
                {
                    VariableKey = notificationNode.TitleInput.VariableKey,
                    SourceNodeId = notificationNode.TitleInput.SourceNodeId,
                    SourceOutputKey = notificationNode.TitleInput.SourceOutputKey
                });
                dict["TitleInput"] = json;
            }

            if (notificationNode.ContentInput != null)
            {
                var json = JsonSerializer.Serialize(new
                {
                    VariableKey = notificationNode.ContentInput.VariableKey,
                    SourceNodeId = notificationNode.ContentInput.SourceNodeId,
                    SourceOutputKey = notificationNode.ContentInput.SourceOutputKey
                });
                dict["ContentInput"] = json;
            }

            if (notificationNode.DurationInput != null)
            {
                var json = JsonSerializer.Serialize(new
                {
                    VariableKey = notificationNode.DurationInput.VariableKey,
                    SourceNodeId = notificationNode.DurationInput.SourceNodeId,
                    SourceOutputKey = notificationNode.DurationInput.SourceOutputKey
                });
                dict["DurationInput"] = json;
            }

            if (!string.IsNullOrWhiteSpace(notificationNode.StaticTitle))
                dict["StaticTitle"] = notificationNode.StaticTitle;

            if (!string.IsNullOrWhiteSpace(notificationNode.StaticContent))
                dict["StaticContent"] = notificationNode.StaticContent;

            if (!string.IsNullOrWhiteSpace(notificationNode.ToastTitleColorKey))
                dict["ToastTitleColorKey"] = notificationNode.ToastTitleColorKey;

            if (!string.IsNullOrWhiteSpace(notificationNode.ToastContentColorKey))
                dict["ToastContentColorKey"] = notificationNode.ToastContentColorKey;

            if (!string.IsNullOrWhiteSpace(notificationNode.ToastBackgroundColorKey))
                dict["ToastBackgroundColorKey"] = notificationNode.ToastBackgroundColorKey;

            dict["ToastBackgroundOpacity"] = notificationNode.ToastBackgroundOpacity;
        }


        // Serialize cấu hình ReuseRoutes (tái sử dụng flow + line style) cho mọi loại node nếu có
        if (node.ReuseRoutes != null && node.ReuseRoutes.Count > 0)
        {
            try
            {
                var routesJson = JsonSerializer.Serialize(node.ReuseRoutes);
                dict["ReuseRoutes"] = routesJson;
            }
            catch
            {
                // best-effort, không chặn save nếu serialize lỗi
            }
        }

        if (!string.IsNullOrWhiteSpace(node.FlowScopeKey))
            dict["FlowScopeKey"] = node.FlowScopeKey;
        dict["RunMode"] = node.RunMode.ToString();
        dict["AutoRunIntervalValue"] = node.AutoRunIntervalValue;
        dict["AutoRunIntervalUnit"] = node.AutoRunIntervalUnit.ToString();
        dict["AutoScopeVisualPadding"] = node.AutoScopeVisualPadding.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["AutoScopeFrameX"] = node.AutoScopeFrameX.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["AutoScopeFrameY"] = node.AutoScopeFrameY.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["AutoScopeFrameWidth"] = node.AutoScopeFrameWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["AutoScopeFrameHeight"] = node.AutoScopeFrameHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict["EndBehavior"] = node.EndBehavior.ToString();
        dict["DiamondSharpness"] = node.DiamondSharpness.ToString();
        if (node.IsConditionalNode)
            dict["ConditionalVisualMode"] = node.ConditionalVisualMode.ToString();

        if (node.DynamicInputs != null && node.DynamicInputs.Count > 0)
        {
            foreach (var inp in node.DynamicInputs)
            {
                var key = inp.Key ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (!string.IsNullOrWhiteSpace(inp.SelectedSourceNodeId))
                    dict[$"DynIn_{key}_SrcNode"] = inp.SelectedSourceNodeId!;

                if (!string.IsNullOrWhiteSpace(inp.SelectedSourceOutputKey))
                    dict[$"DynIn_{key}_SrcKey"] = inp.SelectedSourceOutputKey!;

                if (!string.IsNullOrWhiteSpace(inp.UserKeyOverride))
                    dict[$"DynIn_{key}_UserKey"] = inp.UserKeyOverride!;

                if (!string.IsNullOrWhiteSpace(inp.UserValueOverride))
                    dict[$"DynIn_{key}_UserValue"] = inp.UserValueOverride!;

                dict[$"DynIn_{key}_ConvType"] = inp.ConvertType.ToString();
            }
        }

        else if (node is StorageNode storageNode)
        {
            // TitleDisplayMode / TitleColorMode / TitleColorKey
            dict["TitleDisplayMode"] = storageNode.TitleDisplayMode.ToString();
            dict["TitleColorMode"] = storageNode.TitleColorMode.ToString();
            if (!string.IsNullOrWhiteSpace(storageNode.TitleColorKey))
                dict["TitleColorKey"] = storageNode.TitleColorKey!;

            if (!string.IsNullOrWhiteSpace(storageNode.SourceNodeId))
                dict["SourceNodeId"] = storageNode.SourceNodeId!;
            if (!string.IsNullOrWhiteSpace(storageNode.SourceOutputKey))
                dict["SourceOutputKey"] = storageNode.SourceOutputKey!;

            // IsInputMode
            dict["IsInputMode"] = storageNode.IsInputMode;

            // Lưu StoredOutputs – các giá trị đã được gán vào node
            if (storageNode.StoredOutputs.Count > 0)
            {
                dict["StoredOutputs"] = storageNode.StoredOutputs.ToDictionary(
                    kv => kv.Key,
                    kv => (object?)kv.Value ?? string.Empty);
            }

            // Lưu danh sách OutputKeys hiện tại để khôi phục cấu trúc outputs
            if (storageNode.DynamicOutputs != null && storageNode.DynamicOutputs.Count > 0)
            {
                var keys = storageNode.DynamicOutputs
                    .Where(o => !string.IsNullOrWhiteSpace(o.Key))
                    .Select(o => o.Key!)
                    .ToList();
                dict["OutputKeys"] = keys;
            }
        }

        return dict;
    }

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


