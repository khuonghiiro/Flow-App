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
8. [Các phần còn lại giữ nguyên từ V1](#8-các-phần-còn-lại-giữ-nguyên-từ-v1)

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

## 8. Các phần còn lại giữ nguyên từ V1

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
