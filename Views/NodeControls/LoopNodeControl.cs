using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.Overlays;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public static class LoopNodeControl
    {
        // Throttle title position updates để tránh giật khi pan/zoom
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50; // Throttle updates khi đang pan/zoom

        // Track xem đã update sau khi zoom kết thúc chưa để tránh update nhiều lần không cần thiết
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();
        public static Border CreateBorder(LoopNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // Diamond shape dimensions - smaller size
            const double diamondWidth = 100;
            const double diamondHeight = 100;

            // Create diamond shape using Polygon
            var diamond = new Polygon
            {
                Points = new PointCollection(new[]
                {
                    new Point(diamondWidth / 2, 0),           // Top
                    new Point(diamondWidth, diamondHeight / 2), // Right
                    new Point(diamondWidth / 2, diamondHeight), // Bottom
                    new Point(0, diamondHeight / 2)           // Left
                }),
                Fill = node.NodeBrush,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                Stretch = Stretch.Fill
            };

            var grid = new Grid
            {
                Width = diamondWidth,
                Height = diamondHeight,
                MinWidth = diamondWidth,
                MinHeight = diamondHeight,
                ClipToBounds = false // Important: Allow diamond shape to extend beyond grid bounds
            };

            // Add diamond shape to grid
            grid.Children.Add(diamond);

            // Add icon/text in center
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "arrows-spin duotone", System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = Application.Current.TryFindResource("TextOnWarningBrush") as Brush
            };
            grid.Children.Add(iconSvg);

            // ✅ Tạo TextBlock cho tiêu đề - Foreground từ TitleColorMode/TitleColorKey
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Loop",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTitleBrush(node),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false // Không block mouse events
            };

            // Lưu reference để có thể cập nhật sau
            node.TitleTextBlockUI = titleTextBlock;

            // Xử lý hover để hiển thị/ẩn tiêu đề
            bool isHovering = false;

            var border = new Border
            {
                Child = grid,
                Width = diamondWidth,
                Height = diamondHeight,
                MinWidth = diamondWidth,
                MinHeight = diamondHeight,
                Background = Brushes.Transparent, // Transparent background, diamond shape provides the visual
                BorderBrush = null, // No border, diamond has its own stroke
                BorderThickness = new Thickness(0),
                ClipToBounds = false, // Important: Allow diamond shape to extend beyond border bounds
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node
            };

            // ✅ Sync icon color và diamond fill khi ColorKey hoặc NodeBrush thay đổi
            // ✅ Sync title khi Title hoặc TitleDisplayMode thay đổi
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.ColorKey))
                    {
                        // Update icon color khi ColorKey thay đổi
                        iconSvg.Fill = GetTextBrush(node.ColorKey);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        diamond.Fill = node.NodeBrush;
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(LoopNode.TitleColorMode) || e.PropertyName == nameof(LoopNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        // Update title text khi Title thay đổi
                        titleTextBlock.Text = node.Title ?? "Loop";
                        // ✅ Chỉ update position nếu node visible
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        {
                            UpdateTitlePosition(titleTextBlock, border, host);
                        }
                    }
                    else if (e.PropertyName == nameof(LoopNode.TitleDisplayMode))
                    {
                        // ✅ Chỉ update visibility nếu node visible
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        {
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                        }
                    }
                };
            }

            // ⚠️ CRITICAL: Prevent hover and drag from changing background (which would show square shape)
            // Use MouseEnter/MouseLeave with high priority Dispatcher to override after other handlers
            // Note: NodeBorder_MouseEnter/Leave will skip LoopNode, but we still ensure background stays transparent

            border.Focusable = true;
            border.FocusVisualStyle = null;
            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                // Force background to stay transparent immediately
                border.Background = Brushes.Transparent;
                // ✅ Chỉ update title visibility nếu node border đang visible (trong viewport)
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                // Also use Dispatcher with high priority to ensure it runs after any other handlers
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() => border.Background = Brushes.Transparent));
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (isHovering) border.Focus(); }));
            };

            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                // Ensure background stays transparent when leaving hover
                border.Background = Brushes.Transparent;
                // ✅ Chỉ update title visibility nếu node border đang visible (trong viewport)
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                }
                // Also use Dispatcher with high priority to ensure it runs after any other handlers
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() => border.Background = Brushes.Transparent));
            };

            // Keyboard Port Position: Arrow = Port IN, Shift+Arrow = Port OUT
            border.PreviewKeyDown += (s, e) =>
            {
                if (!isHovering) return;
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PortPosition? newPos = e.Key switch
                {
                    Key.Left  => PortPosition.Left,
                    Key.Up    => PortPosition.Top,
                    Key.Right => PortPosition.Right,
                    Key.Down  => PortPosition.Bottom,
                    _ => null
                };
                if (newPos == null) return;
                e.Handled = true;
                ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
            };

            // ⚠️ CRITICAL: Prevent MouseDown from setting background (which happens when clicking)
            border.MouseDown += (s, e) =>
            {
                // Force background to stay transparent when mouse down
                border.Background = Brushes.Transparent;
                // Also use Dispatcher to ensure it runs after any other handlers
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => border.Background = Brushes.Transparent));
            };

            // ⚠️ CRITICAL: Prevent MouseUp from setting background (which happens after drag)
            border.MouseUp += (s, e) =>
            {
                // Force background to stay transparent after mouse up (after drag ends)
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => border.Background = Brushes.Transparent));
            };

            // ⚠️ CRITICAL: Also handle PreviewMouseUp to catch it earlier
            border.PreviewMouseUp += (s, e) =>
            {
                // Force background to stay transparent after preview mouse up
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => border.Background = Brushes.Transparent));
            };

            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            // ✅ Sync title visibility với border visibility khi border visibility thay đổi (viewport culling)
            var visibilityDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                }
            });

            // Đảm bảo titleTextBlock được thêm vào Canvas sau khi border được render
            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            // Update position khi border di chuyển hoặc resize
            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);

            // Sync position khi node di chuyển (sử dụng LayoutUpdated giống ExecutionStatusBadge)
            // Throttle updates để tránh giật khi pan/zoom
            // ✅ Sync title visibility với border visibility trước khi update position
            border.LayoutUpdated += (s, e) =>
            {
                // ✅ Sync visibility với border trước
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    return;
                }

                bool isZooming = NodeChrome.IsZooming;

                // ✅ Nếu đang zoom, ẩn title để tránh xử lý và đánh dấu chưa update
                // Chỉ set Visibility.Collapsed khi chưa phải Collapsed để tránh property change overhead
                if (isZooming)
                {
                    if (titleTextBlock.Visibility != Visibility.Collapsed)
                    {
                        titleTextBlock.Visibility = Visibility.Collapsed;
                    }
                    _titleUpdatedAfterZoom[border] = false;
                    return;
                }

                // ✅ Nếu zoom vừa kết thúc (không còn zooming) và chưa update -> update ngay lập tức
                bool hasUpdatedAfterZoom = _titleUpdatedAfterZoom.TryGetValue(border, out var updated) && updated;
                if (!hasUpdatedAfterZoom && border.Visibility == Visibility.Visible)
                {
                    // Đánh dấu đã update để tránh update nhiều lần
                    _titleUpdatedAfterZoom[border] = true;

                    // Update visibility theo TitleDisplayMode
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);

                    // Nếu title visible, update position ngay lập tức (không throttle)
                    if (titleTextBlock.Visibility == Visibility.Visible)
                    {
                        UpdateTitlePosition(titleTextBlock, border, host);
                    }
                }

                // ✅ Khi đang kéo chính node này: đồng bộ title ngay theo vị trí border (tránh title đứng yên cho đến khi nhấn canvas)
                if (host.DraggedNode == node)
                {
                    if (titleTextBlock.Visibility == Visibility.Visible)
                    {
                        UpdateTitlePosition(titleTextBlock, border, host);
                    }
                    return;
                }

                if (host.IsPanning)
                {
                    return;
                }

                // Throttle updates bằng DispatcherTimer cho các trường hợp khác (node di chuyển, resize, etc.)
                if (titleTextBlock.Visibility == Visibility.Visible)
                {
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            return border;
        }

        private static void OpenNodeDialog(LoopNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                // ⚠️ CRITICAL: Release mouse capture và clear drag state để tránh node nhảy đến vị trí chuột
                if (node.Border != null && node.Border.IsMouseCaptured)
                {
                    node.Border.ReleaseMouseCapture();
                }

                // ⚠️ CRITICAL: Clear DraggedNode để đảm bảo dialog có thể đóng ngay lập tức
                host.DraggedNode = null;

                // ⚠️ CRITICAL: Deselect node ngay khi click chuột phải để tránh node nhảy đến vị trí chuột
                if (host.ViewModel != null)
                {
                    host.ViewModel.SelectedNode = null;
                }

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new LoopNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            if (host is WorkflowEditorWindow window)
            {
                var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager) return manager;
            }
            return new NodeDialogManager();
        }

        private static Brush GetTextBrush(string colorKey)
        {
            return colorKey switch
            {
                "red" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                "orange" => new SolidColorBrush(Color.FromRgb(249, 115, 22)),
                "yellow" => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
                "green" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                "blue" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                "purple" => new SolidColorBrush(Color.FromRgb(168, 85, 247)),
                "pink" => new SolidColorBrush(Color.FromRgb(236, 72, 153)),
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
        }

        private static Brush GetTitleBrush(LoopNode node)
        {
            if (node.TitleColorMode == TitleColorMode.CustomColor && !string.IsNullOrEmpty(node.TitleColorKey))
            {
                if (node.TitleColorKey == "LimeGreen")
                    return new SolidColorBrush(Colors.LimeGreen);
                var brush = Application.Current.TryFindResource(node.TitleColorKey) as Brush;
                if (brush != null) return brush;
            }
            return node.NodeBrush;
        }

        private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering)
        {
            return mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };
        }

        private static void UpdateTitleVisibility(TextBlock titleTextBlock, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
        {
            // ✅ Nếu node border bị ẩn (không trong viewport), ẩn title luôn
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible)
            {
                titleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            // Nếu node visible, áp dụng TitleDisplayMode
            titleTextBlock.Visibility = GetTitleVisibility(mode, isHovering);
        }

        private static void ThrottledUpdateTitlePosition(TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
        {
            if (!_titleUpdateTimers.TryGetValue(border, out var timer))
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    UpdateTitlePosition(titleTextBlock, border, host);
                };
                _titleUpdateTimers[border] = timer;
            }

            timer.Stop();
            timer.Start();
        }

        private static void UpdateTitlePosition(TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
        {
            if (host.WorkflowCanvas == null || border == null || !host.WorkflowCanvas.Children.Contains(titleTextBlock)) return;

            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);

            // Fallback to node position nếu Canvas position chưa được set
            if (double.IsNaN(left) && border.Tag is WorkflowNode node)
            {
                left = node.X;
            }
            if (double.IsNaN(top) && border.Tag is WorkflowNode node2)
            {
                top = node2.Y;
            }

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            // Đảm bảo ActualWidth và ActualHeight đã được tính toán
            if (titleTextBlock.ActualWidth == 0 || titleTextBlock.ActualHeight == 0)
            {
                titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
            }

            // Đặt titleTextBlock phía trên border (center horizontally)
            // LoopNode có hình diamond, đặt title phía trên đỉnh diamond
            var titleLeft = left + (border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
            var titleTop = top - titleTextBlock.ActualHeight - 4; // 4px spacing

            Canvas.SetLeft(titleTextBlock, titleLeft);
            Canvas.SetTop(titleTextBlock, titleTop);
        }

        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;
            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);
            if (port == null || port.Position == newPosition) return;
            port.Position = newPosition;
            host.UpdatePortsPositionOnSide(node, newPosition);
            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                }
                catch { }
            }
        }
    }
}