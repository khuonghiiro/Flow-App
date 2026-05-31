using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System.Collections.ObjectModel;
using System.IO;
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
        [ObservableProperty] private string _ocrLanguage = "eng";
        [ObservableProperty] private bool _autoDetectLanguage = true;
        [ObservableProperty] private ObservableCollection<object> _selectedLanguages = new();
        public ObservableCollection<string> OcrLanguageOptions { get; } = new();
        public ObservableCollection<TesseractLanguageOption> AvailableTesseractLanguages { get; } = new();

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

            // Sync selected languages
            SelectedLanguages.Clear();
            if (node.SelectedLanguages != null && node.SelectedLanguages.Count > 0)
            {
                foreach (var lang in node.SelectedLanguages)
                    SelectedLanguages.Add(lang);
            }
            else if (AutoDetectLanguage)
            {
                // Default languages if none selected
                SelectedLanguages.Add("eng");
                SelectedLanguages.Add("vie");
            }

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
                else if (e.PropertyName == nameof(TessdataPath))
                {
                    LoadAvailableTesseractLanguages();
                }
                else if (e.PropertyName == nameof(AutoDetectLanguage))
                {
                    if (AutoDetectLanguage && SelectedLanguages.Count == 0)
                    {
                        // Auto-select common languages when enabling auto-detect
                        SelectedLanguages.Add("eng");
                        SelectedLanguages.Add("vie");
                    }
                }
            };

            // Khởi tạo options
            InitializeOcrEngineModeOptions();
            InitializeTesseractOptions();
            InitializeImageSourceModeOptions();
            InitializeOcrLanguageOptions();
            LoadAvailableTesseractLanguages();
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
            OcrLanguageOptions.Add("eng"); // English
            OcrLanguageOptions.Add("vie"); // Vietnamese
            OcrLanguageOptions.Add("jpn"); // Japanese
            OcrLanguageOptions.Add("kor"); // Korean
            OcrLanguageOptions.Add("chi_sim"); // Chinese Simplified
            OcrLanguageOptions.Add("chi_tra"); // Chinese Traditional
            OcrLanguageOptions.Add("fra"); // French
            OcrLanguageOptions.Add("deu"); // German
            OcrLanguageOptions.Add("spa"); // Spanish
            OcrLanguageOptions.Add("ita"); // Italian
            OcrLanguageOptions.Add("por"); // Portuguese
            OcrLanguageOptions.Add("rus"); // Russian
            OcrLanguageOptions.Add("ara"); // Arabic
            OcrLanguageOptions.Add("tha"); // Thai
            OcrLanguageOptions.Add("ind"); // Indonesian
            OcrLanguageOptions.Add("msa"); // Malay
            OcrLanguageOptions.Add("nld"); // Dutch
            OcrLanguageOptions.Add("pol"); // Polish
            OcrLanguageOptions.Add("tur"); // Turkish
            OcrLanguageOptions.Add("swe"); // Swedish
            OcrLanguageOptions.Add("ces"); // Czech
            OcrLanguageOptions.Add("dan"); // Danish
            OcrLanguageOptions.Add("fin"); // Finnish
            OcrLanguageOptions.Add("nor"); // Norwegian
            OcrLanguageOptions.Add("ell"); // Greek
            OcrLanguageOptions.Add("heb"); // Hebrew
            OcrLanguageOptions.Add("hin"); // Hindi
        }

        private void LoadAvailableTesseractLanguages()
        {
            AvailableTesseractLanguages.Clear();

            string tessdataPath = string.IsNullOrWhiteSpace(TessdataPath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata")
                : TessdataPath;

            if (!Directory.Exists(tessdataPath))
                return;

            var trainedDataFiles = Directory.GetFiles(tessdataPath, "*.traineddata");
            foreach (var file in trainedDataFiles)
            {
                var langCode = Path.GetFileNameWithoutExtension(file);
                var displayName = GetLanguageDisplayName(langCode);
                AvailableTesseractLanguages.Add(new TesseractLanguageOption
                {
                    Code = langCode,
                    DisplayName = displayName
                });
            }
        }

        private string GetLanguageDisplayName(string code)
        {
            return code switch
            {
                "afr" => "Afrikaans",
                "amh" => "Amharic",
                "ara" => "Arabic (Tiếng Ả Rập)",
                "asm" => "Assamese",
                "aze" => "Azerbaijani",
                "aze_cyrl" => "Azerbaijani (Cyrillic)",
                "bel" => "Belarusian",
                "ben" => "Bengali",
                "bod" => "Tibetan",
                "bos" => "Bosnian",
                "bre" => "Breton",
                "bul" => "Bulgarian",
                "cat" => "Catalan",
                "ceb" => "Cebuano",
                "ces" => "Czech (Tiếng Séc)",
                "chi_sim" => "Chinese Simplified (Tiếng Trung giản thể)",
                "chi_sim_vert" => "Chinese Simplified (Vertical)",
                "chi_tra" => "Chinese Traditional (Tiếng Trung phồn thể)",
                "chi_tra_vert" => "Chinese Traditional (Vertical)",
                "chr" => "Cherokee",
                "cos" => "Corsican",
                "cym" => "Welsh",
                "dan" => "Danish (Tiếng Đan Mạch)",
                "dan_frak" => "Danish (Fraktur)",
                "deu" => "German (Tiếng Đức)",
                "deu_frak" => "German (Fraktur)",
                "deu_latf" => "German (Latin)",
                "div" => "Dhivehi",
                "dzo" => "Dzongkha",
                "ell" => "Greek (Tiếng Hy Lạp)",
                "eng" => "English (Tiếng Anh)",
                "enm" => "English (Middle)",
                "epo" => "Esperanto",
                "equ" => "Equation",
                "est" => "Estonian",
                "eus" => "Basque",
                "fao" => "Faroese",
                "fas" => "Persian (Tiếng Ba Tư)",
                "fil" => "Filipino",
                "fin" => "Finnish (Tiếng Phần Lan)",
                "fra" => "French (Tiếng Pháp)",
                "frm" => "French (Middle)",
                "fry" => "Western Frisian",
                "gla" => "Scottish Gaelic",
                "gle" => "Irish",
                "glg" => "Galician",
                "grc" => "Greek (Ancient)",
                "guj" => "Gujarati",
                "hat" => "Haitian",
                "heb" => "Hebrew (Tiêng Do Thái)",
                "hin" => "Hindi (Tiếng Hindi)",
                "hrv" => "Croatian",
                "hun" => "Hungarian (Tiếng Hungary)",
                "hye" => "Armenian",
                "iku" => "Inuktitut",
                "ind" => "Indonesian (Tiếng Indonesia)",
                "isl" => "Icelandic",
                "ita" => "Italian (Tiếng Ý)",
                "ita_old" => "Italian (Old)",
                "jav" => "Javanese",
                "jpn" => "Japanese (Tiếng Nhật)",
                "jpn_vert" => "Japanese (Vertical)",
                "kan" => "Kannada",
                "kat" => "Georgian",
                "kat_old" => "Georgian (Old)",
                "kaz" => "Kazakh",
                "khm" => "Khmer",
                "kir" => "Kyrgyz",
                "kmr" => "Kurdish",
                "kor" => "Korean (Tiếng Hàn)",
                "kor_vert" => "Korean (Vertical)",
                "lao" => "Lao",
                "lat" => "Latin",
                "lav" => "Latvian",
                "lit" => "Lithuanian",
                "ltz" => "Luxembourgish",
                "mal" => "Malayalam",
                "mar" => "Marathi",
                "mkd" => "Macedonian",
                "mlt" => "Maltese",
                "mon" => "Mongolian",
                "mri" => "Maori",
                "msa" => "Malay (Tiếng Mã Lai)",
                "mya" => "Burmese",
                "nep" => "Nepali",
                "nld" => "Dutch (Tiếng Hà Lan)",
                "nor" => "Norwegian (Tiếng Na Uy)",
                "oci" => "Occitan",
                "ori" => "Oriya",
                "osd" => "Orientation and Script Detection",
                "pan" => "Punjabi",
                "pol" => "Polish (Tiếng Ba Lan)",
                "por" => "Portuguese (Tiếng Bồ Đào Nha)",
                "pus" => "Pashto",
                "que" => "Quechua",
                "ron" => "Romanian",
                "rus" => "Russian (Tiếng Nga)",
                "san" => "Sanskrit",
                "sin" => "Sinhala",
                "slk" => "Slovak",
                "slk_frak" => "Slovak (Fraktur)",
                "slv" => "Slovenian",
                "snd" => "Sindhi",
                "spa" => "Spanish (Tiếng Tây Ban Nha)",
                "spa_old" => "Spanish (Old)",
                "sqi" => "Albanian",
                "srp" => "Serbian",
                "srp_latn" => "Serbian (Latin)",
                "sun" => "Sundanese",
                "swa" => "Swahili",
                "swe" => "Swedish (Tiếng Thụy Điển)",
                "syr" => "Syriac",
                "tam" => "Tamil",
                "tat" => "Tatar",
                "tel" => "Telugu",
                "tgk" => "Tajik",
                "tgl" => "Tagalog",
                "tha" => "Thai (Tiếng Thái)",
                "tir" => "Tigrinya",
                "ton" => "Tongan",
                "tur" => "Turkish (Tiếng Thổ Nhĩ Kỳ)",
                "uig" => "Uyghur",
                "ukr" => "Ukrainian",
                "urd" => "Urdu",
                "uzb" => "Uzbek",
                "uzb_cyrl" => "Uzbek (Cyrillic)",
                "vie" => "Vietnamese (Tiếng Việt)",
                "yid" => "Yiddish",
                "yor" => "Yoruba",
                _ => code
            };
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
            _textScanNode.SelectedLanguages = SelectedLanguages.Cast<string>().ToList();

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

    public class TesseractLanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
