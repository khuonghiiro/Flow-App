using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Diamond visual mode cho Conditional Node (If/ElseIf/Else).
    /// Hiển thị hình thoi chính + satellite circles cho mỗi nhánh.
    /// </summary>
    public static class ConditionalDiamondControl
    {
        public const double SatelliteScale = 1.4;
        public const double DiamondWidth = 100;
        public const double DiamondHeight = 100;
        public const double SatelliteRadius = 23 * SatelliteScale;
        public const double SatelliteSpacingY = 65 * SatelliteScale;
        public const double SatelliteDefaultOffsetX = 600;
        public const double AddCircleRadius = 15;

        /// <summary>
        /// Tạo Border chính chứa hình thoi (diamond) với icon bên trong.
        /// Sử dụng Polygon giống LoopNodeControl. Title sẽ là TextBlock riêng trên Canvas.
        /// </summary>
        public static Border CreateDiamondBorder(
            WorkflowNode node,
            Window? ownerWindow,
            IWorkflowEditorHost? host,
            Action addElseIfBranch)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // Diamond shape using Polygon (giống LoopNode)
            var diamond = new Polygon
            {
                Points = new PointCollection(new[]
                {
                    new Point(DiamondWidth / 2, 0),           // Top
                    new Point(DiamondWidth, DiamondHeight / 2), // Right
                    new Point(DiamondWidth / 2, DiamondHeight), // Bottom
                    new Point(0, DiamondHeight / 2)           // Left
                }),
                Fill = node.NodeBrush ?? new SolidColorBrush(Color.FromRgb(128, 90, 213)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                Stretch = Stretch.Fill
            };

            // Liquid Glass: đổi fill diamond thành gradient trong suốt
            if (LiquidGlassHelper.IsLiquidGlassMode(host))
            {
                var baseColor = LiquidGlassHelper.GetColorFromBrush(node.NodeBrush);
                diamond.Fill = LiquidGlassHelper.CreateGlassBackground(baseColor);
                diamond.Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
                diamond.StrokeThickness = 1.5;
            }

            var grid = new Grid
            {
                Width = DiamondWidth,
                Height = DiamondHeight,
                MinWidth = DiamondWidth,
                MinHeight = DiamondHeight,
                ClipToBounds = false
            };
            grid.Children.Add(diamond);

            // Icon bên trong diamond (giống LoopNode)
            var iconConverter = new FlowMy.Converters.IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "list-tree sharp-light", System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new FlowMy.Controls.SvgViewboxEx
            {
                Source = iconUri,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = LiquidGlassHelper.IsLiquidGlassMode(host)
                    ? LiquidGlassHelper.GetGlassIconBrush()
                    : (GetBrushFromTheme("TextOnWarningBrush") ?? new SolidColorBrush(Colors.White))
            };
            grid.Children.Add(iconSvg);

            // Liquid Glass: thêm glow effect lên grid chứa diamond
            if (LiquidGlassHelper.IsLiquidGlassMode(host))
            {
                var baseColor = LiquidGlassHelper.GetColorFromBrush(node.NodeBrush);
                var isLightColor = (0.299 * baseColor.R + 0.587 * baseColor.G + 0.114 * baseColor.B) / 255.0 > 0.65;
                grid.Effect = LiquidGlassHelper.CreateGlassEffect(baseColor, isLightColor);
            }

            // Title sẽ là TextBlock riêng trên Canvas (giống LoopNode)
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "If-Else",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = node.NodeBrush ?? new SolidColorBrush(Color.FromRgb(128, 90, 213)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            var border = new Border
            {
                Child = grid,
                Width = DiamondWidth,
                Height = DiamondHeight,
                MinWidth = DiamondWidth,
                MinHeight = DiamondHeight,
                Background = Brushes.Transparent,
                BorderBrush = null,
                BorderThickness = new Thickness(0),
                ClipToBounds = false,
                Cursor = Cursors.Hand,
                Effect = FlowMy.Services.Rendering.GpuOptimizationHelper.CreateDropShadowEffect(),
                Tag = node
            };

            // Keyboard Port Position
            border.Focusable = true;
            border.FocusVisualStyle = null;

            bool isHovering = false;

            // ⚠️ Force transparent background on all mouse events (giống LoopNode)
            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                border.Background = Brushes.Transparent;
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() => border.Background = Brushes.Transparent));
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (isHovering) border.Focus(); }));
            };
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                border.Background = Brushes.Transparent;
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() => border.Background = Brushes.Transparent));
            };
            border.MouseDown += (s, e) =>
            {
                border.Background = Brushes.Transparent;
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => border.Background = Brushes.Transparent));
            };
            border.MouseUp += (s, e) =>
            {
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => border.Background = Brushes.Transparent));
            };
            border.PreviewMouseUp += (s, e) =>
            {
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => border.Background = Brushes.Transparent));
            };
            border.LayoutUpdated += (s, e) =>
            {
                if (!ReferenceEquals(border.Background, Brushes.Transparent))
                {
                    border.Background = Brushes.Transparent;
                }
            };

            // Keyboard Port Position: Arrow = Port IN, Shift+Arrow = Port OUT (ALL output ports on diamond)
            border.PreviewKeyDown += (s, e) =>
            {
                if (!isHovering) return;
                // Guard: chỉ xử lý khi diamond border (hoặc child) thực sự có focus (tránh xung đột với satellite)
                if (!border.IsKeyboardFocusWithin) return;
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PortPosition? newPos = e.Key switch
                {
                    Key.Left  => PortPosition.Left,
                    Key.Up    => PortPosition.Top,
                    Key.Right => PortPosition.Right,
                    Key.Down  => PortPosition.Bottom,
                    _ => null
                };
                if (newPos == null) return;
                e.Handled = true;
                ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
            };

            // --- Use BaseNodeControlHelper for dialog support and cleanup ---
            // Note: WithTitleManagement, WithHoverBehavior, WithPropertySync are NOT used here
            // because the diamond has specialized transparent-background hover behavior and
            // its own title management (always visible, positioned above diamond apex).
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithDialogSupport(ctx => new ConditionalNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        /// <summary>
        /// Cập nhật vị trí title phía trên đỉnh diamond (giống LoopNode).
        /// </summary>
        public static void UpdateTitlePosition(WorkflowNode node, TextBlock? titleTextBlock, Border? border)
        {
            if (titleTextBlock == null || border == null) return;

            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);
            if (double.IsNaN(left)) left = node.X;
            if (double.IsNaN(top)) top = node.Y;

            if (titleTextBlock.ActualWidth == 0)
            {
                titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
            }

            var titleLeft = left + (DiamondWidth / 2) - (titleTextBlock.ActualWidth / 2);
            var titleTop = top - titleTextBlock.ActualHeight - 4;
            Canvas.SetLeft(titleTextBlock, titleLeft);
            Canvas.SetTop(titleTextBlock, titleTop);
            Panel.SetZIndex(titleTextBlock, 20000);
        }

        /// <summary>
        /// Tạo hình tròn "add" (⊕) ở góc dưới-trái hình thoi.
        /// Click vào → thêm nhánh else if mới.
        /// </summary>
        public static Border CreateAddCircle(
            WorkflowNode node,
            IWorkflowEditorHost host,
            Action addElseIfBranch)
        {
            var addBrush = GetBrushFromTheme("PrimaryBrush")
                ?? new SolidColorBrush(Color.FromRgb(76, 175, 80));

            var ellipse = new Ellipse
            {
                Width = AddCircleRadius * 2,
                Height = AddCircleRadius * 2,
                Fill = addBrush,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2
            };
            ApplySmoothCircleRendering(ellipse);

            var plusText = new TextBlock
            {
                Text = "+",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                RenderTransform = new TranslateTransform(0, -1),
                IsHitTestVisible = false
            };

            var addGrid = new Grid
            {
                Width = AddCircleRadius * 2,
                Height = AddCircleRadius * 2
            };
            addGrid.Children.Add(ellipse);
            addGrid.Children.Add(plusText);

            var addBorder = new Border
            {
                Child = addGrid,
                Width = AddCircleRadius * 2,
                Height = AddCircleRadius * 2,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Thêm điều kiện else if",
                Tag = $"AddSatellite:{node.Id}"
            };

            addBorder.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                addElseIfBranch();
            };

            // Hover effect
            addBorder.MouseEnter += (s, e) =>
            {
                ellipse.StrokeThickness = 3;
                ellipse.Effect = new DropShadowEffect
                {
                    Color = Colors.LimeGreen,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.6
                };
            };
            addBorder.MouseLeave += (s, e) =>
            {
                ellipse.StrokeThickness = 2;
                ellipse.Effect = null;
            };

            return addBorder;
        }

        /// <summary>
        /// Tạo satellite circle cho một nhánh điều kiện (if/else if/else).
        /// Hiển thị số thứ tự bên trong, title điều kiện phía trên.
        /// </summary>
        public static Border CreateSatelliteCircle(
            WorkflowNode node,
            ConditionalBranch branch,
            int index,
            IWorkflowEditorHost host,
            Window? ownerWindow,
            Action<ConditionalBranch> removeBranch)
        {
            // Color by index
            var colorKey = GetSatelliteColorKey(index);
            var portColor = GetColorFromTheme(colorKey + "Brush") ?? Colors.Orange;

            var ellipse = new Ellipse
            {
                Width = SatelliteRadius * 2,
                Height = SatelliteRadius * 2,
                Fill = LiquidGlassHelper.IsLiquidGlassMode(host)
                    ? LiquidGlassHelper.CreateGlassBackground(portColor)
                    : new SolidColorBrush(portColor),
                Stroke = LiquidGlassHelper.IsLiquidGlassMode(host)
                    ? LiquidGlassHelper.CreateGlassBorderBrush()
                    : new SolidColorBrush(Colors.White),
                StrokeThickness = LiquidGlassHelper.IsLiquidGlassMode(host) ? 1.5 : 2,
                Effect = CreateSatelliteBaseShadow()
            };
            ApplySmoothCircleRendering(ellipse);

            // Number inside circle
            var numberText = new TextBlock
            {
                Text = (index + 1).ToString(),
                FontSize = 16 * SatelliteScale,
                FontWeight = FontWeights.Bold,
                Foreground = LiquidGlassHelper.IsLiquidGlassMode(host)
                    ? LiquidGlassHelper.GetGlassIconBrush()
                    : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            var circleGrid = new Grid
            {
                Width = SatelliteRadius * 2,
                Height = SatelliteRadius * 2
            };
            circleGrid.Children.Add(ellipse);
            circleGrid.Children.Add(numberText);

            // Title label above the satellite
            var displayLabel = !string.IsNullOrWhiteSpace(branch.DisplayTitle)
                ? branch.DisplayTitle
                : branch.Label;

            var titleLabel = new TextBlock
            {
                Text = displayLabel,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(portColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                MaxWidth = 80,
                TextWrapping = TextWrapping.Wrap,
                Effect = null
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(titleLabel);
            stack.Children.Add(circleGrid);

            var satelliteBorder = new Border
            {
                Child = stack,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Tag = branch,
                ClipToBounds = false,
                Effect = null
            };

            // Hover effect
            satelliteBorder.MouseEnter += (s, e) =>
            {
                ellipse.StrokeThickness = 3;
                ellipse.Effect = new DropShadowEffect
                {
                    Color = portColor,
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            };
            satelliteBorder.MouseLeave += (s, e) =>
            {
                ellipse.StrokeThickness = 2;
                ellipse.Effect = CreateSatelliteBaseShadow();
            };

            // Right-click: context menu with edit title + remove
            var contextMenu = new ContextMenu();

            var editItem = new MenuItem { Header = "Chỉnh sửa điều kiện..." };
            editItem.Click += (s, e) =>
            {
                OpenNodeDialog(node, host, ownerWindow);
            };
            contextMenu.Items.Add(editItem);

            if (branch.CanRemove && branch.Label == "else if")
            {
                var removeItem = new MenuItem { Header = "Xoá nhánh này" };
                removeItem.Click += (s, e) =>
                {
                    removeBranch(branch);
                };
                contextMenu.Items.Add(removeItem);
            }

            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(CreatePortPositionMenu(
                "Vị trí Port IN",
                branch.SatelliteInputPosition,
                selected =>
                {
                    if (branch.SatelliteInputPosition == selected) return;
                    branch.SatelliteInputPosition = selected;
                    host.ReRenderConditionalNode(node);
                    host.RenderConditionalNodePorts(node);
                    host.SyncAllPortsZIndex(node);
                }));
            contextMenu.Items.Add(CreatePortPositionMenu(
                "Vị trí Port OUT",
                branch.Port?.Position ?? PortPosition.Right,
                selected =>
                {
                    if (branch.Port == null || branch.Port.Position == selected) return;
                    branch.Port.Position = selected;
                    host.ReRenderConditionalNode(node);
                    host.RenderConditionalNodePorts(node);
                    host.SyncAllPortsZIndex(node);
                }));

            satelliteBorder.ContextMenu = contextMenu;

            // Keyboard Port Position for satellite
            satelliteBorder.Focusable = true;
            satelliteBorder.FocusVisualStyle = null;

            bool satHovering = false;
            satelliteBorder.MouseEnter += (s, e) =>
            {
                satHovering = true;
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (satHovering) satelliteBorder.Focus(); }));
            };
            satelliteBorder.MouseLeave += (s, e) =>
            {
                satHovering = false;
            };
            satelliteBorder.PreviewKeyDown += (s, e) =>
            {
                if (!satHovering) return;
                // Guard: chỉ xử lý khi satellite (hoặc child) thực sự có focus (tránh xung đột với diamond)
                if (!satelliteBorder.IsKeyboardFocusWithin) return;
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PortPosition? newPos = e.Key switch
                {
                    Key.Left  => PortPosition.Left,
                    Key.Up    => PortPosition.Top,
                    Key.Right => PortPosition.Right,
                    Key.Down  => PortPosition.Bottom,
                    _ => null
                };
                if (newPos == null) return;
                e.Handled = true;

                if (!isShift)
                {
                    // Arrow = đổi Port IN (SatelliteInputPosition)
                    if (branch.SatelliteInputPosition == newPos.Value) return;
                    branch.SatelliteInputPosition = newPos.Value;
                }
                else
                {
                    // Shift+Arrow = đổi Port OUT (branch.Port.Position)
                    if (branch.Port == null || branch.Port.Position == newPos.Value) return;
                    branch.Port.Position = newPos.Value;
                }

                host.ReRenderConditionalNode(node);
                host.RenderConditionalNodePorts(node);
                host.SyncAllPortsZIndex(node);
                var cons = host.ViewModel?.Connections;
                if (cons != null && cons.Count > 0)
                {
                    try
                    {
                        host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                        host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                    }
                    catch { }
                }
            };

            // Store reference
            branch.SatelliteBorder = satelliteBorder;

            return satelliteBorder;
        }

        private static MenuItem CreatePortPositionMenu(string header, PortPosition current, Action<PortPosition> onSelected)
        {
            var menu = new MenuItem { Header = header };
            foreach (var pos in new[] { PortPosition.Left, PortPosition.Top, PortPosition.Right, PortPosition.Bottom })
            {
                var item = new MenuItem
                {
                    Header = pos.ToString(),
                    IsCheckable = true,
                    IsChecked = pos == current
                };
                item.Click += (_, __) => onSelected(pos);
                menu.Items.Add(item);
            }
            return menu;
        }

        private static DropShadowEffect CreateSatelliteBaseShadow()
        {
            return new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = 2,
                BlurRadius = 6,
                Opacity = 0.35
            };
        }

        /// <summary>
        /// Tạo đường nối (line) từ diamond đến satellite circle.
        /// Style giống connection line chính (solid, bezier curve, rounded caps).
        /// </summary>
        public static Path CreateSatelliteLine(
            Point diamondCenter,
            Point satelliteCenter,
            Color color)
        {
            var line = new Path
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2.5,
                Fill = null,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeDashArray = null,
                IsHitTestVisible = false
            };

            UpdateLineGeometry(line, diamondCenter, satelliteCenter);

            return line;
        }

        /// <summary>
        /// Cập nhật geometry cho đường nối (bezier curve giống connection line chính).
        /// </summary>
        public static void UpdateLineGeometry(Path line, Point from, Point to)
        {
            var fig = new PathFigure { StartPoint = from };

            // Tính bezier curve giống connection line: control points đi ngang trước rồi bẻ
            double dx = Math.Abs(to.X - from.X);
            double offset = Math.Max(40, dx * 0.4);

            var cp1 = new Point(from.X + offset, from.Y);
            var cp2 = new Point(to.X - offset, to.Y);
            fig.Segments.Add(new BezierSegment(cp1, cp2, to, true));

            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            line.Data = geo;
        }

        /// <summary>
        /// Bật animation nét đứt chạy + energy glow trên satellite line khi flow chạy qua.
        /// </summary>
        public static void StartSatelliteLineAnimation(Path line, Color color)
        {
            if (line == null) return;

            // Chuyển sang nét đứt chạy
            line.StrokeDashArray = new DoubleCollection { 6, 3 };
            line.StrokeThickness = 3.0;
            line.Effect = null;

            // Animation chạy nét đứt (StrokeDashOffset)
            var dashAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = -18,  // đảo chiều để chạy từ diamond -> satellite
                Duration = new Duration(TimeSpan.FromSeconds(1.25)),
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            line.BeginAnimation(Shape.StrokeDashOffsetProperty, dashAnim);

            // Animation Opacity pulse (energy effect)
            var pulseAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.6,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.4)),
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            line.BeginAnimation(UIElement.OpacityProperty, pulseAnim);
        }

        /// <summary>
        /// Tắt animation satellite line, quay về style bình thường.
        /// </summary>
        public static void StopSatelliteLineAnimation(Path line)
        {
            if (line == null) return;

            // Xóa animation
            line.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
            line.BeginAnimation(UIElement.OpacityProperty, null);

            // Quay về style bình thường (solid line)
            line.StrokeDashArray = null;
            line.StrokeThickness = 2.5;
            line.Effect = null;
            line.Opacity = 1.0;
            line.StrokeDashOffset = 0;
        }

        /// <summary>
        /// Tính vị trí mặc định cho satellite circle (bên phải diamond, dọc).
        /// </summary>
        public static Point GetDefaultSatelliteOffset(int index, int totalCount)
        {
            double totalHeight = (totalCount - 1) * SatelliteSpacingY;
            double startY = (DiamondHeight / 2) - (totalHeight / 2);
            return new Point(SatelliteDefaultOffsetX, startY + (index * SatelliteSpacingY));
        }

        /// <summary>
        /// Lấy color key cho satellite theo index.
        /// </summary>
        public static string GetSatelliteColorKey(int index)
        {
            return index switch
            {
                0 => "ChocolateBrown",
                1 => "OceanBlue",
                2 => "EmeraldGreen",
                3 => "SunsetOrange",
                4 => "RoyalPurple",
                5 => "RubyRed",
                6 => "GoldenYellow",
                7 => "TealCyan",
                8 => "LavenderDream",
                9 => "CrimsonRose",
                _ => "SlateGray"
            };
        }

        private static Color? GetColorFromTheme(string resourceKey)
        {
            try
            {
                var brush = Application.Current.TryFindResource(resourceKey) as SolidColorBrush;
                return brush?.Color;
            }
            catch { return null; }
        }

        private static Brush? GetBrushFromTheme(string resourceKey)
        {
            try
            {
                return Application.Current.TryFindResource(resourceKey) as Brush;
            }
            catch { return null; }
        }

        private static void ApplySmoothCircleRendering(Ellipse ellipse)
        {
            if (ellipse == null) return;

            // Ép anti-alias cho hình tròn để tránh viền cưa ở màn hình scale cao.
            ellipse.UseLayoutRounding = false;
            ellipse.SnapsToDevicePixels = false;
            RenderOptions.SetEdgeMode(ellipse, EdgeMode.Unspecified);
            RenderOptions.SetBitmapScalingMode(ellipse, BitmapScalingMode.HighQuality);
        }

        /// <summary>
        /// Opens the ConditionalNode dialog. Used by context menu items on satellite circles.
        /// </summary>
        private static void OpenNodeDialog(WorkflowNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                host.DraggedNode = null;
                if (host.ViewModel != null)
                    host.ViewModel.SelectedNode = null;

                var dialogManager = BaseNodeControlHelper.GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new ConditionalNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Diamond border xử lý Port IN (Arrow) và Port OUT (Shift+Arrow).
        /// Arrow = đổi Port IN (1 port).
        /// Shift+Arrow = đổi TẤT CẢ output ports cùng lúc (hướng ra từ diamond).
        /// </summary>
        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;

            bool portsChanged = false;
            if (isInputPort)
            {
                var inputPort = node.Ports.FirstOrDefault(p => p.IsInput);
                if (inputPort != null && inputPort.Position != newPosition)
                {
                    inputPort.Position = newPosition;
                    portsChanged = true;
                }
            }
            else
            {
                // Đổi TẤT CẢ output ports cùng lúc (giống dialog SavePortPositions)
                foreach (var outputPort in node.Ports.Where(p => !p.IsInput))
                {
                    if (outputPort.Position != newPosition)
                    {
                        outputPort.Position = newPosition;
                        portsChanged = true;
                    }
                }
            }

            if (!portsChanged) return;

            // ConditionalNode cần re-render ports đặc biệt
            host.ReRenderConditionalNode(node);
            host.RenderConditionalNodePorts(node);
            host.SyncAllPortsZIndex(node);

            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                }
                catch { }
            }
        }
    }
}
