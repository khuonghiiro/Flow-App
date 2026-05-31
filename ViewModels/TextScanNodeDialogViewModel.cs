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
    public partial class TextScanNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly TextScanNode _textScanNode;

        // ── OCR Engine Mode ─────────────────────────────────────────────────────
        [ObservableProperty] private OcrEngineMode _ocrEngineMode = OcrEngineMode.Tesseract;
        public ObservableCollection<OcrEngineModeOption> OcrEngineModeOptions { get; } = new();

        // ── Tesseract settings ─────────────────────────────────────────────────────
        [ObservableProperty] private string _tessdataPath = string.Empty;
        [ObservableProperty] private TesseractPageSegMode _tesseractPageSegMode = TesseractPageSegMode.Auto;
        [ObservableProperty] private TesseractEngineMode _tesseractEngineMode = TesseractEngineMode.Default;
        public ObservableCollection<TesseractPageSegModeOption> TesseractPageSegModeOptions { get; } = new();
        public ObservableCollection<TesseractEngineModeOption> TesseractEngineModeOptions { get; } = new();

        // ── Visibility helpers ────────────────────────────────────────────────────
        public bool IsTesseractMode => OcrEngineMode == OcrEngineMode.Tesseract;

        // ── Image Source Mode ────────────────────────────────────────────────────
        [ObservableProperty] private ImageSourceMode _imageSourceMode = ImageSourceMode.ScreenCapture;
        public ObservableCollection<ImageSourceModeOption> ImageSourceModeOptions { get; } = new();

        // ── Visibility helpers ────────────────────────────────────────────────────
        public bool IsScreenCaptureMode => ImageSourceMode == ImageSourceMode.ScreenCapture;
        public bool IsFromNodeMode => ImageSourceMode == ImageSourceMode.FromNode;
        public bool IsPathUrlMode => ImageSourceMode == ImageSourceMode.PathOrUrl;
        public bool IsBase64Mode => ImageSourceMode == ImageSourceMode.Base64;

        // ── Input node — toạ độ & kích thước vùng cắt ───────────────────────────
        [ObservableProperty] private string? _coordSourceNodeId;
        [ObservableProperty] private string? _coordSourceOutputKey;
        public ObservableCollection<WorkflowOutputKeyOption> CoordKeyOptions { get; } = new();

        // ── Input node — ảnh ─────────────────────────────────────────────────────
        [ObservableProperty] private string? _imageSourceNodeId;
        [ObservableProperty] private string? _imageSourceOutputKey;
        public ObservableCollection<WorkflowOutputKeyOption> ImageKeyOptions { get; } = new();

        // ── Path / URL nhập tay ──────────────────────────────────────────────────
        [ObservableProperty] private string _imagePath = string.Empty;

        // ── Base64 string nhập tay ────────────────────────────────────────────────
        [ObservableProperty] private string _base64Image = string.Empty;

        // ── Ngôn ngữ OCR ─────────────────────────────────────────────────────────
        [ObservableProperty] private string _ocrLanguage = "en";
        [ObservableProperty] private bool _autoDetectLanguage = true;
        public ObservableCollection<string> OcrLanguageOptions { get; } = new();

        // ── Chọn app để đưa lên trước khi chụp ────────────────────────────────────
        [ObservableProperty] private WindowInfo? _selectedTargetWindow;
        public ObservableCollection<WindowInfo> ActiveWindows { get; } = new();
        public IRelayCommand LoadWindowsCommand { get; }

        // ── Danh sách node có thể chọn ────────────────────────────────────────────
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

        public TextScanNodeDialogViewModel(TextScanNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _textScanNode = node ?? throw new ArgumentNullException(nameof(node));

            // Sync từ node → VM
            OcrEngineMode = node.OcrEngineMode;
            ImageSourceMode = node.ImageSourceMode;

            TessdataPath = node.TessdataPath;
            TesseractPageSegMode = node.TesseractPageSegMode;
            TesseractEngineMode = node.TesseractEngineMode;

            CoordSourceNodeId = node.CoordSourceNodeId;
            CoordSourceOutputKey = node.CoordSourceOutputKey;

            ImageSourceNodeId = node.ImageSourceNodeId;
            ImageSourceOutputKey = node.ImageSourceOutputKey;

            ImagePath = node.ImagePath;
            Base64Image = node.Base64Image;

            OcrLanguage = node.OcrLanguage;
            AutoDetectLanguage = node.AutoDetectLanguage;

            // Load danh sách node
            RefreshAllNodesWithOutputs(AvailableNodeOptions);

            // Load windows command
            LoadWindowsCommand = new RelayCommand(ExecuteLoadWindows);

            // Load danh sách cửa sổ ngay nếu đang ở chế độ ScreenCapture
            if (IsScreenCaptureMode)
                ExecuteLoadWindows();

            // Refresh key options khi đổi node
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CoordSourceNodeId))
                    RefreshCoordKeyOptions();
                else if (e.PropertyName == nameof(ImageSourceNodeId))
                    RefreshImageKeyOptions();
                else if (e.PropertyName == nameof(ImageSourceMode) && IsScreenCaptureMode && ActiveWindows.Count == 0)
                    ExecuteLoadWindows();
                else if (e.PropertyName == nameof(ImageSourceMode))
                {
                    // Notify visibility changes
                    OnPropertyChanged(nameof(IsScreenCaptureMode));
                    OnPropertyChanged(nameof(IsFromNodeMode));
                    OnPropertyChanged(nameof(IsPathUrlMode));
                    OnPropertyChanged(nameof(IsBase64Mode));
                }
                else if (e.PropertyName == nameof(OcrEngineMode))
                {
                    OnPropertyChanged(nameof(IsTesseractMode));
                }
            };

            // Khởi tạo options
            InitializeOcrEngineModeOptions();
            InitializeTesseractOptions();
            InitializeImageSourceModeOptions();
            InitializeOcrLanguageOptions();
            RefreshCoordKeyOptions();
            RefreshImageKeyOptions();
        }

        protected override string GetDefaultTitle() => "Text Scan (OCR)";

        private void InitializeOcrEngineModeOptions()
        {
            OcrEngineModeOptions.Clear();
            OcrEngineModeOptions.Add(new OcrEngineModeOption
            {
                Value = OcrEngineMode.Tesseract,
                DisplayName = "Tesseract OCR (Miễn phí, phổ biến)"
            });
            OcrEngineModeOptions.Add(new OcrEngineModeOption
            {
                Value = OcrEngineMode.WindowsOcr,
                DisplayName = "Windows.Media.Ocr (Tích hợp Windows)"
            });
            OcrEngineModeOptions.Add(new OcrEngineModeOption
            {
                Value = OcrEngineMode.OpenCvMlNet,
                DisplayName = "OpenCV + ML.NET/ONNX (Cần GPU mạnh)"
            });
        }

        private void InitializeTesseractOptions()
        {
            TesseractPageSegModeOptions.Clear();
            TesseractPageSegModeOptions.Add(new TesseractPageSegModeOption { Value = TesseractPageSegMode.Auto, DisplayName = "Auto (Tự động)" });
            TesseractPageSegModeOptions.Add(new TesseractPageSegModeOption { Value = TesseractPageSegMode.AutoOsd, DisplayName = "Auto + OSD" });
            TesseractPageSegModeOptions.Add(new TesseractPageSegModeOption { Value = TesseractPageSegMode.SingleColumn, DisplayName = "Single Column" });
            TesseractPageSegModeOptions.Add(new TesseractPageSegModeOption { Value = TesseractPageSegMode.SingleBlock, DisplayName = "Single Block" });
            TesseractPageSegModeOptions.Add(new TesseractPageSegModeOption { Value = TesseractPageSegMode.SingleLine, DisplayName = "Single Line" });
            TesseractPageSegModeOptions.Add(new TesseractPageSegModeOption { Value = TesseractPageSegMode.SingleWord, DisplayName = "Single Word" });
            TesseractPageSegModeOptions.Add(new TesseractPageSegModeOption { Value = TesseractPageSegMode.SingleChar, DisplayName = "Single Character" });
            TesseractPageSegModeOptions.Add(new TesseractPageSegModeOption { Value = TesseractPageSegMode.SparseText, DisplayName = "Sparse Text" });

            TesseractEngineModeOptions.Clear();
            TesseractEngineModeOptions.Add(new TesseractEngineModeOption { Value = TesseractEngineMode.Default, DisplayName = "Default (Tự động)" });
            TesseractEngineModeOptions.Add(new TesseractEngineModeOption { Value = TesseractEngineMode.Lstm, DisplayName = "LSTM (Chính xác hơn)" });
        }

        private void InitializeImageSourceModeOptions()
        {
            ImageSourceModeOptions.Clear();
            ImageSourceModeOptions.Add(new ImageSourceModeOption
            {
                Value = ImageSourceMode.ScreenCapture,
                DisplayName = "Chụp màn hình trực tiếp"
            });
            ImageSourceModeOptions.Add(new ImageSourceModeOption
            {
                Value = ImageSourceMode.FromNode,
                DisplayName = "Lấy từ node khác"
            });
            ImageSourceModeOptions.Add(new ImageSourceModeOption
            {
                Value = ImageSourceMode.PathOrUrl,
                DisplayName = "Path / URL"
            });
            ImageSourceModeOptions.Add(new ImageSourceModeOption
            {
                Value = ImageSourceMode.Base64,
                DisplayName = "Base64 string"
            });
        }

        private void InitializeOcrLanguageOptions()
        {
            OcrLanguageOptions.Clear();
            OcrLanguageOptions.Add("en"); // English
            OcrLanguageOptions.Add("vi"); // Vietnamese
            OcrLanguageOptions.Add("ja"); // Japanese
            OcrLanguageOptions.Add("ko"); // Korean
            OcrLanguageOptions.Add("zh-Hans"); // Chinese Simplified
            OcrLanguageOptions.Add("zh-Hant"); // Chinese Traditional
            OcrLanguageOptions.Add("fr"); // French
            OcrLanguageOptions.Add("de"); // German
            OcrLanguageOptions.Add("es"); // Spanish
            OcrLanguageOptions.Add("it"); // Italian
            OcrLanguageOptions.Add("pt"); // Portuguese
            OcrLanguageOptions.Add("ru"); // Russian
            OcrLanguageOptions.Add("ar"); // Arabic
            OcrLanguageOptions.Add("th"); // Thai
            OcrLanguageOptions.Add("id"); // Indonesian
            OcrLanguageOptions.Add("ms"); // Malay
            OcrLanguageOptions.Add("nl"); // Dutch
            OcrLanguageOptions.Add("pl"); // Polish
            OcrLanguageOptions.Add("tr"); // Turkish
            OcrLanguageOptions.Add("sv"); // Swedish
            OcrLanguageOptions.Add("cs"); // Czech
            OcrLanguageOptions.Add("da"); // Danish
            OcrLanguageOptions.Add("fi"); // Finnish
            OcrLanguageOptions.Add("no"); // Norwegian
            OcrLanguageOptions.Add("el"); // Greek
            OcrLanguageOptions.Add("he"); // Hebrew
            OcrLanguageOptions.Add("hi"); // Hindi
        }

        // ── Load danh sách cửa sổ đang mở ───────────────────────────────────
        private void ExecuteLoadWindows()
        {
            string? prevProcess = SelectedTargetWindow?.ProcessName ?? _textScanNode.TargetProcessName;
            string? prevTitle = SelectedTargetWindow?.Title ?? _textScanNode.TargetWindowTitle;

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

        public void RefreshImageKeyOptions()
        {
            ImageKeyOptions.Clear();
            foreach (var o in GetOutputKeysForNode(ImageSourceNodeId))
                ImageKeyOptions.Add(o);
        }

        // ── Save ─────────────────────────────────────────────────────────────
        protected override void OnSaveTitle()
        {
            _textScanNode.OcrEngineMode = OcrEngineMode;
            _textScanNode.ImageSourceMode = ImageSourceMode;

            _textScanNode.TessdataPath = TessdataPath ?? string.Empty;
            _textScanNode.TesseractPageSegMode = TesseractPageSegMode;
            _textScanNode.TesseractEngineMode = TesseractEngineMode;

            _textScanNode.CoordSourceNodeId = string.IsNullOrWhiteSpace(CoordSourceNodeId) ? null : CoordSourceNodeId;
            _textScanNode.CoordSourceOutputKey = string.IsNullOrWhiteSpace(CoordSourceOutputKey) ? null : CoordSourceOutputKey;

            _textScanNode.ImageSourceNodeId = string.IsNullOrWhiteSpace(ImageSourceNodeId) ? null : ImageSourceNodeId;
            _textScanNode.ImageSourceOutputKey = string.IsNullOrWhiteSpace(ImageSourceOutputKey) ? null : ImageSourceOutputKey;

            _textScanNode.ImagePath = ImagePath ?? string.Empty;
            _textScanNode.Base64Image = Base64Image ?? string.Empty;

            _textScanNode.OcrLanguage = OcrLanguage ?? "eng";
            _textScanNode.AutoDetectLanguage = AutoDetectLanguage;

            // Lưu target app
            _textScanNode.TargetProcessName = SelectedTargetWindow?.ProcessName ?? string.Empty;
            _textScanNode.TargetWindowTitle = SelectedTargetWindow?.Title ?? string.Empty;

            _textScanNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }

    // ── Option classes ───────────────────────────────────────────────────────
    public class OcrEngineModeOption
    {
        public OcrEngineMode Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class TesseractPageSegModeOption
    {
        public TesseractPageSegMode Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class TesseractEngineModeOption
    {
        public TesseractEngineMode Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class ImageSourceModeOption
    {
        public ImageSourceMode Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
