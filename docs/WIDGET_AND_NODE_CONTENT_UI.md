# Node content + Floating Widget — UI không nhoè & cấu trúc dễ bảo trì

> **Mục đích**: Hướng dẫn khi **thêm/sửa node** có hiển thị trên **canvas** và trong **Floating Widget**, để sau này AI/dev chỉnh sửa nhất quán, tránh chữ/control **nhoè** do scale kép, **effect/cache GPU** trên `Border` node, và chênh lệch **designer vs runtime**.
> **Đối tượng**: Agent + dev C#/WPF.  
> **Tham chiếu thực tế**: `ImageProcessingNode` + `ImageProcessingNodeContentControl` + `ImageProcessingNodeControl.BuildImageProcessorColumn`.  
> **Cập nhật**: 2026-05-14 (bổ sung mục 8: canvas + GPU + XAML tránh mờ)

---

## 1. Hai ngữ cảnh hiển thị (bắt buộc nắm)

| Ngữ cảnh | Đặc điểm | Gợi ý nhận biết trong code |
|----------|----------|-----------------------------|
| **Designer / XAML preview** | Không có zoom canvas editor, không chạy `NodeChrome` / `ApplyEditorGpuChrome` như runtime — dễ **ảo giác “nét”** trong khi trên canvas vẫn mờ | So sánh luôn với bản build trên workflow |
| **Canvas (node trên workflow)** | Có `Border` node + `WorkflowCanvas` zoom (`RenderTransform`), `NodeChrome` bọc `Child`, và áp dụng **GPU / cache / effect** lên `node.Border` | `ImageProcessingNodeContentControl`: tham số `Border? chromeBorder` + `freezeScaleInWidget`. `VideoProcessingNodeContentControl`: hiện chỉ `(node, host)` — phân biệt widget vs canvas xử lý ở lớp chrome/GPU (mục **8**), không bắt buộc truyền `Border` vào `UserControl`. |
| **Floating Widget** | Cùng `UserControl` nhưng host trong `FloatingWidgetWindow`; **không** nên phóng to/thu nhỏ UI bằng `LayoutTransform` trên toàn bộ chrome | `chromeBorder == null` + `freezeScaleInWidget` (Image); widget không áp `DropShadowEffect` / `BitmapCache` lên cùng lớp với toàn bộ XAML (xem **8**). |

**Image node (chuẩn tham khảo)**: `FooNodeContentControl` có thể nhận `Border? chromeBorder` — `null` khi nhúng widget, non-null khi nằm trong chrome trên canvas để nhánh `ApplyResponsiveScale()` biết ngữ cảnh.

**Video node**: `UserControl` không cần tham chiếu `Border` chrome để phân nhánh; tránh “fix” mờ bằng `LayoutTransform` scale **cả** `RootContentGrid` theo nghịch zoom canvas — dễ xung đột với render và không phải pattern của Image.

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
3. Phần UI phức tạp build code-behind: thêm cờ **`dipNativeLayout`** (hoặc tương đương) để **bỏ `Viewbox`** — chỉ dùng font/size DIP (vd. helper `Zi`/`Zd`), kèm `UseLayoutRounding`, `SnapsToDevicePixels`, và `TextOptions` theo mục **8.3** (tránh mặc định `Display` nếu chữ trông mềm so với canvas).  
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
4. **Content host**: `FloatingWidgetWindow` nhúng `FooNodeContentControl` — với Image: `chromeBorder: null` (và cờ widget); với node không dùng `chromeBorder` trên `UserControl`, chỉ cần host + `null` border nếu constructor hỗ trợ.
5. **Pseudo-fullscreen widget** (nếu có): API kiểu `SyncWidgetExpandedFullscreen(bool)` để chỉnh cột star / typo — không nhầm với `WindowState.Maximized`.  
6. **Không** bọc toàn bộ content widget trong một `Viewbox` chỉ để “vừa cửa sổ” — thay bằng scroll + DIP.  
7. **Ẩn visual trên canvas** khi widget mở: nếu node có UI nặng / cần một host duy nhất, thêm loại node vào `FloatingWidgetManager.HideNodeVisualWhenWidgetOpened` (xem mục **6** bên dưới).  
8. **Co dãn kích thước widget**: `FloatingWidgetWindow.ResolveSizeBounds` dùng min/max từ config + làm mềm min + **nới sàn max** (`nearFull`) để vẫn kéo rộng gần hết work area khi Max trong config nhỏ (logic gốc commit `892447cc`). `FloatingWidgetConfig` **mới** (`new FloatingWidgetConfig()`): constructor gọi `ApplyDefaultExpandedMaxFromPrimaryWorkArea()` — Max px và max tỉ lệ theo **màn hình chính**, thay cho default 1200×900 cố định.

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

## 8. Canvas + GPU + XAML — tránh UI **mờ / nhoè** (runtime khác design/widget)

### 8.1 Triệu chứng

- Trong **designer** hoặc **floating widget** chữ và viền **nét**, nhưng cùng file `.xaml` trên **node canvas** thì **mờ**, đặc biệt sau zoom / kéo thả / bật cache node.

### 8.2 Nguyên nhân chính (WPF) — ưu tiên kiểm tra trước khi “scale thêm” trong XAML

1. **`DropShadowEffect` gắn trên cùng `Border` bọc toàn bộ nội dung**  
   WPF render effect vào texture; mọi thứ bên trong (toolbar, `TextBlock`, preview) đều đi qua lớp làm mềm → **toàn node** trông nhoè.  
   **Chuẩn Image node**: comment + code trong `ImageProcessingNodeControl.CreateBorder` — viền ngoài `Effect = null`, bóng chỉ trên **`shadowPlate`** (lớp nền phía sau, `IsHitTestVisible = false`), grid nội dung nằm **trên**.

2. **`BitmapCache` trên `node.Border` + vị trí `Canvas.Left/Top` lệch pixel**  
   `GpuOptimizationHelper.ApplyToBorder` (khi `forceCache: true`) có logic snap tọa độ canvas về số nguyên — cache ở offset subpixel bị **resample bilinear** → mờ. Node rich UI (video, ảnh) **không** nên cache cả khối border bọc UI.

3. **`ApplyEditorGpuChrome` — nhánh theo loại node** (`Views/NodeControls/ImageProcessingNodeControl.cs`)  
   - **`ImageProcessingNode`** và **`VideoProcessingNode`**: `border.Effect = null`, `ApplyToBorder(..., forceCache: false)` — **không** `DropShadowEffect` + **không** bitmap cache trên border bọc toàn UI.  
   - Các node khác: vẫn có thể bật shadow + cache theo `hostWantsNodeCache` (cân nhắc nếu node có XAML phức tạp).  
   **`NodeChrome.Apply`** (`Services/Rendering/NodeChrome.cs`) phải gọi `ApplyEditorGpuChrome` cho **cả Image và Video** (đồng bộ với `WorkflowEditorWindow.ApplyGpuSettings` / sau drag).

4. **Đừng “chữa” mờ canvas bằng `LayoutTransform` scale cả `RootContentGrid` theo `1/ZoomLevel`**  
   Image không làm vậy; họ dùng **scale có chọn lọc** trên vùng chrome (`TopMenuBorder`, `LeftMenuBorder`, …) trong `ApplyResponsiveScale()`. Scale toàn cây + zoom canvas dễ tạo **scale kép** và hinting xấu.

### 8.3 XAML — checklist nhanh (bổ sung cho `.xaml` nội dung node)

| Việc nên làm | Ghi chú |
|--------------|---------|
| `UseLayoutRounding="True"` / `SnapsToDevicePixels="True"` trên `UserControl` hoặc grid gốc | Giống `ImageProcessingNodeContentControl` |
| `TextOptions.TextFormattingMode` | `Display` đôi khi làm chữ “mềm” hơn `Ideal` trên UI dày; cân nhắc **`Ideal`** để gần mặc định Image |
| `TextOptions.TextHintingMode="Fixed"` | Hữu ích khi có transform / resize thường xuyên |
| Tránh **`FontSize` lẻ** (9.5, 10.5, …) | Làm glyph lệch subpixel; ưu tiên số nguyên |
| `Viewbox` bọc panel có nhiều text | Chỉ dùng khi thật sự cần; widget thường cần nhánh **bỏ Viewbox** + DIP (mục 2) |
| `GlassCardStyle` / `Border` lớn | Có thể bật `SnapsToDevicePixels` / `UseLayoutRounding` trên style thẻ |

### 8.4 Tham chiếu file (sau chỉnh sửa 2026-05)

| Chủ đề | File |
|--------|------|
| Nhánh GPU/sharp cho Image + Video | `ImageProcessingNodeControl.ApplyEditorGpuChrome` |
| Gọi GPU khi bọc chrome | `Services/Rendering/NodeChrome.Apply` |
| Helper cache / scaling / snap | `Services/Rendering/GpuOptimizationHelper.cs` |
| Cấu trúc bóng tách lớp (Image) | `ImageProcessingNodeControl.CreateBorder` (`shadowPlate` + `outerGrid`) |
| Content video + `ApplyToElement` trên grid overlay | `VideoProcessingNodeControl.CreateBorder` |
| Không scale cả root content theo zoom | `VideoProcessingNodeContentControl.RefreshLargeNodeUiScale` → `Identity` |

---

## 9. Thêm Running Indicator & Play Button vào FloatingWidgetWindow

### 9.1 Play Button (Chạy logic node đơn lẻ)

**Mục đích**: Thêm button vào title bar để chạy logic của node đang chọn (tương tự CodeNodeDialog).

**File cần sửa**: `Views/Overlays/FloatingWidgetWindow.xaml`

**Thêm vào TitleBar StackPanel** (tìm phần có `TitleTextBlock`):
```xml
<Button x:Name="RunSingleNodeBtn"
        Padding="0,0,0,0"
        Width="24"
        Height="24"
        Content="▶"
        FontSize="12"
        Style="{DynamicResource PrimaryButton}"
        Cursor="Hand"
        Margin="8,0,0,0"
        ToolTip="Chạy logic node này"
        Click="RunSingleNodeBtn_Click"/>
```

**File cần sửa**: `Views/Overlays/FloatingWidgetWindow.xaml.cs`

**Thêm event handler**:
```csharp
private void RunSingleNodeBtn_Click(object sender, RoutedEventArgs e)
{
    _host.RequestRunSingleNode(_node);
}
```

### 9.2 Running Indicator Circle (Hiển thị khi workflow/node đang chạy)

**Mục đích**: Hiển thị hình tròn nhỏ ở góc widget khi workflow hoặc node đang chạy, với animation heartbeat và đổi màu.

**File cần sửa**: `Views/Overlays/FloatingWidgetWindow.xaml`

**Thêm vào mỗi idle shape** (IdleCircle, IdleDiamond, IdleSquare, IdleRoundedSquare, EdgeDockSquare):

```xml
<!-- Running indicator circle -->
<Border x:Name="IdleRunningIndicator"
        Width="16"
        Height="16"
        CornerRadius="10"
        Background="{DynamicResource DangerBrush}"
        BorderBrush="{DynamicResource TextOnDangerBrush}"
        BorderThickness="2"
        HorizontalAlignment="Right"
        VerticalAlignment="Top"
        Margin="0,-2,-2,0"
        Visibility="Collapsed">
    <Border.RenderTransform>
        <ScaleTransform x:Name="IdleRunningIndicatorScale" ScaleX="1" ScaleY="1"/>
    </Border.RenderTransform>
</Border>
```

**Lưu ý**: Tương tự cho các shape khác với tên tương ứng:
- `IdleDiamondRunningIndicator` + `IdleDiamondRunningIndicatorScale`
- `IdleSquareRunningIndicator` + `IdleSquareRunningIndicatorScale`
- `IdleRoundedRunningIndicator` + `IdleRoundedRunningIndicatorScale`
- `EdgeDockRunningIndicator` + `EdgeDockRunningIndicatorScale`

**File cần sửa**: `Views/Overlays/FloatingWidgetWindow.xaml.cs`

**Cập nhật `UpdateRunButtonsState()`**:
```csharp
// Update running indicators on idle shapes
var indicatorVisibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
if (IdleRunningIndicator != null)
{
    IdleRunningIndicator.Visibility = indicatorVisibility;
    if (isRunning)
    {
        StartRunningIndicatorAnimation(IdleRunningIndicator, IdleRunningIndicatorScale);
        UpdateRunningIndicatorPosition();
    }
    else StopRunningIndicatorAnimation(IdleRunningIndicator, IdleRunningIndicatorScale);
}
// ... tương tự cho các indicator khác
```

**Thêm method animation**:
```csharp
private void StartRunningIndicatorAnimation(Border indicator, ScaleTransform scaleTransform)
{
    try
    {
        if (indicator == null || scaleTransform == null) return;

        // Heartbeat animation
        var scaleAnimation = new DoubleAnimation(1.0, 1.2, TimeSpan.FromMilliseconds(420))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

        // Color animation: Red -> Blue -> Orange -> Red
        var brush = new SolidColorBrush(Color.FromRgb(255, 74, 74));
        indicator.Background = brush;

        var colorAnimation1 = new ColorAnimation(Color.FromRgb(255, 74, 74), Color.FromRgb(33, 150, 243), TimeSpan.FromSeconds(1));
        var colorAnimation2 = new ColorAnimation(Color.FromRgb(33, 150, 243), Color.FromRgb(255, 152, 0), TimeSpan.FromSeconds(1))
        {
            BeginTime = TimeSpan.FromSeconds(1)
        };
        var colorAnimation3 = new ColorAnimation(Color.FromRgb(255, 152, 0), Color.FromRgb(255, 74, 74), TimeSpan.FromSeconds(1))
        {
            BeginTime = TimeSpan.FromSeconds(2)
        };

        var colorStoryboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(colorAnimation1, indicator);
        Storyboard.SetTargetProperty(colorAnimation1, new PropertyPath("(Border.Background).(SolidColorBrush.Color)"));
        Storyboard.SetTarget(colorAnimation2, indicator);
        Storyboard.SetTargetProperty(colorAnimation2, new PropertyPath("(Border.Background).(SolidColorBrush.Color)"));
        Storyboard.SetTarget(colorAnimation3, indicator);
        Storyboard.SetTargetProperty(colorAnimation3, new PropertyPath("(Border.Background).(SolidColorBrush.Color)"));

        colorStoryboard.Children.Add(colorAnimation1);
        colorStoryboard.Children.Add(colorAnimation2);
        colorStoryboard.Children.Add(colorAnimation3);

        colorStoryboard.Begin();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[FloatingWidget] Error starting running indicator animation: {ex.Message}");
    }
}

private void StopRunningIndicatorAnimation(Border indicator, ScaleTransform scaleTransform)
{
    try
    {
        if (indicator == null || scaleTransform == null) return;
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        indicator.BeginAnimation(Border.BackgroundProperty, null);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[FloatingWidget] Error stopping running indicator animation: {ex.Message}");
    }
}
```

**Thêm method vị trí động**:
```csharp
private void UpdateRunningIndicatorPosition()
{
    try
    {
        var workArea = SystemParameters.WorkArea;
        var widgetCenterX = Left + Width / 2;
        var widgetCenterY = Top + Height / 2;
        var workAreaCenterX = workArea.Left + workArea.Width / 2;
        var workAreaCenterY = workArea.Top + workArea.Height / 2;

        // Determine which edge the widget is closest to
        var isOnLeft = widgetCenterX < workAreaCenterX;
        var isOnRight = widgetCenterX > workAreaCenterX;
        var isOnTop = widgetCenterY < workAreaCenterY;
        var isOnBottom = widgetCenterY > workAreaCenterY;

        Thickness margin;
        HorizontalAlignment hAlign;
        VerticalAlignment vAlign;

        // Left edge → right-bottom
        if (isOnLeft && !isOnBottom)
        {
            margin = new Thickness(0, 0, -4, -4);
            hAlign = HorizontalAlignment.Right;
            vAlign = VerticalAlignment.Bottom;
        }
        // Top edge → right-bottom
        else if (isOnTop && !isOnRight)
        {
            margin = new Thickness(0, 0, -4, -4);
            hAlign = HorizontalAlignment.Right;
            vAlign = VerticalAlignment.Bottom;
        }
        // Right edge → left-bottom
        else if (isOnRight)
        {
            margin = new Thickness(-4, 0, 0, -4);
            hAlign = HorizontalAlignment.Left;
            vAlign = VerticalAlignment.Bottom;
        }
        // Bottom edge → right-top
        else if (isOnBottom)
        {
            margin = new Thickness(0, -4, -4, 0);
            hAlign = HorizontalAlignment.Right;
            vAlign = VerticalAlignment.Top;
        }
        // Default (center) → right-bottom
        else
        {
            margin = new Thickness(0, 0, -4, -4);
            hAlign = HorizontalAlignment.Right;
            vAlign = VerticalAlignment.Bottom;
        }

        // Apply to all running indicators
        if (IdleRunningIndicator != null)
        {
            IdleRunningIndicator.Margin = margin;
            IdleRunningIndicator.HorizontalAlignment = hAlign;
            IdleRunningIndicator.VerticalAlignment = vAlign;
        }
        // ... tương tự cho các indicator khác
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[FloatingWidget] Error updating running indicator position: {ex.Message}");
    }
}
```

### 9.3 SlideHidePercent Workaround (Fix lỗi nhân đôi)

**Vấn đề**: Khi nhập 30% thì ẩn 60% (nhân đôi).

**Giải pháp**: Chia 200 thay vì 100 khi lưu, mặc định 0% thay vì 50%.

**File cần sửa**: `Models/FloatingWidgetConfig.cs`

```csharp
private double _slideHidePercent = 0.0; // Thay vì 0.5
```

**File cần sửa**: `Views/Overlays/FloatingWidgetConfigDialog.xaml.cs`

```csharp
cfg.SlideHidePercent = ParseDouble(SlideHidePercentTextBox.Text, cfg.SlideHidePercent) / 200.0; // Thay vì / 100.0
```

**Logic**: Nhập 30% → lưu 15% → nếu logic nhân đôi → 30% ẩn đúng.

---

## 10. Tóm tắt một dòng cho agent

**Widget vs canvas (Image): dùng `chromeBorder == null` + `freezeScaleInWidget` — widget thì `LayoutTransform = Identity`, chỉnh DIP / `dipNativeLayout`, bỏ Viewbox nội bộ; canvas thì responsive theo vùng (không scale cả cây vô tội vạ). Trên canvas: đừng gắn `DropShadowEffect` + `BitmapCache` lên `Border` bọc toàn XAML — dùng cùng nhánh `ApplyEditorGpuChrome` như Image/Video, cấu trúc `shadowPlate` nếu cần bóng. XAML: layout rounding, snap pixel, font nguyên, `TextOptions` hợp lý. `FloatingWidgetManager` whitelist khi cần ẩn node canvas. Running indicator: thêm Border vào idle shape, animation heartbeat + color trong code-behind, vị trí động theo edge màn hình. Play button: event handler gọi `_host.RequestRunSingleNode(_node)`. SlideHidePercent: chia 200 thay vì 100 để workaround lỗi nhân đôi.**
