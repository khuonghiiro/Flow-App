using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utilities;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using FlowMy.Properties;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowMy.Services.Interaction
{
    public sealed class WorkflowEditorEventService
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly INodeRenderer _nodeRenderer;
        private readonly IConnectionRenderer _connectionRenderer;
        private readonly ZoomPanHandler _zoomPanHandler;
        private readonly MinimapService _minimapService;
        private readonly CollisionResolver _collisionResolver;
        private readonly HashSet<INotifyPropertyChanged> _trackedNodeNotifiers = new();
        /// <summary>
        /// Flag chống re-entrancy khi đang chạy RefreshDynamicDataSourceSelectors.
        /// Không còn dùng timer/real-time, chỉ chạy theo hành động.
        /// </summary>
        private bool _isRefreshingDynamicDataSelectors;
        /// <summary>
        /// Node được copy để paste (Ctrl+C/Ctrl+V)
        /// </summary>
        private WorkflowNode? _copiedNode;

        public WorkflowEditorEventService(
            IWorkflowEditorHostAccessor hostAccessor,
            INodeRenderer nodeRenderer,
            IConnectionRenderer connectionRenderer,
            ZoomPanHandler zoomPanHandler,
            MinimapService minimapService,
            CollisionResolver collisionResolver)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _nodeRenderer = nodeRenderer ?? throw new ArgumentNullException(nameof(nodeRenderer));
            _connectionRenderer = connectionRenderer ?? throw new ArgumentNullException(nameof(connectionRenderer));
            _zoomPanHandler = zoomPanHandler ?? throw new ArgumentNullException(nameof(zoomPanHandler));
            _minimapService = minimapService ?? throw new ArgumentNullException(nameof(minimapService));
            _collisionResolver = collisionResolver ?? throw new ArgumentNullException(nameof(collisionResolver));
        }

        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();
        private WorkflowEditorViewModel? ViewModel => Host.ViewModel;

        public void InitialRender()
        {
            var vm = ViewModel;
            if (vm == null) return;

            // Xóa sạch visuals trước khi render để tránh lỗi parenting
            _nodeRenderer.RemoveAllNodeVisuals(Host.WorkflowCanvas);
            _connectionRenderer.ClearAllConnectionVisuals();

            foreach (var node in vm.Nodes)
            {
                TrackNodeNotifier(node);
                _nodeRenderer.RenderNode(node, Host.WorkflowCanvas);
            }

            // Khi GPU bật: defer connections sang frame sau → nodes hiện trước, load cảm giác nhanh hơn
            var useGpuDefer = GpuDetectionHelper.IsGpuAvailable && Settings.Default.GpuEnabled;
            if (useGpuDefer)
            {
                var conns = vm.Connections.ToList();
                Host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    _connectionRenderer.RenderAllConnections(
                        conns,
                        setSelectedConnection: c => Host.SelectedConnection = c,
                        focusWindow: Host.FocusWindow,
                        requestDeleteConnection: DeleteConnection);
                    _zoomPanHandler.UpdateCanvasSize();
                }));
            }
            else
            {
                _connectionRenderer.RenderAllConnections(
                    vm.Connections,
                    setSelectedConnection: c => Host.SelectedConnection = c,
                    focusWindow: Host.FocusWindow,
                    requestDeleteConnection: DeleteConnection);
            }

            _zoomPanHandler.UpdateCanvasSize();
            // Defer RefreshDynamicDataSourceSelectors để không chặn hiển thị (minimap sẽ được UpdateMinimap sau RestoreViewState)
            Host.Dispatcher.BeginInvoke(new Action(RefreshDynamicDataSourceSelectors), DispatcherPriority.Loaded);
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            if (TryHandleHoveredNodePortShortcut(e))
            {
                e.Handled = true;
                return;
            }

            // Xử lý Ctrl+C (Copy)
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.SelectedNode != null)
                {
                    // Hỗ trợ nhiều loại node có dialog
                    if (vm.SelectedNode is KeyPressEventNode || 
                    vm.SelectedNode is HotkeyPressEventNode || 
                    vm.SelectedNode is MouseEventNode ||
                    vm.SelectedNode is StringSplitNode || 
                    vm.SelectedNode is ListOutNode || 
                    vm.SelectedNode is AssignDataNode ||
                    vm.SelectedNode is MediaGalleryNode ||
                    vm.SelectedNode is ImageProcessingNode ||
                    vm.SelectedNode is CodeNode ||
                    vm.SelectedNode is FolderNode ||
                    vm.SelectedNode is FileDownloadNode ||
                    vm.SelectedNode is FolderFilePathsNode ||
                    vm.SelectedNode is FlowOverwriteNode ||
                    vm.SelectedNode is WebNode ||
                    vm.SelectedNode is HtmlUiNode ||
                    vm.SelectedNode is InputNode || 
                    vm.SelectedNode is OutputNode ||
                    vm.SelectedNode is NotificationNode ||
                    vm.SelectedNode is HttpRequestNode ||
                    vm.SelectedNode is LoopNode ||
                    vm.SelectedNode.IsConditionalNode)
                    {
                        _copiedNode = vm.SelectedNode;
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Xử lý Ctrl+V (Paste)
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (_copiedNode != null)
                {
                    // Hỗ trợ nhiều loại node có dialog
                    if (_copiedNode is KeyPressEventNode ||
                       _copiedNode is HotkeyPressEventNode ||
                       _copiedNode is MouseEventNode ||
                       _copiedNode is InputNode || 
                       _copiedNode is OutputNode ||
                       _copiedNode is StringSplitNode || 
                       _copiedNode is ListOutNode ||
                       _copiedNode is AssignDataNode ||
                       _copiedNode is MediaGalleryNode ||
                       _copiedNode is ImageProcessingNode ||
                       _copiedNode is CodeNode ||
                       _copiedNode is FolderNode ||
                       _copiedNode is FileDownloadNode ||
                       _copiedNode is FolderFilePathsNode ||
                       _copiedNode is FlowOverwriteNode ||
                       _copiedNode is WebNode ||
                       _copiedNode is HtmlUiNode ||
                       _copiedNode is NotificationNode ||
                       _copiedNode is HttpRequestNode ||
                       _copiedNode is LoopNode ||
                       _copiedNode is AsyncTaskNode ||
                       _copiedNode.IsConditionalNode)
                    {
                        // Lấy vị trí con trỏ chuột trên Canvas
                        var mousePos = Mouse.GetPosition(Host.WorkflowCanvas);
                        
                        // Convert từ screen coordinates sang canvas coordinates (có tính zoom/pan)
                        var canvasX = mousePos.X;
                        var canvasY = mousePos.Y;

                        // Tạo node copy tại vị trí con trỏ chuột
                        Host.DuplicateNodeAtPosition(_copiedNode, canvasX, canvasY);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Ctrl+S: lưu (đè nếu đã có file) + toast giống Notification node (ToastNotificationService)
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.TrySaveFromEditorShortcut())
                {
                    var name = string.IsNullOrWhiteSpace(vm.CurrentWorkflowName) ? "Workflow" : vm.CurrentWorkflowName;
                    // SuccessBrush / TextOnSuccessBrush: định nghĩa theo Light/Dark theme → tương phản tốt trên nền app sáng/tối
                    ToastNotificationService.ShowToast(
                        "Đã lưu",
                        name,
                        durationSeconds: 3,
                        titleColorKey: "TextOnSuccessBrush",
                        contentColorKey: "TextOnSuccessBrush",
                        backgroundColorKey: "SuccessBrush",
                        backgroundOpacity: 0.96);
                }
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Delete) return;

            if (Host.SelectedConnection != null)
            {
                DeleteConnection(Host.SelectedConnection);
                e.Handled = true;
                return;
            }

            if (vm.SelectedNode != null && vm.DeleteNodeCommand.CanExecute(null))
            {
                vm.DeleteNodeCommand.Execute(null);
                e.Handled = true;
            }
        }

        private bool TryHandleHoveredNodePortShortcut(KeyEventArgs e)
        {
            // Shortcut hỗ trợ:
            // - Arrow = đổi Port IN
            // - Shift+Arrow = đổi Port OUT
            // - 4/8/6/2 (numpad hoặc hàng số) = đổi Port OUT
            // Không can thiệp khi user đang nhập liệu hoặc giữ Ctrl/Alt/Windows.
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != ModifierKeys.None)
                return false;

            if (IsEditingTextInput())
                return false;

            bool isNumericOutShortcut = e.Key is Key.NumPad4 or Key.NumPad8 or Key.NumPad6 or Key.NumPad2
                or Key.D4 or Key.D8 or Key.D6 or Key.D2;

            PortPosition? newPos = e.Key switch
            {
                Key.Left => PortPosition.Left,
                Key.Up => PortPosition.Top,
                Key.Right => PortPosition.Right,
                Key.Down => PortPosition.Bottom,
                Key.NumPad4 => PortPosition.Left,
                Key.NumPad8 => PortPosition.Top,
                Key.NumPad6 => PortPosition.Right,
                Key.NumPad2 => PortPosition.Bottom,
                Key.D4 => PortPosition.Left,
                Key.D8 => PortPosition.Top,
                Key.D6 => PortPosition.Right,
                Key.D2 => PortPosition.Bottom,
                _ => null
            };

            if (newPos == null)
                return false;

            var hoveredNode = TryGetHoveredNode();
            if (hoveredNode == null)
                return false;

            // 4/8/6/2 luôn đổi Port OUT; Arrow thì giữ behavior cũ.
            bool changeInputPort = !isNumericOutShortcut
                && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift;
            return ChangePortPosition(hoveredNode, newPos.Value, changeInputPort);
        }

        private WorkflowNode? TryGetHoveredNode()
        {
            var hovered = Mouse.DirectlyOver as DependencyObject;
            if (hovered == null)
                return null;

            // 1) Ưu tiên lấy node trực tiếp từ Border.Tag.
            var current = hovered;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.Tag is WorkflowNode nodeFromTag)
                    return nodeFromTag;
                current = VisualTreeHelper.GetParent(current);
            }

            // 2) Nếu đang hover port (Ellipse/Rectangle), dùng NodePort.Tag rồi map ngược về node.
            if (hovered is FrameworkElement hoveredElement && hoveredElement.Tag is NodePort hoveredPort)
            {
                var vm = ViewModel;
                if (vm == null) return null;
                return vm.Nodes.FirstOrDefault(n => n.Ports.Contains(hoveredPort));
            }

            return null;
        }

        private bool ChangePortPosition(WorkflowNode node, PortPosition newPosition, bool isInputPort)
        {
            if (node.Ports == null || node.Ports.Count == 0)
                return false;

            var targetPort = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);

            if (targetPort == null || targetPort.Position == newPosition)
                return false;

            targetPort.Position = newPosition;
            Host.UpdatePortsPositionOnSide(node, newPosition);

            var connections = ViewModel?.Connections;
            if (connections != null && connections.Count > 0)
            {
                try
                {
                    _connectionRenderer.UpdateAllConnectionPaths(connections);
                    _connectionRenderer.UpdateAllConnectionAnimations(connections);
                }
                catch
                {
                    // best-effort refresh visuals
                }
            }

            return true;
        }

        private static bool IsEditingTextInput()
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            while (focused != null)
            {
                if (focused is System.Windows.Controls.Primitives.TextBoxBase ||
                    focused is System.Windows.Controls.PasswordBox ||
                    focused is System.Windows.Controls.ComboBox)
                {
                    return true;
                }

                focused = VisualTreeHelper.GetParent(focused);
            }

            return false;
        }

        public void HandleViewModelPropertyChanged(PropertyChangedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            if (e.PropertyName == nameof(WorkflowEditorViewModel.SelectedNode))
            {
                _minimapService.Update();
            }

            // Khi bắt đầu load (IsLoading = true) cần dọn sạch canvas tránh ghost visuals
            if (e.PropertyName == nameof(WorkflowEditorViewModel.IsLoading))
            {
                if (vm.IsLoading)
                {
                    _nodeRenderer.RemoveAllNodeVisuals(Host.WorkflowCanvas);
                    _connectionRenderer.ClearAllConnectionVisuals();
                }
                else
                {
                    // Sau khi load xong render lại từ dữ liệu mới.
                    // View state (zoom/pan) sẽ được restore thông qua ZoomPanHandler.RestoreViewState
                    // được gọi trong WorkflowEditorWindow.ViewModel_PropertyChanged.
                    InitialRender();
                    // RefreshDynamicDataSourceSelectors đã được gọi trong InitialRender (deferred Loaded)
                }
            }
        }

        public void HandleNodesCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null || vm.IsLoading) return;

            if (e.NewItems != null)
            {
                foreach (WorkflowNode node in e.NewItems)
                {
                    TrackNodeNotifier(node);
                    if (node.X == 100 && node.Y == 100 && node.Id.StartsWith("Node_") && !node.Id.Contains("Start") && !node.Id.Contains("End"))
                    {
                        var viewportCenter = _zoomPanHandler.GetViewportCenter();
                        node.X = viewportCenter.X;
                        node.Y = viewportCenter.Y;
                        vm.UpdateNodePosition(node, node.X, node.Y);
                    }

                    _nodeRenderer.RenderNode(node, Host.WorkflowCanvas);
                    
                    // ✅ Resolve collision sau khi node được render
                    // Sử dụng Dispatcher.BeginInvoke với priority Loaded để đảm bảo node đã được measure (có ActualWidth/ActualHeight)
                    Host.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (node.Border != null && vm != null)
                        {
                            _collisionResolver.ResolveCollision(vm, node, Host);
                        }
                    }), DispatcherPriority.Loaded);
                }
            }

            if (e.OldItems != null)
            {
                foreach (WorkflowNode node in e.OldItems)
                {
                    UntrackNodeNotifier(node);
                    _nodeRenderer.RemoveNode(node, Host.WorkflowCanvas);
                }

                _connectionRenderer.RenderAllConnections(
                    vm.Connections,
                    setSelectedConnection: c => Host.SelectedConnection = c,
                    focusWindow: Host.FocusWindow,
                    requestDeleteConnection: DeleteConnection);
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                ClearTrackedNotifiers();
                _nodeRenderer.RemoveAllNodeVisuals(Host.WorkflowCanvas);
            }

            _minimapService.Update();
        }

        private void TrackNodeNotifier(WorkflowNode node)
        {
            if (node is INotifyPropertyChanged npc && _trackedNodeNotifiers.Add(npc))
            {
                npc.PropertyChanged += NodeNotifier_PropertyChanged;
            }
        }

        private void UntrackNodeNotifier(WorkflowNode node)
        {
            if (node is INotifyPropertyChanged npc && _trackedNodeNotifiers.Remove(npc))
            {
                npc.PropertyChanged -= NodeNotifier_PropertyChanged;
            }
        }

        private void ClearTrackedNotifiers()
        {
            foreach (var npc in _trackedNodeNotifiers.ToList())
            {
                npc.PropertyChanged -= NodeNotifier_PropertyChanged;
            }
            _trackedNodeNotifiers.Clear();
        }

        /// <summary>
        /// Yêu cầu đồng bộ data panel theo hành động của user (TextChanged, SelectionChanged, v.v.).
        /// Không còn idle-timer; mỗi lần gọi sẽ xử lý một lần duy nhất.
        /// </summary>
        public void RequestSyncDataPanels(bool immediate)
        {
            // hiện không cần đồng bộ nữa
            return;

            var vm = ViewModel;
            if (vm == null || vm.IsLoading) return;
            if (Application.Current?.Dispatcher != null && Application.Current.Dispatcher.CheckAccess())
            {
                RefreshDynamicDataSourceSelectors();
            }
            else
            {
                Application.Current?.Dispatcher?.BeginInvoke(
                    new Action(RefreshDynamicDataSourceSelectors),
                    DispatcherPriority.Normal);
            }
        }

        private void NodeNotifier_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // ⚠️ CRITICAL: PropertyChanged có thể được trigger từ background thread
            // Đảm bảo truy cập ViewModel (DependencyProperty) trên UI thread
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => NodeNotifier_PropertyChanged(sender, e)), DispatcherPriority.Normal);
                return;
            }

            // Trước đây: bất kỳ PropertyChanged nào của node cũng trigger đồng bộ real-time.
            // Bây giờ: bỏ hoàn toàn, chỉ đồng bộ khi có hành động (kết nối, combobox, textbox...).
            var vm = ViewModel;
            if (vm == null || vm.IsLoading) return;
            // no-op
        }

        public void HandleConnectionsCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null || vm.IsLoading) return;

            if (e.NewItems != null)
            {
                foreach (WorkflowConnection conn in e.NewItems)
                {
                    _connectionRenderer.RenderConnection(
                        conn,
                        setSelectedConnection: c => Host.SelectedConnection = c,
                        focusWindow: Host.FocusWindow,
                        requestDeleteConnection: DeleteConnection);
                }
            }

            if (e.OldItems != null)
            {
                foreach (WorkflowConnection conn in e.OldItems)
                {
                    _connectionRenderer.RemoveConnectionVisuals(conn);
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                _connectionRenderer.ClearAllConnectionVisuals();
            }

            RefreshDynamicDataSourceSelectors();
            _minimapService.Update();
        }

        private sealed record DataEdge(WorkflowNode OutputNode, WorkflowNode InputNode);

        public void RefreshDynamicDataSourceSelectors()
        {
            if (_isRefreshingDynamicDataSelectors) return;
            var vm = ViewModel;
            if (vm == null) return;
            
            // ✅ QUAN TRỌNG: Nếu đang loading, không refresh để tránh reset giá trị đã restore
            // Giá trị sẽ được refresh sau khi loading xong (trong HandleViewModelPropertyChanged)
            if (vm.IsLoading) return;
            
            _isRefreshingDynamicDataSelectors = true;
            try
            {
                // Include LoopBody nodes too (they can appear in Connections)
                var allNodes = vm.Nodes
                    .Concat(vm.Nodes.OfType<LoopNode>().Select(l => l.LoopBodyNode))
                    .GroupBy(n => n.Id)
                    .Select(g => g.First())
                    .ToList();

                var edges = vm.Connections
                    .Select(TryGetDataEdge)
                    .Where(e => e != null)
                    .Cast<DataEdge>()
                    .ToList();

                var nodeById = allNodes.ToDictionary(n => n.Id, n => n);

                foreach (var target in allNodes.Where(n => n.DynamicInputs != null && n.DynamicInputs.Count > 0))
                {
                    var incoming = edges.Where(ed => ReferenceEquals(ed.InputNode, target)).ToList();
                    if (incoming.Count == 0)
                    {
                        // ✅ QUAN TRỌNG: Lưu SelectedSourceOutputKey trước khi reset
                        // để đảm bảo giá trị đã restore từ JSON không bị mất
                        foreach (var dynIn in target.DynamicInputs)
                        {
                            var savedSelectedSourceOutputKey = dynIn.SelectedSourceOutputKey;
                            
                            dynIn.AvailableSources = new List<WorkflowDataSourceOption>();
                            dynIn.AvailableOutputKeys = new List<string>();
                            dynIn.AvailableOutputKeyOptions = new List<WorkflowOutputKeyOption>();
                            
                            // ✅ QUAN TRỌNG: Chỉ reset SelectedSourceOutputKey nếu chưa được restore từ JSON
                            // Nếu đã có giá trị từ JSON, giữ lại để có thể restore sau khi connections được thiết lập
                            if (string.IsNullOrWhiteSpace(savedSelectedSourceOutputKey))
                            {
                                dynIn.SelectedSourceOutputKey = null;
                            }
                            // Nếu đã có giá trị, giữ lại (không reset)
                            
                            if (dynIn.SourceSelectorUI != null)
                            {
                                dynIn.SourceSelectorUI.ItemsSource = dynIn.AvailableSources;
                                dynIn.SourceSelectorUI.Visibility = Visibility.Collapsed;
                            }
                            if (dynIn.OutputKeySelectorUI != null)
                            {
                                dynIn.OutputKeySelectorUI.ItemsSource = dynIn.AvailableOutputKeyOptions;
                                dynIn.OutputKeySelectorUI.SelectedValue = dynIn.SelectedSourceOutputKey;
                                dynIn.OutputKeySelectorUI.SelectedItem = null;
                                dynIn.OutputKeySelectorUI.Visibility = Visibility.Collapsed;
                            }
                            if (dynIn.ResolvedSourceTextUI != null)
                            {
                                dynIn.ResolvedSourceTextUI.Text = "From: —";
                                dynIn.ResolvedSourceTextUI.Visibility = Visibility.Visible;
                            }
                            if (dynIn.ResolvedOutputKeyTextUI != null)
                            {
                                dynIn.ResolvedOutputKeyTextUI.Text = "Key: —";
                                dynIn.ResolvedOutputKeyTextUI.Visibility = Visibility.Visible;
                            }
                            if (dynIn.ResolvedValueTextUI != null)
                            {
                                dynIn.ResolvedValueTextUI.Text = "Value: —";
                            }
                        }
                        continue;
                    }

                    var producerNodes = incoming
                        .SelectMany(ed => GetUpstreamProducers(ed.OutputNode, edges))
                        .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                        .Distinct()
                        .ToList();

                    var options = producerNodes
                        .Select(n => new WorkflowDataSourceOption
                        {
                            NodeId = n.Id,
                            Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                        })
                        .ToList();

                    // Bỏ auto-expand: chỉ mở khi user click toggle để tránh render nhiều dẫn đến đơ
                    // User sẽ tự mở panel khi cần

                    foreach (var dynIn in target.DynamicInputs)
                    {
                        dynIn.AvailableSources = options;

                        // ✅ QUAN TRỌNG: Giữ lại SelectedSourceNodeId đã được restore từ JSON
                        // Chỉ auto-select node đầu tiên nếu SelectedSourceNodeId chưa được set (null hoặc empty)
                        // Nếu đã có SelectedSourceNodeId nhưng không có trong options, VẪN GIỮ LẠI để có thể restore sau
                        if (!string.IsNullOrWhiteSpace(dynIn.SelectedSourceNodeId))
                        {
                            // Đã có SelectedSourceNodeId (có thể từ JSON restore)
                            if (options.Any(o => o.NodeId == dynIn.SelectedSourceNodeId))
                            {
                                // Có trong options, giữ nguyên
                            }
                            // Nếu không có trong options, VẪN GIỮ LẠI (không reset) để có thể restore sau khi connections được thiết lập đầy đủ
                        }
                        else
                        {
                            // Chưa có SelectedSourceNodeId, auto-select node đầu tiên
                            dynIn.SelectedSourceNodeId = options.FirstOrDefault()?.NodeId;
                        }

                        if (dynIn.SourceSelectorUI != null)
                        {
                            dynIn.SourceSelectorUI.ItemsSource = options;
                            dynIn.SourceSelectorUI.Visibility = options.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                            dynIn.SourceSelectorUI.SelectedValue = dynIn.SelectedSourceNodeId;
                        }

                        // Output keys from selected source (với type metadata)
                        // ✅ QUAN TRỌNG: Lưu SelectedSourceOutputKey TRƯỚC KHI tạo outputKeyOptions
                        // để đảm bảo giá trị đã restore từ JSON không bị mất
                        var savedSelectedSourceOutputKey = dynIn.SelectedSourceOutputKey;

                        var outputKeyOptions = new List<WorkflowOutputKeyOption>();
                        if (!string.IsNullOrWhiteSpace(dynIn.SelectedSourceNodeId) &&
                            nodeById.TryGetValue(dynIn.SelectedSourceNodeId, out var srcNode) &&
                            srcNode.DynamicOutputs != null)
                        {
                            outputKeyOptions = srcNode.DynamicOutputs
                                .Where(o => !string.IsNullOrWhiteSpace(o.Key))
                                .Select(o => new WorkflowOutputKeyOption
                                {
                                    Key = o.Key.Trim(),
                                    Type = o.OutputType
                                })
                                .GroupBy(opt => opt.Key, StringComparer.OrdinalIgnoreCase)
                                .Select(g => g.First()) // Lấy option đầu tiên nếu có duplicate key
                                .ToList();
                        }

                        dynIn.AvailableOutputKeyOptions = outputKeyOptions;
                        dynIn.AvailableOutputKeys = outputKeyOptions.Select(opt => opt.Key).ToList();

                        // ✅ QUAN TRỌNG: Giữ lại SelectedSourceOutputKey đã được restore từ JSON
                        // Sử dụng savedSelectedSourceOutputKey thay vì dynIn.SelectedSourceOutputKey
                        // vì có thể dynIn.SelectedSourceOutputKey đã bị reset ở đâu đó
                        if (!string.IsNullOrWhiteSpace(savedSelectedSourceOutputKey))
                        {
                            // Đã có SelectedSourceOutputKey (có thể từ JSON restore)
                            if (outputKeyOptions.Any(k => string.Equals(k.Key, savedSelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)))
                            {
                                // Có trong outputKeyOptions, giữ nguyên (normalize case)
                                dynIn.SelectedSourceOutputKey = outputKeyOptions.First(k => string.Equals(k.Key, savedSelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)).Key;
                            }
                            else
                            {
                                // Nếu không có trong outputKeyOptions, VẪN GIỮ LẠI (không reset) để có thể restore sau khi output key options được refresh đầy đủ
                                dynIn.SelectedSourceOutputKey = savedSelectedSourceOutputKey;
                            }
                        }
                        else
                        {
                            // Chưa có SelectedSourceOutputKey, fallback logic: prefer input.Key, else first
                            var prefer = (dynIn.Key ?? string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(prefer) &&
                                outputKeyOptions.Any(k => string.Equals(k.Key, prefer, StringComparison.OrdinalIgnoreCase)))
                            {
                                dynIn.SelectedSourceOutputKey = outputKeyOptions.First(k => string.Equals(k.Key, prefer, StringComparison.OrdinalIgnoreCase)).Key;
                            }
                            else
                            {
                                dynIn.SelectedSourceOutputKey = outputKeyOptions.FirstOrDefault()?.Key;
                            }
                        }

                        if (dynIn.OutputKeySelectorUI != null)
                        {
                            dynIn.OutputKeySelectorUI.ItemsSource = dynIn.AvailableOutputKeyOptions;
                            dynIn.OutputKeySelectorUI.SelectedValue = dynIn.SelectedSourceOutputKey;
                            // Set SelectedItem để đảm bảo hiển thị đúng text (WPF sẽ gọi ToString())
                            if (!string.IsNullOrWhiteSpace(dynIn.SelectedSourceOutputKey) && dynIn.AvailableOutputKeyOptions != null)
                            {
                                var selectedOption = dynIn.AvailableOutputKeyOptions.FirstOrDefault(opt => opt.Key == dynIn.SelectedSourceOutputKey);
                                if (selectedOption != null)
                                {
                                    dynIn.OutputKeySelectorUI.SelectedItem = selectedOption;
                                }
                            }
                            dynIn.OutputKeySelectorUI.Visibility = outputKeyOptions.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                        }

                        if (dynIn.ResolvedSourceTextUI != null)
                        {
                            var resolved = ResolveTitleForSelected(dynIn.SelectedSourceNodeId, options);
                            var newFrom = "From: " + resolved;
                            if (dynIn.ResolvedSourceTextUI.Text != newFrom) dynIn.ResolvedSourceTextUI.Text = newFrom;
                            dynIn.ResolvedSourceTextUI.Visibility = options.Count > 1 ? Visibility.Collapsed : Visibility.Visible;
                        }

                        if (dynIn.ResolvedOutputKeyTextUI != null)
                        {
                            var resolvedKey = string.IsNullOrWhiteSpace(dynIn.SelectedSourceOutputKey) ? "—" : dynIn.SelectedSourceOutputKey;
                            var newKey = "Key: " + resolvedKey;
                            if (dynIn.ResolvedOutputKeyTextUI.Text != newKey) dynIn.ResolvedOutputKeyTextUI.Text = newKey;
                            dynIn.ResolvedOutputKeyTextUI.Visibility = outputKeyOptions.Count > 1 ? Visibility.Collapsed : Visibility.Visible;
                        }

                    // Tính preview value một lần để dùng cho cả label và textbox
                    string previewValue = ResolveSelectedValuePreview(dynIn, nodeById);

                    if (dynIn.ResolvedValueTextUI != null)
                    {
                        var newVal = "Value: " + previewValue;

                        // Nếu preview không đổi so với lần trước và text UI đã đúng thì bỏ qua
                        if (dynIn.LastResolvedPreviewValue == previewValue &&
                            dynIn.ResolvedValueTextUI.Text == newVal)
                        {
                            // no-op
                        }
                        else
                        {
                            dynIn.ResolvedValueTextUI.Text = newVal;
                            dynIn.LastResolvedPreviewValue = previewValue;
                        }
                    }

                    // Cập nhật UserValueTextBoxUI nếu có (cho các input đang mở để chỉnh sửa)
                    // Chỉ cập nhật nếu UserValueOverride rỗng (user chưa nhập thủ công) hoặc giá trị preview khác với UserValueOverride
                    if (dynIn.UserValueTextBoxUI != null)
                    {
                        // Nếu UserValueOverride rỗng, luôn cập nhật với giá trị preview
                        // Nếu UserValueOverride không rỗng, chỉ cập nhật nếu preview khác với UserValueOverride
                        var shouldUpdate = string.IsNullOrWhiteSpace(dynIn.UserValueOverride) 
                            || dynIn.UserValueOverride != previewValue;

                        // Nếu mọi thứ đều giống lần trước thì bỏ qua để tránh set Text liên tục
                        if (!shouldUpdate && dynIn.UserValueTextBoxUI.Text == previewValue)
                        {
                            // no-op
                        }
                        else if (shouldUpdate && dynIn.UserValueTextBoxUI.Text != previewValue)
                        {
                            // Set flag để tránh trigger TextChanged không cần thiết
                            dynIn.IsSyncingValue = true;
                            try
                            {
                                dynIn.UserValueTextBoxUI.Text = previewValue;
                                // Chỉ cập nhật UserValueOverride nếu nó rỗng (để sync từ output)
                                if (string.IsNullOrWhiteSpace(dynIn.UserValueOverride))
                                {
                                    dynIn.UserValueOverride = previewValue;
                                }
                            }
                            finally
                            {
                                dynIn.IsSyncingValue = false;
                            }
                        }
                    }

                        // Tự động cập nhật ConvertType dựa trên OutputType của output key được chọn
                        // QUAN TRỌNG: Luôn lấy lại từ vm.Nodes để đảm bảo lấy giá trị mới nhất
                        // Đặc biệt với InputNode: lấy trực tiếp từ InputNode.DataType (biến trung gian)
                        if (!string.IsNullOrWhiteSpace(dynIn.SelectedSourceOutputKey) &&
                            !string.IsNullOrWhiteSpace(dynIn.SelectedSourceNodeId) &&
                            vm != null)
                        {
                            // Lấy lại từ vm.Nodes để đảm bảo lấy instance mới nhất
                            var currentSrcNode = vm.Nodes.FirstOrDefault(n => n.Id == dynIn.SelectedSourceNodeId);
                            if (currentSrcNode != null)
                            {
                                WorkflowDataType? workflowType = null;

                                // Nếu là InputNode, lấy trực tiếp từ InputNode.DataType (biến trung gian mới nhất)
                                if (currentSrcNode is InputNode inputNode)
                                {
                                    // Lấy giá trị mới nhất từ combobox type của InputNode
                                    workflowType = inputNode.DataType;
                                }
                                else if (currentSrcNode.DynamicOutputs != null)
                                {
                                    // Với các node khác, lấy từ DynamicOutputs
                                    var outputPort = currentSrcNode.DynamicOutputs.FirstOrDefault(o =>
                                        string.Equals(o.Key, dynIn.SelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase));

                                    if (outputPort != null && outputPort.OutputType.HasValue)
                                    {
                                        workflowType = outputPort.OutputType.Value;
                                    }
                                }

                                // Set ConvertType nếu có type
                                if (workflowType.HasValue)
                                {
                                    dynIn.ConvertType = workflowType.Value;

                                    // Cập nhật UI combobox nếu có
                                    if (dynIn.ConvertTypeSelectorUI != null)
                                    {
                                        dynIn.ConvertTypeSelectorUI.SelectedItem = workflowType.Value;
                                    }
                                }
                            }
                        }
                    }
                }

            }
            finally
            {
                _isRefreshingDynamicDataSelectors = false;
            }
        }

        private static string ResolveTitleForSelected(string? selectedNodeId, List<WorkflowDataSourceOption> options)
        {
            if (options == null || options.Count == 0) return "—";

            if (!string.IsNullOrWhiteSpace(selectedNodeId))
            {
                var picked = options.FirstOrDefault(o => o.NodeId == selectedNodeId);
                if (picked != null) return picked.ToString();
            }

            return options[0].ToString();
        }

        private static string ResolveSelectedValuePreview(WorkflowDynamicDataPort input, Dictionary<string, WorkflowNode> nodeById)
        {
            if (string.IsNullOrWhiteSpace(input.SelectedSourceNodeId)) return "—";
            if (!nodeById.TryGetValue(input.SelectedSourceNodeId, out var src)) return "—";

            var key = (input.SelectedSourceOutputKey ?? input.UserKeyOverride ?? input.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                // default summary
                if (src is ScreenPositionPickerNode pos) return pos.PositionText;
                if (src is ScreenCaptureNode cap)
                {
                    var w = cap.CapturedImage?.PixelWidth ?? cap.CaptureWidth;
                    var h = cap.CapturedImage?.PixelHeight ?? cap.CaptureHeight;
                    return cap.CapturedImage != null ? $"Image {w}×{h}" : (cap.HasCaptureRegion ? $"Region {cap.CaptureWidth}×{cap.CaptureHeight}" : "—");
                }
                return "—";
            }

            var resolved = NodeDataPanelService.ResolveDynamicValueByKey(src, key);
            if (resolved != "—") return resolved;

            // fallback summary (khi key không match)
            if (src is ScreenPositionPickerNode pos2) return pos2.PositionText;
            if (src is ScreenCaptureNode cap2)
            {
                var w2 = cap2.CapturedImage?.PixelWidth ?? cap2.CaptureWidth;
                var h2 = cap2.CapturedImage?.PixelHeight ?? cap2.CaptureHeight;
                return cap2.CapturedImage != null ? $"Image {w2}×{h2}" : (cap2.HasCaptureRegion ? $"Region {cap2.CaptureWidth}×{cap2.CaptureHeight}" : "—");
            }
            return "—";
        }

        private static string ResolveDynamicValueByKey(WorkflowNode node, string key)
        {
            // Delegate to NodeDataPanelService để tránh duplicate code
            return NodeDataPanelService.ResolveDynamicValueByKey(node, key);
        }

        private static IEnumerable<WorkflowNode> GetUpstreamProducers(WorkflowNode startOutput, List<DataEdge> edges)
        {
            var visited = new HashSet<WorkflowNode>();
            var q = new Queue<WorkflowNode>();

            visited.Add(startOutput);
            q.Enqueue(startOutput);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();

                foreach (var e in edges.Where(ed => ReferenceEquals(ed.InputNode, cur)))
                {
                    if (visited.Add(e.OutputNode))
                    {
                        q.Enqueue(e.OutputNode);
                    }
                }
            }

            return visited;
        }

        private static DataEdge? TryGetDataEdge(WorkflowConnection c)
        {
            if (c.FromPort == null || c.ToPort == null) return null;

            // ⚠️ Loại bỏ connections đến LoopBodyRight (return path, không phải data flow)
            // Nếu không loại bỏ, sẽ tạo cycle: D -> LoopBody B -> E (via return) -> D
            if (c.ToNode is LoopBodyNode &&
                c.ToPort != null &&
                string.Equals(c.ToPort.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!c.FromPort.IsInput && c.ToPort.IsInput)
            {
                return new DataEdge(c.FromNode, c.ToNode);
            }

            if (c.FromPort.IsInput && !c.ToPort.IsInput)
            {
                // Normalize direction to Output -> Input
                return new DataEdge(c.ToNode, c.FromNode);
            }

            return null;
        }

        public void SetConnectionAnimationDisplayMode(ConnectionAnimationDisplayMode mode)
        {
            Host.ConnectionAnimationDisplayMode = mode;
            Host.IsAnimationEnabled = mode == ConnectionAnimationDisplayMode.Animated;

            var vm = ViewModel;
            if (vm == null) return;

            _connectionRenderer.UpdateAllConnectionAnimations(vm.Connections);

            // Sync animation state for internal conditional diamond lines.
            foreach (var node in vm.Nodes.Where(n => n.IsConditionalNode && n.ConditionalVisualMode == ConditionalVisualMode.Diamond))
            {
                foreach (var branch in node.ConditionalBranches)
                {
                    if (branch.SatelliteLine == null) continue;
                    var color = Colors.Orange;
                    if (branch.SatelliteLine.Stroke is SolidColorBrush brush)
                        color = brush.Color;

                    if (mode == ConnectionAnimationDisplayMode.Animated)
                    {
                        branch.SatelliteLine.StrokeDashArray = null;
                        branch.SatelliteLine.StrokeDashOffset = 0;
                        FlowMy.Views.NodeControls.ConditionalDiamondControl.StartSatelliteLineAnimation(branch.SatelliteLine, color);
                    }
                    else if (mode == ConnectionAnimationDisplayMode.Dashed)
                    {
                        FlowMy.Views.NodeControls.ConditionalDiamondControl.StopSatelliteLineAnimation(branch.SatelliteLine);
                        branch.SatelliteLine.StrokeDashArray = new DoubleCollection { 6, 4 };
                        branch.SatelliteLine.StrokeDashOffset = 0;
                    }
                    else
                    {
                        FlowMy.Views.NodeControls.ConditionalDiamondControl.StopSatelliteLineAnimation(branch.SatelliteLine);
                        branch.SatelliteLine.StrokeDashArray = null;
                        branch.SatelliteLine.StrokeDashOffset = 0;
                    }
                }
            }
        }

        public void DeleteConnection(WorkflowConnection connection)
        {
            var vm = ViewModel;
            if (vm == null) return;

            // QUAN TRỌNG: Không cho xóa default connection của Loop Node
            var loopNode = connection.FromNode as LoopNode ?? connection.ToNode as LoopNode;
            if (loopNode != null && loopNode.DefaultConnection == connection)
            {
                // Không cho xóa - có thể hiển thị message hoặc bỏ qua
                System.Diagnostics.Debug.WriteLine("Cannot delete default loop connection");
                return;
            }

            _connectionRenderer.RemoveConnectionVisuals(connection);
            vm.Connections.Remove(connection);
            Host.SelectedConnection = null;
            _minimapService.Update();
            
            // Đóng dialog nếu đang mở
            CloseNodeDialogIfOpen();
        }
        
        private void CloseNodeDialogIfOpen()
        {
            // Lấy NodeDialogManager từ host (WorkflowEditorWindow)
            if (Host is Window window)
            {
                var field = window.GetType().GetField("_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager)
                {
                    manager.CloseCurrentDialog();
                }
            }
        }
    }
}

