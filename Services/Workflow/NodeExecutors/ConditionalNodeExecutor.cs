using FlowMy.Models;

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
            foreach (var branch in node.ConditionalBranches)
            {
                if (branch.Port == null) continue;

                // Nhánh "else": không có điều kiện → chạy khi không nhánh nào trước đúng
                if (branch.Label == "else")
                {
                    portToTake = branch.Port;
                    break;
                }

                // "if" hoặc "else if": đánh giá điều kiện (có thể nhiều sub-conditions kết hợp OR/AND)
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
                        break;
                    }
                    var value = service.ResolveConditionFromUpstreamForExecution(node, key, connections, env);
                    conditionMet = WorkflowExecutionService.ConditionValueToBool(value);
                }

                if (conditionMet)
                {
                    portToTake = branch.Port;
                    break;
                }
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
                bool acc = EvaluateSingleCondition(exprs[0], connections, service, env);
                for (int i = 1; i < exprs.Count; i++)
                {
                    bool next = EvaluateSingleCondition(exprs[i], connections, service, env);
                    var op = (ops != null && i - 1 < ops.Count) ? ops[i - 1] : LogicalOperator.And;
                    acc = op == LogicalOperator.Or ? (acc || next) : (acc && next);
                }
                return acc;
            }

            if (!string.IsNullOrWhiteSpace(branch.LeftSourceNodeId) && !string.IsNullOrWhiteSpace(branch.LeftKey))
            {
                var leftVal = service.ResolveValueByNodeIdAndKeyForExecution(connections, branch.LeftSourceNodeId, branch.LeftKey, env);
                string? rightVal = null;
                if (branch.Operator != ConditionOperator.Empty && branch.Operator != ConditionOperator.NotEmpty)
                {
                    if (branch.RightUseLiteralValue)
                        rightVal = branch.RightLiteralValue ?? string.Empty;
                    else
                        rightVal = service.ResolveValueByNodeIdAndKeyForExecution(connections, branch.RightSourceNodeId, branch.RightKey, env);
                }
                return WorkflowExecutionService.EvaluateCondition(leftVal, rightVal, branch.Operator);
            }

            return null;
        }

        private static bool EvaluateSingleCondition(
            ConditionExpression expr,
            IReadOnlyList<WorkflowConnection> connections,
            WorkflowExecutionService service,
            NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(expr.LeftSourceNodeId) || string.IsNullOrWhiteSpace(expr.LeftKey))
                return false;
            var leftVal = service.ResolveValueByNodeIdAndKeyForExecution(connections, expr.LeftSourceNodeId, expr.LeftKey, env);
            string? rightVal = null;
            if (expr.Operator != ConditionOperator.Empty && expr.Operator != ConditionOperator.NotEmpty)
            {
                if (expr.RightUseLiteralValue)
                    rightVal = expr.RightLiteralValue ?? string.Empty;
                else
                    rightVal = service.ResolveValueByNodeIdAndKeyForExecution(connections, expr.RightSourceNodeId, expr.RightKey, env);
            }
            return WorkflowExecutionService.EvaluateCondition(leftVal, rightVal, expr.Operator);
        }
    }
}
