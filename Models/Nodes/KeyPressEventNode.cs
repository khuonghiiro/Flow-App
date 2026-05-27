using System;
using System.Linq;

namespace FlowMy.Models
{
    /// <summary>
    /// Chế độ hiển thị tiêu đề của node.
    /// </summary>
    public enum TitleDisplayMode
    {
        /// <summary>
        /// Ẩn tiêu đề (không hiện dù hover)
        /// </summary>
        Hidden = 0,
        /// <summary>
        /// Hiện tiêu đề khi hover
        /// </summary>
        Hover = 1,
        /// <summary>
        /// Luôn hiện tiêu đề
        /// </summary>
        Always = 2
    }

    /// <summary>
    /// Chế độ màu sắc tiêu đề của node.
    /// </summary>
    public enum TitleColorMode
    {
        /// <summary>
        /// Màu theo node (NodeBrush)
        /// </summary>
        NodeColor = 0,
        /// <summary>
        /// Màu tùy chọn từ TitleColorKey
        /// </summary>
        CustomColor = 1
    }

    /// <summary>
    /// Option wrapper cho TitleColorMode combobox.
    /// </summary>
    public sealed class TitleColorOption
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public TitleColorOption() { }

        public TitleColorOption(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Node sự kiện nhấn phím: chờ một phím (global) được nhấn rồi mới đi tiếp.
    /// Key được lưu ở base property <see cref="WorkflowNode.Key"/> để hỗ trợ duplicate/persistence sẵn có.
    /// </summary>
    public sealed class KeyPressEventNode : WorkflowNode
    {
        private int _repeatCount = 1;
        private int _pressDelayMs = 100;

        // ── Toạ độ từ node khác ──────────────────────────────────────────────
        private string? _coordSourceNodeId;
        private string? _coordSourceOutputKey;

        // ── Click tại vị trí toạ độ ──────────────────────────────────────────
        private bool _clickOnPosition = true;
        private int _clickDurationMs = 1;

        // ── Chọn app để focus trước khi nhấn phím ────────────────────────────
        private string _targetProcessName = string.Empty;
        private string _targetWindowTitle = string.Empty;

        public KeyPressEventNode()
        {
            Type = NodeType.KeyPressEvent;
            Title = "Key Press";
            
            // Add dynamic input for repeat count
            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "repeatCount",
                DisplayName = "Repeat Count",
                IsMultiple = false,
                ConvertType = WorkflowDataType.Number
            });
        }

        /// <summary>
        /// Phím cần chờ (string name). Wrapper quanh <see cref="WorkflowNode.Key"/> để phát PropertyChanged.
        /// </summary>
        public string TriggerKey
        {
            get => Key ?? string.Empty;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(Key ?? string.Empty, v, StringComparison.Ordinal)) return;
                Key = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Key));
                // Cập nhật DynamicOutputs.Key khi Key thay đổi
                UpdateDynamicOutputsKey();
            }
        }

        /// <summary>
        /// Số lần nhấn phím (mặc định 1). Có thể lấy từ dynamic input hoặc set trực tiếp.
        /// </summary>
        public int RepeatCount
        {
            get => _repeatCount;
            set
            {
                if (_repeatCount == value) return;
                _repeatCount = value < 1 ? 1 : value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Thời gian delay giữa các lần nhấn phím (mili giây, mặc định 100ms).
        /// </summary>
        public int PressDelayMs
        {
            get => _pressDelayMs;
            set
            {
                if (_pressDelayMs == value) return;
                _pressDelayMs = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        // ── Toạ độ từ node khác ──────────────────────────────────────────────

        /// <summary>Node Id cung cấp toạ độ để click trước khi nhấn phím.</summary>
        public string? CoordSourceNodeId
        {
            get => _coordSourceNodeId;
            set { if (_coordSourceNodeId != value) { _coordSourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>Output key của node nguồn (x / y / position).</summary>
        public string? CoordSourceOutputKey
        {
            get => _coordSourceOutputKey;
            set { if (_coordSourceOutputKey != value) { _coordSourceOutputKey = value; OnPropertyChanged(); } }
        }

        // ── Click tại vị trí toạ độ ──────────────────────────────────────────

        /// <summary>Có click vào vị trí toạ độ trước khi nhấn phím không (mặc định true).</summary>
        public bool ClickOnPosition
        {
            get => _clickOnPosition;
            set { if (_clickOnPosition != value) { _clickOnPosition = value; OnPropertyChanged(); } }
        }

        /// <summary>Thời gian giữ chuột khi click (ms, mặc định 1ms).</summary>
        public int ClickDurationMs
        {
            get => _clickDurationMs;
            set { if (_clickDurationMs != value) { _clickDurationMs = value < 0 ? 0 : value; OnPropertyChanged(); } }
        }

        // ── Chọn app để focus ────────────────────────────────────────────────

        /// <summary>Tên process của app cần focus trước khi nhấn phím.</summary>
        public string TargetProcessName
        {
            get => _targetProcessName;
            set { if (_targetProcessName != value) { _targetProcessName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Tiêu đề cửa sổ của app cần focus trước khi nhấn phím.</summary>
        public string TargetWindowTitle
        {
            get => _targetWindowTitle;
            set { if (_targetWindowTitle != value) { _targetWindowTitle = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Cập nhật Key của DynamicOutputs khi Key thay đổi
        /// </summary>
        private void UpdateDynamicOutputsKey()
        {
            if (DynamicOutputs == null || DynamicOutputs.Count == 0) return;

            // KeyPressEventNode chỉ có 1 output duy nhất, luôn cập nhật output đầu tiên
            var output = DynamicOutputs.FirstOrDefault();
            
            if (output != null)
            {
                // Nếu Key rỗng hoặc null, đặt về "key" để backward compatible
                // Nếu Key có giá trị, cập nhật output key
                if (string.IsNullOrWhiteSpace(Key))
                {
                    output.Key = "key";
                }
                else
                {
                    output.Key = Key;
                }
            }
        }
    }
}

