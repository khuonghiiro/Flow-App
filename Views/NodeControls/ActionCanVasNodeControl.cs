using FlowMy.Controls;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    public static class ActionCanVasNodeControl
    {
        private enum ResizeDirection
        {
            None, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left
        }

        public static Border CreateBorder(ActionCanVasNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var border = new Border
            {
                Width = node.BodyWidth,
                Height = node.BodyHeight,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(0), // We use an inner rectangle for border so we can have dash styles
                Tag = node,
                Background = Brushes.Transparent // Needed for hit testing
            };

            var root = new Grid { ClipToBounds = false };
            border.Child = root;
            node.ContainerBorder = border;

            var fillRect = new Rectangle { RadiusX = 10, RadiusY = 10 };
            root.Children.Add(fillRect);

            var borderRect = new Rectangle
            {
                RadiusX = 10,
                RadiusY = 10,
                StrokeThickness = node.BorderThickness,
                StrokeDashArray = GetDashArray(node.BorderDashStyle, node.BorderDashSpacing)
            };
            root.Children.Add(borderRect);

            var titleText = new TextBlock
            {
                Text = node.Title ?? "Thao tác canvas",
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                IsHitTestVisible = false
            };
            titleText.Style = null; // Prevent global WPF styles
            node.TitleTextBlockUI = titleText;

            // Add Resize Handles
            AddResizeHandle(root, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top);
            AddResizeHandle(root, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top);
            AddResizeHandle(root, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top);
            AddResizeHandle(root, ResizeDirection.Right, HorizontalAlignment.Right, VerticalAlignment.Center);
            AddResizeHandle(root, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom);
            AddResizeHandle(root, ResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom);
            AddResizeHandle(root, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom);
            AddResizeHandle(root, ResizeDirection.Left, HorizontalAlignment.Left, VerticalAlignment.Center);

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

            border.SizeChanged += (s, e) =>
            {
                RefreshPortsAndConnections();
            };

            ApplyVisuals(node, border, fillRect, borderRect, titleText);
            AttachResizeLogic(border, node, RefreshPortsAndConnections);

            // Custom property handlers for Sync
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(ActionCanVasNode.BodyWidth)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                [nameof(ActionCanVasNode.BodyHeight)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                [nameof(ActionCanVasNode.BodyBackgroundColorHex)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                [nameof(ActionCanVasNode.BodyBorderColorHex)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                [nameof(ActionCanVasNode.UseUnifiedColors)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                [nameof(ActionCanVasNode.BackgroundOpacityPercent)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                [nameof(ActionCanVasNode.BorderOpacityPercent)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                [nameof(ActionCanVasNode.BorderThickness)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                [nameof(ActionCanVasNode.BorderDashStyle)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                [nameof(ActionCanVasNode.BorderDashSpacing)] = ctx => ApplyVisuals(node, border, fillRect, borderRect, titleText),
                ["TitleColorMode"] = ctx => titleText.Foreground = ResolveTitleBrush(node, ParseColor(node.UseUnifiedColors ? node.BodyBackgroundColorHex : node.BodyBorderColorHex, Colors.Blue)),
                ["TitleColorKey"] = ctx => titleText.Foreground = ResolveTitleBrush(node, ParseColor(node.UseUnifiedColors ? node.BodyBackgroundColorHex : node.BodyBorderColorHex, Colors.Blue))
            };

            // ─── FLUENT API ───
            BaseNodeControlHelper
                .Initialize(border, titleText, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new ActionCanVasNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        private static void ApplyVisuals(ActionCanVasNode node, Border border, Rectangle fillRect, Rectangle borderRect, TextBlock titleText)
        {
            border.Width = node.BodyWidth;
            border.Height = node.BodyHeight;

            var backgroundColor = ParseColor(node.BodyBackgroundColorHex, Color.FromRgb(107, 114, 128));
            var alpha = (byte)Math.Round(Math.Clamp(node.BackgroundOpacityPercent / 100.0, 0, 1) * 255);
            backgroundColor.A = alpha;

            var borderColor = ParseColor(node.UseUnifiedColors ? node.BodyBackgroundColorHex : node.BodyBorderColorHex, Colors.Blue);
            var borderAlpha = (byte)Math.Round(Math.Clamp(node.BorderOpacityPercent / 100.0, 0, 1) * 255);
            borderColor.A = borderAlpha;

            fillRect.Fill = new SolidColorBrush(backgroundColor);
            borderRect.Stroke = new SolidColorBrush(borderColor);
            borderRect.StrokeThickness = node.BorderThickness;
            borderRect.StrokeDashArray = GetDashArray(node.BorderDashStyle, node.BorderDashSpacing);

            // Text scaling
            var titleScale = Math.Max(1.0, Math.Max(node.BodyWidth / 600.0, node.BodyHeight / 300.0));
            titleText.FontSize = 12 * titleScale;
            titleText.Margin = new Thickness(0);
            titleText.Foreground = ResolveTitleBrush(node, borderColor);

            // Resize handle colors
            if (border.Child is Grid root)
            {
                var scale = Math.Clamp(titleScale, 1.0, 2.0);
                foreach (var child in root.Children)
                {
                    if (child is Ellipse handle && handle.Tag is ResizeDirection)
                    {
                        handle.Fill = new SolidColorBrush(borderColor);
                        handle.RenderTransformOrigin = new Point(0.5, 0.5);
                        handle.RenderTransform = new ScaleTransform(scale, scale);
                    }
                }
            }
        }

        private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign)
        {
            var handle = new Ellipse
            {
                Width = 12, Height = 12,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                HorizontalAlignment = hAlign, VerticalAlignment = vAlign,
                Margin = new Thickness(2), Tag = direction,
                Cursor = GetCursor(direction)
            };
            grid.Children.Add(handle);
        }

        private static void AttachResizeLogic(Border border, ActionCanVasNode node, Action refreshPortsAndConnections)
        {
            bool isResizing = false;
            ResizeDirection resizeDirection = ResizeDirection.None;
            Point start = new Point();
            double originalX = 0, originalY = 0, originalW = 0, originalH = 0;

            border.PreviewMouseDown += (_, e) =>
            {
                if (e.OriginalSource is not Ellipse handle || handle.Tag is not ResizeDirection dir) return;
                isResizing = true;
                resizeDirection = dir;
                start = e.GetPosition(border.Parent as UIElement);
                originalX = node.X; originalY = node.Y;
                originalW = node.BodyWidth; originalH = node.BodyHeight;
                border.CaptureMouse();
                border.Cursor = GetCursor(dir);
                e.Handled = true;
            };

            border.PreviewMouseMove += (_, e) =>
            {
                if (!isResizing) return;
                var current = e.GetPosition(border.Parent as UIElement);
                var dx = current.X - start.X;
                var dy = current.Y - start.Y;

                var newX = originalX; var newY = originalY;
                var newW = originalW; var newH = originalH;

                if (resizeDirection is ResizeDirection.Left or ResizeDirection.TopLeft or ResizeDirection.BottomLeft)
                {
                    newW = Math.Max(100, originalW - dx);
                    newX = originalX + (originalW - newW);
                }
                if (resizeDirection is ResizeDirection.Right or ResizeDirection.TopRight or ResizeDirection.BottomRight)
                    newW = Math.Max(100, originalW + dx);

                if (resizeDirection is ResizeDirection.Top or ResizeDirection.TopLeft or ResizeDirection.TopRight)
                {
                    newH = Math.Max(100, originalH - dy);
                    newY = originalY + (originalH - newH);
                }
                if (resizeDirection is ResizeDirection.Bottom or ResizeDirection.BottomLeft or ResizeDirection.BottomRight)
                    newH = Math.Max(100, originalH + dy);

                node.X = newX; node.Y = newY;
                node.BodyWidth = newW; node.BodyHeight = newH;
                border.Width = newW; border.Height = newH;
                
                Canvas.SetLeft(border, newX);
                Canvas.SetTop(border, newY);
                e.Handled = true;
            };

            border.PreviewMouseUp += (_, e) =>
            {
                if (!isResizing) return;
                isResizing = false;
                resizeDirection = ResizeDirection.None;
                border.ReleaseMouseCapture();
                border.Cursor = Cursors.Arrow;
                e.Handled = true;
            };
        }

        private static Cursor GetCursor(ResizeDirection direction) => direction switch
        {
            ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
            ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
            ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
            ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
            _ => Cursors.Arrow
        };

        private static DoubleCollection GetDashArray(BorderDashStyle style, double spacing) => style switch
        {
            BorderDashStyle.Solid => new DoubleCollection(),
            BorderDashStyle.Dash => new DoubleCollection { 5, spacing },
            BorderDashStyle.Dot => new DoubleCollection { 2, spacing },
            BorderDashStyle.DashDot => new DoubleCollection { 5, spacing, 2, spacing },
            BorderDashStyle.DashDotDot => new DoubleCollection { 5, spacing, 2, spacing, 2, spacing },
            _ => new DoubleCollection { 5, spacing }
        };

        private static Color ParseColor(string? input, Color fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input)) return fallback;
                var token = input.Trim();
                if (token.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                    return (Color)ColorConverter.ConvertFromString(token);
                var resource = Application.Current.TryFindResource(token);
                if (resource is SolidColorBrush sb) return sb.Color;
                if (resource is Color c) return c;
                return (Color)ColorConverter.ConvertFromString(token);
            }
            catch { return fallback; }
        }

        private static Brush ResolveTitleBrush(ActionCanVasNode node, Color fallback)
        {
            Brush resultBrush;
            if (node.TitleColorMode != Models.TitleColorMode.CustomColor ||
                string.IsNullOrWhiteSpace(node.TitleColorKey) ||
                string.Equals(node.TitleColorKey, "NodeColor", StringComparison.OrdinalIgnoreCase))
            {
                resultBrush = new SolidColorBrush(fallback);
            }
            else if (string.Equals(node.TitleColorKey, "LimeGreen", StringComparison.OrdinalIgnoreCase))
            {
                resultBrush = new SolidColorBrush(Colors.LimeGreen);
            }
            else
            {
                var resource = Application.Current.TryFindResource(node.TitleColorKey);
                if (resource is Brush brush)
                    resultBrush = brush;
                else if (resource is Color color)
                    resultBrush = new SolidColorBrush(color);
                else
                    resultBrush = new SolidColorBrush(fallback);
            }
            return resultBrush;
        }
    }
}
