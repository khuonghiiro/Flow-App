using CommunityToolkit.Mvvm.ComponentModel;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Workflow;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;

namespace FlowMy.ViewModels;

public partial class FlowOverwriteSourceItemViewModel : ObservableObject
{
    [ObservableProperty] private string? _selectedSourceNodeId;
    [ObservableProperty] private string? _selectedSourceOutputKey;
    [ObservableProperty] private ObservableCollection<WorkflowOutputKeyOption> _availableOutputKeys = new();

    private readonly IWorkflowEditorHost _host;

    public FlowOverwriteSourceItemViewModel(IWorkflowEditorHost host)
    {
        _host = host;
    }

    partial void OnSelectedSourceNodeIdChanged(string? value) => RefreshOutputKeys();

    public void RefreshOutputKeys()
    {
        var selectedId = SelectedSourceNodeId;
        var srcNode = _host.ViewModel?.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        var opts = srcNode?.DynamicOutputs?
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => new WorkflowOutputKeyOption
            {
                Key = x.Key.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? x.Key : x.DisplayName,
                Type = x.OutputType ?? x.ConvertType
            })
            .ToList() ?? new();

        AvailableOutputKeys = new ObservableCollection<WorkflowOutputKeyOption>(opts);
        if (string.IsNullOrWhiteSpace(SelectedSourceOutputKey) ||
            !opts.Any(o => string.Equals(o.Key, SelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedSourceOutputKey = opts.FirstOrDefault()?.Key;
        }
    }
}

public partial class FlowOverwriteNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly FlowOverwriteNode _nodeTyped;

    [ObservableProperty] private string _outputKey = "outputKey";
    [ObservableProperty] private bool _appendMode;
    [ObservableProperty] private bool _includeIndirectSources;

    public ObservableCollection<FlowOverwriteSourceItemViewModel> Sources { get; } = new();
    [ObservableProperty] private ObservableCollection<WorkflowDataSourceOption> _availableSourceOptions = new();

    public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
    {
        new TitleDisplayModeOption(TitleDisplayMode.Hidden, "Ẩn tiêu đề"),
        new TitleDisplayModeOption(TitleDisplayMode.Hover, "Hiện khi hover"),
        new TitleDisplayModeOption(TitleDisplayMode.Always, "Luôn hiện")
    };

    public FlowOverwriteNodeDialogViewModel(FlowOverwriteNode node, IWorkflowEditorHost host)
        : base(node, host)
    {
        _nodeTyped = node;
        _outputKey = node.OutputKey;
        _appendMode = node.AppendMode;
        _includeIndirectSources = node.IncludeIndirectSources;
        LoadSourcesFromNode();

        if (node is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (_, e) => OnNodePropertyChanged(e.PropertyName ?? string.Empty);
        }
    }

    protected override string GetDefaultTitle() => "Flow Overwrite";

    public void AddSource()
    {
        var item = new FlowOverwriteSourceItemViewModel(_host);
        item.SelectedSourceNodeId = AvailableSourceOptions.FirstOrDefault()?.NodeId;
        item.RefreshOutputKeys();
        Sources.Add(item);
    }

    public void RemoveSource(FlowOverwriteSourceItemViewModel? item)
    {
        if (item == null) return;
        Sources.Remove(item);
    }

    public void RefreshDirectIncomingSourceOptions()
    {
        var options = BuildSourceOptions();
        SyncAvailableSourceOptions(options);

        foreach (var row in Sources)
        {
            // Chỉ auto chọn node mặc định nếu row mới chưa có giá trị.
            // Nếu node đã chọn không còn trực tiếp kết nối, vẫn giữ để user tự quyết định đổi.
            if (string.IsNullOrWhiteSpace(row.SelectedSourceNodeId))
            {
                row.SelectedSourceNodeId = AvailableSourceOptions.FirstOrDefault()?.NodeId;
            }
            row.RefreshOutputKeys();
        }
    }

    protected override void OnSaveTitle()
    {
        _nodeTyped.OutputKey = string.IsNullOrWhiteSpace(OutputKey) ? "outputKey" : OutputKey.Trim();
        _nodeTyped.AppendMode = AppendMode;
        _nodeTyped.IncludeIndirectSources = IncludeIndirectSources;
        _nodeTyped.Mappings = Sources
            .Where(s => !string.IsNullOrWhiteSpace(s.SelectedSourceNodeId))
            .Select(s => new FlowOverwriteMapping
            {
                SourceNodeId = s.SelectedSourceNodeId!.Trim(),
                SourceOutputKey = string.IsNullOrWhiteSpace(s.SelectedSourceOutputKey)
                    ? null
                    : s.SelectedSourceOutputKey.Trim()
            })
            .ToList();
        _nodeTyped.RebuildDynamicOutputs();
        _nodeTyped.NotifyTitleChanged();
        _host.RequestSyncDataPanels(immediate: true);
    }

    protected override void LoadInputs()
    {
        Inputs.Clear();
    }

    protected override void LoadOutputs()
    {
        Outputs.Clear();

        // Base ctor calls virtual LoadOutputs() before this class ctor body runs.
        // So avoid relying on _nodeTyped here and resolve from base Node first.
        var node = Node as FlowOverwriteNode ?? _nodeTyped;
        if (node?.DynamicOutputs == null || node.DynamicOutputs.Count == 0) return;

        foreach (var output in node.DynamicOutputs)
        {
            Outputs.Add(new OutputItemViewModel(node, output));
        }
    }

    partial void OnIncludeIndirectSourcesChanged(bool value)
    {
        RefreshDirectIncomingSourceOptions();
    }

    private ObservableCollection<WorkflowDataSourceOption> BuildSourceOptions()
    {
        return IncludeIndirectSources
            ? BuildAllUpstreamSources()
            : BuildDirectIncomingSources();
    }

    private ObservableCollection<WorkflowDataSourceOption> BuildDirectIncomingSources()
    {
        var vm = _host.ViewModel;
        if (vm == null) return new ObservableCollection<WorkflowDataSourceOption>();
        var directIncoming = vm.Connections
            .Where(c => c.ToNode != null && c.FromNode != null && c.ToNode.Id == _nodeTyped.Id)
            .Select(c => c.FromNode!)
            .GroupBy(n => n.Id, System.StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(BaseNodeDialogViewModel.CreateDataSourceOption)
            .ToList();

        // Giữ các node đã được chọn trước đó trong options để không làm combobox "rơi" selection.
        var selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Sources)
        {
            if (!string.IsNullOrWhiteSpace(row.SelectedSourceNodeId))
                selectedIds.Add(row.SelectedSourceNodeId.Trim());
        }
        foreach (var m in _nodeTyped.Mappings)
        {
            if (!string.IsNullOrWhiteSpace(m.SourceNodeId))
                selectedIds.Add(m.SourceNodeId.Trim());
        }

        var byId = directIncoming.ToDictionary(x => x.NodeId, StringComparer.OrdinalIgnoreCase);
        foreach (var selectedId in selectedIds)
        {
            if (byId.ContainsKey(selectedId)) continue;

            var node = vm.Nodes.FirstOrDefault(n => string.Equals(n.Id, selectedId, StringComparison.OrdinalIgnoreCase));
            if (node != null)
            {
                var opt = BaseNodeDialogViewModel.CreateDataSourceOption(node);
                opt.Title = string.IsNullOrWhiteSpace(node.Title) ? node.Id : $"{node.Title} (không còn nối trực tiếp)";
                byId[selectedId] = opt;
            }
            else
            {
                byId[selectedId] = new WorkflowDataSourceOption
                {
                    NodeId = selectedId,
                    Title = $"{selectedId} (không tồn tại)"
                };
            }
        }

        var opts = byId.Values
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ObservableCollection<WorkflowDataSourceOption>(opts);
    }

    private ObservableCollection<WorkflowDataSourceOption> BuildAllUpstreamSources()
    {
        var vm = _host.ViewModel;
        if (vm == null) return new ObservableCollection<WorkflowDataSourceOption>();

        var connections = vm.Connections;
        if (connections == null || connections.Count == 0)
            return BuildDirectIncomingSources();

        var upstream = new HashSet<WorkflowNode>();
        var stack = new Stack<WorkflowNode>();
        stack.Push(_nodeTyped);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var incoming = connections.Where(c => c.ToNode == current && c.FromNode != null).ToList();
            foreach (var conn in incoming)
            {
                var src = conn.FromNode;
                if (src == null || ReferenceEquals(src, _nodeTyped)) continue;
                if (upstream.Add(src))
                    stack.Push(src);
            }
        }

        var producerNodes = upstream
            .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
            .ToList();

        var byId = producerNodes
            .GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(
                n => n.Id,
                BaseNodeDialogViewModel.CreateDataSourceOption,
                StringComparer.OrdinalIgnoreCase);

        foreach (var row in Sources)
        {
            if (string.IsNullOrWhiteSpace(row.SelectedSourceNodeId)) continue;
            EnsureSelectedSourceOption(vm, byId, row.SelectedSourceNodeId.Trim());
        }

        foreach (var m in _nodeTyped.Mappings)
        {
            if (string.IsNullOrWhiteSpace(m.SourceNodeId)) continue;
            EnsureSelectedSourceOption(vm, byId, m.SourceNodeId.Trim());
        }

        var opts = byId.Values
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new ObservableCollection<WorkflowDataSourceOption>(opts);
    }

    private static void EnsureSelectedSourceOption(
        WorkflowEditorViewModel vm,
        Dictionary<string, WorkflowDataSourceOption> byId,
        string selectedId)
    {
        if (byId.ContainsKey(selectedId)) return;

        var node = vm.Nodes.FirstOrDefault((WorkflowNode n) =>
            string.Equals(n.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        if (node != null)
        {
            var opt = BaseNodeDialogViewModel.CreateDataSourceOption(node);
            opt.Title = string.IsNullOrWhiteSpace(node.Title) ? node.Id : $"{node.Title} (không còn trong upstream)";
            byId[selectedId] = opt;
            return;
        }

        byId[selectedId] = new WorkflowDataSourceOption
        {
            NodeId = selectedId,
            Title = $"{selectedId} (không tồn tại)"
        };
    }

    private void LoadSourcesFromNode()
    {
        Sources.Clear();
        var options = BuildSourceOptions();
        SyncAvailableSourceOptions(options);

        foreach (var src in _nodeTyped.Mappings)
        {
            if (src == null || string.IsNullOrWhiteSpace(src.SourceNodeId)) continue;
            var item = new FlowOverwriteSourceItemViewModel(_host)
            {
                SelectedSourceNodeId = src.SourceNodeId,
                SelectedSourceOutputKey = src.SourceOutputKey
            };
            item.RefreshOutputKeys();
            Sources.Add(item);
        }

        if (Sources.Count == 0)
            AddSource();
    }

    private void SyncAvailableSourceOptions(ObservableCollection<WorkflowDataSourceOption> options)
    {
        // IMPORTANT: Replace collection in one shot (don't Clear/Add while ComboBox is bound),
        // to avoid WPF resetting SelectedValue when ItemsSource is temporarily empty.
        AvailableSourceOptions = new ObservableCollection<WorkflowDataSourceOption>(options);
    }
}
