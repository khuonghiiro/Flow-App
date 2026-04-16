using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using FlowMy.Models;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Node hiển thị HTML UI được cấu hình từ 4 phần: Html/Js/Css/Params.
    /// Inputs lấy value từ các node khác (Node + Key), outputs động giống CodeNode (dựa trên OutputKeys).
    /// </summary>
    public sealed class HtmlUiNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;
        private double _width = 420;
        private double _height = 320;

        // Zoom CSS riêng cho node HTML UI này (không theo domain)
        private double _cssZoom; // 0 = chưa cấu hình, >0 = zoom cụ thể

        // Khi true: mỗi lần đóng dialog HTML UI sẽ tự reload lại WebView2 (HTML/CSS/JS),
        // khi false: user tự F5/Ctrl+R trong WebView2 để reload.
        private bool _autoReloadOnDialogClose = false;
        private bool _isViewportExpanded;

        // Dual-tab: khi true node hiển thị 2 tab (Tab1=Web, Tab2=HTML UI)
        private bool _useWebTab = false;

        // URL hiện tại của Tab1 (persist để load lại sau khi mở workflow)
        private string? _webTabUrl;

        // Cookie source: lấy giá trị cookie từ 1 node nguồn (thay vì nhập tay)
        private string? _webTabCookieSourceNodeId;
        private string? _webTabCookieSourceOutputKey;

        // Auto-refresh: tự động re-load cookie từ source node theo kỳ (giống auto-push JS)
        private bool _webTabAutoRefreshEnabled = false;
        private int _webTabAutoRefreshInterval = 5000;
        private string _webTabAutoRefreshUnit = "ms";

        // ✅ Danh sách thư viện JS/CSS offline — được inject vào HTML khi IsEnabled=true
        private List<HtmlOfflineAsset> _offlineAssets = new();

        // ✅ Danh sách nguồn dữ liệu async (API-style push từ AsyncTask branches)
        private List<AsyncDataSource> _asyncDataSources = new();

        private List<CodeInputMapping> _inputMappings = new();
        private List<string> _outputKeys = new() { "result" };

        // HTML mẫu hướng dẫn: có 1 ô input và 1 nút gửi để set output key 'result'
        // 
        // ✅ HƯỚNG DẪN TRUYỀN VALUE TỪ NODE VÀO HTML/CSS/JS:
        // Bạn có thể dùng {variableName} trong HTML, CSS, JS để truyền giá trị từ input mappings.
        // 
        // Cách làm:
        // 1) Trong dialog, tab "Input", cấu hình Node nguồn + Key (ví dụ: node HttpRequest, key responseBody)
        // 2) "Biến trong code" = tên biến bạn muốn dùng (ví dụ: apiResponse, hoặc để trống = dùng key)
        // 3) Trong HTML/CSS/JS, dùng {variableName} để thay thế bằng giá trị thực tế
        // 
        // Ví dụ:
        // - Input mapping: Node=HttpRequest, Key=responseBody, Biến=apiResponse
        // - Trong HTML: <div>Kết quả: {apiResponse}</div>
        // - Trong JS: const data = JSON.parse('{apiResponse}');
        // - Trong CSS: .title::before { content: '{apiResponse}'; }
        // 
        // Lưu ý:
        // - {variableName} sẽ được replace bằng giá trị string từ node nguồn
        // - Nếu giá trị là JSON, bạn có thể dùng JSON.parse() trong JS để parse
        // - Nếu giá trị chứa ký tự đặc biệt HTML, bạn nên escape hoặc dùng textContent thay vì innerHTML
        // - Nếu không tìm thấy variable, {variableName} sẽ giữ nguyên (không replace)
        //
        private string _htmlCode =
            "<!DOCTYPE html>\n" +
            "<html>\n" +
            "<head>\n" +
            "    <meta charset=\"UTF-8\">\n" +
            "    <title>HTML UI – ví dụ output 'result'</title>\n" +
            "</head>\n" +
            "<body>\n" +
            "    <div class=\"container\">\n" +
            "        <h1>HTML UI Node – ví dụ output 'result'</h1>\n" +
            "        <!-- Ví dụ dùng {variableName}: <p>Giá trị từ node: {input}</p> -->\n" +
            "        <p>Nhập giá trị cho output key <strong>result</strong> rồi bấm Gửi.</p>\n" +
            "        <label for=\"txtResult\">Giá trị output 'result':</label>\n" +
            "        <input id=\"txtResult\" type=\"text\" placeholder=\"Nhập giá trị...\" />\n" +
            "        <button onclick=\"acSubmit()\">Gửi</button>\n" +
            "        <p class=\"hint\">Sau khi bấm Gửi, node sẽ đọc giá trị từ <code>#txtResult</code> theo cấu hình Params\n" +
            "        (ví dụ: <code>result: #txtResult</code>) và đẩy ra output key <strong>result</strong>.</p>\n" +
            "        <hr style=\"margin: 20px 0; border-color: #374151;\" />\n" +
            "        <p><strong>Ví dụ dùng JS:</strong></p>\n" +
            "        <button onclick=\"setUpperResult()\">Viết HOA & Gửi</button>\n" +
            "        <button onclick=\"sendResultDirect()\">Gửi trực tiếp (không dùng Params)</button>\n" +
            "        <p class=\"hint\">Các hàm <code>setUpperResult()</code> và <code>sendResultDirect()</code> được định nghĩa trong tab JS.</p>\n" +
            "        <hr style=\"margin: 20px 0; border-color: #374151;\" />\n" +
            "        <p><strong>Chạy Workflow:</strong></p>\n" +
            "        <button onclick=\"acStartWorkflow()\" class=\"btn-start\">▶ Chạy Workflow</button>\n" +
            "        <p class=\"hint\">Nút này sẽ kích hoạt chạy workflow (tương đương nhấn nút Start trong editor).</p>\n" +
            "    </div>\n" +
            "</body>\n" +
            "</html>";

        // JS mẫu: có thể thêm logic xử lý UI, validate, ... Khi muốn submit thì gọi acSubmit()
        // 
        // ✅ HƯỚNG DẪN DÙNG {variableName} TRONG JS:
        // Bạn có thể dùng {variableName} để truyền giá trị từ input mappings vào JavaScript.
        // 
        // Ví dụ:
        // - Input mapping: Node=HttpRequest, Key=responseBody, Biến=apiResponse
        // - Trong JS: const data = JSON.parse('{apiResponse}');
        // - Hoặc: const url = 'https://api.example.com/data?id={userId}';
        // 
        // Lưu ý:
        // - {variableName} sẽ được replace bằng giá trị string từ node nguồn
        // - Nếu giá trị là JSON, dùng JSON.parse('{variableName}') để parse
        // - Nếu giá trị là string thông thường, dùng trực tiếp: const text = '{variableName}';
        // - Nếu giá trị chứa dấu nháy đơn/kép, cần escape hoặc dùng template literal
        // - Nếu không tìm thấy variable, {variableName} sẽ giữ nguyên (không replace)
        //
        private string _jsCode =
            "// JS tab – bạn có thể thêm logic xử lý UI, validate, thay đổi DOM, ...\n" +
            "// Khi muốn 'submit' giá trị ra outputs, gọi hàm acSubmit() (đã được inject tự động).\n" +
            "// Ví dụ: sau khi validate xong, gọi acSubmit() để host đọc DOM theo Params.\n" +
            "//\n" +
            "// Để chạy workflow từ button trong HTML UI, gọi acStartWorkflow():\n" +
            "// <button onclick=\"acStartWorkflow()\">Chạy Workflow</button>\n" +
            "//\n" +
            "// ✅ Ví dụ dùng {variableName} từ input mappings:\n" +
            "// const apiData = JSON.parse('{apiResponse}'); // apiResponse là biến từ input mapping\n" +
            "// const userId = '{userId}'; // userId là biến từ input mapping\n" +
            "//\n" +
            "console.log('HTML UI loaded');\n" +
            "\n" +
            "// Ví dụ 1: Hàm xử lý và gửi qua acSubmit() (dùng Params để map)\n" +
            "function setUpperResult() {\n" +
            "    const el = document.getElementById('txtResult');\n" +
            "    if (!el) return;\n" +
            "    \n" +
            "    // Xử lý: viết hoa giá trị\n" +
            "    el.value = (el.value || '').toUpperCase();\n" +
            "    \n" +
            "    // Gửi qua acSubmit() - host sẽ đọc DOM theo Params (ví dụ: result: #txtResult)\n" +
            "    acSubmit();\n" +
            "}\n" +
            "\n" +
            "// Ví dụ 2: Gửi output trực tiếp từ JS (không cần Params)\n" +
            "function sendResultDirect() {\n" +
            "    const el = document.getElementById('txtResult');\n" +
            "    const value = el ? el.value : '';\n" +
            "    \n" +
            "    // Gửi trực tiếp qua postMessage - các key trong object sẽ thành outputs\n" +
            "    if (window.chrome && window.chrome.webview) {\n" +
            "        window.chrome.webview.postMessage({\n" +
            "            result: value,\n" +
            "            timestamp: new Date().toISOString()\n" +
            "        });\n" +
            "    }\n" +
            "}\n" +
            "\n" +
            "// Ví dụ 3: Tự bind event khi DOM load xong (không cần onclick trong HTML)\n" +
            "document.addEventListener('DOMContentLoaded', function() {\n" +
            "    // Có thể thêm logic tự động bind events, validate, ...\n" +
            "    console.log('DOM ready, có thể thêm event listeners');\n" +
            "});\n";

        // CSS mẫu cho giao diện cơ bản
        // 
        // ✅ HƯỚNG DẪN DÙNG {variableName} TRONG CSS:
        // Bạn có thể dùng {variableName} để truyền giá trị từ input mappings vào CSS.
        // 
        // Ví dụ:
        // - Input mapping: Node=HttpRequest, Key=themeColor, Biến=primaryColor
        // - Trong CSS: .button { background-color: {primaryColor}; }
        // - Hoặc: body { font-size: {fontSize}px; }
        // 
        // Lưu ý:
        // - {variableName} sẽ được replace bằng giá trị string từ node nguồn
        // - Giá trị sẽ được inject trực tiếp vào CSS, nên cần đảm bảo giá trị hợp lệ
        // - Nếu giá trị là số, có thể dùng trực tiếp: width: {width}px;
        // - Nếu giá trị là màu, đảm bảo format đúng: #RRGGBB hoặc rgb(r,g,b)
        // - Nếu không tìm thấy variable, {variableName} sẽ giữ nguyên (không replace)
        //
        private string _cssCode =
            "/* CSS tab – style đơn giản cho ví dụ HTML UI */\n" +
            "/* ✅ Ví dụ dùng {variableName}: .title { color: {titleColor}; } */\n" +
            "body {\n" +
            "    font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;\n" +
            "    margin: 0;\n" +
            "    padding: 0;\n" +
            "    background: #0f172a;\n" +
            "    color: #e5e7eb;\n" +
            "}\n" +
            ".container {\n" +
            "    padding: 20px;\n" +
            "}\n" +
            "input[type=\"text\"] {\n" +
            "    padding: 6px 8px;\n" +
            "    margin-right: 8px;\n" +
            "    min-width: 220px;\n" +
            "}\n" +
            "button {\n" +
            "    padding: 6px 12px;\n" +
            "}\n" +
            ".btn-start {\n" +
            "    background: #10b981;\n" +
            "    color: white;\n" +
            "    border: none;\n" +
            "    cursor: pointer;\n" +
            "    font-weight: 500;\n" +
            "}\n" +
            ".btn-start:hover {\n" +
            "    background: #059669;\n" +
            "}\n" +
            ".hint {\n" +
            "    margin-top: 10px;\n" +
            "    font-size: 12px;\n" +
            "    color: #9ca3af;\n" +
            "}\n";

        // Cấu hình params: mỗi dòng = "key: selector" (ví dụ: "result: #txtResult")
        // Selector dùng CSS selector: #id, .class, tag, [attr], ...
        // Khi gọi acSubmit() hoặc postMessage({ type: 'submit' }), host sẽ đọc DOM theo Params này
        private string _paramsCode =
            "// Output key 'result' lấy từ textbox có id=txtResult\n" +
            "result: #txtResult\n";

        public HtmlUiNode()
        {
            Type = NodeType.HtmlUi;
            Title = "HTML UI";
            ColorKey = "EspressoBrown";

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

            _inputMappings.Add(new CodeInputMapping());
            RebuildDynamicOutputs();
        }

        /// <summary>Danh sách input: mỗi phần tử = một nguồn (node + key) → một biến trong HTML/JS.</summary>
        public List<CodeInputMapping> InputMappings
        {
            get => _inputMappings;
            set
            {
                if (_inputMappings != value)
                {
                    _inputMappings = value ?? new List<CodeInputMapping>();
                    if (_inputMappings.Count == 0)
                        _inputMappings.Add(new CodeInputMapping());
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string? SourceNodeId
        {
            get => _inputMappings.Count > 0 ? _inputMappings[0].SourceNodeId : null;
            set { if (_inputMappings.Count > 0) _inputMappings[0].SourceNodeId = value; }
        }

        [JsonIgnore]
        public string? SourceOutputKey
        {
            get => _inputMappings.Count > 0 ? _inputMappings[0].SourceOutputKey : null;
            set { if (_inputMappings.Count > 0) _inputMappings[0].SourceOutputKey = value; }
        }

        [JsonIgnore]
        public string? InputKeyOverride
        {
            get => _inputMappings.Count > 0 ? _inputMappings[0].InputKeyOverride : null;
            set { if (_inputMappings.Count > 0) _inputMappings[0].InputKeyOverride = value; }
        }

        [JsonIgnore]
        public string EffectiveInputKey => _inputMappings.Count > 0 ? _inputMappings[0].EffectiveInputKey : "input";

        /// <summary>HTML chính để render UI.</summary>
        public string HtmlCode
        {
            get => _htmlCode;
            set { if (_htmlCode != value) { _htmlCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>JavaScript chạy trong HTML UI.</summary>
        public string JsCode
        {
            get => _jsCode;
            set { if (_jsCode != value) { _jsCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>CSS cho HTML UI.</summary>
        public string CssCode
        {
            get => _cssCode;
            set { if (_cssCode != value) { _cssCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Phần cấu hình params (inputs/outputs) – dạng text/JSON/JS, do người dùng tự quy ước.</summary>
        public string ParamsCode
        {
            get => _paramsCode;
            set { if (_paramsCode != value) { _paramsCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Danh sách tên output key (đồng bộ với DynamicOutputs).</summary>
        public List<string> OutputKeys
        {
            get => _outputKeys;
            set
            {
                if (_outputKeys != value)
                {
                    _outputKeys = value ?? new List<string>();
                    OnPropertyChanged();
                    RebuildDynamicOutputs();
                }
            }
        }

        /// <summary>Runtime: giá trị output (không serialize).</summary>
        [JsonIgnore]
        public Dictionary<string, object?> ResolvedOutputs { get; set; } = new();

        /// <summary>
        /// Flag để executor trigger đọc DOM từ WebView2.
        /// HtmlUiNodeControl sẽ listen PropertyChanged và đọc DOM khi thấy flag này = true, sau đó reset về false.
        /// </summary>
        private bool _pendingReadDom = false;

        [JsonIgnore]
        public bool PendingReadDom
        {
            get => _pendingReadDom;
            set
            {
                if (_pendingReadDom != value)
                {
                    _pendingReadDom = value;
                    OnPropertyChanged();
                }
            }
        }

        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set { if (_titleDisplayMode != value) { _titleDisplayMode = value; OnPropertyChanged(); } }
        }

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

        /// <summary>Chiều rộng của node (dùng cho WebView2).</summary>
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

        /// <summary>Chiều cao của node (dùng cho WebView2).</summary>
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
        /// Zoom CSS đã lưu riêng cho node HTML UI này.
        /// 0 = chưa cấu hình, >0 = giá trị zoom cụ thể (ví dụ 1.0, 1.25, 0.8...).
        /// Được lưu khi Ctrl+S và apply lại khi load workflow.
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
        /// Nếu true: dialog HTML UI khi đóng sẽ yêu cầu reload lại nội dung WebView2 cho node này.
        /// Nếu false: không reload, user tự F5/Ctrl+R trong WebView2.
        /// </summary>
        public bool AutoReloadOnDialogClose
        {
            get => _autoReloadOnDialogClose;
            set
            {
                if (_autoReloadOnDialogClose != value)
                {
                    _autoReloadOnDialogClose = value;
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

        /// <summary>
        /// Khi true: node hiển thị 2 tab — Tab1 (Web Browser) + Tab2 (HTML UI logic hiện tại).
        /// Khi false (mặc định): chỉ hiển thị HTML UI như cũ.
        /// </summary>
        public bool UseWebTab
        {
            get => _useWebTab;
            set
            {
                if (_useWebTab != value)
                {
                    _useWebTab = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// URL đang được load trong Tab1 (Web Browser). Được persist để load lại sau khi mở workflow.
        /// </summary>
        public string? WebTabUrl
        {
            get => _webTabUrl;
            set
            {
                if (_webTabUrl != value)
                {
                    _webTabUrl = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Cookie text để load vào Tab1 khi đóng dialog. Runtime only – KHÔNG serialize.</summary>
        [JsonIgnore]
        public string? PendingCookieText
        {
            get => _pendingCookieText;
            set { if (_pendingCookieText != value) { _pendingCookieText = value; OnPropertyChanged(); } }
        }
        private string? _pendingCookieText;

        /// <summary>NodeId của node nguồn cung cấp cookie text cho Tab1 (persist).</summary>
        public string? WebTabCookieSourceNodeId
        {
            get => _webTabCookieSourceNodeId;
            set { if (_webTabCookieSourceNodeId != value) { _webTabCookieSourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>Output key của node nguồn cung cấp cookie text cho Tab1 (persist).</summary>
        public string? WebTabCookieSourceOutputKey
        {
            get => _webTabCookieSourceOutputKey;
            set { if (_webTabCookieSourceOutputKey != value) { _webTabCookieSourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>Bật auto-refresh: tự động lấy cookie từ source node theo kỳ và load vào Tab1.</summary>
        public bool WebTabAutoRefreshEnabled
        {
            get => _webTabAutoRefreshEnabled;
            set { if (_webTabAutoRefreshEnabled != value) { _webTabAutoRefreshEnabled = value; OnPropertyChanged(); } }
        }

        /// <summary>Khoảng thời gian auto-refresh Tab1 (ms / s / min tùy WebTabAutoRefreshUnit).</summary>
        public int WebTabAutoRefreshInterval
        {
            get => _webTabAutoRefreshInterval;
            set { if (_webTabAutoRefreshInterval != value) { _webTabAutoRefreshInterval = value; OnPropertyChanged(); } }
        }

        /// <summary>Đơn vị thời gian: "ms", "s", "min".</summary>
        public string WebTabAutoRefreshUnit
        {
            get => _webTabAutoRefreshUnit;
            set { if (_webTabAutoRefreshUnit != value) { _webTabAutoRefreshUnit = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Danh sách thư viện JS/CSS offline.
        /// Khi IsEnabled=true: file sẽ được inline vào HTML content khi render.
        /// </summary>
        public List<HtmlOfflineAsset> OfflineAssets
        {
            get => _offlineAssets;
            set
            {
                if (_offlineAssets != value)
                {
                    _offlineAssets = value ?? new List<HtmlOfflineAsset>();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Danh sách nguồn dữ liệu async: mỗi phần tử = 1 node nguồn + key nguồn + key nhận trong HTML.
        /// Khi workflow chạy vào node này, executor sẽ resolve data từ các nguồn này
        /// và push vào WebView2 qua window.__acAsync.
        /// Khi F5/reload, dữ liệu được load lại từ cache.
        /// </summary>
        public List<AsyncDataSource> AsyncDataSources
        {
            get => _asyncDataSources;
            set
            {
                if (_asyncDataSources != value)
                {
                    _asyncDataSources = value ?? new List<AsyncDataSource>();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Runtime cache: dữ liệu đã nhận từ async sources (không serialize).</summary>
        [JsonIgnore]
        public System.Collections.Concurrent.ConcurrentDictionary<string, string> AsyncDataCache { get; set; } = new();

        /// <summary>Thread-safe queue cho async data push — mỗi iteration enqueue 1 item, UI handler drain tất cả.</summary>
        [JsonIgnore]
        public System.Collections.Concurrent.ConcurrentQueue<(string Key, string Value)> PendingAsyncPushQueue { get; } = new();

        /// <summary>
        /// Flag để executor trigger push async data vào WebView2.
        /// HtmlUiNodeControl sẽ listen PropertyChanged và push khi thấy flag này = true, sau đó reset về false.
        /// </summary>
        private bool _pendingAsyncDataPush = false;

        [JsonIgnore]
        public bool PendingAsyncDataPush
        {
            get => _pendingAsyncDataPush;
            set
            {
                _pendingAsyncDataPush = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Reference đến TextBlock hiển thị title trên canvas (được tạo trong HtmlUiNodeControl).</summary>
        public new TextBlock? TitleTextBlockUI { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));

        public void RebuildDynamicOutputs()
        {
            DynamicOutputs.Clear();
            foreach (var key in _outputKeys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = key.Trim(),
                    DisplayName = key.Trim(),
                    OutputType = WorkflowDataType.String,
                    IsUserAdded = true
                });
            }
        }

        public void AddOutputKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var k = key.Trim();
            if (!_outputKeys.Contains(k))
            {
                _outputKeys.Add(k);
                RebuildDynamicOutputs();
                OnPropertyChanged(nameof(OutputKeys));
            }
        }

        public void RemoveOutputKeyAt(int index)
        {
            if (index >= 0 && index < _outputKeys.Count)
            {
                _outputKeys.RemoveAt(index);
                RebuildDynamicOutputs();
                OnPropertyChanged(nameof(OutputKeys));
            }
        }
    }
}

