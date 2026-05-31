using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class TextScanNodeRenderer : INodeRenderer
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly PortRenderer _portRenderer;

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public TextScanNodeRenderer(
            IWorkflowEditorHostAccessor hostAccessor,
            PortRenderer portRenderer)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not TextScanNode textScanNode)
                throw new InvalidOperationException("TextScanNodeRenderer can only render TextScanNode.");

            // Tạo border từ NodeControl
            textScanNode.Border = TextScanNodeControl.CreateBorder(
                textScanNode,
                Host as System.Windows.Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host
            );
            NodeChrome.Apply(textScanNode.Border, textScanNode, Host);

            // QUAN TRỌNG: Gắn các handler from Host (drag + hover)
            textScanNode.Border.MouseDown += Host.NodeMouseDown;
            textScanNode.Border.MouseMove += Host.NodeMouseMove;
            textScanNode.Border.MouseUp += Host.NodeMouseUp;
            textScanNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            textScanNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            textScanNode.Border.ContextMenu = Host.CreateNodeContextMenu(textScanNode);

            // Đặt vị trí và thêm vào canvas
            Canvas.SetLeft(textScanNode.Border, textScanNode.X);
            Canvas.SetTop(textScanNode.Border, textScanNode.Y);
            canvas.Children.Add(textScanNode.Border);
            Host.ZIndexManager.InitializeNodeZIndex(textScanNode, textScanNode.Border);

            // Render ports (tạo qua PortRenderer để auto-wire events)
            foreach (var port in textScanNode.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                }
                else if (port.PortUI is Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(textScanNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(textScanNode, port.PortUI);
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

            // ⚠️ CRITICAL: Update title TextBlock position
            if (node is TextScanNode textScanNode && textScanNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var title = textScanNode.TitleTextBlockUI;
                if (!Host.WorkflowCanvas.Children.Contains(title))
                {
                    Host.WorkflowCanvas.Children.Add(title);
                    Panel.SetZIndex(title, 20000);
                }
                if (node.Border != null)
                {
                    if (title.ActualWidth == 0 || title.ActualHeight == 0)
                    {
                        title.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        title.Arrange(new Rect(title.DesiredSize));
                    }
                    Canvas.SetLeft(title, x + (node.Border.ActualWidth / 2) - (title.ActualWidth / 2));
                    Canvas.SetTop(title, y - title.ActualHeight - 4);
                }
            }

            foreach (var port in node.Ports.Where(p => p.IsVisible && p.PortUI != null))
            {
                var portColor = ResolvePortColor(port);

                if (port.PortUI is Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            // ⚠️ CRITICAL: Remove title TextBlock và clear reference
            if (node is TextScanNode textScanNode && textScanNode.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(textScanNode.TitleTextBlockUI))
                    canvas.Children.Remove(textScanNode.TitleTextBlockUI);
                textScanNode.TitleTextBlockUI = null;
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
            catch
            {
                return null;
            }
        }
    }
}
