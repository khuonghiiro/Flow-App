using System.Collections.Generic;
using FlowMy.Models;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class HotkeyPressEventNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is HotkeyPressEventNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var hotkeyNode = (HotkeyPressEventNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var hotkeyText = hotkeyNode.Key;

            // Get repeat count from DynamicInputs if available, otherwise use property
            var repeatCount = env.Service.GetRepeatCountFromDynamicInputs(hotkeyNode, connections, env) ?? hotkeyNode.RepeatCount;
            var delayMs = hotkeyNode.PressDelayMs;

            if (!string.IsNullOrWhiteSpace(hotkeyText))
            {
                try
                {
                    env.Service.KeyboardInput.SendHotkeyPress(hotkeyText, repeatCount, delayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sending hotkey press '{hotkeyText}': {ex.Message}");
                    env.OnNodeFailed?.Invoke(hotkeyNode, ex.Message);
                    throw;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"HotkeyPressEventNode: Hotkey text is empty or null");
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(hotkeyNode, sw.Elapsed);

            await env.Service.TraverseSingleOutputAndLegacyAsync(
                hotkeyNode, connections, env.CancellationToken,
                env.OnEnteringNode, env.OnNodeStarted, env.OnNodeCompleted,
                env.OnNodeFailed, env.ReachableToEnd,
                env.ExecutionId, env.FlowScopeId, env.BranchId, env.ParentFlowScopeId,
                new List<string>(env.ExecutionPath));
        }
    }
}


