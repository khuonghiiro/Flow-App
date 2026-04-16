using FlowMy.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Node lấy dữ liệu output từ bất kỳ node nào trong workflow.
    /// Hỗ trợ 3 chế độ: Timer (tự động theo chu kỳ), Realtime (khi node nguồn xong), Flow (theo workflow).
    /// Nếu nguồn là WebNode, có thể chờ trang load xong trước khi lấy giá trị.
    /// </summary>
    public sealed class DataFetcherNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;

        // ── Nguồn dữ liệu ──
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private bool _waitForWebNodeLoad = false;

        // ── Timer ──
        private bool _enableTimer = false;
        private int _timerIntervalValue = 5;
        private string _timerUnit = "s"; // "ms" | "s" | "m"
        private bool _enableDataReadyScan = false;
        private int _dataReadyScanIntervalValue = 1;
        private string _dataReadyScanUnit = "s"; // "ms" | "s" | "m"
        private List<string> _dataReadyScanKeys = new();

        // ── Realtime ──
        private bool _enableRealtime = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DataFetcherNode()
        {
            Type = NodeType.DataFetcher;
            Title = "Data Fetcher";

            Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });
            Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });

            // Không tạo output mặc định — executor sẽ tự tạo port
            // dựa trên keys thực tế của node nguồn khi chạy.
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));

        public void RaisePropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

        #region TitleDisplayMode / TitleColor

        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set { if (_titleDisplayMode != value) { _titleDisplayMode = value; OnPropertyChanged(); } }
        }

        /// <summary>Reference tới UI element (dùng bởi NodeControl và Renderer).</summary>
        public TextBlock? TitleTextBlockUI { get; set; }

        public TitleColorMode TitleColorMode
        {
            get => _titleColorMode;
            set { if (_titleColorMode != value) { _titleColorMode = value; OnPropertyChanged(); } }
        }

        public string? TitleColorKey
        {
            get => _titleColorKey;
            set { if (_titleColorKey != value) { _titleColorKey = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Source configuration

        /// <summary>Id của node nguồn cần lấy output.</summary>
        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>Key output của node nguồn.</summary>
        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Nếu true và node nguồn là WebNode: chờ PendingOutputsTcs (trang load xong)
        /// trước khi lấy giá trị.
        /// </summary>
        public bool WaitForWebNodeLoad
        {
            get => _waitForWebNodeLoad;
            set { if (_waitForWebNodeLoad != value) { _waitForWebNodeLoad = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Timer configuration

        /// <summary>Bật/tắt chế độ tự động lấy dữ liệu theo chu kỳ.</summary>
        public bool EnableTimer
        {
            get => _enableTimer;
            set { if (_enableTimer != value) { _enableTimer = value; OnPropertyChanged(); } }
        }

        /// <summary>Giá trị interval (đơn vị theo TimerUnit).</summary>
        public int TimerIntervalValue
        {
            get => _timerIntervalValue;
            set
            {
                if (_timerIntervalValue != value && value > 0)
                {
                    _timerIntervalValue = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Đơn vị thời gian: "ms" | "s" | "m".</summary>
        public string TimerUnit
        {
            get => _timerUnit;
            set { if (_timerUnit != value) { _timerUnit = value ?? "s"; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Nếu true: trước khi áp dụng chu kỳ Timer chính, node sẽ quét data theo chu kỳ scan
        /// cho tới khi thấy data hợp lệ ở output key đã chọn (hoặc bất kỳ output khi để trống key).
        /// </summary>
        public bool EnableDataReadyScan
        {
            get => _enableDataReadyScan;
            set { if (_enableDataReadyScan != value) { _enableDataReadyScan = value; OnPropertyChanged(); } }
        }

        /// <summary>Chu kỳ scan data (đơn vị theo DataReadyScanUnit).</summary>
        public int DataReadyScanIntervalValue
        {
            get => _dataReadyScanIntervalValue;
            set
            {
                if (_dataReadyScanIntervalValue != value && value > 0)
                {
                    _dataReadyScanIntervalValue = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Đơn vị thời gian scan: "ms" | "s" | "m".</summary>
        public string DataReadyScanUnit
        {
            get => _dataReadyScanUnit;
            set { if (_dataReadyScanUnit != value) { _dataReadyScanUnit = value ?? "s"; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Danh sách output key cần kiểm tra khi bật DataReadyScan.
        /// Nếu rỗng: dùng logic cũ (theo SourceOutputKey hoặc bất kỳ output nào có data).
        /// Nếu có phần tử: chỉ khi TẤT CẢ key này có data thì mới chuyển sang chu kỳ chính.
        /// </summary>
        public List<string> DataReadyScanKeys
        {
            get => _dataReadyScanKeys;
            set
            {
                _dataReadyScanKeys = value?
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select(k => k.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();
                OnPropertyChanged();
            }
        }

        /// <summary>Tính interval thực tế (ms) từ TimerIntervalValue và TimerUnit.</summary>
        public int GetTimerIntervalMs()
        {
            return _timerUnit switch
            {
                "ms" => Math.Max(50, _timerIntervalValue),
                "m"  => _timerIntervalValue * 60 * 1000,
                _    => _timerIntervalValue * 1000 // "s" (default)
            };
        }

        /// <summary>Tính interval scan data (ms) từ DataReadyScanIntervalValue và DataReadyScanUnit.</summary>
        public int GetDataReadyScanIntervalMs()
        {
            return _dataReadyScanUnit switch
            {
                "ms" => Math.Max(50, _dataReadyScanIntervalValue),
                "m"  => _dataReadyScanIntervalValue * 60 * 1000,
                _    => _dataReadyScanIntervalValue * 1000 // "s" (default)
            };
        }

        #endregion

        #region Realtime configuration

        /// <summary>
        /// Nếu true: khi node nguồn hoàn thành xử lý và phát PropertyChanged/DataChanged,
        /// DataFetcherNode sẽ tự động nhận giá trị mới.
        /// </summary>
        public bool EnableRealtime
        {
            get => _enableRealtime;
            set { if (_enableRealtime != value) { _enableRealtime = value; OnPropertyChanged(); } }
        }

        #endregion

        #region RunSourceNodeFirst

        private bool _runSourceNodeFirst = false;

        /// <summary>
        /// Nếu true: trước khi copy output từ node nguồn, executor sẽ chạy lại node nguồn
        /// để lấy dữ liệu mới nhất (tương đương nhấn nút ▶ của node đó).
        /// </summary>
        public bool RunSourceNodeFirst
        {
            get => _runSourceNodeFirst;
            set { if (_runSourceNodeFirst != value) { _runSourceNodeFirst = value; OnPropertyChanged(); } }
        }

        #endregion
    }
}
