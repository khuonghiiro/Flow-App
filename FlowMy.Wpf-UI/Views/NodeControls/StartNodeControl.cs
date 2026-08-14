using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Control cho Start node với hình tròn và icon play bên trong
    /// </summary>
    public static class StartNodeControl
    {
        public const double NodeSize = 100;

        public static Border CreateBorder(WorkflowNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // --- Create UI elements (node-specific) ---

            var grid = new Grid
            {
                Width = NodeSize,
                Height = NodeSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var border = new Border
            {
                Child = grid,
                Width = NodeSize,
                Height = NodeSize,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = node.NodeBrush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(NodeSize / 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Effect = GpuOptimizationHelper.CreateDropShadowEffect(),
                Tag = node
            };

            // Populate node-specific visuals (shape + icon + badges)
            PopulateVisuals(node, grid, border);

            // Create title TextBlock
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Start",
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

            // --- Initialize with fluent API ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync()
                .WithDialogSupport(ctx => new StartEndNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        public static void RefreshVisual(WorkflowNode node)
        {
            if (node.Border == null) return;
            if (node.Border.Child is not Grid grid) return;
            PopulateVisuals(node, grid, node.Border);
        }

        /// <summary>
        /// Compatibility shim: returns the node's existing TitleTextBlockUI or creates a new one.
        /// Called by legacy rendering code (_NodeRenderer, ZoomPanHandler, etc.).
        /// With BaseNodeControlHelper, the title TextBlock is created in CreateBorder and managed automatically.
        /// </summary>
        public static TextBlock CreateTitleTextBlock(WorkflowNode node)
        {
            if (node.TitleTextBlockUI != null)
                return node.TitleTextBlockUI;

            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Start",
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
                Visibility = System.Windows.Visibility.Collapsed
            };
            node.TitleTextBlockUI = titleTextBlock;
            return titleTextBlock;
        }

        /// <summary>
        /// Compatibility shim: updates the title position using BaseNodeControlHelper.
        /// Called by legacy rendering code (_NodeRenderer, ZoomPanHandler, etc.).
        /// </summary>
        public static void UpdateTitlePosition(WorkflowNode node, System.Windows.Controls.Canvas canvas)
        {
            if (node.TitleTextBlockUI == null || node.Border == null) return;
            var border = node.Border;
            var titleTextBlock = node.TitleTextBlockUI;

            var borderLeft = System.Windows.Controls.Canvas.GetLeft(border);
            var borderTop = System.Windows.Controls.Canvas.GetTop(border);
            if (double.IsNaN(borderLeft)) borderLeft = node.X;
            if (double.IsNaN(borderTop)) borderTop = node.Y;

            var borderWidth = border.ActualWidth > 0 ? border.ActualWidth : border.Width;
            var borderHeight = border.ActualHeight > 0 ? border.ActualHeight : border.Height;

            if (titleTextBlock.ActualWidth == 0)
            {
                titleTextBlock.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                titleTextBlock.Arrange(new System.Windows.Rect(titleTextBlock.DesiredSize));
            }

            var titleWidth = titleTextBlock.ActualWidth > 0 ? titleTextBlock.ActualWidth : titleTextBlock.DesiredSize.Width;
            var titleHeight = titleTextBlock.ActualHeight > 0 ? titleTextBlock.ActualHeight : titleTextBlock.DesiredSize.Height;

            var titleLeft = borderLeft + (borderWidth / 2) - (titleWidth / 2);
            var titleTop = borderTop - titleHeight - 4;

            System.Windows.Controls.Canvas.SetLeft(titleTextBlock, titleLeft);
            System.Windows.Controls.Canvas.SetTop(titleTextBlock, titleTop);
        }

        private static void PopulateVisuals(WorkflowNode node, Grid grid, Border border)
        {
            grid.Children.Clear();
            border.Background = node.NodeBrush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219));
            border.BorderBrush = new SolidColorBrush(Colors.White);
            ApplyShape(node, node.RunMode, border, grid);

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "play duotone-regular",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 55,
                Height = 55,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new SolidColorBrush(Colors.White),
                Margin = new Thickness(4, 0, 0, 0)
            };
            grid.Children.Add(iconSvg);

            var modeText = node.RunMode switch
            {
                FlowRunMode.MainFlow => "MAIN",
                FlowRunMode.SubFlowAttached => "ATT",
                FlowRunMode.SubFlowIndependent => "IND",
                FlowRunMode.AutoScheduled => "AUTO",
                _ => "MAIN"
            };

            var modeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = modeText,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                }
            };
            grid.Children.Add(modeBadge);

            if (!string.IsNullOrWhiteSpace(node.FlowScopeKey))
            {
                var scopeBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(190, 22, 22, 22)),
                    CornerRadius = new CornerRadius(6),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 6, 0, 0),
                    Padding = new Thickness(5, 1, 5, 1),
                    Child = new TextBlock
                    {
                        Text = node.FlowScopeKey,
                        Foreground = Brushes.White,
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold
                    }
                };
                grid.Children.Add(scopeBadge);
            }
        }

        private static void ApplyShape(WorkflowNode node, FlowRunMode mode, Border border, Grid grid)
        {
            border.RenderTransformOrigin = new Point(0.5, 0.5);
            grid.RenderTransformOrigin = new Point(0.5, 0.5);

            switch (mode)
            {
                case FlowRunMode.MainFlow:
                    border.CornerRadius = new CornerRadius(NodeSize / 2);
                    border.RenderTransform = null;
                    grid.RenderTransform = null;
                    break;
                case FlowRunMode.SubFlowAttached:
                    border.CornerRadius = new CornerRadius(14);
                    border.RenderTransform = null;
                    grid.RenderTransform = null;
                    break;
                case FlowRunMode.SubFlowIndependent:
                case FlowRunMode.AutoScheduled:
                    border.CornerRadius = new CornerRadius(0);
                    {
                        var sy = GetSharpnessScaleY(node.DiamondSharpness);
                        var borderTf = new TransformGroup();
                        borderTf.Children.Add(new ScaleTransform(1.0, sy));
                        borderTf.Children.Add(new RotateTransform(45));
                        border.RenderTransform = borderTf;

                        var gridTf = new TransformGroup();
                        gridTf.Children.Add(new RotateTransform(-45));
                        gridTf.Children.Add(new ScaleTransform(1.0, 1.0 / sy));
                        grid.RenderTransform = gridTf;
                    }
                    break;
                default:
                    border.CornerRadius = new CornerRadius(NodeSize / 2);
                    border.RenderTransform = null;
                    grid.RenderTransform = null;
                    break;
            }
        }

        private static double GetSharpnessScaleY(DiamondSharpness sharpness) => sharpness switch
        {
            DiamondSharpness.Soft => 0.88,
            DiamondSharpness.Medium => 1.0,
            DiamondSharpness.Sharp => 1.15,
            _ => 1.0
        };
    }
}
