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
                else if (e.PropertyName == nameof(MouseEventNodeDialogViewModel.SelectedTargetWindow))
                {
                    // Update embed button state when target window changes
                    OnTargetWindowSelectionChanged();
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
            // Flush all textbox bindings
            FlushTextBoxBinding(RepeatCountTextBox);
            FlushTextBoxBinding(ExtraTextBox);
            FlushTextBoxBinding(ClickDurationTextBox);

            // Flush all combobox bindings
            FlushComboBoxBinding(MouseButtonComboBox);

            // Flush NodeSearchComboBoxUserControl bindings (coord source)
            FlushNodeSearchComboBoxBinding(CoordSourceNodeId);

            // Flush output key combobox
            FlushComboBoxBinding(CoordSourceOutputKey);

            // Flush target window combobox
            FlushComboBoxBinding(SelectedTargetWindow);
        }

        private static void FlushTextBoxBinding(System.Windows.Controls.TextBox? tb)
            => tb?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();

        private static void FlushComboBoxBinding(System.Windows.Controls.ComboBox? cb)
            => cb?.GetBindingExpression(System.Windows.Controls.ComboBox.SelectedValueProperty)?.UpdateSource();

        private static void FlushNodeSearchComboBoxBinding(Controls.NodeSearchComboBoxUserControl? nsc)
        {
            if (nsc != null)
            {
                var be = nsc.GetBindingExpression(Controls.NodeSearchComboBoxUserControl.SelectedValueProperty);
                be?.UpdateSource();
            }
        }

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
            // Always activate the target app when picking position, regardless of background mode
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

        // ══════════════════════════════════════════════════════════════════
        // EMBEDDED WINDOW PREVIEW
        // ══════════════════════════════════════════════════════════════════

        private bool _isEmbedded = false;

        private void EmbedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedWindow = _viewModel.SelectedTargetWindow;
            if (selectedWindow == null)
            {
                MessageBox.Show(
                    "Vui lòng chọn app ở tab Logic trước.",
                    "Chưa chọn app",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!Helpers.WindowHostHelper.CanEmbedWindow(selectedWindow.Handle))
            {
                MessageBox.Show(
                    "Không thể embed window này.\n\n" +
                    "System windows (Explorer, Task Manager) không thể embed.\n" +
                    "Thử với Paint, Notepad, Calculator hoặc apps khác.",
                    "Không thể embed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool success = EmbeddedWindowHost.EmbedWindow(selectedWindow.Handle);
                
                if (success)
                {
                    _isEmbedded = true;
                    EmbedPlaceholder.Visibility = Visibility.Collapsed;
                    EmbeddedWindowHost.Visibility = Visibility.Visible;
                    EmbedStatusText.Text = $"Embedded: {selectedWindow.DisplayName}";
                    EmbedStatusText.Tag = "Embedded"; // Trigger UI hide
                    EmbedButton.IsEnabled = false;
                    UnembedButton.IsEnabled = true;

                    System.Diagnostics.Debug.WriteLine($"[MouseEventNodeDialog] ✅ Embedded {selectedWindow.DisplayName}");
                }
                else
                {
                    MessageBox.Show(
                        "Failed to embed window. Try another app.",
                        "Embed Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error embedding window: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"[MouseEventNodeDialog] ❌ Embed error: {ex}");
            }
        }

        private void UnembedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EmbeddedWindowHost.UnembedWindow();
                
                _isEmbedded = false;
                EmbedPlaceholder.Visibility = Visibility.Visible;
                EmbeddedWindowHost.Visibility = Visibility.Collapsed;
                EmbedStatusText.Text = "Chọn app ở tab Logic để embed";
                EmbedStatusText.Tag = "NotEmbedded"; // Restore UI
                EmbedButton.IsEnabled = _viewModel.SelectedTargetWindow != null;
                UnembedButton.IsEnabled = false;

                System.Diagnostics.Debug.WriteLine($"[MouseEventNodeDialog] ✅ Unembedded window");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MouseEventNodeDialog] ❌ Unembed error: {ex}");
            }
        }

        // Enable embed button when target window is selected
        private void OnTargetWindowSelectionChanged()
        {
            EmbedButton.IsEnabled = _viewModel.SelectedTargetWindow != null && !_isEmbedded;
            
            if (_viewModel.SelectedTargetWindow != null)
            {
                EmbedStatusText.Text = $"Ready to embed: {_viewModel.SelectedTargetWindow.DisplayName}";
                EmbedStatusText.Tag = "NotEmbedded";
            }
            else
            {
                EmbedStatusText.Text = "Chọn app ở tab Logic để embed";
                EmbedStatusText.Tag = "NotEmbedded";
            }
        }

        /// <summary>
        /// Activate embedded window trước khi send input.
        /// Call từ PlayButton/RunSingleNode.
        /// </summary>
        public void ActivateEmbeddedWindowForInput()
        {
            if (_isEmbedded && EmbeddedWindowHost != null)
            {
                try
                {
                    // Activate this dialog first
                    this.Activate();
                    this.Focus();
                    
                    // Embedded window as child will receive input
                    System.Threading.Thread.Sleep(50);
                    
                    System.Diagnostics.Debug.WriteLine("[MouseEventNodeDialog] ✅ Activated for embedded input");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MouseEventNodeDialog] Activate error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if window is currently embedded in Preview tab.
        /// </summary>
        public bool IsWindowEmbedded => _isEmbedded;

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Unembed before closing
            if (_isEmbedded)
            {
                try
                {
                    EmbeddedWindowHost.UnembedWindow();
                }
                catch { }
            }

            base.OnClosing(e);
        }
    }
}
