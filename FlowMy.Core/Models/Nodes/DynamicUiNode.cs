// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using FlowMy.Models;

namespace FlowMy.Models.Nodes
{
    public sealed class DynamicUiNode : WorkflowNode, ISciterNode
    {
        private ObservableCollection<DynamicUiFieldConfig> _fields = new();
        private Dictionary<string, object?> _resolvedOutputs = new();
        private List<AsyncDataSource>? _asyncDataSources = new();
        private bool _pendingAsyncDataPush = false;

        public List<AsyncDataSource>? AsyncDataSources
        {
            get => _asyncDataSources;
            set { _asyncDataSources = value; OnPropertyChanged(); }
        }

        private List<HtmlOfflineAsset> _offlineAssets = new();

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
            set { _pendingAsyncDataPush = value; OnPropertyChanged(); }
        }

        private List<CodeInputMapping> _inputMappings = new();
        private string _htmlCode = "";
        private string _cssCode = "";
        private string _jsCode = "";
        private string _paramsCode = "";
        private List<string> _outputKeys = new();
        private bool _pendingReadDom = false;
        private double _windowWidth = 420;
        private double _windowHeight = 350;
        private double _width = 420;
        private double _height = 320;

        public static readonly string DefaultHtmlCode =
@"<div class=""dynamic-form"">
    <div class=""header"">
        <div class=""title"">⚡ Dynamic UI Test Panel</div>
        <div class=""subtitle"">Kiểm tra tính năng giao tiếp Sciter JS ↔ C# Host</div>
    </div>

    <div class=""card"">
        <div class=""field"">
            <label for=""username"">Tên đăng nhập (Username):</label>
            <input type=""text"" id=""username"" placeholder=""Nhập tên tài khoản..."" value=""Admin_User"" />
        </div>

        <div class=""field"">
            <label for=""role"">Vai trò hệ thống (Role):</label>
            <select id=""role"">
                <option value=""Administrator"" selected>Quản trị viên (Admin)</option>
                <option value=""Operator"">Vận hành viên (Operator)</option>
                <option value=""Guest"">Khách (Guest)</option>
            </select>
        </div>

        <div class=""field"">
            <label for=""note"">Ghi chú (Note):</label>
            <textarea id=""note"" placeholder=""Nhập ghi chú thử nghiệm..."">Sciter JS interop test note</textarea>
        </div>
    </div>

    <div class=""test-bar"">
        <button type=""button"" id=""btnRunSingle"" class=""btn btn-run"" onclick=""sciterRunSingleNode()"">▶ Chạy 1 Node này (sciterRunSingleNode)</button>
        <button type=""button"" id=""btnRunFromNode"" class=""btn btn-flow"" onclick=""sciterRunFromNode()"">⏭ Chạy từ Node này (sciterRunFromNode)</button>
    </div>

    <div class=""live-box"">
        <div class=""live-title"">🔴 Realtime Live Stream (hostLive):</div>
        <div id=""liveStatus"" class=""live-content"">Đang chờ dữ liệu từ C# host...</div>
    </div>

    <div class=""action-bar"">
        <button type=""button"" id=""btnUpdate"" class=""btn btn-secondary"" onclick=""sciterUpdate()"">🔄 Cập Nhật (sciterUpdate)</button>
        <button type=""button"" id=""btnSubmitAndClose"" class=""btn btn-primary"" onclick=""sciterSubmitAndClose()"">🚀 Gửi & Đóng (sciterSubmitAndClose)</button>
    </div>
</div>";

        public static readonly string DefaultCssCode =
@"@set dark-scrollbar {
    .thumb {
        background: #334155;
        border-radius: 4px;
    }
    .thumb:hover {
        background: #475569;
    }
    .base, .corner {
        background: transparent;
    }
    .prev, .next {
        display: none;
    }
}
* {
    box-sizing: border-box;
}
html, body {
    margin: 0;
    padding: 0;
    width: 100%;
    height: 100%;
    background-color: #0f172a;
    color: #f8fafc;
    font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    overflow-y: auto;
    vertical-scrollbar: ""dark-scrollbar"";
}
.dynamic-form {
    padding: 16px;
    display: flex;
    flex-direction: column;
    gap: 12px;
}
.header {
    border-bottom: 1px solid #1e293b;
    padding-bottom: 8px;
}
.title {
    font-size: 15px;
    font-weight: 700;
    color: #38bdf8;
}
.subtitle {
    font-size: 11px;
    color: #64748b;
    margin-top: 2px;
}
.card {
    background: #1e293b;
    border: 1px solid #334155;
    border-radius: 10px;
    padding: 14px;
    display: flex;
    flex-direction: column;
    gap: 10px;
}
.field {
    display: flex;
    flex-direction: column;
    gap: 4px;
}
label {
    font-size: 11px;
    font-weight: 600;
    color: #94a3b8;
}
input[type=""text""], select {
    width: 100%;
    height: 38px;
    min-height: 38px;
    padding: 0 12px;
    background: #0f172a;
    border: 1px solid #334155;
    border-radius: 8px;
    color: #ffffff;
    caret-color: #38bdf8;
    text-selection-caret-color: #38bdf8;
    stroke: #38bdf8;
    font-size: 13px;
    font-family: inherit;
    outline: none;
    transition: border-color 0.2s;
}
textarea {
    width: 100%;
    height: 80px;
    min-height: 80px;
    padding: 10px 12px;
    background: #0f172a;
    border: 1px solid #334155;
    border-radius: 8px;
    color: #ffffff;
    caret-color: #38bdf8;
    text-selection-caret-color: #38bdf8;
    stroke: #38bdf8;
    font-size: 13px;
    font-family: inherit;
    outline: none;
    resize: none;
}
input[type=""text""]:focus, select:focus, textarea:focus {
    border-color: #38bdf8;
    box-shadow: 0 0 0 2px rgba(56, 189, 248, 0.2);
}
.test-bar {
    display: flex;
    gap: 8px;
}
.live-box {
    background: #0f172a;
    border: 1px dashed #334155;
    border-radius: 8px;
    padding: 10px;
}
.live-title {
    font-size: 11px;
    font-weight: 600;
    color: #f43f5e;
}
.live-content {
    font-size: 12px;
    color: #cbd5e1;
    margin-top: 4px;
    font-family: monospace;
}
.action-bar {
    display: flex;
    justify-content: flex-end;
    gap: 10px;
    margin-top: 4px;
}
.btn {
    height: 36px;
    padding: 0 14px;
    border-radius: 8px;
    border: none;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s ease;
}
.btn-primary {
    background: #0284c7;
    color: white;
}
.btn-primary:hover {
    background: #0369a1;
}
.btn-secondary {
    background: #334155;
    color: #cbd5e1;
}
.btn-secondary:hover {
    background: #475569;
}
.btn-run {
    background: #16a34a;
    color: white;
    flex: 1;
}
.btn-run:hover {
    background: #15803d;
}
.btn-flow {
    background: #d97706;
    color: white;
    flex: 1;
}
.btn-flow:hover {
    background: #b45309;
}";

        public static readonly string DefaultJsCode =
@"// Sciter JS event delegation chuẩn 100%
document.on('click', '#btnRunSingle', function(evt) {
    if (typeof sciterRunSingleNode === 'function') {
        sciterRunSingleNode();
    }
});

document.on('click', '#btnRunFromNode', function(evt) {
    if (typeof sciterRunFromNode === 'function') {
        sciterRunFromNode();
    }
});

document.on('click', '#btnUpdate', function(evt) {
    if (typeof sciterUpdate === 'function') {
        sciterUpdate();
    } else if (typeof hostSubmit === 'function') {
        hostSubmit();
    }
});

document.on('click', '#btnSubmitAndClose', function(evt) {
    if (typeof sciterSubmitAndClose === 'function') {
        sciterSubmitAndClose();
    } else if (typeof hostSubmitAndClose === 'function') {
        hostSubmitAndClose();
    }
});

// Đăng ký nhận realtime live data từ C# host
if (window.hostLive && typeof window.hostLive.on === 'function') {
    window.hostLive.on(function(live) {
        var el = document.querySelector('#liveStatus');
        if (el) {
            el.textContent = JSON.stringify(live || {});
        }
    });
}";

        public static readonly string DefaultParamsCode =
@"username: #username
role: #role
note: #note";

        public DynamicUiNode()
        {
            Type = NodeType.DynamicUi;
            IconSize = 60;
            Title = "Dynamic UI Form";
            ColorKey = "Aubergine";

            // Add standard run ports
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

            // Initialize default codes
            _htmlCode = DefaultHtmlCode;
            _cssCode = DefaultCssCode;
            _jsCode = DefaultJsCode;
            _paramsCode = DefaultParamsCode;
            _outputKeys = new List<string> { "username", "role", "note" };

            // Default output fields definition
            _fields.Add(new DynamicUiFieldConfig { Key = "username", Label = "Username", DataType = WorkflowDataType.String });
            _fields.Add(new DynamicUiFieldConfig { Key = "role", Label = "Role", DataType = WorkflowDataType.String });
            _fields.Add(new DynamicUiFieldConfig { Key = "note", Label = "Note", DataType = WorkflowDataType.String });

            RebuildDynamicPorts();
            _inputMappings.Add(new CodeInputMapping());
        }

        public double Width
        {
            get => _width;
            set { if (_width != value) { _width = value; OnPropertyChanged(); } }
        }

        public double Height
        {
            get => _height;
            set { if (_height != value) { _height = value; OnPropertyChanged(); } }
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

        public void EnsureUpgradedTemplate()
        {
            if (string.IsNullOrWhiteSpace(_htmlCode) ||
                !_htmlCode.Contains("dynamic-form", StringComparison.OrdinalIgnoreCase) ||
                !_jsCode.Contains("sciterUpdate", StringComparison.OrdinalIgnoreCase) ||
                !_cssCode.Contains("caret-color", StringComparison.OrdinalIgnoreCase))
            {
                _htmlCode = DefaultHtmlCode;
                _cssCode = DefaultCssCode;
                _jsCode = DefaultJsCode;
                _paramsCode = DefaultParamsCode;
                _outputKeys = new List<string> { "username", "role", "note" };
                RebuildDynamicPorts();
                OnPropertyChanged(nameof(HtmlCode));
                OnPropertyChanged(nameof(CssCode));
                OnPropertyChanged(nameof(JsCode));
                OnPropertyChanged(nameof(ParamsCode));
            }
        }

        public string HtmlCode
        {
            get => _htmlCode;
            set { if (_htmlCode != value) { _htmlCode = value ?? ""; OnPropertyChanged(); } }
        }

        public string CssCode
        {
            get => _cssCode;
            set { if (_cssCode != value) { _cssCode = value ?? ""; OnPropertyChanged(); } }
        }

        public string JsCode
        {
            get => _jsCode;
            set { if (_jsCode != value) { _jsCode = value ?? ""; OnPropertyChanged(); } }
        }

        public string ParamsCode
        {
            get => _paramsCode;
            set { if (_paramsCode != value) { _paramsCode = value ?? ""; OnPropertyChanged(); } }
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
                    RebuildDynamicPorts();
                }
            }
        }

        [JsonIgnore]
        public bool PendingReadDom
        {
            get => _pendingReadDom;
            set { if (_pendingReadDom != value) { _pendingReadDom = value; OnPropertyChanged(); } }
        }

        public double WindowWidth
        {
            get => _windowWidth;
            set { if (_windowWidth != value) { _windowWidth = value; OnPropertyChanged(); } }
        }

        public double WindowHeight
        {
            get => _windowHeight;
            set { if (_windowHeight != value) { _windowHeight = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<DynamicUiFieldConfig> Fields
        {
            get => _fields;
            set
            {
                if (_fields != value)
                {
                    _fields = value ?? new ObservableCollection<DynamicUiFieldConfig>();
                    OnPropertyChanged();
                    RebuildDynamicPorts();
                }
            }
        }

        [JsonIgnore]
        public Dictionary<string, object?> ResolvedOutputs
        {
            get => _resolvedOutputs;
            set { _resolvedOutputs = value ?? new Dictionary<string, object?>(); OnPropertyChanged(); }
        }

        [JsonIgnore]
        public object ResolvedOutputsSyncRoot { get; } = new object();

        [JsonIgnore]
        public TextBlock? TitleTextBlockUI { get; set; }

        public void RebuildDynamicPorts()
        {
            DynamicOutputs.Clear();
            DynamicInputs.Clear();

            // Rebuild outputs from OutputKeys first
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

            // Keep Fields for backward compatibility or fallback
            foreach (var field in _fields)
            {
                if (string.IsNullOrWhiteSpace(field.Key)) continue;

                var trimmedKey = field.Key.Trim();
                var displayName = string.IsNullOrWhiteSpace(field.Label) ? trimmedKey : field.Label.Trim();

                // Only add to DynamicOutputs if not already added by OutputKeys
                if (!DynamicOutputs.Any(o => o.Key == trimmedKey))
                {
                    DynamicOutputs.Add(new WorkflowDynamicDataPort
                    {
                        Key = trimmedKey,
                        DisplayName = displayName,
                        OutputType = field.DataType,
                        IsUserAdded = true
                    });
                }

                // If BindToInput is active, add to DynamicInputs
                if (field.BindToInput)
                {
                    DynamicInputs.Add(new WorkflowDynamicDataPort
                    {
                        Key = trimmedKey,
                        DisplayName = $"{displayName} (Default)",
                        OutputType = field.DataType,
                        IsUserAdded = true
                    });
                }
            }
        }

        public void AddOutputKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var k = key.Trim();
            if (!_outputKeys.Contains(k))
            {
                _outputKeys.Add(k);
                RebuildDynamicPorts();
            }
        }

        public void RemoveOutputKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var k = key.Trim();
            if (_outputKeys.Contains(k))
            {
                _outputKeys.Remove(k);
                RebuildDynamicPorts();
            }
        }
    }
}
