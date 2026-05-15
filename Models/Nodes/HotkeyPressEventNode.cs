using System;
using System.Linq;

namespace FlowMy.Models
{
    /// <summary>
    /// Node sự kiện nhấn tổ hợp phím (hotkey): chờ đến khi nhấn đúng tổ hợp (global) rồi mới đi tiếp.
    /// Hotkey được lưu ở base property <see cref="WorkflowNode.Key"/> để tận dụng persistence/duplicate sẵn có.
    /// </summary>
    public sealed class HotkeyPressEventNode : WorkflowNode
    {
        private int _repeatCount = 1;
        private int _pressDelayMs = 100;

        public HotkeyPressEventNode()
        {
            Type = NodeType.HotkeyPressEvent;
            Title = "Hotkey Press";
            
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
        /// Tổ hợp phím cần chờ (vd: Ctrl+Alt+K). Wrapper quanh <see cref="WorkflowNode.Key"/> để phát PropertyChanged.
        /// </summary>
        public string TriggerHotkey
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
        /// Số lần nhấn tổ hợp phím (mặc định 1). Có thể lấy từ dynamic input hoặc set trực tiếp.
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
        /// Cập nhật Key của DynamicOutputs khi Key thay đổi
        /// </summary>
        private void UpdateDynamicOutputsKey()
        {
            if (DynamicOutputs == null || DynamicOutputs.Count == 0) return;

            // HotkeyPressEventNode chỉ có 1 output duy nhất, luôn cập nhật output đầu tiên
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
