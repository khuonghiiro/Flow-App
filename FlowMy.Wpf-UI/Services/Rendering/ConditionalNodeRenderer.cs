using FlowMy.Models;
using FlowMy.Services.Geometry;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class ConditionalNodeRenderer : INodeRenderer
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
        private readonly BezierGeometryGenerator _bezierGeometry;
        private readonly OrthogonalGeometryGenerator _orthogonalGeometry;
        private readonly OrthogonalV2GeometryGenerator _orthogonalV2Geometry;

        private IWorkflowEditorHost _host => _hostAccessor.GetRequiredHost();

        /// <summary>Runtime-only: Border của nút "add" circle trong diamond mode.</summary>
        private readonly Dictionary<string, Border> _addCircles = new();

        public ConditionalNodeRenderer(
            IWorkflowEditorHostAccessor hostAccessor,
            PortRenderer portRenderer,
            BezierGeometryGenerator bezierGeometry,
            OrthogonalGeometryGenerator orthogonalGeometry,
            OrthogonalV2GeometryGenerator orthogonalV2Geometry)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _bezierGeometry = bezierGeometry ?? throw new ArgumentNullException(nameof(bezierGeometry));
            _orthogonalGeometry = orthogonalGeometry ?? throw new ArgumentNullException(nameof(orthogonalGeometry));
            _orthogonalV2Geometry = orthogonalV2Geometry ?? throw new ArgumentNullException(nameof(orthogonalV2Geometry));
        }

        // ==================== DISPATCH BY VISUAL MODE ====================

        public void RenderConditionalNode(WorkflowNode node)
        {
            if (node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
            {
                RenderDiamondNode(node);
            }
            else
            {
                RenderClassicNode(node);
            }
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (!node.IsConditionalNode)
            {
                throw new InvalidOperationException("ConditionalNodeRenderer can only render conditional nodes.");
            }

            if (node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
            {
                RenderDiamondNode(node);
            }
            else
            {
                node.Border = CreateConditionalNodeBorder(node);
                Canvas.SetLeft(node.Border, node.X);
                Canvas.SetTop(node.Border, node.Y);
                canvas.Children.Add(node.Border);

                _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);
                RenderConditionalNodePorts(node);
            }
        }

        public void UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            double deltaX = x - node.X;
            double deltaY = y - node.Y;
            node.X = x;
            node.Y = y;

            if (node.Border != null)
            {
                Canvas.SetLeft(node.Border, x);
                Canvas.SetTop(node.Border, y);
            }

            if (node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
            {
                // Di chuyển tất cả satellites theo diamond
                MoveSatellitesWithDiamond(node, deltaX, deltaY);
                UpdateDiamondAddCirclePosition(node);
                RenderDiamondNodePorts(node);
                SyncDiamondOverlayZIndex(node);

                // Sync title position (giống LoopNode)
                ConditionalDiamondControl.UpdateTitlePosition(node, node.TitleTextBlockUI as System.Windows.Controls.TextBlock, node.Border);
            }
            else
            {
                RenderConditionalNodePorts(node);
            }

            _host.SyncAllPortsZIndex(node);

            // Re-apply diamond satellite port positions after host sync to avoid drift.
            if (node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
            {
                RenderDiamondNodePorts(node);
                SyncDiamondOverlayZIndex(node);
            }
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node.Border != null && canvas.Children.Contains(node.Border))
            {
                canvas.Children.Remove(node.Border);
            }

            foreach (var port in node.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Remove(port.PortUI);
                }
            }

            // Remove diamond-specific visuals
            if (node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
            {
                RemoveDiamondVisuals(node, canvas);
            }
        }

        public void RemoveAllNodeVisuals(Canvas canvas)
        {
            var borders = canvas.Children.OfType<Border>().Where(b => b.Tag is WorkflowNode).ToList();
            foreach (var border in borders)
            {
                canvas.Children.Remove(border);
            }

            var ports = canvas.Children
                .OfType<Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18))
                .ToList();
            foreach (var port in ports)
            {
                canvas.Children.Remove(port);
            }
        }

        // ==================== CLASSIC MODE ====================

        private void RenderClassicNode(WorkflowNode node)
        {
            node.Border = CreateConditionalNodeBorder(node);
            Canvas.SetLeft(node.Border, node.X);
            Canvas.SetTop(node.Border, node.Y);
            _host.WorkflowCanvas.Children.Add(node.Border);

            _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);
            RenderConditionalNodePorts(node);
        }

        public Border CreateConditionalNodeBorder(WorkflowNode node)
        {
            var border = ConditionalNodeControl.CreateBorder(
                node,
                _host as System.Windows.Window,
                _host,
                addElseIfBranch: () => AddElseIfBranch(node),
                removeBranch: b => RemoveBranch(node, b));
            NodeChrome.Apply(border, node, _host);

            border.MouseDown += _host.NodeMouseDown;
            border.MouseMove += _host.NodeMouseMove;
            border.MouseUp += _host.NodeMouseUp;
            border.MouseEnter += _host.NodeBorderMouseEnter;
            border.MouseLeave += _host.NodeBorderMouseLeave;
            border.ContextMenu = _host.CreateNodeContextMenu(node);

            border.LayoutUpdated += (s, e) => _host.SyncAllPortsZIndex(node);

            return border;
        }

        // ==================== DIAMOND MODE ====================

        private void RenderDiamondNode(WorkflowNode node)
        {
            // Defensive cleanup: prevent duplicated add circles after reload/re-render.
            if (_addCircles.TryGetValue(node.Id, out var oldAddCircle))
            {
                if (_host.WorkflowCanvas.Children.Contains(oldAddCircle))
                    _host.WorkflowCanvas.Children.Remove(oldAddCircle);
                _addCircles.Remove(node.Id);
            }
            var staleAddCircles = _host.WorkflowCanvas.Children
                .OfType<Border>()
                .Where(b => b.Tag is string s && s == $"AddSatellite:{node.Id}")
                .ToList();
            foreach (var stale in staleAddCircles)
            {
                _host.WorkflowCanvas.Children.Remove(stale);
            }

            // Create diamond border
            node.Border = CreateDiamondNodeBorder(node);
            Canvas.SetLeft(node.Border, node.X);
            Canvas.SetTop(node.Border, node.Y);
            _host.WorkflowCanvas.Children.Add(node.Border);
            _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);

            // Create add circle
            var addCircle = ConditionalDiamondControl.CreateAddCircle(node, _host, () => AddElseIfBranch(node));
            _addCircles[node.Id] = addCircle;
            _host.WorkflowCanvas.Children.Add(addCircle);
            UpdateDiamondAddCirclePosition(node);
            Panel.SetZIndex(addCircle, 22000);

            // Create satellites for each branch
            RenderDiamondSatellites(node);

            // Render ports (input port on diamond, output ports on satellites)
            RenderDiamondNodePorts(node);
            SyncDiamondOverlayZIndex(node);
        }

        private Border CreateDiamondNodeBorder(WorkflowNode node)
        {
            var border = ConditionalDiamondControl.CreateDiamondBorder(
                node,
                _host as System.Windows.Window,
                _host,
                addElseIfBranch: () => AddElseIfBranch(node));

            border.MouseDown += _host.NodeMouseDown;
            border.MouseMove += _host.NodeMouseMove;
            border.MouseUp += _host.NodeMouseUp;
            border.MouseEnter += _host.NodeBorderMouseEnter;
            border.MouseLeave += _host.NodeBorderMouseLeave;
            border.ContextMenu = CreateDiamondContextMenu(node);

            border.LayoutUpdated += (s, e) =>
            {
                _host.SyncAllPortsZIndex(node);
                // Avoid heavy re-render loop on every layout pass (causes jitter with many branches).
                if (node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                    SyncDiamondOverlayZIndex(node);
            };

            return border;
        }

        private ContextMenu CreateDiamondContextMenu(WorkflowNode node)
        {
            var menu = _host.CreateNodeContextMenu(node);

            var addItem = new MenuItem { Header = "Thêm điều kiện else if" };
            addItem.Click += (s, e) => AddElseIfBranch(node);
            menu.Items.Insert(0, addItem);
            menu.Items.Insert(1, new Separator());

            return menu;
        }

        /// <summary>
        /// Render tất cả satellite circles + connection lines cho diamond mode.
        /// </summary>
        public void RenderDiamondSatellites(WorkflowNode node)
        {
            var canvas = _host.WorkflowCanvas;
            int count = node.ConditionalBranches.Count;

            for (int i = 0; i < count; i++)
            {
                var branch = node.ConditionalBranches[i];

                // Defensive cleanup: avoid duplicated visuals after re-render/load cycles.
                if (branch.SatelliteBorder != null && canvas.Children.Contains(branch.SatelliteBorder))
                    canvas.Children.Remove(branch.SatelliteBorder);
                if (branch.SatelliteLine != null && canvas.Children.Contains(branch.SatelliteLine))
                    canvas.Children.Remove(branch.SatelliteLine);
                if (branch.SatelliteDeleteButton != null && canvas.Children.Contains(branch.SatelliteDeleteButton))
                    canvas.Children.Remove(branch.SatelliteDeleteButton);
                if (branch.SatelliteInputVisual != null && canvas.Children.Contains(branch.SatelliteInputVisual))
                    canvas.Children.Remove(branch.SatelliteInputVisual);
                if (branch.DiamondOutputVisual != null && canvas.Children.Contains(branch.DiamondOutputVisual))
                    canvas.Children.Remove(branch.DiamondOutputVisual);
                if (branch.SatelliteArrowHead != null && canvas.Children.Contains(branch.SatelliteArrowHead))
                    canvas.Children.Remove(branch.SatelliteArrowHead);

                // Calculate satellite position
                if (double.IsNaN(branch.SatelliteOffsetX) || double.IsNaN(branch.SatelliteOffsetY))
                {
                    var defaultOffset = ConditionalDiamondControl.GetDefaultSatelliteOffset(i, count);
                    branch.SatelliteOffsetX = defaultOffset.X;
                    branch.SatelliteOffsetY = defaultOffset.Y;
                }

                // Create satellite circle
                var satellite = ConditionalDiamondControl.CreateSatelliteCircle(
                    node, branch, i, _host,
                    _host as System.Windows.Window,
                    b => RemoveBranch(node, b));

                // ✅ Fix: Disable BitmapCache on satellite borders to avoid rendering artifacts
                GpuOptimizationHelper.ApplyToBorder(satellite, isDragging: true);

                // Enable satellite dragging
                AttachSatelliteDrag(satellite, node, branch, i);

                canvas.Children.Add(satellite);
                branch.SatelliteBorder = satellite;

                // Position the satellite
                double satX = node.X + branch.SatelliteOffsetX;
                double satY = node.Y + branch.SatelliteOffsetY;
                Canvas.SetLeft(satellite, satX);
                Canvas.SetTop(satellite, satY);

                // Create connection line from diamond to satellite
                var diamondOutPoint = GetDiamondOutPoint(node);
                satellite.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var satelliteInPoint = GetSatelliteInPoint(branch, satX, satY);
                var inDirection = GetSatelliteInputDirection(branch);
                var arrowTip = GetArrowTipPoint(satelliteInPoint, inDirection);
                var lineEnd = GetArrowBaseMidPoint(arrowTip, inDirection);

                var colorKey = ConditionalDiamondControl.GetSatelliteColorKey(i);
                var lineColor = GetColorFromTheme(colorKey + "Brush") ?? Colors.Orange;

                var line = ConditionalDiamondControl.CreateSatelliteLine(
                    diamondOutPoint,
                    lineEnd,
                    lineColor);
                canvas.Children.Insert(0, line); // Insert behind other elements
                branch.SatelliteLine = line;
                UpdateSatelliteLineStyle(node, branch);
                if (_host.IsAnimationEnabled)
                {
                    ConditionalDiamondControl.StartSatelliteLineAnimation(line, lineColor);
                }
                else
                {
                    ConditionalDiamondControl.StopSatelliteLineAnimation(line);
                }

                var diamondOutVisual = CreateVisualDiamondPort(lineColor, isInteractive: false);
                canvas.Children.Add(diamondOutVisual);
                branch.DiamondOutputVisual = diamondOutVisual;

                var satelliteInVisual = CreateVisualDiamondPort(lineColor, isInteractive: false);
                canvas.Children.Add(satelliteInVisual);
                branch.SatelliteInputVisual = satelliteInVisual;

                var arrowHead = CreateSatelliteArrowHead(lineColor);
                canvas.Children.Add(arrowHead);
                branch.SatelliteArrowHead = arrowHead;

                var deleteButton = CreateSatelliteDeleteButton(node, branch);
                canvas.Children.Add(deleteButton);
                branch.SatelliteDeleteButton = deleteButton;
                UpdateSatelliteVisualPorts(node, branch);
                UpdateSatelliteArrowHead(node, branch, satelliteInPoint);
                UpdateSatelliteDeleteButtonPosition(branch);
                Panel.SetZIndex(deleteButton, 21500);
            }

            EnsureSatelliteSpacing(node, null);
            SyncDiamondOverlayZIndex(node);
        }

        /// <summary>
        /// Cập nhật vị trí add circle (góc dưới-trái diamond).
        /// </summary>
        private void UpdateDiamondAddCirclePosition(WorkflowNode node)
        {
            if (!_addCircles.TryGetValue(node.Id, out var addCircle)) return;

            var inputPosition = node.Ports.FirstOrDefault(p => p.IsInput)?.Position ?? PortPosition.Left;
            var outputPosition = node.Ports.FirstOrDefault(p => !p.IsInput)?.Position ?? PortPosition.Right;
            var inputAnchor = GetDiamondPortAnchor(node, inputPosition);
            var outputAnchor = GetDiamondPortAnchor(node, outputPosition);

            const double cornerInset = 14d;
            var candidates = new[]
            {
                new Point(node.X + cornerInset, node.Y + cornerInset),
                new Point(node.X + ConditionalDiamondControl.DiamondWidth - cornerInset, node.Y + cornerInset),
                new Point(node.X + ConditionalDiamondControl.DiamondWidth - cornerInset, node.Y + ConditionalDiamondControl.DiamondHeight - cornerInset),
                new Point(node.X + cornerInset, node.Y + ConditionalDiamondControl.DiamondHeight - cornerInset)
            };

            Point selected = candidates[0];
            double bestScore = double.MinValue;
            foreach (var candidate in candidates)
            {
                var inDx = candidate.X - inputAnchor.X;
                var inDy = candidate.Y - inputAnchor.Y;
                var outDx = candidate.X - outputAnchor.X;
                var outDy = candidate.Y - outputAnchor.Y;
                var distToIn = (inDx * inDx) + (inDy * inDy);
                var distToOut = (outDx * outDx) + (outDy * outDy);
                var score = Math.Min(distToIn, distToOut);
                if (score > bestScore)
                {
                    bestScore = score;
                    selected = candidate;
                }
            }

            Canvas.SetLeft(addCircle, selected.X - ConditionalDiamondControl.AddCircleRadius);
            Canvas.SetTop(addCircle, selected.Y - ConditionalDiamondControl.AddCircleRadius);
            Panel.SetZIndex(addCircle, 22000);
        }

        /// <summary>
        /// Cleanup tất cả diamond visuals (satellites, lines, add circle).
        /// </summary>
        public void RemoveDiamondVisuals(WorkflowNode node, Canvas canvas)
        {
            foreach (var branch in node.ConditionalBranches)
            {
                if (branch.SatelliteBorder != null && canvas.Children.Contains(branch.SatelliteBorder))
                    canvas.Children.Remove(branch.SatelliteBorder);
                branch.SatelliteBorder = null;

                if (branch.SatelliteLine != null && canvas.Children.Contains(branch.SatelliteLine))
                    canvas.Children.Remove(branch.SatelliteLine);
                branch.SatelliteLine = null;

                if (branch.SatelliteDeleteButton != null && canvas.Children.Contains(branch.SatelliteDeleteButton))
                    canvas.Children.Remove(branch.SatelliteDeleteButton);
                branch.SatelliteDeleteButton = null;

                if (branch.SatelliteInputVisual != null && canvas.Children.Contains(branch.SatelliteInputVisual))
                    canvas.Children.Remove(branch.SatelliteInputVisual);
                branch.SatelliteInputVisual = null;

                if (branch.DiamondOutputVisual != null && canvas.Children.Contains(branch.DiamondOutputVisual))
                    canvas.Children.Remove(branch.DiamondOutputVisual);
                branch.DiamondOutputVisual = null;

                if (branch.SatelliteArrowHead != null && canvas.Children.Contains(branch.SatelliteArrowHead))
                    canvas.Children.Remove(branch.SatelliteArrowHead);
                branch.SatelliteArrowHead = null;
            }

            if (_addCircles.TryGetValue(node.Id, out var addCircle))
            {
                if (canvas.Children.Contains(addCircle))
                    canvas.Children.Remove(addCircle);
                _addCircles.Remove(node.Id);
            }
        }

        /// <summary>
        /// Di chuyển tất cả satellites theo khi kéo diamond.
        /// </summary>
        private void MoveSatellitesWithDiamond(WorkflowNode node, double deltaX, double deltaY)
        {
            foreach (var branch in node.ConditionalBranches)
            {
                if (branch.SatelliteBorder != null)
                {
                    double curX = Canvas.GetLeft(branch.SatelliteBorder);
                    double curY = Canvas.GetTop(branch.SatelliteBorder);
                    double newX = curX + deltaX;
                    double newY = curY + deltaY;
                    Canvas.SetLeft(branch.SatelliteBorder, newX);
                    Canvas.SetTop(branch.SatelliteBorder, newY);

                    // Keep offsets in sync so add/re-render never snaps satellites.
                    branch.SatelliteOffsetX = newX - node.X;
                    branch.SatelliteOffsetY = newY - node.Y;
                }

                // Update connection line
                UpdateSatelliteLine(node, branch);
            }

            SyncDiamondOverlayZIndex(node);
        }

        /// <summary>
        /// Cập nhật đường nối từ diamond đến satellite.
        /// </summary>
        private void UpdateSatelliteLine(WorkflowNode node, ConditionalBranch branch)
        {
            if (branch.SatelliteLine == null || branch.SatelliteBorder == null) return;

            var diamondOutPoint = GetDiamondOutPoint(node);

            double satX = Canvas.GetLeft(branch.SatelliteBorder);
            double satY = Canvas.GetTop(branch.SatelliteBorder);

            branch.SatelliteBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var satelliteInPoint = GetSatelliteInPoint(branch, satX, satY);
            var inDirection = GetSatelliteInputDirection(branch);
            var arrowTip = GetArrowTipPoint(satelliteInPoint, inDirection);
            var lineEnd = GetArrowBaseMidPoint(arrowTip, inDirection);

            ConditionalDiamondControl.UpdateLineGeometry(
                branch.SatelliteLine,
                diamondOutPoint,
                lineEnd);
            UpdateSatelliteLineStyle(node, branch);

            UpdateSatelliteVisualPorts(node, branch);
            UpdateSatelliteArrowHead(node, branch, satelliteInPoint);
            UpdateSatelliteDeleteButtonPosition(branch);
            SyncBranchOverlayZIndex(node, branch);

            if (_host.IsAnimationEnabled)
            {
                var color = Colors.Orange;
                if (branch.SatelliteLine.Stroke is SolidColorBrush brush)
                {
                    color = brush.Color;
                }
                ConditionalDiamondControl.StartSatelliteLineAnimation(branch.SatelliteLine, color);
            }
        }

        /// <summary>
        /// Attach drag logic for satellite circles.
        /// Kéo riêng satellite (chỉ di chuyển nó, update offset).
        /// </summary>
        private void AttachSatelliteDrag(Border satellite, WorkflowNode node, ConditionalBranch branch, int branchIndex)
        {
            bool isDragging = false;
            Point dragStart = new Point();
            double startLeft = 0, startTop = 0;

            satellite.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 1)
                {
                    isDragging = true;
                    dragStart = e.GetPosition(_host.WorkflowCanvas);
                    startLeft = Canvas.GetLeft(satellite);
                    startTop = Canvas.GetTop(satellite);
                    satellite.CaptureMouse();
                    e.Handled = true;
                }
            };

            satellite.MouseMove += (s, e) =>
            {
                if (!isDragging) return;

                var currentPos = e.GetPosition(_host.WorkflowCanvas);
                double newLeft = startLeft + (currentPos.X - dragStart.X);
                double newTop = startTop + (currentPos.Y - dragStart.Y);

                Canvas.SetLeft(satellite, newLeft);
                Canvas.SetTop(satellite, newTop);

                // Update offset relative to diamond
                branch.SatelliteOffsetX = newLeft - node.X;
                branch.SatelliteOffsetY = newTop - node.Y;

                // Update connection line
                UpdateSatelliteLine(node, branch);

                // Update port position
                UpdateSingleSatellitePort(node, branch, branchIndex);

                // Redraw connections attached to this port
                UpdateConnectionsForBranch(node, branch);

                EnsureSatelliteSpacing(node, branch);
                ResolveSatelliteCollisionsWithNodes(node, branch, branchIndex);

                e.Handled = true;
            };

            satellite.MouseLeftButtonUp += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    satellite.ReleaseMouseCapture();
                    e.Handled = true;
                }
            };
        }

        /// <summary>
        /// Cập nhật position của port OUT trên 1 satellite.
        /// </summary>
        private void UpdateSingleSatellitePort(WorkflowNode node, ConditionalBranch branch, int branchIndex)
        {
            if (branch.Port?.PortUI == null) return;

            // Port OUT nằm ở góc phải hình thoi (cùng 1 vị trí cho tất cả branch)
            double satX = branch.SatelliteBorder != null ? Canvas.GetLeft(branch.SatelliteBorder) : node.X;
            double satY = branch.SatelliteBorder != null ? Canvas.GetTop(branch.SatelliteBorder) : node.Y;
            if (double.IsNaN(satX)) satX = node.X;
            if (double.IsNaN(satY)) satY = node.Y;

            var satelliteCenter = GetSatelliteCenter(branch, satX, satY);

            double portX;
            double portY;
            switch (branch.Port.Position)
            {
                case PortPosition.Left:
                    portX = satelliteCenter.X - ConditionalDiamondControl.SatelliteRadius - 1;
                    portY = satelliteCenter.Y;
                    break;
                case PortPosition.Top:
                    portX = satelliteCenter.X;
                    portY = satelliteCenter.Y - ConditionalDiamondControl.SatelliteRadius - 1;
                    break;
                case PortPosition.Bottom:
                    portX = satelliteCenter.X;
                    portY = satelliteCenter.Y + ConditionalDiamondControl.SatelliteRadius + 1;
                    break;
                default:
                    portX = satelliteCenter.X + ConditionalDiamondControl.SatelliteRadius + 1;
                    portY = satelliteCenter.Y;
                    break;
            }
            double halfW = (branch.Port.PortUI.Width > 0 ? branch.Port.PortUI.Width : 18) / 2;
            double halfH = (branch.Port.PortUI.Height > 0 ? branch.Port.PortUI.Height : 18) / 2;

            Canvas.SetLeft(branch.Port.PortUI, portX - halfW);
            Canvas.SetTop(branch.Port.PortUI, portY - halfH);
            branch.Port.PositionPoint = new Point(portX, portY);
            branch.Port.PortUI.Visibility = Visibility.Visible;
            branch.Port.PortUI.Opacity = 1.0;
            Panel.SetZIndex(branch.Port.PortUI, int.MaxValue - 1000);
        }

        private void UpdateSatelliteVisualPorts(WorkflowNode node, ConditionalBranch branch)
        {
            if (branch.DiamondOutputVisual != null)
            {
                var outPoint = GetDiamondOutPoint(node);
                double diamondOutX = outPoint.X - 7;
                double diamondOutY = outPoint.Y - 7;
                Canvas.SetLeft(branch.DiamondOutputVisual, diamondOutX);
                Canvas.SetTop(branch.DiamondOutputVisual, diamondOutY);
            }

            if (branch.SatelliteInputVisual != null && branch.SatelliteBorder != null)
            {
                double satX = Canvas.GetLeft(branch.SatelliteBorder);
                double satY = Canvas.GetTop(branch.SatelliteBorder);
                var inPoint = GetSatelliteInPoint(branch, satX, satY);
                double inX = inPoint.X - 7;
                double inY = inPoint.Y - 7;
                Canvas.SetLeft(branch.SatelliteInputVisual, inX);
                Canvas.SetTop(branch.SatelliteInputVisual, inY);
            }
        }

        private FrameworkElement CreateVisualDiamondPort(Color color, bool isInteractive)
        {
            var diamond = new Rectangle
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(45),
                IsHitTestVisible = isInteractive
            };
            diamond.Tag = new Size(14, 14);
            return diamond;
        }

        private FrameworkElement CreateInteractiveDiamondPort(NodePort port, Color color)
        {
            var diamond = CreateVisualDiamondPort(color, isInteractive: true);
            diamond.Cursor = Cursors.Hand;
            diamond.Tag = port;
            diamond.MouseDown += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseDown(diamond, e);
            };
            diamond.MouseUp += (s, e) =>
            {
                e.Handled = true;
                _host.PortMouseUp(diamond, e);
            };
            return diamond;
        }

        private FrameworkElement CreateSatelliteArrowHead(Color color)
        {
            var arrow = new Path
            {
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false,
                Tag = "ConditionalSatelliteArrow",
                Effect = null
            };
            return arrow;
        }

        private Button CreateSatelliteDeleteButton(WorkflowNode node, ConditionalBranch branch)
        {
            var button = new Button
            {
                Width = 20,
                Height = 20,
                Content = "×",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Xoá điều kiện này",
                Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1)
            };

            button.Click += (s, e) =>
            {
                e.Handled = true;
                RemoveBranch(node, branch);
            };

            return button;
        }

        private void UpdateSatelliteDeleteButtonPosition(ConditionalBranch branch)
        {
            if (branch.SatelliteDeleteButton == null || branch.SatelliteLine == null) return;
            if (branch.SatelliteLine.Data is not PathGeometry pathGeometry) return;
            if (pathGeometry.Figures.Count == 0) return;
            var midPoint = GetPointAtFractionLength(pathGeometry, 0.5);

            Canvas.SetLeft(branch.SatelliteDeleteButton, midPoint.X - (branch.SatelliteDeleteButton.Width / 2));
            Canvas.SetTop(branch.SatelliteDeleteButton, midPoint.Y - (branch.SatelliteDeleteButton.Height / 2));
        }

        private static Point GetPointAtFractionLength(PathGeometry geometry, double fraction)
        {
            if (geometry == null || double.IsNaN(fraction) || double.IsInfinity(fraction))
                return new Point(0, 0);

            fraction = Math.Clamp(fraction, 0.0, 1.0);
            var flat = geometry.GetFlattenedPathGeometry(0.5, ToleranceType.Absolute);
            if (flat.Figures.Count == 0)
                return new Point(0, 0);

            var points = new List<Point>();
            double totalLength = 0;
            foreach (var fig in flat.Figures)
            {
                var last = fig.StartPoint;
                points.Add(last);
                foreach (var seg in fig.Segments)
                {
                    if (seg is LineSegment ls)
                    {
                        var p = ls.Point;
                        totalLength += (p - last).Length;
                        points.Add(p);
                        last = p;
                    }
                    else if (seg is PolyLineSegment pls)
                    {
                        foreach (var p in pls.Points)
                        {
                            totalLength += (p - last).Length;
                            points.Add(p);
                            last = p;
                        }
                    }
                }
            }

            if (totalLength <= 0 || points.Count < 2)
                return points.Count > 0 ? points[0] : new Point(0, 0);

            double targetLength = totalLength * fraction;
            double accumulated = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                var segLen = (points[i + 1] - points[i]).Length;
                if (accumulated + segLen >= targetLength)
                {
                    double t = segLen > 0 ? (targetLength - accumulated) / segLen : 0;
                    return new Point(
                        points[i].X + (points[i + 1].X - points[i].X) * t,
                        points[i].Y + (points[i + 1].Y - points[i].Y) * t);
                }
                accumulated += segLen;
            }

            return points[points.Count - 1];
        }

        private void EnsureSatelliteSpacing(WorkflowNode node, ConditionalBranch? movedBranch = null)
        {
            if (movedBranch == null) return;

            var minDistance = (ConditionalDiamondControl.SatelliteRadius * 2) + 16d;
            var satellites = node.ConditionalBranches
                .Where(b => b.SatelliteBorder != null)
                .ToList();
            if (satellites.Count < 2) return;

            var movedTop = Canvas.GetTop(movedBranch.SatelliteBorder!);
            var movedLeft = Canvas.GetLeft(movedBranch.SatelliteBorder!);
            bool hasOverlap;
            int safety = 0;
            do
            {
                hasOverlap = false;
                foreach (var other in satellites)
                {
                    if (ReferenceEquals(other, movedBranch)) continue;
                    var otherTop = Canvas.GetTop(other.SatelliteBorder!);
                    var otherLeft = Canvas.GetLeft(other.SatelliteBorder!);
                    var dy = Math.Abs(otherTop - movedTop);
                    var dx = Math.Abs(otherLeft - movedLeft);
                    if (dx < 20 && dy < minDistance)
                    {
                        movedTop = otherTop + minDistance;
                        hasOverlap = true;
                    }
                }
                safety++;
            } while (hasOverlap && safety < 20);

            Canvas.SetTop(movedBranch.SatelliteBorder!, movedTop);
            movedBranch.SatelliteOffsetY = movedTop - node.Y;
            UpdateSatelliteLine(node, movedBranch);
        }

        private void ResolveSatelliteCollisionsWithNodes(WorkflowNode ownerNode, ConditionalBranch movedBranch, int movedBranchIndex)
        {
            if (movedBranch.SatelliteBorder == null) return;
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Nodes.Count == 0) return;

            var satRect = GetElementRect(movedBranch.SatelliteBorder);
            if (satRect.IsEmpty) return;
            var satExpanded = satRect;
            satExpanded.Inflate(12, 12);

            foreach (var other in vm.Nodes)
            {
                if (other == null || ReferenceEquals(other, ownerNode) || other.Border == null) continue;
                if (!_host.WorkflowCanvas.Children.Contains(other.Border)) continue;

                var otherRect = GetElementRect(other.Border);
                if (otherRect.IsEmpty || !satExpanded.IntersectsWith(otherRect)) continue;

                var satCx = satExpanded.X + (satExpanded.Width / 2.0);
                var satCy = satExpanded.Y + (satExpanded.Height / 2.0);
                var nodeCx = otherRect.X + (otherRect.Width / 2.0);
                var nodeCy = otherRect.Y + (otherRect.Height / 2.0);

                var dx = nodeCx - satCx;
                var dy = nodeCy - satCy;
                if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                {
                    dx = 1;
                }

                var overlapX = ((satExpanded.Width / 2.0) + (otherRect.Width / 2.0)) - Math.Abs(dx);
                var overlapY = ((satExpanded.Height / 2.0) + (otherRect.Height / 2.0)) - Math.Abs(dy);
                if (overlapX <= 0 || overlapY <= 0) continue;

                double pushX = 0;
                double pushY = 0;
                if (overlapX < overlapY)
                    pushX = dx >= 0 ? overlapX : -overlapX;
                else
                    pushY = dy >= 0 ? overlapY : -overlapY;

                PushNodeWithChainReaction(ownerNode, other, pushX, pushY);
            }

            // Refresh moved branch visuals after potential node pushes.
            UpdateSingleSatellitePort(ownerNode, movedBranch, movedBranchIndex);
            UpdateConnectionsForBranch(ownerNode, movedBranch);
            UpdateSatelliteLine(ownerNode, movedBranch);
        }

        private void PushNodeWithChainReaction(WorkflowNode ownerNode, WorkflowNode startNode, double initialPushX, double initialPushY)
        {
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Nodes.Count == 0) return;
            if (startNode.Border == null) return;

            var queue = new Queue<(WorkflowNode node, double pushX, double pushY)>();
            queue.Enqueue((startNode, initialPushX, initialPushY));

            int safety = 0;
            const int maxIterations = 200;
            const double extraGap = 8d;

            while (queue.Count > 0 && safety < maxIterations)
            {
                safety++;
                var item = queue.Dequeue();
                var current = item.node;
                if (current == null || current.Border == null) continue;
                if (ReferenceEquals(current, ownerNode)) continue;

                if (Math.Abs(item.pushX) > 0.001 || Math.Abs(item.pushY) > 0.001)
                {
                    _host.UpdateNodePosition(current, current.X + item.pushX, current.Y + item.pushY);
                }

                var currentRect = GetElementRect(current.Border);
                if (currentRect.IsEmpty) continue;
                currentRect.Inflate(extraGap, extraGap);

                foreach (var other in vm.Nodes)
                {
                    if (other == null || ReferenceEquals(other, current) || ReferenceEquals(other, ownerNode) || other.Border == null) continue;
                    if (!_host.WorkflowCanvas.Children.Contains(other.Border)) continue;

                    var otherRect = GetElementRect(other.Border);
                    if (otherRect.IsEmpty || !currentRect.IntersectsWith(otherRect)) continue;

                    var curCx = currentRect.X + (currentRect.Width / 2.0);
                    var curCy = currentRect.Y + (currentRect.Height / 2.0);
                    var otherCx = otherRect.X + (otherRect.Width / 2.0);
                    var otherCy = otherRect.Y + (otherRect.Height / 2.0);

                    var dx = otherCx - curCx;
                    var dy = otherCy - curCy;
                    if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                    {
                        dx = item.pushX != 0 ? Math.Sign(item.pushX) : 1;
                        dy = item.pushY != 0 ? Math.Sign(item.pushY) : 0;
                    }

                    var overlapX = ((currentRect.Width / 2.0) + (otherRect.Width / 2.0)) - Math.Abs(dx);
                    var overlapY = ((currentRect.Height / 2.0) + (otherRect.Height / 2.0)) - Math.Abs(dy);
                    if (overlapX <= 0 || overlapY <= 0) continue;

                    double pushX = 0;
                    double pushY = 0;
                    if (overlapX < overlapY)
                        pushX = dx >= 0 ? overlapX : -overlapX;
                    else
                        pushY = dy >= 0 ? overlapY : -overlapY;

                    if (Math.Abs(pushX) <= 0.001 && Math.Abs(pushY) <= 0.001) continue;
                    queue.Enqueue((other, pushX, pushY));
                }
            }
        }

        private static Rect GetElementRect(FrameworkElement element)
        {
            if (element == null) return Rect.Empty;
            var left = Canvas.GetLeft(element);
            var top = Canvas.GetTop(element);
            if (double.IsNaN(left) || double.IsNaN(top)) return Rect.Empty;

            var width = element.ActualWidth;
            var height = element.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = element.DesiredSize.Width;
                height = element.DesiredSize.Height;
            }
            if (width <= 0 || height <= 0) return Rect.Empty;

            return new Rect(left, top, width, height);
        }

        /// <summary>
        /// Cập nhật connections liên quan đến port của branch.
        /// </summary>
        private void UpdateConnectionsForBranch(WorkflowNode node, ConditionalBranch branch)
        {
            var viewModel = _host.ViewModel;
            if (viewModel == null || branch.Port == null) return;

            foreach (var conn in viewModel.Connections)
            {
                if (conn.FromPort == branch.Port || conn.ToPort == branch.Port ||
                    conn.FromNode == node || conn.ToNode == node)
                {
                    _host.UpdateConnectionPath(conn);
                }
            }
        }

        // ==================== DIAMOND PORT RENDERING ====================

        /// <summary>
        /// Render ports cho diamond mode:
        /// - Port IN ở bên trái diamond
        /// - Port OUT ở bên phải mỗi satellite circle
        /// </summary>
        public void RenderDiamondNodePorts(WorkflowNode node)
        {
            if (node.Border == null) return;

            // Input port on selected side of diamond
            var inputPort = node.Ports.FirstOrDefault(p => p.IsInput && p.IsVisible);
            if (inputPort != null)
            {
                if (inputPort.PortUI == null)
                {
                    var portColor = GetColorFromTheme(node.Ports.Where(s => s.IsInput).FirstOrDefault()?.ColorKey + "Brush") ?? Colors.Cyan;
                    inputPort.PortUI = _portRenderer.CreatePort(portColor);
                }

                var inputPoint = GetDiamondPortAnchor(node, inputPort.Position);
                double portX = inputPoint.X;
                double portY = inputPoint.Y;

                Canvas.SetLeft(inputPort.PortUI, portX - 9);
                Canvas.SetTop(inputPort.PortUI, portY - 9);
                inputPort.PositionPoint = new Point(portX, portY);

                if (!_host.WorkflowCanvas.Children.Contains(inputPort.PortUI))
                {
                    _host.WorkflowCanvas.Children.Add(inputPort.PortUI);
                }
            }

            // Output ports on satellites
            for (int i = 0; i < node.ConditionalBranches.Count; i++)
            {
                var branch = node.ConditionalBranches[i];
                if (branch.Port == null) continue;

                string colorKey = ColorPortsByIndex[i % ColorPortsByIndex.Count];
                branch.Port.ColorKey = colorKey;
                var portColor = GetColorFromTheme(colorKey + "Brush") ?? Colors.Orange;

                if (branch.Port.PortUI == null)
                {
                    branch.Port.PortUI = CreateInteractiveDiamondPort(branch.Port, portColor);
                }
                else if (!IsInteractiveDiamondPort(branch.Port.PortUI))
                {
                    if (_host.WorkflowCanvas.Children.Contains(branch.Port.PortUI))
                    {
                        _host.WorkflowCanvas.Children.Remove(branch.Port.PortUI);
                    }
                    branch.Port.PortUI = CreateInteractiveDiamondPort(branch.Port, portColor);
                }
                else if (branch.Port.PortUI is Shape shape)
                {
                    shape.Fill = new SolidColorBrush(portColor);
                }
                else if (branch.Port.PortUI is Rectangle rect)
                {
                    rect.Fill = new SolidColorBrush(portColor);
                }

                if (branch.Port.PortUI is Rectangle diamondRect)
                {
                    // Harden diamond port style after any hover/reset logic.
                    if (diamondRect.Tag is not Size)
                    {
                        diamondRect.Tag = new Size(14, 14);
                    }
                    diamondRect.Width = 14;
                    diamondRect.Height = 14;
                    diamondRect.RenderTransformOrigin = new Point(0.5, 0.5);
                    if (diamondRect.RenderTransform is not RotateTransform rt || Math.Abs(rt.Angle - 45) > 0.01)
                    {
                        diamondRect.RenderTransform = new RotateTransform(45);
                    }
                }

                if (branch.SatelliteBorder != null)
                {
                    UpdateSingleSatellitePort(node, branch, i);
                    UpdateSatelliteVisualPorts(node, branch);
                    var inPoint = GetSatelliteInPoint(
                        branch, Canvas.GetLeft(branch.SatelliteBorder), Canvas.GetTop(branch.SatelliteBorder));
                    UpdateSatelliteArrowHead(node, branch, inPoint);
                }

                if (!_host.WorkflowCanvas.Children.Contains(branch.Port.PortUI))
                {
                    _host.WorkflowCanvas.Children.Add(branch.Port.PortUI);
                }

                // Diamond out overlay chỉ là visual, không cho drag connection trực tiếp từ diamond.
                branch.Port.PortUI.IsHitTestVisible = true;
                branch.Port.PortUI.Visibility = Visibility.Visible;
                branch.Port.PortUI.Opacity = 1.0;
            }

            UpdateDiamondAddCirclePosition(node);
            SyncDiamondOverlayZIndex(node);
        }

        private static Point GetDiamondPortAnchor(WorkflowNode node, PortPosition position)
        {
            return position switch
            {
                PortPosition.Left => new Point(node.X, node.Y + (ConditionalDiamondControl.DiamondHeight / 2)),
                PortPosition.Right => new Point(node.X + ConditionalDiamondControl.DiamondWidth, node.Y + (ConditionalDiamondControl.DiamondHeight / 2)),
                PortPosition.Top => new Point(node.X + (ConditionalDiamondControl.DiamondWidth / 2), node.Y),
                PortPosition.Bottom => new Point(node.X + (ConditionalDiamondControl.DiamondWidth / 2), node.Y + ConditionalDiamondControl.DiamondHeight),
                _ => new Point(node.X, node.Y + (ConditionalDiamondControl.DiamondHeight / 2))
            };
        }

        public void RefreshAllDiamondInternalLineStyles()
        {
            var vm = _host.ViewModel;
            if (vm == null) return;

            foreach (var node in vm.Nodes.Where(n => n.IsConditionalNode && n.ConditionalVisualMode == ConditionalVisualMode.Diamond))
            {
                foreach (var branch in node.ConditionalBranches)
                {
                    if (branch.SatelliteLine == null || branch.SatelliteBorder == null) continue;
                    var satX = Canvas.GetLeft(branch.SatelliteBorder);
                    var satY = Canvas.GetTop(branch.SatelliteBorder);
                    var inPoint = GetSatelliteInPoint(branch, satX, satY);
                    var inDirection = GetSatelliteInputDirection(branch);
                    var lineEnd = GetArrowBaseMidPoint(GetArrowTipPoint(inPoint, inDirection), inDirection);
                    ConditionalDiamondControl.UpdateLineGeometry(branch.SatelliteLine, GetDiamondOutPoint(node), lineEnd);
                    UpdateSatelliteLineStyle(node, branch);
                    UpdateSatelliteArrowHead(node, branch, inPoint);
                    UpdateSatelliteDeleteButtonPosition(branch);
                    if (_host.IsAnimationEnabled)
                    {
                        var color = Colors.Orange;
                        if (branch.SatelliteLine.Stroke is SolidColorBrush brush)
                            color = brush.Color;
                        ConditionalDiamondControl.StartSatelliteLineAnimation(branch.SatelliteLine, color);
                    }
                    else
                    {
                        ConditionalDiamondControl.StopSatelliteLineAnimation(branch.SatelliteLine);
                    }
                }
            }
        }

        // ==================== SHARED METHODS ====================

        public void AddElseIfBranch(WorkflowNode node)
        {
            int elseIndex = node.ConditionalBranches.FindIndex(b => b.Label == "else");
            var existingOffsets = node.ConditionalBranches.ToDictionary(
                b => b.Id,
                b => new Point(b.SatelliteOffsetX, b.SatelliteOffsetY));

            var newBranch = new ConditionalBranch
            {
                Label = "else if",
                Condition = "condition",
                CanRemove = true
            };

            var portPosition = node.ConditionalBranches.FirstOrDefault()?.Port?.Position ?? PortPosition.Right;
            var newPort = new NodePort
            {
                IsInput = false,
                Position = portPosition,
                IsVisible = true,
                ExecutionMode = PortExecutionMode.Sequential
            };
            newBranch.Port = newPort;
            node.Ports.Add(newPort);

            if (elseIndex >= 0) node.ConditionalBranches.Insert(elseIndex, newBranch);
            else node.ConditionalBranches.Add(newBranch);

            // Giữ nguyên vị trí tuyệt đối của các branch cũ.
            foreach (var branch in node.ConditionalBranches)
            {
                if (branch.Id == newBranch.Id) continue;
                if (existingOffsets.TryGetValue(branch.Id, out var pt))
                {
                    branch.SatelliteOffsetX = pt.X;
                    branch.SatelliteOffsetY = pt.Y;
                }
            }

            // Chỉ đặt vị trí mặc định cho branch mới, không thay đổi các branch đã kéo.
            int branchIndex = node.ConditionalBranches.IndexOf(newBranch);
            var defaultOffset = ConditionalDiamondControl.GetDefaultSatelliteOffset(branchIndex, node.ConditionalBranches.Count);
            // Use widened default spacing in ConditionalDiamondControl for all new branches.
            newBranch.SatelliteOffsetX = defaultOffset.X;
            newBranch.SatelliteOffsetY = defaultOffset.Y;
            PlaceBranchWithoutOverlap(node, newBranch);

            UpdateBranchExecutionOrder(node);
            ReRenderConditionalNode(node);
            _host.SyncAllPortsZIndex(node);
            RenderDiamondNodePorts(node);
            SyncDiamondOverlayZIndex(node);
        }

        public void RemoveBranch(WorkflowNode node, ConditionalBranch branch)
        {
            if (!branch.CanRemove) return;

            var viewModel = _host.ViewModel;
            if (branch.Port != null && viewModel != null)
            {
                var connectionsToRemove = viewModel.Connections
                    .Where(c => c.FromPort == branch.Port || c.ToPort == branch.Port)
                    .ToList();

                foreach (var conn in connectionsToRemove)
                {
                    viewModel.Connections.Remove(conn);
                }
            }

            if (branch.Port?.PortUI != null && _host.WorkflowCanvas.Children.Contains(branch.Port.PortUI))
            {
                _host.WorkflowCanvas.Children.Remove(branch.Port.PortUI);
            }

            // Remove diamond-specific visuals for this branch
            if (node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
            {
                if (branch.SatelliteBorder != null && _host.WorkflowCanvas.Children.Contains(branch.SatelliteBorder))
                    _host.WorkflowCanvas.Children.Remove(branch.SatelliteBorder);
                branch.SatelliteBorder = null;

                if (branch.SatelliteLine != null && _host.WorkflowCanvas.Children.Contains(branch.SatelliteLine))
                    _host.WorkflowCanvas.Children.Remove(branch.SatelliteLine);
                branch.SatelliteLine = null;

                if (branch.SatelliteDeleteButton != null && _host.WorkflowCanvas.Children.Contains(branch.SatelliteDeleteButton))
                    _host.WorkflowCanvas.Children.Remove(branch.SatelliteDeleteButton);
                branch.SatelliteDeleteButton = null;

                if (branch.SatelliteInputVisual != null && _host.WorkflowCanvas.Children.Contains(branch.SatelliteInputVisual))
                    _host.WorkflowCanvas.Children.Remove(branch.SatelliteInputVisual);
                branch.SatelliteInputVisual = null;

                if (branch.DiamondOutputVisual != null && _host.WorkflowCanvas.Children.Contains(branch.DiamondOutputVisual))
                    _host.WorkflowCanvas.Children.Remove(branch.DiamondOutputVisual);
                branch.DiamondOutputVisual = null;

                if (branch.SatelliteArrowHead != null && _host.WorkflowCanvas.Children.Contains(branch.SatelliteArrowHead))
                    _host.WorkflowCanvas.Children.Remove(branch.SatelliteArrowHead);
                branch.SatelliteArrowHead = null;
            }

            if (branch.Port != null)
            {
                node.Ports.Remove(branch.Port);
            }

            node.ConditionalBranches.Remove(branch);

            UpdateBranchExecutionOrder(node);
            ReRenderConditionalNode(node);
            _host.SyncAllPortsZIndex(node);
        }

        public void ReRenderConditionalNode(WorkflowNode node)
        {
            UpdateBranchExecutionOrder(node);

            if (node.TitleTextBlockUI != null && _host.WorkflowCanvas.Children.Contains(node.TitleTextBlockUI))
            {
                _host.WorkflowCanvas.Children.Remove(node.TitleTextBlockUI);
                node.TitleTextBlockUI = null;
            }

            // Remove old border
            if (node.Border != null && _host.WorkflowCanvas.Children.Contains(node.Border))
            {
                _host.WorkflowCanvas.Children.Remove(node.Border);
            }

            // Remove old ports
            foreach (var branch in node.ConditionalBranches)
            {
                if (branch.Port?.PortUI != null && _host.WorkflowCanvas.Children.Contains(branch.Port.PortUI))
                {
                    _host.WorkflowCanvas.Children.Remove(branch.Port.PortUI);
                }
            }

            // Remove diamond-specific visuals
            RemoveDiamondVisuals(node, _host.WorkflowCanvas);

            // Remove input port
            var inputPort = node.Ports.FirstOrDefault(p => p.IsInput && p.IsVisible);
            if (inputPort?.PortUI != null && _host.WorkflowCanvas.Children.Contains(inputPort.PortUI))
            {
                _host.WorkflowCanvas.Children.Remove(inputPort.PortUI);
                inputPort.PortUI = null;
            }

            // Rebuild
            if (node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
            {
                RenderDiamondNode(node);
            }
            else
            {
                var newBorder = CreateConditionalNodeBorder(node);
                node.Border = newBorder;

                Canvas.SetLeft(newBorder, node.X);
                Canvas.SetTop(newBorder, node.Y);
                _host.WorkflowCanvas.Children.Add(newBorder);

                _host.ZIndexManager.InitializeNodeZIndex(node, newBorder);
                RenderConditionalNodePorts(node);
            }

            _host.ZIndexManager.RaiseNodeZIndex(node, Panel.GetZIndex(node.Border));

            // Redraw connections related
            var viewModel = _host.ViewModel;
            if (viewModel != null)
            {
                foreach (var conn in viewModel.Connections)
                {
                    if (conn.FromNode == node || conn.ToNode == node)
                    {
                        _host.UpdateConnectionPath(conn);
                    }
                }
            }

            _host.UpdateMinimap();

            _host.Dispatcher.BeginInvoke(new Action(() => _host.SyncAllPortsZIndex(node)),
                System.Windows.Threading.DispatcherPriority.Loaded);

            _host.Dispatcher.BeginInvoke(new Action(() => SyncDiamondOverlayZIndex(node)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void SyncDiamondOverlayZIndex(WorkflowNode node)
        {
            if (node.ConditionalVisualMode != ConditionalVisualMode.Diamond || node.Border == null) return;

            int nodeZ = Panel.GetZIndex(node.Border);
            int overlayZ = nodeZ + 30;
            int foregroundZ = nodeZ + 40;

            if (_addCircles.TryGetValue(node.Id, out var addCircle))
            {
                Panel.SetZIndex(addCircle, foregroundZ);
            }

            foreach (var branch in node.ConditionalBranches)
            {
                if (branch.SatelliteBorder != null) Panel.SetZIndex(branch.SatelliteBorder, overlayZ);
                if (branch.SatelliteLine != null) Panel.SetZIndex(branch.SatelliteLine, nodeZ - 1);
                if (branch.SatelliteDeleteButton != null) Panel.SetZIndex(branch.SatelliteDeleteButton, foregroundZ);
                if (branch.SatelliteInputVisual != null) Panel.SetZIndex(branch.SatelliteInputVisual, overlayZ + 3);
                if (branch.DiamondOutputVisual != null) Panel.SetZIndex(branch.DiamondOutputVisual, overlayZ + 2);
                if (branch.SatelliteArrowHead != null) Panel.SetZIndex(branch.SatelliteArrowHead, overlayZ + 2);
                if (branch.Port?.PortUI != null) Panel.SetZIndex(branch.Port.PortUI, int.MaxValue - 1000);
            }
        }

        private void PlaceBranchWithoutOverlap(WorkflowNode node, ConditionalBranch newBranch)
        {
            double minGapY = (ConditionalDiamondControl.SatelliteRadius * 2) + 24d;
            double sameColumnDx = Math.Max(30d, ConditionalDiamondControl.SatelliteRadius * 1.1);
            double targetY = newBranch.SatelliteOffsetY;
            double targetX = newBranch.SatelliteOffsetX;
            bool overlap;
            int safety = 0;
            do
            {
                overlap = false;
                foreach (var existing in node.ConditionalBranches)
                {
                    if (ReferenceEquals(existing, newBranch)) continue;
                    if (double.IsNaN(existing.SatelliteOffsetX) || double.IsNaN(existing.SatelliteOffsetY)) continue;
                    if (Math.Abs(existing.SatelliteOffsetX - targetX) < sameColumnDx &&
                        Math.Abs(existing.SatelliteOffsetY - targetY) < minGapY)
                    {
                        targetY = existing.SatelliteOffsetY + minGapY;
                        overlap = true;
                    }
                }
                safety++;
            } while (overlap && safety < 20);

            newBranch.SatelliteOffsetY = targetY;
        }

        private static Point GetDiamondOutPoint(WorkflowNode node)
        {
            var outputPos = GetDiamondOutDirection(node);
            return GetDiamondPortAnchor(node, outputPos);
        }
        
        private static PortPosition GetDiamondOutDirection(WorkflowNode node)
            => node.Ports.FirstOrDefault(p => !p.IsInput && p.IsVisible)?.Position ?? PortPosition.Right;

        private static Point GetSatelliteInPoint(ConditionalBranch branch, double satelliteLeft, double satelliteTop)
        {
            var center = GetSatelliteCenter(branch, satelliteLeft, satelliteTop);
            return GetSatelliteInputDirection(branch) switch
            {
                PortPosition.Right => new Point(center.X + ConditionalDiamondControl.SatelliteRadius, center.Y),
                PortPosition.Top => new Point(center.X, center.Y - ConditionalDiamondControl.SatelliteRadius),
                PortPosition.Bottom => new Point(center.X, center.Y + ConditionalDiamondControl.SatelliteRadius),
                _ => new Point(center.X - ConditionalDiamondControl.SatelliteRadius, center.Y)
            };
        }

        private static Point GetArrowTipPoint(Point satelliteInPoint, PortPosition inputDirection)
        {
            const double tipGap = 14;
            return inputDirection switch
            {
                PortPosition.Right => new Point(satelliteInPoint.X + tipGap, satelliteInPoint.Y),
                PortPosition.Top => new Point(satelliteInPoint.X, satelliteInPoint.Y - tipGap),
                PortPosition.Bottom => new Point(satelliteInPoint.X, satelliteInPoint.Y + tipGap),
                _ => new Point(satelliteInPoint.X - tipGap, satelliteInPoint.Y)
            };
        }

        private static Point GetArrowBaseMidPoint(Point arrowTip, PortPosition inputDirection)
        {
            const double arrowLength = 18;
            return inputDirection switch
            {
                PortPosition.Right => new Point(arrowTip.X + arrowLength, arrowTip.Y),
                PortPosition.Top => new Point(arrowTip.X, arrowTip.Y - arrowLength),
                PortPosition.Bottom => new Point(arrowTip.X, arrowTip.Y + arrowLength),
                _ => new Point(arrowTip.X - arrowLength, arrowTip.Y)
            };
        }

        private static PortPosition GetSatelliteInputDirection(ConditionalBranch branch) => branch.SatelliteInputPosition;

        private static Point GetSatelliteCenter(ConditionalBranch branch, double satelliteLeft, double satelliteTop)
        {
            double centerX = satelliteLeft + ConditionalDiamondControl.SatelliteRadius;
            double centerY = satelliteTop + ConditionalDiamondControl.SatelliteRadius;

            if (branch.SatelliteBorder != null)
            {
                var width = branch.SatelliteBorder.ActualWidth;
                var height = branch.SatelliteBorder.ActualHeight;
                if (width <= 0 || height <= 0)
                {
                    branch.SatelliteBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    width = branch.SatelliteBorder.DesiredSize.Width;
                    height = branch.SatelliteBorder.DesiredSize.Height;
                }
                if (width > 0)
                {
                    centerX = satelliteLeft + (width / 2);
                }
                if (height > 0)
                {
                    centerY = satelliteTop + height - ConditionalDiamondControl.SatelliteRadius;
                }
            }

            return new Point(centerX, centerY);
        }

        private void UpdateSatelliteArrowHead(WorkflowNode node, ConditionalBranch branch, Point satelliteInPoint)
        {
            if (branch.SatelliteArrowHead == null || branch.SatelliteBorder == null) return;

            var inDirection = GetSatelliteInputDirection(branch);
            var to = GetArrowTipPoint(satelliteInPoint, inDirection);
            const double arrowLength = 16;
            const double arrowWidth = 15;
            Point left;
            Point right;
            switch (inDirection)
            {
                case PortPosition.Right:
                    left = new Point(to.X + arrowLength, to.Y - (arrowWidth / 2));
                    right = new Point(to.X + arrowLength, to.Y + (arrowWidth / 2));
                    break;
                case PortPosition.Top:
                    left = new Point(to.X - (arrowWidth / 2), to.Y - arrowLength);
                    right = new Point(to.X + (arrowWidth / 2), to.Y - arrowLength);
                    break;
                case PortPosition.Bottom:
                    left = new Point(to.X - (arrowWidth / 2), to.Y + arrowLength);
                    right = new Point(to.X + (arrowWidth / 2), to.Y + arrowLength);
                    break;
                default:
                    left = new Point(to.X - arrowLength, to.Y - (arrowWidth / 2));
                    right = new Point(to.X - arrowLength, to.Y + (arrowWidth / 2));
                    break;
            }

            if (branch.SatelliteArrowHead is Path arrowPath)
            {
                var fig = new PathFigure
                {
                    StartPoint = to,
                    IsClosed = true,
                    IsFilled = true
                };
                fig.Segments.Add(new LineSegment(left, true));
                fig.Segments.Add(new LineSegment(right, true));
                var geo = new PathGeometry();
                geo.Figures.Add(fig);
                arrowPath.Data = geo;
            }
        }

        private void SyncBranchOverlayZIndex(WorkflowNode node, ConditionalBranch branch)
        {
            if (node.Border == null) return;
            int nodeZ = Panel.GetZIndex(node.Border);
            int overlayZ = nodeZ + 2;
            int foregroundZ = nodeZ + 3;
            if (branch.SatelliteBorder != null) Panel.SetZIndex(branch.SatelliteBorder, overlayZ);
            if (branch.SatelliteLine != null) Panel.SetZIndex(branch.SatelliteLine, nodeZ - 1);
            if (branch.SatelliteDeleteButton != null) Panel.SetZIndex(branch.SatelliteDeleteButton, foregroundZ);
            if (branch.SatelliteInputVisual != null) Panel.SetZIndex(branch.SatelliteInputVisual, overlayZ + 3);
            if (branch.DiamondOutputVisual != null) Panel.SetZIndex(branch.DiamondOutputVisual, overlayZ + 3);
            if (branch.SatelliteArrowHead != null) Panel.SetZIndex(branch.SatelliteArrowHead, overlayZ + 4);
            if (branch.Port?.PortUI != null) Panel.SetZIndex(branch.Port.PortUI, int.MaxValue - 1000);
        }

        private static bool IsInteractiveDiamondPort(FrameworkElement element)
        {
            if (element is not Rectangle rect) return false;
            if (rect.RenderTransform is not RotateTransform rotate) return false;
            return Math.Abs(rotate.Angle - 45) < 0.01;
        }

        private void UpdateSatelliteLineStyle(WorkflowNode node, ConditionalBranch branch)
        {
            if (branch.SatelliteLine == null || branch.SatelliteLine.Data is not PathGeometry currentGeometry || currentGeometry.Figures.Count == 0)
                return;

            var start = currentGeometry.Figures[0].StartPoint;
            var end = GetEndPoint(currentGeometry.Figures[0]);
            var startDir = GetDiamondOutDirection(node);
            var endDir = GetSatelliteInputDirection(branch);
            var styleSnapshot = _host.ConnectionLineStyle;
            var probePath = _host.ConnectionRenderer.CreateConnectionLine(
                start,
                end,
                Colors.White,
                isDashed: false,
                startPortPosition: startDir,
                endPortPosition: endDir);

            if (probePath.Data is PathGeometry geometry)
            {
                branch.SatelliteLine.Data = geometry;
            }
            branch.SatelliteLine.StrokeThickness = probePath.StrokeThickness;
            branch.SatelliteLine.StrokeDashArray = probePath.StrokeDashArray != null
                ? new DoubleCollection(probePath.StrokeDashArray)
                : null;
            // Bảo vệ để tránh side-effect nếu ConnectionRenderer đổi style trong tương lai.
            if (styleSnapshot != _host.ConnectionLineStyle)
            {
                _host.ConnectionLineStyle = styleSnapshot;
            }
        }

        private static Point GetEndPoint(PathFigure figure)
        {
            var last = figure.Segments.LastOrDefault();
            return last switch
            {
                LineSegment line => line.Point,
                BezierSegment bezier => bezier.Point3,
                ArcSegment arc => arc.Point,
                _ => figure.StartPoint
            };
        }

        private static PathGeometry BuildStraightGeometry(Point start, Point end)
        {
            var f = new PathFigure { StartPoint = start };
            f.Segments.Add(new LineSegment(end, true));
            return new PathGeometry(new[] { f });
        }

        private static PathGeometry BuildOrthogonalGeometry(Point start, Point end, PortPosition startDir, PortPosition endDir)
        {
            const double offset = 44;
            var startLead = AdvancePoint(start, startDir, offset);
            var endApproach = GetApproachPoint(end, endDir, offset);
            var f = new PathFigure { StartPoint = start };
            f.Segments.Add(new LineSegment(startLead, true));

            AddOrthogonalBridge(f, startLead, endApproach, preferHorizontalFirst: IsHorizontal(startDir));
            f.Segments.Add(new LineSegment(end, true));
            return new PathGeometry(new[] { f });
        }

        private static PathGeometry BuildArcGeometry(Point start, Point end)
        {
            var ctrlX = (start.X + end.X) / 2;
            var ctrlY = Math.Min(start.Y, end.Y) - 40;
            var f = new PathFigure { StartPoint = start };
            f.Segments.Add(new QuadraticBezierSegment(new Point(ctrlX, ctrlY), end, true));
            return new PathGeometry(new[] { f });
        }

        private static PathGeometry BuildRadialGeometry(Point start, Point end)
        {
            var dx = Math.Abs(end.X - start.X);
            var offset = Math.Max(50, dx * 0.5);
            var f = new PathFigure { StartPoint = start };
            f.Segments.Add(new BezierSegment(
                new Point(start.X + offset, start.Y),
                new Point(end.X - offset, end.Y),
                end, true));
            return new PathGeometry(new[] { f });
        }

        private static PathGeometry BuildBezierGeometry(Point start, Point end, PortPosition startDir, PortPosition endDir)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            var leadDistance = Math.Max(26, Math.Min(48, distance * 0.22));
            var startLead = AdvancePoint(start, startDir, leadDistance);

            // Internal line mềm hơn line global: giảm offset để bớt cong gắt khi gần.
            var c1 = CalculateBezierControlPoint(startLead, startDir, distance, softness: 0.82);
            var c2 = CalculateBezierControlPoint(end, endDir, distance, softness: 0.82);
            c1 = AdjustBezierControlPoint(c1, start, startDir, dx, dy, distance);
            c2 = AdjustBezierControlPoint(c2, end, endDir, dx, dy, distance);
            var f = new PathFigure { StartPoint = start };
            f.Segments.Add(new LineSegment(startLead, true));
            f.Segments.Add(new BezierSegment(
                c1,
                c2,
                end, true));
            return new PathGeometry(new[] { f });
        }

        private static Point CalculateBezierControlPoint(Point portPoint, PortPosition direction, double distance, double softness = 1.0)
        {
            var offset = Math.Min(distance * 0.42 * softness, 185);
            offset = Math.Max(offset, 56);
            return direction switch
            {
                PortPosition.Right => new Point(portPoint.X + offset, portPoint.Y),
                PortPosition.Left => new Point(portPoint.X - offset, portPoint.Y),
                PortPosition.Bottom => new Point(portPoint.X, portPoint.Y + offset),
                PortPosition.Top => new Point(portPoint.X, portPoint.Y - offset),
                _ => new Point(portPoint.X + offset, portPoint.Y)
            };
        }

        private static Point AdjustBezierControlPoint(
            Point controlPoint,
            Point portPoint,
            PortPosition direction,
            double dx,
            double dy,
            double distance)
        {
            if (direction == PortPosition.Right && dx < 0)
            {
                var extraOffset = Math.Max(Math.Abs(dx) * 0.5, 100);
                controlPoint = new Point(portPoint.X + distance * 0.4 + extraOffset, portPoint.Y);
                if (Math.Abs(dy) < 100)
                    controlPoint = new Point(portPoint.X + distance * 0.4, portPoint.Y + dy * 0.5);
            }
            else if (direction == PortPosition.Left && dx > 0)
            {
                var extraOffset = Math.Max(Math.Abs(dx) * 0.5, 100);
                controlPoint = new Point(portPoint.X - distance * 0.4 - extraOffset, portPoint.Y);
                if (Math.Abs(dy) < 100)
                    controlPoint = new Point(portPoint.X - distance * 0.4, portPoint.Y + dy * 0.5);
            }
            else if (direction == PortPosition.Top && dy > 0)
            {
                var extraOffset = Math.Max(Math.Abs(dy) * 0.5, 100);
                controlPoint = new Point(portPoint.X, portPoint.Y - distance * 0.4 - extraOffset);
                if (Math.Abs(dx) < 100)
                    controlPoint = new Point(portPoint.X + dx * 0.5, portPoint.Y - distance * 0.4);
            }
            else if (direction == PortPosition.Bottom && dy < 0)
            {
                var extraOffset = Math.Max(Math.Abs(dy) * 0.5, 100);
                controlPoint = new Point(portPoint.X, portPoint.Y + distance * 0.4 + extraOffset);
                if (Math.Abs(dx) < 100)
                    controlPoint = new Point(portPoint.X + dx * 0.5, portPoint.Y + distance * 0.4);
            }

            return controlPoint;
        }

        private static bool IsHorizontal(PortPosition position)
            => position == PortPosition.Left || position == PortPosition.Right;

        private static Point AdvancePoint(Point point, PortPosition direction, double distance)
        {
            return direction switch
            {
                PortPosition.Left => new Point(point.X - distance, point.Y),
                PortPosition.Right => new Point(point.X + distance, point.Y),
                PortPosition.Top => new Point(point.X, point.Y - distance),
                PortPosition.Bottom => new Point(point.X, point.Y + distance),
                _ => point
            };
        }

        private static Point GetApproachPoint(Point end, PortPosition endDir, double distance)
        {
            return endDir switch
            {
                PortPosition.Left => new Point(end.X + distance, end.Y),
                PortPosition.Right => new Point(end.X - distance, end.Y),
                PortPosition.Top => new Point(end.X, end.Y + distance),
                PortPosition.Bottom => new Point(end.X, end.Y - distance),
                _ => new Point(end.X - distance, end.Y)
            };
        }

        private static void AddOrthogonalBridge(PathFigure figure, Point from, Point to, bool preferHorizontalFirst)
        {
            if (Math.Abs(from.X - to.X) < 0.01 || Math.Abs(from.Y - to.Y) < 0.01)
            {
                figure.Segments.Add(new LineSegment(to, true));
                return;
            }

            if (preferHorizontalFirst)
            {
                figure.Segments.Add(new LineSegment(new Point(to.X, from.Y), true));
            }
            else
            {
                figure.Segments.Add(new LineSegment(new Point(from.X, to.Y), true));
            }

            figure.Segments.Add(new LineSegment(to, true));
        }

        private static void UpdateBranchExecutionOrder(WorkflowNode node)
        {
            int order = 0;
            foreach (var branch in node.ConditionalBranches)
            {
                if (branch.Port != null)
                {
                    branch.Port.ExecutionMode = PortExecutionMode.Sequential;
                    branch.Port.ExecutionOrder = order++;
                }
            }
        }

        // ==================== CLASSIC PORT RENDERING ====================

        public void RenderConditionalNodePorts(WorkflowNode node)
        {
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
                {
                    _host.WorkflowCanvas.Children.Add(inputPort.PortUI);
                }
            }

            for (int i = 0; i < node.ConditionalBranches.Count; i++)
            {
                var branch = node.ConditionalBranches[i];
                if (branch.Port == null) continue;

                // Mỗi điều kiện (index i) có màu port theo thứ tự trong ColorPortsByIndex; line kết nối dùng FromPort.ColorKey trong ConnectionRenderer
                string colorKey = ColorPortsByIndex[i % ColorPortsByIndex.Count];
                branch.Port.ColorKey = colorKey;
                var portColor = GetColorFromTheme(colorKey + "Brush") ?? Colors.Orange;

                if (branch.Port.PortUI == null)
                {
                    branch.Port.PortUI = _portRenderer.CreatePort(portColor);
                }
                else if (branch.Port.PortUI is Shape shape)
                {
                    shape.Fill = new SolidColorBrush(portColor);
                }

                double portY = node.Y + headerHeight + (i * branchHeight) + (branchHeight / 2);
                double portX = branch.Port.Position == PortPosition.Right ? node.X + nodeWidth : node.X;

                Canvas.SetLeft(branch.Port.PortUI, portX - 9);
                Canvas.SetTop(branch.Port.PortUI, portY - 9);
                branch.Port.PositionPoint = new Point(portX, portY);

                if (!_host.WorkflowCanvas.Children.Contains(branch.Port.PortUI))
                {
                    _host.WorkflowCanvas.Children.Add(branch.Port.PortUI);
                }
            }

            // Update regular ports on sides (not branch ports)
            var regularPorts = node.Ports
                .Where(p => p.IsVisible && p != inputPort && !node.ConditionalBranches.Any(b => b.Port == p))
                .Select(p => p.Position)
                .Distinct();

            foreach (var position in regularPorts)
            {
                _portRenderer.UpdatePortsPositionOnSide(node, position);
            }
        }

        // ==================== LAYOUT SWITCHING ====================

        /// <summary>
        /// Chuyển từ Classic sang Diamond mode.
        /// </summary>
        public void ApplyDiamondLayout(WorkflowNode node)
        {
            var canvas = _host.WorkflowCanvas;
            var vm = _host.ViewModel;
            if (vm == null) return;

            // Remove all existing port UIs
            foreach (var p in node.Ports.ToList())
            {
                if (p.PortUI != null && canvas.Children.Contains(p.PortUI))
                    canvas.Children.Remove(p.PortUI);
                p.PortUI = null;
            }

            // Remove connections
            foreach (var c in vm.Connections.ToList())
            {
                if (c.FromPort != null && node.Ports.Contains(c.FromPort)) { _host.ConnectionRenderer.RemoveConnectionVisuals(c); vm.Connections.Remove(c); continue; }
                if (c.ToPort != null && node.Ports.Contains(c.ToPort)) { _host.ConnectionRenderer.RemoveConnectionVisuals(c); vm.Connections.Remove(c); continue; }
                if (c.FromNode == node || c.ToNode == node) { _host.ConnectionRenderer.RemoveConnectionVisuals(c); vm.Connections.Remove(c); }
            }

            // Reset satellite offsets for fresh layout
            foreach (var branch in node.ConditionalBranches)
            {
                branch.SatelliteOffsetX = double.NaN;
                branch.SatelliteOffsetY = double.NaN;
            }

            node.ConditionalVisualMode = ConditionalVisualMode.Diamond;
            ReRenderConditionalNode(node);
        }

        /// <summary>
        /// Chuyển từ Diamond sang Classic mode.
        /// </summary>
        public void RestoreClassicLayout(WorkflowNode node)
        {
            var canvas = _host.WorkflowCanvas;
            var vm = _host.ViewModel;
            if (vm == null) return;

            // Remove diamond visuals first
            RemoveDiamondVisuals(node, canvas);

            // Remove all existing port UIs
            foreach (var p in node.Ports.ToList())
            {
                if (p.PortUI != null && canvas.Children.Contains(p.PortUI))
                    canvas.Children.Remove(p.PortUI);
                p.PortUI = null;
            }

            // Remove connections
            foreach (var c in vm.Connections.ToList())
            {
                if (c.FromPort != null && node.Ports.Contains(c.FromPort)) { _host.ConnectionRenderer.RemoveConnectionVisuals(c); vm.Connections.Remove(c); continue; }
                if (c.ToPort != null && node.Ports.Contains(c.ToPort)) { _host.ConnectionRenderer.RemoveConnectionVisuals(c); vm.Connections.Remove(c); continue; }
                if (c.FromNode == node || c.ToNode == node) { _host.ConnectionRenderer.RemoveConnectionVisuals(c); vm.Connections.Remove(c); }
            }

            node.ConditionalVisualMode = ConditionalVisualMode.Classic;
            ReRenderConditionalNode(node);
        }

        // ==================== THEME HELPERS ====================

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

        private static Brush? GetBrushFromTheme(string resourceKey)
        {
            try
            {
                return Application.Current.TryFindResource(resourceKey) as Brush;
            }
            catch
            {
                return null;
            }
        }
    }
}
