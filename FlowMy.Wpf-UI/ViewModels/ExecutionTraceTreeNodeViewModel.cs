using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.ViewModels;

/// <summary>
/// Một dòng key/value đã parse từ InputSummary/OutputSummary. Dùng để hiển thị ở card trace
/// theo dạng chip (nhãn khóa đậm + giá trị), thay vì blob text dài khó đọc.
/// </summary>
/// <summary>
/// Một lựa chọn cho ComboBox "Hiển thị log" trên toolbar Execution Log.
/// <see cref="Id"/> là giá trị nội bộ ("Full"/"Relative"/"Compact") dùng để so sánh;
/// <see cref="Label"/> là text hiển thị cho user (tiếng Việt).
/// </summary>
public sealed class ExecutionTraceDisplayStyleOption
{
    public string Id { get; }
    public string Label { get; }

    public ExecutionTraceDisplayStyleOption(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public override string ToString() => Label;
}

public sealed class TraceKeyValueItem
{
    public string Key { get; }
    public string Value { get; }
    public bool HasKey => !string.IsNullOrWhiteSpace(Key);
    public string CopyText => HasKey ? $"{Key}: {Value}" : (Value ?? string.Empty);

    public TraceKeyValueItem(string? key, string? value)
    {
        Key = (key ?? string.Empty).Trim();
        Value = value ?? string.Empty;
    }
}

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

    /// <summary>
    /// Phiên bản đầy đủ (KHÔNG cắt ngắn) của IN/OUT/ERR để dùng khi user mở rộng card.
    /// <see cref="InputSummary"/>/<see cref="OutputSummary"/> vẫn giữ bản đã compact cho dòng đóng.
    /// </summary>
    [ObservableProperty]
    private string fullInputSummary = string.Empty;

    [ObservableProperty]
    private string fullOutputSummary = string.Empty;

    [ObservableProperty]
    private string fullErrorMessage = string.Empty;

    /// <summary>Danh sách key/value đã parse từ <see cref="InputSummary"/> (compact); rỗng nếu không phải dạng key:value.</summary>
    public ObservableCollection<TraceKeyValueItem> InputItems { get; } = new();

    /// <summary>Danh sách key/value đã parse từ <see cref="OutputSummary"/> (compact); rỗng nếu không phải dạng key:value.</summary>
    public ObservableCollection<TraceKeyValueItem> OutputItems { get; } = new();

    /// <summary>Key/value đầy đủ parse từ <see cref="FullInputSummary"/> — dùng khi mở rộng card.</summary>
    public ObservableCollection<TraceKeyValueItem> FullInputItems { get; } = new();

    /// <summary>Key/value đầy đủ parse từ <see cref="FullOutputSummary"/> — dùng khi mở rộng card.</summary>
    public ObservableCollection<TraceKeyValueItem> FullOutputItems { get; } = new();

    public bool HasInputItems => InputItems.Count > 0;
    public bool HasOutputItems => OutputItems.Count > 0;
    public bool HasFullInputItems => FullInputItems.Count > 0;
    public bool HasFullOutputItems => FullOutputItems.Count > 0;
    public bool HasInput => !string.IsNullOrWhiteSpace(InputSummary);
    public bool HasOutput => !string.IsNullOrWhiteSpace(OutputSummary);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Text để hiện ở chế độ expanded: ưu tiên Full* (không cắt), fallback về compact.</summary>
    public string InputSummaryExpanded => string.IsNullOrEmpty(FullInputSummary) ? InputSummary : FullInputSummary;
    public string OutputSummaryExpanded => string.IsNullOrEmpty(FullOutputSummary) ? OutputSummary : FullOutputSummary;
    public string ErrorMessageExpanded => string.IsNullOrEmpty(FullErrorMessage) ? ErrorMessage : FullErrorMessage;

    public bool HasAnyDetails => HasInput || HasOutput || HasError;

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

    /// <summary>
    /// True khi có ít nhất 1 child đang IsVisible=true (tức là không bị filter ẩn).
    /// Dùng để ẩn cả vùng chứa children (TraceChildrenBranchGrid + dashed line) khi toàn bộ children
    /// bị filter giấu, tránh để lại dải trống phía dưới card cha.
    /// Gọi <see cref="NotifyHasVisibleChildrenChanged"/> sau khi cập nhật IsVisible của children.
    /// </summary>
    public bool HasVisibleChildren
    {
        get
        {
            foreach (var c in Children)
                if (c.IsVisible) return true;
            return false;
        }
    }

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

    partial void OnInputSummaryChanged(string value)
    {
        RebuildItems(InputItems, value);
        OnPropertyChanged(nameof(HasInputItems));
        OnPropertyChanged(nameof(HasInput));
        OnPropertyChanged(nameof(InputSummaryExpanded));
        NotifyDetailsFlagsChanged();
    }

    partial void OnOutputSummaryChanged(string value)
    {
        RebuildItems(OutputItems, value);
        OnPropertyChanged(nameof(HasOutputItems));
        OnPropertyChanged(nameof(HasOutput));
        OnPropertyChanged(nameof(OutputSummaryExpanded));
        NotifyDetailsFlagsChanged();
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorMessageExpanded));
        NotifyDetailsFlagsChanged();
    }

    partial void OnFullInputSummaryChanged(string value)
    {
        RebuildItems(FullInputItems, value);
        OnPropertyChanged(nameof(HasFullInputItems));
        OnPropertyChanged(nameof(InputSummaryExpanded));
    }

    partial void OnFullOutputSummaryChanged(string value)
    {
        RebuildItems(FullOutputItems, value);
        OnPropertyChanged(nameof(HasFullOutputItems));
        OnPropertyChanged(nameof(OutputSummaryExpanded));
    }

    partial void OnFullErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(ErrorMessageExpanded));
    }

    private void NotifyDetailsFlagsChanged()
    {
        OnPropertyChanged(nameof(HasAnyDetails));
        OnPropertyChanged(nameof(ShowDetailsToggle));
    }

    /// <summary>
    /// Parse summary dạng <c>"key1: val1 | key2: val2"</c> thành danh sách chip key/value.
    /// Bỏ qua các ký tự <c>|</c>/<c>:</c> nằm trong chuỗi JSON/cURL bằng cách chỉ split
    /// ở separator <c>" | "</c> cấp ngoài cùng và cắt value tại dấu <c>": "</c> ĐẦU TIÊN.
    /// Nếu không parse được cặp nào thì trả về list rỗng và UI hiện nguyên summary thô.
    /// </summary>
    private static void RebuildItems(ObservableCollection<TraceKeyValueItem> target, string? summary)
    {
        target.Clear();
        if (string.IsNullOrWhiteSpace(summary)) return;

        var parts = SplitTopLevel(summary!, " | ");
        foreach (var raw in parts)
        {
            var part = raw.Trim();
            if (part.Length == 0) continue;
            var idx = FindFirstKeyValueSeparator(part);
            if (idx < 0)
            {
                target.Add(new TraceKeyValueItem(string.Empty, part));
                continue;
            }
            var key = part[..idx].Trim();
            var value = part[(idx + 2)..].Trim();
            target.Add(new TraceKeyValueItem(key, value));
        }
    }

    /// <summary>Split trên separator <paramref name="sep"/>, nhưng không split ở bên trong cặp ngoặc/kép.</summary>
    private static List<string> SplitTopLevel(string input, string sep)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(sep))
        {
            result.Add(input);
            return result;
        }
        int depthCurly = 0, depthSquare = 0, depthParen = 0;
        char? quote = null;
        int start = 0;
        for (int i = 0; i <= input.Length - sep.Length; i++)
        {
            var c = input[i];
            if (quote.HasValue)
            {
                if (c == '\\' && i + 1 < input.Length) { i++; continue; }
                if (c == quote.Value) quote = null;
                continue;
            }
            if (c == '"' || c == '\'') { quote = c; continue; }
            if (c == '{') depthCurly++;
            else if (c == '}') depthCurly = System.Math.Max(0, depthCurly - 1);
            else if (c == '[') depthSquare++;
            else if (c == ']') depthSquare = System.Math.Max(0, depthSquare - 1);
            else if (c == '(') depthParen++;
            else if (c == ')') depthParen = System.Math.Max(0, depthParen - 1);

            if (depthCurly == 0 && depthSquare == 0 && depthParen == 0 &&
                string.CompareOrdinal(input, i, sep, 0, sep.Length) == 0)
            {
                result.Add(input.Substring(start, i - start));
                i += sep.Length - 1;
                start = i + 1;
            }
        }
        if (start <= input.Length)
            result.Add(input.Substring(start));
        return result;
    }

    /// <summary>Tìm dấu <c>": "</c> đầu tiên ở cấp ngoài cùng (ngoài ngoặc/ngoài kép).</summary>
    private static int FindFirstKeyValueSeparator(string input)
    {
        int depthCurly = 0, depthSquare = 0, depthParen = 0;
        char? quote = null;
        for (int i = 0; i < input.Length - 1; i++)
        {
            var c = input[i];
            if (quote.HasValue)
            {
                if (c == '\\' && i + 1 < input.Length) { i++; continue; }
                if (c == quote.Value) quote = null;
                continue;
            }
            if (c == '"' || c == '\'') { quote = c; continue; }
            if (c == '{') depthCurly++;
            else if (c == '}') depthCurly = System.Math.Max(0, depthCurly - 1);
            else if (c == '[') depthSquare++;
            else if (c == ']') depthSquare = System.Math.Max(0, depthSquare - 1);
            else if (c == '(') depthParen++;
            else if (c == ')') depthParen = System.Math.Max(0, depthParen - 1);

            if (depthCurly == 0 && depthSquare == 0 && depthParen == 0 &&
                c == ':' && input[i + 1] == ' ')
                return i;
        }
        return -1;
    }

    public void NotifyHasChildrenChanged()
    {
        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(HasMultipleChildren));
        OnPropertyChanged(nameof(HasVisibleChildren));
    }

    /// <summary>
    /// Raise PropertyChanged cho <see cref="HasVisibleChildren"/>. Gọi trên node CHA sau khi
    /// cập nhật <c>IsVisible</c> của bất kỳ child nào (filter tree) để XAML cập nhật visibility
    /// của dải dashed line + vùng chứa children.
    /// </summary>
    public void NotifyHasVisibleChildrenChanged()
    {
        OnPropertyChanged(nameof(HasVisibleChildren));
    }

    partial void OnIsVisibleChanged(bool value)
    {
        // Mỗi khi bản thân đổi IsVisible, parent cần raise HasVisibleChildren để dashed line +
        // ItemsControl vùng child ẩn/hiện theo. Parent được set bởi WorkflowEditorViewModel khi
        // build tree (xem PickBestParentTreeNode/append logic).
        Parent?.NotifyHasVisibleChildrenChanged();
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
