using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class NotificationNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is NotificationNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var n = (NotificationNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string Resolve(InputVariable v)
                {
                    if (string.IsNullOrWhiteSpace(v.SourceNodeId) ||
                        string.IsNullOrWhiteSpace(v.SourceOutputKey))
                        return string.Empty;

                    // Tìm source node tương tự OutputNodeExecutor (ưu tiên direct connection, fallback tìm theo Id)
                    WorkflowNode? src = null;

                    var upstreamConnection = env.Connections
                        .FirstOrDefault(c =>
                            c.ToNode == n &&
                            c.FromNode != null &&
                            c.FromNode.Id == v.SourceNodeId);

                    src = upstreamConnection?.FromNode;

                    if (src == null)
                    {
                        src = env.Connections
                            .SelectMany(c => new[] { c.FromNode, c.ToNode })
                            .FirstOrDefault(x => x != null && x.Id == v.SourceNodeId);
                    }

                    if (src == null) return string.Empty;

                    var val = env.Service.ResolveDynamicValueForExecution(src, v.SourceOutputKey, env);
                    return val == "—" ? string.Empty : (val ?? string.Empty);
                }

                var title = Resolve(n.TitleInput);
                var content = Resolve(n.ContentInput);
                var durationStr = Resolve(n.DurationInput);

                if (string.IsNullOrWhiteSpace(title))
                    title = string.IsNullOrWhiteSpace(n.StaticTitle)
                        ? (n.Title ?? "Notification")
                        : n.StaticTitle;

                if (string.IsNullOrWhiteSpace(content))
                    content = n.StaticContent;

                if (!int.TryParse(durationStr, out var durationSeconds) || durationSeconds < 1)
                    durationSeconds = n.DefaultDurationSeconds;

                ToastNotificationService.ShowToast(
                    title,
                    content,
                    durationSeconds,
                    n.ToastTitleColorKey,
                    n.ToastContentColorKey,
                    n.ToastBackgroundColorKey,
                    n.ToastBackgroundOpacity);

                sw.Stop();
                env.OnNodeCompleted?.Invoke(n, sw.Elapsed);
                await env.TraverseOutputsAsync(n);
            }
            catch (Exception ex)
            {
                sw.Stop();
                env.OnNodeFailed?.Invoke(n, ex.Message);
                throw;
            }
        }
    }
}

