using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class ConditionalNodeDialog : BaseNodeDialog
    {
        private readonly ConditionalNodeDialogViewModel _viewModel;

        public ConditionalNodeDialog(WorkflowNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new ConditionalNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveTitleCommand.Execute(null);
            Close();
        }
    }
}
