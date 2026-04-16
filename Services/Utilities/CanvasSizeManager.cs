using System;
using System.Collections.Generic;
using System.Linq;
using FlowMy.Models;
using FlowMy.Services.Interaction;

namespace FlowMy.Services.Utilities
{
    public sealed class CanvasSizeManager
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly double _minWidth;
        private readonly double _minHeight;
        private readonly double _padding;

        public CanvasSizeManager(IWorkflowEditorHostAccessor hostAccessor, double minWidth = 5000, double minHeight = 5000, double padding = 500)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _minWidth = minWidth;
            _minHeight = minHeight;
            _padding = padding;
        }

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public void UpdateSize(IEnumerable<WorkflowNode> nodes)
        {
            var canvas = Host.WorkflowCanvas;
            var nodeList = nodes?.ToList() ?? new List<WorkflowNode>();
            
            // Lấy TranslateTransform và viewport để tính toán canvas size bao gồm cả vùng đã pan
            double translateX = Host.TranslateTransform.X;
            double translateY = Host.TranslateTransform.Y;
            double zoomLevel = Host.ZoomLevel;
            double viewportWidth = Host.ScrollViewer.ViewportWidth;
            double viewportHeight = Host.ScrollViewer.ViewportHeight;
            
            // Tính toán vùng canvas đang được hiển thị (trong canvas coordinates)
            // Viewport trong scroll coordinates được chuyển đổi sang canvas coordinates
            double viewportLeft = Host.ScrollViewer.HorizontalOffset;
            double viewportTop = Host.ScrollViewer.VerticalOffset;
            
            // Canvas coordinates của viewport bounds
            double canvasViewportLeft = (viewportLeft - translateX) / zoomLevel;
            double canvasViewportTop = (viewportTop - translateY) / zoomLevel;
            double canvasViewportRight = canvasViewportLeft + viewportWidth / zoomLevel;
            double canvasViewportBottom = canvasViewportTop + viewportHeight / zoomLevel;
            
            if (nodeList.Count == 0)
            {
                // Đảm bảo canvas size bao gồm cả viewport đang hiển thị
                double minWidth = Math.Max(_minWidth, Math.Max(Math.Abs(canvasViewportLeft), Math.Abs(canvasViewportRight)) * 2 + _padding * 2);
                double minHeight = Math.Max(_minHeight, Math.Max(Math.Abs(canvasViewportTop), Math.Abs(canvasViewportBottom)) * 2 + _padding * 2);
                canvas.Width = minWidth;
                canvas.Height = minHeight;
                return;
            }

            double minX = nodeList.Min(n => n.X);
            double minY = nodeList.Min(n => n.Y);
            double maxX = nodeList.Max(n => n.X + (n.Border?.Width ?? 150));
            double maxY = nodeList.Max(n => n.Y + (n.Border?.Height ?? 80));
            
            // Mở rộng bounds để bao gồm cả viewport đang hiển thị
            minX = Math.Min(minX, canvasViewportLeft - _padding);
            minY = Math.Min(minY, canvasViewportTop - _padding);
            maxX = Math.Max(maxX, canvasViewportRight + _padding);
            maxY = Math.Max(maxY, canvasViewportBottom + _padding);

            double width = Math.Max(_minWidth, maxX - minX + _padding * 2);
            double height = Math.Max(_minHeight, maxY - minY + _padding * 2);

            // WorkflowCanvas >= 20000 khi có lưới (Lines/Dots) để pan hoạt động toàn vùng
            if (Host.GridCanvas.Children.Count > 0)
            {
                width = Math.Max(width, 20000);
                height = Math.Max(height, 20000);
            }

            canvas.Width = width;
            canvas.Height = height;
        }
    }
}

