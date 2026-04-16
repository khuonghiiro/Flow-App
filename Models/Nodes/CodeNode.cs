using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using FlowMy.Models;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    /// <summary>Một input mapping: node nguồn + key → tên biến trong code.</summary>
    public sealed class CodeInputMapping : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private string? _inputKeyOverride;
        private bool _shouldReExecute = false;

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

        /// <summary>Cho phép chạy lại logic của node nguồn để lấy dữ liệu mới nhất (mặc định true).</summary>
        public bool ShouldReExecute
        {
            get => _shouldReExecute;
            set { if (_shouldReExecute != value) { _shouldReExecute = value; OnPropertyChanged(); } }
        }

        // --- Auto-refresh: lấy giá trị mới nhất theo chu kỳ, push vào JS window.__ac ---
        /// <summary>Bật/tắt tự động push giá trị mới nhất vào JS window.__ac theo khoảng thời gian.</summary>
        public bool AutoRefreshEnabled { get; set; } = false;

        /// <summary>Khoảng thời gian lấy dữ liệu mới (kèm AutoRefreshUnit: "ms" / "s" / "min").</summary>
        public int AutoRefreshInterval { get; set; } = 1000;

        /// <summary>Đơn vị thời gian: "ms" (mili-giây), "s" (giây), "min" (phút).</summary>
        public string AutoRefreshUnit { get; set; } = "ms";

        /// <summary>Tên biến trong code = InputKeyOverride nếu có, không thì SourceOutputKey, mặc định "input".</summary>
        public string EffectiveInputKey => !string.IsNullOrWhiteSpace(_inputKeyOverride)
            ? _inputKeyOverride!.Trim()
            : (string.IsNullOrWhiteSpace(_sourceOutputKey) ? "input" : _sourceOutputKey!.Trim());

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Node chạy đoạn code (JavaScript) với input từ nhiều node + key, trả về outputs theo cấu hình.
    /// Mỗi input mapping → một biến trong code (EffectiveInputKey).
    /// </summary>
    public sealed class CodeNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;
        private List<CodeInputMapping> _inputMappings = new();
        private string _scriptCode = "// Nhập code JavaScript. Biến từ input dùng tên key (ví dụ: jsonData).\n// return { key1: value1, key2: value2 }; để trả về outputs.\n// Tip: Nếu muốn điều khiển WebNode (WebView2), hãy trả về output key 'js' chứa script.\n// Ví dụ (dùng parameter từ input):\n// function main() {\n//   const textToInput = JSON.stringify(prompt); // prompt là biến từ input mapping\n//   return {\n//     js: 'await ac.waitForSelector(\\'#btnLogin\\', 15000);\\n' +\n//         'await ac.retryClick(\\'#btnLogin\\', { timeoutMs: 15000, intervalMs: 250 });\\n' +\n//         'const textarea = document.querySelector(\\'#input\\');\\n' +\n//         'textarea.value = ' + textToInput + ';\\n' +\n//         'await ac.waitNetworkIdle({ idleMs: 800, timeoutMs: 15000 });'\n//   };\n// }\nreturn {};";
        private List<string> _outputKeys = new() { "result" };

        public CodeNode()
        {
            Type = NodeType.Code;
            Title = "Code";
            ColorKey = "PapayaOrange";

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

        /// <summary>Danh sách input: mỗi phần tử = một nguồn (node + key) → một biến trong code.</summary>
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

        /// <summary>Backward compat: node nguồn (lấy từ mapping đầu).</summary>
        [JsonIgnore]
        public string? SourceNodeId
        {
            get => _inputMappings.Count > 0 ? _inputMappings[0].SourceNodeId : null;
            set { if (_inputMappings.Count > 0) _inputMappings[0].SourceNodeId = value; }
        }

        /// <summary>Backward compat: key nguồn (lấy từ mapping đầu).</summary>
        [JsonIgnore]
        public string? SourceOutputKey
        {
            get => _inputMappings.Count > 0 ? _inputMappings[0].SourceOutputKey : null;
            set { if (_inputMappings.Count > 0) _inputMappings[0].SourceOutputKey = value; }
        }

        /// <summary>Backward compat: key tùy chỉnh (lấy từ mapping đầu).</summary>
        [JsonIgnore]
        public string? InputKeyOverride
        {
            get => _inputMappings.Count > 0 ? _inputMappings[0].InputKeyOverride : null;
            set { if (_inputMappings.Count > 0) _inputMappings[0].InputKeyOverride = value; }
        }

        /// <summary>Tên biến trong code (mapping đầu).</summary>
        [JsonIgnore]
        public string EffectiveInputKey => _inputMappings.Count > 0 ? _inputMappings[0].EffectiveInputKey : "input";

        /// <summary>Đoạn code JavaScript. Return object { key: value } để trả output.</summary>
        public string ScriptCode
        {
            get => _scriptCode;
            set { if (_scriptCode != value) { _scriptCode = value ?? string.Empty; OnPropertyChanged(); } }
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

        /// <summary>Runtime: giá trị output sau khi chạy code (không serialize).</summary>
        [JsonIgnore]
        public Dictionary<string, object?> ResolvedOutputs { get; set; } = new();

        /// <summary>Đồng bộ ghi <see cref="ResolvedOutputs"/> khi nhiều dispatch chạy song song trên cùng một node.</summary>
        [JsonIgnore]
        public object ResolvedOutputsSyncRoot { get; } = new object();

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

        /// <summary>Reference đến TextBlock hiển thị title trên canvas (được tạo trong CodeNodeControl).</summary>
        public TextBlock? TitleTextBlockUI { get; set; }

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
            if (!_outputKeys.Contains(k)) { _outputKeys.Add(k); RebuildDynamicOutputs(); OnPropertyChanged(nameof(OutputKeys)); }
        }

        public void RemoveOutputKeyAt(int index)
        {
            if (index >= 0 && index < _outputKeys.Count) { _outputKeys.RemoveAt(index); RebuildDynamicOutputs(); OnPropertyChanged(nameof(OutputKeys)); }
        }
    }
}
