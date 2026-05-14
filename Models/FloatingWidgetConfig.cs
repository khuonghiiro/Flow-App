using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FlowMy.Models
{
    // ── Enums cho Floating Widget ──

    /// <summary>Hình dạng khi widget thu nhỏ (idle).</summary>
    public enum WidgetIdleShape
    {
        Circle = 0,
        Diamond = 1,
        Square = 2,
        RoundedSquare = 3
    }

    /// <summary>Cạnh màn hình ưu tiên khi snap.</summary>
    public enum WidgetSnapEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 3,
        Bottom = 4
    }

    /// <summary>Chế độ hiển thị widget.</summary>
    public enum WidgetDisplayMode
    {
        /// <summary>Bình thường — có title bar + content.</summary>
        Normal = 0,
        /// <summary>Nhỏ gọn — title bar rất mỏng.</summary>
        Compact = 1,
        /// <summary>Full content — ẩn title bar hoàn toàn.</summary>
        FullContent = 2
    }

    /// <summary>Hiệu ứng animation cho idle shape.</summary>
    public enum WidgetIdleAnimation
    {
        None = 0,
        Heartbeat = 1,
        Ripple = 2
    }

    /// <summary>
    /// Cấu hình đầy đủ để xuất một node ra ngoài màn hình dưới dạng floating widget.
    /// Được lưu vào workflow JSON khi persist.
    /// Kiến trúc mở rộng: bất kỳ WorkflowNode nào cũng có thể có FloatingWidgetConfig.
    /// </summary>
    public class FloatingWidgetConfig : INotifyPropertyChanged
    {
        /// <summary>Lấy kích thước work area màn hình chính (fallback khi không đọc được).</summary>
        public static (double Width, double Height) GetPrimaryWorkAreaSize()
        {
            try
            {
                var ps = Screen.PrimaryScreen;
                if (ps != null)
                {
                    var a = ps.WorkingArea;
                    if (a.Width > 0 && a.Height > 0)
                        return (a.Width, a.Height);
                }
            }
            catch { /* ignore */ }

            return (1920, 1080);
        }

        public FloatingWidgetConfig()
        {
            ApplyDefaultExpandedMaxFromPrimaryWorkArea();
        }

        /// <summary>
        /// Gán Max expanded (px) và max tỉ lệ theo work area màn hình chính — gọi từ ctor khi tạo widget mới
        /// (thay cho max mặc định 1200×900). Giá trị vẫn bị ghi đè khi deserialize JSON nếu workflow đã lưu Max.
        /// </summary>
        public void ApplyDefaultExpandedMaxFromPrimaryWorkArea()
        {
            var (w, h) = GetPrimaryWorkAreaSize();
            _maxExpandedWidth = Math.Max(200, w - 8);
            _maxExpandedHeight = Math.Max(150, h - 8);
            _maxWidthRatio = 1.0;
            _maxHeightRatio = 1.0;
        }

        // ── Kích hoạt ──
        private bool _isEnabled;
        /// <summary>Bật/tắt chế độ floating widget cho node này.</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
        }

        // ── Tên widget (hiển thị ở MainWindow, launcher, ...) ──
        private string _widgetName = string.Empty;
        /// <summary>Tên widget do người dùng đặt. Nếu trống, fallback về node.Title.</summary>
        public string WidgetName
        {
            get => _widgetName;
            set { if (_widgetName != value) { _widgetName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        // ── Hình dạng khi thu nhỏ (Idle / Collapsed) ──
        private WidgetIdleShape _idleShape = WidgetIdleShape.Circle;
        public WidgetIdleShape IdleShape
        {
            get => _idleShape;
            set { if (_idleShape != value) { _idleShape = value; OnPropertyChanged(); } }
        }

        private double _idleSize = 48;
        /// <summary>Kích thước (px) hình dạng khi idle. Mặc định 48.</summary>
        public double IdleSize
        {
            get => _idleSize;
            set { var v = Math.Max(24, Math.Min(120, value)); if (Math.Abs(_idleSize - v) > 0.01) { _idleSize = v; OnPropertyChanged(); } }
        }

        private string _idleIconText = "⚡";
        /// <summary>Icon/emoji hiển thị bên trong hình idle.</summary>
        public string IdleIconText
        {
            get => _idleIconText;
            set { if (_idleIconText != value) { _idleIconText = value ?? "⚡"; OnPropertyChanged(); } }
        }

        private double _idleOpacity = 0.85;
        public double IdleOpacity
        {
            get => _idleOpacity;
            set { var v = Math.Max(0.1, Math.Min(1.0, value)); if (Math.Abs(_idleOpacity - v) > 0.001) { _idleOpacity = v; OnPropertyChanged(); } }
        }

        private WidgetIdleAnimation _idleAnimation = WidgetIdleAnimation.Heartbeat;
        /// <summary>Hiệu ứng của widget khi ở trạng thái idle.</summary>
        public WidgetIdleAnimation IdleAnimation
        {
            get => _idleAnimation;
            set { if (_idleAnimation != value) { _idleAnimation = value; OnPropertyChanged(); } }
        }

        private string? _idleBackgroundColor;
        /// <summary>Màu nền hình idle (#hex). Để trống = dùng PrimaryBrush của theme.</summary>
        public string? IdleBackgroundColor
        {
            get => _idleBackgroundColor;
            set { if (_idleBackgroundColor != value) { _idleBackgroundColor = value; OnPropertyChanged(); } }
        }

        private string? _idleForegroundColor;
        /// <summary>Màu icon/chữ trong hình idle (#hex). Để trống = TextOnPrimaryBrush của theme.</summary>
        public string? IdleForegroundColor
        {
            get => _idleForegroundColor;
            set { if (_idleForegroundColor != value) { _idleForegroundColor = value; OnPropertyChanged(); } }
        }

        // ── Kích thước khi mở rộng (Expanded) ──
        private double _expandedWidth = 400;
        public double ExpandedWidth
        {
            get => _expandedWidth;
            set { var v = Math.Max(200, value); if (Math.Abs(_expandedWidth - v) > 0.01) { _expandedWidth = v; OnPropertyChanged(); } }
        }

        private double _expandedHeight = 300;
        public double ExpandedHeight
        {
            get => _expandedHeight;
            set { var v = Math.Max(150, value); if (Math.Abs(_expandedHeight - v) > 0.01) { _expandedHeight = v; OnPropertyChanged(); } }
        }

        private bool _allowResize = true;
        /// <summary>Cho phép kéo thay đổi kích thước khi expanded.</summary>
        public bool AllowResize
        {
            get => _allowResize;
            set { if (_allowResize != value) { _allowResize = value; OnPropertyChanged(); } }
        }

        private double _minExpandedWidth = 200;
        public double MinExpandedWidth { get => _minExpandedWidth; set { _minExpandedWidth = value; OnPropertyChanged(); } }

        private double _minExpandedHeight = 150;
        public double MinExpandedHeight { get => _minExpandedHeight; set { _minExpandedHeight = value; OnPropertyChanged(); } }

        private double _maxExpandedWidth = 1200;
        public double MaxExpandedWidth { get => _maxExpandedWidth; set { _maxExpandedWidth = value; OnPropertyChanged(); } }

        private double _maxExpandedHeight = 900;
        public double MaxExpandedHeight { get => _maxExpandedHeight; set { _maxExpandedHeight = value; OnPropertyChanged(); } }

        // ── Kích thước theo tỉ lệ màn hình (0.0–1.0) ──
        private bool _useRatioSize;
        /// <summary>
        /// Nếu true, kích thước expanded tính theo tỉ lệ screen (WidthRatio/HeightRatio).
        /// Nếu false (mặc định), dùng ExpandedWidth/ExpandedHeight (px).
        /// </summary>
        public bool UseRatioSize
        {
            get => _useRatioSize;
            set { if (_useRatioSize != value) { _useRatioSize = value; OnPropertyChanged(); } }
        }

        private double _widthRatio = 0.3;
        /// <summary>Tỉ lệ bề rộng so với work area (0.05 – 1.0).</summary>
        public double WidthRatio
        {
            get => _widthRatio;
            set { var v = Math.Max(0.05, Math.Min(1.0, value)); if (Math.Abs(_widthRatio - v) > 0.0001) { _widthRatio = v; OnPropertyChanged(); } }
        }

        private double _heightRatio = 0.35;
        public double HeightRatio
        {
            get => _heightRatio;
            set { var v = Math.Max(0.05, Math.Min(1.0, value)); if (Math.Abs(_heightRatio - v) > 0.0001) { _heightRatio = v; OnPropertyChanged(); } }
        }

        private double _minWidthRatio = 0.15;
        public double MinWidthRatio
        {
            get => _minWidthRatio;
            set { var v = Math.Max(0.05, Math.Min(1.0, value)); if (Math.Abs(_minWidthRatio - v) > 0.0001) { _minWidthRatio = v; OnPropertyChanged(); } }
        }

        private double _minHeightRatio = 0.15;
        public double MinHeightRatio
        {
            get => _minHeightRatio;
            set { var v = Math.Max(0.05, Math.Min(1.0, value)); if (Math.Abs(_minHeightRatio - v) > 0.0001) { _minHeightRatio = v; OnPropertyChanged(); } }
        }

        private double _maxWidthRatio = 0.9;
        public double MaxWidthRatio
        {
            get => _maxWidthRatio;
            set { var v = Math.Max(0.05, Math.Min(1.0, value)); if (Math.Abs(_maxWidthRatio - v) > 0.0001) { _maxWidthRatio = v; OnPropertyChanged(); } }
        }

        private double _maxHeightRatio = 0.9;
        public double MaxHeightRatio
        {
            get => _maxHeightRatio;
            set { var v = Math.Max(0.05, Math.Min(1.0, value)); if (Math.Abs(_maxHeightRatio - v) > 0.0001) { _maxHeightRatio = v; OnPropertyChanged(); } }
        }

        // ── Vị trí & Di chuyển ──
        private bool _allowDrag = true;
        /// <summary>Cho phép kéo widget di chuyển.</summary>
        public bool AllowDrag
        {
            get => _allowDrag;
            set { if (_allowDrag != value) { _allowDrag = value; OnPropertyChanged(); } }
        }

        private bool _snapToEdge = true;
        /// <summary>Tự bám sát vào cạnh màn hình gần nhất khi thả.</summary>
        public bool SnapToEdge
        {
            get => _snapToEdge;
            set { if (_snapToEdge != value) { _snapToEdge = value; OnPropertyChanged(); } }
        }

        private WidgetSnapEdge _preferredEdge = WidgetSnapEdge.Right;
        public WidgetSnapEdge PreferredEdge
        {
            get => _preferredEdge;
            set { if (_preferredEdge != value) { _preferredEdge = value; OnPropertyChanged(); } }
        }

        private double _snapMargin = 4;
        /// <summary>Khoảng cách (px) từ widget tới cạnh màn hình khi snap.</summary>
        public double SnapMargin
        {
            get => _snapMargin;
            set { if (Math.Abs(_snapMargin - value) > 0.01) { _snapMargin = value; OnPropertyChanged(); } }
        }

        private double? _savedX;
        /// <summary>Vị trí X đã lưu (persist).</summary>
        public double? SavedX { get => _savedX; set { _savedX = value; OnPropertyChanged(); } }

        private double? _savedY;
        /// <summary>Vị trí Y đã lưu (persist).</summary>
        public double? SavedY { get => _savedY; set { _savedY = value; OnPropertyChanged(); } }

        // ── Hành vi Idle (Tự bám sát khi không dùng) ──
        private bool _autoCollapseWhenIdle = true;
        /// <summary>Tự thu nhỏ về idle shape khi không tương tác.</summary>
        public bool AutoCollapseWhenIdle
        {
            get => _autoCollapseWhenIdle;
            set { if (_autoCollapseWhenIdle != value) { _autoCollapseWhenIdle = value; OnPropertyChanged(); } }
        }

        private int _idleTimeoutSeconds = 10;
        /// <summary>Số giây không tương tác trước khi thu nhỏ.</summary>
        public int IdleTimeoutSeconds
        {
            get => _idleTimeoutSeconds;
            set { var v = Math.Max(1, value); if (_idleTimeoutSeconds != v) { _idleTimeoutSeconds = v; OnPropertyChanged(); } }
        }

        private bool _collapseWhenClickOutsideExpanded;
        /// <summary>
        /// Khi widget đang mở rộng (expanded), click ra ngoài widget thì tự thu nhỏ về idle.
        /// </summary>
        public bool CollapseWhenClickOutsideExpanded
        {
            get => _collapseWhenClickOutsideExpanded;
            set { if (_collapseWhenClickOutsideExpanded != value) { _collapseWhenClickOutsideExpanded = value; OnPropertyChanged(); } }
        }

        private bool _pinnedNoAutoHide;
        /// <summary>
        /// Ghim widget: không tự ẩn/thu nhỏ theo thời gian idle.
        /// Khi bật, runtime sẽ ưu tiên giữ widget không auto-collapse/slide.
        /// </summary>
        public bool PinnedNoAutoHide
        {
            get => _pinnedNoAutoHide;
            set { if (_pinnedNoAutoHide != value) { _pinnedNoAutoHide = value; OnPropertyChanged(); } }
        }

        private bool _slideToEdgeWhenIdle = true;
        /// <summary>
        /// Khi idle (quá IdleTimeout mà không tương tác) thì tự bám sát cạnh màn hình.
        /// - EdgeDockAsSquare=true: đổi sang ô vuông nhỏ chứa icon.
        /// - EdgeDockAsSquare=false: giữ nguyên hình idle (Circle/Diamond/...).
        /// Khi dock, widget sẽ ẩn một phần theo SlideHidePercent; hover vào sẽ lộ đầy đủ hình.
        /// Nếu false, widget ở yên vị trí hiện tại (không bám cạnh).
        /// </summary>
        public bool SlideToEdgeWhenIdle
        {
            get => _slideToEdgeWhenIdle;
            set { if (_slideToEdgeWhenIdle != value) { _slideToEdgeWhenIdle = value; OnPropertyChanged(); } }
        }

        private bool _edgeDockAsSquare = true;
        /// <summary>
        /// Khi SlideToEdgeWhenIdle=true và widget bám cạnh:
        /// - true (mặc định): hiển thị dạng ô vuông nhỏ chứa icon.
        /// - false: giữ nguyên hình idle gốc.
        /// </summary>
        public bool EdgeDockAsSquare
        {
            get => _edgeDockAsSquare;
            set { if (_edgeDockAsSquare != value) { _edgeDockAsSquare = value; OnPropertyChanged(); } }
        }

        private double _edgeDockSquareSize = 28;
        /// <summary>Kích thước (px) ô vuông khi bám cạnh (EdgeDockAsSquare=true).</summary>
        public double EdgeDockSquareSize
        {
            get => _edgeDockSquareSize;
            set { var v = Math.Max(16, Math.Min(80, value)); if (Math.Abs(_edgeDockSquareSize - v) > 0.01) { _edgeDockSquareSize = v; OnPropertyChanged(); } }
        }

        private double _slideHidePercent = 0.5;
        /// <summary>
        /// Phần trăm widget ẩn đi khi dock vào cạnh (0.0–0.95). 0.5 = ẩn nửa widget.
        /// </summary>
        public double SlideHidePercent
        {
            get => _slideHidePercent;
            set { var v = Math.Max(0, Math.Min(0.95, value)); if (Math.Abs(_slideHidePercent - v) > 0.001) { _slideHidePercent = v; OnPropertyChanged(); } }
        }

        // ── Trạng thái Window ──
        private bool _alwaysOnTop = true;
        /// <summary>Topmost — không cho cửa sổ khác đè lên.</summary>
        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set { if (_alwaysOnTop != value) { _alwaysOnTop = value; OnPropertyChanged(); } }
        }

        private bool _lockPosition;
        /// <summary>Khóa vị trí, không cho di chuyển.</summary>
        public bool LockPosition
        {
            get => _lockPosition;
            set { if (_lockPosition != value) { _lockPosition = value; OnPropertyChanged(); } }
        }

        private bool _showInTaskbar;
        /// <summary>Hiện trên thanh taskbar Windows.</summary>
        public bool ShowInTaskbar
        {
            get => _showInTaskbar;
            set { if (_showInTaskbar != value) { _showInTaskbar = value; OnPropertyChanged(); } }
        }

        private WidgetIdleShape _taskbarIconShape = WidgetIdleShape.Circle;
        /// <summary>Hình dạng icon của taskbar button.</summary>
        public WidgetIdleShape TaskbarIconShape
        {
            get => _taskbarIconShape;
            set { if (_taskbarIconShape != value) { _taskbarIconShape = value; OnPropertyChanged(); } }
        }

        private double _taskbarIconSize = 22;
        /// <summary>Kích thước icon taskbar (px).</summary>
        public double TaskbarIconSize
        {
            get => _taskbarIconSize;
            set
            {
                var v = Math.Max(12, Math.Min(40, value));
                if (Math.Abs(_taskbarIconSize - v) > 0.01) { _taskbarIconSize = v; OnPropertyChanged(); }
            }
        }

        // ── Title bar ──
        private bool _showTitleBar = true;
        /// <summary>Hiện thanh tiêu đề mini phía trên widget.</summary>
        public bool ShowTitleBar
        {
            get => _showTitleBar;
            set { if (_showTitleBar != value) { _showTitleBar = value; OnPropertyChanged(); } }
        }

        private bool _autoHideTitleBar = true;
        /// <summary>Tự ẩn title bar khi không tương tác, hiện lại khi hover.</summary>
        public bool AutoHideTitleBar
        {
            get => _autoHideTitleBar;
            set { if (_autoHideTitleBar != value) { _autoHideTitleBar = value; OnPropertyChanged(); } }
        }

        private int _titleBarHideTimeoutSeconds = 3;
        /// <summary>Số giây không hover trước khi ẩn title bar.</summary>
        public int TitleBarHideTimeoutSeconds
        {
            get => _titleBarHideTimeoutSeconds;
            set { var v = Math.Max(1, value); if (_titleBarHideTimeoutSeconds != v) { _titleBarHideTimeoutSeconds = v; OnPropertyChanged(); } }
        }

        private bool _showSideActionButton = true;
        /// <summary>Hiện nút thao tác nhỏ bên cạnh widget khi đang expanded.</summary>
        public bool ShowSideActionButton
        {
            get => _showSideActionButton;
            set { if (_showSideActionButton != value) { _showSideActionButton = value; OnPropertyChanged(); } }
        }

        // ── Display Mode ──
        private WidgetDisplayMode _displayMode = WidgetDisplayMode.Normal;
        public WidgetDisplayMode DisplayMode
        {
            get => _displayMode;
            set { if (_displayMode != value) { _displayMode = value; OnPropertyChanged(); } }
        }

        // ── Multi-monitor ──
        private int _monitorIndex = -1;
        /// <summary>Chỉ số màn hình. -1 = primary, 0..N = màn hình cụ thể.</summary>
        public int MonitorIndex
        {
            get => _monitorIndex;
            set { if (_monitorIndex != value) { _monitorIndex = value; OnPropertyChanged(); } }
        }

        private bool _showOnAllMonitors;
        /// <summary>Hiển thị widget trên tất cả màn hình (tạo 1 instance per monitor).</summary>
        public bool ShowOnAllMonitors
        {
            get => _showOnAllMonitors;
            set { if (_showOnAllMonitors != value) { _showOnAllMonitors = value; OnPropertyChanged(); } }
        }

        // ── INotifyPropertyChanged ──
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
