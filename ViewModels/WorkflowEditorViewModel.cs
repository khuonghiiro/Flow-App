using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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

        private readonly object _manualRunCtsLock = new();
        private readonly Dictionary<string, CancellationTokenSource> _manualRunCtsBySession =
            new(StringComparer.Ordinal);

        private readonly object _runningNodesBookkeepingLock = new();
        private readonly Dictionary<WorkflowNode, int> _nodeRunningRefCount = new();

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
            LoadWorkflow(value);
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

        private void RegisterRunningNodeVisual(WorkflowNode node)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
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
            });
        }

        /// <summary>Gỡ ref-count trên UI thread (luôn qua Background) để finally của async không chặn UI khi hủy nhiều node.</summary>
        private void ReleaseRunningNodeVisualBatch(IReadOnlyList<WorkflowNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            var batch = nodes.ToList();
            dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
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
            });
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
                    _executionVisualizer.OnNodeStarted(node, sessionId);
                    RegisterRunningNodeVisual(node);
                }

                void OnNodeCompleted(WorkflowNode node, TimeSpan elapsed)
                {
                    lock (pendingNodesLock)
                        pendingNodesThisSession.Remove(node);
                    if (sessionToken.IsCancellationRequested) return;
                    _executionVisualizer.OnNodeCompleted(node, elapsed, sessionId);

                    FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.NotifyNodeCompleted(node);

                    ReleaseRunningNodeVisual(node);
                }

                void OnNodeFailed(WorkflowNode node, string errorMessage)
                {
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
                }), DispatcherPriority.Background);

                var remaining = Interlocked.Decrement(ref _manualExecutionRunsInFlight);
                IsExecuting = remaining > 0;
                if (remaining == 0)
                    ActiveExecutionConnection = null;

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (!AnyAutoScheduledLaneInFlight() && remaining == 0)
                    {
                        lock (_runningNodesBookkeepingLock)
                            _nodeRunningRefCount.Clear();
                        RunningNodes.Clear();
                        HasRunningNodes = false;
                    }

                    if (operationCancelled && remaining == 0 && !AnyAutoScheduledLaneInFlight())
                        _executionVisualizer.OnExecutionCancelled();
                }), DispatcherPriority.Background);
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
                    _executionVisualizer.OnNodeStarted(node, sessionId);
                    RegisterRunningNodeVisual(node);
                }

                void OnNodeCompleted(WorkflowNode node, TimeSpan elapsed)
                {
                    lock (pendingNodesLockFromNode)
                        pendingNodesFromNode.Remove(node);
                    if (sessionTokenFromNode.IsCancellationRequested) return;
                    _executionVisualizer.OnNodeCompleted(node, elapsed, sessionId);

                    FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.NotifyNodeCompleted(node);

                    ReleaseRunningNodeVisual(node);
                }

                void OnNodeFailed(WorkflowNode node, string errorMessage)
                {
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
                }), DispatcherPriority.Background);

                var remaining = Interlocked.Decrement(ref _manualExecutionRunsInFlight);
                IsExecuting = remaining > 0;
                if (remaining == 0)
                    ActiveExecutionConnection = null;

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (!AnyAutoScheduledLaneInFlight() && remaining == 0)
                    {
                        lock (_runningNodesBookkeepingLock)
                            _nodeRunningRefCount.Clear();
                        RunningNodes.Clear();
                        HasRunningNodes = false;
                    }

                    if (operationCancelled && remaining == 0 && !AnyAutoScheduledLaneInFlight())
                        _executionVisualizer.OnExecutionCancelled();
                }), DispatcherPriority.Background);
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
                    _executionVisualizer.OnNodeStarted(node, runId);
                    RegisterRunningNodeVisual(node);
                }

                void OnNodeCompleted(WorkflowNode node, TimeSpan elapsed)
                {
                    lock (pendingAutoLock)
                        pendingAutoNodes.Remove(node);
                    if (laneToken.IsCancellationRequested) return;
                    _executionVisualizer.OnNodeCompleted(node, elapsed, runId);

                    FlowMy.Services.Workflow.NodeExecutors.DataFetcherNodeExecutor.NotifyNodeCompleted(node);

                    ReleaseRunningNodeVisual(node);
                }

                void OnNodeFailed(WorkflowNode node, string errorMessage)
                {
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
                Nodes.Clear();
                Connections.Clear();

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

                foreach (var node in result.Nodes)
                    Nodes.Add(node);

                foreach (var conn in result.Connections)
                    Connections.Add(conn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing workflow: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }

            if (Nodes.Count > 0)
            {
                var tempNodes = Nodes.ToList();
                Nodes.Clear();
                foreach (var node in tempNodes)
                    Nodes.Add(node);
            }

            if (Connections.Count > 0)
            {
                var tempConns = Connections.ToList();
                Connections.Clear();
                foreach (var conn in tempConns)
                    Connections.Add(conn);
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

        private void LoadWorkflow(string name)
        {
            if (IsLoading) return;
            try
            {
                IsLoading = true;
                var result = _persistenceService.Load(name);
                if (result == null) return;

                Nodes.Clear();
                Connections.Clear();

                CurrentWorkflowName = result.Name;

                // ⭐ CRITICAL: Restore view state BEFORE adding nodes
                // This ensures nodes are rendered at the correct positions with the right zoom/pan
                ZoomLevel = result.ZoomLevel;
                PanX = result.PanX;
                PanY = result.PanY;

                // Restore connection line style (fallback Bezier nếu thiếu)
                if (!string.IsNullOrWhiteSpace(result.ConnectionLineStyle) &&
                    Enum.TryParse<ConnectionLineStyle>(result.ConnectionLineStyle, out var restoredStyle))
                {
                    ConnectionLineStyle = restoredStyle;
                }
                else
                {
                    ConnectionLineStyle = ConnectionLineStyle.Bezier;
                }

                foreach (var node in result.Nodes)
                {
                    Nodes.Add(node);
                }

                foreach (var conn in result.Connections)
                {
                    Connections.Add(conn);
                }
            }
            catch { }
            finally
            {
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
