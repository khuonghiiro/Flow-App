namespace FlowMy.Models
{
    public sealed class MouseEventNode : WorkflowNode
    {
        private string _mouseButton = "Left"; // Left, Right, Middle, ScrollUp, ScrollDown
        private int _repeatCount = 1;
        private double _holdDuration = 0; // Giây
        private int _scrollSpeed = 1; // Chỉ dùng cho scroll

        // ── Toạ độ từ node khác ──────────────────────────────────────────────
        private string? _coordSourceNodeId;
        private string? _coordSourceOutputKey;

        // ── Click tại vị trí toạ độ ──────────────────────────────────────────
        private bool _clickOnPosition = true;
        private int _clickDurationMs = 1;

        // ── Chọn app để focus trước khi click ────────────────────────────────
        private string _targetProcessName = string.Empty;
        private string _targetWindowTitle = string.Empty;

        public MouseEventNode()
        {
            Type = NodeType.MouseEvent;
            Title = "Mouse Event";

            // Dynamic input: Repeat Count (giống pattern keyboard guide)
            // DisplayName = "Số lần" để hiển thị trong data panel chrome
            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "repeatCount",
                DisplayName = "Số lần",
                IsMultiple = false,
                ConvertType = WorkflowDataType.Number
            });

            // Note: holdDuration và scrollSpeed chỉ là properties, không phải dynamic inputs
            // Chúng được set trực tiếp trong node control, không cần data panel
        }

        /// <summary>
        /// Loại nút chuột: Left, Right, Middle, ScrollUp, ScrollDown
        /// </summary>
        public string MouseButton
        {
            get => _mouseButton;
            set
            {
                if (_mouseButton == value) return;
                _mouseButton = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Số lần nhấn/lăn
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
        /// Thời gian giữ chuột (giây) - chỉ áp dụng cho Left/Right/Middle
        /// </summary>
        public double HoldDuration
        {
            get => _holdDuration;
            set
            {
                if (_holdDuration == value) return;
                _holdDuration = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Tốc độ lăn chuột - chỉ áp dụng cho ScrollUp/ScrollDown
        /// </summary>
        public int ScrollSpeed
        {
            get => _scrollSpeed;
            set
            {
                if (_scrollSpeed == value) return;
                _scrollSpeed = value < 1 ? 1 : value;
                OnPropertyChanged();
            }
        }

        // ── Toạ độ từ node khác ──────────────────────────────────────────────

        /// <summary>Node Id cung cấp toạ độ để click/scroll.</summary>
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

        /// <summary>Có click vào vị trí toạ độ trước khi thực hiện hành động không (mặc định true).</summary>
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

        /// <summary>Tên process của app cần focus trước khi click.</summary>
        public string TargetProcessName
        {
            get => _targetProcessName;
            set { if (_targetProcessName != value) { _targetProcessName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Tiêu đề cửa sổ của app cần focus trước khi click.</summary>
        public string TargetWindowTitle
        {
            get => _targetWindowTitle;
            set { if (_targetWindowTitle != value) { _targetWindowTitle = value ?? string.Empty; OnPropertyChanged(); } }
        }
    }
}
