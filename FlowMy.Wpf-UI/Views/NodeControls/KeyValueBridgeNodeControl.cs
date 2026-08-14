using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Linq;

namespace FlowMy.Views.NodeControls;

public static class KeyValueBridgeNodeControl
{
    // Poll timers are node-specific and not managed by BaseNodeControlHelper.
    private static readonly Dictionary<KeyValueBridgeNode, DispatcherTimer> _pollTimers = new();

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

        // --- Create UI elements (node-specific) ---

        var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

        var iconConverter = new IconKeyToPathConverter();
        var iconUri = iconConverter.Convert(null, typeof(Uri), "list-check solid",
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

        var titleTextBlock = new TextBlock
        {
            Text = node.Title ?? "KeyValue Bridge",
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

        // --- Node-specific property handlers ---
        var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
        {
            [nameof(WorkflowNode.ColorKey)] = ctx =>
            {
                iconSvg.Fill = GetTextBrush(node.ColorKey);
            },
            [nameof(KeyValueBridgeNode.IsPassKeyMode)] = ctx =>
            {
                host.RequestSyncDataPanels(immediate: true);
                RestartPollTimer(node, host);
                TryRefreshOpenKeyValueBridgeDeleteTargets(host);
            },
            [nameof(KeyValueBridgeNode.EnableDataCleanup)] = ctx =>
            {
                host.RequestSyncDataPanels(immediate: true);
                try { host.UpdateNodePosition(node, node.X, node.Y); } catch { }
                TryRefreshOpenKeyValueBridgeDeleteTargets(host);
            },
            [nameof(KeyValueBridgeNode.PollIntervalValue)] = ctx =>
            {
                RestartPollTimer(node, host);
            },
            [nameof(KeyValueBridgeNode.PollIntervalUnit)] = ctx =>
            {
                RestartPollTimer(node, host);
            }
        };

        // --- Initialize with fluent API ---
        BaseNodeControlHelper
            .Initialize(border, titleTextBlock, node, host)
            .WithTitleManagement()
            .WithHoverBehavior()
            .WithKeyboardPorts()
            .WithPropertySync(customPropertyHandlers)
            .WithDialogSupport(ctx => new KeyValueBridgeNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
            .WithCleanup()
            .WithVisibilitySync()
            .WithCanvasIntegration()
            .Build();

        // --- Poll timer: start on Loaded, stop on Unloaded (node-specific lifecycle) ---
        border.Loaded += (s, e) =>
        {
            RestartPollTimer(node, host);
        };

        border.Unloaded += (s, e) =>
        {
            try { StopPollTimer(node); } catch { }
        };

        return border;
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

    private static Brush GetTextBrush(string? colorKey)
    {
        if (string.IsNullOrWhiteSpace(colorKey))
            return new SolidColorBrush(Color.FromRgb(148, 163, 184));
        return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
    }
}
