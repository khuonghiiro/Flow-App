using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// ViewModel cho một dòng input variable trong dialog.
    /// </summary>
    public partial class InputVariableItemViewModel : ObservableObject
    {
        private readonly OutputNode _node;
        private readonly IWorkflowEditorHost _host;
        private readonly InputVariable _variable;
        private readonly Action _onRemove;
        private readonly Action _onVariableChanged;

        [ObservableProperty]
        private string _variableKey = string.Empty;

        [ObservableProperty]
        private string _selectedSourceNodeId = string.Empty;

        [ObservableProperty]
        private string _selectedSourceOutputKey = string.Empty;

        [ObservableProperty]
        private ObservableCollection<WorkflowDataSourceOption> _availableSourceNodes = new();

        [ObservableProperty]
        private ObservableCollection<WorkflowOutputKeyOption> _availableOutputKeys = new();

        public InputVariableItemViewModel(
            OutputNode node,
            InputVariable variable,
            IWorkflowEditorHost host,
            Action onRemove,
            Action onVariableChanged)
        {
            _node = node;
            _variable = variable;
            _host = host;
            _onRemove = onRemove;
            _onVariableChanged = onVariableChanged;

            // Load initial values
            _variableKey = variable.VariableKey;
            _selectedSourceNodeId = variable.SourceNodeId;
            _selectedSourceOutputKey = variable.SourceOutputKey;

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
            var upstream = new System.Collections.Generic.HashSet<WorkflowNode>();
            var stack = new System.Collections.Generic.Stack<WorkflowNode>();
            stack.Push(_node);

            var parentLoops = new System.Collections.Generic.HashSet<LoopNode>();

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
                AvailableSourceNodes.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
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

            // Sync to variable
            _variable.SourceNodeId = value;
            _onVariableChanged?.Invoke();
        }

        partial void OnSelectedSourceOutputKeyChanged(string value)
        {
            // Sync to variable
            _variable.SourceOutputKey = value;
            _onVariableChanged?.Invoke();
        }

        partial void OnVariableKeyChanged(string value)
        {
            // Sync to variable
            _variable.VariableKey = value;
            _onVariableChanged?.Invoke();
        }

        [RelayCommand]
        private void Remove()
        {
            _onRemove?.Invoke();
        }
    }

    /// <summary>
    /// ViewModel cho OutputNodeDialog.
    /// </summary>
    public partial class OutputNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly OutputNode _outputNode;

        [ObservableProperty]
        private string _outputKey = "output";

        [ObservableProperty]
        private string _formatString = string.Empty;

        public ObservableCollection<InputVariableItemViewModel> Variables { get; } = new();

        [ObservableProperty]
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;

        public OutputNodeDialogViewModel(OutputNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _outputNode = node ?? throw new ArgumentNullException(nameof(node));

            // Load initial values
            OutputKey = _outputNode.OutputKey;
            FormatString = _outputNode.FormatString;
            TitleDisplayMode = _outputNode.TitleDisplayMode;

            // Load existing variables
            LoadVariables();

            // Sync additional properties
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(OutputNode.InputVariables))
                    {
                        LoadVariables();
                    }
                    else if (e.PropertyName == nameof(OutputNode.OutputKey))
                    {
                        OutputKey = _outputNode.OutputKey;
                    }
                    else if (e.PropertyName == nameof(OutputNode.FormatString))
                    {
                        FormatString = _outputNode.FormatString;
                    }
                    else if (e.PropertyName == nameof(OutputNode.TitleDisplayMode))
                    {
                        TitleDisplayMode = _outputNode.TitleDisplayMode;
                    }
                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }
        }

        protected override string GetDefaultTitle() => "Output";

        /// <summary>
        /// Load variables from node.
        /// </summary>
        private void LoadVariables()
        {
            Variables.Clear();

            foreach (var variable in _outputNode.InputVariables)
            {
                var itemVm = new InputVariableItemViewModel(
                    _outputNode,
                    variable,
                    _host,
                    onRemove: () => RemoveVariable(variable),
                    onVariableChanged: OnVariableChanged
                );
                Variables.Add(itemVm);
            }
        }

        /// <summary>
        /// Called when any variable changes - update format string preview.
        /// </summary>
        private void OnVariableChanged()
        {
            // Sync to node
            _outputNode.RebuildDynamicOutputs();
            _host.RequestSyncDataPanels(immediate: true);
        }

        /// <summary>
        /// Remove a variable.
        /// </summary>
        private void RemoveVariable(InputVariable variable)
        {
            var index = _outputNode.InputVariables.IndexOf(variable);
            if (index >= 0)
            {
                _outputNode.RemoveInputVariable(index);
                LoadVariables(); // Reload UI
                _host.RequestSyncDataPanels(immediate: true);
            }
        }

        /// <summary>
        /// Add new empty variable row.
        /// </summary>
        [RelayCommand]
        private void AddVariable()
        {
            var newVariable = new InputVariable
            {
                VariableKey = $"input{_outputNode.InputVariables.Count + 1}",
                SourceNodeId = string.Empty,
                SourceOutputKey = string.Empty
            };

            _outputNode.InputVariables.Add(newVariable);

            var itemVm = new InputVariableItemViewModel(
                _outputNode,
                newVariable,
                _host,
                onRemove: () => RemoveVariable(newVariable),
                onVariableChanged: OnVariableChanged
            );
            Variables.Add(itemVm);

            _outputNode.RebuildDynamicOutputs();
            _host.RequestSyncDataPanels(immediate: true);
        }

        partial void OnOutputKeyChanged(string value)
        {
            if (_outputNode.OutputKey != value)
            {
                _outputNode.OutputKey = value;
                _outputNode.RebuildDynamicOutputs();
                _host.RequestSyncDataPanels(immediate: true);
            }
        }

        partial void OnFormatStringChanged(string value)
        {
            if (_outputNode.FormatString != value)
            {
                _outputNode.FormatString = value;
                _host.RequestSyncDataPanels(immediate: true);
            }
        }

        partial void OnTitleDisplayModeChanged(TitleDisplayMode value)
        {
            if (_outputNode.TitleDisplayMode != value)
            {
                _outputNode.TitleDisplayMode = value;
            }
        }

        protected override void OnSaveTitle()
        {
            _outputNode.NotifyTitleChanged();

            // Ensure DynamicOutputs are up to date
            _outputNode.RebuildDynamicOutputs();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }
}
