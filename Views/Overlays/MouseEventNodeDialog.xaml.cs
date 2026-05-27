using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class MouseEventNodeDialog : BaseNodeDialog
    {
        private readonly MouseEventNodeDialogViewModel _viewModel;
        private readonly MouseEventNode _node;

        public MouseEventNodeDialog(MouseEventNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            _node = node;
            InitializeComponent();

            // Initialize base after InitializeComponent
            _viewModel = new MouseEventNodeDialogViewModel(node, host);
            
            // Initialize base class properties
            InitializeBase(_viewModel, owner);

            // Update title color preview
            UpdateTitleColorPreview();

            // Sync khi MouseButton thay đổi để cập nhật ExtraPanel
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MouseEventNodeDialogViewModel.MouseButton))
                {
                    UpdateExtraPanel();
                    UpdateRepeatCountTextBoxState();
                }
                else if (e.PropertyName == nameof(MouseEventNodeDialogViewModel.IsRepeatCountConnected))
                {
                    UpdateRepeatCountTextBoxState();
                }
                else if (e.PropertyName == nameof(MouseEventNodeDialogViewModel.IsScrollMode))
                {
                    UpdateRepeatCountTextBoxState();
                }
            };

            // Validation cho RepeatCount và Extra textboxes
            RepeatCountTextBox.LostFocus += (s, e) => ValidateRepeatCount();
            RepeatCountTextBox.PreviewTextInput += (s, e) =>
            {
                e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
            };

            ExtraTextBox.LostFocus += (s, e) => ValidateExtra();
            ExtraTextBox.PreviewTextInput += (s, e) =>
            {
                bool isScroll = _viewModel.MouseButton == "ScrollUp" || _viewModel.MouseButton == "ScrollDown";
                if (isScroll)
                {
                    e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
                }
                else
                {
                    e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+(\\.[0-9]+)?$");
                }
            };
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void BeforeSaveOnClose()
        {
            FlushTextBoxBinding(ClickDurationTextBox);
        }

        private static void FlushTextBoxBinding(System.Windows.Controls.TextBox? tb)
            => tb?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();

        protected override void OnLoaded()
        {
            base.OnLoaded();
            // Reload danh sách cửa sổ khi dialog mở để cập nhật tab mới
            _viewModel.LoadWindowsCommand.Execute(null);
            UpdateExtraPanel();
            UpdateRepeatCountTextBoxState();
            RefreshPositionDisplay();
        }

        protected override FrameworkElement CreateInputItemUI(InputItemViewModel inputVm)
        {
            var item = base.CreateInputItemUI(inputVm);
            
            // Update RepeatCountTextBox state khi connection thay đổi
            inputVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InputItemViewModel.SelectedSourceNodeId) && inputVm.Key == "repeatCount")
                {
                    // Trigger update trong ViewModel
                    _viewModel.UpdateRepeatCountConnectionStatus();
                    UpdateRepeatCountTextBoxState();
                }
            };

            return item;
        }

        protected override void LoadOutputs()
        {
            base.LoadOutputs();

            var count = ViewModel.Outputs.Count;

            BorderOutputPanel.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TextBlockOutputPanel.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateExtraPanel()
        {
            bool isScroll = _viewModel.MouseButton == "ScrollUp" || _viewModel.MouseButton == "ScrollDown";

            if (isScroll)
            {
                ExtraLabel.Text = "Tốc độ lăn (Pixel):";
                ExtraTextBox.Text = _viewModel.ScrollSpeed.ToString();
            }
            else
            {
                ExtraLabel.Text = "Thời gian giữ (giây):";
                ExtraTextBox.Text = _viewModel.HoldDuration.ToString("F2");
            }
        }

        private void UpdateRepeatCountTextBoxState()
        {
            // Disable nếu có connection hoặc là scroll mode
            // Logic này đã được handle bằng binding trong XAML, nhưng giữ lại để đảm bảo
            bool shouldDisable = _viewModel.IsRepeatCountConnected || _viewModel.IsScrollMode;
            RepeatCountTextBox.IsEnabled = !shouldDisable;
        }

        private void ValidateRepeatCount()
        {
            // Không validate nếu disabled hoặc scroll mode
            if (!RepeatCountTextBox.IsEnabled || _viewModel.IsScrollMode)
                return;

            var text = RepeatCountTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                RepeatCountTextBox.Text = "1";
                _viewModel.RepeatCount = 1;
                return;
            }

            if (int.TryParse(text, out var value))
            {
                if (value < 1)
                {
                    RepeatCountTextBox.Text = "1";
                    _viewModel.RepeatCount = 1;
                }
                else
                {
                    _viewModel.RepeatCount = value;
                }
            }
            else
            {
                RepeatCountTextBox.Text = _viewModel.RepeatCount.ToString();
            }
        }

        private void ValidateExtra()
        {
            bool isScroll = _viewModel.MouseButton == "ScrollUp" || _viewModel.MouseButton == "ScrollDown";
            var text = ExtraTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                if (isScroll)
                {
                    ExtraTextBox.Text = "1";
                    _viewModel.ScrollSpeed = 1;
                }
                else
                {
                    ExtraTextBox.Text = "0.00";
                    _viewModel.HoldDuration = 0;
                }
                return;
            }

            if (isScroll)
            {
                if (int.TryParse(text, out var speed))
                {
                    if (speed < 1)
                    {
                        ExtraTextBox.Text = "1";
                        _viewModel.ScrollSpeed = 1;
                    }
                    else
                    {
                        _viewModel.ScrollSpeed = speed;
                    }
                }
                else
                {
                    ExtraTextBox.Text = _viewModel.ScrollSpeed.ToString();
                }
            }
            else
            {
                if (double.TryParse(text, out var duration))
                {
                    if (duration < 0)
                    {
                        ExtraTextBox.Text = "0.00";
                        _viewModel.HoldDuration = 0;
                    }
                    else
                    {
                        _viewModel.HoldDuration = duration;
                    }
                }
                else
                {
                    ExtraTextBox.Text = _viewModel.HoldDuration.ToString("F2");
                }
            }
        }

        private void PickPositionButton_Click(object sender, RoutedEventArgs e)
        {
            // Focus app được chọn trước khi mở overlay
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
