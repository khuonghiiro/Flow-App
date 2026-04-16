using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Globalization;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class DelayNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is DelayNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var delayNode = (DelayNode)node;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var delayMs = ResolveDelayMilliseconds(delayNode, env);
            await Task.Delay(delayMs, env.CancellationToken);
            sw.Stop();
            env.OnNodeCompleted?.Invoke(delayNode, sw.Elapsed);

            await env.TraverseOutputsAsync(delayNode);
        }

        private static int ResolveDelayMilliseconds(DelayNode delayNode, NodeExecutionEnvironment env)
        {
            return delayNode.TimingMode switch
            {
                DelayTimingMode.None => delayNode.DelayMilliseconds,
                DelayTimingMode.Random => ComputeRandomMilliseconds(delayNode),
                DelayTimingMode.NodeKey => ComputeNodeKeyMilliseconds(delayNode, env),
                _ => delayNode.DelayMilliseconds
            };
        }

        private static double UnitMultiplier(DelayTimeUnit unit) => unit switch
        {
            DelayTimeUnit.Milliseconds => 1d,
            DelayTimeUnit.Seconds => 1000d,
            DelayTimeUnit.Minutes => 60_000d,
            DelayTimeUnit.Hours => 3_600_000d,
            _ => 1000d
        };

        private static int ComputeRandomMilliseconds(DelayNode delayNode)
        {
            var mult = UnitMultiplier(delayNode.DelayUnit);
            var minMs = delayNode.RandomMinValue * mult;
            var maxMs = delayNode.RandomMaxValue * mult;
            if (minMs > maxMs)
                (minMs, maxMs) = (maxMs, minMs);
            if (maxMs <= 0)
                return 0;
            if (minMs < 0)
                minMs = 0;

            var span = maxMs - minMs;
            var r = Random.Shared.NextDouble() * span + minMs;
            return ClampToIntMilliseconds(r);
        }

        private static int ComputeNodeKeyMilliseconds(DelayNode delayNode, NodeExecutionEnvironment env)
        {
            var raw = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                env.Connections,
                delayNode.DelaySourceNodeId,
                delayNode.DelaySourceOutputKey,
                env);
            var v = ParseDelayNumeric(raw, defaultValue: 1d);
            var ms = v * UnitMultiplier(delayNode.DelayUnit);
            return ClampToIntMilliseconds(ms);
        }

        private static double ParseDelayNumeric(string? s, double defaultValue)
        {
            if (string.IsNullOrWhiteSpace(s))
                return defaultValue;
            s = s.Trim();
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out d))
                return d;
            return defaultValue;
        }

        private static int ClampToIntMilliseconds(double ms)
        {
            if (double.IsNaN(ms) || double.IsInfinity(ms))
                return 0;
            if (ms <= 0)
                return 0;
            if (ms >= int.MaxValue)
                return int.MaxValue;
            return (int)Math.Round(ms);
        }
    }
}
