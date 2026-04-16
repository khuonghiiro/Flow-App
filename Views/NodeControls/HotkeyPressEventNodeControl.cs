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
    /// Custom UI builder cho HotkeyPressEventNode (sự kiện nhấn tổ hợp phím).
    /// </summary>
    public static class HotkeyPressEventNodeControl
    {
        // Throttle title position updates để tránh giật khi pan/zoom
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50; // Throttle updates khi đang pan/zoom

        // Track xem đã update sau khi zoom kết thúc chưa để tránh update nhiều lần không cần thiết
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();

        public static Border CreateBorder(HotkeyPressEventNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // Grid chỉ chứa icon
            var grid = new Grid
            {
                MinWidth = 60,
                MinHeight = 60,
                Width = 60,
                Height = 60
            };

            // Icon SVG sử dụng SvgViewboxEx
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "keyboard duotone", System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetTextBrush(node.ColorKey)
            };
            grid.Children.Add(iconSvg);

            // Tạo TextBlock cho tiêu đề - Foreground từ TitleColorMode/TitleColorKey
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Hotkey Press",
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

            // Xử lý hover để hiển thị/ẩn tiêu đề trên Canvas
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

            // Sync title khi node thay đổi
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = node.Title ?? "Hotkey Press";
                        // ✅ Chỉ update position nếu node visible
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        {
                            UpdateTitlePosition(titleTextBlock, border, host);
                        }
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(HotkeyPressEventNode.TitleColorMode) || e.PropertyName == nameof(HotkeyPressEventNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(HotkeyPressEventNode.TitleDisplayMode))
                    {
                        // ✅ Chỉ update visibility nếu node visible
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        {
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                        }
                    }
                };
            }

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

            // Sync position khi node di chuyển
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

            // Xử lý chuột phải để mở dialog
            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };


            return border;
        }

        private static Brush GetTitleBrush(HotkeyPressEventNode node)
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

        private static void OpenNodeDialog(HotkeyPressEventNode node, IWorkflowEditorHost host, Window? ownerWindow)
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

                // Nếu đã có dialog mở cho node này thì không mở lại
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node)
                {
                    return;
                }

                // Đóng dialog hiện tại nếu đang mở node khác
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                {
                    dialogManager.CloseCurrentDialog();
                }

                var dialog = new HotkeyPressEventNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow ?? throw new InvalidOperationException("Cannot create dialog without owner window"));
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi mở dialog: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            // Lấy từ WorkflowEditorWindow nếu có thể
            if (host is WorkflowEditorWindow window)
            {
                // Sử dụng reflection để lấy NodeDialogManager từ private field
                var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager)
                {
                    return manager;
                }
            }

            // Fallback: tạo mới (tạm thời)
            return new NodeDialogManager();
        }

        private static Brush GetTextBrush(string? colorKey)
        {
            var key = string.IsNullOrWhiteSpace(colorKey) ? null : $"TextOn{colorKey}Brush";
            if (!string.IsNullOrWhiteSpace(key))
            {
                try
                {
                    if (Application.Current?.TryFindResource(key) is Brush b) return b;
                }
                catch { }
            }
            return Brushes.White;
        }

        private static ControlTemplate BuildRoundedButtonTemplate(double cornerRadius)
        {
            var template = new ControlTemplate(typeof(Button));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            borderFactory.AppendChild(contentPresenter);

            template.VisualTree = borderFactory;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(55, 255, 255, 255))));
            template.Triggers.Add(hover);

            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(75, 255, 255, 255))));
            template.Triggers.Add(pressed);

            return template;
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