using FlowMy.Models;
using System.Linq;
using System.Windows;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho Conditional Node (If-Else).
    /// Logic: nếu port if true → chỉ đi nhánh if, bỏ qua else if và else;
    /// nếu if false → kiểm tra lần lượt else if, nhánh nào đúng thì chỉ đi nhánh đó;
    /// nếu không có else if nào đúng → đi nhánh else.
    /// </summary>
    internal sealed class ConditionalNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node?.IsConditionalNode == true;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            if (node?.ConditionalBranches == null || node.ConditionalBranches.Count == 0)
            {
                env.OnNodeCompleted?.Invoke(node, TimeSpan.Zero);
                return;
            }

            var connections = env.Connections;
            var service = env.Service;

            env.OnNodeCompleted?.Invoke(node, TimeSpan.Zero);

            // Tìm nhánh đúng đầu tiên: if → else if (theo thứ tự) → else
            NodePort? portToTake = null;
            ConditionalBranch? selectedBranch = null;
            var branchEvaluationResults = new Dictionary<ConditionalBranch, bool>();

            for (int i = 0; i < node.ConditionalBranches.Count; i++)
            {
                var branch = node.ConditionalBranches[i];
                if (branch.Port == null) continue;

                // Nhánh "else": không có điều kiện → chạy khi không nhánh nào trước đúng
                if (branch.Label == "else")
                {
                    portToTake = branch.Port;
                    selectedBranch = branch;
                    branchEvaluationResults[branch] = true;
                    break;
                }

                // "if" hoặc "else if": đánh giá điều kiện
                bool conditionMet = false;
                var branchResult = EvaluateBranchCondition(branch, connections, service, env);
                if (branchResult.HasValue)
                {
                    conditionMet = branchResult.Value;
                }
                else
                {
                    // Legacy: một key từ upstream (input port)
                    var key = !string.IsNullOrWhiteSpace(branch.Condition)
                        ? branch.Condition.Trim()
                        : (node.Condition?.Trim() ?? "condition");
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        portToTake = branch.Port;
                        selectedBranch = branch;
                        branchEvaluationResults[branch] = true;
                        break;
                    }
                    var value = service.ResolveConditionFromUpstreamForExecution(node, key, connections, env);
                    conditionMet = WorkflowExecutionService.ConditionValueToBool(value);
                }

                branchEvaluationResults[branch] = conditionMet;

                if (conditionMet)
                {
                    portToTake = branch.Port;
                    selectedBranch = branch;
                    break;
                }
            }

            // Cập nhật UI kết quả điều kiện
            if (Application.Current?.Dispatcher != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var branch in node.ConditionalBranches)
                    {
                        bool isTaken = branch == selectedBranch;
                        bool isEvaluated = branchEvaluationResults.TryGetValue(branch, out bool res);

                        if (branch.ResultBadgeUI != null)
                        {
                            string badgeStr = isTaken ? "✔ TRUE" : (isEvaluated ? "✖ FALSE" : "⏹ SKIP");
                            if (branch.LastImageSimilarityScore.HasValue)
                            {
                                string score = $"{branch.LastImageSimilarityScore.Value:F1}%";
                                badgeStr = isTaken ? $"✔ {score}" : (isEvaluated ? $"✖ {score}" : "⏹ SKIP");
                            }

                            branch.ResultBadgeUI.Text = badgeStr;
                            if (isTaken)
                                branch.ResultBadgeUI.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(187, 247, 208));
                            else if (isEvaluated)
                                branch.ResultBadgeUI.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 202, 202));
                            else
                                branch.ResultBadgeUI.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 255, 255, 255));
                        }

                        if (branch.ResultContainerUI != null)
                        {
                            branch.ResultContainerUI.Visibility = System.Windows.Visibility.Visible;
                            if (!string.IsNullOrEmpty(branch.LastEvaluationDetails))
                            {
                                branch.ResultContainerUI.ToolTip = branch.LastEvaluationDetails;
                            }

                            if (isTaken)
                            {
                                branch.ResultContainerUI.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(140, 22, 101, 52));
                                branch.ResultContainerUI.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
                            }
                            else if (isEvaluated)
                            {
                                branch.ResultContainerUI.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 153, 27, 27));
                                branch.ResultContainerUI.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                            }
                            else
                            {
                                branch.ResultContainerUI.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 0, 0));
                                branch.ResultContainerUI.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255));
                            }
                        }

                        if (isTaken && branch.SatelliteLine != null)
                        {
                            var colorKey = FlowMy.Views.NodeControls.ConditionalDiamondControl.GetSatelliteColorKey(node.ConditionalBranches.IndexOf(branch));
                            var brush = System.Windows.Application.Current.TryFindResource(colorKey + "Brush") as System.Windows.Media.SolidColorBrush;
                            var color = brush?.Color ?? System.Windows.Media.Colors.LimeGreen;
                            FlowMy.Views.NodeControls.ConditionalDiamondControl.StartSatelliteLineAnimation(branch.SatelliteLine, color);
                        }
                    }

                    if (node.ConditionResultTextUI != null)
                    {
                        if (selectedBranch != null)
                        {
                            var label = !string.IsNullOrWhiteSpace(selectedBranch.DisplayTitle)
                                ? selectedBranch.DisplayTitle
                                : selectedBranch.Label;

                            if (selectedBranch.LastImageSimilarityScore.HasValue)
                            {
                                node.ConditionResultTextUI.Text = $"{label} ({selectedBranch.LastImageSimilarityScore.Value:F1}%) → TRUE";
                            }
                            else
                            {
                                node.ConditionResultTextUI.Text = $"{label} → TRUE";
                            }

                            node.ConditionResultTextUI.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(187, 247, 208));
                            if (!string.IsNullOrEmpty(selectedBranch.LastEvaluationDetails))
                            {
                                node.ConditionResultTextUI.ToolTip = $"Tính toán chi tiết: {selectedBranch.LastEvaluationDetails}";
                            }
                        }
                        else
                        {
                            var evalSimBranch = node.ConditionalBranches.FirstOrDefault(b => b.LastImageSimilarityScore.HasValue);
                            if (evalSimBranch != null && evalSimBranch.LastImageSimilarityScore.HasValue)
                            {
                                var label = !string.IsNullOrWhiteSpace(evalSimBranch.DisplayTitle) ? evalSimBranch.DisplayTitle : evalSimBranch.Label;
                                node.ConditionResultTextUI.Text = $"{label} ({evalSimBranch.LastImageSimilarityScore.Value:F1}%) → FALSE";
                            }
                            else
                            {
                                node.ConditionResultTextUI.Text = "Tất cả FALSE";
                            }
                            node.ConditionResultTextUI.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 202, 202));
                        }
                    }

                    if (node.ConditionResultPanelUI != null)
                    {
                        node.ConditionResultPanelUI.Visibility = System.Windows.Visibility.Visible;
                    }

                    if (node.ExecutionStatusTextUI != null && selectedBranch != null)
                    {
                        var label = !string.IsNullOrWhiteSpace(selectedBranch.DisplayTitle) ? selectedBranch.DisplayTitle : selectedBranch.Label;
                        if (selectedBranch.LastImageSimilarityScore.HasValue)
                        {
                            node.ExecutionStatusTextUI.Text = $"✅ [{label}: {selectedBranch.LastImageSimilarityScore.Value:F1}% -> TRUE]";
                        }
                        else
                        {
                            node.ExecutionStatusTextUI.Text = $"✅ [{label}: TRUE]";
                        }
                    }
                });
            }

            if (portToTake == null) return;

            var nextConnections = service.GetConnectionsFromPort(portToTake, node, connections);

            foreach (var conn in nextConnections)
            {
                if (conn.ToNode == null) continue;
                if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                {
                    env.Service.SignalLoopBodyReturn(conn, env.ExecutionId, env.BranchId);
                    continue;
                }
                await env.ExecuteNextAsync(conn.ToNode, conn);
            }
        }

        /// <summary>
        /// Đánh giá điều kiện của branch. Dùng SubConditions nếu có, else dùng Left/Op/Right.
        /// Trả về null nếu không có điều kiện (legacy path).
        /// </summary>
        private static bool? EvaluateBranchCondition(
            ConditionalBranch branch,
            IReadOnlyList<WorkflowConnection> connections,
            WorkflowExecutionService service,
            NodeExecutionEnvironment env)
        {
            var exprs = branch.SubConditions;
            var ops = branch.OperatorsBetween;

            if (exprs != null && exprs.Count > 0)
            {
                bool acc = EvaluateSingleCondition(branch, exprs[0], connections, service, env);
                for (int i = 1; i < exprs.Count; i++)
                {
                    bool next = EvaluateSingleCondition(branch, exprs[i], connections, service, env);
                    var op = (ops != null && i - 1 < ops.Count) ? ops[i - 1] : LogicalOperator.And;
                    acc = op == LogicalOperator.Or ? (acc || next) : (acc && next);
                }
                return acc;
            }

            if (!string.IsNullOrWhiteSpace(branch.LeftSourceNodeId) && !string.IsNullOrWhiteSpace(branch.LeftKey))
            {
                var leftVal = service.ResolveValueByNodeIdAndKeyForExecution(connections, branch.LeftSourceNodeId, branch.LeftKey, env);
                string? rightVal = null;
                bool isImageSim = branch.Operator == ConditionOperator.ImageSimilarityGte ||
                                  branch.Operator == ConditionOperator.ImageSimilarityLte ||
                                  branch.Operator == ConditionOperator.ImageSimilarityGt ||
                                  branch.Operator == ConditionOperator.ImageSimilarityLt;

                if (branch.Operator != ConditionOperator.Empty && branch.Operator != ConditionOperator.NotEmpty)
                {
                    if (branch.RightUseLiteralValue && !isImageSim)
                        rightVal = branch.RightLiteralValue ?? string.Empty;
                    else
                        rightVal = service.ResolveValueByNodeIdAndKeyForExecution(connections, branch.RightSourceNodeId, branch.RightKey, env);
                }

                if (isImageSim)
                {
                    double sim = WorkflowExecutionService.ComputeImageSimilarity(leftVal, rightVal);
                    branch.LastImageSimilarityScore = sim;
                    branch.LastEvaluationDetails = $"Khớp ảnh: {sim:F1}% (Ngưỡng yêu cầu: {branch.SimilarityThreshold:F0}%)";
                }

                var effectiveOp = ViewModels.SubConditionViewModel.GetEffectiveOperator(branch.Operator, branch.IsInverted);
                return WorkflowExecutionService.EvaluateCondition(leftVal, rightVal, effectiveOp, branch.SimilarityThreshold);
            }

            return null;
        }

        private static bool EvaluateSingleCondition(
            ConditionalBranch branch,
            ConditionExpression expr,
            IReadOnlyList<WorkflowConnection> connections,
            WorkflowExecutionService service,
            NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(expr.LeftSourceNodeId) || string.IsNullOrWhiteSpace(expr.LeftKey))
                return false;
            var leftVal = service.ResolveValueByNodeIdAndKeyForExecution(connections, expr.LeftSourceNodeId, expr.LeftKey, env);
            string? rightVal = null;
            bool isImageSim = expr.Operator == ConditionOperator.ImageSimilarityGte ||
                              expr.Operator == ConditionOperator.ImageSimilarityLte ||
                              expr.Operator == ConditionOperator.ImageSimilarityGt ||
                              expr.Operator == ConditionOperator.ImageSimilarityLt;

            if (expr.Operator != ConditionOperator.Empty && expr.Operator != ConditionOperator.NotEmpty)
            {
                if (expr.RightUseLiteralValue && !isImageSim)
                    rightVal = expr.RightLiteralValue ?? string.Empty;
                else
                    rightVal = service.ResolveValueByNodeIdAndKeyForExecution(connections, expr.RightSourceNodeId, expr.RightKey, env);
            }

            if (isImageSim)
            {
                double sim = WorkflowExecutionService.ComputeImageSimilarity(leftVal, rightVal);
                branch.LastImageSimilarityScore = sim;
                branch.LastEvaluationDetails = $"Khớp ảnh: {sim:F1}% (Ngưỡng yêu cầu: {expr.SimilarityThreshold:F0}%)";
            }

            var effectiveOp = ViewModels.SubConditionViewModel.GetEffectiveOperator(expr.Operator, expr.IsInverted);
            return WorkflowExecutionService.EvaluateCondition(leftVal, rightVal, effectiveOp, expr.SimilarityThreshold);
        }
    }
}
