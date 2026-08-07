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
        private bool _isInverted;

        [ObservableProperty]
        private bool _rightUseLiteralValue;

        [ObservableProperty]
        private string _rightLiteralValue = string.Empty;

        [ObservableProperty]
        private string? _rightSourceNodeId;

        [ObservableProperty]
        private string? _rightKey;

        [ObservableProperty]
        private double _similarityThreshold = 90;

        public ObservableCollection<WorkflowDataSourceOption> AvailableSourceNodes { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableLeftKeys { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableRightKeys { get; } = new();

        public bool IsRightSideVisible =>
            Operator != ConditionOperator.Empty &&
            Operator != ConditionOperator.NotEmpty &&
            Operator != ConditionOperator.True &&
            Operator != ConditionOperator.False;

        public bool IsImageSimilarityMode =>
            Operator == ConditionOperator.ImageSimilarityGte ||
            Operator == ConditionOperator.ImageSimilarityLte ||
            Operator == ConditionOperator.ImageSimilarityGt ||
            Operator == ConditionOperator.ImageSimilarityLt;

        public string OperatorLabel => OperatorBefore == LogicalOperator.Or ? "OR" : "AND";

        /// <summary>True nếu là điều kiện đầu tiên (không hiển thị badge OR/AND).</summary>
        public bool IsFirst { get; }

        public static (ConditionOperator Operator, bool IsInverted) NormalizeOperator(ConditionOperator op, bool isInverted)
        {
            if (isInverted) return (op, true);

            return op switch
            {
                ConditionOperator.NotEqual => (ConditionOperator.Equal, true),
                ConditionOperator.LessThan => (ConditionOperator.GreaterThan, true),
                ConditionOperator.LessThanOrEqual => (ConditionOperator.GreaterThanOrEqual, true),
                ConditionOperator.NotContains => (ConditionOperator.Contains, true),
                ConditionOperator.TextNotEquals => (ConditionOperator.TextEquals, true),
                ConditionOperator.NotEmpty => (ConditionOperator.Empty, true),
                ConditionOperator.False => (ConditionOperator.True, true),
                ConditionOperator.ImageSimilarityLte => (ConditionOperator.ImageSimilarityGte, true),
                ConditionOperator.ImageSimilarityLt => (ConditionOperator.ImageSimilarityGt, true),
                _ => (op, false)
            };
        }

        public static ConditionOperator GetEffectiveOperator(ConditionOperator op, bool isInverted)
        {
            if (!isInverted) return op;

            return op switch
            {
                ConditionOperator.Equal => ConditionOperator.NotEqual,
                ConditionOperator.GreaterThan => ConditionOperator.LessThan,
                ConditionOperator.GreaterThanOrEqual => ConditionOperator.LessThanOrEqual,
                ConditionOperator.Contains => ConditionOperator.NotContains,
                ConditionOperator.TextEquals => ConditionOperator.TextNotEquals,
                ConditionOperator.Empty => ConditionOperator.NotEmpty,
                ConditionOperator.True => ConditionOperator.False,
                ConditionOperator.ImageSimilarityGte => ConditionOperator.ImageSimilarityLte,
                ConditionOperator.ImageSimilarityGt => ConditionOperator.ImageSimilarityLt,

                ConditionOperator.NotEqual => ConditionOperator.Equal,
                ConditionOperator.LessThan => ConditionOperator.GreaterThan,
                ConditionOperator.LessThanOrEqual => ConditionOperator.GreaterThanOrEqual,
                ConditionOperator.NotContains => ConditionOperator.Contains,
                ConditionOperator.TextNotEquals => ConditionOperator.TextEquals,
                ConditionOperator.NotEmpty => ConditionOperator.Empty,
                ConditionOperator.False => ConditionOperator.True,
                ConditionOperator.ImageSimilarityLte => ConditionOperator.ImageSimilarityGte,
                ConditionOperator.ImageSimilarityLt => ConditionOperator.ImageSimilarityGt,
                _ => op
            };
        }

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
            var (normOp, normInv) = NormalizeOperator(expression.Operator, expression.IsInverted);
            _operator = normOp;
            _isInverted = normInv;
            _rightUseLiteralValue = expression.RightUseLiteralValue;
            _rightLiteralValue = expression.RightLiteralValue ?? string.Empty;
            _rightSourceNodeId = expression.RightSourceNodeId;
            _rightKey = expression.RightKey;
            _similarityThreshold = expression.SimilarityThreshold;

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
            if (node == null) return;
            foreach (var o in BaseNodeDialogViewModel.GetActiveOutputs(node))
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
            if (node == null) return;
            foreach (var o in BaseNodeDialogViewModel.GetActiveOutputs(node))
            {
                AvailableRightKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key,
                    DisplayName = o.DisplayName ?? o.Key,
                    Type = o.OutputType
                });
            }
        }

        partial void OnOperatorChanged(ConditionOperator value)
        {
            if (IsImageSimilarityMode)
            {
                RightUseLiteralValue = false;
            }
            OnPropertyChanged(nameof(IsRightSideVisible));
            OnPropertyChanged(nameof(IsImageSimilarityMode));
            OnPropertyChanged(nameof(EffectiveConditionDescription));
        }

        partial void OnIsInvertedChanged(bool value)
        {
            OnPropertyChanged(nameof(EffectiveConditionDescription));
        }

        public string EffectiveConditionDescription
        {
            get
            {
                if (!IsInverted) return string.Empty;
                return Operator switch
                {
                    ConditionOperator.Equal => "➔ Khác (!=)",
                    ConditionOperator.GreaterThan => "➔ Nhỏ hơn (<)",
                    ConditionOperator.GreaterThanOrEqual => "➔ Nhỏ hơn hoặc bằng (<=)",
                    ConditionOperator.Contains => "➔ Chuỗi không chứa",
                    ConditionOperator.TextEquals => "➔ So sánh text khác",
                    ConditionOperator.Empty => "➔ Not Null",
                    ConditionOperator.True => "➔ Giá trị là FALSE",
                    ConditionOperator.ImageSimilarityGte => "➔ 🖼️ Ảnh khớp <= %",
                    ConditionOperator.ImageSimilarityGt => "➔ 🖼️ Ảnh khớp < %",
                    _ => "➔ [Phủ định (!)]"
                };
            }
        }

        public void SyncToExpression()
        {
            Expression.LeftSourceNodeId = LeftSourceNodeId;
            Expression.LeftKey = LeftKey;
            Expression.Operator = Operator;
            Expression.IsInverted = IsInverted;
            Expression.RightUseLiteralValue = RightUseLiteralValue;
            Expression.RightLiteralValue = string.IsNullOrWhiteSpace(RightLiteralValue) ? null : RightLiteralValue.Trim();
            Expression.RightSourceNodeId = RightSourceNodeId;
            Expression.RightKey = RightKey;
            Expression.SimilarityThreshold = SimilarityThreshold;
        }
    }
}
