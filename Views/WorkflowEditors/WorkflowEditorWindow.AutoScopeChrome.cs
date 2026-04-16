using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowMy.Models;
using FlowMy.ViewModels;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private enum AutoScopeResizeDirection
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

        private bool _autoScopeIsResizing;
        private AutoScopeResizeDirection _autoScopeResizeDirection = AutoScopeResizeDirection.None;
        private Point _autoScopeResizeMouseStart;
        private Border? _autoScopeResizeBorder;
        private string? _autoScopeResizeStartId;
        private double _autoScopeResizeOrigX;
        private double _autoScopeResizeOrigY;
        private double _autoScopeResizeOrigW;
        private double _autoScopeResizeOrigH;

        private static Cursor AutoScopeResizeCursor(AutoScopeResizeDirection d) => d switch
        {
            AutoScopeResizeDirection.TopLeft or AutoScopeResizeDirection.BottomRight => Cursors.SizeNWSE,
            AutoScopeResizeDirection.TopRight or AutoScopeResizeDirection.BottomLeft => Cursors.SizeNESW,
            AutoScopeResizeDirection.Left or AutoScopeResizeDirection.Right => Cursors.SizeWE,
            AutoScopeResizeDirection.Top or AutoScopeResizeDirection.Bottom => Cursors.SizeNS,
            _ => Cursors.Arrow
        };

        private void WireAutoScopeBorderInteractions(Border scopeBorder, string startNodeId)
        {
            if (scopeBorder.Child is not Grid grid) return;

            scopeBorder.PreviewMouseDown += (_, e) =>
            {
                if (e.OriginalSource is Ellipse { Tag: AutoScopeResizeDirection dir } && dir != AutoScopeResizeDirection.None)
                {
                    TryBeginAutoScopeResize(scopeBorder, startNodeId, dir, e);
                    e.Handled = true;
                }
            };

            scopeBorder.PreviewMouseMove += AutoScopeBorder_PreviewMouseMoveForResize;
            scopeBorder.PreviewMouseUp += AutoScopeBorder_PreviewMouseUpForResize;
        }

        private void TryBeginAutoScopeResize(Border border, string startNodeId, AutoScopeResizeDirection direction, MouseButtonEventArgs e)
        {
            if (ViewModel == null || WorkflowCanvas == null) return;
            var start = ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, startNodeId, StringComparison.OrdinalIgnoreCase) && n.Type == NodeType.Start);
            if (start == null) return;

            const double minSide = 120d;
            if (start.AutoScopeFrameWidth < minSide || start.AutoScopeFrameHeight < minSide)
            {
                start.AutoScopeFrameX = Canvas.GetLeft(border);
                start.AutoScopeFrameY = Canvas.GetTop(border);
                start.AutoScopeFrameWidth = Math.Max(minSide, border.Width);
                start.AutoScopeFrameHeight = Math.Max(minSide, border.Height);
            }

            _autoScopeIsResizing = true;
            _autoScopeResizeDirection = direction;
            _autoScopeResizeBorder = border;
            _autoScopeResizeStartId = startNodeId;
            _autoScopeResizeMouseStart = e.GetPosition(WorkflowCanvas);
            _autoScopeResizeOrigX = start.AutoScopeFrameX;
            _autoScopeResizeOrigY = start.AutoScopeFrameY;
            _autoScopeResizeOrigW = start.AutoScopeFrameWidth;
            _autoScopeResizeOrigH = start.AutoScopeFrameHeight;
            border.CaptureMouse();
        }

        private void AutoScopeBorder_PreviewMouseMoveForResize(object sender, MouseEventArgs e)
        {
            if (!_autoScopeIsResizing || ViewModel == null || WorkflowCanvas == null || _autoScopeResizeBorder == null ||
                string.IsNullOrEmpty(_autoScopeResizeStartId))
                return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var start = ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, _autoScopeResizeStartId, StringComparison.OrdinalIgnoreCase) && n.Type == NodeType.Start);
            if (start == null) return;

            var cur = e.GetPosition(WorkflowCanvas);
            var deltaX = cur.X - _autoScopeResizeMouseStart.X;
            var deltaY = cur.Y - _autoScopeResizeMouseStart.Y;

            double newX = _autoScopeResizeOrigX;
            double newY = _autoScopeResizeOrigY;
            double newW = _autoScopeResizeOrigW;
            double newH = _autoScopeResizeOrigH;

            switch (_autoScopeResizeDirection)
            {
                case AutoScopeResizeDirection.BottomRight:
                    newW = Math.Max(120, _autoScopeResizeOrigW + deltaX);
                    newH = Math.Max(120, _autoScopeResizeOrigH + deltaY);
                    break;
                case AutoScopeResizeDirection.TopLeft:
                    newW = Math.Max(120, _autoScopeResizeOrigW - deltaX);
                    newH = Math.Max(120, _autoScopeResizeOrigH - deltaY);
                    newX = _autoScopeResizeOrigX + (_autoScopeResizeOrigW - newW);
                    newY = _autoScopeResizeOrigY + (_autoScopeResizeOrigH - newH);
                    break;
                case AutoScopeResizeDirection.TopRight:
                    newW = Math.Max(120, _autoScopeResizeOrigW + deltaX);
                    newH = Math.Max(120, _autoScopeResizeOrigH - deltaY);
                    newY = _autoScopeResizeOrigY + (_autoScopeResizeOrigH - newH);
                    break;
                case AutoScopeResizeDirection.BottomLeft:
                    newW = Math.Max(120, _autoScopeResizeOrigW - deltaX);
                    newH = Math.Max(120, _autoScopeResizeOrigH + deltaY);
                    newX = _autoScopeResizeOrigX + (_autoScopeResizeOrigW - newW);
                    break;
                case AutoScopeResizeDirection.Right:
                    newW = Math.Max(120, _autoScopeResizeOrigW + deltaX);
                    break;
                case AutoScopeResizeDirection.Left:
                    newW = Math.Max(120, _autoScopeResizeOrigW - deltaX);
                    newX = _autoScopeResizeOrigX + (_autoScopeResizeOrigW - newW);
                    break;
                case AutoScopeResizeDirection.Bottom:
                    newH = Math.Max(120, _autoScopeResizeOrigH + deltaY);
                    break;
                case AutoScopeResizeDirection.Top:
                    newH = Math.Max(120, _autoScopeResizeOrigH - deltaY);
                    newY = _autoScopeResizeOrigY + (_autoScopeResizeOrigH - newH);
                    break;
            }

            start.AutoScopeFrameX = newX;
            start.AutoScopeFrameY = newY;
            start.AutoScopeFrameWidth = newW;
            start.AutoScopeFrameHeight = newH;

            _autoScopeResizeBorder.Width = newW;
            _autoScopeResizeBorder.Height = newH;
            Canvas.SetLeft(_autoScopeResizeBorder, newX);
            Canvas.SetTop(_autoScopeResizeBorder, newY);

            RefreshAutoStartScopeBorders();
            e.Handled = true;
        }

        private void AutoScopeBorder_PreviewMouseUpForResize(object sender, MouseButtonEventArgs e)
        {
            if (!_autoScopeIsResizing) return;
            if (_autoScopeResizeBorder?.IsMouseCaptured == true)
                _autoScopeResizeBorder.ReleaseMouseCapture();
            _autoScopeIsResizing = false;
            _autoScopeResizeDirection = AutoScopeResizeDirection.None;
            _autoScopeResizeBorder = null;
            _autoScopeResizeStartId = null;
            e.Handled = true;
        }

        private static void AddAutoScopeResizeHandle(Grid grid, AutoScopeResizeDirection direction,
            HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromArgb(235, 72, 149, 239)),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = direction,
                Cursor = AutoScopeResizeCursor(direction)
            };
            Panel.SetZIndex(handle, 60);
            grid.Children.Add(handle);
        }

        private static void BuildAutoScopeBorderInnerGrid(Grid grid)
        {
            var hitSurface = new Border
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            grid.Children.Add(hitSurface);

            var dashedRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromArgb(235, 72, 149, 239)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                RadiusX = 10,
                RadiusY = 10,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Panel.SetZIndex(dashedRect, 5);
            grid.Children.Add(dashedRect);

            const double m = 2;
            AddAutoScopeResizeHandle(grid, AutoScopeResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(m, m, 0, 0));
            AddAutoScopeResizeHandle(grid, AutoScopeResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, m, m, 0));
            AddAutoScopeResizeHandle(grid, AutoScopeResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(m, 0, 0, m));
            AddAutoScopeResizeHandle(grid, AutoScopeResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, m, m));
            AddAutoScopeResizeHandle(grid, AutoScopeResizeDirection.Left, HorizontalAlignment.Left, VerticalAlignment.Center, new Thickness(m, 0, 0, 0));
            AddAutoScopeResizeHandle(grid, AutoScopeResizeDirection.Right, HorizontalAlignment.Right, VerticalAlignment.Center, new Thickness(0, 0, m, 0));
            AddAutoScopeResizeHandle(grid, AutoScopeResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, m, 0, 0));
            AddAutoScopeResizeHandle(grid, AutoScopeResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(0, 0, 0, m));
        }
    }
}
