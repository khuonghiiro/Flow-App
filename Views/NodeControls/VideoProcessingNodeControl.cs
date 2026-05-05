using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.Overlays;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FlowMy.Views.NodeControls
{
    public static class VideoProcessingNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Bottom }
        /// <summary>Phải trùng <see cref="Border.CornerRadius"/> bên dưới — clip nền/control con (vuông mặc định).</summary>
        private const double NodeChromeCornerRadius = 10;

        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();
        private const int TitleUpdateThrottleMs = 50;

        public static Border CreateBorder(VideoProcessingNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var contentControl = new VideoProcessingNodeContentControl(node, host);
            void RefreshPortsAndConnections()
            {
                if (node.Ports != null)
                {
                    foreach (var position in node.Ports.Select(p => p.Position).Distinct())
                    {
                        host.UpdatePortsPositionOnSide(node, position);
                    }
                }

                var connections = host.ViewModel?.Connections;
                if (connections != null && connections.Count > 0)
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(connections);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(connections);
                }
            }

            var border = new Border
            {
                Width = node.Width,
                Height = node.Height,
                MinWidth = 540,
                MinHeight = 340,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(NodeChromeCornerRadius),
                Cursor = Cursors.Hand,
                Effect = null,
                Tag = node
            };
            var overlayGrid = new Grid();
            overlayGrid.Children.Add(contentControl);

            var handlesLayer = new Grid { IsHitTestVisible = true };
            AddResizeHandle(handlesLayer, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(2, 2, 0, 0));
            AddResizeHandle(handlesLayer, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handlesLayer, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handlesLayer, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            AddResizeHandle(handlesLayer, ResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(0, 0, 0, 2));
            overlayGrid.Children.Add(handlesLayer);
            border.Child = overlayGrid;
            AttachResizeLogic(border, node);
            SyncNodeRoundedClip(border);

            if (node.Width < border.MinWidth) node.Width = border.MinWidth;
            if (node.Height < border.MinHeight) node.Height = border.MinHeight;
            border.Width = node.Width;
            border.Height = node.Height;

            var titleText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(node.Title) ? "Video Processing" : node.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTitleBrush(node),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleText;

            contentControl.SuggestedNodeSizeReady += (suggestedWidth, suggestedHeight) =>
            {
                // Auto-fit only grows to avoid surprising shrink after manual resize.
                var nextWidth = Math.Max(node.Width, suggestedWidth);
                var nextHeight = Math.Max(node.Height, suggestedHeight);
                if (nextWidth <= node.Width + 0.01 && nextHeight <= node.Height + 0.01) return;

                node.Width = nextWidth;
                node.Height = nextHeight;

                border.Width = node.Width;
                border.Height = node.Height;
                RefreshPortsAndConnections();
                UpdateTitlePosition(titleText, border, host);
            };

            border.SizeChanged += (_, _) =>
            {
                SyncNodeRoundedClip(border);
                RefreshPortsAndConnections();
            };

            contentControl.Loaded += (_, _) =>
            {
                contentControl.Measure(new Size(border.Width, double.PositiveInfinity));
                var desired = contentControl.DesiredSize;
                var minRequiredWidth = Math.Max(border.MinWidth, desired.Width + 8);
                var minRequiredHeight = Math.Max(border.MinHeight, Math.Min(920, desired.Height + 10));

                border.MinWidth = minRequiredWidth;
                border.MinHeight = minRequiredHeight;

                if (border.Width < minRequiredWidth) border.Width = minRequiredWidth;
                if (border.Height < minRequiredHeight) border.Height = minRequiredHeight;

                node.Width = border.Width;
                node.Height = border.Height;
            };

            bool isHovering = false;
            border.Focusable = true;
            border.FocusVisualStyle = null;
            border.MouseEnter += (_, _) =>
            {
                isHovering = true;
                UpdateTitleVisibility(titleText, node.TitleDisplayMode, isHovering, border);
                UpdateTitlePosition(titleText, border, host);
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => border.Focus()));
            };
            border.MouseLeave += (_, _) =>
            {
                isHovering = false;
                UpdateTitleVisibility(titleText, node.TitleDisplayMode, isHovering, border);
            };

            border.PreviewKeyDown += (s, e) =>
            {
                if (!isHovering) return;
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PortPosition? newPos = e.Key switch
                {
                    Key.Left => PortPosition.Left,
                    Key.Up => PortPosition.Top,
                    Key.Right => PortPosition.Right,
                    Key.Down => PortPosition.Bottom,
                    _ => null
                };
                if (newPos == null) return;
                e.Handled = true;
                ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
            };

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleText.Text = string.IsNullOrWhiteSpace(node.Title) ? "Video Processing" : node.Title;
                    }
                    else if (e.PropertyName == nameof(VideoProcessingNode.TitleDisplayMode))
                    {
                        UpdateTitleVisibility(titleText, node.TitleDisplayMode, isHovering, border);
                    }
                    else if (e.PropertyName == nameof(VideoProcessingNode.TitleColorMode) ||
                             e.PropertyName == nameof(VideoProcessingNode.TitleColorKey) ||
                             e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleText.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(VideoProcessingNode.Width) ||
                             e.PropertyName == nameof(VideoProcessingNode.Height))
                    {
                        border.Width = node.Width;
                        border.Height = node.Height;
                        RefreshPortsAndConnections();
                        UpdateTitlePosition(titleText, border, host);
                    }
                };
            }

            border.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            border.Loaded += (_, _) =>
            {
                SyncNodeRoundedClip(border);
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleText))
                {
                    host.WorkflowCanvas.Children.Add(titleText);
                    Panel.SetZIndex(titleText, 20000);
                    UpdateTitlePosition(titleText, border, host);
                }
            };

            border.LayoutUpdated += (_, _) =>
            {
                if (border.Visibility != Visibility.Visible)
                {
                    titleText.Visibility = Visibility.Collapsed;
                    return;
                }
                if (NodeChrome.IsZooming)
                {
                    titleText.Visibility = Visibility.Collapsed;
                    _titleUpdatedAfterZoom[border] = false;
                    return;
                }
                var hasUpdated = _titleUpdatedAfterZoom.TryGetValue(border, out var v) && v;
                if (!hasUpdated)
                {
                    _titleUpdatedAfterZoom[border] = true;
                    UpdateTitleVisibility(titleText, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleText, border, host);
                }
                if (titleText.Visibility == Visibility.Visible && !host.IsPanning && host.DraggedNode != node)
                    ThrottledUpdateTitlePosition(titleText, border, host);
            };

            border.Unloaded += (_, _) =>
            {
                if (_titleUpdateTimers.TryGetValue(border, out var timer))
                {
                    timer.Stop();
                    _titleUpdateTimers.Remove(border);
                }
                _titleUpdatedAfterZoom.Remove(border);
                if (host.WorkflowCanvas?.Children.Contains(titleText) == true)
                    host.WorkflowCanvas.Children.Remove(titleText);
                if (ReferenceEquals(node.TitleTextBlockUI, titleText))
                    node.TitleTextBlockUI = null;
            };

            return border;
        }

        private static void SyncNodeRoundedClip(Border border)
        {
            var w = Math.Max(1d, border.ActualWidth);
            var h = Math.Max(1d, border.ActualHeight);
            var maxR = Math.Min(w, h) / 2 - 0.001;
            var r = Math.Min(NodeChromeCornerRadius, Math.Max(0, maxR));
            border.Clip = r <= 0.25
                ? new RectangleGeometry(new Rect(0, 0, w, h))
                : new RectangleGeometry(new Rect(0, 0, w, h), r, r);
        }

        private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Stroke = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                StrokeThickness = 1.2,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = direction,
                Cursor = direction switch
                {
                    ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                    ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                    ResizeDirection.Bottom => Cursors.SizeNS,
                    _ => Cursors.Arrow
                }
            };
            // Prevent node drag MouseDown from hijacking resize gestures.
            handle.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
            grid.Children.Add(handle);
        }

        private static Brush GetTitleBrush(VideoProcessingNode node)
        {
            if (node.TitleColorMode == TitleColorMode.CustomColor && !string.IsNullOrWhiteSpace(node.TitleColorKey))
                return Application.Current.TryFindResource(node.TitleColorKey) as Brush ?? node.NodeBrush;
            return node.NodeBrush;
        }

        private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering)
            => mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };

        private static void UpdateTitleVisibility(TextBlock tb, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
        {
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible)
            {
                tb.Visibility = Visibility.Collapsed;
                return;
            }
            tb.Visibility = GetTitleVisibility(mode, isHovering);
        }

        private static void ThrottledUpdateTitlePosition(TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (!_titleUpdateTimers.TryGetValue(border, out var timer))
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    UpdateTitlePosition(tb, border, host);
                };
                _titleUpdateTimers[border] = timer;
            }
            timer.Stop();
            timer.Start();
        }

        private static void UpdateTitlePosition(TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (host.WorkflowCanvas == null || !host.WorkflowCanvas.Children.Contains(tb)) return;
            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);
            if (double.IsNaN(left) || double.IsNaN(top)) return;

            if (tb.ActualWidth == 0 || tb.ActualHeight == 0)
            {
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                tb.Arrange(new Rect(tb.DesiredSize));
            }

            Canvas.SetLeft(tb, left + (border.ActualWidth / 2) - (tb.ActualWidth / 2));
            Canvas.SetTop(tb, top - tb.ActualHeight - 4);
        }

        private static void OpenNodeDialog(VideoProcessingNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                if (node.Border?.IsMouseCaptured == true) node.Border.ReleaseMouseCapture();
                host.DraggedNode = null;
                if (host.ViewModel != null) host.ViewModel.SelectedNode = null;

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new VideoProcessingNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
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

        private static void ChangePortPosition(WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);

            if (port == null || port.Position == newPosition) return;
            port.Position = newPosition;
            host.UpdatePortsPositionOnSide(node, newPosition);
            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
            }
        }

        private static void AttachResizeLogic(Border border, VideoProcessingNode node)
        {
            bool isResizing = false;
            ResizeDirection currentDirection = ResizeDirection.None;
            Point resizeStartPoint = new();
            double originalWidth = 0;
            double originalHeight = 0;
            double originalX = 0;
            double originalY = 0;

            border.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (TryGetResizeDirectionFromSource(e.OriginalSource as DependencyObject, out var direction) == false) return;

                var parent = border.Parent as UIElement;
                if (parent == null) return;
                isResizing = true;
                currentDirection = direction;
                resizeStartPoint = e.GetPosition(parent);
                // Match HtmlUi behavior: start from rendered size to avoid stale Width/Height dead-zone.
                originalWidth = border.ActualWidth > 0 ? border.ActualWidth : Math.Max(border.MinWidth, border.Width);
                originalHeight = border.ActualHeight > 0 ? border.ActualHeight : Math.Max(border.MinHeight, border.Height);
                originalX = Canvas.GetLeft(border);
                originalY = Canvas.GetTop(border);
                if (double.IsNaN(originalX)) originalX = node.X;
                if (double.IsNaN(originalY)) originalY = node.Y;

                border.CaptureMouse();
                e.Handled = true;
            };

            border.PreviewMouseMove += (_, e) =>
            {
                if (!isResizing || !border.IsMouseCaptured) return;
                var parent = border.Parent as UIElement;
                if (parent == null) return;
                var current = e.GetPosition(parent);
                var dx = current.X - resizeStartPoint.X;
                var dy = current.Y - resizeStartPoint.Y;

                var newX = originalX;
                var newY = originalY;
                var newWidth = originalWidth;
                var newHeight = originalHeight;

                switch (currentDirection)
                {
                    case ResizeDirection.BottomRight:
                        newWidth = Math.Max(border.MinWidth, originalWidth + dx);
                        newHeight = Math.Max(border.MinHeight, originalHeight + dy);
                        break;
                    case ResizeDirection.TopLeft:
                        newWidth = Math.Max(border.MinWidth, originalWidth - dx);
                        newHeight = Math.Max(border.MinHeight, originalHeight - dy);
                        newX = originalX + (originalWidth - newWidth);
                        newY = originalY + (originalHeight - newHeight);
                        break;
                    case ResizeDirection.TopRight:
                        newWidth = Math.Max(border.MinWidth, originalWidth + dx);
                        newHeight = Math.Max(border.MinHeight, originalHeight - dy);
                        newY = originalY + (originalHeight - newHeight);
                        break;
                    case ResizeDirection.BottomLeft:
                        newWidth = Math.Max(border.MinWidth, originalWidth - dx);
                        newHeight = Math.Max(border.MinHeight, originalHeight + dy);
                        newX = originalX + (originalWidth - newWidth);
                        break;
                    case ResizeDirection.Bottom:
                        newHeight = Math.Max(border.MinHeight, originalHeight + dy);
                        break;
                }

                border.Width = newWidth;
                border.Height = newHeight;
                Canvas.SetLeft(border, newX);
                Canvas.SetTop(border, newY);
                node.Width = newWidth;
                node.Height = newHeight;
                node.X = newX;
                node.Y = newY;
                e.Handled = true;
            };

            border.PreviewMouseUp += (_, e) =>
            {
                if (!isResizing) return;
                isResizing = false;
                currentDirection = ResizeDirection.None;
                border.ReleaseMouseCapture();
                e.Handled = true;
            };

            // Keep resizing stable even if child content steals mouse capture.
            border.LostMouseCapture += (_, _) =>
            {
                if (isResizing)
                    border.CaptureMouse();
            };
        }

        private static bool TryGetResizeDirectionFromSource(DependencyObject? source, out ResizeDirection direction)
        {
            while (source != null)
            {
                if (source is FrameworkElement fe && fe.Tag is ResizeDirection rd)
                {
                    direction = rd;
                    return true;
                }
                source = VisualTreeHelper.GetParent(source) ?? (source as FrameworkElement)?.Parent;
            }

            direction = ResizeDirection.None;
            return false;
        }
    }
}
