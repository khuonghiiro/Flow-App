using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

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
        {
            switch (node)
            {
                case Models.Nodes.StringSplitNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.HttpRequestNode n: n.TitleColorMode = mode; break;
                case MouseEventNode n: n.TitleColorMode = mode; break;
                case LoopNode n: n.TitleColorMode = mode; break;
                case InputNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.OutputNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.ListOutNode n: n.TitleColorMode = mode; break;
                case HotkeyPressEventNode n: n.TitleColorMode = mode; break;
                case KeyPressEventNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.AssignDataNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.MediaGalleryNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.ImageProcessingNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.CodeNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.FolderNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.FileDownloadNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.FolderFilePathsNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.WebNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.HtmlUiNode n: n.TitleColorMode = mode; break;
                case Models.Nodes.StorageNode n: n.TitleColorMode = mode; break;
                case DelayNode n: n.TitleColorMode = mode; break;
            }
        }

        /// <summary>
        /// Set TitleColorKey trực tiếp cho các node types cụ thể.
        /// </summary>
        private static void SetTitleColorKeyDirectly(WorkflowNode node, string? key)
        {
            switch (node)
            {
                case Models.Nodes.StringSplitNode n: n.TitleColorKey = key; break;
                case Models.Nodes.HttpRequestNode n: n.TitleColorKey = key; break;
                case MouseEventNode n: n.TitleColorKey = key; break;
                case LoopNode n: n.TitleColorKey = key; break;
                case InputNode n: n.TitleColorKey = key; break;
                case Models.Nodes.OutputNode n: n.TitleColorKey = key; break;
                case Models.Nodes.ListOutNode n: n.TitleColorKey = key; break;
                case HotkeyPressEventNode n: n.TitleColorKey = key; break;
                case KeyPressEventNode n: n.TitleColorKey = key; break;
                case Models.Nodes.AssignDataNode n: n.TitleColorKey = key; break;
                case Models.Nodes.MediaGalleryNode n: n.TitleColorKey = key; break;
                case Models.Nodes.CodeNode n: n.TitleColorKey = key; break;
                case Models.Nodes.FolderNode n: n.TitleColorKey = key; break;
                case Models.Nodes.FileDownloadNode n: n.TitleColorKey = key; break;
                case Models.Nodes.FolderFilePathsNode n: n.TitleColorKey = key; break;
                case Models.Nodes.WebNode n: n.TitleColorKey = key; break;
                case DelayNode n: n.TitleColorKey = key; break;
                case Models.Nodes.StorageNode n: n.TitleColorKey = key; break;
                case Models.Nodes.ImageProcessingNode n: n.TitleColorKey = key; break;
            }
        }

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
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
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
                .Select(n => new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                })
                .ToList();

            // Mỗi input một list riêng (cùng nội dung) — tránh dùng chung reference List khiến WPF/binding đồng bộ lệch giữa các ComboBox.
            foreach (var input in _node.DynamicInputs)
            {
                input.AvailableSources = options
                    .Select(o => new WorkflowDataSourceOption { NodeId = o.NodeId, Title = o.Title })
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
                .Select(n => new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                })
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
        {
            // Kiểm tra các node types cụ thể có TitleDisplayMode
            if (node is KeyPressEventNode keyNode)
                return keyNode.TitleDisplayMode;
            if (node is MouseEventNode mouseNode)
                return mouseNode.TitleDisplayMode;
            if (node is LoopNode loopNode)
                return loopNode.TitleDisplayMode;
            if (node is InputNode inputNode)
                return inputNode.TitleDisplayMode;
            if (node is HotkeyPressEventNode hotkeyNode)
                return hotkeyNode.TitleDisplayMode;
            if (node is ListOutNode listOutNode)
                return listOutNode.TitleDisplayMode;
            if (node is Models.Nodes.WebNode webNode)
                return webNode.TitleDisplayMode;
            if (node is Models.Nodes.HtmlUiNode htmlUiNode)
                return htmlUiNode.TitleDisplayMode;
            if (node is Models.Nodes.StorageNode storageNode)
                return storageNode.TitleDisplayMode;

            // Fallback: sử dụng reflection để kiểm tra property
            var property = node.GetType().GetProperty("TitleDisplayMode", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(TitleDisplayMode))
            {
                var value = property.GetValue(node);
                if (value is TitleDisplayMode mode)
                    return mode;
            }

            // Default value nếu node không hỗ trợ TitleDisplayMode
            return TitleDisplayMode.Always;
        }

        /// <summary>
        /// Helper method để set TitleDisplayMode cho node nếu có.
        /// </summary>
        private static void SetTitleDisplayMode(WorkflowNode node, TitleDisplayMode value)
        {
            // Kiểm tra các node types cụ thể có TitleDisplayMode
            if (node is KeyPressEventNode keyNode)
            {
                keyNode.TitleDisplayMode = value;
                return;
            }
            if (node is MouseEventNode mouseNode)
            {
                mouseNode.TitleDisplayMode = value;
                return;
            }
            if (node is LoopNode loopNode)
            {
                loopNode.TitleDisplayMode = value;
                return;
            }
            if (node is InputNode inputNode)
            {
                inputNode.TitleDisplayMode = value;
                return;
            }
            if (node is HotkeyPressEventNode hotkeyNode)
            {
                hotkeyNode.TitleDisplayMode = value;
                return;
            }
            if (node is ListOutNode listOutNode)
            {
                listOutNode.TitleDisplayMode = value;
                return;
            }
            if (node is Models.Nodes.WebNode webNode)
            {
                webNode.TitleDisplayMode = value;
                return;
            }
            if (node is Models.Nodes.HtmlUiNode htmlUiNode)
            {
                htmlUiNode.TitleDisplayMode = value;
                return;
            }

            // Fallback: sử dụng reflection để set property
            var property = node.GetType().GetProperty("TitleDisplayMode", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(TitleDisplayMode) && property.CanWrite)
            {
                property.SetValue(node, value);
            }
        }

        /// <summary>
        /// Helper method để lấy TitleColorMode từ node nếu có.
        /// </summary>
        private static TitleColorMode GetTitleColorMode(WorkflowNode node)
        {
            // Fallback: sử dụng reflection để kiểm tra property
            var property = node.GetType().GetProperty("TitleColorMode", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(TitleColorMode))
            {
                var value = property.GetValue(node);
                if (value is TitleColorMode mode)
                    return mode;
            }

            // Default value nếu node không hỗ trợ TitleColorMode
            return TitleColorMode.NodeColor;
        }

        /// <summary>
        /// Helper method để set TitleColorMode cho node nếu có.
        /// </summary>
        private static void SetTitleColorMode(WorkflowNode node, TitleColorMode value)
        {
            var property = node.GetType().GetProperty("TitleColorMode", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(TitleColorMode) && property.CanWrite)
            {
                property.SetValue(node, value);
            }
        }

        /// <summary>
        /// Helper method để lấy TitleColorKey từ node nếu có.
        /// </summary>
        private static string? GetTitleColorKey(WorkflowNode node)
        {
            var property = node.GetType().GetProperty("TitleColorKey", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(string))
            {
                return property.GetValue(node) as string;
            }
            return null;
        }

        /// <summary>
        /// Helper method để set TitleColorKey cho node nếu có.
        /// </summary>
        private static void SetTitleColorKey(WorkflowNode node, string? value)
        {
            var property = node.GetType().GetProperty("TitleColorKey", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(string) && property.CanWrite)
            {
                property.SetValue(node, value);
            }
        }
    }
}

