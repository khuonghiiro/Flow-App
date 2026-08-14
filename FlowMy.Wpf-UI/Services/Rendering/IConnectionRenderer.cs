using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ShapesPath = System.Windows.Shapes.Path;
using FlowMy.Models;

namespace FlowMy.Services.Rendering
{
    public interface IConnectionRenderer
    {
        void RenderAllConnections(
            IEnumerable<WorkflowConnection> connections,
            Action<WorkflowConnection?> setSelectedConnection,
            Action focusWindow,
            Action<WorkflowConnection> requestDeleteConnection);

        void RenderConnection(
            WorkflowConnection connection,
            Action<WorkflowConnection?> setSelectedConnection,
            Action focusWindow,
            Action<WorkflowConnection> requestDeleteConnection);

        void UpdateConnectionPath(WorkflowConnection connection);
        void UpdateConnectionColor(WorkflowConnection connection);

        void UpdateAllConnectionAnimations(IEnumerable<WorkflowConnection> connections);
        void UpdateAllConnectionColors(IEnumerable<WorkflowConnection> connections);
        void UpdateAllConnectionPaths(IEnumerable<WorkflowConnection> connections);

        ShapesPath CreateConnectionLine(
            Point start,
            Point end,
            Color color,
            bool isDashed,
            PortPosition? startPortPosition,
            PortPosition? endPortPosition);

        /// <summary>
        /// Áp dụng một “cú gió” (impulse) cho line style Windy.
        /// Các style khác sẽ bỏ qua để tránh tốn tài nguyên.
        /// </summary>
        void ApplyWindImpulse(WorkflowConnection connection, double magnitude);

        void RemoveConnectionVisuals(WorkflowConnection connection);
        void ClearAllConnectionVisuals();
    }
}

