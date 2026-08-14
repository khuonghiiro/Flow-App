using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class ScreenCaptureNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly ScreenCaptureNode _scNode;

        // ── Chế độ nguồn ảnh ──────────────────────────────────────────────────
        [ObservableProperty] private ScreenCaptureMode _captureMode = ScreenCaptureMode.ScreenCapture;
        public ObservableCollection<CaptureModeOption> CaptureModeOptions { get; } = new();

        // ── Visibility helpers ────────────────────────────────────────────────
        public bool IsScreenCaptureMode => CaptureMode == ScreenCaptureMode.ScreenCapture;
        public bool IsPathUrlMode => CaptureMode == ScreenCaptureMode.PathOrUrl;
        public bool IsManualRegionMode => CaptureMode == ScreenCaptureMode.ManualRegion;

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

        // ── Kích thước node ──────────────────────────────────────────────────
        [ObservableProperty] private bool _useNativeWidth = true;
        [ObservableProperty] private double _maxNodeWidth = 500;

        // ── Chọn app để đưa lên trước khi chụp ──────────────────────────────
        [ObservableProperty] private WindowInfo? _selectedTargetWindow;
        public ObservableCollection<WindowInfo> ActiveWindows { get; } = new();
        public IRelayCommand LoadWindowsCommand { get; }

        // ── Background Mode (chụp không cần active app) ────────────────────
        [ObservableProperty] private bool _useBackgroundMode = false;
        public bool ShowBackgroundModeCheckbox => SelectedTargetWindow != null;

        // ── Danh sách node có thể chọn ──────────────────────────────────────
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

        public ScreenCaptureNodeDialogViewModel(ScreenCaptureNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _scNode = node ?? throw new ArgumentNullException(nameof(node));

            // Sync từ node → VM
            CaptureMode = node.CaptureMode;

            CoordSourceNodeId    = node.CoordSourceNodeId;
            CoordSourceOutputKey = node.CoordSourceOutputKey;

            PathSourceNodeId    = node.PathSourceNodeId;
            PathSourceOutputKey = node.PathSourceOutputKey;

            ImagePath = node.ImagePath;

            // Kích thước node
            UseNativeWidth = node.UseNativeWidth;
            MaxNodeWidth   = node.MaxNodeWidth;

            // Background mode
            UseBackgroundMode = node.UseBackgroundMode;

            // Initialize mode options
            InitializeCaptureModeOptions();

            // Load danh sách node
            RefreshAllNodesWithOutputs(AvailableNodeOptions);

            // Load windows command
            LoadWindowsCommand = new RelayCommand(ExecuteLoadWindows);

            // Load danh sách cửa sổ ngay nếu đang ở chế độ ScreenCapture hoặc ManualRegion
            if (IsScreenCaptureMode || IsManualRegionMode)
                ExecuteLoadWindows();

            // Refresh key options khi đổi node
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CoordSourceNodeId))
                    RefreshCoordKeyOptions();
                else if (e.PropertyName == nameof(PathSourceNodeId))
                    RefreshPathKeyOptions();
                else if (e.PropertyName == nameof(CaptureMode))
                {
                    // Notify visibility changes
                    OnPropertyChanged(nameof(IsScreenCaptureMode));
                    OnPropertyChanged(nameof(IsPathUrlMode));
                    OnPropertyChanged(nameof(IsManualRegionMode));
                    
                    // Load windows for modes that need app selection
                    if ((IsScreenCaptureMode || IsManualRegionMode) && ActiveWindows.Count == 0)
                        ExecuteLoadWindows();
                }
                else if (e.PropertyName == nameof(SelectedTargetWindow))
                {
                    // Show/hide background mode checkbox based on target window selection
                    OnPropertyChanged(nameof(ShowBackgroundModeCheckbox));
                }
            };

            // Khởi tạo key options
            RefreshCoordKeyOptions();
            RefreshPathKeyOptions();
        }

        protected override string GetDefaultTitle() => "Screen Capture";

        // ── Partial callbacks ────────────────────────────────────────────────
        partial void OnUseBackgroundModeChanged(bool value)
        {
            _scNode.UseBackgroundMode = value;
            _host.RequestSyncDataPanels(immediate: true);
        }

        private void InitializeCaptureModeOptions()
        {
            CaptureModeOptions.Clear();
            CaptureModeOptions.Add(new CaptureModeOption
            {
                Value = ScreenCaptureMode.ScreenCapture,
                DisplayName = "Chụp màn hình trực tiếp"
            });
            CaptureModeOptions.Add(new CaptureModeOption
            {
                Value = ScreenCaptureMode.ManualRegion,
                DisplayName = "Tự chọn vị trí vùng ảnh"
            });
            CaptureModeOptions.Add(new CaptureModeOption
            {
                Value = ScreenCaptureMode.PathOrUrl,
                DisplayName = "Path / URL"
            });
        }

        // ── Load danh sách cửa sổ đang mở ───────────────────────────────────
        private void ExecuteLoadWindows()
        {
            string? prevProcess = SelectedTargetWindow?.ProcessName ?? _scNode.TargetProcessName;
            string? prevTitle   = SelectedTargetWindow?.Title       ?? _scNode.TargetWindowTitle;

            var windows = WindowHelper.GetActiveWindows();
            ActiveWindows.Clear();
            foreach (var w in windows)
                ActiveWindows.Add(w);

            if (!string.IsNullOrWhiteSpace(prevProcess))
            {
                // Ưu tiên exact match (title + process)
                var match = ActiveWindows.FirstOrDefault(x =>
                    x.ProcessName == prevProcess && x.Title == prevTitle)
                    ?? ActiveWindows.FirstOrDefault(x => x.ProcessName == prevProcess);
                SelectedTargetWindow = match;
            }
        }

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
            _scNode.CaptureMode = CaptureMode;

            _scNode.CoordSourceNodeId    = string.IsNullOrWhiteSpace(CoordSourceNodeId)    ? null : CoordSourceNodeId;
            _scNode.CoordSourceOutputKey = string.IsNullOrWhiteSpace(CoordSourceOutputKey) ? null : CoordSourceOutputKey;

            _scNode.PathSourceNodeId    = string.IsNullOrWhiteSpace(PathSourceNodeId)    ? null : PathSourceNodeId;
            _scNode.PathSourceOutputKey = string.IsNullOrWhiteSpace(PathSourceOutputKey) ? null : PathSourceOutputKey;

            _scNode.ImagePath = ImagePath ?? string.Empty;

            _scNode.UseNativeWidth = UseNativeWidth;
            _scNode.MaxNodeWidth   = MaxNodeWidth;

            // Lưu target app
            _scNode.TargetProcessName = SelectedTargetWindow?.ProcessName ?? string.Empty;
            _scNode.TargetWindowTitle = SelectedTargetWindow?.Title       ?? string.Empty;

            // Lưu background mode
            _scNode.UseBackgroundMode = UseBackgroundMode;

            _scNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }

    // ── Option class ─────────────────────────────────────────────────────────
    public class CaptureModeOption
    {
        public ScreenCaptureMode Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
