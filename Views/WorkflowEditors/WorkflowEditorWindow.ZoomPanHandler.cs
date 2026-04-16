using System;
using System.Windows;
using System.Windows.Input;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private Services.Interaction.ZoomPanHandler ZoomPanHandlerService =>
            _zoomPanHandler ?? throw new InvalidOperationException("ZoomPanHandler service is not initialized.");

        private void InitializeZoomPan()
        {
            ZoomPanHandlerService.InitializeZoomPan();
        }

        /// <summary>
        /// Tính toán zoom ban đầu phù hợp với kích thước màn hình hiện tại.
        /// Baseline: 1920px → 1.0x, scale giảm dần trên màn nhỏ hơn.
        /// </summary>
        private static double GetResponsiveInitialZoom(double windowWidth)
        {
            // Các breakpoint: (minWidth, scale)
            if (windowWidth >= 1800) return 1.00;
            if (windowWidth >= 1600) return 0.90;
            if (windowWidth >= 1366) return 0.80;
            if (windowWidth >= 1200) return 0.75;
            return 0.65; // < 1200px màn rất nhỏ
        }

        /// <summary>
        /// Áp dụng responsive UI scale dựa theo kích thước màn hình:
        /// - Canvas zoom (ScaleTransform + GridScaleTransform)
        /// - Palette node panel (LayoutTransform)
        /// - Lưu vào Application.Resources["UIScaleFactor"] để dialog dùng
        /// Gọi từ Loaded event VÀ sau khi load workflow.
        /// </summary>
        internal void ApplyResponsiveInitialZoom()
        {
            try
            {
                var windowWidth = ActualWidth > 0 ? ActualWidth
                    : SystemParameters.PrimaryScreenWidth;

                var scale = GetResponsiveInitialZoom(windowWidth);

                // 1. Publish cho toàn app (dialogs sẽ đọc từ đây)
                Application.Current.Resources["UIScaleFactor"] = scale;

                // 2. Canvas ScaleTransform (zoom nodes trên canvas)
                _zoomLevel = scale;
                if (ScaleTransform != null)
                {
                    ScaleTransform.ScaleX = scale;
                    ScaleTransform.ScaleY = scale;
                }
                if (GridScaleTransform != null)
                {
                    GridScaleTransform.ScaleX = scale;
                    GridScaleTransform.ScaleY = scale;
                }

                // 3. Node palette (left menu) — LayoutTransform để scale icon nodes
                if (NodeTemplatesPanel != null)
                {
                    NodeTemplatesPanel.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
                }
            }
            catch { /* best-effort */ }
        }

        private void ScrollToCenter()
        {
            ZoomPanHandlerService.ScrollToCenter();
        }

        private Point GetViewportCenter()
        {
            return ZoomPanHandlerService.GetViewportCenter();
        }

        private Point GetRandomViewportPosition()
        {
            return ZoomPanHandlerService.GetRandomViewportPosition();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ZoomPanHandlerService.PreviewMouseWheel(e);
            
            // Thông báo viewport đã thay đổi (zoom) để cập nhật culling
            _viewportCullingService?.OnViewportChanged();
        }

        private void UpdateCanvasSize()
        {
            ZoomPanHandlerService.UpdateCanvasSize();
        }
    }
}

