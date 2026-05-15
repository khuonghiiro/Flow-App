using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    public static class DelayNodeControl
    {
        public static Border CreateBorder(DelayNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // --- Create UI elements (node-specific) ---

            var grid = new Grid { MinWidth = 80, MinHeight = 80 };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            // Header: icon + 2 lines of text centered (number + unit label)
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 6, 10, 4)
            };

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "timer regular", CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetTextBrush(node.ColorKey)
            };

            var summaryNumberText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = GetTextBrush(node.ColorKey),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var summaryLabelText = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTextBrush(node.ColorKey),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            headerPanel.Children.Add(iconSvg);
            headerPanel.Children.Add(summaryNumberText);
            headerPanel.Children.Add(summaryLabelText);
            Grid.SetRow(headerPanel, 0);
            grid.Children.Add(headerPanel);

            // Helper functions for summary display
            static string FormatNumber(double value)
            {
                var rounded = Math.Round(value);
                if (Math.Abs(value - rounded) < 0.0000001d)
                    return rounded.ToString(CultureInfo.CurrentCulture);
                return value.ToString("0.###", CultureInfo.CurrentCulture);
            }

            static string GetUnitLabel(DelayTimeUnit unit) => unit switch
            {
                DelayTimeUnit.Milliseconds => "ms",
                DelayTimeUnit.Seconds => "Giây",
                DelayTimeUnit.Minutes => "Phút",
                DelayTimeUnit.Hours => "Giờ",
                _ => "Giây"
            };

            void SyncSummary()
            {
                switch (node.TimingMode)
                {
                    case DelayTimingMode.None:
                        summaryNumberText.Text = FormatNumber(node.DelayValue);
                        summaryLabelText.Text = GetUnitLabel(node.DelayUnit);
                        break;
                    case DelayTimingMode.Random:
                        summaryNumberText.Text = $"{FormatNumber(node.RandomMinValue)}–{FormatNumber(node.RandomMaxValue)}";
                        summaryLabelText.Text = $"{GetUnitLabel(node.DelayUnit)} · ngẫu nhiên";
                        break;
                    case DelayTimingMode.NodeKey:
                        summaryNumberText.Text = "Key";
                        summaryLabelText.Text = string.IsNullOrWhiteSpace(node.DelaySourceOutputKey)
                            ? $"({GetUnitLabel(node.DelayUnit)})"
                            : $"{node.DelaySourceOutputKey} · {GetUnitLabel(node.DelayUnit)}";
                        break;
                    default:
                        summaryNumberText.Text = FormatNumber(node.DelayValue);
                        summaryLabelText.Text = GetUnitLabel(node.DelayUnit);
                        break;
                }
            }

            SyncSummary();

            // Create title TextBlock
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Delay",
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
                CornerRadius = new CornerRadius(8),
                Cursor = System.Windows.Input.Cursors.Hand,
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
            // Handle delay-specific property changes (summary display) and ColorKey (icon/text fill)
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.ColorKey)] = ctx =>
                {
                    iconSvg.Fill = GetTextBrush(node.ColorKey);
                    summaryNumberText.Foreground = GetTextBrush(node.ColorKey);
                    summaryLabelText.Foreground = GetTextBrush(node.ColorKey);
                },
                [nameof(DelayNode.DelayValue)] = _ => SyncSummary(),
                [nameof(DelayNode.DelayUnit)] = _ => SyncSummary(),
                [nameof(DelayNode.DelayMilliseconds)] = _ => SyncSummary(),
                [nameof(DelayNode.TimingMode)] = _ => SyncSummary(),
                [nameof(DelayNode.RandomMinValue)] = _ => SyncSummary(),
                [nameof(DelayNode.RandomMaxValue)] = _ => SyncSummary(),
                [nameof(DelayNode.DelaySourceNodeId)] = _ => SyncSummary(),
                [nameof(DelayNode.DelaySourceOutputKey)] = _ => SyncSummary(),
            };

            // --- Initialize with fluent API ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new DelayNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        /// <summary>
        /// Resolves the text/icon fill brush for the given color key using the theme resource format
        /// "TextOn{colorKey}Brush" (e.g., "TextOnSuccessBrush").
        /// </summary>
        private static Brush GetTextBrush(string? colorKey)
        {
            if (!string.IsNullOrWhiteSpace(colorKey))
            {
                var resourceKey = $"TextOn{colorKey}Brush";
                if (Application.Current?.Resources.Contains(resourceKey) == true)
                    return (Brush)Application.Current.Resources[resourceKey];
            }
            return new SolidColorBrush(Colors.White);
        }
    }
}
