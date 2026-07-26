using System.Windows;
using System.Windows.Controls;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;

namespace FlowMy.Views.Overlays
{
    public partial class AsyncTaskBodyDialog : BaseNodeDialog
    {
        private readonly AsyncTaskBodyDialogViewModel _viewModel;

        public AsyncTaskBodyDialog(AsyncTaskBodyNode bodyNode, IWorkflowEditorHost host, Window? owner)
        {
            InitializeComponent();

            _viewModel = new AsyncTaskBodyDialogViewModel(bodyNode, host);
            InitializeBase(_viewModel, owner);
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => null;

        protected override void BeforeSaveOnClose()
        {
            _viewModel.SaveChanges();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveChanges();
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
