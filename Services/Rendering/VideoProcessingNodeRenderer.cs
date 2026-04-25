using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Services.Rendering
{
    public sealed class VideoProcessingNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public VideoProcessingNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer;
            _hostAccessor = hostAccessor;
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not VideoProcessingNode vpNode) return;

            var window = Host as Window;
            node.Border = VideoProcessingNodeControl.CreateBorder(vpNode, window, Host);
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

            RenderPorts(node);
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
            RenderPorts(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node is VideoProcessingNode vpNode && vpNode.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(vpNode.TitleTextBlockUI))
                    canvas.Children.Remove(vpNode.TitleTextBlockUI);
                vpNode.TitleTextBlockUI = null;
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
        }

        private void RenderPorts(WorkflowNode node)
        {
            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                var color = ResolvePortColor(port);
                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(color);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(color);
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }
        }

        private static Color ResolvePortColor(NodePort port)
        {
            if (!string.IsNullOrWhiteSpace(port.ColorKey))
            {
                var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush") ?? GetColorFromTheme(port.ColorKey);
                if (colorFromKey.HasValue) return colorFromKey.Value;
            }
            return port.IsInput
                ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
        }

        private static Color? GetColorFromTheme(string key)
        {
            var resource = Application.Current.TryFindResource(key);
            if (resource is SolidColorBrush brush) return brush.Color;
            if (resource is Color color) return color;
            return null;
        }
    }
}
