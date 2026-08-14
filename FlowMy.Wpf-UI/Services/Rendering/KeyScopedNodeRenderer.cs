using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;

namespace FlowMy.Services.Rendering;

public sealed class KeyScopedNodeRenderer : INodeRenderer
{
    private readonly PortRenderer _portRenderer;
    private readonly IWorkflowEditorHostAccessor _hostAccessor;
    private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

    public KeyScopedNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
    {
        _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
    }

    private static bool ShouldShowPort(KeyScopedNode kn, NodePort port)
        => !port.IsInput || kn.IsWriteMode;

    public void RenderNode(WorkflowNode node, Canvas canvas)
    {
        if (node is not KeyScopedNode keyNode) return;

        keyNode.Border = KeyScopedNodeControl.CreateBorder(
            keyNode,
            Host as System.Windows.Window ?? throw new InvalidOperationException("Host must be a Window."),
            Host);
        keyNode.Border.Tag = keyNode;

        NodeChrome.Apply(keyNode.Border, keyNode, Host);

        keyNode.Border.MouseDown += Host.NodeMouseDown;
        keyNode.Border.MouseMove += Host.NodeMouseMove;
        keyNode.Border.MouseUp += Host.NodeMouseUp;
        keyNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
        keyNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
        keyNode.Border.ContextMenu = null;

        Canvas.SetLeft(keyNode.Border, keyNode.X);
        Canvas.SetTop(keyNode.Border, keyNode.Y);
        canvas.Children.Add(keyNode.Border);

        Host.ZIndexManager.InitializeNodeZIndex(keyNode, keyNode.Border);

        foreach (var port in keyNode.Ports)
        {
            var show = ShouldShowPort(keyNode, port);
            port.IsVisible = show;
            if (!show)
                continue;

            Color portColor;
            if (!string.IsNullOrWhiteSpace(port.ColorKey))
            {
                var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush")
                                   ?? GetColorFromTheme(port.ColorKey);
                portColor = colorFromKey ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
            }
            else
            {
                portColor = port.IsInput
                    ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                    : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
            }

            if (port.PortUI == null)
            {
                port.PortUI = _portRenderer.CreatePort(portColor);
                port.PortUI.Tag = port;
            }
            else if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
            {
                ellipse.Fill = new SolidColorBrush(portColor);
            }

            _portRenderer.UpdatePortsPositionOnSide(keyNode, port.Position);
            _portRenderer.EnsurePortAddedToCanvas(port);
            Host.ZIndexManager.SetPortZIndex(keyNode, port.PortUI);
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

        if (node is KeyScopedNode keyNode && keyNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
        {
            var titleTextBlock = keyNode.TitleTextBlockUI;
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

        if (node is KeyScopedNode kn)
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

                Color portColor;
                if (!string.IsNullOrWhiteSpace(port.ColorKey))
                {
                    var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush")
                                       ?? GetColorFromTheme(port.ColorKey);
                    portColor = colorFromKey ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
                }
                else
                {
                    portColor = port.IsInput
                        ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                        : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
                }

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(kn, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(kn, port.PortUI);
            }
        }

        Host.SyncAllPortsZIndex(node);
    }

    public void RemoveNode(WorkflowNode node, Canvas canvas)
    {
        if (node.Border != null && canvas.Children.Contains(node.Border))
            canvas.Children.Remove(node.Border);

        if (node is KeyScopedNode keyNode && keyNode.TitleTextBlockUI != null)
        {
            var titleTextBlock = keyNode.TitleTextBlockUI;
            if (canvas.Children.Contains(titleTextBlock))
                canvas.Children.Remove(titleTextBlock);
            keyNode.TitleTextBlockUI = null;
        }

        foreach (var port in node.Ports)
        {
            if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                canvas.Children.Remove(port.PortUI);
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
