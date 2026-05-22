using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Dialog cấu hình cho MacroRecorderNode.
    /// Kế thừa BaseNodeDialog, có 2 tab: "Logic" và "Cấu hình".
    /// </summary>
    public partial class MacroRecorderNodeDialog : BaseNodeDialog
    {
        private readonly MacroRecorderNodeDialogViewModel _viewModel;

        public MacroRecorderNodeDialog(MacroRecorderNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _viewModel = new MacroRecorderNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            UpdateTitleColorPreview();

            // Wire visual mode hint
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MacroRecorderNodeDialogViewModel.SelectedVisualPlaybackMode))
                    UpdateVisualModeHint();
            };
            UpdateVisualModeHint();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        // ─── Button: Ghi lại thao tác ────────────────────────────────────────────

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Minimize app main window
            var appWindow = Application.Current.MainWindow;
            if (appWindow != null)
                appWindow.WindowState = WindowState.Minimized;

            // 2. Hide this dialog so it doesn't block the overlay
            this.Hide();

            // 3. Show overlay
            var overlay = new MacroRecorderOverlay();
            overlay.ShowDialog();

            // 4. Restore app window and re-show dialog
            if (appWindow != null)
            {
                appWindow.WindowState = WindowState.Normal;
                appWindow.Activate();
            }
            this.Show();
            this.Activate();

            // 5. Update MacroDataJson if overlay has data
            if (overlay.HasData && overlay.RecordedJson != null)
            {
                _viewModel.MacroDataJson = overlay.RecordedJson;
            }
        }

        // ─── Button: Export JSON ──────────────────────────────────────────────────

        private void ExportJsonButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_viewModel.MacroDataJson))
                return;

            var dialog = new SaveFileDialog
            {
                Title = "Xuất dữ liệu macro ra file JSON",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = "macro_data"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, _viewModel.MacroDataJson, System.Text.Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Không thể lưu file:\n{ex.Message}",
                        "Lỗi xuất JSON",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        // ─── Button: Import JSON ──────────────────────────────────────────────────

        private void ImportJsonButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Nhập dữ liệu macro từ file JSON",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var content = File.ReadAllText(dialog.FileName, System.Text.Encoding.UTF8);

                    // Validate: phải là JSON hợp lệ và là array
                    if (!IsValidJsonArray(content))
                    {
                        MessageBox.Show(
                            "File không hợp lệ. Nội dung phải là một JSON array (ví dụ: [{...}, {...}]).",
                            "Lỗi nhập JSON",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    _viewModel.MacroDataJson = content;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Không thể đọc file:\n{ex.Message}",
                        "Lỗi nhập JSON",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private void UpdateVisualModeHint()
        {
            if (VisualModeHint == null) return;
            VisualModeHint.Text = _viewModel.SelectedVisualPlaybackMode switch
            {
                "Không hiển thị"     => "Chạy thao tác ngầm, không hiển thị gì lên màn hình.",
                "Hiển thị trực tiếp" => "Hiển thị marker tại vị trí thao tác khi nó xảy ra, rồi mờ dần.",
                "Hiển thị luồng sẵn" => "Vẽ sẵn toàn bộ luồng lên màn hình, thao tác đến đâu marker biến mất đến đó.",
                _                    => ""
            };
        }

        /// <summary>
        /// Kiểm tra chuỗi có phải JSON hợp lệ và là array không.
        /// </summary>
        private static bool IsValidJsonArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == JsonValueKind.Array;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
