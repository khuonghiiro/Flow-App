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
    public sealed class HttpRequestNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public HttpRequestNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not HttpRequestNode httpRequestNode) return;

            httpRequestNode.Border = HttpRequestNodeControl.CreateBorder(
                httpRequestNode,
                Host as System.Windows.Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host
            );
            httpRequestNode.Border.Tag = httpRequestNode;
            
            NodeChrome.Apply(httpRequestNode.Border, httpRequestNode, Host);

            httpRequestNode.Border.MouseDown += Host.NodeMouseDown;
            httpRequestNode.Border.MouseMove += Host.NodeMouseMove;
            httpRequestNode.Border.MouseUp += Host.NodeMouseUp;
            httpRequestNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            httpRequestNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            httpRequestNode.Border.ContextMenu = null; // Dialog opens on right-click

            Canvas.SetLeft(httpRequestNode.Border, httpRequestNode.X);
            Canvas.SetTop(httpRequestNode.Border, httpRequestNode.Y);
            canvas.Children.Add(httpRequestNode.Border);

            Host.ZIndexManager.InitializeNodeZIndex(httpRequestNode, httpRequestNode.Border);

            // ⚠️ CRITICAL: Render ports with ALWAYS update color
            foreach (var port in httpRequestNode.Ports.Where(p => p.IsVisible))
            {
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
                
                // ⚠️ CRITICAL: ALWAYS update color
                if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(httpRequestNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(httpRequestNode, port.PortUI);
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

            // ⚠️ CRITICAL: Update titleTextBlock position
            if (node is HttpRequestNode httpRequestNode && httpRequestNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var titleTextBlock = httpRequestNode.TitleTextBlockUI;
                
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

            // ⚠️ CRITICAL: Update port colors
            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
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
                else
                {
                    if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                    {
                        ellipse.Fill = new SolidColorBrush(portColor);
                    }
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node.Border != null && canvas.Children.Contains(node.Border))
            {
                canvas.Children.Remove(node.Border);
            }

            // ⚠️ CRITICAL: Remove titleTextBlock
            if (node is HttpRequestNode httpRequestNode && httpRequestNode.TitleTextBlockUI != null)
            {
                var titleTextBlock = httpRequestNode.TitleTextBlockUI;
                if (canvas != null && canvas.Children.Contains(titleTextBlock))
                {
                    canvas.Children.Remove(titleTextBlock);
                }
                httpRequestNode.TitleTextBlockUI = null;
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

