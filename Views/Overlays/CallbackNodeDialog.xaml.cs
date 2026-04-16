using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class CallbackNodeDialog : BaseNodeDialog
    {
        private readonly CallbackNodeDialogViewModel _viewModel;

        public CallbackNodeDialog(CallbackNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            // Khởi tạo ViewModel
            _viewModel = new CallbackNodeDialogViewModel(node, host);

            // Kết nối với BaseNodeDialog
            InitializeBase(_viewModel, owner);
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void ViewModel_PropertyChanged(
            object? sender, 
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
            
            // Xử lý thêm nếu cần
            // if (e.PropertyName == nameof(CallbackNodeDialogViewModel.TargetNodeId))
            // {
            //     // Custom handling
            // }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Lưu trước khi đóng
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }
    }
}
