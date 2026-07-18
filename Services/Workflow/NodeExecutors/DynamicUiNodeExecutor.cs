using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Views.Overlays;

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
                var tcs = new TaskCompletionSource<bool>();

                // 1. Resolve prefilled values from Input Mappings and BindToInput fields
                var prefilledValues = new Dictionary<string, string>();
                
                var mappings = dynamicUiNode.InputMappings ?? new List<CodeInputMapping>();
                foreach (var mapping in mappings)
                {
                    if (!string.IsNullOrWhiteSpace(mapping.SourceNodeId) && !string.IsNullOrWhiteSpace(mapping.SourceOutputKey))
                    {
                        var val = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                            env.Connections, mapping.SourceNodeId, mapping.SourceOutputKey, env);
                        
                        if (!string.IsNullOrEmpty(val) && !string.Equals(val.Trim(), "—", StringComparison.OrdinalIgnoreCase))
                        {
                            prefilledValues[mapping.EffectiveInputKey] = val;
                        }
                    }
                }

                foreach (var field in dynamicUiNode.Fields)
                {
                    if (field.BindToInput && !string.IsNullOrWhiteSpace(field.Key))
                    {
                        var val = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                            env.Connections, field.SourceNodeId, field.SourceOutputKey, env);
                        
                        if (!string.IsNullOrEmpty(val) && !string.Equals(val.Trim(), "—", StringComparison.OrdinalIgnoreCase))
                        {
                            prefilledValues[field.Key] = val;
                        }
                    }
                }

                DynamicUiPopupWindow? popup = null;

                // Handle cancellation token to close the window if execution gets cancelled
                using (env.CancellationToken.Register(() =>
                {
                    popup?.Close();
                    tcs.TrySetCanceled();
                }))
                {
                    // 2. Dispatch window showing to UI Thread
                    _ = Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            popup = new DynamicUiPopupWindow(dynamicUiNode, prefilledValues);
                            popup.Show();
                            
                            var result = await popup.WaitForSubmitAsync();
                            
                            if (result)
                            {
                                // Copy values to ResolvedOutputs
                                foreach (var kvp in popup.SubmittedValues)
                                {
                                    dynamicUiNode.ResolvedOutputs[kvp.Key] = kvp.Value;
                                }
                            }
                            
                            tcs.TrySetResult(result);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    });

                    var submitted = await tcs.Task;
                    if (!submitted)
                    {
                        throw new OperationCanceledException("User closed the form without submitting.");
                    }
                }

                // 3. Publish outputs to execution scoped store (so downstream nodes can bind to them)
                if (!string.IsNullOrWhiteSpace(env.ExecutionId) && dynamicUiNode.ResolvedOutputs.Count > 0)
                {
                    var snapshot = new Dictionary<string, object?>(
                        dynamicUiNode.ResolvedOutputs, StringComparer.OrdinalIgnoreCase);
                    
                    env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, dynamicUiNode.Id, snapshot);
                }

                sw.Stop();
                env.OnNodeCompleted?.Invoke(dynamicUiNode, sw.Elapsed);
            }
            catch (OperationCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DynamicUiNodeExecutor] Cancelled: {ex.Message}");
                env.OnNodeFailed?.Invoke(dynamicUiNode, ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DynamicUiNodeExecutor] Error: {ex.Message}");
                env.OnNodeFailed?.Invoke(dynamicUiNode, ex.Message);
                throw;
            }

            // 4. Traverse to next nodes in workflow
            await env.TraverseOutputsAsync(dynamicUiNode).ConfigureAwait(false);
        }
    }
}
