using FlowMy.Models;
using System;
using System.Collections.Generic;

namespace FlowMy.Services.Workflow;

public interface IWorkflowExecutionVisualizer
{
    /// <summary>
    /// Reset toàn bộ trạng thái hiển thị kết quả/timing cho các node.
    /// </summary>
    void ResetVisualization(IEnumerable<WorkflowNode> nodes);

    /// <summary>
    /// Gọi khi một node bắt đầu thực thi.
    /// </summary>
    /// <param name="manualRunSessionId">Id phiên chạy thủ công / lane (để nhiều luồng trùng node không gộp timer).</param>
    void OnNodeStarted(WorkflowNode node, string? manualRunSessionId = null);

    /// <summary>
    /// Gọi khi một node hoàn thành thực thi.
    /// </summary>
    void OnNodeCompleted(WorkflowNode node, TimeSpan elapsed, string? manualRunSessionId = null);

    /// <summary>
    /// Dừng timer hiển thị cho mọi node thuộc một phiên (ví dụ user hủy phiên đó).
    /// </summary>
    void CancelTimersForManualRunSession(string? manualRunSessionId);

    /// <summary>
    /// Gọi khi việc thực thi workflow bị hủy.
    /// </summary>
    void OnExecutionCancelled();

    /// <summary>
    /// Gọi khi một node thực thi bị lỗi (exception). Hiển thị lỗi trên node và execution dừng tại node đó.
    /// </summary>
    void OnNodeFailed(WorkflowNode node, string errorMessage);

    /// <summary>
    /// Cập nhật UI kết quả output cho các node (sau khi load workflow đã lưu đầy đủ output).
    /// </summary>
    void RefreshSavedOutputs(IEnumerable<WorkflowNode> nodes);
}


