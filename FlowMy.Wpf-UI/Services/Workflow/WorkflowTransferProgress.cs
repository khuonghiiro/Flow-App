// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
namespace FlowMy.Services.Workflow;

/// <summary>Tiến trình export/import gói WebView2 (hiển thị trên UI).</summary>
public readonly record struct WorkflowTransferProgress(string Message, int Percent);
