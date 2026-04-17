using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShapesPath = System.Windows.Shapes.Path;
using FlowMy.Models;
using FlowMy.ViewModels;
using FlowMy.Workflow;

namespace FlowMy.Services.Interaction
{
    public interface IWorkflowEditorHost
    {
        // Core references
        WorkflowEditorViewModel? ViewModel { get; }
        Canvas WorkflowCanvas { get; }
        Canvas GridCanvas { get; }
        Canvas MinimapCanvas { get; }
        ScrollViewer ScrollViewer { get; }
        ScaleTransform ScaleTransform { get; }
        TranslateTransform TranslateTransform { get; }
        ScaleTransform? GridScaleTransform { get; }
        TranslateTransform? GridTranslateTransform { get; }
        System.Windows.Threading.Dispatcher Dispatcher { get; }

        /// <summary>The owner Window, used by WebView2AirspaceClipper to resolve overlay elements.</summary>
        Window OwnerWindow { get; }

        ZIndexManager ZIndexManager { get; }
        FlowMy.Services.Rendering.IConnectionRenderer ConnectionRenderer { get; }
        FlowMy.Services.Utilities.ViewportCullingService? ViewportCullingService { get; }

        // Rendering preferences
        FlowMy.Services.Rendering.ConnectionLineStyle ConnectionLineStyle { get; set; }
        FlowMy.Services.Rendering.ConnectionColorMode ConnectionColorMode { get; set; }
        Color CustomConnectionColor { get; set; }
        bool IsAnimationEnabled { get; set; }
        FlowMy.Services.Rendering.ConnectionAnimationDisplayMode ConnectionAnimationDisplayMode { get; set; }
        bool CacheNodeEnabled { get; set; }
        bool NodeSpinnerArcMode { get; set; }
        bool NodeSpinnerMultiColor { get; set; }
        double NodeSpinnerSize { get; set; }
        bool NodeSpinnerScaleWithNode { get; set; }
        double NodeSpinnerSizeRatio { get; set; }
        string NodeSpinnerShape { get; set; }
        string NodeSpinnerPosition { get; set; }
        double NodeSpinnerStrokeThickness { get; set; }
        double NodeSpinnerSpinSeconds { get; set; }
        bool NodeSpinnerBlinkBackground { get; set; }
        string NodeSpinnerBlinkBackgroundColorKey { get; set; }
        string NodeSpinnerBlinkMode { get; set; }
        double NodeSpinnerBlinkIntensity { get; set; }
        double NodeSpinnerBlinkBaseOpacity { get; set; }
        double NodeSpinnerBlinkPeakOpacity { get; set; }

        // Execution "energy" effect preferences (active connection highlight)
        FlowMy.Services.Rendering.ConnectionEnergyColorMode ConnectionEnergyColorMode { get; set; }
        Color CustomEnergyColor { get; set; }
        double EnergyDotGap { get; set; }
        double EnergyDotThicknessExtra { get; set; }
        string EnergyDotText { get; set; }
        bool EnergyDotTextRotate { get; set; }
        double EnergyRunSpeed { get; set; }
        double EnergyTextSpinSeconds { get; set; }
        bool EnergyMeteorMode { get; set; }

        WorkflowConnection? SelectedConnection { get; set; }

        // Mutable editor state (kept on host for now)
        WorkflowNode? DraggedNode { get; set; }
        Point DragOffset { get; set; }
        WorkflowNode? ConnectingFromNode { get; set; }
        ShapesPath? TempLine { get; set; }

        bool IsPanning { get; set; }
        Point PanStartPoint { get; set; }

        bool IsDraggingFromTemplate { get; set; }
        string? DraggingNodeType { get; set; }
        Border? DragGhost { get; set; }

        double ZoomLevel { get; set; }
        double MinZoom { get; }
        double MaxZoom { get; }

        // Rendering/updates
        void RenderConnection(WorkflowConnection connection);
        ShapesPath CreateConnectionLine(Point start, Point end, Color color, bool isDashed, PortPosition? startPortPosition, PortPosition? endPortPosition);
        Point ShortenPoint(Point point, PortPosition direction, double gap);

        void UpdateConnectionPath(WorkflowConnection connection);
        void UpdateMinimap();
        void UpdateCanvasSize();
        void RenderConditionalNodePorts(WorkflowNode node);
        void UpdatePortsPositionOnSide(WorkflowNode node, PortPosition position);
        void UpdateNodePosition(WorkflowNode node, double x, double y);

        /// <summary>Giữ node trong khung nét đứt xanh của Start AutoScheduled (giống nhốt trong LoopBody).</summary>
        void ClampNodeDragToAutoScheduledScope(WorkflowNode? draggedNode, ref double newX, ref double newY);

        // Node/Port event forwarders
        void NodeMouseDown(object sender, MouseButtonEventArgs e);
        void NodeMouseMove(object sender, MouseEventArgs e);
        void NodeMouseUp(object sender, MouseButtonEventArgs e);
        void NodeBorderMouseEnter(object sender, MouseEventArgs e);
        void NodeBorderMouseLeave(object sender, MouseEventArgs e);
        void PortMouseDown(object sender, MouseButtonEventArgs e);
        void PortMouseUp(object sender, MouseButtonEventArgs e);

        // Template helpers
        void CreateNodeFromTemplate(string nodeType, double x, double y);
        void RemoveDragGhost();
        Border? FindTemplateBorder(FrameworkElement element);

        // Misc
        void FocusWindow();
        void RestoreAllNodesZIndex();
        void SyncAllPortsZIndex(WorkflowNode node);

        // Node UI helpers
        ContextMenu CreateNodeContextMenu(WorkflowNode node);

        // Node chrome actions
        void DuplicateNode(WorkflowNode node);
        void DuplicateNodeAtPosition(WorkflowNode node, double x, double y);
        void RequestEditNodeTitle(WorkflowNode node);

        /// <summary>Đồng bộ data panel: immediate=true cập nhật ngay (LostFocus), false=đặt idle timer (TextChanged/Combo), sau DataPanelIdleDelayMs ms không gõ mới cập nhật.</summary>
        void RequestSyncDataPanels(bool immediate);

        // Conditional node: thêm/xóa nhánh else if, build lại UI
        void AddElseIfBranch(WorkflowNode node);
        void RemoveBranch(WorkflowNode node, ConditionalBranch branch);
        void ReRenderConditionalNode(WorkflowNode node);

        /// <summary>Chuyển Conditional Node sang giao diện Diamond (hình thoi + satellite circles).</summary>
        void ApplyConditionalDiamondLayout(WorkflowNode node);

        /// <summary>Khôi phục Conditional Node về giao diện Classic (hình chữ nhật).</summary>
        void RestoreConditionalClassicLayout(WorkflowNode node);

        // Async task node: thêm/xóa task branch, build lại UI
        void AddTaskBranch(WorkflowNode node);
        void RemoveTaskBranch(WorkflowNode node, AsyncTaskBranch branch);
        void ReRenderAsyncTaskNode(WorkflowNode node);

        /// <summary>Chuyển Async Task sang giao diện giống Loop (kim cương + khung body).</summary>
        void ApplyAsyncTaskLoopLikeLayout(AsyncTaskNode node);

        /// <summary>Khôi phục Async Task về nhiều port nhánh tay.</summary>
        void RestoreAsyncTaskManualLayout(AsyncTaskNode node);

        /// <summary>Chạy logic của một node (từ dialog — nút Play). Chỉ thực thi node đó, cập nhật output.</summary>
        void RequestRunSingleNode(WorkflowNode node);
    }
}

