// =========================================================================================================
// AI / DEVELOPER ARCHITECTURAL DIRECTIVE:
// This file is part of the partial class LayerAiDialog (Views/Overlays/LayerAiDialog).
// CRITICAL RULE: DO NOT BLOAT OR STUFF EXCESSIVE LOGIC INTO A SINGLE FILE.
// Maintain each partial class file at a maximum size of ~1000 - 1500 lines of code.
// When adding new features or handlers, refactor and create dedicated partial class files per module.
// =========================================================================================================

using FlowMy.Models.ImageEditor;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        #region Secondary Images Slots

        private void ImgPreview_MouseEnter(object sender, MouseEventArgs e) { }
        private void ImgPreview_MouseLeave(object sender, MouseEventArgs e) { }

        private void BtnAddSecondary_Click(object sender, RoutedEventArgs e)
        {
            BtnUploadSecondary_Click(sender, e);
        }

        private void BtnSlotPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int count) && count > 0)
            {
                SetSlotCount(count);
            }
        }

        private void TxtSlotCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingSlotCount) return;
            if (SecondaryImagesGrid == null || SecondaryImagesGridWv == null) return;
            if (int.TryParse(TxtSlotCount.Text.Trim(), out int count))
            {
                if (count <= 0)
                {
                    // Hide secondary images grid
                    SecondaryImagesGrid.Visibility = Visibility.Collapsed;
                    SecondaryImagesGridWv.Visibility = Visibility.Collapsed;
                    return;
                }
                SecondaryImagesGrid.Visibility = Visibility.Visible;
                SecondaryImagesGridWv.Visibility = Visibility.Visible;
                SetSlotCount(Math.Clamp(count, 1, 999));
            }
        }

        private void SetSlotCount(int count)
        {
            count = Math.Clamp(count, 1, 999);
            _secondarySlotCount = count;

            _isUpdatingSlotCount = true;
            try
            {
                if (TxtSlotCount != null && TxtSlotCount.Text != count.ToString())
                {
                    TxtSlotCount.Text = count.ToString();
                }
            }
            finally
            {
                _isUpdatingSlotCount = false;
            }

            // Sync model list size
            while (_secondaryImages.Count < count)
            {
                _secondaryImages.Add(new SecondaryImageItem());
            }
            while (_secondaryImages.Count > count)
            {
                _secondaryImages.RemoveAt(_secondaryImages.Count - 1);
            }

            RebuildSecondaryGridUI();
        }

        private void RebuildSecondaryGridUI()
        {
            if (SecondaryImagesGrid == null || SecondaryImagesGridWv == null) return;

            // Clear previous elements and definitions
            SecondaryImagesGrid.Children.Clear();
            SecondaryImagesGrid.ColumnDefinitions.Clear();
            SecondaryImagesGrid.RowDefinitions.Clear();
            _slotImages.Clear();
            _slotPlaceholders.Clear();
            _slotBorders.Clear();

            SecondaryImagesGridWv.Children.Clear();
            SecondaryImagesGridWv.ColumnDefinitions.Clear();
            SecondaryImagesGridWv.RowDefinitions.Clear();
            _slotImagesWv.Clear();
            _slotPlaceholdersWv.Clear();
            _slotBordersWv.Clear();

            // 2-column layout
            SecondaryImagesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            SecondaryImagesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            SecondaryImagesGridWv.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            SecondaryImagesGridWv.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int numRows = (int)Math.Ceiling(_secondarySlotCount / 2.0);

            // In combined mode ("Ảnh chung"), rows stretch to fill container height evenly
            for (int r = 0; r < numRows; r++)
            {
                var heightSpec = _isCombinedMode ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
                SecondaryImagesGrid.RowDefinitions.Add(new RowDefinition { Height = heightSpec });
                SecondaryImagesGridWv.RowDefinitions.Add(new RowDefinition { Height = heightSpec });
            }

            for (int i = 0; i < _secondarySlotCount; i++)
            {
                int idx = i;
                var item = _secondaryImages[idx];
                int row = idx / 2;
                int col = idx % 2;

                var border = CreateSlotUI(idx, item, out var img, out var placeholder);
                Grid.SetRow(border, row);
                Grid.SetColumn(border, col);
                SecondaryImagesGrid.Children.Add(border);

                _slotBorders.Add(border);
                _slotImages.Add(img);
                _slotPlaceholders.Add(placeholder);

                var borderWv = CreateSlotUI(idx, item, out var imgWv, out var placeholderWv);
                Grid.SetRow(borderWv, row);
                Grid.SetColumn(borderWv, col);
                SecondaryImagesGridWv.Children.Add(borderWv);

                _slotBordersWv.Add(borderWv);
                _slotImagesWv.Add(imgWv);
                _slotPlaceholdersWv.Add(placeholderWv);
            }

            UpdateSecondaryInfo();
        }

        private Border CreateSlotUI(int index, SecondaryImageItem item, out Image img, out StackPanel placeholder)
        {
            var accentColor = FindResource("AccentColor") as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
            var borderColor = FindResource("BorderColor") as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15171e")),
                BorderBrush = item.IsSelected ? accentColor : borderColor,
                BorderThickness = new Thickness(item.IsSelected ? 2 : 1),
                CornerRadius = new CornerRadius(8),
                Height = _isCombinedMode ? double.NaN : 96,
                Margin = new Thickness(3),
                Cursor = Cursors.Hand,
                Tag = index.ToString(),
                AllowDrop = true,
                ClipToBounds = true,
                VerticalAlignment = _isCombinedMode ? VerticalAlignment.Stretch : VerticalAlignment.Top
            };

            // Enable Drag-and-Drop from Web Browser / Explorer into slot border
            border.DragOver += (s, e) =>
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            };
            border.Drop += Control_Drop;

            // Enable WPF-to-WebView Drag Source from slot border
            border.PreviewMouseLeftButtonDown += Src_PreviewMouseLeftButtonDown;
            border.PreviewMouseLeftButtonUp += Src_PreviewMouseLeftButtonUp;
            border.PreviewMouseMove += Src_PreviewMouseMove;

            // Click behavior: if empty -> upload image for this slot; if has image -> select/focus slot
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (!item.HasImage)
                {
                    UploadImageForSingleSlot(index);
                }
                else
                {
                    SelectSlot(index);
                }
            };

            var grid = new Grid();

            // 1. Photoshop Checkered Background
            var checkerRect = new Rectangle
            {
                Fill = TryFindResource("PsCheckeredBrush") as Brush ?? Brushes.DarkGray,
                SnapsToDevicePixels = true
            };
            grid.Children.Add(checkerRect);

            // 2. High Quality Image View
            img = new Image
            {
                Source = item.Bitmap,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(2)
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            grid.Children.Add(img);

            // 3. Empty Placeholder (shows when slot is empty)
            placeholder = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = item.HasImage ? Visibility.Collapsed : Visibility.Visible
            };

            var iconTxt = new TextBlock
            {
                Text = "📷",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            placeholder.Children.Add(iconTxt);

            var labelTxt = new TextBlock
            {
                Text = $"Ảnh phụ {index + 1}",
                Foreground = FindResource("TextMuted") as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8a8f9d")),
                FontSize = 9.5,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            placeholder.Children.Add(labelTxt);

            grid.Children.Add(placeholder);

            // 4. Badge Pill Top-Left (#1, #2...)
            var badgeBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cc10121a")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3a3f50")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 1, 4, 1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4),
                IsHitTestVisible = false
            };
            var badgeTxt = new TextBlock
            {
                Text = $"#{index + 1}",
                Foreground = Brushes.White,
                FontSize = 9,
                FontWeight = FontWeights.Bold
            };
            badgeBorder.Child = badgeTxt;
            grid.Children.Add(badgeBorder);

            // 5. Dual Action Buttons Overlay (Top-Right: + Change, x Clear) shown on hover when image exists
            var actionStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 4, 0),
                Visibility = Visibility.Collapsed
            };

            // Button '+' : Replace/Change image for this specific slot
            var btnChange = new Button
            {
                Content = "＋",
                Width = 18,
                Height = 18,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e60078d4")),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 3, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Chọn lại ảnh cho ô này"
            };

            var btnChangeTemplate = new ControlTemplate(typeof(Button));
            var bFactoryChange = new FrameworkElementFactory(typeof(Border));
            bFactoryChange.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            bFactoryChange.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            var cFactoryChange = new FrameworkElementFactory(typeof(ContentPresenter));
            cFactoryChange.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cFactoryChange.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bFactoryChange.AppendChild(cFactoryChange);
            btnChangeTemplate.VisualTree = bFactoryChange;
            btnChange.Template = btnChangeTemplate;

            btnChange.Click += (s, e) =>
            {
                e.Handled = true;
                UploadImageForSingleSlot(index);
            };
            actionStack.Children.Add(btnChange);

            // Button 'x' : Remove/Clear image from this slot
            var btnDelete = new Button
            {
                Content = "✕",
                Width = 18,
                Height = 18,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e6dc3545")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Gỡ ảnh khỏi ô này"
            };

            var btnDeleteTemplate = new ControlTemplate(typeof(Button));
            var bFactoryDelete = new FrameworkElementFactory(typeof(Border));
            bFactoryDelete.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            bFactoryDelete.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            var cFactoryDelete = new FrameworkElementFactory(typeof(ContentPresenter));
            cFactoryDelete.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cFactoryDelete.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bFactoryDelete.AppendChild(cFactoryDelete);
            btnDeleteTemplate.VisualTree = bFactoryDelete;
            btnDelete.Template = btnDeleteTemplate;

            btnDelete.Click += (s, e) =>
            {
                e.Handled = true;
                ClearSlot(index);
            };
            actionStack.Children.Add(btnDelete);

            grid.Children.Add(actionStack);

            // Hover events to show/hide actionStack
            border.MouseEnter += (s, e) =>
            {
                if (!_secondaryImages[index].IsSelected)
                {
                    border.BorderBrush = FindResource("AccentColor") as Brush ?? accentColor;
                }
                if (_secondaryImages[index].HasImage)
                {
                    actionStack.Visibility = Visibility.Visible;
                }
            };

            border.MouseLeave += (s, e) =>
            {
                if (!_secondaryImages[index].IsSelected)
                {
                    border.BorderBrush = borderColor;
                }
                actionStack.Visibility = Visibility.Collapsed;
            };

            border.Child = grid;
            return border;
        }

        private void UploadImageForSingleSlot(int index)
        {
            if (index < 0 || index >= _secondaryImages.Count) return;

            var dialog = new OpenFileDialog
            {
                Title = $"Chọn ảnh cho Ô phụ #{index + 1}",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(dialog.FileName);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    _secondaryImages[index].Bitmap = bmp;
                    _secondaryImages[index].FilePath = dialog.FileName;
                    _secondaryImages[index].IsSelected = true;

                    SelectSlot(index);
                    RefreshSlotUI(index);
                    UpdateSecondaryInfo();
                    UpdatePreviewImage();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi nạp ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= _secondaryImages.Count) return;

            // Toggle selection or select
            for (int i = 0; i < _secondaryImages.Count; i++)
            {
                _secondaryImages[i].IsSelected = (i == index);
            }

            // Update UI borders
            var accentColor = FindResource("AccentColor") as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
            var borderColor = FindResource("BorderColor") as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));

            for (int i = 0; i < _slotBorders.Count; i++)
            {
                bool sel = _secondaryImages[i].IsSelected;
                _slotBorders[i].BorderBrush = sel ? accentColor : borderColor;
                _slotBorders[i].BorderThickness = new Thickness(sel ? 2 : 1);
            }
            for (int i = 0; i < _slotBordersWv.Count; i++)
            {
                bool sel = _secondaryImages[i].IsSelected;
                _slotBordersWv[i].BorderBrush = sel ? accentColor : borderColor;
                _slotBordersWv[i].BorderThickness = new Thickness(sel ? 2 : 1);
            }

            UpdateSecondaryInfo();
            UpdatePreviewImage();
        }

        private void ClearSlot(int index)
        {
            if (index < 0 || index >= _secondaryImages.Count) return;

            _secondaryImages[index].Bitmap = null;
            _secondaryImages[index].FilePath = null;
            _secondaryImages[index].IsSelected = false;

            RefreshSlotUI(index);
            UpdateSecondaryInfo();
            UpdatePreviewImage();
        }

        private void RefreshSlotUI(int index)
        {
            if (index < 0 || index >= _secondaryImages.Count) return;
            var item = _secondaryImages[index];

            if (index < _slotImages.Count)
            {
                _slotImages[index].Source = item.Bitmap;
                _slotPlaceholders[index].Visibility = item.HasImage ? Visibility.Collapsed : Visibility.Visible;
            }
            if (index < _slotImagesWv.Count)
            {
                _slotImagesWv[index].Source = item.Bitmap;
                _slotPlaceholdersWv[index].Visibility = item.HasImage ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void RefreshAllSlotsUI()
        {
            for (int i = 0; i < _secondaryImages.Count; i++)
            {
                RefreshSlotUI(i);
            }
        }

        private void UpdateSecondaryInfo()
        {
            if (TxtSecondaryInfo == null) return;

            int loadedCount = _secondaryImages.Count(x => x.HasImage);
            int selectedIdx = GetSelectedSlotIndex();

            string infoText;
            if (!_isCombinedMode)
            {
                if (selectedIdx >= 0)
                {
                    infoText = $"⚡ Đang dùng: Ảnh phụ {selectedIdx + 1} (Chế độ Ảnh đơn)";
                }
                else
                {
                    infoText = $"⚡ Chưa chọn ảnh phụ (Chế độ Ảnh đơn)";
                }
            }
            else
            {
                infoText = $"📁 Đã nạp {loadedCount}/{_secondarySlotCount} ảnh phụ (Chế độ Ảnh chung)";
            }

            TxtSecondaryInfo.Text = infoText;
        }

        private int GetSelectedSlotIndex()
        {
            for (int i = 0; i < _secondaryImages.Count; i++)
            {
                if (_secondaryImages[i].IsSelected) return i;
            }
            return -1;
        }

        private void BtnUploadSecondary_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn ảnh phụ (Có thể chọn nhiều ảnh)",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
            {
                var files = dialog.FileNames;

                // Find first slot index to start populating (selected slot or first empty slot)
                int startIdx = GetSelectedSlotIndex();
                if (startIdx < 0)
                {
                    for (int i = 0; i < _secondaryImages.Count; i++)
                    {
                        if (!_secondaryImages[i].HasImage)
                        {
                            startIdx = i;
                            break;
                        }
                    }
                    if (startIdx < 0) startIdx = 0;
                }

                // If selected files count exceeds current slots, automatically expand slot count to fit all uploaded files!
                int requiredSlots = startIdx + files.Length;
                if (requiredSlots > _secondarySlotCount)
                {
                    SetSlotCount(requiredSlots);
                }

                for (int f = 0; f < files.Length; f++)
                {
                    int targetSlot = startIdx + f;
                    if (targetSlot >= _secondaryImages.Count) break;

                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(files[f]);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        _secondaryImages[targetSlot].Bitmap = bmp;
                        _secondaryImages[targetSlot].FilePath = files[f];
                        _secondaryImages[targetSlot].IsSelected = (f == 0);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi nạp ảnh {files[f]}: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                RefreshAllSlotsUI();
                UpdateSecondaryInfo();
                UpdatePreviewImage();
            }
        }

        #endregion

        #region Aspect Ratio & Preview

        private void CmbBatchSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingUI) return;
        }

        private void CmbAspectRatio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingUI) return;

            bool isCustom = CmbAspectRatio.SelectedIndex == 6; // Custom
            if (PanelCustomSize != null) PanelCustomSize.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

            UpdatePreviewImage();
        }

        private void TxtCustomSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingUI) return;
            UpdatePreviewImage();
        }

        private static BitmapSource ResizeBitmapHighQuality(BitmapSource source, int targetWidth, int targetHeight, bool uniformToFill)
        {
            if (source == null || targetWidth <= 0 || targetHeight <= 0) return source!;

            // Copy source pixels to byte array
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);

            // Compute scaling offset and draw dimensions
            float scaleX = (float)targetWidth / source.PixelWidth;
            float scaleY = (float)targetHeight / source.PixelHeight;

            float drawScale = uniformToFill ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
            float drawW = source.PixelWidth * drawScale;
            float drawH = source.PixelHeight * drawScale;

            float x = (targetWidth - drawW) / 2.0f;
            float y = (targetHeight - drawH) / 2.0f;

            // Create target WriteableBitmap and perform high-quality scaling using SkiaSharp
            var targetBmp = new WriteableBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Bgra32, null);
            targetBmp.Lock();
            try
            {
                var srcInfo = new SkiaSharp.SKImageInfo(source.PixelWidth, source.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                var dstInfo = new SkiaSharp.SKImageInfo(targetWidth, targetHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);

                using (var srcBitmap = new SkiaSharp.SKBitmap())
                {
                    var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                    try
                    {
                        srcBitmap.InstallPixels(srcInfo, handle.AddrOfPinnedObject(), stride);

                        using (var surface = SkiaSharp.SKSurface.Create(dstInfo, targetBmp.BackBuffer, targetBmp.BackBufferStride))
                        {
                            if (surface != null)
                            {
                                var canvas = surface.Canvas;
                                canvas.Clear(SkiaSharp.SKColors.Transparent);
                                using (var paint = new SkiaSharp.SKPaint())
                                {
                                    paint.FilterQuality = SkiaSharp.SKFilterQuality.High;
                                    paint.IsAntialias = true;
                                    canvas.DrawBitmap(srcBitmap, new SkiaSharp.SKRect(x, y, x + drawW, y + drawH), paint);
                                }
                            }
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                targetBmp.AddDirtyRect(new Int32Rect(0, 0, targetWidth, targetHeight));
            }
            finally
            {
                targetBmp.Unlock();
            }

            targetBmp.Freeze();
            return targetBmp;
        }

        private static Rect GetLayerContentBounds(BitmapSource source)
        {
            if (source == null) return Rect.Empty;

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];

            source.CopyPixels(pixels, stride, 0);

            int minX = width, minY = height, maxX = -1, maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * stride;
                for (int x = 0; x < width; x++)
                {
                    byte alpha = pixels[rowOffset + x * 4 + 3];
                    if (alpha > 5)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY) return Rect.Empty;

            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static BitmapSource DrawPreviewImage(BitmapSource baseImage, double? targetAspectRatio, int? targetWidth, int? targetHeight, bool drawCheckerboard = true)
        {
            if (baseImage == null) return null!;

            int baseW = baseImage.PixelWidth;
            int baseH = baseImage.PixelHeight;

            int canvasW = baseW;
            int canvasH = baseH;

            if (targetWidth.HasValue && targetHeight.HasValue && targetWidth.Value > 0 && targetHeight.Value > 0)
            {
                canvasW = targetWidth.Value;
                canvasH = targetHeight.Value;
            }
            else if (targetAspectRatio.HasValue && targetAspectRatio.Value > 0)
            {
                double currentRatio = (double)baseW / baseH;
                if (currentRatio > targetAspectRatio.Value)
                {
                    canvasW = baseW;
                    canvasH = (int)Math.Round(baseW / targetAspectRatio.Value);
                }
                else
                {
                    canvasH = baseH;
                    canvasW = (int)Math.Round(baseH * targetAspectRatio.Value);
                }
            }

            var writeableBmp = new WriteableBitmap(canvasW, canvasH, 96, 96, PixelFormats.Bgra32, null);
            writeableBmp.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(canvasW, canvasH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, writeableBmp.BackBuffer, writeableBmp.BackBufferStride))
                {
                    var canvas = surface.Canvas;

                    // 1. Draw Checkerboard background if transparent
                    if (drawCheckerboard)
                    {
                        int checkSize = 16;
                        var darkPaint1 = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#14161d") };
                        var darkPaint2 = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#222531") };
                        canvas.Clear(darkPaint1.Color);

                        for (int cy = 0; cy < canvasH; cy += checkSize)
                        {
                            for (int cx = 0; cx < canvasW; cx += checkSize)
                            {
                                bool isEven = ((cx / checkSize) + (cy / checkSize)) % 2 == 0;
                                canvas.DrawRect(cx, cy, checkSize, checkSize, isEven ? darkPaint1 : darkPaint2);
                            }
                        }
                    }
                    else
                    {
                        canvas.Clear(SkiaSharp.SKColors.Transparent);
                    }

                    // 2. Draw centered baseImage
                    var stride = baseW * 4;
                    var pixels = new byte[stride * baseH];
                    baseImage.CopyPixels(pixels, stride, 0);

                    using (var skBitmap = new SkiaSharp.SKBitmap())
                    {
                        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try
                        {
                            var baseInfo = new SkiaSharp.SKImageInfo(baseW, baseH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                            skBitmap.InstallPixels(baseInfo, handle.AddrOfPinnedObject(), stride);

                            float drawX = (canvasW - baseW) / 2.0f;
                            float drawY = (canvasH - baseH) / 2.0f;

                            using (var paint = new SkiaSharp.SKPaint())
                            {
                                paint.FilterQuality = SkiaSharp.SKFilterQuality.High;
                                paint.IsAntialias = true;
                                canvas.DrawBitmap(skBitmap, drawX, drawY, paint);
                            }
                        }
                        finally
                        {
                            handle.Free();
                        }
                    }
                }

                writeableBmp.AddDirtyRect(new Int32Rect(0, 0, canvasW, canvasH));
            }
            finally
            {
                writeableBmp.Unlock();
            }

            writeableBmp.Freeze();
            return writeableBmp;
        }

        private BitmapSource MaskAndCropSecondaryImage(BitmapSource secondary, BitmapSource croppedOriginal)
        {
            try
            {
                int srcW = croppedOriginal.PixelWidth;
                int srcH = croppedOriginal.PixelHeight;

                // Compute high-resolution dimensions matching the croppedOriginal aspect ratio
                double scale = Math.Max(1.0, Math.Max((double)secondary.PixelWidth / srcW, (double)secondary.PixelHeight / srcH));
                int hiW = (int)Math.Round(srcW * scale);
                int hiH = (int)Math.Round(srcH * scale);

                // 1. Resize secondary image to match high-resolution dimensions (crop/fill aspect ratio) using SkiaSharp-backed Resize
                BitmapSource resizedSecondary = ResizeBitmapHighQuality(secondary, hiW, hiH, uniformToFill: true);

                // 2. Resize original cropped image (which contains the mask) using SkiaSharp-backed Resize
                BitmapSource resizedOriginal = ResizeBitmapHighQuality(croppedOriginal, hiW, hiH, uniformToFill: false);

                // 3. Perform native masking via SkiaSharp DstIn blend mode (extremely fast!)
                var maskedBmp = new WriteableBitmap(hiW, hiH, 96, 96, PixelFormats.Bgra32, null);
                maskedBmp.Lock();
                try
                {
                    var info = new SkiaSharp.SKImageInfo(hiW, hiH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                    using (var surface = SkiaSharp.SKSurface.Create(info, maskedBmp.BackBuffer, maskedBmp.BackBufferStride))
                    {
                        var canvas = surface.Canvas;
                        canvas.Clear(SkiaSharp.SKColors.Transparent);

                        // Draw secondary image
                        var secStride = resizedSecondary.PixelWidth * 4;
                        var secPixels = new byte[secStride * resizedSecondary.PixelHeight];
                        resizedSecondary.CopyPixels(secPixels, secStride, 0);

                        using (var secSkBmp = new SkiaSharp.SKBitmap())
                        {
                            var handleSec = System.Runtime.InteropServices.GCHandle.Alloc(secPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                            try
                            {
                                secSkBmp.InstallPixels(info, handleSec.AddrOfPinnedObject(), secStride);
                                canvas.DrawBitmap(secSkBmp, 0, 0);
                            }
                            finally
                            {
                                handleSec.Free();
                            }
                        }

                        // Apply original alpha mask via DstIn
                        var origStride = resizedOriginal.PixelWidth * 4;
                        var origPixels = new byte[origStride * resizedOriginal.PixelHeight];
                        resizedOriginal.CopyPixels(origPixels, origStride, 0);

                        using (var origSkBmp = new SkiaSharp.SKBitmap())
                        {
                            var handleOrig = System.Runtime.InteropServices.GCHandle.Alloc(origPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                            try
                            {
                                origSkBmp.InstallPixels(info, handleOrig.AddrOfPinnedObject(), origStride);
                                using (var paint = new SkiaSharp.SKPaint())
                                {
                                    paint.BlendMode = SkiaSharp.SKBlendMode.DstIn;
                                    canvas.DrawBitmap(origSkBmp, 0, 0, paint);
                                }
                            }
                            finally
                            {
                                handleOrig.Free();
                            }
                        }
                    }
                    maskedBmp.AddDirtyRect(new Int32Rect(0, 0, hiW, hiH));
                }
                finally
                {
                    maskedBmp.Unlock();
                }

                maskedBmp.Freeze();
                return maskedBmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to mask secondary image: {ex.Message}");
                return secondary;
            }
        }

        private void UpdatePreviewImage()
        {
            if (ImgPreview == null || _activeLayer == null) return;

            try
            {
                // 1. Get the cropped original image first (as the base and alpha template)
                BitmapSource baseImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
                var bounds = GetLayerContentBounds(baseImg);
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, baseImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, baseImg.PixelHeight - 1);
                    int w = Math.Clamp((int)Math.Ceiling(bounds.Width), 1, baseImg.PixelWidth - x);
                    int h = Math.Clamp((int)Math.Ceiling(bounds.Height), 1, baseImg.PixelHeight - y);
                    if (w > 0 && h > 0 && (x > 0 || y > 0 || w < baseImg.PixelWidth || h < baseImg.PixelHeight))
                    {
                        baseImg = new CroppedBitmap(baseImg, new Int32Rect(x, y, w, h));
                    }
                }

                BitmapSource sourceImg;

                // 2. In OFF mode, check if a slot is selected. If so, override preview source with masked secondary image.
                int selectedSlotIdx = GetSelectedSlotIndex();
                if (!_sendModeOn && selectedSlotIdx >= 0 && _secondaryImages[selectedSlotIdx].HasImage)
                {
                    sourceImg = MaskAndCropSecondaryImage(_secondaryImages[selectedSlotIdx].Bitmap!, baseImg);
                }
                else
                {
                    sourceImg = baseImg;
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
                RenderOptions.SetBitmapScalingMode(ImgPreview, BitmapScalingMode.HighQuality);

                if (ImgPreviewWv != null)
                {
                    ImgPreviewWv.Source = processedImg;
                    RenderOptions.SetBitmapScalingMode(ImgPreviewWv, BitmapScalingMode.HighQuality);
                }
            }
            catch { }
        }

        #endregion
    }
}
