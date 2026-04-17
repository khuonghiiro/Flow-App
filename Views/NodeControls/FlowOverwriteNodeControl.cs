using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.NodeControls;

public static class FlowOverwriteNodeControl
{
    public static Border CreateBorder(FlowOverwriteNode node, Window? ownerWindow, IWorkflowEditorHost host)
    {
        var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };
        var iconUri = new IconKeyToPathConverter().Convert(
            null, typeof(Uri), "merge sharp-regular", System.Globalization.CultureInfo.CurrentCulture) as Uri;
        grid.Children.Add(new SvgViewboxEx
        {
            Source = iconUri,
            Width = 32,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = GetTextBrush(node.ColorKey)
        });

        var border = new Border
        {
            Child = grid,
            Background = node.NodeBrush,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            Tag = node
        };
        border.Focusable = true;
        border.FocusVisualStyle = null;

        var title = new TextBlock
        {
            Text = node.Title ?? "Flow Overwrite",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = node.NodeBrush,
            IsHitTestVisible = false
        };
        node.TitleTextBlockUI = title;
        bool isHovering = false;

        if (node is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WorkflowNode.Title))
                    title.Text = node.Title ?? "Flow Overwrite";
                if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                {
                    border.Background = node.NodeBrush;
                    title.Foreground = GetTitleBrush(node);
                }
                if (e.PropertyName == nameof(FlowOverwriteNode.TitleColorMode) ||
                    e.PropertyName == nameof(FlowOverwriteNode.TitleColorKey))
                {
                    title.Foreground = GetTitleBrush(node);
                }
                if (e.PropertyName == nameof(FlowOverwriteNode.TitleDisplayMode))
                {
                    UpdateTitleVisibility(title, node.TitleDisplayMode, isHovering, border);
                }
            };
        }

        border.MouseEnter += (_, _) =>
        {
            isHovering = true;
            UpdateTitleVisibility(title, node.TitleDisplayMode, true, border);
            UpdateTitlePosition(title, border, host);
            Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input,
                new Action(() => { if (isHovering) border.Focus(); }));
        };
        border.MouseLeave += (_, _) =>
        {
            isHovering = false;
            UpdateTitleVisibility(title, node.TitleDisplayMode, false, border);
        };

        border.PreviewKeyDown += (_, e) =>
        {
            if (!isHovering) return;
            var isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            PortPosition? pos = e.Key switch
            {
                Key.Left => PortPosition.Left,
                Key.Up => PortPosition.Top,
                Key.Right => PortPosition.Right,
                Key.Down => PortPosition.Bottom,
                _ => null
            };
            if (pos == null) return;
            e.Handled = true;
            ChangePortPosition(node, pos.Value, isInputPort: !isShift, host);
        };

        border.Loaded += (_, _) =>
        {
            if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(title))
            {
                host.WorkflowCanvas.Children.Add(title);
                Panel.SetZIndex(title, 20000);
            }
            title.Foreground = GetTitleBrush(node);
            UpdateTitleVisibility(title, node.TitleDisplayMode, isHovering, border);
            UpdateTitlePosition(title, border, host);
        };
        border.SizeChanged += (_, _) => UpdateTitlePosition(title, border, host);
        border.Unloaded += (_, _) =>
        {
            if (host.WorkflowCanvas?.Children.Contains(title) == true)
                host.WorkflowCanvas.Children.Remove(title);
            if (ReferenceEquals(node.TitleTextBlockUI, title))
                node.TitleTextBlockUI = null;
        };

        border.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (node.Border?.IsMouseCaptured == true) node.Border.ReleaseMouseCapture();
            host.DraggedNode = null;
            host.ViewModel!.SelectedNode = null;
            var dm = GetOrCreateDialogManager(host);
            if (dm.IsDialogOpen && dm.CurrentNode != node) dm.CloseCurrentDialog();
            if (!dm.IsDialogOpen || dm.CurrentNode != node)
                dm.OpenDialog(node, new FlowOverwriteNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow), host);
        };

        return border;
    }

    private static void UpdateTitlePosition(TextBlock tb, Border border, IWorkflowEditorHost host)
    {
        if (host.WorkflowCanvas == null || !host.WorkflowCanvas.Children.Contains(tb)) return;
        var left = Canvas.GetLeft(border);
        var top = Canvas.GetTop(border);
        if (tb.ActualWidth == 0 || tb.ActualHeight == 0)
        {
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            tb.Arrange(new Rect(tb.DesiredSize));
        }
        Canvas.SetLeft(tb, left + (border.ActualWidth / 2) - (tb.ActualWidth / 2));
        Canvas.SetTop(tb, top - tb.ActualHeight - 4);
    }

    private static Brush GetTextBrush(string? colorKey)
        => Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush ?? Brushes.WhiteSmoke;

    private static Brush GetTitleBrush(FlowOverwriteNode node)
    {
        if (node.TitleColorMode != TitleColorMode.CustomColor ||
            string.IsNullOrWhiteSpace(node.TitleColorKey) ||
            node.TitleColorKey == "NodeColor")
        {
            return node.NodeBrush;
        }
        return Application.Current.TryFindResource(node.TitleColorKey) as Brush ?? node.NodeBrush;
    }

    private static void UpdateTitleVisibility(TextBlock title, TitleDisplayMode mode, bool isHovering, Border border)
    {
        if (border.Visibility != Visibility.Visible)
        {
            title.Visibility = Visibility.Collapsed;
            return;
        }
        title.Visibility = mode switch
        {
            TitleDisplayMode.Hidden => Visibility.Collapsed,
            TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
            _ => Visibility.Visible
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
}
