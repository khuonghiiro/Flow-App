using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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

            // Click trái → xem full size; chuột phải → để bubble lên border (WithDialogSupport xử lý)
            previewImage.Cursor = Cursors.Hand;
            previewImage.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == System.Windows.Input.MouseButton.Left && node.CapturedImage != null)
                {
                    ShowImagePreviewDialog(node, ownerWindow);
                    e.Handled = true;
                }
                // Chuột phải: KHÔNG handle → bubble lên border để WithDialogSupport mở dialog node
            };

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

            // ── 4. BUTTON BAR (3 nút dưới cùng) ─────────────────────────────
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
            buttonBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(captureButton,  0);
            Grid.SetColumn(pickFileButton, 2);
            Grid.SetColumn(clearButton,    4);
            buttonBar.Children.Add(captureButton);
            buttonBar.Children.Add(pickFileButton);
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
                }
                else
                {
                    captureButton.Content  = node.HasCaptureRegion ? "📸 Chụp lại" : "📸 Chụp vùng";
                    pickFileButton.Content = "🖼 Chọn ảnh";
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
                ownerWindow.Hide();
                try
                {
                    var overlay = new ScreenCaptureOverlay();
                    if (overlay.ShowDialog() == true)
                    {
                        node.CaptureX      = overlay.CaptureX;
                        node.CaptureY      = overlay.CaptureY;
                        node.CaptureWidth  = overlay.CaptureWidth;
                        node.CaptureHeight = overlay.CaptureHeight;
                        node.CapturedImage = overlay.CapturedImage;
                    }
                }
                finally
                {
                    ownerWindow.Show();
                    ownerWindow.Activate();
                }
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

        // ── Helper: dialog xem ảnh full size (chỉ mở bằng click trái) ──────
        private static void ShowImagePreviewDialog(ScreenCaptureNode node, Window ownerWindow)
        {
            if (node.CapturedImage == null) return;

            double maxW = Math.Min(node.CaptureWidth,  1200);
            double maxH = Math.Min(node.CaptureHeight, 800);
            maxW = Math.Max(maxW, 200);
            maxH = Math.Max(maxH, 150);

            var imgControl = new Image
            {
                Source  = node.CapturedImage,
                Stretch = Stretch.Uniform,
                Margin  = new Thickness(8)
            };

            var infoBar = new TextBlock
            {
                Text                = $"{node.CaptureWidth} × {node.CaptureHeight} px  |  ({node.CaptureX}, {node.CaptureY})",
                FontSize            = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 8)
            };
            infoBar.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

            var stack = new StackPanel();
            stack.Children.Add(imgControl);
            stack.Children.Add(infoBar);

            var viewer = new Window
            {
                Title                 = $"Preview – {node.Title}",
                Width                 = maxW + 32,
                Height                = maxH + 80,
                MinWidth              = 200,
                MinHeight             = 150,
                Owner                 = ownerWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle           = WindowStyle.ToolWindow,
                ResizeMode            = ResizeMode.CanResize,
                ShowInTaskbar         = false,
                Content               = stack
            };
            viewer.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");
            viewer.ShowDialog();
        }
    }
}
