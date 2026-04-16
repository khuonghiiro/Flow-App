using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class WebNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public WebNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not WebNode webNode)
                return;

            var window = Host as Window;
            node.Border = WebNodeControl.CreateBorder(webNode, window, Host);
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

            node.Border.SizeChanged += (s, e) =>
            {
                foreach (var port in node.Ports.Where(p => p.IsVisible))
                    _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                if (Host.ViewModel != null)
                {
                    var related = Host.ViewModel.Connections.Where(c => c.FromNode == node || c.ToNode == node).ToList();
                    foreach (var conn in related) Host.UpdateConnectionPath(conn);
                }
            };

            if (webNode.TitleTextBlockUI != null && !canvas.Children.Contains(webNode.TitleTextBlockUI))
            {
                canvas.Children.Add(webNode.TitleTextBlockUI);
                if (node.Border != null)
                {
                    var titleLeft = node.X + (node.Border.ActualWidth / 2) - (webNode.TitleTextBlockUI.ActualWidth / 2);
                    var titleTop = node.Y - webNode.TitleTextBlockUI.ActualHeight - 4;
                    Canvas.SetLeft(webNode.TitleTextBlockUI, titleLeft);
                    Canvas.SetTop(webNode.TitleTextBlockUI, titleTop);
                    Panel.SetZIndex(webNode.TitleTextBlockUI, 20000);
                }
            }

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
                // Ép WebView2 (HwndHost) đồng bộ vị trí khi kéo — không gọi UpdateLayout() để tránh block
                node.Border.InvalidateArrange();
                if (node is WebNode && TryGetWebView2FromBorder(node.Border, out var webView))
                {
                    webView.InvalidateArrange();
                    webView.InvalidateVisual();
                }
            }
            if (node is WebNode webNode && webNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var tb = webNode.TitleTextBlockUI;
                if (!Host.WorkflowCanvas.Children.Contains(tb))
                {
                    Host.WorkflowCanvas.Children.Add(tb);
                    Panel.SetZIndex(tb, 20000);
                }
                if (node.Border != null)
                {
                    if (tb.ActualWidth == 0 || tb.ActualHeight == 0)
                    {
                        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        tb.Arrange(new Rect(tb.DesiredSize));
                    }
                    var titleLeft = x + (node.Border.ActualWidth / 2) - (tb.ActualWidth / 2);
                    var titleTop = y - tb.ActualHeight - 4;
                    Canvas.SetLeft(tb, titleLeft);
                    Canvas.SetTop(tb, titleTop);
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
            if (node is WebNode webNode && webNode.TitleTextBlockUI != null)
            {
                var tb = webNode.TitleTextBlockUI;
                if (canvas != null && canvas.Children.Contains(tb))
                    canvas.Children.Remove(tb);
                webNode.TitleTextBlockUI = null;
            }
            if (node.Border != null && canvas.Children.Contains(node.Border))
                canvas.Children.Remove(node.Border);
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
                canvas.Children.Remove(border);

            // ✅ Tìm tất cả port UI: có thể là Shape (Ellipse/Rectangle) hoặc Border wrapper (FrameworkElement)
            var ports = new List<UIElement>();

            // 1. Tìm Shape có Tag là NodePort hoặc có kích thước đặc biệt
            var shapePorts = canvas.Children.OfType<System.Windows.Shapes.Shape>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18) || (e.Width == 12 && e.Height == 25) ||
                    (e.Width == 12 && e.Height == 12) ||
                    (e.Width == 25 && e.Height == 25) ||
                    // ✅ Thêm: Rectangle có Tag là Size (port chữ nhật đã được highlight, có thể có kích thước khác)
                    (e is Rectangle rect && rect.Tag is Size)
                ).ToList();
            ports.AddRange(shapePorts);

            // 2. ✅ Tìm Border/FrameworkElement có Tag là NodePort (port hình chữ nhật với margin wrapper)
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

        private static Color? GetColorFromTheme(string key)
        {
            try
            {
                var brush = Application.Current.TryFindResource(key) as SolidColorBrush;
                return brush?.Color;
            }
            catch { return null; }
        }

        /// <summary>Lấy WebView2 từ Border của WebNode (border.Child = outerGrid → grid → grid.Children[1] = WebView2).</summary>
        private static bool TryGetWebView2FromBorder(System.Windows.Controls.Border border, out WebView2? webView)
        {
            webView = null;
            if (border?.Child is not Grid outerGrid || outerGrid.Children.Count == 0) return false;
            if (outerGrid.Children[0] is not Grid innerGrid || innerGrid.Children.Count <= 1) return false;
            if (innerGrid.Children[1] is WebView2 wv) { webView = wv; return true; }
            return false;
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
            //    → tránh đụng vào ports của node khác khi di chuyển Web/Html node
            var allPortsOnCanvas = canvas.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag is NodePort)
                .ToList();

            foreach (var portUI in allPortsOnCanvas)
            {
                if (portUI.Tag is NodePort portTag)
                {
                    // Nếu port này KHÔNG thuộc về node hiện tại → bỏ qua, không động tới
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
