using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class AsyncTaskNodeDialog : BaseNodeDialog
    {
        private readonly AsyncTaskNodeDialogViewModel _viewModel;

        public AsyncTaskNodeDialog(WorkflowNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new AsyncTaskNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
            
            // Update explanation text khi checkbox thay đổi
            UpdateExecutionModeExplanation();
            RunInParallelCheckBox.Checked += (s, e) => UpdateExecutionModeExplanation();
            RunInParallelCheckBox.Unchecked += (s, e) => UpdateExecutionModeExplanation();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void UpdateExecutionModeExplanation()
        {
            if (ExecutionModeExplanation == null) return;
            
            bool isParallel = RunInParallelCheckBox.IsChecked == true;
            if (isParallel)
            {
                ExecutionModeExplanation.Text = "Song song: Task1 (3s) + Task2 (2s) + Task3 (4s) = 4 giây (lấy task lâu nhất)";
            }
            else
            {
                ExecutionModeExplanation.Text = "Tuần tự: Task1 (3s) + Task2 (2s) + Task3 (4s) = 9 giây (cộng dồn)";
            }
        }

        private new void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }
    }
}

