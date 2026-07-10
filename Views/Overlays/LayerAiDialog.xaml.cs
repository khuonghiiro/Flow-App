using FlowMy.Helpers;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using FlowMy.Views.NodeControls;
using FlowMy.Views.NodeControls.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
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
        public static readonly ConcurrentQueue<string> PendingExecutionIds = new ConcurrentQueue<string>();

        private readonly EditorLayer _activeLayer;
        private readonly ImageProcessingNode _node;
        private readonly IWorkflowEditorHost _host;
        private readonly EditorDocument _doc;

        // Secondary images management
        private class SecondaryImageItem
        {
            public BitmapSource? Bitmap { get; set; }
            public string? FilePath { get; set; }
            public bool IsSelected { get; set; } = true;
            public bool HasImage => Bitmap != null;
        }

        private readonly SecondaryImageItem[] _secondaryImages = new SecondaryImageItem[4]
        {
            new SecondaryImageItem(),
            new SecondaryImageItem(),
            new SecondaryImageItem(),
            new SecondaryImageItem()
        };

        // Cached references to UI elements for each slot
        private Border[] _slotBorders = null!;
        private Image[] _slotImages = null!;
        private TextBlock[] _slotPlaceholders = null!;
        private Border[] _slotChecks = null!;
        private Border[] _slotRemoves = null!;

        // Tab state
        private bool _isWebViewTabActive = false;
        private double _originalWidth;
        private double _originalHeight;

        public LayerAiDialog(EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            InitializeComponent();
            Owner = owner;
            _activeLayer = activeLayer ?? throw new ArgumentNullException(nameof(activeLayer));
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));

            _originalWidth = Width;
            _originalHeight = Height;

            // Initialize slot references
            _slotBorders = new[] { SlotBorder0, SlotBorder1, SlotBorder2, SlotBorder3 };
            _slotImages = new[] { SlotImage0, SlotImage1, SlotImage2, SlotImage3 };
            _slotPlaceholders = new[] { SlotPlaceholder0, SlotPlaceholder1, SlotPlaceholder2, SlotPlaceholder3 };
            _slotChecks = new[] { SlotCheck0, SlotCheck1, SlotCheck2, SlotCheck3 };
            _slotRemoves = new[] { SlotRemove0, SlotRemove1, SlotRemove2, SlotRemove3 };

            // Load saved settings
            LoadSavedSettings();

            // Load preview image
            UpdatePreviewImage();

            // Refresh all slots UI
            RefreshAllSlotsUI();
        }

        #region Header & Window Actions

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

        #endregion

        #region Tab Switching (Prompt / WebView)

        private void TabPrompt_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_isWebViewTabActive) return;
            _isWebViewTabActive = false;

            // Sync prompt text back from WebView layout
            TxtPrompt.Text = TxtPromptWv.Text;

            // Toggle layouts
            GridNormalLayout.Visibility = Visibility.Visible;
            GridWebViewLayout.Visibility = Visibility.Collapsed;

            // Tab header styling
            TabHeaderPrompt.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a4fffb0"));
            TabHeaderPrompt.BorderBrush = FindResource("AccentColor") as Brush;
            TabHeaderWebView.Background = Brushes.Transparent;
            TabHeaderWebView.BorderBrush = FindResource("BorderColor") as Brush;
            TabHeaderWebViewText.Foreground = FindResource("TextMuted") as Brush;

            // Restore original dialog size
            Width = _originalWidth;
            Height = _originalHeight;
            CenterOnScreen();
        }

        private void TabWebView_Click(object sender, MouseButtonEventArgs e)
        {
            if (_isWebViewTabActive) return;
            _isWebViewTabActive = true;

            // Sync prompt text to WebView layout
            TxtPromptWv.Text = TxtPrompt.Text;

            // Sync images to WebView layout mirrors
            SyncImagesToWebViewLayout();

            // Toggle layouts
            GridNormalLayout.Visibility = Visibility.Collapsed;
            GridWebViewLayout.Visibility = Visibility.Visible;

            // Tab header styling
            TabHeaderWebView.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a4fffb0"));
            TabHeaderWebView.BorderBrush = FindResource("AccentColor") as Brush;
            TabHeaderWebViewText.Foreground = FindResource("AccentColor") as Brush;
            TabHeaderPrompt.Background = Brushes.Transparent;
            TabHeaderPrompt.BorderBrush = FindResource("BorderColor") as Brush;

            // Expand dialog to fill screen
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;
            var targetW = Math.Min(screenW * 0.90, screenW - 60);
            var targetH = Math.Min(screenH * 0.85, screenH - 80);
            Width = Math.Max(_originalWidth, targetW);
            Height = Math.Max(_originalHeight, targetH);

            // Center on screen
            Left = (screenW - Width) / 2;
            Top = (screenH - Height) / 2;
        }

        /// <summary>Copy ảnh chính + ảnh phụ sang các Image element trong GridWebViewLayout.</summary>
        private void SyncImagesToWebViewLayout()
        {
            // Sync preview image
            ImgPreviewWv.Source = ImgPreview.Source;

            // Sync secondary image slots
            var wvImages = new[] { SlotImageWv0, SlotImageWv1, SlotImageWv2, SlotImageWv3 };
            var wvPlaceholders = new[] { SlotPlaceholderWv0, SlotPlaceholderWv1, SlotPlaceholderWv2, SlotPlaceholderWv3 };
            for (int i = 0; i < 4; i++)
            {
                wvImages[i].Source = _slotImages[i].Source;
                wvPlaceholders[i].Visibility = _secondaryImages[i] == null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void CenterOnScreen()
        {
            if (Owner != null)
            {
                Left = Owner.Left + (Owner.Width - Width) / 2;
                Top = Owner.Top + (Owner.Height - Height) / 2;
            }
        }

        #endregion

        #region Secondary Images Slots

        private void BtnAddSecondary_Click(object sender, RoutedEventArgs e)
        {
            // Find first empty slot
            int emptySlot = -1;
            for (int i = 0; i < 4; i++)
            {
                if (!_secondaryImages[i].HasImage)
                {
                    emptySlot = i;
                    break;
                }
            }

            if (emptySlot == -1)
            {
                MessageBox.Show("Đã đủ 4 ảnh phụ. Hãy xóa ảnh cũ trước.", "Ảnh phụ", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh phụ",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dlg.ShowDialog(this) == true)
            {
                int slotIdx = emptySlot;
                foreach (var file in dlg.FileNames)
                {
                    if (slotIdx >= 4) break;

                    // Find next empty slot
                    while (slotIdx < 4 && _secondaryImages[slotIdx].HasImage)
                        slotIdx++;
                    if (slotIdx >= 4) break;

                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(file);
                        bmp.EndInit();
                        bmp.Freeze();

                        _secondaryImages[slotIdx].Bitmap = bmp;
                        _secondaryImages[slotIdx].FilePath = file;
                        _secondaryImages[slotIdx].IsSelected = true;
                        slotIdx++;
                    }
                    catch { }
                }

                RefreshAllSlotsUI();
            }
        }

        private void Slot_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx < 0 || idx >= 4) return;

                if (_secondaryImages[idx].HasImage)
                {
                    // Toggle selection
                    _secondaryImages[idx].IsSelected = !_secondaryImages[idx].IsSelected;
                    RefreshSlotUI(idx);
                }
                else
                {
                    // Empty slot — open file dialog for this specific slot
                    var dlg = new OpenFileDialog
                    {
                        Title = "Chọn ảnh phụ",
                        Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
                        CheckFileExists = true,
                        Multiselect = false
                    };

                    if (dlg.ShowDialog(this) == true)
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = new Uri(dlg.FileName);
                            bmp.EndInit();
                            bmp.Freeze();

                            _secondaryImages[idx].Bitmap = bmp;
                            _secondaryImages[idx].FilePath = dlg.FileName;
                            _secondaryImages[idx].IsSelected = true;
                        }
                        catch { }
                        RefreshAllSlotsUI();
                    }
                }

                e.Handled = true;
            }
        }

        private void SlotRemove_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < 4)
                {
                    _secondaryImages[idx].Bitmap = null;
                    _secondaryImages[idx].FilePath = null;
                    _secondaryImages[idx].IsSelected = false;
                    RefreshAllSlotsUI();
                }
                e.Handled = true;
            }
        }

        private void Slot_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < 4)
                {
                    // Hover glow effect
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                    border.BorderThickness = new Thickness(2);

                    // Show remove button if has image
                    if (_secondaryImages[idx].HasImage)
                    {
                        _slotRemoves[idx].Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void Slot_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < 4)
                {
                    // Reset border based on selection state
                    if (_secondaryImages[idx].HasImage && _secondaryImages[idx].IsSelected)
                    {
                        border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                        border.BorderThickness = new Thickness(2);
                    }
                    else
                    {
                        border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));
                        border.BorderThickness = new Thickness(1.5);
                    }

                    // Hide remove button
                    _slotRemoves[idx].Visibility = Visibility.Collapsed;
                }
            }
        }

        private void RefreshAllSlotsUI()
        {
            for (int i = 0; i < 4; i++)
                RefreshSlotUI(i);
            UpdateSecondaryInfo();
        }

        private void RefreshSlotUI(int idx)
        {
            if (idx < 0 || idx >= 4) return;

            var item = _secondaryImages[idx];

            if (item.HasImage)
            {
                _slotImages[idx].Source = item.Bitmap;
                _slotPlaceholders[idx].Visibility = Visibility.Collapsed;
                _slotChecks[idx].Visibility = item.IsSelected ? Visibility.Visible : Visibility.Collapsed;

                if (item.IsSelected)
                {
                    _slotBorders[idx].BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                    _slotBorders[idx].BorderThickness = new Thickness(2);
                }
                else
                {
                    _slotBorders[idx].BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));
                    _slotBorders[idx].BorderThickness = new Thickness(1.5);
                }
            }
            else
            {
                _slotImages[idx].Source = null;
                _slotPlaceholders[idx].Visibility = Visibility.Visible;
                _slotChecks[idx].Visibility = Visibility.Collapsed;
                _slotBorders[idx].BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));
                _slotBorders[idx].BorderThickness = new Thickness(1.5);
            }
        }

        private void UpdateSecondaryInfo()
        {
            int total = _secondaryImages.Count(s => s.HasImage);
            int selected = _secondaryImages.Count(s => s.HasImage && s.IsSelected);
            TxtSecondaryInfo.Text = total > 0 ? $"Ảnh phụ: {selected}/{total} đã chọn" : "";
        }

        #endregion

        #region Aspect Ratio & Preview

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

                // Luôn tính bounds trực tiếp từ sourceImg (tránh mismatch giữa ContentBounds cache và OriginalTransformBitmap)
                var bounds = GetLayerContentBounds(sourceImg);
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, sourceImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, sourceImg.PixelHeight - 1);
                    int w = Math.Clamp((int)Math.Ceiling(bounds.Width), 1, sourceImg.PixelWidth - x);
                    int h = Math.Clamp((int)Math.Ceiling(bounds.Height), 1, sourceImg.PixelHeight - y);
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

        #endregion

        #region Send AI (BtnSend_Click)

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            BtnSend.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            BtnSend.Content = "Đang xử lý...";

            var destinationParent = _activeLayer.ParentLayer ?? _activeLayer;
            var placeholders = new List<EditorLayer>();

            try
            {
                BitmapSource sourceImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
                var bounds = GetLayerContentBounds(sourceImg);
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, sourceImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, sourceImg.PixelHeight - 1);
                    int w = Math.Clamp((int)Math.Ceiling(bounds.Width), 1, sourceImg.PixelWidth - x);
                    int h = Math.Clamp((int)Math.Ceiling(bounds.Height), 1, sourceImg.PixelHeight - y);
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

                // Convert main image to base64
                var b64 = await Task.Run(() => ImageProcessorHelper.ToBase64(processedImg));

                // Bind main image base64 output
                var cropBase64Port = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropBase64", StringComparison.OrdinalIgnoreCase));
                if (cropBase64Port != null) cropBase64Port.UserValueOverride = b64;

                var activePromptText = _isWebViewTabActive ? TxtPromptWv.Text : TxtPrompt.Text;
                _node.ProcessorPrompt = activePromptText;
                var promptPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "prompt", StringComparison.OrdinalIgnoreCase));
                if (promptPort != null) promptPort.UserValueOverride = activePromptText;

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

                PendingExecutionIds.Enqueue(execId);

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

                // *** NEW: Collect secondary images base64 and set listBase64 output ***
                await CollectAndSetListBase64Async();

                // Refresh outputs list in node dialog immediately to reflect the generated overrides
                RefreshRelatedNodeDialogs();

                // Create variant placeholders in parent's ChildLayers before starting workflow execution
                for (int i = 0; i < batchSize; i++)
                {
                    var placeholder = new EditorLayer(destinationParent.Width, destinationParent.Height, $"Layer AI {destinationParent.ChildLayers.Count + 1}");
                    placeholder.ParentLayer = destinationParent;
                    placeholder.IsLoading = true;
                    placeholder.StartLoadingTimer();
                    destinationParent.ChildLayers.Add(placeholder);
                    placeholders.Add(placeholder);
                }

                // Notify HasChildren changed on parent so collapse toggle appears
                destinationParent.OnPropertyChanged(nameof(EditorLayer.HasChildren));

                // Refresh main panel immediately to render loading placeholders in the layers ListBox
                var editorPanel = FindVisualChild<ImageEditorPanel>(this.Owner);
                editorPanel?.RefreshLayersList();

                // Close dialog immediately — workflow runs in background, results applied to placeholders
                DialogResult = true;
                Close();

                // Capture references needed for background processing
                var activeLayerRef = _activeLayer;
                var docRef = _doc;
                var nodeRef = _node;
                var hostRef = _host;
                var ownerRef = this.Owner;

                // Fire-and-forget: run workflow, then process results on UI thread
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Run workflow on background thread via reflection
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            var vm = hostRef.ViewModel;
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
                        }).Task.Unwrap();

                        // Process results on UI thread
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                // Refresh outputs list again to show final outputs/execution IDs
                                RefreshRelatedNodeDialogs();

                                // Resolve AI outputs
                                if (string.IsNullOrWhiteSpace(nodeRef.RenderNodeId) || string.IsNullOrWhiteSpace(nodeRef.RenderNodeOutputKey))
                                {
                                    CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                                    return;
                                }

                                // Tìm executionId thực tế được chạy trong workflow
                                string actualRunId = execId;
                                if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(execId, out var mappedRunId))
                                {
                                    actualRunId = mappedRunId;
                                }

                                var raw = ResolveFromHistoricalCache(nodeRef.RenderNodeId, nodeRef.RenderNodeOutputKey, actualRunId);
                                if (string.IsNullOrWhiteSpace(raw))
                                {
                                    raw = ResolveFromNodeIfAny(hostRef, nodeRef.RenderNodeId, nodeRef.RenderNodeOutputKey);
                                }

                                // Dọn dẹp cache của lần chạy này để tránh rò rỉ RAM
                                WorkflowExecutionService.ExecutionIdMapping.TryRemove(execId, out _);
                                WorkflowExecutionService.ScopedOutputsHistoricalCache.TryRemove(actualRunId, out _);
                                
                                var childPrefix = actualRunId + ":";
                                var childrenKeys = WorkflowExecutionService.ScopedOutputsHistoricalCache.Keys
                                    .Where(k => k.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                foreach (var childKey in childrenKeys)
                                {
                                    WorkflowExecutionService.ScopedOutputsHistoricalCache.TryRemove(childKey, out _);
                                }

                                if (string.IsNullOrWhiteSpace(raw))
                                {
                                    CleanupPlaceholders(placeholders, destinationParent, ownerRef);
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
                                    CleanupPlaceholders(placeholders, destinationParent, ownerRef);
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
                                            childLayer = placeholders[placeholderIndex];
                                            childLayer.IsLoading = false;
                                            childLayer.StopLoadingTimer();
                                        }
                                        else
                                        {
                                            childLayer = new EditorLayer(destinationParent.Width, destinationParent.Height, $"Layer AI {destinationParent.ChildLayers.Count + 1}");
                                            childLayer.ParentLayer = destinationParent;
                                            destinationParent.ChildLayers.Add(childLayer);
                                        }

                                        ProcessAndApplyAiImage(childLayer, bmp, activeLayerRef, bounds, targetRatio, customW, customH);
                                        countAdded++;
                                        placeholderIndex++;
                                    }
                                }

                                // Remove any unused placeholders
                                for (int i = placeholders.Count - 1; i >= placeholderIndex; i--)
                                {
                                    destinationParent.ChildLayers.Remove(placeholders[i]);
                                }

                                if (countAdded > 0)
                                {
                                    destinationParent.ActiveChildLayer = destinationParent.ChildLayers.Last();
                                    docRef.ActiveLayer = destinationParent.ActiveChildLayer;

                                    foreach (var child in destinationParent.ChildLayers)
                                    {
                                        child.IsActive = (child == destinationParent.ActiveChildLayer);
                                        child.IsSelected = (child == destinationParent.ActiveChildLayer);
                                    }
                                    destinationParent.IsActive = false;
                                    destinationParent.IsSelected = false;
                                }

                                // Refresh panel to show AI results and trigger re-composite
                                var panel = FindVisualChild<ImageEditorPanel>(ownerRef);
                                panel?.RefreshLayersList();
                                panel?.OnDocumentModified();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine("AI result processing error: " + ex.Message);
                                CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("AI workflow error: " + ex.Message);
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                // Pre-workflow error (e.g. image processing) — mark placeholders as error
                CleanupPlaceholders(placeholders, destinationParent, this.Owner);

                MessageBox.Show("Lỗi thực thi AI: " + ex.Message, "AI Layer Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetButtons();
            }
        }

        /// <summary>
        /// Collect selected secondary images, convert to base64, and set the listBase64 output.
        /// </summary>
        private async Task CollectAndSetListBase64Async()
        {
            var selectedImages = _secondaryImages
                .Where(s => s.HasImage && s.IsSelected && s.Bitmap != null)
                .Select(s => s.Bitmap!)
                .ToList();

            if (selectedImages.Count == 0)
            {
                // Set empty array
                var listBase64Port = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "listBase64", StringComparison.OrdinalIgnoreCase));
                if (listBase64Port != null) listBase64Port.UserValueOverride = "[]";
                return;
            }

            var base64List = new List<string>();
            foreach (var bmp in selectedImages)
            {
                var b64 = await Task.Run(() => ImageProcessorHelper.ToBase64(bmp));
                base64List.Add(b64);
            }

            // Serialize as JSON array
            var jsonArray = System.Text.Json.JsonSerializer.Serialize(base64List);
            var port = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "listBase64", StringComparison.OrdinalIgnoreCase));
            if (port != null) port.UserValueOverride = jsonArray;
        }

        #endregion

        #region Helpers

        private void CleanupPlaceholders(List<EditorLayer> placeholders, EditorLayer parent, Window? owner)
        {
            foreach (var placeholder in placeholders)
            {
                // Mark as error state instead of removing — user can delete manually
                placeholder.IsLoading = false;
                placeholder.StopLoadingTimer(isError: true);
                placeholder.IsLoadingError = true;
                placeholder.Name = placeholder.Name + " (Lỗi)";
            }
            if (owner != null)
            {
                var panel = FindVisualChild<ImageEditorPanel>(owner);
                panel?.RefreshLayersList();
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

            // 1. Thử lấy trực tiếp bằng executionId chính xác
            if (WorkflowExecutionService.ScopedOutputsHistoricalCache.TryGetValue(executionId, out var byNode) &&
                byNode.TryGetValue(nodeId, out var byKey) &&
                byKey.TryGetValue(key, out var value))
            {
                if (value != "—" && !string.IsNullOrWhiteSpace(value)) return value;
            }

            // 2. Nếu không thấy, duyệt qua cache tìm các run con (ví dụ: executionId + ":dispatch-..." hoặc executionId + ":at-manual-...")
            var prefix = executionId + ":";
            foreach (var kv in WorkflowExecutionService.ScopedOutputsHistoricalCache)
            {
                if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.Value.TryGetValue(nodeId, out var childKey) &&
                        childKey.TryGetValue(key, out var childVal))
                    {
                        if (childVal != "—" && !string.IsNullOrWhiteSpace(childVal)) return childVal;
                    }
                }
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
            BitmapSource sourceImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
            var bounds = GetLayerContentBounds(sourceImg);

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
            BitmapSource resizedAi = ResizeBitmapHighQuality(aiBmp, newW, newH, uniformToFill: !customW.HasValue);

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
                croppedAiRegion = ResizeBitmapHighQuality(croppedAiRegion, srcW, srcH, uniformToFill: false);
            }

            // 6. Mask the AI pixels using the original layer's alpha channel to preserve lasso/polygon shapes
            var converted = croppedAiRegion;
            if (croppedAiRegion.Format != PixelFormats.Bgra32)
            {
                converted = new FormatConvertedBitmap(croppedAiRegion, PixelFormats.Bgra32, null, 0);
            }

            double parentX = 0;
            double parentY = 0;
            if (activeLayer.OriginalTransformBitmap != null)
            {
                parentX = activeLayer.ContentBounds.X;
                parentY = activeLayer.ContentBounds.Y;
            }
            int posX = (int)Math.Clamp(parentX + originalBounds.X, 0, childLayer.Width - 1);
            int posY = (int)Math.Clamp(parentY + originalBounds.Y, 0, childLayer.Height - 1);
            int finalW = Math.Clamp(srcW, 1, childLayer.Width - posX);
            int finalH = Math.Clamp(srcH, 1, childLayer.Height - posY);

            // Resize converted to match final clamped bounds if needed
            if (converted.PixelWidth != finalW || converted.PixelHeight != finalH)
            {
                converted = ResizeBitmapHighQuality(converted, finalW, finalH, uniformToFill: false);
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

        private static BitmapSource ResizeBitmapHighQuality(BitmapSource source, int targetWidth, int targetHeight, bool uniformToFill = false)
        {
            if (source.PixelWidth == targetWidth && source.PixelHeight == targetHeight)
            {
                return source;
            }

            int drawW = targetWidth;
            int drawH = targetHeight;
            double x = 0;
            double y = 0;

            if (uniformToFill)
            {
                double scale = Math.Max((double)targetWidth / source.PixelWidth, (double)targetHeight / source.PixelHeight);
                drawW = (int)Math.Ceiling(source.PixelWidth * scale);
                drawH = (int)Math.Ceiling(source.PixelHeight * scale);
                x = (targetWidth - drawW) / 2.0;
                y = (targetHeight - drawH) / 2.0;
            }

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
                dc.DrawImage(source, new Rect(x, y, drawW, drawH));
            }

            var rtb = new RenderTargetBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        #endregion
    }
}
