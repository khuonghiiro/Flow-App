using System.Collections.Generic;
using FlowMy.Models;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class KeyPressEventNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is KeyPressEventNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var keyNode = (KeyPressEventNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var keyText = keyNode.Key;

            // Get repeat count from DynamicInputs if available, otherwise use property
            var repeatCount = env.Service.GetRepeatCountFromDynamicInputs(keyNode, connections, env) ?? keyNode.RepeatCount;
            var delayMs = keyNode.PressDelayMs;

            if (!string.IsNullOrWhiteSpace(keyText))
            {
                try
                {
                    env.Service.KeyboardInput.SendKeyPress(keyText, repeatCount, delayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sending key press '{keyText}': {ex.Message}");
                    env.OnNodeFailed?.Invoke(keyNode, ex.Message);
                    throw;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"KeyPressEventNode: Key text is empty or null");
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(keyNode, sw.Elapsed);

            await env.Service.TraverseSingleOutputAndLegacyAsync(
                keyNode, connections, env.CancellationToken,
                env.OnEnteringNode, env.OnNodeStarted, env.OnNodeCompleted,
                env.OnNodeFailed, env.ReachableToEnd,
                env.ExecutionId, env.FlowScopeId, env.BranchId, env.ParentFlowScopeId,
                new List<string>(env.ExecutionPath));
        }
    }
}


