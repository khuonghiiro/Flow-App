// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Views.NodeControls
{
    public static class VideoEditorNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }

        public const double VideoNodeMinWidthPx = 480;
        public const double VideoNodeMinHeightPx = 360;
        private const double VideoNodeDefaultWidthPx = 640;
        private const double VideoNodeDefaultHeightPx = 480;

        public static Border CreateBorder(VideoEditorNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            double initW = double.IsNaN(node.Width) || node.Width < VideoNodeMinWidthPx ? VideoNodeDefaultWidthPx : Math.Max(node.Width, VideoNodeMinWidthPx);
            double initH = double.IsNaN(node.Height) || node.Height < VideoNodeMinHeightPx ? VideoNodeDefaultHeightPx : Math.Max(node.Height, VideoNodeMinHeightPx);
            node.Width = initW;
            node.Height = initH;

            var border = new Border
            {
                Width = initW,
                Height = initH,
                MinWidth = VideoNodeMinWidthPx,
                MinHeight = VideoNodeMinHeightPx,
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Cursor = Cursors.Hand,
                Effect = null,
                Tag = node,
                CacheMode = null
            };

            var shadowPlate = new Border
            {
                Background = node.NodeBrush ?? new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27)),
                CornerRadius = new CornerRadius(10),
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
            var videoContent = new VideoEditorNodeContentControl(node, host, border, ownerWindow, () => isResizing);

            AddResizeHandle(handleOverlay, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 2, 0, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.Right, HorizontalAlignment.Right, VerticalAlignment.Center, new Thickness(0, 0, 2, 0));

            var outerGrid = new Grid();
            outerGrid.Children.Add(videoContent);
            outerGrid.Children.Add(handleOverlay);
            Panel.SetZIndex(outerGrid, 1);
            GpuOptimizationHelper.ApplyToElement(outerGrid);

            var chromeFillGrid = new Grid();
            chromeFillGrid.Children.Add(shadowPlate);
            chromeFillGrid.Children.Add(outerGrid);
            border.Child = chromeFillGrid;

            // --- Resize Drag Handle Logic ---
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
                    origW = border.ActualWidth;
                    origH = border.ActualHeight;
                    origX = node.X;
                    origY = node.Y;
                    border.CaptureMouse();
                    e.Handled = true;
                }
            };

            border.PreviewMouseMove += (s, e) =>
            {
                if (!isResizing) return;
                var pos = e.GetPosition(border.Parent as UIElement);
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
                    case ResizeDirection.Right:
                        newW = Math.Max(minW, origW + dx);
                        break;
                    case ResizeDirection.Top:
                        newH = Math.Max(minH, origH - dy);
                        newY = origY + (origH - newH);
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

            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Chỉnh sửa video",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode, node.TitleColorKey, node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always
                    ? Visibility.Visible : Visibility.Collapsed
            };
            node.TitleTextBlockUI = titleTextBlock;

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
                [nameof(VideoEditorNode.Width)] = ctx =>
                {
                    if (!isResizing) border.Width = node.Width;
                },
                [nameof(VideoEditorNode.Height)] = ctx =>
                {
                    if (!isResizing) border.Height = node.Height;
                }
            };

            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new VideoEditorNodeDialog(
                    node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        private static void AddResizeHandle(Grid parent, ResizeDirection dir, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 11,
                Height = 11,
                Fill = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                StrokeThickness = 1.5,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = dir,
                Cursor = GetCursorForDirection(dir),
                Opacity = 0.8
            };
            handle.MouseEnter += (s, e) => handle.Opacity = 1.0;
            handle.MouseLeave += (s, e) => handle.Opacity = 0.8;
            parent.Children.Add(handle);
        }

        private static Cursor GetCursorForDirection(ResizeDirection dir)
        {
            return dir switch
            {
                ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
                ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
                _ => Cursors.Arrow,
            };
        }
    }
}
