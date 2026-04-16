using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays;

public partial class KeyScopedNodeDialog : BaseNodeDialog
{
    private readonly KeyScopedNodeDialogViewModel _viewModel;

    public KeyScopedNodeDialog(KeyScopedNode node, IWorkflowEditorHost host, Window? owner)
        : base()
    {
        InitializeComponent();

        _viewModel = new KeyScopedNodeDialogViewModel(node, host);
        InitializeBase(_viewModel, owner);
        UpdateTitleColorPreview();
    }

    protected override Panel? GetInputsPanel() => InputsPanel;
    protected override Panel? GetOutputsPanel() => OutputsPanel;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveTitleCommand.Execute(null);
        Close();
    }

    private void TitleColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateTitleColorPreview();
    }

    private void UpdateTitleColorPreview()
    {
        if (TitleColorPreview == null) return;

        var colorKey = _viewModel.TitleColorKey;
        Brush? brush = null;

        if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            brush = _viewModel.Node?.NodeBrush;
        else if (colorKey == "LimeGreen")
            brush = new SolidColorBrush(Colors.LimeGreen);
        else
            brush = Application.Current.TryFindResource(colorKey) as Brush;

        TitleColorPreview.Background = brush ?? new SolidColorBrush(Colors.Gray);
    }
}
