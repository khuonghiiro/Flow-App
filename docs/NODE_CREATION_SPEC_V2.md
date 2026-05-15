# NODE_CREATION_SPEC V2 — Đặc tả chuẩn tạo Node (BaseNodeControlHelper)

> **Mục đích**: Tài liệu cập nhật sau khi refactor NodeControl. Mọi node mới phải dùng `BaseNodeControlHelper` thay vì tự viết event handlers.
> **Cập nhật**: 2026-05-15
> **Thay thế**: `NODE_CREATION_SPEC.md` (phần §4.5 NodeControl)

---

## MỤC LỤC

1. [Thay đổi so với V1](#1-thay-đổi-so-với-v1)
2. [Danh sách file phải tạo/sửa](#2-danh-sách-file-phải-tạosửa)
3. [Checklist bắt buộc](#3-checklist-bắt-buộc)
4. [Template NodeControl mới (BaseNodeControlHelper)](#4-template-nodecontrol-mới)
5. [Fluent API — Tham chiếu nhanh](#5-fluent-api--tham-chiếu-nhanh)
6. [Các trường hợp đặc biệt](#6-các-trường-hợp-đặc-biệt)
7. [Lỗi thường gặp (bổ sung)](#7-lỗi-thường-gặp-bổ-sung)
8. [Dialog Code-behind — Quy tắc tránh trùng lặp](#8-dialog-code-behind--quy-tắc-tránh-trùng-lặp)
9. [ViewModel — Quy tắc tránh trùng lặp](#9-viewmodel--quy-tắc-tránh-trùng-lặp)
10. [Các phần còn lại giữ nguyên từ V1](#10-các-phần-còn-lại-giữ-nguyên-từ-v1)

---

## 1. Thay đổi so với V1

### Điều đã thay đổi: NodeControl (§4.5)

**Trước (V1)**: Mỗi NodeControl tự viết ~350 dòng event handlers lặp lại:
- `MouseEnter` / `MouseLeave` + hover state
- `PreviewKeyDown` + keyboard port positioning
- `PropertyChanged` + thread-safe UI update
- `Loaded` / `SizeChanged` / `LayoutUpdated` / `Unloaded`
- `DependencyPropertyDescriptor` visibility sync
- `DispatcherTimer` throttling
- `NodeDialogManager` dialog management
- Static dictionaries `_titleUpdateTimers`, `_titleUpdatedAfterZoom`

**Sau (V2)**: Tất cả logic trên được trích xuất vào `BaseNodeControlHelper`. NodeControl chỉ còn:
- Tạo UI elements (grid, icon, border, title)
- Định nghĩa custom property handlers (node-specific)
- Định nghĩa dialog factory lambda
- Gọi fluent API 1 lần

**Kết quả**: File NodeControl từ 400–500 dòng → 100–200 dòng (giảm 60–75%).

### Điều KHÔNG thay đổi

Tất cả các phần khác trong `NODE_CREATION_SPEC.md` vẫn còn hiệu lực:
- §4.1 Node Model
- §4.2 Palette (WorkflowEditorWindow.xaml)
- §4.3 TemplateFactory
- §4.4 Dialog XAML + Code-behind + ViewModel
- §4.6 Renderer
- §4.7 Executor
- §4.8 Persistence
- §4.9 Copy/Paste
- §5–§11 (Input/Output, ExecutionId, Theme, Responsive, Lỗi, Reference, IconKey/ColorKey)

---

## 2. Danh sách file phải tạo/sửa

Giống V1, **chỉ thay đổi cách viết file `YourNodeControl.cs`**.

| # | File | Vị trí | Ghi chú |
|---|------|--------|---------|
| 1 | `YourNode.cs` | `Models/Nodes/` | Không đổi |
| 2 | `YourNodeDialog.xaml` | `Views/Overlays/` | Không đổi |
| 3 | `YourNodeDialog.xaml.cs` | `Views/Overlays/` | Không đổi |
| 4 | `YourNodeDialogViewModel.cs` | `ViewModels/` | Không đổi |
| **5** | **`YourNodeControl.cs`** | **`Views/NodeControls/`** | **Dùng BaseNodeControlHelper** |
| 6 | `YourNodeRenderer.cs` | `Services/Rendering/` | Không đổi |
| 7 | `YourNodeExecutor.cs` | `Services/Workflow/NodeExecutors/` | Không đổi (nếu cần) |

---

## 3. Checklist bắt buộc

```yaml
NodeControl (thay thế hoàn toàn §3 Bước 6 trong V1):
  - [ ] static class
  - [ ] Tạo grid + icon + border + titleTextBlock
  - [ ] node.TitleTextBlockUI = titleTextBlock  ← CRITICAL
  - [ ] border.Tag = node                       ← CRITICAL
  - [ ] Định nghĩa customPropertyHandlers (Dictionary<string, Action<NodeControlContext>>)
        - Ít nhất: ColorKey → update icon fill
        - Thêm handlers cho properties đặc thù của node
  - [ ] Gọi BaseNodeControlHelper.Initialize(...).With*().Build()
  - [ ] KHÔNG tự viết: MouseEnter/Leave, PreviewKeyDown, PropertyChanged,
        Loaded/SizeChanged/LayoutUpdated/Unloaded, DependencyPropertyDescriptor,
        DispatcherTimer, _titleUpdateTimers, _titleUpdatedAfterZoom
  - [ ] KHÔNG tự viết: GetTitleBrush, UpdateTitleVisibility, UpdateTitlePosition,
        ThrottledUpdateTitlePosition, GetOrCreateDialogManager, GetTextBrush
        (tất cả đã có trong BaseNodeControlHelper)
```

---

## 4. Template NodeControl mới

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
            var iconUri = iconConverter.Convert(
                null, typeof(Uri), "your-icon-key duotone-regular",
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
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock; // ⚠️ CRITICAL

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
                Tag = node // ⚠️ CRITICAL: renderer dùng Tag để lấy node
            };

            // ─── 5. CUSTOM PROPERTY HANDLERS (node-specific only) ───
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                // Cập nhật icon fill khi ColorKey thay đổi
                [nameof(WorkflowNode.ColorKey)] = ctx =>
                {
                    iconSvg.Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);
                },
                // Thêm handlers cho properties đặc thù của node nếu cần
                // [nameof(YourNode.SomeProperty)] = ctx => { ... }
            };

            // ─── 6. FLUENT API (thay thế ~350 dòng event handlers) ───
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()      // Loaded, SizeChanged, LayoutUpdated + zoom handling
                .WithHoverBehavior()        // MouseEnter/Leave + focus
                .WithKeyboardPorts()        // Arrow keys → đổi port position
                .WithPropertySync(customPropertyHandlers) // PropertyChanged thread-safe
                .WithDialogSupport(ctx => new YourNodeDialog(
                    node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()             // Unloaded → cleanup timers, title, context
                .WithVisibilitySync()      // DependencyPropertyDescriptor visibility tracking
                .WithCanvasIntegration()   // Loaded → add title to WorkflowCanvas
                .Build();

            return border;
        }
    }
}
```

**Tổng cộng: ~80 dòng** thay vì ~400 dòng.

---

## 5. Fluent API — Tham chiếu nhanh

| Method | Đăng ký gì | Bắt buộc? |
|--------|-----------|-----------|
| `.WithTitleManagement()` | `Loaded`, `SizeChanged`, `LayoutUpdated` (zoom + throttle) | Có (nếu có floating title) |
| `.WithHoverBehavior()` | `MouseEnter`, `MouseLeave`, `border.Focusable = true` | Có |
| `.WithKeyboardPorts()` | `PreviewKeyDown` → Arrow keys đổi port | Có |
| `.WithPropertySync(handlers)` | `INotifyPropertyChanged.PropertyChanged` thread-safe | Có |
| `.WithDialogSupport(factory)` | `MouseRightButtonUp` → mở dialog | Có (nếu có dialog) |
| `.WithCleanup()` | `Unloaded` → stop timer, remove title, dispose context | **Bắt buộc** |
| `.WithVisibilitySync()` | `DependencyPropertyDescriptor` Visibility → sync title | Có (nếu có floating title) |
| `.WithCanvasIntegration()` | `Loaded` → add title to WorkflowCanvas, ZIndex=20000 | Có (nếu có floating title) |
| `.Build()` | Áp dụng tất cả + lưu context vào dictionary | **Bắt buộc** |

### Utility methods có sẵn

```csharp
// Resolve icon fill từ resource dictionary
Brush fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);

// Resolve title foreground brush
Brush titleBrush = BaseNodeControlHelper.ResolveTitleBrush(
    node.TitleColorMode, node.TitleColorKey, node.NodeBrush);

// Lấy context của border (dùng cho advanced scenarios)
var ctx = BaseNodeControlHelper.GetContext(border);

// Lấy TitleDisplayMode/TitleColorMode/TitleColorKey qua reflection (không cần cast)
var mode = BaseNodeControlHelper.GetTitleDisplayMode(node);
var colorMode = BaseNodeControlHelper.GetTitleColorMode(node);
var colorKey = BaseNodeControlHelper.GetTitleColorKey(node);
```

### Default property handlers (tự động xử lý, KHÔNG cần khai báo lại)

`WithPropertySync` đã tự xử lý các properties sau:
- `NodeBrush` → update `border.Background` + `titleTextBlock.Foreground`
- `Title` → update `titleTextBlock.Text` + reposition
- `TitleDisplayMode` → update title visibility
- `TitleColorMode` / `TitleColorKey` → update `titleTextBlock.Foreground`

Chỉ cần khai báo trong `customPropertyHandlers` những gì **đặc thù của node** (ví dụ: `ColorKey` → update icon fill, `Width`/`Height` → resize border, `MouseButton` → đổi icon).

---

## 6. Các trường hợp đặc biệt

### 6.1 Node có embedded title (không dùng floating canvas title)

Ví dụ: `ScreenCaptureNodeControl`, `ScreenPositionPickerNodeControl` — title nằm trong border, không phải TextBlock riêng trên Canvas.

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

### 6.2 Node cần cleanup thêm (WebView2, subscriptions)

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

### 6.3 Node có resize handles (ImageProcessing, VideoProcessing)

Giữ resize handle logic trong NodeControl class — đây là logic đặc thù của node, không thuộc BaseNodeControlHelper.

```csharp
// Sau Build(), thêm resize logic
AttachResizeHandles(border, node, host);
```

### 6.4 Node diamond/conditional (ConditionalDiamondControl)

Diamond có transparent background đặc biệt và satellite circles. Dùng partial fluent API:

```csharp
BaseNodeControlHelper
    .Initialize(border, titleTextBlock, node, host)
    .WithDialogSupport(ctx => new ConditionalNodeDialog(node, host, ownerWindow))
    .WithCleanup()
    .WithCanvasIntegration()
    .Build();
// Hover + keyboard được xử lý thủ công vì cần transparent background logic đặc biệt
```

---

## 7. Lỗi thường gặp (bổ sung)

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| 19 | `ArgumentException` khi đổi workflow/theme | `DependencyPropertyDescriptor.AddValueChanged` không được remove khi node unload | **Đã fix trong V2**: `WithVisibilitySync()` tự track và remove handler qua `TrackDpDescriptorHandler` + `RemoveValueChanged` trong `Dispose()` |
| 20 | Title không hiện sau load workflow | Quên `.WithTitleManagement()` hoặc `.WithCanvasIntegration()` | Luôn gọi cả hai nếu node có floating title |
| 21 | Memory leak sau nhiều lần đổi workflow | Quên `.WithCleanup()` | `.WithCleanup()` là **bắt buộc** — không bao giờ bỏ qua |
| 22 | Icon fill không cập nhật khi đổi ColorKey | Không khai báo `ColorKey` handler trong `customPropertyHandlers` | Luôn thêm `[nameof(WorkflowNode.ColorKey)] = ctx => { iconSvg.Fill = ... }` |
| 23 | Port position không đổi khi nhấn arrow key | Quên `.WithKeyboardPorts()` hoặc `border.Focusable` | `.WithHoverBehavior()` tự set `Focusable=true`; `.WithKeyboardPorts()` đăng ký handler |
| 24 | `node.TitleTextBlockUI` là null sau render | Quên `node.TitleTextBlockUI = titleTextBlock` trước khi gọi `Initialize` | Gán trước `Build()` |
| 25 | Dialog mở nhưng node vẫn bị drag | Không gọi `.WithDialogSupport()` đúng cách | `WithDialogSupport` tự xử lý `ReleaseMouseCapture`, `DraggedNode = null`, `SelectedNode = null` |

---

## 8. Dialog Code-behind — Quy tắc tránh trùng lặp

> **Bối cảnh**: Sau khi refactor, `BaseNodeDialog` đã chứa toàn bộ logic chung. Mọi node dialog mới phải tuân theo quy tắc này để không tái tạo code trùng lặp.

### 8.1 Những gì BaseNodeDialog đã xử lý — KHÔNG viết lại

| Tính năng | Method/Property trong BaseNodeDialog | Ghi chú |
|-----------|--------------------------------------|---------|
| Vị trí dialog (snap phải màn hình) | `PositionDialogRightSnap()` | Tự động qua `SourceInitialized` |
| Clamp khi resize | `ClampToScreen()` | Tự động qua `SizeChanged` |
| Lưu title khi đóng | `Closing` → `SaveTitleCommand` | Tự động qua `InitializeBase` |
| Flush binding trước khi đóng | `virtual BeforeSaveOnClose()` | Override nếu cần flush thêm |
| Load Inputs/Outputs panel | `LoadInputs()`, `LoadOutputs()` | Tự động qua `Loaded` |
| Tạo UI input item | `virtual CreateInputItemUI(InputItemViewModel)` | Override để tùy chỉnh |
| Tạo UI output item | `virtual CreateOutputItemUI(OutputItemViewModel)` | Override để tùy chỉnh |
| Hook sau khi Loaded | `virtual OnLoaded()` | Override để thêm logic post-load |
| Nút đóng dialog | `virtual CloseButton_Click(...)` | Tự động: `SaveTitleCommand + Close()` |
| Preview màu tiêu đề | `UpdateTitleColorPreview()` | Gọi trong constructor sau `InitializeBase()` |
| Handler combobox màu tiêu đề | `virtual TitleColorComboBox_SelectionChanged(...)` | Wire trong XAML, không cần override |
| Mở WinForms ColorDialog | `static ShowColorPicker(string? currentHex)` | Trả về `#RRGGBB` hoặc null |
| Resolve brush từ key/hex | `static ResolveBrush(string? key, Brush fallback)` | Hỗ trợ hex, "LimeGreen", resource key |
| Lấy brush từ theme | `GetThemeBrush(string key, Brush fallback)` | Instance method |
| Lấy color từ theme | `GetThemeColor(string key, Color fallback)` | Instance method |
| Bind DynamicResource cho code-behind control | `static BindThemeResource(element, dp, key)` | Dùng khi tạo control trong code |
| Validate repeatCount input | `virtual ValidateRepeatCountValue(...)` | Tự động cho input key "repeatCount" |
| PropertyChanged hook | `virtual ViewModel_PropertyChanged(...)` | Override nếu cần react |

### 8.2 Template code-behind chuẩn

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

        // ✅ Gọi nếu dialog có TitleColorPreview + TitleColorComboBox trong XAML
        UpdateTitleColorPreview();

        // ✅ Thêm node-specific init ở đây (nếu cần)
        // _viewModel.RefreshAvailableNodes();
    }

    // ✅ BẮT BUỘC: trả về panel chứa inputs (null nếu không có)
    protected override Panel? GetInputsPanel() => InputsPanel;

    // ✅ BẮT BUỘC: trả về panel chứa outputs (null nếu không có)
    protected override Panel? GetOutputsPanel() => OutputsPanel;

    // ✅ CHỈ override nếu cần logic sau khi Loaded (không cần gọi LoadInputs/LoadOutputs — base đã làm)
    // protected override void OnLoaded() { ... }

    // ✅ CHỈ override nếu CloseButton cần làm thêm gì trước khi đóng
    // protected override void CloseButton_Click(object sender, RoutedEventArgs e)
    // {
    //     FlushMyBindings();          // flush binding đặc thù
    //     base.CloseButton_Click(sender, e);  // gọi base để SaveTitle + Close
    // }

    // ✅ CHỈ override BeforeSaveOnClose nếu cần flush binding khi đóng bằng Alt+F4 / X
    // protected override void BeforeSaveOnClose() { FlushMyBindings(); }
}
```

### 8.3 Checklist bắt buộc khi tạo dialog mới

```yaml
Dialog Code-behind:
  - [ ] Kế thừa BaseNodeDialog (KHÔNG kế thừa Window trực tiếp)
  - [ ] Constructor: InitializeComponent() → new ViewModel → InitializeBase(vm, owner)
  - [ ] Gọi UpdateTitleColorPreview() trong constructor nếu có TitleColorPreview trong XAML
  - [ ] Override GetInputsPanel() và GetOutputsPanel() (trả về null nếu không có)
  - [ ] KHÔNG tự viết CloseButton_Click nếu chỉ cần SaveTitleCommand + Close
  - [ ] KHÔNG tự viết TitleColorComboBox_SelectionChanged (đã có trong base)
  - [ ] KHÔNG tự viết UpdateTitleColorPreview() (đã có trong base)
  - [ ] KHÔNG tự viết ViewModel_PropertyChanged rỗng chỉ gọi base
  - [ ] KHÔNG tự viết ShowColorPicker() (đã có trong base — dùng cho color picker hex)
  - [ ] KHÔNG tự viết ResolveBrush() (đã có trong base)
  - [ ] KHÔNG tự viết GetThemeBrush() / GetThemeColor() (đã có trong base)
  - [ ] Nếu cần flush binding trước khi đóng: override BeforeSaveOnClose() thay vì CloseButton_Click
```

### 8.4 XAML chuẩn cho dialog

```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
    ...
    Width="460" MinWidth="350" MaxWidth="900" MinHeight="350">
    <!-- ⚠️ KHÔNG đặt Height cứng — NodeDialogManager auto-size 90% screen -->

    <Border CornerRadius="12" Style="{DynamicResource DialogOuterBorder}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>  <!-- Header -->
                <RowDefinition Height="*"/>     <!-- TabControl -->
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
                        <!-- ✅ Click="CloseButton_Click" — base class xử lý -->
                        <Button Width="24" Height="24" Content="×"
                                Style="{DynamicResource DangerButton}"
                                Click="CloseButton_Click" Margin="8,0,0,0"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- TAB CONTROL -->
            <TabControl Grid.Row="1" Background="Transparent" BorderThickness="0">
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

                            <!-- TitleColorMode — ✅ SelectionChanged wire vào base handler -->
                            <TextBlock Text="Màu tiêu đề:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Grid Margin="0,0,0,16">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <!-- ✅ x:Name="TitleColorComboBox" — base dùng FindName để update preview -->
                                <ComboBox x:Name="TitleColorComboBox" Grid.Column="0" Height="36"
                                          Style="{DynamicResource BaseComboBox}"
                                          ItemsSource="{Binding TitleColorOptions}"
                                          SelectedValuePath="Key" DisplayMemberPath="DisplayName"
                                          SelectedValue="{Binding TitleColorKey, Mode=TwoWay}"
                                          SelectionChanged="TitleColorComboBox_SelectionChanged"/>
                                <!-- ✅ x:Name="TitleColorPreview" — base dùng FindName để update -->
                                <Border x:Name="TitleColorPreview" Grid.Column="1"
                                        Width="36" Height="36" CornerRadius="6" Margin="8,0,0,0"
                                        BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1"/>
                            </Grid>

                            <!-- Custom properties của node -->

                            <!-- Inputs Panel -->
                            <TextBlock Text="Inputs:" Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <!-- ✅ x:Name="InputsPanel" — base gọi GetInputsPanel() để load -->
                                <StackPanel x:Name="InputsPanel"/>
                            </Border>

                            <!-- Outputs Panel -->
                            <TextBlock Text="Outputs:" Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <!-- ✅ x:Name="OutputsPanel" — base gọi GetOutputsPanel() để load -->
                                <StackPanel x:Name="OutputsPanel"/>
                            </Border>
                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

                <TabItem Header="Cấu hình" Style="{StaticResource HttpTabItemStyle}">
                    <!-- Port position + ReuseRoutes — xem NODE_CREATION_SPEC.md §4.4 -->
                </TabItem>
            </TabControl>
        </Grid>
    </Border>
</local:BaseNodeDialog>
```

**Quy tắc đặt tên XAML bắt buộc** (base dùng `FindName` để tìm):

| x:Name | Kiểu | Mục đích |
|--------|------|---------|
| `TitleColorComboBox` | `ComboBox` | Base đọc `SelectedValue` để update preview |
| `TitleColorPreview` | `Border` | Base set `Background` theo màu đã chọn |
| `InputsPanel` | `StackPanel` | Base gọi `GetInputsPanel()` → load input items |
| `OutputsPanel` | `StackPanel` | Base gọi `GetOutputsPanel()` → load output items |

### 8.5 Các trường hợp đặc biệt trong dialog

#### Dialog không có Inputs/Outputs

```csharp
protected override Panel? GetInputsPanel() => null;
protected override Panel? GetOutputsPanel() => null;
```

Không cần thêm gì — base tự bỏ qua khi trả về null.

#### CloseButton cần flush binding trước khi đóng

```csharp
// Cách 1: Override BeforeSaveOnClose (xử lý cả Alt+F4 và nút X)
protected override void BeforeSaveOnClose()
{
    MyComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
    MyTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
}

// Cách 2: Override CloseButton_Click (chỉ xử lý khi bấm nút X)
protected override void CloseButton_Click(object sender, RoutedEventArgs e)
{
    FlushMyBindings();
    base.CloseButton_Click(sender, e); // ← gọi base để SaveTitle + Close
}
```

> ⚠️ Luôn ưu tiên `BeforeSaveOnClose` vì nó xử lý cả trường hợp đóng bằng Alt+F4 hoặc click X trên taskbar.

#### Dialog cần color picker (hex) cho màu tùy chỉnh

```csharp
// ✅ Dùng base.ShowColorPicker — KHÔNG tự viết lại
private void PickMyColor_Click(object sender, RoutedEventArgs e)
{
    var hex = ShowColorPicker(_viewModel.MyColorHex);
    if (!string.IsNullOrWhiteSpace(hex))
    {
        _viewModel.MyColorHex = hex;
        UpdateMyColorPreview();
    }
}

private void UpdateMyColorPreview()
{
    if (MyColorPreview == null) return;
    // ✅ Dùng base.ResolveBrush — KHÔNG tự viết lại
    var brush = ResolveBrush(_viewModel.MyColorHex, Brushes.Gray);
    brush = brush.Clone();
    brush.Opacity = _viewModel.MyOpacity;
    MyColorPreview.Background = brush;
}
```

#### Dialog cần refresh node list sau khi mở

```csharp
protected override void OnLoaded()
{
    base.OnLoaded(); // ← gọi base trước (đã load Inputs/Outputs)
    _viewModel.RefreshAvailableNodes();
    _viewModel.RefreshSomeOtherOptions();
}
```

#### Override CreateInputItemUI để tùy chỉnh label

```csharp
protected override FrameworkElement CreateInputItemUI(InputItemViewModel inputVm)
{
    var element = base.CreateInputItemUI(inputVm); // ← gọi base để tạo UI chuẩn
    // Chỉ sửa label nếu cần
    if (element is StackPanel stack && stack.Children[0] is TextBlock label)
    {
        if (inputVm.Key == "mySpecialKey")
            label.Text = "Tên hiển thị tùy chỉnh";
    }
    return element;
}
```

### 8.6 Lỗi thường gặp trong dialog (bổ sung)

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| D1 | Title color preview không cập nhật khi mở dialog | Quên gọi `UpdateTitleColorPreview()` trong constructor | Gọi sau `InitializeBase()` nếu XAML có `TitleColorPreview` |
| D2 | Title color preview không cập nhật khi đổi combobox | XAML không wire `SelectionChanged="TitleColorComboBox_SelectionChanged"` | Thêm `SelectionChanged="TitleColorComboBox_SelectionChanged"` vào `TitleColorComboBox` trong XAML |
| D3 | `TitleColorPreview` không tìm thấy | `x:Name` sai hoặc control nằm trong DataTemplate | Đặt đúng `x:Name="TitleColorPreview"` ở cấp trực tiếp trong XAML |
| D4 | Inputs/Outputs không load | `GetInputsPanel()` trả về null hoặc sai panel | Trả về đúng `StackPanel` có `x:Name="InputsPanel"` |
| D5 | Binding mất khi đóng bằng Alt+F4 | Chỉ override `CloseButton_Click`, không override `BeforeSaveOnClose` | Override `BeforeSaveOnClose()` để flush binding |
| D6 | Tự viết lại `ShowColorPicker` / `ResolveBrush` | Không biết base đã có | Dùng `ShowColorPicker(hex)` và `ResolveBrush(key, fallback)` từ base |
| D7 | `ViewModel_PropertyChanged` rỗng | Copy từ template cũ | Xóa hoàn toàn — base đã có virtual empty implementation |
| D8 | `CloseButton_Click` chỉ gọi `SaveTitleCommand + Close` | Copy từ template cũ | Xóa hoàn toàn — base đã xử lý |
| D9 | Dialog không snap vào cạnh phải màn hình | Tự set `WindowStartupLocation` sau `InitializeBase` | Không set `WindowStartupLocation` sau `InitializeBase` — base đã set `Manual` |
| D10 | Dialog bị đóng khi mất focus | Tự thêm `Deactivated` handler đóng dialog | Không thêm — base đã có `Deactivated` handler giữ dialog mở |

---

## 9. ViewModel — Quy tắc tránh trùng lặp

> **Bối cảnh**: Sau khi refactor, `BaseNodeDialogViewModel` đã chứa toàn bộ logic chung. Mọi ViewModel mới phải tuân theo quy tắc này.

### 9.1 Những gì BaseNodeDialogViewModel đã xử lý — KHÔNG viết lại

#### Observable Properties (đã có sẵn)

| Property | Kiểu | Ghi chú |
|----------|------|---------|
| `NodeTitle` | `string` | Sync từ `node.Title` |
| `TitleDisplayMode` | `TitleDisplayMode` | Sync từ node qua reflection |
| `TitleColorMode` | `TitleColorMode` | Sync từ node qua reflection |
| `TitleColorKey` | `string?` | Sync từ node; `OnTitleColorKeyChanged` update canvas ngay |
| `InputPortPosition` | `PortPosition` | Từ port IN đầu tiên |
| `OutputPortPosition` | `PortPosition` | Từ port OUT đầu tiên |
| `Inputs` | `ObservableCollection<InputItemViewModel>` | Load từ `DynamicInputs` |
| `Outputs` | `ObservableCollection<OutputItemViewModel>` | Load từ `DynamicOutputs` |
| `ReuseRoutes` | `ObservableCollection<ReuseRouteItemViewModel>` | Load từ connections |

#### Collections tĩnh (đã có sẵn — KHÔNG khai báo lại)

| Collection | Nội dung |
|-----------|---------|
| `TitleDisplayModeOptions` | Hidden / Hover / Always |
| `TitleColorOptions` | NodeColor / LimeGreen / PrimaryBrush / ... |
| `PortPositionOptions` | Left / Top / Right / Bottom |
| `ConnectionLineStyleOptions` | WorkflowDefault / Bezier / Orthogonal / ... |

> ⚠️ **Lỗi phổ biến**: Khai báo lại `TitleDisplayModeOptions` trong derived class — nó sẽ shadow base và tạo bản sao thừa. Xóa đi, XAML binding `{Binding TitleDisplayModeOptions}` sẽ tự resolve về base.

#### Commands (đã có sẵn)

| Command | Chức năng |
|---------|----------|
| `RunSingleNodeCommand` | Chạy node đơn lẻ |
| `RunWorkflowFromNodeCommand` | Chạy workflow từ node này |
| `SaveTitleCommand` | Lưu Title + TitleDisplayMode + TitleColorMode + TitleColorKey + ReuseRoutes + port positions → gọi `OnSaveTitle()` |

#### Virtual/Abstract Methods (override khi cần)

| Method | Mặc định | Override khi nào |
|--------|----------|-----------------|
| `abstract GetDefaultTitle()` | — | **Bắt buộc** |
| `virtual OnSaveTitle()` | empty | Khi cần lưu thêm properties |
| `virtual OnNodePropertyChanged(string)` | empty | Khi cần react với node property changes |
| `virtual LoadInputs()` | Load từ `DynamicInputs` | Khi cần filter inputs (LoopNode, AsyncTaskNode) |
| `virtual LoadOutputs()` | Load từ `DynamicOutputs` | Hiếm khi cần |
| `virtual LoadReuseRoutes()` | Build từ connections | Không cần override nếu `SupportsReuseRoutes = false` |
| `virtual RefreshAvailableSourcesForInputs()` | DFS upstream traversal | Hiếm khi cần |
| `virtual SavePortPositions()` | Lưu IN/OUT port | Override cho ConditionalNode (multi-port) |
| `virtual SupportsReuseRoutes` | `true` | Override `=> false` cho multi-output nodes |

#### Static/Protected Helpers (dùng trực tiếp — KHÔNG viết lại)

| Method | Chức năng |
|--------|----------|
| `CreateDataSourceOption(WorkflowNode)` | Tạo `WorkflowDataSourceOption` đầy đủ icon/brush |
| `CreateDataSourceOption_Clone(option)` | Clone option để tránh shared reference |
| `GetOutputKeysForNode(string? nodeId)` | Lấy output keys của node theo ID |
| `FillOutputKeys(string? nodeId, target)` | Điền output keys vào collection |
| `RefreshAllNodesWithOutputs(target)` | Điền tất cả nodes có DynamicOutputs vào collection |
| `ResolveNodeTypeDisplayName(NodeType)` | NodeType → display name |
| `ResolveNodeIconKey(NodeType)` | NodeType → icon key |
| `ResolveTextOnNodeBrush(string?)` | ColorKey → `TextOnXxxBrush` |
| `ResolveNodeStateBrush(string?, suffix, fallback)` | ColorKey → Brush/HoverBrush/PressedBrush |

### 9.2 Template ViewModel chuẩn

```csharp
public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly YourNode _yourNode;

    // ✅ Chỉ khai báo properties ĐẶC THÙ của node này
    [ObservableProperty] private string _someProperty = string.Empty;
    [ObservableProperty] private int _someCount;

    // ✅ Chỉ khai báo collections ĐẶC THÙ (không khai báo lại TitleDisplayModeOptions!)
    public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

    public YourNodeDialogViewModel(YourNode node, IWorkflowEditorHost host)
        : base(node, host)  // ← base ctor gọi LoadInputs, LoadOutputs, LoadReuseRoutes
    {
        _yourNode = node ?? throw new ArgumentNullException(nameof(node));

        // ✅ Sync properties từ node → VM
        SomeProperty = _yourNode.SomeProperty;
        SomeCount = _yourNode.SomeCount;

        // ✅ Dùng base helper để load node options
        RefreshAllNodesWithOutputs(AvailableNodeOptions);

        // ✅ Subscribe node PropertyChanged cho properties ĐẶC THÙ
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

    // ✅ BẮT BUỘC
    protected override string GetDefaultTitle() => "Your Node";

    // ✅ CHỈ override nếu cần lưu thêm properties ngoài Title/TitleDisplayMode/TitleColorMode
    protected override void OnSaveTitle()
    {
        if (_yourNode.SomeProperty != SomeProperty)
        {
            _yourNode.SomeProperty = SomeProperty;
            _host.RequestSyncDataPanels(immediate: true);
        }
        _yourNode.NotifyTitleChanged();
    }

    // ✅ CHỈ override LoadInputs nếu cần filter inputs theo điều kiện
    // protected override void LoadInputs() { ... }

    // ✅ Dùng base helper cho output keys — KHÔNG tự viết lại
    public ObservableCollection<WorkflowOutputKeyOption> GetKeysForNode(string? nodeId)
        => GetOutputKeysForNode(nodeId); // ← gọi base
}
```

### 9.3 Checklist bắt buộc khi tạo ViewModel mới

```yaml
ViewModel:
  - [ ] Kế thừa BaseNodeDialogViewModel
  - [ ] Constructor: base(node, host) ← base tự gọi LoadInputs/LoadOutputs/LoadReuseRoutes
  - [ ] Override GetDefaultTitle() — BẮT BUỘC
  - [ ] KHÔNG khai báo lại TitleDisplayModeOptions (đã có trong base)
  - [ ] KHÔNG khai báo lại TitleColorOptions (đã có trong base)
  - [ ] KHÔNG khai báo lại PortPositionOptions (đã có trong base)
  - [ ] KHÔNG khai báo lại ConnectionLineStyleOptions (đã có trong base)
  - [ ] KHÔNG tự viết GetOutputKeysForNode() (đã có trong base)
  - [ ] KHÔNG tự viết FillOutputKeys() (đã có trong base)
  - [ ] KHÔNG tự viết RefreshAvailableNodes() cho trường hợp thông thường
        → Dùng RefreshAllNodesWithOutputs(target) từ base
  - [ ] KHÔNG tự viết ResolveNodeTypeDisplayName/ResolveNodeIconKey/ResolveTextOnNodeBrush
        (đã có trong base dưới dạng protected static)
  - [ ] KHÔNG tự viết CreateDataSourceOption() (đã có trong base)
  - [ ] Nếu node không dùng ReuseRoutes: chỉ cần override SupportsReuseRoutes => false
        KHÔNG cần override LoadReuseRoutes()
  - [ ] Nếu cần lưu thêm properties: override OnSaveTitle()
  - [ ] Nếu cần filter inputs: override LoadInputs()
```

### 9.4 Khi nào cần RefreshAvailableNodes tùy chỉnh

`RefreshAllNodesWithOutputs(target)` lấy **tất cả** nodes có DynamicOutputs (trừ chính node hiện tại). Dùng nó cho hầu hết trường hợp.

Chỉ viết logic riêng khi cần filter đặc biệt:

```csharp
// Trường hợp 1: Chỉ lấy nodes kết nối trực tiếp (WebNode, HtmlUiNode)
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

// Trường hợp 2: Chỉ lấy StorageNode (StorageNodeDialogViewModel)
private void RefreshStorageNodes(ObservableCollection<WorkflowDataSourceOption> target)
{
    target.Clear();
    foreach (var n in _host.ViewModel?.Nodes?.OfType<StorageNode>() ?? Enumerable.Empty<StorageNode>())
        target.Add(CreateDataSourceOption(n));
}
```

### 9.5 Lỗi thường gặp trong ViewModel (bổ sung)

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| V1 | `TitleDisplayModeOptions` binding không hoạt động | Khai báo lại local với tên khác hoặc type khác | Xóa local copy — base đã có |
| V2 | `GetOutputKeysForNode` không tìm thấy | Tự viết lại trong derived class với `private` | Dùng `GetOutputKeysForNode` từ base (`protected`) |
| V3 | `CreateDataSourceOption` tạo option thiếu icon/brush | Tự tạo `new WorkflowDataSourceOption { NodeId, Title }` | Dùng `CreateDataSourceOption(node)` từ base |
| V4 | `LoadReuseRoutes()` override thừa | Override cả `SupportsReuseRoutes => false` lẫn `LoadReuseRoutes()` | Chỉ cần `SupportsReuseRoutes => false` |
| V5 | `ResolveNodeIconKey` private copy | Copy từ file cũ | Dùng `protected static ResolveNodeIconKey` từ base |
| V6 | `OnSaveTitle` không gọi `NotifyTitleChanged` | Quên gọi sau khi sync properties | Gọi `_node.NotifyTitleChanged()` ở cuối `OnSaveTitle` nếu node có method này |
| V7 | Inputs không load khi mở dialog | Quên gọi `base(node, host)` hoặc override `LoadInputs` không gọi `RefreshAvailableSourcesForInputs()` | Base ctor tự gọi `LoadInputs()` — chỉ override khi cần filter |
| V8 | ComboBox source node hiện title cũ | Không gọi `RefreshAllNodesWithOutputs` sau khi node title thay đổi | Gọi trong `OnNodePropertyChanged` khi `propertyName == nameof(WorkflowNode.Title)` |

---

## 10. Các phần còn lại giữ nguyên từ V1

Đọc `NODE_CREATION_SPEC.md` cho các phần sau (không thay đổi):

- **§4.1** Node Model (`sealed class`, `INotifyPropertyChanged`, `NotifyTitleChanged`)
- **§4.2** Palette XAML (`WorkflowEditorWindow.xaml`)
- **§4.3** TemplateFactory (switch case + `CreateYourNode`)
- **§4.4** Dialog XAML + Code-behind + ViewModel
- **§4.6** Renderer (`INodeRenderer`, `RenderNode`, `UpdateNodePosition`, `RemoveNode`)
- **§4.7** Executor (`ResolveDynamicValueForExecution`, `TraverseOutputsAsync`)
- **§4.8** Persistence (Serialize + Deserialize ALL properties)
- **§4.9** Copy/Paste (`CreateDuplicateNodeInstance`, `RemapPastedNodeReferences`)
- **§5** Input/Output & truyền dữ liệu
- **§6** ExecutionId & Scoped Outputs
- **§7** Theme System & DynamicResource
- **§8** Responsive Screen & Dialog Sizing
- **§9** Lỗi thường gặp (rows 1–18)
- **§10** Reference Implementations
- **§11** IconKey / ColorKey checklist (4 chỗ phải khớp)

---

## Tham khảo nhanh — Files đã migrate

Xem các file sau làm mẫu (đã migrate sang BaseNodeControlHelper):

| File | Đặc điểm |
|------|----------|
| `OutputNodeControl.cs` | Standard pattern, 1 custom handler (ColorKey) |
| `CodeNodeControl.cs` | Standard pattern, 1 custom handler (ColorKey) |
| `StorageNodeControl.cs` | 2 custom handlers (ColorKey + IsInputMode) |
| `MouseEventNodeControl.cs` | 2 custom handlers (ColorKey + MouseButton → đổi icon) |
| `ScreenCaptureNodeControl.cs` | Embedded title, partial fluent API (dummy title) |
| `ScreenPositionPickerNodeControl.cs` | Embedded title, partial fluent API (dummy title) |
| `ConditionalDiamondControl.cs` | Diamond mode, partial fluent API (no hover/keyboard) |
| `VideoProcessingNodeControl.cs` | Resizable border, extra Unloaded cleanup |
| `WebNodeControl.cs` | Extra Unloaded cleanup (WebView2 dispose) |

Migration guide đầy đủ: `Views/NodeControls/Helpers/MIGRATION_GUIDE.md`
