using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    public sealed class WebNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;
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
        private bool _enableSleepMode = true;
        private int _sleepIdleTimeoutValue = 5;
        private string _sleepIdleTimeoutUnit = "s"; // "ms" | "s" | "min" | "phút"
        private int _wakeRequestToken;

        // JS injection: danh sách (Node+Key) – khi node đó chạy đến Web thì chạy JS từ key đó trong WebView2
        private List<WebJsSourceMapping> _jsSources = new();

        // Runtime-only: pending JS to execute in WebView2
        private string? _pendingJavaScript;

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

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void RaisePropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));

        #region TitleDisplayMode / TitleColor

        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set { if (_titleDisplayMode != value) { _titleDisplayMode = value; OnPropertyChanged(); } }
        }

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
            // Xóa các output cũ do user thêm (IsUserAdded = true)
            var toRemove = DynamicOutputs.Where(o => o.IsUserAdded).ToList();
            foreach (var o in toRemove)
            {
                DynamicOutputs.Remove(o);
            }

            // Thêm lại các output từ ResponseOutputs
            foreach (var responseOutput in ResponseOutputs)
            {
                if (string.IsNullOrWhiteSpace(responseOutput.Key)) continue;
                
                // Kiểm tra xem output key đã tồn tại chưa (tránh trùng với cookie, bearer, access_token)
                if (DynamicOutputs.Any(o => string.Equals(o.Key, responseOutput.Key, StringComparison.OrdinalIgnoreCase)))
                    continue;

                DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = responseOutput.Key,
                    DisplayName = responseOutput.Key,
                    ConvertType = WorkflowDataType.String,
                    OutputType = WorkflowDataType.String,
                    IsUserAdded = true
                });
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

        /// <summary>Dictionary lưu response outputs theo key: key -> response body.</summary>
        [JsonIgnore]
        public Dictionary<string, string> ResponseOutputValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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

        #endregion
    }
}
