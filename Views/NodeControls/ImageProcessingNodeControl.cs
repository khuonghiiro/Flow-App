using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Globalization;
using System.Collections.Specialized;
using System.Text.Json;
using WinForms = System.Windows.Forms;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public static class ImageProcessingNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }

        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50;
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();

        private static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, int> _previewVersion = new();
        private static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, ImageCropRegion?> _activeCropRegion = new();
        private static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, int> _activeCropColorIndex = new();
        private static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, int> _cropOrderCounter = new();
        private static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, ImageCropRegion?> _currentCropRegionForIp = new();
        // Lưu polygon overlay trên imageGrid theo từng vùng crop để xoá/cập nhật đúng khi cần
        private static readonly System.Collections.Generic.Dictionary<ImageCropRegion, System.Windows.Shapes.Polygon> _polygonMap = new();
        private static readonly Color[] _cropColors = new[]
        {
            Colors.Gold,
            Colors.DeepSkyBlue,
            Colors.LimeGreen,
            Colors.OrangeRed,
            Colors.MediumOrchid
        };

        public static Border CreateBorder(ImageProcessingNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // Đảm bảo node có Width/Height hợp lệ (không NaN) để Grid Star columns tính đúng
            double initW = double.IsNaN(node.Width) ? 800 : Math.Max(node.Width, 800);
            double initH = double.IsNaN(node.Height) ? 600 : Math.Max(node.Height, 600);

            // Khởi tạo bộ đếm Order từ các crop đã load sẵn (để crop mới tiếp nối đúng số)
            if (!_cropOrderCounter.ContainsKey(node) && node.Crops.Count > 0)
            {
                _cropOrderCounter[node] = node.Crops.Max(c => c.Order);
            }

            var border = new Border
            {
                Width = initW,
                Height = initH,
                MinWidth = 800,
                MinHeight = 600,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = GpuOptimizationHelper.CreateDropShadowEffect(),
                Tag = node,
                CacheMode = null
            };

            // Force layout refresh on Loaded to ensure Grid Star columns render correctly
            border.Loaded += (s, e) =>
            {
                border.InvalidateMeasure();
                border.InvalidateArrange();
                border.UpdateLayout();
            };

            GpuOptimizationHelper.ApplyToBorder(border);

            // ===== Layout: Top/Left/Right menus + image area =====
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            // Menu trái: tỉ lệ 0.8* (8% của tổng width)
            // Image area: tỉ lệ 6* (60% của tổng width)
            // Menu phải: tỉ lệ 3.2* (32% của tổng width)
            // Tổng: 10* → khi node width tăng 10px:
            //   - Menu trái tăng 0.8px
            //   - Image area tăng 6px
            //   - Menu phải tăng 3.2px
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3.2, GridUnitType.Star) });
            // Column 3: Image Processor (ẩn mặc định, width=0)
            var ipColumnDef = new ColumnDefinition { Width = new GridLength(0) };
            root.ColumnDefinitions.Add(ipColumnDef);

            bool ipColumnVisible = false;
            const double IP_COL_STAR = 3.5;
            const double BASE_STAR = 10.0; // 0.8 + 6 + 3.2
            double originalMinWidth = border.MinWidth;
            Action<ImageCropRegion>? onCropClickForIP = null;

            void ToggleIPColumn()
            {
                ipColumnVisible = !ipColumnVisible;
                double totalStar = BASE_STAR + IP_COL_STAR; // 13.5
                if (ipColumnVisible)
                {
                    // Mở rộng node: giữ phần content gốc nguyên, thêm phần IP
                    double currentContentWidth = node.Width;
                    double newWidth = currentContentWidth * totalStar / BASE_STAR;
                    ipColumnDef.Width = new GridLength(IP_COL_STAR, GridUnitType.Star);
                    node.Width = newWidth;
                    border.Width = newWidth;
                    border.MinWidth = originalMinWidth * totalStar / BASE_STAR;
                }
                else
                {
                    // Thu hẹp node: bỏ phần IP
                    double newWidth = node.Width * BASE_STAR / totalStar;
                    ipColumnDef.Width = new GridLength(0);
                    node.Width = Math.Max(originalMinWidth, newWidth);
                    border.Width = node.Width;
                    border.MinWidth = originalMinWidth;
                }
            }

            // --- Top menu: hiển thị title ảnh ---
            var topMenu = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                Padding = new Thickness(8, 4, 8, 4)
            };
            Grid.SetRow(topMenu, 0);
            Grid.SetColumnSpan(topMenu, 3);

            // TextBlock hiển thị title với text truncation
            var imageTitleTextBlock = new TextBlock
            {
                Text = "Chưa có ảnh",
                Foreground = Application.Current.TryFindResource("WhiteBrush") as Brush ?? new SolidColorBrush(Colors.White),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Toggle button for Image Processor column (góc phải trên)
            var ipToggleBtn = new Button
            {
                Content = "📐",
                ToolTip = "Ẩn/hiện Image Processor",
                Width = 28,
                Height = 22,
                FontSize = 12,
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Style = Application.Current.TryFindResource("DangerButton") as Style,
            };
            ipToggleBtn.MouseLeftButtonDown += (s, e) => e.Handled = true;
            ipToggleBtn.Click += (s, e) =>
            {
                ToggleIPColumn();
                e.Handled = true;
            };

            var topContent = new DockPanel();
            DockPanel.SetDock(ipToggleBtn, Dock.Right);
            topContent.Children.Add(ipToggleBtn);
            topContent.Children.Add(imageTitleTextBlock);

            var topDragLayer = new Border
            {
                Background = Brushes.Transparent,
                IsHitTestVisible = true,
                Child = topContent
            };
            topMenu.Child = topDragLayer;
            root.Children.Add(topMenu);

            // --- Left menu: chứa nút Mở, Crop + vùng kéo node ---
            var leftMenu = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(4, 0, 4, 6) // Sát top: padding top = 0
            };
            var leftStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 48 // Fixed reference width — Viewbox sẽ scale tỉ lệ
            };

            // Button Mở (icon folder) - đặt đầu tiên, sát top
            var btnOpen = new Button
            {
                Content = "📁", // Icon folder Unicode
                ToolTip = "Mở ảnh",
                Width = 40,
                Height = 40,
                Style = Application.Current.TryFindResource("PrimaryButton") as Style,
                FontSize = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 10)
            };
            // Chặn bubble lên Border để không kéo node khi bấm nút,
            // nhưng vẫn cho Button nhận mouse để raise Click.
            btnOpen.MouseLeftButtonDown += (s, e) => e.Handled = true;
            btnOpen.Click += (s, e) =>
            {
                e.Handled = true;
                OpenImageFilePicker(node);
            };
            leftStack.Children.Add(btnOpen);

            // Nhóm nút màu + crop chung một khung để user biết cùng chức năng
            var colorCropGroupBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var colorCropStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };

            // Nút chọn màu crop (chọn mã màu tuỳ ý hoặc cycle khi ColorDialog lỗi)
            var colorButton = new Button
            {
                Width = 40,
                Height = 40,
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                ToolTip = "Chọn màu viền/background vùng crop"
            };
            void UpdateColorButtonBackground()
            {
                if (!_activeCropColorIndex.TryGetValue(node, out var idx))
                    idx = 0;
                idx = ((idx % _cropColors.Length) + _cropColors.Length) % _cropColors.Length;
                colorButton.Background = new SolidColorBrush(_cropColors[idx]);
            }
            colorButton.Click += (s, e) =>
            {
                try
                {
                    using var dlg = new WinForms.ColorDialog();
                    if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        var c = dlg.Color;
                        var mediaColor = Color.FromArgb(255, c.R, c.G, c.B);
                        // Lưu màu tuỳ chỉnh vào slot 0 cho node hiện tại
                        if (_cropColors.Length > 0)
                        {
                            _cropColors[0] = mediaColor;
                            _activeCropColorIndex[node] = 0;
                        }
                        colorButton.Background = new SolidColorBrush(mediaColor);
                        e.Handled = true;
                        return;
                    }
                }
                catch
                {
                    // Nếu ColorDialog lỗi thì rơi xuống phần cycle
                }

                // Fallback: cycle qua palette có sẵn
                if (!_activeCropColorIndex.TryGetValue(node, out var idxFallback))
                    idxFallback = 0;
                idxFallback = (idxFallback + 1) % _cropColors.Length;
                _activeCropColorIndex[node] = idxFallback;
                UpdateColorButtonBackground();
                e.Handled = true;
            };
            UpdateColorButtonBackground();
            colorCropStack.Children.Add(colorButton);

            // Nút Crop (gợi ý Alt + Click) – mỗi lần nhấn sẽ bắt đầu một crop mới
            var cropButton = new Button
            {
                Content = "✂",
                ToolTip = "Crop ảnh (Alt + click trái trên ảnh để tạo điểm)",
                Style = Application.Current.TryFindResource("WarningButton") as Style,
                Width = 40,
                Height = 40,
                Margin = new Thickness(0, 0, 0, 0),
                FontSize = 18,
                Padding = new Thickness(0)
            };
            // Không cho nút làm node bị kéo
            cropButton.MouseLeftButtonDown += (s, e) => e.Handled = true;
            cropButton.Click += (s, e) =>
            {
                // Mỗi lần nhấn: bắt đầu một vùng crop mới (không tái sử dụng vùng cũ)
                _activeCropRegion[node] = null;
                e.Handled = true;
            };
            colorCropStack.Children.Add(cropButton);

            colorCropGroupBorder.Child = colorCropStack;
            leftStack.Children.Add(colorCropGroupBorder);

            // Vùng drag (transparent) để kéo node khi giữ chuột trên menu trái
            var leftDragArea = new Border
            {
                Background = Brushes.Transparent,
                Width = 22,
                Height = 40,
                Margin = new Thickness(0, 0, 0, 6)
            };
            leftStack.Children.Add(leftDragArea);

            // Wrap leftStack trong Viewbox để buttons scale tỉ lệ với column width
            var leftViewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };
            leftViewbox.Child = leftStack;
            leftMenu.Child = leftViewbox;
            Grid.SetRow(leftMenu, 1);
            Grid.SetColumn(leftMenu, 0);
            root.Children.Add(leftMenu);

            // --- Right menu: danh sách ảnh cắt + logic render ảnh ---
            var rightMenu = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                Padding = new Thickness(4, 4, 4, 4)
            };

            var cropsLabel = new TextBlock
            {
                Text = "Danh sách ảnh cắt",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 9,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var cropsList = new ItemsControl
            {
                ItemsSource = node.Crops,
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Danh sách crop: mỗi item 1 dòng (StackPanel dọc)
            var itemsPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            itemsPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            itemsPanelFactory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            cropsList.ItemsPanel = new ItemsPanelTemplate(itemsPanelFactory);

            // Biến tham chiếu tới imageGrid (sẽ được gán sau khi imageGrid được khai báo)
            Grid? imageGridRef = null;

            // Mỗi item: số thứ tự + ảnh crop + nút xử lý
            var itemFactory = new FrameworkElementFactory(typeof(StackPanel));
            itemFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            itemFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(1, 0, 1, 4));
            itemFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);

            // Số thứ tự crop (bên trái ảnh thumbnail)
            var orderFactory = new FrameworkElementFactory(typeof(TextBlock));
            orderFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ImageCropRegion.Order)));
            orderFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.White));
            orderFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            orderFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            orderFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            orderFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
            orderFactory.SetValue(TextBlock.OpacityProperty, 0.7);
            itemFactory.AppendChild(orderFactory);

            var imgBorderFactory = new FrameworkElementFactory(typeof(Border));
            // Bind BorderBrush theo màu của từng vùng crop (StrokeBrush từ ColorHex)
            imgBorderFactory.SetBinding(Border.BorderBrushProperty,
                new System.Windows.Data.Binding(nameof(ImageCropRegion.StrokeBrush)));
            imgBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            imgBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            imgBorderFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 1));
            imgBorderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)));
            // Đặt width cố định cho thumbnail để phần nút có không gian
            imgBorderFactory.SetValue(FrameworkElement.WidthProperty, 64.0);

            var imgFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            imgFactory.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.Uniform);
            imgFactory.SetValue(FrameworkElement.MaxHeightProperty, 64.0);
            imgFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            imgFactory.SetBinding(System.Windows.Controls.Image.SourceProperty, new System.Windows.Data.Binding(nameof(ImageCropRegion.Thumbnail)));

            imgBorderFactory.AppendChild(imgFactory);
            imgBorderFactory.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
            imgBorderFactory.SetValue(FrameworkElement.ToolTipProperty, "Click để mở Image Processor");
            imgBorderFactory.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is FrameworkElement fe && fe.DataContext is ImageCropRegion reg)
                {
                    onCropClickForIP?.Invoke(reg);
                    e.Handled = true;
                }
            }));
            itemFactory.AppendChild(imgBorderFactory);

            // Cột phải: StackPanel ngang chứa các nút xử lý (từ trái sang phải)
            var rightPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            rightPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            rightPanelFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            rightPanelFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            rightPanelFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));

            // Checkbox 1: Ẩn/hiện vùng crop – màu vàng (Gold)
            var chkFactory = new FrameworkElementFactory(typeof(CheckBox));
            chkFactory.SetValue(CheckBox.ToolTipProperty, "Bật/tắt hiển thị vùng crop");
            chkFactory.SetValue(CheckBox.FontSizeProperty, 8.0);
            chkFactory.SetValue(CheckBox.ForegroundProperty, new SolidColorBrush(Colors.Gold));
            chkFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            chkFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 3, 0));
            chkFactory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(ImageCropRegion.IsVisible)) { Mode = System.Windows.Data.BindingMode.TwoWay });
            rightPanelFactory.AppendChild(chkFactory);

            // Checkbox 2: Ẩn nền + viền nét đứt – màu primary
            var chkOutlineFactory = new FrameworkElementFactory(typeof(CheckBox));
            chkOutlineFactory.SetValue(CheckBox.ToolTipProperty, "Ẩn nền polygon, đổi viền sang nét đứt");
            chkOutlineFactory.SetValue(CheckBox.FontSizeProperty, 8.0);
            chkOutlineFactory.SetValue(CheckBox.ForegroundProperty,
                Application.Current.TryFindResource("PrimaryBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(0x42, 0x96, 0xD0)));
            chkOutlineFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            chkOutlineFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 3, 0));
            chkOutlineFactory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(ImageCropRegion.IsOutlineOnly)) { Mode = System.Windows.Data.BindingMode.TwoWay });
            rightPanelFactory.AppendChild(chkOutlineFactory);

            var btnDelFactory = new FrameworkElementFactory(typeof(Button));
            // Hiển thị text "X" với tooltip giải thích, dùng DangerButton style
            btnDelFactory.SetValue(FrameworkElement.StyleProperty, Application.Current.TryFindResource("DangerButton"));
            btnDelFactory.SetValue(Button.ContentProperty, "X");
            btnDelFactory.SetValue(Button.WidthProperty, 18.0);
            btnDelFactory.SetValue(Button.HeightProperty, 18.0);
            btnDelFactory.SetValue(Button.FontSizeProperty, 9.0);
            btnDelFactory.SetValue(Button.PaddingProperty, new Thickness(0));
            btnDelFactory.SetValue(Button.MarginProperty, new Thickness(0));
            btnDelFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            btnDelFactory.SetValue(Button.ToolTipProperty, "Xoá vùng crop này");
            // Thêm PreviewMouseLeftButtonUp (tunneling) để đảm bảo xoá chạy kể cả khi outer handler bắt event
            btnDelFactory.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is Button b && b.DataContext is ImageCropRegion reg)
                {
                    if (imageGridRef != null && _polygonMap.TryGetValue(reg, out var poly))
                    {
                        imageGridRef.Children.Remove(poly);
                        _polygonMap.Remove(reg);
                    }
                    node.Crops.Remove(reg);
                    if (_activeCropRegion.TryGetValue(node, out var active) && ReferenceEquals(active, reg))
                    {
                        _activeCropRegion[node] = null;
                        cropButton.IsEnabled = true;
                    }
                    e.Handled = true;
                }
            }));
            btnDelFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                // Fallback: nếu PreviewMouseLeftButtonUp không chạy được
                if (s is Button b && b.DataContext is ImageCropRegion reg)
                {
                    if (imageGridRef != null && _polygonMap.TryGetValue(reg, out var poly))
                    {
                        imageGridRef.Children.Remove(poly);
                        _polygonMap.Remove(reg);
                    }
                    node.Crops.Remove(reg);
                    if (_activeCropRegion.TryGetValue(node, out var active) && ReferenceEquals(active, reg))
                    {
                        _activeCropRegion[node] = null;
                        cropButton.IsEnabled = true;
                    }
                    e.Handled = true;
                }
            }));
            rightPanelFactory.AppendChild(btnDelFactory);

            itemFactory.AppendChild(rightPanelFactory);

            cropsList.ItemTemplate = new DataTemplate(typeof(ImageCropRegion)) { VisualTree = itemFactory };

            // ── Phần trên (50%): danh sách crop ──
            var cropsContentStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            cropsContentStack.Children.Add(cropsLabel);
            cropsContentStack.Children.Add(cropsList);

            var cropsViewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };
            cropsViewbox.Child = cropsContentStack;

            var cropsScroll = new ScrollViewer
            {
                Content = cropsViewbox,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            // ── Phần dưới (50%): danh sách ảnh render ──
            // Label
            var renderLabel = new TextBlock
            {
                Text = "Ảnh render",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 9,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Danh sách nhóm ảnh render theo crop: #Order + list ảnh
            var renderGroups = new ItemsControl
            {
                ItemsSource = node.Crops
            };

            var groupTemplate = new DataTemplate(typeof(ImageCropRegion));
            var groupRoot = new FrameworkElementFactory(typeof(StackPanel));
            groupRoot.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            groupRoot.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 6));

            // Header: #Order
            var headerText = new FrameworkElementFactory(typeof(TextBlock));
            headerText.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.White));
            headerText.SetValue(TextBlock.FontSizeProperty, 9.0);
            headerText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            headerText.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 2));
            headerText.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(ImageCropRegion.Order))
                {
                    Converter = new IntToHashLabelConverter()
                });
            groupRoot.AppendChild(headerText);

            // Danh sách ảnh render trong nhóm (WrapPanel - 3 ảnh/dòng)
            var imagesItemsControl = new FrameworkElementFactory(typeof(ItemsControl));
            imagesItemsControl.SetBinding(ItemsControl.ItemsSourceProperty,
                new System.Windows.Data.Binding(nameof(ImageCropRegion.RenderedImages)));

            var imagesPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(System.Windows.Controls.WrapPanel)));
            imagesPanel.VisualTree.SetValue(System.Windows.Controls.WrapPanel.OrientationProperty, Orientation.Horizontal);
            imagesItemsControl.SetValue(ItemsControl.ItemsPanelProperty, imagesPanel);

            var imageItemTemplate = new DataTemplate();
            var imageBorder = new FrameworkElementFactory(typeof(Border));
            imageBorder.SetValue(Border.BorderBrushProperty,
                new SolidColorBrush(Color.FromArgb(120, 148, 163, 184)));
            imageBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            imageBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            imageBorder.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 4, 2));
            imageBorder.SetValue(FrameworkElement.WidthProperty, 55.0);

            var imageInGroup = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            imageInGroup.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.Uniform);
            imageInGroup.SetValue(FrameworkElement.MaxHeightProperty, 55.0);
            imageInGroup.SetBinding(System.Windows.Controls.Image.SourceProperty, new System.Windows.Data.Binding());

            imageBorder.AppendChild(imageInGroup);
            imageItemTemplate.VisualTree = imageBorder;
            imagesItemsControl.SetValue(ItemsControl.ItemTemplateProperty, imageItemTemplate);

            groupRoot.AppendChild(imagesItemsControl);
            groupTemplate.VisualTree = groupRoot;
            renderGroups.ItemTemplate = groupTemplate;

            var renderContentStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            renderContentStack.Children.Add(renderLabel);
            renderContentStack.Children.Add(renderGroups);

            var renderViewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };
            renderViewbox.Child = renderContentStack;

            var renderScroll = new ScrollViewer
            {
                Content = renderViewbox,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            // ── Chia đôi cột phải: Grid 2 dòng * bằng nhau ──
            var rightSplitGrid = new Grid();
            rightSplitGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightSplitGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(cropsScroll, 0);
            Grid.SetRow(renderScroll, 1);
            rightSplitGrid.Children.Add(cropsScroll);
            rightSplitGrid.Children.Add(renderScroll);

            rightMenu.Child = rightSplitGrid;
            Grid.SetRow(rightMenu, 1);
            Grid.SetColumn(rightMenu, 2);
            root.Children.Add(rightMenu);

            // --- Image Processor column (Column 3, ẩn mặc định) ---
            var (ipColumnBorder, setIpImage) = BuildImageProcessorColumn(node, host);
            Grid.SetRow(ipColumnBorder, 0);
            Grid.SetRowSpan(ipColumnBorder, 2);
            Grid.SetColumn(ipColumnBorder, 3);
            root.Children.Add(ipColumnBorder);

            // --- Image area ---
            var image = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            if (GpuDetectionHelper.IsGpuAvailable)
            {
                RenderOptions.SetCachingHint(image, CachingHint.Unspecified);
                image.CacheMode = null;
            }

            var scale = new ScaleTransform(1.0, 1.0);

            var placeholder = new TextBlock
            {
                Text = "Chưa có ảnh. Mở dialog (chuột phải hoặc nút Mở) để dán link/base64.",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.85
            };

            // imageGrid chứa image + polygon, scale chung qua LayoutTransform
            var imageGrid = new Grid();
            imageGridRef = imageGrid; // Gán cho biến tham chiếu dùng trong btnDelFactory handlers
            imageGrid.Children.Add(image);
            imageGrid.Children.Add(placeholder);
            imageGrid.LayoutTransform = scale;
            imageGrid.HorizontalAlignment = HorizontalAlignment.Center;
            imageGrid.VerticalAlignment = VerticalAlignment.Center;

            // Wrapper để center imageGrid khi nhỏ hơn viewport
            var scrollContent = new Grid();
            scrollContent.Children.Add(imageGrid);

            var scrollViewer = new ScrollViewer
            {
                Content = scrollContent,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0),
                CacheMode = null,
                Focusable = true
            };

            // Bind MinWidth/MinHeight sau khi scrollViewer tạo xong
            scrollContent.SetBinding(FrameworkElement.MinWidthProperty,
                new System.Windows.Data.Binding("ViewportWidth") { Source = scrollViewer });
            scrollContent.SetBinding(FrameworkElement.MinHeightProperty,
                new System.Windows.Data.Binding("ViewportHeight") { Source = scrollViewer });
            if (GpuDetectionHelper.IsGpuAvailable)
            {
                RenderOptions.SetBitmapScalingMode(scrollViewer, BitmapScalingMode.Unspecified);
                RenderOptions.SetCachingHint(scrollViewer, CachingHint.Unspecified);
            }

            // Khi xoá crop khỏi danh sách → xoá cả polygon overlay trên imageGrid
            // Lưu handler vào biến local để có thể huỷ đăng ký khi border Unloaded
            NotifyCollectionChangedEventHandler cropsChangedHandler = (s, e) =>
            {
                if (e.OldItems == null) return;
                foreach (ImageCropRegion removedReg in e.OldItems)
                {
                    if (_polygonMap.TryGetValue(removedReg, out var poly))
                    {
                        imageGrid.Children.Remove(poly);
                        _polygonMap.Remove(removedReg);
                    }
                }
            };
            node.Crops.CollectionChanged += cropsChangedHandler;

            Grid.SetRow(scrollViewer, 1);
            Grid.SetColumn(scrollViewer, 1);
            root.Children.Add(scrollViewer);

            // Wire crop click → mở Image Processor column với ảnh crop đó
            onCropClickForIP = (reg) =>
            {
                if (!ipColumnVisible) ToggleIPColumn();

                // Lưu crop region hiện tại để dùng khi nhấn "Bắt đầu"
                _currentCropRegionForIp[node] = reg;

                // Dùng Thumbnail (ảnh đã crop theo polygon) — giống hiển thị ở cột crop
                if (reg.Thumbnail is BitmapSource thumb)
                {
                    setIpImage(thumb);
                }
            };

            // Zoom: Ctrl + wheel, keep cursor point stable
            scrollViewer.PreviewMouseWheel += (s, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                    return; // allow workflow zoom / normal scroll

                e.Handled = true;

                var position = e.GetPosition(scrollViewer);
                var oldScale = scale.ScaleX;
                double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
                var newScale = oldScale * zoomFactor;
                if (newScale < 0.01) newScale = 0.01;
                if (newScale > 100.0) newScale = 100.0;
                if (Math.Abs(newScale - oldScale) < 0.0001) return;

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

            // --- Magnifier overlay (góc phải dưới, hiện khi Alt) ---
            const int magSize = 120;
            const int magZoom = 4;
            var magImage = new System.Windows.Controls.Image
            {
                Width = magSize,
                Height = magSize,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapScalingMode(magImage, BitmapScalingMode.NearestNeighbor);
            // Crosshair ở tâm magnifier
            var magCrosshair = new Ellipse
            {
                Width = 10,
                Height = 10,
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeThickness = 1.5,
                Fill = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            var magCrossH = new System.Windows.Shapes.Line
            {
                X1 = 0,
                Y1 = magSize / 2.0,
                X2 = magSize,
                Y2 = magSize / 2.0,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 0, 0)),
                StrokeThickness = 0.8,
                IsHitTestVisible = false
            };
            var magCrossV = new System.Windows.Shapes.Line
            {
                X1 = magSize / 2.0,
                Y1 = 0,
                X2 = magSize / 2.0,
                Y2 = magSize,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 0, 0)),
                StrokeThickness = 0.8,
                IsHitTestVisible = false
            };
            var magContent = new Grid
            {
                Width = magSize,
                Height = magSize,
                ClipToBounds = true,
                IsHitTestVisible = false
            };
            magContent.Children.Add(magImage);
            magContent.Children.Add(magCrossH);
            magContent.Children.Add(magCrossV);
            magContent.Children.Add(magCrosshair);

            var magBorder = new Border
            {
                Width = magSize + 4,
                Height = magSize + 4,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromArgb(200, 0x1E, 0x1E, 0x2E)),
                CornerRadius = new CornerRadius(4),
                Child = magContent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 8, 8),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            // Crosshair + magnifier khi Alt
            var magCoordText = new TextBlock
            {
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2),
                IsHitTestVisible = false
            };
            var magPanel = new Grid
            {
                Width = magSize + 4,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 8, 8),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            magPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            magPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(magBorder, 0); // move magBorder into panel instead
            magPanel.Children.Add(magBorder);
            magBorder.HorizontalAlignment = HorizontalAlignment.Center;
            magBorder.VerticalAlignment = VerticalAlignment.Top;
            magBorder.Margin = new Thickness(0);
            magBorder.Visibility = Visibility.Visible;
            Grid.SetRow(magCoordText, 1);
            magPanel.Children.Add(magCoordText);
            Grid.SetRow(magPanel, 1);
            Grid.SetColumn(magPanel, 1);
            System.Windows.Controls.Panel.SetZIndex(magPanel, 100);
            root.Children.Add(magPanel);

            void UpdateMagnifier(Point imgPos)
            {
                if (image.Source is not BitmapSource bmp) return;
                int px = (int)Math.Round(imgPos.X);
                int py = (int)Math.Round(imgPos.Y);
                magCoordText.Text = $"{px}, {py}";

                int halfRegion = magSize / (magZoom * 2);
                int srcX = Math.Max(0, px - halfRegion);
                int srcY = Math.Max(0, py - halfRegion);
                int srcW = halfRegion * 2;
                int srcH = halfRegion * 2;
                if (srcX + srcW > bmp.PixelWidth) srcW = bmp.PixelWidth - srcX;
                if (srcY + srcH > bmp.PixelHeight) srcH = bmp.PixelHeight - srcY;
                if (srcW <= 0 || srcH <= 0) return;

                try
                {
                    var cb = new CroppedBitmap(bmp, new Int32Rect(srcX, srcY, srcW, srcH));
                    magImage.Source = cb;
                }
                catch { /* ignore */ }
            }

            // Pan: only when content overflows viewport
            bool isPanning = false;
            Point panStart = default;
            double panOriginX = 0, panOriginY = 0;

            scrollViewer.PreviewMouseLeftButtonDown += (s, e) =>
            {
                scrollViewer.Focus();

                // ALT + left click: thêm point cho crop polygon thay vì pan.
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    e.Handled = true;
                    AddCropPointFromClick(node, image, scale, e.GetPosition(image), imageGrid, cropButton, onCropClickForIP);
                    return;
                }

                if (scrollViewer.ExtentWidth <= scrollViewer.ViewportWidth &&
                    scrollViewer.ExtentHeight <= scrollViewer.ViewportHeight) return;
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
                if (isPanning)
                {
                    var pos = e.GetPosition(scrollViewer);
                    var dx = pos.X - panStart.X;
                    var dy = pos.Y - panStart.Y;
                    scrollViewer.ScrollToHorizontalOffset(panOriginX - dx);
                    scrollViewer.ScrollToVerticalOffset(panOriginY - dy);
                    e.Handled = true;
                    return;
                }

                // Khi Alt: hiện crosshair + magnifier
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && image.Source is BitmapSource)
                {
                    scrollViewer.Cursor = Cursors.Cross;
                    magPanel.Visibility = Visibility.Visible;
                    UpdateMagnifier(e.GetPosition(image));
                }
                else
                {
                    if (magPanel.Visibility == Visibility.Visible)
                        magPanel.Visibility = Visibility.Collapsed;
                    if (scrollViewer.Cursor == Cursors.Cross)
                        scrollViewer.Cursor = Cursors.Arrow;
                }
            };

            // Ẩn magnifier khi chuột rời vùng ảnh
            scrollViewer.MouseLeave += (s, e) =>
            {
                magPanel.Visibility = Visibility.Collapsed;
                if (scrollViewer.Cursor == Cursors.Cross)
                    scrollViewer.Cursor = Cursors.Arrow;
            };

            // Enter: hoàn thành crop hiện tại (nối end → start hoặc huỷ nếu < 3 điểm)
            scrollViewer.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    if (CompleteActiveCrop(node, cropButton))
                    {
                        e.Handled = true;
                    }
                }
            };

            // --- Resize handles overlay (same pattern as MediaGallery) ---
            var handleOverlay = new Grid();
            AddResizeHandle(handleOverlay, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 2, 0, 0));

            var outerGrid = new Grid();
            outerGrid.Children.Add(root);
            outerGrid.Children.Add(handleOverlay);
            GpuOptimizationHelper.ApplyToElement(outerGrid);
            border.Child = outerGrid;

            bool isResizing = false;
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

                var minW = border.MinWidth > 0 ? border.MinWidth : 260;
                var minH = border.MinHeight > 0 ? border.MinHeight : 200;

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

            // ===== TitleTextBlock (follow Node_Dialog_V2) =====
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Xử lý ảnh",
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
                        titleTextBlock.Text = node.Title ?? "Xử lý ảnh";
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitlePosition(titleTextBlock, border, host);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(ImageProcessingNode.TitleDisplayMode))
                    {
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    }
                    else if (e.PropertyName == nameof(ImageProcessingNode.TitleColorMode) ||
                             e.PropertyName == nameof(ImageProcessingNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(ImageProcessingNode.Width) ||
                             e.PropertyName == nameof(ImageProcessingNode.Height))
                    {
                        if (!isResizing)
                        {
                            border.Width = node.Width;
                            border.Height = node.Height;
                        }

                        // Scale UI elements theo Height, baseline = MinHeight
                        var heightBaseline = border.MinHeight > 0 ? border.MinHeight : 600.0;
                        var heightScaleFactor = Math.Max(0.8, Math.Min(1.8, node.Height / heightBaseline));
                        var menuHeightScale = new ScaleTransform(heightScaleFactor, heightScaleFactor);
                        topMenu.LayoutTransform = menuHeightScale;
                        // Không scale leftStack theo height vì button trong menu trái chỉ scale theo width
                        cropsLabel.LayoutTransform = menuHeightScale;

                        // Scale UI elements theo Width, baseline = MinWidth
                        var widthBaseline = border.MinWidth > 0 ? border.MinWidth : 800.0;

                        // Menu trái chiếm 0.8/10 = 8% của tổng width
                        // Tính scale factor dựa trên width thực tế của menu trái
                        // Button sẽ scale tỉ lệ 1:1 với width của menu trái
                        // Khi menu trái width tăng 1px → button scale theo tỉ lệ đó
                        const double leftMenuWidthRatio = 0.8 / 10.0; // 8%
                        var leftMenuBaselineWidth = widthBaseline * leftMenuWidthRatio;
                        // Tính width hiện tại của menu trái từ node.Width
                        var leftMenuCurrentWidth = node.Width * leftMenuWidthRatio;
                        // Scale factor = tỉ lệ width hiện tại / width baseline của menu trái
                        // Điều này đảm bảo button scale đúng theo tỉ lệ width của menu trái
                        var leftMenuScaleFactor = leftMenuBaselineWidth > 0
                            ? leftMenuCurrentWidth / leftMenuBaselineWidth
                            : 1.0;

                        // Scale menu trái theo tỉ lệ width thực tế của menu trái
                        // Khi menu trái tăng 1px, button sẽ scale theo tỉ lệ tương ứng
                        var leftMenuScale = new ScaleTransform(leftMenuScaleFactor, leftMenuScaleFactor);
                        leftMenu.LayoutTransform = leftMenuScale;

                        // Scale factor cho menu phải dựa trên width của toàn node
                        var widthScaleFactor = node.Width / widthBaseline;
                        var interactionScale = Math.Max(heightScaleFactor, leftMenuScaleFactor);
                        UpdateInteractionVisualScale(handleOverlay, node, interactionScale);


                    }
                    else if (e.PropertyName == nameof(ImageProcessingNode.InputMode) ||
                             e.PropertyName == nameof(ImageProcessingNode.ImageUrl) ||
                             e.PropertyName == nameof(ImageProcessingNode.ImageBase64) ||
                             e.PropertyName == nameof(ImageProcessingNode.ImageUrlSourceNodeId) ||
                             e.PropertyName == nameof(ImageProcessingNode.ImageUrlSourceOutputKey) ||
                             e.PropertyName == nameof(ImageProcessingNode.ImageBase64SourceNodeId) ||
                             e.PropertyName == nameof(ImageProcessingNode.ImageBase64SourceOutputKey))
                    {
                        _ = UpdatePreviewAsync(
                            node, host, image, placeholder, scale,
                            imageGrid, scrollViewer, imageTitleTextBlock,
                            onCropClickForIP);
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

                // Áp dụng scale ban đầu dựa trên node.Width/Height để khôi phục đúng kích thước khi load workflow
                // (ActualWidth/ActualHeight có thể là 0 tại thời điểm Loaded, nên dùng node.Width/Height trực tiếp)
                var initHeightBaseline = border.MinHeight > 0 ? border.MinHeight : 600.0;
                var initHeightScaleFactor = Math.Max(0.8, Math.Min(1.8, node.Height / initHeightBaseline));
                var initMenuHeightScale = new ScaleTransform(initHeightScaleFactor, initHeightScaleFactor);
                topMenu.LayoutTransform = initMenuHeightScale;
                cropsLabel.LayoutTransform = initMenuHeightScale;

                var initWidthBaseline = border.MinWidth > 0 ? border.MinWidth : 800.0;
                const double initLeftMenuWidthRatio = 0.8 / 10.0;
                var initLeftMenuBaselineWidth = initWidthBaseline * initLeftMenuWidthRatio;
                var initLeftMenuCurrentWidth = node.Width * initLeftMenuWidthRatio;
                var initLeftMenuScaleFactor = initLeftMenuBaselineWidth > 0
                    ? initLeftMenuCurrentWidth / initLeftMenuBaselineWidth
                    : 1.0;
                leftMenu.LayoutTransform = new ScaleTransform(initLeftMenuScaleFactor, initLeftMenuScaleFactor);
                var initInteractionScale = Math.Max(initHeightScaleFactor, initLeftMenuScaleFactor);
                UpdateInteractionVisualScale(handleOverlay, node, initInteractionScale);



                _ = UpdatePreviewAsync(
                    node, host, image, placeholder, scale,
                    imageGrid, scrollViewer, imageTitleTextBlock,
                    onCropClickForIP);
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
                    // Huỷ đăng ký CollectionChanged để handler không giữ reference đến imageGrid cũ
                    node.Crops.CollectionChanged -= cropsChangedHandler;
                    // Dọn sạch polygon map khi node unload
                    foreach (var reg in node.Crops)
                        _polygonMap.Remove(reg);
                    _activeCropRegion.Remove(node);
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

            // Chặn context menu mặc định trên vùng ảnh
            scrollViewer.ContextMenu = new ContextMenu { Visibility = Visibility.Collapsed };

            // Chuột phải trên ảnh → hoàn thành crop (thay cho Enter)
            scrollViewer.PreviewMouseRightButtonDown += (s, e) =>
            {
                e.Handled = true;
                CompleteActiveCrop(node, cropButton);
            };

            // Chuột phải trên các vùng khác của node → mở dialog
            border.MouseRightButtonUp += (s, e) =>
            {
                var src = e.OriginalSource as DependencyObject;
                bool IsInside(FrameworkElement fe)
                    => src != null && fe != null && VisualTreeHelper.GetParent(src) != null &&
                       fe.IsAncestorOf(src);

                if (!IsInside(scrollViewer))
                {
                    e.Handled = true;
                    OpenNodeDialog(node, host, ownerWindow);
                }
            };

            return border;
        }

        private static void OpenImageFilePicker(ImageProcessingNode node)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Chọn ảnh",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dlg.ShowDialog() == true)
                {
                    node.ImageUrl = dlg.FileName;
                    node.InputMode = ImageInputMode.Url;
                    node.RaisePropertyChanged(nameof(ImageProcessingNode.ImageUrl));
                    node.RaisePropertyChanged(nameof(ImageProcessingNode.InputMode));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không mở được file: " + ex.Message, "Ảnh", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Thêm một point polygon từ click ALT+chuột trái, tính bounding box + tạo overlay polygon với màu tuỳ chọn.
        /// </summary>
        private static void AddCropPointFromClick(
            ImageProcessingNode node,
            System.Windows.Controls.Image image,
            ScaleTransform scale,
            Point clickOnImage,
            Grid imageGrid,
            Button cropButton,
            Action<ImageCropRegion>? onCropClickForIp)
        {
            // e.GetPosition(image) với LayoutTransform đã trả về toạ độ trong local space
            // (tức toạ độ pixel gốc), KHÔNG cần chia cho scale nữa
            var imgX = clickOnImage.X;
            var imgY = clickOnImage.Y;

            if (double.IsNaN(imgX) || double.IsNaN(imgY) || imgX < 0 || imgY < 0) return;

            if (!_activeCropRegion.TryGetValue(node, out var region) || region == null)
            {
                region = new ImageCropRegion();
                // Gán số thứ tự crop (không sort lại khi xoá)
                if (!_cropOrderCounter.TryGetValue(node, out int counter))
                    counter = 0;
                counter++;
                _cropOrderCounter[node] = counter;
                region.Order = counter;
                node.Crops.Add(region);
                _activeCropRegion[node] = region;

                // Đang vẽ crop mới → disable nút crop cho đến khi hoàn thành
                if (cropButton != null)
                    cropButton.IsEnabled = false;

                // Lấy màu stroke hiện tại từ lựa chọn màu
                if (!_activeCropColorIndex.TryGetValue(node, out var colorIdx))
                    colorIdx = 0;
                colorIdx = ((colorIdx % _cropColors.Length) + _cropColors.Length) % _cropColors.Length;
                var baseColor = _cropColors[colorIdx];

                // Lưu màu vào model để persist khi save workflow
                region.ColorHex = $"#{baseColor.R:X2}{baseColor.G:X2}{baseColor.B:X2}";

                // Màu nền trong suốt dựa trên màu đã chọn
                var fillColor = Color.FromArgb(80, baseColor.R, baseColor.G, baseColor.B);

                // Tạo overlay Polygon với stroke + fill theo màu đã chọn
                var polygon = new System.Windows.Shapes.Polygon
                {
                    Stroke = new SolidColorBrush(baseColor),
                    StrokeThickness = 1,
                    Fill = new SolidColorBrush(fillColor),
                    IsHitTestVisible = true,
                    Tag = region,
                    Cursor = Cursors.Hand,
                    ToolTip = "Click để mở Image Processor"
                };

                // Bind điểm polygon theo Crops.Points (toạ độ ảnh gốc, transform xử lý zoom)
                region.Points.CollectionChanged += (s, e) =>
                {
                    polygon.Points.Clear();
                    foreach (var p in region.Points)
                    {
                        polygon.Points.Add(new Point(p.X, p.Y));
                    }

                    if (region.Points.Count > 0)
                    {
                        var minX = region.Points.Min(p => p.X);
                        var maxX = region.Points.Max(p => p.X);
                        var minY = region.Points.Min(p => p.Y);
                        var maxY = region.Points.Max(p => p.Y);
                        region.BoundingBox = new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));

                        // Map theo tỉ lệ gốc 1920x1080 để biết orientation + scale
                        const double baseW = 1920.0;
                        const double baseH = 1080.0;
                        var w = region.BoundingBox.Width;
                        var h = region.BoundingBox.Height;
                        if (w <= 0 || h <= 0)
                        {
                            region.TargetWidth = w;
                            region.TargetHeight = h;
                        }
                        else
                        {
                            var isLandscape = w >= h;
                            if (isLandscape)
                            {
                                var k = h / baseH;
                                region.TargetWidth = baseW * k;
                                region.TargetHeight = baseH * k;
                            }
                            else
                            {
                                var k = w / baseH; // dùng 1080x1920 cho ảnh dọc
                                region.TargetWidth = baseH * k;
                                region.TargetHeight = baseW * k;
                            }
                        }

                        // Tạo thumbnail clip theo polygon (chỉ giữ pixel trong polygon)
                        if (image.Source is BitmapSource bmp && region.Points.Count >= 3)
                        {
                            try
                            {
                                var bx = Math.Max(0, region.BoundingBox.X);
                                var by = Math.Max(0, region.BoundingBox.Y);
                                var bw = Math.Min(region.BoundingBox.Width, bmp.PixelWidth - bx);
                                var bh = Math.Min(region.BoundingBox.Height, bmp.PixelHeight - by);
                                if (bw > 1 && bh > 1)
                                {
                                    int ix = (int)Math.Round(bx), iy = (int)Math.Round(by);
                                    int iw = (int)Math.Round(bw), ih = (int)Math.Round(bh);
                                    var cropped = new CroppedBitmap(bmp, new Int32Rect(ix, iy, iw, ih));

                                    var clipGeo = new StreamGeometry();
                                    using (var ctx = clipGeo.Open())
                                    {
                                        var pts = region.Points;
                                        ctx.BeginFigure(new Point(pts[0].X - bx, pts[0].Y - by), true, true);
                                        for (int pi = 1; pi < pts.Count; pi++)
                                            ctx.LineTo(new Point(pts[pi].X - bx, pts[pi].Y - by), false, false);
                                    }
                                    clipGeo.Freeze();

                                    var dv = new DrawingVisual();
                                    using (var dc = dv.RenderOpen())
                                    {
                                        dc.PushClip(clipGeo);
                                        dc.DrawImage(cropped, new Rect(0, 0, iw, ih));
                                        dc.Pop();
                                    }

                                    var rtb = new RenderTargetBitmap(iw, ih, 96, 96, PixelFormats.Pbgra32);
                                    rtb.Render(dv);
                                    rtb.Freeze();
                                    region.Thumbnail = rtb;
                                }
                            }
                            catch { /* ignore thumbnail errors */ }
                        }
                    }
                };

                if (onCropClickForIp != null)
                {
                    polygon.MouseLeftButtonDown += (s2, e2) =>
                    {
                        onCropClickForIp(region);
                        e2.Handled = true;
                    };
                }

                imageGrid.Children.Add(polygon);
                // Lưu vào map để xoá/ẩn đúng polygon khi crop bị xoá hoặc ẩn
                _polygonMap[region] = polygon;

                // Lắng nghe PropertyChanged của region để đồng bộ polygon
                region.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ImageCropRegion.IsVisible))
                    {
                        polygon.Visibility = region.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else if (e.PropertyName == nameof(ImageCropRegion.IsOutlineOnly))
                    {
                        if (region.IsOutlineOnly)
                        {
                            polygon.Fill = Brushes.Transparent;
                            polygon.StrokeDashArray = new System.Windows.Media.DoubleCollection { 6, 3 };
                        }
                        else
                        {
                            var baseColor = (polygon.Stroke as SolidColorBrush)?.Color ?? Colors.Gold;
                            polygon.Fill = new SolidColorBrush(Color.FromArgb(80, baseColor.R, baseColor.G, baseColor.B));
                            polygon.StrokeDashArray = null;
                        }
                    }
                };
            }

            region.Points.Add(new Point(imgX, imgY));
        }

        /// <summary>
        /// Hoàn thành vùng crop hiện tại: nếu đủ 3 điểm → nối end→start, nếu không thì huỷ.
        /// Đồng thời enable lại nút crop và tạo tên crop theo format Image_{Order}_{DateTime}.
        /// </summary>
        private static bool CompleteActiveCrop(ImageProcessingNode node, Button cropButton)
        {
            if (!_activeCropRegion.TryGetValue(node, out var region) || region == null)
                return false;

            if (region.Points.Count >= 3)
            {
                var first = region.Points[0];
                var last = region.Points[^1];
                if (!first.Equals(last))
                {
                    region.Points.Add(first);
                }

                // Tạo tên crop theo format Image_{Order}_{DateTime}
                var now = DateTime.Now;
                region.CropName = $"Image_{region.Order}_{now:yyyyMMddHHmmss}";
            }
            else
            {
                node.Crops.Remove(region);
            }

            _activeCropRegion[node] = null;
            if (cropButton != null)
                cropButton.IsEnabled = true;
            return true;
        }

        private static (FrameworkElement, Action<BitmapSource?>) BuildImageProcessorColumn(ImageProcessingNode node, IWorkflowEditorHost host)
        {
            // ── State ──
            BitmapSource? currentSource = null;
            BitmapSource? processedBitmap = null;
            bool isVerticalMode = node.IsVerticalMode; // Khôi phục từ node

            // ── Colors (dark theme tokens) ──
            var ipAccent = new SolidColorBrush(Color.FromRgb(0x4f, 0xff, 0xb0));
            var ipAccent2 = new SolidColorBrush(Color.FromRgb(0x00, 0xcf, 0xff));
            var ipSurface2 = new SolidColorBrush(Color.FromRgb(0x18, 0x1c, 0x24));
            var ipBorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x29, 0x32));
            var ipText = new SolidColorBrush(Color.FromRgb(0xdd, 0xe3, 0xef));
            var ipMuted = new SolidColorBrush(Color.FromRgb(0x5a, 0x60, 0x72));
            var ipBg = new SolidColorBrush(Color.FromRgb(0x0a, 0x0c, 0x10));
            var ipSurface = new SolidColorBrush(Color.FromRgb(0x11, 0x13, 0x18));

            // ── Column layout ──
            var columnBorder = new Border
            {
                Background = ipBg,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3a)),
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            var columnDock = new DockPanel();

            // Header – gradient accent bar
            var ipHeader = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x14, 0x18, 0x20),
                    Color.FromRgb(0x0e, 0x12, 0x18),
                    new Point(0, 0), new Point(0, 1)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3a)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 8, 10, 8)
            };
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            // Accent bar bên trái header
            headerStack.Children.Add(new Border
            {
                Width = 3,
                Background = ipAccent,
                CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Stretch
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "IMAGE PROCESSOR",
                Foreground = ipAccent,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });
            ipHeader.Child = headerStack;
            DockPanel.SetDock(ipHeader, Dock.Top);
            columnDock.Children.Add(ipHeader);

            // Buttons at bottom (sẽ được thêm vào contentStack bên dưới để scale cùng Viewbox)

            // Scrollable content
            var contentStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10, 6, 10, 6),
                Width = 240 // Fixed reference width — Viewbox sẽ scale tỉ lệ theo column
            };

            // Helper: tạo custom button template tránh hover WPF mặc định che text
            ControlTemplate MakeDarkButtonTemplate()
            {
                var t = new ControlTemplate(typeof(Button));
                var bd = new FrameworkElementFactory(typeof(Border), "bd");
                bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                bd.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                bd.AppendChild(cp);
                t.VisualTree = bd;
                var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8, "bd"));
                t.Triggers.Add(hover);
                var press = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
                press.Setters.Add(new Setter(UIElement.OpacityProperty, 0.6, "bd"));
                t.Triggers.Add(press);
                return t;
            }

            // Helper: tạo section label với accent bar bên trái
            Border MakeSectionLabel(string text) => new Border
            {
                BorderBrush = ipAccent,
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(6, 2, 0, 2),
                Margin = new Thickness(0, 8, 0, 4),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = ipMuted,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold
                }
            };

            // Helper: tạo info card
            Border MakeCard(UIElement child) => new Border
            {
                Background = ipSurface,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1e, 0x22, 0x2c)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 4),
                Child = child
            };



            // ═══════════ HƯỚNG XUẤT + SCALE (2 cột cùng dòng) ═══════════
            contentStack.Children.Add(MakeSectionLabel("HƯỚNG XUẤT / SCALE"));

            // isVerticalMode đã được khai báo ở trên (dòng 1416)

            // Toggle button với custom template
            var btnOrientation = new Button
            {
                Cursor = Cursors.Hand,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(0, 6, 0, 6),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            // Custom template
            var btnTemplate = new ControlTemplate(typeof(Button));
            var bdFactory = new FrameworkElementFactory(typeof(Border), "btnBorder");
            bdFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bdFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bdFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            bdFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            bdFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bdFactory.AppendChild(cpFactory);
            btnTemplate.VisualTree = bdFactory;
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8, "btnBorder"));
            btnTemplate.Triggers.Add(hoverTrigger);
            var pressTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.6, "btnBorder"));
            btnTemplate.Triggers.Add(pressTrigger);
            btnOrientation.Template = btnTemplate;
            btnOrientation.MouseLeftButtonDown += (s, e) => e.Handled = true;

            void UpdateOrientationButton()
            {
                if (isVerticalMode)
                {
                    btnOrientation.Content = "📱 Dọc 9:16";
                    btnOrientation.Background = new SolidColorBrush(Color.FromArgb(40, 0x4f, 0xff, 0xb0));
                    btnOrientation.Foreground = ipAccent;
                    btnOrientation.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 0x4f, 0xff, 0xb0));
                }
                else
                {
                    btnOrientation.Content = "🖥 Ngang 16:9";
                    btnOrientation.Background = ipSurface2;
                    btnOrientation.Foreground = ipText;
                    btnOrientation.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3a));
                }
            }
            UpdateOrientationButton();

            // Scale cycling button (thay ComboBox để tránh hover trắng che text)
            int currentScaleIndex = 0;
            string[] scaleLabels = { "1×", "2×", "3×", "4×" };
            int[] scaleValues = { 1, 2, 3, 4 };
            var btnScale = new Button
            {
                Content = scaleLabels[0],
                Background = ipSurface2,
                Foreground = ipText,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3a)),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(0, 6, 0, 6),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            btnScale.Template = MakeDarkButtonTemplate();
            btnScale.MouseLeftButtonDown += (s, e) => e.Handled = true;

            // Grid 2 cột: Hướng xuất | Scale
            var orientScaleGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            orientScaleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            orientScaleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) }); // gap
            orientScaleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(btnOrientation, 0);
            Grid.SetColumn(btnScale, 2);
            orientScaleGrid.Children.Add(btnOrientation);
            orientScaleGrid.Children.Add(btnScale);
            contentStack.Children.Add(orientScaleGrid);

            // Ratio & standard size info
            var txtStdSize = new TextBlock
            {
                Text = "Chuẩn: 1920×1080",
                Foreground = ipMuted,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9
            };
            var txtRatioInfo = new TextBlock
            {
                Foreground = ipAccent2,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Margin = new Thickness(0, 2, 0, 0)
            };
            var infoStack = new StackPanel();
            infoStack.Children.Add(txtStdSize);
            infoStack.Children.Add(txtRatioInfo);
            contentStack.Children.Add(MakeCard(infoStack));


            // Helpers
            int GetScale()
            {
                return scaleValues[currentScaleIndex];
            }



            // ═══════════ PREVIEW ═══════════
            contentStack.Children.Add(MakeSectionLabel("PREVIEW"));

            var txtImgInfo = new TextBlock
            {
                Foreground = ipMuted,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Margin = new Thickness(0, 0, 0, 2)
            };
            contentStack.Children.Add(txtImgInfo);

            var txtPreviewHint = new TextBlock
            {
                Text = "Nhấn vào ảnh crop để xử lý",
                Foreground = ipMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10
            };
            var imgPreview = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                MaxHeight = 240,
                Margin = new Thickness(2)
            };
            RenderOptions.SetBitmapScalingMode(imgPreview, BitmapScalingMode.HighQuality);

            // Checkerboard background cho preview (phân biệt vùng đen của ảnh vs nền)
            var checkerSize = 8.0;
            var lightSquare = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x38));
            var darkSquare = new SolidColorBrush(Color.FromRgb(0x1e, 0x22, 0x2c));
            var checkerGroup = new DrawingGroup();
            checkerGroup.Children.Add(new GeometryDrawing(darkSquare, null,
                new RectangleGeometry(new Rect(0, 0, checkerSize * 2, checkerSize * 2))));
            checkerGroup.Children.Add(new GeometryDrawing(lightSquare, null,
                new RectangleGeometry(new Rect(0, 0, checkerSize, checkerSize))));
            checkerGroup.Children.Add(new GeometryDrawing(lightSquare, null,
                new RectangleGeometry(new Rect(checkerSize, checkerSize, checkerSize, checkerSize))));
            var checkerBrush = new DrawingBrush(checkerGroup)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, checkerSize * 2, checkerSize * 2),
                ViewportUnits = BrushMappingMode.Absolute
            };

            var previewGrid = new Grid { MinHeight = 80 };
            previewGrid.Children.Add(txtPreviewHint);
            previewGrid.Children.Add(imgPreview);
            contentStack.Children.Add(new Border
            {
                Background = checkerBrush,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3a)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 4),
                Child = previewGrid
            });

            var panelMeta = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };
            contentStack.Children.Add(panelMeta);

            void AddMetaTag(string text)
            {
                panelMeta.Children.Add(new Border
                {
                    Background = ipSurface,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x1e, 0x22, 0x2c)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5, 2, 5, 2),
                    Margin = new Thickness(0, 0, 4, 3),
                    Child = new TextBlock
                    {
                        Text = text,
                        Foreground = ipAccent2,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 8
                    }
                });
            }

            // ═══════════ SỐ LẦN GỬI ═══════════
            contentStack.Children.Add(MakeSectionLabel("SỐ LẦN GỬI"));

            var comboPromptSize = new ComboBox
            {
                Background = ipSurface,
                Foreground = ipText,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1e, 0x22, 0x2c)),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Padding = new Thickness(6, 4, 6, 4),
                Height = 32,
                Margin = new Thickness(0, 0, 0, 4)
            };
            comboPromptSize.Items.Add("1");
            comboPromptSize.Items.Add("2");
            comboPromptSize.Items.Add("3");
            comboPromptSize.Items.Add("4");
            comboPromptSize.SelectedIndex = Math.Max(0, Math.Min(3, node.PromptSize - 1)); // Default 4 (index 3)
            comboPromptSize.SelectionChanged += (s, e) =>
            {
                if (comboPromptSize.SelectedItem is string str && int.TryParse(str, out var val) && val >= 1 && val <= 4)
                {
                    node.PromptSize = val;
                }
            };
            contentStack.Children.Add(comboPromptSize);

            // ═══════════ PROMPT ═══════════
            contentStack.Children.Add(MakeSectionLabel("PROMPT"));

            var txtPrompt = new TextBox
            {
                Background = ipSurface,
                Foreground = ipText,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1e, 0x22, 0x2c)),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Padding = new Thickness(6, 4, 6, 4),
                Height = 120,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                CaretBrush = ipAccent,
                Margin = new Thickness(0, 0, 0, 4)
            };
            // Bind prompt text với node.ProcessorPrompt
            txtPrompt.Text = node.ProcessorPrompt ?? string.Empty;
            txtPrompt.TextChanged += (s, e) =>
            {
                node.ProcessorPrompt = txtPrompt.Text ?? string.Empty;
            };
            contentStack.Children.Add(txtPrompt);

            // ═══════════ ACTION BUTTONS (2 cột cùng dòng) ═══════════
            var btnProcess = new Button
            {
                Content = "✨ Lưu ảnh",
                Style = Application.Current.TryFindResource("SuccessButton") as Style,
                FontWeight = FontWeights.Bold,
                Width = 90,
                Height = 30,
                FontSize = 11,
                Padding = new Thickness(0, 7, 0, 7),
                Cursor = Cursors.Hand
            };
            btnProcess.Template = MakeDarkButtonTemplate();
            btnProcess.MouseLeftButtonDown += (s, e) => e.Handled = true;

            var btnStart = new Button
            {
                Content = "▶ Bắt đầu",
                Style = Application.Current.TryFindResource("PrimaryButton") as Style,
                FontWeight = FontWeights.Bold,
                Width = 90,
                Height = 30,
                FontSize = 11,
                Padding = new Thickness(0, 7, 0, 7),
                Cursor = Cursors.Hand
            };
            btnStart.Template = MakeDarkButtonTemplate();
            btnStart.MouseLeftButtonDown += (s, e) => e.Handled = true;

            var actionGrid = new Grid { Margin = new Thickness(0, 8, 0, 4) };
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(btnProcess, 0);
            Grid.SetColumn(btnStart, 2);
            actionGrid.Children.Add(btnProcess);
            actionGrid.Children.Add(btnStart);
            contentStack.Children.Add(actionGrid);

            // Viewbox + ScrollViewer: scale nội dung tỉ lệ theo column width
            // contentStack có Width cố định = 240, Viewbox scale uniform để vừa column
            var ipViewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Child = contentStack
            };
            var ipScroll = new ScrollViewer
            {
                Content = ipViewbox,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            columnDock.Children.Add(ipScroll);
            columnBorder.Child = columnDock;

            // ═══════════ Processing logic ═══════════
            async System.Threading.Tasks.Task ProcessAsync()
            {
                if (currentSource == null)
                {
                    txtPreviewHint.Visibility = Visibility.Visible;
                    txtPreviewHint.Text = "Nhấn vào ảnh crop để xử lý";
                    imgPreview.Source = null;
                    panelMeta.Children.Clear();
                    txtImgInfo.Text = "";
                    return;
                }

                bool isVert = isVerticalMode;
                int sc = GetScale();

                txtPreviewHint.Visibility = Visibility.Visible;
                txtPreviewHint.Text = "Đang xử lý...";
                imgPreview.Source = null;
                panelMeta.Children.Clear();

                var src = currentSource;
                var result = await System.Threading.Tasks.Task.Run(() =>
                    ImageProcessorHelper.Render(src, isVert, sc));

                if (result == null)
                {
                    txtPreviewHint.Text = "Lỗi xử lý ảnh";
                    return;
                }

                processedBitmap = result.Bitmap;
                txtPreviewHint.Visibility = Visibility.Collapsed;
                imgPreview.Source = result.Bitmap;
                txtImgInfo.Text = $"{result.OutW}×{result.OutH}";

                AddMetaTag($"Gốc: {result.SrcW}×{result.SrcH}");
                AddMetaTag($"Xuất: {result.OutW}×{result.OutH}");
                AddMetaTag($"Scale: {sc}×");
                AddMetaTag(isVert ? "Dọc" : "Ngang");
                AddMetaTag($"Tỉ lệ: {result.RatioW:F4} × {result.RatioH:F4}");
                AddMetaTag($"Pad: ±{result.PadX}/{result.PadY}px");
                txtRatioInfo.Text = $"Tỉ lệ: {result.RatioW:F4} × {result.RatioH:F4}";
            }

            // ═══════════ Events ═══════════
            btnOrientation.Click += (s, e) =>
            {
                e.Handled = true;
                isVerticalMode = !isVerticalMode;
                node.IsVerticalMode = isVerticalMode; // Lưu vào node
                UpdateOrientationButton();
                txtStdSize.Text = isVerticalMode ? "Chuẩn: 1080×1920" : "Chuẩn: 1920×1080";
                if (currentSource != null) _ = ProcessAsync();
            };

            btnScale.Click += (s, e) =>
            {
                e.Handled = true;
                currentScaleIndex = (currentScaleIndex + 1) % scaleLabels.Length;
                btnScale.Content = scaleLabels[currentScaleIndex];
                if (currentSource != null) _ = ProcessAsync();
            };

            btnProcess.Click += (s, e) =>
            {
                e.Handled = true;
                if (processedBitmap == null)
                {
                    MessageBox.Show("Chưa có ảnh đã xử lý.", "Image Processor",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var dlg = new SaveFileDialog
                {
                    Title = "Lưu ảnh đã xử lý",
                    Filter = "PNG|*.png|JPEG|*.jpg;*.jpeg",
                    FileName = "processed_image.png"
                };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        ImageProcessorHelper.SaveBitmap(processedBitmap, dlg.FileName);
                        MessageBox.Show("Đã lưu!", "Image Processor",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi: " + ex.Message, "Image Processor",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };

            btnStart.Click += async (s, e) =>
            {
                e.Handled = true;
                if (processedBitmap == null)
                {
                    MessageBox.Show("Chưa có ảnh đã xử lý.", "Image Processor",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Lưu base64 của processedBitmap vào cropBase64 output
                var b64 = await System.Threading.Tasks.Task.Run(() =>
                    ImageProcessorHelper.ToBase64(processedBitmap));

                // Set output cropBase64 (sẽ được executor kiểm tra SkipOutputs)
                var cropBase64Port = node.DynamicOutputs?.FirstOrDefault(o =>
                    string.Equals(o.Key, "cropBase64", StringComparison.OrdinalIgnoreCase));
                if (cropBase64Port != null && !node.SkipOutputs.Contains("cropBase64"))
                {
                    cropBase64Port.UserValueOverride = b64;
                }

                // Set executionId output: mỗi lần nhấn Bắt đầu tạo id mới
                var execId = Guid.NewGuid().ToString("N");
                node.LastExecutionId = execId;
                var execIdPort = node.DynamicOutputs?.FirstOrDefault(o =>
                    string.Equals(o.Key, "executionId", StringComparison.OrdinalIgnoreCase));
                if (execIdPort != null && !node.SkipOutputs.Contains("executionId"))
                {
                    execIdPort.UserValueOverride = execId;
                }

                // Lấy crop region hiện tại và set cropName, cropWidth, cropHeight
                if (_currentCropRegionForIp.TryGetValue(node, out var cropRegion) && cropRegion != null)
                {
                    // Set cropName
                    var cropNamePort = node.DynamicOutputs?.FirstOrDefault(o =>
                        string.Equals(o.Key, "cropName", StringComparison.OrdinalIgnoreCase));
                    if (cropNamePort != null && !node.SkipOutputs.Contains("cropName"))
                    {
                        cropNamePort.UserValueOverride = cropRegion.CropName;
                    }

                    // Set cropWidth và cropHeight từ processedBitmap
                    var cropWidthPort = node.DynamicOutputs?.FirstOrDefault(o =>
                        string.Equals(o.Key, "cropWidth", StringComparison.OrdinalIgnoreCase));
                    if (cropWidthPort != null && !node.SkipOutputs.Contains("cropWidth"))
                    {
                        cropWidthPort.UserValueOverride = processedBitmap.PixelWidth.ToString();
                    }

                    var cropHeightPort = node.DynamicOutputs?.FirstOrDefault(o =>
                        string.Equals(o.Key, "cropHeight", StringComparison.OrdinalIgnoreCase));
                    if (cropHeightPort != null && !node.SkipOutputs.Contains("cropHeight"))
                    {
                        cropHeightPort.UserValueOverride = processedBitmap.PixelHeight.ToString();
                    }
                }

                // Sau khi set output cho node image, trigger chạy workflow giống nút Bắt đầu trên toolbar
                try
                {
                    var vm = host.ViewModel;
                    if (vm != null)
                    {
                        var vmType = vm.GetType();
                        // Ưu tiên gọi trực tiếp method StartTest (async)
                        var startTestMethod = vmType.GetMethod("StartTest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (startTestMethod != null)
                        {
                            _ = btnStart.Dispatcher.InvokeAsync(async () =>
                            {
                                try
                                {
                                    if (startTestMethod.Invoke(vm, null) is System.Threading.Tasks.Task t)
                                    {
                                        await t;
                                    }

                                    // Sau khi workflow chạy xong, tự động nạp ảnh render từ node đã cấu hình (nếu có)
                                    await RefreshRenderedImagesFromRenderNodeAsync(node, host);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"ImageProcessor StartWorkflow error (StartTest): {ex.Message}");
                                }
                            }, System.Windows.Threading.DispatcherPriority.Normal);
                        }
                        else
                        {
                            // Fallback: dùng StartTestCommand nếu có
                            var commandProp = vmType.GetProperty("StartTestCommand", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (commandProp?.GetValue(vm) is System.Windows.Input.ICommand cmd && cmd.CanExecute(null))
                            {
                                _ = btnStart.Dispatcher.InvokeAsync(async () =>
                                {
                                    try
                                    {
                                        cmd.Execute(null);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"ImageProcessor StartWorkflow error (StartTestCommand): {ex.Message}");
                                    }

                                    try
                                    {
                                        // Sau khi workflow kết thúc, thử nạp ảnh render
                                        await RefreshRenderedImagesFromRenderNodeAsync(node, host);
                                    }
                                    catch (Exception ex2)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"ImageProcessor RefreshRenderedImages error: {ex2.Message}");
                                    }
                                }, System.Windows.Threading.DispatcherPriority.Normal);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ImageProcessor StartWorkflow dispatch error: {ex.Message}");
                }
            };

            // Set image action (exposed to caller)
            Action<BitmapSource?> setImage = (bmp) =>
            {
                currentSource = bmp;
                _ = ProcessAsync();
            };

            return (columnBorder, setImage);
        }

        private static Border CreateSideMenu(string label)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Padding = new Thickness(0),
            };

            var g = new Grid();
            var dragLayer = new Border { Background = Brushes.Transparent, IsHitTestVisible = true };
            g.Children.Add(dragLayer);
            g.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 11,
                Opacity = 0.7,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            });
            b.Child = g;
            return b;
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
                CacheMode = null
            };
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

        private static Brush GetTitleBrush(ImageProcessingNode node)
        {
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

        private static void OpenNodeDialog(ImageProcessingNode node, IWorkflowEditorHost host, Window? ownerWindow)
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
                var dialog = new ImageProcessingNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
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

        /// <summary>
        /// Khôi phục polygon overlay trên imageGrid cho tất cả crop region đã được load từ workflow.
        /// Gọi sau khi image.Source được set (có ảnh thật).
        /// </summary>
        private static void RestorePolygonsForNode(
            ImageProcessingNode node,
            System.Windows.Controls.Image image,
            Grid imageGrid,
            Action<ImageCropRegion>? onCropClickForIp)
        {
            if (node.Crops.Count == 0) return;

            for (int i = 0; i < node.Crops.Count; i++)
            {
                var region = node.Crops[i];
                // Bỏ qua nếu polygon đã được tạo (user đang vẽ hoặc đã restore)
                if (_polygonMap.ContainsKey(region)) continue;
                if (region.Points.Count == 0) continue;

                // Lấy màu từ ColorHex đã lưu trong model (nếu có), fallback theo thứ tự
                Color baseColor;
                try
                {
                    baseColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString(region.ColorHex);
                }
                catch
                {
                    baseColor = _cropColors[i % _cropColors.Length];
                }
                var fillColor = Color.FromArgb(80, baseColor.R, baseColor.G, baseColor.B);

                var polygon = new System.Windows.Shapes.Polygon
                {
                    Stroke = new SolidColorBrush(baseColor),
                    StrokeThickness = 1,
                    Fill = new SolidColorBrush(fillColor),
                    IsHitTestVisible = true,
                    Tag = region,
                    Cursor = Cursors.Hand,
                    ToolTip = "Click để mở Image Processor"
                };

                // Set points hiện tại
                foreach (var p in region.Points)
                    polygon.Points.Add(new Point(p.X, p.Y));

                // Lắng nghe thay đổi điểm tương lai (nếu user edit thêm)
                var capturedRegion = region;
                var capturedPolygon = polygon;
                capturedRegion.Points.CollectionChanged += (s, e) =>
                {
                    capturedPolygon.Points.Clear();
                    foreach (var p in capturedRegion.Points)
                        capturedPolygon.Points.Add(new Point(p.X, p.Y));
                };

                // Áp dụng trạng thái IsOutlineOnly
                if (region.IsOutlineOnly)
                {
                    polygon.Fill = Brushes.Transparent;
                    polygon.StrokeDashArray = new System.Windows.Media.DoubleCollection { 6, 3 };
                }

                // Áp dụng trạng thái IsVisible
                polygon.Visibility = region.IsVisible ? Visibility.Visible : Visibility.Collapsed;

                // Đồng bộ PropertyChanged của region
                region.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ImageCropRegion.IsVisible))
                        capturedPolygon.Visibility = capturedRegion.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    else if (e.PropertyName == nameof(ImageCropRegion.IsOutlineOnly))
                    {
                        if (capturedRegion.IsOutlineOnly)
                        {
                            capturedPolygon.Fill = Brushes.Transparent;
                            capturedPolygon.StrokeDashArray = new System.Windows.Media.DoubleCollection { 6, 3 };
                        }
                        else
                        {
                            var c = (capturedPolygon.Stroke as SolidColorBrush)?.Color ?? Colors.Gold;
                            capturedPolygon.Fill = new SolidColorBrush(Color.FromArgb(80, c.R, c.G, c.B));
                            capturedPolygon.StrokeDashArray = null;
                        }
                    }
                };

                if (onCropClickForIp != null)
                {
                    polygon.MouseLeftButtonDown += (s2, e2) =>
                    {
                        onCropClickForIp(region);
                        e2.Handled = true;
                    };
                }

                imageGrid.Children.Add(polygon);
                _polygonMap[region] = polygon;

                // Cập nhật color index để crop tiếp theo dùng màu kế tiếp
                _activeCropColorIndex[node] = (i + 1) % _cropColors.Length;
            }
        }

        /// <summary>
        /// Tái tạo thumbnail cho tất cả crop region từ ảnh đang hiện.
        /// Gọi sau khi ảnh đã load và có kích thước thực tế.
        /// </summary>
        private static void RegenerateThumbnails(
            ImageProcessingNode node,
            System.Windows.Controls.Image image)
        {
            if (image.Source is not BitmapSource bmp) return;

            foreach (var region in node.Crops)
            {
                if (region.Points.Count < 3) continue;
                try
                {
                    var minX = region.Points.Min(p => p.X);
                    var maxX = region.Points.Max(p => p.X);
                    var minY = region.Points.Min(p => p.Y);
                    var maxY = region.Points.Max(p => p.Y);

                    var bx = Math.Max(0, minX);
                    var by = Math.Max(0, minY);
                    var bw = Math.Min(maxX - minX, bmp.PixelWidth - bx);
                    var bh = Math.Min(maxY - minY, bmp.PixelHeight - by);
                    if (bw < 2 || bh < 2) continue;

                    int ix = (int)Math.Round(bx), iy = (int)Math.Round(by);
                    int iw = (int)Math.Round(bw), ih = (int)Math.Round(bh);
                    if (ix < 0 || iy < 0 || ix + iw > bmp.PixelWidth || iy + ih > bmp.PixelHeight) continue;

                    var cropped = new CroppedBitmap(bmp, new Int32Rect(ix, iy, iw, ih));

                    var clipGeo = new StreamGeometry();
                    using (var ctx = clipGeo.Open())
                    {
                        var pts = region.Points;
                        ctx.BeginFigure(new Point(pts[0].X - bx, pts[0].Y - by), true, true);
                        for (int pi = 1; pi < pts.Count; pi++)
                            ctx.LineTo(new Point(pts[pi].X - bx, pts[pi].Y - by), false, false);
                    }
                    clipGeo.Freeze();

                    var dv = new DrawingVisual();
                    using (var dc = dv.RenderOpen())
                    {
                        dc.PushClip(clipGeo);
                        dc.DrawImage(cropped, new Rect(0, 0, iw, ih));
                        dc.Pop();
                    }

                    var rtb = new RenderTargetBitmap(iw, ih, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(dv);
                    rtb.Freeze();
                    region.Thumbnail = rtb;
                }
                catch { /* bỏ qua lỗi thumbnail */ }
            }
        }

        private static async System.Threading.Tasks.Task UpdatePreviewAsync(
            ImageProcessingNode node,
            IWorkflowEditorHost host,
            System.Windows.Controls.Image image,
            TextBlock placeholder,
            ScaleTransform scale,
            Grid? imageGrid = null,
            ScrollViewer? scrollViewer = null,
            TextBlock? imageTitleTextBlock = null,
            Action<ImageCropRegion>? onCropClickForIp = null)
        {
            try
            {
                var version = NextPreviewVersion(node);
                placeholder.Visibility = Visibility.Visible;
                placeholder.Text = "Đang tải ảnh...";
                image.Source = null;

                scale.ScaleX = 1.0;
                scale.ScaleY = 1.0;

                string resolved = string.Empty;
                BitmapSource? bitmap = null;

                // Cập nhật title ban đầu
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (imageTitleTextBlock != null)
                        imageTitleTextBlock.Text = "Đang tải ảnh...";
                });

                if (node.InputMode == ImageInputMode.Base64)
                {
                    resolved = ResolveFromNodeIfAny(host, node.ImageBase64SourceNodeId, node.ImageBase64SourceOutputKey)
                               ?? node.ImageBase64;
                    bitmap = await System.Threading.Tasks.Task.Run(() => CreateBitmapFromBase64(resolved));
                    if (bitmap == null) throw new InvalidOperationException("Base64 không hợp lệ hoặc không decode được.");
                }
                else
                {
                    resolved = ResolveFromNodeIfAny(host, node.ImageUrlSourceNodeId, node.ImageUrlSourceOutputKey)
                               ?? node.ImageUrl;
                    resolved = resolved?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(resolved))
                        throw new InvalidOperationException("Chưa có link/file ảnh.");

                    // Nếu là URL online thì cập nhật placeholder để hiển thị đang tải
                    bool isUrl = resolved.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                 resolved.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                    if (isUrl)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            placeholder.Text = "Đang tải ảnh từ URL...";
                            placeholder.Visibility = Visibility.Visible;
                        });
                    }

                    bitmap = await System.Threading.Tasks.Task.Run(() => CreateBitmapFromUrlOrFile(resolved));
                    if (bitmap == null) throw new InvalidOperationException("Không tải được ảnh từ link/file.");
                }

                // Cập nhật title với tên file hoặc URL
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (imageTitleTextBlock != null && !string.IsNullOrWhiteSpace(resolved))
                    {
                        string displayTitle;
                        if (node.InputMode == ImageInputMode.Base64)
                        {
                            // Base64: hiển thị "Base64 Image" hoặc tên file nếu có trong data URI
                            if (resolved.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                            {
                                var commaIdx = resolved.IndexOf(',');
                                if (commaIdx > 0)
                                {
                                    var mimePart = resolved.Substring(5, commaIdx - 5);
                                    if (mimePart.Contains("filename="))
                                    {
                                        var filenameMatch = System.Text.RegularExpressions.Regex.Match(mimePart, @"filename=([^;]+)");
                                        if (filenameMatch.Success)
                                            displayTitle = filenameMatch.Groups[1].Value.Trim('"', '\'');
                                        else
                                            displayTitle = "Base64 Image";
                                    }
                                    else
                                        displayTitle = "Base64 Image";
                                }
                                else
                                    displayTitle = "Base64 Image";
                            }
                            else
                                displayTitle = "Base64 Image";
                        }
                        else
                        {
                            // URL hoặc file path
                            displayTitle = resolved;
                            try
                            {
                                if (File.Exists(resolved))
                                {
                                    displayTitle = System.IO.Path.GetFileName(resolved);
                                }
                                else if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri))
                                {
                                    displayTitle = uri.AbsoluteUri;
                                }
                            }
                            catch { /* giữ nguyên resolved nếu có lỗi */ }
                        }
                        imageTitleTextBlock.Text = displayTitle;
                    }
                });

                if (!IsLatestPreview(node, version)) return;
                var loadedBitmap = bitmap;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    image.Source = loadedBitmap;
                    placeholder.Visibility = Visibility.Collapsed;

                    // Khôi phục polygon cho các crop region đã được load từ workflow
                    if (imageGrid != null)
                        RestorePolygonsForNode(node, image, imageGrid, onCropClickForIp);

                    double imageWidth = loadedBitmap.PixelWidth;
                    double imageHeight = loadedBitmap.PixelHeight;
                    if (imageWidth <= 0 || imageHeight <= 0) return;

                    // Tính scale từ kích thước node (pattern giống MediaGallery)
                    var borderW = Math.Max(node.Width, node.Border?.MinWidth ?? 800);
                    var borderH = Math.Max(node.Height, node.Border?.MinHeight ?? 400);
                    double availW = borderW - 56 - 190;
                    double availH = borderH - 30;

                    double sX = availW / imageWidth;
                    double sY = availH / imageHeight;
                    double initialScale = Math.Min(sX, sY);
                    if (initialScale < 0.1) initialScale = 0.1;
                    if (initialScale > 1.0) initialScale = 1.0;

                    // Set Width/Height/Scale cùng lúc trong BeginInvoke (giống MediaGallery)
                    image.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        image.Width = imageWidth;
                        image.Height = imageHeight;
                        scale.ScaleX = initialScale;
                        scale.ScaleY = initialScale;

                        if (scrollViewer == null) return;
                        scrollViewer.UpdateLayout();

                        if (scrollViewer.ExtentWidth > scrollViewer.ViewportWidth)
                            scrollViewer.ScrollToHorizontalOffset((scrollViewer.ExtentWidth - scrollViewer.ViewportWidth) / 2);
                        if (scrollViewer.ExtentHeight > scrollViewer.ViewportHeight)
                            scrollViewer.ScrollToVerticalOffset((scrollViewer.ExtentHeight - scrollViewer.ViewportHeight) / 2);

                        // Regenerate thumbnails sau khi image đã có kích thước thực tế
                        if (imageGrid != null)
                            RegenerateThumbnails(node, image);
                    }), DispatcherPriority.Loaded);
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    image.Source = null;
                    placeholder.Visibility = Visibility.Visible;
                    placeholder.Text = "Không hiển thị được ảnh: " + ex.Message;
                    if (imageTitleTextBlock != null)
                        imageTitleTextBlock.Text = "Lỗi: " + ex.Message;
                });
            }
        }

        /// <summary>
        /// Đọc output từ Node render ảnh (RenderNodeId + RenderNodeOutputKey) và map
        /// thành ảnh render tương ứng cho từng crop (theo thứ tự Order tăng dần).
        /// Hỗ trợ:
        /// - Chuỗi đơn: path local hoặc URL online, hoặc base64 → áp dụng cho crop đầu tiên.
        /// - JSON array chuỗi: ["path1","path2",...] → map theo thứ tự crop 1,2,3...
        /// </summary>
        private static async System.Threading.Tasks.Task RefreshRenderedImagesFromRenderNodeAsync(
            ImageProcessingNode node,
            IWorkflowEditorHost host)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(node.RenderNodeId) ||
                    string.IsNullOrWhiteSpace(node.RenderNodeOutputKey))
                {
                    MessageBox.Show("Chưa cấu hình Node render ảnh + Key trong dialog Xử lý ảnh.",
                        "Image Processor", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var raw = ResolveFromNodeIfAny(host, node.RenderNodeId, node.RenderNodeOutputKey);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    MessageBox.Show("Node render ảnh chưa có dữ liệu output (hoặc không đọc được).",
                        "Image Processor", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                raw = raw.Trim();
                List<string> list;

                // Thử parse JSON array chuẩn trước, fallback sang chuỗi đơn hoặc array không có dấu nháy
                if (raw.StartsWith("["))
                {
                    try
                    {
                        list = JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
                    }
                    catch
                    {
                        // Fallback: thử parse theo format [path1, path2, ...] không có dấu nháy
                        var inner = raw.Trim();
                        if (inner.StartsWith("[")) inner = inner.Substring(1);
                        if (inner.EndsWith("]")) inner = inner.Substring(0, inner.Length - 1);
                        var parts = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(p => p.Trim().Trim('"'))
                                         .Where(p => !string.IsNullOrWhiteSpace(p))
                                         .ToList();
                        list = parts.Count > 0 ? parts : new List<string> { raw };
                    }
                }
                else
                {
                    list = new List<string> { raw };
                }

                if (list.Count == 0)
                {
                    MessageBox.Show("Output của Node render ảnh rỗng.",
                        "Image Processor", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Xác định crop đang active (crop mà user đã click trước khi nhấn Bắt đầu)
                ImageCropRegion? activeCrop = null;
                _currentCropRegionForIp.TryGetValue(node, out activeCrop);

                // Nếu không có crop active, fallback lấy crop đầu tiên
                if (activeCrop == null && node.Crops.Count > 0)
                {
                    activeCrop = node.Crops.OrderBy(c => c.Order).FirstOrDefault();
                }

                if (activeCrop == null)
                {
                    MessageBox.Show("Không tìm thấy vùng crop để gán ảnh render.",
                        "Image Processor", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Load ảnh trong background, gán TẤT CẢ ảnh render vào crop đang active
                // Không clear ảnh cũ của crop khác — mỗi crop tích luỹ ảnh qua các lần nhấn Bắt đầu
                var targetCrop = activeCrop;
                await System.Threading.Tasks.Task.Run(() =>
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var entry = list[i];
                        if (string.IsNullOrWhiteSpace(entry)) continue;
                        entry = entry.Trim();

                        // Thử load như URL/path trước, nếu fail thì thử base64
                        BitmapImage? bmp = CreateBitmapFromUrlOrFile(entry);
                        if (bmp == null)
                        {
                            bmp = CreateBitmapFromBase64(entry);
                        }

                        if (bmp == null) continue;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            targetCrop.RenderedImages.Add(bmp);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không nạp được ảnh render từ node: " + ex.Message,
                    "Image Processor", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string? ResolveFromNodeIfAny(IWorkflowEditorHost host, string? nodeId, string? key)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key)) return null;
            var src = host.ViewModel?.Nodes?.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (src == null) return null;
            var value = NodeDataPanelService.ResolveDynamicValueByKey(src, key);
            if (string.IsNullOrWhiteSpace(value) || value == "—") return null;
            return value;
        }

        private static BitmapImage? CreateBitmapFromUrlOrFile(string value)
        {
            try
            {
                value = value.Trim();
                if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    value = new Uri(value).LocalPath;
                }

                if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(value, UriKind.Absolute);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = uri;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }

                // Assume local path
                if (!File.Exists(value)) return null;
                using var fs = File.OpenRead(value);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = fs;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage? CreateBitmapFromBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;
            base64 = base64.Trim();
            // Strip data URI prefix if present
            var comma = base64.IndexOf(',');
            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
                base64 = base64.Substring(comma + 1);

            // Remove whitespace/newlines
            base64 = new string(base64.Where(c => !char.IsWhiteSpace(c)).ToArray());
            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64); }
            catch { return null; }

            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static int NextPreviewVersion(ImageProcessingNode node)
        {
            lock (_previewVersion)
            {
                if (!_previewVersion.TryGetValue(node, out var v)) v = 0;
                v++;
                _previewVersion[node] = v;
                return v;
            }
        }

        private static bool IsLatestPreview(ImageProcessingNode node, int version)
        {
            lock (_previewVersion)
            {
                return _previewVersion.TryGetValue(node, out var v) && v == version;
            }
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

    /// <summary>Converter: int Order → "#N" label (ví dụ 2 → "#2").</summary>
    public sealed class IntToHashLabelConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int n)
                return $"#{n}";
            return $"#{value}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
