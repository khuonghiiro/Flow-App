using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
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
    public static class OutputNodeControl
    {
        // ⚠️ CRITICAL: Static dictionaries cho throttling
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50;
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();

        public static Border CreateBorder(OutputNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "right-to-dotted-line duotone-regular", System.Globalization.CultureInfo.CurrentCulture) as Uri;
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

            // ⚠️ CRITICAL: Tạo titleTextBlock với Visibility dựa trên TitleDisplayMode, Foreground từ TitleColorMode/TitleColorKey
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Output",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTitleBrush(node),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false // Không block mouse events
            };

            // ⚠️ CRITICAL: Lưu reference để cập nhật sau
            node.TitleTextBlockUI = titleTextBlock;

            bool isHovering = false;

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

            // ⚠️ CRITICAL: Combine ALL PropertyChanged handlers in ONE block
            // ⚠️ CRITICAL: Wrap UI updates in Dispatcher để tránh threading exception
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    // Lấy dispatcher từ border hoặc Application.Current
                    var dispatcher = border.Dispatcher ?? Application.Current?.Dispatcher;
                    if (dispatcher == null) return;

                    // Kiểm tra nếu đã ở UI thread, gọi trực tiếp; nếu không, dùng BeginInvoke
                    if (dispatcher.CheckAccess())
                    {
                        UpdateUIForPropertyChange(e.PropertyName, iconSvg, border, titleTextBlock, node, isHovering, host);
                    }
                    else
                    {
                        dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateUIForPropertyChange(e.PropertyName, iconSvg, border, titleTextBlock, node, isHovering, host);
                        }), DispatcherPriority.Normal);
                    }
                };
            }

            // ⚠️ CRITICAL: Hover handling để show/hide title khi TitleDisplayMode = Hover

            border.Focusable = true;
            border.FocusVisualStyle = null;
            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
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

            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            // ⚠️ CRITICAL: Sync title visibility với border visibility (viewport culling)
            var visibilityDescriptor = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                var dispatcher = border.Dispatcher ?? Application.Current?.Dispatcher;
                if (dispatcher == null) return;

                if (!dispatcher.CheckAccess())
                {
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (border.Visibility != Visibility.Visible)
                        {
                            titleTextBlock.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                        }
                    }), DispatcherPriority.Normal);
                    return;
                }

                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                }
            });

            // ⚠️ CRITICAL: Add titleTextBlock to Canvas khi border loaded
            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);
                    // Sync visibility ngay khi add vào canvas
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);

            // ⚠️ CRITICAL: Cleanup để tránh memory leak khi node bị remove/unload
            border.Unloaded += (s, e) =>
            {
                try
                {
                    // Stop & remove throttling timer
                    if (_titleUpdateTimers.TryGetValue(border, out var timer))
                    {
                        timer.Stop();
                        _titleUpdateTimers.Remove(border);
                    }
                    _titleUpdatedAfterZoom.Remove(border);

                    // Remove titleTextBlock khỏi canvas (nếu còn)
                    if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
                    {
                        host.WorkflowCanvas.Children.Remove(titleTextBlock);
                    }

                    // Clear reference để tránh giữ UI element
                    if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                    {
                        node.TitleTextBlockUI = null;
                    }
                }
                catch
                {
                    // Best-effort cleanup; ignore to avoid crashing unload path
                }
            };

            // ⚠️ CRITICAL: LayoutUpdated với zoom handling và throttling
            border.LayoutUpdated += (s, e) =>
            {
                // ⚠️ CRITICAL: LayoutUpdated có thể được trigger từ background thread
                var dispatcher = border.Dispatcher ?? Application.Current?.Dispatcher;
                if (dispatcher == null) return;

                if (!dispatcher.CheckAccess())
                {
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Sync visibility với border trước
                        if (border.Visibility != Visibility.Visible)
                        {
                            titleTextBlock.Visibility = Visibility.Collapsed;
                            return;
                        }

                        bool isZooming = NodeChrome.IsZooming;

                        // Nếu đang zoom, ẩn title và đánh dấu chưa update
                        if (isZooming)
                        {
                            if (titleTextBlock.Visibility != Visibility.Collapsed)
                            {
                                titleTextBlock.Visibility = Visibility.Collapsed;
                            }
                            _titleUpdatedAfterZoom[border] = false;
                            return;
                        }

                        // Nếu zoom vừa kết thúc và chưa update → update ngay
                        bool hasUpdatedAfterZoom = _titleUpdatedAfterZoom.TryGetValue(border, out var updated) && updated;
                        if (!hasUpdatedAfterZoom && border.Visibility == Visibility.Visible)
                        {
                            _titleUpdatedAfterZoom[border] = true;
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                            if (titleTextBlock.Visibility == Visibility.Visible)
                            {
                                UpdateTitlePosition(titleTextBlock, border, host);
                            }
                        }

                        // Skip khi pan hoặc drag để tránh giật
                        if (host.IsPanning || host.DraggedNode == node)
                        {
                            return;
                        }

                        // Throttle updates cho các trường hợp khác
                        if (titleTextBlock.Visibility == Visibility.Visible)
                        {
                            ThrottledUpdateTitlePosition(titleTextBlock, border, host);
                        }
                    }), DispatcherPriority.Normal);
                    return;
                }

                // Sync visibility với border trước
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    return;
                }

                bool isZooming = NodeChrome.IsZooming;

                // Nếu đang zoom, ẩn title và đánh dấu chưa update
                if (isZooming)
                {
                    if (titleTextBlock.Visibility != Visibility.Collapsed)
                    {
                        titleTextBlock.Visibility = Visibility.Collapsed;
                    }
                    _titleUpdatedAfterZoom[border] = false;
                    return;
                }

                // Nếu zoom vừa kết thúc và chưa update → update ngay
                bool hasUpdatedAfterZoom = _titleUpdatedAfterZoom.TryGetValue(border, out var updated) && updated;
                if (!hasUpdatedAfterZoom && border.Visibility == Visibility.Visible)
                {
                    _titleUpdatedAfterZoom[border] = true;
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    if (titleTextBlock.Visibility == Visibility.Visible)
                    {
                        UpdateTitlePosition(titleTextBlock, border, host);
                    }
                }

                // Skip khi pan hoặc drag để tránh giật
                if (host.IsPanning || host.DraggedNode == node)
                {
                    return;
                }

                // Throttle updates cho các trường hợp khác
                if (titleTextBlock.Visibility == Visibility.Visible)
                {
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            return border;
        }

        private static void OpenNodeDialog(OutputNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                if (node.Border != null && node.Border.IsMouseCaptured)
                {
                    node.Border.ReleaseMouseCapture();
                }
                host.DraggedNode = null;
                if (host.ViewModel != null)
                {
                    host.ViewModel.SelectedNode = null;
                }

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new OutputNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
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
            return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        private static Brush GetTitleBrush(OutputNode node)
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

        // ⚠️ CRITICAL: Helper methods cho TitleDisplayMode
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
            // ⚠️ CRITICAL: Đảm bảo chạy trên UI thread
            var dispatcher = titleTextBlock.Dispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => UpdateTitleVisibility(titleTextBlock, mode, isHovering, nodeBorder)), DispatcherPriority.Normal);
                return;
            }

            // Nếu node border bị ẩn (không trong viewport), ẩn title
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible)
            {
                titleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }
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
            // ⚠️ CRITICAL: Đảm bảo chạy trên UI thread
            var dispatcher = titleTextBlock.Dispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => UpdateTitlePosition(titleTextBlock, border, host)), DispatcherPriority.Normal);
                return;
            }

            if (host.WorkflowCanvas == null || border == null || !host.WorkflowCanvas.Children.Contains(titleTextBlock)) return;

            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);

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

            if (titleTextBlock.ActualWidth == 0 || titleTextBlock.ActualHeight == 0)
            {
                titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
            }

            var titleLeft = left + (border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
            var titleTop = top - titleTextBlock.ActualHeight - 4;

            Canvas.SetLeft(titleTextBlock, titleLeft);
            Canvas.SetTop(titleTextBlock, titleTop);
        }

        // ⚠️ CRITICAL: Helper method để update UI từ PropertyChanged handler (đảm bảo thread-safe)
        private static void UpdateUIForPropertyChange(string? propertyName, SvgViewboxEx iconSvg, Border border, TextBlock titleTextBlock, OutputNode node, bool isHovering, IWorkflowEditorHost host)
        {
            if (propertyName == nameof(WorkflowNode.ColorKey))
            {
                iconSvg.Fill = GetTextBrush(node.ColorKey);
            }
            else if (propertyName == nameof(WorkflowNode.NodeBrush))
            {
                border.Background = node.NodeBrush;
                titleTextBlock.Foreground = GetTitleBrush(node);
            }
            else if (propertyName == nameof(OutputNode.TitleColorMode) || propertyName == nameof(OutputNode.TitleColorKey))
            {
                titleTextBlock.Foreground = GetTitleBrush(node);
            }
            else if (propertyName == nameof(WorkflowNode.Title))
            {
                titleTextBlock.Text = node.Title ?? "Output";
                // Chỉ update position nếu node visible
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
            }
            else if (propertyName == nameof(OutputNode.TitleDisplayMode))
            {
                // Chỉ update visibility nếu node visible
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                }
            }
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