using FlowMy.Controls;
using FlowMy.Converters;
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
using System.Windows.Threading;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// NodeControl chuẩn cho DataFetcherNode.
    /// Node hình vuông 60x60, icon inbox-out duotone-light, màu MintChocolate.
    /// Dialog mở bên phải màn hình qua NodeDialogManager.
    /// </summary>
    public static class DataFetcherNodeControl
    {
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50;
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();

        // Timers và subscriptions cho DataFetcher runtime
        private static readonly System.Collections.Generic.Dictionary<DataFetcherNode, DispatcherTimer> _fetchTimers = new();

        public static Border CreateBorder(DataFetcherNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // ───── 1. GRID 60x60 ─────
            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

            // ───── 2. ICON ─────
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "inbox-out duotone-light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;

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

            // ───── 3. BORDER CHÍNH ─────
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

            // ───── 4. TITLE TEXTBLOCK ─────
            var titleTextBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(node.Title) ? "Data Fetcher" : node.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTitleBrush(node),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            // ───── 5a. HOVER EVENTS ─────
            border.Focusable = true;
            border.FocusVisualStyle = null;

            bool isHovering = false;
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
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
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

            // ───── 5b. PROPERTYCHANGED ─────
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = string.IsNullOrWhiteSpace(node.Title) ? "Data Fetcher" : node.Title;
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitlePosition(titleTextBlock, border, host);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleTextBlock.Foreground = GetTitleBrush(node);
                        if (iconSvg != null) iconSvg.Fill = GetTextBrush(node.ColorKey);
                    }
                    else if (e.PropertyName == nameof(DataFetcherNode.TitleColorMode) ||
                             e.PropertyName == nameof(DataFetcherNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(DataFetcherNode.TitleDisplayMode))
                    {
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    }
                    else if (e.PropertyName == nameof(DataFetcherNode.EnableTimer) ||
                             e.PropertyName == nameof(DataFetcherNode.TimerIntervalValue) ||
                             e.PropertyName == nameof(DataFetcherNode.TimerUnit) ||
                             e.PropertyName == nameof(DataFetcherNode.EnableDataReadyScan) ||
                             e.PropertyName == nameof(DataFetcherNode.DataReadyScanIntervalValue) ||
                             e.PropertyName == nameof(DataFetcherNode.DataReadyScanUnit) ||
                             e.PropertyName == nameof(DataFetcherNode.DataReadyScanKeys) ||
                             e.PropertyName == nameof(DataFetcherNode.SourceOutputKey))
                    {
                        // Restart timer khi config thay đổi
                        RestartFetchTimer(node, host);
                    }
                    else if (e.PropertyName == nameof(DataFetcherNode.EnableRealtime) ||
                             e.PropertyName == nameof(DataFetcherNode.SourceNodeId))
                    {
                        // Re-subscribe realtime khi config thay đổi
                        AttachRealtimeSubscription(node, host);
                    }
                };
            }

            // ───── 5c. VISIBILITY SYNC ─────
            var visibilityDescriptor = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                    titleTextBlock.Visibility = Visibility.Collapsed;
                else
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            });

            // ───── 5d. LOADED ─────
            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                // Khởi động Timer nếu đã bật
                RestartFetchTimer(node, host);
                // Subscribe Realtime nếu đã bật
                AttachRealtimeSubscription(node, host);
            };

            // ───── 5e. SIZECHANGED ─────
            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);

            // ───── 5f. UNLOADED ─────
            border.Unloaded += (s, e) =>
            {
                try
                {
                    if (_titleUpdateTimers.TryGetValue(border, out var timer))
                    {
                        timer.Stop();
                        _titleUpdateTimers.Remove(border);
                    }
                    _titleUpdatedAfterZoom.Remove(border);
                    if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
                        host.WorkflowCanvas.Children.Remove(titleTextBlock);
                    if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                        node.TitleTextBlockUI = null;
                    // Cleanup Timer và Realtime
                    StopFetchTimer(node);
                    DetachRealtimeSubscription(node, host);
                }
                catch { }
            };

            // ───── 5g. LAYOUTUPDATED ─────
            border.LayoutUpdated += (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    return;
                }
                bool isZooming = NodeChrome.IsZooming;
                if (isZooming)
                {
                    if (titleTextBlock.Visibility != Visibility.Collapsed)
                        titleTextBlock.Visibility = Visibility.Collapsed;
                    _titleUpdatedAfterZoom[border] = false;
                    return;
                }
                bool hasUpdatedAfterZoom = _titleUpdatedAfterZoom.TryGetValue(border, out var updated) && updated;
                if (!hasUpdatedAfterZoom && border.Visibility == Visibility.Visible)
                {
                    _titleUpdatedAfterZoom[border] = true;
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    if (titleTextBlock.Visibility == Visibility.Visible)
                        UpdateTitlePosition(titleTextBlock, border, host);
                }
                if (host.IsPanning || host.DraggedNode == node) return;
                if (titleTextBlock.Visibility == Visibility.Visible)
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
            };

            // ───── 6. RIGHT CLICK → OPEN DIALOG ─────
            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            // Double-click cũng mở dialog
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    OpenNodeDialog(node, host, ownerWindow);
                }
            };

            return border;
        }

        // ===== TITLE BRUSH =====
        private static Brush GetTitleBrush(DataFetcherNode node)
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

        // ===== TITLE VISIBILITY =====
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
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible)
            {
                titleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }
            titleTextBlock.Visibility = GetTitleVisibility(mode, isHovering);
        }

        // ===== TITLE POSITION =====
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
            if (double.IsNaN(left) || double.IsNaN(top)) return;
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

        // ===== OPEN DIALOG (qua NodeDialogManager → tự position bên phải màn hình) =====
        private static void OpenNodeDialog(DataFetcherNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                if (node.Border != null && node.Border.IsMouseCaptured)
                    node.Border.ReleaseMouseCapture();
                host.DraggedNode = null;
                if (host.ViewModel != null)
                    host.ViewModel.SelectedNode = null;

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new DataFetcherNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== DIALOG MANAGER =====
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

        // ===== TIMER LOGIC =====
        private static void RestartFetchTimer(DataFetcherNode node, IWorkflowEditorHost host)
        {
            StopFetchTimer(node);

            if (!node.EnableTimer || string.IsNullOrWhiteSpace(node.SourceNodeId)) return;

            var intervalMs = node.GetTimerIntervalMs();
            if (intervalMs <= 0) return;

            // Chế độ mặc định: timer tick theo chu kỳ chính.
            if (!node.EnableDataReadyScan)
            {
                var normalTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(intervalMs)
                };
                normalTimer.Tick += (s, e) => RunTimerFetch(node, host);
                _fetchTimers[node] = normalTimer;
                normalTimer.Start();
                return;
            }

            // Chế độ "quét data trước": ban đầu quét nhanh, khi có data thì đổi hẳn sang chu kỳ chính.
            var scanIntervalMs = Math.Min(intervalMs, Math.Max(50, node.GetDataReadyScanIntervalMs()));
            var timer = new DispatcherTimer();

            void SwitchToScanMode()
            {
                timer.Stop();
                timer.Interval = TimeSpan.FromMilliseconds(scanIntervalMs);
                timer.Start();
            }

            void SwitchToMainCycleMode()
            {
                timer.Stop();
                timer.Interval = TimeSpan.FromMilliseconds(intervalMs);
                timer.Start();
            }

            // Bắt đầu ở chế độ quét nhanh.
            var isScanMode = true;
            timer.Interval = TimeSpan.FromMilliseconds(scanIntervalMs);

            timer.Tick += (s, e) =>
            {
                try
                {
                    if (isScanMode)
                    {
                        if (!HasSourceData(node, host))
                            return;

                        // Có data lần đầu: chạy ngay, rồi chuyển sang chu kỳ chính.
                        RunTimerFetch(node, host);
                        isScanMode = false;
                        SwitchToMainCycleMode();
                        return;
                    }

                    // Chế độ chu kỳ chính: tới kỳ mới chạy.
                    if (!HasSourceData(node, host))
                    {
                        // Data bị null/rỗng lại: quay về quét nhanh để chờ data trở lại.
                        isScanMode = true;
                        SwitchToScanMode();
                        return;
                    }

                    RunTimerFetch(node, host);
                }
                catch
                {
                    // Ignore runtime checking errors to keep timer alive.
                }
            };

            _fetchTimers[node] = timer;
            timer.Start();
        }

        private static bool HasSourceData(DataFetcherNode node, IWorkflowEditorHost host)
        {
            var sourceNode = FindSourceNodeByHost(node.SourceNodeId, host);
            if (sourceNode == null) return false;

            // Nếu user chọn danh sách key quét cụ thể:
            // chỉ coi là "data ready" khi TẤT CẢ key đã chọn đều có data.
            if (node.DataReadyScanKeys != null && node.DataReadyScanKeys.Count > 0)
            {
                foreach (var key in node.DataReadyScanKeys)
                {
                    var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key);
                    if (IsNullOrEmptyValue(value))
                        return false;
                }
                return true;
            }

            if (!string.IsNullOrWhiteSpace(node.SourceOutputKey))
            {
                var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, node.SourceOutputKey);
                return !IsNullOrEmptyValue(value);
            }

            if (sourceNode.DynamicOutputs == null || sourceNode.DynamicOutputs.Count == 0)
                return false;

            return sourceNode.DynamicOutputs.Any(output =>
            {
                var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, output.Key);
                return !IsNullOrEmptyValue(value);
            });
        }

        private static bool IsNullOrEmptyValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            if (string.Equals(value, "—", StringComparison.Ordinal)) return true;
            if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void StopFetchTimer(DataFetcherNode node)
        {
            if (_fetchTimers.TryGetValue(node, out var timer))
            {
                timer.Stop();
                _fetchTimers.Remove(node);
            }
        }

        private static void RunTimerFetch(DataFetcherNode node, IWorkflowEditorHost host)
        {
            try
            {
                // Dùng RequestRunSingleNode để:
                // 1) Chạy DataFetcherNodeExecutor.FetchValueAsync (tìm source node đúng cách qua all connections)
                // 2) Tự gọi IWorkflowExecutionVisualizer.RefreshSavedOutputs → cập nhật toggle "Có X kết quả"
                // 3) Cập nhật panel output hiển thị đúng giá trị mới
                host.RequestRunSingleNode(node);

                // System.Diagnostics.Debug.WriteLine($"[DataFetcherNodeControl] Timer/Realtime fetch triggered for '{node.Title}'");
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine( $"[DataFetcherNodeControl] Timer fetch error: {ex.Message}");
            }
        }

        // ===== REALTIME SUBSCRIPTION =====
        // Dùng static event DataFetcherNodeExecutor.AnyNodeCompleted (fired sau mỗi node execution).
        // Cách này reliable hơn PropertyChanged vì WorkflowDynamicDataPort không INotifyPropertyChanged.
        private static readonly System.Collections.Generic.Dictionary<DataFetcherNode, Action<WorkflowNode>> _realtimeStaticHandlers = new();

        private static void AttachRealtimeSubscription(DataFetcherNode node, IWorkflowEditorHost host)
        {
            DetachRealtimeSubscription(node, host);

            if (!node.EnableRealtime || string.IsNullOrWhiteSpace(node.SourceNodeId)) return;

            Action<WorkflowNode> handler = completedNode =>
            {
                // Chỉ react khi đúng source node hoàn thành
                if (!string.Equals(completedNode.Id, node.SourceNodeId, StringComparison.OrdinalIgnoreCase))
                    return;

                // Phải chạy trên UI thread vì RunTimerFetch cập nhật DynamicOutputs
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null) return;

                if (dispatcher.CheckAccess())
                    RunTimerFetch(node, host);
                else
                    dispatcher.BeginInvoke(new Action(() => RunTimerFetch(node, host)));
            };

            FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.AnyNodeCompleted += handler;
            _realtimeStaticHandlers[node] = handler;

            // System.Diagnostics.Debug.WriteLine($"[DataFetcherNodeControl] Realtime subscribed via AnyNodeCompleted: '{node.Title}' → SourceNodeId={node.SourceNodeId}");
        }

        private static void DetachRealtimeSubscription(DataFetcherNode node, IWorkflowEditorHost host)
        {
            if (!_realtimeStaticHandlers.TryGetValue(node, out var handler)) return;
            _realtimeStaticHandlers.Remove(node);
            FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.AnyNodeCompleted -= handler;
        }

        /// <summary>Tìm source node từ IWorkflowEditorHost.ViewModel.Nodes (dùng cho Timer/Realtime ngoài workflow execution).</summary>
        private static WorkflowNode? FindSourceNodeByHost(string? sourceNodeId, IWorkflowEditorHost host)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId) || host.ViewModel?.Nodes == null) return null;
            return host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
        }

        // ===== ICON TEXT BRUSH =====
        private static Brush GetTextBrush(string? colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey))
                return new SolidColorBrush(Color.FromRgb(148, 163, 184));
            var brush = Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush;
            return brush ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
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