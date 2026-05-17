using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using FlowMy.Workflow;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        #region Node Template Drag and Drop

        // Track mouse movement để phân biệt click và drag
        private Point _templateMouseDownPos;
        private bool _templateHasMoved = false;
        private TemplateFactory _templateFactory = null!;
        private Border? _draggingTemplateBorder = null;
        private Effect? _originalTemplateEffect = null;

        /// <summary>
        /// Xử lý khi bắt đầu kéo node từ template menu
        /// </summary>
        private void NodeTemplate_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is Border border && border.Tag is string nodeType)
            {
                _templateMouseDownPos = e.GetPosition(this);
                _templateHasMoved = false;
                _isDraggingFromTemplate = true;
                _draggingNodeType = nodeType;
                _draggingTemplateBorder = border;
                _originalTemplateEffect = border.Effect; // Lưu Effect gốc
                border.CaptureMouse();

                // Tạo ghost preview ngay
                CreateDragGhost(border, nodeType);
                Point mousePos = e.GetPosition(WorkflowCanvas);
                Canvas.SetLeft(_dragGhost, mousePos.X - 75);
                Canvas.SetTop(_dragGhost, mousePos.Y - 40);

                e.Handled = true;
            }
        }

        /// <summary>
        /// Xử lý khi di chuyển chuột khi đang kéo từ template
        /// </summary>
        private void NodeTemplate_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingFromTemplate && e.LeftButton == MouseButtonState.Pressed)
            {
                // Kiểm tra xem có di chuyển không
                Point currentPos = e.GetPosition(this);
                if (Math.Abs(currentPos.X - _templateMouseDownPos.X) > 5 ||
                    Math.Abs(currentPos.Y - _templateMouseDownPos.Y) > 5)
                {
                    if (!_templateHasMoved)
                    {
                        _templateHasMoved = true;
                        // Loại bỏ bóng mờ khi bắt đầu kéo
                        if (_draggingTemplateBorder != null)
                        {
                            _draggingTemplateBorder.Effect = null;
                        }
                    }
                }

                // Cập nhật vị trí ghost theo chuột
                if (_dragGhost != null)
                {
                    try
                    {
                        Point mousePos = e.GetPosition(WorkflowCanvas);
                        Canvas.SetLeft(_dragGhost, mousePos.X - 75); // Center ghost
                        Canvas.SetTop(_dragGhost, mousePos.Y - 40);
                        // ✅ Đảm bảo z-index luôn cao nhất khi di chuyển
                        Panel.SetZIndex(_dragGhost, 2000000);
                    }
                    catch
                    {
                        // Nếu không lấy được vị trí trên canvas, dùng vị trí window
                        Point windowPos = e.GetPosition(this);
                        Canvas.SetLeft(_dragGhost, windowPos.X - 275); // Trừ đi menu width và center
                        Canvas.SetTop(_dragGhost, windowPos.Y - 100);
                        // ✅ Đảm bảo z-index luôn cao nhất khi di chuyển
                        Panel.SetZIndex(_dragGhost, 2000000);
                    }
                }
            }
        }

        /// <summary>
        /// Xử lý khi thả chuột từ template
        /// </summary>
        private void NodeTemplate_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingFromTemplate && _draggingNodeType != null)
            {
                Point dropPosition;

                // Nếu không di chuyển (chỉ click), tạo node ở vị trí random trong viewport
                if (!_templateHasMoved)
                {
                    dropPosition = GetRandomViewportPosition();
                }
                else
                {
                    // Lấy vị trí từ ghost nếu có
                    if (_dragGhost != null)
                    {
                        dropPosition = new Point(
                            Canvas.GetLeft(_dragGhost) + 75, // Center của ghost
                            Canvas.GetTop(_dragGhost) + 40
                        );
                    }
                    else
                    {
                        // Fallback: thử lấy vị trí trên canvas
                        try
                        {
                            dropPosition = e.GetPosition(WorkflowCanvas);
                        }
                        catch
                        {
                            // Nếu không lấy được, dùng vị trí window
                            Point windowPos = e.GetPosition(this);
                            dropPosition = new Point(windowPos.X - 200, windowPos.Y - 100);
                        }
                    }
                }

                CreateNodeFromTemplate(_draggingNodeType, dropPosition.X, dropPosition.Y);

                // Xóa ghost
                RemoveDragGhost();

                // Khôi phục bóng mờ gốc
                if (_draggingTemplateBorder != null)
                {
                    _draggingTemplateBorder.Effect = _originalTemplateEffect;
                    _draggingTemplateBorder.ReleaseMouseCapture();
                }

                _isDraggingFromTemplate = false;
                _draggingNodeType = null;
                _templateHasMoved = false;
                _draggingTemplateBorder = null;
                _originalTemplateEffect = null;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Tạo node từ template
        /// </summary>
        private void CreateNodeFromTemplate(string nodeType, double x, double y)
        {
            if (ViewModel == null) return;
            var newNode = _templateFactory.Create(nodeType, x, y);

            // Áp dụng màu title hàng loạt nếu có
            ApplyBulkTitleColorToNode(newNode);

            ViewModel.Nodes.Add(newNode);

            // Đóng dialog nếu đang mở
            if (_nodeDialogManager != null && _nodeDialogManager.IsDialogOpen)
            {
                _nodeDialogManager.CloseCurrentDialog();
            }
        }

        /// <summary>
        /// Áp dụng màu title hàng loạt cho node mới
        /// </summary>
        private void ApplyBulkTitleColorToNode(WorkflowNode node)
        {
            var (colorMode, colorKey) = GetBulkTitleColor();

            // Nếu là màu theo node thì không cần áp dụng gì (mặc định)
            if (colorMode == TitleColorMode.NodeColor) return;

            // Cập nhật TitleColorMode và TitleColorKey cho từng loại node
            switch (node)
            {
                case StringSplitNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case HttpRequestNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case MouseEventNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case LoopNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case InputNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case OutputNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case ListOutNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case HotkeyPressEventNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case KeyPressEventNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case AssignDataNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case MediaGalleryNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case ImageProcessingNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case VideoProcessingNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case CodeNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case FolderNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case WebNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case DataFetcherNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case FolderFilePathsNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case KeyValueBridgeNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
                case FlowOverwriteNode n:
                    n.TitleColorMode = colorMode;
                    n.TitleColorKey = colorKey;
                    break;
            }
        }

        /// <summary>
        /// Tìm Background không trong suốt từ Border hoặc các Border con
        /// </summary>
        private Brush? FindNonTransparentBackground(Border border)
        {
            // Kiểm tra Border hiện tại
            if (border.Background != null && !IsTransparent(border.Background))
            {
                return border.Background;
            }

            // Tìm trong các Border con
            return FindNonTransparentBackgroundInChildren(border);
        }

        /// <summary>
        /// Tìm Background không trong suốt trong các element con
        /// </summary>
        private Brush? FindNonTransparentBackgroundInChildren(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is Border border)
                {
                    if (border.Background != null && !IsTransparent(border.Background))
                    {
                        return border.Background;
                    }
                }

                // Đệ quy tìm trong các element con
                var found = FindNonTransparentBackgroundInChildren(child);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Kiểm tra xem Brush có trong suốt không
        /// </summary>
        private bool IsTransparent(Brush brush)
        {
            if (brush == null) return true;

            if (brush is SolidColorBrush solidBrush)
            {
                // Kiểm tra Alpha channel hoặc Opacity
                return solidBrush.Color.A == 0 || solidBrush.Opacity == 0;
            }

            // Với các loại Brush khác, kiểm tra Opacity
            return brush.Opacity == 0;
        }

        /// <summary>
        /// Lấy icon name từ nodeType
        /// </summary>
        private string GetIconNameForNodeType(string nodeType)
        {
            return nodeType switch
            {
                "Start" => "play duotone-regular",
                "End" => "flag-checkered sharp-duotone-solid",
                "Input" => "left-to-dotted-line duotone-regular",
                "Output" => "right-to-dotted-line duotone-regular",
                "Process" => "cog",
                "IfElse" => "list-tree sharp-light",
                "Loop" => "arrows-spin duotone",
                "Break" => "circle-stop duotone",
                "Continue" => "diagram-predecessor duotone-light",
                "Delay" => "timer regular",
                "Keyboard" => "keyboard duotone",
                "KeyPressEvent" => "key duotone-regular",
                "HotkeyPressEvent" => "keyboard duotone",
                "MouseEvent" => "computer-mouse duotone",
                "Variable" => "square-root-variable",
                "Function" => "calculator",
                "ScreenPosition" => "crosshairs sharp-duotone-solid",
                "ScreenCapture" => "camera-viewfinder duotone-light",
                "StringSplit" => "scissors light",
                "ListOut" => "list-radio regular",
                "AssignData" => "arrows-left-right duotone",
                "MediaGallery" => "image-stack duotone",
                "ImageProcessing" => "image notdog-duo-solid",
                "VideoProcessing" => "circle-video sharp-light",
                "Code" => "code duotone-regular",
                "HtmlUi" => "html5 brands",
                "Folder" => "folder-open duotone-thin",
                "HttpRequest" => "globe-pointer sharp-duotone-light",
                "Web" => "internet-explorer brands",
                "AsyncTask" => "diagram-project duotone-light",
                "DataFetcher" => "inbox-out duotone-light",
                "FolderFilePaths" => "file-import duotone-light",
                "KeyValueBridge" => "list-check solid",
                "FlowOverwrite" => "merge sharp-regular",
                "BodyContainer" => "border-none sharp-duotone-regular",
                "Notification" => "message-captions duotone-regular",
                "Storage" => "arrow-progress sharp-regular",
                "Callback" => "arrows-turn-right regular",
                "FileDownload" => "download solid",
                "GitSource" => "code-branch duotone-regular",
                _ => "circle2"
            };
        }

        /// <summary>
        /// Tạo ghost preview khi đang kéo
        /// </summary>
        private void CreateDragGhost(Border sourceBorder, string nodeType)
        {
            // Xóa ghost cũ nếu có
            if (_dragGhost != null)
            {
                if (WorkflowCanvas.Children.Contains(_dragGhost))
                    WorkflowCanvas.Children.Remove(_dragGhost);
                _dragGhost = null;
            }

            // Tìm Background không trong suốt (từ Border cha hoặc Border con)
            var background = FindNonTransparentBackground(sourceBorder) ?? sourceBorder.Background;

            // Tạo ghost mới dựa trên template
            _dragGhost = new Border
            {
                Width = 80,
                Height = 80,
                Background = string.Equals(_nodeAppearanceMode, "LiquidGlass", System.StringComparison.OrdinalIgnoreCase)
                    ? Services.Rendering.LiquidGlassHelper.CreateGlassBackground(
                        Services.Rendering.LiquidGlassHelper.GetColorFromBrush(background))
                    : background,
                BorderBrush = string.Equals(_nodeAppearanceMode, "LiquidGlass", System.StringComparison.OrdinalIgnoreCase)
                    ? Services.Rendering.LiquidGlassHelper.CreateGlassBorderBrush()
                    : sourceBorder.BorderBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Opacity = 0.5,
                IsHitTestVisible = false,
                Effect = sourceBorder.Effect ?? new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 4,
                    BlurRadius = 10,
                    Opacity = 0.4
                }
            };

            // Tạo Grid chứa icon
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Lấy icon name và tạo icon
            var iconName = GetIconNameForNodeType(nodeType);
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(string.Empty, typeof(Uri), iconName, System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri ?? new Uri("/Assets/Icons/circle2.svg", UriKind.RelativeOrAbsolute),
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = string.Equals(_nodeAppearanceMode, "LiquidGlass", System.StringComparison.OrdinalIgnoreCase)
                    ? Services.Rendering.LiquidGlassHelper.GetGlassIconBrush()
                    : new SolidColorBrush(Colors.White),
                IsHitTestVisible = false
            };

            grid.Children.Add(iconSvg);
            _dragGhost.Child = grid;

            // Thêm vào canvas
            WorkflowCanvas.Children.Add(_dragGhost);
            // ✅ Đặt z-index rất cao để đảm bảo ghost luôn hiển thị trên tất cả nodes
            // Z-index tiers: Body (0-499k), Connection (500k-999k), Port (1M+)
            // Ghost cần cao hơn cả node đang được kéo (có thể lên đến ~300k với DraggingOffset)
            // Đặt ở tier riêng: 2,000,000 để đảm bảo luôn ở trên cùng
            Panel.SetZIndex(_dragGhost, 2000000);
        }

        /// <summary>
        /// Xóa ghost preview
        /// </summary>
        private void RemoveDragGhost()
        {
            if (_dragGhost != null)
            {
                if (WorkflowCanvas.Children.Contains(_dragGhost))
                    WorkflowCanvas.Children.Remove(_dragGhost);
                _dragGhost = null;
            }
        }

        /// <summary>
        /// Tìm template border từ element
        /// </summary>
        private Border? FindTemplateBorder(FrameworkElement? element)
        {
            while (element != null)
            {
                if (element is Border border && border.Tag is string)
                {
                    return border;
                }
                element = element.Parent as FrameworkElement;
            }
            return null;
        }

        // Track hover state để tránh nhấp nháy
        private Border? _hoveredTemplate = null;

        /// <summary>
        /// Hover effect khi di chuột vào node template
        /// </summary>
        private void NodeTemplate_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && !_isDraggingFromTemplate && _hoveredTemplate != border)
            {
                _hoveredTemplate = border;
                // Hiệu ứng hover (scale, shadow, viền) do Style PaletteIconNodeStyle trigger IsMouseOver xử lý.
                if (border.RenderTransform == null || border.RenderTransform is not System.Windows.Media.ScaleTransform)
                {
                    var scaleTransform = new System.Windows.Media.ScaleTransform(1.1, 1.1);
                    scaleTransform.CenterX = border.ActualWidth > 0 ? border.ActualWidth / 2 : border.Width / 2;
                    scaleTransform.CenterY = border.ActualHeight > 0 ? border.ActualHeight / 2 : border.Height / 2;
                    border.RenderTransform = scaleTransform;
                }
            }
        }

        /// <summary>
        /// Hover effect khi rời chuột khỏi node template
        /// </summary>
        private void NodeTemplate_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border && !_isDraggingFromTemplate && _hoveredTemplate == border)
            {
                _hoveredTemplate = null;
                // Không set Effect = null hay RenderTransform = null ở đây.
                // Các giá trị local đó override Style và làm mất viền trắng + shadow.
                // Để Style trigger IsMouseOver=False tự khôi phục Effect, RenderTransform, BorderBrush.

                if (border.RenderTransform != null)
                {
                    border.RenderTransform = null;
                }
            }
        }

        /// <summary>
        /// Filter nodes based on search text
        /// </summary>
        private void NodeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NodeTemplatesPanel == null || sender is not TextBox searchBox) return;

            string searchText = searchBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;
            bool hasSearchText = !string.IsNullOrEmpty(searchText);

            // Find all node template borders recursively
            var allBorders = FindVisualChildren<Border>(NodeTemplatesPanel)
                .Where(b => b.Tag is string)
                .ToList();

            foreach (var border in allBorders)
            {
                if (border.Tag is string nodeType)
                {
                    // Get node name from Tag
                    string nodeName = nodeType;
                    string tooltipText = string.Empty;

                    // Try to get tooltip text for better search
                    if (border.ToolTip is ToolTip tooltip)
                    {
                        if (tooltip.Content is StackPanel tooltipPanel)
                        {
                            var textBlocks = tooltipPanel.Children.OfType<TextBlock>().ToList();
                            if (textBlocks.Count > 0)
                            {
                                tooltipText = textBlocks[0].Text?.ToLowerInvariant() ?? string.Empty;
                            }
                        }
                    }

                    // Check if matches search (search in node type name or tooltip)
                    bool matches = !hasSearchText || 
                                   nodeName.ToLowerInvariant().Contains(searchText) ||
                                   (!string.IsNullOrEmpty(tooltipText) && tooltipText.Contains(searchText));

                    border.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            // Update group headers visibility based on visible nodes
            UpdateGroupHeadersVisibility();
        }

        /// <summary>
        /// Helper method to find visual children of a specific type
        /// </summary>
        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        /// <summary>
        /// Update group headers visibility based on visible nodes
        /// </summary>
        private void UpdateGroupHeadersVisibility()
        {
            if (NodeTemplatesPanel == null) return;

            var children = NodeTemplatesPanel.Children.OfType<UIElement>().ToList();
            
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                
                if (child is TextBlock textBlock && 
                    textBlock.Style == (Style)NodeTemplatesPanel.FindResource("PaletteGroupHeaderStyle"))
                {
                    // Check if next sibling is UniformGrid and has visible children
                    bool hasVisibleChildren = false;
                    
                    if (i + 1 < children.Count && children[i + 1] is UniformGrid uniformGrid)
                    {
                        hasVisibleChildren = uniformGrid.Children.OfType<Border>()
                            .Any(b => b.Visibility == Visibility.Visible);
                    }

                    textBlock.Visibility = hasVisibleChildren ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (child is UniformGrid uniformGrid)
                {
                    bool hasVisibleChildren = uniformGrid.Children.OfType<Border>()
                        .Any(b => b.Visibility == Visibility.Visible);
                    
                    uniformGrid.Visibility = hasVisibleChildren ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        #endregion
    }
}

