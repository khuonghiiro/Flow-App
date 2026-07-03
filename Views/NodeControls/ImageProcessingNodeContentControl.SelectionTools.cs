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

            if (_selectionRect.HasValue)
            {
                int startX = Math.Max(0, (int)_selectionRect.Value.Left);
                int endX = Math.Min(activeLayer.Width - 1, (int)_selectionRect.Value.Right);
                int startY = Math.Max(0, (int)_selectionRect.Value.Top);
                int endY = Math.Min(activeLayer.Height - 1, (int)_selectionRect.Value.Bottom);

                int w = endX - startX + 1;
                int h = endY - startY + 1;
                if (w <= 0 || h <= 0) return;

                var rect = new Int32Rect(startX, startY, w, h);
                int stride = w * 4;
                _selectionClipboardPixels = new byte[stride * h];
                activeLayer.Bitmap.CopyPixels(rect, _selectionClipboardPixels, stride, 0);
                _selectionClipboardRect = new Rect(startX, startY, w, h);
                _selectionClipboardIsFullLayer = false;
                _selectionClipboardLayerSource = null;
            }
            else if (_selectionPoints != null && _selectionPoints.Count >= 3)
            {
                double minX = _selectionPoints.Min(p => p.X);
                double maxX = _selectionPoints.Max(p => p.X);
                double minY = _selectionPoints.Min(p => p.Y);
                double maxY = _selectionPoints.Max(p => p.Y);

                int startX = Math.Max(0, (int)minX);
                int endX = Math.Min(activeLayer.Width - 1, (int)maxX);
                int startY = Math.Max(0, (int)minY);
                int endY = Math.Min(activeLayer.Height - 1, (int)maxY);

                int w = endX - startX + 1;
                int h = endY - startY + 1;
                if (w <= 0 || h <= 0) return;

                var rect = new Int32Rect(startX, startY, w, h);
                int stride = w * 4;
                _selectionClipboardPixels = new byte[stride * h];
                activeLayer.Bitmap.CopyPixels(rect, _selectionClipboardPixels, stride, 0);

                // Clear pixels outside the polygon
                for (int dy = 0; dy < h; dy++)
                {
                    int py = startY + dy;
                    for (int dx = 0; dx < w; dx++)
                    {
                        int px = startX + dx;
                        if (!IsPointInPolygon(new Point(px, py), _selectionPoints))
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

            int stride = activeLayer.Width * 4;
            var oldPixels = new byte[stride * activeLayer.Height];
            activeLayer.Bitmap.CopyPixels(oldPixels, stride, 0);

            var newPixels = new byte[stride * activeLayer.Height];
            Array.Copy(oldPixels, newPixels, oldPixels.Length);

            int startX = 0, endX = 0, startY = 0, endY = 0;
            bool hasSelection = false;

            if (_selectionRect.HasValue)
            {
                startX = Math.Max(0, (int)_selectionRect.Value.Left);
                endX = Math.Min(activeLayer.Width - 1, (int)_selectionRect.Value.Right);
                startY = Math.Max(0, (int)_selectionRect.Value.Top);
                endY = Math.Min(activeLayer.Height - 1, (int)_selectionRect.Value.Bottom);
                hasSelection = true;
            }
            else if (_selectionPoints != null && _selectionPoints.Count >= 3)
            {
                double minX = _selectionPoints.Min(p => p.X);
                double maxX = _selectionPoints.Max(p => p.X);
                double minY = _selectionPoints.Min(p => p.Y);
                double maxY = _selectionPoints.Max(p => p.Y);

                startX = Math.Max(0, (int)minX);
                endX = Math.Min(activeLayer.Width - 1, (int)maxX);
                startY = Math.Max(0, (int)minY);
                endY = Math.Min(activeLayer.Height - 1, (int)maxY);
                hasSelection = true;
            }

            if (!hasSelection) return;

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * activeLayer.Width * 4;
                for (int x = startX; x <= endX; x++)
                {
                    if (_selectionRect.HasValue)
                    {
                        newPixels[rowOffset + x * 4 + 3] = 0;
                    }
                    else if (_selectionPoints != null && IsPointInPolygon(new Point(x, y), _selectionPoints))
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

        #endregion
    }
}
