using System;
using System.Linq;
using FlowMy.Models.Enums;

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
        private HotkeyTriggerModeEnum _triggerMode = HotkeyTriggerModeEnum.Send;

        // ── Toạ độ từ node khác ──────────────────────────────────────────────
        private string? _coordSourceNodeId;
        private string? _coordSourceOutputKey;

        // ── Click tại vị trí toạ độ ──────────────────────────────────────────
        private bool _clickOnPosition = true;
        private int _clickDurationMs = 1;

        // ── Toạ độ thủ công (fallback khi không có node nguồn) ───────────────
        private System.Windows.Point _manualPosition;
        private bool _hasManualPosition;

        // ── Chọn app để focus trước khi nhấn phím ────────────────────────────
        private string _targetProcessName = string.Empty;
        private string _targetWindowTitle = string.Empty;

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

        // ── Toạ độ từ node khác ──────────────────────────────────────────────

        /// <summary>Node Id cung cấp toạ độ để click trước khi nhấn hotkey.</summary>
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

        /// <summary>Có click vào vị trí toạ độ trước khi nhấn hotkey không (mặc định true).</summary>
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

        /// <summary>Toạ độ thủ công (fallback khi không có node nguồn).</summary>
        public System.Windows.Point ManualPosition
        {
            get => _manualPosition;
            set
            {
                if (_manualPosition == value) return;
                _manualPosition = value;
                HasManualPosition = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PositionText));
            }
        }

        public bool HasManualPosition
        {
            get => _hasManualPosition;
            set { if (_hasManualPosition != value) { _hasManualPosition = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionText)); } }
        }

        public string PositionText => _hasManualPosition
            ? $"X: {(int)_manualPosition.X}, Y: {(int)_manualPosition.Y}"
            : "Chưa chọn vị trí";

        // ── Chọn app để focus trước khi nhấn hotkey ──────────────────────────
        public string TargetProcessName
        {
            get => _targetProcessName;
            set { if (_targetProcessName != value) { _targetProcessName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Tiêu đề cửa sổ của app cần focus trước khi nhấn hotkey.</summary>
        public string TargetWindowTitle
        {
            get => _targetWindowTitle;
            set { if (_targetWindowTitle != value) { _targetWindowTitle = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Chế độ kích hoạt: Send (gửi phím) hoặc Listen (nghe phím từ user).
        /// </summary>
        public HotkeyTriggerModeEnum TriggerMode
        {
            get => _triggerMode;
            set { if (_triggerMode != value) { _triggerMode = value; OnPropertyChanged(); } }
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
