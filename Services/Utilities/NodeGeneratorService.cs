using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FlowMy.Services.Utilities
{
    /// <summary>
    /// Cấu hình để sinh base node files.
    /// </summary>
    public sealed class NodeGeneratorConfig
    {
        /// <summary>Tên class (VD: "HelloWorld" → sẽ sinh HelloWorldNode, HelloWorldNodeDialog, ...)</summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>Title hiển thị mặc định trên canvas</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Icon key (VD: "star duotone-regular")</summary>
        public string IconKey { get; set; } = "circle-nodes duotone-regular";

        /// <summary>Color key (VD: "Info", "Primary", "SunsetOrange")</summary>
        public string ColorKey { get; set; } = "Info";

        /// <summary>Danh sách port INPUT: mỗi phần tử là ColorKey của port đó</summary>
        public List<string> InputPortColorKeys { get; set; } = new() { "Info" };

        /// <summary>Danh sách port OUTPUT: mỗi phần tử là ColorKey của port đó</summary>
        public List<string> OutputPortColorKeys { get; set; } = new() { "SunsetOrange" };

        /// <summary>Có section "Input từ node khác" trong dialog không (NodeSearchComboBox)</summary>
        public bool HasInputSection { get; set; } = true;

        /// <summary>Có OutputsPanel trong dialog không</summary>
        public bool HasOutputsPanel { get; set; } = true;

        /// <summary>Có DynamicInputs không (ItemsControl add/remove)</summary>
        public bool HasDynamicInputs { get; set; } = false;

        /// <summary>Thêm NodeType mới vào enum không (false = dùng Generic)</summary>
        public bool AddNewNodeType { get; set; } = true;

        /// <summary>Tên NodeType enum value (nếu AddNewNodeType = true). Rỗng = dùng NodeName</summary>
        public string NodeTypeName { get; set; } = string.Empty;

        /// <summary>Danh sách TextBox tùy chỉnh trong dialog: Label → BindingPath</summary>
        public List<DialogFieldConfig> CustomTextBoxes { get; set; } = new();

        /// <summary>Danh sách ComboBox tùy chỉnh trong dialog: Label → BindingPath</summary>
        public List<DialogFieldConfig> CustomComboBoxes { get; set; } = new();

        /// <summary>Danh sách CheckBox trong dialog</summary>
        public List<DialogFieldConfig> CustomCheckBoxes { get; set; } = new();

        /// <summary>Danh sách RadioButton group: tên group → danh sách options</summary>
        public List<RadioGroupConfig> RadioGroups { get; set; } = new();

        /// <summary>Các output key mà node này sẽ produce (sẽ thêm vào DynamicOutputs trong constructor)</summary>
        public List<string> OutputKeys { get; set; } = new();

        /// <summary>Thư mục gốc của project (để viết file)</summary>
        public string ProjectRoot { get; set; } = string.Empty;

        // Derived helpers
        public string EffectiveNodeTypeName => string.IsNullOrWhiteSpace(NodeTypeName) ? NodeName : NodeTypeName;
        public string NodeClassName => $"{NodeName}Node";
        public string ControlClassName => $"{NodeName}NodeControl";
        public string DialogClassName => $"{NodeName}NodeDialog";
        public string ViewModelClassName => $"{NodeName}NodeDialogViewModel";
        public string RendererClassName => $"{NodeName}NodeRenderer";
    }

    public sealed class DialogFieldConfig
    {
        public string Label { get; set; } = string.Empty;
        public string BindingPath { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
    }

    public sealed class RadioGroupConfig
    {
        public string GroupLabel { get; set; } = string.Empty;
        public string BindingPath { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
    }

    /// <summary>
    /// Service sinh base node files từ config. Không ghi đè file nếu đã tồn tại.
    /// </summary>
    public sealed class NodeGeneratorService
    {
        /// <summary>
        /// Sinh tất cả file và trả về danh sách file đã tạo + lỗi nếu có.
        /// </summary>
        public NodeGenerationResult GenerateAll(NodeGeneratorConfig config)
        {
            var result = new NodeGenerationResult();

            if (string.IsNullOrWhiteSpace(config.NodeName))
            {
                result.Errors.Add("NodeName không được để trống.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(config.ProjectRoot) || !Directory.Exists(config.ProjectRoot))
            {
                result.Errors.Add($"ProjectRoot không hợp lệ: '{config.ProjectRoot}'");
                return result;
            }

            // 1. Node Model
            TryWrite(result, Path.Combine(config.ProjectRoot, "Models", "Nodes", $"{config.NodeClassName}.cs"),
                GenerateNodeModel(config));

            // 2. NodeControl
            TryWrite(result, Path.Combine(config.ProjectRoot, "Views", "NodeControls", $"{config.ControlClassName}.cs"),
                GenerateNodeControl(config));

            // 3. Dialog XAML
            TryWrite(result, Path.Combine(config.ProjectRoot, "Views", "Overlays", $"{config.DialogClassName}.xaml"),
                GenerateDialogXaml(config));

            // 4. Dialog Code-behind
            TryWrite(result, Path.Combine(config.ProjectRoot, "Views", "Overlays", $"{config.DialogClassName}.xaml.cs"),
                GenerateDialogCodeBehind(config));

            // 5. ViewModel
            TryWrite(result, Path.Combine(config.ProjectRoot, "ViewModels", $"{config.ViewModelClassName}.cs"),
                GenerateViewModel(config));

            // 6. Renderer
            TryWrite(result, Path.Combine(config.ProjectRoot, "Services", "Rendering", $"{config.RendererClassName}.cs"),
                GenerateRenderer(config));

            // 7. NodeType.cs — append enum value nếu cần
            if (config.AddNewNodeType)
            {
                TryAppendNodeType(result, config);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // FILE GENERATORS
        // ─────────────────────────────────────────────────────────────────────────

        public string GenerateNodeModel(NodeGeneratorConfig c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using FlowMy.Models;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.ObjectModel;");
            sb.AppendLine();
            sb.AppendLine("namespace FlowMy.Models.Nodes");
            sb.AppendLine("{");
            sb.AppendLine($"    // ✅ KHÔNG thêm INotifyPropertyChanged — WorkflowNode đã implement");
            sb.AppendLine($"    // ✅ KHÔNG khai báo lại: PropertyChanged, OnPropertyChanged, TitleDisplayMode, TitleColorMode, TitleColorKey");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {c.Title} node — được tạo tự động bởi NodeGeneratorService.");
            sb.AppendLine($"    /// TODO: Thêm properties đặc thù, logic, và mô tả chi tiết.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public sealed class {c.NodeClassName} : WorkflowNode");
            sb.AppendLine("    {");

            // Properties placeholder
            sb.AppendLine("        // TODO: Khai báo properties đặc thù của node ở đây.");
            sb.AppendLine("        // Ví dụ:");
            sb.AppendLine("        // private string _someProperty = string.Empty;");
            sb.AppendLine("        // public string SomeProperty");
            sb.AppendLine("        // {");
            sb.AppendLine("        //     get => _someProperty;");
            sb.AppendLine("        //     set { if (_someProperty != value) { _someProperty = value; OnPropertyChanged(); } }");
            sb.AppendLine("        // }");
            sb.AppendLine();

            // Output keys as DynamicOutputs
            if (c.OutputKeys.Count > 0)
            {
                sb.AppendLine("        // Output keys mà node này produce:");
                foreach (var key in c.OutputKeys)
                {
                    sb.AppendLine($"        public const string OutputKey_{SanitizeName(key)} = \"{key}\";");
                }
                sb.AppendLine();
            }

            // Constructor
            sb.AppendLine($"        public {c.NodeClassName}()");
            sb.AppendLine("        {");
            var nodeTypeValue = c.AddNewNodeType ? $"NodeType.{c.EffectiveNodeTypeName}" : "NodeType.Generic";
            sb.AppendLine($"            Type = {nodeTypeValue};");
            sb.AppendLine($"            Title = \"{c.Title}\";");
            sb.AppendLine();

            // DynamicOutputs
            if (c.OutputKeys.Count > 0)
            {
                sb.AppendLine("            // Thêm output keys vào DynamicOutputs");
                foreach (var key in c.OutputKeys)
                {
                    sb.AppendLine($"            DynamicOutputs.Add(new WorkflowDynamicDataPort {{ Key = \"{key}\", DisplayName = \"{key}\", OutputType = WorkflowDataType.String }});");
                }
                sb.AppendLine();
            }

            sb.AppendLine("            // ⚠️ KHÔNG thêm Ports ở đây — TemplateFactory sẽ tạo port để tránh duplicate.");
            sb.AppendLine("            // Ports sẽ được tạo trong TemplateFactory.CreateYourNode()");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public string GenerateNodeControl(NodeGeneratorConfig c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using FlowMy.Controls;");
            sb.AppendLine("using FlowMy.Converters;");
            sb.AppendLine("using FlowMy.Models;");
            sb.AppendLine("using FlowMy.Models.Nodes;");
            sb.AppendLine("using FlowMy.Services.Interaction;");
            sb.AppendLine("using FlowMy.Views.NodeControls.Helpers;");
            sb.AppendLine($"using FlowMy.Views.Overlays;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Windows;");
            sb.AppendLine("using System.Windows.Controls;");
            sb.AppendLine("using System.Windows.Input;");
            sb.AppendLine("using System.Windows.Media;");
            sb.AppendLine("using System.Windows.Media.Effects;");
            sb.AppendLine();
            sb.AppendLine("namespace FlowMy.Views.NodeControls");
            sb.AppendLine("{");
            sb.AppendLine($"    public static class {c.ControlClassName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static Border CreateBorder({c.NodeClassName} node, Window? ownerWindow, IWorkflowEditorHost? host = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (host == null) throw new ArgumentNullException(nameof(host));");
            sb.AppendLine();
            sb.AppendLine("            // ─── 1. ICON ───");
            sb.AppendLine("            var iconConverter = new IconKeyToPathConverter();");
            sb.AppendLine("            var iconUri = iconConverter.Convert(null, typeof(Uri),");
            sb.AppendLine($"                \"{c.IconKey}\",");
            sb.AppendLine("                System.Globalization.CultureInfo.CurrentCulture) as Uri;");
            sb.AppendLine("            var iconSvg = new SvgViewboxEx");
            sb.AppendLine("            {");
            sb.AppendLine("                Source = iconUri,");
            sb.AppendLine("                Width = 32, Height = 32,");
            sb.AppendLine("                HorizontalAlignment = HorizontalAlignment.Center,");
            sb.AppendLine("                VerticalAlignment = VerticalAlignment.Center,");
            sb.AppendLine("                Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey)");
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            // ─── 2. GRID ───");
            sb.AppendLine("            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };");
            sb.AppendLine("            grid.Children.Add(iconSvg);");
            sb.AppendLine();
            sb.AppendLine("            // ─── 3. TITLE TEXTBLOCK ───");
            sb.AppendLine("            var titleTextBlock = new TextBlock");
            sb.AppendLine("            {");
            sb.AppendLine($"                Text = node.Title ?? \"{c.Title}\",");
            sb.AppendLine("                FontSize = 12,");
            sb.AppendLine("                FontWeight = FontWeights.SemiBold,");
            sb.AppendLine("                Foreground = BaseNodeControlHelper.ResolveTitleBrush(");
            sb.AppendLine("                    node.TitleColorMode, node.TitleColorKey, node.NodeBrush),");
            sb.AppendLine("                HorizontalAlignment = HorizontalAlignment.Center,");
            sb.AppendLine("                VerticalAlignment = VerticalAlignment.Top,");
            sb.AppendLine("                TextAlignment = TextAlignment.Center,");
            sb.AppendLine("                IsHitTestVisible = false,");
            sb.AppendLine("                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always");
            sb.AppendLine("                    ? Visibility.Visible : Visibility.Collapsed");
            sb.AppendLine("            };");
            sb.AppendLine("            node.TitleTextBlockUI = titleTextBlock; // ⚠️ BẮT BUỘC");
            sb.AppendLine();
            sb.AppendLine("            // ─── 4. BORDER ───");
            sb.AppendLine("            var border = new Border");
            sb.AppendLine("            {");
            sb.AppendLine("                Child = grid,");
            sb.AppendLine("                Background = node.NodeBrush,");
            sb.AppendLine("                BorderBrush = new SolidColorBrush(Colors.White),");
            sb.AppendLine("                BorderThickness = new Thickness(2),");
            sb.AppendLine("                CornerRadius = new CornerRadius(10),");
            sb.AppendLine("                Cursor = Cursors.Hand,");
            sb.AppendLine("                Effect = new DropShadowEffect");
            sb.AppendLine("                {");
            sb.AppendLine("                    Color = Colors.Black, Direction = 270,");
            sb.AppendLine("                    ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5");
            sb.AppendLine("                },");
            sb.AppendLine("                Tag = node // ⚠️ BẮT BUỘC");
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            // ─── 5. CUSTOM PROPERTY HANDLERS ───");
            sb.AppendLine("            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>");
            sb.AppendLine("            {");
            sb.AppendLine("                [nameof(WorkflowNode.ColorKey)] = ctx =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    iconSvg.Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);");
            sb.AppendLine("                },");
            sb.AppendLine("                // TODO: Thêm handlers cho properties đặc thù nếu cần:");
            sb.AppendLine("                // [nameof(NodeClassName.SomeProperty)] = ctx => { ... }");
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            // ─── 6. FLUENT API ───");
            sb.AppendLine("            BaseNodeControlHelper");
            sb.AppendLine("                .Initialize(border, titleTextBlock, node, host)");
            sb.AppendLine("                .WithTitleManagement()");
            sb.AppendLine("                .WithHoverBehavior()");
            sb.AppendLine("                .WithKeyboardPorts()");
            sb.AppendLine("                .WithPropertySync(customPropertyHandlers)");
            sb.AppendLine($"                .WithDialogSupport(ctx => new {c.DialogClassName}(");
            sb.AppendLine("                    node, host, ownerWindow ?? Application.Current?.MainWindow))");
            sb.AppendLine("                .WithCleanup()");
            sb.AppendLine("                .WithVisibilitySync()");
            sb.AppendLine("                .WithCanvasIntegration()");
            sb.AppendLine("                .Build();");
            sb.AppendLine();
            sb.AppendLine("            return border;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public string GenerateDialogXaml(NodeGeneratorConfig c)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<local:BaseNodeDialog x:Class=\"FlowMy.Views.Overlays.{c.DialogClassName}\"");
            sb.AppendLine("    xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
            sb.AppendLine("    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
            sb.AppendLine("    xmlns:controls=\"clr-namespace:FlowMy.Controls\"");
            sb.AppendLine("    xmlns:local=\"clr-namespace:FlowMy.Views.Overlays\"");
            sb.AppendLine($"    Title=\"{c.Title}\"");
            sb.AppendLine("    WindowStyle=\"None\" ResizeMode=\"CanResize\"");
            sb.AppendLine("    AllowsTransparency=\"True\" Background=\"Transparent\"");
            sb.AppendLine("    ShowInTaskbar=\"False\" Topmost=\"True\"");
            sb.AppendLine("    Width=\"520\" MinWidth=\"420\" MinHeight=\"420\">");
            sb.AppendLine();
            sb.AppendLine("    <Border CornerRadius=\"12\" Padding=\"0\" Style=\"{DynamicResource DialogOuterBorder}\">");
            sb.AppendLine("        <Grid>");
            sb.AppendLine("            <Grid.RowDefinitions>");
            sb.AppendLine("                <RowDefinition Height=\"Auto\"/>");
            sb.AppendLine("                <RowDefinition Height=\"*\"/>");
            sb.AppendLine("            </Grid.RowDefinitions>");
            sb.AppendLine();
            sb.AppendLine("            <!-- HEADER -->");
            sb.AppendLine("            <Border Grid.Row=\"0\" Style=\"{DynamicResource DialogHeaderBorder}\" Padding=\"16,12\">");
            sb.AppendLine("                <Grid>");
            sb.AppendLine("                    <Grid.ColumnDefinitions>");
            sb.AppendLine("                        <ColumnDefinition Width=\"*\"/>");
            sb.AppendLine("                        <ColumnDefinition Width=\"Auto\"/>");
            sb.AppendLine("                    </Grid.ColumnDefinitions>");
            sb.AppendLine("                    <TextBox x:Name=\"TitleTextBox\" Grid.Column=\"0\"");
            sb.AppendLine("                             Text=\"{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}\"");
            sb.AppendLine("                             Style=\"{DynamicResource BaseTextBoxV2}\" FontSize=\"16\"");
            sb.AppendLine("                             Padding=\"0,4\" VerticalContentAlignment=\"Center\" Cursor=\"IBeam\"/>");
            sb.AppendLine("                    <StackPanel Grid.Column=\"1\" Orientation=\"Horizontal\" VerticalAlignment=\"Center\">");
            sb.AppendLine("                        <Button Width=\"24\" Height=\"24\" Content=\"▶\" FontSize=\"12\" Padding=\"0\" Margin=\"8,0,0,0\"");
            sb.AppendLine("                                Style=\"{DynamicResource PrimaryButton}\" Cursor=\"Hand\" ToolTip=\"Chạy logic node này\"");
            sb.AppendLine("                                Command=\"{Binding RunSingleNodeCommand}\"/>");
            sb.AppendLine("                        <Button x:Name=\"CloseButton\" Width=\"24\" Height=\"24\" Padding=\"0\" Content=\"✕\" FontSize=\"12\"");
            sb.AppendLine("                                FontWeight=\"Bold\" Margin=\"8,0,0,0\" Cursor=\"Hand\"");
            sb.AppendLine("                                Style=\"{DynamicResource DangerButton}\" Click=\"CloseButton_Click\"/>");
            sb.AppendLine("                    </StackPanel>");
            sb.AppendLine("                </Grid>");
            sb.AppendLine("            </Border>");
            sb.AppendLine();
            sb.AppendLine("            <!-- CONTENT -->");
            sb.AppendLine("            <TabControl Grid.Row=\"1\" Background=\"Transparent\" BorderThickness=\"0\">");
            sb.AppendLine();
            sb.AppendLine("                <!-- TAB: LOGIC -->");
            sb.AppendLine("                <TabItem Header=\"Logic\" Style=\"{StaticResource HttpTabItemStyle}\">");
            sb.AppendLine("                    <ScrollViewer VerticalScrollBarVisibility=\"Auto\" Padding=\"16\">");
            sb.AppendLine("                        <StackPanel>");
            sb.AppendLine();
            sb.AppendLine("                            <!-- 🎨 Cấu hình hiển thị -->");
            sb.AppendLine("                            <Border Background=\"{DynamicResource WindowBackground}\"");
            sb.AppendLine("                                    BorderBrush=\"{DynamicResource ControlBorderBrush}\"");
            sb.AppendLine("                                    BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"12\" Margin=\"0,0,0,12\">");
            sb.AppendLine("                                <StackPanel>");
            sb.AppendLine("                                    <TextBlock Text=\"🎨 Cấu hình hiển thị\" Foreground=\"{DynamicResource TextBrush}\"");
            sb.AppendLine("                                               FontSize=\"12\" FontWeight=\"SemiBold\" Margin=\"0,0,0,10\"/>");
            sb.AppendLine("                                    <TextBlock Text=\"Hiển thị tiêu đề\" Foreground=\"{DynamicResource TextMuted}\"");
            sb.AppendLine("                                               FontSize=\"10\" Margin=\"0,0,0,4\"/>");
            sb.AppendLine("                                    <ComboBox Height=\"32\" Style=\"{DynamicResource BaseComboBox}\" Margin=\"0,0,0,8\"");
            sb.AppendLine("                                              ItemsSource=\"{Binding TitleDisplayModeOptions}\"");
            sb.AppendLine("                                              SelectedValuePath=\"Value\" DisplayMemberPath=\"DisplayName\"");
            sb.AppendLine("                                              SelectedValue=\"{Binding TitleDisplayMode, Mode=TwoWay}\"/>");
            sb.AppendLine("                                    <TextBlock Text=\"Màu tiêu đề\" Foreground=\"{DynamicResource TextMuted}\"");
            sb.AppendLine("                                               FontSize=\"10\" Margin=\"0,0,0,4\"/>");
            sb.AppendLine("                                    <Grid>");
            sb.AppendLine("                                        <Grid.ColumnDefinitions>");
            sb.AppendLine("                                            <ColumnDefinition Width=\"*\"/>");
            sb.AppendLine("                                            <ColumnDefinition Width=\"Auto\"/>");
            sb.AppendLine("                                        </Grid.ColumnDefinitions>");
            sb.AppendLine("                                        <ComboBox x:Name=\"TitleColorComboBox\" Grid.Column=\"0\" Height=\"32\"");
            sb.AppendLine("                                                  Style=\"{DynamicResource BaseComboBox}\"");
            sb.AppendLine("                                                  ItemsSource=\"{Binding TitleColorOptions}\"");
            sb.AppendLine("                                                  SelectedValuePath=\"Key\" DisplayMemberPath=\"DisplayName\"");
            sb.AppendLine("                                                  SelectedValue=\"{Binding TitleColorKey, Mode=TwoWay}\"");
            sb.AppendLine("                                                  SelectionChanged=\"TitleColorComboBox_SelectionChanged\"/>");
            sb.AppendLine("                                        <Border x:Name=\"TitleColorPreview\" Grid.Column=\"1\"");
            sb.AppendLine("                                                Width=\"32\" Height=\"32\" CornerRadius=\"6\" Margin=\"8,0,0,0\"");
            sb.AppendLine("                                                BorderBrush=\"{DynamicResource ControlBorderBrush}\" BorderThickness=\"1\"/>");
            sb.AppendLine("                                    </Grid>");
            sb.AppendLine("                                </StackPanel>");
            sb.AppendLine("                            </Border>");

            // Input section (nếu có)
            if (c.HasInputSection)
            {
                sb.AppendLine();
                sb.AppendLine("                            <!-- 📍 Input từ node khác -->");
                sb.AppendLine("                            <Border Background=\"{DynamicResource WindowBackground}\"");
                sb.AppendLine("                                    BorderBrush=\"{DynamicResource ControlBorderBrush}\"");
                sb.AppendLine("                                    BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"12\" Margin=\"0,0,0,12\">");
                sb.AppendLine("                                <StackPanel>");
                sb.AppendLine("                                    <TextBlock Text=\"📍 Input — Dữ liệu đầu vào\" Foreground=\"{DynamicResource TextBrush}\"");
                sb.AppendLine("                                               FontSize=\"11\" FontWeight=\"SemiBold\" Margin=\"0,0,0,6\"/>");
                sb.AppendLine("                                    <TextBlock Foreground=\"{DynamicResource TextMuted}\" FontSize=\"10\"");
                sb.AppendLine("                                               TextWrapping=\"Wrap\" Margin=\"0,0,0,10\">");
                sb.AppendLine("                                        <Run Text=\"Chọn node nguồn và key output để lấy dữ liệu.\"/>");
                sb.AppendLine("                                    </TextBlock>");
                sb.AppendLine("                                    <Grid>");
                sb.AppendLine("                                        <Grid.ColumnDefinitions>");
                sb.AppendLine("                                            <ColumnDefinition Width=\"*\"/>");
                sb.AppendLine("                                            <ColumnDefinition Width=\"8\"/>");
                sb.AppendLine("                                            <ColumnDefinition Width=\"*\"/>");
                sb.AppendLine("                                        </Grid.ColumnDefinitions>");
                sb.AppendLine("                                        <StackPanel Grid.Column=\"0\">");
                sb.AppendLine("                                            <TextBlock Text=\"Node\" Foreground=\"{DynamicResource TextMuted}\"");
                sb.AppendLine("                                                       FontSize=\"10\" Margin=\"0,0,0,4\"/>");
                sb.AppendLine("                                            <controls:NodeSearchComboBoxUserControl x:Name=\"SourceNodeCombo\" Height=\"32\"");
                sb.AppendLine("                                                      ItemsSource=\"{Binding AvailableNodeOptions}\"");
                sb.AppendLine("                                                      SelectedValuePath=\"NodeId\" DisplayMemberPath=\"Title\"");
                sb.AppendLine("                                                      SelectedValue=\"{Binding SourceNodeId, Mode=TwoWay}\"/>");
                sb.AppendLine("                                        </StackPanel>");
                sb.AppendLine("                                        <StackPanel Grid.Column=\"2\">");
                sb.AppendLine("                                            <TextBlock Text=\"Key\" Foreground=\"{DynamicResource TextMuted}\"");
                sb.AppendLine("                                                       FontSize=\"10\" Margin=\"0,0,0,4\"/>");
                sb.AppendLine("                                            <ComboBox x:Name=\"SourceKeyCombo\" Height=\"32\" Style=\"{DynamicResource BaseComboBox}\"");
                sb.AppendLine("                                                      ItemsSource=\"{Binding SourceKeyOptions}\"");
                sb.AppendLine("                                                      SelectedValue=\"{Binding SourceOutputKey, Mode=TwoWay}\"/>");
                sb.AppendLine("                                        </StackPanel>");
                sb.AppendLine("                                    </Grid>");
                sb.AppendLine("                                </StackPanel>");
                sb.AppendLine("                            </Border>");
            }

            // Dynamic Inputs panel (nếu có)
            if (c.HasDynamicInputs)
            {
                sb.AppendLine();
                sb.AppendLine("                            <!-- 🔄 Dynamic Inputs -->");
                sb.AppendLine("                            <TextBlock Text=\"Inputs\" Foreground=\"{DynamicResource TextBrush}\"");
                sb.AppendLine("                                       FontSize=\"12\" FontWeight=\"SemiBold\" Margin=\"0,0,0,6\"/>");
                sb.AppendLine("                            <Border Style=\"{DynamicResource DialogOuterBorder}\" CornerRadius=\"8\" Padding=\"10\" Margin=\"0,0,0,12\">");
                sb.AppendLine("                                <StackPanel x:Name=\"InputsPanel\"/>");
                sb.AppendLine("                            </Border>");
            }

            // Custom TextBoxes
            foreach (var tb in c.CustomTextBoxes)
            {
                sb.AppendLine();
                sb.AppendLine($"                            <!-- 🎯 {tb.Label} -->");
                sb.AppendLine("                            <Border Background=\"{DynamicResource WindowBackground}\"");
                sb.AppendLine("                                    BorderBrush=\"{DynamicResource ControlBorderBrush}\"");
                sb.AppendLine("                                    BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"12\" Margin=\"0,0,0,12\">");
                sb.AppendLine("                                <StackPanel>");
                sb.AppendLine($"                                    <TextBlock Text=\"🎯 {tb.Label}\" Foreground=\"{{DynamicResource TextBrush}}\"");
                sb.AppendLine("                                               FontSize=\"11\" FontWeight=\"SemiBold\" Margin=\"0,0,0,6\"/>");
                if (!string.IsNullOrWhiteSpace(tb.Placeholder))
                {
                    sb.AppendLine($"                                    <TextBlock Text=\"{tb.Placeholder}\" Foreground=\"{{DynamicResource TextMuted}}\"");
                    sb.AppendLine("                                               FontSize=\"10\" Margin=\"0,0,0,6\"/>");
                }
                sb.AppendLine($"                                    <TextBox Height=\"32\" Style=\"{{DynamicResource BaseTextBoxV2}}\"");
                sb.AppendLine($"                                             Text=\"{{Binding {tb.BindingPath}, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}\"/>");
                sb.AppendLine("                                </StackPanel>");
                sb.AppendLine("                            </Border>");
            }

            // Custom ComboBoxes
            foreach (var cb in c.CustomComboBoxes)
            {
                sb.AppendLine();
                sb.AppendLine($"                            <!-- ⚙️ {cb.Label} -->");
                sb.AppendLine("                            <Border Background=\"{DynamicResource WindowBackground}\"");
                sb.AppendLine("                                    BorderBrush=\"{DynamicResource ControlBorderBrush}\"");
                sb.AppendLine("                                    BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"12\" Margin=\"0,0,0,12\">");
                sb.AppendLine("                                <StackPanel>");
                sb.AppendLine($"                                    <TextBlock Text=\"⚙️ {cb.Label}\" Foreground=\"{{DynamicResource TextBrush}}\"");
                sb.AppendLine("                                               FontSize=\"11\" FontWeight=\"SemiBold\" Margin=\"0,0,0,6\"/>");
                sb.AppendLine($"                                    <ComboBox Height=\"32\" Style=\"{{DynamicResource BaseComboBox}}\"");
                sb.AppendLine($"                                              ItemsSource=\"{{Binding {cb.BindingPath}Options}}\"");
                sb.AppendLine($"                                              SelectedValue=\"{{Binding {cb.BindingPath}, Mode=TwoWay}}\"/>");
                sb.AppendLine("                                </StackPanel>");
                sb.AppendLine("                            </Border>");
            }

            // Custom CheckBoxes
            if (c.CustomCheckBoxes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("                            <!-- ✅ Tùy chọn -->");
                sb.AppendLine("                            <Border Background=\"{DynamicResource WindowBackground}\"");
                sb.AppendLine("                                    BorderBrush=\"{DynamicResource ControlBorderBrush}\"");
                sb.AppendLine("                                    BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"12\" Margin=\"0,0,0,12\">");
                sb.AppendLine("                                <StackPanel>");
                sb.AppendLine("                                    <TextBlock Text=\"✅ Tùy chọn\" Foreground=\"{DynamicResource TextBrush}\"");
                sb.AppendLine("                                               FontSize=\"12\" FontWeight=\"SemiBold\" Margin=\"0,0,0,10\"/>");
                foreach (var chk in c.CustomCheckBoxes)
                {
                    sb.AppendLine($"                                    <CheckBox Content=\"{chk.Label}\" Margin=\"0,0,0,8\"");
                    sb.AppendLine($"                                              Foreground=\"{{DynamicResource TextBrush}}\"");
                    sb.AppendLine($"                                              IsChecked=\"{{Binding {chk.BindingPath}, Mode=TwoWay}}\"/>");
                }
                sb.AppendLine("                                </StackPanel>");
                sb.AppendLine("                            </Border>");
            }

            // Radio groups
            foreach (var rg in c.RadioGroups)
            {
                sb.AppendLine();
                sb.AppendLine($"                            <!-- 🔘 {rg.GroupLabel} -->");
                sb.AppendLine("                            <Border Background=\"{DynamicResource WindowBackground}\"");
                sb.AppendLine("                                    BorderBrush=\"{DynamicResource ControlBorderBrush}\"");
                sb.AppendLine("                                    BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"12\" Margin=\"0,0,0,12\">");
                sb.AppendLine("                                <StackPanel>");
                sb.AppendLine($"                                    <TextBlock Text=\"🔘 {rg.GroupLabel}\" Foreground=\"{{DynamicResource TextBrush}}\"");
                sb.AppendLine("                                               FontSize=\"11\" FontWeight=\"SemiBold\" Margin=\"0,0,0,8\"/>");
                foreach (var opt in rg.Options)
                {
                    sb.AppendLine($"                                    <RadioButton Content=\"{opt}\" Margin=\"0,0,0,4\"");
                    sb.AppendLine($"                                                 Foreground=\"{{DynamicResource TextBrush}}\"");
                    sb.AppendLine($"                                                 GroupName=\"{SanitizeName(rg.GroupLabel)}\"/>");
                    sb.AppendLine($"                                    <!-- TODO: Bind IsChecked theo {rg.BindingPath} -->");
                }
                sb.AppendLine("                                </StackPanel>");
                sb.AppendLine("                            </Border>");
            }

            // Outputs Panel
            if (c.HasOutputsPanel)
            {
                sb.AppendLine();
                sb.AppendLine("                            <!-- 📊 Outputs -->");
                sb.AppendLine("                            <TextBlock Text=\"Outputs\" Foreground=\"{DynamicResource TextBrush}\"");
                sb.AppendLine("                                       FontSize=\"12\" FontWeight=\"SemiBold\" Margin=\"0,0,0,6\"/>");
                sb.AppendLine("                            <Border Style=\"{DynamicResource DialogOuterBorder}\" CornerRadius=\"8\" Padding=\"10\">");
                sb.AppendLine("                                <StackPanel x:Name=\"OutputsPanel\"/>");
                sb.AppendLine("                            </Border>");
            }

            sb.AppendLine();
            sb.AppendLine("                        </StackPanel>");
            sb.AppendLine("                    </ScrollViewer>");
            sb.AppendLine("                </TabItem>");
            sb.AppendLine();
            sb.AppendLine("                <!-- TAB: CẤU HÌNH -->");
            sb.AppendLine("                <TabItem Header=\"Cấu hình\" Style=\"{StaticResource HttpTabItemStyle}\">");
            sb.AppendLine("                    <ScrollViewer VerticalScrollBarVisibility=\"Auto\" Padding=\"16\">");
            sb.AppendLine("                        <StackPanel>");
            sb.AppendLine();
            sb.AppendLine("                            <TextBlock Text=\"Vị trí cổng IN/OUT\" Foreground=\"{DynamicResource TextBrush}\"");
            sb.AppendLine("                                       FontSize=\"12\" FontWeight=\"SemiBold\" Margin=\"0,0,0,8\"/>");
            sb.AppendLine("                            <Grid Margin=\"0,0,0,16\">");
            sb.AppendLine("                                <Grid.ColumnDefinitions>");
            sb.AppendLine("                                    <ColumnDefinition Width=\"*\"/>");
            sb.AppendLine("                                    <ColumnDefinition Width=\"*\"/>");
            sb.AppendLine("                                </Grid.ColumnDefinitions>");
            sb.AppendLine("                                <StackPanel Grid.Column=\"0\" Margin=\"0,0,6,0\">");
            sb.AppendLine("                                    <TextBlock Text=\"Port IN\" Foreground=\"{DynamicResource TextMuted}\"");
            sb.AppendLine("                                               FontSize=\"10\" Margin=\"0,0,0,4\"/>");
            sb.AppendLine("                                    <ComboBox Height=\"32\" Style=\"{DynamicResource BaseComboBox}\"");
            sb.AppendLine("                                              ItemsSource=\"{Binding PortPositionOptions}\"");
            sb.AppendLine("                                              SelectedItem=\"{Binding InputPortPosition, Mode=TwoWay}\"/>");
            sb.AppendLine("                                </StackPanel>");
            sb.AppendLine("                                <StackPanel Grid.Column=\"1\" Margin=\"6,0,0,0\">");
            sb.AppendLine("                                    <TextBlock Text=\"Port OUT\" Foreground=\"{DynamicResource TextMuted}\"");
            sb.AppendLine("                                               FontSize=\"10\" Margin=\"0,0,0,4\"/>");
            sb.AppendLine("                                    <ComboBox Height=\"32\" Style=\"{DynamicResource BaseComboBox}\"");
            sb.AppendLine("                                              ItemsSource=\"{Binding PortPositionOptions}\"");
            sb.AppendLine("                                              SelectedItem=\"{Binding OutputPortPosition, Mode=TwoWay}\"/>");
            sb.AppendLine("                                </StackPanel>");
            sb.AppendLine("                            </Grid>");
            sb.AppendLine();
            sb.AppendLine("                            <TextBlock Text=\"Tái sử dụng flow\" Foreground=\"{DynamicResource TextBrush}\"");
            sb.AppendLine("                                       FontSize=\"12\" FontWeight=\"SemiBold\" Margin=\"0,0,0,8\"/>");
            sb.AppendLine("                            <ItemsControl ItemsSource=\"{Binding ReuseRoutes}\">");
            sb.AppendLine("                                <ItemsControl.ItemTemplate>");
            sb.AppendLine("                                    <DataTemplate>");
            sb.AppendLine("                                        <Border Margin=\"0,0,0,12\"");
            sb.AppendLine("                                                Background=\"{DynamicResource WindowBackground}\"");
            sb.AppendLine("                                                BorderBrush=\"{DynamicResource ControlBorderBrush}\"");
            sb.AppendLine("                                                BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"10\">");
            sb.AppendLine("                                            <Grid>");
            sb.AppendLine("                                                <Grid.ColumnDefinitions>");
            sb.AppendLine("                                                    <ColumnDefinition Width=\"2*\"/>");
            sb.AppendLine("                                                    <ColumnDefinition Width=\"2*\"/>");
            sb.AppendLine("                                                    <ColumnDefinition Width=\"2*\"/>");
            sb.AppendLine("                                                </Grid.ColumnDefinitions>");
            sb.AppendLine("                                                <StackPanel Grid.Column=\"0\">");
            sb.AppendLine("                                                    <TextBlock Text=\"Node IN\" Foreground=\"{DynamicResource TextBrush}\"");
            sb.AppendLine("                                                               FontSize=\"10\" Opacity=\"0.7\" Margin=\"0,0,0,4\"/>");
            sb.AppendLine("                                                    <TextBlock Text=\"{Binding IncomingNodeTitle}\"");
            sb.AppendLine("                                                               Foreground=\"{DynamicResource TextBrush}\"");
            sb.AppendLine("                                                               FontSize=\"11\" FontWeight=\"SemiBold\"");
            sb.AppendLine("                                                               TextTrimming=\"CharacterEllipsis\"/>");
            sb.AppendLine("                                                </StackPanel>");
            sb.AppendLine("                                                <StackPanel Grid.Column=\"1\">");
            sb.AppendLine("                                                    <TextBlock Text=\"Node OUT\" Foreground=\"{DynamicResource TextBrush}\"");
            sb.AppendLine("                                                               FontSize=\"10\" Opacity=\"0.7\" Margin=\"0,0,0,4\"/>");
            sb.AppendLine("                                                    <controls:NodeSearchComboBoxUserControl Height=\"32\"");
            sb.AppendLine("                                                              ItemsSource=\"{Binding OutgoingOptions}\"");
            sb.AppendLine("                                                              SelectedValuePath=\"NodeId\" DisplayMemberPath=\"Title\"");
            sb.AppendLine("                                                              SelectedValue=\"{Binding SelectedOutgoingNodeId, Mode=TwoWay}\"/>");
            sb.AppendLine("                                                </StackPanel>");
            sb.AppendLine("                                                <StackPanel Grid.Column=\"2\">");
            sb.AppendLine("                                                    <TextBlock Text=\"Kiểu line OUT\" Foreground=\"{DynamicResource TextBrush}\"");
            sb.AppendLine("                                                               FontSize=\"10\" Opacity=\"0.7\" Margin=\"0,0,0,4\"/>");
            sb.AppendLine("                                                    <ComboBox Height=\"32\" Style=\"{DynamicResource BaseComboBox}\"");
            sb.AppendLine("                                                              ItemsSource=\"{Binding DataContext.ConnectionLineStyleOptions, RelativeSource={RelativeSource AncestorType=Window}}\"");
            sb.AppendLine("                                                              SelectedValuePath=\"Key\" DisplayMemberPath=\"DisplayName\"");
            sb.AppendLine("                                                              SelectedValue=\"{Binding SelectedLineStyleKey, Mode=TwoWay}\"/>");
            sb.AppendLine("                                                </StackPanel>");
            sb.AppendLine("                                            </Grid>");
            sb.AppendLine("                                        </Border>");
            sb.AppendLine("                                    </DataTemplate>");
            sb.AppendLine("                                </ItemsControl.ItemTemplate>");
            sb.AppendLine("                            </ItemsControl>");
            sb.AppendLine();
            sb.AppendLine("                        </StackPanel>");
            sb.AppendLine("                    </ScrollViewer>");
            sb.AppendLine("                </TabItem>");
            sb.AppendLine();
            sb.AppendLine("            </TabControl>");
            sb.AppendLine("        </Grid>");
            sb.AppendLine("    </Border>");
            sb.AppendLine($"</local:BaseNodeDialog>");

            return sb.ToString();
        }

        public string GenerateDialogCodeBehind(NodeGeneratorConfig c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using FlowMy.Models.Nodes;");
            sb.AppendLine("using FlowMy.Services.Interaction;");
            sb.AppendLine("using FlowMy.ViewModels;");
            sb.AppendLine("using FlowMy.Views.Overlays;");
            sb.AppendLine("using System.Windows;");
            sb.AppendLine("using System.Windows.Controls;");
            sb.AppendLine();
            sb.AppendLine("namespace FlowMy.Views.Overlays");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {c.DialogClassName} : BaseNodeDialog");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {c.ViewModelClassName} _viewModel;");
            sb.AppendLine();
            sb.AppendLine($"        public {c.DialogClassName}({c.NodeClassName} node, IWorkflowEditorHost host, Window? owner)");
            sb.AppendLine("            : base()");
            sb.AppendLine("        {");
            sb.AppendLine("            InitializeComponent();");
            sb.AppendLine($"            _viewModel = new {c.ViewModelClassName}(node, host);");
            sb.AppendLine("            InitializeBase(_viewModel, owner);");
            sb.AppendLine();
            sb.AppendLine("            // Gọi nếu XAML có TitleColorPreview + TitleColorComboBox");
            sb.AppendLine("            UpdateTitleColorPreview();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // GetInputsPanel
            bool hasInputsPanel = c.HasDynamicInputs;
            if (hasInputsPanel)
            {
                sb.AppendLine("        protected override Panel? GetInputsPanel() => InputsPanel;");
            }
            else
            {
                sb.AppendLine("        protected override Panel? GetInputsPanel() => null;");
            }

            // GetOutputsPanel
            if (c.HasOutputsPanel)
            {
                sb.AppendLine("        protected override Panel? GetOutputsPanel() => OutputsPanel;");
            }
            else
            {
                sb.AppendLine("        protected override Panel? GetOutputsPanel() => null;");
            }

            sb.AppendLine();
            sb.AppendLine("        // CHỈ override nếu cần flush binding khi đóng bằng Alt+F4 hoặc X taskbar");
            sb.AppendLine("        // protected override void BeforeSaveOnClose()");
            sb.AppendLine("        // {");
            sb.AppendLine("        //     MyComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();");
            sb.AppendLine("        // }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public string GenerateViewModel(NodeGeneratorConfig c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using CommunityToolkit.Mvvm.ComponentModel;");
            sb.AppendLine("using FlowMy.Models;");
            sb.AppendLine("using FlowMy.Models.Nodes;");
            sb.AppendLine("using FlowMy.Services.Interaction;");
            sb.AppendLine("using FlowMy.ViewModels;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.ObjectModel;");
            sb.AppendLine();
            sb.AppendLine("namespace FlowMy.ViewModels");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {c.ViewModelClassName} : BaseNodeDialogViewModel");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {c.NodeClassName} _{LowerFirst(c.NodeName)}Node;");
            sb.AppendLine();
            sb.AppendLine("        // ─── Properties đặc thù ───");
            sb.AppendLine("        // TODO: Khai báo properties đặc thù với [ObservableProperty]:");
            sb.AppendLine("        // [ObservableProperty] private string _someProperty = string.Empty;");
            sb.AppendLine();

            if (c.HasInputSection)
            {
                sb.AppendLine("        // Properties cho input section — chọn node nguồn và key output của nó");
                sb.AppendLine("        [ObservableProperty] private string _sourceNodeId = string.Empty;");
                sb.AppendLine("        [ObservableProperty] private string _sourceOutputKey = string.Empty;");
                sb.AppendLine("        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();");
                sb.AppendLine("        // WorkflowOutputKeyOption chứa Key + DisplayName + Type — dùng SelectedValuePath=\"Key\"");
                sb.AppendLine("        public ObservableCollection<WorkflowOutputKeyOption> SourceKeyOptions { get; } = new();");
                sb.AppendLine();
                sb.AppendLine("        partial void OnSourceNodeIdChanged(string value)");
                sb.AppendLine("        {");
                sb.AppendLine("            // TODO: Lưu SourceNodeId vào node nếu node có property này:");
                sb.AppendLine($"            // _{LowerFirst(c.NodeName)}Node.SourceNodeId = value;");
                sb.AppendLine("            FillOutputKeys(value, SourceKeyOptions);");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        partial void OnSourceOutputKeyChanged(string value)");
                sb.AppendLine("        {");
                sb.AppendLine("            // TODO: Lưu SourceOutputKey vào node nếu node có property này:");
                sb.AppendLine($"            // _{LowerFirst(c.NodeName)}Node.SourceOutputKey = value;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            foreach (var tb in c.CustomTextBoxes)
            {
                var propName = LowerFirst(SanitizeName(tb.BindingPath));
                sb.AppendLine($"        [ObservableProperty] private string _{propName} = string.Empty;");
                sb.AppendLine($"        partial void On{SanitizeName(tb.BindingPath)}Changed(string value)");
                sb.AppendLine("        {");
                sb.AppendLine($"            // TODO: _{LowerFirst(c.NodeName)}Node.{tb.BindingPath} = value;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            foreach (var cb in c.CustomCheckBoxes)
            {
                var propName = LowerFirst(SanitizeName(cb.BindingPath));
                sb.AppendLine($"        [ObservableProperty] private bool _{propName};");
                sb.AppendLine($"        partial void On{SanitizeName(cb.BindingPath)}Changed(bool value)");
                sb.AppendLine("        {");
                sb.AppendLine($"            // TODO: _{LowerFirst(c.NodeName)}Node.{cb.BindingPath} = value;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine($"        public {c.ViewModelClassName}({c.NodeClassName} node, IWorkflowEditorHost host)");
            sb.AppendLine("            : base(node, host)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _{LowerFirst(c.NodeName)}Node = node ?? throw new ArgumentNullException(nameof(node));");
            sb.AppendLine();

            if (c.HasInputSection)
            {
                sb.AppendLine("            // Load node options và sync properties từ node");
                sb.AppendLine("            RefreshAllNodesWithOutputs(AvailableNodeOptions);");
                sb.AppendLine($"            SourceNodeId = _{LowerFirst(c.NodeName)}Node.SourceNodeId;");
                sb.AppendLine($"            SourceOutputKey = _{LowerFirst(c.NodeName)}Node.SourceOutputKey;");
                sb.AppendLine($"            FillOutputKeys(SourceNodeId, SourceKeyOptions);");
                sb.AppendLine();
            }

            sb.AppendLine("            // TODO: Sync thêm properties từ node:");
            sb.AppendLine("            // SomeProperty = node.SomeProperty;");
            sb.AppendLine();
            sb.AppendLine("            // Subscribe PropertyChanged cho properties đặc thù");
            sb.AppendLine("            node.PropertyChanged += (s, e) =>");
            sb.AppendLine("            {");
            sb.AppendLine("                // TODO: Xử lý khi node properties thay đổi từ bên ngoài");
            sb.AppendLine("                OnNodePropertyChanged(e.PropertyName ?? string.Empty);");
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        protected override string GetDefaultTitle() => \"{c.Title}\";");
            sb.AppendLine();
            sb.AppendLine("        // CHỈ override nếu cần lưu thêm properties ngoài Title/TitleDisplayMode/TitleColorMode");
            sb.AppendLine("        // protected override void OnSaveTitle()");
            sb.AppendLine("        // {");
            sb.AppendLine("        //     base.OnSaveTitle();");
            sb.AppendLine("        //     if (node.SomeProperty != SomeProperty)");
            sb.AppendLine("        //     {");
            sb.AppendLine("        //         node.SomeProperty = SomeProperty;");
            sb.AppendLine("        //         _host.RequestSyncDataPanels(immediate: true);");
            sb.AppendLine("        //     }");
            sb.AppendLine("        // }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public string GenerateRenderer(NodeGeneratorConfig c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using FlowMy.Models;");
            sb.AppendLine("using FlowMy.Models.Nodes;");
            sb.AppendLine("using FlowMy.Services.Interaction;");
            sb.AppendLine("using FlowMy.Views.NodeControls;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Windows;");
            sb.AppendLine("using System.Windows.Controls;");
            sb.AppendLine("using System.Windows.Media;");
            sb.AppendLine("using System.Windows.Shapes;");
            sb.AppendLine();
            sb.AppendLine("namespace FlowMy.Services.Rendering");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed class {c.RendererClassName} : INodeRenderer");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly PortRenderer _portRenderer;");
            sb.AppendLine("        private readonly IWorkflowEditorHostAccessor _hostAccessor;");
            sb.AppendLine("        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();");
            sb.AppendLine();
            sb.AppendLine($"        public {c.RendererClassName}(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)");
            sb.AppendLine("        {");
            sb.AppendLine("            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));");
            sb.AppendLine("            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void RenderNode(WorkflowNode node, Canvas canvas)");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (node is not {c.NodeClassName} {LowerFirst(c.NodeName)}Node) return;");
            sb.AppendLine();
            sb.AppendLine("            // 1. Tạo border từ NodeControl");
            sb.AppendLine($"            {LowerFirst(c.NodeName)}Node.Border = {c.ControlClassName}.CreateBorder(");
            sb.AppendLine($"                {LowerFirst(c.NodeName)}Node,");
            sb.AppendLine("                Host as Window ?? throw new InvalidOperationException(\"Host must be a Window.\"),");
            sb.AppendLine("                Host);");
            sb.AppendLine($"            {LowerFirst(c.NodeName)}Node.Border.Tag = {LowerFirst(c.NodeName)}Node;");
            sb.AppendLine();
            sb.AppendLine("            // 2. Apply chrome (execution badge, GPU optimization)");
            sb.AppendLine($"            NodeChrome.Apply({LowerFirst(c.NodeName)}Node.Border, {LowerFirst(c.NodeName)}Node, Host);");
            sb.AppendLine();
            sb.AppendLine("            // 3. Attach mouse handlers");
            sb.AppendLine($"            {LowerFirst(c.NodeName)}Node.Border.MouseDown  += Host.NodeMouseDown;");
            sb.AppendLine($"            {LowerFirst(c.NodeName)}Node.Border.MouseMove  += Host.NodeMouseMove;");
            sb.AppendLine($"            {LowerFirst(c.NodeName)}Node.Border.MouseUp    += Host.NodeMouseUp;");
            sb.AppendLine($"            {LowerFirst(c.NodeName)}Node.Border.MouseEnter += Host.NodeBorderMouseEnter;");
            sb.AppendLine($"            {LowerFirst(c.NodeName)}Node.Border.MouseLeave += Host.NodeBorderMouseLeave;");
            sb.AppendLine($"            {LowerFirst(c.NodeName)}Node.Border.ContextMenu = null;");
            sb.AppendLine();
            sb.AppendLine("            // 4. Đặt vị trí và thêm vào canvas");
            sb.AppendLine($"            Canvas.SetLeft({LowerFirst(c.NodeName)}Node.Border, {LowerFirst(c.NodeName)}Node.X);");
            sb.AppendLine($"            Canvas.SetTop({LowerFirst(c.NodeName)}Node.Border, {LowerFirst(c.NodeName)}Node.Y);");
            sb.AppendLine($"            canvas.Children.Add({LowerFirst(c.NodeName)}Node.Border);");
            sb.AppendLine($"            Host.ZIndexManager.InitializeNodeZIndex({LowerFirst(c.NodeName)}Node, {LowerFirst(c.NodeName)}Node.Border);");
            sb.AppendLine();
            sb.AppendLine("            // 5. Render ports");
            sb.AppendLine($"            foreach (var port in {LowerFirst(c.NodeName)}Node.Ports.Where(p => p.IsVisible))");
            sb.AppendLine("            {");
            sb.AppendLine("                var portColor = ResolvePortColor(port);");
            sb.AppendLine("                if (port.PortUI == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    port.PortUI = _portRenderer.CreatePort(portColor);");
            sb.AppendLine("                    port.PortUI.Tag = port;");
            sb.AppendLine("                }");
            sb.AppendLine("                else if (port.PortUI is Ellipse ellipse)");
            sb.AppendLine("                {");
            sb.AppendLine("                    ellipse.Fill = new SolidColorBrush(portColor);");
            sb.AppendLine("                }");
            sb.AppendLine($"                _portRenderer.UpdatePortsPositionOnSide({LowerFirst(c.NodeName)}Node, port.Position);");
            sb.AppendLine("                _portRenderer.EnsurePortAddedToCanvas(port);");
            sb.AppendLine($"                Host.ZIndexManager.SetPortZIndex({LowerFirst(c.NodeName)}Node, port.PortUI);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void UpdateNodePosition(WorkflowNode node, double x, double y)");
            sb.AppendLine("        {");
            sb.AppendLine("            node.X = x;");
            sb.AppendLine("            node.Y = y;");
            sb.AppendLine();
            sb.AppendLine("            if (node.Border != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                Canvas.SetLeft(node.Border, x);");
            sb.AppendLine("                Canvas.SetTop(node.Border, y);");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine($"            if (node is {c.NodeClassName} {LowerFirst(c.NodeName)}N && {LowerFirst(c.NodeName)}N.TitleTextBlockUI != null && Host.WorkflowCanvas != null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var title = {LowerFirst(c.NodeName)}N.TitleTextBlockUI;");
            sb.AppendLine("                if (!Host.WorkflowCanvas.Children.Contains(title))");
            sb.AppendLine("                {");
            sb.AppendLine("                    Host.WorkflowCanvas.Children.Add(title);");
            sb.AppendLine("                    Panel.SetZIndex(title, 20000);");
            sb.AppendLine("                }");
            sb.AppendLine("                if (node.Border != null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (title.ActualWidth == 0 || title.ActualHeight == 0)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        title.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));");
            sb.AppendLine("                        title.Arrange(new Rect(title.DesiredSize));");
            sb.AppendLine("                    }");
            sb.AppendLine("                    Canvas.SetLeft(title, x + (node.Border.ActualWidth / 2) - (title.ActualWidth / 2));");
            sb.AppendLine("                    Canvas.SetTop(title, y - title.ActualHeight - 4);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            foreach (var port in node.Ports.Where(p => p.IsVisible))");
            sb.AppendLine("            {");
            sb.AppendLine("                var portColor = ResolvePortColor(port);");
            sb.AppendLine("                if (port.PortUI == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    port.PortUI = _portRenderer.CreatePort(portColor);");
            sb.AppendLine("                    port.PortUI.Tag = port;");
            sb.AppendLine("                }");
            sb.AppendLine("                else if (port.PortUI is Ellipse ellipse)");
            sb.AppendLine("                {");
            sb.AppendLine("                    ellipse.Fill = new SolidColorBrush(portColor);");
            sb.AppendLine("                }");
            sb.AppendLine("                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);");
            sb.AppendLine("                _portRenderer.EnsurePortAddedToCanvas(port);");
            sb.AppendLine("                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            Host.SyncAllPortsZIndex(node);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void RemoveNode(WorkflowNode node, Canvas canvas)");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (node is {c.NodeClassName} {LowerFirst(c.NodeName)}N && {LowerFirst(c.NodeName)}N.TitleTextBlockUI != null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                if (canvas.Children.Contains({LowerFirst(c.NodeName)}N.TitleTextBlockUI))");
            sb.AppendLine($"                    canvas.Children.Remove({LowerFirst(c.NodeName)}N.TitleTextBlockUI);");
            sb.AppendLine($"                {LowerFirst(c.NodeName)}N.TitleTextBlockUI = null;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (node.Border != null && canvas.Children.Contains(node.Border))");
            sb.AppendLine("                canvas.Children.Remove(node.Border);");
            sb.AppendLine();
            sb.AppendLine("            foreach (var port in node.Ports)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))");
            sb.AppendLine("                    canvas.Children.Remove(port.PortUI);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void RemoveAllNodeVisuals(Canvas canvas)");
            sb.AppendLine("        {");
            sb.AppendLine("            var borders = canvas.Children.OfType<Border>()");
            sb.AppendLine("                .Where(b => b.Tag is WorkflowNode).ToList();");
            sb.AppendLine("            foreach (var b in borders) canvas.Children.Remove(b);");
            sb.AppendLine();
            sb.AppendLine("            var ports = canvas.Children.OfType<Ellipse>()");
            sb.AppendLine("                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18)).ToList();");
            sb.AppendLine("            foreach (var p in ports) canvas.Children.Remove(p);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static Color ResolvePortColor(NodePort port)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!string.IsNullOrWhiteSpace(port.ColorKey))");
            sb.AppendLine("            {");
            sb.AppendLine("                var c2 = GetColorFromTheme($\"{port.ColorKey}Brush\") ?? GetColorFromTheme(port.ColorKey);");
            sb.AppendLine("                if (c2.HasValue) return c2.Value;");
            sb.AppendLine("            }");
            sb.AppendLine("            return port.IsInput");
            sb.AppendLine("                ? (GetColorFromTheme(\"InfoBrush\") ?? Colors.Orange)");
            sb.AppendLine("                : (GetColorFromTheme(\"SunsetOrangeBrush\") ?? Colors.Cyan);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static Color? GetColorFromTheme(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { return (Application.Current.TryFindResource(key) as SolidColorBrush)?.Color; }");
            sb.AppendLine("            catch { return null; }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────────

        private static void TryWrite(NodeGenerationResult result, string path, string content)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(path))
                {
                    result.Skipped.Add(path);
                    return;
                }

                File.WriteAllText(path, content, Encoding.UTF8);
                result.CreatedFiles.Add(path);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Lỗi khi ghi '{path}': {ex.Message}");
            }
        }

        private static void TryAppendNodeType(NodeGenerationResult result, NodeGeneratorConfig config)
        {
            try
            {
                var path = Path.Combine(config.ProjectRoot, "Models", "Nodes", "NodeType.cs");
                if (!File.Exists(path))
                {
                    result.Errors.Add("Không tìm thấy NodeType.cs để thêm enum value.");
                    return;
                }

                var content = File.ReadAllText(path);
                var typeName = config.EffectiveNodeTypeName;

                // Kiểm tra đã có chưa
                if (content.Contains(typeName))
                {
                    result.Warnings.Add($"NodeType.{typeName} đã tồn tại trong NodeType.cs — bỏ qua.");
                    return;
                }

                // Tìm vị trí cuối enum (trước dấu } đóng enum) và thêm vào
                var lastBrace = content.LastIndexOf("    }");
                if (lastBrace < 0)
                {
                    result.Errors.Add("Không thể parse NodeType.cs để thêm enum value.");
                    return;
                }

                // Tìm dòng cuối cùng của enum (trước lastBrace)
                var beforeBrace = content.Substring(0, lastBrace).TrimEnd();
                // Thêm dấu phẩy sau entry cuối nếu chưa có
                if (!beforeBrace.EndsWith(","))
                    beforeBrace += ",";

                var newEntry = $"\r\n        /// <summary>{config.Title} node — được tạo tự động.</summary>\r\n        {typeName}";
                var newContent = beforeBrace + newEntry + "\r\n    }" + content.Substring(lastBrace + 5);
                File.WriteAllText(path, newContent, Encoding.UTF8);
                result.ModifiedFiles.Add(path);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Lỗi khi sửa NodeType.cs: {ex.Message}");
            }
        }

        private static string SanitizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Field";
            var sb = new StringBuilder();
            bool nextUpper = true;
            foreach (var ch in input)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(nextUpper ? char.ToUpper(ch) : ch);
                    nextUpper = false;
                }
                else
                {
                    nextUpper = true;
                }
            }
            return sb.Length > 0 ? sb.ToString() : "Field";
        }

        private static string LowerFirst(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToLower(s[0]) + s.Substring(1);

        // ─────────────────────────────────────────────────────────────────────────
        // AUTO-REGISTER TO SYSTEM
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tự động đăng ký node đã generate vào toàn bộ hệ thống:
        /// TemplateFactory, _NodeRenderer, ServiceCollectionExtensions,
        /// WorkflowEditorWindow.TemplateNodeHandler, WorkflowEditorWindow.xaml palette.
        /// Mỗi bước đều idempotent — an toàn khi gọi nhiều lần.
        /// </summary>
        public NodeGenerationResult AutoRegisterToSystem(NodeGeneratorConfig config)
        {
            var result = new NodeGenerationResult();

            if (string.IsNullOrWhiteSpace(config.NodeName))
            {
                result.Errors.Add("NodeName không được để trống.");
                return result;
            }
            if (string.IsNullOrWhiteSpace(config.ProjectRoot) || !Directory.Exists(config.ProjectRoot))
            {
                result.Errors.Add($"ProjectRoot không hợp lệ: '{config.ProjectRoot}'");
                return result;
            }

            PatchTemplateFactory(result, config);
            PatchNodeRenderer(result, config);
            PatchServiceCollection(result, config);
            PatchTemplateNodeHandler(result, config);
            PatchWorkflowEditorXaml(result, config);

            return result;
        }

        // ── Patch: TemplateFactory.cs ─────────────────────────────────────────

        private static void PatchTemplateFactory(NodeGenerationResult result, NodeGeneratorConfig config)
        {
            var path = Path.Combine(config.ProjectRoot, "Workflow", "TemplateFactory.cs");
            if (!File.Exists(path)) path = Path.Combine(config.ProjectRoot, "Services", "Workflow", "TemplateFactory.cs");
            if (!File.Exists(path)) { result.Errors.Add("Không tìm thấy TemplateFactory.cs."); return; }

            try
            {
                var content = File.ReadAllText(path);
                var nodeKey = $"\"{config.NodeClassName}\"";
                bool changed = false;

                // 1. Thêm switch arm nếu chưa có
                if (!content.Contains(nodeKey))
                {
                    // Tìm dòng fallback "_ => throw new NotSupportedException"
                    var fallback = "_ => throw new NotSupportedException";
                    var idx = content.IndexOf(fallback);
                    if (idx < 0) { result.Errors.Add("TemplateFactory.cs: Không tìm thấy fallback switch arm."); return; }

                    var arm = $"                {nodeKey} => Create{config.NodeName}Node(x, y),\r\n                ";
                    content = content.Substring(0, idx) + arm + content.Substring(idx);
                    changed = true;
                }

                // 2. Thêm CreateYourNode method nếu chưa có
                var createMethodSig = $"private WorkflowNode Create{config.NodeName}Node(";
                if (!content.Contains(createMethodSig))
                {
                    // Tìm vị trí trước "}" cuối class
                    var lastBrace = content.LastIndexOf("\n    }");
                    if (lastBrace < 0) lastBrace = content.LastIndexOf("\r\n    }");
                    if (lastBrace < 0) { result.Errors.Add("TemplateFactory.cs: Không tìm thấy cuối class."); return; }

                    var colorKey = config.ColorKey;
                    var brushResource = $"{colorKey}Brush";
                    var inputPorts = new StringBuilder();
                    foreach (var ck in config.InputPortColorKeys)
                        inputPorts.AppendLine($"            node.Ports.Add(new NodePort {{ Id = Guid.NewGuid().ToString(), IsInput = true, Position = PortPosition.Left, IsVisible = true, ColorKey = \"{ck}\" }});");
                    var outputPorts = new StringBuilder();
                    foreach (var ck in config.OutputPortColorKeys)
                        outputPorts.AppendLine($"            node.Ports.Add(new NodePort {{ Id = Guid.NewGuid().ToString(), IsInput = false, Position = PortPosition.Right, IsVisible = true, ColorKey = \"{ck}\" }});");
                    var outputKeys = new StringBuilder();
                    foreach (var k in config.OutputKeys)
                        outputKeys.AppendLine($"            node.DynamicOutputs.Add(new WorkflowDynamicDataPort {{ Key = \"{k}\", DisplayName = \"{k}\", IsMultiple = false, OutputType = WorkflowDataType.String }});");

                    var nodeTypeLine = config.AddNewNodeType
                        ? $"                Type = NodeType.{config.EffectiveNodeTypeName}"
                        : "                Type = NodeType.Generic";

                    var method = $@"
        private WorkflowNode Create{config.NodeName}Node(double x, double y)
        {{
            var node = new FlowMy.Models.Nodes.{config.NodeClassName}
            {{
                Id = $""Node_{config.NodeName}_{{Guid.NewGuid()}}"",
                Title = ""{config.Title}"",
                X = x - 30,
                Y = y - 30,
                ColorKey = ""{colorKey}"",
                NodeBrush = Application.Current.TryFindResource(""{brushResource}"") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.CornflowerBlue,
{nodeTypeLine}
            }};
{inputPorts}{outputPorts}{outputKeys}
            return node;
        }}
";
                    content = content.Substring(0, lastBrace) + method + content.Substring(lastBrace);
                    changed = true;
                }

                if (changed)
                {
                    File.WriteAllText(path, content, Encoding.UTF8);
                    result.ModifiedFiles.Add(path);
                }
                else
                {
                    result.Warnings.Add($"TemplateFactory.cs: {config.NodeClassName} đã được đăng ký — bỏ qua.");
                }
            }
            catch (Exception ex) { result.Errors.Add($"Lỗi PatchTemplateFactory: {ex.Message}"); }
        }

        // ── Patch: _NodeRenderer.cs ───────────────────────────────────────────

        private static void PatchNodeRenderer(NodeGenerationResult result, NodeGeneratorConfig config)
        {
            var path = Path.Combine(config.ProjectRoot, "Services", "Rendering", "_NodeRenderer.cs");
            if (!File.Exists(path)) { result.Errors.Add("Không tìm thấy _NodeRenderer.cs."); return; }

            try
            {
                var content = File.ReadAllText(path);
                var rendererType = config.RendererClassName;
                var fieldName = $"_{LowerFirst(rendererType)}";
                bool changed = false;

                // 1. Thêm field
                if (!content.Contains($"private readonly {rendererType}"))
                {
                    var embedField = "private readonly EmbedApplicationNodeRenderer _embedApplicationNodeRenderer;";
                    var newField = $"private readonly {rendererType} {fieldName};\r\n        {embedField}";
                    content = content.Replace(embedField, newField);
                    changed = true;
                }

                // 2. Thêm constructor param
                if (!content.Contains($"{rendererType} {LowerFirst(rendererType)}"))
                {
                    var embedParam = "EmbedApplicationNodeRenderer embedApplicationNodeRenderer";
                    var paramVar = LowerFirst(rendererType);
                    var newParam = $"{rendererType} {paramVar},\r\n            {embedParam}";
                    content = content.Replace(embedParam, newParam);
                    changed = true;
                }

                // 3. Thêm assignment trong constructor body
                if (!content.Contains($"{fieldName} = "))
                {
                    var embedAssign = "_embedApplicationNodeRenderer = embedApplicationNodeRenderer ?? throw new ArgumentNullException(nameof(embedApplicationNodeRenderer));";
                    var paramVar = LowerFirst(rendererType);
                    var newAssign = $"{fieldName} = {paramVar} ?? throw new ArgumentNullException(nameof({paramVar}));\r\n            {embedAssign}";
                    content = content.Replace(embedAssign, newAssign);
                    changed = true;
                }

                // 4. Thêm vào _rendererMap
                var nodeClass = $"typeof({config.NodeClassName})";
                if (!content.Contains(nodeClass))
                {
                    var embedMapEntry = "[typeof(EmbedApplicationNode)]  = _embedApplicationNodeRenderer,";
                    var newEntry = $"[typeof(FlowMy.Models.Nodes.{config.NodeClassName})] = {fieldName},\r\n                {embedMapEntry}";
                    content = content.Replace(embedMapEntry, newEntry);
                    changed = true;
                }

                if (changed)
                {
                    File.WriteAllText(path, content, Encoding.UTF8);
                    result.ModifiedFiles.Add(path);
                }
                else
                {
                    result.Warnings.Add($"_NodeRenderer.cs: {rendererType} đã được đăng ký — bỏ qua.");
                }
            }
            catch (Exception ex) { result.Errors.Add($"Lỗi PatchNodeRenderer: {ex.Message}"); }
        }

        // ── Patch: ServiceCollectionExtensions.cs ────────────────────────────

        private static void PatchServiceCollection(NodeGenerationResult result, NodeGeneratorConfig config)
        {
            var path = Path.Combine(config.ProjectRoot, "Services", "ServiceCollectionExtensions.cs");
            if (!File.Exists(path)) { result.Errors.Add("Không tìm thấy ServiceCollectionExtensions.cs."); return; }

            try
            {
                var content = File.ReadAllText(path);
                var rendererType = config.RendererClassName;
                var addLine = $"services.AddScoped<{rendererType}>();";

                if (content.Contains(addLine))
                {
                    result.Warnings.Add($"ServiceCollectionExtensions.cs: {rendererType} đã được đăng ký — bỏ qua.");
                    return;
                }

                // Thêm sau dòng EmbedApplicationNodeRenderer
                var anchor = "services.AddScoped<EmbedApplicationNodeRenderer>();";
                if (!content.Contains(anchor)) { result.Errors.Add("ServiceCollectionExtensions.cs: Không tìm thấy anchor EmbedApplicationNodeRenderer."); return; }

                content = content.Replace(anchor, $"{anchor}\r\n            {addLine}");
                File.WriteAllText(path, content, Encoding.UTF8);
                result.ModifiedFiles.Add(path);
            }
            catch (Exception ex) { result.Errors.Add($"Lỗi PatchServiceCollection: {ex.Message}"); }
        }

        // ── Patch: WorkflowEditorWindow.TemplateNodeHandler.cs ───────────────

        private static void PatchTemplateNodeHandler(NodeGenerationResult result, NodeGeneratorConfig config)
        {
            // Tìm file TemplateNodeHandler
            var path = Path.Combine(config.ProjectRoot, "Views", "WorkflowEditors", "WorkflowEditorWindow.TemplateNodeHandler.cs");
            if (!File.Exists(path)) path = Path.Combine(config.ProjectRoot, "Views", "WorkflowEditorWindow.TemplateNodeHandler.cs");
            if (!File.Exists(path)) { result.Errors.Add("Không tìm thấy WorkflowEditorWindow.TemplateNodeHandler.cs."); return; }

            try
            {
                var content = File.ReadAllText(path);
                var nodeKey = $"\"{config.NodeClassName}\"";

                if (content.Contains(nodeKey))
                {
                    result.Warnings.Add($"TemplateNodeHandler.cs: {config.NodeClassName} đã có icon mapping — bỏ qua.");
                    return;
                }

                // Thêm trước fallback "_  => "circle duotone""
                var fallback = "_ => \"circle duotone\"";
                if (!content.Contains(fallback)) { result.Errors.Add("TemplateNodeHandler.cs: Không tìm thấy fallback icon arm."); return; }

                var arm = $"                {nodeKey} => \"{config.IconKey}\",\r\n                {fallback}";
                content = content.Replace(fallback, arm);
                File.WriteAllText(path, content, Encoding.UTF8);
                result.ModifiedFiles.Add(path);
            }
            catch (Exception ex) { result.Errors.Add($"Lỗi PatchTemplateNodeHandler: {ex.Message}"); }
        }

        // ── Patch: WorkflowEditorWindow.xaml ─────────────────────────────────

        private static void PatchWorkflowEditorXaml(NodeGenerationResult result, NodeGeneratorConfig config)
        {
            // Tìm WorkflowEditorWindow.xaml
            var path = Path.Combine(config.ProjectRoot, "Views", "WorkflowEditorWindow.xaml");
            if (!File.Exists(path)) path = Path.Combine(config.ProjectRoot, "Views", "WorkflowEditors", "WorkflowEditorWindow.xaml");
            if (!File.Exists(path)) { result.Errors.Add("Không tìm thấy WorkflowEditorWindow.xaml."); return; }

            try
            {
                var content = File.ReadAllText(path);
                // Check xem node đã có trong palette chưa
                if (content.Contains($"Tag=\"{config.NodeClassName}\""))
                {
                    result.Warnings.Add($"WorkflowEditorWindow.xaml: {config.NodeClassName} đã có trong palette — bỏ qua.");
                    return;
                }

                // Tìm vị trí block EmbedApplicationNode (node cuối cùng trong palette trước khi thêm mới)
                // Chiến lược: tìm Tag="EmbedApplicationNode" -> đi tới </Border> kết thúc block -> chèn trước </WrapPanel>
                var embedTagMarker = "Tag=\"EmbedApplicationNode\"";
                var embedIdx = content.IndexOf(embedTagMarker, StringComparison.Ordinal);
                if (embedIdx < 0)
                {
                    result.Errors.Add("WorkflowEditorWindow.xaml: Không tìm thấy block EmbedApplicationNode trong palette. Cần thêm thủ công.");
                    return;
                }

                // Từ vị trí EmbedApplicationNode, tìm </WrapPanel> tiếp theo
                var wrapPanelClose = content.IndexOf("</WrapPanel>", embedIdx, StringComparison.Ordinal);
                if (wrapPanelClose < 0)
                {
                    result.Errors.Add("WorkflowEditorWindow.xaml: Không tìm thấy </WrapPanel> sau EmbedApplicationNode.");
                    return;
                }

                // Tìm </Border> ngay trước </WrapPanel> (border đóng của EmbedApplicationNode)
                var borderCloseBeforeWrap = content.LastIndexOf("</Border>", wrapPanelClose - 1, StringComparison.Ordinal);
                if (borderCloseBeforeWrap < 0)
                {
                    result.Errors.Add("WorkflowEditorWindow.xaml: Không tìm được vị trí chèn palette node.");
                    return;
                }

                // Chèn palette XML sau </Border> của EmbedApplicationNode
                var insertPos = borderCloseBeforeWrap + "</Border>".Length;

                var colorKey = config.ColorKey;
                var brushKey = $"{colorKey}Brush";
                var textOnBrushKey = $"TextOn{colorKey}Brush";
                var title = string.IsNullOrWhiteSpace(config.Title) ? config.NodeName : config.Title;

                var nl = "\r\n";
                var paletteXml =
                    nl + "                            <Border Style=\"{StaticResource PaletteIconNodeStyle}\"" +
                    nl + $"                                    Background=\"{{DynamicResource {brushKey}}}\"" +
                    nl + $"                                    Tag=\"{config.NodeClassName}\"" +
                    nl + "                                    MouseDown=\"NodeTemplate_MouseDown\"" +
                    nl + "                                    MouseMove=\"NodeTemplate_MouseMove\"" +
                    nl + "                                    MouseUp=\"NodeTemplate_MouseUp\"" +
                    nl + "                                    MouseEnter=\"NodeTemplate_MouseEnter\"" +
                    nl + "                                    MouseLeave=\"NodeTemplate_MouseLeave\">" +
                    nl + "                                <Border.ToolTip>" +
                    nl + "                                    <ToolTip>" +
                    nl + "                                        <StackPanel MaxWidth=\"240\">" +
                    nl + "                                            <TextBlock FontWeight=\"Bold\" FontStyle=\"Italic\">" +
                    nl + $"                                                <Run Text=\"{title}\"/>" +
                    nl + "                                            </TextBlock>" +
                    nl + "                                            <TextBlock Text=\"Node được tạo bởi Node Generator.\"" +
                    nl + "                                                       TextWrapping=\"Wrap\" Margin=\"0,4,0,0\" Opacity=\"0.9\"/>" +
                    nl + "                                        </StackPanel>" +
                    nl + "                                    </ToolTip>" +
                    nl + "                                </Border.ToolTip>" +
                    nl + "                                <Border.ContextMenu>" +
                    nl + "                                    <ContextMenu Placement=\"MousePoint\" StaysOpen=\"False\">" +
                    nl + "                                        <MenuItem IsHitTestVisible=\"False\">" +
                    nl + "                                            <MenuItem.Header>" +
                    nl + $"                                                <Border Background=\"{{DynamicResource {brushKey}}}\"" +
                    nl + "                                                        CornerRadius=\"10\" Padding=\"10\"" +
                    nl + "                                                        BorderBrush=\"{DynamicResource BorderColor}\" BorderThickness=\"1\">" +
                    nl + "                                                    <StackPanel>" +
                    nl + $"                                                        <TextBlock Text=\"{title}\"" +
                    nl + $"                                                                   Foreground=\"{{DynamicResource {textOnBrushKey}}}\"" +
                    nl + "                                                                   FontWeight=\"Bold\" FontSize=\"13\"/>" +
                    nl + "                                                        <TextBlock Text=\"Node tự động sinh bởi Node Generator.\"" +
                    nl + $"                                                                   Foreground=\"{{DynamicResource {textOnBrushKey}}}\"" +
                    nl + "                                                                   Opacity=\"0.9\" TextWrapping=\"Wrap\" Margin=\"0,4,0,0\"/>" +
                    nl + "                                                    </StackPanel>" +
                    nl + "                                                </Border>" +
                    nl + "                                            </MenuItem.Header>" +
                    nl + "                                        </MenuItem>" +
                    nl + "                                    </ContextMenu>" +
                    nl + "                                </Border.ContextMenu>" +
                    nl + "                                <Grid>" +
                    nl + "                                    <TextBlock Text=\"◆\" FontSize=\"24\"" +
                    nl + "                                               HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\"" +
                    nl + $"                                               Foreground=\"{{DynamicResource {textOnBrushKey}}}\"/>" +
                    nl + "                                </Grid>" +
                    nl + "                            </Border>";

                content = content.Substring(0, insertPos) + paletteXml + content.Substring(insertPos);
                File.WriteAllText(path, content, Encoding.UTF8);
                result.ModifiedFiles.Add(path);
            }
            catch (Exception ex) { result.Errors.Add($"Lỗi PatchWorkflowEditorXaml: {ex.Message}"); }
        }

        /// <summary>
        /// Sinh cấu hình từ JSON string (dùng cho CLI).
        /// Format: { "NodeName": "HelloWorld", "Title": "Hello World", ... }
        /// </summary>
        public static NodeGeneratorConfig? ParseFromJson(string json)
        {
            try
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<NodeGeneratorConfig>(json, opts);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sinh cấu hình từ command-line args kiểu: --name HelloWorld --title "Hello World" ...
        /// </summary>
        public static NodeGeneratorConfig ParseFromArgs(string[] args)
        {
            var config = new NodeGeneratorConfig();
            for (int i = 0; i < args.Length - 1; i++)
            {
                var key = args[i].TrimStart('-').ToLower();
                var val = args[i + 1];
                switch (key)
                {
                    case "name": config.NodeName = val; i++; break;
                    case "title": config.Title = val; i++; break;
                    case "iconkey": config.IconKey = val; i++; break;
                    case "colorkey": config.ColorKey = val; i++; break;
                    case "projectroot": config.ProjectRoot = val; i++; break;
                    case "addnodetype": config.AddNewNodeType = val.ToLower() is "true" or "1" or "yes"; i++; break;
                    case "nodetypename": config.NodeTypeName = val; i++; break;
                    case "outputkeys":
                        config.OutputKeys = val.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                        i++;
                        break;
                    case "inputcount":
                        if (int.TryParse(val, out var ic))
                            config.InputPortColorKeys = Enumerable.Range(0, ic).Select(_ => "Info").ToList();
                        i++;
                        break;
                    case "outputcount":
                        if (int.TryParse(val, out var oc))
                            config.OutputPortColorKeys = Enumerable.Range(0, oc).Select(_ => "SunsetOrange").ToList();
                        i++;
                        break;
                }
            }
            return config;
        }
    }

    public sealed class NodeGenerationResult
    {
        public List<string> CreatedFiles { get; } = new();
        public List<string> ModifiedFiles { get; } = new();
        public List<string> Skipped { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool IsSuccess => Errors.Count == 0;

        public string ToSummary()
        {
            var sb = new StringBuilder();
            if (CreatedFiles.Count > 0)
            {
                sb.AppendLine($"✅ Đã tạo {CreatedFiles.Count} file:");
                foreach (var f in CreatedFiles)
                    sb.AppendLine($"   + {Path.GetFileName(f)}");
            }
            if (ModifiedFiles.Count > 0)
            {
                sb.AppendLine($"📝 Đã sửa {ModifiedFiles.Count} file:");
                foreach (var f in ModifiedFiles)
                    sb.AppendLine($"   ~ {Path.GetFileName(f)}");
            }
            if (Skipped.Count > 0)
            {
                sb.AppendLine($"⏭️ Bỏ qua {Skipped.Count} file (đã tồn tại):");
                foreach (var f in Skipped)
                    sb.AppendLine($"   = {Path.GetFileName(f)}");
            }
            if (Warnings.Count > 0)
            {
                sb.AppendLine($"⚠️ Cảnh báo:");
                foreach (var w in Warnings)
                    sb.AppendLine($"   ⚠ {w}");
            }
            if (Errors.Count > 0)
            {
                sb.AppendLine($"❌ Lỗi:");
                foreach (var e in Errors)
                    sb.AppendLine($"   ✗ {e}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
