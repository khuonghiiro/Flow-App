using System.Collections.Generic;
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
        // Skip NodeChrome.Apply() để tránh Liquid Glass shadow trên title
        border.ContextMenu = Host.CreateNodeContextMenu(bodyNode);
        AttachBodyDragHandlers(bodyNode, border);
        AttachHoverTitleBehavior(bodyNode, border);

        Canvas.SetLeft(border, bodyNode.X);
        Canvas.SetTop(border, bodyNode.Y);
        canvas.Children.Add(border);
        Host.ZIndexManager.InitializeNodeZIndex(bodyNode, border);
        // Nếu đã locked từ đầu → nâng z-index lên trên inner nodes
        if (bodyNode.LockInnerNodes)
            Host.ZIndexManager.SetLockedBodyZIndex(bodyNode);
        // Đảm bảo border không có shadow từ Canvas
        border.Effect = null;
        canvas.Effect = null;
        // Clear style để tránh implicit style từ WPF
        border.Style = null;
        // Xóa effect cho tất cả element con
        ClearEffectsRecursive(border);

        if (bodyNode is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += (_, e) =>
            {
                RefreshNodeVisual(bodyNode);
                // Khi LockInnerNodes thay đổi → cập nhật z-index
                if (e.PropertyName == nameof(BodyContainerNode.LockInnerNodes))
                    Host.ZIndexManager.SetLockedBodyZIndex(bodyNode);
            };
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

            // ✅ Nếu bodyNode này nằm bên trong một locked BodyContainerNode khác → không cho kéo
            if (Host.ViewModel != null)
            {
                var owningLocked = FindOwningLockedBodyForBody(Host.ViewModel, bodyNode);
                if (owningLocked != null)
                {
                    e.Handled = true;
                    return;
                }
            }

            dragging = true;
            startPoint = e.GetPosition(Host.WorkflowCanvas);
            origin = new Point(bodyNode.X, bodyNode.Y);
            nodesInside = bodyNode.LockInnerNodes ? CaptureNodesInsideBody(bodyNode) : null;
            border.CaptureMouse();
            border.Cursor = Cursors.SizeAll;
            Host.ZIndexManager.DragNode(bodyNode);
            e.Handled = true;
        };

        border.PreviewMouseMove += (_, e) =>
        {
            if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
            var current = e.GetPosition(Host.WorkflowCanvas);
            var dx = current.X - startPoint.X;
            var dy = current.Y - startPoint.Y;

            var proposedX = origin.X + dx;
            var proposedY = origin.Y + dy;

            bodyNode.X = proposedX;
            bodyNode.Y = proposedY;
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

            // ✅ Nếu body KHÔNG locked → đẩy locked bodies ra khi overlap (push effect)
            if (!bodyNode.LockInnerNodes && Host.ViewModel != null)
            {
                var movingGroup = new List<WorkflowNode> { bodyNode };
                if (nodesInside != null) movingGroup.AddRange(nodesInside);
                LockedBodyHelper.PushLockedBodiesAway(Host.ViewModel, Host, movingGroup);
            }

            // ✅ Nếu body locked → đẩy external nodes và child bodies ra ngoài
            if (bodyNode.LockInnerNodes && Host.ViewModel != null)
            {
                var lockedSet = new HashSet<WorkflowNode>(nodesInside ?? new List<WorkflowNode>()) { bodyNode };
                // Đẩy body con (LoopBody, AsyncBody, BodyContainer) ra - dùng cùng logic push mượt
                LockedBodyHelper.PushChildBodiesAwayFromLockedBody(Host.ViewModel, Host, bodyNode, lockedSet);
                // Đẩy nodes thường ra ngoài
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
            border.Cursor = Cursors.Arrow;
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
        var initialCaptured = new HashSet<WorkflowNode>();

        foreach (var node in vm.Nodes)
        {
            if (ReferenceEquals(node, bodyNode)) continue;
            
            // Lấy width/height chính xác kể cả cho LoopBody/AsyncBody
            double nodeW = node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150;
            double nodeH = node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80;
            if (node is LoopBodyNode lb) { nodeW = lb.Width > 0 ? lb.Width : nodeW; nodeH = lb.Height > 0 ? lb.Height : nodeH; }
            else if (node is AsyncTaskBodyNode ab) { nodeW = ab.Width > 0 ? ab.Width : nodeW; nodeH = ab.Height > 0 ? ab.Height : nodeH; }

            var cx = node.X + nodeW / 2.0;
            var cy = node.Y + nodeH / 2.0;

            if (bounds.Contains(new Point(cx, cy)))
            {
                initialCaptured.Add(node);
            }
        }

        // Bổ sung các mảnh ghép liên kết (nếu diamond bị bắt thì body cũng bị bắt và ngược lại)
        foreach (var node in initialCaptured.ToList())
        {
            result.Add(node);

            if (node is LoopNode loopNode && loopNode.LoopBodyNode != null && !initialCaptured.Contains(loopNode.LoopBodyNode))
            {
                result.Add(loopNode.LoopBodyNode);
                initialCaptured.Add(loopNode.LoopBodyNode); // Để tránh duplicate
            }
            else if (node is LoopBodyNode loopBody)
            {
                var parentLoop = vm.Nodes.OfType<LoopNode>().FirstOrDefault(n => n.LoopBodyNode == loopBody);
                if (parentLoop != null && !initialCaptured.Contains(parentLoop))
                {
                    result.Add(parentLoop);
                    initialCaptured.Add(parentLoop);
                }
            }
            else if (node is AsyncTaskNode asyncNode && asyncNode.AsyncTaskBodyNode != null && !initialCaptured.Contains(asyncNode.AsyncTaskBodyNode))
            {
                result.Add(asyncNode.AsyncTaskBodyNode);
                initialCaptured.Add(asyncNode.AsyncTaskBodyNode);
            }
            else if (node is AsyncTaskBodyNode asyncBody)
            {
                var parentAsync = vm.Nodes.OfType<AsyncTaskNode>().FirstOrDefault(n => n.AsyncTaskBodyNode == asyncBody);
                if (parentAsync != null && !initialCaptured.Contains(parentAsync))
                {
                    result.Add(parentAsync);
                    initialCaptured.Add(parentAsync);
                }
            }
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
        // Mở rộng vùng phát hiện thêm margin để đẩy sớm hơn (khi cách 10px viền)
        var inflatedBounds = new Rect(
            bodyBounds.X - LockedBodyHelper.Margin,
            bodyBounds.Y - LockedBodyHelper.Margin,
            bodyBounds.Width + LockedBodyHelper.Margin * 2,
            bodyBounds.Height + LockedBodyHelper.Margin * 2);
        var lockedSet = new HashSet<WorkflowNode>(lockedNodes) { bodyNode };
        const double gap = 12.0;
        var movedNodes = new List<WorkflowNode>();

        foreach (var node in vm.Nodes)
        {
            if (lockedSet.Contains(node)) continue;
            // Bỏ qua các body-type nodes vì chúng đã được xử lý bởi PushChildBodiesAwayFromLockedBody
            if (node is LoopBodyNode or AsyncTaskBodyNode or BodyContainerNode) continue;
            var nodeRect = LockedBodyHelper.GetNodeRect(vm, node);
            if (!inflatedBounds.IntersectsWith(nodeRect)) continue;

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
                targetX = bodyBounds.Left - nodeRect.Width - gap;
            }
            else if (minDist == rightDist)
            {
                targetX = bodyBounds.Right + gap;
            }
            else if (minDist == topDist)
            {
                targetY = bodyBounds.Top - nodeRect.Height - gap;
            }
            else
            {
                targetY = bodyBounds.Bottom + gap;
            }

            double dx = targetX - node.X;
            double dy = targetY - node.Y;

            // Gather all linked nodes (node itself + associated body/parent + inner nodes if any)
            var linkedNodes = new HashSet<WorkflowNode>();

            if (node is BodyContainerNode pushedBody && pushedBody.LockInnerNodes)
            {
                var innerNodes = CaptureNodesInsideBody(pushedBody);
                foreach (var inner in innerNodes) linkedNodes.Add(inner);
            }
            else if (node is LoopNode loopNode && loopNode.LoopBodyNode != null)
            {
                linkedNodes.Add(loopNode.LoopBodyNode);
                var loopInnerNodes = CaptureLoopOrAsyncBodyChildren(vm, loopNode.LoopBodyNode);
                foreach (var inner in loopInnerNodes) linkedNodes.Add(inner);
            }
            else if (node is LoopBodyNode loopBody)
            {
                var parentLoop = vm.Nodes.OfType<LoopNode>().FirstOrDefault(n => n.LoopBodyNode == loopBody);
                if (parentLoop != null) linkedNodes.Add(parentLoop);
                
                var loopInnerNodes = CaptureLoopOrAsyncBodyChildren(vm, loopBody);
                foreach (var inner in loopInnerNodes) linkedNodes.Add(inner);
            }
            else if (node is AsyncTaskNode asyncNode && asyncNode.AsyncTaskBodyNode != null && asyncNode.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
            {
                linkedNodes.Add(asyncNode.AsyncTaskBodyNode);
                var asyncInnerNodes = CaptureLoopOrAsyncBodyChildren(vm, asyncNode.AsyncTaskBodyNode);
                foreach (var inner in asyncInnerNodes) linkedNodes.Add(inner);
            }
            else if (node is AsyncTaskBodyNode asyncBody)
            {
                var parentAsync = vm.Nodes.OfType<AsyncTaskNode>().FirstOrDefault(n => n.AsyncTaskBodyNode == asyncBody);
                if (parentAsync != null) linkedNodes.Add(parentAsync);
                
                var asyncInnerNodes = CaptureLoopOrAsyncBodyChildren(vm, asyncBody);
                foreach (var inner in asyncInnerNodes) linkedNodes.Add(inner);
            }

            // Move the pushed node itself
            Host.UpdateNodePosition(node, targetX, targetY);
            // ✅ Force cập nhật visual cho body-type nodes (bypass RenderTransform optimization)
            ForceCanvasPosition(node, targetX, targetY);
            foreach (var conn in vm.Connections.Where(c => c.FromNode == node || c.ToNode == node))
            {
                Host.UpdateConnectionPath(conn);
            }
            movedNodes.Add(node);

            // Move linked nodes directly
            foreach (var inner in linkedNodes)
            {
                var innerTargetX = inner.X + dx;
                var innerTargetY = inner.Y + dy;
                Host.UpdateNodePosition(inner, innerTargetX, innerTargetY);
                ForceCanvasPosition(inner, innerTargetX, innerTargetY);
                foreach (var conn in vm.Connections.Where(c => c.FromNode == inner || c.ToNode == inner))
                {
                    Host.UpdateConnectionPath(conn);
                }
            }
        }

        foreach (var moved in movedNodes.ToList())
        {
            _collisionResolver.ResolveCollision(vm, moved, Host);
        }
    }

    /// <summary>
    /// Force cập nhật Canvas position cho node, bypass RenderTransform optimization.
    /// Cần thiết khi push body-type nodes (LoopBodyNode, AsyncTaskBodyNode) vì
    /// _NodeRenderer.UpdateNodePosition skip Canvas.SetLeft/Top khi có RenderTransform.
    /// </summary>
    private void ForceCanvasPosition(WorkflowNode node, double x, double y)
    {
        Border? targetBorder = node.Border;
        if (node is LoopBodyNode loopBody && Host.ViewModel != null)
        {
            var parentLoop = Host.ViewModel.Nodes.OfType<LoopNode>().FirstOrDefault(n => n.LoopBodyNode == loopBody);
            if (parentLoop != null) targetBorder = parentLoop.ContainerBorder;
        }
        else if (node is AsyncTaskBodyNode asyncBody && Host.ViewModel != null)
        {
            var parentAsync = Host.ViewModel.Nodes.OfType<AsyncTaskNode>().FirstOrDefault(n => n.AsyncTaskBodyNode == asyncBody);
            if (parentAsync != null) targetBorder = parentAsync.ContainerBorder;
        }

        if (targetBorder == null) return;

        // Reset RenderTransform nếu có để tránh position drift
        if (targetBorder.RenderTransform is System.Windows.Media.TranslateTransform tt && (tt.X != 0 || tt.Y != 0))
        {
            tt.X = 0;
            tt.Y = 0;
        }
        Canvas.SetLeft(targetBorder, x);
        Canvas.SetTop(targetBorder, y);
    }

    private List<WorkflowNode> CaptureLoopOrAsyncBodyChildren(FlowMy.ViewModels.WorkflowEditorViewModel vm, WorkflowNode bodyNode)
    {
        var result = new List<WorkflowNode>();
        double bodyX = bodyNode.X;
        double bodyY = bodyNode.Y;
        double bodyW = 0;
        double bodyH = 0;

        if (bodyNode is LoopBodyNode lb) { bodyW = lb.Width > 0 ? lb.Width : 400; bodyH = lb.Height > 0 ? lb.Height : 300; }
        else if (bodyNode is AsyncTaskBodyNode ab) { bodyW = ab.Width > 0 ? ab.Width : 400; bodyH = ab.Height > 0 ? ab.Height : 300; }

        var rect = new Rect(bodyX, bodyY, bodyW, bodyH);

        foreach (var child in vm.Nodes)
        {
            if (child == bodyNode || child is LoopNode || child is AsyncTaskNode || child is BodyContainerNode) continue;
            
            var childW = child.Border?.ActualWidth > 1 ? child.Border.ActualWidth : 150;
            var childH = child.Border?.ActualHeight > 1 ? child.Border.ActualHeight : 80;
            var center = new Point(child.X + childW / 2.0, child.Y + childH / 2.0);
            
            if (rect.Contains(center))
            {
                result.Add(child);
            }
        }
        return result;
    }

    private static void ClearEffectsRecursive(DependencyObject parent)
    {
        if (parent is UIElement uiElement)
        {
            uiElement.Effect = null;
        }

        int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            ClearEffectsRecursive(child);
        }
    }
    /// <summary>
    /// Kiểm tra xem BodyContainerNode này có nằm bên trong một locked BodyContainerNode khác không.
    /// Khác với FindOwningLockedBody thông thường (skip BodyContainerNode), method này cho phép
    /// kiểm tra BodyContainerNode con bên trong BodyContainerNode cha.
    /// </summary>
    private static BodyContainerNode? FindOwningLockedBodyForBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        BodyContainerNode childBody)
    {
        double nodeW = childBody.BodyWidth > 0 ? childBody.BodyWidth : (childBody.Border?.ActualWidth ?? 150);
        double nodeH = childBody.BodyHeight > 0 ? childBody.BodyHeight : (childBody.Border?.ActualHeight ?? 80);
        var center = new Point(childBody.X + nodeW / 2.0, childBody.Y + nodeH / 2.0);

        foreach (var body in viewModel.Nodes.OfType<BodyContainerNode>())
        {
            if (ReferenceEquals(body, childBody)) continue; // Skip chính nó
            if (!body.LockInnerNodes) continue;
            var width = body.BodyWidth > 0 ? body.BodyWidth : (body.Border?.ActualWidth ?? body.Border?.Width ?? 0);
            var height = body.BodyHeight > 0 ? body.BodyHeight : (body.Border?.ActualHeight ?? body.Border?.Height ?? 0);
            if (width <= 0 || height <= 0) continue;

            var rect = new Rect(body.X, body.Y, width, height);
            if (rect.Contains(center))
                return body;
        }
        return null;
    }
}
