using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using FlowMy.Models;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utilities;

namespace FlowMy.Services.Layout
{
    public sealed class AutoLayoutService
    {
        private readonly HierarchicalLayout _hierarchicalLayout;
        private readonly GridLayout _gridLayout;
        private readonly IConnectionRenderer _connectionRenderer;
        private readonly MinimapService _minimapService;

        public AutoLayoutService(
            HierarchicalLayout hierarchicalLayout,
            GridLayout gridLayout,
            IConnectionRenderer connectionRenderer,
            MinimapService minimapService)
        {
            _hierarchicalLayout = hierarchicalLayout ?? throw new ArgumentNullException(nameof(hierarchicalLayout));
            _gridLayout = gridLayout ?? throw new ArgumentNullException(nameof(gridLayout));
            _connectionRenderer = connectionRenderer ?? throw new ArgumentNullException(nameof(connectionRenderer));
            _minimapService = minimapService ?? throw new ArgumentNullException(nameof(minimapService));
        }

        /// <summary>
        /// Orchestrate auto layout:
        /// 1) Partition connected vs unconnected nodes
        /// 2) Apply hierarchical layout to connected nodes
        /// 3) Apply grid layout to unconnected nodes
        /// 4) Redraw connections
        /// 5) Update minimap
        /// 6) Fit to view
        /// </summary>
        public void AutoArrange(
            ObservableCollection<WorkflowNode> nodes,
            ObservableCollection<WorkflowConnection> connections,
            Point centerPoint)
        {
            var nodeList = nodes.Distinct().ToList();
            if (nodeList.Count == 0) return;

            var connList = connections.ToList();

            var connected = new HashSet<WorkflowNode>();
            foreach (var conn in connList)
            {
                if (conn.FromNode != null) connected.Add(conn.FromNode);
                if (conn.ToNode != null) connected.Add(conn.ToNode);
            }

            var connectedNodes = nodeList.Where(connected.Contains).ToList();
            var unconnectedNodes = nodeList.Where(n => !connected.Contains(n)).ToList();

            if (connectedNodes.Count > 0)
            {
                _hierarchicalLayout.ApplyLayout(connectedNodes, connList, centerPoint);
            }

            if (unconnectedNodes.Count > 0)
            {
                var gridCenter = connectedNodes.Count > 0 ? new Point(centerPoint.X, centerPoint.Y + 500) : centerPoint;
                _gridLayout.ApplyLayout(unconnectedNodes, connList, gridCenter);
            }

            _connectionRenderer.UpdateAllConnectionPaths(connections);
            _minimapService.Update();
            _minimapService.FitToView();
        }
    }
}

