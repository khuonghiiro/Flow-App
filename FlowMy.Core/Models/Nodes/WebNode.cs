using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using FlowMy.Models;

namespace FlowMy.Models.Nodes
{
    /// <summary>Chế độ chờ outputs: đợi tất cả keys hoặc chỉ cần bất kỳ 1 key (ANY) xuất hiện.</summary>
    public enum WebOutputsWaitMode
    {
        /// <summary>Đợi cho tới khi TẤT CẢ keys cần đợi đã có value.</summary>
        All = 0,
        /// <summary>Chỉ cần BẤT KỲ 1 key cần đợi có value là tiếp tục (phù hợp nhiều nhánh request khác nhau).</summary>
        Any = 1
    }

    /// <summary>Một input mapping: node nguồn + key → tên biến trong URL template.</summary>
    public sealed class WebInputMapping : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private string? _inputKeyOverride;

        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveInputKey)); } }
        }

        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveInputKey)); } }
        }

        public string? InputKeyOverride
        {
            get => _inputKeyOverride;
            set { if (_inputKeyOverride != value) { _inputKeyOverride = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveInputKey)); } }
        }

        /// <summary>Tên biến trong URL = InputKeyOverride nếu có, không thì SourceOutputKey, mặc định "input".</summary>
        public string EffectiveInputKey => !string.IsNullOrWhiteSpace(_inputKeyOverride)
            ? _inputKeyOverride!.Trim()
            : (string.IsNullOrWhiteSpace(_sourceOutputKey) ? "input" : _sourceOutputKey!.Trim());

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Output response: lấy response từ URL với method, key dùng để node khác gọi tới lấy value.
    /// ExtractType: Response (body), Headers, Params (query string), Payload (request body - chỉ khi bị chặn), RequestHeaders.
    /// </summary>
    public sealed class WebResponseOutput : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _url = string.Empty;
        private string _requestMethod = "GET";
        private string _extractType = "Response";
        private bool _waitForCompletion; // Nếu true: executor sẽ đợi key này trước khi chạy node tiếp theo

        public string Key
        {
            get => _key;
            set { if (_key != value) { _key = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string Url
        {
            get => _url;
            set { if (_url != value) { _url = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string RequestMethod
        {
            get => _requestMethod;
            set { if (_requestMethod != value) { _requestMethod = value ?? "GET"; OnPropertyChanged(); } }
        }

        /// <summary>Loại dữ liệu lấy: Response (body), Headers, Params (query string), Payload (request body), RequestHeaders.</summary>
        public string ExtractType
        {
            get => _extractType;
            set { if (_extractType != value) { _extractType = value ?? "Response"; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Nếu true: workflow executor sẽ CHỜ cho đến khi WebView2 populate xong giá trị cho key này
        /// (hoặc tới khi hết timeout) trước khi traverse sang các node sau. Nếu false: key này được
        /// coi là optional, không bắt buộc phải xong trước khi node tiếp theo chạy.
        /// </summary>
        public bool WaitForCompletion
        {
            get => _waitForCompletion;
            set { if (_waitForCompletion != value) { _waitForCompletion = value; OnPropertyChanged(); } }
        }

        private int _timeoutMs = 0;
        /// <summary>
        /// Thời gian chờ cho key này (ms). Mặc định 0 = chờ cho tới khi request/response xong.
        /// > 0: Chờ tối đa số ms này. Nếu xong trước timeout thì ngắt chờ ngay lập tức; nếu quá timeout thì tiếp tục workflow.
        /// </summary>
        public int TimeoutMs
        {
            get => _timeoutMs;
            set { if (_timeoutMs != value) { _timeoutMs = value; OnPropertyChanged(); } }
        }

        private bool _isList;
        /// <summary>
        /// Nếu true: gom tất cả response khớp vào mảng JSON ["item1", "item2"].
        /// Mặc định false: chỉ giữ 1 giá trị chuỗi đơn lẻ duy nhất.
        /// </summary>
        public bool IsList
        {
            get => _isList;
            set { if (_isList != value) { _isList = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Một rule chặn/thay request: khớp URL → thay URL/params/body bằng value từ Node+Key.
    /// </summary>
    public sealed class WebRequestInterceptRule : INotifyPropertyChanged
    {
        private string _matchUrlPattern = string.Empty;
        private string _replaceUrlValue = string.Empty;
        private string? _replaceUrlSourceNodeId;
        private string? _replaceUrlSourceOutputKey;
        private bool _replaceUrlWithNodeKey = false; // Nếu true: dùng node+key để thay URL (cURL), nếu false: dùng ReplaceUrlValue
        private string _replaceParamsValue = string.Empty;
        private string? _replaceParamsSourceNodeId;
        private string? _replaceParamsSourceOutputKey;
        private string _replaceBodyValue = string.Empty;
        private string? _replaceBodySourceNodeId;
        private string? _replaceBodySourceOutputKey;

        public string MatchUrlPattern
        {
            get => _matchUrlPattern;
            set { if (_matchUrlPattern != value) { _matchUrlPattern = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string ReplaceUrlValue
        {
            get => _replaceUrlValue;
            set { if (_replaceUrlValue != value) { _replaceUrlValue = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string? ReplaceUrlSourceNodeId
        {
            get => _replaceUrlSourceNodeId;
            set { if (_replaceUrlSourceNodeId != value) { _replaceUrlSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? ReplaceUrlSourceOutputKey
        {
            get => _replaceUrlSourceOutputKey;
            set { if (_replaceUrlSourceOutputKey != value) { _replaceUrlSourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>Nếu true: dùng node+key để thay URL (cURL), nếu false: dùng ReplaceUrlValue.</summary>
        public bool ReplaceUrlWithNodeKey
        {
            get => _replaceUrlWithNodeKey;
            set { if (_replaceUrlWithNodeKey != value) { _replaceUrlWithNodeKey = value; OnPropertyChanged(); } }
        }

        public string ReplaceParamsValue
        {
            get => _replaceParamsValue;
            set { if (_replaceParamsValue != value) { _replaceParamsValue = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string? ReplaceParamsSourceNodeId
        {
            get => _replaceParamsSourceNodeId;
            set { if (_replaceParamsSourceNodeId != value) { _replaceParamsSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? ReplaceParamsSourceOutputKey
        {
            get => _replaceParamsSourceOutputKey;
            set { if (_replaceParamsSourceOutputKey != value) { _replaceParamsSourceOutputKey = value; OnPropertyChanged(); } }
        }

        public string ReplaceBodyValue
        {
            get => _replaceBodyValue;
            set { if (_replaceBodyValue != value) { _replaceBodyValue = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string? ReplaceBodySourceNodeId
        {
            get => _replaceBodySourceNodeId;
            set { if (_replaceBodySourceNodeId != value) { _replaceBodySourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? ReplaceBodySourceOutputKey
        {
            get => _replaceBodySourceOutputKey;
            set { if (_replaceBodySourceOutputKey != value) { _replaceBodySourceOutputKey = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Một mapping JS: node nguồn + key → khi node đó chạy đến Web thì chạy JS từ key đó trong WebView2.</summary>
    public sealed class WebJsSourceMapping : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private bool _autoTimerEnabled;
        private double _autoTimerIntervalValue = 30;
        private string _autoTimerIntervalUnit = "s";

        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>Bật/tắt chế độ tự động chạy JS theo chu kỳ timer (không cần chờ flow đến node).</summary>
        public bool AutoTimerEnabled
        {
            get => _autoTimerEnabled;
            set { if (_autoTimerEnabled != value) { _autoTimerEnabled = value; OnPropertyChanged(); } }
        }

        /// <summary>Giá trị khoảng thời gian auto-timer (số).</summary>
        public double AutoTimerIntervalValue
        {
            get => _autoTimerIntervalValue;
            set { if (Math.Abs(_autoTimerIntervalValue - value) > 0.0001) { _autoTimerIntervalValue = value; OnPropertyChanged(); } }
        }

        /// <summary>Đơn vị khoảng thời gian auto-timer: "ms", "s", hoặc "phút".</summary>
        public string AutoTimerIntervalUnit
        {
            get => _autoTimerIntervalUnit;
            set { if (_autoTimerIntervalUnit != value) { _autoTimerIntervalUnit = value ?? "s"; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Một URL con cho rule chặn: có URL pattern + method riêng.</summary>
    public sealed class WebBlockingChildRule : INotifyPropertyChanged
    {
        private string _urlPattern = string.Empty;
        private string _method = "All";

        /// <summary>URL pattern của URL con.</summary>
        public string UrlPattern
        {
            get => _urlPattern;
            set { if (_urlPattern != value) { _urlPattern = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Method áp dụng cho URL con (All/GET/POST/...).</summary>
        public string Method
        {
            get => _method;
            set { if (_method != value) { _method = value ?? "All"; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Rule chặn request: nếu URL khớp pattern -> chặn. Có thể cấu hình thêm danh sách URL con.
    /// </summary>
    public sealed class WebBlockingRule : INotifyPropertyChanged
    {
        private string _urlPattern = string.Empty;
        private string _method = "All";

        public string UrlPattern
        {
            get => _urlPattern;
            set { if (_urlPattern != value) { _urlPattern = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string Method
        {
            get => _method;
            set { if (_method != value) { _method = value ?? "All"; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Danh sách URL con của rule này. Chỉ bị chặn SAU KHI URL cha (UrlPattern)
        /// đã từng bị chặn ít nhất một lần trong lần chạy node hiện tại.
        /// </summary>
        public ObservableCollection<WebBlockingChildRule> ChildRules { get; } = new();

        /// <summary>
        /// Runtime-only: đánh dấu rằng trong lần chạy node hiện tại URL cha của rule này đã bị chặn.
        /// Dùng để quyết định có chặn các URL con hay không.
        /// </summary>
        [JsonIgnore]
        public bool HasTriggeredParentInCurrentRun { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Node Web (WebView2): hiển thị web, output cookie/bearer/access_token từ response,
    /// input cookie từ node+key, chặn/thay request theo rule, chặn request sau khi request nào đó thành công.
    /// </summary>
    public sealed class WebNode : WorkflowNode
    {
        private double _width = 420;
        private double _height = 320;
        private bool _isViewportExpanded;
        private string _extractUrl = "https://google.com";
        private string _extractRequestMethod = "GET";
        private string _extractStatusCode = "200";
        private bool _syncLiveOutputsToResults; // Nếu true: khi WebView2 bắt được response thì cập nhật luôn Execution Results toggle
        private List<WebInputMapping> _inputMappings = new();
        private string? _cookieText; // Text cookie để set vào WebView2 khi mở website
        private int _responseOutputsWaitTimeoutMs = 15000; // Thời gian chờ WebView2 populate các ResponseOutputs trước khi chạy node tiếp theo (ms). 0 = không chờ.
        private WebOutputsWaitMode _responseOutputsWaitMode = WebOutputsWaitMode.All;

        // Checkpoint URL/Method: điểm tựa phân chia luồng workflow
        private string? _checkpointUrlPattern; // URL pattern checkpoint
        private string _checkpointMethod = "All"; // Method cho checkpoint
        private int _collectionTimeoutMs = 5000; // Thời gian gom tối đa (ms)

        // Auto-reload timer: tự động load lại trang sau mỗi khoảng thời gian
        private bool _autoReloadEnabled;
        private double _autoReloadIntervalValue = 30;
        private string _autoReloadIntervalUnit = "s"; // "ms" | "s" | "min"

        // Zoom theo CSS cho domain hiện tại (được lưu theo tên miền, dùng lại cho các node cùng domain)
        private string? _lastHost;
        private double _cssZoom; // 0 = chưa cấu hình, >0 = zoom đã lưu

        // Element inspector: bật/tắt chế độ hover element với border và copy XPath khi Alt+Shift
        private bool _enableElementInspector;
        private bool _enableCssSelectorInspector;
        private bool _enableSleepMode = false;
        private int _sleepIdleTimeoutValue = 5;
        private string _sleepIdleTimeoutUnit = "s"; // "ms" | "s" | "min" | "phút"
        private int _wakeRequestToken;

        // JS injection: danh sách (Node+Key) – khi node đó chạy đến Web thì chạy JS từ key đó trong WebView2
        private List<WebJsSourceMapping> _jsSources = new();

        // Runtime-only: pending JS to execute in WebView2
        private string? _pendingJavaScript;

        // Cache & Profile configuration
        private string _cacheMode = "Shared"; // "Shared" | "Isolated"
        private string _customCacheName = "Shared"; // Tên thư mục cache độc lập

        public WebNode()
        {
            Type = NodeType.Web;
            Title = "Web";

            Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"           // Port IN: dùng màu Info theo guideline
            });
            Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"   // Port OUT: dùng màu SunsetOrange theo guideline
            });

            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "cookie",
                DisplayName = "Cookie",
                ConvertType = WorkflowDataType.String
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "cookie",
                DisplayName = "Cookie",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "bearer",
                DisplayName = "Bearer",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "access_token",
                DisplayName = "Access Token",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });

            // Rebuild outputs khi ResponseOutputs thay đổi
            ResponseOutputs.CollectionChanged += (s, e) =>
            {
                // Subscribe PropertyChanged cho các output mới được thêm
                if (e.NewItems != null)
                {
                    foreach (WebResponseOutput newOutput in e.NewItems)
                    {
                        if (newOutput is INotifyPropertyChanged npc)
                        {
                            npc.PropertyChanged += (sender, args) => RebuildResponseOutputs();
                        }
                    }
                }
                RebuildResponseOutputs();
            };
            
            // Subscribe PropertyChanged cho các output hiện có
            foreach (var output in ResponseOutputs)
            {
                if (output is INotifyPropertyChanged npc)
                {
                    npc.PropertyChanged += (s, e) => RebuildResponseOutputs();
                }
            }

            // Khởi tạo với một input mapping rỗng
            _inputMappings.Add(new WebInputMapping());
        }

        public void RaisePropertyChanged(string propertyName) =>
            OnPropertyChanged(propertyName);

        public TextBlock? TitleTextBlockUI { get; set; }

        #region JS injection (from other node -> execute in WebView2)

        /// <summary>Danh sách mapping: node + key → khi node đó chạy đến Web thì chạy JS từ key đó trong WebView2.</summary>
        public List<WebJsSourceMapping> JsSources
        {
            get => _jsSources;
            set { if (_jsSources != value) { _jsSources = value ?? new List<WebJsSourceMapping>(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// Runtime-only: JS script đang chờ WebView2 thực thi. WebNodeControl sẽ ExecuteScriptAsync rồi clear.
        /// </summary>
        [JsonIgnore]
        public string? PendingJavaScript
        {
            get => _pendingJavaScript;
            set { if (_pendingJavaScript != value) { _pendingJavaScript = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Runtime-only: ExecutionId của lần chạy đang thực thi trực tiếp trên WebNode.
        /// </summary>
        [JsonIgnore]
        public string? CurrentExecutingExecutionId { get; set; }

        #endregion

        #region Size (resizable)

        public double Width
        {
            get => _width;
            set
            {
                // Đảm bảo Width luôn >= 280 để tránh lỗi HwndHost khi chuyển workflow giữa các máy
                var clampedValue = Math.Max(280, value);
                if (Math.Abs(_width - clampedValue) > 0.01)
                {
                    _width = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                // Đảm bảo Height luôn >= 200 để tránh lỗi HwndHost khi chuyển workflow giữa các máy
                var clampedValue = Math.Max(200, value);
                if (Math.Abs(_height - clampedValue) > 0.01)
                {
                    _height = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Trạng thái node đang được phóng to vừa khung nhìn (ẩn menu trái + top bar).
        /// Được lưu trong workflow JSON để khi mở lại giữ đúng layout editing.
        /// </summary>
        public bool IsViewportExpanded
        {
            get => _isViewportExpanded;
            set
            {
                if (_isViewportExpanded != value)
                {
                    _isViewportExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Extract config (URL, Method, StatusCode → cookie/bearer/access_token)

        /// <summary>URL request dùng để lấy response (vd: https://labs.google/fx/api/auth/session).</summary>
        public string ExtractUrl
        {
            get => _extractUrl;
            set { _extractUrl = value ?? string.Empty; OnPropertyChanged(); }
        }

        /// <summary>Request method (GET, POST, ...).</summary>
        public string ExtractRequestMethod
        {
            get => _extractRequestMethod;
            set { _extractRequestMethod = value ?? "GET"; OnPropertyChanged(); }
        }

        /// <summary>StatusCode kỳ vọng để coi response là thành công và lấy cookie/bearer/access_token.</summary>
        public string ExtractStatusCode
        {
            get => _extractStatusCode;
            set { _extractStatusCode = value ?? "200"; OnPropertyChanged(); }
        }

        /// <summary>
        /// Thời gian chờ (ms) cho WebView2 populate các ResponseOutputs (theo WaitForCompletion)
        /// trước khi WebNodeExecutor traverse sang các node tiếp theo.
        /// - &gt; 0: số ms cụ thể.
        /// - 0 : không chờ outputs (executor không block).
        /// </summary>
        public int ResponseOutputsWaitTimeoutMs
        {
            get => _responseOutputsWaitTimeoutMs;
            set
            {
                if (_responseOutputsWaitTimeoutMs != value)
                {
                    _responseOutputsWaitTimeoutMs = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Chế độ chờ outputs:
        /// - All: đợi tất cả key cần đợi.
        /// - Any: chỉ cần 1 key cần đợi xuất hiện là chạy tiếp (tránh kẹt khi nhiều nhánh request khác nhau).
        /// </summary>
        public WebOutputsWaitMode ResponseOutputsWaitMode
        {
            get => _responseOutputsWaitMode;
            set
            {
                if (_responseOutputsWaitMode != value)
                {
                    _responseOutputsWaitMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>URL pattern checkpoint - khi request khớp URL+Method này xuất hiện,
        /// đánh dấu ranh giới giữa các lần gom data cho từng luồng workflow.</summary>
        public string? CheckpointUrlPattern
        {
            get => _checkpointUrlPattern;
            set { if (_checkpointUrlPattern != value) { _checkpointUrlPattern = value; OnPropertyChanged(); } }
        }

        /// <summary>Method cho checkpoint URL (GET, POST, All...).</summary>
        public string CheckpointMethod
        {
            get => _checkpointMethod;
            set
            {
                var v = string.IsNullOrWhiteSpace(value) ? "All" : value;
                if (_checkpointMethod != v) { _checkpointMethod = v; OnPropertyChanged(); }
            }
        }

        /// <summary>Thời gian gom tối đa (ms). Sau thời gian này dừng gom và trả data. 0 = không giới hạn.</summary>
        public int CollectionTimeoutMs
        {
            get => _collectionTimeoutMs;
            set { if (_collectionTimeoutMs != value) { _collectionTimeoutMs = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Input mappings (URL template variables)

        /// <summary>Danh sách input: mỗi phần tử = một nguồn (node + key) → một biến trong URL template.</summary>
        public List<WebInputMapping> InputMappings
        {
            get => _inputMappings;
            set
            {
                if (_inputMappings != value)
                {
                    _inputMappings = value ?? new List<WebInputMapping>();
                    if (_inputMappings.Count == 0)
                        _inputMappings.Add(new WebInputMapping());
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Cookie configuration

        /// <summary>
        /// Text cookie để set vào WebView2. Có thể paste từ Netscape format hoặc JSON format.
        /// Nếu có link trong cookie text thì sẽ mở link đó sau khi set cookie.
        /// </summary>
        public string? CookieText
        {
            get => _cookieText;
            set { if (_cookieText != value) { _cookieText = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Profile & Cache configuration

        /// <summary>Chế độ Cache: Shared (dùng chung) hoặc Isolated (độc lập)</summary>
        public string CacheMode
        {
            get => _cacheMode;
            set { if (_cacheMode != value) { _cacheMode = value ?? "Shared"; OnPropertyChanged(); } }
        }

        /// <summary>Tên profile cache độc lập</summary>
        public string CustomCacheName
        {
            get => _customCacheName;
            set { if (_customCacheName != value) { _customCacheName = value ?? "Shared"; OnPropertyChanged(); } }
        }

        #endregion

        #region Per-domain CSS zoom

        /// <summary>Tên host (domain) cuối cùng mà WebView2 đã điều hướng tới. Dùng để map zoom theo domain.</summary>
        public string? LastHost
        {
            get => _lastHost;
            set { if (_lastHost != value) { _lastHost = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Zoom CSS đã lưu cho domain hiện tại.
        /// 0 = chưa cấu hình, >0 = giá trị zoom cụ thể (ví dụ 1.0, 1.25, 0.8...).
        /// Được lưu khi Ctrl+S và apply lại khi load workflow cho các node cùng domain.
        /// </summary>
        public double CssZoom
        {
            get => _cssZoom;
            set
            {
                if (Math.Abs(_cssZoom - value) > 0.0001)
                {
                    _cssZoom = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Bật/tắt chế độ element inspector: hover element hiển thị border, Alt+Shift copy XPath.
        /// </summary>
        public bool EnableElementInspector
        {
            get => _enableElementInspector;
            set
            {
                if (_enableElementInspector != value)
                {
                    _enableElementInspector = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Bật/tắt chế độ CSS Selector inspector: hover element hiển thị border, Alt+` copy CSS Selector.
        /// </summary>
        public bool EnableCssSelectorInspector
        {
            get => _enableCssSelectorInspector;
            set
            {
                if (_enableCssSelectorInspector != value)
                {
                    _enableCssSelectorInspector = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Nếu bật, Web node sẽ chuyển về trạng thái nghỉ khi không có tín hiệu chạy vào.
        /// Khi có flow hoặc JS source kích hoạt node, control sẽ đánh thức lại runtime.
        /// </summary>
        public bool EnableSleepMode
        {
            get => _enableSleepMode;
            set
            {
                if (_enableSleepMode != value)
                {
                    _enableSleepMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Thời gian rảnh trước khi node chuyển sang trạng thái nghỉ.</summary>
        public int SleepIdleTimeoutValue
        {
            get => _sleepIdleTimeoutValue;
            set
            {
                var v = Math.Max(1, value);
                if (_sleepIdleTimeoutValue != v)
                {
                    _sleepIdleTimeoutValue = v;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Đơn vị thời gian: "ms", "s", "min" hoặc "phút".</summary>
        public string SleepIdleTimeoutUnit
        {
            get => _sleepIdleTimeoutUnit;
            set
            {
                var u = string.IsNullOrWhiteSpace(value) ? "s" : value.Trim();
                if (_sleepIdleTimeoutUnit != u)
                {
                    _sleepIdleTimeoutUnit = u;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Block requests

        /// <summary>Danh sách các URL pattern cần chặn.</summary>
        public ObservableCollection<WebBlockingRule> BlockingRules { get; } = new();

        /// <summary>
        /// Nếu true: sau khi có ÍT NHẤT MỘT request bị chặn bởi BlockingRules, tất cả các request
        /// tiếp theo (trong cùng lần chạy node) cũng sẽ bị chặn luôn.
        /// Dùng để tránh các request “chạy đằng sau” tiếp tục gọi lên server sau khi đã chặn được request chính.
        /// </summary>
        public bool BlockAllRequestsAfterFirstMatch { get; set; }

        #endregion

        #region Live execution results sync

        /// <summary>
        /// Nếu true: khi WebView2 bắt được response (ResponseOutputs) thì đồng bộ luôn sang Execution Results
        /// (toggle result của node) giống như sau khi chạy node xong.
        /// </summary>
        public bool SyncLiveOutputsToResults
        {
            get => _syncLiveOutputsToResults;
            set { if (_syncLiveOutputsToResults != value) { _syncLiveOutputsToResults = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Auto-reload timer

        /// <summary>Bật/tắt chế độ tự động tải lại trang (F5) theo chu kỳ.</summary>
        public bool AutoReloadEnabled
        {
            get => _autoReloadEnabled;
            set { if (_autoReloadEnabled != value) { _autoReloadEnabled = value; OnPropertyChanged(); } }
        }

        /// <summary>Giá trị khoảng thời gian auto-reload (số).</summary>
        public double AutoReloadIntervalValue
        {
            get => _autoReloadIntervalValue;
            set { if (Math.Abs(_autoReloadIntervalValue - value) > 0.0001) { _autoReloadIntervalValue = value; OnPropertyChanged(); } }
        }

        /// <summary>Đơn vị khoảng thời gian auto-reload: "ms", "s", hoặc "min".</summary>
        public string AutoReloadIntervalUnit
        {
            get => _autoReloadIntervalUnit;
            set { if (_autoReloadIntervalUnit != value) { _autoReloadIntervalUnit = value ?? "s"; OnPropertyChanged(); } }
        }

        #endregion

        #region Request intercept rules

        public ObservableCollection<WebRequestInterceptRule> RequestInterceptRules { get; } = new();

        #endregion

        #region Response outputs

        /// <summary>Danh sách response outputs: mỗi output có key, url, requestMethod để lấy response.</summary>
        public ObservableCollection<WebResponseOutput> ResponseOutputs { get; } = new();

        public void RebuildResponseOutputs()
        {
            if (DynamicOutputs == null) return;

            var validKeys = new HashSet<string>(
                ResponseOutputs?
                    .Where(ro => ro != null && !string.IsNullOrWhiteSpace(ro.Key))
                    .Select(ro => ro.Key.Trim()) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            // 1. Chỉ xóa những output port do user thêm mà hiện không còn nằm trong ResponseOutputs cấu hình
            var toRemove = DynamicOutputs
                .Where(o => o.IsUserAdded && !validKeys.Contains(o.Key.Trim()))
                .ToList();
            foreach (var o in toRemove)
            {
                DynamicOutputs.Remove(o);
            }

            // 2. Với mỗi key cấu hình trong ResponseOutputs:
            // - Nếu port đã tồn tại: giữ nguyên instance, chỉ cập nhật UserValueOverride.
            // - Nếu chưa có port: tạo mới port và thêm vào DynamicOutputs.
            foreach (var responseOutput in ResponseOutputs ?? Enumerable.Empty<WebResponseOutput>())
            {
                if (string.IsNullOrWhiteSpace(responseOutput.Key)) continue;
                var trimmedKey = responseOutput.Key.Trim();

                string? val = null;
                if (ResponseOutputValues != null && ResponseOutputValues.TryGetValue(trimmedKey, out var v))
                {
                    val = v;
                }

                var isArray = responseOutput.IsList;
                var dataType = isArray ? WorkflowDataType.ArrayDynamic : WorkflowDataType.String;

                var existingPort = DynamicOutputs.FirstOrDefault(o => string.Equals(o.Key, trimmedKey, StringComparison.OrdinalIgnoreCase));
                if (existingPort != null)
                {
                    existingPort.IsMultiple = isArray;
                    existingPort.OutputType = dataType;
                    existingPort.ConvertType = dataType;
                    if (val != null)
                    {
                        existingPort.UserValueOverride = val;
                    }
                }
                else
                {
                    DynamicOutputs.Add(new WorkflowDynamicDataPort
                    {
                        Key = trimmedKey,
                        DisplayName = trimmedKey,
                        ConvertType = dataType,
                        OutputType = dataType,
                        IsMultiple = isArray,
                        UserValueOverride = val,
                        IsUserAdded = true
                    });
                }
            }
        }

        #endregion

        #region Runtime (not serialized)

        [JsonIgnore]
        public string? LastCookie { get; set; }

        [JsonIgnore]
        public string? LastBearer { get; set; }

        [JsonIgnore]
        public string? LastAccessToken { get; set; }

        /// <summary>
        /// Runtime-only: đánh dấu rằng trong lần chạy node hiện tại đã có ít nhất một request
        /// bị chặn bởi BlockingRules. Kết hợp với BlockAllRequestsAfterFirstMatch để chặn luôn
        /// các request chạy sau đó.
        /// </summary>
        [JsonIgnore]
        public bool HasTriggeredBlockingChain { get; set; }

        /// <summary>Dictionary lưu response outputs theo key: key -> json array string of responses/curls.</summary>
        [JsonIgnore]
        public Dictionary<string, string> ResponseOutputValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        private readonly object _responseOutputLock = new();
        private readonly Dictionary<string, List<string>> _responseOutputLists = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, WebNodeExecutionRun> _activeExecutionRuns = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGetMasterResponseOutputList(string key, out List<string>? list)
        {
            lock (_responseOutputLock)
            {
                if (_responseOutputLists.TryGetValue(key, out var masterList) && masterList != null)
                {
                    list = new List<string>(masterList);
                    return true;
                }
                list = null;
                return false;
            }
        }

        public bool TryGetMasterResponseOutputValue(string key, out string? value)
        {
            lock (_responseOutputLock)
            {
                return ResponseOutputValues.TryGetValue(key, out value);
            }
        }

        public WebNodeExecutionRun StartExecutionRun(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                executionId = "default";

            CurrentExecutingExecutionId = executionId;

            if (_activeExecutionRuns.TryRemove(executionId, out var oldRunSameId))
            {
                oldRunSameId.CancelDebounce();
            }

            // Chỉ xóa các output key được checked WaitForCompletion = true cho lượt chạy này,
            // giữ nguyên các key không chờ và cookie/bearer/access_token mặc định.
            ClearResponseOutputValues(onlyWaitKeys: true);

            var run = new WebNodeExecutionRun(executionId) { OwnerNode = this };

            _activeExecutionRuns[executionId] = run;
            return run;
        }

        public WebNodeExecutionRun? GetExecutionRun(string? executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return null;
            _activeExecutionRuns.TryGetValue(executionId, out var run);
            return run;
        }

        public IReadOnlyCollection<WebNodeExecutionRun> GetActiveExecutionRuns()
        {
            return _activeExecutionRuns.Values.ToList();
        }

        public void FinishExecutionRun(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return;
            if (_activeExecutionRuns.TryRemove(executionId, out var run))
            {
                run.CancelDebounce();
                lock (_responseOutputLock)
                {
                    lock (run.Lock)
                    {
                        foreach (var kv in run.ResponseOutputLists)
                        {
                            if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value != null && kv.Value.Count > 0)
                            {
                                _responseOutputLists[kv.Key] = new List<string>(kv.Value);
                            }
                        }
                        foreach (var kv in run.ResponseOutputValues)
                        {
                            if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value) && kv.Value != "[]")
                            {
                                ResponseOutputValues[kv.Key] = kv.Value;
                            }
                        }
                    }
                    RebuildResponseOutputs();
                }
            }
        }

        /// <summary>
        /// Cập nhật response output cho tất cả luồng execution đang active của node này,
        /// đồng thời đẩy real-time vào WorkflowExecutionService (nếu có) và schedule debounce TCS.
        /// </summary>
        public void UpdateResponseOutputValueForActiveRuns(
            string key,
            string value,
            bool isList,
            object? executionServiceObj,
            WebResponseOutput? roConfig = null,
            int statusCode = 200,
            object? host = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var trimmedKey = key.Trim();
            var valStr = value ?? string.Empty;
            string extractType = roConfig?.ExtractType?.Trim() ?? "Response";

            // 1. Cập nhật UI display master của node (cổng output & result panel) để thấy item gom lần lượt ngay lập tức
            UpdateResponseOutputValue(trimmedKey, valStr, isList, statusCode, extractType);
            SchedulePendingOutputsCompletion(800);

            // 2. Cập nhật cho tất cả luồng ExecutionRun đang active
            var activeRuns = GetActiveExecutionRuns();
            if (activeRuns.Count > 0)
            {
                var targetRuns = activeRuns.Where(r => r != null && !r.IsCollectionStopped).ToList();
                foreach (var run in targetRuns)
                {
                    if (run != null)
                    {
                        UpdateResponseOutputValueForExecutionRun(run.ExecutionId, trimmedKey, valStr, isList, executionServiceObj, roConfig, statusCode, host);
                    }
                }
            }
        }

        /// <summary>
        /// Cập nhật giá trị trích xuất cho key tương ứng trong lần chạy hiện tại.
        /// - Nếu isList = true: gom tất cả response thành công (200-399) khớp vào mảng JSON ["res1", "res2"].
        /// - Nếu isList = false (mặc định): lưu giá trị chuỗi đơn lẻ duy nhất (không thành mảng JSON).
        /// </summary>
        public void UpdateResponseOutputValue(string key, string value, bool isList, int statusCode = 200, string? extractType = "Response")
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var trimmedKey = key.Trim();
            var valStr = value ?? string.Empty;
            bool isResponseExtract = string.IsNullOrWhiteSpace(extractType) || string.Equals(extractType.Trim(), "Response", StringComparison.OrdinalIgnoreCase);
            bool isSuccess = statusCode >= 200 && statusCode < 400;

            // CHỈ áp dụng kiểm tra request thành công (Status 200-399) đối với kiểu lấy dữ liệu là "Response" (body)
            // Các kiểu lấy dữ liệu khác (Params, Payload, RequestHeaders, CurlCmd, Headers...) không cần check status code
            if (isResponseExtract && (!isSuccess || string.IsNullOrEmpty(valStr))) return;
            if (string.IsNullOrEmpty(valStr)) return;

            lock (_responseOutputLock)
            {
                if (isList)
                {
                    if (!_responseOutputLists.TryGetValue(trimmedKey, out var list))
                    {
                        list = new List<string>();
                        _responseOutputLists[trimmedKey] = list;
                    }
                    list.Add(valStr);
                    var jsonArray = System.Text.Json.JsonSerializer.Serialize(list);
                    ResponseOutputValues[trimmedKey] = jsonArray;

                    if (DynamicOutputs != null)
                    {
                        var dyn = DynamicOutputs.FirstOrDefault(o =>
                            string.Equals(o.Key, trimmedKey, StringComparison.OrdinalIgnoreCase));
                        if (dyn != null) dyn.UserValueOverride = jsonArray;
                    }
                }
                else
                {
                    ResponseOutputValues[trimmedKey] = valStr;

                    if (DynamicOutputs != null)
                    {
                        var dyn = DynamicOutputs.FirstOrDefault(o =>
                            string.Equals(o.Key, trimmedKey, StringComparison.OrdinalIgnoreCase));
                        if (dyn != null) dyn.UserValueOverride = valStr;
                    }
                }
            }
            OnPropertyChanged(nameof(ResponseOutputValues));
            OnPropertyChanged(nameof(DynamicOutputs));
        }

        public void AppendResponseOutputValue(string key, string value)
        {
            UpdateResponseOutputValue(key, value, isList: false);
        }

        private System.Threading.CancellationTokenSource? _pendingOutputsDebounceCts;
        private readonly object _pendingOutputsDebounceLock = new object();

        /// <summary>
        /// Xóa các mảng kết quả output tích lũy của lần chạy trước.
        /// - Nếu onlyWaitKeys = true (mặc định): chỉ xóa các key được checked WaitForCompletion = true (dữ liệu cách ly theo luồng), giữ nguyên các key không chờ và cookie/bearer/access_token.
        /// - Nếu onlyWaitKeys = false: xóa các key trừ cookie, bearer, access_token mặc định.
        /// </summary>
        public void ClearResponseOutputValues(bool onlyWaitKeys = false)
        {
            CancelPendingOutputsDebounce();
            lock (_responseOutputLock)
            {
                var defaultKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cookie", "bearer", "access_token" };

                if (onlyWaitKeys)
                {
                    if (ResponseOutputs != null && ResponseOutputs.Count > 0)
                    {
                        var waitKeySet = new HashSet<string>(
                            ResponseOutputs
                                .Where(ro => ro != null && ro.WaitForCompletion && !string.IsNullOrWhiteSpace(ro.Key))
                                .Select(ro => ro.Key.Trim()),
                            StringComparer.OrdinalIgnoreCase);

                        var listKeysToRemove = _responseOutputLists.Keys.Where(k => waitKeySet.Contains(k)).ToList();
                        foreach (var k in listKeysToRemove)
                        {
                            _responseOutputLists.Remove(k);
                        }

                        var valKeysToRemove = ResponseOutputValues.Keys.Where(k => waitKeySet.Contains(k)).ToList();
                        foreach (var k in valKeysToRemove)
                        {
                            ResponseOutputValues.Remove(k);
                        }

                        if (DynamicOutputs != null)
                        {
                            foreach (var dyn in DynamicOutputs)
                            {
                                if (!string.IsNullOrWhiteSpace(dyn.Key) && waitKeySet.Contains(dyn.Key.Trim()))
                                {
                                    dyn.UserValueOverride = null;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Khi xóa toàn bộ (chủ động reset), giữ lại các key mặc định cookie, bearer, access_token
                    var listKeysToRemove = _responseOutputLists.Keys.Where(k => !defaultKeys.Contains(k)).ToList();
                    foreach (var k in listKeysToRemove)
                    {
                        _responseOutputLists.Remove(k);
                    }

                    var valKeysToRemove = ResponseOutputValues.Keys.Where(k => !defaultKeys.Contains(k)).ToList();
                    foreach (var k in valKeysToRemove)
                    {
                        ResponseOutputValues.Remove(k);
                    }

                    if (DynamicOutputs != null)
                    {
                        foreach (var dyn in DynamicOutputs)
                        {
                            if (!string.IsNullOrWhiteSpace(dyn.Key) && !defaultKeys.Contains(dyn.Key.Trim()))
                            {
                                dyn.UserValueOverride = null;
                            }
                        }
                    }
                }
            }
            OnPropertyChanged(nameof(ResponseOutputValues));
            OnPropertyChanged(nameof(DynamicOutputs));
        }

        /// <summary>
        /// Xóa các mảng kết quả tích lũy cũ của các output keys đã cấu hình.
        /// Được gọi tự động mỗi khi một luồng workflow mới bắt đầu để làm mới dữ liệu (chỉ xóa wait keys).
        /// </summary>
        public void ClearConfiguredResponseOutputValues()
        {
            ClearResponseOutputValues(onlyWaitKeys: true);
        }

        /// <summary>
        /// Lên lịch (schedule hoặc reset) debounce completion cho PendingOutputsTcs.
        /// Giúp chờ đủ các response trong 1 stream mà không bị hoàn thành quá sớm ngay từ request đầu tiên.
        /// </summary>
        public void SchedulePendingOutputsCompletion(int debounceMs = 800)
        {
            var tcs = PendingOutputsTcs;
            if (tcs == null || tcs.Task.IsCompleted) return;

            lock (_pendingOutputsDebounceLock)
            {
                try
                {
                    _pendingOutputsDebounceCts?.Cancel();
                    _pendingOutputsDebounceCts?.Dispose();
                }
                catch { }

                var cts = new System.Threading.CancellationTokenSource();
                _pendingOutputsDebounceCts = cts;

                System.Threading.Tasks.Task.Delay(debounceMs, cts.Token).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && !cts.IsCancellationRequested)
                    {
                        var curTcs = PendingOutputsTcs;
                        if (curTcs != null && !curTcs.Task.IsCompleted)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WebNode] ✓ Debounce idle period ({debounceMs}ms) passed without new responses, completing PendingOutputsTcs.");
                            curTcs.TrySetResult(true);
                        }
                    }
                }, System.Threading.Tasks.TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Hủy debounce timer đang chờ.
        /// </summary>
        public void CancelPendingOutputsDebounce()
        {
            lock (_pendingOutputsDebounceLock)
            {
                try
                {
                    _pendingOutputsDebounceCts?.Cancel();
                    _pendingOutputsDebounceCts?.Dispose();
                }
                catch { }
                _pendingOutputsDebounceCts = null;
            }
        }

        /// <summary>
        /// TCS dùng để đồng bộ giữa WebNodeExecutor và WebView2 (WebNodeControl).
        /// Executor sẽ tạo PendingOutputsTcs mới và await nó; WebNodeControl sẽ TrySetResult(true)
        /// sau khi đã populate các ResponseOutputValues/DynamicOutputs tương ứng (ví dụ CurlCmd).
        /// </summary>
        [JsonIgnore]
        public TaskCompletionSource<bool>? PendingOutputsTcs { get; set; }

        /// <summary>
        /// Runtime-only token để yêu cầu UI đánh thức WebView2.
        /// Mỗi lần RequestWake() được gọi, token tăng lên để đảm bảo PropertyChanged luôn fire.
        /// </summary>
        [JsonIgnore]
        public int WakeRequestToken
        {
            get => _wakeRequestToken;
            private set
            {
                if (_wakeRequestToken != value)
                {
                    _wakeRequestToken = value;
                    OnPropertyChanged();
                }
            }
        }
        public void RequestWake() => WakeRequestToken++;

        public void ProcessInterceptedNetworkResponse(
            string url,
            string method,
            Dictionary<string, string> headers,
            string? postData,
            string bodyText,
            int statusCode)
        {
            ProcessInterceptedNetworkResponse(url, method, headers, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), postData, bodyText, statusCode);
        }

        private static FlowMy.Interfaces.IScopedOutputSync? ResolveScopedOutputSync(object? host)
        {
            if (host == null) return null;
            if (host is FlowMy.Interfaces.IScopedOutputSync sync) return sync;
            try
            {
                var vmProp = host.GetType().GetProperty("ViewModel");
                var vm = vmProp?.GetValue(host);
                if (vm == null) return null;
                var execProp = vm.GetType().GetProperty("WorkflowExecutionService") ?? vm.GetType().GetProperty("ExecutionService");
                return execProp?.GetValue(vm) as FlowMy.Interfaces.IScopedOutputSync;
            }
            catch { return null; }
        }

        public void ProcessInterceptedNetworkRequest(
            string url,
            string method,
            Dictionary<string, string> requestHeaders,
            string? postData,
            string? targetExecutionId = null,
            object? host = null)
        {
            if (ResponseOutputs == null || ResponseOutputs.Count == 0 || string.IsNullOrWhiteSpace(url)) return;

            var execService = ResolveScopedOutputSync(host);

            foreach (var ro in ResponseOutputs)
            {
                if (ro == null || string.IsNullOrWhiteSpace(ro.Key)) continue;

                string extractType = ro.ExtractType?.Trim() ?? "Response";
                if (!IsImmediateExtractType(extractType)) continue;

                string pattern = ro.Url?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                if (!string.IsNullOrWhiteSpace(ro.RequestMethod) &&
                    !string.Equals(ro.RequestMethod, "ALL", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ro.RequestMethod, method, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (UrlMatchesPattern(url, pattern))
                {
                    string val = ExtractValueByOutputType(extractType, url, method, requestHeaders, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), postData, string.Empty);
                    if (!string.IsNullOrEmpty(targetExecutionId) && GetExecutionRun(targetExecutionId) != null)
                    {
                        UpdateResponseOutputValueForExecutionRun(targetExecutionId, ro.Key.Trim(), val, ro.IsList, execService, ro, 200, host);
                    }
                    else
                    {
                        UpdateResponseOutputValueForActiveRuns(ro.Key.Trim(), val, ro.IsList, execService, ro, 200, host);
                    }
                }
            }
        }

        public void ProcessInterceptedNetworkResponse(
            string url,
            string method,
            Dictionary<string, string> requestHeaders,
            Dictionary<string, string> responseHeaders,
            string? postData,
            string bodyText,
            int statusCode,
            string? targetExecutionId = null,
            object? host = null)
        {
            if (ResponseOutputs == null || ResponseOutputs.Count == 0 || string.IsNullOrWhiteSpace(url)) return;

            var execService = ResolveScopedOutputSync(host);

            foreach (var ro in ResponseOutputs)
            {
                if (ro == null || string.IsNullOrWhiteSpace(ro.Key)) continue;

                string extractType = ro.ExtractType?.Trim() ?? "Response";
                // Skip immediate types if already processed during request init
                if (IsImmediateExtractType(extractType)) continue;

                string pattern = ro.Url?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                if (!string.IsNullOrWhiteSpace(ro.RequestMethod) &&
                    !string.Equals(ro.RequestMethod, "ALL", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ro.RequestMethod, method, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (UrlMatchesPattern(url, pattern))
                {
                    string val = ExtractValueByOutputType(extractType, url, method, requestHeaders, responseHeaders, postData, bodyText);
                    if (!string.IsNullOrEmpty(targetExecutionId) && GetExecutionRun(targetExecutionId) != null)
                    {
                        UpdateResponseOutputValueForExecutionRun(targetExecutionId, ro.Key.Trim(), val, ro.IsList, execService, ro, statusCode, host);
                    }
                    else
                    {
                        UpdateResponseOutputValueForActiveRuns(ro.Key.Trim(), val, ro.IsList, execService, ro, statusCode, host);
                    }
                }
            }
        }

        public static bool IsImmediateExtractType(string extractType)
        {
            if (string.IsNullOrWhiteSpace(extractType)) return false;
            var t = extractType.Trim();
            // Kiểu dữ liệu lấy từ response (Response body, response Headers) thì cần đợi response trả về hoặc lỗi
            if (string.Equals(t, "Response", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "Headers", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            // Tất cả kiểu lấy dữ liệu request (cURL, Params, Payload, RequestHeaders, RequestUrl...) được trích xuất tức thì qua CefSharp
            return true;
        }

        public void UpdateResponseOutputValueForExecutionRun(
            string executionId,
            string key,
            string? value,
            bool isList,
            object? executionServiceObj,
            WebResponseOutput? roConfig = null,
            int statusCode = 200,
            object? host = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var trimmedKey = key.Trim();
            var valStr = value ?? string.Empty;
            string extractType = roConfig?.ExtractType?.Trim() ?? "Response";
            bool isResponseExtract = string.Equals(extractType, "Response", StringComparison.OrdinalIgnoreCase);
            bool isSuccess = statusCode >= 200 && statusCode < 400;
            bool isWaitKey = roConfig != null && roConfig.WaitForCompletion;

            // CHỈ áp dụng kiểm tra request lỗi (Status Code không thuộc 200-399) đối với kiểu lấy dữ liệu là "Response" (body):
            // Nếu là kiểu "Response", request lỗi hoặc response trả về null/empty thì đối với key KHÔNG checked chờ key (WaitForCompletion == false),
            // tuyệt đối không ghi đè value và không clear key/value đi.
            if (isResponseExtract && (!isSuccess || string.IsNullOrEmpty(valStr)))
            {
                if (!isWaitKey)
                {
                    // Key không chờ & kiểu Response: Giữ nguyên dữ liệu cũ, không ghi đè rỗng/lỗi
                    return;
                }
            }
            else if (!isResponseExtract && string.IsNullOrEmpty(valStr) && !isWaitKey)
            {
                // Các kiểu khác (Params, Payload, RequestHeaders, CurlCmd...): Không check status code lỗi, chỉ bỏ qua nếu valStr rỗng đối với non-wait key
                return;
            }

            var run = GetExecutionRun(executionId);
            if (run != null)
            {
                if (run.IsCollectionStopped) return;

                string jsonOrVal = string.Empty;

                lock (run.Lock)
                {
                    if (run.IsCollectionStopped) return;

                    if (!run.ResponseOutputAttemptCounts.TryGetValue(trimmedKey, out var attempts))
                    {
                        attempts = 0;
                    }
                    attempts++;
                    run.ResponseOutputAttemptCounts[trimmedKey] = attempts;

                    if (isList)
                    {
                        if (!run.ResponseOutputLists.TryGetValue(trimmedKey, out var list))
                        {
                            list = new List<string>();
                            run.ResponseOutputLists[trimmedKey] = list;
                        }

                        bool shouldAddItem = isResponseExtract ? (isSuccess && !string.IsNullOrEmpty(valStr)) : !string.IsNullOrEmpty(valStr);

                        if (shouldAddItem)
                        {
                            var trimmedVal = valStr.Trim();
                            if (trimmedVal.StartsWith("[") && trimmedVal.EndsWith("]"))
                            {
                                try
                                {
                                    var parsedList = System.Text.Json.JsonSerializer.Deserialize<List<object>>(trimmedVal);
                                    if (parsedList != null && parsedList.Count > 0)
                                    {
                                        foreach (var item in parsedList)
                                        {
                                            var itemStr = item?.ToString() ?? string.Empty;
                                            if (!string.IsNullOrEmpty(itemStr))
                                                list.Add(itemStr);
                                        }
                                    }
                                    else
                                    {
                                        list.Add(valStr);
                                    }
                                }
                                catch
                                {
                                    list.Add(valStr);
                                }
                            }
                            else
                            {
                                list.Add(valStr);
                            }
                        }

                        jsonOrVal = System.Text.Json.JsonSerializer.Serialize(list);
                        run.ResponseOutputValues[trimmedKey] = jsonOrVal;
                        System.Diagnostics.Debug.WriteLine($"[WebNodeExecutionRun][DIAG] Key '{trimmedKey}' received response data: isList=true, list.Count={list.Count}");
                    }
                    else
                    {
                        bool shouldUpdateSingle = isResponseExtract ? (isSuccess && !string.IsNullOrEmpty(valStr)) : !string.IsNullOrEmpty(valStr);

                        if (shouldUpdateSingle)
                        {
                            jsonOrVal = valStr;
                            run.ResponseOutputValues[trimmedKey] = jsonOrVal;
                            run.SignalKeyCompleted(trimmedKey);
                        }
                        else
                        {
                            jsonOrVal = run.ResponseOutputValues.TryGetValue(trimmedKey, out var existing) ? existing : string.Empty;
                        }
                        System.Diagnostics.Debug.WriteLine($"[WebNodeExecutionRun][DIAG] Key '{trimmedKey}' received SINGLE response data");
                    }
                }

                var execService = executionServiceObj as FlowMy.Interfaces.IScopedOutputSync;
                if (execService != null && !string.IsNullOrEmpty(run.ExecutionId))
                {
                    try
                    {
                        execService.SetScopedNodeStringOutput(run.ExecutionId, Id, trimmedKey, jsonOrVal);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebNode] Real-time sync error: {ex.Message}");
                    }
                }

                // Cập nhật UI display master của node để phản ánh đúng danh sách tươi của lượt chạy hiện tại
                lock (_responseOutputLock)
                {
                    if (isList && run.ResponseOutputLists.TryGetValue(trimmedKey, out var runList) && runList != null)
                    {
                        _responseOutputLists[trimmedKey] = new List<string>(runList);
                    }
                    if (!string.IsNullOrEmpty(jsonOrVal))
                    {
                        ResponseOutputValues[trimmedKey] = jsonOrVal;
                        RebuildResponseOutputs();
                        if (DynamicOutputs != null)
                        {
                            var dyn = DynamicOutputs.FirstOrDefault(o =>
                                string.Equals(o.Key, trimmedKey, StringComparison.OrdinalIgnoreCase));
                            if (dyn != null) dyn.UserValueOverride = jsonOrVal;
                        }
                    }
                }
                OnPropertyChanged(nameof(ResponseOutputValues));
                int debounceMs = (roConfig != null && roConfig.TimeoutMs > 0) ? Math.Max(1500, roConfig.TimeoutMs) : 2000;
                run.ScheduleDebounceCompletion(debounceMs);
            }
            else
            {
                UpdateResponseOutputValueForActiveRuns(trimmedKey, valStr, isList, executionServiceObj);
            }
        }

        public bool ShouldBlockRequest(string url, string method)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (BlockAllRequestsAfterFirstMatch && HasTriggeredBlockingChain)
            {
                return true;
            }

            if (BlockingRules == null || BlockingRules.Count == 0) return false;

            foreach (var rule in BlockingRules)
            {
                if (rule == null) continue;

                if (!string.IsNullOrWhiteSpace(rule.UrlPattern) &&
                    UrlMatchesPattern(url, rule.UrlPattern) &&
                    MethodMatches(method, rule.Method))
                {
                    HasTriggeredBlockingChain = true;
                    rule.HasTriggeredParentInCurrentRun = true;
                    return true;
                }

                if (rule.ChildRules != null && rule.ChildRules.Count > 0)
                {
                    foreach (var child in rule.ChildRules)
                    {
                        if (child != null && !string.IsNullOrWhiteSpace(child.UrlPattern) &&
                            UrlMatchesPattern(url, child.UrlPattern) &&
                            MethodMatches(method, child.Method))
                        {
                            HasTriggeredBlockingChain = true;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool MethodMatches(string requestMethod, string? ruleMethod)
        {
            if (string.IsNullOrWhiteSpace(ruleMethod) || string.Equals(ruleMethod, "All", StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(requestMethod, ruleMethod, StringComparison.OrdinalIgnoreCase);
        }

        public static bool UrlMatchesPattern(string url, string pattern)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(pattern)) return false;
            pattern = pattern.Trim();

            // 1. Direct contains check (if no wildcard or template symbols)
            if (!pattern.Contains('*') && !pattern.Contains('{') && !pattern.Contains('?'))
            {
                return url.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // 2. Variable template matching like https://example.com/api/{id}
            if (pattern.Contains('{') && pattern.Contains('}'))
            {
                try
                {
                    var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                        .Replace(@"\{", "(?<")
                        .Replace(@"\}", @">[^\/\?\#]+)");
                    regexPattern = regexPattern.Replace(@"\*", ".*") + "$";
                    if (System.Text.RegularExpressions.Regex.IsMatch(url, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        return true;
                }
                catch { }
            }

            // 3. Wildcard matching (* and ?)
            try
            {
                // Full match regex
                string fullRegexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".") + "$";
                if (System.Text.RegularExpressions.Regex.IsMatch(url, fullRegexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return true;

                // Substring glob regex (for partial patterns like "*api/v1*")
                string subRegexPattern = System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".");
                if (System.Text.RegularExpressions.Regex.IsMatch(url, subRegexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return true;
            }
            catch { }

            return false;
        }

        private static string ExtractValueByOutputType(
            string? extractType,
            string url,
            string method,
            Dictionary<string, string> requestHeaders,
            Dictionary<string, string> responseHeaders,
            string? postData,
            string bodyText)
        {
            var type = extractType?.Trim() ?? "Response";

            if (string.Equals(type, "Headers", StringComparison.OrdinalIgnoreCase))
            {
                return responseHeaders != null && responseHeaders.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(responseHeaders)
                    : string.Empty;
            }
            if (string.Equals(type, "RequestHeaders", StringComparison.OrdinalIgnoreCase))
            {
                return requestHeaders != null && requestHeaders.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(requestHeaders)
                    : string.Empty;
            }
            if (string.Equals(type, "Params", StringComparison.OrdinalIgnoreCase))
            {
                int qIdx = url.IndexOf('?');
                return (qIdx >= 0 && qIdx < url.Length - 1) ? url.Substring(qIdx + 1) : string.Empty;
            }
            if (string.Equals(type, "Payload", StringComparison.OrdinalIgnoreCase))
            {
                return postData ?? string.Empty;
            }
            if (string.Equals(type, "CurlCmd", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "CurlBash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Curl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "cURL", StringComparison.OrdinalIgnoreCase))
            {
                var sb = new StringBuilder();
                sb.Append($"curl \"{url}\"");

                if (!string.IsNullOrWhiteSpace(method) && !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append($" -X {method.ToUpperInvariant()}");
                }

                if (requestHeaders != null)
                {
                    foreach (var kvp in requestHeaders)
                    {
                        if (string.Equals(kvp.Key, "Host", StringComparison.OrdinalIgnoreCase)) continue;
                        var safeVal = kvp.Value.Replace("\"", "\\\"");
                        sb.Append($" -H \"{kvp.Key}: {safeVal}\"");
                    }
                }

                if (!string.IsNullOrWhiteSpace(postData))
                {
                    var safeData = postData.Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
                    sb.Append($" --data-raw \"{safeData}\"");
                }

                return sb.ToString();
            }

            // Default: Response (body)
            return bodyText ?? string.Empty;
        }

        #endregion
    }

    /// <summary>
    /// Đại diện cho luồng dữ liệu riêng biệt của một lần chạy workflow (ExecutionId).
    /// </summary>
    public sealed class WebNodeExecutionRun
    {
        public string ExecutionId { get; }
        public Dictionary<string, string> ResponseOutputValues { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> ResponseOutputLists { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> ResponseOutputAttemptCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public TaskCompletionSource<bool>? PendingOutputsTcs { get; set; }
        public System.Threading.CancellationTokenSource? DebounceCts { get; set; }
        public object Lock { get; } = new();



        // Checkpoint-based collection state
        /// <summary>Checkpoint URL đã được phát hiện lần đầu → bắt đầu gom.</summary>
        public bool CheckpointDetected { get; set; }
        /// <summary>Đang trong trạng thái gom data (giữa checkpoint lần 1 và lần 2).</summary>
        public bool IsCollecting { get; set; }
        /// <summary>Thời điểm bắt đầu gom (để tính timeout).</summary>
        public DateTime? CollectionStartTime { get; set; }
        /// <summary>Số lần checkpoint URL xuất hiện trong run này.</summary>
        public int CheckpointHitCount { get; set; }
        /// <summary>Danh sách các Task trích xuất response content (GetContentAsync) đang chạy ngầm cho run này.</summary>
        public System.Collections.Concurrent.ConcurrentBag<System.Threading.Tasks.Task> PendingExtractions { get; } = new();

        private readonly System.Collections.Generic.HashSet<string> _completedKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.Tasks.TaskCompletionSource<bool>> _keyTcsMap = new(StringComparer.OrdinalIgnoreCase);

        public WebNodeExecutionRun(string executionId)
        {
            ExecutionId = executionId;
        }

        public WebNode? OwnerNode { get; set; }

        public bool IsCollectionStopped { get; set; }

        public void StopCollectionAndSignalAllKeys()
        {
            lock (Lock)
            {
                IsCollectionStopped = true;
                if (OwnerNode?.ResponseOutputs != null)
                {
                    foreach (var ro in OwnerNode.ResponseOutputs)
                    {
                        if (ro != null && !string.IsNullOrWhiteSpace(ro.Key))
                        {
                            var k = ro.Key.Trim();
                            _completedKeys.Add(k);
                            if (_keyTcsMap.TryGetValue(k, out var tcs))
                            {
                                tcs.TrySetResult(true);
                            }
                        }
                    }
                }
                PendingOutputsTcs?.TrySetResult(true);
            }
        }

        public bool IsKeyCompleted(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return true;
            lock (Lock)
            {
                if (IsCollectionStopped) return true;
                return _completedKeys.Contains(key.Trim());
            }
        }

        public bool IsAllKeysCompleted()
        {
            lock (Lock)
            {
                if (IsCollectionStopped) return true;
                if (OwnerNode?.ResponseOutputs == null || OwnerNode.ResponseOutputs.Count == 0) return true;
                var waitKeys = OwnerNode.ResponseOutputs
                    .Where(ro => ro != null && ro.WaitForCompletion && !string.IsNullOrWhiteSpace(ro.Key))
                    .Select(ro => ro.Key.Trim());
                return waitKeys.All(k => _completedKeys.Contains(k));
            }
        }

        public System.Threading.Tasks.Task<bool> GetWaitTaskForKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return System.Threading.Tasks.Task.FromResult(true);
            var trimmedKey = key.Trim();
            lock (Lock)
            {
                if (IsCollectionStopped || _completedKeys.Contains(trimmedKey))
                {
                    return System.Threading.Tasks.Task.FromResult(true);
                }

                var tcs = _keyTcsMap.GetOrAdd(trimmedKey, _ => new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously));
                if (IsCollectionStopped || _completedKeys.Contains(trimmedKey))
                {
                    tcs.TrySetResult(true);
                }
                return tcs.Task;
            }
        }

        public bool CheckAndSignalIfKeyCompleted(
            WebNode node,
            WebResponseOutput outputConfig,
            object? executionServiceObj = null,
            object? host = null)
        {
            if (outputConfig == null || string.IsNullOrWhiteSpace(outputConfig.Key)) return true;
            var trimmedKey = outputConfig.Key.Trim();

            lock (Lock)
            {
                if (IsCollectionStopped || _completedKeys.Contains(trimmedKey))
                {
                    return true;
                }

                string extractType = outputConfig.ExtractType?.Trim() ?? "Response";
                bool isResponseExtract = string.Equals(extractType, "Response", StringComparison.OrdinalIgnoreCase);

                // 1. Nếu kiểu trích xuất KHÔNG PHẢI "Response" (CurlCmd, Payload, Params, Headers...) hoặc là Mảng (IsList = true):
                //    Loại bỏ cơ chế 0ms, bắt buộc phải chờ cho tới khi hết TimeoutMs để gom đủ tất cả các request trong lượt chạy.
                if (!isResponseExtract || outputConfig.IsList)
                {
                    return false;
                }

                // 2. Nếu kiểu trích xuất là "Response" đơn lẻ (IsList = false && ExtractType == "Response"):
                //    Giữ nguyên cơ chế 0ms - hoàn thành ngay lập tức khi nhận được response body hợp lệ.
                bool hasValue = ResponseOutputValues.TryGetValue(trimmedKey, out var valStr) &&
                                !string.IsNullOrWhiteSpace(valStr) && valStr != "[]";
                if (hasValue)
                {
                    SignalKeyCompleted(trimmedKey);
                    return true;
                }
            }

            return false;
        }

        public void SignalKeyCompleted(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var trimmedKey = key.Trim();
            lock (Lock)
            {
                _completedKeys.Add(trimmedKey);
                var tcs = _keyTcsMap.GetOrAdd(trimmedKey, _ => new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously));
                tcs.TrySetResult(true);
            }
        }

        public void CancelDebounce()
        {
            lock (Lock)
            {
                try
                {
                    DebounceCts?.Cancel();
                    DebounceCts?.Dispose();
                }
                catch { }
                DebounceCts = null;
            }
        }

        public void ScheduleDebounceCompletion(int debounceMs = 300)
        {
            lock (Lock)
            {
                try
                {
                    DebounceCts?.Cancel();
                    DebounceCts?.Dispose();
                }
                catch { }

                var cts = new System.Threading.CancellationTokenSource();
                DebounceCts = cts;

                System.Threading.Tasks.Task.Delay(debounceMs, cts.Token).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && !cts.IsCancellationRequested)
                    {
                        lock (Lock)
                        {
                            if (OwnerNode?.ResponseOutputs != null)
                            {
                                foreach (var ro in OwnerNode.ResponseOutputs)
                                {
                                    if (ro != null && !string.IsNullOrWhiteSpace(ro.Key))
                                    {
                                        var k = ro.Key.Trim();
                                        int currentCount = 0;
                                        if (ResponseOutputLists.TryGetValue(k, out var list) && list != null)
                                        {
                                            currentCount = list.Count;
                                        }
                                        else if (OwnerNode.TryGetMasterResponseOutputList(k, out var mList) && mList != null)
                                        {
                                            currentCount = mList.Count;
                                        }
                                        else if (ResponseOutputValues.TryGetValue(k, out var val) && !string.IsNullOrWhiteSpace(val) && val != "[]")
                                        {
                                            currentCount = 1;
                                        }

                                        if (currentCount > 0)
                                        {
                                            SignalKeyCompleted(k);
                                        }
                                    }
                                }
                            }
                        }

                        var curTcs = PendingOutputsTcs;
                        if (curTcs != null && !curTcs.Task.IsCompleted)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WebNodeExecutionRun] ✓ Debounce idle period ({debounceMs}ms) passed for execution {ExecutionId}, completing PendingOutputsTcs.");
                            curTcs.TrySetResult(true);
                        }
                    }
                }, System.Threading.Tasks.TaskScheduler.Default);
            }
        }
    }
}
