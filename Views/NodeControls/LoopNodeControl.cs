using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Views.NodeControls
{
    public static class LoopNodeControl
    {
        public static Border CreateBorder(LoopNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // Diamond shape dimensions
            const double diamondWidth = 100;
            const double diamondHeight = 100;

            // Create diamond shape using Polygon
            var diamond = new Polygon
            {
                Points = new PointCollection(new[]
                {
                    new Point(diamondWidth / 2, 0),            // Top
                    new Point(diamondWidth, diamondHeight / 2), // Right
                    new Point(diamondWidth / 2, diamondHeight), // Bottom
                    new Point(0, diamondHeight / 2)            // Left
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

            // Icon in center
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "arrows-spin duotone",
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

            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Loop",
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
                Width = diamondWidth,
                Height = diamondHeight,
                MinWidth = diamondWidth,
                MinHeight = diamondHeight,
                // Transparent background — diamond polygon provides the visual
                Background = Brushes.Transparent,
                BorderBrush = null,
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

            // --- Node-specific property handlers ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.ColorKey)] = ctx =>
                {
                    iconSvg.Fill = GetIconBrush(node.ColorKey);
                },
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    // Diamond fill tracks NodeBrush; title foreground resolved via base helper
                    diamond.Fill = node.NodeBrush;
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
                .WithDialogSupport(ctx => new LoopNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            // Keep background transparent after hover/drag (diamond shape must not show square)
            border.MouseDown += (s, e) =>
            {
                border.Background = Brushes.Transparent;
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => border.Background = Brushes.Transparent));
            };
            border.MouseUp += (s, e) =>
            {
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => border.Background = Brushes.Transparent));
            };
            border.PreviewMouseUp += (s, e) =>
            {
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => border.Background = Brushes.Transparent));
            };
            border.LayoutUpdated += (s, e) =>
            {
                if (!ReferenceEquals(border.Background, Brushes.Transparent))
                    border.Background = Brushes.Transparent;
            };

            return border;
        }

        /// <summary>
        /// Resolves the icon fill brush for the given color key using the theme resource format
        /// "TextOn{colorKey}Brush" (e.g., "TextOnWarningBrush").
        /// Falls back to a neutral slate color when the key is not found.
        /// </summary>
        private static Brush GetIconBrush(string? colorKey)
        {
            if (!string.IsNullOrEmpty(colorKey))
            {
                var brush = Application.Current?.TryFindResource($"TextOn{colorKey}Brush") as Brush;
                if (brush != null) return brush;
            }
            // Fallback: use TextOnWarningBrush (original default for LoopNode)
            var fallback = Application.Current?.TryFindResource("TextOnWarningBrush") as Brush;
            return fallback ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }
    }
}
