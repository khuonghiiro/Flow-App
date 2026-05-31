using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Chế độ OCR engine.
    /// </summary>
    public enum OcrEngineMode
    {
        /// <summary>Windows.Media.Ocr - tích hợp sẵn Windows, hỗ trợ nhiều ngôn ngữ.</summary>
        WindowsOcr = 0,
        /// <summary>OpenCV + ML.NET/ONNX Runtime - mạnh hơn, cần GPU tốt.</summary>
        OpenCvMlNet = 1,
        /// <summary>Tự động phát hiện ngôn ngữ.</summary>
        AutoDetect = 2
    }

    /// <summary>
    /// Nguồn ảnh đầu vào cho OCR.
    /// </summary>
    public enum ImageSourceMode
    {
        /// <summary>Chụp màn hình trực tiếp với vùng chọn.</summary>
        ScreenCapture = 0,
        /// <summary>Lấy ảnh từ node khác (ScreenCaptureNode, MediaGalleryNode, v.v.)</summary>
        FromNode = 1,
        /// <summary>Lấy ảnh từ đường dẫn file hoặc URL.</summary>
        PathOrUrl = 2,
        /// <summary>Lấy ảnh từ base64 string.</summary>
        Base64 = 3
    }

    public sealed class TextScanNode : WorkflowNode
    {
        // ── Chế độ OCR engine ─────────────────────────────────────────────────
        private OcrEngineMode _ocrEngineMode = OcrEngineMode.WindowsOcr;

        // ── Nguồn ảnh ─────────────────────────────────────────────────────────
        private ImageSourceMode _imageSourceMode = ImageSourceMode.ScreenCapture;

        // ── Vùng chụp màn hình thủ công ─────────────────────────────────────────
        private int _captureX;
        private int _captureY;
        private int _captureWidth;
        private int _captureHeight;
        private BitmapImage? _capturedImage;

        // ── Input từ node khác — toạ độ & kích thước vùng cắt ───────────────────
        private string? _coordSourceNodeId;
        private string? _coordSourceOutputKey;

        // ── Input từ node khác — ảnh (base64 hoặc BitmapImage) ───────────────────
        private string? _imageSourceNodeId;
        private string? _imageSourceOutputKey;

        // ── Path / URL nhập tay ─────────────────────────────────────────────────
        private string _imagePath = string.Empty;

        // ── Base64 string nhập tay ──────────────────────────────────────────────
        private string _base64Image = string.Empty;

        // ── Ngôn ngữ OCR ───────────────────────────────────────────────────────
        private string _ocrLanguage = "en"; // ISO 639-2 code: en, vi, ja, ko, zh-Hans, etc.
        private bool _autoDetectLanguage = true;

        // ── Chọn app để chụp (giống ScreenCaptureNode) ─────────────────────────
        private string _targetProcessName = string.Empty;
        private string _targetWindowTitle = string.Empty;

        // ── Kết quả OCR ─────────────────────────────────────────────────────────
        private string _extractedText = string.Empty;
        private string _extractedTextLines = string.Empty; // Text phân theo dòng
        private Dictionary<string, string> _extractedWords = new Dictionary<string, string>(); // Word -> bounding box

        // ── SkipOutputs (giống ScreenCaptureNode) ─────────────────────────────
        /// <summary>Danh sách output keys bị tắt (unchecked trong dialog).</summary>
        public HashSet<string> SkipOutputs { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public TextScanNode()
        {
            Type = NodeType.TextScan;
            Title = "Text Scan (OCR)";
        }

        // ── Chế độ OCR engine ─────────────────────────────────────────────────
        public OcrEngineMode OcrEngineMode
        {
            get => _ocrEngineMode;
            set { if (_ocrEngineMode != value) { _ocrEngineMode = value; OnPropertyChanged(); } }
        }

        // ── Nguồn ảnh ─────────────────────────────────────────────────────────
        public ImageSourceMode ImageSourceMode
        {
            get => _imageSourceMode;
            set { if (_imageSourceMode != value) { _imageSourceMode = value; OnPropertyChanged(); } }
        }

        // ── Vùng chụp màn hình thủ công ─────────────────────────────────────────
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

        // ── Input node — toạ độ vùng cắt ────────────────────────────────────────
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

        // ── Input node — ảnh ────────────────────────────────────────────────────
        public string? ImageSourceNodeId
        {
            get => _imageSourceNodeId;
            set { if (_imageSourceNodeId != value) { _imageSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? ImageSourceOutputKey
        {
            get => _imageSourceOutputKey;
            set { if (_imageSourceOutputKey != value) { _imageSourceOutputKey = value; OnPropertyChanged(); } }
        }

        // ── Path / URL nhập tay ────────────────────────────────────────────────
        public string ImagePath
        {
            get => _imagePath;
            set { if (_imagePath != value) { _imagePath = value ?? string.Empty; OnPropertyChanged(); } }
        }

        // ── Base64 string nhập tay ──────────────────────────────────────────────
        public string Base64Image
        {
            get => _base64Image;
            set { if (_base64Image != value) { _base64Image = value ?? string.Empty; OnPropertyChanged(); } }
        }

        // ── Ngôn ngữ OCR ───────────────────────────────────────────────────────
        /// <summary>Mã ngôn ngữ OCR (ISO 639-2): en, vi, ja, ko, zh-Hans, zh-Hant, etc.</summary>
        public string OcrLanguage
        {
            get => _ocrLanguage;
            set { if (_ocrLanguage != value) { _ocrLanguage = value ?? "en"; OnPropertyChanged(); } }
        }

        /// <summary>Tự động phát hiện ngôn ngữ từ ảnh.</summary>
        public bool AutoDetectLanguage
        {
            get => _autoDetectLanguage;
            set { if (_autoDetectLanguage != value) { _autoDetectLanguage = value; OnPropertyChanged(); } }
        }

        // ── Chọn app để chụp ────────────────────────────────────────────────────
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

        // ── Kết quả OCR ─────────────────────────────────────────────────────────
        /// <summary>Văn bản trích xuất từ ảnh.</summary>
        public string ExtractedText
        {
            get => _extractedText;
            set { if (_extractedText != value) { _extractedText = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Văn bản phân theo dòng (mỗi dòng cách nhau bằng newline).</summary>
        public string ExtractedTextLines
        {
            get => _extractedTextLines;
            set { if (_extractedTextLines != value) { _extractedTextLines = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Từ điển word -> bounding box (dạng JSON string).</summary>
        public Dictionary<string, string> ExtractedWords
        {
            get => _extractedWords;
            set { _extractedWords = value ?? new Dictionary<string, string>(); OnPropertyChanged(); }
        }
    }
}
