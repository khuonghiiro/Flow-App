using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        public event EventHandler? TextPropertiesChanged;
        public event EventHandler? ActiveLayerChanged;

        public double TextFontSize => SliderTextFontSize != null ? SliderTextFontSize.Value : 24;
        public Color TextColor => (BorderTextColorSwatch != null && BorderTextColorSwatch.Background is SolidColorBrush scb) ? scb.Color : Colors.White;
        public string TextFontFamily => CmbFontFamily != null && CmbFontFamily.SelectedItem is ComboBoxItem item ? item.Content.ToString() : "Arial";
        public string TextFontStyle => CmbFontStyle != null && CmbFontStyle.SelectedItem is ComboBoxItem item ? item.Content.ToString() : "Bold";

        public void UpdatePanelVisibilities(string activeTool)
        {
            if (BrushPropertiesPanel != null)
                BrushPropertiesPanel.Visibility = Visibility.Collapsed;
            if (TextPropertiesPanel != null)
                TextPropertiesPanel.Visibility = (activeTool == "Text") ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetTextProperties(double size, Color color, string family, string style)
        {
            if (SliderTextFontSize != null) SliderTextFontSize.Value = size;
            if (BorderTextColorSwatch != null) BorderTextColorSwatch.Background = new SolidColorBrush(color);
            if (BtnTextColor != null) BtnTextColor.Content = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            
            if (CmbFontFamily != null)
            {
                foreach (ComboBoxItem item in CmbFontFamily.Items)
                {
                    if (item.Content.ToString() == family)
                    {
                        CmbFontFamily.SelectedItem = item;
                        break;
                    }
                }
            }
            if (CmbFontStyle != null)
            {
                foreach (ComboBoxItem item in CmbFontStyle.Items)
                {
                    if (item.Content.ToString() == style)
                    {
                        CmbFontStyle.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void SliderTextFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPanelFontSize != null) TxtPanelFontSize.Text = $"{(int)e.NewValue}";
            TextPropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BtnTextColor_Click(object sender, RoutedEventArgs e)
        {
            Color current = TextColor;
            var picked = PickColor(current);
            if (picked.HasValue)
            {
                BorderTextColorSwatch.Background = new SolidColorBrush(picked.Value);
                BtnTextColor.Content = $"#{picked.Value.R:X2}{picked.Value.G:X2}{picked.Value.B:X2}";
                TextPropertiesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CmbFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TextPropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CmbFontStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TextPropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        private ImageProcessingNode? _node;
        private IWorkflowEditorHost? _host;

        public void SetNodeAndHost(ImageProcessingNode node, IWorkflowEditorHost host)
        {
            _node = node;
            _host = host;
        }

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

        public void RefreshLayersList()
        {
            if (_doc == null) return;
            // Hiển thị reversed: top layer ở trên (giống Photoshop)
            var reversedParents = _doc.Layers.Reverse().ToList();
            var targetList = new List<EditorLayer>();
            foreach (var parent in reversedParents)
            {
                targetList.Add(parent);
                if (parent.ChildLayers != null)
                {
                    foreach (var child in parent.ChildLayers)
                    {
                        targetList.Add(child);
                    }
                }
            }

            // Luôn dùng in-place sync để tránh phá huỷ toàn bộ ListBoxItem containers
            if (LayersList.ItemsSource is System.Collections.ObjectModel.ObservableCollection<EditorLayer> currentCollection)
            {
                for (int i = 0; i < targetList.Count; i++)
                {
                    var item = targetList[i];
                    if (i < currentCollection.Count && currentCollection[i] == item) continue;
                    int curIdx = -1;
                    for (int j = i; j < currentCollection.Count; j++)
                    {
                        if (currentCollection[j] == item) { curIdx = j; break; }
                    }
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
            {
                layer.IsActive = (layer == _doc.ActiveLayer);
                if (!_isSyncingSelection)
                {
                    layer.IsSelected = (layer == _doc.ActiveLayer);
                }
                else
                {
                    if (layer.IsActive && !layer.IsSelected)
                        layer.IsSelected = true;
                }

                if (layer.ChildLayers != null)
                {
                    foreach (var child in layer.ChildLayers)
                    {
                        child.IsActive = (child == _doc.ActiveLayer);
                        if (!_isSyncingSelection)
                        {
                            child.IsSelected = (child == _doc.ActiveLayer);
                        }
                        else
                        {
                            if (child.IsActive && !child.IsSelected)
                                child.IsSelected = true;
                        }
                    }
                }
            }
        }

        private bool _isSyncingSelection = false;
        private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_doc == null || _isSyncingSelection) return;

            _isSyncingSelection = true;
            try
            {
                var selectedLayers = LayersList.SelectedItems.Cast<EditorLayer>().ToList();
                if (e.AddedItems.Count > 0)
                {
                    var newActive = e.AddedItems[0] as EditorLayer;
                    if (newActive != null && _doc.ActiveLayer != newActive)
                    {
                        _doc.ActiveLayer = newActive;
                        if (newActive.ParentLayer != null)
                        {
                            newActive.ParentLayer.ActiveChildLayer = newActive;
                        }
                        else
                        {
                            newActive.ActiveChildLayer = null;
                        }
                    }
                }
                
                foreach (var layer in _doc.Layers)
                {
                    layer.IsSelected = selectedLayers.Contains(layer);
                    if (layer.ChildLayers != null)
                    {
                        foreach (var child in layer.ChildLayers)
                        {
                            child.IsSelected = selectedLayers.Contains(child);
                        }
                    }
                }
            }
            finally
            {
                _isSyncingSelection = false;
            }

            SyncActiveLayerHighlight();
            SyncActiveLayerOpacity();
            SyncBlendModeCombo();
            OnDocumentModified();
        }

        private void HandleLayerClick(EditorLayer clickedLayer, bool ctrl, bool shift)
        {
            if (_doc == null) return;

            _isSyncingSelection = true;
            try
            {
                if (shift)
                {
                    var active = _doc.ActiveLayer;
                    if (active == null)
                    {
                        SelectSingleLayer(clickedLayer);
                    }
                    else
                    {
                        int idx1 = _doc.Layers.IndexOf(active);
                        int idx2 = _doc.Layers.IndexOf(clickedLayer);
                        if (idx1 >= 0 && idx2 >= 0)
                        {
                            int min = Math.Min(idx1, idx2);
                            int max = Math.Max(idx1, idx2);

                            foreach (var l in _doc.Layers)
                            {
                                int idx = _doc.Layers.IndexOf(l);
                                l.IsSelected = (idx >= min && idx <= max);
                            }
                            
                            _doc.ActiveLayer = clickedLayer;
                        }
                    }
                }
                else if (ctrl)
                {
                    clickedLayer.IsSelected = !clickedLayer.IsSelected;
                    
                    if (!clickedLayer.IsSelected && _doc.ActiveLayer == clickedLayer)
                    {
                        var nextActive = _doc.Layers.LastOrDefault(l => l.IsSelected);
                        _doc.ActiveLayer = nextActive;
                    }
                    else if (clickedLayer.IsSelected)
                    {
                        _doc.ActiveLayer = clickedLayer;
                    }
                }
                else
                {
                    SelectSingleLayer(clickedLayer);
                }
            }
            finally
            {
                _isSyncingSelection = false;
            }

            SyncActiveLayerHighlight();
            SyncActiveLayerOpacity();
            SyncBlendModeCombo();
            OnDocumentModified();
        }

        private void SelectSingleLayer(EditorLayer clickedLayer)
        {
            if (_doc == null) return;
            bool wasSyncing = _isSyncingSelection;
            _isSyncingSelection = true;
            try
            {
                foreach (var l in _doc.Layers)
                {
                    l.IsSelected = (l == clickedLayer);
                    if (l.ChildLayers != null)
                    {
                        foreach (var child in l.ChildLayers)
                        {
                            child.IsSelected = (child == clickedLayer);
                        }
                    }
                }
                _doc.ActiveLayer = clickedLayer;

                // Sync ActiveChildLayer on the parent so radio indicators update correctly
                if (clickedLayer.ParentLayer != null)
                {
                    // User clicked a child variant → activate it on the parent
                    clickedLayer.ParentLayer.ActiveChildLayer = clickedLayer;
                }
                else
                {
                    // User clicked a parent layer → deactivate any active child variant
                    clickedLayer.ActiveChildLayer = null;
                }
            }
            finally
            {
                _isSyncingSelection = wasSyncing;
            }
        }

        public List<EditorLayer> SelectedLayers
        {
            get
            {
                if (_doc == null) return new List<EditorLayer>();
                var list = _doc.Layers.Where(l => l.IsSelected).ToList();
                if (list.Count == 0 && _doc.ActiveLayer != null)
                {
                    list.Add(_doc.ActiveLayer);
                }
                return list;
            }
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

                ActiveLayerChanged?.Invoke(this, EventArgs.Empty);
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
                LayersList.Focus();

                bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

                HandleLayerClick(layer, ctrl, shift);

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
            e.Handled = true;
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
            // Legacy — kept for compatibility but no longer wired in XAML
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
            // Legacy — kept for compatibility
            if (_doc == null) return;
            AddNewLayerFromActive();
        }

        /// <summary>Inline copy button trên mỗi layer item — nhân đôi layer được click.</summary>
        private void BtnCopyLayer_Click(object sender, MouseButtonEventArgs e)
        {
            if (_doc == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorLayer sourceLayer)
            {
                int insertIndex = _doc.Layers.IndexOf(sourceLayer);
                if (insertIndex < 0) insertIndex = _doc.Layers.Count;
                else insertIndex += 1;

                var newLayer = sourceLayer.Duplicate();
                newLayer.Name = GenerateCopyName(sourceLayer.Name);

                var cmd = new LayerAddCommand(_doc, newLayer, insertIndex);
                _doc.History.Execute(cmd);

                SelectSingleLayer(newLayer);
                SyncActiveLayerHighlight();
                SyncActiveLayerOpacity();
                SyncBlendModeCombo();
                OnDocumentModified();
            }
            e.Handled = true;
        }

        private void BtnPromoteLayer_Click(object sender, MouseButtonEventArgs e)
        {
            if (_doc == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorLayer childLayer && childLayer.ParentLayer != null)
            {
                var parent = childLayer.ParentLayer;

                // Duplicate the child layer as standalone
                var newLayer = childLayer.Duplicate();
                newLayer.ParentLayer = null; // detach from parent
                newLayer.Name = GenerateCopyName(childLayer.Name);

                // Insert after the parent in the main layers list
                int parentIndex = _doc.Layers.IndexOf(parent);
                int insertIndex = parentIndex >= 0 ? parentIndex + 1 : _doc.Layers.Count;

                var cmd = new LayerAddCommand(_doc, newLayer, insertIndex);
                _doc.History.Execute(cmd);

                SelectSingleLayer(newLayer);
                SyncActiveLayerHighlight();
                SyncActiveLayerOpacity();
                SyncBlendModeCombo();
                OnDocumentModified();
            }
            e.Handled = true;
        }

        private void AddNewLayerFromActive()
        {
            int insertIndex = _doc.Layers.Count;
            if (_doc.ActiveLayer != null)
            {
                int idx = _doc.Layers.IndexOf(_doc.ActiveLayer);
                if (idx >= 0) insertIndex = idx + 1;
            }

            EditorLayer newLayer;
            if (_doc.ActiveLayer != null)
            {
                newLayer = _doc.ActiveLayer.Duplicate();
                newLayer.Name = GenerateCopyName(_doc.ActiveLayer.Name);
            }
            else
            {
                newLayer = new EditorLayer(_doc.Width, _doc.Height, $"layer {_doc.Layers.Count}");
            }

            var cmd = new LayerAddCommand(_doc, newLayer, insertIndex);
            _doc.History.Execute(cmd);

            SelectSingleLayer(newLayer);
            SyncActiveLayerHighlight();
            SyncActiveLayerOpacity();
            SyncBlendModeCombo();
            OnDocumentModified();
        }

        /// <summary>Tạo tên copy tự động: lấy số ở cuối tên + 1. VD: "layer 0" → "layer 1", "layer 3" → "layer 4".</summary>
        public string GenerateCopyName(string baseName)
        {
            if (_doc == null) return "layer 0";

            // Tách phần text và số ở cuối tên
            string prefix = baseName;
            int baseNum = 0;

            // Tìm số ở cuối chuỗi
            int i = baseName.Length - 1;
            while (i >= 0 && char.IsDigit(baseName[i])) i--;

            if (i < baseName.Length - 1)
            {
                // Có số ở cuối
                prefix = baseName.Substring(0, i + 1);
                if (int.TryParse(baseName.Substring(i + 1), out int parsed))
                    baseNum = parsed;
            }
            else
            {
                // Không có số → thêm space + bắt đầu từ 1
                prefix = baseName + " ";
                baseNum = 0;
            }

            // Tìm số tiếp theo chưa tồn tại
            int num = baseNum + 1;
            while (true)
            {
                string candidate = $"{prefix}{num}";
                bool exists = false;
                foreach (var l in _doc.Layers)
                {
                    if (string.Equals(l.Name, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    return candidate;
                num++;
            }
        }

        private void BtnRemoveLayer_Click(object sender, RoutedEventArgs e)
        {
            // Legacy — kept for compatibility
            if (_doc?.ActiveLayer == null || _doc.Layers.Count <= 1) return;
            var cmd = new LayerRemoveCommand(_doc, _doc.ActiveLayer);
            _doc.History.Execute(cmd);
            OnDocumentModified();
        }

        /// <summary>Inline delete button trên mỗi layer item — xoá layer được click.</summary>
        private void BtnDeleteLayer_Click(object sender, MouseButtonEventArgs e)
        {
            if (_doc == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorLayer layer)
            {
                if (layer.ParentLayer != null)
                {
                    var parent = layer.ParentLayer;
                    parent.ChildLayers.Remove(layer);
                    
                    if (parent.ActiveChildLayer == layer)
                    {
                        parent.ActiveChildLayer = parent.ChildLayers.LastOrDefault();
                    }
                    if (_doc.ActiveLayer == layer)
                    {
                        _doc.ActiveLayer = parent.ActiveChildLayer ?? parent;
                    }
                    RefreshLayersList();
                    OnDocumentModified();
                }
                else
                {
                    if (_doc.Layers.Count <= 1) return;
                    var cmd = new LayerRemoveCommand(_doc, layer);
                    _doc.History.Execute(cmd);
                    OnDocumentModified();
                }
            }
            e.Handled = true;
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

        public event EventHandler? BrushPropertiesChanged;

        private void SliderBrushSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrushSize == null) return;
            TxtBrushSize.Text = $"{(int)SliderBrushSize.Value}";
            UpdateBrushPreview();
            BrushPropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SliderBrushHardness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrushHardness == null) return;
            TxtBrushHardness.Text = $"{(int)SliderBrushHardness.Value}%";
            UpdateBrushPreview();
            BrushPropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SliderBrushFlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrushFlow == null) return;
            TxtBrushFlow.Text = $"{(int)SliderBrushFlow.Value}%";
            UpdateBrushPreview();
            BrushPropertiesChanged?.Invoke(this, EventArgs.Empty);
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

        private void ExecuteHistoryAction(Action action)
        {
            if (_doc == null) return;
            _doc.Layers.CollectionChanged -= OnLayersCollectionChanged;
            try
            {
                action();
            }
            finally
            {
                _doc.Layers.CollectionChanged += OnLayersCollectionChanged;
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            UndoAction();
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            RedoAction();
        }


        private void TxtLayerName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBlock tb && tb.DataContext is EditorLayer layer)
            {
                layer.IsEditingName = true;
                e.Handled = true;

                // Find the TextBox inside the parent Grid and focus it
                var grid = tb.Parent as Grid;
                if (grid != null)
                {
                    var textBox = grid.Children.OfType<TextBox>().FirstOrDefault();
                    if (textBox != null)
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }
            }
        }

        private void TxtLayerNameEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is EditorLayer layer)
            {
                CommitLayerRename(layer, tb.Text);
            }
        }

        private void TxtLayerNameEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is EditorLayer layer)
            {
                if (e.Key == Key.Enter)
                {
                    CommitLayerRename(layer, tb.Text);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    layer.IsEditingName = false;
                    e.Handled = true;
                }
            }
        }

        private void CommitLayerRename(EditorLayer layer, string newName)
        {
            layer.IsEditingName = false;
            newName = newName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(newName) || newName == layer.Name)
                return;

            if (_doc != null)
            {
                var cmd = new RenameLayerCommand(layer, layer.Name, newName);
                _doc.History.Execute(cmd);
                RefreshLayersList();
                OnDocumentModified();
            }
            else
            {
                layer.Name = newName;
            }
        }

        /// <summary>Gọi khi document thay đổi — trigger re-composite.</summary>
        public event Action? DocumentModified;

        internal void OnDocumentModified()
        {
            DocumentModified?.Invoke();
        }

        public void DuplicateSelectedLayers()
        {
            if (_doc == null) return;
            var selected = SelectedLayers;
            if (selected.Count == 0) return;

            var cmd = new DuplicateLayersCommand(_doc, selected);
            ExecuteHistoryAction(() => _doc.History.Execute(cmd));
            
            _isSyncingSelection = true;
            try
            {
                // Re-select the duplicated layers
                foreach (var l in _doc.Layers)
                {
                    l.IsSelected = cmd.DuplicatedLayers.Contains(l);
                }
            }
            finally
            {
                _isSyncingSelection = false;
            }

            RefreshLayersList();
            OnDocumentModified();
        }

        public void DeleteSelectedLayers()
        {
            if (_doc == null || _doc.Layers.Count <= 1) return;
            var selected = SelectedLayers;
            if (selected.Count == 0) return;

            if (selected.Count >= _doc.Layers.Count)
            {
                MessageBox.Show("Không thể xoá tất cả các layer. Tài liệu phải chứa ít nhất một layer.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cmd = new DeleteLayersCommand(_doc, selected);
            ExecuteHistoryAction(() => _doc.History.Execute(cmd));

            RefreshLayersList();
            OnDocumentModified();
        }

        public void MergeSelectedLayers()
        {
            if (_doc == null) return;

            var selected = SelectedLayers;
            List<EditorLayer> mergeSet;

            if (selected.Count >= 2)
            {
                mergeSet = selected;
            }
            else
            {
                var active = _doc.ActiveLayer;
                if (active == null) return;

                int idx = _doc.Layers.IndexOf(active);
                if (idx <= 0) return;

                mergeSet = new List<EditorLayer> { _doc.Layers[idx - 1], active };
            }

            var cmd = new MergeLayersCommand(_doc, mergeSet);
            ExecuteHistoryAction(() => _doc.History.Execute(cmd));

            RefreshLayersList();
            OnDocumentModified();
        }

        public void UndoAction()
        {
            if (_doc == null) return;
            ExecuteHistoryAction(() => _doc.History.Undo());
            RefreshLayersList();
            OnDocumentModified();
        }

        public void RedoAction()
        {
            if (_doc == null) return;
            ExecuteHistoryAction(() => _doc.History.Redo());
            RefreshLayersList();
            OnDocumentModified();
        }

        private void BtnAddImageLayer_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All Files|*.*",
                Title = "Chọn ảnh để thêm vào Layer mới"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(dlg.FileName);
                    bmp.EndInit();
                    bmp.Freeze();

                    int maxNum = 0;
                    foreach (var l in _doc.Layers)
                    {
                        string name = l.Name.Trim();
                        if (name.StartsWith("layer ", StringComparison.OrdinalIgnoreCase))
                        {
                            string numPart = name.Substring(6).Trim();
                            if (int.TryParse(numPart, out int parsed))
                            {
                                if (parsed > maxNum)
                                {
                                    maxNum = parsed;
                                }
                            }
                        }
                    }
                    string finalName = $"layer {maxNum + 1}";

                    var newLayer = new EditorLayer(_doc.Width, _doc.Height, finalName);
                    newLayer.CopyFromPreserveAspectRatio(bmp);

                    int insertIndex = _doc.Layers.Count;
                    if (_doc.ActiveLayer != null)
                    {
                        int idx = _doc.Layers.IndexOf(_doc.ActiveLayer);
                        if (idx >= 0) insertIndex = idx + 1;
                    }

                    var cmd = new LayerAddCommand(_doc, newLayer, insertIndex);
                    ExecuteHistoryAction(() => _doc.History.Execute(cmd));

                    SelectSingleLayer(newLayer);

                    RefreshLayersList();
                    OnDocumentModified();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể tải ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void AddImageLayer()
        {
            BtnAddImageLayer_Click(this, new RoutedEventArgs());
        }

        private void LayerItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_doc == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorLayer layer)
            {
                if (!layer.IsSelected)
                {
                    SelectSingleLayer(layer);
                }

                int selectedCount = SelectedLayers.Count;
                if (selectedCount == 1)
                {
                    PopupBtnAI.Visibility = Visibility.Visible;
                    PopupBtnMerge.Visibility = Visibility.Collapsed;
                }
                else if (selectedCount > 1)
                {
                    PopupBtnAI.Visibility = Visibility.Collapsed;
                    PopupBtnMerge.Visibility = Visibility.Visible;
                }
                else
                {
                    return;
                }

                LayerActionPopup.PlacementTarget = fe;
                LayerActionPopup.IsOpen = true;
                e.Handled = true;
            }
        }

        private void PopupBtnMerge_Click(object sender, MouseButtonEventArgs e)
        {
            LayerActionPopup.IsOpen = false;
            if (_doc == null) return;

            var selected = SelectedLayers;
            if (selected.Count < 2) return;

            var cmd = new MergeLayersCommand(_doc, selected);
            ExecuteHistoryAction(() => _doc.History.Execute(cmd));

            RefreshLayersList();
            OnDocumentModified();
            e.Handled = true;
        }

        private void PopupBtnAI_Click(object sender, MouseButtonEventArgs e)
        {
            LayerActionPopup.IsOpen = false;
            if (_doc == null || _node == null || _host == null) return;

            var active = _doc.ActiveLayer;
            if (active == null) return;

            var ownerWindow = Window.GetWindow(this);
            var dialog = new Views.Overlays.LayerAiDialog(active, _node, _host, _doc, ownerWindow);
            if (dialog.ShowDialog() == true)
            {
                RefreshLayersList();
                OnDocumentModified();
            }
            e.Handled = true;
        }
    }
}
