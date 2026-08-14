using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class ListOutNodeDialog : BaseNodeDialog
    {
        private readonly ListOutNodeDialogViewModel _viewModel;

        public ListOutNodeDialog(ListOutNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            // Initialize ViewModel
            _viewModel = new ListOutNodeDialogViewModel(node, host);

            // Kết nối với BaseNodeDialog
            InitializeBase(_viewModel, owner);

            // Update title color preview
            UpdateTitleColorPreview();

            // Update empty state visibility
            UpdateEmptyState();
            _viewModel.Mappings.CollectionChanged += (s, e) => UpdateEmptyState();
        }

        protected override Panel? GetInputsPanel() => null; // ListOutNode không có inputs section
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void UpdateEmptyState()
        {
            if (EmptyStateText != null && MappingsItemsControl != null)
            {
                EmptyStateText.Visibility = _viewModel.Mappings.Count == 0 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }
    }
}

