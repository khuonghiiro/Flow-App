using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
                if (parent.ChildLayers != null && !parent.IsChildrenCollapsed)
                {
                    foreach (var child in parent.ChildLayers)
                    {
                        targetList.Add(child);
                    }
                }
            }

            if (LayersList.ItemsSource is System.Collections.ObjectModel.ObservableCollection<EditorLayer> cc)
            {
                // Fast path: kiểm tra nếu giống hệt → skip hoàn toàn
                if (cc.Count == targetList.Count)
                {
                    bool same = true;
                    for (int i = 0; i < targetList.Count; i++)
                    {
                        if (cc[i] != targetList[i]) { same = false; break; }
                    }
                    if (same) { SyncActiveLayerHighlight(); return; }
                }

                // Incremental diff: chỉ Insert item mới hoặc Remove item đã xoá
                // Giống cách Photoshop chỉ thêm/xoá đúng item thay đổi
                var targetSet = new HashSet<EditorLayer>(targetList);
                var currentSet = new HashSet<EditorLayer>(cc);

                // Bước 1: Remove các item không còn trong target (lặp ngược để giữ index)
                for (int i = cc.Count - 1; i >= 0; i--)
                {
                    if (!targetSet.Contains(cc[i]))
                    {
                        cc.RemoveAt(i);
                    }
                }

                // Bước 2: Insert các item mới vào đúng vị trí
                for (int i = 0; i < targetList.Count; i++)
                {
                    if (i < cc.Count && cc[i] == targetList[i]) continue;

                    if (!currentSet.Contains(targetList[i]))
                    {
                        // Item mới, insert vào đúng vị trí
                        cc.Insert(i, targetList[i]);
                    }
                    else if (i < cc.Count && cc[i] != targetList[i])
                    {
                        // Item đã tồn tại nhưng sai vị trí → move
                        int curIdx = cc.IndexOf(targetList[i]);
                        if (curIdx >= 0 && curIdx != i)
                        {
                            cc.Move(curIdx, i);
                        }
                    }
                }
            }
            else
            {
                LayersList.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<EditorLayer>(targetList);
            }

            SyncActiveLayerHighlight();
        }

        private EditorLayer? _lastActiveLayer;

        private void SyncActiveLayerHighlight()
        {
            if (_doc == null) return;
            var activeLayer = _doc.ActiveLayer;

            // Loop all layers — chỉ set property khi thay đổi (avoid unnecessary PropertyChanged)
            foreach (var layer in _doc.Layers)
            {
                bool shouldBeActive = (layer == activeLayer);
                if (layer.IsActive != shouldBeActive) layer.IsActive = shouldBeActive;

                if (!_isSyncingSelection)
                {
                    bool shouldBeSelected = (layer == activeLayer);
                    if (layer.IsSelected != shouldBeSelected) layer.IsSelected = shouldBeSelected;
                }
                else
                {
                    if (shouldBeActive && !layer.IsSelected) layer.IsSelected = true;
                }

                if (layer.ChildLayers != null)
                {
                    foreach (var child in layer.ChildLayers)
                    {
                        bool childActive = (child == activeLayer);
                        if (child.IsActive != childActive) child.IsActive = childActive;

                        if (!_isSyncingSelection)
                        {
                            bool childSelected = (child == activeLayer);
                            if (child.IsSelected != childSelected) child.IsSelected = childSelected;
                        }
                        else
                        {
                            if (childActive && !child.IsSelected) child.IsSelected = true;
                        }
                    }
                }
            }
            _lastActiveLayer = activeLayer;
        }

        private bool _isSyncingSelection = false;
        private bool _isSyncingUI = false;
        private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_doc == null || _isSyncingSelection) return;

            _isSyncingSelection = true;
            _isSyncingUI = true;
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
                _isSyncingUI = false;
            }

            _lastActiveLayer = _doc.ActiveLayer;
        }

        private void HandleLayerClick(EditorLayer clickedLayer, bool ctrl, bool shift)
        {
            if (_doc == null) return;

            _isSyncingSelection = true;
            _isSyncingUI = true;
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
                _isSyncingUI = false;
            }

            // Sync _lastActiveLayer trước khi SyncActiveLayerHighlight chạy
            _lastActiveLayer = _doc.ActiveLayer;

            _isSyncingUI = true;
            try
            {
                SyncActiveLayerHighlight();
                SyncActiveLayerOpacity();
                SyncBlendModeCombo();
            }
            finally
            {
                _isSyncingUI = false;
            }
        }

        private void SelectSingleLayer(EditorLayer clickedLayer)
        {
            if (_doc == null) return;
            bool wasSyncing = _isSyncingSelection;
            bool wasSyncingUI = _isSyncingUI;
            _isSyncingSelection = true;
            _isSyncingUI = true;
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
                _isSyncingUI = wasSyncingUI;
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
                SyncColorAdjustPanel();

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
            CommitParentBrushSession();
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
            CommitParentBrushSession();
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
            CommitParentBrushSession();
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
                newLayer.Name = _doc.GetNextLayerName();
            }
            else
            {
                newLayer = new EditorLayer(_doc.Width, _doc.Height, _doc.GetNextLayerName());
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
            if (_isSyncingUI) return;
            if (_doc?.ActiveLayer == null) return;
            var val = (int)SliderLayerOpacity.Value;
            _doc.ActiveLayer.Opacity = val / 100.0;
            TxtOpacityValue.Text = $"{val}%";
            OnDocumentModified();
        }

        private void CmbBlendMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingUI) return;
            if (_doc?.ActiveLayer == null || CmbBlendMode.SelectedIndex < 0) return;
            _doc.ActiveLayer.BlendMode = (BlendMode)CmbBlendMode.SelectedIndex;
            OnDocumentModified();
        }

        // ═══════ TOOLS ═══════

        private void SelectTool(string toolName)
        {
            if (_activeTool != toolName)
            {
                if (_activeTool == "Brush")
                {
                    CommitParentBrushSession();
                }
                _activeTool = toolName;
            }
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
            bool wasSyncingUI = _isSyncingUI;
            _isSyncingUI = true;
            _doc.Layers.CollectionChanged -= OnLayersCollectionChanged;
            try
            {
                action();
            }
            finally
            {
                _doc.Layers.CollectionChanged += OnLayersCollectionChanged;
                _isSyncingUI = wasSyncingUI;
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
            CommitParentBrushSession();
            var selected = SelectedLayers;
            if (selected.Count == 0) return;

            var cmd = new DuplicateLayersCommand(_doc, selected);
            ExecuteHistoryAction(() => _doc.History.Execute(cmd));
            
            _isSyncingSelection = true;
            try
            {
                // Chỉ deselect source layers và select duplicated layers (targeted, không loop all)
                foreach (var s in selected) s.IsSelected = false;
                foreach (var d in cmd.DuplicatedLayers) d.IsSelected = true;
            }
            finally
            {
                _isSyncingSelection = false;
            }

            // Sync _lastActiveLayer vì ActiveLayerChanged bị suppress trong ExecuteHistoryAction
            _lastActiveLayer = _doc.ActiveLayer;

            RefreshLayersList();
            OnDocumentModified();
        }

        public void DeleteSelectedLayers()
        {
            if (_doc == null || _doc.Layers.Count <= 1) return;
            CommitParentBrushSession();
            var selected = SelectedLayers;
            if (selected.Count == 0) return;

            if (selected.Count >= _doc.Layers.Count)
            {
                MessageBox.Show("Không thể xoá tất cả các layer. Tài liệu phải chứa ít nhất một layer.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cmd = new DeleteLayersCommand(_doc, selected);
            ExecuteHistoryAction(() => _doc.History.Execute(cmd));

            // Sync _lastActiveLayer vì ActiveLayerChanged bị suppress
            _lastActiveLayer = _doc.ActiveLayer;

            RefreshLayersList();
            OnDocumentModified();
        }

        public void MergeSelectedLayers()
        {
            if (_doc == null) return;
            CommitParentBrushSession();

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
            CommitParentBrushSession();
            ExecuteHistoryAction(() => _doc.History.Undo());
            RefreshLayersList();
            OnDocumentModified();
        }

        public void RedoAction()
        {
            if (_doc == null) return;
            CommitParentBrushSession();
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

                // Reset all popup buttons
                PopupBtnAI.Visibility = Visibility.Collapsed;
                PopupBtnMerge.Visibility = Visibility.Collapsed;
                PopupBtnDuplicate.Visibility = Visibility.Collapsed;
                PopupBtnDelete.Visibility = Visibility.Collapsed;

                if (layer.IsChildLayer)
                {
                    // Child/variant layer: only show Duplicate + Delete (no AI)
                    PopupBtnDuplicate.Visibility = Visibility.Visible;
                    PopupBtnDelete.Visibility = Visibility.Visible;
                }
                else
                {
                    // Standard layer(s)
                    PopupBtnDelete.Visibility = Visibility.Visible;

                    if (selectedCount == 1)
                    {
                        PopupBtnAI.Visibility = Visibility.Visible;
                    }
                    else if (selectedCount > 1)
                    {
                        PopupBtnMerge.Visibility = Visibility.Visible;
                    }
                }

                LayerActionPopup.PlacementTarget = fe;
                LayerActionPopup.IsOpen = true;
                e.Handled = true;
            }
        }

        private void CollapseToggle_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is EditorLayer layer)
            {
                layer.IsChildrenCollapsed = !layer.IsChildrenCollapsed;
                RefreshLayersList();
                e.Handled = true;
            }
        }

        private void PopupBtnDuplicate_Click(object sender, MouseButtonEventArgs e)
        {
            LayerActionPopup.IsOpen = false;
            if (_doc == null) return;
            CommitParentBrushSession();

            var active = _doc.ActiveLayer;
            if (active == null) return;

            // Promote variant to standalone layer (same as BtnPromoteLayer_Click)
            var parent = active.ParentLayer ?? active;
            var promoted = active.Duplicate();
            promoted.ParentLayer = null;
            promoted.Name = _doc.GetNextLayerName();
            int idx = _doc.Layers.IndexOf(parent);
            _doc.Layers.Insert(idx + 1, promoted);
            _doc.ActiveLayer = promoted;
            _lastActiveLayer = promoted;

            RefreshLayersList();
            OnDocumentModified();
            e.Handled = true;
        }

        private void PopupBtnDelete_Click(object sender, MouseButtonEventArgs e)
        {
            LayerActionPopup.IsOpen = false;
            if (_doc == null) return;

            var active = _doc.ActiveLayer;
            if (active == null) return;

            if (active.ParentLayer != null) // Child layer
            {
                var parent = active.ParentLayer;
                parent.ChildLayers.Remove(active);

                // Notify HasChildren changed
                parent.OnPropertyChanged(nameof(EditorLayer.HasChildren));

                // Select parent or another child
                if (parent.ChildLayers.Count > 0)
                {
                    parent.ActiveChildLayer = parent.ChildLayers.Last();
                    _doc.ActiveLayer = parent.ActiveChildLayer;
                }
                else
                {
                    parent.ActiveChildLayer = null;
                    _doc.ActiveLayer = parent;
                }
                _lastActiveLayer = _doc.ActiveLayer;

                RefreshLayersList();
                OnDocumentModified();
            }
            else // Standard layer(s)
            {
                DeleteSelectedLayers();
            }
            e.Handled = true;
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

        // ═══════ COLOR ADJUST (Photoshop-style, Per-Layer State) ═══════

        /// <summary>Per-layer color adjustment state — stores config + original snapshot.</summary>
        private class LayerColorState
        {
            public int Brightness, Contrast, Saturation, Hue;
            public List<Point>[] CurvePoints = new List<Point>[4];
            public bool CurveModified;
            public byte[]? OriginalPixels; // snapshot BEFORE any color adjustments

            public LayerColorState()
            {
                for (int ch = 0; ch < 4; ch++)
                    CurvePoints[ch] = new List<Point> { new Point(0, 0), new Point(255, 255) };
            }

            public bool HasSliderChanges => Brightness != 0 || Contrast != 0 || Saturation != 0 || Hue != 0;
            public bool HasAnyChanges => HasSliderChanges || CurveModified;
        }

        private readonly Dictionary<EditorLayer, LayerColorState> _layerColorStates = new();
        private EditorLayer? _colorAdjActiveLayer; // track which layer we're currently editing
        private byte[]? _colorAdjOriginalPixels;
        private bool _colorAdjDirty;
        private bool _colorAdjIsProcessing;
        private bool _colorAdjIsDragging;
        private bool _colorAdjSuppressSliderEvents;
        private bool _colorAdjBodyCollapsed = true;
        private DispatcherTimer? _colorAdjDebounceTimer;

        // Curves UI state
        private int _curveDragIndex = -1;
        private bool _curveDragDirty;
        private DispatcherTimer? _curveDebounceTimer;
        private readonly List<System.Windows.Shapes.Ellipse> _curvePointEllipses = new();
        private readonly List<System.Windows.Shapes.Rectangle> _histogramBars = new();

        private LayerColorState GetOrCreateState(EditorLayer layer)
        {
            if (!_layerColorStates.TryGetValue(layer, out var state))
            {
                state = new LayerColorState();
                _layerColorStates[layer] = state;
            }
            return state;
        }

        private void ColorAdjustHeader_Click(object sender, MouseButtonEventArgs e)
        {
            _colorAdjBodyCollapsed = !_colorAdjBodyCollapsed;
            ColorAdjustBody.Visibility = _colorAdjBodyCollapsed ? Visibility.Collapsed : Visibility.Visible;
            ColorAdjustCollapseIcon.Text = _colorAdjBodyCollapsed ? "▶" : "▼";

            if (!_colorAdjBodyCollapsed)
            {
                BeginEditingLayer(_doc?.ActiveLayer);
            }
            else
            {
                // Collapse: auto-apply pending changes
                AutoApplyPendingChanges();
            }
            e.Handled = true;
        }

        private void ColorAdjustTab_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tabName)
            {
                bool isSliders = tabName == "Sliders";
                ColorAdjustTabSliders.Visibility = isSliders ? Visibility.Visible : Visibility.Collapsed;
                ColorAdjustTabCurves.Visibility = isSliders ? Visibility.Collapsed : Visibility.Visible;

                TabHeaderSliders.Background = isSliders
                    ? (Brush)FindResource("ipHover") : (Brush)FindResource("ipSurface");
                TabHeaderCurves.Background = isSliders
                    ? (Brush)FindResource("ipSurface") : (Brush)FindResource("ipHover");

                var slidersText = (TextBlock)TabHeaderSliders.Child;
                var curvesText = (TextBlock)TabHeaderCurves.Child;
                slidersText.FontWeight = isSliders ? FontWeights.Bold : FontWeights.Normal;
                slidersText.Foreground = isSliders
                    ? (Brush)FindResource("ipAccent2") : (Brush)FindResource("ipMuted");
                curvesText.FontWeight = isSliders ? FontWeights.Normal : FontWeights.Bold;
                curvesText.Foreground = isSliders
                    ? (Brush)FindResource("ipMuted") : (Brush)FindResource("ipAccent2");

                if (!isSliders)
                {
                    UpdateHistogram();
                    RedrawCurve();
                }
                e.Handled = true;
            }
        }

        /// <summary>Called when active layer changes — save old state, load new.</summary>
        private void SyncColorAdjustPanel()
        {
            if (_colorAdjBodyCollapsed) return;

            // 1. Save pending state to old layer (don't commit — preserve preview)
            if (_colorAdjActiveLayer != null)
            {
                SaveCurrentStateToLayer();
                // Store original pixels in the state so we can restore later
                var oldState = GetOrCreateState(_colorAdjActiveLayer);
                oldState.OriginalPixels = _colorAdjOriginalPixels;
            }

            // 2. Begin editing the new active layer
            BeginEditingLayer(_doc?.ActiveLayer);
        }

        private void BeginEditingLayer(EditorLayer? layer)
        {
            CommitParentBrushSession();
            _colorAdjActiveLayer = layer;
            if (layer == null)
            {
                _colorAdjOriginalPixels = null;
                SetSlidersFromState(new LayerColorState());
                RedrawCurve();
                return;
            }

            var state = GetOrCreateState(layer);

            // Use stored original pixels if we have them (switching back)
            if (state.OriginalPixels != null)
            {
                _colorAdjOriginalPixels = state.OriginalPixels;
            }
            else
            {
                // First time editing this layer — snapshot current pixels
                int stride = layer.Width * 4;
                _colorAdjOriginalPixels = new byte[stride * layer.Height];
                layer.Bitmap.CopyPixels(_colorAdjOriginalPixels, stride, 0);
                state.OriginalPixels = _colorAdjOriginalPixels;
            }

            // Load this layer's saved config into UI
            SetSlidersFromState(state);
            UpdateHistogram();
            RedrawCurve();
        }

        private void SetSlidersFromState(LayerColorState state)
        {
            _colorAdjSuppressSliderEvents = true;
            SliderBrightness.Value = state.Brightness;
            SliderContrast.Value = state.Contrast;
            SliderSaturation.Value = state.Saturation;
            SliderHue.Value = state.Hue;
            if (TxtBrightness != null) TxtBrightness.Text = $"{state.Brightness}";
            if (TxtContrast != null) TxtContrast.Text = $"{state.Contrast}";
            if (TxtSaturation != null) TxtSaturation.Text = $"{state.Saturation}";
            if (TxtHue != null) TxtHue.Text = $"{state.Hue}°";
            _colorAdjSuppressSliderEvents = false;
        }

        private void SaveCurrentStateToLayer()
        {
            if (_colorAdjActiveLayer == null) return;
            var state = GetOrCreateState(_colorAdjActiveLayer);
            state.Brightness = (int)SliderBrightness.Value;
            state.Contrast = (int)SliderContrast.Value;
            state.Saturation = (int)SliderSaturation.Value;
            state.Hue = (int)SliderHue.Value;
            // Curve points are saved in-place (shared reference)
        }

        /// <summary>Commit all pending layer states to history (called on panel collapse).</summary>
        private void AutoApplyPendingChanges()
        {
            // Commit ALL layers that have pending changes
            foreach (var kvp in _layerColorStates.ToList())
            {
                var layer = kvp.Key;
                var state = kvp.Value;
                if (!state.HasAnyChanges || state.OriginalPixels == null) continue;

                // Make sure the preview is fully applied to bitmap
                if (state.CurveModified)
                    ApplyCurveToLayerSync(layer, state.OriginalPixels);

                // Commit to history
                int stride = layer.Width * 4;
                var newPixels = new byte[stride * layer.Height];
                layer.Bitmap.CopyPixels(newPixels, stride, 0);

                var cmd = new ColorAdjustCommand(layer, state.OriginalPixels, newPixels);
                _doc?.History.Execute(cmd);
            }

            _layerColorStates.Clear();
            _colorAdjOriginalPixels = null;
            _colorAdjActiveLayer = null;
            OnDocumentModified();
        }

        private bool IsActiveLayerEditable()
        {
            if (_doc?.ActiveLayer == null) return false;
            return _doc.ActiveLayer.IsVisible && !_doc.ActiveLayer.IsLocked;
        }

        private void BtnResetColorAdjust_Click(object sender, RoutedEventArgs e)
        {
            if (_colorAdjOriginalPixels == null || _colorAdjActiveLayer == null) return;

            // Restore original pixels
            int stride = _colorAdjActiveLayer.Width * 4;
            _colorAdjActiveLayer.Bitmap.WritePixels(
                new Int32Rect(0, 0, _colorAdjActiveLayer.Width, _colorAdjActiveLayer.Height),
                _colorAdjOriginalPixels, stride, 0);
            _colorAdjActiveLayer.InvalidateThumbnail();

            // Reset state
            _layerColorStates.Remove(_colorAdjActiveLayer);
            var freshState = GetOrCreateState(_colorAdjActiveLayer);
            SetSlidersFromState(freshState);
            UpdateHistogram();
            RedrawCurve();
            OnDocumentModified();
        }

        private void ColorAdjustSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsActiveLayerEditable()) return;
            if (_colorAdjOriginalPixels == null && _colorAdjActiveLayer != null)
                BeginEditingLayer(_colorAdjActiveLayer);
            _colorAdjIsDragging = true;

            if (_colorAdjDebounceTimer == null)
            {
                _colorAdjDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                _colorAdjDebounceTimer.Tick += ColorAdjustDebounce_Tick;
            }
            _colorAdjDebounceTimer.Start();
        }

        private void ColorAdjustSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _colorAdjIsDragging = false;
            _colorAdjDebounceTimer?.Stop();
            if (_colorAdjDirty) { _colorAdjDirty = false; ApplyColorAdjustToLayer(); }
            SaveCurrentStateToLayer();
        }

        private void ColorAdjustSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_colorAdjSuppressSliderEvents) return;
            if (TxtBrightness != null) TxtBrightness.Text = $"{(int)SliderBrightness.Value}";
            if (TxtContrast != null) TxtContrast.Text = $"{(int)SliderContrast.Value}";
            if (TxtSaturation != null) TxtSaturation.Text = $"{(int)SliderSaturation.Value}";
            if (TxtHue != null) TxtHue.Text = $"{(int)SliderHue.Value}°";
            _colorAdjDirty = true;
            if (!_colorAdjIsDragging && _colorAdjOriginalPixels != null) ApplyColorAdjustToLayer();
        }

        private void ColorAdjustDebounce_Tick(object? sender, EventArgs e)
        {
            if (!_colorAdjDirty || _colorAdjIsProcessing) return;
            _colorAdjDirty = false;
            ApplyColorAdjustToLayer();
        }

        private async void ApplyColorAdjustToLayer()
        {
            if (_colorAdjOriginalPixels == null || _colorAdjActiveLayer == null || _colorAdjIsProcessing) return;
            if (!IsActiveLayerEditable()) return;

            var layer = _colorAdjActiveLayer;
            int w = layer.Width, h = layer.Height, stride = w * 4;
            int bright = (int)SliderBrightness.Value, contrast = (int)SliderContrast.Value;
            int saturation = (int)SliderSaturation.Value, hue = (int)SliderHue.Value;

            if (bright == 0 && contrast == 0 && saturation == 0 && hue == 0)
            {
                layer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), _colorAdjOriginalPixels, stride, 0);
                layer.InvalidateThumbnail(); OnDocumentModified(); return;
            }

            var original = _colorAdjOriginalPixels;
            _colorAdjIsProcessing = true;
            byte[]? result = null;
            try
            {
                result = await Task.Run(() =>
                {
                    var output = new byte[original.Length];
                    double contrastFactor = 1.0;
                    if (contrast != 0) { double c = contrast * 2.55; contrastFactor = (259.0 * (c + 255.0)) / (255.0 * (259.0 - c)); }
                    double satFactor = saturation >= 0 ? 1.0 + saturation / 50.0 : 1.0 + saturation / 100.0;
                    double hueShift = hue / 360.0;
                    bool needHueSat = saturation != 0 || hue != 0;

                    Parallel.For(0, h, y =>
                    {
                        int rowOff = y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            int i = rowOff + x * 4;
                            byte a = original[i + 3];
                            if (a == 0) { output[i] = 0; output[i + 1] = 0; output[i + 2] = 0; output[i + 3] = 0; continue; }
                            double bv = original[i], gv = original[i + 1], rv = original[i + 2];
                            if (bright != 0) { double bVal = bright * 2.55; rv += bVal; gv += bVal; bv += bVal; }
                            if (contrast != 0) { rv = contrastFactor * (rv - 128.0) + 128.0; gv = contrastFactor * (gv - 128.0) + 128.0; bv = contrastFactor * (bv - 128.0) + 128.0; }
                            if (needHueSat)
                            {
                                double rn = Clamp01(rv / 255.0), gn = Clamp01(gv / 255.0), bn = Clamp01(bv / 255.0);
                                RgbToHsl(rn, gn, bn, out double hh, out double ss, out double ll);
                                if (hue != 0) { hh += hueShift; if (hh > 1) hh -= 1; if (hh < 0) hh += 1; }
                                if (saturation != 0) { ss *= satFactor; if (ss > 1) ss = 1; if (ss < 0) ss = 0; }
                                HslToRgb(hh, ss, ll, out rn, out gn, out bn);
                                rv = rn * 255.0; gv = gn * 255.0; bv = bn * 255.0;
                            }
                            output[i] = ClampByte(bv); output[i + 1] = ClampByte(gv); output[i + 2] = ClampByte(rv); output[i + 3] = a;
                        }
                    });
                    return output;
                });
            }
            finally { _colorAdjIsProcessing = false; }

            if (result != null && _colorAdjActiveLayer == layer)
            {
                layer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), result, stride, 0);
                layer.InvalidateThumbnail(); OnDocumentModified();
            }
        }

        private void BtnApplyColorAdjust_Click(object sender, RoutedEventArgs e)
        {
            if (_colorAdjOriginalPixels == null || _colorAdjActiveLayer == null) return;
            if (!IsActiveLayerEditable()) return;

            SaveCurrentStateToLayer();
            var state = GetOrCreateState(_colorAdjActiveLayer);

            if (!state.HasAnyChanges) return;

            // Apply curves if on curves tab
            if (state.CurveModified)
                ApplyCurveToLayerSync(_colorAdjActiveLayer, _colorAdjOriginalPixels);

            int stride = _colorAdjActiveLayer.Width * 4;
            var newPixels = new byte[stride * _colorAdjActiveLayer.Height];
            _colorAdjActiveLayer.Bitmap.CopyPixels(newPixels, stride, 0);

            var cmd = new ColorAdjustCommand(_colorAdjActiveLayer, _colorAdjOriginalPixels, newPixels);
            _doc?.History.Execute(cmd);

            // Clear state and re-snapshot
            _layerColorStates.Remove(_colorAdjActiveLayer);
            BeginEditingLayer(_colorAdjActiveLayer);
            OnDocumentModified();
        }

        // ═══════ CURVES EDITOR ═══════

        private const double CURVE_SIZE = 180.0;

        private List<Point>[] CurrentCurvePoints
        {
            get
            {
                if (_colorAdjActiveLayer == null) return new List<Point>[4];
                return GetOrCreateState(_colorAdjActiveLayer).CurvePoints;
            }
        }

        private void UpdateHistogram()
        {
            if (_colorAdjOriginalPixels == null || CurvesCanvas == null) return;

            foreach (var bar in _histogramBars) CurvesCanvas.Children.Remove(bar);
            _histogramBars.Clear();

            int channel = CmbCurveChannel?.SelectedIndex ?? 0;
            var histogram = new int[256];

            for (int i = 0; i < _colorAdjOriginalPixels.Length; i += 4)
            {
                if (_colorAdjOriginalPixels[i + 3] == 0) continue;
                if (channel == 0)
                {
                    int lum = (int)(0.299 * _colorAdjOriginalPixels[i + 2] + 0.587 * _colorAdjOriginalPixels[i + 1] + 0.114 * _colorAdjOriginalPixels[i]);
                    histogram[Math.Min(lum, 255)]++;
                }
                else
                {
                    int offset = channel == 1 ? 2 : (channel == 2 ? 1 : 0);
                    histogram[_colorAdjOriginalPixels[i + offset]]++;
                }
            }

            int maxCount = 1;
            for (int i = 0; i < 256; i++) if (histogram[i] > maxCount) maxCount = histogram[i];
            double logMax = Math.Log(maxCount + 1);

            Brush barBrush = channel switch
            {
                1 => new SolidColorBrush(Color.FromArgb(60, 255, 80, 80)),
                2 => new SolidColorBrush(Color.FromArgb(60, 80, 255, 80)),
                3 => new SolidColorBrush(Color.FromArgb(60, 80, 130, 255)),
                _ => new SolidColorBrush(Color.FromArgb(40, 200, 200, 200))
            };

            double barWidth = CURVE_SIZE / 256.0;
            for (int i = 0; i < 256; i++)
            {
                if (histogram[i] == 0) continue;
                double barH = (Math.Log(histogram[i] + 1) / logMax) * CURVE_SIZE;
                var bar = new System.Windows.Shapes.Rectangle { Width = Math.Max(1, barWidth), Height = barH, Fill = barBrush, IsHitTestVisible = false };
                Canvas.SetLeft(bar, i * CURVE_SIZE / 255.0);
                Canvas.SetTop(bar, CURVE_SIZE - barH);
                CurvesCanvas.Children.Add(bar);
                _histogramBars.Add(bar);
            }
        }

        // Pre-cached brushes for curves (avoid GC on every redraw)
        private static readonly Brush[] _curveBrushes = {
            new SolidColorBrush(Color.FromRgb(79, 195, 247)),  // RGB
            new SolidColorBrush(Color.FromRgb(255, 100, 100)),  // Red
            new SolidColorBrush(Color.FromRgb(100, 255, 100)),  // Green
            new SolidColorBrush(Color.FromRgb(100, 160, 255))   // Blue
        };
        private PointCollection? _curvePolyPoints; // reusable collection
        private int _lastCurveChannel = -1;

        private void RedrawCurve()
        {
            if (CurvesCanvas == null || CurvePolyline == null) return;
            int channel = CmbCurveChannel?.SelectedIndex ?? 0;
            var pts = CurrentCurvePoints;
            if (pts[channel] == null || pts[channel].Count < 2) return;

            // Build LUT via Catmull-Rom spline
            var lut = BuildCurveLUT(pts[channel]);

            // Reuse PointCollection — only recreate if size changed
            if (_curvePolyPoints == null || _curvePolyPoints.Count != 256)
            {
                _curvePolyPoints = new PointCollection(256);
                for (int i = 0; i < 256; i++) _curvePolyPoints.Add(new Point());
                CurvePolyline.Points = _curvePolyPoints;
            }

            // Update existing points in-place (no allocation)
            for (int i = 0; i < 256; i++)
                _curvePolyPoints[i] = new Point(i * CURVE_SIZE / 255.0, CURVE_SIZE - (lut[i] * CURVE_SIZE / 255.0));

            // Set stroke only when channel changed
            if (_lastCurveChannel != channel)
            {
                CurvePolyline.Stroke = _curveBrushes[channel];
                _lastCurveChannel = channel;
            }

            // Ellipse pool: reuse existing, add/remove only when count changes
            var ctrlPts = pts[channel];
            int needed = ctrlPts.Count;
            int existing = _curvePointEllipses.Count;

            // Remove excess
            while (_curvePointEllipses.Count > needed)
            {
                var last = _curvePointEllipses[_curvePointEllipses.Count - 1];
                CurvesCanvas.Children.Remove(last);
                _curvePointEllipses.RemoveAt(_curvePointEllipses.Count - 1);
            }

            // Add missing
            while (_curvePointEllipses.Count < needed)
            {
                var ell = new System.Windows.Shapes.Ellipse
                {
                    Width = 8, Height = 8, Fill = Brushes.White,
                    Stroke = _curveBrushes[channel], StrokeThickness = 1.5,
                    Cursor = Cursors.Hand, IsHitTestVisible = false
                };
                CurvesCanvas.Children.Add(ell);
                _curvePointEllipses.Add(ell);
            }

            // Update positions + stroke color
            for (int i = 0; i < needed; i++)
            {
                double cx = ctrlPts[i].X * CURVE_SIZE / 255.0;
                double cy = CURVE_SIZE - (ctrlPts[i].Y * CURVE_SIZE / 255.0);
                Canvas.SetLeft(_curvePointEllipses[i], cx - 4);
                Canvas.SetTop(_curvePointEllipses[i], cy - 4);
                _curvePointEllipses[i].Stroke = _curveBrushes[channel];
            }
        }

        /// <summary>Build 256-entry LUT using Catmull-Rom cubic spline (Photoshop-style smooth curves).</summary>
        private byte[] BuildCurveLUT(List<Point> pts)
        {
            var lut = new byte[256];
            if (pts == null || pts.Count < 2) { for (int i = 0; i < 256; i++) lut[i] = (byte)i; return lut; }

            // Sort without LINQ allocation
            var sorted = new List<Point>(pts);
            sorted.Sort((a, b) => a.X.CompareTo(b.X));
            int n = sorted.Count;

            int seg = 0;
            for (int i = 0; i < 256; i++)
            {
                double x = i;
                // Advance segment
                while (seg < n - 2 && sorted[seg + 1].X < x) seg++;

                if (seg >= n - 1) { lut[i] = ClampByte(sorted[n - 1].Y); continue; }

                double x0 = sorted[seg].X, y0 = sorted[seg].Y;
                double x1 = sorted[seg + 1].X, y1 = sorted[seg + 1].Y;
                double dx = x1 - x0;
                double t = dx > 0.001 ? (x - x0) / dx : 0;

                // Catmull-Rom tangents
                double m0, m1;
                if (seg > 0)
                    m0 = 0.5 * ((y1 - sorted[seg - 1].Y) / (x1 - sorted[seg - 1].X)) * dx;
                else
                    m0 = (y1 - y0); // natural boundary

                if (seg + 2 < n)
                    m1 = 0.5 * ((sorted[seg + 2].Y - y0) / (sorted[seg + 2].X - x0)) * dx;
                else
                    m1 = (y1 - y0); // natural boundary

                // Hermite basis with Catmull-Rom tangents
                double t2 = t * t, t3 = t2 * t;
                double h00 = 2 * t3 - 3 * t2 + 1;
                double h10 = t3 - 2 * t2 + t;
                double h01 = -2 * t3 + 3 * t2;
                double h11 = t3 - t2;

                double y = h00 * y0 + h10 * m0 + h01 * y1 + h11 * m1;
                lut[i] = ClampByte(y);
            }
            return lut;
        }

        private void CurvesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsActiveLayerEditable() || _colorAdjActiveLayer == null) return;
            if (_colorAdjOriginalPixels == null) BeginEditingLayer(_colorAdjActiveLayer);

            Point pos = e.GetPosition(CurvesCanvas);
            int channel = CmbCurveChannel?.SelectedIndex ?? 0;
            var pts = CurrentCurvePoints;

            // Check if clicking near existing point
            for (int i = 0; i < pts[channel].Count; i++)
            {
                double px = pts[channel][i].X * CURVE_SIZE / 255.0;
                double py = CURVE_SIZE - (pts[channel][i].Y * CURVE_SIZE / 255.0);
                if (Math.Abs(px - pos.X) < 8 && Math.Abs(py - pos.Y) < 8)
                {
                    _curveDragIndex = i;
                    StartCurveDebounce();
                    CurvesCanvas.CaptureMouse();
                    e.Handled = true;
                    return;
                }
            }

            // Add new control point
            double iv = Math.Clamp(pos.X * 255.0 / CURVE_SIZE, 0, 255);
            double ov = Math.Clamp((CURVE_SIZE - pos.Y) * 255.0 / CURVE_SIZE, 0, 255);
            var newPt = new Point(iv, ov);
            pts[channel].Add(newPt);
            pts[channel] = pts[channel].OrderBy(p => p.X).ToList();
            GetOrCreateState(_colorAdjActiveLayer).CurvePoints[channel] = pts[channel];
            _curveDragIndex = pts[channel].IndexOf(newPt);
            GetOrCreateState(_colorAdjActiveLayer).CurveModified = true;
            RedrawCurve();
            _curveDragDirty = true;
            StartCurveDebounce();
            CurvesCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void StartCurveDebounce()
        {
            if (_curveDebounceTimer == null)
            {
                _curveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                _curveDebounceTimer.Tick += CurveDebounce_Tick;
            }
            _curveDebounceTimer.Start();
        }

        private void CurveDebounce_Tick(object? sender, EventArgs e)
        {
            if (!_curveDragDirty || _colorAdjIsProcessing) return;
            _curveDragDirty = false;
            ApplyCurveToLayerAsync();
        }

        private void CurvesCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point pos = e.GetPosition(CurvesCanvas);
            double iv = Math.Clamp(pos.X * 255.0 / CURVE_SIZE, 0, 255);
            double ov = Math.Clamp((CURVE_SIZE - pos.Y) * 255.0 / CURVE_SIZE, 0, 255);
            if (TxtCurveInput != null) TxtCurveInput.Text = $"{(int)iv}";
            if (TxtCurveOutput != null) TxtCurveOutput.Text = $"{(int)ov}";

            if (_curveDragIndex >= 0 && e.LeftButton == MouseButtonState.Pressed && _colorAdjActiveLayer != null)
            {
                int channel = CmbCurveChannel?.SelectedIndex ?? 0;
                var pts = CurrentCurvePoints;
                if (pts[channel] == null) return;
                if (_curveDragIndex == 0) iv = 0;
                else if (_curveDragIndex == pts[channel].Count - 1) iv = 255;
                pts[channel][_curveDragIndex] = new Point(iv, ov);
                GetOrCreateState(_colorAdjActiveLayer).CurveModified = true;
                RedrawCurve(); // UI update is instant (lightweight)
                _curveDragDirty = true; // pixel processing deferred to debounce timer
            }
        }

        private void CurvesCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _curveDebounceTimer?.Stop();
            // Final full-res apply
            if (_curveDragDirty) { _curveDragDirty = false; ApplyCurveToLayerAsync(); }
            _curveDragIndex = -1;
            CurvesCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void CurvesCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_colorAdjActiveLayer == null) return;
            Point pos = e.GetPosition(CurvesCanvas);
            int channel = CmbCurveChannel?.SelectedIndex ?? 0;
            var pts = CurrentCurvePoints;
            if (pts[channel] == null || pts[channel].Count <= 2) return;

            for (int i = 1; i < pts[channel].Count - 1; i++)
            {
                double px = pts[channel][i].X * CURVE_SIZE / 255.0;
                double py = CURVE_SIZE - (pts[channel][i].Y * CURVE_SIZE / 255.0);
                if (Math.Abs(px - pos.X) < 10 && Math.Abs(py - pos.Y) < 10)
                {
                    pts[channel].RemoveAt(i);
                    GetOrCreateState(_colorAdjActiveLayer).CurveModified = true;
                    RedrawCurve();
                    ApplyCurveToLayerAsync();
                    break;
                }
            }
            e.Handled = true;
        }

        private void CmbCurveChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateHistogram();
            RedrawCurve();
        }

        private void BtnResetCurve_Click(object sender, RoutedEventArgs e)
        {
            if (_colorAdjActiveLayer == null) return;
            int channel = CmbCurveChannel?.SelectedIndex ?? 0;
            var state = GetOrCreateState(_colorAdjActiveLayer);
            state.CurvePoints[channel] = new List<Point> { new Point(0, 0), new Point(255, 255) };
            state.CurveModified = false;
            RedrawCurve();

            // Restore original pixels
            if (_colorAdjOriginalPixels != null)
            {
                var layer = _colorAdjActiveLayer;
                layer.Bitmap.WritePixels(new Int32Rect(0, 0, layer.Width, layer.Height),
                    _colorAdjOriginalPixels, layer.Width * 4, 0);
                layer.InvalidateThumbnail();
                OnDocumentModified();
            }
        }

        /// <summary>Synchronous curve apply (for auto-apply on layer switch).</summary>
        private void ApplyCurveToLayerSync(EditorLayer layer, byte[] original)
        {
            var state = GetOrCreateState(layer);
            int w = layer.Width, h = layer.Height, stride = w * 4;
            var lutRgb = BuildCurveLUT(state.CurvePoints[0]);
            var lutR = BuildCurveLUT(state.CurvePoints[1]);
            var lutG = BuildCurveLUT(state.CurvePoints[2]);
            var lutB = BuildCurveLUT(state.CurvePoints[3]);

            bool rgbC = false, rC = false, gC = false, bC = false;
            for (int i = 0; i < 256; i++)
            {
                if (lutRgb[i] != i) rgbC = true;
                if (lutR[i] != i) rC = true;
                if (lutG[i] != i) gC = true;
                if (lutB[i] != i) bC = true;
            }
            if (!rgbC && !rC && !gC && !bC) return;

            var output = new byte[original.Length];
            Parallel.For(0, h, y =>
            {
                int rowOff = y * stride;
                for (int x = 0; x < w; x++)
                {
                    int i = rowOff + x * 4;
                    byte a = original[i + 3];
                    if (a == 0) { output[i] = 0; output[i + 1] = 0; output[i + 2] = 0; output[i + 3] = 0; continue; }
                    byte bVal = original[i], gVal = original[i + 1], rVal = original[i + 2];
                    if (rgbC) { rVal = lutRgb[rVal]; gVal = lutRgb[gVal]; bVal = lutRgb[bVal]; }
                    if (rC) rVal = lutR[rVal];
                    if (gC) gVal = lutG[gVal];
                    if (bC) bVal = lutB[bVal];
                    output[i] = bVal; output[i + 1] = gVal; output[i + 2] = rVal; output[i + 3] = a;
                }
            });
            layer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), output, stride, 0);
            layer.InvalidateThumbnail();
        }

        /// <summary>Async curve apply for realtime preview while dragging.</summary>
        private async void ApplyCurveToLayerAsync()
        {
            if (_colorAdjOriginalPixels == null || _colorAdjActiveLayer == null || _colorAdjIsProcessing) return;
            if (!IsActiveLayerEditable()) return;

            var layer = _colorAdjActiveLayer;
            var state = GetOrCreateState(layer);
            int w = layer.Width, h = layer.Height, stride = w * 4;
            var original = _colorAdjOriginalPixels;

            var lutRgb = BuildCurveLUT(state.CurvePoints[0]);
            var lutR = BuildCurveLUT(state.CurvePoints[1]);
            var lutG = BuildCurveLUT(state.CurvePoints[2]);
            var lutB = BuildCurveLUT(state.CurvePoints[3]);

            bool rgbC = false, rC = false, gC = false, bC = false;
            for (int i = 0; i < 256; i++)
            {
                if (lutRgb[i] != i) rgbC = true;
                if (lutR[i] != i) rC = true;
                if (lutG[i] != i) gC = true;
                if (lutB[i] != i) bC = true;
            }

            if (!rgbC && !rC && !gC && !bC)
            {
                layer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), original, stride, 0);
                layer.InvalidateThumbnail(); OnDocumentModified(); return;
            }

            _colorAdjIsProcessing = true;
            byte[]? result = null;
            try
            {
                result = await Task.Run(() =>
                {
                    var output = new byte[original.Length];
                    Parallel.For(0, h, y =>
                    {
                        int rowOff = y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            int i = rowOff + x * 4;
                            byte a = original[i + 3];
                            if (a == 0) { output[i] = 0; output[i + 1] = 0; output[i + 2] = 0; output[i + 3] = 0; continue; }
                            byte bVal = original[i], gVal = original[i + 1], rVal = original[i + 2];
                            if (rgbC) { rVal = lutRgb[rVal]; gVal = lutRgb[gVal]; bVal = lutRgb[bVal]; }
                            if (rC) rVal = lutR[rVal];
                            if (gC) gVal = lutG[gVal];
                            if (bC) bVal = lutB[bVal];
                            output[i] = bVal; output[i + 1] = gVal; output[i + 2] = rVal; output[i + 3] = a;
                        }
                    });
                    return output;
                });
            }
            finally { _colorAdjIsProcessing = false; }

            if (result != null && _colorAdjActiveLayer == layer)
            {
                layer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), result, stride, 0);
                layer.InvalidateThumbnail(); OnDocumentModified();
            }
        }

        // ═══ Color Math Helpers ═══

        private static byte ClampByte(double v) => v <= 0 ? (byte)0 : v >= 255 ? (byte)255 : (byte)(v + 0.5);
        private static double Clamp01(double v) => v <= 0 ? 0 : v >= 1 ? 1 : v;

        private static void RgbToHsl(double r, double g, double b, out double h, out double s, out double l)
        {
            double max = r > g ? (r > b ? r : b) : (g > b ? g : b);
            double min = r < g ? (r < b ? r : b) : (g < b ? g : b);
            double delta = max - min;
            l = (max + min) / 2.0;
            if (delta < 1e-10) { h = 0; s = 0; return; }
            s = l < 0.5 ? delta / (max + min) : delta / (2.0 - max - min);
            if (max == r) h = (g - b) / delta + (g < b ? 6.0 : 0.0);
            else if (max == g) h = (b - r) / delta + 2.0;
            else h = (r - g) / delta + 4.0;
            h /= 6.0;
        }

        private static void HslToRgb(double h, double s, double l, out double r, out double g, out double b)
        {
            if (s < 1e-10) { r = g = b = l; return; }
            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1.0; if (t > 1) t -= 1.0;
            if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            return p;
        }

        private ImageProcessingNodeContentControl? FindParentControl()
        {
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null && parent is not ImageProcessingNodeContentControl)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as ImageProcessingNodeContentControl;
        }

        private void CommitParentBrushSession()
        {
            FindParentControl()?.CommitBrushDrawingSession();
        }
    }
}

