using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class OutputNodeDialog : BaseNodeDialog
    {
        private readonly OutputNodeDialogViewModel _viewModel;

        public OutputNodeDialog(OutputNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            // Initialize ViewModel
            _viewModel = new OutputNodeDialogViewModel(node, host);

            // Kết nối với BaseNodeDialog
            InitializeBase(_viewModel, owner);

            // Update title color preview
            UpdateTitleColorPreview();

            // Update empty state visibility
            UpdateEmptyState();
            _viewModel.Variables.CollectionChanged += (s, e) => UpdateEmptyState();
        }

        protected override Panel? GetInputsPanel() => null; // OutputNode không có inputs section
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void UpdateEmptyState()
        {
            if (EmptyStateText != null && VariablesItemsControl != null)
            {
                EmptyStateText.Visibility = _viewModel.Variables.Count == 0 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }
    }
}

