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
using System.Windows.Threading;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Custom UI control builder cho Input Node.
    /// Chỉ tạo UI; attach mouse/context menu/renderer logic sẽ do caller đảm nhiệm.
    /// </summary>
    public static class InputNodeControl
    {
        // Throttle title position updates để tránh giật khi pan/zoom
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50; // Throttle updates khi đang pan/zoom

        // Track xem đã update sau khi zoom kết thúc chưa để tránh update nhiều lần không cần thiết
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();

        public static Border CreateBorder(InputNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

            // RECOMMENDED: Use SvgViewboxEx
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "left-to-dotted-line duotone-regular", System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = Brushes.White
            };
            grid.Children.Add(iconSvg);

            // Tạo TextBlock cho tiêu đề - Foreground từ TitleColorMode/TitleColorKey
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Input",
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

            var border = new Border
            {
                Child = grid,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
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

            // Xử lý hover để hiển thị/ẩn tiêu đề
            border.Focusable = true;
            border.FocusVisualStyle = null;

            bool isHovering = false;
            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                // ✅ Chỉ update visibility nếu node border đang visible (trong viewport)
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (isHovering) border.Focus(); }));
            };
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                // ✅ Chỉ update visibility nếu node border đang visible (trong viewport)
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                }
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

            // ✅ Sync title visibility với node border visibility (viewport culling)
            // Khi node border bị ẩn (không trong viewport), title cũng phải bị ẩn
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = node.Title ?? "Input";
                        // ✅ Chỉ update position nếu node visible
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        {
                            UpdateTitlePosition(titleTextBlock, border, host);
                        }
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(InputNode.TitleColorMode) || e.PropertyName == nameof(InputNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(InputNode.TitleDisplayMode))
                    {
                        // ✅ Chỉ update visibility nếu node visible
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        {
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                        }
                    }
                };
            }

            // ✅ Sync title visibility với border visibility khi border visibility thay đổi
            // Sử dụng DependencyPropertyDescriptor để listen Border.Visibility changes
            var visibilityDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                // Khi border visibility thay đổi, sync title visibility
                if (node.Border != null)
                {
                    if (node.Border.Visibility != Visibility.Visible)
                    {
                        // Node không trong viewport -> ẩn title
                        titleTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // Node trong viewport -> hiển thị title theo TitleDisplayMode
                        UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering);
                    }
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

                // Skip updates khi đang pan hoặc drag để tránh giật
                if (host.IsPanning || host.DraggedNode == node)
                {
                    return;
                }

                // Throttle updates bằng DispatcherTimer cho các trường hợp khác (node di chuyển, resize, etc.)
                if (titleTextBlock.Visibility == Visibility.Visible)
                {
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            return border;
        }

        private static void OpenNodeDialog(InputNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                // Release mouse capture và clear drag state để tránh node nhảy đến vị trí chuột
                if (node.Border != null && node.Border.IsMouseCaptured)
                {
                    node.Border.ReleaseMouseCapture();
                }

                // Clear DraggedNode để đảm bảo dialog có thể đóng ngay lập tức
                host.DraggedNode = null;

                // Deselect node ngay khi click chuột phải để tránh node nhảy đến vị trí chuột
                if (host.ViewModel != null)
                {
                    host.ViewModel.SelectedNode = null;
                }

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new InputNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
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

        private static Brush GetTitleBrush(InputNode node)
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
            var titleLeft = left + (border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
            var titleTop = top - titleTextBlock.ActualHeight - 4; // 4px spacing

            Canvas.SetLeft(titleTextBlock, titleLeft);
            Canvas.SetTop(titleTextBlock, titleTop);
            Panel.SetZIndex(titleTextBlock, 20000); // Đảm bảo hiển thị trên cùng
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