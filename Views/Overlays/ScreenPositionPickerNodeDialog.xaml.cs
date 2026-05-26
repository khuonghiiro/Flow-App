using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class ScreenPositionPickerNodeDialog : BaseNodeDialog
    {
        private readonly ScreenPositionPickerNodeDialogViewModel _viewModel;
        private readonly ScreenPositionPickerNode _node;
        private readonly Window? _ownerWindow;

        // Giữ tham chiếu tất cả windows cần ẩn khi chọn vị trí
        private readonly List<Window> _windowsToHide = new();

        public ScreenPositionPickerNodeDialog(ScreenPositionPickerNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _ownerWindow = owner;
            InitializeComponent();

            _viewModel = new ScreenPositionPickerNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            UpdateTitleColorPreview();
            RefreshPositionDisplay();

            // Sync combobox selection khi VM thay đổi MouseAction
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScreenPositionPickerNodeDialogViewModel.MouseAction))
                    SyncMouseActionComboBox();
            };
        }

        protected override Panel? GetInputsPanel()  => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void OnLoaded()
        {
            base.OnLoaded();
            RefreshPositionDisplay();
            SyncMouseActionComboBox();
        }

        // ── Sync combobox chọn đúng item theo enum value ──
        private void SyncMouseActionComboBox()
        {
            if (MouseActionComboBox == null) return;
            foreach (var item in MouseActionComboBox.Items)
            {
                if (item is MouseActionOption opt && opt.Value == _viewModel.MouseAction)
                {
                    MouseActionComboBox.SelectedItem = item;
                    return;
                }
            }
        }

        // ── Handler khi user đổi combobox hành động chuột ──
        private void MouseActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MouseActionComboBox?.SelectedItem is MouseActionOption opt)
                _viewModel.MouseAction = opt.Value;
        }

        // ── Nút chọn vị trí thủ công — ẩn toàn bộ app ──
        private void PickPositionButton_Click(object sender, RoutedEventArgs e)
        {
            // Thu thập tất cả windows đang hiển thị để ẩn
            _windowsToHide.Clear();
            foreach (Window w in Application.Current.Windows)
            {
                if (w.IsVisible)
                    _windowsToHide.Add(w);
            }

            // Ẩn tất cả
            foreach (var w in _windowsToHide)
                w.Hide();

            try
            {
                var overlay = new ScreenPositionPickerOverlay();
                var result = overlay.ShowDialog();
                if (result == true && overlay.SelectedPosition.HasValue)
                {
                    _node.SelectedPosition = overlay.SelectedPosition.Value;
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
                // Hiện lại tất cả windows theo thứ tự ngược (dialog trước, app sau)
                foreach (var w in _windowsToHide)
                    w.Show();

                // Đưa focus về dialog này
                Activate();
            }
        }

        private void RefreshPositionDisplay()
        {
            if (PositionDisplayText != null)
                PositionDisplayText.Text = _node.PositionText;
        }

        protected override void BeforeSaveOnClose()
        {
            // Flush numeric textbox bindings trước khi lưu
            FlushTextBoxBinding(ClickCountTextBox);
            FlushTextBoxBinding(HoldDurationTextBox);
            FlushTextBoxBinding(ScrollCountTextBox);
            FlushTextBoxBinding(ScrollIntervalTextBox);
        }

        private static void FlushTextBoxBinding(TextBox? tb)
            => tb?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
}
