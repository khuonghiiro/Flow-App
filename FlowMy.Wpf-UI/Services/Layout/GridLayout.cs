using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FlowMy.Models;
using FlowMy.Services.Rendering;

namespace FlowMy.Services.Layout
{
    public sealed class GridLayout : ILayoutAlgorithm
    {
        private readonly INodeRenderer _nodeRenderer;
        private readonly int _nodesPerRow;
        private readonly double _spacing;

        public GridLayout(INodeRenderer nodeRenderer, int nodesPerRow = 4, double spacing = 60)
        {
            _nodeRenderer = nodeRenderer ?? throw new ArgumentNullException(nameof(nodeRenderer));
            _nodesPerRow = Math.Max(1, nodesPerRow);
            _spacing = Math.Max(0, spacing);
        }

        /// <summary>
        /// Apply simple grid layout for unconnected nodes.
        /// Updates node positions via <see cref="INodeRenderer.UpdateNodePosition"/>.
        /// </summary>
        public void ApplyLayout(IEnumerable<WorkflowNode> nodes, IEnumerable<WorkflowConnection> connections, Point centerPoint)
        {
            var nodeList = nodes.Distinct().ToList();
            if (nodeList.Count == 0) return;

            const double nodeWidth = 150;
            const double nodeHeight = 80;
            double spacing = _spacing;
            int nodesPerRow = _nodesPerRow;

            double currentX = centerPoint.X - ((nodesPerRow * (nodeWidth + spacing)) / 2);
            double currentY = centerPoint.Y;

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (i > 0 && i % nodesPerRow == 0)
                {
                    currentX = centerPoint.X - ((nodesPerRow * (nodeWidth + spacing)) / 2);
                    currentY += nodeHeight + spacing;
                }

                var node = nodeList[i];
                _nodeRenderer.UpdateNodePosition(node, currentX, currentY);

                currentX += nodeWidth + spacing;
            }
        }
    }
}

