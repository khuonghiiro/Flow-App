using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Services.Rendering
{
    public sealed class BorderHighlightNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public BorderHighlightNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new System.ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new System.ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not BorderHighlightNode borderHighlightNode) return;

            // Tạo UI
            node.Border = BorderHighlightNodeControl.CreateBorder(borderHighlightNode, Application.Current?.MainWindow, Host);
            node.Border.Tag = node;

            // Apply chrome (header buttons + data panel)
            NodeChrome.Apply(node.Border, node, Host);

            // Attach event handlers (CRITICAL - enables drag/drop)
            node.Border.MouseDown += Host.NodeMouseDown;
            node.Border.MouseMove += Host.NodeMouseMove;
            node.Border.MouseUp += Host.NodeMouseUp;
            node.Border.MouseEnter += Host.NodeBorderMouseEnter;
            node.Border.MouseLeave += Host.NodeBorderMouseLeave;

            // Context menu
            node.Border.ContextMenu = Host.CreateNodeContextMenu(node);

            // Position on canvas
            Canvas.SetLeft(node.Border, node.X);
            Canvas.SetTop(node.Border, node.Y);
            canvas.Children.Add(node.Border);

            // Z-index
            Host.ZIndexManager.InitializeNodeZIndex(node, node.Border);

            // Thêm titleTextBlock vào canvas nếu có
            if (borderHighlightNode.TitleTextBlockUI != null && !canvas.Children.Contains(borderHighlightNode.TitleTextBlockUI))
            {
                canvas.Children.Add(borderHighlightNode.TitleTextBlockUI);
                // Set initial position
                if (node.Border != null)
                {
                    var titleLeft = node.X + (node.Border.ActualWidth / 2) - (borderHighlightNode.TitleTextBlockUI.ActualWidth / 2);
                    var titleTop = node.Y - borderHighlightNode.TitleTextBlockUI.ActualHeight - 4;
                    Canvas.SetLeft(borderHighlightNode.TitleTextBlockUI, titleLeft);
                    Canvas.SetTop(borderHighlightNode.TitleTextBlockUI, titleTop);
                    Panel.SetZIndex(borderHighlightNode.TitleTextBlockUI, 20000);
                }
            }

            // Render ports
            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                Color portColor;
                if (!string.IsNullOrWhiteSpace(port.ColorKey))
                {
                    var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush")
                                    ?? GetColorFromTheme(port.ColorKey);
                    portColor = colorFromKey ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
                }
                else
                {
                    portColor = port.IsInput
                        ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                        : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
                }

                // Xóa port cũ nếu đã tồn tại để tạo lại với màu mới
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Remove(port.PortUI);
                    port.PortUI = null;
                }

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else
                {
                    if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                    {
                        ellipse.Fill = new SolidColorBrush(portColor);
                    }
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

            // Update titleTextBlock position nếu có
            if (node is BorderHighlightNode borderHighlightNode && borderHighlightNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var titleTextBlock = borderHighlightNode.TitleTextBlockUI;

                // Đảm bảo titleTextBlock được thêm vào canvas nếu chưa có
                if (!Host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    Host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);
                }

                if (node.Border != null)
                {
                    // Đảm bảo ActualWidth và ActualHeight đã được tính toán
                    if (titleTextBlock.ActualWidth == 0 || titleTextBlock.ActualHeight == 0)
                    {
                        titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
                    }

                    var titleLeft = x + (node.Border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
                    var titleTop = y - titleTextBlock.ActualHeight - 4;
                    Canvas.SetLeft(titleTextBlock, titleLeft);
                    Canvas.SetTop(titleTextBlock, titleTop);
                }
            }

            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                Color portColor;
                if (!string.IsNullOrWhiteSpace(port.ColorKey))
                {
                    var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush")
                                    ?? GetColorFromTheme(port.ColorKey);
                    portColor = colorFromKey ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
                }
                else
                {
                    portColor = port.IsInput
                        ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                        : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
                }

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else
                {
                    if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                    {
                        ellipse.Fill = new SolidColorBrush(portColor);
                    }
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node.Border != null && canvas.Children.Contains(node.Border))
            {
                canvas.Children.Remove(node.Border);
            }

            // Remove titleTextBlock nếu có
            if (node is BorderHighlightNode borderHighlightNode && borderHighlightNode.TitleTextBlockUI != null)
            {
                var titleTextBlock = borderHighlightNode.TitleTextBlockUI;
                if (canvas != null && canvas.Children.Contains(titleTextBlock))
                {
                    canvas.Children.Remove(titleTextBlock);
                }
                // Clear reference để tránh memory leak và hiển thị lại
                borderHighlightNode.TitleTextBlockUI = null;
            }

            foreach (var port in node.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Remove(port.PortUI);
                }
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
                .OfType<System.Windows.Shapes.Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18))
                .ToList();
            foreach (var port in ports)
            {
                canvas.Children.Remove(port);
            }
        }

        private static Color? GetColorFromTheme(string resourceKey)
        {
            try
            {
                var brush = Application.Current.TryFindResource(resourceKey) as SolidColorBrush;
                return brush?.Color;
            }
            catch
            {
                return null;
            }
        }
    }
}
