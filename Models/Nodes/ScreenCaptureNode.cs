using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Chế độ hoạt động của ScreenCaptureNode.
    /// </summary>
    public enum ScreenCaptureMode
    {
        /// <summary>Chụp màn hình theo toạ độ vùng đã chọn (hoặc từ input node).</summary>
        ScreenCapture = 0,
        /// <summary>Lấy ảnh từ đường dẫn file hoặc URL online.</summary>
        PathOrUrl = 1,
        /// <summary>Tự chọn vị trí vùng ảnh - chụp thủ công một lần, tự động chụp lại khi chạy workflow.</summary>
        ManualRegion = 2
    }

    public sealed class ScreenCaptureNode : WorkflowNode
    {
        // ── Chế độ ──────────────────────────────────────────────────────────
        private ScreenCaptureMode _captureMode = ScreenCaptureMode.ScreenCapture;

        // ── Vùng chụp thủ công (chọn bằng overlay) ──────────────────────────
        private int _captureX;
        private int _captureY;
        private int _captureWidth;
        private int _captureHeight;
        private BitmapImage? _capturedImage;

        // ── Input từ node khác — toạ độ & kích thước ────────────────────────
        private string? _coordSourceNodeId;
        private string? _coordSourceOutputKey;

        // ── Input từ node khác — Path / URL ─────────────────────────────────
        private string? _pathSourceNodeId;
        private string? _pathSourceOutputKey;

        // ── Path / URL nhập tay ──────────────────────────────────────────────
        private string _imagePath = string.Empty;

        // ── Kích thước node ──────────────────────────────────────────────────
        private bool _useNativeWidth = false;   // true = theo kích thước ảnh gốc (không giới hạn)
        private double _maxNodeWidth = 500;    // giới hạn width khi UseNativeWidth = false

        // ── Chọn app để chụp (chỉ dùng khi CaptureMode = ScreenCapture) ─────
        private string _targetProcessName = string.Empty;
        private string _targetWindowTitle = string.Empty;

        // ── SkipOutputs (giống ImageProcessingNode) ──────────────────────────
        /// <summary>Danh sách output keys bị tắt (unchecked trong dialog).</summary>
        public HashSet<string> SkipOutputs { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ScreenCaptureNode()
        {
            Type = NodeType.ScreenCapture;
            Title = "Screen Capture";
        }

        // ── Chế độ ──────────────────────────────────────────────────────────
        public ScreenCaptureMode CaptureMode
        {
            get => _captureMode;
            set { if (_captureMode != value) { _captureMode = value; OnPropertyChanged(); } }
        }

        // ── Vùng chụp thủ công ───────────────────────────────────────────────
        public int CaptureX
        {
            get => _captureX;
            set { if (_captureX != value) { _captureX = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCaptureRegion)); } }
        }

        public int CaptureY
        {
            get => _captureY;
            set { if (_captureY != value) { _captureY = value; OnPropertyChanged(); } }
        }

        public int CaptureWidth
        {
            get => _captureWidth;
            set { if (_captureWidth != value) { _captureWidth = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCaptureRegion)); } }
        }

        public int CaptureHeight
        {
            get => _captureHeight;
            set { if (_captureHeight != value) { _captureHeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCaptureRegion)); } }
        }

        public BitmapImage? CapturedImage
        {
            get => _capturedImage;
            set { if (!ReferenceEquals(_capturedImage, value)) { _capturedImage = value; OnPropertyChanged(); } }
        }

        public bool HasCaptureRegion => CaptureWidth > 0 && CaptureHeight > 0;

        // ── Input node — toạ độ ──────────────────────────────────────────────
        public string? CoordSourceNodeId
        {
            get => _coordSourceNodeId;
            set { if (_coordSourceNodeId != value) { _coordSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? CoordSourceOutputKey
        {
            get => _coordSourceOutputKey;
            set { if (_coordSourceOutputKey != value) { _coordSourceOutputKey = value; OnPropertyChanged(); } }
        }

        // ── Input node — Path / URL ──────────────────────────────────────────
        public string? PathSourceNodeId
        {
            get => _pathSourceNodeId;
            set { if (_pathSourceNodeId != value) { _pathSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? PathSourceOutputKey
        {
            get => _pathSourceOutputKey;
            set { if (_pathSourceOutputKey != value) { _pathSourceOutputKey = value; OnPropertyChanged(); } }
        }

        // ── Path / URL nhập tay ──────────────────────────────────────────────
        public string ImagePath
        {
            get => _imagePath;
            set { if (_imagePath != value) { _imagePath = value ?? string.Empty; OnPropertyChanged(); } }
        }

        // ── Kích thước node ──────────────────────────────────────────────────
        /// <summary>true = node rộng theo kích thước ảnh gốc (không giới hạn); false = giới hạn theo MaxNodeWidth.</summary>
        public bool UseNativeWidth
        {
            get => _useNativeWidth;
            set { if (_useNativeWidth != value) { _useNativeWidth = value; OnPropertyChanged(); } }
        }

        /// <summary>Giới hạn width tối đa của node khi UseNativeWidth = false. Mặc định 500px.</summary>
        public double MaxNodeWidth
        {
            get => _maxNodeWidth;
            set { if (Math.Abs(_maxNodeWidth - value) > 0.01) { _maxNodeWidth = Math.Max(80, value); OnPropertyChanged(); } }
        }

        // ── Chọn app để chụp ────────────────────────────────────────────────
        /// <summary>Tên process của app cần đưa lên trước khi chụp (ví dụ: "chrome").</summary>
        public string TargetProcessName
        {
            get => _targetProcessName;
            set { if (_targetProcessName != value) { _targetProcessName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Tiêu đề cửa sổ của app cần đưa lên trước khi chụp.</summary>
        public string TargetWindowTitle
        {
            get => _targetWindowTitle;
            set { if (_targetWindowTitle != value) { _targetWindowTitle = value ?? string.Empty; OnPropertyChanged(); } }
        }
    }
}
