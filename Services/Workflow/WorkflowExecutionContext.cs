using System.Threading;

namespace FlowMy.Services.Workflow;

/// <summary>
/// AsyncLocal-backed ambient context for the currently-executing dispatch, so UI callbacks
/// (TraceNodeStarted/Completed/Failed) can resolve the right executionId even when multiple
/// AsyncTask dispatches race on a shared <c>WorkflowNode.LastExecutionId</c>.
/// </summary>
public static class WorkflowExecutionContext
{
    private static readonly AsyncLocal<string?> _currentExecutionId = new();

    /// <summary>ExecutionId of the dispatch currently being traversed in this async flow.</summary>
    public static string? CurrentExecutionId
    {
        get => _currentExecutionId.Value;
        set => _currentExecutionId.Value = value;
    }
}
