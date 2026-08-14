using System.Windows;

namespace FlowMy.Models
{
    /// <summary>
    /// Hành động chuột sẽ thực hiện tại vị trí đã chọn.
    /// </summary>
    public enum ScreenPositionMouseAction
    {
        None,
        LeftClick,
        RightClick,
        ScrollUp,
        ScrollDown
    }

    /// <summary>
    /// Model cho Screen Position Picker Node — chọn vị trí màn hình và tuỳ chọn thực hiện thao tác chuột.
    /// </summary>
    public class ScreenPositionPickerNode : WorkflowNode
    {
        private Point _selectedPosition;
        private bool _hasPosition;

        // ── Combobox input toạ độ từ node khác ──
        private string? _coordSourceNodeId;
        private string? _coordSourceOutputKey;

        // ── Hành động chuột ──
        private ScreenPositionMouseAction _mouseAction = ScreenPositionMouseAction.None;

        // ── Left / Right click ──
        private int _clickCount = 1;
        private int _holdDurationMs = 1;

        // ── Scroll ──
        private int _scrollCount = 1;
        private int _scrollIntervalMs = 1000;

        // ── Chọn app để focus trước khi chọn vị trí ──
        private string _targetProcessName = string.Empty;
        private string _targetWindowTitle = string.Empty;

        // ── Background Mode ─────────────────────────────────────────────────────
        private bool _useBackgroundMode = false;
        private FlowMy.Helpers.BackgroundInputHelper.InputMode _backgroundInputMode = FlowMy.Helpers.BackgroundInputHelper.InputMode.Auto;

        // ── Trở về màn hình ban đầu ─────────────────────────────────────────────
        private bool _returnToOriginalScreen = false;

        // ─────────────────────────────────────────────────────────────────────
        // Vị trí đã chọn thủ công
        // ─────────────────────────────────────────────────────────────────────

        public Point SelectedPosition
        {
            get => _selectedPosition;
            set
            {
                if (_selectedPosition == value) return;
                _selectedPosition = value;
                HasPosition = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PositionText));
            }
        }

        public bool HasPosition
        {
            get => _hasPosition;
            set
            {
                if (_hasPosition == value) return;
                _hasPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PositionText));
            }
        }

        public string PositionText => _hasPosition
            ? $"X: {(int)_selectedPosition.X}, Y: {(int)_selectedPosition.Y}"
            : "Chưa chọn vị trí";

        // ─────────────────────────────────────────────────────────────────────
        // Nguồn toạ độ từ node khác (ưu tiên hơn SelectedPosition khi chạy)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Node Id cung cấp toạ độ (x, y hoặc position).</summary>
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

        // ─────────────────────────────────────────────────────────────────────
        // Hành động chuột
        // ─────────────────────────────────────────────────────────────────────

        public ScreenPositionMouseAction MouseAction
        {
            get => _mouseAction;
            set { if (_mouseAction != value) { _mouseAction = value; OnPropertyChanged(); } }
        }

        /// <summary>Số lần nhấn (áp dụng cho LeftClick / RightClick).</summary>
        public int ClickCount
        {
            get => _clickCount;
            set { if (_clickCount != value) { _clickCount = value; OnPropertyChanged(); } }
        }

        /// <summary>Thời gian giữ chuột trước khi nhả (ms).</summary>
        public int HoldDurationMs
        {
            get => _holdDurationMs;
            set { if (_holdDurationMs != value) { _holdDurationMs = value; OnPropertyChanged(); } }
        }

        /// <summary>Số lần lăn scroll.</summary>
        public int ScrollCount
        {
            get => _scrollCount;
            set { if (_scrollCount != value) { _scrollCount = value; OnPropertyChanged(); } }
        }

        /// <summary>Khoảng cách giữa mỗi lần lăn (ms).</summary>
        public int ScrollIntervalMs
        {
            get => _scrollIntervalMs;
            set { if (_scrollIntervalMs != value) { _scrollIntervalMs = value; OnPropertyChanged(); } }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Chọn app để focus trước khi chọn vị trí
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Tên process của app cần focus trước khi chọn vị trí (ví dụ: "chrome").</summary>
        public string TargetProcessName
        {
            get => _targetProcessName;
            set { if (_targetProcessName != value) { _targetProcessName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Tiêu đề cửa sổ của app cần focus trước khi chọn vị trí.</summary>
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

        // ─────────────────────────────────────────────────────────────────────

        public ScreenPositionPickerNode()
        {
            Type = NodeType.ScreenPosition;
            Title = "Screen Position";
            ColorKey = "Amethyst";
            _hasPosition = false;
            _selectedPosition = new Point(0, 0);
        }

        public void ClearPosition()
        {
            HasPosition = false;
            _selectedPosition = new Point(0, 0);
            OnPropertyChanged(nameof(SelectedPosition));
        }
    }
}
