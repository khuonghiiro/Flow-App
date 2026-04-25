using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private const string WorkflowClipboardMarker = "FLOWMY_SUBGRAPH_V1";
        private const string WorkflowClipboardCompression = "brotli-base64";
        private const string LegacyWorkflowClipboardCompression = "gzip-base64";
        private readonly HashSet<WorkflowNode> _boxSelectedNodes = new();
        private Border? _selectionDragBorder;
        private Border? _selectionResultBorder;
        private Point _boxSelectionStart;
        private bool _isBoxSelecting;

        private bool TryHandleWorkflowClipboardShortcuts(KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control || IsEditingTextInput())
                return false;

            if (e.Key == Key.C)
            {
                if (TryCopyCurrentSelectionToClipboard())
                {
                    e.Handled = true;
                    return true;
                }
            }
            else if (e.Key == Key.V)
            {
                if (TryPasteWorkflowSelectionFromClipboard())
                {
                    e.Handled = true;
                    return true;
                }
            }

            return false;
        }

        private bool TryHandleBoxSelectionDeleteShortcut(KeyEventArgs e)
        {
            if (e.Key != Key.Delete) return false;
            if (IsEditingTextInput()) return false;
            if (_boxSelectedNodes.Count == 0) return false;

            var vm = ViewModel;
            if (vm == null) return false;
            if (vm.IsDebugReadOnlyMode) return false;

            var selectedNodes = _boxSelectedNodes
                .Where(vm.Nodes.Contains)
                .ToList();
            if (selectedNodes.Count == 0) return false;

            // Đồng bộ behavior với copy/paste: nếu chọn Loop/AsyncTask parent thì lấy thêm body companion.
            selectedNodes = ExpandSelectionWithCompanionBodyNodes(selectedNodes);

            var nodesToDelete = selectedNodes
                .Where(n => n.Type != NodeType.Start && n.Type != NodeType.End)
                .Where(n => !IsNodeLockedByBodyContainer(vm, n))
                .Distinct()
                .ToList();
            if (nodesToDelete.Count == 0) return false;

            var nodeIds = new HashSet<string>(nodesToDelete.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
            var connectionsToDelete = vm.Connections
                .Where(c => nodeIds.Contains(c.FromNode.Id) || nodeIds.Contains(c.ToNode.Id))
                .ToList();

            foreach (var connection in connectionsToDelete)
                vm.Connections.Remove(connection);

            foreach (var node in nodesToDelete)
                vm.Nodes.Remove(node);

            _boxSelectedNodes.Clear();
            if (_selectionResultBorder != null && WorkflowCanvas.Children.Contains(_selectionResultBorder))
                WorkflowCanvas.Children.Remove(_selectionResultBorder);
            _selectionResultBorder = null;
            vm.SelectedNode = null;
            return true;
        }

        private bool IsEditingTextInput()
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            while (focused != null)
            {
                if (focused is System.Windows.Controls.Primitives.TextBoxBase ||
                    focused is System.Windows.Controls.PasswordBox ||
                    focused is System.Windows.Controls.ComboBox)
                {
                    return true;
                }

                focused = VisualTreeHelper.GetParent(focused);
            }

            return false;
        }

        private bool TryCopyCurrentSelectionToClipboard()
        {
            var vm = ViewModel;
            if (vm == null) return false;

            var selectedNodes = _boxSelectedNodes.Count > 0
                ? _boxSelectedNodes.Where(vm.Nodes.Contains).ToList()
                : (vm.SelectedNode != null ? new List<WorkflowNode> { vm.SelectedNode } : new List<WorkflowNode>());

            if (selectedNodes.Count == 0) return false;

            selectedNodes = ExpandSelectionWithCompanionBodyNodes(selectedNodes);
            var selectedNodeIds = new HashSet<string>(selectedNodes.Select(n => n.Id));
            var selectedConnections = vm.Connections
                .Where(c => selectedNodeIds.Contains(c.FromNode.Id) && selectedNodeIds.Contains(c.ToNode.Id))
                .ToList();

            var persistence = new FileWorkflowPersistenceService(_templateFactory);
            var workflowJson = persistence.ExportToJson(
                "ClipboardSelection",
                selectedNodes,
                selectedConnections,
                zoomLevel: 1,
                panX: 0,
                panY: 0);

            var envelope = JsonSerializer.Serialize(new WorkflowClipboardEnvelope
            {
                Marker = WorkflowClipboardMarker,
                CreatedAtUtc = DateTime.UtcNow,
                Compression = WorkflowClipboardCompression,
                WorkflowPayloadBase64 = CompressToBase64(workflowJson)
            });

            Clipboard.SetText(envelope);
            return true;
        }

        private bool TryPasteWorkflowSelectionFromClipboard()
        {
            var vm = ViewModel;
            if (vm == null || !Clipboard.ContainsText()) return false;

            WorkflowClipboardEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<WorkflowClipboardEnvelope>(Clipboard.GetText());
            }
            catch
            {
                return false;
            }

            if (envelope == null ||
                !string.Equals(envelope.Marker, WorkflowClipboardMarker, StringComparison.Ordinal))
            {
                return false;
            }

            var workflowJson = TryExtractWorkflowJsonFromEnvelope(envelope);
            if (string.IsNullOrWhiteSpace(workflowJson)) return false;

            var persistence = new FileWorkflowPersistenceService(_templateFactory);
            var load = persistence.ImportFromJson(workflowJson);
            if (load == null || load.Nodes.Count == 0) return false;
            ClipboardWorkflowDto? rawWorkflowDto = null;
            try
            {
                rawWorkflowDto = JsonSerializer.Deserialize<ClipboardWorkflowDto>(workflowJson);
            }
            catch
            {
                // Fallback: vẫn dùng load.Connections nếu parse DTO fail
            }

            var importableNodes = load.Nodes
                .Where(n => n is not LoopBodyNode && n is not AsyncTaskBodyNode)
                .ToList();
            if (importableNodes.Count == 0) return false;

            var minX = importableNodes.Min(n => n.X);
            var minY = importableNodes.Min(n => n.Y);
            var pasteAnchor = Mouse.GetPosition(WorkflowCanvas);
            var dx = pasteAnchor.X - minX;
            var dy = pasteAnchor.Y - minY;

            var nodeMap = new Dictionary<string, WorkflowNode>(StringComparer.OrdinalIgnoreCase);
            var sourceNodeById = load.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var sourceNode in importableNodes)
            {
                var clone = CreateDuplicateNodeInstance(sourceNode, dx, dy);
                if (clone == null) continue;

                vm.Nodes.Add(clone);
                nodeMap[sourceNode.Id] = clone;
            }

            foreach (var sourceNode in importableNodes)
            {
                if (!nodeMap.TryGetValue(sourceNode.Id, out var mappedNode)) continue;

                if (sourceNode is AsyncTaskNode srcAsync &&
                    mappedNode is AsyncTaskNode dstAsync &&
                    srcAsync.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
                {
                    dstAsync.UiPresentationMode = AsyncTaskUiPresentationMode.LoopLikeDispatch;
                    dstAsync.RunInParallel = srcAsync.RunInParallel;
                    dstAsync.DispatchLoopType = srcAsync.DispatchLoopType;
                    dstAsync.RepeatCount = srcAsync.RepeatCount;
                    dstAsync.StartIndex = srcAsync.StartIndex;
                    dstAsync.EndIndex = srcAsync.EndIndex;
                    dstAsync.ReadResultsInBody = srcAsync.ReadResultsInBody;
                }

                if (sourceNode.IsConditionalNode &&
                    sourceNode.ConditionalVisualMode == ConditionalVisualMode.Diamond &&
                    mappedNode.IsConditionalNode)
                {
                    mappedNode.ConditionalVisualMode = ConditionalVisualMode.Diamond;
                }
            }

            foreach (var sourceLoop in load.Nodes.OfType<LoopNode>())
            {
                if (sourceLoop.LoopBodyNode == null) continue;
                if (!nodeMap.TryGetValue(sourceLoop.Id, out var mappedLoopNode)) continue;
                if (mappedLoopNode is not LoopNode mappedLoop || mappedLoop.LoopBodyNode == null) continue;
                nodeMap[sourceLoop.LoopBodyNode.Id] = mappedLoop.LoopBodyNode;
            }

            foreach (var sourceAsync in load.Nodes.OfType<AsyncTaskNode>())
            {
                if (sourceAsync.AsyncTaskBodyNode == null) continue;
                if (!nodeMap.TryGetValue(sourceAsync.Id, out var mappedAsyncNode)) continue;
                if (mappedAsyncNode is not AsyncTaskNode mappedAsync || mappedAsync.AsyncTaskBodyNode == null) continue;
                EnsureAsyncTaskBodyPortsExistForPaste(mappedAsync.AsyncTaskBodyNode);
                nodeMap[sourceAsync.AsyncTaskBodyNode.Id] = mappedAsync.AsyncTaskBodyNode;
            }

            RemapPastedNodeReferences(nodeMap);

            if (nodeMap.Count == 0) return false;

            var rawConnections = rawWorkflowDto?.Connections
                .Where(c => !string.IsNullOrWhiteSpace(c.FromNodeId) && !string.IsNullOrWhiteSpace(c.ToNodeId))
                .ToList();
            var rawNodeById = (rawWorkflowDto?.Nodes ?? new List<ClipboardNodeDto>())
                .Where(n => !string.IsNullOrWhiteSpace(n.Id))
                .ToDictionary(n => n.Id!, StringComparer.OrdinalIgnoreCase);

            if (rawConnections != null && rawConnections.Count > 0)
            {
                foreach (var rawConn in rawConnections)
                {
                    if (!nodeMap.TryGetValue(rawConn.FromNodeId!, out var newFromNode) ||
                        !nodeMap.TryGetValue(rawConn.ToNodeId!, out var newToNode))
                    {
                        continue;
                    }

                    rawNodeById.TryGetValue(rawConn.FromNodeId!, out var rawFromNode);
                    rawNodeById.TryGetValue(rawConn.ToNodeId!, out var rawToNode);

                    var sourceFromPort = !string.IsNullOrWhiteSpace(rawConn.FromPortId) && rawFromNode != null
                        ? rawFromNode.Ports.FirstOrDefault(p => string.Equals(p.Id, rawConn.FromPortId, StringComparison.Ordinal))
                        : null;
                    var sourceToPort = !string.IsNullOrWhiteSpace(rawConn.ToPortId) && rawToNode != null
                        ? rawToNode.Ports.FirstOrDefault(p => string.Equals(p.Id, rawConn.ToPortId, StringComparison.Ordinal))
                        : null;

                    var newFromPort = MapPortForPastedConnection(newFromNode, rawConn.FromPortId, sourceFromPort);
                    var newToPort = MapPortForPastedConnection(newToNode, rawConn.ToPortId, sourceToPort);

                    var newConnection = new WorkflowConnection
                    {
                        FromNode = newFromNode,
                        ToNode = newToNode,
                        FromPort = newFromPort,
                        ToPort = newToPort,
                        IsFromInput = false,
                        IsDeleteVisible = true
                    };

                    if (!vm.Connections.Any(c =>
                        c.FromNode == newConnection.FromNode &&
                        c.ToNode == newConnection.ToNode &&
                        c.FromPort == newConnection.FromPort &&
                        c.ToPort == newConnection.ToPort))
                    {
                        vm.Connections.Add(newConnection);
                    }
                }
            }
            else
            {
                foreach (var conn in load.Connections)
                {
                    if (!nodeMap.TryGetValue(conn.FromNode.Id, out var newFromNode) ||
                        !nodeMap.TryGetValue(conn.ToNode.Id, out var newToNode))
                    {
                        continue;
                    }

                    if (!sourceNodeById.TryGetValue(conn.FromNode.Id, out var srcFromNode) ||
                        !sourceNodeById.TryGetValue(conn.ToNode.Id, out var srcToNode))
                    {
                        continue;
                    }

                    var newFromPort = MapPortForPastedConnection(newFromNode, conn.FromPort?.Id, conn.FromPort == null ? null : new ClipboardPortDto
                    {
                        Id = conn.FromPort.Id,
                        IsInput = conn.FromPort.IsInput,
                        Position = conn.FromPort.Position.ToString()
                    });
                    var newToPort = MapPortForPastedConnection(newToNode, conn.ToPort?.Id, conn.ToPort == null ? null : new ClipboardPortDto
                    {
                        Id = conn.ToPort.Id,
                        IsInput = conn.ToPort.IsInput,
                        Position = conn.ToPort.Position.ToString()
                    });

                    var newConnection = new WorkflowConnection
                    {
                        FromNode = newFromNode,
                        ToNode = newToNode,
                        FromPort = newFromPort,
                        ToPort = newToPort,
                        IsFromInput = false,
                        IsDeleteVisible = true
                    };

                    if (!vm.Connections.Any(c =>
                        c.FromNode == newConnection.FromNode &&
                        c.ToNode == newConnection.ToNode &&
                        c.FromPort == newConnection.FromPort &&
                        c.ToPort == newConnection.ToPort))
                    {
                        vm.Connections.Add(newConnection);
                    }
                }
            }

            _boxSelectedNodes.Clear();
            foreach (var node in nodeMap.Values)
                _boxSelectedNodes.Add(node);

            vm.SelectedNode = nodeMap.Values.LastOrDefault();
            _eventService.RefreshDynamicDataSourceSelectors();
            RefreshConditionalDiamondGeometryAfterPaste(nodeMap.Values.ToList());
            return true;
        }

        private void RefreshConditionalDiamondGeometryAfterPaste(List<WorkflowNode> pastedNodes)
        {
            if (pastedNodes == null || pastedNodes.Count == 0) return;

            var diamondNodes = pastedNodes
                .Where(n => n.IsConditionalNode && n.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                .ToList();
            if (diamondNodes.Count == 0) return;

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                var vm = ViewModel;
                if (vm == null) return;

                foreach (var node in diamondNodes)
                {
                    ReRenderConditionalNode(node);
                    RenderConditionalNodePorts(node);
                    var relatedConnections = vm.Connections.Where(c => c.FromNode == node || c.ToNode == node).ToList();
                    foreach (var conn in relatedConnections)
                        UpdateConnectionPath(conn);
                }

                RefreshConditionalDiamondLineStyles();

                // Pass 2 at Render priority to settle any late measure/arrange updates
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
                {
                    var vm2 = ViewModel;
                    if (vm2 == null) return;
                    RefreshConditionalDiamondLineStyles();
                    foreach (var node in diamondNodes)
                    {
                        RenderConditionalNodePorts(node);
                        var relatedConnections = vm2.Connections.Where(c => c.FromNode == node || c.ToNode == node).ToList();
                        foreach (var conn in relatedConnections)
                            UpdateConnectionPath(conn);
                    }
                }));
            }));
        }

        private static NodePort? MapPortForPastedConnection(WorkflowNode targetNode, string? sourcePortId, ClipboardPortDto? sourcePort)
        {
            if (sourcePort == null && string.IsNullOrWhiteSpace(sourcePortId))
                return null;

            if (targetNode is AsyncTaskBodyNode targetAsyncBody)
                EnsureAsyncTaskBodyPortsExistForPaste(targetAsyncBody);

            if (targetNode is AsyncTaskBodyNode)
            {
                var semanticBodyPort = TryMapAsyncTaskBodyPort(targetNode, sourcePortId, sourcePort);
                if (semanticBodyPort != null) return semanticBodyPort;
            }

            // 1) Preferred: direct ID match
            var byId = !string.IsNullOrWhiteSpace(sourcePortId)
                ? targetNode.Ports.FirstOrDefault(p => string.Equals(p.Id, sourcePortId, StringComparison.Ordinal))
                : null;
            if (byId != null) return byId;

            if (sourcePort == null) return null;

            // 2) Fallback: semantic match by input/output + side + order on that side
            var targetCandidates = targetNode.Ports
                .Where(p => p.IsInput == sourcePort.IsInput && string.Equals(p.Position.ToString(), sourcePort.Position, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (targetCandidates.Count == 0)
                return targetNode.Ports.FirstOrDefault(p => p.IsInput == sourcePort.IsInput);

            return targetCandidates[0];
        }

        private static NodePort? TryMapAsyncTaskBodyPort(WorkflowNode bodyNode, string? sourcePortId, ClipboardPortDto? sourcePort)
        {
            var sourceId = sourcePortId ?? sourcePort?.Id ?? string.Empty;
            if (sourceId.IndexOf("LoopBodyTop", StringComparison.OrdinalIgnoreCase) >= 0)
                return bodyNode.Ports.FirstOrDefault(p => string.Equals(p.Id, "LoopBodyTop", StringComparison.OrdinalIgnoreCase));
            if (sourceId.IndexOf("LoopBodyLeft", StringComparison.OrdinalIgnoreCase) >= 0)
                return bodyNode.Ports.FirstOrDefault(p => string.Equals(p.Id, "LoopBodyLeft", StringComparison.OrdinalIgnoreCase));
            if (sourceId.IndexOf("LoopBodyRight", StringComparison.OrdinalIgnoreCase) >= 0)
                return bodyNode.Ports.FirstOrDefault(p => string.Equals(p.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase));

            if (sourcePort == null) return null;

            var pos = sourcePort.Position ?? string.Empty;
            if (sourcePort.IsInput && string.Equals(pos, "Top", StringComparison.OrdinalIgnoreCase))
                return bodyNode.Ports.FirstOrDefault(p => string.Equals(p.Id, "LoopBodyTop", StringComparison.OrdinalIgnoreCase));
            if (!sourcePort.IsInput && string.Equals(pos, "Right", StringComparison.OrdinalIgnoreCase))
                return bodyNode.Ports.FirstOrDefault(p => string.Equals(p.Id, "LoopBodyLeft", StringComparison.OrdinalIgnoreCase));
            if (sourcePort.IsInput && string.Equals(pos, "Left", StringComparison.OrdinalIgnoreCase))
                return bodyNode.Ports.FirstOrDefault(p => string.Equals(p.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase));

            return null;
        }

        private static void EnsureAsyncTaskBodyPortsExistForPaste(AsyncTaskBodyNode bodyNode)
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

        private static void RemapPastedNodeReferences(Dictionary<string, WorkflowNode> sourceToNewNodeMap)
        {
            if (sourceToNewNodeMap.Count == 0) return;

            var pastedNodeIds = new HashSet<string>(
                sourceToNewNodeMap.Values.Select(n => n.Id),
                StringComparer.OrdinalIgnoreCase);

            var visited = new HashSet<WorkflowNode>();
            foreach (var node in sourceToNewNodeMap.Values)
            {
                if (!visited.Add(node)) continue;
                RemapNodeReferenceIds(node, sourceToNewNodeMap);
            }

            // HTTP Request: sau remap, mọi binding nguồn phải trỏ vào node trong cụm vừa dán;
            // tham chiếu tới node không nằm trong selection paste → bỏ binding (theo NODE_CREATION_SPEC §4.9).
            foreach (var node in sourceToNewNodeMap.Values)
            {
                if (node is HttpRequestNode http)
                    ClearHttpRequestBindingsToNodesOutsidePasteSelection(http, pastedNodeIds);
            }
        }

        /// <summary>
        /// Khi paste nhiều node, chỉ giữ binding dynamic tới các bản sao trong cùng clipboard;
        /// id trỏ ra ngoài cụm paste (node gốc không được copy) → xóa để ComboBox không còn trỏ nhầm.
        /// </summary>
        private static void ClearHttpRequestBindingsToNodesOutsidePasteSelection(
            HttpRequestNode http,
            HashSet<string> pastedNodeIds)
        {
            static bool InsidePaste(string? nodeId, HashSet<string> pastedNodeIds)
                => string.IsNullOrWhiteSpace(nodeId) || pastedNodeIds.Contains(nodeId);

            if (!InsidePaste(http.UrlSourceNodeId, pastedNodeIds))
            {
                http.UrlSourceNodeId = null;
                http.UrlSourceOutputKey = null;
            }

            if (!InsidePaste(http.CurlSourceNodeId, pastedNodeIds))
            {
                http.CurlSourceNodeId = null;
                http.CurlSourceOutputKey = null;
            }

            if (!InsidePaste(http.BodySourceNodeId, pastedNodeIds))
            {
                http.BodySourceNodeId = null;
                http.BodySourceOutputKey = null;
            }

            if (!InsidePaste(http.TokenSourceNodeId, pastedNodeIds))
            {
                http.TokenSourceNodeId = null;
                http.TokenSourceOutputKey = null;
            }

            if (!InsidePaste(http.ApiKeyValueSourceNodeId, pastedNodeIds))
            {
                http.ApiKeyValueSourceNodeId = null;
                http.ApiKeyValueSourceOutputKey = null;
            }

            foreach (var h in http.Headers)
            {
                if (!InsidePaste(h.SourceNodeId, pastedNodeIds))
                {
                    h.SourceNodeId = null;
                    h.SourceOutputKey = null;
                }
            }

            foreach (var q in http.QueryParams)
            {
                if (!InsidePaste(q.SourceNodeId, pastedNodeIds))
                {
                    q.SourceNodeId = null;
                    q.SourceOutputKey = null;
                }
            }

            foreach (var f in http.FormData)
            {
                if (!InsidePaste(f.SourceNodeId, pastedNodeIds))
                {
                    f.SourceNodeId = null;
                    f.SourceOutputKey = null;
                }
            }

            if (http.DynamicInputs != null)
            {
                foreach (var di in http.DynamicInputs)
                {
                    if (!InsidePaste(di.SelectedSourceNodeId, pastedNodeIds))
                    {
                        di.SelectedSourceNodeId = null;
                        di.SelectedSourceOutputKey = null;
                    }
                }
            }
        }

        private static void RemapNodeReferenceIds(WorkflowNode node, Dictionary<string, WorkflowNode> sourceToNewNodeMap)
        {
            static string RemapNodeId(string? originalId, Dictionary<string, WorkflowNode> map)
            {
                if (string.IsNullOrWhiteSpace(originalId)) return string.Empty;
                return map.TryGetValue(originalId, out var mapped) ? mapped.Id : originalId;
            }

            if (node.DynamicInputs != null)
            {
                foreach (var input in node.DynamicInputs)
                    input.SelectedSourceNodeId = RemapNodeId(input.SelectedSourceNodeId, sourceToNewNodeMap);
            }

            if (node.ReuseRoutes != null)
            {
                foreach (var route in node.ReuseRoutes)
                {
                    route.IncomingNodeId = RemapNodeId(route.IncomingNodeId, sourceToNewNodeMap);
                    route.OutgoingNodeId = RemapNodeId(route.OutgoingNodeId, sourceToNewNodeMap);
                }
            }

            if (node.ConditionalBranches != null)
            {
                foreach (var branch in node.ConditionalBranches)
                {
                    branch.LeftSourceNodeId = RemapNodeId(branch.LeftSourceNodeId, sourceToNewNodeMap);
                    branch.RightSourceNodeId = RemapNodeId(branch.RightSourceNodeId, sourceToNewNodeMap);
                    if (branch.SubConditions == null) continue;
                    foreach (var sub in branch.SubConditions)
                    {
                        sub.LeftSourceNodeId = RemapNodeId(sub.LeftSourceNodeId, sourceToNewNodeMap);
                        sub.RightSourceNodeId = RemapNodeId(sub.RightSourceNodeId, sourceToNewNodeMap);
                    }
                }
            }

            switch (node)
            {
                case OutputNode outputNode:
                    foreach (var iv in outputNode.InputVariables ?? new List<InputVariable>())
                        iv.SourceNodeId = RemapNodeId(iv.SourceNodeId, sourceToNewNodeMap);
                    break;

                case NotificationNode notificationNode:
                    if (notificationNode.TitleInput != null) notificationNode.TitleInput.SourceNodeId = RemapNodeId(notificationNode.TitleInput.SourceNodeId, sourceToNewNodeMap);
                    if (notificationNode.ContentInput != null) notificationNode.ContentInput.SourceNodeId = RemapNodeId(notificationNode.ContentInput.SourceNodeId, sourceToNewNodeMap);
                    if (notificationNode.DurationInput != null) notificationNode.DurationInput.SourceNodeId = RemapNodeId(notificationNode.DurationInput.SourceNodeId, sourceToNewNodeMap);
                    break;

                case CodeNode codeNode:
                    foreach (var m in codeNode.InputMappings ?? new List<CodeInputMapping>())
                        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);
                    break;

                case HtmlUiNode htmlUiNode:
                    foreach (var m in htmlUiNode.InputMappings ?? new List<CodeInputMapping>())
                        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);
                    foreach (var a in htmlUiNode.AsyncDataSources ?? new List<AsyncDataSource>())
                        a.SourceNodeId = RemapNodeId(a.SourceNodeId, sourceToNewNodeMap);
                    htmlUiNode.WebTabCookieSourceNodeId = RemapNodeId(htmlUiNode.WebTabCookieSourceNodeId, sourceToNewNodeMap);
                    break;

                case WebNode webNode:
                    if (webNode.DynamicInputs != null)
                    {
                        foreach (var input in webNode.DynamicInputs)
                            input.SelectedSourceNodeId = RemapNodeId(input.SelectedSourceNodeId, sourceToNewNodeMap);
                    }
                    foreach (var js in webNode.JsSources ?? new List<WebJsSourceMapping>())
                        js.SourceNodeId = RemapNodeId(js.SourceNodeId, sourceToNewNodeMap);
                    if (webNode.RequestInterceptRules != null)
                    {
                        foreach (var rule in webNode.RequestInterceptRules)
                        {
                            rule.ReplaceUrlSourceNodeId = RemapNodeId(rule.ReplaceUrlSourceNodeId, sourceToNewNodeMap);
                            rule.ReplaceParamsSourceNodeId = RemapNodeId(rule.ReplaceParamsSourceNodeId, sourceToNewNodeMap);
                            rule.ReplaceBodySourceNodeId = RemapNodeId(rule.ReplaceBodySourceNodeId, sourceToNewNodeMap);
                        }
                    }
                    break;

                case AssignDataNode assignDataNode:
                    foreach (var a in assignDataNode.Assignments)
                    {
                        a.SourceNodeId = RemapNodeId(a.SourceNodeId, sourceToNewNodeMap);
                        a.TargetNodeId = RemapNodeId(a.TargetNodeId, sourceToNewNodeMap);
                    }
                    break;

                case FlowOverwriteNode flowOverwriteNode:
                    foreach (var m in flowOverwriteNode.Mappings ?? new List<FlowOverwriteMapping>())
                        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);
                    break;

                case FolderNode folderNode:
                    foreach (var kv in folderNode.KeyValueInputs ?? new List<FolderKeyValueInput>())
                        kv.SourceNodeId = RemapNodeId(kv.SourceNodeId, sourceToNewNodeMap);
                    break;

                case FolderFilePathsNode folderFilePathsNode:
                    folderFilePathsNode.FolderSourceNodeId = RemapNodeId(folderFilePathsNode.FolderSourceNodeId, sourceToNewNodeMap);
                    break;

                case FileDownloadNode fileDownloadNode:
                    fileDownloadNode.UrlSourceNodeId = RemapNodeId(fileDownloadNode.UrlSourceNodeId, sourceToNewNodeMap);
                    fileDownloadNode.CurlSourceNodeId = RemapNodeId(fileDownloadNode.CurlSourceNodeId, sourceToNewNodeMap);
                    fileDownloadNode.FolderSourceNodeId = RemapNodeId(fileDownloadNode.FolderSourceNodeId, sourceToNewNodeMap);
                    fileDownloadNode.FileNameSourceNodeId = RemapNodeId(fileDownloadNode.FileNameSourceNodeId, sourceToNewNodeMap);
                    foreach (var x in fileDownloadNode.AdditionalOutputSaves ?? new List<FileDownloadAdditionalOutputSaveEntry>())
                        x.SourceNodeId = RemapNodeId(x.SourceNodeId, sourceToNewNodeMap);
                    break;

                case HttpRequestNode httpRequestNode:
                    httpRequestNode.TokenSourceNodeId = RemapNodeId(httpRequestNode.TokenSourceNodeId, sourceToNewNodeMap);
                    httpRequestNode.ApiKeyValueSourceNodeId = RemapNodeId(httpRequestNode.ApiKeyValueSourceNodeId, sourceToNewNodeMap);
                    httpRequestNode.UrlSourceNodeId = RemapNodeId(httpRequestNode.UrlSourceNodeId, sourceToNewNodeMap);
                    httpRequestNode.BodySourceNodeId = RemapNodeId(httpRequestNode.BodySourceNodeId, sourceToNewNodeMap);
                    httpRequestNode.CurlSourceNodeId = RemapNodeId(httpRequestNode.CurlSourceNodeId, sourceToNewNodeMap);
                    foreach (var h in httpRequestNode.Headers) h.SourceNodeId = RemapNodeId(h.SourceNodeId, sourceToNewNodeMap);
                    foreach (var q in httpRequestNode.QueryParams) q.SourceNodeId = RemapNodeId(q.SourceNodeId, sourceToNewNodeMap);
                    foreach (var f in httpRequestNode.FormData) f.SourceNodeId = RemapNodeId(f.SourceNodeId, sourceToNewNodeMap);
                    break;

                case MediaGalleryNode mediaGalleryNode:
                    mediaGalleryNode.FolderSourceNodeId = RemapNodeId(mediaGalleryNode.FolderSourceNodeId, sourceToNewNodeMap);
                    mediaGalleryNode.FolderSourceNodeIdVideo = RemapNodeId(mediaGalleryNode.FolderSourceNodeIdVideo, sourceToNewNodeMap);
                    mediaGalleryNode.JsonSourceNodeId = RemapNodeId(mediaGalleryNode.JsonSourceNodeId, sourceToNewNodeMap);
                    break;

                case ImageProcessingNode imageProcessingNode:
                    imageProcessingNode.ImageUrlSourceNodeId = RemapNodeId(imageProcessingNode.ImageUrlSourceNodeId, sourceToNewNodeMap);
                    imageProcessingNode.ImageBase64SourceNodeId = RemapNodeId(imageProcessingNode.ImageBase64SourceNodeId, sourceToNewNodeMap);
                    break;

                case VideoProcessingNode videoProcessingNode:
                    videoProcessingNode.VideoSourceNodeId = RemapNodeId(videoProcessingNode.VideoSourceNodeId, sourceToNewNodeMap);
                    videoProcessingNode.OutputFolderSourceNodeId = RemapNodeId(videoProcessingNode.OutputFolderSourceNodeId, sourceToNewNodeMap);
                    foreach (var track in videoProcessingNode.AudioTracks)
                        track.SourceNodeId = RemapNodeId(track.SourceNodeId, sourceToNewNodeMap);
                    break;

                case KeyValueBridgeNode keyValueBridgeNode:
                    keyValueBridgeNode.SelectedSourceBridgeNodeId = RemapNodeId(keyValueBridgeNode.SelectedSourceBridgeNodeId, sourceToNewNodeMap);
                    keyValueBridgeNode.CleanupTargetBridgeNodeId = RemapNodeId(keyValueBridgeNode.CleanupTargetBridgeNodeId, sourceToNewNodeMap);
                    keyValueBridgeNode.CleanupTriggerSourceNodeId = RemapNodeId(keyValueBridgeNode.CleanupTriggerSourceNodeId, sourceToNewNodeMap);
                    keyValueBridgeNode.CleanupKeySourceNodeId = RemapNodeId(keyValueBridgeNode.CleanupKeySourceNodeId, sourceToNewNodeMap);
                    keyValueBridgeNode.CleanupFilterFieldSourceNodeId = RemapNodeId(keyValueBridgeNode.CleanupFilterFieldSourceNodeId, sourceToNewNodeMap);
                    keyValueBridgeNode.CleanupFilterValueSourceNodeId = RemapNodeId(keyValueBridgeNode.CleanupFilterValueSourceNodeId, sourceToNewNodeMap);
                    foreach (var a in keyValueBridgeNode.AdditionalAppendSources ?? new List<KeyValueBridgeAppendSource>())
                        a.SourceNodeId = RemapNodeId(a.SourceNodeId, sourceToNewNodeMap);
                    break;
            }
        }

        private sealed class WorkflowClipboardEnvelope
        {
            public string Marker { get; set; } = string.Empty;
            public DateTime CreatedAtUtc { get; set; }
            public string? Compression { get; set; }
            public string? WorkflowPayloadBase64 { get; set; }
            public string WorkflowJson { get; set; } = string.Empty;
        }

        private static string? TryExtractWorkflowJsonFromEnvelope(WorkflowClipboardEnvelope envelope)
        {
            if (!string.IsNullOrWhiteSpace(envelope.WorkflowPayloadBase64))
            {
                if (string.Equals(envelope.Compression, WorkflowClipboardCompression, StringComparison.OrdinalIgnoreCase))
                    return TryDecompressFromBase64(envelope.WorkflowPayloadBase64);
                if (string.Equals(envelope.Compression, LegacyWorkflowClipboardCompression, StringComparison.OrdinalIgnoreCase))
                    return TryDecompressGzipFromBase64(envelope.WorkflowPayloadBase64);

                // Unknown compression marker: fallback to null to avoid importing corrupted payload.
                return null;
            }

            // Backward compatibility with old plaintext clipboard payload.
            return string.IsNullOrWhiteSpace(envelope.WorkflowJson) ? null : envelope.WorkflowJson;
        }

        private static string CompressToBase64(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            using var output = new MemoryStream();
            // Brotli cho tỷ lệ nén tốt hơn gzip, decompression vẫn đủ nhanh cho paste UX.
            using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                brotli.Write(bytes, 0, bytes.Length);
            }
            return Convert.ToBase64String(output.ToArray());
        }

        private static string? TryDecompressFromBase64(string base64)
        {
            try
            {
                var compressed = Convert.FromBase64String(base64);
                using var input = new MemoryStream(compressed);
                using var brotli = new BrotliStream(input, CompressionMode.Decompress);
                using var reader = new StreamReader(brotli, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch
            {
                return null;
            }
        }

        private static string? TryDecompressGzipFromBase64(string base64)
        {
            try
            {
                var compressed = Convert.FromBase64String(base64);
                using var input = new MemoryStream(compressed);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch
            {
                return null;
            }
        }

        private sealed class ClipboardWorkflowDto
        {
            public List<ClipboardNodeDto> Nodes { get; set; } = new();
            public List<ClipboardConnectionDto> Connections { get; set; } = new();
        }

        private sealed class ClipboardNodeDto
        {
            public string? Id { get; set; }
            public List<ClipboardPortDto> Ports { get; set; } = new();
        }

        private sealed class ClipboardPortDto
        {
            public string? Id { get; set; }
            public bool IsInput { get; set; }
            public string? Position { get; set; }
        }

        private sealed class ClipboardConnectionDto
        {
            public string? FromNodeId { get; set; }
            public string? ToNodeId { get; set; }
            public string? FromPortId { get; set; }
            public string? ToPortId { get; set; }
        }

        private static List<WorkflowNode> ExpandSelectionWithCompanionBodyNodes(List<WorkflowNode> selectedNodes)
        {
            var expanded = new List<WorkflowNode>(selectedNodes);
            var ids = new HashSet<string>(selectedNodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var node in selectedNodes)
            {
                if (node is LoopNode loop && loop.LoopBodyNode != null && ids.Add(loop.LoopBodyNode.Id))
                    expanded.Add(loop.LoopBodyNode);

                if (node is AsyncTaskNode asyncTask &&
                    asyncTask.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch &&
                    asyncTask.AsyncTaskBodyNode != null &&
                    ids.Add(asyncTask.AsyncTaskBodyNode.Id))
                {
                    expanded.Add(asyncTask.AsyncTaskBodyNode);
                }
            }

            return expanded;
        }

        private static bool IsNodeLockedByBodyContainer(ViewModels.WorkflowEditorViewModel vm, WorkflowNode node)
        {
            if (node is BodyContainerNode) return false;
            foreach (var body in vm.Nodes.OfType<BodyContainerNode>())
            {
                if (!body.LockInnerNodes) continue;
                var width = body.BodyWidth > 0 ? body.BodyWidth : (body.Border?.ActualWidth ?? body.Border?.Width ?? 0);
                var height = body.BodyHeight > 0 ? body.BodyHeight : (body.Border?.ActualHeight ?? body.Border?.Height ?? 0);
                if (width <= 0 || height <= 0) continue;
                var nodeW = node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150;
                var nodeH = node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80;
                if (new Rect(body.X, body.Y, width, height).Contains(new Point(node.X + nodeW / 2.0, node.Y + nodeH / 2.0)))
                    return true;
            }
            return false;
        }

        private void BeginBoxSelection(Point startCanvasPoint)
        {
            CancelBoxSelection();

            _isBoxSelecting = true;
            _boxSelectionStart = startCanvasPoint;
            _boxSelectedNodes.Clear();

            _selectionDragBorder = new Border
            {
                BorderThickness = new Thickness(1.5),
                BorderBrush = Brushes.DeepSkyBlue,
                Background = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255)),
                IsHitTestVisible = false
            };

            WorkflowCanvas.Children.Add(_selectionDragBorder);
            Panel.SetZIndex(_selectionDragBorder, 1_999_999);
            WorkflowCanvas.CaptureMouse();
            UpdateBoxSelection(startCanvasPoint);
        }

        private void UpdateBoxSelection(Point currentCanvasPoint)
        {
            if (!_isBoxSelecting || _selectionDragBorder == null) return;

            var left = Math.Min(_boxSelectionStart.X, currentCanvasPoint.X);
            var top = Math.Min(_boxSelectionStart.Y, currentCanvasPoint.Y);
            var width = Math.Abs(currentCanvasPoint.X - _boxSelectionStart.X);
            var height = Math.Abs(currentCanvasPoint.Y - _boxSelectionStart.Y);

            Canvas.SetLeft(_selectionDragBorder, left);
            Canvas.SetTop(_selectionDragBorder, top);
            _selectionDragBorder.Width = width;
            _selectionDragBorder.Height = height;

            UpdateSelectedNodesInRect(new Rect(left, top, width, height));
        }

        private void CompleteBoxSelection()
        {
            _isBoxSelecting = false;
            WorkflowCanvas.ReleaseMouseCapture();
            if (_selectionDragBorder != null && WorkflowCanvas.Children.Contains(_selectionDragBorder))
                WorkflowCanvas.Children.Remove(_selectionDragBorder);
            _selectionDragBorder = null;

            ShowSelectionResultBorder();
        }

        private void CancelBoxSelection()
        {
            _isBoxSelecting = false;
            WorkflowCanvas.ReleaseMouseCapture();
            if (_selectionDragBorder != null && WorkflowCanvas.Children.Contains(_selectionDragBorder))
                WorkflowCanvas.Children.Remove(_selectionDragBorder);
            _selectionDragBorder = null;
            if (_selectionResultBorder != null && WorkflowCanvas.Children.Contains(_selectionResultBorder))
                WorkflowCanvas.Children.Remove(_selectionResultBorder);
            _selectionResultBorder = null;
            _boxSelectedNodes.Clear();
        }

        private void UpdateSelectedNodesInRect(Rect selectionRect)
        {
            var vm = ViewModel;
            if (vm == null) return;

            _boxSelectedNodes.Clear();
            foreach (var node in vm.Nodes)
            {
                var width = node.Border?.ActualWidth;
                var height = node.Border?.ActualHeight;
                var nodeRect = new Rect(
                    node.X,
                    node.Y,
                    width.GetValueOrDefault(150) > 1 ? width!.Value : 150,
                    height.GetValueOrDefault(80) > 1 ? height!.Value : 80);

                if (selectionRect.IntersectsWith(nodeRect))
                    _boxSelectedNodes.Add(node);
            }

            vm.SelectedNode = _boxSelectedNodes.LastOrDefault();
        }

        private void ShowSelectionResultBorder()
        {
            if (_selectionResultBorder != null && WorkflowCanvas.Children.Contains(_selectionResultBorder))
                WorkflowCanvas.Children.Remove(_selectionResultBorder);
            _selectionResultBorder = null;

            if (_boxSelectedNodes.Count == 0) return;

            Rect? merged = null;
            foreach (var node in _boxSelectedNodes)
            {
                var width = node.Border?.ActualWidth;
                var height = node.Border?.ActualHeight;
                var nodeRect = new Rect(
                    node.X,
                    node.Y,
                    width.GetValueOrDefault(150) > 1 ? width!.Value : 150,
                    height.GetValueOrDefault(80) > 1 ? height!.Value : 80);
                merged = merged == null ? nodeRect : Rect.Union(merged.Value, nodeRect);
            }

            if (merged == null) return;

            const double padding = 8;
            var frame = merged.Value;
            frame.Inflate(padding, padding);

            _selectionResultBorder = new Border
            {
                BorderThickness = new Thickness(1.8),
                BorderBrush = Brushes.DeepSkyBlue,
                Background = new SolidColorBrush(Color.FromArgb(18, 30, 144, 255)),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(_selectionResultBorder, frame.Left);
            Canvas.SetTop(_selectionResultBorder, frame.Top);
            _selectionResultBorder.Width = frame.Width;
            _selectionResultBorder.Height = frame.Height;
            WorkflowCanvas.Children.Add(_selectionResultBorder);
            Panel.SetZIndex(_selectionResultBorder, 1_999_998);
        }
    }
}
