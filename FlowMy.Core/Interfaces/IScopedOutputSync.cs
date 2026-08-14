// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
namespace FlowMy.Interfaces
{
    /// <summary>
    /// Giao diện đồng bộ output của node theo ExecutionId trong runtime execution
    /// </summary>
    public interface IScopedOutputSync
    {
        void SetScopedNodeStringOutput(string executionId, string nodeId, string key, string value);
    }
}
