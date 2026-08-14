using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class CodeNodeDialog : BaseNodeDialog
    {
        private readonly CodeNodeDialogViewModel _viewModel;

        public CodeNodeDialog(CodeNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new CodeNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
            _viewModel.RefreshAvailableNodes();
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void ExpandCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var popup = new CodeEditorPopupWindow(_viewModel, this);
            popup.ShowDialog();
        }
    }
}
