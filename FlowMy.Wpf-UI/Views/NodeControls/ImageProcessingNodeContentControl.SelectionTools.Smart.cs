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

            if (SelectionGroupPopup.Child is FrameworkElement childBorder)
            {
                childBorder.LayoutTransform = EditorToolbox.LayoutTransform ?? Transform.Identity;
            }
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

            var transform = new TranslateTransform(activeLayer.OffsetX, activeLayer.OffsetY);
            var bakedGroup = Geometry.Combine(group, Geometry.Empty, GeometryCombineMode.Union, transform);
            ApplyNewGeometry(bakedGroup);
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

            var transform = new TranslateTransform(activeLayer.OffsetX, activeLayer.OffsetY);
            var groupDoc = Geometry.Combine(group, Geometry.Empty, GeometryCombineMode.Union, transform);

            Geometry combined;
            if (_activeSelectionGeometry == null || _currentSelectionMode == SelectionMode.New)
            {
                combined = groupDoc;
            }
            else if (_currentSelectionMode == SelectionMode.Add)
            {
                combined = Geometry.Combine(_activeSelectionGeometry, groupDoc, GeometryCombineMode.Union, null);
            }
            else
            {
                combined = Geometry.Combine(_activeSelectionGeometry, groupDoc, GeometryCombineMode.Exclude, null);
            }

            double scaleX = MainImage.ActualWidth / _node.EditorDoc.Width;
            double scaleY = MainImage.ActualHeight / _node.EditorDoc.Height;
            var scaledGeom = combined.Clone();
            scaledGeom.Transform = new ScaleTransform(scaleX, scaleY);

            if (SelectionPreviewPolygon != null)
            {
                var outlinedPreview = scaledGeom;
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
            var activeLayer = _node?.EditorDoc?.ActiveLayer;
            if (activeLayer == null) return;

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

            var transform = new TranslateTransform(activeLayer.OffsetX, activeLayer.OffsetY);
            var bakedGroup = Geometry.Combine(group, Geometry.Empty, GeometryCombineMode.Union, transform);
            ApplyNewGeometry(bakedGroup);
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

                var transform = new TranslateTransform(activeLayer.OffsetX, activeLayer.OffsetY);
                var bakedGroup = Geometry.Combine(group, Geometry.Empty, GeometryCombineMode.Union, transform);
                ApplyNewGeometry(bakedGroup);
            }
        }

        private (byte R, byte G, byte B, byte A) GetPixelColor(byte[] pixels, int x, int y, int width)
        {
            int offset = (y * width + x) * 4;
            return (pixels[offset + 2], pixels[offset + 1], pixels[offset], pixels[offset + 3]);
        }
    }
}
