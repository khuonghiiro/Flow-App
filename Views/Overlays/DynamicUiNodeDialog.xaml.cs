using System;
using System.Windows;
using System.Windows.Controls;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;

namespace FlowMy.Views.Overlays
{
    public partial class DynamicUiNodeDialog : BaseNodeDialog
    {
        private readonly DynamicUiNodeDialogViewModel _viewModel;

        public DynamicUiNodeDialog(DynamicUiNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new DynamicUiNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            // If we have color picker previews, update them
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => null;

        protected override Panel? GetOutputsPanel() => null;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
