using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class StartEndNodeDialog : BaseNodeDialog
    {
        private readonly StartEndNodeDialogViewModel _viewModel;

        public StartEndNodeDialog(WorkflowNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new StartEndNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private new void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }
    }
}
