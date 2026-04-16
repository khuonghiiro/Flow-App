using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models
{
    /// <summary>
    /// Chế độ tính thời gian chờ bổ sung (ngoài delay cố định theo DelayValue/DelayUnit).
    /// </summary>
    public enum DelayTimingMode
    {
        /// <summary>Chỉ dùng DelayValue + DelayUnit như hiện tại.</summary>
        None = 0,
        /// <summary>Chọn ngẫu nhiên một khoảng trong [RandomMinValue, RandomMaxValue] theo DelayUnit.</summary>
        Random = 1,
        /// <summary>Lấy số từ output key của node nguồn (theo DelayUnit).</summary>
        NodeKey = 2
    }

    /// <summary>
    /// Node chờ một khoảng thời gian trước khi tiếp tục workflow.
    /// - Lưu thực thi: DelayMilliseconds (ms)
    /// - Lưu hiển thị/UI: DelayValue + DelayUnit (để node hiển thị theo đơn vị người dùng chọn)
    /// </summary>
    public sealed class DelayNode : WorkflowNode, INotifyPropertyChanged
    {
        private int _delayMilliseconds = 1000;
        private double _delayValue = 1d;
        private DelayTimeUnit _delayUnit = DelayTimeUnit.Seconds;
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;
        private DelayTimingMode _timingMode = DelayTimingMode.None;
        private double _randomMinValue;
        private double _randomMaxValue = 1d;
        private string _delaySourceNodeId = string.Empty;
        private string _delaySourceOutputKey = string.Empty;

        public DelayNode()
        {
            Type = NodeType.Delay;
            Title = "Delay";

            // Defaults: 1 Giây
            _delayUnit = DelayTimeUnit.Seconds;
            _delayValue = 1d;
            _delayMilliseconds = 1000;
            _randomMinValue = 0d;
            _randomMaxValue = 1d;
        }

        /// <summary>
        /// Không có / ngẫu nhiên / lấy từ node key.
        /// </summary>
        public DelayTimingMode TimingMode
        {
            get => _timingMode;
            set
            {
                if (_timingMode == value) return;
                _timingMode = value;
                OnPropertyChanged();
                if (value == DelayTimingMode.None)
                    SyncMillisecondsFromUi();
            }
        }

        /// <summary>
        /// Cận dưới (cùng đơn vị <see cref="DelayUnit"/>) khi <see cref="TimingMode"/> = Random.
        /// </summary>
        public double RandomMinValue
        {
            get => _randomMinValue;
            set
            {
                if (Math.Abs(_randomMinValue - value) < 0.0000001d) return;
                if (value < 0) value = 0;
                _randomMinValue = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Cận trên (cùng đơn vị <see cref="DelayUnit"/>) khi <see cref="TimingMode"/> = Random.
        /// </summary>
        public double RandomMaxValue
        {
            get => _randomMaxValue;
            set
            {
                if (Math.Abs(_randomMaxValue - value) < 0.0000001d) return;
                if (value < 0) value = 0;
                _randomMaxValue = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Node nguồn lấy giá trị delay khi <see cref="TimingMode"/> = NodeKey.
        /// </summary>
        public string DelaySourceNodeId
        {
            get => _delaySourceNodeId;
            set
            {
                if (_delaySourceNodeId == value) return;
                _delaySourceNodeId = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Output key trên node nguồn khi <see cref="TimingMode"/> = NodeKey.
        /// </summary>
        public string DelaySourceOutputKey
        {
            get => _delaySourceOutputKey;
            set
            {
                if (_delaySourceOutputKey == value) return;
                _delaySourceOutputKey = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Chế độ hiển thị tiêu đề (Phase 2 - TitleDisplayMode).
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
        /// Chế độ màu tiêu đề (Phase 3 - TitleColorMode).
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
        /// Key màu tiêu đề khi TitleColorMode = CustomColor.
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

        /// <summary>
        /// Thời gian chờ dùng để thực thi (milliseconds).
        /// </summary>
        public int DelayMilliseconds
        {
            get => _delayMilliseconds;
            set
            {
                var clamped = value < 0 ? 0 : value;
                if (_delayMilliseconds == clamped) return;
                _delayMilliseconds = clamped;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Giá trị người dùng nhập (theo DelayUnit).
        /// Ví dụ: 2.5 + Seconds => 2500ms.
        /// </summary>
        public double DelayValue
        {
            get => _delayValue;
            set
            {
                if (value < 0) value = 0;
                if (Math.Abs(_delayValue - value) < 0.0000001d) return;
                _delayValue = value;
                OnPropertyChanged();

                SyncMillisecondsFromUi();
            }
        }

        /// <summary>
        /// Đơn vị thời gian người dùng chọn (mặc định: Giây).
        /// </summary>
        public DelayTimeUnit DelayUnit
        {
            get => _delayUnit;
            set
            {
                if (_delayUnit == value) return;
                _delayUnit = value;
                OnPropertyChanged();

                SyncMillisecondsFromUi();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SyncMillisecondsFromUi()
        {
            if (_timingMode != DelayTimingMode.None)
                return;

            var multiplier = DelayUnit switch
            {
                DelayTimeUnit.Milliseconds => 1d,
                DelayTimeUnit.Seconds => 1000d,
                DelayTimeUnit.Minutes => 60_000d,
                DelayTimeUnit.Hours => 3_600_000d,
                _ => 1000d
            };

            var ms = DelayValue * multiplier;
            if (ms >= int.MaxValue)
            {
                if (DelayMilliseconds != int.MaxValue)
                {
                    _delayMilliseconds = int.MaxValue;
                    OnPropertyChanged(nameof(DelayMilliseconds));
                }
                return;
            }

            var rounded = (int)Math.Round(ms);
            if (rounded < 0) rounded = 0;

            if (DelayMilliseconds != rounded)
            {
                _delayMilliseconds = rounded;
                OnPropertyChanged(nameof(DelayMilliseconds));
            }
        }

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum DelayTimeUnit
    {
        Milliseconds,
        Seconds,
        Minutes,
        Hours
    }
}

