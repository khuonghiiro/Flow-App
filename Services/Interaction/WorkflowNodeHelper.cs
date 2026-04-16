using FlowMy.Models;
using FlowMy.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowMy.Services.Interaction
{
    /// <summary>
    /// Helper methods for working with workflow nodes, especially for finding connected nodes with DynamicOutputs.
    /// </summary>
    public static class WorkflowNodeHelper
    {
        private sealed record DataEdge(WorkflowNode OutputNode, WorkflowNode InputNode);

        /// <summary>
        /// Tìm tất cả các nodes có DynamicOutputs và có kết nối (trực tiếp hoặc gián tiếp) với target node.
        /// Logic: Tìm tất cả nodes upstream từ target node thông qua connections.
        /// </summary>
        public static List<WorkflowNode> GetConnectedProducerNodes(WorkflowEditorViewModel viewModel, WorkflowNode targetNode)
        {
            if (viewModel == null || targetNode == null) return new List<WorkflowNode>();

            // Include LoopBody nodes too
            var allNodes = viewModel.Nodes
                .Concat(viewModel.Nodes.OfType<LoopNode>().Select(l => l.LoopBodyNode).Where(n => n != null))
                .GroupBy(n => n.Id)
                .Select(g => g.First())
                .ToList();

            // Convert connections to data edges (Output -> Input)
            var edges = viewModel.Connections
                .Select(c => TryGetDataEdge(c))
                .Where(e => e != null)
                .Cast<DataEdge>()
                .ToList();

            // Find all incoming connections to target node
            var incoming = edges.Where(ed => ReferenceEquals(ed.InputNode, targetNode)).ToList();
            if (incoming.Count == 0)
            {
                return new List<WorkflowNode>();
            }

            // Get all upstream producer nodes (directly or indirectly connected)
            var producerNodes = incoming
                .SelectMany(ed => GetUpstreamProducers(ed.OutputNode, edges))
                .Where(n => HasDynamicOutputs(n))
                .Distinct()
                .ToList();

            return producerNodes;
        }

        /// <summary>
        /// Kiểm tra node có DynamicOutputs hay không (bao gồm InputNode).
        /// </summary>
        private static bool HasDynamicOutputs(WorkflowNode node)
        {
            if (node == null) return false;

            // InputNode luôn có output (Value)
            if (node is InputNode) return true;

            // Các node khác cần có DynamicOutputs
            return node.DynamicOutputs != null && node.DynamicOutputs.Count > 0;
        }

        /// <summary>
        /// Tìm tất cả upstream producer nodes từ một start node, đi ngược theo connections.
        /// </summary>
        private static IEnumerable<WorkflowNode> GetUpstreamProducers(WorkflowNode startOutput, List<DataEdge> edges)
        {
            var visited = new HashSet<WorkflowNode>();
            var q = new Queue<WorkflowNode>();

            visited.Add(startOutput);
            q.Enqueue(startOutput);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();

                foreach (var e in edges.Where(ed => ReferenceEquals(ed.InputNode, cur)))
                {
                    if (visited.Add(e.OutputNode))
                    {
                        q.Enqueue(e.OutputNode);
                    }
                }
            }

            return visited;
        }

        /// <summary>
        /// Convert WorkflowConnection thành DataEdge nếu là data connection (Output -> Input).
        /// </summary>
        private static DataEdge? TryGetDataEdge(WorkflowConnection c)
        {
            if (c.FromPort == null || c.ToPort == null) return null;

            if (!c.FromPort.IsInput && c.ToPort.IsInput)
            {
                return new DataEdge(c.FromNode, c.ToNode);
            }

            if (c.FromPort.IsInput && !c.ToPort.IsInput)
            {
                // Normalize direction to Output -> Input
                return new DataEdge(c.ToNode, c.FromNode);
            }

            return null;
        }
    }
}

