using FlowMy.Models.Nodes;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using FlowMy.Views.Overlays;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;

namespace FlowMy.Views.Overlays
{
    public partial class ActionCanVasNodeDialog : BaseNodeDialog
    {
        private readonly ActionCanVasNodeDialogViewModel _viewModel;
        private readonly ActionCanVasNode _node;

        public ActionCanVasNodeDialog(ActionCanVasNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _node = node;
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
            };

            UpdateTitleColorPreview();
            UpdateColorPreviews();
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

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            var border = _node.ContainerBorder;
            if (border == null) return;

            var pt = border.PointToScreen(new Point(0, 0));
            var ptBottomRight = border.PointToScreen(new Point(border.ActualWidth, border.ActualHeight));
            var bounds = new Rect(pt.X, pt.Y, ptBottomRight.X - pt.X, ptBottomRight.Y - pt.Y);

            this.Hide();

            var overlay = new MacroRecorderOverlay(false, FlowMy.Models.MacroExecutionMode.Free, "", "", bounds, true);
            overlay.ShowDialog();

            if (!string.IsNullOrEmpty(overlay.RecordedJson))
            {
                _viewModel.MacroDataJson = overlay.RecordedJson;
            }

            this.Show();
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
    }
}
