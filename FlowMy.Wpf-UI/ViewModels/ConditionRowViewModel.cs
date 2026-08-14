// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// Một dòng điều kiện trong dialog If-Else (if / else if / else).
    /// Số thứ tự (BranchIndex) tương ứng với số hiển thị cạnh port out.
    /// </summary>
    public partial class ConditionRowViewModel : ObservableObject
    {
        private readonly WorkflowNode _conditionalNode;
        private readonly IWorkflowEditorHost _host;

        public ConditionalBranch Branch { get; }

        [ObservableProperty]
        private int _branchIndex;
        public int DisplayOrder => BranchIndex + 1;

        /// <summary>Tiêu đề có thể chỉnh sửa; fallback "Điều kiện {index}" nếu trống.</summary>
        [ObservableProperty]
        private string _title = string.Empty;

        public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Title.Trim() : $"Điều kiện {BranchIndex}";

        [ObservableProperty]
        private string? _leftSourceNodeId;

        [ObservableProperty]
        private string? _leftKey;

        [ObservableProperty]
        private ConditionOperator _operator;

        [ObservableProperty]
        private bool _isInverted;

        /// <summary>True = bên phải dùng giá trị nhập (TextBox); False = chọn node + key.</summary>
        [ObservableProperty]
        private bool _rightUseLiteralValue;

        [ObservableProperty]
        private string _rightLiteralValue = string.Empty;

        [ObservableProperty]
        private string? _rightSourceNodeId;

        [ObservableProperty]
        private string? _rightKey;

        [ObservableProperty]
        private bool _isElse;

        [ObservableProperty]
        private double _similarityThreshold = 90;

        public bool CanRemove { get; }

        /// <summary>Sub-conditions thêm bằng OR/AND (item đầu = main condition từ Left/Op/Right).</summary>
        public ObservableCollection<SubConditionViewModel> SubConditions { get; } = new();

        /// <summary>Tóm tắt số sub-conditions hiện có (dùng cho Expander header).</summary>
        public string SubConditionsSummary
        {
            get
            {
                int total = SubConditions.Count;
                if (total <= 1) return "📋 1 điều kiện";
                int orCount = SubConditions.Count(s => !s.IsFirst && s.OperatorBefore == LogicalOperator.Or);
                int andCount = SubConditions.Count(s => !s.IsFirst && s.OperatorBefore == LogicalOperator.And);
                var parts = new System.Collections.Generic.List<string>();
                if (orCount > 0) parts.Add($"{orCount} OR");
                if (andCount > 0) parts.Add($"{andCount} AND");
                return parts.Count > 0
                    ? $"📋 {total} điều kiện ({string.Join(", ", parts)})"
                    : $"📋 {total} điều kiện";
            }
        }

        /// <summary>
        /// Binding cho Expander.IsExpanded — mặc định mở khi ≤ 2 sub-conditions, đóng khi > 2.
        /// User có thể toggle thủ công (TwoWay binding).
        /// </summary>
        [ObservableProperty]
        private bool _isSubConditionsExpanded = true;

        public ObservableCollection<WorkflowDataSourceOption> AvailableSourceNodes { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableLeftKeys { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableRightKeys { get; } = new();

        /// <summary>
        /// Hiển thị phần cấu hình bên phải (literal / node phải + key phải).
        /// Ẩn khi là nhánh else, hoặc dùng các toán tử chỉ cần Left:
        /// Empty, NotEmpty, True, False.
        /// </summary>
        public bool IsRightSideVisible =>
            !IsElse &&
            Operator != ConditionOperator.Empty &&
            Operator != ConditionOperator.NotEmpty &&
            Operator != ConditionOperator.True &&
            Operator != ConditionOperator.False;

        public bool IsImageSimilarityMode =>
            Operator == ConditionOperator.ImageSimilarityGte ||
            Operator == ConditionOperator.ImageSimilarityLte ||
            Operator == ConditionOperator.ImageSimilarityGt ||
            Operator == ConditionOperator.ImageSimilarityLt;

        public ConditionRowViewModel(
            WorkflowNode conditionalNode,
            ConditionalBranch branch,
            int branchIndex,
            IWorkflowEditorHost host,
            ObservableCollection<WorkflowDataSourceOption> availableSourceNodes)
        {
            _conditionalNode = conditionalNode;
            _host = host;
            Branch = branch;
            _branchIndex = branchIndex;
            CanRemove = branch.CanRemove;
            _isElse = branch.Label == "else";
            _title = !string.IsNullOrWhiteSpace(branch.DisplayTitle) ? branch.DisplayTitle : string.Empty;

            _leftSourceNodeId = branch.LeftSourceNodeId;
            _leftKey = branch.LeftKey;
            var (normOp, normInv) = SubConditionViewModel.NormalizeOperator(branch.Operator, branch.IsInverted);
            _operator = normOp;
            _isInverted = normInv;
            _rightUseLiteralValue = branch.RightUseLiteralValue;
            _rightLiteralValue = branch.RightLiteralValue ?? string.Empty;
            _rightSourceNodeId = branch.RightSourceNodeId;
            _rightKey = branch.RightKey;
            _similarityThreshold = branch.SimilarityThreshold;

            foreach (var opt in availableSourceNodes)
                AvailableSourceNodes.Add(opt);

            RefreshLeftKeys();
            RefreshRightKeys();

            LoadSubConditions(availableSourceNodes);

            // Auto-collapse Expander khi > 2 sub-conditions
            _isSubConditionsExpanded = SubConditions.Count <= 2;

            // Refresh summary khi thêm/xóa sub-conditions
            SubConditions.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(SubConditionsSummary));
            };

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

        partial void OnBranchIndexChanged(int value)
        {
            OnPropertyChanged(nameof(DisplayTitle));
            OnPropertyChanged(nameof(DisplayOrder));
        }
        partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(DisplayTitle));

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

        private void LoadSubConditions(ObservableCollection<WorkflowDataSourceOption> availableSourceNodes)
        {
            SubConditions.Clear();
            if (IsElse) return;

            var exprs = Branch.SubConditions;
            var ops = Branch.OperatorsBetween;

            if (exprs != null && exprs.Count > 0)
            {
                for (int i = 0; i < exprs.Count; i++)
                {
                    var op = (i == 0) ? LogicalOperator.And : ((ops != null && i - 1 < ops.Count) ? ops[i - 1] : LogicalOperator.And);
                    SubConditions.Add(new SubConditionViewModel(exprs[i], op, _host, availableSourceNodes, isFirst: i == 0));
                }
            }
            else
            {
                var first = new ConditionExpression
                {
                    LeftSourceNodeId = Branch.LeftSourceNodeId,
                    LeftKey = Branch.LeftKey,
                    Operator = Branch.Operator,
                    RightUseLiteralValue = Branch.RightUseLiteralValue,
                    RightLiteralValue = Branch.RightLiteralValue,
                    RightSourceNodeId = Branch.RightSourceNodeId,
                    RightKey = Branch.RightKey
                };
                Branch.SubConditions = new System.Collections.Generic.List<ConditionExpression> { first };
                Branch.OperatorsBetween = new System.Collections.Generic.List<LogicalOperator>();
                SubConditions.Add(new SubConditionViewModel(first, LogicalOperator.And, _host, availableSourceNodes, isFirst: true));
            }
        }

        [RelayCommand]
        private void AddWithOr()
        {
            if (IsElse) return;
            var newExpr = new ConditionExpression();
            if (Branch.SubConditions == null) Branch.SubConditions = new System.Collections.Generic.List<ConditionExpression>();
            if (Branch.OperatorsBetween == null) Branch.OperatorsBetween = new System.Collections.Generic.List<LogicalOperator>();
            Branch.SubConditions.Add(newExpr);
            Branch.OperatorsBetween.Add(LogicalOperator.Or);
            SubConditions.Add(new SubConditionViewModel(newExpr, LogicalOperator.Or, _host, AvailableSourceNodes, isFirst: false));
        }

        [RelayCommand]
        private void AddWithAnd()
        {
            if (IsElse) return;
            var newExpr = new ConditionExpression();
            if (Branch.SubConditions == null) Branch.SubConditions = new System.Collections.Generic.List<ConditionExpression>();
            if (Branch.OperatorsBetween == null) Branch.OperatorsBetween = new System.Collections.Generic.List<LogicalOperator>();
            Branch.SubConditions.Add(newExpr);
            Branch.OperatorsBetween.Add(LogicalOperator.And);
            SubConditions.Add(new SubConditionViewModel(newExpr, LogicalOperator.And, _host, AvailableSourceNodes, isFirst: false));
        }

        [RelayCommand]
        private void RemoveSubCondition(SubConditionViewModel? sub)
        {
            if (sub == null || IsElse) return;
            var idx = SubConditions.IndexOf(sub);
            if (idx < 1) return;
            SubConditions.Remove(sub);
            Branch.SubConditions?.RemoveAt(idx);
            if (Branch.OperatorsBetween != null && idx - 1 < Branch.OperatorsBetween.Count)
                Branch.OperatorsBetween.RemoveAt(idx - 1);
        }

        public void SyncToBranch()
        {
            Branch.DisplayTitle = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim();
            if (SubConditions.Count > 0)
            {
                var first = SubConditions[0];
                first.SyncToExpression();
                Branch.LeftSourceNodeId = Branch.SubConditions?[0]?.LeftSourceNodeId;
                Branch.LeftKey = Branch.SubConditions?[0]?.LeftKey;
                Branch.Operator = Branch.SubConditions?[0]?.Operator ?? Operator;
                Branch.IsInverted = Branch.SubConditions?[0]?.IsInverted ?? IsInverted;
                Branch.RightUseLiteralValue = Branch.SubConditions?[0]?.RightUseLiteralValue ?? RightUseLiteralValue;
                Branch.RightLiteralValue = Branch.SubConditions?[0]?.RightLiteralValue;
                Branch.RightSourceNodeId = Branch.SubConditions?[0]?.RightSourceNodeId;
                Branch.RightKey = Branch.SubConditions?[0]?.RightKey;
                Branch.SimilarityThreshold = Branch.SubConditions?[0]?.SimilarityThreshold ?? SimilarityThreshold;
                for (int i = 1; i < SubConditions.Count; i++)
                    SubConditions[i].SyncToExpression();
            }
            else
            {
                Branch.LeftSourceNodeId = LeftSourceNodeId;
                Branch.LeftKey = LeftKey;
                Branch.Operator = Operator;
                Branch.IsInverted = IsInverted;
                Branch.RightUseLiteralValue = RightUseLiteralValue;
                Branch.RightLiteralValue = string.IsNullOrWhiteSpace(RightLiteralValue) ? null : RightLiteralValue.Trim();
                Branch.RightSourceNodeId = RightSourceNodeId;
                Branch.RightKey = RightKey;
                Branch.SimilarityThreshold = SimilarityThreshold;
            }
        }
    }
}
