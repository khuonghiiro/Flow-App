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
    public sealed class EmbedApplicationNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public EmbedApplicationNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not EmbedApplicationNode embedNode) return;

            // 1. Tạo border từ NodeControl
            embedNode.Border = EmbedApplicationNodeControl.CreateBorder(
                embedNode,
                Host as Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host);
            embedNode.Border.Tag = embedNode;

            // 2. Apply chrome (execution badge, GPU optimization)
            NodeChrome.Apply(embedNode.Border, embedNode, Host);

            // 3. Attach mouse handlers
            embedNode.Border.MouseDown += Host.NodeMouseDown;
            embedNode.Border.MouseMove += Host.NodeMouseMove;
            embedNode.Border.MouseUp += Host.NodeMouseUp;
            embedNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            embedNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            embedNode.Border.ContextMenu = null;

            // 4. Đặt vị trí và thêm vào canvas
            Canvas.SetLeft(embedNode.Border, embedNode.X);
            Canvas.SetTop(embedNode.Border, embedNode.Y);
            canvas.Children.Add(embedNode.Border);
            Host.ZIndexManager.InitializeNodeZIndex(embedNode, embedNode.Border);

            // 5. Render ports
            foreach (var port in embedNode.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(embedNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(embedNode, port.PortUI);
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

            // Update title TextBlock position
            if (node is EmbedApplicationNode embedNode && embedNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var title = embedNode.TitleTextBlockUI;
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

            // Update ports
            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);
                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor);
                }
                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            // Remove title TextBlock
            if (node is EmbedApplicationNode embedNode && embedNode.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(embedNode.TitleTextBlockUI))
                    canvas.Children.Remove(embedNode.TitleTextBlockUI);
                embedNode.TitleTextBlockUI = null;
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
            var borders = canvas.Children.OfType<Border>()
                .Where(b => b.Tag is WorkflowNode).ToList();
            foreach (var b in borders) canvas.Children.Remove(b);

            var ports = canvas.Children.OfType<Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18)).ToList();
            foreach (var p in ports) canvas.Children.Remove(p);
        }

        private static Color ResolvePortColor(NodePort port)
        {
            if (!string.IsNullOrWhiteSpace(port.ColorKey))
            {
                var c = GetColorFromTheme($"{port.ColorKey}Brush") ?? GetColorFromTheme(port.ColorKey);
                if (c.HasValue) return c.Value;
            }
            return port.IsInput
                ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
        }

        private static Color? GetColorFromTheme(string key)
        {
            try { return (Application.Current.TryFindResource(key) as SolidColorBrush)?.Color; }
            catch { return null; }
        }
    }
}
