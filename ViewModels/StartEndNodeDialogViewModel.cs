using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public sealed partial class StartEndNodeDialogViewModel : BaseNodeDialogViewModel
    {
        [ObservableProperty]
        private string _flowScopeKey = string.Empty;

        [ObservableProperty]
        private FlowRunMode _runMode = FlowRunMode.MainFlow;

        [ObservableProperty]
        private EndNodeBehavior _endBehavior = EndNodeBehavior.StopCurrentFlow;

        [ObservableProperty]
        private DiamondSharpness _diamondSharpness = DiamondSharpness.Medium;
        
        [ObservableProperty]
        private double _autoRunIntervalValue = 5d;

        [ObservableProperty]
        private AutoRunIntervalUnit _autoRunIntervalUnit = AutoRunIntervalUnit.Seconds;

        [ObservableProperty]
        private bool _isDuplicateFlowScopeKey;

        [ObservableProperty]
        private string _flowScopeWarningText = string.Empty;

        public bool IsStartNode => _node.Type == NodeType.Start;
        public bool IsEndNode => _node.Type == NodeType.End;
        public bool IsAutoScheduledMode => IsStartNode && RunMode == FlowRunMode.AutoScheduled;
        public bool ShowInputPortConfig => IsStartNode || IsEndNode;
        public bool ShowOutputPortConfig => IsStartNode || IsEndNode;

        public ObservableCollection<RunModeOption> RunModeOptions { get; } = new()
        {
            new RunModeOption(FlowRunMode.MainFlow, "Luồng chính", "Điểm bắt đầu chính của workflow", "Tròn"),
            new RunModeOption(FlowRunMode.SubFlowAttached, "Luồng con bám cha", "Tạo nhánh phụ và giữ liên hệ với luồng cha", "Vuông bo"),
            new RunModeOption(FlowRunMode.SubFlowIndependent, "Luồng con độc lập", "Tách scope độc lập, phù hợp worker riêng", "Hình thoi"),
            new RunModeOption(FlowRunMode.AutoScheduled, "Tự động theo lịch", "Tự chạy theo chu kỳ đã cài, không phụ thuộc nút Bắt đầu", "Hình thoi")
        };
        
        public ObservableCollection<AutoRunIntervalUnit> AutoRunIntervalUnitOptions { get; } = new()
        {
            AutoRunIntervalUnit.Milliseconds,
            AutoRunIntervalUnit.Seconds,
            AutoRunIntervalUnit.Minutes
        };

        public ObservableCollection<EndBehaviorOption> EndBehaviorOptions { get; } = new()
        {
            new EndBehaviorOption(EndNodeBehavior.StopCurrentFlow, "Dừng nhánh hiện tại", "Kết thúc ngay luồng hiện tại", "Tròn"),
            new EndBehaviorOption(EndNodeBehavior.ReturnToParent, "Trả về luồng cha", "Đóng nhánh con và quay về parent scope", "Hình thoi"),
            new EndBehaviorOption(EndNodeBehavior.EmitResultOnly, "Chỉ phát kết quả", "Đánh dấu hoàn thành kết quả, không đi tiếp", "Vuông")
        };

        public ObservableCollection<DiamondSharpnessOption> DiamondSharpnessOptions { get; } = new()
        {
            // new DiamondSharpnessOption(DiamondSharpness.Soft, "Mềm", "Ít nhọn, dễ đọc khi zoom nhỏ"),
            // new DiamondSharpnessOption(DiamondSharpness.Medium, "Vừa", "Cân bằng giữa nhận diện và không chiếm diện tích"),
            // new DiamondSharpnessOption(DiamondSharpness.Sharp, "Sắc", "Nhọn rõ, phân biệt mạnh với dạng tròn/vuông")
        };

        public StartEndNodeDialogViewModel(WorkflowNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _flowScopeKey = node.FlowScopeKey ?? string.Empty;
            _runMode = node.RunMode;
            _endBehavior = node.EndBehavior;
            _diamondSharpness = node.DiamondSharpness;
            _autoRunIntervalValue = node.AutoRunIntervalValue <= 0 ? 5d : node.AutoRunIntervalValue;
            _autoRunIntervalUnit = node.AutoRunIntervalUnit;
            RefreshFlowScopeWarning();
        }

        protected override string GetDefaultTitle() => _node.Type == NodeType.Start ? "Start" : "End";

        protected override bool SupportsReuseRoutes => false;

        protected override void OnSaveTitle()
        {
            _node.FlowScopeKey = string.IsNullOrWhiteSpace(FlowScopeKey) ? null : FlowScopeKey.Trim();
            _node.RunMode = RunMode;
            _node.EndBehavior = EndBehavior;
            _node.DiamondSharpness = DiamondSharpness;
            _node.AutoRunIntervalValue = AutoRunIntervalValue <= 0 ? 1d : AutoRunIntervalValue;
            _node.AutoRunIntervalUnit = AutoRunIntervalUnit;

            ApplyVisualUpdate();

            _host.RequestSyncDataPanels(immediate: true);
        }

        partial void OnFlowScopeKeyChanged(string value)
        {
            RefreshFlowScopeWarning();
        }

        partial void OnRunModeChanged(FlowRunMode value)
        {
            _node.RunMode = value;
            OnPropertyChanged(nameof(IsAutoScheduledMode));
            ApplyVisualUpdate();
        }

        partial void OnEndBehaviorChanged(EndNodeBehavior value)
        {
            _node.EndBehavior = value;
            ApplyVisualUpdate();
        }

        partial void OnDiamondSharpnessChanged(DiamondSharpness value)
        {
            _node.DiamondSharpness = value;
            ApplyVisualUpdate();
        }

        private void RefreshFlowScopeWarning()
        {
            var key = FlowScopeKey?.Trim();
            if (string.IsNullOrWhiteSpace(key) || _host.ViewModel == null)
            {
                IsDuplicateFlowScopeKey = false;
                FlowScopeWarningText = string.Empty;
                return;
            }

            var duplicateCount = _host.ViewModel.Nodes
                .Where(n => n.Type == NodeType.Start && !ReferenceEquals(n, _node))
                .Count(n => string.Equals((n.FlowScopeKey ?? string.Empty).Trim(), key, StringComparison.OrdinalIgnoreCase));

            IsDuplicateFlowScopeKey = duplicateCount > 0;
            FlowScopeWarningText = duplicateCount > 0
                ? $"FlowScopeKey '{key}' đang trùng với {duplicateCount} Start node khác."
                : string.Empty;
        }

        private void ApplyVisualUpdate()
        {
            if (_node.Type == NodeType.Start)
            {
                StartNodeControl.RefreshVisual(_node);
                StartNodeControl.UpdateTitlePosition(_node, _host.WorkflowCanvas);
            }
            else if (_node.Type == NodeType.End)
            {
                EndNodeControl.RefreshVisual(_node);
                EndNodeControl.UpdateTitlePosition(_node, _host.WorkflowCanvas);
            }

            _host.UpdateNodePosition(_node, _node.X, _node.Y);
            var cons = _host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                _host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                _host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
            }
        }
    }

    public sealed class RunModeOption
    {
        public FlowRunMode Value { get; }
        public string Title { get; }
        public string Summary { get; }
        public string ShapeHint { get; }

        public RunModeOption(FlowRunMode value, string title, string summary, string shapeHint)
        {
            Value = value;
            Title = title;
            Summary = summary;
            ShapeHint = shapeHint;
        }

        public override string ToString() => $"{Title} - {Summary}";
    }

    public sealed class EndBehaviorOption
    {
        public EndNodeBehavior Value { get; }
        public string Title { get; }
        public string Summary { get; }
        public string ShapeHint { get; }

        public EndBehaviorOption(EndNodeBehavior value, string title, string summary, string shapeHint)
        {
            Value = value;
            Title = title;
            Summary = summary;
            ShapeHint = shapeHint;
        }

        public override string ToString() => $"{Title} - {Summary}";
    }

    public sealed class DiamondSharpnessOption
    {
        public DiamondSharpness Value { get; }
        public string Title { get; }
        public string Summary { get; }

        public DiamondSharpnessOption(DiamondSharpness value, string title, string summary)
        {
            Value = value;
            Title = title;
            Summary = summary;
        }

        public override string ToString() => $"{Title} - {Summary}";
    }
}
