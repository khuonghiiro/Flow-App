using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class HotkeyPressEventNodeDialog : BaseNodeDialog
    {
        private readonly HotkeyPressEventNodeDialogViewModel _viewModel;
        private readonly HotkeyPressEventNode _node;

        public HotkeyPressEventNodeDialog(HotkeyPressEventNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            _node = node;
            InitializeComponent();

            // Initialize base after InitializeComponent
            _viewModel = new HotkeyPressEventNodeDialogViewModel(node, host);
            
            // Initialize base class properties
            InitializeBase(_viewModel, owner);

            // Update title color preview
            UpdateTitleColorPreview();

            // Sync hotkey display text khi ViewModel thay đổi
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(HotkeyPressEventNodeDialogViewModel.HotkeyDisplayText))
                {
                    HotkeyButton.Content = _viewModel.HotkeyDisplayText;
                }
            };

            // Validation cho PressDelayMs textbox
            PressDelayTextBox.LostFocus += (s, e) =>
            {
                ValidatePressDelay();
            };

            PressDelayTextBox.PreviewTextInput += (s, e) =>
            {
                // Chỉ cho phép nhập số
                e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
            };
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void OnLoaded()
        {
            base.OnLoaded();
            RefreshPositionDisplay();
        }

        protected override void BeforeSaveOnClose()
        {
            FlushTextBoxBinding(ClickDurationTextBox);
        }

        private static void FlushTextBoxBinding(System.Windows.Controls.TextBox? tb)
            => tb?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();

        private void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CaptureHotkeyCommand.Execute(null);
        }

        private void ValidatePressDelay()
        {
            var text = PressDelayTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                PressDelayTextBox.Text = "100";
                _viewModel.PressDelayMs = 100;
                return;
            }

            if (int.TryParse(text, out var value))
            {
                if (value < 0)
                {
                    PressDelayTextBox.Text = "0";
                    _viewModel.PressDelayMs = 0;
                }
                else
                {
                    _viewModel.PressDelayMs = value;
                }
            }
            else
            {
                // Không phải số hợp lệ, reset về giá trị hiện tại của ViewModel
                PressDelayTextBox.Text = _viewModel.PressDelayMs.ToString();
            }
        }

        private void PickPositionButton_Click(object sender, RoutedEventArgs e)
        {
            // Focus app được chọn trước khi mở overlay (để chọn đúng vị trí trong app đó)
            var targetWindow = _viewModel.SelectedTargetWindow;
            if (targetWindow != null)
            {
                FlowMy.Helpers.WindowHelper.BringToFront(targetWindow.Handle);
                System.Threading.Thread.Sleep(300);
            }

            var windowsToHide = new System.Collections.Generic.List<Window>();
            foreach (Window w in Application.Current.Windows)
            {
                if (w.IsVisible) windowsToHide.Add(w);
            }
            foreach (var w in windowsToHide) w.Hide();

            try
            {
                var overlay = new ScreenPositionPickerOverlay();
                var result = overlay.ShowDialog();
                if (result == true && overlay.SelectedPosition.HasValue)
                {
                    _node.ManualPosition = overlay.SelectedPosition.Value;
                    RefreshPositionDisplay();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi chọn vị trí: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                foreach (var w in windowsToHide) w.Show();
                Activate();
            }
        }

        private void RefreshPositionDisplay()
        {
            _viewModel.PositionText = _node.PositionText;
        }
    }
}
