using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class HtmlUiNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public HtmlUiNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not HtmlUiNode htmlNode) return;

            var window = Host as Window;
            node.Border = HtmlUiNodeControl.CreateBorder(htmlNode, window, Host);
            node.Border.Tag = node;

            NodeChrome.Apply(node.Border, node, Host);

            node.Border.MouseDown += Host.NodeMouseDown;
            node.Border.MouseMove += Host.NodeMouseMove;
            node.Border.MouseUp += Host.NodeMouseUp;
            node.Border.MouseEnter += Host.NodeBorderMouseEnter;
            node.Border.MouseLeave += Host.NodeBorderMouseLeave;
            node.Border.ContextMenu = Host.CreateNodeContextMenu(node);

            Canvas.SetLeft(node.Border, node.X);
            Canvas.SetTop(node.Border, node.Y);
            canvas.Children.Add(node.Border);

            Host.ZIndexManager.InitializeNodeZIndex(node, node.Border);

            if (htmlNode.TitleTextBlockUI != null && !canvas.Children.Contains(htmlNode.TitleTextBlockUI))
            {
                canvas.Children.Add(htmlNode.TitleTextBlockUI);
                if (node.Border != null)
                {
                    var titleLeft = node.X + (node.Border.ActualWidth / 2) - (htmlNode.TitleTextBlockUI.ActualWidth / 2);
                    var titleTop = node.Y - htmlNode.TitleTextBlockUI.ActualHeight - 4;
                    Canvas.SetLeft(htmlNode.TitleTextBlockUI, titleLeft);
                    Canvas.SetTop(htmlNode.TitleTextBlockUI, titleTop);
                    Panel.SetZIndex(htmlNode.TitleTextBlockUI, 20000);
                }
            }

            // ✅ CRITICAL: Cleanup port cũ của node này trước khi render port mới
            // Tránh port trùng khi save/load hoặc di chuyển node
            CleanupOrphanedPortsForNode(node, canvas);

            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);

                if (port.PortUI == null)
                {
                    // Tạo port với margin khác nhau tùy vị trí để dễ nhìn khi bị khuất
                    var margin = GetPortMarginForPosition(port.Position);
                    port.PortUI = _portRenderer.CreateRectangularPortWithMargin(portColor, margin, width: 12, height: 25);
                    port.PortUI.Tag = port;
                }
                else
                {
                    var shape = PortRenderer.GetActualPortShape(port.PortUI);
                    if (shape != null)
                        shape.Fill = new SolidColorBrush(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }
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

            if (node is HtmlUiNode htmlNode)
            {
                // Sync Width/Height cho HtmlUiNode
                if (node.Border != null)
                {
                    node.Border.Width = htmlNode.Width;
                    node.Border.Height = htmlNode.Height;
                }

                // Update title position - đảm bảo update real-time khi drag
                if (htmlNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
                {
                    var titleTextBlock = htmlNode.TitleTextBlockUI;

                    // Đảm bảo titleTextBlock đã được add vào canvas
                    if (!Host.WorkflowCanvas.Children.Contains(titleTextBlock))
                    {
                        Host.WorkflowCanvas.Children.Add(titleTextBlock);
                        Panel.SetZIndex(titleTextBlock, 20000);
                    }

                    if (node.Border != null)
                    {
                        // Measure nếu chưa có kích thước
                        if (titleTextBlock.ActualWidth == 0 || titleTextBlock.ActualHeight == 0)
                        {
                            titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                            titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
                        }

                        // Tính toán và set position ngay lập tức
                        var titleLeft = x + (node.Border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
                        var titleTop = y - titleTextBlock.ActualHeight - 4;

                        Canvas.SetLeft(titleTextBlock, titleLeft);
                        Canvas.SetTop(titleTextBlock, titleTop);

                        // Force update để đảm bảo hiển thị ngay
                        titleTextBlock.UpdateLayout();
                    }
                }
            }

            // ✅ CRITICAL: Cleanup port cũ của node này trước khi render port mới
            // Tránh port trùng khi save/load hoặc di chuyển node
            CleanupOrphanedPortsForNode(node, Host.WorkflowCanvas);

            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);

                if (port.PortUI == null)
                {
                    // Tạo port với margin khác nhau tùy vị trí để dễ nhìn khi bị khuất
                    var margin = GetPortMarginForPosition(port.Position);
                    port.PortUI = _portRenderer.CreateRectangularPortWithMargin(portColor, margin, width: 12, height: 25);
                    port.PortUI.Tag = port;
                }
                else
                {
                    var shape = PortRenderer.GetActualPortShape(port.PortUI);
                    if (shape != null)
                        shape.Fill = new SolidColorBrush(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node is HtmlUiNode htmlNode && htmlNode.TitleTextBlockUI != null)
            {
                var titleTextBlock = htmlNode.TitleTextBlockUI;
                if (canvas != null && canvas.Children.Contains(titleTextBlock))
                    canvas.Children.Remove(titleTextBlock);
                htmlNode.TitleTextBlockUI = null;
            }

            if (node.Border != null && canvas != null && canvas.Children.Contains(node.Border))
                canvas.Children.Remove(node.Border);

            foreach (var port in node.Ports)
            {
                if (port?.PortUI != null && canvas != null && canvas.Children.Contains(port.PortUI))
                    canvas.Children.Remove(port.PortUI);
            }
        }

        public void RemoveAllNodeVisuals(Canvas canvas)
        {
            var borders = canvas.Children.OfType<Border>().Where(b => b.Tag is WorkflowNode).ToList();
            foreach (var border in borders)
                canvas.Children.Remove(border);

            // ✅ Tìm tất cả port UI: có thể là Shape (Ellipse/Rectangle) hoặc Border wrapper (FrameworkElement)
            // Port có Tag là NodePort hoặc có child là Shape với kích thước đặc biệt
            var ports = new List<UIElement>();
            
            // 1. Tìm Shape có Tag là NodePort hoặc có kích thước đặc biệt
            var shapePorts = canvas.Children.OfType<Shape>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18) || (e.Width == 12 && e.Height == 25) ||
                    (e.Width == 12 && e.Height == 12) ||
                    (e.Width == 25 && e.Height == 25) ||
                    // ✅ Thêm: Rectangle có Tag là Size (port chữ nhật đã được highlight, có thể có kích thước khác)
                    (e is Rectangle rect && rect.Tag is Size)
                ).ToList();
            ports.AddRange(shapePorts);
            
            // 2. ✅ Tìm Border/FrameworkElement có Tag là NodePort (port hình chữ nhật với margin wrapper)
            // Loại trừ Border có Tag là WorkflowNode (đã xóa ở trên)
            var frameworkElementPorts = canvas.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag is NodePort && !(e is Border border && border.Tag is WorkflowNode))
                .ToList();
            ports.AddRange(frameworkElementPorts);
            
            // 3. ✅ Tìm Border có child là Rectangle với kích thước port hoặc có Tag là Size
            var borderPorts = canvas.Children.OfType<Border>()
                .Where(b => 
                {
                    // Nếu Border có Tag là NodePort, thêm vào
                    if (b.Tag is NodePort)
                        return true;
                    
                    // Nếu Border có child là Rectangle với kích thước port hoặc có Tag là Size
                    if (b.Child is Rectangle rect)
                    {
                        // ✅ Kiểm tra kích thước port chuẩn
                        if ((rect.Width == 12 && rect.Height == 25) ||
                            (rect.Width == 10 && rect.Height == 18) ||
                            // ✅ Kiểm tra kích thước port đã được highlight (phóng to +2px mỗi chiều)
                            (rect.Width == 14 && rect.Height == 27) ||
                            (rect.Width == 12 && rect.Height == 20))
                            return true;
                        
                        // ✅ Kiểm tra Tag là Size (port chữ nhật luôn có Tag là Size)
                        if (rect.Tag is Size)
                            return true;
                    }
                    
                    return false;
                })
                .ToList();
            ports.AddRange(borderPorts);
            
            // Xóa tất cả ports đã tìm thấy (loại bỏ duplicate)
            foreach (var port in ports.Distinct())
            {
                if (port != null && canvas.Children.Contains(port))
                    canvas.Children.Remove(port);
            }
        }

        private static Color ResolvePortColor(NodePort port)
        {
            // Ưu tiên ColorKey của port nếu có
            if (!string.IsNullOrWhiteSpace(port.ColorKey))
            {
                var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush")
                                   ?? GetColorFromTheme(port.ColorKey);
                if (colorFromKey.HasValue)
                    return colorFromKey.Value;
            }

            // Fallback: IN = Info, OUT = SunsetOrange
            if (port.IsInput)
            {
                return GetColorFromTheme("InfoBrush") ?? Colors.Orange;
            }

            return GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan;
        }

        private static Color? GetColorFromTheme(string resourceKey)
        {
            try
            {
                var brush = Application.Current.TryFindResource(resourceKey) as SolidColorBrush;
                return brush?.Color;
            }
            catch { return null; }
        }

        /// <summary>
        /// Cleanup port cũ (orphaned) của node này trên canvas.
        /// Port có thể bị mất reference (port.PortUI = null) nhưng vẫn còn trên canvas sau khi save/load.
        /// </summary>
        private void CleanupOrphanedPortsForNode(WorkflowNode node, Canvas? canvas)
        {
            if (canvas == null) return;

            var orphanedPorts = new List<UIElement>();

            // 1. Chỉ xử lý các port thuộc VỀ node này
            //    → tránh đụng vào ports của node khác khi di chuyển HtmlUi node
            var allPortsOnCanvas = canvas.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag is NodePort)
                .ToList();

            foreach (var portUI in allPortsOnCanvas)
            {
                if (portUI.Tag is NodePort portTag)
                {
                    // Nếu port này KHÔNG thuộc về node hiện tại → bỏ qua
                    if (!node.Ports.Contains(portTag))
                        continue;

                    // Port thuộc node này nhưng UI không còn khớp với PortUI hiện tại → orphaned
                    if (portTag.PortUI != null && !ReferenceEquals(portTag.PortUI, portUI))
                    {
                        orphanedPorts.Add(portUI);
                    }
                }
            }

            // Xóa tất cả orphaned ports
            foreach (var orphanedPort in orphanedPorts.Distinct())
            {
                if (canvas.Children.Contains(orphanedPort))
                {
                    canvas.Children.Remove(orphanedPort);
                }
            }
        }

        /// <summary>Lấy margin cho port dựa trên vị trí để dễ nhìn khi bị khuất.</summary>
        private static Thickness GetPortMarginForPosition(PortPosition position)
        {
            return position switch
            {
                PortPosition.Left => new Thickness(6, 2, 15, 2),   // Margin phải lớn hơn để port sang trái nhiều hơn
                PortPosition.Right => new Thickness(15, 2, 6, 2),  // Margin trái lớn hơn để port sang phải nhiều hơn
                PortPosition.Top => new Thickness(2, 3, 2, 1),   // Margin trên lớn hơn
                PortPosition.Bottom => new Thickness(2, 1, 2, 3), // Margin dưới lớn hơn
                _ => new Thickness(2)
            };
        }
    }
}

