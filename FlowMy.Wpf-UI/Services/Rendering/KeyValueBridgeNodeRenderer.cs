using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Services.Rendering;

public sealed class KeyValueBridgeNodeRenderer : INodeRenderer
{
    private readonly PortRenderer _portRenderer;
    private readonly IWorkflowEditorHostAccessor _hostAccessor;
    private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

    public KeyValueBridgeNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
    {
        _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
    }

    private static bool ShouldShowPort(KeyValueBridgeNode kn, NodePort port)
        => !port.IsInput || kn.ShouldShowFlowInputPort;

    public void RenderNode(WorkflowNode node, Canvas canvas)
    {
        if (node is not KeyValueBridgeNode bridgeNode) return;

        var window = Host as Window;
        bridgeNode.Border = KeyValueBridgeNodeControl.CreateBorder(bridgeNode, window, Host);
        bridgeNode.Border.Tag = node;

        NodeChrome.Apply(bridgeNode.Border, bridgeNode, Host);

        bridgeNode.Border.MouseDown += Host.NodeMouseDown;
        bridgeNode.Border.MouseMove += Host.NodeMouseMove;
        bridgeNode.Border.MouseUp += Host.NodeMouseUp;
        bridgeNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
        bridgeNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
        bridgeNode.Border.ContextMenu = Host.CreateNodeContextMenu(node);

        Canvas.SetLeft(bridgeNode.Border, bridgeNode.X);
        Canvas.SetTop(bridgeNode.Border, bridgeNode.Y);
        canvas.Children.Add(bridgeNode.Border);
        Host.ZIndexManager.InitializeNodeZIndex(bridgeNode, bridgeNode.Border);

        bridgeNode.Border.SizeChanged += (_, _) =>
        {
            foreach (var port in bridgeNode.Ports.Where(p => p.IsVisible))
                _portRenderer.UpdatePortsPositionOnSide(bridgeNode, port.Position);
            if (Host.ViewModel != null)
                foreach (var c in Host.ViewModel.Connections.Where(x => x.FromNode == bridgeNode || x.ToNode == bridgeNode))
                    Host.RenderConnection(c);
        };

        foreach (var port in bridgeNode.Ports)
        {
            var show = ShouldShowPort(bridgeNode, port);
            port.IsVisible = show;
            if (!show) continue;

            var portColor = ResolvePortColor(port);
            if (port.PortUI == null)
            {
                port.PortUI = _portRenderer.CreatePort(portColor);
                port.PortUI.Tag = port;
            }
            else if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                ellipse.Fill = new SolidColorBrush(portColor);

            _portRenderer.UpdatePortsPositionOnSide(bridgeNode, port.Position);
            _portRenderer.EnsurePortAddedToCanvas(port);
            Host.ZIndexManager.SetPortZIndex(bridgeNode, port.PortUI);
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

        if (node is KeyValueBridgeNode bridge && bridge.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
        {
            var titleTextBlock = bridge.TitleTextBlockUI;
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

        if (node is KeyValueBridgeNode kn)
        {
            foreach (var port in kn.Ports)
            {
                var show = ShouldShowPort(kn, port);
                port.IsVisible = show;
                if (!show)
                {
                    if (port.PortUI != null && Host.WorkflowCanvas != null)
                    {
                        if (Host.WorkflowCanvas.Children.Contains(port.PortUI))
                            Host.WorkflowCanvas.Children.Remove(port.PortUI);
                        port.PortUI = null;
                    }
                    continue;
                }

                var portColor = ResolvePortColor(port);
                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                    ellipse.Fill = new SolidColorBrush(portColor);

                _portRenderer.UpdatePortsPositionOnSide(kn, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(kn, port.PortUI);
            }
        }

        Host.SyncAllPortsZIndex(node);
    }

    public void RemoveNode(WorkflowNode node, Canvas canvas)
    {
        if (node is KeyValueBridgeNode bridge && bridge.TitleTextBlockUI != null)
        {
            var tb = bridge.TitleTextBlockUI;
            if (canvas.Children.Contains(tb))
                canvas.Children.Remove(tb);
            bridge.TitleTextBlockUI = null;
        }

        if (node.Border != null && canvas.Children.Contains(node.Border))
            canvas.Children.Remove(node.Border);

        foreach (var port in node.Ports)
        {
            if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                canvas.Children.Remove(port.PortUI);
        }
    }

    private static Color ResolvePortColor(NodePort port)
    {
        if (!string.IsNullOrWhiteSpace(port.ColorKey))
        {
            var c = GetColorFromTheme($"{port.ColorKey}Brush") ?? GetColorFromTheme(port.ColorKey);
            if (c.HasValue) return c.Value;
        }
        return port.IsInput
            ? GetColorFromTheme("InfoBrush") ?? Colors.Orange
            : GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan;
    }

    public void RemoveAllNodeVisuals(Canvas canvas)
    {
        // Handled by NodeRenderer.RemoveAllNodeVisuals (generic).
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
