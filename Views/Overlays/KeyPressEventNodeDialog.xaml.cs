using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class KeyPressEventNodeDialog : BaseNodeDialog
    {
        private readonly KeyPressEventNodeDialogViewModel _viewModel;
        private readonly KeyPressEventNode _node;

        public KeyPressEventNodeDialog(KeyPressEventNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            _node = node;
            InitializeComponent();

            // Initialize base after InitializeComponent
            _viewModel = new KeyPressEventNodeDialogViewModel(node, host);
            
            // Initialize base class properties
            Owner = owner;
            SetViewModel(_viewModel);
            DataContext = ViewModel;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Update title color preview
            UpdateTitleColorPreview();

            // Load inputs và outputs vào UI sau khi dialog đã được load
            Loaded += (s, e) =>
            {
                try
                {
                    // Reload danh sách cửa sổ khi dialog mở để cập nhật tab mới
                    _viewModel.LoadWindowsCommand.Execute(null);

                    var inputsPanel = GetInputsPanel();
                    var outputsPanel = GetOutputsPanel();

                    if (inputsPanel != null && ViewModel != null)
                    {
                        LoadInputs();
                    }

                    if (outputsPanel != null && ViewModel != null)
                    {
                        LoadOutputs();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading inputs/outputs: {ex.Message}");
                }
            };

            // Ngăn dialog bị đóng khi click vào nó hoặc mất focus
            Deactivated += (s, e) =>
            {
                // Không làm gì - để dialog vẫn mở
            };

            // Lưu title khi đóng
            Closing += (s, e) =>
            {
                try
                {
                    ViewModel.SaveTitleCommand.Execute(null);
                }
                catch { }
            };

            // Đảm bảo dialog không bị đóng khi click vào nó
            PreviewMouseDown += (s, e) =>
            {
                // Chỉ ngăn event bubble lên owner window
                // Không set e.Handled = true để các controls bên trong vẫn nhận được events
            };

            // Setup PropertyChanged handlers
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Sync key display text khi ViewModel thay đổi
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(KeyPressEventNodeDialogViewModel.KeyDisplayText))
                {
                    KeyButton.Content = _viewModel.KeyDisplayText;
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

        private void KeyButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CaptureKeyCommand.Execute(null);
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
