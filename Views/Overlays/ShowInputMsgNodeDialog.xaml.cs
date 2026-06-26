using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        protected override void BeforeSaveOnClose()
        {
            try
            {
                HtmlEditor?.ForceUpdateBinding();
                JsEditor?.ForceUpdateBinding();
                CssEditor?.ForceUpdateBinding();
                ParamsEditor?.ForceUpdateBinding();
            }
            catch { }

            if (Keyboard.FocusedElement is UIElement element)
            {
                element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }
    }
}
