using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    public static class ContinueNodeControl
    {
        public static Border CreateBorder(WorkflowNode node, Window ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            const double size = 60;

            var grid = new Grid
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "diagram-predecessor duotone-light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 25,
                Height = 25,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = Application.Current.TryFindResource("TextOnInfoBrush") as Brush
            };
            grid.Children.Add(iconSvg);

            // Create title TextBlock (required by BaseNodeControlHelper)
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Continue",
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

            var border = new Border
            {
                Child = grid,
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = Application.Current.TryFindResource("InfoBrush") as Brush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(size / 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node
            };

            // --- Initialize with fluent API ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync()
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }
    }
}
