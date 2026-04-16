# Node Dialog Implementation Guide - For AI Code Generation

## 📋 MỤC LỤC

1. [Tổng quan & Kiến trúc](#1-tổng-quan--kiến-trúc)
2. [Quy trình triển khai chuẩn](#2-quy-trình-triển-khai-chuẩn)
3. [Chi tiết từng bước](#3-chi-tiết-từng-bước)
4. [Tính năng nâng cao (Optional)](#4-tính-năng-nâng-cao-optional)
5. [Xử lý lỗi thường gặp](#5-xử-lý-lỗi-thường-gặp)
6. [Checklist cuối cùng](#6-checklist-cuối-cùng)

---

## 1. TỔNG QUAN & KIẾN TRÚC

### 1.1. Mục đích
Tài liệu này hướng dẫn tạo node dialog cho hệ thống workflow editor theo mô hình MVVM, đảm bảo:
- ✅ Tính nhất quán giữa các node
- ✅ Dễ bảo trì và mở rộng
- ✅ Tối ưu hiệu năng
- ✅ Tránh lỗi phổ biến

### 1.2. Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────────┐
│                    WORKFLOW EDITOR                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐ │
│  │   Node       │◄───│  Dialog      │◄───│  ViewModel   │ │
│  │   Control    │    │  (XAML)      │    │              │ │
│  └──────────────┘    └──────────────┘    └──────────────┘ │
│         │                    │                    │        │
│         │                    │                    │        │
│         ▼                    ▼                    ▼        │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              Node Model (Data)                       │  │
│  └──────────────────────────────────────────────────────┘  │
│         │                                        │          │
│         ▼                                        ▼          │
│  ┌──────────────┐                        ┌──────────────┐  │
│  │   Renderer   │                        │ Persistence  │  │
│  │   Service    │                        │   Service    │  │
│  └──────────────┘                        └──────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 1.3. Thành phần chính

| Thành phần | File | Chức năng |
|-----------|------|-----------|
| **Model** | `Models/Nodes/YourNode.cs` | Lưu trữ dữ liệu, properties |
| **View** | `Views/Overlays/YourNodeDialog.xaml` | Giao diện dialog |
| **View Code-behind** | `Views/Overlays/YourNodeDialog.xaml.cs` | Logic UI, event handling |
| **ViewModel** | `ViewModels/YourNodeDialogViewModel.cs` | Business logic, binding |
| **NodeControl** | `Views/NodeControls/YourNodeControl.cs` | Render node trên canvas |
| **Renderer** | `Services/Rendering/YourNodeRenderer.cs` | Quản lý vẽ và cập nhật node |
| **Persistence** | `Services/Workflow/FileWorkflowPersistenceService.cs` | Save/Load workflow |
| **Executor** | `Services/Workflow/NodeExecutors/YourNodeExecutor.cs` | Thực thi logic node |

---

## 2. QUY TRÌNH TRIỂN KHAI CHUẨN

### 2.1. Flowchart tổng quan

```
START
  │
  ├─► Bước 1: Trao đổi với user về thiết kế
  │   ├─ Hỏi màu sắc (ColorKey) cho node
  │   ├─ Hỏi icon hiển thị
  │   ├─ Xác nhận chức năng và properties
  │   └─ Xác nhận cấu trúc input/output
  │
  ├─► Bước 2: Tạo Node Model
  │   ├─ Tạo file Models/Nodes/YourNode.cs
  │   ├─ Định nghĩa properties
  │   ├─ Implement INotifyPropertyChanged (nếu cần)
  │   └─ Thêm NotifyTitleChanged() method
  │
  ├─► Bước 3: Tạo Dialog (View)
  │   ├─ Tạo XAML: Views/Overlays/YourNodeDialog.xaml
  │   ├─ Kế thừa BaseNodeDialog
  │   ├─ Thiết kế 2 tabs: Logic + Cấu hình
  │   └─ Thêm controls cho properties
  │
  ├─► Bước 4: Tạo Dialog Code-behind
  │   ├─ Tạo file .xaml.cs
  │   ├─ Kế thừa BaseNodeDialog
  │   ├─ Implement GetInputsPanel/GetOutputsPanel
  │   └─ Implement CloseButton_Click
  │
  ├─► Bước 5: Tạo ViewModel
  │   ├─ Kế thừa BaseNodeDialogViewModel
  │   ├─ Thêm ObservableProperty cho properties
  │   ├─ Implement LoadInputs/LoadOutputs
  │   ├─ Implement OnSaveTitle
  │   └─ Thêm RefreshAvailableSourcesForInputs
  │
  ├─► Bước 6: Tạo NodeControl
  │   ├─ Implement CreateBorder method
  │   ├─ Thêm icon và style
  │   ├─ Implement OpenNodeDialog
  │   └─ [Optional] Thêm TitleTextBlock logic
  │
  ├─► Bước 7: Tạo Renderer
  │   ├─ Implement RenderNode
  │   ├─ Implement UpdateNodePosition
  │   ├─ Implement RemoveNode
  │   └─ Đăng ký trong WorkflowEditorWindow
  │
  ├─► Bước 8: Implement Persistence
  │   ├─ Thêm serialize logic (GetNodeProperties)
  │   └─ Thêm deserialize logic (RestoreNodeProperties)
  │
  ├─► Bước 9: Implement Copy/Paste
  │   ├─ Thêm Ctrl+C/V trong EventService
  │   └─ Thêm copy logic trong CreateDuplicateNodeInstance
  │
  ├─► Bước 10: [Optional] Implement Executor
  │   ├─ Tạo YourNodeExecutor.cs
  │   ├─ Implement ExecuteAsync
  │   └─ Đăng ký trong WorkflowExecutionService
  │
  └─► Bước 11: Testing & Validation
      ├─ Test create/edit node
      ├─ Test save/load workflow
      ├─ Test copy/paste
      ├─ Test execution (nếu có)
      └─ Test UI responsiveness
        │
        END
```

### 2.2. Thứ tự ưu tiên

#### **Phase 1: Core (Bắt buộc)**
1. Trao đổi thiết kế với user
2. Tạo Node Model
3. Tạo Dialog (XAML + Code-behind)
4. Tạo ViewModel
5. Tạo NodeControl
6. Tạo Renderer
7. Implement Persistence
8. Implement Copy/Paste

#### **Phase 2: Advanced (Tùy chọn)**
9. TitleDisplayMode support
10. TitleColorMode support
11. ReuseRoutes + LineStyle
12. PortPosition configuration
13. Input/Output động
14. Executor implementation

---

## 3. CHI TIẾT TỪNG BƯỚC

### BƯỚC 1: Trao đổi thiết kế với user

#### 1.1. Xác nhận thiết kế UI + Node Palette (WorkflowEditorWindow)

**⚠️ CRITICAL: MỖI NODE MỚI PHẢI ĐƯỢC THIẾT KẾ TRƯỚC Ở MENU BÊN TRÁI (`WorkflowEditorWindow.xaml`).**  
AI agent phải luôn hỏi/ghi nhận hoặc tự suy luận các thông tin sau:

```markdown
Để tạo node [TÊN NODE], tôi cần xác nhận:

**1. Thiết kế trên Palette (Node Templates):**
- ColorKey (màu node): ForestPine / SkyAzure / AmberWarm / ... (xem `Themes/Base/Colors/Common.xaml`)
- Icon hiển thị trong menu: 
  - Nếu user chỉ định: dùng đúng icon đó (ví dụ: "timer regular")
  - Nếu user không chỉ định: AI chọn icon **hợp ngữ nghĩa** với chức năng (ví dụ: HTTP → "globe-pointer", Folder → "folder-open", Delay → "timer regular")
- Mô tả tooltip ngắn:
  - Tiêu đề: "Tên node"
  - Mô tả: 1–2 câu về chức năng

**2. Chức năng & cấu trúc:**
- Node này làm gì? (mô tả ngắn gọn)
- Inputs: danh sách inputs (từ node khác / giá trị cố định)
- Outputs: danh sách outputs (kiểu dữ liệu, có dynamic không)

**3. Properties & cấu hình:**
- Các properties cần cấu hình (URL, code, path, delay, ...).
- Có cần input/output động không? (user tự thêm/xóa).

**4. Tiêu đề & màu tiêu đề (Title):**
- Có cần hiển thị title trên node không? (TitleDisplayMode: Always / Hover / Hidden).
- Có cần đổi **màu title riêng** hay dùng màu node? (TitleColorMode + TitleColorKey).
- Có cần chọn vị trí port IN/OUT trong dialog không?
```

#### 1.2. Ví dụ minh họa (một node bất kỳ dùng ForestPine + timer regular)

Ví dụ dưới đây chỉ là **mẫu tham khảo** cho một node trong palette sử dụng ColorKey `ForestPine` và icon `timer regular`.  
Khi implement node mới (kể cả không phải Delay), hãy **áp dụng cùng cấu trúc** nhưng thay `Tag`, `ColorKey`, `Brush` và `ConverterParameter` cho phù hợp với node đó.

```xml
<!-- Trong NodeTemplatesPanel của WorkflowEditorWindow.xaml -->
<Border Style="{StaticResource PaletteIconNodeStyle}"
        Background="{DynamicResource ForestPineBrush}"
        Tag="Delay">
    <Border.ToolTip>
        <ToolTip>
            <StackPanel MaxWidth="220">
                <TextBlock Text="Delay" FontWeight="Bold"/>
                <TextBlock Text="Chờ một khoảng thời gian (ms) rồi đi tiếp." 
                           TextWrapping="Wrap" Margin="0,2,0,0"/>
            </StackPanel>
        </ToolTip>
    </Border.ToolTip>
    <Border.ContextMenu>
        <ContextMenu Placement="MousePoint" StaysOpen="False">
            <MenuItem IsHitTestVisible="False">
                <MenuItem.Header>
                    <Border Background="{DynamicResource ForestPineBrush}" 
                            CornerRadius="10" Padding="10" 
                            BorderBrush="{DynamicResource BorderColor}" BorderThickness="1">
                        <StackPanel>
                            <TextBlock Text="Delay" 
                                       Foreground="{DynamicResource TextOnForestPineBrush}" 
                                       FontWeight="Bold" FontSize="13"/>
                            <TextBlock Text="Tạm dừng workflow một khoảng thời gian." 
                                       Foreground="{DynamicResource TextOnForestPineBrush}" 
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
                                               ConverterParameter='timer regular'}"
                              Fill="{DynamicResource TextOnForestPineBrush}"/>
    </Grid>
</Border>
```

Khi thiết kế node mới, AI **phải**:
- Chọn/ghi nhận **ColorKey** và icon, sau đó:
  - Tạo `Border` trong `WorkflowEditorWindow.xaml` với `Background="{DynamicResource [ColorKey]Brush}"`.
  - Dùng `TextOn[ColorKey]Brush` cho icon/title palette.
  - Thêm `Tag="YourNodeTypeName"` để `TemplateFactory.Create(...)` có thể map đúng.
- Xem ví dụ này như **mẫu chuẩn giao diện palette**, không phải mô tả duy nhất cho `DelayNode`. Nội dung thực tế của từng node (Delay, Code, ListOut, ...) có thể khác, nhưng pattern palette phải thống nhất.

#### 1.3. Ghi nhận thông tin chức năng

```yaml
Node Type: YourNode
ColorKey: ForestPine
Icon: timer regular
Title Default: "Your Node"

Inputs:
  - input1: String (từ node khác)
  - input2: Number (giá trị cố định hoặc từ node)

Outputs:
  - output1: String
  - output2: ArrayString (nếu có)

Properties:
  - SomeProperty: String
  - SomeNumber: Integer
  
Features:
  - TitleDisplayMode: Yes/No
  - TitleColorMode: Yes/No
  - Dynamic Inputs: Yes/No
  - Dynamic Outputs: Yes/No
  - Executor Required: Yes/No
```

---

### BƯỚC 2: Tạo Node Model

#### 2.1. Template cơ bản (không có INotifyPropertyChanged)

```csharp
using FlowMy.Models;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// [MÔ TẢ CHỨC NĂNG NODE]
    /// </summary>
    public sealed class YourNode : WorkflowNode
    {
        // ===== PROPERTIES =====
        
        /// <summary>
        /// [Mô tả property]
        /// </summary>
        public string SomeProperty { get; set; } = string.Empty;
        
        public int SomeNumber { get; set; }
        
        // ===== CONSTRUCTOR =====
        
        public YourNode()
        {
            Type = NodeType.YourType;
            Title = "Your Node";
            
            // Khởi tạo properties
            SomeProperty = string.Empty;
            SomeNumber = 0;
        }
    }
}
```

#### 2.2. Template với INotifyPropertyChanged

```csharp
using FlowMy.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    public sealed class YourNode : WorkflowNode, INotifyPropertyChanged
    {
        // ===== PROPERTIES =====
        
        private string _someProperty = string.Empty;
        public string SomeProperty
        {
            get => _someProperty;
            set
            {
                if (_someProperty != value)
                {
                    _someProperty = value;
                    OnPropertyChanged();
                }
            }
        }
        
        // ===== CONSTRUCTOR =====
        
        public YourNode()
        {
            Type = NodeType.YourType;
            Title = "Your Node";
        }
        
        // ===== INOTIFYPROPERTYCHANGED =====
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        /// <summary>
        /// Notify Title changed (dùng khi Title bị thay đổi từ bên ngoài)
        /// </summary>
        public void NotifyTitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}
```

#### 2.3. Quy tắc quan trọng (bao gồm Title & TitleColor)

| Quy tắc | ✅ Đúng | ❌ Sai |
|---------|---------|--------|
| **Không override Title** | Dùng `NotifyTitleChanged()` | `public new string Title { get; set; }` |
| **Sealed class** | `public sealed class YourNode` | `public class YourNode` |
| **Property naming** | `SomeProperty` (PascalCase) | `someProperty` (camelCase) |
| **Default values** | `= string.Empty` | Không khởi tạo |
| **TitleDisplayMode (nếu dùng)** | Có property `TitleDisplayMode` + `TitleTextBlockUI` + `NotifyTitleChanged()` | Chỉ thêm ComboBox trong dialog, không có model/TitleTextBlock |
| **TitleColorMode (nếu dùng)** | Có property `TitleColorMode` + `TitleColorKey` trong model, và NodeControl dùng `GetTitleBrush(node)` | Chỉ lưu màu trong dialog, NodeControl vẫn dùng `NodeBrush` trực tiếp |

---

#### 2.4. Đăng ký node trong `TemplateFactory` (kết nối Palette → Model)

Mỗi node trong palette (Border với `Tag="YourNodeTypeName"`) phải được map về model trong `Workflow/TemplateFactory.cs`:

```csharp
public WorkflowNode Create(string nodeType, double x, double y)
{
    return nodeType switch
    {
        "Start"   => CreateStartNode(x, y),
        "End"     => CreateEndNode(x, y),
        "Input"   => CreateInputNode(x, y),
        "Output"  => CreateOutputNode(x, y),
        "ListOut" => CreateListOutNode(x, y),
        "Delay"   => CreateDelayNode(x, y),
        // ... thêm node mới tại đây ...
        _ => throw new NotSupportedException($"Unknown node type '{nodeType}'.")
    };
}
```

Ví dụ tạo node mới kiểu `ListOut` với ColorKey + ports chuẩn:

```csharp
public static WorkflowNode CreateListOutNode(double x, double y)
{
    var node = new ListOutNode
    {
        Id = Guid.NewGuid().ToString(),
        X = x,
        Y = y,
        // ✅ Theme color - dùng ColorKey + Brush trùng với palette
        ColorKey = "Fluidity",
        NodeBrush = Application.Current.TryFindResource("FluidityBrush") as Brush ?? Brushes.Teal
    };

    // Input port (trái) - nhận flow
    node.Ports.Add(new NodePort
    {
        Id = Guid.NewGuid().ToString(),
        IsInput = true,
        Position = PortPosition.Left,
        IsVisible = true,
        ColorKey = "Info"          // ⚠️ Port IN: màu mặc định Info
    });

    // Output port (phải) - gửi flow tiếp
    node.Ports.Add(new NodePort
    {
        Id = Guid.NewGuid().ToString(),
        IsInput = false,
        Position = PortPosition.Right,
        IsVisible = true,
        ColorKey = "SunsetOrange"  // ⚠️ Port OUT: màu mặc định SunsetOrange
    });

    return node;
}
```

**QUY TẮC COLOR PORT MẶC ĐỊNH (nếu không có yêu cầu riêng):**
- **Cách renderer tính màu port:**
  - Nếu `port.ColorKey` **có giá trị** → renderer sẽ thử lấy màu từ `"{port.ColorKey}Brush"` trong theme  
    (`Application.Current.TryFindResource($"{port.ColorKey}Brush")`), nếu không có sẽ thử chính `port.ColorKey` như một resource màu.
  - Nếu `port.ColorKey` **trống** → renderer sẽ tự fallback theo hướng IN/OUT:
    - Port IN (`IsInput = true`): dùng `InfoBrush` (mặc định màu Info). Nếu resource không tồn tại thì fallback `Colors.Orange`.
    - Port OUT (`IsInput = false`): dùng `SunsetOrangeBrush` (mặc định SunsetOrange). Nếu resource không tồn tại thì fallback `Colors.Cyan`.
- **Khi tạo node mới, để màu luôn đúng và không phụ thuộc fallback**, nên set rõ:
  - Port IN: `ColorKey = "Info"`
  - Port OUT: `ColorKey = "SunsetOrange"`
  Như vậy renderer sẽ lấy đúng màu từ theme và việc hiển thị port in/out luôn thống nhất giữa các node.

Như vậy, trình tự chuẩn:
1. Tạo **Border** trong `WorkflowEditorWindow.xaml` (palette) với `Tag="YourNodeTypeName"` + ColorKey + icon.
2. Thêm node mới vào `TemplateFactory.Create(...)` map theo `Tag`.
3. Implement `CreateYourNode(x, y)` để:
   - Set `ColorKey` + `NodeBrush` đúng theo palette.
   - Khởi tạo **ports** với vị trí, `IsInput`, `IsVisible` và **màu port** chuẩn (`Info` / `SunsetOrange`).
4. Sau đó mới tiếp tục Bước 3–7 (Dialog, NodeControl, Renderer, ...).

### BƯỚC 3: Tạo Dialog (XAML)

#### 3.1. Cấu trúc file

```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:FlowMy.Views.Overlays"
        Title="Your Node Dialog"
        WindowStyle="None"
        ResizeMode="CanResize"
        AllowsTransparency="True"
        Background="Transparent"
        ShowInTaskbar="False"
        Topmost="True"
        Width="400"
        Height="600"
        MinWidth="350"
        MinHeight="400">
    
    <Border CornerRadius="12" Padding="0" 
            Background="#FF1E293B" 
            BorderBrush="#33FFFFFF" 
            BorderThickness="1">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- HEADER -->
            <Border Grid.Row="0" Background="#FF0F172A" 
                    CornerRadius="12,12,0,0" Padding="16,12,12,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <!-- Title TextBox -->
                    <TextBox x:Name="TitleTextBox"
                          Grid.Column="0"
                          Text="{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}"
                          Style="{DynamicResource BaseTextBoxV2}"
                          FontSize="16"
                          Padding="0,4,0,4"
                          VerticalContentAlignment="Center"
                          Cursor="IBeam"/>

                    <!-- Action Buttons -->
                    <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                        <!-- Play Button -->
                        <Button x:Name="PlayButton" 
                                Width="24" Height="24" 
                                Content="▶" FontSize="12"
                                Style="{DynamicResource PrimaryButton}"
                                Cursor="Hand"
                                Margin="8,0,0,0" 
                                ToolTip="Chạy logic node này"
                                Command="{Binding RunSingleNodeCommand}"/>

                        <!-- Close Button -->
                        <Button x:Name="CloseButton" 
                                Width="24" Height="24"
                                Style="{DynamicResource DangerButton}"
                                Content="×"
                                FontSize="12"
                                FontWeight="Bold"
                                Cursor="Hand"
                                Margin="8,0,0,0" 
                                Click="CloseButton_Click"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- CONTENT: TAB CONTROL -->
            <TabControl Grid.Row="1" 
                       Background="Transparent"
                       BorderThickness="0"
                       Margin="0,8,0,0">
                
                <!-- TAB 1: LOGIC -->
                <TabItem Header="Logic" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>
                            
                            <!-- TitleDisplayMode (Optional) -->
                            <TextBlock Text="Hiển thị tiêu đề:"
                                       Foreground="White"
                                       FontSize="14"
                                       FontWeight="SemiBold"
                                       Margin="0,0,0,8"/>
                            
                            <ComboBox x:Name="TitleDisplayModeComboBox"
                                      Height="36"
                                      Style="{DynamicResource BaseComboBox}"
                                      Margin="0,0,0,16"
                                      ItemsSource="{Binding TitleDisplayModeOptions}"
                                      SelectedValuePath="Value"
                                      DisplayMemberPath="DisplayName"
                                      SelectedValue="{Binding TitleDisplayMode, Mode=TwoWay}"/>
                            
                            <!-- Custom Properties -->
                            <TextBlock Text="Your Property:"
                                       Foreground="White"
                                       FontSize="14"
                                       FontWeight="SemiBold"
                                       Margin="0,0,0,8"/>
                            
                            <TextBox Text="{Binding YourProperty, Mode=TwoWay}"
                                     Style="{DynamicResource BaseTextBoxV2}"
                                     Height="36"
                                     Margin="0,0,0,16"/>
                            
                            <!-- Inputs Section -->
                            <TextBlock Text="Inputs:"
                                       Foreground="White"
                                       FontSize="14"
                                       FontWeight="SemiBold"
                                       Margin="0,0,0,8"/>
                            
                            <Border Background="#FF1E293B"
                                    BorderBrush="#33FFFFFF"
                                    BorderThickness="1"
                                    CornerRadius="8"
                                    Padding="12"
                                    Margin="0,0,0,16">
                                <StackPanel x:Name="InputsPanel"/>
                            </Border>
                            
                            <!-- Outputs Section -->
                            <TextBlock Text="Outputs:"
                                       Foreground="White"
                                       FontSize="14"
                                       FontWeight="SemiBold"
                                       Margin="0,0,0,8"
                                       x:Name="TextBlockOutputPanel"/>
                            
                            <Border Background="#FF1E293B"
                                    BorderBrush="#33FFFFFF"
                                    BorderThickness="1"
                                    CornerRadius="8"
                                    Padding="12"
                                    Margin="0,0,0,16"
                                    x:Name="BorderOutputPanel">
                                <StackPanel x:Name="OutputsPanel"/>
                            </Border>
                            
                        </StackPanel>
                    </ScrollViewer>
                </TabItem>
                
                <!-- TAB 2: CẤU HÌNH -->
                <TabItem Header="Cấu hình" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>
                            
                            <!-- Vị trí cổng IN/OUT -->
                            <TextBlock Text="Vị trí cổng IN/OUT:"
                                       Foreground="White"
                                       FontSize="14"
                                       FontWeight="SemiBold"
                                       Margin="0,0,0,8"/>
                            
                            <Grid Margin="0,0,0,12">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                
                                <!-- Port IN -->
                                <StackPanel Grid.Column="0" Margin="0,0,8,0">
                                    <TextBlock Text="Port IN"
                                               Foreground="White"
                                               FontSize="12"
                                               Opacity="0.8"
                                               Margin="0,0,0,4"/>
                                    <ComboBox Height="32"
                                              Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding InputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                                
                                <!-- Port OUT -->
                                <StackPanel Grid.Column="1" Margin="8,0,0,0">
                                    <TextBlock Text="Port OUT"
                                               Foreground="White"
                                               FontSize="12"
                                               Opacity="0.8"
                                               Margin="0,0,0,4"/>
                                    <ComboBox Height="32"
                                              Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding OutputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                            </Grid>
                            
                            <!-- Tái sử dụng flow (ReuseRoutes) -->
                            <!-- [Thêm ItemsControl cho ReuseRoutes nếu cần] -->
                            
                        </StackPanel>
                    </ScrollViewer>
                </TabItem>
                
            </TabControl>
            
        </Grid>
    </Border>
</local:BaseNodeDialog>
```

#### 3.2. Quy tắc thiết kế

| Phần | Quy tắc |
|------|---------|
| **Header** | Luôn có: Title TextBox + Play Button + Close Button. Với các node cần chạy **workflow từ chính node đó** (ví dụ ImageProcessing), có thể thêm **PlayFlowButton** (▶▶) cạnh Play để chạy flow từ node hiện tại. |
| **TabControl** | 2 tabs: "Logic" (chức năng chính) + "Cấu hình" (settings) |
| **Tab Logic** | TitleDisplayMode → Custom Properties → Inputs → Outputs. Với các output nặng (ví dụ imageBase64, crop list...), nên thêm **checkbox SkipOutput** để cho phép user tắt logic xử lý cho từng key. |
| **Tab Cấu hình** | PortPosition → ReuseRoutes → Advanced settings |
| **Spacing** | Margin="0,0,0,16" giữa các sections |
| **Colors** | Background="#FF1E293B", Border="#33FFFFFF" |

##### 3.2.1. Nút chạy workflow từ node hiện tại (PlayFlowButton – ví dụ ImageProcessingNode)

Đối với các node cần hỗ trợ **chạy cả workflow bắt đầu từ chính node đó** (không chạy lại các node phía trước), dialog header nên bổ sung thêm một nút **PlayFlowButton** ngay cạnh nút Play chuẩn:

```xml
<StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
    <!-- Nút Play chuẩn: chỉ chạy logic riêng node (RunSingleNodeCommand) -->
    <Button x:Name="PlayButton" Width="24" Height="24" Content="▶" FontSize="12"
            Style="{DynamicResource PrimaryButton}"
            Cursor="Hand"
            Margin="8,0,0,0" ToolTip="Chạy logic node này"
            Command="{Binding RunSingleNodeCommand}"/>

    <!-- Nút PlayFlow: chạy workflow từ node hiện tại theo đúng luồng connections -->
    <Button x:Name="PlayFlowButton" Width="24" Height="24" Content="▶▶" FontSize="12"
            Style="{DynamicResource PrimaryButton}"
            Cursor="Hand"
            Margin="8,0,0,0" ToolTip="Chạy workflow từ node này"
            Command="{Binding RunWorkflowFromNodeCommand}"/>

    <Button x:Name="CloseButton" Width="24" Height="24"
            Style="{DynamicResource DangerButton}"
            Content="×"
            FontSize="12"
            FontWeight="Bold"
            Cursor="Hand"
            Margin="8,0,0,0" Click="CloseButton_Click"/>
</StackPanel>
```

**Yêu cầu ViewModel và BaseNodeDialogViewModel:**

- `BaseNodeDialogViewModel` phải cung cấp command:

```csharp
[RelayCommand]
protected async Task RunWorkflowFromNode()
{
    var vm = _host.ViewModel;
    if (vm == null) return;
    await vm.RunWorkflowFromNodeAsync(_node);
}
```

- `WorkflowEditorViewModel` cần có API:

```csharp
public async Task RunWorkflowFromNodeAsync(WorkflowNode startNode)
{
    // Giống StartTest nhưng gọi ExecuteNodeAsync(startNode, ...) thay vì duyệt tất cả Start nodes.
}
```

Nút **PlayFlowButton** cho phép user test nhanh luồng từ một node giữa workflow mà vẫn **tái sử dụng toàn bộ cơ chế thực thi, visualize, cancel** giống như nút Bắt đầu trên toolbar.

##### 3.2.2. Checkbox SkipOutput cho từng output key (tối ưu tài nguyên)

Đối với các node có **output nặng hoặc tốn tài nguyên để tính toán** (ví dụ `imageBase64`, `cropListBase64` trong ImageProcessingNode), dialog nên cung cấp checkbox bên cạnh mỗi output key để user có thể **tắt hoàn toàn logic xử lý** cho key đó.

**Mẫu UI cho từng output item:**

```csharp
protected override FrameworkElement CreateOutputItemUI(OutputItemViewModel outputVm)
{
    var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

    var grid = new Grid();
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

    // Checkbox: Checked = SKIP xử lý output này
    var checkbox = new CheckBox
    {
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0),
        ToolTip = "Checked = không xử lý output này"
    };

    var imageNode = _viewModel.Node as ImageProcessingNode; // hoặc YourNode tương ứng
    if (imageNode != null)
    {
        // imageNode.SkipOutputs: HashSet<string> lưu danh sách key bị bỏ qua
        checkbox.IsChecked = imageNode.SkipOutputs.Contains(outputVm.Key);
        checkbox.Checked += (s, e) =>
        {
            if (!imageNode.SkipOutputs.Contains(outputVm.Key))
                imageNode.SkipOutputs.Add(outputVm.Key);
        };
        checkbox.Unchecked += (s, e) =>
        {
            imageNode.SkipOutputs.Remove(outputVm.Key);
        };
    }

    Grid.SetColumn(checkbox, 0);
    grid.Children.Add(checkbox);

    var keyLabel = new TextBlock
    {
        Text = $"Key: {outputVm.Key}",
        Foreground = Brushes.White,
        FontSize = 12,
        Opacity = 0.9,
        Margin = new Thickness(0, 0, 0, 4),
        VerticalAlignment = VerticalAlignment.Center
    };
    Grid.SetColumn(keyLabel, 1);
    grid.Children.Add(keyLabel);

    stack.Children.Add(grid);

    var valueText = new TextBlock
    {
        Foreground = Brushes.White,
        FontSize = 11,
        Opacity = 0.9,
        Margin = new Thickness(0, 4, 0, 0)
    };

    valueText.SetBinding(TextBlock.TextProperty,
        new Binding(nameof(OutputItemViewModel.Value)) { Source = outputVm });

    stack.Children.Add(valueText);
    return stack;
}
```

**Yêu cầu phía Model & Executor:**

- Model node cần có cấu trúc tương tự:

```csharp
public HashSet<string> SkipOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);
```

- Executor khi set output phải **kiểm tra `SkipOutputs` trước khi tính toán/ghi giá trị**, ví dụ:

```csharp
private static void SetOutput(ImageProcessingNode node, string key, string value)
{
    if (node.SkipOutputs != null && node.SkipOutputs.Contains(key))
        return; // Không xử lý / không set value cho key này

    var port = node.DynamicOutputs?.FirstOrDefault(o =>
        string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
    if (port != null)
    {
        port.UserValueOverride = value ?? string.Empty;
    }
}
```

**Nguyên tắc:**

- Checkbox **Checked = bỏ qua**: không chạy logic tốn tài nguyên (FFmpeg, decode/encode ảnh, gọi HTTP, v.v.) cho output đó.
- Checkbox **Unchecked = xử lý bình thường**: executor chạy logic, set giá trị cho key tương ứng.
- Điều này giúp workflow **tiết kiệm tài nguyên** khi user chỉ cần một số outputs nhất định từ node.

---

### BƯỚC 4: Tạo Dialog Code-behind

#### 4.1. Template

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

            // Khởi tạo ViewModel
            _viewModel = new YourNodeDialogViewModel(node, host);

            // Kết nối với BaseNodeDialog
            InitializeBase(_viewModel, owner);
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void ViewModel_PropertyChanged(
            object? sender, 
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
            
            // Xử lý thêm nếu cần
            // if (e.PropertyName == nameof(YourNodeDialogViewModel.SomeProperty))
            // {
            //     // Custom handling
            // }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Lưu trước khi đóng
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }
    }
}
```

#### 4.2. Quy tắc quan trọng

| Quy tắc | Giải thích |
|---------|------------|
| **Kế thừa BaseNodeDialog** | Tái sử dụng logic chung |
| **InitializeComponent() trước** | Gọi trước khi tạo ViewModel |
| **InitializeBase() sau** | Gọi sau khi tạo ViewModel |
| **GetInputsPanel/GetOutputsPanel** | Override để BaseNodeDialog biết panel nào |
| **SaveTitleCommand trước Close** | Đảm bảo lưu dữ liệu |

---

### BƯỚC 5: Tạo ViewModel

#### 5.1. Template cơ bản

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
        
        [ObservableProperty]
        private string _someProperty = string.Empty;
        
        [ObservableProperty]
        private int _someNumber;

        // ===== OPTIONS (cho ComboBox) =====
        
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

            // Sync properties từ node sang ViewModel
            SomeProperty = _yourNode.SomeProperty;
            SomeNumber = _yourNode.SomeNumber;

            // Subscribe PropertyChanged nếu node implement INotifyPropertyChanged
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(YourNode.SomeProperty))
                        SomeProperty = _yourNode.SomeProperty;
                    else if (e.PropertyName == nameof(YourNode.SomeNumber))
                        SomeNumber = _yourNode.SomeNumber;

                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }
        }

        // ===== OVERRIDES =====
        
        protected override string GetDefaultTitle() => "Your Node";

        protected override void OnSaveTitle()
        {
            // Sync properties từ ViewModel về node
            if (_yourNode.SomeProperty != SomeProperty)
            {
                _yourNode.SomeProperty = SomeProperty;
                _host.RequestSyncDataPanels(immediate: true);
            }
            
            if (_yourNode.SomeNumber != SomeNumber)
            {
                _yourNode.SomeNumber = SomeNumber;
                _host.RequestSyncDataPanels(immediate: true);
            }

            // Trigger PropertyChanged
            _yourNode.NotifyTitleChanged();
        }

        // ===== REFRESH METHODS =====
        
        /// <summary>
        /// Refresh AvailableSources cho inputs với tiêu đề node mới nhất
        /// </summary>
        private void RefreshAvailableSourcesForInputs()
        {
            if (_host.ViewModel == null) return;
            if (_yourNode.DynamicInputs == null || _yourNode.DynamicInputs.Count == 0) return;

            // Tìm connections đến node này
            var connections = _host.ViewModel.Connections
                .Where(c => c.ToNode == _yourNode && c.FromNode != null)
                .ToList();

            if (connections.Count == 0)
            {
                foreach (var input in _yourNode.DynamicInputs)
                {
                    input.AvailableSources = new System.Collections.Generic.List<WorkflowDataSourceOption>();
                }
                return;
            }

            // Tìm producer nodes
            var producerNodes = connections
                .Select(c => c.FromNode)
                .Where(n => n != null && n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                .Distinct()
                .ToList();

            // Tạo options với tiêu đề mới nhất
            var options = producerNodes
                .Select(n => new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                })
                .ToList();

            // Update AvailableSources
            foreach (var input in _yourNode.DynamicInputs)
            {
                input.AvailableSources = options;
            }
        }

        protected override void LoadInputs()
        {
            Inputs.Clear();
            if (_yourNode.DynamicInputs == null || _yourNode.DynamicInputs.Count == 0) return;

            // ⚠️ CRITICAL: Refresh trước khi load
            RefreshAvailableSourcesForInputs();

            foreach (var input in _yourNode.DynamicInputs)
            {
                var inputVm = new InputItemViewModel(_yourNode, input, _host);
                
                // Cập nhật AvailableSources
                if (input.AvailableSources != null)
                {
                    inputVm.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(input.AvailableSources);
                }
                
                Inputs.Add(inputVm);
            }
        }
    }
}
```

#### 5.2. Quy tắc quan trọng

| Quy tắc | ✅ Đúng | ❌ Sai |
|---------|---------|--------|
| **Kế thừa Base** | `: BaseNodeDialogViewModel` | Không kế thừa |
| **ObservableProperty** | `[ObservableProperty]` | Manual INotifyPropertyChanged |
| **Constructor order** | Gọi `base(node, host)` trước | Không gọi base |
| **RefreshAvailableSources** | Gọi trong `LoadInputs()` | Không refresh |
| **OnSaveTitle** | Sync tất cả properties | Chỉ sync một vài properties |

---

### BƯỚC 6: Tạo NodeControl

> ⚠️ **CRITICAL**: Hầu hết các NodeControl chuẩn trong codebase (Code, Input, Output, Delay, ListOut, HttpRequest, StringSplit, AssignData, Folder, ...) đều implement **đầy đủ TitleDisplayMode + TitleColorMode**. Template dưới đây là template **chuẩn bắt buộc** cho mọi node có dialog. Chỉ các node đặc biệt (Start, End) mới dùng template đơn giản hơn.

#### 6.1. Luồng xử lý trong CreateBorder (Flowchart)

```
CreateBorder(node, ownerWindow, host)
  │
  ├─► 1. Tạo Grid container (MinWidth/MinHeight/Width/Height)
  │
  ├─► 2. Tạo Icon (SvgViewboxEx + IconKeyToPathConverter)
  │     └─ Fill = GetTextBrush(node.ColorKey)  // TextOn[ColorKey]Brush
  │
  ├─► 3. Tạo Border chính
  │     ├─ Background = node.NodeBrush
  │     ├─ Effect = DropShadowEffect
  │     └─ Tag = node  // ⚠️ CRITICAL: Dùng để truyền node cho renderer
  │
  ├─► 4. Tạo TitleTextBlock  ⚠️ BẮT BUỘC cho mọi node chuẩn
  │     ├─ Foreground = GetTitleBrush(node)  // Theo TitleColorMode
  │     ├─ Visibility = GetTitleVisibility(node.TitleDisplayMode, false)
  │     ├─ IsHitTestVisible = false
  │     └─ node.TitleTextBlockUI = titleTextBlock  // Lưu reference
  │
  ├─► 5. Đăng ký EVENT HANDLERS (7 handlers)
  │     ├─ border.MouseEnter → UpdateTitleVisibility + UpdateTitlePosition
  │     ├─ border.MouseLeave → UpdateTitleVisibility
  │     ├─ INotifyPropertyChanged → Sync Title/NodeBrush/TitleColor/TitleDisplayMode
  │     ├─ DependencyPropertyDescriptor(Visibility) → Sync title với viewport
  │     ├─ border.Loaded → Add titleTextBlock vào Canvas
  │     ├─ border.SizeChanged → UpdateTitlePosition
  │     ├─ border.Unloaded → Cleanup timers, dictionaries, remove titleTextBlock
  │     └─ border.LayoutUpdated → Zoom handling + ThrottledUpdateTitlePosition
  │
  ├─► 6. border.MouseRightButtonUp → OpenNodeDialog
  │
  └─► 7. Return border
```

#### 6.2. Template chuẩn (ĐẦY ĐỦ - dùng cho mọi node có dialog)

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
        // ===== STATIC FIELDS (Throttle title updates khi pan/zoom) =====
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50;
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();

        // ===== CREATEBORDER =====
        public static Border CreateBorder(YourNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // ───── 1. GRID CONTAINER ─────
            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

            // ───── 2. ICON (phải trùng với icon trong WorkflowEditorWindow.xaml) ─────
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(
                null, typeof(Uri),
                "your-icon-key",  // ⚠️ Thay bằng icon đã chọn (ví dụ: "timer regular", "code duotone-regular")
                System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetTextBrush(node.ColorKey)  // TextOn[ColorKey]Brush
            };
            grid.Children.Add(iconSvg);

            // ───── 3. BORDER CHÍNH ─────
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
                    Color = Colors.Black, Direction = 270, ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node  // ⚠️ CRITICAL: Renderer dùng Tag để lấy node
            };

            // ───── 4. TITLE TEXTBLOCK (BẮT BUỘC) ─────
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Your Node",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTitleBrush(node),           // Theo TitleColorMode
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false                     // Không block mouse events
            };
            node.TitleTextBlockUI = titleTextBlock;          // ⚠️ CRITICAL: Lưu reference

            // ───── 5a. HOVER EVENTS (MouseEnter/Leave) ─────
            bool isHovering = false;
            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
            };
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            };

            // ───── 5b. PROPERTYCHANGED (Sync Title/NodeBrush/TitleColor/TitleDisplayMode) ─────
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = node.Title ?? "Your Node";
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
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
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    }
                };
            }

            // ───── 5c. VISIBILITY SYNC (Viewport culling) ─────
            var visibilityDescriptor = DependencyPropertyDescriptor.FromProperty(
                UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                    titleTextBlock.Visibility = Visibility.Collapsed;
                else
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            });

            // ───── 5d. LOADED (Add titleTextBlock vào Canvas) ─────
            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);  // Hiển thị trên cùng
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            // ───── 5e. SIZECHANGED ─────
            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);

            // ───── 5f. UNLOADED (Cleanup để tránh memory leak) ─────
            border.Unloaded += (s, e) =>
            {
                try
                {
                    if (_titleUpdateTimers.TryGetValue(border, out var timer))
                    {
                        timer.Stop();
                        _titleUpdateTimers.Remove(border);
                    }
                    _titleUpdatedAfterZoom.Remove(border);
                    if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
                        host.WorkflowCanvas.Children.Remove(titleTextBlock);
                    if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                        node.TitleTextBlockUI = null;
                }
                catch { }
            };

            // ───── 5g. LAYOUTUPDATED (Zoom handling + position sync) ─────
            border.LayoutUpdated += (s, e) =>
            {
                // Sync visibility với border
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    return;
                }

                // Ẩn title khi đang zoom để tránh giật
                if (NodeChrome.IsZooming)
                {
                    if (titleTextBlock.Visibility != Visibility.Collapsed)
                        titleTextBlock.Visibility = Visibility.Collapsed;
                    _titleUpdatedAfterZoom[border] = false;
                    return;
                }

                // Sau khi zoom xong, update lại title 1 lần
                bool hasUpdated = _titleUpdatedAfterZoom.TryGetValue(border, out var u) && u;
                if (!hasUpdated && border.Visibility == Visibility.Visible)
                {
                    _titleUpdatedAfterZoom[border] = true;
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    if (titleTextBlock.Visibility == Visibility.Visible)
                        UpdateTitlePosition(titleTextBlock, border, host);
                }

                // Skip khi đang pan hoặc drag
                if (host.IsPanning || host.DraggedNode == node) return;

                // Throttle position updates
                if (titleTextBlock.Visibility == Visibility.Visible)
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
            };

            // ───── 6. RIGHT CLICK → OPEN DIALOG ─────
            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            return border;
        }

        // ===== TITLEBBRUSH (Theo TitleColorMode/TitleColorKey) =====
        private static Brush GetTitleBrush(YourNode node)
        {
            if (node.TitleColorMode != TitleColorMode.CustomColor ||
                string.IsNullOrEmpty(node.TitleColorKey) ||
                node.TitleColorKey == "NodeColor")
                return node.NodeBrush;
            if (node.TitleColorKey == "LimeGreen")
                return new SolidColorBrush(Colors.LimeGreen);
            var brush = Application.Current.TryFindResource(node.TitleColorKey) as Brush;
            return brush ?? node.NodeBrush;
        }

        // ===== TITLE VISIBILITY HELPERS =====
        private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering)
        {
            return mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };
        }

        private static void UpdateTitleVisibility(
            TextBlock titleTextBlock, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
        {
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible)
            {
                titleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }
            titleTextBlock.Visibility = GetTitleVisibility(mode, isHovering);
        }

        // ===== TITLE POSITION HELPERS (Throttled) =====
        private static void ThrottledUpdateTitlePosition(
            TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
        {
            if (!_titleUpdateTimers.TryGetValue(border, out var timer))
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
                timer.Tick += (s, e) => { timer.Stop(); UpdateTitlePosition(titleTextBlock, border, host); };
                _titleUpdateTimers[border] = timer;
            }
            timer.Stop();
            timer.Start();
        }

        private static void UpdateTitlePosition(
            TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
        {
            if (host.WorkflowCanvas == null || border == null ||
                !host.WorkflowCanvas.Children.Contains(titleTextBlock)) return;
            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);
            if (double.IsNaN(left) || double.IsNaN(top)) return;
            if (titleTextBlock.ActualWidth == 0 || titleTextBlock.ActualHeight == 0)
            {
                titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
            }
            var titleLeft = left + (border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
            var titleTop = top - titleTextBlock.ActualHeight - 4;  // 4px spacing
            Canvas.SetLeft(titleTextBlock, titleLeft);
            Canvas.SetTop(titleTextBlock, titleTop);
        }

        // ===== OPEN DIALOG =====
        private static void OpenNodeDialog(
            YourNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                // ⚠️ CRITICAL: Release mouse capture
                if (node.Border != null && node.Border.IsMouseCaptured)
                    node.Border.ReleaseMouseCapture();

                // ⚠️ CRITICAL: Clear drag state
                host.DraggedNode = null;

                // ⚠️ CRITICAL: Deselect node
                if (host.ViewModel != null)
                    host.ViewModel.SelectedNode = null;

                // Get dialog manager
                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                // Open dialog
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

        // ===== DIALOG MANAGER =====
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

        // ===== ICON TEXT BRUSH =====
        private static Brush GetTextBrush(string? colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey))
                return new SolidColorBrush(Color.FromRgb(148, 163, 184));
            var brush = Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush;
            return brush ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }
    }
}
```

#### 6.3. Danh sách methods PHẢI CÓ trong mỗi NodeControl

| Method | Mục đích | Bắt buộc |
|--------|----------|----------|
| `CreateBorder()` | Tạo UI cho node trên canvas | ✅ |
| `GetTitleBrush()` | Lấy màu title theo TitleColorMode/TitleColorKey | ✅ |
| `GetTitleVisibility()` | Tính Visibility theo TitleDisplayMode + hover | ✅ |
| `UpdateTitleVisibility()` | Cập nhật Visibility (kèm check viewport) | ✅ |
| `ThrottledUpdateTitlePosition()` | Throttle vị trí title (tránh giật khi pan/zoom) | ✅ |
| `UpdateTitlePosition()` | Tính toán vị trí title (center horizontally, trên node) | ✅ |
| `OpenNodeDialog()` | Mở dialog (release capture + clear drag + deselect) | ✅ |
| `GetOrCreateDialogManager()` | Lấy `_nodeDialogManager` từ `WorkflowEditorWindow` | ✅ |
| `GetTextBrush()` | Lấy brush icon theo ColorKey (`TextOn{ColorKey}Brush`) | ✅ |

#### 6.4. Danh sách Event Handlers PHẢI CÓ trong CreateBorder

| Event | Xử lý | Mục đích |
|-------|--------|----------|
| `border.MouseEnter` | `isHovering=true` → UpdateTitleVisibility + UpdateTitlePosition | Hiện title khi hover |
| `border.MouseLeave` | `isHovering=false` → UpdateTitleVisibility | Ẩn title khi rời chuột |
| `npc.PropertyChanged` | Sync Text/Background/Foreground khi Title/NodeBrush/TitleColorMode/TitleColorKey/TitleDisplayMode thay đổi | Realtime sync từ dialog |
| `VisibilityDescriptor` | Sync titleTextBlock.Visibility với border.Visibility | Viewport culling |
| `border.Loaded` | Add titleTextBlock vào WorkflowCanvas + SetZIndex(20000) + UpdateTitlePosition | Đảm bảo title trên canvas |
| `border.SizeChanged` | UpdateTitlePosition | Cập nhật vị trí khi resize |
| `border.Unloaded` | Stop timer, remove từ dictionaries, remove titleTextBlock từ canvas, clear node.TitleTextBlockUI | **Tránh memory leak** |
| `border.LayoutUpdated` | Ẩn title khi zoom (`NodeChrome.IsZooming`), update sau zoom, throttle khi drag/pan | Smooth zoom/pan |
| `border.MouseRightButtonUp` | `e.Handled=true` → OpenNodeDialog | Mở dialog |

#### 6.5. Quy tắc quan trọng

| Quy tắc | Giải thích |
|---------|------------|
| **Static class** | `public static class YourNodeControl` |
| **Static dictionaries** | `_titleUpdateTimers` + `_titleUpdatedAfterZoom` cho throttle |
| **TitleTextBlock BẮT BUỘC** | Mọi node chuẩn đều tạo TitleTextBlock, gán `node.TitleTextBlockUI` |
| **Tag = node** | `border.Tag = node` để renderer/chrome có thể lấy node |
| **Release mouse capture** | Trong OpenNodeDialog, tránh node nhảy |
| **Clear DraggedNode** | Trong OpenNodeDialog, tránh dialog không đóng |
| **Deselect node** | Trong OpenNodeDialog, tránh node nhảy đến vị trí chuột |
| **NodeChrome.IsZooming** | Ẩn title khi zoom, update lại sau zoom |
| **Panel.SetZIndex(title, 20000)** | Title luôn hiển thị trên tất cả elements khác |
| **border.Unloaded cleanup** | **BẮT BUỘC** - stop timer, remove dictionaries, remove title, clear reference |
| **Đồng bộ với palette** | Icon/ColorKey trong CreateBorder phải trùng với `WorkflowEditorWindow.xaml` |
| **Tham khảo node có sẵn** | Copy pattern từ `CodeNodeControl.cs`, `InputNodeControl.cs`, `OutputNodeControl.cs` |

---

### BƯỚC 7: Tạo Renderer

#### 7.1. Template

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

        // ===== RENDERNODE =====
        
        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not YourNode yourNode)
                throw new InvalidOperationException("YourNodeRenderer can only render YourNode.");

            // Tạo border
            yourNode.Border = YourNodeControl.CreateBorder(
                yourNode,
                _host as Window ?? throw new InvalidOperationException("Host must be a Window."),
                _host
            );

            // Apply NodeChrome (BEFORE hiding buttons)
            NodeChrome.Apply(yourNode.Border, yourNode, _host);

            // Hide header buttons (nếu cần)
            // yourNode.HiddenHeaderButtons.Add("duplicate");
            // yourNode.HiddenHeaderButtons.Add("editTitle");

            // Add to canvas
            canvas.Children.Add(yourNode.Border);
            Canvas.SetLeft(yourNode.Border, yourNode.X);
            Canvas.SetTop(yourNode.Border, yourNode.Y);

            // Set Z-Index
            _host.ZIndexManager.SetNodeZIndex(yourNode);

            // ⚠️ CRITICAL: Render ports
            foreach (var port in yourNode.Ports.Where(p => p.IsVisible))
            {
                // Determine port color
                Color portColor;
                if (!string.IsNullOrWhiteSpace(port.ColorKey))
                {
                    var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush") 
                                    ?? GetColorFromTheme(port.ColorKey);
                    portColor = colorFromKey ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
                }
                else
                {
                    portColor = port.IsInput
                        ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                        : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
                }
                
                // Create or update port
                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else
                {
                    // ⚠️ CRITICAL: ALWAYS update color
                    if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                    {
                        ellipse.Fill = new SolidColorBrush(portColor);
                    }
                }

                _portRenderer.UpdatePortsPositionOnSide(yourNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                _host.ZIndexManager.SetPortZIndex(yourNode, port.PortUI);
            }
        }

        // ===== UPDATENODEPOSITION =====
        
        public void UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            node.X = x;
            node.Y = y;

            if (node.Border != null)
            {
                Canvas.SetLeft(node.Border, x);
                Canvas.SetTop(node.Border, y);
            }

            // ⚠️ CRITICAL: Update port colors (same logic as RenderNode)
            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                Color portColor;
                if (!string.IsNullOrWhiteSpace(port.ColorKey))
                {
                    var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush") 
                                    ?? GetColorFromTheme(port.ColorKey);
                    portColor = colorFromKey ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
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
                else
                {
                    // ⚠️ CRITICAL: ALWAYS update color
                    if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
                    {
                        ellipse.Fill = new SolidColorBrush(portColor);
                    }
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                _host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }
        }

        // ===== REMOVENODE =====
        
        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            if (node.Border != null && canvas.Children.Contains(node.Border))
            {
                canvas.Children.Remove(node.Border);
            }

            // Remove ports
            foreach (var port in node.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Remove(port.PortUI);
                }
            }
        }

        // ===== HELPER METHODS =====
        
        private Color? GetColorFromTheme(string key)
        {
            var resource = Application.Current.TryFindResource(key);
            if (resource is SolidColorBrush brush)
                return brush.Color;
            if (resource is Color color)
                return color;
            return null;
        }
    }
}
```

#### 7.2. Đăng ký Renderer

File: `Views/WorkflowEditors/WorkflowEditorWindow.xaml.cs`

```csharp
// Trong constructor WorkflowEditorWindow
_yourNodeRenderer = new YourNodeRenderer(this, _portRenderer);

// Thêm vào GetRendererForNode
private INodeRenderer GetRendererForNode(WorkflowNode node)
{
    return node.Type switch
    {
        NodeType.YourType => _yourNodeRenderer,
        // ... other renderers
        _ => _defaultRenderer
    };
}
```

#### 7.3. Quy tắc quan trọng

| Quy tắc | Giải thích |
|---------|------------|
| **Update port colors** | Trong CẢ `RenderNode()` và `UpdateNodePosition()` |
| **ALWAYS update color** | Ngay cả khi `port.PortUI` đã tồn tại |
| **GetColorFromTheme** | Dùng helper method cho consistent color |
| **RemoveNode cleanup** | Remove cả border và ports |

---

### BƯỚC 8: Implement Persistence

#### 8.1. Serialize (GetNodeProperties)

File: `Services/Workflow/FileWorkflowPersistenceService.cs`

```csharp
private static Dictionary<string, object> GetNodeProperties(WorkflowNode node)
{
    var dict = new Dictionary<string, object>();

    // ... existing code for other node types ...

    else if (node is YourNode yourNode)
    {
        // Simple properties
        if (!string.IsNullOrWhiteSpace(yourNode.SomeProperty))
            dict["SomeProperty"] = yourNode.SomeProperty;
        
        dict["SomeNumber"] = yourNode.SomeNumber;
        
        // Enum properties
        dict["SomeEnumProperty"] = yourNode.SomeEnumProperty.ToString();
        
        // ⚠️ CRITICAL: List/Array properties
        if (yourNode.OutputMappings != null && yourNode.OutputMappings.Count > 0)
        {
            var mappingsJson = JsonSerializer.Serialize(
                yourNode.OutputMappings.Select(m => new
                {
                    NewKey = m.NewKey,
                    SourceNodeId = m.SourceNodeId,
                    SourceOutputKey = m.SourceOutputKey
                }).ToList()
            );
            dict["OutputMappings"] = mappingsJson;
        }
        
        // TitleDisplayMode (nếu node hỗ trợ)
        dict["TitleDisplayMode"] = yourNode.TitleDisplayMode.ToString();
    }

    return dict;
}
```

#### 8.2. Deserialize (RestoreNodeProperties)

```csharp
private static void RestoreNodeProperties(
    WorkflowNode node, 
    Dictionary<string, object> properties)
{
    if (properties == null) return;

    // ... existing code for other node types ...

    else if (node is YourNode yourNode)
    {
        // Simple properties
        if (properties.TryGetValue("SomeProperty", out var somePropObj))
            yourNode.SomeProperty = somePropObj?.ToString() ?? string.Empty;
        
        if (properties.TryGetValue("SomeNumber", out var numObj) &&
            int.TryParse(numObj?.ToString(), out var num))
        {
            yourNode.SomeNumber = num;
        }
        
        // Enum properties
        if (properties.TryGetValue("SomeEnumProperty", out var enumObj))
        {
            var enumStr = enumObj?.ToString();
            if (!string.IsNullOrWhiteSpace(enumStr) &&
                Enum.TryParse<YourEnumType>(enumStr, out var parsedEnum))
            {
                yourNode.SomeEnumProperty = parsedEnum;
            }
        }
        
        // ⚠️ CRITICAL: List/Array properties
        if (properties.TryGetValue("OutputMappings", out var mappingsObj))
        {
            List<OutputMapping>? parsedMappings = null;

            // Handle string JSON
            if (mappingsObj is string jsonMappings)
            {
                try
                {
                    var mappingData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonMappings);
                    if (mappingData != null)
                    {
                        parsedMappings = mappingData.Select(m => new OutputMapping
                        {
                            NewKey = m.TryGetValue("NewKey", out var nk) ? nk?.ToString() ?? string.Empty : string.Empty,
                            SourceNodeId = m.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                            SourceOutputKey = m.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                        }).ToList();
                    }
                }
                catch
                {
                    // Try direct deserialize
                    try
                    {
                        parsedMappings = JsonSerializer.Deserialize<List<OutputMapping>>(jsonMappings);
                    }
                    catch { }
                }
            }
            // Handle JsonElement
            else if (mappingsObj is JsonElement jsonElement)
            {
                try
                {
                    if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        var jsonString = jsonElement.GetString();
                        if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            parsedMappings = JsonSerializer.Deserialize<List<OutputMapping>>(jsonString);
                        }
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        parsedMappings = JsonSerializer.Deserialize<List<OutputMapping>>(jsonElement.GetRawText());
                    }
                }
                catch { }
            }

            if (parsedMappings != null)
            {
                yourNode.OutputMappings = parsedMappings;
                // ⚠️ CRITICAL: Rebuild dependent properties
                yourNode.RebuildDynamicOutputs();
            }
        }
        
        // TitleDisplayMode
        if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
        {
            var tdmStr = tdmObj?.ToString();
            if (!string.IsNullOrWhiteSpace(tdmStr) &&
                Enum.TryParse<TitleDisplayMode>(tdmStr, out var titleDisplayMode))
            {
                yourNode.TitleDisplayMode = titleDisplayMode;
            }
        }
    }
}
```

#### 8.3. Quy tắc quan trọng

| Quy tắc | Giải thích |
|---------|------------|
| **Serialize ALL properties** | Kể cả List/Array, Enum |
| **Multiple format support** | Hỗ trợ cả string JSON và JsonElement |
| **Error handling** | Dùng try-catch tránh crash |
| **Rebuild logic** | Gọi rebuild methods sau deserialize |
| **Null safety** | Luôn check null trước assign |

---

### BƯỚC 9: Implement Copy/Paste

#### 9.1. Keyboard Shortcuts

File: `Services/Interaction/WorkflowEditorEventService.cs`

```csharp
// Handle Ctrl+C (Copy)
if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
{
    if (vm.SelectedNode != null)
    {
        // ⚠️ CRITICAL: Add your node type
        if (vm.SelectedNode is YourNode || 
            vm.SelectedNode is InputNode || 
            vm.SelectedNode is KeyPressEventNode)
        {
            _copiedNode = vm.SelectedNode;
            e.Handled = true;
            return;
        }
    }
}

// Handle Ctrl+V (Paste)
if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
{
    if (_copiedNode != null)
    {
        // ⚠️ CRITICAL: Add your node type
        if (_copiedNode is YourNode || 
            _copiedNode is InputNode || 
            _copiedNode is KeyPressEventNode)
        {
            var mousePos = Mouse.GetPosition(Host.WorkflowCanvas);
            Host.DuplicateNodeAtPosition(_copiedNode, mousePos.X, mousePos.Y);
            e.Handled = true;
            return;
        }
    }
}
```

#### 9.2. Copy Logic

File: `Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs`

```csharp
// In CreateDuplicateNodeInstance()
if (source is YourNode srcNode && node is YourNode dstNode)
{
    // ⚠️ CRITICAL: Copy ALL properties
    dstNode.SomeProperty = srcNode.SomeProperty;
    dstNode.SomeNumber = srcNode.SomeNumber;
    dstNode.TitleDisplayMode = srcNode.TitleDisplayMode;
    
    // ⚠️ CRITICAL: Clone list/array to avoid reference sharing
    if (srcNode.ArrayValues != null && srcNode.ArrayValues.Count > 0)
    {
        dstNode.ArrayValues = new List<string>(srcNode.ArrayValues);
    }
    else
    {
        dstNode.ArrayValues = new List<string>();
    }
}

// After setting Title
var baseTitle = source.Title ?? string.Empty;
var newTitle = GenerateUniqueTitle(baseTitle);
node.Title = newTitle;

// ⚠️ CRITICAL: Trigger PropertyChanged
if (node is YourNode yourNode)
{
    yourNode.NotifyTitleChanged();
}
```

#### 9.3. Quy tắc quan trọng

| Quy tắc | Giải thích |
|---------|------------|
| **Add to both Ctrl+C and Ctrl+V** | Cả 2 conditions phải có node type |
| **Copy ALL properties** | Kể cả TitleDisplayMode |
| **Clone lists** | Tránh reference sharing |
| **NotifyTitleChanged** | Gọi sau khi set Title |

---

### BƯỚC 10: [Optional] Implement Executor

#### 10.1. Template

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using System;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    public sealed class YourNodeExecutor : INodeExecutor
    {
        public async Task ExecuteAsync(
            WorkflowNode node, 
            NodeExecutionEnvironment env)
        {
            if (node is not YourNode yourNode)
                throw new InvalidOperationException("YourNodeExecutor can only execute YourNode.");

            try
            {
                // ⚠️ Callback: Entering node
                env.OnEnteringNode?.Invoke(node);

                // ⚠️ Callback: Started
                env.OnNodeStarted?.Invoke(node);

                // ===== YOUR LOGIC HERE =====
                
                // Example: Resolve input
                var inputValue = await ResolveInputAsync(yourNode, env);
                
                // Example: Process
                var outputValue = ProcessData(inputValue);
                
                // Example: Set output
                SetOutput(yourNode, "OutputKey", outputValue);
                
                // ===== END LOGIC =====

                // ⚠️ Callback: Completed
                env.OnNodeCompleted?.Invoke(node);

                // ⚠️ Execute next nodes
                var outputConnections = env.Connections
                    .Where(c => c.FromNode == node)
                    .ToList();

                foreach (var conn in outputConnections)
                {
                    if (conn.ToNode == null) continue;
                    await env.ExecuteNextAsync(conn.ToNode, conn);
                }
            }
            catch (Exception ex)
            {
                // ⚠️ Callback: Failed
                env.OnNodeFailed?.Invoke(node, ex.Message);
                throw; // Re-throw to stop execution
            }
        }

        // ===== HELPER METHODS =====
        
        private async Task<string> ResolveInputAsync(
            YourNode node, 
            NodeExecutionEnvironment env)
        {
            // Example: Resolve from DynamicInputs
            var input = node.DynamicInputs?.FirstOrDefault();
            if (input == null) return string.Empty;

            var sourceNodeId = input.SelectedSourceNodeId;
            var sourceOutputKey = input.SelectedSourceOutputKey;

            if (string.IsNullOrWhiteSpace(sourceNodeId) || 
                string.IsNullOrWhiteSpace(sourceOutputKey))
            {
                return string.Empty;
            }

            // Try direct connection first
            var directConn = env.Connections
                .FirstOrDefault(c => c.ToNode == node && 
                                     c.FromNode?.Id == sourceNodeId);

            if (directConn?.FromNode != null)
            {
                var value = env.Service.NodeDataPanelService.ResolveDynamicValueByKey(
                    directConn.FromNode, 
                    sourceOutputKey);
                return value ?? string.Empty;
            }

            // Fallback: Find node by ID
            var sourceNode = env.Service.ViewModel?.Nodes
                .FirstOrDefault(n => n.Id == sourceNodeId);

            if (sourceNode != null)
            {
                var value = env.Service.NodeDataPanelService.ResolveDynamicValueByKey(
                    sourceNode, 
                    sourceOutputKey);
                return value ?? string.Empty;
            }

            return string.Empty;
        }

        private string ProcessData(string input)
        {
            // Your processing logic here
            return input.ToUpper();
        }

        private void SetOutput(YourNode node, string key, string value)
        {
            var output = node.DynamicOutputs?.FirstOrDefault(o => o.Key == key);
            if (output != null)
            {
                output.UserValueOverride = value;
            }
        }
    }
}
```

#### 10.2. Đăng ký Executor

File: `Services/Workflow/WorkflowExecutionService.cs`

```csharp
// Trong constructor
_nodeExecutors = new Dictionary<NodeType, INodeExecutor>
{
    [NodeType.YourType] = new YourNodeExecutor(),
    // ... other executors
    [NodeType.Default] = new DefaultNodeExecutor()
};
```

#### 10.3. Quy tắc quan trọng

| Quy tắc | Giải thích |
|---------|------------|
| **Use callbacks** | `OnEnteringNode`, `OnNodeStarted`, `OnNodeCompleted`, `OnNodeFailed` |
| **Try-catch** | Wrap logic và gọi `OnNodeFailed` + re-throw |
| **ExecuteNextAsync** | Gọi cho tất cả output connections |
| **Resolve inputs** | Thử direct connection trước, fallback node lookup |

---

## 4. TÍNH NĂNG NÂNG CAO (OPTIONAL)

### 4.1. TitleDisplayMode + TitleColorMode (ĐÃ CÓ TRONG TEMPLATE CHUẨN)

> ⚠️ **LƯU Ý**: TitleDisplayMode và TitleColorMode **đã được bao gồm** trong template chuẩn ở Bước 6.  
> Mục này chỉ nhắc lại những gì cần thêm ở **Node Model** và **ViewModel** (NodeControl đã có sẵn trong template).

#### Thêm vào Node Model (BẮT BUỘC):

```csharp
// TitleDisplayMode: Hidden / Hover / Always
private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
public TitleDisplayMode TitleDisplayMode
{
    get => _titleDisplayMode;
    set { if (_titleDisplayMode != value) { _titleDisplayMode = value; OnPropertyChanged(); } }
}

// TitleColorMode: NodeColor / CustomColor
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

// Reference tới UI element (dùng bởi NodeControl và Renderer)
public TextBlock? TitleTextBlockUI { get; set; }
```

#### Thêm vào ViewModel:

```csharp
public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
{
    new TitleDisplayModeOption(TitleDisplayMode.Hidden, "Ẩn tiêu đề"),
    new TitleDisplayModeOption(TitleDisplayMode.Hover, "Hiện khi hover"),
    new TitleDisplayModeOption(TitleDisplayMode.Always, "Luôn hiện")
};
```

#### NodeControl logic:

Đã có **đầy đủ** trong template chuẩn ở **Bước 6.2**. Bao gồm:
- TitleTextBlock creation + `node.TitleTextBlockUI` assignment
- 7 event handlers (Hover, PropertyChanged, VisibilityDescriptor, Loaded, SizeChanged, Unloaded, LayoutUpdated)
- GetTitleBrush, GetTitleVisibility, UpdateTitleVisibility, ThrottledUpdateTitlePosition, UpdateTitlePosition

**Checklist TitleDisplayMode + TitleColorMode:**
- [ ] Node Model: 3 properties (`TitleDisplayMode`, `TitleColorMode`, `TitleColorKey`) + `TitleTextBlockUI`
- [ ] ViewModel: `TitleDisplayModeOptions` collection
- [ ] Dialog XAML: ComboBox cho TitleDisplayMode (+ optional TitleColor ComboBox)
- [ ] NodeControl: ✅ Đã có trong template chuẩn Bước 6.2
- [ ] Renderer: Update `RemoveNode()` xóa titleTextBlock khỏi canvas
- [ ] Persistence: Serialize/Deserialize `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey`

---

### 4.2. TitleColorMode Support

> Cho phép đổi màu title riêng (NodeColor/CustomColor)

**Đã có sẵn trong BaseNodeDialogViewModel**, chỉ cần:

1. Thêm vào Node Model:

```csharp
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
```

2. Thêm vào Dialog XAML (trong Tab Logic, sau TitleDisplayMode):

```xml
<!-- TitleColorMode Section -->
<TextBlock Text="Màu tiêu đề:" ... />
<Grid>
    <ComboBox x:Name="TitleColorComboBox"
              ItemsSource="{Binding TitleColorOptions}"
              SelectedValuePath="Key"
              DisplayMemberPath="DisplayName"
              SelectedValue="{Binding TitleColorKey, Mode=TwoWay}"
              SelectionChanged="TitleColorComboBox_SelectionChanged"/>
    <Border x:Name="TitleColorPreview" ... />
</Grid>
```

3. Thêm vào Dialog Code-behind:

```csharp
private void TitleColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    UpdateTitleColorPreview();
}

private void UpdateTitleColorPreview()
{
    // Xem template trong tài liệu gốc
}
```

4. Persistence: Serialize/Deserialize `TitleColorMode` và `TitleColorKey`

---

### 4.3. ReuseRoutes + LineStyle

> Cho phép cấu hình node IN/OUT và kiểu line per-route

**Chi tiết xem Phase 4 trong tài liệu gốc.**

Tóm tắt:
- Thêm `NodeReuseRoute` class vào WorkflowNode
- Thêm `ReuseRoutes` list vào node
- Implement UI trong Tab "Cấu hình"
- Implement `GetLineStyleForConnection()` trong ConnectionRenderer
- Serialize/Deserialize ReuseRoutes

---

### 4.4. PortPosition Configuration

> Cho phép chọn vị trí port IN/OUT (Left/Top/Right/Bottom)

**Chi tiết xem Phase 4b trong tài liệu gốc.**

Tóm tắt:
- `NodePort.Position` đã có sẵn
- Thêm `InputPortPosition`/`OutputPortPosition` vào BaseNodeDialogViewModel
- Implement `SavePortPositions()` trong BaseNodeDialogViewModel
- Thêm UI trong Tab "Cấu hình"
- Persistence: Lưu/khôi phục `Position` trong PortDto

---

### 4.5. Input/Output Động

> Cho phép user tự thêm/xóa input/output

**Chi tiết xem các ví dụ như CodeNode, WebNode trong tài liệu gốc.**

Tóm tắt:
- Model: Add `InputMappings` list
- ViewModel: Add `InputMappingsList` + Add/Remove commands
- Dialog XAML: ItemsControl với TextBox + Button xóa
- Persistence: Serialize/Deserialize list

---

## 5. XỬ LÝ LỖI THƯỜNG GẶP

### Lỗi 1: "A local variable named 'npc' is already defined"

**Nguyên nhân:** Nhiều block `if (node is INotifyPropertyChanged npc)`

**Giải pháp:** Gộp tất cả vào MỘT block

```csharp
// ✅ ĐÚNG
if (node is INotifyPropertyChanged npc)
{
    npc.PropertyChanged += (s, e) =>
    {
        if (e.PropertyName == nameof(WorkflowNode.Title))
            titleTextBlock.Text = node.Title;
        else if (e.PropertyName == nameof(YourNode.SomeProperty))
            // handle
    };
}
```

---

### Lỗi 2: Node nhảy đến vị trí chuột khi mở dialog

**Nguyên nhân:** Node vẫn có mouse capture hoặc được select

**Giải pháp:** Trong `OpenNodeDialog()`:

```csharp
if (node.Border != null && node.Border.IsMouseCaptured)
{
    node.Border.ReleaseMouseCapture();
}
host.DraggedNode = null;
if (host.ViewModel != null)
{
    host.ViewModel.SelectedNode = null;
}
```

---

### Lỗi 3: Properties bị mất khi save/load

**Nguyên nhân:** Không implement serialize/deserialize

**Giải pháp:** Xem Bước 8 - Implement Persistence

---

### Lỗi 4: ComboBox source node hiển thị tiêu đề cũ

**Nguyên nhân:** Không gọi `RefreshAvailableSourcesForInputs()`

**Giải pháp:** Gọi trong `LoadInputs()` trước khi tạo `InputItemViewModel`

---

### Lỗi 5: Title không di chuyển theo node

**Nguyên nhân:** Renderer thiếu `UpdateNodePosition()` implementation

**Giải pháp:** Implement `UpdateNodePosition()` trong Renderer (xem Bước 7)

---

### Lỗi 6: Port colors không apply

**Nguyên nhân:** Chỉ set color khi `port.PortUI == null`

**Giải pháp:** LUÔN update color trong cả `RenderNode()` và `UpdateNodePosition()`

```csharp
if (port.PortUI is Ellipse ellipse)
{
    ellipse.Fill = new SolidColorBrush(portColor);
}
```

---

### Lỗi 7: Memory leak khi xóa node (TitleDisplayMode)

**Nguyên nhân:** Không cleanup `titleTextBlock`, timers, dictionaries

**Giải pháp:** Thêm `border.Unloaded` handler (xem template NodeControl)

---

### Lỗi 8: ComboBox SelectedValue bị null sau mỗi lần mở dialog

**Nguyên nhân:** Gọi `RefreshAvailableNodes()` trong `OnLoaded()` → `Clear()` → TwoWay binding set null

**Giải pháp:**
- KHÔNG gọi `RefreshAvailableNodes()` trong `OnLoaded()`
- Chỉ gọi trong constructor
- `RefreshAvailableNodes()` phải bao gồm các node đã chọn trong mappings

---

## 6. CHECKLIST CUỐI CÙNG

### Phase 1: Core (Bắt buộc)

- [ ] **Bước 1:** Trao đổi thiết kế với user (ColorKey, Icon, Features)
- [ ] **Bước 2:** Tạo Node Model
  - [ ] Properties đầy đủ
  - [ ] INotifyPropertyChanged (nếu cần)
  - [ ] NotifyTitleChanged() method
- [ ] **Bước 3:** Tạo Dialog XAML
  - [ ] Kế thừa BaseNodeDialog
  - [ ] 2 tabs: Logic + Cấu hình
  - [ ] Header: Title + Play + Close
- [ ] **Bước 4:** Tạo Dialog Code-behind
  - [ ] InitializeBase()
  - [ ] GetInputsPanel/GetOutputsPanel
  - [ ] CloseButton_Click
- [ ] **Bước 5:** Tạo ViewModel
  - [ ] Kế thừa BaseNodeDialogViewModel
  - [ ] ObservableProperty cho properties
  - [ ] RefreshAvailableSourcesForInputs()
  - [ ] OnSaveTitle()
- [ ] **Bước 6:** Tạo NodeControl (xem template chuẩn 6.2)
  - [ ] Static fields: `_titleUpdateTimers`, `_titleUpdatedAfterZoom`
  - [ ] CreateBorder: Grid + Icon(SvgViewboxEx) + Border(Tag=node)
  - [ ] TitleTextBlock: tạo + gán `node.TitleTextBlockUI`
  - [ ] 9 Event handlers (MouseEnter/Leave, PropertyChanged, VisibilityDescriptor, Loaded, SizeChanged, Unloaded, LayoutUpdated, MouseRightButtonUp)
  - [ ] OpenNodeDialog: Release capture + Clear drag + Deselect + DialogManager
  - [ ] 5 Helper methods: GetTitleBrush, GetTitleVisibility, UpdateTitleVisibility, ThrottledUpdateTitlePosition, UpdateTitlePosition
  - [ ] GetOrCreateDialogManager + GetTextBrush
- [ ] **Bước 7:** Tạo Renderer
  - [ ] RenderNode()
  - [ ] UpdateNodePosition()
  - [ ] RemoveNode()
  - [ ] Đăng ký trong WorkflowEditorWindow
- [ ] **Bước 8:** Implement Persistence
  - [ ] Serialize trong GetNodeProperties()
  - [ ] Deserialize trong RestoreNodeProperties()
  - [ ] Test save/load workflow
- [ ] **Bước 9:** Implement Copy/Paste
  - [ ] Add Ctrl+C/V trong EventService
  - [ ] Copy ALL properties trong CreateDuplicateNodeInstance()
  - [ ] NotifyTitleChanged() sau set Title
  - [ ] Test copy/paste

### Phase 2: Advanced (Tùy chọn)

- [ ] **TitleDisplayMode** (nếu cần)
  - [ ] Node Model properties
  - [ ] ViewModel options
  - [ ] Dialog XAML
  - [ ] NodeControl logic
  - [ ] Renderer UpdateNodePosition/RemoveNode
- [ ] **TitleColorMode** (nếu cần)
  - [ ] Node Model properties
  - [ ] Dialog XAML + Code-behind
  - [ ] Persistence
- [ ] **ReuseRoutes + LineStyle** (nếu cần)
  - [ ] Model classes
  - [ ] Dialog UI
  - [ ] ConnectionRenderer logic
  - [ ] Persistence
- [ ] **PortPosition** (nếu cần)
  - [ ] ViewModel properties
  - [ ] Dialog UI
  - [ ] SavePortPositions()
  - [ ] Persistence
- [ ] **Executor** (nếu cần thực thi logic)
  - [ ] Tạo YourNodeExecutor.cs
  - [ ] Implement ExecuteAsync()
  - [ ] Đăng ký trong WorkflowExecutionService
  - [ ] Test execution

### Testing

- [ ] Create node → hiển thị đúng icon + màu
- [ ] Open dialog → hiển thị đúng properties
- [ ] Edit properties → save thành công
- [ ] Close dialog → lưu tự động
- [ ] Save workflow → properties được lưu
- [ ] Load workflow → properties được khôi phục
- [ ] Copy/Paste → properties được copy
- [ ] Delete node → cleanup hoàn toàn
- [ ] [Optional] Execute node → output đúng

---

## 7. KẾT LUẬN

### Tóm tắt quy trình

```
1. Trao đổi → 2. Model → 3. Dialog XAML → 4. Code-behind → 
5. ViewModel → 6. NodeControl → 7. Renderer → 
8. Persistence → 9. Copy/Paste → [10. Executor]
```

### Nguyên tắc vàng

1. **Luôn đọc template trước khi code**
2. **Follow checklist từng bước**
3. **Test sau mỗi bước**
4. **Copy từ implementation tương tự**
5. **Persistence là BẮT BUỘC**
6. **Port colors LUÔN update**
7. **Cleanup để tránh memory leak**

### Reference Implementations

Tham khảo các node đã implement đầy đủ:
- `InputNode` - Basic + TitleDisplayMode
- `KeyPressEventNode` - Basic + TitleDisplayMode
- `LoopNode` - Basic + TitleDisplayMode + Special shape
- `WebNode` - Advanced (input mapping + port position)
- `CodeNode` - Advanced (input mapping + dynamic outputs)