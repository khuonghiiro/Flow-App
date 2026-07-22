using FlowMy.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using System.Linq;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Node Nhập dữ liệu - hiển thị popup HTML UI ở vị trí chuột tại runtime
    /// và hỗ trợ xem trước UI trên canvas.
    /// </summary>
    public sealed class ShowInputMsgNode : WorkflowNode, ISciterNode
    {
        private double _width = 450;
        private double _height = 245;
        private bool _isPreviewVisible = false;

        private List<CodeInputMapping> _inputMappings = new();
        private List<string> _outputKeys = new() { "result" };
        private List<HtmlOfflineAsset> _offlineAssets = new();
        private List<AsyncDataSource> _asyncDataSources = new();
        private bool _pendingAsyncDataPush = false;

        public static readonly string DefaultHtmlCode =
            "<div class=\"chat-input-container\">\n" +
            "    <div class=\"chat-header\">\n" +
            "        <span class=\"chat-title\">Nhập Dữ Liệu</span>\n" +
            "    </div>\n" +
            "    <div class=\"input-wrapper\">\n" +
            "        <textarea id=\"inputValue\" placeholder=\"Nhập nội dung... (Enter để gửi, Shift + Enter để xuống dòng)\"></textarea>\n" +
            "        <button id=\"btnSubmit\" type=\"button\" class=\"btn-send\" title=\"Gửi (Enter)\" onclick=\"hostSubmitAndClose()\">\n" +
            "            <svg width=\"18\" height=\"18\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\">\n" +
            "                <line x1=\"12\" y1=\"19\" x2=\"12\" y2=\"5\"></line>\n" +
            "                <polyline points=\"5 12 12 5 19 12\"></polyline>\n" +
            "            </svg>\n" +
            "        </button>\n" +
            "    </div>\n" +
            "</div>";

        public static readonly string DefaultJsCode =
            "document.on('click', '#btnSubmit', function(evt) {\n" +
            "    if (typeof hostSubmitAndClose === 'function') hostSubmitAndClose();\n" +
            "    else if (typeof hostSubmit === 'function') hostSubmit();\n" +
            "});\n" +
            "\n" +
            "document.on('keydown', '#inputValue', function(evt) {\n" +
            "    if ((evt.key === 'Enter' || evt.code === 'Enter' || evt.keyCode === 13) && !evt.shiftKey) {\n" +
            "        if (typeof evt.preventDefault === 'function') evt.preventDefault();\n" +
            "        if (typeof hostSubmitAndClose === 'function') hostSubmitAndClose();\n" +
            "        else if (typeof hostSubmit === 'function') hostSubmit();\n" +
            "        return true;\n" +
            "    }\n" +
            "});";

        public static readonly string DefaultCssCode =
            "@set dark-scrollbar {\n" +
            "    .thumb {\n" +
            "        background: #334155;\n" +
            "        border-radius: 4px;\n" +
            "    }\n" +
            "    .thumb:hover {\n" +
            "        background: #475569;\n" +
            "    }\n" +
            "    .base, .corner {\n" +
            "        background: transparent;\n" +
            "    }\n" +
            "    .prev, .next {\n" +
            "        display: none;\n" +
            "    }\n" +
            "}\n" +
            "* {\n" +
            "    box-sizing: border-box;\n" +
            "}\n" +
            "html, body {\n" +
            "    margin: 0;\n" +
            "    padding: 0;\n" +
            "    width: 100%;\n" +
            "    height: 100%;\n" +
            "    background-color: #0f172a;\n" +
            "    color: #f8fafc;\n" +
            "    font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;\n" +
            "    overflow: hidden;\n" +
            "}\n" +
            ".chat-input-container {\n" +
            "    display: flex;\n" +
            "    flex-direction: column;\n" +
            "    width: 100%;\n" +
            "    height: 100%;\n" +
            "    padding: 16px;\n" +
            "    background: #0f172a;\n" +
            "}\n" +
            ".chat-header {\n" +
            "    margin-bottom: 8px;\n" +
            "    display: flex;\n" +
            "    align-items: center;\n" +
            "}\n" +
            ".chat-title {\n" +
            "    font-size: 13px;\n" +
            "    font-weight: 600;\n" +
            "    color: #38bdf8;\n" +
            "    letter-spacing: 0.3px;\n" +
            "}\n" +
            ".input-wrapper {\n" +
            "    position: relative;\n" +
            "    flex: 1;\n" +
            "    display: flex;\n" +
            "    background: #1e293b;\n" +
            "    border: 1px solid #334155;\n" +
            "    border-radius: 12px;\n" +
            "    padding: 12px 50px 12px 14px;\n" +
            "    box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.2);\n" +
            "    transition: border-color 0.2s, box-shadow 0.2s;\n" +
            "}\n" +
            ".input-wrapper:focus-within {\n" +
            "    border-color: #38bdf8;\n" +
            "    box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.15);\n" +
            "}\n" +
            "textarea {\n" +
            "    width: 100%;\n" +
            "    height: 100%;\n" +
            "    background: transparent;\n" +
            "    border: none;\n" +
            "    outline: none;\n" +
            "    color: #f8fafc;\n" +
            "    caret-color: #38bdf8;\n" +
            "    font-family: inherit;\n" +
            "    font-size: 14px;\n" +
            "    line-height: 1.5;\n" +
            "    resize: none;\n" +
            "    vertical-scrollbar: \"dark-scrollbar\";\n" +
            "    overflow-y: auto;\n" +
            "}\n" +
            "textarea::placeholder {\n" +
            "    color: #64748b;\n" +
            "}\n" +
            ".btn-send {\n" +
            "    position: absolute;\n" +
            "    right: 10px;\n" +
            "    bottom: 10px;\n" +
            "    width: 32px;\n" +
            "    height: 32px;\n" +
            "    border-radius: 8px;\n" +
            "    border: none;\n" +
            "    background: #38bdf8;\n" +
            "    color: #0f172a;\n" +
            "    display: flex;\n" +
            "    align-items: center;\n" +
            "    justify-content: center;\n" +
            "    cursor: pointer;\n" +
            "    transition: all 0.2s ease;\n" +
            "    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);\n" +
            "}\n" +
            ".btn-send:hover {\n" +
            "    background: #0ea5e9;\n" +
            "    transform: scale(1.05);\n" +
            "}\n" +
            ".btn-send:active {\n" +
            "    transform: scale(0.95);\n" +
            "}\n" +
            "::-webkit-scrollbar {\n" +
            "    width: 6px;\n" +
            "    height: 6px;\n" +
            "}\n" +
            "::-webkit-scrollbar-track {\n" +
            "    background: transparent;\n" +
            "}\n" +
            "::-webkit-scrollbar-thumb {\n" +
            "    background: #334155;\n" +
            "    border-radius: 10px;\n" +
            "}\n" +
            "::-webkit-scrollbar-thumb:hover {\n" +
            "    background: #475569;\n" +
            "}";

        public static readonly string DefaultParamsCode = "result: #inputValue";

        private string _htmlCode = DefaultHtmlCode;
        private string _jsCode = DefaultJsCode;
        private string _cssCode = DefaultCssCode;
        private string _paramsCode = DefaultParamsCode;

        private bool _pendingReadDom = false;

        public const string OutputKey_JsonDynamic = "jsonDynamic";

        public ShowInputMsgNode()
        {
            Type = NodeType.ShowInputMsg;
            Title = "Nhập dữ liệu";
            ColorKey = "EspressoBrown";

            // IN/OUT Ports will be created by TemplateFactory
            _inputMappings.Add(new CodeInputMapping());
            RebuildDynamicOutputs();
        }

        public void EnsureUpgradedTemplate()
        {
            if (string.IsNullOrWhiteSpace(_htmlCode) ||
                _htmlCode.Contains("class=\"card\"", StringComparison.OrdinalIgnoreCase) ||
                _htmlCode.Contains("Nhập nội dung ở đây và nhấn Enter để xác nhận", StringComparison.OrdinalIgnoreCase) ||
                (!_htmlCode.Contains("onclick=\"hostSubmitAndClose()\"", StringComparison.OrdinalIgnoreCase) && !_htmlCode.Contains("onclick=\"hostSubmit()\"", StringComparison.OrdinalIgnoreCase)) ||
                !_jsCode.Contains("document.on(", StringComparison.OrdinalIgnoreCase))
            {
                _htmlCode = DefaultHtmlCode;
                _cssCode = DefaultCssCode;
                _jsCode = DefaultJsCode;
                _paramsCode = DefaultParamsCode;
                OnPropertyChanged(nameof(HtmlCode));
                OnPropertyChanged(nameof(CssCode));
                OnPropertyChanged(nameof(JsCode));
                OnPropertyChanged(nameof(ParamsCode));
            }
        }

        public string HtmlCode
        {
            get { EnsureUpgradedTemplate(); return _htmlCode; }
            set { if (_htmlCode != value) { _htmlCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string JsCode
        {
            get { EnsureUpgradedTemplate(); return _jsCode; }
            set { if (_jsCode != value) { _jsCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string CssCode
        {
            get { EnsureUpgradedTemplate(); return _cssCode; }
            set { if (_cssCode != value) { _cssCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string ParamsCode
        {
            get { EnsureUpgradedTemplate(); return _paramsCode; }
            set { if (_paramsCode != value) { _paramsCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public double Width
        {
            get => _width;
            set { if (Math.Abs(_width - value) > 0.01) { _width = Math.Max(100, value); OnPropertyChanged(); } }
        }

        public double Height
        {
            get => _height;
            set { if (Math.Abs(_height - value) > 0.01) { _height = Math.Max(100, value); OnPropertyChanged(); } }
        }

        public bool IsPreviewVisible
        {
            get => _isPreviewVisible;
            set { if (_isPreviewVisible != value) { _isPreviewVisible = value; OnPropertyChanged(); } }
        }

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

        [JsonIgnore]
        public System.Collections.Concurrent.ConcurrentDictionary<string, string> AsyncDataCache { get; set; } = new();

        [JsonIgnore]
        public System.Collections.Concurrent.ConcurrentQueue<(string SessionId, string Key, string Value)> AsyncDataReplayBuffer { get; } = new();

        [JsonIgnore]
        public System.Collections.Concurrent.ConcurrentQueue<(string SessionId, string Key, string Value)> PendingAsyncPushQueue { get; } = new();

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

        [JsonIgnore]
        public Dictionary<string, object?> ResolvedOutputs { get; set; } = new();

        [JsonIgnore]
        public bool PendingReadDom
        {
            get => _pendingReadDom;
            set { if (_pendingReadDom != value) { _pendingReadDom = value; OnPropertyChanged(); } }
        }

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
            }
        }

        public void RemoveOutputKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var k = key.Trim();
            if (_outputKeys.Contains(k))
            {
                _outputKeys.Remove(k);
                RebuildDynamicOutputs();
            }
        }

        public new TextBlock? TitleTextBlockUI { get; set; }
    }
}
