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
using System.Collections.Generic;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl : UserControl
    {
        #region PHOTOSHOP STYLE SELECTION TOOLS (Marquee, Lasso, Polygonal Lasso)

        private byte[]? _selectionClipboardPixels;
        private Rect? _selectionClipboardRect;
        private bool _selectionClipboardIsFullLayer;
        private EditorLayer? _selectionClipboardLayerSource;
        private int _magicWandTolerance = 25;
        private bool[,]? _quickSelectionVisited;
        private byte[]? _quickSelectionPixels;
        private byte _quickSelTargetR, _quickSelTargetG, _quickSelTargetB, _quickSelTargetA;

        public class SelectionToolItem
        {
            public string Name { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string IconKey { get; set; } = string.Empty;
            public string ToolTipText => $"{DisplayName} ({Description})";
        }

        private Border? _activeSelectionGroupBorder;

        private void SelectionGroupBtn_LeftClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string toolName)
            {
                EditorPanel.SelectToolByName(toolName);
                SyncToolboxHighlight();
                e.Handled = true;
            }
        }

        private void SelectionGroupBtn_RightClick(object sender, MouseButtonEventArgs e)
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
                new SelectionToolItem { Name = "Selection", DisplayName = "Rectangular Marquee", Description = "Tạo vùng chọn hình chữ nhật (M)", IconKey = "square-dashed-circle-plus sharp-duotone-solid" },
                new SelectionToolItem { Name = "Lasso", DisplayName = "Lasso Tool", Description = "Vẽ tự do để tạo vùng chọn (L)", IconKey = "pen-swirl duotone" },
                new SelectionToolItem { Name = "PolyLasso", DisplayName = "Polygonal Lasso", Description = "Chấm các điểm để tạo vùng chọn đa giác (Y)", IconKey = "share-nodes jelly-regular" }
            };

            SelectionPopupItemsControl.ItemsSource = list;

            SelectionGroupPopup.PlacementTarget = border;
            SelectionGroupPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            SelectionGroupPopup.HorizontalOffset = 6;
            SelectionGroupPopup.VerticalOffset = -4;

            border.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));

            SelectionGroupPopup.IsOpen = true;
        }

        private void SelectionGroupPopup_Closed(object? sender, EventArgs e)
        {
            if (_activeSelectionGroupBorder != null)
            {
                _activeSelectionGroupBorder = null;
                SyncToolboxHighlight();
            }
        }

        private void SelectionPopupItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is SelectionToolItem selectedItem)
            {
                if (_activeSelectionGroupBorder != null)
                {
                    _activeSelectionGroupBorder.Tag = selectedItem.Name;
                    _activeSelectionGroupBorder.ToolTip = selectedItem.ToolTipText;

                    if (_activeSelectionGroupBorder.Child is Grid grid)
                    {
                        var svg = grid.Children[0] as SvgViewboxEx;
                        if (svg != null)
                        {
                            var converter = new IconKeyToPathConverter();
                            svg.Source = (Uri)converter.Convert(null, typeof(Uri), selectedItem.IconKey, null);
                        }
                    }
                }

                EditorPanel.SelectToolByName(selectedItem.Name);
                SyncToolboxHighlight();

                SelectionGroupPopup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void CopyActiveSelection()
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null || !activeLayer.IsVisible) return;

            if (_activeSelectionGeometry != null && _hasCachedSelectionMask && _cachedSelectionMask != null)
            {
                int startX = _cachedSelectionStartX;
                int endX = _cachedSelectionEndX;
                int startY = _cachedSelectionStartY;
                int endY = _cachedSelectionEndY;

                int w = endX - startX + 1;
                int h = endY - startY + 1;
                if (w <= 0 || h <= 0) return;

                var rect = new Int32Rect(startX, startY, w, h);
                int stride = w * 4;
                _selectionClipboardPixels = new byte[stride * h];
                activeLayer.Bitmap.CopyPixels(rect, _selectionClipboardPixels, stride, 0);

                // Clear pixels outside the polygon using the cached mask
                for (int dy = 0; dy < h; dy++)
                {
                    for (int dx = 0; dx < w; dx++)
                    {
                        if (!_cachedSelectionMask[dx, dy])
                        {
                            _selectionClipboardPixels[(dy * w + dx) * 4 + 3] = 0; // Alpha = 0 (Transparent)
                        }
                    }
                }

                _selectionClipboardRect = new Rect(startX, startY, w, h);
                _selectionClipboardIsFullLayer = false;
                _selectionClipboardLayerSource = null;
            }
            else
            {
                _selectionClipboardPixels = null;
                _selectionClipboardRect = null;
                _selectionClipboardIsFullLayer = true;
                _selectionClipboardLayerSource = activeLayer.Duplicate();
            }
        }

        private void PasteSelectionAsLayer()
        {
            if (_node.EditorDoc == null) return;

            EditorLayer newLayer;
            if (_selectionClipboardIsFullLayer && _selectionClipboardLayerSource != null)
            {
                newLayer = _selectionClipboardLayerSource.Duplicate();
                newLayer.Name = EditorPanel.GenerateCopyName(_selectionClipboardLayerSource.Name);
            }
            else if (_selectionClipboardPixels != null && _selectionClipboardRect.HasValue)
            {
                int docW = _node.EditorDoc.Width;
                int docH = _node.EditorDoc.Height;

                newLayer = new EditorLayer(docW, docH, $"layer {_node.EditorDoc.Layers.Count}");
                newLayer.Clear();

                int startX = (int)_selectionClipboardRect.Value.Left;
                int startY = (int)_selectionClipboardRect.Value.Top;
                int w = (int)_selectionClipboardRect.Value.Width;
                int h = (int)_selectionClipboardRect.Value.Height;

                int stride = w * 4;
                newLayer.Bitmap.WritePixels(new Int32Rect(startX, startY, w, h), _selectionClipboardPixels, stride, 0);
                newLayer.InvalidateThumbnail();
            }
            else
            {
                return;
            }

            int insertIndex = _node.EditorDoc.Layers.Count;
            if (_node.EditorDoc.ActiveLayer != null)
            {
                int idx = _node.EditorDoc.Layers.IndexOf(_node.EditorDoc.ActiveLayer);
                if (idx >= 0) insertIndex = idx + 1;
            }

            var cmd = new Models.ImageEditor.Commands.LayerAddCommand(_node.EditorDoc, newLayer, insertIndex);
            _node.EditorDoc.History.Execute(cmd);
            _node.EditorDoc.ActiveLayer = newLayer;

            EditorPanel.RefreshLayersList();
            OnEditorDocumentModified();
        }

        private void DeleteSelectionContent()
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null || activeLayer.IsLocked || !activeLayer.IsVisible) return;

            if (_activeSelectionGeometry != null && _hasCachedSelectionMask && _cachedSelectionMask != null)
            {
                int startX = _cachedSelectionStartX;
                int endX = _cachedSelectionEndX;
                int startY = _cachedSelectionStartY;
                int endY = _cachedSelectionEndY;

                int stride = activeLayer.Width * 4;
                var oldPixels = new byte[stride * activeLayer.Height];
                activeLayer.Bitmap.CopyPixels(oldPixels, stride, 0);

                var newPixels = new byte[stride * activeLayer.Height];
                Array.Copy(oldPixels, newPixels, oldPixels.Length);

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * activeLayer.Width * 4;
                    for (int x = startX; x <= endX; x++)
                    {
                        if (_cachedSelectionMask[x - startX, y - startY])
                        {
                            newPixels[rowOffset + x * 4 + 3] = 0;
                        }
                    }
                }

                var cmd = new PixelEditCommand(activeLayer, oldPixels, newPixels);
                _node.EditorDoc.History.Execute(cmd);

                activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, activeLayer.Width, activeLayer.Height), newPixels, stride, 0);
                activeLayer.InvalidateThumbnail();
                OnEditorDocumentModified();
            }
        }

        private void SelMode_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string modeStr)
            {
                if (Enum.TryParse<SelectionMode>(modeStr, out var mode))
                {
                    _currentSelectionMode = mode;
                    UpdateSelModeVisuals();
                }
            }
            e.Handled = true;
        }

        private void UpdateSelModeVisuals()
        {
            if (BtnSelModeNew == null || BtnSelModeAdd == null || BtnSelModeSubtract == null) return;

            var activeBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00cfff"));
            var activeBg = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0xcf, 0xff));
            var inactiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#353945"));
            var inactiveBg = Brushes.Transparent;

            BtnSelModeNew.BorderBrush = (_currentSelectionMode == SelectionMode.New) ? activeBorder : inactiveBorder;
            BtnSelModeNew.Background = (_currentSelectionMode == SelectionMode.New) ? activeBg : inactiveBg;

            BtnSelModeAdd.BorderBrush = (_currentSelectionMode == SelectionMode.Add) ? activeBorder : inactiveBorder;
            BtnSelModeAdd.Background = (_currentSelectionMode == SelectionMode.Add) ? activeBg : inactiveBg;

            BtnSelModeSubtract.BorderBrush = (_currentSelectionMode == SelectionMode.Subtract) ? activeBorder : inactiveBorder;
            BtnSelModeSubtract.Background = (_currentSelectionMode == SelectionMode.Subtract) ? activeBg : inactiveBg;
        }

        private void SmartSelectionGroupBtn_RightClick(object sender, MouseButtonEventArgs e)
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
                new SelectionToolItem { Name = "MagicWand", DisplayName = "Magic Wand Tool", Description = "Chọn vùng có màu sắc tương tự (W)", IconKey = "wand-magic-sparkles sharp-duotone-solid" },
                new SelectionToolItem { Name = "QuickSelection", DisplayName = "Quick Selection Tool", Description = "Vẽ cọ để chọn vùng tự động (Q)", IconKey = "paintbrush sharp-duotone-solid" },
                new SelectionToolItem { Name = "ObjectSelection", DisplayName = "Object Selection Tool", Description = "Kéo khung để tự động chọn đối tượng (O)", IconKey = "object-group sharp-duotone-solid" }
            };

            SelectionPopupItemsControl.ItemsSource = list;

            SelectionGroupPopup.PlacementTarget = border;
            SelectionGroupPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            SelectionGroupPopup.HorizontalOffset = 6;
            SelectionGroupPopup.VerticalOffset = -4;

            border.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));

            SelectionGroupPopup.IsOpen = true;
        }

        private void RunMagicWandSelection(int startX, int startY)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            int w = activeLayer.Width;
            int h = activeLayer.Height;
            int stride = w * 4;
            byte[] pixels = new byte[stride * h];
            activeLayer.Bitmap.CopyPixels(pixels, stride, 0);

            int startOffset = (startY * w + startX) * 4;
            byte targetB = pixels[startOffset];
            byte targetG = pixels[startOffset + 1];
            byte targetR = pixels[startOffset + 2];
            byte targetA = pixels[startOffset + 3];

            bool[,] visited = new bool[w, h];
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(new Point(startX, startY));
            visited[startX, startY] = true;

            double tolerance = _magicWandTolerance;

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                int cx = (int)p.X;
                int cy = (int)p.Y;

                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i];
                    int ny = cy + dy[i];

                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && !visited[nx, ny])
                    {
                        int offset = (ny * w + nx) * 4;
                        byte b = pixels[offset];
                        byte g = pixels[offset + 1];
                        byte r = pixels[offset + 2];
                        byte a = pixels[offset + 3];

                        int db = b - targetB;
                        int dg = g - targetG;
                        int dr = r - targetR;
                        int da = a - targetA;
                        double dist = Math.Sqrt(db * db + dg * dg + dr * dr + da * da);

                        if (dist <= tolerance)
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue(new Point(nx, ny));
                        }
                    }
                }
            }

            var group = new GeometryGroup();
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    if (visited[x, y])
                    {
                        int runStart = x;
                        while (x < w && visited[x, y])
                        {
                            x++;
                        }
                        group.Children.Add(new RectangleGeometry(new Rect(runStart, y, x - runStart, 1)));
                    }
                    else
                    {
                        x++;
                    }
                }
            }

            ApplyNewGeometry(group);
        }

        private void StartQuickSelection(int px, int py, double radius)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            int w = activeLayer.Width;
            int h = activeLayer.Height;
            int stride = w * 4;
            _quickSelectionPixels = new byte[stride * h];
            activeLayer.Bitmap.CopyPixels(_quickSelectionPixels, stride, 0);

            int startOffset = (py * w + px) * 4;
            _quickSelTargetB = _quickSelectionPixels[startOffset];
            _quickSelTargetG = _quickSelectionPixels[startOffset + 1];
            _quickSelTargetR = _quickSelectionPixels[startOffset + 2];
            _quickSelTargetA = _quickSelectionPixels[startOffset + 3];

            _quickSelectionVisited = new bool[w, h];
            
            _isSelecting = true;
            _selectionPoints.Clear();

            GrowQuickSelection(px, py, radius);
        }

        private void GrowQuickSelection(int px, int py, double radius)
        {
            if (_quickSelectionVisited == null || _quickSelectionPixels == null || _node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            int w = activeLayer.Width;
            int h = activeLayer.Height;
            double tolerance = _magicWandTolerance;

            Queue<Point> queue = new Queue<Point>();

            int rInt = (int)Math.Ceiling(radius);
            for (int dy = -rInt; dy <= rInt; dy++)
            {
                for (int dx = -rInt; dx <= rInt; dx++)
                {
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        int nx = px + dx;
                        int ny = py + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                        {
                            if (!_quickSelectionVisited[nx, ny])
                            {
                                _quickSelectionVisited[nx, ny] = true;
                                queue.Enqueue(new Point(nx, ny));
                            }
                        }
                    }
                }
            }

            int[] dirX = { 0, 0, 1, -1 };
            int[] dirY = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                int cx = (int)p.X;
                int cy = (int)p.Y;

                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dirX[i];
                    int ny = cy + dirY[i];

                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && !_quickSelectionVisited[nx, ny])
                    {
                        int offset = (ny * w + nx) * 4;
                        byte b = _quickSelectionPixels[offset];
                        byte g = _quickSelectionPixels[offset + 1];
                        byte r = _quickSelectionPixels[offset + 2];
                        byte a = _quickSelectionPixels[offset + 3];

                        int db = b - _quickSelTargetB;
                        int dg = g - _quickSelTargetG;
                        int dr = r - _quickSelTargetR;
                        int da = a - _quickSelTargetA;
                        double dist = Math.Sqrt(db * db + dg * dg + dr * dr + da * da);

                        if (dist <= tolerance)
                        {
                            _quickSelectionVisited[nx, ny] = true;
                            queue.Enqueue(new Point(nx, ny));
                        }
                    }
                }
            }

            var group = new GeometryGroup();
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    if (_quickSelectionVisited[x, y])
                    {
                        int runStart = x;
                        while (x < w && _quickSelectionVisited[x, y])
                        {
                            x++;
                        }
                        group.Children.Add(new RectangleGeometry(new Rect(runStart, y, x - runStart, 1)));
                    }
                    else
                    {
                        x++;
                    }
                }
            }

            Geometry combined;
            if (_activeSelectionGeometry == null || _currentSelectionMode == SelectionMode.New)
            {
                combined = group;
            }
            else if (_currentSelectionMode == SelectionMode.Add)
            {
                combined = Geometry.Combine(_activeSelectionGeometry, group, GeometryCombineMode.Union, null);
            }
            else
            {
                combined = Geometry.Combine(_activeSelectionGeometry, group, GeometryCombineMode.Exclude, null);
            }

            double scaleX = MainImage.ActualWidth / w;
            double scaleY = MainImage.ActualHeight / h;
            var scaledGeom = combined.Clone();
            scaledGeom.Transform = new ScaleTransform(scaleX, scaleY);

            if (SelectionPreviewPolygon != null)
            {
                SelectionPreviewPolygon.Data = scaledGeom.GetOutlinedPathGeometry();
                SelectionPreviewPolygon.Visibility = Visibility.Visible;
            }
        }

        private void CommitQuickSelection()
        {
            if (_quickSelectionVisited == null) return;
            int w = _quickSelectionVisited.GetLength(0);
            int h = _quickSelectionVisited.GetLength(1);

            var group = new GeometryGroup();
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    if (_quickSelectionVisited[x, y])
                    {
                        int runStart = x;
                        while (x < w && _quickSelectionVisited[x, y])
                        {
                            x++;
                        }
                        group.Children.Add(new RectangleGeometry(new Rect(runStart, y, x - runStart, 1)));
                    }
                    else
                    {
                        x++;
                    }
                }
            }

            ApplyNewGeometry(group);
            _quickSelectionVisited = null;
            _quickSelectionPixels = null;
        }

        private void RunObjectSelection(int x1, int y1, int x2, int y2)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            int w = activeLayer.Width;
            int h = activeLayer.Height;
            int stride = w * 4;
            byte[] pixels = new byte[stride * h];
            activeLayer.Bitmap.CopyPixels(pixels, stride, 0);

            List<(byte R, byte G, byte B, byte A)> boundaryColors = new();
            for (int x = x1; x <= x2; x++)
            {
                boundaryColors.Add(GetPixelColor(pixels, x, y1, w));
                boundaryColors.Add(GetPixelColor(pixels, x, y2, w));
            }
            for (int y = y1 + 1; y < y2; y++)
            {
                boundaryColors.Add(GetPixelColor(pixels, x1, y, w));
                boundaryColors.Add(GetPixelColor(pixels, x2, y, w));
            }

            List<(byte R, byte G, byte B, byte A)> sampledBoundary = new();
            for (int i = 0; i < boundaryColors.Count; i += 5)
            {
                sampledBoundary.Add(boundaryColors[i]);
            }

            bool[,] isForeground = new bool[w, h];
            double tolerance = 35.0;

            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    var color = GetPixelColor(pixels, x, y, w);
                    
                    double minDist = double.MaxValue;
                    foreach (var bc in sampledBoundary)
                    {
                        int dr = color.R - bc.R;
                        int dg = color.G - bc.G;
                        int db = color.B - bc.B;
                        int da = color.A - bc.A;
                        double dist = Math.Sqrt(dr * dr + dg * dg + db * db + da * da);
                        if (dist < minDist)
                        {
                            minDist = dist;
                        }
                    }

                    if (minDist > tolerance)
                    {
                        isForeground[x, y] = true;
                    }
                }
            }

            bool[,] visited = new bool[w, h];
            List<List<Point>> components = new();

            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    if (isForeground[x, y] && !visited[x, y])
                    {
                        List<Point> comp = new();
                        Queue<Point> queue = new Queue<Point>();
                        queue.Enqueue(new Point(x, y));
                        visited[x, y] = true;

                        int[] dx = { 0, 0, 1, -1 };
                        int[] dy = { 1, -1, 0, 0 };

                        while (queue.Count > 0)
                        {
                            Point p = queue.Dequeue();
                            comp.Add(p);
                            int cx = (int)p.X;
                            int cy = (int)p.Y;

                            for (int i = 0; i < 4; i++)
                            {
                                int nx = cx + dx[i];
                                int ny = cy + dy[i];

                                if (nx >= x1 && nx <= x2 && ny >= y1 && ny <= y2)
                                {
                                    if (isForeground[nx, ny] && !visited[nx, ny])
                                    {
                                        visited[nx, ny] = true;
                                        queue.Enqueue(new Point(nx, ny));
                                    }
                                }
                            }
                        }
                        components.Add(comp);
                    }
                }
            }

            List<Point>? largestComponent = null;
            int maxPoints = 0;
            foreach (var comp in components)
            {
                if (comp.Count > maxPoints)
                {
                    maxPoints = comp.Count;
                    largestComponent = comp;
                }
            }

            if (largestComponent != null)
            {
                bool[,] selectionMask = new bool[w, h];
                foreach (var p in largestComponent)
                {
                    selectionMask[(int)p.X, (int)p.Y] = true;
                }

                var group = new GeometryGroup();
                for (int y = 0; y < h; y++)
                {
                    int x = 0;
                    while (x < w)
                    {
                        if (selectionMask[x, y])
                        {
                            int runStart = x;
                            while (x < w && selectionMask[x, y])
                            {
                                x++;
                            }
                            group.Children.Add(new RectangleGeometry(new Rect(runStart, y, x - runStart, 1)));
                        }
                        else
                        {
                            x++;
                        }
                    }
                }

                ApplyNewGeometry(group);
            }
        }

        private (byte R, byte G, byte B, byte A) GetPixelColor(byte[] pixels, int x, int y, int width)
        {
            int offset = (y * width + x) * 4;
            return (pixels[offset + 2], pixels[offset + 1], pixels[offset], pixels[offset + 3]);
        }

        #endregion
    }
}
