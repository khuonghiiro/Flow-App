using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowMy.Services.Rendering;

namespace FlowMy.Services.Rendering
{
    public sealed class LoopNodeRenderer : INodeRenderer
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly PortRenderer _portRenderer;

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public LoopNodeRenderer(
            IWorkflowEditorHostAccessor hostAccessor,
            PortRenderer portRenderer)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not LoopNode loopNode)
                throw new InvalidOperationException("LoopNodeRenderer can only render LoopNode.");

            // 1) Render Loop Node chính
            loopNode.Border = LoopNodeControl.CreateBorder(
                loopNode,
                Host as System.Windows.Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host
            );
            NodeChrome.Apply(loopNode.Border, loopNode, Host);

            loopNode.Border.MouseDown += Host.NodeMouseDown;
            loopNode.Border.MouseMove += Host.NodeMouseMove;
            loopNode.Border.MouseUp += Host.NodeMouseUp;
            loopNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            loopNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            loopNode.Border.ContextMenu = Host.CreateNodeContextMenu(loopNode);

            Canvas.SetLeft(loopNode.Border, loopNode.X);
            Canvas.SetTop(loopNode.Border, loopNode.Y);
            canvas.Children.Add(loopNode.Border);
            Host.ZIndexManager.InitializeNodeZIndex(loopNode, loopNode.Border);

            // ✅ Cập nhật vị trí port khi size của node thay đổi (ví dụ khi gõ text dài ra)
            // ⚠️ CRITICAL: Don't use UpdatePortsPositionOnSide for LoopNode - it uses rectangular positioning
            // LoopNode uses custom diamond shape positioning in RenderLoopNodePorts
            loopNode.Border.SizeChanged += (s, e) =>
            {
                // Use custom RenderLoopNodePorts instead of UpdatePortsPositionOnSide
                RenderLoopNodePorts(loopNode, canvas);

                // ✅ Cập nhật lại đường nối ngay lập tức khi size node thay đổi (thời gian thực)
                if (Host.ViewModel != null)
                {
                    var related = Host.ViewModel.Connections.Where(c => c.FromNode == loopNode || c.ToNode == loopNode).ToList();
                    foreach (var conn in related) Host.UpdateConnectionPath(conn);
                }
            };

            // ✅ Khi LoopType thay đổi (ví dụ chuyển sang While), cần cập nhật hiển thị Ports
            loopNode.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LoopNode.LoopType))
                {
                    RenderLoopNodePorts(loopNode, canvas);
                    // Cập nhật lại đường nối vì port có thể bị ẩn/hiện
                    if (Host.ViewModel != null)
                    {
                        foreach (var conn in Host.ViewModel.Connections.Where(c => c.FromNode == loopNode || c.ToNode == loopNode))
                        {
                            Host.UpdateConnectionPath(conn);
                        }
                    }

                    // DataPanel embedded trong node đã bị loại bỏ, chỉ còn dialog để cấu hình dynamic inputs,
                    // nên không cần rebuild DataPanel ở đây nữa.
                }
            };

            // 2) Render Loop Body Container
            loopNode.ContainerBorder = LoopContainerControl.CreateContainer(loopNode);
            AttachContainerHandlers(loopNode);

            // Init position if new - sync with parent position
            if (loopNode.LoopBodyNode.X == 0 && loopNode.LoopBodyNode.Y == 0)
            {
                loopNode.LoopBodyNode.SyncPositionWithParent();
            }

            Canvas.SetLeft(loopNode.ContainerBorder, loopNode.LoopBodyNode.X);
            Canvas.SetTop(loopNode.ContainerBorder, loopNode.LoopBodyNode.Y);
            Host.ZIndexManager.InitializeNodeZIndex(loopNode.LoopBodyNode, loopNode.ContainerBorder);
            canvas.Children.Add(loopNode.ContainerBorder);

            // ✅ Gán Border cho LoopBodyNode
            loopNode.LoopBodyNode.Border = loopNode.ContainerBorder;

            // 3) Đợi layout xong rồi mới render ports
            loopNode.Border.Loaded += (s, e) =>
            {
                RenderLoopNodePorts(loopNode, canvas);
                RenderLoopBodyPorts(loopNode, canvas);
                CreateDefaultConnection(loopNode);

                // After ports are positioned, update any existing connections that touch this loop or its body (import case).
                if (Host.ViewModel != null)
                {
                    var related = Host.ViewModel.Connections
                        .Where(c => c.FromNode == loopNode
                                    || c.ToNode == loopNode
                                    || c.FromNode == loopNode.LoopBodyNode
                                    || c.ToNode == loopNode.LoopBodyNode)
                        .ToList();

                    foreach (var conn in related)
                    {
                        Host.UpdateConnectionPath(conn);
                    }

                    // ✅ Rebuild outputs từ ListOutNodes trong LoopBody (import case)
                    // Đảm bảo LoopNode có các outputs từ ListOutNodes đã được cấu hình
                    loopNode.RebuildOutputsFromLoopBody(
                        Host.ViewModel.Connections.ToList(),
                        Host.ViewModel.Nodes);
                }
            };
        }

        private void RenderLoopNodePorts(LoopNode loopNode, Canvas canvas)
        {
            if (loopNode.Border == null) return;

            if (loopNode.Border.ActualWidth == 0 || loopNode.Border.ActualHeight == 0)
            {
                loopNode.Border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                loopNode.Border.Arrange(new Rect(loopNode.Border.DesiredSize));
            }

            // ✅ Diamond shape: ports at corners
            // Left corner (input), Right corner (output), Bottom (output to LoopContainerControl)
            var nodeX = loopNode.X;
            var nodeY = loopNode.Y;
            // ✅ Use fixed diamond size (100x100) instead of ActualWidth/ActualHeight to prevent port jumping
            const double diamondWidth = 100;
            const double diamondHeight = 100;
            const double portRadius = 9.0; // Half of port size (18/2)

            // ✅ Xử lý các port hiển thị: Tạo UI và đưa lên canvas
            // ✅ Ẩn port LoopIndexOut - chỉ giữ LoopNodeOut ở góc phải
            var indexPort = loopNode.Ports.FirstOrDefault(p => p.Id == "LoopIndexOut");
            if (indexPort != null)
            {
                indexPort.IsVisible = false;
            }

            var visiblePorts = loopNode.Ports.Where(p => p.IsVisible).ToList();

            foreach (var port in visiblePorts)
            {
                // ✅ Determine port color (prioritize ColorKey, fallback to IsInput logic)
                Color? portSpecificColor = null;
                if (!string.IsNullOrEmpty(port.ColorKey))
                {
                    var brush = Application.Current.TryFindResource(port.ColorKey) as SolidColorBrush
                                ?? Application.Current.TryFindResource(port.ColorKey + "Brush") as SolidColorBrush;
                    if (brush != null) portSpecificColor = brush.Color;
                }

                var nodeColor = (loopNode.NodeBrush as SolidColorBrush)?.Color;
                var finalColor = portSpecificColor ?? nodeColor ?? (port.IsInput ? Colors.Orange : Colors.Cyan);

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(finalColor);
                    port.PortUI.Tag = port;
                }
                else
                {
                    // ⚠️ CRITICAL: LUÔN update màu để đảm bảo đúng màu (kể cả khi port đã tồn tại)
                    if (port.PortUI is Ellipse ellipse)
                    {
                        ellipse.Fill = new SolidColorBrush(finalColor);
                    }
                }

                // ✅ Position ports at diamond corners
                Point portPosition;
                switch (port.Position)
                {
                    case PortPosition.Left:
                        // Left corner: (0, height/2)
                        portPosition = new Point(nodeX, nodeY + diamondHeight / 2);
                        break;
                    case PortPosition.Right:
                        // Right corner: (width, height/2)
                        portPosition = new Point(nodeX + diamondWidth, nodeY + diamondHeight / 2);
                        break;
                    case PortPosition.Bottom:
                        // Bottom: (width/2, height)
                        portPosition = new Point(nodeX + diamondWidth / 2, nodeY + diamondHeight);
                        break;
                    case PortPosition.Top:
                        // Top: (width/2, 0) - for condition port if needed
                        portPosition = new Point(nodeX + diamondWidth / 2, nodeY);
                        break;
                    default:
                        portPosition = new Point(nodeX, nodeY);
                        break;
                }

                port.PositionPoint = portPosition;

                if (port.PortUI != null)
                {
                    Canvas.SetLeft(port.PortUI, portPosition.X - portRadius);
                    Canvas.SetTop(port.PortUI, portPosition.Y - portRadius);
                }

                _portRenderer.EnsurePortAddedToCanvas(port);
                if (port.PortUI != null)
                {
                    Host.ZIndexManager.SetPortZIndex(loopNode, port.PortUI);
                }
            }

            // ✅ Xử lý các port bị ẩn: Xóa khỏi canvas
            var hiddenPorts = loopNode.Ports.Where(p => !p.IsVisible).ToList();
            foreach (var port in hiddenPorts)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Remove(port.PortUI);
                }
            }
        }

        private void RenderLoopBodyPorts(LoopNode loopNode, Canvas canvas)
        {
            var bodyNode = loopNode.LoopBodyNode;
            if (loopNode.ContainerBorder == null) return;

            if (loopNode.ContainerBorder.ActualWidth == 0 || loopNode.ContainerBorder.ActualHeight == 0)
            {
                loopNode.ContainerBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                loopNode.ContainerBorder.Arrange(new Rect(loopNode.ContainerBorder.DesiredSize));
            }

            // ✅ Tạo/lấy ports cho Loop Body Node (KHÔNG phải Loop Node)
            var bodyTopPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyTop");
            if (bodyTopPort == null)
            {
                bodyTopPort = new NodePort
                {
                    Id = "LoopBodyTop",
                    IsInput = true,
                    Position = PortPosition.Top,
                    IsVisible = true,
                    CanDeleteConnection = false // ✅ Không cho xóa connection này
                };
                bodyNode.Ports.Add(bodyTopPort);
            }

            var bodyLeftPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyLeft");
            if (bodyLeftPort == null)
            {
                bodyLeftPort = new NodePort
                {
                    Id = "LoopBodyLeft",
                    IsInput = false,
                    Position = PortPosition.Right, // ✅ Changed to Right (Inward facing)
                    IsVisible = true
                };
                bodyNode.Ports.Add(bodyLeftPort);
            }
            bodyLeftPort.Position = PortPosition.Right;

            var bodyRightPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyRight");
            if (bodyRightPort == null)
            {
                bodyRightPort = new NodePort
                {
                    Id = "LoopBodyRight",
                    IsInput = true,
                    Position = PortPosition.Left, // ✅ Changed to Left (Inward facing)
                    IsVisible = true
                };
                bodyNode.Ports.Add(bodyRightPort);
            }
            bodyRightPort.Position = PortPosition.Left;

            // ✅ Tạo UI cho ports với logic ưu tiên màu
            foreach (var port in new[] { bodyTopPort, bodyLeftPort, bodyRightPort })
            {
                if (port.PortUI == null)
                {
                    // ✅ Ưu tiên 1: ColorKey của Port
                    Color? portSpecificColor = null;
                    if (!string.IsNullOrEmpty(port.ColorKey))
                    {
                        var brush = Application.Current.TryFindResource(port.ColorKey) as SolidColorBrush
                                    ?? Application.Current.TryFindResource(port.ColorKey + "Brush") as SolidColorBrush;
                        if (brush != null) portSpecificColor = brush.Color;
                    }

                    // ✅ Ưu tiên 2: Màu của Node (nếu có - dùng chung màu với Loop Node cha cho đồng bộ)
                    var nodeColor = (loopNode.NodeBrush as SolidColorBrush)?.Color;

                    // ✅ Ưu tiên 3: Mặc định (Orange/Cyan)
                    var finalColor = portSpecificColor ?? nodeColor ?? (port.IsInput ? Colors.Orange : Colors.Cyan);

                    port.PortUI = _portRenderer.CreatePort(finalColor);
                }
            }

            // ✅ Cập nhật vị trí ports dựa trên BODY NODE
            ApplyBodyPortScaleBySize(bodyNode);
            UpdateLoopBodyPortsPosition(loopNode);

            // ✅ Thêm ports vào canvas
            foreach (var port in new[] { bodyTopPort, bodyLeftPort, bodyRightPort })
            {
                if (double.IsNaN(port.PositionPoint.X) || double.IsNaN(port.PositionPoint.Y))
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Port {port.Id} has invalid position");
                    continue;
                }

                if (!canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Add(port.PortUI);
                    Canvas.SetZIndex(port.PortUI, 1000);
                }
            }
        }

        /// <summary>Gọi sau khi di chuyển LoopBodyNode bằng đường khác LoopNodeRenderer (vd. kéo khung scope auto).</summary>
        public void UpdateLoopBodyPortsPosition(LoopNode loopNode)
        {
            var bodyNode = loopNode.LoopBodyNode;

            // ✅ Tính toán vị trí container (Sử dụng Absolute Position)
            var containerX = bodyNode.X;
            var containerY = bodyNode.Y;
            var containerWidth = bodyNode.Width;
            var containerHeight = bodyNode.Height;

            var bodyTopPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyTop");
            var bodyLeftPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyLeft");
            var bodyRightPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyRight");

            // ✅ TOP PORT - giữa cạnh trên
            if (bodyTopPort != null)
            {
                bodyTopPort.PositionPoint = new Point(
                    containerX + containerWidth / 2,
                    containerY
                );

                if (bodyTopPort.PortUI != null)
                {
                    Canvas.SetLeft(bodyTopPort.PortUI, bodyTopPort.PositionPoint.X - 9);
                    Canvas.SetTop(bodyTopPort.PortUI, bodyTopPort.PositionPoint.Y - 9);
                }
            }

            // ✅ LEFT PORT - giữa cạnh trái
            if (bodyLeftPort != null)
            {
                bodyLeftPort.PositionPoint = new Point(
                    containerX,
                    containerY + containerHeight / 2
                );

                if (bodyLeftPort.PortUI != null)
                {
                    Canvas.SetLeft(bodyLeftPort.PortUI, bodyLeftPort.PositionPoint.X - 9);
                    Canvas.SetTop(bodyLeftPort.PortUI, bodyLeftPort.PositionPoint.Y - 9);
                }
            }

            // ✅ RIGHT PORT - giữa cạnh phải
            if (bodyRightPort != null)
            {
                bodyRightPort.PositionPoint = new Point(
                    containerX + containerWidth,
                    containerY + containerHeight / 2
                );

                if (bodyRightPort.PortUI != null)
                {
                    Canvas.SetLeft(bodyRightPort.PortUI, bodyRightPort.PositionPoint.X - 9);
                    Canvas.SetTop(bodyRightPort.PortUI, bodyRightPort.PositionPoint.Y - 9);
                }
            }
        }

        private static void ApplyBodyPortScaleBySize(LoopBodyNode bodyNode)
        {
            var visualScale = LoopContainerControl.ComputeBodyInteractionScale(bodyNode.Width, bodyNode.Height);

            foreach (var port in bodyNode.Ports.Where(p => p.Id is "LoopBodyTop" or "LoopBodyLeft" or "LoopBodyRight"))
            {
                if (port.PortUI == null) continue;
                port.PortUI.RenderTransformOrigin = new Point(0.5, 0.5);
                port.PortUI.RenderTransform = new ScaleTransform(visualScale, visualScale);
            }
        }

        private void CreateDefaultConnection(LoopNode loopNode)
        {
            var bottomPort = loopNode.Ports.FirstOrDefault(p => p.Id == "LoopNodeBottom");
            var topPort = loopNode.LoopBodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyTop");

            if (bottomPort == null || topPort == null) return;

            // ✅ Nếu đã có DefaultConnection (ví dụ vừa render lại UI) thì chỉ cần đảm bảo không có nút x
            if (loopNode.DefaultConnection != null)
            {
                loopNode.DefaultConnection.IsDeleteVisible = false;
                // Cập nhật lại UI để ẩn nút x nếu đang hiển thị
                Host.UpdateConnectionPath(loopNode.DefaultConnection);
                return;
            }

            // ✅ IMPORT CASE: Tìm các connection đã được khôi phục từ JSON giữa LoopNode và LoopBody
            if (Host.ViewModel != null)
            {
                var vm = Host.ViewModel;

                // Tất cả connection từ LoopNode → LoopBody (bất kể port nào)
                var related = vm.Connections
                    .Where(c => c.FromNode == loopNode && c.ToNode == loopNode.LoopBodyNode)
                    .ToList();

                if (related.Count > 0)
                {
                    // Ưu tiên connection đúng cặp port (LoopNodeBottom → LoopBodyTop)
                    var existing = related
                        .FirstOrDefault(c => c.FromPort == bottomPort && c.ToPort == topPort)
                                   ?? related[0];

                    // Dọn dẹp các connection trùng còn lại (giữ đúng 1 line duy nhất)
                    foreach (var extra in related.Where(c => c != existing).ToList())
                    {
                        Host.ConnectionRenderer.RemoveConnectionVisuals(extra);
                        vm.Connections.Remove(extra);
                    }

                    // Đảm bảo port gắn đúng (trong trường hợp file cũ chưa có PortId)
                    existing.FromPort = bottomPort;
                    existing.ToPort = topPort;

                    // ✅ Không cho xóa connection mặc định này => luôn ẩn nút x
                    existing.IsDeleteVisible = false;

                    loopNode.DefaultConnection = existing;

                    // Cập nhật lại path + vị trí nút x (để ẩn ngay lập tức)
                    Host.UpdateConnectionPath(existing);
                    return;
                }
            }

            // ✅ Connection từ Loop Node → Loop Body Node
            var connection = new WorkflowConnection
            {
                FromNode = loopNode,
                ToNode = loopNode.LoopBodyNode,  // ✅ Kết nối đến Body Node
                FromPort = bottomPort,
                ToPort = topPort,
                // ✅ Connection mặc định giữa Loop và Body không bao giờ có nút x
                IsDeleteVisible = false
            };

            loopNode.DefaultConnection = connection;

            if (Host.ViewModel != null && !Host.ViewModel.Connections.Contains(connection))
            {
                Host.ViewModel.Connections.Add(connection);
            }
        }

        private void AttachContainerHandlers(LoopNode loopNode)
        {
            if (loopNode.ContainerBorder == null) return;

            var containerBorder = loopNode.ContainerBorder;
            bool isDraggingContainer = false;
            Point containerDragStart = new Point();
            Point containerOriginalPos = new Point();
            List<WorkflowNode>? bodyClusterNodes = null;

            // ✅ Sử dụng PreviewMouseDown để ưu tiên cao hơn
            containerBorder.PreviewMouseDown += (s, e) =>
            {
                // ✅ Allow dragging on background or headers (excluding resize handles)
                if (e.OriginalSource is Ellipse) return; // Ignore resize handles
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    CloseNodeDialogIfOpen();
                }

                // ✅ Quản lý Z-index: Nổi lên khi click vào Loop Body
                Host.ZIndexManager.SelectNode(loopNode.LoopBodyNode);

                // Khôi phục z-index cho các node khác
                if (Host.ViewModel != null)
                {
                    foreach (var node in Host.ViewModel.Nodes)
                    {
                        if (node != loopNode.LoopBodyNode)
                        {
                            Host.ZIndexManager.RestoreNodeZIndex(node);
                        }
                    }
                }

                isDraggingContainer = true;
                containerDragStart = e.GetPosition(Host.WorkflowCanvas);
                containerOriginalPos = new Point(loopNode.LoopBodyNode.X, loopNode.LoopBodyNode.Y);

                // ✅ Tính trước toàn bộ "cluster" nodes nằm trong LoopBody (kết nối trực tiếp hoặc gián tiếp)
                bodyClusterNodes = GetLoopBodyClusterNodes(loopNode);

                containerBorder.CaptureMouse();
                e.Handled = true;
            };

            containerBorder.PreviewMouseMove += (s, e) =>
            {
                if (isDraggingContainer && e.LeftButton == MouseButtonState.Pressed)
                {
                    // ✅ Quản lý Z-index: Giữ nổi khi đang kéo
                    Host.ZIndexManager.DragNode(loopNode.LoopBodyNode);

                    var currentPos = e.GetPosition(Host.WorkflowCanvas);
                    var deltaX = currentPos.X - containerDragStart.X;
                    var deltaY = currentPos.Y - containerDragStart.Y;

                    // ✅ Capture old position
                    double oldX = loopNode.LoopBodyNode.X;
                    double oldY = loopNode.LoopBodyNode.Y;

                    loopNode.LoopBodyNode.X = containerOriginalPos.X + deltaX;
                    loopNode.LoopBodyNode.Y = containerOriginalPos.Y + deltaY;

                    Canvas.SetLeft(containerBorder, loopNode.LoopBodyNode.X);
                    Canvas.SetTop(containerBorder, loopNode.LoopBodyNode.Y);

                    // ✅ Calculate step delta for children
                    double stepX = loopNode.LoopBodyNode.X - oldX;
                    double stepY = loopNode.LoopBodyNode.Y - oldY;

                    // ✅ Cập nhật vị trí ports của Body
                    UpdateLoopBodyPortsPosition(loopNode);

                    // ✅ Cập nhật Minimap khi di chuyển Loop Body
                    Host.UpdateMinimap();

                    // ✅ Cập nhật tất cả connections liên quan đến Loop Body
                    if (Host.ViewModel != null)
                    {
                        var vm = Host.ViewModel;
                        // ✅ Cập nhật tất cả connections liên quan đến LoopBodyNode
                        var bodyConnections = vm.Connections
                            .Where(c => c.FromNode == loopNode.LoopBodyNode || c.ToNode == loopNode.LoopBodyNode)
                            .ToList();

                        foreach (var conn in bodyConnections)
                        {
                            Host.UpdateConnectionPath(conn);
                        }

                        // ✅ NEW: Move toàn bộ cluster nodes nằm trong LoopBody (kể cả qua nhiều bước A→B→C)
                        if (bodyClusterNodes != null)
                        {
                            foreach (var child in bodyClusterNodes)
                            {
                                // Bỏ qua chính LoopBodyNode (đã xử lý ở trên)
                                if (ReferenceEquals(child, loopNode.LoopBodyNode)) continue;

                                double cx = child.X + stepX;
                                double cy = child.Y + stepY;

                                // Update logic
                                // Update renderer first so specialized renderers (e.g. Conditional diamond)
                                // can compute movement delta from previous X/Y and move satellite visuals.
                                Host.UpdateNodePosition(child, cx, cy);
                                vm.UpdateNodePosition(child, cx, cy);

                                // Update visual (border đã được set trong UpdateNodePosition, giữ lại cho chắc)
                                if (child.Border != null)
                                {
                                    Canvas.SetLeft(child.Border, cx);
                                    Canvas.SetTop(child.Border, cy);
                                }

                                // Conditional diamond has custom satellite/branch ports.
                                if (child.IsConditionalNode && child.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                                {
                                    Host.RenderConditionalNodePorts(child);
                                }
                                else
                                {
                                    foreach (var port in child.Ports.Where(p => p.IsVisible))
                                    {
                                        _portRenderer.UpdatePortsPositionOnSide(child, port.Position);
                                    }
                                }

                                // Update connections cho từng child
                                var childConns = vm.Connections
                                    .Where(c => c.FromNode == child || c.ToNode == child)
                                    .ToList();
                                foreach (var cc in childConns) Host.UpdateConnectionPath(cc);
                            }
                        }
                    }

                    e.Handled = true;
                }
            };

            containerBorder.PreviewMouseUp += (s, e) =>
            {
                if (isDraggingContainer)
                {
                    isDraggingContainer = false;
                    containerBorder.ReleaseMouseCapture();
                    bodyClusterNodes = null; // clear cached cluster
                    
                    // ✅ Khôi phục Z-index ban đầu của Loop Body (-100)
                    Host.ZIndexManager.RestoreNodeZIndex(loopNode.LoopBodyNode);

                    // ✅ Cập nhật minimap lần cuối khi thả chuột
                    Host.UpdateMinimap();
                    e.Handled = true;
                }
            };

            // ✅ Listen to property changes of Body Node width/height
            loopNode.LoopBodyNode.PropertyChanged += (s, e) =>
            {
                 if (e.PropertyName == nameof(LoopBodyNode.Width) ||
                     e.PropertyName == nameof(LoopBodyNode.Height))
                 {
                    ApplyBodyPortScaleBySize(loopNode.LoopBodyNode);
                    UpdateLoopBodyPortsPosition(loopNode);
                    Host.UpdateMinimap(); // ✅ Cập nhật minimap khi resize
                    
                    // ✅ Cập nhật viewport culling khi LoopBodyNode resize
                    Host.ViewportCullingService?.OnNodeChanged(loopNode.LoopBodyNode);
                    
                     if (Host.ViewModel != null)
                     {
                         var relatedConnections = Host.ViewModel.Connections
                             .Where(c => c.FromNode == loopNode.LoopBodyNode || c.ToNode == loopNode.LoopBodyNode)
                             .ToList();

                         foreach (var conn in relatedConnections)
                         {
                             Host.UpdateConnectionPath(conn);
                         }
                     }
                 }
            };
        }

        private void CloseNodeDialogIfOpen()
        {
            if (Host is not Window window) return;
            var field = window.GetType().GetField(
                "_nodeDialogManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(window) is NodeDialogManager manager)
            {
                manager.CloseCurrentDialog();
            }
        }

        /// <summary>
        /// Lấy toàn bộ nodes nằm trong LoopBody cluster: tất cả nodes được kết nối
        /// (trực tiếp hoặc gián tiếp) với LoopBodyNode, bỏ qua LoopNode cha.
        /// </summary>
        private List<WorkflowNode> GetLoopBodyClusterNodes(LoopNode loopNode)
        {
            var result = new List<WorkflowNode>();
            var vm = Host.ViewModel;
            if (vm == null) return result;

            var body = loopNode.LoopBodyNode;
            var visited = new HashSet<WorkflowNode> { body };
            var queue = new Queue<WorkflowNode>();
            queue.Enqueue(body);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                var neighbors = vm.Connections
                    .Where(c => c.FromNode == current || c.ToNode == current)
                    .Select(c => c.FromNode == current ? c.ToNode : c.FromNode);

                foreach (var neighbor in neighbors)
                {
                    // Bỏ qua LoopNode cha để không kéo cả node cha / graph bên ngoài qua default connection
                    if (ReferenceEquals(neighbor, loopNode)) continue;

                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Loại bỏ chính LoopBodyNode, chỉ trả về các node "bên trong" body
            visited.Remove(body);
            foreach (var node in visited)
            {
                if (!result.Contains(node))
                    result.Add(node);
            }

            return result;
        }

        public void UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            node.X = x;
            node.Y = y;

            if (node.Border != null)
            {
                Canvas.SetLeft(node.Border, x);
                Canvas.SetTop(node.Border, y);
            }

            // ✅ Luôn đồng bộ title (TitleTextBlockUI) theo (x,y) khi cập nhật vị trí Loop Node
            // ⚠️ CRITICAL: Chỉ update nếu TitleTextBlockUI vẫn còn reference hợp lệ
            if (node is LoopNode loopNode && loopNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var titleTextBlock = loopNode.TitleTextBlockUI;

                // ⚠️ CRITICAL: Kiểm tra xem titleTextBlock có còn trong canvas không
                // Nếu không có, chỉ add lại nếu node vẫn còn trong ViewModel (tránh add lại titleTextBlock của node đã bị xóa)
                if (!Host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    // Chỉ add lại nếu node vẫn còn trong ViewModel (tránh add lại titleTextBlock của node đã bị xóa)
                    if (Host.ViewModel != null && Host.ViewModel.Nodes.Contains(loopNode))
                    {
                        Host.WorkflowCanvas.Children.Add(titleTextBlock);
                        Panel.SetZIndex(titleTextBlock, 20000);
                    }
                    else
                    {
                        // Node đã bị xóa, clear reference để tránh add lại
                        loopNode.TitleTextBlockUI = null;
                        return;
                    }
                }

                if (titleTextBlock.ActualWidth == 0 || titleTextBlock.ActualHeight == 0)
                {
                    titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
                }

                const double diamondWidth = 100;
                var titleLeft = x + (diamondWidth / 2) - (titleTextBlock.ActualWidth / 2);
                var titleTop = y - titleTextBlock.ActualHeight - 4;
                Canvas.SetLeft(titleTextBlock, titleLeft);
                Canvas.SetTop(titleTextBlock, titleTop);
            }

            // ✅ Loop và Loop Body di chuyển riêng: khi di chuyển Loop Node (diamond) không kéo theo Body.

            // ✅ Cập nhật ports của Loop Node với diamond shape positioning
            if (node is LoopNode loopNode2)
            {
                // ✅ Use fixed diamond size (100x100) instead of ActualWidth/ActualHeight to prevent port jumping
                const double diamondWidth = 100;
                const double diamondHeight = 100;
                const double portRadius = 9.0;

                foreach (var port in loopNode2.Ports.Where(p => p.IsVisible))
                {
                    // ✅ Position ports at diamond corners
                    Point portPosition;
                    switch (port.Position)
                    {
                        case PortPosition.Left:
                            // Left corner: (0, height/2)
                            portPosition = new Point(x, y + diamondHeight / 2);
                            break;
                        case PortPosition.Right:
                            // Right corner: (width, height/2)
                            portPosition = new Point(x + diamondWidth, y + diamondHeight / 2);
                            break;
                        case PortPosition.Bottom:
                            // Bottom: (width/2, height)
                            portPosition = new Point(x + diamondWidth / 2, y + diamondHeight);
                            break;
                        case PortPosition.Top:
                            // Top: (width/2, 0) - for condition port if needed
                            portPosition = new Point(x + diamondWidth / 2, y);
                            break;
                        default:
                            portPosition = new Point(x, y);
                            break;
                    }

                    port.PositionPoint = portPosition;

                    if (port.PortUI != null)
                    {
                        Canvas.SetLeft(port.PortUI, portPosition.X - portRadius);
                        Canvas.SetTop(port.PortUI, portPosition.Y - portRadius);
                    }

                    // ✅ Update port colors (CRITICAL: Always update colors)
                    if (port.PortUI != null)
                    {
                        Color? portSpecificColor = null;
                        if (!string.IsNullOrEmpty(port.ColorKey))
                        {
                            var brush = Application.Current.TryFindResource(port.ColorKey) as SolidColorBrush
                                        ?? Application.Current.TryFindResource(port.ColorKey + "Brush") as SolidColorBrush;
                            if (brush != null) portSpecificColor = brush.Color;
                        }

                        var nodeColor = (loopNode2.NodeBrush as SolidColorBrush)?.Color;
                        var finalColor = portSpecificColor ?? nodeColor ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
                        
                        if (port.PortUI is Ellipse ellipse)
                        {
                            ellipse.Fill = new SolidColorBrush(finalColor);
                        }
                    }

                    _portRenderer.EnsurePortAddedToCanvas(port);
                    if (port.PortUI != null)
                    {
                        Host.ZIndexManager.SetPortZIndex(loopNode2, port.PortUI);
                    }
                }
            }

            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node.Border != null && canvas.Children.Contains(node.Border))
                canvas.Children.Remove(node.Border);

            if (node is LoopNode loopNode)
            {
                // ✅ Remove titleTextBlock nếu có
                if (loopNode.TitleTextBlockUI != null)
                {
                    var titleTextBlock = loopNode.TitleTextBlockUI;
                    if (canvas != null && canvas.Children.Contains(titleTextBlock))
                    {
                        canvas.Children.Remove(titleTextBlock);
                    }
                    // Clear reference để tránh memory leak và hiển thị lại
                    loopNode.TitleTextBlockUI = null;
                }

                // ✅ Xóa container (Thử tìm trong Children nếu reference cũ không hoạt động)
                if (loopNode.ContainerBorder != null && canvas.Children.Contains(loopNode.ContainerBorder))
                {
                    canvas.Children.Remove(loopNode.ContainerBorder);
                }
                else
                {
                    // Fallback: Tìm Border có DataContext là LoopBodyNode
                    var orphanContainer = canvas.Children.OfType<Border>()
                        .FirstOrDefault(b => b.DataContext == loopNode.LoopBodyNode || b.DataContext == loopNode);
                    if (orphanContainer != null)
                    {
                        canvas.Children.Remove(orphanContainer);
                    }
                }

                // ✅ Xóa ports của Loop Body
                foreach (var port in loopNode.LoopBodyNode.Ports)
                {
                    if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                        canvas.Children.Remove(port.PortUI);
                }

                // ✅ FORCE Xóa tất cả connections của Loop Body from ViewModel
                if (Host.ViewModel != null)
                {
                    // Lấy tất cả connections liên quan đến Loop Body (Cả From và To)
                    var bodyConnections = Host.ViewModel.Connections
                        .Where(c => c.FromNode == loopNode.LoopBodyNode || c.ToNode == loopNode.LoopBodyNode)
                        .ToList(); // ToList để copy ra tránh lỗi collection modified

                    foreach(var conn in bodyConnections)
                    {
                        // Xóa visual trước cho chắc
                        Host.ConnectionRenderer.RemoveConnectionVisuals(conn);
                        Host.ViewModel.Connections.Remove(conn);
                    }
                }

                // ✅ Xóa default connection property
                loopNode.DefaultConnection = null;
                
                // ✅ Cập nhật Minimap sau khi xóa
                Host.UpdateMinimap();
            }

            // ✅ Xóa ports của Loop Node
            foreach (var port in node.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    canvas.Children.Remove(port.PortUI);
            }
        }

        public void RemoveAllNodeVisuals(Canvas canvas)
        {
            var borders = canvas.Children.OfType<Border>().Where(b => b.Tag is WorkflowNode).ToList();
            foreach (var border in borders)
            {
                canvas.Children.Remove(border);
            }

            var ports = canvas.Children
                .OfType<Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18))
                .ToList();
            foreach (var port in ports)
            {
                canvas.Children.Remove(port);
            }
        }
    }
}
