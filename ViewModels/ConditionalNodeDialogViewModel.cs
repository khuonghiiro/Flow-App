using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class ConditionalNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly WorkflowNode _conditionalNode;

        public ObservableCollection<ConditionRowViewModel> Conditions { get; } = new();
        public ObservableCollection<WorkflowDataSourceOption> AvailableSourceNodes { get; } = new();

        // ===== Visual Mode =====
        public ObservableCollection<string> VisualModeOptions { get; } = new()
        {
            "Giao diện cũ",
            "Giao diện mới"
        };

        private string _selectedVisualMode = "Giao diện cũ";
        public string SelectedVisualMode
        {
            get => _selectedVisualMode;
            set
            {
                if (_selectedVisualMode == value) return;
                _selectedVisualMode = value;
                OnPropertyChanged();
                ApplyVisualModeChange(value);
            }
        }

        public ObservableCollection<ConditionOperatorOption> OperatorOptions { get; } = new()
        {
            new ConditionOperatorOption(ConditionOperator.Equal, "Bằng (==)"),
            new ConditionOperatorOption(ConditionOperator.NotEqual, "Khác (!=)"),
            new ConditionOperatorOption(ConditionOperator.GreaterThan, "Lớn hơn (>)"),
            new ConditionOperatorOption(ConditionOperator.GreaterThanOrEqual, "Lớn hơn hoặc bằng (>=)"),
            new ConditionOperatorOption(ConditionOperator.LessThan, "Nhỏ hơn (<)"),
            new ConditionOperatorOption(ConditionOperator.LessThanOrEqual, "Nhỏ hơn hoặc bằng (<=)"),
            new ConditionOperatorOption(ConditionOperator.Contains, "Chuỗi chứa"),
            new ConditionOperatorOption(ConditionOperator.NotContains, "Chuỗi không chứa"),
            new ConditionOperatorOption(ConditionOperator.TextEquals, "So sánh text (không phân biệt hoa thường)"),
            new ConditionOperatorOption(ConditionOperator.TextNotEquals, "So sánh text khác"),
            new ConditionOperatorOption(ConditionOperator.Empty, "Rỗng"),
            new ConditionOperatorOption(ConditionOperator.NotEmpty, "Không rỗng"),
            new ConditionOperatorOption(ConditionOperator.True, "Giá trị là TRUE"),
            new ConditionOperatorOption(ConditionOperator.False, "Giá trị là FALSE")
        };

        public ConditionalNodeDialogViewModel(WorkflowNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _conditionalNode = node ?? throw new ArgumentNullException(nameof(node));
            // Set initial visual mode from model (without triggering layout change)
            _selectedVisualMode = node.ConditionalVisualMode == ConditionalVisualMode.Diamond
                ? "Giao diện mới"
                : "Giao diện cũ";
            RefreshAvailableSourceNodes();
            LoadConditions();
        }

        protected override string GetDefaultTitle() => "If-Else";

        /// <summary>
        /// ConditionalNode có nhiều port OUT (if/else if/else), không dùng ReuseRoutes.
        /// </summary>
        protected override bool SupportsReuseRoutes => false;

        protected override void LoadReuseRoutes()
        {
            ReuseRoutes.Clear();
            Node.ReuseRoutes?.Clear();
        }

        /// <summary>
        /// Chỉ lấy các node upstream: đi ngược theo connection vào port in của node If-Else.
        /// Ví dụ: A → B → C → IfElse → D → E thì combobox chỉ hiển thị A, B, C (không lấy D, E).
        /// </summary>
        private void RefreshAvailableSourceNodes()
        {
            AvailableSourceNodes.Clear();
            var vm = _host.ViewModel;
            if (vm?.Connections == null) return;

            var connections = vm.Connections;
            var upstream = new HashSet<WorkflowNode>();
            var stack = new Stack<WorkflowNode>();
            stack.Push(_conditionalNode);

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
                    if (src is ListOutNode)
                    {
                        upstream.Add(src);
                        continue;
                    }
                    if (upstream.Add(src))
                        stack.Push(src);
                }
            }

            upstream.Remove(_conditionalNode);

            var producerNodes = upstream
                .Where(n => (n.DynamicOutputs != null && n.DynamicOutputs.Count > 0) || n is InputNode)
                .OrderBy(n => n.Title ?? n.Id)
                .ToList();

            foreach (var n in producerNodes)
            {
                AvailableSourceNodes.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
            }
        }

        private void LoadConditions()
        {
            Conditions.Clear();
            if (_conditionalNode.ConditionalBranches == null) return;
            for (int i = 0; i < _conditionalNode.ConditionalBranches.Count; i++)
            {
                var branch = _conditionalNode.ConditionalBranches[i];
                var row = new ConditionRowViewModel(
                    _conditionalNode,
                    branch,
                    i,
                    _host,
                    AvailableSourceNodes);
                Conditions.Add(row);
            }
        }

        /// <summary>
        /// Sync tất cả VM hiện tại về Branch model trước khi reload,
        /// tránh mất dữ liệu đã set trên UI.
        /// </summary>
        private void SyncAllConditions()
        {
            foreach (var row in Conditions)
                row.SyncToBranch();
        }

        [RelayCommand]
        private void AddCondition()
        {
            SyncAllConditions();
            _host.AddElseIfBranch(_conditionalNode);
            RefreshAvailableSourceNodes();
            LoadConditions();
        }

        [RelayCommand]
        private void RemoveCondition(ConditionRowViewModel? row)
        {
            if (row == null || !row.CanRemove) return;
            SyncAllConditions();
            _host.RemoveBranch(_conditionalNode, row.Branch);
            RefreshAvailableSourceNodes();
            LoadConditions();
        }

        protected override void OnSaveTitle()
        {
            foreach (var row in Conditions)
                row.SyncToBranch();
            _host.ReRenderConditionalNode(_conditionalNode);
            _host.RenderConditionalNodePorts(_conditionalNode);
            _host.SyncAllPortsZIndex(_conditionalNode);
            var connections = _host.ViewModel?.Connections;
            if (connections != null && connections.Count > 0)
            {
                _host.ConnectionRenderer.UpdateAllConnectionPaths(connections);
                _host.ConnectionRenderer.UpdateAllConnectionAnimations(connections);
            }
        }

        protected override void SavePortPositions()
        {
            if (_conditionalNode.Ports == null || _conditionalNode.Ports.Count == 0)
                return;

            bool portsChanged = false;
            var inputPort = _conditionalNode.Ports.FirstOrDefault(p => p.IsInput);
            if (inputPort != null && inputPort.Position != InputPortPosition)
            {
                inputPort.Position = InputPortPosition;
                portsChanged = true;
            }

            foreach (var outputPort in _conditionalNode.Ports.Where(p => !p.IsInput))
            {
                if (outputPort.Position != OutputPortPosition)
                {
                    outputPort.Position = OutputPortPosition;
                    portsChanged = true;
                }
            }

            if (!portsChanged)
                return;

            _host.RenderConditionalNodePorts(_conditionalNode);
            _host.SyncAllPortsZIndex(_conditionalNode);

            var connections = _host.ViewModel?.Connections;
            if (connections != null && connections.Count > 0)
            {
                _host.ConnectionRenderer.UpdateAllConnectionPaths(connections);
                _host.ConnectionRenderer.UpdateAllConnectionAnimations(connections);
            }
        }

        private void ApplyVisualModeChange(string mode)
        {
            if (mode == "Giao diện mới" && _conditionalNode.ConditionalVisualMode != ConditionalVisualMode.Diamond)
            {
                SyncAllConditions();
                _host.ApplyConditionalDiamondLayout(_conditionalNode);
                RefreshAvailableSourceNodes();
                LoadConditions();
            }
            else if (mode == "Giao diện cũ" && _conditionalNode.ConditionalVisualMode != ConditionalVisualMode.Classic)
            {
                SyncAllConditions();
                _host.RestoreConditionalClassicLayout(_conditionalNode);
                RefreshAvailableSourceNodes();
                LoadConditions();
            }
        }
    }

    public sealed class ConditionOperatorOption
    {
        public ConditionOperator Value { get; }
        public string DisplayName { get; }
        public ConditionOperatorOption(ConditionOperator value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }
}
