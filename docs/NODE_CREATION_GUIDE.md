# Hướng dẫn tạo Node — FlowMy

> Cập nhật: 2026-05-16
> Áp dụng cho toàn bộ node mới và node cần sửa đổi.
> Tài liệu này tự đứng độc lập — không cần đọc tài liệu nào khác.

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Danh sách file cần tạo](#2-danh-sách-file-cần-tạo)
3. [Node Model](#3-node-model)
4. [NodeControl — Giao diện node trên canvas](#4-nodecontrol)
5. [Dialog XAML](#5-dialog-xaml)
6. [Dialog Code-behind](#6-dialog-code-behind)
7. [ViewModel](#7-viewmodel)
8. [Renderer](#8-renderer)
9. [Executor](#9-executor)
10. [Persistence](#10-persistence)
11. [Đăng ký node vào hệ thống](#11-đăng-ký-node-vào-hệ-thống)
12. [Các trường hợp đặc biệt](#12-các-trường-hợp-đặc-biệt)
13. [Lỗi thường gặp và cách tránh](#13-lỗi-thường-gặp)

---

## 1. Tổng quan kiến trúc

```
Models/Nodes/YourNode.cs                          ← Data model
Views/NodeControls/YourNodeControl.cs             ← UI trên canvas
Views/Overlays/YourNodeDialog.xaml                ← Dialog cấu hình
Views/Overlays/YourNodeDialog.xaml.cs             ← Code-behind
ViewModels/YourNodeDialogViewModel.cs             ← ViewModel
Services/Rendering/YourNodeRenderer.cs            ← Render node lên canvas
Services/Workflow/NodeExecutors/YourNodeExecutor.cs ← Logic thực thi (nếu cần)
```

**Nguyên tắc cốt lõi**: Mọi logic chung đã nằm trong base classes. File của bạn chỉ chứa những gì **đặc thù** của node đó.

| Base class | Xử lý gì |
|-----------|---------|
| `BaseNodeControlHelper` | Hover, keyboard port, zoom, title position, visibility sync, dialog, cleanup |
| `BaseNodeDialog` | Vị trí dialog, lưu title, load Inputs/Outputs, color picker, brush resolver |
| `BaseNodeDialogViewModel` | NodeTitle, TitleDisplayMode, TitleColorMode, collections, commands |
| `WorkflowNode` | INotifyPropertyChanged, TitleDisplayMode, TitleColorMode, TitleColorKey |

---

## 2. Danh sách file cần tạo

| # | File | Vị trí |
|---|------|--------|
| 1 | `YourNode.cs` | `Models/Nodes/` |
| 2 | `YourNodeControl.cs` | `Views/NodeControls/` |
| 3 | `YourNodeDialog.xaml` + `.xaml.cs` | `Views/Overlays/` |
| 4 | `YourNodeDialogViewModel.cs` | `ViewModels/` |
| 5 | `YourNodeRenderer.cs` | `Services/Rendering/` |
| 6 | `YourNodeExecutor.cs` | `Services/Workflow/NodeExecutors/` (nếu cần) |

Ngoài ra cần đăng ký ở 4 chỗ — xem [§11](#11-đăng-ký-node-vào-hệ-thống).

---

## 3. Node Model

`WorkflowNode` đã implement `INotifyPropertyChanged` và chứa các property chung. **Không khai báo lại** những gì base đã có.

### 3.1 WorkflowNode đã có sẵn — KHÔNG khai báo lại

| Đã có trong base | Ghi chú |
|-----------------|---------|
| `PropertyChanged` event | Gọi `OnPropertyChanged()` trong setter là đủ |
| `OnPropertyChanged()` method | Có sẵn, gọi trực tiếp |
| `TitleDisplayMode` property | Default: `Always` — set lại trong constructor nếu muốn khác |
| `TitleColorMode` property | Default: `NodeColor` |
| `TitleColorKey` property | Default: `null` |
| `NotifyTitleChanged()` method | Gọi `OnPropertyChanged(nameof(Title))` — override nếu cần thêm logic |

### 3.2 Template chuẩn

```csharp
namespace FlowMy.Models.Nodes
{
    // KHÔNG thêm INotifyPropertyChanged — WorkflowNode đã implement
    public sealed class YourNode : WorkflowNode
    {
        private string _someProperty = string.Empty;
        private int _someCount;

        public YourNode()
        {
            Type = NodeType.YourType;   // thêm vào Models/Nodes/NodeType.cs
            Title = "Your Node";

            // Nếu muốn default khác Always:
            TitleDisplayMode = TitleDisplayMode.Hidden;

            // Thêm ports
            Ports.Add(new NodePort { IsInput = true,  Position = PortPosition.Left,  IsVisible = true, ColorKey = "Info" });
            Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true, ColorKey = "SunsetOrange" });
        }

        public string SomeProperty
        {
            get => _someProperty;
            set { if (_someProperty != value) { _someProperty = value; OnPropertyChanged(); } }
        }

        public int SomeCount
        {
            get => _someCount;
            set { if (_someCount != value) { _someCount = value; OnPropertyChanged(); } }
        }

        // CHỈ override nếu cần thêm logic sau khi title thay đổi
        // public override void NotifyTitleChanged() { base.NotifyTitleChanged(); ... }

        // ❌ KHÔNG khai báo: PropertyChanged event
        // ❌ KHÔNG khai báo: OnPropertyChanged method
        // ❌ KHÔNG khai báo: TitleDisplayMode property
        // ❌ KHÔNG khai báo: TitleColorMode property
        // ❌ KHÔNG khai báo: TitleColorKey property
    }
}
```

### 3.3 Tác động lên Services

Vì `WorkflowNode` có `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey` trực tiếp:

```csharp
// ✅ Persistence — dùng trực tiếp, không cần if node is XxxNode
dict["TitleDisplayMode"] = node.TitleDisplayMode.ToString();
dict["TitleColorMode"]   = node.TitleColorMode.ToString();
if (!string.IsNullOrEmpty(node.TitleColorKey))
    dict["TitleColorKey"] = node.TitleColorKey;

// ✅ PropertyChanged — không cần cast
node.PropertyChanged += (s, e) => { ... };

// ✅ NotifyTitleChanged — gọi trực tiếp
node.NotifyTitleChanged();
```

### 3.4 Checklist Node Model

```yaml
- [ ] Kế thừa WorkflowNode (KHÔNG thêm INotifyPropertyChanged)
- [ ] KHÔNG khai báo lại: PropertyChanged, OnPropertyChanged,
      TitleDisplayMode, TitleColorMode, TitleColorKey, NotifyTitleChanged
- [ ] Thêm NodeType enum value vào Models/Nodes/NodeType.cs
- [ ] Thêm ports trong constructor
- [ ] Dùng OnPropertyChanged() trong mọi setter
- [ ] Nếu muốn default TitleDisplayMode khác Always: set trong constructor
```

---

## 4. NodeControl

`BaseNodeControlHelper` xử lý toàn bộ logic chung. NodeControl chỉ cần tạo UI elements và gọi fluent API một lần.

### 4.1 BaseNodeControlHelper đã xử lý — KHÔNG tự viết lại

- `MouseEnter` / `MouseLeave` + hover state
- `PreviewKeyDown` + arrow key port positioning (Arrow = Port IN, Shift+Arrow = Port OUT)
- `PropertyChanged` thread-safe cho: `NodeBrush`, `Title`, `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey`
- `Loaded` / `SizeChanged` / `LayoutUpdated` / `Unloaded`
- `DependencyPropertyDescriptor` visibility sync (tránh memory leak khi đổi workflow)
- `DispatcherTimer` throttling cho title position update
- `NodeDialogManager` dialog management (tránh mở 2 dialog cùng lúc)
- Canvas ZIndex = 20000 cho title TextBlock

### 4.2 Utility methods có sẵn

```csharp
// Icon fill từ resource "TextOn{colorKey}Brush"
Brush fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);

// Title foreground brush theo TitleColorMode
Brush titleBrush = BaseNodeControlHelper.ResolveTitleBrush(
    node.TitleColorMode, node.TitleColorKey, node.NodeBrush);

// Lấy context của border (advanced scenarios)
var ctx = BaseNodeControlHelper.GetContext(border);
```

### 4.3 Template chuẩn (node thông thường)

```csharp
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    public static class YourNodeControl
    {
        public static Border CreateBorder(YourNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // ─── 1. ICON ───
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri),
                "your-icon-key duotone-regular",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32, Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey)
            };

            // ─── 2. GRID ───
            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };
            grid.Children.Add(iconSvg);

            // ─── 3. TITLE TEXTBLOCK ───
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Your Node",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode, node.TitleColorKey, node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always
                    ? Visibility.Visible : Visibility.Collapsed
            };
            node.TitleTextBlockUI = titleTextBlock; // ⚠️ BẮT BUỘC

            // ─── 4. BORDER ───
            var border = new Border
            {
                Child = grid,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, Direction = 270,
                    ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node // ⚠️ BẮT BUỘC — renderer dùng Tag để lấy node
            };

            // ─── 5. CUSTOM PROPERTY HANDLERS (chỉ những gì đặc thù của node) ───
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                // Luôn có: cập nhật icon fill khi ColorKey thay đổi
                [nameof(WorkflowNode.ColorKey)] = ctx =>
                {
                    iconSvg.Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);
                },
                // Thêm handlers cho properties đặc thù nếu cần:
                // [nameof(YourNode.SomeProperty)] = ctx => { ... }
            };

            // ─── 6. FLUENT API ───
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()      // Loaded, SizeChanged, LayoutUpdated + zoom
                .WithHoverBehavior()        // MouseEnter/Leave + focus
                .WithKeyboardPorts()        // Arrow keys → port position
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new YourNodeDialog(
                    node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()             // ⚠️ BẮT BUỘC
                .WithVisibilitySync()      // DependencyPropertyDescriptor visibility
                .WithCanvasIntegration()   // Loaded → add title to WorkflowCanvas
                .Build();                  // ⚠️ BẮT BUỘC

            return border;
        }
    }
}
```

### 4.4 Fluent API — tham chiếu nhanh

| Method | Đăng ký gì | Bắt buộc? |
|--------|-----------|-----------|
| `.WithTitleManagement()` | `Loaded`, `SizeChanged`, `LayoutUpdated` + zoom handling | Có (nếu có floating title) |
| `.WithHoverBehavior()` | `MouseEnter`, `MouseLeave`, `Focusable = true` | Có |
| `.WithKeyboardPorts()` | `PreviewKeyDown` → Arrow keys đổi port | Có |
| `.WithPropertySync(handlers)` | `PropertyChanged` thread-safe | Có |
| `.WithDialogSupport(factory)` | `MouseRightButtonUp` → mở dialog | Có (nếu có dialog) |
| `.WithCleanup()` | `Unloaded` → stop timer, remove title, dispose | **Luôn luôn** |
| `.WithVisibilitySync()` | `DependencyPropertyDescriptor` Visibility sync | Có (nếu có floating title) |
| `.WithCanvasIntegration()` | `Loaded` → add title to WorkflowCanvas, ZIndex=20000 | Có (nếu có floating title) |
| `.Build()` | Áp dụng tất cả + lưu context | **Luôn luôn** |

### 4.5 Default property handlers (WithPropertySync tự xử lý — KHÔNG khai báo lại)

`WithPropertySync` đã tự xử lý:
- `NodeBrush` → `border.Background` + `titleTextBlock.Foreground`
- `Title` → `titleTextBlock.Text` + reposition
- `TitleDisplayMode` → title visibility
- `TitleColorMode` / `TitleColorKey` → `titleTextBlock.Foreground`

Chỉ khai báo trong `customPropertyHandlers` những gì **đặc thù của node** (ví dụ: `ColorKey` → update icon fill, `MouseButton` → đổi icon, `IsInputMode` → sync data panels).

### 4.6 Checklist NodeControl

```yaml
- [ ] public static class
- [ ] node.TitleTextBlockUI = titleTextBlock  ← BẮT BUỘC trước Build()
- [ ] border.Tag = node                       ← BẮT BUỘC
- [ ] customPropertyHandlers có ColorKey → update icon fill
- [ ] Gọi .WithCleanup() và .Build()
- [ ] KHÔNG tự viết: MouseEnter/Leave, PreviewKeyDown, PropertyChanged,
      Loaded/SizeChanged/LayoutUpdated/Unloaded, DispatcherTimer,
      _titleUpdateTimers, _titleUpdatedAfterZoom, GetOrCreateDialogManager,
      GetTitleBrush, UpdateTitleVisibility, UpdateTitlePosition
```

---

## 5. Dialog XAML

```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="clr-namespace:FlowMy.Views.Overlays"
    Width="460" MinWidth="350" MaxWidth="900" MinHeight="350">
    <!-- KHÔNG đặt Height cứng — NodeDialogManager auto-size 90% screen -->

    <Border CornerRadius="12" Style="{DynamicResource DialogOuterBorder}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- HEADER -->
            <Border Grid.Row="0" Style="{DynamicResource DialogHeaderBorder}" Padding="16,12,12,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBox x:Name="TitleTextBox" Grid.Column="0"
                             Text="{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}"
                             Style="{DynamicResource BaseTextBoxV2}" FontSize="16"/>
                    <StackPanel Grid.Column="1" Orientation="Horizontal">
                        <Button Width="24" Height="24" Content="▶"
                                Style="{DynamicResource PrimaryButton}"
                                Command="{Binding RunSingleNodeCommand}" Margin="8,0,0,0"/>
                        <Button Width="24" Height="24" Content="×"
                                Style="{DynamicResource DangerButton}"
                                Click="CloseButton_Click" Margin="8,0,0,0"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- CONTENT -->
            <TabControl Grid.Row="1" Background="Transparent" BorderThickness="0">
                <TabItem Header="Cấu hình" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>

                            <!-- TitleDisplayMode -->
                            <TextBlock Text="Hiển thị tiêu đề:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <ComboBox Height="36" Style="{DynamicResource BaseComboBox}" Margin="0,0,0,16"
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
                                <Border x:Name="TitleColorPreview" Grid.Column="1"
                                        Width="36" Height="36" CornerRadius="6" Margin="8,0,0,0"
                                        BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1"/>
                            </Grid>

                            <!-- Properties đặc thù của node ở đây -->

                            <!-- Inputs Panel -->
                            <TextBlock Text="Inputs:" Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <StackPanel x:Name="InputsPanel"/>
                            </Border>

                            <!-- Outputs Panel -->
                            <TextBlock Text="Outputs:" Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <StackPanel x:Name="OutputsPanel"/>
                            </Border>

                        </StackPanel>
                    </ScrollViewer>
                </TabItem>
            </TabControl>
        </Grid>
    </Border>
</local:BaseNodeDialog>
```

**x:Name bắt buộc** (base dùng `FindName` để tìm):

| x:Name | Kiểu | Mục đích |
|--------|------|---------|
| `TitleColorComboBox` | `ComboBox` | Base đọc `SelectedValue` để update preview |
| `TitleColorPreview` | `Border` | Base set `Background` theo màu đã chọn |
| `InputsPanel` | `StackPanel` | Base load input items vào đây |
| `OutputsPanel` | `StackPanel` | Base load output items vào đây |

---

## 6. Dialog Code-behind

`BaseNodeDialog` đã xử lý toàn bộ logic chung. Code-behind chỉ cần constructor + 2 override bắt buộc.

### 6.1 BaseNodeDialog đã có sẵn — KHÔNG viết lại

| Tính năng | Ghi chú |
|-----------|---------|
| Snap phải màn hình | Tự động qua `SourceInitialized` |
| Clamp khi resize | Tự động qua `SizeChanged` |
| Lưu title khi đóng | Tự động qua `Closing` → `SaveTitleCommand` |
| `CloseButton_Click` | Tự động: `SaveTitleCommand + Close()` |
| `TitleColorComboBox_SelectionChanged` | Wire trong XAML, không cần override |
| `UpdateTitleColorPreview()` | Gọi trong constructor sau `InitializeBase()` |
| `ShowColorPicker(string? hex)` | Trả về `#RRGGBB` hoặc null |
| `ResolveBrush(string? key, Brush fallback)` | Hỗ trợ hex, "LimeGreen", resource key |
| `GetThemeBrush(string key, Brush fallback)` | Instance method |
| `GetThemeColor(string key, Color fallback)` | Instance method |
| `BindThemeResource(element, dp, key)` | Bind DynamicResource cho code-behind control |
| Load Inputs/Outputs | Tự động qua `Loaded` → `GetInputsPanel()` / `GetOutputsPanel()` |

### 6.2 Template chuẩn

```csharp
public partial class YourNodeDialog : BaseNodeDialog
{
    private readonly YourNodeDialogViewModel _viewModel;

    public YourNodeDialog(YourNode node, IWorkflowEditorHost host, Window? owner)
        : base()
    {
        InitializeComponent();
        _viewModel = new YourNodeDialogViewModel(node, host);
        InitializeBase(_viewModel, owner);

        // Gọi nếu XAML có TitleColorPreview + TitleColorComboBox
        UpdateTitleColorPreview();
    }

    // BẮT BUỘC: trả về panel chứa inputs (null nếu không có)
    protected override Panel? GetInputsPanel() => InputsPanel;

    // BẮT BUỘC: trả về panel chứa outputs (null nếu không có)
    protected override Panel? GetOutputsPanel() => OutputsPanel;

    // CHỈ override nếu cần logic sau Loaded
    // protected override void OnLoaded() { base.OnLoaded(); ... }

    // CHỈ override nếu cần flush binding khi đóng bằng Alt+F4 hoặc X taskbar
    // protected override void BeforeSaveOnClose() { FlushMyBindings(); }

    // CHỈ override CloseButton_Click nếu cần làm thêm gì trước khi đóng
    // protected override void CloseButton_Click(object sender, RoutedEventArgs e)
    // {
    //     FlushMyBindings();
    //     base.CloseButton_Click(sender, e); // ← gọi base để SaveTitle + Close
    // }
}
```

### 6.3 Flush binding trước khi đóng

```csharp
// Ưu tiên BeforeSaveOnClose — xử lý cả Alt+F4 và nút X
protected override void BeforeSaveOnClose()
{
    MyComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
    MyTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
}
```

### 6.4 Color picker tùy chỉnh

```csharp
private void PickMyColor_Click(object sender, RoutedEventArgs e)
{
    // ✅ Dùng base.ShowColorPicker — KHÔNG tự viết lại
    var hex = ShowColorPicker(_viewModel.MyColorHex);
    if (!string.IsNullOrWhiteSpace(hex))
    {
        _viewModel.MyColorHex = hex;
        // ✅ Dùng base.ResolveBrush — KHÔNG tự viết lại
        MyColorPreview.Background = ResolveBrush(hex, Brushes.Gray);
    }
}
```

### 6.5 Checklist Dialog Code-behind

```yaml
- [ ] Kế thừa BaseNodeDialog (KHÔNG kế thừa Window trực tiếp)
- [ ] Constructor: InitializeComponent() → new ViewModel → InitializeBase(vm, owner)
- [ ] Gọi UpdateTitleColorPreview() nếu XAML có TitleColorPreview
- [ ] Override GetInputsPanel() và GetOutputsPanel()
- [ ] KHÔNG tự viết: CloseButton_Click (chỉ SaveTitle+Close),
      TitleColorComboBox_SelectionChanged, UpdateTitleColorPreview,
      ShowColorPicker, ResolveBrush, GetThemeBrush, GetThemeColor,
      ViewModel_PropertyChanged rỗng
- [ ] Nếu cần flush binding: override BeforeSaveOnClose() (xử lý cả Alt+F4)
- [ ] KHÔNG set WindowStartupLocation sau InitializeBase — base đã set Manual
```

---

## 7. ViewModel

`BaseNodeDialogViewModel` đã có sẵn toàn bộ properties, collections và commands chung.

### 7.1 BaseNodeDialogViewModel đã có sẵn — KHÔNG khai báo lại

**Observable Properties:**

| Property | Kiểu | Ghi chú |
|----------|------|---------|
| `NodeTitle` | `string` | Sync từ `node.Title` |
| `TitleDisplayMode` | `TitleDisplayMode` | Sync từ node |
| `TitleColorMode` | `TitleColorMode` | Sync từ node |
| `TitleColorKey` | `string?` | Sync từ node; update canvas ngay khi thay đổi |
| `InputPortPosition` | `PortPosition` | Từ port IN đầu tiên |
| `OutputPortPosition` | `PortPosition` | Từ port OUT đầu tiên |
| `Inputs` | `ObservableCollection<InputItemViewModel>` | Load từ `DynamicInputs` |
| `Outputs` | `ObservableCollection<OutputItemViewModel>` | Load từ `DynamicOutputs` |
| `ReuseRoutes` | `ObservableCollection<ReuseRouteItemViewModel>` | Load từ connections |

**Collections tĩnh (KHÔNG khai báo lại — khai báo lại sẽ shadow base):**

| Collection | Nội dung |
|-----------|---------|
| `TitleDisplayModeOptions` | Hidden / Hover / Always |
| `TitleColorOptions` | NodeColor / LimeGreen / PrimaryBrush / ... |
| `PortPositionOptions` | Left / Top / Right / Bottom |
| `ConnectionLineStyleOptions` | WorkflowDefault / Bezier / Orthogonal / ... |

**Commands:** `RunSingleNodeCommand`, `RunWorkflowFromNodeCommand`, `SaveTitleCommand`

**Protected helpers (gọi trực tiếp — KHÔNG viết lại):**

| Method | Chức năng |
|--------|----------|
| `RefreshAllNodesWithOutputs(target)` | Lấy tất cả nodes có DynamicOutputs |
| `GetOutputKeysForNode(nodeId)` | Lấy output keys của node theo ID |
| `FillOutputKeys(nodeId, target)` | Điền output keys vào collection |
| `CreateDataSourceOption(node)` | Tạo option đầy đủ icon/brush |
| `ResolveNodeTypeDisplayName(type)` | NodeType → display name |
| `ResolveNodeIconKey(type)` | NodeType → icon key |
| `ResolveTextOnNodeBrush(colorKey)` | ColorKey → `TextOnXxxBrush` |

### 7.2 Template chuẩn

```csharp
public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly YourNode _yourNode;

    // Chỉ khai báo properties ĐẶC THÙ của node này
    [ObservableProperty] private string _someProperty = string.Empty;
    [ObservableProperty] private int _someCount;

    // Chỉ khai báo collections ĐẶC THÙ (KHÔNG khai báo lại TitleDisplayModeOptions!)
    public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

    public YourNodeDialogViewModel(YourNode node, IWorkflowEditorHost host)
        : base(node, host)  // base ctor tự gọi LoadInputs, LoadOutputs, LoadReuseRoutes
    {
        _yourNode = node ?? throw new ArgumentNullException(nameof(node));

        // Sync properties từ node → VM
        SomeProperty = _yourNode.SomeProperty;
        SomeCount    = _yourNode.SomeCount;

        // Dùng base helper để load node options
        RefreshAllNodesWithOutputs(AvailableNodeOptions);

        // Subscribe PropertyChanged cho properties ĐẶC THÙ
        node.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(YourNode.SomeProperty))
                SomeProperty = _yourNode.SomeProperty;
            OnNodePropertyChanged(e.PropertyName ?? string.Empty);
        };
    }

    // BẮT BUỘC
    protected override string GetDefaultTitle() => "Your Node";

    // CHỈ override nếu cần lưu thêm properties ngoài Title/TitleDisplayMode/TitleColorMode
    protected override void OnSaveTitle()
    {
        if (_yourNode.SomeProperty != SomeProperty)
        {
            _yourNode.SomeProperty = SomeProperty;
            _host.RequestSyncDataPanels(immediate: true);
        }
        _yourNode.NotifyTitleChanged();
    }

    // CHỈ override LoadInputs nếu cần filter inputs
    // protected override void LoadInputs() { ... }
}
```

### 7.3 Khi nào cần RefreshAvailableNodes tùy chỉnh

`RefreshAllNodesWithOutputs(target)` lấy **tất cả** nodes có DynamicOutputs. Chỉ viết logic riêng khi cần filter đặc biệt:

```csharp
// Chỉ lấy nodes kết nối trực tiếp
private void RefreshDirectlyConnectedNodes(ObservableCollection<WorkflowDataSourceOption> target)
{
    target.Clear();
    var directSources = _host.ViewModel?.Connections
        .Where(c => c.ToNode == _node && c.FromNode?.DynamicOutputs?.Count > 0)
        .Select(c => c.FromNode!)
        .Distinct();
    foreach (var n in directSources ?? Enumerable.Empty<WorkflowNode>())
        target.Add(CreateDataSourceOption(n));
}
```

### 7.4 Checklist ViewModel

```yaml
- [ ] Kế thừa BaseNodeDialogViewModel
- [ ] Constructor gọi base(node, host)
- [ ] Override GetDefaultTitle() — BẮT BUỘC
- [ ] KHÔNG khai báo lại: TitleDisplayModeOptions, TitleColorOptions,
      PortPositionOptions, ConnectionLineStyleOptions
- [ ] KHÔNG tự viết: GetOutputKeysForNode, FillOutputKeys, CreateDataSourceOption,
      ResolveNodeTypeDisplayName, ResolveNodeIconKey, ResolveTextOnNodeBrush
- [ ] Nếu node không dùng ReuseRoutes: override SupportsReuseRoutes => false
- [ ] Nếu cần lưu thêm: override OnSaveTitle()
- [ ] Nếu cần filter inputs: override LoadInputs()
- [ ] Gọi _node.NotifyTitleChanged() ở cuối OnSaveTitle()
```

---

## 8. Renderer

Renderer là class thực sự đặt node lên canvas. Interface thực tế có 4 methods:

```csharp
public interface INodeRenderer
{
    void RenderNode(WorkflowNode node, Canvas canvas);
    void UpdateNodePosition(WorkflowNode node, double x, double y);
    void RemoveNode(WorkflowNode node, Canvas canvas);
    void RemoveAllNodeVisuals(Canvas canvas);
}
```

### 8.1 Template chuẩn

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class YourNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public YourNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not YourNode yourNode) return;

            // 1. Tạo border từ NodeControl
            yourNode.Border = YourNodeControl.CreateBorder(
                yourNode,
                Host as Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host);
            yourNode.Border.Tag = yourNode;

            // 2. Apply chrome (execution badge, GPU optimization)
            NodeChrome.Apply(yourNode.Border, yourNode, Host);

            // 3. Attach mouse handlers
            yourNode.Border.MouseDown  += Host.NodeMouseDown;
            yourNode.Border.MouseMove  += Host.NodeMouseMove;
            yourNode.Border.MouseUp    += Host.NodeMouseUp;
            yourNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            yourNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            // ⚠️ QUAN TRỌNG: set null vì dialog dùng right-click (WithDialogSupport)
            // Nếu set ContextMenu, WPF sẽ ưu tiên mở menu thay vì dialog
            yourNode.Border.ContextMenu = null;

            // 4. Đặt vị trí và thêm vào canvas
            Canvas.SetLeft(yourNode.Border, yourNode.X);
            Canvas.SetTop(yourNode.Border, yourNode.Y);
            canvas.Children.Add(yourNode.Border);
            Host.ZIndexManager.InitializeNodeZIndex(yourNode, yourNode.Border);

            // 5. Render ports
            foreach (var port in yourNode.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is Ellipse ellipse)
                {
                    // ⚠️ CRITICAL: ALWAYS update color
                    ellipse.Fill = new SolidColorBrush(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(yourNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(yourNode, port.PortUI);
            }
        }

        public void UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            node.X = x;
            node.Y = y;

            if (node.Border != null)
            {
                Canvas.SetLeft(node.Border, x);
                Canvas.SetTop(node.Border, y);
            }

            // ⚠️ CRITICAL: Update title TextBlock position
            if (node is YourNode yourNode && yourNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var title = yourNode.TitleTextBlockUI;
                if (!Host.WorkflowCanvas.Children.Contains(title))
                {
                    Host.WorkflowCanvas.Children.Add(title);
                    Panel.SetZIndex(title, 20000);
                }
                if (node.Border != null)
                {
                    if (title.ActualWidth == 0 || title.ActualHeight == 0)
                    {
                        title.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        title.Arrange(new Rect(title.DesiredSize));
                    }
                    Canvas.SetLeft(title, x + (node.Border.ActualWidth / 2) - (title.ActualWidth / 2));
                    Canvas.SetTop(title, y - title.ActualHeight - 4);
                }
            }

            // Update ports
            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);
                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor);
                }
                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            // ⚠️ CRITICAL: Remove title TextBlock và clear reference
            if (node is YourNode yourNode && yourNode.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(yourNode.TitleTextBlockUI))
                    canvas.Children.Remove(yourNode.TitleTextBlockUI);
                yourNode.TitleTextBlockUI = null;
            }

            if (node.Border != null && canvas.Children.Contains(node.Border))
                canvas.Children.Remove(node.Border);

            foreach (var port in node.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    canvas.Children.Remove(port.PortUI);
            }
        }

        public void RemoveAllNodeVisuals(Canvas canvas)
        {
            var borders = canvas.Children.OfType<Border>()
                .Where(b => b.Tag is WorkflowNode).ToList();
            foreach (var b in borders) canvas.Children.Remove(b);

            var ports = canvas.Children.OfType<Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18)).ToList();
            foreach (var p in ports) canvas.Children.Remove(p);
        }

        private static Color ResolvePortColor(NodePort port)
        {
            if (!string.IsNullOrWhiteSpace(port.ColorKey))
            {
                var c = GetColorFromTheme($"{port.ColorKey}Brush") ?? GetColorFromTheme(port.ColorKey);
                if (c.HasValue) return c.Value;
            }
            return port.IsInput
                ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
        }

        private static Color? GetColorFromTheme(string key)
        {
            try { return (Application.Current.TryFindResource(key) as SolidColorBrush)?.Color; }
            catch { return null; }
        }
    }
}
```

### 8.2 Đăng ký Renderer

Renderer dùng DI — phải đăng ký ở **2 chỗ**:

**Bước 1**: Thêm field + constructor parameter vào `NodeRenderer` (`Services/Rendering/_NodeRenderer.cs`):

```csharp
// Thêm field
private readonly YourNodeRenderer _yourNodeRenderer;

// Thêm vào constructor parameter list
YourNodeRenderer yourNodeRenderer,

// Thêm vào constructor body
_yourNodeRenderer = yourNodeRenderer ?? throw new ArgumentNullException(nameof(yourNodeRenderer));
```

**Bước 2**: Thêm `if` branch vào `NodeRenderer.RenderNode()` (trước đoạn fallback cuối):

```csharp
if (node is YourNode yourNode)
{
    _yourNodeRenderer.RenderNode(yourNode, canvas);
    return;
}
```

Tương tự thêm branch vào `UpdateNodePosition()` và `RemoveNode()`.

**Bước 3**: Đăng ký trong DI container (thường là `App.xaml.cs` hoặc `ServiceRegistration.cs`):

```csharp
services.AddSingleton<YourNodeRenderer>();
```

### 8.3 Checklist Renderer

```yaml
- [ ] Implement INodeRenderer (4 methods: RenderNode, UpdateNodePosition, RemoveNode, RemoveAllNodeVisuals)
- [ ] Constructor nhận PortRenderer + IWorkflowEditorHostAccessor (DI)
- [ ] RenderNode: gọi NodeChrome.Apply() sau CreateBorder()
- [ ] RenderNode: attach 5 mouse handlers (MouseDown/Move/Up/Enter/Leave)
- [ ] RenderNode: set ContextMenu = null (nếu dùng right-click dialog)
- [ ] RenderNode: gọi ZIndexManager.InitializeNodeZIndex()
- [ ] RenderNode: render ports với màu từ port.ColorKey
- [ ] UpdateNodePosition: update cả border, title TextBlock, ports
- [ ] UpdateNodePosition: gọi Host.SyncAllPortsZIndex(node) ở cuối
- [ ] RemoveNode: remove title TextBlock + set null để tránh memory leak
- [ ] RemoveNode: remove border + tất cả ports
- [ ] Đăng ký vào NodeRenderer constructor + RenderNode/UpdateNodePosition/RemoveNode branches
- [ ] Đăng ký DI container
```

---

## 9. Executor

Executor xử lý logic thực thi node khi workflow chạy. Chỉ cần tạo nếu node có logic thực thi riêng (không phải node chỉ pass-through).

### 9.1 Interface thực tế

```csharp
public interface INodeExecutor
{
    bool CanExecute(WorkflowNode node);
    Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env);
}
```

`NodeExecutionEnvironment` cung cấp:
- `env.Service` — `WorkflowExecutionService` để resolve values, get connections
- `env.Connections` — tất cả connections trong workflow
- `env.CancellationToken` — để cancel
- `env.ExecutionId` — id duy nhất của lần chạy (dùng cho scoped output)
- `env.ExecuteNextAsync(node, connection)` — chạy node tiếp theo
- `env.TraverseOutputsAsync(node)` — traverse tất cả output connections (dùng cho node thông thường)
- `env.IncomingConnection` — connection đến node này
- `env.OnNodeCompleted` / `env.OnNodeFailed` — callbacks

### 9.2 Template chuẩn

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow.NodeExecutors;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class YourNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is YourNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var yourNode = (YourNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 1. Lấy input value từ node nguồn
                var inputValue = env.Service.ResolveDynamicValueForExecution(
                    sourceNode, "outputKey", env);

                // 2. Thực thi logic
                var result = DoYourLogic(yourNode, inputValue);

                // 3. Lưu output vào node (để downstream đọc)
                yourNode.SomeOutputProperty = result;

                // 4. Publish vào scoped store (bắt buộc nếu node có DynamicOutputs)
                if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                {
                    env.Service.SetScopedNodeStringOutput(
                        env.ExecutionId, yourNode.Id, "outputKey", result);
                }
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(yourNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(yourNode, sw.Elapsed);

            // 5. Traverse sang node tiếp theo
            await env.TraverseOutputsAsync(yourNode);
        }
    }
}
```

### 9.3 Resolve input từ node nguồn

```csharp
// Lấy node nguồn qua incoming connection
var sourceNode = env.IncomingConnection?.FromNode;
if (sourceNode != null)
{
    var value = env.Service.ResolveDynamicValueForExecution(sourceNode, "outputKey", env);
}

// Hoặc lấy theo nodeId đã cấu hình trong node
var value = env.Service.ResolveValueByNodeIdAndKeyForExecution(
    env.Connections, yourNode.SourceNodeId, yourNode.SourceOutputKey, env);
```

### 9.4 Đăng ký Executor

Thêm vào list `_nodeExecutors` trong constructor của `WorkflowExecutionService` (`Services/Workflow/WorkflowExecutionService.cs`):

```csharp
_nodeExecutors = new List<NodeExecutors.INodeExecutor>
{
    // ... existing executors ...
    new NodeExecutors.YourNodeExecutor(),  // ← thêm vào đây
};
```

### 9.5 Checklist Executor

```yaml
- [ ] Implement INodeExecutor (CanExecute + ExecuteAsync)
- [ ] CanExecute: return node is YourNode
- [ ] ExecuteAsync: dùng NodeExecutionEnvironment (không phải WorkflowExecutionContext)
- [ ] Dùng env.Service.ResolveDynamicValueForExecution() để lấy input
- [ ] Gọi env.OnNodeCompleted?.Invoke() sau khi xong
- [ ] Gọi env.OnNodeFailed?.Invoke() + throw khi có lỗi
- [ ] Gọi env.TraverseOutputsAsync(node) ở cuối để đi tiếp
- [ ] Đăng ký vào _nodeExecutors list trong WorkflowExecutionService constructor
- [ ] KHÔNG tạo NodeExecutorFactory riêng — dùng list trong WorkflowExecutionService
```

---

## 10. Persistence

Thêm serialize/deserialize trong `FileWorkflowPersistenceService`:

```csharp
// SERIALIZE — trong SerializeNode(node, dict)
case NodeType.YourType:
    var yourNode = (YourNode)node;
    dict["SomeProperty"] = yourNode.SomeProperty;
    dict["SomeCount"]    = yourNode.SomeCount.ToString();
    // Title properties — dùng trực tiếp (không cần if-is chain)
    dict["TitleDisplayMode"] = node.TitleDisplayMode.ToString();
    dict["TitleColorMode"]   = node.TitleColorMode.ToString();
    if (!string.IsNullOrEmpty(node.TitleColorKey))
        dict["TitleColorKey"] = node.TitleColorKey;
    break;

// DESERIALIZE — trong DeserializeNode(type, properties)
case NodeType.YourType:
    var yourNode = new YourNode();
    if (properties.TryGetValue("SomeProperty", out var sp))
        yourNode.SomeProperty = sp?.ToString() ?? string.Empty;
    if (properties.TryGetValue("SomeCount", out var sc) &&
        int.TryParse(sc?.ToString(), out var count))
        yourNode.SomeCount = count;
    // Title properties — dùng trực tiếp
    if (properties.TryGetValue("TitleDisplayMode", out var tdm) &&
        Enum.TryParse<TitleDisplayMode>(tdm?.ToString(), out var tdmVal))
        yourNode.TitleDisplayMode = tdmVal;
    if (properties.TryGetValue("TitleColorMode", out var tcm) &&
        Enum.TryParse<TitleColorMode>(tcm?.ToString(), out var tcmVal))
        yourNode.TitleColorMode = tcmVal;
    if (properties.TryGetValue("TitleColorKey", out var tck))
        yourNode.TitleColorKey = tck?.ToString();
    return yourNode;
```

**Lưu ý**: Serialize **tất cả** properties — kể cả những gì có default value. Deserialize phải dùng `TryGetValue` để tương thích với file cũ không có key đó.

---

## 10.5 Copy/Paste

Node phải hỗ trợ copy/paste. Logic nằm trong `WorkflowEditorWindow.NodeActions.cs` — method `CreateDuplicateNodeInstance`.

Thêm một `else if` branch cho node mới vào method này:

```csharp
else if (source is YourNode srcYour && node is YourNode dstYour)
{
    // Copy tất cả properties đặc thù của node
    dstYour.SomeProperty = srcYour.SomeProperty;
    dstYour.SomeCount    = srcYour.SomeCount;

    // Copy title properties (bắt buộc cho mọi node)
    dstYour.TitleDisplayMode = srcYour.TitleDisplayMode;
    dstYour.TitleColorMode   = srcYour.TitleColorMode;
    dstYour.TitleColorKey    = srcYour.TitleColorKey;

    // Notify để renderer update UI
    dstYour.NotifyTitleChanged();
}
```

**Lưu ý**: Nếu node có properties chứa reference types (List, Dictionary, object), phải deep copy — không gán trực tiếp để tránh shared reference giữa node gốc và bản sao.

```csharp
// ❌ SAI — shared reference
dstYour.Items = srcYour.Items;

// ✅ ĐÚNG — deep copy
dstYour.Items = srcYour.Items.Select(i => new YourItem { Key = i.Key, Value = i.Value }).ToList();
```

---

## 11. Đăng ký node vào hệ thống

Có 4 chỗ cần đăng ký khi thêm node mới:

### 11.1 NodeType enum

```csharp
// Models/Nodes/NodeType.cs
public enum NodeType
{
    // ... existing types ...
    YourType,  // ← thêm vào đây
}
```

### 11.2 Palette XAML

```xml
<!-- Views/WorkflowEditorWindow.xaml — trong danh sách palette -->
<Button Content="Your Node"
        Tag="{x:Static models:NodeType.YourType}"
        Style="{StaticResource PaletteNodeButton}"
        Click="PaletteNode_Click"/>
```

### 11.3 TemplateFactory

```csharp
// Services/Workflow/NodeTemplateFactory.cs
public WorkflowNode CreateNode(NodeType type) => type switch
{
    // ... existing cases ...
    NodeType.YourType => new YourNode(),
    _ => throw new ArgumentOutOfRangeException(nameof(type))
};
```

### 11.4 NodeRendererFactory

```csharp
// Services/Rendering/NodeRendererFactory.cs
private static readonly INodeRenderer[] _renderers = new INodeRenderer[]
{
    // ... existing renderers ...
    new YourNodeRenderer(),
};
```

### 11.5 NodeExecutorFactory (nếu có executor)

```csharp
// Services/Workflow/NodeExecutorFactory.cs
private static readonly INodeExecutor[] _executors = new INodeExecutor[]
{
    // ... existing executors ...
    new YourNodeExecutor(),
};
```

---

## 12. Các trường hợp đặc biệt

### 12.1 Node có embedded title (không dùng floating canvas title)

Ví dụ: `ScreenCaptureNodeControl` — title nằm trong border, không phải TextBlock riêng trên Canvas.

```csharp
// Dùng dummy TextBlock thay vì title thật
var dummyTitle = new TextBlock { Visibility = Visibility.Collapsed, IsHitTestVisible = false };

BaseNodeControlHelper
    .Initialize(border, dummyTitle, node, host)
    .WithHoverBehavior()
    .WithKeyboardPorts()
    .WithPropertySync(customPropertyHandlers)
    .WithDialogSupport(ctx => new YourNodeDialog(node, host, ownerWindow))
    .WithCleanup()
    // ⚠️ KHÔNG gọi WithTitleManagement(), WithVisibilitySync(), WithCanvasIntegration()
    .Build();
```

### 12.2 Node cần cleanup thêm (WebView2, subscriptions)

```csharp
BaseNodeControlHelper
    .Initialize(border, titleTextBlock, node, host)
    // ... fluent chain ...
    .WithCleanup()  // BaseNodeControlHelper cleanup chạy trước
    .Build();

// Cleanup bổ sung sau Build()
border.Unloaded += (s, e) =>
{
    webView?.Dispose();
    // unsubscribe other events...
};
```

### 12.3 Node có resize handles

Giữ resize handle logic trong NodeControl class — đây là logic đặc thù của node.

```csharp
// Sau Build(), thêm resize logic
AttachResizeHandles(border, node, host);
```

### 12.4 Node diamond/conditional (transparent background)

Diamond cần transparent background đặc biệt — không dùng `WithHoverBehavior()` vì nó sẽ set background khi hover.

```csharp
BaseNodeControlHelper
    .Initialize(border, titleTextBlock, node, host)
    .WithDialogSupport(ctx => new ConditionalNodeDialog(node, host, ownerWindow))
    .WithCleanup()
    .WithCanvasIntegration()
    .Build();
// Hover + keyboard được xử lý thủ công để giữ transparent background
```

### 12.5 Dialog không có Inputs/Outputs

```csharp
protected override Panel? GetInputsPanel() => null;
protected override Panel? GetOutputsPanel() => null;
// Base tự bỏ qua khi trả về null
```

### 12.6 Node không dùng ReuseRoutes

```csharp
// Trong ViewModel — chỉ cần 1 dòng này
protected override bool SupportsReuseRoutes => false;
// KHÔNG cần override LoadReuseRoutes()
```

### 12.7 Files KHÔNG dùng BaseNodeControlHelper

Các file sau là container/content controls — **không migrate**, giữ nguyên:

| File | Lý do |
|------|-------|
| `BodyContainerControl.cs` | Container cho loop/conditional body |
| `LoopContainerControl.cs` | Container cho loop body |
| `ImageProcessingNodeContentControl.xaml.cs` | XAML UserControl code-behind |
| `VideoProcessingNodeContentControl.cs` | Content control |

---

## 13. Lỗi thường gặp

### NodeControl

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| 1 | `ArgumentException` khi đổi workflow/theme | `DependencyPropertyDescriptor.AddValueChanged` không được remove | Dùng `.WithVisibilitySync()` — tự track và remove handler |
| 2 | Title không hiện sau load workflow | Quên `.WithTitleManagement()` hoặc `.WithCanvasIntegration()` | Luôn gọi cả hai nếu node có floating title |
| 3 | Memory leak sau nhiều lần đổi workflow | Quên `.WithCleanup()` | `.WithCleanup()` là bắt buộc — không bao giờ bỏ qua |
| 4 | Icon fill không cập nhật khi đổi ColorKey | Không khai báo `ColorKey` handler trong `customPropertyHandlers` | Luôn thêm `[nameof(WorkflowNode.ColorKey)] = ctx => { iconSvg.Fill = ... }` |
| 5 | Port position không đổi khi nhấn arrow key | Quên `.WithKeyboardPorts()` | `.WithHoverBehavior()` tự set `Focusable=true`; `.WithKeyboardPorts()` đăng ký handler |
| 6 | `node.TitleTextBlockUI` là null sau render | Quên `node.TitleTextBlockUI = titleTextBlock` trước `Build()` | Gán trước khi gọi `Initialize` |
| 7 | Dialog mở nhưng node vẫn bị drag | Không gọi `.WithDialogSupport()` đúng cách | `WithDialogSupport` tự xử lý `ReleaseMouseCapture`, `DraggedNode = null` |

### Dialog Code-behind

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| D1 | Title color preview không cập nhật khi mở | Quên gọi `UpdateTitleColorPreview()` trong constructor | Gọi sau `InitializeBase()` |
| D2 | Title color preview không cập nhật khi đổi combobox | XAML không wire `SelectionChanged="TitleColorComboBox_SelectionChanged"` | Thêm vào XAML |
| D3 | `TitleColorPreview` không tìm thấy | `x:Name` sai hoặc nằm trong DataTemplate | Đặt đúng `x:Name="TitleColorPreview"` ở cấp trực tiếp |
| D4 | Inputs/Outputs không load | `GetInputsPanel()` trả về null hoặc sai panel | Trả về đúng `StackPanel` có `x:Name="InputsPanel"` |
| D5 | Binding mất khi đóng bằng Alt+F4 | Chỉ override `CloseButton_Click`, không override `BeforeSaveOnClose` | Override `BeforeSaveOnClose()` |
| D6 | Dialog không snap vào cạnh phải màn hình | Tự set `WindowStartupLocation` sau `InitializeBase` | Không set — base đã set `Manual` |

### ViewModel

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| V1 | `TitleDisplayModeOptions` binding không hoạt động | Khai báo lại local với tên khác | Xóa local copy — base đã có |
| V2 | `GetOutputKeysForNode` không tìm thấy | Tự viết lại với `private` | Dùng `protected` method từ base |
| V3 | `CreateDataSourceOption` tạo option thiếu icon/brush | Tự tạo `new WorkflowDataSourceOption { NodeId, Title }` | Dùng `CreateDataSourceOption(node)` từ base |
| V4 | `LoadReuseRoutes()` override thừa | Override cả `SupportsReuseRoutes => false` lẫn `LoadReuseRoutes()` | Chỉ cần `SupportsReuseRoutes => false` |
| V5 | Inputs không load khi mở dialog | Quên gọi `base(node, host)` | Base ctor tự gọi `LoadInputs()` |

### Node Model

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| M1 | Compiler error: ambiguous `PropertyChanged` | Derived class khai báo lại event | Xóa — base đã có |
| M2 | Compiler error: ambiguous `OnPropertyChanged` | Derived class khai báo lại method | Xóa — base đã có |
| M3 | `TitleDisplayMode` luôn là `Always` dù muốn `Hidden` | Quên set trong constructor | Thêm `TitleDisplayMode = TitleDisplayMode.Hidden;` trong constructor |
| M4 | `if node is XxxNode` chain trong service | Không biết base đã có property | Dùng `node.TitleDisplayMode` trực tiếp |
| M5 | `if (node is INotifyPropertyChanged npc)` | Không biết WorkflowNode đã implement | Dùng `node.PropertyChanged +=` trực tiếp |

---

*Tài liệu này được tạo từ codebase thực tế sau khi hoàn thành refactor 38 NodeControl classes. Xem `Views/NodeControls/OutputNodeControl.cs` và `Views/NodeControls/StorageNodeControl.cs` làm ví dụ tham khảo.*
