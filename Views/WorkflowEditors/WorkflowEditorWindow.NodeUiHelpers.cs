using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Views.NodeControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private Color? GetColorFromTheme(string resourceKey)
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

        private Brush? GetBrushFromTheme(string resourceKey)
        {
            try
            {
                return Application.Current.TryFindResource(resourceKey) as Brush;
            }
            catch
            {
                return null;
            }
        }

        private void NodeBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.Tag is WorkflowNode node)
            {
                // ⚠️ Skip hover effect for LoopNode (has custom diamond shape)
                if (node is LoopNode) return;

                if (node is AsyncTaskNode at && at.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
                    return;

                // ⚠️ Skip hover for ConditionalNode Diamond mode (transparent border like LoopNode)
                if (node.IsConditionalNode && node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                    return;

                // ✅ Update title position for Start/End nodes on hover (đảm bảo title luôn đúng vị trí)
                if (node.Type == NodeType.Start && node.TitleTextBlockUI != null)
                {
                    StartNodeControl.UpdateTitlePosition(node, WorkflowCanvas);
                }
                else if (node.Type == NodeType.End && node.TitleTextBlockUI != null)
                {
                    EndNodeControl.UpdateTitlePosition(node, WorkflowCanvas);
                }

                if (_draggedNode != node && !string.IsNullOrEmpty(node.ColorKey))
                {
                    var hoverBrush = Application.Current.TryFindResource($"{node.ColorKey}HoverBrush") as Brush;
                    if (hoverBrush != null)
                    {
                        border.Background = hoverBrush;
                    }
                }
            }
        }

        private void NodeBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.Tag is WorkflowNode node)
            {
                // ⚠️ Skip hover effect for LoopNode (has custom diamond shape)
                if (node is LoopNode) return;

                if (node is AsyncTaskNode at && at.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
                    return;

                // ⚠️ Skip hover for ConditionalNode Diamond mode (transparent border like LoopNode)
                if (node.IsConditionalNode && node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                    return;

                if (_draggedNode != node)
                {
                    border.Background = node.NodeBrush;
                }
            }
        }

        private ContextMenu CreateNodeContextMenu(WorkflowNode node)
        {
            var contextMenu = new ContextMenu();

            // Context menu cho node hiện đang rỗng (các menu port cũ bên dưới đang disabled).
            // Cấu hình Floating Widget đã được chuyển sang nút dành riêng cạnh
            // CanvasSettingsPopupButton (xem FloatingWidgetConfigDialog).
            return contextMenu;

            // Start và End nodes không có context menu (hoặc chỉ có menu rỗng)
            if (node.Type == NodeType.Start || node.Type == NodeType.End)
            {
                return contextMenu; // Trả về menu rỗng
            }

            var addInputMenu = new MenuItem { Header = "Thêm Input Port" };
            addInputMenu.Items.Add(CreateAddPortMenuItem("Ở trên", node, isOutput: false, PortPosition.Top));
            addInputMenu.Items.Add(CreateAddPortMenuItem("Ở dưới", node, isOutput: false, PortPosition.Bottom));
            addInputMenu.Items.Add(CreateAddPortMenuItem("Ở trái", node, isOutput: false, PortPosition.Left));
            addInputMenu.Items.Add(CreateAddPortMenuItem("Ở phải", node, isOutput: false, PortPosition.Right));

            var addOutputMenu = new MenuItem { Header = "Thêm Output Port" };
            addOutputMenu.Items.Add(CreateAddPortMenuItem("Ở trên", node, isOutput: true, PortPosition.Top));
            addOutputMenu.Items.Add(CreateAddPortMenuItem("Ở dưới", node, isOutput: true, PortPosition.Bottom));
            addOutputMenu.Items.Add(CreateAddPortMenuItem("Ở trái", node, isOutput: true, PortPosition.Left));
            addOutputMenu.Items.Add(CreateAddPortMenuItem("Ở phải", node, isOutput: true, PortPosition.Right));

            var removePortsMenu = new MenuItem { Header = "Xóa Ports" };
            if (node.Ports != null && node.Ports.Any(p => p.IsVisible))
            {
                foreach (var port in node.Ports.Where(p => p.IsVisible))
                {
                    var portType = port.IsInput ? "Input" : "Output";
                    var portPos = port.Position.ToString();
                    var removeItem = new MenuItem
                    {
                        Header = $"Xóa {portType} ({portPos})",
                        Tag = port
                    };
                    removeItem.Click += (s, e) => RemovePort(node, port);
                    removePortsMenu.Items.Add(removeItem);
                }
            }
            else
            {
                removePortsMenu.Items.Add(new MenuItem { Header = "Không có port nào", IsEnabled = false });
            }

            var togglePortsMenu = new MenuItem { Header = "Ẩn/Hiện Ports" };
            var toggleItem = new MenuItem { Header = node.Ports.Any(p => p.IsVisible) ? "Ẩn tất cả" : "Hiện tất cả" };
            toggleItem.Click += (s, e) => TogglePortsVisibility(node);
            togglePortsMenu.Items.Add(toggleItem);

            contextMenu.Items.Add(addInputMenu);
            contextMenu.Items.Add(addOutputMenu);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(removePortsMenu);
            contextMenu.Items.Add(togglePortsMenu);

            return contextMenu;
        }

        private MenuItem CreateAddPortMenuItem(string header, WorkflowNode node, bool isOutput, PortPosition position)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => AddPort(node, isOutput, position);
            return item;
        }

        private void AddPort(WorkflowNode node, bool isOutput, PortPosition position)
        {
            if (_portRenderer == null) return;

            var newPort = new NodePort
            {
                IsInput = !isOutput,
                Position = position,
                IsVisible = true
            };
            node.Ports.Add(newPort);

            var portColor = !isOutput
                ? (GetColorFromTheme("CoralBrush") ?? Colors.Orange)
                : (GetColorFromTheme("InfoBrush") ?? Colors.Cyan);

            newPort.PortUI = _portRenderer.CreatePort(portColor);
            _portRenderer.EnsurePortAddedToCanvas(newPort);

            _portRenderer.UpdatePortsPositionOnSide(node, position);
            _zIndexManager.SetPortZIndex(node, newPort.PortUI);
        }

        private void RemovePort(WorkflowNode node, NodePort port)
        {
            if (ViewModel != null)
            {
                var connectionsToRemove = ViewModel.Connections
                    .Where(c => c.FromPort == port || c.ToPort == port)
                    .ToList();

                foreach (var conn in connectionsToRemove)
                {
                    DeleteConnection(conn);
                }
            }

            if (port.PortUI != null && WorkflowCanvas.Children.Contains(port.PortUI))
            {
                WorkflowCanvas.Children.Remove(port.PortUI);
            }

            var removedPosition = port.Position;
            node.Ports.Remove(port);

            _portRenderer?.UpdatePortsPositionOnSide(node, removedPosition);
        }
    }
}

