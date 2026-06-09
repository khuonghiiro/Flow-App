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
        private const double MinNodeSpacing = 20.0; // Khoảng cách tối thiểu giữa các node
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
            BodyContainerNode? lockedBodyNode = node as BodyContainerNode;
            var targetIsLockedBody = lockedBodyNode?.LockInnerNodes == true;
            var targetBodyRect = targetIsLockedBody
                ? new Rect(lockedBodyNode!.X, lockedBodyNode.Y, lockedBodyNode.BodyWidth, lockedBodyNode.BodyHeight)
                : Rect.Empty;

            foreach (var otherNode in viewModel.Nodes)
            {
                if (otherNode == node || processedNodes.Contains(otherNode)) continue;
                if (otherNode.Border == null) continue; // Skip node chưa được render

                // Nếu otherNode là unlocked BodyContainerNode, skip collision (cho phép đè lên body)
                if (otherNode is BodyContainerNode otherBody && !otherBody.LockInnerNodes)
                {
                    continue;
                }

                // Nếu otherNode nằm trong locked body, skip - không được di chuyển
                var otherOwningLockedBody = FindOwningLockedBody(viewModel, otherNode);
                if (otherOwningLockedBody != null)
                {
                    continue; // Node trong locked body không bị di chuyển
                }

                // Nếu otherNode nằm trong unlocked body (không phải body), nó đứng im
                // node đang di chuyển bị đẩy ra nếu đè lên nó
                var otherOwningBody = FindOwningBody(viewModel, otherNode);
                var otherInUnlockedBody = otherOwningBody != null && !otherOwningBody.LockInnerNodes;

                // Nếu otherNode là locked body, không cho phép node lọt vào trong nó
                if (otherNode is BodyContainerNode otherBodyLocked && otherBodyLocked.LockInnerNodes)
                {
                    var otherBodyRect = new Rect(otherBodyLocked.X, otherBodyLocked.Y, otherBodyLocked.BodyWidth, otherBodyLocked.BodyHeight);
                    if (IsNodeInsideBodyRect(node, otherBodyRect))
                    {
                        // Node đang lọt vào locked body - cần đẩy node ra ngoài
                        overlappingNodes.Add((otherNode, otherBodyRect, 0, 0));
                        continue;
                    }
                }

                // Nếu target là locked body, skip các node nằm bên trong nó
                if (targetIsLockedBody && IsNodeInsideBodyRect(otherNode, targetBodyRect)) continue;

                // Nếu otherNode nằm trong unlocked body, node đang di chuyển bị đẩy ra
                if (otherInUnlockedBody)
                {
                    var otherBounds2 = GetNodeBounds(otherNode);
                    if (otherBounds2.Width <= 0 || otherBounds2.Height <= 0) continue;

                    // Kiểm tra overlap - nếu có, đẩy node đang di chuyển (node) ra
                    if (nodeBounds.IntersectsWith(otherBounds2))
                    {
                        var pushDirection = CalculatePushDirection(nodeBounds, otherBounds2,
                            Math.Min(Math.Abs(nodeBounds.Right - otherBounds2.Left), Math.Abs(otherBounds2.Right - nodeBounds.Left)),
                            Math.Min(Math.Abs(nodeBounds.Bottom - otherBounds2.Top), Math.Abs(otherBounds2.Bottom - nodeBounds.Top)));
                        // Push in opposite direction (push node away from otherNode)
                        pushDirection = new Vector(-pushDirection.X, -pushDirection.Y);

                        var newX = node.X + pushDirection.X;
                        var newY = node.Y + pushDirection.Y;

                        viewModel.UpdateNodePosition(node, newX, newY);
                        host.UpdateNodePosition(node, newX, newY);

                        if (node.Border != null)
                        {
                            Canvas.SetLeft(node.Border, newX);
                            Canvas.SetTop(node.Border, newY);
                        }

                        UpdatePortsPosition(node, host);

                        foreach (var conn in viewModel.Connections)
                        {
                            if (conn.FromNode == node || conn.ToNode == node)
                            {
                                host.UpdateConnectionPath(conn);
                            }
                        }

                        // Đệ quy kiểm tra lại sau khi đẩy
                        ResolveCollisionRecursive(viewModel, node, host, processedNodes, depth + 1);
                    }
                    continue;
                }

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
                double newX, newY;

                // Nếu otherNode là locked body và node đang lọt vào trong nó, đẩy node (target) ra ngoài
                if (otherNode is BodyContainerNode otherBody && otherBody.LockInnerNodes && overlapX == 0 && overlapY == 0)
                {
                    // Đẩy target node ra ngoài locked body
                    var pushDir = CalculatePushDirectionOutOfBody(nodeBounds, otherBounds);
                    newX = node.X + pushDir.X;
                    newY = node.Y + pushDir.Y;

                    MoveNodeWithLockedInnerNodes(viewModel, host, node, newX, newY);

                    // Đệ quy kiểm tra target node có overlap với node khác không
                    ResolveCollisionRecursive(viewModel, node, host, processedNodes, depth + 1);
                    continue;
                }

                // Logic bình thường: đẩy otherNode
                var pushDirection = CalculatePushDirection(nodeBounds, otherBounds, overlapX, overlapY);

                // Nếu otherNode là locked body, nó đứng yên -> phải đẩy ngược lại target node!
                if (otherNode is BodyContainerNode pushedBody && pushedBody.LockInnerNodes)
                {
                    // Lật ngược hướng đẩy để áp dụng cho node (target node)
                    newX = node.X - pushDirection.X;
                    newY = node.Y - pushDirection.Y;

                    MoveNodeWithLockedInnerNodes(viewModel, host, node, newX, newY);

                    // Đệ quy kiểm tra node vừa bị dội ngược có đè lên ai khác không
                    ResolveCollisionRecursive(viewModel, node, host, processedNodes, depth + 1);
                    continue; 
                }

                newX = otherNode.X + pushDirection.X;
                newY = otherNode.Y + pushDirection.Y;

                MoveNodeWithLockedInnerNodes(viewModel, host, otherNode, newX, newY);

                // Đệ quy kiểm tra node bị đẩy có overlap với node khác không
                processedNodes.Add(otherNode);
                ResolveCollisionRecursive(viewModel, otherNode, host, processedNodes, depth + 1);
            }
        }

        private void MoveNodeWithLockedInnerNodes(
            WorkflowEditorViewModel viewModel,
            IWorkflowEditorHost host,
            WorkflowNode nodeToMove,
            double newX,
            double newY)
        {
            double dx = newX - nodeToMove.X;
            double dy = newY - nodeToMove.Y;
            if (Math.Abs(dx) < 0.1 && Math.Abs(dy) < 0.1) return;

            // Tìm các node liên quan cần di chuyển cùng
            var linkedNodes = new HashSet<WorkflowNode>();

            // 1. Nếu là BodyContainerNode bị khóa -> di chuyển các node con
            if (nodeToMove is BodyContainerNode body && body.LockInnerNodes)
            {
                var bodyRect = new Rect(body.X, body.Y, body.BodyWidth, body.BodyHeight);
                foreach (var child in viewModel.Nodes)
                {
                    if (child == body) continue;
                    
                    var childW = child.Border?.ActualWidth > 1 ? child.Border.ActualWidth : 150;
                    var childH = child.Border?.ActualHeight > 1 ? child.Border.ActualHeight : 80;
                    var center = new Point(child.X + childW / 2.0, child.Y + childH / 2.0);
                    
                    if (bodyRect.Contains(center))
                    {
                        linkedNodes.Add(child);
                    }
                }
            }

            // 2. Coi LoopNode và LoopBodyNode là 1 khối duy nhất khi bị đẩy
            if (nodeToMove is LoopNode loopNode && loopNode.LoopBodyNode != null)
            {
                linkedNodes.Add(loopNode.LoopBodyNode);
                // Bắt luôn các node con trong LoopBodyNode
                var loopInnerNodes = CaptureLoopOrAsyncBodyChildren(viewModel, loopNode.LoopBodyNode);
                foreach (var inner in loopInnerNodes) linkedNodes.Add(inner);
            }
            else if (nodeToMove is LoopBodyNode loopBody)
            {
                // Tìm LoopNode cha tương ứng
                var parentLoop = viewModel.Nodes.OfType<LoopNode>().FirstOrDefault(n => n.LoopBodyNode == loopBody);
                if (parentLoop != null) linkedNodes.Add(parentLoop);
                
                // Bắt các node con trong LoopBodyNode
                var loopInnerNodes = CaptureLoopOrAsyncBodyChildren(viewModel, loopBody);
                foreach (var inner in loopInnerNodes) linkedNodes.Add(inner);
            }

            // 3. Coi AsyncTaskNode và AsyncTaskBodyNode là 1 khối duy nhất khi bị đẩy
            if (nodeToMove is AsyncTaskNode asyncNode && asyncNode.AsyncTaskBodyNode != null && asyncNode.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
            {
                linkedNodes.Add(asyncNode.AsyncTaskBodyNode);
                var asyncInnerNodes = CaptureLoopOrAsyncBodyChildren(viewModel, asyncNode.AsyncTaskBodyNode);
                foreach (var inner in asyncInnerNodes) linkedNodes.Add(inner);
            }
            else if (nodeToMove is AsyncTaskBodyNode asyncBody)
            {
                var parentAsync = viewModel.Nodes.OfType<AsyncTaskNode>().FirstOrDefault(n => n.AsyncTaskBodyNode == asyncBody);
                if (parentAsync != null) linkedNodes.Add(parentAsync);
                
                var asyncInnerNodes = CaptureLoopOrAsyncBodyChildren(viewModel, asyncBody);
                foreach (var inner in asyncInnerNodes) linkedNodes.Add(inner);
            }

            // Di chuyển bản thân node
            UpdateNodeSilent(viewModel, host, nodeToMove, newX, newY);

            // Di chuyển các node liên kết
            foreach (var child in linkedNodes)
            {
                UpdateNodeSilent(viewModel, host, child, child.X + dx, child.Y + dy);
            }
        }

        private void UpdateNodeSilent(WorkflowEditorViewModel viewModel, IWorkflowEditorHost host, WorkflowNode node, double x, double y)
        {
            viewModel.UpdateNodePosition(node, x, y);
            host.UpdateNodePosition(node, x, y);
            if (node.Border != null)
            {
                Canvas.SetLeft(node.Border, x);
                Canvas.SetTop(node.Border, y);
            }
            UpdatePortsPosition(node, host);
            foreach (var conn in viewModel.Connections.Where(c => c.FromNode == node || c.ToNode == node))
            {
                host.UpdateConnectionPath(conn);
            }
        }

        private List<WorkflowNode> CaptureLoopOrAsyncBodyChildren(WorkflowEditorViewModel viewModel, WorkflowNode bodyNode)
        {
            // Simple spatial check for loop/async body
            var result = new List<WorkflowNode>();
            double bodyX = bodyNode.X;
            double bodyY = bodyNode.Y;
            double bodyW = 0;
            double bodyH = 0;

            if (bodyNode is LoopBodyNode lb) { bodyW = lb.Width > 0 ? lb.Width : 400; bodyH = lb.Height > 0 ? lb.Height : 300; }
            else if (bodyNode is AsyncTaskBodyNode ab) { bodyW = ab.Width > 0 ? ab.Width : 400; bodyH = ab.Height > 0 ? ab.Height : 300; }

            var rect = new Rect(bodyX, bodyY, bodyW, bodyH);

            foreach (var child in viewModel.Nodes)
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

        private static Point GetNodeCenter(WorkflowNode node)
        {
            double nodeW = node.Border?.ActualWidth > 1 ? node.Border.ActualWidth : 150;
            double nodeH = node.Border?.ActualHeight > 1 ? node.Border.ActualHeight : 80;
            if (node is LoopBodyNode lb) { nodeW = lb.Width > 0 ? lb.Width : nodeW; nodeH = lb.Height > 0 ? lb.Height : nodeH; }
            else if (node is AsyncTaskBodyNode ab) { nodeW = ab.Width > 0 ? ab.Width : nodeW; nodeH = ab.Height > 0 ? ab.Height : nodeH; }
            return new Point(node.X + nodeW / 2.0, node.Y + nodeH / 2.0);
        }

        private static bool IsNodeInsideBodyRect(WorkflowNode node, Rect bodyRect)
        {
            if (node is BodyContainerNode) return false;
            return bodyRect.Contains(GetNodeCenter(node));
        }

        private static BodyContainerNode? FindOwningLockedBody(WorkflowEditorViewModel viewModel, WorkflowNode node)
        {
            if (node is BodyContainerNode) return null;
            foreach (var body in viewModel.Nodes.OfType<BodyContainerNode>())
            {
                if (!body.LockInnerNodes) continue;
                var width = body.BodyWidth > 0 ? body.BodyWidth : (body.Border?.ActualWidth ?? body.Border?.Width ?? 0);
                var height = body.BodyHeight > 0 ? body.BodyHeight : (body.Border?.ActualHeight ?? body.Border?.Height ?? 0);
                if (width <= 0 || height <= 0) continue;

                if (new Rect(body.X, body.Y, width, height).Contains(GetNodeCenter(node)))
                    return body;
            }
            return null;
        }

        private static BodyContainerNode? FindOwningBody(WorkflowEditorViewModel viewModel, WorkflowNode node)
        {
            if (node is BodyContainerNode) return null;
            foreach (var body in viewModel.Nodes.OfType<BodyContainerNode>())
            {
                var width = body.BodyWidth > 0 ? body.BodyWidth : (body.Border?.ActualWidth ?? body.Border?.Width ?? 0);
                var height = body.BodyHeight > 0 ? body.BodyHeight : (body.Border?.ActualHeight ?? body.Border?.Height ?? 0);
                if (width <= 0 || height <= 0) continue;

                if (new Rect(body.X, body.Y, width, height).Contains(GetNodeCenter(node)))
                    return body;
            }
            return null;
        }

        /// <summary>
        /// Tính toán hướng đẩy node ra ngoài locked body
        /// </summary>
        private Vector CalculatePushDirectionOutOfBody(Rect nodeBounds, Rect bodyBounds)
        {
            // Tính toán khoảng cách từ tâm node đến các cạnh của body
            double centerX = nodeBounds.Left + nodeBounds.Width / 2;
            double centerY = nodeBounds.Top + nodeBounds.Height / 2;

            double distToLeft = centerX - bodyBounds.Left;
            double distToRight = bodyBounds.Right - centerX;
            double distToTop = centerY - bodyBounds.Top;
            double distToBottom = bodyBounds.Bottom - centerY;

            // Chọn hướng đẩy ngắn nhất (ra khỏi body)
            double minDist = Math.Min(Math.Min(distToLeft, distToRight), Math.Min(distToTop, distToBottom));
            double pushDistance = minDist + MinNodeSpacing + 30; // Tăng buffer để node nằm xa viền body hơn

            if (minDist == distToLeft)
                return new Vector(-pushDistance, 0); // Đẩy sang trái
            if (minDist == distToRight)
                return new Vector(pushDistance, 0); // Đẩy sang phải
            if (minDist == distToTop)
                return new Vector(0, -pushDistance); // Đẩy lên trên
            return new Vector(0, pushDistance); // Đẩy xuống dưới
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
            else if (node is AsyncTaskBodyNode asyncBodyNode)
            {
                // AsyncTaskBodyNode có Width/Height properties riêng
                nodeWidth = asyncBodyNode.Width > 0 ? asyncBodyNode.Width :
                           (node.Border?.ActualWidth > 0 ? node.Border.ActualWidth :
                           (node.Border?.Width > 0 ? node.Border.Width : 400));
                nodeHeight = asyncBodyNode.Height > 0 ? asyncBodyNode.Height :
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

