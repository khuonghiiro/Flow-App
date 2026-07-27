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
using FlowMy.Services.Workflow;
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

        internal static void UpdateInteractionVisualScale(Grid handleOverlay, WorkflowNode node, double rawScale)
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

        internal static void OpenNodeDialog(ImageProcessingNode node, IWorkflowEditorHost host, Window? ownerWindow)
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

        internal static async System.Threading.Tasks.Task UpdatePreviewAsync(
            ImageProcessingNode node,
            IWorkflowEditorHost host,
            System.Windows.Controls.Image image,
            TextBlock placeholder,
            ScaleTransform scale,
            Grid? imageGrid = null,
            Action<double, double>? centerImageCallback = null,
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
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        bitmap = await System.Threading.Tasks.Task.Run(() => CreateBitmapFromBase64(resolved));
                    }
                }
                else
                {
                    resolved = ResolveFromNodeIfAny(host, node.ImageUrlSourceNodeId, node.ImageUrlSourceOutputKey)
                               ?? node.ImageUrl;
                    resolved = resolved?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
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
                    }
                }

                // Nếu chưa có ảnh hoặc nạp lỗi -> mặc định tạo ảnh trắng 720x1080
                if (bitmap == null)
                {
                    bitmap = CreateBlankWhiteBitmap(720, 1080);
                    resolved = "Ảnh trắng (720x1080)";
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

                    // Set Width/Height cùng lúc trong BeginInvoke, centering do callback xử lý
                    image.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        image.Width = imageWidth;
                        image.Height = imageHeight;

                        // Gọi callback để center ảnh trên canvas
                        centerImageCallback?.Invoke(imageWidth, imageHeight);

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

                var execId = node.LastExecutionId;
                var list = new List<string>();

                // 1. Quét tìm tất cả dữ liệu output của RenderNode trong ScopedOutputsHistoricalCache
                // bao gồm cả các luồng con (dispatch-0, dispatch-1, dispatch-2...) thuộc về lần chạy này
                if (!string.IsNullOrWhiteSpace(execId) &&
                    !string.IsNullOrWhiteSpace(node.RenderNodeId) &&
                    !string.IsNullOrWhiteSpace(node.RenderNodeOutputKey))
                {
                    string actualRunId = execId;
                    if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(execId, out var mappedRunId))
                    {
                        actualRunId = mappedRunId;
                    }

                    var prefix1 = execId + ":";
                    var prefix2 = actualRunId + ":";

                    foreach (var kv in WorkflowExecutionService.ScopedOutputsHistoricalCache)
                    {
                        if (string.Equals(kv.Key, execId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(kv.Key, actualRunId, StringComparison.OrdinalIgnoreCase) ||
                            kv.Key.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase) ||
                            kv.Key.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase))
                        {
                            if (kv.Value.TryGetValue(node.RenderNodeId, out var nodeOutputs) &&
                                nodeOutputs.TryGetValue(node.RenderNodeOutputKey, out var valStr) &&
                                !string.IsNullOrWhiteSpace(valStr) && valStr != "—")
                            {
                                valStr = valStr.Trim();
                                if (valStr.StartsWith("["))
                                {
                                    try
                                    {
                                        var parsedList = JsonSerializer.Deserialize<List<string>>(valStr);
                                        if (parsedList != null)
                                        {
                                            foreach (var item in parsedList)
                                            {
                                                var clean = CleanImageUrl(item);
                                                if (!string.IsNullOrWhiteSpace(clean) && !list.Contains(clean))
                                                    list.Add(clean);
                                            }
                                        }
                                    }
                                    catch { }
                                }
                                else
                                {
                                    var clean = CleanImageUrl(valStr);
                                    if (!string.IsNullOrWhiteSpace(clean) && !list.Contains(clean))
                                        list.Add(clean);
                                }
                            }
                        }
                    }
                }

                // 2. Fallback: Nếu trong HistoricalCache chưa thấy, dùng ResolveFromNodeIfAny
                if (list.Count == 0)
                {
                    var raw = ResolveFromNodeIfAny(host, node.RenderNodeId, node.RenderNodeOutputKey);
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        raw = raw.Trim();
                        if (raw.StartsWith("["))
                        {
                            try
                            {
                                var parsedList = JsonSerializer.Deserialize<List<string>>(raw);
                                if (parsedList != null)
                                {
                                    foreach (var item in parsedList)
                                    {
                                        var clean = CleanImageUrl(item);
                                        if (!string.IsNullOrWhiteSpace(clean) && !list.Contains(clean))
                                            list.Add(clean);
                                    }
                                }
                            }
                            catch
                            {
                                var inner = raw.Trim();
                                if (inner.StartsWith("[")) inner = inner.Substring(1);
                                if (inner.EndsWith("]")) inner = inner.Substring(0, inner.Length - 1);
                                var parts = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(p => p.Trim().Trim('"'))
                                                 .Where(p => !string.IsNullOrWhiteSpace(p));
                                foreach (var p in parts)
                                {
                                    var clean = CleanImageUrl(p);
                                    if (!string.IsNullOrWhiteSpace(clean) && !list.Contains(clean))
                                        list.Add(clean);
                                }
                            }
                        }
                        else
                        {
                            var clean = CleanImageUrl(raw);
                            if (!string.IsNullOrWhiteSpace(clean))
                                list.Add(clean);
                        }
                    }
                }

                if (list.Count == 0)
                {
                    MessageBox.Show("Output của Node render ảnh rỗng.",
                        "Image Processor", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // Lấy danh sách crops có LastExecutionId khớp với executionId hiện tại
                // Đây là các crops đã được xử lý trong lần chạy workflow này
                var targetCrops = node.Crops
                    .Where(c => !string.IsNullOrWhiteSpace(c.LastExecutionId) && 
                                string.Equals(c.LastExecutionId, execId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.Order)
                    .ToList();
                
                // Fallback: nếu không tìm thấy crop theo executionId, dùng crop đang active
                if (targetCrops.Count == 0)
                {
                    ImageCropRegion? activeCrop = null;
                    _currentCropRegionForIp.TryGetValue(node, out activeCrop);
                    
                    if (activeCrop != null)
                    {
                        targetCrops.Add(activeCrop);
                    }
                }
                
                // Fallback cuối: lấy tất cả crops theo thứ tự
                if (targetCrops.Count == 0 && node.Crops.Count > 0)
                {
                    targetCrops = node.Crops.OrderBy(c => c.Order).ToList();
                }

                if (targetCrops.Count == 0)
                {
                    MessageBox.Show("Không tìm thấy vùng crop để gán ảnh render.",
                        "Image Processor", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Map ảnh render vào crops và tạo radio variant child layers nếu ở EditorDoc:
                await System.Threading.Tasks.Task.Run(() =>
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var entry = list[i];
                        if (string.IsNullOrWhiteSpace(entry)) continue;

                        // Thử load như URL/path trước, nếu fail thì thử base64
                        BitmapImage? bmp = CreateBitmapFromUrlOrFile(entry);
                        if (bmp == null)
                        {
                            bmp = CreateBitmapFromBase64(entry);
                        }

                        if (bmp == null) continue;

                        var cropIndex = Math.Min(i, targetCrops.Count - 1);
                        var targetCrop = targetCrops[cropIndex];

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (!targetCrop.RenderedImages.Contains(bmp))
                            {
                                targetCrop.RenderedImages.Add(bmp);
                            }

                            // Tạo child variant layer cho Layer AI / EditorPanel nếu có EditorDoc
                            if (node.EditorDoc != null && node.EditorDoc.Layers.Count > 0)
                            {
                                var parentLayer = node.EditorDoc.ActiveLayer?.ParentLayer ?? node.EditorDoc.ActiveLayer ?? node.EditorDoc.Layers[0];
                                var existingVariant = parentLayer.ChildLayers.FirstOrDefault(c => c.Name == $"Layer AI {i + 1}");
                                if (existingVariant == null)
                                {
                                    var childLayer = new FlowMy.Models.ImageEditor.EditorLayer(parentLayer.Width, parentLayer.Height, $"Layer AI {parentLayer.ChildLayers.Count + 1}");
                                    childLayer.ParentLayer = parentLayer;
                                    childLayer.CopyFrom(bmp);
                                    parentLayer.ChildLayers.Add(childLayer);

                                    if (parentLayer.ActiveChildLayer == null)
                                    {
                                        parentLayer.ActiveChildLayer = childLayer;
                                    }
                                }
                            }
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

    }
}
