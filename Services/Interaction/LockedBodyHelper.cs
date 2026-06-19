using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FlowMy.Models;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Interaction;

/// <summary>
/// Helper tĩnh chung để kiểm tra và xử lý push logic cho locked BodyContainerNode.
/// Được sử dụng bởi BodyContainerNodeRenderer, LoopNodeRenderer, AsyncTaskNodeRenderer
/// và DragDropHandler để đảm bảo logic nhất quán.
/// </summary>
public static class LockedBodyHelper
{
    /// <summary>
    /// Khoảng cách margin (px) giữa node và locked body edge trước khi push.
    /// </summary>
    public const double Margin = 10.0;

    /// <summary>
    /// Re-entrancy guard: ngăn vòng lặp vô hạn khi push A đẩy B rồi B đẩy lại A.
    /// </summary>
    [ThreadStatic]
    private static bool _isPushing;

    /// <summary>
    /// Tìm locked BodyContainerNode đang chứa node này (dựa trên rect overlap).
    /// Trả về null nếu node không nằm trong bất kỳ locked body nào.
    /// </summary>
    public static BodyContainerNode? FindOwningLockedBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        WorkflowNode node)
    {
        if (node is BodyContainerNode) return null;

        var nodeRect = GetNodeRect(viewModel, node);

        foreach (var body in viewModel.Nodes.OfType<BodyContainerNode>())
        {
            if (!body.LockInnerNodes) continue;
            var width = body.BodyWidth > 0 ? body.BodyWidth : (body.Border?.ActualWidth ?? body.Border?.Width ?? 0);
            var height = body.BodyHeight > 0 ? body.BodyHeight : (body.Border?.ActualHeight ?? body.Border?.Height ?? 0);
            if (width <= 0 || height <= 0) continue;

            var bodyRect = new Rect(body.X, body.Y, width, height);
            if (bodyRect.IntersectsWith(nodeRect))
                return body;
        }
        return null;
    }

    /// <summary>
    /// Kiểm tra xem nếu BodyContainerNode di chuyển thêm (dx, dy) thì có lọt vào
    /// trong viền của một LoopBodyNode, AsyncTaskBodyNode, hoặc BodyContainerNode khác hay không.
    /// Dùng để chặn di chuyển (block) thay vì push.
    /// </summary>
    public static bool WouldBodyEnterOtherContainerBodies(
        ViewModels.WorkflowEditorViewModel viewModel,
        BodyContainerNode movingBody,
        double moveDx, double moveDy)
    {
        var originalRect = GetNodeRect(viewModel, movingBody);
        var proposedRect = new Rect(
            originalRect.X + moveDx,
            originalRect.Y + moveDy,
            originalRect.Width,
            originalRect.Height);

        // Mở rộng thêm margin để chặn từ xa
        var inflatedProposed = new Rect(
            proposedRect.X - Margin,
            proposedRect.Y - Margin,
            proposedRect.Width + Margin * 2,
            proposedRect.Height + Margin * 2);

        foreach (var node in viewModel.Nodes)
        {
            if (ReferenceEquals(node, movingBody)) continue;
            // Chỉ kiểm tra block với các body-type nodes
            if (node is not (LoopBodyNode or AsyncTaskBodyNode or BodyContainerNode)) continue;

            // Nếu movingBody chứa node này (node này là con của movingBody), thì không block
            if (movingBody.LockInnerNodes)
            {
                var bodyOriginalRect = new Rect(movingBody.X, movingBody.Y, movingBody.BodyWidth, movingBody.BodyHeight);
                var nodeCenter = GetNodeCenter(viewModel, node);
                if (bodyOriginalRect.Contains(nodeCenter)) continue; // là con, bỏ qua
            }

            var targetRect = GetNodeRect(viewModel, node);
            if (inflatedProposed.IntersectsWith(targetRect))
                return true; // Sẽ lọt vào -> block
        }
        return false;
    }

    /// <summary>
    /// Khi một node (hoặc nhóm nodes) di chuyển và overlap với locked BodyContainerNode,
    /// đẩy locked body đi theo hướng ngược lại. Tạo hiệu ứng push mượt thay vì block cứng.
    /// </summary>
    /// <param name="viewModel">ViewModel chứa danh sách nodes</param>
    /// <param name="host">Host để update positions và connections</param>
    /// <param name="movingNodes">Nhóm nodes đang di chuyển (không bị push)</param>
    public static void PushLockedBodiesAway(
        ViewModels.WorkflowEditorViewModel viewModel,
        IWorkflowEditorHost host,
        IEnumerable<WorkflowNode> movingNodes)
    {
        if (_isPushing) return;
        _isPushing = true;
        try
        {
        PushLockedBodiesAwayCore(viewModel, host, movingNodes);
        }
        finally { _isPushing = false; }
    }

    private static void PushLockedBodiesAwayCore(
        ViewModels.WorkflowEditorViewModel viewModel,
        IWorkflowEditorHost host,
        IEnumerable<WorkflowNode> movingNodes)
    {
        var movingSet = new HashSet<WorkflowNode>(movingNodes);

        var lockedBodies = viewModel.Nodes.OfType<BodyContainerNode>()
            .Where(b => b.LockInnerNodes && !movingSet.Contains(b))
            .ToList();

        if (lockedBodies.Count == 0) return;

        foreach (var lockedBody in lockedBodies)
        {
            var bodyRect = new Rect(lockedBody.X, lockedBody.Y, lockedBody.BodyWidth, lockedBody.BodyHeight);
            // Mở rộng vùng phát hiện thêm Margin mỗi bên
            var inflatedRect = new Rect(
                bodyRect.X - Margin,
                bodyRect.Y - Margin,
                bodyRect.Width + Margin * 2,
                bodyRect.Height + Margin * 2);

            // Tìm node trong nhóm di chuyển gần nhất overlap với locked body
            double maxPushDx = 0, maxPushDy = 0;
            bool needPush = false;

            foreach (var node in movingSet)
            {
                if (node is BodyContainerNode) continue; // Body không push body khác bằng cách này

                var nodeRect = GetNodeRect(viewModel, node);
                if (!inflatedRect.IntersectsWith(nodeRect)) continue;

                // Tính overlap trên mỗi cạnh
                double overlapRight = nodeRect.Right + Margin - bodyRect.Left;   // node ở bên trái → push body sang phải
                double overlapLeft = bodyRect.Right + Margin - nodeRect.Left;     // node ở bên phải → push body sang trái
                double overlapBottom = nodeRect.Bottom + Margin - bodyRect.Top;   // node ở trên → push body xuống
                double overlapTop = bodyRect.Bottom + Margin - nodeRect.Top;      // node ở dưới → push body lên

                // Chỉ xét overlap dương (thực sự overlap)
                if (overlapRight <= 0 || overlapLeft <= 0 || overlapBottom <= 0 || overlapTop <= 0)
                    continue;

                // Chọn hướng push có overlap nhỏ nhất (đẩy ít nhất)
                double minOverlap = Math.Min(Math.Min(overlapRight, overlapLeft), Math.Min(overlapBottom, overlapTop));
                double pushDx = 0, pushDy = 0;

                if (Math.Abs(minOverlap - overlapRight) < 0.01)
                    pushDx = overlapRight;
                else if (Math.Abs(minOverlap - overlapLeft) < 0.01)
                    pushDx = -overlapLeft;
                else if (Math.Abs(minOverlap - overlapBottom) < 0.01)
                    pushDy = overlapBottom;
                else
                    pushDy = -overlapTop;

                // Giữ push lớn nhất (nếu nhiều nodes cùng overlap)
                if (Math.Abs(pushDx) > Math.Abs(maxPushDx)) maxPushDx = pushDx;
                if (Math.Abs(pushDy) > Math.Abs(maxPushDy)) maxPushDy = pushDy;
                needPush = true;
            }

            if (!needPush || (Math.Abs(maxPushDx) < 0.5 && Math.Abs(maxPushDy) < 0.5)) continue;

            // Đẩy locked body + tất cả inner nodes
            MoveLockedBodyWithChildren(viewModel, host, lockedBody, maxPushDx, maxPushDy, movingSet);
        }
    }
    /// <summary>
    /// Khi locked body di chuyển và overlap với child body nodes (LoopBodyNode, AsyncTaskBodyNode,
    /// BodyContainerNode khác) bên ngoài nó → đẩy child body đi.
    /// Dùng CÙNG logic overlap/push như PushLockedBodiesAway nhưng đảo vai trò.
    /// </summary>
    public static void PushChildBodiesAwayFromLockedBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        IWorkflowEditorHost host,
        BodyContainerNode lockedBody,
        HashSet<WorkflowNode> lockedInnerNodes)
    {
        if (_isPushing) return;
        _isPushing = true;
        try
        {
        PushChildBodiesAwayCore(viewModel, host, lockedBody, lockedInnerNodes);
        }
        finally { _isPushing = false; }
    }

    private static void PushChildBodiesAwayCore(
        ViewModels.WorkflowEditorViewModel viewModel,
        IWorkflowEditorHost host,
        BodyContainerNode lockedBody,
        HashSet<WorkflowNode> lockedInnerNodes)
    {
        var lockedRect = new Rect(lockedBody.X, lockedBody.Y, lockedBody.BodyWidth, lockedBody.BodyHeight);
        var inflatedRect = new Rect(
            lockedRect.X - Margin,
            lockedRect.Y - Margin,
            lockedRect.Width + Margin * 2,
            lockedRect.Height + Margin * 2);

        var lockedSet = new HashSet<WorkflowNode>(lockedInnerNodes) { lockedBody };

        var targetBodies = new List<WorkflowNode>();
        foreach (var node in viewModel.Nodes)
        {
            if (node is BodyContainerNode bcn && !lockedSet.Contains(bcn))
                targetBodies.Add(bcn);
            else if (node is LoopNode ln && ln.LoopBodyNode != null && !lockedSet.Contains(ln.LoopBodyNode))
                targetBodies.Add(ln.LoopBodyNode);
            else if (node is AsyncTaskNode an && an.AsyncTaskBodyNode != null && !lockedSet.Contains(an.AsyncTaskBodyNode))
                targetBodies.Add(an.AsyncTaskBodyNode);
        }

        foreach (var node in targetBodies)
        {
            var nodeRect = GetNodeRect(viewModel, node);
            if (!inflatedRect.IntersectsWith(nodeRect)) continue;

            double overlapRight = lockedRect.Right + Margin - nodeRect.Left;
            double overlapLeft = nodeRect.Right + Margin - lockedRect.Left;
            double overlapBottom = lockedRect.Bottom + Margin - nodeRect.Top;
            double overlapTop = nodeRect.Bottom + Margin - lockedRect.Top;

            if (overlapRight <= 0 || overlapLeft <= 0 || overlapBottom <= 0 || overlapTop <= 0)
                continue;

            double minOverlap = Math.Min(Math.Min(overlapRight, overlapLeft), Math.Min(overlapBottom, overlapTop));
            double pushDx = 0, pushDy = 0;

            if (Math.Abs(minOverlap - overlapRight) < 0.01)
                pushDx = overlapRight;
            else if (Math.Abs(minOverlap - overlapLeft) < 0.01)
                pushDx = -overlapLeft;
            else if (Math.Abs(minOverlap - overlapBottom) < 0.01)
                pushDy = overlapBottom;
            else
                pushDy = -overlapTop;

            if (Math.Abs(pushDx) < 0.5 && Math.Abs(pushDy) < 0.5) continue;

            // Thu thập linked nodes
            var linkedNodes = new List<WorkflowNode>();

            if (node is LoopBodyNode loopBody)
            {
                var parentLoop = viewModel.Nodes.OfType<LoopNode>().FirstOrDefault(n => n.LoopBodyNode == loopBody);
                if (parentLoop != null) linkedNodes.Add(parentLoop);
                linkedNodes.AddRange(GetBodyClusterChildren(viewModel, loopBody));
            }
            else if (node is AsyncTaskBodyNode asyncBody)
            {
                var parentAsync = viewModel.Nodes.OfType<AsyncTaskNode>().FirstOrDefault(n => n.AsyncTaskBodyNode == asyncBody);
                if (parentAsync != null) linkedNodes.Add(parentAsync);
                linkedNodes.AddRange(GetBodyClusterChildren(viewModel, asyncBody));
            }
            else if (node is BodyContainerNode childBody)
            {
                var bodyRect2 = new Rect(childBody.X, childBody.Y, childBody.BodyWidth, childBody.BodyHeight);
                foreach (var inner in viewModel.Nodes)
                {
                    if (ReferenceEquals(inner, childBody) || lockedSet.Contains(inner)) continue;
                    if (bodyRect2.Contains(GetNodeCenter(viewModel, inner)))
                        linkedNodes.Add(inner);
                }
            }

            // Di chuyển node chính + linked nodes
            host.UpdateNodePosition(node, node.X + pushDx, node.Y + pushDy);
            ForceCanvasPosition(viewModel, node, node.X, node.Y);

            foreach (var linked in linkedNodes)
            {
                host.UpdateNodePosition(linked, linked.X + pushDx, linked.Y + pushDy);
                ForceCanvasPosition(viewModel, linked, linked.X, linked.Y);
            }

            var allMoved = new HashSet<WorkflowNode>(linkedNodes) { node };
            foreach (var conn in viewModel.Connections)
            {
                if (allMoved.Contains(conn.FromNode) || allMoved.Contains(conn.ToNode))
                    host.UpdateConnectionPath(conn);
            }
        }
    }

    /// <summary>
    /// Lấy cluster children nằm trong LoopBody hoặc AsyncTaskBody.
    /// </summary>
    private static List<WorkflowNode> GetBodyClusterChildren(
        ViewModels.WorkflowEditorViewModel viewModel, WorkflowNode bodyNode)
    {
        var result = new List<WorkflowNode>();
        var bodyRect = GetNodeRect(viewModel, bodyNode);
        foreach (var child in viewModel.Nodes)
        {
            if (ReferenceEquals(child, bodyNode)) continue;
            if (child is LoopNode || child is AsyncTaskNode || child is BodyContainerNode) continue;
            if (bodyRect.Contains(GetNodeCenter(viewModel, child)))
                result.Add(child);
        }
        return result;
    }

    /// <summary>
    /// Di chuyển locked body và tất cả inner nodes theo (dx, dy).
    /// Cập nhật visual positions và connections.
    /// </summary>
    private static void MoveLockedBodyWithChildren(
        ViewModels.WorkflowEditorViewModel viewModel,
        IWorkflowEditorHost host,
        BodyContainerNode lockedBody,
        double dx, double dy,
        HashSet<WorkflowNode>? excludeNodes = null)
    {
        // Thu thập inner nodes trước khi di chuyển
        var bodyRect = new Rect(lockedBody.X, lockedBody.Y, lockedBody.BodyWidth, lockedBody.BodyHeight);
        var innerNodes = new List<WorkflowNode>();

        foreach (var node in viewModel.Nodes)
        {
            if (ReferenceEquals(node, lockedBody)) continue;
            if (excludeNodes != null && excludeNodes.Contains(node)) continue;

            var nodeCenter = GetNodeCenter(viewModel, node);
            if (bodyRect.Contains(nodeCenter))
                innerNodes.Add(node);
        }

        // ✅ Thu thập cả LoopBodyNode/AsyncTaskBodyNode pseudo-nodes nằm trong body
        // (chúng không nằm trong viewModel.Nodes nên phải quét qua parent nodes)
        var pseudoBodyNodes = new List<WorkflowNode>();
        foreach (var node in viewModel.Nodes)
        {
            if (node is LoopNode ln && ln.LoopBodyNode != null
                && !ReferenceEquals(ln.LoopBodyNode, lockedBody)
                && (excludeNodes == null || !excludeNodes.Contains(ln.LoopBodyNode)))
            {
                var lbCenter = GetNodeCenter(viewModel, ln.LoopBodyNode);
                if (bodyRect.Contains(lbCenter))
                    pseudoBodyNodes.Add(ln.LoopBodyNode);
            }
            else if (node is AsyncTaskNode an && an.AsyncTaskBodyNode != null
                && !ReferenceEquals(an.AsyncTaskBodyNode, lockedBody)
                && (excludeNodes == null || !excludeNodes.Contains(an.AsyncTaskBodyNode)))
            {
                var abCenter = GetNodeCenter(viewModel, an.AsyncTaskBodyNode);
                if (bodyRect.Contains(abCenter))
                    pseudoBodyNodes.Add(an.AsyncTaskBodyNode);
            }
        }

        // Di chuyển locked body
        host.UpdateNodePosition(lockedBody, lockedBody.X + dx, lockedBody.Y + dy);
        ForceCanvasPosition(viewModel, lockedBody, lockedBody.X, lockedBody.Y);

        // Di chuyển inner nodes
        foreach (var inner in innerNodes)
        {
            host.UpdateNodePosition(inner, inner.X + dx, inner.Y + dy);
            ForceCanvasPosition(viewModel, inner, inner.X, inner.Y);
        }

        // Di chuyển pseudo body nodes (LoopBodyNode, AsyncTaskBodyNode)
        foreach (var pseudo in pseudoBodyNodes)
        {
            host.UpdateNodePosition(pseudo, pseudo.X + dx, pseudo.Y + dy);
            ForceCanvasPosition(viewModel, pseudo, pseudo.X, pseudo.Y);
        }

        // Cập nhật tất cả connections liên quan
        var affectedNodes = new HashSet<WorkflowNode>(innerNodes) { lockedBody };
        foreach (var pseudo in pseudoBodyNodes) affectedNodes.Add(pseudo);
        foreach (var conn in viewModel.Connections)
        {
            if (affectedNodes.Contains(conn.FromNode) || affectedNodes.Contains(conn.ToNode))
                host.UpdateConnectionPath(conn);
        }
    }

    /// <summary>
    /// Lấy bounding rect của một node.
    /// </summary>
    public static Rect GetNodeRect(ViewModels.WorkflowEditorViewModel viewModel, WorkflowNode node)
    {
        double nodeW = 150, nodeH = 80;
        Border? targetBorder = node.Border;
        
        if (node is LoopBodyNode loopBody)
        {
            var parentLoop = viewModel.Nodes.OfType<LoopNode>().FirstOrDefault(n => n.LoopBodyNode == loopBody);
            if (parentLoop != null) targetBorder = parentLoop.ContainerBorder;
            nodeW = loopBody.Width > 0 ? loopBody.Width : (targetBorder?.ActualWidth > 1 ? targetBorder.ActualWidth : 150);
            nodeH = loopBody.Height > 0 ? loopBody.Height : (targetBorder?.ActualHeight > 1 ? targetBorder.ActualHeight : 80);
        }
        else if (node is AsyncTaskBodyNode asyncBody)
        {
            var parentAsync = viewModel.Nodes.OfType<AsyncTaskNode>().FirstOrDefault(n => n.AsyncTaskBodyNode == asyncBody);
            if (parentAsync != null) targetBorder = parentAsync.ContainerBorder;
            nodeW = asyncBody.Width > 0 ? asyncBody.Width : (targetBorder?.ActualWidth > 1 ? targetBorder.ActualWidth : 150);
            nodeH = asyncBody.Height > 0 ? asyncBody.Height : (targetBorder?.ActualHeight > 1 ? targetBorder.ActualHeight : 80);
        }
        else if (node is BodyContainerNode bcn)
        {
            nodeW = bcn.BodyWidth > 0 ? bcn.BodyWidth : (targetBorder?.ActualWidth > 1 ? targetBorder.ActualWidth : 150);
            nodeH = bcn.BodyHeight > 0 ? bcn.BodyHeight : (targetBorder?.ActualHeight > 1 ? targetBorder.ActualHeight : 80);
        }
        else
        {
            nodeW = targetBorder?.ActualWidth > 1 ? targetBorder.ActualWidth : 150;
            nodeH = targetBorder?.ActualHeight > 1 ? targetBorder.ActualHeight : 80;
        }
        
        return new Rect(node.X, node.Y, nodeW, nodeH);
    }

    private static Point GetNodeCenter(ViewModels.WorkflowEditorViewModel viewModel, WorkflowNode node)
    {
        var rect = GetNodeRect(viewModel, node);
        return new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0);
    }
    /// <summary>
    /// Force cập nhật Canvas position cho node, bypass RenderTransform optimization.
    /// Reset TranslateTransform nếu có để tránh position drift.
    /// </summary>
    private static void ForceCanvasPosition(ViewModels.WorkflowEditorViewModel viewModel, WorkflowNode node, double x, double y)
    {
        Border? targetBorder = node.Border;
        if (node is LoopBodyNode loopBody)
        {
            var parentLoop = viewModel.Nodes.OfType<LoopNode>().FirstOrDefault(n => n.LoopBodyNode == loopBody);
            if (parentLoop != null) targetBorder = parentLoop.ContainerBorder;
        }
        else if (node is AsyncTaskBodyNode asyncBody)
        {
            var parentAsync = viewModel.Nodes.OfType<AsyncTaskNode>().FirstOrDefault(n => n.AsyncTaskBodyNode == asyncBody);
            if (parentAsync != null) targetBorder = parentAsync.ContainerBorder;
        }

        if (targetBorder == null) return;
        if (targetBorder.RenderTransform is System.Windows.Media.TranslateTransform tt && (tt.X != 0 || tt.Y != 0))
        {
            tt.X = 0;
            tt.Y = 0;
        }
        Canvas.SetLeft(targetBorder, x);
        Canvas.SetTop(targetBorder, y);
    }
}
