using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FlowMy.Views.Overlays;

public partial class KeyValueBridgeNodeDialog : BaseNodeDialog
{
    private readonly KeyValueBridgeNodeDialogViewModel _viewModel;

    /// <summary>Gọi từ canvas khi một KeyValueBridge khác đổi Pass/Cleanup — cập nhật danh sách node đích xóa KV.</summary>
    public void RefreshDeleteBridgeNodeOptionsForOpenDialog() => _viewModel.RefreshDeleteBridgeNodeOptions();

    public KeyValueBridgeNodeDialog(KeyValueBridgeNode node, IWorkflowEditorHost host, Window? owner)
        : base()
    {
        InitializeComponent();

        _viewModel = new KeyValueBridgeNodeDialogViewModel(node, host);
        InitializeBase(_viewModel, owner);
        UpdateTitleColorPreview();
        Loaded += (_, _) =>
        {
            _viewModel.RefreshPassBridgeOptions();
            _viewModel.RefreshKnownKeys();

            // Combo cleanup / node đích xóa dùng TwoWay: Clear ItemsSource có thể ghi null xuống node trước khi restore chọn.
            _viewModel.BeginSuppressCleanupBridgeSync();
            try
            {
                _viewModel.RefreshDeleteBridgeNodeOptions();
                _viewModel.RefreshCleanupTriggerSourceOptions();
                _viewModel.RefreshDeleteKeyOptions();
                _viewModel.RefreshCleanupTriggerOutputKeyOptions();
                _viewModel.RefreshCleanupKeySourceOptions();
                _viewModel.RefreshCleanupKeySourceOutputKeyOptions();
                _viewModel.RefreshCleanupFilterFieldSourceOptions();
                _viewModel.RefreshCleanupFilterFieldSourceOutputKeyOptions();
                _viewModel.RefreshCleanupFilterValueSourceOptions();
                _viewModel.RefreshCleanupFilterValueSourceOutputKeyOptions();
            }
            finally
            {
                _viewModel.EndSuppressCleanupBridgeSync();
            }
        };
    }

    private void FlushBindingsBeforeSave()
    {
        KeyCombo?.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
        SourceBridgeCombo?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
    }

    protected override void BeforeSaveOnClose() => FlushBindingsBeforeSave();

    protected override Panel? GetInputsPanel() => InputsPanel;
    protected override Panel? GetOutputsPanel() => OutputsPanel;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        FlushBindingsBeforeSave();
        ViewModel.SaveTitleCommand.Execute(null);
        Close();
    }

    private void AddAdditionalAppendSource_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddAdditionalAppendSource();
    }

    private void RemoveAdditionalAppendSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not KeyValueBridgeAppendSourceItemViewModel item) return;
        _viewModel.RemoveAdditionalAppendSource(item);
    }

    protected override FrameworkElement CreateInputItemUI(InputItemViewModel inputVm)
    {
        var element = base.CreateInputItemUI(inputVm);
        if (element is StackPanel stack && stack.Children.Count > 0 && stack.Children[0] is TextBlock keyLabel)
        {
            var k = inputVm.Key ?? string.Empty;
            if (string.Equals(k, "kvChannelKeyIn", StringComparison.OrdinalIgnoreCase))
            {
                keyLabel.Text = "Key Identifier";
                keyLabel.ToolTip =
                    "Key dùng để gom nhiều nhánh chạy song song vào cùng một mảng trong KV store.";
            }
            else if (string.Equals(k, "keyIn", StringComparison.OrdinalIgnoreCase))
            {
                keyLabel.Text = "Value (append)";
                keyLabel.ToolTip =
                    "Giá trị của mỗi nhánh sẽ được append vào mảng của Key Identifier.";
            }
        }

        return element;
    }

    protected override FrameworkElement CreateOutputItemUI(OutputItemViewModel outputVm)
    {
        var element = base.CreateOutputItemUI(outputVm);
        if (element is StackPanel stack && stack.Children.Count > 0 && stack.Children[0] is TextBlock keyLabel)
        {
            var k = outputVm.Key ?? string.Empty;
            if (string.Equals(k, "key", StringComparison.OrdinalIgnoreCase))
            {
                keyLabel.Text = "Aggregated (dict {KeyIdentifier: [values...]})";
                keyLabel.ToolTip =
                    "Pass mode: sau khi append nhiều nhánh, output sẽ là JSON dict gom theo Key Identifier.";
            }
            else if (string.Equals(k, "value", StringComparison.OrdinalIgnoreCase))
            {
                keyLabel.Text = "Current snapshot (dict)";
                keyLabel.ToolTip =
                    "Get mode: đọc snapshot từ KV store theo Key Identifier và hiển thị lại dưới dạng JSON dict.";
            }
        }

        return element;
    }
}
