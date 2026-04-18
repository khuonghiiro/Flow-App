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

    public bool HasAnyDetails =>
        !string.IsNullOrWhiteSpace(InputSummary) ||
        !string.IsNullOrWhiteSpace(OutputSummary) ||
        !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowDetailsToggle => HasAnyDetails;

    [ObservableProperty]
    private bool isVisible = true;

    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    private bool isLastSibling = true;

    public ObservableCollection<ExecutionTraceTreeNodeViewModel> Children { get; } = new();
    public Thickness ItemMargin { get; set; } = new Thickness(0, 5, 4, 5);

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
        var leftIndent = IsRunRoot ? 0 : (Depth * 50);
        ItemMargin = new Thickness(leftIndent, 5, 4, 5);
        IsExpanded = true;
        IsVisible = true;
        IsLastSibling = true;
    }

    partial void OnIsLastSiblingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBottomStem));
    }

    partial void OnInputSummaryChanged(string value) => NotifyDetailsFlagsChanged();
    partial void OnOutputSummaryChanged(string value) => NotifyDetailsFlagsChanged();
    partial void OnErrorMessageChanged(string value) => NotifyDetailsFlagsChanged();

    private void NotifyDetailsFlagsChanged()
    {
        OnPropertyChanged(nameof(HasAnyDetails));
        OnPropertyChanged(nameof(ShowDetailsToggle));
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
