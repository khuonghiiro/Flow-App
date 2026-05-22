using FlowMy.Models;
using FlowMy.Services.Interaction;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho MacroRecorderNode: phát lại chuỗi thao tác chuột/bàn phím đã ghi.
    /// </summary>
    internal sealed class MacroRecorderNodeExecutor : INodeExecutor
    {
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public bool CanExecute(WorkflowNode node) => node is MacroRecorderNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var macroNode = (MacroRecorderNode)node;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Nếu JSON rỗng, chỉ traverse và kết thúc — không throw
            if (string.IsNullOrWhiteSpace(macroNode.MacroDataJson))
            {
                sw.Stop();
                env.OnNodeCompleted?.Invoke(macroNode, sw.Elapsed);
                await env.TraverseOutputsAsync(node);
                return;
            }

            // Parse JSON
            List<MacroAction>? actions;
            try
            {
                actions = JsonSerializer.Deserialize<List<MacroAction>>(macroNode.MacroDataJson, _jsonOptions);
            }
            catch (JsonException ex)
            {
                env.OnNodeFailed?.Invoke(macroNode, ex.Message);
                throw;
            }

            if (actions == null || actions.Count == 0)
            {
                sw.Stop();
                env.OnNodeCompleted?.Invoke(macroNode, sw.Elapsed);
                await env.TraverseOutputsAsync(node);
                return;
            }

            // Xác định số chu kỳ phát lại
            int cycles = macroNode.PlaybackMode == MacroPlaybackMode.Once ? 1 : macroNode.RepeatCount;

            try
            {
                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    env.CancellationToken.ThrowIfCancellationRequested();

                    // Delay giữa các chu kỳ (bỏ qua chu kỳ đầu tiên)
                    if (cycle > 0 && macroNode.RepeatIntervalMs > 0)
                    {
                        await Task.Delay(macroNode.RepeatIntervalMs, env.CancellationToken);
                    }

                    for (int i = 0; i < actions.Count; i++)
                    {
                        env.CancellationToken.ThrowIfCancellationRequested();

                        // Delay theo delta timestamp (bỏ qua action đầu tiên)
                        if (i > 0)
                        {
                            long delta = actions[i].Timestamp - actions[i - 1].Timestamp;
                            // Nếu delta <= 0 (clock skew hoặc cùng timestamp) thì bỏ qua delay
                            if (delta > 0)
                            {
                                int delayMs = (int)Math.Min(delta, int.MaxValue);
                                await Task.Delay(delayMs, env.CancellationToken);
                            }
                        }

                        var action = actions[i];
                        switch (action.Type)
                        {
                            case "MouseClick":
                                SetCursorPos(action.X, action.Y);
                                if (Enum.TryParse<MouseButton>(action.Button, ignoreCase: true, out var mouseButton))
                                {
                                    env.Service.MouseInput.SendMouseClick(mouseButton, 1, 0);
                                }
                                else
                                {
                                    // Mặc định Left nếu không parse được
                                    env.Service.MouseInput.SendMouseClick(MouseButton.Left, 1, 0);
                                }
                                break;

                            case "KeyPress":
                                if (!string.IsNullOrWhiteSpace(action.Key))
                                {
                                    env.Service.KeyboardInput.SendKeyPress(action.Key, 1, 0);
                                }
                                break;

                            case "MouseMove":
                                SetCursorPos(action.X, action.Y);
                                break;

                            default:
                                System.Diagnostics.Debug.WriteLine($"MacroRecorderNodeExecutor: Unknown action type '{action.Type}', skipping.");
                                break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MacroRecorderNodeExecutor error: {ex.Message}");
                env.OnNodeFailed?.Invoke(macroNode, ex.Message);
                throw;
            }

            // Publish output vào scoped store
            if (!string.IsNullOrWhiteSpace(macroNode.OutputKey) && !string.IsNullOrWhiteSpace(env.ExecutionId))
            {
                env.Service.SetScopedNodeStringOutput(env.ExecutionId, macroNode.Id, macroNode.OutputKey.Trim(), macroNode.MacroDataJson);
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(macroNode, sw.Elapsed);

            await env.TraverseOutputsAsync(node);
        }
    }
}
