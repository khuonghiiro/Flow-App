using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Một input mapping: node nguồn + key → tên key dùng trong path (ValueConfirm; nếu trống dùng key mặc định).
    /// </summary>
    public sealed class FolderKeyValueInput : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private string? _valueConfirm;

        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveKey)); } }
        }

        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveKey)); } }
        }

        /// <summary>TextBox value: xác nhận key để tránh trùng key node khác. Trống = dùng key mặc định (SourceOutputKey).</summary>
        public string? ValueConfirm
        {
            get => _valueConfirm;
            set { if (_valueConfirm != value) { _valueConfirm = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveKey)); } }
        }

        /// <summary>Tên key dùng trong SubPathTemplate = ValueConfirm nếu có, không thì SourceOutputKey.</summary>
        public string EffectiveKey => !string.IsNullOrWhiteSpace(_valueConfirm)
            ? _valueConfirm!.Trim()
            : (string.IsNullOrWhiteSpace(_sourceOutputKey) ? "value" : _sourceOutputKey!.Trim());

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Node tạo folder: chọn thư mục gốc, path con (có thể dùng key từ inputs và {DateTime.*}), tạo folder và output folder + fullPath.
    /// </summary>
    public sealed class FolderNode : WorkflowNode
    {
        private string _rootFolderPath = string.Empty;
        private string _rootFolderPresetKey = string.Empty;
        private string _subPathTemplate = string.Empty;
        private readonly List<FolderKeyValueInput> _keyValueInputs = new();

        public FolderNode()
        {
            Type = NodeType.Folder;
            Title = "Folder";
            ColorKey = "GoldenYellow";

            Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"           // Port IN: dùng màu Info theo guideline
            });
            Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"   // Port OUT: dùng màu SunsetOrange theo guideline
            });

            _keyValueInputs.Add(new FolderKeyValueInput());
            RebuildDynamicOutputs();
        }

        /// <summary>Đường dẫn thư mục gốc (chọn bằng nút Chọn folder).</summary>
        public string RootFolderPath
        {
            get => _rootFolderPath;
            set { if (_rootFolderPath != value) { _rootFolderPath = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Preset thư mục gốc theo thư mục hệ thống Windows (Desktop/Downloads/Documents...).
        /// Rỗng = dùng RootFolderPath custom nhập tay.
        /// </summary>
        public string RootFolderPresetKey
        {
            get => _rootFolderPresetKey;
            set { if (_rootFolderPresetKey != value) { _rootFolderPresetKey = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Path con (có thể dùng {key1}, {key2}, {DateTime.YYYY}, ...). Tạo folder con trong RootFolderPath.</summary>
        public string SubPathTemplate
        {
            get => _subPathTemplate;
            set { if (_subPathTemplate != value) { _subPathTemplate = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Danh sách input: mỗi dòng = Node + Key + Value (xác nhận key). Key dùng trong SubPathTemplate = Value nếu có, không thì Key.</summary>
        public List<FolderKeyValueInput> KeyValueInputs
        {
            get => _keyValueInputs;
        }

        /// <summary>Output đã resolve khi chạy: folder (root), fullPath (root + path con đã tạo).</summary>
        public Dictionary<string, object?> ResolvedOutputs { get; set; } = new();

        /// <summary>Xây lại DynamicOutputs: folder, fullPath.</summary>
        public void RebuildDynamicOutputs()
        {
            DynamicOutputs.Clear();
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "folder",
                DisplayName = "Folder",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "fullPath",
                DisplayName = "Full path",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
        }
    }
}
