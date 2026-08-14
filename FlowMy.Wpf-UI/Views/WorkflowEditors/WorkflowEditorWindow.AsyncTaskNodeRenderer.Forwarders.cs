using System;
using FlowMy.Models;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private Services.Rendering.AsyncTaskNodeRenderer AsyncTaskNodeRendererService =>
            _asyncTaskNodeRenderer ?? throw new InvalidOperationException("AsyncTaskNodeRenderer service is not initialized.");

        private System.Windows.Controls.Border CreateAsyncTaskNodeBorder(WorkflowNode node)
        {
            return AsyncTaskNodeRendererService.CreateAsyncTaskNodeBorder(node);
        }

        private void RenderAsyncTaskNodePorts(WorkflowNode node)
        {
            AsyncTaskNodeRendererService.RenderAsyncTaskNodePorts(node);
        }

        public void ReRenderAsyncTaskNode(WorkflowNode node)
        {
            AsyncTaskNodeRendererService.ReRenderAsyncTaskNode(node);
        }

        public void AddTaskBranch(WorkflowNode node)
        {
            AsyncTaskNodeRendererService.AddTaskBranch(node);
        }

        public void RemoveTaskBranch(WorkflowNode node, AsyncTaskBranch branch)
        {
            AsyncTaskNodeRendererService.RemoveTaskBranch(node, branch);
        }
    }
}

