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
        private bool _enableSleepMode = true;
        private int _sleepIdleTimeoutValue = 5;
        private string _sleepIdleTimeoutUnit = "s"; // "ms" | "s" | "min" | "phút"
        private int _wakeRequestToken;

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
            "    <title>HTML UI - New Host API Demo</title>\n" +
            "</head>\n" +
            "<body>\n" +
            "    <div class=\"container\">\n" +
            "        <h2>HTML UI Demo - host* API</h2>\n" +
            "        <p class=\"hint\">Input mapping mẫu: <code>datas</code> hoặc <code>jobResult</code>. Trong JS có thể đọc bằng <code>window.hostLive</code> / <code>window.hostAsync</code>.</p>\n" +
            "\n" +
            "        <div class=\"block\">\n" +
            "            <label for=\"localImagePath\">Local image path:</label>\n" +
            "            <input id=\"localImagePath\" type=\"text\" placeholder=\"C:\\\\path\\\\image.png\" />\n" +
            "            <button id=\"btnResolveImage\">Resolve ảnh local</button>\n" +
            "            <img id=\"previewImage\" alt=\"preview image\" />\n" +
            "        </div>\n" +
            "\n" +
            "        <div class=\"block\">\n" +
            "            <label for=\"localVideoPath\">Local video path:</label>\n" +
            "            <input id=\"localVideoPath\" type=\"text\" placeholder=\"C:\\\\path\\\\video.mp4\" />\n" +
            "            <button id=\"btnResolveVideo\">Resolve video local</button>\n" +
            "            <video id=\"previewVideo\" controls playsinline preload=\"metadata\"></video>\n" +
            "        </div>\n" +
            "\n" +
            "        <div class=\"block\">\n" +
            "            <label for=\"txtResult\">Output result:</label>\n" +
            "            <input id=\"txtResult\" type=\"text\" placeholder=\"Giá trị sẽ submit ra output\" />\n" +
            "            <button id=\"btnSubmit\">hostSubmit()</button>\n" +
            "            <button id=\"btnRun\" class=\"btn-start\">hostStart()</button>\n" +
            "        </div>\n" +
            "\n" +
            "        <pre id=\"logBox\">[log]</pre>\n" +
            "    </div>\n" +
            "</body>\n" +
            "</html>";

        // JS mẫu: có thể thêm logic xử lý UI, validate, ... Khi muốn submit thì gọi hostSubmit()
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
            "// New API demo: hostLive / hostAsync / hostResolvePath / hostSubmit / hostStart\n" +
            "// HTML id cần có: localImagePath, localVideoPath, previewImage, previewVideo, txtResult, logBox\n" +
            "(function(){\n" +
            "    var reqImage = '';\n" +
            "    var reqVideo = '';\n" +
            "    var logBox = document.getElementById('logBox');\n" +
            "\n" +
            "    function log(msg) {\n" +
            "        console.log(msg);\n" +
            "        if (logBox) {\n" +
            "            logBox.textContent = (logBox.textContent || '') + '\\n' + msg;\n" +
            "        }\n" +
            "    }\n" +
            "\n" +
            "    function resolveLocalPath(localPath, requestId) {\n" +
            "        if (!localPath) {\n" +
            "            log('[warn] localPath rỗng');\n" +
            "            return;\n" +
            "        }\n" +
            "        if (typeof hostResolvePath !== 'function') {\n" +
            "            log('[warn] hostResolvePath chưa sẵn sàng');\n" +
            "            return;\n" +
            "        }\n" +
            "        hostResolvePath(localPath, requestId);\n" +
            "        log('[call] hostResolvePath: ' + localPath);\n" +
            "    }\n" +
            "\n" +
            "    window.addEventListener('hostPathResolved', function(ev){\n" +
            "        var d = (ev && ev.detail) || {};\n" +
            "        if (!d.ok || !d.localUrl) return;\n" +
            "\n" +
            "        if (d.requestId === reqImage) {\n" +
            "            var img = document.getElementById('previewImage');\n" +
            "            if (img) img.src = d.localUrl;\n" +
            "            log('[ok] image localUrl: ' + d.localUrl);\n" +
            "        }\n" +
            "\n" +
            "        if (d.requestId === reqVideo) {\n" +
            "            var vid = document.getElementById('previewVideo');\n" +
            "            if (vid) {\n" +
            "                vid.src = d.localUrl;\n" +
            "                try { vid.load(); } catch (_) {}\n" +
            "            }\n" +
            "            log('[ok] video localUrl: ' + d.localUrl);\n" +
            "        }\n" +
            "    });\n" +
            "\n" +
            "    if (window.hostLive && typeof window.hostLive.on === 'function') {\n" +
            "        window.hostLive.on('datas', function(datas){\n" +
            "            log('[live] datas updated');\n" +
            "            try {\n" +
            "                var obj = (typeof datas === 'string') ? JSON.parse(datas) : datas;\n" +
            "                if (obj && obj.localImagePath && !document.getElementById('localImagePath').value) {\n" +
            "                    document.getElementById('localImagePath').value = String(obj.localImagePath);\n" +
            "                }\n" +
            "                if (obj && obj.localVideoPath && !document.getElementById('localVideoPath').value) {\n" +
            "                    document.getElementById('localVideoPath').value = String(obj.localVideoPath);\n" +
            "                }\n" +
            "            } catch (_) {}\n" +
            "        });\n" +
            "    }\n" +
            "\n" +
            "    if (window.hostAsync && typeof window.hostAsync.on === 'function') {\n" +
            "        window.hostAsync.on('jobResult', function(v){\n" +
            "            log('[async] jobResult: ' + JSON.stringify(v));\n" +
            "        });\n" +
            "    }\n" +
            "\n" +
            "    var btnResolveImage = document.getElementById('btnResolveImage');\n" +
            "    if (btnResolveImage) {\n" +
            "        btnResolveImage.addEventListener('click', function(){\n" +
            "            reqImage = 'img_' + Date.now();\n" +
            "            var p = (document.getElementById('localImagePath').value || '').trim();\n" +
            "            resolveLocalPath(p, reqImage);\n" +
            "        });\n" +
            "    }\n" +
            "\n" +
            "    var btnResolveVideo = document.getElementById('btnResolveVideo');\n" +
            "    if (btnResolveVideo) {\n" +
            "        btnResolveVideo.addEventListener('click', function(){\n" +
            "            reqVideo = 'vid_' + Date.now();\n" +
            "            var p = (document.getElementById('localVideoPath').value || '').trim();\n" +
            "            resolveLocalPath(p, reqVideo);\n" +
            "        });\n" +
            "    }\n" +
            "\n" +
            "    var btnSubmit = document.getElementById('btnSubmit');\n" +
            "    if (btnSubmit) {\n" +
            "        btnSubmit.addEventListener('click', function(){\n" +
            "            if (typeof hostSubmit === 'function') hostSubmit();\n" +
            "            log('[call] hostSubmit');\n" +
            "        });\n" +
            "    }\n" +
            "\n" +
            "    var btnRun = document.getElementById('btnRun');\n" +
            "    if (btnRun) {\n" +
            "        btnRun.addEventListener('click', function(){\n" +
            "            if (typeof hostStart === 'function') hostStart();\n" +
            "            log('[call] hostStart');\n" +
            "        });\n" +
            "    }\n" +
            "})();\n";

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
            "/* CSS mẫu cho demo host API mới */\n" +
            "/* Có thể dùng variable mapping, ví dụ: .container { border-color: {themeColor}; } */\n" +
            "body {\n" +
            "    margin: 0;\n" +
            "    background: #0b1220;\n" +
            "    color: #e2e8f0;\n" +
            "    font-family: system-ui, -apple-system, Segoe UI, sans-serif;\n" +
            "}\n" +
            ".container {\n" +
            "    max-width: 920px;\n" +
            "    margin: 16px auto;\n" +
            "    padding: 16px;\n" +
            "}\n" +
            ".block {\n" +
            "    border: 1px solid #334155;\n" +
            "    border-radius: 10px;\n" +
            "    padding: 12px;\n" +
            "    margin-bottom: 12px;\n" +
            "    background: #111b2f;\n" +
            "}\n" +
            "label {\n" +
            "    display: block;\n" +
            "    margin-bottom: 6px;\n" +
            "    font-weight: 600;\n" +
            "}\n" +
            "input[type=\"text\"] {\n" +
            "    width: 100%;\n" +
            "    height: 34px;\n" +
            "    border: 1px solid #475569;\n" +
            "    border-radius: 8px;\n" +
            "    background: #0f172a;\n" +
            "    color: #e2e8f0;\n" +
            "    padding: 0 10px;\n" +
            "    margin-bottom: 8px;\n" +
            "}\n" +
            "button {\n" +
            "    height: 32px;\n" +
            "    padding: 0 12px;\n" +
            "    border: none;\n" +
            "    border-radius: 8px;\n" +
            "    background: #2563eb;\n" +
            "    color: #fff;\n" +
            "    margin-right: 6px;\n" +
            "    cursor: pointer;\n" +
            "}\n" +
            "button:hover { background: #1d4ed8; }\n" +
            ".btn-start { background: #059669; }\n" +
            ".btn-start:hover { background: #047857; }\n" +
            "#previewImage {\n" +
            "    display: block;\n" +
            "    width: 100%;\n" +
            "    max-height: 220px;\n" +
            "    object-fit: contain;\n" +
            "    border: 1px solid #334155;\n" +
            "    border-radius: 8px;\n" +
            "    margin-top: 8px;\n" +
            "    background: #020617;\n" +
            "}\n" +
            "#previewVideo {\n" +
            "    display: block;\n" +
            "    width: 100%;\n" +
            "    max-height: 260px;\n" +
            "    margin-top: 8px;\n" +
            "    border: 1px solid #334155;\n" +
            "    border-radius: 8px;\n" +
            "    background: #000;\n" +
            "}\n" +
            "#logBox {\n" +
            "    min-height: 120px;\n" +
            "    border: 1px solid #334155;\n" +
            "    border-radius: 8px;\n" +
            "    padding: 10px;\n" +
            "    background: #020617;\n" +
            "    color: #93c5fd;\n" +
            "    white-space: pre-wrap;\n" +
            "}\n" +
            ".hint {\n" +
            "    color: #94a3b8;\n" +
            "    margin: 6px 0 12px;\n" +
            "}\n";

        // Cấu hình params: mỗi dòng = "key: selector" (ví dụ: "result: #txtResult")
        // Selector dùng CSS selector: #id, .class, tag, [attr], ...
        // Khi gọi hostSubmit() hoặc postMessage({ type: 'submit' }), host sẽ đọc DOM theo Params này
        private string _paramsCode =
            "// Output mặc định khi gọi hostSubmit()\n" +
            "// result: text user nhập để gửi ra node output\n" +
            "result: #txtResult\n" +
            "\n" +
            "// localImagePath/localVideoPath: lưu lại path user nhập\n" +
            "// (giúp node sau dùng tiếp nếu cần)\n" +
            "localImagePath: #localImagePath\n" +
            "localVideoPath: #localVideoPath\n";

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
        /// Nếu bật, HTML UI node sẽ nghỉ khi không có flow/data chạy vào để giảm tải tài nguyên.
        /// Khi có tín hiệu mới, control sẽ tự đánh thức lại runtime.
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
        /// và push vào WebView2 qua window.hostAsync.
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

        /// <summary>
        /// Runtime replay buffer: lưu lịch sử async push theo thứ tự nhận để F5/Ctrl+R có thể phát lại đầy đủ.
        /// Không serialize vào workflow JSON.
        /// </summary>
        [JsonIgnore]
        public System.Collections.Concurrent.ConcurrentQueue<(string SessionId, string Key, string Value)> AsyncDataReplayBuffer { get; } = new();

        /// <summary>Thread-safe queue cho async data push — mỗi iteration enqueue 1 item, UI handler drain tất cả.</summary>
        [JsonIgnore]
        public System.Collections.Concurrent.ConcurrentQueue<(string SessionId, string Key, string Value)> PendingAsyncPushQueue { get; } = new();

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

        /// <summary>
        /// Runtime-only token để yêu cầu UI đánh thức WebView2.
        /// Mỗi lần RequestWake() được gọi, token tăng lên để luôn fire PropertyChanged.
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

