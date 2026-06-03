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
        /// <summary>Tesseract OCR - miễn phí, phổ biến, hỗ trợ nhiều ngôn ngữ.</summary>
        Tesseract = 0
        // WindowsOcr đã bị xóa vì không hỗ trợ trong .NET Framework WPF
        // OpenCvMlNet đã bị xóa vì chưa được implement
    }

    /// <summary>
    /// Chế độ phân trang Tesseract (Page Segmentation Mode).
    /// </summary>
    public enum TesseractPageSegMode
    {
        /// <summary>Orientation and script detection (OSD) only.</summary>
        OsdOnly = 0,
        /// <summary>Automatic page segmentation with OSD.</summary>
        AutoOsd = 1,
        /// <summary>Automatic page segmentation, but no OSD.</summary>
        Auto = 2,
        /// <summary>Fully automatic page segmentation, but no OSD or OCR.</summary>
        AutoNoOcr = 3,
        /// <summary>Assume a single column of text of variable sizes.</summary>
        SingleColumn = 4,
        /// <summary>Assume a single uniform block of vertically aligned text.</summary>
        SingleBlockVertText = 5,
        /// <summary>Assume a single uniform block of text.</summary>
        SingleBlock = 6,
        /// <summary>Treat the image as a single text line.</summary>
        SingleLine = 7,
        /// <summary>Treat the image as a single word.</summary>
        SingleWord = 8,
        /// <summary>Treat the image as a single word in a circle.</summary>
        CircleWord = 9,
        /// <summary>Treat the image as a single character.</summary>
        SingleChar = 10,
        /// <summary>Sparse text. Find as much text as possible in no particular order.</summary>
        SparseText = 11,
        /// <summary>Sparse text with OSD.</summary>
        SparseTextOsd = 12,
        /// <summary>Raw line. Treat the image as a single text line, bypassing hacks that are Tesseract-specific.</summary>
        RawLine = 13
    }

    /// <summary>
    /// Chế độ engine Tesseract (OCR Engine Mode).
    /// </summary>
    public enum TesseractEngineMode
    {
        /// <summary>Legacy engine only.</summary>
        Legacy = 0,
        /// <summary>LSTM neural network engine only.</summary>
        Lstm = 1,
        /// <summary>Legacy + LSTM.</summary>
        LegacyLstm = 2,
        /// <summary>Default, based on what is available.</summary>
        Default = 3
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
        Base64 = 3,
        /// <summary>Tự chọn vị trí vùng ảnh - chụp thủ công một lần, tự động chụp lại khi chạy workflow.</summary>
        ManualRegion = 4
    }

    public sealed class TextScanNode : WorkflowNode
    {
        // ── Chế độ OCR engine ─────────────────────────────────────────────────
        private OcrEngineMode _ocrEngineMode = OcrEngineMode.Tesseract;

        // ── Tesseract settings ─────────────────────────────────────────────────
        private string _tessdataPath = string.Empty; // Đường dẫn đến thư mục tessdata
        private TesseractPageSegMode _tesseractPageSegMode = TesseractPageSegMode.Auto;
        private TesseractEngineMode _tesseractEngineMode = TesseractEngineMode.Default;
        private List<string> _selectedLanguages = new List<string> { "eng", "vie" }; // Danh sách ngôn ngữ đã chọn cho auto-detect

        // ── Nguồn ảnh ─────────────────────────────────────────────────────────
        private ImageSourceMode _imageSourceMode = ImageSourceMode.ManualRegion;

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

        // ── Background Mode ─────────────────────────────────────────────────────
        private bool _useBackgroundMode = false;
        private FlowMy.Helpers.BackgroundInputHelper.InputMode _backgroundInputMode = FlowMy.Helpers.BackgroundInputHelper.InputMode.Auto;

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
        /// <summary>Mã ngôn ngữ OCR (ISO 639-2): en, vie, jpn, kor, chi_sim, chi_tra, etc.</summary>
        public string OcrLanguage
        {
            get => _ocrLanguage;
            set { if (_ocrLanguage != value) { _ocrLanguage = value ?? "eng"; OnPropertyChanged(); } }
        }

        /// <summary>Tự động phát hiện ngôn ngữ từ ảnh.</summary>
        public bool AutoDetectLanguage
        {
            get => _autoDetectLanguage;
            set { if (_autoDetectLanguage != value) { _autoDetectLanguage = value; OnPropertyChanged(); } }
        }

        // ── Tesseract settings ─────────────────────────────────────────────────
        /// <summary>Đường dẫn đến thư mục tessdata (chứa file .traineddata).</summary>
        public string TessdataPath
        {
            get => _tessdataPath;
            set { if (_tessdataPath != value) { _tessdataPath = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Chế độ phân trang Tesseract.</summary>
        public TesseractPageSegMode TesseractPageSegMode
        {
            get => _tesseractPageSegMode;
            set { if (_tesseractPageSegMode != value) { _tesseractPageSegMode = value; OnPropertyChanged(); } }
        }

        /// <summary>Chế độ engine Tesseract.</summary>
        public TesseractEngineMode TesseractEngineMode
        {
            get => _tesseractEngineMode;
            set { if (_tesseractEngineMode != value) { _tesseractEngineMode = value; OnPropertyChanged(); } }
        }

        /// <summary>Danh sách ngôn ngữ đã chọn cho auto-detect.</summary>
        public List<string> SelectedLanguages
        {
            get => _selectedLanguages;
            set { if (_selectedLanguages != value) { _selectedLanguages = value ?? new List<string>(); OnPropertyChanged(); } }
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

        // ── Background Mode ─────────────────────────────────────────────────────

        /// <summary>
        /// Sử dụng Background Mode - chụp màn hình app mà không cần active.
        /// Khi true, app đích sẽ không được đưa lên foreground trước khi chụp.
        /// </summary>
        public bool UseBackgroundMode
        {
            get => _useBackgroundMode;
            set { if (_useBackgroundMode != value) { _useBackgroundMode = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Chế độ gửi input khi UseBackgroundMode = true.
        /// DirectMessage: Nhanh nhất, ít tương thích với game/DirectX.
        /// SilentActivation: Cân bằng, tương thích cao.
        /// ForegroundActivation: Giống user thật nhưng gián đoạn.
        /// Auto: Tự chọn chế độ phù hợp.
        /// </summary>
        public FlowMy.Helpers.BackgroundInputHelper.InputMode BackgroundInputMode
        {
            get => _backgroundInputMode;
            set { if (_backgroundInputMode != value) { _backgroundInputMode = value; OnPropertyChanged(); } }
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
