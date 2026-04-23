using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// ViewModel cho Workflow Editor
    /// </summary>
    public partial class WorkflowEditorViewModel : BaseViewModel
    {
        private readonly FlowMy.Workflow.TemplateFactory _templateFactory;
        private readonly WorkflowExecutionService _workflowExecutionService;
        private readonly IWorkflowPersistenceService _persistenceService;
        private readonly IWorkflowExecutionVisualizer _executionVisualizer;

        /// <summary>Số phiên chạy thủ công đang chạy (mỗi lần Bắt đầu / chạy từ node = một phiên độc lập).</summary>
        private int _manualExecutionRunsInFlight;
        public int ManualExecutionRunsInFlight => Volatile.Read(ref _manualExecutionRunsInFlight);

        private readonly object _manualRunCtsLock = new();
        private readonly Dictionary<string, CancellationTokenSource> _manualRunCtsBySession =
            new(StringComparer.Ordinal);

        private readonly object _runningNodesBookkeepingLock = new();
        private readonly Dictionary<WorkflowNode, int> _nodeRunningRefCount = new();
        private CancellationTokenSource? _workflowLoadCts;

        /// <summary>Số lượt chạy song song trên lane auto-scheduled (không dùng IsExecuting).</summary>
        private int _autoScheduledLaneRunsInFlight;

        /// <summary>Còn lane thủ công hoặc auto-scheduled — UI highlight line không được coi null là "hết chạy".</summary>
        public bool IsAnyExecutionLaneActive => IsExecuting || Volatile.Read(ref _autoScheduledLaneRunsInFlight) != 0;
        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<WorkflowNode> nodes = new();

        [ObservableProperty]
        private ObservableCollection<WorkflowConnection> connections = new();

        // Danh sách các node đang chạy (để hiển thị trong panel thu nhỏ)
        [ObservableProperty]
        private ObservableCollection<WorkflowNode> runningNodes = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteNodeCommand))]
        private WorkflowNode? selectedNode;

        [ObservableProperty]
        private string currentWorkflowName = "";

        [ObservableProperty]
        private ObservableCollection<string> savedWorkflows = new();

        private bool _isRefreshingAfterSave;

        partial void OnCurrentWorkflowNameChanged(string value)
        {
            if (IsLoading || string.IsNullOrEmpty(value)) return;
            if (_isRefreshingAfterSave) return; // Save vừa xong, chỉ refresh ComboBox, không reload
            _ = LoadWorkflowAsync(value);
        }

        [ObservableProperty]
        private bool isExecuting;

        [ObservableProperty]
        private WorkflowConnection? activeExecutionConnection;

        // View state properties for persistence
        [ObservableProperty]
        private double zoomLevel = 1.0;

        [ObservableProperty]
        private double panX = 0.0;

        [ObservableProperty]
        private double panY = 0.0;

        // Connection line style (Bezier/Orthogonal/Straight) per workflow
        [ObservableProperty]
        private ConnectionLineStyle connectionLineStyle = ConnectionLineStyle.Bezier;

        // Có node nào đang chạy hay không (dùng cho Visibility của panel nổi)
        [ObservableProperty]
        private bool hasRunningNodes;

        /// <summary>Phiên chạy thủ công đang mở (toolbar).</summary>
        [ObservableProperty]
        private ObservableCollection<ManualWorkflowRunSessionViewModel> manualRunSessions = new();

        [ObservableProperty]
        private bool hasManualRunSessions;

        [ObservableProperty]
        private bool enableExecutionTraceLog;

        [ObservableProperty]
        private bool isExecutionTracePanelExpanded;

        [ObservableProperty]
        private bool isExecutionTracePanelMaximized;

        /// <summary>
        /// Lưu lại ExecutionTraceDisplayStyle mà user đang dùng trước khi detach → dùng để restore
        /// khi cửa sổ detached đóng lại. Null = chưa detach lần nào / đã restore xong.
        /// Lý do: khi detach ra cửa sổ lớn, force về "Full" để không bị cắt UI; khi dock lại dưới
        /// bottom nhỏ → restore về style user ưa dùng (thường là Compact/Relative).
        /// </summary>
        private string? _executionTraceDisplayStyleBeforeDetach;

        [ObservableProperty]
        private ObservableCollection<ExecutionTraceLogItemViewModel> executionTraceLogs = new();
        [ObservableProperty]
        private ObservableCollection<ExecutionTraceTreeNodeViewModel> executionTraceRunRoots = new();

        private readonly object _executionTraceLock = new();
        private readonly Dictionary<string, Dictionary<string, int>> _executionTraceDepthByRun = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ExecutionTraceTreeNodeViewModel> _executionTraceTreeRootByRun = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ExecutionTraceTreeNodeViewModel>> _executionTraceTreeNodesByRunAndNode = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ExecutionTraceTreeNodeViewModel>> _executionTraceTreeNodesByExecutionId = new(StringComparer.Ordinal);
        private readonly Dictionary<WorkflowNode, List<ExecutionTraceLogItemViewModel>> _pendingTraceRowsByNode = new();
        private readonly Dictionary<WorkflowNode, List<ExecutionTraceTreeNodeViewModel>> _pendingTraceTreeRowsByNode = new();
        private const int MaxExecutionTraceRows = 1200;
        public ICollectionView? ExecutionTraceFilteredView { get; private set; }

        [ObservableProperty]
        private string executionTraceSearchText = string.Empty;

        [ObservableProperty]
        private string executionTraceStatusFilter = "All";

        [ObservableProperty]
        private string executionTraceNodeTypeFilter = "All";

        [ObservableProperty]
        private double executionTraceZoom = 1.0;

        /// <summary>
        /// Giới hạn chiều rộng card log (title + IN/OUT/ERR). Vượt quá sẽ trim ellipsis khi collapsed
        /// hoặc wrap xuống dòng khi user expand từng card.
        /// </summary>
        [ObservableProperty]
        private double executionTraceCardMaxWidth = 380;

        // ---- Cấu hình xuất log (JSON) ----
        // Cho phép bật/tắt từng trường nặng (IN/OUT/ERR) + cắt độ dài để giảm kích thước file.

        [ObservableProperty]
        private bool exportIncludeInput = true;

        [ObservableProperty]
        private bool exportIncludeOutput = true;

        [ObservableProperty]
        private bool exportIncludeError = true;

        /// <summary>
        /// Độ dài tối đa (ký tự) cho IN/OUT/ERR khi export. 0 hoặc âm = không cắt (giữ nguyên).
        /// </summary>
        [ObservableProperty]
        private int exportMaxFieldLength = 0;

        /// <summary>True để kèm cấu trúc cây Run (tree) ngoài danh sách flat rows.</summary>
        [ObservableProperty]
        private bool exportIncludeTree = true;

        /// <summary>True để chỉ xuất các rows đang thỏa filter/search hiện tại.</summary>
        [ObservableProperty]
        private bool exportOnlyCurrentFilter = false;

        /// <summary>True để xuất JSON pretty-printed (dễ đọc, file to hơn một chút).</summary>
        [ObservableProperty]
        private bool exportPrettyPrint = true;

        // ---- DevTools-like docking cho panel log ----
        // Mode string dùng "Bottom" | "Left" | "Right" | "Detached" (dễ bind DataTrigger trong XAML).

        [ObservableProperty]
        private string executionTracePanelDockMode = "Bottom";

        /// <summary>Chiều cao panel khi dock Bottom (user kéo GridSplitter sẽ cập nhật).</summary>
        [ObservableProperty]
        private double executionTracePanelDockHeight = 320;

        /// <summary>Chiều rộng panel khi dock Left/Right.</summary>
        [ObservableProperty]
        private double executionTracePanelDockWidth = 480;

        /// <summary>Position/size của cửa sổ tách rời (persist giữa các lần mở).</summary>
        [ObservableProperty]
        private double executionTracePanelDetachedLeft = double.NaN;
        [ObservableProperty]
        private double executionTracePanelDetachedTop = double.NaN;
        [ObservableProperty]
        private double executionTracePanelDetachedWidth = 820;
        [ObservableProperty]
        private double executionTracePanelDetachedHeight = 520;

        public bool IsTraceDockBottom => string.Equals(ExecutionTracePanelDockMode, "Bottom", StringComparison.OrdinalIgnoreCase);
        public bool IsTraceDockLeft => string.Equals(ExecutionTracePanelDockMode, "Left", StringComparison.OrdinalIgnoreCase);
        public bool IsTraceDockRight => string.Equals(ExecutionTracePanelDockMode, "Right", StringComparison.OrdinalIgnoreCase);
        public bool IsTraceDockDetached => string.Equals(ExecutionTracePanelDockMode, "Detached", StringComparison.OrdinalIgnoreCase);

        // ---- Display style cho log items: "Full" (đầy đủ - default) | "Relative" (tương đối) | "Compact" (thu gọn).
        // XAML dùng DataTrigger trên IsTraceDisplayFull/Relative/Compact để ẩn/hiện các phần chi tiết.

        [ObservableProperty]
        private string executionTraceDisplayStyle = "Full";

        public ObservableCollection<ExecutionTraceDisplayStyleOption> ExecutionTraceDisplayStyleOptions { get; } = new()
        {
            new ExecutionTraceDisplayStyleOption("Full",     "Đầy đủ"),
            new ExecutionTraceDisplayStyleOption("Relative", "Tương đối"),
            new ExecutionTraceDisplayStyleOption("Compact",  "Thu gọn"),
        };

        public bool IsTraceDisplayFull => string.Equals(ExecutionTraceDisplayStyle, "Full", StringComparison.OrdinalIgnoreCase);
        public bool IsTraceDisplayRelative => string.Equals(ExecutionTraceDisplayStyle, "Relative", StringComparison.OrdinalIgnoreCase);
        public bool IsTraceDisplayCompact => string.Equals(ExecutionTraceDisplayStyle, "Compact", StringComparison.OrdinalIgnoreCase);

        /// <summary>True khi panel log đang được hosted trong cửa sổ tách — ẩn hẳn khỏi main grid.</summary>
        public bool IsExecutionTracePanelInMainGrid =>
            IsExecutionTracePanelVisible && !IsTraceDockDetached;

        public ObservableCollection<string> ExecutionTraceStatusOptions { get; } = new() { "All", "running", "completed", "failed" };
        public ObservableCollection<string> ExecutionTraceNodeTypeOptions { get; } = new() { "All" };

        public bool IsExecutionTracePanelVisible => EnableExecutionTraceLog && IsExecutionTracePanelExpanded;

        /// <summary>
        /// True khi user đã bật trace nhưng đang ẩn panel → hiển thị nút nổi để mở lại panel.
        /// </summary>
        public bool IsExecutionTraceReopenerVisible => EnableExecutionTraceLog && !IsExecutionTracePanelExpanded;

        /// <summary>Height truyền cho panel dock=Bottom. Khi maximize: Double.NaN để panel stretch toàn bộ.
        /// Khi bình thường: trả ExecutionTracePanelDockHeight (user resize được qua GridSplitter).</summary>
        public double ExecutionTracePanelHeight => IsExecutionTracePanelMaximized ? double.NaN : ExecutionTracePanelDockHeight;

        /// <summary>True khi panel log đang mở và maximize — ẩn canvas để chỉ xem log.</summary>
        public bool IsExecutionLogMaximizedOverCanvas =>
            IsExecutionTracePanelMaximized && EnableExecutionTraceLog && IsExecutionTracePanelExpanded;

        private int _nodeCounter = 1;

        /// <summary>
        /// Callback để View sync zoom/pan từ host xuống ViewModel trước khi save.
        /// Gọi trước _persistenceService.Save để lưu đúng vị trí view hiện tại.
        /// </summary>
        public Action? SyncViewStateBeforeSave { get; set; }
        public double SavedScreenWidth { get; set; }
        public double SavedScreenHeight { get; set; }
        public double SavedViewportCenterX { get; set; }
        public double SavedViewportCenterY { get; set; }

        public WorkflowEditorViewModel(
            FlowMy.Workflow.TemplateFactory templateFactory,
            WorkflowExecutionService workflowExecutionService,
            IWorkflowPersistenceService persistenceService,
            IWorkflowExecutionVisualizer executionVisualizer)
        {
            _templateFactory = templateFactory ?? throw new ArgumentNullException(nameof(templateFactory));
            _workflowExecutionService = workflowExecutionService ?? throw new ArgumentNullException(nameof(workflowExecutionService));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _executionVisualizer = executionVisualizer ?? throw new ArgumentNullException(nameof(executionVisualizer));
            RefreshSavedWorkflows();

            if (Nodes.Count == 0)
            {
                InitializeSampleNodes();
            }

            // Khôi phục cấu hình Execution Trace panel đã lưu từ lần chạy trước (AppData\FlowMy).
            try
            {
                var prefs = FlowMy.Services.Utilities.ExecutionTracePreferencesStore.Load();
                enableExecutionTraceLog = prefs.EnableExecutionTraceLog;
                isExecutionTracePanelExpanded = prefs.IsExecutionTracePanelExpanded;
                if (prefs.ExecutionTraceCardMaxWidth >= 200) executionTraceCardMaxWidth = prefs.ExecutionTraceCardMaxWidth;
                if (!string.IsNullOrWhiteSpace(prefs.ExecutionTracePanelDockMode))
                    executionTracePanelDockMode = prefs.ExecutionTracePanelDockMode!;
                if (prefs.ExecutionTracePanelDockHeight >= 120) executionTracePanelDockHeight = prefs.ExecutionTracePanelDockHeight;
                if (prefs.ExecutionTracePanelDockWidth >= 240) executionTracePanelDockWidth = prefs.ExecutionTracePanelDockWidth;
                executionTracePanelDetachedLeft = prefs.ExecutionTracePanelDetachedLeft;
                executionTracePanelDetachedTop = prefs.ExecutionTracePanelDetachedTop;
                if (prefs.ExecutionTracePanelDetachedWidth >= 300) executionTracePanelDetachedWidth = prefs.ExecutionTracePanelDetachedWidth;
                if (prefs.ExecutionTracePanelDetachedHeight >= 200) executionTracePanelDetachedHeight = prefs.ExecutionTracePanelDetachedHeight;
                if (!string.IsNullOrWhiteSpace(prefs.ExecutionTraceDisplayStyle))
                    executionTraceDisplayStyle = prefs.ExecutionTraceDisplayStyle!;
                exportIncludeInput = prefs.ExportIncludeInput;
                exportIncludeOutput = prefs.ExportIncludeOutput;
                exportIncludeError = prefs.ExportIncludeError;
                exportMaxFieldLength = prefs.ExportMaxFieldLength;
                exportIncludeTree = prefs.ExportIncludeTree;
                exportOnlyCurrentFilter = prefs.ExportOnlyCurrentFilter;
                exportPrettyPrint = prefs.ExportPrettyPrint;
            }
            catch
            {
                enableExecutionTraceLog = false;
                isExecutionTracePanelExpanded = true;
            }
            ExecutionTraceFilteredView = CollectionViewSource.GetDefaultView(ExecutionTraceLogs);
            if (ExecutionTraceFilteredView != null)
            {
                ExecutionTraceFilteredView.Filter = ExecutionTraceFilterPredicate;
                ExecutionTraceFilteredView.GroupDescriptions.Clear();
                ExecutionTraceFilteredView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ExecutionTraceLogItemViewModel.RootExecutionId)));
            }
        }

        partial void OnEnableExecutionTraceLogChanged(bool value)
        {
            // Khi user bật lại trace từ checkbox toolbar, tự mở panel để thấy log ngay.
            if (value && !IsExecutionTracePanelExpanded)
                IsExecutionTracePanelExpanded = true;

            OnPropertyChanged(nameof(IsExecutionTracePanelVisible));
            OnPropertyChanged(nameof(IsExecutionTraceReopenerVisible));
            OnPropertyChanged(nameof(IsExecutionLogMaximizedOverCanvas));
            OnPropertyChanged(nameof(IsExecutionTracePanelInMainGrid));
            if (!value)
                ClearExecutionTraceLogs();
            SaveExecutionTracePreferencesSafe();
        }

        partial void OnIsExecutionTracePanelExpandedChanged(bool value)
        {
            // Nếu đang maximize và user ẩn panel → cũng un-maximize để canvas hiện lại,
            // tránh trường hợp panel ẩn nhưng canvas vẫn bị che khi lần sau mở lại.
            if (!value && IsExecutionTracePanelMaximized)
                IsExecutionTracePanelMaximized = false;
            OnPropertyChanged(nameof(IsExecutionTracePanelVisible));
            OnPropertyChanged(nameof(IsExecutionTraceReopenerVisible));
            OnPropertyChanged(nameof(IsExecutionLogMaximizedOverCanvas));
            OnPropertyChanged(nameof(IsExecutionTracePanelInMainGrid));
            SaveExecutionTracePreferencesSafe();
        }

        partial void OnIsExecutionTracePanelMaximizedChanged(bool value)
        {
            OnPropertyChanged(nameof(ExecutionTracePanelHeight));
            OnPropertyChanged(nameof(IsExecutionLogMaximizedOverCanvas));
        }

        partial void OnExecutionTracePanelDockModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsTraceDockBottom));
            OnPropertyChanged(nameof(IsTraceDockLeft));
            OnPropertyChanged(nameof(IsTraceDockRight));
            OnPropertyChanged(nameof(IsTraceDockDetached));
            OnPropertyChanged(nameof(IsExecutionTracePanelInMainGrid));
            SaveExecutionTracePreferencesSafe();
        }

        partial void OnExecutionTraceDisplayStyleChanged(string value)
        {
            OnPropertyChanged(nameof(IsTraceDisplayFull));
            OnPropertyChanged(nameof(IsTraceDisplayRelative));
            OnPropertyChanged(nameof(IsTraceDisplayCompact));
            SaveExecutionTracePreferencesSafe();
        }

        partial void OnExecutionTracePanelDockHeightChanged(double value)
        {
            OnPropertyChanged(nameof(ExecutionTracePanelHeight));
            SaveExecutionTracePreferencesSafe();
        }

        partial void OnExecutionTracePanelDockWidthChanged(double value) => SaveExecutionTracePreferencesSafe();
        partial void OnExecutionTracePanelDetachedLeftChanged(double value) => SaveExecutionTracePreferencesSafe();
        partial void OnExecutionTracePanelDetachedTopChanged(double value) => SaveExecutionTracePreferencesSafe();
        partial void OnExecutionTracePanelDetachedWidthChanged(double value) => SaveExecutionTracePreferencesSafe();
        partial void OnExecutionTracePanelDetachedHeightChanged(double value) => SaveExecutionTracePreferencesSafe();

        partial void OnExecutionTraceCardMaxWidthChanged(double value) => SaveExecutionTracePreferencesSafe();
        partial void OnExportIncludeInputChanged(bool value) => SaveExecutionTracePreferencesSafe();
        partial void OnExportIncludeOutputChanged(bool value) => SaveExecutionTracePreferencesSafe();
        partial void OnExportIncludeErrorChanged(bool value) => SaveExecutionTracePreferencesSafe();
        partial void OnExportMaxFieldLengthChanged(int value) => SaveExecutionTracePreferencesSafe();
        partial void OnExportIncludeTreeChanged(bool value) => SaveExecutionTracePreferencesSafe();
        partial void OnExportOnlyCurrentFilterChanged(bool value) => SaveExecutionTracePreferencesSafe();
        partial void OnExportPrettyPrintChanged(bool value) => SaveExecutionTracePreferencesSafe();

        private void SaveExecutionTracePreferencesSafe()
        {
            try
            {
                FlowMy.Services.Utilities.ExecutionTracePreferencesStore.Save(new FlowMy.Services.Utilities.ExecutionTracePreferences
                {
                    EnableExecutionTraceLog = EnableExecutionTraceLog,
                    IsExecutionTracePanelExpanded = IsExecutionTracePanelExpanded,
                    ExecutionTraceCardMaxWidth = ExecutionTraceCardMaxWidth,
                    ExecutionTracePanelDockMode = ExecutionTracePanelDockMode,
                    ExecutionTracePanelDockHeight = ExecutionTracePanelDockHeight,
                    ExecutionTracePanelDockWidth = ExecutionTracePanelDockWidth,
                    ExecutionTracePanelDetachedLeft = ExecutionTracePanelDetachedLeft,
                    ExecutionTracePanelDetachedTop = ExecutionTracePanelDetachedTop,
                    ExecutionTracePanelDetachedWidth = ExecutionTracePanelDetachedWidth,
                    ExecutionTracePanelDetachedHeight = ExecutionTracePanelDetachedHeight,
                    ExecutionTraceDisplayStyle = ExecutionTraceDisplayStyle,
                    ExportIncludeInput = ExportIncludeInput,
                    ExportIncludeOutput = ExportIncludeOutput,
                    ExportIncludeError = ExportIncludeError,
                    ExportMaxFieldLength = ExportMaxFieldLength,
                    ExportIncludeTree = ExportIncludeTree,
                    ExportOnlyCurrentFilter = ExportOnlyCurrentFilter,
                    ExportPrettyPrint = ExportPrettyPrint,
                });
            }
            catch { /* best-effort */ }
        }

        partial void OnExecutionTraceSearchTextChanged(string value) => RefreshExecutionTraceFilter();
        partial void OnExecutionTraceStatusFilterChanged(string value) => RefreshExecutionTraceFilter();
        partial void OnExecutionTraceNodeTypeFilterChanged(string value) => RefreshExecutionTraceFilter();

        [RelayCommand]
        private void ToggleExecutionTracePanel()
        {
            IsExecutionTracePanelExpanded = !IsExecutionTracePanelExpanded;
        }

        [RelayCommand]
        private void ToggleExecutionTracePanelMaximize()
        {
            IsExecutionTracePanelMaximized = !IsExecutionTracePanelMaximized;
        }

        /// <summary>
        /// View gọi sau khi <c>ExecutionTraceDetachedWindow</c> đóng — restore display style user từng dùng
        /// (Full/Relative/Compact) trước khi detach. Nếu chưa từng cache (null) thì giữ nguyên.
        /// </summary>
        public void RestoreExecutionTraceDisplayStyleAfterAttach()
        {
            if (string.IsNullOrWhiteSpace(_executionTraceDisplayStyleBeforeDetach))
                return;
            ExecutionTraceDisplayStyle = _executionTraceDisplayStyleBeforeDetach!;
            _executionTraceDisplayStyleBeforeDetach = null;
        }

        /// <summary>Đặt vị trí dock panel log: Bottom/Left/Right/Detached (nhận từ XAML CommandParameter).</summary>
        [RelayCommand]
        private void SetExecutionTracePanelDockMode(object? parameter)
        {
            var mode = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(mode)) return;
            // Chuẩn hóa để khớp với so sánh IsTraceDockBottom/Left/Right/Detached (OrdinalIgnoreCase).
            switch (mode.Trim().ToLowerInvariant())
            {
                case "bottom": ExecutionTracePanelDockMode = "Bottom"; break;
                case "left": ExecutionTracePanelDockMode = "Left"; break;
                case "right": ExecutionTracePanelDockMode = "Right"; break;
                case "detached":
                case "detach":
                case "float":
                    {
                        // User click nút Detach. Có 2 nhánh:
                        // (a) Chưa ở mode Detached → set để view tạo/hiển thị window.
                        // (b) Đã ở mode Detached nhưng window đang bị minimize hoặc bị khuất →
                        //     giá trị property không đổi nên PropertyChanged không fire, view sẽ không biết
                        //     để restore. Vì vậy force raise PropertyChanged để Sync logic chạy lại và
                        //     gọi RestoreAndActivate() trên window đã tồn tại.
                        bool wasAlreadyDetached = string.Equals(
                            ExecutionTracePanelDockMode, "Detached", StringComparison.OrdinalIgnoreCase);

                        // Lưu display style user đang dùng để restore khi đóng window. CHỈ lưu khi lần đầu
                        // detach (wasAlreadyDetached=false) để tránh ghi đè bằng "Full" đã auto-set.
                        if (!wasAlreadyDetached)
                        {
                            _executionTraceDisplayStyleBeforeDetach = ExecutionTraceDisplayStyle;
                            // Force display style về "Full" → window lớn hiển thị card đầy đủ, đẹp như user yêu cầu
                            // (tránh cảnh dock compact chuyển vào cửa sổ lớn bị trông trống/nhỏ).
                            if (!string.Equals(ExecutionTraceDisplayStyle, "Full", StringComparison.OrdinalIgnoreCase))
                                ExecutionTraceDisplayStyle = "Full";
                        }

                        if (wasAlreadyDetached)
                            OnPropertyChanged(nameof(ExecutionTracePanelDockMode));
                        else
                            ExecutionTracePanelDockMode = "Detached";

                        // Đảm bảo panel đang expanded khi detach — view sẽ tự mở window khi thấy mode=Detached.
                        if (!IsExecutionTracePanelExpanded) IsExecutionTracePanelExpanded = true;
                        break;
                    }
            }
        }

        [RelayCommand]
        private void ClearExecutionTraceLogs()
        {
            Application.Current?.Dispatcher?.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                lock (_executionTraceLock)
                {
                    ExecutionTraceLogs.Clear();
                    ExecutionTraceRunRoots.Clear();
                    _executionTraceDepthByRun.Clear();
                    _executionTraceTreeRootByRun.Clear();
                    _executionTraceTreeNodesByRunAndNode.Clear();
                    _executionTraceTreeNodesByExecutionId.Clear();
                    _pendingTraceRowsByNode.Clear();
                    _pendingTraceTreeRowsByNode.Clear();
                    ExecutionTraceNodeTypeOptions.Clear();
                    ExecutionTraceNodeTypeOptions.Add("All");
                    ExecutionTraceNodeTypeFilter = "All";
                }
                RefreshExecutionTraceFilter();
            }));
        }

        [RelayCommand]
        private void ExportExecutionTraceLogs()
        {
            if (ExecutionTraceLogs.Count == 0)
            {
                MessageBox.Show("Chưa có log để xuất.", "Execution Log", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var configDialog = new FlowMy.Views.Overlays.ExecutionTraceExportDialog
            {
                Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                     ?? Application.Current?.MainWindow,
                DataContext = this
            };
            if (configDialog.ShowDialog() != true) return;

            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = $"execution-log-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                Title = "Xuất execution log"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var includeIn = ExportIncludeInput;
                var includeOut = ExportIncludeOutput;
                var includeErr = ExportIncludeError;
                var maxLen = ExportMaxFieldLength > 0 ? ExportMaxFieldLength : 0;
                var onlyFiltered = ExportOnlyCurrentFilter;

                IEnumerable<ExecutionTraceLogItemViewModel> source;
                lock (_executionTraceLock)
                {
                    source = onlyFiltered && ExecutionTraceFilteredView != null
                        ? ExecutionTraceFilteredView.Cast<ExecutionTraceLogItemViewModel>().ToList()
                        : ExecutionTraceLogs.ToList();
                }

                var rows = source.Select(x => (object)new
                {
                    x.TimestampUtc,
                    x.TimestampText,
                    x.RootExecutionId,
                    x.ExecutionId,
                    x.Status,
                    x.ElapsedText,
                    x.NodeId,
                    x.NodeTitle,
                    x.NodeType,
                    x.ParentNodeId,
                    x.ParentNodeTitle,
                    x.Depth,
                    InputSummary = includeIn ? TruncateForExport(x.InputSummary, maxLen) : null,
                    OutputSummary = includeOut ? TruncateForExport(x.OutputSummary, maxLen) : null,
                    ErrorMessage = includeErr ? TruncateForExport(x.ErrorMessage, maxLen) : null
                }).ToList();

                object? treePayload = null;
                if (ExportIncludeTree)
                {
                    lock (_executionTraceLock)
                    {
                        treePayload = ExecutionTraceRunRoots.Select(r => BuildExportTreeNode(r, includeIn, includeOut, includeErr, maxLen)).ToList();
                    }
                }

                var payload = new
                {
                    exportedAtUtc = DateTime.UtcNow,
                    filters = new
                    {
                        search = ExecutionTraceSearchText,
                        status = ExecutionTraceStatusFilter,
                        nodeType = ExecutionTraceNodeTypeFilter,
                        onlyFiltered
                    },
                    options = new
                    {
                        includeInput = includeIn,
                        includeOutput = includeOut,
                        includeError = includeErr,
                        maxFieldLength = maxLen,
                        includeTree = ExportIncludeTree,
                        prettyPrint = ExportPrettyPrint
                    },
                    total = rows.Count,
                    items = rows,
                    tree = treePayload
                };

                // MaxDepth mặc định của System.Text.Json = 64. Tree execution có thể lồng sâu (AsyncTask/Callback loop lặp lại),
                // nên ta đẩy lên 1024 để không bị "object cycle / depth > 64" khi xuất log. ReferenceHandler.IgnoreCycles
                // phòng hờ trường hợp cấu trúc tự tham chiếu (không xảy ra hiện tại nhưng an toàn cho tương lai).
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = ExportPrettyPrint,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    MaxDepth = 1024,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                });
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show("Đã xuất execution log JSON thành công.", "Execution Log", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xuất log thất bại: {ex.Message}", "Execution Log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string? TruncateForExport(string? value, int maxLen)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (maxLen <= 0 || value.Length <= maxLen) return value;
            return value.Substring(0, maxLen) + "...";
        }

        private static object BuildExportTreeNode(
            ExecutionTraceTreeNodeViewModel vm,
            bool includeIn,
            bool includeOut,
            bool includeErr,
            int maxLen)
        {
            return new
            {
                vm.RootExecutionId,
                vm.ExecutionId,
                vm.NodeId,
                vm.NodeTitle,
                vm.NodeType,
                vm.ParentNodeId,
                vm.Depth,
                vm.IsRunRoot,
                vm.Status,
                vm.ElapsedText,
                InputSummary = includeIn ? TruncateForExport(vm.InputSummary, maxLen) : null,
                OutputSummary = includeOut ? TruncateForExport(vm.OutputSummary, maxLen) : null,
                ErrorMessage = includeErr ? TruncateForExport(vm.ErrorMessage, maxLen) : null,
                Children = vm.Children.Select(c => BuildExportTreeNode(c, includeIn, includeOut, includeErr, maxLen)).ToList()
            };
        }

        [RelayCommand]
        private void ExpandAllExecutionTraceTree()
        {
            lock (_executionTraceLock)
            {
                foreach (var root in ExecutionTraceRunRoots)
                    SetTreeExpandedRecursive(root, true);
            }
        }

        [RelayCommand]
        private void CollapseAllExecutionTraceTree()
        {
            lock (_executionTraceLock)
            {
                foreach (var root in ExecutionTraceRunRoots)
                    SetTreeExpandedRecursive(root, false);
            }
        }

        [RelayCommand]
        private void ZoomInExecutionTrace()
        {
            ExecutionTraceZoom = Math.Min(2.0, ExecutionTraceZoom + 0.1);
        }

        [RelayCommand]
        private void ZoomOutExecutionTrace()
        {
            ExecutionTraceZoom = Math.Max(0.6, ExecutionTraceZoom - 0.1);
        }

        /// <summary>
        /// Copy text (IN/OUT/ERR hoặc 1 chip key:value) trên card trace vào clipboard.
        /// Nhận <see cref="string"/> làm parameter để binding thẳng từ XAML.
        /// </summary>
        [RelayCommand]
        private void CopyTraceText(object? parameter)
        {
            try
            {
                var text = parameter?.ToString();
                if (string.IsNullOrEmpty(text)) return;
                System.Windows.Clipboard.SetText(text);
            }
            catch
            {
                // Clipboard có thể busy khi app khác đang giữ; bỏ qua để không phá UX.
            }
        }

        private static void SetTreeExpandedRecursive(ExecutionTraceTreeNodeViewModel node, bool expanded)
        {
            node.IsExpanded = expanded;
            foreach (var child in node.Children)
                SetTreeExpandedRecursive(child, expanded);
        }

        private bool ExecutionTraceFilterPredicate(object obj)
        {
            if (obj is not ExecutionTraceLogItemViewModel row) return false;

            if (!string.IsNullOrWhiteSpace(ExecutionTraceStatusFilter) &&
                !string.Equals(ExecutionTraceStatusFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(row.Status, ExecutionTraceStatusFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(ExecutionTraceNodeTypeFilter) &&
                !string.Equals(ExecutionTraceNodeTypeFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(row.NodeType, ExecutionTraceNodeTypeFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            var q = (ExecutionTraceSearchText ?? string.Empty).Trim();
            if (q.Length == 0) return true;
            return (row.NodeTitle?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (row.NodeId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (row.InputSummary?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (row.OutputSummary?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (row.ErrorMessage?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void RefreshExecutionTraceFilter()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                ExecutionTraceFilteredView?.Refresh();
                ApplyExecutionTraceFilterToTree();
                return;
            }
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                ExecutionTraceFilteredView?.Refresh();
                ApplyExecutionTraceFilterToTree();
            }));
        }

        private static string BuildTreeRunNodeKey(string rootExecutionId, string nodeId)
            => $"{rootExecutionId}|{nodeId}";

        private static ExecutionTraceTreeNodeViewModel? PickBestParentTreeNode(
            List<ExecutionTraceTreeNodeViewModel> parentCandidates,
            string childExecutionId)
        {
            if (parentCandidates == null || parentCandidates.Count == 0)
                return null;

            if (string.IsNullOrWhiteSpace(childExecutionId))
                return parentCandidates[^1];

            var preferredIds = WorkflowKeyValueStore
                .EnumerateScopedLookupExecutionIds(childExecutionId)
                .ToHashSet(StringComparer.Ordinal);

            for (var i = parentCandidates.Count - 1; i >= 0; i--)
            {
                var p = parentCandidates[i];
                if (preferredIds.Contains(p.ExecutionId))
                    return p;
            }

            // Fallback: exact prefix relation helps async dispatch trees.
            for (var i = parentCandidates.Count - 1; i >= 0; i--)
            {
                var p = parentCandidates[i];
                if (childExecutionId.StartsWith(p.ExecutionId, StringComparison.Ordinal))
                    return p;
            }

            return parentCandidates[^1];
        }

        private ExecutionTraceTreeNodeViewModel? PickBestParentFromExecutionChain(string childExecutionId, string rootExecutionId)
        {
            if (string.IsNullOrWhiteSpace(childExecutionId))
                return null;

            foreach (var execId in WorkflowKeyValueStore.EnumerateScopedLookupExecutionIds(childExecutionId))
            {
                if (!_executionTraceTreeNodesByExecutionId.TryGetValue(execId, out var byExec) || byExec.Count == 0)
                    continue;

                for (var i = byExec.Count - 1; i >= 0; i--)
                {
                    var candidate = byExec[i];
                    if (!string.Equals(candidate.RootExecutionId, rootExecutionId, StringComparison.Ordinal))
                        continue;
                    if (string.Equals(candidate.ExecutionId, childExecutionId, StringComparison.Ordinal) && i == byExec.Count - 1)
                        continue;
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Pixels trimmed from the bottom of the children dashed trunk so it stops before the last child row.</summary>
        private const double ExecutionTraceTrunkTrimLastChildPx = 56d;

        private static void RefreshTreeConnectorMetadata(ExecutionTraceTreeNodeViewModel parent)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                var child = parent.Children[i];
                child.Parent = parent;
                child.IsFirstSibling = i == 0;
                child.IsLastSibling = i == parent.Children.Count - 1;
                RefreshTreeConnectorMetadata(child);
            }

            var trim = 0d;
            if (parent.Children.Count > 0 &&
                string.Equals(parent.Children[^1].NodeType, nameof(NodeType.End), StringComparison.OrdinalIgnoreCase))
            {
                trim = ExecutionTraceTrunkTrimLastChildPx;
            }

            if (Math.Abs(parent.ChildrenDashedLineHeightTrim - trim) > 0.01d)
                parent.ChildrenDashedLineHeightTrim = trim;
        }

        private static void AttachTreeChild(ExecutionTraceTreeNodeViewModel parent, ExecutionTraceTreeNodeViewModel child, int? insertIndex = null)
        {
            if (insertIndex is >= 0 and int ix && ix <= parent.Children.Count)
                parent.Children.Insert(ix, child);
            else
                parent.Children.Add(child);
            RefreshTreeConnectorMetadata(parent);
            parent.NotifyHasChildrenChanged();
        }

        /// <summary>Node vùng body của AsyncTask (scope LoopBodyTop), không phải chính node AsyncTask.</summary>
        private static bool IsAsyncTaskBodyHostNode(WorkflowNode? n)
        {
            if (n == null) return false;
            if (n.Id.StartsWith("AsyncTaskBody_", StringComparison.OrdinalIgnoreCase))
                return true;
            return n.Ports.Any(p => string.Equals(p.Id, "LoopBodyTop", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Tìm AsyncTask nối LoopNodeBottom → LoopBodyTop vào body này (đúng theo graph workflow).</summary>
        private bool TryGetAsyncTaskOwningBodyNode(WorkflowNode? bodyNode, out WorkflowNode? asyncTaskNode)
        {
            asyncTaskNode = null;
            if (bodyNode == null || !IsAsyncTaskBodyHostNode(bodyNode))
                return false;

            foreach (var conn in Connections)
            {
                if (conn.ToNode != bodyNode || conn.ToPort == null)
                    continue;
                if (!string.Equals(conn.ToPort.Id, "LoopBodyTop", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (conn.FromNode?.Type == NodeType.AsyncTask)
                {
                    asyncTaskNode = conn.FromNode;
                    return true;
                }
            }

            return false;
        }

        private ExecutionTraceTreeNodeViewModel? PickLatestNodeInExactExecution(string executionId, string rootExecutionId, string currentNodeId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                return null;
            if (!_executionTraceTreeNodesByExecutionId.TryGetValue(executionId, out var rows) || rows.Count == 0)
                return null;

            for (var i = rows.Count - 1; i >= 0; i--)
            {
                var candidate = rows[i];
                if (!string.Equals(candidate.RootExecutionId, rootExecutionId, StringComparison.Ordinal))
                    continue;
                if (string.Equals(candidate.NodeId, currentNodeId, StringComparison.OrdinalIgnoreCase))
                    continue;
                return candidate;
            }
            return null;
        }

        private ExecutionTraceTreeNodeViewModel? PickNearestAsyncTaskInExecutionChain(string childExecutionId, string rootExecutionId)
        {
            if (string.IsNullOrWhiteSpace(childExecutionId))
                return null;

            foreach (var execId in WorkflowKeyValueStore.EnumerateScopedLookupExecutionIds(childExecutionId))
            {
                if (!_executionTraceTreeNodesByExecutionId.TryGetValue(execId, out var rows) || rows.Count == 0)
                    continue;

                for (var i = rows.Count - 1; i >= 0; i--)
                {
                    var candidate = rows[i];
                    if (!string.Equals(candidate.RootExecutionId, rootExecutionId, StringComparison.Ordinal))
                        continue;
                    if (!string.Equals(candidate.NodeType, NodeType.AsyncTask.ToString(), StringComparison.OrdinalIgnoreCase))
                        continue;
                    return candidate;
                }
            }

            return null;
        }

        private static TRow? TakePendingRow<TRow>(
            Dictionary<WorkflowNode, List<TRow>> pendingByNode,
            WorkflowNode node,
            string? preferredExecutionId,
            Func<TRow, string> executionIdSelector,
            Func<TRow, string> statusSelector)
            where TRow : class
        {
            if (!pendingByNode.TryGetValue(node, out var rows) || rows.Count == 0)
                return null;

            var idx = -1;
            if (!string.IsNullOrWhiteSpace(preferredExecutionId))
            {
                idx = rows.FindIndex(r =>
                    string.Equals(executionIdSelector(r), preferredExecutionId, StringComparison.Ordinal) &&
                    string.Equals(statusSelector(r), "running", StringComparison.OrdinalIgnoreCase));
            }
            if (idx < 0)
            {
                idx = rows.FindIndex(r => string.Equals(statusSelector(r), "running", StringComparison.OrdinalIgnoreCase));
            }
            if (idx < 0) idx = rows.Count - 1;
            if (idx < 0) return null;

            var row = rows[idx];
            rows.RemoveAt(idx);
            if (rows.Count == 0)
                pendingByNode.Remove(node);
            return row;
        }

        private static bool RowMatchesCurrentExecutionTraceFilter(
            ExecutionTraceTreeNodeViewModel row,
            string statusFilter,
            string nodeTypeFilter,
            string searchText)
        {
            if (row.IsRunRoot) return true;

            if (!string.IsNullOrWhiteSpace(statusFilter) &&
                !string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(row.Status, statusFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(nodeTypeFilter) &&
                !string.Equals(nodeTypeFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(row.NodeType, nodeTypeFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            var q = (searchText ?? string.Empty).Trim();
            if (q.Length == 0) return true;
            return (row.NodeTitle?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (row.NodeId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (row.InputSummary?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (row.OutputSummary?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (row.ErrorMessage?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void ApplyExecutionTraceFilterToTree()
        {
            bool ApplyNode(ExecutionTraceTreeNodeViewModel node, string status, string type, string search)
            {
                var self = RowMatchesCurrentExecutionTraceFilter(node, status, type, search);
                var anyChildVisible = false;
                foreach (var child in node.Children)
                {
                    if (ApplyNode(child, status, type, search))
                        anyChildVisible = true;
                }

                node.IsVisible = node.IsRunRoot ? anyChildVisible : (self || anyChildVisible);
                return node.IsVisible;
            }

            lock (_executionTraceLock)
            {
                var status = ExecutionTraceStatusFilter;
                var type = ExecutionTraceNodeTypeFilter;
                var search = ExecutionTraceSearchText;
                foreach (var root in ExecutionTraceRunRoots)
                    ApplyNode(root, status, type, search);
            }
        }

        private static string ResolveNodeIconKey(WorkflowNode node)
        {
            return node.Type switch
            {
                NodeType.Start => "play duotone-regular",
                NodeType.End => "flag-checkered sharp-duotone-solid",
                NodeType.Input => "left-to-dotted-line duotone-regular",
                NodeType.Output => "right-to-dotted-line duotone-regular",
                NodeType.Process => "cog",
                NodeType.IfElse => "list-tree sharp-light",
                NodeType.Loop => "arrows-spin duotone",
                NodeType.Break => "circle-stop duotone",
                NodeType.Continue => "diagram-predecessor duotone-light",
                NodeType.Delay => "timer regular",
                NodeType.Keyboard => "keyboard duotone",
                NodeType.KeyPressEvent => "key duotone-regular",
                NodeType.HotkeyPressEvent => "keyboard duotone",
                NodeType.MouseEvent => "computer-mouse duotone",
                NodeType.Variable => "square-root-variable",
                NodeType.Function => "calculator",
                NodeType.ScreenPosition => "crosshairs sharp-duotone-solid",
                NodeType.ScreenCapture => "camera-viewfinder duotone-light",
                NodeType.StringSplit => "scissors light",
                NodeType.ListOut => "list-radio regular",
                NodeType.AssignData => "arrows-left-right duotone",
                NodeType.MediaGallery => "image-stack duotone",
                NodeType.ImageProcessing => "image notdog-duo-solid",
                NodeType.Code => "code duotone-regular",
                NodeType.HtmlUi => "html5 brands",
                NodeType.Folder => "folder-open duotone-thin",
                NodeType.HttpRequest => "globe-pointer sharp-duotone-light",
                NodeType.Web => "internet-explorer brands",
                NodeType.AsyncTask => "diagram-project duotone-light",
                NodeType.DataFetcher => "inbox-out duotone-light",
                NodeType.FolderFilePaths => "file-import duotone-light",
                NodeType.KeyValueBridge => "list-check solid",
                NodeType.FlowOverwrite => "merge sharp-regular",
                NodeType.Notification => "message-captions duotone-regular",
                NodeType.Storage => "arrow-progress sharp-regular",
                NodeType.Callback => "arrows-turn-right regular",
                NodeType.FileDownload => "download solid",
                NodeType.AsyncTaskDispatchCollect => "list-radio regular",
                NodeType.KeyScopedStore => "arrow-progress sharp-regular",
                NodeType.LoopContext => "arrows-spin duotone",
                NodeType.Condition => "list-tree sharp-light",
                _ => "circle-nodes duotone-regular"
            };
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            var ms = elapsed.TotalMilliseconds;
            if (ms < 1) return "<1 ms";
            if (ms >= 1000) return $"{elapsed.TotalSeconds:0.#} s";
            return $"{ms:0} ms";
        }

        private static string ToCompactText(string? raw, int maxLen = 220)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var t = raw.Replace("\r", " ").Replace("\n", " ").Trim();
            return t.Length <= maxLen ? t : t[..maxLen] + "...";
        }

        private static string BuildInputSummary(WorkflowNode node)
        {
            if (node.DynamicInputs == null || node.DynamicInputs.Count == 0) return string.Empty;
            var parts = new List<string>();
            foreach (var inp in node.DynamicInputs)
            {
                var key = inp?.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;
                var v = ToCompactText(inp?.UserValueOverride);
                if (string.IsNullOrWhiteSpace(v)) continue;
                parts.Add($"{key}: {v}");
                if (parts.Count >= 4) break;
            }
            return string.Join(" | ", parts);
        }

        private static string BuildOutputSummary(WorkflowNode node)
        {
            if (node.DynamicOutputs == null || node.DynamicOutputs.Count == 0) return string.Empty;
            var parts = new List<string>();
            foreach (var outp in node.DynamicOutputs)
            {
                var key = outp?.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;
                var v = ToCompactText(outp?.UserValueOverride);
                if (string.IsNullOrWhiteSpace(v)) continue;
                parts.Add($"{key}: {v}");
                if (parts.Count >= 4) break;
            }
            return string.Join(" | ", parts);
        }

        private string BuildInputSummaryForExecution(WorkflowNode node, WorkflowConnection? incoming, string executionId)
            => BuildInputSummaryForExecutionCore(node, incoming, executionId, maxPerValue: 220, maxParts: 4);

        /// <summary>Bản không cắt ngắn của <see cref="BuildInputSummaryForExecution"/> — dùng khi user mở rộng card trace.</summary>
        private string BuildFullInputSummaryForExecution(WorkflowNode node, WorkflowConnection? incoming, string executionId)
            => BuildInputSummaryForExecutionCore(node, incoming, executionId, maxPerValue: int.MaxValue, maxParts: int.MaxValue);

        private string BuildInputSummaryForExecutionCore(WorkflowNode node, WorkflowConnection? incoming, string executionId, int maxPerValue, int maxParts)
        {
            if (incoming?.FromNode == null || string.IsNullOrWhiteSpace(executionId))
                return BuildInputSummary(node);

            var source = incoming.FromNode;
            if (source.DynamicOutputs == null || source.DynamicOutputs.Count == 0)
                return BuildInputSummary(node);

            var parts = new List<string>();
            foreach (var outp in source.DynamicOutputs)
            {
                var key = outp?.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!_workflowExecutionService.TryGetScopedNodeStringOutputForLookupChain(executionId, source.Id, key, out var val))
                    continue;
                var compact = ToCompactText(val, maxPerValue);
                if (string.IsNullOrWhiteSpace(compact)) continue;
                parts.Add($"{source.Title ?? source.Id}.{key}: {compact}");
                if (parts.Count >= maxParts) break;
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : BuildInputSummary(node);
        }

        private string BuildOutputSummaryForExecution(WorkflowNode node, string executionId)
            => BuildOutputSummaryForExecutionCore(node, executionId, maxPerValue: 220, maxParts: 4);

        /// <summary>Bản không cắt ngắn của <see cref="BuildOutputSummaryForExecution"/> — dùng khi user mở rộng card trace.</summary>
        private string BuildFullOutputSummaryForExecution(WorkflowNode node, string executionId)
            => BuildOutputSummaryForExecutionCore(node, executionId, maxPerValue: int.MaxValue, maxParts: int.MaxValue);

        private string BuildOutputSummaryForExecutionCore(WorkflowNode node, string executionId, int maxPerValue, int maxParts)
        {
            if (string.IsNullOrWhiteSpace(executionId) || node.DynamicOutputs == null || node.DynamicOutputs.Count == 0)
                return BuildOutputSummary(node);

            var parts = new List<string>();
            foreach (var outp in node.DynamicOutputs)
            {
                var key = outp?.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!_workflowExecutionService.TryGetScopedNodeStringOutputForLookupChain(executionId, node.Id, key, out var val))
                    continue;
                var compact = ToCompactText(val, maxPerValue);
                if (string.IsNullOrWhiteSpace(compact)) continue;
                parts.Add($"{key}: {compact}");
                if (parts.Count >= maxParts) break;
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : BuildOutputSummary(node);
        }

        private Brush? ResolveTraceNodeBrush(WorkflowNode node)
        {
            if (node.NodeBrush != null)
                return node.NodeBrush;

            var colorKey = (node.ColorKey ?? string.Empty).Trim();
            if (colorKey.Length > 0)
            {
                var themed = GetBrushFromTheme($"{colorKey}Brush");
                if (themed != null) return themed;
                themed = GetBrushFromTheme(colorKey);
                if (themed != null) return themed;
            }

            return GetBrushFromTheme("AccentBrush");
        }

        private static string NormalizeRootExecutionId(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return string.Empty;
            var list = WorkflowKeyValueStore.EnumerateScopedLookupExecutionIds(executionId).ToList();
            return list.Count > 0 ? list[^1] : executionId;
        }

        private void TraceNodeStarted(WorkflowNode node, WorkflowConnection? incoming, string laneId)
        {
            if (!EnableExecutionTraceLog || node == null) return;
            // Ưu tiên AsyncLocal (an toàn với AsyncTask dispatch song song) rồi mới fallback field chia sẻ.
            var ambient = FlowMy.Services.Workflow.WorkflowExecutionContext.CurrentExecutionId;
            var executionKey = !string.IsNullOrWhiteSpace(ambient)
                ? ambient!
                : (string.IsNullOrWhiteSpace(node.LastExecutionId) ? laneId : node.LastExecutionId!);
            var rootExecutionId = NormalizeRootExecutionId(executionKey);
            var parentTitle = incoming?.FromNode?.Title;
            var parentNodeId = incoming?.FromNode?.Id;
            var depth = 0;
            lock (_executionTraceLock)
            {
                if (!_executionTraceDepthByRun.TryGetValue(rootExecutionId, out var byNode))
                {
                    byNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    _executionTraceDepthByRun[rootExecutionId] = byNode;
                }

                // Node chạy trong body AsyncTask: depth = depth(AsyncTask) + 1 (không cùng cấp với AsyncTask).
                if (TryGetAsyncTaskOwningBodyNode(incoming?.FromNode, out var ownerAsyncForDepth))
                {
                    if (byNode.TryGetValue(ownerAsyncForDepth!.Id, out var asyncDepth))
                        depth = asyncDepth + 1;
                    else if (!string.IsNullOrWhiteSpace(parentNodeId) && byNode.TryGetValue(parentNodeId, out var parentDepth))
                        depth = parentDepth + 1;
                    else if (incoming?.FromNode != null)
                        depth = 1;
                }
                else if (!string.IsNullOrWhiteSpace(parentNodeId) && byNode.TryGetValue(parentNodeId, out var parentDepth))
                    depth = parentDepth + 1;
                else if (incoming?.FromNode != null)
                    depth = 1;

                byNode[node.Id] = depth;
            }

            var inputSummary = BuildInputSummaryForExecution(node, incoming, executionKey);
            var fullInputSummary = BuildFullInputSummaryForExecution(node, incoming, executionKey);
            var item = new ExecutionTraceLogItemViewModel(
                node: node,
                executionId: executionKey,
                rootExecutionId: rootExecutionId,
                iconKey: ResolveNodeIconKey(node),
                nodeColorKey: string.IsNullOrWhiteSpace(node.ColorKey) ? "SecondaryBrush" : node.ColorKey!,
                parentNodeTitle: parentTitle,
                parentNodeId: parentNodeId,
                depth: depth)
            {
                InputSummary = inputSummary,
                Status = "running"
            };

            var treeNode = new ExecutionTraceTreeNodeViewModel(
                rootExecutionId: rootExecutionId,
                executionId: executionKey,
                nodeId: node.Id,
                nodeTitle: item.NodeTitle,
                nodeType: node.Type.ToString(),
                iconKey: ResolveNodeIconKey(node),
                parentNodeId: parentNodeId ?? string.Empty,
                isRunRoot: false,
                nodeBrush: ResolveTraceNodeBrush(node),
                depth: depth,
                nodeColorKey: string.IsNullOrWhiteSpace(node.ColorKey) ? null : node.ColorKey)
            {
                InputSummary = item.InputSummary,
                FullInputSummary = fullInputSummary,
                Status = "running"
            };

            Application.Current?.Dispatcher?.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!EnableExecutionTraceLog) return;
                lock (_executionTraceLock)
                {
                    if (_pendingTraceRowsByNode.TryGetValue(node, out var pendingExisting) &&
                        pendingExisting.Any(x =>
                            string.Equals(x.ExecutionId, executionKey, StringComparison.Ordinal) &&
                            string.Equals(x.Status, "running", StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    if (!_executionTraceTreeRootByRun.TryGetValue(rootExecutionId, out var rootNode))
                    {
                        rootNode = new ExecutionTraceTreeNodeViewModel(
                            rootExecutionId: rootExecutionId,
                            executionId: rootExecutionId,
                            nodeId: rootExecutionId,
                            nodeTitle: $"Run {rootExecutionId}",
                            nodeType: "Run",
                            iconKey: "timeline-arrow duotone-light",
                            parentNodeId: string.Empty,
                            isRunRoot: true,
                            nodeBrush: null,
                            depth: 0);
                        _executionTraceTreeRootByRun[rootExecutionId] = rootNode;
                        ExecutionTraceRunRoots.Add(rootNode);
                    }

                    ExecutionTraceTreeNodeViewModel parentTreeNode = rootNode;
                    if (TryGetAsyncTaskOwningBodyNode(incoming?.FromNode, out var ownerAsync))
                    {
                        var asyncKey = BuildTreeRunNodeKey(rootExecutionId, ownerAsync!.Id);
                        if (_executionTraceTreeNodesByRunAndNode.TryGetValue(asyncKey, out var asyncCandidates) &&
                            asyncCandidates.Count > 0)
                            parentTreeNode = PickBestParentTreeNode(asyncCandidates, executionKey) ?? parentTreeNode;
                        else
                            parentTreeNode = PickNearestAsyncTaskInExecutionChain(executionKey, rootExecutionId) ?? parentTreeNode;
                    }
                    else
                    {
                        var parentResolved = false;
                        if (!string.IsNullOrWhiteSpace(parentNodeId))
                        {
                            var parentKey = BuildTreeRunNodeKey(rootExecutionId, parentNodeId);
                            if (_executionTraceTreeNodesByRunAndNode.TryGetValue(parentKey, out var parentCandidates) &&
                                parentCandidates.Count > 0)
                            {
                                parentTreeNode = PickBestParentTreeNode(parentCandidates, executionKey) ?? parentTreeNode;
                                parentResolved = true;
                            }
                        }
                        if (!parentResolved)
                        {
                            parentTreeNode = PickBestParentFromExecutionChain(executionKey, rootExecutionId) ?? parentTreeNode;
                        }
                        if (IsAsyncTaskBodyHostNode(incoming?.FromNode))
                        {
                            parentTreeNode = PickNearestAsyncTaskInExecutionChain(executionKey, rootExecutionId) ?? parentTreeNode;
                        }
                    }

                    ExecutionTraceTreeNodeViewModel? endAfterSibling = null;
                    if (node.Type == NodeType.End)
                    {
                        endAfterSibling = PickLatestNodeInExactExecution(executionKey, rootExecutionId, node.Id);
                        if (endAfterSibling?.Parent != null)
                            parentTreeNode = endAfterSibling.Parent;
                    }

                    int? insertIndex = null;
                    if (node.Type == NodeType.End && endAfterSibling?.Parent != null &&
                        ReferenceEquals(endAfterSibling.Parent, parentTreeNode))
                    {
                        var afterIdx = parentTreeNode.Children.IndexOf(endAfterSibling);
                        if (afterIdx >= 0)
                            insertIndex = afterIdx + 1;
                    }

                    var visualDepth = parentTreeNode.IsRunRoot ? 1 : parentTreeNode.Depth + 1;
                    treeNode.ApplyTraceVisualDepth(visualDepth);
                    if (_executionTraceDepthByRun.TryGetValue(rootExecutionId, out var depthByNodeForVisual))
                        depthByNodeForVisual[node.Id] = visualDepth;

                    // Nếu incoming.FromNode null (AsyncTask dispatch, scheduler resume...), dùng chính
                    // parentTreeNode đã resolve để card trace luôn có "from node" thay vì hiện trống.
                    if (string.IsNullOrWhiteSpace(treeNode.ParentNodeId) && !parentTreeNode.IsRunRoot)
                        treeNode.ParentNodeId = parentTreeNode.NodeId;

                    AttachTreeChild(parentTreeNode, treeNode, insertIndex);
                    var nodeKey = BuildTreeRunNodeKey(rootExecutionId, node.Id);
                    if (!_executionTraceTreeNodesByRunAndNode.TryGetValue(nodeKey, out var list))
                    {
                        list = new List<ExecutionTraceTreeNodeViewModel>();
                        _executionTraceTreeNodesByRunAndNode[nodeKey] = list;
                    }
                    list.Add(treeNode);

                    if (!_executionTraceTreeNodesByExecutionId.TryGetValue(executionKey, out var byExecList))
                    {
                        byExecList = new List<ExecutionTraceTreeNodeViewModel>();
                        _executionTraceTreeNodesByExecutionId[executionKey] = byExecList;
                    }
                    byExecList.Add(treeNode);

                    if (!_pendingTraceRowsByNode.TryGetValue(node, out var pendingRows))
                    {
                        pendingRows = new List<ExecutionTraceLogItemViewModel>();
                        _pendingTraceRowsByNode[node] = pendingRows;
                    }
                    pendingRows.Add(item);

                    if (!_pendingTraceTreeRowsByNode.TryGetValue(node, out var pendingTreeRows))
                    {
                        pendingTreeRows = new List<ExecutionTraceTreeNodeViewModel>();
                        _pendingTraceTreeRowsByNode[node] = pendingTreeRows;
                    }
                    pendingTreeRows.Add(treeNode);

                    ExecutionTraceLogs.Add(item);
                    if (!ExecutionTraceNodeTypeOptions.Contains(item.NodeType))
                        ExecutionTraceNodeTypeOptions.Add(item.NodeType);
                    while (ExecutionTraceLogs.Count > MaxExecutionTraceRows)
                        ExecutionTraceLogs.RemoveAt(0);
                }
                RefreshExecutionTraceFilter();
            }));
        }

        private void TraceNodeCompleted(WorkflowNode node, TimeSpan elapsed)
        {
            if (!EnableExecutionTraceLog || node == null) return;
            var ambient = FlowMy.Services.Workflow.WorkflowExecutionContext.CurrentExecutionId;
            var executionKey = !string.IsNullOrWhiteSpace(ambient)
                ? ambient!
                : (node.LastExecutionId ?? string.Empty);
            var elapsedText = FormatElapsed(elapsed);
            // Snapshot on executor thread: UI callback may run after ClearScopedOutputsForRun (finally),
            // lúc đó TryGetScopedNodeStringOutputForLookupChain không còn dữ liệu → OUT trống.
            var outputSummarySnapshot = BuildOutputSummaryForExecution(node, executionKey);
            if (string.IsNullOrWhiteSpace(outputSummarySnapshot))
                outputSummarySnapshot = BuildOutputSummary(node);
            var fullOutputSummarySnapshot = BuildFullOutputSummaryForExecution(node, executionKey);
            if (string.IsNullOrWhiteSpace(fullOutputSummarySnapshot))
                fullOutputSummarySnapshot = outputSummarySnapshot;

            Application.Current?.Dispatcher?.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!EnableExecutionTraceLog) return;
                lock (_executionTraceLock)
                {
                    var row = TakePendingRow(
                        _pendingTraceRowsByNode,
                        node,
                        executionKey,
                        x => x.ExecutionId,
                        x => x.Status);
                    if (row != null)
                    {
                        row.Status = "completed";
                        row.ElapsedText = elapsedText;
                        row.OutputSummary = string.IsNullOrWhiteSpace(outputSummarySnapshot)
                            ? BuildOutputSummaryForExecution(node, row.ExecutionId)
                            : outputSummarySnapshot;
                    }

                    var trow = TakePendingRow(
                        _pendingTraceTreeRowsByNode,
                        node,
                        row?.ExecutionId ?? executionKey,
                        x => x.ExecutionId,
                        x => x.Status);
                    if (trow != null)
                    {
                        trow.Status = "completed";
                        trow.ElapsedText = elapsedText;
                        trow.OutputSummary = string.IsNullOrWhiteSpace(outputSummarySnapshot)
                            ? BuildOutputSummaryForExecution(node, trow.ExecutionId)
                            : outputSummarySnapshot;
                        trow.FullOutputSummary = string.IsNullOrWhiteSpace(fullOutputSummarySnapshot)
                            ? BuildFullOutputSummaryForExecution(node, trow.ExecutionId)
                            : fullOutputSummarySnapshot;
                    }
                }
            }));
        }

        /// <summary>Cập nhật card gốc "Run …" (không gắn với WorkflowNode nên không qua TraceNodeCompleted).</summary>
        private void TraceRunRootSetStatus(string executionId, string status, string? elapsedText = null)
        {
            if (!EnableExecutionTraceLog || string.IsNullOrWhiteSpace(executionId)) return;
            var rootKey = NormalizeRootExecutionId(executionId);
            Application.Current?.Dispatcher?.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!EnableExecutionTraceLog) return;
                lock (_executionTraceLock)
                {
                    if (!_executionTraceTreeRootByRun.TryGetValue(rootKey, out var rootNode) || !rootNode.IsRunRoot)
                        return;
                    rootNode.Status = status;
                    if (elapsedText != null)
                        rootNode.ElapsedText = elapsedText;
                }
            }));
        }

        private void TraceNodeFailed(WorkflowNode node, string errorMessage)
        {
            if (!EnableExecutionTraceLog || node == null) return;
            var ambient = FlowMy.Services.Workflow.WorkflowExecutionContext.CurrentExecutionId;
            var executionKey = !string.IsNullOrWhiteSpace(ambient)
                ? ambient!
                : (node.LastExecutionId ?? string.Empty);
            var effectiveExecutionId = !string.IsNullOrWhiteSpace(ambient)
                ? ambient!
                : (string.IsNullOrWhiteSpace(node.LastExecutionId) ? "failed" : node.LastExecutionId!);
            var err = ToCompactText(errorMessage, 400);
            var fullErr = errorMessage ?? string.Empty;
            Application.Current?.Dispatcher?.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!EnableExecutionTraceLog) return;
                lock (_executionTraceLock)
                {
                    var row = TakePendingRow(
                        _pendingTraceRowsByNode,
                        node,
                        executionKey,
                        x => x.ExecutionId,
                        x => x.Status);
                    if (row == null)
                    {
                        row = new ExecutionTraceLogItemViewModel(
                            node,
                            effectiveExecutionId,
                            NormalizeRootExecutionId(effectiveExecutionId),
                            ResolveNodeIconKey(node),
                            string.IsNullOrWhiteSpace(node.ColorKey) ? "SecondaryBrush" : node.ColorKey!,
                            null,
                            null,
                            0);
                        ExecutionTraceLogs.Add(row);
                    }
                    row.Status = "failed";
                    row.ErrorMessage = err;

                    var trow = TakePendingRow(
                        _pendingTraceTreeRowsByNode,
                        node,
                        effectiveExecutionId,
                        x => x.ExecutionId,
                        x => x.Status);
                    if (trow != null)
                    {
                        trow.Status = "failed";
                        trow.ErrorMessage = err;
                        trow.FullErrorMessage = fullErr;
                    }
                }
            }));
        }

        /// <summary>
        /// Yêu cầu render lại toàn bộ path connections (dùng khi thay đổi cấu hình ReuseRoutes / line style per-node).
        /// </summary>
        public void RequestRefreshAllConnectionPaths()
        {
            try
            {
                // Gọi sang host (UI) qua event service/host accessor là phức tạp;
                // hiện tại ViewModel không trực tiếp giữ reference tới ConnectionRenderer,
                // nên method này chỉ là hook để UI có thể override/extend nếu cần.
                // WorkflowEditorWindow sẽ gọi UpdateAllConnectionPaths() sau khi load workflow.
            }
            catch { }
        }

        /// <summary>Hủy một phiên chạy thủ công theo id (nút Dừng trên từng dòng).</summary>
        public void CancelManualRunSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            CancellationTokenSource? ctsToCancel;
            lock (_manualRunCtsLock)
                _manualRunCtsBySession.TryGetValue(sessionId, out ctsToCancel);

            if (ctsToCancel != null)
            {
                // Cancel không chạy trên UI thread — callback đăng ký với token có thể đồng bộ nặng.
                _ = Task.Run(() =>
                {
                    try { ctsToCancel.Cancel(throwOnFirstException: false); }
                    catch (ObjectDisposedException) { }
                });
            }

            var disp = Application.Current?.Dispatcher;
            if (disp != null)
            {
                disp.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => _executionVisualizer.CancelTimersForManualRunSession(sessionId)));
            }
        }

        /// <summary>Luôn queue lên Dispatcher — không chạy đồng bộ trên UI (tránh chặn animation khi workflow báo từ thread nền hoặc async trên UI).</summary>
        private static void PostToUi(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            var d = Application.Current?.Dispatcher;
            if (d == null) { action(); return; }
            d.BeginInvoke(priority, action);
        }

        private void NotifyManualRunsInFlightChanged()
        {
            var d = Application.Current?.Dispatcher;
            if (d == null || d.CheckAccess())
            {
                OnPropertyChanged(nameof(ManualExecutionRunsInFlight));
                return;
            }

            // Ưu tiên cập nhật ngay để badge/toolbar không bị "trễ số" sau khi run đã kết thúc.
            d.Invoke(() => OnPropertyChanged(nameof(ManualExecutionRunsInFlight)), DispatcherPriority.Send);
        }

        private void FinalizeManualRunUiState(int remaining, bool operationCancelled)
        {
            void Apply()
            {
                IsExecuting = remaining > 0;
                if (remaining == 0)
                {
                    ActiveExecutionConnection = null;

                    if (!AnyAutoScheduledLaneInFlight())
                    {
                        lock (_runningNodesBookkeepingLock)
                            _nodeRunningRefCount.Clear();
                        RunningNodes.Clear();
                        HasRunningNodes = false;

                        if (operationCancelled)
                            _executionVisualizer.OnExecutionCancelled();
                    }
                }
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                Apply();
            }
            else
            {
                // Chốt trạng thái flow ngay khi runtime code đã kết thúc.
                dispatcher.Invoke(Apply, DispatcherPriority.Send);
            }
        }

        private void FinalizeAllExecutionUiStateIfIdle()
        {
            var manualInFlight = Volatile.Read(ref _manualExecutionRunsInFlight);
            var autoInFlight = Volatile.Read(ref _autoScheduledLaneRunsInFlight);
            if (manualInFlight != 0 || autoInFlight != 0) return;

            void Apply()
            {
                IsExecuting = false;
                ActiveExecutionConnection = null;
                lock (_runningNodesBookkeepingLock)
                    _nodeRunningRefCount.Clear();
                RunningNodes.Clear();
                HasRunningNodes = false;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                Apply();
            }
            else
            {
                // Ép chốt ngay trạng thái "đã dừng" khi không còn lane nào chạy.
                dispatcher.Invoke(Apply, DispatcherPriority.Send);
            }
        }

        private void RegisterRunningNodeVisual(WorkflowNode node)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            void Apply()
            {
                lock (_runningNodesBookkeepingLock)
                {
                    _nodeRunningRefCount.TryGetValue(node, out var c);
                    c++;
                    _nodeRunningRefCount[node] = c;
                    if (c == 1 && !RunningNodes.Contains(node))
                        RunningNodes.Add(node);
                    HasRunningNodes = RunningNodes.Count > 0;
                }
            }

            if (dispatcher.CheckAccess())
                Apply();
            else
                dispatcher.Invoke(Apply, DispatcherPriority.Send);
        }

        /// <summary>Gỡ ref-count trên UI thread (luôn qua Background) để finally của async không chặn UI khi hủy nhiều node.</summary>
        private void ReleaseRunningNodeVisualBatch(IReadOnlyList<WorkflowNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            var batch = nodes.ToList();
            void Apply()
            {
                lock (_runningNodesBookkeepingLock)
                {
                    foreach (var node in batch)
                    {
                        if (!_nodeRunningRefCount.TryGetValue(node, out var c))
                            continue;
                        c--;
                        if (c <= 0)
                        {
                            _nodeRunningRefCount.Remove(node);
                            RunningNodes.Remove(node);
                        }
                        else
                        {
                            _nodeRunningRefCount[node] = c;
                        }
                    }

                    HasRunningNodes = RunningNodes.Count > 0;
                }
            }

            if (dispatcher.CheckAccess())
                Apply();
            else
                dispatcher.Invoke(Apply, DispatcherPriority.Send);
        }

        private void ReleaseRunningNodeVisual(WorkflowNode node)
        {
            ReleaseRunningNodeVisualBatch(new[] { node });
        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task StartTest()
        {
            var validation = _workflowExecutionService.ValidateWorkflow(Nodes, Connections);
            if (!validation.IsValid)
            {
                MessageBox.Show(
                    string.Join("\n", validation.Errors),
                    "Workflow không hợp lệ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var startNodes = _workflowExecutionService.FindStartNodes(Nodes);
            if (startNodes.Count == 0) return;

            var sessionId = Guid.NewGuid().ToString("N");
            var sessionRow = new ManualWorkflowRunSessionViewModel(sessionId, this);
            ManualRunSessions.Insert(0, sessionRow);
            HasManualRunSessions = true;

            using var sessionCts = new CancellationTokenSource();
            lock (_manualRunCtsLock)
                _manualRunCtsBySession[sessionId] = sessionCts;

            var n = Interlocked.Increment(ref _manualExecutionRunsInFlight);
            NotifyManualRunsInFlightChanged();
            if (n == 1)
            {
                ActiveExecutionConnection = null;
                _executionVisualizer.ResetVisualization(Nodes);
            }

            IsExecuting = true;
            var operationCancelled = false;
            var pendingNodesLock = new object();
            var pendingNodesThisSession = new HashSet<WorkflowNode>();

            try
            {
                var sessionToken = sessionCts.Token;
                void NotifyEnteringNode(WorkflowConnection? incoming)
                {
                    PostToUi(() =>
                    {
                        if (sessionToken.IsCancellationRequested)
                            return;
                        ActiveExecutionConnection = incoming;
                    });
                }

                void OnNodeStarted(WorkflowNode node, WorkflowConnection? incoming)
                {
                    if (sessionToken.IsCancellationRequested) return;
                    lock (pendingNodesLock)
                        pendingNodesThisSession.Add(node);
                    NotifyEnteringNode(incoming);
                    TraceNodeStarted(node, incoming, sessionId);
                    _executionVisualizer.OnNodeStarted(node, sessionId);
                    RegisterRunningNodeVisual(node);
                }

                void OnNodeCompleted(WorkflowNode node, TimeSpan elapsed)
                {
                    lock (pendingNodesLock)
                        pendingNodesThisSession.Remove(node);
                    if (sessionToken.IsCancellationRequested) return;
                    TraceNodeCompleted(node, elapsed);
                    _executionVisualizer.OnNodeCompleted(node, elapsed, sessionId);

                    FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.NotifyNodeCompleted(node);

                    ReleaseRunningNodeVisual(node);
                }

                void OnNodeFailed(WorkflowNode node, string errorMessage)
                {
                    TraceNodeFailed(node, errorMessage);
                    _executionVisualizer.OnNodeFailed(node, errorMessage);
                }

                await Task.Run(
                    async () =>
                    {
                        foreach (var start in startNodes)
                        {
                            if (sessionToken.IsCancellationRequested)
                                break;
                            var runId = Guid.NewGuid().ToString("N");
                            try
                            {
                                await _workflowExecutionService.ExecuteNodeAsync(
                                    start,
                                    Connections,
                                    sessionCts.Token,
                                    onEnteringNode: NotifyEnteringNode,
                                    onNodeStarted: OnNodeStarted,
                                    onNodeCompleted: OnNodeCompleted,
                                    onNodeFailed: OnNodeFailed,
                                    incomingConnection: null,
                                    reachableToEnd: null,
                                    executionId: runId).ConfigureAwait(false);
                                TraceRunRootSetStatus(runId, "completed");
                            }
                            finally
                            {
                                _workflowExecutionService.ClearScopedOutputsForRun(runId);
                            }
                        }
                    });
            }
            catch (OperationCanceledException)
            {
                operationCancelled = true;
            }
            catch (Exception)
            {
                // Lỗi đã hiển thị trên node (toggle "Có lỗi"), không cần MessageBox nữa
            }
            finally
            {
                List<WorkflowNode> orphanNodes;
                lock (pendingNodesLock)
                {
                    orphanNodes = pendingNodesThisSession.ToList();
                    pendingNodesThisSession.Clear();
                }

                ReleaseRunningNodeVisualBatch(orphanNodes);

                lock (_manualRunCtsLock)
                    _manualRunCtsBySession.Remove(sessionId);

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    ManualRunSessions.Remove(sessionRow);
                    HasManualRunSessions = ManualRunSessions.Count > 0;
                }), DispatcherPriority.Send);

                var remaining = Interlocked.Decrement(ref _manualExecutionRunsInFlight);
                NotifyManualRunsInFlightChanged();
                FinalizeManualRunUiState(remaining, operationCancelled);
            }
        }

        /// <summary>
        /// Chạy workflow bắt đầu từ một node cụ thể (thay vì từ Start node).
        /// Logic traversal, visualize, cancel... giữ nguyên như StartTest, chỉ khác điểm xuất phát.
        /// </summary>
        public async Task RunWorkflowFromNodeAsync(WorkflowNode startNode)
        {
            if (startNode == null) return;

            var validation = _workflowExecutionService.ValidateWorkflow(Nodes, Connections);
            if (!validation.IsValid)
            {
                MessageBox.Show(
                    string.Join("\n", validation.Errors),
                    "Workflow không hợp lệ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var sessionId = Guid.NewGuid().ToString("N");
            var sessionRow = new ManualWorkflowRunSessionViewModel(sessionId, this);
            ManualRunSessions.Insert(0, sessionRow);
            HasManualRunSessions = true;

            using var sessionCts = new CancellationTokenSource();
            lock (_manualRunCtsLock)
                _manualRunCtsBySession[sessionId] = sessionCts;

            var n = Interlocked.Increment(ref _manualExecutionRunsInFlight);
            NotifyManualRunsInFlightChanged();
            if (n == 1)
            {
                ActiveExecutionConnection = null;
                _executionVisualizer.ResetVisualization(Nodes);
            }

            IsExecuting = true;
            var operationCancelled = false;
            var pendingNodesLockFromNode = new object();
            var pendingNodesFromNode = new HashSet<WorkflowNode>();

            try
            {
                var sessionTokenFromNode = sessionCts.Token;
                void NotifyEnteringNode(WorkflowConnection? incoming)
                {
                    PostToUi(() =>
                    {
                        if (sessionTokenFromNode.IsCancellationRequested)
                            return;
                        ActiveExecutionConnection = incoming;
                    });
                }

                void OnNodeStarted(WorkflowNode node, WorkflowConnection? incoming)
                {
                    if (sessionTokenFromNode.IsCancellationRequested) return;
                    lock (pendingNodesLockFromNode)
                        pendingNodesFromNode.Add(node);
                    NotifyEnteringNode(incoming);
                    TraceNodeStarted(node, incoming, sessionId);
                    _executionVisualizer.OnNodeStarted(node, sessionId);
                    RegisterRunningNodeVisual(node);
                }

                void OnNodeCompleted(WorkflowNode node, TimeSpan elapsed)
                {
                    lock (pendingNodesLockFromNode)
                        pendingNodesFromNode.Remove(node);
                    if (sessionTokenFromNode.IsCancellationRequested) return;
                    TraceNodeCompleted(node, elapsed);
                    _executionVisualizer.OnNodeCompleted(node, elapsed, sessionId);

                    FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.NotifyNodeCompleted(node);

                    ReleaseRunningNodeVisual(node);
                }

                void OnNodeFailed(WorkflowNode node, string errorMessage)
                {
                    TraceNodeFailed(node, errorMessage);
                    _executionVisualizer.OnNodeFailed(node, errorMessage);
                }

                var runId = Guid.NewGuid().ToString("N");
                await Task.Run(
                    async () =>
                    {
                        try
                        {
                            await _workflowExecutionService.ExecuteNodeAsync(
                                startNode,
                                Connections,
                                sessionCts.Token,
                                onEnteringNode: NotifyEnteringNode,
                                onNodeStarted: OnNodeStarted,
                                onNodeCompleted: OnNodeCompleted,
                                onNodeFailed: OnNodeFailed,
                                incomingConnection: null,
                                reachableToEnd: null,
                                executionId: runId).ConfigureAwait(false);
                            TraceRunRootSetStatus(runId, "completed");
                        }
                        finally
                        {
                            _workflowExecutionService.ClearScopedOutputsForRun(runId);
                        }
                    });
            }
            catch (OperationCanceledException)
            {
                operationCancelled = true;
            }
            catch (Exception)
            {
                // Lỗi đã hiển thị trên node, không cần MessageBox nữa
            }
            finally
            {
                List<WorkflowNode> orphanFromNode;
                lock (pendingNodesLockFromNode)
                {
                    orphanFromNode = pendingNodesFromNode.ToList();
                    pendingNodesFromNode.Clear();
                }

                ReleaseRunningNodeVisualBatch(orphanFromNode);

                lock (_manualRunCtsLock)
                    _manualRunCtsBySession.Remove(sessionId);

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    ManualRunSessions.Remove(sessionRow);
                    HasManualRunSessions = ManualRunSessions.Count > 0;
                }), DispatcherPriority.Send);

                var remaining = Interlocked.Decrement(ref _manualExecutionRunsInFlight);
                NotifyManualRunsInFlightChanged();
                FinalizeManualRunUiState(remaining, operationCancelled);
            }
        }

        /// <summary>
        /// Lane riêng cho Start AutoScheduled: chạy song song với manual (bỏ qua IsExecuting), CTS riêng, không ResetVisualization.
        /// </summary>
        public async Task RunAutoScheduledLaneAsync(WorkflowNode startNode)
        {
            if (startNode == null || startNode.Type != NodeType.Start || startNode.RunMode != FlowRunMode.AutoScheduled)
                return;

            var validation = _workflowExecutionService.ValidateWorkflow(Nodes, Connections);
            if (!validation.IsValid)
                return;

            Interlocked.Increment(ref _autoScheduledLaneRunsInFlight);
            using var laneCts = new CancellationTokenSource();
            var pendingAutoLock = new object();
            var pendingAutoNodes = new HashSet<WorkflowNode>();
            try
            {
                var runId = Guid.NewGuid().ToString("N");
                var laneToken = laneCts.Token;

                void NotifyEnteringNode(WorkflowConnection? incoming)
                {
                    PostToUi(() =>
                    {
                        if (laneToken.IsCancellationRequested)
                            return;
                        ActiveExecutionConnection = incoming;
                    });
                }

                void OnNodeStarted(WorkflowNode node, WorkflowConnection? incoming)
                {
                    if (laneToken.IsCancellationRequested) return;
                    lock (pendingAutoLock)
                        pendingAutoNodes.Add(node);
                    NotifyEnteringNode(incoming);
                    TraceNodeStarted(node, incoming, runId);
                    _executionVisualizer.OnNodeStarted(node, runId);
                    RegisterRunningNodeVisual(node);
                }

                void OnNodeCompleted(WorkflowNode node, TimeSpan elapsed)
                {
                    lock (pendingAutoLock)
                        pendingAutoNodes.Remove(node);
                    if (laneToken.IsCancellationRequested) return;
                    TraceNodeCompleted(node, elapsed);
                    _executionVisualizer.OnNodeCompleted(node, elapsed, runId);

                    FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.NotifyNodeCompleted(node);

                    ReleaseRunningNodeVisual(node);
                }

                void OnNodeFailed(WorkflowNode node, string errorMessage)
                {
                    TraceNodeFailed(node, errorMessage);
                    _executionVisualizer.OnNodeFailed(node, errorMessage);
                }

                await Task.Run(
                    async () =>
                    {
                        try
                        {
                            await _workflowExecutionService.ExecuteNodeAsync(
                                startNode,
                                Connections,
                                laneCts.Token,
                                onEnteringNode: NotifyEnteringNode,
                                onNodeStarted: OnNodeStarted,
                                onNodeCompleted: OnNodeCompleted,
                                onNodeFailed: OnNodeFailed,
                                incomingConnection: null,
                                reachableToEnd: null,
                                executionId: runId).ConfigureAwait(false);
                            TraceRunRootSetStatus(runId, "completed");
                        }
                        finally
                        {
                            _workflowExecutionService.ClearScopedOutputsForRun(runId);
                        }
                    });
            }
            catch (OperationCanceledException)
            {
                // chỉ từ laneCts
            }
            catch (Exception)
            {
                // Lỗi đã hiển thị trên node
            }
            finally
            {
                List<WorkflowNode> orphanAuto;
                lock (pendingAutoLock)
                {
                    orphanAuto = pendingAutoNodes.ToList();
                    pendingAutoNodes.Clear();
                }

                ReleaseRunningNodeVisualBatch(orphanAuto);

                Interlocked.Decrement(ref _autoScheduledLaneRunsInFlight);
                FinalizeAllExecutionUiStateIfIdle();
            }
        }

        private bool AnyAutoScheduledLaneInFlight() => Volatile.Read(ref _autoScheduledLaneRunsInFlight) != 0;

        public IReadOnlyList<WorkflowNode> GetAutoScheduledStartNodes()
        {
            return Nodes
                .Where(n => n.Type == NodeType.Start && n.RunMode == FlowRunMode.AutoScheduled)
                .ToList();
        }

        public double GetAutoRunIntervalMilliseconds(WorkflowNode startNode)
        {
            if (startNode == null) return 1000d;
            var value = startNode.AutoRunIntervalValue <= 0 ? 1d : startNode.AutoRunIntervalValue;
            var factor = startNode.AutoRunIntervalUnit switch
            {
                AutoRunIntervalUnit.Milliseconds => 1d,
                AutoRunIntervalUnit.Seconds => 1000d,
                AutoRunIntervalUnit.Minutes => 60_000d,
                _ => 1000d
            };
            return Math.Max(100d, value * factor);
        }

        [RelayCommand]
        private void EndTest()
        {
            List<CancellationTokenSource> snapshot;
            lock (_manualRunCtsLock)
                snapshot = new List<CancellationTokenSource>(_manualRunCtsBySession.Values);

            // Stop visual effects immediately even when cancellation callbacks are still draining.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    _executionVisualizer.OnExecutionCancelled();
                    lock (_runningNodesBookkeepingLock)
                        _nodeRunningRefCount.Clear();
                    RunningNodes.Clear();
                    HasRunningNodes = false;
                    ActiveExecutionConnection = null;
                }));
            }

            if (snapshot.Count == 0) return;
            _ = Task.Run(() =>
            {
                foreach (var cts in snapshot)
                {
                    try { cts.Cancel(throwOnFirstException: false); }
                    catch (ObjectDisposedException) { }
                }
            });
        }

        // Timing & execution result visualization đã được tách sang IWorkflowExecutionVisualizer

        [RelayCommand]
        private void ResetWorkflow()
        {
            Nodes.Clear();
            Connections.Clear();
            _nodeCounter = 1;
            InitializeSampleNodes();
            CurrentWorkflowName = "Untitled Workflow";
            
            // Reset view state to defaults
            ZoomLevel = 1.0;
            PanX = 0.0;
            PanY = 0.0;

            // Reset connection line style về mặc định
            ConnectionLineStyle = ConnectionLineStyle.Bezier;
        }

        [RelayCommand]
        public void SaveWorkflow()
        {
            SaveWorkflowInternal(promptForName: true);
        }

        /// <summary>
        /// Dùng cho các luồng không cần hỏi lại tên (ví dụ sau khi import)
        /// </summary>
        public void SaveWorkflowSilently()
        {
            SaveWorkflowInternal(promptForName: false);
        }

        /// <summary>
        /// Ctrl+S: lưu đè nếu workflow đã có trong danh sách, không thì hỏi tên. Trả về true nếu đã ghi file.
        /// </summary>
        public bool TrySaveFromEditorShortcut()
        {
            if (!string.IsNullOrWhiteSpace(CurrentWorkflowName) && SavedWorkflows.Contains(CurrentWorkflowName))
                return SaveWorkflowInternal(promptForName: false);
            return SaveWorkflowInternal(promptForName: true);
        }

        private bool TryPrepareWorkflowNameForSave(bool promptForName)
        {
            if (promptForName)
            {
                var inputName = PromptWorkflowName();
                if (string.IsNullOrWhiteSpace(inputName))
                    return false;

                _isRefreshingAfterSave = true;
                try { CurrentWorkflowName = inputName; }
                finally { _isRefreshingAfterSave = false; }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(CurrentWorkflowName))
                {
                    _isRefreshingAfterSave = true;
                    try { CurrentWorkflowName = $"Workflow_{DateTime.Now:yyyyMMdd_HHmmss}"; }
                    finally { _isRefreshingAfterSave = false; }
                }
            }

            return true;
        }

        private void RefreshUiAfterSave()
        {
            _isRefreshingAfterSave = true;
            try
            {
                RefreshSavedWorkflows();
            }
            finally
            {
                _isRefreshingAfterSave = false;
            }

            var savedName = CurrentWorkflowName;
            if (!string.IsNullOrWhiteSpace(savedName))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isRefreshingAfterSave = true;
                    try
                    {
                        OnPropertyChanged(nameof(SavedWorkflows));
                        if (SavedWorkflows.Contains(savedName))
                            CurrentWorkflowName = savedName;
                        else
                        {
                            var match = SavedWorkflows.FirstOrDefault(n =>
                                string.Equals(n, savedName, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                                CurrentWorkflowName = match;
                        }
                    }
                    finally
                    {
                        _isRefreshingAfterSave = false;
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private bool SaveWorkflowInternal(bool promptForName)
        {
            try
            {
                if (!TryPrepareWorkflowNameForSave(promptForName))
                    return false;

                SyncViewStateBeforeSave?.Invoke();

                _persistenceService.Save(
                    CurrentWorkflowName,
                    Nodes,
                    Connections,
                    ZoomLevel,
                    PanX,
                    PanY,
                    SavedScreenWidth,
                    SavedScreenHeight,
                    SavedViewportCenterX,
                    SavedViewportCenterY,
                    ConnectionLineStyle.ToString());

                RefreshUiAfterSave();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving workflow: {ex.Message}");
                return false;
            }
        }

        private string? PromptWorkflowName()
        {
            var defaultName = string.IsNullOrWhiteSpace(CurrentWorkflowName)
                ? $"Workflow_{DateTime.Now:yyyyMMdd_HHmmss}"
                : CurrentWorkflowName;

            var input = Interaction.InputBox("Nhập tên workflow để lưu", "Save workflow", defaultName).Trim();
            if (string.IsNullOrWhiteSpace(input))
                return null;

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(input) ? defaultName : input;
        }

        /// <param name="portableWebBundleFileName">Tên file .webpkg.zip cùng thư mục JSON (chỉ tên file), hoặc null khi export chỉ logic.</param>
        public string ExportToJson(string? portableWebBundleFileName = null)
        {
            return _persistenceService.ExportToJson(
                CurrentWorkflowName,
                Nodes,
                Connections,
                ZoomLevel,
                PanX,
                PanY,
                SavedScreenWidth,
                SavedScreenHeight,
                SavedViewportCenterX,
                SavedViewportCenterY,
                ConnectionLineStyle.ToString(),
                portableWebBundleFileName);
        }

        /// <summary>
        /// Chạy chỉ logic của một node (cập nhật output), không chạy các node tiếp theo.
        /// Dùng khi nhấn nút Play trong dialog của node đó.
        /// </summary>
        public async Task RunSingleNodeAsync(WorkflowNode node)
        {
            if (node == null) return;
            var connections = Connections?.ToList() ?? new List<WorkflowConnection>();
            var allNodes = Nodes?.ToList() ?? new List<WorkflowNode>();
            try
            {
                await _workflowExecutionService.ExecuteNodeLogicOnlyAsync(node, connections, CancellationToken.None, allNodesForLookup: allNodes);
                Application.Current?.Dispatcher.Invoke(() => _executionVisualizer.RefreshSavedOutputs(new[] { node }));

                // Notify DataFetcher Realtime: khi bấm ▶ PlayButton trên bất kỳ node nào,
                // các DataFetcherNode đang lắng nghe node đó sẽ tự động lấy dữ liệu mới nhất.
                FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.NotifyNodeCompleted(node);
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Lỗi khi chạy node: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
        }

        private List<WorkflowNode> OrderNodesForExport()
        {
            var ordered = new List<WorkflowNode>();
            var visited = new HashSet<string>();

            var startNodes = Nodes.Where(n => n.Type == NodeType.Start).ToList();
            var endNodes = Nodes.Where(n => n.Type == NodeType.End).ToList();

            void visit(WorkflowNode node)
            {
                if (!visited.Add(node.Id)) return;
                ordered.Add(node);

                var nextNodes = Connections
                    .Where(c => c.FromNode.Id == node.Id)
                    .Select(c => c.ToNode)
                    .Where(n => !visited.Contains(n.Id))
                    .ToList();

                foreach (var nxt in nextNodes)
                {
                    visit(nxt);
                }
            }

            foreach (var s in startNodes)
                visit(s);

            foreach (var e in endNodes)
                visit(e);

            foreach (var node in Nodes)
                visit(node);

            return ordered;
        }

        public void ImportFromJson(string json)
        {
            var result = _persistenceService.ImportFromJson(json);
            if (result != null)
                ApplyWorkflowLoadResult(result);
        }

        /// <summary>
        /// Import JSON + tùy chọn giải nén <see cref="WorkflowLoadResult.PortableWebBundleFileName"/> hoặc thư mục <c>_webcache</c> legacy.
        /// </summary>
        public async Task ImportFromJsonAsync(
            string json,
            string? importJsonFilePath,
            IProgress<WorkflowTransferProgress>? progress,
            CancellationToken cancellationToken)
        {
            var result = _persistenceService.ImportFromJson(json);
            if (result == null) return;

            ApplyWorkflowLoadResult(result);

            var dir = string.IsNullOrWhiteSpace(importJsonFilePath) ? null : Path.GetDirectoryName(importJsonFilePath);
            if (string.IsNullOrEmpty(dir)) return;

            var nodeList = Nodes.ToList();
            var usedZip = false;
            if (!string.IsNullOrWhiteSpace(result.PortableWebBundleFileName))
            {
                var zip = Path.Combine(dir, result.PortableWebBundleFileName);
                if (File.Exists(zip))
                {
                    await PortableWebBundleZipService.ExtractAndRestoreAsync(zip, nodeList, progress, cancellationToken);
                    usedZip = true;
                }
            }

            if (!usedZip && !string.IsNullOrWhiteSpace(importJsonFilePath))
            {
                var baseName = Path.GetFileNameWithoutExtension(importJsonFilePath);
                var portableCache = Path.Combine(dir, baseName + "_webcache");
                if (Directory.Exists(portableCache))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Run(() => WebNodeCacheHelper.RestorePortableWebCaches(portableCache, nodeList), cancellationToken);
                }
            }
        }

        private void ApplyWorkflowLoadResult(WorkflowLoadResult result)
        {
            IsLoading = true;
            try
            {
                CurrentWorkflowName = result.Name;

                ZoomLevel = result.ZoomLevel;
                PanX = result.PanX;
                PanY = result.PanY;
                SavedScreenWidth = result.SavedScreenWidth ?? 0;
                SavedScreenHeight = result.SavedScreenHeight ?? 0;
                SavedViewportCenterX = result.SavedViewportCenterX ?? 0;
                SavedViewportCenterY = result.SavedViewportCenterY ?? 0;
                SavedScreenWidth = result.SavedScreenWidth ?? 0;
                SavedScreenHeight = result.SavedScreenHeight ?? 0;
                SavedViewportCenterX = result.SavedViewportCenterX ?? 0;
                SavedViewportCenterY = result.SavedViewportCenterY ?? 0;

                if (!string.IsNullOrWhiteSpace(result.ConnectionLineStyle) &&
                    Enum.TryParse<ConnectionLineStyle>(result.ConnectionLineStyle, out var restoredStyle))
                    ConnectionLineStyle = restoredStyle;
                else
                    ConnectionLineStyle = ConnectionLineStyle.Bezier;
                // Batch replace để giảm notify/render từng phần tử khi load workflow lớn.
                Nodes = new ObservableCollection<WorkflowNode>(result.Nodes);
                Connections = new ObservableCollection<WorkflowConnection>(result.Connections);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing workflow: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static void EnsureLoopBodyPortsExist(LoopBodyNode bodyNode)
        {
            // Must match the semantic roles used in LoopNodeRenderer / ConnectionHandler
            if (bodyNode.Ports.All(p => p.Id != "LoopBodyTop"))
            {
                bodyNode.Ports.Add(new NodePort
                {
                    Id = "LoopBodyTop",
                    IsInput = true,
                    Position = PortPosition.Top,
                    IsVisible = true,
                    CanDeleteConnection = false
                });
            }

            if (bodyNode.Ports.All(p => p.Id != "LoopBodyLeft"))
            {
                bodyNode.Ports.Add(new NodePort
                {
                    Id = "LoopBodyLeft",
                    IsInput = false,
                    // Inward facing (matches LoopNodeRenderer)
                    Position = PortPosition.Right,
                    IsVisible = true
                });
            }

            if (bodyNode.Ports.All(p => p.Id != "LoopBodyRight"))
            {
                bodyNode.Ports.Add(new NodePort
                {
                    Id = "LoopBodyRight",
                    IsInput = true,
                    // Inward facing (matches LoopNodeRenderer)
                    Position = PortPosition.Left,
                    IsVisible = true
                });
            }
        }

        private static void CopyLoopBodyPortId(LoopBodyNode from, LoopBodyNode to, string portId)
        {
            var src = from.Ports.FirstOrDefault(p => p.Id == portId);
            var dst = to.Ports.FirstOrDefault(p => p.Id == portId);
            if (src == null || dst == null) return;
            dst.Id = src.Id;
        }

        private void RestoreNodeProperties(WorkflowNode node, Dictionary<string, object> properties)
        {
            if (properties == null) return;

            foreach (var prop in properties)
            {
                var value = prop.Value?.ToString();
                if (value == null) continue;

                switch (prop.Key)
                {
                    case "Condition": node.Condition = value; break;
                    case "Key": node.Key = value; break;
                    case "MouseEvent":
                        if (Enum.TryParse<MouseEventType>(value, out var me)) node.MouseEvent = me;
                        break;
                    case "TargetElement": node.TargetElement = value; break;
                    case "RepeatCount":
                        if (int.TryParse(value, out var rc))
                        {
                            if (node is KeyPressEventNode kp) kp.RepeatCount = rc;
                            else if (node is HotkeyPressEventNode hk) hk.RepeatCount = rc;
                        }
                        break;
                }
            }

            if (node is LoopNode loop)
            {
                if (properties.TryGetValue("LoopType", out var typeObj))
                    loop.LoopType = Enum.Parse<LoopType>(typeObj.ToString()!);
                if (properties.TryGetValue("RepeatCount", out var rc))
                    loop.RepeatCount = int.Parse(rc.ToString()!);
                if (properties.TryGetValue("StartIndex", out var si))
                    loop.StartIndex = int.Parse(si.ToString()!);
                if (properties.TryGetValue("EndIndex", out var ei))
                    loop.EndIndex = int.Parse(ei.ToString()!);
                if (properties.TryGetValue("ArrayInputKey", out var aik))
                    loop.ArrayInputKey = aik?.ToString() ?? "array";
                if (properties.TryGetValue("InputType", out var it))
                {
                    if (Enum.TryParse<WorkflowDataType>(it?.ToString(), out var inputType))
                        loop.InputType = inputType;
                }
                if (properties.TryGetValue("CustomOutputMappings", out var comObj) && comObj != null)
                {
                    try
                    {
                        var json = comObj is string s ? s : (comObj is System.Text.Json.JsonElement je ? je.GetString() : null);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var list = System.Text.Json.JsonSerializer.Deserialize<List<LoopCustomOutputMapping>>(json);
                            if (list != null) { loop.CustomOutputMappings.Clear(); foreach (var m in list) loop.CustomOutputMappings.Add(m); }
                        }
                    }
                    catch { }
                }
                if (properties.TryGetValue("DataAssignments", out var daObj) && daObj != null)
                {
                    try
                    {
                        var json = daObj is string s ? s : (daObj is System.Text.Json.JsonElement je ? je.GetString() : null);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var list = System.Text.Json.JsonSerializer.Deserialize<List<LoopDataAssignment>>(json);
                            if (list != null) { loop.DataAssignments.Clear(); foreach (var a in list) loop.DataAssignments.Add(a); }
                        }
                    }
                    catch { }
                }
            }
            else if (node is AssignDataNode assignDataNode)
            {
                if (properties.TryGetValue("Assignments", out var assignObj) && assignObj != null)
                {
                    try
                    {
                        var json = assignObj is string s ? s : (assignObj is System.Text.Json.JsonElement je ? je.GetString() : null);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var list = System.Text.Json.JsonSerializer.Deserialize<List<AssignDataAssignment>>(json);
                            if (list != null) { assignDataNode.Assignments.Clear(); foreach (var a in list) assignDataNode.Assignments.Add(a); }
                        }
                    }
                    catch { }
                }
            }
            else if (node is MouseEventNode mouseNode)
            {
                if (properties.TryGetValue("MouseButton", out var btn))
                    mouseNode.MouseButton = btn?.ToString() ?? "Left";

                if (properties.TryGetValue("RepeatCount", out var rep) && int.TryParse(rep?.ToString(), out var repVal))
                    mouseNode.RepeatCount = repVal;

                if (properties.TryGetValue("HoldDuration", out var hold) && double.TryParse(hold?.ToString(), out var holdVal))
                    mouseNode.HoldDuration = holdVal;

                if (properties.TryGetValue("ScrollSpeed", out var speed) && int.TryParse(speed?.ToString(), out var speedVal))
                    mouseNode.ScrollSpeed = speedVal;
            }
            else if (node is ScreenPositionPickerNode pos)
            {
                if (properties.TryGetValue("X_Pos", out var x) && properties.TryGetValue("Y_Pos", out var y))
                {
                    pos.SelectedPosition = new Point(double.Parse(x.ToString()!), double.Parse(y.ToString()!));
                }
                if (properties.TryGetValue("HasPosition", out var hp))
                    pos.HasPosition = bool.Parse(hp.ToString()!);
            }
            else if (node is ScreenCaptureNode cap)
            {
                if (properties.TryGetValue("CaptureX", out var cx))
                    cap.CaptureX = int.Parse(cx.ToString()!);
                if (properties.TryGetValue("CaptureY", out var cy))
                    cap.CaptureY = int.Parse(cy.ToString()!);
                if (properties.TryGetValue("CaptureWidth", out var cw))
                    cap.CaptureWidth = int.Parse(cw.ToString()!);
                if (properties.TryGetValue("CaptureHeight", out var ch))
                    cap.CaptureHeight = int.Parse(ch.ToString()!);

                if (properties.TryGetValue("CapturedImageBase64", out var b64Obj))
                {
                    var b64 = b64Obj?.ToString();
                    var restored = TryDecodePngBase64ToBitmapImage(b64);
                    if (restored != null)
                    {
                        cap.CapturedImage = restored;
                    }
                }
            }
            else if (node is WebNode webNode)
            {
                if (properties.TryGetValue("Web_LastHost", out var hostObj))
                    webNode.LastHost = hostObj?.ToString();
                if (properties.TryGetValue("Web_CssZoom", out var zoomObj) &&
                    double.TryParse(zoomObj?.ToString(), out var cssZoom) &&
                    cssZoom > 0)
                {
                    webNode.CssZoom = cssZoom;
                }
            }
            else if (node is HtmlUiNode htmlUiNode)
            {
                if (properties.TryGetValue("HtmlUi_CssZoom", out var htmlZoomObj) &&
                    double.TryParse(htmlZoomObj?.ToString(), out var htmlCssZoom) &&
                    htmlCssZoom > 0)
                {
                    htmlUiNode.CssZoom = htmlCssZoom;
                }
            }
            else if (node is LoopBodyNode loopBody)
            {
                if (properties.TryGetValue("Width", out var w))
                    loopBody.Width = double.Parse(w.ToString()!);
                if (properties.TryGetValue("Height", out var h))
                    loopBody.Height = double.Parse(h.ToString()!);
            }
            else if (node is InputNode inputNode)
            {
                if (properties.TryGetValue("InputKey", out var keyObj))
                    inputNode.Key = keyObj?.ToString() ?? string.Empty;
                if (properties.TryGetValue("InputValue", out var valueObj))
                    inputNode.Value = valueObj?.ToString() ?? string.Empty;

                // ✅ QUAN TRỌNG: Restore DataType TRƯỚC ArrayValues
                // Vì setter của DataType sẽ tự động khởi tạo ArrayValues nếu là array type,
                // nên cần restore DataType trước để đảm bảo ArrayValues được restore đúng
                // Backward-compatible: chấp nhận cả "InputDataType" (mới) và "WorkflowDataType" (cũ)
                if (!properties.TryGetValue("InputDataType", out var typeObj))
                {
                    properties.TryGetValue("WorkflowDataType", out typeObj);
                }

                if (typeObj != null)
                {
                    var typeStr = typeObj.ToString();
                    if (!string.IsNullOrWhiteSpace(typeStr) &&
                        Enum.TryParse<WorkflowDataType>(typeStr, out var parsedType))
                    {
                        inputNode.DataType = parsedType;
                        // UpdateDynamicOutputsType sẽ được gọi tự động trong DataType setter
                    }
                }

                // Load ArrayValues sau khi đã restore DataType
                // Nếu DataType là array type và ArrayValues có trong properties, restore nó
                // (sẽ ghi đè giá trị mặc định được khởi tạo bởi DataType setter)
                if (properties.TryGetValue("InputArrayValues", out var arrayValuesObj))
                {
                    List<string>? parsedArray = null;

                    // Handle string (format mới: serialize thành JSON string)
                    if (arrayValuesObj is string jsonArray)
                    {
                        try
                        {
                            parsedArray = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jsonArray);
                        }
                        catch { }
                    }
                    // Handle JsonElement - có thể là string hoặc array
                    else if (arrayValuesObj is System.Text.Json.JsonElement jsonElement)
                    {
                        try
                        {
                            if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                // JsonElement là string (JSON string được deserialize)
                                var jsonString = jsonElement.GetString();
                                if (!string.IsNullOrWhiteSpace(jsonString))
                                {
                                    parsedArray = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jsonString);
                                }
                            }
                            else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                // JsonElement là array (format cũ hoặc khi deserialize trực tiếp)
                                parsedArray = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jsonElement.GetRawText());
                            }
                        }
                        catch { }
                    }
                    // Backward compatible: handle List<object> (format cũ)
                    else if (arrayValuesObj is List<object> list)
                    {
                        parsedArray = list.Select(x => x?.ToString() ?? string.Empty).ToList();
                    }

                    // Chỉ restore nếu parse thành công và là array type
                    if (parsedArray != null && inputNode.IsArrayType)
                    {
                        inputNode.ArrayValues = parsedArray;
                    }
                }
            }
            else if (node is DelayNode delayNode)
            {
                if (properties.TryGetValue("DelayMilliseconds", out var delayObj) &&
                    int.TryParse(delayObj?.ToString(), out var delayMs))
                {
                    delayNode.DelayMilliseconds = delayMs;
                }

                // UI display settings (optional - older workflows may not have these)
                if (properties.TryGetValue("DelayUnit", out var unitObj))
                {
                    var unitStr = unitObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(unitStr) &&
                        Enum.TryParse<DelayTimeUnit>(unitStr, out var parsedUnit))
                    {
                        delayNode.DelayUnit = parsedUnit;
                    }
                }

                if (properties.TryGetValue("DelayValue", out var valObj) &&
                    double.TryParse(valObj?.ToString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.CurrentCulture, out var parsedVal))
                {
                    delayNode.DelayValue = parsedVal;
                }
                else
                {
                    var multiplier = delayNode.DelayUnit switch
                    {
                        DelayTimeUnit.Milliseconds => 1d,
                        DelayTimeUnit.Seconds => 1000d,
                        DelayTimeUnit.Minutes => 60_000d,
                        DelayTimeUnit.Hours => 3_600_000d,
                        _ => 1000d
                    };
                    delayNode.DelayValue = multiplier <= 0 ? 0 : delayNode.DelayMilliseconds / multiplier;
                }
            }

            // Dynamic input selections (source node + output key)
            if (node.DynamicInputs != null && node.DynamicInputs.Count > 0)
            {
                foreach (var inp in node.DynamicInputs)
                {
                    var key = inp.Key ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    if (properties.TryGetValue($"DynIn_{key}_SrcNode", out var srcNodeObj))
                    {
                        var s = srcNodeObj?.ToString();
                        inp.SelectedSourceNodeId = string.IsNullOrWhiteSpace(s) ? null : s;
                    }

                    if (properties.TryGetValue($"DynIn_{key}_SrcKey", out var srcKeyObj))
                    {
                        var k = srcKeyObj?.ToString();
                        inp.SelectedSourceOutputKey = string.IsNullOrWhiteSpace(k) ? null : k;
                    }

                    // Extended: editable key/value + convert type
                    if (properties.TryGetValue($"DynIn_{key}_UserKey", out var userKeyObj))
                    {
                        var uk = userKeyObj?.ToString();
                        inp.UserKeyOverride = string.IsNullOrWhiteSpace(uk) ? null : uk;
                    }

                    if (properties.TryGetValue($"DynIn_{key}_UserValue", out var userValObj))
                    {
                        var uv = userValObj?.ToString();
                        inp.UserValueOverride = string.IsNullOrWhiteSpace(uv) ? null : uv;
                    }

                    if (properties.TryGetValue($"DynIn_{key}_ConvType", out var ctObj))
                    {
                        var ct = ctObj?.ToString();
                        if (!string.IsNullOrWhiteSpace(ct) &&
                            Enum.TryParse<WorkflowDataType>(ct, out var parsed))
                        {
                            inp.ConvertType = parsed;
                        }
                    }
                }
            }
        }

        private Dictionary<string, object> GetNodeProperties(WorkflowNode node)
        {
            var dict = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(node.Condition)) dict["Condition"] = node.Condition;
            if (!string.IsNullOrEmpty(node.Key)) dict["Key"] = node.Key;
            if (node.MouseEvent.HasValue) dict["MouseEvent"] = node.MouseEvent.Value.ToString();
            if (!string.IsNullOrEmpty(node.TargetElement)) dict["TargetElement"] = node.TargetElement;

            if (node is KeyPressEventNode kp && kp.RepeatCount != 1)
                dict["RepeatCount"] = kp.RepeatCount;
            else if (node is HotkeyPressEventNode hk && hk.RepeatCount != 1)
                dict["RepeatCount"] = hk.RepeatCount;

            if (node is LoopNode loop)
            {
                dict["LoopType"] = loop.LoopType.ToString();
                dict["RepeatCount"] = loop.RepeatCount;
                dict["StartIndex"] = loop.StartIndex;
                dict["EndIndex"] = loop.EndIndex;
                dict["ArrayInputKey"] = loop.ArrayInputKey;
                dict["InputType"] = loop.InputType.ToString();
                if (loop.CustomOutputMappings.Count > 0)
                    dict["CustomOutputMappings"] = System.Text.Json.JsonSerializer.Serialize(loop.CustomOutputMappings);
                if (loop.DataAssignments.Count > 0)
                    dict["DataAssignments"] = System.Text.Json.JsonSerializer.Serialize(loop.DataAssignments);
            }
            else if (node is AssignDataNode assignDataNode)
            {
                if (assignDataNode.Assignments.Count > 0)
                    dict["Assignments"] = System.Text.Json.JsonSerializer.Serialize(assignDataNode.Assignments);
            }
            else if (node is MouseEventNode mouseNode)
            {
                dict["MouseButton"] = mouseNode.MouseButton;
                dict["RepeatCount"] = mouseNode.RepeatCount;
                dict["HoldDuration"] = mouseNode.HoldDuration;
                dict["ScrollSpeed"] = mouseNode.ScrollSpeed;
            }
            else if (node is ScreenPositionPickerNode pos)
            {
                dict["X_Pos"] = pos.SelectedPosition.X;
                dict["Y_Pos"] = pos.SelectedPosition.Y;
                dict["HasPosition"] = pos.HasPosition;
            }
            else if (node is ScreenCaptureNode cap)
            {
                dict["CaptureX"] = cap.CaptureX;
                dict["CaptureY"] = cap.CaptureY;
                dict["CaptureWidth"] = cap.CaptureWidth;
                dict["CaptureHeight"] = cap.CaptureHeight;

                // Lưu ảnh dạng base64 (PNG) để import/export/ComboBox workflow load lại vẫn thấy preview ảnh
                var b64 = TryEncodeBitmapSourceToPngBase64(cap.CapturedImage);
                if (!string.IsNullOrWhiteSpace(b64))
                {
                    dict["CapturedImageBase64"] = b64;
                }
            }
            else if (node is WebNode webNode)
            {
                // Lưu thông tin zoom theo domain cho WebNode
                if (!string.IsNullOrWhiteSpace(webNode.LastHost))
                    dict["Web_LastHost"] = webNode.LastHost;
                if (webNode.CssZoom > 0)
                    dict["Web_CssZoom"] = webNode.CssZoom;
            }
            else if (node is HtmlUiNode htmlUiNode)
            {
                // Lưu zoom riêng cho HtmlUiNode
                if (htmlUiNode.CssZoom > 0)
                    dict["HtmlUi_CssZoom"] = htmlUiNode.CssZoom;
            }
            else if (node is LoopBodyNode loopBody)
            {
                dict["Width"] = loopBody.Width;
                dict["Height"] = loopBody.Height;
            }
            else if (node is InputNode inputNode)
            {
                if (!string.IsNullOrWhiteSpace(inputNode.Key))
                    dict["InputKey"] = inputNode.Key;
                if (!string.IsNullOrWhiteSpace(inputNode.Value))
                    dict["InputValue"] = inputNode.Value;
                dict["InputDataType"] = inputNode.DataType.ToString();

                // Save ArrayValues nếu là array type (luôn lưu kể cả khi rỗng để đảm bảo type consistency)
                if (inputNode.IsArrayType && inputNode.ArrayValues != null)
                {
                    // Serialize thành JSON string để đảm bảo deserialize đúng cách
                    dict["InputArrayValues"] = System.Text.Json.JsonSerializer.Serialize(inputNode.ArrayValues);
                }
            }
            else if (node is DelayNode delayNode)
            {
                dict["DelayMilliseconds"] = delayNode.DelayMilliseconds;
                dict["DelayValue"] = delayNode.DelayValue;
                dict["DelayUnit"] = delayNode.DelayUnit.ToString();
            }

            // Dynamic input selections (persist minimal config)
            if (node.DynamicInputs != null && node.DynamicInputs.Count > 0)
            {
                foreach (var inp in node.DynamicInputs)
                {
                    var key = inp.Key ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    if (!string.IsNullOrWhiteSpace(inp.SelectedSourceNodeId))
                        dict[$"DynIn_{key}_SrcNode"] = inp.SelectedSourceNodeId!;

                    if (!string.IsNullOrWhiteSpace(inp.SelectedSourceOutputKey))
                        dict[$"DynIn_{key}_SrcKey"] = inp.SelectedSourceOutputKey!;

                    // Extended: editable key/value + convert type
                    if (!string.IsNullOrWhiteSpace(inp.UserKeyOverride))
                        dict[$"DynIn_{key}_UserKey"] = inp.UserKeyOverride!;

                    if (!string.IsNullOrWhiteSpace(inp.UserValueOverride))
                        dict[$"DynIn_{key}_UserValue"] = inp.UserValueOverride!;

                    dict[$"DynIn_{key}_ConvType"] = inp.ConvertType.ToString();
                }
            }

            return dict;
        }

        private static string? TryEncodeBitmapSourceToPngBase64(BitmapSource? source)
        {
            if (source == null) return null;
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                return Convert.ToBase64String(ms.ToArray());
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage? TryDecodePngBase64ToBitmapImage(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;
            try
            {
                var bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                ms.Position = 0;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public void RefreshSavedWorkflows()
        {
            try
            {
                var names = _persistenceService.GetAllWorkflowNames();
                var currentName = CurrentWorkflowName;

                SavedWorkflows.Clear();
                foreach (var name in names) SavedWorkflows.Add(name);
                OnPropertyChanged(nameof(SavedWorkflows));

                // Ensure CurrentWorkflowName still selected (khi _isRefreshingAfterSave=true thì không trigger LoadWorkflow)
                if (!string.IsNullOrWhiteSpace(currentName))
                {
                    if (SavedWorkflows.Contains(currentName))
                        CurrentWorkflowName = currentName;
                    else
                    {
                        var match = SavedWorkflows.FirstOrDefault(n =>
                            string.Equals(n, currentName, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                            CurrentWorkflowName = match;
                    }
                }
            }
            catch { }
        }

        private async Task LoadWorkflowAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            CancellationTokenSource? previousCts = null;
            var cts = new CancellationTokenSource();
            previousCts = Interlocked.Exchange(ref _workflowLoadCts, cts);
            previousCts?.Cancel();
            previousCts?.Dispose();

            try
            {
                IsLoading = true;
                var token = cts.Token;
                var result = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    return _persistenceService.Load(name);
                }, token);

                if (token.IsCancellationRequested || !ReferenceEquals(_workflowLoadCts, cts))
                    return;

                if (result == null) return;
                ApplyWorkflowLoadResult(result);
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                if (ReferenceEquals(_workflowLoadCts, cts))
                {
                    _workflowLoadCts = null;
                }
                cts.Dispose();
                IsLoading = false;
            }
        }

        private void InitializeSampleNodes()
        {
            // Tâm lưới 20000×20000 → center (10000, 10000)
            double centerX = 10000;
            double centerY = 10000;
            double nodeHalfSize = 50;  // Start/End node size = 100
            double halfGap = 100;

            var startNode = new WorkflowNode
            {
                Id = "Node_Start",
                Title = "Start",
                X = centerX - halfGap - nodeHalfSize,
                Y = centerY - nodeHalfSize,
                NodeBrush = GetBrushFromTheme("SkyAzureBrush") ?? GetBrushFromTheme("PrimaryBrush") ?? Brushes.Blue,
                ColorKey = "SkyAzure",
                Type = NodeType.Start,
                IsDefaultSampleStartEnd = true
            };
            startNode.Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true
            });
            Nodes.Add(startNode);

            var endNode = new WorkflowNode
            {
                Id = "Node_End",
                Title = "End",
                X = centerX + halfGap - nodeHalfSize,
                Y = centerY - nodeHalfSize,
                NodeBrush = GetBrushFromTheme("DangerBrush") ?? Brushes.Red,
                ColorKey = "Danger",
                Type = NodeType.End,
                IsDefaultSampleStartEnd = true
            };
            endNode.Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true
            });
            Nodes.Add(endNode);
        }

        public void AddNodeInternal(string? title = null, double? x = null, double? y = null, Brush? brush = null)
        {
            var node = new WorkflowNode
            {
                Id = $"Node_{_nodeCounter++}",
                Title = title ?? $"Node {_nodeCounter - 1}",
                X = x ?? 100,
                Y = y ?? 100,
                NodeBrush = brush ?? GetBrushFromTheme("InfoBrush")
            };

            Nodes.Add(node);
        }

        [RelayCommand(CanExecute = nameof(CanDeleteNode))]
        private void DeleteNode()
        {
            if (SelectedNode != null)
            {
                var connectionsToRemove = Connections
                    .Where(c => c.FromNode == SelectedNode || c.ToNode == SelectedNode)
                    .ToList();

                foreach (var conn in connectionsToRemove)
                {
                    Connections.Remove(conn);
                }

                Nodes.Remove(SelectedNode);
                SelectedNode = null;
            }
        }

        private bool CanDeleteNode()
        {
            if (SelectedNode == null) return false;
            if ((SelectedNode.Type == NodeType.Start || SelectedNode.Type == NodeType.End) &&
                SelectedNode.IsDefaultSampleStartEnd)
                return false;
            return true;
        }

        public void CreateConnection(WorkflowNode fromNode, WorkflowNode toNode, bool isFromInput = false)
        {
            if (Connections.Any(c => c.FromNode == fromNode && c.ToNode == toNode && c.IsFromInput == isFromInput))
            {
                return;
            }

            var connection = new WorkflowConnection
            {
                FromNode = fromNode,
                ToNode = toNode,
                IsFromInput = isFromInput
            };

            Connections.Add(connection);
        }

        public void UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            node.X = x;
            node.Y = y;
        }

        private Brush GetBrushFromTheme(string resourceKey)
        {
            try
            {
                var brush = Application.Current.TryFindResource(resourceKey) as Brush;
                return brush ?? Brushes.Gray;
            }
            catch
            {
                return Brushes.Gray;
            }
        }
    }
}
