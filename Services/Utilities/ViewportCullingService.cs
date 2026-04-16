using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls;

namespace FlowMy.Services.Utilities
{
    /// <summary>
    /// Viewport Culling Service - chỉ render các nodes, connections và lines nằm trong viewport để tăng hiệu suất
    /// Giống như frustum culling trong game 3D: chỉ render những gì nhìn thấy được
    /// </summary>
    public sealed class ViewportCullingService
    {
        public enum CullingPerformanceProfile
        {
            Low,
            Normal,
            High
        }

        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly DispatcherTimer _updateTimer;
        
        // Margin để tránh flicker khi scroll (render thêm một chút ngoài viewport)
        private const double ViewportMargin = 200; // pixels trong canvas coordinates
        
        // Cache viewport bounds để tránh tính toán lại nhiều lần
        private Rect _cachedViewportBounds = Rect.Empty;
        private double _cachedZoomLevel = 1.0;
        private bool _isEnabled = true;
        private bool _forceShowTitleForVisibleNodes;
        private bool _focusRunningNodesWhenExecuting;
        private CullingPerformanceProfile _performanceProfile = CullingPerformanceProfile.Normal;
        private bool _useStrictViewportCulling;
        
        // Cache viewport bounds để tránh tính toán lại nhiều lần trong OnNodeChanged/OnConnectionChanged
        private Rect _cachedViewportBoundsForNodeCheck = Rect.Empty;
        private DateTime _lastViewportBoundsUpdate = DateTime.MinValue;
        private const int ViewportBoundsCacheMs = 50; // Cache viewport bounds trong 50ms

        /// <summary>Tránh kẹt node Collapsed sau khi hết chạy: lúc đang chạy dùng ShowExecutionPathNodes, khi dừng phải chạy lại culling dù viewport không đổi.</summary>
        private bool _lastViewModelIsExecuting;

        // Cache visibility state để tránh set Visibility lặp lại quá nhiều.
        private readonly Dictionary<string, bool> _nodeVisibilityCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _connectionVisibilityCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _executedNodeIds = new(StringComparer.OrdinalIgnoreCase);
        public event Action<int, int>? VisibilityStatsChanged;
        
        /// <summary>
        /// Lấy viewport bounds với cache để tránh tính toán lại nhiều lần
        /// </summary>
        private Rect GetCachedViewportBounds()
        {
            var now = DateTime.Now;
            if (_cachedViewportBoundsForNodeCheck.IsEmpty || 
                (now - _lastViewportBoundsUpdate).TotalMilliseconds > ViewportBoundsCacheMs)
            {
                _cachedViewportBoundsForNodeCheck = CalculateViewportBounds();
                _lastViewportBoundsUpdate = now;
            }
            return _cachedViewportBoundsForNodeCheck;
        }

        public ViewportCullingService(IWorkflowEditorHostAccessor hostAccessor)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            
            // Timer để cập nhật viewport culling khi scroll/zoom (debounced)
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Tăng từ 50ms lên 100ms để giảm CPU usage
            };
            _updateTimer.Tick += (s, e) =>
            {
                _updateTimer.Stop();
                UpdateViewportCulling();
            };
            ApplyPerformanceProfileSettings();
        }

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        /// <summary>
        /// Bật/tắt viewport culling
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    if (value)
                    {
                        UpdateViewportCulling();
                    }
                    else
                    {
                        // Hiển thị tất cả nodes và connections khi tắt culling
                        ShowAllNodes();
                        ShowAllConnections();
                    }
                }
            }
        }

        /// <summary>
        /// Khi bật, node nào đang visible sẽ luôn hiển thị title (không ép theo TitleDisplayMode).
        /// </summary>
        public bool ForceShowTitleForVisibleNodes
        {
            get => _forceShowTitleForVisibleNodes;
            set => _forceShowTitleForVisibleNodes = value;
        }

        /// <summary>
        /// Khi workflow đang chạy, chỉ hiển thị node đang chạy (và connection liên quan) trong vùng viewport.
        /// </summary>
        public bool FocusRunningNodesWhenExecuting
        {
            get => _focusRunningNodesWhenExecuting;
            set => _focusRunningNodesWhenExecuting = value;
        }

        public CullingPerformanceProfile PerformanceProfile
        {
            get => _performanceProfile;
            set
            {
                if (_performanceProfile == value) return;
                _performanceProfile = value;
                ApplyPerformanceProfileSettings();
                ForceUpdate();
            }
        }

        public bool UseStrictViewportCulling
        {
            get => _useStrictViewportCulling;
            set
            {
                if (_useStrictViewportCulling == value) return;
                _useStrictViewportCulling = value;
                ForceUpdate();
            }
        }

        /// <summary>
        /// Tính toán viewport bounds trong canvas coordinates
        /// </summary>
        private Rect CalculateViewportBounds()
        {
            var host = Host;
            var scrollViewer = host.ScrollViewer;
            
            if (scrollViewer.ViewportWidth <= 0 || scrollViewer.ViewportHeight <= 0)
                return Rect.Empty;

            // Lấy viewport trong scroll viewer coordinates
            double viewportLeft = scrollViewer.HorizontalOffset;
            double viewportTop = scrollViewer.VerticalOffset;
            double viewportWidth = scrollViewer.ViewportWidth;
            double viewportHeight = scrollViewer.ViewportHeight;

            // Chuyển đổi sang canvas coordinates (accounting for zoom và translate)
            double zoom = host.ZoomLevel;
            double translateX = host.TranslateTransform.X;
            double translateY = host.TranslateTransform.Y;

            // ⚠️ QUAN TRỌNG: Canvas coordinates = (scroll coordinates - translate) / zoom
            // Nhưng cần cẩn thận với edge cases khi zoom < 1 hoặc translate khác 0
            double canvasLeft = (viewportLeft - translateX) / zoom;
            double canvasTop = (viewportTop - translateY) / zoom;
            double canvasWidth = viewportWidth / zoom;
            double canvasHeight = viewportHeight / zoom;

            // Đảm bảo không có NaN hoặc Infinity
            if (double.IsNaN(canvasLeft) || double.IsInfinity(canvasLeft) ||
                double.IsNaN(canvasTop) || double.IsInfinity(canvasTop) ||
                double.IsNaN(canvasWidth) || double.IsInfinity(canvasWidth) ||
                double.IsNaN(canvasHeight) || double.IsInfinity(canvasHeight))
            {
                return Rect.Empty;
            }

            // Thêm margin động theo zoom + density để giảm pop-in khi pan nhanh trên workflow lớn.
            var nodeCount = host.ViewModel?.Nodes?.Count ?? 0;
            double margin = CalculateDynamicViewportMargin(zoom, nodeCount, _performanceProfile);
            return new Rect(
                canvasLeft - margin,
                canvasTop - margin,
                canvasWidth + (margin * 2),
                canvasHeight + (margin * 2)
            );
        }

        private static double CalculateDynamicViewportMargin(double zoom, int nodeCount, CullingPerformanceProfile profile)
        {
            var zoomFactor = Math.Max(1.0, 1.35 / Math.Max(0.2, zoom));
            var densityBoost = nodeCount switch
            {
                >= 400 => 2.0,
                >= 250 => 1.7,
                >= 120 => 1.45,
                >= 60 => 1.25,
                _ => 1.0
            };

            var profileBoost = profile switch
            {
                CullingPerformanceProfile.Low => 1.45,
                CullingPerformanceProfile.High => 0.9,
                _ => 1.15
            };

            var margin = ViewportMargin * zoomFactor * densityBoost * profileBoost;
            return Math.Clamp(margin, 180, 1100);
        }

        private void ApplyPerformanceProfileSettings()
        {
            _updateTimer.Interval = _performanceProfile switch
            {
                CullingPerformanceProfile.Low => TimeSpan.FromMilliseconds(150),
                CullingPerformanceProfile.High => TimeSpan.FromMilliseconds(70),
                _ => TimeSpan.FromMilliseconds(100)
            };
        }

        /// <summary>
        /// Tính toán bounds của node trong canvas coordinates
        /// ⚠️ Đặc biệt xử lý LoopBodyNode - có Width/Height properties riêng và có thể resize
        /// </summary>
        private Rect GetNodeBounds(WorkflowNode node)
        {
            double nodeX = node.X;
            double nodeY = node.Y;
            
            double nodeWidth = 0;
            double nodeHeight = 0;
            
            if (node is LoopBodyNode loopBodyNode)
            {
                // LoopBodyNode có Width/Height properties riêng - ưu tiên properties này
                nodeWidth = loopBodyNode.Width > 0 ? loopBodyNode.Width : 
                           (node.Border?.ActualWidth > 0 ? node.Border.ActualWidth : 
                           (node.Border?.Width > 0 ? node.Border.Width : 400));
                nodeHeight = loopBodyNode.Height > 0 ? loopBodyNode.Height : 
                            (node.Border?.ActualHeight > 0 ? node.Border.ActualHeight : 
                            (node.Border?.Height > 0 ? node.Border.Height : 300));
            }
            else if (node.Border != null)
            {
                // Các node khác: ưu tiên ActualWidth/ActualHeight (đã được measure), sau đó mới dùng Width/Height
                nodeWidth = node.Border.ActualWidth > 0 ? node.Border.ActualWidth : 
                          (node.Border.Width > 0 ? node.Border.Width : 0);
                nodeHeight = node.Border.ActualHeight > 0 ? node.Border.ActualHeight : 
                           (node.Border.Height > 0 ? node.Border.Height : 0);
            }

            // Nếu node chưa có size hợp lệ, dùng giá trị mặc định lớn hơn để đảm bảo không bị ẩn nhầm
            // Các node có thể có size khác nhau: InputNode (280x180), LoopNode (240x160), ConditionalNode (200xvariable)
            if (nodeWidth <= 0) nodeWidth = 300; // Dùng giá trị lớn hơn để an toàn
            if (nodeHeight <= 0) nodeHeight = 200; // Dùng giá trị lớn hơn để an toàn

            return new Rect(nodeX, nodeY, nodeWidth, nodeHeight);
        }

        /// <summary>
        /// Kiểm tra node có nằm trong viewport không
        /// QUAN TRỌNG: Nếu node chỉ một phần trong viewport thì vẫn phải hiển thị (intersection)
        /// </summary>
        private bool IsNodeInViewport(WorkflowNode node, Rect viewportBounds)
        {
            // ⚠️ QUAN TRỌNG: Nếu node chưa có Border (chưa được render), luôn coi là visible
            // để tránh ẩn node trước khi nó được render
            if (node.Border == null) return true;

            Rect nodeBounds = GetNodeBounds(node);

            // ⚠️ QUAN TRỌNG: Kiểm tra intersection - nếu node chỉ một phần trong viewport thì vẫn hiển thị
            // Không cần mở rộng viewport bounds quá nhiều vì IntersectsWith đã đủ để kiểm tra partial intersection
            return viewportBounds.IntersectsWith(nodeBounds);
        }

        /// <summary>
        /// Kiểm tra connection có nằm trong viewport không
        /// Connection được coi là visible nếu:
        /// 1. Ít nhất một endpoint (FromNode hoặc ToNode) nằm trong viewport, HOẶC
        /// 2. Đường line intersect với viewport
        /// </summary>
        private bool IsConnectionInViewport(WorkflowConnection connection, Rect viewportBounds)
        {
            if (connection.FromNode == null || connection.ToNode == null)
                return false;

            // Kiểm tra nếu ít nhất một node nằm trong viewport
            bool fromNodeVisible = IsNodeInViewport(connection.FromNode, viewportBounds);
            bool toNodeVisible = IsNodeInViewport(connection.ToNode, viewportBounds);

            if (fromNodeVisible || toNodeVisible)
                return true;

            // Nếu cả hai node đều ngoài viewport, kiểm tra xem line có intersect với viewport không
            // Lấy điểm start và end của connection
            Point startPoint, endPoint;

            if (connection.FromPort != null && connection.ToPort != null)
            {
                startPoint = GetValidPortPosition(connection.FromPort, connection.FromNode, connection.FromPort.IsInput);
                endPoint = GetValidPortPosition(connection.ToPort, connection.ToNode, connection.ToPort.IsInput);
            }
            else
            {
                // Fallback: dùng port position mặc định của node
                startPoint = connection.IsFromInput
                    ? connection.FromNode.InputPortPosition
                    : connection.FromNode.OutputPortPosition;
                endPoint = connection.IsFromInput
                    ? connection.ToNode.OutputPortPosition
                    : connection.ToNode.InputPortPosition;
            }

            // Kiểm tra xem line segment có intersect với viewport không
            return LineIntersectsRect(startPoint, endPoint, viewportBounds);
        }

        /// <summary>
        /// Lấy vị trí port hợp lệ (helper method)
        /// </summary>
        private static Point GetValidPortPosition(NodePort? port, WorkflowNode node, bool isInput)
        {
            if (port != null &&
                !double.IsNaN(port.PositionPoint.X) &&
                !double.IsNaN(port.PositionPoint.Y) &&
                !double.IsInfinity(port.PositionPoint.X) &&
                !double.IsInfinity(port.PositionPoint.Y))
            {
                return port.PositionPoint;
            }

            // Fallback: dùng vị trí mặc định
            return isInput
                ? new Point(node.X, node.Y + 40)
                : new Point(node.X + 150, node.Y + 40);
        }

        /// <summary>
        /// Kiểm tra line segment có intersect với rectangle không
        /// </summary>
        private static bool LineIntersectsRect(Point p1, Point p2, Rect rect)
        {
            // Kiểm tra nếu cả hai điểm đều nằm trong rect
            if (rect.Contains(p1) || rect.Contains(p2))
                return true;

            // Kiểm tra nếu line segment intersect với bất kỳ cạnh nào của rect
            // Top edge
            if (LineIntersectsLine(p1, p2, new Point(rect.Left, rect.Top), new Point(rect.Right, rect.Top)))
                return true;
            // Bottom edge
            if (LineIntersectsLine(p1, p2, new Point(rect.Left, rect.Bottom), new Point(rect.Right, rect.Bottom)))
                return true;
            // Left edge
            if (LineIntersectsLine(p1, p2, new Point(rect.Left, rect.Top), new Point(rect.Left, rect.Bottom)))
                return true;
            // Right edge
            if (LineIntersectsLine(p1, p2, new Point(rect.Right, rect.Top), new Point(rect.Right, rect.Bottom)))
                return true;

            return false;
        }

        /// <summary>
        /// Kiểm tra hai line segments có intersect không
        /// </summary>
        private static bool LineIntersectsLine(Point p1, Point p2, Point p3, Point p4)
        {
            double d = (p2.X - p1.X) * (p4.Y - p3.Y) - (p2.Y - p1.Y) * (p4.X - p3.X);
            if (Math.Abs(d) < 0.0001) return false; // Parallel lines

            double t = ((p3.X - p1.X) * (p4.Y - p3.Y) - (p3.Y - p1.Y) * (p4.X - p3.X)) / d;
            double u = ((p3.X - p1.X) * (p2.Y - p1.Y) - (p3.Y - p1.Y) * (p2.X - p1.X)) / d;

            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }

        /// <summary>
        /// Cập nhật visibility của nodes dựa trên viewport
        /// </summary>
        public void UpdateViewportCulling()
        {
            if (!_isEnabled) return;

            var host = Host;
            var viewModel = host.ViewModel;
            if (viewModel == null) return;

            var wasExecuting = _lastViewModelIsExecuting;
            var isExecutingNow = viewModel.IsExecuting;
            var justEnteredExecutingMode = !wasExecuting && isExecutingNow;
            var justLeftExecutingMode = wasExecuting && !isExecutingNow;
            _lastViewModelIsExecuting = isExecutingNow;

            if (justEnteredExecutingMode)
            {
                _executedNodeIds.Clear();
            }
            else if (justLeftExecutingMode)
            {
                _executedNodeIds.Clear();
            }

            // ⚠️ QUAN TRỌNG: Khi đang load workflow, disable viewport culling để tránh lag
            // Sẽ enable lại sau khi load xong
            if (viewModel.IsLoading)
            {
                // Hiển thị tất cả nodes và connections khi đang load để tránh flicker
                ShowAllNodes();
                ShowAllConnections();
                PublishVisibilityStats(viewModel.Nodes.Count, viewModel.Nodes.Count);
                return;
            }

            // Khi đang chạy vẫn dùng culling theo viewport (không ẩn node ngoài path Start→End) để luôn thấy node đang xử lý / nhánh song song.
            var viewportBounds = CalculateViewportBounds();
            
            // Nếu viewport không hợp lệ, hiển thị tất cả
            if (viewportBounds.IsEmpty || viewportBounds.Width <= 0 || viewportBounds.Height <= 0)
            {
                ShowAllNodes();
                ShowAllConnections();
                PublishVisibilityStats(viewModel.Nodes.Count, viewModel.Nodes.Count);
                return;
            }

            if (_focusRunningNodesWhenExecuting && isExecutingNow)
            {
                UpdateRunningNodesOnly(viewModel, viewportBounds);
                return;
            }

            // Kiểm tra xem viewport có thay đổi đáng kể không
            // Giảm độ nhạy để tránh quá nhiều updates (tối ưu CPU)
            var movementThreshold = _performanceProfile switch
            {
                CullingPerformanceProfile.High => 20.0,
                CullingPerformanceProfile.Low => 70.0,
                _ => 40.0
            };

            bool viewportChanged = _cachedViewportBounds.IsEmpty ||
                                   !_cachedViewportBounds.IntersectsWith(viewportBounds) ||
                                   Math.Abs(_cachedZoomLevel - host.ZoomLevel) > 0.01 ||
                                   Math.Abs(_cachedViewportBounds.Left - viewportBounds.Left) > movementThreshold ||
                                   Math.Abs(_cachedViewportBounds.Top - viewportBounds.Top) > movementThreshold ||
                                   Math.Abs(_cachedViewportBounds.Width - viewportBounds.Width) > movementThreshold ||
                                   Math.Abs(_cachedViewportBounds.Height - viewportBounds.Height) > movementThreshold;

            if (!viewportChanged && !_cachedViewportBounds.IsEmpty && !justLeftExecutingMode)
            {
                // Viewport chưa thay đổi đáng kể, không cần update
                return;
            }

            _cachedViewportBounds = viewportBounds;
            _cachedZoomLevel = host.ZoomLevel;
            
            // Cập nhật cache cho OnNodeChanged/OnConnectionChanged
            _cachedViewportBoundsForNodeCheck = viewportBounds;
            _lastViewportBoundsUpdate = DateTime.Now;

            CleanupVisibilityCaches(viewModel);

            // Tính trước viewport mở rộng; strict mode thì gần như chỉ giữ trong màn để cắt gọn.
            // double margin = _useStrictViewportCulling
            //    ? (_performanceProfile == CullingPerformanceProfile.High ? 40 : 80)

            // Tính trước viewport mở rộng; strict mode thì gần như chỉ giữ trong màn để cắt gọn.
            double margin = _useStrictViewportCulling
                ? _performanceProfile switch
                {
                    CullingPerformanceProfile.High => 6,
                    CullingPerformanceProfile.Normal => 12,
                    _ => 20
                }
                : (host.ZoomLevel > 1.0 ? 800 : 400);
            Rect expandedViewport = new Rect(
                viewportBounds.Left - margin,
                viewportBounds.Top - margin,
                viewportBounds.Width + (margin * 2),
                viewportBounds.Height + (margin * 2)
            );
            Rect ultraExpandedViewport = new Rect(
                viewportBounds.Left - (margin * 2),
                viewportBounds.Top - (margin * 2),
                viewportBounds.Width + (margin * 4),
                viewportBounds.Height + (margin * 4)
            );

            // Cập nhật visibility cho tất cả nodes
            int visibleCount = 0;
            int hiddenCount = 0;

            foreach (var node in viewModel.Nodes)
            {
                // ⚠️ QUAN TRỌNG: Nếu node chưa có Border, bỏ qua (sẽ được kiểm tra lại khi render)
                // Không ẩn node chưa được render để tránh mất node
                if (node.Border == null)
                {
                    visibleCount++; // Coi như visible để không bị mất
                    continue;
                }

                // ✅ Tối ưu: Tính toán node bounds một lần và tái sử dụng
                double nodeWidth = node.Border.ActualWidth > 0 ? node.Border.ActualWidth : 
                                  (node.Border.Width > 0 ? node.Border.Width : 300);
                double nodeHeight = node.Border.ActualHeight > 0 ? node.Border.ActualHeight : 
                                   (node.Border.Height > 0 ? node.Border.Height : 200);
                Rect nodeBounds = new Rect(node.X, node.Y, nodeWidth, nodeHeight);
                
                bool shouldBeVisible = IsNodeInViewport(node, viewportBounds);
                
                // ✅ Tối ưu: Chỉ kiểm tra lại nếu node không visible và đang trong viewport expanded
                // Giảm số lần kiểm tra từ 3 xuống 1-2 để tăng performance
                if (!shouldBeVisible)
                {
                    if (!_useStrictViewportCulling && expandedViewport.IntersectsWith(nodeBounds))
                    {
                        shouldBeVisible = true;
                    }
                    // ✅ Tối ưu: Chỉ kiểm tra thêm nếu node đang visible (để tránh flicker)
                    // Không cần kiểm tra 3 lần như trước
                    else if (!_useStrictViewportCulling && node.Border.Visibility == Visibility.Visible)
                    {
                        // Giữ nguyên visible nếu đang visible để tránh flicker
                        // Chỉ ẩn khi chắc chắn ngoài viewport rất xa
                        if (ultraExpandedViewport.IntersectsWith(nodeBounds))
                        {
                            shouldBeVisible = true;
                        }
                    }
                }
                
                // Cập nhật visibility của node border + các visual phụ thuộc.
                ApplyNodeVisibility(node, shouldBeVisible);
                ApplyAsyncTaskContainerVisibility(node, shouldBeVisible);
                ApplyConditionalBranchVisualsVisibility(node, shouldBeVisible);

                // Cập nhật visibility của ports
                foreach (var port in node.Ports)
                {
                    if (port.PortUI != null)
                    {
                        var targetVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                        if (port.PortUI.Visibility != targetVisibility)
                        {
                            port.PortUI.Visibility = targetVisibility;
                        }
                    }
                }

                // ✅ Cập nhật visibility của title text block dựa trên viewport
                UpdateTitleVisibility(node, shouldBeVisible);

                // Đặc biệt xử lý LoopNode (có LoopBodyNode)
                if (node is LoopNode loopNode && loopNode.LoopBodyNode != null)
                {
                    bool bodyVisible = IsNodeInViewport(loopNode.LoopBodyNode, viewportBounds);
                    if (loopNode.LoopBodyNode.Border != null && loopNode.LoopBodyNode.Border.Visibility != (bodyVisible ? Visibility.Visible : Visibility.Collapsed))
                    {
                        loopNode.LoopBodyNode.Border.Visibility = bodyVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                    
                    // Ports của LoopBodyNode
                    foreach (var port in loopNode.LoopBodyNode.Ports)
                    {
                        if (port.PortUI != null)
                        {
                            var targetVisibility = bodyVisible ? Visibility.Visible : Visibility.Collapsed;
                            if (port.PortUI.Visibility != targetVisibility)
                            {
                                port.PortUI.Visibility = targetVisibility;
                            }
                        }
                    }
                }

                if (shouldBeVisible) visibleCount++;
                else hiddenCount++;
            }

            // Cập nhật visibility cho tất cả connections
            int visibleConnections = 0;
            int hiddenConnections = 0;

            foreach (var connection in viewModel.Connections)
            {
                bool inViewport = IsConnectionInViewport(connection, viewportBounds);
                // Giữ line + layer năng lượng khi đang chạy trên connection này (tránh culling tắt animation giữa chừng).
                bool shouldBeVisible;
                if (_useStrictViewportCulling)
                {
                    bool endpointVisible =
                        connection.FromNode?.Border?.Visibility == Visibility.Visible ||
                        connection.ToNode?.Border?.Visibility == Visibility.Visible;
                    shouldBeVisible = endpointVisible || (isExecutingNow && connection.IsExecutionActive);
                }
                else
                {
                    shouldBeVisible = inViewport || (isExecutingNow && connection.IsExecutionActive);
                }
                ApplyConnectionVisibility(connection, shouldBeVisible);
                ApplyConnectionArrowVisibility(connection, shouldBeVisible);

                // Note: ArrowHead được quản lý bởi ConnectionRenderer và sẽ tự động ẩn khi LineUI bị ẩn

                if (shouldBeVisible) visibleConnections++;
                else hiddenConnections++;
            }

            // Debug output để kiểm tra viewport culling có hoạt động không
            // Uncomment dòng dưới để xem thống kê trong Debug Output window
            // System.Diagnostics.Debug.WriteLine($"[ViewportCulling] Visible Nodes: {visibleCount}, Hidden: {hiddenCount} | Connections Visible: {visibleConnections}, Hidden: {hiddenConnections} | Viewport: {viewportBounds}");
            PublishVisibilityStats(visibleCount, viewModel.Nodes.Count);
        }

        /// <summary>
        /// Hiển thị tất cả nodes (khi tắt culling hoặc viewport không hợp lệ)
        /// </summary>
        private void ShowAllNodes()
        {
            var host = Host;
            var viewModel = host.ViewModel;
            if (viewModel == null) return;

            foreach (var node in viewModel.Nodes)
            {
                if (node.Border != null && node.Border.Visibility != Visibility.Visible)
                {
                    node.Border.Visibility = Visibility.Visible;
                }
                ApplyAsyncTaskContainerVisibility(node, true);
                ApplyConditionalBranchVisualsVisibility(node, true);

                foreach (var port in node.Ports)
                {
                    if (port.PortUI != null && port.PortUI.Visibility != Visibility.Visible)
                    {
                        port.PortUI.Visibility = Visibility.Visible;
                    }
                }

                // LoopBodyNode
                if (node is LoopNode loopNode && loopNode.LoopBodyNode != null)
                {
                    if (loopNode.LoopBodyNode.Border != null)
                    {
                        loopNode.LoopBodyNode.Border.Visibility = Visibility.Visible;
                    }
                    
                    foreach (var port in loopNode.LoopBodyNode.Ports)
                    {
                        if (port.PortUI != null)
                        {
                            port.PortUI.Visibility = Visibility.Visible;
                        }
                    }
                }
                
                // ✅ Hiển thị title khi show all nodes
                UpdateTitleVisibility(node, true);
            }
            PublishVisibilityStats(viewModel.Nodes.Count, viewModel.Nodes.Count);
        }

        /// <summary>
        /// Hiển thị tất cả connections (khi tắt culling hoặc viewport không hợp lệ)
        /// </summary>
        private void ShowAllConnections()
        {
            var host = Host;
            var viewModel = host.ViewModel;
            if (viewModel == null) return;

            foreach (var connection in viewModel.Connections)
            {
                if (connection.LineUI != null && connection.LineUI.Visibility != Visibility.Visible)
                {
                    connection.LineUI.Visibility = Visibility.Visible;
                }
                ApplyConnectionArrowVisibility(connection, true);

                if (connection.HitArea != null && connection.HitArea.Visibility != Visibility.Visible)
                {
                    connection.HitArea.Visibility = Visibility.Visible;
                }

                if (connection.EnergyUI != null && connection.EnergyUI.Visibility != Visibility.Visible)
                {
                    connection.EnergyUI.Visibility = Visibility.Visible;
                }

                if (connection.EnergyBallUI != null && connection.EnergyBallUI.Visibility != Visibility.Visible)
                {
                    connection.EnergyBallUI.Visibility = Visibility.Visible;
                }

                if (connection.EnergyTextUI != null && connection.EnergyTextUI.Visibility != Visibility.Visible)
                {
                    connection.EnergyTextUI.Visibility = Visibility.Visible;
                }

                // Delete button chỉ hiển thị nếu IsDeleteVisible = true
                if (connection.DeleteButton != null)
                {
                    connection.DeleteButton.Visibility = connection.IsDeleteVisible ? Visibility.Visible : Visibility.Collapsed;
                }

                // Note: ArrowHead được quản lý bởi ConnectionRenderer
            }
        }

        /// <summary>
        /// Chỉ hiển thị các nodes và connections trong execution path từ Start đến End
        /// </summary>
        private void ShowExecutionPathNodes()
        {
            var host = Host;
            var viewModel = host.ViewModel;
            if (viewModel == null) return;

            // Tìm tất cả nodes trong execution path
            var executionPathNodes = GetExecutionPathNodes(viewModel.Nodes, viewModel.Connections);

            // Node đang chạy thực tế + connection bị pin (vd. Loop→LoopBody) có thể không nằm trong tập static Start→End
            foreach (var c in viewModel.Connections)
            {
                if (!c.IsExecutionPinned || c.FromNode == null || c.ToNode == null) continue;
                executionPathNodes.Add(c.FromNode);
                executionPathNodes.Add(c.ToNode);
            }

            foreach (var n in viewModel.RunningNodes)
                executionPathNodes.Add(n);

            var connectionsList = viewModel.Connections.ToList();
            ExpandLoopBodiesForExecutionPath(executionPathNodes, connectionsList);
            EnsureLoopHeaderBodyPairOnPath(executionPathNodes, connectionsList);
            ExpandLoopBodiesForExecutionPath(executionPathNodes, connectionsList);

            var executionPathConnections = GetExecutionPathConnections(viewModel.Connections, executionPathNodes);

            // Ẩn tất cả nodes trước
            foreach (var node in viewModel.Nodes)
            {
                if (node.Border != null)
                {
                    node.Border.Visibility = executionPathNodes.Contains(node) ? Visibility.Visible : Visibility.Collapsed;
                }

                foreach (var port in node.Ports)
                {
                    if (port.PortUI != null)
                    {
                        port.PortUI.Visibility = executionPathNodes.Contains(node) ? Visibility.Visible : Visibility.Collapsed;
                    }
                }

                // LoopBodyNode
                if (node is LoopNode loopNode && loopNode.LoopBodyNode != null)
                {
                    bool bodyVisible = executionPathNodes.Contains(loopNode.LoopBodyNode) || executionPathNodes.Contains(node);
                    if (loopNode.LoopBodyNode.Border != null)
                    {
                        loopNode.LoopBodyNode.Border.Visibility = bodyVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                    
                    foreach (var port in loopNode.LoopBodyNode.Ports)
                    {
                        if (port.PortUI != null)
                        {
                            port.PortUI.Visibility = bodyVisible ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
                
                // ✅ Cập nhật title visibility dựa trên execution path
                UpdateTitleVisibility(node, executionPathNodes.Contains(node));
            }

            // Ẩn tất cả connections trước
            foreach (var connection in viewModel.Connections)
            {
                bool shouldBeVisible = executionPathConnections.Contains(connection);
                
                if (connection.LineUI != null)
                {
                    connection.LineUI.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                }

                if (connection.HitArea != null)
                {
                    connection.HitArea.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                }

                if (connection.EnergyUI != null)
                {
                    connection.EnergyUI.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                }

                if (connection.EnergyBallUI != null)
                {
                    connection.EnergyBallUI.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                }

                if (connection.EnergyTextUI != null)
                {
                    connection.EnergyTextUI.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                }

                if (connection.DeleteButton != null)
                {
                    connection.DeleteButton.Visibility = shouldBeVisible && connection.IsDeleteVisible ? Visibility.Visible : Visibility.Collapsed;
                }

                ApplyConnectionArrowVisibility(connection, shouldBeVisible);
            }
        }

        /// <summary>
        /// Tìm tất cả nodes trong execution path từ Start đến End
        /// </summary>
        private HashSet<WorkflowNode> GetExecutionPathNodes(
            IEnumerable<WorkflowNode> nodes,
            IEnumerable<WorkflowConnection> connections)
        {
            var nodeList = nodes.ToList();
            var connectionList = connections.ToList();
            var executionPathNodes = new HashSet<WorkflowNode>();

            // Tìm Start nodes và End nodes
            var startNodes = nodeList.Where(n => n.Type == NodeType.Start).ToList();
            var endNodes = nodeList.Where(n => n.Type == NodeType.End).ToList();

            if (startNodes.Count == 0 || endNodes.Count == 0)
            {
                // Nếu không có Start hoặc End nodes, hiển thị tất cả
                return new HashSet<WorkflowNode>(nodeList);
            }

            // Từ Start nodes, traverse forward để tìm tất cả nodes có thể đến được
            var reachableFromStart = new HashSet<WorkflowNode>();
            foreach (var startNode in startNodes)
            {
                TraverseForward(startNode, connectionList, reachableFromStart);
            }

            // Từ End nodes, traverse backward để tìm tất cả nodes có thể đến được End
            var canReachEnd = new HashSet<WorkflowNode>();
            foreach (var endNode in endNodes)
            {
                TraverseBackward(endNode, connectionList, canReachEnd);
            }

            // Chỉ giữ lại các nodes có thể đến được từ Start VÀ có thể đến được End
            executionPathNodes.UnionWith(reachableFromStart);
            executionPathNodes.IntersectWith(canReachEnd);

            // Luôn bao gồm Start và End nodes
            foreach (var startNode in startNodes)
            {
                executionPathNodes.Add(startNode);
            }
            foreach (var endNode in endNodes)
            {
                executionPathNodes.Add(endNode);
            }

            ExpandLoopBodiesForExecutionPath(executionPathNodes, connectionList);
            EnsureLoopHeaderBodyPairOnPath(executionPathNodes, connectionList);

            return executionPathNodes;
        }

        /// <summary>
        /// Khi LoopNode nằm trên path, bổ sung LoopBody + toàn bộ cluster nối với body.
        /// </summary>
        private void ExpandLoopBodiesForExecutionPath(HashSet<WorkflowNode> executionPathNodes, List<WorkflowConnection> connectionList)
        {
            var loopNodesToExpand = executionPathNodes.OfType<LoopNode>().ToList();
            foreach (var loopNode in loopNodesToExpand)
            {
                if (loopNode.LoopBodyNode == null) continue;
                executionPathNodes.Add(loopNode.LoopBodyNode);
                foreach (var clusterNode in GetLoopBodyClusterNodes(loopNode, connectionList))
                    executionPathNodes.Add(clusterNode);
            }
        }

        /// <summary>
        /// Đảm bảo cặp Loop diamond ↔ LoopBody cùng hiện khi một trong hai đã có trên path (tránh mất line khi chạy).
        /// </summary>
        private static void EnsureLoopHeaderBodyPairOnPath(HashSet<WorkflowNode> executionPathNodes, List<WorkflowConnection> connections)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var c in connections)
                {
                    if (c.FromNode is not LoopNode ln || c.ToNode == null || !ReferenceEquals(c.ToNode, ln.LoopBodyNode)) continue;
                    var body = ln.LoopBodyNode;
                    if (body == null) continue;
                    var hasL = executionPathNodes.Contains(ln);
                    var hasB = executionPathNodes.Contains(body);
                    if (hasL && !hasB)
                    {
                        executionPathNodes.Add(body);
                        changed = true;
                    }
                    else if (hasB && !hasL)
                    {
                        executionPathNodes.Add(ln);
                        changed = true;
                    }
                }
            }
        }

        /// <summary>
        /// Traverse forward từ một node theo connections
        /// </summary>
        private void TraverseForward(
            WorkflowNode currentNode,
            List<WorkflowConnection> connections,
            HashSet<WorkflowNode> visited)
        {
            if (visited.Contains(currentNode))
            {
                return; // Đã thăm rồi, tránh vòng lặp
            }

            visited.Add(currentNode);

            // Tìm tất cả connections từ node này
            var outgoingConnections = connections.Where(c => c.FromNode == currentNode).ToList();
            foreach (var conn in outgoingConnections)
            {
                if (conn.ToNode != null && !visited.Contains(conn.ToNode))
                {
                    TraverseForward(conn.ToNode, connections, visited);
                }
            }
        }

        /// <summary>
        /// Traverse backward từ một node theo connections (ngược lại)
        /// </summary>
        private void TraverseBackward(
            WorkflowNode currentNode,
            List<WorkflowConnection> connections,
            HashSet<WorkflowNode> visited)
        {
            if (visited.Contains(currentNode))
            {
                return; // Đã thăm rồi, tránh vòng lặp
            }

            visited.Add(currentNode);

            // Tìm tất cả connections đến node này
            var incomingConnections = connections.Where(c => c.ToNode == currentNode).ToList();
            foreach (var conn in incomingConnections)
            {
                if (conn.FromNode != null && !visited.Contains(conn.FromNode))
                {
                    TraverseBackward(conn.FromNode, connections, visited);
                }
            }
        }

        /// <summary>
        /// Tìm tất cả connections trong execution path
        /// </summary>
        private HashSet<WorkflowConnection> GetExecutionPathConnections(
            IEnumerable<WorkflowConnection> connections,
            HashSet<WorkflowNode> executionPathNodes)
        {
            // ✅ FIX: Bao gồm tất cả connections giữa các nodes trong execution path
            // Bao gồm cả connections trong Loop Body clusters (có thể có cycles)
            return new HashSet<WorkflowConnection>(
                connections.Where(c =>
                    c.FromNode != null &&
                    c.ToNode != null &&
                    executionPathNodes.Contains(c.FromNode) &&
                    executionPathNodes.Contains(c.ToNode)));
        }

        /// <summary>
        /// Lấy toàn bộ nodes nằm trong LoopBody cluster: tất cả nodes được kết nối
        /// (trực tiếp hoặc gián tiếp) với LoopBodyNode, bỏ qua LoopNode cha.
        /// </summary>
        private List<WorkflowNode> GetLoopBodyClusterNodes(LoopNode loopNode, List<WorkflowConnection> connections)
        {
            var result = new List<WorkflowNode>();
            var body = loopNode.LoopBodyNode;
            if (body == null) return result;

            var visited = new HashSet<WorkflowNode> { body };
            var queue = new Queue<WorkflowNode>();
            queue.Enqueue(body);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                var neighbors = connections
                    .Where(c => c.FromNode == current || c.ToNode == current)
                    .Select(c => c.FromNode == current ? c.ToNode : c.FromNode)
                    .Where(n => n != null);

                foreach (var neighbor in neighbors)
                {
                    // Bỏ qua LoopNode cha để không lan ra ngoài qua default connection
                    if (ReferenceEquals(neighbor, loopNode)) continue;

                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Loại bỏ chính LoopBodyNode, chỉ trả về các node "bên trong" body
            visited.Remove(body);
            result.AddRange(visited);
            return result;
        }

        /// <summary>
        /// Gọi khi viewport thay đổi (scroll/zoom/pan) - debounced
        /// </summary>
        public void OnViewportChanged()
        {
            if (!_isEnabled) return;
            
            // Restart timer để debounce updates
            _updateTimer.Stop();
            _updateTimer.Start();
        }

        /// <summary>
        /// Force update ngay lập tức (không debounce)
        /// </summary>
        public void ForceUpdate()
        {
            _updateTimer.Stop();
            UpdateViewportCulling();
        }

        /// <summary>
        /// Tạm dừng viewport culling timer để tối ưu CPU khi window bị ẩn
        /// </summary>
        public void Pause()
        {
            _updateTimer.Stop();
        }

        /// <summary>
        /// Tiếp tục viewport culling timer khi window được hiển thị lại
        /// </summary>
        public void Resume()
        {
            if (_isEnabled)
            {
                // ✅ Clear cache để force tính toán lại viewport bounds
                _cachedViewportBounds = Rect.Empty;
                _cachedViewportBoundsForNodeCheck = Rect.Empty;
                _cachedZoomLevel = 1.0;
                _lastViewportBoundsUpdate = DateTime.MinValue;
                
                _updateTimer.Stop();
                // ✅ Force update ngay lập tức
                UpdateViewportCulling();
            }
        }

        /// <summary>
        /// Cập nhật khi node được thêm/xóa/di chuyển
        /// </summary>
        public void OnNodeChanged(WorkflowNode node)
        {
            if (!_isEnabled) return;
            
            // ⚠️ QUAN TRỌNG: Nếu node chưa có Border, đảm bảo nó sẽ được hiển thị khi render
            // Không cull node chưa được render để tránh mất node
            if (node.Border == null)
            {
                // Schedule một update sau khi node được render
                Host.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (node.Border != null)
                    {
                        OnNodeChanged(node); // Gọi lại sau khi node được render
                    }
                }), DispatcherPriority.Loaded);
                return;
            }
            
            var viewportBounds = GetCachedViewportBounds();
            
            // ⚠️ QUAN TRỌNG: Nếu viewport không hợp lệ, hiển thị node để tránh mất node
            if (viewportBounds.IsEmpty)
            {
                // Đảm bảo node được hiển thị nếu viewport không hợp lệ
                if (node.Border != null && node.Border.Visibility != Visibility.Visible)
                {
                    node.Border.Visibility = Visibility.Visible;
                }
                foreach (var port in node.Ports)
                {
                    if (port.PortUI != null && port.PortUI.Visibility != Visibility.Visible)
                    {
                        port.PortUI.Visibility = Visibility.Visible;
                    }
                }
                // ✅ Hiển thị title khi viewport không hợp lệ
                UpdateTitleVisibility(node, true);
                return;
            }

            bool shouldBeVisible = IsNodeInViewport(node, viewportBounds);
            
            // ⚠️ SAFETY: Khi zoom in (zoom > 1), hiển thị nhiều hơn để tránh mất node
            if (!shouldBeVisible && Host.ZoomLevel > 0.5)
            {
                Rect veryExpandedViewport = new Rect(
                    viewportBounds.Left - 500,
                    viewportBounds.Top - 500,
                    viewportBounds.Width + 1000,
                    viewportBounds.Height + 1000
                );
                Rect nodeBounds = GetNodeBounds(node);
                if (veryExpandedViewport.IntersectsWith(nodeBounds))
                {
                    shouldBeVisible = true;
                }
            }
            
            if (node.Border != null)
            {
                node.Border.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            foreach (var port in node.Ports)
            {
                if (port.PortUI != null)
                {
                    port.PortUI.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            // ✅ Cập nhật visibility của title text block dựa trên viewport
            UpdateTitleVisibility(node, shouldBeVisible);

            // Cập nhật lại các connections liên quan đến node này
            var viewModel = Host.ViewModel;
            if (viewModel != null)
            {
                foreach (var connection in viewModel.Connections)
                {
                    if (connection.FromNode == node || connection.ToNode == node)
                    {
                        OnConnectionChanged(connection);
                    }
                }
            }
        }

        /// <summary>
        /// Cập nhật khi connection được thêm/xóa/thay đổi
        /// </summary>
        public void OnConnectionChanged(WorkflowConnection connection)
        {
            if (!_isEnabled) return;
            
            var viewportBounds = GetCachedViewportBounds();
            if (viewportBounds.IsEmpty) return;

            var vm = Host.ViewModel;
            bool shouldBeVisible = IsConnectionInViewport(connection, viewportBounds)
                || (vm?.IsExecuting == true && connection.IsExecutionActive);

            if (connection.LineUI != null)
            {
                connection.LineUI.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            if (connection.HitArea != null)
            {
                connection.HitArea.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            if (connection.EnergyUI != null)
            {
                connection.EnergyUI.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            if (connection.EnergyBallUI != null)
            {
                connection.EnergyBallUI.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            if (connection.EnergyTextUI != null)
            {
                connection.EnergyTextUI.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            if (connection.DeleteButton != null)
            {
                bool deleteButtonVisible = shouldBeVisible && connection.IsDeleteVisible;
                connection.DeleteButton.Visibility = deleteButtonVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            // Note: ArrowHead được quản lý bởi ConnectionRenderer
        }

        /// <summary>
        /// Cập nhật visibility của title text block dựa trên viewport và zoom state
        /// </summary>
        private void UpdateTitleVisibility(WorkflowNode node, bool nodeShouldBeVisible)
        {
            // ✅ Disable title hoàn toàn khi đang zoom để tránh giật
            if (NodeChrome.IsZooming)
            {
                if (node.TitleTextBlockUI != null && node.TitleTextBlockUI.Visibility != Visibility.Collapsed)
                {
                    node.TitleTextBlockUI.Visibility = Visibility.Collapsed;
                }
                return;
            }

            // Chỉ hiển thị title khi node visible và trong viewport
            var titleTextBlock = node.TitleTextBlockUI;
            if (titleTextBlock != null)
            {
                // Lấy TitleDisplayMode từ node (nếu có)
                Visibility targetVisibility = Visibility.Collapsed;
                
                if (nodeShouldBeVisible)
                {
                    if (_forceShowTitleForVisibleNodes)
                    {
                        targetVisibility = Visibility.Visible;
                    }
                    // Start/End nodes: title luôn hiển thị
                    else
                    // Start/End nodes: title luôn hiển thị
                    if (node.Type == NodeType.Start || node.Type == NodeType.End)
                    {
                        targetVisibility = Visibility.Visible;
                        
                        // Cập nhật vị trí title sau khi zoom
                        if (!NodeChrome.IsZooming)
                        {
                            if (node.Type == NodeType.Start)
                            {
                                StartNodeControl.UpdateTitlePosition(node, Host.WorkflowCanvas);
                            }
                            else
                            {
                                EndNodeControl.UpdateTitlePosition(node, Host.WorkflowCanvas);
                            }
                        }
                    }
                    // Kiểm tra TitleDisplayMode
                    else if (node is InputNode inputNode)
                    {
                        targetVisibility = inputNode.TitleDisplayMode switch
                        {
                            TitleDisplayMode.Always => Visibility.Visible,
                            TitleDisplayMode.Hidden => Visibility.Collapsed,
                            TitleDisplayMode.Hover => Visibility.Collapsed, // Hover sẽ được xử lý bởi NodeControl
                            _ => Visibility.Collapsed
                        };
                    }
                    else if (node is KeyPressEventNode keyNode)
                    {
                        targetVisibility = keyNode.TitleDisplayMode switch
                        {
                            TitleDisplayMode.Always => Visibility.Visible,
                            TitleDisplayMode.Hidden => Visibility.Collapsed,
                            TitleDisplayMode.Hover => Visibility.Collapsed,
                            _ => Visibility.Collapsed
                        };
                    }
                    else if (node is HotkeyPressEventNode hotkeyNode)
                    {
                        targetVisibility = hotkeyNode.TitleDisplayMode switch
                        {
                            TitleDisplayMode.Always => Visibility.Visible,
                            TitleDisplayMode.Hidden => Visibility.Collapsed,
                            TitleDisplayMode.Hover => Visibility.Collapsed,
                            _ => Visibility.Collapsed
                        };
                    }
                    else
                    {
                        // Default: hiển thị nếu có TitleTextBlockUI
                        targetVisibility = Visibility.Visible;
                    }
                }
                
                // Chỉ update nếu visibility thay đổi để tránh unnecessary updates
                if (titleTextBlock.Visibility != targetVisibility)
                {
                    titleTextBlock.Visibility = targetVisibility;
                }
            }
        }

        public void ForceShowEverything()
        {
            ShowAllNodes();
            ShowAllConnections();
        }

        private void UpdateRunningNodesOnly(FlowMy.ViewModels.WorkflowEditorViewModel viewModel, Rect viewportBounds)
        {
            var runningNodes = new HashSet<WorkflowNode>(viewModel.RunningNodes ?? Enumerable.Empty<WorkflowNode>());

            // Tích lũy node đã chạy để hiển thị dần theo luồng thực thi.
            foreach (var node in runningNodes)
            {
                if (!string.IsNullOrWhiteSpace(node.Id))
                    _executedNodeIds.Add(node.Id);
            }

            if (viewModel.ActiveExecutionConnection != null)
            {
                if (viewModel.ActiveExecutionConnection.FromNode != null)
                {
                    runningNodes.Add(viewModel.ActiveExecutionConnection.FromNode);
                    if (!string.IsNullOrWhiteSpace(viewModel.ActiveExecutionConnection.FromNode.Id))
                        _executedNodeIds.Add(viewModel.ActiveExecutionConnection.FromNode.Id);
                }
                if (viewModel.ActiveExecutionConnection.ToNode != null)
                {
                    runningNodes.Add(viewModel.ActiveExecutionConnection.ToNode);
                    if (!string.IsNullOrWhiteSpace(viewModel.ActiveExecutionConnection.ToNode.Id))
                        _executedNodeIds.Add(viewModel.ActiveExecutionConnection.ToNode.Id);
                }
            }

            var revealedNodes = new HashSet<WorkflowNode>(
                viewModel.Nodes.Where(n => !string.IsNullOrWhiteSpace(n.Id) && _executedNodeIds.Contains(n.Id)));

            foreach (var node in viewModel.Nodes)
            {
                if (node.Border == null) continue;

                bool shouldBeVisible = (runningNodes.Contains(node) || revealedNodes.Contains(node)) &&
                                       IsNodeInViewport(node, viewportBounds);
                ApplyNodeVisibility(node, shouldBeVisible);
                ApplyAsyncTaskContainerVisibility(node, shouldBeVisible);
                ApplyConditionalBranchVisualsVisibility(node, shouldBeVisible);

                foreach (var port in node.Ports)
                {
                    if (port.PortUI != null)
                    {
                        var targetVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                        if (port.PortUI.Visibility != targetVisibility)
                        {
                            port.PortUI.Visibility = targetVisibility;
                        }
                    }
                }

                UpdateTitleVisibility(node, shouldBeVisible);

                if (node is LoopNode loopNode && loopNode.LoopBodyNode != null)
                {
                    bool bodyVisible = (runningNodes.Contains(loopNode.LoopBodyNode) || revealedNodes.Contains(loopNode.LoopBodyNode)) &&
                                       IsNodeInViewport(loopNode.LoopBodyNode, viewportBounds);
                    if (loopNode.LoopBodyNode.Border != null)
                    {
                        loopNode.LoopBodyNode.Border.Visibility = bodyVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                    foreach (var port in loopNode.LoopBodyNode.Ports)
                    {
                        if (port.PortUI != null)
                        {
                            port.PortUI.Visibility = bodyVisible ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }

            foreach (var connection in viewModel.Connections)
            {
                bool touchesRevealedNode = (connection.FromNode != null && (runningNodes.Contains(connection.FromNode) || revealedNodes.Contains(connection.FromNode))) ||
                                           (connection.ToNode != null && (runningNodes.Contains(connection.ToNode) || revealedNodes.Contains(connection.ToNode)));
                bool shouldBeVisible = touchesRevealedNode && IsConnectionInViewport(connection, viewportBounds);
                if (connection.IsExecutionActive)
                {
                    shouldBeVisible = true;
                }
                ApplyConnectionVisibility(connection, shouldBeVisible);
                ApplyConnectionArrowVisibility(connection, shouldBeVisible);
            }

            int visibleNodes = viewModel.Nodes.Count(n => n.Border?.Visibility == Visibility.Visible);
            PublishVisibilityStats(visibleNodes, viewModel.Nodes.Count);
        }

        private void PublishVisibilityStats(int visibleNodes, int totalNodes)
        {
            try
            {
                VisibilityStatsChanged?.Invoke(visibleNodes, totalNodes);
            }
            catch
            {
                // best effort only
            }
        }

        private void ApplyNodeVisibility(WorkflowNode node, bool shouldBeVisible)
        {
            var targetVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;

            if (_nodeVisibilityCache.TryGetValue(node.Id, out var lastVisible) && lastVisible == shouldBeVisible)
            {
                if (node.Border != null && node.Border.Visibility != targetVisibility)
                {
                    node.Border.Visibility = targetVisibility;
                }
                return;
            }

            _nodeVisibilityCache[node.Id] = shouldBeVisible;
            if (node.Border != null && node.Border.Visibility != targetVisibility)
            {
                node.Border.Visibility = targetVisibility;
            }
        }

        private void ApplyConnectionVisibility(WorkflowConnection connection, bool shouldBeVisible)
        {
            var targetVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            var key = GetConnectionCacheKey(connection);

            if (_connectionVisibilityCache.TryGetValue(key, out var lastVisible) && lastVisible == shouldBeVisible)
            {
                return;
            }

            _connectionVisibilityCache[key] = shouldBeVisible;

            if (connection.LineUI != null && connection.LineUI.Visibility != targetVisibility)
            {
                connection.LineUI.Visibility = targetVisibility;
            }

            if (connection.HitArea != null && connection.HitArea.Visibility != targetVisibility)
            {
                connection.HitArea.Visibility = targetVisibility;
            }

            if (connection.EnergyUI != null && connection.EnergyUI.Visibility != targetVisibility)
            {
                connection.EnergyUI.Visibility = targetVisibility;
            }

            if (connection.EnergyBallUI != null && connection.EnergyBallUI.Visibility != targetVisibility)
            {
                connection.EnergyBallUI.Visibility = targetVisibility;
            }

            if (connection.EnergyTextUI != null && connection.EnergyTextUI.Visibility != targetVisibility)
            {
                connection.EnergyTextUI.Visibility = targetVisibility;
            }

            if (connection.DeleteButton != null)
            {
                bool deleteButtonVisible = shouldBeVisible && connection.IsDeleteVisible;
                var deleteVisibility = deleteButtonVisible ? Visibility.Visible : Visibility.Collapsed;
                if (connection.DeleteButton.Visibility != deleteVisibility)
                {
                    connection.DeleteButton.Visibility = deleteVisibility;
                }
            }
        }

        private static void ApplyConnectionArrowVisibility(WorkflowConnection connection, bool shouldBeVisible)
        {
            if (connection.LineUI?.Tag is not ConnectionRenderer.ConnectionTag tag || tag.ArrowHead == null)
                return;

            var targetVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            if (tag.ArrowHead.Visibility != targetVisibility)
            {
                tag.ArrowHead.Visibility = targetVisibility;
            }
        }

        private static void ApplyAsyncTaskContainerVisibility(WorkflowNode node, bool shouldBeVisible)
        {
            if (node is not AsyncTaskNode asyncTaskNode || asyncTaskNode.ContainerBorder == null)
                return;

            var targetVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            if (asyncTaskNode.ContainerBorder.Visibility != targetVisibility)
            {
                asyncTaskNode.ContainerBorder.Visibility = targetVisibility;
            }
        }

        private static void ApplyConditionalBranchVisualsVisibility(WorkflowNode node, bool shouldBeVisible)
        {
            if (!node.IsConditionalNode || node.ConditionalVisualMode != ConditionalVisualMode.Diamond)
                return;

            var targetVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            foreach (var branch in node.ConditionalBranches)
            {
                if (branch.SatelliteBorder != null && branch.SatelliteBorder.Visibility != targetVisibility)
                    branch.SatelliteBorder.Visibility = targetVisibility;
                if (branch.SatelliteLine != null && branch.SatelliteLine.Visibility != targetVisibility)
                    branch.SatelliteLine.Visibility = targetVisibility;
                if (branch.SatelliteInputVisual != null && branch.SatelliteInputVisual.Visibility != targetVisibility)
                    branch.SatelliteInputVisual.Visibility = targetVisibility;
                if (branch.DiamondOutputVisual != null && branch.DiamondOutputVisual.Visibility != targetVisibility)
                    branch.DiamondOutputVisual.Visibility = targetVisibility;
                if (branch.SatelliteArrowHead != null && branch.SatelliteArrowHead.Visibility != targetVisibility)
                    branch.SatelliteArrowHead.Visibility = targetVisibility;

                if (branch.SatelliteDeleteButton != null)
                {
                    var deleteVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                    if (branch.SatelliteDeleteButton.Visibility != deleteVisibility)
                        branch.SatelliteDeleteButton.Visibility = deleteVisibility;
                }
            }
        }

        private void CleanupVisibilityCaches(FlowMy.ViewModels.WorkflowEditorViewModel viewModel)
        {
            if (_nodeVisibilityCache.Count > viewModel.Nodes.Count + 16)
            {
                var currentNodeIds = new HashSet<string>(viewModel.Nodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
                foreach (var key in _nodeVisibilityCache.Keys.Where(k => !currentNodeIds.Contains(k)).ToList())
                {
                    _nodeVisibilityCache.Remove(key);
                }
            }

            if (_connectionVisibilityCache.Count > viewModel.Connections.Count + 16)
            {
                var currentConnectionIds = new HashSet<string>(
                    viewModel.Connections.Select(GetConnectionCacheKey),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var key in _connectionVisibilityCache.Keys.Where(k => !currentConnectionIds.Contains(k)).ToList())
                {
                    _connectionVisibilityCache.Remove(key);
                }
            }
        }

        private static string GetConnectionCacheKey(WorkflowConnection connection)
        {
            var fromPortId = connection.FromPort?.Id ?? (connection.IsFromInput ? "in" : "out");
            var toPortId = connection.ToPort?.Id ?? (connection.IsFromInput ? "out" : "in");
            return $"{connection.FromNode?.Id}:{fromPortId}->{connection.ToNode?.Id}:{toPortId}";
        }
    }
}
