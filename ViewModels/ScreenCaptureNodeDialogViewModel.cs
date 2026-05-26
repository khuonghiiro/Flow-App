using CommunityToolkit.Mvvm.ComponentModel;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System.Collections.ObjectModel;

namespace FlowMy.ViewModels
{
    public partial class ScreenCaptureNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly ScreenCaptureNode _scNode;

        // ── Chế độ (Checkbox chính) ──────────────────────────────────────────
        /// <summary>true = chụp màn hình theo toạ độ; false = lấy ảnh theo Path/URL.</summary>
        [ObservableProperty] private bool _isScreenCaptureMode;

        // ── Input node — toạ độ & kích thước ────────────────────────────────
        [ObservableProperty] private string? _coordSourceNodeId;
        [ObservableProperty] private string? _coordSourceOutputKey;
        public ObservableCollection<WorkflowOutputKeyOption> CoordKeyOptions { get; } = new();

        // ── Input node — Path / URL ──────────────────────────────────────────
        [ObservableProperty] private string? _pathSourceNodeId;
        [ObservableProperty] private string? _pathSourceOutputKey;
        public ObservableCollection<WorkflowOutputKeyOption> PathKeyOptions { get; } = new();

        // ── Path / URL nhập tay ──────────────────────────────────────────────
        [ObservableProperty] private string _imagePath = string.Empty;

        // ── Danh sách node có thể chọn ──────────────────────────────────────
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

        public ScreenCaptureNodeDialogViewModel(ScreenCaptureNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _scNode = node ?? throw new ArgumentNullException(nameof(node));

            // Sync từ node → VM
            IsScreenCaptureMode = node.CaptureMode == ScreenCaptureMode.ScreenCapture;

            CoordSourceNodeId    = node.CoordSourceNodeId;
            CoordSourceOutputKey = node.CoordSourceOutputKey;

            PathSourceNodeId    = node.PathSourceNodeId;
            PathSourceOutputKey = node.PathSourceOutputKey;

            ImagePath = node.ImagePath;

            // Load danh sách node
            RefreshAllNodesWithOutputs(AvailableNodeOptions);

            // Refresh key options khi đổi node
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CoordSourceNodeId))
                    RefreshCoordKeyOptions();
                else if (e.PropertyName == nameof(PathSourceNodeId))
                    RefreshPathKeyOptions();
            };

            // Khởi tạo key options
            RefreshCoordKeyOptions();
            RefreshPathKeyOptions();
        }

        protected override string GetDefaultTitle() => "Screen Capture";

        // ── Refresh key options ──────────────────────────────────────────────
        public void RefreshCoordKeyOptions()
        {
            CoordKeyOptions.Clear();
            foreach (var o in GetOutputKeysForNode(CoordSourceNodeId))
                CoordKeyOptions.Add(o);
        }

        public void RefreshPathKeyOptions()
        {
            PathKeyOptions.Clear();
            foreach (var o in GetOutputKeysForNode(PathSourceNodeId))
                PathKeyOptions.Add(o);
        }

        // ── Save ─────────────────────────────────────────────────────────────
        protected override void OnSaveTitle()
        {
            _scNode.CaptureMode = IsScreenCaptureMode
                ? ScreenCaptureMode.ScreenCapture
                : ScreenCaptureMode.PathOrUrl;

            _scNode.CoordSourceNodeId    = string.IsNullOrWhiteSpace(CoordSourceNodeId)    ? null : CoordSourceNodeId;
            _scNode.CoordSourceOutputKey = string.IsNullOrWhiteSpace(CoordSourceOutputKey) ? null : CoordSourceOutputKey;

            _scNode.PathSourceNodeId    = string.IsNullOrWhiteSpace(PathSourceNodeId)    ? null : PathSourceNodeId;
            _scNode.PathSourceOutputKey = string.IsNullOrWhiteSpace(PathSourceOutputKey) ? null : PathSourceOutputKey;

            _scNode.ImagePath = ImagePath ?? string.Empty;

            _scNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }
}
