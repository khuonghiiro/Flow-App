using FlowMy.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Liệt kê đường dẫn file trong một thư mục (tuỳ chọn đệ quy), lọc theo đuôi; có thể đọc nội dung (text/base64) theo cấu hình.
    /// Output <c>paths</c> là JSON mảng chuỗi.
    /// </summary>
    public sealed class FolderFilePathsNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;

        private string _folderPath = string.Empty;
        private string? _folderSourceNodeId;
        private string? _folderSourceOutputKey;
        private bool _refreshFolderSourceNodeBeforeUse;

        private bool _includeSubfolders;
        private string _extensionFilterText = string.Empty;
        private readonly List<string> _extensionTags = new();
        private bool _readFileContents;
        private string _readContentExtensionsText = ".txt";

        public FolderFilePathsNode()
        {
            Type = NodeType.FolderFilePaths;
            Title = "File trong thư mục";
            ColorKey = "ForestPine";

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
        public object ResolvedOutputsSyncRoot { get; } = new();

        public void RebuildDynamicOutputs()
        {
            DynamicOutputs.Clear();
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "paths",
                DisplayName = "paths (JSON array)",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "count",
                DisplayName = "Số phần tử",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
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
        }

        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set { if (_titleDisplayMode != value) { _titleDisplayMode = value; OnPropertyChanged(); } }
        }

        public TitleColorMode TitleColorMode
        {
            get => _titleColorMode;
            set { if (_titleColorMode != value) { _titleColorMode = value; OnPropertyChanged(); } }
        }

        public string? TitleColorKey
        {
            get => _titleColorKey;
            set { if (_titleColorKey != value) { _titleColorKey = value; OnPropertyChanged(); } }
        }

        public string FolderPath
        {
            get => _folderPath;
            set { if (_folderPath != value) { _folderPath = value ?? string.Empty; OnPropertyChanged(); } }
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

        /// <summary>Khi bật: mỗi lần workflow chạy node này, chạy lại logic node nguồn (<see cref="FolderSourceNodeId"/>) trước khi đọc đường dẫn thư mục.</summary>
        public bool RefreshFolderSourceNodeBeforeUse
        {
            get => _refreshFolderSourceNodeBeforeUse;
            set { if (_refreshFolderSourceNodeBeforeUse != value) { _refreshFolderSourceNodeBeforeUse = value; OnPropertyChanged(); } }
        }

        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set { if (_includeSubfolders != value) { _includeSubfolders = value; OnPropertyChanged(); } }
        }

        /// <summary>Lọc đuôi dạng Notepad++: <c>.png,.jpg</c> hoặc <c>*.png;*.jpg</c> (cộng thêm <see cref="ExtensionTags"/>).</summary>
        public string ExtensionFilterText
        {
            get => _extensionFilterText;
            set { if (_extensionFilterText != value) { _extensionFilterText = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public List<string> ExtensionTags => _extensionTags;

        public bool ReadFileContents
        {
            get => _readFileContents;
            set { if (_readFileContents != value) { _readFileContents = value; OnPropertyChanged(); } }
        }

        /// <summary>Đuôi file được phép đọc nội dung khi bật <see cref="ReadFileContents"/> (mặc định <c>.txt</c>).</summary>
        public string ReadContentExtensionsText
        {
            get => _readContentExtensionsText;
            set { if (_readContentExtensionsText != value) { _readContentExtensionsText = value ?? ".txt"; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));

        public void NotifyRuntimeOutputsChanged() => OnPropertyChanged(nameof(ResolvedOutputs));
    }
}
