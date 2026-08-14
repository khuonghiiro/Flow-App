using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Utilities
{
    public sealed class MinimapService
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();
        private Canvas _minimapCanvas => Host.MinimapCanvas;
        private ScrollViewer _scrollViewer => Host.ScrollViewer;
        private ScaleTransform _scaleTransform => Host.ScaleTransform;
        private TranslateTransform _translateTransform => Host.TranslateTransform;
        private ScaleTransform? _gridScaleTransform => Host.GridScaleTransform;
        private TranslateTransform? _gridTranslateTransform => Host.GridTranslateTransform;

        public MinimapService(IWorkflowEditorHostAccessor hostAccessor)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void Update()
        {
            if (_minimapCanvas.ActualWidth == 0 || _minimapCanvas.ActualHeight == 0)
            {
                _minimapCanvas.Dispatcher.BeginInvoke(new Action(Update),
                    System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            var viewModel = Host.ViewModel;
            if (viewModel == null) return;

            // Clear minimap và invalidate để tránh ghost effects khi dùng GPU
            _minimapCanvas.Children.Clear();
            if (GpuDetectionHelper.IsGpuAvailable)
            {
                _minimapCanvas.InvalidateVisual();
            }
            
            if (viewModel.Nodes.Count == 0) return;

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (var node in viewModel.Nodes)
            {
                Size size = GetNodeSize(node);
                if (node.X < minX) minX = node.X;
                if (node.Y < minY) minY = node.Y;
                if (node.X + size.Width > maxX) maxX = node.X + size.Width;
                if (node.Y + size.Height > maxY) maxY = node.Y + size.Height;
            }

            // ✅ Include LoopBodyNodes in bounds calculation
            foreach (var node in viewModel.Nodes.OfType<LoopNode>())
            {
                if (node.LoopBodyNode != null)
                {
                    minX = Math.Min(minX, node.LoopBodyNode.X);
                    minY = Math.Min(minY, node.LoopBodyNode.Y);
                    maxX = Math.Max(maxX, node.LoopBodyNode.X + node.LoopBodyNode.Width);
                    maxY = Math.Max(maxY, node.LoopBodyNode.Y + node.LoopBodyNode.Height);
                }
            }

            double width = maxX - minX;
            double height = maxY - minY;
            if (width == 0) width = 1;
            if (height == 0) height = 1;

            double scaleX = _minimapCanvas.ActualWidth / width;
            double scaleY = _minimapCanvas.ActualHeight / height;
            double scale = Math.Min(scaleX, scaleY) * 0.9;

            double scaledWidth = width * scale;
            double scaledHeight = height * scale;
            double offsetX = (_minimapCanvas.ActualWidth - scaledWidth) / 2;
            double offsetY = (_minimapCanvas.ActualHeight - scaledHeight) / 2;

            foreach (var conn in viewModel.Connections)
            {
                Point fromPoint;
                Point toPoint;

                // Đảm bảo luôn có giá trị hợp lệ cho fromPoint
                if (conn.FromPort != null &&
                    IsPortPositionValid(conn.FromNode, conn.FromPort.PositionPoint))
                {
                    fromPoint = conn.FromPort.PositionPoint;
                }
                else
                {
                    // Fallback: tính toán từ node position và port position
                    var fromNode = conn.FromNode;
                    var fromPortPos = conn.FromPort?.Position ?? PortPosition.Right;
                    fromPoint = CalculatePortPosition(fromNode, fromPortPos, false);
                }

                // Đảm bảo luôn có giá trị hợp lệ cho toPoint
                if (conn.ToPort != null &&
                    IsPortPositionValid(conn.ToNode, conn.ToPort.PositionPoint))
                {
                    toPoint = conn.ToPort.PositionPoint;
                }
                else
                {
                    // Fallback: tính toán từ node position và port position
                    var toNode = conn.ToNode;
                    var toPortPos = conn.ToPort?.Position ?? PortPosition.Left;
                    toPoint = CalculatePortPosition(toNode, toPortPos, true);
                }

                // Kiểm tra lại để đảm bảo không có NaN
                if (double.IsNaN(fromPoint.X) || double.IsNaN(fromPoint.Y) ||
                    double.IsNaN(toPoint.X) || double.IsNaN(toPoint.Y))
                {
                    continue; // Skip connection này nếu vẫn có NaN
                }

                // Tính toán vị trí chính xác với coordinate mapping đúng
                double lineX1 = offsetX + (fromPoint.X - minX) * scale;
                double lineY1 = offsetY + (fromPoint.Y - minY) * scale;
                double lineX2 = offsetX + (toPoint.X - minX) * scale;
                double lineY2 = offsetY + (toPoint.Y - minY) * scale;
                
                // Đảm bảo không có NaN hoặc Infinity
                if (double.IsNaN(lineX1) || double.IsNaN(lineY1) || 
                    double.IsNaN(lineX2) || double.IsNaN(lineY2) ||
                    double.IsInfinity(lineX1) || double.IsInfinity(lineY1) ||
                    double.IsInfinity(lineX2) || double.IsInfinity(lineY2))
                {
                    continue; // Skip connection này nếu có giá trị không hợp lệ
                }
                
                var line = new Line
                {
                    X1 = lineX1,
                    Y1 = lineY1,
                    X2 = lineX2,
                    Y2 = lineY2,
                    Stroke = new SolidColorBrush(Colors.LimeGreen),
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true
                };
                
                // Áp dụng GPU optimization cho minimap line
                if (GpuDetectionHelper.IsGpuAvailable)
                {
                    RenderOptions.SetBitmapScalingMode(line, BitmapScalingMode.LowQuality);
                    RenderOptions.SetEdgeMode(line, EdgeMode.Aliased);
                }
                
                _minimapCanvas.Children.Add(line);
            }

            foreach (var node in viewModel.Nodes)
            {
                Size size = GetNodeSize(node);
                double nodeWidth = size.Width;
                double nodeHeight = size.Height;

                // Tính toán vị trí node chính xác với coordinate mapping đúng
                double rectX = offsetX + (node.X - minX) * scale;
                double rectY = offsetY + (node.Y - minY) * scale;
                double rectWidth = nodeWidth * scale;
                double rectHeight = nodeHeight * scale;
                
                // Đảm bảo không có NaN hoặc Infinity
                if (double.IsNaN(rectX) || double.IsNaN(rectY) || 
                    double.IsNaN(rectWidth) || double.IsNaN(rectHeight) ||
                    double.IsInfinity(rectX) || double.IsInfinity(rectY) ||
                    double.IsInfinity(rectWidth) || double.IsInfinity(rectHeight))
                {
                    continue; // Skip node này nếu có giá trị không hợp lệ
                }
                
                var rect = new Rectangle
                {
                    Width = rectWidth,
                    Height = rectHeight,
                    Fill = node.NodeBrush,
                    Stroke = node == viewModel.SelectedNode ? new SolidColorBrush(Colors.Yellow) : new SolidColorBrush(Colors.White),
                    StrokeThickness = node == viewModel.SelectedNode ? 2 : 1,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true
                };

                Canvas.SetLeft(rect, rectX);
                Canvas.SetTop(rect, rectY);
                
                // Áp dụng GPU optimization cho minimap node
                if (GpuDetectionHelper.IsGpuAvailable)
                {
                    RenderOptions.SetBitmapScalingMode(rect, BitmapScalingMode.LowQuality);
                    RenderOptions.SetEdgeMode(rect, EdgeMode.Aliased);
                }
                
                _minimapCanvas.Children.Add(rect);

                // ✅ Thêm Logic hiển thị Loop Body
                if (node is LoopNode loopNode && loopNode.LoopBodyNode != null)
                {
                    var bodyNode = loopNode.LoopBodyNode;
                    var bodyRect = new Rectangle
                    {
                        Width = bodyNode.Width * scale,
                        Height = bodyNode.Height * scale,
                        Fill = new SolidColorBrush(Color.FromArgb(100, 200, 200, 200)), // Semi-transparent gray
                        Stroke = new SolidColorBrush(Colors.White),
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 2, 2 }
                    };

                    Canvas.SetLeft(bodyRect, offsetX + (bodyNode.X - minX) * scale);
                    Canvas.SetTop(bodyRect, offsetY + (bodyNode.Y - minY) * scale);
                    _minimapCanvas.Children.Add(bodyRect);
                }
            }
        }

        public void FitToView()
        {
            var host = Host;
            var viewModel = host.ViewModel;
            if (viewModel == null || viewModel.Nodes.Count == 0) return;

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (var node in viewModel.Nodes)
            {
                Size size = GetNodeSize(node);
                if (node.X < minX) minX = node.X;
                if (node.Y < minY) minY = node.Y;
                if (node.X + size.Width > maxX) maxX = node.X + size.Width;
                if (node.Y + size.Height > maxY) maxY = node.Y + size.Height;
            }

            foreach (var node in viewModel.Nodes.OfType<LoopNode>())
            {
                if (node.LoopBodyNode != null)
                {
                    minX = Math.Min(minX, node.LoopBodyNode.X);
                    minY = Math.Min(minY, node.LoopBodyNode.Y);
                    maxX = Math.Max(maxX, node.LoopBodyNode.X + node.LoopBodyNode.Width);
                    maxY = Math.Max(maxY, node.LoopBodyNode.Y + node.LoopBodyNode.Height);
                }
            }

            double nodesWidth = maxX - minX;
            double nodesHeight = maxY - minY;
            if (nodesWidth <= 0 || nodesHeight <= 0) return;

            double padding = Math.Max(nodesWidth, nodesHeight) * 0.1;
            double totalWidth = nodesWidth + padding * 2;
            double totalHeight = nodesHeight + padding * 2;

            double viewportWidth = _scrollViewer.ViewportWidth;
            double viewportHeight = _scrollViewer.ViewportHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                _scrollViewer.Dispatcher.BeginInvoke(new Action(FitToView),
                    System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            double zoomX = viewportWidth / totalWidth;
            double zoomY = viewportHeight / totalHeight;
            double newZoom = Math.Min(zoomX, zoomY);
            newZoom = Math.Max(host.MinZoom, Math.Min(host.MaxZoom, newZoom));

            double centerX = (minX + maxX) / 2;
            double centerY = (minY + maxY) / 2;

            _scrollViewer.ScrollToHorizontalOffset(0);
            _scrollViewer.ScrollToVerticalOffset(0);

            host.ZoomLevel = newZoom;
            _scaleTransform.ScaleX = newZoom;
            _scaleTransform.ScaleY = newZoom;

            if (_gridScaleTransform != null)
            {
                _gridScaleTransform.ScaleX = newZoom;
                _gridScaleTransform.ScaleY = newZoom;
            }

            double newTranslateX = (viewportWidth / 2) - (centerX * newZoom);
            double newTranslateY = (viewportHeight / 2) - (centerY * newZoom);

            _translateTransform.X = newTranslateX;
            _translateTransform.Y = newTranslateY;

            if (_gridTranslateTransform != null)
            {
                _gridTranslateTransform.X = newTranslateX;
                _gridTranslateTransform.Y = newTranslateY;
            }
        }
        private Size GetNodeSize(WorkflowNode node)
        {
            // LoopBodyNode can exist (and be connected) before its Border is fully loaded;
            // use its own Width/Height properties as the most stable source of truth.
            if (node is LoopBodyNode loopBody)
            {
                return new Size(
                    loopBody.Width > 0 ? loopBody.Width : 400,
                    loopBody.Height > 0 ? loopBody.Height : 300);
            }

            // LoopNode is rendered as a fixed-size diamond (100x100)
            if (node is LoopNode)
            {
                return new Size(100, 100);
            }

            if (node.Border == null) return new Size(150, 80);

            double width = !double.IsNaN(node.Border.Width) ? node.Border.Width : (node.Border.ActualWidth > 0 ? node.Border.ActualWidth : 150);
            double height = !double.IsNaN(node.Border.Height) ? node.Border.Height : (node.Border.ActualHeight > 0 ? node.Border.ActualHeight : (node.Border.MinHeight > 0 ? node.Border.MinHeight : 80));

            return new Size(width, height);
        }

        private bool IsPortPositionValid(WorkflowNode node, Point p)
        {
            if (double.IsNaN(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.X) || double.IsInfinity(p.Y))
                return false;

            // Many ports start at (0,0) until the renderer positions them.
            // Treat (0,0) as invalid unless the owning node is actually near origin.
            if (p.X == 0 && p.Y == 0 && (node.X != 0 || node.Y != 0))
                return false;

            // Also validate the point roughly lies on/near the node bounds to avoid drawing "ghost lines"
            // during load/import before layout has completed.
            var size = GetNodeSize(node);
            var bounds = new Rect(node.X, node.Y, size.Width, size.Height);
            bounds.Inflate(30, 30); // tolerate slight offsets due to port radius / styles

            return bounds.Contains(p);
        }

        private Point CalculatePortPosition(WorkflowNode node, PortPosition position, bool isInput)
        {
            Size nodeSize = GetNodeSize(node);
            double nodeWidth = nodeSize.Width;
            double nodeHeight = nodeSize.Height;

            return position switch
            {
                PortPosition.Left => new Point(node.X, node.Y + nodeHeight / 2.0),
                PortPosition.Right => new Point(node.X + nodeWidth, node.Y + nodeHeight / 2.0),
                PortPosition.Top => new Point(node.X + nodeWidth / 2.0, node.Y),
                PortPosition.Bottom => new Point(node.X + nodeWidth / 2.0, node.Y + nodeHeight),
                _ => isInput 
                    ? new Point(node.X, node.Y + nodeHeight / 2.0) 
                    : new Point(node.X + nodeWidth, node.Y + nodeHeight / 2.0)
            };
        }
    }
}

