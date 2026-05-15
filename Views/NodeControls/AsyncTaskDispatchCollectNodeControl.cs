using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.NodeControls
{
    public static class AsyncTaskDispatchCollectNodeControl
    {
        public static Border CreateBorder(AsyncTaskDispatchCollectNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // --- Internal title text (displayed inside the border) ---
            var titleText = new TextBlock
            {
                Text = node.Title ?? "Collect",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            var border = new Border
            {
                Width = 150,
                Height = 80,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = GpuOptimizationHelper.CreateDropShadowEffect(),
                Tag = node,
                Child = titleText
            };

            // Floating title TextBlock (required by BaseNodeControlHelper; kept Collapsed since
            // this node uses an internal title text instead of a floating one above the border).
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Collect",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    BaseNodeControlHelper.GetTitleColorMode(node),
                    BaseNodeControlHelper.GetTitleColorKey(node),
                    node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

            node.TitleTextBlockUI = titleTextBlock;

            // --- Node-specific property handler: sync internal title text when Title/NodeBrush changes ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.Title)] = ctx =>
                {
                    titleText.Text = node.Title ?? "Collect";
                    ctx.TitleTextBlock.Text = node.Title ?? "Collect";
                },
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    border.Background = node.NodeBrush;
                    ctx.TitleTextBlock.Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                        BaseNodeControlHelper.GetTitleColorMode(node),
                        BaseNodeControlHelper.GetTitleColorKey(node),
                        node.NodeBrush);
                }
            };

            // --- Initialize with fluent API ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new AsyncTaskDispatchCollectNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .Build();

            return border;
        }
    }
}
