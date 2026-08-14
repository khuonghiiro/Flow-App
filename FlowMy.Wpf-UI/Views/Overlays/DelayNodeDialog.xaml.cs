using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowMy.Views.Overlays
{
    public partial class DelayNodeDialog : BaseNodeDialog
    {
        private readonly DelayNodeDialogViewModel _viewModel;

        public DelayNodeDialog(DelayNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _viewModel = new DelayNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            // Update title color preview
            UpdateTitleColorPreview();

            // Validate numeric input for DelayValue
            DelayValueTextBox.PreviewTextInput += DelayValueTextBox_PreviewTextInput;
            DataObject.AddPastingHandler(DelayValueTextBox, DelayValueTextBox_OnPaste);

            DelayValueTextBox.LostFocus += (s, e) => NormalizeDelayValueText();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private static readonly Regex _numberRegex = new Regex(@"^[0-9]*([.,][0-9]*)?$");

        private void DelayValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb) return;

            // Simulate the new text after typing
            var text = tb.Text ?? string.Empty;
            var selectionStart = tb.SelectionStart;
            var selectionLength = tb.SelectionLength;

            var newText = text.Remove(selectionStart, selectionLength).Insert(selectionStart, e.Text);
            e.Handled = !_numberRegex.IsMatch(newText);
        }

        private void DelayValueTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.Text, true)) { e.CancelCommand(); return; }
            var pasteText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;

            if (sender is not TextBox tb) return;

            var text = tb.Text ?? string.Empty;
            var selectionStart = tb.SelectionStart;
            var selectionLength = tb.SelectionLength;

            var newText = text.Remove(selectionStart, selectionLength).Insert(selectionStart, pasteText);
            if (!_numberRegex.IsMatch(newText))
                e.CancelCommand();
        }

        private void NormalizeDelayValueText()
        {
            // Nếu binding parse fail, ViewModel vẫn giữ giá trị cũ. Ta chỉ normalize UI nhẹ nhàng.
            if (DelayValueTextBox == null) return;
            var t = (DelayValueTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(t))
            {
                DelayValueTextBox.Text = _viewModel.DelayValue.ToString(System.Globalization.CultureInfo.CurrentCulture);
            }
        }

    }
}

