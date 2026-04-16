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
    public sealed class ScreenCaptureNodeRenderer : INodeRenderer
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly PortRenderer _portRenderer;

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public ScreenCaptureNodeRenderer(
            IWorkflowEditorHostAccessor hostAccessor,
            PortRenderer portRenderer)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not ScreenCaptureNode captureNode)
                throw new InvalidOperationException("ScreenCaptureNodeRenderer can only render ScreenCaptureNode.");

            // Tạo border từ NodeControl
            captureNode.Border = ScreenCaptureNodeControl.CreateBorder(
                captureNode,
                Host as System.Windows.Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host
            );
            NodeChrome.Apply(captureNode.Border, captureNode, Host);

            // QUAN TRỌNG: Gắn các handler from Host (drag + hover)
            captureNode.Border.MouseDown += Host.NodeMouseDown;
            captureNode.Border.MouseMove += Host.NodeMouseMove;
            captureNode.Border.MouseUp += Host.NodeMouseUp;
            captureNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            captureNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            captureNode.Border.ContextMenu = Host.CreateNodeContextMenu(captureNode);

            // Đặt vị trí và thêm vào canvas
            Canvas.SetLeft(captureNode.Border, captureNode.X);
            Canvas.SetTop(captureNode.Border, captureNode.Y);
            canvas.Children.Add(captureNode.Border);
            Host.ZIndexManager.InitializeNodeZIndex(captureNode, captureNode.Border);

            // Render ports (tạo qua PortRenderer để auto-wire events)
            foreach (var port in captureNode.Ports.Where(p => p.IsVisible))
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

                _portRenderer.UpdatePortsPositionOnSide(captureNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(captureNode, port.PortUI);
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