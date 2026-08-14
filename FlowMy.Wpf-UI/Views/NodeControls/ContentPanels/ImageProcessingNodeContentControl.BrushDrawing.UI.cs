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
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using FlowMy.Services.Rendering;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl : UserControl
    {

        private bool _isSyncingBrushProperties = false;

        private void InitializeBrushPresetListBoxItems()
        {
            if (PopupBrushPreset == null || PopupBrushPreset.Items.Count > 0) return;
            PopupBrushPreset.Items.Clear();

            var presets = new[]
            {
                (BrushPreset.RoundHard, "Round Hard (Cọ tròn cứng)"),
                (BrushPreset.RoundSoft, "Round Soft (Cọ tròn mềm)"),
                (BrushPreset.Flat, "Flat (Cọ dẹp)"),
                (BrushPreset.Chalk, "Chalk (Cọ phấn)"),
                (BrushPreset.Spray, "Spray (Bình xịt)"),
                (BrushPreset.Scatter, "Scatter (Điểm rải rác)"),
                (BrushPreset.Pencil, "Pencil (Bút chì)"),
                (BrushPreset.Airbrush, "Airbrush (Bút phun khí)"),
                (BrushPreset.Splatter, "Splatter (Vết mực bắn)"),
                (BrushPreset.Charcoal, "Charcoal (Than củi)"),
                (BrushPreset.OilBrush, "Oil Brush (Sơn dầu)")
            };

            foreach (var presetInfo in presets)
            {
                var previewElement = CreateBrushPreviewElement(24, 24, presetInfo.Item1, 50, 100, 100, Colors.White);
                var listBoxItem = new ListBoxItem
                {
                    Content = previewElement,
                    ToolTip = presetInfo.Item2
                };
                PopupBrushPreset.Items.Add(listBoxItem);
            }
        }

        private void SyncFromEditorPanelBrushProperties()
        {
            if (EditorPanel == null || _isSyncingBrushProperties) return;
            _isSyncingBrushProperties = true;
            try
            {
                if (OptBrushSizeInput != null && OptBrushSizeInput.Text != ((int)EditorPanel.BrushSize).ToString())
                {
                    OptBrushSizeInput.Text = ((int)EditorPanel.BrushSize).ToString();
                }

                if (PopupBrushSize != null && PopupBrushSize.Value != EditorPanel.BrushSize)
                {
                    PopupBrushSize.Value = EditorPanel.BrushSize;
                }

                if (PopupBrushHardness != null && PopupBrushHardness.Value != EditorPanel.BrushHardness)
                {
                    PopupBrushHardness.Value = EditorPanel.BrushHardness;
                }

                if (PopupBrushFlow != null && PopupBrushFlow.Value != EditorPanel.BrushFlow)
                {
                    PopupBrushFlow.Value = EditorPanel.BrushFlow;
                }

                if (PopupBrushPreset != null && PopupBrushPreset.SelectedIndex != (int)_currentBrushPreset)
                {
                    PopupBrushPreset.SelectedIndex = (int)_currentBrushPreset;
                }

                // Update preview representations
                UpdateBrushPreviewVisuals();
            }
            finally
            {
                _isSyncingBrushProperties = false;
            }
        }

        private void UpdateBrushPreviewVisuals()
        {
            if (EditorPanel == null) return;

            if (BrushBarPreviewContainer != null)
            {
                BrushBarPreviewContainer.Content = CreateBrushPreviewElement(20, 20, _currentBrushPreset, EditorPanel.BrushSize, EditorPanel.BrushHardness, EditorPanel.BrushFlow, Colors.White);
            }
            if (BrushPopupPreviewContainer != null)
            {
                BrushPopupPreviewContainer.Content = CreateBrushPreviewElement(38, 38, _currentBrushPreset, EditorPanel.BrushSize, EditorPanel.BrushHardness, EditorPanel.BrushFlow, Colors.White);
            }
        }

        private UIElement CreateBrushPreviewElement(double width, double height, BrushPreset preset, double actualSize, double hardness, double flow, Color brushColor)
        {
            double maxDiameter = height - 4;
            // PREVIEW SIZE: Ignore actualSize to make preview fill the container and represent shape clearly
            double diameter = maxDiameter;
            double radius = diameter / 2.0;

            // Normalize hardness and flow since they are in 0-100 and 1-100 ranges
            double f = flow / 100.0;
            double h = hardness / 100.0;

            var grid = new Grid { Width = width, Height = height };

            switch (preset)
            {
                case BrushPreset.RoundSoft:
                    {
                        var ellipse = new Ellipse
                        {
                            Width = diameter,
                            Height = diameter,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var gradient = new RadialGradientBrush();
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255 * 0.5), brushColor.R, brushColor.G, brushColor.B), 0.4));
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        ellipse.Fill = gradient;
                        grid.Children.Add(ellipse);
                    }
                    break;

                case BrushPreset.Flat:
                    {
                        var rect = new Rectangle
                        {
                            Width = diameter,
                            Height = Math.Max(2.0, diameter / 3.0),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            RadiusX = 1,
                            RadiusY = 1
                        };
                        var brush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(0, 1)
                        };
                        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), h));
                        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        rect.Fill = brush;
                        grid.Children.Add(rect);
                    }
                    break;

                case BrushPreset.Chalk:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;
                        
                        double previewRadius = radius / 6.0;
                        foreach (var offset in ChalkPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * (offset.size * 0.15);
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 180), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Spray:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in SprayPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * 0.25;
                            double spotDiameter = spotRadius * 2;
                            double distRatio = Math.Sqrt(offset.x * offset.x + offset.y * offset.y);
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 200 * (1.0 - distRatio * 0.5)), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Scatter:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in ScatterPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * offset.scale;
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 160), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Pencil:
                    {
                        double pencilSize = Math.Max(2.0, diameter * 0.4);
                        var ellipse = new Ellipse
                        {
                            Width = pencilSize,
                            Height = pencilSize,
                            Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        grid.Children.Add(ellipse);
                    }
                    break;

                case BrushPreset.Airbrush:
                    {
                        var ellipse = new Ellipse
                        {
                            Width = diameter,
                            Height = diameter,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var gradient = new RadialGradientBrush();
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255 * 0.25), brushColor.R, brushColor.G, brushColor.B), 0.3));
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        ellipse.Fill = gradient;
                        grid.Children.Add(ellipse);
                    }
                    break;

                case BrushPreset.Splatter:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in SplatterPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * offset.size * 0.4;
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * offset.opacity * 255), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Charcoal:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in CharcoalPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * offset.size * 0.45;
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * offset.opacity * 200), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.OilBrush:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in OilBrushPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * offset.size * 0.25;
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 200), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                default: // RoundHard
                    {
                        var ellipse = new Ellipse
                        {
                            Width = diameter,
                            Height = diameter,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var gradient = new RadialGradientBrush();
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                        double stopOffset = Math.Max(0.0, h);
                        if (stopOffset < 0.99)
                        {
                            gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), stopOffset));
                            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        }
                        else
                        {
                            gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), 0.99));
                            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        }
                        ellipse.Fill = gradient;
                        grid.Children.Add(ellipse);
                    }
                    break;
            }

            return grid;
        }

        private void OptBrushSizeInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyBrushSizeFromTextBox();
        }

        private void OptBrushSizeInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyBrushSizeFromTextBox();
                MainScrollViewer.Focus();
                e.Handled = true;
            }
        }

        private void ApplyBrushSizeFromTextBox()
        {
            if (OptBrushSizeInput == null || EditorPanel == null) return;
            if (double.TryParse(OptBrushSizeInput.Text, out double size))
            {
                size = Math.Clamp(size, 1.0, 5000.0);
                if (EditorPanel.SliderBrushSize != null && EditorPanel.SliderBrushSize.Value != size)
                {
                    EditorPanel.SliderBrushSize.Value = size;
                }
            }
            SyncFromEditorPanelBrushProperties();
            UpdateBrushCursorPosition();
        }

        private void BtnBrushPreviewDropdown_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (BrushSettingsPopup != null)
            {
                if (BrushSettingsPopup.IsOpen)
                {
                    BrushSettingsPopup.IsOpen = false;
                }
                else
                {
                    SyncFromEditorPanelBrushProperties();
                    if (BrushSettingsPopup.Child is FrameworkElement childBorder)
                    {
                        childBorder.LayoutTransform = TopOptionsBar.LayoutTransform ?? Transform.Identity;
                    }
                    BrushSettingsPopup.IsOpen = true;

                    var window = Window.GetWindow(this);
                    if (window != null)
                    {
                        window.PreviewMouseDown -= Window_PreviewMouseDown;
                        window.PreviewMouseDown += Window_PreviewMouseDown;
                    }
                }
            }
        }

        private bool IsClickInsidePopupOrComboBox(DependencyObject clickedElement, DependencyObject popupChild)
        {
            if (clickedElement == null || popupChild == null) return false;

            DependencyObject current = clickedElement;
            while (current != null)
            {
                if (current == popupChild || current == PopupBrushPreset)
                    return true;

                if (current is ComboBox || current is ComboBoxItem || current is ListBox || current is ListBoxItem)
                    return true;

                if (current is System.Windows.Controls.Primitives.Popup p)
                {
                    if (p.PlacementTarget != null && IsClickInsidePopupOrComboBox(p.PlacementTarget, popupChild))
                        return true;
                }

                DependencyObject next = null;
                if (current is Visual)
                {
                    next = VisualTreeHelper.GetParent(current);
                }
                
                if (next == null && current is FrameworkElement fe)
                {
                    next = fe.Parent ?? fe.TemplatedParent;
                }
                
                if (next == null && current is FrameworkContentElement fce)
                {
                    next = fce.Parent ?? fce.TemplatedParent;
                }

                current = next;
            }
            return false;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (BrushSettingsPopup == null || !BrushSettingsPopup.IsOpen) return;

            var child = BrushSettingsPopup.Child as FrameworkElement;
            if (child != null)
            {
                var clickedElement = e.OriginalSource as DependencyObject;
                if (IsClickInsidePopupOrComboBox(clickedElement, child))
                {
                    return; // Nhấp vào bên trong Popup hoặc ComboBox Dropdown, không đóng Popup
                }

                var mousePosBtn = e.GetPosition(BtnBrushPreviewDropdown);
                var btnRect = new Rect(0, 0, BtnBrushPreviewDropdown.ActualWidth, BtnBrushPreviewDropdown.ActualHeight);

                if (!btnRect.Contains(mousePosBtn))
                {
                    BrushSettingsPopup.IsOpen = false;

                    var window = sender as Window;
                    if (window != null)
                    {
                        window.PreviewMouseDown -= Window_PreviewMouseDown;
                    }
                }
            }
        }

        private void BrushSettingsPopup_Closed(object sender, EventArgs e)
        {
            SyncFromEditorPanelBrushProperties();
            UpdateBrushCursorPosition();

            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown -= Window_PreviewMouseDown;
            }
        }

        private void PopupBrushPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PopupBrushPreset == null || _isSyncingBrushProperties || EditorPanel == null) return;
            _currentBrushPreset = (BrushPreset)PopupBrushPreset.SelectedIndex;

            if (_currentBrushPreset == BrushPreset.RoundHard)
            {
                if (EditorPanel.SliderBrushHardness != null) EditorPanel.SliderBrushHardness.Value = 100;
            }
            else if (_currentBrushPreset == BrushPreset.RoundSoft)
            {
                if (EditorPanel.SliderBrushHardness != null) EditorPanel.SliderBrushHardness.Value = 0;
            }

            if (_currentBrushPreset == BrushPreset.Pencil)
            {
                if (EditorPanel.SliderBrushHardness != null) EditorPanel.SliderBrushHardness.Value = 100;
            }

            if (PopupHardnessPanel != null)
            {
                bool useHardness = (_currentBrushPreset == BrushPreset.RoundHard || _currentBrushPreset == BrushPreset.Flat);
                PopupHardnessPanel.Opacity = useHardness ? 1.0 : 0.4;
                PopupBrushHardness.IsEnabled = useHardness;
            }

            SyncFromEditorPanelBrushProperties();
            UpdateBrushCursorPosition();
        }

        private void PopupBrushSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingBrushProperties || EditorPanel == null) return;
            if (EditorPanel.SliderBrushSize != null && EditorPanel.SliderBrushSize.Value != e.NewValue)
            {
                EditorPanel.SliderBrushSize.Value = e.NewValue;
            }
            if (TxtPopupBrushSize != null)
            {
                TxtPopupBrushSize.Text = $"{(int)e.NewValue}px";
            }
            if (OptBrushSizeInput != null && OptBrushSizeInput.Text != ((int)e.NewValue).ToString())
            {
                OptBrushSizeInput.Text = ((int)e.NewValue).ToString();
            }
            UpdateBrushPreviewVisuals();
        }

        private void PopupBrushHardness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingBrushProperties || EditorPanel == null) return;
            if (EditorPanel.SliderBrushHardness != null && EditorPanel.SliderBrushHardness.Value != e.NewValue)
            {
                EditorPanel.SliderBrushHardness.Value = e.NewValue;
            }
            if (TxtPopupBrushHardness != null)
            {
                TxtPopupBrushHardness.Text = $"{(int)e.NewValue}%";
            }
            UpdateBrushPreviewVisuals();
        }

        private void PopupBrushFlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingBrushProperties || EditorPanel == null) return;
            if (EditorPanel.SliderBrushFlow != null && EditorPanel.SliderBrushFlow.Value != e.NewValue)
            {
                EditorPanel.SliderBrushFlow.Value = e.NewValue;
            }
            if (TxtPopupBrushFlow != null)
            {
                TxtPopupBrushFlow.Text = $"{(int)e.NewValue}%";
            }
            UpdateBrushPreviewVisuals();
        }

    }
}
