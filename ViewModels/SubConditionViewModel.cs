using FlowMy.Models;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// Một sub-condition trong một branch (kết hợp OR/AND với sub-condition trước).
    /// </summary>
    public partial class SubConditionViewModel : ObservableObject
    {
        private readonly IWorkflowEditorHost _host;

        public LogicalOperator OperatorBefore { get; }
        public ConditionExpression Expression { get; }

        [ObservableProperty]
        private string? _leftSourceNodeId;

        [ObservableProperty]
        private string? _leftKey;

        [ObservableProperty]
        private ConditionOperator _operator;

        [ObservableProperty]
        private bool _rightUseLiteralValue;

        [ObservableProperty]
        private string _rightLiteralValue = string.Empty;

        [ObservableProperty]
        private string? _rightSourceNodeId;

        [ObservableProperty]
        private string? _rightKey;

        public ObservableCollection<WorkflowDataSourceOption> AvailableSourceNodes { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableLeftKeys { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableRightKeys { get; } = new();

        public bool IsRightSideVisible =>
            Operator != ConditionOperator.Empty &&
            Operator != ConditionOperator.NotEmpty &&
            Operator != ConditionOperator.True &&
            Operator != ConditionOperator.False;

        public string OperatorLabel => OperatorBefore == LogicalOperator.Or ? "OR" : "AND";

        /// <summary>True nếu là điều kiện đầu tiên (không hiển thị badge OR/AND).</summary>
        public bool IsFirst { get; }

        public SubConditionViewModel(
            ConditionExpression expression,
            LogicalOperator operatorBefore,
            IWorkflowEditorHost host,
            ObservableCollection<WorkflowDataSourceOption> availableSourceNodes,
            bool isFirst = false)
        {
            _host = host;
            Expression = expression;
            OperatorBefore = operatorBefore;
            IsFirst = isFirst;

            _leftSourceNodeId = expression.LeftSourceNodeId;
            _leftKey = expression.LeftKey;
            _operator = expression.Operator;
            _rightUseLiteralValue = expression.RightUseLiteralValue;
            _rightLiteralValue = expression.RightLiteralValue ?? string.Empty;
            _rightSourceNodeId = expression.RightSourceNodeId;
            _rightKey = expression.RightKey;

            foreach (var opt in availableSourceNodes)
                AvailableSourceNodes.Add(opt);

            RefreshLeftKeys();
            RefreshRightKeys();

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LeftSourceNodeId)) RefreshLeftKeys();
                if (e.PropertyName == nameof(RightSourceNodeId)) RefreshRightKeys();
            };
        }

        public void RefreshLeftKeys()
        {
            AvailableLeftKeys.Clear();
            if (string.IsNullOrWhiteSpace(LeftSourceNodeId)) return;
            var node = _host.ViewModel?.Nodes?.FirstOrDefault(n => n.Id == LeftSourceNodeId);
            if (node?.DynamicOutputs == null) return;
            foreach (var o in node.DynamicOutputs)
            {
                AvailableLeftKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key,
                    DisplayName = o.DisplayName ?? o.Key,
                    Type = o.OutputType
                });
            }
        }

        public void RefreshRightKeys()
        {
            AvailableRightKeys.Clear();
            if (string.IsNullOrWhiteSpace(RightSourceNodeId)) return;
            var node = _host.ViewModel?.Nodes?.FirstOrDefault(n => n.Id == RightSourceNodeId);
            if (node?.DynamicOutputs == null) return;
            foreach (var o in node.DynamicOutputs)
            {
                AvailableRightKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key,
                    DisplayName = o.DisplayName ?? o.Key,
                    Type = o.OutputType
                });
            }
        }

        partial void OnOperatorChanged(ConditionOperator value) => OnPropertyChanged(nameof(IsRightSideVisible));

        public void SyncToExpression()
        {
            Expression.LeftSourceNodeId = LeftSourceNodeId;
            Expression.LeftKey = LeftKey;
            Expression.Operator = Operator;
            Expression.RightUseLiteralValue = RightUseLiteralValue;
            Expression.RightLiteralValue = string.IsNullOrWhiteSpace(RightLiteralValue) ? null : RightLiteralValue.Trim();
            Expression.RightSourceNodeId = RightSourceNodeId;
            Expression.RightKey = RightKey;
        }
    }
}
