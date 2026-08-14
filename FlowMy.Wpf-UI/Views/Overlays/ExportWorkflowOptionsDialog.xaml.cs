using System.Windows;
using System.Windows.Controls;
using System.IO.Compression;

namespace FlowMy.Views.Overlays
{
    public partial class ExportWorkflowOptionsDialog : Window
    {
        public ExportWorkflowOptionsDialog()
        {
            InitializeComponent();
            UpdateWebBundleOptionAvailability();
        }

        public string SelectedFormat
        {
            get
            {
                if (ExportFormatComboBox != null &&
                    ExportFormatComboBox.SelectedItem is ComboBoxItem item &&
                    item.Tag is string tag &&
                    !string.IsNullOrWhiteSpace(tag))
                {
                    return tag;
                }

                return "json";
            }
        }

        public bool IncludeRuntimeOutput => IncludeRuntimeOutputCheckBox.IsChecked == true;
        public bool IncludeWebCookies => IncludeWebCookiesCheckBox.IsChecked == true;
        public CompressionLevel SelectedCompressionLevel
        {
            get
            {
                var mode = SelectedCompressionMode;
                return mode switch
                {
                    "light" => CompressionLevel.Fastest,
                    "strong" => CompressionLevel.SmallestSize,
                    _ => CompressionLevel.Optimal
                };
            }
        }

        public string SelectedCompressionMode
        {
            get
            {
                if (CompressionModeComboBox != null &&
                    CompressionModeComboBox.SelectedItem is ComboBoxItem item &&
                    item.Tag is string tag &&
                    !string.IsNullOrWhiteSpace(tag))
                {
                    return tag;
                }

                return "medium";
            }
        }

        public string SelectedProfile
        {
            get
            {
                if (ProfileComboBox != null &&
                    ProfileComboBox.SelectedItem is ComboBoxItem item &&
                    item.Tag is string tag &&
                    !string.IsNullOrWhiteSpace(tag))
                {
                    return tag;
                }
                return "All";
            }
        }

        public void PopulateProfiles(System.Collections.Generic.IEnumerable<string>? availableProfiles = null)
        {
            if (ProfileComboBox == null) return;
            ProfileComboBox.Items.Clear();
            ProfileComboBox.Items.Add(new ComboBoxItem { Content = "Tất cả profile (Khuyên dùng)", Tag = "All", IsSelected = true });

            var profiles = availableProfiles?.Distinct(System.StringComparer.OrdinalIgnoreCase).ToList()
                ?? FlowMy.Services.Workflow.WebNodeCacheHelper.GetAvailableCacheProfiles();

            foreach (var p in profiles)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                ProfileComboBox.Items.Add(new ComboBoxItem { Content = $"Profile '{p}'", Tag = p });
            }
            ProfileComboBox.SelectedIndex = 0;
        }

        private void IncludeWebCookiesCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ProfileSelectPanel == null || IncludeWebCookiesCheckBox == null) return;
            ProfileSelectPanel.Visibility = IncludeWebCookiesCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ExportFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateWebBundleOptionAvailability();
        }

        private void UpdateWebBundleOptionAvailability()
        {
            // SelectionChanged có thể bắn ra ngay trong InitializeComponent
            // khi một số control phía dưới chưa được tạo xong.
            if (WebCookiesPanel == null ||
                IncludeWebCookiesCheckBox == null ||
                CompressionModeComboBox == null ||
                CompressionHintTextBlock == null)
            {
                return;
            }

            var isWebBundle = SelectedFormat == "webpkg";
            var isCompressedOutput = isWebBundle || SelectedFormat == "flowz";

            // Ẩn hoàn toàn checkbox web cookies khi chọn JSON; hiện khi flowz hoặc webpkg
            WebCookiesPanel.Visibility = isCompressedOutput
                ? Visibility.Visible
                : Visibility.Collapsed;

            CompressionModeComboBox.IsEnabled = isCompressedOutput;
            CompressionHintTextBlock.Opacity = isCompressedOutput ? 1.0 : 0.6;

            if (!isCompressedOutput)
            {
                IncludeWebCookiesCheckBox.IsChecked = false;
            }

            if (ProfileSelectPanel != null)
            {
                ProfileSelectPanel.Visibility = (isCompressedOutput && IncludeWebCookiesCheckBox.IsChecked == true)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
