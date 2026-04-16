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

        public KeyPressEventNodeDialog(KeyPressEventNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
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

        protected override void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
        }

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

        private void TitleColorComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateTitleColorPreview();
        }

        private void UpdateTitleColorPreview()
        {
            if (TitleColorPreview == null || TitleColorComboBox?.SelectedValue == null) return;

            var colorKey = TitleColorComboBox.SelectedValue.ToString();
            System.Windows.Media.Brush? brush = null;

            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                // Màu theo node - lấy từ node hiện tại
                if (_viewModel?.Node != null)
                {
                    brush = _viewModel.Node.NodeBrush;
                }
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            }
            else
            {
                brush = Application.Current.TryFindResource(colorKey) as System.Windows.Media.Brush;
            }

            TitleColorPreview.Background = brush ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
    }
}
