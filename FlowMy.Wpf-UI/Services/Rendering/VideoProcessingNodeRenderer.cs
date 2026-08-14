// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            if (node is VideoProcessingNode vpNode &&
                vpNode.TitleTextBlockUI != null &&
                Host.WorkflowCanvas != null)
            {
                var tb = vpNode.TitleTextBlockUI;
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
            RenderPorts(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node == null || canvas == null) return;

            // 1. Release mouse capture if mouse is captured by node, border, or any child control
            if (Mouse.Captured is DependencyObject captured)
            {
                if (IsDescendantOf(captured, node.Border) || node.Ports.Any(p => p.PortUI != null && IsDescendantOf(captured, p.PortUI)))
                {
                    (captured as FrameworkElement)?.ReleaseMouseCapture();
                }
            }
            try { node.Border?.ReleaseMouseCapture(); } catch { }

            // 2. Remove Title
            if (node is VideoProcessingNode vpNode && vpNode.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(vpNode.TitleTextBlockUI))
                    canvas.Children.Remove(vpNode.TitleTextBlockUI);
                vpNode.TitleTextBlockUI = null;
            }

            // 3. Remove Border
            if (node.Border != null)
            {
                if (canvas.Children.Contains(node.Border))
                    canvas.Children.Remove(node.Border);
                node.Border = null;
            }

            // 4. Remove Ports
            foreach (var port in node.Ports)
            {
                if (port.PortUI != null)
                {
                    if (canvas.Children.Contains(port.PortUI))
                    {
                        canvas.Children.Remove(port.PortUI);
                    }
                    else if (port.PortUI.Parent is Panel parentPanel)
                    {
                        parentPanel.Children.Remove(port.PortUI);
                    }
                    port.PortUI = null;
                }
            }

            // 5. Clean up any remaining orphaned elements on canvas tagged with port or node
            var orphans = canvas.Children.OfType<FrameworkElement>()
                .Where(el => el.Tag == node || node.Ports.Any(p => p == el.Tag))
                .ToList();
            foreach (var orphan in orphans)
            {
                canvas.Children.Remove(orphan);
            }
        }

        private static bool IsDescendantOf(DependencyObject? child, DependencyObject? parent)
        {
            if (child == null || parent == null) return false;
            while (child != null)
            {
                if (child == parent) return true;
                child = VisualTreeHelper.GetParent(child) ?? (child as FrameworkElement)?.Parent;
            }
            return false;
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
