// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Helpers;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using FlowMy.Views.NodeControls;
using CefSharp;
using CefSharp.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        #region Secondary Images Slots

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
                SetSlotCount(count);
            }
        }

        private void SetSlotCount(int count)
        {
            if (count < 1) count = 1;
            if (count > 100) count = 100; // Reasonable upper limit
            if (count == _secondarySlotCount) return;

            _secondarySlotCount = count;
            EnsureSecondaryImagesCount(count);
            RebuildSecondaryGrid(count);
            RebuildSecondaryGridWv(count);
            UpdateSlotCountUI(count);
            SetupDragAndDrop();
            RefreshAllSlotsUI();

            // Persist to active layer
            _activeLayer.LayerAiSecondarySlotCount = count;
        }

        private void EnsureSecondaryImagesCount(int count)
        {
            // Grow: add empty items
            while (_secondaryImages.Count < count)
                _secondaryImages.Add(new SecondaryImageItem());
            // We do NOT shrink — data is preserved but hidden
        }

        private void UpdateSlotCountUI(int count)
        {
            _isUpdatingSlotCount = true;
            try
            {
                if (TxtSlotCount != null) TxtSlotCount.Text = count.ToString();

                // Update header text
                if (TxtSecondaryHeader != null) TxtSecondaryHeader.Text = $"🖼️ ẢNH PHỤ ({count})";
                if (TxtSecondaryHeaderWv != null) TxtSecondaryHeaderWv.Text = $"🖼️ Ảnh phụ ({count})";

                // Update preset button highlights
                var presetButtons = new[] { BtnSlot4, BtnSlot6, BtnSlot8, BtnSlot10 };
                foreach (var btn in presetButtons)
                {
                    if (btn == null) continue;
                    bool isActive = btn.Tag is string t && int.TryParse(t, out int v) && v == count;
                    btn.Style = null; // Reset to allow direct property setting
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#4fffb0" : "#252a39"));
                    btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#111318" : "#788296"));
                    // Re-apply template with CornerRadius
                    var template = new ControlTemplate(typeof(Button));
                    var borderFactory = new FrameworkElementFactory(typeof(Border));
                    borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                    borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                    borderFactory.SetValue(Border.PaddingProperty, new Thickness(2, 0, 2, 0));
                    var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                    contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                    borderFactory.AppendChild(contentFactory);
                    template.VisualTree = borderFactory;
                    btn.Template = template;
                }
            }
            finally
            {
                _isUpdatingSlotCount = false;
            }
        }

        private void RebuildSecondaryGrid(int count)
        {
            SecondaryImagesGrid.Children.Clear();
            SecondaryImagesGrid.RowDefinitions.Clear();
            SecondaryImagesGrid.ColumnDefinitions.Clear();
            _slotBorders.Clear();
            _slotImages.Clear();
            _slotPlaceholders.Clear();
            _slotChecks.Clear();
            _slotRemoves.Clear();
            _slotChildPanels.Clear();

            if (count <= 0) return;

            // Calculate a balanced grid layout (square-ish)
            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling((double)count / cols);

            // Create row definitions with gaps
            for (int r = 0; r < rows; r++)
            {
                if (r > 0) SecondaryImagesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
                SecondaryImagesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            // Create column definitions with gaps
            for (int c = 0; c < cols; c++)
            {
                if (c > 0) SecondaryImagesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                SecondaryImagesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Create slot borders in row-major order
            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int gridRow = row * 2; // account for gap rows
                int gridCol = col * 2; // account for gap cols

                var border = CreateSlotBorder(i, isCompact: false);
                Grid.SetRow(border, gridRow);
                Grid.SetColumn(border, gridCol);
                SecondaryImagesGrid.Children.Add(border);
            }
        }

        private void RebuildSecondaryGridWv(int count)
        {
            SecondaryImagesGridWv.Children.Clear();
            SecondaryImagesGridWv.RowDefinitions.Clear();
            SecondaryImagesGridWv.ColumnDefinitions.Clear();
            _slotBordersWv.Clear();
            _slotImagesWv.Clear();
            _slotPlaceholdersWv.Clear();
            _slotChecksWv.Clear();
            _slotRemovesWv.Clear();
            _slotChildPanelsWv.Clear();

            if (count <= 0) return;

            // Calculate a balanced grid layout (square-ish)
            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling((double)count / cols);

            for (int r = 0; r < rows; r++)
            {
                if (r > 0) SecondaryImagesGridWv.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) });
                SecondaryImagesGridWv.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (int c = 0; c < cols; c++)
            {
                if (c > 0) SecondaryImagesGridWv.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
                SecondaryImagesGridWv.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int gridRow = row * 2;
                int gridCol = col * 2;

                var border = CreateSlotBorder(i, isCompact: true);
                Grid.SetRow(border, gridRow);
                Grid.SetColumn(border, gridCol);
                SecondaryImagesGridWv.Children.Add(border);
            }
        }

        /// <summary>Create a single slot Border with all child elements (checkerboard, image, placeholder, check, remove, number badge).</summary>
        private Border CreateSlotBorder(int index, bool isCompact)
        {
            // SecondaryImageBorder style reference
            var borderStyle = (Style)FindResource("SecondaryImageBorder");

            var border = new Border
            {
                Style = borderStyle,
                Focusable = true,
                Tag = index.ToString(),
                AllowDrop = true
            };
            border.KeyDown += SlotBorder_KeyDown;
            border.MouseEnter += Slot_MouseEnter;
            border.MouseLeave += Slot_MouseLeave;
            border.MouseLeftButtonDown += Slot_Click;
            border.DragOver += (s, e) =>
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            };
            border.Drop += Control_Drop;

            var grid = new Grid();

            // Checkerboard
            var checkerBrush = (Brush)FindResource("PsDarkCheckeredBrush");
            var rect = new System.Windows.Shapes.Rectangle { Fill = checkerBrush, SnapsToDevicePixels = true };
            grid.Children.Add(rect);

            // Image
            var image = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(isCompact ? 1 : 2) };
            grid.Children.Add(image);

            // Placeholder
            var placeholder = new TextBlock
            {
                Text = "＋",
                FontSize = isCompact ? 16 : 24,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3a3f52")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            grid.Children.Add(placeholder);

            // Check badge for AI ID (✓ AI)
            var checkBadge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0")),
                CornerRadius = new CornerRadius(0, 0, 6, 0),
                Padding = isCompact ? new Thickness(3, 1, 3, 1) : new Thickness(6, 2, 6, 2),
                Visibility = Visibility.Collapsed
            };
            checkBadge.Child = new TextBlock
            {
                Text = "✓ AI",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111318")),
                FontSize = isCompact ? 8 : 10,
                FontWeight = FontWeights.Bold
            };
            grid.Children.Add(checkBadge);

            // Remove button
            var removeBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cc3333")),
                CornerRadius = new CornerRadius(0, 0, 0, 6),
                Padding = isCompact ? new Thickness(4, 1, 4, 1) : new Thickness(5, 2, 5, 2),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                Tag = index.ToString()
            };
            removeBorder.Child = new TextBlock
            {
                Text = "✕",
                Foreground = Brushes.White,
                FontSize = isCompact ? 8 : 9,
                FontWeight = FontWeights.Bold
            };
            removeBorder.MouseLeftButtonDown += SlotRemove_Click;
            grid.Children.Add(removeBorder);

            // Number badge
            var numberBadge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#aa111318")),
                CornerRadius = new CornerRadius(0, 4, 0, 0),
                Padding = new Thickness(5, 2, 5, 2),
                IsHitTestVisible = false
            };
            numberBadge.Child = new TextBlock
            {
                Text = (index + 1).ToString(),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dde3ef")),
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            grid.Children.Add(numberBadge);

            // Child thumbnails panel (ô ảnh con nhỏ khi ảnh phụ cũ có ID)
            var childScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = isCompact ? new Thickness(0, 0, 2, 2) : new Thickness(0, 0, 4, 2),
                MaxHeight = isCompact ? 22 : 28,
                MaxWidth = isCompact ? 100 : 160
            };

            var childStackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            childScrollViewer.Content = childStackPanel;
            // grid.Children.Add(childScrollViewer); // Removed as per user request to hide inline history thumbnails

            border.Child = grid;

            // Register in the appropriate lists
            if (isCompact)
            {
                _slotBordersWv.Add(border);
                _slotImagesWv.Add(image);
                _slotPlaceholdersWv.Add(placeholder);
                _slotChecksWv.Add(checkBadge);
                _slotRemovesWv.Add(removeBorder);
                _slotChildPanelsWv.Add(childStackPanel);
            }
            else
            {
                _slotBorders.Add(border);
                _slotImages.Add(image);
                _slotPlaceholders.Add(placeholder);
                _slotChecks.Add(checkBadge);
                _slotRemoves.Add(removeBorder);
                _slotChildPanels.Add(childStackPanel);
            }

            return border;
        }

        private void BtnAddSecondary_Click(object sender, RoutedEventArgs e)
        {
            // Find first empty slot
            int emptySlot = -1;
            for (int i = 0; i < _secondarySlotCount && i < _secondaryImages.Count; i++)
            {
                if (!_secondaryImages[i].HasImage)
                {
                    emptySlot = i;
                    break;
                }
            }

            if (emptySlot == -1)
            {
                MessageBox.Show($"Đã đủ {_secondarySlotCount} ảnh phụ. Hãy xóa ảnh cũ trước.", "Ảnh phụ", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    if (slotIdx >= _secondarySlotCount) break;

                    // Find next empty slot
                    while (slotIdx < _secondarySlotCount && slotIdx < _secondaryImages.Count && _secondaryImages[slotIdx].HasImage)
                        slotIdx++;
                    if (slotIdx >= _secondarySlotCount) break;

                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(file);
                        bmp.EndInit();
                        bmp.Freeze();

                        _secondaryImages[slotIdx].SetNewImage(bmp, file);
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
                border.Focus();
                if (idx < 0 || idx >= _secondarySlotCount || idx >= _secondaryImages.Count) return;

                if (_secondaryImages[idx].HasImage)
                {
                    if (_sendModeOn)
                    {
                        // ON mode: toggle selection, allow multi-select
                        _secondaryImages[idx].IsSelected = !_secondaryImages[idx].IsSelected;
                        RefreshSlotUI(idx);
                        UpdateSecondaryInfo();
                        UpdatePreviewImage();
                    }
                    else
                    {
                        // OFF mode: toggle selection, enforce single-select
                        bool wasSelected = _secondaryImages[idx].IsSelected;
                        for (int i = 0; i < _secondaryImages.Count; i++)
                        {
                            _secondaryImages[i].IsSelected = false;
                        }
                        _secondaryImages[idx].IsSelected = !wasSelected;
                        
                        RefreshAllSlotsUI();
                        UpdateSecondaryInfo();
                        UpdatePreviewImage();
                    }
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

                            if (!_sendModeOn)
                            {
                                // OFF mode: deselect all others, select this one
                                for (int i = 0; i < _secondaryImages.Count; i++)
                                {
                                    _secondaryImages[i].IsSelected = false;
                                }
                            }
                            _secondaryImages[idx].SetNewImage(bmp, dlg.FileName);
                        }
                        catch { }
                        RefreshAllSlotsUI();
                        UpdateSecondaryInfo();
                        UpdatePreviewImage();
                    }
                }

                e.Handled = true;
            }
        }

        private void SlotRemove_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
                {
                    _secondaryImages[idx].ArchiveCurrentIfHasId();
                    _secondaryImages[idx].Bitmap = null;
                    _secondaryImages[idx].FilePath = null;
                    _secondaryImages[idx].ResetImageIdAndCodeId();
                    _secondaryImages[idx].IsSelected = false;
                    RefreshAllSlotsUI();
                    UpdateSecondaryInfo();
                    UpdatePreviewImage();
                }
                e.Handled = true;
            }
        }

        private void Slot_MouseEnter(object sender, MouseEventArgs e)
        {
            _hoveredImageContainer = sender as FrameworkElement;
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
                {
                    // Hover glow effect
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                    border.BorderThickness = new Thickness(2);

                    // Show remove button if has image
                    if (_secondaryImages[idx].HasImage)
                    {
                        if (idx < _slotRemoves.Count) _slotRemoves[idx].Visibility = Visibility.Visible;
                        if (idx < _slotRemovesWv.Count) _slotRemovesWv[idx].Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void Slot_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoveredImageContainer == sender)
            {
                _hoveredImageContainer = null;
            }
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
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
                    if (idx < _slotRemoves.Count) _slotRemoves[idx].Visibility = Visibility.Collapsed;
                    if (idx < _slotRemovesWv.Count) _slotRemovesWv[idx].Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ImgPreview_MouseEnter(object sender, MouseEventArgs e)
        {
            _hoveredImageContainer = sender as FrameworkElement;
        }

        private void ImgPreview_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoveredImageContainer == sender)
            {
                _hoveredImageContainer = null;
            }
        }

        private async void LayerAiDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (_hoveredImageContainer != null)
                {
                    var bmp = await GetImageFromClipboardAsync();
                    if (bmp != null)
                    {
                        e.Handled = true;
                        ProcessHoveredSlotPaste(bmp);
                    }
                }
            }
        }

        private void ProcessHoveredSlotPaste(BitmapSource bitmap)
        {
            if (_hoveredImageContainer == null) return;
            FlashSlotBorder(_hoveredImageContainer);

            string name = _hoveredImageContainer.Name ?? "";
            bool isMainImage = name == "ImgPreview" || name == "ImgPreviewWv";

            if (isMainImage)
            {
                try
                {
                    int layerW = _activeLayer.Width;
                    int layerH = _activeLayer.Height;
                    var resized = ResizeBitmapHighQuality(bitmap, layerW, layerH, uniformToFill: true);
                    
                    var stride = layerW * 4;
                    var pixels = new byte[stride * layerH];
                    resized.CopyPixels(pixels, stride, 0);
                    
                    _activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, layerW, layerH), pixels, stride, 0);
                    _activeLayer.InvalidateThumbnail();
                    
                    UpdatePreviewImage();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update main image from paste: {ex.Message}");
                }
            }
            else
            {
                // It is a slot border
                int idx = -1;
                if (_hoveredImageContainer is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int tagIdx))
                {
                    idx = tagIdx;
                }
                else
                {
                    if (name.EndsWith("0")) idx = 0;
                    else if (name.EndsWith("1")) idx = 1;
                    else if (name.EndsWith("2")) idx = 2;
                    else if (name.EndsWith("3")) idx = 3;
                }

                if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
                {
                    _secondaryImages[idx].SetNewImage(bitmap, null);
                    RefreshAllSlotsUI();
                }
            }
        }

        private async Task<BitmapSource?> GetImageFromClipboardAsync()
        {
            try
            {
                var dataObject = Clipboard.GetDataObject();
                if (dataObject == null) return null;

                // 0a. Check FileContents (direct original compressed file data from clipboard)
                if (dataObject.GetDataPresent("FileContents"))
                {
                    var bmp = GetImageFromFileContentsCOM(dataObject);
                    if (bmp != null) return bmp;
                }

                // 0. Check DeviceIndependentBitmap / DeviceIndependentBitmapV5
                if (dataObject.GetDataPresent("DeviceIndependentBitmap"))
                {
                    var data = dataObject.GetData("DeviceIndependentBitmap");
                    if (data is MemoryStream ms)
                    {
                        var bmp = GetImageFromDIB(ms);
                        if (bmp != null) return bmp;
                    }
                }
                if (dataObject.GetDataPresent("DeviceIndependentBitmapV5"))
                {
                    var data = dataObject.GetData("DeviceIndependentBitmapV5");
                    if (data is MemoryStream ms)
                    {
                        var bmp = GetImageFromDIB(ms);
                        if (bmp != null) return bmp;
                    }
                }

                // 1. Check Bitmap directly
                if (dataObject.GetDataPresent(DataFormats.Bitmap))
                {
                    if (dataObject.GetData(DataFormats.Bitmap) is BitmapSource bmp)
                    {
                        return bmp;
                    }
                }

                // 2. Check FileDrop
                if (dataObject.GetDataPresent(DataFormats.FileDrop))
                {
                    if (dataObject.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    {
                        var filePath = files[0];
                        if (File.Exists(filePath))
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                    }
                }

                // 3. Check HTML Format / URL
                string? url = null;
                string? sourcePageUrl = null;
                if (dataObject.GetDataPresent(DataFormats.Html))
                {
                    if (dataObject.GetData(DataFormats.Html) is string htmlText)
                    {
                        var sourceUrlMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"SourceURL:\s*([^\r\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (sourceUrlMatch.Success)
                        {
                            sourcePageUrl = sourceUrlMatch.Groups[1].Value.Trim();
                        }

                        string? foundUrl = null;
                        var attributes = new[] { "data-src", "data-original", "data-srcset", "srcset", "src" };
                        foreach (var attr in attributes)
                        {
                            var regex = new System.Text.RegularExpressions.Regex(
                                attr + @"\s*=\s*[""']([^""' >]+)[""']", 
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            var match = regex.Match(htmlText);
                            if (match.Success)
                            {
                                foundUrl = match.Groups[1].Value;
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(foundUrl))
                        {
                            url = foundUrl;
                            url = System.Net.WebUtility.HtmlDecode(url);
                        }
                    }
                }

                if (string.IsNullOrEmpty(url) && dataObject.GetDataPresent(DataFormats.Text))
                {
                    url = dataObject.GetData(DataFormats.Text) as string;
                }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    url = url.Trim();
                    if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                    {
                        return CreateBitmapFromBase64(url);
                    }

                    Uri? uri = null;
                    if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
                    {
                        uri = absoluteUri;
                    }
                    else
                    {
                        string? pageUrl = sourcePageUrl;
                        if (string.IsNullOrWhiteSpace(pageUrl))
                        {
                            ChromiumWebBrowser? activeWv = null;
                            if (_activeTab == ActiveTab.WebBrowser && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count) activeWv = _webTabs[_activeTabIdx].WebView;
                            else if (_activeTab == ActiveTab.WebView) activeWv = _dynamicWebView;
                            if (activeWv != null && !string.IsNullOrWhiteSpace(activeWv.Address))
                            {
                                pageUrl = activeWv.Address;
                            }
                        }
                        if (string.IsNullOrWhiteSpace(pageUrl)) pageUrl = _node?.LayerAiWebUrl;
                        if (string.IsNullOrWhiteSpace(pageUrl)) pageUrl = TxtWebUrl?.Text;

                        if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri))
                        {
                            if (Uri.TryCreate(baseUri, url, out var resolvedUri))
                            {
                                uri = resolvedUri;
                            }
                        }
                    }

                    if (uri != null)
                    {
                        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                        {
                            return await DownloadImageAsync(uri);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get image from clipboard: {ex.Message}");
            }
            return null;
        }

        private void RefreshAllSlotsUI()
        {
            for (int i = 0; i < _secondarySlotCount && i < _secondaryImages.Count; i++)
                RefreshSlotUI(i);
            UpdateSecondaryInfo();
            RefreshHistoryPanelUI();
        }

        private void RefreshSlotUI(int idx)
        {
            if (idx < 0 || idx >= _secondarySlotCount || idx >= _secondaryImages.Count) return;
            if (idx >= _slotBorders.Count || idx >= _slotImages.Count) return;

            var item = _secondaryImages[idx];

            if (item.HasImage)
            {
                // Normal slots
                _slotImages[idx].Source = item.Bitmap;
                _slotPlaceholders[idx].Visibility = Visibility.Collapsed;
                if (idx < _slotChecks.Count) _slotChecks[idx].Visibility = item.IsSelected ? Visibility.Visible : Visibility.Collapsed;

                // Expanded slots
                if (idx < _slotImagesWv.Count) _slotImagesWv[idx].Source = item.Bitmap;
                if (idx < _slotPlaceholdersWv.Count) _slotPlaceholdersWv[idx].Visibility = Visibility.Collapsed;

                var activeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                var normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));

                bool hasId = !string.IsNullOrEmpty(item.GetImageId(CmbAspectRatio?.SelectedIndex ?? 3));

                if (idx < _slotChecks.Count) _slotChecks[idx].Visibility = hasId ? Visibility.Visible : Visibility.Collapsed;
                if (idx < _slotChecksWv.Count) _slotChecksWv[idx].Visibility = hasId ? Visibility.Visible : Visibility.Collapsed;

                if (item.IsSelected)
                {
                    _slotBorders[idx].BorderBrush = activeBrush;
                    _slotBorders[idx].BorderThickness = new Thickness(2);
                    if (idx < _slotBordersWv.Count)
                    {
                        _slotBordersWv[idx].BorderBrush = activeBrush;
                        _slotBordersWv[idx].BorderThickness = new Thickness(2);
                    }
                }
                else
                {
                    _slotBorders[idx].BorderBrush = normalBrush;
                    _slotBorders[idx].BorderThickness = new Thickness(1.5);
                    if (idx < _slotBordersWv.Count)
                    {
                        _slotBordersWv[idx].BorderBrush = normalBrush;
                        _slotBordersWv[idx].BorderThickness = new Thickness(1.5);
                    }
                }
            }
            else
            {
                // Normal slots
                _slotImages[idx].Source = null;
                _slotPlaceholders[idx].Visibility = Visibility.Visible;
                if (idx < _slotChecks.Count) _slotChecks[idx].Visibility = Visibility.Collapsed;
                if (idx < _slotRemoves.Count) _slotRemoves[idx].Visibility = Visibility.Collapsed;

                // Expanded slots
                if (idx < _slotImagesWv.Count) _slotImagesWv[idx].Source = null;
                if (idx < _slotPlaceholdersWv.Count) _slotPlaceholdersWv[idx].Visibility = Visibility.Visible;
                if (idx < _slotChecksWv.Count) _slotChecksWv[idx].Visibility = Visibility.Collapsed;
                if (idx < _slotRemovesWv.Count) _slotRemovesWv[idx].Visibility = Visibility.Collapsed;

                var normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));
                _slotBorders[idx].BorderBrush = normalBrush;
                _slotBorders[idx].BorderThickness = new Thickness(1.5);
                if (idx < _slotBordersWv.Count)
                {
                    _slotBordersWv[idx].BorderBrush = normalBrush;
                    _slotBordersWv[idx].BorderThickness = new Thickness(1.5);
                }
            }

            RefreshChildThumbnailsUI(idx);
        }

        private void RefreshChildThumbnailsUI(int idx)
        {
            if (idx < 0 || idx >= _secondarySlotCount || idx >= _secondaryImages.Count) return;
            var item = _secondaryImages[idx];

            var panels = new List<StackPanel>();
            if (idx < _slotChildPanels.Count) panels.Add(_slotChildPanels[idx]);
            if (idx < _slotChildPanelsWv.Count) panels.Add(_slotChildPanelsWv[idx]);

            foreach (var panel in panels)
            {
                panel.Children.Clear();
                if (item.SavedChildImages != null && item.SavedChildImages.Count > 0)
                {
                    bool isCompact = (idx < _slotChildPanelsWv.Count && panel == _slotChildPanelsWv[idx]);
                    double size = isCompact ? 18 : 22;

                    foreach (var child in item.SavedChildImages)
                    {
                        var border = new Border
                        {
                            Width = size,
                            Height = size,
                            CornerRadius = new CornerRadius(3),
                            BorderThickness = new Thickness(1.2),
                            Margin = new Thickness(1, 0, 1, 0),
                            Cursor = Cursors.Hand,
                            ToolTip = !string.IsNullOrEmpty(child.ImageId) ? $"ID: #{child.ImageId}" : "Ảnh con"
                        };

                        bool isActive = !string.IsNullOrEmpty(child.ImageId) && string.Equals(child.ImageId, item.ImageId, StringComparison.OrdinalIgnoreCase);
                        border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#4fffb0" : "#2a2e3d"));
                        border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15171e"));

                        var childImg = new Image
                        {
                            Stretch = Stretch.Uniform,
                            Source = child.Bitmap
                        };
                        border.Child = childImg;

                        var capturedChild = child;
                        border.MouseLeftButtonDown += (s, e) =>
                        {
                            e.Handled = true;
                            // Lưu ảnh hiện tại (nếu có ID) vào SavedChildImages
                            item.ArchiveCurrentIfHasId();

                            // Chọn ảnh con này lên ô ảnh phụ to
                            item.Bitmap = capturedChild.Bitmap;
                            item.FilePath = capturedChild.FilePath;
                            item.ResetImageIdAndCodeId();
                            if (!string.IsNullOrEmpty(capturedChild.ImageId))
                            {
                                item.ImageId = capturedChild.ImageId;
                                item.AspectRatioIds[CmbAspectRatio?.SelectedIndex ?? 3] = capturedChild.ImageId;
                            }
                            item.IsSelected = true;

                            RefreshAllSlotsUI();
                            UpdateSecondaryInfo();
                            UpdatePreviewImage();
                        };

                        panel.Children.Add(border);
                    }
                }
            }
        }

        private void UpdateSecondaryInfo()
        {
            if (TxtSecondaryInfo == null) return;
            int total = _secondaryImages.Count(s => s.HasImage);
            int selected = _secondaryImages.Count(s => s.HasImage && s.IsSelected);
            if (total > 0)
            {
                string info = $"Ảnh phụ: {selected}/{total} đã chọn";
                // Ảnh đơn mode: hiển thị tổng số ảnh output dự kiến
                if (!_isCombinedMode && selected > 0)
                {
                    int batchSize = (CmbBatchSize?.SelectedIndex ?? 2) + 1;
                    int totalOutput = selected * batchSize;
                    info += $" · Tổng: {totalOutput} ảnh";
                }
                TxtSecondaryInfo.Text = info;
            }
            else
            {
                TxtSecondaryInfo.Text = "";
            }
        }

        private void RefreshHistoryPanelUI()
        {
            if (HistoryImagesWrapPanel == null) return;
            HistoryImagesWrapPanel.Children.Clear();

            var allHistory = new List<SecondaryImageItem>();

            // 1. Gather from all layers in document
            if (_doc?.Layers != null)
            {
                foreach (var layer in _doc.Layers)
                {
                    if (layer.LayerAiSecondaryImages != null)
                    {
                        foreach (var sec in layer.LayerAiSecondaryImages)
                        {
                            if (sec.SavedChildImages != null)
                            {
                                foreach (var child in sec.SavedChildImages)
                                {
                                    if (!string.IsNullOrEmpty(child.ImageId))
                                    {
                                        allHistory.Add(new SecondaryImageItem
                                        {
                                            ImageId = child.ImageId,
                                            FilePath = child.FilePath,
                                            IsSelected = child.IsSelected,
                                            Bitmap = child.Bitmap
                                        });
                                    }
                                }
                            }
                            if (!string.IsNullOrEmpty(sec.ImageId))
                            {
                                allHistory.Add(new SecondaryImageItem
                                {
                                    ImageId = sec.ImageId,
                                    FilePath = sec.FilePath,
                                    IsSelected = sec.IsSelected,
                                    Bitmap = sec.Bitmap
                                });
                            }
                        }
                    }
                }
            }

            // 2. Gather from current dialog _secondaryImages
            foreach (var slot in _secondaryImages)
            {
                if (slot.SavedChildImages != null)
                {
                    allHistory.AddRange(slot.SavedChildImages.Where(s => !string.IsNullOrEmpty(s.ImageId)));
                }
                if (!string.IsNullOrEmpty(slot.ImageId))
                {
                    allHistory.Add(slot);
                }
            }

            // Remove duplicates by ImageId
            var distinctHistory = allHistory
                .Where(s => !string.IsNullOrEmpty(s.ImageId))
                .GroupBy(s => s.ImageId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            foreach (var child in distinctHistory)
            {
                if (child.Bitmap == null && !string.IsNullOrEmpty(child.FilePath) && File.Exists(child.FilePath))
                {
                    try { child.Bitmap = CreateBitmapFromUrlOrFile(child.FilePath); } catch { }
                }

                if (child.Bitmap == null) continue;

                var border = new Border
                {
                    Width = 60,
                    Height = 60,
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(1.5),
                    Margin = new Thickness(4),
                    Cursor = Cursors.Hand,
                    ToolTip = !string.IsNullOrEmpty(child.ImageId) ? $"ID: #{child.ImageId}\n(Kéo thả vào ô ảnh phụ)" : "Ảnh lịch sử"
                };

                bool isActive = _secondaryImages.Any(s => s.HasImage && string.Equals(s.ImageId, child.ImageId, StringComparison.OrdinalIgnoreCase));
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#00ffff" : "#2a2e3d"));
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15171e"));

                var childImg = new Image
                {
                    Stretch = Stretch.Uniform,
                    Source = child.Bitmap,
                    Margin = new Thickness(2)
                };
                border.Child = childImg;

                var capturedChild = child;
                Point dragStartPoint = new Point();
                bool isMouseDown = false;

                border.MouseLeftButtonDown += (s, e) =>
                {
                    isMouseDown = true;
                    dragStartPoint = e.GetPosition(null);
                    border.CaptureMouse();
                    e.Handled = true;
                };

                border.MouseLeftButtonUp += (s, e) =>
                {
                    if (isMouseDown)
                    {
                        isMouseDown = false;
                        border.ReleaseMouseCapture();
                        
                        // Click behavior
                        e.Handled = true;
                        int targetSlot = GetSelectedSlotIndex();
                        if (targetSlot < 0)
                        {
                            for (int i = 0; i < _secondarySlotCount; i++)
                            {
                                if (!_secondaryImages[i].HasImage)
                                {
                                    targetSlot = i;
                                    break;
                                }
                            }
                        }
                        if (targetSlot >= 0 && targetSlot < _secondaryImages.Count)
                        {
                            var item = _secondaryImages[targetSlot];
                            item.ArchiveCurrentIfHasId();
                            item.Bitmap = capturedChild.Bitmap;
                            item.FilePath = capturedChild.FilePath;
                            item.ResetImageIdAndCodeId();
                            if (!string.IsNullOrEmpty(capturedChild.ImageId))
                            {
                                item.ImageId = capturedChild.ImageId;
                            }
                            if (!string.IsNullOrEmpty(capturedChild.CodeId))
                            {
                                item.CodeId = capturedChild.CodeId;
                            }
                            if (capturedChild.AspectRatioIds != null)
                            {
                                foreach (var kvp in capturedChild.AspectRatioIds)
                                {
                                    item.AspectRatioIds[kvp.Key] = kvp.Value;
                                }
                            }
                            item.IsSelected = true;
                            RefreshAllSlotsUI();
                            UpdatePreviewImage();
                        }
                    }
                };

                border.MouseMove += (s, e) =>
                {
                    if (isMouseDown && e.LeftButton == MouseButtonState.Pressed)
                    {
                        var currentPosition = e.GetPosition(null);
                        if (Math.Abs(currentPosition.X - dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                            Math.Abs(currentPosition.Y - dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                        {
                            isMouseDown = false;
                            border.ReleaseMouseCapture();

                            var data = new DataObject();
                            data.SetData("LayerAiHistoryItem", "dummy");
                            LayerAiDialog.DraggedHistoryItem = capturedChild;
                            try
                            {
                                DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
                            }
                            catch { }
                            finally
                            {
                                LayerAiDialog.DraggedHistoryItem = null;
                            }
                        }
                    }
                };

                HistoryImagesWrapPanel.Children.Add(border);
            }
        }

        #endregion

    }
}
