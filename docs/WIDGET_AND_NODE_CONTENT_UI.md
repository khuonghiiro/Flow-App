# Node content + Floating Widget — UI không nhoè & cấu trúc dễ bảo trì

> **Mục đích**: Hướng dẫn khi **thêm/sửa node** có hiển thị trên **canvas** và trong **Floating Widget**, để sau này AI/dev chỉnh sửa nhất quán, tránh chữ/control **nhoè** do scale kép.  
> **Đối tượng**: Agent + dev C#/WPF.  
> **Tham chiếu thực tế**: `ImageProcessingNode` + `ImageProcessingNodeContentControl` + `ImageProcessingNodeControl.BuildImageProcessorColumn`.  
> **Cập nhật**: 2026-05-14

---

## 1. Hai ngữ cảnh hiển thị (bắt buộc nắm)

| Ngữ cảnh | Đặc điểm | Gợi ý nhận biết trong code |
|----------|----------|-----------------------------|
| **Canvas (node trên workflow)** | Có `Border` chrome, thường `LayoutTransform` / `Viewbox` để “responsive” theo kích thước node | `chromeBorder != null` khi tạo `*ContentControl` |
| **Floating Widget** | Cùng `UserControl` nhưng host trong `FloatingWidgetWindow`; **không** nên phóng to/thu nhỏ UI bằng `LayoutTransform` trên toàn bộ chrome | `chromeBorder == null` (và thường kèm flag như `freezeScaleInWidget`) |

Một `UserControl` nội dung node (`FooNodeContentControl`) thường được dùng **cả hai chỗ**: truyền `Border` khi embed canvas, truyền `null` khi nhúng widget.

### 1.1 Cờ phân nhánh — chỉ widget mới dùng logic DIP; canvas giữ scaling như cũ

**Nguyên tắc**: Cùng một `UserControl` nhưng **hai nhánh layout** — **không** thay toàn bộ node sang DIP; chỉ khi biết đang **ở widget** thì mới bật nhánh “không Viewbox / không `LayoutTransform` scale UI / chỉnh `FontSize` & size thật”. Trên **canvas** vẫn dùng **`LayoutTransform` + `ScaleTransform` / `Viewbox`** (hoặc pattern responsive hiện có) như trước.

**Cách nhận biết (tham khảo `ImageProcessingNodeContentControl`)**

| Ý nghĩa | Cờ / điều kiện gợi ý | Ghi chú |
|--------|----------------------|---------|
| **Widget** (áp mục “không nhoè”) | `_chromeBorder == null` **và** thường kèm `freezeScaleInWidget == true` (hoặc tên tương đương do bạn đặt) | `FloatingWidgetWindow` tạo content với `chromeBorder: null`. |
| **Canvas** (scaling thường) | `chromeBorder != null` **hoặc** `freezeScaleInWidget == false` | Node trên workflow editor: luôn có `Border` chrome. |

Trong `ApplyResponsiveScale()` (hoặc method tương đương), **nhánh đầu** nên là biểu thức kiểu:

```text
if (chromeBorder == null && freezeScaleInWidget) { /* widget: Identity, typoMul, DIP, rebuild IP với dipNativeLayout */ }
else { /* canvas: Viewbox / LayoutTransform scale như cũ, khôi phục resource mặc định */ }
```

**Build UI con (code-behind)**: truyền thêm cờ kiểu **`dipNativeLayout: true`** *chỉ* khi gọi từ luồng widget (vd. sau khi đo `ipDip`), còn lần build đầu trên canvas giữ `false` — để canvas **vẫn dùng Viewbox** scale theo cột như thiết kế ban đầu.

---

## 2. Vì sao widget nên tránh “scale toàn cục”

- **`LayoutTransform` + `ScaleTransform`** (hoặc **`Viewbox`** bọc panel có TextBlock/Button/TextBox) scale **đã raster hóa** theo pixel layout → dễ **mờ / méo** ở tỉ lệ không nguyên.
- **`PlaceholderTextBlock`** nhìn nét hơn vì thường **không** chịu cùng một lớp scale với vùng list/toolbar — hiệu ứng “chỗ nét chỗ nhoè”.

Các bước dưới đây **chỉ áp dụng khi cờ widget đúng** (mục **1.1**); trên canvas **không** áp dụng thay cho scaling hiện có.

**Hướng xử lý đúng trong widget**

1. **`LayoutTransform = Identity`** trên các vùng toolbar / cột phụ / nhãn cần nét.  
2. Điều chỉnh **kích thước thật (DIP)**: `FontSize`, `Width`/`Height`, `Padding`, `Margin` theo một **hệ số suy ra từ viewport** (vd. `ActualWidth`/`ActualHeight` hoặc độ rộng cột star).  
3. Phần UI phức tạp build code-behind: thêm cờ **`dipNativeLayout`** (hoặc tương đương) để **bỏ `Viewbox`** — chỉ dùng font/size DIP (vd. helper `Zi`/`Zd`), kèm `TextOptions.TextFormattingMode = Display`, `UseLayoutRounding`, `SnapsToDevicePixels`.  
4. **`DynamicResource`** cho font trong `ItemTemplate` (XAML) khi cần đồng bộ list với code-behind (`SetResource` / `PutFontResource`).

---

## 3. Khuyến nghị cấu trúc file (dễ sửa sau này)

### 3.1 Ưu tiên tách UI ra XAML

- **`FooNodeContentControl.xaml` + `.xaml.cs`**: layout chính, style, template list — **dễ diff**, dễ chỉnh margin/column hơn code-only.  
- **Code-behind** (`ApplyResponsiveScale`, `OnSizeChanged`): chỉ tính **số** (scale factor, cột star), gán `FontSize`/grid column / `Resources` cho template.  
- Phần **panel đặc thù** (vd. “Image Processor” cột dài): có thể vẫn build C# nếu logic nặng, nhưng nên **tách method** rõ (`BuildFooProcessorColumn(..., dipNativeLayout)`) giống `BuildImageProcessorColumn`.

### 3.2 Một constructor, hai chế độ

Mẫu tham số (tham khảo `ImageProcessingNodeContentControl`):

- `Border? chromeBorder` — `null` ⇒ host là widget (một phần của điều kiện cờ).  
- `bool freezeScaleInWidget` — `true` ⇒ khi `chromeBorder == null`, nhánh responsive dùng **DIP / Identity** thay vì scale toàn UI như canvas.  
- Gọi `ApplyResponsiveScale()` (hoặc tên tương đương) trong `SizeChanged` / sau khi biết kích thước widget.

**Tóm lại**: constructor nhận cờ → toàn bộ logic “không nhoè” **gói trong `if (widget cờ)`**; nhánh `else` = canvas = scaling như thường.

---

## 4. Checklist khi thêm node có Floating Widget

1. **Model**: `FloatingWidgetConfig` trên node (đã có pattern trong codebase).  
2. **Min kích thước widget**: `FloatingWidgetWindow` dùng `Config.MinExpandedWidth` / `MinWidthRatio` — đừng đặt `Border.MinWidth` node quá lớn nếu muốn widget thu nhỏ sâu (tham khảo `ImageProcessingNodeControl.ImageNodeMinWidthPx`).  
3. **Dialog cấu hình widget** (`FloatingWidgetConfigDialog`): cập nhật `ResolveNodeMinSizePx()` cho node mới nếu cần default min an toàn.  
4. **Content host**: `FloatingWidgetWindow` nhúng `FooNodeContentControl` với `chromeBorder: null`.  
5. **Pseudo-fullscreen widget** (nếu có): API kiểu `SyncWidgetExpandedFullscreen(bool)` để chỉnh cột star / typo — không nhầm với `WindowState.Maximized`.  
6. **Không** bọc toàn bộ content widget trong một `Viewbox` chỉ để “vừa cửa sổ” — thay bằng scroll + DIP.  
7. **Ẩn visual trên canvas** khi widget mở: nếu node có UI nặng / cần một host duy nhất, thêm loại node vào `FloatingWidgetManager.HideNodeVisualWhenWidgetOpened` (xem mục **6** bên dưới).

---

## 5. Tham chiếu nhanh — Image Processing (chuẩn thực tế)

| Thành phần | File | Ghi chú |
|-------------|------|---------|
| Content chính + responsive | `Views/NodeControls/ImageProcessingNodeContentControl.xaml(.cs)` | `_chromeBorder == null` ⇒ nhánh widget: `Identity` transform, `typoMul`, cột `leftStarMul`, `RebuildWidgetImageProcessorIfNeeded(ipDip)` |
| Cột IP (code-built) | `Views/NodeControls/ImageProcessingNodeControl.cs` — `BuildImageProcessorColumn` | Tham số `widgetDipScale`, **`dipNativeLayout: true`** trong widget: **không Viewbox** scroll + bottom bar; `Zi`/`Zd` + `sharpMul`; scroll ngang `Auto` khi cần |
| Widget window | `Views/Overlays/FloatingWidgetWindow.xaml(.cs)` | `ResizeGrip_DragDelta` → `ResolveSizeBounds()`; `ResizeMode="NoResize"` — resize custom bằng Thumb |
| Min widget / node | `ImageProcessingNodeControl.ImageNodeMinWidthPx` / `ImageNodeMinHeightPx`, `FloatingWidgetConfigDialog.ResolveNodeMinSizePx` | Giữ đồng bộ khi đổi min |

---

## 6. Ẩn node trên canvas khi widget đang mở — `FloatingWidgetManager` + HtmlUi tham khảo

### 6.1 Cơ chế chung (`Services/FloatingWidgetManager.cs`)

Khi gọi `OpenWidget(node, host)`:

1. **`HideNodeVisualWhenWidgetOpened(node)`** chạy **trước** `widget.Show()` — chỉ áp dụng cho các loại node được **liệt kê rõ** trong `if` (whitelist): hiện gồm **`HtmlUiNode`**, **`WebNode`**, **`VideoProcessingNode`**, **`ImageProcessingNode`**.  
2. Lưu `Visibility` cũ của `node.Border`, `node.TitleTextBlockUI`, từng `port.PortUI` → gán **`Collapsed`**, đồng thời gắn **guard** `IsVisibleChanged` để code khác không vô tình `Visible` lại trong lúc widget còn mở.  
3. Khi widget `Closed` → **`RestoreNodeVisualAfterWidgetClosed`** khôi phục visibility đã lưu và gỡ guard.

**Node mới có widget**: nếu cần **không thấy** bản canvas trong lúc widget mở, thêm `YourNode` vào cùng điều kiện trong `HideNodeVisualWhenWidgetOpened`.

### 6.2 HtmlUi trên canvas vs trên widget (`HtmlUiNodeControl.cs`)

`FloatingWidgetManager` chỉ **ẩn khung** (Border / title / port); **không** tự chuyển WebView2. HtmlUi bổ sung:

- Khi **`FloatingWidgetManager.Instance.IsWidgetOpen(node.Id)`**:
  - Bỏ qua xử lý **`PendingReadDom`** trên **canvas** (property changed) — để **widget** là nơi đọc DOM thực tế.  
  - Bỏ qua **`PendingAsyncDataPush`** trên **canvas** — nếu canvas drain queue trước thì **widget không còn dữ liệu** để `hostAsync` nhận.

Các node không có WebView2 / queue tương tự (vd. **Image Processing**) chỉ cần nhánh **ẩn Border** ở manager là đủ; không bắt chước toàn bộ skip HtmlUi trừ khi sau này có logic “một host” tương tự.

---

## 7. Liên kết tài liệu khác

- Đặc tả tạo node tổng quát: [`NODE_CREATION_SPEC.md`](./NODE_CREATION_SPEC.md)  
- Dialog node: [`NODE_DIALOG_GUIDE.md`](./NODE_DIALOG_GUIDE.md)

---

## 8. Tóm tắt một dòng cho agent

**Dùng cờ (`chromeBorder == null` + `freezeScaleInWidget`) để phân nhánh: nếu widget thì `LayoutTransform = Identity`, chỉnh DIP (`FontSize`/size), rebuild phần con với `dipNativeLayout: true` (bỏ Viewbox nội bộ); nếu canvas thì giữ `LayoutTransform`/`Viewbox` scaling như cũ. Khi widget mở, `FloatingWidgetManager` ẩn Border/title/port trên canvas cho các loại node đã whitelist. XAML + `DynamicResource` cho template; đồng bộ min size với `FloatingWidgetConfig` + `Border` node.**
