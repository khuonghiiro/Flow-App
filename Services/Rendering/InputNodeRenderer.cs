using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class InputNodeRenderer : INodeRenderer
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly PortRenderer _portRenderer;

        private IWorkflowEditorHost _host => _hostAccessor.GetRequiredHost();

        public InputNodeRenderer(IWorkflowEditorHostAccessor hostAccessor, PortRenderer portRenderer)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not InputNode inputNode)
            {
                throw new InvalidOperationException("InputNodeRenderer can only render InputNode.");
            }

            node.Border = InputNodeControl.CreateBorder(inputNode, Application.Current.MainWindow, _host);
            NodeChrome.Apply(node.Border, node, _host);

            node.Border.MouseDown += _host.NodeMouseDown;
            node.Border.MouseMove += _host.NodeMouseMove;
            node.Border.MouseUp += _host.NodeMouseUp;
            node.Border.MouseEnter += _host.NodeBorderMouseEnter;
            node.Border.MouseLeave += _host.NodeBorderMouseLeave;
            node.Border.ContextMenu = _host.CreateNodeContextMenu(node);

            Canvas.SetLeft(node.Border, node.X);
            Canvas.SetTop(node.Border, node.Y);
            canvas.Children.Add(node.Border);

            _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);

            RenderNodePorts(node, canvas);
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

            // Update titleTextBlock position if exists
            if (node is InputNode inputNode && inputNode.TitleTextBlockUI != null && _host.WorkflowCanvas != null)
            {
                var titleTextBlock = inputNode.TitleTextBlockUI;
                if (node.Border != null && _host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    var titleLeft = x + (node.Border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
                    var titleTop = y - titleTextBlock.ActualHeight - 4;
                    Canvas.SetLeft(titleTextBlock, titleLeft);
                    Canvas.SetTop(titleTextBlock, titleTop);
                }
            }

            RenderNodePorts(node, _host.WorkflowCanvas);
            _host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            // Remove titleTextBlock from canvas when node is removed
            if (node is InputNode inputNode && inputNode.TitleTextBlockUI != null && canvas != null)
            {
                if (canvas.Children.Contains(inputNode.TitleTextBlockUI))
                {
                    canvas.Children.Remove(inputNode.TitleTextBlockUI);
                }
                inputNode.TitleTextBlockUI = null;
            }

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

        private void RenderNodePorts(WorkflowNode node, Canvas canvas)
        {
            if (node.Border == null) return;

            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                if (port.PortUI == null)
                {
                    var portColor = GetColorFromTheme($"SunsetOrangeBrush") ?? Colors.Cyan;
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                _host.ZIndexManager.SetPortZIndex(node, port.PortUI);
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

