namespace FlowMy.Models
{
    /// <summary>
    /// Chế độ hiển thị viền sáng: Fullscreen hoặc TargetApp (cửa sổ cụ thể).
    /// </summary>
    public enum BorderHighlightMode
    {
        Fullscreen,
        TargetApp
    }

    /// <summary>
    /// Loại hiệu ứng viền sáng.
    /// </summary>
    public enum BorderEffectType
    {
        None,
        Pulse,
        Glow,
        Rainbow
    }

    /// <summary>
    /// Đơn vị thời gian cho Duration.
    /// </summary>
    public enum DurationUnit
    {
        Milliseconds,
        Seconds,
        Minutes,
        Hours
    }

    /// <summary>
    /// Node hiển thị viền sáng màn hình (border highlight) với cấu hình màu, độ dày, hiệu ứng.
    /// Có thể chọn tắt các node BorderHighlight khác trước khi chạy.
    /// </summary>
    public sealed class BorderHighlightNode : WorkflowNode
    {
        private string _borderColorHex = "#00D2FF";
        private int _borderThickness = 2;
        private int _gradientSize = 15;
        private double _opacity = 0.85;
        private BorderEffectType _effectType = BorderEffectType.Pulse;
        private BorderHighlightMode _highlightMode = BorderHighlightMode.Fullscreen;
        private string _targetProcessName = "";
        private string _targetWindowTitle = "";
        private string _selectedWindowJson = "";
        private uint _targetProcessId = 0;
        private int _durationMs = 5000;
        private DurationUnit _durationUnit = DurationUnit.Seconds;
        private bool _waitForCompletion = true;
        private string _nodesToDisableJson = "[]";

        public BorderHighlightNode()
        {
            Type = NodeType.BorderHighlight;
            Title = "Border Highlight";
            ColorKey = "AzureBlue";

            Ports.Add(new NodePort
            {
                Id = System.Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });
            Ports.Add(new NodePort
            {
                Id = System.Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });
        }

        /// <summary>
        /// Màu viền sáng (hex format #RRGGBB).
        /// </summary>
        public string BorderColorHex
        {
            get => _borderColorHex;
            set
            {
                var s = value ?? "#00D2FF";
                if (_borderColorHex == s) return;
                _borderColorHex = s;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Độ dày viền (pixel).
        /// </summary>
        public int BorderThickness
        {
            get => _borderThickness;
            set
            {
                var v = value < 1 ? 1 : value > 10 ? 10 : value;
                if (_borderThickness == v) return;
                _borderThickness = v;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Kích thước gradient fade từ mép vào trong (pixel).
        /// </summary>
        public int GradientSize
        {
            get => _gradientSize;
            set
            {
                var v = value < 5 ? 5 : value > 50 ? 50 : value;
                if (_gradientSize == v) return;
                _gradientSize = v;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Độ trong suốt của viền (0.0 - 1.0).
        /// </summary>
        public double Opacity
        {
            get => _opacity;
            set
            {
                var v = value < 0.1 ? 0.1 : value > 1.0 ? 1.0 : value;
                if (Math.Abs(_opacity - v) < 0.01) return;
                _opacity = v;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Loại hiệu ứng viền.
        /// </summary>
        public BorderEffectType EffectType
        {
            get => _effectType;
            set
            {
                if (_effectType == value) return;
                _effectType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Chế độ hiển thị: Fullscreen hoặc TargetApp.
        /// </summary>
        public BorderHighlightMode HighlightMode
        {
            get => _highlightMode;
            set
            {
                if (_highlightMode == value) return;
                _highlightMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Tên tiến trình đích (chỉ dùng khi HighlightMode = TargetApp).
        /// </summary>
        public string TargetProcessName
        {
            get => _targetProcessName;
            set
            {
                var s = value ?? "";
                if (_targetProcessName == s) return;
                _targetProcessName = s;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Tiêu đề cửa sổ đích (chỉ dùng khi HighlightMode = TargetApp).
        /// </summary>
        public string TargetWindowTitle
        {
            get => _targetWindowTitle;
            set
            {
                var s = value ?? "";
                if (_targetWindowTitle == s) return;
                _targetWindowTitle = s;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// JSON của cửa sổ được chọn (WindowInfo) - dùng thay cho TargetProcessName/TargetWindowTitle.
        /// </summary>
        public string SelectedWindowJson
        {
            get => _selectedWindowJson;
            set
            {
                var s = value ?? "";
                if (_selectedWindowJson == s) return;
                _selectedWindowJson = s;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Process ID của ứng dụng đích (chỉ dùng khi HighlightMode = TargetApp).
        /// Dùng ProcessId thay vì Handle vì Handle không thể serialize.
        /// </summary>
        public uint TargetProcessId
        {
            get => _targetProcessId;
            set
            {
                if (_targetProcessId == value) return;
                _targetProcessId = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Thời gian hiển thị viền (ms). 0 = hiển thị mãi cho đến khi node tiếp theo chạy.
        /// </summary>
        public int DurationMs
        {
            get => _durationMs;
            set
            {
                var v = value < 0 ? 0 : value;
                if (_durationMs == v) return;
                _durationMs = v;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Đơn vị thời gian cho Duration.
        /// </summary>
        public DurationUnit DurationUnit
        {
            get => _durationUnit;
            set
            {
                if (_durationUnit == value) return;
                _durationUnit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Nếu true, workflow sẽ chờ hết DurationMs mới chạy node tiếp theo (sequential).
        /// Nếu false, workflow sẽ chạy node tiếp theo ngay lập tức (parallel), nhưng viền sẽ tự tắt sau DurationMs hoặc khi node BorderHighlight khác tắt nó.
        /// </summary>
        public bool WaitForCompletion
        {
            get => _waitForCompletion;
            set
            {
                if (_waitForCompletion == value) return;
                _waitForCompletion = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// JSON array chứa các node ID của BorderHighlight cần tắt trước khi chạy node này.
        /// </summary>
        public string NodesToDisableJson
        {
            get => _nodesToDisableJson;
            set
            {
                var s = value ?? "[]";
                if (_nodesToDisableJson == s) return;
                _nodesToDisableJson = s;
                OnPropertyChanged();
            }
        }
    }
}
