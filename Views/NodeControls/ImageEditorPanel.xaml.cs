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
        private readonly Dictionary<string, Border> _toolBorders = new();

        public ImageEditorPanel()
        {
            InitializeComponent();
            Loaded += (_, _) => CacheToolBorders();
        }

        private void CacheToolBorders()
        {
            _toolBorders["Brush"] = ToolBrush;
            _toolBorders["Eraser"] = ToolEraser;
            _toolBorders["Fill"] = ToolFill;
            _toolBorders["Eyedropper"] = ToolEyedropper;
            _toolBorders["Move"] = ToolMove;
            _toolBorders["Text"] = ToolText;
            _toolBorders["Selection"] = ToolSelection;
        }

        /// <summary>Tool name hiện tại (Brush, Eraser, Fill, etc.).</summary>
        public string ActiveToolName => _activeTool;

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
            LayersList.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<EditorLayer>(
                _doc.Layers.Reverse());
        }

        private void OnLayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshLayersList();
        }

        private void OnDocPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorDocument.ActiveLayer))
            {
                SyncActiveLayerOpacity();
                SyncBlendModeCombo();
            }
            else if (e.PropertyName == nameof(EditorDocument.ForegroundColor) ||
                     e.PropertyName == nameof(EditorDocument.BackgroundColor))
            {
                SyncColorDisplay();
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
                SyncActiveLayerOpacity();
                SyncBlendModeCombo();
            }
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

            var newLayer = new EditorLayer(_doc.Width, _doc.Height, $"Layer {_doc.Layers.Count + 1}");
            var cmd = new LayerAddCommand(_doc, newLayer, insertIndex);
            _doc.History.Execute(cmd);
            OnDocumentModified();
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

        private void ToolBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string toolName)
            {
                SelectTool(toolName);
                e.Handled = true;
            }
        }

        private void SelectTool(string toolName)
        {
            _activeTool = toolName;

            // Update visual state — active = accent bg, inactive = transparent
            var activeBg = new SolidColorBrush(Color.FromArgb(0x30, 0x4f, 0xff, 0xb0)); // ipAccent 20%
            var activeBorder = new SolidColorBrush(Color.FromRgb(0x4f, 0xff, 0xb0));    // ipAccent

            foreach (var (name, border) in _toolBorders)
            {
                if (name == toolName)
                {
                    border.Background = activeBg;
                    border.BorderBrush = activeBorder;
                    border.BorderThickness = new Thickness(1.5);
                }
                else
                {
                    border.Background = Brushes.Transparent;
                    border.BorderBrush = Brushes.Transparent;
                    border.BorderThickness = new Thickness(1.5);
                }
            }
        }

        private void SliderBrushSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrushSize == null) return;
            TxtBrushSize.Text = $"{(int)SliderBrushSize.Value}";
        }

        private void SliderBrushHardness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrushHardness == null) return;
            TxtBrushHardness.Text = $"{(int)SliderBrushHardness.Value}%";
        }

        private void SliderBrushFlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrushFlow == null) return;
            TxtBrushFlow.Text = $"{(int)SliderBrushFlow.Value}%";
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
