// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS & DEVELOPERS:
// DO NOT ALLOW ANY FILE IN THIS COMPONENT TO EXCEED ~1500 LINES OF CODE!
// To maintain readability, ease of testing, and modularity:
// - If a file grows larger than ~1500 lines, you MUST split/separate the logic into a new
//   partial class file (e.g., ImageProcessingNodeContentControl.<FeatureName>.cs).
// - Always place distinct features, tools, or event groupings in their respective files.
// - Ensure comments and documentation remain clean and structured.
// ========================================================================================
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlowMy.Helpers;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.Nodes;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl
    {
        // DTOs for project save/load
        public class ProjectMetadataDto
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public string ForegroundColor { get; set; } = "#000000";
            public string BackgroundColor { get; set; } = "#FFFFFF";
            public System.Collections.Generic.List<LayerMetadataDto> Layers { get; set; } = new();
            public string? ActiveLayerId { get; set; }

            // Layer AI settings
            public string? LayerAiWebUrl { get; set; }
            public string? LayerAiCacheProfileName { get; set; }
            public string? LayerAiWebTabsJson { get; set; }
            public string? LayerAiWebSplitMode { get; set; }
            public string? LayerAiActiveTab { get; set; }
            public bool LayerAiPromptHidden { get; set; }
            public bool LayerAiSendModeOn { get; set; } = true;
        }

        public class SecondaryImageDto
        {
            public string? ImageFileName { get; set; }
            public string? FilePath { get; set; }
            public bool IsSelected { get; set; }
        }

        public class LayerMetadataDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public double Opacity { get; set; } = 1.0;
            public bool IsVisible { get; set; } = true;
            public bool IsLocked { get; set; } = false;
            public bool IsChildrenCollapsed { get; set; } = false;
            public string BlendMode { get; set; } = "Normal";
            public System.Collections.Generic.List<LayerMetadataDto> ChildLayers { get; set; } = new();
            public string? ActiveChildLayerId { get; set; }

            // Extended layer geometry properties
            public int Width { get; set; }
            public int Height { get; set; }
            public int OffsetX { get; set; }
            public int OffsetY { get; set; }
            public double LayerScaleX { get; set; } = 1.0;
            public double LayerScaleY { get; set; } = 1.0;
            public double LayerAngle { get; set; } = 0.0;
            public double LayerTranslateX { get; set; } = 0.0;
            public double LayerTranslateY { get; set; } = 0.0;

            public double ContentBoundsX { get; set; }
            public double ContentBoundsY { get; set; }
            public double ContentBoundsWidth { get; set; }
            public double ContentBoundsHeight { get; set; }

            public double ImageContentBoundsX { get; set; }
            public double ImageContentBoundsY { get; set; }
            public double ImageContentBoundsWidth { get; set; }
            public double ImageContentBoundsHeight { get; set; }

            public string? ContentGeometryMarkup { get; set; }

            // Text layer properties
            public bool IsTextLayer { get; set; }
            public string TextContent { get; set; } = "";
            public double TextX { get; set; }
            public double TextY { get; set; }
            public double TextWidth { get; set; } = 200;
            public double TextHeight { get; set; } = 100;
            public double TextFontSize { get; set; } = 24;
            public string TextColor { get; set; } = "#FFFFFFFF";
            public string TextFontFamily { get; set; } = "Arial";
            public string TextFontStyle { get; set; } = "Bold";
            public string TextAlignment { get; set; } = "Left";

            // Dedicated filename in zip for deduplication
            public string? ImageFileName { get; set; }

            // Layer AI settings specific to this layer
            public string? LayerAiPrompt { get; set; }
            public int LayerAiBatchSizeIndex { get; set; }
            public int LayerAiAspectRatioIndex { get; set; }
            public string? LayerAiCustomWidth { get; set; }
            public string? LayerAiCustomHeight { get; set; }
            public System.Collections.Generic.List<SecondaryImageDto> SecondaryImages { get; set; } = new();
        }

        // File Menu Handlers
        private void BtnFileMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.ContextMenu != null)
                {
                    btn.ContextMenu.PlacementTarget = btn;
                    btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    btn.ContextMenu.IsOpen = true;
                }
            }
        }

        private void MenuItemOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenImageOrProjectFile();
        }

        private void MenuItemOpenProject_Click(object sender, RoutedEventArgs e)
        {
            ImportProjectZip();
        }

        private void MenuItemSaveProject_Click(object sender, RoutedEventArgs e)
        {
            SaveProjectZip();
        }

        private void MenuItemImport_Click(object sender, RoutedEventArgs e)
        {
            if (EditorPanel != null)
            {
                EditorPanel.AddImageLayer();
            }
        }

        private void MenuItemQuickExportPng_Click(object sender, RoutedEventArgs e)
        {
            QuickExportAsPngDirect();
        }

        private void MenuItemQuickExportJpg_Click(object sender, RoutedEventArgs e)
        {
            QuickExportAsJpgDirect();
        }

        private void MenuItemExportAs_Click(object sender, RoutedEventArgs e)
        {
            ExportImageAs();
        }

        private void MenuItemExportProject_Click(object sender, RoutedEventArgs e)
        {
            ExportProjectZip();
        }

        private void BtnOpenImageQuick_Click(object sender, RoutedEventArgs e)
        {
            OpenImageOrProjectFile();
        }

        private void BtnSavePngQuick_Click(object sender, RoutedEventArgs e)
        {
            QuickExportAsPngDirect();
        }

        private void BtnSaveJpgQuick_Click(object sender, RoutedEventArgs e)
        {
            QuickExportAsJpgDirect();
        }

        private void BtnImportProjectQuick_Click(object sender, RoutedEventArgs e)
        {
            ImportProjectZip();
        }

        private void BtnExportProjectQuick_Click(object sender, RoutedEventArgs e)
        {
            ExportProjectZip();
        }

        private void BtnSaveProjectQuick_Click(object sender, RoutedEventArgs e)
        {
            SaveProjectZip(sender as Button);
        }

        // Helper Logic Methods
        private void OpenImageOrProjectFile()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All Supported Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.iep;*.zip|Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Project Files (*.iep)|*.iep;*.zip|All Files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

                if (ext == ".iep" || ext == ".zip")
                {
                    ImportProjectZip(filePath);
                }
                else
                {
                    if (_activeTabData != null)
                    {
                        SaveCurrentTabState();
                    }

                    _node.ImageUrl = filePath;
                    _node.InputMode = ImageInputMode.Url;
                    _node.RaisePropertyChanged(nameof(ImageProcessingNode.ImageUrl));
                    _node.RaisePropertyChanged(nameof(ImageProcessingNode.InputMode));

                    _node.EditorDoc = null;
                    _activeTabData = null;
                }
            }
        }

        private string GetDefaultExportFileName(string fallbackPrefix = "flow_image")
        {
            // 1. Check active tab title or image title text block first
            string? activeTitle = null;
            if (_activeTabData != null && !string.IsNullOrWhiteSpace(_activeTabData.Title))
            {
                activeTitle = _activeTabData.Title;
            }
            else if (ImageTitleTextBlock != null && !string.IsNullOrWhiteSpace(ImageTitleTextBlock.Text))
            {
                activeTitle = ImageTitleTextBlock.Text;
            }

            if (!string.IsNullOrWhiteSpace(activeTitle) && 
                activeTitle != "Chưa có ảnh" && 
                activeTitle != "Đang tải ảnh...")
            {
                try
                {
                    string filename = System.IO.Path.GetFileNameWithoutExtension(activeTitle);
                    if (!string.IsNullOrWhiteSpace(filename) && !filename.Contains(":/") && !filename.Contains("\\"))
                    {
                        return filename;
                    }
                }
                catch { }
            }

            // 2. Check active tab's project path or image URL
            if (_activeTabData != null)
            {
                if (_activeTabData.EditorDoc != null && !string.IsNullOrWhiteSpace(_activeTabData.EditorDoc.ProjectPath))
                {
                    try
                    {
                        string filename = System.IO.Path.GetFileNameWithoutExtension(_activeTabData.EditorDoc.ProjectPath);
                        if (!string.IsNullOrWhiteSpace(filename)) return filename;
                    }
                    catch { }
                }
                if (!string.IsNullOrWhiteSpace(_activeTabData.ImageUrl))
                {
                    try
                    {
                        string filename = System.IO.Path.GetFileNameWithoutExtension(_activeTabData.ImageUrl);
                        if (!string.IsNullOrWhiteSpace(filename)) return filename;
                    }
                    catch { }
                }
            }

            // 3. Fallback to _node properties
            if (_node != null)
            {
                if (!string.IsNullOrWhiteSpace(_node.ImageUrl))
                {
                    try
                    {
                        string filename = System.IO.Path.GetFileNameWithoutExtension(_node.ImageUrl);
                        if (!string.IsNullOrWhiteSpace(filename) && !filename.Contains(":/") && !filename.Contains("\\"))
                        {
                            return filename;
                        }
                    }
                    catch { }
                }
                if (_node.EditorDoc != null && !string.IsNullOrWhiteSpace(_node.EditorDoc.ProjectPath))
                {
                    try
                    {
                        string filename = System.IO.Path.GetFileNameWithoutExtension(_node.EditorDoc.ProjectPath);
                        if (!string.IsNullOrWhiteSpace(filename)) return filename;
                    }
                    catch { }
                }
            }

            return $"{fallbackPrefix}_{DateTime.Now:ddMMyyyy_HHmmss}";
        }

        private void QuickExportAsPngDirect()
        {
            CommitBrushDrawingSession();
            if (_node.EditorDoc == null)
            {
                MessageBox.Show("Chưa có tài liệu để xuất ảnh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                DefaultExt = ".png",
                FileName = GetDefaultExportFileName() + ".png"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var composite = _node.EditorDoc.Composite();
                    if (composite == null)
                    {
                        MessageBox.Show("Không thể tạo ảnh composite.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ImageProcessorHelper.SaveBitmap(composite, saveFileDialog.FileName);
                    MessageBox.Show("Xuất ảnh nhanh PNG thành công!", "Xuất ảnh", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xuất ảnh nhanh PNG: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void QuickExportAsJpgDirect()
        {
            CommitBrushDrawingSession();
            if (_node.EditorDoc == null)
            {
                MessageBox.Show("Chưa có tài liệu để xuất ảnh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JPEG Image (*.jpg)|*.jpg",
                DefaultExt = ".jpg",
                FileName = GetDefaultExportFileName() + ".jpg"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var composite = _node.EditorDoc.Composite();
                    if (composite == null)
                    {
                        MessageBox.Show("Không thể tạo ảnh composite.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                    encoder.Frames.Add(BitmapFrame.Create(composite));
                    using (var fs = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    MessageBox.Show("Xuất ảnh JPG thành công!", "Xuất ảnh", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xuất ảnh JPG: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportImageAs()
        {
            CommitBrushDrawingSession();
            if (_node.EditorDoc == null)
            {
                MessageBox.Show("Chưa có tài liệu để xuất ảnh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP Image (*.bmp)|*.bmp|GIF Image (*.gif)|*.gif",
                DefaultExt = ".png",
                FileName = GetDefaultExportFileName()
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var composite = _node.EditorDoc.Composite();
                    if (composite == null)
                    {
                        MessageBox.Show("Không thể tạo ảnh composite.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string filePath = saveFileDialog.FileName;
                    string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

                    BitmapEncoder encoder = ext switch
                    {
                        ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                        ".bmp" => new BmpBitmapEncoder(),
                        ".gif" => new GifBitmapEncoder(),
                        _ => new PngBitmapEncoder()
                    };

                    encoder.Frames.Add(BitmapFrame.Create(composite));
                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    MessageBox.Show("Xuất ảnh thành công!", "Xuất ảnh", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xuất ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveProjectZip(Button? btn = null)
        {
            CommitBrushDrawingSession();
            if (_node.EditorDoc == null)
            {
                MessageBox.Show("Chưa có dự án để lưu.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(_node.EditorDoc.ProjectPath))
            {
                SaveProjectToPath(_node.EditorDoc.ProjectPath, btn);
            }
            else
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Image Editor Project (*.iep)|*.iep|Zip Archive (*.zip)|*.zip",
                    DefaultExt = ".iep",
                    FileName = GetDefaultExportFileName("flow_project")
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    SaveProjectToPath(saveFileDialog.FileName, btn);
                }
            }
        }

        private static double SanitizeDouble(double val, double fallback = 0.0)
        {
            return double.IsNaN(val) || double.IsInfinity(val) ? fallback : val;
        }

        private class LayerPixelBuffer
        {
            public string LayerId { get; set; } = "";
            public BitmapSource? FrozenBitmap { get; set; }
            public byte[]? EncodedBytes { get; set; }
        }

        private async void SaveProjectToPath(string filePath, Button? btn = null)
        {
            CommitBrushDrawingSession();
            CommitTransformSession();
            CommitActiveText();
            CommitKeyMoveSession();
            if (_node.EditorDoc == null) return;

            // Show loading
            if (FxLoadingOverlay != null)
            {
                FxLoadingOverlay.Visibility = Visibility.Visible;
            }
            if (FxLoadingText != null)
            {
                FxLoadingText.Text = "Đang lưu dự án...";
            }
            if (FxLoadingCancelHint != null)
            {
                FxLoadingCancelHint.Visibility = Visibility.Collapsed;
            }

            // Yield to let WPF render the loading overlay
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            try
            {
                // 1. Gather all data on the UI thread
                var dto = new ProjectMetadataDto
                {
                    Width = _node.EditorDoc.Width,
                    Height = _node.EditorDoc.Height,
                    ForegroundColor = _node.EditorDoc.ForegroundColor.ToString(),
                    BackgroundColor = _node.EditorDoc.BackgroundColor.ToString(),
                    ActiveLayerId = _node.EditorDoc.ActiveLayer?.Id,

                    // Save Layer AI settings to Project DTO
                    LayerAiWebUrl = _node.LayerAiWebUrl,
                    LayerAiCacheProfileName = _node.LayerAiCacheProfileName,
                    LayerAiWebTabsJson = _node.LayerAiWebTabsJson,
                    LayerAiWebSplitMode = _node.LayerAiWebSplitMode,
                    LayerAiActiveTab = _node.LayerAiActiveTab,
                    LayerAiPromptHidden = _node.LayerAiPromptHidden,
                    LayerAiSendModeOn = _node.LayerAiSendModeOn
                };

                // We need to collect layer data and encode bitmaps on UI thread
                var layersToProcess = new System.Collections.Generic.List<(LayerMetadataDto Dto, string LayerId)>();
                var layerBuffers = new System.Collections.Generic.Dictionary<string, LayerPixelBuffer>();
                var secondaryImagesToProcess = new System.Collections.Generic.List<(string ImageFileName, BitmapSource Bitmap)>();
                
                // Helper to traverse and serialize
                void AddLayersToDto(System.Collections.Generic.IEnumerable<EditorLayer> layers, System.Collections.Generic.List<LayerMetadataDto> targetList)
                {
                    foreach (var l in layers)
                    {
                        var lDto = new LayerMetadataDto
                        {
                            Id = l.Id,
                            Name = l.Name,
                            Opacity = l.Opacity,
                            IsVisible = l.IsVisible,
                            IsLocked = l.IsLocked,
                            IsChildrenCollapsed = l.IsChildrenCollapsed,
                            BlendMode = l.BlendMode.ToString(),
                            ActiveChildLayerId = l.ActiveChildLayer?.Id,

                            Width = l.Width,
                            Height = l.Height,
                            OffsetX = l.OffsetX,
                            OffsetY = l.OffsetY,
                            LayerScaleX = SanitizeDouble(l.LayerScaleX, 1.0),
                            LayerScaleY = SanitizeDouble(l.LayerScaleY, 1.0),
                            LayerAngle = SanitizeDouble(l.LayerAngle, 0.0),
                            LayerTranslateX = SanitizeDouble(l.LayerTranslateX, 0.0),
                            LayerTranslateY = SanitizeDouble(l.LayerTranslateY, 0.0),

                            ContentBoundsX = l.ContentBounds.IsEmpty ? 0.0 : SanitizeDouble(l.ContentBounds.X, 0.0),
                            ContentBoundsY = l.ContentBounds.IsEmpty ? 0.0 : SanitizeDouble(l.ContentBounds.Y, 0.0),
                            ContentBoundsWidth = l.ContentBounds.IsEmpty ? 0.0 : SanitizeDouble(l.ContentBounds.Width, 0.0),
                            ContentBoundsHeight = l.ContentBounds.IsEmpty ? 0.0 : SanitizeDouble(l.ContentBounds.Height, 0.0),

                            ImageContentBoundsX = l.ImageContentBounds.IsEmpty ? 0.0 : SanitizeDouble(l.ImageContentBounds.X, 0.0),
                            ImageContentBoundsY = l.ImageContentBounds.IsEmpty ? 0.0 : SanitizeDouble(l.ImageContentBounds.Y, 0.0),
                            ImageContentBoundsWidth = l.ImageContentBounds.IsEmpty ? 0.0 : SanitizeDouble(l.ImageContentBounds.Width, 0.0),
                            ImageContentBoundsHeight = l.ImageContentBounds.IsEmpty ? 0.0 : SanitizeDouble(l.ImageContentBounds.Height, 0.0),

                            IsTextLayer = l.IsTextLayer,
                            TextContent = l.TextContent ?? "",
                            TextX = SanitizeDouble(l.TextX, 0.0),
                            TextY = SanitizeDouble(l.TextY, 0.0),
                            TextWidth = SanitizeDouble(l.TextWidth, 200.0),
                            TextHeight = SanitizeDouble(l.TextHeight, 100.0),
                            TextFontSize = SanitizeDouble(l.TextFontSize, 24.0),
                            TextColor = l.TextColor.ToString(),
                            TextFontFamily = l.TextFontFamily ?? "Arial",
                            TextFontStyle = l.TextFontStyle ?? "Bold",
                            TextAlignment = l.TextAlignment ?? "Left",

                            // Save Layer AI properties specific to this layer
                            LayerAiPrompt = l.LayerAiPrompt,
                            LayerAiBatchSizeIndex = l.LayerAiBatchSizeIndex,
                            LayerAiAspectRatioIndex = l.LayerAiAspectRatioIndex,
                            LayerAiCustomWidth = l.LayerAiCustomWidth,
                            LayerAiCustomHeight = l.LayerAiCustomHeight
                        };

                        for (int i = 0; i < 4; i++)
                        {
                            var sec = l.LayerAiSecondaryImages[i];
                            var secDto = new SecondaryImageDto
                            {
                                FilePath = sec.FilePath,
                                IsSelected = sec.IsSelected
                            };

                            if (sec.Bitmap != null)
                            {
                                string entryName = $"layers/{l.Id}_sec_{i}.png";
                                secDto.ImageFileName = entryName;

                                var bmp = sec.Bitmap;
                                if (!bmp.IsFrozen)
                                {
                                    try
                                    {
                                        var clonedBmp = bmp.Clone();
                                        clonedBmp.Freeze();
                                        bmp = clonedBmp;
                                    }
                                    catch
                                    {
                                        int wbW = bmp.PixelWidth;
                                        int wbH = bmp.PixelHeight;
                                        int wbStride = wbW * 4;
                                        byte[] wbPixels = new byte[wbH * wbStride];
                                        bmp.CopyPixels(wbPixels, wbStride, 0);
                                        var clonedBmp = BitmapSource.Create(wbW, wbH, bmp.DpiX, bmp.DpiY, PixelFormats.Bgra32, null, wbPixels, wbStride);
                                        clonedBmp.Freeze();
                                        bmp = clonedBmp;
                                    }
                                }

                                secondaryImagesToProcess.Add((entryName, bmp));
                            }

                            lDto.SecondaryImages.Add(secDto);
                        }

                        if (l.ContentGeometry != null)
                        {
                            lDto.ContentGeometryMarkup = l.ContentGeometry.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }

                        // Copy pixels and construct a frozen BitmapSource on UI thread (very fast)
                        if (l.PngBytes != null)
                        {
                            layerBuffers[l.Id] = new LayerPixelBuffer
                            {
                                LayerId = l.Id,
                                EncodedBytes = l.PngBytes
                            };
                        }
                        else if (l.Bitmap != null)
                        {
                            int w = l.Bitmap.PixelWidth;
                            int h = l.Bitmap.PixelHeight;
                            int stride = l.Bitmap.BackBufferStride;
                            byte[] pixels = new byte[h * stride];
                            l.Bitmap.CopyPixels(pixels, stride, 0);

                            var bitmapSource = BitmapSource.Create(
                                w,
                                h,
                                l.Bitmap.DpiX,
                                l.Bitmap.DpiY,
                                PixelFormats.Bgra32,
                                null,
                                pixels,
                                stride);
                            bitmapSource.Freeze();

                            layerBuffers[l.Id] = new LayerPixelBuffer
                            {
                                LayerId = l.Id,
                                FrozenBitmap = bitmapSource
                            };
                        }

                        layersToProcess.Add((lDto, l.Id));
                        targetList.Add(lDto);

                        if (l.ChildLayers != null && l.ChildLayers.Count > 0)
                        {
                            AddLayersToDto(l.ChildLayers, lDto.ChildLayers);
                        }
                    }
                }

                AddLayersToDto(_node.EditorDoc.Layers, dto.Layers);

                // 2. Offload compression and writing to a background thread
                await System.Threading.Tasks.Task.Run(() =>
                {
                    // Encode PNG bytes in parallel on background thread pool
                    var encodedPngs = new System.Collections.Concurrent.ConcurrentDictionary<string, byte[]>();

                    System.Threading.Tasks.Parallel.ForEach(layerBuffers.Values, buffer =>
                    {
                        if (buffer.EncodedBytes != null)
                        {
                            encodedPngs[buffer.LayerId] = buffer.EncodedBytes;
                            return;
                        }
                        if (buffer.FrozenBitmap == null) return;
                        try
                        {
                            using (var ms = new MemoryStream())
                            {
                                var encoder = new PngBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(buffer.FrozenBitmap));
                                encoder.Save(ms);
                                byte[] bytes = ms.ToArray();
                                encodedPngs[buffer.LayerId] = bytes;

                                // Cache the encoded bytes back to the layer so subsequent saves are instant
                                var layer = _node.EditorDoc?.Layers?.FirstOrDefault(x => x.Id == buffer.LayerId)
                                            ?? _node.EditorDoc?.Layers?.SelectMany(x => x.ChildLayers).FirstOrDefault(x => x.Id == buffer.LayerId);
                                if (layer != null)
                                {
                                    layer.PngBytes = bytes;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error encoding layer {buffer.LayerId}: {ex.Message}");
                        }
                    });

                    // Encode secondary images in parallel
                    var encodedSecPngs = new System.Collections.Concurrent.ConcurrentDictionary<string, byte[]>();
                    System.Threading.Tasks.Parallel.ForEach(secondaryImagesToProcess, item =>
                    {
                        try
                        {
                            using (var ms = new MemoryStream())
                            {
                                var encoder = new PngBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(item.Bitmap));
                                encoder.Save(ms);
                                encodedSecPngs[item.ImageFileName] = ms.ToArray();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error encoding secondary image: {ex.Message}");
                        }
                    });

                    using (var fs = new FileStream(filePath, FileMode.Create))
                    using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        var savedImages = new System.Collections.Generic.Dictionary<string, string>();

                        foreach (var item in layersToProcess)
                        {
                            string layerId = item.LayerId;
                            if (!encodedPngs.TryGetValue(layerId, out var pngBytes))
                            {
                                continue;
                            }

                            // Compute SHA256 of PNG bytes
                            string hash;
                            using (var sha = System.Security.Cryptography.SHA256.Create())
                            {
                                var hashBytes = sha.ComputeHash(pngBytes);
                                hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                            }

                            string entryName;
                            if (savedImages.TryGetValue(hash, out var existingEntryName))
                            {
                                entryName = existingEntryName;
                            }
                            else
                            {
                                entryName = $"layers/{layerId}.png";
                                // PNG is already compressed, store it without extra deflate compression to save time
                                var imgEntry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                                using (var entryStream = imgEntry.Open())
                                {
                                    entryStream.Write(pngBytes, 0, pngBytes.Length);
                                }
                                savedImages[hash] = entryName;
                            }

                            item.Dto.ImageFileName = entryName;
                        }

                        // Write secondary images to Zip Archive
                        foreach (var item in secondaryImagesToProcess)
                        {
                            if (encodedSecPngs.TryGetValue(item.ImageFileName, out var pngBytes))
                            {
                                var entry = archive.CreateEntry(item.ImageFileName, CompressionLevel.NoCompression);
                                using (var entryStream = entry.Open())
                                {
                                    entryStream.Write(pngBytes, 0, pngBytes.Length);
                                }
                            }
                        }

                        // Save project.json to zip
                        var jsonEntry = archive.CreateEntry("project.json", CompressionLevel.Fastest);
                        using (var entryStream = jsonEntry.Open())
                        using (var writer = new StreamWriter(entryStream))
                        {
                            var options = new JsonSerializerOptions { WriteIndented = true };
                            writer.Write(JsonSerializer.Serialize(dto, options));
                        }
                    }
                });

                _node.EditorDoc.ProjectPath = filePath;

                if (btn != null)
                {
                    // Visual feedback: brief flash
                    var origContent = btn.Content;
                    btn.Content = new TextBlock { Text = "✅", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    btn.IsEnabled = false;
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
                    timer.Tick += (_, _) =>
                    {
                        btn.Content = origContent;
                        btn.IsEnabled = true;
                        timer.Stop();
                    };
                    timer.Start();
                }
                else
                {
                    MessageBox.Show("Lưu dự án thành công!", "Lưu dự án", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu dự án: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (FxLoadingOverlay != null)
                {
                    FxLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ExportProjectZip()
        {
            CommitBrushDrawingSession();
            CommitTransformSession();
            CommitActiveText();
            CommitKeyMoveSession();
            if (_node.EditorDoc == null)
            {
                MessageBox.Show("Chưa có dự án để xuất.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Image Editor Project (*.iep)|*.iep|Zip Archive (*.zip)|*.zip",
                DefaultExt = ".iep",
                FileName = GetDefaultExportFileName("flow_project")
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                SaveProjectToPath(saveFileDialog.FileName);
            }
        }

        private async void ImportProjectZip(string? filePath = null)
        {
            string? selectedPath = filePath;
            if (string.IsNullOrEmpty(selectedPath))
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Editor Project (*.iep)|*.iep|Zip Archive (*.zip)|*.zip|All Files|*.*"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedPath = openFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }

            // Save current tab state before importing a new project
            if (_activeTabData != null)
            {
                SaveCurrentTabState();
            }

            // Show loading overlay
            if (FxLoadingOverlay != null)
            {
                FxLoadingOverlay.Visibility = Visibility.Visible;
            }
            if (FxLoadingText != null)
            {
                FxLoadingText.Text = "Đang mở dự án...";
            }
            if (FxLoadingCancelHint != null)
            {
                FxLoadingCancelHint.Visibility = Visibility.Collapsed;
            }

            // Yield to let WPF render the loading overlay
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            try
            {
                ProjectMetadataDto? dto = null;
                var layerBytesMap = new System.Collections.Generic.Dictionary<string, byte[]>();

                // 1. Read files and extract zip archive on background thread
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using (var fs = new FileStream(selectedPath, FileMode.Open, FileAccess.Read))
                    using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
                    {
                        var jsonEntry = archive.GetEntry("project.json");
                        if (jsonEntry == null)
                        {
                            throw new FileNotFoundException("Không tìm thấy tệp cấu hình project.json trong dự án.");
                        }

                        using (var entryStream = jsonEntry.Open())
                        using (var reader = new StreamReader(entryStream))
                        {
                            var json = reader.ReadToEnd();
                            dto = JsonSerializer.Deserialize<ProjectMetadataDto>(json);
                        }

                        if (dto == null)
                        {
                            throw new InvalidDataException("Tệp cấu hình dự án không hợp lệ.");
                        }

                        // Read all entries in the layers/ directory
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.StartsWith("layers/", StringComparison.OrdinalIgnoreCase) && 
                                entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                            {
                                using (var entryStream = entry.Open())
                                using (var ms = new MemoryStream())
                                {
                                    entryStream.CopyTo(ms);
                                    layerBytesMap[entry.FullName] = ms.ToArray();
                                }
                            }
                        }
                    }
                });

                if (dto == null) return;

                // 2. Process and build UI elements on UI thread (due to thread affinity)
                var doc = new EditorDocument(dto.Width, dto.Height);
                doc.ProjectPath = selectedPath;

                if (!string.IsNullOrEmpty(dto.ForegroundColor))
                {
                    try { doc.ForegroundColor = (Color)ColorConverter.ConvertFromString(dto.ForegroundColor); } catch { }
                }
                if (!string.IsNullOrEmpty(dto.BackgroundColor))
                {
                    try { doc.BackgroundColor = (Color)ColorConverter.ConvertFromString(dto.BackgroundColor); } catch { }
                }

                EditorLayer RestoreLayer(LayerMetadataDto lDto, EditorLayer? parent)
                {
                    var layer = new EditorLayer(lDto.Width > 0 ? lDto.Width : dto.Width, lDto.Height > 0 ? lDto.Height : dto.Height, lDto.Name);

                    // 1. Assign parent first so dynamic resolution functions properly
                    layer.ParentLayer = parent;

                    // 2. Load bitmap first so that setting OriginalTransformBitmap (which triggers the setter calculating default ContentBounds)
                    // happens BEFORE we restore the saved ContentBounds.
                    string imgFileName = !string.IsNullOrEmpty(lDto.ImageFileName) ? lDto.ImageFileName : $"layers/{lDto.Id}.png";
                    if (layerBytesMap.TryGetValue(imgFileName, out var pngBytes))
                    {
                        layer.PngBytes = pngBytes;
                        using (var ms = new MemoryStream(pngBytes))
                        {
                            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            BitmapSource bmpSource = decoder.Frames[0];
                            if (bmpSource.Format != PixelFormats.Bgra32)
                            {
                                bmpSource = new FormatConvertedBitmap(bmpSource, PixelFormats.Bgra32, null, 0);
                            }
                            var loadedBmp = new WriteableBitmap(bmpSource);
                            layer.Bitmap = loadedBmp;
                            
                            // Restore OriginalTransformBitmap as a separate instance so editing/moving works seamlessly
                            // ONLY if the layer had a non-empty ContentBounds saved!
                            if (lDto.ContentBoundsWidth > 0 && lDto.ContentBoundsHeight > 0)
                            {
                                layer.OriginalTransformBitmap = new WriteableBitmap(loadedBmp);
                            }
                        }
                    }

                    // 3. Now restore all metadata properties from lDto (overwriting the auto-calculated ContentBounds)
                    layer.Id = lDto.Id;
                    layer.Opacity = lDto.Opacity;
                    layer.IsVisible = lDto.IsVisible;
                    layer.IsLocked = lDto.IsLocked;
                    layer.IsChildrenCollapsed = lDto.IsChildrenCollapsed;
                    layer.OffsetX = lDto.OffsetX;
                    layer.OffsetY = lDto.OffsetY;
                    layer.LayerScaleX = lDto.LayerScaleX;
                    layer.LayerScaleY = lDto.LayerScaleY;
                    layer.LayerAngle = lDto.LayerAngle;
                    layer.LayerTranslateX = lDto.LayerTranslateX;
                    layer.LayerTranslateY = lDto.LayerTranslateY;
                    layer.IsTextLayer = lDto.IsTextLayer;
                    layer.TextContent = lDto.TextContent ?? "";
                    layer.TextX = lDto.TextX;
                    layer.TextY = lDto.TextY;
                    layer.TextWidth = lDto.TextWidth;
                    layer.TextHeight = lDto.TextHeight;
                    layer.TextFontSize = lDto.TextFontSize;
                    layer.TextFontFamily = lDto.TextFontFamily ?? "Arial";
                    layer.TextFontStyle = lDto.TextFontStyle ?? "Bold";
                    layer.TextAlignment = lDto.TextAlignment ?? "Left";

                    // Restore layer-specific Layer AI configurations
                    layer.LayerAiPrompt = lDto.LayerAiPrompt ?? string.Empty;
                    layer.LayerAiBatchSizeIndex = lDto.LayerAiBatchSizeIndex;
                    layer.LayerAiAspectRatioIndex = lDto.LayerAiAspectRatioIndex;
                    layer.LayerAiCustomWidth = lDto.LayerAiCustomWidth ?? string.Empty;
                    layer.LayerAiCustomHeight = lDto.LayerAiCustomHeight ?? string.Empty;

                    if (lDto.SecondaryImages != null)
                    {
                        for (int i = 0; i < Math.Min(4, lDto.SecondaryImages.Count); i++)
                        {
                            var secDto = lDto.SecondaryImages[i];
                            layer.LayerAiSecondaryImages[i].FilePath = secDto.FilePath;
                            layer.LayerAiSecondaryImages[i].IsSelected = secDto.IsSelected;

                            if (!string.IsNullOrEmpty(secDto.ImageFileName) && layerBytesMap.TryGetValue(secDto.ImageFileName, out var secPngBytes))
                            {
                                layer.LayerAiSecondaryImages[i].PngBytes = secPngBytes;
                                try
                                {
                                    using (var ms = new MemoryStream(secPngBytes))
                                    {
                                        var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                                        layer.LayerAiSecondaryImages[i].Bitmap = dec.Frames[0];
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(lDto.TextColor))
                    {
                        try { layer.TextColor = (Color)ColorConverter.ConvertFromString(lDto.TextColor); } catch { }
                    }

                    if (lDto.ContentBoundsWidth > 0 && lDto.ContentBoundsHeight > 0)
                    {
                        layer.ContentBounds = new Rect(lDto.ContentBoundsX, lDto.ContentBoundsY, lDto.ContentBoundsWidth, lDto.ContentBoundsHeight);
                    }
                    if (lDto.ImageContentBoundsWidth > 0 && lDto.ImageContentBoundsHeight > 0)
                    {
                        layer.ImageContentBounds = new Rect(lDto.ImageContentBoundsX, lDto.ImageContentBoundsY, lDto.ImageContentBoundsWidth, lDto.ImageContentBoundsHeight);
                    }

                    if (!string.IsNullOrEmpty(lDto.ContentGeometryMarkup))
                    {
                        try
                        {
                            layer.ContentGeometry = Geometry.Parse(lDto.ContentGeometryMarkup);
                        }
                        catch { }
                    }

                    if (Enum.TryParse<BlendMode>(lDto.BlendMode, out var bMode))
                    {
                        layer.BlendMode = bMode;
                    }

                    // Restore child layers
                    foreach (var childDto in lDto.ChildLayers)
                    {
                        var childLayer = RestoreLayer(childDto, layer);
                        layer.ChildLayers.Add(childLayer);
                    }

                    // Restore ActiveChildLayer
                    if (!string.IsNullOrEmpty(lDto.ActiveChildLayerId))
                    {
                        layer.ActiveChildLayer = layer.ChildLayers.FirstOrDefault(c => c.Id == lDto.ActiveChildLayerId);
                    }

                    return layer;
                }

                foreach (var lDto in dto.Layers)
                {
                    var layer = RestoreLayer(lDto, null);
                    doc.Layers.Add(layer);
                }

                if (!string.IsNullOrEmpty(dto.ActiveLayerId))
                {
                    var foundActive = doc.Layers.FirstOrDefault(x => x.Id == dto.ActiveLayerId)
                                      ?? doc.Layers.SelectMany(x => x.ChildLayers).FirstOrDefault(x => x.Id == dto.ActiveLayerId);
                    if (foundActive != null)
                    {
                        doc.ActiveLayer = foundActive;
                    }
                    else if (doc.Layers.Count > 0)
                    {
                        doc.ActiveLayer = doc.Layers[doc.Layers.Count - 1];
                    }
                }
                else if (doc.Layers.Count > 0)
                {
                    doc.ActiveLayer = doc.Layers[doc.Layers.Count - 1];
                }

                // Apply to node and view
                _node.EditorDoc = doc;
                EditorPanel.SetDocument(doc);
                CenterImageOnCanvas(doc.Width, doc.Height);

                // Restore Layer AI settings from DTO to Node
                if (dto != null)
                {
                    if (dto.LayerAiWebUrl != null) _node.LayerAiWebUrl = dto.LayerAiWebUrl;
                    if (dto.LayerAiCacheProfileName != null) _node.LayerAiCacheProfileName = dto.LayerAiCacheProfileName;
                    if (dto.LayerAiWebTabsJson != null) _node.LayerAiWebTabsJson = dto.LayerAiWebTabsJson;
                    if (dto.LayerAiWebSplitMode != null) _node.LayerAiWebSplitMode = dto.LayerAiWebSplitMode;
                    if (dto.LayerAiActiveTab != null) _node.LayerAiActiveTab = dto.LayerAiActiveTab;
                    _node.LayerAiPromptHidden = dto.LayerAiPromptHidden;
                    _node.LayerAiSendModeOn = dto.LayerAiSendModeOn;
                }

                // Set title and sync active tab
                if (ImageTitleTextBlock != null)
                {
                    ImageTitleTextBlock.Text = System.IO.Path.GetFileName(selectedPath);
                }
                _activeTabData = null; // Create a new tab for the imported project
                SyncActiveTab();

                OnEditorDocumentModified();
                SyncToolboxColors();

                MessageBox.Show("Mở dự án thành công!", "Mở dự án", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi mở dự án: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (FxLoadingOverlay != null)
                {
                    FxLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
