using CommunityToolkit.Mvvm.ComponentModel;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Workflow;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels;

public partial class FlowOverwriteSourceItemViewModel : ObservableObject
{
    [ObservableProperty] private string? _selectedSourceNodeId;
    [ObservableProperty] private string? _selectedSourceOutputKey;
    [ObservableProperty] private ObservableCollection<WorkflowDataSourceOption> _availableSources = new();
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
        var srcNode = _host.ViewModel?.Nodes.FirstOrDefault(n => n.Id == selectedId);
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
            !opts.Any(o => string.Equals(o.Key, SelectedSourceOutputKey, System.StringComparison.OrdinalIgnoreCase)))
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

    public ObservableCollection<FlowOverwriteSourceItemViewModel> Sources { get; } = new();

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
        LoadSourcesFromNode();

        if (node is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (_, e) => OnNodePropertyChanged(e.PropertyName ?? string.Empty);
        }
    }

    protected override string GetDefaultTitle() => "Flow Overwrite";

    public void AddSource()
    {
        var item = new FlowOverwriteSourceItemViewModel(_host)
        {
            AvailableSources = BuildDirectIncomingSources()
        };
        item.SelectedSourceNodeId = item.AvailableSources.FirstOrDefault()?.NodeId;
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
        var options = BuildDirectIncomingSources();
        foreach (var row in Sources)
        {
            row.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(options);
            if (string.IsNullOrWhiteSpace(row.SelectedSourceNodeId) ||
                !row.AvailableSources.Any(s => s.NodeId == row.SelectedSourceNodeId))
            {
                row.SelectedSourceNodeId = row.AvailableSources.FirstOrDefault()?.NodeId;
            }
            row.RefreshOutputKeys();
        }
    }

    protected override void OnSaveTitle()
    {
        _nodeTyped.OutputKey = string.IsNullOrWhiteSpace(OutputKey) ? "outputKey" : OutputKey.Trim();
        _nodeTyped.AppendMode = AppendMode;
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

    private ObservableCollection<WorkflowDataSourceOption> BuildDirectIncomingSources()
    {
        var vm = _host.ViewModel;
        if (vm == null) return new ObservableCollection<WorkflowDataSourceOption>();
        var opts = vm.Connections
            .Where(c => c.ToNode != null && c.FromNode != null && c.ToNode.Id == _nodeTyped.Id)
            .Select(c => c.FromNode!)
            .GroupBy(n => n.Id, System.StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
            .Select(n => new WorkflowDataSourceOption
            {
                NodeId = n.Id,
                Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
            })
            .ToList();
        return new ObservableCollection<WorkflowDataSourceOption>(opts);
    }

    private void LoadSourcesFromNode()
    {
        Sources.Clear();
        var options = BuildDirectIncomingSources();
        foreach (var src in _nodeTyped.Mappings)
        {
            if (src == null || string.IsNullOrWhiteSpace(src.SourceNodeId)) continue;
            var item = new FlowOverwriteSourceItemViewModel(_host)
            {
                AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(options),
                SelectedSourceNodeId = src.SourceNodeId,
                SelectedSourceOutputKey = src.SourceOutputKey
            };
            item.RefreshOutputKeys();
            Sources.Add(item);
        }

        if (Sources.Count == 0)
            AddSource();
    }
}
