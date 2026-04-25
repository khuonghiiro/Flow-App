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
    private readonly CollisionResolver _collisionResolver;
    private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

    public BodyContainerNodeRenderer(IWorkflowEditorHostAccessor hostAccessor, CollisionResolver collisionResolver)
    {
        _hostAccessor = hostAccessor;
        _collisionResolver = collisionResolver;
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
        BodyContainerControl.RefreshVisualFromNode(node);
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

            if (bodyNode.LockInnerNodes && Host.ViewModel != null)
            {
                PushExternalNodesOutOfBody(bodyNode, nodesInside ?? new List<WorkflowNode>());
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
        if (!BodyContainerControl.TryGetVisualElements(bodyNode, out _, out _, out _, out var titleText, out _))
            return;

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

    private void PushExternalNodesOutOfBody(BodyContainerNode bodyNode, List<WorkflowNode> lockedNodes)
    {
        var vm = Host.ViewModel;
        if (vm == null) return;

        var bodyBounds = new Rect(bodyNode.X, bodyNode.Y, bodyNode.BodyWidth, bodyNode.BodyHeight);
        var lockedSet = new HashSet<WorkflowNode>(lockedNodes) { bodyNode };
        const double gap = 12.0;
        var movedNodes = new List<WorkflowNode>();

        foreach (var node in vm.Nodes)
        {
            if (lockedSet.Contains(node)) continue;
            if (node is LoopBodyNode or AsyncTaskBodyNode) continue;

            var width = node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150;
            var height = node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80;
            var nodeRect = new Rect(node.X, node.Y, width, height);
            if (!bodyBounds.IntersectsWith(nodeRect)) continue;

            var center = new Point(nodeRect.Left + nodeRect.Width / 2.0, nodeRect.Top + nodeRect.Height / 2.0);
            var leftDist = Math.Abs(center.X - bodyBounds.Left);
            var rightDist = Math.Abs(bodyBounds.Right - center.X);
            var topDist = Math.Abs(center.Y - bodyBounds.Top);
            var bottomDist = Math.Abs(bodyBounds.Bottom - center.Y);

            var minDist = Math.Min(Math.Min(leftDist, rightDist), Math.Min(topDist, bottomDist));
            double targetX = node.X;
            double targetY = node.Y;

            if (minDist == leftDist)
            {
                targetX = bodyBounds.Left - width - gap;
            }
            else if (minDist == rightDist)
            {
                targetX = bodyBounds.Right + gap;
            }
            else if (minDist == topDist)
            {
                targetY = bodyBounds.Top - height - gap;
            }
            else
            {
                targetY = bodyBounds.Bottom + gap;
            }

            Host.UpdateNodePosition(node, targetX, targetY);
            foreach (var conn in vm.Connections.Where(c => c.FromNode == node || c.ToNode == node))
                Host.UpdateConnectionPath(conn);
            movedNodes.Add(node);
        }

        // Collision pass: nodes vừa bị đẩy sẽ tiếp tục đẩy nhau nếu còn chồng lấn.
        foreach (var moved in movedNodes)
        {
            _collisionResolver.ResolveCollision(vm, moved, Host);
        }
    }
}
