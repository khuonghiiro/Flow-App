// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System.Windows.Controls;
using FlowMy.Models;

namespace FlowMy.Services.Rendering
{
    public interface INodeRenderer
    {
        void RenderNode(WorkflowNode node, Canvas canvas);
        void UpdateNodePosition(WorkflowNode node, double x, double y);
        void RemoveNode(WorkflowNode node, Canvas canvas);
        void RemoveAllNodeVisuals(Canvas canvas);
    }
}

