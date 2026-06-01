using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.NodeControls
{
    public static class ScreenCaptureNodeControl
    {
        // ── Giới hạn kích thước node ────────────────────────────────────────
        private const double MinNodeW      = 160;   // chiều rộng tối thiểu khi có ảnh
        private const double MinNodeH      = 80;    // chiều cao tối thiểu khi có ảnh
        private const double ImagePadding  = 5;     // khoảng cách ảnh cách viền node
        private const double ButtonHeight  = 32;
        private const double ButtonBarH    = ButtonHeight + 10; // margin top/bottom button bar
        // Ngưỡng width để thu gọn button thành icon
        private const double CompactWidth  = 200;

        public static Border CreateBorder(ScreenCaptureNode node, Window ownerWindow, IWorkflowEditorHost? host = null)
        {
            // ── 1. PREVIEW IMAGE ────────────────────────────────────────────
            var previewImage = new Image
            {
                Stretch             = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(ImagePadding)
            };
            // Fix ảnh mờ: dùng NearestNeighbor để ảnh pixel-perfect khi scale nhỏ,
            // HighQuality khi scale lớn hơn kích thước gốc
            RenderOptions.SetBitmapScalingMode(previewImage, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(previewImage, EdgeMode.Aliased);

            // Zoom nhẹ khi hover (không resize node)
            previewImage.RenderTransformOrigin = new Point(0.5, 0.5);
            var scaleTransform = new ScaleTransform(1, 1);
            previewImage.RenderTransform = scaleTransform;
            previewImage.MouseEnter += (s, e) => { scaleTransform.ScaleX = 1.06; scaleTransform.ScaleY = 1.06; };
            previewImage.MouseLeave += (s, e) => { scaleTransform.ScaleX = 1.0;  scaleTransform.ScaleY = 1.0; };

            // ── 2. PLACEHOLDER (icon khi chưa có ảnh) ───────────────────────
            var placeholderPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(12)
            };
            var placeholderIcon = new TextBlock
            {
                Text                = "📸",
                FontSize            = 32,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var placeholderText = new TextBlock
            {
                Text                = "Chưa có ảnh",
                FontSize            = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 4, 0, 0)
            };
            placeholderIcon.SetResourceReference(TextBlock.ForegroundProperty, $"TextOn{node.ColorKey}Brush");
            placeholderText.SetResourceReference(TextBlock.ForegroundProperty, $"TextOn{node.ColorKey}Brush");
            placeholderPanel.Children.Add(placeholderIcon);
            placeholderPanel.Children.Add(placeholderText);

            // ── 3. PREVIEW BORDER — bo góc top-left/top-right khớp node ─────
            // CornerRadius top = 8 (node là 10, trừ border 2px), bottom = 0 (tiếp giáp button bar)
            var previewBorder = new Border
            {
                Background          = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
                CornerRadius        = new CornerRadius(8, 8, 0, 0),
                // Margin: cách viền node 5px ở top/left/right, 0 ở bottom (button bar sát)
                Margin              = new Thickness(ImagePadding, ImagePadding, ImagePadding, 0),
                ClipToBounds        = true,
                Child               = placeholderPanel
            };

            // ── 4. BUTTON BAR (4 nút dưới cùng) ─────────────────────────────
            var captureButton = new Button
            {
                Content    = node.HasCaptureRegion ? "📸 Chụp lại" : "📸 Chụp vùng",
                Height     = ButtonHeight,
                FontSize   = 11,
                FontWeight = FontWeights.Medium,
                Cursor     = Cursors.Hand,
                ToolTip    = "Chụp vùng màn hình",
                Style      = Application.Current.FindResource("PrimaryButton") as Style
            };

            var pickFileButton = new Button
            {
                Content    = "🖼 Chọn ảnh",
                Height     = ButtonHeight,
                FontSize   = 11,
                FontWeight = FontWeights.Medium,
                Cursor     = Cursors.Hand,
                ToolTip    = "Chọn ảnh từ file",
                Style      = Application.Current.FindResource("PrimaryButton") as Style
            };

            var previewButton = new Button
            {
                Content    = "👁 Preview",
                Height     = ButtonHeight,
                FontSize   = 11,
                FontWeight = FontWeights.Medium,
                Cursor     = Cursors.Hand,
                ToolTip    = "Xem ảnh full size",
                Style      = Application.Current.FindResource("InfoButton") as Style
            };

            var clearButton = new Button
            {
                Content   = "x",
                Height    = ButtonHeight,
                Width     = ButtonHeight,
                FontSize  = 13,
                Cursor    = Cursors.Hand,
                ToolTip   = "Xoá ảnh",
                Padding    = new Thickness(0),
                Style     = Application.Current.FindResource("DangerButton") as Style
            };

            var buttonBar = new Grid { Margin = new Thickness(ImagePadding, 4, ImagePadding, ImagePadding) };
            buttonBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            buttonBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            buttonBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            buttonBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(captureButton,  0);
            Grid.SetColumn(pickFileButton, 2);
            Grid.SetColumn(previewButton, 4);
            Grid.SetColumn(clearButton,    6);
            buttonBar.Children.Add(captureButton);
            buttonBar.Children.Add(pickFileButton);
            buttonBar.Children.Add(previewButton);
            buttonBar.Children.Add(clearButton);

            // ── 5. MAIN GRID ─────────────────────────────────────────────────
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // preview
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // buttons
            Grid.SetRow(previewBorder, 0);
            Grid.SetRow(buttonBar,     1);
            mainGrid.Children.Add(previewBorder);
            mainGrid.Children.Add(buttonBar);

            // ── 6. BORDER (node ngoài cùng) ──────────────────────────────────
            var border = new Border
            {
                Background      = node.NodeBrush,
                Cursor          = Cursors.Hand,
                BorderBrush     = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius    = new CornerRadius(10),
                Effect          = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = Colors.Black,
                    Direction   = 270,
                    ShadowDepth = 5,
                    BlurRadius  = 10,
                    Opacity     = 0.5
                },
                Tag   = node,
                Child = mainGrid
            };

            // ── 7. TITLE TEXTBLOCK (nổi phía trên canvas) ───────────────────
            var titleTextBlock = new TextBlock
            {
                Text                = node.Title ?? "Screen Capture",
                FontSize            = 12,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = BaseNodeControlHelper.ResolveTitleBrush(
                                          node.TitleColorMode, node.TitleColorKey, node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Top,
                TextAlignment       = TextAlignment.Center,
                IsHitTestVisible    = false,
                Visibility          = node.TitleDisplayMode == TitleDisplayMode.Always
                                          ? Visibility.Visible : Visibility.Collapsed
            };
            node.TitleTextBlockUI = titleTextBlock;

            // ── 8. CẬP NHẬT PREVIEW + KÍCH THƯỚC NODE ───────────────────────
            void UpdateButtonLabels(double nodeWidth)
            {
                // Thu gọn thành icon khi node nhỏ hơn CompactWidth
                if (nodeWidth < CompactWidth)
                {
                    captureButton.Content  = "📸";
                    pickFileButton.Content = "🖼";
                    previewButton.Content = "👁";
                }
                else
                {
                    captureButton.Content  = node.HasCaptureRegion ? "📸 Chụp lại" : "📸 Chụp vùng";
                    pickFileButton.Content = "🖼 Chọn ảnh";
                    previewButton.Content = "👁 Preview";
                }
            }

            void UpdateNodeSize()
            {
                var img = node.CapturedImage;
                if (img == null || img.PixelWidth <= 0 || img.PixelHeight <= 0)
                {
                    // Chưa có ảnh → kích thước mặc định
                    border.Width  = 200;
                    border.Height = 120 + ButtonBarH;
                    previewBorder.Child = placeholderPanel;
                    UpdateButtonLabels(200);
                    return;
                }

                double pw = img.PixelWidth;
                double ph = img.PixelHeight;

                // Tính kích thước node theo tỉ lệ ảnh
                // UseNativeWidth = true  → không giới hạn width, dùng kích thước ảnh gốc
                // UseNativeWidth = false → giới hạn theo node.MaxNodeWidth
                double nodeW, nodeH;
                double maxW = node.UseNativeWidth ? double.MaxValue : Math.Max(node.MaxNodeWidth, MinNodeW);

                if (pw >= ph)
                {
                    // Landscape: giới hạn width
                    nodeW = Math.Min(pw, maxW);
                    nodeH = nodeW * ph / pw;
                }
                else
                {
                    // Portrait: tính từ height tương ứng với maxW
                    double maxH = maxW * ph / pw;
                    nodeH = Math.Min(ph, maxH);
                    nodeW = nodeH * pw / ph;
                    // Nếu width vẫn vượt maxW (ảnh rất rộng), clamp lại
                    if (nodeW > maxW)
                    {
                        nodeW = maxW;
                        nodeH = nodeW * ph / pw;
                    }
                }

                // Đảm bảo không quá nhỏ để button bar vẫn dùng được
                nodeW = Math.Max(nodeW, MinNodeW);
                nodeH = Math.Max(nodeH, MinNodeH);

                border.Width  = nodeW;
                border.Height = nodeH + ButtonBarH;

                previewImage.Source = img;
                previewBorder.Child = previewImage;
                UpdateButtonLabels(nodeW);
            }

            UpdateNodeSize();

            node.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScreenCaptureNode.CapturedImage)
                    || e.PropertyName == nameof(ScreenCaptureNode.UseNativeWidth)
                    || e.PropertyName == nameof(ScreenCaptureNode.MaxNodeWidth))
                    Application.Current?.Dispatcher.Invoke(UpdateNodeSize);
            };

            // Cập nhật label button khi node bị resize (zoom canvas thay đổi ActualWidth)
            border.SizeChanged += (s, e) => UpdateButtonLabels(e.NewSize.Width);

            // ── 9. BUTTON EVENTS ─────────────────────────────────────────────

            // Nút chụp màn hình
            captureButton.Click += (s, e) =>
            {
                e.Handled = true; // không bubble lên border
                FlowMy.Helpers.ScreenCaptureHelper.CaptureForScreenCaptureNode(node, ownerWindow);
            };

            // Nút chọn ảnh từ file
            pickFileButton.Click += (s, e) =>
            {
                e.Handled = true;
                try
                {
                    var dlg = new OpenFileDialog
                    {
                        Title           = "Chọn ảnh",
                        Filter          = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
                        CheckFileExists = true,
                        Multiselect     = false
                    };

                    if (dlg.ShowDialog(ownerWindow) == true)
                    {
                        var bmp = LoadBitmapFromFile(dlg.FileName);
                        if (bmp != null)
                        {
                            node.ImagePath     = dlg.FileName;
                            node.CaptureX      = 0;
                            node.CaptureY      = 0;
                            node.CaptureWidth  = bmp.PixelWidth;
                            node.CaptureHeight = bmp.PixelHeight;
                            node.CapturedImage = bmp; // trigger UpdateNodeSize qua PropertyChanged
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không mở được file: " + ex.Message, "Ảnh",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            // Nút preview ảnh
            previewButton.Click += (s, e) =>
            {
                e.Handled = true;
                if (node.CapturedImage != null)
                {
                    ShowImagePreviewPopup(node, ownerWindow);
                }
            };

            // Nút xoá ảnh
            clearButton.Click += (s, e) =>
            {
                e.Handled = true;
                node.CapturedImage = null;
                node.CaptureX      = 0;
                node.CaptureY      = 0;
                node.CaptureWidth  = 0;
                node.CaptureHeight = 0;
                node.ImagePath     = string.Empty;
            };

            // ── 10. FLUENT API ───────────────────────────────────────────────
            if (host != null)
            {
                var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
                {
                    [nameof(WorkflowNode.NodeBrush)] = ctx =>
                    {
                        border.Background = node.NodeBrush;
                    },
                    [nameof(WorkflowNode.ColorKey)] = ctx =>
                    {
                        placeholderText.SetResourceReference(TextBlock.ForegroundProperty,
                            $"TextOn{node.ColorKey}Brush");
                    }
                };

                BaseNodeControlHelper
                    .Initialize(border, titleTextBlock, node, host)
                    .WithTitleManagement()
                    .WithHoverBehavior()
                    .WithKeyboardPorts()
                    .WithPropertySync(customPropertyHandlers)
                    .WithDialogSupport(ctx => new ScreenCaptureNodeDialog(
                        node, host, ownerWindow ?? Application.Current?.MainWindow))
                    .WithCleanup()
                    .WithVisibilitySync()
                    .WithCanvasIntegration()
                    .Build();
            }

            return border;
        }

        // ── Helper: load BitmapImage từ file ────────────────────────────────
        private static BitmapImage? LoadBitmapFromFile(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource   = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCaptureNodeControl] LoadBitmapFromFile: {ex.Message}");
                return null;
            }
        }

        // ── Helper: popup xem ảnh full size với zoom/pan (Popup style, đóng khi click ra bên ngoài) ──
        private static void ShowImagePreviewPopup(ScreenCaptureNode node, Window ownerWindow)
        {
            if (node.CapturedImage == null) return;

            var bitmap = node.CapturedImage;

            // Tạo popup với StaysOpen=false để tự đóng khi click ra bên ngoài
            var popup = new Popup
            {
                StaysOpen = false,
                Placement = PlacementMode.Center,
                PlacementTarget = ownerWindow,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };

            // Tạo border làm container cho popup
            var popupBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 70)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 8,
                    BlurRadius = 16,
                    Opacity = 0.6
                },
                Width = Math.Min(Math.Max(bitmap.PixelWidth + 32, 300), 1400),
                Height = Math.Min(Math.Max(bitmap.PixelHeight + 80, 200), 900),
                MinWidth = 200,
                MinHeight = 150
            };

            var imgControl = new Image
            {
                Source = bitmap,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight
            };
            RenderOptions.SetBitmapScalingMode(imgControl, BitmapScalingMode.HighQuality);

            var scale = new ScaleTransform(1.0, 1.0);
            imgControl.LayoutTransform = scale;

            var infoBar = new TextBlock
            {
                Text = $"{node.CaptureWidth} × {node.CaptureHeight} px  |  ({node.CaptureX}, {node.CaptureY})",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4),
                Foreground = Brushes.White,
                Opacity = 0.8
            };

            var contentGrid = new Grid();
            contentGrid.Children.Add(imgControl);

            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8),
                Content = contentGrid,
                Focusable = true
            };
            RenderOptions.SetBitmapScalingMode(scrollViewer, BitmapScalingMode.HighQuality);

            var rootStack = new DockPanel();
            DockPanel.SetDock(infoBar, Dock.Bottom);
            rootStack.Children.Add(infoBar);
            rootStack.Children.Add(scrollViewer);
            popupBorder.Child = rootStack;
            popup.Child = popupBorder;

            // Fit ảnh vào popup khi mở
            popup.Opened += (_, _) =>
            {
                double availW = scrollViewer.ActualWidth - 16;
                double availH = scrollViewer.ActualHeight - 16;
                if (availW <= 0 || availH <= 0) return;
                double sx = availW / bitmap.PixelWidth;
                double sy = availH / bitmap.PixelHeight;
                double initial = Math.Min(sx, sy);
                if (initial > 1.0) initial = 1.0;
                scale.ScaleX = initial;
                scale.ScaleY = initial;
            };

            // Zoom bằng Ctrl+Scroll hoặc Scroll thường
            scrollViewer.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
                var pos = e.GetPosition(scrollViewer);
                var oldScale = scale.ScaleX;
                double factor = e.Delta > 0 ? 1.12 : 0.89;
                var newScale = Math.Max(0.02, Math.Min(oldScale * factor, 50.0));
                if (Math.Abs(newScale - oldScale) < 0.0001) return;

                double relX = 0.5, relY = 0.5;
                if (scrollViewer.ExtentWidth > 0) relX = (scrollViewer.HorizontalOffset + pos.X) / scrollViewer.ExtentWidth;
                if (scrollViewer.ExtentHeight > 0) relY = (scrollViewer.VerticalOffset + pos.Y) / scrollViewer.ExtentHeight;

                scale.ScaleX = newScale;
                scale.ScaleY = newScale;
                scrollViewer.UpdateLayout();

                if (scrollViewer.ExtentWidth > 0) scrollViewer.ScrollToHorizontalOffset(Math.Max(0, relX * scrollViewer.ExtentWidth - pos.X));
                if (scrollViewer.ExtentHeight > 0) scrollViewer.ScrollToVerticalOffset(Math.Max(0, relY * scrollViewer.ExtentHeight - pos.Y));
            };

            // Pan bằng giữ chuột trái
            bool isPanning = false;
            Point panStart = default;
            double panOX = 0, panOY = 0;

            scrollViewer.PreviewMouseLeftButtonDown += (s, e) =>
            {
                isPanning = true;
                panStart = e.GetPosition(scrollViewer);
                panOX = scrollViewer.HorizontalOffset;
                panOY = scrollViewer.VerticalOffset;
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
                scrollViewer.ScrollToHorizontalOffset(panOX - (pos.X - panStart.X));
                scrollViewer.ScrollToVerticalOffset(panOY - (pos.Y - panStart.Y));
                e.Handled = true;
            };

            // Mở popup
            popup.IsOpen = true;
        }

        // ── Helper: popup xem ảnh full size với zoom/pan (giống MediaGalleryNodeControl) - giữ lại cho backward compatibility ──
        private static void ShowImagePreviewDialog(ScreenCaptureNode node, Window ownerWindow)
        {
            if (node.CapturedImage == null) return;

            var bitmap = node.CapturedImage;
            var w = new Window
            {
                Title                 = $"Preview – {node.Title}  ({node.CaptureWidth} × {node.CaptureHeight} px)",
                Width                 = Math.Min(Math.Max(bitmap.PixelWidth + 32, 300), 1400),
                Height                = Math.Min(Math.Max(bitmap.PixelHeight + 80, 200), 900),
                MinWidth              = 200,
                MinHeight             = 150,
                Owner                 = ownerWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle           = WindowStyle.ToolWindow,
                ResizeMode            = ResizeMode.CanResize,
                ShowInTaskbar         = false,
                Background            = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))
            };

            var imgControl = new Image
            {
                Source              = bitmap,
                Stretch             = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Top,
                Width               = bitmap.PixelWidth,
                Height              = bitmap.PixelHeight
            };
            RenderOptions.SetBitmapScalingMode(imgControl, BitmapScalingMode.HighQuality);

            var scale = new ScaleTransform(1.0, 1.0);
            imgControl.LayoutTransform = scale;

            var infoBar = new TextBlock
            {
                Text                = $"{node.CaptureWidth} × {node.CaptureHeight} px  |  ({node.CaptureX}, {node.CaptureY})",
                FontSize            = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 4, 0, 4),
                Foreground          = Brushes.White,
                Opacity             = 0.8
            };
            //infoBar.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

            var contentGrid = new Grid();
            contentGrid.Children.Add(imgControl);

            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                Padding                       = new Thickness(8),
                Content                       = contentGrid,
                Focusable                     = true
            };
            RenderOptions.SetBitmapScalingMode(scrollViewer, BitmapScalingMode.HighQuality);

            var rootStack = new DockPanel();
            DockPanel.SetDock(infoBar, Dock.Bottom);
            rootStack.Children.Add(infoBar);
            rootStack.Children.Add(scrollViewer);
            w.Content = rootStack;

            // Fit ảnh vào cửa sổ khi mở
            w.Loaded += (_, _) =>
            {
                double availW = scrollViewer.ActualWidth  - 16;
                double availH = scrollViewer.ActualHeight - 16;
                if (availW <= 0 || availH <= 0) return;
                double sx = availW  / bitmap.PixelWidth;
                double sy = availH / bitmap.PixelHeight;
                double initial = Math.Min(sx, sy);
                if (initial > 1.0) initial = 1.0;
                scale.ScaleX = initial;
                scale.ScaleY = initial;
            };

            // Zoom bằng Ctrl+Scroll hoặc Scroll thường
            scrollViewer.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
                var pos      = e.GetPosition(scrollViewer);
                var oldScale = scale.ScaleX;
                double factor = e.Delta > 0 ? 1.12 : 0.89;
                var newScale = Math.Max(0.02, Math.Min(oldScale * factor, 50.0));
                if (Math.Abs(newScale - oldScale) < 0.0001) return;

                double relX = 0.5, relY = 0.5;
                if (scrollViewer.ExtentWidth  > 0) relX = (scrollViewer.HorizontalOffset + pos.X) / scrollViewer.ExtentWidth;
                if (scrollViewer.ExtentHeight > 0) relY = (scrollViewer.VerticalOffset   + pos.Y) / scrollViewer.ExtentHeight;

                scale.ScaleX = newScale;
                scale.ScaleY = newScale;
                scrollViewer.UpdateLayout();

                if (scrollViewer.ExtentWidth  > 0) scrollViewer.ScrollToHorizontalOffset(Math.Max(0, relX * scrollViewer.ExtentWidth  - pos.X));
                if (scrollViewer.ExtentHeight > 0) scrollViewer.ScrollToVerticalOffset  (Math.Max(0, relY * scrollViewer.ExtentHeight - pos.Y));
            };

            // Pan bằng giữ chuột trái
            bool isPanning = false;
            Point panStart = default;
            double panOX = 0, panOY = 0;

            scrollViewer.PreviewMouseLeftButtonDown += (s, e) =>
            {
                isPanning = true;
                panStart  = e.GetPosition(scrollViewer);
                panOX     = scrollViewer.HorizontalOffset;
                panOY     = scrollViewer.VerticalOffset;
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
                scrollViewer.ScrollToHorizontalOffset(panOX - (pos.X - panStart.X));
                scrollViewer.ScrollToVerticalOffset  (panOY - (pos.Y - panStart.Y));
                e.Handled = true;
            };

            w.ShowDialog();
        }
    }
}
