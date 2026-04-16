using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    public sealed class KeyPressEventNode : WorkflowNode, INotifyPropertyChanged
    {
        private int _repeatCount = 1;
        private int _pressDelayMs = 100;
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;

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

        /// <summary>
        /// Chế độ hiển thị tiêu đề của node (mặc định Hover).
        /// </summary>
        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set
            {
                if (_titleDisplayMode == value) return;
                _titleDisplayMode = value;
                OnPropertyChanged();
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
                if (_titleColorMode == value) return;
                _titleColorMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Key của màu tùy chọn cho tiêu đề (khi TitleColorMode = CustomColor).
        /// Ví dụ: "PrimaryBrush", "SuccessBrush", "DangerBrush", etc.
        /// </summary>
        public string? TitleColorKey
        {
            get => _titleColorKey;
            set
            {
                if (_titleColorKey == value) return;
                _titleColorKey = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

