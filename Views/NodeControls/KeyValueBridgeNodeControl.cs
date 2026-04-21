using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Views.Overlays;
using FlowMy.Views;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;

namespace FlowMy.Views.NodeControls;

public static class KeyValueBridgeNodeControl
{
    private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> TitleUpdateTimers = new();
    private const int TitleUpdateThrottleMs = 50;
    private static readonly System.Collections.Generic.Dictionary<Border, bool> TitleUpdatedAfterZoom = new();
    private static readonly System.Collections.Generic.Dictionary<KeyValueBridgeNode, DispatcherTimer> _pollTimers = new();

    /// <summary>
    /// LayoutUpdated chạy theo mọi vòng layout (kể cả node khác trong AsyncTask body). Bỏ qua khi input tính vị trí title không đổi để tránh spam throttle/timer.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<Border, TitleLayoutSignature> LastTitleLayoutByBorder = new();

    private readonly struct TitleLayoutSignature(
        double borderLeft,
        double borderTop,
        double borderW,
        double borderH,
        double titleW,
        double titleH)
    {
        public double BorderLeft { get; } = borderLeft;
        public double BorderTop { get; } = borderTop;
        public double BorderW { get; } = borderW;
        public double BorderH { get; } = borderH;
        public double TitleW { get; } = titleW;
        public double TitleH { get; } = titleH;
    }

    private const double TitleLayoutEpsilon = 0.4;

    private static bool TryReadTitleLayoutSignature(Border border, TextBlock titleTextBlock, out TitleLayoutSignature sig)
    {
        var left = Canvas.GetLeft(border);
        var top = Canvas.GetTop(border);
        if (double.IsNaN(left) && border.Tag is WorkflowNode wn)
            left = wn.X;
        if (double.IsNaN(top) && border.Tag is WorkflowNode wn2)
            top = wn2.Y;
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;
        sig = new TitleLayoutSignature(
            left,
            top,
            border.ActualWidth,
            border.ActualHeight,
            titleTextBlock.ActualWidth,
            titleTextBlock.ActualHeight);
        return true;
    }

    private static bool TitleLayoutSignaturesMatch(TitleLayoutSignature a, TitleLayoutSignature b)
    {
        return Math.Abs(a.BorderLeft - b.BorderLeft) < TitleLayoutEpsilon
               && Math.Abs(a.BorderTop - b.BorderTop) < TitleLayoutEpsilon
               && Math.Abs(a.BorderW - b.BorderW) < TitleLayoutEpsilon
               && Math.Abs(a.BorderH - b.BorderH) < TitleLayoutEpsilon
               && Math.Abs(a.TitleW - b.TitleW) < TitleLayoutEpsilon
               && Math.Abs(a.TitleH - b.TitleH) < TitleLayoutEpsilon;
    }

    private static void RememberTitleLayoutSignature(Border border, TextBlock titleTextBlock)
    {
        if (TryReadTitleLayoutSignature(border, titleTextBlock, out var sig))
            LastTitleLayoutByBorder[border] = sig;
    }

    private static int GetPollIntervalMs(KeyValueBridgeNode node)
    {
        if (node == null) return 0;
        if (node.IsPassKeyMode) return 0;
        if (node.PollIntervalValue <= 0) return 0;
        return node.PollIntervalUnit switch
        {
            KeyValueBridgePollUnit.Seconds => node.PollIntervalValue * 1000,
            KeyValueBridgePollUnit.Minutes => node.PollIntervalValue * 60_000,
            _ => node.PollIntervalValue // milliseconds
        };
    }

    private static void StopPollTimer(KeyValueBridgeNode node)
    {
        if (node == null) return;
        if (_pollTimers.TryGetValue(node, out var timer))
        {
            timer.Stop();
            _pollTimers.Remove(node);
        }
    }

    private static void RestartPollTimer(KeyValueBridgeNode node, IWorkflowEditorHost host)
    {
        if (node == null || host == null) return;
        StopPollTimer(node);

        if (node.IsPassKeyMode) return;

        var intervalMs = GetPollIntervalMs(node);
        if (intervalMs <= 0) return;

        // Best-effort: run immediately so user sees output without waiting one interval.
        try { host.RequestRunSingleNode(node); } catch { }

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(intervalMs)
        };
        timer.Tick += (s, e) =>
        {
            try { host.RequestRunSingleNode(node); } catch { }
        };

        _pollTimers[node] = timer;
        timer.Start();
    }

    public static Border CreateBorder(KeyValueBridgeNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));

        var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

        var iconConverter = new IconKeyToPathConverter();
        var iconUri = iconConverter.Convert(null, typeof(Uri), "list-check solid", System.Globalization.CultureInfo.CurrentCulture) as Uri;
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

        var titleTextBlock = new TextBlock
        {
            Text = node.Title ?? "KeyValue Bridge",
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

        if (node is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (_, e293) =>
            {
                if (e293.PropertyName == nameof(WorkflowNode.ColorKey))
                    iconSvg.Fill = GetTextBrush(node.ColorKey);
                else if (e293.PropertyName == nameof(WorkflowNode.NodeBrush))
                {
                    border.Background = node.NodeBrush;
                    if (node.TitleColorMode == TitleColorMode.NodeColor)
                        titleTextBlock.Foreground = node.NodeBrush;
                }
                else if (e293.PropertyName == nameof(KeyValueBridgeNode.TitleColorMode) || e293.PropertyName == nameof(KeyValueBridgeNode.TitleColorKey))
                    titleTextBlock.Foreground = GetTitleBrush(node);
                else if (e293.PropertyName == nameof(WorkflowNode.Title))
                {
                    titleTextBlock.Text = node.Title ?? "KeyValue Bridge";
                    if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        UpdateTitlePosition(titleTextBlock, border, host);
                }
                else if (e293.PropertyName == nameof(KeyValueBridgeNode.TitleDisplayMode))
                {
                    if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                }
                else if (e293.PropertyName == nameof(KeyValueBridgeNode.IsPassKeyMode))
                {
                    host.RequestSyncDataPanels(immediate: true);
                    RestartPollTimer(node, host);
                    TryRefreshOpenKeyValueBridgeDeleteTargets(host);
                }
                else if (e293.PropertyName == nameof(KeyValueBridgeNode.EnableDataCleanup))
                {
                    host.RequestSyncDataPanels(immediate: true);
                    try { host.UpdateNodePosition(node, node.X, node.Y); } catch { }
                    TryRefreshOpenKeyValueBridgeDeleteTargets(host);
                }
                else if (e293.PropertyName == nameof(KeyValueBridgeNode.PollIntervalValue) ||
                         e293.PropertyName == nameof(KeyValueBridgeNode.PollIntervalUnit))
                {
                    RestartPollTimer(node, host);
                }
            };
        }

        border.Focusable = true;
        border.FocusVisualStyle = null;

        border.MouseEnter += (_, _) =>
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

        border.MouseLeave += (_, _) =>
        {
            isHovering = false;
            if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
        };

        // Keyboard Port Position: Arrow = Port IN, Shift+Arrow = Port OUT
        border.PreviewKeyDown += (_, e) =>
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

        border.MouseRightButtonUp += (_, e293) =>
        {
            e293.Handled = true;
            OpenNodeDialog(node, host, ownerWindow);
        };

        var visibilityDescriptor = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
        visibilityDescriptor?.AddValueChanged(border, (_, _) =>
        {
            if (border.Visibility != Visibility.Visible)
                titleTextBlock.Visibility = Visibility.Collapsed;
            else
                UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
        });

        border.Loaded += (_, EVx) =>
        {
            if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
            {
                host.WorkflowCanvas.Children.Add(titleTextBlock);
                Panel.SetZIndex(titleTextBlock, 20000);
                UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                UpdateTitlePosition(titleTextBlock, border, host);
            }

                // Poll Get-mode (unchecked) on interval even without needing workflow traversal.
                RestartPollTimer(node, host);
        };

        border.SizeChanged += (_, EVx) => UpdateTitlePosition(titleTextBlock, border, host);

        border.Unloaded += (_, EVx) =>
        {
            try
            {
                    StopPollTimer(node);
                if (TitleUpdateTimers.TryGetValue(border, out var timer))
                {
                    timer.Stop();
                    TitleUpdateTimers.Remove(border);
                }
                TitleUpdatedAfterZoom.Remove(border);
                LastTitleLayoutByBorder.Remove(border);

                if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
                    host.WorkflowCanvas.Children.Remove(titleTextBlock);

                if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                    node.TitleTextBlockUI = null;
            }
            catch { }
        };

        border.LayoutUpdated += (_, EVx) =>
        {
            if (border.Visibility != Visibility.Visible)
            {
                titleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            if (NodeChrome.IsZooming)
            {
                if (titleTextBlock.Visibility != Visibility.Collapsed)
                    titleTextBlock.Visibility = Visibility.Collapsed;
                TitleUpdatedAfterZoom[border] = false;
                return;
            }

            bool hasUpdatedAfterZoom = TitleUpdatedAfterZoom.TryGetValue(border, out var updated) && updated;
            if (!hasUpdatedAfterZoom && border.Visibility == Visibility.Visible)
            {
                TitleUpdatedAfterZoom[border] = true;
                UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                if (titleTextBlock.Visibility == Visibility.Visible)
                    UpdateTitlePosition(titleTextBlock, border, host);
            }

            if (host.IsPanning || host.DraggedNode == node)
                return;

            if (titleTextBlock.Visibility == Visibility.Visible)
            {
                if (TryReadTitleLayoutSignature(border, titleTextBlock, out var curSig)
                    && LastTitleLayoutByBorder.TryGetValue(border, out var prevSig)
                    && TitleLayoutSignaturesMatch(curSig, prevSig))
                {
                    return;
                }

                ThrottledUpdateTitlePosition(titleTextBlock, border, host);
            }
        };

        return border;
    }

    private static void OpenNodeDialog(KeyValueBridgeNode node, IWorkflowEditorHost host, Window? ownerWindow)
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

            var dialog = new KeyValueBridgeNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
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

    /// <summary>Dialog cleanup đang mở: làm mới combobox node đích khi bất kỳ KVB nào đổi Pass hoặc Xoá dữ liệu.</summary>
    private static void TryRefreshOpenKeyValueBridgeDeleteTargets(IWorkflowEditorHost host)
    {
        try
        {
            var dm = GetOrCreateDialogManager(host);
            if (!dm.IsDialogOpen || dm.GetCurrentDialog() is not KeyValueBridgeNodeDialog dlg)
                return;
            dlg.RefreshDeleteBridgeNodeOptionsForOpenDialog();
        }
        catch { }
    }

    private static Brush GetTextBrush(string? colorKey)
    {
        if (string.IsNullOrWhiteSpace(colorKey))
            return new SolidColorBrush(Color.FromRgb(148, 163, 184));
        return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
    }

    private static Brush GetTitleBrush(KeyValueBridgeNode node)
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

    private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering) => mode switch
    {
        TitleDisplayMode.Hidden => Visibility.Collapsed,
        TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
        TitleDisplayMode.Always => Visibility.Visible,
        _ => Visibility.Collapsed
    };

    private static void UpdateTitleVisibility(TextBlock titleTextBlock, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
    {
        if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible)
        {
            titleTextBlock.Visibility = Visibility.Collapsed;
            return;
        }
        titleTextBlock.Visibility = GetTitleVisibility(mode, isHovering);
    }

    private static void ThrottledUpdateTitlePosition(TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
    {
        if (!TitleUpdateTimers.TryGetValue(border, out var timer))
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
            timer.Tick += (_, EVx) =>
            {
                timer.Stop();
                UpdateTitlePosition(titleTextBlock, border, host);
            };
            TitleUpdateTimers[border] = timer;
        }

        timer.Stop();
        timer.Start();
    }

    private static void UpdateTitlePosition(TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
    {
        if (host.WorkflowCanvas == null || border == null || !host.WorkflowCanvas.Children.Contains(titleTextBlock)) return;

        var left = Canvas.GetLeft(border);
        var top = Canvas.GetTop(border);

        if (double.IsNaN(left) && border.Tag is WorkflowNode wn)
            left = wn.X;
        if (double.IsNaN(top) && border.Tag is WorkflowNode wn2)
            top = wn2.Y;

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

        RememberTitleLayoutSignature(border, titleTextBlock);
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