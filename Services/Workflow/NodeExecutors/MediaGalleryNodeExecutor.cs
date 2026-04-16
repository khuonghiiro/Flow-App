using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class MediaGalleryNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is MediaGalleryNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var galleryNode = (MediaGalleryNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            WorkflowNode? fromNode = null;
            string? keyToUse = null;

            // Ưu tiên: dùng node + key đã chọn trong dialog (Nguồn JSON)
            if (!string.IsNullOrWhiteSpace(galleryNode.JsonSourceNodeId))
            {
                fromNode = env.ReachableToEnd.FirstOrDefault(n => string.Equals(n.Id, galleryNode.JsonSourceNodeId, System.StringComparison.OrdinalIgnoreCase));
                keyToUse = string.IsNullOrWhiteSpace(galleryNode.JsonSourceOutputKey) ? null : galleryNode.JsonSourceOutputKey.Trim();
            }

            // Fallback: dùng kết nối từ input port
            if (fromNode == null)
            {
                var inputPort = galleryNode.Ports.FirstOrDefault(p => p.IsInput && p.IsVisible);
                if (inputPort != null)
                {
                    var conn = connections.FirstOrDefault(c =>
                        c.ToNode == galleryNode &&
                        c.FromNode != null &&
                        (c.ToPort == null || ReferenceEquals(c.ToPort, inputPort) ||
                         (c.ToPort != null && c.ToPort.IsInput && string.Equals(c.ToPort.Id, inputPort.Id, System.StringComparison.OrdinalIgnoreCase))));
                    if (conn != null)
                    {
                        fromNode = conn.FromNode!;
                        keyToUse = !string.IsNullOrWhiteSpace(conn.FromPort?.Id) ? conn.FromPort!.Id!.Trim() : "output";
                    }
                }
            }

            if (fromNode != null)
            {
                string? jsonString = null;

                if (galleryNode.CanReexecuteSourceNode)
                {
                    // Checked: chạy lại logic node nguồn như behavior cũ.
                    await env.Service.ExecuteNodeLogicOnlyAsync(fromNode, connections, env.CancellationToken);
                    jsonString = GetJsonByKey(fromNode, keyToUse, env);
                }
                else
                {
                    // Unchecked: KHÔNG chạy lại logic node nguồn, chỉ lấy Output hiện tại.
                    jsonString = GetJsonByKey(fromNode, keyToUse, env);
                }

                if (!string.IsNullOrWhiteSpace(jsonString) && jsonString != "—")
                {
                    galleryNode.LastJson = jsonString;
                    var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                    dispatcher.Invoke(() =>
                    {
                        MediaGalleryJsonHelper.ParseAndFill(jsonString, galleryNode);
                    });
                }
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(galleryNode, sw.Elapsed);

            await env.TraverseOutputsAsync(galleryNode);
        }

        /// <summary>Lấy chuỗi JSON từ node nguồn theo đúng key đã chọn (không thử hết key, tránh lấy sai dữ liệu).</summary>
        private static string GetJsonByKey(WorkflowNode fromNode, string? key, NodeExecutionEnvironment env)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                var value = env.Service.ResolveDynamicValueForExecution(fromNode, key, env);
                if (!string.IsNullOrWhiteSpace(value) && value != "—")
                    return value;
            }
            return string.Empty;
        }
    }
}
