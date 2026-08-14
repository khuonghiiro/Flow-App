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
        private double _pressDelay = 100;
        private string _delayUnit = "ms"; // ms, s, m, h
        private bool _isAsync = false;

        private double _holdDuration = 0;
        private string _holdDurationUnit = "ms";
        private bool _isHoldAsync = false;

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

        // ── Background Mode ─────────────────────────────────────────────────────
        private bool _useBackgroundMode = false;
        private FlowMy.Helpers.BackgroundInputHelper.InputMode _backgroundInputMode = FlowMy.Helpers.BackgroundInputHelper.InputMode.Auto;

        // ── Trở về màn hình ban đầu ─────────────────────────────────────────────
        private bool _returnToOriginalScreen = false;

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
        /// Thời gian delay giữa các lần nhấn phím.
        /// </summary>
        public double PressDelay
        {
            get => _pressDelay;
            set
            {
                if (Math.Abs(_pressDelay - value) < 0.000001) return;
                _pressDelay = value < 0 ? 0 : value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PressDelayMs)); // Cho tương thích
            }
        }

        /// <summary>
        /// Đơn vị của delay (ms, s, m, h).
        /// </summary>
        public string DelayUnit
        {
            get => _delayUnit;
            set
            {
                var val = value ?? "ms";
                if (_delayUnit == val) return;
                _delayUnit = val;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Có chạy bất đồng bộ hay không (node tiếp theo chạy luôn mà không chờ nhấn xong).
        /// </summary>
        public bool IsAsync
        {
            get => _isAsync;
            set
            {
                if (_isAsync == value) return;
                _isAsync = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Thời gian giữ phím.
        /// </summary>
        public double HoldDuration
        {
            get => _holdDuration;
            set
            {
                if (Math.Abs(_holdDuration - value) < 0.000001) return;
                _holdDuration = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Đơn vị thời gian giữ phím (ms, s, m, h).
        /// </summary>
        public string HoldDurationUnit
        {
            get => _holdDurationUnit;
            set
            {
                var val = value ?? "ms";
                if (_holdDurationUnit == val) return;
                _holdDurationUnit = val;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Cờ cho biết việc giữ phím có chạy bất đồng bộ hay không.
        /// </summary>
        public bool IsHoldAsync
        {
            get => _isHoldAsync;
            set
            {
                if (_isHoldAsync == value) return;
                _isHoldAsync = value;
                OnPropertyChanged();
            }
        }

        [Obsolete("Dùng PressDelay và DelayUnit")]
        public int PressDelayMs
        {
            get => (int)_pressDelay;
            set => PressDelay = value;
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

        // ── Chọn app để focus trước khi nhấn phím ────────────────────────────
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

        // ── Background Mode ─────────────────────────────────────────────────────

        /// <summary>
        /// Sử dụng Background Mode - gửi input đến app mà không cần active (giống AnyDesk/TeamViewer).
        /// Khi true, app đích sẽ không được đưa lên foreground.
        /// </summary>
        public bool UseBackgroundMode
        {
            get => _useBackgroundMode;
            set { if (_useBackgroundMode != value) { _useBackgroundMode = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Chế độ gửi input khi UseBackgroundMode = true.
        /// DirectMessage: Nhanh nhất, ít tương thích với game/DirectX.
        /// SilentActivation: Cân bằng, tương thích cao.
        /// ForegroundActivation: Giống user thật nhưng gián đoạn.
        /// Auto: Tự chọn chế độ phù hợp.
        /// </summary>
        public FlowMy.Helpers.BackgroundInputHelper.InputMode BackgroundInputMode
        {
            get => _backgroundInputMode;
            set { if (_backgroundInputMode != value) { _backgroundInputMode = value; OnPropertyChanged(); } }
        }

        // ── Trở về màn hình ban đầu ─────────────────────────────────────────────

        /// <summary>
        /// Trở về màn hình mà user đang dùng sau khi thực hiện thao tác xong.
        /// </summary>
        public bool ReturnToOriginalScreen
        {
            get => _returnToOriginalScreen;
            set { if (_returnToOriginalScreen != value) { _returnToOriginalScreen = value; OnPropertyChanged(); } }
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
                // Dùng tên cố định "key" thay vì giá trị Key thực tế
                output.Key = "key";
            }
        }
    }
}

