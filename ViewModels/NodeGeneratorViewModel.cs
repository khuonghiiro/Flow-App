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

        // Flag: người dùng đã tự sửa Title/NodeTypeName → không auto-sync khi NodeName thay đổi nữa
        private bool _titleEditedManually = false;
        private bool _nodeTypeNameEditedManually = false;
        // Guard tránh OnTitleChanged/OnNodeTypeNameChanged fire khi ta tự set từ NodeName
        private bool _isSyncingFromNodeName = false;

        // ─── Palette Category ─────────────────────────────────────────────────
        [ObservableProperty] private string _paletteCategory = "Screen";
        public ObservableCollection<string> PaletteCategories { get; } = new();

        // ─── Edit Existing Node ───────────────────────────────────────────────
        [ObservableProperty] private string _selectedExistingNode = string.Empty;
        [ObservableProperty] private string _editColorKey = "Info";
        [ObservableProperty] private string _editIconKey = "circle-nodes duotone-regular";
        [ObservableProperty] private string _editInputPortColorKey = "Info";
        [ObservableProperty] private string _editOutputPortColorKey = "SunsetOrange";
        public ObservableCollection<string> ExistingNodes { get; } = new();


        // ─── Dialog options ───────────────────────────────────────────────────
        [ObservableProperty] private bool _hasInputSection = true;
        [ObservableProperty] private int _defaultInputCount = 1;
        [ObservableProperty] private bool _hasCheckboxToToggleInputs = false;
        [ObservableProperty] private bool _hasOutputsPanel = true;
        [ObservableProperty] private bool _hasDynamicInputs = false;
        [ObservableProperty] private bool _hasCustomKeyOverride = false;

        // ─── Output Keys (csv) ────────────────────────────────────────────────
        [ObservableProperty] private string _outputKeysRaw = string.Empty;

        // ─── Project Root ─────────────────────────────────────────────────────
        [ObservableProperty] private string _projectRoot = string.Empty;

        // ─── Status / Result ──────────────────────────────────────────────────
        [ObservableProperty] private string _resultText = string.Empty;
        [ObservableProperty] private bool _hasError = false;
        [ObservableProperty] private bool _isSuccess = false;
        [ObservableProperty] private bool _hasResult = false; // true khi có ResultText để hiện panel

        // ─── Registration Status ───────────────────────────────────────────────
        [ObservableProperty] private string _registrationLog = string.Empty;
        [ObservableProperty] private bool _isRegistered = false;
        [ObservableProperty] private bool _hasRegistrationResult = false;
        [ObservableProperty] private bool _hasRegistrationError = false;

        // ─── Collections ──────────────────────────────────────────────────────
        public ObservableCollection<PortConfigItem> InputPorts { get; } = new();
        public ObservableCollection<PortConfigItem> OutputPorts { get; } = new();
        public ObservableCollection<DialogFieldItem> CustomTextBoxes { get; } = new();
        public ObservableCollection<DialogFieldItem> CustomComboBoxes { get; } = new();
        public ObservableCollection<DialogFieldItem> CustomCheckBoxes { get; } = new();
        public ObservableCollection<RadioGroupItem> RadioGroups { get; } = new();

        // ─── Available color keys (from Common.xaml — đủ 4 brush: Brush/Hover/Pressed/TextOn) ─────
        public ObservableCollection<string> ColorKeyOptions { get; } = new()
        {
            // ── Semantic (Bootstrap-style) ──────────────────────────────────────
            "Info", "Success", "Warning", "Danger", "Dark", "Light",
            // ── Indigo / Blue family ────────────────────────────────────────────
            "Indigo", "IndigoNight", "SkyBlue", "SkyAzure", "OceanBlue",
            "AquaMarine", "TealCyan", "MidnightBlue", "NavyDeep", "CobaltBlue",
            "SteelBlue", "SapphireBlue", "PrussianBlue", "CeruleanSky",
            "PeacockBlue", "BlueberryIce", "GlacierBlue", "SerenityBlue",
            "AzureBlue", "Fluidity", "Atlassian", "Retro",
            // ── Green family ────────────────────────────────────────────────────
            "EmeraldGreen", "Emerald", "ForestPine", "OliveGreen", "LimeGreen",
            "LimeBright", "JadeGreen", "BambooGreen", "BambooGreen",
            "CucumberGreen", "KiwiGreen", "OliveGreen", "SageGreen",
            "PistachioGreen", "MossGreen", "SeaFoam", "MintFresh",
            "MintChocolate", "ArcticTeal", "Ocean",
            // ── Red / Orange / Coral family ─────────────────────────────────────
            "CoralVivid", "Coral", "CoralSunset", "RubyRed", "CrimsonRose",
            "CrimsonVelvet", "RaspberrySorbet", "BrickRed", "Terracotta",
            "BurgundyWine", "WineRed", "BrightPower", "SunsetOrange",
            "Sunset", "MangoTango", "TangerineDream", "PumpkinSpice",
            "CantaloupeOrange", "PapayaOrange", "DuskyRose", "DustyRose",
            "BerryPurple", "SalmonPink", "FlamingoPink",
            // ── Yellow / Gold family ─────────────────────────────────────────────
            "GoldenYellow", "AmberWarm", "LemonZest", "LemonLime",
            "MarigoldYellow", "HoneyGold", "PeachSoft", "ApricotSoft",
            "ChampagneGold", "ButtercupYellow", "SunflowerYellow",
            "EggYolk", "LavenderDream",
            // ── Purple / Violet family ───────────────────────────────────────────
            "RoyalPurple", "LavenderDream", "Lavender", "Amethyst",
            "VioletDeep", "VioletHaze", "PlumPurple", "WisteriaPurple",
            "SlatePurple", "IrisPurple", "MagentaBold", "MagentaBloom",
            "FuchsiaBright", "LilacGrace", "OrchidPink", "CherryBlossom",
            "Cherry", "BlushPink", "RoseQuartz", "Space", "Gentle",
            // ── Brown / Gray family ──────────────────────────────────────────────
            "ChocolateBrown", "EspressoBrown", "CaramelBrown", "BronzeMetal",
            "SlateGray", "CharcoalDark", "CharcoalMist", "GraphiteGray",
            "Aubergine",
            // ── Teal / Cyan ──────────────────────────────────────────────────────
            "Turquoise", "ArcticTeal", "TealCyan", "AquaMarine",
            // ── Misc ─────────────────────────────────────────────────────────────
            "PeriwinkleBlue", "Cerulean",
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
                    LoadPaletteCategories();
                    LoadExistingNodes();
                    return;
                }
                var parent = System.IO.Directory.GetParent(dir)?.FullName;
                if (parent == null) break;
                dir = parent;
            }
            // Fallback: thư mục exe
            ProjectRoot = AppDomain.CurrentDomain.BaseDirectory;

            LoadPaletteCategories();
            LoadExistingNodes();
        }

        private void LoadExistingNodes()
        {
            if (string.IsNullOrWhiteSpace(ProjectRoot)) return;
            var dir = Path.Combine(ProjectRoot, "Views", "NodeControls");
            if (!Directory.Exists(dir)) return;

            try
            {
                // We generate .cs files for NodeControl now
                var files = Directory.GetFiles(dir, "*NodeControl.cs");
                var nodes = new System.Collections.Generic.HashSet<string>();
                foreach (var f in files)
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (name.EndsWith("NodeControl"))
                    {
                        nodes.Add(name.Substring(0, name.Length - "NodeControl".Length));
                    }
                }
                if (nodes.Count > 0)
                {
                    ExistingNodes.Clear();
                    foreach (var n in nodes) ExistingNodes.Add(n);
                    if (string.IsNullOrWhiteSpace(SelectedExistingNode) || !ExistingNodes.Contains(SelectedExistingNode))
                        SelectedExistingNode = ExistingNodes.FirstOrDefault() ?? string.Empty;
                }
            }
            catch { }
        }

        private void LoadPaletteCategories()
        {
            if (string.IsNullOrWhiteSpace(ProjectRoot)) return;
            var path = Path.Combine(ProjectRoot, "Views", "WorkflowEditorWindow.xaml");
            if (!File.Exists(path)) path = Path.Combine(ProjectRoot, "Views", "WorkflowEditors", "WorkflowEditorWindow.xaml");
            if (!File.Exists(path)) return;

            try
            {
                var content = File.ReadAllText(path);
                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"<TextBlock\s+Text=""([^""]+)""\s+Style=""\{StaticResource\s+PaletteGroupHeaderStyle\}""");
                var cats = new System.Collections.Generic.HashSet<string>();
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    if (m.Groups.Count > 1) cats.Add(m.Groups[1].Value);
                }
                if (cats.Count > 0)
                {
                    PaletteCategories.Clear();
                    foreach (var c in cats) PaletteCategories.Add(c);
                    if (!PaletteCategories.Contains(PaletteCategory)) 
                        PaletteCategory = PaletteCategories.FirstOrDefault() ?? "Screen";
                }
            }
            catch { }
        }

        partial void OnSelectedExistingNodeChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(ProjectRoot)) return;
            try
            {
                // Reset to empty
                EditIconKey = string.Empty;
                EditColorKey = string.Empty;
                EditInputPortColorKey = string.Empty;
                EditOutputPortColorKey = string.Empty;

                // 1. Try get Icon from [NodeName]NodeControl.cs or [NodeName]Control.cs
                var controlCsPath = Path.Combine(ProjectRoot, "Views", "NodeControls", $"{value}NodeControl.cs");
                if (!File.Exists(controlCsPath)) 
                    controlCsPath = Path.Combine(ProjectRoot, "Views", "NodeControls", $"{value}Control.cs");

                if (File.Exists(controlCsPath))
                {
                    var content = File.ReadAllText(controlCsPath);
                    var iconMatch = System.Text.RegularExpressions.Regex.Match(content, @"iconConverter\.Convert\(null,\s*typeof\(Uri\),\s*""([^""]+)""");
                    if (iconMatch.Success) EditIconKey = iconMatch.Groups[1].Value;
                }

                // 1b. Fallback to [NodeName]Control.xaml
                var xamlPath = Path.Combine(ProjectRoot, "Views", "NodeControls", $"{value}Control.xaml");
                if (File.Exists(xamlPath))
                {
                    var content = File.ReadAllText(xamlPath);
                    var bgMatch = System.Text.RegularExpressions.Regex.Match(content, @"Background=""\{DynamicResource\s+([A-Za-z]+)Brush\}""");
                    if (bgMatch.Success) EditColorKey = bgMatch.Groups[1].Value;

                    var iconMatch = System.Text.RegularExpressions.Regex.Match(content, @"ConverterParameter='([^']+)'");
                    if (iconMatch.Success) EditIconKey = iconMatch.Groups[1].Value;
                }

                // 2. Try get Colors from TemplateFactory.cs
                var templateFactoryPath = Path.Combine(ProjectRoot, "Workflow", "TemplateFactory.cs");
                if (!File.Exists(templateFactoryPath)) templateFactoryPath = Path.Combine(ProjectRoot, "Services", "Workflow", "TemplateFactory.cs");
                if (File.Exists(templateFactoryPath))
                {
                    var content = File.ReadAllText(templateFactoryPath);
                    var createMethodIdx = content.IndexOf($"Create{value}Node(");
                    if (createMethodIdx > 0)
                    {
                        var endIdx = content.IndexOf("return node;", createMethodIdx);
                        if (endIdx > 0)
                        {
                            var methodContent = content.Substring(createMethodIdx, endIdx - createMethodIdx);
                            var colorKeyMatch = System.Text.RegularExpressions.Regex.Match(methodContent, @"ColorKey\s*=\s*""([^""]+)""");
                            if (colorKeyMatch.Success) EditColorKey = colorKeyMatch.Groups[1].Value;

                            var inPortMatch = System.Text.RegularExpressions.Regex.Match(methodContent, @"IsInput\s*=\s*true[^}]*ColorKey\s*=\s*""([^""]+)""");
                            if (inPortMatch.Success) EditInputPortColorKey = inPortMatch.Groups[1].Value;

                            var outPortMatch = System.Text.RegularExpressions.Regex.Match(methodContent, @"IsInput\s*=\s*false[^}]*ColorKey\s*=\s*""([^""]+)""");
                            if (outPortMatch.Success) EditOutputPortColorKey = outPortMatch.Groups[1].Value;
                        }
                    }
                }

                // 3. Fallback: try get colors from [NodeName]Node.cs
                var nodeCsPath = Path.Combine(ProjectRoot, "Models", "Nodes", $"{value}Node.cs");
                if (File.Exists(nodeCsPath))
                {
                    var content = File.ReadAllText(nodeCsPath);
                    if (string.IsNullOrWhiteSpace(EditColorKey))
                    {
                        var colorKeyMatch = System.Text.RegularExpressions.Regex.Match(content, @"ColorKey\s*=\s*""([^""]+)""");
                        if (colorKeyMatch.Success) EditColorKey = colorKeyMatch.Groups[1].Value;
                    }
                    if (string.IsNullOrWhiteSpace(EditInputPortColorKey))
                    {
                        var inPortMatch = System.Text.RegularExpressions.Regex.Match(content, @"IsInput\s*=\s*true[^}]*ColorKey\s*=\s*""([^""]+)""");
                        if (inPortMatch.Success) EditInputPortColorKey = inPortMatch.Groups[1].Value;
                    }
                    if (string.IsNullOrWhiteSpace(EditOutputPortColorKey))
                    {
                        var outPortMatch = System.Text.RegularExpressions.Regex.Match(content, @"IsInput\s*=\s*false[^}]*ColorKey\s*=\s*""([^""]+)""");
                        if (outPortMatch.Success) EditOutputPortColorKey = outPortMatch.Groups[1].Value;
                    }
                }
            }
            catch { }
        }

        // ─── Partial onChange ─────────────────────────────────────────────────

        partial void OnNodeNameChanged(string value)
        {
            _isSyncingFromNodeName = true;
            try
            {
                // Title: luôn sync (trừ khi user đã sửa tay), hiển thị có dấu cách theo PascalCase
                if (!_titleEditedManually)
                    Title = PascalCaseToWords(value);

                // NodeTypeName: luôn sync (trừ khi user đã sửa tay), y nguyên không thêm dấu cách
                if (!_nodeTypeNameEditedManually)
                    NodeTypeName = value;
            }
            finally
            {
                _isSyncingFromNodeName = false;
            }
        }

        partial void OnTitleChanged(string value)
        {
            // Nếu không phải do sync từ NodeName → đánh dấu user đã sửa tay
            if (!_isSyncingFromNodeName)
                _titleEditedManually = !string.IsNullOrWhiteSpace(value);
        }

        partial void OnNodeTypeNameChanged(string value)
        {
            // Nếu không phải do sync từ NodeName → đánh dấu user đã sửa tay
            if (!_isSyncingFromNodeName)
                _nodeTypeNameEditedManually = !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Chuyển PascalCase thành chuỗi có dấu cách giữa các từ.
        /// VD: "HelloWorld" → "Hello World", "MyHTTPRequest" → "My HTTP Request"
        /// </summary>
        private static string PascalCaseToWords(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (i > 0 && char.IsUpper(c))
                {
                    // Thêm dấu cách nếu:
                    // - ký tự trước là chữ thường (HelloWorld → Hello World)
                    // - hoặc ký tự tiếp theo là chữ thường và ký tự trước là hoa (HTTPRequest → HTTP Request)
                    char prev = input[i - 1];
                    bool nextIsLower = i + 1 < input.Length && char.IsLower(input[i + 1]);
                    if (char.IsLower(prev) || char.IsDigit(prev) || (nextIsLower && char.IsUpper(prev)))
                        sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
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
            var genResult = service.GenerateAll(config);

            if (!genResult.IsSuccess || genResult.CreatedFiles.Count == 0)
            {
                ResultText = genResult.ToSummary();
                HasError = true;
                HasResult = true;
                return;
            }

            // Gọi đăng ký hệ thống sau khi đã sinh file
            RegisterToSystem();

            ResultText = genResult.ToSummary() + "\n\n" + RegistrationLog;
            HasError = HasRegistrationError;
            IsSuccess = IsRegistered;
            HasResult = true;
        }

        [RelayCommand]
        private void UpdateNodeVisuals()
        {
            HasResult = false;
            ResultText = string.Empty;
            HasError = false;
            IsSuccess = false;

            if (string.IsNullOrWhiteSpace(SelectedExistingNode))
            {
                ResultText = "❌ Vui lòng chọn Node cần sửa.";
                HasError = true;
                HasResult = true;
                return;
            }

            var service = new NodeGeneratorService();
            var result = service.UpdateExistingNodeVisuals(
                ProjectRoot, 
                SelectedExistingNode, 
                EditColorKey, 
                EditIconKey, 
                EditInputPortColorKey, 
                EditOutputPortColorKey
            );

            ResultText = result.ToSummary();
            HasError = !result.IsSuccess;
            IsSuccess = result.IsSuccess;
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

        // ─── Register to System command ───────────────────────────────────────

        [RelayCommand]
        private void RegisterToSystem()
        {
            RegistrationLog = string.Empty;
            HasRegistrationResult = false;
            HasRegistrationError = false;
            IsRegistered = false;

            var config = BuildConfig();
            var validation = ValidateConfig(config);
            if (!string.IsNullOrEmpty(validation))
            {
                RegistrationLog = validation;
                HasRegistrationError = true;
                HasRegistrationResult = true;
                return;
            }

            var service = new NodeGeneratorService();
            var result = service.AutoRegisterToSystem(config);

            RegistrationLog = result.ToSummary();
            HasRegistrationError = !result.IsSuccess;
            IsRegistered = result.IsSuccess;
            HasRegistrationResult = true;
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
                DefaultInputCount = DefaultInputCount,
                HasCheckboxToToggleInputs = HasCheckboxToToggleInputs,
                HasOutputsPanel = HasOutputsPanel,
                HasDynamicInputs = HasDynamicInputs,
                HasCustomKeyOverride = HasCustomKeyOverride,
                ProjectRoot = ProjectRoot?.Trim() ?? string.Empty,
                PaletteCategory = PaletteCategory?.Trim() ?? "Screen",
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
                $"\n✅ Sau khi tạo xong, nhấn 🚀 Đăng ký vào Hệ thống để tự động:\n" +
                $"  • Thêm vào TemplateFactory.cs\n" +
                $"  • Thêm vào _NodeRenderer.cs (field + ctor + map)\n" +
                $"  • Thêm vào ServiceCollectionExtensions.cs\n" +
                $"  • Thêm icon mapping vào TemplateNodeHandler.cs\n" +
                $"  • Thêm palette Border vào WorkflowEditorWindow.xaml";
        }
    }
}
