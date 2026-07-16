// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS & DEVELOPERS:
// DO NOT ALLOW ANY FILE IN THIS COMPONENT TO EXCEED ~1500 LINES OF CODE!
// To maintain readability, ease of testing, and modularity:
// - If a file grows larger than ~1500 lines, you MUST split/separate the logic into a new
//   partial class file (e.g., ImageProcessingNodeControl.<FeatureName>.cs).
// - Always place distinct features, tools, or event groupings in their respective files.
// - Ensure comments and documentation remain clean and structured.
// ========================================================================================
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System.Collections.Generic;
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
using System;
namespace FlowMy.Views.NodeControls
{
    public static partial class ImageProcessingNodeControl
    {
        internal static void OpenImageFilePicker(ImageProcessingNode node)
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
        internal static void AddCropPointFromClick(
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
        internal static bool CompleteActiveCrop(ImageProcessingNode node, Button cropButton)
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
                DetachCropPolygon(region);
                node.Crops.Remove(region);
            }

            _activeCropRegion[node] = null;
            if (cropButton != null)
                cropButton.IsEnabled = true;
            return true;
        }

        internal static (FrameworkElement, Action<BitmapSource?>) BuildImageProcessorColumn(
            ImageProcessingNode node,
            IWorkflowEditorHost host,
            bool preventScaleUp = false,
            double widgetDipScale = 1.0,
            bool dipNativeLayout = false)
        {
            // ── State ──
            BitmapSource? currentSource = null;
            BitmapSource? processedBitmap = null;
            bool isVerticalMode = node.IsVerticalMode; // Khôi phục từ node

            if (widgetDipScale < 0.5 || double.IsNaN(widgetDipScale) || double.IsInfinity(widgetDipScale))
                widgetDipScale = 1.0;

            // Widget DIP: bỏ Viewbox — chỉ scale bằng Zi/Zd; nhẹ nhàng tăng thêm để chữ/control dễ đọc.
            double sharpMul = dipNativeLayout ? 1.05 : 1.0;
            int Zi(double pts) => Math.Max(1, (int)Math.Round(pts * widgetDipScale * sharpMul));
            double Zd(double px) => Math.Max(1.0, Math.Round(px * widgetDipScale * sharpMul, MidpointRounding.AwayFromZero));

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
            if (dipNativeLayout)
            {
                TextOptions.SetTextFormattingMode(columnBorder, TextFormattingMode.Display);
                columnBorder.SnapsToDevicePixels = true;
                columnBorder.UseLayoutRounding = true;
            }
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
                Padding = new Thickness(Zd(10), Zd(8), Zd(10), Zd(8))
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
                FontSize = Zi(10),
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });
            ipHeader.Child = headerStack;
            DockPanel.SetDock(ipHeader, Dock.Top);
            columnDock.Children.Add(ipHeader);

            // Buttons ở đáy (canvas: scale theo Viewbox; widget DIP: cùng border, không Viewbox)

            // Scrollable content (canvas: width cố định + Viewbox scale; widget DIP: stretch, không Viewbox)
            var contentStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(Zd(10), Zd(6), Zd(10), Zd(6)),
            };
            if (!dipNativeLayout)
                contentStack.Width = Zd(240);
            else
            {
                contentStack.HorizontalAlignment = HorizontalAlignment.Stretch;
                contentStack.SnapsToDevicePixels = true;
                contentStack.UseLayoutRounding = true;
            }

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
                    FontSize = Zi(9),
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
                FontSize = Zi(10),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(0, Zd(6), 0, Zd(6)),
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
                FontSize = Zi(10),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(0, Zd(6), 0, Zd(6)),
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
                FontSize = Zi(9)
            };
            var txtRatioInfo = new TextBlock
            {
                Foreground = ipAccent2,
                FontFamily = new FontFamily("Consolas"),
                FontSize = Zi(9),
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
                FontSize = Zi(9),
                Margin = new Thickness(0, 0, 0, 2)
            };
            contentStack.Children.Add(txtImgInfo);

            var txtPreviewHint = new TextBlock
            {
                Text = "Nhấn vào ảnh crop để xử lý",
                Foreground = ipMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = Zi(10)
            };
            var imgPreview = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(2)
            };
            // Cả widget và canvas đều giới hạn MaxHeight để ảnh dọc không chiếm quá nhiều
            // chiều dọc, che mất các control bên dưới (prompt, buttons...).
            // Stretch.Uniform + HorizontalScroll=Disabled → width tự co theo tỉ lệ ảnh.
            imgPreview.MaxHeight = dipNativeLayout ? Zd(300) : Zd(240);
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

            var previewGrid = new Grid { MinHeight = Zd(80) };
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
                        FontSize = Zi(8)
                    }
                });
            }

            // ═══════════ SỐ LẦN GỬI ═══════════
            contentStack.Children.Add(MakeSectionLabel("SỐ LẦN GỬI"));

            var comboPromptSize = new ComboBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = Zi(10),
                Padding = new Thickness(Zd(6), Zd(4), Zd(6), Zd(4)),
                Height = Zd(32),
                Margin = new Thickness(0, 0, 0, 4)
            };

            // ── Custom dark ComboBox style cho IP panel ──
            {
                var cbNormalBg = ipSurface2;          // #181c24
                var cbHoverBg = new SolidColorBrush(Color.FromRgb(0x1e, 0x22, 0x2c));
                var cbBorder = ipBorderBrush;         // #252932
                var cbHoverBorder = new SolidColorBrush(Color.FromArgb(160, 0x4f, 0xff, 0xb0)); // accent semi
                var cbDropdownBg = new SolidColorBrush(Color.FromRgb(0x14, 0x18, 0x20));
                var cbItemHover = new SolidColorBrush(Color.FromArgb(40, 0x4f, 0xff, 0xb0));
                var cbGlyph = ipMuted;                // #5a6072
                var cbGlyphHover = ipAccent;           // #4fffb0

                // Toggle button template (header phần hiển thị)
                var toggleTpl = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
                var toggleBd = new FrameworkElementFactory(typeof(Border), "toggleBd");
                toggleBd.SetValue(Border.BackgroundProperty, cbNormalBg);
                toggleBd.SetValue(Border.BorderBrushProperty, cbBorder);
                toggleBd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                toggleBd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                toggleBd.SetValue(Border.PaddingProperty, new Thickness(6, 4, 4, 4));

                // DockPanel: content trái, arrow phải
                var tgDock = new FrameworkElementFactory(typeof(DockPanel));

                var tgArrow = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path), "arrow");
                tgArrow.SetValue(DockPanel.DockProperty, Dock.Right);
                tgArrow.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M0,0 L4,4 L8,0"));
                tgArrow.SetValue(System.Windows.Shapes.Path.StrokeProperty, cbGlyph);
                tgArrow.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.5);
                tgArrow.SetValue(System.Windows.Shapes.Path.FillProperty, Brushes.Transparent);
                tgArrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                tgArrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                tgArrow.SetValue(FrameworkElement.WidthProperty, 16.0);
                tgDock.AppendChild(tgArrow);

                var tgContent = new FrameworkElementFactory(typeof(ContentPresenter));
                tgContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                tgContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                tgContent.SetValue(ContentPresenter.MarginProperty, new Thickness(2, 0, 0, 0));
                tgDock.AppendChild(tgContent);

                toggleBd.AppendChild(tgDock);
                toggleTpl.VisualTree = toggleBd;

                // Hover trigger
                var tgHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                tgHover.Setters.Add(new Setter(Border.BackgroundProperty, cbHoverBg, "toggleBd"));
                tgHover.Setters.Add(new Setter(Border.BorderBrushProperty, cbHoverBorder, "toggleBd"));
                tgHover.Setters.Add(new Setter(System.Windows.Shapes.Path.StrokeProperty, cbGlyphHover, "arrow"));
                toggleTpl.Triggers.Add(tgHover);

                // Checked (dropdown mở) trigger
                var tgChecked = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
                tgChecked.Setters.Add(new Setter(Border.BorderBrushProperty, cbHoverBorder, "toggleBd"));
                tgChecked.Setters.Add(new Setter(System.Windows.Shapes.Path.StrokeProperty, cbGlyphHover, "arrow"));
                toggleTpl.Triggers.Add(tgChecked);

                // ComboBox template
                var cbTpl = new ControlTemplate(typeof(ComboBox));
                var cbGrid = new FrameworkElementFactory(typeof(Grid));

                var cbToggle = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.ToggleButton), "cbToggle");
                cbToggle.SetValue(System.Windows.Controls.Primitives.ToggleButton.TemplateProperty, toggleTpl);
                cbToggle.SetValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                    new System.Windows.Data.Binding("IsDropDownOpen")
                    {
                        RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
                        Mode = System.Windows.Data.BindingMode.TwoWay
                    });
                cbToggle.SetValue(System.Windows.Controls.Primitives.ToggleButton.FocusableProperty, false);
                cbToggle.SetValue(System.Windows.Controls.Primitives.ToggleButton.ClickModeProperty, ClickMode.Press);
                cbGrid.AppendChild(cbToggle);

                // ContentPresenter cho selected item
                var cbPresenter = new FrameworkElementFactory(typeof(ContentPresenter), "cbContent");
                cbPresenter.SetValue(ContentPresenter.ContentTemplateProperty,
                    new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
                cbPresenter.SetValue(ContentPresenter.ContentProperty,
                    new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
                cbPresenter.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 4, 24, 4));
                cbPresenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                cbPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                cbPresenter.SetValue(UIElement.IsHitTestVisibleProperty, false);
                cbGrid.AppendChild(cbPresenter);

                // Popup dropdown
                var cbPopup = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup), "PART_Popup");
                cbPopup.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty, System.Windows.Controls.Primitives.PlacementMode.Bottom);
                cbPopup.SetValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
                    new TemplateBindingExtension(ComboBox.IsDropDownOpenProperty));
                cbPopup.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
                cbPopup.SetValue(System.Windows.Controls.Primitives.Popup.PopupAnimationProperty, System.Windows.Controls.Primitives.PopupAnimation.Slide);

                var popupBd = new FrameworkElementFactory(typeof(Border));
                popupBd.SetValue(Border.BackgroundProperty, cbDropdownBg);
                popupBd.SetValue(Border.BorderBrushProperty, cbHoverBorder);
                popupBd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                popupBd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                popupBd.SetValue(Border.PaddingProperty, new Thickness(2));
                popupBd.SetValue(FrameworkElement.MinWidthProperty,
                    new TemplateBindingExtension(FrameworkElement.ActualWidthProperty));

                var popupScroll = new FrameworkElementFactory(typeof(ScrollViewer));
                var popupItemsHost = new FrameworkElementFactory(typeof(StackPanel));
                popupItemsHost.SetValue(Panel.IsItemsHostProperty, true);
                popupScroll.AppendChild(popupItemsHost);
                popupBd.AppendChild(popupScroll);
                cbPopup.AppendChild(popupBd);
                cbGrid.AppendChild(cbPopup);

                cbTpl.VisualTree = cbGrid;

                // ComboBox style
                var cbStyle = new Style(typeof(ComboBox));
                cbStyle.Setters.Add(new Setter(Control.TemplateProperty, cbTpl));
                cbStyle.Setters.Add(new Setter(Control.ForegroundProperty, ipText));
                cbStyle.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));

                // ItemContainerStyle (hover cho từng item)
                var itemStyle = new Style(typeof(ComboBoxItem));
                itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, ipText));
                itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
                itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
                itemStyle.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
                itemStyle.Setters.Add(new Setter(Control.SnapsToDevicePixelsProperty, true));

                var itemTpl = new ControlTemplate(typeof(ComboBoxItem));
                var itemBd = new FrameworkElementFactory(typeof(Border), "itemBd");
                itemBd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
                itemBd.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
                itemBd.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
                var itemCp = new FrameworkElementFactory(typeof(ContentPresenter));
                itemCp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                itemBd.AppendChild(itemCp);
                itemTpl.VisualTree = itemBd;

                var itemHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                itemHover.Setters.Add(new Setter(Control.BackgroundProperty, cbItemHover));
                itemHover.Setters.Add(new Setter(Control.ForegroundProperty, cbGlyphHover));
                itemTpl.Triggers.Add(itemHover);

                var itemSelected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
                itemSelected.Setters.Add(new Setter(Control.BackgroundProperty, cbItemHover));
                itemSelected.Setters.Add(new Setter(Control.ForegroundProperty, ipAccent));
                itemSelected.Setters.Add(new Setter(System.Windows.Documents.TextElement.FontWeightProperty, FontWeights.SemiBold));
                itemTpl.Triggers.Add(itemSelected);

                itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTpl));
                cbStyle.Setters.Add(new Setter(ItemsControl.ItemContainerStyleProperty, itemStyle));

                comboPromptSize.Style = cbStyle;
            }
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
                FontSize = Zi(10),
                Padding = new Thickness(Zd(6), Zd(4), Zd(6), Zd(4)),
                Height = Zd(120),
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
                Width = Zd(90),
                Height = Zd(30),
                FontSize = Zi(11),
                Padding = new Thickness(0, Zd(7), 0, Zd(7)),
                Cursor = Cursors.Hand
            };
            btnProcess.Template = MakeDarkButtonTemplate();
            btnProcess.MouseLeftButtonDown += (s, e) => e.Handled = true;

            var btnStart = new Button
            {
                Content = "▶ Bắt đầu",
                Style = Application.Current.TryFindResource("PrimaryButton") as Style,
                FontWeight = FontWeights.Bold,
                Width = Zd(90),
                Height = Zd(30),
                FontSize = Zi(11),
                Padding = new Thickness(0, Zd(7), 0, Zd(7)),
                Cursor = Cursors.Hand
            };
            btnStart.Template = MakeDarkButtonTemplate();
            btnStart.MouseLeftButtonDown += (s, e) => e.Handled = true;

            var actionGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(btnProcess, 0);
            Grid.SetColumn(btnStart, 2);
            actionGrid.Children.Add(btnProcess);
            actionGrid.Children.Add(btnStart);

            var bottomStack = new StackPanel { Orientation = Orientation.Vertical };
            bottomStack.Children.Add(actionGrid);
            if (!dipNativeLayout)
                bottomStack.Width = Zd(240);
            else
            {
                bottomStack.HorizontalAlignment = HorizontalAlignment.Stretch;
                bottomStack.SnapsToDevicePixels = true;
                bottomStack.UseLayoutRounding = true;
            }

            var bottomActionsPaddingBorder = new Border
            {
                Padding = new Thickness(Zd(10), Zd(8), Zd(10), Zd(10)),
                Child = bottomStack
            };

            ScrollViewer ipScroll;
            Border bottomActionsHost;
            if (dipNativeLayout)
            {
                ipScroll = new ScrollViewer
                {
                    Content = contentStack,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    // Disabled: ràng buộc width nội dung theo cột, ảnh preview không tràn ngang.
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };
                bottomActionsHost = new Border
                {
                    Background = ipBg,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3a)),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Child = bottomActionsPaddingBorder
                };
            }
            else
            {
                // Canvas / node: Viewbox scale nội dung design-width 240 theo cột
                var ipViewbox = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = preventScaleUp ? StretchDirection.DownOnly : StretchDirection.Both,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    Child = contentStack
                };
                ipScroll = new ScrollViewer
                {
                    Content = ipViewbox,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };
                var bottomActionsViewbox = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = preventScaleUp ? StretchDirection.DownOnly : StretchDirection.Both,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = bottomActionsPaddingBorder
                };
                bottomActionsHost = new Border
                {
                    Background = ipBg,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3a)),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Child = bottomActionsViewbox
                };
            }

            DockPanel.SetDock(bottomActionsHost, Dock.Bottom);
            columnDock.Children.Add(bottomActionsHost);
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
                // Chức năng Xử lý AI/Crop cũ trên node widget đã được comment lại để không sử dụng nữa.
                // Cơ chế mới được chạy trực tiếp từ Editor thông qua LayerAiDialog.
                /*
                if (processedBitmap == null)
                {
                    MessageBox.Show("Chưa có ảnh đã xử lý.", "Image Processor",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Clear UserValueOverride cho các key bị skip trước khi set output mới,
                // tránh giá trị cũ từ lần chạy trước vẫn bị resolve.
                if (node.SkipOutputs != null && node.SkipOutputs.Count > 0 && node.DynamicOutputs != null)
                {
                    foreach (var skippedPort in node.DynamicOutputs)
                    {
                        if (node.SkipOutputs.Contains(skippedPort.Key ?? string.Empty))
                            skippedPort.UserValueOverride = string.Empty;
                    }
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
                    // Lưu executionId vào crop để map ảnh render về đúng crop sau này
                    cropRegion.LastExecutionId = execId;
                    
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
                */
                await System.Threading.Tasks.Task.CompletedTask;
            };

            // Set image action (exposed to caller)
            Action<BitmapSource?> setImage = (bmp) =>
            {
                currentSource = bmp;
                _ = ProcessAsync();
            };

            return (columnBorder, setImage);
        }

    }
}
