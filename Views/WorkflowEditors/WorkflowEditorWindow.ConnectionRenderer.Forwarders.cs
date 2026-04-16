using FlowMy.Models;
using FlowMy.Services.Rendering;
using System;
using System.Windows;
using System.Windows.Media;
using ShapesPath = System.Windows.Shapes.Path;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        #region Connection Rendering (forwarders -> Services/Rendering/ConnectionRenderer)

        private IConnectionRenderer ConnectionRendererService =>
            _connectionRenderer ?? throw new InvalidOperationException("ConnectionRenderer service is not initialized.");

        private void RenderAllConnections()
        {
            if (ViewModel == null) return;

            ConnectionRendererService.RenderAllConnections(
                ViewModel.Connections,
                setSelectedConnection: c => _selectedConnection = c,
                focusWindow: () => Focus(),
                requestDeleteConnection: DeleteConnection);
            
            // Cập nhật viewport culling sau khi render tất cả connections
            _viewportCullingService?.ForceUpdate();

            // Đảm bảo animation line (dash / energy) được áp dụng sau khi render lại toàn bộ connections,
            // ví dụ khi load workflow từ combobox hoặc import JSON.
            UpdateAllConnectionAnimations();
        }

        private void RenderConnection(WorkflowConnection connection)
        {
            ConnectionRendererService.RenderConnection(
                connection,
                setSelectedConnection: c => _selectedConnection = c,
                focusWindow: () => Focus(),
                requestDeleteConnection: DeleteConnection);
            
            // Cập nhật viewport culling sau khi render connection
            _viewportCullingService?.OnConnectionChanged(connection);

            // Khi đang ở chế độ animation, connection mới tạo cũng phải có animation line
            // giống các connection khác, nên cần cập nhật lại animation sau khi render.
            UpdateAllConnectionAnimations();
        }

        private void UpdateConnectionPath(WorkflowConnection connection)
        {
            ConnectionRendererService.UpdateConnectionPath(connection);
            
            // Cập nhật viewport culling khi connection path thay đổi
            _viewportCullingService?.OnConnectionChanged(connection);
        }

        private void UpdateConnectionColor(WorkflowConnection connection)
        {
            ConnectionRendererService.UpdateConnectionColor(connection);
        }

        private void UpdateAllConnectionAnimations()
        {
            if (ViewModel == null) return;
            ConnectionRendererService.UpdateAllConnectionAnimations(ViewModel.Connections);
        }

        private void UpdateAllConnectionColors()
        {
            if (ViewModel == null) return;
            ConnectionRendererService.UpdateAllConnectionColors(ViewModel.Connections);
        }

        private void UpdateAllConnectionPaths()
        {
            if (ViewModel == null) return;
            ConnectionRendererService.UpdateAllConnectionPaths(ViewModel.Connections);
        }

        private ShapesPath CreateConnectionLine(
            Point start,
            Point end,
            Color color,
            bool isDashed,
            PortPosition? startPortPosition = null,
            PortPosition? endPortPosition = null)
        {
            return ConnectionRendererService.CreateConnectionLine(
                start,
                end,
                color,
                isDashed,
                startPortPosition,
                endPortPosition);
        }

        /// <summary>
        /// Xóa connection (helper để dùng chung button click và keyboard delete)
        /// </summary>
        private void DeleteConnection(WorkflowConnection connection)
        {
            _eventService.DeleteConnection(connection);
        }

        #endregion
    }
}

