using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls;

public static class FlowOverwriteNodeControl
{
    public static Border CreateBorder(FlowOverwriteNode node, Window? ownerWindow, IWorkflowEditorHost host)
    {
        var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };
        var iconUri = new IconKeyToPathConverter().Convert(
            null, typeof(Uri), "merge sharp-regular", System.Globalization.CultureInfo.CurrentCulture) as Uri;
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
            BorderBrush = Brushes.White,
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
            Text = node.Title ?? "Flow Overwrite",
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

        // --- Node-specific property handler: update icon fill when ColorKey changes ---
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
            }
        };

        // --- Initialize with fluent API ---
        BaseNodeControlHelper
            .Initialize(border, titleTextBlock, node, host)
            .WithTitleManagement()
            .WithHoverBehavior()
            .WithKeyboardPorts()
            .WithPropertySync(customPropertyHandlers)
            .WithDialogSupport(ctx => new FlowOverwriteNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
            .WithCleanup()
            .WithVisibilitySync()
            .WithCanvasIntegration()
            .Build();

        return border;
    }

    private static Brush GetIconBrush(string? colorKey)
        => Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush ?? Brushes.WhiteSmoke;
}
