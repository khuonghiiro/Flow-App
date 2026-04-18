using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.ViewModels;

public sealed partial class ExecutionTraceTreeNodeViewModel : ObservableObject
{
    public string RootExecutionId { get; }
    public string ExecutionId { get; }
    public string NodeId { get; }
    public string NodeTitle { get; }
    public string NodeType { get; }
    public string IconKey { get; }
    public string ParentNodeId { get; }
    public bool IsRunRoot { get; }
    public bool ShowConnector { get; }
    public Brush? NodeBrush { get; }
    public int Depth { get; }
    public Thickness ConnectorIndent { get; }
    public ExecutionTraceTreeNodeViewModel? Parent { get; internal set; }
    public ObservableCollection<ExecutionTraceConnectorGuideViewModel> ConnectorGuides { get; } = new();
    public bool ShowTopStem => ShowConnector;
    public bool ShowBottomStem => ShowConnector && !IsLastSibling;
    public bool ShowHorizontalConnector => ShowConnector;

    [ObservableProperty]
    private string status = "running";

    [ObservableProperty]
    private string elapsedText = string.Empty;

    [ObservableProperty]
    private string inputSummary = string.Empty;

    [ObservableProperty]
    private string outputSummary = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isVisible = true;

    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    private bool isLastSibling = true;

    public ObservableCollection<ExecutionTraceTreeNodeViewModel> Children { get; } = new();
    public Thickness ItemMargin { get; set; } = new Thickness(0, 4, 4, 4);

    public ExecutionTraceTreeNodeViewModel(
        string rootExecutionId,
        string executionId,
        string nodeId,
        string nodeTitle,
        string nodeType,
        string iconKey,
        string parentNodeId,
        bool isRunRoot,
        Brush? nodeBrush = null,
        int depth = 0)
    {
        RootExecutionId = rootExecutionId;
        ExecutionId = executionId;
        NodeId = nodeId;
        NodeTitle = nodeTitle;
        NodeType = nodeType;
        IconKey = iconKey;
        ParentNodeId = parentNodeId;
        IsRunRoot = isRunRoot;
        ShowConnector = !isRunRoot;
        NodeBrush = nodeBrush;
        Depth = depth < 0 ? 0 : depth;
        ConnectorIndent = IsRunRoot ? new Thickness(0) : new Thickness(0, 0, 2, 0);
        ItemMargin = IsRunRoot ? new Thickness(0, 4, 4, 4) : new Thickness(8, 4, 4, 4);
        IsExpanded = true;
        IsVisible = true;
        IsLastSibling = true;
    }

    partial void OnIsLastSiblingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBottomStem));
    }

    public void SetConnectorGuides(IEnumerable<bool> ancestorHasNextSibling)
    {
        ConnectorGuides.Clear();
        foreach (var show in ancestorHasNextSibling)
            ConnectorGuides.Add(new ExecutionTraceConnectorGuideViewModel(show));
    }
}

public sealed class ExecutionTraceConnectorGuideViewModel
{
    public bool ShowVertical { get; }
    public ExecutionTraceConnectorGuideViewModel(bool showVertical) => ShowVertical = showVertical;
}
