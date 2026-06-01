# NodeControl — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách tạo file NodeControl cho node mới.

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

## Lỗi thường gặp

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| 1 | `ArgumentException` khi đổi workflow/theme | `DependencyPropertyDescriptor.AddValueChanged` không được remove | Dùng `.WithVisibilitySync()` — tự track và remove handler |
| 2 | Title không hiện sau load workflow | Quên `.WithTitleManagement()` hoặc `.WithCanvasIntegration()` | Luôn gọi cả hai nếu node có floating title |
| 3 | Memory leak sau nhiều lần đổi workflow | Quên `.WithCleanup()` | `.WithCleanup()` là bắt buộc — không bao giờ bỏ qua |
| 4 | Icon fill không cập nhật khi đổi ColorKey | Không khai báo `ColorKey` handler trong `customPropertyHandlers` | Luôn thêm `[nameof(WorkflowNode.ColorKey)] = ctx => { iconSvg.Fill = ... }` |
| 5 | Port position không đổi khi nhấn arrow key | Quên `.WithKeyboardPorts()` | `.WithHoverBehavior()` tự set `Focusable=true`; `.WithKeyboardPorts()` đăng ký handler |
| 6 | `node.TitleTextBlockUI` là null sau render | Quên `node.TitleTextBlockUI = titleTextBlock` trước `Build()` | Gán trước khi gọi `Initialize` |
| 7 | Dialog mở nhưng node vẫn bị drag | Không gọi `.WithDialogSupport()` đúng cách | `WithDialogSupport` tự xử lý `ReleaseMouseCapture`, `DraggedNode = null` |

---

## Reference Implementations

Xem các file sau làm mẫu thực tế:
- `Views/NodeControls/OutputNodeControl.cs` - Standard pattern, 1 custom handler (ColorKey)
- `Views/NodeControls/StorageNodeControl.cs` - 2 custom handlers (ColorKey + IsInputMode)
- `Views/NodeControls/MouseEventNodeControl.cs` - 2 custom handlers (ColorKey + MouseButton → đổi icon)
- `Views/NodeControls/ScreenCaptureNodeControl.cs` - Embedded title, partial fluent API (dummy title)
- `Views/NodeControls/VideoProcessingNodeControl.cs` - Resizable border, extra Unloaded cleanup
- `Views/NodeControls/WebNodeControl.cs` - Extra Unloaded cleanup (WebView2 dispose)
