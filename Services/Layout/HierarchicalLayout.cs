using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;

namespace FlowMy.Services.Layout
{
    /// <summary>
    /// Neural Network Style Layout - Hierarchical from left to right
    /// - Isolated nodes (no connections) are placed in a column on the left
    /// - Connected nodes are arranged in layers from left to right (like neural network)
    /// - Nodes in the same layer are evenly distributed vertically
    /// - Loop containers are placed below their header nodes
    /// </summary>
    public sealed class HierarchicalLayout : ILayoutAlgorithm
    {
        private readonly INodeRenderer _nodeRenderer;

        // Layout configuration
        private const double LayerSpacing = 350;           // Horizontal spacing between layers
        private const double NodeSpacing = 180;            // Vertical spacing between nodes in same layer
        private const double IsolatedNodeSpacing = 150;    // Spacing for isolated nodes
        private const double LeftMargin = 100;             // Left margin for isolated nodes
        private const double TopMargin = 100;              // Top margin

        // Loop container configuration
        private const double LoopBodyVerticalOffset = 280;
        private const double LoopBodyMinWidth = 400;
        private const double LoopBodyMinHeight = 300;
        private const double LoopBodyPadding = 100;

        public HierarchicalLayout(INodeRenderer nodeRenderer)
        {
            _nodeRenderer = nodeRenderer ?? throw new ArgumentNullException(nameof(nodeRenderer));
        }

        private class LayoutNode
        {
            public WorkflowNode Node { get; set; } = null!;
            public Point Position { get; set; }
            public int Layer { get; set; } = -1;
            public int IndexInLayer { get; set; }
            public List<LayoutNode> Children { get; set; } = new();
            public List<LayoutNode> Parents { get; set; } = new();
            public bool IsIsolated { get; set; }
            public bool IsLoopBody { get; set; }
            public bool IsLoopHeader { get; set; }
            public LayoutNode? LoopParent { get; set; }
            public double Weight { get; set; } = 1.0;  // For sorting within layer
            public double BarycenterY { get; set; }    // For reducing edge crossings
        }

        public void ApplyLayout(IEnumerable<WorkflowNode> nodes, IEnumerable<WorkflowConnection> connections, Point centerPoint)
        {
            var nodeList = nodes.Distinct().ToList();
            if (nodeList.Count == 0) return;

            var nodeSet = new HashSet<WorkflowNode>(nodeList);
            var connList = connections
                .Where(c => c.FromNode != null && c.ToNode != null)
                .Where(c => nodeSet.Contains(c.FromNode) && nodeSet.Contains(c.ToNode))
                .ToList();

            // 1. Build layout network
            var layoutNodes = BuildLayoutNetwork(nodeSet, connList);

            // 2. Separate isolated and connected nodes
            var isolatedNodes = layoutNodes.Values.Where(n => n.IsIsolated && !n.IsLoopBody).ToList();
            var connectedNodes = layoutNodes.Values.Where(n => !n.IsIsolated && !n.IsLoopBody).ToList();

            // 3. Assign layers to connected nodes (BFS from roots)
            AssignLayers(layoutNodes, connectedNodes);

            // 4. Identify loop relationships
            IdentifyLoopRelationships(layoutNodes);

            // 5. Calculate positions
            double startX = centerPoint.X - 400;  // Start position
            double startY = centerPoint.Y;

            // Place isolated nodes on the left
            PlaceIsolatedNodes(isolatedNodes, startX - 500, startY);

            // Place connected nodes in neural network style
            PlaceConnectedNodesNeuralStyle(layoutNodes, connectedNodes, startX, startY);

            // 6. Place loop bodies below their headers
            PlaceLoopBodies(layoutNodes);

            // 7. Reduce edge crossings by reordering within layers
            ReduceEdgeCrossings(layoutNodes, connectedNodes);

            // 8. Final adjustment - ensure no overlaps
            EnsureNoOverlaps(layoutNodes);

            // 9. Apply positions to nodes
            ApplyPositions(layoutNodes);

            // 10. Adjust loop containers
            AdjustLoopContainers(layoutNodes);
        }

        private Dictionary<WorkflowNode, LayoutNode> BuildLayoutNetwork(
            HashSet<WorkflowNode> nodes,
            List<WorkflowConnection> connections)
        {
            var layoutNodes = new Dictionary<WorkflowNode, LayoutNode>();

            foreach (var node in nodes)
            {
                layoutNodes[node] = new LayoutNode
                {
                    Node = node,
                    IsLoopBody = node is LoopBodyNode,
                    IsLoopHeader = node is LoopNode
                };
            }

            // Build parent-child relationships
            foreach (var conn in connections)
            {
                if (layoutNodes.TryGetValue(conn.FromNode, out var from) &&
                    layoutNodes.TryGetValue(conn.ToNode, out var to))
                {
                    // Skip connections to/from loop body for layer calculation
                    if (!from.IsLoopBody && !to.IsLoopBody)
                    {
                        from.Children.Add(to);
                        to.Parents.Add(from);
                    }
                }
            }

            // Mark isolated nodes (no parents and no children, excluding loop bodies)
            foreach (var node in layoutNodes.Values)
            {
                if (!node.IsLoopBody)
                {
                    node.IsIsolated = node.Parents.Count == 0 && node.Children.Count == 0;
                }
            }

            return layoutNodes;
        }

        private void AssignLayers(Dictionary<WorkflowNode, LayoutNode> layoutNodes, List<LayoutNode> connectedNodes)
        {
            if (connectedNodes.Count == 0) return;

            // Find roots (nodes with no parents among connected nodes)
            var roots = connectedNodes.Where(n => n.Parents.Count == 0).ToList();

            // If no roots found, pick the first connected node or Start node
            if (roots.Count == 0)
            {
                var startNode = connectedNodes.FirstOrDefault(n =>
                    n.Node.Type == NodeType.Start ||
                    n.Node.Id?.Contains("Start", StringComparison.OrdinalIgnoreCase) == true);

                if (startNode != null)
                {
                    roots.Add(startNode);
                }
                else
                {
                    roots.Add(connectedNodes.First());
                }
            }

            // BFS to assign layers
            var queue = new Queue<LayoutNode>();
            var visited = new HashSet<LayoutNode>();

            foreach (var root in roots)
            {
                root.Layer = 0;
                queue.Enqueue(root);
                visited.Add(root);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var child in current.Children)
                {
                    if (!visited.Contains(child))
                    {
                        // Layer is max of all parents' layers + 1
                        int maxParentLayer = child.Parents
                            .Where(p => p.Layer >= 0)
                            .Select(p => p.Layer)
                            .DefaultIfEmpty(-1)
                            .Max();

                        child.Layer = maxParentLayer + 1;
                        visited.Add(child);
                        queue.Enqueue(child);
                    }
                }
            }

            // Handle any remaining unvisited nodes (cycles or disconnected components)
            foreach (var node in connectedNodes.Where(n => !visited.Contains(n)))
            {
                node.Layer = 0;  // Default to layer 0
            }
        }

        private void IdentifyLoopRelationships(Dictionary<WorkflowNode, LayoutNode> layoutNodes)
        {
            foreach (var node in layoutNodes.Values.Where(n => n.IsLoopHeader))
            {
                if (node.Node is LoopNode loopNode && loopNode.LoopBodyNode != null)
                {
                    if (layoutNodes.TryGetValue(loopNode.LoopBodyNode, out var bodyNode))
                    {
                        bodyNode.LoopParent = node;
                    }
                }
            }
        }

        private void PlaceIsolatedNodes(List<LayoutNode> isolatedNodes, double startX, double centerY)
        {
            if (isolatedNodes.Count == 0) return;

            // Sort by node type and title for consistency
            isolatedNodes = isolatedNodes
                .OrderBy(n => n.Node.Type)
                .ThenBy(n => n.Node.Title ?? n.Node.Id)
                .ToList();

            double totalHeight = (isolatedNodes.Count - 1) * IsolatedNodeSpacing;
            double startY = centerY - totalHeight / 2;

            for (int i = 0; i < isolatedNodes.Count; i++)
            {
                isolatedNodes[i].Position = new Point(startX, startY + i * IsolatedNodeSpacing);
                isolatedNodes[i].IndexInLayer = i;
            }
        }

        private void PlaceConnectedNodesNeuralStyle(
            Dictionary<WorkflowNode, LayoutNode> layoutNodes,
            List<LayoutNode> connectedNodes,
            double startX,
            double centerY)
        {
            if (connectedNodes.Count == 0) return;

            // Group nodes by layer
            var layers = connectedNodes
                .Where(n => n.Layer >= 0)
                .GroupBy(n => n.Layer)
                .OrderBy(g => g.Key)
                .ToList();

            if (layers.Count == 0) return;

            // Calculate positions for each layer
            foreach (var layer in layers)
            {
                var nodesInLayer = layer.OrderBy(n => n.Node.Title ?? n.Node.Id).ToList();
                int layerIndex = layer.Key;

                // Calculate X position for this layer
                double layerX = startX + layerIndex * LayerSpacing;

                // Calculate Y positions - evenly distributed, centered
                double totalHeight = (nodesInLayer.Count - 1) * NodeSpacing;
                double layerStartY = centerY - totalHeight / 2;

                for (int i = 0; i < nodesInLayer.Count; i++)
                {
                    var node = nodesInLayer[i];
                    node.Position = new Point(layerX, layerStartY + i * NodeSpacing);
                    node.IndexInLayer = i;
                }
            }
        }

        private void PlaceLoopBodies(Dictionary<WorkflowNode, LayoutNode> layoutNodes)
        {
            var loopBodies = layoutNodes.Values.Where(n => n.IsLoopBody && n.LoopParent != null).ToList();

            foreach (var body in loopBodies)
            {
                var header = body.LoopParent!;

                // Use LoopBodyNode's SyncPositionWithParent logic
                if (body.Node is LoopBodyNode bodyNode && header.Node is LoopNode loopNode)
                {
                    // Temporarily set parent position to calculate body position
                    loopNode.X = header.Position.X;
                    loopNode.Y = header.Position.Y;
                    bodyNode.SyncPositionWithParent();

                    body.Position = new Point(bodyNode.X, bodyNode.Y);
                }
                else
                {
                    // Fallback: Place body directly below header
                    body.Position = new Point(
                        header.Position.X,
                        header.Position.Y + LoopBodyVerticalOffset
                    );
                }
            }
        }

        /// <summary>
        /// Reduce edge crossings using barycenter method
        /// </summary>
        private void ReduceEdgeCrossings(Dictionary<WorkflowNode, LayoutNode> layoutNodes, List<LayoutNode> connectedNodes)
        {
            var layers = connectedNodes
                .Where(n => n.Layer >= 0)
                .GroupBy(n => n.Layer)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (layers.Count < 2) return;

            // Multiple passes for better results
            for (int pass = 0; pass < 5; pass++)
            {
                // Forward pass (layer 0 to n)
                for (int i = 1; i < layers.Count; i++)
                {
                    if (!layers.TryGetValue(i, out var currentLayer)) continue;
                    if (!layers.TryGetValue(i - 1, out var prevLayer)) continue;

                    ReorderLayerByBarycenter(currentLayer, prevLayer, true);
                }

                // Backward pass (layer n to 0)
                for (int i = layers.Count - 2; i >= 0; i--)
                {
                    if (!layers.TryGetValue(i, out var currentLayer)) continue;
                    if (!layers.TryGetValue(i + 1, out var nextLayer)) continue;

                    ReorderLayerByBarycenter(currentLayer, nextLayer, false);
                }
            }

            // Update positions after reordering
            foreach (var layerGroup in layers)
            {
                var nodesInLayer = layerGroup.Value;
                if (nodesInLayer.Count == 0) continue;

                // Keep X, recalculate Y
                double layerX = nodesInLayer[0].Position.X;
                double centerY = nodesInLayer.Average(n => n.Position.Y);
                double totalHeight = (nodesInLayer.Count - 1) * NodeSpacing;
                double startY = centerY - totalHeight / 2;

                for (int i = 0; i < nodesInLayer.Count; i++)
                {
                    nodesInLayer[i].Position = new Point(layerX, startY + i * NodeSpacing);
                    nodesInLayer[i].IndexInLayer = i;
                }
            }
        }

        private void ReorderLayerByBarycenter(List<LayoutNode> currentLayer, List<LayoutNode> adjacentLayer, bool useParents)
        {
            foreach (var node in currentLayer)
            {
                var neighbors = useParents ? node.Parents : node.Children;
                var relevantNeighbors = neighbors.Where(n => adjacentLayer.Contains(n)).ToList();

                if (relevantNeighbors.Count > 0)
                {
                    // Barycenter = average Y position of neighbors
                    node.BarycenterY = relevantNeighbors.Average(n => n.Position.Y);
                }
                else
                {
                    // Keep current position
                    node.BarycenterY = node.Position.Y;
                }
            }

            // Sort by barycenter
            var sorted = currentLayer.OrderBy(n => n.BarycenterY).ToList();
            currentLayer.Clear();
            currentLayer.AddRange(sorted);
        }

        private void EnsureNoOverlaps(Dictionary<WorkflowNode, LayoutNode> layoutNodes)
        {
            var allNodes = layoutNodes.Values.ToList();
            double minDistance = NodeSpacing * 0.8;

            for (int iter = 0; iter < 50; iter++)
            {
                bool hadOverlap = false;

                for (int i = 0; i < allNodes.Count; i++)
                {
                    for (int j = i + 1; j < allNodes.Count; j++)
                    {
                        var n1 = allNodes[i];
                        var n2 = allNodes[j];

                        // Skip if different layers (X spacing should be enough)
                        if (Math.Abs(n1.Position.X - n2.Position.X) > LayerSpacing * 0.5) continue;

                        // Skip loop body - they have special positioning
                        if (n1.IsLoopBody || n2.IsLoopBody) continue;

                        double dy = Math.Abs(n1.Position.Y - n2.Position.Y);

                        if (dy < minDistance)
                        {
                            hadOverlap = true;
                            double push = (minDistance - dy) / 2 + 5;

                            if (n1.Position.Y < n2.Position.Y)
                            {
                                n1.Position = new Point(n1.Position.X, n1.Position.Y - push);
                                n2.Position = new Point(n2.Position.X, n2.Position.Y + push);
                            }
                            else
                            {
                                n1.Position = new Point(n1.Position.X, n1.Position.Y + push);
                                n2.Position = new Point(n2.Position.X, n2.Position.Y - push);
                            }
                        }
                    }
                }

                if (!hadOverlap) break;
            }
        }

        private void ApplyPositions(Dictionary<WorkflowNode, LayoutNode> layoutNodes)
        {
            foreach (var layoutNode in layoutNodes.Values)
            {
                // Don't apply position to loop body - it will be handled separately
                if (layoutNode.IsLoopBody) continue;

                _nodeRenderer.UpdateNodePosition(
                    layoutNode.Node,
                    layoutNode.Position.X,
                    layoutNode.Position.Y
                );
            }
        }

        private void AdjustLoopContainers(Dictionary<WorkflowNode, LayoutNode> layoutNodes)
        {
            var loopHeaders = layoutNodes.Values.Where(n => n.IsLoopHeader).ToList();

            foreach (var header in loopHeaders)
            {
                if (header.Node is LoopNode loopNode && loopNode.LoopBodyNode != null)
                {
                    if (!layoutNodes.TryGetValue(loopNode.LoopBodyNode, out var body)) continue;

                    // Find all nodes that are inside this loop body
                    var nodesInBody = FindNodesInLoopBody(layoutNodes, header, body);

                    if (nodesInBody.Count > 0)
                    {
                        // Calculate bounding box
                        double minX = nodesInBody.Min(n => n.Position.X) - LoopBodyPadding;
                        double maxX = nodesInBody.Max(n => n.Position.X) + LoopBodyPadding;
                        double minY = nodesInBody.Min(n => n.Position.Y) - LoopBodyPadding;
                        double maxY = nodesInBody.Max(n => n.Position.Y) + LoopBodyPadding;

                        // Ensure minimum size
                        double width = Math.Max(LoopBodyMinWidth, maxX - minX);
                        double height = Math.Max(LoopBodyMinHeight, maxY - minY);

                        // Position container
                        loopNode.LoopBodyNode.X = minX;
                        loopNode.LoopBodyNode.Y = minY;
                        loopNode.LoopBodyNode.Width = width;
                        loopNode.LoopBodyNode.Height = height;
                    }
                    else
                    {
                        // No nodes in body - use default size, positioned below header
                        loopNode.LoopBodyNode.X = header.Position.X - LoopBodyMinWidth / 2;
                        loopNode.LoopBodyNode.Y = header.Position.Y + LoopBodyVerticalOffset - 50;
                        loopNode.LoopBodyNode.Width = LoopBodyMinWidth;
                        loopNode.LoopBodyNode.Height = LoopBodyMinHeight;
                    }

                    // Update renderer
                    _nodeRenderer.UpdateNodePosition(
                        loopNode.LoopBodyNode,
                        loopNode.LoopBodyNode.X,
                        loopNode.LoopBodyNode.Y
                    );
                }
            }
        }

        /// <summary>
        /// Find nodes that belong to a loop body (connected through LoopBodyLeft port)
        /// </summary>
        private List<LayoutNode> FindNodesInLoopBody(
            Dictionary<WorkflowNode, LayoutNode> layoutNodes,
            LayoutNode header,
            LayoutNode body)
        {
            var result = new List<LayoutNode>();
            var visited = new HashSet<LayoutNode> { body, header };
            var queue = new Queue<LayoutNode>();

            // Start from nodes connected to the loop body's children
            foreach (var child in body.Children)
            {
                if (!visited.Contains(child))
                {
                    queue.Enqueue(child);
                    visited.Add(child);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // Don't include other loop headers or loop bodies
                if (current.IsLoopHeader || current.IsLoopBody) continue;

                result.Add(current);

                // Add children (but not back to parent loop)
                foreach (var child in current.Children)
                {
                    if (!visited.Contains(child) && child != header)
                    {
                        queue.Enqueue(child);
                        visited.Add(child);
                    }
                }

                // Add connected nodes through parents too (for bidirectional connections in loop)
                foreach (var parent in current.Parents)
                {
                    if (!visited.Contains(parent) && parent != header && !parent.IsLoopBody)
                    {
                        queue.Enqueue(parent);
                        visited.Add(parent);
                    }
                }
            }

            return result;
        }
    }
}
