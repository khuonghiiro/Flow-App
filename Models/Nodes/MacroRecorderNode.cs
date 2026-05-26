namespace FlowMy.Models
{
    /// <summary>
    /// Chế độ phát lại macro: chạy 1 lần hoặc lặp lại N lần.
    /// </summary>
    public enum MacroPlaybackMode
    {
        Once,
        Repeat
    }

    /// <summary>
    /// Chế độ hiển thị overlay khi phát lại:
    /// Silent = không hiển thị gì,
    /// Live   = hiển thị marker khi thao tác xảy ra rồi mờ dần,
    /// Ghost  = vẽ sẵn toàn bộ luồng, thao tác đến đâu marker biến mất đến đó.
    /// </summary>
    public enum VisualPlaybackMode
    {
        Silent,
        Live,
        Ghost
    }

    /// <summary>
    /// Chế độ thực thi: Tự do (gửi sự kiện toàn cầu) hoặc TargetApp (gửi sự kiện ngầm tới client area của process/cửa sổ cụ thể).
    /// </summary>
    public enum MacroExecutionMode
    {
        Free,
        TargetApp
    }

    /// <summary>
    /// Node ghi lại và phát lại thao tác chuột/bàn phím.
    /// </summary>
    public sealed class MacroRecorderNode : WorkflowNode
    {
        private string _outputKey = "macroData";
        private string _macroDataJson = "";
        private MacroPlaybackMode _playbackMode = MacroPlaybackMode.Once;
        private MacroExecutionMode _executionMode = MacroExecutionMode.Free;
        private string _targetProcessName = "";
        private string _targetWindowTitle = "";
        private int _repeatIntervalMs = 500;
        private int _repeatCount = 1;
        private VisualPlaybackMode _visualPlaybackMode = VisualPlaybackMode.Live;
        private bool _showMouseTrail = false;
        private int _countdownSeconds = 3;
        private bool _stayOnTargetAfterExecution = true;

        public MacroRecorderNode()
        {
            Type = NodeType.MacroRecorder;
            Title = "Macro Recorder";
            ColorKey = "MangoTango";

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

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "macroData",
                DisplayName = "Macro Data",
                OutputType = WorkflowDataType.String
            });
        }

        /// <summary>
        /// Tên key output trong scoped store.
        /// </summary>
        public string OutputKey
        {
            get => _outputKey;
            set
            {
                var s = value ?? "macroData";
                if (_outputKey == s) return;
                _outputKey = s;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// JSON array các action đã ghi (serialize từ List&lt;MacroAction&gt;).
        /// </summary>
        public string MacroDataJson
        {
            get => _macroDataJson;
            set
            {
                var s = value ?? "";
                if (_macroDataJson == s) return;
                _macroDataJson = s;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Chế độ phát lại: Once (1 lần) hoặc Repeat (lặp lại N lần).
        /// </summary>
        public MacroPlaybackMode PlaybackMode
        {
            get => _playbackMode;
            set
            {
                if (_playbackMode == value) return;
                _playbackMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Chế độ thực thi: Free hoặc TargetApp.
        /// </summary>
        public MacroExecutionMode ExecutionMode
        {
            get => _executionMode;
            set
            {
                if (_executionMode == value) return;
                _executionMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Tên tiến trình (chỉ dùng nếu ExecutionMode = TargetApp).
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
        /// Tiêu đề cửa sổ (chỉ dùng nếu ExecutionMode = TargetApp).
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
        /// Thời gian giữa các chu kỳ lặp (ms). Không âm.
        /// </summary>
        public int RepeatIntervalMs
        {
            get => _repeatIntervalMs;
            set
            {
                var v = value < 0 ? 0 : value;
                if (_repeatIntervalMs == v) return;
                _repeatIntervalMs = v;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Số lần lặp khi chế độ Repeat. Tối thiểu 1.
        /// </summary>
        public int RepeatCount
        {
            get => _repeatCount;
            set
            {
                var v = value < 1 ? 1 : value;
                if (_repeatCount == v) return;
                _repeatCount = v;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Chế độ hiển thị overlay khi phát lại.
        /// </summary>
        public VisualPlaybackMode VisualPlaybackMode
        {
            get => _visualPlaybackMode;
            set
            {
                if (_visualPlaybackMode == value) return;
                _visualPlaybackMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Số giây đếm ngược trước khi phát lại (để user kịp chuyển sang app target).
        /// 0 = không đếm ngược.
        /// </summary>
        public int CountdownSeconds
        {
            get => _countdownSeconds;
            set
            {
                var v = value < 0 ? 0 : value > 10 ? 10 : value;
                if (_countdownSeconds == v) return;
                _countdownSeconds = v;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Hiển thị nét đứt di chuyển chuột khi ghi lại thao tác.
        /// </summary>
        public bool ShowMouseTrail
        {
            get => _showMouseTrail;
            set
            {
                if (_showMouseTrail == value) return;
                _showMouseTrail = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Khi true: sau khi chạy xong macro, giữ nguyên focus ở app đích (không restore FlowMy).
        /// Khi false: restore FlowMy về foreground như cũ.
        /// Mặc định: true.
        /// </summary>
        public bool StayOnTargetAfterExecution
        {
            get => _stayOnTargetAfterExecution;
            set
            {
                if (_stayOnTargetAfterExecution == value) return;
                _stayOnTargetAfterExecution = value;
                OnPropertyChanged();
            }
        }
    }
}
