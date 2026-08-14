// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS & DEVELOPERS:
// DO NOT ALLOW ANY FILE IN THIS COMPONENT TO EXCEED ~1500 LINES OF CODE!
// To maintain readability, ease of testing, and modularity:
// - If a file grows larger than ~1500 lines, you MUST split/separate the logic into a new
//   partial class file (e.g., ImageProcessingNodeContentControl.<FeatureName>.cs).
// - Always place distinct features, tools, or event groupings in their respective files.
// - Ensure comments and documentation remain clean and structured.
// ========================================================================================
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System;
using System.Linq;
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;
using System.Collections.Generic;
using System.IO;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl : UserControl
    {
        private Rect _activeCropRect;
        private Rect _normalizedCropRect = new Rect(0.05, 0.05, 0.9, 0.9);
        private string _activeCropHandle = "";
        private Point _cropDragStartMouse;
        private Rect _cropDragStartRect;
        private bool _isDraggingCrop = false;

        private void CropGroupBtn_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is not Border border) return;

            if (SelectionGroupPopup.IsOpen && SelectionGroupPopup.PlacementTarget == border)
            {
                SelectionGroupPopup.IsOpen = false;
                return;
            }

            if (_activeSelectionGroupBorder != null && _activeSelectionGroupBorder != border)
            {
                _activeSelectionGroupBorder.Background = Brushes.Transparent;
                _activeSelectionGroupBorder.BorderBrush = Brushes.Transparent;
            }

            _activeSelectionGroupBorder = border;

            var list = new List<SelectionToolItem>
            {
                new SelectionToolItem { Name = "CropCanvas", DisplayName = "Crop Tool", Description = "Cắt xén kích thước ảnh (C)", IconKey = "crop duotone" },
                new SelectionToolItem { Name = "Transform", DisplayName = "Transform Tool", Description = "Co dãn, xoay ảnh (Ctrl+T)", IconKey = "expand duotone" },
                new SelectionToolItem { Name = "Slice", DisplayName = "Slice Tool", Description = "Chia cắt ảnh thành các phần (K)", IconKey = "scalpel sharp-duotone-solid" },
                new SelectionToolItem { Name = "SliceSelect", DisplayName = "Slice Select Tool", Description = "Chọn và chỉnh sửa lát cắt (Shift+K)", IconKey = "scalpel-line-dashed sharp-duotone-solid" }
            };

            SelectionPopupItemsControl.ItemsSource = list;

            SelectionGroupPopup.PlacementTarget = border;
            SelectionGroupPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            SelectionGroupPopup.HorizontalOffset = 6;
            SelectionGroupPopup.VerticalOffset = -4;

            border.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));

            if (SelectionGroupPopup.Child is FrameworkElement childBorder)
            {
                childBorder.LayoutTransform = EditorToolbox.LayoutTransform ?? Transform.Identity;
            }
            SelectionGroupPopup.IsOpen = true;
        }

        private void OptCropRatioCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OptCropRatioCombo == null || OptCropCustomInputs == null) return;
            if (OptCropRatioCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _cropRatioMode = tag;
                OptCropCustomInputs.Visibility = (tag == "Custom") ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OptCropCustomRatio_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (OptCropCustomW == null || OptCropCustomH == null) return;
            double.TryParse(OptCropCustomW.Text, out _cropCustomW);
            double.TryParse(OptCropCustomH.Text, out _cropCustomH);
        }

        private void OptCropApply_Click(object sender, RoutedEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null || activeLayer.IsLocked)
            {
                MessageBox.Show("Layer đang bị khóa hoặc không tồn tại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double scaleX = activeLayer.Width / MainImage.ActualWidth;
            double scaleY = activeLayer.Height / MainImage.ActualHeight;

            int cx = (int)(_activeCropRect.X * scaleX);
            int cy = (int)(_activeCropRect.Y * scaleY);
            int cw = (int)(_activeCropRect.Width * scaleX);
            int ch = (int)(_activeCropRect.Height * scaleY);

            if (cw > 4 && ch > 4)
            {
                int w = activeLayer.Width;
                int h = activeLayer.Height;
                int stride = w * 4;
                byte[] oldPixels = new byte[stride * h];
                activeLayer.Bitmap.CopyPixels(oldPixels, stride, 0);

                byte[] newPixels = (byte[])oldPixels.Clone();

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (x < cx || x >= cx + cw || y < cy || y >= cy + ch)
                        {
                            int idx = (y * w + x) * 4;
                            newPixels[idx] = 0;
                            newPixels[idx + 1] = 0;
                            newPixels[idx + 2] = 0;
                            newPixels[idx + 3] = 0;
                        }
                    }
                }

                var cmd = new PixelEditCommand(activeLayer, oldPixels, newPixels);
                _node.EditorDoc.History.Execute(cmd);

                activeLayer.InvalidateThumbnail();
                OnEditorDocumentModified();

                if (CropOverlayCanvas != null) CropOverlayCanvas.Visibility = Visibility.Collapsed;
            }
        }

        private void OptCropCancel_Click(object sender, RoutedEventArgs e)
        {
            if (CropOverlayCanvas != null) CropOverlayCanvas.Visibility = Visibility.Collapsed;
        }

        private void OptSliceClearAll_Click(object sender, RoutedEventArgs e)
        {
            _slices.Clear();
            _selectedSliceIndex = -1;
            UpdateSlicesDisplay();
        }

        private void OptSliceExport_Click(object sender, RoutedEventArgs e)
        {
            if (_slices.Count == 0)
            {
                MessageBox.Show("Không có lát cắt (slices) nào để xuất.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_node.EditorDoc == null) return;

            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Chọn thư mục xuất các ảnh slices",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                string destFolder = dialog.SelectedPath;
                var compositeSource = _node.EditorDoc.Composite();

                try
                {
                    for (int i = 0; i < _slices.Count; i++)
                    {
                        var rect = _slices[i];
                        int x = (int)Math.Clamp(rect.X, 0, compositeSource.PixelWidth - 1);
                        int y = (int)Math.Clamp(rect.Y, 0, compositeSource.PixelHeight - 1);
                        int w = (int)Math.Clamp(rect.Width, 1, compositeSource.PixelWidth - x);
                        int h = (int)Math.Clamp(rect.Height, 1, compositeSource.PixelHeight - y);

                        var cropped = new CroppedBitmap(compositeSource, new Int32Rect(x, y, w, h));

                        string filePath = Path.Combine(destFolder, $"slice_{i + 1:D2}.png");
                        using (var fs = new FileStream(filePath, FileMode.Create))
                        {
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(cropped));
                            encoder.Save(fs);
                        }
                    }

                    MessageBox.Show($"Đã xuất thành công {_slices.Count} ảnh chia cắt vào thư mục:\n{destFolder}", 
                                    "Xuất Slices Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xuất ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateSlicesDisplay()
        {
            if (SlicesCanvas == null || _node.EditorDoc == null) return;
            SlicesCanvas.Children.Clear();

            if (_slices.Count == 0)
            {
                SlicesCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            SlicesCanvas.Visibility = Visibility.Visible;

            double scaleX = MainImage.ActualWidth / _node.EditorDoc.Width;
            double scaleY = MainImage.ActualHeight / _node.EditorDoc.Height;

            for (int i = 0; i < _slices.Count; i++)
            {
                var rect = _slices[i];
                double left = rect.Left * scaleX;
                double top = rect.Top * scaleY;
                double width = rect.Width * scaleX;
                double height = rect.Height * scaleY;

                var border = new Border
                {
                    Width = width,
                    Height = height,
                    BorderThickness = new Thickness(i == _selectedSliceIndex ? 2.0 : 1.0),
                    BorderBrush = i == _selectedSliceIndex ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00cfff")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0")),
                    Background = new SolidColorBrush(Color.FromArgb(25, 0, 207, 255)),
                    Cursor = Cursors.Hand
                };
                Canvas.SetLeft(border, left);
                Canvas.SetTop(border, top);

                var labelBorder = new Border
                {
                    Background = i == _selectedSliceIndex ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00cfff")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12141c")),
                    Padding = new Thickness(4, 1, 4, 1),
                    CornerRadius = new CornerRadius(2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
                var labelText = new TextBlock
                {
                    Text = $"0{i + 1}",
                    Foreground = Brushes.White,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold
                };
                labelBorder.Child = labelText;

                var grid = new Grid();
                grid.Children.Add(labelBorder);
                border.Child = grid;

                SlicesCanvas.Children.Add(border);
            }
        }

        private void HandleSliceSelectMouseDown(Point clickPos, int px, int py)
        {
            double scaleX = _node.EditorDoc.Width / MainImage.ActualWidth;
            double scaleY = _node.EditorDoc.Height / MainImage.ActualHeight;

            if (_selectedSliceIndex >= 0 && _selectedSliceIndex < _slices.Count)
            {
                var rect = _slices[_selectedSliceIndex];
                double handleSize = 6.0;
                
                double left = rect.Left;
                double right = rect.Right;
                double top = rect.Top;
                double bottom = rect.Bottom;

                bool nearLeft = Math.Abs(px - left) <= handleSize;
                bool nearRight = Math.Abs(px - right) <= handleSize;
                bool nearTop = Math.Abs(py - top) <= handleSize;
                bool nearBottom = Math.Abs(py - bottom) <= handleSize;

                if (nearLeft && nearTop) { _sliceResizeHandle = "TL"; }
                else if (nearRight && nearTop) { _sliceResizeHandle = "TR"; }
                else if (nearLeft && nearBottom) { _sliceResizeHandle = "BL"; }
                else if (nearRight && nearBottom) { _sliceResizeHandle = "BR"; }
                else if (nearLeft) { _sliceResizeHandle = "L"; }
                else if (nearRight) { _sliceResizeHandle = "R"; }
                else if (nearTop) { _sliceResizeHandle = "T"; }
                else if (nearBottom) { _sliceResizeHandle = "B"; }
                else if (px >= left && px <= right && py >= top && py <= bottom)
                {
                    _sliceResizeHandle = "Move";
                }
                else
                {
                    _sliceResizeHandle = "";
                }

                if (_sliceResizeHandle != "")
                {
                    _sliceDragStartRect = rect;
                    _isSelecting = true;
                    _selectionStartPoint = clickPos;
                    MainScrollViewer.CaptureMouse();
                    return;
                }
            }

            _selectedSliceIndex = -1;
            for (int i = _slices.Count - 1; i >= 0; i--)
            {
                var rect = _slices[i];
                if (px >= rect.Left && px <= rect.Right && py >= rect.Top && py <= rect.Bottom)
                {
                    _selectedSliceIndex = i;
                    _sliceResizeHandle = "Move";
                    _sliceDragStartRect = rect;
                    _isSelecting = true;
                    _selectionStartPoint = clickPos;
                    UpdateSlicesDisplay();
                    MainScrollViewer.CaptureMouse();
                    return;
                }
            }

            UpdateSlicesDisplay();
        }

        private void HandleSliceSelectMouseMove(Point mousePos, int px, int py)
        {
            if (!_isSelecting || _selectedSliceIndex < 0 || _selectedSliceIndex >= _slices.Count || _node.EditorDoc == null) return;

            var startRect = _sliceDragStartRect;
            double scaleX = (double)_node.EditorDoc.Width / MainImage.ActualWidth;
            double scaleY = (double)_node.EditorDoc.Height / MainImage.ActualHeight;

            double dx = (mousePos.X - _selectionStartPoint.X) * scaleX;
            double dy = (mousePos.Y - _selectionStartPoint.Y) * scaleY;

            var rect = startRect;

            if (_sliceResizeHandle == "Move")
            {
                double newL = Math.Clamp(startRect.Left + dx, 0, _node.EditorDoc.Width - startRect.Width);
                double newT = Math.Clamp(startRect.Top + dy, 0, _node.EditorDoc.Height - startRect.Height);
                rect = new Rect(newL, newT, startRect.Width, startRect.Height);
            }
            else
            {
                double left = startRect.Left;
                double top = startRect.Top;
                double right = startRect.Right;
                double bottom = startRect.Bottom;

                if (_sliceResizeHandle.Contains("L")) left = Math.Min(startRect.Right - 5, Math.Max(0, startRect.Left + dx));
                if (_sliceResizeHandle.Contains("R")) right = Math.Max(startRect.Left + 5, Math.Min(_node.EditorDoc.Width, startRect.Right + dx));
                if (_sliceResizeHandle.Contains("T")) top = Math.Min(startRect.Bottom - 5, Math.Max(0, startRect.Top + dy));
                if (_sliceResizeHandle.Contains("B")) bottom = Math.Max(startRect.Top + 5, Math.Min(_node.EditorDoc.Height, startRect.Bottom + dy));

                rect = new Rect(left, top, right - left, bottom - top);
            }

            _slices[_selectedSliceIndex] = rect;
            UpdateSlicesDisplay();
        }

        public void StartCropMode()
        {
            if (MainImage.ActualWidth == 0 || MainImage.ActualHeight == 0 || _node.EditorDoc == null) return;

            _normalizedCropRect = new Rect(0, 0, 1, 1);

            double docW = _node.EditorDoc.Width;
            double docH = _node.EditorDoc.Height;

            if (docW > 0 && docH > 0)
            {
                if (_activeSelectionGeometry != null && !_activeSelectionGeometry.IsEmpty())
                {
                    var docBounds = _activeSelectionGeometry.Bounds;
                    double normX = Math.Clamp(docBounds.X / docW, 0, 1);
                    double normY = Math.Clamp(docBounds.Y / docH, 0, 1);
                    double normW = Math.Clamp(docBounds.Width / docW, 0, 1 - normX);
                    double normH = Math.Clamp(docBounds.Height / docH, 0, 1 - normY);
                    _normalizedCropRect = new Rect(normX, normY, normW, normH);
                }
                else
                {
                    var activeLayer = _node.EditorDoc.ActiveLayer;
                    if (activeLayer != null)
                    {
                        Rect layerBounds = activeLayer.ContentBounds;
                        if (layerBounds.IsEmpty || layerBounds.Width <= 0 || layerBounds.Height <= 0)
                        {
                            layerBounds = GetLayerContentBounds(activeLayer.Bitmap);
                        }

                        if (!layerBounds.IsEmpty && layerBounds.Width > 0 && layerBounds.Height > 0)
                        {
                            double normX = Math.Clamp(layerBounds.X / docW, 0, 1);
                            double normY = Math.Clamp(layerBounds.Y / docH, 0, 1);
                            double normW = Math.Clamp(layerBounds.Width / docW, 0, 1 - normX);
                            double normH = Math.Clamp(layerBounds.Height / docH, 0, 1 - normY);
                            _normalizedCropRect = new Rect(normX, normY, normW, normH);
                        }
                    }
                }
            }
            
            if (CropOverlayCanvas != null)
            {
                CropOverlayCanvas.Visibility = Visibility.Visible;
                UpdateCropOverlayDisplay();
            }
        }

        private void UpdateCropOverlayDisplay()
        {
            if (CropOverlayCanvas == null || MainImage == null) return;

            double width = MainImage.ActualWidth;
            double height = MainImage.ActualHeight;
            if (width <= 0 || height <= 0) return;

            _activeCropRect = new Rect(
                _normalizedCropRect.X * width,
                _normalizedCropRect.Y * height,
                _normalizedCropRect.Width * width,
                _normalizedCropRect.Height * height
            );

            var outerRect = new RectangleGeometry(new Rect(0, 0, width, height));
            var innerRect = new RectangleGeometry(_activeCropRect);
            CropShieldPath.Data = Geometry.Combine(outerRect, innerRect, GeometryCombineMode.Exclude, null);

            CropBoxBorderPath.Data = innerRect;

            var gridGeom = new GeometryGroup();
            double w3 = _activeCropRect.Width / 3.0;
            double h3 = _activeCropRect.Height / 3.0;

            gridGeom.Children.Add(new LineGeometry(
                new Point(_activeCropRect.Left + w3, _activeCropRect.Top),
                new Point(_activeCropRect.Left + w3, _activeCropRect.Bottom)
            ));
            gridGeom.Children.Add(new LineGeometry(
                new Point(_activeCropRect.Left + 2 * w3, _activeCropRect.Top),
                new Point(_activeCropRect.Left + 2 * w3, _activeCropRect.Bottom)
            ));

            gridGeom.Children.Add(new LineGeometry(
                new Point(_activeCropRect.Left, _activeCropRect.Top + h3),
                new Point(_activeCropRect.Right, _activeCropRect.Top + h3)
            ));
            gridGeom.Children.Add(new LineGeometry(
                new Point(_activeCropRect.Left, _activeCropRect.Top + 2 * h3),
                new Point(_activeCropRect.Right, _activeCropRect.Top + 2 * h3)
            ));

            CropGridLinesPath.Data = gridGeom;
            CropGridLinesPath.Visibility = Visibility.Visible;

            double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
            if (zoom <= 0) zoom = 1.0;

            CropBoxBorderPath.StrokeThickness = 2.5 / zoom;
            CropGridLinesPath.StrokeThickness = 1.8 / zoom;

            double handleSize = 12.0 / zoom;
            double half = handleSize / 2.0;
            double strokeThickness = 1.5 / zoom;

            foreach (var child in CropHandlesCanvas.Children)
            {
                if (child is System.Windows.Shapes.Rectangle rect)
                {
                    rect.Width = handleSize;
                    rect.Height = handleSize;
                    rect.StrokeThickness = strokeThickness;
                }
            }

            if (Handle_TL != null) { Canvas.SetLeft(Handle_TL, _activeCropRect.Left - half); Canvas.SetTop(Handle_TL, _activeCropRect.Top - half); }
            if (Handle_T != null)  { Canvas.SetLeft(Handle_T, _activeCropRect.Left + _activeCropRect.Width / 2 - half); Canvas.SetTop(Handle_T, _activeCropRect.Top - half); }
            if (Handle_TR != null) { Canvas.SetLeft(Handle_TR, _activeCropRect.Right - half); Canvas.SetTop(Handle_TR, _activeCropRect.Top - half); }
            if (Handle_R != null)  { Canvas.SetLeft(Handle_R, _activeCropRect.Right - half); Canvas.SetTop(Handle_R, _activeCropRect.Top + _activeCropRect.Height / 2 - half); }
            if (Handle_BR != null) { Canvas.SetLeft(Handle_BR, _activeCropRect.Right - half); Canvas.SetTop(Handle_BR, _activeCropRect.Bottom - half); }
            if (Handle_B != null)  { Canvas.SetLeft(Handle_B, _activeCropRect.Left + _activeCropRect.Width / 2 - half); Canvas.SetTop(Handle_B, _activeCropRect.Bottom - half); }
            if (Handle_BL != null) { Canvas.SetLeft(Handle_BL, _activeCropRect.Left - half); Canvas.SetTop(Handle_BL, _activeCropRect.Bottom - half); }
            if (Handle_L != null)  { Canvas.SetLeft(Handle_L, _activeCropRect.Left - half); Canvas.SetTop(Handle_L, _activeCropRect.Top + _activeCropRect.Height / 2 - half); }
        }

        private void CropOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var clickPos = e.GetPosition(CropOverlayCanvas);
            
            double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
            if (zoom <= 0) zoom = 1.0;

            double threshold = 16.0 / zoom;

            Point[] handlePoints = new Point[8]
            {
                new Point(_activeCropRect.Left, _activeCropRect.Top),
                new Point(_activeCropRect.Left + _activeCropRect.Width / 2, _activeCropRect.Top),
                new Point(_activeCropRect.Right, _activeCropRect.Top),
                new Point(_activeCropRect.Right, _activeCropRect.Top + _activeCropRect.Height / 2),
                new Point(_activeCropRect.Right, _activeCropRect.Bottom),
                new Point(_activeCropRect.Left + _activeCropRect.Width / 2, _activeCropRect.Bottom),
                new Point(_activeCropRect.Left, _activeCropRect.Bottom),
                new Point(_activeCropRect.Left, _activeCropRect.Top + _activeCropRect.Height / 2)
            };

            string[] handleNames = { "TL", "T", "TR", "R", "BR", "B", "BL", "L" };

            _activeCropHandle = "";
            double minDistance = double.MaxValue;
            int bestHandleIdx = -1;

            for (int i = 0; i < 8; i++)
            {
                double dx = clickPos.X - handlePoints[i].X;
                double dy = clickPos.Y - handlePoints[i].Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < threshold && dist < minDistance)
                {
                    minDistance = dist;
                    bestHandleIdx = i;
                }
            }

            if (bestHandleIdx != -1)
            {
                _activeCropHandle = handleNames[bestHandleIdx];
            }
            else
            {
                double marginW = _activeCropRect.Width * 0.2;
                double marginH = _activeCropRect.Height * 0.2;
                var centerRect = new Rect(
                    _activeCropRect.X + marginW,
                    _activeCropRect.Y + marginH,
                    Math.Max(1, _activeCropRect.Width - 2 * marginW),
                    Math.Max(1, _activeCropRect.Height - 2 * marginH)
                );

                if (centerRect.Contains(clickPos))
                {
                    _activeCropHandle = "Move";
                }
            }

            if (_activeCropHandle != "")
            {
                _isDraggingCrop = true;
                _cropDragStartMouse = clickPos;
                _cropDragStartRect = _activeCropRect;
                CropOverlayCanvas.CaptureMouse();
                UpdateCropOverlayDisplay();
                e.Handled = true;
            }
        }

        private void CropOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingCrop) return;

            var currentMouse = e.GetPosition(CropOverlayCanvas);
            double dx = currentMouse.X - _cropDragStartMouse.X;
            double dy = currentMouse.Y - _cropDragStartMouse.Y;

            double left = _cropDragStartRect.Left;
            double top = _cropDragStartRect.Top;
            double right = _cropDragStartRect.Right;
            double bottom = _cropDragStartRect.Bottom;

            if (_activeCropHandle == "Move")
            {
                left = Math.Clamp(_cropDragStartRect.Left + dx, 0, MainImage.ActualWidth - _cropDragStartRect.Width);
                top = Math.Clamp(_cropDragStartRect.Top + dy, 0, MainImage.ActualHeight - _cropDragStartRect.Height);
                right = left + _cropDragStartRect.Width;
                bottom = top + _cropDragStartRect.Height;
            }
            else
            {
                if (_activeCropHandle.Contains("L"))
                {
                    left = _cropDragStartRect.Left + dx;
                    left = Math.Clamp(left, 0, right - 10);
                }
                if (_activeCropHandle.Contains("R"))
                {
                    right = _cropDragStartRect.Right + dx;
                    right = Math.Clamp(right, left + 10, MainImage.ActualWidth);
                }
                if (_activeCropHandle.Contains("T"))
                {
                    top = _cropDragStartRect.Top + dy;
                    top = Math.Clamp(top, 0, bottom - 10);
                }
                if (_activeCropHandle.Contains("B"))
                {
                    bottom = _cropDragStartRect.Bottom + dy;
                    bottom = Math.Clamp(bottom, top + 10, MainImage.ActualHeight);
                }

                double w = right - left;
                double h = bottom - top;
                double targetRatio = 1.0;
                bool hasRatio = false;

                if (_cropRatioMode == "1:1") { targetRatio = 1.0; hasRatio = true; }
                else if (_cropRatioMode == "16:9") { targetRatio = 16.0 / 9.0; hasRatio = true; }
                else if (_cropRatioMode == "4:3") { targetRatio = 4.0 / 3.0; hasRatio = true; }
                else if (_cropRatioMode == "Custom")
                {
                    if (_cropCustomW > 0 && _cropCustomH > 0)
                    {
                        targetRatio = _cropCustomW / _cropCustomH;
                        hasRatio = true;
                    }
                }

                if (hasRatio)
                {
                    if (_activeCropHandle == "L" || _activeCropHandle == "R")
                    {
                        h = w / targetRatio;
                        bottom = top + h;
                        if (bottom > MainImage.ActualHeight)
                        {
                            bottom = MainImage.ActualHeight;
                            h = bottom - top;
                            w = h * targetRatio;
                            if (_activeCropHandle == "L") left = right - w;
                            else right = left + w;
                        }
                    }
                    else if (_activeCropHandle == "T" || _activeCropHandle == "B")
                    {
                        w = h * targetRatio;
                        right = left + w;
                        if (right > MainImage.ActualWidth)
                        {
                            right = MainImage.ActualWidth;
                            w = right - left;
                            h = w / targetRatio;
                            if (_activeCropHandle == "T") top = bottom - h;
                            else bottom = top + h;
                        }
                    }
                    else
                    {
                        w = h * targetRatio;
                        if (w > MainImage.ActualWidth - left)
                        {
                            w = MainImage.ActualWidth - left;
                            h = w / targetRatio;
                        }
                        if (_activeCropHandle.Contains("L")) left = right - w;
                        else right = left + w;
                        if (_activeCropHandle.Contains("T")) top = bottom - h;
                        else bottom = top + h;
                    }
                }
            }

            _activeCropRect = new Rect(left, top, right - left, bottom - top);
            
            _normalizedCropRect = new Rect(
                _activeCropRect.X / MainImage.ActualWidth,
                _activeCropRect.Y / MainImage.ActualHeight,
                _activeCropRect.Width / MainImage.ActualWidth,
                _activeCropRect.Height / MainImage.ActualHeight
            );

            UpdateCropOverlayDisplay();
            e.Handled = true;
        }

        private void CropOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingCrop)
            {
                _isDraggingCrop = false;
                CropOverlayCanvas.ReleaseMouseCapture();
                UpdateCropOverlayDisplay();
                e.Handled = true;
            }
        }
    }
}
