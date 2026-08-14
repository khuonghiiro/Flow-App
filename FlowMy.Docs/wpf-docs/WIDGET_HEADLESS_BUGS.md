# Widget Headless Mode — Lỗi thường gặp & Cách sửa

> **Mục đích**: Ghi nhận các lỗi đã xảy ra khi chạy workflow ở chế độ headless (widget ngầm), nguyên nhân gốc và cách khắc phục. Dùng để tra cứu nhanh khi gặp lỗi tương tự trong tương lai.  
> **Đối tượng**: Agent + dev C#/WPF.  
> **Cập nhật**: 2026-06-26

---

## Kiến trúc tổng quan Headless Mode

Khi nhấn nút **"Chỉ mở widget này (ngầm)"** từ `MainWindow`:

1. `MainViewModel.LaunchWidgetHeadless()` gọi `OpenWorkflowEditorInternal(headless: true)`
2. `WorkflowEditorWindow` được tạo, gọi `ConfigureHeadlessCanvasOptimization()` → `PrepareWindowForHeadlessBackground()`
3. Window được `Show()` (để Loaded event kích hoạt) rồi ẩn đi
4. `ActivateWidgets()` mở `FloatingWidgetWindow` cho các node đã cấu hình widget
5. Workflow engine chạy bình thường trên cửa sổ editor ẩn

### File chính liên quan

| File | Vai trò |
|------|---------|
| `FlowMy.Wpf-UI/ViewModels/MainViewModel.cs` | `LaunchWidgetHeadless`, `OpenWorkflowEditorInternal`, `PrepareWindowForHeadlessBackground`, `ReopenHeadlessWorkflow` |
| `FlowMy.Wpf-UI/Views/WorkflowEditorWindow.xaml.cs` | `ShouldRenderNodeInHeadlessMode`, `ApplyHeadlessCanvasOptimization`, `DisableHeadlessCanvasOptimizationForDebug` |
| `FlowMy.Wpf-UI/Services/Workflow/NodeExecutors/ActionCanVasNodeExecutor.cs` | Executor chạy macro trên ActionCanvas (cần `Border` visible + `PointToScreen`) |
| `FlowMy.Wpf-UI/Views/Overlays/FloatingWidgetWindow.xaml.cs` | Widget overlay hiện trên desktop |

---

## Lỗi #1: Node ActionCanvas / WebView2 không thao tác được khi chạy headless

**Ngày phát hiện**: 2026-06-26

### Triệu chứng
- Chạy workflow headless với node Start là widget → thao tác macro trên ActionCanvas không chạy
- Kéo/di chuyển node trong canvas không hoạt động
- WebView2 có thể không render đúng

### Nguyên nhân gốc

**`ShouldRenderNodeInHeadlessMode()`** trong `WorkflowEditorWindow.xaml.cs` chỉ cho phép render `HtmlUi` và `Web`, **bỏ qua `ActionCanVas`**:

```csharp
// ❌ TRƯỚC — thiếu ActionCanVas
return node.Type == NodeType.HtmlUi || node.Type == NodeType.Web;
```

Kết quả:
- Node ActionCanvas bị `Visibility.Collapsed` hoặc không được render (`Border = null`)
- `ActionCanVasNodeExecutor.GetNodeBoundsAsync()` cần `macroNode.Border != null` để gọi `PointToScreen()` → throw exception "Không thể xác định vị trí của ActionCanvas trên màn hình"

### Cách sửa

Thêm `NodeType.ActionCanVas` vào whitelist trong `ShouldRenderNodeInHeadlessMode()`:

```csharp
// ✅ SAU — thêm ActionCanVas
return node.Type == NodeType.HtmlUi || node.Type == NodeType.Web || node.Type == NodeType.ActionCanVas;
```

**Lưu ý**: Không resize ActionCanVas trong headless (khác với Web/HtmlUi bị resize lên `1366×768`), vì bounds playback phụ thuộc vào kích thước gốc (`BodyWidth`/`BodyHeight`).

### Vị trí code cần sửa

| File | Method | Dòng (approx) |
|------|--------|---------------|
| `Views/WorkflowEditorWindow.xaml.cs` | `ShouldRenderNodeInHeadlessMode()` | ~320 |

---

## Hướng dẫn: Thêm node type mới vào headless mode

Khi tạo node type mới mà **cần hiển thị trực quan** trong headless mode (ví dụ: cần UI element rendered, WebView2, canvas interaction...), thực hiện:

### Bước 1: Thêm vào `ShouldRenderNodeInHeadlessMode()`

```csharp
// File: Views/WorkflowEditorWindow.xaml.cs
private bool ShouldRenderNodeInHeadlessMode(WorkflowNode node)
{
    if (!_headlessCanvasOptimizationEnabled) return true;
    if (node == null) return false;
    if (_headlessHiddenWidgetNodeIds.Contains(node.Id)) return false;
    return node.Type == NodeType.HtmlUi 
        || node.Type == NodeType.Web 
        || node.Type == NodeType.ActionCanVas
        || node.Type == NodeType.NewNodeType;  // ← THÊM Ở ĐÂY
}
```

### Bước 2: Xác định có cần resize trong headless không

Trong `ApplyHeadlessCanvasOptimization()`, quyết định:

- **Nếu node cần kích thước lớn hơn** (như WebView2 cần viewport rộng): thêm vào nhánh `ApplyHeadlessNodeSize()`
- **Nếu node cần kích thước gốc** (như ActionCanvas cần bounds chính xác): không resize

```csharp
if (node.Type == NodeType.Web || node.Type == NodeType.HtmlUi || node.Type == NodeType.NewNodeType)
{
    ApplyHeadlessNodeSize(node, HeadlessWebNodeWidth, HeadlessWebNodeHeight);
}
// ActionCanVas: giữ kích thước gốc
```

### Bước 3: Kiểm tra executor có cần window visible không

Nếu executor của node mới cần gọi `PointToScreen()`, `Window.GetWindow()`, hoặc tương tác trực tiếp với UI element trên canvas → cần đảm bảo workflow window **không bị Hidden/Minimized** trong headless mode.

Hiện tại `PrepareWindowForHeadlessBackground()` dùng `Minimized + Hidden`. Nếu node mới yêu cầu window visible, cần cân nhắc chuyển sang off-screen approach:

```csharp
// Off-screen: WPF vẫn render nhưng user không thấy
workflowWindow.WindowState = WindowState.Normal;
workflowWindow.Visibility = Visibility.Visible;
workflowWindow.Left = -10000;
workflowWindow.Top = -10000;
```

---

## Checklist nhanh khi debug widget headless

- [ ] Node có được render không? → Kiểm tra `ShouldRenderNodeInHeadlessMode()` có bao gồm `NodeType` của nó
- [ ] Node `Border` có null không? → Kiểm tra `ApplyHeadlessCanvasOptimization()` có gọi `RenderNode()` cho nó
- [ ] `PointToScreen()` trả về đúng không? → Kiểm tra window state (`Hidden`/`Minimized` sẽ cho tọa độ sai)
- [ ] Executor có throw exception không? → Kiểm tra `GetNodeBoundsAsync()` trả về `Rect.Empty`
- [ ] Widget có hiện loading đúng không? → Kiểm tra `IsLaunchingHeadless` và `WaitUntilWidgetOpenAsync` timeout
