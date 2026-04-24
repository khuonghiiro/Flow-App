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

        private void ExportFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateWebBundleOptionAvailability();
        }

        private void UpdateWebBundleOptionAvailability()
        {
            // SelectionChanged có thể bắn ra ngay trong InitializeComponent
            // khi một số control phía dưới chưa được tạo xong.
            if (IncludeWebCookiesCheckBox == null ||
                CompressionModeComboBox == null ||
                CompressionHintTextBlock == null)
            {
                return;
            }

            var isWebBundle = SelectedFormat == "webpkg";
            var isCompressedOutput = isWebBundle || SelectedFormat == "flowz";
            IncludeWebCookiesCheckBox.IsEnabled = isWebBundle;
            CompressionModeComboBox.IsEnabled = isCompressedOutput;
            CompressionHintTextBlock.Opacity = isCompressedOutput ? 1.0 : 0.6;

            if (!isWebBundle)
            {
                IncludeWebCookiesCheckBox.IsChecked = false;
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
