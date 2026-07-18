using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlowMy.Models;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class DynamicUiNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is DynamicUiNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var dynamicUiNode = (DynamicUiNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            dynamicUiNode.ResolvedOutputs.Clear();

            try
            {
                // Trigger UI thread to read DOM from Sciter in-node control
                dynamicUiNode.PendingReadDom = true;

                // Wait for the UI thread to read the DOM and reset the flag (max 2 seconds)
                var maxWaitMs = 2000;
                var checkIntervalMs = 50;
                var waited = 0;

                while (waited < maxWaitMs)
                {
                    try
                    {
                        await Task.Delay(checkIntervalMs, env.CancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        dynamicUiNode.PendingReadDom = false;
                        return;
                    }
                    waited += checkIntervalMs;

                    if (!dynamicUiNode.PendingReadDom)
                    {
                        break;
                    }

                    if (dynamicUiNode.ResolvedOutputs.Count > 0)
                    {
                        break;
                    }
                }

                dynamicUiNode.PendingReadDom = false;

                // Publish outputs to execution scoped store (so downstream nodes can bind to them)
                if (!string.IsNullOrWhiteSpace(env.ExecutionId) && dynamicUiNode.ResolvedOutputs.Count > 0)
                {
                    var snapshot = new Dictionary<string, object?>(
                        dynamicUiNode.ResolvedOutputs, StringComparer.OrdinalIgnoreCase);
                    
                    env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, dynamicUiNode.Id, snapshot);
                }

                sw.Stop();
                env.OnNodeCompleted?.Invoke(dynamicUiNode, sw.Elapsed);
            }
            catch (Exception ex)
            {
                dynamicUiNode.PendingReadDom = false;
                System.Diagnostics.Debug.WriteLine($"[DynamicUiNodeExecutor] Error: {ex.Message}");
                env.OnNodeFailed?.Invoke(dynamicUiNode, ex.Message);
                throw;
            }

            // Traverse to next nodes in workflow
            await env.TraverseOutputsAsync(dynamicUiNode).ConfigureAwait(false);
        }
    }
}
