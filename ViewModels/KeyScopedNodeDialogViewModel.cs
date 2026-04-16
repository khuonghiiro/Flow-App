using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels;

public sealed record PollUnitOptionItem(KeyScopedPollUnit Unit, string Label);

public partial class KeyScopedNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly KeyScopedNode _keyNode;
    private bool _suppressWriteModeSync;

    [ObservableProperty]
    private bool _isWriteMode = true;

    [ObservableProperty]
    private string _staticKey = string.Empty;

    [ObservableProperty]
    private int _pollTimeValue;

    [ObservableProperty]
    private KeyScopedPollUnit _pollUnit = KeyScopedPollUnit.Milliseconds;

    public ObservableCollection<PollUnitOptionItem> PollUnitOptions { get; } = new()
    {
        new(KeyScopedPollUnit.Milliseconds, "Milli giây"),
        new(KeyScopedPollUnit.Seconds, "Giây"),
        new(KeyScopedPollUnit.Minutes, "Phút")
    };

    public KeyScopedNodeDialogViewModel(KeyScopedNode node, IWorkflowEditorHost host)
        : base(node, host)
    {
        _keyNode = node ?? throw new System.ArgumentNullException(nameof(node));
        _suppressWriteModeSync = true;
        _isWriteMode = node.IsWriteMode;
        _staticKey = node.StaticKey ?? string.Empty;
        _pollTimeValue = node.PollTimeValue;
        _pollUnit = node.PollUnit;
        _suppressWriteModeSync = false;

        if (node is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(KeyScopedNode.IsWriteMode))
                    IsWriteMode = _keyNode.IsWriteMode;
                else if (e.PropertyName == nameof(KeyScopedNode.StaticKey))
                    StaticKey = _keyNode.StaticKey ?? string.Empty;
                else if (e.PropertyName == nameof(KeyScopedNode.PollTimeValue))
                    PollTimeValue = _keyNode.PollTimeValue;
                else if (e.PropertyName == nameof(KeyScopedNode.PollUnit))
                    PollUnit = _keyNode.PollUnit;
                OnNodePropertyChanged(e.PropertyName ?? string.Empty);
            };
        }

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StaticKey) && _keyNode.StaticKey != StaticKey)
                _keyNode.StaticKey = StaticKey;
            else if (e.PropertyName == nameof(PollTimeValue) && _keyNode.PollTimeValue != PollTimeValue)
                _keyNode.PollTimeValue = PollTimeValue;
            else if (e.PropertyName == nameof(PollUnit) && _keyNode.PollUnit != PollUnit)
                _keyNode.PollUnit = PollUnit;
        };
    }

    protected override string GetDefaultTitle() => "Key Scoped";

    partial void OnIsWriteModeChanged(bool value)
    {
        if (_suppressWriteModeSync) return;
        _keyNode.IsWriteMode = value;
        LoadInputs();

        _host.Dispatcher.BeginInvoke(new System.Action(() =>
        {
            foreach (var port in _keyNode.Ports)
            {
                if (port.PortUI != null && _host.WorkflowCanvas != null)
                {
                    if (_host.WorkflowCanvas.Children.Contains(port.PortUI))
                        _host.WorkflowCanvas.Children.Remove(port.PortUI);
                    port.PortUI = null;
                }
            }

            foreach (var port in _keyNode.Ports)
            {
                var show = !port.IsInput || _keyNode.IsWriteMode;
                port.IsVisible = show;
            }

            if (_host.ViewModel != null)
            {
                var toRemove = _host.ViewModel.Connections
                    .Where(c =>
                        (c.FromNode == _keyNode && c.FromPort != null && !c.FromPort.IsVisible) ||
                        (c.ToNode == _keyNode && c.ToPort != null && !c.ToPort.IsVisible))
                    .ToList();

                foreach (var conn in toRemove)
                {
                    _host.ConnectionRenderer.RemoveConnectionVisuals(conn);
                    _host.ViewModel.Connections.Remove(conn);
                }
            }

            _host.UpdateNodePosition(_keyNode, _keyNode.X, _keyNode.Y);

            if (_host.ViewModel != null)
            {
                foreach (var conn in _host.ViewModel.Connections.Where(c =>
                             c.FromNode == _keyNode || c.ToNode == _keyNode))
                    _host.RenderConnection(conn);
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);

        _host.RequestSyncDataPanels(immediate: true);
    }

    protected override void OnSaveTitle()
    {
        _keyNode.NotifyTitleChanged();

        _keyNode.IsWriteMode = IsWriteMode;
        _keyNode.StaticKey = StaticKey ?? string.Empty;
        _keyNode.PollTimeValue = PollTimeValue;
        _keyNode.PollUnit = PollUnit;
        _host.RequestSyncDataPanels(immediate: true);
    }

    protected override void LoadInputs()
    {
        Inputs.Clear();
        if (_node is not KeyScopedNode kn || !kn.IsWriteMode) return;
        if (kn.DynamicInputs == null || kn.DynamicInputs.Count == 0) return;

        RefreshAvailableSourcesForInputs();

        foreach (var input in kn.DynamicInputs)
        {
            var inputVm = new InputItemViewModel(kn, input, _host);
            if (input.AvailableSources != null)
                inputVm.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(input.AvailableSources);
            Inputs.Add(inputVm);
        }
    }
}
