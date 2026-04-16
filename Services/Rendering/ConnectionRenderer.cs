using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ShapesPath = System.Windows.Shapes.Path;
using FlowMy.Models;
using FlowMy.Services.Geometry;
using FlowMy.Services.Interaction;

namespace FlowMy.Services.Rendering
{
    public sealed class ConnectionRenderer : IConnectionRenderer
    {
        public sealed class ConnectionTag
        {
            public string Type { get; set; } = "Connection";
            public ShapesPath? ArrowHead { get; set; }
        }
        
        /// <summary>
        /// Cache for connection path geometry to avoid expensive recalculations
        /// </summary>
        public sealed class ConnectionPathCache
        {
            public PathGeometry? CachedPath { get; set; }
            public Point LastStartPos { get; set; }
            public Point LastEndPos { get; set; }
            public double LastZoomLevel { get; set; }
            public ConnectionLineStyle LastLineStyle { get; set; }
            
            /// <summary>
            /// Kiểm tra xem cache có cần update không (nếu nodes di chuyển >5px hoặc zoom thay đổi)
            /// </summary>
            public bool NeedsUpdate(Point start, Point end, double zoom, ConnectionLineStyle lineStyle)
            {
                const double threshold = 5.0; // pixels - chỉ recalculate khi di chuyển >5px
                const double zoomThreshold = 0.1; // zoom change threshold
                
                return CachedPath == null ||
                       LastLineStyle != lineStyle ||
                       (start - LastStartPos).Length > threshold ||
                       (end - LastEndPos).Length > threshold ||
                       Math.Abs(zoom - LastZoomLevel) > zoomThreshold;
            }
            
            /// <summary>
            /// Cập nhật cache với path mới
            /// </summary>
            public void UpdateCache(PathGeometry path, Point start, Point end, double zoom, ConnectionLineStyle lineStyle)
            {
                CachedPath = path;
                LastStartPos = start;
                LastEndPos = end;
                LastZoomLevel = zoom;
                LastLineStyle = lineStyle;
            }
        }

        // ✅ Z-Index: mặc định line dưới node (đẹp),
        // nhưng nếu line nằm TRONG LoopBody thì nâng lên trên LoopBody để thao tác được.
        // LoopBody thường ~100, node thường ~10000 theo ZIndexManager.
        private const int ConnectionBaseUnderNodes = 10;     // dưới tất cả node
        private const int ConnectionBaseAboveLoopBody = 500; // trên LoopBody (100) nhưng dưới node thường (10000)

        // Ensure delete buttons are above loop/async body chrome even when nodes/ports are raised
        // (e.g. due to selection/drag). Ports already use a much higher "PortTier".
        private const int DeleteButtonZIndexMin = 900000;

        private readonly IWorkflowEditorHostAccessor _hostAccessor;

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();
        private Canvas _canvas => Host.WorkflowCanvas;
        // Workflow-level line style (global). Per-connection override được xử lý trong GetLineStyleForConnection().
        private ConnectionLineStyle LineStyle => Host.ConnectionLineStyle;
        private bool IsAnimationEnabled => Host.IsAnimationEnabled;
        private ConnectionAnimationDisplayMode AnimationDisplayMode => Host.ConnectionAnimationDisplayMode;
        private ConnectionColorMode ColorMode => Host.ConnectionColorMode;
        private Color CustomColor => Host.CustomConnectionColor;
        private ConnectionEnergyColorMode EnergyColorMode => Host.ConnectionEnergyColorMode;
        private Color CustomEnergyColor => Host.CustomEnergyColor;
        private double EnergyDotGap => Host.EnergyDotGap;
        private double EnergyDotThicknessExtra => Host.EnergyDotThicknessExtra;
        private string EnergyDotText => Host.EnergyDotText ?? string.Empty;
        private bool EnergyDotTextRotate => Host.EnergyDotTextRotate;
        private double EnergyRunSpeed => Host.EnergyRunSpeed;
        private double EnergyTextSpinSeconds => Host.EnergyTextSpinSeconds;
        private bool EnergyMeteorMode => Host.EnergyMeteorMode;

        private readonly IPathGeometryGenerator _bezier;
        private readonly IPathGeometryGenerator _orthogonal;
        private readonly IPathGeometryGenerator _straight;
        private readonly OrthogonalV2GeometryGenerator _orthogonalV2;

        // Windy (gió thổi) animation state
        private EventHandler? _windRenderingHandler;
        private bool _isWindRenderingActive;
        private DateTime _lastWindFrameTime = DateTime.MinValue;
        private readonly Random _windRandom = new Random();
        
        // Random cho thời gian wave (2-5 giây)
        private double GetRandomWaveInterval()
        {
            return 2.0 + _windRandom.NextDouble() * 3.0; // 2-5 giây
        }

        public ConnectionRenderer(
            IWorkflowEditorHostAccessor hostAccessor,
            BezierGeometryGenerator bezier,
            OrthogonalGeometryGenerator orthogonal,
            StraightGeometryGenerator straight,
            OrthogonalV2GeometryGenerator orthogonalV2)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _bezier = bezier ?? throw new ArgumentNullException(nameof(bezier));
            _orthogonal = orthogonal ?? throw new ArgumentNullException(nameof(orthogonal));
            _straight = straight ?? throw new ArgumentNullException(nameof(straight));
            _orthogonalV2 = orthogonalV2 ?? throw new ArgumentNullException(nameof(orthogonalV2));
        }
        
        /// <summary>
        /// Lấy số lượng connections tối đa được phép animate dựa trên GPU quality
        /// Low/Medium quality sẽ giới hạn animations để cải thiện performance
        /// </summary>
        private int GetMaxAnimatedConnections()
        {
            var quality = GpuOptimizationHelper.GetGpuRenderQuality();
            return quality switch
            {
                GpuRenderQuality.Low => 10,        // Chỉ 10 connections animated cho máy yếu
                GpuRenderQuality.Medium => 30,     // 30 connections cho máy TB
                GpuRenderQuality.High => 100,      // 100 connections cho máy mạnh
                GpuRenderQuality.Best => int.MaxValue,  // Unlimited cho máy siêu mạnh
                _ => 30
            };
        }
        
        /// <summary>
        /// Tính khoảng cách từ connection đến viewport center (để ưu tiên animate gần center)
        /// </summary>
        private double GetDistanceToViewportCenter(WorkflowConnection conn)
        {
            if (conn.FromNode == null || conn.ToNode == null)
                return double.MaxValue;
            
            // Lấy viewport center
            var host = Host;
            var scrollViewer = host.ScrollViewer;
            double viewportCenterX = scrollViewer.HorizontalOffset + (scrollViewer.ViewportWidth / 2);
            double viewportCenterY = scrollViewer.VerticalOffset + (scrollViewer.ViewportHeight / 2);
            
            // Tính trung điểm của connection
            double connMidX = (conn.FromNode.X + conn.ToNode.X) / 2.0;
            double connMidY = (conn.FromNode.Y + conn.ToNode.Y) / 2.0;
            
            // Khoảng cách Manhattan (nhanh hơn Euclidean, đủ dùng cho sorting)
            double dx = Math.Abs(connMidX - viewportCenterX);
            double dy = Math.Abs(connMidY - viewportCenterY);
            
            return dx + dy;
        }

        /// <summary>
        /// Build obstacle rects from all nodes except source/target of the given connection.
        /// Used by OrthogonalV2 to route lines around nodes.
        /// </summary>
        private IReadOnlyList<Rect> GetObstacleRects(WorkflowConnection connection)
        {
            var nodes = Host.ViewModel?.Nodes;
            if (nodes == null || nodes.Count == 0) return Array.Empty<Rect>();

            var result = new List<Rect>();
            foreach (var node in nodes)
            {
                // Skip source and target nodes of this connection
                if (node == connection.FromNode || node == connection.ToNode) continue;

                // Get actual rendered size from the Border element
                double w = node.Border?.ActualWidth ?? 150;
                double h = node.Border?.ActualHeight ?? 80;
                if (w <= 0) w = 150;
                if (h <= 0) h = 80;

                result.Add(new Rect(node.X, node.Y, w, h));
            }
            return result;
        }

        public void RenderAllConnections(
            IEnumerable<WorkflowConnection> connections,
            Action<WorkflowConnection?> setSelectedConnection,
            Action focusWindow,
            Action<WorkflowConnection> requestDeleteConnection)
        {
            var connectionList = connections?.ToList() ?? new List<WorkflowConnection>();

            // Xóa arrow heads trước
            var arrowsToRemove = _canvas.Children
                .OfType<ShapesPath>()
                .Where(p => p.Tag?.ToString() == "ConnectionArrow")
                .ToList();
            foreach (var arrow in arrowsToRemove)
            {
                GpuOptimizationHelper.SafeRemoveFromCanvas(arrow, _canvas);
            }

            // Xóa delete buttons cũ
            var buttonsToRemove = _canvas.Children
                .OfType<Button>()
                .Where(b => b.Tag is WorkflowConnection)
                .ToList();
            foreach (var button in buttonsToRemove) _canvas.Children.Remove(button);

            // Xóa hit areas cũ (đang được giữ trên model)
            foreach (var conn in connectionList)
            {
                if (conn.HitArea != null)
                {
                    GpuOptimizationHelper.SafeRemoveFromCanvas(conn.HitArea, _canvas);
                }
            }

            // Xóa các connection lines cũ
            var connectionsToRemove = _canvas.Children
                .OfType<ShapesPath>()
                .Where(p => p.Tag?.ToString() == "Connection" || p.Tag is ConnectionTag)
                .ToList();
            foreach (var path in connectionsToRemove)
            {
                if (path.Tag is ConnectionTag tag && tag.ArrowHead != null && _canvas.Children.Contains(tag.ArrowHead))
                {
                    GpuOptimizationHelper.SafeRemoveFromCanvas(tag.ArrowHead, _canvas);
                }
                GpuOptimizationHelper.SafeRemoveFromCanvas(path, _canvas);
            }

            foreach (var connection in connectionList)
            {
                RenderConnection(connection, setSelectedConnection, focusWindow, requestDeleteConnection);
            }

            // Sau khi render xong toàn bộ connections, đảm bảo animation (dash / energy) được áp dụng
            // theo cấu hình hiện tại của host (IsAnimationEnabled, v.v.).
            UpdateAllConnectionAnimations(connectionList);
        }

        private static Point GetValidPortPosition(NodePort? port, WorkflowNode node, bool isInput)
        {
            // Nếu port đã có tọa độ hợp lệ thì dùng luôn
            if (port != null &&
                !double.IsNaN(port.PositionPoint.X) &&
                !double.IsNaN(port.PositionPoint.Y) &&
                !double.IsInfinity(port.PositionPoint.X) &&
                !double.IsInfinity(port.PositionPoint.Y))
            {
                return port.PositionPoint;
            }

            // Fallback an toàn dựa trên tọa độ node (không phụ thuộc PositionPoint của port)
            // Mặc định node rộng ~150, cao ~80 => đặt port input bên trái, output bên phải, giữa chiều cao.
            return isInput
                ? new Point(node.X, node.Y + 40)
                : new Point(node.X + 150, node.Y + 40);
        }

        public void RenderConnection(
            WorkflowConnection connection,
            Action<WorkflowConnection?> setSelectedConnection,
            Action focusWindow,
            Action<WorkflowConnection> requestDeleteConnection)
        {
            if (connection.LineUI != null && _canvas.Children.Contains(connection.LineUI))
            {
                UpdateConnectionPath(connection);
                UpdateConnectionColor(connection);
                
                // Đảm bảo nút xóa vẫn hiển thị/ẩn đúng theo thuộc tính IsDeleteVisible
                CreateDeleteButton(connection, requestDeleteConnection, setSelectedConnection);
                return;
            }

            Point start, end;
            PortPosition? startDirection = null;
            PortPosition? endDirection = null;

            if (connection.FromPort != null && connection.ToPort != null)
            {
                start = GetValidPortPosition(connection.FromPort, connection.FromNode, connection.FromPort.IsInput);
                end = GetValidPortPosition(connection.ToPort, connection.ToNode, connection.ToPort.IsInput);
                startDirection = connection.FromPort.Position;
                endDirection = connection.ToPort.Position;

                if (endDirection.HasValue)
                {
                    end = ShortenEndPointForArrow(end, endDirection.Value, 12);
                }
            }
            else
            {
                // Backward-compat: nếu chưa có port, dùng vị trí input/output mặc định của node
                start = connection.IsFromInput
                    ? connection.FromNode.InputPortPosition
                    : connection.FromNode.OutputPortPosition;
                end = connection.IsFromInput
                    ? connection.ToNode.OutputPortPosition
                    : connection.ToNode.InputPortPosition;

                startDirection = connection.IsFromInput ? PortPosition.Left : PortPosition.Right;
                endDirection = connection.IsFromInput ? PortPosition.Right : PortPosition.Left;

                if (endDirection.HasValue)
                {
                    end = ShortenEndPointForArrow(end, endDirection.Value, 12);
                }
            }

            Color lineColor = GetConnectionColor(connection);

            // Tạo line cho connection này dựa trên line style thực tế (có thể override theo ReuseRoutes)
            var lineStyle = GetLineStyleForConnection(connection);

            var path = new ShapesPath
            {
                Stroke = new SolidColorBrush(lineColor),
                StrokeThickness = lineStyle == ConnectionLineStyle.Orthogonal ? 2.0 : 2.5,
                Fill = null,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            PathGeometry geometry = lineStyle switch
            {
                ConnectionLineStyle.Orthogonal        => _orthogonal.Generate(start, end, startDirection, endDirection),
                ConnectionLineStyle.Straight          => _straight.Generate(start, end, startDirection, endDirection),
                ConnectionLineStyle.SmoothOrthogonal  => _orthogonal.Generate(start, end, startDirection, endDirection),
                ConnectionLineStyle.Arc               => GenerateArcGeometry(start, end, startDirection, endDirection),
                ConnectionLineStyle.RadialFanout      => GenerateRadialFanoutGeometry(start, end, startDirection, endDirection),
                ConnectionLineStyle.Windy             => _bezier.Generate(start, end, startDirection, endDirection),
                ConnectionLineStyle.OrthogonalV2      => _orthogonalV2.Generate(start, end, startDirection, endDirection, GetObstacleRects(connection)),
                _                                     => _bezier.Generate(start, end, startDirection, endDirection),
            };

            path.Data = geometry;

            ShapesPath? arrowHead = null;
            // RenderConnection luôn dùng line liền (không dashed) nên vẽ arrow head
            arrowHead = CreateArrowHeadElement(geometry, lineColor, endDirection);

            path.Tag = new ConnectionTag { Type = "Connection", ArrowHead = arrowHead };

            // Áp dụng GPU optimization cho arrow head nếu có
            if (arrowHead != null)
            {
                GpuOptimizationHelper.ApplyToPath(arrowHead, allowCache: true);
            }

            connection.LineUI = path;

            // Áp dụng GPU optimization cho connection line
            GpuOptimizationHelper.ApplyToPath(path, allowCache: !IsAnimationEnabled);

            var geometry2 = path.Data as PathGeometry ?? new PathGeometry();
            var hitArea = new ShapesPath
            {
                Data = geometry2,
                Stroke = new SolidColorBrush(Colors.Transparent),
                StrokeThickness = 15,
                Fill = null,
                IsHitTestVisible = true,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = connection
            };

            connection.HitArea = hitArea;

            // Áp dụng GPU optimization cho hit area (không cache vì cần hit test)
            GpuOptimizationHelper.ApplyToPath(hitArea, allowCache: false);

            hitArea.MouseEnter += (s, e) =>
            {
                setSelectedConnection(connection);
                focusWindow();
            };
            hitArea.MouseDown += (s, e) =>
            {
                setSelectedConnection(connection);
                focusWindow();
            };
            hitArea.MouseLeave += (s, e) =>
            {
                if (connection.DeleteButton == null || !connection.DeleteButton.IsMouseOver)
                {
                    setSelectedConnection(null);
                }
            };

            path.MouseEnter += (s, e) =>
            {
                setSelectedConnection(connection);
                focusWindow();
            };
            path.MouseDown += (s, e) =>
            {
                setSelectedConnection(connection);
                focusWindow();
            };

            ApplyConnectionZIndex(connection, start, end);
            _canvas.Children.Add(hitArea);
            _canvas.Children.Add(path);

            if (path.Tag is ConnectionTag tag && tag.ArrowHead != null)
            {
                _canvas.Children.Add(tag.ArrowHead);
            }

            CreateDeleteButton(connection, requestDeleteConnection, setSelectedConnection);
            ApplyConnectionZIndex(connection, start, end);
            
            // Đảm bảo wind rendering loop chạy nếu connection là Windy style
            if (IsWindyStyle(connection))
            {
                EnsureWindRenderingLoop();
            }
        }

        public void UpdateConnectionPath(WorkflowConnection connection)
        {
            if (connection.LineUI == null) return;

            Point start, end;
            PortPosition? startDirection = null;
            PortPosition? endDirection = null;

            if (connection.FromPort != null && connection.ToPort != null)
            {
                start = GetValidPortPosition(connection.FromPort, connection.FromNode, connection.FromPort.IsInput);
                end = GetValidPortPosition(connection.ToPort, connection.ToNode, connection.ToPort.IsInput);
                startDirection = connection.FromPort.Position;
                endDirection = connection.ToPort.Position;

                if (endDirection.HasValue)
                {
                    end = ShortenEndPointForArrow(end, endDirection.Value, 12);
                }
            }
            else
            {
                // Backward-compat: nếu chưa có port, dùng vị trí input/output mặc định của node
                start = connection.IsFromInput
                    ? connection.FromNode.InputPortPosition
                    : connection.FromNode.OutputPortPosition;
                end = connection.IsFromInput
                    ? connection.ToNode.OutputPortPosition
                    : connection.ToNode.InputPortPosition;

                startDirection = connection.IsFromInput ? PortPosition.Left : PortPosition.Right;
                endDirection = connection.IsFromInput ? PortPosition.Right : PortPosition.Left;

                if (endDirection.HasValue)
                {
                    end = ShortenEndPointForArrow(end, endDirection.Value, 12);
                }
            }


            var lineStyle = GetLineStyleForConnection(connection);

            // ✅ PERFORMANCE: Path caching - avoid expensive recalculations
            // Initialize cache if needed
            if (connection.PathCache == null)
            {
                connection.PathCache = new ConnectionPathCache();
            }
            
            PathGeometry geometry;
            double currentZoom = 1.0; // Placeholder - can enhance later with actual zoom
            
            // Check if we can reuse cached path  
            if (!connection.PathCache.NeedsUpdate(start, end, currentZoom, lineStyle))
            {
                // ✅ Use cached path - saves expensive path generation
                geometry = connection.PathCache.CachedPath!;
            }
            else
            {
                // Recalculate path geometry
                geometry = lineStyle switch
                {
                    ConnectionLineStyle.Orthogonal        => _orthogonal.Generate(start, end, startDirection, endDirection),
                    ConnectionLineStyle.Straight          => _straight.Generate(start, end, startDirection, endDirection),
                    ConnectionLineStyle.SmoothOrthogonal  => _orthogonal.Generate(start, end, startDirection, endDirection),
                    ConnectionLineStyle.Arc               => GenerateArcGeometry(start, end, startDirection, endDirection),
                    ConnectionLineStyle.RadialFanout      => GenerateRadialFanoutGeometry(start, end, startDirection, endDirection),
                    ConnectionLineStyle.Windy             => _bezier.Generate(start, end, startDirection, endDirection),
                    ConnectionLineStyle.OrthogonalV2      => _orthogonalV2.Generate(start, end, startDirection, endDirection, GetObstacleRects(connection)),
                    _                                     => _bezier.Generate(start, end, startDirection, endDirection),
                };
                
                // ✅ Update cache with new path
                connection.PathCache.UpdateCache(geometry, start, end, currentZoom, lineStyle);
            }

            connection.LineUI.Data = geometry;
            
            // Tối ưu: Không invalidate mỗi lần update - chỉ update Data, WPF sẽ tự render
            // Invalidate chỉ khi thực sự cần (khi drag kết thúc hoặc có thay đổi lớn)
            // Điều này giúp GPU render hiệu quả hơn và giảm lag

            if (connection.HitArea != null)
            {
                connection.HitArea.Data = geometry;
            }

            if (connection.EnergyUI != null)
            {
                connection.EnergyUI.Data = geometry;
            }

            // Reset base control points và wind state cho Windy để lần render tiếp theo lấy geometry mới làm gốc
            if (IsWindyStyle(connection))
            {
                connection.WindBaseControlPoint1 = null;
                connection.WindBaseControlPoint2 = null;
                connection.WindNormal = new Vector(0, 0); // Reset normal để tính lại
                // Giữ nguyên WindTime, WaveAmplitude để animation tiếp tục mượt mà
            }

            // Xóa arrow head cũ
            if (connection.LineUI.Tag is ConnectionTag oldTag && oldTag.ArrowHead != null && _canvas.Children.Contains(oldTag.ArrowHead))
            {
                _canvas.Children.Remove(oldTag.ArrowHead);
            }

            var color = GetConnectionColor(connection);
            PortPosition? endPortDir = connection.ToPort?.Position ?? (connection.IsFromInput ? PortPosition.Right : PortPosition.Left);
            var newArrow = CreateArrowHeadElement(geometry, color, endPortDir);
            if (newArrow != null)
            {
                _canvas.Children.Add(newArrow);
            }

            connection.LineUI.Tag = new ConnectionTag { Type = "Connection", ArrowHead = newArrow };

            // Cập nhật màu line và arrow theo FromPort.ColorKey (khi thêm/xóa điều kiện conditional, port đổi ColorKey)
            UpdateConnectionColor(connection);

            // Áp dụng GPU optimization cho connection line khi update path
            if (connection.LineUI != null)
            {
                GpuOptimizationHelper.ApplyToPath(connection.LineUI, allowCache: !IsAnimationEnabled);
            }

            UpdateDeleteButtonPosition(connection);
            ApplyConnectionZIndex(connection, start, end);
        }

        private Color GetEnergyColor(WorkflowConnection connection)
        {
            return EnergyColorMode switch
            {
                ConnectionEnergyColorMode.FollowLineColor => GetConnectionColor(connection),
                ConnectionEnergyColorMode.CustomColor => CustomEnergyColor,
                _ => GetConnectionColor(connection)
            };
        }

        private ShapesPath EnsureEnergyOverlay(WorkflowConnection connection)
        {
            if (connection.LineUI == null)
                throw new InvalidOperationException("Cannot create energy overlay without LineUI.");

            if (connection.EnergyUI != null)
            {
                if (!_canvas.Children.Contains(connection.EnergyUI))
                {
                    _canvas.Children.Add(connection.EnergyUI);
                }
                // Ensure Z-index stays in sync (same tier as line, but drawn on top)
                Panel.SetZIndex(connection.EnergyUI, GetZBaseForConnection(connection) + 1);
                return connection.EnergyUI;
            }

            var overlay = new ShapesPath
            {
                Data = connection.LineUI.Data,
                Fill = null,
                IsHitTestVisible = false,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0.9,
                Tag = connection
            };

            connection.EnergyUI = overlay;
            _canvas.Children.Add(overlay);
            Panel.SetZIndex(overlay, GetZBaseForConnection(connection) + 1);
            
            // Áp dụng GPU optimization cho energy overlay (không cache vì có animation)
            GpuOptimizationHelper.ApplyToPath(overlay, allowCache: false);
            
            return overlay;
        }

        private void RemoveEnergyOverlay(WorkflowConnection connection)
        {
            if (connection.EnergyUI != null)
            {
                if (_canvas.Children.Contains(connection.EnergyUI))
                {
                    _canvas.Children.Remove(connection.EnergyUI);
                }
                connection.EnergyUI = null;
            }
        }

        private static double EstimatePathLength(PathGeometry geometry)
        {
            // Flatten geometry để ước lượng length (đủ tốt để set tốc độ ball)
            var flat = geometry.GetFlattenedPathGeometry(0.5, ToleranceType.Absolute);
            double len = 0;

            foreach (var fig in flat.Figures)
            {
                var last = fig.StartPoint;
                foreach (var seg in fig.Segments)
                {
                    if (seg is LineSegment ls)
                    {
                        var p = ls.Point;
                        len += (p - last).Length;
                        last = p;
                    }
                    else if (seg is PolyLineSegment pls)
                    {
                        foreach (var p in pls.Points)
                        {
                            len += (p - last).Length;
                            last = p;
                        }
                    }
                }
            }

            return Math.Max(1, len);
        }

        private System.Windows.Shapes.Ellipse EnsureEnergyBall(WorkflowConnection connection, Color energyColor)
        {
            var diameter = EnergyMeteorMode ? 14.0 : 10.0;
            var radius = diameter / 2.0;

            if (connection.EnergyBallUI != null)
            {
                if (!_canvas.Children.Contains(connection.EnergyBallUI))
                {
                    _canvas.Children.Add(connection.EnergyBallUI);
                }
                Panel.SetZIndex(connection.EnergyBallUI, GetZBaseForConnection(connection) + 2);
                return connection.EnergyBallUI;
            }

            // Fill: lõi trắng + viền màu năng lượng
            var fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.35),
                Center = new Point(0.45, 0.45),
                RadiusX = 0.9,
                RadiusY = 0.9
            };
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.0));
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(220, energyColor.R, energyColor.G, energyColor.B), 0.55));
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(0, energyColor.R, energyColor.G, energyColor.B), 1.0));

            var ball = new System.Windows.Shapes.Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = fill,
                Stroke = new SolidColorBrush(Color.FromArgb(200, energyColor.R, energyColor.G, energyColor.B)),
                StrokeThickness = 1,
                IsHitTestVisible = false,
                Tag = connection,
                Effect = GpuOptimizationHelper.CreateDropShadowEffect(),
                RenderTransform = new TranslateTransform(-radius, -radius) // center on path point
            };

            connection.EnergyBallUI = ball;
            _canvas.Children.Add(ball);
            Panel.SetZIndex(ball, GetZBaseForConnection(connection) + 2);
            
            // Áp dụng GPU optimization cho energy ball (không cache vì có animation)
            GpuOptimizationHelper.ApplyToShape(ball);
            
            return ball;
        }

        private void RemoveEnergyBall(WorkflowConnection connection)
        {
            if (connection.EnergyBallUI != null)
            {
                // Stop animations
                connection.EnergyBallUI.BeginAnimation(Canvas.LeftProperty, null);
                connection.EnergyBallUI.BeginAnimation(Canvas.TopProperty, null);
                connection.EnergyBallUI.BeginAnimation(UIElement.OpacityProperty, null);

                if (_canvas.Children.Contains(connection.EnergyBallUI))
                {
                    _canvas.Children.Remove(connection.EnergyBallUI);
                }
                connection.EnergyBallUI = null;
            }
        }

        private void StartEnergyBallAnimation(WorkflowConnection connection, PathGeometry geometry, Color energyColor)
        {
            if (!IsAnimationEnabled) return;

            var ball = EnsureEnergyBall(connection, energyColor);

            // Duration theo length để tốc độ đều
            var length = EstimatePathLength(geometry);
            const double pixelsPerSecond = 520; // tune speed here
            var seconds = length / pixelsPerSecond;
            seconds = Math.Clamp(seconds, 0.45, 1.8);

            var duration = new Duration(TimeSpan.FromSeconds(seconds));

            var animX = new DoubleAnimationUsingPath
            {
                PathGeometry = geometry,
                Source = PathAnimationSource.X,
                Duration = duration,
                RepeatBehavior = RepeatBehavior.Forever
            };

            var animY = new DoubleAnimationUsingPath
            {
                PathGeometry = geometry,
                Source = PathAnimationSource.Y,
                Duration = duration,
                RepeatBehavior = RepeatBehavior.Forever
            };

            ball.BeginAnimation(Canvas.LeftProperty, animX);
            ball.BeginAnimation(Canvas.TopProperty, animY);

            var pulse = new DoubleAnimation
            {
                From = 0.65,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.35)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            ball.BeginAnimation(UIElement.OpacityProperty, pulse);
        }

        private FrameworkElement EnsureEnergyText(WorkflowConnection connection, Color energyColor)
        {
            const double size = 18;
            const double radius = size / 2.0;

            if (connection.EnergyTextUI != null)
            {
                if (!_canvas.Children.Contains(connection.EnergyTextUI))
                    _canvas.Children.Add(connection.EnergyTextUI);
                Panel.SetZIndex(connection.EnergyTextUI, GetZBaseForConnection(connection) + 2);
                return connection.EnergyTextUI;
            }

            var text = new TextBlock
            {
                Text = EnergyDotText,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var border = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(70, energyColor.R, energyColor.G, energyColor.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, energyColor.R, energyColor.G, energyColor.B)),
                BorderThickness = new Thickness(1),
                Child = text,
                IsHitTestVisible = false,
                Tag = connection,
                Effect = GpuOptimizationHelper.CreateDropShadowEffect()  
            };

            // Center on path point + optional rotation
            var tg = new TransformGroup();
            tg.Children.Add(new TranslateTransform(-radius, -radius));
            tg.Children.Add(new RotateTransform(0));
            border.RenderTransformOrigin = new Point(0.5, 0.5);
            border.RenderTransform = tg;

            connection.EnergyTextUI = border;
            _canvas.Children.Add(border);
            Panel.SetZIndex(border, GetZBaseForConnection(connection) + 2);
            return border;
        }

        private void RemoveEnergyText(WorkflowConnection connection)
        {
            if (connection.EnergyTextUI != null)
            {
                connection.EnergyTextUI.BeginAnimation(Canvas.LeftProperty, null);
                connection.EnergyTextUI.BeginAnimation(Canvas.TopProperty, null);
                connection.EnergyTextUI.BeginAnimation(UIElement.OpacityProperty, null);

                if (connection.EnergyTextUI.RenderTransform is TransformGroup g)
                {
                    foreach (var t in g.Children)
                    {
                        if (t is RotateTransform rt)
                            rt.BeginAnimation(RotateTransform.AngleProperty, null);
                    }
                }

                if (_canvas.Children.Contains(connection.EnergyTextUI))
                    _canvas.Children.Remove(connection.EnergyTextUI);
                connection.EnergyTextUI = null;
            }
        }

        private void StartEnergyTextAnimation(WorkflowConnection connection, PathGeometry geometry, Color energyColor)
        {
            if (!IsAnimationEnabled) return;
            if (string.IsNullOrWhiteSpace(EnergyDotText)) return;

            var el = EnsureEnergyText(connection, energyColor);

            // duration theo length để chạy "nhanh hơn" line dash animation
            var length = EstimatePathLength(geometry);
            var speed = Math.Max(0.05, EnergyRunSpeed);
            var pixelsPerSecond = 520.0 * speed; // base 520px/s, speed>1 => nhanh hơn
            var seconds = Math.Clamp(length / pixelsPerSecond, 0.45, 2.2);
            var duration = new Duration(TimeSpan.FromSeconds(seconds));

            var animX = new DoubleAnimationUsingPath
            {
                PathGeometry = geometry,
                Source = PathAnimationSource.X,
                Duration = duration,
                RepeatBehavior = RepeatBehavior.Forever
            };

            var animY = new DoubleAnimationUsingPath
            {
                PathGeometry = geometry,
                Source = PathAnimationSource.Y,
                Duration = duration,
                RepeatBehavior = RepeatBehavior.Forever
            };

            el.BeginAnimation(Canvas.LeftProperty, animX);
            el.BeginAnimation(Canvas.TopProperty, animY);

            // rotate 360° continuously
            if (el.RenderTransform is TransformGroup tg)
            {
                var rot = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                if (rot != null)
                {
                    rot.BeginAnimation(RotateTransform.AngleProperty, null);
                    if (EnergyDotTextRotate)
                    {
                        var spin = new DoubleAnimation
                        {
                            From = 0,
                            To = 360,
                            Duration = new Duration(TimeSpan.FromSeconds(Math.Max(0.05, EnergyTextSpinSeconds))),
                            RepeatBehavior = RepeatBehavior.Forever
                        };
                        rot.BeginAnimation(RotateTransform.AngleProperty, spin);
                    }
                }
            }

            var pulse = new DoubleAnimation
            {
                From = 0.75,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.35)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            el.BeginAnimation(UIElement.OpacityProperty, pulse);
        }

        public void UpdateConnectionColor(WorkflowConnection connection)
        {
            if (connection.LineUI == null) return;

            var lineStyle = GetLineStyleForConnection(connection);
            var baseThickness = lineStyle == ConnectionLineStyle.Orthogonal ? 2.0 : 2.5;

            // Base line luôn theo color mode hiện tại
            var lineColor = GetConnectionColor(connection);
            connection.LineUI.StrokeThickness = baseThickness;
            connection.LineUI.Effect = null;

            connection.LineUI.Stroke = new SolidColorBrush(lineColor);
            connection.LineUI.StrokeDashCap = PenLineCap.Flat;

            if (connection.LineUI.Tag is ConnectionTag tag && tag.ArrowHead != null)
            {
                tag.ArrowHead.Fill = new SolidColorBrush(lineColor);
                tag.ArrowHead.Stroke = new SolidColorBrush(lineColor);
                tag.ArrowHead.Effect = null;
            }

            // Khi chạy: đổi sang line "chấm tròn chạy" (dotted running)
            if (connection.IsExecutionActive)
            {
                var energyColor = GetEnergyColor(connection);
                RemoveEnergyOverlay(connection);
                RemoveEnergyBall(connection);

                // Override stroke theo màu năng lượng
                connection.LineUI.Stroke = new SolidColorBrush(energyColor);
                connection.LineUI.StrokeThickness = baseThickness + EnergyDotThicknessExtra;
                connection.LineUI.StrokeDashCap = EnergyMeteorMode ? PenLineCap.Flat : PenLineCap.Round;
                connection.LineUI.Effect = null; // bỏ đổ bóng line khi đang chạy

                if (connection.LineUI.Tag is ConnectionTag tag2 && tag2.ArrowHead != null)
                {
                    // Arrow theo màu năng lượng khi đang chạy
                    tag2.ArrowHead.Fill = new SolidColorBrush(energyColor);
                    tag2.ArrowHead.Stroke = new SolidColorBrush(energyColor);
                    tag2.ArrowHead.Effect = null; // bỏ đổ bóng arrow khi đang chạy
                }
            }
            else
            {
                RemoveEnergyOverlay(connection);
                RemoveEnergyBall(connection);
                RemoveEnergyText(connection);
            }
        }

        public void UpdateAllConnectionAnimations(IEnumerable<WorkflowConnection> connections)
        {
            // ✅ OPTIMIZATION: Throttle animations based on GPU quality
            var maxAnimated = GetMaxAnimatedConnections();
            var connectionList = connections.ToList();
            
            // Phân loại connections: execution active vs idle animations
            var activeConnections = connectionList.Where(c => c.IsExecutionActive).ToList();
            var idleConnections = connectionList.Where(c => !c.IsExecutionActive).ToList();
            
            // ✅ OPTIMIZATION: Ưu tiên animate connections gần viewport center nhất
            // Execution animations luôn được ưu tiên (không throttle)
            // Idle animations sẽ được throttle dựa trên quality settings
            HashSet<WorkflowConnection> connectionsToAnimate;
            
            if (maxAnimated >= connectionList.Count)
            {
                // Enough budget for all connections
                connectionsToAnimate = new HashSet<WorkflowConnection>(connectionList);
            }
            else
            {
                connectionsToAnimate = new HashSet<WorkflowConnection>();
                
                // Luôn animate tất cả execution active connections
                connectionsToAnimate.UnionWith(activeConnections);
                
                // Còn budget cho idle connections không?
                int remainingBudget = maxAnimated - activeConnections.Count;
                if (remainingBudget > 0 && idleConnections.Count > 0)
                {
                    // Sort idle connections theo khoảng cách đến viewport center
                    var sortedIdle = idleConnections
                        .OrderBy(c => GetDistanceToViewportCenter(c))
                        .Take(remainingBudget);
                    connectionsToAnimate.UnionWith(sortedIdle);
                }
            }
            
            foreach (var conn in connectionList)
            {
                if (conn.LineUI == null) continue;

                // Stop all existing animations
                conn.LineUI.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
                if (conn.EnergyUI != null)
                {
                    conn.EnergyUI.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
                    conn.EnergyUI.BeginAnimation(UIElement.OpacityProperty, null);
                }
                if (conn.EnergyBallUI != null)
                {
                    conn.EnergyBallUI.BeginAnimation(Canvas.LeftProperty, null);
                    conn.EnergyBallUI.BeginAnimation(Canvas.TopProperty, null);
                    conn.EnergyBallUI.BeginAnimation(UIElement.OpacityProperty, null);
                }
                if (conn.EnergyTextUI != null)
                {
                    conn.EnergyTextUI.BeginAnimation(Canvas.LeftProperty, null);
                    conn.EnergyTextUI.BeginAnimation(Canvas.TopProperty, null);
                    conn.EnergyTextUI.BeginAnimation(UIElement.OpacityProperty, null);
                }

                // ✅ Check if this connection should be animated
                bool shouldAnimate = connectionsToAnimate.Contains(conn);

                if (conn.IsExecutionActive)
                {
                    if (AnimationDisplayMode == ConnectionAnimationDisplayMode.Dashed)
                    {
                        conn.LineUI.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
                        conn.LineUI.BeginAnimation(UIElement.OpacityProperty, null);
                        conn.LineUI.StrokeDashCap = PenLineCap.Flat;
                        conn.LineUI.StrokeDashArray = new DoubleCollection { 6, 4 };
                        conn.LineUI.StrokeDashOffset = 0;
                        conn.LineUI.Opacity = 1.0;
                        RemoveEnergyOverlay(conn);
                        RemoveEnergyBall(conn);
                        RemoveEnergyText(conn);
                        continue;
                    }

                    if (AnimationDisplayMode == ConnectionAnimationDisplayMode.Off)
                    {
                        conn.LineUI.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
                        conn.LineUI.StrokeDashArray = null;
                        conn.LineUI.StrokeDashOffset = 0;
                        conn.LineUI.StrokeDashCap = PenLineCap.Flat;
                        conn.LineUI.Opacity = 1.0;
                        RemoveEnergyOverlay(conn);
                        RemoveEnergyBall(conn);
                        RemoveEnergyText(conn);
                        continue;
                    }

                    // Pinned execution (vd: FileDownload đang chạy lâu):
                    // giữ tín hiệu "đang chạy" nhưng dùng hiệu ứng nhẹ để tránh giật UI.
                    if (conn.IsExecutionPinned)
                    {
                        conn.LineUI.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
                        conn.LineUI.StrokeDashArray = null;
                        conn.LineUI.StrokeDashOffset = 0;
                        conn.LineUI.StrokeDashCap = PenLineCap.Round;
                        conn.LineUI.Opacity = 0.9;

                        if (shouldAnimate)
                        {
                            conn.LineUI.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
                            {
                                From = 0.45,
                                To = 0.95,
                                Duration = new Duration(TimeSpan.FromSeconds(0.9)),
                                AutoReverse = true,
                                RepeatBehavior = RepeatBehavior.Forever
                            });
                        }
                        else
                        {
                            conn.LineUI.BeginAnimation(UIElement.OpacityProperty, null);
                        }

                        RemoveEnergyOverlay(conn);
                        RemoveEnergyBall(conn);
                        RemoveEnergyText(conn);
                        continue;
                    }

                    // Nếu có EnergyDotText => TEXT sẽ thay thế "line animation"
                    // (line vẫn hiển thị nhẹ để user thấy đường nối)
                    if (!string.IsNullOrWhiteSpace(EnergyDotText))
                    {
                        conn.LineUI.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
                        conn.LineUI.StrokeDashArray = null;
                        conn.LineUI.StrokeDashOffset = 0;
                        conn.LineUI.Opacity = 0.25;

                        if (shouldAnimate && conn.LineUI.Data is PathGeometry geo)
                        {
                            var energyColor = GetEnergyColor(conn);
                            StartEnergyTextAnimation(conn, geo, energyColor);
                        }
                        else
                        {
                            RemoveEnergyText(conn);
                        }
                    }
                    else
                    {
                        if (EnergyMeteorMode)
                        {
                            var energyColor = GetEnergyColor(conn);
                            // Meteor mode: spacing rõ để tránh cảm giác "gần như line liền"
                            var dashLength = Math.Max(7.0, EnergyDotGap * 1.2);
                            var dashGap = Math.Max(12.0, EnergyDotGap * 2.6);
                            var dashPattern = dashLength + dashGap;

                            conn.LineUI.Opacity = 0.78;
                            conn.LineUI.StrokeDashCap = PenLineCap.Flat;
                            conn.LineUI.StrokeDashArray = new DoubleCollection { dashLength, dashGap };
                            conn.LineUI.StrokeDashOffset = 0;

                            var overlay = EnsureEnergyOverlay(conn);
                            overlay.Data = conn.LineUI.Data;
                            overlay.Stroke = new SolidColorBrush(Color.FromArgb(170, energyColor.R, energyColor.G, energyColor.B));
                            overlay.StrokeThickness = conn.LineUI.StrokeThickness + 6.0;
                            overlay.StrokeDashCap = PenLineCap.Round;
                            // Chỉ giữ 1 "đuôi thiên thạch" nổi bật đang chạy
                            var tailLength = 26.0;
                            var tailGap = Math.Max(120.0, dashGap * 8.0);
                            overlay.StrokeDashArray = new DoubleCollection { tailLength, tailGap };
                            overlay.StrokeDashOffset = 0;
                            overlay.Opacity = 0.66;
                            overlay.Effect = new BlurEffect { Radius = 7.5 };

                            if (shouldAnimate)
                            {
                                var speed = Math.Max(0.05, EnergyRunSpeed);
                                var fast = new DoubleAnimation
                                {
                                    From = 0,
                                    To = -dashPattern,
                                    Duration = new Duration(TimeSpan.FromSeconds(0.24 / speed)),
                                    RepeatBehavior = RepeatBehavior.Forever,
                                    EasingFunction = null
                                };
                                conn.LineUI.BeginAnimation(Shape.StrokeDashOffsetProperty, fast);

                                var tailPattern = tailLength + tailGap;
                                var tail = new DoubleAnimation
                                {
                                    From = 0,
                                    To = -tailPattern,
                                    Duration = new Duration(TimeSpan.FromSeconds(0.24 / speed)),
                                    RepeatBehavior = RepeatBehavior.Forever,
                                    EasingFunction = null
                                };
                                overlay.BeginAnimation(Shape.StrokeDashOffsetProperty, tail);
                                overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
                                {
                                    From = 0.45,
                                    To = 0.75,
                                    Duration = new Duration(TimeSpan.FromSeconds(0.28)),
                                    AutoReverse = true,
                                    RepeatBehavior = RepeatBehavior.Forever
                                });

                                if (conn.LineUI.Data is PathGeometry geo)
                                {
                                    StartEnergyBallAnimation(conn, geo, energyColor);
                                }
                            }
                            else
                            {
                                RemoveEnergyOverlay(conn);
                                RemoveEnergyBall(conn);
                            }
                        }
                        else
                        {
                            // Line chấm tròn chạy: dash length rất nhỏ + dashcap Round
                            double dot = 0.0;
                            double gap = Math.Max(1.0, EnergyDotGap);
                            double pattern = dot + gap;

                            conn.LineUI.Opacity = 1.0;
                            conn.LineUI.StrokeDashCap = PenLineCap.Round;
                            conn.LineUI.StrokeDashArray = new DoubleCollection { dot, gap };
                            conn.LineUI.StrokeDashOffset = 0;

                            if (shouldAnimate)
                            {
                                var speed = Math.Max(0.05, EnergyRunSpeed);
                                var fast = new DoubleAnimation
                                {
                                    From = 0,
                                    To = -pattern,
                                    Duration = new Duration(TimeSpan.FromSeconds(0.28 / speed)),
                                    RepeatBehavior = RepeatBehavior.Forever,
                                    EasingFunction = null
                                };

                                conn.LineUI.BeginAnimation(Shape.StrokeDashOffsetProperty, fast);
                            }

                            RemoveEnergyOverlay(conn);
                            RemoveEnergyBall(conn);
                        }

                        RemoveEnergyText(conn);
                    }
                }
                else if (AnimationDisplayMode == ConnectionAnimationDisplayMode.Dashed)
                {
                    conn.LineUI.StrokeDashCap = PenLineCap.Flat;
                    conn.LineUI.StrokeDashArray = new DoubleCollection { 8, 5 };
                    conn.LineUI.StrokeDashOffset = 0;
                    conn.LineUI.Opacity = 1.0;
                    RemoveEnergyBall(conn);
                    RemoveEnergyText(conn);
                }
                else if (IsAnimationEnabled && shouldAnimate)
                {
                    // ✅ Only animate if within budget
                    double dashLength = 8;
                    double gapLength = 6;
                    double patternLength = dashLength + gapLength;

                    conn.LineUI.StrokeDashCap = PenLineCap.Flat;
                    conn.LineUI.StrokeDashArray = new DoubleCollection { dashLength, gapLength };
                    conn.LineUI.StrokeDashOffset = 0;

                    var dashAnimation = new DoubleAnimation
                    {
                        From = patternLength,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = null
                    };

                    Storyboard.SetTarget(dashAnimation, conn.LineUI);
                    Storyboard.SetTargetProperty(dashAnimation, new PropertyPath(Shape.StrokeDashOffsetProperty));

                    var storyboard = new Storyboard();
                    storyboard.Children.Add(dashAnimation);
                    storyboard.Begin(conn.LineUI, true);
                }
                else
                {
                    // No animation (either disabled or throttled)
                    conn.LineUI.StrokeDashArray = null;
                    conn.LineUI.StrokeDashOffset = 0;
                    conn.LineUI.StrokeDashCap = PenLineCap.Flat;
                    RemoveEnergyBall(conn);
                    RemoveEnergyText(conn);
                }
            }
        }

        public void UpdateAllConnectionColors(IEnumerable<WorkflowConnection> connections)
        {
            foreach (var conn in connections) UpdateConnectionColor(conn);
        }

        public void UpdateAllConnectionPaths(IEnumerable<WorkflowConnection> connections)
        {
            bool hasWindy = false;
            foreach (var conn in connections)
            {
                UpdateConnectionPath(conn);
                if (IsWindyStyle(conn))
                {
                    hasWindy = true;
                }
            }
            
            // Đảm bảo wind rendering loop chạy nếu có connection Windy
            if (hasWindy)
            {
                EnsureWindRenderingLoop();
            }
        }

        public void ApplyWindImpulse(WorkflowConnection connection, double magnitude)
        {
            // Bỏ hiệu ứng hover - không còn dùng nữa
            // Giữ lại method để không break code khi di chuyển node
        }

        public ShapesPath CreateConnectionLine(
            Point start,
            Point end,
            Color color,
            bool isDashed,
            PortPosition? startPortPosition,
            PortPosition? endPortPosition)
        {
            var path = new ShapesPath
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = LineStyle == ConnectionLineStyle.Orthogonal ? 2.0 : 2.5,
                Fill = null,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            PathGeometry geometry = LineStyle switch
            {
                ConnectionLineStyle.Orthogonal        => _orthogonal.Generate(start, end, startPortPosition, endPortPosition),
                ConnectionLineStyle.Straight          => _straight.Generate(start, end, startPortPosition, endPortPosition),
                ConnectionLineStyle.SmoothOrthogonal  => _orthogonal.Generate(start, end, startPortPosition, endPortPosition),
                ConnectionLineStyle.Arc               => GenerateArcGeometry(start, end, startPortPosition, endPortPosition),
                ConnectionLineStyle.RadialFanout      => GenerateRadialFanoutGeometry(start, end, startPortPosition, endPortPosition),
                ConnectionLineStyle.Windy             => _bezier.Generate(start, end, startPortPosition, endPortPosition),
                ConnectionLineStyle.OrthogonalV2      => _orthogonalV2.Generate(start, end, startPortPosition, endPortPosition),
                _                                     => _bezier.Generate(start, end, startPortPosition, endPortPosition),
            };

            path.Data = geometry;

            ShapesPath? arrowHead = null;
            if (!isDashed)
            {
                arrowHead = CreateArrowHeadElement(geometry, color, endPortPosition);
            }

            path.Tag = new ConnectionTag { Type = "Connection", ArrowHead = arrowHead };

            // Áp dụng GPU optimization cho arrow head nếu có
            if (arrowHead != null)
            {
                GpuOptimizationHelper.ApplyToPath(arrowHead, allowCache: true);
            }

            if (isDashed)
            {
                path.StrokeDashArray = new DoubleCollection { 5, 3 };

                // Khi đang ở chế độ animation, temp dashed line (kéo nối port)
                // cũng phải có animation để đồng bộ trải nghiệm với các connection khác.
                if (IsAnimationEnabled)
                {
                    double dashLength = 5;
                    double gapLength = 3;
                    double patternLength = dashLength + gapLength;

                    var dashAnimation = new DoubleAnimation
                    {
                        From = patternLength,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = null
                    };

                    Storyboard.SetTarget(dashAnimation, path);
                    Storyboard.SetTargetProperty(dashAnimation, new PropertyPath(Shape.StrokeDashOffsetProperty));

                    var storyboard = new Storyboard();
                    storyboard.Children.Add(dashAnimation);
                    storyboard.Begin(path, true);
                }
            }
            else if (IsAnimationEnabled)
            {
                double dashLength = 8;
                double gapLength = 6;
                double patternLength = dashLength + gapLength;

                path.StrokeDashArray = new DoubleCollection { dashLength, gapLength };
                path.StrokeDashOffset = 0;

                var dashAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = -patternLength,
                    Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = null
                };

                Storyboard.SetTarget(dashAnimation, path);
                Storyboard.SetTargetProperty(dashAnimation, new PropertyPath(Shape.StrokeDashOffsetProperty));

                var storyboard = new Storyboard();
                storyboard.Children.Add(dashAnimation);
                storyboard.Begin(path, true);
            }

            return path;
        }

        public void RemoveConnectionVisuals(WorkflowConnection connection)
        {
            RemoveEnergyOverlay(connection);
            RemoveEnergyBall(connection);
            RemoveEnergyText(connection);

            // ✅ QUAN TRỌNG: Lấy arrow head từ Tag TRƯỚC KHI xóa LineUI
            ShapesPath? arrowHeadToRemove = null;
            if (connection.LineUI?.Tag is ConnectionTag tag && tag.ArrowHead != null)
            {
                arrowHeadToRemove = tag.ArrowHead;
            }

            // ✅ Sử dụng SafeRemoveFromCanvas để clear cache trước khi remove
            if (arrowHeadToRemove != null)
            {
                GpuOptimizationHelper.SafeRemoveFromCanvas(arrowHeadToRemove, _canvas);
            }

            if (connection.LineUI != null)
            {
                GpuOptimizationHelper.SafeRemoveFromCanvas(connection.LineUI, _canvas);
            }

            if (connection.HitArea != null)
            {
                GpuOptimizationHelper.SafeRemoveFromCanvas(connection.HitArea, _canvas);
            }

            if (connection.DeleteButton != null && _canvas.Children.Contains(connection.DeleteButton))
            {
                _canvas.Children.Remove(connection.DeleteButton);
            }
            
            // Clear references để tránh memory leak
            connection.LineUI = null;
            connection.HitArea = null;
            connection.DeleteButton = null;
        }

        public void ClearAllConnectionVisuals()
        {
            // Xóa tất cả Path (Line, Arrow) mà có Tag liên quan đến connection
            var paths = _canvas.Children.OfType<ShapesPath>()
                .Where(p => p.Tag is WorkflowConnection || p.Tag is ConnectionTag || p.Tag?.ToString() == "ConnectionArrow")
                .ToList();
            foreach (var path in paths)
            {
                GpuOptimizationHelper.SafeRemoveFromCanvas(path, _canvas);
            }

            var ellipses = _canvas.Children.OfType<System.Windows.Shapes.Ellipse>()
                .Where(e => e.Tag is WorkflowConnection)
                .ToList();
            foreach (var el in ellipses)
            {
                GpuOptimizationHelper.SafeRemoveFromCanvas(el, _canvas);
            }

            var energyTexts = _canvas.Children.OfType<FrameworkElement>()
                .Where(fe => fe.Tag is WorkflowConnection && fe is Border)
                .ToList();
            foreach (var t in energyTexts)
            {
                GpuOptimizationHelper.SafeRemoveFromCanvas(t, _canvas);
            }

            // Xóa các delete buttons
            var buttons = _canvas.Children.OfType<Button>()
                .Where(b => b.Tag is WorkflowConnection)
                .ToList();
            foreach (var btn in buttons)
            {
                _canvas.Children.Remove(btn);
            }

            // Clear cached UI references trên model để tránh trỏ vào visual đã bị remove
            var vm = Host.ViewModel;
            if (vm != null)
            {
                foreach (var c in vm.Connections)
                {
                    c.LineUI = null;
                    c.EnergyUI = null;
                    c.EnergyBallUI = null;
                    c.EnergyTextUI = null;
                    c.HitArea = null;
                    c.DeleteButton = null;
                }
            }
        }

        private static Point ShortenEndPointForArrow(Point point, PortPosition direction, double offset)
        {
            return direction switch
            {
                PortPosition.Right => new Point(point.X + offset, point.Y),
                PortPosition.Left => new Point(point.X - offset, point.Y),
                PortPosition.Bottom => new Point(point.X, point.Y + offset),
                PortPosition.Top => new Point(point.X, point.Y - offset),
                _ => point
            };
        }

        private Color GetConnectionColor(WorkflowConnection connection)
        {
            if (ColorMode == ConnectionColorMode.CustomColor)
            {
                return CustomColor;
            }

            // Ưu tiên 1: ColorKey của Port 
            // if (connection.FromPort != null && !string.IsNullOrEmpty(connection.FromPort.ColorKey))
            // {
            //    var brush = System.Windows.Application.Current.TryFindResource(connection.FromPort.ColorKey) as SolidColorBrush
            //                ?? System.Windows.Application.Current.TryFindResource(connection.FromPort.ColorKey + "Brush") as SolidColorBrush;
            //    if (brush != null) return brush.Color;
            //    
            //    // Fallback: Nếu không tìm thấy resource match ColorKey (VD: "FuchsiaBright"), có thể dùng hash hoặc màu mặc định
            //    // Hiện tại sẽ fallback xuống màu của Node
            // }

            // Sử dụng màu của node nguồn
            return connection.FromNode != null
                ? GetColorFromBrush(connection.FromNode.NodeBrush)
                : Colors.LimeGreen;
        }

        private static Color GetColorFromBrush(Brush? brush)
        {
            if (brush == null) return Colors.LimeGreen;

            if (brush is SolidColorBrush solidBrush) return solidBrush.Color;

            if (brush is LinearGradientBrush linearGradient && linearGradient.GradientStops.Count > 0)
            {
                return linearGradient.GradientStops[0].Color;
            }

            if (brush is RadialGradientBrush radialGradient && radialGradient.GradientStops.Count > 0)
            {
                return radialGradient.GradientStops[0].Color;
            }

            if (brush is DrawingBrush drawingBrush && drawingBrush.Drawing is GeometryDrawing geometryDrawing &&
                geometryDrawing.Brush is SolidColorBrush drawingSolidBrush)
            {
                return drawingSolidBrush.Color;
            }

            try
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(brush, null, new Rect(0, 0, 1, 1));
                }
                var rtb = new RenderTargetBitmap(1, 1, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);
                var pixels = new byte[4];
                rtb.CopyPixels(pixels, 4, 0);
                return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
            }
            catch
            {
                return Colors.LimeGreen;
            }
        }

        private static Point CalculateBezierPoint(Point p0, Point p1, Point p2, Point p3, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            return new Point(
                uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X,
                uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y
            );
        }

        /// <summary>
        /// Kiểm tra style line thực tế (bao gồm override từ ReuseRoutes) có phải Windy không.
        /// </summary>
        private bool IsWindyStyle(WorkflowConnection connection)
        {
            return GetLineStyleForConnection(connection) == ConnectionLineStyle.Windy;
        }

        /// <summary>
        /// Đảm bảo đã đăng ký CompositionTarget.Rendering cho animation Windy.
        /// Chỉ chạy khi có ít nhất một connection Windy còn active.
        /// </summary>
        private void EnsureWindRenderingLoop()
        {
            if (_isWindRenderingActive) return;

            if (_windRenderingHandler == null)
            {
                _windRenderingHandler = (s, e) => OnWindRenderingFrame();
            }

            CompositionTarget.Rendering += _windRenderingHandler;
            _isWindRenderingActive = true;
            _lastWindFrameTime = DateTime.Now;
        }

        private void StopWindRenderingLoop()
        {
            if (_windRenderingHandler != null && _isWindRenderingActive)
            {
                CompositionTarget.Rendering -= _windRenderingHandler;
                _isWindRenderingActive = false;
            }
        }

        /// <summary>
        /// Frame update cho toàn bộ connections kiểu Windy.
        /// </summary>
        private void OnWindRenderingFrame()
        {
            var vm = Host.ViewModel;
            if (vm == null || vm.Connections == null || vm.Connections.Count == 0)
            {
                StopWindRenderingLoop();
                return;
            }

            var now = DateTime.Now;
            double dt = (_lastWindFrameTime == DateTime.MinValue)
                ? 0.016
                : Math.Max(0.001, (now - _lastWindFrameTime).TotalSeconds);
            _lastWindFrameTime = now;

            bool anyWindy = false;

            foreach (var conn in vm.Connections)
            {
                if (!IsWindyStyle(conn)) continue;
                if (conn.LineUI?.Data is not PathGeometry geo) continue;

                anyWindy = true;
                // Luôn update để giữ continuous wave animation chạy
                UpdateSingleWindConnection(conn, geo, dt);
            }

            // Chỉ dừng loop nếu không còn connection Windy nào
            // Continuous wave luôn chạy nên không cần check anyActive
            if (!anyWindy)
            {
                StopWindRenderingLoop();
            }
        }

        /// <summary>
        /// Cập nhật trạng thái "gió" cho một connection Windy.
        /// Trả về true nếu line vẫn còn đang chuyển động.
        /// </summary>
        private bool UpdateSingleWindConnection(WorkflowConnection connection, PathGeometry geometry, double dt)
        {
            const double minEpsilon = 0.01;

            // Lấy figure và BezierSegment đầu tiên (đường cong chính)
            var figure = geometry.Figures.FirstOrDefault();
            if (figure == null || figure.Segments.FirstOrDefault() is not BezierSegment bezier)
            {
                return false;
            }

            // Khởi tạo base control points + normal nếu chưa có (hoặc sau khi path được update lại)
            if (!connection.WindBaseControlPoint1.HasValue || !connection.WindBaseControlPoint2.HasValue ||
                connection.WindNormal.LengthSquared < 0.0001)
            {
                connection.WindBaseControlPoint1 = bezier.Point1;
                connection.WindBaseControlPoint2 = bezier.Point2;

                // Normal của đoạn p0->p3
                var p0 = figure.StartPoint;
                var p3 = bezier.Point3;
                var v = p3 - p0;
                if (v.LengthSquared < 0.0001)
                {
                    v = new Vector(0, -1);
                }
                v.Normalize();
                // Pháp tuyến 90° (xoay trái) - hướng vuông góc với đường thẳng
                var n = new Vector(-v.Y, v.X);
                connection.WindNormal = n;
                
                // Khởi tạo các giá trị mới
                connection.WindTime = 0.0;
                // Bắt đầu wave ngay lập tức để dễ thấy hiệu ứng
                connection.NextWaveTime = 0.2; // Bắt đầu wave sau 0.2 giây
                connection.WaveProgress = 0.0;
                connection.IsWaveActive = false;
                connection.WaveAmplitude = 30.0; // Biên độ wave (tăng để dễ thấy hơn)
            }

            // Nếu trước đó chưa từng update, set LastWindUpdate
            if (connection.LastWindUpdate == DateTime.MinValue)
            {
                connection.LastWindUpdate = DateTime.Now;
            }

            // ========== 1. WAVE ANIMATION TỪ TRÁI SANG PHẢI (Mỗi 2-5 giây) ==========
            // Đảm bảo NextWaveTime được khởi tạo nếu chưa có
            if (connection.NextWaveTime <= 0.0)
            {
                connection.NextWaveTime = GetRandomWaveInterval();
            }
            
            connection.WindTime += dt;
            
            // Kiểm tra xem đã đến lúc tạo wave mới chưa
            if (!connection.IsWaveActive && connection.WindTime >= connection.NextWaveTime)
            {
                // Bắt đầu wave mới
                connection.IsWaveActive = true;
                connection.WaveProgress = 0.0;
                connection.WindTime = 0.0; // Reset timer để đếm lại
                connection.NextWaveTime = GetRandomWaveInterval(); // Set thời gian cho lần tiếp theo
            }
            
            // Nếu wave đang active, cập nhật progress
            double waveOffset = 0.0;
            if (connection.IsWaveActive)
            {
                // Wave di chuyển từ 0.0 (đầu trái) đến 1.0 (đầu phải)
                const double waveDuration = 1.5; // Thời gian wave chạy từ đầu đến cuối (giây) - tăng để dễ thấy hơn
                connection.WaveProgress += dt / waveDuration;
                
                if (connection.WaveProgress >= 1.0)
                {
                    // Wave đã hoàn thành, reset và chờ lần tiếp theo
                    connection.IsWaveActive = false;
                    connection.WaveProgress = 0.0;
                    connection.WindTime = 0.0; // Reset để đếm lại từ đầu
                }
                else
                {
                    // Tính toán offset dựa trên vị trí wave dọc theo line
                    // Sử dụng sin wave để tạo hiệu ứng sóng lượn
                    // Wave có biên độ lớn nhất ở giữa và giảm dần ở 2 đầu
                    double wavePhase = connection.WaveProgress * Math.PI * 2.0 * 3.0; // Tăng tần số để có nhiều sóng hơn
                    
                    // Envelope function: tăng dần từ 0, đạt max ở giữa, giảm dần về 0
                    // Sử dụng sin để tạo envelope mượt mà
                    double envelope = Math.Sin(connection.WaveProgress * Math.PI);
                    
                    // Wave offset: sin wave với envelope - tăng biên độ để dễ thấy
                    // Sử dụng biên độ đầy đủ để wave rõ ràng hơn
                    waveOffset = Math.Sin(wavePhase) * envelope * connection.WaveAmplitude;
                    
                    // Thêm một wave phụ với tần số khác để tạo hiệu ứng tự nhiên hơn
                    double waveOffset2 = Math.Sin(wavePhase * 1.5 + Math.PI * 0.2) * envelope * (connection.WaveAmplitude * 0.6);
                    waveOffset = (waveOffset + waveOffset2) * 0.9; // Kết hợp với tỷ lệ cao hơn
                }
            }
            
            // Đảm bảo wave amplitude luôn ở mức đủ lớn để dễ thấy
            if (connection.WaveAmplitude < 20.0)
            {
                connection.WaveAmplitude = 30.0; // Tăng biên độ để dễ thấy hơn
            }

            // ========== 2. BỎ ELASTIC EFFECT ==========
            // Bỏ hoàn toàn elastic effect để tránh animation khi hover
            double elasticOffset = 0.0;

            // ========== 3. CHỈ DÙNG WAVE OFFSET ==========
            // Chỉ dùng wave offset, không có elastic effect
            double totalOffset = waveOffset;

            // Luôn active để wave animation chạy liên tục
            connection.IsWindActive = true;
            connection.LastWindUpdate = DateTime.Now;

            // ========== 4. ÁP DỤNG VÀO CONTROL POINTS ==========
            var baseP1 = connection.WindBaseControlPoint1!.Value;
            var baseP2 = connection.WindBaseControlPoint2!.Value;
            var nrm = connection.WindNormal;

            // Phân bố offset cho 2 control points dựa trên vị trí wave
            // Wave di chuyển từ đầu trái (control point 1) đến đầu phải (control point 2)
            double k1, k2;
            
            if (connection.IsWaveActive)
            {
                // Khi wave đang chạy, phân bố offset dựa trên vị trí wave
                // Wave di chuyển từ control point 1 (đầu trái, progress = 0.0) 
                // đến control point 2 (đầu phải, progress = 1.0)
                double progress = connection.WaveProgress;
                
                // Control point 1 ở đầu trái (progress = 0.0)
                // Control point 2 ở đầu phải (progress = 1.0)
                // Wave ảnh hưởng mạnh nhất ở vị trí hiện tại của nó
                
                // Tính ảnh hưởng của wave lên từng control point
                // Control point 1 ở đầu trái (progress = 0.0)
                // Control point 2 ở đầu phải (progress = 1.0)
                // Wave ảnh hưởng mạnh nhất ở vị trí hiện tại của nó
                
                // Sử dụng Gaussian-like distribution để tạo hiệu ứng mượt mà
                double sigma = 0.35; // Độ rộng ảnh hưởng
                
                // Control point 1: ảnh hưởng mạnh khi wave ở gần đầu trái
                double dist1 = progress; // Khoảng cách từ wave đến control point 1
                k1 = Math.Exp(-(dist1 * dist1) / (2.0 * sigma * sigma));
                
                // Control point 2: ảnh hưởng mạnh khi wave ở gần đầu phải
                double dist2 = 1.0 - progress; // Khoảng cách từ wave đến control point 2
                k2 = Math.Exp(-(dist2 * dist2) / (2.0 * sigma * sigma));
                
                // Normalize để đảm bảo tổng ảnh hưởng hợp lý
                double total = k1 + k2;
                if (total > 0.001)
                {
                    k1 = k1 / total;
                    k2 = k2 / total;
                }
                else
                {
                    k1 = 0.5;
                    k2 = 0.5;
                }
            }
            else
            {
                // Khi không có wave, chỉ áp dụng elastic effect đều cho cả 2 control points
                k1 = 0.5;
                k2 = 0.5;
            }

            // Áp dụng offset với phân bố dựa trên vị trí wave
            bezier.Point1 = baseP1 + nrm * (totalOffset * k1);
            bezier.Point2 = baseP2 + nrm * (totalOffset * k2);

            // HitArea và EnergyUI dùng chung geometry hoặc đã bind cùng PathGeometry nên không cần update riêng
            return true;
        }

        /// <summary>
        /// Tạo geometry kiểu cung tròn (Arc) giữa 2 điểm.
        /// Dùng QuadraticBezier với control point lệch lên/xuống (hoặc trái/phải)
        /// TÙY THEO vị trí tương đối của 2 node:
        /// - Nếu đoạn chủ yếu ngang: cong về phía node nào đang "cao/thấp" hơn (dy).
        /// - Nếu đoạn chủ yếu dọc: cong về phía node nào đang "trái/phải" hơn (dx).
        /// </summary>
        private static PathGeometry GenerateArcGeometry(
            Point start,
            Point end,
            PortPosition? startDir,
            PortPosition? endDir)
        {
            var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) len = 0.001;

            var mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);

            // Độ cong tối đa ~80px, tỷ lệ theo khoảng cách
            double offset = Math.Min(80, len * 0.4);

            // Chọn hướng cong:
            // - Nếu gần như ngang (|dx| >= |dy|) → cong lên/xuống theo dấu của dy
            //   + dy > 0 (node đích thấp hơn)  → cong xuống
            //   + dy < 0 (node đích cao hơn)  → cong lên
            // - Nếu gần như dọc (|dx| < |dy|) → cong trái/phải theo dấu của dx
            //   + dx > 0 (node đích bên phải) → cong sang phải
            //   + dx < 0 (node đích bên trái) → cong sang trái
            double dirX = 0;
            double dirY = 0;

            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                // Ưu tiên cong theo trục Y
                double vy = dy;
                if (Math.Abs(vy) < 1) vy = -1; // nếu gần như thẳng ngang, mặc định cong lên một chút
                dirY = Math.Sign(vy);
            }
            else
            {
                // Ưu tiên cong theo trục X
                double vx = dx;
                if (Math.Abs(vx) < 1) vx = 1; // nếu gần như thẳng dọc, mặc định cong sang phải
                dirX = Math.Sign(vx);
            }

            var control = new Point(
                mid.X + dirX * offset,
                mid.Y + dirY * offset);

            figure.Segments.Add(new QuadraticBezierSegment(control, end, true));

            var geo = new PathGeometry();
            geo.Figures.Add(figure);
            return geo;
        }

        /// <summary>
        /// Tạo geometry kiểu Radial / Fan-out: đường cong chữ S mềm, ưu tiên tỏa quạt từ node nguồn.
        /// </summary>
        private static PathGeometry GenerateRadialFanoutGeometry(
            Point start,
            Point end,
            PortPosition? startDir,
            PortPosition? endDir)
        {
            var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };

            var p0 = start;
            var p3 = end;

            double dx = p3.X - p0.X;
            double dy = p3.Y - p0.Y;

            // Khoảng cách tương đối để đẩy control points ra khỏi node nguồn/đích
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double baseOffset = Math.Max(40, dist * 0.25);

            // Hướng chính: nếu chủ yếu trái-phải → fan-out theo trục X, nếu trên-dưới → fan-out theo trục Y
            bool horizontal = Math.Abs(dx) >= Math.Abs(dy);

            Point c1, c2;
            if (horizontal)
            {
                // Fan-out theo trục X, thêm độ lệch Y nhẹ để tạo chữ S
                double sgn = dy >= 0 ? 1 : -1;
                c1 = new Point(p0.X + baseOffset, p0.Y + sgn * baseOffset * 0.3);
                c2 = new Point(p3.X - baseOffset, p3.Y - sgn * baseOffset * 0.3);
            }
            else
            {
                // Fan-out theo trục Y
                double sgn = dx >= 0 ? 1 : -1;
                c1 = new Point(p0.X + sgn * baseOffset * 0.3, p0.Y + baseOffset);
                c2 = new Point(p3.X - sgn * baseOffset * 0.3, p3.Y - baseOffset);
            }

            figure.Segments.Add(new BezierSegment(c1, c2, p3, true));

            var geo = new PathGeometry();
            geo.Figures.Add(figure);
            return geo;
        }

        private static ShapesPath? CreateArrowHeadElement(PathGeometry lineGeometry, Color color, PortPosition? endPortPosition = null)
        {
            if (lineGeometry.Figures.Count == 0) return null;

            var lastFigure = lineGeometry.Figures.Last();
            if (lastFigure.Segments.Count == 0) return null;

            Point arrowEnd = GetEndPoint(lastFigure);
            if (double.IsNaN(arrowEnd.X) || double.IsNaN(arrowEnd.Y) ||
                double.IsInfinity(arrowEnd.X) || double.IsInfinity(arrowEnd.Y))
            {
                return null;
            }

            double angle;
            if (endPortPosition.HasValue)
            {
                angle = endPortPosition.Value switch
                {
                    PortPosition.Left => 0,
                    PortPosition.Right => Math.PI,
                    PortPosition.Top => Math.PI / 2,
                    PortPosition.Bottom => -Math.PI / 2,
                    _ => 0
                };
            }
            else
            {
                Point beforeEnd = GetBeforeEndPoint(lastFigure);
                angle = (double.IsNaN(beforeEnd.X) || double.IsNaN(beforeEnd.Y) ||
                         double.IsInfinity(beforeEnd.X) || double.IsInfinity(beforeEnd.Y))
                    ? 0
                    : Math.Atan2(arrowEnd.Y - beforeEnd.Y, arrowEnd.X - beforeEnd.X);
            }

            double arrowLength = 16;
            double arrowWidth = 15;

            Point tip = arrowEnd;
            Point leftPoint = new Point(
                tip.X - arrowLength * Math.Cos(angle) + (arrowWidth / 2) * Math.Cos(angle + Math.PI / 2),
                tip.Y - arrowLength * Math.Sin(angle) + (arrowWidth / 2) * Math.Sin(angle + Math.PI / 2)
            );
            Point rightPoint = new Point(
                tip.X - arrowLength * Math.Cos(angle) + (arrowWidth / 2) * Math.Cos(angle - Math.PI / 2),
                tip.Y - arrowLength * Math.Sin(angle) + (arrowWidth / 2) * Math.Sin(angle - Math.PI / 2)
            );

            var arrowGeometry = new PathGeometry();
            var arrowFigure = new PathFigure
            {
                StartPoint = tip,
                IsClosed = true,
                IsFilled = true
            };
            arrowFigure.Segments.Add(new LineSegment(leftPoint, true));
            arrowFigure.Segments.Add(new LineSegment(rightPoint, true));
            arrowGeometry.Figures.Add(arrowFigure);

            var arrowPath = new ShapesPath
            {
                Data = arrowGeometry,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round,
                Tag = "ConnectionArrow",
                Opacity = 1.0,
                IsHitTestVisible = false,
                Effect = null
            };

            // Áp dụng GPU optimization cho arrow head
            GpuOptimizationHelper.ApplyToPath(arrowPath, allowCache: true);

            return arrowPath;
        }

        private static Point GetEndPoint(PathFigure figure)
        {
            var lastSegment = figure.Segments.Last();
            return lastSegment switch
            {
                LineSegment ls => ls.Point,
                BezierSegment bs => bs.Point3,
                ArcSegment arc => arc.Point,
                _ => figure.StartPoint
            };
        }

        private static Point GetBeforeEndPoint(PathFigure figure)
        {
            var lastSegment = figure.Segments.Last();
            if (lastSegment is BezierSegment bezierSegment)
            {
                return bezierSegment.Point2;
            }

            if (figure.Segments.Count > 1)
            {
                var beforeLast = figure.Segments[figure.Segments.Count - 2];
                return beforeLast switch
                {
                    LineSegment ls => ls.Point,
                    BezierSegment bs => bs.Point3,
                    ArcSegment arc => arc.Point,
                    _ => figure.StartPoint
                };
            }

            return figure.StartPoint;
        }

        private void CreateDeleteButton(
            WorkflowConnection connection,
            Action<WorkflowConnection> requestDeleteConnection,
            Action<WorkflowConnection?> setSelectedConnection)
        {
            if (connection.DeleteButton != null && _canvas.Children.Contains(connection.DeleteButton))
            {
                UpdateDeleteButtonPosition(connection);
                return;
            }

            var button = new Button
            {
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = connection,
                Opacity = 0.9,
                IsHitTestVisible = true,
                Background = new SolidColorBrush(Color.FromArgb(220, 239, 68, 68)),
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                Visibility = connection.IsDeleteVisible ? Visibility.Visible : Visibility.Collapsed
            };

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

            var pathFactory = new FrameworkElementFactory(typeof(ShapesPath));
            pathFactory.SetValue(ShapesPath.DataProperty, System.Windows.Media.Geometry.Parse("M 6,6 L 10,10 M 10,6 L 6,10"));
            pathFactory.SetValue(ShapesPath.StrokeProperty, new SolidColorBrush(Colors.White));
            pathFactory.SetValue(ShapesPath.StrokeThicknessProperty, 2.5);
            pathFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            pathFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            pathFactory.SetValue(Shape.StretchProperty, Stretch.Uniform);
            pathFactory.SetValue(FrameworkElement.WidthProperty, 12.0);
            pathFactory.SetValue(FrameworkElement.HeightProperty, 12.0);

            borderFactory.AppendChild(pathFactory);
            template.VisualTree = borderFactory;
            button.Template = template;

            button.Effect = GpuOptimizationHelper.CreateDropShadowEffect();

            button.MouseEnter += (s, e) =>
            {
                button.Opacity = 1.0;
                button.Background = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38));
                button.Width = 32;
                button.Height = 32;
                
                Point start, end;
                if (connection.FromPort != null && connection.ToPort != null)
                {
                    start = connection.FromPort.PositionPoint;
                    end = connection.ToPort.PositionPoint;
                }
                else
                {
                    start = connection.IsFromInput ? connection.FromNode.InputPortPosition : connection.FromNode.OutputPortPosition;
                    end = connection.IsFromInput ? connection.ToNode.OutputPortPosition : connection.ToNode.InputPortPosition;
                }

                int baseZ = GetZBaseForConnection(connection, start, end);
                bool aboveBody = baseZ == ConnectionBaseAboveLoopBody;
                int z = baseZ + 4;
                if (aboveBody) z = Math.Max(z, DeleteButtonZIndexMin);
                Panel.SetZIndex(button, z);

                var scaleTransform = new ScaleTransform(1.0, 1.0);
                button.RenderTransform = scaleTransform;
                button.RenderTransformOrigin = new Point(0.5, 0.5);

                var animation = new DoubleAnimation(1.15, TimeSpan.FromMilliseconds(150));
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            };

            button.MouseLeave += (s, e) =>
            {
                button.Opacity = 0.9;
                button.Background = new SolidColorBrush(Color.FromArgb(220, 239, 68, 68));
                button.Width = 28;
                button.Height = 28;
                
                Point start, end;
                if (connection.FromPort != null && connection.ToPort != null)
                {
                    start = connection.FromPort.PositionPoint;
                    end = connection.ToPort.PositionPoint;
                }
                else
                {
                    start = connection.IsFromInput ? connection.FromNode.InputPortPosition : connection.FromNode.OutputPortPosition;
                    end = connection.IsFromInput ? connection.ToNode.OutputPortPosition : connection.ToNode.InputPortPosition;
                }

                int baseZ = GetZBaseForConnection(connection, start, end);
                bool aboveBody = baseZ == ConnectionBaseAboveLoopBody;
                int z = baseZ + 3;
                if (aboveBody) z = Math.Max(z, DeleteButtonZIndexMin);
                Panel.SetZIndex(button, z);

                if (button.RenderTransform is ScaleTransform transform)
                {
                    var animation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150));
                    transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                    transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
                }

                // Nếu vừa rời khỏi delete button thì có thể clear selection (giống logic cũ)
                if (connection.HitArea != null && !connection.HitArea.IsMouseOver)
                {
                    setSelectedConnection(null);
                }
            };

            button.Click += (s, e) =>
            {
                requestDeleteConnection(connection);
            };

            // Default delete button z-index
            Point startZ, endZ;
            if (connection.FromPort != null && connection.ToPort != null)
            {
                startZ = connection.FromPort.PositionPoint;
                endZ = connection.ToPort.PositionPoint;
            }
            else
            {
                startZ = connection.IsFromInput ? connection.FromNode.InputPortPosition : connection.FromNode.OutputPortPosition;
                endZ = connection.IsFromInput ? connection.ToNode.OutputPortPosition : connection.ToNode.InputPortPosition;
            }

            int baseZDefault = GetZBaseForConnection(connection, startZ, endZ);
            bool aboveBodyDefault = baseZDefault == ConnectionBaseAboveLoopBody;
            int zDefault = baseZDefault + 3;
            if (aboveBodyDefault) zDefault = Math.Max(zDefault, DeleteButtonZIndexMin);
            Panel.SetZIndex(button, zDefault);
            connection.DeleteButton = button;
            _canvas.Children.Add(button);
            UpdateDeleteButtonPosition(connection);
        }

        private void ApplyConnectionZIndex(WorkflowConnection connection, Point start, Point end)
        {
            int baseZ = GetZBaseForConnection(connection, start, end);
            if (connection.IsExecutionActive)
            {
                // Nâng lên để nổi bật trong khi chạy (vẫn dưới node)
                baseZ += 80;
            }

            if (connection.HitArea != null) Panel.SetZIndex(connection.HitArea, baseZ);
            if (connection.LineUI != null) Panel.SetZIndex(connection.LineUI, baseZ + 1);
            if (connection.EnergyUI != null) Panel.SetZIndex(connection.EnergyUI, baseZ + 1);
            if (connection.EnergyTextUI != null) Panel.SetZIndex(connection.EnergyTextUI, baseZ + 2);

            if (connection.LineUI?.Tag is ConnectionTag tag && tag.ArrowHead != null)
            {
                Panel.SetZIndex(tag.ArrowHead, baseZ + 2);
            }

            if (connection.DeleteButton != null)
            {
                // Default z (hover sẽ +4)
                int z = baseZ + 3;
                if (baseZ == ConnectionBaseAboveLoopBody)
                    z = Math.Max(z, DeleteButtonZIndexMin);
                Panel.SetZIndex(connection.DeleteButton, z);
            }
        }

        private int GetZBaseForConnection(WorkflowConnection connection)
        {
            // Fallback dùng midpoint (nếu chưa có port positions)
            var mid = GetConnectionMidpoint(connection);
            return IsPointInsideAnyLoopBody(mid) ? ConnectionBaseAboveLoopBody : ConnectionBaseUnderNodes;
        }

        private int GetZBaseForConnection(WorkflowConnection connection, Point start, Point end)
        {
            var mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            // Be conservative: if either end is inside (or the midpoint is inside),
            // treat it as above body chrome to keep delete buttons clickable.
            bool aboveBody = IsPointInsideAnyLoopBody(start) || IsPointInsideAnyLoopBody(end) || IsPointInsideAnyLoopBody(mid);
            return aboveBody ? ConnectionBaseAboveLoopBody : ConnectionBaseUnderNodes;
        }

        private bool IsPointInsideAnyLoopBody(Point p)
        {
            var vm = Host.ViewModel;
            if (vm == null) return false;

            // Rect.Contains trong WPF không tính cả hai phía biên theo cách trực quan,
            // nên với các port nằm đúng mép trái/phải (hoặc góc) có thể trả false.
            // Inflate nhẹ để đảm bảo delete button/line luôn nằm "trên body" theo hit-test.
            const double padding = 2.0;

            foreach (var loop in vm.Nodes.OfType<LoopNode>())
            {
                var body = loop.LoopBodyNode;
                var rect = new Rect(body.X, body.Y, body.Width, body.Height);
                rect.Inflate(padding, padding);
                if (rect.Contains(p))
                {
                    return true;
                }
            }

            // AsyncTaskBody: treat the body rectangle as a "loop body" for Z-index purposes,
            // so delete buttons/lines can appear above the body chrome and remain clickable.
            foreach (var asyncTask in vm.Nodes.OfType<AsyncTaskNode>())
            {
                if (asyncTask.UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch)
                    continue;
                var body = asyncTask.AsyncTaskBodyNode;
                if (body == null) continue;

                var rect = new Rect(body.X, body.Y, body.Width, body.Height);
                rect.Inflate(padding, padding);
                if (rect.Contains(p))
                    return true;
            }

            return false;
        }

        private void UpdateDeleteButtonPosition(WorkflowConnection connection)
        {
            if (connection.DeleteButton == null || connection.LineUI == null) return;

            // Đồng bộ lại visibility nếu cần (quan trọng khi re-render)
            connection.DeleteButton.Visibility = connection.IsDeleteVisible ? Visibility.Visible : Visibility.Collapsed;

            Point midpoint = GetConnectionMidpoint(connection);
            Canvas.SetLeft(connection.DeleteButton, midpoint.X - 14); // 28/2 = 14
            Canvas.SetTop(connection.DeleteButton, midpoint.Y - 14);

            // Important: re-apply ZIndex because port positions can move after initial render.
            // Use start/end/mid conservative check so delete buttons on body segments remain clickable.
            Point start, end;
            if (connection.FromPort != null && connection.ToPort != null)
            {
                start = connection.FromPort.PositionPoint;
                end = connection.ToPort.PositionPoint;
            }
            else
            {
                start = connection.IsFromInput ? connection.FromNode.InputPortPosition : connection.FromNode.OutputPortPosition;
                end = connection.IsFromInput ? connection.ToNode.OutputPortPosition : connection.ToNode.InputPortPosition;
            }

            int baseZ = GetZBaseForConnection(connection, start, end);
            bool aboveBody = baseZ == ConnectionBaseAboveLoopBody;
            if (connection.IsExecutionActive) baseZ += 80;

            int z = baseZ + 3;
            if (aboveBody) z = Math.Max(z, DeleteButtonZIndexMin);
            Panel.SetZIndex(connection.DeleteButton, z);
        }

        private Point GetConnectionMidpoint(WorkflowConnection connection)
        {
            Point start, end;
            if (connection.FromPort != null && connection.ToPort != null)
            {
                start = connection.FromPort.PositionPoint;
                end = connection.ToPort.PositionPoint;
            }
            else
            {
                start = connection.IsFromInput
                    ? connection.FromNode.InputPortPosition
                    : connection.FromNode.OutputPortPosition;
                end = connection.IsFromInput
                    ? connection.ToNode.OutputPortPosition
                    : connection.ToNode.InputPortPosition;
            }

            // Luôn lấy trung điểm theo chiều dài thực của path để nút X luôn nằm giữa line.
            if (connection.LineUI?.Data is PathGeometry pathGeometry)
            {
                return GetPointAtFractionLength(pathGeometry, 0.5);
            }

            // Fallback: điểm giữa đơn giản (cho Straight line hoặc khi chưa có LineUI)
            return new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        }

        /// <summary>
        /// Lấy điểm trên đường PathGeometry tại một fraction của chiều dài (0.0 = start, 1.0 = end).
        /// </summary>
        private static Point GetPointAtFractionLength(PathGeometry geometry, double fraction)
        {
            if (geometry == null || double.IsNaN(fraction) || double.IsInfinity(fraction))
                return new Point(0, 0);

            fraction = Math.Clamp(fraction, 0.0, 1.0);

            // Flatten geometry để lấy các điểm trên đường line
            var flat = geometry.GetFlattenedPathGeometry(0.5, ToleranceType.Absolute);
            
            if (flat.Figures.Count == 0)
                return new Point(0, 0);

            // Tính tổng chiều dài và lưu các điểm
            var points = new List<Point>();
            double totalLength = 0;
            var segmentLengths = new List<double>();

            foreach (var fig in flat.Figures)
            {
                var last = fig.StartPoint;
                points.Add(last);

                foreach (var seg in fig.Segments)
                {
                    if (seg is LineSegment ls)
                    {
                        var p = ls.Point;
                        double segLen = (p - last).Length;
                        segmentLengths.Add(segLen);
                        totalLength += segLen;
                        points.Add(p);
                        last = p;
                    }
                    else if (seg is PolyLineSegment pls)
                    {
                        foreach (var p in pls.Points)
                        {
                            double segLen = (p - last).Length;
                            segmentLengths.Add(segLen);
                            totalLength += segLen;
                            points.Add(p);
                            last = p;
                        }
                    }
                }
            }

            if (totalLength <= 0 || points.Count < 2)
                return points.Count > 0 ? points[0] : new Point(0, 0);

            // Tìm điểm tại fraction của chiều dài
            double targetLength = totalLength * fraction;
            double accumulatedLength = 0;

            for (int i = 0; i < points.Count - 1; i++)
            {
                double segLen = (points[i + 1] - points[i]).Length;
                
                if (accumulatedLength + segLen >= targetLength)
                {
                    // Điểm nằm trong segment này
                    double t = (targetLength - accumulatedLength) / segLen;
                    return new Point(
                        points[i].X + (points[i + 1].X - points[i].X) * t,
                        points[i].Y + (points[i + 1].Y - points[i].Y) * t
                    );
                }

                accumulatedLength += segLen;
            }

            // Fallback: điểm cuối cùng
            return points[points.Count - 1];
        }

        /// <summary>
        /// Lấy kiểu line cho một connection cụ thể, ưu tiên cấu hình ReuseRoutes trên FromNode nếu có.
        /// Nếu không có override thì dùng ConnectionLineStyle từ host (workflow hiện tại).
        /// </summary>
        private ConnectionLineStyle GetLineStyleForConnection(WorkflowConnection connection)
        {
            try
            {
                var fromNode = connection.FromNode;
                var toNode = connection.ToNode;

                if (fromNode?.ReuseRoutes != null && fromNode.ReuseRoutes.Count > 0 && toNode != null)
                {
                    // Tìm route có OutgoingNodeId khớp với ToNode.Id và có LineStyleKey cụ thể
                    var route = fromNode.ReuseRoutes
                        .FirstOrDefault(r =>
                            !string.IsNullOrWhiteSpace(r.OutgoingNodeId) &&
                            !string.IsNullOrWhiteSpace(r.LineStyleKey) &&
                            string.Equals(r.OutgoingNodeId, toNode.Id, StringComparison.OrdinalIgnoreCase));

                    if (route != null && !string.IsNullOrWhiteSpace(route.LineStyleKey))
                    {
                        if (Enum.TryParse<ConnectionLineStyle>(route.LineStyleKey, out var style))
                        {
                            return style;
                        }
                    }
                }
            }
            catch
            {
                // Fallback về workflow default nếu có lỗi bất kỳ
            }

            return Host.ConnectionLineStyle;
        }
    }
}

