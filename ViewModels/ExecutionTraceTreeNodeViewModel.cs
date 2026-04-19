using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
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

    /// <summary>
    /// Id của node cha trong tree. Khởi tạo từ <c>incoming?.FromNode?.Id</c>; nếu null/empty
    /// thì <see cref="FlowMy.ViewModels.WorkflowEditorViewModel"/> sẽ gán lại bằng id của
    /// <c>parentTreeNode</c> thực tế (AsyncTaskBody/Run/...) để card không bị hiện "from node:" trống.
    /// </summary>
    [ObservableProperty]
    private string parentNodeId = string.Empty;

    public bool IsRunRoot { get; }
    public bool ShowConnector { get; }
    public Brush? NodeBrush { get; }

    /// <summary>
    /// Brush cho icon khi card dùng <see cref="NodeBrush"/> làm nền. Map theo quy ước
    /// <c>TextOn{ColorKey}Brush</c> để icon luôn tương phản với nền (tránh nền tối + icon đen).
    /// </summary>
    public Brush IconBrush { get; }

    private int _depth;

    public int Depth => _depth;

    public Thickness ConnectorIndent { get; }
    public ExecutionTraceTreeNodeViewModel? Parent { get; internal set; }
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
    public bool HasMultipleChildren => Children.Count > 1;

    public ObservableCollection<ExecutionTraceTreeNodeViewModel> Children { get; } = new();
    public Thickness ItemMargin { get; set; } = new Thickness(0, 5, 4, 5);
    public Thickness ChildrenDashedLineMargin { get; set; } = new Thickness(30, 0, 0, 0);

    /// <summary>Bottom trim (px) for the dashed children trunk so it does not run into the last child row.</summary>
    [ObservableProperty]
    private double childrenDashedLineHeightTrim;

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
        int depth = 0,
        string? nodeColorKey = null)
    {
        RootExecutionId = rootExecutionId;
        ExecutionId = executionId;
        NodeId = nodeId;
        NodeTitle = nodeTitle;
        NodeType = nodeType;
        IconKey = iconKey;
        this.parentNodeId = parentNodeId ?? string.Empty;
        IsRunRoot = isRunRoot;
        ShowConnector = !isRunRoot;
        NodeBrush = nodeBrush;
        IconBrush = ResolveIconBrush(nodeColorKey);
        _depth = depth < 0 ? 0 : depth;
        ConnectorIndent = IsRunRoot ? new Thickness(0) : new Thickness(0, 0, 2, 0);
        ApplyTraceMarginsForDepth();
        IsExpanded = true;
        IsVisible = true;
        IsLastSibling = true;
        IsFirstSibling = true;
    }

    /// <summary>Align visual indent with resolved tree parent (e.g. End as sibling of last step, not nested under it).</summary>
    public void ApplyTraceVisualDepth(int newDepth)
    {
        newDepth = newDepth < 0 ? 0 : newDepth;
        if (_depth == newDepth)
            return;
        _depth = newDepth;
        ApplyTraceMarginsForDepth();
        OnPropertyChanged(nameof(Depth));
        OnPropertyChanged(nameof(ItemMargin));
        OnPropertyChanged(nameof(ChildrenDashedLineMargin));
    }

    private void ApplyTraceMarginsForDepth()
    {
        const int indentPerDepth = 38;
        var leftIndent = IsRunRoot ? 0 : (_depth * indentPerDepth);
        ItemMargin = new Thickness(leftIndent, 6, 4, 6);
        ChildrenDashedLineMargin = new Thickness(leftIndent + 30, 0, 0, 0);
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
        OnPropertyChanged(nameof(HasMultipleChildren));
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

    /// <summary>
    /// Resolve icon brush từ theme theo quy ước <c>TextOn{ColorKey}Brush</c>, fallback
    /// <c>TextOnPrimaryBrush</c> rồi trắng. Giữ icon luôn dễ nhìn kể cả khi nền node tối.
    /// </summary>
    private static Brush ResolveIconBrush(string? nodeColorKey)
    {
        try
        {
            var app = System.Windows.Application.Current;
            if (app != null && !string.IsNullOrWhiteSpace(nodeColorKey))
            {
                var clean = nodeColorKey!.Trim();
                if (clean.EndsWith("Brush"))
                    clean = clean.Substring(0, clean.Length - "Brush".Length);
                if (app.TryFindResource($"TextOn{clean}Brush") is Brush b1) return b1;
                if (app.TryFindResource($"TextOn{clean}") is Brush b2) return b2;
            }
            if (app?.TryFindResource("TextOnPrimaryBrush") is Brush fallback) return fallback;
        }
        catch { }
        var w = new SolidColorBrush(Colors.White);
        w.Freeze();
        return w;
    }
}
