using System;
using FlowMy.Models;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private Services.Rendering.ConditionalNodeRenderer ConditionalNodeRendererService =>
            _conditionalNodeRenderer ?? throw new InvalidOperationException("ConditionalNodeRenderer service is not initialized.");

        private System.Windows.Controls.Border CreateConditionalNodeBorder(WorkflowNode node)
        {
            return ConditionalNodeRendererService.CreateConditionalNodeBorder(node);
        }

        private void RenderConditionalNodePorts(WorkflowNode node)
        {
            if (node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
            {
                ConditionalNodeRendererService.RenderDiamondNodePorts(node);
            }
            else
            {
                ConditionalNodeRendererService.RenderConditionalNodePorts(node);
            }
        }

        public void ReRenderConditionalNode(WorkflowNode node)
        {
            ConditionalNodeRendererService.ReRenderConditionalNode(node);
        }

        public void AddElseIfBranch(WorkflowNode node)
        {
            ConditionalNodeRendererService.AddElseIfBranch(node);
        }

        public void RemoveBranch(WorkflowNode node, ConditionalBranch branch)
        {
            ConditionalNodeRendererService.RemoveBranch(node, branch);
        }

        public void ApplyConditionalDiamondLayout(WorkflowNode node)
        {
            ConditionalNodeRendererService.ApplyDiamondLayout(node);
        }

        public void RestoreConditionalClassicLayout(WorkflowNode node)
        {
            ConditionalNodeRendererService.RestoreClassicLayout(node);
        }

        private void RefreshConditionalDiamondLineStyles()
        {
            ConditionalNodeRendererService.RefreshAllDiamondInternalLineStyles();
        }
    }
}

