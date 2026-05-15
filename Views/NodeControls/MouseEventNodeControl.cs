using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Custom UI builder cho MouseEventNode (sự kiện chuột).
    /// </summary>
    public static class MouseEventNodeControl
    {
        public static Border CreateBorder(MouseEventNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // --- Create UI elements (node-specific) ---

            var grid = new Grid
            {
                MinWidth = 60,
                MinHeight = 60,
                Width = 60,
                Height = 60
            };

            var iconConverter = new IconKeyToPathConverter();
            var defaultIconUri = iconConverter.Convert(null, typeof(Uri), "computer-mouse duotone", System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = defaultIconUri,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetTextBrush(node.ColorKey)
            };
            grid.Children.Add(iconSvg);

            // Update icon based on initial MouseButton value
            UpdateIcon(iconSvg, node.MouseButton ?? "Left");

            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Mouse Event",
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
                [nameof(MouseEventNode.MouseButton)] = ctx =>
                {
                    UpdateIcon(iconSvg, node.MouseButton ?? "Left");
                }
            };

            // --- Initialize with fluent API ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new MouseEventNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        private static void UpdateIcon(SvgViewboxEx iconSvg, string mouseButton)
        {
            var iconConverter = new IconKeyToPathConverter();
            string iconKey = mouseButton switch
            {
                "Left"   => "computer-mouse-button-left duotone-light",
                "Right"  => "computer-mouse-button-right duotone-light",
                "Middle" or "ScrollUp" or "ScrollDown" => "computer-mouse-scrollwheel duotone",
                _        => "computer-mouse duotone"
            };

            var iconUri = iconConverter.Convert(null, typeof(Uri), iconKey, System.Globalization.CultureInfo.CurrentCulture) as Uri;
            iconSvg.Source = iconUri;
        }

        private static Brush GetTextBrush(string? colorKey)
        {
            var key = string.IsNullOrWhiteSpace(colorKey) ? null : $"TextOn{colorKey}Brush";
            if (!string.IsNullOrWhiteSpace(key))
            {
                try
                {
                    if (Application.Current?.TryFindResource(key) is Brush b) return b;
                }
                catch { }
            }
            return Brushes.White;
        }
    }
}
