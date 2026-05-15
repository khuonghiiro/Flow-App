using FlowMy.Models;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Tải file (ảnh, video, …) từ URL hoặc lệnh curl, lưu vào thư mục cấu hình.
    /// Hỗ trợ placeholder trong tên file: {time}, {date}, {datetime}, {index} và giới hạn độ dài tên.
    /// </summary>
    public sealed class FileDownloadNode : WorkflowNode
    {
        private string _fileNameTemplate = "download_{datetime}";
        private int _maxFileNameLength = 200;
        private bool _autoIncrementIfExists = true;
        private bool _removeDiacriticsFromFileName = false;

        private string _downloadUrl = string.Empty;
        private string? _urlSourceNodeId;
        private string? _urlSourceOutputKey;

        private string _curlCommand = string.Empty;
        private string? _curlSourceNodeId;
        private string? _curlSourceOutputKey;

        private string _downloadFolderPath = string.Empty;
        private string? _folderSourceNodeId;
        private string? _folderSourceOutputKey;

        private string? _fileNameSourceNodeId;
        private string? _fileNameSourceOutputKey;

        private bool _saveAdditionalOutputFiles;
        private string? _additionalOutputDefaultNameTemplate;
        private List<FileDownloadAdditionalOutputSaveEntry> _additionalOutputSaves = new();

        public FileDownloadNode()
        {
            Type = NodeType.FileDownload;
            Title = "Tải file";
            ColorKey = "CantaloupeOrange";

            Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });
            Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });

            RebuildDynamicOutputs();
        }

        public Dictionary<string, object?> ResolvedOutputs { get; set; } = new();

        /// <summary>
        /// Đồng bộ truy cập <see cref="ResolvedOutputs"/> khi node chạy song song.
        /// </summary>
        public object ResolvedOutputsSyncRoot { get; } = new();

        public void RebuildDynamicOutputs()
        {
            DynamicOutputs.Clear();
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "filePath",
                DisplayName = "Đường dẫn file",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "completed",
                DisplayName = "Đã tải xong",
                ConvertType = WorkflowDataType.Boolean,
                OutputType = WorkflowDataType.Boolean,
                IsUserAdded = false
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "errorMessage",
                DisplayName = "Lỗi",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "timingLog",
                DisplayName = "Timing log",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
        }

        public TextBlock? TitleTextBlockUI { get; set; }

        /// <summary>Mẫu tên file (có thể dùng {time}, {date}, {datetime}, {index}).</summary>
        public string FileNameTemplate
        {
            get => _fileNameTemplate;
            set { if (_fileNameTemplate != value) { _fileNameTemplate = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Giới hạn độ dài phần tên (không gồm phần mở rộng), ký tự đầu → cắt nếu vượt.</summary>
        public int MaxFileNameLength
        {
            get => _maxFileNameLength;
            set
            {
                var v = Math.Clamp(value, 1, 512);
                if (_maxFileNameLength != v) { _maxFileNameLength = v; OnPropertyChanged(); }
            }
        }

        /// <summary>Nếu true: khi trùng file, thử tên_1, tên_2… (và/hoặc thay {index}).</summary>
        public bool AutoIncrementIfExists
        {
            get => _autoIncrementIfExists;
            set { if (_autoIncrementIfExists != value) { _autoIncrementIfExists = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Nếu true: chuyển tên file từ có dấu sang không dấu trước khi lưu.
        /// </summary>
        public bool RemoveDiacriticsFromFileName
        {
            get => _removeDiacriticsFromFileName;
            set { if (_removeDiacriticsFromFileName != value) { _removeDiacriticsFromFileName = value; OnPropertyChanged(); } }
        }

        public string DownloadUrl
        {
            get => _downloadUrl;
            set { if (_downloadUrl != value) { _downloadUrl = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string? UrlSourceNodeId
        {
            get => _urlSourceNodeId;
            set { if (_urlSourceNodeId != value) { _urlSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? UrlSourceOutputKey
        {
            get => _urlSourceOutputKey;
            set { if (_urlSourceOutputKey != value) { _urlSourceOutputKey = value; OnPropertyChanged(); } }
        }

        public string CurlCommand
        {
            get => _curlCommand;
            set { if (_curlCommand != value) { _curlCommand = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string? CurlSourceNodeId
        {
            get => _curlSourceNodeId;
            set { if (_curlSourceNodeId != value) { _curlSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? CurlSourceOutputKey
        {
            get => _curlSourceOutputKey;
            set { if (_curlSourceOutputKey != value) { _curlSourceOutputKey = value; OnPropertyChanged(); } }
        }

        public string DownloadFolderPath
        {
            get => _downloadFolderPath;
            set { if (_downloadFolderPath != value) { _downloadFolderPath = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string? FolderSourceNodeId
        {
            get => _folderSourceNodeId;
            set { if (_folderSourceNodeId != value) { _folderSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? FolderSourceOutputKey
        {
            get => _folderSourceOutputKey;
            set { if (_folderSourceOutputKey != value) { _folderSourceOutputKey = value; OnPropertyChanged(); } }
        }

        public string? FileNameSourceNodeId
        {
            get => _fileNameSourceNodeId;
            set { if (_fileNameSourceNodeId != value) { _fileNameSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? FileNameSourceOutputKey
        {
            get => _fileNameSourceOutputKey;
            set { if (_fileNameSourceOutputKey != value) { _fileNameSourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>Bật lưu thêm nhiều file từ output node/key (nội dung text/ghi chuỗi).</summary>
        public bool SaveAdditionalOutputFiles
        {
            get => _saveAdditionalOutputFiles;
            set { if (_saveAdditionalOutputFiles != value) { _saveAdditionalOutputFiles = value; OnPropertyChanged(); } }
        }

        /// <summary>Mẫu tên mặc định cho các dòng khi NameTemplate dòng để trống. Trống = dùng <see cref="FileNameTemplate"/>.</summary>
        public string? AdditionalOutputDefaultNameTemplate
        {
            get => _additionalOutputDefaultNameTemplate;
            set { if (_additionalOutputDefaultNameTemplate != value) { _additionalOutputDefaultNameTemplate = value; OnPropertyChanged(); } }
        }

        public List<FileDownloadAdditionalOutputSaveEntry> AdditionalOutputSaves
        {
            get => _additionalOutputSaves;
            set
            {
                value ??= new List<FileDownloadAdditionalOutputSaveEntry>();
                _additionalOutputSaves = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Gọi sau khi executor cập nhật <see cref="ResolvedOutputs"/> để UI dialog refresh preview.</summary>
        public void NotifyRuntimeOutputsChanged() => OnPropertyChanged(nameof(ResolvedOutputs));
    }
}
