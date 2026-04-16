using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShapesPath = System.Windows.Shapes.Path;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using FlowMy.Workflow;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow : IWorkflowEditorHost
    {
        WorkflowEditorViewModel? IWorkflowEditorHost.ViewModel => ViewModel;
        Canvas IWorkflowEditorHost.WorkflowCanvas => WorkflowCanvas;
        Canvas IWorkflowEditorHost.GridCanvas => GridCanvas;
        Canvas IWorkflowEditorHost.MinimapCanvas => MinimapCanvas;
        ScrollViewer IWorkflowEditorHost.ScrollViewer => ScrollViewer;
        ScaleTransform IWorkflowEditorHost.ScaleTransform => ScaleTransform;
        TranslateTransform IWorkflowEditorHost.TranslateTransform => TranslateTransform;
        ScaleTransform? IWorkflowEditorHost.GridScaleTransform => GridScaleTransform;
        TranslateTransform? IWorkflowEditorHost.GridTranslateTransform => GridTranslateTransform;
        System.Windows.Threading.Dispatcher IWorkflowEditorHost.Dispatcher => Dispatcher;
        Window IWorkflowEditorHost.OwnerWindow => this;

        ZIndexManager IWorkflowEditorHost.ZIndexManager => _zIndexManager;
        FlowMy.Services.Rendering.IConnectionRenderer IWorkflowEditorHost.ConnectionRenderer => _connectionRenderer;
        FlowMy.Services.Utilities.ViewportCullingService? IWorkflowEditorHost.ViewportCullingService => _viewportCullingService;

        FlowMy.Services.Rendering.ConnectionLineStyle IWorkflowEditorHost.ConnectionLineStyle { get => _connectionLineStyle; set => _connectionLineStyle = value; }
        FlowMy.Services.Rendering.ConnectionColorMode IWorkflowEditorHost.ConnectionColorMode { get => _connectionColorMode; set => _connectionColorMode = value; }
        Color IWorkflowEditorHost.CustomConnectionColor { get => _customConnectionColor; set => _customConnectionColor = value; }
        bool IWorkflowEditorHost.IsAnimationEnabled { get => _isAnimationEnabled; set => _isAnimationEnabled = value; }
        FlowMy.Services.Rendering.ConnectionAnimationDisplayMode IWorkflowEditorHost.ConnectionAnimationDisplayMode { get => _connectionAnimationDisplayMode; set => _connectionAnimationDisplayMode = value; }
        FlowMy.Services.Rendering.ConnectionEnergyColorMode IWorkflowEditorHost.ConnectionEnergyColorMode { get => _connectionEnergyColorMode; set => _connectionEnergyColorMode = value; }
        Color IWorkflowEditorHost.CustomEnergyColor { get => _customEnergyColor; set => _customEnergyColor = value; }
        double IWorkflowEditorHost.EnergyDotGap { get => _energyDotGap; set => _energyDotGap = value; }
        double IWorkflowEditorHost.EnergyDotThicknessExtra { get => _energyDotThicknessExtra; set => _energyDotThicknessExtra = value; }
        string IWorkflowEditorHost.EnergyDotText { get => _energyDotText; set => _energyDotText = value ?? string.Empty; }
        bool IWorkflowEditorHost.EnergyDotTextRotate { get => _energyDotTextRotate; set => _energyDotTextRotate = value; }
        double IWorkflowEditorHost.EnergyRunSpeed { get => _energyRunSpeed; set => _energyRunSpeed = value; }
        double IWorkflowEditorHost.EnergyTextSpinSeconds { get => _energyTextSpinSeconds; set => _energyTextSpinSeconds = value; }
        bool IWorkflowEditorHost.EnergyMeteorMode { get => _energyMeteorMode; set => _energyMeteorMode = value; }

        WorkflowConnection? IWorkflowEditorHost.SelectedConnection { get => _selectedConnection; set => _selectedConnection = value; }

        WorkflowNode? IWorkflowEditorHost.DraggedNode { get => _draggedNode; set => _draggedNode = value; }
        Point IWorkflowEditorHost.DragOffset { get => _dragOffset; set => _dragOffset = value; }
        WorkflowNode? IWorkflowEditorHost.ConnectingFromNode { get => _connectingFromNode; set => _connectingFromNode = value; }
        ShapesPath? IWorkflowEditorHost.TempLine { get => _tempLine; set => _tempLine = value; }

        bool IWorkflowEditorHost.IsPanning { get => _isPanning; set => _isPanning = value; }
        Point IWorkflowEditorHost.PanStartPoint { get => _panStartPoint; set => _panStartPoint = value; }

        bool IWorkflowEditorHost.IsDraggingFromTemplate { get => _isDraggingFromTemplate; set => _isDraggingFromTemplate = value; }
        string? IWorkflowEditorHost.DraggingNodeType { get => _draggingNodeType; set => _draggingNodeType = value; }
        Border? IWorkflowEditorHost.DragGhost { get => _dragGhost; set => _dragGhost = value; }

        double IWorkflowEditorHost.ZoomLevel { get => _zoomLevel; set => _zoomLevel = value; }
        double IWorkflowEditorHost.MinZoom => MinZoom;
        double IWorkflowEditorHost.MaxZoom => MaxZoom;

        void IWorkflowEditorHost.RenderConnection(WorkflowConnection connection) => RenderConnection(connection);
        ShapesPath IWorkflowEditorHost.CreateConnectionLine(Point start, Point end, Color color, bool isDashed, PortPosition? startPortPosition, PortPosition? endPortPosition)
            => CreateConnectionLine(start, end, color, isDashed, startPortPosition, endPortPosition);
        Point IWorkflowEditorHost.ShortenPoint(Point point, PortPosition direction, double gap) => ShortenPoint(point, direction, gap);

        void IWorkflowEditorHost.UpdateConnectionPath(WorkflowConnection connection) => UpdateConnectionPath(connection);
        void IWorkflowEditorHost.UpdateMinimap() => UpdateMinimap();
        void IWorkflowEditorHost.UpdateCanvasSize() => UpdateCanvasSize();
        void IWorkflowEditorHost.RenderConditionalNodePorts(WorkflowNode node) => RenderConditionalNodePorts(node);
        void IWorkflowEditorHost.UpdatePortsPositionOnSide(WorkflowNode node, PortPosition position) => UpdatePortsPositionOnSide(node, position);

        void IWorkflowEditorHost.ClampNodeDragToAutoScheduledScope(WorkflowNode? draggedNode, ref double newX, ref double newY) =>
            ClampNodeDragToAutoScheduledScope(draggedNode, ref newX, ref newY);

        void IWorkflowEditorHost.NodeMouseDown(object sender, MouseButtonEventArgs e) => Node_MouseDown(sender, e);
        void IWorkflowEditorHost.NodeMouseMove(object sender, MouseEventArgs e) => Node_MouseMove(sender, e);
        void IWorkflowEditorHost.NodeMouseUp(object sender, MouseButtonEventArgs e) => Node_MouseUp(sender, e);
        void IWorkflowEditorHost.NodeBorderMouseEnter(object sender, MouseEventArgs e) => NodeBorder_MouseEnter(sender, e);
        void IWorkflowEditorHost.NodeBorderMouseLeave(object sender, MouseEventArgs e) => NodeBorder_MouseLeave(sender, e);
        void IWorkflowEditorHost.PortMouseDown(object sender, MouseButtonEventArgs e) => Port_MouseDown(sender, e);
        void IWorkflowEditorHost.PortMouseUp(object sender, MouseButtonEventArgs e) => Port_MouseUp(sender, e);

        void IWorkflowEditorHost.CreateNodeFromTemplate(string nodeType, double x, double y) => CreateNodeFromTemplate(nodeType, x, y);
        void IWorkflowEditorHost.RemoveDragGhost() => RemoveDragGhost();
        Border? IWorkflowEditorHost.FindTemplateBorder(FrameworkElement element) => FindTemplateBorder(element);

        void IWorkflowEditorHost.FocusWindow() => Focus();
        void IWorkflowEditorHost.RestoreAllNodesZIndex()
        {
            if (ViewModel != null)
            {
                _zIndexManager.RestoreAllNodesZIndex(ViewModel.Nodes);
            }
        }
        void IWorkflowEditorHost.SyncAllPortsZIndex(WorkflowNode node) => SyncAllPortsZIndex(node);

        ContextMenu IWorkflowEditorHost.CreateNodeContextMenu(WorkflowNode node) => CreateNodeContextMenu(node);

        void IWorkflowEditorHost.DuplicateNode(WorkflowNode node) => DuplicateNode(node);
        void IWorkflowEditorHost.DuplicateNodeAtPosition(WorkflowNode node, double x, double y) => DuplicateNodeAtPosition(node, x, y);
        void IWorkflowEditorHost.RequestEditNodeTitle(WorkflowNode node) => RequestEditNodeTitle(node);
        void IWorkflowEditorHost.RequestSyncDataPanels(bool immediate) => _eventService.RequestSyncDataPanels(immediate);

        void IWorkflowEditorHost.RequestRunSingleNode(WorkflowNode node)
        {
            if (ViewModel == null || node == null) return;
            _ = ViewModel.RunSingleNodeAsync(node);
        }

        void IWorkflowEditorHost.ApplyAsyncTaskLoopLikeLayout(AsyncTaskNode node) => ApplyAsyncTaskLoopLikeLayout(node);

        void IWorkflowEditorHost.RestoreAsyncTaskManualLayout(AsyncTaskNode node) => RestoreAsyncTaskManualLayout(node);

        void IWorkflowEditorHost.ApplyConditionalDiamondLayout(WorkflowNode node) => ApplyConditionalDiamondLayout(node);

        void IWorkflowEditorHost.RestoreConditionalClassicLayout(WorkflowNode node) => RestoreConditionalClassicLayout(node);
    }
}

