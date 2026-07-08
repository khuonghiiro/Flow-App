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

            var destinationParent = _activeLayer.ParentLayer ?? _activeLayer;
            var placeholders = new List<EditorLayer>();
            bool success = false;

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

                double? targetRatio = null;
                int? customW = null;
                int? customH = null;

                int selectedIndex = CmbAspectRatio.SelectedIndex;
                if (selectedIndex == 0)
                {
                    processedImg = DrawPreviewImage(sourceImg, null, null, null, drawCheckerboard: false);
                }
                else if (selectedIndex == 6)
                {
                    customW = int.TryParse(TxtCustomWidth.Text, out var w) ? w : 512;
                    customH = int.TryParse(TxtCustomHeight.Text, out var h) ? h : 512;
                    processedImg = DrawPreviewImage(sourceImg, null, customW, customH, drawCheckerboard: false);
                }
                else
                {
                    targetRatio = selectedIndex switch
                    {
                        1 => 16.0 / 9.0,
                        2 => 4.0 / 3.0,
                        3 => 1.0,
                        4 => 3.0 / 4.0,
                        5 => 9.0 / 16.0,
                        _ => 1.0
                    };
                    processedImg = DrawPreviewImage(sourceImg, targetRatio, null, null, drawCheckerboard: false);
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

                // Create variant placeholders in parent's ChildLayers before starting workflow execution
                for (int i = 0; i < batchSize; i++)
                {
                    var placeholder = new EditorLayer(destinationParent.Width, destinationParent.Height, $"{destinationParent.Name} variant {destinationParent.ChildLayers.Count + 1}");
                    placeholder.ParentLayer = destinationParent;
                    placeholder.IsLoading = true;
                    destinationParent.ChildLayers.Add(placeholder);
                    placeholders.Add(placeholder);
                }

                // Refresh main panel immediately to render loading placeholders in the layers ListBox
                var editorPanel = FindVisualChild<ImageEditorPanel>(this.Owner);
                editorPanel?.RefreshLayersList();

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

                // Tìm executionId thực tế được chạy trong workflow
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
                int placeholderIndex = 0;
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
                        EditorLayer childLayer;
                        if (placeholderIndex < placeholders.Count)
                        {
                            // Overwrite the placeholder layer at this index
                            childLayer = placeholders[placeholderIndex];
                            childLayer.IsLoading = false;
                        }
                        else
                        {
                            // Create a new child layer if we received more variant images than batch size
                            childLayer = new EditorLayer(destinationParent.Width, destinationParent.Height, $"{destinationParent.Name} variant {destinationParent.ChildLayers.Count + 1}");
                            childLayer.ParentLayer = destinationParent;
                            destinationParent.ChildLayers.Add(childLayer);
                        }

                        ProcessAndApplyAiImage(childLayer, bmp, _activeLayer, bounds, targetRatio, customW, customH);
                        countAdded++;
                        placeholderIndex++;
                    }
                }

                // Remove any unused placeholders (e.g. if the AI returned fewer images than requested)
                for (int i = placeholders.Count - 1; i >= placeholderIndex; i--)
                {
                    destinationParent.ChildLayers.Remove(placeholders[i]);
                }

                if (countAdded > 0)
                {
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

                success = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi thực thi AI: " + ex.Message, "AI Layer Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetButtons();
            }
            finally
            {
                if (!success)
                {
                    // Clean up placeholders in case of failure or early abort
                    foreach (var placeholder in placeholders)
                    {
                        destinationParent.ChildLayers.Remove(placeholder);
                    }
                    var editorPanel = FindVisualChild<ImageEditorPanel>(this.Owner);
                    editorPanel?.RefreshLayersList();
                }
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

                return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            catch
            {
                return new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                {
                    return t;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void ProcessAndApplyAiImage(
            EditorLayer childLayer, 
            BitmapSource aiBmp, 
            EditorLayer activeLayer, 
            Rect originalBounds, 
            double? targetRatio, 
            int? customW, 
            int? customH)
        {
            // 1. Get original crop dimensions
            BitmapSource sourceImg = activeLayer.OriginalTransformBitmap ?? activeLayer.Bitmap;
            int srcW = (int)originalBounds.Width;
            int srcH = (int)originalBounds.Height;
            if (srcW <= 0) srcW = sourceImg.PixelWidth;
            if (srcH <= 0) srcH = sourceImg.PixelHeight;

            // 2. Compute the newW and newH (the size of the image sent to AI)
            int newW = srcW;
            int newH = srcH;
            double currentRatio = (double)srcW / srcH;

            if (customW.HasValue && customH.HasValue)
            {
                newW = customW.Value;
                newH = customH.Value;
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

            // 3. Resize AI image to newW x newH with High Quality
            BitmapSource resizedAi;
            if (aiBmp.PixelWidth == newW && aiBmp.PixelHeight == newH)
            {
                resizedAi = aiBmp;
            }
            else
            {
                var scaleX = (double)newW / aiBmp.PixelWidth;
                var scaleY = (double)newH / aiBmp.PixelHeight;
                var scale = new ScaleTransform(scaleX, scaleY);
                resizedAi = new TransformedBitmap(aiBmp, scale);
            }

            // 4. Calculate crop offsets (where the original crop region is located in the newW x newH image)
            double xOffset = 0;
            double yOffset = 0;
            BitmapSource croppedAiRegion;

            if (customW.HasValue && customH.HasValue)
            {
                // For custom size, the image was scaled (stretched) to customW x customH.
                // So we just take the whole image, and it will be scaled back to srcW x srcH.
                croppedAiRegion = resizedAi;
            }
            else
            {
                xOffset = (newW - srcW) / 2.0;
                yOffset = (newH - srcH) / 2.0;
                int cropX = Math.Clamp((int)Math.Round(xOffset), 0, newW - 1);
                int cropY = Math.Clamp((int)Math.Round(yOffset), 0, newH - 1);
                int cropW = Math.Clamp(srcW, 1, newW - cropX);
                int cropH = Math.Clamp(srcH, 1, newH - cropY);
                
                croppedAiRegion = new CroppedBitmap(resizedAi, new Int32Rect(cropX, cropY, cropW, cropH));
            }

            // 5. If it needs to be scaled back to original bounds (for custom size, etc.)
            if (croppedAiRegion.PixelWidth != srcW || croppedAiRegion.PixelHeight != srcH)
            {
                var scaleX = (double)srcW / croppedAiRegion.PixelWidth;
                var scaleY = (double)srcH / croppedAiRegion.PixelHeight;
                croppedAiRegion = new TransformedBitmap(croppedAiRegion, new ScaleTransform(scaleX, scaleY));
            }

            // 6. Mask the AI pixels using the original layer's alpha channel to preserve lasso/polygon shapes
            var converted = croppedAiRegion;
            if (croppedAiRegion.Format != PixelFormats.Bgra32)
            {
                converted = new FormatConvertedBitmap(croppedAiRegion, PixelFormats.Bgra32, null, 0);
            }

            int posX = (int)Math.Clamp(originalBounds.X, 0, childLayer.Width - 1);
            int posY = (int)Math.Clamp(originalBounds.Y, 0, childLayer.Height - 1);
            int finalW = Math.Clamp(srcW, 1, childLayer.Width - posX);
            int finalH = Math.Clamp(srcH, 1, childLayer.Height - posY);

            // Resize converted to match final clamped bounds if needed
            if (converted.PixelWidth != finalW || converted.PixelHeight != finalH)
            {
                converted = new TransformedBitmap(converted, new ScaleTransform((double)finalW / converted.PixelWidth, (double)finalH / converted.PixelHeight));
            }

            var aiPixels = new byte[finalW * 4 * finalH];
            converted.CopyPixels(aiPixels, finalW * 4, 0);

            var maskPixels = new byte[finalW * 4 * finalH];
            activeLayer.Bitmap.CopyPixels(new Int32Rect(posX, posY, finalW, finalH), maskPixels, finalW * 4, 0);

            for (int i = 0; i < aiPixels.Length; i += 4)
            {
                aiPixels[i + 3] = maskPixels[i + 3];
            }

            var maskedBmp = new WriteableBitmap(finalW, finalH, 96, 96, PixelFormats.Bgra32, null);
            maskedBmp.WritePixels(new Int32Rect(0, 0, finalW, finalH), aiPixels, finalW * 4, 0);

            // Render into the destination layer's WriteableBitmap
            var drawingVisual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.HighQuality);
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Draw the transparent background (clear the layer)
                drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, childLayer.Width, childLayer.Height));
                // Draw the masked processed AI cropped region at the exact original position
                drawingContext.DrawImage(maskedBmp, new Rect(posX, posY, finalW, finalH));
            }

            var rtb = new RenderTargetBitmap(childLayer.Width, childLayer.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            var finalBmp = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
            var stride = childLayer.Width * 4;
            var pixels = new byte[stride * childLayer.Height];
            finalBmp.CopyPixels(pixels, stride, 0);

            childLayer.Bitmap.WritePixels(new Int32Rect(0, 0, childLayer.Width, childLayer.Height), pixels, stride, 0);
            
            // Set OriginalTransformBitmap and ContentBounds so that transform tool works properly
            childLayer.OriginalTransformBitmap = new WriteableBitmap(maskedBmp);
            childLayer.ContentBounds = new Rect(posX, posY, finalW, finalH);
            
            childLayer.InvalidateThumbnail();
        }
    }
}
