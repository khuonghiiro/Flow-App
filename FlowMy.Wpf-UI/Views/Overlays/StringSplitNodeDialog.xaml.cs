using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class StringSplitNodeDialog : BaseNodeDialog
    {
        private readonly StringSplitNodeDialogViewModel _viewModel;

        public StringSplitNodeDialog(StringSplitNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            // Initialize ViewModel
            _viewModel = new StringSplitNodeDialogViewModel(node, host);

            // ⚠️ CRITICAL: Kết nối với BaseNodeDialog
            InitializeBase(_viewModel, owner);

            // Initialize title color preview
            UpdateTitleColorPreview();
        }

        // ⚠️ CRITICAL: Override để BaseNodeDialog biết panels nào để populate
        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;
    }
}
