using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Node để cắt chuỗi thành mảng string dựa trên regex pattern.
    /// Bỏ qua các item rỗng, null, hoặc chỉ có khoảng trắng.
    /// </summary>
    public sealed class StringSplitNode : WorkflowNode
    {
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
        /// Runtime result: danh sách items sau khi split (không serialize).
        /// </summary>
        [JsonIgnore]
        public List<string>? SplitResult { get; set; }
    }
}