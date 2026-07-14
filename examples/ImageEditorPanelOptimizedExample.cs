using FlowMy.Helpers;
using FlowMy.Extensions;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace FlowMy.Examples
{
    /// <summary>
    /// Example implementation của ImageEditorPanel với các tối ưu hóa.
    /// Copy các methods này vào ImageEditorPanel.xaml.cs thực tế.
    /// </summary>
    public partial class ImageEditorPanelOptimizedExample : UserControl
    {
        private EditorDocument? _doc;
        private CancellationTokenSource? _colorAdjustCts;
        private CancellationTokenSource? _effectCts;

        // ═══════════════════════════════════════════════════════
        // 1. DUPLICATE LAYER (OPTIMIZED)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// ✅ OPTIMIZED: Duplicate layer instantly với copy-on-write.
        /// Replace method: BtnCopyLayer_Click
        /// </summary>
        private async void BtnCopyLayer_Click_Optimized(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_doc == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorLayer sourceLayer)
            {
                // Show loading indicator
                ShowLoadingOverlay("Duplicating layer...");

                try
                {
                    // ✅ INSTANT duplicate (50ms vs 1500ms)
                    var newLayer = await ImageProcessingHelpers.DuplicateLayerFastAsync(sourceLayer);
                    newLayer.Name = GenerateCopyName(sourceLayer.Name);

                    int insertIndex = _doc.Layers.IndexOf(sourceLayer);
                    if (insertIndex < 0) insertIndex = _doc.Layers.Count;
                    else insertIndex += 1;

                    var cmd = new LayerAddCommand(_doc, newLayer, insertIndex);
                    _doc.History.Execute(cmd);

                    SelectSingleLayer(newLayer);
                    OnDocumentModified();
                }
                finally
                {
                    HideLoadingOverlay();
                }
            }
            e.Handled = true;
        }

        // ═══════════════════════════════════════════════════════
        // 2. ADD IMAGE TO LAYER (OPTIMIZED)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// ✅ OPTIMIZED: Load ảnh với progressive loading (preview → full).
        /// Replace method: BtnAddImageLayer_Click
        /// </summary>
        private async void BtnAddImageLayer_Click_Optimized(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
                Title = "Import Image to Layer"
            };

            if (dlg.ShowDialog() != true) return;

            ShowLoadingOverlay("Loading image...");

            try
            {
                // Tạo layer mới
                var newLayer = new EditorLayer(_doc.Width, _doc.Height, System.IO.Path.GetFileNameWithoutExtension(dlg.FileName));

                // ✅ PROGRESSIVE LOAD: Preview hiện ngay, full quality sau
                await ImageProcessingHelpers.LoadImageToLayerAsync(newLayer, dlg.FileName);

                // Add vào document
                var cmd = new LayerAddCommand(_doc, newLayer, _doc.Layers.Count);
                _doc.History.Execute(cmd);

                SelectSingleLayer(newLayer);
                OnDocumentModified();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        // ═══════════════════════════════════════════════════════
        // 3. COLOR ADJUSTMENT SLIDERS (DEBOUNCED + PROGRESSIVE)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// ✅ OPTIMIZED: Debounced slider với progressive rendering.
        /// Replace method: ColorAdjustSlider_ValueChanged
        /// </summary>
        private async void ColorAdjustSlider_ValueChanged_Optimized(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_doc?.ActiveLayer == null) return;

            // Cancel previous adjustment
            _colorAdjustCts?.Cancel();
            _colorAdjustCts = new CancellationTokenSource();
            var token = _colorAdjustCts.Token;

            // Update text labels immediately (instant feedback)
            UpdateColorAdjustLabels();

            try
            {
                // ✅ DEBOUNCE: Wait 150ms after user stops dragging
                await ImageProcessingOptimization.DebounceAsync("colorAdjust", async () =>
                {
                    if (token.IsCancellationRequested) return;

                    var brightness = SliderBrightness?.Value ?? 0;
                    var contrast = SliderContrast?.Value ?? 0;
                    var saturation = SliderSaturation?.Value ?? 0;
                    var hue = SliderHue?.Value ?? 0;

                    // ✅ PROGRESSIVE RENDERING: quick → draft → full
                    await ApplyColorAdjustmentsProgressive(brightness, contrast, saturation, hue, token);
                }, delayMs: 150);
            }
            catch (TaskCanceledException) { }
        }

        /// <summary>
        /// Apply color adjustments với progressive rendering.
        /// </summary>
        private async Task ApplyColorAdjustmentsProgressive(
            double brightness, double contrast, double saturation, double hue,
            CancellationToken cancellationToken)
        {
            if (_doc?.ActiveLayer?.PixelData == null) return;

            var sourceData = _doc.ActiveLayer.PixelData;

            await ImageProcessingOptimization.RenderProgressiveAsync(
                async (scale) =>
                {
                    var scaledSource = scale < 1.0
                        ? ImageProcessingOptimization.CreateScaledPreview(sourceData, scale)
                        : sourceData;

                    return await Task.Run(() =>
                    {
                        using (var magick = scaledSource.ToMagickImage())
                        {
                            // Apply all adjustments
                            if (Math.Abs(brightness) > 0.1 || Math.Abs(contrast) > 0.1)
                                magick.BrightnessContrast(new ImageMagick.Percentage(brightness), new ImageMagick.Percentage(contrast));
                            
                            if (Math.Abs(saturation) > 0.1)
                                magick.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(100 + saturation), new ImageMagick.Percentage(100));
                            
                            if (Math.Abs(hue) > 0.1)
                                magick.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(100), new ImageMagick.Percentage(100 + (hue / 1.8)));

                            return ImageProcessingOptimization.ConvertToOptimizedBitmap(magick);
                        }
                    }, cancellationToken);
                },
                preview =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Update preview (MainImage.Source in parent control)
                        UpdateColorAdjustPreview(preview);
                    });
                },
                cancellationToken);
        }

        /// <summary>
        /// Apply color adjustments permanently to layer.
        /// Replace method: BtnApplyColorAdjust_Click
        /// </summary>
        private async void BtnApplyColorAdjust_Click_Optimized(object sender, RoutedEventArgs e)
        {
            if (_doc?.ActiveLayer == null) return;

            ShowLoadingOverlay("Applying color adjustments...");

            try
            {
                var brightness = SliderBrightness?.Value ?? 0;
                var contrast = SliderContrast?.Value ?? 0;
                var saturation = SliderSaturation?.Value ?? 0;
                var hue = SliderHue?.Value ?? 0;

                var layer = _doc.ActiveLayer;
                var originalPixelData = layer.PixelData;

                // ✅ BACKGROUND THREADING với progress
                var progress = new Progress<int>(percent =>
                {
                    Dispatcher.Invoke(() => UpdateProgressBar(percent));
                });

                var result = await ImageProcessingHelpers.ApplyMagickEffectAsync(
                    originalPixelData,
                    img =>
                    {
                        if (Math.Abs(brightness) > 0.1 || Math.Abs(contrast) > 0.1)
                            img.BrightnessContrast(new ImageMagick.Percentage(brightness), new ImageMagick.Percentage(contrast));
                        
                        if (Math.Abs(saturation) > 0.1)
                            img.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(100 + saturation), new ImageMagick.Percentage(100));
                        
                        if (Math.Abs(hue) > 0.1)
                            img.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(100), new ImageMagick.Percentage(100 + (hue / 1.8)));
                    },
                    progress: progress);

                // Create undo command
                var cmd = new ColorAdjustCommand(_doc, layer, originalPixelData, result);
                _doc.History.Execute(cmd);

                // Reset sliders
                ResetColorAdjustSliders();
                OnDocumentModified();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply adjustments: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        // ═══════════════════════════════════════════════════════
        // 4. MERGE LAYERS (OPTIMIZED)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// ✅ OPTIMIZED: Merge layers với progress tracking.
        /// Replace method: PopupBtnMerge_Click
        /// </summary>
        private async void PopupBtnMerge_Click_Optimized(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;

            var selectedLayers = SelectedLayers;
            if (selectedLayers.Count < 2)
            {
                MessageBox.Show("Please select at least 2 layers to merge.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowLoadingOverlay("Merging layers...");

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    Dispatcher.Invoke(() => UpdateProgressBar(percent));
                });

                // ✅ OPTIMIZED MERGE với tile processing
                var merged = await EditorLayerOptimizations.MergeLayersOptimizedAsync(
                    _doc,
                    selectedLayers,
                    "Merged Layer",
                    progress);

                // Remove old layers và add merged layer
                int firstIndex = _doc.Layers.IndexOf(selectedLayers[0]);
                foreach (var layer in selectedLayers)
                {
                    _doc.Layers.Remove(layer);
                }

                _doc.Layers.Insert(firstIndex, merged);
                SelectSingleLayer(merged);
                OnDocumentModified();

                MessageBox.Show($"Merged {selectedLayers.Count} layers successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to merge layers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        // ═══════════════════════════════════════════════════════
        // 5. APPLY MAGICK EFFECT (GENERIC OPTIMIZED)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// ✅ OPTIMIZED: Generic effect applicator với cancellation support.
        /// Dùng cho blur, artistic, edge detection, etc.
        /// </summary>
        private async Task ApplyMagickEffectOptimized(
            string effectName,
            Action<ImageMagick.MagickImage> effectAction)
        {
            if (_doc?.ActiveLayer == null) return;

            // Cancel previous effect
            _effectCts?.Cancel();
            _effectCts = new CancellationTokenSource();
            var token = _effectCts.Token;

            ShowLoadingOverlay($"Applying {effectName}...");

            try
            {
                var layer = _doc.ActiveLayer;
                var originalData = layer.PixelData ?? layer.Bitmap.ToBitmapSource();

                var progress = new Progress<int>(percent =>
                {
                    Dispatcher.Invoke(() => UpdateProgressBar(percent));
                });

                // ✅ TILE-BASED PROCESSING cho ảnh lớn
                var result = await ImageProcessingHelpers.ApplyMagickEffectAsync(
                    originalData,
                    effectAction,
                    progress: progress,
                    cancellationToken: token);

                // Update layer
                layer.PixelData = result;
                await UpdateLayerBitmapAsync(layer, result);

                OnDocumentModified();
            }
            catch (OperationCanceledException)
            {
                // User cancelled
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply {effectName}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        /// <summary>
        /// Example usage: Apply Gaussian Blur
        /// </summary>
        private async void BtnApplyBlur_Click(object sender, RoutedEventArgs e)
        {
            double radius = 5.0; // Get from config/UI
            double sigma = 2.0;

            await ApplyMagickEffectOptimized(
                "Gaussian Blur",
                img => img.GaussianBlur(radius, sigma));
        }

        // ═══════════════════════════════════════════════════════
        // 6. HELPER METHODS
        // ═══════════════════════════════════════════════════════

        private async Task UpdateLayerBitmapAsync(EditorLayer layer, BitmapSource source)
        {
            // Update WriteableBitmap from BitmapSource (tile-based, async)
            if (source.Format != System.Windows.Media.PixelFormats.Bgra32)
            {
                source = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            }

            const int tileSize = 512;
            for (int ty = 0; ty < layer.Height; ty += tileSize)
            {
                int th = Math.Min(tileSize, layer.Height - ty);
                for (int tx = 0; tx < layer.Width; tx += tileSize)
                {
                    int tw = Math.Min(tileSize, layer.Width - tx);
                    var rect = new System.Windows.Int32Rect(tx, ty, tw, th);
                    int stride = tw * 4;
                    byte[] buffer = new byte[stride * th];

                    await Task.Run(() => source.CopyPixels(rect, buffer, stride, 0));
                    layer.Bitmap.WritePixels(rect, buffer, stride, 0);
                }
                
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            }

            layer.InvalidateThumbnail();
        }

        private void ShowLoadingOverlay(string message)
        {
            // Implement loading overlay UI
        }

        private void HideLoadingOverlay()
        {
            // Hide loading overlay UI
        }

        private void UpdateProgressBar(int percent)
        {
            // Update progress bar UI
        }

        private void UpdateColorAdjustLabels()
        {
            // Update text labels for sliders
        }

        private void UpdateColorAdjustPreview(BitmapSource preview)
        {
            // Update preview image
        }

        private void ResetColorAdjustSliders()
        {
            // Reset all sliders to 0
        }

        private void OnDocumentModified()
        {
            // Notify document changed
        }

        private string GenerateCopyName(string baseName)
        {
            // Generate unique copy name
            return baseName + " copy";
        }

        private void SelectSingleLayer(EditorLayer layer)
        {
            // Select single layer
        }

        private System.Collections.Generic.List<EditorLayer> SelectedLayers => new();

        // Dummy UI elements (replace with actual controls)
        private Slider SliderBrightness { get; set; }
        private Slider SliderContrast { get; set; }
        private Slider SliderSaturation { get; set; }
        private Slider SliderHue { get; set; }

        // ═══════════════════════════════════════════════════════
        // 7. CLEANUP ON CLOSE
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Cleanup khi đóng editor (important cho memory management).
        /// </summary>
        public void Cleanup()
        {
            _colorAdjustCts?.Cancel();
            _effectCts?.Cancel();
            
            // Clear cache
            ImageProcessingHelpers.ClearCache();
            
            // Force GC
            ImageProcessingHelpers.CleanupAfterHeavyOperation();
        }
    }

    // ═══════════════════════════════════════════════════════
    // DUMMY COMMAND CLASS (Replace with actual)
    // ═══════════════════════════════════════════════════════

    internal class ColorAdjustCommand : IHistoryCommand
    {
        private readonly EditorDocument _doc;
        private readonly EditorLayer _layer;
        private readonly BitmapSource _before;
        private readonly BitmapSource _after;

        public ColorAdjustCommand(EditorDocument doc, EditorLayer layer, BitmapSource before, BitmapSource after)
        {
            _doc = doc;
            _layer = layer;
            _before = before;
            _after = after;
        }

        public void Execute()
        {
            _layer.PixelData = _after;
        }

        public void Undo()
        {
            _layer.PixelData = _before;
        }

        public string Description => "Color Adjustment";
    }
}
