using FlowMy.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;
using System.Windows;

namespace FlowMy.ViewModels;

public sealed partial class ExecutionTraceLogItemViewModel : ObservableObject
{
    public WorkflowNode Node { get; }
    public string NodeId { get; }
    public string NodeTitle { get; }
    public string NodeType { get; }
    public string IconKey { get; }
    public string NodeColorKey { get; }
    public string ExecutionId { get; }
    public string RootExecutionId { get; }
    public string? ParentNodeTitle { get; }
    public string? ParentNodeId { get; }
    public int Depth { get; }
    public string ExecutionShortId { get; }
    public Thickness IndentMargin { get; }
    public string TreePrefix { get; }
    public DateTime TimestampUtc { get; }

    [ObservableProperty]
    private string status = "running";

    [ObservableProperty]
    private string elapsedText = string.Empty;

    [ObservableProperty]
    private string inputSummary = string.Empty;

    [ObservableProperty]
    private string outputSummary = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public string TimestampText => TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff");

    public ExecutionTraceLogItemViewModel(
        WorkflowNode node,
        string executionId,
        string rootExecutionId,
        string iconKey,
        string nodeColorKey,
        string? parentNodeTitle,
        string? parentNodeId,
        int depth)
    {
        Node = node;
        NodeId = node.Id;
        NodeTitle = string.IsNullOrWhiteSpace(node.Title) ? node.Id : node.Title;
        NodeType = node.Type.ToString();
        IconKey = iconKey;
        NodeColorKey = nodeColorKey;
        ExecutionId = executionId;
        RootExecutionId = rootExecutionId;
        ParentNodeTitle = parentNodeTitle;
        ParentNodeId = parentNodeId;
        Depth = Math.Max(0, depth);
        ExecutionShortId = string.IsNullOrWhiteSpace(executionId)
            ? "-"
            : (executionId.Length > 12 ? executionId[..12] + "..." : executionId);
        IndentMargin = new Thickness(6 + (Depth * 16), 4, 6, 4);
        // TreePrefix = Depth <= 0
        //     ? string.Empty
        //     : string.Concat(Enumerable.Repeat("│  ", Math.Max(0, Depth - 1))) + "├─► ";

        TreePrefix = string.Empty;

        TimestampUtc = DateTime.UtcNow;
    }
}
