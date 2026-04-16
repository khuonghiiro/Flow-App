using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FlowMy.Models;
using FlowMy.Services.Utilities;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls;

namespace FlowMy.Services.Interaction
{
    public sealed class ZoomPanHandler
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly CanvasSizeManager _canvasSizeManager;
        private DispatcherTimer? _canvasSizeUpdateTimer;
        
        /// <summary>
        /// Thời gian debounce (ms) trước khi cập nhật kích thước canvas sau khi zoom/pan.
        /// Giá trị cao hơn: Giảm số lần cập nhật → Giảm CPU usage, phù hợp khi có nhiều nodes (>100).
        /// Giá trị thấp hơn: Canvas size cập nhật nhanh hơn → Responsive hơn khi pan, nhưng tăng CPU usage.
        /// Khuyến nghị: 150-300ms cho cân bằng tốt giữa performance và responsiveness.
        /// </summary>
        private const int CanvasSizeUpdateDebounceMs = 150;

        // Zoom throttling - sử dụng CompositionTarget.Rendering để frame-perfect updates
        private EventHandler? _renderingHandler;
        private bool _isRenderingHandlerActive;
        private DateTime _lastRenderingUpdate = DateTime.MinValue;
        private const int MinRenderingIntervalMs = 8; // ~120fps max để tránh quá tải
        
        private double? _pendingZoomLevel;
        private Point _pendingMousePosOnViewport;

        // During heavy zoom, temporarily disable expensive effects (e.g., DropShadow)
        private DispatcherTimer? _zoomEndTimer;
        
        /// <summary>
        /// Thời gian debounce (ms) để phát hiện khi zoom kết thúc hoàn toàn.
        /// Sau khi người dùng dừng zoom, đợi khoảng thời gian này trước khi:
        /// - Khôi phục DropShadow effects (nếu đã tắt)
        /// - Bật lại NodeChrome handlers
        /// - Cập nhật viewport culling và title visibility/position
        /// Giá trị cao hơn: Giảm số lần khôi phục effects → Giảm CPU usage, phù hợp khi zoom liên tục.
        /// Giá trị thấp hơn: Title/effects hiện lại nhanh hơn → Responsive hơn, nhưng tăng CPU usage.
        /// Khuyến nghị: 300-500ms cho cân bằng tốt giữa performance và responsiveness.
        /// </summary>
        private const int ZoomEndDebounceMs = 300;
        private bool _effectsTemporarilyDisabled;
        private readonly System.Collections.Generic.Dictionary<System.Windows.Controls.Border, Effect?> _savedBorderEffects = new();

        public ZoomPanHandler(IWorkflowEditorHostAccessor hostAccessor, CanvasSizeManager canvasSizeManager)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _canvasSizeManager = canvasSizeManager ?? throw new ArgumentNullException(nameof(canvasSizeManager));
        }

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public void InitializeZoomPan()
        {
            var host = Host;
            host.ScaleTransform.ScaleX = host.ZoomLevel;
            host.ScaleTransform.ScaleY = host.ZoomLevel;
        }

        /// <summary>
        /// Restore view state (zoom and pan) from ViewModel values.
        /// This should be called when loading a workflow to restore the saved view.
        /// </summary>
        public void RestoreViewState(double zoomLevel, double panX, double panY)
        {
            var host = Host;
            var viewModel = host.ViewModel;

            // Reset scroll để tránh trạng thái cũ từ workflow trước (giống FitToView)
            host.ScrollViewer.ScrollToHorizontalOffset(0);
            host.ScrollViewer.ScrollToVerticalOffset(0);

            // Update zoom level
            host.ZoomLevel = Math.Clamp(zoomLevel, 0.1, 3.0);
            host.ScaleTransform.ScaleX = host.ZoomLevel;
            host.ScaleTransform.ScaleY = host.ZoomLevel;

            // Update grid scale
            if (host.GridScaleTransform != null)
            {
                host.GridScaleTransform.ScaleX = host.ZoomLevel;
                host.GridScaleTransform.ScaleY = host.ZoomLevel;
            }

            // Update pan position
            host.TranslateTransform.X = panX;
            host.TranslateTransform.Y = panY;

            // Update grid pan
            if (host.GridTranslateTransform != null)
            {
                host.GridTranslateTransform.X = panX;
                host.GridTranslateTransform.Y = panY;
            }

            // Update canvas size
            UpdateCanvasSize();

        }

        public void ScrollToCenter()
        {
            var host = Host;
            var viewModel = host.ViewModel;
            if (viewModel == null || viewModel.Nodes.Count == 0) return;

            // Khớp với tâm lưới 20000×20000 và vị trí Start/End trong InitializeSampleNodes
            double centerX = 10000;
            double centerY = 10000;

            double scrollX = (centerX * host.ZoomLevel) - (host.ScrollViewer.ViewportWidth / 2);
            double scrollY = (centerY * host.ZoomLevel) - (host.ScrollViewer.ViewportHeight / 2);

            host.Dispatcher.BeginInvoke(new Action(() =>
            {
                host.ScrollViewer.ScrollToHorizontalOffset(Math.Max(0, scrollX));
                host.ScrollViewer.ScrollToVerticalOffset(Math.Max(0, scrollY));
                host.ScrollViewer.UpdateLayout();

                host.Dispatcher.BeginInvoke(new Action(() =>
                {
                    host.ScrollViewer.ScrollToHorizontalOffset(Math.Max(0, scrollX));
                    host.ScrollViewer.ScrollToVerticalOffset(Math.Max(0, scrollY));
                }), DispatcherPriority.ApplicationIdle);
            }), DispatcherPriority.Loaded);
        }

        public Point GetViewportCenter()
        {
            var host = Host;
            double viewportCenterX = (host.ScrollViewer.HorizontalOffset + host.ScrollViewer.ViewportWidth / 2) / host.ZoomLevel;
            double viewportCenterY = (host.ScrollViewer.VerticalOffset + host.ScrollViewer.ViewportHeight / 2) / host.ZoomLevel;

            viewportCenterX -= host.TranslateTransform.X / host.ZoomLevel;
            viewportCenterY -= host.TranslateTransform.Y / host.ZoomLevel;

            return new Point(viewportCenterX, viewportCenterY);
        }

        public Point GetRandomViewportPosition()
        {
            Random random = new Random();

            var host = Host;
            double viewportWidth = host.ScrollViewer.ViewportWidth / host.ZoomLevel;
            double viewportHeight = host.ScrollViewer.ViewportHeight / host.ZoomLevel;
            double viewportLeft = host.ScrollViewer.HorizontalOffset / host.ZoomLevel;
            double viewportTop = host.ScrollViewer.VerticalOffset / host.ZoomLevel;

            viewportLeft -= host.TranslateTransform.X / host.ZoomLevel;
            viewportTop -= host.TranslateTransform.Y / host.ZoomLevel;

            double padding = 100;
            double nodeWidth = 150;
            double nodeHeight = 80;

            double minX = viewportLeft + padding;
            double maxX = viewportLeft + viewportWidth - padding - nodeWidth;
            double minY = viewportTop + padding;
            double maxY = viewportTop + viewportHeight - padding - nodeHeight;

            if (maxX < minX) maxX = minX;
            if (maxY < minY) maxY = minY;

            double randomX = random.NextDouble() * (maxX - minX) + minX;
            double randomY = random.NextDouble() * (maxY - minY) + minY;

            return new Point(randomX, randomY);
        }

        public void PreviewMouseWheel(MouseWheelEventArgs e)
        {
            var host = Host;
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                return;
            }

            // Kiểm tra xem mouse có đang ở trên ScrollViewer nội bộ không (như execution results)
            // Nếu có, không handle event để ScrollViewer nội bộ có thể scroll
            if (IsMouseOverInternalScrollViewer(e))
            {
                return;
            }

            e.Handled = true;

            _pendingMousePosOnViewport = e.GetPosition(host.ScrollViewer);

            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            double targetZoom = host.ZoomLevel * zoomFactor;
            targetZoom = Math.Max(host.MinZoom, Math.Min(host.MaxZoom, targetZoom));
            if (Math.Abs(targetZoom - host.ZoomLevel) < 0.000001) return;

            _pendingZoomLevel = targetZoom;

            // With many nodes, shadows are expensive to re-render for every zoom step
            TryDisableExpensiveEffectsDuringZoom();

            // Throttle NodeChrome handlers during zoom
            NodeChrome.SetZoomingState(true);

            // ✅ Tối ưu: Tắt viewport culling khi đang zoom để tránh lag
            var viewportCullingService = Host.ViewportCullingService;
            if (viewportCullingService != null && viewportCullingService.IsEnabled)
            {
                viewportCullingService.Pause();
            }

            // Apply zoom sử dụng CompositionTarget.Rendering để frame-perfect updates
            EnsureRenderingHandler();

            // Debounce "zoom end" to restore effects + finalize canvas size once
            EnsureZoomEndTimer();
            _zoomEndTimer!.Stop();
            _zoomEndTimer.Start();
        }

        /// <summary>
        /// Kiểm tra xem mouse có đang ở trên một ScrollViewer nội bộ (không phải ScrollViewer chính của canvas)
        /// </summary>
        private bool IsMouseOverInternalScrollViewer(MouseWheelEventArgs e)
        {
            var host = Host;
            
            // Lấy element dưới con trỏ chuột
            var hitElement = e.OriginalSource as DependencyObject;
            if (hitElement == null) return false;

            // Duyệt lên tree để tìm ScrollViewer
            var element = hitElement;
            while (element != null)
            {
                // Nếu gặp ScrollViewer chính của canvas, dừng lại
                if (element == host.ScrollViewer)
                {
                    return false;
                }

                // Nếu gặp ScrollViewer nội bộ (không phải ScrollViewer chính)
                if (element is System.Windows.Controls.ScrollViewer scrollViewer)
                {
                    // Kiểm tra xem ScrollViewer visible
                    if (scrollViewer.Visibility == Visibility.Visible)
                    {
                        return true;
                    }
                }

                // Lấy parent - xử lý cả Visual và non-Visual elements (như Run, Inline, etc.)
                element = GetParentSafe(element);
            }

            return false;
        }

        /// <summary>
        /// Lấy parent của element, hỗ trợ cả Visual và non-Visual elements
        /// </summary>
        private static DependencyObject? GetParentSafe(DependencyObject element)
        {
            if (element == null) return null;

            // Nếu là Visual hoặc Visual3D, dùng VisualTreeHelper
            if (element is Visual || element is System.Windows.Media.Media3D.Visual3D)
            {
                return VisualTreeHelper.GetParent(element);
            }

            // Nếu là ContentElement (như Run, Inline, etc.), dùng LogicalTreeHelper
            if (element is System.Windows.ContentElement contentElement)
            {
                var parent = System.Windows.LogicalTreeHelper.GetParent(contentElement);
                if (parent != null) return parent;
            }

            // Nếu là FrameworkContentElement, thử lấy Parent
            if (element is System.Windows.FrameworkContentElement fce)
            {
                return fce.Parent;
            }

            return null;
        }

        /// <summary>
        /// Sử dụng CompositionTarget.Rendering để có frame-perfect zoom updates
        /// Tốt hơn DispatcherTimer vì đồng bộ với refresh rate của màn hình
        /// </summary>
        private void EnsureRenderingHandler()
        {
            // ✅ Tạo handler nếu chưa có
            if (_renderingHandler == null)
            {
            _renderingHandler = (s, e) =>
            {
                // Nếu không còn zoom pending, không làm gì (nhưng vẫn giữ handler active)
                // Handler sẽ được dừng bởi zoom end timer
                if (_pendingZoomLevel == null)
                {
                    return;
                }
                
                // ✅ Throttle để tránh quá tải - chỉ update mỗi 8ms (~120fps max)
                var now = DateTime.Now;
                if ((now - _lastRenderingUpdate).TotalMilliseconds < MinRenderingIntervalMs)
                {
                    return; // Skip frame này
                }
                _lastRenderingUpdate = now;
                
                ApplyPendingZoom();
            };
            }
            
            // ✅ Đảm bảo handler được đăng ký mỗi khi có wheel event mới
            if (!_isRenderingHandlerActive)
            {
                CompositionTarget.Rendering += _renderingHandler;
                _isRenderingHandlerActive = true;
                _lastRenderingUpdate = DateTime.Now;
            }
        }
        
        private void StopRenderingHandler()
        {
            if (_renderingHandler != null && _isRenderingHandlerActive)
            {
                CompositionTarget.Rendering -= _renderingHandler;
                _isRenderingHandlerActive = false;
            }
        }

        private void ApplyPendingZoom()
        {
            var host = Host;
            if (_pendingZoomLevel == null)
            {
                // Không có zoom pending, không làm gì (handler sẽ tự dừng sau)
                return;
            }

            double newZoom = _pendingZoomLevel.Value;
            // ✅ QUAN TRỌNG: Clear _pendingZoomLevel TRƯỚC KHI apply để tránh apply lại
            // Nhưng đảm bảo wheel event mới có thể set lại giá trị ngay lập tức
            _pendingZoomLevel = null;

            // Apply around cursor position (same logic as before)
            Point mousePosOnViewport = _pendingMousePosOnViewport;

            double currentScrollX = host.ScrollViewer.HorizontalOffset;
            double currentScrollY = host.ScrollViewer.VerticalOffset;
            double contentX = currentScrollX + mousePosOnViewport.X;
            double contentY = currentScrollY + mousePosOnViewport.Y;

            double oldZoom = host.ZoomLevel;
            double canvasX = (contentX - host.TranslateTransform.X) / oldZoom;
            double canvasY = (contentY - host.TranslateTransform.Y) / oldZoom;

            // Tính toán translate mới
            double newTranslateX = contentX - (canvasX * newZoom);
            double newTranslateY = contentY - (canvasY * newZoom);

            // ✅ Tối ưu: Batch tất cả transform updates - set trực tiếp để tránh overhead
            // Update ZoomLevel property trước
            host.ZoomLevel = newZoom;
            
            // Update scale transforms - set trực tiếp để tránh animation overhead
            host.ScaleTransform.ScaleX = newZoom;
            host.ScaleTransform.ScaleY = newZoom;

            if (host.GridScaleTransform != null)
            {
                host.GridScaleTransform.ScaleX = newZoom;
                host.GridScaleTransform.ScaleY = newZoom;
            }

            // Update translate transforms
            host.TranslateTransform.X = newTranslateX;
            host.TranslateTransform.Y = newTranslateY;

            if (host.GridTranslateTransform != null)
            {
                host.GridTranslateTransform.X = newTranslateX;
                host.GridTranslateTransform.Y = newTranslateY;
            }
        }

        private void EnsureZoomEndTimer()
        {
            if (_zoomEndTimer != null) return;
            _zoomEndTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ZoomEndDebounceMs) };
            _zoomEndTimer.Tick += (s, e) =>
            {
                _zoomEndTimer!.Stop();
                StopRenderingHandler(); // Dừng rendering handler khi zoom kết thúc

                // finalize expensive work after zoom settles
                RestoreExpensiveEffectsAfterZoom();
                NodeChrome.SetZoomingState(false); // Re-enable NodeChrome handlers
                
                // ✅ Tối ưu: Bật lại viewport culling và trigger update
                var viewportCullingService = Host.ViewportCullingService;
                if (viewportCullingService != null)
                {
                    viewportCullingService.Resume(); // Bật lại viewport culling
                    // Chỉ trigger update, không force ngay để tránh lag
                    viewportCullingService.OnViewportChanged();
                }
                
                // ✅ Tối ưu: Update title position chỉ cho nodes trong viewport, không phải tất cả
                // Sử dụng DispatcherPriority thấp hơn để không block UI thread
                UpdateVisibleTitlePositionsAfterZoom();
                
                // Update ViewModel with current zoom and pan state (effectivePan = Translate - Scroll)
                var viewModel = Host.ViewModel;
                if (viewModel != null)
                {
                    if (Math.Abs(viewModel.ZoomLevel - Host.ZoomLevel) > 0.001)
                        viewModel.ZoomLevel = Host.ZoomLevel;
                    var effectivePanX = Host.TranslateTransform.X - Host.ScrollViewer.HorizontalOffset;
                    var effectivePanY = Host.TranslateTransform.Y - Host.ScrollViewer.VerticalOffset;
                    if (Math.Abs(viewModel.PanX - effectivePanX) > 0.1)
                        viewModel.PanX = effectivePanX;
                    if (Math.Abs(viewModel.PanY - effectivePanY) > 0.1)
                        viewModel.PanY = effectivePanY;
                }
                
                ScheduleCanvasSizeUpdate();
            };
        }
        
        /// <summary>
        /// Cập nhật position của title chỉ cho nodes trong viewport sau khi zoom kết thúc
        /// Tối ưu: Chỉ update nodes visible thay vì tất cả để giảm layout passes và lag
        /// </summary>
        private void UpdateVisibleTitlePositionsAfterZoom()
        {
            var viewModel = Host.ViewModel;
            if (viewModel == null) return;
            
            var host = Host;
            var viewportCullingService = host.ViewportCullingService;
            
            // Tính toán viewport bounds một lần
            Rect viewportBounds = Rect.Empty;
            if (viewportCullingService != null)
            {
                // Sử dụng reflection hoặc public method để lấy viewport bounds
                // Nếu không có, tính toán đơn giản
                try
                {
                    var scrollViewer = host.ScrollViewer;
                    if (scrollViewer.ViewportWidth > 0 && scrollViewer.ViewportHeight > 0)
                    {
                        double zoom = host.ZoomLevel;
                        double translateX = host.TranslateTransform.X;
                        double translateY = host.TranslateTransform.Y;
                        
                        double canvasLeft = (scrollViewer.HorizontalOffset - translateX) / zoom;
                        double canvasTop = (scrollViewer.VerticalOffset - translateY) / zoom;
                        double canvasWidth = scrollViewer.ViewportWidth / zoom;
                        double canvasHeight = scrollViewer.ViewportHeight / zoom;
                        
                        // Thêm margin lớn để đảm bảo không bỏ sót
                        viewportBounds = new Rect(
                            canvasLeft - 300,
                            canvasTop - 300,
                            canvasWidth + 600,
                            canvasHeight + 600
                        );
                    }
                }
                catch
                {
                    // Nếu lỗi, fallback: update tất cả visible nodes
                    viewportBounds = Rect.Empty;
                }
            }
            
            // Sử dụng DispatcherPriority thấp hơn để không block UI thread
            Host.Dispatcher.BeginInvoke(new Action(() =>
            {
                int updatedCount = 0;
                
                // Chỉ update nodes trong viewport hoặc đang visible
                foreach (var node in viewModel.Nodes)
                {
                    if (node.TitleTextBlockUI == null || node.Border == null) continue;
                    
                    // Chỉ update nodes đang visible để tránh layout passes không cần thiết
                    if (node.Border.Visibility != Visibility.Visible) continue;
                    
                    // Kiểm tra xem node có trong viewport không (nếu có viewport bounds)
                    if (!viewportBounds.IsEmpty)
                    {
                        // Tính toán node bounds đơn giản
                        double nodeWidth = node.Border.ActualWidth > 0 ? node.Border.ActualWidth : 300;
                        double nodeHeight = node.Border.ActualHeight > 0 ? node.Border.ActualHeight : 200;
                        Rect nodeBounds = new Rect(node.X, node.Y, nodeWidth, nodeHeight);
                        
                        // Nếu node không intersect với viewport, bỏ qua
                        if (!viewportBounds.IntersectsWith(nodeBounds)) continue;
                    }
                    
                    // Batch update: chỉ invalidate một lần và update title position
                    // Không gọi InvalidateMeasure/InvalidateArrange để tránh layout passes không cần thiết
                    // Thay vào đó, chỉ update title position trực tiếp
                    
                    // Explicitly update Start/End node title positions
                    if (node.Type == NodeType.Start)
                    {
                        StartNodeControl.UpdateTitlePosition(node, Host.WorkflowCanvas);
                        updatedCount++;
                    }
                    else if (node.Type == NodeType.End)
                    {
                        EndNodeControl.UpdateTitlePosition(node, Host.WorkflowCanvas);
                        updatedCount++;
                    }
                    // Với các node khác, title position sẽ được update tự động qua LayoutUpdated handler
                    // Không cần force invalidate để tránh lag
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void TryDisableExpensiveEffectsDuringZoom()
        {
            var vm = Host.ViewModel;
            if (vm == null) return;

            // only do this for large graphs to avoid overhead
            if (vm.Nodes.Count < 100) return;
            if (_effectsTemporarilyDisabled) return;

            _effectsTemporarilyDisabled = true;
            _savedBorderEffects.Clear();

            foreach (var n in vm.Nodes)
            {
                var b = n.Border;
                if (b == null) continue;
                if (b.Effect == null) continue;

                _savedBorderEffects[b] = b.Effect;
                b.Effect = null;
            }
        }

        private void RestoreExpensiveEffectsAfterZoom()
        {
            if (!_effectsTemporarilyDisabled) return;
            _effectsTemporarilyDisabled = false;

            foreach (var kvp in _savedBorderEffects)
            {
                // Border might have been removed/recreated; best-effort restore
                kvp.Key.Effect = kvp.Value;
            }
            _savedBorderEffects.Clear();
        }

        private void ScheduleCanvasSizeUpdate()
        {
            if (_canvasSizeUpdateTimer == null)
            {
                _canvasSizeUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(CanvasSizeUpdateDebounceMs)
                };
                _canvasSizeUpdateTimer.Tick += (s, e) =>
                {
                    _canvasSizeUpdateTimer.Stop();
                    UpdateCanvasSize();
                };
            }

            _canvasSizeUpdateTimer.Stop();
            _canvasSizeUpdateTimer.Start();
        }

        public void UpdateCanvasSize()
        {
            var viewModel = Host.ViewModel;
            _canvasSizeManager.UpdateSize(viewModel?.Nodes ?? Enumerable.Empty<FlowMy.Models.WorkflowNode>());
        }
    }
}

