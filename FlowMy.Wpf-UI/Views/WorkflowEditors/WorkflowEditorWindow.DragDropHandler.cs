using System;
using System.Windows.Input;
using FlowMy.Models;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private Services.Interaction.DragDropHandler DragDropHandlerService =>
            _dragDropHandler ?? throw new InvalidOperationException("DragDropHandler service is not initialized.");

        // ✅ PERFORMANCE: Mouse move throttling to reduce update frequency
        private DateTime _lastNodeMouseMoveUpdate = DateTime.MinValue;
        private const int MouseMoveThrottleMs = 20; // ~50fps max: giảm tải CPU khi graph lớn
        
        private void Node_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragDropHandlerService.NodeMouseDown(sender, e);
        }

        private void Node_MouseMove(object sender, MouseEventArgs e)
        {
            // ✅ OPTIMIZATION: Throttle mouse move events to 60fps max
            var now = DateTime.Now;
            if ((now - _lastNodeMouseMoveUpdate).TotalMilliseconds < MouseMoveThrottleMs)
                return; // Skip this update - too soon since last update
            
            _lastNodeMouseMoveUpdate = now;
            DragDropHandlerService.NodeMouseMove(sender, e);
        }

        private void Node_MouseUp(object sender, MouseButtonEventArgs e)
        {
            DragDropHandlerService.NodeMouseUp(sender, e);
        }

        /// <summary>
        /// Backward-compat helper: một số chỗ (ConditionalNodeRenderer) vẫn gọi.
        /// </summary>
        private void RedrawConnections(WorkflowNode movedNode)
        {
            if (ViewModel == null) return;

            foreach (var conn in ViewModel.Connections)
            {
                if (conn.FromNode == movedNode || conn.ToNode == movedNode)
                {
                    UpdateConnectionPath(conn);
                }
            }
        }
    }
}

