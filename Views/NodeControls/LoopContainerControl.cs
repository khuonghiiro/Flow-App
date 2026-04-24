using FlowMy.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Views.NodeControls
{
    public static class LoopContainerControl
    {
        // Enum cho resize directions
        private enum ResizeDirection
        {
            None,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Left,
            Right,
            Top,
            Bottom
        }

        public static Border CreateContainer(LoopNode node)
        {
            var border = new Border
            {
                Width = node.LoopBodyNode.Width,
                Height = node.LoopBodyNode.Height,
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 200, 100)),
                BorderBrush = new SolidColorBrush(Colors.Orange),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Tag = node
            };

            var grid = new Grid();

            // Border nét đứt
            var dashedBorder = new Rectangle
            {
                Stroke = new SolidColorBrush(Colors.Orange),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                RadiusX = 8,
                RadiusY = 8
            };
            grid.Children.Add(dashedBorder);

            // Header
            var headerText = new TextBlock
            {
                Text = "🔁 Loop Body",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.Orange),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(100, 255, 200, 100)),
                Padding = new Thickness(8, 2, 8, 2)
            };
            grid.Children.Add(headerText);

            // Resize handles (8 handles - 4 góc + 4 cạnh)
            AddResizeHandle(grid, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(2, 2, 0, 0));
            AddResizeHandle(grid, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(grid, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(grid, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));

            AddResizeHandle(grid, ResizeDirection.Left, HorizontalAlignment.Left, VerticalAlignment.Center, new Thickness(2, 0, 0, 0));
            AddResizeHandle(grid, ResizeDirection.Right, HorizontalAlignment.Right, VerticalAlignment.Center, new Thickness(0, 0, 2, 0));
            AddResizeHandle(grid, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 2, 0, 0));
            AddResizeHandle(grid, ResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(0, 0, 0, 2));

            border.Child = grid;

            // Attach resize logic
            AttachResizeLogic(border, node);
            UpdateBodyResizeHandleScale(border, node.LoopBodyNode.Width, node.LoopBodyNode.Height);

            return border;
        }

        private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Colors.Orange),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = direction,
                Cursor = GetCursorForDirection(direction)
            };

            grid.Children.Add(handle);
        }

        private static void UpdateBodyResizeHandleScale(Border border, double bodyWidth, double bodyHeight)
        {
            if (border.Child is not Grid grid) return;

            var widthScale = bodyWidth / 800.0;
            var heightScale = bodyHeight / 400.0;
            var rawScale = Math.Max(1.0, Math.Max(widthScale, heightScale));
            var visualScale = Math.Max(1.0, Math.Min(2.8, rawScale * 1.2));

            foreach (var child in grid.Children)
            {
                if (child is Ellipse handle && handle.Tag is ResizeDirection)
                {
                    handle.RenderTransformOrigin = new Point(0.5, 0.5);
                    handle.RenderTransform = new ScaleTransform(visualScale, visualScale);
                }
            }
        }

        private static Cursor GetCursorForDirection(ResizeDirection direction)
        {
            return direction switch
            {
                ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
                ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
                _ => Cursors.Arrow
            };
        }

        public static void UpdateContainerSize(Border container, double width, double height)
        {
            container.Width = Math.Max(200, width);
            container.Height = Math.Max(200, height);
        }

        private static void AttachResizeLogic(Border border, LoopNode node)
        {
            bool isResizing = false;
            ResizeDirection currentDirection = ResizeDirection.None;
            Point resizeStartPoint = new Point();
            
            // Loop Body Node properties snapshot
            double originalWidth = 0;
            double originalHeight = 0;
            double originalX = 0;
            double originalY = 0;

            border.PreviewMouseDown += (s, e) =>
            {
                if (e.OriginalSource is Ellipse handle && handle.Tag is ResizeDirection direction)
                {
                    isResizing = true;
                    currentDirection = direction;
                    resizeStartPoint = e.GetPosition(border.Parent as UIElement);
                    
                    originalWidth = node.LoopBodyNode.Width;
                    originalHeight = node.LoopBodyNode.Height;
                    originalX = node.LoopBodyNode.X;
                    originalY = node.LoopBodyNode.Y;
                    
                    border.CaptureMouse();
                    e.Handled = true;
                }
            };

            border.PreviewMouseMove += (s, e) =>
            {
                if (isResizing)
                {
                    var currentPoint = e.GetPosition(border.Parent as UIElement);
                    var deltaX = currentPoint.X - resizeStartPoint.X;
                    var deltaY = currentPoint.Y - resizeStartPoint.Y;

                    double newX = originalX;
                    double newY = originalY;
                    double newWidth = originalWidth;
                    double newHeight = originalHeight;

                    switch (currentDirection)
                    {
                        case ResizeDirection.BottomRight:
                            newWidth = Math.Max(200, originalWidth + deltaX);
                            newHeight = Math.Max(200, originalHeight + deltaY);
                            break;

                        case ResizeDirection.TopLeft:
                            newWidth = Math.Max(200, originalWidth - deltaX);
                            newHeight = Math.Max(200, originalHeight - deltaY);
                            // Adjust X/Y to keep BottomRight fixed
                            newX = originalX + (originalWidth - newWidth);
                            newY = originalY + (originalHeight - newHeight);
                            break;

                        case ResizeDirection.TopRight:
                            newWidth = Math.Max(200, originalWidth + deltaX);
                            newHeight = Math.Max(200, originalHeight - deltaY);
                            // Adjust Y to keep BottomLeft fixed
                            newY = originalY + (originalHeight - newHeight);
                            break;

                        case ResizeDirection.BottomLeft:
                            newWidth = Math.Max(200, originalWidth - deltaX);
                            newHeight = Math.Max(200, originalHeight + deltaY);
                            // Adjust X to keep TopRight fixed
                            newX = originalX + (originalWidth - newWidth);
                            break;

                        case ResizeDirection.Right:
                            newWidth = Math.Max(200, originalWidth + deltaX);
                            break;

                        case ResizeDirection.Left:
                            newWidth = Math.Max(200, originalWidth - deltaX);
                            newX = originalX + (originalWidth - newWidth);
                            break;

                        case ResizeDirection.Bottom:
                            newHeight = Math.Max(200, originalHeight + deltaY);
                            break;

                        case ResizeDirection.Top:
                            newHeight = Math.Max(200, originalHeight - deltaY);
                            newY = originalY + (originalHeight - newHeight);
                            break;
                    }

                    // Apply changes to LoopBodyNode
                    node.LoopBodyNode.Width = newWidth;
                    node.LoopBodyNode.Height = newHeight;
                    node.LoopBodyNode.X = newX;
                    node.LoopBodyNode.Y = newY;

                    UpdateContainerSize(border, newWidth, newHeight);
                    UpdateBodyResizeHandleScale(border, newWidth, newHeight);

                    // Update Position on Canvas
                    Canvas.SetLeft(border, newX);
                    Canvas.SetTop(border, newY);

                    e.Handled = true;
                }
            };

            border.PreviewMouseUp += (s, e) =>
            {
                if (isResizing)
                {
                    isResizing = false;
                    currentDirection = ResizeDirection.None;
                    border.ReleaseMouseCapture();
                    e.Handled = true;
                }
            };
        }

        /// <summary>Khung body cho AsyncTask (chế độ giống Loop) — màu xanh mint.</summary>
        public static Border CreateAsyncTaskContainer(AsyncTaskNode node)
        {
            if (node.AsyncTaskBodyNode == null)
                throw new InvalidOperationException("AsyncTaskBodyNode is required.");

            var body = node.AsyncTaskBodyNode;
            var border = new Border
            {
                Width = body.Width,
                Height = body.Height,
                // Important: avoid Background fill stealing hit-test over delete buttons/lines inside the body.
                // Dashed rectangle + resize handles still provide the visuals/interaction.
                // Use Transparent (not null) so containerBorder still receives mouse events for dragging.
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Tag = node
            };

            var grid = new Grid();
            var dashedBorder = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                RadiusX = 8,
                RadiusY = 8
            };
            grid.Children.Add(dashedBorder);

            grid.Children.Add(new TextBlock
            {
                Text = "⚡ Async Task Body",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 0, 0),
                Background = null,
                Padding = new Thickness(8, 2, 8, 2)
            });

            AddResizeHandle(grid, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(2, 2, 0, 0));
            AddResizeHandle(grid, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(grid, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(grid, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            // Chỉ giữ resize ở góc + cạnh dưới để tránh chặn các thao tác kéo/kết nối port hai bên + top.
            AddResizeHandle(grid, ResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(0, 0, 0, 2));

            border.Child = grid;
            AttachResizeLogicAsyncTask(border, node);
            UpdateBodyResizeHandleScale(border, body.Width, body.Height);
            return border;
        }

        private static void AttachResizeLogicAsyncTask(Border border, AsyncTaskNode node)
        {
            var body = node.AsyncTaskBodyNode ?? throw new InvalidOperationException();
            bool isResizing = false;
            ResizeDirection currentDirection = ResizeDirection.None;
            Point resizeStartPoint = new();
            double originalWidth = 0, originalHeight = 0, originalX = 0, originalY = 0;

            border.PreviewMouseDown += (s, e) =>
            {
                if (e.OriginalSource is Ellipse handle && handle.Tag is ResizeDirection direction)
                {
                    isResizing = true;
                    currentDirection = direction;
                    resizeStartPoint = e.GetPosition(border.Parent as UIElement);
                    originalWidth = body.Width;
                    originalHeight = body.Height;
                    originalX = body.X;
                    originalY = body.Y;
                    border.CaptureMouse();
                    e.Handled = true;
                }
            };

            border.PreviewMouseMove += (s, e) =>
            {
                if (!isResizing) return;
                var currentPoint = e.GetPosition(border.Parent as UIElement);
                var deltaX = currentPoint.X - resizeStartPoint.X;
                var deltaY = currentPoint.Y - resizeStartPoint.Y;
                double newX = originalX, newY = originalY, newWidth = originalWidth, newHeight = originalHeight;

                switch (currentDirection)
                {
                    case ResizeDirection.BottomRight:
                        newWidth = Math.Max(200, originalWidth + deltaX);
                        newHeight = Math.Max(200, originalHeight + deltaY);
                        break;
                    case ResizeDirection.TopLeft:
                        newWidth = Math.Max(200, originalWidth - deltaX);
                        newHeight = Math.Max(200, originalHeight - deltaY);
                        newX = originalX + (originalWidth - newWidth);
                        newY = originalY + (originalHeight - newHeight);
                        break;
                    case ResizeDirection.TopRight:
                        newWidth = Math.Max(200, originalWidth + deltaX);
                        newHeight = Math.Max(200, originalHeight - deltaY);
                        newY = originalY + (originalHeight - newHeight);
                        break;
                    case ResizeDirection.BottomLeft:
                        newWidth = Math.Max(200, originalWidth - deltaX);
                        newHeight = Math.Max(200, originalHeight + deltaY);
                        newX = originalX + (originalWidth - newWidth);
                        break;
                    case ResizeDirection.Right:
                        newWidth = Math.Max(200, originalWidth + deltaX);
                        break;
                    case ResizeDirection.Left:
                        newWidth = Math.Max(200, originalWidth - deltaX);
                        newX = originalX + (originalWidth - newWidth);
                        break;
                    case ResizeDirection.Bottom:
                        newHeight = Math.Max(200, originalHeight + deltaY);
                        break;
                    case ResizeDirection.Top:
                        newHeight = Math.Max(200, originalHeight - deltaY);
                        newY = originalY + (originalHeight - newHeight);
                        break;
                }

                body.Width = newWidth;
                body.Height = newHeight;
                body.X = newX;
                body.Y = newY;
                UpdateContainerSize(border, newWidth, newHeight);
                UpdateBodyResizeHandleScale(border, newWidth, newHeight);
                Canvas.SetLeft(border, newX);
                Canvas.SetTop(border, newY);
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
        }
    }
}