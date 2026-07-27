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
using SkiaSharp;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl
    {
        private void SwitchToMode(Models.Nodes.ImageProcessingMode mode)
        {
            // Luôn cưỡng ép chạy ở chế độ chỉnh sửa thủ công (Editor/Manual) vì cơ chế AI đã chuyển sang LayerAiDialog
            _node.ProcessingMode = Models.Nodes.ImageProcessingMode.Manual;

            // Ẩn AI panels, hiện Editor
            // RightMenuBorder.Visibility = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Visible;
            // LeftMenuBorder.Visibility = Visibility.Collapsed;
            EditorToolbox.Visibility = Visibility.Visible;

            // Tắt cột Image Processor nếu đang mở
            if (_ipColumnVisible)
            {
                ToggleIPColumn();
            }

            // Tạo EditorDocument nếu chưa có
            EnsureEditorDocument();

            // Sync toolbox visual state với active tool
            SyncToolboxHighlight();
            SyncToolboxColors();
            SyncModeButtonStyles();
        }

        private void EnsureEditorDocument()
        {
            ClearSelection();
            var imgSource = MainImage.Source as BitmapSource;

            if (_node.EditorDoc != null)
            {
                if (imgSource != null)
                {
                    // Kiểm tra kích thước: nếu ảnh khác doc → tạo lại từ ảnh thực
                    if (_node.EditorDoc.Width != imgSource.PixelWidth
                        || _node.EditorDoc.Height != imgSource.PixelHeight)
                    {
                        _node.EditorDoc = Models.ImageEditor.EditorDocument.FromBitmapSource(imgSource);
                    }
                    // Nếu cùng kích thước → giữ nguyên mọi layer, KHÔNG overwrite.
                    // Pixel data đã được user chỉnh sửa trên tất cả layers, phải bảo toàn.
                }
                EditorPanel.SetDocument(_node.EditorDoc);
                _node.EditorDoc.PropertyChanged -= EditorDoc_PropertyChanged;
                _node.EditorDoc.PropertyChanged += EditorDoc_PropertyChanged;
                // Re-composite để canvas hiển thị composite mới nhất
                OnEditorDocumentModified();
                return;
            }

            // Tạo document từ ảnh hiện tại (nếu có) hoặc tạo blank
            if (imgSource != null)
            {
                _node.EditorDoc = Models.ImageEditor.EditorDocument.FromBitmapSource(imgSource);
            }
            else
            {
                _node.EditorDoc = Models.ImageEditor.EditorDocument.CreateBlank(720, 1080);
            }

            EditorPanel.SetDocument(_node.EditorDoc);
            _node.EditorDoc.PropertyChanged -= EditorDoc_PropertyChanged;
            _node.EditorDoc.PropertyChanged += EditorDoc_PropertyChanged;
        }

        private bool _compositeScheduled;

        private void OnEditorDocumentModified()
        {
            if (_node.EditorDoc == null) return;

            // Always ensure PropertyChanged handler is registered on the active document and swatches are synced
            _node.EditorDoc.PropertyChanged -= EditorDoc_PropertyChanged;
            _node.EditorDoc.PropertyChanged += EditorDoc_PropertyChanged;
            SyncToolboxColors();

            // Coalesce: nếu đã có schedule thì không cần thêm
            if (_compositeScheduled) return;
            _compositeScheduled = true;

            // Defer composite sang cuối vòng lặp Dispatcher để không block UI
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {
                _compositeScheduled = false;
                if (_node.EditorDoc == null) return;

                try
                {
                    var composite = _node.EditorDoc.Composite();
                    MainImage.Source = composite;
                    MainImage.Width = _node.EditorDoc.Width;
                    MainImage.Height = _node.EditorDoc.Height;
                }
                catch (Exception ex)
                {
                    try
                    {
                        System.IO.File.WriteAllText(@"d:\_DuAn\App_Desktop\workflows\Flow-My\composite_error.txt", "Composite error:\n" + ex.ToString());
                    }
                    catch { }
                }
                finally
                {
                    // Hide active layer drawing overlay seamlessly after composite rendering has completed
                    ActiveLayerDrawingOverlay.Visibility = Visibility.Collapsed;
                    ActiveLayerDrawingOverlay.Source = null;

                    // Clean up and dispose cached plates for brush/eraser drawing session
                    _node.EditorDoc.IsDrawingSessionActive = false;
                    _node.EditorDoc.CachedBgPlate?.Dispose();
                    _node.EditorDoc.CachedBgPlate = null;
                    _node.EditorDoc.CachedFgPlate?.Dispose();
                    _node.EditorDoc.CachedFgPlate = null;
                }

                try
                {
                    UpdateTransformOverlayDisplay();
                }
                catch (Exception ex)
                {
                    try
                    {
                        System.IO.File.WriteAllText(@"d:\_DuAn\App_Desktop\workflows\Flow-My\composite_error.txt", "UpdateOverlay error:\n" + ex.ToString());
                    }
                    catch { }
                }
            }));
        }

        private void SyncModeButtonStyles()
        {
            bool isAI = _node.ProcessingMode == Models.Nodes.ImageProcessingMode.AI;

            // Active: accent bg + white text; Inactive: dark + muted
            var activeBg = new SolidColorBrush(Color.FromArgb(0x40, 0x4f, 0xff, 0xb0));
            var activeFg = new SolidColorBrush(Color.FromRgb(0xdd, 0xe3, 0xef));
            var inactiveBg = new SolidColorBrush(Color.FromArgb(0x18, 0xff, 0xff, 0xff));
            var inactiveFg = new SolidColorBrush(Color.FromRgb(0x5a, 0x60, 0x72));

            // BtnModeAI.Background = isAI ? activeBg : inactiveBg;
            // BtnModeAI.Foreground = isAI ? activeFg : inactiveFg;
            // BtnModeEditor.Background = isAI ? inactiveBg : activeBg;
            // BtnModeEditor.Foreground = isAI ? inactiveFg : activeFg;

            // AI mode có IP toggle, Editor mode ẩn nó
            IpToggleButton.Visibility = isAI ? Visibility.Visible : Visibility.Collapsed;
        }


        private readonly Dictionary<string, Border> _toolboxBorders = new();

        private void InitToolboxBorders()
        {
            _toolboxBorders["Brush"] = TbxBrush;
            _toolboxBorders["Eraser"] = TbxEraser;
            _toolboxBorders["Fill"] = TbxFill;
            _toolboxBorders["Eyedropper"] = TbxEyedropper;
            _toolboxBorders["Move"] = TbxMove;
            _toolboxBorders["Text"] = TbxText;
            _toolboxBorders["Selection"] = TbxSelectionActive;
            _toolboxBorders["Lasso"] = TbxSelectionActive;
            _toolboxBorders["PolyLasso"] = TbxSelectionActive;
            _toolboxBorders["MagicWand"] = TbxSmartSelectionActive;
            _toolboxBorders["QuickSelection"] = TbxSmartSelectionActive;
            _toolboxBorders["ObjectSelection"] = TbxSmartSelectionActive;
            _toolboxBorders["CropCanvas"] = TbxCropActive;
            _toolboxBorders["Slice"] = TbxCropActive;
            _toolboxBorders["SliceSelect"] = TbxCropActive;
            _toolboxBorders["Transform"] = TbxCropActive;
        }

        private void EditorToolbox_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string toolName)
            {
                CommitKeyMoveSession();

                // If switching away from Text tool, commit active text editing!
                if (toolName != "Text" && _editingTextLayer != null)
                {
                    CommitActiveText();
                }

                // Delegate to EditorPanel's tool selection (keeps both in sync)
                EditorPanel.SelectToolByName(toolName);
                SyncToolboxHighlight();

                // If switching to Text tool, and the active layer is a text layer, enter editing mode!
                if (toolName == "Text" && _node.EditorDoc != null && _node.EditorDoc.ActiveLayer != null && _node.EditorDoc.ActiveLayer.IsTextLayer)
                {
                    EnterTextEditingMode(_node.EditorDoc.ActiveLayer);
                }

                e.Handled = true;
            }
        }

        private void EditorToolbox_FgColor_Click(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var color = PickColorDialog(_node.EditorDoc.ForegroundColor);
            if (color.HasValue)
            {
                _node.EditorDoc.ForegroundColor = color.Value;
                SyncToolboxColors();
            }
            e.Handled = true;
        }

        private void EditorToolbox_BgColor_Click(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var color = PickColorDialog(_node.EditorDoc.BackgroundColor);
            if (color.HasValue)
            {
                _node.EditorDoc.BackgroundColor = color.Value;
                SyncToolboxColors();
            }
            e.Handled = true;
        }

        private static Color? PickColorDialog(Color initial)
        {
            using var dlg = new WinForms.ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B),
                FullOpen = true,
                AnyColor = true
            };
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                var c = dlg.Color;
                return Color.FromArgb(c.A, c.R, c.G, c.B);
            }
            return null;
        }

        private void SyncToolboxHighlight()
        {
            if (_toolboxBorders.Count == 0) InitToolboxBorders();

            string activeTool = EditorPanel.ActiveToolName;

            // Auto-commit transform session if switching away from Transform tool
            if (activeTool != "Transform" && _transformSessionActive)
            {
                CommitTransformSession();
            }

            var activeBg = new SolidColorBrush(Color.FromArgb(0x30, 0x4f, 0xff, 0xb0));
            var activeBorder = new SolidColorBrush(Color.FromRgb(0x4f, 0xff, 0xb0));

            // 1. Clear all highlights first
            foreach (var border in _toolboxBorders.Values)
            {
                border.Background = Brushes.Transparent;
                border.BorderBrush = Brushes.Transparent;
            }

            // 2. Set active highlight
            if (_toolboxBorders.TryGetValue(activeTool, out var activeBorderElement) && activeBorderElement != null)
            {
                activeBorderElement.Background = activeBg;
                activeBorderElement.BorderBrush = activeBorder;
            }

            EditorPanel.UpdatePanelVisibilities(activeTool);
            UpdateTopOptionsBar(activeTool);

            if (activeTool != "Text" && TextMoveContainer != null && TextMoveContainer.Visibility == Visibility.Visible)
            {
                CommitActiveText();
            }

            UpdatePolygonDisplay();
            UpdateTransformOverlayDisplay(); // Ensure transform bounding box updates immediately when active tool changes!

            if (activeTool == "Brush" || activeTool == "Eraser")
            {
                MainScrollViewer?.Focus();
            }
        }

        private void SyncToolboxColors()
        {
            if (_node.EditorDoc == null) return;

            var fgBrush = new SolidColorBrush(_node.EditorDoc.ForegroundColor);
            var bgBrush = new SolidColorBrush(_node.EditorDoc.BackgroundColor);

            TbxFgColor.Background = fgBrush;
            TbxBgColor.Background = bgBrush;

            if (OptFgColorSwatch != null)
                OptFgColorSwatch.Background = fgBrush;
            if (OptBgColorSwatch != null)
                OptBgColorSwatch.Background = bgBrush;

            string hex = $"#{_node.EditorDoc.ForegroundColor.R:X2}{_node.EditorDoc.ForegroundColor.G:X2}{_node.EditorDoc.ForegroundColor.B:X2}";
            if (OptColorHexInput != null && OptColorHexInput.Text != hex)
            {
                OptColorHexInput.Text = hex;
            }
        }

        private void OptColorHexInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyColorFromHexInput();
        }

        private void OptColorHexInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyColorFromHexInput();
                MainScrollViewer.Focus();
                e.Handled = true;
            }
        }

        private void ApplyColorFromHexInput()
        {
            if (_node.EditorDoc == null || OptColorHexInput == null) return;
            try
            {
                var text = OptColorHexInput.Text.Trim();
                if (string.IsNullOrEmpty(text)) return;
                if (!text.StartsWith("#")) text = "#" + text;
                var color = (Color)ColorConverter.ConvertFromString(text);
                if (_node.EditorDoc.ForegroundColor != color)
                {
                    _node.EditorDoc.ForegroundColor = color;
                }
            }
            catch { /* ignore invalid hex format */ }
        }

        private void OptFgColorSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var color = PickColorDialog(_node.EditorDoc.ForegroundColor);
            if (color.HasValue)
            {
                _node.EditorDoc.ForegroundColor = color.Value;
            }
            e.Handled = true;
        }

        private void OptBgColorSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var color = PickColorDialog(_node.EditorDoc.BackgroundColor);
            if (color.HasValue)
            {
                _node.EditorDoc.BackgroundColor = color.Value;
            }
            e.Handled = true;
        }

        private void EditorToolbox_SwapColors_Click(object sender, RoutedEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var temp = _node.EditorDoc.ForegroundColor;
            _node.EditorDoc.ForegroundColor = _node.EditorDoc.BackgroundColor;
            _node.EditorDoc.BackgroundColor = temp;
        }

        private void EditorDoc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Models.ImageEditor.EditorDocument.ForegroundColor) ||
                e.PropertyName == nameof(Models.ImageEditor.EditorDocument.BackgroundColor))
            {
                SyncToolboxColors();
            }
        }
    }
}
