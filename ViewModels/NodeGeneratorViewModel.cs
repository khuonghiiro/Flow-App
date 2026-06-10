using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Services.Utilities;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// Đại diện cho một port có thể cấu hình trong form.
    /// </summary>
    public sealed partial class PortConfigItem : ObservableObject
    {
        [ObservableProperty] private string _colorKey = "Info";
        [ObservableProperty] private string _label = string.Empty;
    }

    /// <summary>
    /// Đại diện cho một TextBox / ComboBox / CheckBox field tùy chỉnh trong form.
    /// </summary>
    public sealed partial class DialogFieldItem : ObservableObject
    {
        [ObservableProperty] private string _label = string.Empty;
        [ObservableProperty] private string _bindingPath = string.Empty;
        [ObservableProperty] private string _placeholder = string.Empty;
    }

    /// <summary>
    /// Đại diện cho một Radio Group.
    /// </summary>
    public sealed partial class RadioGroupItem : ObservableObject
    {
        [ObservableProperty] private string _groupLabel = string.Empty;
        [ObservableProperty] private string _bindingPath = string.Empty;
        [ObservableProperty] private string _optionsRaw = string.Empty; // csv: "Option A,Option B"
    }

    /// <summary>
    /// ViewModel cho NodeGeneratorWindow — quản lý form nhập liệu và sinh code.
    /// </summary>
    public sealed partial class NodeGeneratorViewModel : ObservableObject
    {
        // ─── Basic info ───────────────────────────────────────────────────────
        [ObservableProperty] private string _nodeName = string.Empty;
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private string _iconKey = "circle-nodes duotone-regular";
        [ObservableProperty] private string _colorKey = "Info";

        // ─── Node Type ────────────────────────────────────────────────────────
        [ObservableProperty] private bool _addNewNodeType = true;
        [ObservableProperty] private string _nodeTypeName = string.Empty;

        // ─── Dialog options ───────────────────────────────────────────────────
        [ObservableProperty] private bool _hasInputSection = true;
        [ObservableProperty] private bool _hasOutputsPanel = true;
        [ObservableProperty] private bool _hasDynamicInputs = false;

        // ─── Output Keys (csv) ────────────────────────────────────────────────
        [ObservableProperty] private string _outputKeysRaw = string.Empty;

        // ─── Project Root ─────────────────────────────────────────────────────
        [ObservableProperty] private string _projectRoot = string.Empty;

        // ─── Status / Result ──────────────────────────────────────────────────
        [ObservableProperty] private string _resultText = string.Empty;
        [ObservableProperty] private bool _hasError = false;
        [ObservableProperty] private bool _isSuccess = false;
        [ObservableProperty] private bool _hasResult = false; // true khi có ResultText để hiện panel

        // ─── Collections ──────────────────────────────────────────────────────
        public ObservableCollection<PortConfigItem> InputPorts { get; } = new();
        public ObservableCollection<PortConfigItem> OutputPorts { get; } = new();
        public ObservableCollection<DialogFieldItem> CustomTextBoxes { get; } = new();
        public ObservableCollection<DialogFieldItem> CustomComboBoxes { get; } = new();
        public ObservableCollection<DialogFieldItem> CustomCheckBoxes { get; } = new();
        public ObservableCollection<RadioGroupItem> RadioGroups { get; } = new();

        // ─── Available color keys (suggestions) ───────────────────────────────
        public ObservableCollection<string> ColorKeyOptions { get; } = new()
        {
            "Info", "Primary", "Success", "Warning", "Danger", "SunsetOrange",
            "LimeGreen", "ForestPine", "NavyDeep", "ChocolateBrown",
            "Gold", "Violet", "Teal", "Coral", "Slate"
        };

        public NodeGeneratorViewModel()
        {
            // Defaults
            InputPorts.Add(new PortConfigItem { ColorKey = "Info", Label = "Input" });
            OutputPorts.Add(new PortConfigItem { ColorKey = "SunsetOrange", Label = "Output" });

            // Auto-detect project root
            TryAutoDetectProjectRoot();
        }

        private void TryAutoDetectProjectRoot()
        {
            // Đi ngược từ BaseDirectory (bin\Debug\net9.0-windows\) lên tìm .csproj
            var dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            for (int i = 0; i < 6; i++)
            {
                if (System.IO.Directory.GetFiles(dir, "*.csproj").Length > 0)
                {
                    ProjectRoot = dir;
                    return;
                }
                var parent = System.IO.Directory.GetParent(dir)?.FullName;
                if (parent == null) break;
                dir = parent;
            }
            // Fallback: thư mục exe
            ProjectRoot = AppDomain.CurrentDomain.BaseDirectory;
        }

        // ─── Partial onChange ─────────────────────────────────────────────────

        partial void OnNodeNameChanged(string value)
        {
            // Auto-fill Title nếu chưa nhập
            if (string.IsNullOrWhiteSpace(Title))
                Title = value;
            // Auto-fill NodeTypeName nếu chưa nhập
            if (string.IsNullOrWhiteSpace(NodeTypeName))
                NodeTypeName = value;
        }

        // ─── Port commands ────────────────────────────────────────────────────

        [RelayCommand]
        private void AddInputPort() =>
            InputPorts.Add(new PortConfigItem { ColorKey = "Info", Label = $"Input {InputPorts.Count + 1}" });

        [RelayCommand]
        private void RemoveInputPort(PortConfigItem? item)
        {
            if (item != null) InputPorts.Remove(item);
        }

        [RelayCommand]
        private void AddOutputPort() =>
            OutputPorts.Add(new PortConfigItem { ColorKey = "SunsetOrange", Label = $"Output {OutputPorts.Count + 1}" });

        [RelayCommand]
        private void RemoveOutputPort(PortConfigItem? item)
        {
            if (item != null) OutputPorts.Remove(item);
        }

        // ─── Custom field commands ────────────────────────────────────────────

        [RelayCommand]
        private void AddTextBox() =>
            CustomTextBoxes.Add(new DialogFieldItem { Label = $"Text Field {CustomTextBoxes.Count + 1}", BindingPath = $"Field{CustomTextBoxes.Count + 1}" });

        [RelayCommand]
        private void RemoveTextBox(DialogFieldItem? item) { if (item != null) CustomTextBoxes.Remove(item); }

        [RelayCommand]
        private void AddComboBox() =>
            CustomComboBoxes.Add(new DialogFieldItem { Label = $"ComboBox {CustomComboBoxes.Count + 1}", BindingPath = $"Combo{CustomComboBoxes.Count + 1}" });

        [RelayCommand]
        private void RemoveComboBox(DialogFieldItem? item) { if (item != null) CustomComboBoxes.Remove(item); }

        [RelayCommand]
        private void AddCheckBox() =>
            CustomCheckBoxes.Add(new DialogFieldItem { Label = $"Option {CustomCheckBoxes.Count + 1}", BindingPath = $"IsOption{CustomCheckBoxes.Count + 1}" });

        [RelayCommand]
        private void RemoveCheckBox(DialogFieldItem? item) { if (item != null) CustomCheckBoxes.Remove(item); }

        [RelayCommand]
        private void AddRadioGroup() =>
            RadioGroups.Add(new RadioGroupItem { GroupLabel = $"Group {RadioGroups.Count + 1}", BindingPath = $"SelectedGroup{RadioGroups.Count + 1}Option", OptionsRaw = "Option A,Option B" });

        [RelayCommand]
        private void RemoveRadioGroup(RadioGroupItem? item) { if (item != null) RadioGroups.Remove(item); }

        // ─── Main Generate command ─────────────────────────────────────────────

        [RelayCommand]
        private void GenerateNode()
        {
            ResultText = string.Empty;
            HasError = false;
            IsSuccess = false;
            HasResult = false;

            var config = BuildConfig();
            var validation = ValidateConfig(config);
            if (!string.IsNullOrEmpty(validation))
            {
                ResultText = validation;
                HasError = true;
                HasResult = true;
                return;
            }

            var service = new NodeGeneratorService();
            var result = service.GenerateAll(config);

            ResultText = result.ToSummary();
            HasError = !result.IsSuccess;
            IsSuccess = result.IsSuccess && result.CreatedFiles.Count > 0;
            HasResult = true;
        }

        // ─── CLI JSON command ─────────────────────────────────────────────────

        [RelayCommand]
        private void CopyCliCommand()
        {
            var config = BuildConfig();
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

            // Tạo lệnh PowerShell
            var escaped = json.Replace("'", "''");
            var cliCmd =
                $"# Lưu JSON vào file tạm\r\n" +
                $"$json = @'\r\n{json}\r\n'@\r\n" +
                $"$json | Out-File -FilePath 'node_config.json' -Encoding utf8\r\n\r\n" +
                $"# Hoặc dùng NodeGeneratorService trực tiếp qua C# script";

            try
            {
                Clipboard.SetText(cliCmd);
                ResultText = "✅ Đã copy CLI command vào clipboard!\r\n\r\nBạn có thể dùng JSON config này để gọi NodeGeneratorService.ParseFromJson() trong code.";
                HasError = false;
                HasResult = true;
            }
            catch
            {
                ResultText = cliCmd;
                HasResult = true;
            }
        }

        [RelayCommand]
        private void CopyJsonConfig()
        {
            var config = BuildConfig();
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            try
            {
                Clipboard.SetText(json);
                ResultText = "✅ Đã copy JSON config vào clipboard!";
                HasError = false;
                HasResult = true;
            }
            catch
            {
                ResultText = json;
                HasResult = true;
            }
        }

        [RelayCommand]
        private void BrowseProjectRoot()
        {
            // Dùng FolderBrowserDialog
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Chọn thư mục gốc của project FlowMy",
                SelectedPath = ProjectRoot,
                ShowNewFolderButton = false
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProjectRoot = dlg.SelectedPath;
            }
        }

        // ─── Build config from form ───────────────────────────────────────────

        public NodeGeneratorConfig BuildConfig()
        {
            var config = new NodeGeneratorConfig
            {
                NodeName = NodeName?.Trim() ?? string.Empty,
                Title = string.IsNullOrWhiteSpace(Title) ? NodeName?.Trim() ?? string.Empty : Title.Trim(),
                IconKey = IconKey?.Trim() ?? "circle-nodes duotone-regular",
                ColorKey = ColorKey?.Trim() ?? "Info",
                AddNewNodeType = AddNewNodeType,
                NodeTypeName = NodeTypeName?.Trim() ?? string.Empty,
                HasInputSection = HasInputSection,
                HasOutputsPanel = HasOutputsPanel,
                HasDynamicInputs = HasDynamicInputs,
                ProjectRoot = ProjectRoot?.Trim() ?? string.Empty,
            };

            // Ports
            foreach (var p in InputPorts)
                config.InputPortColorKeys.Add(p.ColorKey);
            config.InputPortColorKeys.RemoveAt(0); // remove default empty

            foreach (var p in OutputPorts)
                config.OutputPortColorKeys.Add(p.ColorKey);
            config.OutputPortColorKeys.RemoveAt(0);

            // Rebuild from items (clear defaults first)
            config.InputPortColorKeys = new System.Collections.Generic.List<string>();
            foreach (var p in InputPorts)
                config.InputPortColorKeys.Add(string.IsNullOrWhiteSpace(p.ColorKey) ? "Info" : p.ColorKey);

            config.OutputPortColorKeys = new System.Collections.Generic.List<string>();
            foreach (var p in OutputPorts)
                config.OutputPortColorKeys.Add(string.IsNullOrWhiteSpace(p.ColorKey) ? "SunsetOrange" : p.ColorKey);

            // Output keys
            if (!string.IsNullOrWhiteSpace(OutputKeysRaw))
            {
                foreach (var k in OutputKeysRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = k.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        config.OutputKeys.Add(trimmed);
                }
            }

            // Custom fields
            foreach (var tb in CustomTextBoxes)
                config.CustomTextBoxes.Add(new DialogFieldConfig { Label = tb.Label, BindingPath = tb.BindingPath, Placeholder = tb.Placeholder });

            foreach (var cb in CustomComboBoxes)
                config.CustomComboBoxes.Add(new DialogFieldConfig { Label = cb.Label, BindingPath = cb.BindingPath });

            foreach (var chk in CustomCheckBoxes)
                config.CustomCheckBoxes.Add(new DialogFieldConfig { Label = chk.Label, BindingPath = chk.BindingPath });

            foreach (var rg in RadioGroups)
            {
                var opts = rg.OptionsRaw?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                config.RadioGroups.Add(new RadioGroupConfig
                {
                    GroupLabel = rg.GroupLabel,
                    BindingPath = rg.BindingPath,
                    Options = new System.Collections.Generic.List<string>(opts)
                });
            }

            return config;
        }

        private static string ValidateConfig(NodeGeneratorConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.NodeName))
                return "❌ NodeName không được để trống.\nVí dụ: HelloWorld → tạo HelloWorldNode";

            if (!char.IsLetter(config.NodeName[0]))
                return "❌ NodeName phải bắt đầu bằng chữ cái.";

            if (config.NodeName.Contains(' '))
                return "❌ NodeName không được có dấu cách (dùng PascalCase: HelloWorld).";

            if (string.IsNullOrWhiteSpace(config.ProjectRoot))
                return "❌ Project Root chưa được chọn.";

            if (!Directory.Exists(config.ProjectRoot))
                return $"❌ Thư mục Project Root không tồn tại:\n{config.ProjectRoot}";

            return string.Empty;
        }

        /// <summary>Tạo preview text hiển thị các file sẽ được sinh ra.</summary>
        public string GetPreviewText()
        {
            var config = BuildConfig();
            if (string.IsNullOrWhiteSpace(config.NodeName))
                return "Nhập NodeName để xem preview...";

            var root = string.IsNullOrWhiteSpace(config.ProjectRoot) ? "[ProjectRoot]" : config.ProjectRoot;
            return
                $"📁 Files sẽ được tạo:\n\n" +
                $"  📄 Models/Nodes/{config.NodeClassName}.cs\n" +
                $"  📄 Views/NodeControls/{config.ControlClassName}.cs\n" +
                $"  📄 Views/Overlays/{config.DialogClassName}.xaml\n" +
                $"  📄 Views/Overlays/{config.DialogClassName}.xaml.cs\n" +
                $"  📄 ViewModels/{config.ViewModelClassName}.cs\n" +
                $"  📄 Services/Rendering/{config.RendererClassName}.cs\n" +
                (config.AddNewNodeType ? $"  📝 Models/Nodes/NodeType.cs (thêm {config.EffectiveNodeTypeName})\n" : "") +
                $"\n🔧 Cấu hình:\n" +
                $"  NodeType  : {(config.AddNewNodeType ? config.EffectiveNodeTypeName : "Generic")}\n" +
                $"  IconKey   : {config.IconKey}\n" +
                $"  ColorKey  : {config.ColorKey}\n" +
                $"  Ports IN  : {config.InputPortColorKeys.Count} ({string.Join(", ", config.InputPortColorKeys)})\n" +
                $"  Ports OUT : {config.OutputPortColorKeys.Count} ({string.Join(", ", config.OutputPortColorKeys)})\n" +
                (config.OutputKeys.Count > 0 ? $"  Output Keys: {string.Join(", ", config.OutputKeys)}\n" : "") +
                $"\n⚠️ Sau khi tạo xong, bạn cần đăng ký thủ công:\n" +
                $"  • TemplateFactory.cs — thêm CreateYourNode()\n" +
                $"  • _NodeRenderer.cs — thêm renderer branch\n" +
                $"  • WorkflowEditorWindow.xaml — thêm palette icon\n" +
                $"  • App.xaml.cs — đăng ký DI: services.AddSingleton<{config.RendererClassName}>()";
        }
    }
}
