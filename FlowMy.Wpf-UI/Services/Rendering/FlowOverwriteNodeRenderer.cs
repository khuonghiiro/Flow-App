using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Services.Rendering;

public sealed class FlowOverwriteNodeRenderer : INodeRenderer
{
    private readonly PortRenderer _portRenderer;
    private readonly IWorkflowEditorHostAccessor _hostAccessor;
    private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

    public FlowOverwriteNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
    {
        _portRenderer = portRenderer;
        _hostAccessor = hostAccessor;
    }

    public void RenderNode(WorkflowNode node, Canvas canvas)
    {
        if (node is not FlowOverwriteNode typed) return;
        typed.Border = FlowOverwriteNodeControl.CreateBorder(typed, Host as Window, Host);
        NodeChrome.Apply(typed.Border, typed, Host);
        typed.Border.MouseDown += Host.NodeMouseDown;
        typed.Border.MouseMove += Host.NodeMouseMove;
        typed.Border.MouseUp += Host.NodeMouseUp;
        typed.Border.MouseEnter += Host.NodeBorderMouseEnter;
        typed.Border.MouseLeave += Host.NodeBorderMouseLeave;
        typed.Border.ContextMenu = Host.CreateNodeContextMenu(node);

        Canvas.SetLeft(typed.Border, typed.X);
        Canvas.SetTop(typed.Border, typed.Y);
        canvas.Children.Add(typed.Border);
        Host.ZIndexManager.InitializeNodeZIndex(typed, typed.Border);
        RenderPorts(typed);
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

        if (node is FlowOverwriteNode flowOverwriteNode &&
            flowOverwriteNode.TitleTextBlockUI != null &&
            Host.WorkflowCanvas != null)
        {
            var title = flowOverwriteNode.TitleTextBlockUI;
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

        RenderPorts(node);
        Host.SyncAllPortsZIndex(node);
    }

    public void RemoveNode(WorkflowNode node, Canvas canvas)
    {
        if (node.Border != null && canvas.Children.Contains(node.Border))
            canvas.Children.Remove(node.Border);
        if (node is FlowOverwriteNode n && n.TitleTextBlockUI != null && canvas.Children.Contains(n.TitleTextBlockUI))
            canvas.Children.Remove(n.TitleTextBlockUI);
        foreach (var port in node.Ports)
            if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                canvas.Children.Remove(port.PortUI);
    }

    public void RemoveAllNodeVisuals(Canvas canvas) { }

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
            var fromKey = GetColor($"{port.ColorKey}Brush") ?? GetColor(port.ColorKey);
            if (fromKey.HasValue) return fromKey.Value;
        }
        return port.IsInput ? (GetColor("InfoBrush") ?? Colors.Orange) : (GetColor("SunsetOrangeBrush") ?? Colors.Cyan);
    }

    private static Color? GetColor(string key)
        => (Application.Current.TryFindResource(key) as SolidColorBrush)?.Color;
}
