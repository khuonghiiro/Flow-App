using FlowMy.Models;
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
    public sealed class HotkeyPressEventNodeRenderer : INodeRenderer
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly PortRenderer _portRenderer;

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public HotkeyPressEventNodeRenderer(IWorkflowEditorHostAccessor hostAccessor, PortRenderer portRenderer)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not HotkeyPressEventNode hotkeyNode)
                return;

            node.Border = HotkeyPressEventNodeControl.CreateBorder(hotkeyNode, Application.Current?.MainWindow, Host);
            NodeChrome.Apply(node.Border, node, Host);
            
            // Bỏ auto-expand: chỉ mở khi user click toggle để tránh render nhiều dẫn đến đơ

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

            RenderPorts(node, canvas);
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

            // Update titleTextBlock position nếu có
            if (node is HotkeyPressEventNode hotkeyNode && hotkeyNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var titleTextBlock = hotkeyNode.TitleTextBlockUI;
                if (node.Border != null && Host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    var titleLeft = x + (node.Border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
                    var titleTop = y - titleTextBlock.ActualHeight - 4;
                    Canvas.SetLeft(titleTextBlock, titleLeft);
                    Canvas.SetTop(titleTextBlock, titleTop);
                }
            }

            RenderPorts(node, Host.WorkflowCanvas);
            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node.Border != null && canvas.Children.Contains(node.Border))
            {
                canvas.Children.Remove(node.Border);
            }

            // Remove titleTextBlock nếu có
            if (node is HotkeyPressEventNode hotkeyNode && hotkeyNode.TitleTextBlockUI != null)
            {
                var titleTextBlock = hotkeyNode.TitleTextBlockUI;
                if (canvas != null && canvas.Children.Contains(titleTextBlock))
                {
                    canvas.Children.Remove(titleTextBlock);
                }
                // Clear reference để tránh memory leak và hiển thị lại
                hotkeyNode.TitleTextBlockUI = null;
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

        private void RenderPorts(WorkflowNode node, Canvas canvas)
        {
            if (node.Border == null) return;

            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                // Port trái (input) màu khác với port phải (output) - giống MouseEventNodeRenderer
                // Ưu tiên ColorKey của port nếu có, nếu không thì dùng logic IsInput
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
                    // ⚠️ CRITICAL: LUÔN update màu để đảm bảo đúng màu (kể cả khi port đã tồn tại)
                    if (port.PortUI is Ellipse ellipse)
                    {
                        ellipse.Fill = new SolidColorBrush(portColor);
                    }
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
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

