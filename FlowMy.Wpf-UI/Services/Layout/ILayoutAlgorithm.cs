// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System.Collections.Generic;
using System.Windows;
using FlowMy.Models;

namespace FlowMy.Services.Layout
{
    public interface ILayoutAlgorithm
    {
        void ApplyLayout(
            IEnumerable<WorkflowNode> nodes,
            IEnumerable<WorkflowConnection> connections,
            Point centerPoint);
    }
}

