// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
        private bool _isDraggingTextContainer = false;
        private Point _textDragStartMousePos;
        private Thickness _textDragStartMargin;

        private void EditorPanel_BrushPropertiesChanged(object? sender, EventArgs e)
        {
            SyncFromEditorPanelBrushProperties();
            UpdateBrushCursorPosition();
        }

        private void OptTextSizeInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyDirectTextSize();
        }

        private void OptTextSizeInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyDirectTextSize();
                e.Handled = true;
            }
        }

        private void ApplyDirectTextSize()
        {
            if (OptTextSizeInput == null || EditorPanel == null || EditorPanel.SliderTextFontSize == null) return;
            if (double.TryParse(OptTextSizeInput.Text, out double size))
            {
                size = Math.Clamp(size, 6, 200);
                if (EditorPanel.SliderTextFontSize.Value != size)
                {
                    EditorPanel.SliderTextFontSize.Value = size;
                }
            }
            OptTextSizeInput.Text = $"{(int)EditorPanel.TextFontSize}";
        }

        private void TextAlign_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string align)
            {
                if (EditorPanel != null)
                {
                    EditorPanel.TextAlignment = align;
                }
            }
            e.Handled = true;
        }

        private void SyncTopTextAlignButtons()
        {
            if (BtnTextAlignLeft == null || BtnTextAlignCenter == null || BtnTextAlignRight == null || EditorPanel == null) return;
            
            var activeBg = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0xcf, 0xff));
            var activeBorder = new SolidColorBrush(Color.FromRgb(0x00, 0xcf, 0xff));
            var normalBorder = new SolidColorBrush(Color.FromRgb(0x35, 0x39, 0x45));

            BtnTextAlignLeft.Background = Brushes.Transparent;
            BtnTextAlignLeft.BorderBrush = normalBorder;
            BtnTextAlignCenter.Background = Brushes.Transparent;
            BtnTextAlignCenter.BorderBrush = normalBorder;
            BtnTextAlignRight.Background = Brushes.Transparent;
            BtnTextAlignRight.BorderBrush = normalBorder;

            string align = EditorPanel.TextAlignment;
            if (align == "Left")
            {
                BtnTextAlignLeft.Background = activeBg;
                BtnTextAlignLeft.BorderBrush = activeBorder;
            }
            else if (align == "Center")
            {
                BtnTextAlignCenter.Background = activeBg;
                BtnTextAlignCenter.BorderBrush = activeBorder;
            }
            else if (align == "Right")
            {
                BtnTextAlignRight.Background = activeBg;
                BtnTextAlignRight.BorderBrush = activeBorder;
            }
        }

        private void OptTextColorSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OptBtnTextColor_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }

        private void OptTextSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (EditorPanel != null && EditorPanel.SliderTextFontSize != null && EditorPanel.SliderTextFontSize.Value != e.NewValue)
                EditorPanel.SliderTextFontSize.Value = e.NewValue;
        }

        private void OptFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OptFontFamily == null || OptFontFamily.SelectedItem is not ComboBoxItem item) return;
            string family = item.Content.ToString()!;
            if (EditorPanel != null && EditorPanel.CmbFontFamily != null)
            {
                foreach (ComboBoxItem rightItem in EditorPanel.CmbFontFamily.Items)
                {
                    if (rightItem.Content.ToString() == family)
                    {
                        if (EditorPanel.CmbFontFamily.SelectedItem != rightItem)
                            EditorPanel.CmbFontFamily.SelectedItem = rightItem;
                        break;
                    }
                }
            }
        }

        private void OptFontStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OptFontStyle == null || OptFontStyle.SelectedItem is not ComboBoxItem item) return;
            string style = item.Content.ToString()!;
            if (EditorPanel != null && EditorPanel.CmbFontStyle != null)
            {
                foreach (ComboBoxItem rightItem in EditorPanel.CmbFontStyle.Items)
                {
                    if (rightItem.Content.ToString() == style)
                    {
                        if (EditorPanel.CmbFontStyle.SelectedItem != rightItem)
                            EditorPanel.CmbFontStyle.SelectedItem = rightItem;
                        break;
                    }
                }
            }
        }

        private void OptBtnTextColor_Click(object sender, EventArgs e)
        {
            if (EditorPanel != null && EditorPanel.BtnTextColor != null)
                EditorPanel.BtnTextColor.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private void OptDeselect_Click(object sender, RoutedEventArgs e) => ClearSelection();
        private void OptCopy_Click(object sender, RoutedEventArgs e) => CopyActiveSelection();
        private void OptPaste_Click(object sender, RoutedEventArgs e) => PasteSelectionAsLayer();
        private void OptDelete_Click(object sender, RoutedEventArgs e) => DeleteSelectionContent();

        private void EditorPanel_TextPropertiesChanged(object? sender, EventArgs e)
        {
            if (TextMoveContainer.Visibility == Visibility.Visible && _node.EditorDoc != null)
            {
                var activeLayer = _node.EditorDoc.ActiveLayer;
                if (activeLayer != null && activeLayer.IsTextLayer)
                {
                    TextEditorBox.FontSize = EditorPanel.TextFontSize;
                    TextEditorBox.Foreground = new SolidColorBrush(EditorPanel.TextColor);
                    TextEditorBox.CaretBrush = TextEditorBox.Foreground;
                    TextEditorBox.FontFamily = new FontFamily(EditorPanel.TextFontFamily);
                    TextEditorBox.FontWeight = EditorPanel.TextFontStyle == "Bold" ? FontWeights.Bold : FontWeights.Normal;
                    TextEditorBox.FontStyle = EditorPanel.TextFontStyle == "Italic" ? FontStyles.Italic : FontStyles.Normal;
                    TextEditorBox.TextAlignment = EditorPanel.TextAlignment == "Center" ? TextAlignment.Center : 
                                                 EditorPanel.TextAlignment == "Right" ? TextAlignment.Right : TextAlignment.Left;
                }
            }

            if (OptTextSize != null && OptTextSize.Value != EditorPanel.TextFontSize)
                OptTextSize.Value = EditorPanel.TextFontSize;
            if (OptTextSizeInput != null && OptTextSizeInput.Text != $"{(int)EditorPanel.TextFontSize}")
                OptTextSizeInput.Text = $"{(int)EditorPanel.TextFontSize}";
            if (OptTextColorSwatch != null)
                OptTextColorSwatch.Background = new SolidColorBrush(EditorPanel.TextColor);
            if (OptBtnTextColor != null)
                OptBtnTextColor.Text = $"#{EditorPanel.TextColor.R:X2}{EditorPanel.TextColor.G:X2}{EditorPanel.TextColor.B:X2}";

            if (OptFontFamily != null)
            {
                foreach (ComboBoxItem item in OptFontFamily.Items)
                {
                    if (item.Content.ToString() == EditorPanel.TextFontFamily)
                    {
                        if (OptFontFamily.SelectedItem != item)
                            OptFontFamily.SelectedItem = item;
                        break;
                    }
                }
            }
            if (OptFontStyle != null)
            {
                foreach (ComboBoxItem item in OptFontStyle.Items)
                {
                    if (item.Content.ToString() == EditorPanel.TextFontStyle)
                    {
                        if (OptFontStyle.SelectedItem != item)
                            OptFontStyle.SelectedItem = item;
                        break;
                    }
                }
            }
            SyncTopTextAlignButtons();
        }

        private void EditorPanel_ActiveLayerChanged(object? sender, EventArgs e)
        {
            CommitPendingMoveTranslation();
            CommitBrushDrawingSession();
            
            // Auto commit transform session on active layer change!
            if (_transformSessionActive)
            {
                CommitTransformSession();
            }

            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;

            // Commit the previously editing text layer if switching away from it!
            if (_editingTextLayer != null && _editingTextLayer != activeLayer)
            {
                CommitActiveText();
            }

            if (activeLayer != null && activeLayer.IsTextLayer && EditorPanel.ActiveToolName == "Text")
            {
                EnterTextEditingMode(activeLayer);
            }
            else
            {
                TextMoveContainer.Visibility = Visibility.Collapsed;
            }

            UpdateTransformOverlayDisplay();
        }

        private void EnterTextEditingMode(EditorLayer activeLayer)
        {
            if (activeLayer == null || !activeLayer.IsTextLayer) return;

            _editingTextLayer = activeLayer;

            // Sync side panel inputs to match this text layer
            EditorPanel.SetTextProperties(
                activeLayer.TextFontSize,
                activeLayer.TextColor,
                activeLayer.TextFontFamily,
                activeLayer.TextFontStyle,
                activeLayer.TextAlignment
            );

            // Initialize overlay bounding box
            TextMoveContainer.Margin = new Thickness(activeLayer.TextX, activeLayer.TextY, 0, 0);
            TextBoundingBorder.Width = activeLayer.TextWidth;
            TextBoundingBorder.Height = activeLayer.TextHeight;
            TextEditorBox.Text = activeLayer.TextContent;

            // Sync overlay look
            TextEditorBox.FontSize = activeLayer.TextFontSize;
            TextEditorBox.Foreground = new SolidColorBrush(activeLayer.TextColor);
            TextEditorBox.CaretBrush = TextEditorBox.Foreground;
            TextEditorBox.FontFamily = new FontFamily(activeLayer.TextFontFamily);
            TextEditorBox.FontWeight = activeLayer.TextFontStyle == "Bold" ? FontWeights.Bold : FontWeights.Normal;
            TextEditorBox.FontStyle = activeLayer.TextFontStyle == "Italic" ? FontStyles.Italic : FontStyles.Normal;
            TextEditorBox.TextAlignment = activeLayer.TextAlignment == "Center" ? TextAlignment.Center : 
                                          activeLayer.TextAlignment == "Right" ? TextAlignment.Right : TextAlignment.Left;

            // Hide the text layer during editing so it doesn't double-render
            activeLayer.IsTempHidden = true;
            OnEditorDocumentModified();

            TextMoveContainer.Visibility = Visibility.Visible;
            TextEditorBox.Focus();
            TextEditorBox.SelectAll();
        }

        private void TextToolbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var parent = dep;
                while (parent != null)
                {
                    if (parent == TextEditorBox)
                    {
                        return; // Let the TextBox handle the click (placing caret, selecting text)
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            var element = sender as FrameworkElement;
            if (element == null) return;

            _isDraggingTextContainer = true;
            _textDragStartMousePos = e.GetPosition(MainScrollViewer);
            _textDragStartMargin = TextMoveContainer.Margin;
            element.CaptureMouse();
            e.Handled = true;
        }

        private void TextToolbar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingTextContainer) return;
            var element = sender as FrameworkElement;
            if (element == null) return;

            var currentPos = e.GetPosition(MainScrollViewer);
            double dx = currentPos.X - _textDragStartMousePos.X;
            double dy = currentPos.Y - _textDragStartMousePos.Y;

            double scale = ImageZoomScale.ScaleX;
            if (scale <= 0) scale = 1.0;

            double newLeft = _textDragStartMargin.Left + (dx / scale);
            double newTop = _textDragStartMargin.Top + (dy / scale);

            // Clamp container bounds loosely
            newLeft = Math.Clamp(newLeft, -1000, MainImage.ActualWidth + 1000);
            newTop = Math.Clamp(newTop, -1000, MainImage.ActualHeight + 1000);

            TextMoveContainer.Margin = new Thickness(newLeft, newTop, 0, 0);
            e.Handled = true;
        }

        private void TextToolbar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingTextContainer)
            {
                _isDraggingTextContainer = false;
                var element = sender as FrameworkElement;
                element?.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void TextResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double scale = ImageZoomScale.ScaleX;
            if (scale <= 0) scale = 1.0;

            double dw = e.HorizontalChange / scale;
            double dh = e.VerticalChange / scale;

            if (double.IsNaN(TextBoundingBorder.Width) || TextBoundingBorder.Width <= 0)
                TextBoundingBorder.Width = TextBoundingBorder.ActualWidth;
            if (double.IsNaN(TextBoundingBorder.Height) || TextBoundingBorder.Height <= 0)
                TextBoundingBorder.Height = TextBoundingBorder.ActualHeight;

            TextBoundingBorder.Width = Math.Max(100, TextBoundingBorder.Width + dw);
            TextBoundingBorder.Height = Math.Max(40, TextBoundingBorder.Height + dh);
            e.Handled = true;
        }

        private void TextEditorBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CommitActiveText();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelActiveText();
                e.Handled = true;
            }
        }

        private void TextEditorBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void RedrawTextLayer(EditorLayer layer)
        {
            if (layer == null || !layer.IsTextLayer) return;
            // Clear bitmap so it stays transparent (drawing is done dynamically in Composite)
            layer.Clear();
        }

        private void CommitActiveText()
        {
            var textLayer = _editingTextLayer;
            if (textLayer == null || !textLayer.IsTextLayer) return;

            string text = TextEditorBox.Text;
            if (string.IsNullOrEmpty(text) || text == "Nhập chữ...")
            {
                CancelActiveText();
                return;
            }

            // Snapshot old metadata
            string oldText = textLayer.TextContent;
            double oldX = textLayer.TextX;
            double oldY = textLayer.TextY;
            double oldW = textLayer.TextWidth;
            double oldH = textLayer.TextHeight;
            double oldSize = textLayer.TextFontSize;
            Color oldColor = textLayer.TextColor;
            string oldFamily = textLayer.TextFontFamily;
            string oldStyle = textLayer.TextFontStyle;
            string oldAlign = textLayer.TextAlignment;

            // Save new metadata
            string newText = text;
            double newX = TextMoveContainer.Margin.Left;
            double newY = TextMoveContainer.Margin.Top;
            double newW = TextBoundingBorder.Width;
            double newH = TextBoundingBorder.Height;
            double newSize = EditorPanel.TextFontSize;
            Color newColor = EditorPanel.TextColor;
            string newFamily = EditorPanel.TextFontFamily;
            string newStyle = EditorPanel.TextFontStyle;
            string newAlign = EditorPanel.TextAlignment;

            // Execute TextEditCommand
            if (_node.EditorDoc != null)
            {
                var cmd = new TextEditCommand(
                    textLayer,
                    RedrawTextLayer,
                    oldText, oldX, oldY, oldW, oldH, oldSize, oldColor, oldFamily, oldStyle, oldAlign,
                    newText, newX, newY, newW, newH, newSize, newColor, newFamily, newStyle, newAlign
                );
                _node.EditorDoc.History.Execute(cmd);
            }

            TextMoveContainer.Visibility = Visibility.Collapsed;
            textLayer.IsTempHidden = false; // Restore visibility
            textLayer.InvalidateThumbnail();
            _editingTextLayer = null;
            OnEditorDocumentModified();
        }

        private void CancelActiveText()
        {
            TextMoveContainer.Visibility = Visibility.Collapsed;
            var layer = _editingTextLayer;
            if (layer != null && layer.IsTextLayer)
            {
                layer.IsTempHidden = false; // Restore visibility
                if (layer.TextContent == "Nhập chữ..." || string.IsNullOrEmpty(layer.TextContent))
                {
                    if (_node.EditorDoc != null)
                    {
                        _node.EditorDoc.Layers.Remove(layer);
                        EditorPanel.RefreshLayersList();
                    }
                }
                else
                {
                    RedrawTextLayer(layer);
                }
            }
            _editingTextLayer = null;
        }
    }
}
