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
    - [11.A IconKey / ColorKey — 4 chỗ phải khớp](#11a-iconkey--colorkey--4-chỗ-phải-khớp-nhau)
    - [11.B Theme System — DynamicResource bắt buộc](#11b-theme-system--dynamicresource-bắt-buộc)
    - [11.C ExecutionId & Scoped Outputs](#11c-executionid--scoped-outputs--bắt-buộc-khi-viết-executor)
12. [Các trường hợp đặc biệt](#12-các-trường-hợp-đặc-biệt)
13. [Lỗi thường gặp và cách tránh](#13-lỗi-thường-gặp)
14. [Reference Implementations](#14-reference-implementations)
15. [Dynamic Input/Output — Node cho phép user thêm/xóa](#15-dynamic-inputoutput--node-cho-phép-user-thêmxóa-inputs-và-outputs)
16. [Multi-row NodeSearchComboBox trong ItemsControl](#16-multi-row-nodesearchcombobox-trong-itemscontrol--tránh-lỗi-đồng-bộ)
17. [Liquid Glass — Hỗ trợ giao diện Kính Lỏng](#17-liquid-glass--hỗ-trợ-giao-diện-kính-lỏng)

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
    // ✅ KHÔNG thêm INotifyPropertyChanged — WorkflowNode đã implement
    // ✅ KHÔNG khai báo lại: PropertyChanged, OnPropertyChanged,
    //    TitleDisplayMode, TitleColorMode, TitleColorKey, NotifyTitleChanged
    public sealed class YourNode : WorkflowNode
    {
        private string _someProperty = string.Empty;
        private int _someCount;

        public YourNode()
        {
            Type = NodeType.YourType;   // thêm vào Models/Nodes/NodeType.cs
            Title = "Your Node";

            // Nếu muốn default TitleDisplayMode khác Always:
            TitleDisplayMode = TitleDisplayMode.Hidden;

            // Thêm ports
            Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = true,  Position = PortPosition.Left,  IsVisible = true, ColorKey = "Info" });
            Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = false, Position = PortPosition.Right, IsVisible = true, ColorKey = "SunsetOrange" });
        }

        // ✅ Properties đặc thù — dùng OnPropertyChanged() từ base
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

        // ✅ CHỈ override nếu cần thêm logic sau khi title thay đổi
        // public override void NotifyTitleChanged() { base.NotifyTitleChanged(); /* extra */ }

        // ❌ KHÔNG khai báo: PropertyChanged event
        // ❌ KHÔNG khai báo: OnPropertyChanged method
        // ❌ KHÔNG khai báo: TitleDisplayMode property
        // ❌ KHÔNG khai báo: TitleColorMode property
        // ❌ KHÔNG khai báo: TitleColorKey property
        // ❌ KHÔNG khai báo: NotifyTitleChanged() nếu chỉ gọi OnPropertyChanged(nameof(Title))
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
- [ ] KHÔNG thêm ports trong constructor (TemplateFactory sẽ tạo — tránh duplicate ports)
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

**Tham khảo:** `Views/Overlays/ScreenCaptureNodeDialog.xaml` (đã chuẩn hóa)

### 5.1 Template chuẩn

```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="clr-namespace:FlowMy.Controls"
    xmlns:local="clr-namespace:FlowMy.Views.Overlays"
    Title="Your Node"
    WindowStyle="None" ResizeMode="CanResize"
    AllowsTransparency="True" Background="Transparent"
    ShowInTaskbar="False" Topmost="True"
    Width="520" MinWidth="420" MinHeight="420">

    <Border CornerRadius="12" Padding="0" Style="{DynamicResource DialogOuterBorder}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- HEADER -->
            <Border Grid.Row="0" Style="{DynamicResource DialogHeaderBorder}" Padding="16,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBox x:Name="TitleTextBox" Grid.Column="0"
                             Text="{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}"
                             Style="{DynamicResource BaseTextBoxV2}" FontSize="16"
                             Padding="0,4" VerticalContentAlignment="Center" Cursor="IBeam"/>
                    <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                        <Button Width="24" Height="24" Content="▶" FontSize="12" Padding="0" Margin="8,0,0,0"
                                Style="{DynamicResource PrimaryButton}" Cursor="Hand" ToolTip="Chạy logic node này"
                                Command="{Binding RunSingleNodeCommand}"/>
                        <Button x:Name="CloseButton" Width="24" Height="24" Padding="0" Content="✕" FontSize="12"
                                FontWeight="Bold" Margin="8,0,0,0" Cursor="Hand"
                                Style="{DynamicResource DangerButton}" Click="CloseButton_Click"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- CONTENT -->
            <TabControl Grid.Row="1" Background="Transparent" BorderThickness="0">

                <!-- TAB: LOGIC -->
                <TabItem Header="Logic" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>

                            <!-- 🎨 Cấu hình hiển thị -->
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,12">
                                <StackPanel>
                                    <TextBlock Text="🎨 Cấu hình hiển thị" Foreground="{DynamicResource TextBrush}"
                                               FontSize="12" FontWeight="SemiBold" Margin="0,0,0,10"/>
                                    <TextBlock Text="Hiển thị tiêu đề" Foreground="{DynamicResource TextMuted}"
                                               FontSize="10" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}" Margin="0,0,0,8"
                                              ItemsSource="{Binding TitleDisplayModeOptions}"
                                              SelectedValuePath="Value" DisplayMemberPath="DisplayName"
                                              SelectedValue="{Binding TitleDisplayMode, Mode=TwoWay}"/>
                                    <TextBlock Text="Màu tiêu đề" Foreground="{DynamicResource TextMuted}"
                                               FontSize="10" Margin="0,0,0,4"/>
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>
                                        <ComboBox x:Name="TitleColorComboBox" Grid.Column="0" Height="32"
                                                  Style="{DynamicResource BaseComboBox}"
                                                  ItemsSource="{Binding TitleColorOptions}"
                                                  SelectedValuePath="Key" DisplayMemberPath="DisplayName"
                                                  SelectedValue="{Binding TitleColorKey, Mode=TwoWay}"
                                                  SelectionChanged="TitleColorComboBox_SelectionChanged"/>
                                        <Border x:Name="TitleColorPreview" Grid.Column="1"
                                                Width="32" Height="32" CornerRadius="6" Margin="8,0,0,0"
                                                BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1"/>
                                    </Grid>
                                </StackPanel>
                            </Border>

                            <!-- Properties đặc thù của node ở đây -->
                            <!-- Sử dụng Border với emoji icon cho mỗi section quan trọng -->

                            <!-- 📍 Input từ node khác -->
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,12">
                                <StackPanel>
                                    <TextBlock Text="📍 Input — [Tên input]" Foreground="{DynamicResource TextBrush}"
                                               FontSize="11" FontWeight="SemiBold" Margin="0,0,0,6"/>
                                    <TextBlock Foreground="{DynamicResource TextMuted}" FontSize="10"
                                               TextWrapping="Wrap" Margin="0,0,0,10">
                                        <Run Text="[Mô tả ngắn gọn]"/>
                                    </TextBlock>
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="8"/>
                                            <ColumnDefinition Width="*"/>
                                        </Grid.ColumnDefinitions>
                                        <StackPanel Grid.Column="0">
                                            <TextBlock Text="Node" Foreground="{DynamicResource TextMuted}"
                                                       FontSize="10" Margin="0,0,0,4"/>
                                            <controls:NodeSearchComboBoxUserControl Height="32"
                                                      ItemsSource="{Binding AvailableNodeOptions}"
                                                      SelectedValuePath="NodeId" DisplayMemberPath="Title"
                                                      SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"/>
                                        </StackPanel>
                                        <StackPanel Grid.Column="2">
                                            <TextBlock Text="Key" Foreground="{DynamicResource TextMuted}"
                                                       FontSize="10" Margin="0,0,0,4"/>
                                            <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                                      ItemsSource="{Binding KeyOptions}"
                                                      SelectedValuePath="Key" DisplayMemberPath="Key"
                                                      SelectedValue="{Binding SelectedKey, Mode=TwoWay}"/>
                                        </StackPanel>
                                    </Grid>
                                </StackPanel>
                            </Border>

                            <!-- Outputs -->
                            <TextBlock Text="Outputs" Foreground="{DynamicResource TextBrush}"
                                       FontSize="12" FontWeight="SemiBold" Margin="0,0,0,6"/>
                            <Border Style="{DynamicResource DialogOuterBorder}" CornerRadius="8" Padding="10">
                                <StackPanel x:Name="OutputsPanel"/>
                            </Border>

                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

                <!-- TAB: CẤU HÌNH -->
                <TabItem Header="Cấu hình" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>

                            <TextBlock Text="Vị trí cổng IN/OUT" Foreground="{DynamicResource TextBrush}"
                                       FontSize="12" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Grid Margin="0,0,0,16">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" Margin="0,0,6,0">
                                    <TextBlock Text="Port IN" Foreground="{DynamicResource TextMuted}"
                                               FontSize="10" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding InputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                                <StackPanel Grid.Column="1" Margin="6,0,0,0">
                                    <TextBlock Text="Port OUT" Foreground="{DynamicResource TextMuted}"
                                               FontSize="10" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding OutputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                            </Grid>

                            <TextBlock Text="Tái sử dụng flow" Foreground="{DynamicResource TextBrush}"
                                       FontSize="12" FontWeight="SemiBold" Margin="0,0,0,8"/>
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
                                                    <TextBlock Text="Node IN" Foreground="{DynamicResource TextBrush}"
                                                               FontSize="10" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <TextBlock Text="{Binding IncomingNodeTitle}"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="11" FontWeight="SemiBold"
                                                               TextTrimming="CharacterEllipsis"/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="1">
                                                    <TextBlock Text="Node OUT" Foreground="{DynamicResource TextBrush}"
                                                               FontSize="10" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <controls:NodeSearchComboBoxUserControl Height="32"
                                                              ItemsSource="{Binding OutgoingOptions}"
                                                              SelectedValuePath="NodeId" DisplayMemberPath="Title"
                                                              SelectedValue="{Binding SelectedOutgoingNodeId, Mode=TwoWay}"/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="2" Margin="10,0,0,0">
                                                    <TextBlock Text="Kiểu line OUT" Foreground="{DynamicResource TextBrush}"
                                                               FontSize="10" Opacity="0.7" Margin="0,0,0,4"/>
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

### 5.2 Chuẩn UI (từ UI_Dialog_Standardization_Guide.md)

| Thuộc tính | Giá trị chuẩn |
|-----------|---------------|
| Window Width | 520px |
| Window MinWidth | 420px |
| Window MinHeight | 420px |
| Header Padding | 16,12 (không phải 16,12,12,12) |
| Title FontSize | 16px |
| Play/Close Button | 24x24, Padding="0" |
| Section Header FontSize | 12px bold (với emoji icon) |
| Subsection Header FontSize | 11px bold |
| Label FontSize | 10px với TextMuted color |
| TextBox/ComboBox Height | 32px (không phải 36px) |
| Color Preview Size | 32x32 (không phải 36x36) |
| Border Padding | 12px |
| Border CornerRadius | 8px (section), 6px (hint box) |
| Section Margin | 0,0,0,12 |
| Element Margin | 0,0,0,8 hoặc 0,0,0,10 |
| Label-Control Margin | 0,0,0,4 hoặc 0,0,0,6 |
| Grid Column Gap | 6-12px |

### 5.3 Icon Emoji cho Sections

- 🎨 Cấu hình hiển thị
- 📍 Input từ node khác
- 🎯 Manual input/selection
- ⚙️ Technical settings
- 💾 Storage/Save settings
- 🔄 Loop/Repeat settings
- 🌐 Network/HTTP settings
- 📊 Data/Output settings

### 5.4 x:Name bắt buộc (base dùng `FindName`)

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

Có **5 chỗ** bắt buộc khi thêm node mới. Thiếu bất kỳ chỗ nào sẽ gây fallback icon/màu sai hoặc node không tạo được.

### 11.1 NodeType enum

```csharp
// Models/Nodes/NodeType.cs
public enum NodeType
{
    // ... existing types ...
    YourType,  // ← thêm vào đây
}
```

### 11.2 Palette XAML — `Views/WorkflowEditorWindow.xaml`

Thêm vào `NodeTemplatesPanel` (trong `WrapPanel` của nhóm phù hợp). **Mỗi node palette phải có cả `ToolTip` lẫn `ContextMenu`** — thiếu `ContextMenu` thì chuột phải vào icon không hiện thông tin:

```xml
<Border Style="{StaticResource PaletteIconNodeStyle}"
        Background="{DynamicResource ForestPineBrush}"
        Tag="YourNodeTypeName">

    <!-- ToolTip: hiện khi hover — title in đậm/nghiêng + gạch đầu dòng -->
    <Border.ToolTip>
        <ToolTip>
            <StackPanel MaxWidth="240">
                <!-- Title: in đậm + in nghiêng -->
                <TextBlock FontWeight="Bold" FontStyle="Italic">
                    <Run Text="Tên Node"/>
                </TextBlock>
                <!-- Mô tả ngắn (1 dòng) -->
                <TextBlock Text="Mô tả ngắn gọn về chức năng."
                           TextWrapping="Wrap" Margin="0,4,0,0" Opacity="0.9"/>
                <!-- Gạch đầu dòng: mỗi tính năng / output là 1 dòng -->
                <TextBlock Margin="0,6,0,0">
                    <Run Text="• "/>
                    <Run Text="Tính năng / output 1" FontWeight="SemiBold"/>
                </TextBlock>
                <TextBlock>
                    <Run Text="• "/>
                    <Run Text="Tính năng / output 2"/>
                </TextBlock>
                <TextBlock>
                    <Run Text="• "/>
                    <Run Text="Tính năng / output 3"/>
                </TextBlock>
            </StackPanel>
        </ToolTip>
    </Border.ToolTip>

    <!-- ContextMenu: hiện khi chuột phải (chi tiết hơn) — BẮT BUỘC -->
    <Border.ContextMenu>
        <ContextMenu Placement="MousePoint" StaysOpen="False">
            <MenuItem IsHitTestVisible="False">
                <MenuItem.Header>
                    <Border Background="{DynamicResource ForestPineBrush}"
                            CornerRadius="10" Padding="10"
                            BorderBrush="{DynamicResource BorderColor}" BorderThickness="1">
                        <StackPanel>
                            <TextBlock Text="Tên Node"
                                       Foreground="{DynamicResource TextOnForestPineBrush}"
                                       FontWeight="Bold" FontSize="13"/>
                            <TextBlock Text="Mô tả chi tiết hơn tooltip."
                                       Foreground="{DynamicResource TextOnForestPineBrush}"
                                       Opacity="0.9" TextWrapping="Wrap" Margin="0,4,0,0"/>
                            <!-- Gạch đầu dòng cho outputs / tính năng quan trọng -->
                            <TextBlock Text="• Tính năng / output 1"
                                       Foreground="{DynamicResource TextOnForestPineBrush}"
                                       Opacity="0.85" Margin="0,4,0,0"/>
                            <TextBlock Text="• Tính năng / output 2"
                                       Foreground="{DynamicResource TextOnForestPineBrush}"
                                       Opacity="0.85"/>
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
                                       ConverterParameter='your-icon-key duotone-regular'}"
                              Fill="{DynamicResource TextOnForestPineBrush}"/>
    </Grid>
</Border>
```

**Quy tắc format ToolTip:**

| Phần | Format | Ghi chú |
|------|--------|---------|
| Title | `FontWeight="Bold" FontStyle="Italic"` | Dùng `<Run Text="..."/>` bên trong `<TextBlock>` |
| Mô tả ngắn | `TextWrapping="Wrap" Opacity="0.9"` | 1 câu, không quá 2 dòng |
| Gạch đầu dòng | `<Run Text="• "/>` + `<Run Text="..."/>` | Mỗi output/tính năng 1 dòng; key quan trọng dùng `FontWeight="SemiBold"` |
| MaxWidth | `240` | Đủ rộng cho 2–3 từ/dòng |

> ⚠️ **Bắt buộc**: Mọi node trong palette phải có **cả `ToolTip` lẫn `ContextMenu`**.
> - `ToolTip`: hiện khi hover — title in đậm/nghiêng + gạch đầu dòng tính năng/output
> - `ContextMenu`: hiện khi chuột phải — mô tả chi tiết hơn, có thể thêm bullet points
> - Thiếu `ContextMenu` → chuột phải vào icon không hiện gì (lỗi UX)
> - Thiếu format title → tooltip trông như plain text, khó đọc
>
> `Tag` phải là **string** khớp chính xác với switch case trong TemplateFactory.
> Thay `ForestPine` bằng ColorKey thực của node.

### 11.3 TemplateFactory — `Services/Workflow/TemplateFactory.cs`

```csharp
// Thêm vào switch trong Create(string nodeType, double x, double y):
"YourNodeTypeName" => CreateYourNode(x, y),

// Thêm method:
private static WorkflowNode CreateYourNode(double x, double y)
{
    var node = new YourNode
    {
        Id = Guid.NewGuid().ToString(),
        X = x, Y = y,
        ColorKey = "ForestPine",  // ⚠️ Khớp với palette Background
        NodeBrush = Application.Current.TryFindResource("ForestPineBrush") as Brush ?? Brushes.Green
    };

    node.Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = true,
        Position = PortPosition.Left, IsVisible = true, ColorKey = "Info" });
    node.Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = false,
        Position = PortPosition.Right, IsVisible = true, ColorKey = "SunsetOrange" });

    return node;
}
```

### 11.4 Icon trên canvas — `Views/WorkflowEditors/WorkflowEditorWindow.TemplateNodeHandler.cs`

```csharp
// Trong GetIconNameForNodeType(string nodeType):
"YourNodeTypeName" => "your-icon-key duotone-regular",
```

### 11.5 Icon trong Execution Trace — `ViewModels/WorkflowEditorViewModel.cs`

```csharp
// Trong ResolveNodeIconKey(NodeType type):
NodeType.YourType => "your-icon-key duotone-regular",
```

### 11.6 Đăng ký Renderer — `Services/Rendering/_NodeRenderer.cs`

```csharp
// Thêm field
private readonly YourNodeRenderer _yourNodeRenderer;

// Thêm vào constructor parameter list + body
_yourNodeRenderer = yourNodeRenderer ?? throw new ArgumentNullException(nameof(yourNodeRenderer));

// Thêm if branch vào RenderNode() (trước fallback cuối):
if (node is YourNode yourNode)
{
    _yourNodeRenderer.RenderNode(yourNode, canvas);
    return;
}
// Tương tự cho UpdateNodePosition() và RemoveNode()
```

Đăng ký DI (thường `App.xaml.cs` hoặc `ServiceRegistration.cs`):
```csharp
services.AddSingleton<YourNodeRenderer>();
```

### 11.7 Đăng ký Executor (nếu có) — `Services/Workflow/WorkflowExecutionService.cs`

```csharp
// Thêm vào _nodeExecutors list trong constructor:
new NodeExecutors.YourNodeExecutor(),
```

### 11.8 Copy/Paste — `Services/Interaction/WorkflowEditorEventService.cs`

```csharp
// Ctrl+C: thêm YourNode vào điều kiện copy
if (vm.SelectedNode is YourNode || vm.SelectedNode is InputNode || ...)
{ _copiedNode = vm.SelectedNode; e.Handled = true; return; }

// Ctrl+V: thêm tương tự cho paste
```

### 11.9 Copy/Paste properties — `Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs`

```csharp
// Trong CreateDuplicateNodeInstance():
else if (source is YourNode srcYour && node is YourNode dstYour)
{
    dstYour.SomeProperty    = srcYour.SomeProperty;
    dstYour.SomeCount       = srcYour.SomeCount;
    // Title properties (bắt buộc cho mọi node)
    dstYour.TitleDisplayMode = srcYour.TitleDisplayMode;
    dstYour.TitleColorMode   = srcYour.TitleColorMode;
    dstYour.TitleColorKey    = srcYour.TitleColorKey;
    // ⚠️ Clone lists — KHÔNG gán trực tiếp reference
    dstYour.Items = srcYour.Items.Select(i => new YourItem { Key = i.Key, Value = i.Value }).ToList();
    dstYour.NotifyTitleChanged();
}
```

### 11.10 Remap NodeId sau multi-paste — `Views/WorkflowEditors/WorkflowEditorWindow.MultiNodeClipboard.cs`

```csharp
// Trong RemapNodeReferenceIds(node, sourceToNewNodeMap):
case YourNode yourNode:
    yourNode.SourceNodeId = RemapNodeId(yourNode.SourceNodeId, sourceToNewNodeMap);
    foreach (var m in yourNode.InputMappings ?? new())
        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);
    break;
```

> ⚠️ Mọi field dạng `*NodeId` phải remap. Không remap → combobox chọn sai node sau paste.
> Quy ước: dùng hậu tố `NodeId` cho mọi field giữ Id của node khác.

### 11.11 Checklist đăng ký

```yaml
- [ ] NodeType enum value thêm vào NodeType.cs
- [ ] Palette XAML: Border + Tag + ColorKey + iconKey + TextOnColorKeyBrush + **cả ToolTip lẫn ContextMenu**
- [ ] TemplateFactory: string switch case + CreateYourNode()
- [ ] WorkflowEditorWindow.TemplateNodeHandler.cs: GetIconNameForNodeType
- [ ] WorkflowEditorViewModel.cs: ResolveNodeIconKey
- [ ] BaseNodeDialogViewModel.cs: ResolveNodeIconKey method (cho icon hiển thị trong dialog)
- [ ] NodeSearchComboBoxUserControl.xaml.cs: ResolveIconKey method (cho icon hiển thị trong NodeSearchComboBox)
- [ ] _NodeRenderer.cs: field + constructor + if branch trong 3 methods
- [ ] DI container: services.AddSingleton<YourNodeRenderer>()
- [ ] WorkflowExecutionService: thêm executor vào _nodeExecutors (nếu có)
- [ ] WorkflowEditorEventService.cs: Ctrl+C + Ctrl+V
- [ ] WorkflowEditorWindow.NodeActions.cs: CreateDuplicateNodeInstance
- [ ] WorkflowEditorWindow.MultiNodeClipboard.cs: RemapNodeReferenceIds (nếu có *NodeId fields)
```

---

## 11.A IconKey / ColorKey — 6 chỗ phải khớp nhau

> Thiếu bất kỳ chỗ nào → node bị fallback icon `circle-question`, nền `AccentBrush`, icon màu sai.

| # | File | Khai báo gì |
|---|------|-------------|
| 1 | `Views/WorkflowEditorWindow.xaml` | `Tag="NodeTypeName"` + `Background="{DynamicResource <ColorKey>Brush}"` + `ConverterParameter='<iconKey>'` + `Fill="{DynamicResource TextOn<ColorKey>Brush}"` |
| 2 | `Views/WorkflowEditors/WorkflowEditorWindow.TemplateNodeHandler.cs` | `case "NodeTypeName" => "<iconKey>"` trong `GetIconNameForNodeType` |
| 3 | `Services/Workflow/TemplateFactory.cs` | `ColorKey = "<ColorKey>"` trong `CreateYourNode()` |
| 4 | `ViewModels/WorkflowEditorViewModel.cs` | `NodeType.YourType => "<iconKey>"` trong `ResolveNodeIconKey` |
| 5 | `Controls/NodeSearchComboBoxUserControl.xaml.cs` | `"YourNodeType" => "<iconKey>"` trong `ResolveIconKey` method |
| 6 | `ViewModels/BaseNodeDialogViewModel.cs` | `NodeType.YourType => "<iconKey>"` trong `ResolveNodeIconKey` method |

**Quy tắc ColorKey:**
- Nền node/palette: `{DynamicResource <ColorKey>Brush}` — brush phải có trong `Themes/Base/Colors/Common.xaml`
- Màu chữ/icon: `{DynamicResource TextOn<ColorKey>Brush}` — **KHÔNG hardcode Black/White**
- Nếu ColorKey mới chưa có: thêm `<SolidColorBrush x:Key="NeonLimeBrush" Color="#C6FF00"/>` và `<SolidColorBrush x:Key="TextOnNeonLimeBrush" Color="#1F2937"/>` vào `Common.xaml`

---

## 11.B Theme System — DynamicResource bắt buộc

**KHÔNG hardcode màu trong XAML dialog.** Dùng DynamicResource để dialog tự đổi màu theo theme:

| Resource Key | Dùng cho | Thay cho |
|---|---|---|
| `{DynamicResource DialogOuterBorder}` | Style outer border dialog | `Background="#FF1E293B"` |
| `{DynamicResource DialogHeaderBorder}` | Style header dialog | `Background="#FF0F172A"` |
| `{DynamicResource TextBrush}` | Foreground text chính | `Foreground="White"` |
| `{DynamicResource TextSecondary}` | Text phụ / mô tả | `Foreground="#CCCCCC"` |
| `{DynamicResource WindowBackground}` | Background card/panel | `Background="#FF1E293B"` |
| `{DynamicResource ControlBorderBrush}` | BorderBrush controls | `BorderBrush="#33FFFFFF"` |
| `{DynamicResource BaseTextBoxV2}` | Style TextBox | — |
| `{DynamicResource BaseComboBox}` | Style ComboBox | — |
| `{DynamicResource PrimaryButton}` | Style nút Play | — |
| `{DynamicResource DangerButton}` | Style nút Close | — |
| `{StaticResource HttpTabItemStyle}` | Style TabItem | — (**StaticResource**, không phải Dynamic) |

**Dialog sizing:**
```xml
<local:BaseNodeDialog Width="460" MinWidth="350" MaxWidth="900" MinHeight="350">
<!-- ⚠️ KHÔNG đặt Height cứng — NodeDialogManager auto-size 90% screen -->
<!-- ⚠️ KHÔNG đặt MaxHeight cố định -->
```

---

## 11.C ExecutionId & Scoped Outputs — bắt buộc khi viết Executor

Nhiều workflow có thể chạy **đồng thời**. Nếu executor đọc output node khác qua `NodeDataPanelService`, nó sẽ đọc nhầm kết quả của run khác.

| API | Dùng khi nào |
|-----|-------------|
| `env.Service.ResolveDynamicValueForExecution(sourceNode, key, env)` | Đọc output node khác trong executor |
| `env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, nodeId, key, env)` | Đọc theo nodeId + key |
| `env.TraverseOutputsAsync(node)` | Chuyển sang node tiếp theo — **LUÔN gọi ở cuối** |
| `MirrorRuntimeOutputsToScopedStore(node, executionId)` | Tự động gọi bởi service sau executor — không cần gọi thủ công |
| `PublishStorageOutputsToScoped(storage, executionId)` | StorageNode: gọi **trước** `TraverseOutputsAsync` |
| `IWorkflowScopedOutputContributor` | Implement trên node model nếu output không nằm trong switch mirror |

**Quy tắc:**
- Trong Executor: **LUÔN** dùng `*ForExecution` APIs có `env`
- **KHÔNG** dùng `NodeDataPanelService.ResolveDynamicValueByKey` trong executor
- Output mới sau khi node chạy → đảm bảo có trong `MirrorRuntimeOutputsToScopedStore` hoặc `IWorkflowScopedOutputContributor`

**Checklist nhanh khi thêm Executor:**
```yaml
- [ ] Mọi chỗ đọc output node khác → dùng ResolveDynamicValueForExecution (có env)
- [ ] Gọi env.TraverseOutputsAsync(node) ở cuối ExecuteAsync
- [ ] Gọi env.OnNodeCompleted?.Invoke() sau khi xong
- [ ] Gọi env.OnNodeFailed?.Invoke() + throw khi có lỗi
- [ ] Nếu node có output chuỗi mới → đảm bảo có trong MirrorRuntimeOutputsToScopedStore
- [ ] Nếu là StorageNode → PublishStorageOutputsToScoped trước TraverseOutputsAsync
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

## 14. Reference Implementations

Xem các file sau làm mẫu thực tế:

| File | Đặc điểm |
|------|----------|
| `Views/NodeControls/OutputNodeControl.cs` | Standard pattern, 1 custom handler (ColorKey) |
| `Views/NodeControls/StorageNodeControl.cs` | 2 custom handlers (ColorKey + IsInputMode) |
| `Views/NodeControls/MouseEventNodeControl.cs` | 2 custom handlers (ColorKey + MouseButton → đổi icon) |
| `Views/NodeControls/ScreenCaptureNodeControl.cs` | Embedded title, partial fluent API (dummy title) |
| `Views/NodeControls/VideoProcessingNodeControl.cs` | Resizable border, extra Unloaded cleanup |
| `Views/NodeControls/WebNodeControl.cs` | Extra Unloaded cleanup (WebView2 dispose) |
| `Views/Overlays/DelayNodeDialog.xaml` | Dialog mẫu chuẩn theme + responsive |
| `Views/Overlays/AssignDataNodeDialog.xaml` | Dialog với custom property handlers |
| `Views/Overlays/CodeNodeDialog.xaml` | Dynamic Input mapping + Dynamic Output keys |
| `ViewModels/DelayNodeDialogViewModel.cs` | ViewModel chuẩn với OnSaveTitle |
| `ViewModels/StorageNodeDialogViewModel.cs` | ViewModel với filter đặc biệt |
| `Services/Workflow/NodeExecutors/StorageNodeExecutor.cs` | Executor mẫu với scoped output |
| `Services/Workflow/NodeExecutors/DelayNodeExecutor.cs` | Executor đơn giản |
| `Services/Rendering/StorageNodeRenderer.cs` | Renderer mẫu chuẩn |

---

*Tài liệu này tổng hợp từ codebase thực tế sau khi hoàn thành refactor 38 NodeControl classes và các base classes. Cập nhật: 2026-05-16.*

---

## 15. Dynamic Input/Output — Node cho phép user thêm/xóa inputs và outputs

> Dùng khi node cần user cấu hình nhiều nguồn dữ liệu (như CodeNode, HtmlUiNode) hoặc nhiều output keys (như CodeNode).
> **Reference**: `CodeNode.cs`, `CodeNodeDialogViewModel.cs`, `CodeNodeDialog.xaml`

### 15.1 Node Model — List thay vì DynamicInputs cố định

```csharp
// Tạo class mapping item riêng (implement INotifyPropertyChanged nếu cần reactive)
public sealed class YourInputMapping : INotifyPropertyChanged
{
    private string? _sourceNodeId;
    private string? _sourceOutputKey;
    private string? _inputKeyOverride;

    public string? SourceNodeId
    {
        get => _sourceNodeId;
        set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
    }
    public string? SourceOutputKey
    {
        get => _sourceOutputKey;
        set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
    }
    // Tên biến tùy chỉnh (trống = dùng SourceOutputKey)
    public string? InputKeyOverride
    {
        get => _inputKeyOverride;
        set { if (_inputKeyOverride != value) { _inputKeyOverride = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class YourNode : WorkflowNode
{
    private List<YourInputMapping> _inputMappings = new();
    private List<string> _outputKeys = new() { "result" };

    public YourNode()
    {
        Type = NodeType.YourType;
        Title = "Your Node";
        _inputMappings.Add(new YourInputMapping()); // 1 dòng mặc định
        RebuildDynamicOutputs();
        Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = true,  Position = PortPosition.Left,  IsVisible = true, ColorKey = "Info" });
        Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = false, Position = PortPosition.Right, IsVisible = true, ColorKey = "SunsetOrange" });
    }

    // List input mappings (user có thể thêm/xóa)
    public List<YourInputMapping> InputMappings
    {
        get => _inputMappings;
        set { _inputMappings = value ?? new(); OnPropertyChanged(); }
    }

    // List output keys (user có thể thêm/xóa)
    public List<string> OutputKeys
    {
        get => _outputKeys;
        set { _outputKeys = value ?? new(); OnPropertyChanged(); RebuildDynamicOutputs(); }
    }

    // Đồng bộ OutputKeys → DynamicOutputs (gọi sau mỗi lần thay đổi OutputKeys)
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
}
```

### 15.2 ViewModel — ObservableCollection + Add/Remove Commands

```csharp
// Sub-ViewModel cho mỗi dòng input
public partial class YourInputMappingItemViewModel : ObservableObject
{
    [ObservableProperty] private string? _sourceNodeId;
    [ObservableProperty] private string? _sourceOutputKey;
    [ObservableProperty] private string _inputKeyOverride = string.Empty;

    // Output keys của node đã chọn (mỗi dòng có list riêng)
    public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

    // Khi user đổi node → refresh output keys
    partial void OnSourceNodeIdChanged(string? value) => RefreshOutputKeys(value);

    private void RefreshOutputKeys(string? nodeId)
    {
        // Sẽ được gọi từ ViewModel cha — xem bên dưới
    }
}

// Sub-ViewModel cho mỗi output key
public partial class OutputKeyItemViewModel : ObservableObject
{
    [ObservableProperty] private string _key = "result";
}

public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly YourNode _yourNode;
    private bool _isSyncingFromNode; // Guard tránh StackOverflow khi sync 2 chiều

    public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
    public ObservableCollection<YourInputMappingItemViewModel> InputMappingsList { get; } = new();
    public ObservableCollection<OutputKeyItemViewModel> OutputKeysList { get; } = new();

    public YourNodeDialogViewModel(YourNode node, IWorkflowEditorHost host)
        : base(node, host)
    {
        _yourNode = node;

        // Load input mappings từ node → VM
        foreach (var m in node.InputMappings)
        {
            var item = new YourInputMappingItemViewModel
            {
                SourceNodeId = m.SourceNodeId,
                SourceOutputKey = m.SourceOutputKey,
                InputKeyOverride = m.InputKeyOverride ?? string.Empty
            };
            item.PropertyChanged += InputMappingItem_PropertyChanged;
            InputMappingsList.Add(item);
            RefreshOutputKeyOptionsFor(item); // load keys cho node đã chọn
        }

        // Load output keys từ node → VM
        foreach (var k in node.OutputKeys)
            OutputKeysList.Add(new OutputKeyItemViewModel { Key = k });

        // Load danh sách node có thể chọn
        RefreshAllNodesWithOutputs(AvailableNodeOptions);
    }

    protected override string GetDefaultTitle() => "Your Node";

    // Khi user thay đổi bất kỳ field nào trong 1 dòng input → sync về node
    private void InputMappingItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncingFromNode) return;
        if (sender is not YourInputMappingItemViewModel item) return;

        if (e.PropertyName == nameof(YourInputMappingItemViewModel.SourceNodeId))
        {
            RefreshOutputKeyOptionsFor(item); // refresh keys khi đổi node
        }
        SyncInputMappingsToNode();
    }

    // Refresh output keys cho 1 dòng input (gọi khi user đổi node)
    public void RefreshOutputKeyOptionsFor(YourInputMappingItemViewModel item)
    {
        item.AvailableOutputKeyOptions.Clear();
        if (string.IsNullOrWhiteSpace(item.SourceNodeId) || _host.ViewModel?.Nodes == null) return;

        var node = _host.ViewModel.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, item.SourceNodeId, StringComparison.OrdinalIgnoreCase));
        if (node?.DynamicOutputs == null) return;

        foreach (var o in node.DynamicOutputs)
        {
            item.AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
            {
                Key = o.Key ?? string.Empty,
                Type = o.OutputType ?? o.ConvertType,
                DisplayName = o.DisplayName ?? o.Key
            });
        }

        // Nếu key đang chọn không còn trong list → chọn key đầu tiên
        if (item.AvailableOutputKeyOptions.Count > 0 &&
            !item.AvailableOutputKeyOptions.Any(k =>
                string.Equals(k.Key, item.SourceOutputKey, StringComparison.OrdinalIgnoreCase)))
        {
            item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key;
        }
    }

    // Sync VM → node (gọi sau mỗi thay đổi)
    private void SyncInputMappingsToNode()
    {
        _yourNode.InputMappings = InputMappingsList.Select(x => new YourInputMapping
        {
            SourceNodeId = x.SourceNodeId,
            SourceOutputKey = x.SourceOutputKey,
            InputKeyOverride = string.IsNullOrWhiteSpace(x.InputKeyOverride) ? null : x.InputKeyOverride.Trim()
        }).ToList();
    }

    private void SyncOutputKeysToNode()
    {
        _yourNode.OutputKeys = OutputKeysList
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => x.Key.Trim())
            .Distinct()
            .ToList();
        _yourNode.RebuildDynamicOutputs();
    }

    protected override void OnSaveTitle()
    {
        SyncInputMappingsToNode();
        SyncOutputKeysToNode();
        _yourNode.NotifyTitleChanged();
        _host.RequestSyncDataPanels(immediate: true);
    }

    // Commands Add/Remove input
    [RelayCommand]
    private void AddInputMapping()
    {
        var item = new YourInputMappingItemViewModel();
        item.PropertyChanged += InputMappingItem_PropertyChanged;
        InputMappingsList.Add(item);
        SyncInputMappingsToNode();
    }

    [RelayCommand]
    private void RemoveInputMapping(YourInputMappingItemViewModel? item)
    {
        if (item == null || InputMappingsList.Count <= 1) return; // giữ ít nhất 1 dòng
        item.PropertyChanged -= InputMappingItem_PropertyChanged;
        InputMappingsList.Remove(item);
        SyncInputMappingsToNode();
    }

    // Commands Add/Remove output key
    [RelayCommand]
    private void AddOutputKey()
    {
        OutputKeysList.Add(new OutputKeyItemViewModel { Key = "result" });
        SyncOutputKeysToNode();
    }

    [RelayCommand]
    private void RemoveOutputKey(OutputKeyItemViewModel? item)
    {
        if (item == null) return;
        OutputKeysList.Remove(item);
        SyncOutputKeysToNode();
    }
}
```

### 15.3 Dialog XAML — ItemsControl với Add/Remove

```xml
<!-- Input mappings: nhiều dòng, mỗi dòng = Node + Key + Tên biến -->
<TextBlock Text="Input (node nguồn + key):"
           Foreground="{DynamicResource TextBrush}" FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
<ItemsControl x:Name="InputMappingsItemsControl"
              ItemsSource="{Binding InputMappingsList}" Margin="0,0,0,6">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Grid Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="8"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="8"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Cột 1: Chọn Node nguồn -->
                <StackPanel Grid.Column="0">
                    <TextBlock Text="Node" Foreground="{DynamicResource TextMuted}" FontSize="11" Margin="0,0,0,4"/>
                    <controls:NodeSearchComboBoxUserControl Height="36"
                        ItemsSource="{Binding DataContext.AvailableNodeOptions,
                                     RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                        SelectedValuePath="NodeId"
                        DisplayMemberPath="Title"
                        SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"/>
                </StackPanel>

                <!-- Cột 2: Chọn Output Key của node đã chọn -->
                <StackPanel Grid.Column="2">
                    <TextBlock Text="Key" Foreground="{DynamicResource TextMuted}" FontSize="11" Margin="0,0,0,4"/>
                    <ComboBox Height="36" Style="{DynamicResource BaseComboBox}"
                              ItemsSource="{Binding AvailableOutputKeyOptions}"
                              SelectedValuePath="Key"
                              DisplayMemberPath="DisplayName"
                              IsSynchronizedWithCurrentItem="False"
                              SelectedValue="{Binding SourceOutputKey, Mode=TwoWay}"/>
                </StackPanel>

                <!-- Cột 3: Tên biến tùy chỉnh (optional) -->
                <StackPanel Grid.Column="4">
                    <TextBlock Text="Tên biến (trống = dùng key)" Foreground="{DynamicResource TextMuted}" FontSize="11" Margin="0,0,0,4"/>
                    <TextBox Text="{Binding InputKeyOverride, UpdateSourceTrigger=PropertyChanged}"
                             Style="{DynamicResource BaseTextBoxV2}" Height="36"/>
                </StackPanel>

                <!-- Nút xóa dòng -->
                <Button Grid.Column="5" Content="-" Width="28" Height="28" Margin="8,0,0,0"
                        VerticalAlignment="Bottom"
                        Command="{Binding DataContext.RemoveInputMappingCommand,
                                  RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                        CommandParameter="{Binding}"
                        Background="{DynamicResource DangerBrush}" Foreground="White"
                        BorderThickness="0" Cursor="Hand"/>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
<Button Content="+ Thêm input" Height="32" Width="120"
        Command="{Binding AddInputMappingCommand}"
        Style="{DynamicResource PrimaryButton}" HorizontalAlignment="Left" Margin="0,0,0,16"/>

<!-- Output keys: nhiều dòng, mỗi dòng = 1 TextBox key + nút xóa -->
<TextBlock Text="Output keys:" Foreground="{DynamicResource TextBrush}"
           FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
<Border Background="{DynamicResource WindowBackground}"
        BorderBrush="{DynamicResource ControlBorderBrush}"
        BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,8">
    <StackPanel>
        <ItemsControl ItemsSource="{Binding OutputKeysList}">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Grid Margin="0,0,0,6">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <TextBox Text="{Binding Key, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                 Style="{DynamicResource BaseTextBoxV2}" Height="32"/>
                        <Button Grid.Column="1" Content="-" Width="28" Height="28" Margin="8,0,0,0"
                                Command="{Binding DataContext.RemoveOutputKeyCommand,
                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                CommandParameter="{Binding}"
                                Background="{DynamicResource DangerBrush}" Foreground="White"
                                BorderThickness="0" Cursor="Hand"/>
                    </Grid>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
        <Button Content="+ Thêm output key" Height="32" Width="140"
                Command="{Binding AddOutputKeyCommand}"
                Style="{DynamicResource PrimaryButton}" HorizontalAlignment="Left" Margin="0,8,0,0"/>
    </StackPanel>
</Border>
```

**⚠️ Điểm quan trọng trong XAML:**
- `NodeSearchComboBoxUserControl` bind `ItemsSource` qua `RelativeSource AncestorType=ItemsControl` để lấy `AvailableNodeOptions` từ parent ViewModel — **không bind trực tiếp `{Binding AvailableNodeOptions}`** vì DataContext của DataTemplate là item VM, không phải parent VM.
- `ComboBox` Key dùng `IsSynchronizedWithCurrentItem="False"` để tránh các dòng đồng bộ selection với nhau.
- Mỗi dòng có `AvailableOutputKeyOptions` riêng (trong `YourInputMappingItemViewModel`) — không dùng chung 1 list.

### 15.4 Persistence — Serialize/Deserialize list

```csharp
// SERIALIZE — trong GetNodeProperties():
if (node is YourNode yn)
{
    // Serialize InputMappings thành JSON array
    if (yn.InputMappings?.Count > 0)
    {
        var arr = yn.InputMappings.Select(m => new Dictionary<string, object?>
        {
            ["SourceNodeId"]    = m.SourceNodeId,
            ["SourceOutputKey"] = m.SourceOutputKey,
            ["InputKeyOverride"] = m.InputKeyOverride
        }).ToList();
        dict["InputMappings"] = JsonSerializer.Serialize(arr);
    }

    // Serialize OutputKeys thành JSON array
    if (yn.OutputKeys?.Count > 0)
        dict["OutputKeys"] = JsonSerializer.Serialize(yn.OutputKeys);
}

// DESERIALIZE — trong RestoreNodeProperties():
if (node is YourNode yn)
{
    // Deserialize InputMappings — phải handle cả string và JsonElement
    if (properties.TryGetValue("InputMappings", out var imObj) && imObj != null)
    {
        var list = new List<YourInputMapping>();
        try
        {
            string? json = imObj is string s ? s
                : imObj is JsonElement je && je.ValueKind == JsonValueKind.String ? je.GetString()
                : imObj is JsonElement je2 && je2.ValueKind == JsonValueKind.Array ? je2.GetRawText()
                : null;

            if (!string.IsNullOrWhiteSpace(json))
            {
                var raw = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
                if (raw != null)
                {
                    foreach (var item in raw)
                    {
                        list.Add(new YourInputMapping
                        {
                            SourceNodeId    = item.TryGetValue("SourceNodeId", out var sid) ? sid.GetString() : null,
                            SourceOutputKey = item.TryGetValue("SourceOutputKey", out var sok) ? sok.GetString() : null,
                            InputKeyOverride = item.TryGetValue("InputKeyOverride", out var iko) ? iko.GetString() : null
                        });
                    }
                }
            }
        }
        catch { }
        if (list.Count > 0) yn.InputMappings = list;
    }

    // Deserialize OutputKeys
    if (properties.TryGetValue("OutputKeys", out var okObj) && okObj != null)
    {
        try
        {
            string? json = okObj is string s ? s
                : okObj is JsonElement je && je.ValueKind == JsonValueKind.String ? je.GetString()
                : okObj is JsonElement je2 && je2.ValueKind == JsonValueKind.Array ? je2.GetRawText()
                : null;

            if (!string.IsNullOrWhiteSpace(json))
            {
                var keys = JsonSerializer.Deserialize<List<string>>(json);
                if (keys?.Count > 0)
                {
                    yn.OutputKeys = keys;
                    yn.RebuildDynamicOutputs();
                }
            }
        }
        catch { }
    }
}
```

### 15.5 Copy/Paste — Deep copy lists

```csharp
// Trong CreateDuplicateNodeInstance():
else if (source is YourNode srcYour && node is YourNode dstYour)
{
    // ⚠️ Deep copy — KHÔNG gán trực tiếp reference
    dstYour.InputMappings = srcYour.InputMappings
        .Select(m => new YourInputMapping
        {
            SourceNodeId     = m.SourceNodeId,
            SourceOutputKey  = m.SourceOutputKey,
            InputKeyOverride = m.InputKeyOverride
        }).ToList();

    dstYour.OutputKeys = new List<string>(srcYour.OutputKeys);
    dstYour.RebuildDynamicOutputs();
    dstYour.NotifyTitleChanged();
}

// Trong RemapNodeReferenceIds():
case YourNode yourNode:
    foreach (var m in yourNode.InputMappings ?? new())
        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);
    break;
```

### 15.6 Checklist Dynamic Input/Output

```yaml
Node Model:
  - [ ] Tạo class YourInputMapping (INotifyPropertyChanged nếu cần reactive)
  - [ ] List<YourInputMapping> InputMappings với 1 item mặc định trong constructor
  - [ ] List<string> OutputKeys với default keys
  - [ ] RebuildDynamicOutputs() đồng bộ OutputKeys → DynamicOutputs
  - [ ] Gọi RebuildDynamicOutputs() trong constructor và khi OutputKeys thay đổi

ViewModel:
  - [ ] Sub-VM YourInputMappingItemViewModel với AvailableOutputKeyOptions riêng
  - [ ] Sub-VM OutputKeyItemViewModel
  - [ ] bool _isSyncingFromNode guard tránh StackOverflow
  - [ ] InputMappingItem_PropertyChanged: khi đổi SourceNodeId → RefreshOutputKeyOptionsFor
  - [ ] RefreshOutputKeyOptionsFor: Clear + Add keys, giữ selection nếu key còn tồn tại
  - [ ] SyncInputMappingsToNode() + SyncOutputKeysToNode() gọi sau mỗi thay đổi
  - [ ] AddInputMappingCommand / RemoveInputMappingCommand (giữ ít nhất 1 dòng)
  - [ ] AddOutputKeyCommand / RemoveOutputKeyCommand
  - [ ] OnSaveTitle: gọi cả 2 Sync methods

XAML:
  - [ ] ItemsControl bind ItemsSource="{Binding InputMappingsList}"
  - [ ] NodeSearchComboBoxUserControl bind qua RelativeSource AncestorType=ItemsControl
  - [ ] ComboBox Key dùng IsSynchronizedWithCurrentItem="False"
  - [ ] Nút "-" dùng CommandParameter="{Binding}" + RelativeSource để gọi Remove command

Persistence:
  - [ ] Serialize InputMappings thành JSON array (Dictionary per item)
  - [ ] Serialize OutputKeys thành JSON array
  - [ ] Deserialize handle cả string và JsonElement
  - [ ] Gọi RebuildDynamicOutputs() sau khi restore OutputKeys

Copy/Paste:
  - [ ] Deep copy InputMappings (Select + new item)
  - [ ] Deep copy OutputKeys (new List<string>)
  - [ ] Gọi RebuildDynamicOutputs() sau copy
  - [ ] Remap SourceNodeId trong RemapNodeReferenceIds
```

---

## 16. Multi-row NodeSearchComboBox trong ItemsControl — Tránh lỗi đồng bộ

> Đây là lỗi phổ biến nhất khi có nhiều dòng `NodeSearchComboBoxUserControl` + `ComboBox Key` trong `ItemsControl`.
> **Reference**: `CodeNodeDialog.xaml`, `FlowOverwriteNodeDialog.xaml`, `FlowOverwriteNodeDialogViewModel.cs`

### 16.1 Các lỗi thường gặp và nguyên nhân

| Lỗi | Nguyên nhân |
|-----|-------------|
| Tất cả dòng hiển thị cùng 1 node/key | Nhiều dòng dùng chung 1 `ObservableCollection` `ItemsSource` + `IsSynchronizedWithCurrentItem=True` (default) |
| Selection bị reset khi mở dialog lại | `Loaded` event gọi `Clear()` + `Add()` trên `ItemsSource` trong khi `SelectedValue` đang bind TwoWay |
| ComboBox Key trống sau khi chọn Node | `AvailableOutputKeyOptions` là list chung, bị clear khi dòng khác refresh |
| Selection lệch sau paste | `SourceNodeId` không được remap sang Id mới |

### 16.2 Quy tắc bắt buộc

**1. Mỗi dòng có `AvailableOutputKeyOptions` riêng**

```csharp
// ✅ ĐÚNG: mỗi item VM có collection riêng
public partial class YourInputMappingItemViewModel : ObservableObject
{
    // Collection này chỉ thuộc về dòng này — không share với dòng khác
    public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();
}

// ❌ SAI: dùng chung 1 collection ở parent VM
// public ObservableCollection<WorkflowOutputKeyOption> SharedOutputKeys { get; } = new();
```

**2. `IsSynchronizedWithCurrentItem="False"` trên ComboBox Key**

```xml
<!-- ✅ ĐÚNG -->
<ComboBox ItemsSource="{Binding AvailableOutputKeyOptions}"
          IsSynchronizedWithCurrentItem="False"
          SelectedValue="{Binding SourceOutputKey, Mode=TwoWay}"/>

<!-- ❌ SAI: thiếu IsSynchronizedWithCurrentItem="False" -->
<ComboBox ItemsSource="{Binding AvailableOutputKeyOptions}"
          SelectedValue="{Binding SourceOutputKey, Mode=TwoWay}"/>
```

**3. `NodeSearchComboBoxUserControl` bind qua `RelativeSource`, không bind trực tiếp**

```xml
<!-- ✅ ĐÚNG: lấy AvailableNodeOptions từ parent ViewModel qua RelativeSource -->
<controls:NodeSearchComboBoxUserControl
    ItemsSource="{Binding DataContext.AvailableNodeOptions,
                 RelativeSource={RelativeSource AncestorType=ItemsControl}}"
    SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"/>

<!-- ❌ SAI: DataContext của DataTemplate là item VM, không có AvailableNodeOptions -->
<controls:NodeSearchComboBoxUserControl
    ItemsSource="{Binding AvailableNodeOptions}"
    SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"/>
```

**4. Thay collection một lần, không Clear + Add**

```csharp
// ✅ ĐÚNG: thay toàn bộ collection trong 1 lần (tránh WPF reset SelectedValue khi ItemsSource tạm rỗng)
private void SyncAvailableSourceOptions(List<WorkflowDataSourceOption> options)
{
    AvailableSourceOptions = new ObservableCollection<WorkflowDataSourceOption>(options);
    // Dùng [ObservableProperty] để WPF nhận PropertyChanged và rebind
}

// ❌ SAI: Clear + Add từng item → WPF reset SelectedValue khi collection tạm rỗng
AvailableSourceOptions.Clear();
foreach (var opt in options) AvailableSourceOptions.Add(opt);
```

**5. So sánh NodeId dùng `OrdinalIgnoreCase`**

```csharp
// ✅ ĐÚNG
var node = nodes.FirstOrDefault(n =>
    string.Equals(n.Id, selectedId, StringComparison.OrdinalIgnoreCase));

// ❌ SAI: so sánh case-sensitive có thể không khớp
var node = nodes.FirstOrDefault(n => n.Id == selectedId);
```

**6. Refresh output keys: giữ selection nếu key còn tồn tại**

```csharp
public void RefreshOutputKeyOptionsFor(YourInputMappingItemViewModel item)
{
    var currentKey = item.SourceOutputKey; // lưu lại trước khi clear

    item.AvailableOutputKeyOptions.Clear();
    // ... thêm keys mới ...

    // Chỉ reset selection nếu key cũ không còn trong list mới
    if (!string.IsNullOrWhiteSpace(currentKey) &&
        item.AvailableOutputKeyOptions.Any(k =>
            string.Equals(k.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
    {
        item.SourceOutputKey = currentKey; // giữ nguyên
    }
    else if (item.AvailableOutputKeyOptions.Count > 0)
    {
        item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key; // chọn key đầu tiên
    }
}
```

### 16.3 Pattern hoàn chỉnh cho multi-row với Node + Key

Đây là pattern đã được kiểm chứng từ `FlowOverwriteNodeDialog`:

**ViewModel:**
```csharp
// Parent ViewModel
public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
{
    // ✅ Dùng [ObservableProperty] để thay collection 1 lần
    [ObservableProperty]
    private ObservableCollection<WorkflowDataSourceOption> _availableSourceOptions = new();

    public ObservableCollection<YourSourceItemViewModel> Sources { get; } = new();

    // Khi cần refresh: tạo collection mới, không Clear + Add
    private void RefreshSourceOptions()
    {
        var options = BuildOptions(); // build list mới
        AvailableSourceOptions = new ObservableCollection<WorkflowDataSourceOption>(options);

        // Sau khi thay collection, refresh output keys cho từng dòng
        foreach (var row in Sources)
            row.RefreshOutputKeys();
    }
}

// Item ViewModel
public partial class YourSourceItemViewModel : ObservableObject
{
    [ObservableProperty] private string? _selectedSourceNodeId;
    [ObservableProperty] private string? _selectedSourceOutputKey;

    // ✅ Collection riêng cho mỗi dòng
    [ObservableProperty]
    private ObservableCollection<WorkflowOutputKeyOption> _availableOutputKeys = new();

    private readonly IWorkflowEditorHost _host;

    public YourSourceItemViewModel(IWorkflowEditorHost host) { _host = host; }

    // Khi user đổi node → tự động refresh keys
    partial void OnSelectedSourceNodeIdChanged(string? value) => RefreshOutputKeys();

    public void RefreshOutputKeys()
    {
        var selectedId = SelectedSourceNodeId;
        var srcNode = _host.ViewModel?.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, selectedId, StringComparison.OrdinalIgnoreCase));

        var opts = srcNode?.DynamicOutputs?
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => new WorkflowOutputKeyOption
            {
                Key = x.Key.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? x.Key : x.DisplayName,
                Type = x.OutputType ?? x.ConvertType
            })
            .ToList() ?? new();

        // ✅ Thay collection 1 lần
        AvailableOutputKeys = new ObservableCollection<WorkflowOutputKeyOption>(opts);

        // Giữ selection nếu key còn tồn tại, không thì chọn key đầu tiên
        if (string.IsNullOrWhiteSpace(SelectedSourceOutputKey) ||
            !opts.Any(o => string.Equals(o.Key, SelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedSourceOutputKey = opts.FirstOrDefault()?.Key;
        }
    }
}
```

**XAML:**
```xml
<ItemsControl ItemsSource="{Binding Sources}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Grid Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Node: lấy AvailableSourceOptions từ parent VM qua RelativeSource -->
                <controls:NodeSearchComboBoxUserControl Grid.Column="0" Height="32" Margin="0,0,6,0"
                    ItemsSource="{Binding DataContext.AvailableSourceOptions,
                                 RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                    SelectedValuePath="NodeId"
                    DisplayMemberPath="Title"
                    SelectedValue="{Binding SelectedSourceNodeId, Mode=TwoWay}"/>

                <!-- Key: lấy từ item VM riêng, IsSynchronizedWithCurrentItem="False" -->
                <ComboBox Grid.Column="1" Height="32" Margin="0,0,6,0"
                          Style="{DynamicResource BaseComboBox}"
                          ItemsSource="{Binding AvailableOutputKeys}"
                          IsSynchronizedWithCurrentItem="False"
                          SelectedValuePath="Key"
                          DisplayMemberPath="DisplayName"
                          SelectedValue="{Binding SelectedSourceOutputKey, Mode=TwoWay}"/>

                <Button Grid.Column="2" Content="X" Width="28" Height="28"
                        Style="{DynamicResource DangerButton}"
                        Click="RemoveSource_Click"/>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
<Button Content="+ Thêm nguồn" Height="32"
        Style="{DynamicResource PrimaryButton}" Click="AddSource_Click"/>
```

**Code-behind (nếu dùng Click thay vì Command):**
```csharp
private void AddSource_Click(object sender, RoutedEventArgs e)
{
    _viewModel.AddSource();
}

private void RemoveSource_Click(object sender, RoutedEventArgs e)
{
    if (sender is FrameworkElement fe && fe.DataContext is YourSourceItemViewModel item)
        _viewModel.RemoveSource(item);
}
```

### 16.4 Checklist Multi-row NodeSearchComboBox

```yaml
- [ ] Mỗi item VM có AvailableOutputKeyOptions/AvailableOutputKeys riêng (không share)
- [ ] ComboBox Key dùng IsSynchronizedWithCurrentItem="False"
- [ ] NodeSearchComboBoxUserControl bind qua RelativeSource AncestorType=ItemsControl
- [ ] Thay collection 1 lần (new ObservableCollection) thay vì Clear + Add
- [ ] RefreshOutputKeys: giữ selection nếu key còn tồn tại
- [ ] So sánh NodeId dùng OrdinalIgnoreCase
- [ ] partial void OnSelectedSourceNodeIdChanged → tự động RefreshOutputKeys
- [ ] Remap SourceNodeId trong RemapNodeReferenceIds sau multi-paste
```



---

## 17. Liquid Glass — Hỗ trợ giao diện Kính Lỏng

> Khi user chọn `NodeAppearanceMode = "LiquidGlass"` (ComboBox trên toolbar cạnh ThemeSelector),
> tất cả node trên canvas và palette sẽ hiển thị dạng kính lỏng: nền gradient bán trong suốt,
> viền trắng mờ, glow shadow, icon color theo theme (đen cho theme sáng, trắng cho theme tối).
>
> **File chính**: `Services/Rendering/LiquidGlassHelper.cs`

### 17.1 Cách hoạt động — KHÔNG cần code thêm cho node thông thường

Đối với node thông thường (dùng `BaseNodeControlHelper` + `NodeChrome.Apply`), Liquid Glass **tự động hoạt động** mà không cần code thêm:

| Thành phần | Xử lý tự động bởi |
|-----------|-------------------|
| Border background → glass gradient | `NodeChrome.Apply()` |
| Border viền → trắng mờ | `NodeChrome.Apply()` |
| Border effect → glow shadow | `NodeChrome.Apply()` |
| Icon fill → đen/trắng theo theme | `NodeChrome.Apply()` → `UpdateSvgIconFills()` |
| Text fill → đen/trắng + shadow | `NodeChrome.Apply()` → `LiquidGlassHelper.ApplyGlassTextStyle()` |
| Hover → glass hover gradient | `NodeBorder_MouseEnter/Leave` trong `WorkflowEditorWindow.NodeUiHelpers.cs` |
| PropertyChanged NodeBrush → glass | `BaseNodeControlHelper.WithPropertySync()` |

**Kết luận**: Nếu node dùng pattern chuẩn (§4), bạn **không cần làm gì thêm** cho Liquid Glass.

### 17.2 Khi nào CẦN code thêm — Node diamond/polygon

Nếu node dùng **hình thoi (diamond)** hoặc **Polygon** thay vì Border background (như LoopNode, ConditionalDiamondControl, AsyncTask LoopLikeDispatch), cần thêm logic trong `CreateBorder()`:

```csharp
using FlowMy.Services.Rendering;

// Sau khi tạo Polygon:
var diamond = new Polygon
{
    Fill = node.NodeBrush,
    Stroke = new SolidColorBrush(Colors.White),
    StrokeThickness = 2,
    // ...
};

// ✅ Thêm Liquid Glass check:
if (LiquidGlassHelper.IsLiquidGlassMode(host))
{
    var baseColor = LiquidGlassHelper.GetColorFromBrush(node.NodeBrush);
    diamond.Fill = LiquidGlassHelper.CreateGlassBackground(baseColor);
    diamond.Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
    diamond.StrokeThickness = 1.5;
}

// Sau khi tạo icon:
var iconSvg = new SvgViewboxEx
{
    Fill = LiquidGlassHelper.IsLiquidGlassMode(host)
        ? LiquidGlassHelper.GetGlassIconBrush()
        : BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey),
    // ...
};

// Sau khi tạo Grid chứa diamond:
if (LiquidGlassHelper.IsLiquidGlassMode(host))
{
    var baseColor = LiquidGlassHelper.GetColorFromBrush(node.NodeBrush);
    var isLightColor = (0.299 * baseColor.R + 0.587 * baseColor.G + 0.114 * baseColor.B) / 255.0 > 0.65;
    grid.Effect = LiquidGlassHelper.CreateGlassEffect(baseColor, isLightColor);
}
```

### 17.3 Satellite circles (ConditionalDiamondControl)

Nếu node có satellite circles (hình tròn con), cũng cần check:

```csharp
var ellipse = new Ellipse
{
    Fill = LiquidGlassHelper.IsLiquidGlassMode(host)
        ? LiquidGlassHelper.CreateGlassBackground(portColor)
        : new SolidColorBrush(portColor),
    Stroke = LiquidGlassHelper.IsLiquidGlassMode(host)
        ? LiquidGlassHelper.CreateGlassBorderBrush()
        : new SolidColorBrush(Colors.White),
    StrokeThickness = LiquidGlassHelper.IsLiquidGlassMode(host) ? 1.5 : 2,
};

var numberText = new TextBlock
{
    Foreground = LiquidGlassHelper.IsLiquidGlassMode(host)
        ? LiquidGlassHelper.GetGlassIconBrush()
        : Brushes.White,
};
```

### 17.4 LiquidGlassHelper API Reference

| Method | Trả về | Dùng khi |
|--------|--------|---------|
| `IsLiquidGlassMode(host)` | `bool` | Check mode hiện tại |
| `IsCurrentThemeLight()` | `bool` | Check theme sáng (luminance `WindowBackgroundBrush` > 0.5) |
| `GetGlassIconColor()` | `Color` | Đen (theme sáng) / Trắng (theme tối) |
| `GetGlassIconBrush()` | `Brush` | SolidColorBrush từ `GetGlassIconColor()` |
| `CreateGlassBackground(baseColor)` | `LinearGradientBrush` | Nền gradient bán trong suốt |
| `CreateGlassHoverBackground(baseColor)` | `LinearGradientBrush` | Nền hover (sáng hơn) |
| `CreateGlassBorderBrush()` | `SolidColorBrush` | Viền trắng mờ (alpha 100) |
| `CreateGlassHoverBorderBrush()` | `SolidColorBrush` | Viền hover (alpha 180) |
| `CreateGlassEffect(baseColor, isLight)` | `DropShadowEffect` | Glow shadow |
| `ApplyToExistingBorder(border, baseColor)` | `void` | Áp dụng full glass lên Border |
| `ApplyGlassTextStyle(textBlock)` | `void` | Text đen/trắng + shadow theo theme |
| `GetColorFromBrush(brush)` | `Color` | Extract color từ SolidColorBrush/LinearGradientBrush |

### 17.5 NodeChrome.Apply — Logic tự động cho diamond nodes

`NodeChrome.Apply()` đã tự xử lý diamond nodes:

```csharp
// Trong NodeChrome.Apply():
if (LiquidGlassHelper.IsLiquidGlassMode(host))
{
    var isDiamondNode = node is LoopNode
        || (node.IsConditionalNode && node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
        || (node is AsyncTaskNode at && at.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch);

    if (isDiamondNode)
    {
        // Tìm Polygon bên trong border → đổi Fill thành glass gradient
        ApplyGlassToDiamondPolygon(border, baseColor);
    }
    else
    {
        // Node thông thường: đổi border background/viền/effect
        LiquidGlassHelper.ApplyToExistingBorder(border, baseColor);
    }
}
```

> ⚠️ Nếu bạn tạo node mới dùng Polygon/diamond shape, **phải thêm check** vào danh sách `isDiamondNode` trong `NodeChrome.Apply()` để tránh bị áp dụng glass lên border transparent (gây viền vuông).

### 17.6 Hover handlers — Tự động skip diamond nodes

`NodeBorder_MouseEnter/Leave` trong `WorkflowEditorWindow.NodeUiHelpers.cs` đã skip:
- `LoopNode`
- `AsyncTaskNode` với `LoopLikeDispatch`
- `ConditionalNode` với `Diamond` mode

Nếu node mới cũng dùng transparent border + inner shape, **phải thêm skip** vào cả 2 handlers.

### 17.7 Theme switch — Tự động refresh

Khi user đổi theme (Light ↔ Dark), nếu đang ở LiquidGlass mode:
- Canvas nodes: `NodeRendererService.RenderAllNodes()` + `RenderAllConnections()` → icon color cập nhật
- Palette: `ApplyLiquidGlassIconsToPalette()` → chỉ đổi icon fill, không đụng background

Logic này nằm trong `ThemeSelector_SelectionChanged` (ApplicationIdle callback). **Không cần code thêm** cho node mới.

### 17.8 Checklist Liquid Glass cho node mới

```yaml
Node thông thường (dùng BaseNodeControlHelper):
  - [ ] Không cần code thêm — NodeChrome.Apply tự xử lý

Node diamond/polygon (transparent border + inner shape):
  - [ ] Thêm using FlowMy.Services.Rendering
  - [ ] Check LiquidGlassHelper.IsLiquidGlassMode(host) sau khi tạo Polygon
  - [ ] Đổi Polygon.Fill → CreateGlassBackground(baseColor)
  - [ ] Đổi Polygon.Stroke → trắng mờ (alpha 100)
  - [ ] Đổi icon Fill → GetGlassIconBrush()
  - [ ] Thêm grid.Effect → CreateGlassEffect(baseColor, isLightColor)
  - [ ] Thêm node type vào isDiamondNode check trong NodeChrome.Apply()
  - [ ] Thêm skip vào NodeBorder_MouseEnter/Leave (nếu chưa có)

Node có satellite circles:
  - [ ] Ellipse.Fill → CreateGlassBackground(portColor)
  - [ ] Ellipse.Stroke → CreateGlassBorderBrush()
  - [ ] Text Foreground → GetGlassIconBrush()
```

### 17.9 Reference Implementations

| File | Loại | Ghi chú |
|------|------|---------|
| `Services/Rendering/LiquidGlassHelper.cs` | Helper chính | Tất cả API glass |
| `Services/Rendering/NodeChrome.cs` | Auto-apply | `Apply()` → glass cho mọi node |
| `Views/NodeControls/LoopNodeControl.cs` | Diamond node | Polygon + glass check |
| `Views/NodeControls/ConditionalDiamondControl.cs` | Diamond + satellites | Polygon + Ellipse + glass |
| `Views/WorkflowEditors/WorkflowEditorWindow.NodeUiHelpers.cs` | Hover | Glass hover/leave |
| `Views/NodeControls/Helpers/BaseNodeControlHelper.cs` | PropertySync | NodeBrush → glass background |
