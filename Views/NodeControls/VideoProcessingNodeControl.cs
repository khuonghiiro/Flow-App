using FlowMy.Models;
using FlowMy.Models.Nodes;
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
using System.Windows.Threading;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public static class VideoProcessingNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Bottom }
        /// <summary>Phải trùng <see cref="Border.CornerRadius"/> bên dưới — clip nền/control con (vuông mặc định).</summary>
        private const double NodeChromeCornerRadius = 10;

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

            var border = new Border
            {
                Width = node.Width,
                Height = node.Height,
                MinWidth = 540,
                MinHeight = 340,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(NodeChromeCornerRadius),
                Cursor = Cursors.Hand,
                Effect = null,
                CacheMode = null,
                Tag = node,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            var contentControl = new VideoProcessingNodeContentControl(node, host);
            var overlayGrid = new Grid();
            overlayGrid.Children.Add(contentControl);

            var handlesLayer = new Grid { IsHitTestVisible = true };
            AddResizeHandle(handlesLayer, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(2, 2, 0, 0));
            AddResizeHandle(handlesLayer, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handlesLayer, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handlesLayer, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            AddResizeHandle(handlesLayer, ResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(0, 0, 0, 2));
            overlayGrid.Children.Add(handlesLayer);
            GpuOptimizationHelper.ApplyToElement(overlayGrid);
            border.Child = overlayGrid;
            AttachResizeLogic(border, node, RefreshPortsAndConnections);
            SyncNodeRoundedClip(border);

            if (node.Width < border.MinWidth) node.Width = border.MinWidth;
            if (node.Height < border.MinHeight) node.Height = border.MinHeight;
            border.Width = node.Width;
            border.Height = node.Height;

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
                // Auto-fit only grows to avoid surprising shrink after manual resize.
                var nextWidth = Math.Max(node.Width, suggestedWidth);
                var nextHeight = Math.Max(node.Height, suggestedHeight);
                if (nextWidth <= node.Width + 0.01 && nextHeight <= node.Height + 0.01) return;

                node.Width = nextWidth;
                node.Height = nextHeight;

                border.Width = node.Width;
                border.Height = node.Height;
                RefreshPortsAndConnections();
                // Title position will be updated automatically via LayoutUpdated in BaseNodeControlHelper
            };

            border.SizeChanged += (_, _) =>
            {
                SyncNodeRoundedClip(border);
                RefreshPortsAndConnections();
            };

            contentControl.Loaded += (_, _) =>
            {
                contentControl.Measure(new Size(border.Width, double.PositiveInfinity));
                var desired = contentControl.DesiredSize;
                var minRequiredWidth = Math.Max(border.MinWidth, desired.Width + 8);
                var minRequiredHeight = Math.Max(border.MinHeight, Math.Min(920, desired.Height + 10));

                border.MinWidth = minRequiredWidth;
                border.MinHeight = minRequiredHeight;

                if (border.Width < minRequiredWidth) border.Width = minRequiredWidth;
                if (border.Height < minRequiredHeight) border.Height = minRequiredHeight;

                node.Width = border.Width;
                node.Height = border.Height;
            };

            // --- Node-specific custom property handlers ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                // NodeBrush: update border background and title foreground
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    border.Background = node.NodeBrush;
                    ctx.TitleTextBlock.Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                        BaseNodeControlHelper.GetTitleColorMode(node),
                        BaseNodeControlHelper.GetTitleColorKey(node),
                        node.NodeBrush);
                },
                // Width/Height: sync border size when changed externally
                [nameof(VideoProcessingNode.Width)] = ctx =>
                {
                    border.Width = node.Width;
                    RefreshPortsAndConnections();
                },
                [nameof(VideoProcessingNode.Height)] = ctx =>
                {
                    border.Height = node.Height;
                    RefreshPortsAndConnections();
                }
            };

            // --- Initialize with fluent API (replaces ~200 lines of duplicated event handler code) ---
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

        private static void SyncNodeRoundedClip(Border border)
        {
            var w = Math.Max(1d, border.ActualWidth);
            var h = Math.Max(1d, border.ActualHeight);
            var maxR = Math.Min(w, h) / 2 - 0.001;
            var r = Math.Min(NodeChromeCornerRadius, Math.Max(0, maxR));
            border.Clip = r <= 0.25
                ? new RectangleGeometry(new Rect(0, 0, w, h))
                : new RectangleGeometry(new Rect(0, 0, w, h), r, r);
        }

        private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Stroke = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                StrokeThickness = 1.2,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = direction,
                Cursor = direction switch
                {
                    ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                    ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                    ResizeDirection.Bottom => Cursors.SizeNS,
                    _ => Cursors.Arrow
                }
            };
            // Prevent node drag MouseDown from hijacking resize gestures.
            handle.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
            grid.Children.Add(handle);
        }

        private static void AttachResizeLogic(Border border, VideoProcessingNode node, Action refreshPortsAndConnections)
        {
            bool isResizing = false;
            ResizeDirection currentDirection = ResizeDirection.None;
            Point resizeStartPoint = new();
            double originalWidth = 0;
            double originalHeight = 0;
            double originalX = 0;
            double originalY = 0;

            border.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (TryGetResizeDirectionFromSource(e.OriginalSource as DependencyObject, out var direction) == false) return;

                var parent = border.Parent as UIElement;
                if (parent == null) return;
                isResizing = true;
                currentDirection = direction;
                resizeStartPoint = e.GetPosition(parent);
                // Match HtmlUi behavior: start from rendered size to avoid stale Width/Height dead-zone.
                originalWidth = border.ActualWidth > 0 ? border.ActualWidth : Math.Max(border.MinWidth, border.Width);
                originalHeight = border.ActualHeight > 0 ? border.ActualHeight : Math.Max(border.MinHeight, border.Height);
                originalX = Canvas.GetLeft(border);
                originalY = Canvas.GetTop(border);
                if (double.IsNaN(originalX)) originalX = node.X;
                if (double.IsNaN(originalY)) originalY = node.Y;

                border.CaptureMouse();
                e.Handled = true;
            };

            border.PreviewMouseMove += (_, e) =>
            {
                if (!isResizing || !border.IsMouseCaptured) return;
                var parent = border.Parent as UIElement;
                if (parent == null) return;
                var current = e.GetPosition(parent);
                var dx = current.X - resizeStartPoint.X;
                var dy = current.Y - resizeStartPoint.Y;

                var newX = originalX;
                var newY = originalY;
                var newWidth = originalWidth;
                var newHeight = originalHeight;

                switch (currentDirection)
                {
                    case ResizeDirection.BottomRight:
                        newWidth = Math.Max(border.MinWidth, originalWidth + dx);
                        newHeight = Math.Max(border.MinHeight, originalHeight + dy);
                        break;
                    case ResizeDirection.TopLeft:
                        newWidth = Math.Max(border.MinWidth, originalWidth - dx);
                        newHeight = Math.Max(border.MinHeight, originalHeight - dy);
                        newX = originalX + (originalWidth - newWidth);
                        newY = originalY + (originalHeight - newHeight);
                        break;
                    case ResizeDirection.TopRight:
                        newWidth = Math.Max(border.MinWidth, originalWidth + dx);
                        newHeight = Math.Max(border.MinHeight, originalHeight - dy);
                        newY = originalY + (originalHeight - newHeight);
                        break;
                    case ResizeDirection.BottomLeft:
                        newWidth = Math.Max(border.MinWidth, originalWidth - dx);
                        newHeight = Math.Max(border.MinHeight, originalHeight + dy);
                        newX = originalX + (originalWidth - newWidth);
                        break;
                    case ResizeDirection.Bottom:
                        newHeight = Math.Max(border.MinHeight, originalHeight + dy);
                        break;
                }

                border.Width = newWidth;
                border.Height = newHeight;
                Canvas.SetLeft(border, newX);
                Canvas.SetTop(border, newY);
                node.Width = newWidth;
                node.Height = newHeight;
                node.X = newX;
                node.Y = newY;
                e.Handled = true;
            };

            border.PreviewMouseUp += (_, e) =>
            {
                if (!isResizing) return;
                isResizing = false;
                currentDirection = ResizeDirection.None;
                border.ReleaseMouseCapture();
                e.Handled = true;
            };

            // Keep resizing stable even if child content steals mouse capture.
            border.LostMouseCapture += (_, _) =>
            {
                if (isResizing)
                    border.CaptureMouse();
            };
        }

        private static bool TryGetResizeDirectionFromSource(DependencyObject? source, out ResizeDirection direction)
        {
            while (source != null)
            {
                if (source is FrameworkElement fe && fe.Tag is ResizeDirection rd)
                {
                    direction = rd;
                    return true;
                }
                source = VisualTreeHelper.GetParent(source) ?? (source as FrameworkElement)?.Parent;
            }

            direction = ResizeDirection.None;
            return false;
        }
    }
}
