using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FlowMy.Services.Utils;
using FlowMy.ViewModels;

namespace FlowMy.Views.Overlays
{
    public partial class CustomPresetManagerDialog : Window
    {
        private ObservableCollection<AssetPreset> _presets = new();

        // Đang ở chế độ Edit: lưu preset gốc để update đúng
        private AssetPreset? _editingPreset = null;

        public CustomPresetManagerDialog()
        {
            InitializeComponent();
            LoadPresets();
            MouseLeftButtonDown += (_, _) => DragMove();
        }

        // ─────────────────── Load ───────────────────────────────
        private void LoadPresets()
        {
            _presets = new(CustomPresetService.Load());
            PresetListControl.ItemsSource = _presets;
            UpdateCountText();
        }

        private void UpdateCountText()
        {
            CountText.Text = $"{_presets.Count} preset tùy chỉnh";
        }

        // ─────────────────── Save form ──────────────────────────
        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            var title    = FormTitleBox.Text.Trim();
            var url      = FormUrlBox.Text.Trim();
            var desc     = FormDescBox.Text.Trim();
            var fileName = FormFileNameBox.Text.Trim();
            var typeStr  = (FormTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "js";

            if (string.IsNullOrWhiteSpace(title))
            {
                ShowStatus("⚠ Vui lòng nhập Tên hiển thị.", isError: true);
                return;
            }

            // Auto-generate fileName nếu thiếu
            if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(url))
            {
                try { fileName = System.IO.Path.GetFileName(new Uri(url).LocalPath); } catch { }
            }
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"{title.Replace(" ", "_").ToLower()}.{typeStr}";

            var preset = new AssetPreset(title, desc, url, fileName, typeStr);

            if (_editingPreset != null)
            {
                // Update
                var idx = _presets.IndexOf(_presets.FirstOrDefault(p =>
                    p.FileName == _editingPreset.FileName && p.Type == _editingPreset.Type)!);
                if (idx >= 0) _presets[idx] = preset;
                CustomPresetService.UpdatePreset(_editingPreset, preset);
                ShowStatus($"✅ Đã cập nhật preset '{title}'.");
                ExitEditMode();
            }
            else
            {
                // Add new
                _presets.Add(preset);
                CustomPresetService.AddPreset(preset);
                ShowStatus($"✅ Đã thêm preset '{title}'.");
            }

            ClearForm();
            UpdateCountText();
        }

        // ─────────────────── Edit ───────────────────────────────
        private void EditPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AssetPreset preset)
            {
                _editingPreset = preset;

                FormTitleBox.Text    = preset.Title;
                FormUrlBox.Text      = preset.Url;
                FormDescBox.Text     = preset.Description;
                FormFileNameBox.Text = preset.FileName;
                FormTypeCombo.SelectedIndex = preset.Type?.ToLower() == "css" ? 1 : 0;

                FormTitle.Text = "✏ Đang sửa preset";
                CancelEditButton.Visibility = Visibility.Visible;
                SavePresetButton.Content = "💾 Cập nhật";
                FormTitleBox.Focus();
                HideStatus();
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
            ClearForm();
            HideStatus();
        }

        private void ExitEditMode()
        {
            _editingPreset = null;
            FormTitle.Text = "➕ Thêm preset mới";
            CancelEditButton.Visibility = Visibility.Collapsed;
            SavePresetButton.Content = "💾 Lưu";
        }

        // ─────────────────── Delete ─────────────────────────────
        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AssetPreset preset)
            {
                var result = MessageBox.Show(
                    $"Xóa preset '{preset.Title}'?\nHành động này không thể hoàn tác.",
                    "Xác nhận xóa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _presets.Remove(preset);
                    CustomPresetService.RemovePreset(preset);
                    UpdateCountText();
                    ShowStatus($"🗑 Đã xóa preset '{preset.Title}'.");
                    if (_editingPreset?.FileName == preset.FileName)
                        ExitEditMode();
                }
            }
        }

        // ─────────────────── Helpers ────────────────────────────
        private void ClearForm()
        {
            FormTitleBox.Text    = string.Empty;
            FormUrlBox.Text      = string.Empty;
            FormDescBox.Text     = string.Empty;
            FormFileNameBox.Text = string.Empty;
            FormTypeCombo.SelectedIndex = 0;
        }

        private void ShowStatus(string msg, bool isError = false)
        {
            FormStatus.Text = msg;
            FormStatus.Foreground = isError
                ? System.Windows.Media.Brushes.Salmon
                : System.Windows.Media.Brushes.MediumSeaGreen;
            FormStatus.Visibility = Visibility.Visible;
        }

        private void HideStatus() => FormStatus.Visibility = Visibility.Collapsed;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
