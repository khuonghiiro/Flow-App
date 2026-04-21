using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace FlowMy.ViewModels
{
    public partial class CallbackNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly CallbackNode _callbackNode;

        // ===== OBSERVABLE PROPERTIES =====
        
        [ObservableProperty]
        private string _targetNodeId = string.Empty;
        
        [ObservableProperty]
        private int _maxCallbackCount = 3;

        [ObservableProperty]
        private CallbackFlowBehavior _flowBehavior = CallbackFlowBehavior.JumpOnly;

        partial void OnFlowBehaviorChanged(CallbackFlowBehavior value)
        {
            if (_callbackNode.FlowBehavior != value)
            {
                _callbackNode.FlowBehavior = value;
            }
            else
            {
                _callbackNode.SyncPortsForBehavior();
            }

            RefreshCallbackPortsVisual();
        }

        // ===== OPTIONS =====
        
        public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
        {
            new TitleDisplayModeOption(TitleDisplayMode.Hidden, "Ẩn tiêu đề"),
            new TitleDisplayModeOption(TitleDisplayMode.Hover, "Hiện khi hover"),
            new TitleDisplayModeOption(TitleDisplayMode.Always, "Luôn hiện")
        };

        /// <summary>
        /// Danh sách các node có thể callback (tất cả node trừ Start, End, và chính node này)
        /// </summary>
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodes { get; } = new();
        public ObservableCollection<CallbackFlowBehaviorOption> FlowBehaviorOptions { get; } = new()
        {
            new CallbackFlowBehaviorOption(CallbackFlowBehavior.JumpOnly, "Chỉ callback (Jump only)"),
            new CallbackFlowBehaviorOption(CallbackFlowBehavior.JumpThenContinue, "Callback rồi chạy tiếp flow (Jump then continue)")
        };

        // ===== CONSTRUCTOR =====
        
        public CallbackNodeDialogViewModel(CallbackNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _callbackNode = node ?? throw new ArgumentNullException(nameof(node));

            // Sync properties từ node sang ViewModel
            _callbackNode.EnsurePorts();
            _callbackNode.SyncPortsForBehavior();
            TargetNodeId = _callbackNode.TargetNodeId;
            MaxCallbackCount = _callbackNode.MaxCallbackCount;
            FlowBehavior = _callbackNode.FlowBehavior;

            // Load available nodes
            RefreshAvailableNodes();

            // Subscribe PropertyChanged nếu node implement INotifyPropertyChanged
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(CallbackNode.TargetNodeId))
                        TargetNodeId = _callbackNode.TargetNodeId;
                    else if (e.PropertyName == nameof(CallbackNode.MaxCallbackCount))
                        MaxCallbackCount = _callbackNode.MaxCallbackCount;
                    else if (e.PropertyName == nameof(CallbackNode.FlowBehavior))
                        FlowBehavior = _callbackNode.FlowBehavior;

                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }
        }

        // ===== OVERRIDES =====
        
        protected override string GetDefaultTitle() => "Callback";

        protected override void OnSaveTitle()
        {
            // Sync properties từ ViewModel về node
            if (_callbackNode.TargetNodeId != TargetNodeId)
            {
                _callbackNode.TargetNodeId = TargetNodeId;
                _host.RequestSyncDataPanels(immediate: true);
            }
            
            if (_callbackNode.MaxCallbackCount != MaxCallbackCount)
            {
                _callbackNode.MaxCallbackCount = MaxCallbackCount;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_callbackNode.FlowBehavior != FlowBehavior)
            {
                _callbackNode.FlowBehavior = FlowBehavior;
            }
            else
            {
                _callbackNode.SyncPortsForBehavior();
            }
            RefreshCallbackPortsVisual();
            _host.RequestSyncDataPanels(immediate: true);

            // Trigger PropertyChanged
            _callbackNode.NotifyTitleChanged();
        }

        private void RefreshCallbackPortsVisual()
        {
            var outputPort = _callbackNode.Ports.FirstOrDefault(p => !p.IsInput);
            if (outputPort != null)
            {
                // Keep output port aligned with the dialog setting,
                // so switching to JumpThenContinue shows it at the selected side immediately.
                outputPort.Position = OutputPortPosition;
            }

            if (outputPort?.PortUI != null)
            {
                outputPort.PortUI.Visibility = outputPort.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            _host.UpdateNodePosition(_callbackNode, _callbackNode.X, _callbackNode.Y);

            var connections = _host.ViewModel?.Connections;
            if (connections != null && connections.Count > 0)
            {
                try
                {
                    _host.ConnectionRenderer.UpdateAllConnectionPaths(connections);
                    _host.ConnectionRenderer.UpdateAllConnectionAnimations(connections);
                }
                catch
                {
                    // best-effort visual refresh
                }
            }
        }

        // ===== REFRESH METHODS =====
        
        /// <summary>
        /// Refresh danh sách các node có thể callback
        /// </summary>
        private void RefreshAvailableNodes()
        {
            AvailableNodes.Clear();

            if (_host.ViewModel == null) return;

            // Lấy tất cả node trong workflow, trừ Start, End, và chính node này
            var nodes = _host.ViewModel.Nodes
                .Where(n => n.Id != _callbackNode.Id && 
                           n.Type != NodeType.Start && 
                           n.Type != NodeType.End)
                .OrderBy(n => n.Title)
                .ToList();

            foreach (var node in nodes)
            {
                AvailableNodes.Add(CreateDataSourceOption(node));
            }
        }

        protected override void LoadInputs()
        {
            // Callback node không có dynamic inputs
            Inputs.Clear();
        }

        protected override void LoadOutputs()
        {
            // Callback node không có dynamic outputs
            Outputs.Clear();
        }
    }

    public sealed class CallbackFlowBehaviorOption
    {
        public CallbackFlowBehavior Value { get; }
        public string DisplayName { get; }

        public CallbackFlowBehaviorOption(CallbackFlowBehavior value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }
}
