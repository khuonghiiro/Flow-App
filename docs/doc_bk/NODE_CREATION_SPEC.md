# NODE_CREATION_SPEC — Đặc tả chuẩn tạo Node cho AI Agent

> **Mục đích**: Tài liệu duy nhất, đầy đủ để AI agent đọc và tạo ra một node mới **hoàn chỉnh, không thiếu bất kỳ xử lý nào** trong hệ thống workflow Auto_Click_V2 (WPF/MVVM + C#).
> **Cập nhật**: 2026-04-21

---

## MỤC LỤC

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Danh sách file phải tạo/sửa](#2-danh-sách-file-phải-tạosửa)
3. [Quy trình tạo Node — Checklist bắt buộc](#3-quy-trình-tạo-node--checklist-bắt-buộc)
4. [Template Code cho từng file](#4-template-code-cho-từng-file)
5. [Cơ chế Input/Output & truyền dữ liệu](#5-cơ-chế-inputoutput--truyền-dữ-liệu)
6. [ExecutionId & Scoped Outputs (chạy song song)](#6-executionid--scoped-outputs-chạy-song-song)
7. [Theme System & DynamicResource](#7-theme-system--dynamicresource)
8. [Responsive Screen & Dialog Sizing](#8-responsive-screen--dialog-sizing)
9. [Lỗi thường gặp & cách tránh](#9-lỗi-thường-gặp--cách-tránh)
10. [Reference Implementations](#10-reference-implementations)
11. [IconKey / ColorKey — Checklist bắt buộc cho mọi node](#11-iconkey--colorkey--checklist-bắt-buộc-cho-mọi-node)

---

## 1. Tổng quan kiến trúc

```
┌──────────────────────────────────────────────────────────────┐
│                    WORKFLOW EDITOR                            │
│                                                              │
│  ┌────────────┐    ┌────────────┐    ┌──────────────────┐   │
│  │  Palette   │    │  Canvas    │    │    ViewModel     │   │
│  │ (XAML Tag) │    │ (NodeBorder│    │    (MVVM)        │   │
│  └─────┬──────┘    └─────┬──────┘    └──────────────────┘   │
│        │drag-drop        │right-click                        │
│        ▼                 ▼                                   │
│  ┌───────────────┐  ┌──────────────────────────────────┐    │
│  │TemplateFactory│  │ NodeControl.OpenNodeDialog()      │    │
│  │.Create(tag)   │  │  → NodeDialogManager.OpenDialog() │    │
│  └───────┬───────┘  └──────────┬───────────────────────┘    │
│          ▼                     ▼                             │
│  ┌───────────────┐    ┌────────────────────┐                │
│  │  YourNode     │    │ YourNodeDialog     │                │
│  │ (Model/Data)  │◄───│ (XAML+cs+ViewModel)│                │
│  └───────┬───────┘    └────────────────────┘                │
│          │ Save/Load                                         │
│          ▼                                                   │
│  ┌─────────────────────────┐  ┌──────────────────────────┐  │
│  │FileWorkflowPersistence  │  │WorkflowExecutionService  │  │
│  │(JSON serialize/deser.)  │  │→ NodeExecutors (logic)   │  │
│  └─────────────────────────┘  └──────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. Danh sách file phải tạo/sửa

### File MỚI cần tạo (7 file)

| # | File | Vị trí | Mô tả |
|---|------|--------|-------|
| 1 | `YourNode.cs` | `Models/Nodes/` | Model chứa dữ liệu node |
| 2 | `YourNodeDialog.xaml` | `Views/Overlays/` | Giao diện dialog |
| 3 | `YourNodeDialog.xaml.cs` | `Views/Overlays/` | Code-behind dialog |
| 4 | `YourNodeDialogViewModel.cs` | `ViewModels/` | ViewModel theo MVVM |
| 5 | `YourNodeControl.cs` | `Views/NodeControls/` | Render node trên canvas |
| 6 | `YourNodeRenderer.cs` | `Services/Rendering/` | Quản lý vòng đời render |
| 7 | `YourNodeExecutor.cs` | `Services/Workflow/NodeExecutors/` | Logic thực thi (**nếu cần**) |

### File CẦN SỬA (5 file)

| # | File | Thao tác |
|---|------|----------|
| 1 | `WorkflowEditorWindow.xaml` | Thêm node vào Palette (Border + Tag) |
| 2 | `Services/Workflow/TemplateFactory.cs` | Map Tag → tạo Model (switch case + CreateYourNode) |
| 3 | `Services/Workflow/FileWorkflowPersistenceService.cs` | Serialize + Deserialize properties |
| 4 | `Services/Interaction/WorkflowEditorEventService.cs` | Thêm Ctrl+C / Ctrl+V cho node type |
| 5 | `Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs` | Copy ALL properties trong CreateDuplicateNodeInstance |

### File CẦN SỬA (nếu có Executor)

| # | File | Thao tác |
|---|------|----------|
| 6 | `Services/Workflow/WorkflowExecutionService.cs` | Đăng ký executor + MirrorRuntimeOutputsToScopedStore (nếu output đặc biệt) |
| 7 | `Views/WorkflowEditors/WorkflowEditorWindow.xaml.cs` | Đăng ký Renderer + GetRendererForNode |

---

## 3. Quy trình tạo Node — Checklist bắt buộc

### PHASE 0: Xác nhận thiết kế với user

```yaml
Trước khi code, hỏi/xác nhận:
  □ ColorKey: (ForestPine / SkyAzure / AmberWarm / Fluidity / ...)
  □ Icon: (timer regular / code duotone-regular / globe-pointer / folder-open / ...)
  □ Tên node + mô tả ngắn
  □ Danh sách Inputs (key + kiểu dữ liệu)
  □ Danh sách Outputs (key + kiểu dữ liệu)
  □ Có cần Executor không?
  □ Có cần Dynamic Input/Output không?
  □ NodeType enum value (thêm mới nếu chưa có)
```

### PHASE 1: Core — Bắt buộc cho mọi node

```yaml
Bước 1: Tạo Node Model
  - [ ] Models/Nodes/YourNode.cs
  - [ ] ⚠️ sealed class, kế thừa WorkflowNode
  - [ ] ⚠️ Implement INotifyPropertyChanged
  - [ ] ⚠️ Có NotifyTitleChanged() method
  - [ ] Properties: TitleDisplayMode, TitleTextBlockUI, TitleColorMode, TitleColorKey
  - [ ] Thêm NodeType enum value (nếu chưa có)

Bước 2: Palette + TemplateFactory
  - [ ] Thêm Border vào WorkflowEditorWindow.xaml (Tag + ColorKey + Icon + ToolTip)
  - [ ] Thêm switch case vào TemplateFactory.Create()
  - [ ] Implement CreateYourNode(x, y) với:
        - Id = Guid.NewGuid().ToString()
        - ColorKey + NodeBrush khớp palette
        - Port IN: IsInput=true, Position=Left, ColorKey="Info"
        - Port OUT: IsInput=false, Position=Right, ColorKey="SunsetOrange"

Bước 3: Dialog XAML
  - [ ] Views/Overlays/YourNodeDialog.xaml
  - [ ] ⚠️ Kế thừa BaseNodeDialog (x:Class + xmlns:local)
  - [ ] Header: TitleTextBox + PlayButton + CloseButton
  - [ ] TabControl 2 tabs:
        - Tab "Logic": TitleDisplayMode + TitleColorMode + Custom props + InputsPanel + OutputsPanel
        - Tab "Cấu hình": PortPosition IN/OUT + ReuseRoutes ItemsControl
  - [ ] ⚠️ Dùng DynamicResource thay hardcode color (xem §7)
  - [ ] Nếu có **nhiều dòng** `NodeSearchComboBoxUserControl` + Key trong `ItemsControl`: đọc **[NODE_DIALOG_GUIDE.md](./NODE_DIALOG_GUIDE.md)** (Error 16 + mục **Multi-row `ItemsControl`** ngay sau đó) và bảng §9 hàng **#18** ở tài liệu này

Bước 4: Dialog Code-behind
  - [ ] Views/Overlays/YourNodeDialog.xaml.cs
  - [ ] ⚠️ Kế thừa BaseNodeDialog
  - [ ] Constructor: InitializeComponent() → new ViewModel → InitializeBase(vm, owner)
  - [ ] Override GetInputsPanel() => InputsPanel
  - [ ] Override GetOutputsPanel() => OutputsPanel
  - [ ] CloseButton_Click: SaveTitleCommand.Execute(null) + Close()
  - [ ] (Nếu có TitleColor) TitleColorComboBox_SelectionChanged + UpdateTitleColorPreview()

Bước 5: ViewModel
  - [ ] ViewModels/YourNodeDialogViewModel.cs
  - [ ] ⚠️ Kế thừa BaseNodeDialogViewModel
  - [ ] [ObservableProperty] cho mỗi property
  - [ ] TitleDisplayModeOptions collection
  - [ ] Constructor: base(node, host), sync properties, subscribe PropertyChanged
  - [ ] Override GetDefaultTitle() => "Your Node"
  - [ ] Override OnSaveTitle(): sync VM→node, gọi NotifyTitleChanged()
  - [ ] Override LoadInputs(): Clear + refresh sources + tạo InputItemViewModel
  - [ ] ⚠️ Mọi list source node phải dùng `WorkflowDataSourceOption` (không dùng `WorkflowNode` trực tiếp)
  - [ ] ⚠️ Khi build options từ node thật: dùng `BaseNodeDialogViewModel.CreateDataSourceOption(node)`
  - [ ] Override LoadOutputs(): Clear + tạo OutputItemViewModel

Bước 6: NodeControl
  - [ ] Views/NodeControls/YourNodeControl.cs
  - [ ] ⚠️ static class
  - [ ] Static dictionaries: _titleUpdateTimers, _titleUpdatedAfterZoom
  - [ ] CreateBorder() với 9 event handlers (xem §4.5)
  - [ ] ⚠️ Keyboard Port Position handlers trong MouseEnter/MouseLeave (xem §4.5 mục 5a)
        - Arrow keys (←↑→↓) khi hover: đổi vị trí Port IN
        - Shift + Arrow keys khi hover: đổi vị trí Port OUT
        - border.Focusable = true, border.Focus() trong MouseEnter
        - border.PreviewKeyDown handler gọi ChangePortPosition()
  - [ ] OpenNodeDialog() với 3 critical steps
  - [ ] 7 helper methods (xem §4.5)
  - [ ] ChangePortPosition() helper method (xem §4.5)

Bước 7: Renderer
  - [ ] Services/Rendering/YourNodeRenderer.cs
  - [ ] ⚠️ RenderNode(): CreateBorder + NodeChrome.Apply + ports + color
  - [ ] ⚠️ UpdateNodePosition(): sync title + ports
  - [ ] ⚠️ RemoveNode(): cleanup border + titleTextBlock + ports
  - [ ] Đăng ký trong WorkflowEditorWindow.cs

Bước 8: Persistence
  - [ ] Serialize trong GetNodeProperties(): ALL properties + TitleDisplayMode + TitleColorMode/Key
  - [ ] Deserialize trong RestoreNodeProperties(): ALL properties + enum parse + list deserialize
  - [ ] ⚠️ Hỗ trợ cả string JSON và JsonElement (import/load)

Bước 9: Copy/Paste
  - [ ] WorkflowEditorEventService.cs: Thêm node type vào cả Ctrl+C và Ctrl+V
  - [ ] WorkflowEditorWindow.NodeActions.cs: Copy ALL properties + clone lists
  - [ ] ⚠️ Gọi NotifyTitleChanged() sau set Title

Bước 10: Executor (nếu cần)
  - [ ] Services/Workflow/NodeExecutors/YourNodeExecutor.cs
  - [ ] ⚠️ Dùng ResolveDynamicValueForExecution (có env), KHÔNG NodeDataPanelService
  - [ ] ⚠️ Gọi TraverseOutputsAsync(node) để chạy node tiếp theo
  - [ ] Đăng ký trong WorkflowExecutionService
```

---

## 4. Template Code cho từng file

### 4.1 Node Model — `Models/Nodes/YourNode.cs`

```csharp
using FlowMy.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// [MÔ TẢ CHỨC NĂNG NODE]
    /// </summary>
    public sealed class YourNode : WorkflowNode, INotifyPropertyChanged
    {
        // ===== CUSTOM PROPERTIES =====

        private string _someProperty = string.Empty;
        public string SomeProperty
        {
            get => _someProperty;
            set { if (_someProperty != value) { _someProperty = value; OnPropertyChanged(); } }
        }

        // ===== TITLE DISPLAY (BẮT BUỘC cho mọi node chuẩn) =====

        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Hidden;
        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set { if (_titleDisplayMode != value) { _titleDisplayMode = value; OnPropertyChanged(); } }
        }

        public TextBlock? TitleTextBlockUI { get; set; }

        // ===== TITLE COLOR =====

        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        public TitleColorMode TitleColorMode
        {
            get => _titleColorMode;
            set { if (_titleColorMode != value) { _titleColorMode = value; OnPropertyChanged(); } }
        }

        private string? _titleColorKey;
        public string? TitleColorKey
        {
            get => _titleColorKey;
            set { if (_titleColorKey != value) { _titleColorKey = value; OnPropertyChanged(); } }
        }

        // ===== CONSTRUCTOR =====

        public YourNode()
        {
            Type = NodeType.YourType; // ⚠️ Thêm enum value nếu chưa có
            Title = "Your Node";
        }

        // ===== INOTIFYPROPERTYCHANGED =====

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Gọi khi Title thay đổi từ bên ngoài (ViewModel, copy, edit...)
        /// </summary>
        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));
    }
}
```

**⚠️ QUY TẮC BẮT BUỘC:**
- `public sealed class` — KHÔNG dùng class thường
- KHÔNG `public new string Title { get; set; }` — dùng `NotifyTitleChanged()`
- Properties dùng PascalCase, khởi tạo giá trị mặc định rõ ràng

---

### 4.2 Palette — `WorkflowEditorWindow.xaml`

```xml
<!-- Trong NodeTemplatesPanel -->
<Border Style="{StaticResource PaletteIconNodeStyle}"
        Background="{DynamicResource [ColorKey]Brush}"
        Tag="YourNodeTypeName">
    <Border.ToolTip>
        <ToolTip>
            <StackPanel MaxWidth="220">
                <TextBlock Text="Tên Node" FontWeight="Bold"/>
                <TextBlock Text="Mô tả ngắn về chức năng."
                           TextWrapping="Wrap" Margin="0,2,0,0"/>
            </StackPanel>
        </ToolTip>
    </Border.ToolTip>
    <Border.ContextMenu>
        <ContextMenu Placement="MousePoint" StaysOpen="False">
            <MenuItem IsHitTestVisible="False">
                <MenuItem.Header>
                    <Border Background="{DynamicResource [ColorKey]Brush}"
                            CornerRadius="10" Padding="10"
                            BorderBrush="{DynamicResource BorderColor}" BorderThickness="1">
                        <StackPanel>
                            <TextBlock Text="Tên Node"
                                       Foreground="{DynamicResource TextOn[ColorKey]Brush}"
                                       FontWeight="Bold" FontSize="13"/>
                            <TextBlock Text="Mô tả chi tiết."
                                       Foreground="{DynamicResource TextOn[ColorKey]Brush}"
                                       Opacity="0.9" TextWrapping="Wrap" Margin="0,4,0,0"/>
                        </StackPanel>
                    </Border>
                </MenuItem.Header>
            </MenuItem>
        </ContextMenu>
    </Border.ContextMenu>
    <Grid>
        <controls:SvgViewboxEx Style="{StaticResource PaletteSvgIconStyle}"
                              Source="{Binding Source={x:Static sys:String.Empty},
                                       Converter={StaticResource IconKeyToPathConverter},
                                       ConverterParameter='your-icon-key'}"
                              Fill="{DynamicResource TextOn[ColorKey]Brush}"/>
    </Grid>
</Border>
```

> ⚠️ Thay `[ColorKey]` bằng tên thực (ForestPine, SkyAzure, ...). **Tag** phải khớp chính xác với switch case trong TemplateFactory.

---

### 4.3 TemplateFactory — `Services/Workflow/TemplateFactory.cs`

```csharp
// Thêm vào switch trong Create():
"YourNodeTypeName" => CreateYourNode(x, y),

// Thêm method:
private static WorkflowNode CreateYourNode(double x, double y)
{
    var node = new YourNode
    {
        Id = Guid.NewGuid().ToString(),
        X = x, Y = y,
        ColorKey = "ForestPine", // ⚠️ Khớp với palette Background
        NodeBrush = Application.Current.TryFindResource("ForestPineBrush") as Brush ?? Brushes.Green
    };

    // Port IN (trái)
    node.Ports.Add(new NodePort
    {
        Id = Guid.NewGuid().ToString(),
        IsInput = true,
        Position = PortPosition.Left,
        IsVisible = true,
        ColorKey = "Info" // ⚠️ Port IN mặc định: "Info"
    });

    // Port OUT (phải)
    node.Ports.Add(new NodePort
    {
        Id = Guid.NewGuid().ToString(),
        IsInput = false,
        Position = PortPosition.Right,
        IsVisible = true,
        ColorKey = "SunsetOrange" // ⚠️ Port OUT mặc định: "SunsetOrange"
    });

    return node;
}
```

---

### 4.4 Dialog — `Views/Overlays/YourNodeDialog.xaml`

```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:controls="clr-namespace:FlowMy.Controls"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:FlowMy.Views.Overlays"
        Title="Your Node Dialog"
        WindowStyle="None" ResizeMode="CanResize"
        AllowsTransparency="True" Background="Transparent"
        ShowInTaskbar="False" Topmost="True"
        Width="460" MinWidth="350" MaxWidth="900" MinHeight="350">
    <!-- ⚠️ KHÔNG đặt Height cứng — NodeDialogManager auto-size 90% screen -->

    <Border CornerRadius="12" Padding="0" Style="{DynamicResource DialogOuterBorder}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>  <!-- Header -->
                <RowDefinition Height="*"/>     <!-- TabControl -->
            </Grid.RowDefinitions>

            <!-- ═══════ HEADER ═══════ -->
            <Border Grid.Row="0" Style="{DynamicResource DialogHeaderBorder}" Padding="16,12,12,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <TextBox x:Name="TitleTextBox" Grid.Column="0"
                             Text="{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}"
                             Style="{DynamicResource BaseTextBoxV2}"
                             FontSize="16" Padding="0,4,0,4"
                             VerticalContentAlignment="Center" Cursor="IBeam"/>

                    <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                        <Button x:Name="PlayButton" Padding="0,0,0,0"  Width="24" Height="24"
                                Content="▶" FontSize="12"
                                Style="{DynamicResource PrimaryButton}" Cursor="Hand"
                                Margin="8,0,0,0" ToolTip="Chạy logic node này"
                                Command="{Binding RunSingleNodeCommand}"/>

                        <Button x:Name="CloseButton" Padding="0,0,0,0"  Width="24" Height="24"
                                Style="{DynamicResource DangerButton}"
                                Content="×" FontSize="12" FontWeight="Bold" Cursor="Hand"
                                Margin="8,0,0,0" Click="CloseButton_Click"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- ═══════ TAB CONTROL ═══════ -->
            <TabControl Grid.Row="1" Background="Transparent" BorderThickness="0">

                <!-- ═══ TAB 1: LOGIC ═══ -->
                <TabItem Header="Logic" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>

                            <!-- TitleDisplayMode -->
                            <TextBlock Text="Hiển thị tiêu đề:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <ComboBox x:Name="TitleDisplayModeComboBox" Height="36"
                                      Style="{DynamicResource BaseComboBox}" Margin="0,0,0,16"
                                      ItemsSource="{Binding TitleDisplayModeOptions}"
                                      SelectedValuePath="Value" DisplayMemberPath="DisplayName"
                                      SelectedValue="{Binding TitleDisplayMode, Mode=TwoWay}"/>

                            <!-- TitleColorMode -->
                            <TextBlock Text="Màu tiêu đề:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Grid Margin="0,0,0,16">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <ComboBox x:Name="TitleColorComboBox" Grid.Column="0" Height="36"
                                          Style="{DynamicResource BaseComboBox}"
                                          ItemsSource="{Binding TitleColorOptions}"
                                          SelectedValuePath="Key" DisplayMemberPath="DisplayName"
                                          SelectedValue="{Binding TitleColorKey, Mode=TwoWay}"
                                          SelectionChanged="TitleColorComboBox_SelectionChanged"/>
                                <Border Grid.Column="1" x:Name="TitleColorPreview"
                                        Width="36" Height="36" CornerRadius="6" Margin="8,0,0,0"
                                        BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1"/>
                            </Grid>

                            <!-- ═══ CUSTOM PROPERTIES ═══ -->
                            <!-- Thêm controls cho các property riêng của node -->

                            <!-- ═══ INPUTS ═══ -->
                            <TextBlock Text="Inputs:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <StackPanel x:Name="InputsPanel"/>
                            </Border>

                            <!-- ═══ OUTPUTS ═══ -->
                            <TextBlock Text="Outputs:" x:Name="TextBlockOutputPanel"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border x:Name="BorderOutputPanel"
                                    Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <StackPanel x:Name="OutputsPanel"/>
                            </Border>

                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

                <!-- ═══ TAB 2: CẤU HÌNH ═══ -->
                <TabItem Header="Cấu hình" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>

                            <!-- Port Position -->
                            <TextBlock Text="Vị trí cổng IN/OUT:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Grid Margin="0,0,0,12">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" Margin="0,0,8,0">
                                    <TextBlock Text="Port IN (cổng vào)"
                                               Foreground="{DynamicResource TextBrush}"
                                               FontSize="12" Opacity="0.8" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding InputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                                <StackPanel Grid.Column="1" Margin="8,0,0,0">
                                    <TextBlock Text="Port OUT (cổng ra)"
                                               Foreground="{DynamicResource TextBrush}"
                                               FontSize="12" Opacity="0.8" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding OutputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                            </Grid>

                            <!-- ReuseRoutes -->
                            <ItemsControl ItemsSource="{Binding ReuseRoutes}">
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate>
                                        <Border Margin="0,0,0,12"
                                                Background="{DynamicResource WindowBackground}"
                                                BorderBrush="{DynamicResource ControlBorderBrush}"
                                                BorderThickness="1" CornerRadius="8" Padding="10">
                                            <Grid>
                                                <Grid.ColumnDefinitions>
                                                    <ColumnDefinition Width="2*"/>
                                                    <ColumnDefinition Width="2*"/>
                                                    <ColumnDefinition Width="2*"/>
                                                </Grid.ColumnDefinitions>
                                                <StackPanel Grid.Column="0">
                                                    <TextBlock Text="Node IN"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="12" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <TextBlock Text="{Binding IncomingNodeTitle}"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="13" FontWeight="SemiBold"
                                                               TextTrimming="CharacterEllipsis"/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="1">
                                                    <TextBlock Text="Node OUT"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="12" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <controls:NodeSearchComboBoxUserControl Height="32"
                                                              ItemsSource="{Binding OutgoingOptions}"
                                                              SelectedValuePath="NodeId" DisplayMemberPath="Title"
                                                              SelectedValue="{Binding SelectedOutgoingNodeId, Mode=TwoWay}"
                                                              PlaceholderText="Chọn node OUT..."/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="2" Margin="10,0,0,0">
                                                    <TextBlock Text="Kiểu line OUT"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="12" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                                              ItemsSource="{Binding DataContext.ConnectionLineStyleOptions, RelativeSource={RelativeSource AncestorType=Window}}"
                                                              SelectedValuePath="Key" DisplayMemberPath="DisplayName"
                                                              SelectedValue="{Binding SelectedLineStyleKey, Mode=TwoWay}"/>
                                                </StackPanel>
                                            </Grid>
                                        </Border>
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>

                        </StackPanel>
                    </ScrollViewer>
                </TabItem>
            </TabControl>
        </Grid>
    </Border>
</local:BaseNodeDialog>
```

---

### 4.4b Dialog Code-behind — `Views/Overlays/YourNodeDialog.xaml.cs`

```csharp
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class YourNodeDialog : BaseNodeDialog
    {
        private readonly YourNodeDialogViewModel _viewModel;

        public YourNodeDialog(YourNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new YourNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
            UpdateTitleColorPreview(); // TitleColor preview
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void ViewModel_PropertyChanged(
            object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        // ===== TITLE COLOR PREVIEW =====
        private void TitleColorComboBox_SelectionChanged(
            object sender, SelectionChangedEventArgs e) => UpdateTitleColorPreview();

        private void UpdateTitleColorPreview()
        {
            if (TitleColorPreview == null || TitleColorComboBox?.SelectedValue == null) return;
            var colorKey = TitleColorComboBox.SelectedValue.ToString();
            System.Windows.Media.Brush? brush = null;

            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                if (_viewModel?.Node != null) brush = _viewModel.Node.NodeBrush;
            }
            else if (colorKey == "LimeGreen")
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            else
                brush = Application.Current.TryFindResource(colorKey) as System.Windows.Media.Brush;

            TitleColorPreview.Background = brush
                ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
    }
}
```

---

### 4.4c ViewModel — `ViewModels/YourNodeDialogViewModel.cs`

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly YourNode _yourNode;

        // ===== OBSERVABLE PROPERTIES =====
        [ObservableProperty] private string _someProperty = string.Empty;

        // ===== OPTIONS =====
        public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
        {
            new TitleDisplayModeOption(TitleDisplayMode.Hidden, "Ẩn tiêu đề"),
            new TitleDisplayModeOption(TitleDisplayMode.Hover, "Hiện khi hover"),
            new TitleDisplayModeOption(TitleDisplayMode.Always, "Luôn hiện")
        };

        // ===== CONSTRUCTOR =====
        public YourNodeDialogViewModel(YourNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _yourNode = node ?? throw new ArgumentNullException(nameof(node));

            // Sync properties từ node → VM
            SomeProperty = _yourNode.SomeProperty;

            // Subscribe PropertyChanged
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(YourNode.SomeProperty))
                        SomeProperty = _yourNode.SomeProperty;
                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }
        }

        protected override string GetDefaultTitle() => "Your Node";

        // ===== SAVE: VM → Node =====
        protected override void OnSaveTitle()
        {
            if (_yourNode.SomeProperty != SomeProperty)
            {
                _yourNode.SomeProperty = SomeProperty;
                _host.RequestSyncDataPanels(immediate: true);
            }
            _yourNode.NotifyTitleChanged();
        }

        // ===== LOAD INPUTS =====
        protected override void LoadInputs()
        {
            Inputs.Clear();
            // ⚠️ RefreshAvailableSourcesForInputs() trước khi tạo InputItemViewModel
            // Thêm InputItemViewModel cho mỗi input
        }

        // ===== LOAD OUTPUTS =====
        protected override void LoadOutputs()
        {
            Outputs.Clear();
            foreach (var kvp in _yourNode.Outputs)
            {
                Outputs.Add(new OutputItemViewModel(kvp.Key, kvp.Value));
            }
        }
    }
}
```

---

### 4.5 NodeControl — `Views/NodeControls/YourNodeControl.cs`

**Đây là file QUAN TRỌNG NHẤT**, chứa toàn bộ logic render node trên canvas.

```csharp
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowMy.Views.NodeControls
{
    public static class YourNodeControl
    {
        // ===== STATIC FIELDS =====
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer>
            _titleUpdateTimers = new();
        private static readonly System.Collections.Generic.Dictionary<Border, bool>
            _titleUpdatedAfterZoom = new();
        private const int TitleUpdateThrottleMs = 50;

        // ===== CREATEBORDER =====
        public static Border CreateBorder(
            YourNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // ─── 1. GRID CONTAINER ───
            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

            // ─── 2. ICON ───
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(
                null, typeof(Uri), "your-icon-key", // ⚠️ Thay bằng icon thực
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32, Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetTextBrush(node.ColorKey)
            };
            grid.Children.Add(iconSvg);

            // ─── 3. BORDER CHÍNH ───
            var border = new Border
            {
                Child = grid,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Direction = 270,
                    ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node // ⚠️ CRITICAL: renderer dùng Tag để lấy node
            };

            // ─── 4. TITLE TEXTBLOCK ───
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Your Node",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = GetTitleBrush(node), // ⚠️ CRITICAL: dùng GetTitleBrush, KHÔNG node.NodeBrush
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock; // ⚠️ CRITICAL: lưu reference

            // ─── 5a. HOVER EVENTS + KEYBOARD PORT POSITION ───
            bool isHovering = false;

            // ⚠️ BẮT BUỘC: Cho phép border nhận focus keyboard khi hover
            border.Focusable = true;
            border.FocusVisualStyle = null; // Ẩn focus visual mặc định (nét đứt)

            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                // ⚠️ CRITICAL: Dùng Dispatcher.BeginInvoke để focus đáng tin cậy
                // Gọi border.Focus() trực tiếp có thể bị WPF bỏ qua khi layout chưa sẵn sàng
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (isHovering) border.Focus(); }));
            };
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            };

            // ─── 5a-2. KEYBOARD PORT POSITION (Arrow = Port IN, Shift+Arrow = Port OUT) ───
            // Khi chuột hover trên node + nhấn phím mũi tên → đổi vị trí port ngay lập tức
            // KHÔNG cần mở dialog → UX nhanh hơn
            border.PreviewKeyDown += (s, e) =>
            {
                if (!isHovering) return;

                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PortPosition? newPos = e.Key switch
                {
                    Key.Left  => PortPosition.Left,
                    Key.Up    => PortPosition.Top,
                    Key.Right => PortPosition.Right,
                    Key.Down  => PortPosition.Bottom,
                    _ => null
                };

                if (newPos == null) return;

                e.Handled = true;
                ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
            };

            // ─── 5a-3. LƯU Ý ĐẶC BIỆT CHO CONDITIONAL NODE (Diamond mode) ───
            // ConditionalDiamondControl có 2 loại element nhận phím:
            //
            // 1. Diamond border (hình thoi chính):
            //    - Arrow: đổi Port IN của diamond
            //    - Shift+Arrow: đổi TẤT CẢ Port OUT (hướng output từ diamond)
            //    - Guard: if (!border.IsKeyboardFocusWithin) return;
            //
            // 2. Satellite circles (hình tròn điều kiện, mỗi nhánh 1 circle):
            //    - Arrow: đổi SatelliteInputPosition (hướng line đi vào circle)
            //    - Shift+Arrow: đổi branch.Port.Position (port OUT riêng của nhánh)
            //    - Guard: if (!satelliteBorder.IsKeyboardFocusWithin) return;
            //
            // ⚠️ CRITICAL: Dùng IsKeyboardFocusWithin (không phải IsKeyboardFocused)
            //    để tránh xung đột giữa diamond và satellite.
            //    IsKeyboardFocused quá strict (focus có thể ở child element).
            //    IsKeyboardFocusWithin đúng vì satellite KHÔNG nằm trong visual tree
            //    của diamond border (chúng là siblings trên Canvas).

            // ─── 5b. PROPERTYCHANGED ───
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = node.Title ?? "Your Node";
                        if (node.Border?.Visibility == Visibility.Visible)
                            UpdateTitlePosition(titleTextBlock, border, host);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(YourNode.TitleColorMode) ||
                             e.PropertyName == nameof(YourNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(YourNode.TitleDisplayMode))
                    {
                        if (node.Border?.Visibility == Visibility.Visible)
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    }
                };
            }

            // ─── 5c. VISIBILITY SYNC (viewport culling) ───
            var visibilityDescriptor = DependencyPropertyDescriptor.FromProperty(
                UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                    titleTextBlock.Visibility = Visibility.Collapsed;
                else
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            });

            // ─── 5d. LOADED ───
            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            // ─── 5e. SIZECHANGED ───
            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);

            // ─── 5f. UNLOADED (cleanup) ───
            border.Unloaded += (s, e) =>
            {
                try
                {
                    if (_titleUpdateTimers.TryGetValue(border, out var timer))
                    {
                        timer.Stop(); _titleUpdateTimers.Remove(border);
                    }
                    _titleUpdatedAfterZoom.Remove(border);
                    if (host.WorkflowCanvas?.Children.Contains(titleTextBlock) == true)
                        host.WorkflowCanvas.Children.Remove(titleTextBlock);
                    if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                        node.TitleTextBlockUI = null;
                }
                catch { }
            };

            // ─── 5g. LAYOUTUPDATED (zoom handling) ───
            border.LayoutUpdated += (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    return;
                }
                if (NodeChrome.IsZooming)
                {
                    if (titleTextBlock.Visibility != Visibility.Collapsed)
                        titleTextBlock.Visibility = Visibility.Collapsed;
                    _titleUpdatedAfterZoom[border] = false;
                    return;
                }
                bool hasUpdated = _titleUpdatedAfterZoom.TryGetValue(border, out var u) && u;
                if (!hasUpdated && border.Visibility == Visibility.Visible)
                {
                    _titleUpdatedAfterZoom[border] = true;
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    if (titleTextBlock.Visibility == Visibility.Visible)
                        UpdateTitlePosition(titleTextBlock, border, host);
                }
                if (host.IsPanning || host.DraggedNode == node) return;
                if (titleTextBlock.Visibility == Visibility.Visible)
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
            };

            // ─── 6. RIGHT-CLICK → OPEN DIALOG ───
            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            return border;
        }

        // ===== OPEN DIALOG =====
        private static void OpenNodeDialog(
            YourNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                // ⚠️ CRITICAL: 3 bước bắt buộc
                if (node.Border?.IsMouseCaptured == true) node.Border.ReleaseMouseCapture();
                host.DraggedNode = null;
                if (host.ViewModel != null) host.ViewModel.SelectedNode = null;

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new YourNodeDialog(
                    node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== HELPERS =====
        private static Brush GetTitleBrush(YourNode node)
        {
            if (node.TitleColorMode != TitleColorMode.CustomColor ||
                string.IsNullOrEmpty(node.TitleColorKey) || node.TitleColorKey == "NodeColor")
                return node.NodeBrush;
            if (node.TitleColorKey == "LimeGreen")
                return new SolidColorBrush(Colors.LimeGreen);
            return Application.Current.TryFindResource(node.TitleColorKey) as Brush ?? node.NodeBrush;
        }

        private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering)
            => mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };

        private static void UpdateTitleVisibility(
            TextBlock tb, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
        {
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible)
            { tb.Visibility = Visibility.Collapsed; return; }
            tb.Visibility = GetTitleVisibility(mode, isHovering);
        }

        private static void ThrottledUpdateTitlePosition(
            TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (!_titleUpdateTimers.TryGetValue(border, out var timer))
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
                timer.Tick += (s, e) => { timer.Stop(); UpdateTitlePosition(tb, border, host); };
                _titleUpdateTimers[border] = timer;
            }
            timer.Stop(); timer.Start();
        }

        private static void UpdateTitlePosition(
            TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (host.WorkflowCanvas == null || border == null ||
                !host.WorkflowCanvas.Children.Contains(tb)) return;
            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);
            if (double.IsNaN(left) || double.IsNaN(top)) return;
            if (tb.ActualWidth == 0 || tb.ActualHeight == 0)
            {
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                tb.Arrange(new Rect(tb.DesiredSize));
            }
            Canvas.SetLeft(tb, left + (border.ActualWidth / 2) - (tb.ActualWidth / 2));
            Canvas.SetTop(tb, top - tb.ActualHeight - 4);
        }

        private static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            if (host is WorkflowEditorWindow window)
            {
                var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager) return manager;
            }
            return new NodeDialogManager();
        }

        private static Brush GetTextBrush(string? colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey))
                return new SolidColorBrush(Color.FromRgb(148, 163, 184));
            return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        // ===== KEYBOARD PORT POSITION =====
        /// <summary>
        /// Đổi vị trí port IN hoặc OUT bằng phím mũi tên khi hover.
        /// Arrow keys (không Shift) → đổi Port IN.
        /// Shift + Arrow keys → đổi Port OUT.
        /// 
        /// ⚠️ LƯU Ý:
        /// - Node thường (1 IN + 1 OUT): dùng FirstOrDefault tìm port.
        /// - ConditionalNode (nhiều OUT): ConditionalNodeControl/ConditionalDiamondControl
        ///   override logic này — xem riêng trong §5a-3 phía trên.
        ///   + Classic mode: Shift+Arrow đổi TẤT CẢ output ports cùng lúc
        ///                  + gọi ReRenderConditionalNode + RenderConditionalNodePorts
        ///   + Diamond mode: Diamond border đổi tất cả output ports,
        ///                   Satellite circle đổi riêng branch port.
        /// </summary>
        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;

            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);

            if (port == null || port.Position == newPosition) return;

            port.Position = newPosition;

            // Cập nhật vị trí port trên canvas
            host.UpdatePortsPositionOnSide(node, newPosition);

            // Redraw connections để line bám theo port mới
            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                }
                catch { /* best-effort */ }
            }
        }
    }
}
```

---

### 4.6 Renderer — `Services/Rendering/YourNodeRenderer.cs`

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Services.Rendering
{
    public sealed class YourNodeRenderer : INodeRenderer
    {
        private readonly IWorkflowEditorHost _host;
        private readonly PortRenderer _portRenderer;
        public IWorkflowEditorHost Host => _host;

        public YourNodeRenderer(IWorkflowEditorHost host, PortRenderer portRenderer)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not YourNode yourNode)
                throw new InvalidOperationException("YourNodeRenderer can only render YourNode.");

            yourNode.Border = YourNodeControl.CreateBorder(
                yourNode, _host as Window, _host);
            NodeChrome.Apply(yourNode.Border, yourNode, _host);

            canvas.Children.Add(yourNode.Border);
            Canvas.SetLeft(yourNode.Border, yourNode.X);
            Canvas.SetTop(yourNode.Border, yourNode.Y);
            _host.ZIndexManager.SetNodeZIndex(yourNode);

            // ⚠️ CRITICAL: Render ports với màu đúng
            RenderPorts(yourNode);
        }

        public void UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            node.X = x; node.Y = y;
            if (node.Border != null)
            {
                Canvas.SetLeft(node.Border, x);
                Canvas.SetTop(node.Border, y);
            }
            // ⚠️ Title position sync
            if (node is YourNode yn && yn.TitleTextBlockUI != null && _host.WorkflowCanvas != null)
            {
                var tb = yn.TitleTextBlockUI;
                if (!_host.WorkflowCanvas.Children.Contains(tb))
                {
                    _host.WorkflowCanvas.Children.Add(tb);
                    Panel.SetZIndex(tb, 20000);
                }
                if (node.Border != null)
                {
                    if (tb.ActualWidth == 0 || tb.ActualHeight == 0)
                    {
                        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        tb.Arrange(new Rect(tb.DesiredSize));
                    }
                    Canvas.SetLeft(tb, x + (node.Border.ActualWidth / 2) - (tb.ActualWidth / 2));
                    Canvas.SetTop(tb, y - tb.ActualHeight - 4);
                }
            }
            // ⚠️ CRITICAL: update ports
            RenderPorts(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node.Border != null && canvas.Children.Contains(node.Border))
                canvas.Children.Remove(node.Border);
            // ⚠️ Cleanup title
            if (node is YourNode yn && yn.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(yn.TitleTextBlockUI))
                    canvas.Children.Remove(yn.TitleTextBlockUI);
                yn.TitleTextBlockUI = null;
            }
            // Remove ports
            foreach (var port in node.Ports)
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    canvas.Children.Remove(port.PortUI);
        }

        private void RenderPorts(WorkflowNode node)
        {
            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                Color portColor;
                if (!string.IsNullOrWhiteSpace(port.ColorKey))
                {
                    var c = GetColorFromTheme($"{port.ColorKey}Brush")
                         ?? GetColorFromTheme(port.ColorKey);
                    portColor = c ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
                }
                else
                {
                    portColor = port.IsInput
                        ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                        : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
                }

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor); // ⚠️ ALWAYS update
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                _host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }
        }

        private Color? GetColorFromTheme(string key)
        {
            var resource = Application.Current.TryFindResource(key);
            if (resource is SolidColorBrush brush) return brush.Color;
            if (resource is Color color) return color;
            return null;
        }
    }
}
```

---

### 4.7 Executor — `Services/Workflow/NodeExecutors/YourNodeExecutor.cs`

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class YourNodeExecutor : INodeExecutor
    {

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var yn = (YourNode)node;
            env.CancellationToken.ThrowIfCancellationRequested();

            // ===== 1. ĐỌC INPUT (⚠️ PHẢI dùng API có env) =====
            // var inputValue = env.Service.ResolveDynamicValueForExecution(sourceNode, "key", env);
            // HOẶC: env.Service.ResolveValueByNodeIdAndKeyForExecution(env.Connections, nodeId, "key", env);
            // ⚠️ KHÔNG dùng NodeDataPanelService.ResolveDynamicValueByKey trong executor!

            // ===== 2. LOGIC NGHIỆP VỤ =====
            // var result = await DoWorkAsync(..., env.CancellationToken);

            // ===== 3. GHI OUTPUT =====
            // yn.Outputs["outputKey"] = result;

            // ===== 4. TRAVERSE (BẮT BUỘC) =====
            await env.TraverseOutputsAsync(node);
        }
    }
}
```

---

### 4.8 Persistence — `FileWorkflowPersistenceService.cs`

#### Serialize (GetNodeProperties)

```csharp
// Thêm trong GetNodeProperties():
if (node is YourNode yn)
{
    if (!string.IsNullOrWhiteSpace(yn.SomeProperty))
        dict["SomeProperty"] = yn.SomeProperty;
    dict["TitleDisplayMode"] = yn.TitleDisplayMode.ToString();
    dict["TitleColorMode"] = yn.TitleColorMode.ToString();
    if (!string.IsNullOrEmpty(yn.TitleColorKey))
        dict["TitleColorKey"] = yn.TitleColorKey;
    // List: dùng JsonSerializer.Serialize(...)
}
```

#### Deserialize (RestoreNodeProperties)

```csharp
// Thêm trong RestoreNodeProperties():
if (node is YourNode yn)
{
    if (props.TryGetValue("SomeProperty", out var sp))
        yn.SomeProperty = sp?.ToString() ?? "";

    if (props.TryGetValue("TitleDisplayMode", out var tdm) &&
        Enum.TryParse<TitleDisplayMode>(tdm?.ToString(), out var tdmVal))
        yn.TitleDisplayMode = tdmVal;

    if (props.TryGetValue("TitleColorMode", out var tcm) &&
        Enum.TryParse<TitleColorMode>(tcm?.ToString(), out var tcmVal))
        yn.TitleColorMode = tcmVal;

    if (props.TryGetValue("TitleColorKey", out var tck))
        yn.TitleColorKey = tck?.ToString();
    
    // List: phải handle cả string và JsonElement
}
```

> ⚠️ **PortPosition** lưu tự động qua `PortDto.Position`. Khi restore **phải** gán `targetPort.Position = (PortPosition)portDto.Position`.

---

### 4.9 Copy/Paste

#### WorkflowEditorEventService.cs

```csharp
// Ctrl+C: Thêm YourNode vào if condition
if (vm.SelectedNode is YourNode || vm.SelectedNode is InputNode || ...)
{ _copiedNode = vm.SelectedNode; e.Handled = true; return; }

// Ctrl+V: Thêm _copiedNode is YourNode tương tự
```

#### WorkflowEditorWindow.NodeActions.cs

```csharp
// Trong CreateDuplicateNodeInstance():
if (source is YourNode src && node is YourNode dst)
{
    dst.SomeProperty = src.SomeProperty;
    dst.TitleDisplayMode = src.TitleDisplayMode;
    dst.TitleColorMode = src.TitleColorMode;
    dst.TitleColorKey = src.TitleColorKey;
    // ⚠️ Clone lists: new List<T>(src.List)
}
// Sau khi set Title:
if (node is YourNode ynn) ynn.NotifyTitleChanged();
```

#### WorkflowEditorWindow.MultiNodeClipboard.cs (Multi-select copy/paste)

```csharp
// Sau khi clone toàn bộ node và map sourceId -> newNode:
RemapPastedNodeReferences(nodeMap);

// Quy tắc bắt buộc:
// - Mọi field dạng SourceNodeId/TargetNodeId phải remap sang Id mới
// - Không giữ Id gốc của workflow cũ (sẽ làm combobox chọn lệch)
```

Checklist remap cho node có combobox chọn node/key/value:
- Remap trong cấu trúc chung: `DynamicInputs`, `ReuseRoutes`, `ConditionalBranches`, `SubConditions`.
- Remap trong node chuyên biệt: `InputMappings`, `OutputMappings`, `Assignments`, `Headers`, `QueryParams`, `FormData`, `RequestInterceptRules`, `AsyncDataSources`, `AdditionalAppendSources`, ...
- Chỉ remap khi `oldId` nằm trong tập node đã copy (`nodeMap`); nếu không có thì giữ nguyên để không phá reference ngoài vùng copy.
- Thực hiện remap **trước** khi render lại UI/refresh dialog để combobox bind đúng data source mới.

Template áp dụng nhanh khi thêm node mới:

```csharp
case YourNode yourNode:
    yourNode.SourceNodeId = RemapNodeId(yourNode.SourceNodeId, sourceToNewNodeMap);
    yourNode.TargetNodeId = RemapNodeId(yourNode.TargetNodeId, sourceToNewNodeMap);

    foreach (var m in yourNode.InputMappings ?? new List<YourInputMapping>())
        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);

    if (yourNode.Routes != null)
    {
        foreach (var r in yourNode.Routes)
        {
            r.FromNodeId = RemapNodeId(r.FromNodeId, sourceToNewNodeMap);
            r.ToNodeId = RemapNodeId(r.ToNodeId, sourceToNewNodeMap);
        }
    }
    break;
```

Quy ước đặt tên field tham chiếu node (để không sót remap):
- Dùng hậu tố `NodeId` cho mọi field giữ Id của node khác (ví dụ: `SourceNodeId`, `TargetNodeId`, `FolderSourceNodeId`).
- Với list mapping, field tham chiếu trong item cũng phải theo chuẩn `*NodeId`.
- Không dùng tên mơ hồ như `Source`, `From`, `NodeRef` cho kiểu dữ liệu string chứa node id.
- Khi review PR: search nhanh `NodeId` trong class mới và đối chiếu xem đã có trong `RemapNodeReferenceIds(...)` chưa.

### 4.9.1 One-pass implementation pack (làm 1 thể, không sót bước)

Mục tiêu: thêm/sửa node mà không bị lỗi copy/paste lệch combobox source.

`Phase A - Model/Persistence`
- [ ] Toàn bộ field tham chiếu node đặt tên `*NodeId`.
- [ ] Serialize/Deserialize đủ trong `FileWorkflowPersistenceService` (không mất field sau Save/Load).
- [ ] List/object lồng nhau được clone đúng (tránh reference sharing).

`Phase B - Duplicate/Paste`
- [ ] `CreateDuplicateNodeInstance()` copy ALL properties (bao gồm mode, layout, options, title settings).
- [ ] Multi-node paste gọi `RemapPastedNodeReferences(nodeMap)` ngay sau khi dựng `nodeMap`.
- [ ] `RemapNodeReferenceIds(...)` có case cho node mới + remap nested mappings.
- [ ] Chỉ remap id thuộc selection; id ngoài selection giữ nguyên.

`Phase C - UI/Dialog consistency`
- [ ] Sau paste, combobox Node/Key/Value bind vào node copy (`- copy n`), không còn trỏ node gốc.
- [ ] Nếu node có geometry đặc thù (diamond/body/satellite), chạy refresh layout sau paste.
- [ ] `NotifyTitleChanged()` hoặc cơ chế PropertyChanged tương đương được gọi đúng lúc.

`Phase D - Verification bắt buộc`
- [ ] Case 1: copy/paste trong cùng canvas -> connection và combobox đúng.
- [ ] Case 2: copy sang workflow mới -> cấu hình và mapping vẫn đúng.
- [ ] Case 3: copy cụm có nested mapping (conditional/routes/dynamic inputs) -> không lệch source.
- [ ] Build pass (`dotnet build /p:UseAppHost=false` nếu apphost đang lock).

`Done criteria (được coi là hoàn tất)`
- [ ] Không còn field `*NodeId` nào trong node mới mà chưa remap ở paste.
- [ ] Không còn hiện tượng combobox source chọn node cũ sau paste.
- [ ] Không regression line/port connection ở node đặc thù.

`PR checklist copy nhanh`
- [ ] Copy ALL properties ở duplicate.
- [ ] Remap ALL `*NodeId` sau paste (bao gồm nested).
- [ ] Tested 3 case copy/paste (same canvas / new workflow / nested mapping).
- [ ] Build pass.

---

## 5. Cơ chế Input/Output & truyền dữ liệu

```
Node A (source)                     Node B (consumer)
├── Outputs["key"] = "value"    →   InputMappings:
│                                   ├── Key: "inputKey"
│                                   ├── SourceNodeId: "A.Id"
│                                   └── SourceOutputKey: "key"
│
│   [Khi B.Executor chạy]
│   env.Service.ResolveDynamicValueForExecution(sourceNode, "key", env)
│     → Ưu tiên scoped snapshot → fallback Outputs["key"]
```

### UI trong Dialog

**InputsPanel** (BaseNodeDialog tự render):
- Label "Key: inputKey"
- NodeSearchComboBoxUserControl chọn Source Node (AvailableSources)
- ComboBox chọn Output Key (ẩn nếu chỉ có 1 key)
- TextBlock hiển thị giá trị hiện tại

**OutputsPanel** (BaseNodeDialog tự render):
- Label "Key: outputKey"
- TextBlock hiển thị giá trị hiện tại

---

## 6. ExecutionId & Scoped Outputs (chạy song song)

> ⚠️ BẮT BUỘC đọc khi viết Executor

| Thành phần | Vai trò |
|---|---|
| `ExecutionId` | Mỗi lần chạy workflow có 1 id duy nhất |
| `_scopedStringOutputsByRun` | Snapshot output theo ExecutionId |
| `ResolveDynamicValueForExecution(node, key, env)` | Đọc output node khác (ưu tiên scoped) |
| `ResolveValueByNodeIdAndKeyForExecution(conns, id, key, env)` | Đọc theo NodeId + key |
| `MirrorRuntimeOutputsToScopedStore(node, execId)` | Copy output vào snapshot |
| `PublishStorageOutputsToScoped(storage, execId)` | StorageNode: publish trước TraverseOutputsAsync |
| `IWorkflowScopedOutputContributor` | Mở rộng mirror cho node output đặc biệt |

**QUY TẮC:**
- Trong Executor: **LUÔN** dùng `*ForExecution` APIs có `env`
- **KHÔNG** dùng `NodeDataPanelService.ResolveDynamicValueByKey` trong executor
- Output mới → đảm bảo có trong `MirrorRuntimeOutputsToScopedStore` hoặc `IWorkflowScopedOutputContributor`
- StorageNode → `PublishStorageOutputsToScoped` trước `TraverseOutputsAsync`

---

## 7. Theme System & DynamicResource

### ⚠️ BẮT BUỘC: Dùng DynamicResource, KHÔNG hardcode color

| Resource Key | Dùng cho | Thay cho |
|---|---|---|
| `DialogOuterBorder` | Style outer border dialog | `Background="#FF1E293B"` |
| `DialogHeaderBorder` | Style header dialog | `Background="#FF0F172A"` |
| `TextBrush` | Foreground text chính | `Foreground="White"` |
| `TextSecondary` | Text phụ / mô tả | `Foreground="#CCCCCC"` |
| `TextMuted` | Text rất mờ, placeholder | `Opacity="0.5"` |
| `WindowBackground` | Background card/panel | `Background="#FF1E293B"` |
| `ControlBorderBrush` | BorderBrush controls | `BorderBrush="#33FFFFFF"` |
| `BaseTextBoxV2` | Style TextBox | — |
| `BaseComboBox` | Style ComboBox | — |
| `PrimaryButton` | Style nút Play | — |
| `DangerButton` | Style nút Close | — |
| `HttpTabItemStyle` | Style TabItem (**StaticResource**) | — |

> ⚠️ `HttpTabItemStyle` dùng **`{StaticResource}`**, không phải `{DynamicResource}`.

---

## 8. Responsive Screen & Dialog Sizing

```xml
<!-- Template kích thước đề xuất -->
<local:BaseNodeDialog
    Width="460"       <!-- Chiều rộng mặc định -->
    MinWidth="350"    <!-- Tối thiểu -->
    MaxWidth="900"    <!-- Giới hạn tối đa -->
    MinHeight="350"   <!-- MinHeight bắt buộc -->
    <!-- ⚠️ KHÔNG đặt Height cứng — NodeDialogManager auto-size -->
    <!-- ⚠️ KHÔNG đặt MaxHeight cố định -->
>
```

`NodeDialogManager` tự động:
- Chiều cao = `90% WorkArea` (tôn trọng MinHeight/MaxHeight)
- Vị trí = sát cạnh phải, căn giữa dọc
- Scale = áp dụng `UIScaleFactor` qua `LayoutTransform`

---

## 9. Lỗi thường gặp & cách tránh

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| 1 | Dialog không save khi đóng bên ngoài nút X | Quên `SaveTitleCommand` trong Closing | `InitializeBase()` đã tự xử lý |
| 2 | Node bị drag khi mở dialog | Không clear `host.DraggedNode` | Luôn `host.DraggedNode = null` trong OpenNodeDialog |
| 3 | Title màu sai sau Save/Load | Dùng `node.NodeBrush` thay `GetTitleBrush(node)` | Luôn dùng `GetTitleBrush(node)` |
| 4 | Port vị trí sai sau Load | Không gán lại `NodePort.Position` khi restore | `targetPort.Position = (PortPosition)portDto.Position` |
| 5 | PortPosition không lưu | Không gọi `SavePortPositions()` | Base ViewModel đã gọi trong `SaveTitle()` |
| 6 | TitleTextBlock memory leak | Không cleanup trong Unloaded | Xóa khỏi Canvas + clear timer trong Unloaded |
| 7 | `class YourNode` thay `sealed class` | Sai convention | LUÔN dùng `public sealed class` |
| 8 | `public new string Title` | Conflict base class | Dùng `NotifyTitleChanged()` |
| 9 | Palette Tag không match TemplateFactory | Tên khác nhau | Tag và switch-case phải khớp chính xác |
| 10 | Port colors mất/sai | Chỉ set khi `port.PortUI == null` | ALWAYS update color trong CẢ RenderNode và UpdateNodePosition |
| 11 | Hai run song song đọc nhầm output | Executor dùng `NodeDataPanelService` | Dùng `*ForExecution` API có `env` — xem §6 |
| 12 | ComboBox source hiện title cũ | Không refresh AvailableSources | Gọi `RefreshAvailableSourcesForInputs()` trong LoadInputs |
| 13 | Node nhảy vị trí khi mở dialog | Node vẫn selected/captured | OpenNodeDialog: release capture + deselect |
| 14 | Properties mất khi save/load | Không implement Persistence | Serialize + Deserialize TẤT CẢ properties |
| 15 | Copy/Paste mất properties | Không copy all properties | Copy ALL + clone lists + NotifyTitleChanged |
| 16 | Copy/Paste xong combobox chọn sai node nguồn | Không remap `SourceNodeId`/`TargetNodeId` sang node mới | Gọi `RemapPastedNodeReferences(nodeMap)` trong `WorkflowEditorWindow.MultiNodeClipboard.cs` |
| 17 | Arrow keys không đổi port khi hover | Quên `border.Focusable = true` hoặc `border.Focus()` trong MouseEnter | Đặt `border.Focusable = true`, `FocusVisualStyle = null`, gọi `border.Focus()` trong MouseEnter, thêm `PreviewKeyDown` handler gọi `ChangePortPosition()` |
| 18 | Nhiều dòng `NodeSearchComboBoxUserControl` + Key trong dialog bị đồng bộ sai / mất selection khi mở lại hoặc đổi connection | `Loaded` gọi lại refresh full sau ctor; `Clear()` + `Add()` `ItemsSource` khi đã bind `SelectedValue` TwoWay; nhiều row dùng chung `ItemsSource` + CurrentItem; so `NodeId` sai case | Đọc **[NODE_DIALOG_GUIDE.md](./NODE_DIALOG_GUIDE.md)** mục **Error 16** và ngay bên dưới mục **Multi-row `ItemsControl`: nhiều NodeSearchComboBox + Key**; pattern: `CodeNodeDialog` / `FlowOverwriteNodeDialog`; một list node ở parent VM, bind qua `RelativeSource AncestorType=ItemsControl`, thay collection một lần, `IsSynchronizedWithCurrentItem=False`, so khớp `OrdinalIgnoreCase`, Key chỉ từ `DynamicOutputs` của node đã chọn |

---

## 10. Reference Implementations

| Node | File tham khảo | Đặc điểm |
|------|---------------|-----------|
| **DelayNode** | `DelayNodeControl.cs` | TitleDisplayMode đầy đủ nhất |
| **AssignDataNode** | `AssignDataNodeDialog.xaml` | Dialog mẫu chuẩn theme + responsive |
| **CodeNode** | `CodeNodeDialog.xaml` | Dynamic Input mapping + Dynamic Output keys |
| **InputNode** | `InputNodeControl.cs` | Basic + TitleDisplayMode |
| **WebNode** | `WebNodeControl.cs` | Advanced: input mapping + port position + rectangular ports |
| **CallbackNode** | `CallbackNodeControl.cs` | Combobox Node + Key pattern |
| **BaseNodeDialog.xaml.cs** | `Views/Overlays/` | Lifecycle dialog: Loaded → SetupInputsOutputs → LoadInputs/LoadOutputs |
| **BaseNodeDialogViewModel** | `ViewModels/` | NodeTitle, SaveTitleCommand, RunSingleNodeCommand, Inputs/Outputs, PortPosition, ReuseRoutes |

---

## 11. IconKey / ColorKey — Checklist bắt buộc cho mọi node

> Mọi node mới (hoặc node cũ bị thiếu) PHẢI khai báo đầy đủ ở **4 chỗ** dưới đây. Nếu thiếu bất kỳ
> chỗ nào, node sẽ bị rơi vào fallback (icon `circle-nodes`, nền `AccentBrush`, icon màu đen trên nền tối).

### 11.1 Bốn chỗ phải khớp nhau

| # | File | Khai báo gì | Ghi chú |
|---|------|-------------|---------|
| 1 | `Views/WorkflowEditorWindow.xaml` (khối `NodeTemplatesPanel`) | `Tag="<NodeType>"` + `Background="{DynamicResource <ColorKey>Brush}"` + `ConverterParameter='<iconKey>'` + `Fill="{DynamicResource TextOn<ColorKey>Brush}"` | Hiển thị ở palette bên trái |
| 2 | `Views/WorkflowEditors/WorkflowEditorWindow.TemplateNodeHandler.cs` (`GetIconNameForNodeType`) | `case "<NodeType>" => "<iconKey>"` | Icon node hiển thị trên canvas |
| 3 | `Workflow/TemplateFactory.cs` (hàm `Create<NodeType>`) | `ColorKey = "<ColorKey>"` trên model | Dùng cho theme & serialize |
| 4 | `ViewModels/WorkflowEditorViewModel.cs` (`ResolveNodeIconKey`) | `NodeType.<X> => "<iconKey>"` | Icon hiển thị trong Execution Trace panel (log chạy) |

**Ví dụ node `Storage`** (dùng ColorKey `VioletHaze`, iconKey `arrow-progress sharp-regular`):

```xml
<!-- (1) Views/WorkflowEditorWindow.xaml -->
<Border Style="{StaticResource PaletteIconNodeStyle}"
        Background="{DynamicResource VioletHazeBrush}"
        Tag="Storage">
  <controls:SvgViewboxEx
      Source="{Binding Source={x:Static sys:String.Empty},
               Converter={StaticResource IconKeyToPathConverter},
               ConverterParameter='arrow-progress sharp-regular'}"
      Fill="{DynamicResource TextOnVioletHazeBrush}"/>
</Border>
```

```csharp
// (2) Views/WorkflowEditors/WorkflowEditorWindow.TemplateNodeHandler.cs
"Storage" => "arrow-progress sharp-regular",

// (3) Workflow/TemplateFactory.cs
var node = new StorageNode { ColorKey = "VioletHaze", Type = NodeType.Storage };

// (4) ViewModels/WorkflowEditorViewModel.cs → ResolveNodeIconKey(...)
NodeType.Storage => "arrow-progress sharp-regular",
```

### 11.2 Cách ColorKey được dùng (quan trọng)

- **Nền node trên canvas / palette**: `DynamicResource <ColorKey>Brush` — brush phải tồn tại trong
  `Themes/Base/Colors/Common.xaml` (mọi ColorKey trong hệ thống đều đã có cặp brush tương ứng).
- **Màu chữ / icon trên nền đó**: `DynamicResource TextOn<ColorKey>Brush` — được đặt sẵn (White
  cho nền tối, `#1F2937`/`#333` cho nền sáng). **Không hardcode `Black`/`White`**.
- **Trace Log card** (ViewModels/`ExecutionTraceTreeNodeViewModel.cs`): card tự resolve icon brush
  theo quy ước `TextOn{ColorKey}Brush` trong hàm `ResolveIconBrush`. Vì vậy chỉ cần node có
  `ColorKey` đúng và brush `TextOn<ColorKey>Brush` tồn tại là icon sẽ tự tương phản; không phải sửa
  thêm code.

### 11.3 Khi thêm ColorKey mới

Nếu node dùng ColorKey **chưa tồn tại** (ví dụ `NeonLime`), phải thêm 2 brush trong
`Themes/Base/Colors/Common.xaml`:

```xml
<SolidColorBrush x:Key="NeonLimeBrush" Color="#C6FF00"/>
<SolidColorBrush x:Key="TextOnNeonLimeBrush" Color="#1F2937"/>  <!-- tối vì nền quá sáng -->
```

Nếu không có `TextOn<ColorKey>Brush`, trace card tự fallback về `TextOnPrimaryBrush` (thường là
trắng) — đủ dùng nhưng **không khuyến khích**, nhất là với nền ColorKey sáng (sẽ bị mờ chữ).

### 11.4 Execution Trace panel — các điểm phụ thuộc cấu hình

Ngoài IconKey/ColorKey, panel "Hiện log chạy" còn dùng:

- `WorkflowExecutionContext.CurrentExecutionId` (AsyncLocal) — executor phải set khi dispatch để
  log không nhảy sai khi có AsyncTask song song (đã có sẵn trong `WorkflowExecutionService.ExecuteNodeAsync`).
- `WorkflowEditorViewModel.ResolveTraceNodeBrush(node)` — fallback về `AccentBrush` nếu ColorKey
  không resolve được brush.
- `ExecutionTracePreferencesStore` (`Services/Utilities/`) — lưu `EnableExecutionTraceLog`,
  `IsExecutionTracePanelExpanded`, cấu hình export... tại `%LocalAppData%\FlowMy\execution-trace-preferences.json`.

### 11.5 Checklist nhanh khi tạo node mới

```yaml
☐ Thêm NodeType enum value (Models/Nodes/NodeType.cs)
☐ Palette: Border + Tag + ColorKey background + iconKey + TextOn...Brush fill  (WorkflowEditorWindow.xaml)
☐ Canvas icon: GetIconNameForNodeType → thêm case                              (WorkflowEditorWindow.TemplateNodeHandler.cs)
☐ Template: Create<X>() set ColorKey                                            (TemplateFactory.cs)
☐ Execution Trace iconKey: ResolveNodeIconKey → thêm case                       (WorkflowEditorViewModel.cs)
☐ Nếu ColorKey mới: thêm <X>Brush + TextOn<X>Brush                              (Themes/Base/Colors/Common.xaml)
```

---

*Tài liệu tổng hợp từ AI_NODE_FLOW_GUIDE.md, NODE_DIALOG_GUIDE.md, Node_Dialog_V2.md và source code thực tế — cập nhật 2026-04-19*
