using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Control cho Start node với hình tròn và icon play bên trong
    /// </summary>
    public static class StartNodeControl
    {
        public const double NodeSize = 100; // Kích thước hình tròn
        private const double TitleOffset = 8; // Khoảng cách từ top node đến bottom title

        public static Border CreateBorder(WorkflowNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            var grid = new Grid
            {
                Width = NodeSize,
                Height = NodeSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Icon SVG sử dụng SvgViewboxEx
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "play duotone-regular", System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 55,
                Height = 55,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new SolidColorBrush(Colors.White),
                Margin = new Thickness(4, 0, 0, 0) // Điều chỉnh vị trí icon play cho cân đối
            };
            grid.Children.Add(iconSvg);

            var border = new Border
            {
                Child = grid,
                Width = NodeSize,
                Height = NodeSize,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = node.NodeBrush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219)), // Default: SkyAzure
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(NodeSize / 2), // Hình tròn hoàn hảo
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Effect = FlowMy.Services.Rendering.GpuOptimizationHelper.CreateDropShadowEffect(),
                Tag = node
            };

            PopulateVisuals(node, grid, border);

            // Keyboard Port Position
            border.Focusable = true;
            border.FocusVisualStyle = null;

            bool isHovering = false;
            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (isHovering) border.Focus(); }));
            };
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
            };
            border.PreviewKeyDown += (s, e) =>
            {
                if (!isHovering) return;
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

            if (host != null)
            {
                border.MouseRightButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    OpenNodeDialog(node, host, ownerWindow);
                };
            }

            return border;
        }

        public static void RefreshVisual(WorkflowNode node)
        {
            if (node.Border == null) return;
            if (node.Border.Child is not Grid grid) return;
            PopulateVisuals(node, grid, node.Border);
        }

        private static void PopulateVisuals(WorkflowNode node, Grid grid, Border border)
        {
            grid.Children.Clear();
            border.Background = node.NodeBrush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219));
            border.BorderBrush = new SolidColorBrush(Colors.White);
            ApplyShape(node, node.RunMode, border, grid);

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "play duotone-regular", System.Globalization.CultureInfo.CurrentCulture) as Uri;

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

        private static double GetSharpnessScaleY(DiamondSharpness sharpness)
        {
            return sharpness switch
            {
                DiamondSharpness.Soft => 0.88,
                DiamondSharpness.Medium => 1.0,
                DiamondSharpness.Sharp => 1.15,
                _ => 1.0
            };
        }

        private static void OpenNodeDialog(WorkflowNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            if (node.Border?.IsMouseCaptured == true) node.Border.ReleaseMouseCapture();
            host.DraggedNode = null;
            if (host.ViewModel != null) host.ViewModel.SelectedNode = null;

            var dialogManager = GetOrCreateDialogManager(host);
            if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
            if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node) dialogManager.CloseCurrentDialog();

            var dialog = new StartEndNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
            dialogManager.OpenDialog(node, dialog, host);
        }

        private static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            if (host is WorkflowEditorWindow window)
            {
                var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager) return manager;
            }
            return new NodeDialogManager();
        }

        /// <summary>
        /// Tạo TextBlock để hiển thị title trên top của node
        /// </summary>
        public static TextBlock CreateTitleTextBlock(WorkflowNode node)
        {
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Start",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = node.NodeBrush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Background = Brushes.Transparent,
                Padding = new Thickness(4, 2, 4, 2),
                Visibility = Visibility.Visible
            };

            // Lưu reference vào node
            node.TitleTextBlockUI = titleTextBlock;

            // Đăng ký Loaded event để cập nhật vị trí sau khi render
            titleTextBlock.Loaded += (s, e) =>
            {
                if (node.TitleTextBlockUI != null)
                {
                    var parent = titleTextBlock.Parent as Canvas;
                    UpdateTitlePosition(node, parent);
                }
            };

            // Đăng ký SizeChanged để cập nhật vị trí khi kích thước thay đổi
            titleTextBlock.SizeChanged += (s, e) =>
            {
                if (node.TitleTextBlockUI != null)
                {
                    var parent = titleTextBlock.Parent as Canvas;
                    UpdateTitlePosition(node, parent);
                }
            };

            return titleTextBlock;
        }

        /// <summary>
        /// Cập nhật vị trí title khi node di chuyển hoặc sau khi zoom
        /// </summary>
        public static void UpdateTitlePosition(WorkflowNode node, Canvas? canvas)
        {
            if (node.TitleTextBlockUI == null) return;

            var titleTextBlock = node.TitleTextBlockUI;
            
            // Đảm bảo title visible
            if (titleTextBlock.Visibility != Visibility.Visible)
            {
                titleTextBlock.Visibility = Visibility.Visible;
            }

            // Sử dụng ActualWidth/Height nếu có, fallback về DesiredSize
            double titleWidth = titleTextBlock.ActualWidth;
            double titleHeight = titleTextBlock.ActualHeight;

            if (titleWidth <= 0 || titleHeight <= 0)
            {
                titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                titleWidth = titleTextBlock.DesiredSize.Width;
                titleHeight = titleTextBlock.DesiredSize.Height;
            }

            // Fallback nếu vẫn không có kích thước
            if (titleWidth <= 0) titleWidth = 40;
            if (titleHeight <= 0) titleHeight = 18;

            // Tính toán vị trí title ở trên node, căn giữa theo chiều ngang
            var titleX = node.X + (NodeSize / 2) - (titleWidth / 2);
            var extraDiamondOffset = node.IsStartDiamondVisual ? 14 : 0;
            var titleY = node.Y - titleHeight - TitleOffset - extraDiamondOffset;

            Canvas.SetLeft(titleTextBlock, titleX);
            Canvas.SetTop(titleTextBlock, titleY);
            Panel.SetZIndex(titleTextBlock, 20000); // Đảm bảo title luôn hiển thị trên cùng
        }

        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;
            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);
            if (port == null || port.Position == newPosition) return;
            port.Position = newPosition;
            host.UpdatePortsPositionOnSide(node, newPosition);
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
