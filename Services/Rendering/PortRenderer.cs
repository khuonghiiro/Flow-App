using FlowMy.Models;
using FlowMy.Services.Interaction;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowMy.Services.Rendering;

namespace FlowMy.Services.Rendering
{
    public sealed class PortRenderer
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;

        private IWorkflowEditorHost _host => _hostAccessor.GetRequiredHost();

        public PortRenderer(IWorkflowEditorHostAccessor hostAccessor)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public Ellipse CreatePort(Color color)
        {
            var ellipse = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                IsHitTestVisible = true
            };

            ellipse.MouseDown += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseDown(ellipse, e);
            };
            ellipse.MouseUp += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseUp(ellipse, e);
            };

            return ellipse;
        }

        /// <summary>Tạo port với margin wrapper để dễ nhìn khi bị khuất.</summary>
        public FrameworkElement CreatePortWithMargin(Color color, Thickness margin, double portSize = 18)
        {
            var ellipse = new Ellipse
            {
                Width = portSize,
                Height = portSize,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                IsHitTestVisible = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            ellipse.MouseDown += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseDown(ellipse, e);
            };
            ellipse.MouseUp += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseUp(ellipse, e);
            };

            // Wrap trong Border với margin và background nhạt để dễ nhìn khi bị khuất
            var wrapper = new Border
            {
                Margin = margin,
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), // Background nhạt để dễ nhìn
                CornerRadius = new CornerRadius(portSize / 2), // Bo góc theo port size
                Child = ellipse,
                IsHitTestVisible = false // Chỉ ellipse nhận hit test
            };

            return wrapper;
        }

        /// <summary>Tạo port hình chữ nhật dọc (chiều cao > chiều rộng) cho WebNode, HtmlUiNode.</summary>
        public Rectangle CreateRectangularPort(Color color)
        {
            const double width = 10;
            const double height = 18;

            var rect = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                IsHitTestVisible = true,
                RadiusX = 2,
                RadiusY = 2
            };

            // Lưu kích thước mặc định vào Tag của Rectangle để có thể reset đúng size sau khi highlight
            rect.Tag = new Size(width, height);

            rect.MouseDown += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseDown(rect, e);
            };
            rect.MouseUp += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseUp(rect, e);
            };

            return rect;
        }

        /// <summary>Tạo port hình chữ nhật dọc với margin wrapper để dễ nhìn khi bị khuất.</summary>
        public FrameworkElement CreateRectangularPortWithMargin(Color color, Thickness margin, double width = 10, double height = 18)
        {
            var rect = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                IsHitTestVisible = true,
                RadiusX = 2,
                RadiusY = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Lưu kích thước mặc định vào Tag của Rectangle để có thể reset đúng size sau khi highlight
            rect.Tag = new Size(width, height);

            rect.MouseDown += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseDown(rect, e);
            };
            rect.MouseUp += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseUp(rect, e);
            };

            // Wrap trong Border với margin và background nhạt để dễ nhìn khi bị khuất
            var wrapper = new Border
            {
                Margin = margin,
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), // Background nhạt để dễ nhìn
                CornerRadius = new CornerRadius(2), // Bo góc nhẹ
                Child = rect,
                IsHitTestVisible = true, // ✅ Cho phép Border nhận hit test để dễ kết nối hơn
                ClipToBounds = false // ✅ Không clip để port có thể enlarge khi highlight
            };
            
            // ✅ Forward mouse events từ Border đến Rectangle để đảm bảo PortMouseDown/Up vẫn hoạt động
            wrapper.MouseDown += (s, e) =>
            {
                // Forward event đến Rectangle bên trong
                rect.RaiseEvent(e);
            };
            wrapper.MouseUp += (s, e) =>
            {
                // Forward event đến Rectangle bên trong
                rect.RaiseEvent(e);
            };

            return wrapper;
        }

        public void UpdatePortsPositionOnSide(WorkflowNode node, PortPosition position)
        {
            if (node.Border == null) return;

            var border = node.Border;
            var nodeX = node.X;
            var nodeY = node.Y;
            var nodeWidth = border.ActualWidth > 0 ? border.ActualWidth : border.Width;
            var nodeHeight = border.ActualHeight > 0 ? border.ActualHeight : border.Height;

            var portsOnSide = node.Ports.Where(p => p.Position == position && p.IsVisible).ToList();
            if (!portsOnSide.Any()) return;

            // Lấy kích thước port để căn giữa: Ellipse 18x18 → radius 9; Rectangle 10x18 → halfW 5, halfH 9
            // Hỗ trợ cả port trực tiếp và port có wrapper Border
            static (double halfW, double halfH, Thickness margin) GetPortSizeAndMargin(FrameworkElement? ui)
            {
                if (ui == null) return (9, 9, new Thickness(0));
                
                Thickness margin = new Thickness(0);
                FrameworkElement? actualPort = ui;
                
                // Nếu là Border wrapper, lấy child và margin
                if (ui is Border border && border.Child is FrameworkElement child)
                {
                    actualPort = child;
                    margin = border.Margin;
                }
                
                var w = actualPort.Width > 0 ? actualPort.Width : 18;
                var h = actualPort.Height > 0 ? actualPort.Height : 18;
                
                // Tính toán với margin: wrapper size = port size + margin
                var totalW = w + margin.Left + margin.Right;
                var totalH = h + margin.Top + margin.Bottom;
                
                return (totalW / 2, totalH / 2, margin);
            }
            // Offset để điều chỉnh vị trí port: input ra ngoài hơn, output vào trong hơn
            const double inputPortOffset = 1.0; // Đẩy input ports ra ngoài (Left)
            const double outputPortOffset = 1.0;  // Đẩy output ports vào trong (Right/Top)

            switch (position)
            {
                case PortPosition.Left:
                    {
                        var spacing = nodeHeight / (portsOnSide.Count + 1);
                        for (int i = 0; i < portsOnSide.Count; i++)
                        {
                            var port = portsOnSide[i];
                            var (halfW, halfH, margin) = GetPortSizeAndMargin(port.PortUI);
                            var (insetX, insetY) = GetStartEndInset(node, port, position);
                            var yOffset = spacing * (i + 1);
                            var point = new Point(nodeX + inputPortOffset + insetX, nodeY + yOffset);
                            if (TryGetDiamondPortPoint(node, port, position, nodeX, nodeY, nodeWidth, nodeHeight, insetX, insetY, out var diamondPoint))
                                point = diamondPoint;
                            port.PositionPoint = point;

                            if (port.PortUI != null)
                            {
                                Canvas.SetLeft(port.PortUI, port.PositionPoint.X - halfW);
                                Canvas.SetTop(port.PortUI, port.PositionPoint.Y - halfH);
                                
                                // Invalidate port để tránh ghost effects và đảm bảo vị trí chính xác khi dùng GPU
                                var portShape = GetActualPortShape(port.PortUI);
                                if (portShape != null)
                                {
                                    GpuOptimizationHelper.InvalidatePortShape(portShape);
                                }
                                else
                                {
                                    port.PortUI.InvalidateVisual();
                                }
                            }
                        }
                        break;
                    }

                case PortPosition.Right:
                    {
                        var spacing = nodeHeight / (portsOnSide.Count + 1);
                        for (int i = 0; i < portsOnSide.Count; i++)
                        {
                            var port = portsOnSide[i];
                            var (halfW, halfH, margin) = GetPortSizeAndMargin(port.PortUI);
                            var (insetX, insetY) = GetStartEndInset(node, port, position);
                            var yOffset = spacing * (i + 1);
                            var point = new Point(nodeX + nodeWidth - outputPortOffset + insetX, nodeY + yOffset);
                            if (TryGetDiamondPortPoint(node, port, position, nodeX, nodeY, nodeWidth, nodeHeight, insetX, insetY, out var diamondPoint))
                                point = diamondPoint;
                            port.PositionPoint = point;

                            if (port.PortUI != null)
                            {
                                Canvas.SetLeft(port.PortUI, port.PositionPoint.X - halfW);
                                Canvas.SetTop(port.PortUI, port.PositionPoint.Y - halfH);
                                
                                // Invalidate port để tránh ghost effects và đảm bảo vị trí chính xác khi dùng GPU
                                var portShape = GetActualPortShape(port.PortUI);
                                if (portShape != null)
                                {
                                    GpuOptimizationHelper.InvalidatePortShape(portShape);
                                }
                                else
                                {
                                    port.PortUI.InvalidateVisual();
                                }
                            }
                        }
                        break;
                    }

                case PortPosition.Top:
                    {
                        var spacing = nodeWidth / (portsOnSide.Count + 1);
                        for (int i = 0; i < portsOnSide.Count; i++)
                        {
                            var port = portsOnSide[i];
                            var (halfW, halfH, margin) = GetPortSizeAndMargin(port.PortUI);
                            var (insetX, insetY) = GetStartEndInset(node, port, position);
                            var xOffset = spacing * (i + 1);
                            var point = new Point(nodeX + xOffset, nodeY + outputPortOffset + insetY);
                            if (TryGetDiamondPortPoint(node, port, position, nodeX, nodeY, nodeWidth, nodeHeight, insetX, insetY, out var diamondPoint))
                                point = diamondPoint;
                            port.PositionPoint = point;

                            if (port.PortUI != null)
                            {
                                Canvas.SetLeft(port.PortUI, port.PositionPoint.X - halfW);
                                Canvas.SetTop(port.PortUI, port.PositionPoint.Y - halfH);
                                
                                // Invalidate port để tránh ghost effects và đảm bảo vị trí chính xác khi dùng GPU
                                var portShape = GetActualPortShape(port.PortUI);
                                if (portShape != null)
                                {
                                    GpuOptimizationHelper.InvalidatePortShape(portShape);
                                }
                                else
                                {
                                    port.PortUI.InvalidateVisual();
                                }
                            }
                        }
                        break;
                    }

                case PortPosition.Bottom:
                    {
                        var spacing = nodeWidth / (portsOnSide.Count + 1);
                        for (int i = 0; i < portsOnSide.Count; i++)
                        {
                            var port = portsOnSide[i];
                            var (halfW, halfH, margin) = GetPortSizeAndMargin(port.PortUI);
                            var (insetX, insetY) = GetStartEndInset(node, port, position);
                            var xOffset = spacing * (i + 1);
                            var point = new Point(nodeX + xOffset, nodeY + nodeHeight + insetY);
                            if (TryGetDiamondPortPoint(node, port, position, nodeX, nodeY, nodeWidth, nodeHeight, insetX, insetY, out var diamondPoint))
                                point = diamondPoint;
                            port.PositionPoint = point;

                            if (port.PortUI != null)
                            {
                                Canvas.SetLeft(port.PortUI, port.PositionPoint.X - halfW);
                                Canvas.SetTop(port.PortUI, port.PositionPoint.Y - halfH);
                                
                                // Invalidate port để tránh ghost effects và đảm bảo vị trí chính xác khi dùng GPU
                                var portShape = GetActualPortShape(port.PortUI);
                                if (portShape != null)
                                {
                                    GpuOptimizationHelper.InvalidatePortShape(portShape);
                                }
                                else
                                {
                                    port.PortUI.InvalidateVisual();
                                }
                            }
                        }
                        break;
                    }
            }
        }

        private static bool TryGetDiamondPortPoint(
            WorkflowNode node,
            NodePort port,
            PortPosition side,
            double nodeX,
            double nodeY,
            double nodeWidth,
            double nodeHeight,
            double insetX,
            double insetY,
            out Point point)
        {
            bool isStartDiamond = node.IsStartDiamondVisual;
            bool isEndDiamond = node.Type == NodeType.End && node.EndBehavior == EndNodeBehavior.ReturnToParent;
            if (!isStartDiamond && !isEndDiamond)
            {
                point = default;
                return false;
            }

            var baseSize = node.Type == NodeType.Start
                ? FlowMy.Views.NodeControls.StartNodeControl.NodeSize
                : FlowMy.Views.NodeControls.EndNodeControl.NodeSize;

            var sy = node.DiamondSharpness switch
            {
                DiamondSharpness.Soft => 0.88,
                DiamondSharpness.Medium => 1.0,
                DiamondSharpness.Sharp => 1.15,
                _ => 1.0
            };

            var tipOffset = baseSize * (0.3535533905932738 * (1d + sy) - 0.5d);
            const double protrude = 8d;
            var centerX = nodeX + (nodeWidth / 2d);
            var centerY = nodeY + (nodeHeight / 2d);

            point = side switch
            {
                PortPosition.Left => new Point(nodeX - tipOffset - protrude + insetX, centerY + insetY),
                PortPosition.Right => new Point(nodeX + nodeWidth + tipOffset + protrude + insetX, centerY + insetY),
                PortPosition.Top => new Point(centerX + insetX, nodeY - tipOffset - protrude + insetY),
                PortPosition.Bottom => new Point(centerX + insetX, nodeY + nodeHeight + tipOffset + protrude + insetY),
                _ => new Point(centerX, centerY)
            };
            return true;
        }

        private static (double insetX, double insetY) GetStartEndInset(WorkflowNode node, NodePort port, PortPosition side)
        {
            if (node.Type != NodeType.Start && node.Type != NodeType.End)
            {
                return (0d, 0d);
            }

            // Chỉ áp dụng inset khi node đang ở shape hình thoi.
            var isStartDiamond = node.IsStartDiamondVisual;
            var isEndDiamond = node.Type == NodeType.End && node.EndBehavior == EndNodeBehavior.ReturnToParent;
            if (!isStartDiamond && !isEndDiamond)
            {
                return (0d, 0d);
            }

            const double inset = 12d;
            return side switch
            {
                PortPosition.Left => (inset, 0d),
                PortPosition.Right => (-inset, 0d),
                PortPosition.Top => (0d, inset),
                PortPosition.Bottom => (0d, -inset),
                _ => (0d, 0d)
            };
        }

        public void EnsurePortAddedToCanvas(NodePort port)
        {
            if (port.PortUI == null) return;

            // Critical: tag the UI element so cleanup routines can reliably remove it from canvas.
            // Without this, RemoveAllNodeVisuals() won't find orphaned port ellipses.
            port.PortUI.Tag = port;

            if (!_host.WorkflowCanvas.Children.Contains(port.PortUI))
            {
                _host.WorkflowCanvas.Children.Add(port.PortUI);
            }
        }

        public void TogglePortsVisibility(WorkflowNode node)
        {
            bool anyVisible = node.Ports.Any(p => p.IsVisible);
            foreach (var port in node.Ports)
            {
                port.IsVisible = !anyVisible;
                if (port.PortUI != null)
                {
                    port.PortUI.Visibility = port.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        /// <summary>Lấy shape thực tế từ port UI (có thể là wrapper Border hoặc shape trực tiếp).</summary>
        public static Shape? GetActualPortShape(FrameworkElement? portUI)
        {
            if (portUI == null) return null;
            
            if (portUI is Shape shape)
                return shape;
            
            if (portUI is Border border && border.Child is Shape childShape)
                return childShape;
            
            return null;
        }
    }
}

