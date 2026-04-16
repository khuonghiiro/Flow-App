using FlowMy.Models;
using System.Linq;
using System.Windows.Controls;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        /// <summary>
        /// Đồng bộ z-index của TẤT CẢ ports theo z-index hiện tại của node border
        /// Gọi method này sau mọi thao tác thay đổi node structure
        /// </summary>
        private void SyncAllPortsZIndex(WorkflowNode node)
        {
            if (node?.Border == null) return;

            int currentNodeZIndex = Panel.GetZIndex(node.Border);

            // Sync tất cả ports (bao gồm input, output, branch ports)
            foreach (var port in node.Ports.Where(p => p.PortUI != null && p.IsVisible))
            {
                Panel.SetZIndex(port.PortUI, currentNodeZIndex + 1);
            }
        }
    }
}

