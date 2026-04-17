using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays;

public partial class FlowOverwriteNodeDialog : BaseNodeDialog
{
    private readonly FlowOverwriteNodeDialogViewModel _viewModel;

    public FlowOverwriteNodeDialog(FlowOverwriteNode node, IWorkflowEditorHost host, Window? owner)
        : base()
    {
        InitializeComponent();
        _viewModel = new FlowOverwriteNodeDialogViewModel(node, host);
        InitializeBase(_viewModel, owner);
    }

    protected override Panel? GetInputsPanel() => InputsPanel;
    protected override Panel? GetOutputsPanel() => OutputsPanel;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveTitleCommand.Execute(null);
        Close();
    }

    private void AddSource_Click(object sender, RoutedEventArgs e) => _viewModel.AddSource();

    private void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FlowOverwriteSourceItemViewModel vm)
            _viewModel.RemoveSource(vm);
    }
}
