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
    /// Tìm locked BodyContainerNode đang chứa node này (dựa trên rect overlap).
    /// Trả về null nếu node không nằm trong bất kỳ locked body nào.
    /// </summary>
    public static BodyContainerNode? FindOwningLockedBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        WorkflowNode node)
    {
        if (node is BodyContainerNode) return null;

        var nodeRect = GetNodeRect(node);

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

                var nodeRect = GetNodeRect(node);
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
        var lockedRect = new Rect(lockedBody.X, lockedBody.Y, lockedBody.BodyWidth, lockedBody.BodyHeight);
        var inflatedRect = new Rect(
            lockedRect.X - Margin,
            lockedRect.Y - Margin,
            lockedRect.Width + Margin * 2,
            lockedRect.Height + Margin * 2);

        var lockedSet = new HashSet<WorkflowNode>(lockedInnerNodes) { lockedBody };

        foreach (var node in viewModel.Nodes.ToList())
        {
            if (lockedSet.Contains(node)) continue;
            if (node is not (LoopBodyNode or AsyncTaskBodyNode or BodyContainerNode)) continue;

            var nodeRect = GetNodeRect(node);
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
                    if (bodyRect2.Contains(GetNodeCenter(inner)))
                        linkedNodes.Add(inner);
                }
            }

            // Di chuyển node chính + linked nodes
            host.UpdateNodePosition(node, node.X + pushDx, node.Y + pushDy);
            ForceCanvasPosition(node, node.X, node.Y);

            foreach (var linked in linkedNodes)
            {
                host.UpdateNodePosition(linked, linked.X + pushDx, linked.Y + pushDy);
                ForceCanvasPosition(linked, linked.X, linked.Y);
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
        var bodyRect = GetNodeRect(bodyNode);
        foreach (var child in viewModel.Nodes)
        {
            if (ReferenceEquals(child, bodyNode)) continue;
            if (child is LoopNode || child is AsyncTaskNode || child is BodyContainerNode) continue;
            if (bodyRect.Contains(GetNodeCenter(child)))
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

            var nodeCenter = GetNodeCenter(node);
            if (bodyRect.Contains(nodeCenter))
                innerNodes.Add(node);
        }

        // Di chuyển locked body
        host.UpdateNodePosition(lockedBody, lockedBody.X + dx, lockedBody.Y + dy);
        ForceCanvasPosition(lockedBody, lockedBody.X, lockedBody.Y);

        // Di chuyển inner nodes
        foreach (var inner in innerNodes)
        {
            host.UpdateNodePosition(inner, inner.X + dx, inner.Y + dy);
            ForceCanvasPosition(inner, inner.X, inner.Y);
        }

        // Cập nhật tất cả connections liên quan
        var affectedNodes = new HashSet<WorkflowNode>(innerNodes) { lockedBody };
        foreach (var conn in viewModel.Connections)
        {
            if (affectedNodes.Contains(conn.FromNode) || affectedNodes.Contains(conn.ToNode))
                host.UpdateConnectionPath(conn);
        }
    }

    /// <summary>
    /// Lấy bounding rect của một node.
    /// </summary>
    public static Rect GetNodeRect(WorkflowNode node)
    {
        double nodeW, nodeH;
        if (node is LoopBodyNode lb)
        {
            nodeW = lb.Width > 0 ? lb.Width : (node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150);
            nodeH = lb.Height > 0 ? lb.Height : (node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80);
        }
        else if (node is AsyncTaskBodyNode ab)
        {
            nodeW = ab.Width > 0 ? ab.Width : (node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150);
            nodeH = ab.Height > 0 ? ab.Height : (node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80);
        }
        else if (node is BodyContainerNode bcn)
        {
            nodeW = bcn.BodyWidth > 0 ? bcn.BodyWidth : (node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150);
            nodeH = bcn.BodyHeight > 0 ? bcn.BodyHeight : (node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80);
        }
        else
        {
            nodeW = node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150;
            nodeH = node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80;
        }
        return new Rect(node.X, node.Y, nodeW, nodeH);
    }

    private static Point GetNodeCenter(WorkflowNode node)
    {
        var rect = GetNodeRect(node);
        return new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0);
    }
    /// <summary>
    /// Force cập nhật Canvas position cho node, bypass RenderTransform optimization.
    /// Reset TranslateTransform nếu có để tránh position drift.
    /// </summary>
    private static void ForceCanvasPosition(WorkflowNode node, double x, double y)
    {
        if (node.Border == null) return;
        if (node.Border.RenderTransform is System.Windows.Media.TranslateTransform tt && (tt.X != 0 || tt.Y != 0))
        {
            tt.X = 0;
            tt.Y = 0;
        }
        Canvas.SetLeft(node.Border, x);
        Canvas.SetTop(node.Border, y);
    }
}
