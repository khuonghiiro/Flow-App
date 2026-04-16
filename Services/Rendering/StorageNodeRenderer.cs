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
    public sealed class StorageNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public StorageNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not StorageNode storageNode) return;

            storageNode.Border = StorageNodeControl.CreateBorder(
                storageNode,
                Host as Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host);

            NodeChrome.Apply(storageNode.Border, storageNode, Host);

            storageNode.Border.MouseDown += Host.NodeMouseDown;
            storageNode.Border.MouseMove += Host.NodeMouseMove;
            storageNode.Border.MouseUp += Host.NodeMouseUp;
            storageNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            storageNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            storageNode.Border.ContextMenu = null; // dùng right-click để mở dialog

            Canvas.SetLeft(storageNode.Border, storageNode.X);
            Canvas.SetTop(storageNode.Border, storageNode.Y);
            canvas.Children.Add(storageNode.Border);

            Host.ZIndexManager.InitializeNodeZIndex(storageNode, storageNode.Border);

            // Render ports (luôn update màu và visibility dựa trên IsInputMode)
            foreach (var port in storageNode.Ports)
            {
                // Show/hide ports dựa trên IsInputMode
                bool shouldShowPort = storageNode.IsInputMode 
                    ? port.IsInput  // IsInputMode = true: chỉ hiện port IN
                    : !port.IsInput; // IsInputMode = false: chỉ hiện port OUT
                
                port.IsVisible = shouldShowPort;
                
                if (!port.IsVisible)
                {
                    // KHÔNG render port này - skip luôn
                    continue;
                }
                
                // Chỉ render port nếu IsVisible = true
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
                    // ⚠️ CRITICAL: ALWAYS update color (theo guideline)
                    if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                    {
                        ellipse.Fill = new SolidColorBrush(portColor);
                    }
                }

                _portRenderer.UpdatePortsPositionOnSide(storageNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(storageNode, port.PortUI);
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

            if (node is StorageNode storageNode)
            {
                // Update title position
                if (storageNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
                {
                    var titleTextBlock = storageNode.TitleTextBlockUI;
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

                // Update port visibility dựa trên IsInputMode
                System.Diagnostics.Debug.WriteLine($"[StorageNodeRenderer] UpdateNodePosition - IsInputMode: {storageNode.IsInputMode}");
                foreach (var port in storageNode.Ports)
                {
                    bool shouldShowPort = storageNode.IsInputMode 
                        ? port.IsInput  // IsInputMode = true: chỉ hiện port IN
                        : !port.IsInput; // IsInputMode = false: chỉ hiện port OUT
                    
                    // ✅ Đồng bộ port.IsVisible với shouldShowPort
                    port.IsVisible = shouldShowPort;
                    
                    System.Diagnostics.Debug.WriteLine($"[StorageNodeRenderer] Port IsInput={port.IsInput}, IsVisible={port.IsVisible}, shouldShowPort={shouldShowPort}, PortUI={port.PortUI != null}");
                    
                    if (!shouldShowPort)
                    {
                        // XÓA port UI khỏi canvas VÀ clear reference
                        if (port.PortUI != null && Host.WorkflowCanvas != null)
                        {
                            if (Host.WorkflowCanvas.Children.Contains(port.PortUI))
                            {
                                Host.WorkflowCanvas.Children.Remove(port.PortUI);
                            }
                            port.PortUI = null; // ✅ Clear reference để force tạo mới khi cần
                        }
                        continue;
                    }
                    
                    // Hiện port UI - đảm bảo port được tạo và add vào canvas
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

                    // ✅ Luôn tạo port UI mới nếu chưa có hoặc đã bị remove
                    if (port.PortUI == null)
                    {
                        port.PortUI = _portRenderer.CreatePort(portColor);
                        port.PortUI.Tag = port;
                    }
                    else if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                    {
                        ellipse.Fill = new SolidColorBrush(portColor);
                    }

                    // ✅ Update vị trí port theo node position
                    _portRenderer.UpdatePortsPositionOnSide(storageNode, port.Position);
                    _portRenderer.EnsurePortAddedToCanvas(port);
                    Host.ZIndexManager.SetPortZIndex(storageNode, port.PortUI);
                }
            }
            else
            {
                // Logic cũ cho các node khác
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

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node is StorageNode storageNode && storageNode.TitleTextBlockUI != null)
            {
                var titleTextBlock = storageNode.TitleTextBlockUI;
                if (canvas.Children.Contains(titleTextBlock))
                {
                    canvas.Children.Remove(titleTextBlock);
                }
                storageNode.TitleTextBlockUI = null;
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

