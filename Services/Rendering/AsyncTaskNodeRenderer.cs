using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class AsyncTaskNodeRenderer : INodeRenderer
    {
        private static readonly Dictionary<int, string> ColorPortsByIndex = new()
        {
            { 0, "ChocolateBrown" },
            { 1, "OceanBlue" },
            { 2, "EmeraldGreen" },
            { 3, "SunsetOrange" },
            { 4, "RoyalPurple" },
            { 5, "RubyRed" },
            { 6, "GoldenYellow" },
            { 7, "TealCyan" },
            { 8, "LavenderDream" },
            { 9, "CrimsonRose" },
            { 10, "SlateGray" },
            { 11, "MintFresh" },
            { 12, "IndigoNight" },
            { 13, "PeachSoft" },
            { 14, "SkyAzure" },
            { 15, "CherryBlossom" },
            { 16, "ForestPine" },
            { 17, "AmberWarm" },
            { 18, "MidnightBlue" },
            { 19, "LimeBright" },
            { 20, "MagentaBold" },
            { 21, "BronzeMetal" },
            { 22, "AquaMarine" },
            { 23, "Terracotta" },
            { 24, "VioletDeep" },
            { 25, "OliveGreen" }
        };

        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly PortRenderer _portRenderer;

        private IWorkflowEditorHost _host => _hostAccessor.GetRequiredHost();

        // Keep track of which port UI elements were last rendered for a given AsyncTask.
        // This lets us remove stale port ellipses when switching between modes (manual <-> loop-like),
        // even if the underlying NodePort objects were removed from node.Ports by the template.
        private static readonly Dictionary<string, HashSet<NodePort>> _renderedPortsByAsyncTaskNodeId = new();

        public AsyncTaskNodeRenderer(IWorkflowEditorHostAccessor hostAccessor, PortRenderer portRenderer)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        }

        private static bool IsLoopLike(AsyncTaskNode n) =>
            n.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch && n.AsyncTaskBodyNode != null;

        private void RememberRenderedPorts(AsyncTaskNode asyncTaskNode)
        {
            if (string.IsNullOrWhiteSpace(asyncTaskNode.Id)) return;

            var set = new HashSet<NodePort>(asyncTaskNode.Ports);
            if (asyncTaskNode.AsyncTaskBodyNode != null)
            {
                foreach (var p in asyncTaskNode.AsyncTaskBodyNode.Ports)
                    set.Add(p);
            }

            _renderedPortsByAsyncTaskNodeId[asyncTaskNode.Id] = set;
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not AsyncTaskNode asyncTaskNode)
                throw new InvalidOperationException("AsyncTaskNodeRenderer can only render AsyncTaskNode.");

            if (!IsLoopLike(asyncTaskNode))
            {
                RenderManualAsyncTask(asyncTaskNode, canvas);
                return;
            }

            asyncTaskNode.Border = CreateAsyncTaskNodeBorder(asyncTaskNode);
            NodeChrome.Apply(asyncTaskNode.Border, asyncTaskNode, _host);
            // Avoid bitmap cache artifacts on the diamond chrome during drag/move.
            // Using isDragging:true disables BitmapCache in GpuOptimizationHelper.
            GpuOptimizationHelper.ApplyToBorder(asyncTaskNode.Border, isDragging: true);

            asyncTaskNode.Border.MouseDown += _host.NodeMouseDown;
            asyncTaskNode.Border.MouseMove += _host.NodeMouseMove;
            asyncTaskNode.Border.MouseUp += _host.NodeMouseUp;
            asyncTaskNode.Border.MouseEnter += _host.NodeBorderMouseEnter;
            asyncTaskNode.Border.MouseLeave += _host.NodeBorderMouseLeave;
            asyncTaskNode.Border.ContextMenu = _host.CreateNodeContextMenu(asyncTaskNode);

            Canvas.SetLeft(asyncTaskNode.Border, asyncTaskNode.X);
            Canvas.SetTop(asyncTaskNode.Border, asyncTaskNode.Y);
            canvas.Children.Add(asyncTaskNode.Border);
            _host.ZIndexManager.InitializeNodeZIndex(asyncTaskNode, asyncTaskNode.Border);

            asyncTaskNode.Border.SizeChanged += (_, _) =>
            {
                RenderLoopLikeAsyncTaskPorts(asyncTaskNode, canvas);
                if (_host.ViewModel == null) return;
                foreach (var conn in _host.ViewModel.Connections.Where(c => c.FromNode == asyncTaskNode || c.ToNode == asyncTaskNode))
                    _host.UpdateConnectionPath(conn);
            };

            asyncTaskNode.ContainerBorder = LoopContainerControl.CreateAsyncTaskContainer(asyncTaskNode);
            AttachAsyncTaskContainerHandlers(asyncTaskNode);

            var body = asyncTaskNode.AsyncTaskBodyNode!;
            if (body.X == 0 && body.Y == 0)
                body.SyncPositionWithParent();

            Canvas.SetLeft(asyncTaskNode.ContainerBorder, body.X);
            Canvas.SetTop(asyncTaskNode.ContainerBorder, body.Y);
            _host.ZIndexManager.InitializeNodeZIndex(body, asyncTaskNode.ContainerBorder);
            canvas.Children.Add(asyncTaskNode.ContainerBorder);
            body.Border = asyncTaskNode.ContainerBorder;

            asyncTaskNode.Border.Loaded += (_, _) =>
            {
                RenderLoopLikeAsyncTaskPorts(asyncTaskNode, canvas);
                RenderAsyncTaskBodyPorts(asyncTaskNode, canvas);
                CreateDefaultAsyncTaskBodyConnection(asyncTaskNode);

                if (_host.ViewModel == null) return;
                var related = _host.ViewModel.Connections
                    .Where(c => c.FromNode == asyncTaskNode || c.ToNode == asyncTaskNode
                        || c.FromNode == body || c.ToNode == body)
                    .ToList();
                foreach (var conn in related)
                    _host.UpdateConnectionPath(conn);

                // Now body ports are created/positioned -> remember port UI set for next mode switch cleanup.
                RememberRenderedPorts(asyncTaskNode);
            };
        }

        private void RenderManualAsyncTask(AsyncTaskNode asyncTaskNode, Canvas canvas)
        {
            asyncTaskNode.Border = CreateAsyncTaskNodeBorder(asyncTaskNode);

            Canvas.SetLeft(asyncTaskNode.Border, asyncTaskNode.X);
            Canvas.SetTop(asyncTaskNode.Border, asyncTaskNode.Y);
            canvas.Children.Add(asyncTaskNode.Border);

            _host.ZIndexManager.InitializeNodeZIndex(asyncTaskNode, asyncTaskNode.Border);
            RenderAsyncTaskNodePorts(asyncTaskNode);

            // Manual mode: ports are created immediately.
            RememberRenderedPorts(asyncTaskNode);
        }

        public void UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            if (node is not AsyncTaskNode asyncTaskNode) return;

            asyncTaskNode.X = x;
            asyncTaskNode.Y = y;

            if (asyncTaskNode.Border != null)
            {
                var transform = asyncTaskNode.Border.RenderTransform as TranslateTransform;
                if (transform == null || (transform.X == 0 && transform.Y == 0))
                {
                    Canvas.SetLeft(asyncTaskNode.Border, x);
                    Canvas.SetTop(asyncTaskNode.Border, y);
                }
            }

            if (IsLoopLike(asyncTaskNode))
            {
                const double diamondWidth = 100;
                const double diamondHeight = 100;
                const double portRadius = 9.0;

                foreach (var port in asyncTaskNode.Ports.Where(p => p.IsVisible))
                {
                    Point portPosition;
                    switch (port.Position)
                    {
                        case PortPosition.Left:
                            portPosition = new Point(x, y + diamondHeight / 2);
                            break;
                        case PortPosition.Right:
                            portPosition = new Point(x + diamondWidth, y + diamondHeight / 2);
                            break;
                        case PortPosition.Bottom:
                            portPosition = new Point(x + diamondWidth / 2, y + diamondHeight);
                            break;
                        case PortPosition.Top:
                            portPosition = new Point(x + diamondWidth / 2, y);
                            break;
                        default:
                            portPosition = new Point(x, y);
                            break;
                    }

                    port.PositionPoint = portPosition;
                    if (port.PortUI != null)
                    {
                        Canvas.SetLeft(port.PortUI, portPosition.X - portRadius);
                        Canvas.SetTop(port.PortUI, portPosition.Y - portRadius);
                    }

                    _portRenderer.EnsurePortAddedToCanvas(port);
                    if (port.PortUI != null)
                        _host.ZIndexManager.SetPortZIndex(asyncTaskNode, port.PortUI);
                }

                UpdateAsyncTaskBodyPortsPosition(asyncTaskNode);

                if (_host.ViewModel != null)
                {
                    foreach (var conn in _host.ViewModel.Connections.Where(c => c.FromNode == asyncTaskNode || c.ToNode == asyncTaskNode || c.FromNode == asyncTaskNode.AsyncTaskBodyNode || c.ToNode == asyncTaskNode.AsyncTaskBodyNode))
                        _host.UpdateConnectionPath(conn);
                }

                _host.SyncAllPortsZIndex(asyncTaskNode);
                return;
            }

            RenderAsyncTaskNodePorts(asyncTaskNode);
            _host.SyncAllPortsZIndex(asyncTaskNode);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not AsyncTaskNode asyncTaskNode) return;

            DetachLoopLikeVisuals(asyncTaskNode, canvas);

            if (asyncTaskNode.Border != null && canvas.Children.Contains(asyncTaskNode.Border))
                canvas.Children.Remove(asyncTaskNode.Border);

            foreach (var port in asyncTaskNode.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    canvas.Children.Remove(port.PortUI);
            }
        }

        private void DetachLoopLikeVisuals(AsyncTaskNode node, Canvas canvas)
        {
            if (node.ContainerBorder != null && canvas.Children.Contains(node.ContainerBorder))
                canvas.Children.Remove(node.ContainerBorder);
            node.ContainerBorder = null;

            if (node.AsyncTaskBodyNode == null) return;
            foreach (var port in node.AsyncTaskBodyNode.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Remove(port.PortUI);
                    port.PortUI = null;
                }
            }
        }

        public void RemoveAllNodeVisuals(Canvas canvas)
        {
            var borders = canvas.Children.OfType<Border>().Where(b => b.Tag is WorkflowNode).ToList();
            foreach (var border in borders)
                canvas.Children.Remove(border);

            var ports = canvas.Children
                .OfType<Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18))
                .ToList();
            foreach (var port in ports)
                canvas.Children.Remove(port);
        }

        public Border CreateAsyncTaskNodeBorder(WorkflowNode node)
        {
            if (node is AsyncTaskNode at && IsLoopLike(at))
            {
                var border = AsyncTaskLoopDiamondControl.CreateBorder(at, _host.OwnerWindow, _host);
                border.LayoutUpdated += (_, _) => _host.SyncAllPortsZIndex(node);
                return border;
            }

            var manualBorder = AsyncTaskNodeControl.CreateBorder(
                node,
                _host as Window,
                _host,
                addTaskBranch: () => AddTaskBranch(node),
                removeBranch: b => RemoveTaskBranch(node, b));
            NodeChrome.Apply(manualBorder, node, _host);

            manualBorder.MouseDown += _host.NodeMouseDown;
            manualBorder.MouseMove += _host.NodeMouseMove;
            manualBorder.MouseUp += _host.NodeMouseUp;
            manualBorder.MouseEnter += _host.NodeBorderMouseEnter;
            manualBorder.MouseLeave += _host.NodeBorderMouseLeave;
            manualBorder.ContextMenu = _host.CreateNodeContextMenu(node);

            manualBorder.LayoutUpdated += (_, _) => _host.SyncAllPortsZIndex(node);

            return manualBorder;
        }

        public void AddTaskBranch(WorkflowNode node)
        {
            if (node is AsyncTaskNode at && IsLoopLike(at)) return;

            var newBranch = new AsyncTaskBranch
            {
                Label = "Task",
                CanRemove = true
            };

            var portPosition = node.AsyncTaskBranches.FirstOrDefault()?.Port?.Position ?? PortPosition.Right;
            var newPort = new NodePort
            {
                IsInput = false,
                Position = portPosition,
                IsVisible = true,
                ExecutionMode = (node as AsyncTaskNode)?.RunInParallel == true
                    ? PortExecutionMode.Parallel
                    : PortExecutionMode.Sequential
            };
            newBranch.Port = newPort;
            node.Ports.Add(newPort);
            node.AsyncTaskBranches.Add(newBranch);

            UpdateTaskExecutionOrder(node);
            ReRenderAsyncTaskNode(node);
            _host.SyncAllPortsZIndex(node);
        }

        public void RemoveTaskBranch(WorkflowNode node, AsyncTaskBranch branch)
        {
            if (node is AsyncTaskNode at && IsLoopLike(at)) return;
            if (!branch.CanRemove) return;

            var viewModel = _host.ViewModel;
            if (branch.Port != null && viewModel != null)
            {
                var connectionsToRemove = viewModel.Connections
                    .Where(c => c.FromPort == branch.Port || c.ToPort == branch.Port)
                    .ToList();

                foreach (var conn in connectionsToRemove)
                    viewModel.Connections.Remove(conn);
            }

            if (branch.Port?.PortUI != null && _host.WorkflowCanvas.Children.Contains(branch.Port.PortUI))
                _host.WorkflowCanvas.Children.Remove(branch.Port.PortUI);

            if (branch.Port != null)
                node.Ports.Remove(branch.Port);

            node.AsyncTaskBranches.Remove(branch);

            UpdateTaskExecutionOrder(node);
            ReRenderAsyncTaskNode(node);
            _host.SyncAllPortsZIndex(node);
        }

        public void ReRenderAsyncTaskNode(WorkflowNode node)
        {
            if (node is not AsyncTaskNode asyncTaskNode) return;

            var canvas = _host.WorkflowCanvas;

            // 1) Remove stale port UI from the previous render (manual <-> loop-like switching).
            if (_renderedPortsByAsyncTaskNodeId.TryGetValue(asyncTaskNode.Id, out var oldPorts))
            {
                foreach (var p in oldPorts)
                {
                    if (p?.PortUI != null && canvas.Children.Contains(p.PortUI))
                    {
                        canvas.Children.Remove(p.PortUI);
                    }
                    if (p != null) p.PortUI = null;
                }
                _renderedPortsByAsyncTaskNodeId.Remove(asyncTaskNode.Id);
            }

            // 2) Remove loop-like body/container visuals if they still exist (e.g. switching back to manual).
            if (asyncTaskNode.ContainerBorder != null ||
                (asyncTaskNode.AsyncTaskBodyNode?.Ports?.Any(p => p.PortUI != null) == true))
            {
                DetachLoopLikeVisuals(asyncTaskNode, canvas);
            }

            UpdateTaskExecutionOrder(node);

            if (node.Border != null && canvas.Children.Contains(node.Border))
                _host.WorkflowCanvas.Children.Remove(node.Border);

            foreach (var branch in node.AsyncTaskBranches)
            {
                if (branch.Port?.PortUI != null && _host.WorkflowCanvas.Children.Contains(branch.Port.PortUI))
                    _host.WorkflowCanvas.Children.Remove(branch.Port.PortUI);
            }

            if (IsLoopLike(asyncTaskNode))
            {
                RenderNode(asyncTaskNode, _host.WorkflowCanvas);
            }
            else
            {
                var newBorder = CreateAsyncTaskNodeBorder(node);
                node.Border = newBorder;

                Canvas.SetLeft(newBorder, node.X);
                Canvas.SetTop(newBorder, node.Y);
                _host.WorkflowCanvas.Children.Add(newBorder);

                _host.ZIndexManager.InitializeNodeZIndex(node, newBorder);
                RenderAsyncTaskNodePorts(node);
                _host.ZIndexManager.RaiseNodeZIndex(node, Panel.GetZIndex(node.Border));
            }

            var viewModel = _host.ViewModel;
            if (viewModel != null)
            {
                foreach (var conn in viewModel.Connections)
                {
                    if (conn.FromNode == node || conn.ToNode == node
                        || (asyncTaskNode.AsyncTaskBodyNode != null && (conn.FromNode == asyncTaskNode.AsyncTaskBodyNode || conn.ToNode == asyncTaskNode.AsyncTaskBodyNode)))
                        _host.UpdateConnectionPath(conn);
                }
            }

            _host.UpdateMinimap();

            _host.Dispatcher.BeginInvoke(new Action(() => _host.SyncAllPortsZIndex(node)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static void UpdateTaskExecutionOrder(WorkflowNode node)
        {
            if (node is not AsyncTaskNode asyncTaskNode) return;

            int order = 0;
            var executionMode = asyncTaskNode.RunInParallel ? PortExecutionMode.Parallel : PortExecutionMode.Sequential;

            foreach (var branch in asyncTaskNode.AsyncTaskBranches)
            {
                if (branch.Port != null)
                {
                    branch.Port.ExecutionMode = executionMode;
                    branch.Port.ExecutionOrder = order++;
                }
            }
        }

        private void RenderLoopLikeAsyncTaskPorts(AsyncTaskNode asyncTaskNode, Canvas canvas)
        {
            if (asyncTaskNode.Border == null) return;

            if (asyncTaskNode.Border.ActualWidth == 0 || asyncTaskNode.Border.ActualHeight == 0)
            {
                asyncTaskNode.Border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                asyncTaskNode.Border.Arrange(new Rect(asyncTaskNode.Border.DesiredSize));
            }

            var nodeX = asyncTaskNode.X;
            var nodeY = asyncTaskNode.Y;
            const double diamondWidth = 100;
            const double diamondHeight = 100;
            const double portRadius = 9.0;

            var indexPort = asyncTaskNode.Ports.FirstOrDefault(p => p.Id == "LoopIndexOut");
            if (indexPort != null)
                indexPort.IsVisible = false;

            var visiblePorts = asyncTaskNode.Ports.Where(p => p.IsVisible).ToList();

            foreach (var port in visiblePorts)
            {
                Color? portSpecificColor = null;
                if (!string.IsNullOrEmpty(port.ColorKey))
                {
                    var brush = Application.Current.TryFindResource(port.ColorKey) as SolidColorBrush
                                ?? Application.Current.TryFindResource(port.ColorKey + "Brush") as SolidColorBrush;
                    if (brush != null) portSpecificColor = brush.Color;
                }

                var nodeColor = (asyncTaskNode.NodeBrush as SolidColorBrush)?.Color;
                var finalColor = portSpecificColor ?? nodeColor ?? (port.IsInput ? Colors.Orange : Colors.Cyan);

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(finalColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is Shape shape)
                    shape.Fill = new SolidColorBrush(finalColor);

                Point portPosition;
                switch (port.Position)
                {
                    case PortPosition.Left:
                        portPosition = new Point(nodeX, nodeY + diamondHeight / 2);
                        break;
                    case PortPosition.Right:
                        portPosition = new Point(nodeX + diamondWidth, nodeY + diamondHeight / 2);
                        break;
                    case PortPosition.Bottom:
                        portPosition = new Point(nodeX + diamondWidth / 2, nodeY + diamondHeight);
                        break;
                    case PortPosition.Top:
                        portPosition = new Point(nodeX + diamondWidth / 2, nodeY);
                        break;
                    default:
                        portPosition = new Point(nodeX, nodeY);
                        break;
                }

                port.PositionPoint = portPosition;
                if (port.PortUI != null)
                {
                    Canvas.SetLeft(port.PortUI, portPosition.X - portRadius);
                    Canvas.SetTop(port.PortUI, portPosition.Y - portRadius);
                }

                _portRenderer.EnsurePortAddedToCanvas(port);
                if (port.PortUI != null)
                    _host.ZIndexManager.SetPortZIndex(asyncTaskNode, port.PortUI);
            }

            foreach (var port in asyncTaskNode.Ports.Where(p => !p.IsVisible).ToList())
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    canvas.Children.Remove(port.PortUI);
            }
        }

        private void RenderAsyncTaskBodyPorts(AsyncTaskNode parent, Canvas canvas)
        {
            var bodyNode = parent.AsyncTaskBodyNode;
            if (bodyNode == null || parent.ContainerBorder == null) return;

            if (parent.ContainerBorder.ActualWidth == 0 || parent.ContainerBorder.ActualHeight == 0)
            {
                parent.ContainerBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                parent.ContainerBorder.Arrange(new Rect(parent.ContainerBorder.DesiredSize));
            }

            var bodyTopPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyTop");
            if (bodyTopPort == null)
            {
                bodyTopPort = new NodePort
                {
                    Id = "LoopBodyTop",
                    IsInput = true,
                    Position = PortPosition.Top,
                    IsVisible = true,
                    CanDeleteConnection = false
                };
                bodyNode.Ports.Add(bodyTopPort);
            }

            var bodyLeftPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyLeft");
            if (bodyLeftPort == null)
            {
                bodyLeftPort = new NodePort
                {
                    Id = "LoopBodyLeft",
                    IsInput = false,
                    Position = PortPosition.Right,
                    IsVisible = true
                };
                bodyNode.Ports.Add(bodyLeftPort);
            }
            bodyLeftPort.Position = PortPosition.Right;

            var bodyRightPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyRight");
            if (bodyRightPort == null)
            {
                bodyRightPort = new NodePort
                {
                    Id = "LoopBodyRight",
                    IsInput = true,
                    Position = PortPosition.Left,
                    IsVisible = true
                };
                bodyNode.Ports.Add(bodyRightPort);
            }
            bodyRightPort.Position = PortPosition.Left;

            foreach (var port in new[] { bodyTopPort, bodyLeftPort, bodyRightPort })
            {
                if (port.PortUI == null)
                {
                    Color? portSpecificColor = null;
                    if (!string.IsNullOrEmpty(port.ColorKey))
                    {
                        var brush = Application.Current.TryFindResource(port.ColorKey) as SolidColorBrush
                                    ?? Application.Current.TryFindResource(port.ColorKey + "Brush") as SolidColorBrush;
                        if (brush != null) portSpecificColor = brush.Color;
                    }

                    var nodeColor = (parent.NodeBrush as SolidColorBrush)?.Color;
                    var finalColor = portSpecificColor ?? nodeColor ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
                    port.PortUI = _portRenderer.CreatePort(finalColor);
                }
            }

            ApplyAsyncBodyPortScaleBySize(bodyNode);
            UpdateAsyncTaskBodyPortsPosition(parent);

            foreach (var port in new[] { bodyTopPort, bodyLeftPort, bodyRightPort })
            {
                if (double.IsNaN(port.PositionPoint.X) || double.IsNaN(port.PositionPoint.Y))
                    continue;
                if (!canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Add(port.PortUI);
                    // Use same Z-index tier as other ports so body dashed border never overlaps port.
                    _host.ZIndexManager.SetPortZIndex(bodyNode, port.PortUI);
                }
            }
        }

        public void UpdateAsyncTaskBodyPortsPosition(AsyncTaskNode parent)
        {
            var bodyNode = parent.AsyncTaskBodyNode;
            if (bodyNode == null) return;

            var containerX = bodyNode.X;
            var containerY = bodyNode.Y;
            var containerWidth = bodyNode.Width;
            var containerHeight = bodyNode.Height;

            var bodyTopPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyTop");
            var bodyLeftPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyLeft");
            var bodyRightPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyRight");

            if (bodyTopPort != null)
            {
                bodyTopPort.PositionPoint = new Point(containerX + containerWidth / 2, containerY);
                if (bodyTopPort.PortUI != null)
                {
                    Canvas.SetLeft(bodyTopPort.PortUI, bodyTopPort.PositionPoint.X - 9);
                    Canvas.SetTop(bodyTopPort.PortUI, bodyTopPort.PositionPoint.Y - 9);
                }
            }

            if (bodyLeftPort != null)
            {
                bodyLeftPort.PositionPoint = new Point(containerX, containerY + containerHeight / 2);
                if (bodyLeftPort.PortUI != null)
                {
                    Canvas.SetLeft(bodyLeftPort.PortUI, bodyLeftPort.PositionPoint.X - 9);
                    Canvas.SetTop(bodyLeftPort.PortUI, bodyLeftPort.PositionPoint.Y - 9);
                }
            }

            if (bodyRightPort != null)
            {
                bodyRightPort.PositionPoint = new Point(containerX + containerWidth, containerY + containerHeight / 2);
                if (bodyRightPort.PortUI != null)
                {
                    Canvas.SetLeft(bodyRightPort.PortUI, bodyRightPort.PositionPoint.X - 9);
                    Canvas.SetTop(bodyRightPort.PortUI, bodyRightPort.PositionPoint.Y - 9);
                }
            }
        }

        private static void ApplyAsyncBodyPortScaleBySize(AsyncTaskBodyNode bodyNode)
        {
            var visualScale = LoopContainerControl.ComputeBodyInteractionScale(bodyNode.Width, bodyNode.Height);

            foreach (var port in bodyNode.Ports.Where(p => p.Id is "LoopBodyTop" or "LoopBodyLeft" or "LoopBodyRight"))
            {
                if (port.PortUI == null) continue;
                port.PortUI.RenderTransformOrigin = new Point(0.5, 0.5);
                port.PortUI.RenderTransform = new ScaleTransform(visualScale, visualScale);
            }
        }

        private void CreateDefaultAsyncTaskBodyConnection(AsyncTaskNode asyncTaskNode)
        {
            var body = asyncTaskNode.AsyncTaskBodyNode;
            if (body == null) return;

            var bottomPort = asyncTaskNode.Ports.FirstOrDefault(p => p.Id == "LoopNodeBottom");
            var topPort = body.Ports.FirstOrDefault(p => p.Id == "LoopBodyTop");
            if (bottomPort == null || topPort == null) return;

            if (asyncTaskNode.DefaultConnection != null)
            {
                asyncTaskNode.DefaultConnection.IsDeleteVisible = false;
                _host.UpdateConnectionPath(asyncTaskNode.DefaultConnection);
                return;
            }

            if (_host.ViewModel != null)
            {
                var vm = _host.ViewModel;
                var related = vm.Connections
                    .Where(c => c.FromNode == asyncTaskNode && c.ToNode == body)
                    .ToList();

                if (related.Count > 0)
                {
                    var existing = related.FirstOrDefault(c => c.FromPort == bottomPort && c.ToPort == topPort) ?? related[0];
                    foreach (var extra in related.Where(c => c != existing).ToList())
                    {
                        _host.ConnectionRenderer.RemoveConnectionVisuals(extra);
                        vm.Connections.Remove(extra);
                    }

                    existing.FromPort = bottomPort;
                    existing.ToPort = topPort;
                    existing.IsDeleteVisible = false;
                    asyncTaskNode.DefaultConnection = existing;
                    _host.UpdateConnectionPath(existing);
                    return;
                }
            }

            var connection = new WorkflowConnection
            {
                FromNode = asyncTaskNode,
                ToNode = body,
                FromPort = bottomPort,
                ToPort = topPort,
                IsDeleteVisible = false
            };

            asyncTaskNode.DefaultConnection = connection;

            if (_host.ViewModel != null && !_host.ViewModel.Connections.Contains(connection))
                _host.ViewModel.Connections.Add(connection);
        }

        private void AttachAsyncTaskContainerHandlers(AsyncTaskNode asyncTaskNode)
        {
            if (asyncTaskNode.ContainerBorder == null) return;

            var containerBorder = asyncTaskNode.ContainerBorder;
            var body = asyncTaskNode.AsyncTaskBodyNode!;
            bool isDraggingContainer = false;
            Point containerDragStart = new();
            Point containerOriginalPos = new();
            List<WorkflowNode>? bodyClusterNodes = null;

            containerBorder.PreviewMouseDown += (s, e) =>
            {
                if (e.OriginalSource is Ellipse) return;

                _host.ZIndexManager.SelectNode(body);

                if (_host.ViewModel != null)
                {
                    foreach (var n in _host.ViewModel.Nodes)
                    {
                        if (n != body)
                            _host.ZIndexManager.RestoreNodeZIndex(n);
                    }
                }

                isDraggingContainer = true;
                containerDragStart = e.GetPosition(_host.WorkflowCanvas);
                containerOriginalPos = new Point(body.X, body.Y);
                bodyClusterNodes = GetAsyncTaskBodyClusterNodes(asyncTaskNode);
                containerBorder.CaptureMouse();
                e.Handled = true;
            };

            containerBorder.PreviewMouseMove += (s, e) =>
            {
                if (!isDraggingContainer || e.LeftButton != MouseButtonState.Pressed) return;

                _host.ZIndexManager.DragNode(body);

                var currentPos = e.GetPosition(_host.WorkflowCanvas);
                var deltaX = currentPos.X - containerDragStart.X;
                var deltaY = currentPos.Y - containerDragStart.Y;

                double oldX = body.X;
                double oldY = body.Y;

                body.X = containerOriginalPos.X + deltaX;
                body.Y = containerOriginalPos.Y + deltaY;

                Canvas.SetLeft(containerBorder, body.X);
                Canvas.SetTop(containerBorder, body.Y);

                double stepX = body.X - oldX;
                double stepY = body.Y - oldY;

                UpdateAsyncTaskBodyPortsPosition(asyncTaskNode);
                _host.UpdateMinimap();

                if (_host.ViewModel != null)
                {
                    var vm = _host.ViewModel;
                    foreach (var conn in vm.Connections.Where(c => c.FromNode == body || c.ToNode == body))
                        _host.UpdateConnectionPath(conn);

                    if (bodyClusterNodes != null)
                    {
                        foreach (var child in bodyClusterNodes)
                        {
                            if (ReferenceEquals(child, body)) continue;

                            double cx = child.X + stepX;
                            double cy = child.Y + stepY;
                            // Update renderer first so specialized renderers (e.g. Conditional diamond)
                            // can compute movement delta from previous X/Y and move their satellite visuals.
                            _host.UpdateNodePosition(child, cx, cy);
                            vm.UpdateNodePosition(child, cx, cy);

                            if (child.Border != null)
                            {
                                Canvas.SetLeft(child.Border, cx);
                                Canvas.SetTop(child.Border, cy);
                            }

                            if (child.IsConditionalNode && child.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                            {
                                _host.RenderConditionalNodePorts(child);
                            }
                            else
                            {
                                foreach (var port in child.Ports.Where(p => p.IsVisible))
                                    _portRenderer.UpdatePortsPositionOnSide(child, port.Position);
                            }

                            foreach (var cc in vm.Connections.Where(c => c.FromNode == child || c.ToNode == child))
                                _host.UpdateConnectionPath(cc);
                        }
                    }
                }

                e.Handled = true;
            };

            containerBorder.PreviewMouseUp += (_, e) =>
            {
                if (!isDraggingContainer) return;
                isDraggingContainer = false;
                containerBorder.ReleaseMouseCapture();
                bodyClusterNodes = null;
                _host.ZIndexManager.RestoreNodeZIndex(body);
                _host.UpdateMinimap();
                e.Handled = true;
            };

            body.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(AsyncTaskBodyNode.Width) && e.PropertyName != nameof(AsyncTaskBodyNode.Height))
                    return;

                ApplyAsyncBodyPortScaleBySize(body);
                UpdateAsyncTaskBodyPortsPosition(asyncTaskNode);
                _host.UpdateMinimap();
                _host.ViewportCullingService?.OnNodeChanged(body);

                if (_host.ViewModel != null)
                {
                    foreach (var conn in _host.ViewModel.Connections.Where(c => c.FromNode == body || c.ToNode == body))
                        _host.UpdateConnectionPath(conn);
                }
            };
        }

        private List<WorkflowNode> GetAsyncTaskBodyClusterNodes(AsyncTaskNode asyncTaskNode)
        {
            var result = new List<WorkflowNode>();
            var vm = _host.ViewModel;
            if (vm == null) return result;

            var body = asyncTaskNode.AsyncTaskBodyNode!;
            var visited = new HashSet<WorkflowNode> { body };
            var queue = new Queue<WorkflowNode>();
            queue.Enqueue(body);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var neighbors = vm.Connections
                    .Where(c => c.FromNode == current || c.ToNode == current)
                    .Select(c => c.FromNode == current ? c.ToNode : c.FromNode);

                foreach (var neighbor in neighbors)
                {
                    if (ReferenceEquals(neighbor, asyncTaskNode)) continue;
                    if (visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            visited.Remove(body);
            foreach (var node in visited)
            {
                if (!result.Contains(node))
                    result.Add(node);
            }

            return result;
        }

        public void RenderAsyncTaskNodePorts(WorkflowNode node)
        {
            if (node is AsyncTaskNode at && IsLoopLike(at))
            {
                RenderLoopLikeAsyncTaskPorts(at, _host.WorkflowCanvas);
                RenderAsyncTaskBodyPorts(at, _host.WorkflowCanvas);
                return;
            }

            if (node.Border == null) return;

            const double headerHeight = 40;
            const double branchHeight = 35;
            double nodeWidth = node.Border.Width;

            var inputPort = node.Ports.FirstOrDefault(p => p.IsInput && p.IsVisible);
            if (inputPort != null)
            {
                if (inputPort.PortUI == null)
                {
                    var portColor = GetColorFromTheme(node.Ports.Where(s => s.IsInput).FirstOrDefault()?.ColorKey + "Brush") ?? Colors.Cyan;
                    inputPort.PortUI = _portRenderer.CreatePort(portColor);
                    // Tag giúp renderer/cleanup xác định port UI đúng owner
                    if (inputPort.PortUI is Shape sh) sh.Tag = inputPort;
                }

                double nodeHeight = node.Border.Height;
                double portX, portY;
                switch (inputPort.Position)
                {
                    case PortPosition.Left:
                        portX = node.X;
                        portY = node.Y + nodeHeight / 2;
                        break;
                    case PortPosition.Right:
                        portX = node.X + nodeWidth;
                        portY = node.Y + nodeHeight / 2;
                        break;
                    case PortPosition.Top:
                        portX = node.X + nodeWidth / 2;
                        portY = node.Y;
                        break;
                    case PortPosition.Bottom:
                        portX = node.X + nodeWidth / 2;
                        portY = node.Y + nodeHeight;
                        break;
                    default:
                        portX = node.X;
                        portY = node.Y + nodeHeight / 2;
                        break;
                }

                Canvas.SetLeft(inputPort.PortUI, portX - 9);
                Canvas.SetTop(inputPort.PortUI, portY - 9);
                inputPort.PositionPoint = new Point(portX, portY);

                if (!_host.WorkflowCanvas.Children.Contains(inputPort.PortUI))
                    _host.WorkflowCanvas.Children.Add(inputPort.PortUI);
            }

            for (int i = 0; i < node.AsyncTaskBranches.Count; i++)
            {
                var branch = node.AsyncTaskBranches[i];
                if (branch.Port == null) continue;

                string colorKey = ColorPortsByIndex[i % ColorPortsByIndex.Count];
                branch.Port.ColorKey = colorKey;
                var portColor = GetColorFromTheme(colorKey + "Brush") ?? Colors.Orange;

                if (branch.Port.PortUI == null)
                {
                    branch.Port.PortUI = _portRenderer.CreatePort(portColor);
                    if (branch.Port.PortUI is Shape sh) sh.Tag = branch.Port;
                }
                else if (branch.Port.PortUI is Shape shape)
                    shape.Fill = new SolidColorBrush(portColor);

                double portY = node.Y + headerHeight + (i * branchHeight) + (branchHeight / 2);
                double portX = branch.Port.Position == PortPosition.Right ? node.X + nodeWidth : node.X;

                Canvas.SetLeft(branch.Port.PortUI, portX - 9);
                Canvas.SetTop(branch.Port.PortUI, portY - 9);
                branch.Port.PositionPoint = new Point(portX, portY);

                if (!_host.WorkflowCanvas.Children.Contains(branch.Port.PortUI))
                    _host.WorkflowCanvas.Children.Add(branch.Port.PortUI);
            }

            var regularPorts = node.Ports
                .Where(p => p.IsVisible && p != inputPort && !node.AsyncTaskBranches.Any(b => b.Port == p))
                .Select(p => p.Position)
                .Distinct();

            foreach (var position in regularPorts)
                _portRenderer.UpdatePortsPositionOnSide(node, position);
        }

        private static Color? GetColorFromTheme(string resourceKey)
        {
            try
            {
                var brush = Application.Current.TryFindResource(resourceKey) as SolidColorBrush;
                return brush?.Color;
            }
            catch
            {
                return null;
            }
        }
    }
}
