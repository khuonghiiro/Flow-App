using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Node để cắt chuỗi thành mảng string dựa trên regex pattern.
    /// Bỏ qua các item rỗng, null, hoặc chỉ có khoảng trắng.
    /// </summary>
    public sealed class StringSplitNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;
        private string _regexPattern = @"\r?\n"; // Mặc định cắt theo dòng mới
        private string _outputKey = "ListItems"; // Key output mặc định

        public StringSplitNode()
        {
            Type = NodeType.StringSplit;
            Title = "String Split";

            // Input: Chuỗi cần cắt
            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "inputString",
                DisplayName = "Input String",
                ConvertType = WorkflowDataType.String
            });

            // Output: Mảng string sau khi cắt
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "ListItems",
                DisplayName = "List Items",
                // ⚠️ Quan trọng: đây là mảng string, dùng ArrayString để downstream nodes nhận đúng kiểu
                ConvertType = WorkflowDataType.ArrayString,
                // Đồng bộ luôn OutputType để DataPanel, combobox và NodeChrome biết đây là ArrayString (không bị null)
                OutputType = WorkflowDataType.ArrayString,
                IsUserAdded = false
            });
        }

        /// <summary>
        /// Regex pattern để cắt chuỗi (mặc định: \r?\n để cắt theo dòng).
        /// </summary>
        public string RegexPattern
        {
            get => _regexPattern;
            set
            {
                if (_regexPattern != value)
                {
                    _regexPattern = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Key của output (mặc định: "ListItems").
        /// Nếu rỗng, sẽ dùng key mặc định.
        /// </summary>
        public string OutputKey
        {
            get => _outputKey;
            set
            {
                if (_outputKey != value)
                {
                    var newKey = string.IsNullOrWhiteSpace(value) ? "ListItems" : value.Trim();
                    _outputKey = newKey;

                    // Update DynamicOutputs key
                    if (DynamicOutputs.Count > 0)
                    {
                        DynamicOutputs[0].Key = newKey;
                        DynamicOutputs[0].DisplayName = newKey;
                    }

                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Chế độ hiển thị tiêu đề (mặc định Always).
        /// </summary>
        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set
            {
                if (_titleDisplayMode != value)
                {
                    _titleDisplayMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Chế độ màu sắc tiêu đề (mặc định NodeColor - theo màu node).
        /// </summary>
        public TitleColorMode TitleColorMode
        {
            get => _titleColorMode;
            set
            {
                if (_titleColorMode != value)
                {
                    _titleColorMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Key của màu tùy chọn cho tiêu đề (khi TitleColorMode = CustomColor).
        /// </summary>
        public string? TitleColorKey
        {
            get => _titleColorKey;
            set
            {
                if (_titleColorKey != value)
                {
                    _titleColorKey = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Runtime result: danh sách items sau khi split (không serialize).
        /// </summary>
        [JsonIgnore]
        public List<string>? SplitResult { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Helper để notify PropertyChanged khi Title thay đổi từ bên ngoài.
        /// ⚠️ CRITICAL: Phải gọi trong:
        /// - SaveTitle() trong ViewModel (sau khi set Title)
        /// - CreateDuplicateNodeInstance() trong NodeActions.cs (sau khi set Title)
        /// - RequestEditNodeTitle() trong NodeActions.cs (sau khi set Title)
        /// </summary>
        public void NotifyTitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}