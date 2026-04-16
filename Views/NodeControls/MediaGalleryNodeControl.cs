using FlowMy;
using FlowMy.Behaviors;
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Data;
using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FlowMy.Views.NodeControls
{
    public static class MediaGalleryNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50;
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();
        
        // Cache cho BitmapImage để tránh tải lại nhiều lần
        private static readonly System.Collections.Generic.Dictionary<string, BitmapImage> _imageCache = new();
        private static readonly object _cacheLock = new object();

        public static Border CreateBorder(MediaGalleryNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var border = new Border
            {
                Width = node.Width,
                Height = node.Height,
                MinWidth = 200,
                MinHeight = 180,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Direction = 270, ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node,
                // Tránh ghosting: không dùng BitmapCache khi có GPU
                CacheMode = null
            };
            
            // Áp dụng GPU optimization cho border (tự động kiểm tra GPU và chỉ áp dụng khi có GPU)
            GpuOptimizationHelper.ApplyToBorder(border);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Áp dụng GPU optimization cho grid (tự động kiểm tra GPU)
            GpuOptimizationHelper.ApplyToElement(grid);

            // Top bar: lớp nền nhận hit-test để kéo node + CheckBox "Chọn tất cả" (chỉ chiếm bên trái)
            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(8, 4, 8, 4)
            };
            var topBarGrid = new Grid();
            var topBarDragLayer = new Border
            {
                Background = Brushes.Transparent,
                IsHitTestVisible = true
            };
            var selectAllCheck = new CheckBox
            {
                Content = "Chọn tất cả",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = node
            };
            selectAllCheck.Checked += (s, e) =>
            {
                foreach (var it in node.GalleryItems) it.IsSelected = true;
                foreach (var g in node.GalleryGroups)
                    foreach (var it in g.Items) it.IsSelected = true;
            };
            selectAllCheck.Unchecked += (s, e) =>
            {
                foreach (var it in node.GalleryItems) it.IsSelected = false;
                foreach (var g in node.GalleryGroups)
                    foreach (var it in g.Items) it.IsSelected = false;
            };
            topBarGrid.Children.Add(topBarDragLayer);
            topBarGrid.Children.Add(selectAllCheck);
            topBar.Child = topBarGrid;
            Grid.SetRow(topBar, 0);
            grid.Children.Add(topBar);

            // Scroll area: placeholder + lưới (Grid) hoặc nhóm (Grouped)
            var placeholder = new TextBlock
            {
                Text = "Kết nối node đưa data (JSON) rồi chạy workflow để hiển thị danh sách ảnh/video.",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8),
                Visibility = Visibility.Collapsed
            };
            var itemsControl = new ItemsControl
            {
                DataContext = node,
                Tag = node,
                ItemsSource = node.GalleryItems,
                ItemTemplate = CreateGalleryItemTemplate(node),
                Padding = new Thickness(4)
            };
            var wrapPanelFactory = new FrameworkElementFactory(typeof(WrapPanel));
            wrapPanelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            itemsControl.ItemsPanel = new ItemsPanelTemplate(wrapPanelFactory);

            var groupsControl = new ItemsControl
            {
                DataContext = node,
                Tag = node,
                ItemsSource = node.GalleryGroups,
                ItemTemplate = CreateGroupTemplate(node),
                Padding = new Thickness(4)
            };

            var galleryContent = new Grid();
            galleryContent.Children.Add(placeholder);
            galleryContent.Children.Add(itemsControl);
            galleryContent.Children.Add(groupsControl);
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(6),
                Content = galleryContent,
                CacheMode = null // Tránh ghosting
            };
            
            // Áp dụng GPU optimization cho ScrollViewer
            if (GpuDetectionHelper.IsGpuAvailable)
            {
                RenderOptions.SetBitmapScalingMode(scroll, BitmapScalingMode.Unspecified);
                RenderOptions.SetCachingHint(scroll, CachingHint.Unspecified);
            }
            void UpdatePlaceholderAndMode()
            {
                var isGrid = node.DisplayMode == GalleryDisplayMode.Grid;
                var hasGrid = node.GalleryItems.Count > 0;
                var hasGroups = node.GalleryGroups.Count > 0;
                var hasContent = isGrid ? hasGrid : hasGroups;
                placeholder.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
                itemsControl.Visibility = isGrid && hasGrid ? Visibility.Visible : Visibility.Collapsed;
                groupsControl.Visibility = !isGrid && hasGroups ? Visibility.Visible : Visibility.Collapsed;
            }
            node.GalleryItems.CollectionChanged += (s, e) => UpdatePlaceholderAndMode();
            node.GalleryGroups.CollectionChanged += (s, e) => UpdatePlaceholderAndMode();
            node.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MediaGalleryNode.DisplayMode) && !string.IsNullOrWhiteSpace(node.LastJson))
                {
                    MediaGalleryJsonHelper.ParseAndFill(node.LastJson, node);
                }
            };
            UpdatePlaceholderAndMode();
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            // Bottom bar: icon buttons (Tải ảnh, Tải video, Xem video) — folder từ textbox hoặc node+key
            var btnTaiAnh = CreateIconButton("📷 Tải ảnh");
            var btnTaiVideo = CreateIconButton("🎬 Tải video");
            btnTaiAnh.Click += (s, e) => DownloadSelectedMedia(node, host, isImage: true);
            btnTaiVideo.Click += (s, e) => DownloadSelectedMedia(node, host, isImage: false);
            var bottomBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        btnTaiAnh,
                        btnTaiVideo,
                        //CreateIconButton("▶ Xem video")
                    }
                }
            };
            Grid.SetRow(bottomBar, 2);
            grid.Children.Add(bottomBar);

            // Lớp phủ chứa 8 handle resize (full grid) giống LoopContainerControl
            var handleOverlay = new Grid();
            // AddResizeHandle(handleOverlay, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(2, 2, 0, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            // AddResizeHandle(handleOverlay, ResizeDirection.Left, HorizontalAlignment.Left, VerticalAlignment.Center, new Thickness(2, 0, 0, 0));
            // AddResizeHandle(handleOverlay, ResizeDirection.Right, HorizontalAlignment.Right, VerticalAlignment.Center, new Thickness(0, 0, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 2, 0, 0));
            // AddResizeHandle(handleOverlay, ResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(0, 0, 0, 2));

            var outerGrid = new Grid();
            outerGrid.Children.Add(grid);
            outerGrid.Children.Add(handleOverlay);
            
            // Áp dụng GPU optimization cho outerGrid
            GpuOptimizationHelper.ApplyToElement(outerGrid);
            
            border.Child = outerGrid;

            bool isResizing = false;
            ResizeDirection currentDir = ResizeDirection.None;
            Point resizeStart = default;
            double origW = 0, origH = 0, origX = 0, origY = 0;

            // Chỉ xử lý resize khi click vào handle (Ellipse); click chỗ khác → event bubble → host NodeMouseDown → kéo node
            border.PreviewMouseDown += (s, e) =>
            {
                if (e.OriginalSource is Ellipse handle && handle.Tag is ResizeDirection dir)
                {
                    isResizing = true;
                    currentDir = dir;
                    resizeStart = e.GetPosition(border.Parent as UIElement);
                    origW = node.Width;
                    origH = node.Height;
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
                switch (currentDir)
                {
                    case ResizeDirection.BottomRight:
                        newW = Math.Max(200, origW + dx);
                        newH = Math.Max(180, origH + dy);
                        break;
                    case ResizeDirection.TopLeft:
                        newW = Math.Max(200, origW - dx);
                        newH = Math.Max(180, origH - dy);
                        newX = origX + (origW - newW);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.TopRight:
                        newW = Math.Max(200, origW + dx);
                        newH = Math.Max(180, origH - dy);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.BottomLeft:
                        newW = Math.Max(200, origW - dx);
                        newH = Math.Max(180, origH + dy);
                        newX = origX + (origW - newW);
                        break;
                    case ResizeDirection.Right:
                        newW = Math.Max(200, origW + dx);
                        break;
                    case ResizeDirection.Left:
                        newW = Math.Max(200, origW - dx);
                        newX = origX + (origW - newW);
                        break;
                    case ResizeDirection.Bottom:
                        newH = Math.Max(180, origH + dy);
                        break;
                    case ResizeDirection.Top:
                        newH = Math.Max(180, origH - dy);
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
                if (isResizing) { isResizing = false; border.ReleaseMouseCapture(); e.Handled = true; }
            };

            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Gallery ảnh/video",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTitleBrush(node),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            border.Focusable = true;
            border.FocusVisualStyle = null;

            bool isHovering = false;
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = node.Title ?? "Gallery ảnh/video";
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitlePosition(titleTextBlock, border, host);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(MediaGalleryNode.TitleDisplayMode))
                    {
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    }
                    else if (e.PropertyName == nameof(MediaGalleryNode.TitleColorMode) || e.PropertyName == nameof(MediaGalleryNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(MediaGalleryNode.Width) || e.PropertyName == nameof(MediaGalleryNode.Height))
                    {
                        if (s == node && !isResizing)
                        {
                            border.Width = node.Width;
                            border.Height = node.Height;
                        }

                        var baseline = border.MinHeight > 0 ? border.MinHeight : 180.0;
                        var rawScale = baseline > 0 ? node.Height / baseline : 1.0;
                        UpdateInteractionVisualScale(handleOverlay, node, rawScale);
                    }
                    else if (e.PropertyName == nameof(MediaGalleryNode.FrameDisplayWidth) || e.PropertyName == nameof(MediaGalleryNode.FrameDisplayHeight))
                    {
                        itemsControl.ItemTemplate = CreateGalleryItemTemplate(node);
                        groupsControl.ItemTemplate = CreateGroupTemplate(node);
                    }
                    else if (e.PropertyName == nameof(MediaGalleryNode.DisplayMode))
                    {
                        UpdatePlaceholderAndMode();
                    }
                };
            }

            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (isHovering) border.Focus(); }));
            };
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            };

            // Keyboard Port Position: Arrow = Port IN, Shift+Arrow = Port OUT
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

            var visibilityDescriptor = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                    titleTextBlock.Visibility = Visibility.Collapsed;
                else
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            });

            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }

                var loadedBaseline = border.MinHeight > 0 ? border.MinHeight : 180.0;
                var loadedScale = loadedBaseline > 0 ? node.Height / loadedBaseline : 1.0;
                UpdateInteractionVisualScale(handleOverlay, node, loadedScale);
            };
            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);
            border.Unloaded += (s, e) =>
            {
                try
                {
                    if (_titleUpdateTimers.TryGetValue(border, out var t)) { t.Stop(); _titleUpdateTimers.Remove(border); }
                    _titleUpdatedAfterZoom.Remove(border);
                    if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
                        host.WorkflowCanvas.Children.Remove(titleTextBlock);
                    if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                        node.TitleTextBlockUI = null;
                }
                catch { }
            };

            border.LayoutUpdated += (s, e) =>
            {
                if (border.Visibility != Visibility.Visible) { titleTextBlock.Visibility = Visibility.Collapsed; return; }
                if (NodeChrome.IsZooming) { titleTextBlock.Visibility = Visibility.Collapsed; _titleUpdatedAfterZoom[border] = false; return; }
                if (!_titleUpdatedAfterZoom.TryGetValue(border, out var up) || !up)
                {
                    _titleUpdatedAfterZoom[border] = true;
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    if (titleTextBlock.Visibility == Visibility.Visible)
                        UpdateTitlePosition(titleTextBlock, border, host);
                }
                if (host.IsPanning || host.DraggedNode == node) return;
                if (titleTextBlock.Visibility == Visibility.Visible)
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
            };

            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            return border;
        }

        private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = direction,
                Cursor = GetCursorForResizeDirection(direction),
                CacheMode = null // Tránh ghosting
            };
            
            // Áp dụng GPU optimization cho resize handle
            GpuOptimizationHelper.ApplyToShape(handle);
            
            grid.Children.Add(handle);
        }

        private static void UpdateInteractionVisualScale(Grid handleOverlay, WorkflowNode node, double rawScale)
        {
            var visualScale = Math.Max(1.0, Math.Min(2.8, rawScale * 1.2));

            if (handleOverlay != null)
            {
                foreach (var child in handleOverlay.Children)
                {
                    if (child is Ellipse handle && handle.Tag is ResizeDirection)
                    {
                        handle.RenderTransformOrigin = new Point(0.5, 0.5);
                        handle.RenderTransform = new ScaleTransform(visualScale, visualScale);
                    }
                }
            }

            if (node?.Ports != null)
            {
                foreach (var p in node.Ports)
                {
                    if (p?.PortUI is FrameworkElement portUi)
                    {
                        portUi.RenderTransformOrigin = new Point(0.5, 0.5);
                        portUi.RenderTransform = new ScaleTransform(visualScale, visualScale);
                    }
                }
            }
        }

        private static Cursor GetCursorForResizeDirection(ResizeDirection direction)
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

        private static DataTemplate CreateGroupTemplate(MediaGalleryNode node)
        {
            var groupBorder = new FrameworkElementFactory(typeof(Border));
            groupBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x28, 0, 0, 0)));
            groupBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x60, 255, 255, 255)));
            groupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            groupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            groupBorder.SetValue(Border.MarginProperty, new Thickness(4));
            groupBorder.SetValue(Border.PaddingProperty, new Thickness(8));
            var groupStack = new FrameworkElementFactory(typeof(StackPanel));
            groupStack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            var header = new FrameworkElementFactory(typeof(TextBlock));
            header.SetBinding(TextBlock.TextProperty, new Binding("Title"));
            header.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.White));
            header.SetValue(TextBlock.FontSizeProperty, 12.0);
            header.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            header.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 6));
            groupStack.AppendChild(header);
            var innerItems = new FrameworkElementFactory(typeof(ItemsControl));
            innerItems.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("Items"));
            innerItems.SetValue(FrameworkElement.TagProperty, node); // Tag = node để item template lấy FrameDisplayWidth/Height (tránh FindAncestor lỗi)
            innerItems.SetValue(ItemsControl.ItemTemplateProperty, CreateGalleryItemTemplate(node));
            innerItems.SetValue(ItemsControl.PaddingProperty, new Thickness(0));
            var wrapFactory = new FrameworkElementFactory(typeof(WrapPanel));
            wrapFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            innerItems.SetValue(ItemsControl.ItemsPanelProperty, new ItemsPanelTemplate(wrapFactory));
            groupStack.AppendChild(innerItems);
            groupBorder.AppendChild(groupStack);
            return new DataTemplate(typeof(MediaGalleryGroup)) { VisualTree = groupBorder };
        }

        private static DataTemplate CreateGalleryItemTemplate(MediaGalleryNode node)
        {
            const double cellW = 60;
            const double cellH = 40;
            var fw = Math.Max(cellW, node.FrameDisplayWidth);
            var fh = Math.Max(cellH, node.FrameDisplayHeight);

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetValue(Border.MarginProperty, new Thickness(4));
            border.SetBinding(Border.WidthProperty, new Binding("Tag.FrameDisplayWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ItemsControl), 1),
                Converter = new Converters.FrameSizeClampConverter(),
                ConverterParameter = cellW
            });
            border.SetBinding(Border.HeightProperty, new Binding("Tag.FrameDisplayHeight")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ItemsControl), 1),
                Converter = new Converters.FrameSizeClampConverter(),
                ConverterParameter = cellH
            });
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)));

            var stack = new FrameworkElementFactory(typeof(StackPanel));
            stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);

            var topRow = new FrameworkElementFactory(typeof(StackPanel));
            topRow.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            topRow.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 2, 2, 0));
            var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay });
            check.SetValue(CheckBox.ForegroundProperty, new SolidColorBrush(Colors.White));
            check.SetValue(CheckBox.FontSizeProperty, 10.0);
            check.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
            topRow.AppendChild(check);
            var title = new FrameworkElementFactory(typeof(TextBlock));
            title.SetBinding(TextBlock.TextProperty, new Binding("Title"));
            title.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.White));
            title.SetValue(TextBlock.FontSizeProperty, 10.0);
            title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            title.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            title.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
            title.SetBinding(TextBlock.MaxWidthProperty, new Binding("Tag.FrameDisplayWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ItemsControl), 1),
                Converter = new Converters.FrameWidthToTitleMaxConverter()
            });
            topRow.AppendChild(title);
            stack.AppendChild(topRow);

            var imgArea = new FrameworkElementFactory(typeof(Border));
            imgArea.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)));
            imgArea.SetBinding(Border.HeightProperty, new Binding("Tag.FrameDisplayHeight")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ItemsControl), 1),
                Converter = new Converters.FrameHeightToImageAreaConverter()
            });
            imgArea.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 4));
            imgArea.SetValue(Border.CursorProperty, Cursors.Hand);
            imgArea.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is Border b && b.DataContext is MediaGalleryItem item)
                {
                    e.Handled = true;
                    if (node.ItemClickPreviewMode == ItemClickPreviewMode.Video)
                    {
                        if (!string.IsNullOrEmpty(item.VideoUrl)) ShowVideoPopup(item.VideoUrl, item.Title);
                        else if (!string.IsNullOrEmpty(item.ImageUrl)) ShowImageZoomPopup(item.ImageUrl, item.Title);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(item.ImageUrl)) ShowImageZoomPopup(item.ImageUrl, item.Title);
                        else if (!string.IsNullOrEmpty(item.VideoUrl)) ShowVideoPopup(item.VideoUrl, item.Title);
                    }
                }
            }), true);

            var img = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            img.SetBinding(AsyncUrlImageBehavior.UrlProperty, new Binding("ImageUrl"));
            img.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.Uniform);
            img.SetValue(System.Windows.Controls.Image.StretchDirectionProperty, StretchDirection.Both);
            // Luôn dùng chất lượng cao nhất khi scale thumbnail
            img.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            img.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            img.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            imgArea.AppendChild(img);
            stack.AppendChild(imgArea);

            var iconOnlyTemplate = CreateIconOnlyButtonTemplate();
            var iconColorImage = Color.FromRgb(0x93, 0xC5, 0xFD); // xanh nhạt cho icon ảnh
            var btnImage = new FrameworkElementFactory(typeof(Button));
            var iconImage = IconResources.GetSvgImage("image regular", iconColorImage);
            if (iconImage != null)
            {
                var gridImage = new FrameworkElementFactory(typeof(Grid));
                var viewboxImage = new FrameworkElementFactory(typeof(Viewbox));
                viewboxImage.SetValue(FrameworkElement.WidthProperty, 18.0);
                viewboxImage.SetValue(FrameworkElement.HeightProperty, 18.0);
                viewboxImage.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                viewboxImage.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                viewboxImage.SetValue(Viewbox.StretchProperty, Stretch.Uniform);
                var imgContentImage = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
                imgContentImage.SetValue(System.Windows.Controls.Image.SourceProperty, iconImage);
                imgContentImage.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.Uniform);
                viewboxImage.AppendChild(imgContentImage);
                gridImage.AppendChild(viewboxImage);
                btnImage.SetValue(Button.ContentProperty, " ");
                btnImage.SetValue(Button.ContentTemplateProperty, new DataTemplate { VisualTree = gridImage });
            }
            else
            {
                btnImage.SetValue(Button.ContentProperty, "📷");
            }
            btnImage.SetValue(Button.TemplateProperty, iconOnlyTemplate);
            btnImage.SetValue(Button.WidthProperty, 18.0);
            btnImage.SetValue(Button.HeightProperty, 18.0);
            btnImage.SetValue(Button.MarginProperty, new Thickness(0, 0, 4, 0));
            btnImage.SetValue(Button.BackgroundProperty, Brushes.Transparent);
            btnImage.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            btnImage.SetValue(Button.BorderBrushProperty, Brushes.Transparent);
            btnImage.SetValue(Button.CursorProperty, Cursors.Hand);
            btnImage.SetValue(Button.ToolTipProperty, "Xem ảnh");
            btnImage.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                if (s is Button b && b.DataContext is MediaGalleryItem item && !string.IsNullOrEmpty(item.ImageUrl))
                {
                    e.Handled = true;
                    ShowImageZoomPopup(item.ImageUrl, item.Title);
                }
            }));
            var iconColorVideo = Color.FromRgb(0x86, 0xEF, 0xAC); // xanh lá nhạt cho icon video
            var btnVideo = new FrameworkElementFactory(typeof(Button));
            var iconVideo = IconResources.GetSvgImage("circle-video sharp-light", iconColorVideo);
            if (iconVideo != null)
            {
                var gridVideo = new FrameworkElementFactory(typeof(Grid));
                var viewboxVideo = new FrameworkElementFactory(typeof(Viewbox));
                viewboxVideo.SetValue(FrameworkElement.WidthProperty, 18.0);
                viewboxVideo.SetValue(FrameworkElement.HeightProperty, 18.0);
                viewboxVideo.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                viewboxVideo.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                viewboxVideo.SetValue(Viewbox.StretchProperty, Stretch.Uniform);
                var imgContentVideo = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
                imgContentVideo.SetValue(System.Windows.Controls.Image.SourceProperty, iconVideo);
                imgContentVideo.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.Uniform);
                viewboxVideo.AppendChild(imgContentVideo);
                gridVideo.AppendChild(viewboxVideo);
                btnVideo.SetValue(Button.ContentProperty, " ");
                btnVideo.SetValue(Button.ContentTemplateProperty, new DataTemplate { VisualTree = gridVideo });
            }
            else
            {
                btnVideo.SetValue(Button.ContentProperty, "📽️");
            }
            btnVideo.SetValue(Button.TemplateProperty, iconOnlyTemplate);
            btnVideo.SetValue(Button.WidthProperty, 18.0);
            btnVideo.SetValue(Button.HeightProperty, 18.0);
            btnVideo.SetValue(Button.MarginProperty, new Thickness(0));
            btnVideo.SetValue(Button.BackgroundProperty, Brushes.Transparent);
            btnVideo.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            btnVideo.SetValue(Button.BorderBrushProperty, Brushes.Transparent);
            btnVideo.SetValue(Button.CursorProperty, Cursors.Hand);
            btnVideo.SetValue(Button.ToolTipProperty, "Xem video");
            btnVideo.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                if (s is Button b && b.DataContext is MediaGalleryItem item && !string.IsNullOrEmpty(item.VideoUrl))
                {
                    e.Handled = true;
                    ShowVideoPopup(item.VideoUrl, item.Title);
                }
            }));
            var bottomRow = new FrameworkElementFactory(typeof(StackPanel));
            bottomRow.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            bottomRow.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            bottomRow.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 4, 4, 4));
            bottomRow.AppendChild(btnImage);
            bottomRow.AppendChild(btnVideo);
            stack.AppendChild(bottomRow);

            border.AppendChild(stack);
            var dt = new DataTemplate(typeof(MediaGalleryItem)) { VisualTree = border };
            return dt;
        }

        /// <summary>
        /// Lấy BitmapImage từ cache hoặc tạo mới và cache
        /// Public để AsyncUrlImageBehavior có thể sử dụng
        /// </summary>
        public static BitmapImage GetOrCreateCachedBitmap(string imageUrl)
        {
            lock (_cacheLock)
            {
                if (_imageCache.TryGetValue(imageUrl, out var cached))
                {
                    return cached;
                }
                
                var kind = imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                           imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? UriKind.Absolute : UriKind.RelativeOrAbsolute;
                var uri = new Uri(imageUrl, kind);
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                // Không set DecodePixelWidth để có chất lượng cao nhất
                bitmap.EndInit();
                
                // Cache bitmap
                _imageCache[imageUrl] = bitmap;
                return bitmap;
            }
        }
        
        private static void ShowImageZoomPopup(string imageUrl, string title)
        {
            var w = new Window
            {
                Title = string.IsNullOrEmpty(title) ? "Ảnh" : title,
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))
            };
            var img = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            
            // Loading indicator
            var loadingText = new TextBlock
            {
                Text = "Đang tải ảnh...",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.7
            };
            
            var loadingGrid = new Grid();
            loadingGrid.Children.Add(img);
            loadingGrid.Children.Add(loadingText);
            
            var scrollViewer = new ScrollViewer
            {
                Content = loadingGrid,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8),
                CacheMode = null // Tránh ghosting
            };
            
            BitmapImage? bitmap = null;
            var scale = new ScaleTransform(1.0, 1.0);
            img.LayoutTransform = scale;
            
            try
            {
                // Lấy bitmap từ cache hoặc tạo mới
                bitmap = GetOrCreateCachedBitmap(imageUrl);
                
                // Đợi ảnh load xong (đặc biệt quan trọng với URL ảnh)
                if (bitmap.IsDownloading)
                {
                    EventHandler? downloadCompleted = null;
                    EventHandler<System.Windows.Media.ExceptionEventArgs>? downloadFailed = null;
                    
                    downloadCompleted = (s, e) =>
                    {
                        bitmap.DownloadCompleted -= downloadCompleted;
                        if (downloadFailed != null)
                            bitmap.DownloadFailed -= downloadFailed;
                        SetupImageScale();
                    };
                    
                    downloadFailed = (s, e) =>
                    {
                        bitmap.DownloadCompleted -= downloadCompleted;
                        bitmap.DownloadFailed -= downloadFailed;
                        loadingText.Text = "Không tải được ảnh.";
                    };
                    
                    bitmap.DownloadCompleted += downloadCompleted;
                    bitmap.DownloadFailed += downloadFailed;
                }
                else
                {
                    // Ảnh đã load xong (local file hoặc cached), setup ngay
                    loadingText.Visibility = Visibility.Collapsed; // Ẩn loading ngay nếu đã cache
                    SetupImageScale();
                }
                
                img.Source = bitmap;
            }
            catch
            {
                img.Source = null;
                loadingText.Text = "Không tải được ảnh.";
            }
            
            void SetupImageScale()
            {
                if (bitmap == null || bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0) return;
                
                // Tính scale ban đầu và set size cho image
                double initialScale = 1.0;
                double imageWidth = bitmap.PixelWidth;
                double imageHeight = bitmap.PixelHeight;
                
                // Lấy kích thước window (trừ padding)
                double availableWidth = w.Width - 40;
                double availableHeight = w.Height - 60;
                
                // Tính tỉ lệ để fit
                double scaleX = availableWidth / bitmap.PixelWidth;
                double scaleY = availableHeight / bitmap.PixelHeight;
                initialScale = Math.Min(scaleX, scaleY);
                
                // Không scale quá nhỏ/to ban đầu
                if (initialScale < 0.1) initialScale = 0.1;
                if (initialScale > 1.0) initialScale = 1.0;
                
                // Set explicit size cho image để ScrollViewer tính đúng extent
                img.Dispatcher.BeginInvoke(new Action(() =>
                {
                    img.Width = imageWidth;
                    img.Height = imageHeight;
                    scale.ScaleX = initialScale;
                    scale.ScaleY = initialScale;
                    
                    // Ẩn loading text
                    loadingText.Visibility = Visibility.Collapsed;
                    
                    // Force update layout
                    scrollViewer.UpdateLayout();
                    
                    // Center ảnh
                    if (scrollViewer.ExtentWidth > scrollViewer.ViewportWidth)
                    {
                        scrollViewer.ScrollToHorizontalOffset((scrollViewer.ExtentWidth - scrollViewer.ViewportWidth) / 2);
                    }
                    if (scrollViewer.ExtentHeight > scrollViewer.ViewportHeight)
                    {
                        scrollViewer.ScrollToVerticalOffset((scrollViewer.ExtentHeight - scrollViewer.ViewportHeight) / 2);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            
            // Áp dụng GPU optimization cho ScrollViewer
            if (GpuDetectionHelper.IsGpuAvailable)
            {
                RenderOptions.SetBitmapScalingMode(scrollViewer, BitmapScalingMode.HighQuality);
                RenderOptions.SetCachingHint(scrollViewer, CachingHint.Unspecified);
            }
            
            // Tối ưu Image cho GPU + chất lượng cao
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            if (GpuDetectionHelper.IsGpuAvailable)
            {
                RenderOptions.SetCachingHint(img, CachingHint.Unspecified);
                img.CacheMode = null; // Tránh ghosting
            }

            // Hỗ trợ pan/zoom giống canvas
            scrollViewer.Focusable = true;

            scrollViewer.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;

                var position = e.GetPosition(scrollViewer);
                var oldScale = scale.ScaleX;
                double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
                var newScale = oldScale * zoomFactor;
                // Giới hạn kỹ thuật, nhưng đủ để coi như không giới hạn với người dùng
                if (newScale < 0.01) newScale = 0.01;
                if (newScale > 100.0) newScale = 100.0;
                if (Math.Abs(newScale - oldScale) < 0.0001) return;

                // Tính tỉ lệ trước/sau zoom để giữ nguyên điểm dưới con trỏ chuột
                var extentWidth = scrollViewer.ExtentWidth;
                var extentHeight = scrollViewer.ExtentHeight;
                double relativeX = 0.5;
                double relativeY = 0.5;
                if (extentWidth > 0 && extentHeight > 0)
                {
                    relativeX = (scrollViewer.HorizontalOffset + position.X) / extentWidth;
                    relativeY = (scrollViewer.VerticalOffset + position.Y) / extentHeight;
                }

                scale.ScaleX = newScale;
                scale.ScaleY = newScale;

                scrollViewer.UpdateLayout();

                extentWidth = scrollViewer.ExtentWidth;
                extentHeight = scrollViewer.ExtentHeight;
                if (extentWidth > 0 && extentHeight > 0)
                {
                    var targetX = relativeX * extentWidth - position.X;
                    var targetY = relativeY * extentHeight - position.Y;
                    scrollViewer.ScrollToHorizontalOffset(Math.Max(0, Math.Min(targetX, extentWidth)));
                    scrollViewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(targetY, extentHeight)));
                }
            };

            bool isPanning = false;
            Point panStart;
            double panOriginX = 0, panOriginY = 0;

            scrollViewer.PreviewMouseLeftButtonDown += (s, e) =>
            {
                isPanning = true;
                panStart = e.GetPosition(scrollViewer);
                panOriginX = scrollViewer.HorizontalOffset;
                panOriginY = scrollViewer.VerticalOffset;
                scrollViewer.Cursor = Cursors.SizeAll;
                scrollViewer.CaptureMouse();
                e.Handled = true;
            };

            scrollViewer.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (!isPanning) return;
                isPanning = false;
                scrollViewer.Cursor = Cursors.Arrow;
                scrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            };

            scrollViewer.PreviewMouseMove += (s, e) =>
            {
                if (!isPanning) return;
                var pos = e.GetPosition(scrollViewer);
                var dx = pos.X - panStart.X;
                var dy = pos.Y - panStart.Y;
                scrollViewer.ScrollToHorizontalOffset(panOriginX - dx);
                scrollViewer.ScrollToVerticalOffset(panOriginY - dy);
                e.Handled = true;
            };
            
            w.Content = scrollViewer;
            w.Show();
        }

        private static void ShowVideoPopup(string videoUrl, string title)
        {
            if (string.IsNullOrWhiteSpace(videoUrl)) return;
            videoUrl = videoUrl.Trim();
            var w = new Window
            {
                Title = string.IsNullOrEmpty(title) ? "Video" : title,
                Width = 800,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))
            };
            void OpenUrlInBrowser()
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = videoUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không mở được: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            var bottomBarBtn = new Button
            {
                Content = "Mở trong trình duyệt",
                Margin = new Thickness(0),
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            bottomBarBtn.Click += (_, _) => OpenUrlInBrowser();
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Áp dụng GPU optimization cho mainGrid
            GpuOptimizationHelper.ApplyToElement(mainGrid);
            
            var webView = new WebView2();
            Grid.SetRow(webView, 0);
            
            // Tối ưu WebView2 cho GPU: disable software rendering, enable hardware acceleration
            if (GpuDetectionHelper.IsGpuAvailable)
            {
                // Áp dụng GPU-friendly render options cho WebView2 container
                RenderOptions.SetBitmapScalingMode(webView, BitmapScalingMode.Unspecified);
                RenderOptions.SetCachingHint(webView, CachingHint.Unspecified);
                webView.CacheMode = null; // Tránh ghosting
            }
            
            mainGrid.Children.Add(webView);
            var bottomBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)),
                Padding = new Thickness(8),
                Child = bottomBarBtn
            };
            Grid.SetRow(bottomBar, 1);
            mainGrid.Children.Add(bottomBar);
            w.Content = mainGrid;
            w.Closing += (_, _) =>
            {
                try
                {
                    webView.CoreWebView2?.Navigate("about:blank");
                }
                catch { /* ignore */ }
            };
            w.Loaded += async (_, _) =>
            {
                try
                {
                    // Cấu hình WebView2 với GPU acceleration
                    CoreWebView2EnvironmentOptions? options = null;
                    
                    if (GpuDetectionHelper.IsGpuAvailable)
                    {
                        // Bật GPU acceleration và tối ưu render
                        options = new CoreWebView2EnvironmentOptions();
                        var gpuArgs = new StringBuilder();
                        gpuArgs.Append("--enable-gpu-rasterization ");
                        gpuArgs.Append("--enable-zero-copy ");
                        gpuArgs.Append("--enable-features=VaapiVideoDecoder ");
                        gpuArgs.Append("--ignore-gpu-blacklist ");
                        gpuArgs.Append("--enable-accelerated-2d-canvas ");
                        gpuArgs.Append("--enable-accelerated-video-decode ");
                        
                        options.AdditionalBrowserArguments = gpuArgs.ToString();
                    }
                    
                    if (options != null)
                    {
                        var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                        await webView.EnsureCoreWebView2Async(env);
                    }
                    else
                    {
                        await webView.EnsureCoreWebView2Async(null);
                    }
                    
                    var escapedUrl = WebUtility.HtmlEncode(videoUrl);
                    var html = $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><style>html,body{{margin:0;padding:0;width:100%;height:100%;overflow:hidden;background:#1e293b;box-sizing:border-box}}*{{box-sizing:inherit}}body{{display:flex;align-items:center;justify-content:center}}video{{max-width:100%;max-height:100%;width:100%;height:100%;object-fit:contain;display:block}}</style></head><body><video src=\"{escapedUrl}\" controls autoplay playsinline></video></body></html>";
                    webView.NavigateToString(html);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Không thể phát video trong app (thiếu WebView2 Runtime?). Dùng nút \"Mở trong trình duyệt\" bên dưới.\n\nChi tiết: " + ex.Message,
                        "Video",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            };
            w.Show();
        }

        /// <summary>Template nút chỉ hiển thị nội dung (icon), không viền không nền.</summary>
        private static ControlTemplate CreateIconOnlyButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetBinding(ContentPresenter.ContentProperty, new Binding(Button.ContentProperty.Name) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            cp.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding(Button.ContentTemplateProperty.Name) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            template.VisualTree = cp;
            return template;
        }

        private static ControlTemplate CreateRoundButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(999));
            border.SetValue(Border.PaddingProperty, new Thickness(2, 2, 2, 2));
            border.SetBinding(Border.BackgroundProperty, new Binding(Button.BackgroundProperty.Name) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new Binding(Button.BorderThicknessProperty.Name) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new Binding(Button.BorderBrushProperty.Name) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetBinding(ContentPresenter.ContentProperty, new Binding(Button.ContentProperty.Name) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            cp.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding(Button.ContentTemplateProperty.Name) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.AppendChild(cp);
            template.VisualTree = border;
            return template;
        }

        private static readonly HttpClient _downloadClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

        private static IEnumerable<MediaGalleryItem> GetSelectedGalleryItems(MediaGalleryNode node)
        {
            if (node.DisplayMode == GalleryDisplayMode.Grid)
                return node.GalleryItems.Where(i => i.IsSelected);
            return node.GalleryGroups.SelectMany(g => g.Items).Where(i => i.IsSelected);
        }

        private static void DownloadSelectedMedia(MediaGalleryNode node, IWorkflowEditorHost host, bool isImage)
        {
            var folder = MediaGalleryFolderHelper.GetEffectiveFolderPath(node, host.ViewModel?.Nodes, forVideo: !isImage);
            if (string.IsNullOrWhiteSpace(folder))
            {
                System.Windows.MessageBox.Show(
                    isImage
                        ? "Chưa chọn folder lưu ảnh. Trong dialog Gallery: điền \"Folder lưu ảnh\" hoặc chọn Node + Key (folder ảnh) khi để trống."
                        : "Chưa chọn folder lưu video. Trong dialog Gallery: điền \"Folder lưu video\" hoặc chọn Node + Key (folder video) khi để trống.",
                    isImage ? "Tải ảnh" : "Tải video",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }
            var dir = folder.Trim();
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Không tạo được folder: " + ex.Message, isImage ? "Tải ảnh" : "Tải video", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            var selected = GetSelectedGalleryItems(node).ToList();
            var urls = isImage
                ? selected.Where(i => !string.IsNullOrWhiteSpace(i.ImageUrl)).Select(i => (i.ImageUrl!.Trim(), i.Title)).ToList()
                : selected.Where(i => !string.IsNullOrWhiteSpace(i.VideoUrl)).Select(i => (i.VideoUrl!.Trim(), i.Title)).ToList();
            if (urls.Count == 0)
            {
                System.Windows.MessageBox.Show("Không có item nào được chọn hoặc không có URL " + (isImage ? "ảnh" : "video") + ".", isImage ? "Tải ảnh" : "Tải video", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }
            _ = DownloadUrlsToFolderAsync(urls, dir, isImage);
        }

        private const int MaxFileNameBaseLength = 150;

        private static async Task DownloadUrlsToFolderAsync(List<(string Url, string Title)> urls, string folder, bool isImage)
        {
            var ext = isImage ? ".jpg" : ".mp4";
            var total = urls.Count;
            var label = isImage ? "ảnh" : "video";
            Window? progressWindow = null;
            TextBlock? progressText = null;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                progressText = new TextBlock
                {
                    Text = $"Đang tải {label}... 0/{total}",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 13,
                    Margin = new Thickness(16, 12, 16, 12),
                    VerticalAlignment = VerticalAlignment.Center
                };
                progressWindow = new Window
                {
                    Title = isImage ? "Tải ảnh" : "Tải video",
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    Topmost = true,
                    Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                    Content = progressText
                };
                progressWindow.Show();
            });

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < urls.Count; i++)
            {
                var current = i + 1;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (progressText != null)
                        progressText.Text = $"Đang tải {label}... {current}/{total}";
                });

                var (url, title) = urls[i];
                try
                {
                    var baseName = SafeFileName(string.IsNullOrWhiteSpace(title) ? GetBaseNameFromUrl(url) : title);
                    if (string.IsNullOrEmpty(baseName)) baseName = "file_" + (i + 1);
                    if (baseName.Length > MaxFileNameBaseLength)
                        baseName = baseName.Substring(0, MaxFileNameBaseLength);
                    var name = baseName + ext;
                    var n = 0;
                    while (usedNames.Contains(name))
                        name = baseName + "_" + (++n) + ext;
                    var path = System.IO.Path.Combine(folder, name);
                    if (File.Exists(path))
                    {
                        usedNames.Add(name);
                        continue; // Đã có file trùng trong folder, bỏ qua không tải lại
                    }
                    usedNames.Add(name);
                    
                    // Luôn tải từ URL để đảm bảo chất lượng gốc 100%
                    // Cache chỉ dùng để hiển thị, không dùng cho download
                    var bytes = await _downloadClient.GetByteArrayAsync(url).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        System.Windows.MessageBox.Show("Lỗi tải " + url + ": " + ex.Message, isImage ? "Tải ảnh" : "Tải video", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning));
                }
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                progressWindow?.Close();
                System.Windows.MessageBox.Show("Đã lưu " + urls.Count + " file vào " + folder, isImage ? "Tải ảnh" : "Tải video", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            });
        }

        private static string GetBaseNameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            try
            {
                var last = url.TrimEnd('/').Split('/').LastOrDefault();
                return string.IsNullOrEmpty(last) ? "" : System.IO.Path.GetFileNameWithoutExtension(last);
            }
            catch { return ""; }
        }

        private static string SafeFileName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var arr = s.Trim().ToCharArray();
            for (var i = 0; i < arr.Length; i++)
                if (invalid.Contains(arr[i])) arr[i] = '_';
            return new string(arr).TrimStart('.');
        }

        private static Button CreateIconButton(string label)
        {
            return new Button
            {
                Content = label,
                FontSize = 11,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(4, 0, 4, 0),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
        }

        private static Brush GetTitleBrush(MediaGalleryNode node)
        {
            // Màu theo node: mode NodeColor hoặc key rỗng/"NodeColor" (theo NODE_DIALOG_GUIDE)
            if (node.TitleColorMode != TitleColorMode.CustomColor ||
                string.IsNullOrEmpty(node.TitleColorKey) ||
                node.TitleColorKey == "NodeColor")
                return node.NodeBrush;
            if (node.TitleColorKey == "LimeGreen") return new SolidColorBrush(Colors.LimeGreen);
            var brush = Application.Current.TryFindResource(node.TitleColorKey) as Brush;
            return brush ?? node.NodeBrush;
        }

        private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering)
        {
            return mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };
        }

        private static void UpdateTitleVisibility(TextBlock tb, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
        {
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible) { tb.Visibility = Visibility.Collapsed; return; }
            tb.Visibility = GetTitleVisibility(mode, isHovering);
        }

        private static void ThrottledUpdateTitlePosition(TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (!_titleUpdateTimers.TryGetValue(border, out var timer))
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
                timer.Tick += (s, e) => { timer.Stop(); UpdateTitlePosition(tb, border, host); };
                _titleUpdateTimers[border] = timer;
            }
            timer.Stop();
            timer.Start();
        }

        private static void UpdateTitlePosition(TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (host.WorkflowCanvas == null || !host.WorkflowCanvas.Children.Contains(tb)) return;
            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);
            if (double.IsNaN(left) && border.Tag is WorkflowNode n) left = n.X;
            if (double.IsNaN(top) && border.Tag is WorkflowNode n2) top = n2.Y;
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            if (tb.ActualWidth == 0 || tb.ActualHeight == 0)
            {
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                tb.Arrange(new Rect(tb.DesiredSize));
            }
            var titleLeft = left + (border.ActualWidth / 2) - (tb.ActualWidth / 2);
            var titleTop = top - tb.ActualHeight - 4;
            Canvas.SetLeft(tb, titleLeft);
            Canvas.SetTop(tb, titleTop);
        }

        private static void OpenNodeDialog(MediaGalleryNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                if (node.Border != null && node.Border.IsMouseCaptured)
                    node.Border.ReleaseMouseCapture();
                host.DraggedNode = null;
                if (host.ViewModel != null)
                    host.ViewModel.SelectedNode = null;
                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();
                var dialog = new MediaGalleryNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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