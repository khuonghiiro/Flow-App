using System;

namespace FlowMy.Services.Workflow
{
    /// <summary>
    /// Node model có thể implement để đẩy thêm cặp key→chuỗi vào snapshot scoped theo <c>ExecutionId</c>
    /// mà không cần sửa switch trong <see cref="WorkflowExecutionService"/> (sau bước mirror mặc định).
    /// </summary>
    public interface IWorkflowScopedOutputContributor
    {
        /// <param name="set">Gọi <c>set(key, value)</c> cho mỗi output; <paramref name="value"/> có thể null.</param>
        void AppendScopedStringOutputs(Action<string, string?> set);
    }
}
