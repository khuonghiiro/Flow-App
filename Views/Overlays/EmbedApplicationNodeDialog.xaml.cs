using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class EmbedApplicationNodeDialog : BaseNodeDialog
    {
        private readonly EmbedApplicationNodeDialogViewModel _viewModel;

        public EmbedApplicationNodeDialog(EmbedApplicationNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new EmbedApplicationNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void BeforeSaveOnClose()
        {
            ApplicationComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
            TitleTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }
}
