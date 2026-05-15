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
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Custom UI builder cho HotkeyPressEventNode (sự kiện nhấn tổ hợp phím).
    /// </summary>
    public static class HotkeyPressEventNodeControl
    {
        public static Border CreateBorder(HotkeyPressEventNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
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
            var iconUri = iconConverter.Convert(null, typeof(Uri), "keyboard duotone",
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
                Text = node.Title ?? "Hotkey Press",
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
                .WithDialogSupport(ctx => new HotkeyPressEventNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        private static Brush GetIconBrush(string? colorKey)
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
