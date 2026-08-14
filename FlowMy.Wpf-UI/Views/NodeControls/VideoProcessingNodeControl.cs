// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Views.NodeControls
{
    public static class VideoProcessingNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }

        /// <summary>Min kích thước node video.</summary>
        public const double VideoNodeMinWidthPx = 540;
        /// <summary>Min chiều cao node video.</summary>
        public const double VideoNodeMinHeightPx = 340;
        private const double VideoNodeDefaultWidthPx = 1366;
        private const double VideoNodeDefaultHeightPx = 768;

        public static Border CreateBorder(VideoProcessingNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            void RefreshPortsAndConnections()
            {
                if (node.Ports != null)
                {
                    foreach (var position in node.Ports.Select(p => p.Position).Distinct())
                    {
                        host.UpdatePortsPositionOnSide(node, position);
                    }
                }

                var connections = host.ViewModel?.Connections;
                if (connections != null && connections.Count > 0)
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(connections);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(connections);
                }
            }

            double initW = double.IsNaN(node.Width) ? VideoNodeDefaultWidthPx : Math.Max(node.Width, VideoNodeMinWidthPx);
            double initH = double.IsNaN(node.Height) ? VideoNodeDefaultHeightPx : Math.Max(node.Height, VideoNodeMinHeightPx);

            node.Width = initW;
            node.Height = initH;

            var border = new Border
            {
                Width = initW,
                Height = initH,
                MinWidth = VideoNodeMinWidthPx,
                MinHeight = VideoNodeMinHeightPx,
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = null,
                Tag = node,
                CacheMode = null
            };

            var shadowPlate = new Border
            {
                Background = node.NodeBrush,
                CornerRadius = new CornerRadius(8),
                Effect = GpuOptimizationHelper.CreateDropShadowEffect(),
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                ClipToBounds = false
            };
            GpuOptimizationHelper.ApplyToElement(shadowPlate);

            border.Loaded += (s, e) =>
            {
                border.InvalidateMeasure();
                border.InvalidateArrange();
                border.UpdateLayout();
            };

            bool isResizing = false;

            var handleOverlay = new Grid();
            var contentControl = new VideoProcessingNodeContentControl(node, host);

            AddResizeHandle(handleOverlay, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 2, 0, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(0, 0, 0, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.Left, HorizontalAlignment.Left, VerticalAlignment.Center, new Thickness(2, 0, 0, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.Right, HorizontalAlignment.Right, VerticalAlignment.Center, new Thickness(0, 0, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(2, 2, 0, 0));

            var outerGrid = new Grid();
            outerGrid.Children.Add(contentControl);
            outerGrid.Children.Add(handleOverlay);
            Panel.SetZIndex(outerGrid, 1);
            GpuOptimizationHelper.ApplyToElement(outerGrid);

            var chromeFillGrid = new Grid();
            chromeFillGrid.Children.Add(shadowPlate);
            chromeFillGrid.Children.Add(outerGrid);
            border.Child = chromeFillGrid;

            // --- Resize handle logic ---
            ResizeDirection currentDir = ResizeDirection.None;
            Point resizeStart = default;
            double origW = 0, origH = 0, origX = 0, origY = 0;

            border.PreviewMouseDown += (s, e) =>
            {
                if (e.OriginalSource is Ellipse handle && handle.Tag is ResizeDirection dir)
                {
                    isResizing = true;
                    currentDir = dir;
                    resizeStart = e.GetPosition(border.Parent as UIElement);
                    origW = border.ActualWidth > 0 ? border.ActualWidth : Math.Max(border.MinWidth, border.Width);
                    origH = border.ActualHeight > 0 ? border.ActualHeight : Math.Max(border.MinHeight, border.Height);
                    origX = Canvas.GetLeft(border);
                    origY = Canvas.GetTop(border);
                    if (double.IsNaN(origX)) origX = node.X;
                    if (double.IsNaN(origY)) origY = node.Y;

                    border.CaptureMouse();
                    e.Handled = true;
                }
            };

            border.PreviewMouseMove += (s, e) =>
            {
                if (!isResizing) return;
                var parent = border.Parent as UIElement;
                if (parent == null) return;
                var pos = e.GetPosition(parent);
                var dx = pos.X - resizeStart.X;
                var dy = pos.Y - resizeStart.Y;

                double newX = origX, newY = origY, newW = origW, newH = origH;
                var minW = border.MinWidth > 0 ? border.MinWidth : VideoNodeMinWidthPx;
                var minH = border.MinHeight > 0 ? border.MinHeight : VideoNodeMinHeightPx;

                switch (currentDir)
                {
                    case ResizeDirection.BottomRight:
                        newW = Math.Max(minW, origW + dx);
                        newH = Math.Max(minH, origH + dy);
                        break;
                    case ResizeDirection.TopRight:
                        newW = Math.Max(minW, origW + dx);
                        newH = Math.Max(minH, origH - dy);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.BottomLeft:
                        newW = Math.Max(minW, origW - dx);
                        newH = Math.Max(minH, origH + dy);
                        newX = origX + (origW - newW);
                        break;
                    case ResizeDirection.TopLeft:
                        newW = Math.Max(minW, origW - dx);
                        newH = Math.Max(minH, origH - dy);
                        newX = origX + (origW - newW);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.Top:
                        newH = Math.Max(minH, origH - dy);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.Bottom:
                        newH = Math.Max(minH, origH + dy);
                        break;
                    case ResizeDirection.Left:
                        newW = Math.Max(minW, origW - dx);
                        newX = origX + (origW - newW);
                        break;
                    case ResizeDirection.Right:
                        newW = Math.Max(minW, origW + dx);
                        break;
                }

                node.Width = newW;
                node.Height = newH;
                node.X = newX;
                node.Y = newY;
                border.Width = newW;
                border.Height = newH;
                if (host.WorkflowCanvas != null)
                {
                    Canvas.SetLeft(border, newX);
                    Canvas.SetTop(border, newY);
                }
                RefreshPortsAndConnections();
                e.Handled = true;
            };

            border.PreviewMouseUp += (s, e) =>
            {
                if (isResizing)
                {
                    isResizing = false;
                    border.ReleaseMouseCapture();
                    e.Handled = true;
                }
            };

            // --- Create title TextBlock (node-specific initial text and color) ---
            var titleTextBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(node.Title) ? "Video Processing" : node.Title,
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

            contentControl.SuggestedNodeSizeReady += (suggestedWidth, suggestedHeight) =>
            {
                if (isResizing) return;
                var nextWidth = Math.Max(node.Width, suggestedWidth);
                var nextHeight = Math.Max(node.Height, suggestedHeight);
                if (nextWidth <= node.Width + 0.01 && nextHeight <= node.Height + 0.01) return;

                node.Width = nextWidth;
                node.Height = nextHeight;

                border.Width = node.Width;
                border.Height = node.Height;
                RefreshPortsAndConnections();
            };

            border.SizeChanged += (_, _) =>
            {
                RefreshPortsAndConnections();
            };

            // --- Node-specific custom property handlers ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    border.Background = Brushes.Transparent;
                    shadowPlate.Background = node.NodeBrush;
                    ctx.TitleTextBlock.Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                        BaseNodeControlHelper.GetTitleColorMode(node),
                        BaseNodeControlHelper.GetTitleColorKey(node),
                        node.NodeBrush);
                },
                [nameof(VideoProcessingNode.Width)] = ctx =>
                {
                    if (!isResizing)
                    {
                        border.Width = node.Width;
                        RefreshPortsAndConnections();
                    }
                },
                [nameof(VideoProcessingNode.Height)] = ctx =>
                {
                    if (!isResizing)
                    {
                        border.Height = node.Height;
                        RefreshPortsAndConnections();
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
                .WithDialogSupport(ctx => new VideoProcessingNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 32,
                Height = 32,
                Fill = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = direction,
                Cursor = direction switch
                {
                    ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                    ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                    ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
                    ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
                    _ => Cursors.Arrow
                },
                CacheMode = null
            };
            GpuOptimizationHelper.ApplyToShape(handle);
            grid.Children.Add(handle);
        }
    }
}

