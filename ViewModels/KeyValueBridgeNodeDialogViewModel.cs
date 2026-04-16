using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Workflow;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace FlowMy.ViewModels;

public sealed record KeyValuePollUnitOptionItem(KeyValueBridgePollUnit Unit, string Label);

public partial class KeyValueBridgeAppendSourceItemViewModel : ObservableObject
{
    private readonly IWorkflowEditorHost _host;
    [ObservableProperty] private string? _selectedSourceNodeId;
    [ObservableProperty] private string? _selectedSourceOutputKey;
    [ObservableProperty] private ObservableCollection<WorkflowDataSourceOption> _availableSources = new();
    [ObservableProperty] private ObservableCollection<WorkflowOutputKeyOption> _availableOutputKeyOptions = new();

    public KeyValueBridgeAppendSourceItemViewModel(IWorkflowEditorHost host)
    {
        _host = host;
    }

    partial void OnSelectedSourceNodeIdChanged(string? value)
    {
        RefreshOutputKeys();
    }

    public void RefreshOutputKeys()
    {
        var vm = _host.ViewModel;
        if (vm == null || string.IsNullOrWhiteSpace(SelectedSourceNodeId))
        {
            AvailableOutputKeyOptions = new ObservableCollection<WorkflowOutputKeyOption>();
            SelectedSourceOutputKey = null;
            return;
        }

        var src = vm.Nodes.FirstOrDefault(n => string.Equals(n.Id, SelectedSourceNodeId, StringComparison.OrdinalIgnoreCase));
        var opts = src?.DynamicOutputs?
            .Where(o => !string.IsNullOrWhiteSpace(o.Key))
            .Select(o => new WorkflowOutputKeyOption { Key = o.Key, Type = o.OutputType })
            .ToList() ?? new List<WorkflowOutputKeyOption>();
        AvailableOutputKeyOptions = new ObservableCollection<WorkflowOutputKeyOption>(opts);

        if (!string.IsNullOrWhiteSpace(SelectedSourceOutputKey) &&
            opts.Any(o => string.Equals(o.Key, SelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedSourceOutputKey = opts.FirstOrDefault()?.Key;
    }
}

public partial class KeyValueBridgeNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly KeyValueBridgeNode _bridge;
    private bool _suppressPassSync;
    private const string NoneSelectionTitle = "(Không chọn)";
    private const string AllKeysTitle = "Lấy tất cả";
    /// <summary>Khi refresh ItemsSource ComboBox, WPF có thể gán SelectedValue = null; không ghi null xuống node.</summary>
    private bool _suppressSelectionSyncToBridge;

    /// <summary>Khi Loaded refresh cleanup combos, binding TwoWay có thể ghi null xuống node trước khi restore lựa chọn.</summary>
    private int _suppressCleanupBridgeSync;

    [ObservableProperty]
    private bool _isPassKeyMode = true;

    [ObservableProperty]
    private string _kvChannelKey = string.Empty;

    [ObservableProperty]
    private string? _selectedSourceBridgeNodeId;

    [ObservableProperty]
    private int _pollIntervalValue;

    [ObservableProperty]
    private KeyValueBridgePollUnit _pollIntervalUnit = KeyValueBridgePollUnit.Milliseconds;

    public ObservableCollection<WorkflowDataSourceOption> PassBridgeSourceOptions { get; } = new();
    public ObservableCollection<WorkflowDataSourceOption> DeleteBridgeNodeOptions { get; } = new();
    public ObservableCollection<WorkflowDataSourceOption> CleanupTriggerSourceOptions { get; } = new();
    public ObservableCollection<WorkflowDataSourceOption> CleanupKeySourceOptions { get; } = new();
    public ObservableCollection<WorkflowOutputKeyOption> CleanupKeySourceOutputKeyOptions { get; } = new();
    public ObservableCollection<WorkflowDataSourceOption> CleanupFilterFieldSourceOptions { get; } = new();
    public ObservableCollection<WorkflowOutputKeyOption> CleanupFilterFieldSourceOutputKeyOptions { get; } = new();
    public ObservableCollection<WorkflowDataSourceOption> CleanupFilterValueSourceOptions { get; } = new();
    public ObservableCollection<WorkflowOutputKeyOption> CleanupFilterValueSourceOutputKeyOptions { get; } = new();

    public ObservableCollection<string> KnownKeyOptions { get; } = new();
    public ObservableCollection<string> DeleteKeyOptions { get; } = new();
    public ObservableCollection<WorkflowOutputKeyOption> CleanupTriggerOutputKeyOptions { get; } = new();
    public ObservableCollection<string> CleanupTriggerExpectedValueOptions { get; } = new() { "true", "false", "1", "0" };

    public ObservableCollection<KeyValuePollUnitOptionItem> PollUnitOptions { get; } = new()
    {
        new(KeyValueBridgePollUnit.Milliseconds, "ms"),
        new(KeyValueBridgePollUnit.Seconds, "sec"),
        new(KeyValueBridgePollUnit.Minutes, "min")
    };

    public ObservableCollection<KeyValueBridgeAppendSourceItemViewModel> AdditionalAppendSources { get; } = new();

    [ObservableProperty] private string? _selectedDeleteBridgeNodeId;
    [ObservableProperty] private string _deleteTargetKey = string.Empty;
    [ObservableProperty] private bool _enableDataCleanup;
    [ObservableProperty] private bool _clearAllDataForSelectedNode;
    [ObservableProperty] private string _arrayFilterField = string.Empty;
    [ObservableProperty] private string _arrayFilterValue = string.Empty;
    [ObservableProperty] private bool _removeAllMatchedArrayItems;
    [ObservableProperty] private string _deleteResultMessage = "(chưa thao tác xóa)";
    [ObservableProperty] private string _deletePreviewJson = "(chưa có dữ liệu để preview)";
    [ObservableProperty] private string _effectiveDeleteKeyPreview = "(n/a)";
    [ObservableProperty] private string _effectiveFilterValuePreview = "(n/a)";
    [ObservableProperty] private string? _cleanupTriggerSourceNodeId;
    [ObservableProperty] private string? _cleanupTriggerSourceOutputKey;
    [ObservableProperty] private string _cleanupTriggerExpectedValue = "true";
    [ObservableProperty] private string? _cleanupKeySourceNodeId;
    [ObservableProperty] private string? _cleanupKeySourceOutputKey;
    [ObservableProperty] private string? _cleanupFilterFieldSourceNodeId;
    [ObservableProperty] private string? _cleanupFilterFieldSourceOutputKey;
    [ObservableProperty] private string? _cleanupFilterValueSourceNodeId;
    [ObservableProperty] private string? _cleanupFilterValueSourceOutputKey;

    public KeyValueBridgeNodeDialogViewModel(KeyValueBridgeNode node, IWorkflowEditorHost host)
        : base(node, host)
    {
        _bridge = node ?? throw new System.ArgumentNullException(nameof(node));
        _suppressPassSync = true;
        _isPassKeyMode = node.IsPassKeyMode;
        _kvChannelKey = node.KvChannelKey ?? string.Empty;
        _selectedSourceBridgeNodeId = node.SelectedSourceBridgeNodeId;
        _pollIntervalValue = node.PollIntervalValue;
        _pollIntervalUnit = node.PollIntervalUnit;
        _enableDataCleanup = node.EnableDataCleanup;
        _selectedDeleteBridgeNodeId = node.CleanupTargetBridgeNodeId;
        _deleteTargetKey = node.CleanupTargetKey ?? string.Empty;
        _clearAllDataForSelectedNode = node.CleanupClearAllNodeData;
        _arrayFilterField = node.CleanupArrayFilterField ?? string.Empty;
        _arrayFilterValue = node.CleanupArrayFilterValue ?? string.Empty;
        _removeAllMatchedArrayItems = node.CleanupRemoveAllMatchedArrayItems;
        _cleanupTriggerSourceNodeId = node.CleanupTriggerSourceNodeId;
        _cleanupTriggerSourceOutputKey = node.CleanupTriggerSourceOutputKey;
        _cleanupTriggerExpectedValue = string.IsNullOrWhiteSpace(node.CleanupTriggerExpectedValue) ? "true" : node.CleanupTriggerExpectedValue;
        _cleanupKeySourceNodeId = node.CleanupKeySourceNodeId;
        _cleanupKeySourceOutputKey = node.CleanupKeySourceOutputKey;
        _cleanupFilterFieldSourceNodeId = node.CleanupFilterFieldSourceNodeId;
        _cleanupFilterFieldSourceOutputKey = node.CleanupFilterFieldSourceOutputKey;
        _cleanupFilterValueSourceNodeId = node.CleanupFilterValueSourceNodeId;
        _cleanupFilterValueSourceOutputKey = node.CleanupFilterValueSourceOutputKey;
        _suppressPassSync = false;

        RefreshPassBridgeOptions();
        RefreshKnownKeys();
        RefreshDeleteBridgeNodeOptions();
        RefreshCleanupTriggerSourceOptions();
        RefreshDeleteKeyOptions();
        RefreshCleanupTriggerOutputKeyOptions();
        RefreshCleanupKeySourceOptions();
        RefreshCleanupKeySourceOutputKeyOptions();
        RefreshCleanupFilterFieldSourceOptions();
        RefreshCleanupFilterFieldSourceOutputKeyOptions();
        RefreshCleanupFilterValueSourceOptions();
        RefreshCleanupFilterValueSourceOutputKeyOptions();
        RefreshEffectiveCleanupPreview();
        LoadAdditionalAppendSources();

        if (node is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (_, EVx) =>
            {
                if (EVx.PropertyName == nameof(KeyValueBridgeNode.IsPassKeyMode))
                    IsPassKeyMode = _bridge.IsPassKeyMode;
                else if (EVx.PropertyName == nameof(KeyValueBridgeNode.KvChannelKey))
                    KvChannelKey = _bridge.KvChannelKey ?? string.Empty;
                else if (EVx.PropertyName == nameof(KeyValueBridgeNode.SelectedSourceBridgeNodeId))
                    SelectedSourceBridgeNodeId = _bridge.SelectedSourceBridgeNodeId;
                else if (EVx.PropertyName == nameof(KeyValueBridgeNode.PollIntervalValue))
                    PollIntervalValue = _bridge.PollIntervalValue;
                else if (EVx.PropertyName == nameof(KeyValueBridgeNode.PollIntervalUnit))
                    PollIntervalUnit = _bridge.PollIntervalUnit;
                OnNodePropertyChanged(EVx.PropertyName ?? string.Empty);
            };
        }

        PropertyChanged += (_, EVx) =>
        {
            if (EVx.PropertyName == nameof(KvChannelKey) && _bridge.KvChannelKey != KvChannelKey)
                _bridge.KvChannelKey = KvChannelKey ?? string.Empty;
            else if (EVx.PropertyName == nameof(PollIntervalValue) && _bridge.PollIntervalValue != PollIntervalValue)
                _bridge.PollIntervalValue = PollIntervalValue;
            else if (EVx.PropertyName == nameof(PollIntervalUnit) && _bridge.PollIntervalUnit != PollIntervalUnit)
                _bridge.PollIntervalUnit = PollIntervalUnit;
            else if (EVx.PropertyName == nameof(SelectedSourceBridgeNodeId) && !_suppressSelectionSyncToBridge &&
                     _bridge.SelectedSourceBridgeNodeId != SelectedSourceBridgeNodeId)
                _bridge.SelectedSourceBridgeNodeId = SelectedSourceBridgeNodeId;
        };
    }

    public void RefreshPassBridgeOptions()
    {
        var preserveSel = SelectedSourceBridgeNodeId ?? _bridge.SelectedSourceBridgeNodeId;
        _suppressSelectionSyncToBridge = true;
        try
        {
            PassBridgeSourceOptions.Clear();
            var nodes = _host.ViewModel?.Nodes;
            if (nodes == null) return;
            foreach (var b in nodes.OfType<KeyValueBridgeNode>())
            {
                if (!b.IsPassKeyMode || string.Equals(b.Id, _bridge.Id, StringComparison.Ordinal)) continue;
                PassBridgeSourceOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = b.Id,
                    Title = string.IsNullOrWhiteSpace(b.Title) ? b.Id : b.Title
                });
            }

            if (!string.IsNullOrWhiteSpace(preserveSel))
                SelectedSourceBridgeNodeId = preserveSel;
        }
        finally
        {
            _suppressSelectionSyncToBridge = false;
        }
    }

    public void RefreshKnownKeys()
    {
        var preserveKey = !string.IsNullOrWhiteSpace(KvChannelKey)
            ? KvChannelKey
            : (_bridge.KvChannelKey ?? string.Empty);
        KnownKeyOptions.Clear();
        var nodes = _host.ViewModel?.Nodes;
        if (nodes == null) return;
        foreach (var k in nodes.OfType<KeyValueBridgeNode>()
                     .Where(b => b.IsPassKeyMode && !string.IsNullOrWhiteSpace(b.KvChannelKey))
                     .Select(b => b.KvChannelKey.Trim())
                     .Distinct())
            KnownKeyOptions.Add(k);

        // Get mode: allow selecting "Lấy tất cả" to read every key in KV store.
        if (!KnownKeyOptions.Any(x => string.Equals(x, AllKeysTitle, System.StringComparison.OrdinalIgnoreCase)))
            KnownKeyOptions.Add(AllKeysTitle);

        if (!string.IsNullOrWhiteSpace(preserveKey))
            KvChannelKey = preserveKey.Trim();
    }

    public void RefreshDeleteBridgeNodeOptions()
    {
        var preserveSelected = SelectedDeleteBridgeNodeId ?? _bridge.CleanupTargetBridgeNodeId;
        DeleteBridgeNodeOptions.Clear();

        var nodes = _host.ViewModel?.Nodes;
        if (nodes == null) return;

        // Cleanup bật: chọn Pass hoặc Get làm đích (không bật cleanup trên node đó), trừ chính node cleanup — để xóa đúng kho kv gốc (Pass) hoặc Get+Pass nguồn.
        if (_bridge.EnableDataCleanup)
        {
            foreach (var b in nodes.OfType<KeyValueBridgeNode>()
                         .Where(b => !string.Equals(b.Id, _bridge.Id, StringComparison.OrdinalIgnoreCase)
                                     && !b.EnableDataCleanup)
                         .OrderByDescending(b => b.IsPassKeyMode)
                         .ThenBy(b => string.IsNullOrWhiteSpace(b.Title) ? b.Id : b.Title, StringComparer.OrdinalIgnoreCase))
            {
                var label = string.IsNullOrWhiteSpace(b.Title) ? b.Id : b.Title;
                var modeTag = b.IsPassKeyMode ? "Pass" : "Get";
                DeleteBridgeNodeOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = b.Id,
                    Title = $"{label}  [{modeTag}]"
                });
            }
        }
        else
        {
            foreach (var b in nodes.OfType<KeyValueBridgeNode>().Where(n => n.IsPassKeyMode))
            {
                DeleteBridgeNodeOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = b.Id,
                    Title = string.IsNullOrWhiteSpace(b.Title) ? b.Id : b.Title
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(preserveSelected) &&
            DeleteBridgeNodeOptions.Any(o => string.Equals(o.NodeId, preserveSelected, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedDeleteBridgeNodeId = preserveSelected;
            return;
        }

        if (_bridge.EnableDataCleanup)
        {
            SelectedDeleteBridgeNodeId = DeleteBridgeNodeOptions.FirstOrDefault()?.NodeId;
            return;
        }

        if (_bridge.IsPassKeyMode)
            SelectedDeleteBridgeNodeId = _bridge.Id;
        else if (!string.IsNullOrWhiteSpace(_bridge.SelectedSourceBridgeNodeId) &&
                 DeleteBridgeNodeOptions.Any(o => string.Equals(o.NodeId, _bridge.SelectedSourceBridgeNodeId, StringComparison.OrdinalIgnoreCase)))
            SelectedDeleteBridgeNodeId = _bridge.SelectedSourceBridgeNodeId;
        else
            SelectedDeleteBridgeNodeId = DeleteBridgeNodeOptions.FirstOrDefault()?.NodeId;
    }

    public void RefreshCleanupTriggerSourceOptions()
    {
        // Trước khi Clear ItemsSource: chụp lại — binding có thể đã gán VM = null khi mở lại dialog.
        var preserveNodeId = CleanupTriggerSourceNodeId ?? _bridge.CleanupTriggerSourceNodeId;

        CleanupTriggerSourceOptions.Clear();
        var nodes = _host.ViewModel?.Nodes;
        if (nodes == null) return;

        foreach (var n in nodes.Where(n => n != null && !string.Equals(n.Id, _bridge.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
            CleanupTriggerSourceOptions.Add(new WorkflowDataSourceOption
            {
                NodeId = n.Id,
                Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
            });
        }

        if (!string.IsNullOrWhiteSpace(preserveNodeId) &&
            CleanupTriggerSourceOptions.Any(o => string.Equals(o.NodeId, preserveNodeId, StringComparison.OrdinalIgnoreCase)))
            CleanupTriggerSourceNodeId = preserveNodeId;
        else
            CleanupTriggerSourceNodeId = CleanupTriggerSourceOptions.FirstOrDefault()?.NodeId;
    }

    public void RefreshCleanupKeySourceOptions()
    {
        var preserveNodeId = CleanupKeySourceNodeId ?? _bridge.CleanupKeySourceNodeId;

        CleanupKeySourceOptions.Clear();
        var nodes = _host.ViewModel?.Nodes;
        if (nodes == null) return;
        foreach (var n in nodes.Where(n => n != null && !string.Equals(n.Id, _bridge.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
            CleanupKeySourceOptions.Add(new WorkflowDataSourceOption
            {
                NodeId = n.Id,
                Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
            });
        }

        if (!string.IsNullOrWhiteSpace(preserveNodeId) &&
            CleanupKeySourceOptions.Any(o => string.Equals(o.NodeId, preserveNodeId, StringComparison.OrdinalIgnoreCase)))
            CleanupKeySourceNodeId = preserveNodeId;
    }

    public void RefreshCleanupKeySourceOutputKeyOptions()
    {
        var preserveKey = CleanupKeySourceOutputKey ?? _bridge.CleanupKeySourceOutputKey;

        CleanupKeySourceOutputKeyOptions.Clear();
        if (string.IsNullOrWhiteSpace(CleanupKeySourceNodeId)) return;
        var src = _host.ViewModel?.Nodes?.FirstOrDefault(n => n != null &&
            string.Equals(n.Id, CleanupKeySourceNodeId, StringComparison.OrdinalIgnoreCase));
        if (src?.DynamicOutputs == null) return;
        foreach (var output in src.DynamicOutputs.Where(o => !string.IsNullOrWhiteSpace(o.Key)))
        {
            CleanupKeySourceOutputKeyOptions.Add(new WorkflowOutputKeyOption
            {
                Key = output.Key,
                DisplayName = string.IsNullOrWhiteSpace(output.DisplayName) ? output.Key : output.DisplayName,
                Type = output.OutputType ?? output.ConvertType
            });
        }

        if (!string.IsNullOrWhiteSpace(preserveKey) &&
            CleanupKeySourceOutputKeyOptions.Any(k => string.Equals(k.Key, preserveKey, StringComparison.OrdinalIgnoreCase)))
            CleanupKeySourceOutputKey = preserveKey;
        else
            CleanupKeySourceOutputKey = CleanupKeySourceOutputKeyOptions.FirstOrDefault()?.Key;

        RefreshEffectiveCleanupPreview();
    }

    public void RefreshCleanupFilterValueSourceOptions()
    {
        var preserveNodeId = CleanupFilterValueSourceNodeId ?? _bridge.CleanupFilterValueSourceNodeId;

        CleanupFilterValueSourceOptions.Clear();
        var nodes = _host.ViewModel?.Nodes;
        if (nodes == null) return;
        foreach (var n in nodes.Where(n => n != null && !string.Equals(n.Id, _bridge.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
            CleanupFilterValueSourceOptions.Add(new WorkflowDataSourceOption
            {
                NodeId = n.Id,
                Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
            });
        }

        if (!string.IsNullOrWhiteSpace(preserveNodeId) &&
            CleanupFilterValueSourceOptions.Any(o => string.Equals(o.NodeId, preserveNodeId, StringComparison.OrdinalIgnoreCase)))
            CleanupFilterValueSourceNodeId = preserveNodeId;
    }

    public void RefreshCleanupFilterFieldSourceOptions()
    {
        var preserveNodeId = CleanupFilterFieldSourceNodeId ?? _bridge.CleanupFilterFieldSourceNodeId;

        CleanupFilterFieldSourceOptions.Clear();
        var nodes = _host.ViewModel?.Nodes;
        if (nodes == null) return;
        foreach (var n in nodes.Where(n => n != null && !string.Equals(n.Id, _bridge.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
            CleanupFilterFieldSourceOptions.Add(new WorkflowDataSourceOption
            {
                NodeId = n.Id,
                Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
            });
        }

        if (!string.IsNullOrWhiteSpace(preserveNodeId) &&
            CleanupFilterFieldSourceOptions.Any(o => string.Equals(o.NodeId, preserveNodeId, StringComparison.OrdinalIgnoreCase)))
            CleanupFilterFieldSourceNodeId = preserveNodeId;
    }

    public void RefreshCleanupFilterFieldSourceOutputKeyOptions()
    {
        var preserveKey = CleanupFilterFieldSourceOutputKey ?? _bridge.CleanupFilterFieldSourceOutputKey;

        CleanupFilterFieldSourceOutputKeyOptions.Clear();
        if (string.IsNullOrWhiteSpace(CleanupFilterFieldSourceNodeId)) return;
        var src = _host.ViewModel?.Nodes?.FirstOrDefault(n => n != null &&
            string.Equals(n.Id, CleanupFilterFieldSourceNodeId, StringComparison.OrdinalIgnoreCase));
        if (src?.DynamicOutputs == null) return;
        foreach (var output in src.DynamicOutputs.Where(o => !string.IsNullOrWhiteSpace(o.Key)))
        {
            CleanupFilterFieldSourceOutputKeyOptions.Add(new WorkflowOutputKeyOption
            {
                Key = output.Key,
                DisplayName = string.IsNullOrWhiteSpace(output.DisplayName) ? output.Key : output.DisplayName,
                Type = output.OutputType ?? output.ConvertType
            });
        }

        if (!string.IsNullOrWhiteSpace(preserveKey) &&
            CleanupFilterFieldSourceOutputKeyOptions.Any(k => string.Equals(k.Key, preserveKey, StringComparison.OrdinalIgnoreCase)))
            CleanupFilterFieldSourceOutputKey = preserveKey;
        else
            CleanupFilterFieldSourceOutputKey = CleanupFilterFieldSourceOutputKeyOptions.FirstOrDefault()?.Key;

        RefreshEffectiveCleanupPreview();
    }

    public void RefreshCleanupFilterValueSourceOutputKeyOptions()
    {
        var preserveKey = CleanupFilterValueSourceOutputKey ?? _bridge.CleanupFilterValueSourceOutputKey;

        CleanupFilterValueSourceOutputKeyOptions.Clear();
        if (string.IsNullOrWhiteSpace(CleanupFilterValueSourceNodeId)) return;
        var src = _host.ViewModel?.Nodes?.FirstOrDefault(n => n != null &&
            string.Equals(n.Id, CleanupFilterValueSourceNodeId, StringComparison.OrdinalIgnoreCase));
        if (src?.DynamicOutputs == null) return;
        foreach (var output in src.DynamicOutputs.Where(o => !string.IsNullOrWhiteSpace(o.Key)))
        {
            CleanupFilterValueSourceOutputKeyOptions.Add(new WorkflowOutputKeyOption
            {
                Key = output.Key,
                DisplayName = string.IsNullOrWhiteSpace(output.DisplayName) ? output.Key : output.DisplayName,
                Type = output.OutputType ?? output.ConvertType
            });
        }

        if (!string.IsNullOrWhiteSpace(preserveKey) &&
            CleanupFilterValueSourceOutputKeyOptions.Any(k => string.Equals(k.Key, preserveKey, StringComparison.OrdinalIgnoreCase)))
            CleanupFilterValueSourceOutputKey = preserveKey;
        else
            CleanupFilterValueSourceOutputKey = CleanupFilterValueSourceOutputKeyOptions.FirstOrDefault()?.Key;

        RefreshEffectiveCleanupPreview();
    }

    public void RefreshCleanupTriggerOutputKeyOptions()
    {
        var preserveOutputKey = CleanupTriggerSourceOutputKey ?? _bridge.CleanupTriggerSourceOutputKey;

        CleanupTriggerOutputKeyOptions.Clear();
        if (string.IsNullOrWhiteSpace(CleanupTriggerSourceNodeId))
            return;

        var src = _host.ViewModel?.Nodes?.FirstOrDefault(n => n != null &&
            string.Equals(n.Id, CleanupTriggerSourceNodeId, StringComparison.OrdinalIgnoreCase));
        if (src?.DynamicOutputs == null) return;

        foreach (var output in src.DynamicOutputs.Where(o => !string.IsNullOrWhiteSpace(o.Key)))
        {
            CleanupTriggerOutputKeyOptions.Add(new WorkflowOutputKeyOption
            {
                Key = output.Key,
                DisplayName = string.IsNullOrWhiteSpace(output.DisplayName) ? output.Key : output.DisplayName,
                Type = output.OutputType ?? output.ConvertType
            });
        }

        if (!string.IsNullOrWhiteSpace(preserveOutputKey) &&
            CleanupTriggerOutputKeyOptions.Any(k => string.Equals(k.Key, preserveOutputKey, StringComparison.OrdinalIgnoreCase)))
            CleanupTriggerSourceOutputKey = preserveOutputKey;
        else
            CleanupTriggerSourceOutputKey = CleanupTriggerOutputKeyOptions.FirstOrDefault()?.Key;
    }

    public void RefreshDeleteKeyOptions()
    {
        var preserveKey = DeleteTargetKey;
        DeleteKeyOptions.Clear();

        if (string.IsNullOrWhiteSpace(SelectedDeleteBridgeNodeId))
            return;

        var kvRunIds = KeyValueBridgeKvScopeHelper.ResolveKvRunIdsForTarget(
            SelectedDeleteBridgeNodeId.Trim(),
            _host.ViewModel?.Nodes);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rid in kvRunIds)
        {
            foreach (var k in WorkflowKeyValueStore.GetAllSnapshots(rid).Keys.Where(x => !string.IsNullOrWhiteSpace(x)))
                keys.Add(k);
        }

        foreach (var key in keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            DeleteKeyOptions.Add(key);

        if (!string.IsNullOrWhiteSpace(preserveKey))
            DeleteTargetKey = preserveKey;

        RefreshDeletePreview();
    }

    public void AddAdditionalAppendSource()
    {
        var item = new KeyValueBridgeAppendSourceItemViewModel(_host);
        item.AvailableSources = BuildAppendSourceOptions();
        item.SelectedSourceNodeId = item.AvailableSources.FirstOrDefault()?.NodeId;
        item.RefreshOutputKeys();
        AdditionalAppendSources.Add(item);
    }

    public void RemoveAdditionalAppendSource(KeyValueBridgeAppendSourceItemViewModel? item)
    {
        if (item == null) return;
        AdditionalAppendSources.Remove(item);
    }

    private ObservableCollection<WorkflowDataSourceOption> BuildAppendSourceOptions()
    {
        var keyIn = Inputs.FirstOrDefault(i => string.Equals(i.Key, "keyIn", StringComparison.OrdinalIgnoreCase));
        if (keyIn?.AvailableSources != null && keyIn.AvailableSources.Count > 0)
            return new ObservableCollection<WorkflowDataSourceOption>(keyIn.AvailableSources);

        var vm = _host.ViewModel;
        if (vm == null) return new ObservableCollection<WorkflowDataSourceOption>();

        // Snapshot to avoid null/collection mutation issues while UI is refreshing nodes.
        var nodesSnapshot = vm.Nodes?.ToList() ?? new List<WorkflowNode>();
        var options = new List<WorkflowDataSourceOption>();
        foreach (var n in nodesSnapshot)
        {
            if (n == null) continue;
            if (string.Equals(n.Id, _bridge?.Id, StringComparison.OrdinalIgnoreCase)) continue;
            if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
            if (string.IsNullOrWhiteSpace(n.Id)) continue;

            options.Add(new WorkflowDataSourceOption
            {
                NodeId = n.Id,
                Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
            });
        }

        return new ObservableCollection<WorkflowDataSourceOption>(options);
    }

    private void LoadAdditionalAppendSources()
    {
        AdditionalAppendSources.Clear();
        if (_bridge.AdditionalAppendSources == null || _bridge.AdditionalAppendSources.Count == 0) return;
        var options = BuildAppendSourceOptions();
        foreach (var src in _bridge.AdditionalAppendSources)
        {
            if (src == null || string.IsNullOrWhiteSpace(src.SourceNodeId)) continue;
            var item = new KeyValueBridgeAppendSourceItemViewModel(_host)
            {
                AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(options),
                SelectedSourceNodeId = src.SourceNodeId,
                SelectedSourceOutputKey = src.SourceOutputKey
            };
            item.RefreshOutputKeys();
            AdditionalAppendSources.Add(item);
        }
    }

    protected override string GetDefaultTitle() => "KeyValue Bridge";

    public void BeginSuppressCleanupBridgeSync() => _suppressCleanupBridgeSync++;

    public void EndSuppressCleanupBridgeSync()
    {
        if (_suppressCleanupBridgeSync > 0)
            _suppressCleanupBridgeSync--;
        if (_suppressCleanupBridgeSync == 0)
            FlushCleanupBindingFieldsFromViewModelToBridge();
    }

    private void FlushCleanupBindingFieldsFromViewModelToBridge()
    {
        _bridge.CleanupTriggerSourceNodeId = CleanupTriggerSourceNodeId;
        _bridge.CleanupTriggerSourceOutputKey = CleanupTriggerSourceOutputKey;
        _bridge.CleanupTriggerExpectedValue = string.IsNullOrWhiteSpace(CleanupTriggerExpectedValue) ? "true" : CleanupTriggerExpectedValue;
        _bridge.CleanupKeySourceNodeId = CleanupKeySourceNodeId;
        _bridge.CleanupKeySourceOutputKey = CleanupKeySourceOutputKey;
        _bridge.CleanupFilterFieldSourceNodeId = CleanupFilterFieldSourceNodeId;
        _bridge.CleanupFilterFieldSourceOutputKey = CleanupFilterFieldSourceOutputKey;
        _bridge.CleanupFilterValueSourceNodeId = CleanupFilterValueSourceNodeId;
        _bridge.CleanupFilterValueSourceOutputKey = CleanupFilterValueSourceOutputKey;
        _bridge.CleanupTargetBridgeNodeId = SelectedDeleteBridgeNodeId;
        _bridge.CleanupTargetKey = DeleteTargetKey ?? string.Empty;
        _bridge.CleanupClearAllNodeData = ClearAllDataForSelectedNode;
        _bridge.CleanupArrayFilterField = ArrayFilterField ?? string.Empty;
        _bridge.CleanupArrayFilterValue = ArrayFilterValue ?? string.Empty;
        _bridge.CleanupRemoveAllMatchedArrayItems = RemoveAllMatchedArrayItems;
    }

    private void ApplyKeyValueBridgeFlowPortCanvasUpdate()
    {
        _host.Dispatcher.BeginInvoke(new System.Action(() =>
        {
            foreach (var port in _bridge.Ports)
            {
                if (port.PortUI != null && _host.WorkflowCanvas != null)
                {
                    if (_host.WorkflowCanvas.Children.Contains(port.PortUI))
                        _host.WorkflowCanvas.Children.Remove(port.PortUI);
                    port.PortUI = null;
                }
            }

            foreach (var port in _bridge.Ports)
                port.IsVisible = !port.IsInput || _bridge.ShouldShowFlowInputPort;

            if (_host.ViewModel != null)
            {
                var toRemove = _host.ViewModel.Connections
                    .Where(c =>
                        (c.FromNode == _bridge && c.FromPort != null && !c.FromPort.IsVisible) ||
                        (c.ToNode == _bridge && c.ToPort != null && !c.ToPort.IsVisible))
                    .ToList();
                foreach (var conn in toRemove)
                {
                    _host.ConnectionRenderer.RemoveConnectionVisuals(conn);
                    _host.ViewModel.Connections.Remove(conn);
                }
            }

            _host.UpdateNodePosition(_bridge, _bridge.X, _bridge.Y);

            if (_host.ViewModel != null)
            {
                foreach (var conn in _host.ViewModel.Connections.Where(c =>
                             c.FromNode == _bridge || c.ToNode == _bridge))
                    _host.RenderConnection(conn);
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    partial void OnIsPassKeyModeChanged(bool value)
    {
        if (_suppressPassSync) return;
        _bridge.IsPassKeyMode = value;
        RefreshPassBridgeOptions();
        RefreshKnownKeys();
        LoadInputs();

        ApplyKeyValueBridgeFlowPortCanvasUpdate();
        if (_bridge.EnableDataCleanup)
            RefreshDeleteBridgeNodeOptions();
        _host.RequestSyncDataPanels(immediate: true);
    }

    partial void OnSelectedSourceBridgeNodeIdChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var src = _host.ViewModel?.Nodes.OfType<KeyValueBridgeNode>()
            .FirstOrDefault(b => string.Equals(b.Id, value, StringComparison.OrdinalIgnoreCase));
        if (src != null && !string.IsNullOrWhiteSpace(src.KvChannelKey) &&
            string.IsNullOrWhiteSpace(KvChannelKey))
            KvChannelKey = src.KvChannelKey;
    }

    partial void OnSelectedDeleteBridgeNodeIdChanged(string? value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupTargetBridgeNodeId = value;
        RefreshDeleteKeyOptions();
    }

    partial void OnEnableDataCleanupChanged(bool value)
    {
        _bridge.EnableDataCleanup = value;
        RefreshDeleteBridgeNodeOptions();
        ApplyKeyValueBridgeFlowPortCanvasUpdate();
        _host.RequestSyncDataPanels(immediate: true);
    }
    partial void OnDeleteTargetKeyChanged(string value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupTargetKey = value ?? string.Empty;
        RefreshDeletePreview();
        RefreshEffectiveCleanupPreview();
    }
    partial void OnClearAllDataForSelectedNodeChanged(bool value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupClearAllNodeData = value;
    }
    partial void OnArrayFilterFieldChanged(string value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupArrayFilterField = value ?? string.Empty;
    }
    partial void OnArrayFilterValueChanged(string value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupArrayFilterValue = value ?? string.Empty;
        RefreshEffectiveCleanupPreview();
    }
    partial void OnRemoveAllMatchedArrayItemsChanged(bool value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupRemoveAllMatchedArrayItems = value;
    }
    partial void OnCleanupTriggerSourceNodeIdChanged(string? value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupTriggerSourceNodeId = value;
        RefreshCleanupTriggerOutputKeyOptions();
    }
    partial void OnCleanupTriggerSourceOutputKeyChanged(string? value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupTriggerSourceOutputKey = value;
    }
    partial void OnCleanupTriggerExpectedValueChanged(string value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupTriggerExpectedValue = string.IsNullOrWhiteSpace(value) ? "true" : value;
    }
    partial void OnCleanupKeySourceNodeIdChanged(string? value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupKeySourceNodeId = value;
        RefreshCleanupKeySourceOutputKeyOptions();
        RefreshEffectiveCleanupPreview();
    }
    partial void OnCleanupKeySourceOutputKeyChanged(string? value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupKeySourceOutputKey = value;
        RefreshEffectiveCleanupPreview();
    }
    partial void OnCleanupFilterFieldSourceNodeIdChanged(string? value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupFilterFieldSourceNodeId = value;
        RefreshCleanupFilterFieldSourceOutputKeyOptions();
        RefreshEffectiveCleanupPreview();
    }
    partial void OnCleanupFilterFieldSourceOutputKeyChanged(string? value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupFilterFieldSourceOutputKey = value;
        RefreshEffectiveCleanupPreview();
    }
    partial void OnCleanupFilterValueSourceNodeIdChanged(string? value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupFilterValueSourceNodeId = value;
        RefreshCleanupFilterValueSourceOutputKeyOptions();
        RefreshEffectiveCleanupPreview();
    }
    partial void OnCleanupFilterValueSourceOutputKeyChanged(string? value)
    {
        if (_suppressCleanupBridgeSync == 0)
            _bridge.CleanupFilterValueSourceOutputKey = value;
        RefreshEffectiveCleanupPreview();
    }

    [RelayCommand]
    private void DeleteKvData()
    {
        if (string.IsNullOrWhiteSpace(SelectedDeleteBridgeNodeId))
        {
            DeleteResultMessage = "(chưa chọn node KeyValueBridge để xóa dữ liệu)";
            return;
        }

        var kvRunIds = KeyValueBridgeKvScopeHelper.ResolveKvRunIdsForTarget(
            SelectedDeleteBridgeNodeId.Trim(),
            _host.ViewModel?.Nodes);
        if (ClearAllDataForSelectedNode)
        {
            var removedKeyCount = 0;
            foreach (var rid in kvRunIds)
                removedKeyCount += WorkflowKeyValueStore.ClearRunKeys(rid);
            DeleteResultMessage = removedKeyCount > 0
                ? (kvRunIds.Count > 1
                    ? $"Đã xóa toàn bộ data runtime ({removedKeyCount} key) trên {kvRunIds.Count} scope (Get + Pass nguồn)."
                    : $"Đã xóa toàn bộ data runtime của node ({removedKeyCount} key).")
                : "Node chưa có data runtime để xóa.";
            RefreshDeleteKeyOptions();
            _host.RequestSyncDataPanels(immediate: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(DeleteTargetKey))
        {
            DeleteResultMessage = "(hãy chọn/nhập key cần xóa hoặc bật xóa toàn bộ node)";
            return;
        }

        var key = DeleteTargetKey.Trim();
        if (!string.IsNullOrWhiteSpace(ArrayFilterField) && !string.IsNullOrWhiteSpace(ArrayFilterValue))
        {
            var total = 0;
            int? lastNeg = null;
            foreach (var rid in kvRunIds)
            {
                var removedItems = WorkflowKeyValueStore.RemoveArrayItemsByJsonField(
                    rid,
                    key,
                    ArrayFilterField.Trim(),
                    ArrayFilterValue.Trim(),
                    RemoveAllMatchedArrayItems);
                if (removedItems > 0)
                    total += removedItems;
                else if (removedItems < 0)
                    lastNeg = removedItems;
            }

            DeleteResultMessage = total switch
            {
                > 0 => kvRunIds.Count > 1
                    ? $"Đã xóa {total} item trong mảng của key '{key}' (mọi scope liên quan)."
                    : $"Đã xóa {total} item trong mảng của key '{key}'.",
                _ => lastNeg == -1
                    ? $"Key '{key}' không phải dữ liệu mảng để xóa theo điều kiện."
                    : $"Không có item nào khớp điều kiện trong key '{key}'."
            };
        }
        else
        {
            var removed = false;
            foreach (var rid in kvRunIds)
                removed |= WorkflowKeyValueStore.RemoveKey(rid, key);
            DeleteResultMessage = removed
                ? (kvRunIds.Count > 1
                    ? $"Đã xóa toàn bộ dữ liệu của key '{key}' trên mọi scope liên quan."
                    : $"Đã xóa toàn bộ dữ liệu của key '{key}'.")
                : $"Không tìm thấy key '{key}'.";
        }

        RefreshDeleteKeyOptions();
        RefreshDeletePreview();
        _host.RequestSyncDataPanels(immediate: true);
    }

    [RelayCommand]
    private void RefreshDeletePreviewNow()
    {
        RefreshDeleteKeyOptions();
        RefreshDeletePreview();
    }

    private void RefreshDeletePreview()
    {
        if (string.IsNullOrWhiteSpace(SelectedDeleteBridgeNodeId))
        {
            DeletePreviewJson = "(chưa chọn node để xem preview)";
            return;
        }

        var kvRunIds = KeyValueBridgeKvScopeHelper.ResolveKvRunIdsForTarget(
            SelectedDeleteBridgeNodeId.Trim(),
            _host.ViewModel?.Nodes);
        var opts = new JsonSerializerOptions { WriteIndented = true };

        if (string.IsNullOrWhiteSpace(DeleteTargetKey))
        {
            if (kvRunIds.Count <= 1)
            {
                var all = WorkflowKeyValueStore.GetAllSnapshots(kvRunIds[0]);
                DeletePreviewJson = all.Count == 0
                    ? "{}"
                    : JsonSerializer.Serialize(all, opts);
                return;
            }

            var scopeMap = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rid in kvRunIds)
            {
                var all = WorkflowKeyValueStore.GetAllSnapshots(rid);
                if (all.Count > 0)
                    scopeMap[rid] = new Dictionary<string, object?>(all, StringComparer.OrdinalIgnoreCase);
            }

            DeletePreviewJson = scopeMap.Count == 0
                ? "{}"
                : JsonSerializer.Serialize(scopeMap, opts);
            return;
        }

        var key = DeleteTargetKey.Trim();
        if (kvRunIds.Count <= 1)
        {
            var snap = WorkflowKeyValueStore.GetSnapshot(kvRunIds[0], key);
            if (snap == null)
            {
                DeletePreviewJson = $"{{ \"{key}\": null }}";
                return;
            }

            var one = new Dictionary<string, object?> { [key] = snap };
            DeletePreviewJson = JsonSerializer.Serialize(one, opts);
            return;
        }

        var perScope = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var rid in kvRunIds)
            perScope[rid] = WorkflowKeyValueStore.GetSnapshot(rid, key);
        DeletePreviewJson = JsonSerializer.Serialize(perScope, opts);
    }

    private void RefreshEffectiveCleanupPreview()
    {
        var dynamicKeyBinding = !string.IsNullOrWhiteSpace(CleanupKeySourceNodeId) &&
                                !string.IsNullOrWhiteSpace(CleanupKeySourceOutputKey);
        EffectiveDeleteKeyPreview = dynamicKeyBinding
            ? $"dynamic: {CleanupKeySourceNodeId}/{CleanupKeySourceOutputKey}"
            : (string.IsNullOrWhiteSpace(DeleteTargetKey) ? "(empty)" : $"static: {DeleteTargetKey.Trim()}");

        var dynamicFilterBinding = !string.IsNullOrWhiteSpace(CleanupFilterValueSourceNodeId) &&
                                   !string.IsNullOrWhiteSpace(CleanupFilterValueSourceOutputKey);
        var dynamicFilterFieldBinding = !string.IsNullOrWhiteSpace(CleanupFilterFieldSourceNodeId) &&
                                        !string.IsNullOrWhiteSpace(CleanupFilterFieldSourceOutputKey);
        var fieldPreview = dynamicFilterFieldBinding
            ? $"field=dynamic: {CleanupFilterFieldSourceNodeId}/{CleanupFilterFieldSourceOutputKey}"
            : (string.IsNullOrWhiteSpace(ArrayFilterField) ? "field=(empty)" : $"field=static: {ArrayFilterField.Trim()}");
        var valuePreview = dynamicFilterBinding
            ? $"value=dynamic: {CleanupFilterValueSourceNodeId}/{CleanupFilterValueSourceOutputKey}"
            : (string.IsNullOrWhiteSpace(ArrayFilterValue) ? "value=(empty)" : $"value=static: {ArrayFilterValue.Trim()}");
        EffectiveFilterValuePreview = $"{fieldPreview} | {valuePreview}";
    }

    protected override void OnSaveTitle()
    {
        _bridge.NotifyTitleChanged();
        _bridge.IsPassKeyMode = IsPassKeyMode;
        _bridge.KvChannelKey = KvChannelKey ?? string.Empty;
        _bridge.SelectedSourceBridgeNodeId = SelectedSourceBridgeNodeId;
        _bridge.PollIntervalValue = PollIntervalValue;
        _bridge.PollIntervalUnit = PollIntervalUnit;
        _bridge.EnableDataCleanup = EnableDataCleanup;
        _bridge.CleanupTargetBridgeNodeId = SelectedDeleteBridgeNodeId;
        _bridge.CleanupTargetKey = DeleteTargetKey ?? string.Empty;
        _bridge.CleanupClearAllNodeData = ClearAllDataForSelectedNode;
        _bridge.CleanupArrayFilterField = ArrayFilterField ?? string.Empty;
        _bridge.CleanupArrayFilterValue = ArrayFilterValue ?? string.Empty;
        _bridge.CleanupRemoveAllMatchedArrayItems = RemoveAllMatchedArrayItems;
        _bridge.CleanupTriggerSourceNodeId = CleanupTriggerSourceNodeId;
        _bridge.CleanupTriggerSourceOutputKey = CleanupTriggerSourceOutputKey;
        _bridge.CleanupTriggerExpectedValue = string.IsNullOrWhiteSpace(CleanupTriggerExpectedValue) ? "true" : CleanupTriggerExpectedValue;
        _bridge.CleanupKeySourceNodeId = CleanupKeySourceNodeId;
        _bridge.CleanupKeySourceOutputKey = CleanupKeySourceOutputKey;
        _bridge.CleanupFilterFieldSourceNodeId = CleanupFilterFieldSourceNodeId;
        _bridge.CleanupFilterFieldSourceOutputKey = CleanupFilterFieldSourceOutputKey;
        _bridge.CleanupFilterValueSourceNodeId = CleanupFilterValueSourceNodeId;
        _bridge.CleanupFilterValueSourceOutputKey = CleanupFilterValueSourceOutputKey;
        _bridge.AdditionalAppendSources = AdditionalAppendSources
            .Where(x => !string.IsNullOrWhiteSpace(x.SelectedSourceNodeId))
            .Select(x => new KeyValueBridgeAppendSource
            {
                SourceNodeId = x.SelectedSourceNodeId!.Trim(),
                SourceOutputKey = string.IsNullOrWhiteSpace(x.SelectedSourceOutputKey) ? null : x.SelectedSourceOutputKey!.Trim()
            })
            .ToList();
        _host.RequestSyncDataPanels(immediate: true);
    }

    protected override void LoadInputs()
    {
        Inputs.Clear();
        if (_node is not KeyValueBridgeNode kn || !kn.IsPassKeyMode) return;
        if (kn.DynamicInputs == null || kn.DynamicInputs.Count == 0) return;
        RefreshAvailableSourcesForInputs();
        foreach (var input in kn.DynamicInputs)
        {
            var inputVm = new InputItemViewModel(kn, input, _host);
            if (input.AvailableSources != null)
                inputVm.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(input.AvailableSources);
            else
                inputVm.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>();

            // Pass mode: for kvChannelKeyIn, provide an explicit "(Không chọn)" option.
            // When selected => executor falls back to Key Identifier textbox (KvChannelKey).
            if (string.Equals(input.Key, "kvChannelKeyIn", System.StringComparison.OrdinalIgnoreCase))
            {
                var current = inputVm.AvailableSources;
                inputVm.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>
                {
                    new WorkflowDataSourceOption
                    {
                        NodeId = string.Empty,
                        Title = NoneSelectionTitle
                    }
                };
                foreach (var src in current)
                    inputVm.AvailableSources.Add(src);
            }
            Inputs.Add(inputVm);
        }
        // KeyIn sources changed -> refresh options for additional append sources rows
        var opts = BuildAppendSourceOptions();
        foreach (var row in AdditionalAppendSources)
        {
            row.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(opts);
            row.RefreshOutputKeys();
        }
    }
}
