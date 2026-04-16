using System.Collections.Generic;
using FlowMy.Models;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private WorkflowConnection? _executionActiveConnection;
        private readonly HashSet<WorkflowConnection> _executionPinnedConnections = new();

        private void ApplyExecutionConnectionHighlight(WorkflowConnection? active)
        {
            if (ViewModel == null) return;

            if (ReferenceEquals(_executionActiveConnection, active))
            {
                // Active connection không đổi nhưng vẫn có thể cần refresh (đổi màu năng lượng, bật/tắt animation...)
                RefreshExecutionConnectionHighlight();
                return;
            }

            var previous = _executionActiveConnection;
            _executionActiveConnection = active;

            // Track pinned connections (multi-active): keep them active across body traversal,
            // and auto-disable them when they get unpinned.
            var pinnedNow = new List<WorkflowConnection>();
            foreach (var c in ViewModel.Connections)
            {
                if (c.IsExecutionPinned) pinnedNow.Add(c);
            }

            // Turn off any previously pinned connections that are no longer pinned (unless it's the current active)
            var turnedOff = new List<WorkflowConnection>();
            foreach (var oldPinned in _executionPinnedConnections.ToArray())
            {
                if (!oldPinned.IsExecutionPinned && !ReferenceEquals(oldPinned, active))
                {
                    oldPinned.IsExecutionActive = false;
                    turnedOff.Add(oldPinned);
                    _executionPinnedConnections.Remove(oldPinned);
                }
            }

            // Ensure pinned connections are active and tracked
            foreach (var p in pinnedNow)
            {
                p.IsExecutionActive = true;
                _executionPinnedConnections.Add(p);
            }

            // null giữa các bước (OnEnteringNode(null)) KHÔNG được coi là hết workflow — nếu không animation năng lượng bị tắt hết.
            // Chỉ tắt toàn bộ khi không còn bất kỳ lane thủ công/auto nào, hoặc khi tới End node.
            bool isReachingEndNode = active?.ToNode?.Type == NodeType.End;
            bool isWorkflowFullyIdle = active == null && !ViewModel.IsAnyExecutionLaneActive;

            if (isReachingEndNode || isWorkflowFullyIdle)
            {
                // Tắt tất cả connections đang active
                var allConnections = new List<WorkflowConnection>(ViewModel.Connections);
                foreach (var c in allConnections)
                {
                    if (c.IsExecutionActive)
                    {
                        c.IsExecutionActive = false;
                        if (!turnedOff.Contains(c)) turnedOff.Add(c);
                    }
                }
                
                // Clear pinned khi hết workflow hoặc đã tới End (không còn nhánh body).
                if (isWorkflowFullyIdle || isReachingEndNode)
                {
                    _executionPinnedConnections.Clear();
                }
            }
            else
            {
                // Current active connection is always active (chỉ khi không phải End node)
                if (active != null) active.IsExecutionActive = true;

                // Previous active connection: turn off if not pinned and not active
                if (previous != null && !previous.IsExecutionPinned && !ReferenceEquals(previous, active))
                {
                    previous.IsExecutionActive = false;
                    if (!turnedOff.Contains(previous)) turnedOff.Add(previous);
                }
            }

            var affected = new List<WorkflowConnection>(4);
            if (previous != null) affected.Add(previous);
            if (active != null && !isReachingEndNode) affected.Add(active);
            foreach (var c in turnedOff)
            {
                if (!affected.Contains(c)) affected.Add(c);
            }
            foreach (var p in _executionPinnedConnections)
            {
                if (!affected.Contains(p)) affected.Add(p);
            }

            // Khi đến End node hoặc workflow kết thúc, update tất cả connections để đảm bảo animation được tắt
            if (isReachingEndNode || isWorkflowFullyIdle)
            {
                affected = new List<WorkflowConnection>(ViewModel.Connections);
            }

            foreach (var c in affected)
            {
                // Update color/thickness/effects and bring to correct Z-index
                UpdateConnectionPath(c);
                UpdateConnectionColor(c);
            }

            // Update dash animation (energy effect)
            ConnectionRendererService.UpdateAllConnectionAnimations(affected);
        }

        private void RefreshExecutionConnectionHighlight()
        {
            var active = _executionActiveConnection;
            if (active == null)
            {
                // Khi active tạm null nhưng vẫn có lane chạy, giữ/refresh các cạnh pinned (async/loop body)
                // để hiệu ứng truyền năng lượng không bị "mất luồng".
                if (ViewModel == null) return;
                var pinned = ViewModel.Connections.Where(c => c.IsExecutionPinned).ToList();
                if (pinned.Count == 0) return;
                foreach (var c in pinned)
                {
                    UpdateConnectionPath(c);
                    UpdateConnectionColor(c);
                }
                ConnectionRendererService.UpdateAllConnectionAnimations(pinned);
                return;
            }

            // Refresh visuals theo config mới
            UpdateConnectionPath(active);
            UpdateConnectionColor(active);
            ConnectionRendererService.UpdateAllConnectionAnimations(new[] { active });
        }
    }
}

