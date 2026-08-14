// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
}
