using FlowMy.Models;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        public void ApplyAsyncTaskLoopLikeLayout(AsyncTaskNode node)
        {
            var vm = ViewModel;
            if (vm == null) return;

            // Remove manual-mode port UI first to avoid stale port circles staying on canvas.
            var canvas = WorkflowCanvas;
            foreach (var p in node.Ports.ToList())
            {
                if (p.PortUI != null && canvas.Children.Contains(p.PortUI))
                    canvas.Children.Remove(p.PortUI);
                if (p.PortUI != null) p.PortUI = null;
            }

            // Cleanup connections that are tied to the *current* AsyncTask interface ports
            // (manual input + manual task output ports).
            // These connections reference old NodePort objects and their PortUI will be removed/replaced.
            foreach (var p in vm.Connections.SelectMany(c => new[] { c.FromPort, c.ToPort }).Where(pp => pp != null).OfType<NodePort>().ToList())
            {
                // no-op: kept to preserve original structure
            }

            foreach (var c in vm.Connections.ToList())
            {
                if (c.FromPort != null && node.Ports.Contains(c.FromPort)) { _connectionRenderer.RemoveConnectionVisuals(c); vm.Connections.Remove(c); continue; }
                if (c.ToPort != null && node.Ports.Contains(c.ToPort)) { _connectionRenderer.RemoveConnectionVisuals(c); vm.Connections.Remove(c); continue; }
            }

            node.UiPresentationMode = AsyncTaskUiPresentationMode.LoopLikeDispatch;
            _templateFactory.ConfigureAsyncTaskLoopLikePorts(node);
            ReRenderAsyncTaskNode(node);
        }

        public void RestoreAsyncTaskManualLayout(AsyncTaskNode node)
        {
            var vm = ViewModel;
            if (vm == null) return;

            // Remove loop-like header ports + body chrome/ports first.
            var canvas = WorkflowCanvas;
            foreach (var p in node.Ports.ToList())
            {
                if (p.PortUI != null && canvas.Children.Contains(p.PortUI))
                    canvas.Children.Remove(p.PortUI);
                if (p.PortUI != null) p.PortUI = null;
            }

            if (node.ContainerBorder != null && canvas.Children.Contains(node.ContainerBorder))
                canvas.Children.Remove(node.ContainerBorder);

            if (node.AsyncTaskBodyNode != null)
            {
                foreach (var bp in node.AsyncTaskBodyNode.Ports.ToList())
                {
                    if (bp.PortUI != null && canvas.Children.Contains(bp.PortUI))
                        canvas.Children.Remove(bp.PortUI);
                    bp.PortUI = null;
                }
            }

            if (node.AsyncTaskBodyNode != null)
            {
                foreach (var c in vm.Connections.Where(x => x.FromNode == node.AsyncTaskBodyNode || x.ToNode == node.AsyncTaskBodyNode).ToList())
                {
                    _connectionRenderer.RemoveConnectionVisuals(c);
                    vm.Connections.Remove(c);
                }
            }

            foreach (var p in node.Ports.ToList())
            {
                foreach (var c in vm.Connections.Where(x => x.FromPort == p || x.ToPort == p).ToList())
                {
                    _connectionRenderer.RemoveConnectionVisuals(c);
                    vm.Connections.Remove(c);
                }
            }

            node.DefaultConnection = null;
            node.UiPresentationMode = AsyncTaskUiPresentationMode.ManualBranches;
            _templateFactory.ConfigureAsyncTaskManualPorts(node);
            ReRenderAsyncTaskNode(node);
        }
    }
}
