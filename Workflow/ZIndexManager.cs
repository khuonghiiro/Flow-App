using FlowMy.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Workflow
{
    /// <summary>
    /// Quản lý z-index của nodes và ports trong workflow canvas
    /// </summary>
    public class ZIndexManager
    {
        private readonly Dictionary<WorkflowNode, int> _nodeOriginalZIndex = new();

        // --- HỆ THỐNG Z-INDEX TIERS (Cách nhau đủ xa để không bao giờ chồng lấn) ---
        // 1. Body Tier: 0 - 499,999 (Chứa node borders)
        // 2. Connection Tier: 500,000 - 999,999 (Đường nối và nút xóa)
        // 3. Port Tier: 1,000,000+ (Các vòng tròn ports)

        private const int ConnectionTierBase = 500000;
        private const int PortTierBase = 1000000;

        // Nội bộ Body Tier:
        private const int BackgroundBase = 100;    // Loop Body nodes
        private const int ForegroundBase = 10000;  // Normal nodes
        private const int LockedBodyZIndexOffset = 50000; // Locked body: nằm trên ForegroundBase (10000+) nhưng dưới SelectedOffset (100000)
        private const int SelectedOffset = 100000; // Offset khi được chọn
        private const int DraggingOffset = 200000; // Offset khi đang kéo

        private int _nextBackgroundZIndex = BackgroundBase;
        private int _nextForegroundZIndex = ForegroundBase;

        /// <summary>
        /// Khởi tạo z-index cho node mới dựa trên loại node (Background vs Foreground)
        /// </summary>
        public void InitializeNodeZIndex(WorkflowNode node, FrameworkElement nodeBorder, int? originalZIndex = null)
        {
            if (!_nodeOriginalZIndex.ContainsKey(node))
            {
                if (originalZIndex.HasValue)
                {
                    _nodeOriginalZIndex[node] = originalZIndex.Value;
                }
                else if (node is LoopBodyNode or AsyncTaskBodyNode or FlowMy.Models.Nodes.BodyContainerNode or FlowMy.Models.Nodes.ActionCanVasNode)
                {
                    // Giống Loop Body: khung vùng body phải nằm dưới các node foreground (~10000+)
                    // để kéo/chuột phải vào node con trên canvas (các node là sibling, không nằm trong Border).
                    _nodeOriginalZIndex[node] = _nextBackgroundZIndex++;
                }
                else
                {
                    _nodeOriginalZIndex[node] = _nextForegroundZIndex++;
                }
            }

            int nodeZIndex = _nodeOriginalZIndex[node];
            Panel.SetZIndex(nodeBorder, nodeZIndex);
        }

        /// <summary>
        /// Set z-index cho port (áp dụng PortTierBase)
        /// </summary>
        public void SetPortZIndex(WorkflowNode node, FrameworkElement portUI)
        {
            if (node.Border == null) return;

            int currentBodyZIndex = Panel.GetZIndex(node.Border);
            // Port luôn nằm trên cùng của Body tương ứng, nhưng trong lớp PortTier
            Panel.SetZIndex(portUI, PortTierBase + currentBodyZIndex);
        }

        /// <summary>
        /// Tăng z-index của node và các ports của nó
        /// </summary>
        public void RaiseNodeZIndex(WorkflowNode node, int zIndex)
        {
            if (node.Border == null) return;

            Panel.SetZIndex(node.Border, zIndex);
            UpdateAllPortsZIndex(node, PortTierBase + zIndex);
        }

        private void UpdateAllPortsZIndex(WorkflowNode node, int portZIndex)
        {
            foreach (var port in node.Ports.Where(p => p.PortUI != null && p.IsVisible))
            {
                Panel.SetZIndex(port.PortUI!, portZIndex);
            }
        }

        public void SelectNode(WorkflowNode node) 
        {
            int baseZ = _nodeOriginalZIndex.ContainsKey(node) ? _nodeOriginalZIndex[node] : ForegroundBase;
            int targetZ = baseZ + SelectedOffset;

            if (node is FlowMy.Models.Nodes.BodyContainerNode bcn)
            {
                // Container should stay behind normal nodes even when selected, unless locked
                targetZ = bcn.FullLockInnerNodes ? baseZ + LockedBodyZIndexOffset + SelectedOffset : baseZ;
            }
            else if (node is LoopBodyNode or AsyncTaskBodyNode or FlowMy.Models.Nodes.ActionCanVasNode)
            {
                // Containers should stay behind normal nodes even when selected
                targetZ = baseZ;
            }

            if (node.Border != null && Panel.GetZIndex(node.Border) == targetZ) return;
            RaiseNodeZIndex(node, targetZ);
        }

        public void DragNode(WorkflowNode node)
        {
            int baseZ = _nodeOriginalZIndex.ContainsKey(node) ? _nodeOriginalZIndex[node] : ForegroundBase;
            if (node is FlowMy.Models.Nodes.BodyContainerNode)
            {
                // Body container should stay behind normal nodes/lines.
                // Chỉ khi FullLockInnerNodes mới nâng z-index lên chặn click vào inner nodes.
                var bcn = (FlowMy.Models.Nodes.BodyContainerNode)node;
                int targetZ = bcn.FullLockInnerNodes ? baseZ + LockedBodyZIndexOffset : baseZ;
                if (node.Border != null && Panel.GetZIndex(node.Border) == targetZ) return;
                RaiseNodeZIndex(node, targetZ);
                return;
            }
            if (node is FlowMy.Models.Nodes.ActionCanVasNode)
            {
                // ActionCanVasNode should stay behind normal nodes/lines.
                if (node.Border != null && Panel.GetZIndex(node.Border) == baseZ) return;
                RaiseNodeZIndex(node, baseZ);
                return;
            }
            int dragZ = baseZ + DraggingOffset;
            if (node.Border != null && Panel.GetZIndex(node.Border) == dragZ) return;
            RaiseNodeZIndex(node, dragZ);
        }

        public void RestoreNodeZIndex(WorkflowNode node)
        {
            if (node.Border == null) return;

            if (_nodeOriginalZIndex.TryGetValue(node, out int originalZIndex))
            {
                // Chỉ khi FullLockInnerNodes → giữ z-index cao hơn inner nodes
                int targetZ = originalZIndex;
                if (node is FlowMy.Models.Nodes.BodyContainerNode bcn && bcn.FullLockInnerNodes)
                    targetZ = originalZIndex + LockedBodyZIndexOffset;

                Panel.SetZIndex(node.Border, targetZ);
                UpdateAllPortsZIndex(node, PortTierBase + targetZ);
            }
        }

        public void RestoreAllNodesZIndex(IEnumerable<WorkflowNode> nodes)
        {
            foreach (var node in nodes) RestoreNodeZIndex(node);
        }

        public void RemoveNode(WorkflowNode node)
        {
            _nodeOriginalZIndex.Remove(node);
        }

        /// <summary>
        /// Cập nhật z-index cho BodyContainerNode khi FullLockInnerNodes thay đổi.
        /// Khi FullLock, nâng z-index lên trên các node con để chặn mọi thao tác chuột.
        /// Khi không FullLock, hạ z-index về vị trí ban đầu (background).
        /// </summary>
        public void SetLockedBodyZIndex(FlowMy.Models.Nodes.BodyContainerNode bodyNode)
        {
            if (bodyNode.Border == null) return;
            if (!_nodeOriginalZIndex.TryGetValue(bodyNode, out int baseZ)) return;

            int targetZ = bodyNode.FullLockInnerNodes ? baseZ + LockedBodyZIndexOffset : baseZ;
            Panel.SetZIndex(bodyNode.Border, targetZ);
            UpdateAllPortsZIndex(bodyNode, PortTierBase + targetZ);
        }
    }
}

