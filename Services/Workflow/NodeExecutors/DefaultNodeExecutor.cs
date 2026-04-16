using FlowMy.Models;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor mặc định cho các node không có logic riêng: chỉ tính thời gian = 0ms và đi tiếp theo connections.
    /// </summary>
    internal sealed class DefaultNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => true;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            // Default/local work: hiện chưa có action riêng => coi như 0ms.
            env.OnNodeCompleted?.Invoke(node, TimeSpan.Zero);

            // End node là điểm chốt flow; không tiếp tục traverse xuống dưới.
            if (node.Type == NodeType.End)
                return;

            await env.TraverseOutputsAsync(node);
        }
    }
}


