using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using FlowMy.Services.Utils;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    public static class HttpRequestNodeControl
    {
        public static Border CreateBorder(HttpRequestNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // --- Create UI elements (node-specific) ---

            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "globe-pointer sharp-duotone-light",
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

            var curlBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(3, 1, 3, 1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -5, -5),
                Visibility = node.UseCurl ? Visibility.Visible : Visibility.Collapsed,
                IsHitTestVisible = true,
                ToolTip = "Sử dụng cURL (Bypass Cloudflare/Anti-bot)"
            };
            var curlText = new TextBlock
            {
                Text = "cURL",
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            curlBadge.Child = curlText;
            grid.Children.Add(curlBadge);

            var streamBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2, 0, 2, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(-5, 0, 0, -5),
                Visibility = node.IsStream ? Visibility.Visible : Visibility.Collapsed,
                IsHitTestVisible = true,
                ToolTip = "Phản hồi thời gian thực (Real-time Stream)"
            };
            var streamText = new TextBlock
            {
                Text = "📡",
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            streamBadge.Child = streamText;
            grid.Children.Add(streamBadge);

            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "HTTP Request",
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
                [nameof(HttpRequestNode.IsStream)] = ctx =>
                {
                    streamBadge.Visibility = node.IsStream ? Visibility.Visible : Visibility.Collapsed;
                },
                [nameof(HttpRequestNode.UseCurl)] = ctx =>
                {
                    curlBadge.Visibility = node.UseCurl ? Visibility.Visible : Visibility.Collapsed;
                }
            };

            // --- Initialize with fluent API ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new HttpRequestNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        private static Brush GetTextBrush(string? colorKey)
        {
            return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }
    }
}
