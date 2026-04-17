using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using System.ComponentModel;
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
            null, typeof(Uri), "book-arrow-right duotone", System.Globalization.CultureInfo.CurrentCulture) as Uri;
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

        var title = new TextBlock
        {
            Text = node.Title ?? "Flow Overwrite",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = node.NodeBrush,
            IsHitTestVisible = false
        };
        node.TitleTextBlockUI = title;

        if (node is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WorkflowNode.Title))
                    title.Text = node.Title ?? "Flow Overwrite";
                if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    border.Background = node.NodeBrush;
            };
        }

        border.Loaded += (_, _) =>
        {
            if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(title))
            {
                host.WorkflowCanvas.Children.Add(title);
                Panel.SetZIndex(title, 20000);
            }
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
