using FlowMy.Models;
using FlowMy.Services.Interaction;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class MouseEventNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is MouseEventNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var mouseNode = (MouseEventNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Parse mouse button
                if (!Enum.TryParse<MouseButton>(mouseNode.MouseButton, out var button))
                {
                    button = MouseButton.Left;
                }

                // Thực thi mouse event
                if (button == MouseButton.ScrollUp || button == MouseButton.ScrollDown)
                {
                    // ScrollUp/ScrollDown: chỉ dùng scrollSpeed, không dùng repeatCount
                    var scrollSpeed = mouseNode.ScrollSpeed;
                    env.Service.MouseInput.SendMouseScroll(button, scrollSpeed);
                }
                else
                {
                    // Left/Right/Middle: dùng repeatCount và holdDuration
                    // Lấy repeatCount từ DynamicInput (giống pattern keyboard guide), fallback về property
                    var repeatCount = env.Service.GetRepeatCountFromDynamicInputs(mouseNode, connections, env) ?? mouseNode.RepeatCount;
                    var holdDuration = mouseNode.HoldDuration;

                    // Click/giữ chuột: SendMouseClick đã xử lý repeatCount bên trong
                    // Không cần vòng lặp for vì SendMouseClick đã tự lặp repeatCount lần
                    env.Service.MouseInput.SendMouseClick(button, repeatCount, holdDuration);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MouseEvent error: {ex.Message}");
                env.OnNodeFailed?.Invoke(mouseNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(mouseNode, sw.Elapsed);

            await env.TraverseOutputsAsync(mouseNode);
        }
    }
}


