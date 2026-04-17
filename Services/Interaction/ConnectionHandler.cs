using FlowMy.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowMy.Services.Rendering;

namespace FlowMy.Services.Interaction
{
    public sealed class ConnectionHandler
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;

        private IWorkflowEditorHost _host => _hostAccessor.GetRequiredHost();

        public ConnectionHandler(IWorkflowEditorHostAccessor hostAccessor)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void PortMouseDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = _host.ViewModel;
            if (viewModel == null) return;

            e.Handled = true;
            if (sender is not Shape port) return;

            // ✅ Tìm node owner (có thể là node thường hoặc LoopBodyNode)
            // Hỗ trợ cả port trực tiếp và port có wrapper Border
            _host.ConnectingFromNode = viewModel.Nodes.FirstOrDefault(n => 
                n.Ports.Any(p => (p.PortUI == port || (p.PortUI is Border border && border.Child == port)) && p.IsVisible));

            // ✅ Nếu không tìm thấy, tìm trong LoopBodyNode
            if (_host.ConnectingFromNode == null)
            {
                foreach (var loopNode in viewModel.Nodes.OfType<LoopNode>())
                {
                    if (loopNode.LoopBodyNode.Ports.Any(p => 
                        (p.PortUI == port || (p.PortUI is Border border && border.Child == port)) && p.IsVisible))
                    {
                        _host.ConnectingFromNode = loopNode.LoopBodyNode;
                        break;
                    }
                }
            }

            // ✅ Nếu vẫn không tìm thấy, tìm trong AsyncTaskBodyNode (chế độ giống lặp)
            if (_host.ConnectingFromNode == null)
            {
                foreach (var asyncTaskNode in viewModel.Nodes.OfType<AsyncTaskNode>())
                {
                    if (asyncTaskNode.AsyncTaskBodyNode == null) continue;
                    if (asyncTaskNode.UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch) continue;

                    if (asyncTaskNode.AsyncTaskBodyNode.Ports.Any(p =>
                        (p.PortUI == port || (p.PortUI is Border border && border.Child == port)) && p.IsVisible))
                    {
                        _host.ConnectingFromNode = asyncTaskNode.AsyncTaskBodyNode;
                        break;
                    }
                }
            }

            if (_host.ConnectingFromNode == null) return;

            var connectingPort = _host.ConnectingFromNode.Ports.FirstOrDefault(p => 
                (p.PortUI == port || (p.PortUI is Border border && border.Child == port)) && p.IsVisible);
            if (connectingPort == null) return;

            // ✅ Không cho phép kết nối từ default ports
            if (connectingPort.Id == "LoopNodeBottom" || connectingPort.Id == "LoopBodyTop")
            {
                System.Diagnostics.Debug.WriteLine("Cannot start connection from default loop ports");
                _host.ConnectingFromNode = null;
                return;
            }

            // ✅ Capture mouse trên portUI (Border wrapper) để đảm bảo capture hoạt động ngay cả khi mouse di chuyển ra ngoài Rectangle
            // Border có IsHitTestVisible = false nên mouse events vẫn đi qua đến Rectangle
            if (connectingPort.PortUI is FrameworkElement portUIElement)
            {
                portUIElement.CaptureMouse();
            }
            else
            {
                port.CaptureMouse();
            }

            Point startPos = connectingPort.PositionPoint;
            PortPosition? startPortPos = connectingPort.Position;

            _host.TempLine = _host.CreateConnectionLine(
                startPos,
                startPos,
                Colors.Gray,
                isDashed: true,
                startPortPosition: startPortPos,
                endPortPosition: null);

            Panel.SetZIndex(_host.TempLine, 999);
            _host.WorkflowCanvas.Children.Add(_host.TempLine);
        }

        public void PortMouseUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel = _host.ViewModel;
            if (viewModel == null) return;

            e.Handled = true;

            var sourcePort = sender as Shape;
            
            if (_host.ConnectingFromNode == null)
            {
                // Release mouse capture nếu có
                if (sourcePort != null && sourcePort.IsMouseCaptured)
                {
                    sourcePort.ReleaseMouseCapture();
                }
                RemoveTempLineIfAny();
                return;
            }

            // Hỗ trợ cả port trực tiếp và port có wrapper Border
            var fromPort = _host.ConnectingFromNode.Ports.FirstOrDefault(p => 
                (p.PortUI == sourcePort || (p.PortUI is Border border && border.Child == sourcePort)) && p.IsVisible);
            
            // ✅ Release mouse capture từ portUI (Border wrapper) nếu có, hoặc từ Shape trực tiếp
            if (fromPort?.PortUI is FrameworkElement portUIElement && portUIElement.IsMouseCaptured)
            {
                portUIElement.ReleaseMouseCapture();
            }
            else if (sourcePort != null && sourcePort.IsMouseCaptured)
            {
                sourcePort.ReleaseMouseCapture();
            }
            
            if (fromPort == null)
            {
                RemoveTempLineIfAny();
                return;
            }

            // ✅ KIỂM TRA: Chỉ không cho kết nối với 2 ports default
            if (fromPort.Id == "LoopNodeBottom" || fromPort.Id == "LoopBodyTop")
            {
                System.Diagnostics.Debug.WriteLine("Cannot connect to default loop ports");
                RemoveTempLineIfAny();
                _host.ConnectingFromNode = null;
                return;
            }

            bool isFromInput = fromPort.IsInput;
            bool isFromOutput = !fromPort.IsInput;

            Point mousePos = e.GetPosition(_host.WorkflowCanvas);
            var hitElement = _host.WorkflowCanvas.InputHitTest(mousePos);

            WorkflowNode? targetNode = null;
            NodePort? targetPort = null;
            bool isToInput = false;
            bool isToOutput = false;

            // ✅ Kiểm tra cả Shape và Border wrapper
            if (hitElement is Shape hitShape)
            {
                // Tìm trong tất cả nodes (bao gồm cả LoopBodyNode)
                foreach (var node in viewModel.Nodes)
                {
                    targetPort = node.Ports.FirstOrDefault(p => 
                        (p.PortUI == hitShape || (p.PortUI is Border border && border.Child == hitShape)) && p.IsVisible);
                    if (targetPort != null)
                    {
                        targetNode = node;
                        isToInput = targetPort.IsInput;
                        isToOutput = !targetPort.IsInput;
                        break;
                    }
                }

                // Nếu không tìm thấy, tìm trong LoopBodyNode
                if (targetPort == null)
                {
                    foreach (var node in viewModel.Nodes.OfType<LoopNode>())
                    {
                        targetPort = node.LoopBodyNode.Ports.FirstOrDefault(p => 
                            (p.PortUI == hitShape || (p.PortUI is Border border && border.Child == hitShape)) && p.IsVisible);
                        if (targetPort != null)
                        {
                            targetNode = node.LoopBodyNode;
                            isToInput = targetPort.IsInput;
                            isToOutput = !targetPort.IsInput;
                            break;
                        }
                    }
                }

                // Nếu vẫn không tìm thấy, tìm trong AsyncTaskBodyNode (chế độ giống lặp)
                if (targetPort == null)
                {
                    foreach (var asyncTask in viewModel.Nodes.OfType<AsyncTaskNode>())
                    {
                        if (asyncTask.AsyncTaskBodyNode == null) continue;
                        if (asyncTask.UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch) continue;

                        targetPort = asyncTask.AsyncTaskBodyNode.Ports.FirstOrDefault(p =>
                            (p.PortUI == hitShape || (p.PortUI is Border border && border.Child == hitShape)) && p.IsVisible);

                        if (targetPort != null)
                        {
                            targetNode = asyncTask.AsyncTaskBodyNode;
                            isToInput = targetPort.IsInput;
                            isToOutput = !targetPort.IsInput;
                            break;
                        }
                    }
                }
            }
            else if (hitElement is Border hitBorder)
            {
                // ✅ Kiểm tra nếu hit vào Border wrapper của port
                foreach (var node in viewModel.Nodes)
                {
                    targetPort = node.Ports.FirstOrDefault(p => p.PortUI == hitBorder && p.IsVisible);
                    if (targetPort != null)
                    {
                        targetNode = node;
                        isToInput = targetPort.IsInput;
                        isToOutput = !targetPort.IsInput;
                        break;
                    }
                }

                if (targetPort == null)
                {
                    foreach (var node in viewModel.Nodes.OfType<LoopNode>())
                    {
                        targetPort = node.LoopBodyNode.Ports.FirstOrDefault(p => p.PortUI == hitBorder && p.IsVisible);
                        if (targetPort != null)
                        {
                            targetNode = node.LoopBodyNode;
                            isToInput = targetPort.IsInput;
                            isToOutput = !targetPort.IsInput;
                            break;
                        }
                    }
                }

                // Nếu vẫn không tìm thấy, tìm trong AsyncTaskBodyNode (chế độ giống lặp)
                if (targetPort == null)
                {
                    foreach (var asyncTask in viewModel.Nodes.OfType<AsyncTaskNode>())
                    {
                        if (asyncTask.AsyncTaskBodyNode == null) continue;
                        if (asyncTask.UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch) continue;

                        targetPort = asyncTask.AsyncTaskBodyNode.Ports.FirstOrDefault(p => p.PortUI == hitBorder && p.IsVisible);
                        if (targetPort != null)
                        {
                            targetNode = asyncTask.AsyncTaskBodyNode;
                            isToInput = targetPort.IsInput;
                            isToOutput = !targetPort.IsInput;
                            break;
                        }
                    }
                }
            }
            
            // ✅ Nếu không tìm thấy qua hit-test, dùng distance-based detection với threshold lớn hơn
            if (targetPort == null)
            {
                double minDistance = double.MaxValue;
                const double maxHitDistance = 30; // Tăng threshold để bao phủ port chữ nhật lớn (12x25 + margin)

                // Tìm trong tất cả nodes
                foreach (var node in viewModel.Nodes)
                {
                    if (node == _host.ConnectingFromNode) continue;

                    foreach (var port in node.Ports.Where(p => p.IsVisible))
                    {
                        if (port.Id == "LoopNodeBottom" || port.Id == "LoopBodyTop")
                            continue;

                        double distance = Math.Sqrt(
                            Math.Pow(mousePos.X - port.PositionPoint.X, 2) +
                            Math.Pow(mousePos.Y - port.PositionPoint.Y, 2)
                        );

                        if (distance < maxHitDistance && distance < minDistance)
                        {
                            minDistance = distance;
                            targetNode = node;
                            targetPort = port;
                            isToInput = port.IsInput;
                            isToOutput = !port.IsInput;
                        }
                    }
                }

                // Tìm trong LoopBodyNode
                if (targetPort == null)
                {
                    foreach (var node in viewModel.Nodes.OfType<LoopNode>())
                    {
                        if (node.LoopBodyNode == _host.ConnectingFromNode) continue;

                        foreach (var port in node.LoopBodyNode.Ports.Where(p => p.IsVisible))
                        {
                            if (port.Id == "LoopNodeBottom" || port.Id == "LoopBodyTop")
                                continue;

                            double distance = Math.Sqrt(
                                Math.Pow(mousePos.X - port.PositionPoint.X, 2) +
                                Math.Pow(mousePos.Y - port.PositionPoint.Y, 2)
                            );

                            if (distance < maxHitDistance && distance < minDistance)
                            {
                                minDistance = distance;
                                targetNode = node.LoopBodyNode;
                                targetPort = port;
                                isToInput = port.IsInput;
                                isToOutput = !port.IsInput;
                            }
                        }
                    }
                }

                // Tìm trong AsyncTaskBodyNode
                if (targetPort == null)
                {
                    foreach (var asyncTask in viewModel.Nodes.OfType<AsyncTaskNode>())
                    {
                        var body = asyncTask.AsyncTaskBodyNode;
                        if (body == null) continue;
                        if (asyncTask.UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch) continue;
                        if (body == _host.ConnectingFromNode) continue;

                        foreach (var port in body.Ports.Where(p => p.IsVisible))
                        {
                            if (port.Id == "LoopNodeBottom" || port.Id == "LoopBodyTop")
                                continue;

                            double distance = Math.Sqrt(
                                Math.Pow(mousePos.X - port.PositionPoint.X, 2) +
                                Math.Pow(mousePos.Y - port.PositionPoint.Y, 2)
                            );

                            if (distance < maxHitDistance && distance < minDistance)
                            {
                                minDistance = distance;
                                targetNode = body;
                                targetPort = port;
                                isToInput = port.IsInput;
                                isToOutput = !port.IsInput;
                            }
                        }
                    }
                }
            }
            
            // ✅ Kiểm tra default ports sau khi tìm thấy targetPort
            if (targetPort != null && (targetPort.Id == "LoopNodeBottom" || targetPort.Id == "LoopBodyTop"))
            {
                System.Diagnostics.Debug.WriteLine("Cannot connect to default loop ports");
                RemoveTempLineIfAny();
                _host.ConnectingFromNode = null;
                return;
            }

            if (targetNode != null && targetNode != _host.ConnectingFromNode && targetPort != null)
            {
                if ((isFromInput && isToOutput) || (isFromOutput && isToInput))
                {
                    var fromNode = _host.ConnectingFromNode;

                    // ✅ RULE: Break / Continue chỉ kết nối được các node nằm trong cùng LoopBody cluster.
                    // Nếu không có node nào kết nối với body thì cũng không connect được.
                    if (fromNode is BreakNode or ContinueNode || targetNode is BreakNode or ContinueNode)
                    {
                        if (!AreNodesInSameLoopBodyCluster(viewModel, fromNode, targetNode))
                        {
                            System.Diagnostics.Debug.WriteLine("Break/Continue connections must stay inside a LoopBody cluster.");
                            RemoveTempLineIfAny();
                            _host.ConnectingFromNode = null;
                            return;
                        }
                    }

                    // ✅ ALWAYS: Connection phải đi từ output port → input port
                    // Nếu kéo từ input port sang output port, cần swap lại
                    // Điều này đảm bảo mũi tên luôn trỏ về input port và animation chạy đúng hướng
                    WorkflowNode actualFromNode;
                    WorkflowNode actualToNode;
                    NodePort actualFromPort;
                    NodePort actualToPort;

                    if (isFromInput)
                    {
                        // Kéo từ input → output: swap để connection đi từ output → input
                        actualFromNode = targetNode;
                        actualToNode = fromNode;
                        actualFromPort = targetPort;
                        actualToPort = fromPort;
                    }
                    else
                    {
                        // Kéo từ output → input: giữ nguyên
                        actualFromNode = fromNode;
                        actualToNode = targetNode;
                        actualFromPort = fromPort;
                        actualToPort = targetPort;
                    }

                    var connection = new WorkflowConnection
                    {
                        FromNode = actualFromNode,
                        ToNode = actualToNode,
                        FromPort = actualFromPort,
                        ToPort = actualToPort,
                        IsFromInput = false, // Connection luôn đi từ output → input
                        IsDeleteVisible = actualFromPort.CanDeleteConnection && actualToPort.CanDeleteConnection
                    };

                    if (!viewModel.Connections.Any(c =>
                        c.FromNode == connection.FromNode &&
                        c.ToNode == connection.ToNode &&
                        c.FromPort == connection.FromPort &&
                        c.ToPort == connection.ToPort))
                    {
                        viewModel.Connections.Add(connection);
                        _host.RenderConnection(connection);
                        
                        // ✅ Rebuild outputs cho LoopNodes liên quan (nếu connection liên quan đến LoopBody)
                        RebuildLoopNodeOutputsIfNeeded(viewModel, connection);
                    }
                }
            }

            RemoveTempLineIfAny();

            foreach (var node in viewModel.Nodes)
            {
                foreach (var port in node.Ports.Where(p => p.IsVisible && p.PortUI != null))
                {
                    ResetPortToDefault(port.PortUI!);
                }
            }

            // ✅ Reset ports của LoopBodyNode
            foreach (var loopNode in viewModel.Nodes.OfType<LoopNode>())
            {
                foreach (var port in loopNode.LoopBodyNode.Ports.Where(p => p.IsVisible && p.PortUI != null))
                {
                    ResetPortToDefault(port.PortUI!);
                }
            }

            // ✅ Reset ports của AsyncTaskBodyNode (chế độ giống lặp)
            foreach (var asyncTask in viewModel.Nodes.OfType<AsyncTaskNode>())
            {
                if (asyncTask.UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch) continue;
                if (asyncTask.AsyncTaskBodyNode == null) continue;

                foreach (var port in asyncTask.AsyncTaskBodyNode.Ports.Where(p => p.IsVisible && p.PortUI != null))
                {
                    ResetPortToDefault(port.PortUI!);
                }
            }

            _host.ConnectingFromNode = null;
        }

        public void WorkflowCanvasMouseMove(object sender, MouseEventArgs e)
        {
            var viewModel = _host.ViewModel;

            if (_host.IsBoxSelecting)
            {
                _host.UpdateBoxSelection(e.GetPosition(_host.WorkflowCanvas));
                return;
            }

            // Bỏ hiệu ứng hover cho Windy style - đã thay bằng wave animation tự động

            // Template ghost
            if (_host.IsDraggingFromTemplate && e.LeftButton == MouseButtonState.Pressed && _host.DragGhost != null)
            {
                Point mousePos = e.GetPosition(_host.WorkflowCanvas);
                Canvas.SetLeft(_host.DragGhost, mousePos.X - 75);
                Canvas.SetTop(_host.DragGhost, mousePos.Y - 40);
                return;
            }

            // Pan
            if (_host.IsPanning && e.LeftButton == MouseButtonState.Pressed && _host.DraggedNode == null)
            {
                Point currentPos = e.GetPosition(_host.ScrollViewer);
                double deltaX = currentPos.X - _host.PanStartPoint.X;
                double deltaY = currentPos.Y - _host.PanStartPoint.Y;

                _host.TranslateTransform.X += deltaX;
                _host.TranslateTransform.Y += deltaY;

                if (_host.GridTranslateTransform != null)
                {
                    _host.GridTranslateTransform.X = _host.TranslateTransform.X;
                    _host.GridTranslateTransform.Y = _host.TranslateTransform.Y;
                }

                _host.PanStartPoint = currentPos;
                return;
            }

            // Temp line
            if (_host.TempLine != null && _host.ConnectingFromNode != null)
            {
                Point mousePos = e.GetPosition(_host.WorkflowCanvas);

                var connectingPort = _host.ConnectingFromNode.Ports.FirstOrDefault(p => p.PortUI != null && p.PortUI.IsMouseCaptured);
                Point startPos = connectingPort != null ? connectingPort.PositionPoint : mousePos;
                PortPosition? fromPortPosition = connectingPort?.Position;

                PortPosition? toPortPosition = null;
                Point endPos = mousePos;

                // ✅ Reset tất cả ports (bao gồm cả LoopBodyNode)
                if (viewModel != null)
                {
                    foreach (var node in viewModel.Nodes)
                    {
                        foreach (var port in node.Ports.Where(p => p.IsVisible && p.PortUI != null))
                        {
                            ResetPortToDefault(port.PortUI!);
                        }
                    }

                    // ✅ Reset ports của LoopBodyNode
                    foreach (var loopNode in viewModel.Nodes.OfType<LoopNode>())
                    {
                        foreach (var port in loopNode.LoopBodyNode.Ports.Where(p => p.IsVisible && p.PortUI != null))
                        {
                            ResetPortToDefault(port.PortUI!);
                        }
                    }

                    // ✅ Reset ports của AsyncTaskBodyNode (chế độ giống lặp)
                    foreach (var asyncTask in viewModel.Nodes.OfType<AsyncTaskNode>())
                    {
                        if (asyncTask.UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch) continue;
                        if (asyncTask.AsyncTaskBodyNode == null) continue;

                        foreach (var port in asyncTask.AsyncTaskBodyNode.Ports.Where(p => p.IsVisible && p.PortUI != null))
                        {
                            ResetPortToDefault(port.PortUI!);
                        }
                    }
                }

                var hitElement = _host.WorkflowCanvas.InputHitTest(mousePos);
                NodePort? foundPort = null;
                
                if (viewModel != null)
                {
                    // ✅ Kiểm tra cả Shape và Border wrapper
                    if (hitElement is Shape hitShape)
                    {
                        // Tìm trong nodes thường
                        foreach (var node in viewModel.Nodes)
                        {
                            foundPort = node.Ports.FirstOrDefault(p => 
                                (p.PortUI == hitShape || (p.PortUI is Border border && border.Child == hitShape)) && p.IsVisible);
                            if (foundPort != null) break;
                        }

                        // Tìm trong LoopBodyNode
                        if (foundPort == null)
                        {
                            foreach (var loopNode in viewModel.Nodes.OfType<LoopNode>())
                            {
                                foundPort = loopNode.LoopBodyNode.Ports.FirstOrDefault(p => 
                                    (p.PortUI == hitShape || (p.PortUI is Border border && border.Child == hitShape)) && p.IsVisible);
                                if (foundPort != null) break;
                            }
                        }
                    }
                    else if (hitElement is Border hitBorder)
                    {
                        // ✅ Kiểm tra nếu hit vào Border wrapper của port
                        foreach (var node in viewModel.Nodes)
                        {
                            foundPort = node.Ports.FirstOrDefault(p => p.PortUI == hitBorder && p.IsVisible);
                            if (foundPort != null) break;
                        }

                        if (foundPort == null)
                        {
                            foreach (var loopNode in viewModel.Nodes.OfType<LoopNode>())
                            {
                                foundPort = loopNode.LoopBodyNode.Ports.FirstOrDefault(p => p.PortUI == hitBorder && p.IsVisible);
                                if (foundPort != null) break;
                            }
                        }
                    }
                    
                    // ✅ Nếu không tìm thấy qua hit-test, dùng distance-based detection với threshold lớn hơn
                    if (foundPort == null)
                    {
                        double minDistance = double.MaxValue;
                        const double maxHitDistance = 30; // Tăng threshold để bao phủ port chữ nhật lớn (12x25 + margin)

                        foreach (var node in viewModel.Nodes)
                        {
                            foreach (var port in node.Ports.Where(p => p.IsVisible && p.PortUI != null))
                            {
                                double distance = Math.Sqrt(
                                    Math.Pow(mousePos.X - port.PositionPoint.X, 2) +
                                    Math.Pow(mousePos.Y - port.PositionPoint.Y, 2)
                                );

                                if (distance < maxHitDistance && distance < minDistance)
                                {
                                    minDistance = distance;
                                    foundPort = port;
                                }
                            }
                        }

                        // Tìm trong LoopBodyNode
                        if (foundPort == null)
                        {
                            foreach (var loopNode in viewModel.Nodes.OfType<LoopNode>())
                            {
                                foreach (var port in loopNode.LoopBodyNode.Ports.Where(p => p.IsVisible && p.PortUI != null))
                                {
                                    double distance = Math.Sqrt(
                                        Math.Pow(mousePos.X - port.PositionPoint.X, 2) +
                                        Math.Pow(mousePos.Y - port.PositionPoint.Y, 2)
                                    );

                                    if (distance < maxHitDistance && distance < minDistance)
                                    {
                                        minDistance = distance;
                                        foundPort = port;
                                    }
                                }
                            }
                        }
                    }
                }

                if (foundPort != null)
                {
                    HighlightPort(foundPort.PortUI!);
                    endPos = foundPort.PositionPoint;
                    toPortPosition = foundPort.Position;
                }
                else
                {
                    toPortPosition = PredictPortDirection(startPos, mousePos, fromPortPosition);
                }

                if (toPortPosition.HasValue && hitElement is Shape)
                {
                    endPos = _host.ShortenPoint(endPos, toPortPosition.Value, 2);
                }

                var preview = _host.CreateConnectionLine(
                    startPos,
                    endPos,
                    Colors.Gray,
                    isDashed: true,
                    startPortPosition: fromPortPosition,
                    endPortPosition: toPortPosition);

                _host.TempLine.Data = preview.Data;
            }
        }

        public void WorkflowCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_host.IsBoxSelecting)
            {
                _host.CompleteBoxSelection();
                return;
            }

            // Template drop fallback
            if (_host.IsDraggingFromTemplate && _host.DraggingNodeType != null)
            {
                Point dropPosition;
                if (_host.DragGhost != null)
                {
                    dropPosition = new Point(Canvas.GetLeft(_host.DragGhost) + 75, Canvas.GetTop(_host.DragGhost) + 40);
                }
                else
                {
                    dropPosition = e.GetPosition(_host.WorkflowCanvas);
                }

                _host.CreateNodeFromTemplate(_host.DraggingNodeType, dropPosition.X, dropPosition.Y);
                _host.RemoveDragGhost();

                if (e.OriginalSource is FrameworkElement element)
                {
                    _host.FindTemplateBorder(element)?.ReleaseMouseCapture();
                }

                _host.IsDraggingFromTemplate = false;
                _host.DraggingNodeType = null;
                return;
            }

            if (_host.IsPanning)
            {
                _host.IsPanning = false;
                _host.WorkflowCanvas.ReleaseMouseCapture();
                
                // Re-enable NodeChrome handlers after pan ends
                var viewModel = _host.ViewModel;
                if (viewModel != null && viewModel.Nodes.Count > 550)
                {
                    NodeChrome.SetZoomingState(false);
                }
                
                // Update ViewModel with current pan state (effectivePan = Translate - Scroll)
                if (viewModel != null)
                {
                    var effectivePanX = _host.TranslateTransform.X - _host.ScrollViewer.HorizontalOffset;
                    var effectivePanY = _host.TranslateTransform.Y - _host.ScrollViewer.VerticalOffset;
                    if (Math.Abs(viewModel.PanX - effectivePanX) > 0.1)
                        viewModel.PanX = effectivePanX;
                    if (Math.Abs(viewModel.PanY - effectivePanY) > 0.1)
                        viewModel.PanY = effectivePanY;
                }
                
                // Cập nhật canvas size sau khi pan để đảm bảo có thể scroll tiếp
                _host.UpdateCanvasSize();
            }

            RemoveTempLineIfAny();
            _host.ConnectingFromNode = null;
        }

        private void CloseNodeDialogIfOpen()
        {
            // Lấy NodeDialogManager từ host (WorkflowEditorWindow)
            if (_host is Window window)
            {
                var field = window.GetType().GetField("_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager)
                {
                    manager.CloseCurrentDialog();
                }
            }
        }

        public void WorkflowCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = _host.ViewModel;
            if (viewModel == null) return;

            // Kiểm tra xem click có phải vào dialog không
            var hitElement = e.OriginalSource as DependencyObject;
            if (hitElement != null && _host is Window window)
            {
                var field = window.GetType().GetField("_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager)
                {
                    if (manager.IsElementInDialog(hitElement))
                    {
                        // Click vào dialog -> không đóng và không xử lý gì
                        return;
                    }
                }
            }

            // Kiểm tra xem click có phải vào canvas không (không phải window khác)
            var canvas = sender as System.Windows.Controls.Canvas;
            if (canvas == null) return; // Nếu không phải canvas thì không xử lý

            var clickedNode = null as WorkflowNode;

            while (hitElement != null && clickedNode == null)
            {
                clickedNode = viewModel.Nodes.FirstOrDefault(n => n.Border == hitElement);
                if (clickedNode == null)
                {
                    clickedNode = viewModel.Nodes.FirstOrDefault(n => n.InputPort == hitElement || n.OutputPort == hitElement);
                }
                hitElement = hitElement is FrameworkElement fe ? fe.Parent as DependencyObject : null;
            }

            if (clickedNode != null)
            {
                // Nếu click vào node khác, đóng dialog hiện tại (nếu có)
                // Dialog sẽ được mở trong NodeMouseUp của KeyPressEventNodeControl nếu cần
                // Nhưng chỉ đóng nếu không đang drag (để tránh đóng khi đang di chuyển node)
                if (_host.DraggedNode == null)
                {
                    CloseNodeDialogIfOpen();
                }
                return;
            }

            // Click vào canvas (không phải node) -> đóng dialog và bắt đầu pan
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Đóng dialog trước (không cần kiểm tra DraggedNode)
                CloseNodeDialogIfOpen();

                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    _host.BeginBoxSelection(e.GetPosition(_host.WorkflowCanvas));
                    return;
                }

                _host.CancelBoxSelection();
                
                // Chỉ bắt đầu pan nếu không đang drag
                if (_host.DraggedNode == null)
                {
                    // Bắt đầu pan canvas
                    _host.IsPanning = true;
                    _host.PanStartPoint = e.GetPosition(_host.ScrollViewer);
                    _host.WorkflowCanvas.CaptureMouse();
                    viewModel.SelectedNode = null;
                    _host.RestoreAllNodesZIndex();
                    
                    // Throttle NodeChrome handlers during pan (only if > 550 nodes)
                    if (viewModel.Nodes.Count > 550)
                    {
                        NodeChrome.SetZoomingState(true);
                    }
                }
            }
        }

        private void RemoveTempLineIfAny()
        {
            if (_host.TempLine != null && _host.WorkflowCanvas.Children.Contains(_host.TempLine))
            {
                _host.WorkflowCanvas.Children.Remove(_host.TempLine);
            }
            _host.TempLine = null;
        }

        /// <summary>
        /// Kiểm tra 2 node có nằm trong cùng một LoopBody cluster hay không.
        /// Cluster = toàn bộ nodes được kết nối (trực tiếp hoặc gián tiếp) với LoopBodyNode, bỏ qua LoopNode cha,
        /// cộng thêm các Break/Continue nằm hình học bên trong vùng body.
        /// Yêu cầu: body phải có ít nhất 1 node "thật" được nối (không tính Break/Continue).
        /// </summary>
        private static bool AreNodesInSameLoopBodyCluster(ViewModels.WorkflowEditorViewModel viewModel, WorkflowNode a, WorkflowNode b)
        {
            if (a == null || b == null) return false;

            foreach (var loop in viewModel.Nodes.OfType<LoopNode>())
            {
                var body = loop.LoopBodyNode;
                var visited = new HashSet<WorkflowNode> { body };
                var queue = new Queue<WorkflowNode>();
                queue.Enqueue(body);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();

                    var neighbors = viewModel.Connections
                        .Where(c => c.FromNode == current || c.ToNode == current)
                        .Select(c => c.FromNode == current ? c.ToNode : c.FromNode);

                    foreach (var neighbor in neighbors)
                    {
                        // Bỏ qua LoopNode cha để không lan ra ngoài qua default connection
                        if (ReferenceEquals(neighbor, loop)) continue;

                        if (visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // visited hiện chứa body + tất cả nodes đạt được từ body qua connections (kể cả Break/Continue nếu đã nối)
                visited.Remove(body);

                // Tập node "thật" bên trong cluster (không tính Break/Continue)
                var realNodes = visited.Where(n => n is not BreakNode && n is not ContinueNode).ToList();
                if (realNodes.Count == 0)
                {
                    // Chưa có node nào nối với body -> không cho Break/Continue kết nối trong loop này
                    continue;
                }

                // Thêm các Break/Continue đang nằm HÌNH HỌC trong vùng body (kể cả chưa có connection)
                var breakInside = viewModel.Nodes
                    .Where(n => n is BreakNode or ContinueNode)
                    .Where(n => IsNodeInsideLoopBodyRect(loop, n));

                var fullCluster = new HashSet<WorkflowNode>(visited);
                foreach (var n in breakInside) fullCluster.Add(n);

                if (fullCluster.Contains(a) && fullCluster.Contains(b))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Kiểm tra node có nằm trong hình chữ nhật LoopBody của một LoopNode hay không (dùng vị trí X/Y + Width/Height).
        /// </summary>
        private static bool IsNodeInsideLoopBodyRect(LoopNode loop, WorkflowNode node)
        {
            var body = loop.LoopBodyNode;
            var rect = new Rect(body.X, body.Y, body.Width, body.Height);

            // Dùng tâm tương đối của node (kích thước khoảng 150x80 mặc định)
            var probe = new Point(node.X + 75, node.Y + 40);
            return rect.Contains(probe);
        }

        private static PortPosition PredictPortDirection(Point fromPos, Point toPos, PortPosition? fromDirection)
        {
            double dx = toPos.X - fromPos.X;
            double dy = toPos.Y - fromPos.Y;

            if (fromDirection.HasValue)
            {
                switch (fromDirection.Value)
                {
                    case PortPosition.Right:
                        if (dx > 0) return PortPosition.Left;
                        if (Math.Abs(dy) > Math.Abs(dx)) return dy > 0 ? PortPosition.Top : PortPosition.Bottom;
                        return PortPosition.Right;

                    case PortPosition.Left:
                        if (dx < 0) return PortPosition.Right;
                        if (Math.Abs(dy) > Math.Abs(dx)) return dy > 0 ? PortPosition.Top : PortPosition.Bottom;
                        return PortPosition.Left;

                    case PortPosition.Bottom:
                        if (dy > 0) return PortPosition.Top;
                        if (Math.Abs(dx) > Math.Abs(dy)) return dx > 0 ? PortPosition.Left : PortPosition.Right;
                        return PortPosition.Bottom;

                    case PortPosition.Top:
                        if (dy < 0) return PortPosition.Bottom;
                        if (Math.Abs(dx) > Math.Abs(dy)) return dx > 0 ? PortPosition.Left : PortPosition.Right;
                        return PortPosition.Top;
                }
            }

            return Math.Abs(dx) > Math.Abs(dy)
                ? (dx > 0 ? PortPosition.Left : PortPosition.Right)
                : (dy > 0 ? PortPosition.Top : PortPosition.Bottom);
        }

        /// <summary>
        /// Rebuild outputs cho LoopNodes nếu connection liên quan đến LoopBody cluster.
        /// Khi một node mới (đặc biệt là ListOutNode) được kết nối vào LoopBody,
        /// cần rebuild outputs của parent LoopNode để sync các outputs mới.
        /// </summary>
        private static void RebuildLoopNodeOutputsIfNeeded(ViewModels.WorkflowEditorViewModel viewModel, WorkflowConnection connection)
        {
            if (viewModel == null || connection == null) return;

            var connectionsList = viewModel.Connections.ToList();
            var nodesList = viewModel.Nodes.ToList();

            // Kiểm tra xem connection có liên quan đến LoopBody cluster không
            foreach (var loopNode in nodesList.OfType<LoopNode>())
            {
                var body = loopNode.LoopBodyNode;
                if (body == null) continue;

                // Kiểm tra xem FromNode hoặc ToNode có nằm trong LoopBody cluster không
                var clusterNodes = GetLoopBodyClusterNodesStatic(loopNode, connectionsList);
                
                // Thêm LoopBodyNode vào cluster để check
                clusterNodes.Add(body);

                if (clusterNodes.Contains(connection.FromNode) || clusterNodes.Contains(connection.ToNode))
                {
                    // Connection liên quan đến LoopBody cluster → rebuild outputs
                    loopNode.RebuildOutputsFromLoopBody(connectionsList, nodesList);
                }
            }
        }

        /// <summary>
        /// Lấy toàn bộ nodes nằm trong LoopBody cluster (static version để dùng trong ConnectionHandler).
        /// </summary>
        private static HashSet<WorkflowNode> GetLoopBodyClusterNodesStatic(LoopNode loopNode, List<WorkflowConnection> connections)
        {
            var result = new HashSet<WorkflowNode>();
            var body = loopNode.LoopBodyNode;
            if (body == null) return result;

            var visited = new HashSet<WorkflowNode> { body };
            var queue = new Queue<WorkflowNode>();
            queue.Enqueue(body);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                var neighbors = connections
                    .Where(c => c.FromNode == current || c.ToNode == current)
                    .Select(c => c.FromNode == current ? c.ToNode : c.FromNode)
                    .Where(n => n != null);

                foreach (var neighbor in neighbors)
                {
                    // Bỏ qua LoopNode cha để không lan ra ngoài qua default connection
                    if (ReferenceEquals(neighbor, loopNode)) continue;

                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Loại bỏ chính LoopBodyNode, chỉ trả về các node "bên trong" body
            visited.Remove(body);
            result.UnionWith(visited);
            return result;
        }

        private static void ResetPortToDefault(FrameworkElement portUI)
        {
            // Lấy shape thực tế (có thể từ wrapper Border)
            var shape = PortRenderer.GetActualPortShape(portUI);
            if (shape != null)
            {
                shape.StrokeThickness = 2;
                if (shape is Ellipse)
                {
                    shape.Width = 18;
                    shape.Height = 18;
                }
                else if (shape is Rectangle rect)
                {
                    // Với port hình chữ nhật, ưu tiên reset về kích thước mặc định đã lưu trong Tag (Size)
                    if (rect.Tag is Size size && size.Width > 0 && size.Height > 0)
                    {
                        rect.Width = size.Width;
                        rect.Height = size.Height;
                    }
                    else
                    {
                        // Fallback cho các port chữ nhật cũ (không có Tag)
                        rect.Width = 10;
                        rect.Height = 18;
                    }
                }
            }
            
            // ✅ Nếu portUI là Border wrapper, đảm bảo nó không clip shape bên trong
            if (portUI is Border border && border.Child is FrameworkElement)
            {
                // Border không cần resize, chỉ cần đảm bảo không clip
                border.ClipToBounds = false;
            }
        }

        private static void HighlightPort(FrameworkElement portUI)
        {
            // Lấy shape thực tế (có thể từ wrapper Border)
            var shape = PortRenderer.GetActualPortShape(portUI);
            if (shape != null)
            {
                shape.StrokeThickness = 3;
                if (shape is Ellipse)
                {
                    shape.Width = 22;
                    shape.Height = 22;
                }
                else if (shape is Rectangle rect)
                {
                    // Phóng to dựa trên kích thước mặc định đã lưu trong Tag (Size)
                    double baseW = 10;
                    double baseH = 18;
                    if (rect.Tag is Size size && size.Width > 0 && size.Height > 0)
                    {
                        baseW = size.Width;
                        baseH = size.Height;
                    }

                    rect.Width = baseW + 2;
                    rect.Height = baseH + 2;
                }
            }
            
            // ✅ Nếu portUI là Border wrapper, đảm bảo nó không clip shape khi enlarge
            if (portUI is Border border && border.Child is FrameworkElement)
            {
                // Border không cần resize, chỉ cần đảm bảo không clip khi shape enlarge
                border.ClipToBounds = false;
            }
        }
    }
}

