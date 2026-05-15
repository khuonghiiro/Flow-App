using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Header hình thoi cho AsyncTask ở chế độ giao diện giống Loop.
    /// </summary>
    public static class AsyncTaskLoopDiamondControl
    {
        public static Border CreateBorder(AsyncTaskNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            const double diamondWidth = 100;
            const double diamondHeight = 100;

            var diamond = new Polygon
            {
                Points = new PointCollection(new[]
                {
                    new Point(diamondWidth / 2, 0),
                    new Point(diamondWidth, diamondHeight / 2),
                    new Point(diamondWidth / 2, diamondHeight),
                    new Point(0, diamondHeight / 2)
                }),
                Fill = node.NodeBrush,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                Stretch = Stretch.Fill
            };

            var grid = new Grid
            {
                Width = diamondWidth,
                Height = diamondHeight,
                MinWidth = diamondWidth,
                MinHeight = diamondHeight,
                ClipToBounds = false
            };
            grid.Children.Add(diamond);

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "diagram-project duotone-light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 28,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                Fill = Application.Current.TryFindResource("TextOnMintChocolateBrush") as Brush ?? Brushes.White
            };
            grid.Children.Add(iconSvg);

            var titleBrush = Application.Current.TryFindResource("TextOnMintChocolateBrush") as Brush
                ?? new SolidColorBrush(Colors.White);
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Async Task",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = titleBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 2, 4, 0),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = diamondWidth - 4,
                Visibility = Visibility.Visible,
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            var border = new Border
            {
                Child = grid,
                Width = diamondWidth,
                Height = diamondHeight,
                MinWidth = diamondWidth,
                MinHeight = diamondHeight,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ClipToBounds = false,
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

            // Khi kéo (drag) diamond: giảm artifact dạng "nền vuông" do shadow bitmap.
            var shadowEffect = border.Effect as System.Windows.Media.Effects.DropShadowEffect;
            border.PreviewMouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    border.Effect = null;
            };
            border.PreviewMouseUp += (s, e) =>
            {
                if (shadowEffect != null)
                    border.Effect = shadowEffect;
            };

            // --- Node-specific property handlers: sync diamond fill and title text ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.Title)] = ctx =>
                {
                    ctx.TitleTextBlock.Text = node.Title ?? "Async Task";
                },
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    diamond.Fill = node.NodeBrush;
                    ctx.TitleTextBlock.Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                        BaseNodeControlHelper.GetTitleColorMode(node),
                        BaseNodeControlHelper.GetTitleColorKey(node),
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
                .WithDialogSupport(ctx => new AsyncTaskNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }
    }
}
