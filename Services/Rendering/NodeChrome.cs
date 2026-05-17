using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using FlowMy.Views.NodeControls;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FlowMy.Services.Rendering
{
    public static class NodeChrome
    {
        private const string RootTag = "NodeChromeRoot";
        private const string BtnDuplicateKey = "duplicate";
        private const string BtnEditTitleKey = "editTitle";
        private const string BtnDataToggleKey = "dataToggle";

        // Sizing when DataPanel expands (tune UI comfort here)
        private const double ExpandedMinWidth = 340;
        private const double ExpandedMinHeightExtra = 120;

        // Performance optimization: throttle expensive handlers during zoom
        private static bool _isZooming = false;
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _badgeUpdateTimers = new();
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _sizeChangedTimers = new();
        private const int BadgeUpdateThrottleMs = 100; // Throttle badge position updates
        private const int SizeChangedThrottleMs = 50; // Throttle port/connection updates during zoom

        /// <summary>
        /// Set zooming state to throttle expensive handlers (called by ZoomPanHandler)
        /// </summary>
        public static void SetZoomingState(bool isZooming)
        {
            _isZooming = isZooming;
        }

        /// <summary>
        /// Check if currently zooming (for performance optimization)
        /// </summary>
        public static bool IsZooming => _isZooming;

        public static void Apply(Border border, WorkflowNode node, IWorkflowEditorHost host)
        {
            // Không gắn chrome cho LoopBody container (node ảo)
            if (node is LoopBodyNode) return;

            // ── Liquid Glass: transform border appearance trước khi apply GPU/chrome ──
            // Skip cho các node dùng hình thoi (diamond) — border của chúng là transparent container
            if (LiquidGlassHelper.IsLiquidGlassMode(host))
            {
                var isDiamondNode = node is LoopNode
                    || (node.IsConditionalNode && node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                    || (node is AsyncTaskNode asyncTask && asyncTask.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch);

                if (isDiamondNode)
                {
                    // Diamond nodes: áp dụng glass lên Polygon fill bên trong, không đụng border
                    var baseColor = LiquidGlassHelper.GetColorFromBrush(node.NodeBrush);
                    ApplyGlassToDiamondPolygon(border, baseColor);
                }
                else
                {
                    var baseColor = LiquidGlassHelper.GetColorFromBrush(node.NodeBrush);
                    LiquidGlassHelper.ApplyToExistingBorder(border, baseColor);
                }
            }

            if (node is ImageProcessingNode or VideoProcessingNode)
                ImageProcessingNodeControl.ApplyEditorGpuChrome(node, border, host.CacheNodeEnabled);
            else
                GpuOptimizationHelper.ApplyToBorder(border, isDragging: false);

            // Chống apply nhiều lần (re-render/import)
            if (border.Child is Grid existingRoot && Equals(existingRoot.Tag, RootTag))
            {
                return;
            }

            var originalChild = border.Child as UIElement;

            // 1) Default/simple node: child là TextBlock (chỉ title)
            if (originalChild is TextBlock titleText)
            {
                // Ensure title sync + store ref for later updates
                titleText.Text = node.Title;
                node.TitleTextBlockUI = titleText;

                // Liquid Glass: đổi text style cho dễ đọc trên nền trong suốt
                if (LiquidGlassHelper.IsLiquidGlassMode(host))
                {
                    LiquidGlassHelper.ApplyGlassTextStyle(titleText);
                }

                var root = new Grid { Tag = RootTag };
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Row 0: title (giữ layout center như cũ)
                border.Child = null;
                Grid.SetRow(titleText, 0);
                root.Children.Add(titleText);

                // Execution status badge (runtime-only) - attach outside node to avoid inheriting node shadow
                EnsureExecutionStatusBadgeAttached(border, node, host);

                // ĐÃ ĐƠN GIẢN HÓA:
                // - Không còn header buttons (duplicate/editTitle/dataToggle) trên node.
                // - Không còn DataPanel embedded trong node (Inputs/Outputs trong node).
                // Cấu hình dữ liệu, inputs/outputs sẽ được thao tác qua dialog riêng.

                border.Child = root;

                AttachSizeChangedSync(border, node, host);

                // Snapshot collapsed size (không apply expanded size vì panel chưa được tạo)
                EnsureChromeSizeSnapshot(border, node);
                // Bỏ auto-expand: chỉ expand khi user mở toggle
                return;
            }

            // 2) Custom/complex node: overlay đơn giản, không còn header buttons / data panel
            var overlayRoot = new Grid
            {
                Tag = RootTag,
                Background = Brushes.Transparent // Đảm bảo transparent để tránh hình vuông mất bo góc
            };
            overlayRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            overlayRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            border.Child = null;

            if (originalChild != null)
            {
                Grid.SetRow(originalChild, 0);
                overlayRoot.Children.Add(originalChild);
            }

            // Execution status badge (runtime-only) - attach outside node to avoid inheriting node shadow
            EnsureExecutionStatusBadgeAttached(border, node, host);

            border.Child = overlayRoot;

            // Liquid Glass: update icon fills cho complex nodes (SVG icons bên trong)
            if (LiquidGlassHelper.IsLiquidGlassMode(host))
            {
                var iconBrush = LiquidGlassHelper.GetGlassIconBrush();
                UpdateSvgIconFills(border, iconBrush);
            }

            AttachSizeChangedSync(border, node, host);

            EnsureChromeSizeSnapshot(border, node);
            // Bỏ auto-expand: chỉ expand khi user mở toggle
        }

        /// <summary>
        /// Cập nhật Fill của tất cả SvgViewboxEx bên trong border (dùng cho Liquid Glass icon color).
        /// </summary>
        private static void UpdateSvgIconFills(Border border, Brush iconBrush)
        {
            if (border.Child == null) return;
            UpdateSvgIconFillsRecursive(border.Child, iconBrush);
        }

        /// <summary>
        /// Áp dụng Liquid Glass lên Polygon (diamond shape) bên trong border.
        /// Tìm Polygon đầu tiên và đổi Fill thành glass gradient + thêm glow effect lên Grid chứa nó.
        /// </summary>
        private static void ApplyGlassToDiamondPolygon(Border border, Color baseColor)
        {
            if (border.Child == null) return;
            var polygon = FindFirstVisualChild<System.Windows.Shapes.Polygon>(border);
            if (polygon != null)
            {
                polygon.Fill = LiquidGlassHelper.CreateGlassBackground(baseColor);
                polygon.Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
                polygon.StrokeThickness = 1.2;
            }

            // Thêm glow effect lên Grid chứa diamond (nếu chưa có)
            var grid = border.Child as Grid;
            if (grid == null && border.Child is FrameworkElement fe)
            {
                grid = FindFirstVisualChild<Grid>(border);
            }
            if (grid != null)
            {
                var isLightColor = (0.299 * baseColor.R + 0.587 * baseColor.G + 0.114 * baseColor.B) / 255.0 > 0.65;
                grid.Effect = LiquidGlassHelper.CreateGlassEffect(baseColor, isLightColor);
            }
        }

        private static T? FindFirstVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T found) return found;
                var result = FindFirstVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private static void UpdateSvgIconFillsRecursive(DependencyObject parent, Brush iconBrush)
        {
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is FlowMy.Controls.SvgViewboxEx svg)
                {
                    svg.Fill = iconBrush;
                }
                else
                {
                    UpdateSvgIconFillsRecursive(child, iconBrush);
                }
            }
        }

        private static FrameworkElement CreateExecutionStatusBadge(WorkflowNode node)
        {
            var statusText = new TextBlock
            {
                Text = "",
                Foreground = node.NodeBrush,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Opacity = 0.95
            };

            // Spinner quay tượng trưng "node đang xử lý" khi bật Cache node mode.
            // Spinner chỉ chạy khi container (badge) đang visible và spinner được host bật Visibility.
            var spinner = CreateSpinnerShape("Circle");

            // Host sẽ gán spinner.Tag = true/false theo CacheNode mode.
            // Spinner chỉ hiển thị khi: cache enabled AND node đang chạy.
            spinner.Tag = false;

            // Màu chữ status dùng theo node, còn toggle kết quả dùng màu xám đậm dễ đọc trên nền sáng
            var resultsToggle = new ToggleButton
            {
                Content = "▸ Có 0 kết quả trả về",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Application.Current.TryFindResource("PrimaryBrush") as Brush, // xám đậm, không đen tuyệt đối
                Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var resultsList = new StackPanel
            {
                Margin = new Thickness(4, 4, 0, 0),
                Visibility = Visibility.Collapsed
            };

            resultsToggle.Checked += (s, e) =>
            {
                resultsList.Visibility = Visibility.Visible;
                UpdateExecutionResultsToggleText(resultsToggle, node.ExecutionResultsItemsPanel?.Children.Count ?? 0, true);
            };
            resultsToggle.Unchecked += (s, e) =>
            {
                resultsList.Visibility = Visibility.Collapsed;
                UpdateExecutionResultsToggleText(resultsToggle, node.ExecutionResultsItemsPanel?.Children.Count ?? 0, false);
            };

            var errorToggle = new ToggleButton
            {
                Content = "▸ Có lỗi",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.DarkRed),
                Background = new SolidColorBrush(Color.FromArgb(20, 200, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 200, 0, 0)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var errorList = new StackPanel
            {
                Margin = new Thickness(4, 4, 0, 0),
                Visibility = Visibility.Collapsed
            };

            errorToggle.Checked += (s, e) =>
            {
                errorList.Visibility = Visibility.Visible;
                UpdateExecutionErrorToggleText(errorToggle, true);
            };
            errorToggle.Unchecked += (s, e) =>
            {
                errorList.Visibility = Visibility.Collapsed;
                UpdateExecutionErrorToggleText(errorToggle, false);
            };

            var content = new StackPanel();
            content.Children.Add(statusText);
            content.Children.Add(resultsToggle);
            content.Children.Add(resultsList);
            content.Children.Add(errorToggle);
            content.Children.Add(errorList);

            var container = new Border
            {
                Background = Brushes.White,
                BorderBrush = node.NodeBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0),
                Child = content,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = true,
                Effect = null // bỏ đổ bóng
            };

            node.ExecutionStatusTextUI = statusText;
            node.ExecutionBusySpinnerUI = spinner;
            node.ExecutionResultsToggleUI = resultsToggle;
            node.ExecutionResultsItemsPanel = resultsList;
            node.ExecutionErrorToggleUI = errorToggle;
            node.ExecutionErrorItemsPanel = errorList;
            node.ExecutionStatusContainerUI = container;
            Panel.SetZIndex(container, 998);

            container.IsVisibleChanged += (_, __) =>
            {
                // spinner được quản lý bởi Visibility của chính nó (tách khỏi badge container)
            };

            return container;
        }

        internal static void UpdateExecutionErrorToggleText(ToggleButton toggle, bool isExpanded)
        {
            var arrow = isExpanded ? "▾" : "▸";
            toggle.Content = $"{arrow} Có lỗi";
        }

        public static void RefreshExecutionIndicators(IEnumerable<WorkflowNode> nodes, IWorkflowEditorHost host)
        {
            if (nodes == null || host == null) return;

            foreach (var node in nodes)
            {
                if (node?.Border == null) continue;
                EnsureExecutionStatusBadgeAttached(node.Border, node, host);
            }
        }

        private static void EnsureExecutionStatusBadgeAttached(Border nodeBorder, WorkflowNode node, IWorkflowEditorHost host)
        {
            // Create once
            if (node.ExecutionStatusContainerUI == null || node.ExecutionStatusTextUI == null)
            {
                CreateExecutionStatusBadge(node);
            }

            if (node.ExecutionStatusContainerUI == null) return;

            var badge = node.ExecutionStatusContainerUI;
            badge.Tag = node;

            // Attach spinner to canvas (spinner không nằm trong badge để vẫn quay khi bật BitmapCache).
            // Hiển thị indicator ở node khi:
            // - CacheNodeEnabled = true, hoặc
            // - ConnectionAnimationDisplayMode != Animated (Off / Dashed)
            // Và node đang chạy (text status bắt đầu bằng "⏳").
            if (node.ExecutionBusySpinnerUI != null)
            {
                EnsureSpinnerShapeAndStyle(node, host);
                var useNodeExecutionIndicator =
                    host.CacheNodeEnabled ||
                    host.ConnectionAnimationDisplayMode != ConnectionAnimationDisplayMode.Animated;

                node.ExecutionBusySpinnerUI.Tag = useNodeExecutionIndicator;

                var isExecutingNow = node.ExecutionStatusTextUI?.Text?.StartsWith("⏳", System.StringComparison.Ordinal) == true;
                node.ExecutionBusySpinnerUI.Visibility = (useNodeExecutionIndicator && isExecutingNow)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (!host.WorkflowCanvas.Children.Contains(node.ExecutionBusySpinnerUI))
                {
                    host.WorkflowCanvas.Children.Add(node.ExecutionBusySpinnerUI);
                    Panel.SetZIndex(node.ExecutionBusySpinnerUI, 19999);
                }
            }

            if (!host.WorkflowCanvas.Children.Contains(badge))
            {
                host.WorkflowCanvas.Children.Add(badge);
            }

            void UpdatePos()
            {
                if (node.Border == null) return;
                var b = node.Border;
                var extraDiamondYOffset = GetStartEndDiamondBadgeYOffset(node);

                // place bottom-right, outside node (below)
                var left = Canvas.GetLeft(b) + b.ActualWidth - badge.ActualWidth;
                var top = Canvas.GetTop(b) + b.ActualHeight + 4 + extraDiamondYOffset;

                if (double.IsNaN(left)) left = node.X + b.ActualWidth - badge.ActualWidth;
                if (double.IsNaN(top)) top = node.Y + b.ActualHeight + 4 + extraDiamondYOffset;

                Canvas.SetLeft(badge, left);
                Canvas.SetTop(badge, top);
                Panel.SetZIndex(badge, 20000);

                // Place spinner outside the node at top-right with a small margin for visibility.
                if (node.ExecutionBusySpinnerUI != null)
                {
                    var sp = node.ExecutionBusySpinnerUI;
                    var spinnerSize = sp.Width > 0 ? sp.Width : 26d;
                    const double outsideMargin = 14d;
                    var baseX = Canvas.GetLeft(b);
                    var baseY = Canvas.GetTop(b);
                    var sx = baseX + b.ActualWidth + 10d;
                    var sy = baseY + 8d;

                    switch (host.NodeSpinnerPosition ?? "TopRight")
                    {
                        case "TopLeft":
                            sx = baseX - spinnerSize - outsideMargin;
                            sy = baseY - outsideMargin;
                            break;
                        case "BottomRight":
                            sx = baseX + b.ActualWidth - (spinnerSize * 0.2) + outsideMargin * 0.5;
                            sy = baseY + b.ActualHeight - (spinnerSize * 0.2) + outsideMargin * 0.5;
                            break;
                        case "BottomLeft":
                            sx = baseX - spinnerSize + (spinnerSize * 0.2) - outsideMargin * 0.5;
                            sy = baseY + b.ActualHeight - (spinnerSize * 0.2) + outsideMargin * 0.5;
                            break;
                        case "Center":
                            sx = baseX + (b.ActualWidth - spinnerSize) / 2d;
                            sy = baseY + (b.ActualHeight - spinnerSize) / 2d;
                            break;
                        default: // TopRight
                            sx = baseX + b.ActualWidth - (spinnerSize * 0.2) + outsideMargin * 0.5;
                            sy = baseY - outsideMargin;
                            break;
                    }
                    if (!double.IsNaN(sx) && !double.IsInfinity(sx))
                        Canvas.SetLeft(sp, sx);
                    if (!double.IsNaN(sy) && !double.IsInfinity(sy))
                        Canvas.SetTop(sp, sy);
                    Panel.SetZIndex(sp, 19999);
                }
            }

            // Keep in sync when node moves / resizes
            nodeBorder.Loaded += (_, __) => UpdatePos();
            nodeBorder.SizeChanged += (_, __) => UpdatePos();

            // Throttle LayoutUpdated to avoid excessive updates during zoom
            nodeBorder.LayoutUpdated += (_, __) =>
            {
                if (badge.Visibility != Visibility.Visible) return;

                // During zoom, throttle updates
                if (_isZooming)
                {
                    if (!_badgeUpdateTimers.TryGetValue(nodeBorder, out var timer))
                    {
                        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(BadgeUpdateThrottleMs) };
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            UpdatePos();
                        };
                        _badgeUpdateTimers[nodeBorder] = timer;
                    }

                    if (!timer.IsEnabled)
                    {
                        timer.Start();
                    }
                }
                else
                {
                    // Not zooming: update immediately
                    UpdatePos();
                }
            };
            nodeBorder.Unloaded += (_, __) =>
            {
                if (host.WorkflowCanvas.Children.Contains(badge))
                {
                    host.WorkflowCanvas.Children.Remove(badge);
                }

                if (node.ExecutionBusySpinnerUI != null && host.WorkflowCanvas.Children.Contains(node.ExecutionBusySpinnerUI))
                {
                    host.WorkflowCanvas.Children.Remove(node.ExecutionBusySpinnerUI);
                }
            };
        }

        private static System.Windows.Shapes.Shape CreateSpinnerShape(string shapeKey)
        {
            System.Windows.Shapes.Shape spinner = shapeKey switch
            {
                "Diamond" => new System.Windows.Shapes.Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(13, 1), new Point(25, 13), new Point(13, 25), new Point(1, 13)
                    },
                    Stretch = Stretch.Fill
                },
                "Square" or "FollowNodeShape" => new System.Windows.Shapes.Rectangle(),
                "RoundedSquare" => new System.Windows.Shapes.Rectangle { RadiusX = 6, RadiusY = 6 },
                _ => new System.Windows.Shapes.Ellipse()
            };

            spinner.Width = 26;
            spinner.Height = 26;
            spinner.Margin = new Thickness(0);
            spinner.StrokeThickness = 3.2;
            spinner.StrokeDashArray = new DoubleCollection { 1.35, 2.4 };
            spinner.Stroke = Application.Current.TryFindResource("InfoBrush") as Brush
                             ?? new SolidColorBrush(Color.FromRgb(56, 189, 248));
            spinner.StrokeStartLineCap = PenLineCap.Round;
            spinner.StrokeEndLineCap = PenLineCap.Round;
            spinner.Fill = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
            spinner.Visibility = Visibility.Collapsed;
            spinner.IsHitTestVisible = false;
            spinner.Opacity = 0.98;
            spinner.RenderTransformOrigin = new Point(0.5, 0.5);
            spinner.RenderTransform = new RotateTransform(0);
            spinner.Uid = shapeKey;
            return spinner;
        }

        private static void EnsureSpinnerShapeAndStyle(WorkflowNode node, IWorkflowEditorHost host)
        {
            if (node.ExecutionBusySpinnerUI == null) return;

            var desiredShape = host.NodeSpinnerShape ?? "Circle";
            var currentShapeKey = node.ExecutionBusySpinnerUI.Uid;
            var needsRebuild = !string.Equals(currentShapeKey, desiredShape, StringComparison.OrdinalIgnoreCase);
            if (needsRebuild)
            {
                var old = node.ExecutionBusySpinnerUI;
                var wasVisible = old.Visibility == Visibility.Visible;
                var spinner = CreateSpinnerShape(desiredShape);
                spinner.Uid = desiredShape;
                spinner.Visibility = wasVisible ? Visibility.Visible : Visibility.Collapsed;

                if (host.WorkflowCanvas.Children.Contains(old))
                    host.WorkflowCanvas.Children.Remove(old);
                host.WorkflowCanvas.Children.Add(spinner);
                Panel.SetZIndex(spinner, 19999);
                node.ExecutionBusySpinnerUI = spinner;
            }

            var sp = node.ExecutionBusySpinnerUI;
            if (sp == null) return;

            var size = host.NodeSpinnerScaleWithNode && node.Border != null
                ? Math.Max(10, node.Border.ActualWidth * host.NodeSpinnerSizeRatio)
                : Math.Max(10, host.NodeSpinnerSize);
            sp.Width = size;
            sp.Height = size;
            sp.StrokeThickness = Math.Max(1, host.NodeSpinnerStrokeThickness);

            if (desiredShape == "FollowNodeShape")
            {
                if (node.IsStartDiamondVisual || (node.Type == NodeType.End && node.EndBehavior == EndNodeBehavior.ReturnToParent))
                {
                    if (sp is not System.Windows.Shapes.Polygon diamond)
                    {
                        var replacement = CreateSpinnerShape("Diamond");
                        ReplaceSpinnerShape(host, node, sp, replacement);
                        sp = replacement;
                    }
                }
                else if (node.Border?.CornerRadius.TopLeft > 0)
                {
                    if (sp is not System.Windows.Shapes.Rectangle rounded)
                    {
                        var replacement = CreateSpinnerShape("RoundedSquare");
                        ReplaceSpinnerShape(host, node, sp, replacement);
                        sp = replacement;
                    }
                    if (sp is System.Windows.Shapes.Rectangle rr && node.Border != null)
                    {
                        var r = Math.Max(0, Math.Min(node.Border.CornerRadius.TopLeft, sp.Width / 3d));
                        rr.RadiusX = r;
                        rr.RadiusY = r;
                    }
                }
                else if (sp is not System.Windows.Shapes.Rectangle)
                {
                    var replacement = CreateSpinnerShape("Square");
                    ReplaceSpinnerShape(host, node, sp, replacement);
                    sp = replacement;
                }
            }

            sp.StrokeDashArray = host.NodeSpinnerArcMode
                ? new DoubleCollection { 1.35, 2.4 }
                : new DoubleCollection { 3.0, 1.8 };

            if (host.NodeSpinnerMultiColor)
            {
                SolidColorBrush multiBrush;
                if (sp.Stroke is SolidColorBrush existingSolid)
                {
                    var baseColor = existingSolid.Color;
                    multiBrush = new SolidColorBrush(baseColor);
                }
                else
                {
                    multiBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                }
                sp.Stroke = multiBrush;
                var multiColorAnim = new ColorAnimationUsingKeyFrames
                {
                    // Xen kẽ màu đậm (cho nền sáng) và màu sáng (cho nền tối), mỗi 1 giây đổi màu.
                    Duration = new Duration(TimeSpan.FromSeconds(10.0)),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(13, 110, 253), KeyTime.FromPercent(0.0)));  // dark blue
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(125, 211, 252), KeyTime.FromPercent(0.1))); // light cyan
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(190, 24, 93), KeyTime.FromPercent(0.2)));  // dark pink
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(253, 186, 116), KeyTime.FromPercent(0.3))); // light orange
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(22, 101, 52), KeyTime.FromPercent(0.4)));  // dark green
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(134, 239, 172), KeyTime.FromPercent(0.5))); // light green
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(109, 40, 217), KeyTime.FromPercent(0.6))); // dark purple
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(196, 181, 253), KeyTime.FromPercent(0.7))); // light violet
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(161, 98, 7), KeyTime.FromPercent(0.8)));   // dark amber
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(253, 224, 71), KeyTime.FromPercent(0.9))); // light yellow
                multiColorAnim.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(13, 110, 253), KeyTime.FromPercent(1.0)));
                multiBrush.BeginAnimation(SolidColorBrush.ColorProperty, multiColorAnim);
            }
            else
            {
                var monoSource = Application.Current.TryFindResource("InfoBrush") as SolidColorBrush;
                var mono = monoSource != null
                    ? new SolidColorBrush(monoSource.Color)
                    : new SolidColorBrush(Color.FromRgb(56, 189, 248));
                mono.BeginAnimation(SolidColorBrush.ColorProperty, null);
                sp.Stroke = mono;
            }

            // Optional blinking soft background, brighter on odd-second phases.
            var blinkSource = Application.Current.TryFindResource(host.NodeSpinnerBlinkBackgroundColorKey ?? "WarningBrush") as SolidColorBrush;
            var blinkBase = blinkSource?.Color ?? Color.FromRgb(245, 158, 11);
            var fillBrush = sp.Fill as SolidColorBrush ?? new SolidColorBrush(blinkBase);
            fillBrush.Color = blinkBase;
            sp.Fill = fillBrush;
            if (host.NodeSpinnerBlinkBackground)
            {
                var intensity = Math.Max(0.10, Math.Min(1.0, host.NodeSpinnerBlinkIntensity));
                var baseOpacityInput = Math.Max(0.0, Math.Min(1.0, host.NodeSpinnerBlinkBaseOpacity));
                var baseOpacity = Math.Max(0.0, Math.Min(0.95, baseOpacityInput));
                var peakOpacityInput = Math.Max(0.0, Math.Min(1.0, host.NodeSpinnerBlinkPeakOpacity));
                var configuredPeak = Math.Max(baseOpacity + 0.02, peakOpacityInput);
                var peakOpacity = Math.Min(1.0, baseOpacity + (configuredPeak - baseOpacity) * intensity);
                var blinkMode = host.NodeSpinnerBlinkMode ?? "Soft";
                var fillOpacityAnim = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(TimeSpan.FromSeconds(2)),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                // second 0-1: low; second 1-2 (odd-second phase): blink high.
                if (string.Equals(blinkMode, "Hard", StringComparison.OrdinalIgnoreCase))
                {
                    fillOpacityAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
                    fillOpacityAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1))));
                    fillOpacityAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(peakOpacity, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.001))));
                    fillOpacityAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(peakOpacity, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2))));
                }
                else
                {
                    fillOpacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
                    fillOpacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1))));
                    fillOpacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(peakOpacity, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.5))));
                    fillOpacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2))));
                }
                fillBrush.BeginAnimation(SolidColorBrush.OpacityProperty, fillOpacityAnim);
            }
            else
            {
                fillBrush.BeginAnimation(SolidColorBrush.OpacityProperty, null);
                fillBrush.Opacity = Math.Max(0.0, Math.Min(1.0, host.NodeSpinnerBlinkBaseOpacity));
            }

            EnsureSpinnerAnimationHook(sp, host);
            ApplySpinnerBorderAnimationState(sp, host);
        }

        private static void ReplaceSpinnerShape(IWorkflowEditorHost host, WorkflowNode node, System.Windows.Shapes.Shape oldShape, System.Windows.Shapes.Shape newShape)
        {
            var wasVisible = oldShape.Visibility == Visibility.Visible;
            newShape.Visibility = wasVisible ? Visibility.Visible : Visibility.Collapsed;
            newShape.Width = oldShape.Width;
            newShape.Height = oldShape.Height;
            newShape.StrokeThickness = oldShape.StrokeThickness;
            newShape.Stroke = oldShape.Stroke;
            newShape.StrokeDashArray = oldShape.StrokeDashArray;

            if (host.WorkflowCanvas.Children.Contains(oldShape))
                host.WorkflowCanvas.Children.Remove(oldShape);
            host.WorkflowCanvas.Children.Add(newShape);
            Panel.SetZIndex(newShape, 19999);
            node.ExecutionBusySpinnerUI = newShape;
        }

        private static void EnsureSpinnerAnimationHook(System.Windows.Shapes.Shape spinner, IWorkflowEditorHost host)
        {
            if (spinner.Resources.Contains("SpinnerHooked")) return;
            spinner.Resources["SpinnerHooked"] = true;
            spinner.IsVisibleChanged += (_, __) => ApplySpinnerBorderAnimationState(spinner, host);
        }

        private static void ApplySpinnerBorderAnimationState(System.Windows.Shapes.Shape spinner, IWorkflowEditorHost host)
        {
            if (spinner.IsVisible)
            {
                spinner.BeginAnimation(
                    System.Windows.Shapes.Shape.StrokeDashOffsetProperty,
                    new DoubleAnimation
                    {
                        From = 0,
                        To = -24,
                        Duration = new Duration(TimeSpan.FromSeconds(Math.Max(0.2, host.NodeSpinnerSpinSeconds))),
                        RepeatBehavior = RepeatBehavior.Forever
                    });
                spinner.BeginAnimation(
                    UIElement.OpacityProperty,
                    new DoubleAnimation
                    {
                        From = 0.62,
                        To = 1.0,
                        Duration = new Duration(TimeSpan.FromSeconds(Math.Max(0.2, host.NodeSpinnerSpinSeconds) * 0.8)),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    });
            }
            else
            {
                spinner.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, null);
                spinner.BeginAnimation(UIElement.OpacityProperty, null);
                spinner.StrokeDashOffset = 0;
                spinner.Opacity = 0.98;
            }
        }

        private static double GetStartEndDiamondBadgeYOffset(WorkflowNode node)
        {
            var isStartDiamond = node.IsStartDiamondVisual;
            var isEndDiamond = node.Type == NodeType.End && node.EndBehavior == EndNodeBehavior.ReturnToParent;
            if (!isStartDiamond && !isEndDiamond)
            {
                return 0d;
            }

            // Hình thoi có phần tip chìa ra nên badge cần hạ thêm để tránh đè text runtime.
            return node.DiamondSharpness switch
            {
                DiamondSharpness.Soft => 10d,
                DiamondSharpness.Medium => 14d,
                DiamondSharpness.Sharp => 18d,
                _ => 14d
            };
        }

        // NOTE: Từ phiên bản đơn giản hóa, DataPanel embedded trong node không còn được dùng.
        // Các chức năng cấu hình input/output được chuyển sang dialog riêng nên các API dưới
        // đây chỉ còn phục vụ logic cũ. Nếu không còn nơi nào sử dụng, có thể xoá hoàn toàn.

        public static FrameworkElement CreateDataPanel(WorkflowNode node, IWorkflowEditorHost host, DataPanelCustomizationOptions? customizationOptions = null)
        {
            var textBrush = GetNodeTextBrush(node);

            var container = new Border
            {
                Margin = new Thickness(6, 2, 6, 6),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Visibility = Visibility.Collapsed
            };

            var stack = new StackPanel();

            if (node.DynamicInputs.Any())
            {
                // Section header: Inputs + collapse toggle (right)
                // Get custom section title from customization options or use default
                var sectionTitle = customizationOptions?.CustomInputsSectionTitle
                    ?? GetInputsSectionTitle(node)
                    ?? "Inputs";

                var inputsContent = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
                var inputsHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                inputsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                inputsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var inputsTitle = new TextBlock
                {
                    Text = sectionTitle,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = textBrush,
                    Opacity = 0.9,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var inputsToggle = new ToggleButton
                {
                    Width = 18,
                    Height = 18,
                    Content = "▾",
                    IsChecked = true,
                    ToolTip = "Đóng/mở Inputs",
                    Background = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)),
                    Foreground = Brushes.White,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Template = BuildRoundedToggleTemplate()
                };

                inputsToggle.Checked += (s, e) =>
                {
                    inputsContent.Visibility = Visibility.Visible;
                    inputsToggle.Content = "▾";
                };
                inputsToggle.Unchecked += (s, e) =>
                {
                    inputsContent.Visibility = Visibility.Collapsed;
                    inputsToggle.Content = "▸";
                };

                Grid.SetColumn(inputsTitle, 0);
                Grid.SetColumn(inputsToggle, 1);
                inputsHeader.Children.Add(inputsTitle);
                inputsHeader.Children.Add(inputsToggle);

                stack.Children.Add(inputsHeader);

                // Filter inputs if ShowOnlyFirstInput is true
                var inputsToShow = customizationOptions?.ShowOnlyFirstInput == true
                    ? node.DynamicInputs.Take(1).ToList()
                    : node.DynamicInputs.ToList();

                // Special handling for LoopNode: filter inputs based on LoopType
                if (node is LoopNode loopNode)
                {
                    inputsToShow = loopNode.LoopType switch
                    {
                        LoopType.ForLoop or LoopType.RepeatN =>
                            node.DynamicInputs.Where(i => i.Key == "loopCount").ToList(),
                        LoopType.ForEachArray =>
                            node.DynamicInputs.Where(i => i.Key == "loopArray").ToList(),
                        _ => inputsToShow
                    };
                }

                foreach (var input in inputsToShow)
                {
                    // Each Data-In item: 3 lines
                    // Line 1: [ (Node Combo + Key Combo) 80% ] [ + 20% ]
                    // Line 2: labels: Key | Value | Type
                    // Line 3: controls: TextKey | TextValue | ComboType
                    var item = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

                    var combo = new ComboBox
                    {
                        Height = 24,
                        MinWidth = 90,
                        Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                        ItemsSource = input.AvailableSources,
                        SelectedValuePath = nameof(WorkflowDataSourceOption.NodeId),
                        DisplayMemberPath = nameof(WorkflowDataSourceOption.Title),
                        SelectedValue = input.SelectedSourceNodeId,
                        Visibility = Visibility.Visible,
                        IsEnabled = true
                    };

                    // ensure output-key list for current source
                    // RefreshOutputKeyOptions(host, input);

                    var outputKeyCombo = new ComboBox
                    {
                        Height = 24,
                        MinWidth = 90,
                        Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                        ItemsSource = input.AvailableOutputKeyOptions,
                        SelectedValuePath = nameof(WorkflowOutputKeyOption.Key),
                        SelectedValue = input.SelectedSourceOutputKey,
                        ToolTip = "Chọn output key từ node nguồn (hiển thị kèm type)",
                        Visibility = (input.AvailableOutputKeyOptions?.Count ?? 0) > 1 ? Visibility.Visible : Visibility.Collapsed,
                        IsEnabled = (input.AvailableOutputKeyOptions?.Count ?? 0) > 0
                    };
                    // WPF sẽ tự gọi ToString() của WorkflowOutputKeyOption để hiển thị
                    // Format: "key (Type)" ví dụ: "value (Integer)"

                    // Set SelectedItem để đảm bảo hiển thị đúng text
                    if (!string.IsNullOrWhiteSpace(input.SelectedSourceOutputKey) && input.AvailableOutputKeyOptions != null)
                    {
                        var initialSelected = input.AvailableOutputKeyOptions.FirstOrDefault(opt => opt.Key == input.SelectedSourceOutputKey);
                        if (initialSelected != null)
                        {
                            outputKeyCombo.SelectedItem = initialSelected;
                        }
                    }

                    // Store reference để có thể cập nhật sau này
                    input.OutputKeySelectorUI = outputKeyCombo;

                    var btnAdd = new Button
                    {
                        MinWidth = 28,
                        Height = 26,
                        Padding = new Thickness(0),
                        Content = new TextBlock
                        {
                            Text = "+",
                            FontSize = 14,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        ToolTip = "Thêm một Data In mới",
                        Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Template = BuildRoundedButtonTemplate(),
                        Visibility = customizationOptions?.HideAddButton == true ? Visibility.Collapsed : Visibility.Visible
                    };

                    // Line 1 layout
                    var line1 = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                    line1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) }); // ~90%
                    line1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // ~10%

                    var comboHost = new Grid { Margin = new Thickness(0, 0, 8, 0) };
                    comboHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    comboHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    combo.Margin = new Thickness(0, 0, 6, 0);
                    outputKeyCombo.Margin = new Thickness(6, 0, 0, 0);
                    Grid.SetColumn(combo, 0);
                    Grid.SetColumn(outputKeyCombo, 1);
                    comboHost.Children.Add(combo);
                    comboHost.Children.Add(outputKeyCombo);

                    Grid.SetColumn(comboHost, 0);
                    Grid.SetColumn(btnAdd, 1);
                    line1.Children.Add(comboHost);
                    line1.Children.Add(btnAdd);

                    // Line 2: labels 3 cells
                    var line2 = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                    line2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    line2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    line2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    TextBlock MakeSmallLabel(string t) => new TextBlock
                    {
                        Text = t,
                        Foreground = textBrush,
                        Opacity = 0.75,
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 6, 0)
                    };

                    var keyLabel = MakeSmallLabel("Key");
                    var valueLabel = MakeSmallLabel("Value");
                    var typeLabel = MakeSmallLabel("Type");
                    Grid.SetColumn(keyLabel, 0);
                    Grid.SetColumn(valueLabel, 1);
                    Grid.SetColumn(typeLabel, 2);
                    line2.Children.Add(keyLabel);
                    line2.Children.Add(valueLabel);
                    line2.Children.Add(typeLabel);

                    // Line 3: controls 3 cells
                    var line3 = new Grid { Margin = new Thickness(0, 0, 0, 0) };
                    line3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    line3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    line3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    // Determine initial key text: nếu source node là InputNode và output key là "Input", lấy từ InputNode.Key
                    // Ưu tiên InputNode.Key hơn UserKeyOverride khi source là InputNode
                    // var initialKeyText = string.Empty;
                    // if (host.ViewModel != null && !string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
                    // {
                    //     var srcNode = host.ViewModel.Nodes.FirstOrDefault(n => n.Id == input.SelectedSourceNodeId);
                    //     if (srcNode is InputNode inputNode)
                    //     {
                    //         var outputKey = (input.SelectedSourceOutputKey ?? input.Key ?? string.Empty).Trim();
                    //         if (string.Equals(outputKey, "Input", StringComparison.OrdinalIgnoreCase) ||
                    //             string.Equals(outputKey, "value", StringComparison.OrdinalIgnoreCase))
                    //         {
                    //             // Nếu output key là "Input" và source là InputNode, lấy key từ InputNode.Key
                    //             if (!string.IsNullOrWhiteSpace(inputNode.Key))
                    //             {
                    //                 initialKeyText = inputNode.Key;
                    //                 // Clear UserKeyOverride để đảm bảo luôn lấy từ InputNode.Key
                    //                 input.UserKeyOverride = inputNode.Key;
                    //             }
                    //         }
                    //     }
                    // }

                    // // Nếu không phải InputNode hoặc không có key, dùng UserKeyOverride
                    //if (string.IsNullOrWhiteSpace(initialKeyText))
                    //{
                    //    initialKeyText = input.UserKeyOverride ?? string.Empty;
                    //}

                    var keyOverrideBox = new TextBox
                    {
                        MinHeight = 26,
                        MaxHeight = 200,
                        MinWidth = 80,
                        FontSize = 12,
                        Style = Application.Current.TryFindResource("InputTextBoxV2") as Style,
                        Padding = new Thickness(2, 2, 2, 0),
                        Margin = new Thickness(0, 0, 6, 0),
                        Text = input.UserKeyOverride ?? string.Empty,
                        TextWrapping = TextWrapping.Wrap,
                        AcceptsReturn = true,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        IsEnabled = customizationOptions?.DisableKeyTextBox != true
                    };

                    // Determine default value: customizationOptions.DefaultValue > input.UserValueOverride > resolved value từ source
                    // For MouseEventNode/KeyPressEventNode: DefaultValue is set from RepeatCount property in GetCustomizationOptions
                    // Khi đã chọn source (InputNode/...) mà UserValueOverride rỗng → lấy GetInputResolvedValue để đồng bộ ngay khi mở panel
                    var defaultValue = customizationOptions?.DefaultValue
                        ?? input.UserValueOverride
                        ?? (!string.IsNullOrWhiteSpace(input.SelectedSourceNodeId) ? GetInputResolvedValue(host, node, input) : string.Empty);

                    var valueOverrideBox = new TextBox
                    {
                        MinHeight = 26,
                        MaxHeight = 200,
                        MinWidth = 80,
                        FontSize = 12,
                        Style = Application.Current.TryFindResource("InputTextBoxV2") as Style,
                        Margin = new Thickness(0, 0, 6, 0),
                        Padding = new Thickness(2, 2, 2, 0),
                        Text = defaultValue,
                        TextWrapping = TextWrapping.Wrap,
                        AcceptsReturn = true,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };

                    // Update input.UserValueOverride if default value was set from customizationOptions
                    if (customizationOptions?.DefaultValue != null && string.IsNullOrWhiteSpace(input.UserValueOverride))
                    {
                        input.UserValueOverride = customizationOptions.DefaultValue;
                    }

                    // Determine allowed types and default type
                    // Special handling for LoopNode: restrict types based on input key
                    List<WorkflowDataType> allowedTypes;
                    if (node is LoopNode loopNodeForType)
                    {
                        // Dựa vào input key để quyết định allowed types
                        if (input.Key == "loopCount")
                        {
                            // loopCount: chỉ cho Integer và Number
                            allowedTypes = new List<WorkflowDataType>
                            {
                                WorkflowDataType.Integer,
                                WorkflowDataType.Number
                            };
                        }
                        else if (input.Key == "loopArray")
                        {
                            // loopArray: chỉ cho ArrayString, ArrayNumber, ArrayDynamic
                            allowedTypes = new List<WorkflowDataType>
                            {
                                WorkflowDataType.ArrayString,
                                WorkflowDataType.ArrayNumber,
                                WorkflowDataType.ArrayDynamic
                            };
                        }
                        else
                        {
                            // Fallback: dùng customizationOptions hoặc tất cả types
                            allowedTypes = customizationOptions?.AllowedTypes
                                ?? Enum.GetValues(typeof(WorkflowDataType)).Cast<WorkflowDataType>().ToList();
                        }
                    }
                    else
                    {
                        // Non-LoopNode: dùng customizationOptions hoặc tất cả types
                        allowedTypes = customizationOptions?.AllowedTypes
                            ?? Enum.GetValues(typeof(WorkflowDataType)).Cast<WorkflowDataType>().ToList();
                    }

                    var defaultType = customizationOptions?.DefaultType ?? input.ConvertType;

                    var typeCombo = new ComboBox
                    {
                        Height = 26,
                        MinWidth = 90,
                        Margin = new Thickness(0, 0, 0, 0),
                        Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                        ItemsSource = allowedTypes,
                        SelectedItem = defaultType,
                        ToolTip = "Chọn kiểu dữ liệu để convert value",
                        IsEnabled = customizationOptions?.DisableTypeComboBox != true
                    };

                    // Update input.ConvertType if default type was set
                    if (customizationOptions?.DefaultType != null)
                    {
                        input.ConvertType = customizationOptions.DefaultType.Value;
                    }

                    // Store reference để có thể cập nhật sau này
                    input.ConvertTypeSelectorUI = typeCombo;

                    // Convert/validation section (shown below, easier to read)
                    var convertedText = new TextBlock
                    {
                        Text = "Converted: —",
                        Foreground = textBrush,
                        Opacity = 0.9,
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap
                    };

                    var errorText = new TextBlock
                    {
                        Text = "",
                        Foreground = Brushes.OrangeRed,
                        Opacity = 0.95,
                        FontSize = 11,
                        Margin = new Thickness(0, 2, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };

                    var convertInfoPanel = new Border
                    {
                        Margin = new Thickness(0, 6, 0, 0),
                        Padding = new Thickness(8, 6, 8, 6),
                        CornerRadius = new CornerRadius(8),
                        Background = new SolidColorBrush(Color.FromArgb(95, 0, g: 0, 0)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                        BorderThickness = new Thickness(1)
                    };

                    var convertInfoStack = new StackPanel();
                    convertInfoStack.Children.Add(convertedText);
                    convertInfoStack.Children.Add(errorText);
                    convertInfoPanel.Child = convertInfoStack;

                    string GetRawValueForConvert()
                    {
                        var manual = (input.UserValueOverride ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(manual)) return manual;

                        var resolved = GetInputResolvedValue(host, node, input);
                        return (resolved ?? string.Empty).Trim();
                    }

                    void UpdateConvertPreview()
                    {
                        var raw = GetRawValueForConvert();
                        var t = input.ConvertType;

                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            convertedText.Text = "Converted: —";
                            errorText.Text = "";
                            valueOverrideBox.ClearValue(Control.BorderBrushProperty);
                            valueOverrideBox.ClearValue(Control.BorderThicknessProperty);
                            return;
                        }

                        if (TryConvertValue(raw, t, out var converted, out var err))
                        {
                            convertedText.Text = "Converted: " + converted;
                            errorText.Text = "";
                            valueOverrideBox.ClearValue(Control.BorderBrushProperty);
                            valueOverrideBox.ClearValue(Control.BorderThicknessProperty);
                        }
                        else
                        {
                            convertedText.Text = "Converted: —";
                            errorText.Text = err ?? "Không convert được";
                            valueOverrideBox.BorderBrush = Brushes.OrangeRed;
                            valueOverrideBox.BorderThickness = new Thickness(1);
                        }
                    }

                    combo.SelectionChanged += (s, e) =>
                    {
                        if (combo.SelectedValue is string id)
                        {
                            input.SelectedSourceNodeId = id;
                            // RefreshOutputKeyOptions(host, input);

                            // Cập nhật outputKeyCombo
                            outputKeyCombo.ItemsSource = input.AvailableOutputKeyOptions;
                            outputKeyCombo.SelectedValue = input.SelectedSourceOutputKey;

                            // Cập nhật visibility và enabled state
                            var outputKeyCount = input.AvailableOutputKeyOptions?.Count ?? 0;
                            outputKeyCombo.Visibility = outputKeyCount > 1 ? Visibility.Visible : Visibility.Collapsed;
                            outputKeyCombo.IsEnabled = outputKeyCount > 0;

                            // Set SelectedItem để đảm bảo hiển thị đúng
                            if (!string.IsNullOrWhiteSpace(input.SelectedSourceOutputKey) && input.AvailableOutputKeyOptions != null)
                            {
                                var selectedOption = input.AvailableOutputKeyOptions.FirstOrDefault(opt => opt.Key == input.SelectedSourceOutputKey);
                                if (selectedOption != null)
                                {
                                    outputKeyCombo.SelectedItem = selectedOption;
                                }
                            }

                            // Tự động set ConvertType khi source node thay đổi
                            // QUAN TRỌNG: Nếu là InputNode, lấy trực tiếp từ InputNode.DataType (biến trung gian mới nhất)
                            if (host.ViewModel != null && !string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
                            {
                                var srcNode = host.ViewModel.Nodes.FirstOrDefault(n => n.Id == input.SelectedSourceNodeId);
                                WorkflowDataType? workflowType = null;

                                // Nếu là InputNode, lấy trực tiếp từ InputNode.DataType (biến trung gian)
                                if (srcNode is InputNode inputNode)
                                {
                                    // Lấy giá trị mới nhất từ combobox type của InputNode
                                    workflowType = inputNode.DataType;

                                    // Đảm bảo SelectedSourceOutputKey được set đúng
                                    if (string.IsNullOrWhiteSpace(input.SelectedSourceOutputKey))
                                    {
                                        input.SelectedSourceOutputKey = input.Key;
                                        outputKeyCombo.SelectedValue = input.Key;

                                        if (input.AvailableOutputKeyOptions != null)
                                        {
                                            var selectedOption = input.AvailableOutputKeyOptions
                                                .FirstOrDefault(opt => opt != null && opt.Key == input.SelectedSourceOutputKey);
                                            if (selectedOption != null)
                                            {
                                                outputKeyCombo.SelectedItem = selectedOption;
                                            }
                                        }
                                    }
                                }
                                else if (input.AvailableOutputKeyOptions != null && input.AvailableOutputKeyOptions.Count > 0)
                                {
                                    // Với các node khác, dùng logic cũ
                                    WorkflowOutputKeyOption? selectedOption = null;

                                    if (!string.IsNullOrWhiteSpace(input.SelectedSourceOutputKey))
                                    {
                                        selectedOption = input.AvailableOutputKeyOptions.FirstOrDefault(opt => opt.Key == input.SelectedSourceOutputKey);
                                    }

                                    if (selectedOption == null)
                                    {
                                        selectedOption = input.AvailableOutputKeyOptions.FirstOrDefault();
                                        if (selectedOption != null)
                                        {
                                            input.SelectedSourceOutputKey = selectedOption.Key;
                                            outputKeyCombo.SelectedValue = selectedOption.Key;
                                            outputKeyCombo.SelectedItem = selectedOption;
                                        }
                                    }

                                    if (selectedOption?.Type.HasValue == true)
                                    {
                                        workflowType = selectedOption.Type.Value;
                                    }
                                }

                                // Set ConvertType nếu có type
                                if (workflowType.HasValue)
                                {
                                    input.ConvertType = workflowType.Value;
                                    typeCombo.SelectedItem = workflowType.Value;
                                }
                            }

                            // Luôn đồng bộ Key textbox + Value textbox theo lựa chọn mới
                            // Nếu output key là "Input" và source node là InputNode, hiển thị Key property của InputNode
                            var keyText = (input.SelectedSourceOutputKey ?? input.Key ?? string.Empty).Trim();
                            // if (string.Equals(keyText, "Input", StringComparison.OrdinalIgnoreCase) &&
                            //     host.ViewModel != null &&
                            //     !string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
                            // {
                            //     var srcNode = host.ViewModel.Nodes.FirstOrDefault(n => n.Id == input.SelectedSourceNodeId);
                            //     if (srcNode is InputNode inputNode)
                            //     {
                            //         // Luôn lấy từ InputNode.Key, kể cả khi rỗng
                            //         keyText = inputNode.Key ?? string.Empty;
                            //     }
                            // }
                            keyOverrideBox.Text = keyText;
                            input.UserKeyOverride = string.IsNullOrWhiteSpace(keyText) ? null : keyText;
                            valueOverrideBox.Text = GetInputResolvedValue(host, node, input);
                            UpdateConvertPreview();
                            host.RequestSyncDataPanels(immediate: false);
                        }
                    };
                    combo.LostFocus += (s, e) => host.RequestSyncDataPanels(immediate: true);

                    outputKeyCombo.SelectionChanged += (s, e) =>
                    {
                        string? k = null;
                        // Lấy key từ SelectedValue hoặc SelectedItem để đảm bảo lấy được giá trị
                        if (outputKeyCombo.SelectedValue is string selectedValue)
                        {
                            k = selectedValue;
                        }
                        else if (outputKeyCombo.SelectedItem is WorkflowOutputKeyOption selectedOption)
                        {
                            k = selectedOption.Key;
                        }

                        if (!string.IsNullOrWhiteSpace(k))
                        {
                            input.SelectedSourceOutputKey = k;

                            // Tự động set ConvertType dựa trên OutputType của output key được chọn
                            // QUAN TRỌNG: Nếu là InputNode, lấy trực tiếp từ InputNode.DataType (biến trung gian mới nhất)
                            WorkflowDataType? workflowType = null;

                            if (host.ViewModel != null && !string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
                            {
                                var srcNode = host.ViewModel.Nodes.FirstOrDefault(n => n.Id == input.SelectedSourceNodeId);

                                // Nếu là InputNode, lấy trực tiếp từ InputNode.DataType (biến trung gian)
                                if (srcNode is InputNode inputNode)
                                {
                                    // Lấy giá trị mới nhất từ combobox type của InputNode
                                    workflowType = inputNode.DataType;
                                }
                                else
                                {
                                    // Với các node khác, lấy từ AvailableOutputKeyOptions
                                    var selectedOpt = input.AvailableOutputKeyOptions?.FirstOrDefault(opt => opt.Key == k);
                                    if (selectedOpt?.Type.HasValue == true)
                                    {
                                        workflowType = selectedOpt.Type.Value;
                                    }
                                }
                            }

                            // Set ConvertType nếu có type
                            if (workflowType.HasValue)
                            {
                                input.ConvertType = workflowType.Value;
                                typeCombo.SelectedItem = workflowType.Value;
                            }

                            // Luôn đồng bộ Key textbox + Value textbox theo lựa chọn mới
                            // Nếu output key là "Input" và source node là InputNode, hiển thị Key property của InputNode
                            var keyText = (input.SelectedSourceOutputKey ?? input.Key ?? string.Empty).Trim();
                            // if (string.Equals(keyText, "Input", StringComparison.OrdinalIgnoreCase) &&
                            //     host.ViewModel != null &&
                            //     !string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
                            // {
                            //     var srcNode = host.ViewModel.Nodes.FirstOrDefault(n => n.Id == input.SelectedSourceNodeId);
                            //     if (srcNode is InputNode inputNode)
                            //     {
                            //         // Luôn lấy từ InputNode.Key, kể cả khi rỗng
                            //         keyText = inputNode.Key ?? string.Empty;
                            //     }
                            // }
                            keyOverrideBox.Text = keyText;
                            input.UserKeyOverride = string.IsNullOrWhiteSpace(keyText) ? null : keyText;
                            valueOverrideBox.Text = GetInputResolvedValue(host, node, input);
                            UpdateConvertPreview();
                            host.RequestSyncDataPanels(immediate: false);
                        }
                    };
                    outputKeyCombo.LostFocus += (s, e) => host.RequestSyncDataPanels(immediate: true);

                    keyOverrideBox.LostFocus += (s, e) => host.RequestSyncDataPanels(immediate: true);
                    keyOverrideBox.TextChanged += (s, e) =>
                    {
                        var txt = (keyOverrideBox.Text ?? string.Empty).Trim();
                        input.UserKeyOverride = string.IsNullOrWhiteSpace(txt) ? null : txt;
                        // Khi user đổi key thủ công, cập nhật luôn value theo key mới
                        valueOverrideBox.Text = GetInputResolvedValue(host, node, input);
                        UpdateConvertPreview();
                        host.RequestSyncDataPanels(immediate: false);
                    };

                    // Flag để tránh recursive update khi sync từ property về textbox
                    var isSyncingFromProperty = false;

                    valueOverrideBox.TextChanged += (s, e) =>
                    {
                        // Bỏ qua nếu đang sync từ property để tránh loop
                        if (isSyncingFromProperty) return;

                        var txt = (valueOverrideBox.Text ?? string.Empty);
                        input.UserValueOverride = string.IsNullOrWhiteSpace(txt) ? null : txt;
                        UpdateConvertPreview();

                        // For KeyPressEventNode, HotkeyPressEventNode, and MouseEventNode: update RepeatCount
                        if (node is KeyPressEventNode keyNode)
                        {
                            if (int.TryParse(txt.Trim(), System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture, out var count) ||
                                int.TryParse(txt.Trim(), System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.CurrentCulture, out count))
                            {
                                var newValue = count < 1 ? 1 : count;
                                if (keyNode.RepeatCount != newValue)
                                {
                                    keyNode.RepeatCount = newValue;
                                }
                            }
                            else if (double.TryParse(txt.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var d) ||
                                double.TryParse(txt.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.CurrentCulture, out d))
                            {
                                var intVal = (int)Math.Round(d);
                                var newValue = intVal < 1 ? 1 : intVal;
                                if (keyNode.RepeatCount != newValue)
                                {
                                    keyNode.RepeatCount = newValue;
                                }
                            }
                        }
                        else if (node is HotkeyPressEventNode hotkeyNode)
                        {
                            if (int.TryParse(txt.Trim(), System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture, out var count) ||
                                int.TryParse(txt.Trim(), System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.CurrentCulture, out count))
                            {
                                var newValue = count < 1 ? 1 : count;
                                if (hotkeyNode.RepeatCount != newValue)
                                {
                                    hotkeyNode.RepeatCount = newValue;
                                }
                            }
                            else if (double.TryParse(txt.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var d) ||
                                double.TryParse(txt.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.CurrentCulture, out d))
                            {
                                var intVal = (int)Math.Round(d);
                                var newValue = intVal < 1 ? 1 : intVal;
                                if (hotkeyNode.RepeatCount != newValue)
                                {
                                    hotkeyNode.RepeatCount = newValue;
                                }
                            }
                        }
                        else if (node is MouseEventNode mouseNode)
                        {
                            if (int.TryParse(txt.Trim(), System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture, out var count) ||
                                int.TryParse(txt.Trim(), System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.CurrentCulture, out count))
                            {
                                var newValue = count < 1 ? 1 : count;
                                if (mouseNode.RepeatCount != newValue)
                                {
                                    mouseNode.RepeatCount = newValue;
                                }
                            }
                            else if (double.TryParse(txt.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var d) ||
                                double.TryParse(txt.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.CurrentCulture, out d))
                            {
                                var intVal = (int)Math.Round(d);
                                var newValue = intVal < 1 ? 1 : intVal;
                                if (mouseNode.RepeatCount != newValue)
                                {
                                    mouseNode.RepeatCount = newValue;
                                }
                            }
                        }
                        host.RequestSyncDataPanels(immediate: false);
                    };
                    valueOverrideBox.LostFocus += (s, e) => host.RequestSyncDataPanels(immediate: true);

                    // For KeyPressEventNode, HotkeyPressEventNode, and MouseEventNode: sync value textbox when RepeatCount changes
                    if (node is KeyPressEventNode keyNode2 && keyNode2 is System.ComponentModel.INotifyPropertyChanged npcKey)
                    {
                        npcKey.PropertyChanged += (s, e) =>
                        {
                            if (string.Equals(e.PropertyName, nameof(KeyPressEventNode.RepeatCount), StringComparison.OrdinalIgnoreCase))
                            {
                                var newText = keyNode2.RepeatCount.ToString();
                                if (valueOverrideBox.Text != newText)
                                {
                                    isSyncingFromProperty = true;
                                    try
                                    {
                                        valueOverrideBox.Text = newText;
                                        input.UserValueOverride = newText;
                                    }
                                    finally
                                    {
                                        isSyncingFromProperty = false;
                                    }
                                }
                            }
                        };
                    }
                    else if (node is HotkeyPressEventNode hotkeyNode2 && hotkeyNode2 is System.ComponentModel.INotifyPropertyChanged npcHotkey)
                    {
                        npcHotkey.PropertyChanged += (s, e) =>
                        {
                            if (string.Equals(e.PropertyName, nameof(HotkeyPressEventNode.RepeatCount), StringComparison.OrdinalIgnoreCase))
                            {
                                var newText = hotkeyNode2.RepeatCount.ToString();
                                if (valueOverrideBox.Text != newText)
                                {
                                    isSyncingFromProperty = true;
                                    try
                                    {
                                        valueOverrideBox.Text = newText;
                                        input.UserValueOverride = newText;
                                    }
                                    finally
                                    {
                                        isSyncingFromProperty = false;
                                    }
                                }
                            }
                        };
                    }
                    else if (node is MouseEventNode mouseNode2 && mouseNode2 is System.ComponentModel.INotifyPropertyChanged npcMouse)
                    {
                        npcMouse.PropertyChanged += (s, e) =>
                        {
                            if (string.Equals(e.PropertyName, nameof(MouseEventNode.RepeatCount), StringComparison.OrdinalIgnoreCase))
                            {
                                var newText = mouseNode2.RepeatCount.ToString();
                                if (valueOverrideBox.Text != newText)
                                {
                                    isSyncingFromProperty = true;
                                    try
                                    {
                                        valueOverrideBox.Text = newText;
                                        input.UserValueOverride = newText;
                                    }
                                    finally
                                    {
                                        isSyncingFromProperty = false;
                                    }
                                }
                            }
                        };
                    }

                    // Update key textbox when InputNode.Key changes (if source node is InputNode)
                    // if (host.ViewModel != null && !string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
                    // {
                    //     var srcNode = host.ViewModel.Nodes.FirstOrDefault(n => n.Id == input.SelectedSourceNodeId);
                    //     if (srcNode is InputNode inputNode && inputNode is System.ComponentModel.INotifyPropertyChanged npcInput)
                    //     {
                    //         // Check if output key is "Input" (or "value" for backward compatibility)
                    //         var outputKey = (input.SelectedSourceOutputKey ?? input.Key ?? string.Empty).Trim();
                    //         if (string.Equals(outputKey, "Input", StringComparison.OrdinalIgnoreCase) ||
                    //             string.Equals(outputKey, "value", StringComparison.OrdinalIgnoreCase))
                    //         {
                    //             npcInput.PropertyChanged += (s, e) =>
                    //             {
                    //                 if (string.Equals(e.PropertyName, nameof(InputNode.Key), StringComparison.OrdinalIgnoreCase))
                    //                 {
                    //                     // Update key textbox with new InputNode.Key value
                    //                     var newKeyText = inputNode.Key ?? string.Empty;
                    //                     if (keyOverrideBox.Text != newKeyText)
                    //                     {
                    //                         keyOverrideBox.Text = newKeyText;
                    //                         // Clear UserKeyOverride để đảm bảo luôn lấy từ InputNode.Key
                    //                         input.UserKeyOverride = string.IsNullOrWhiteSpace(newKeyText) ? null : newKeyText;
                    //                     }
                    //                 }
                    //             };
                    //         }
                    //     }
                    // }

                    typeCombo.SelectionChanged += (s, e) =>
                    {
                        if (typeCombo.SelectedItem is WorkflowDataType t)
                        {
                            input.ConvertType = t;
                            UpdateConvertPreview();
                        }
                    };

                    btnAdd.Click += (s, e) =>
                    {
                        var newKey = ("in_" + Guid.NewGuid().ToString("N")).Substring(0, 10);
                        var newInput = new WorkflowDynamicDataPort
                        {
                            Key = newKey,
                            DisplayName = "Data In",
                            IsMultiple = true
                        };

                        // Tự động điền giá trị từ source node đã kết nối (nếu có)
                        // Lấy giá trị từ input đầu tiên (nếu có) để làm mẫu
                        var firstInput = node.DynamicInputs.FirstOrDefault();
                        if (firstInput != null && host.ViewModel != null)
                        {
                            // Copy các giá trị từ input đầu tiên
                            newInput.SelectedSourceNodeId = firstInput.SelectedSourceNodeId;
                            newInput.SelectedSourceOutputKey = firstInput.SelectedSourceOutputKey;
                            newInput.ConvertType = firstInput.ConvertType;

                            // Nếu có source node, đảm bảo lấy giá trị mới nhất từ InputNode.DataType (nếu là InputNode)
                            if (!string.IsNullOrWhiteSpace(newInput.SelectedSourceNodeId))
                            {
                                var sourceNode = host.ViewModel.Nodes.FirstOrDefault(n => n.Id == newInput.SelectedSourceNodeId);
                                if (sourceNode is InputNode inputNode)
                                {
                                    // Lấy giá trị mới nhất từ InputNode.DataType (biến trung gian)
                                    newInput.ConvertType = inputNode.DataType;
                                }
                            }
                        }
                        else if (host.ViewModel != null)
                        {
                            // Nếu chưa có input nào, tìm source node từ connections
                            var connections = host.ViewModel.Connections
                                .Where(c => c.ToNode == node && c.FromNode != null)
                                .ToList();

                            if (connections.Count > 0)
                            {
                                var sourceNode = connections[0].FromNode;
                                newInput.SelectedSourceNodeId = sourceNode.Id;

                                if (sourceNode.DynamicOutputs != null && sourceNode.DynamicOutputs.Count > 0)
                                {
                                    var firstOutput = sourceNode.DynamicOutputs.FirstOrDefault();
                                    if (firstOutput != null)
                                    {
                                        newInput.SelectedSourceOutputKey = firstOutput.Key;

                                        // Nếu là InputNode, lấy trực tiếp từ InputNode.DataType (biến trung gian)
                                        if (sourceNode is InputNode inputNode)
                                        {
                                            newInput.ConvertType = inputNode.DataType;
                                        }
                                        else if (firstOutput.OutputType.HasValue)
                                        {
                                            newInput.ConvertType = firstOutput.OutputType.Value;
                                        }
                                    }
                                }
                            }
                        }

                        node.DynamicInputs.Add(newInput);

                        // DataPanel embedded không còn được sử dụng trong NodeChrome.
                        // Các thay đổi DynamicInputs sẽ được phản ánh qua dialog & DataPanel trong ViewModel.
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // Populate AvailableSources cho input mới (tương tự RefreshDynamicDataSourceSelectors)
                            if (host.ViewModel != null)
                            {
                                var connections = host.ViewModel.Connections
                                    .Where(c => c.ToNode == node && c.FromNode != null)
                                    .ToList();

                                if (connections.Count > 0)
                                {
                                    // Tìm các producer nodes (nodes có DynamicOutputs)
                                    var producerNodes = connections
                                        .Select(c => c.FromNode)
                                        .Where(n => n != null && n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                                        .Distinct()
                                        .ToList();

                                    var options = producerNodes
                                        .Select(BaseNodeDialogViewModel.CreateDataSourceOption)
                                        .ToList();

                                    newInput.AvailableSources = options;

                                    // Update combobox source nếu đã được tạo
                                    if (newInput.SourceSelectorUI != null)
                                    {
                                        newInput.SourceSelectorUI.ItemsSource = options;
                                        newInput.SourceSelectorUI.Visibility = options.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                                        if (!string.IsNullOrWhiteSpace(newInput.SelectedSourceNodeId))
                                        {
                                            newInput.SourceSelectorUI.SelectedValue = newInput.SelectedSourceNodeId;
                                        }
                                    }
                                }
                            }

                            // Refresh output key options cho input mới
                            // RefreshOutputKeyOptions(host, newInput);

                            // Update UI comboboxes nếu đã được tạo
                            if (newInput.SourceSelectorUI != null && !string.IsNullOrWhiteSpace(newInput.SelectedSourceNodeId))
                            {
                                newInput.SourceSelectorUI.SelectedValue = newInput.SelectedSourceNodeId;
                            }

                            if (newInput.OutputKeySelectorUI != null && !string.IsNullOrWhiteSpace(newInput.SelectedSourceOutputKey))
                            {
                                newInput.OutputKeySelectorUI.SelectedValue = newInput.SelectedSourceOutputKey;
                                var selectedOption = newInput.AvailableOutputKeyOptions?.FirstOrDefault(opt => opt.Key == newInput.SelectedSourceOutputKey);
                                if (selectedOption != null)
                                {
                                    newInput.OutputKeySelectorUI.SelectedItem = selectedOption;
                                }
                            }

                            if (newInput.ConvertTypeSelectorUI != null)
                            {
                                newInput.ConvertTypeSelectorUI.SelectedItem = newInput.ConvertType;
                            }

                            // Update textboxes
                            if (newInput.UserKeyTextBoxUI != null && host.ViewModel != null && !string.IsNullOrWhiteSpace(newInput.SelectedSourceNodeId))
                            {
                                var srcNode = host.ViewModel.Nodes.FirstOrDefault(n => n.Id == newInput.SelectedSourceNodeId);
                                if (srcNode is InputNode inputNode && !string.IsNullOrWhiteSpace(inputNode.Key))
                                {
                                    newInput.UserKeyTextBoxUI.Text = inputNode.Key;
                                }
                                else
                                {
                                    newInput.UserKeyTextBoxUI.Text = newInput.SelectedSourceOutputKey ?? string.Empty;
                                }
                            }

                            if (newInput.UserValueTextBoxUI != null)
                            {
                                var resolvedValue = GetInputResolvedValue(host, node, newInput);
                                newInput.UserValueTextBoxUI.Text = resolvedValue;
                            }
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    };

                    input.SourceSelectorUI = combo;
                    input.OutputKeySelectorUI = outputKeyCombo;
                    input.UserKeyTextBoxUI = keyOverrideBox;
                    input.UserValueTextBoxUI = valueOverrideBox;
                    input.ConvertTypeSelectorUI = typeCombo;
                    input.ConvertedValueTextUI = convertedText;
                    input.ConvertErrorTextUI = errorText;

                    UpdateConvertPreview();

                    Grid.SetColumn(keyOverrideBox, 0);
                    Grid.SetColumn(valueOverrideBox, 1);
                    Grid.SetColumn(typeCombo, 2);
                    line3.Children.Add(keyOverrideBox);
                    line3.Children.Add(valueOverrideBox);
                    line3.Children.Add(typeCombo);

                    item.Children.Add(line1);
                    item.Children.Add(line2);
                    item.Children.Add(line3);
                    item.Children.Add(convertInfoPanel);

                    inputsContent.Children.Add(item);
                }

                stack.Children.Add(inputsContent);
            }

            if (node.DynamicOutputs.Any())
            {
                // Section header: Outputs + collapse toggle (right) + Add button
                var outputsContent = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
                var outputsHeader = new Grid { Margin = new Thickness(0, node.DynamicInputs.Any() ? 8 : 0, 0, 6) };
                outputsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outputsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Toggle button
                outputsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Add button

                var outputsTitle = new TextBlock
                {
                    Text = "Outputs",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = textBrush,
                    Opacity = 0.9,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var outputsToggle = new ToggleButton
                {
                    Width = 18,
                    Height = 18,
                    Content = "▾",
                    IsChecked = true,
                    ToolTip = "Đóng/mở Outputs",
                    Background = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)),
                    Foreground = Brushes.White,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Template = BuildRoundedToggleTemplate()
                };

                outputsToggle.Checked += (s, e) =>
                {
                    outputsContent.Visibility = Visibility.Visible;
                    outputsToggle.Content = "▾";
                };
                outputsToggle.Unchecked += (s, e) =>
                {
                    outputsContent.Visibility = Visibility.Collapsed;
                    outputsToggle.Content = "▸";
                };

                #region thêm output

                // Button + để thêm output mới (cạnh toggle Outputs)
                var btnAddOutput = new Button
                {
                    MinWidth = 28,
                    Height = 18,
                    Padding = new Thickness(0),
                    Content = new TextBlock
                    {
                        Text = "+",
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    ToolTip = "Thêm một Output mới",
                    Background = Application.Current.TryFindResource("PrimaryBrush") as Brush ?? new SolidColorBrush(Color.FromArgb(120, 0, 150, 0)),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                btnAddOutput.Click += (s, e) =>
                {
                    // Thêm output mới (đánh dấu là user-added)
                    var newOutput = new WorkflowDynamicDataPort
                    {
                        Key = $"output_{node.DynamicOutputs.Count + 1}",
                        DisplayName = $"Output {node.DynamicOutputs.Count + 1}",
                        OutputType = WorkflowDataType.String,
                        ConvertType = WorkflowDataType.String,
                        IsUserAdded = true // Đánh dấu là output do user thêm
                    };
                    node.DynamicOutputs.Add(newOutput);

                    // Rebuild outputs UI (có cập nhật disable/enable nút + theo MaxOutputsCount)
                    RebuildOutputsUI(node, outputsContent, textBrush, host, btnAddOutput, customizationOptions);

                    // Refresh dynamic source selectors ngay lập tức để cập nhật combobox key của các node kết nối
                    // Giống như InputNode.PropertyChanged trigger refresh
                    if (host != null && host.ViewModel != null)
                    {
                        var eventService = host.ViewModel.GetType()
                            .GetProperty("EventService")?.GetValue(host.ViewModel)
                            as FlowMy.Services.Interaction.WorkflowEditorEventService;

                        // Gọi trực tiếp nếu đang ở UI thread, không delay
                        if (Application.Current?.Dispatcher != null && Application.Current.Dispatcher.CheckAccess())
                        {
                            eventService?.RefreshDynamicDataSourceSelectors();
                        }
                        else
                        {
                            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                eventService?.RefreshDynamicDataSourceSelectors();
                            }), System.Windows.Threading.DispatcherPriority.Normal);
                        }
                    }
                };

                // Ẩn/hiện và disable/enable nút "+" theo customizationOptions (và MaxOutputsCount)
                UpdateAddOutputButtonState(btnAddOutput, node, customizationOptions);

                #endregion

                Grid.SetColumn(outputsTitle, 0);
                Grid.SetColumn(outputsToggle, 1);
                Grid.SetColumn(btnAddOutput, 2);
                outputsHeader.Children.Add(outputsTitle);
                outputsHeader.Children.Add(outputsToggle);
                outputsHeader.Children.Add(btnAddOutput);

                stack.Children.Add(outputsHeader);

                // Build outputs UI
                RebuildOutputsUI(node, outputsContent, textBrush, host, btnAddOutput, customizationOptions);

                stack.Children.Add(outputsContent);

                // Đồng bộ Outputs thời gian thực: khi dữ liệu node thay đổi (ScreenCapture/ScreenPosition/InputNode/...) cập nhật Outputs UI
                if (node is System.ComponentModel.INotifyPropertyChanged npc)
                {
                    npc.PropertyChanged += (s, e) =>
                    {
                        if (node.DynamicOutputs == null || node.DynamicOutputs.Count == 0) return;
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // Rebuild toàn bộ Outputs UI để bắt kịp thay đổi type (String <-> Array, ...)
                            RebuildOutputsUI(node, outputsContent, textBrush, host, btnAddOutput, customizationOptions);
                        }), DispatcherPriority.Normal);
                    };
                }
            }

            container.Child = stack;

            return container;
        }

        private static void RebuildOutputsUI(WorkflowNode node, StackPanel outputsContent, Brush textBrush, IWorkflowEditorHost host, Button? btnAddOutput = null, DataPanelCustomizationOptions? customizationOptions = null)
        {
            outputsContent.Children.Clear();

            for (int i = 0; i < node.DynamicOutputs.Count; i++)
            {
                var output = node.DynamicOutputs[i];
                var index = i;

                // Nếu là output mặc định (không phải user-added), hiển thị dạng text như cũ
                if (!output.IsUserAdded)
                {
                    var view = BuildDefaultOutputPreviewUI(node, output, textBrush, host);
                    outputsContent.Children.Add(view);
                    continue;
                }

                // Nếu là output do user thêm, hiển thị UI để nhập key/value/type
                // Mỗi output item: 2 lines
                var item = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

                // Line 1: Key | Value | Type labels
                var line1 = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                line1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons

                TextBlock MakeSmallLabel(string t) => new TextBlock
                {
                    Text = t,
                    Foreground = textBrush,
                    Opacity = 0.75,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 6, 0)
                };

                var keyLabel = MakeSmallLabel("Key");
                var valueLabel = MakeSmallLabel("Value");
                var typeLabel = MakeSmallLabel("Type");
                Grid.SetColumn(keyLabel, 0);
                Grid.SetColumn(valueLabel, 1);
                Grid.SetColumn(typeLabel, 2);
                line1.Children.Add(keyLabel);
                line1.Children.Add(valueLabel);
                line1.Children.Add(typeLabel);

                // Line 2: Key TextBox | Value TextBox | Type ComboBox | Buttons
                var line2 = new Grid { Margin = new Thickness(0, 0, 0, 0) };
                line2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons

                // Key TextBox
                var keyTextBox = new TextBox
                {
                    MinHeight = 26,
                    FontSize = 12,
                    Style = Application.Current.TryFindResource("InputTextBoxV2") as Style,
                    Padding = new Thickness(2, 2, 2, 0),
                    Margin = new Thickness(0, 0, 6, 0),
                    Text = output.Key ?? string.Empty
                };
                keyTextBox.LostFocus += (s, e) => host.RequestSyncDataPanels(immediate: true);
                keyTextBox.TextChanged += (s, e) =>
                {
                    output.Key = keyTextBox.Text ?? string.Empty;
                    host.RequestSyncDataPanels(immediate: false);
                };
                output.UserKeyTextBoxUI = keyTextBox;

                // Value TextBox với đồng bộ thời gian thực từ node khác
                var valueTextBox = new TextBox
                {
                    MinHeight = 26,
                    FontSize = 12,
                    Style = Application.Current.TryFindResource("InputTextBoxV2") as Style,
                    Margin = new Thickness(0, 0, 6, 0),
                    Padding = new Thickness(2, 2, 2, 0),
                    Text = output.UserValueOverride ?? string.Empty
                };

                // Function để lấy giá trị từ node khác (giống inputs)
                string GetOutputValueFromOtherNodes()
                {
                    if (host?.ViewModel == null) return string.Empty;

                    // Tìm các node kết nối đến node hiện tại (upstream nodes)
                    var connections = host.ViewModel.Connections
                        .Where(c => c.ToNode == node && c.FromNode != null)
                        .ToList();

                    if (connections.Count == 0) return string.Empty;

                    // Lấy giá trị từ node đầu tiên (hoặc có thể chọn node cụ thể)
                    var sourceNode = connections[0].FromNode;
                    if (sourceNode == null) return string.Empty;

                    // Lấy giá trị từ output của source node với key tương ứng
                    var key = (output.Key ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(key)) return string.Empty;

                    // Tìm output trong source node với key tương ứng
                    if (sourceNode.DynamicOutputs != null)
                    {
                        var sourceOutput = sourceNode.DynamicOutputs.FirstOrDefault(o =>
                            string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));

                        if (sourceOutput != null)
                        {
                            // Ưu tiên UserValueOverride nếu có
                            if (!string.IsNullOrWhiteSpace(sourceOutput.UserValueOverride))
                            {
                                return sourceOutput.UserValueOverride;
                            }
                        }
                    }

                    // Fallback: dùng ResolveDynamicValueByKey
                    return ResolveDynamicValueByKey(sourceNode, key);
                }

                // DispatcherTimer để polling mỗi 500ms
                //var syncTimer = new System.Windows.Threading.DispatcherTimer
                //{
                //    Interval = TimeSpan.FromMilliseconds(500)
                //};

                //syncTimer.Tick += (s, e) =>
                //{
                //    // Chỉ sync nếu UserValueOverride rỗng (user chưa nhập thủ công)
                //    if (string.IsNullOrWhiteSpace(output.UserValueOverride))
                //    {
                //        var syncedValue = GetOutputValueFromOtherNodes();
                //        if (!string.IsNullOrWhiteSpace(syncedValue) && valueTextBox.Text != syncedValue)
                //        {
                //            // Set flag để tránh trigger TextChanged khi sync
                //            output.IsSyncingValue = true;
                //            try
                //            {
                //                valueTextBox.Text = syncedValue;
                //                output.UserValueOverride = syncedValue;
                //            }
                //            finally
                //            {
                //                output.IsSyncingValue = false;
                //            }
                //        }
                //    }
                //};

                //syncTimer.Start();

                // Lưu timer reference để có thể dừng khi cần
                // (có thể lưu vào output hoặc node để cleanup sau)

                valueTextBox.LostFocus += (s, e) => host.RequestSyncDataPanels(immediate: true);
                valueTextBox.TextChanged += (s, e) =>
                {
                    // Bỏ qua nếu đang sync từ output khác để tránh loop
                    if (output.IsSyncingValue) return;

                    output.UserValueOverride = valueTextBox.Text;
                    host.RequestSyncDataPanels(immediate: false);
                };

                // Khởi tạo giá trị ban đầu
                if (string.IsNullOrWhiteSpace(output.UserValueOverride))
                {
                    var initialValue = GetOutputValueFromOtherNodes();
                    if (!string.IsNullOrWhiteSpace(initialValue))
                    {
                        output.IsSyncingValue = true;
                        try
                        {
                            valueTextBox.Text = initialValue;
                            output.UserValueOverride = initialValue;
                        }
                        finally
                        {
                            output.IsSyncingValue = false;
                        }
                    }
                }

                output.UserValueTextBoxUI = valueTextBox;

                // Type ComboBox
                var allowedTypes = Enum.GetValues(typeof(WorkflowDataType)).Cast<WorkflowDataType>().ToList();
                var typeCombo = new ComboBox
                {
                    Height = 26,
                    MinWidth = 90,
                    Margin = new Thickness(0, 0, 6, 0),
                    Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                    ItemsSource = allowedTypes,
                    SelectedItem = output.OutputType ?? output.ConvertType
                };
                typeCombo.LostFocus += (s, e) => host.RequestSyncDataPanels(immediate: true);
                typeCombo.SelectionChanged += (s, e) =>
                {
                    if (typeCombo.SelectedItem is WorkflowDataType selectedType)
                    {
                        output.OutputType = selectedType;
                        output.ConvertType = selectedType;
                        host.RequestSyncDataPanels(immediate: false);
                    }
                };
                output.ConvertTypeSelectorUI = typeCombo;

                // Buttons panel
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                // Button + (luôn có)
                var btnAdd = new Button
                {
                    Content = "+",
                    Width = 24,
                    Height = 24,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(2, 0, 2, 0),
                    Padding = new Thickness(0),
                    Background = Application.Current.TryFindResource("PrimaryBrush") as Brush ?? new SolidColorBrush(Color.FromArgb(120, 0, 150, 0)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                btnAdd.Click += (s, e) =>
                {
                    // Thêm output mới sau output hiện tại
                    var newOutput = new WorkflowDynamicDataPort
                    {
                        Key = $"output_{node.DynamicOutputs.Count + 1}",
                        DisplayName = $"Output {node.DynamicOutputs.Count + 1}",
                        OutputType = WorkflowDataType.String,
                        ConvertType = WorkflowDataType.String,
                        IsUserAdded = true
                    };
                    node.DynamicOutputs.Insert(index + 1, newOutput);
                    RebuildOutputsUI(node, outputsContent, textBrush, host, btnAddOutput, customizationOptions);
                    host.RequestSyncDataPanels(immediate: true);
                };

                // Button - (luôn có cho user-added outputs)
                var btnRemove = new Button
                {
                    Content = "-",
                    Width = 24,
                    Height = 24,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(2, 0, 2, 0),
                    Padding = new Thickness(0),
                    Background = new SolidColorBrush(Color.FromArgb(120, 150, 0, 0)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                btnRemove.Click += (s, e) =>
                {
                    // Xóa output
                    if (node.DynamicOutputs.Count > index)
                    {
                        node.DynamicOutputs.RemoveAt(index);
                        RebuildOutputsUI(node, outputsContent, textBrush, host, btnAddOutput, customizationOptions);
                        host.RequestSyncDataPanels(immediate: true);
                    }
                };

                buttonPanel.Children.Add(btnAdd);
                buttonPanel.Children.Add(btnRemove); // Luôn thêm button - cho user-added outputs

                Grid.SetColumn(keyTextBox, 0);
                Grid.SetColumn(valueTextBox, 1);
                Grid.SetColumn(typeCombo, 2);
                Grid.SetColumn(buttonPanel, 3);
                line2.Children.Add(keyTextBox);
                line2.Children.Add(valueTextBox);
                line2.Children.Add(typeCombo);
                line2.Children.Add(buttonPanel);

                item.Children.Add(line1);
                item.Children.Add(line2);
                outputsContent.Children.Add(item);
            }

            // Cập nhật ẩn/hiện và disable/enable nút "+" (header) sau mỗi lần rebuild
            UpdateAddOutputButtonState(btnAddOutput, node, customizationOptions);
        }

        private static FrameworkElement BuildDefaultOutputPreviewUI(WorkflowNode node, WorkflowDynamicDataPort output, Brush textBrush, IWorkflowEditorHost host)
        {
            // Reset refs (rebuild)
            output.OutputValueTextUI = null;
            output.ArrayPreviewToggleUI = null;
            output.ArrayPreviewItemsContainerUI = null;

            var resolved = GetOutputResolvedValue(node, output);
            var label = GetPortLabel(output);

            // Array output preview
            if (IsArrayWorkflowType(output.OutputType) && TryParseJsonArrayItems(resolved, out var items))
            {
                var root = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };

                var toggle = new ToggleButton
                {
                    IsChecked = output.IsArrayPreviewExpanded,
                    Foreground = Brushes.White,
                    Opacity = 0.95,
                    FontSize = 12,
                    Width = 200,
                    Height = 35,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 2, 0, 4),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(10, 6, 10, 6),
                    Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Template = BuildRoundedToggleTemplate()
                };

                var list = new StackPanel
                {
                    Visibility = output.IsArrayPreviewExpanded ? Visibility.Visible : Visibility.Collapsed,
                    Margin = new Thickness(18, 0, 0, 0)
                };

                toggle.Checked += (s, e) =>
                {
                    output.IsArrayPreviewExpanded = true;
                    list.Visibility = Visibility.Visible;
                    RefreshDefaultOutputPreviewUI(node, output);
                    host.RequestSyncDataPanels(immediate: false);
                };
                toggle.Unchecked += (s, e) =>
                {
                    output.IsArrayPreviewExpanded = false;
                    list.Visibility = Visibility.Collapsed;
                    RefreshDefaultOutputPreviewUI(node, output);
                    host.RequestSyncDataPanels(immediate: false);
                };

                output.ArrayPreviewToggleUI = toggle;
                output.ArrayPreviewItemsContainerUI = list;

                root.Children.Add(toggle);
                root.Children.Add(list);

                // Khởi tạo nội dung ban đầu theo cùng một logic với refresh runtime
                RefreshDefaultOutputPreviewUI(node, output);
                return root;
            }

            // Default (single line)
            var text = NormalizeForMultilineAndTruncate("- " + label + ": " + resolved, 200);
            var outputText = new TextBlock
            {
                Text = text,
                Foreground = textBrush,
                Opacity = 0.85,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            };
            output.OutputValueTextUI = outputText;
            return outputText;
        }

        private static bool IsArrayWorkflowType(WorkflowDataType? t)
        {
            return t == WorkflowDataType.ArrayString ||
                   t == WorkflowDataType.ArrayNumber ||
                   t == WorkflowDataType.ArrayDynamic;
        }

        private static bool TryParseJsonArrayItems(string raw, out List<string> items)
        {
            items = new List<string>();
            if (string.IsNullOrWhiteSpace(raw) || raw == "—") return false;

            // Accept only JSON arrays: ["a","b"] or [1,2]
            var s = raw.Trim();
            if (!s.StartsWith("[") || !s.EndsWith("]")) return false;

            try
            {
                using var doc = JsonDocument.Parse(s);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String) items.Add(el.GetString() ?? string.Empty);
                    else items.Add(el.ToString());
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void RefreshDefaultOutputPreviewUI(WorkflowNode node, WorkflowDynamicDataPort output)
        {
            // Called by refresh loops (NodeChrome and WorkflowEditorEventService)
            var resolved = GetOutputResolvedValue(node, output);
            var label = GetPortLabel(output);

            // Array preview update - cần cập nhật toggle content ngay cả khi giá trị resolved không đổi
            // (vì IsArrayPreviewExpanded có thể thay đổi khi user click toggle)
            if (output.ArrayPreviewToggleUI != null && output.ArrayPreviewItemsContainerUI != null &&
                IsArrayWorkflowType(output.OutputType) && TryParseJsonArrayItems(resolved, out var items))
            {
                var toggle = output.ArrayPreviewToggleUI;
                var list = output.ArrayPreviewItemsContainerUI;

                var verb = output.IsArrayPreviewExpanded ? "🔼 Ẩn" : "🔽 Hiện";
                toggle.Content = $"- {label}: {verb} {items.Count} item";

                // Chỉ rebuild list items nếu giá trị resolved thay đổi hoặc list đang rỗng
                if (output.LastResolvedOutputValue != resolved || list.Children.Count == 0)
                {
                    list.Children.Clear();
                    for (int i = 0; i < items.Count; i++)
                    {
                        list.Children.Add(new TextBlock
                        {
                            Text = $"[{i}]: {items[i]}",
                            Foreground = toggle.Foreground,
                            Opacity = 0.85,
                            Margin = new Thickness(0, 0, 0, 2),
                            TextWrapping = TextWrapping.Wrap
                        });
                    }
                }

                list.Visibility = output.IsArrayPreviewExpanded ? Visibility.Visible : Visibility.Collapsed;

                // Cập nhật LastResolvedOutputValue sau khi đã xử lý array preview
                output.LastResolvedOutputValue = resolved;
                return;
            }

            // Nếu giá trị không đổi so với lần refresh trước thì bỏ qua để giảm CPU/RAM
            // (chỉ áp dụng cho default single-line, không áp dụng cho array preview)
            if (output.LastResolvedOutputValue == resolved)
            {
                return;
            }

            output.LastResolvedOutputValue = resolved;

            // Default single-line update
            if (output.OutputValueTextUI != null)
            {
                var line = "- " + label + ": " + resolved;
                var newText = NormalizeForMultilineAndTruncate(line, 200);
                if (output.OutputValueTextUI.Text != newText)
                    output.OutputValueTextUI.Text = newText;
            }
        }

        /// <summary>
        /// Áp dụng ẩn/hiện và disable/enable cho nút "+" thêm output (cạnh toggle Outputs) theo customizationOptions.
        /// </summary>
        private static void UpdateAddOutputButtonState(Button? btnAddOutput, WorkflowNode node, DataPanelCustomizationOptions? options)
        {
            if (btnAddOutput == null) return;

            if (options?.HideAddOutputButton == true)
            {
                btnAddOutput.Visibility = Visibility.Collapsed;
                btnAddOutput.IsEnabled = false;
                return;
            }

            btnAddOutput.Visibility = Visibility.Visible;

            if (options?.DisableAddOutputButton == true)
            {
                btnAddOutput.IsEnabled = false;
                return;
            }

            if (options?.MaxOutputsCount is int max && (node.DynamicOutputs?.Count ?? 0) >= max)
            {
                btnAddOutput.IsEnabled = false;
                return;
            }

            btnAddOutput.IsEnabled = true;
        }

        internal static string GetPortLabel(WorkflowDynamicDataPort port)
        {
            var key = port.Key ?? string.Empty;
            var name = port.DisplayName ?? string.Empty;

            // UX: nếu display name là "Data In"/"Data Out" (template default) thì bỏ, chỉ dùng key cho gọn.
            if (string.Equals(name, "Data In", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Data Out", StringComparison.OrdinalIgnoreCase))
            {
                name = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(name)) return key;
            if (string.IsNullOrWhiteSpace(key)) return name;
            return string.Equals(name, key, StringComparison.Ordinal) ? name : $"{name} ({key})";
        }

        private static string GetResolvedSourceTitle(WorkflowDynamicDataPort input)
        {
            var options = input.AvailableSources ?? new System.Collections.Generic.List<WorkflowDataSourceOption>();
            if (options.Count == 0) return "—";

            if (!string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
            {
                var picked = options.FirstOrDefault(o => o.NodeId == input.SelectedSourceNodeId);
                if (picked != null) return picked.ToString();
            }

            return options[0].ToString();
        }

        internal static string GetInputResolvedValue(IWorkflowEditorHost host, WorkflowNode node, WorkflowDynamicDataPort input)
        {
            // Dùng ResolveInputValueUpstream để chỉ lấy từ upstream (trái sang phải)
            return NodeDataPanelService.ResolveInputValueUpstream(host, node, input);
        }

        private static bool TryConvertValue(string raw, WorkflowDataType type, out string converted, out string? error)
        {
            converted = string.Empty;
            error = null;

            try
            {
                switch (type)
                {
                    case WorkflowDataType.String:
                        converted = raw;
                        return true;

                    case WorkflowDataType.Integer:
                        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ||
                            long.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out l))
                        {
                            converted = l.ToString(CultureInfo.InvariantCulture);
                            return true;
                        }
                        error = "Giá trị không hợp lệ cho Integer";
                        return false;

                    case WorkflowDataType.Number:
                        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) ||
                            double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out d))
                        {
                            converted = d.ToString(CultureInfo.InvariantCulture);
                            return true;
                        }
                        error = "Giá trị không hợp lệ cho Number";
                        return false;

                    case WorkflowDataType.Boolean:
                        {
                            var s = raw.Trim();
                            if (bool.TryParse(s, out var b))
                            {
                                converted = b ? "true" : "false";
                                return true;
                            }

                            if (string.Equals(s, "1", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "y", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "on", StringComparison.OrdinalIgnoreCase))
                            {
                                converted = "true";
                                return true;
                            }
                            if (string.Equals(s, "0", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "no", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "n", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "off", StringComparison.OrdinalIgnoreCase))
                            {
                                converted = "false";
                                return true;
                            }

                            error = "Giá trị không hợp lệ cho Boolean (true/false/1/0/yes/no)";
                            return false;
                        }

                    default:
                        converted = raw;
                        return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static string NormalizeForMultilineAndTruncate(string text, int maxChars)
        {
            if (text == null) return string.Empty;
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            if (normalized.Length <= maxChars) return normalized;
            return normalized.Substring(0, maxChars) + "...";
        }

        private static ControlTemplate BuildRoundedButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
            borderFactory.AppendChild(presenter);

            template.VisualTree = borderFactory;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(170, 0, 0, 0))));
            template.Triggers.Add(hover);

            return template;
        }

        private static ControlTemplate BuildRoundedToggleTemplate()
        {
            var template = new ControlTemplate(typeof(ToggleButton));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ToggleButton.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(ToggleButton.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(ToggleButton.BorderThicknessProperty));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ToggleButton.ContentProperty));
            borderFactory.AppendChild(presenter);

            template.VisualTree = borderFactory;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(ToggleButton.BackgroundProperty, new SolidColorBrush(Color.FromArgb(170, 0, 0, 0))));
            template.Triggers.Add(hover);

            var check = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            check.Setters.Add(new Setter(ToggleButton.BackgroundProperty, new SolidColorBrush(Color.FromArgb(210, 0, 0, 0))));
            template.Triggers.Add(check);

            return template;
        }

        internal static void RefreshOutputKeyOptions(IWorkflowEditorHost host, WorkflowDynamicDataPort input)
        {
            input.AvailableOutputKeys = new System.Collections.Generic.List<string>();
            input.AvailableOutputKeyOptions = new System.Collections.Generic.List<WorkflowOutputKeyOption>();

            if (host.ViewModel == null) return;
            if (string.IsNullOrWhiteSpace(input.SelectedSourceNodeId)) return;

            var src = host.ViewModel.Nodes.FirstOrDefault(n => n.Id == input.SelectedSourceNodeId);
            if (src?.DynamicOutputs == null) return;

            // Tạo list với type metadata
            var keyOptions = src.DynamicOutputs
                .Where(o => !string.IsNullOrWhiteSpace(o.Key))
                .Select(o => new WorkflowOutputKeyOption
                {
                    Key = o.Key.Trim(),
                    Type = o.OutputType
                })
                .GroupBy(opt => opt.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()) // Lấy option đầu tiên nếu có duplicate key
                .ToList();

            input.AvailableOutputKeyOptions = keyOptions;
            input.AvailableOutputKeys = keyOptions.Select(opt => opt.Key).ToList();

            // keep selection if possible; otherwise prefer input.Key; else first
            if (!string.IsNullOrWhiteSpace(input.SelectedSourceOutputKey) &&
                keyOptions.Any(k => string.Equals(k.Key, input.SelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)))
            {
                input.SelectedSourceOutputKey = keyOptions.First(k => string.Equals(k.Key, input.SelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)).Key;
                return;
            }

            var prefer = (input.Key ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(prefer) &&
                keyOptions.Any(k => string.Equals(k.Key, prefer, StringComparison.OrdinalIgnoreCase)))
            {
                input.SelectedSourceOutputKey = keyOptions.First(k => string.Equals(k.Key, prefer, StringComparison.OrdinalIgnoreCase)).Key;
                return;
            }

            input.SelectedSourceOutputKey = keyOptions.FirstOrDefault()?.Key;
        }

        private static string GetResolvedOutputKey(WorkflowDynamicDataPort input)
        {
            var key = (input.SelectedSourceOutputKey ?? input.Key ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(key) ? "—" : key;
        }

        internal static string GetOutputResolvedValue(WorkflowNode node, WorkflowDynamicDataPort output)
        {
            var key = (output.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key)) return "—";
            return ResolveDynamicValueByKey(node, key);
        }

        /// <summary>Giá trị resolved từ node theo key (dùng cho đồng bộ Outputs từ upstream).</summary>
        internal static string GetResolvedValueByKey(WorkflowNode node, string key)
        {
            var k = (key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(k)) return "—";
            return ResolveDynamicValueByKey(node, k);
        }

        internal static void UpdateExecutionResultsToggleText(ToggleButton toggle, int count, bool isExpanded)
        {
            var arrow = isExpanded ? "▾" : "▸";
            toggle.Content = $"{arrow} Có {count} kết quả trả về";
        }

        private static string SummarizeNodeDynamicOutputs(WorkflowNode node)
        {
            if (node.DynamicOutputs == null || node.DynamicOutputs.Count == 0) return "—";
            if (node.DynamicOutputs.Count == 1) return NodeDataPanelService.ResolveDynamicValueByKey(node, node.DynamicOutputs[0].Key);
            return $"[{node.DynamicOutputs.Count} outputs]";
        }

        private static string ResolveDynamicValueByKey(WorkflowNode node, string key)
        {
            // Delegate to NodeDataPanelService
            return NodeDataPanelService.ResolveDynamicValueByKey(node, key);
        }


        private static void EnsureChromeSizeSnapshot(Border border, WorkflowNode node)
        {
            // Kích thước snapshot cho DataPanel đã không còn dùng,
            // method giữ lại để tương thích chữ ký nhưng không làm gì.
        }

        private static void ApplyExpandedDataPanelSize(Border border, WorkflowNode node)
        {
            if (!node.SupportsDynamicData) return;

            EnsureChromeSizeSnapshot(border, node);

            // Width
            // DataPanel đã bỏ, không còn auto-expand theo panel,
            // giữ nguyên kích thước hiện tại của border.
        }

        private static void RestoreCollapsedDataPanelSize(Border border, WorkflowNode node)
        {
            // DataPanel đã bỏ, không cần restore kích thước.
        }

        private static double GetBaselineWidth(Border border)
        {
            if (!double.IsNaN(border.Width) && border.Width > 0) return border.Width;
            if (border.ActualWidth > 0) return border.ActualWidth;
            if (border.MinWidth > 0) return border.MinWidth;
            return 240;
        }

        private static double GetBaselineHeight(Border border)
        {
            if (!double.IsNaN(border.Height) && border.Height > 0) return border.Height;
            if (border.ActualHeight > 0) return border.ActualHeight;
            if (border.MinHeight > 0) return border.MinHeight;
            return 140;
        }

        private static double CalculateExpandedExtraHeight(WorkflowNode node)
        {
            var inCount = node.DynamicInputs?.Count ?? 0;
            var outCount = node.DynamicOutputs?.Count ?? 0;

            double extra = 0;

            if (inCount > 0)
            {
                extra += 24;          // "Inputs" header
                extra += inCount * 32; // each input row
            }

            if (outCount > 0)
            {
                extra += 24;           // "Outputs" header
                extra += outCount * 22; // each output line
            }

            extra += 18; // padding / margins
            return Math.Max(ExpandedMinHeightExtra, extra);
        }

        internal static Brush GetNodeTextBrush(WorkflowNode node)
        {
            // Ưu tiên: TextOn{ColorKey}Brush (đúng theme)
            if (!string.IsNullOrWhiteSpace(node.ColorKey))
            {
                var themed = Application.Current.TryFindResource($"TextOn{node.ColorKey}Brush") as Brush;
                if (themed != null) return themed;
            }

            // Fallback: tự chọn đen/trắng theo độ sáng của NodeBrush
            var c = GetColorFromBrush(node.NodeBrush);
            double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return luminance > 0.5
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.White);
        }

        private static Color GetColorFromBrush(Brush brush)
        {
            if (brush is SolidColorBrush solid) return solid.Color;
            if (brush is LinearGradientBrush linear && linear.GradientStops.Count > 0) return linear.GradientStops[0].Color;
            if (brush is RadialGradientBrush radial && radial.GradientStops.Count > 0) return radial.GradientStops[0].Color;
            return Colors.Gray;
        }

        private static Button CreateMiniIconButton(string glyph, string tooltip, Action onClick)
        {
            var btn = new Button
            {
                Width = 16,
                Height = 16,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 2, 2, 0),
                Content = new TextBlock
                {
                    Text = glyph,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                ToolTip = tooltip,
                Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // ✅ Bo góc bằng Border trong ControlTemplate
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
            borderFactory.AppendChild(presenter);

            template.VisualTree = borderFactory;

            // Hover nhẹ cho đẹp
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(170, 0, 0, 0))));
            template.Triggers.Add(hoverTrigger);

            btn.Template = template;

            btn.Click += (s, e) => onClick();
            return btn;
        }

        private static void AttachSizeChangedSync(Border border, WorkflowNode node, IWorkflowEditorHost host)
        {
            border.SizeChanged += (s, e) =>
            {
                // During zoom, throttle expensive updates (ports + connections)
                if (_isZooming)
                {
                    if (!_sizeChangedTimers.TryGetValue(border, out var timer))
                    {
                        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SizeChangedThrottleMs) };
                        timer.Tick += (s2, e2) =>
                        {
                            timer.Stop();
                            UpdatePortsAndConnections();
                        };
                        _sizeChangedTimers[border] = timer;
                    }

                    if (!timer.IsEnabled)
                    {
                        timer.Start();
                    }
                }
                else
                {
                    // Not zooming: update immediately
                    UpdatePortsAndConnections();
                }

                void UpdatePortsAndConnections()
                {
                    // ⚠️ Skip LoopNode - it has custom diamond shape port positioning
                    if (node is LoopNode)
                    {
                        // LoopNode ports are updated in LoopNodeRenderer.UpdateNodePosition
                        // Just update connections
                        var vm = host.ViewModel;
                        if (vm != null)
                        {
                            var related = vm.Connections.Where(c => c.FromNode == node || c.ToNode == node).ToList();
                            foreach (var conn in related)
                            {
                                host.UpdateConnectionPath(conn);
                            }
                        }
                        host.SyncAllPortsZIndex(node);
                        return;
                    }

                    // ⚠️ Conditional node: ports Y = headerHeight + i * branchHeight, không dùng UpdatePortsPositionOnSide
                    if (node.IsConditionalNode)
                    {
                        host.RenderConditionalNodePorts(node);
                        var vmCond = host.ViewModel;
                        if (vmCond != null)
                        {
                            var related = vmCond.Connections.Where(c => c.FromNode == node || c.ToNode == node).ToList();
                            foreach (var conn in related)
                            {
                                host.UpdateConnectionPath(conn);
                            }
                        }
                        host.SyncAllPortsZIndex(node);
                        return;
                    }

                    // Update ports positions for other nodes
                    var positions = node.Ports.Where(p => p.IsVisible).Select(p => p.Position).Distinct().ToList();
                    foreach (var pos in positions)
                    {
                        host.UpdatePortsPositionOnSide(node, pos);
                    }

                    // Update connected lines
                    var vm2 = host.ViewModel;
                    if (vm2 != null)
                    {
                        var related = vm2.Connections.Where(c => c.FromNode == node || c.ToNode == node).ToList();
                        foreach (var conn in related)
                        {
                            host.UpdateConnectionPath(conn);
                        }
                    }

                    host.SyncAllPortsZIndex(node);
                }
            };
        }



        #region Func để áp dụng customization node chorme Inputs và Outputs


        /// <summary>
        /// Get custom section title for Inputs section. Returns null to use default "Inputs".
        /// </summary>
        public static string? GetInputsSectionTitle(WorkflowNode node)
        {
            // MouseEventNode: hiển thị "Số lần" thay vì "Inputs"
            if (node is MouseEventNode)
            {
                return "Số lần";
            }

            // KeyPressEventNode và HotkeyPressEventNode: có thể dùng "Số lần" hoặc giữ "Inputs"
            // Nếu muốn đổi, uncomment:
            if (node is KeyPressEventNode || node is HotkeyPressEventNode)
            {
                return "Số lần";
            }

            return null; // Use default "Inputs"
        }

        /// <summary>
        /// Get customization options for a node. Override this in derived classes or check node type.
        /// </summary>
        public static DataPanelCustomizationOptions? GetCustomizationOptions(WorkflowNode node)
        {
            if (node is ScreenCaptureNode screenCaptureNode)
            {
                return new DataPanelCustomizationOptions
                {
                    HideAddButton = true,
                    HideAddOutputButton = true,
                };
            }

            if (node is ScreenPositionPickerNode screenPositionPickerNode)
            {
                return new DataPanelCustomizationOptions
                {
                    HideAddButton = true,
                    HideAddOutputButton = true,
                };
            }

            //// KeyPressEventNode and HotkeyPressEventNode: customize for repeat count
            //if (node is KeyPressEventNode keyNode)
            //{
            //    return new DataPanelCustomizationOptions
            //    {
            //        HideAddButton = true,
            //        DisableKeyTextBox = true,
            //        AllowedTypes = new List<WorkflowDataType> { WorkflowDataType.Number },
            //        DefaultType = WorkflowDataType.Number,
            //        DisableTypeComboBox = true,
            //        DefaultValue = keyNode.RepeatCount.ToString(),
            //        ShowOnlyFirstInput = true
            //    };
            //}

            //if (node is MouseEventNode mouseNode)
            //{
            //    return new DataPanelCustomizationOptions
            //    {
            //        HideAddButton = true,              // Ẩn nút "+" (chỉ có 1 input)
            //        DisableKeyTextBox = true,         // Disable textbox key
            //        AllowedTypes = new List<WorkflowDataType> { WorkflowDataType.Number },
            //        DefaultType = WorkflowDataType.Number,
            //        DisableTypeComboBox = true,       // Disable type combobox
            //        DefaultValue = mouseNode.RepeatCount.ToString(), // Giá trị mặc định từ RepeatCount property
            //        ShowOnlyFirstInput = true,        // Chỉ hiện input đầu tiên (repeatCount)
            //        CustomInputsSectionTitle = "Số lần" // Tiêu đề section tùy chỉnh
            //    };
            //}

            //if (node is HotkeyPressEventNode hotkeyNode)
            //{
            //    return new DataPanelCustomizationOptions
            //    {
            //        HideAddButton = true,
            //        DisableKeyTextBox = true,
            //        AllowedTypes = new List<WorkflowDataType> { WorkflowDataType.Number },
            //        DefaultType = WorkflowDataType.Number,
            //        DisableTypeComboBox = true,
            //        DefaultValue = hotkeyNode.RepeatCount.ToString(),
            //        ShowOnlyFirstInput = true
            //    };
            //}

            //// LoopNode: có 2 inputs (loopCount và loopArray), ẩn button +
            //// AllowedTypes sẽ được set động trong CreateDataPanel dựa trên input key
            //if (node is LoopNode loopNode)
            //{
            //    return new DataPanelCustomizationOptions
            //    {
            //        HideAddButton = true,  // Ẩn nút "+" (chỉ có 2 inputs cố định)
            //        ShowOnlyFirstInput = false  // Hiện cả 2 inputs (sẽ được filter trong CreateDataPanel)
            //    };
            //}

            //if (node is InputNode inputNode)
            //{
            //    return new DataPanelCustomizationOptions
            //    {
            //        HideAddButton = true,  // Ẩn nút "+" 
            //        HideAddOutputButton = true
            //    };
            //}

            return null;
        }

        #endregion
    }
}

