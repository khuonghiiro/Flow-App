using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class ScreenPositionNodeRenderer : INodeRenderer
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly PortRenderer _portRenderer;

        private IWorkflowEditorHost _host => _hostAccessor.GetRequiredHost();

        public ScreenPositionNodeRenderer(IWorkflowEditorHostAccessor hostAccessor, PortRenderer portRenderer)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        }

        public Border CreateBorder(ScreenPositionPickerNode node)
        {
            return ScreenPositionPickerNodeControl.CreateBorder(node, _host as System.Windows.Window ?? throw new InvalidOperationException("Host must be a Window."), _host);
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not ScreenPositionPickerNode screenNode)
            {
                throw new InvalidOperationException("ScreenPositionNodeRenderer can only render ScreenPositionPickerNode.");
            }

            screenNode.Border = CreateBorder(screenNode);
            screenNode.Border.Cursor = Cursors.Hand;
            screenNode.Border.Tag = screenNode;
            NodeChrome.Apply(screenNode.Border, screenNode, _host);

            screenNode.Border.MouseDown += _host.NodeMouseDown;
            screenNode.Border.MouseMove += _host.NodeMouseMove;
            screenNode.Border.MouseUp += _host.NodeMouseUp;
            screenNode.Border.MouseEnter += _host.NodeBorderMouseEnter;
            screenNode.Border.MouseLeave += _host.NodeBorderMouseLeave;
            screenNode.Border.ContextMenu = _host.CreateNodeContextMenu(screenNode);

            Canvas.SetLeft(screenNode.Border, screenNode.X);
            Canvas.SetTop(screenNode.Border, screenNode.Y);
            canvas.Children.Add(screenNode.Border);
            _host.ZIndexManager.InitializeNodeZIndex(screenNode, screenNode.Border);

            // Ports like normal nodes
            foreach (var port in screenNode.Ports.Where(p => p.IsVisible))
            {
                if (port.PortUI == null)
                {
                    var portColor = port.IsInput ? Colors.Orange : Colors.Cyan;
                    port.PortUI = _portRenderer.CreatePort(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(screenNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                _host.ZIndexManager.SetPortZIndex(screenNode, port.PortUI);
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
                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            _host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node.Border != null && canvas.Children.Contains(node.Border))
            {
                canvas.Children.Remove(node.Border);
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

