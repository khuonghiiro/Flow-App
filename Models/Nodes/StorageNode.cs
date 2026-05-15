using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// StorageNode - node lưu trữ dữ liệu toàn cục.
    /// - Các node khác có thể gán giá trị vào outputs của node này (thông qua executor hoặc gán tay).
    /// - Bất kỳ node nào có combobox Node/Key đều có thể đọc lại giá trị từ node này mà không cần kết nối flow.
    /// - Khi save workflow, giá trị hiện tại của outputs được serialize vào JSON,
    ///   khi load workflow thì khôi phục lại, tránh mất dữ liệu đã gán.
    /// </summary>
    public sealed class StorageNode : WorkflowNode
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private bool _isInputMode = true; // true = port IN visible, false = port OUT visible

        /// <summary>
        /// Runtime cache cho các giá trị đã được gán vào StorageNode.
        /// Key = OutputKey, Value = giá trị cuối cùng.
        /// Được serialize vào workflow JSON để lần load sau vẫn giữ nguyên.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, string?> StoredOutputs { get; } = new();

        public StorageNode()
        {
            Type = NodeType.Storage;
            Title = "Storage";
        }

        /// <summary>
        /// Id của node nguồn mà Storage sẽ mirror outputs.
        /// Nếu null/empty: dùng dữ liệu mặc định trong StoredOutputs.
        /// </summary>
        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set
            {
                if (_sourceNodeId == value) return;
                _sourceNodeId = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Output key nguồn.
        /// Nếu null/empty: copy TẤT CẢ outputs của node nguồn.
        /// </summary>
        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set
            {
                if (_sourceOutputKey == value) return;
                _sourceOutputKey = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Reference tới UI TextBlock của tiêu đề, dùng cho NodeControl.
        /// </summary>
        [JsonIgnore]
        public TextBlock? TitleTextBlockUI { get; set; }

        /// <summary>
        /// Chế độ hoạt động của StorageNode:
        /// - true (checked): Hiện port IN, ẩn port OUT. Dùng combobox để chọn node nguồn mirror outputs.
        /// - false (unchecked): Ẩn port IN, hiện port OUT. Tự động set outputs khi có connection đến port IN, port OUT chỉ lấy từ storage nodes khác.
        /// </summary>
        public bool IsInputMode
        {
            get => _isInputMode;
            set
            {
                if (_isInputMode == value) return;
                _isInputMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gán hoặc cập nhật giá trị lưu trữ cho 1 output key.
        /// Đồng thời sync với DynamicOutputs.UserValueOverride để DataPanel có thể hiển thị.
        /// </summary>
        public void SetStoredOutput(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            key = key.Trim();

            StoredOutputs[key] = value;

            if (DynamicOutputs != null)
            {
                var output = DynamicOutputs.FirstOrDefault(o =>
                    string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                if (output != null)
                {
                    output.UserValueOverride = value;
                }
            }
        }

        /// <summary>
        /// Lấy giá trị hiện tại theo key (nếu không có trả null).
        /// </summary>
        public string? GetStoredOutput(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            key = key.Trim();
            return StoredOutputs.TryGetValue(key, out var value) ? value : null;
        }

    }
}

