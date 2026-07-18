using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;

namespace FlowMy.Services.Rendering
{
    public sealed class DynamicUiNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public DynamicUiNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not DynamicUiNode dynamicUiNode) return;

            dynamicUiNode.Border = DynamicUiNodeControl.CreateBorder(
                dynamicUiNode,
                Host as Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host);

            NodeChrome.Apply(dynamicUiNode.Border, dynamicUiNode, Host);

            dynamicUiNode.Border.MouseDown += Host.NodeMouseDown;
            dynamicUiNode.Border.MouseMove += Host.NodeMouseMove;
            dynamicUiNode.Border.MouseUp += Host.NodeMouseUp;
            dynamicUiNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            dynamicUiNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            dynamicUiNode.Border.ContextMenu = null;

            Canvas.SetLeft(dynamicUiNode.Border, dynamicUiNode.X);
            Canvas.SetTop(dynamicUiNode.Border, dynamicUiNode.Y);
            canvas.Children.Add(dynamicUiNode.Border);

            Host.ZIndexManager.InitializeNodeZIndex(dynamicUiNode, dynamicUiNode.Border);

            RenderPorts(dynamicUiNode);
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

            if (node is DynamicUiNode dynamicUiNode)
            {
                // Update title position
                if (dynamicUiNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
                {
                    var titleTextBlock = dynamicUiNode.TitleTextBlockUI;
                    if (!Host.WorkflowCanvas.Children.Contains(titleTextBlock))
                    {
                        Host.WorkflowCanvas.Children.Add(titleTextBlock);
                        Panel.SetZIndex(titleTextBlock, 20000);
                    }

                    if (node.Border != null)
                    {
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

                RenderPorts(dynamicUiNode);
            }
            else
            {
                // Fallback for standard nodes
                foreach (var port in node.Ports.Where(p => p.IsVisible))
                {
                    Color portColor = GetPortColor(port);

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

            Host.SyncAllPortsZIndex(node);
        }

        private void RenderPorts(DynamicUiNode node)
        {
            // Gather standard ports (IN/OUT) and dynamic ports (if any)
            // Ensure they are correctly drawn based on current visible list
            foreach (var port in node.Ports)
            {
                port.IsVisible = true; // Always show standard IN/OUT run ports
                Color portColor = GetPortColor(port);

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

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node is DynamicUiNode dynamicUiNode && dynamicUiNode.TitleTextBlockUI != null)
            {
                var titleTextBlock = dynamicUiNode.TitleTextBlockUI;
                if (canvas.Children.Contains(titleTextBlock))
                {
                    canvas.Children.Remove(titleTextBlock);
                }
                dynamicUiNode.TitleTextBlockUI = null;
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
            foreach (var border in borders) canvas.Children.Remove(border);

            var ports = canvas.Children.OfType<System.Windows.Shapes.Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18)).ToList();
            foreach (var port in ports) canvas.Children.Remove(port);
        }

        private static Color GetPortColor(NodePort port)
        {
            if (!string.IsNullOrWhiteSpace(port.ColorKey))
            {
                var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush")
                                   ?? GetColorFromTheme(port.ColorKey);
                if (colorFromKey.HasValue) return colorFromKey.Value;
            }
            return port.IsInput
                ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
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
    }
}
