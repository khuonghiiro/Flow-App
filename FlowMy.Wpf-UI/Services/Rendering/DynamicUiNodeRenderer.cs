// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
            dynamicUiNode.Border.Tag = dynamicUiNode;

            NodeChrome.Apply(dynamicUiNode.Border, dynamicUiNode, Host);

            dynamicUiNode.Border.MouseDown += Host.NodeMouseDown;
            dynamicUiNode.Border.MouseMove += Host.NodeMouseMove;
            dynamicUiNode.Border.MouseUp += Host.NodeMouseUp;
            dynamicUiNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            dynamicUiNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            dynamicUiNode.Border.ContextMenu = Host.CreateNodeContextMenu(node);

            Canvas.SetLeft(dynamicUiNode.Border, dynamicUiNode.X);
            Canvas.SetTop(dynamicUiNode.Border, dynamicUiNode.Y);
            canvas.Children.Add(dynamicUiNode.Border);

            Host.ZIndexManager.InitializeNodeZIndex(dynamicUiNode, dynamicUiNode.Border);

            if (dynamicUiNode.TitleTextBlockUI != null && !canvas.Children.Contains(dynamicUiNode.TitleTextBlockUI))
            {
                canvas.Children.Add(dynamicUiNode.TitleTextBlockUI);
                if (node.Border != null)
                {
                    var titleLeft = node.X + (node.Border.ActualWidth / 2) - (dynamicUiNode.TitleTextBlockUI.ActualWidth / 2);
                    var titleTop = node.Y - dynamicUiNode.TitleTextBlockUI.ActualHeight - 4;
                    Canvas.SetLeft(dynamicUiNode.TitleTextBlockUI, titleLeft);
                    Canvas.SetTop(dynamicUiNode.TitleTextBlockUI, titleTop);
                    Panel.SetZIndex(dynamicUiNode.TitleTextBlockUI, 20000);
                }
            }

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
                if (node.Border != null)
                {
                    node.Border.Width = dynamicUiNode.Width;
                    node.Border.Height = dynamicUiNode.Height;
                }

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
                        titleTextBlock.UpdateLayout();
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
            if (Host.WorkflowCanvas == null) return;

            CleanupOrphanedPortsForNode(node, Host.WorkflowCanvas);

            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                Color portColor = GetPortColor(port);

                if (port.PortUI == null)
                {
                    var margin = GetPortMarginForPosition(port.Position);
                    port.PortUI = _portRenderer.CreateRectangularPortWithMargin(portColor, margin, width: 12, height: 25);
                    port.PortUI.Tag = port;
                }
                else
                {
                    var shape = PortRenderer.GetActualPortShape(port.PortUI);
                    if (shape != null)
                        shape.Fill = new SolidColorBrush(portColor);
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
                if (canvas != null && canvas.Children.Contains(titleTextBlock))
                {
                    canvas.Children.Remove(titleTextBlock);
                }
                dynamicUiNode.TitleTextBlockUI = null;
            }

            if (node.Border != null && canvas != null && canvas.Children.Contains(node.Border))
            {
                canvas.Children.Remove(node.Border);
            }

            foreach (var port in node.Ports)
            {
                if (port?.PortUI != null && canvas != null && canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Remove(port.PortUI);
                }
            }
        }

        public void RemoveAllNodeVisuals(Canvas canvas)
        {
            var borders = canvas.Children.OfType<Border>().Where(b => b.Tag is WorkflowNode).ToList();
            foreach (var border in borders) canvas.Children.Remove(border);

            var ports = new List<UIElement>();
            
            var shapePorts = canvas.Children.OfType<Shape>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18) || (e.Width == 12 && e.Height == 25) ||
                    (e.Width == 12 && e.Height == 12) ||
                    (e.Width == 25 && e.Height == 25) ||
                    (e is Rectangle rect && rect.Tag is Size)
                ).ToList();
            ports.AddRange(shapePorts);
            
            var frameworkElementPorts = canvas.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag is NodePort && !(e is Border border && border.Tag is WorkflowNode))
                .ToList();
            ports.AddRange(frameworkElementPorts);
            
            var borderPorts = canvas.Children.OfType<Border>()
                .Where(b => 
                {
                    if (b.Tag is NodePort)
                        return true;
                    
                    if (b.Child is Rectangle rect)
                    {
                        if ((rect.Width == 12 && rect.Height == 25) ||
                            (rect.Width == 10 && rect.Height == 18) ||
                            (rect.Width == 14 && rect.Height == 27) ||
                            (rect.Width == 12 && rect.Height == 20))
                            return true;
                        
                        if (rect.Tag is Size)
                            return true;
                    }
                    
                    return false;
                })
                .ToList();
            ports.AddRange(borderPorts);
            
            foreach (var port in ports.Distinct())
            {
                if (port != null && canvas.Children.Contains(port))
                    canvas.Children.Remove(port);
            }
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

        private void CleanupOrphanedPortsForNode(WorkflowNode node, Canvas? canvas)
        {
            if (canvas == null) return;

            var orphanedPorts = new List<UIElement>();

            var allPortsOnCanvas = canvas.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag is NodePort)
                .ToList();

            foreach (var portUI in allPortsOnCanvas)
            {
                if (portUI.Tag is NodePort portTag)
                {
                    if (!node.Ports.Contains(portTag))
                        continue;

                    if (portTag.PortUI != null && !ReferenceEquals(portTag.PortUI, portUI))
                    {
                        orphanedPorts.Add(portUI);
                    }
                }
            }

            foreach (var orphanedPort in orphanedPorts.Distinct())
            {
                if (canvas.Children.Contains(orphanedPort))
                {
                    canvas.Children.Remove(orphanedPort);
                }
            }
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
