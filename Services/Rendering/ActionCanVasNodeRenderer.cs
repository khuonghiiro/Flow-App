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
    public sealed class ActionCanVasNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public ActionCanVasNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not ActionCanVasNode actionCanVasNode) return;

            // 1. Tạo border từ NodeControl
            actionCanVasNode.Border = ActionCanVasNodeControl.CreateBorder(
                actionCanVasNode,
                Host as Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host);
            actionCanVasNode.Border.Tag = actionCanVasNode;

            // 2. Apply chrome (execution badge, GPU optimization)
            NodeChrome.Apply(actionCanVasNode.Border, actionCanVasNode, Host);

            // 3. Attach mouse handlers
            actionCanVasNode.Border.MouseDown  += Host.NodeMouseDown;
            actionCanVasNode.Border.MouseMove  += Host.NodeMouseMove;
            actionCanVasNode.Border.MouseUp    += Host.NodeMouseUp;
            actionCanVasNode.Border.ContextMenu = null;

            // 4. Đặt vị trí và thêm vào canvas
            Canvas.SetLeft(actionCanVasNode.Border, actionCanVasNode.X);
            Canvas.SetTop(actionCanVasNode.Border, actionCanVasNode.Y);
            canvas.Children.Add(actionCanVasNode.Border);
            Host.ZIndexManager.InitializeNodeZIndex(actionCanVasNode, actionCanVasNode.Border);

            // 5. Render ports
            foreach (var port in actionCanVasNode.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);
                if (port.PortUI == null)
                {
                    var margin = GetPortMarginForPosition(port.Position);
                    port.PortUI = _portRenderer.CreateRectangularPortWithMargin(portColor, margin, width: 12, height: 25);
                    port.PortUI.Tag = port;
                }
                else
                {
                    var shape = FlowMy.Services.Rendering.PortRenderer.GetActualPortShape(port.PortUI);
                    if (shape != null)
                        shape.Fill = new SolidColorBrush(portColor);
                }
                _portRenderer.UpdatePortsPositionOnSide(actionCanVasNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(actionCanVasNode, port.PortUI);
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

            if (node is ActionCanVasNode actionNode &&
                actionNode.TitleTextBlockUI != null &&
                Host.WorkflowCanvas != null)
            {
                var tb = actionNode.TitleTextBlockUI;
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
                    Canvas.SetLeft(tb, x + (node.Border.ActualWidth / 2) - (tb.ActualWidth / 2));
                    Canvas.SetTop(tb, y - tb.ActualHeight - 4);
                }
            }

            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);
                if (port.PortUI == null)
                {
                    var margin = GetPortMarginForPosition(port.Position);
                    port.PortUI = _portRenderer.CreateRectangularPortWithMargin(portColor, margin, width: 12, height: 25);
                    port.PortUI.Tag = port;
                }
                else
                {
                    var shape = FlowMy.Services.Rendering.PortRenderer.GetActualPortShape(port.PortUI);
                    if (shape != null)
                        shape.Fill = new SolidColorBrush(portColor);
                }
                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node is ActionCanVasNode actionNode && actionNode.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(actionNode.TitleTextBlockUI))
                    canvas.Children.Remove(actionNode.TitleTextBlockUI);
                actionNode.TitleTextBlockUI = null;
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
                var c2 = GetColorFromTheme($"{port.ColorKey}Brush") ?? GetColorFromTheme(port.ColorKey);
                if (c2.HasValue) return c2.Value;
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

        private static Thickness GetPortMarginForPosition(PortPosition position)
        {
            return position switch
            {
                PortPosition.Left => new Thickness(6, 2, 15, 2),
                PortPosition.Right => new Thickness(15, 2, 6, 2),
                PortPosition.Top => new Thickness(2, 3, 2, 1),
                PortPosition.Bottom => new Thickness(2, 1, 2, 3),
                _ => new Thickness(2)
            };
        }
    }
}
