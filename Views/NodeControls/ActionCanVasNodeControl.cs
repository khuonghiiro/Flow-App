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
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Linq;

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
            border.Style = null; // Đảm bảo không có style mặc định từ WPF gây shadow

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

            var togglePlaybackInfoBtn = new System.Windows.Controls.Button
            {
                Content = node.ShowPlaybackInfo ? "👁" : "🚫",
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 8, 30, 0),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                ToolTip = "Bật/tắt hiển thị dòng thông tin trạng thái khi chạy",
                Cursor = Cursors.Hand
            };
            
            var btnStyle = Application.Current?.TryFindResource("DynamicOutlineCircleButton") as Style;
            if (btnStyle != null) togglePlaybackInfoBtn.Style = btnStyle;

            FlowMy.Helpers.ButtonHelper.SetColorTheme(togglePlaybackInfoBtn, node.ShowPlaybackInfo ? "Primary" : "Danger");

            Panel.SetZIndex(togglePlaybackInfoBtn, 100);
            togglePlaybackInfoBtn.Click += (s, e) => 
            { 
                node.ShowPlaybackInfo = !node.ShowPlaybackInfo; 
                togglePlaybackInfoBtn.Content = node.ShowPlaybackInfo ? "👁" : "🚫"; 
                FlowMy.Helpers.ButtonHelper.SetColorTheme(togglePlaybackInfoBtn, node.ShowPlaybackInfo ? "Primary" : "Danger");
            };
            root.Children.Add(togglePlaybackInfoBtn);

            // Add Resize Handles
            AddResizeHandle(root, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top);
            AddResizeHandle(root, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top);
            AddResizeHandle(root, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top);
            AddResizeHandle(root, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom);
            AddResizeHandle(root, ResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom);
            AddResizeHandle(root, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom);

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
                [nameof(ActionCanVasNode.ShowPlaybackInfo)] = ctx => {
                    togglePlaybackInfoBtn.Content = node.ShowPlaybackInfo ? "👁" : "🚫";
                    FlowMy.Helpers.ButtonHelper.SetColorTheme(togglePlaybackInfoBtn, node.ShowPlaybackInfo ? "Success" : "Secondary");
                },
                [nameof(WorkflowNode.NodeBrush)] = ctx => { /* Do nothing to Border.Background, we manage our own fill */ },
                ["TitleColorMode"] = ctx => titleText.Foreground = ResolveTitleBrush(node, ParseColor(node.UseUnifiedColors ? node.BodyBackgroundColorHex : node.BodyBorderColorHex, Colors.Blue)),
                ["TitleColorKey"] = ctx => titleText.Foreground = ResolveTitleBrush(node, ParseColor(node.UseUnifiedColors ? node.BodyBackgroundColorHex : node.BodyBorderColorHex, Colors.Blue))
            };

            // ─── FLUENT API ───
            BaseNodeControlHelper
                .Initialize(border, titleText, node, host)
                .WithTitleManagement()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new ActionCanVasNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            border.Effect = null;
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

            UpdateResizeHandleScale(border, node.BodyWidth, node.BodyHeight, borderColor);
            
            // Connection port scaling on ApplyVisuals
            if (node.Ports != null)
            {
                var scale = Math.Clamp(titleScale, 1.0, 2.0);
                foreach (var port in node.Ports)
                {
                    if (port.PortUI != null)
                    {
                        var shape = FlowMy.Services.Rendering.PortRenderer.GetActualPortShape(port.PortUI);
                        if (shape != null)
                        {
                            shape.Width = 12 * scale;
                            shape.Height = 25 * scale;
                            shape.Tag = new Size(shape.Width, shape.Height);
                        }
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

        private static void UpdateResizeHandleScale(Border border, double bodyWidth, double bodyHeight, Color handleColor)
        {
            if (border.Child is not Grid grid) return;
            var scale = Math.Clamp(Math.Max(bodyWidth / 600.0, bodyHeight / 300.0), 1.0, 2.0);
            foreach (var child in grid.Children)
            {
                if (child is not Ellipse handle || handle.Tag is not ResizeDirection) continue;
                handle.Fill = new SolidColorBrush(handleColor);
                handle.RenderTransformOrigin = new Point(0.5, 0.5);
                handle.RenderTransform = new ScaleTransform(scale, scale);
            }
        }

        private static void AttachResizeLogic(Border border, ActionCanVasNode node, Action refreshPortsAndConnections)
        {
            bool isResizing = false;
            ResizeDirection resizeDirection = ResizeDirection.None;
            Point start = new Point();
            double originalX = 0, originalY = 0, originalW = 0, originalH = 0;

            border.PreviewMouseDown += (_, e) =>
            {
                bool isRecording = false;
                try { isRecording = Application.Current.Windows.OfType<MacroRecorderOverlay>().Any(w => w.IsVisible); } catch { }
                bool isPlaying = node.ExecutionStatusTextUI?.Text?.Contains("⏳") == true;
                if (isRecording || isPlaying)
                {
                    e.Handled = true;
                    return;
                }

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
                
                var borderColor = ParseColor(node.UseUnifiedColors ? node.BodyBackgroundColorHex : node.BodyBorderColorHex, Colors.Blue);
                var borderAlpha = (byte)Math.Round(Math.Clamp(node.BorderOpacityPercent / 100.0, 0, 1) * 255);
                borderColor.A = borderAlpha;
                UpdateResizeHandleScale(border, newW, newH, borderColor);
                
                var titleScaleDynamic = Math.Max(1.0, Math.Max(newW / 600.0, newH / 300.0));
                var scaleDynamic = Math.Clamp(titleScaleDynamic, 1.0, 2.0);
                if (node.Ports != null)
                {
                    foreach (var port in node.Ports)
                    {
                        if (port.PortUI != null)
                        {
                            var shape = FlowMy.Services.Rendering.PortRenderer.GetActualPortShape(port.PortUI);
                            if (shape != null)
                            {
                                shape.Width = 12 * scaleDynamic;
                                shape.Height = 25 * scaleDynamic;
                                // Cập nhật lại kích thước cho Tag để highlighter không bị sai
                                shape.Tag = new Size(shape.Width, shape.Height);
                            }
                        }
                    }
                }
                
                Canvas.SetLeft(border, newX);
                Canvas.SetTop(border, newY);
                
                border.UpdateLayout();
                refreshPortsAndConnections();
                
                e.Handled = true;
            };

            border.PreviewMouseUp += (_, e) =>
            {
                bool isRecording = false;
                try { isRecording = Application.Current.Windows.OfType<MacroRecorderOverlay>().Any(w => w.IsVisible); } catch { }
                bool isPlaying = node.ExecutionStatusTextUI?.Text?.Contains("⏳") == true;
                if (isRecording || isPlaying)
                {
                    e.Handled = true;
                    return;
                }

                if (!isResizing) return;
                isResizing = false;
                resizeDirection = ResizeDirection.None;
                border.ReleaseMouseCapture();
                border.Cursor = Cursors.Arrow;
                e.Handled = true;
            };
        }

        private static Storyboard? _currentPlaybackStoryboard;

        public static void StartPlaybackEffect(ActionCanVasNode node)
        {
            if (node.Border == null) return;
            var border = node.Border;
            var effectType = node.PlaybackEffectType;
            if (effectType == Models.BorderEffectType.None) return;

            var color = ParseColor(node.PlaybackBorderColorHex, Colors.Cyan);

            // Bỏ hẳn DropShadowEffect theo yêu cầu người dùng
            border.Effect = null;
            border.BorderBrush = new SolidColorBrush(color);
            border.BorderThickness = new Thickness(node.PlaybackBorderThickness);

            _currentPlaybackStoryboard?.Stop();
            _currentPlaybackStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

            switch (effectType)
            {
                case Models.BorderEffectType.Pulse:
                case Models.BorderEffectType.Glow:
                    var pulseAnim = new DoubleAnimation { From = 0.3, To = node.PlaybackOpacity, Duration = TimeSpan.FromSeconds(0.5), AutoReverse = true };
                    Storyboard.SetTarget(pulseAnim, border);
                    Storyboard.SetTargetProperty(pulseAnim, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Opacity)"));
                    _currentPlaybackStoryboard.Children.Add(pulseAnim);
                    break;
                case Models.BorderEffectType.Rainbow:
                    var colors = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue, Colors.Indigo, Colors.Violet };
                    for (int i = 0; i < colors.Length; i++)
                    {
                        var brushColorAnim = new ColorAnimation
                        {
                            From = colors[i], To = colors[(i + 1) % colors.Length],
                            Duration = TimeSpan.FromSeconds(1), BeginTime = TimeSpan.FromSeconds(i)
                        };
                        Storyboard.SetTarget(brushColorAnim, border);
                        Storyboard.SetTargetProperty(brushColorAnim, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));
                        _currentPlaybackStoryboard.Children.Add(brushColorAnim);
                    }
                    break;
            }

            _currentPlaybackStoryboard.Begin();
        }

        public static void StopPlaybackEffect(ActionCanVasNode node)
        {
            if (node.Border == null) return;
            _currentPlaybackStoryboard?.Stop();
            _currentPlaybackStoryboard = null;
            node.Border.Effect = null;
            node.Border.BorderThickness = new Thickness(0);
            node.Border.BorderBrush = Brushes.Transparent;
            
            // Restore visual state
            if (node.Border.Child is Grid grid)
            {
                var fillRect = grid.Children.OfType<Rectangle>().FirstOrDefault(r => r.Fill != null);
                var borderRect = grid.Children.OfType<Rectangle>().FirstOrDefault(r => r.Stroke != null);
                var titleText = grid.Children.OfType<TextBlock>().FirstOrDefault();
                if (fillRect != null && borderRect != null && titleText != null)
                {
                    ApplyVisuals(node, node.Border, fillRect, borderRect, titleText);
                }
            }
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

            // Đảm bảo brush không có effect
            if (resultBrush is SolidColorBrush solidBrush)
            {
                // SolidColorBrush không có effect property
            }

            return resultBrush;
        }

        /*
         * 💡 HƯỚNG DẪN LOẠI BỎ SHADOW CHO NODE:
         * Bóng đổ (shadow) của các Node trong Editor được áp dụng tự động bởi phương thức:
         * ImageProcessingNodeControl.ApplyEditorGpuChrome(...) [Views/NodeControls/ImageProcessingNodeControl.cs]
         * Khi kéo, thả, chọn hoặc tải lại workflow, hàm đó sẽ gán border.Effect = GpuOptimizationHelper.CreateDropShadowEffect().
         * Để bỏ shadow hoàn toàn cho một Node mới (như ActionCanVasNode hoặc BodyContainerNode):
         * Thêm loại Node đó vào nhánh check loại trừ của hàm ApplyEditorGpuChrome:
         *     if (node is VideoProcessingNode || node is FlowMy.Models.Nodes.BodyContainerNode || node is FlowMy.Models.Nodes.ActionCanVasNode) {
         *         border.Effect = null;
         *         GpuOptimizationHelper.ApplyToBorder(border, isDragging: false, forceCache: false);
         *         return;
         *     }
         */
    }
}
