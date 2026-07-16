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

                var tightRectGeom = new RectangleGeometry(new Rect(minX, minY, bw, bh));
                _selectionClipboardGeometry = Geometry.Combine(_activeSelectionGeometry.Clone(), tightRectGeom, GeometryCombineMode.Intersect, null);
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

    }
}
