using FlowMy.Models;
using System.Collections.Generic;

namespace FlowMy.Services.Workflow;

public sealed class WorkflowLoadResult
{
    public string Name { get; init; } = string.Empty;
    public List<WorkflowNode> Nodes { get; init; } = new();
    public List<WorkflowConnection> Connections { get; init; } = new();

    // View state properties
    public double ZoomLevel { get; init; } = 1.0;
    public double PanX { get; init; } = 0.0;
    public double PanY { get; init; } = 0.0;
    public double? SavedScreenWidth { get; init; }
    public double? SavedScreenHeight { get; init; }
    public double? SavedViewportCenterX { get; init; }
    public double? SavedViewportCenterY { get; init; }

    /// <summary>
    /// Kiểu vẽ đường kết nối (Bezier/Orthogonal/Straight) đã lưu trong workflow.
    /// </summary>
    public string? ConnectionLineStyle { get; init; }

    /// <summary>Tên file .webpkg.zip (cùng thư mục JSON) nếu có trong file import.</summary>
    public string? PortableWebBundleFileName { get; init; }
}

public interface IWorkflowPersistenceService
{
    IReadOnlyList<string> GetAllWorkflowNames();

    void Save(
        string workflowName,
        IEnumerable<WorkflowNode> nodes,
        IEnumerable<WorkflowConnection> connections,
        double zoomLevel = 1.0,
        double panX = 0.0,
        double panY = 0.0,
        double? savedScreenWidth = null,
        double? savedScreenHeight = null,
        double? savedViewportCenterX = null,
        double? savedViewportCenterY = null,
        string? connectionLineStyle = null);

    WorkflowLoadResult? Load(string workflowName);

    string ExportToJson(
        string workflowName,
        IEnumerable<WorkflowNode> nodes,
        IEnumerable<WorkflowConnection> connections,
        double zoomLevel = 1.0,
        double panX = 0.0,
        double panY = 0.0,
        double? savedScreenWidth = null,
        double? savedScreenHeight = null,
        double? savedViewportCenterX = null,
        double? savedViewportCenterY = null,
        string? connectionLineStyle = null,
        string? portableWebBundleFileName = null);

    WorkflowLoadResult? ImportFromJson(string json);
}


