using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using FlowMy.Views.Overlays;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class ShowInputMsgNodeDialog : BaseNodeDialog
    {
        private readonly ShowInputMsgNodeDialogViewModel _viewModel;

        public ShowInputMsgNodeDialog(ShowInputMsgNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new ShowInputMsgNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            // Gọi nếu XAML có TitleColorPreview + TitleColorComboBox
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        // CHỈ override nếu cần flush binding khi đóng bằng Alt+F4 hoặc X taskbar
        // protected override void BeforeSaveOnClose()
        // {
        //     MyComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
        // }
    }
}
