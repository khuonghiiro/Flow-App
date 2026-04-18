using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
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

    [ObservableProperty]
    private bool isFirstSibling = true;

    [ObservableProperty]
    private bool isDetailsExpanded;

    public bool HasChildren => Children.Count > 0;

    public ObservableCollection<ExecutionTraceTreeNodeViewModel> Children { get; } = new();
    public Thickness ItemMargin { get; set; } = new Thickness(0, 5, 4, 5);

    /// <summary>X1 in the first guide cell so the dashed horizontal meets the left spine (12px column, x=7).</summary>
    public double GuideDashedLineX1 { get; private set; }

    /// <summary>Viền card (ảnh mẫu: xám nhạt).</summary>
    public Brush TraceCardBorderBrush => CreateTraceBorderBrush();

    /// <summary>Nền card trắng như ảnh mẫu.</summary>
    public Brush TraceCardBackgroundBrush => CreateTraceBackgroundBrush();

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
        const int indentPerDepth = 38;
        var leftIndent = IsRunRoot ? 0 : (Depth * indentPerDepth);
        ItemMargin = new Thickness(leftIndent, 6, 4, 6);
        GuideDashedLineX1 = ComputeGuideDashedLineX1(indentPerDepth);
        IsExpanded = true;
        IsVisible = true;
        IsLastSibling = true;
        IsFirstSibling = true;
    }

    private double ComputeGuideDashedLineX1(int indentPerDepth)
    {
        if (IsRunRoot) return 0;
        const int spineLocalX = 7;
        const int outerSpineColumnWidth = 14;
        var leftIndent = Depth * indentPerDepth;
        return spineLocalX - (outerSpineColumnWidth + leftIndent);
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

    public void NotifyHasChildrenChanged()
    {
        OnPropertyChanged(nameof(HasChildren));
    }


    public void SetConnectorGuides(IEnumerable<bool> ancestorHasNextSibling)
    {
        ConnectorGuides.Clear();
        var list = ancestorHasNextSibling as IList<bool> ?? ancestorHasNextSibling.ToList();
        for (var i = 0; i < list.Count; i++)
            ConnectorGuides.Add(new ExecutionTraceConnectorGuideViewModel(list[i], i == 0));
    }

    private Brush CreateTraceBorderBrush()
    {
        // Ảnh mẫu: viền xám rất nhạt, không tô màu theo level.
        var b = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
        b.Freeze();
        return b;
    }

    private Brush CreateTraceBackgroundBrush()
    {
        var b = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        b.Freeze();
        return b;
    }
}

public sealed class ExecutionTraceConnectorGuideViewModel
{
    public bool ShowVertical { get; }
    public bool IsFirstGuideColumn { get; }

    public ExecutionTraceConnectorGuideViewModel(bool showVertical, bool isFirstGuideColumn)
    {
        ShowVertical = showVertical;
        IsFirstGuideColumn = isFirstGuideColumn;
    }
}
