using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Services.Interaction
{
    /// <summary>
    /// Service xử lý collision detection và đẩy node khi có overlap
    /// </summary>
    public class CollisionResolver
    {
        private const double MinNodeSpacing = 10.0; // Khoảng cách tối thiểu giữa các node
        private const int MaxRecursionDepth = 50; // Giới hạn độ sâu đệ quy để tránh vòng lặp vô hạn

        /// <summary>
        /// Resolve collision cho một node - đẩy các node khác nếu bị overlap
        /// </summary>
        /// <param name="viewModel">ViewModel chứa danh sách nodes</param>
        /// <param name="targetNode">Node cần kiểm tra collision</param>
        /// <param name="host">Host để update node position</param>
        public void ResolveCollision(
            WorkflowEditorViewModel viewModel,
            WorkflowNode targetNode,
            IWorkflowEditorHost host)
        {
            if (viewModel == null || targetNode == null) return;

            // Node Web / HTML UI đang phóng to vừa khung nhìn: cho phép đè lên node khác,
            // không đẩy bố cục workflow khi thả chuột.
            if (targetNode is HtmlUiNode hUi && hUi.IsViewportExpanded) return;
            if (targetNode is WebNode wn && wn.IsViewportExpanded) return;

            var processedNodes = new HashSet<WorkflowNode> { targetNode };
            ResolveCollisionRecursive(viewModel, targetNode, host, processedNodes, 0);
        }

        /// <summary>
        /// Đệ quy resolve collision - đẩy node bị overlap và tiếp tục kiểm tra
        /// </summary>
        private void ResolveCollisionRecursive(
            WorkflowEditorViewModel viewModel,
            WorkflowNode node,
            IWorkflowEditorHost host,
            HashSet<WorkflowNode> processedNodes,
            int depth)
        {
            if (depth > MaxRecursionDepth) return; // Tránh vòng lặp vô hạn

            var nodeBounds = GetNodeBounds(node);
            if (nodeBounds.Width <= 0 || nodeBounds.Height <= 0) return;

            // Tìm tất cả các node khác có thể bị overlap
            var overlappingNodes = new List<(WorkflowNode otherNode, Rect otherBounds, double overlapX, double overlapY)>();

            foreach (var otherNode in viewModel.Nodes)
            {
                if (otherNode == node || processedNodes.Contains(otherNode)) continue;
                if (otherNode.Border == null) continue; // Skip node chưa được render

                var otherBounds = GetNodeBounds(otherNode);
                if (otherBounds.Width <= 0 || otherBounds.Height <= 0) continue;

                // Kiểm tra overlap
                if (nodeBounds.IntersectsWith(otherBounds))
                {
                    // Tính toán overlap amount - đảm bảo giá trị dương
                    double overlapX = Math.Min(
                        Math.Abs(nodeBounds.Right - otherBounds.Left),
                        Math.Abs(otherBounds.Right - nodeBounds.Left)
                    );
                    double overlapY = Math.Min(
                        Math.Abs(nodeBounds.Bottom - otherBounds.Top),
                        Math.Abs(otherBounds.Bottom - nodeBounds.Top)
                    );

                    // Chỉ thêm nếu overlap thực sự xảy ra
                    if (overlapX > 0 && overlapY > 0)
                    {
                        overlappingNodes.Add((otherNode, otherBounds, overlapX, overlapY));
                    }
                }
            }

            // Đẩy các node bị overlap
            foreach (var (otherNode, otherBounds, overlapX, overlapY) in overlappingNodes)
            {
                // Tính toán hướng đẩy tốt nhất (ít di chuyển nhất)
                var pushDirection = CalculatePushDirection(nodeBounds, otherBounds, overlapX, overlapY);

                // Tính toán vị trí mới cho node bị đẩy
                double newX = otherNode.X + pushDirection.X;
                double newY = otherNode.Y + pushDirection.Y;

                // Cập nhật vị trí node bị đẩy
                viewModel.UpdateNodePosition(otherNode, newX, newY);
                host.UpdateNodePosition(otherNode, newX, newY);

                if (otherNode.Border != null)
                {
                    Canvas.SetLeft(otherNode.Border, newX);
                    Canvas.SetTop(otherNode.Border, newY);
                }

                // Cập nhật ports positions
                UpdatePortsPosition(otherNode, host);

                // Cập nhật connection paths cho node bị đẩy
                foreach (var conn in viewModel.Connections)
                {
                    if (conn.FromNode == otherNode || conn.ToNode == otherNode)
                    {
                        host.UpdateConnectionPath(conn);
                    }
                }

                // Đệ quy kiểm tra node bị đẩy có overlap với node khác không
                processedNodes.Add(otherNode);
                ResolveCollisionRecursive(viewModel, otherNode, host, processedNodes, depth + 1);
            }
        }

        /// <summary>
        /// Tính toán hướng đẩy tốt nhất để giải quyết collision
        /// </summary>
        private Vector CalculatePushDirection(Rect nodeBounds, Rect otherBounds, double overlapX, double overlapY)
        {
            // Tính toán khoảng cách từ tâm của node đến tâm của otherNode
            double centerX1 = nodeBounds.Left + nodeBounds.Width / 2;
            double centerY1 = nodeBounds.Top + nodeBounds.Height / 2;
            double centerX2 = otherBounds.Left + otherBounds.Width / 2;
            double centerY2 = otherBounds.Top + otherBounds.Height / 2;

            double dx = centerX2 - centerX1;
            double dy = centerY2 - centerY1;

            // Nếu overlap theo cả hai hướng, chọn hướng có overlap ít hơn
            if (overlapX < overlapY)
            {
                // Đẩy theo trục X
                if (dx > 0)
                    return new Vector(overlapX + MinNodeSpacing, 0); // Đẩy sang phải
                else
                    return new Vector(-(overlapX + MinNodeSpacing), 0); // Đẩy sang trái
            }
            else
            {
                // Đẩy theo trục Y
                if (dy > 0)
                    return new Vector(0, overlapY + MinNodeSpacing); // Đẩy xuống dưới
                else
                    return new Vector(0, -(overlapY + MinNodeSpacing)); // Đẩy lên trên
            }
        }

        /// <summary>
        /// Lấy bounds của node (tương tự ViewportCullingService.GetNodeBounds)
        /// </summary>
        private Rect GetNodeBounds(WorkflowNode node)
        {
            double nodeX = node.X;
            double nodeY = node.Y;

            double nodeWidth = 0;
            double nodeHeight = 0;

            if (node is LoopBodyNode loopBodyNode)
            {
                // LoopBodyNode có Width/Height properties riêng
                nodeWidth = loopBodyNode.Width > 0 ? loopBodyNode.Width :
                           (node.Border?.ActualWidth > 0 ? node.Border.ActualWidth :
                           (node.Border?.Width > 0 ? node.Border.Width : 400));
                nodeHeight = loopBodyNode.Height > 0 ? loopBodyNode.Height :
                            (node.Border?.ActualHeight > 0 ? node.Border.ActualHeight :
                            (node.Border?.Height > 0 ? node.Border.Height : 300));
            }
            else if (node.Border != null)
            {
                // Các node khác: ưu tiên ActualWidth/ActualHeight
                nodeWidth = node.Border.ActualWidth > 0 ? node.Border.ActualWidth :
                          (node.Border.Width > 0 ? node.Border.Width : 0);
                nodeHeight = node.Border.ActualHeight > 0 ? node.Border.ActualHeight :
                           (node.Border.Height > 0 ? node.Border.Height : 0);
            }

            // Nếu node chưa có size hợp lệ, dùng giá trị mặc định
            if (nodeWidth <= 0) nodeWidth = 200;
            if (nodeHeight <= 0) nodeHeight = 100;

            return new Rect(nodeX, nodeY, nodeWidth, nodeHeight);
        }

        /// <summary>
        /// Cập nhật vị trí ports của node
        /// </summary>
        private void UpdatePortsPosition(WorkflowNode node, IWorkflowEditorHost host)
        {
            if (node.IsConditionalNode)
            {
                host.RenderConditionalNodePorts(node);

                var regularPorts = node.Ports
                    .Where(p => p.IsVisible &&
                               !node.ConditionalBranches.Any(b => b.Port == p))
                    .Select(p => p.Position)
                    .Distinct();

                foreach (var position in regularPorts)
                {
                    host.UpdatePortsPositionOnSide(node, position);
                }
            }
            else if (!(node is LoopNode))
            {
                var positions = node.Ports
                    .Where(p => p.IsVisible)
                    .Select(p => p.Position)
                    .Distinct();

                foreach (var position in positions)
                {
                    host.UpdatePortsPositionOnSide(node, position);
                }
            }
        }
    }
}

