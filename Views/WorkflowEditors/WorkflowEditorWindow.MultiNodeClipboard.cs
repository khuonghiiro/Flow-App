using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
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
                WorkflowJson = workflowJson
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
                !string.Equals(envelope.Marker, WorkflowClipboardMarker, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(envelope.WorkflowJson))
            {
                return false;
            }

            var persistence = new FileWorkflowPersistenceService(_templateFactory);
            var load = persistence.ImportFromJson(envelope.WorkflowJson);
            if (load == null || load.Nodes.Count == 0) return false;
            ClipboardWorkflowDto? rawWorkflowDto = null;
            try
            {
                rawWorkflowDto = JsonSerializer.Deserialize<ClipboardWorkflowDto>(envelope.WorkflowJson);
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

        private sealed class WorkflowClipboardEnvelope
        {
            public string Marker { get; set; } = string.Empty;
            public DateTime CreatedAtUtc { get; set; }
            public string WorkflowJson { get; set; } = string.Empty;
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
