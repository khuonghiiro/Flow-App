using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using FlowMy.Views.Overlays;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class ActionCanVasNodeDialog : BaseNodeDialog
    {
        private readonly ActionCanVasNodeDialogViewModel _viewModel;

        public ActionCanVasNodeDialog(ActionCanVasNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new ActionCanVasNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            // Gọi nếu XAML có TitleColorPreview + TitleColorComboBox
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        // CHỈ override nếu cần flush binding khi đóng bằng Alt+F4 hoặc X taskbar
        // protected override void BeforeSaveOnClose()
        // {
        //     MyComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
        // }
    }
}
