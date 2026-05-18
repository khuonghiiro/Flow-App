using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System.Collections.ObjectModel;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// Base ViewModel cho các node dialog, chứa các phần chung như NodeTitle, TitleDisplayMode, Inputs, Outputs.
    /// </summary>
    public abstract partial class BaseNodeDialogViewModel : BaseViewModel
    {
        protected readonly WorkflowNode _node;
        protected readonly IWorkflowEditorHost _host;

        /// <summary>
        /// Gets the underlying WorkflowNode for accessing node properties like NodeBrush.
        /// </summary>
        public WorkflowNode Node => _node;

        [ObservableProperty]
        private string _nodeTitle;

        [ObservableProperty]
        private TitleDisplayMode _titleDisplayMode;

        [ObservableProperty]
        private TitleColorMode _titleColorMode;

        [ObservableProperty]
        private string? _titleColorKey;

        /// <summary>
        /// Được gọi khi TitleColorKey thay đổi.
        /// Cập nhật TitleColorMode và sync về node ngay lập tức.
        /// </summary>
        partial void OnTitleColorKeyChanged(string? value)
        {
            // Cập nhật TitleColorMode dựa trên key được chọn
            TitleColorMode newMode;
            if (string.IsNullOrEmpty(value) || value == "NodeColor")
            {
                newMode = TitleColorMode.NodeColor;
            }
            else
            {
                newMode = TitleColorMode.CustomColor;
            }

            // Sync về node ngay lập tức - dùng trực tiếp thay vì reflection
            SetTitleColorModeDirectly(_node, newMode);
            SetTitleColorKeyDirectly(_node, value == "NodeColor" ? null : value);

            // Update UI title color ngay
            UpdateTitleColorImmediate(value);
        }

        /// <summary>
        /// Set TitleColorMode trực tiếp cho các node types cụ thể.
        /// </summary>
        private static void SetTitleColorModeDirectly(WorkflowNode node, TitleColorMode mode)
            => node.TitleColorMode = mode;

        /// <summary>
        /// Set TitleColorKey trực tiếp cho các node types cụ thể.
        /// </summary>
        private static void SetTitleColorKeyDirectly(WorkflowNode node, string? key)
            => node.TitleColorKey = key;

        /// <summary>
        /// Cập nhật màu title ngay lập tức trên canvas.
        /// </summary>
        private void UpdateTitleColorImmediate(string? colorKey)
        {
            if (_node.TitleTextBlockUI == null) return;

            System.Windows.Media.Brush? brush = null;

            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                brush = _node.NodeBrush;
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            }
            else
            {
                brush = System.Windows.Application.Current.TryFindResource(colorKey) as System.Windows.Media.Brush;
            }

            if (brush != null)
            {
                _node.TitleTextBlockUI.Foreground = brush;
            }
        }

        public ObservableCollection<InputItemViewModel> Inputs { get; } = new();
        public ObservableCollection<OutputItemViewModel> Outputs { get; } = new();

        /// <summary>
        /// Danh sách cấu hình route lại flow cho node hiện tại (tab "Tái sử dụng flow").
        /// Mỗi item tương ứng 1 node nối trực tiếp vào input của node hiện tại.
        /// </summary>
        public ObservableCollection<ReuseRouteItemViewModel> ReuseRoutes { get; } = new();

        /// <summary>
        /// Options cho combobox kiểu line trong tab "Tái sử dụng flow".
        /// </summary>
        public ObservableCollection<ConnectionLineStyleOption> ConnectionLineStyleOptions { get; } = new()
        {
            new ConnectionLineStyleOption("WorkflowDefault", "Theo cấu hình workflow (nút kiểu đường kết nối)"),
            new ConnectionLineStyleOption("Bezier", "Bezier (Cong mượt)"),
            new ConnectionLineStyleOption("Orthogonal", "Vuông góc (Orthogonal)"),
            new ConnectionLineStyleOption("Straight", "Thẳng (Straight)"),
            new ConnectionLineStyleOption("SmoothOrthogonal", "Vuông góc bo tròn (Smooth-Orthogonal)"),
            new ConnectionLineStyleOption("Arc", "Cung tròn (Arc)"),
            new ConnectionLineStyleOption("RadialFanout", "Tỏa quạt (Radial / Fan-out)"),
            new ConnectionLineStyleOption("Windy", "Gió thổi (Windy)"),
            new ConnectionLineStyleOption("OrthogonalV2", "Vuông góc thông minh (Orthogonal V2)")
        };

        /// <summary>
        /// Options chung cho combobox vị trí cổng IN/OUT (Left/Top/Right/Bottom).
        /// Dùng trong tab "Tái sử dụng flow" để cho phép người dùng chọn phía của cổng.
        /// </summary>
        public ObservableCollection<PortPosition> PortPositionOptions { get; } = new()
        {
            PortPosition.Left,
            PortPosition.Top,
            PortPosition.Right,
            PortPosition.Bottom
        };

        /// <summary>
        /// Vị trí cổng IN (input port) của node hiện tại.
        /// Mặc định lấy từ port input đầu tiên trong <see cref="WorkflowNode.Ports"/>.
        /// </summary>
        [ObservableProperty]
        private PortPosition _inputPortPosition;

        /// <summary>
        /// Vị trí cổng OUT (output port) của node hiện tại.
        /// Mặc định lấy từ port output đầu tiên trong <see cref="WorkflowNode.Ports"/>.
        /// </summary>
        [ObservableProperty]
        private PortPosition _outputPortPosition;

        // Options cho ComboBox TitleDisplayMode
        public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
        {
            new TitleDisplayModeOption(TitleDisplayMode.Hidden, "Ẩn tiêu đề"),
            new TitleDisplayModeOption(TitleDisplayMode.Hover, "Hiện khi hover"),
            new TitleDisplayModeOption(TitleDisplayMode.Always, "Luôn hiện")
        };

        // Options cho ComboBox TitleColorMode
        public ObservableCollection<TitleColorOption> TitleColorOptions { get; } = new()
        {
            new TitleColorOption("NodeColor", "Màu theo node"),
            new TitleColorOption("LimeGreen", "Lime Green"),
            new TitleColorOption("PrimaryBrush", "Primary Blue"),
            new TitleColorOption("SuccessBrush", "Success Green"),
            new TitleColorOption("DangerBrush", "Danger Red"),
            new TitleColorOption("WarningBrush", "Warning Orange"),
            new TitleColorOption("InfoBrush", "Info Cyan"),
            new TitleColorOption("IndigoBrush", "Indigo"),
            new TitleColorOption("CoralBrush", "Coral"),
            new TitleColorOption("OceanBrush", "Ocean"),
            new TitleColorOption("LavenderBrush", "Lavender")
        };

        protected BaseNodeDialogViewModel(WorkflowNode node, IWorkflowEditorHost host)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host ?? throw new ArgumentNullException(nameof(host));

            _nodeTitle = node.Title ?? GetDefaultTitle();
            _titleDisplayMode = GetTitleDisplayMode(node);
            _titleColorMode = GetTitleColorMode(node);

            // Set TitleColorKey based on current mode
            var currentColorKey = GetTitleColorKey(node);
            if (_titleColorMode == TitleColorMode.NodeColor || string.IsNullOrEmpty(currentColorKey))
            {
                _titleColorKey = "NodeColor";
            }
            else
            {
                _titleColorKey = currentColorKey;
            }

            // Khởi tạo vị trí port IN/OUT từ node.Ports (nếu có).
            var inputPort = node.Ports.FirstOrDefault(p => p.IsInput);
            var outputPort = node.Ports.FirstOrDefault(p => !p.IsInput);
            _inputPortPosition = inputPort?.Position ?? PortPosition.Left;
            _outputPortPosition = outputPort?.Position ?? PortPosition.Right;

            LoadInputs();
            LoadOutputs();
            LoadReuseRoutes(); // override có thể clear khi SupportsReuseRoutes=false

            // Sync title khi node thay đổi
            node.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WorkflowNode.Title))
                {
                    NodeTitle = node.Title ?? GetDefaultTitle();
                }
                else if (e.PropertyName == "TitleDisplayMode")
                {
                    TitleDisplayMode = GetTitleDisplayMode(node);
                }
                else if (e.PropertyName == "TitleColorMode")
                {
                    TitleColorMode = GetTitleColorMode(node);
                }
                else if (e.PropertyName == "TitleColorKey")
                {
                    TitleColorKey = GetTitleColorKey(node);
                }
                OnNodePropertyChanged(e.PropertyName);
            };
        }

        /// <summary>
        /// Override để cung cấp default title cho từng loại node.
        /// </summary>
        protected abstract string GetDefaultTitle();

        /// <summary>
        /// Node có nhiều port OUT (ConditionalNode, AsyncTaskNode) thì không dùng ReuseRoutes
        /// vì logic map Node IN -> Node OUT gây gộp tất cả line vào 1 port khi save.
        /// </summary>
        protected virtual bool SupportsReuseRoutes => true;

        /// <summary>
        /// Override để xử lý các property changes từ node (ngoài Title và TitleDisplayMode).
        /// </summary>
        protected virtual void OnNodePropertyChanged(string propertyName) { }

        /// <summary>
        /// Load inputs từ node's DynamicInputs.
        /// </summary>
        protected virtual void LoadInputs()
        {
            Inputs.Clear();

            if (_node.DynamicInputs == null || _node.DynamicInputs.Count == 0) return;

            // Refresh AvailableSources cho tất cả inputs trước khi tạo InputItemViewModel
            RefreshAvailableSourcesForInputs();

            foreach (var input in _node.DynamicInputs)
            {
                var item = new InputItemViewModel(_node, input, _host);
                // Refresh AvailableSources trong InputItemViewModel sau khi refresh input.AvailableSources
                if (input.AvailableSources != null)
                {
                    item.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(input.AvailableSources);
                }
                Inputs.Add(item);
            }
        }

        /// <summary>
        /// Refresh AvailableSources cho tất cả inputs với tiêu đề node mới nhất.
        /// Mặc định: lấy TOÀN BỘ upstream nodes (các node kết nối đến INPUT ports của node hiện tại)
        /// có DynamicOutputs > 0 trong cùng workflow, ví dụ: A -> E -> G -> D
        /// thì dialog của D sẽ thấy A, E trong combobox (G không có output thì bỏ qua).
        /// 
        /// Quan trọng: Chỉ lấy upstream nodes (các node kết nối đến port IN của node hiện tại),
        /// KHÔNG lấy downstream nodes (các node kết nối từ port OUT của node hiện tại).
        /// 
        /// ⚠️ CRITICAL: ListOutNode acts as a "barrier" - chặn upstream nodes.
        /// Nếu có ListOutNode ở upstream, chỉ thấy ListOutNode, không thấy các node trước đó.
        /// Ví dụ: A -> B -> ListOut -> Z, khi mở dialog Z thì chỉ thấy ListOut.
        /// </summary>
        protected virtual void RefreshAvailableSourcesForInputs()
        {
            if (_host.ViewModel == null) return;
            if (_node.DynamicInputs == null || _node.DynamicInputs.Count == 0) return;

            var vm = _host.ViewModel;
            var connections = vm.Connections;
            if (connections == null || connections.Count == 0) return;

            // Thu thập toàn bộ upstream nodes của _node (D): A, E, G...
            // ⚠️ CRITICAL: Dừng lại khi gặp ListOutNode (barrier)
            var upstream = new HashSet<WorkflowNode>();
            var listOutBarriers = new HashSet<ListOutNode>(); // Track ListOutNode barriers
            var stack = new Stack<WorkflowNode>();
            stack.Push(_node);

            // Track các LoopNode cha nếu luồng đi qua LoopBodyLeft của LoopBodyNode
            var parentLoops = new HashSet<LoopNode>();

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                // Tìm các incoming connections (connections có ToNode == current)
                // ⚠️ QUAN TRỌNG: Loại bỏ connections đến LoopBodyRight vì đây là return path,
                // không phải data flow. Nếu không loại bỏ, sẽ tạo cycle:
                // D -> LoopBody B (via LoopBodyLeft) -> E (via LoopBodyRight return) -> D (via E's input)
                var incoming = connections
                    .Where(c => c.ToNode == current && c.FromNode != null)
                    .Where(c => !(current is LoopBodyNode &&
                                  c.ToPort != null &&
                                  string.Equals(c.ToPort.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var conn in incoming)
                {
                    var src = conn.FromNode;
                    if (src == null) continue;

                    // ⚠️ CRITICAL: ListOutNode is a barrier - add it but don't traverse beyond it
                    if (src is ListOutNode listOutNode)
                    {
                        if (upstream.Add(src))
                        {
                            listOutBarriers.Add(listOutNode);
                        }
                        // DON'T push to stack - stop traversing upstream
                        continue;
                    }

                    if (src is LoopBodyNode body &&
                        conn.FromPort != null &&
                        string.Equals(conn.FromPort.Id, "LoopBodyLeft", StringComparison.OrdinalIgnoreCase) &&
                        body.ParentLoopNode != null)
                    {
                        parentLoops.Add(body.ParentLoopNode);
                    }

                    if (upstream.Add(src))
                    {
                        stack.Push(src);
                    }
                }
            }

            // ⚠️ CRITICAL: Nếu có ListOutNode barriers, chỉ expose ListOutNodes
            // Các node khác sẽ bị filter out
            var producerNodes = upstream
                .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                .ToList();

            if (listOutBarriers.Count > 0)
            {
                // Filter: chỉ giữ ListOutNodes và các nodes KHÔNG bị barrier chặn
                // Logic: một node bị chặn nếu TẤT CẢ các đường đi từ nó đến _node đều đi qua ListOutNode
                // Simplified: nếu có ListOutNode, chỉ expose ListOutNodes
                producerNodes = producerNodes
                    .Where(n => n is ListOutNode)
                    .ToList();
            }

            foreach (var loop in parentLoops)
            {
                if (loop.DynamicOutputs != null && loop.DynamicOutputs.Count > 0 && !producerNodes.Contains(loop))
                {
                    // ⚠️ Nếu có ListOutNode barrier, không thêm LoopNode cha
                    if (listOutBarriers.Count == 0)
                    {
                        producerNodes.Add(loop);
                    }
                }
            }

            // Không bao giờ cho phép chính _node xuất hiện trong combobox source
            producerNodes = producerNodes
                .Where(n => !ReferenceEquals(n, _node))
                .ToList();

            if (producerNodes.Count == 0)
            {
                // Không có producers, clear AvailableSources
                foreach (var input in _node.DynamicInputs)
                {
                    input.AvailableSources = new List<WorkflowDataSourceOption>();
                }
                return;
            }

            // Tạo options với tiêu đề mới nhất từ node
            var options = producerNodes
                .Select(n => CreateDataSourceOption(n))
                .ToList();

            // Mỗi input một list riêng (cùng nội dung) — tránh dùng chung reference List khiến WPF/binding đồng bộ lệch giữa các ComboBox.
            foreach (var input in _node.DynamicInputs)
            {
                input.AvailableSources = options
                    .Select(o => CreateDataSourceOption_Clone(o))
                    .ToList();
            }
        }

        /// <summary>
        /// Load outputs từ node's DynamicOutputs.
        /// </summary>
        protected virtual void LoadOutputs()
        {
            Outputs.Clear();

            if (_node.DynamicOutputs == null || _node.DynamicOutputs.Count == 0) return;

            foreach (var output in _node.DynamicOutputs)
            {
                var item = new OutputItemViewModel(_node, output);
                Outputs.Add(item);
            }
        }

        /// <summary>
        /// Load danh sách cấu hình "tái sử dụng flow" dựa trên các node nối trực tiếp vào/ra node hiện tại.
        /// </summary>
        protected virtual void LoadReuseRoutes()
        {
            ReuseRoutes.Clear();

            if (!SupportsReuseRoutes) return;
            if (_host.ViewModel == null) return;
            var vm = _host.ViewModel;
            var connections = vm.Connections;
            if (connections == null || connections.Count == 0) return;

            // Chỉ lấy các node nối TRỰC TIẾP (không qua trung gian) vào/ra node hiện tại
            // ⚠️ LOẠI BỎ StorageNode - vì StorageNode luôn được chạy tự động khi có connection
            var previousNodes = connections
                .Where(c => c.ToNode == _node && c.FromNode != null && c.FromNode is not FlowMy.Models.Nodes.StorageNode)
                .Select(c => c.FromNode!)
                .Distinct()
                .ToList();

            var nextNodes = connections
                .Where(c => c.FromNode == _node && c.ToNode != null && c.ToNode is not FlowMy.Models.Nodes.StorageNode)
                .Select(c => c.ToNode!)
                .Distinct()
                .ToList();

            if (previousNodes.Count == 0 || nextNodes.Count == 0)
            {
                return; // Không có đủ in/out để cấu hình route
            }

            // Chuẩn bị danh sách lựa chọn cho node out
            var outgoingOptions = nextNodes
                .Select(n => CreateDataSourceOption(n))
                .ToList();

            // Tạo 1 item cho mỗi node nối trực tiếp vào
            foreach (var prev in previousNodes)
            {
                var existing = _node.ReuseRoutes
                    .FirstOrDefault(r => string.Equals(r.IncomingNodeId, prev.Id, StringComparison.OrdinalIgnoreCase));

                var item = new ReuseRouteItemViewModel
                {
                    IncomingNodeId = prev.Id,
                    IncomingNodeTitle = string.IsNullOrWhiteSpace(prev.Title) ? prev.Id : prev.Title
                };

                foreach (var opt in outgoingOptions)
                {
                    item.OutgoingOptions.Add(opt);
                }

                if (existing != null)
                {
                    if (!string.IsNullOrWhiteSpace(existing.OutgoingNodeId))
                    {
                        item.SelectedOutgoingNodeId = existing.OutgoingNodeId;
                    }

                    // Nếu chưa có LineStyleKey hoặc rỗng ⇒ dùng WorkflowDefault, ngược lại dùng key đã lưu
                    item.SelectedLineStyleKey = string.IsNullOrWhiteSpace(existing.LineStyleKey)
                        ? "WorkflowDefault"
                        : existing.LineStyleKey;
                }
                else
                {
                    // Mặc định: theo cấu hình workflow
                    item.SelectedLineStyleKey = "WorkflowDefault";
                }

                ReuseRoutes.Add(item);
            }
        }

        /// <summary>
        /// Chạy logic của node này (từ nút Play trong dialog). Chỉ thực thi node đó, cập nhật output.
        /// </summary>
        [RelayCommand]
        protected void RunSingleNode()
        {
            _host.RequestRunSingleNode(_node);
        }

        /// <summary>
        /// Chạy workflow bắt đầu từ node hiện tại theo đúng luồng connections (giống nút Bắt đầu, nhưng start từ node này).
        /// Các node phía trước nó sẽ không chạy lại.
        /// </summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        protected async Task RunWorkflowFromNode()
        {
            var vm = _host.ViewModel;
            if (vm == null) return;

            try
            {
                await vm.RunWorkflowFromNodeAsync(_node);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RunWorkflowFromNode error: {ex.Message}");
            }
        }

        /// <summary>
        /// Save title và title display mode. Override OnSaveTitle để thêm các properties khác.
        /// </summary>
        [RelayCommand]
        protected void SaveTitle()
        {
            bool hasChanges = false;

            if (_node.Title != NodeTitle)
            {
                _node.Title = NodeTitle;
                hasChanges = true;
            }

            var currentTitleDisplayMode = GetTitleDisplayMode(_node);
            if (currentTitleDisplayMode != TitleDisplayMode)
            {
                SetTitleDisplayMode(_node, TitleDisplayMode);
                hasChanges = true;
            }

            var currentTitleColorMode = GetTitleColorMode(_node);
            if (currentTitleColorMode != TitleColorMode)
            {
                SetTitleColorMode(_node, TitleColorMode);
                hasChanges = true;
            }

            var currentTitleColorKey = GetTitleColorKey(_node);
            if (currentTitleColorKey != TitleColorKey)
            {
                SetTitleColorKey(_node, TitleColorKey);
                hasChanges = true;
            }

            // Sync cấu hình "tái sử dụng flow" về node (áp dụng cho node có SupportsReuseRoutes)
            // ConditionalNode, AsyncTaskNode có nhiều port OUT nên không dùng ReuseRoutes
            bool reuseChanged = false;
            if (SupportsReuseRoutes && _node.ReuseRoutes != null)
            {
                _node.ReuseRoutes.Clear();
                foreach (var routeVm in ReuseRoutes)
                {
                    if (string.IsNullOrWhiteSpace(routeVm.IncomingNodeId) ||
                        string.IsNullOrWhiteSpace(routeVm.SelectedOutgoingNodeId))
                    {
                        continue;
                    }

                    var lineStyleKey = routeVm.SelectedLineStyleKey;
                    if (string.Equals(lineStyleKey, "WorkflowDefault", StringComparison.OrdinalIgnoreCase))
                    {
                        lineStyleKey = null; // null => follow workflow default
                    }

                    _node.ReuseRoutes.Add(new NodeReuseRoute
                    {
                        IncomingNodeId = routeVm.IncomingNodeId,
                        OutgoingNodeId = routeVm.SelectedOutgoingNodeId,
                        LineStyleKey = lineStyleKey
                    });
                }

                reuseChanged = true;
            }

            if (hasChanges)
            {
                if (_node.TitleTextBlockUI != null)
                {
                    _node.TitleTextBlockUI.Text = NodeTitle;
                    // Update title color
                    UpdateTitleColor(_node.TitleTextBlockUI);
                }
                _host.RequestSyncDataPanels(immediate: true);
            }

            // Nếu ReuseRoutes thay đổi (tab "Tái sử dụng flow"), cần cập nhật lại toàn bộ path connection
            // để style line mới được áp dụng ngay sau khi đóng dialog.
            if (reuseChanged && _host.ViewModel != null)
            {
                try
                {
                    // Dùng ConnectionRenderer từ host để update lại path cho toàn bộ connections,
                    // logic GetLineStyleForConnection sẽ đọc ReuseRoutes mới và áp dụng style ngay lập tức.
                    var connections = _host.ViewModel.Connections;
                    if (connections != null && connections.Count > 0)
                    {
                        _host.ConnectionRenderer.UpdateAllConnectionPaths(connections);
                        _host.ConnectionRenderer.UpdateAllConnectionAnimations(connections);
                    }
                }
                catch
                {
                    // best-effort, không để lỗi propagate ra ngoài dialog
                }
            }

            // Lưu cấu hình vị trí cổng IN/OUT (nếu node có Ports)
            SavePortPositions();

            // Gọi OnSaveTitle để derived classes có thể thêm logic riêng
            OnSaveTitle();
        }

        /// <summary>
        /// Lưu cấu hình vị trí cổng IN/OUT dựa trên InputPortPosition/OutputPortPosition.
        /// Áp dụng chung cho hầu hết các node có 1 cổng IN và 1 cổng OUT.
        /// </summary>
        protected virtual void SavePortPositions()
        {
            if (_node.Ports == null || _node.Ports.Count == 0)
                return;

            var inputPort = _node.Ports.FirstOrDefault(p => p.IsInput);
            var outputPort = _node.Ports.FirstOrDefault(p => !p.IsInput);

            bool portsChanged = false;

            if (inputPort != null && inputPort.Position != InputPortPosition)
            {
                inputPort.Position = InputPortPosition;
                portsChanged = true;
            }

            if (outputPort != null && outputPort.Position != OutputPortPosition)
            {
                outputPort.Position = OutputPortPosition;
                portsChanged = true;
            }

            if (!portsChanged)
                return;

            // Cập nhật lại vị trí port trên canvas
            if (inputPort != null)
            {
                _host.UpdatePortsPositionOnSide(_node, inputPort.Position);
            }

            if (outputPort != null && (inputPort == null || outputPort.Position != inputPort.Position))
            {
                _host.UpdatePortsPositionOnSide(_node, outputPort.Position);
            }

            // Cập nhật lại toàn bộ connections để line bám theo vị trí port mới
            var cons = _host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    _host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                    _host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                }
                catch
                {
                    // best-effort, không để lỗi propagate ra ngoài dialog
                }
            }
        }

        /// <summary>
        /// Update title color based on TitleColorMode and TitleColorKey.
        /// </summary>
        private void UpdateTitleColor(System.Windows.Controls.TextBlock titleTextBlock)
        {
            if (TitleColorMode == TitleColorMode.NodeColor)
            {
                titleTextBlock.Foreground = _node.NodeBrush;
            }
            else if (!string.IsNullOrEmpty(TitleColorKey))
            {
                var brush = System.Windows.Application.Current.TryFindResource(TitleColorKey) as System.Windows.Media.Brush;
                if (brush != null)
                {
                    titleTextBlock.Foreground = brush;
                }
                else if (TitleColorKey == "LimeGreen")
                {
                    titleTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
                }
            }
        }

        /// <summary>
        /// Override để thêm logic save các properties khác.
        /// </summary>
        protected virtual void OnSaveTitle() { }

        /// <summary>
        /// Helper method để lấy TitleDisplayMode từ node nếu có.
        /// </summary>
        private static TitleDisplayMode GetTitleDisplayMode(WorkflowNode node)
            => node.TitleDisplayMode;

        /// <summary>
        /// Helper method để set TitleDisplayMode cho node nếu có.
        /// </summary>
        private static void SetTitleDisplayMode(WorkflowNode node, TitleDisplayMode value)
            => node.TitleDisplayMode = value;
        /// <summary>
        /// Helper method để lấy TitleColorMode từ node nếu có.
        /// </summary>
        private static TitleColorMode GetTitleColorMode(WorkflowNode node)
            => node.TitleColorMode;

        /// <summary>
        /// Helper method để set TitleColorMode cho node nếu có.
        /// </summary>
        private static void SetTitleColorMode(WorkflowNode node, TitleColorMode value)
            => node.TitleColorMode = value;

        /// <summary>
        /// Helper method để lấy TitleColorKey từ node nếu có.
        /// </summary>
        private static string? GetTitleColorKey(WorkflowNode node)
            => node.TitleColorKey;

        /// <summary>
        /// Helper method để set TitleColorKey cho node nếu có.
        /// </summary>
        private static void SetTitleColorKey(WorkflowNode node, string? value)
            => node.TitleColorKey = value;
        // ===== Shared helpers cho tạo WorkflowDataSourceOption với đầy đủ icon/brush =====

        /// <summary>
        /// Tạo WorkflowDataSourceOption có đầy đủ Icon, NodeBrush, NodeTextBrush... từ WorkflowNode.
        /// Tất cả dialog VMs nên dùng method này thay vì tạo option chỉ có NodeId + Title.
        /// </summary>
        public static WorkflowDataSourceOption CreateDataSourceOption(WorkflowNode n)
        {
            return new WorkflowDataSourceOption
            {
                NodeId = n.Id,
                Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title,
                NodeType = n.Type,
                NodeTypeDisplayName = ResolveNodeTypeDisplayName(n.Type),
                IconKey = ResolveNodeIconKey(n.Type),
                NodeBrush = ResolveNodeStateBrush(n.ColorKey, "Brush", n.NodeBrush),
                NodeTextBrush = ResolveTextOnNodeBrush(n.ColorKey),
                NodeHoverBrush = ResolveNodeStateBrush(n.ColorKey, "HoverBrush", n.NodeBrush),
                NodeSelectedBrush = ResolveNodeStateBrush(n.ColorKey, "PressedBrush", n.NodeBrush)
            };
        }

        /// <summary>
        /// Clone option để mỗi ComboBox giữ instance riêng, tránh side-effect binding.
        /// </summary>
        public static WorkflowDataSourceOption CreateDataSourceOption_Clone(WorkflowDataSourceOption source)
        {
            return new WorkflowDataSourceOption
            {
                NodeId = source.NodeId,
                Title = source.Title,
                NodeType = source.NodeType,
                NodeTypeDisplayName = source.NodeTypeDisplayName,
                IconKey = source.IconKey,
                NodeBrush = source.NodeBrush,
                NodeTextBrush = source.NodeTextBrush,
                NodeHoverBrush = source.NodeHoverBrush,
                NodeSelectedBrush = source.NodeSelectedBrush
            };
        }

        protected static string ResolveNodeTypeDisplayName(NodeType type)
        {
            return type switch
            {
                NodeType.IfElse => "Conditional",
                NodeType.KeyPressEvent => "Key Press Event",
                NodeType.HotkeyPressEvent => "Hotkey Press Event",
                NodeType.MouseEvent => "Mouse Event",
                NodeType.StringSplit => "String Split",
                NodeType.AssignData => "Assign Data",
                NodeType.MediaGallery => "Media Gallery",
                NodeType.ImageProcessing => "Image Processing",
                NodeType.VideoProcessing => "Video Processing",
                NodeType.HttpRequest => "HTTP Request",
                NodeType.FileDownload => "File Download",
                NodeType.DataFetcher => "Data Fetcher",
                NodeType.KeyValueBridge => "Key Value Bridge",
                NodeType.FlowOverwrite => "Flow Overwrite",
                NodeType.FolderFilePaths => "Folder File Paths",
                NodeType.AsyncTaskDispatchCollect => "Async Dispatch Collect",
                NodeType.KeyScopedStore => "Key Scoped Store",
                NodeType.BodyContainer => "Body Container",
                _ => type.ToString()
            };
        }

        protected static string ResolveNodeIconKey(NodeType type)
        {
            return type switch
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
                NodeType.VideoProcessing => "circle-video sharp-light",
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
                NodeType.BodyContainer => "border-none sharp-duotone-regular",
                NodeType.Notification => "message-captions duotone-regular",
                NodeType.Storage => "arrow-progress sharp-regular",
                NodeType.Callback => "arrows-turn-right regular",
                NodeType.FileDownload => "download solid",
                NodeType.AsyncTaskDispatchCollect => "list-radio regular",
                NodeType.KeyScopedStore => "arrow-progress sharp-regular",
                NodeType.LoopContext => "arrows-spin duotone",
                NodeType.Condition => "list-tree sharp-light",
                NodeType.GitSource => "git-alt brands",
                _ => "circle-question chisel-regular"
            };
        }

        protected static System.Windows.Media.Brush ResolveTextOnNodeBrush(string? nodeColorKey)
        {
            var app = System.Windows.Application.Current;
            if (app != null && !string.IsNullOrWhiteSpace(nodeColorKey))
            {
                var clean = nodeColorKey.Trim();
                if (clean.EndsWith("Brush", StringComparison.OrdinalIgnoreCase))
                    clean = clean[..^"Brush".Length];

                if (app.TryFindResource($"TextOn{clean}Brush") is System.Windows.Media.Brush textOnBrush) return textOnBrush;
                if (app.TryFindResource($"TextOn{clean}") is System.Windows.Media.Brush textOnKey) return textOnKey;
            }

            return app?.TryFindResource("TextOnPrimaryBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White;
        }

        protected static System.Windows.Media.Brush ResolveNodeStateBrush(string? nodeColorKey, string suffix, System.Windows.Media.Brush? fallback)
        {
            var app = System.Windows.Application.Current;
            var cleaned = NormalizeColorKeyForOption(nodeColorKey);
            if (app != null && !string.IsNullOrWhiteSpace(cleaned))
            {
                if (app.TryFindResource($"{cleaned}{suffix}") is System.Windows.Media.Brush exact) return exact;
                if (suffix != "Brush" && app.TryFindResource($"{cleaned}Brush") is System.Windows.Media.Brush baseBrush) return baseBrush;
            }

            return fallback
                ?? app?.TryFindResource("SecondaryBrush") as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.Gray;
        }

        private static string NormalizeColorKeyForOption(string? nodeColorKey)
        {
            var cleaned = (nodeColorKey ?? string.Empty).Trim();
            if (cleaned.EndsWith("Brush", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[..^"Brush".Length];
            return cleaned;
        }

        // ===== Shared helpers — output key / node list helpers =====

        /// <summary>
        /// Lấy danh sách output key của một node theo nodeId.
        /// Dùng chung cho tất cả dialog VMs — KHÔNG tự viết lại trong derived class.
        /// </summary>
        public ObservableCollection<WorkflowOutputKeyOption> GetOutputKeysForNode(string? nodeId)
        {
            var list = new ObservableCollection<WorkflowOutputKeyOption>();
            if (string.IsNullOrWhiteSpace(nodeId) || _host.ViewModel?.Nodes == null) return list;

            var node = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node?.DynamicOutputs == null) return list;

            foreach (var o in node.DynamicOutputs)
            {
                list.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key ?? string.Empty,
                    Type = o.OutputType ?? o.ConvertType,
                    DisplayName = o.DisplayName ?? o.Key
                });
            }
            return list;
        }

        /// <summary>
        /// Điền danh sách output key của một node vào collection target.
        /// Dùng chung cho tất cả dialog VMs — KHÔNG tự viết lại trong derived class.
        /// </summary>
        protected void FillOutputKeys(string? nodeId, ObservableCollection<WorkflowOutputKeyOption> target)
        {
            target.Clear();
            if (string.IsNullOrWhiteSpace(nodeId) || _host.ViewModel?.Nodes == null) return;

            var src = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (src?.DynamicOutputs == null) return;

            foreach (var o in src.DynamicOutputs)
            {
                var key = o.Key ?? string.Empty;
                target.Add(new WorkflowOutputKeyOption
                {
                    Key = key,
                    DisplayName = o.DisplayName ?? key,
                    Type = o.OutputType ?? o.ConvertType
                });
            }
        }

        /// <summary>
        /// Làm mới danh sách tất cả nodes có DynamicOutputs vào collection target.
        /// Loại bỏ chính node hiện tại (_node). Dùng chung cho dialog VMs cần combobox chọn node nguồn.
        /// KHÔNG dùng cho các node cần filter đặc biệt (WebNode, HtmlUiNode, StorageNode).
        /// </summary>
        protected void RefreshAllNodesWithOutputs(ObservableCollection<WorkflowDataSourceOption> target)
        {
            target.Clear();
            if (_host.ViewModel?.Nodes == null) return;

            foreach (var n in _host.ViewModel.Nodes)
            {
                if (ReferenceEquals(n, _node)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                target.Add(CreateDataSourceOption(n));
            }
        }
    }
}

