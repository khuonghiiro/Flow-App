using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using FlowMy.Views;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// NodeControl chuẩn cho DataFetcherNode.
    /// Node hình vuông 60x60, icon inbox-out duotone-light, màu MintChocolate.
    /// Dialog mở bên phải màn hình qua NodeDialogManager.
    /// </summary>
    public static class DataFetcherNodeControl
    {
        // Timers và subscriptions cho DataFetcher runtime
        private static readonly Dictionary<DataFetcherNode, DispatcherTimer> _fetchTimers = new();
        private static readonly Dictionary<DataFetcherNode, Action<WorkflowNode>> _realtimeStaticHandlers = new();

        public static Border CreateBorder(DataFetcherNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // --- Create UI elements (node-specific) ---

            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

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
                Fill = GetIconBrush(node.ColorKey)
            };
            grid.Children.Add(iconSvg);

            var border = new Border
            {
                Child = grid,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node
            };

            var titleTextBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(node.Title) ? "Data Fetcher" : node.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode,
                    node.TitleColorKey,
                    node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always
                    ? Visibility.Visible
                    : Visibility.Collapsed
            };

            node.TitleTextBlockUI = titleTextBlock;

            // --- Node-specific property handlers ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.ColorKey)] = ctx =>
                {
                    iconSvg.Fill = GetIconBrush(node.ColorKey);
                },
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    border.Background = node.NodeBrush;
                    iconSvg.Fill = GetIconBrush(node.ColorKey);
                    ctx.TitleTextBlock.Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                        node.TitleColorMode,
                        node.TitleColorKey,
                        node.NodeBrush);
                },
                // DataFetcher-specific: restart timer/realtime when config changes
                [nameof(DataFetcherNode.EnableTimer)] = ctx => RestartFetchTimer(node, host),
                [nameof(DataFetcherNode.TimerIntervalValue)] = ctx => RestartFetchTimer(node, host),
                [nameof(DataFetcherNode.TimerUnit)] = ctx => RestartFetchTimer(node, host),
                [nameof(DataFetcherNode.EnableDataReadyScan)] = ctx => RestartFetchTimer(node, host),
                [nameof(DataFetcherNode.DataReadyScanIntervalValue)] = ctx => RestartFetchTimer(node, host),
                [nameof(DataFetcherNode.DataReadyScanUnit)] = ctx => RestartFetchTimer(node, host),
                [nameof(DataFetcherNode.DataReadyScanKeys)] = ctx => RestartFetchTimer(node, host),
                [nameof(DataFetcherNode.SourceOutputKey)] = ctx => RestartFetchTimer(node, host),
                [nameof(DataFetcherNode.EnableRealtime)] = ctx => AttachRealtimeSubscription(node, host),
                [nameof(DataFetcherNode.SourceNodeId)] = ctx => AttachRealtimeSubscription(node, host)
            };

            // --- Initialize with fluent API ---
            var context = BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new DataFetcherNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            // --- DataFetcher-specific: start timer and realtime on load, stop on unload ---
            border.Loaded += (s, e) =>
            {
                RestartFetchTimer(node, host);
                AttachRealtimeSubscription(node, host);
            };

            border.Unloaded += (s, e) =>
            {
                StopFetchTimer(node);
                DetachRealtimeSubscription(node, host);
            };

            // Double-click also opens dialog
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

        // ===== ICON BRUSH =====
        private static Brush GetIconBrush(string? colorKey)
        {
            if (!string.IsNullOrEmpty(colorKey))
            {
                var brush = Application.Current?.TryFindResource($"TextOn{colorKey}Brush") as Brush;
                if (brush != null) return brush;
            }
            return new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        // ===== OPEN DIALOG =====
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

                        RunTimerFetch(node, host);
                        isScanMode = false;
                        SwitchToMainCycleMode();
                        return;
                    }

                    if (!HasSourceData(node, host))
                    {
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
                host.RequestRunSingleNode(node);
            }
            catch (Exception)
            {
                // Ignore runtime errors to keep timer alive.
            }
        }

        // ===== REALTIME SUBSCRIPTION =====
        private static void AttachRealtimeSubscription(DataFetcherNode node, IWorkflowEditorHost host)
        {
            DetachRealtimeSubscription(node, host);

            if (!node.EnableRealtime || string.IsNullOrWhiteSpace(node.SourceNodeId)) return;

            Action<WorkflowNode> handler = completedNode =>
            {
                if (!string.Equals(completedNode.Id, node.SourceNodeId, StringComparison.OrdinalIgnoreCase))
                    return;

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null) return;

                if (dispatcher.CheckAccess())
                    RunTimerFetch(node, host);
                else
                    dispatcher.BeginInvoke(new Action(() => RunTimerFetch(node, host)));
            };

            FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.AnyNodeCompleted += handler;
            _realtimeStaticHandlers[node] = handler;
        }

        private static void DetachRealtimeSubscription(DataFetcherNode node, IWorkflowEditorHost host)
        {
            if (!_realtimeStaticHandlers.TryGetValue(node, out var handler)) return;
            _realtimeStaticHandlers.Remove(node);
            FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.AnyNodeCompleted -= handler;
        }

        private static WorkflowNode? FindSourceNodeByHost(string? sourceNodeId, IWorkflowEditorHost host)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId) || host.ViewModel?.Nodes == null) return null;
            return host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, sourceNodeId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
