using FlowMy.Models;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class StringSplitNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is Models.Nodes.StringSplitNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var stringSplitNode = (Models.Nodes.StringSplitNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Resolve input string
                var input = stringSplitNode.DynamicInputs.FirstOrDefault(i => i.Key == "inputString");
                string inputString = string.Empty;

                if (input != null && !string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
                {
                    // Ưu tiên: tìm direct upstream connection tới StringSplit (đúng với hầu hết các node thường)
                    var upstreamConnection = connections
                        .FirstOrDefault(c =>
                            c.ToNode == stringSplitNode &&
                            c.FromNode != null &&
                            c.FromNode.Id == input.SelectedSourceNodeId);

                    WorkflowNode? srcNode = upstreamConnection?.FromNode;

                    // Trong LoopBody: SelectedSourceNodeId có thể là LoopNode, nhưng connection thực tế đi qua LoopBody.
                    // Khi đó không có direct connection FromNode == LoopNode → StringSplit.
                    // Fallback: tìm node có Id đúng SelectedSourceNodeId trong toàn bộ graph và đọc value trực tiếp từ node đó.
                    if (srcNode == null)
                    {
                        srcNode = connections
                            .SelectMany(c => new[] { c.FromNode, c.ToNode })
                            .FirstOrDefault(n => n != null && n.Id == input.SelectedSourceNodeId);
                    }

                    if (srcNode != null)
                    {
                        var key = input.SelectedSourceOutputKey ?? input.Key;
                        inputString = env.Service.ResolveDynamicValueForExecution(srcNode, key, env);
                    }
                }

                // Split string using regex
                if (!string.IsNullOrWhiteSpace(inputString))
                {
                    var pattern = string.IsNullOrWhiteSpace(stringSplitNode.RegexPattern)
                        ? @"\r?\n"
                        : stringSplitNode.RegexPattern;

                    var splitItems = System.Text.RegularExpressions.Regex.Split(inputString, pattern);

                    // Filter out empty, null, or whitespace-only items
                    stringSplitNode.SplitResult = splitItems
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Select(item => item.Trim())
                        .ToList();
                }
                else
                {
                    stringSplitNode.SplitResult = new List<string>();
                }

                if (!string.IsNullOrWhiteSpace(env.ExecutionId))
                {
                    var outputKey = string.IsNullOrWhiteSpace(stringSplitNode.OutputKey)
                        ? "ListItems"
                        : stringSplitNode.OutputKey.Trim();
                    env.Service.SetScopedNodeStringOutput(
                        env.ExecutionId,
                        stringSplitNode.Id,
                        outputKey,
                        System.Text.Json.JsonSerializer.Serialize(stringSplitNode.SplitResult));
                }

                System.Diagnostics.Debug.WriteLine($"StringSplitNode: Split into {stringSplitNode.SplitResult.Count} items");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StringSplitNode error: {ex.Message}");
                stringSplitNode.SplitResult = new List<string>();
                env.OnNodeFailed?.Invoke(stringSplitNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(stringSplitNode, sw.Elapsed);

            await env.TraverseOutputsAsync(stringSplitNode);
        }
    }
}


