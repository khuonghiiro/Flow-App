using System;
using System.Windows;
using System.Windows.Input;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private Services.Interaction.ConnectionHandler ConnectionHandlerService =>
            _connectionHandler ?? throw new InvalidOperationException("ConnectionHandler service is not initialized.");

        // ✅ PERFORMANCE: Canvas mouse move throttling for connection dragging
        private DateTime _lastCanvasMouseMoveUpdate = DateTime.MinValue;
        private const int CanvasMouseMoveThrottleMs = 16; // ~60fps max
        
        private void Port_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ConnectionHandlerService.PortMouseDown(sender, e);
        }

        private void Port_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ConnectionHandlerService.PortMouseUp(sender, e);
        }

        private void WorkflowCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // ✅ OPTIMIZATION: Throttle canvas mouse move to 60fps max
            var now = DateTime.Now;
            if ((now - _lastCanvasMouseMoveUpdate).TotalMilliseconds < CanvasMouseMoveThrottleMs)
                return; // Skip this update
            
            _lastCanvasMouseMoveUpdate = now;
            ConnectionHandlerService.WorkflowCanvasMouseMove(sender, e);
        }

        private void WorkflowCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ConnectionHandlerService.WorkflowCanvasMouseUp(sender, e);
            
            // Cập nhật viewport culling sau khi pan xong
            _viewportCullingService?.OnViewportChanged();
        }

        private void WorkflowCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ConnectionHandlerService.WorkflowCanvasMouseDown(sender, e);
        }
    }
}

