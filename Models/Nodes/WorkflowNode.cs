using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Models
{
    public sealed class WorkflowDataSourceOption
    {
        public string NodeId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public NodeType NodeType { get; set; } = NodeType.Generic;
        public string NodeTypeDisplayName { get; set; } = "Generic";
        public string IconKey { get; set; } = "cog";
        public Brush? NodeBrush { get; set; }
        public Brush? NodeTextBrush { get; set; }
        public Brush? NodeHoverBrush { get; set; }
        public Brush? NodeSelectedBrush { get; set; }

        public override string ToString() => string.IsNullOrWhiteSpace(Title) ? NodeId : Title;
    }

    /// <summary>
    /// Wrapper class để hiển thị output key kèm type trong combobox.
    /// Dùng cho OutputKeySelectorUI để user biết được type của mỗi output key.
    /// </summary>
    public sealed class WorkflowOutputKeyOption
    {
        public string Key { get; set; } = string.Empty;
        public WorkflowDataType? Type { get; set; }
        public string? DisplayName { get; set; } = string.Empty;

        public override string ToString()
        {
            if (Type.HasValue)
            {
                return $"{Key} ({Type.Value})";
            }
            return Key;
        }
    }

    public sealed class WorkflowDynamicDataPort
    {
        /// <summary>Key nội bộ (ổn định) để map với logic/persistence.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Tên hiển thị trong UI.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Type của output này (dùng để hiển thị trong combobox và validate type).
        /// Chỉ áp dụng cho DynamicOutputs. Null nếu không xác định.
        /// </summary>
        public WorkflowDataType? OutputType { get; set; }

        /// <summary>Nếu true nghĩa là output có thể có nhiều giá trị/nguồn (cần chọn).</summary>
        public bool IsMultiple { get; set; }

        /// <summary>Danh sách nguồn có thể chọn khi node này đang nhận data.</summary>
        public List<WorkflowDataSourceOption> AvailableSources { get; set; } = new();

        /// <summary>NodeId được chọn để lấy data.</summary>
        public string? SelectedSourceNodeId { get; set; }

        /// <summary>
        /// Output key được chọn từ node nguồn (khi node nguồn có nhiều outputs).
        /// Nếu null/empty thì fallback sang <see cref="Key"/>.
        /// </summary>
        public string? SelectedSourceOutputKey { get; set; }

        /// <summary>ComboBox UI tương ứng (được build khi render node).</summary>
        public ComboBox? SourceSelectorUI { get; set; }

        /// <summary>Danh sách output keys khả dụng từ node nguồn đang chọn (legacy, dùng string).</summary>
        public List<string> AvailableOutputKeys { get; set; } = new();

        /// <summary>Danh sách output keys kèm type metadata để hiển thị trong combobox.</summary>
        public List<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; set; } = new();

        /// <summary>ComboBox chọn output key (được build khi render node).</summary>
        public ComboBox? OutputKeySelectorUI { get; set; }

        /// <summary>
        /// Text hiển thị nguồn đã resolve (dùng khi chỉ có 0/1 source nên ComboBox bị ẩn).
        /// </summary>
        public TextBlock? ResolvedSourceTextUI { get; set; }

        /// <summary>
        /// Text hiển thị output key đã resolve (khi chỉ có 0/1 key nên ComboBox bị ẩn).
        /// </summary>
        public TextBlock? ResolvedOutputKeyTextUI { get; set; }

        /// <summary>
        /// Text hiển thị preview value của data đang nhận (data-in).
        /// </summary>
        public TextBlock? ResolvedValueTextUI { get; set; }

        /// <summary>
        /// Text hiển thị value của output (data-out) trên DataPanel.
        /// </summary>
        public TextBlock? OutputValueTextUI { get; set; }

        // ===== Array output preview (runtime-only UI refs/state) =====

        /// <summary>
        /// Toggle ẩn/hiện preview items khi OutputType là Array*.
        /// Runtime-only.
        /// </summary>
        public ToggleButton? ArrayPreviewToggleUI { get; set; }

        /// <summary>
        /// Container chứa danh sách items dạng "[i]:value" khi OutputType là Array*.
        /// Runtime-only.
        /// </summary>
        public StackPanel? ArrayPreviewItemsContainerUI { get; set; }

        /// <summary>
        /// Lưu trạng thái expand/collapse của array preview (runtime-only).
        /// </summary>
        public bool IsArrayPreviewExpanded { get; set; } = false;

        // ===== Extended editable/transform features (Data In) =====

        /// <summary>
        /// Key override do user nhập (dùng để thay thế key hiển thị/resolve trong UI).
        /// Không đổi Key "ổn định" để tránh break persistence/logic cũ.
        /// </summary>
        public string? UserKeyOverride { get; set; }

        /// <summary>
        /// Value override do user nhập (nếu có thì ưu tiên value này).
        /// </summary>
        public string? UserValueOverride { get; set; }

        /// <summary>
        /// Kiểu dữ liệu muốn convert cho value preview.
        /// </summary>
        public WorkflowDataType ConvertType { get; set; } = WorkflowDataType.String;

        /// <summary>TextBox UI cho key override (optional).</summary>
        public TextBox? UserKeyTextBoxUI { get; set; }

        /// <summary>TextBox UI cho value override (optional).</summary>
        public TextBox? UserValueTextBoxUI { get; set; }

        /// <summary>ComboBox UI chọn convert type.</summary>
        public ComboBox? ConvertTypeSelectorUI { get; set; }

        /// <summary>Text hiển thị lỗi convert (nếu invalid).</summary>
        public TextBlock? ConvertErrorTextUI { get; set; }

        /// <summary>Text hiển thị value sau convert.</summary>
        public TextBlock? ConvertedValueTextUI { get; set; }

        /// <summary>
        /// Đánh dấu output này có phải do user thêm bằng button + không.
        /// Nếu true thì hiển thị UI để nhập key/value/type, nếu false thì hiển thị dạng text như cũ.
        /// </summary>
        public bool IsUserAdded { get; set; } = false;

        /// <summary>
        /// Flag để đánh dấu đang sync value từ output khác (tránh trigger TextChanged không cần thiết).
        /// </summary>
        public bool IsSyncingValue { get; set; } = false;

        /// <summary>
        /// Cache runtime: giá trị output đã resolve lần cuối
        /// (dùng để tránh update UI/resolve lại khi giá trị không thay đổi).
        /// Chỉ dùng trong NodeChrome/WorkflowEditorEventService, không serialize.
        /// </summary>
        public string? LastResolvedOutputValue { get; set; }

        /// <summary>
        /// Cache runtime: giá trị preview (Value:) đã resolve lần cuối
        /// cho các input/output dùng DataPanel preview.
        /// Dùng để giảm số lần set Text/trigger layout.
        /// </summary>
        public string? LastResolvedPreviewValue { get; set; }
    }

    /// <summary>
    /// Enum chung cho tất cả các loại dữ liệu được hỗ trợ trong workflow.
    /// Dùng cho OutputType trong WorkflowDynamicDataPort để các node khác biết được type của output.
    /// Tất cả các combobox type trong các node đều phải dùng enum này để đảm bảo consistency.
    /// </summary>
    public enum WorkflowDataType
    {
        String = 0,
        Integer = 1,
        Number = 2,
        DateTime = 3,
        Time = 4,
        Boolean = 5,
        Object,
        ArrayString,
        ArrayNumber,
        ArrayDynamic

    }

    /// <summary>
    /// Cấu hình route lại flow cho phép "tái sử dụng" logic của node hiện tại
    /// dựa trên node đi vào (incoming) và node đi ra (outgoing) trực tiếp,
    /// kèm theo style đường nối out (Bezier/Orthogonal/Straight hoặc theo workflow).
    /// </summary>
    public sealed class NodeReuseRoute
    {
        /// <summary>Id của node nối trực tiếp vào input port của node hiện tại.</summary>
        public string? IncomingNodeId { get; set; }

        /// <summary>Id của node nối trực tiếp ra từ output port của node hiện tại.</summary>
        public string? OutgoingNodeId { get; set; }

        /// <summary>
        /// Kiểu line cho connection từ node hiện tại sang OutgoingNodeId.
        /// Giá trị là tên enum ConnectionLineStyle
        /// ("Bezier", "Orthogonal", "Straight", "SmoothOrthogonal", "Arc", "RadialFanout")
        /// hoặc null/empty để dùng style workflow hiện tại.
        /// </summary>
        public string? LineStyleKey { get; set; }
    }

    /// <summary>
    /// Cách Start node tham gia vào ngữ cảnh định danh flow.
    /// </summary>
    public enum FlowRunMode
    {
        MainFlow = 0,
        SubFlowAttached = 1,
        SubFlowIndependent = 2,
        AutoScheduled = 3
    }

    public enum AutoRunIntervalUnit
    {
        Milliseconds = 0,
        Seconds = 1,
        Minutes = 2
    }

    /// <summary>
    /// Hành vi khi flow chạm End node.
    /// </summary>
    public enum EndNodeBehavior
    {
        StopCurrentFlow = 0,
        ReturnToParent = 1,
        EmitResultOnly = 2
    }

    public enum DiamondSharpness
    {
        Soft = 0,
        Medium = 1,
        Sharp = 2
    }

    /// <summary>
    /// Chế độ hiển thị giao diện Conditional Node.
    /// Classic = giao diện cũ (hình chữ nhật với branches liệt kê).
    /// Diamond = giao diện mới (hình thoi + satellite circles).
    /// </summary>
    public enum ConditionalVisualMode
    {
        Classic = 0,
        Diamond = 1
    }

    /// <summary>
    /// Model đại diện cho một Node trong workflow editor
    /// </summary>
    public class WorkflowNode
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }

        public Brush NodeBrush { get; set; } = Brushes.Transparent;
        public string? ColorKey { get; set; } // Key của màu từ theme (ví dụ: "SkyAzure", "Ocean", ...)

        // UI của node (hiện đang dùng trực tiếp trong renderer)
        public Border? Border { get; set; }

        public NodeType Type { get; set; } = NodeType.Generic;

        /// <summary>Start hiển thị hình thoi (SubFlow độc lập hoặc Auto scheduled) — port/title/badge phải khớp.</summary>
        public bool IsStartDiamondVisual =>
            Type == NodeType.Start &&
            (RunMode == FlowRunMode.SubFlowIndependent || RunMode == FlowRunMode.AutoScheduled);

        /// <summary>
        /// Cặp Start/End khởi tạo sẵn trên canvas (sample); không xóa bằng Delete. Start/End kéo từ palette có giá trị false.
        /// </summary>
        public bool IsDefaultSampleStartEnd { get; set; }

        // Danh sách ports (hỗ trợ nhiều ports)
        public List<NodePort> Ports { get; set; } = new();

        /// <summary>
        /// Mảng input dynamic (data). Nếu có item => node có thể nhận data.
        /// </summary>
        public List<WorkflowDynamicDataPort> DynamicInputs { get; set; } = new();

        /// <summary>
        /// Mảng output dynamic (data). Nếu có item => node có thể xuất data.
        /// </summary>
        public List<WorkflowDynamicDataPort> DynamicOutputs { get; set; } = new();

        /// <summary>
        /// UI refs (được renderer gắn vào) để update realtime khi edit title.
        /// </summary>
        public TextBlock? TitleTextBlockUI { get; set; }

        /// <summary>
        /// UI refs (runtime-only): hiển thị trạng thái/thời gian xử lý khi chạy workflow.
        /// </summary>
        public FrameworkElement? ExecutionStatusContainerUI { get; set; }
        public TextBlock? ExecutionStatusTextUI { get; set; }
        /// <summary>
        /// Runtime-only: spinner quay thay cho connection/energy animation khi Cache node mode được bật.
        /// </summary>
        public System.Windows.Shapes.Shape? ExecutionBusySpinnerUI { get; set; }
        /// <summary>
        /// UI refs (runtime-only): hiển thị kết quả output sau khi node thực thi.
        /// </summary>
        public ToggleButton? ExecutionResultsToggleUI { get; set; }
        public StackPanel? ExecutionResultsItemsPanel { get; set; }
        /// <summary>
        /// UI refs (runtime-only): hiển thị lỗi khi node thực thi bị exception.
        /// </summary>
        public ToggleButton? ExecutionErrorToggleUI { get; set; }
        public StackPanel? ExecutionErrorItemsPanel { get; set; }

        public bool SupportsDynamicData => (DynamicInputs?.Count > 0) || (DynamicOutputs?.Count > 0);

        // Conditional branches cho conditional nodes (IfElse, Switch, etc.)
        public List<ConditionalBranch> ConditionalBranches { get; set; } = new();

        // Async task branches cho async task nodes
        public List<AsyncTaskBranch> AsyncTaskBranches { get; set; } = new();

        // Property để kiểm tra nếu node là conditional node
        public virtual bool IsConditionalNode => Type == NodeType.IfElse;

        /// <summary>
        /// Cấu hình "tái sử dụng" logic:
        /// - IncomingNodeId: node nối trực tiếp vào input của node hiện tại.
        /// - OutgoingNodeId: node nối trực tiếp ra từ output của node hiện tại.
        ///
        /// Khi được cấu hình, executor có thể dùng bảng này để route flow:
        /// nếu flow đi vào từ A và tại node hiện tại chọn out là H thì sau khi chạy xong
        /// node hiện tại sẽ nhảy sang H (thay vì đi tất cả nhánh out mặc định).
        ///
        /// Nếu danh sách rỗng hoặc không khớp với incoming node hiện tại thì flow chạy như cũ.
        /// </summary>
        public List<NodeReuseRoute> ReuseRoutes { get; set; } = new();

        /// <summary>
        /// Key cấu hình flow của node Start/End. Nếu rỗng, runtime tự sinh scope.
        /// </summary>
        public string? FlowScopeKey { get; set; }

        /// <summary>
        /// Chế độ chạy flow cho Start node.
        /// </summary>
        public FlowRunMode RunMode { get; set; } = FlowRunMode.MainFlow;
        public double AutoRunIntervalValue { get; set; } = 5d;
        public AutoRunIntervalUnit AutoRunIntervalUnit { get; set; } = AutoRunIntervalUnit.Seconds;

        /// <summary>
        /// Padding (mỗi phía) từ bbox cụm node tới khung nét đứt xanh khi chưa dùng khung tay chỉnh.
        /// Mặc định lớn hơn trước để Start/End không sát viền.
        /// </summary>
        public double AutoScopeVisualPadding { get; set; } = 40d;

        /// <summary>Khung scope auto chỉnh tay (canvas). Width hoặc Height &lt; 120 = chưa bật, dùng auto + padding.</summary>
        public double AutoScopeFrameX { get; set; }
        public double AutoScopeFrameY { get; set; }
        public double AutoScopeFrameWidth { get; set; }
        public double AutoScopeFrameHeight { get; set; }

        /// <summary>
        /// Hành vi kết thúc flow cho End node.
        /// </summary>
        public EndNodeBehavior EndBehavior { get; set; } = EndNodeBehavior.StopCurrentFlow;
        public DiamondSharpness DiamondSharpness { get; set; } = DiamondSharpness.Medium;

        /// <summary>
        /// Chế độ hiển thị cho Conditional Node: Classic (hình chữ nhật) hoặc Diamond (hình thoi + satellite).
        /// Mặc định Classic để không break node cũ.
        /// </summary>
        public ConditionalVisualMode ConditionalVisualMode { get; set; } = ConditionalVisualMode.Classic;

        // Runtime observability (không serialize trực tiếp ngoài Properties).
        public string? LastExecutionId { get; set; }
        public string? LastFlowScopeId { get; set; }
        public string? LastBranchId { get; set; }
        public string? LastParentFlowScopeId { get; set; }

        // ── Floating Widget ──
        /// <summary>
        /// Cấu hình để xuất node ra ngoài màn hình dưới dạng floating widget.
        /// Null / IsEnabled=false = không dùng. Được persist vào workflow JSON.
        /// Đặt ở WorkflowNode base để bất kỳ node nào cũng có thể mở rộng thành widget.
        /// </summary>
        public FloatingWidgetConfig? FloatingWidget { get; set; }

        // Backward compatibility - giữ lại để không break code cũ
        public string? Condition { get; set; }

        // Keyboard node
        public string? Key { get; set; }

        // MouseEvent node
        public MouseEventType? MouseEvent { get; set; }
        public string? TargetElement { get; set; }

        // Backward compatibility - lấy port đầu tiên
        public Point InputPortPosition
        {
            get
            {
                var inputPort = Ports.FirstOrDefault(p => p.IsInput && p.IsVisible);
                return inputPort?.PositionPoint ?? new Point(X, Y + 40);
            }
        }

        public Point OutputPortPosition
        {
            get
            {
                var outputPort = Ports.FirstOrDefault(p => !p.IsInput && p.IsVisible);
                return outputPort?.PositionPoint ?? new Point(X + 150, Y + 40);
            }
        }

        public FrameworkElement? InputPort
        {
            get
            {
                var inputPort = Ports.FirstOrDefault(p => p.IsInput && p.IsVisible);
                return inputPort?.PortUI;
            }
        }

        public FrameworkElement? OutputPort
        {
            get
            {
                var outputPort = Ports.FirstOrDefault(p => !p.IsInput && p.IsVisible);
                return outputPort?.PortUI;
            }
        }
    }
}

