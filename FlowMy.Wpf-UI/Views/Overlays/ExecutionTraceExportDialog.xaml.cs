using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Dialog cấu hình xuất execution log JSON. Bind trực tiếp vào WorkflowEditorViewModel
    /// (ExportIncludeInput/Output/Error, ExportMaxFieldLength, ExportIncludeTree, ...)
    /// nên không cần code-behind giữ state riêng.
    /// </summary>
    public partial class ExecutionTraceExportDialog : Window
    {
        private static readonly Regex DigitsOnly = new("^[0-9]+$", RegexOptions.Compiled);

        public ExecutionTraceExportDialog()
        {
            InitializeComponent();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void MaxLengthTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DigitsOnly.IsMatch(e.Text);
        }
    }
}
