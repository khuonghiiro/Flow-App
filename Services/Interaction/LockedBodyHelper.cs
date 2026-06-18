using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FlowMy.Models;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Interaction;

/// <summary>
/// Helper tĩnh chung để kiểm tra locked BodyContainerNode.
/// Được sử dụng bởi BodyContainerNodeRenderer, LoopNodeRenderer, AsyncTaskNodeRenderer
/// và DragDropHandler để đảm bảo logic nhất quán.
/// </summary>
public static class LockedBodyHelper
{
    /// <summary>
    /// Tìm locked BodyContainerNode đang chứa node này (dựa trên center point).
    /// Trả về null nếu node không nằm trong bất kỳ locked body nào.
    /// </summary>
    public static BodyContainerNode? FindOwningLockedBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        WorkflowNode node)
    {
        // BodyContainerNode không thể bị nhốt bởi body khác (trong context này)
        if (node is BodyContainerNode) return null;

        var center = GetNodeCenter(node);

        foreach (var body in viewModel.Nodes.OfType<BodyContainerNode>())
        {
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

    /// <summary>
    /// Kiểm tra xem node (hoặc nhóm nodes di chuyển cùng nó) có sẽ lọt vào
    /// một locked BodyContainerNode sau khi di chuyển theo (dx, dy) hay không.
    /// </summary>
    /// <param name="viewModel">ViewModel chứa danh sách nodes</param>
    /// <param name="movingNodes">Tất cả nodes sẽ di chuyển cùng (bao gồm bản thân node chính)</param>
    /// <param name="dx">Delta X</param>
    /// <param name="dy">Delta Y</param>
    /// <returns>true nếu sẽ lọt vào → cần chặn di chuyển</returns>
    public static bool WouldEnterLockedBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        IEnumerable<WorkflowNode> movingNodes,
        double dx, double dy)
    {
        var movingSet = new HashSet<WorkflowNode>(movingNodes);

        // Lấy danh sách tất cả locked BodyContainerNode mà KHÔNG thuộc nhóm đang di chuyển
        var lockedBodies = viewModel.Nodes.OfType<BodyContainerNode>()
            .Where(b => b.LockInnerNodes && !movingSet.Contains(b))
            .ToList();

        if (lockedBodies.Count == 0) return false;

        foreach (var node in movingSet)
        {
            // Bỏ qua BodyContainerNode (chúng không bị nhốt vào body khác)
            if (node is BodyContainerNode) continue;

            double nodeW = node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150;
            double nodeH = node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80;
            if (node is LoopBodyNode lb) { nodeW = lb.Width > 0 ? lb.Width : nodeW; nodeH = lb.Height > 0 ? lb.Height : nodeH; }
            else if (node is AsyncTaskBodyNode ab) { nodeW = ab.Width > 0 ? ab.Width : nodeW; nodeH = ab.Height > 0 ? ab.Height : nodeH; }

            var currentCenter = new Point(node.X + nodeW / 2.0, node.Y + nodeH / 2.0);
            var newCenter = new Point(node.X + dx + nodeW / 2.0, node.Y + dy + nodeH / 2.0);

            foreach (var lockedBody in lockedBodies)
            {
                var bodyRect = new Rect(lockedBody.X, lockedBody.Y, lockedBody.BodyWidth, lockedBody.BodyHeight);

                // Chỉ chặn nếu node CHƯA nằm trong locked body nhưng SAU khi di chuyển SẼ lọt vào
                bool wasInside = bodyRect.Contains(currentCenter);
                bool wouldBeInside = bodyRect.Contains(newCenter);

                if (!wasInside && wouldBeInside)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra xem một node cụ thể (không tính nhóm) có sẽ lọt vào locked body hay không.
    /// Dùng cho trường hợp đơn giản khi chỉ cần check 1 node.
    /// </summary>
    public static bool WouldSingleNodeEnterLockedBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        WorkflowNode node,
        double newX, double newY)
    {
        if (node is BodyContainerNode) return false;

        double nodeW = node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150;
        double nodeH = node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80;
        if (node is LoopBodyNode lb) { nodeW = lb.Width > 0 ? lb.Width : nodeW; nodeH = lb.Height > 0 ? lb.Height : nodeH; }
        else if (node is AsyncTaskBodyNode ab) { nodeW = ab.Width > 0 ? ab.Width : nodeW; nodeH = ab.Height > 0 ? ab.Height : nodeH; }

        var currentCenter = new Point(node.X + nodeW / 2.0, node.Y + nodeH / 2.0);
        var newCenter = new Point(newX + nodeW / 2.0, newY + nodeH / 2.0);

        foreach (var body in viewModel.Nodes.OfType<BodyContainerNode>())
        {
            if (!body.LockInnerNodes) continue;
            var bodyRect = new Rect(body.X, body.Y, body.BodyWidth, body.BodyHeight);

            bool wasInside = bodyRect.Contains(currentCenter);
            bool wouldBeInside = bodyRect.Contains(newCenter);

            if (!wasInside && wouldBeInside)
                return true;
        }

        return false;
    }

    private static Point GetNodeCenter(WorkflowNode node)
    {
        double nodeW = node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150;
        double nodeH = node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80;
        if (node is LoopBodyNode lb) { nodeW = lb.Width > 0 ? lb.Width : nodeW; nodeH = lb.Height > 0 ? lb.Height : nodeH; }
        else if (node is AsyncTaskBodyNode ab) { nodeW = ab.Width > 0 ? ab.Width : nodeW; nodeH = ab.Height > 0 ? ab.Height : nodeH; }
        return new Point(node.X + nodeW / 2.0, node.Y + nodeH / 2.0);
    }
}
