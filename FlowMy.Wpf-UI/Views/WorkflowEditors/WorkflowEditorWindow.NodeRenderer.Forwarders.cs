using System;
using FlowMy.Models;
using FlowMy.Services.Interaction;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private Services.Rendering.NodeRenderer NodeRendererService =>
            _nodeRenderer as Services.Rendering.NodeRenderer
            ?? throw new InvalidOperationException("NodeRenderer service is not initialized.");

        private void RenderAllNodes()
        {
            NodeRendererService.RenderAllNodes();
            RenderAllConnections();
        }

        private void RenderNode(WorkflowNode node)
        {
            _nodeRenderer?.RenderNode(node, WorkflowCanvas);
            
            // Cập nhật viewport culling sau khi render node
            _viewportCullingService?.OnNodeChanged(node);
        }

        // Forwarder methods still referenced elsewhere
        private void RenderNodePorts(WorkflowNode node)
        {
            NodeRendererService.RenderNodePorts(node, WorkflowCanvas);
        }

        void IWorkflowEditorHost.UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            NodeRendererService.UpdateNodePosition(node, x, y);
        }

        private void UpdatePortsPositionOnSide(WorkflowNode node, PortPosition position)
        {
            _portRenderer?.UpdatePortsPositionOnSide(node, position);
        }

        private void TogglePortsVisibility(WorkflowNode node)
        {
            _portRenderer?.TogglePortsVisibility(node);
        }
    }
}

