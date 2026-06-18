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
    /// Khoảng cách margin (px) giữa node và locked body edge trước khi bị chặn.
    /// </summary>
    private const double Margin = 10.0;

    /// <summary>
    /// Tìm locked BodyContainerNode đang chứa node này (dựa trên rect overlap).
    /// Trả về null nếu node không nằm trong bất kỳ locked body nào.
    /// </summary>
    public static BodyContainerNode? FindOwningLockedBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        WorkflowNode node)
    {
        // BodyContainerNode không thể bị nhốt bởi body khác (trong context này)
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
    /// Kiểm tra xem node (hoặc nhóm nodes di chuyển cùng nó) có sẽ lọt vào
    /// một locked BodyContainerNode sau khi di chuyển theo (dx, dy) hay không.
    /// Sử dụng rect overlap với 10px margin thay vì center-point containment.
    /// </summary>
    public static bool WouldEnterLockedBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        IEnumerable<WorkflowNode> movingNodes,
        double dx, double dy)
    {
        var movingSet = new HashSet<WorkflowNode>(movingNodes);

        var lockedBodies = viewModel.Nodes.OfType<BodyContainerNode>()
            .Where(b => b.LockInnerNodes && !movingSet.Contains(b))
            .ToList();

        if (lockedBodies.Count == 0) return false;

        foreach (var node in movingSet)
        {
            if (node is BodyContainerNode) continue;

            var currentRect = GetNodeRect(node);
            var proposedRect = new Rect(node.X + dx, node.Y + dy, currentRect.Width, currentRect.Height);

            foreach (var lockedBody in lockedBodies)
            {
                // Mở rộng locked body rect thêm Margin px mỗi bên
                var inflatedBodyRect = new Rect(
                    lockedBody.X - Margin,
                    lockedBody.Y - Margin,
                    lockedBody.BodyWidth + Margin * 2,
                    lockedBody.BodyHeight + Margin * 2);

                bool wasOverlapping = inflatedBodyRect.IntersectsWith(currentRect);
                bool wouldOverlap = inflatedBodyRect.IntersectsWith(proposedRect);

                if (!wasOverlapping && wouldOverlap)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra xem một node cụ thể (không tính nhóm) có sẽ lọt vào locked body hay không.
    /// Sử dụng rect overlap với 10px margin.
    /// </summary>
    public static bool WouldSingleNodeEnterLockedBody(
        ViewModels.WorkflowEditorViewModel viewModel,
        WorkflowNode node,
        double newX, double newY)
    {
        if (node is BodyContainerNode) return false;

        var currentRect = GetNodeRect(node);
        var proposedRect = new Rect(newX, newY, currentRect.Width, currentRect.Height);

        foreach (var body in viewModel.Nodes.OfType<BodyContainerNode>())
        {
            if (!body.LockInnerNodes) continue;

            var inflatedBodyRect = new Rect(
                body.X - Margin,
                body.Y - Margin,
                body.BodyWidth + Margin * 2,
                body.BodyHeight + Margin * 2);

            bool wasOverlapping = inflatedBodyRect.IntersectsWith(currentRect);
            bool wouldOverlap = inflatedBodyRect.IntersectsWith(proposedRect);

            if (!wasOverlapping && wouldOverlap)
                return true;
        }

        return false;
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
}
