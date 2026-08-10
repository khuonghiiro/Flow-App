using FlowMy.Models;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Documents;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Workflow;

public sealed class WorkflowExecutionVisualizer : IWorkflowExecutionVisualizer
{
    private readonly Dispatcher _dispatcher;

    private DispatcherTimer? _nodeTimingTimer;
    private Stopwatch? _nodeTimingStopwatch;
    private WorkflowNode? _timingNode;
    
    private sealed class ActiveNodeTiming
    {
        public DispatcherTimer Timer { get; init; } = null!;
        public Stopwatch Stopwatch { get; init; } = null!;
        public int ActiveCount { get; set; } = 1;
    }

    // Mỗi (node, runKey) giữ timer + counter active tasks.
    // Async song song có thể start/complete nhiều lần cùng (node, runKey),
    // nên không được remove timer chỉ vì 1 nhánh hoàn tất.
    private readonly Dictionary<(WorkflowNode Node, string RunKey), ActiveNodeTiming> _activeNodeTimers = new();

    public bool IsDebugMode { get; set; }

    public WorkflowExecutionVisualizer()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        FlowMy.Services.Rendering.NodeChromeLazyRenderBridge.RequestResultsRender = UpdateNodeExecutionResults;
    }

    public void ResetVisualization(IEnumerable<WorkflowNode> nodes)
    {
        RunOnUi(() =>
        {
            StopTimingTimer();
            _timingNode = null;
            _nodeTimingStopwatch = null;
            
            // Stop và clear tất cả active node timers
            foreach (var kvp in _activeNodeTimers.ToList())
            {
                kvp.Value.Timer.Stop();
                _activeNodeTimers.Remove(kvp.Key);
            }

            foreach (var n in nodes)
            {
                if (n.ExecutionStatusTextUI != null) n.ExecutionStatusTextUI.Text = "";
                if (n.ExecutionStatusContainerUI != null) n.ExecutionStatusContainerUI.Visibility = Visibility.Collapsed;
                if (n.ExecutionBusySpinnerUI != null) n.ExecutionBusySpinnerUI.Visibility = Visibility.Collapsed;
                if (n.ExecutionResultsItemsPanel != null) n.ExecutionResultsItemsPanel.Children.Clear();
                if (n.ExecutionResultsItemsPanel != null) n.ExecutionResultsItemsPanel.Visibility = Visibility.Collapsed;
                if (n.ExecutionResultsToggleUI != null)
                {
                    n.ExecutionResultsToggleUI.IsChecked = false;
                    n.ExecutionResultsToggleUI.Visibility = Visibility.Collapsed;
                    NodeChrome.UpdateExecutionResultsToggleText(n.ExecutionResultsToggleUI, 0, false);
                }
                if (n.ExecutionErrorItemsPanel != null) n.ExecutionErrorItemsPanel.Children.Clear();
                if (n.ExecutionErrorItemsPanel != null) n.ExecutionErrorItemsPanel.Visibility = Visibility.Collapsed;
                if (n.ExecutionErrorToggleUI != null)
                {
                    n.ExecutionErrorToggleUI.IsChecked = false;
                    n.ExecutionErrorToggleUI.Visibility = Visibility.Collapsed;
                    NodeChrome.UpdateExecutionErrorToggleText(n.ExecutionErrorToggleUI, false);
                }
            }
        });
    }

    public void OnNodeStarted(WorkflowNode node, string? manualRunSessionId = null)
    {
        var runKey = manualRunSessionId ?? "";
        _dispatcher.BeginInvoke(GetExecutionStatusPriority(), () => StartNodeTiming(node, runKey));
    }

    public void OnNodeCompleted(WorkflowNode node, TimeSpan elapsed, string? manualRunSessionId = null)
    {
        _dispatcher.BeginInvoke(GetExecutionStatusPriority(), () =>
        {
            StopNodeTiming(node, elapsed, manualRunSessionId);
        });
        // Render results có thể nặng, giữ ở Background để không giật UI.
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            UpdateNodeExecutionResults(node);
        });
    }

    public void CancelTimersForManualRunSession(string? manualRunSessionId)
    {
        if (string.IsNullOrEmpty(manualRunSessionId)) return;
        var rk = manualRunSessionId;
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            var touchedNodes = new HashSet<WorkflowNode>();
            foreach (var kvp in _activeNodeTimers.ToList())
            {
                if (kvp.Key.RunKey != rk) continue;
                kvp.Value.Timer.Stop();
                _activeNodeTimers.Remove(kvp.Key);
                touchedNodes.Add(kvp.Key.Node);
            }

            var nodesStillActive = _activeNodeTimers.Keys
                .Select(k => k.Node)
                .ToHashSet();

            foreach (var n in touchedNodes)
            {
                if (nodesStillActive.Contains(n))
                    RefreshAggregateTimingForNode(n);
                else
                {
                    if (n.ExecutionStatusContainerUI != null)
                        n.ExecutionStatusContainerUI.Visibility = Visibility.Visible;
                    if (n.ExecutionStatusTextUI != null)
                        n.ExecutionStatusTextUI.Text = "⏹ Cancelled";
                    if (n.ExecutionBusySpinnerUI != null)
                        n.ExecutionBusySpinnerUI.Visibility = Visibility.Collapsed;
                }
            }
        }));
    }

    public void OnExecutionCancelled()
    {
        // Không chạy đồng bộ trên UI khi bấm Stop hàng loạt; luôn queue để click/UI animation không bị khựng.
        _dispatcher.BeginInvoke(GetExecutionStatusPriority(), (Action)CancelNodeTiming);
    }

    public void RefreshSavedOutputs(IEnumerable<WorkflowNode> nodes)
    {
        if (nodes == null) return;
        RunOnUi(() =>
        {
            foreach (var node in nodes)
            {
                UpdateNodeExecutionResults(node);
            }
        });
    }

    public void OnNodeFailed(WorkflowNode node, string errorMessage)
    {
        // Luôn dùng BeginInvoke để tránh đứng hình: nếu đang trên UI thread, gọi trực tiếp sẽ block
        // trong lúc build nhiều WPF controls. Đẩy sang message sau để call stack hiện tại unwind trước.
        var msg = errorMessage ?? "";
        _dispatcher.BeginInvoke(() => UpdateNodeExecutionError(node, msg), DispatcherPriority.Background);
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    private DispatcherPriority GetExecutionStatusPriority()
    {
        return DispatcherPriority.Background;
    }

    private void RefreshAggregateTimingForNode(WorkflowNode node)
    {
        var entries = _activeNodeTimers.Where(k => ReferenceEquals(k.Key.Node, node)).ToList();
        if (entries.Count == 0 || node.ExecutionStatusTextUI == null) return;

        var maxSec = entries.Max(e => e.Value.Stopwatch.Elapsed.TotalSeconds);
        var thisNodeActiveTasks = entries.Sum(e => Math.Max(1, e.Value.ActiveCount));

        if (thisNodeActiveTasks <= 1 && entries.Count <= 1)
        {
            node.ExecutionStatusTextUI.Text = $"⏳ {maxSec:0.00}s đang xử lý...";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"⏳ {maxSec:0.00}s · {thisNodeActiveTasks} luồng đang chạy:");

        int threadIdx = 1;
        foreach (var entry in entries.OrderByDescending(e => e.Value.Stopwatch.Elapsed.TotalSeconds))
        {
            var elapsed = entry.Value.Stopwatch.Elapsed.TotalSeconds;
            var runKey = string.IsNullOrWhiteSpace(entry.Key.RunKey) ? "mặc định" : entry.Key.RunKey;
            
            if (runKey.Length > 18)
            {
                runKey = runKey.Substring(0, 15) + "...";
            }

            var activeCount = Math.Max(1, entry.Value.ActiveCount);
            if (activeCount > 1)
            {
                for (int sub = 1; sub <= activeCount; sub++)
                {
                    sb.AppendLine($"  • Luồng #{threadIdx++} [{runKey} #{sub}]: {elapsed:0.00}s");
                }
            }
            else
            {
                sb.AppendLine($"  • Luồng #{threadIdx++} [{runKey}]: {elapsed:0.00}s");
            }
        }

        node.ExecutionStatusTextUI.Text = sb.ToString().TrimEnd();
    }

    private void StartNodeTiming(WorkflowNode node, string runKey)
    {
        var key = (node, runKey);
        if (_activeNodeTimers.TryGetValue(key, out var existing))
        {
            existing.ActiveCount++;
            RefreshAggregateTimingForNode(node);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            // 200ms: đủ mượt cho UX (5fps) nhưng giảm đáng kể số timer events trên Dispatcher queue
            // so với 80ms (12fps). Quan trọng khi nhiều node chạy song song = nhiều timer cùng lúc.
            Interval = TimeSpan.FromMilliseconds(200)
        };

        var nodeRef = node;
        timer.Tick += (_, __) => RefreshAggregateTimingForNode(nodeRef);

        timer.Start();
        _activeNodeTimers[key] = new ActiveNodeTiming
        {
            Timer = timer,
            Stopwatch = stopwatch,
            ActiveCount = 1
        };

        if (node.ExecutionStatusContainerUI != null)
            node.ExecutionStatusContainerUI.Visibility = Visibility.Visible;

        if (node.ExecutionBusySpinnerUI != null)
        {
            var cacheEnabled = node.ExecutionBusySpinnerUI.Tag is bool b && b;
            node.ExecutionBusySpinnerUI.Visibility = cacheEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        RefreshAggregateTimingForNode(node);
    }

    private void StopNodeTiming(WorkflowNode node, TimeSpan elapsed, string? manualRunSessionId)
    {
        var runKey = manualRunSessionId ?? "";
        var key = (node, runKey);
        if (_activeNodeTimers.TryGetValue(key, out var timerInfo))
        {
            timerInfo.ActiveCount = Math.Max(0, timerInfo.ActiveCount - 1);
            if (timerInfo.ActiveCount <= 0)
            {
                timerInfo.Timer.Stop();
                _activeNodeTimers.Remove(key);
            }
        }

        var remainingOnNode = _activeNodeTimers.Keys.Count(k => ReferenceEquals(k.Node, node));
        if (remainingOnNode > 0)
        {
            RefreshAggregateTimingForNode(node);
            return;
        }

        if (ReferenceEquals(_timingNode, node) && _activeNodeTimers.Count == 0)
            StopTimingTimer();

        if (node.ExecutionStatusContainerUI != null)
            node.ExecutionStatusContainerUI.Visibility = Visibility.Visible;

        if (node.ExecutionBusySpinnerUI != null)
            node.ExecutionBusySpinnerUI.Visibility = Visibility.Collapsed;

        if (node.ExecutionStatusTextUI != null)
        {
            var current = node.ExecutionStatusTextUI.Text ?? string.Empty;
            // Nếu node vừa bị lỗi thì giữ badge lỗi, không ghi đè thành "✅" làm user tưởng đã pass.
            if (!current.StartsWith("❌", StringComparison.Ordinal))
            {
                if (node is HttpRequestNode httpNode && httpNode.LastIsSuccess == false)
                {
                    node.ExecutionStatusTextUI.Text = $"⚠ HTTP fail {elapsed.TotalSeconds:0.00}s{BuildFlowBadge(node)}";
                }
                else
                {
                    node.ExecutionStatusTextUI.Text = $"✅ {elapsed.TotalSeconds:0.00}s{BuildFlowBadge(node)}";
                }
            }
        }
    }

    private void CancelNodeTiming()
    {
        var nodes = _activeNodeTimers.Keys.Select(k => k.Node).Distinct().ToList();
        foreach (var kvp in _activeNodeTimers.ToList())
        {
            kvp.Value.Timer.Stop();
            _activeNodeTimers.Remove(kvp.Key);
        }

        foreach (var node in nodes)
        {
            if (node.ExecutionStatusContainerUI != null)
                node.ExecutionStatusContainerUI.Visibility = Visibility.Visible;
            if (node.ExecutionStatusTextUI != null)
                node.ExecutionStatusTextUI.Text = "⏹ Cancelled";
            if (node.ExecutionBusySpinnerUI != null)
                node.ExecutionBusySpinnerUI.Visibility = Visibility.Collapsed;
        }

        if (_timingNode != null)
        {
            if (_timingNode.ExecutionStatusContainerUI != null)
                _timingNode.ExecutionStatusContainerUI.Visibility = Visibility.Visible;
            if (_timingNode.ExecutionStatusTextUI != null)
                _timingNode.ExecutionStatusTextUI.Text = "⏹ Cancelled";
            if (_timingNode.ExecutionBusySpinnerUI != null)
                _timingNode.ExecutionBusySpinnerUI.Visibility = Visibility.Collapsed;
        }

        StopTimingTimer();
    }

    private void StopTimingTimer()
    {
        if (_nodeTimingTimer != null)
        {
            _nodeTimingTimer.Stop();
            _nodeTimingTimer = null;
        }
        _nodeTimingStopwatch = null;
        _timingNode = null;
    }

    public void UpdateNodeExecutionResults(WorkflowNode node)
    {
        if (node.ExecutionResultsToggleUI == null || node.ExecutionResultsItemsPanel == null) return;

        var keysToProcess = new List<string>();
        if (node.DynamicOutputs != null)
        {
            foreach (var output in node.DynamicOutputs)
            {
                var key = output.Key?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(key) && !keysToProcess.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    keysToProcess.Add(key);
                }
            }
        }

        if (node is AsyncTaskNode asyncTaskNode && asyncTaskNode.BodyOutputMappings != null)
        {
            foreach (var mapping in asyncTaskNode.BodyOutputMappings)
            {
                var key = mapping.OutputKey?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(key) && !keysToProcess.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    keysToProcess.Add(key);
                }
            }
        }

        if (node is CodeNode codeNode)
        {
            lock (codeNode.ResolvedOutputsSyncRoot)
            {
                foreach (var k in codeNode.ResolvedOutputs.Keys)
                {
                    var trimmed = k?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(trimmed) && !keysToProcess.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                    {
                        keysToProcess.Add(trimmed);
                    }
                }
            }
        }

        if (keysToProcess.Count == 0) return;

        var results = new List<(string Key, string RawValue, bool IsArray, List<string> ArrayItems)>();

        foreach (var key in keysToProcess)
        {
            var value = NodeDataPanelService.ResolveDynamicValueByKey(node, key);
            if (string.IsNullOrWhiteSpace(value) || value == "—") continue;

            if (TryParseJsonArrayItems(value, out var items) && items.Count > 0)
            {
                results.Add((key, value, true, items));
            }
            else
            {
                results.Add((key, value, false, new List<string>()));
            }
        }

        var panel = node.ExecutionResultsItemsPanel;
        bool isExpanded = node.ExecutionResultsToggleUI.IsChecked == true;

        if (results.Count == 0)
        {
            panel.Children.Clear();
            if (isExpanded)
            {
                panel.Visibility = Visibility.Visible;
                node.ExecutionResultsToggleUI.Visibility = Visibility.Visible;
                NodeChrome.UpdateExecutionResultsToggleText(node.ExecutionResultsToggleUI, 0, true);

                var emptyBlock = new TextBlock
                {
                    Text = "Chưa có kết quả trả về",
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(4, 2, 4, 2)
                };
                emptyBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
                panel.Children.Add(emptyBlock);
            }
            else
            {
                panel.Visibility = Visibility.Collapsed;
                node.ExecutionResultsToggleUI.Visibility = Visibility.Collapsed;
                NodeChrome.UpdateExecutionResultsToggleText(node.ExecutionResultsToggleUI, 0, false);
            }
            return;
        }

        node.ExecutionResultsToggleUI.Visibility = Visibility.Visible;
        NodeChrome.UpdateExecutionResultsToggleText(node.ExecutionResultsToggleUI, results.Count, isExpanded);
        panel.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;

        if (!isExpanded)
        {
            panel.Children.Clear();
            return;
        }

        panel.Children.Clear();

        foreach (var result in results)
        {
            if (result.IsArray)
            {
                var container = new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 5, 0, 0),
                    MaxWidth = 330
                };
                container.SetResourceReference(Border.BackgroundProperty, "HeaderBackgroundBrush");
                container.SetResourceReference(Border.BorderBrushProperty, "ControlBorderBrush");

                var toggleKey = result.Key.Replace("_", "__");
                var itemCount = result.ArrayItems.Count;

                var toggle = new ToggleButton
                {
                    Content = $"▸ ⚡ {toggleKey}: [{itemCount} items]",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 2),
                    Padding = new Thickness(4, 2, 4, 2),
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var amberNormalBg = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber 500
                var amberHoverBg = new SolidColorBrush(Color.FromRgb(251, 191, 36));  // Amber 400
                var amberBorder = new SolidColorBrush(Color.FromRgb(217, 119, 6));    // Amber 600
                var amberText = Brushes.Black;

                Action refreshArrayToggleVisual = () =>
                {
                    bool isExpanded = toggle.IsChecked == true;
                    bool isHover = toggle.IsMouseOver;
                    var arrow = isExpanded ? "▾" : "▸";
                    toggle.Content = $"{arrow} ⚡ {toggleKey}: [{itemCount} items]";

                    if (isExpanded || isHover)
                    {
                        toggle.Background = isHover ? amberHoverBg : amberNormalBg;
                        toggle.Foreground = amberText;
                        toggle.BorderBrush = amberBorder;
                    }
                    else
                    {
                        toggle.SetResourceReference(ToggleButton.ForegroundProperty, "ChocolateBrownBrush");
                        toggle.SetResourceReference(ToggleButton.BackgroundProperty, "HeaderBackgroundBrush");
                        toggle.SetResourceReference(ToggleButton.BorderBrushProperty, "HeaderBackgroundBrush");
                    }
                };

                refreshArrayToggleVisual();
                toggle.MouseEnter += (s, e) => refreshArrayToggleVisual();
                toggle.MouseLeave += (s, e) => refreshArrayToggleVisual();

                var itemsPanel = new StackPanel
                {
                    Margin = new Thickness(6, 4, 0, 0),
                    Visibility = Visibility.Collapsed,
                    MaxWidth = 320
                };

                const int MaxPreviewCharsItem = 150;

                for (int i = 0; i < result.ArrayItems.Count; i++)
                {
                    var value = result.ArrayItems[i] ?? string.Empty;
                    var isLongItem = value.Length > MaxPreviewCharsItem;
                    var previewItem = isLongItem
                        ? value.Substring(0, MaxPreviewCharsItem) + "..."
                        : value;

                    var itemContainer = new StackPanel
                    {
                        Margin = new Thickness(0, i == 0 ? 0 : 4, 0, 0),
                        Orientation = Orientation.Vertical,
                        MaxWidth = 320
                    };

                    var indexRun = new Run($"[{i}]")
                    {
                        FontWeight = FontWeights.Bold,
                        FontFamily = new FontFamily("Consolas, Segoe UI")
                    };
                    indexRun.SetResourceReference(Run.ForegroundProperty, "PrimaryBrush");

                    var textRun = new Run($" {previewItem}")
                    {
                        FontFamily = new FontFamily("Consolas, Segoe UI")
                    };
                    textRun.SetResourceReference(Run.ForegroundProperty, "TextBrush");

                    var collapsedText = new TextBlock
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        FontSize = 10.5,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 320
                    };
                    collapsedText.Inlines.Add(indexRun);
                    collapsedText.Inlines.Add(textRun);

                    TextBlock? fullTextBlock = null;
                    ScrollViewer? fullScroll = null;
                    Button? btnToggle = null;
                    Button? btnCopyItem = null;

                    if (isLongItem)
                    {
                        var fullIndexRun = new Run($"[{i}]")
                        {
                            FontWeight = FontWeights.Bold,
                            FontFamily = new FontFamily("Consolas, Segoe UI")
                        };
                        fullIndexRun.SetResourceReference(Run.ForegroundProperty, "PrimaryBrush");

                        var fullTextRun = new Run($" {value}")
                        {
                            FontFamily = new FontFamily("Consolas, Segoe UI")
                        };
                        fullTextRun.SetResourceReference(Run.ForegroundProperty, "TextPrimary");

                        fullTextBlock = new TextBlock
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            FontSize = 10.5,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 320
                        };
                        fullTextBlock.Inlines.Add(fullIndexRun);
                        fullTextBlock.Inlines.Add(fullTextRun);

                        fullScroll = new ScrollViewer
                        {
                            Content = fullTextBlock,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            MaxHeight = 180,
                            Visibility = Visibility.Collapsed,
                            Margin = new Thickness(0, 2, 0, 0),
                            MaxWidth = 320
                        };

                        var capturedIndex = i;
                        var fallbackValue = value;

                        TextBlock? labelToggleRef = null;
                        var (toggleBtn, lblToggle) = CreateMicroActionButton(
                            "🔍 Xem thêm",
                            "PrimaryBrush",
                            "PrimaryHoverBrush",
                            "TextOnPrimaryBrush",
                            new Thickness(0, 2, 4, 0),
                            (s, e) =>
                            {
                                if (fullScroll!.Visibility == Visibility.Collapsed)
                                {
                                    fullScroll.Visibility = Visibility.Visible;
                                    collapsedText.Visibility = Visibility.Collapsed;
                                    if (labelToggleRef != null) labelToggleRef.Text = "▲ Thu gọn";
                                }
                                else
                                {
                                    fullScroll.Visibility = Visibility.Collapsed;
                                    collapsedText.Visibility = Visibility.Visible;
                                    if (labelToggleRef != null) labelToggleRef.Text = "🔍 Xem thêm";
                                }
                            });
                        btnToggle = toggleBtn;
                        labelToggleRef = lblToggle;

                        var (copyItemBtn, _) = CreateMicroActionButton(
                            "📋 Copy",
                            "SecondaryBrush",
                            "SecondaryHoverBrush",
                            "TextOnSecondaryBrush",
                            new Thickness(0, 2, 0, 0),
                            (s, e) =>
                            {
                                try
                                {
                                    var fullArrayStr = NodeDataPanelService.ResolveDynamicValueByKey(node, result.Key, forDisplay: false);
                                    if (TryParseJsonArrayItems(fullArrayStr, out var fullItems) && capturedIndex < fullItems.Count)
                                    {
                                        Clipboard.SetText(fullItems[capturedIndex]);
                                    }
                                    else
                                    {
                                        Clipboard.SetText(fallbackValue);
                                    }
                                }
                                catch { }
                            });
                        btnCopyItem = copyItemBtn;

                        fullScroll.PreviewMouseWheel += (s, e) =>
                        {
                            var sv = s as ScrollViewer;
                            if (sv != null)
                            {
                                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                                e.Handled = true;
                            }
                        };
                    }

                    itemContainer.Children.Add(collapsedText);
                    if (isLongItem && fullScroll != null && btnToggle != null)
                    {
                        var buttonsPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Left
                        };
                        buttonsPanel.Children.Add(btnToggle);
                        buttonsPanel.Children.Add(btnCopyItem!);
                        itemContainer.Children.Add(buttonsPanel);
                        itemContainer.Children.Add(fullScroll);
                    }
                    else
                    {
                        var capturedShortIndex = i;
                        var fallbackShortValue = value;
                        var (btnCopyShort, _) = CreateMicroActionButton(
                            "📋 Copy",
                            "SecondaryBrush",
                            "SecondaryHoverBrush",
                            "TextOnSecondaryBrush",
                            new Thickness(0, 2, 0, 0),
                            (s, e) =>
                            {
                                try
                                {
                                    var fullArrayStr = NodeDataPanelService.ResolveDynamicValueByKey(node, result.Key, forDisplay: false);
                                    if (TryParseJsonArrayItems(fullArrayStr, out var fullItems) && capturedShortIndex < fullItems.Count)
                                    {
                                        Clipboard.SetText(fullItems[capturedShortIndex]);
                                    }
                                    else
                                    {
                                        Clipboard.SetText(fallbackShortValue);
                                    }
                                }
                                catch { }
                            });
                        itemContainer.Children.Add(btnCopyShort);
                    }

                    itemsPanel.Children.Add(itemContainer);
                }

                var scroll = new ScrollViewer
                {
                    Content = itemsPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    MaxHeight = 260,
                    MaxWidth = 320,
                    Margin = new Thickness(0, 0, 0, 0)
                };
                scroll.Visibility = Visibility.Collapsed;

                scroll.PreviewMouseWheel += (s, e) =>
                {
                    var sv = s as ScrollViewer;
                    if (sv != null)
                    {
                        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                        e.Handled = true;
                    }
                };

                toggle.Checked += (s, e) =>
                {
                    itemsPanel.Visibility = Visibility.Visible;
                    scroll.Visibility = Visibility.Visible;
                    refreshArrayToggleVisual();
                };
                toggle.Unchecked += (s, e) =>
                {
                    itemsPanel.Visibility = Visibility.Collapsed;
                    scroll.Visibility = Visibility.Collapsed;
                    refreshArrayToggleVisual();
                };

                var itemStack = new StackPanel();
                itemStack.Children.Add(toggle);
                itemStack.Children.Add(scroll);
                container.Child = itemStack;
                panel.Children.Add(container);
            }
            else
            {
                const int MaxPreviewChars = 150;
                var isLong = result.RawValue.Length > MaxPreviewChars;
                var preview = isLong
                    ? result.RawValue.Substring(0, MaxPreviewChars) + "..."
                    : result.RawValue;

                var container = new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 5, 0, 0),
                    MaxWidth = 330
                };
                container.SetResourceReference(Border.BackgroundProperty, "HeaderBackgroundBrush");
                container.SetResourceReference(Border.BorderBrushProperty, "ControlBorderBrush");

                var keyRun = new Run($"⚡ {result.Key}: ")
                {
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas, Segoe UI")
                };
                keyRun.SetResourceReference(Run.ForegroundProperty, "PrimaryBrush");

                var previewRun = new Run(preview)
                {
                    FontFamily = new FontFamily("Consolas, Segoe UI")
                };
                previewRun.SetResourceReference(Run.ForegroundProperty, "TextBrush");

                var collapsedText = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontSize = 10.5,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 320
                };
                collapsedText.Inlines.Add(keyRun);
                collapsedText.Inlines.Add(previewRun);

                var fullKeyRun = new Run($"⚡ {result.Key}: ")
                {
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas, Segoe UI")
                };
                fullKeyRun.SetResourceReference(Run.ForegroundProperty, "PrimaryBrush");

                var displayFullValue = NodeDataPanelService.IsBase64Value(result.Key, result.RawValue)
                    ? NodeDataPanelService.TruncateBase64ForDisplay(result.RawValue, 300)
                    : (result.RawValue.Length > 2000 ? result.RawValue.Substring(0, 2000) + "..." : result.RawValue);

                var fullValueRun = new Run(displayFullValue)
                {
                    FontFamily = new FontFamily("Consolas, Segoe UI")
                };
                fullValueRun.SetResourceReference(Run.ForegroundProperty, "TextPrimary");

                var fullTextBlock = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontSize = 10.5,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 320
                };
                fullTextBlock.Inlines.Add(fullKeyRun);
                fullTextBlock.Inlines.Add(fullValueRun);

                var fullScroll = new ScrollViewer
                {
                    Content = fullTextBlock,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    MaxHeight = 180,
                    Visibility = Visibility.Collapsed,
                    Margin = new Thickness(0, 2, 0, 0),
                    MaxWidth = 320
                };

                fullScroll.PreviewMouseWheel += (s, e) =>
                {
                    var sv = s as ScrollViewer;
                    if (sv != null)
                    {
                        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                        e.Handled = true;
                    }
                };

                var (btnCopy, _) = CreateMicroActionButton(
                    "📋 Copy",
                    "SecondaryBrush",
                    "SecondaryHoverBrush",
                    "TextOnSecondaryBrush",
                    new Thickness(0, 4, 0, 0),
                    (s, e) =>
                    {
                        try
                        {
                            var fullVal = NodeDataPanelService.ResolveDynamicValueByKey(node, result.Key, forDisplay: false);
                            if (string.IsNullOrWhiteSpace(fullVal) || fullVal == "—") fullVal = result.RawValue;
                            Clipboard.SetText(fullVal);
                        }
                        catch { }
                    });

                var itemStack = new StackPanel();
                itemStack.Children.Add(collapsedText);
                if (isLong)
                {
                    TextBlock? labelToggleRef = null;
                    var (btnToggle, lblToggle) = CreateMicroActionButton(
                        "🔍 Xem thêm",
                        "PrimaryBrush",
                        "PrimaryHoverBrush",
                        "TextOnPrimaryBrush",
                        new Thickness(0, 4, 6, 0),
                        (s, e) =>
                        {
                            if (fullScroll.Visibility == Visibility.Collapsed)
                            {
                                fullScroll.Visibility = Visibility.Visible;
                                collapsedText.Visibility = Visibility.Collapsed;
                                if (labelToggleRef != null) labelToggleRef.Text = "▲ Thu gọn";
                            }
                            else
                            {
                                fullScroll.Visibility = Visibility.Collapsed;
                                collapsedText.Visibility = Visibility.Visible;
                                if (labelToggleRef != null) labelToggleRef.Text = "🔍 Xem thêm";
                            }
                        });
                    labelToggleRef = lblToggle;

                    var buttonsPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    buttonsPanel.Children.Add(btnToggle);
                    buttonsPanel.Children.Add(btnCopy);
                    itemStack.Children.Add(buttonsPanel);
                    itemStack.Children.Add(fullScroll);
                }
                else
                {
                    itemStack.Children.Add(btnCopy);
                }

                container.Child = itemStack;
                panel.Children.Add(container);
            }
        }

        if (node.ExecutionStatusContainerUI != null)
        {
            node.ExecutionStatusContainerUI.Visibility = Visibility.Visible;
        }

        var expanded = node.ExecutionResultsToggleUI.IsChecked == true;
        node.ExecutionResultsToggleUI.Visibility = Visibility.Visible;
        NodeChrome.UpdateExecutionResultsToggleText(node.ExecutionResultsToggleUI, results.Count, expanded);
        panel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateNodeExecutionError(WorkflowNode node, string errorMessage)
    {
        if (node.ExecutionStatusContainerUI == null || node.ExecutionStatusTextUI == null) return;
        if (node.ExecutionErrorToggleUI == null || node.ExecutionErrorItemsPanel == null) return;

        var activeForNode = _activeNodeTimers.Where(k => ReferenceEquals(k.Key.Node, node)).ToList();
        double elapsedSec = 0;
        if (activeForNode.Count > 0)
        {
            elapsedSec = activeForNode.Max(e => e.Value.Stopwatch.Elapsed.TotalSeconds);
            foreach (var kvp in activeForNode)
            {
                kvp.Value.Timer.Stop();
                _activeNodeTimers.Remove(kvp.Key);
            }
        }

        if (ReferenceEquals(_timingNode, node) && _activeNodeTimers.Count == 0)
            StopTimingTimer();

        node.ExecutionStatusContainerUI.Visibility = Visibility.Visible;
        var elapsedBadge = elapsedSec > 0 ? $" {elapsedSec:0.00}s" : "";
        node.ExecutionStatusTextUI.Text = $"❌ Lỗi{elapsedBadge}{BuildFlowBadge(node)}";

        if (node.ExecutionBusySpinnerUI != null)
            node.ExecutionBusySpinnerUI.Visibility = Visibility.Collapsed;

        var panel = node.ExecutionErrorItemsPanel;
        panel.Children.Clear();

        const int MaxPreviewChars = 150;
        var isLong = errorMessage.Length > MaxPreviewChars;
        var preview = isLong ? errorMessage.Substring(0, MaxPreviewChars) + "..." : errorMessage;

        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 0), MaxWidth = 300 };

        var keyRun = new Run("- Lỗi:")
        {
            FontWeight = FontWeights.Bold
        };
        keyRun.SetResourceReference(Run.ForegroundProperty, "DangerBrush");
        var previewRun = new Run($" {preview}");

        var collapsedText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            FontSize = 11,
            Opacity = 0.95,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300
        };
        collapsedText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
        collapsedText.Inlines.Add(keyRun);
        collapsedText.Inlines.Add(previewRun);

        var fullKeyRun = new Run("- Lỗi:")
        {
            FontWeight = FontWeights.Bold
        };
        fullKeyRun.SetResourceReference(Run.ForegroundProperty, "DangerBrush");
        var fullValueRun = new Run($" {errorMessage}");

        var fullTextBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            FontSize = 11,
            Opacity = 0.95,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300
        };
        fullTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        fullTextBlock.Inlines.Add(fullKeyRun);
        fullTextBlock.Inlines.Add(fullValueRun);

        var fullScroll = new ScrollViewer
        {
            Content = fullTextBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 200,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 0),
            MaxWidth = 300
        };

        fullScroll.PreviewMouseWheel += (s, e) =>
        {
            if (s is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                e.Handled = true;
            }
        };

        var capturedError = errorMessage;
        var (btnCopyError, _) = CreateMicroActionButton(
            "📋 Copy Lỗi",
            "DangerBrush",
            "DangerHoverBrush",
            "TextOnDangerBrush",
            new Thickness(0, 4, 0, 0),
            (_, _) =>
            {
                try { System.Windows.Clipboard.SetText(capturedError); }
                catch { }
            });

        container.Children.Add(collapsedText);
        if (isLong)
        {
            TextBlock? labelErrorToggleRef = null;
            var (btnToggleError, lblErrorToggle) = CreateMicroActionButton(
                "🔍 Xem chi tiết",
                "PrimaryBrush",
                "PrimaryHoverBrush",
                "TextOnPrimaryBrush",
                new Thickness(0, 4, 6, 0),
                (_, _) =>
                {
                    if (fullScroll.Visibility == Visibility.Collapsed)
                    {
                        fullScroll.Visibility = Visibility.Visible;
                        collapsedText.Visibility = Visibility.Collapsed;
                        if (labelErrorToggleRef != null) labelErrorToggleRef.Text = "▲ Thu gọn";
                    }
                    else
                    {
                        fullScroll.Visibility = Visibility.Collapsed;
                        collapsedText.Visibility = Visibility.Visible;
                        if (labelErrorToggleRef != null) labelErrorToggleRef.Text = "🔍 Xem chi tiết";
                    }
                });
            labelErrorToggleRef = lblErrorToggle;

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            buttonsPanel.Children.Add(btnToggleError);
            buttonsPanel.Children.Add(btnCopyError);
            container.Children.Add(buttonsPanel);
            container.Children.Add(fullScroll);
        }
        else
        {
            container.Children.Add(btnCopyError);
        }

        panel.Children.Add(container);

        node.ExecutionErrorToggleUI.Visibility = Visibility.Visible;
        var expanded = node.ExecutionErrorToggleUI.IsChecked == true;
        NodeChrome.UpdateExecutionErrorToggleText(node.ExecutionErrorToggleUI, expanded);
        panel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool TryParseJsonArrayItems(string raw, out List<string> items)
    {
        items = new List<string>();
        if (string.IsNullOrWhiteSpace(raw) || raw == "—") return false;

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

    private string BuildParallelActivityBadgeForNode(
        WorkflowNode node,
        List<KeyValuePair<(WorkflowNode Node, string RunKey), ActiveNodeTiming>> nodeEntries)
    {
        if (nodeEntries == null || nodeEntries.Count == 0) return string.Empty;

        // Một node có thể chạy ở nhiều runKey khác nhau (ví dụ nhiều phiên).
        // Chọn runKey đang có nhiều task active nhất để hiển thị ngữ cảnh song song rõ nhất.
        var selectedRunKey = nodeEntries
            .GroupBy(e => e.Key.RunKey ?? string.Empty, StringComparer.Ordinal)
            .OrderByDescending(g => g.Sum(x => Math.Max(1, x.Value.ActiveCount)))
            .Select(g => g.Key)
            .FirstOrDefault() ?? string.Empty;

        var sameRunEntries = _activeNodeTimers
            .Where(k => string.Equals(k.Key.RunKey ?? string.Empty, selectedRunKey, StringComparison.Ordinal))
            .ToList();

        if (sameRunEntries.Count == 0) return string.Empty;

        var sameRunNodes = sameRunEntries
            .Select(e => e.Key.Node)
            .Where(n => n != null)
            .Distinct()
            .ToList();

        var activeTasksInRun = sameRunEntries.Sum(e => Math.Max(1, e.Value.ActiveCount));
        var activeNodesInRun = sameRunNodes.Count;

        var thisNodeTasksInRun = sameRunEntries
            .Where(e => ReferenceEquals(e.Key.Node, node))
            .Sum(e => Math.Max(1, e.Value.ActiveCount));
        var otherTasks = Math.Max(0, activeTasksInRun - thisNodeTasksInRun);
        var otherNodes = Math.Max(0, activeNodesInRun - 1);

        if (otherTasks <= 0) return string.Empty;

        var otherNodeGroups = sameRunEntries
            .Where(e => !ReferenceEquals(e.Key.Node, node))
            .GroupBy(e => e.Key.Node)
            .Select(g => new
            {
                Node = g.Key,
                Count = g.Sum(x => Math.Max(1, x.Value.ActiveCount))
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Node?.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var details = string.Join(", ", otherNodeGroups.Select(g =>
        {
            var title = string.IsNullOrWhiteSpace(g.Node?.Title) ? "Node" : g.Node!.Title;
            return $"{title}({g.Count})";
        }));

        return string.IsNullOrWhiteSpace(details)
            ? $" · song song: {otherTasks} tác vụ/{otherNodes} node"
            : $" · song song: {otherTasks} tác vụ/{otherNodes} node · {details}";
    }

    private static string BuildFlowBadge(WorkflowNode node)
    {
        var scope = node.LastFlowScopeId;
        var branch = node.LastBranchId;
        var execution = node.LastExecutionId;
        if (string.IsNullOrWhiteSpace(scope) && string.IsNullOrWhiteSpace(branch) && string.IsNullOrWhiteSpace(execution))
            return string.Empty;

        var scopePart = string.IsNullOrWhiteSpace(scope) ? "?" : scope;
        var branchPart = string.IsNullOrWhiteSpace(branch) ? "main" : branch;
        var execPart = string.IsNullOrWhiteSpace(execution)
            ? "no-run"
            : (execution.Length > 8 ? execution.Substring(0, 8) : execution);
        return $" [{scopePart}|{branchPart}|{execPart}]";
    }

    private static (Button Button, TextBlock Label) CreateMicroActionButton(
        string text,
        string normalBgKey,
        string hoverBgKey,
        string textBrushKey,
        Thickness margin,
        RoutedEventHandler onClick)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 10.5,
            FontWeight = FontWeights.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, textBrushKey);

        var border = new Border
        {
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 3, 8, 3),
            Child = label
        };
        border.SetResourceReference(Border.BackgroundProperty, normalBgKey);

        var btn = new Button
        {
            Content = border,
            Margin = margin,
            HorizontalAlignment = HorizontalAlignment.Left,
            Height = 26,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Style = Application.Current.TryFindResource("TransparentButtonStyle") as Style
        };

        btn.MouseEnter += (s, e) =>
        {
            border.SetResourceReference(Border.BackgroundProperty, hoverBgKey);
        };
        btn.MouseLeave += (s, e) =>
        {
            border.SetResourceReference(Border.BackgroundProperty, normalBgKey);
        };

        btn.Click += onClick;
        return (btn, label);
    }
}


