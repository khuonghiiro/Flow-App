using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class StorageNodeDialog : BaseNodeDialog
    {
        private readonly StorageNodeDialogViewModel _viewModel;

        public StorageNodeDialog(StorageNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _viewModel = new StorageNodeDialogViewModel(node, host);

            InitializeBase(_viewModel, owner);

            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => null;

    }
}

