using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class ScreenPositionNodeRenderer : INodeRenderer
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly PortRenderer _portRenderer;

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public ScreenPositionNodeRenderer(IWorkflowEditorHostAccessor hostAccessor, PortRenderer portRenderer)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not ScreenPositionPickerNode screenNode) return;

            screenNode.Border = ScreenPositionPickerNodeControl.CreateBorder(
                screenNode,
                Host as Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host);
            screenNode.Border.Tag = screenNode;

            NodeChrome.Apply(screenNode.Border, screenNode, Host);

            screenNode.Border.MouseDown  += Host.NodeMouseDown;
            screenNode.Border.MouseMove  += Host.NodeMouseMove;
            screenNode.Border.MouseUp    += Host.NodeMouseUp;
            screenNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            screenNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            // Dialog mở bằng right-click (WithDialogSupport) — không attach ContextMenu
            screenNode.Border.ContextMenu = null;

            Canvas.SetLeft(screenNode.Border, screenNode.X);
            Canvas.SetTop(screenNode.Border, screenNode.Y);
            canvas.Children.Add(screenNode.Border);
            Host.ZIndexManager.InitializeNodeZIndex(screenNode, screenNode.Border);

            // Title TextBlock lên canvas
            if (screenNode.TitleTextBlockUI != null && !canvas.Children.Contains(screenNode.TitleTextBlockUI))
            {
                canvas.Children.Add(screenNode.TitleTextBlockUI);
                if (screenNode.Border != null)
                {
                    var titleLeft = screenNode.X + (screenNode.Border.ActualWidth / 2) - (screenNode.TitleTextBlockUI.ActualWidth / 2);
                    var titleTop  = screenNode.Y - screenNode.TitleTextBlockUI.ActualHeight - 4;
                    Canvas.SetLeft(screenNode.TitleTextBlockUI, titleLeft);
                    Canvas.SetTop(screenNode.TitleTextBlockUI, titleTop);
                    Panel.SetZIndex(screenNode.TitleTextBlockUI, 20000);
                }
            }

            foreach (var port in screenNode.Ports.Where(p => p.IsVisible))
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
                _portRenderer.UpdatePortsPositionOnSide(screenNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(screenNode, port.PortUI);
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

            if (node is ScreenPositionPickerNode screenNode && screenNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var title = screenNode.TitleTextBlockUI;
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
            if (node is ScreenPositionPickerNode screenNode && screenNode.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(screenNode.TitleTextBlockUI))
                    canvas.Children.Remove(screenNode.TitleTextBlockUI);
                screenNode.TitleTextBlockUI = null;
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

