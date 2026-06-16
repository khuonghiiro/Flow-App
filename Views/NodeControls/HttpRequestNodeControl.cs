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

            var isCurlAvailable = CurlNativeExecutor.IsCurlThinAvailable;

            var curlBadge = new Border
            {
                Background = isCurlAvailable 
                    ? new SolidColorBrush(Color.FromRgb(30, 41, 59)) 
                    : new SolidColorBrush(Color.FromRgb(59, 30, 30)),
                BorderBrush = isCurlAvailable 
                    ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) 
                    : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2, 0, 2, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -5, -5),
                Visibility = node.UseCurl ? Visibility.Visible : Visibility.Collapsed,
                IsHitTestVisible = true,
                ToolTip = isCurlAvailable ? "Sử dụng libcurl bypass" : "Lỗi: Chưa cài thư viện libcurl"
            };
            var curlText = new TextBlock
            {
                Text = isCurlAvailable ? "⚡" : "⚠️",
                Foreground = isCurlAvailable 
                    ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) 
                    : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            curlBadge.Child = curlText;
            grid.Children.Add(curlBadge);

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
                [nameof(HttpRequestNode.UseCurl)] = ctx =>
                {
                    curlBadge.Visibility = node.UseCurl ? Visibility.Visible : Visibility.Collapsed;
                    if (node.UseCurl)
                    {
                        var available = CurlNativeExecutor.IsCurlThinAvailable;
                        curlBadge.Background = available 
                            ? new SolidColorBrush(Color.FromRgb(30, 41, 59)) 
                            : new SolidColorBrush(Color.FromRgb(59, 30, 30));
                        curlBadge.BorderBrush = available 
                            ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) 
                            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        curlBadge.ToolTip = available ? "Sử dụng libcurl bypass" : "Lỗi: Chưa cài thư viện libcurl";
                        curlText.Text = available ? "⚡" : "⚠️";
                        curlText.Foreground = available 
                            ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) 
                            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
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
