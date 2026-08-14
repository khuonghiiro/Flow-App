using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Services.Rendering
{
    public sealed class MediaGalleryNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public MediaGalleryNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not MediaGalleryNode galleryNode)
                return;

            var window = Host as Window;
            node.Border = MediaGalleryNodeControl.CreateBorder(galleryNode, window, Host);
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

            if (galleryNode.TitleTextBlockUI != null && !canvas.Children.Contains(galleryNode.TitleTextBlockUI))
            {
                canvas.Children.Add(galleryNode.TitleTextBlockUI);
                if (node.Border != null)
                {
                    var titleLeft = node.X + (node.Border.ActualWidth / 2) - (galleryNode.TitleTextBlockUI.ActualWidth / 2);
                    var titleTop = node.Y - galleryNode.TitleTextBlockUI.ActualHeight - 4;
                    Canvas.SetLeft(galleryNode.TitleTextBlockUI, titleLeft);
                    Canvas.SetTop(galleryNode.TitleTextBlockUI, titleTop);
                    Panel.SetZIndex(galleryNode.TitleTextBlockUI, 20000);
                }
            }

            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor);
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
            if (node is MediaGalleryNode galleryNode && galleryNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var tb = galleryNode.TitleTextBlockUI;
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
            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
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
            if (node is MediaGalleryNode galleryNode && galleryNode.TitleTextBlockUI != null)
            {
                var tb = galleryNode.TitleTextBlockUI;
                if (canvas != null && canvas.Children.Contains(tb))
                    canvas.Children.Remove(tb);
                galleryNode.TitleTextBlockUI = null;
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

            var ports = canvas.Children
                .OfType<System.Windows.Shapes.Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18))
                .ToList();
            foreach (var port in ports)
                canvas.Children.Remove(port);
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
    }
}
