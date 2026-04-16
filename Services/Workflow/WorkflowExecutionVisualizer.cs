using FlowMy.Models;
using FlowMy.Services.Rendering;
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
    
    // Mỗi (node, runKey) một timer — runKey = id phiên thủ công hoặc executionId lane để nhiều luồng trùng node không bị bỏ qua.
    private readonly Dictionary<(WorkflowNode Node, string RunKey), (DispatcherTimer Timer, Stopwatch Stopwatch)> _activeNodeTimers = new();

    public WorkflowExecutionVisualizer()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
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
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () => StartNodeTiming(node, runKey));
    }

    public void OnNodeCompleted(WorkflowNode node, TimeSpan elapsed, string? manualRunSessionId = null)
    {
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            StopNodeTiming(node, elapsed, manualRunSessionId);
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
                }
            }
        }));
    }

    public void OnExecutionCancelled()
    {
        // Không chạy đồng bộ trên UI khi bấm Stop hàng loạt; luôn queue để click/UI animation không bị khựng.
        _dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)CancelNodeTiming);
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

    private void RefreshAggregateTimingForNode(WorkflowNode node)
    {
        var entries = _activeNodeTimers.Where(k => ReferenceEquals(k.Key.Node, node)).ToList();
        if (entries.Count == 0 || node.ExecutionStatusTextUI == null) return;

        var maxSec = entries.Max(e => e.Value.Stopwatch.Elapsed.TotalSeconds);
        var parallelBadge = BuildParallelActivityBadgeForNode(node, entries);
        var badge = BuildFlowBadge(node);
        node.ExecutionStatusTextUI.Text = entries.Count > 1
            ? $"⏳ {maxSec:0.00}s · {entries.Count} luồng{parallelBadge}{badge}"
            : $"⏳ {maxSec:0.00}s{parallelBadge}{badge}";
    }

    private void StartNodeTiming(WorkflowNode node, string runKey)
    {
        var key = (node, runKey);
        if (_activeNodeTimers.ContainsKey(key))
            return;

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
        _activeNodeTimers[key] = (timer, stopwatch);

        if (node.ExecutionStatusContainerUI != null)
            node.ExecutionStatusContainerUI.Visibility = Visibility.Visible;

        RefreshAggregateTimingForNode(node);
    }

    private void StopNodeTiming(WorkflowNode node, TimeSpan elapsed, string? manualRunSessionId)
    {
        var runKey = manualRunSessionId ?? "";
        var key = (node, runKey);
        if (_activeNodeTimers.TryGetValue(key, out var timerInfo))
        {
            timerInfo.Timer.Stop();
            _activeNodeTimers.Remove(key);
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
        }

        if (_timingNode != null)
        {
            if (_timingNode.ExecutionStatusContainerUI != null)
                _timingNode.ExecutionStatusContainerUI.Visibility = Visibility.Visible;
            if (_timingNode.ExecutionStatusTextUI != null)
                _timingNode.ExecutionStatusTextUI.Text = "⏹ Cancelled";
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

    private void UpdateNodeExecutionResults(WorkflowNode node)
    {
        if (node.DynamicOutputs == null || node.DynamicOutputs.Count == 0) return;
        if (node.ExecutionResultsToggleUI == null || node.ExecutionResultsItemsPanel == null) return;

        var results = new List<(string Key, string RawValue, bool IsArray, List<string> ArrayItems)>();

        foreach (var output in node.DynamicOutputs)
        {
            var key = output.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)) continue;

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
        panel.Children.Clear();

        if (results.Count == 0)
        {
            panel.Visibility = Visibility.Collapsed;
            node.ExecutionResultsToggleUI.Visibility = Visibility.Collapsed;
            NodeChrome.UpdateExecutionResultsToggleText(node.ExecutionResultsToggleUI, 0, false);
            return;
        }

        foreach (var result in results)
        {
            if (result.IsArray)
            {
                var container = new StackPanel
                {
                    Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 4, 0, 0),
                    MaxWidth = 300,
                };

                var toggle = new ToggleButton
                {
                    Content = $"- {result.Key}: [{result.ArrayItems.Count} item]",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Foreground = Application.Current.TryFindResource("ChocolateBrownBrush") as Brush,
                    Margin = new Thickness(0, 0, 0, 2),
                    Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var itemsPanel = new StackPanel
                {
                    Margin = new Thickness(12, 0, 0, 0),
                    Visibility = Visibility.Collapsed,
                    MaxWidth = 300
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
                        Margin = new Thickness(0, i == 0 ? 0 : 2, 0, 0),
                        Orientation = Orientation.Vertical,
                        MaxWidth = 300
                    };

                    // Phần index nổi bật
                    var indexRun = new Run($"[{i}]")
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = Application.Current.TryFindResource("ChocolateBrownBrush") as Brush
                                           ?? Application.Current.TryFindResource("PrimaryBrush") as Brush
                                           ?? Brushes.DeepSkyBlue
                    };

                    var textRun = new Run($" {previewItem}");

                    var collapsedText = new TextBlock
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Foreground = Application.Current.TryFindResource("PrimaryBrush") as Brush,
                        FontSize = 11,
                        Opacity = 0.95,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 300
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
                            Foreground = indexRun.Foreground
                        };
                        var fullTextRun = new Run($" {value}");

                        fullTextBlock = new TextBlock
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Foreground = Application.Current.TryFindResource("CharcoalDarkBrush") as Brush,
                            FontSize = 11,
                            Opacity = 0.95,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 300
                        };
                        fullTextBlock.Inlines.Add(fullIndexRun);
                        fullTextBlock.Inlines.Add(fullTextRun);

                        fullScroll = new ScrollViewer
                        {
                            Content = fullTextBlock,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            MaxHeight = 200,
                            Visibility = Visibility.Collapsed,
                            Margin = new Thickness(0, 2, 0, 0),
                            MaxWidth = 300
                        };

                        btnToggle = new Button
                        {
                            Content = "Xem thêm",
                            FontSize = 10,
                            Padding = new Thickness(4, 1, 4, 1),
                            Margin = new Thickness(0, 2, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Width = 60,
                            Height = 25,
                            Style = Application.Current.TryFindResource("PrimaryButton") as Style,
                            //Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                            //BorderBrush = Brushes.Transparent,
                            //BorderThickness = new Thickness(0),
                            Cursor = System.Windows.Input.Cursors.Hand
                        };

                        btnCopyItem = new Button
                        {
                            Content = "Copy",
                            FontSize = 10,
                            Padding = new Thickness(4, 1, 4, 1),
                            Margin = new Thickness(4, 2, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Width = 40,
                            Height = 25,
                            Style = Application.Current.TryFindResource("DangerButton") as Style,
                            //Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                            //BorderBrush = Brushes.Transparent,
                            //BorderThickness = new Thickness(0),
                            Cursor = System.Windows.Input.Cursors.Hand
                        };

                        var capturedValue = value;
                        btnCopyItem.Click += (s, e) =>
                        {
                            try
                            {
                                Clipboard.SetText(capturedValue);
                            }
                            catch { }
                        };

                        btnToggle.Click += (s, e) =>
                        {
                            if (fullScroll!.Visibility == Visibility.Collapsed)
                            {
                                fullScroll.Visibility = Visibility.Visible;
                                collapsedText.Visibility = Visibility.Collapsed;
                                btnToggle.Content = "Thu gọn";
                            }
                            else
                            {
                                fullScroll.Visibility = Visibility.Collapsed;
                                collapsedText.Visibility = Visibility.Visible;
                                btnToggle.Content = "Xem thêm";
                            }
                        };

                        // Ngăn zoom canvas khi scroll trong ScrollViewer
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
                        // Copy button cho short items
                        var btnCopyShort = new Button
                        {
                            Content = "Copy",
                            FontSize = 10,
                            Padding = new Thickness(4, 1, 4, 1),
                            Margin = new Thickness(4, 2, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Width = 40,
                            Height = 25,
                            Style = Application.Current.TryFindResource("DangerButton") as Style,
                            //Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                            //BorderBrush = Brushes.Transparent,
                            //BorderThickness = new Thickness(0),
                            Cursor = System.Windows.Input.Cursors.Hand
                        };

                        var capturedShortValue = value;
                        btnCopyShort.Click += (s, e) =>
                        {
                            try
                            {
                                Clipboard.SetText(capturedShortValue);
                            }
                            catch { }
                        };
                        itemContainer.Children.Add(btnCopyShort);
                    }

                    itemsPanel.Children.Add(itemContainer);
                }

                // Luôn dùng ScrollViewer với MaxHeight = 300 để tự động scroll khi cần
                // (kể cả khi <= 10 items, nếu tổng chiều cao vượt quá 300px)
                var scroll = new ScrollViewer
                {
                    Content = itemsPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    MaxHeight = 300,
                    MaxWidth = 300,
                    Margin = new Thickness(0, 0, 0, 0)
                };
                scroll.Visibility = Visibility.Collapsed;

                // Ngăn zoom canvas khi scroll trong ScrollViewer của array items
                scroll.PreviewMouseWheel += (s, e) =>
                {
                    var sv = s as ScrollViewer;
                    if (sv != null)
                    {
                        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                        e.Handled = true;
                    }
                };

                var itemsHost = scroll;

                toggle.Checked += (s, e) =>
                {
                    itemsPanel.Visibility = Visibility.Visible;
                    scroll.Visibility = Visibility.Visible;
                };
                toggle.Unchecked += (s, e) =>
                {
                    itemsPanel.Visibility = Visibility.Collapsed;
                    scroll.Visibility = Visibility.Collapsed;
                };

                container.Children.Add(toggle);
                container.Children.Add(itemsHost);
                panel.Children.Add(container);
            }
            else
            {
                const int MaxPreviewChars = 150;
                var isLong = result.RawValue.Length > MaxPreviewChars;
                var preview = isLong
                    ? result.RawValue.Substring(0, MaxPreviewChars) + "..."
                    : result.RawValue;

                var container = new StackPanel
                {
                    Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 4, 0, 0),
                    MaxWidth = 300
                };

                // Phần key nổi bật
                var keyRun = new Run($"- {result.Key}:")
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = Application.Current.TryFindResource("ChocolateBrownBrush") as Brush
                                       ?? Application.Current.TryFindResource("PrimaryBrush") as Brush
                                       ?? Brushes.DeepSkyBlue
                };

                var previewRun = new Run($" {preview}");

                var collapsedText = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Foreground = Application.Current.TryFindResource("PrimaryBrush") as Brush,
                    FontSize = 11,
                    Opacity = 0.95,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 300
                };
                collapsedText.Inlines.Add(keyRun);
                collapsedText.Inlines.Add(previewRun);

                var fullKeyRun = new Run($"- {result.Key}:")
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = keyRun.Foreground
                };

                var fullValueRun = new Run($" {result.RawValue}");

                var fullTextBlock = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Foreground = Application.Current.TryFindResource("CharcoalDarkBrush") as Brush,
                    FontSize = 11,
                    Opacity = 0.95,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 300
                };
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

                // Ngăn zoom canvas khi scroll trong ScrollViewer
                fullScroll.PreviewMouseWheel += (s, e) =>
                {
                    var sv = s as ScrollViewer;
                    if (sv != null)
                    {
                        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                        e.Handled = true;
                    }
                };

                // Button Copy cho value (luôn hiển thị)
                var btnCopy = new Button
                {
                    Content = "Copy",
                    FontSize = 10,
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(4, 2, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 40,
                    Height = 25,
                    Style = Application.Current.TryFindResource("DangerButton") as Style,
                    //Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                    //BorderBrush = Brushes.Transparent,
                    //BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var capturedRawValue = result.RawValue;
                btnCopy.Click += (s, e) =>
                {
                    try
                    {
                        Clipboard.SetText(capturedRawValue);
                    }
                    catch { }
                };

                container.Children.Add(collapsedText);
                if (isLong)
                {
                    var btnToggle = new Button
                    {
                        Content = "Xem thêm",
                        FontSize = 10,
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(0, 2, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = 60,
                        Height = 25,
                        Style = Application.Current.TryFindResource("PrimaryButton") as Style,
                        //Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                        //BorderBrush = Brushes.Transparent,
                        //BorderThickness = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };

                    btnToggle.Click += (s, e) =>
                    {
                        if (fullScroll.Visibility == Visibility.Collapsed)
                        {
                            fullScroll.Visibility = Visibility.Visible;
                            collapsedText.Visibility = Visibility.Collapsed;
                            btnToggle.Content = "Thu gọn";
                        }
                        else
                        {
                            fullScroll.Visibility = Visibility.Collapsed;
                            collapsedText.Visibility = Visibility.Visible;
                            btnToggle.Content = "Xem thêm";
                        }
                    };

                    var buttonsPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    buttonsPanel.Children.Add(btnToggle);
                    buttonsPanel.Children.Add(btnCopy);
                    container.Children.Add(buttonsPanel);
                    container.Children.Add(fullScroll);
                }
                else
                {
                    container.Children.Add(btnCopy);
                }

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

        // Dừng timing timer để không còn ghi đè status thành "⏳ X.XXs"
        if (ReferenceEquals(_timingNode, node))
            StopTimingTimer();

        node.ExecutionStatusContainerUI.Visibility = Visibility.Visible;
        node.ExecutionStatusTextUI.Text = $"❌ Lỗi{BuildFlowBadge(node)}";

        var panel = node.ExecutionErrorItemsPanel;
        panel.Children.Clear();

        const int MaxPreviewChars = 150;
        var isLong = errorMessage.Length > MaxPreviewChars;
        var preview = isLong ? errorMessage.Substring(0, MaxPreviewChars) + "..." : errorMessage;

        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 0), MaxWidth = 300 };

        var keyRun = new Run("- Lỗi:")
        {
            FontWeight = FontWeights.Bold,
            Foreground = Application.Current.TryFindResource("ChocolateBrownBrush") as Brush
                                   ?? Application.Current.TryFindResource("PrimaryBrush") as Brush
                                   ?? Brushes.DarkRed
        };
        var previewRun = new Run($" {preview}");

        var collapsedText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = Application.Current.TryFindResource("PrimaryBrush") as Brush ?? Brushes.DarkRed,
            FontSize = 11,
            Opacity = 0.95,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300
        };
        collapsedText.Inlines.Add(keyRun);
        collapsedText.Inlines.Add(previewRun);

        var fullKeyRun = new Run("- Lỗi:")
        {
            FontWeight = FontWeights.Bold,
            Foreground = keyRun.Foreground
        };
        var fullValueRun = new Run($" {errorMessage}");

        var fullTextBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = Application.Current.TryFindResource("CharcoalDarkBrush") as Brush ?? Brushes.DarkRed,
            FontSize = 11,
            Opacity = 0.95,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300
        };
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

        var btnCopy = new Button
        {
            Content = "Copy",
            FontSize = 10,
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(4, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 40,
            Height = 25,
            Style = Application.Current.TryFindResource("DangerButton") as Style,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var capturedError = errorMessage;
        btnCopy.Click += (_, _) =>
        {
            try { System.Windows.Clipboard.SetText(capturedError); }
            catch { }
        };

        container.Children.Add(collapsedText);
        if (isLong)
        {
            var btnToggle = new Button
            {
                Content = "Xem thêm",
                FontSize = 10,
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 60,
                Height = 25,
                Style = Application.Current.TryFindResource("PrimaryButton") as Style,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnToggle.Click += (_, _) =>
            {
                if (fullScroll.Visibility == Visibility.Collapsed)
                {
                    fullScroll.Visibility = Visibility.Visible;
                    collapsedText.Visibility = Visibility.Collapsed;
                    btnToggle.Content = "Thu gọn";
                }
                else
                {
                    fullScroll.Visibility = Visibility.Collapsed;
                    collapsedText.Visibility = Visibility.Visible;
                    btnToggle.Content = "Xem thêm";
                }
            };
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            buttonsPanel.Children.Add(btnToggle);
            buttonsPanel.Children.Add(btnCopy);
            container.Children.Add(buttonsPanel);
            container.Children.Add(fullScroll);
        }
        else
        {
            container.Children.Add(btnCopy);
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
        List<KeyValuePair<(WorkflowNode Node, string RunKey), (DispatcherTimer Timer, Stopwatch Stopwatch)>> nodeEntries)
    {
        if (nodeEntries == null || nodeEntries.Count == 0) return string.Empty;

        // Một node có thể chạy ở nhiều runKey khác nhau (ví dụ nhiều phiên).
        // Chọn runKey đang có nhiều task active nhất để hiển thị ngữ cảnh song song rõ nhất.
        var selectedRunKey = nodeEntries
            .GroupBy(e => e.Key.RunKey ?? string.Empty, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
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

        var activeTasksInRun = sameRunEntries.Count;
        var activeNodesInRun = sameRunNodes.Count;

        var thisNodeTasksInRun = sameRunEntries.Count(e => ReferenceEquals(e.Key.Node, node));
        var otherTasks = Math.Max(0, activeTasksInRun - thisNodeTasksInRun);
        var otherNodes = Math.Max(0, activeNodesInRun - 1);

        if (otherTasks <= 0) return string.Empty;

        var otherNodeGroups = sameRunEntries
            .Where(e => !ReferenceEquals(e.Key.Node, node))
            .GroupBy(e => e.Key.Node)
            .Select(g => new
            {
                Node = g.Key,
                Count = g.Count()
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
}


