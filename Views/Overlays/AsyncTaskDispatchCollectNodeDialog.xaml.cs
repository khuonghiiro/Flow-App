using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class AsyncTaskDispatchCollectNodeDialog : BaseNodeDialog
    {
        private readonly AsyncTaskDispatchCollectNodeDialogViewModel _viewModel;

        public AsyncTaskDispatchCollectNodeDialog(AsyncTaskDispatchCollectNode node, IWorkflowEditorHost host, Window? owner)
        {
            InitializeComponent();

            _viewModel = new AsyncTaskDispatchCollectNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => null;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }
    }
}

