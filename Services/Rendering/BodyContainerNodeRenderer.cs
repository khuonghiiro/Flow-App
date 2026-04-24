using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using FlowMy.Views.Overlays;
using FlowMy.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering;

public sealed class BodyContainerNodeRenderer : INodeRenderer
{
    private readonly IWorkflowEditorHostAccessor _hostAccessor;
    private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

    public BodyContainerNodeRenderer(IWorkflowEditorHostAccessor hostAccessor)
    {
        _hostAccessor = hostAccessor;
    }

    public void RenderNode(WorkflowNode node, Canvas canvas)
    {
        if (node is not BodyContainerNode bodyNode)
            throw new InvalidOperationException("BodyContainerNodeRenderer can only render BodyContainerNode.");

        var border = BodyContainerControl.CreateBorder(bodyNode);
        bodyNode.Border = border;
        NodeChrome.Apply(border, bodyNode, Host);
        border.ContextMenu = Host.CreateNodeContextMenu(bodyNode);
        AttachBodyDragHandlers(bodyNode, border);
        AttachHoverTitleBehavior(bodyNode, border);

        Canvas.SetLeft(border, bodyNode.X);
        Canvas.SetTop(border, bodyNode.Y);
        canvas.Children.Add(border);
        Host.ZIndexManager.InitializeNodeZIndex(bodyNode, border);

        if (bodyNode is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += (_, _) => RefreshNodeVisual(bodyNode);
        }
        RefreshNodeVisual(bodyNode);
    }

    public void UpdateNodePosition(WorkflowNode node, double x, double y)
    {
        if (node is not BodyContainerNode bodyNode) return;
        bodyNode.X = x;
        bodyNode.Y = y;
        if (bodyNode.Border != null)
        {
            Canvas.SetLeft(bodyNode.Border, x);
            Canvas.SetTop(bodyNode.Border, y);
        }
    }

    public void RemoveNode(WorkflowNode node, Canvas canvas)
    {
        if (node.Border != null && canvas.Children.Contains(node.Border))
            canvas.Children.Remove(node.Border);
    }

    public void RemoveAllNodeVisuals(Canvas canvas)
    {
        var borders = canvas.Children.OfType<Border>().Where(b => b.Tag is BodyContainerNode).ToList();
        foreach (var border in borders)
            canvas.Children.Remove(border);
    }

    private void RefreshNodeVisual(BodyContainerNode node)
    {
        if (node.Border?.Child is not Grid grid) return;
        if (grid.Children.Count < 4) return;
        if (grid.Children[0] is not Rectangle fillRect) return;
        if (grid.Children[1] is not Rectangle borderRect) return;
        if (grid.Children[2] is not TextBlock titleText) return;
        if (grid.Children[3] is not FlowMy.Controls.SvgViewboxEx lockIcon) return;
        BodyContainerControl.ApplyNodeVisual(node, node.Border, fillRect, borderRect, titleText, lockIcon);
    }

    private void AttachBodyDragHandlers(BodyContainerNode bodyNode, Border border)
    {
        var dragging = false;
        var startPoint = new Point();
        var origin = new Point();
        List<WorkflowNode>? nodesInside = null;

        border.PreviewMouseDown += (_, e) =>
        {
            if (e.OriginalSource is Ellipse) return;
            if (e.ChangedButton == System.Windows.Input.MouseButton.Right)
            {
                OpenNodeDialog(bodyNode);
                e.Handled = true;
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed) return;
            dragging = true;
            startPoint = e.GetPosition(Host.WorkflowCanvas);
            origin = new Point(bodyNode.X, bodyNode.Y);
            nodesInside = bodyNode.LockInnerNodes ? CaptureNodesInsideBody(bodyNode) : null;
            border.CaptureMouse();
            Host.ZIndexManager.DragNode(bodyNode);
            e.Handled = true;
        };

        border.PreviewMouseMove += (_, e) =>
        {
            if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
            var current = e.GetPosition(Host.WorkflowCanvas);
            var dx = current.X - startPoint.X;
            var dy = current.Y - startPoint.Y;

            bodyNode.X = origin.X + dx;
            bodyNode.Y = origin.Y + dy;
            Canvas.SetLeft(border, bodyNode.X);
            Canvas.SetTop(border, bodyNode.Y);

            if (nodesInside != null && Host.ViewModel != null)
            {
                foreach (var innerNode in nodesInside)
                {
                    Host.UpdateNodePosition(innerNode, innerNode.X + dx, innerNode.Y + dy);
                    foreach (var conn in Host.ViewModel.Connections.Where(c => c.FromNode == innerNode || c.ToNode == innerNode))
                        Host.UpdateConnectionPath(conn);
                }
            }

            if (Host.ViewModel != null)
            {
                foreach (var conn in Host.ViewModel.Connections.Where(c => c.FromNode == bodyNode || c.ToNode == bodyNode))
                    Host.UpdateConnectionPath(conn);
            }
            Host.UpdateMinimap();
            origin = new Point(bodyNode.X, bodyNode.Y);
            startPoint = current;
            e.Handled = true;
        };

        border.PreviewMouseUp += (_, e) =>
        {
            if (!dragging) return;
            dragging = false;
            nodesInside = null;
            border.ReleaseMouseCapture();
            Host.ZIndexManager.RestoreNodeZIndex(bodyNode);
            e.Handled = true;
        };
    }

    private List<WorkflowNode> CaptureNodesInsideBody(BodyContainerNode bodyNode)
    {
        var result = new List<WorkflowNode>();
        var vm = Host.ViewModel;
        if (vm == null) return result;

        var bounds = new Rect(bodyNode.X, bodyNode.Y, bodyNode.BodyWidth, bodyNode.BodyHeight);
        foreach (var node in vm.Nodes)
        {
            if (ReferenceEquals(node, bodyNode)) continue;
            if (node is LoopBodyNode or AsyncTaskBodyNode) continue;
            var cx = node.X + (node.Border?.ActualWidth > 1 ? node.Border.ActualWidth / 2 : 75);
            var cy = node.Y + (node.Border?.ActualHeight > 1 ? node.Border.ActualHeight / 2 : 40);
            if (bounds.Contains(new Point(cx, cy)))
                result.Add(node);
        }

        return result;
    }

    private void OpenNodeDialog(BodyContainerNode node)
    {
        if (Host.OwnerWindow is not WorkflowEditorWindow window) return;
        var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(window) is not NodeDialogManager dialogManager) return;

        if (node.Border?.IsMouseCaptured == true)
            node.Border.ReleaseMouseCapture();
        Host.DraggedNode = null;
        if (Host.ViewModel != null) Host.ViewModel.SelectedNode = null;

        if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
        if (dialogManager.IsDialogOpen) dialogManager.CloseCurrentDialog();

        var dialog = new BodyContainerNodeDialog(node, Host, Host.OwnerWindow);
        dialogManager.OpenDialog(node, dialog, Host);
    }

    private void AttachHoverTitleBehavior(BodyContainerNode bodyNode, Border border)
    {
        if (border.Child is not Grid grid) return;
        if (grid.Children.Count < 3) return;
        if (grid.Children[2] is not TextBlock titleText) return;

        var hovering = false;
        BodyContainerControl.UpdateTitleVisibility(bodyNode, titleText, hovering);

        border.MouseEnter += (_, _) =>
        {
            hovering = true;
            BodyContainerControl.UpdateTitleVisibility(bodyNode, titleText, hovering);
        };
        border.MouseLeave += (_, _) =>
        {
            hovering = false;
            BodyContainerControl.UpdateTitleVisibility(bodyNode, titleText, hovering);
        };

        if (bodyNode is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BodyContainerNode.TitleDisplayMode))
                    BodyContainerControl.UpdateTitleVisibility(bodyNode, titleText, hovering);
            };
        }
    }
}
