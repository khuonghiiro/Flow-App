using FlowMy.Helpers;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using FlowMy.Views.NodeControls;
using FlowMy.Views.NodeControls.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        private readonly EditorLayer _activeLayer;
        private readonly ImageProcessingNode _node;
        private readonly IWorkflowEditorHost _host;
        private readonly EditorDocument _doc;

        public LayerAiDialog(EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            InitializeComponent();
            Owner = owner;
            _activeLayer = activeLayer ?? throw new ArgumentNullException(nameof(activeLayer));
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));

            // Load saved settings
            LoadSavedSettings();

            // Load preview image
            UpdatePreviewImage();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CmbAspectRatio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelCustomSize == null) return;
            PanelCustomSize.Visibility = (CmbAspectRatio.SelectedIndex == 6) ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreviewImage();
        }

        private void TxtCustomSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreviewImage();
        }

        private void UpdatePreviewImage()
        {
            if (ImgPreview == null || _activeLayer == null) return;

            try
            {
                BitmapSource sourceImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
                var bounds = _activeLayer.ContentBounds;
                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    bounds = GetLayerContentBounds(_activeLayer.Bitmap);
                }
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, sourceImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, sourceImg.PixelHeight - 1);
                    int w = Math.Clamp((int)bounds.Width, 1, sourceImg.PixelWidth - x);
                    int h = Math.Clamp((int)bounds.Height, 1, sourceImg.PixelHeight - y);
                    if (w > 0 && h > 0 && (x > 0 || y > 0 || w < sourceImg.PixelWidth || h < sourceImg.PixelHeight))
                    {
                        sourceImg = new CroppedBitmap(sourceImg, new Int32Rect(x, y, w, h));
                    }
                }
                BitmapSource processedImg;

                int selectedIndex = CmbAspectRatio.SelectedIndex;
                if (selectedIndex == 0)
                {
                    processedImg = DrawPreviewImage(sourceImg, null, null, null, drawCheckerboard: true);
                }
                else if (selectedIndex == 6)
                {
                    int targetW = int.TryParse(TxtCustomWidth.Text, out var w) ? w : 512;
                    int targetH = int.TryParse(TxtCustomHeight.Text, out var h) ? h : 512;
                    processedImg = DrawPreviewImage(sourceImg, null, targetW, targetH, drawCheckerboard: true);
                }
                else
                {
                    double ratio = selectedIndex switch
                    {
                        1 => 16.0 / 9.0,
                        2 => 4.0 / 3.0,
                        3 => 1.0,
                        4 => 3.0 / 4.0,
                        5 => 9.0 / 16.0,
                        _ => 1.0
                    };
                    processedImg = DrawPreviewImage(sourceImg, ratio, null, null, drawCheckerboard: true);
                }

                ImgPreview.Source = processedImg;
            }
            catch { }
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            BtnSend.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            BtnSend.Content = "Đang xử lý...";

            try
            {
                BitmapSource sourceImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
                var bounds = _activeLayer.ContentBounds;
                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    bounds = GetLayerContentBounds(_activeLayer.Bitmap);
                }
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, sourceImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, sourceImg.PixelHeight - 1);
                    int w = Math.Clamp((int)bounds.Width, 1, sourceImg.PixelWidth - x);
                    int h = Math.Clamp((int)bounds.Height, 1, sourceImg.PixelHeight - y);
                    if (w > 0 && h > 0 && (x > 0 || y > 0 || w < sourceImg.PixelWidth || h < sourceImg.PixelHeight))
                    {
                        sourceImg = new CroppedBitmap(sourceImg, new Int32Rect(x, y, w, h));
                    }
                }
                BitmapSource processedImg;

                int selectedIndex = CmbAspectRatio.SelectedIndex;
                if (selectedIndex == 0)
                {
                    processedImg = DrawPreviewImage(sourceImg, null, null, null, drawCheckerboard: false);
                }
                else if (selectedIndex == 6)
                {
                    int targetW = int.TryParse(TxtCustomWidth.Text, out var w) ? w : 512;
                    int targetH = int.TryParse(TxtCustomHeight.Text, out var h) ? h : 512;
                    processedImg = DrawPreviewImage(sourceImg, null, targetW, targetH, drawCheckerboard: false);
                }
                else
                {
                    double ratio = selectedIndex switch
                    {
                        1 => 16.0 / 9.0,
                        2 => 4.0 / 3.0,
                        3 => 1.0,
                        4 => 3.0 / 4.0,
                        5 => 9.0 / 16.0,
                        _ => 1.0
                    };
                    processedImg = DrawPreviewImage(sourceImg, ratio, null, null, drawCheckerboard: false);
                }

                // Convert to base64
                var b64 = await Task.Run(() => ImageProcessorHelper.ToBase64(processedImg));

                // Bind outputs
                var cropBase64Port = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropBase64", StringComparison.OrdinalIgnoreCase));
                if (cropBase64Port != null) cropBase64Port.UserValueOverride = b64;

                _node.ProcessorPrompt = TxtPrompt.Text;
                var promptPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "prompt", StringComparison.OrdinalIgnoreCase));
                if (promptPort != null) promptPort.UserValueOverride = TxtPrompt.Text;

                int batchSize = CmbBatchSize.SelectedIndex + 1;
                _node.PromptSize = batchSize;
                var sizePort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "promptSize", StringComparison.OrdinalIgnoreCase));
                if (sizePort != null) sizePort.UserValueOverride = batchSize.ToString();

                var widthPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropWidth", StringComparison.OrdinalIgnoreCase));
                if (widthPort != null) widthPort.UserValueOverride = processedImg.PixelWidth.ToString();

                var heightPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropHeight", StringComparison.OrdinalIgnoreCase));
                if (heightPort != null) heightPort.UserValueOverride = processedImg.PixelHeight.ToString();

                string execId = Guid.NewGuid().ToString("N");
                _node.LastExecutionId = execId;
                var execIdPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "executionId", StringComparison.OrdinalIgnoreCase));
                if (execIdPort != null) execIdPort.UserValueOverride = execId;

                _node.IsVerticalMode = (selectedIndex == 4 || selectedIndex == 5);
                var aspectPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "aspectRatio", StringComparison.OrdinalIgnoreCase));
                if (aspectPort != null)
                {
                    string aspectStr = selectedIndex switch
                    {
                        1 => "16:9",
                        2 => "4:3",
                        3 => "1:1",
                        4 => "3:4",
                        5 => "9:16",
                        6 => "Free",
                        _ => "Default"
                    };
                    aspectPort.UserValueOverride = aspectStr;
                }

                // Refresh outputs list in node dialog immediately to reflect the generated overrides
                RefreshRelatedNodeDialogs();

                // Run workflow
                var vm = _host.ViewModel;
                if (vm != null)
                {
                    var vmType = vm.GetType();
                    var startTestMethod = vmType.GetMethod("StartTest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (startTestMethod != null)
                    {
                        if (startTestMethod.Invoke(vm, null) is Task t)
                        {
                            await t;
                        }
                    }
                }

                // Refresh outputs list again to show final outputs/execution IDs
                RefreshRelatedNodeDialogs();

                // Resolve AI outputs
                if (string.IsNullOrWhiteSpace(_node.RenderNodeId) || string.IsNullOrWhiteSpace(_node.RenderNodeOutputKey))
                {
                    MessageBox.Show("Chưa cấu hình Render Node Id hoặc Output Key trong thiết lập Node.", "AI Layer Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetButtons();
                    return;
                }

                // Định tuyến và tìm executionId thực tế được chạy trong workflow
                string actualRunId = execId;
                if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(execId, out var mappedRunId))
                {
                    actualRunId = mappedRunId;
                }

                var raw = ResolveFromHistoricalCache(_node.RenderNodeId, _node.RenderNodeOutputKey, actualRunId);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    raw = ResolveFromNodeIfAny(_host, _node.RenderNodeId, _node.RenderNodeOutputKey);
                }

                // Dọn dẹp cache của lần chạy này để tránh rò rỉ RAM
                WorkflowExecutionService.ExecutionIdMapping.TryRemove(execId, out _);
                WorkflowExecutionService.ScopedOutputsHistoricalCache.TryRemove(actualRunId, out _);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    MessageBox.Show("Node render chưa có dữ liệu output trả về từ AI.", "AI Layer Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetButtons();
                    return;
                }

                raw = raw.Trim();
                List<string> list = new List<string>();
                if (raw.StartsWith("["))
                {
                    try
                    {
                        list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
                    }
                    catch
                    {
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
                    MessageBox.Show("Output của Render Node rỗng.", "AI Layer Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetButtons();
                    return;
                }

                int countAdded = 0;
                foreach (var entry in list)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;

                    BitmapImage? bmp = CreateBitmapFromUrlOrFile(entry.Trim());
                    if (bmp == null)
                    {
                        bmp = CreateBitmapFromBase64(entry.Trim());
                    }

                    if (bmp != null)
                    {
                        countAdded++;
                        var destinationParent = _activeLayer.ParentLayer ?? _activeLayer;
                        // Create child layer
                        var childLayer = new EditorLayer(destinationParent.Width, destinationParent.Height, $"{destinationParent.Name} variant {destinationParent.ChildLayers.Count + 1}");
                        childLayer.ParentLayer = destinationParent;
                        childLayer.CopyFromPreserveAspectRatio(bmp);
                        destinationParent.ChildLayers.Add(childLayer);
                    }
                }

                if (countAdded > 0)
                {
                    var destinationParent = _activeLayer.ParentLayer ?? _activeLayer;
                    destinationParent.ActiveChildLayer = destinationParent.ChildLayers.Last();
                    _doc.ActiveLayer = destinationParent.ActiveChildLayer;
                    
                    // Sync active variants status
                    foreach (var child in destinationParent.ChildLayers)
                    {
                        child.IsActive = (child == destinationParent.ActiveChildLayer);
                        child.IsSelected = (child == destinationParent.ActiveChildLayer);
                    }
                    destinationParent.IsActive = false;
                    destinationParent.IsSelected = false;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "AI Layer Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetButtons();
            }
        }

        private void ResetButtons()
        {
            BtnSend.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            BtnSend.Content = "✨ Gửi AI";
        }

        private static BitmapSource DrawPreviewImage(BitmapSource src, double? targetRatio, int? customW, int? customH, bool drawCheckerboard)
        {
            int srcW = src.PixelWidth;
            int srcH = src.PixelHeight;
            
            int newW = srcW;
            int newH = srcH;
            double currentRatio = (double)srcW / srcH;

            BitmapSource imageToDraw = src;

            if (customW.HasValue && customH.HasValue)
            {
                newW = customW.Value;
                newH = customH.Value;
                var scale = new ScaleTransform((double)newW / srcW, (double)newH / srcH);
                imageToDraw = new TransformedBitmap(src, scale);
            }
            else if (targetRatio.HasValue)
            {
                double ratio = targetRatio.Value;
                if (currentRatio > ratio)
                {
                    newH = (int)Math.Ceiling(srcW / ratio);
                }
                else if (currentRatio < ratio)
                {
                    newW = (int)Math.Ceiling(srcH * ratio);
                }
            }

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                if (drawCheckerboard)
                {
                    var brush = Application.Current.TryFindResource("PsDarkCheckeredBrush") as Brush ?? Brushes.Black;
                    dc.DrawRectangle(brush, null, new Rect(0, 0, newW, newH));
                }
                else
                {
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, newW, newH));
                }

                // Center the image within the padded dimensions
                double x = (newW - imageToDraw.PixelWidth) / 2.0;
                double y = (newH - imageToDraw.PixelHeight) / 2.0;
                dc.DrawImage(imageToDraw, new Rect(x, y, imageToDraw.PixelWidth, imageToDraw.PixelHeight));
            }

            var rtb = new RenderTargetBitmap(newW, newH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
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

                if (File.Exists(value))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(value);
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                else if (Uri.TryCreate(value, UriKind.Absolute, out var uriResult) &&
                         (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = uriResult;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { }
            return null;
        }

        private static BitmapImage? CreateBitmapFromBase64(string base64)
        {
            try
            {
                string data = base64.Contains(',') ? base64.Split(',')[1] : base64;
                byte[] bytes = Convert.FromBase64String(data);
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { }
            return null;
        }

        private static string? ResolveFromHistoricalCache(string nodeId, string key, string executionId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(executionId)) return null;

            if (WorkflowExecutionService.ScopedOutputsHistoricalCache.TryGetValue(executionId, out var byNode) &&
                byNode.TryGetValue(nodeId, out var byKey) &&
                byKey.TryGetValue(key, out var value))
            {
                if (value == "—") return null;
                return value;
            }
            return null;
        }

        private void LoadSavedSettings()
        {
            if (_node == null) return;

            // Load prompt
            var savedPrompt = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "prompt", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedPrompt))
            {
                TxtPrompt.Text = savedPrompt;
            }
            else
            {
                TxtPrompt.Text = _node.ProcessorPrompt ?? string.Empty;
            }

            // Load batch size (promptSize)
            var savedSize = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "promptSize", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedSize) && int.TryParse(savedSize, out var bSize))
            {
                CmbBatchSize.SelectedIndex = Math.Clamp(bSize - 1, 0, 3);
            }
            else
            {
                CmbBatchSize.SelectedIndex = Math.Clamp(_node.PromptSize - 1, 0, 3);
            }

            // Load aspect ratio
            var savedAspect = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "aspectRatio", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedAspect))
            {
                CmbAspectRatio.SelectedIndex = savedAspect switch
                {
                    "16:9" => 1,
                    "4:3" => 2,
                    "1:1" => 3,
                    "3:4" => 4,
                    "9:16" => 5,
                    "Free" => 6,
                    _ => 0
                };
            }

            // Load custom width and height
            var bounds = _activeLayer.ContentBounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = GetLayerContentBounds(_activeLayer.Bitmap);
            }

            var savedWidth = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropWidth", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedWidth))
            {
                TxtCustomWidth.Text = savedWidth;
            }
            else
            {
                if (!bounds.IsEmpty && bounds.Width > 0)
                {
                    TxtCustomWidth.Text = ((int)bounds.Width).ToString();
                }
            }

            var savedHeight = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropHeight", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedHeight))
            {
                TxtCustomHeight.Text = savedHeight;
            }
            else
            {
                if (!bounds.IsEmpty && bounds.Height > 0)
                {
                    TxtCustomHeight.Text = ((int)bounds.Height).ToString();
                }
            }
        }

        private void RefreshRelatedNodeDialogs()
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is BaseNodeDialog baseDialog && baseDialog.DataContext is FlowMy.ViewModels.BaseNodeDialogViewModel dialogVm && dialogVm.Node == _node)
                {
                    baseDialog.Dispatcher.Invoke(() => baseDialog.RefreshOutputsUI());
                }
            }
        }

        private static Rect GetLayerContentBounds(BitmapSource bitmap)
        {
            if (bitmap == null) return Rect.Empty;
            try
            {
                int w = bitmap.PixelWidth;
                int h = bitmap.PixelHeight;
                if (w <= 0 || h <= 0) return Rect.Empty;

                int stride = w * 4;
                byte[] pixels = new byte[stride * h];
                bitmap.CopyPixels(pixels, stride, 0);

                int minX = w, maxX = 0, minY = h, maxY = 0;
                bool found = false;

                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = pixels[rowOffset + x * 4 + 3];
                        if (alpha > 5) // Ignore transparent edges
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            found = true;
                        }
                    }
                }

                if (!found)
                {
                    return new Rect(0, 0, w, h);
                }

                minX = Math.Max(0, minX - 4);
                minY = Math.Max(0, minY - 4);
                maxX = Math.Min(w - 1, maxX + 4);
                maxY = Math.Min(h - 1, maxY + 4);

                return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            catch
            {
                return new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
            }
        }
    }
}
