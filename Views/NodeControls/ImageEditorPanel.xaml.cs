using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Panel bên phải cho Manual editor mode — quản lý layers, tools, colors, undo/redo.
    /// </summary>
    public partial class ImageEditorPanel : UserControl
    {
        private EditorDocument? _doc;
        private string _activeTool = "Brush";
        
        // Drag reorder layers state fields
        private EditorLayer? _draggedLayer;
        private int _dragStartIndex = -1;
        private Point _dragStartPoint;
        private bool _isDraggingLayer;

        public ImageEditorPanel()
        {
            InitializeComponent();
        }



        /// <summary>Tool name hiện tại (Brush, Eraser, Fill, etc.).</summary>
        public string ActiveToolName => _activeTool;

        public double BrushSize => SliderBrushSize.Value;
        public double BrushHardness => SliderBrushHardness.Value;
        public double BrushFlow => SliderBrushFlow.Value;

        /// <summary>Gán document để panel bind vào.</summary>
        public void SetDocument(EditorDocument? doc)
        {
            // Unsubscribe cũ
            if (_doc != null)
            {
                _doc.Layers.CollectionChanged -= OnLayersCollectionChanged;
                _doc.PropertyChanged -= OnDocPropertyChanged;
                _doc.History.PropertyChanged -= OnHistoryPropertyChanged;
            }

            _doc = doc;

            if (_doc != null)
            {
                _doc.Layers.CollectionChanged += OnLayersCollectionChanged;
                _doc.PropertyChanged += OnDocPropertyChanged;
                _doc.History.PropertyChanged += OnHistoryPropertyChanged;
                RefreshLayersList();
                SyncColorDisplay();
                SyncHistoryButtons();
                SyncActiveLayerOpacity();
                SyncBlendModeCombo();
                UpdateBrushPreview();
            }
            else
            {
                LayersList.ItemsSource = null;
            }
        }

        // ═══════ LAYERS ═══════

        private void RefreshLayersList()
        {
            if (_doc == null) return;
            // Hiển thị reversed: top layer ở trên (giống Photoshop)
            var targetList = _doc.Layers.Reverse().ToList();

            if (LayersList.ItemsSource is System.Collections.ObjectModel.ObservableCollection<EditorLayer> currentCollection)
            {
                // Đồng bộ phần tử in-place để tránh huỷ container (giữ mouse capture khi kéo thả)
                for (int i = 0; i < targetList.Count; i++)
                {
                    var item = targetList[i];
                    int curIdx = currentCollection.IndexOf(item);
                    if (curIdx == -1)
                    {
                        currentCollection.Insert(i, item);
                    }
                    else if (curIdx != i)
                    {
                        currentCollection.Move(curIdx, i);
                    }
                }
                while (currentCollection.Count > targetList.Count)
                {
                    currentCollection.RemoveAt(currentCollection.Count - 1);
                }
            }
            else
            {
                LayersList.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<EditorLayer>(targetList);
            }

            SyncActiveLayerHighlight();
        }

        private void SyncActiveLayerHighlight()
        {
            if (_doc == null) return;
            foreach (var layer in _doc.Layers)
                layer.IsActive = (layer == _doc.ActiveLayer);
        }

        private void OnLayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshLayersList();
        }

        private void OnDocPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorDocument.ActiveLayer))
            {
                SyncActiveLayerHighlight();
                SyncActiveLayerOpacity();
                SyncBlendModeCombo();
                // Re-composite để canvas phản ánh trạng thái mới (ví dụ layer visibility thay đổi)
                OnDocumentModified();
            }
            else if (e.PropertyName == nameof(EditorDocument.ForegroundColor) ||
                     e.PropertyName == nameof(EditorDocument.BackgroundColor))
            {
                SyncColorDisplay();
                UpdateBrushPreview();
            }
        }

        private void OnHistoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SyncHistoryButtons();
        }

        private void LayerItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_doc == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorLayer layer)
            {
                _doc.ActiveLayer = layer;
                SyncActiveLayerHighlight();
                SyncActiveLayerOpacity();
                SyncBlendModeCombo();

                // Setup drag state
                _draggedLayer = layer;
                _dragStartIndex = _doc.Layers.IndexOf(layer);
                _dragStartPoint = e.GetPosition(this);
                _isDraggingLayer = false;

                fe.CaptureMouse();
                e.Handled = true;
            }
        }

        private void LayerItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (_doc == null || _draggedLayer == null || e.LeftButton != MouseButtonState.Pressed) return;

            if (!_isDraggingLayer)
            {
                Point currentPos = e.GetPosition(this);
                if (Math.Abs(currentPos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingLayer = true;
                }
            }

            if (_isDraggingLayer && sender is FrameworkElement fe)
            {
                // Tìm item bên dưới con trỏ chuột
                Point pt = e.GetPosition(LayersList);
                var hitResult = VisualTreeHelper.HitTest(LayersList, pt);
                if (hitResult != null)
                {
                    DependencyObject obj = hitResult.VisualHit;
                    while (obj != null && obj != LayersList)
                    {
                        if (obj is FrameworkElement hitFe && hitFe.DataContext is EditorLayer targetLayer)
                        {
                            if (targetLayer != _draggedLayer)
                            {
                                int fromIdx = _doc.Layers.IndexOf(_draggedLayer);
                                int toIdx = _doc.Layers.IndexOf(targetLayer);
                                if (fromIdx >= 0 && toIdx >= 0 && fromIdx != toIdx)
                                {
                                    // Move item trực tiếp trong ObservableCollection để có visual feedback tức thì
                                    _doc.Layers.Move(fromIdx, toIdx);
                                }
                            }
                            break;
                        }
                        obj = VisualTreeHelper.GetParent(obj);
                    }
                }
            }
        }

        private void LayerItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                fe.ReleaseMouseCapture();
            }

            if (_doc != null && _draggedLayer != null && _isDraggingLayer)
            {
                int finalIndex = _doc.Layers.IndexOf(_draggedLayer);
                if (finalIndex >= 0 && _dragStartIndex >= 0 && finalIndex != _dragStartIndex)
                {
                    // Tạm thời move ngược lại index ban đầu trước khi add command vào history
                    _doc.Layers.Move(finalIndex, _dragStartIndex);

                    var cmd = new LayerReorderCommand(_doc, _dragStartIndex, finalIndex);
                    _doc.History.Execute(cmd);
                    OnDocumentModified();
                }
            }

            _draggedLayer = null;
            _dragStartIndex = -1;
            _isDraggingLayer = false;
        }

        private void LayerVisibilityToggle_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is EditorLayer layer)
            {
                layer.IsVisible = !layer.IsVisible;
                RefreshLayersList();
                OnDocumentModified();
                e.Handled = true;
            }
        }

        private void LayerLockToggle_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is EditorLayer layer)
            {
                layer.IsLocked = !layer.IsLocked;
                RefreshLayersList();
                e.Handled = true;
            }
        }

        private void SyncActiveLayerOpacity()
        {
            if (_doc?.ActiveLayer == null) return;
            var pct = (int)(_doc.ActiveLayer.Opacity * 100);
            SliderLayerOpacity.Value = pct;
            TxtOpacityValue.Text = $"{pct}%";
        }

        private void SyncBlendModeCombo()
        {
            if (_doc?.ActiveLayer == null) return;
            CmbBlendMode.SelectedIndex = (int)_doc.ActiveLayer.BlendMode;
        }

        private void BtnAddLayer_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;

            int insertIndex = _doc.Layers.Count;
            if (_doc.ActiveLayer != null)
            {
                int idx = _doc.Layers.IndexOf(_doc.ActiveLayer);
                if (idx >= 0) insertIndex = idx + 1;
            }

            // Duplicate active layer nếu có, ngược lại tạo layer transparent
            EditorLayer newLayer;
            if (_doc.ActiveLayer != null)
            {
                newLayer = _doc.ActiveLayer.Duplicate();
                // Tạo tên copy tăng dần: "X copy 1", "X copy 2", ...
                newLayer.Name = GenerateCopyName(_doc.ActiveLayer.Name);
            }
            else
            {
                newLayer = new EditorLayer(_doc.Width, _doc.Height, $"Layer {_doc.Layers.Count + 1}");
            }

            var cmd = new LayerAddCommand(_doc, newLayer, insertIndex);
            _doc.History.Execute(cmd);

            // Chọn layer mới (giống Photoshop Ctrl+J) — layer mới ở TRÊN, có quyền ưu tiên cao hơn
            _doc.ActiveLayer = newLayer;
            SyncActiveLayerHighlight();
            SyncActiveLayerOpacity();
            SyncBlendModeCombo();
            OnDocumentModified();
        }

        /// <summary>Tạo tên copy tăng dần dựa trên tên gốc và các layer hiện có.</summary>
        private string GenerateCopyName(string baseName)
        {
            if (_doc == null) return baseName + " copy 1";

            // Tìm số copy tiếp theo: "baseName copy N"
            string prefix = baseName + " copy ";
            int maxNum = 0;
            foreach (var layer in _doc.Layers)
            {
                if (layer.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var numPart = layer.Name.Substring(prefix.Length);
                    if (int.TryParse(numPart, out int num) && num > maxNum)
                        maxNum = num;
                }
                else if (string.Equals(layer.Name, baseName + " copy", StringComparison.OrdinalIgnoreCase))
                {
                    // Trường hợp cũ "X copy" (không có số) → coi như copy 1
                    if (maxNum < 1) maxNum = 1;
                }
            }

            return prefix + (maxNum + 1);
        }

        private void BtnRemoveLayer_Click(object sender, RoutedEventArgs e)
        {
            if (_doc?.ActiveLayer == null || _doc.Layers.Count <= 1) return;

            var cmd = new LayerRemoveCommand(_doc, _doc.ActiveLayer);
            _doc.History.Execute(cmd);
            OnDocumentModified();
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_doc?.ActiveLayer == null) return;
            int idx = _doc.Layers.IndexOf(_doc.ActiveLayer);
            if (idx < 0 || idx >= _doc.Layers.Count - 1) return;

            var cmd = new LayerReorderCommand(_doc, idx, idx + 1);
            _doc.History.Execute(cmd);
            OnDocumentModified();
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_doc?.ActiveLayer == null) return;
            int idx = _doc.Layers.IndexOf(_doc.ActiveLayer);
            if (idx <= 0) return;

            var cmd = new LayerReorderCommand(_doc, idx, idx - 1);
            _doc.History.Execute(cmd);
            OnDocumentModified();
        }

        private void SliderLayerOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_doc?.ActiveLayer == null) return;
            var val = (int)SliderLayerOpacity.Value;
            _doc.ActiveLayer.Opacity = val / 100.0;
            TxtOpacityValue.Text = $"{val}%";
            OnDocumentModified();
        }

        private void CmbBlendMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_doc?.ActiveLayer == null || CmbBlendMode.SelectedIndex < 0) return;
            _doc.ActiveLayer.BlendMode = (BlendMode)CmbBlendMode.SelectedIndex;
            OnDocumentModified();
        }

        // ═══════ TOOLS ═══════

        private void SelectTool(string toolName)
        {
            _activeTool = toolName;
        }

        /// <summary>Public accessor để parent control có thể set tool từ toolbox bên ngoài.</summary>
        public void SelectToolByName(string toolName)
        {
            SelectTool(toolName);
        }

        private void SliderBrushSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrushSize == null) return;
            TxtBrushSize.Text = $"{(int)SliderBrushSize.Value}";
            UpdateBrushPreview();
        }

        private void SliderBrushHardness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrushHardness == null) return;
            TxtBrushHardness.Text = $"{(int)SliderBrushHardness.Value}%";
            UpdateBrushPreview();
        }

        private void SliderBrushFlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrushFlow == null) return;
            TxtBrushFlow.Text = $"{(int)SliderBrushFlow.Value}%";
            UpdateBrushPreview();
        }

        private void UpdateBrushPreview()
        {
            if (BrushPreviewEllipse == null) return;

            double hardness = SliderBrushHardness.Value / 100.0;
            double flow = SliderBrushFlow.Value / 100.0;
            Color brushColor = Colors.White;

            // Fixed maximum preview size to focus purely on hardness and flow representation
            double previewSize = 44.0;
            BrushPreviewEllipse.Width = previewSize;
            BrushPreviewEllipse.Height = previewSize;

            var brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));

            double stopOffset = Math.Max(0.0, hardness);
            if (stopOffset < 0.99)
            {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), stopOffset));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
            }
            else
            {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.99));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
            }

            BrushPreviewEllipse.Fill = brush;
        }

        // ═══════ COLORS ═══════

        private void SyncColorDisplay()
        {
            if (_doc == null) return;
            FgColorBorder.Background = new SolidColorBrush(_doc.ForegroundColor);
            BgColorBorder.Background = new SolidColorBrush(_doc.BackgroundColor);
            TxtColorHex.Text = $"#{_doc.ForegroundColor.R:X2}{_doc.ForegroundColor.G:X2}{_doc.ForegroundColor.B:X2}";
        }

        private void FgColorBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_doc == null) return;
            var color = PickColor(_doc.ForegroundColor);
            if (color.HasValue)
            {
                _doc.ForegroundColor = color.Value;
                SyncColorDisplay();
            }
        }

        private void BgColorBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_doc == null) return;
            var color = PickColor(_doc.BackgroundColor);
            if (color.HasValue)
            {
                _doc.BackgroundColor = color.Value;
                SyncColorDisplay();
            }
        }

        private void BtnSwapColors_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            (_doc.ForegroundColor, _doc.BackgroundColor) = (_doc.BackgroundColor, _doc.ForegroundColor);
            SyncColorDisplay();
        }

        private void BtnResetColors_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            _doc.ForegroundColor = Colors.Black;
            _doc.BackgroundColor = Colors.White;
            SyncColorDisplay();
        }

        private static Color? PickColor(Color initial)
        {
            using var dlg = new System.Windows.Forms.ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B),
                FullOpen = true,
                AnyColor = true
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = dlg.Color;
                return Color.FromArgb(c.A, c.R, c.G, c.B);
            }
            return null;
        }

        // ═══════ UNDO/REDO ═══════

        private void SyncHistoryButtons()
        {
            if (_doc == null) return;
            BtnUndo.IsEnabled = _doc.History.CanUndo;
            BtnRedo.IsEnabled = _doc.History.CanRedo;
            BtnUndo.ToolTip = _doc.History.CanUndo
                ? $"Undo: {_doc.History.NextUndoDescription} (Ctrl+Z)"
                : "Undo (Ctrl+Z)";
            BtnRedo.ToolTip = _doc.History.CanRedo
                ? $"Redo: {_doc.History.NextRedoDescription} (Ctrl+Y)"
                : "Redo (Ctrl+Y)";
            TxtHistoryStatus.Text = $"{_doc.History.UndoCount} steps";
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            _doc?.History.Undo();
            OnDocumentModified();
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            _doc?.History.Redo();
            OnDocumentModified();
        }


        /// <summary>Gọi khi document thay đổi — trigger re-composite.</summary>
        public event Action? DocumentModified;

        private void OnDocumentModified()
        {
            DocumentModified?.Invoke();
        }
    }
}
