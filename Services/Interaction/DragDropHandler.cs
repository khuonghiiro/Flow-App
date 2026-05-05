using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using FlowMy.Services.Interaction;
using System.Windows.Shapes;

namespace FlowMy.Services.Interaction
{
    public sealed class DragDropHandler
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly CollisionResolver _collisionResolver;
        
        // Throttling for expensive operations during drag
        private DispatcherTimer? _connectionUpdateTimer;
        private DispatcherTimer? _minimapUpdateTimer;
        private DispatcherTimer? _canvasSizeUpdateTimer;
        private HashSet<FlowMy.Models.WorkflowConnection>? _pendingConnectionsToUpdate;
        private List<FlowMy.Models.WorkflowConnection>? _draggedNodeConnections;
        private Dictionary<WorkflowNode, List<WorkflowNode>>? _dragAdjacency;
        private LoopBodyNode? _draggedNodeOwningLoopBody;
        private AsyncTaskBodyNode? _draggedNodeOwningAsyncBody;
        private List<WorkflowNode>? _draggedLoopBodyChildren;
        private List<WorkflowNode>? _draggedAsyncBodyChildren;
        private BodyContainerNode? _draggedOwningLockedBody;
        private List<WorkflowNode>? _draggedLockedBodyChildren;
        
        // Sử dụng CompositionTarget.Rendering để sync với refresh rate và tăng hiệu suất GPU
        private EventHandler? _renderingHandler;
        private bool _isRenderingHandlerActive;
        private DateTime _lastRenderingUpdate = DateTime.MinValue;
        private const int MinRenderingIntervalMs = 16; // ~60fps để cân bằng hiệu suất và mượt mà
        
        private const int ConnectionUpdateThrottleMs = 8; // Giảm từ 16ms xuống 8ms để mượt hơn
        private const int MinimapUpdateThrottleMs = 50;
        private const int CanvasSizeUpdateThrottleMs = 100;

        public DragDropHandler(IWorkflowEditorHostAccessor hostAccessor, CollisionResolver collisionResolver)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _collisionResolver = collisionResolver ?? throw new ArgumentNullException(nameof(collisionResolver));
        }

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public void NodeMouseDown(object sender, MouseButtonEventArgs e)
        {
            var host = Host;
            var viewModel = host.ViewModel;
            if (viewModel == null) return;

            // Resize handles are Ellipse; do not start node dragging from those grips.
            if (sender is Border bodyBorder &&
                bodyBorder.Tag is BodyContainerNode &&
                e.OriginalSource is Ellipse)
            {
                return;
            }
            if (HasResizeHandleTag(e.OriginalSource as DependencyObject))
            {
                return;
            }

            // ✅ Nếu click vào control tương tác (Button/ComboBox/TextBox/...) thì KHÔNG drag node
            if (IsInteractiveElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            // ✅ Nếu click vào port thì KHÔNG drag node (để cho phép kéo kết nối từ port)
            if (IsPortElement(e.OriginalSource as DependencyObject, viewModel))
            {
                return;
            }

            // ✅ Kiểm tra xem có dialog đang mở không - nếu có thì không drag
            if (host is Window window)
            {
                var field = window.GetType().GetField("_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager)
                {
                    if (manager.IsDialogOpen)
                    {
                        // Có dialog đang mở -> không drag node
                        return;
                    }
                }
            }

            host.IsPanning = false;
            if (host.WorkflowCanvas.IsMouseCaptured)
            {
                host.WorkflowCanvas.ReleaseMouseCapture();
            }

            if (host.ConnectingFromNode != null) return;

            var border = sender as Border;
            host.DraggedNode = viewModel.Nodes.FirstOrDefault(n => n.Border == border);

            if (host.DraggedNode == null) return;
            _draggedOwningLockedBody = null;
            _draggedLockedBodyChildren = null;

            if (host.DraggedNode is not BodyContainerNode)
            {
                var owningLockedBody = FindOwningLockedBody(viewModel, host.DraggedNode);
                if (owningLockedBody != null)
                {
                    _draggedOwningLockedBody = owningLockedBody;
                    _draggedLockedBodyChildren = CaptureNodesInsideBody(viewModel, owningLockedBody);
                    host.DraggedNode = owningLockedBody;
                }
            }

            host.ZIndexManager.SelectNode(host.DraggedNode);
            
            // Tối ưu GPU: Clear cache khi bắt đầu drag để tránh ghost effects
            if (GpuDetectionHelper.IsGpuAvailable && host.DraggedNode.Border != null)
            {
                host.DraggedNode.Border.CacheMode = null;
                RenderOptions.SetCachingHint(host.DraggedNode.Border, CachingHint.Unspecified);
            }

            foreach (var port in host.DraggedNode.Ports.Where(p => p.PortUI != null && p.IsVisible))
            {
                host.ZIndexManager.SetPortZIndex(host.DraggedNode, port.PortUI!);
            }

            if (host.DraggedNode.IsConditionalNode && host.DraggedNode.ConditionalBranches != null)
            {
                foreach (var branch in host.DraggedNode.ConditionalBranches)
                {
                    if (branch.Port?.PortUI != null && branch.Port.IsVisible)
                    {
                        host.ZIndexManager.SetPortZIndex(host.DraggedNode, branch.Port.PortUI);
                    }
                }
            }

            foreach (var otherNode in viewModel.Nodes)
            {
                if (otherNode != host.DraggedNode && otherNode.Border != null)
                {
                    host.ZIndexManager.RestoreNodeZIndex(otherNode);
                }

                // Nếu node khác là LoopNode, cần khôi phục z-index cho cả LoopBody của nó
                if (otherNode is LoopNode loopNode && loopNode.LoopBodyNode != host.DraggedNode)
                {
                    host.ZIndexManager.RestoreNodeZIndex(loopNode.LoopBodyNode);
                }
            }

            Point mousePos = e.GetPosition(host.WorkflowCanvas);
            host.DragOffset = new Point(mousePos.X - host.DraggedNode.X, mousePos.Y - host.DraggedNode.Y);

            host.DraggedNode.Border?.CaptureMouse();
            viewModel.SelectedNode = host.DraggedNode;
            host.FocusWindow();

            host.SyncAllPortsZIndex(host.DraggedNode);
            
            // Throttle expensive operations during drag (only if > 550 nodes)
            if (viewModel.Nodes.Count > 550)
            {
                NodeChrome.SetZoomingState(true);
                _pendingConnectionsToUpdate = new HashSet<FlowMy.Models.WorkflowConnection>();
            }

            // Cache connections liên quan đến node đang kéo để tránh quét toàn bộ danh sách mỗi mouse move.
            _draggedNodeConnections = viewModel.Connections
                .Where(conn => conn.FromNode == host.DraggedNode || conn.ToNode == host.DraggedNode)
                .ToList();

            if (host.DraggedNode is BodyContainerNode draggedBodyForConnections && draggedBodyForConnections.LockInnerNodes && _draggedLockedBodyChildren != null)
            {
                var lockedSet = new HashSet<WorkflowNode>(_draggedLockedBodyChildren) { draggedBodyForConnections };
                _draggedNodeConnections = viewModel.Connections
                    .Where(conn => lockedSet.Contains(conn.FromNode) || lockedSet.Contains(conn.ToNode))
                    .ToList();
            }

            // Cache adjacency + kết quả BFS cho phiên drag hiện tại để tránh tính lại mỗi frame.
            _dragAdjacency = BuildAdjacency(viewModel);
            _draggedNodeOwningLoopBody = host.DraggedNode != null
                ? FindOwningLoopBody(viewModel, host.DraggedNode, _dragAdjacency)
                : null;
            _draggedNodeOwningAsyncBody = host.DraggedNode != null
                ? FindOwningAsyncTaskBody(viewModel, host.DraggedNode, _dragAdjacency)
                : null;

            _draggedLoopBodyChildren = host.DraggedNode is LoopBodyNode loopBody
                ? GetAllNodesInLoopBodyComponent(viewModel, loopBody, _dragAdjacency)
                    .Where(n => n != loopBody && n is not LoopNode)
                    .ToList()
                : null;

            _draggedAsyncBodyChildren = host.DraggedNode is AsyncTaskBodyNode asyncBody
                ? GetAllNodesInAsyncTaskBodyComponent(viewModel, asyncBody, _dragAdjacency)
                    .Where(n => n != asyncBody && n is not LoopNode)
                    .ToList()
                : null;
        }

        private static bool IsNodeLockedByBodyContainer(FlowMy.ViewModels.WorkflowEditorViewModel viewModel, WorkflowNode node)
        {
            if (node is BodyContainerNode) return false;
            foreach (var body in viewModel.Nodes.OfType<BodyContainerNode>())
            {
                if (!body.LockInnerNodes || body.Border == null) continue;
                var width = body.BodyWidth > 0 ? body.BodyWidth : (body.Border.ActualWidth > 0 ? body.Border.ActualWidth : body.Border.Width);
                var height = body.BodyHeight > 0 ? body.BodyHeight : (body.Border.ActualHeight > 0 ? body.Border.ActualHeight : body.Border.Height);
                if (width <= 0 || height <= 0) continue;

                var centerX = node.X + ((node.Border?.ActualWidth ?? node.Border?.Width ?? 150) / 2.0);
                var centerY = node.Y + ((node.Border?.ActualHeight ?? node.Border?.Height ?? 80) / 2.0);
                var bounds = new Rect(body.X, body.Y, width, height);
                if (bounds.Contains(new Point(centerX, centerY)))
                    return true;
            }
            return false;
        }

        private static bool IsInteractiveElement(DependencyObject? source)
        {
            while (source != null)
            {
                // Những control này phải ưu tiên thao tác UI thay vì kéo node
                if (source is ButtonBase) return true;
                if (source is ComboBox) return true;
                if (source is TextBoxBase) return true;
                if (source is PasswordBox) return true;
                if (source is Selector) return true; // ListBox/ComboBox etc.
                if (source is Slider) return true;
                if (source is ScrollBar) return true;
                if (source is MenuItem) return true;

                // Đi lên Visual tree
                source = VisualTreeHelper.GetParent(source) ?? (source as FrameworkElement)?.Parent;
            }

            return false;
        }

        private static bool HasResizeHandleTag(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is FrameworkElement fe &&
                    fe.Tag != null &&
                    string.Equals(fe.Tag.GetType().Name, "ResizeDirection", StringComparison.Ordinal))
                {
                    return true;
                }
                source = VisualTreeHelper.GetParent(source) ?? (source as FrameworkElement)?.Parent;
            }

            return false;
        }

        /// <summary>Kiểm tra xem element có phải là port (Shape hoặc trong Border wrapper) không.</summary>
        private static bool IsPortElement(DependencyObject? source, ViewModels.WorkflowEditorViewModel? viewModel)
        {
            if (viewModel == null) return false;

            while (source != null)
            {
                // Kiểm tra nếu là Shape (Ellipse/Rectangle) có Tag là NodePort
                if (source is Shape shape && shape.Tag is Models.NodePort)
                {
                    return true;
                }

                // Kiểm tra nếu là Border wrapper có Tag là NodePort hoặc có child là Shape với Tag là NodePort
                if (source is Border border)
                {
                    if (border.Tag is Models.NodePort)
                        return true;
                    if (border.Child is Shape childShape && childShape.Tag is Models.NodePort)
                        return true;
                }

                // Kiểm tra trong tất cả nodes xem có port nào match không
                foreach (var node in viewModel.Nodes)
                {
                    foreach (var port in node.Ports.Where(p => p.IsVisible && p.PortUI != null))
                    {
                        // Port trực tiếp
                        if (ReferenceEquals(port.PortUI, source))
                            return true;
                        
                        // Port trong Border wrapper
                        if (port.PortUI is Border portBorder && portBorder.Child == source)
                            return true;
                        
                        // Shape trong Border wrapper
                        if (port.PortUI is Border portBorder2 && portBorder2.Child is Shape portShape && ReferenceEquals(portShape, source))
                            return true;
                    }
                }

                // Đi lên Visual tree
                source = VisualTreeHelper.GetParent(source) ?? (source as FrameworkElement)?.Parent;
            }

            return false;
        }

        public void NodeMouseMove(object sender, MouseEventArgs e)
        {
            var host = Host;
            var viewModel = host.ViewModel;
            if (viewModel == null) return;

            if (host.DraggedNode?.Border == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            host.ZIndexManager.DragNode(host.DraggedNode);

            Point mousePos = e.GetPosition(host.WorkflowCanvas);
            double newX = mousePos.X - host.DragOffset.X;
            double newY = mousePos.Y - host.DragOffset.Y;
            double moveDist = 0.0;
            if (host.DraggedNode != null)
            {
                double dxNode = newX - host.DraggedNode.X;
                double dyNode = newY - host.DraggedNode.Y;
                moveDist = Math.Sqrt(dxNode * dxNode + dyNode * dyNode);
            }

            // ✅ Logic 1: Ràng buộc (Constraint) - Nhốt node con trong LoopBody
            // (Ngoại trừ chính LoopNode cha thì không bị nhốt)
            var parentBody = _draggedNodeOwningLoopBody;
            var parentAsyncBody = _draggedNodeOwningAsyncBody;

            if ((parentBody != null || parentAsyncBody != null) && host.DraggedNode != null)
            {
                double nodeWidth = host.DraggedNode.Border.ActualWidth > 0 ? host.DraggedNode.Border.ActualWidth : 150;
                double nodeHeight = host.DraggedNode.Border.ActualHeight > 0 ? host.DraggedNode.Border.ActualHeight : 80;

                // Prefer LoopBody clamp if both exist (shouldn't happen in valid graphs).
                var clampLoopBody = parentBody;
                var clampAsyncBody = parentAsyncBody;

                // Don't clamp the loop/async header nodes themselves.
                if (host.DraggedNode is LoopNode || host.DraggedNode is AsyncTaskNode) { /* skip */ }
                else
                {
                    var clamp = (clampLoopBody != null)
                        ? (object)clampLoopBody
                        : (object)clampAsyncBody!;

                    // clamp.Width/Height differs by body type; use pattern matching.
                    if (clampLoopBody != null)
                    {
                        double minX = clampLoopBody.X;
                        double minY = clampLoopBody.Y;
                        double maxX = clampLoopBody.X + clampLoopBody.Width - nodeWidth;
                        double maxY = clampLoopBody.Y + clampLoopBody.Height - nodeHeight;

                        if (maxX < minX) maxX = minX;
                        if (maxY < minY) maxY = minY;

                        newX = Math.Max(minX, Math.Min(maxX, newX));
                        newY = Math.Max(minY, Math.Min(maxY, newY));
                    }
                    else if (clampAsyncBody != null)
                    {
                        double minX = clampAsyncBody.X;
                        double minY = clampAsyncBody.Y;
                        double maxX = clampAsyncBody.X + clampAsyncBody.Width - nodeWidth;
                        double maxY = clampAsyncBody.Y + clampAsyncBody.Height - nodeHeight;

                        if (maxX < minX) maxX = minX;
                        if (maxY < minY) maxY = minY;

                        newX = Math.Max(minX, Math.Min(maxX, newX));
                        newY = Math.Max(minY, Math.Min(maxY, newY));
                    }
                }
            }

            // ✅ Ràng buộc thêm: trong khung scope Start AutoScheduled (nét đứt xanh)
            if (host.DraggedNode != null)
                host.ClampNodeDragToAutoScheduledScope(host.DraggedNode, ref newX, ref newY);

            // ✅ Logic 2: Kéo LoopBody -> Kéo theo các node con (Connected Children)
            if (host.DraggedNode is LoopBodyNode bodyNode)
            {
                double dx = newX - bodyNode.X;
                double dy = newY - bodyNode.Y;

                // ✅ NEW: lấy cả chuỗi node nối tiếp trong body (transitive closure)
                var childNodes = _draggedLoopBodyChildren ?? new List<WorkflowNode>();

                foreach (var child in childNodes)
                {
                    // Snap child position: tránh BitmapCache render subpixel gây mờ sau drag.
                    double cx = Math.Round(child.X + dx);
                    double cy = Math.Round(child.Y + dy);
                    // Update renderer first so Conditional diamond can move satellite nodes by delta.
                    host.UpdateNodePosition(child, cx, cy);
                    viewModel.UpdateNodePosition(child, cx, cy);

                    if (child.Border != null)
                    {
                        Canvas.SetLeft(child.Border, cx);
                        Canvas.SetTop(child.Border, cy);
                    }

                    // Conditional diamond ports are custom-rendered (satellites + branch out ports).
                    if (child.IsConditionalNode && child.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                    {
                        host.RenderConditionalNodePorts(child);
                    }
                    else
                    {
                        foreach (var port in child.Ports.Where(p => p.IsVisible))
                        {
                            host.UpdatePortsPositionOnSide(child, port.Position);
                        }
                    }
                    
                    // ✅ Cập nhật viewport culling cho child node khi di chuyển
                    host.ViewportCullingService?.OnNodeChanged(child);
                }
            }

            // ✅ Logic 2b: Kéo AsyncTaskBody -> Kéo theo các node con trong cùng AsyncTaskBody cluster
            if (host.DraggedNode is AsyncTaskBodyNode asyncBodyNode)
            {
                double dx = newX - asyncBodyNode.X;
                double dy = newY - asyncBodyNode.Y;

                var childNodes = _draggedAsyncBodyChildren ?? new List<WorkflowNode>();

                foreach (var child in childNodes)
                {
                    // Snap child position: tránh BitmapCache render subpixel gây mờ sau drag.
                    double cx = Math.Round(child.X + dx);
                    double cy = Math.Round(child.Y + dy);
                    // Update renderer first so Conditional diamond can move satellite nodes by delta.
                    host.UpdateNodePosition(child, cx, cy);
                    viewModel.UpdateNodePosition(child, cx, cy);

                    if (child.Border != null)
                    {
                        Canvas.SetLeft(child.Border, cx);
                        Canvas.SetTop(child.Border, cy);
                    }

                    if (child.IsConditionalNode && child.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                    {
                        host.RenderConditionalNodePorts(child);
                    }
                    else
                    {
                        foreach (var port in child.Ports.Where(p => p.IsVisible))
                        {
                            host.UpdatePortsPositionOnSide(child, port.Position);
                        }
                    }

                    host.ViewportCullingService?.OnNodeChanged(child);
                }
            }

            if (host.DraggedNode != null)
            {
                if (host.DraggedNode is BodyContainerNode draggedBody && draggedBody.LockInnerNodes && _draggedLockedBodyChildren != null)
                {
                    var dxBody = newX - draggedBody.X;
                    var dyBody = newY - draggedBody.Y;
                    if (Math.Abs(dxBody) > 0.001 || Math.Abs(dyBody) > 0.001)
                    {
                        foreach (var child in _draggedLockedBodyChildren)
                        {
                            var cx = Math.Round(child.X + dxBody);
                            var cy = Math.Round(child.Y + dyBody);
                            host.UpdateNodePosition(child, cx, cy);
                            viewModel.UpdateNodePosition(child, cx, cy);

                            if (child.Border != null)
                            {
                                Canvas.SetLeft(child.Border, cx);
                                Canvas.SetTop(child.Border, cy);
                            }

                            if (child.IsConditionalNode && child.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                            {
                                host.RenderConditionalNodePorts(child);
                            }
                            else
                            {
                                foreach (var port in child.Ports.Where(p => p.IsVisible))
                                    host.UpdatePortsPositionOnSide(child, port.Position);
                            }

                            host.ViewportCullingService?.OnNodeChanged(child);
                        }
                    }
                }

                // Snap toạ độ về integer pixel. Lý do: BitmapCache render lại bitmap tại
                // offset subpixel sẽ bị bilinear-resample → hình ảnh node bị mờ sau khi
                // drag xong (khác với lúc mới kéo từ palette rơi ở vị trí nguyên).
                // Snap ngay tại drag-move để toạ độ model + Canvas.Left/Top luôn khớp,
                // và drag cảm nhận mượt ở cấp pixel (không dao động subpixel).
                newX = Math.Round(newX);
                newY = Math.Round(newY);

                viewModel.UpdateNodePosition(host.DraggedNode, newX, newY);

                // ✅ Update node position thông qua renderer để update title position
                host.UpdateNodePosition(host.DraggedNode, newX, newY);
                
                // Update canvas position
                if (host.DraggedNode.Border != null)
                {
                    Canvas.SetLeft(host.DraggedNode.Border, newX);
                    Canvas.SetTop(host.DraggedNode.Border, newY);
                }
            }
            
            // Tối ưu: Không invalidate mỗi frame khi drag - chỉ update position
            // WPF sẽ tự động render lại khi position thay đổi
            // Chỉ clear cache khi bắt đầu drag (đã xử lý trong NodeMouseDown)

            // Tối ưu: Throttle port updates - chỉ update mỗi vài frame để tăng hiệu suất
            // Ports sẽ được update trong connection update timer hoặc khi drag kết thúc
            // Chỉ update ports ngay lập tức nếu có ít nodes (< 20) để đảm bảo responsive
            if (viewModel.Nodes.Count < 20 && host.DraggedNode != null)
            {
                // Update ports positions (giữ behavior như code cũ)
                if (host.DraggedNode.IsConditionalNode)
                {
                    host.RenderConditionalNodePorts(host.DraggedNode);

                    var regularPorts = host.DraggedNode.Ports
                        .Where(p => p.IsVisible &&
                                   !host.DraggedNode.ConditionalBranches.Any(b => b.Port == p))
                        .Select(p => p.Position)
                        .Distinct();

                    foreach (var position in regularPorts)
                    {
                        host.UpdatePortsPositionOnSide(host.DraggedNode, position);
                    }
                }
                else if (!(host.DraggedNode is LoopNode))
                {
                    // ⚠️ Skip LoopNode - it has custom diamond shape port positioning in UpdateNodePosition
                    if (host.DraggedNode is AsyncTaskNode at && at.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
                    {
                        // AsyncTask diamond loop-like also needs custom port positioning; don't use UpdatePortsPositionOnSide.
                    }
                    else
                    {
                    var positions = host.DraggedNode.Ports
                        .Where(p => p.IsVisible)
                        .Select(p => p.Position)
                        .Distinct();

                    foreach (var position in positions)
                    {
                        host.UpdatePortsPositionOnSide(host.DraggedNode, position);
                    }
                    }
                }
                // LoopNode ports are updated in LoopNodeRenderer.UpdateNodePosition
            }

            if (host.DraggedNode != null && host.DraggedNode.Border != null)
            {
                host.ZIndexManager.RaiseNodeZIndex(host.DraggedNode, Panel.GetZIndex(host.DraggedNode.Border));
                host.SyncAllPortsZIndex(host.DraggedNode);
            }

            // Tối ưu: Throttle viewport culling - không check mỗi frame khi drag
            // Viewport culling sẽ được update trong timer hoặc khi drag kết thúc
            // Chỉ update ngay nếu có ít nodes
            if (viewModel.Nodes.Count < 20 && host.DraggedNode != null)
            {
                // ✅ Cập nhật viewport culling khi node di chuyển
                host.ViewportCullingService?.OnNodeChanged(host.DraggedNode);
                
                // Cập nhật viewport culling cho child nodes (nếu có)
                if (host.DraggedNode is LoopNode loopNode && loopNode.LoopBodyNode != null)
                {
                    host.ViewportCullingService?.OnNodeChanged(loopNode.LoopBodyNode);
                }
            }

            // Tối ưu: Update connections ngay lập tức để line không bị giật
            // Batch update để tối ưu hiệu suất GPU
            var relatedConnections = _draggedNodeConnections ?? new List<FlowMy.Models.WorkflowConnection>();
            
            // Batch update connections - GPU sẽ render hiệu quả hơn khi batch
            if (relatedConnections.Count > 0)
            {
                // Update tất cả connections
                foreach (var conn in relatedConnections)
                {
                    host.UpdateConnectionPath(conn);
                }
            }
            
            // Sử dụng CompositionTarget.Rendering để sync với refresh rate cho ports và viewport culling
            // Đảm bảo updates mượt mà và GPU được sử dụng tối đa
            // Chỉ update ports và viewport culling trong rendering handler, connections đã update ngay ở trên
            EnsureRenderingHandler();
            
            // Throttle minimap updates - chỉ update mỗi 50ms
            EnsureMinimapUpdateTimer();
            if (!_minimapUpdateTimer!.IsEnabled)
            {
                _minimapUpdateTimer.Start();
            }
            
            // Throttle canvas size updates - chỉ update mỗi 100ms
            EnsureCanvasSizeUpdateTimer();
            if (!_canvasSizeUpdateTimer!.IsEnabled)
            {
                _canvasSizeUpdateTimer.Start();
            }
        }
        
        /// <summary>
        /// Sử dụng CompositionTarget.Rendering để sync với refresh rate và tăng hiệu suất GPU
        /// Tốt hơn DispatcherTimer vì đồng bộ với refresh rate của màn hình
        /// </summary>
        private void EnsureRenderingHandler()
        {
            if (_renderingHandler == null)
            {
                _renderingHandler = (s, e) =>
                {
                    var host = Host;
                    var viewModel = host.ViewModel;
                    if (viewModel == null || _pendingConnectionsToUpdate == null || host.DraggedNode == null)
                    {
                        return;
                    }
                    
                    // Throttle để tránh quá tải - chỉ update mỗi 16ms (~60fps) để cân bằng hiệu suất
                    var now = DateTime.Now;
                    if ((now - _lastRenderingUpdate).TotalMilliseconds < MinRenderingIntervalMs)
                    {
                        return; // Skip frame này
                    }
                    _lastRenderingUpdate = now;
                    
                    // Update ports positions và viewport culling trong rendering handler
                    // Connections đã được update ngay lập tức trong NodeMouseMove để không bị giật
                    if (host.DraggedNode.IsConditionalNode)
                    {
                        host.RenderConditionalNodePorts(host.DraggedNode);
                        var regularPorts = host.DraggedNode.Ports
                            .Where(p => p.IsVisible &&
                                       !host.DraggedNode.ConditionalBranches.Any(b => b.Port == p))
                            .Select(p => p.Position)
                            .Distinct();
                        foreach (var position in regularPorts)
                        {
                            host.UpdatePortsPositionOnSide(host.DraggedNode, position);
                        }
                    }
                    else if (!(host.DraggedNode is LoopNode))
                    {
                        var positions = host.DraggedNode.Ports
                            .Where(p => p.IsVisible)
                            .Select(p => p.Position)
                            .Distinct();
                        foreach (var position in positions)
                        {
                            host.UpdatePortsPositionOnSide(host.DraggedNode, position);
                        }
                    }
                    
                    // Update viewport culling - chỉ khi có nhiều nodes để tối ưu
                    if (viewModel.Nodes.Count >= 20)
                    {
                        host.ViewportCullingService?.OnNodeChanged(host.DraggedNode);
                        if (host.DraggedNode is LoopNode loopNode && loopNode.LoopBodyNode != null)
                        {
                            host.ViewportCullingService?.OnNodeChanged(loopNode.LoopBodyNode);
                        }
                    }
                };
            }
            
            if (!_isRenderingHandlerActive)
            {
                System.Windows.Media.CompositionTarget.Rendering += _renderingHandler;
                _isRenderingHandlerActive = true;
                _lastRenderingUpdate = DateTime.Now;
            }
        }
        
        private void StopRenderingHandler()
        {
            if (_renderingHandler != null && _isRenderingHandlerActive)
            {
                System.Windows.Media.CompositionTarget.Rendering -= _renderingHandler;
                _isRenderingHandlerActive = false;
            }
        }
        
        private void EnsureConnectionUpdateTimer()
        {
            // Không còn dùng DispatcherTimer - đã chuyển sang CompositionTarget.Rendering
            // Giữ lại method này để tương thích với code cũ
        }
        
        private void EnsureMinimapUpdateTimer()
        {
            if (_minimapUpdateTimer != null) return;
            _minimapUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(MinimapUpdateThrottleMs) };
            _minimapUpdateTimer.Tick += (s, e) =>
            {
                _minimapUpdateTimer!.Stop();
                Host.UpdateMinimap();
            };
        }
        
        private void EnsureCanvasSizeUpdateTimer()
        {
            if (_canvasSizeUpdateTimer != null) return;
            _canvasSizeUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CanvasSizeUpdateThrottleMs) };
            _canvasSizeUpdateTimer.Tick += (s, e) =>
            {
                _canvasSizeUpdateTimer!.Stop();
                Host.UpdateCanvasSize();
            };
        }

        private static LoopBodyNode? FindOwningLoopBody(
            FlowMy.ViewModels.WorkflowEditorViewModel viewModel,
            WorkflowNode node,
            Dictionary<WorkflowNode, List<WorkflowNode>>? adjacency = null)
        {
            if (node is LoopNode) return null;
            if (node is LoopBodyNode body) return body;

            var neighbors = adjacency ?? BuildAdjacency(viewModel);

            foreach (var loop in viewModel.Nodes.OfType<LoopNode>())
            {
                var bodyNode = loop.LoopBodyNode;
                var component = Bfs(neighbors, start: bodyNode, canTraverse: n => n is not LoopNode);
                if (component.Contains(node))
                {
                    return bodyNode;
                }
            }

            return null;
        }

        private static HashSet<WorkflowNode> GetAllNodesInLoopBodyComponent(
            FlowMy.ViewModels.WorkflowEditorViewModel viewModel,
            LoopBodyNode bodyNode,
            Dictionary<WorkflowNode, List<WorkflowNode>>? adjacency = null)
        {
            var neighbors = adjacency ?? BuildAdjacency(viewModel);
            return Bfs(neighbors, start: bodyNode, canTraverse: n => n is not LoopNode);
        }

        private static AsyncTaskBodyNode? FindOwningAsyncTaskBody(
            FlowMy.ViewModels.WorkflowEditorViewModel viewModel,
            WorkflowNode node,
            Dictionary<WorkflowNode, List<WorkflowNode>>? adjacency = null)
        {
            if (node is AsyncTaskNode) return null; // header node should not be considered inside body
            if (node is AsyncTaskBodyNode body) return body;

            var neighbors = adjacency ?? BuildAdjacency(viewModel);

            foreach (var asyncTask in viewModel.Nodes.OfType<AsyncTaskNode>())
            {
                if (asyncTask.UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch) continue;
                var bodyNode = asyncTask.AsyncTaskBodyNode;
                if (bodyNode == null) continue;

                var component = Bfs(neighbors, start: bodyNode, canTraverse: n => n is not AsyncTaskNode);
                if (component.Contains(node))
                    return bodyNode;
            }

            return null;
        }

        private static HashSet<WorkflowNode> GetAllNodesInAsyncTaskBodyComponent(
            FlowMy.ViewModels.WorkflowEditorViewModel viewModel,
            AsyncTaskBodyNode bodyNode,
            Dictionary<WorkflowNode, List<WorkflowNode>>? adjacency = null)
        {
            var neighbors = adjacency ?? BuildAdjacency(viewModel);
            return Bfs(neighbors, start: bodyNode, canTraverse: n => n is not AsyncTaskNode);
        }

        private static Dictionary<WorkflowNode, List<WorkflowNode>> BuildAdjacency(FlowMy.ViewModels.WorkflowEditorViewModel viewModel)
        {
            var map = new Dictionary<WorkflowNode, List<WorkflowNode>>();

            void add(WorkflowNode a, WorkflowNode b)
            {
                if (!map.TryGetValue(a, out var list))
                {
                    list = new List<WorkflowNode>();
                    map[a] = list;
                }
                list.Add(b);
            }

            foreach (var c in viewModel.Connections)
            {
                // Undirected adjacency for membership in body
                add(c.FromNode, c.ToNode);
                add(c.ToNode, c.FromNode);
            }

            return map;
        }

        private static HashSet<WorkflowNode> Bfs(
            Dictionary<WorkflowNode, List<WorkflowNode>> neighbors,
            WorkflowNode start,
            Func<WorkflowNode, bool> canTraverse)
        {
            var visited = new HashSet<WorkflowNode>();
            var q = new Queue<WorkflowNode>();

            if (!canTraverse(start))
            {
                // vẫn cho phép start là LoopBodyNode
                visited.Add(start);
                q.Enqueue(start);
            }
            else
            {
                visited.Add(start);
                q.Enqueue(start);
            }

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (!neighbors.TryGetValue(cur, out var list)) continue;

                foreach (var nxt in list)
                {
                    if (!canTraverse(nxt)) continue;
                    if (visited.Add(nxt))
                    {
                        q.Enqueue(nxt);
                    }
                }
            }

            return visited;
        }

        public void NodeMouseUp(object sender, MouseButtonEventArgs e)
        {
            var host = Host;
            if (host.DraggedNode?.Border != null)
            {
                // ⚠️ Skip setting background for diamond-like nodes (custom shape)
                if (host.DraggedNode is LoopNode ||
                    (host.DraggedNode.IsConditionalNode && host.DraggedNode.ConditionalVisualMode == ConditionalVisualMode.Diamond))
                {
                    // keep loop background behavior
                }
                else if (host.DraggedNode is AsyncTaskNode at && at.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
                {
                    // keep async diamond background transparent; avoid square artifacts
                }
                else
                {
                    host.DraggedNode.Border.Background = host.DraggedNode.NodeBrush;
                }
                host.DraggedNode.Border.ReleaseMouseCapture();
                
                // ✅ Resolve collision khi nhả chuột - đẩy các node bị overlap
                var viewModel = host.ViewModel;
                if (viewModel != null && host.DraggedNode != null)
                {
                    if (host.DraggedNode is not BodyContainerNode bodyNode || !bodyNode.LockInnerNodes)
                    {
                        _collisionResolver.ResolveCollision(viewModel, host.DraggedNode, host);
                    }
                }
                
                // Stop rendering handler khi drag kết thúc
                StopRenderingHandler();
                
                // Re-enable NodeChrome handlers and flush pending updates after drag ends
                if (viewModel != null)
                {
                    NodeChrome.SetZoomingState(false);
                    
                    // Flush any pending connection updates
                    if (_pendingConnectionsToUpdate != null && _pendingConnectionsToUpdate.Count > 0)
                    {
                        var validConnections = _pendingConnectionsToUpdate
                            .Where(conn => viewModel.Connections.Contains(conn))
                            .ToList();
                        
                        foreach (var conn in validConnections)
                        {
                            host.UpdateConnectionPath(conn);
                        }
                        
                        _pendingConnectionsToUpdate.Clear();
                    }
                    
                    // Flush minimap and canvas size updates
                    host.UpdateMinimap();
                    host.UpdateCanvasSize();
                    
                    // Stop timers
                    _minimapUpdateTimer?.Stop();
                    _canvasSizeUpdateTimer?.Stop();
                }
                
                // Tối ưu GPU: Re-apply cache sau khi drag xong để tăng tốc render
                if (GpuDetectionHelper.IsGpuAvailable)
                {
                    // Re-apply cache cho node đã drag
                    if (host.DraggedNode?.Border != null)
                    {
                        GpuOptimizationHelper.ApplyToBorder(
                            host.DraggedNode.Border,
                            isDragging: false,
                            forceCache: host.CacheNodeEnabled);
                    }
                    
                    // Re-apply cache cho connections liên quan (nếu không có animation)
                    if (viewModel != null && !host.IsAnimationEnabled)
                    {
                        var relatedConnections = viewModel.Connections
                            .Where(conn => conn.FromNode == host.DraggedNode || conn.ToNode == host.DraggedNode)
                            .ToList();
                        
                        foreach (var conn in relatedConnections)
                        {
                            if (conn.LineUI != null)
                            {
                                GpuOptimizationHelper.ApplyToPath(conn.LineUI, allowCache: true);
                            }
                        }
                    }
                }
                
                host.DraggedNode = null;
                _draggedNodeConnections = null;
                _dragAdjacency = null;
                _draggedNodeOwningLoopBody = null;
                _draggedNodeOwningAsyncBody = null;
                _draggedLoopBodyChildren = null;
                _draggedAsyncBodyChildren = null;
                _draggedOwningLockedBody = null;
                _draggedLockedBodyChildren = null;
            }
        }

        private static BodyContainerNode? FindOwningLockedBody(FlowMy.ViewModels.WorkflowEditorViewModel viewModel, WorkflowNode node)
        {
            if (node is BodyContainerNode) return null;
            foreach (var body in viewModel.Nodes.OfType<BodyContainerNode>())
            {
                if (!body.LockInnerNodes) continue;
                var width = body.BodyWidth > 0 ? body.BodyWidth : (body.Border?.ActualWidth ?? body.Border?.Width ?? 0);
                var height = body.BodyHeight > 0 ? body.BodyHeight : (body.Border?.ActualHeight ?? body.Border?.Height ?? 0);
                if (width <= 0 || height <= 0) continue;

                var nodeW = node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150;
                var nodeH = node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80;
                var center = new Point(node.X + nodeW / 2.0, node.Y + nodeH / 2.0);
                if (new Rect(body.X, body.Y, width, height).Contains(center))
                    return body;
            }
            return null;
        }

        private static List<WorkflowNode> CaptureNodesInsideBody(FlowMy.ViewModels.WorkflowEditorViewModel viewModel, BodyContainerNode bodyNode)
        {
            var result = new List<WorkflowNode>();
            var bounds = new Rect(bodyNode.X, bodyNode.Y, bodyNode.BodyWidth, bodyNode.BodyHeight);
            foreach (var node in viewModel.Nodes)
            {
                if (ReferenceEquals(node, bodyNode)) continue;
                if (node is LoopBodyNode or AsyncTaskBodyNode) continue;
                var cx = node.X + (node.Border?.ActualWidth > 1 ? node.Border.ActualWidth / 2 : 75);
                var cy = node.Y + (node.Border?.ActualHeight > 1 ? node.Border.ActualHeight / 2 : 40);
                if (bounds.Contains(new Point(cx, cy)))
                    result.Add(node);
            }

            return result;
        }
    }
}

