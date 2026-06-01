# Liquid Glass — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách hỗ trợ giao diện Kính Lỏng cho node.

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
