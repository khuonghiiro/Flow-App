using FlowMy.Models;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Interface chung cho tất cả executor xử lý logic thực thi 1 loại WorkflowNode.
    /// </summary>
    public interface INodeExecutor
    {
        /// <summary>
        /// Cho biết executor này có xử lý được node hiện tại hay không.
        /// </summary>
        bool CanExecute(WorkflowNode node);

        /// <summary>
        /// Thực thi logic của node. Điều hướng sang node tiếp theo thông qua env.ExecuteNextAsync().
        /// </summary>
        Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env);
    }
}


