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
        #region PHOTOSHOP STYLE SELECTION TOOLS (Marquee, Lasso, Polygonal Lasso)

        private byte[]? _selectionClipboardPixels;
        private Rect? _selectionClipboardRect;
        private bool _selectionClipboardIsFullLayer;
        private EditorLayer? _selectionClipboardLayerSource;
        private Geometry? _selectionClipboardGeometry;
        private int _magicWandTolerance = 25;
        private bool[,]? _quickSelectionVisited;
        private byte[]? _quickSelectionPixels;
        private byte _quickSelTargetR, _quickSelTargetG, _quickSelTargetB, _quickSelTargetA;
        private string _cropRatioMode = "Free";
        private double _cropCustomW = 1.0;
        private double _cropCustomH = 1.0;
        private readonly List<Rect> _slices = new();
        private int _selectedSliceIndex = -1;
        private string _sliceResizeHandle = "";
        private Rect _sliceDragStartRect;

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

                int stride = activeLayer.Width * 4;
                byte[] tempFullPixels = new byte[stride * activeLayer.Height];
                activeLayer.Bitmap.CopyPixels(tempFullPixels, stride, 0);

                // Scan for tight bounds of non-transparent pixels inside selection
                int minX = int.MaxValue;
                int maxX = int.MinValue;
                int minY = int.MaxValue;
                int maxY = int.MinValue;

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = startX; x <= endX; x++)
                    {
                        if (IsInsideSelection(x, y))
                        {
                            int idx = rowOffset + x * 4;
                            if (tempFullPixels[idx + 3] > 0)
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                        }
                    }
                }

                if (minX > maxX || minY > maxY)
                {
                    // No non-transparent pixels inside selection!
                    _selectionClipboardPixels = null;
                    _selectionClipboardRect = null;
                    _selectionClipboardGeometry = null;
                    return;
                }

                int bw = maxX - minX + 1;
                int bh = maxY - minY + 1;
                int tightStride = bw * 4;
                _selectionClipboardPixels = new byte[tightStride * bh];

                for (int y = minY; y <= maxY; y++)
                {
                    int rowOffset = y * stride;
                    int tightRowOffset = (y - minY) * tightStride;
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (IsInsideSelection(x, y))
                        {
                            int idx = rowOffset + x * 4;
                            int tightIdx = tightRowOffset + (x - minX) * 4;
                            _selectionClipboardPixels[tightIdx] = tempFullPixels[idx];
                            _selectionClipboardPixels[tightIdx + 1] = tempFullPixels[idx + 1];
                            _selectionClipboardPixels[tightIdx + 2] = tempFullPixels[idx + 2];
                            _selectionClipboardPixels[tightIdx + 3] = tempFullPixels[idx + 3];
                        }
                    }
                }

                _selectionClipboardRect = new Rect(minX, minY, bw, bh);
                _selectionClipboardIsFullLayer = false;
                _selectionClipboardLayerSource = null;

                // Intersect selection geometry with the tight rect geometry
                var tightRectGeom = new RectangleGeometry(new Rect(minX, minY, bw, bh));
                _selectionClipboardGeometry = Geometry.Combine(_activeSelectionGeometry.Clone(), tightRectGeom, GeometryCombineMode.Intersect, null).GetOutlinedPathGeometry();
            }
            else
            {
                _selectionClipboardPixels = null;
                _selectionClipboardRect = null;
                _selectionClipboardIsFullLayer = true;
                _selectionClipboardLayerSource = activeLayer.Duplicate();
                _selectionClipboardGeometry = activeLayer.ContentGeometry?.Clone();
            }
        }

        private void PasteSelectionAsLayer()
        {
            if (_node.EditorDoc == null) return;

            EditorLayer newLayer;
            if (_selectionClipboardIsFullLayer && _selectionClipboardLayerSource != null)
            {
                newLayer = _selectionClipboardLayerSource.Duplicate();
                newLayer.Name = _node.EditorDoc.GetNextLayerName();
            }
            else if (_selectionClipboardPixels != null && _selectionClipboardRect.HasValue)
            {
                int docW = _node.EditorDoc.Width;
                int docH = _node.EditorDoc.Height;

                newLayer = new EditorLayer(docW, docH, _node.EditorDoc.GetNextLayerName());
                newLayer.Clear();

                int startX = (int)_selectionClipboardRect.Value.Left;
                int startY = (int)_selectionClipboardRect.Value.Top;
                int w = (int)_selectionClipboardRect.Value.Width;
                int h = (int)_selectionClipboardRect.Value.Height;

                int stride = w * 4;
                newLayer.Bitmap.WritePixels(new Int32Rect(startX, startY, w, h), _selectionClipboardPixels, stride, 0);
                
                var origBmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                origBmp.WritePixels(new Int32Rect(0, 0, w, h), _selectionClipboardPixels, stride, 0);
                newLayer.OriginalTransformBitmap = origBmp;
                newLayer.ContentBounds = new Rect(startX, startY, w, h);
                
                if (_selectionClipboardGeometry != null)
                {
                    newLayer.ContentGeometry = _selectionClipboardGeometry.Clone();
                }
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
            foreach (var l in _node.EditorDoc.Layers)
            {
                l.IsSelected = (l == newLayer);
                if (l.ChildLayers != null)
                {
                    foreach (var c in l.ChildLayers)
                    {
                        c.IsSelected = (c == newLayer);
                    }
                }
            }
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
                var outlinedPreview = scaledGeom.GetOutlinedPathGeometry();
                double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
                if (zoom <= 0) zoom = 1.0;
                double strokeW = 1.0 / zoom;

                SelectionPreviewPolygon.Data = outlinedPreview;
                SelectionPreviewPolygon.StrokeThickness = strokeW;
                SelectionPreviewPolygon.StrokeDashArray = new DoubleCollection { 2.0, 2.0 };
                SelectionPreviewPolygon.Visibility = Visibility.Visible;

                if (SelectionPreviewPolygonBg != null)
                {
                    SelectionPreviewPolygonBg.Data = outlinedPreview;
                    SelectionPreviewPolygonBg.StrokeThickness = strokeW;
                    SelectionPreviewPolygonBg.Visibility = Visibility.Visible;
                }
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

        private Rect _activeCropRect;
        private Rect _normalizedCropRect = new Rect(0.05, 0.05, 0.9, 0.9);
        private string _activeCropHandle = "";
        private Point _cropDragStartMouse;
        private Rect _cropDragStartRect;
        private bool _isDraggingCrop = false;

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



        #endregion
    }
}
