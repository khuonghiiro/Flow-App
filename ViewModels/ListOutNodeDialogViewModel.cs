using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// ViewModel cho một dòng output mapping trong dialog.
    /// </summary>
    public partial class OutputMappingItemViewModel : ObservableObject
    {
        private readonly ListOutNode _node;
        private readonly IWorkflowEditorHost _host;
        private readonly OutputMapping _mapping;
        private readonly Action _onRemove;
        private readonly Action _onMappingChanged;

        [ObservableProperty]
        private string _newKey = string.Empty;

        [ObservableProperty]
        private string _selectedSourceNodeId = string.Empty;

        [ObservableProperty]
        private string _selectedSourceOutputKey = string.Empty;

        [ObservableProperty]
        private ObservableCollection<WorkflowDataSourceOption> _availableSourceNodes = new();

        [ObservableProperty]
        private ObservableCollection<WorkflowOutputKeyOption> _availableOutputKeys = new();

        public OutputMappingItemViewModel(
            ListOutNode node,
            OutputMapping mapping,
            IWorkflowEditorHost host,
            Action onRemove,
            Action onMappingChanged)
        {
            _node = node;
            _mapping = mapping;
            _host = host;
            _onRemove = onRemove;
            _onMappingChanged = onMappingChanged;

            // Load initial values
            _newKey = mapping.NewKey;
            _selectedSourceNodeId = mapping.SourceNodeId;
            _selectedSourceOutputKey = mapping.SourceOutputKey;

            // Load available source nodes
            RefreshAvailableSources();

            // Load output keys if source is already selected
            if (!string.IsNullOrEmpty(_selectedSourceNodeId))
            {
                RefreshAvailableOutputKeys();
            }
        }

        /// <summary>
        /// Refresh available source nodes (upstream nodes with DynamicOutputs).
        /// </summary>
        public void RefreshAvailableSources()
        {
            AvailableSourceNodes.Clear();

            if (_host.ViewModel == null) return;

            var vm = _host.ViewModel;
            var connections = vm.Connections;
            if (connections == null || connections.Count == 0) return;

            // Collect all upstream nodes of _node
            var upstream = new HashSet<WorkflowNode>();
            var stack = new Stack<WorkflowNode>();
            stack.Push(_node);

            var parentLoops = new HashSet<LoopNode>();

            while (stack.Count > 0)
            {
                var current = stack.Pop();

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

            // Filter only nodes with DynamicOutputs
            var producerNodes = upstream
                .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                .Where(n => !ReferenceEquals(n, _node))
                .ToList();

            foreach (var loop in parentLoops)
            {
                if (loop.DynamicOutputs != null && loop.DynamicOutputs.Count > 0 && !producerNodes.Contains(loop))
                {
                    producerNodes.Add(loop);
                }
            }

            // Create options
            foreach (var n in producerNodes)
            {
                AvailableSourceNodes.Add(BaseNodeDialogViewModel.CreateDataSourceOption(n));
            }
        }

        /// <summary>
        /// Refresh available output keys from selected source node.
        /// </summary>
        public void RefreshAvailableOutputKeys()
        {
            AvailableOutputKeys.Clear();

            if (string.IsNullOrEmpty(SelectedSourceNodeId) || _host.ViewModel == null) return;

            // Find source node
            var sourceNode = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == SelectedSourceNodeId);
            if (sourceNode == null || sourceNode.DynamicOutputs == null) return;

            foreach (var output in sourceNode.DynamicOutputs)
            {
                AvailableOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        partial void OnSelectedSourceNodeIdChanged(string value)
        {
            // When source node changes, refresh output keys
            RefreshAvailableOutputKeys();

            // Clear selected output key if it's not valid anymore
            if (!AvailableOutputKeys.Any(k => k.Key == SelectedSourceOutputKey))
            {
                SelectedSourceOutputKey = AvailableOutputKeys.FirstOrDefault()?.Key ?? string.Empty;
            }

            // Sync to mapping
            _mapping.SourceNodeId = value;
            _onMappingChanged?.Invoke();
        }

        partial void OnSelectedSourceOutputKeyChanged(string value)
        {
            // Auto-fill NewKey if empty
            if (string.IsNullOrWhiteSpace(NewKey) && !string.IsNullOrWhiteSpace(value))
            {
                NewKey = value;
            }

            // Sync to mapping
            _mapping.SourceOutputKey = value;
            _onMappingChanged?.Invoke();
        }

        partial void OnNewKeyChanged(string value)
        {
            // Sync to mapping
            _mapping.NewKey = value;
            _onMappingChanged?.Invoke();
        }

        [RelayCommand]
        private void Remove()
        {
            _onRemove?.Invoke();
        }
    }

    /// <summary>
    /// ViewModel cho ListOutNodeDialog.
    /// </summary>
    public partial class ListOutNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly ListOutNode _listOutNode;

        public ObservableCollection<OutputMappingItemViewModel> Mappings { get; } = new();

        public ListOutNodeDialogViewModel(ListOutNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _listOutNode = node ?? throw new ArgumentNullException(nameof(node));

            // Load existing mappings
            LoadMappings();

            // Sync additional properties
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ListOutNode.OutputMappings))
                    {
                        LoadMappings();
                    }
                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }
        }

        protected override string GetDefaultTitle() => "List Out";

        /// <summary>
        /// Load mappings from node.
        /// </summary>
        private void LoadMappings()
        {
            Mappings.Clear();

            foreach (var mapping in _listOutNode.OutputMappings)
            {
                var itemVm = new OutputMappingItemViewModel(
                    _listOutNode,
                    mapping,
                    _host,
                    onRemove: () => RemoveMapping(mapping),
                    onMappingChanged: OnMappingChanged
                );
                Mappings.Add(itemVm);
            }
        }

        /// <summary>
        /// Called when any mapping changes - update node's DynamicOutputs.
        /// </summary>
        private void OnMappingChanged()
        {
            _listOutNode.RebuildDynamicOutputs();
            
            // ✅ Nếu ListOutNode nằm trong LoopBody, rebuild outputs của parent LoopNode
            RebuildParentLoopNodeOutputs();
            
            _host.RequestSyncDataPanels(immediate: true);
        }

        /// <summary>
        /// Tìm parent LoopNode và rebuild outputs từ ListOutNodes trong LoopBody.
        /// </summary>
        private void RebuildParentLoopNodeOutputs()
        {
            if (_host.ViewModel == null) return;

            var vm = _host.ViewModel;
            var connections = vm.Connections?.ToList();
            if (connections == null) return;

            // Tìm parent LoopNode chứa ListOutNode này
            var parentLoopNode = FindParentLoopNode(_listOutNode, vm.Nodes, connections);
            if (parentLoopNode != null)
            {
                parentLoopNode.RebuildOutputsFromLoopBody(connections, vm.Nodes);
            }
        }

        /// <summary>
        /// Tìm LoopNode cha chứa node này (nếu node nằm trong LoopBody).
        /// </summary>
        private LoopNode? FindParentLoopNode(WorkflowNode node, System.Collections.Generic.IEnumerable<WorkflowNode> allNodes, System.Collections.Generic.List<WorkflowConnection> connections)
        {
            // Tìm tất cả LoopNodes
            var loopNodes = allNodes.OfType<LoopNode>().ToList();

            foreach (var loopNode in loopNodes)
            {
                var body = loopNode.LoopBodyNode;
                if (body == null) continue;

                // Kiểm tra xem node có nằm trong LoopBody cluster không
                var visited = new System.Collections.Generic.HashSet<WorkflowNode> { body };
                var queue = new System.Collections.Generic.Queue<WorkflowNode>();
                queue.Enqueue(body);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();

                    // Nếu tìm thấy node trong cluster, return parent LoopNode
                    if (ReferenceEquals(current, node))
                    {
                        return loopNode;
                    }

                    var neighbors = connections
                        .Where(c => c.FromNode == current || c.ToNode == current)
                        .Select(c => c.FromNode == current ? c.ToNode : c.FromNode)
                        .Where(n => n != null);

                    foreach (var neighbor in neighbors)
                    {
                        // Bỏ qua LoopNode cha để không lan ra ngoài
                        if (ReferenceEquals(neighbor, loopNode)) continue;

                        if (visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Remove a mapping.
        /// </summary>
        private void RemoveMapping(OutputMapping mapping)
        {
            var index = _listOutNode.OutputMappings.IndexOf(mapping);
            if (index >= 0)
            {
                _listOutNode.RemoveMapping(index);
                LoadMappings(); // Reload UI
                
                // ✅ Nếu ListOutNode nằm trong LoopBody, rebuild outputs của parent LoopNode
                RebuildParentLoopNodeOutputs();
                
                _host.RequestSyncDataPanels(immediate: true);
            }
        }

        /// <summary>
        /// Add new empty mapping row.
        /// </summary>
        [RelayCommand]
        private void AddMapping()
        {
            var newMapping = new OutputMapping
            {
                NewKey = $"output{_listOutNode.OutputMappings.Count + 1}",
                SourceNodeId = string.Empty,
                SourceOutputKey = string.Empty
            };

            _listOutNode.OutputMappings.Add(newMapping);

            var itemVm = new OutputMappingItemViewModel(
                _listOutNode,
                newMapping,
                _host,
                onRemove: () => RemoveMapping(newMapping),
                onMappingChanged: OnMappingChanged
            );
            Mappings.Add(itemVm);

            _listOutNode.RebuildDynamicOutputs();
            
            // ✅ Nếu ListOutNode nằm trong LoopBody, rebuild outputs của parent LoopNode
            RebuildParentLoopNodeOutputs();
            
            _host.RequestSyncDataPanels(immediate: true);
        }

        protected override void OnSaveTitle()
        {
            _listOutNode.NotifyTitleChanged();

            // Ensure DynamicOutputs are up to date
            _listOutNode.RebuildDynamicOutputs();
            
            // ✅ Nếu ListOutNode nằm trong LoopBody, rebuild outputs của parent LoopNode
            RebuildParentLoopNodeOutputs();
            
            _host.RequestSyncDataPanels(immediate: true);
        }
    }
}

