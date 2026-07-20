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
            _htmlCode = @"<div class=""form-container"">
  <h2>Nhập Thông Tin</h2>
  
  <div class=""field"">
    <label>Tên đăng nhập:</label>
    <input type=""text"" id=""username"" value="""" />
  </div>

  <div class=""field"">
    <label>Vai trò:</label>
    <select id=""role"">
      <option value=""Admin"">Administrator</option>
      <option value=""User"">Standard User</option>
    </select>
  </div>

  <div class=""buttons"">
    <button class=""secondary"" id=""btnCancel"">Hủy</button>
    <button class=""primary"" id=""btnSubmit"">Xác nhận</button>
  </div>
</div>";

            _cssCode = @"body {
  font-family: system-ui, -apple-system, sans-serif;
  background: #1e293b;
  color: #f8fafc;
  margin: 0;
  padding: 16px;
}
h2 {
  margin-top: 0;
  font-size: 16px;
  border-bottom: 1px solid #334155;
  padding-bottom: 8px;
}
.field {
  margin-bottom: 12px;
}
label {
  display: block;
  font-size: 11px;
  margin-bottom: 4px;
  color: #94a3b8;
  font-weight: 600;
}
input[type=""text""], select {
  width: 100%;
  padding: 8px;
  background: #0f172a;
  border: 1px solid #334155;
  color: white;
  border-radius: 6px;
  box-sizing: border-box;
}
.buttons {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}
button {
  padding: 8px 16px;
  border-radius: 6px;
  border: none;
  cursor: pointer;
  font-weight: bold;
}
button.primary {
  background: #2563eb;
  color: white;
}
button.secondary {
  background: #334155;
  color: #cbd5e1;
}";

            _jsCode = @"document.on(""ready"", function() {
  // Get prefilled values passed from C# host
  Window.this.xcall(""getPrefilledValue"", ""username"").then(val => {
    if (val) document.$(""#username"").value = val;
  });
  Window.this.xcall(""getPrefilledValue"", ""role"").then(val => {
    if (val) document.$(""#role"").value = val;
  });
});

document.$(""#btnSubmit"").on(""click"", function() {
  let data = {
    username: document.$(""#username"").value,
    role: document.$(""#role"").value
  };
  // Call submit method in C#
  Window.this.xcall(""submitForm"", data);
});

document.$(""#btnCancel"").on(""click"", function() {
  Window.this.xcall(""cancelForm"");
});";

            _paramsCode = @"// Cấu hình cổng ra
// key: selector
username: #username
role: #role
";
            _outputKeys = new List<string> { "username", "role" };

            // Default output fields definition
            _fields.Add(new DynamicUiFieldConfig { Key = "username", Label = "Username", DataType = WorkflowDataType.String });
            _fields.Add(new DynamicUiFieldConfig { Key = "role", Label = "Role", DataType = WorkflowDataType.String });

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
