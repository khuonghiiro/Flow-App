# AI_NODE_FLOW_GUIDE — Hướng dẫn toàn diện tạo Node cho AI Agent

> **Mục đích**: Tài liệu tổng hợp đầy đủ nhất để AI agent hiểu đúng kiến trúc, luồng xử lý, và cách implement một node hoàn chỉnh từ A→Z trong hệ thống workflow của Auto_Click_V2 (WPF/MVVM + C#).
> Đọc tài liệu này **trước tiên** trước khi đọc Node_Dialog_V2.md hoặc NODE_DIALOG_GUIDE.md.

---

## 📋 MỤC LỤC

1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Luồng tạo Node từ A→Z (Checklist)](#2-luồng-tạo-node-từ-az-checklist)
3. [Chi tiết từng thành phần](#3-chi-tiết-từng-thành-phần)
   - 3.1 [Node Model](#31-node-model)
   - 3.2 [TemplateFactory — Palette → Model](#32-templatefactory--palette--model)
   - 3.3 [NodeControl — Render trên Canvas](#33-nodecontrol--render-trên-canvas)
   - 3.4 [Dialog XAML + Code-behind](#34-dialog-xaml--code-behind)
   - 3.5 [BaseNodeDialog — Lifecycle & Input/Output UI](#35-basenodedialog--lifecycle--inputoutput-ui)
   - 3.6 [ViewModel](#36-viewmodel)
   - 3.7 [Persistence (Save/Load)](#37-persistence-saveload)
   - 3.8 [Executor — Chạy logic](#38-executor--chạy-logic)
4. [Luồng chạy Workflow (Runtime Flow)](#4-luồng-chạy-workflow-runtime-flow)
   - 4.1 [ExecutionId & scoped outputs (chạy song song)](#41-executionid--scoped-outputs-chạy-song-song)
5. [Patterns nâng cao](#5-patterns-nâng-cao)
   - 5.1 [TitleDisplayMode](#51-titledisplaymode)
   - 5.2 [TitleColorMode](#52-titlecolormode)
   - 5.3 [ReuseRoutes (Tái sử dụng flow)](#53-reuseroutes-tái-sử-dụng-flow)
   - 5.4 [PortPosition — Đổi vị trí cổng](#54-portposition--đổi-vị-trí-cổng)
   - 5.5 [Dynamic Input/Output](#55-dynamic-inputoutput)
6. [Input/Output — Cơ chế truyền dữ liệu giữa các node](#6-inputoutput--cơ-chế-truyền-dữ-liệu-giữa-các-node)
7. [Các lỗi thường gặp & cách tránh](#7-các-lỗi-thường-gặp--cách-tránh)

---

## 1. Kiến trúc tổng quan

```
┌──────────────────────────────────────────────────────────────────────┐
│                       WORKFLOW EDITOR                                 │
│                                                                       │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │  WorkflowEditorWindow (IWorkflowEditorHost)                  │    │
│  │  ┌─────────────┐  ┌──────────────┐  ┌────────────────────┐  │    │
│  │  │  Palette    │  │  Canvas      │  │  ViewModel         │  │    │
│  │  │ (XAML Tag)  │  │ (NodeBorder) │  │  (MVVM)            │  │    │
│  │  └──────┬──────┘  └──────┬───────┘  └────────────────────┘  │    │
│  └─────────┼────────────────┼─────────────────────────────────--┘    │
│            │                │                                         │
│     drag-drop           right-click                                  │
│            │                │                                         │
│            ▼                ▼                                         │
│  ┌─────────────────┐  ┌────────────────────────────────────────┐     │
│  │ TemplateFactory │  │ NodeControl.OpenNodeDialog()            │     │
│  │ .Create(tag)    │  │   → NodeDialogManager.OpenDialog()      │     │
│  └────────┬────────┘  └──────────────┬─────────────────────────┘     │
│           │                          │                                │
│           ▼                          ▼                                │
│  ┌─────────────────┐        ┌────────────────────┐                   │
│  │   YourNode      │        │  YourNodeDialog     │                   │
│  │  (Model/Data)   │◄───────│  (XAML + cs)        │                   │
│  └────────┬────────┘        │  + ViewModel        │                   │
│           │                 └────────────────────┘                   │
│           │ Save/Load                                                 │
│           ▼                                                           │
│  ┌─────────────────────────────┐  ┌──────────────────────────────┐   │
│  │ FileWorkflowPersistenceService│  │ WorkflowExecutionService      │  │
│  │ (JSON serialize/deserialize) │  │ → NodeExecutors (logic)       │  │
│  └─────────────────────────────┘  └──────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────┘
```

### Các file cần tạo cho mỗi node mới

| File | Vị trí | Mô tả |
|------|---------|-------|
| `YourNode.cs` | `Models/Nodes/` | Model chứa dữ liệu node |
| `YourNodeDialog.xaml` | `Views/Overlays/` | Giao diện dialog |
| `YourNodeDialog.xaml.cs` | `Views/Overlays/` | Code-behind dialog |
| `YourNodeDialogViewModel.cs` | `ViewModels/` | ViewModel theo MVVM |
| `YourNodeControl.cs` | `Views/NodeControls/` | Render node trên canvas |
| `YourNodeRenderer.cs` | `Services/Rendering/` | Quản lý vòng đời render |
| `YourNodeExecutor.cs` | `Services/Workflow/NodeExecutors/` | Logic thực thi (nếu cần) |
| `FileWorkflowPersistenceService.cs` | `Services/Workflow/` | Thêm serialize/deserialize |
| `WorkflowEditorWindow.xaml` | `Views/WorkflowEditors/` | Thêm node vào palette |
| `TemplateFactory.cs` | `Services/Workflow/` | Map Tag → Model |

---

## 2. Luồng tạo Node từ A→Z (Checklist)

```
[PHASE 0] Xác nhận với user:
  □ ColorKey (màu node: ForestPine, SkyAzure, AmberWarm...)
  □ Icon (timer regular, globe-pointer, folder-open...)
  □ Tên node, mô tả chức năng
  □ Danh sách Inputs/Outputs
  □ Có cần Executor không?
  □ Có cần TitleDisplayMode / TitleColorMode không?
  □ Có cần Dynamic Input/Output không?

[PHASE 1] Core — Bắt buộc:
  □ 1. Tạo Model: Models/Nodes/YourNode.cs
  □ 2. Thêm tag + palette vào WorkflowEditorWindow.xaml
  □ 3. Map tag → model trong TemplateFactory.cs
  □ 4. Tạo Dialog XAML: Views/Overlays/YourNodeDialog.xaml
  □ 5. Tạo Dialog Code-behind: YourNodeDialog.xaml.cs
  □ 6. Tạo ViewModel: ViewModels/YourNodeDialogViewModel.cs
  □ 7. Tạo NodeControl: Views/NodeControls/YourNodeControl.cs
  □ 8. Tạo Renderer: Services/Rendering/YourNodeRenderer.cs
  □ 9. Thêm Persistence: FileWorkflowPersistenceService.cs
  □ 10. Thêm Copy/Paste: WorkflowEditorEventService.cs

[PHASE 2] TitleDisplayMode (nếu cần):
  □ Property TitleDisplayMode + TitleTextBlockUI trên Model
  □ ViewModel ObservableProperty + TitleDisplayModeOptions
  □ NodeControl: titleTextBlock floating trên Canvas
  □ XAML: ComboBox TitleDisplayMode

[PHASE 3] TitleColorMode (nếu cần):
  □ Property TitleColorMode + TitleColorKey trên Model
  □ NodeControl: GetTitleBrush(node) thay vì node.NodeBrush
  □ XAML: TitleColorComboBox + color preview border

[PHASE 4] ReuseRoutes + PortPosition (nếu cần):
  □ Tab "Tái sử dụng flow" trong dialog
  □ ItemsControl bind ReuseRoutes
  □ ComboBox chọn Port IN/OUT position

[PHASE 5] Executor (nếu node thực thi logic):
  □ Tạo YourNodeExecutor.cs implement INodeExecutor
  □ Đăng ký trong WorkflowExecutionService
  □ ⚠️ Đọc [§4.1 ExecutionId & scoped outputs](#41-executionid--scoped-outputs-chạy-song-song):
        - Đọc output node khác = ResolveDynamicValueForExecution / ResolveValueByNodeIdAndKeyForExecution (+ env), KHÔNG NodeDataPanelService trong luồng run
        - Node có output đặc biệt: mirror trong MirrorRuntimeOutputsToScopedStore hoặc IWorkflowScopedOutputContributor
        - Storage: PublishStorageOutputsToScoped trước Traverse nếu ghi StoredOutputs trong executor
```

---

## 3. Chi tiết từng thành phần

### 3.1 Node Model

**File**: `Models/Nodes/YourNode.cs`

```csharp
// ✅ Sealed class, kế thừa WorkflowNode
// ✅ Implement INotifyPropertyChanged nếu property cần reactive
// ✅ Có NotifyTitleChanged() nếu INotifyPropertyChanged
// ✅ Có TitleDisplayMode, TitleColorMode, TitleColorKey nếu Phase 2/3

public sealed class YourNode : WorkflowNode, INotifyPropertyChanged
{
    // --- Properties ---
    private string _someProperty = string.Empty;
    public string SomeProperty
    {
        get => _someProperty;
        set { if (_someProperty != value) { _someProperty = value; OnPropertyChanged(); } }
    }

    // Phase 2:
    public TitleDisplayMode TitleDisplayMode { get; set; } = TitleDisplayMode.Hidden;
    public TextBlock? TitleTextBlockUI { get; set; } // UI reference (set bởi NodeControl)

    // Phase 3:
    public TitleColorMode TitleColorMode { get; set; } = TitleColorMode.NodeColor;
    public string? TitleColorKey { get; set; }

    // --- Constructor ---
    public YourNode()
    {
        Type = NodeType.YourType; // enum NodeType
        Title = "Your Node";
    }

    // --- INotifyPropertyChanged ---
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));
}
```

**⚠️ Quy tắc**:
- `public sealed class` — KHÔNG dùng class thường
- KHÔNG `public new string Title { get; set; }` — dùng `NotifyTitleChanged()` thay thế
- Properties dùng PascalCase, khởi tạo giá trị mặc định rõ ràng

---

### 3.2 TemplateFactory — Palette → Model

**File**: `Services/Workflow/TemplateFactory.cs`

1. **Palette** (WorkflowEditorWindow.xaml): `<Border Tag="YourNodeTypeName" Background="{DynamicResource ForestPineBrush}">`
2. **TemplateFactory**: Map tag → tạo model

```csharp
public WorkflowNode Create(string nodeType, double x, double y)
{
    return nodeType switch
    {
        "YourNode" => CreateYourNode(x, y),
        // ... các node khác
        _ => throw new NotSupportedException($"Unknown node type '{nodeType}'.")
    };
}

private static WorkflowNode CreateYourNode(double x, double y)
{
    var node = new YourNode
    {
        Id = Guid.NewGuid().ToString(),
        X = x, Y = y,
        ColorKey = "ForestPine",  // ← khớp với palette
        NodeBrush = Application.Current.TryFindResource("ForestPineBrush") as Brush ?? Brushes.Green
    };

    // Port IN (trái)
    node.Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = true,
        Position = PortPosition.Left, IsVisible = true, ColorKey = "Info" });

    // Port OUT (phải)
    node.Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = false,
        Position = PortPosition.Right, IsVisible = true, ColorKey = "SunsetOrange" });

    return node;
}
```

**⚠️ Port màu mặc định**:
- Port IN: `ColorKey = "Info"` (màu xanh dương)
- Port OUT: `ColorKey = "SunsetOrange"` (màu cam)

---

### 3.3 NodeControl — Render trên Canvas

**File**: `Views/NodeControls/YourNodeControl.cs`

NodeControl là **static class** với method `CreateBorder(node, ownerWindow, host)` trả về `Border`.

```csharp
public static class YourNodeControl
{
    // Throttling dictionaries cho TitleDisplayMode
    private static readonly Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
    private static readonly Dictionary<Border, bool> _titleUpdatedAfterZoom = new();
    private const int TitleUpdateThrottleMs = 50;

    public static Border CreateBorder(YourNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));

        // 1. Tạo Grid + nội dung hiển thị (icon, text...)
        var grid = new Grid { MinWidth = 80, MinHeight = 80 };
        // ... thêm icon, label ...

        // 2. Tạo Border (ngoài cùng)
        var border = new Border
        {
            Child = grid,
            Background = node.NodeBrush,
            BorderBrush = new SolidColorBrush(Colors.White),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Effect = new DropShadowEffect { Color = Colors.Black, ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5 }
        };

        // 3. Phase 2: Title TextBlock (floating trên Canvas)
        var titleTextBlock = new TextBlock
        {
            Text = node.Title ?? "Your Node",
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = GetTitleBrush(node),           // ← Phase 3: dùng GetTitleBrush
            Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
            IsHitTestVisible = false
        };
        node.TitleTextBlockUI = titleTextBlock;         // ← gán reference vào model

        // 4. Hover cho TitleDisplayMode
        bool isHovering = false;
        border.MouseEnter += (s, e) => { isHovering = true; UpdateTitleVisibility(...); };
        border.MouseLeave += (s, e) => { isHovering = false; UpdateTitleVisibility(...); };

        // 5. Subscribe PropertyChanged
        if (node is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WorkflowNode.Title))
                    titleTextBlock.Text = node.Title ?? "Your Node";
                else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                {
                    border.Background = node.NodeBrush;
                    titleTextBlock.Foreground = GetTitleBrush(node);
                }
                else if (e.PropertyName == nameof(YourNode.TitleColorMode) ||
                         e.PropertyName == nameof(YourNode.TitleColorKey))
                    titleTextBlock.Foreground = GetTitleBrush(node);
                else if (e.PropertyName == nameof(YourNode.TitleDisplayMode))
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            };
        }

        // 6. Loaded: thêm titleTextBlock vào Canvas
        border.Loaded += (s, e) =>
        {
            if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
            {
                host.WorkflowCanvas.Children.Add(titleTextBlock);
                Panel.SetZIndex(titleTextBlock, 20000);
                UpdateTitlePosition(titleTextBlock, border, host);
            }
        };

        // 7. Unloaded: cleanup
        border.Unloaded += (s, e) =>
        {
            if (_titleUpdateTimers.TryGetValue(border, out var t)) { t.Stop(); _titleUpdateTimers.Remove(border); }
            _titleUpdatedAfterZoom.Remove(border);
            if (host.WorkflowCanvas?.Children.Contains(titleTextBlock) == true)
                host.WorkflowCanvas.Children.Remove(titleTextBlock);
            if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                node.TitleTextBlockUI = null;
        };

        // 8. LayoutUpdated: sync position khi zoom/pan
        border.LayoutUpdated += (s, e) => { /* throttle + UpdateTitlePosition */ };

        // 9. Right-click: mở dialog
        border.MouseRightButtonUp += (s, e) =>
        {
            e.Handled = true;
            OpenNodeDialog(node, host, ownerWindow);
        };

        return border;
    }

    private static void OpenNodeDialog(YourNode node, IWorkflowEditorHost host, Window? ownerWindow)
    {
        // ⚠️ CRITICAL: release mouse capture, clear DraggedNode
        if (node.Border?.IsMouseCaptured == true) node.Border.ReleaseMouseCapture();
        host.DraggedNode = null;
        if (host.ViewModel != null) host.ViewModel.SelectedNode = null;

        var dialogManager = GetOrCreateDialogManager(host);
        if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
        if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
            dialogManager.CloseCurrentDialog();

        var dialog = new YourNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
        dialogManager.OpenDialog(node, dialog, host);
    }

    // Phase 3: lấy màu title đúng
    private static Brush GetTitleBrush(YourNode node)
    {
        if (node.TitleColorMode != TitleColorMode.CustomColor ||
            string.IsNullOrEmpty(node.TitleColorKey) || node.TitleColorKey == "NodeColor")
            return node.NodeBrush;
        if (node.TitleColorKey == "LimeGreen") return new SolidColorBrush(Colors.LimeGreen);
        return Application.Current.TryFindResource(node.TitleColorKey) as Brush ?? node.NodeBrush;
    }

    // Icon text màu (dùng TextOn{ColorKey}Brush)
    private static Brush GetTextBrush(string? colorKey)
    {
        if (string.IsNullOrWhiteSpace(colorKey)) return Brushes.White;
        return Application.Current.Resources.Contains($"TextOn{colorKey}Brush")
            ? (Brush)Application.Current.Resources[$"TextOn{colorKey}Brush"]
            : Brushes.White;
    }
}
```

---

### 3.4 Dialog XAML + Code-behind

**File**: `Views/Overlays/YourNodeDialog.xaml`

Cấu trúc chuẩn (PHẢI kế thừa `BaseNodeDialog`):

```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
        xmlns:local="clr-namespace:FlowMy.Views.Overlays"
        WindowStyle="None" ResizeMode="CanResize"
        AllowsTransparency="True" Background="Transparent"
        ShowInTaskbar="False" Topmost="True"
        Width="400" Height="600" MinWidth="350" MinHeight="400">

    <Border CornerRadius="12" Background="#FF1E293B" BorderBrush="#33FFFFFF" BorderThickness="1">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>  <!-- Header -->
                <RowDefinition Height="*"/>     <!-- TabControl -->
            </Grid.RowDefinitions>

            <!-- HEADER: Title + Play + Close -->
            <Border Grid.Row="0" Background="#FF0F172A" CornerRadius="12,12,0,0" Padding="16,12,12,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <TextBox x:Name="TitleTextBox" Grid.Column="0"
                             Text="{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}"
                             Style="{DynamicResource BaseTextBoxV2}"
                             FontSize="16" Padding="0,4,0,4" VerticalContentAlignment="Center"/>

                    <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                        <!-- Play: chạy logic riêng node -->
                        <Button Width="24" Height="24" Content="▶" FontSize="12"
                                Style="{DynamicResource PrimaryButton}" Margin="8,0,0,0"
                                ToolTip="Chạy logic node này"
                                Command="{Binding RunSingleNodeCommand}"/>
                        <!-- Close -->
                        <Button x:Name="CloseButton" Padding="0,0,0,0"  Width="24" Height="24"
                                Style="{DynamicResource DangerButton}" Content="×" FontSize="12"
                                FontWeight="Bold" Margin="8,0,0,0" Click="CloseButton_Click"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- TAB CONTROL: Logic + Cấu hình -->
            <TabControl Grid.Row="1" Background="Transparent" BorderThickness="0" Margin="0,8,0,0">

                <!-- TAB 1: LOGIC -->
                <TabItem Header="Logic" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>
                            <!-- TitleDisplayMode (Phase 2) -->
                            <!-- TitleColorMode (Phase 3) -->
                            <!-- Custom properties của node -->

                            <!-- Inputs Panel -->
                            <TextBlock Text="Inputs:" Foreground="White" FontSize="14"
                                       FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border Background="#FF1E293B" BorderBrush="#33FFFFFF"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <StackPanel x:Name="InputsPanel"/>
                            </Border>

                            <!-- Outputs Panel -->
                            <TextBlock x:Name="TextBlockOutputPanel" Text="Outputs:"
                                       Foreground="White" FontSize="14"
                                       FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border x:Name="BorderOutputPanel" Background="#FF1E293B"
                                    BorderBrush="#33FFFFFF" BorderThickness="1"
                                    CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <StackPanel x:Name="OutputsPanel"/>
                            </Border>
                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

                <!-- TAB 2: CẤU HÌNH / TÁI SỬ DỤNG FLOW -->
                <TabItem Header="Cấu hình" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>
                            <!-- Phase 4b: Port Position -->
                            <!-- Phase 4: ReuseRoutes -->
                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

            </TabControl>
        </Grid>
    </Border>
</local:BaseNodeDialog>
```

**File**: `Views/Overlays/YourNodeDialog.xaml.cs`

```csharp
public partial class YourNodeDialog : BaseNodeDialog
{
    private readonly YourNodeDialogViewModel _viewModel;

    public YourNodeDialog(YourNode node, IWorkflowEditorHost host, Window? owner)
    {
        InitializeComponent();
        _viewModel = new YourNodeDialogViewModel(node, host);
        InitializeBase(_viewModel, owner);
        // Phase 3: UpdateTitleColorPreview() nếu có
    }

    // ⚠️ REQUIRED: trả về panel chứa Inputs
    protected override Panel? GetInputsPanel() => InputsPanel;

    // ⚠️ REQUIRED: trả về panel chứa Outputs
    protected override Panel? GetOutputsPanel() => OutputsPanel;

    // ⚠️ REQUIRED: save title trước khi đóng
    protected override void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveTitleCommand.Execute(null);
        Close();
    }
}
```

---

### 3.5 BaseNodeDialog — Lifecycle & Input/Output UI

**File**: `Views/Overlays/BaseNodeDialog.xaml.cs`

Đây là lớp cha của tất cả dialog. Hiểu lifecycle:

```
Constructor (derived)
  │→ InitializeComponent()       ← XAML khởi tạo
  │→ InitializeBase(vm, owner)   ← set DataContext, subscribe events
  │   ├── Owner = owner
  │   ├── DataContext = ViewModel
  │   ├── Loaded += BaseNodeDialog_Loaded
  │   └── Closing += SaveTitleCommand.Execute   ← ⚠️ Auto-save khi đóng
  │
  └── [Window = Loaded event]
      └── BaseNodeDialog_Loaded()
          └── SetupInputsOutputs()
              ├── GetInputsPanel() + LoadInputs()    ← render Input slots
              └── GetOutputsPanel() + LoadOutputs()  ← render Output slots
              └── OnLoaded()                         ← hook cho derived class
```

**LoadInputs()** — mỗi input tạo UI:
- Label "Key: ..."
- ComboBox chọn Source Node (AvailableSources)
- ComboBox chọn Output Key (ẩn nếu chỉ 1 key)
- TextBlock hiển thị giá trị hiện tại

**LoadOutputs()** — mỗi output tạo UI:
- Label "Key: ..."
- TextBlock hiển thị giá trị hiện tại

Để **tùy chỉnh UI của Input/Output**, override `CreateInputItemUI(inputVm)` hoặc `CreateOutputItemUI(outputVm)` trong dialog con.

---

### 3.6 ViewModel

**File**: `ViewModels/YourNodeDialogViewModel.cs`

```csharp
public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly YourNode _node;

    // Properties của node — dùng [ObservableProperty]
    [ObservableProperty] private string _yourProperty = string.Empty;

    public YourNodeDialogViewModel(YourNode node, IWorkflowEditorHost host)
        : base(node, host)
    {
        _node = node;

        // Load init values từ node vào VM
        _yourProperty = node.SomeProperty;

        // Phase 2: TitleDisplayMode
        // _titleDisplayMode = node.TitleDisplayMode;

        // Load inputs/outputs
        LoadInputs();
        LoadOutputs();
    }

    // ⚠️ REQUIRED: Load danh sách inputs vào ViewModel.Inputs
    protected override void LoadInputs()
    {
        Inputs.Clear();
        foreach (var input in _node.InputMappings) // hoặc fixed inputs
        {
            var vm = new InputItemViewModel(input.Key, _host);
            vm.SelectedSourceNodeId = input.SourceNodeId;
            vm.SelectedSourceOutputKey = input.SourceOutputKey;
            Inputs.Add(vm);
        }
        RefreshAvailableSourcesForInputs();
    }

    // ⚠️ REQUIRED: Load danh sách outputs vào ViewModel.Outputs
    protected override void LoadOutputs()
    {
        Outputs.Clear();
        foreach (var output in _node.Outputs)
        {
            Outputs.Add(new OutputItemViewModel(output.Key, output.Value));
        }
    }

    // ⚠️ REQUIRED: Lưu dữ liệu về node khi Save
    protected override void OnSaveTitle()
    {
        _node.SomeProperty = YourProperty;

        // Lưu InputMappings từ ViewModel.Inputs về node
        _node.InputMappings.Clear();
        foreach (var inputVm in Inputs)
        {
            _node.InputMappings.Add(new InputMapping
            {
                Key = inputVm.Key,
                SourceNodeId = inputVm.SelectedSourceNodeId,
                SourceOutputKey = inputVm.SelectedSourceOutputKey
            });
        }
    }
}
```

**BaseNodeDialogViewModel** đã có sẵn:
- `NodeTitle` (ObservableProperty, bind với TitleTextBox)
- `SaveTitleCommand` (gọi `OnSaveTitle()` + sync title về node)
- `RunSingleNodeCommand` (chạy logic node đơn lẻ)
- `Inputs` / `Outputs` (ObservableCollection)
- `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey` (Phase 2/3)
- `ReuseRoutes`, `ConnectionLineStyleOptions` (Phase 4)
- `PortPositionOptions`, `InputPortPosition`, `OutputPortPosition` (Phase 4b)

---

### 3.7 Persistence (Save/Load)

**File**: `Services/Workflow/FileWorkflowPersistenceService.cs`

#### Serialize (GetNodeProperties)

```csharp
// Thêm trong GetNodeProperties(WorkflowNode node):
if (node is YourNode yn)
{
    dict["SomeProperty"] = yn.SomeProperty;
    dict["TitleDisplayMode"] = yn.TitleDisplayMode.ToString();
    dict["TitleColorMode"]   = yn.TitleColorMode.ToString();
    dict["TitleColorKey"]    = yn.TitleColorKey ?? "";

    // Serialize list: dùng JsonSerializer
    dict["InputMappings"] = JsonSerializer.Serialize(yn.InputMappings);

    // Serialize ReuseRoutes (Phase 4)
    if (yn.ReuseRoutes?.Count > 0)
        dict["ReuseRoutes"] = JsonSerializer.Serialize(yn.ReuseRoutes);
}
```

#### Deserialize (RestoreNodeProperties)

```csharp
// Thêm trong RestoreNodeProperties(WorkflowNode node, Dictionary props):
if (node is YourNode yn)
{
    if (props.TryGetValue("SomeProperty", out var sp)) yn.SomeProperty = sp?.ToString() ?? "";

    if (props.TryGetValue("TitleDisplayMode", out var tdm) &&
        Enum.TryParse<TitleDisplayMode>(tdm?.ToString(), out var tdmVal))
        yn.TitleDisplayMode = tdmVal;

    if (props.TryGetValue("TitleColorMode", out var tcm) &&
        Enum.TryParse<TitleColorMode>(tcm?.ToString(), out var tcmVal))
        yn.TitleColorMode = tcmVal;

    if (props.TryGetValue("TitleColorKey", out var tck))
        yn.TitleColorKey = tck?.ToString();

    // Deserialize list:
    if (props.TryGetValue("InputMappings", out var im) && im is string imStr)
        yn.InputMappings = JsonSerializer.Deserialize<List<InputMapping>>(imStr) ?? new();

    // Deserialize ReuseRoutes (Phase 4)
    if (props.TryGetValue("ReuseRoutes", out var rr))
    {
        // Xử lý cả string và JsonElement (xem NODE_DIALOG_GUIDE.md Phase 4 Step 6)
        // ...
    }
}
```

> **⚠️ PortPosition**: lưu tự động qua `PortDto.Position`. Khi restore **phải** gán lại `targetPort.Position = (PortPosition)portDto.Position`.

---

### 3.8 Executor — Chạy logic

**File**: `Services/Workflow/NodeExecutors/YourNodeExecutor.cs`

```csharp
public sealed class YourNodeExecutor : INodeExecutor
{
    // Cho biết executor này xử lý loại node nào
    public bool CanExecute(WorkflowNode node) => node is YourNode;

    public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
    {
        var yn = (YourNode)node;
        env.CancellationToken.ThrowIfCancellationRequested();

        // 1. Đọc giá trị từ node nguồn (⚠️ luôn qua scoped khi có env — xem §4.1)
        //    Ví dụ: tìm sourceNode theo Id rồi:
        //    var inputValue = env.Service.ResolveDynamicValueForExecution(sourceNode, "outputKey", env);

        // 2. Thực hiện logic nghiệp vụ
        var result = await DoSomeLogicAsync(inputValue, env.CancellationToken);

        // 3. Ghi output
        yn.Outputs["outputKeyName"] = result;

        // 4. Chuyển sang node tiếp theo (LUÔN gọi TraverseOutputsAsync)
        await env.TraverseOutputsAsync(node);
    }
}
```

**Đăng ký**: thêm `new YourNodeExecutor()` vào danh sách executors trong `WorkflowExecutionService`.

---

## 4. Luồng chạy Workflow (Runtime Flow)

```
User bấm "Bắt đầu"
  │
  ▼
WorkflowExecutionService.StartAsync()
  │ Tìm tất cả StartNode
  │ Với mỗi StartNode:
  ▼
ExecuteNodeAsync(startNode, connections, token, ...)
  │
  ├─ Tìm INodeExecutor.CanExecute(node) == true
  ├─ OnNodeStarted?.Invoke(node)        ← bắt đầu visual (glow/highlight)
  ├─ executor.ExecuteAsync(node, env)   ← chạy logic
  └─ OnNodeCompleted?.Invoke(node, duration) ← kết thúc visual
       │
       └─ [Bên trong executor]
            env.TraverseOutputsAsync(node)
              │ Tìm output port
              │ Lấy connections từ output port
              │ Kiểm tra ReuseRoutes (filter connection nếu có)
              │ ⚠️ StorageNode luôn chạy trước
              │ Với mỗi connection:
              └─ env.ExecuteNextAsync(toNode, connection)
                   └─ Service.ExecuteNodeAsync(toNode, ...) [đệ quy]
```

**NodeExecutionEnvironment** chứa:
- `Service` — WorkflowExecutionService (helpers, resolve scoped, traverse...)
- `Connections` — toàn bộ connections trong workflow
- `CancellationToken` — để cancel
- `ExecutionId` — id của **lần chạy** (mỗi lần Start một id); snapshot output chuỗi theo id này
- `FlowScopeId` / `BranchId` / `ParentFlowScopeId` — ngữ cảnh nhánh flow (async/subflow)
- `ExecutionPath` — danh sách node IDs đã chạy (detect infinite loop, max 100 nodes)
- `IncomingConnection` — connection dẫn vào node hiện tại
- `ReachableToEnd` — set các node có thể đến End
- `RefreshOnly` — khi true: Play single node / logic-only → **không** dùng snapshot scoped như run đầy đủ (đọc trực tiếp qua data panel)

### 4.1 ExecutionId & scoped outputs (chạy song song)

**Vì sao cần:** Cùng một graph WPF, nhiều workflow có thể chạy **đồng thời** (hoặc lần sau đọc trước khi node kia ghi xong). Nhiều node lưu output trên **instance model dùng chung** (ví dụ `CodeNode.ResolvedOutputs`, `StorageNode.StoredOutputs`). Không có lớp cách ly theo lần chạy → downstream có thể đọc nhầm bản ghi của run khác.

**Cơ chế (tóm tắt kỹ thuật):**

| Thành phần | Vai trò |
|------------|---------|
| `ExecutionId` (string) | Mỗi lần chạy workflow có một id; truyền xuyên suốt `ExecuteNodeAsync` / `NodeExecutionEnvironment`. |
| `_scopedStringOutputsByRun` | `ConcurrentDictionary`: ExecutionId → NodeId → key → giá trị chuỗi (snapshot). |
| `SetScopedNodeStringOutput` / `TryGetScopedNodeStringOutput` | Ghi/đọc snapshot (internal). |
| `MirrorRuntimeOutputsToScopedStore(node, executionId)` | Gọi trong **finally** sau `executor.ExecuteAsync` trong `WorkflowExecutionService` — copy output runtime của node vào snapshot (switch theo loại node + `IWorkflowScopedOutputContributor`). |
| `ClearScopedOutputsForRun(executionId)` | Host/ViewModel gọi khi run kết thúc (`finally`) để giảm RAM; đồng thời gỡ id khỏi registry LRU. |
| LRU | Giới hạn số run giữ snapshot (hằng `MaxScopedRunsRetained`); run cũ bị evict nếu quên Clear. |

**API resolve — bắt buộc trong Executor khi đọc output node khác trong luồng thực thi:**

- `env.Service.ResolveDynamicValueForExecution(sourceNode, key, env)`
- `env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, nodeId, key, env)`
- Điều kiện If: `ResolveConditionFromUpstreamForExecution(conditionalNode, key, connections, env)`

**Không** dùng `NodeDataPanelService.ResolveDynamicValueByKey` trong executor cho dữ liệu phụ thuộc run (trừ code chỉ phục vụ UI preview / Timer không có `env` — và phải ghi chú rõ).

**Khi chỉ có `executionId` (không có full `env`):**

- `WorkflowExecutionService.ResolveDynamicValueForRun(node, key, executionId)` — ưu tiên scoped, fallback data panel. Dùng trong `MirrorOutputsToStorageNodes` và các helper service nội bộ tương tự.

**StorageNode:**

- Sau khi cập nhật `StoredOutputs` (executor, AssignData, Loop assignment, mirror passive), cần **`PublishStorageOutputsToScoped(storage, executionId)`** để downstream trong **cùng run** đọc qua scoped. Đặc biệt: trong `StorageNodeExecutor`, gọi publish **trước** `TraverseOutputsAsync` (vì mirror trong `finally` của service chạy *sau* khi executor trả về, trong khi traverse đã chạy bên trong executor).

**Loop / Repeat chuột-phím:**

- `ResolveLoopIterations(loopNode, connections, env)` — luôn truyền `env` để ForEach / RepeatN động đọc nguồn qua scoped.
- `GetRepeatCountFromDynamicInputs(node, connections, env)` — Mouse / KeyPress / Hotkey khi repeat lấy từ dây.

**Mở rộng node có output đặc biệt không nằm trong switch mirror:**

- Implement `IWorkflowScopedOutputContributor` trên model node, `AppendScopedStringOutputs` gọi callback `(key, value) => ...` — được gọi sau switch trong `MirrorRuntimeOutputsToScopedStore`.

**Checklist nhanh khi thêm/sửa node có Executor:**

1. Mọi chỗ đọc output node khác → một trong các API `*ForExecution` ở trên (có `env`).
2. Output chuỗi mới sau khi node chạy → đảm bảo đã có trong `MirrorRuntimeOutputsToScopedStore` **hoặc** `IWorkflowScopedOutputContributor`.
3. Ghi `StorageNode.StoredOutputs` → `PublishStorageOutputsToScoped` (và mirror passive Storage đã xử lý trong service).
4. Thêm helper trong `WorkflowExecutionService` đọc giá trị theo node/key → thread `executionId` hoặc `env`, không copy-paste chỉ `NodeDataPanelService`.
5. Host run: sinh `executionId`, truyền vào execution, `finally` → `ClearScopedOutputsForRun`.

**File tham chiếu:** `WorkflowExecutionService.cs` (`ResolveDynamicValueForRun`, `MirrorRuntimeOutputsToScopedStore`, `MirrorOutputsToStorageNodes`), `NodeExecutionEnvironment.cs`, `IWorkflowScopedOutputContributor.cs`, các `*NodeExecutor.cs` đã refactor.

---

## 5. Patterns nâng cao

### 5.1 TitleDisplayMode

Hiển thị label tiêu đề nổi phía trên node trên canvas.

| Mode | Hành vi |
|------|---------|
| `Hidden` | Không hiển thị |
| `Hover` | Hiển thị khi hover chuột vào node |
| `Always` | Luôn hiển thị |

**Cách implement** (xem mẫu đầy đủ trong `DelayNodeControl.cs`):
- Model: `TitleDisplayMode`, `TitleTextBlockUI`
- NodeControl: tạo `titleTextBlock` → thêm vào `host.WorkflowCanvas` (không phải trong `border`)
- `Panel.SetZIndex(titleTextBlock, 20000)` — đảm bảo nổi trên tất cả
- Cleanup trong `border.Unloaded`
- Throttle 50ms khi update vị trí
- Ẩn tiêu đề khi `IsZooming == true`

### 5.2 TitleColorMode

Cho phép user đổi màu chữ tiêu đề độc lập với màu node.

```
TitleColorMode.NodeColor   → dùng node.NodeBrush
TitleColorMode.CustomColor → dùng TitleColorKey làm resource key
                             ("LimeGreen" hoặc "{ColorKey}Brush" trong theme)
```

**⚠️ QUAN TRỌNG**: NodeControl PHẢI dùng `GetTitleBrush(node)` khi tạo `titleTextBlock.Foreground`, KHÔNG dùng `node.NodeBrush` trực tiếp.

### 5.3 ReuseRoutes (Tái sử dụng flow)

Cho phép 1 node chia luồng đi khác nhau tuỳ theo node nào vào:
- Dialog Tab 2: `ItemsControl` với mỗi row = 1 `ReuseRouteItemViewModel`
- Mỗi row: `IncomingNodeTitle` (label) + ComboBox chọn `OutgoingNodeId` + ComboBox `LineStyleKey`
- Khi Save: lưu vào `node.ReuseRoutes` (List<NodeReuseRoute>)
- Runtime: `TraverseOutputsAsync` kiểm tra `IncomingConnection.FromNode` rồi filter connection theo route

### 5.4 PortPosition — Đổi vị trí cổng

```
Left | Top | Right | Bottom
```

- Dialog Tab 2: 2 ComboBox bind `InputPortPosition`/`OutputPortPosition`
- Save: `SavePortPositions()` → cập nhật `NodePort.Position` → `PortRenderer.UpdatePortsPositionOnSide(...)` → `ConnectionRenderer.UpdateAllConnectionPaths(...)`
- Persistence: `PortDto.Position` lưu tự động, phải gán lại khi restore
- PortRenderer tự chia đều các port trên cùng 1 cạnh

### 5.5 Dynamic Input/Output

**Dynamic Inputs** (user tự thêm/xóa input mapping):
- Model: `List<InputMapping> InputMappings`
- ViewModel: `ObservableCollection<InputMappingViewModel> InputMappingsList` + `AddInputCommand`/`RemoveInputCommand`
- Dialog: `ItemsControl` với 3 cột (ComboBox Node, ComboBox Key, TextBox OverrideName)
- Persistence: serialize toàn bộ list

**Dynamic Outputs** (user tự định nghĩa output keys):
- Model: `List<string> OutputKeys` hoặc `List<DynamicOutput>`
- ViewModel: `ObservableCollection<OutputKeyViewModel> OutputKeysList` + Add/Remove commands
- Đồng bộ port với outputs (thêm port ảo cho mỗi output key)
- Persistence: serialize list

---

## 6. Input/Output — Cơ chế truyền dữ liệu giữa các node

### Luồng dữ liệu Input

```
Node A (source)                   Node B (consumer)
├── Outputs["key"] = "value"  →   InputMappings:
│                                 ├── Key: "inputKey"
│                                 ├── SourceNodeId: "A.Id"
│                                 └── SourceOutputKey: "key"
│
│     [Khi B.Executor chạy]
│     env.Service.GetInputValue(B, "inputKey")
│       └── B.InputMappings.Find("inputKey")
│             └── source = A (by SourceNodeId)
│                   └── return A.Outputs["key"]
```

### UI trong Dialog

**InputsPanel** (per input):
```
┌────────────────────────────────────────────┐
│ Key: inputKey                              │
│ [ComboBox: chọn Source Node (A, B, C...)] │
│ [ComboBox: chọn Output Key] ← ẩn nếu 1    │
│ Output key: key               ← hiện key  │
│ Value: "value hiện tại"                    │
└────────────────────────────────────────────┘
```

**OutputsPanel** (per output):
```
┌─────────────────────────────┐
│ Key: outputKey              │
│ Value: "giá trị hiện tại"   │
└─────────────────────────────┘
```

### Refresh nguồn cho Inputs

Khi ViewModel load, gọi `RefreshAvailableSourcesForInputs()`:
- Lấy tất cả nodes trong workflow (có thể là nguồn)
- Đặt vào `InputItemViewModel.AvailableSources`
- Khi user chọn node khác → tự động load `AvailableOutputKeyOptions`

---

## 7. Các lỗi thường gặp & cách tránh

| Lỗi | Nguyên nhân | Cách tránh |
|-----|-------------|-----------|
| Dialog không save khi đóng ngoài nút X | Quên `SaveTitleCommand.Execute(null)` trong `Closing` event | `InitializeBase()` đã xử lý tự động |
| Node bị drag khi mở dialog | Không clear `host.DraggedNode` | Luôn `host.DraggedNode = null` trong `OpenNodeDialog()` |
| Title màu sai sau Save/Load | NodeControl dùng `node.NodeBrush` thay `GetTitleBrush(node)` | Luôn dùng `GetTitleBrush(node)` cho `titleTextBlock.Foreground` |
| Port vị trí sai sau Load | Không gán lại `NodePort.Position` khi restore | Thêm `targetPort.Position = (PortPosition)portDto.Position` |
| PortPosition không lưu | Chỉ thay ComboBox trong VM, không gọi `SavePortPositions()` | Gọi `SavePortPositions()` trong `SaveTitle()` |
| Infinite loop khi chạy | ReuseRoutes trỏ về node đã qua | `ExecutionPath` detect tự động, max 100 nodes |
| TitleTextBlock leak memory | Không cleanup trong `border.Unloaded` | Xóa khỏi Canvas + clear timer trong `Unloaded` |
| `class YourNode` thay `sealed class` | Sai convention | LUÔN dùng `public sealed class` |
| `public new string Title` | Conflict với base class | Dùng `NotifyTitleChanged()` thay thế |
| Input không hiện Output Key combo | `AvailableOutputKeyOptions` chỉ có 1 item | Tự động ẩn, đúng behavior |
| Node palette không map được | Tag XAML khác tên trong TemplateFactory | Tag và switch-case phải khớp chính xác |
| Hai run song song đọc nhầm output Code/HTTP/Storage | Executor gọi `NodeDataPanelService.ResolveDynamicValueByKey` thay vì `ResolveDynamicValueForExecution` | Luôn dùng API có `env` / `ExecutionId` — xem §4.1 |
| Downstream sau Storage sai khi concurrent | Không `PublishStorageOutputsToScoped` trước traverse hoặc sau Assign/Loop | Theo §4.1 và `StorageNodeExecutor` mẫu |
| Loop ForEach / repeat động sai khi concurrent | `ResolveLoopIterations` thiếu `env` hoặc helper loop đọc flat | Truyền `env`; service đã chuẩn hóa — đừng revert |

---

## Tài liệu tham khảo thêm

| Tài liệu | Nội dung |
|----------|---------|
| [Node_Dialog_V2.md](./Node_Dialog_V2.md) | Template code đầy đủ cho mỗi bước |
| [NODE_DIALOG_GUIDE.md](./NODE_DIALOG_GUIDE.md) | Phase checklist chi tiết, Phase 1→4b |
| `DelayNodeControl.cs` | Ví dụ NodeControl với TitleDisplayMode đầy đủ nhất |
| `BaseNodeDialog.xaml.cs` | Lifecycle dialog, LoadInputs/LoadOutputs |
| `NodeExecutionEnvironment.cs` | `ExecutionId`, TraverseOutputsAsync, ReuseRoute runtime |
| `WorkflowExecutionService.cs` | Scoped snapshot, mirror, `ResolveDynamicValueForExecution` / `ResolveDynamicValueForRun` |
| `IWorkflowScopedOutputContributor.cs` | Mở rộng mirror output không sửa switch lớn |
| `INodeExecutor.cs` | Contract cho Executor |

---

*Tài liệu tổng hợp từ codebase Auto_Click_V2 — cập nhật 2026-03-31*
