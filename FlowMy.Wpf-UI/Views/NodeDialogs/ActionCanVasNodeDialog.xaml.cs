// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models.Nodes;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using FlowMy.Views.Overlays;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using System.IO;

namespace FlowMy.Views.Overlays
{
    public partial class ActionCanVasNodeDialog : BaseNodeDialog
    {
        private readonly ActionCanVasNodeDialogViewModel _viewModel;
        private readonly ActionCanVasNode _node;
        private readonly IWorkflowEditorHost _host;

        public ActionCanVasNodeDialog(ActionCanVasNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _node = node;
            _host = host;
            _viewModel = new ActionCanVasNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ActionCanVasNodeDialogViewModel.BodyBackgroundColorHex) ||
                    e.PropertyName == nameof(ActionCanVasNodeDialogViewModel.BodyBorderColorHex) ||
                    e.PropertyName == nameof(ActionCanVasNodeDialogViewModel.UseUnifiedColors) ||
                    e.PropertyName == nameof(ActionCanVasNodeDialogViewModel.BackgroundOpacityPercent))
                {
                    UpdateColorPreviews();
                }
                else if (e.PropertyName == nameof(ActionCanVasNodeDialogViewModel.PlaybackBorderColorHex) ||
                         e.PropertyName == nameof(ActionCanVasNodeDialogViewModel.PlaybackEffectType))
                {
                    UpdateEffectPreview();
                }
            };

            // Subscribe tab switch event
            _viewModel.RequestSwitchToConfigTab += () =>
            {
                if (MainTabControl != null)
                {
                    // Tab "Cấu hình" is the last tab (index based on current layout)
                    for (int i = 0; i < MainTabControl.Items.Count; i++)
                    {
                        if (MainTabControl.Items[i] is TabItem tab && (tab.Header?.ToString() == "Cấu hình" || tab.Header?.ToString() == "Cấu hình cơ bản"))
                        {
                            MainTabControl.SelectedIndex = i;
                            break;
                        }
                    }
                }
            };

            UpdateTitleColorPreview();
            UpdateColorPreviews();
            UpdateEffectPreview();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void TitleColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateTitleColorPreview();

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            FlushEditorsToViewModel();
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        protected override void BeforeSaveOnClose()
        {
            FlushEditorsToViewModel();
            FlushReuseRoutesComboBoxBindings();
        }

        private void FlushEditorsToViewModel()
        {
            BodyBackgroundColorComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
            BodyBorderColorComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
            UseUnifiedColorsCheckBox?.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateSource();
            BackgroundOpacitySlider?.GetBindingExpression(Slider.ValueProperty)?.UpdateSource();
            BorderOpacitySlider?.GetBindingExpression(Slider.ValueProperty)?.UpdateSource();
            BorderThicknessSlider?.GetBindingExpression(Slider.ValueProperty)?.UpdateSource();
            BorderDashSpacingSlider?.GetBindingExpression(Slider.ValueProperty)?.UpdateSource();
            TitleTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            System.Windows.Data.BindingOperations.GetBindingExpressionBase(this, DataContextProperty)?.UpdateSource();
            FlushReuseRoutesComboBoxBindings();
        }

        private void FlushReuseRoutesComboBoxBindings()
        {
            if (ReuseRoutesItemsControl == null) return;
            for (int i = 0; i < ReuseRoutesItemsControl.Items.Count; i++)
            {
                var container = ReuseRoutesItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;
                var cbList = FindVisualChildren<ComboBox>(container);
                foreach (var cb in cbList)
                {
                    cb.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                }
            }
        }

        private static System.Collections.Generic.List<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            var list = new System.Collections.Generic.List<T>();
            if (depObj == null) return list;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) list.Add(t);
                list.AddRange(FindVisualChildren<T>(child));
            }
            return list;
        }

        private void PickBodyBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            var picked = ShowColorPicker(_viewModel.BodyBackgroundColorHex);
            if (!string.IsNullOrWhiteSpace(picked))
                _viewModel.BodyBackgroundColorKey = picked;
        }

        private void PickBodyBorderColor_Click(object sender, RoutedEventArgs e)
        {
            var picked = ShowColorPicker(_viewModel.BodyBorderColorHex);
            if (!string.IsNullOrWhiteSpace(picked))
                _viewModel.BodyBorderColorKey = picked;
        }

        private void UpdateColorPreviews()
        {
            if (BodyBackgroundColorPreview != null)
            {
                var bgBrush = ResolveBrush(_viewModel.BodyBackgroundColorHex, new SolidColorBrush(Color.FromRgb(107, 114, 128)));
                bgBrush = bgBrush.Clone();
                bgBrush.Opacity = Math.Clamp(_viewModel.BackgroundOpacityPercent / 100.0, 0.0, 1.0);
                BodyBackgroundColorPreview.Background = bgBrush;
            }

            if (BodyBorderColorPreview != null)
            {
                var borderHex = _viewModel.UseUnifiedColors ? _viewModel.BodyBackgroundColorHex : _viewModel.BodyBorderColorHex;
                BodyBorderColorPreview.Background = ResolveBrush(borderHex, new SolidColorBrush(Color.FromRgb(107, 114, 128)));
            }
        }

        private void RecordNewAction_Click(object sender, RoutedEventArgs e)
        {
            var border = _node.ContainerBorder;
            if (border == null) return;

            // Create new action item
            var newItem = new MacroActionItemViewModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Thao tác {_viewModel.MacroActions.Count + 1}",
                IsDefault = _viewModel.MacroActions.Count == 0 // First one is default
            };

            StartMacroRecording(border, recordedJson =>
            {
                newItem.MacroDataJson = recordedJson;
                _viewModel.MacroActions.Add(newItem);
                if (newItem.IsDefault)
                    _node.DefaultMacroActionId = newItem.Id;
                // Also keep backward compat — store first action's data in MacroDataJson
                if (_viewModel.MacroActions.Count == 1)
                    _viewModel.MacroDataJson = recordedJson;
            });
        }

        private void EditMacroAction_Click(object sender, RoutedEventArgs e)
        {
            var border = _node.ContainerBorder;
            if (border == null) return;

            var btn = sender as System.Windows.Controls.Button;
            var item = btn?.Tag as MacroActionItemViewModel;
            if (item == null) return;

            StartMacroRecording(border, recordedJson =>
            {
                item.MacroDataJson = recordedJson;
                // Sync default item to legacy MacroDataJson
                if (item.IsDefault)
                    _viewModel.MacroDataJson = recordedJson;
            });
        }

        private void StartMacroRecording(System.Windows.Controls.Border border, Action<string> onRecorded)
        {
            var pt = border.PointToScreen(new Point(0, 0));
            var ptBottomRight = border.PointToScreen(new Point(border.ActualWidth, border.ActualHeight));
            var bounds = new Rect(pt.X, pt.Y, ptBottomRight.X - pt.X, ptBottomRight.Y - pt.Y);

            this.Hide();

            var overlay = new MacroRecorderOverlay(false, FlowMy.Models.MacroExecutionMode.Free, "", "", bounds, true);
            
            overlay.Closed += (s, args) =>
            {
                if (!string.IsNullOrEmpty(overlay.RecordedJson))
                {
                    onRecorded(overlay.RecordedJson);
                    _node.MacroDataJson = overlay.RecordedJson; // Ensure node model backward compat
                }
                
                try 
                {
                    if (this.IsLoaded)
                    {
                        this.Show();
                    }
                    else
                    {
                        var manager = FlowMy.Views.NodeControls.Helpers.BaseNodeControlHelper.GetOrCreateDialogManager(_host);
                        var newDlg = new ActionCanVasNodeDialog(_node, _host, Application.Current?.MainWindow);
                        manager.OpenDialog(_node, newDlg, _host);
                    }
                }
                catch
                {
                    try
                    {
                        var manager = FlowMy.Views.NodeControls.Helpers.BaseNodeControlHelper.GetOrCreateDialogManager(_host);
                        var newDlg = new ActionCanVasNodeDialog(_node, _host, Application.Current?.MainWindow);
                        manager.OpenDialog(_node, newDlg, _host);
                    }
                    catch { } // Fallback
                }
            };
            
            overlay.Show();
        }

        private void ImportJsonButton_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (openDlg.ShowDialog() == true)
            {
                try
                {
                    _viewModel.MacroDataJson = File.ReadAllText(openDlg.FileName);
                    MessageBox.Show("Nhập dữ liệu Macro thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Lỗi đọc file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportJsonButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_viewModel.MacroDataJson))
            {
                MessageBox.Show("Không có dữ liệu Macro để xuất!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDlg = new SaveFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", DefaultExt = "json" };
            if (saveDlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveDlg.FileName, _viewModel.MacroDataJson);
                    MessageBox.Show("Xuất dữ liệu Macro thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Lỗi lưu file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PlaybackBorderColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_viewModel.PlaybackBorderColorHex);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _viewModel.PlaybackBorderColorHex = hex;
                UpdateEffectPreview();
            }
        }

        private void PlaybackEffectTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateEffectPreview();
        }

        private void UpdateEffectPreview()
        {
            if (EffectPreview == null) return;

            // Reset animations
            EffectPreview.BeginAnimation(BorderBrushProperty, null);
            EffectPreview.BeginAnimation(OpacityProperty, null);

            var effectType = _viewModel.PlaybackEffectType;
            var color = ResolveBrush(_viewModel.PlaybackBorderColorHex, Brushes.Cyan) as SolidColorBrush;
            if (color == null) color = new SolidColorBrush(Colors.Cyan);

            EffectPreview.BorderBrush = color;

            switch (effectType)
            {
                case BorderEffectType.Pulse:
                    var pulseStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    var pulseAnim = new DoubleAnimation
                    {
                        From = 0.3,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(1.0),
                        AutoReverse = true
                    };
                    Storyboard.SetTarget(pulseAnim, EffectPreview);
                    Storyboard.SetTargetProperty(pulseAnim, new PropertyPath("Opacity"));
                    pulseStoryboard.Children.Add(pulseAnim);
                    pulseStoryboard.Begin();
                    break;

                case BorderEffectType.Glow:
                    var glowStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    var glowAnim = new DoubleAnimation
                    {
                        From = 0.5,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(0.5),
                        AutoReverse = true
                    };
                    Storyboard.SetTarget(glowAnim, EffectPreview);
                    Storyboard.SetTargetProperty(glowAnim, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Opacity)"));
                    glowStoryboard.Children.Add(glowAnim);
                    glowStoryboard.Begin();
                    break;

                case BorderEffectType.Rainbow:
                    var rainbowStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    var colors = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue, Colors.Indigo, Colors.Violet };
                    
                    for (int i = 0; i < colors.Length; i++)
                    {
                        var colorAnim = new ColorAnimation
                        {
                            From = colors[i],
                            To = colors[(i + 1) % colors.Length],
                            Duration = TimeSpan.FromSeconds(1),
                            BeginTime = TimeSpan.FromSeconds(i)
                        };
                        
                        Storyboard.SetTarget(colorAnim, EffectPreview);
                        Storyboard.SetTargetProperty(colorAnim, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));
                        rainbowStoryboard.Children.Add(colorAnim);
                    }
                    
                    rainbowStoryboard.Begin();
                    break;

                case BorderEffectType.None:
                default:
                    EffectPreview.Opacity = 1.0;
                    break;
            }
        }
    }
}
