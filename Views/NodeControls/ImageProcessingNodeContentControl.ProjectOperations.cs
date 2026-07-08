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
        }

        public class LayerMetadataDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public double Opacity { get; set; } = 1.0;
            public bool IsVisible { get; set; } = true;
            public bool IsLocked { get; set; } = false;
            public string BlendMode { get; set; } = "Normal";
            public System.Collections.Generic.List<LayerMetadataDto> ChildLayers { get; set; } = new();
            public string? ActiveChildLayerId { get; set; }
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
            OpenImageAndAddTab();
        }

        private void MenuItemOpenProject_Click(object sender, RoutedEventArgs e)
        {
            ImportProjectZip();
        }

        private void MenuItemSave_Click(object sender, RoutedEventArgs e)
        {
            if (BtnSaveFxQuick != null)
            {
                SaveFxConfig(BtnSaveFxQuick);
            }
            else
            {
                FlowMy.Utils.FxConfigCache.SaveToFile();
            }
        }

        private void MenuItemImport_Click(object sender, RoutedEventArgs e)
        {
            if (EditorPanel != null)
            {
                EditorPanel.AddImageLayer();
            }
        }

        private void MenuItemQuickExport_Click(object sender, RoutedEventArgs e)
        {
            QuickExportAsPng();
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
            OpenImageAndAddTab();
        }

        private void BtnSaveFxQuick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                SaveFxConfig(btn);
            }
            else
            {
                FlowMy.Utils.FxConfigCache.SaveToFile();
            }
        }

        // Helper Logic Methods
        private void SaveFxConfig(Button btn)
        {
            FlowMy.Utils.FxConfigCache.SaveToFile();

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

        private void QuickExportAsPng()
        {
            if (_node.EditorDoc == null)
            {
                MessageBox.Show("Chưa có tài liệu để xuất ảnh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                DefaultExt = ".png",
                FileName = (_node.Title ?? "export") + "_quick.png"
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
                    MessageBox.Show("Xuất ảnh nhanh thành công!", "Xuất ảnh", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xuất ảnh nhanh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportImageAs()
        {
            if (_node.EditorDoc == null)
            {
                MessageBox.Show("Chưa có tài liệu để xuất ảnh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP Image (*.bmp)|*.bmp|GIF Image (*.gif)|*.gif",
                DefaultExt = ".png",
                FileName = _node.Title ?? "export"
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

        private void ExportProjectZip()
        {
            if (_node.EditorDoc == null)
            {
                MessageBox.Show("Chưa có dự án để xuất.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Image Editor Project (*.iep)|*.iep|Zip Archive (*.zip)|*.zip",
                DefaultExt = ".iep",
                FileName = _node.Title ?? "project"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var fs = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        // Build metadata DTO
                        var dto = new ProjectMetadataDto
                        {
                            Width = _node.EditorDoc.Width,
                            Height = _node.EditorDoc.Height,
                            ForegroundColor = _node.EditorDoc.ForegroundColor.ToString(),
                            BackgroundColor = _node.EditorDoc.BackgroundColor.ToString()
                        };

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
                                    BlendMode = l.BlendMode.ToString(),
                                    ActiveChildLayerId = l.ActiveChildLayer?.Id
                                };

                                // Save layer bitmap to zip using MemoryStream to avoid seek unsupported exception
                                var imgEntry = archive.CreateEntry($"layers/{l.Id}.png");
                                using (var entryStream = imgEntry.Open())
                                using (var ms = new MemoryStream())
                                {
                                    var encoder = new PngBitmapEncoder();
                                    encoder.Frames.Add(BitmapFrame.Create(l.Bitmap));
                                    encoder.Save(ms);
                                    byte[] bytes = ms.ToArray();
                                    entryStream.Write(bytes, 0, bytes.Length);
                                }

                                if (l.ChildLayers != null && l.ChildLayers.Count > 0)
                                {
                                    AddLayersToDto(l.ChildLayers, lDto.ChildLayers);
                                }

                                targetList.Add(lDto);
                            }
                        }

                        AddLayersToDto(_node.EditorDoc.Layers, dto.Layers);

                        // Save project.json to zip
                        var jsonEntry = archive.CreateEntry("project.json");
                        using (var entryStream = jsonEntry.Open())
                        using (var writer = new StreamWriter(entryStream))
                        {
                            var options = new JsonSerializerOptions { WriteIndented = true };
                            writer.Write(JsonSerializer.Serialize(dto, options));
                        }
                    }

                    MessageBox.Show("Xuất dự án thành công!", "Xuất dự án", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xuất dự án: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportProjectZip()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Editor Project (*.iep)|*.iep|Zip Archive (*.zip)|*.zip|All Files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    ProjectMetadataDto? dto = null;
                    using (var fs = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read))
                    using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
                    {
                        var jsonEntry = archive.GetEntry("project.json");
                        if (jsonEntry == null)
                        {
                            MessageBox.Show("Không tìm thấy tệp cấu hình project.json trong dự án.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        using (var entryStream = jsonEntry.Open())
                        using (var reader = new StreamReader(entryStream))
                        {
                            var json = reader.ReadToEnd();
                            dto = JsonSerializer.Deserialize<ProjectMetadataDto>(json);
                        }

                        if (dto == null)
                        {
                            MessageBox.Show("Tệp cấu hình dự án không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        var doc = new EditorDocument(dto.Width, dto.Height);
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
                            var layer = new EditorLayer(dto.Width, dto.Height, lDto.Name)
                            {
                                Id = lDto.Id,
                                Opacity = lDto.Opacity,
                                IsVisible = lDto.IsVisible,
                                IsLocked = lDto.IsLocked
                            };

                            if (Enum.TryParse<BlendMode>(lDto.BlendMode, out var bMode))
                            {
                                layer.BlendMode = bMode;
                            }
                            layer.ParentLayer = parent;

                            // Load bitmap
                            var imgEntry = archive.GetEntry($"layers/{lDto.Id}.png");
                            if (imgEntry != null)
                            {
                                using (var imgStream = imgEntry.Open())
                                using (var ms = new MemoryStream())
                                {
                                    imgStream.CopyTo(ms);
                                    ms.Position = 0;
                                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                                    var loadedBmp = new WriteableBitmap(decoder.Frames[0]);
                                    layer.Bitmap = loadedBmp;
                                }
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

                        if (doc.Layers.Count > 0)
                        {
                            doc.ActiveLayer = doc.Layers[doc.Layers.Count - 1];
                        }

                        // Apply to node and view
                        _node.EditorDoc = doc;
                        EditorPanel.SetDocument(doc);
                        OnEditorDocumentModified();
                        SyncToolboxColors();
                    }

                    MessageBox.Show("Mở dự án thành công!", "Mở dự án", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi mở dự án: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
