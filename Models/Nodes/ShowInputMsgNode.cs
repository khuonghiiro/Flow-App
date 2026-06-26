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
    public sealed class ShowInputMsgNode : WorkflowNode
    {
        private double _width = 450;
        private double _height = 350;
        private bool _isPreviewVisible = false;

        private List<CodeInputMapping> _inputMappings = new();
        private List<string> _outputKeys = new() { "result" };
        private List<HtmlOfflineAsset> _offlineAssets = new();
        private List<AsyncDataSource> _asyncDataSources = new();
        private bool _pendingAsyncDataPush = false;

        private string _htmlCode =
            "<!DOCTYPE html>\n" +
            "<html>\n" +
            "<head>\n" +
            "    <meta charset=\"UTF-8\">\n" +
            "    <title>Nhập Dữ Liệu</title>\n" +
            "</head>\n" +
            "<body>\n" +
            "    <div class=\"card\">\n" +
            "        <h3>Nhập Dữ Liệu</h3>\n" +
            "        <p class=\"description\">Vui lòng điền thông tin bên dưới và bấm Xác nhận.</p>\n" +
            "        <div class=\"form-group\">\n" +
            "            <label for=\"inputValue\">Thông tin cần nhập:</label>\n" +
            "            <input type=\"text\" id=\"inputValue\" placeholder=\"Nhập nội dung ở đây...\" />\n" +
            "        </div>\n" +
            "        <div class=\"actions\">\n" +
            "            <button id=\"btnSubmit\">Xác nhận</button>\n" +
            "        </div>\n" +
            "    </div>\n" +
            "</body>\n" +
            "</html>";

        private string _jsCode =
            "(function() {\n" +
            "    var btnSubmit = document.getElementById('btnSubmit');\n" +
            "    if (btnSubmit) {\n" +
            "        btnSubmit.addEventListener('click', function() {\n" +
            "            if (typeof hostSubmit === 'function') {\n" +
            "                hostSubmit();\n" +
            "            }\n" +
            "        });\n" +
            "    }\n" +
            "})();";

        private string _cssCode =
            "body {\n" +
            "    margin: 0;\n" +
            "    padding: 20px;\n" +
            "    font-family: system-ui, -apple-system, sans-serif;\n" +
            "    background: #0f172a;\n" +
            "    color: #f8fafc;\n" +
            "}\n" +
            ".card {\n" +
            "    background: #1e293b;\n" +
            "    border: 1px solid #334155;\n" +
            "    border-radius: 12px;\n" +
            "    padding: 20px;\n" +
            "    box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.3);\n" +
            "}\n" +
            "h3 { margin-top: 0; color: #38bdf8; }\n" +
            ".description { font-size: 13px; color: #94a3b8; margin-bottom: 16px; }\n" +
            ".form-group { margin-bottom: 16px; }\n" +
            "label { display: block; margin-bottom: 6px; font-size: 14px; font-weight: 500; }\n" +
            "input[type=\"text\"] {\n" +
            "    width: 100%;\n" +
            "    height: 36px;\n" +
            "    padding: 0 10px;\n" +
            "    background: #0f172a;\n" +
            "    border: 1px solid #475569;\n" +
            "    border-radius: 6px;\n" +
            "    color: #f8fafc;\n" +
            "    box-sizing: border-box;\n" +
            "}\n" +
            "input:focus { border-color: #38bdf8; outline: none; }\n" +
            ".actions { text-align: right; }\n" +
            "button {\n" +
            "    height: 36px;\n" +
            "    padding: 0 16px;\n" +
            "    background: #0284c7;\n" +
            "    color: white;\n" +
            "    border: none;\n" +
            "    border-radius: 6px;\n" +
            "    font-weight: 500;\n" +
            "    cursor: pointer;\n" +
            "}\n" +
            "button:hover { background: #0369a1; }";

        private string _paramsCode = "result: #inputValue";

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

        public string HtmlCode
        {
            get => _htmlCode;
            set { if (_htmlCode != value) { _htmlCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string JsCode
        {
            get => _jsCode;
            set { if (_jsCode != value) { _jsCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string CssCode
        {
            get => _cssCode;
            set { if (_cssCode != value) { _cssCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string ParamsCode
        {
            get => _paramsCode;
            set { if (_paramsCode != value) { _paramsCode = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public double Width
        {
            get => _width;
            set { if (Math.Abs(_width - value) > 0.01) { _width = Math.Max(280, value); OnPropertyChanged(); } }
        }

        public double Height
        {
            get => _height;
            set { if (Math.Abs(_height - value) > 0.01) { _height = Math.Max(200, value); OnPropertyChanged(); } }
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
