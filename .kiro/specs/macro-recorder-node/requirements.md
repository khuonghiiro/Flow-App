# Requirements Document

## Introduction

Node "Macro Recorder" cho phép người dùng ghi lại và phát lại thao tác chuột (click trái, click phải, di chuyển) và bàn phím trên màn hình. Node tích hợp vào workflow FlowMy theo đúng kiến trúc hiện có, kế thừa `WorkflowNode` và đăng ký đầy đủ theo `NODE_CREATION_GUIDE.md`.

## Requirements

### 1. Node Model

**1.1** `MacroRecorderNode` phải kế thừa `WorkflowNode` và được đăng ký với `NodeType.MacroRecorder` trong `Models/Nodes/NodeType.cs`.

**1.2** Node phải có đúng 1 port IN (Left) và 1 port OUT (Right), với `ColorKey = "Info"` cho port IN và `ColorKey = "SunsetOrange"` cho port OUT.

**1.3** Node phải lưu trữ các properties sau:
- `OutputKey` (string, default: `"macroData"`) — tên key output trong scoped store
- `MacroDataJson` (string, default: `""`) — JSON array các action đã ghi
- `PlaybackMode` (enum: `Once` / `Repeat`, default: `Once`) — chế độ phát lại
- `RepeatIntervalMs` (int, default: `500`) — thời gian giữa các chu kỳ lặp (ms), không âm
- `RepeatCount` (int, default: `1`) — số lần lặp khi chế độ `Repeat`, tối thiểu 1

**1.4** `IconKey` của node là `"chart-network light"`, `ColorKey` là `"MangoTango"`.

---

### 2. NodeControl (UI trên canvas)

**2.1** `MacroRecorderNodeControl` phải là `public static class` với method `CreateBorder(MacroRecorderNode, Window?, IWorkflowEditorHost?)`.

**2.2** NodeControl phải dùng fluent API của `BaseNodeControlHelper` với đầy đủ: `.WithTitleManagement()`, `.WithHoverBehavior()`, `.WithKeyboardPorts()`, `.WithPropertySync()`, `.WithDialogSupport()`, `.WithCleanup()`, `.WithVisibilitySync()`, `.WithCanvasIntegration()`, `.Build()`.

**2.3** Chuột phải vào node trên canvas phải mở `MacroRecorderNodeDialog`.

**2.4** `customPropertyHandlers` phải xử lý `ColorKey` → cập nhật icon fill.

---

### 3. Dialog cấu hình

**3.1** Dialog `MacroRecorderNodeDialog` phải kế thừa `BaseNodeDialog` và có 2 tab: "Logic" và "Cấu hình".

**3.2 Tab Logic** phải chứa theo thứ tự:
- `TitleDisplayMode` ComboBox (theo chuẩn guide)
- `TitleColorMode` ComboBox + preview (theo chuẩn guide, với `x:Name="TitleColorComboBox"` và `x:Name="TitleColorPreview"`)
- TextBox `OutputKey` — đặt tên output key (label: "Output key:")
- TextArea readonly `MacroDataJson` — preview JSON đã ghi (label: "Dữ liệu đã ghi (JSON):")
- Button "🔴 Ghi lại thao tác" — bắt đầu quy trình ghi
- ComboBox `PlaybackMode` — "Chạy 1 lần" / "Lặp lại"
- TextBox `RepeatIntervalMs` — chỉ hiển thị khi `PlaybackMode = Repeat` (label: "Thời gian giữa chu kỳ (ms):")
- TextBox `RepeatCount` — chỉ hiển thị khi `PlaybackMode = Repeat` (label: "Số lần lặp:")
- Button "📤 Export JSON" — disabled khi `MacroDataJson` rỗng
- Button "📥 Import JSON" — luôn enabled
- `InputsPanel` (`StackPanel x:Name="InputsPanel"`)
- `OutputsPanel` (`StackPanel x:Name="OutputsPanel"`)

**3.3 Tab Cấu hình** phải chứa:
- Port IN/OUT position ComboBox (theo chuẩn guide)
- ReuseRoutes `ItemsControl` (theo chuẩn guide)

**3.4** Khi `PlaybackMode` chuyển sang "Chạy 1 lần", hai TextBox `RepeatIntervalMs` và `RepeatCount` phải ẩn (`Visibility.Collapsed`). Khi chuyển sang "Lặp lại", hai TextBox đó phải hiện (`Visibility.Visible`).

**3.5** Button "Export JSON" phải bị disabled (`IsEnabled = false`) khi `MacroDataJson` là null hoặc rỗng.

---

### 4. Overlay ghi thao tác

**4.1** Khi nhấn "Ghi lại thao tác":
1. App window minimize (`WindowState = WindowState.Minimized`)
2. Hiển thị `MacroRecorderOverlay` — cửa sổ toàn màn hình trong suốt (Topmost, AllowsTransparency)
3. Overlay hiển thị hướng dẫn: *"Nhấn giữ tổ hợp phím Ctrl+Alt+Shift để bắt đầu ghi lại thao tác"*

**4.2** Khi user nhấn tổ hợp `Ctrl+Alt+Shift` lần đầu:
- Bắt đầu ghi, hiển thị đồng hồ đếm giây đang chạy
- Ghi nhận các sự kiện: click chuột trái, click chuột phải, nhấn phím, di chuyển chuột

**4.3** Trong khi ghi, overlay phải hiển thị visual feedback tại vị trí thao tác:
- Click chuột trái: icon vòng tròn đặc màu xanh + số thứ tự
- Click chuột phải: icon vòng tròn đặc màu đỏ + số thứ tự
- Nhấn phím: hiển thị tên phím + số thứ tự tại vị trí chuột hiện tại
- Di chuyển chuột: vẽ đường trail (polyline) theo đường đi

**4.4** Khi user nhấn `Ctrl+Alt+Shift` lần hai (dừng ghi):
- Dừng ghi, serialize thành JSON
- Restore app window (`WindowState = WindowState.Normal`, `Activate()`)
- Trả dữ liệu JSON về dialog, cập nhật `MacroDataJson` preview

**4.5** Khi user nhấn ESC hoặc đóng overlay trong khi đang ghi:
- Nếu đã ghi được ít nhất 1 action: lưu lại những gì đã ghi, restore app
- Nếu chưa có action nào: hủy bỏ, restore app, không thay đổi `MacroDataJson`

**4.6** Khi user nhấn ESC hoặc đóng overlay trước khi bắt đầu ghi (chưa nhấn Ctrl+Alt+Shift lần đầu): hủy bỏ, restore app.

**4.7** Overlay phải dùng low-level keyboard hook (`SetWindowsHookEx` với `WH_KEYBOARD_LL`) và low-level mouse hook (`WH_MOUSE_LL`) để bắt sự kiện toàn hệ thống, tương tự pattern trong `ScreenPositionPickerOverlay.cs`.

---

### 5. Cấu trúc JSON data

**5.1** `MacroDataJson` là một JSON array, mỗi phần tử là một object với các field:

```json
{
  "sequenceNumber": 1,
  "type": "MouseClick",
  "timestamp": 1234567890123,
  "x": 500,
  "y": 300,
  "button": "Left",
  "key": null
}
```

**5.2** Các giá trị hợp lệ cho `type`: `"MouseClick"`, `"KeyPress"`, `"MouseMove"`.

**5.3** Với `type = "MouseClick"`: `x`, `y`, `button` ("Left" / "Right") phải có giá trị; `key` là null.

**5.4** Với `type = "KeyPress"`: `key` phải có giá trị (tên phím, ví dụ "A", "Enter", "F5"); `button` là null; `x`, `y` là tọa độ chuột tại thời điểm nhấn phím.

**5.5** Với `type = "MouseMove"`: `x`, `y` phải có giá trị; `button` và `key` là null.

**5.6** `timestamp` là Unix timestamp milliseconds tại thời điểm sự kiện xảy ra.

**5.7** `sequenceNumber` là số nguyên tăng dần bắt đầu từ 1.

---

### 6. Export / Import JSON

**6.1** Button "Export JSON" mở `SaveFileDialog` với filter `*.json`, lưu nội dung `MacroDataJson` ra file.

**6.2** Button "Import JSON" mở `OpenFileDialog` với filter `*.json`, đọc file và cập nhật `MacroDataJson` (và preview trong dialog).

**6.3** Khi import, nếu file không phải JSON hợp lệ hoặc không phải array, hiển thị `MessageBox` thông báo lỗi.

---

### 7. Executor (phát lại khi workflow chạy)

**7.1** `MacroRecorderNodeExecutor` phải implement `INodeExecutor` với `CanExecute(node) => node is MacroRecorderNode`.

**7.2** Nếu `MacroDataJson` rỗng hoặc null, executor phải gọi `env.TraverseOutputsAsync()` và kết thúc mà không throw exception.

**7.3** Chế độ "Chạy 1 lần" (`PlaybackMode.Once`): phát lại toàn bộ array action 1 lần.

**7.4** Chế độ "Lặp lại" (`PlaybackMode.Repeat`): phát lại `RepeatCount` lần, giữa mỗi chu kỳ delay `RepeatIntervalMs` milliseconds.

**7.5** Khi phát lại, timing giữa các action phải dựa trên delta timestamp: `delay = action[i].timestamp - action[i-1].timestamp` (action đầu tiên không delay).

**7.6** Phát lại dùng:
- `env.Service.MouseInput.SendMouseClick(button, 1, 0)` cho `MouseClick`
- `env.Service.KeyboardInput.SendKeyPress(key)` cho `KeyPress`
- Mouse move: dùng `SetCursorPos` (P/Invoke) để di chuyển con trỏ

**7.7** Sau khi phát lại xong, executor phải publish `MacroDataJson` vào scoped store với key = `OutputKey`, rồi gọi `env.TraverseOutputsAsync(node)`.

---

### 8. Renderer

**8.1** `MacroRecorderNodeRenderer` phải implement `INodeRenderer` với đầy đủ `RenderNode`, `UpdateNodePosition`, `RemoveNode`, `RemoveAllNodeVisuals`.

**8.2** Renderer phải gọi `MacroRecorderNodeControl.CreateBorder()`, apply `NodeChrome`, attach mouse event handlers, add title TextBlock lên canvas, render ports — theo đúng pattern của `MouseEventNodeRenderer`.

---

### 9. Persistence

**9.1** `GetNodeProperties` (trong `FileWorkflowPersistenceService` và `WorkflowEditorViewModel`) phải serialize các properties: `OutputKey`, `MacroDataJson`, `PlaybackMode`, `RepeatIntervalMs`, `RepeatCount`.

**9.2** `RestoreNodeProperties` phải deserialize đúng các properties trên khi load workflow.

**9.3** Copy/paste node (trong `WorkflowEditorWindow.NodeActions.cs`) phải deep copy tất cả properties của `MacroRecorderNode`.

---

### 10. Đăng ký vào hệ thống

**10.1** Thêm `MacroRecorder` vào enum `NodeType` trong `Models/Nodes/NodeType.cs`.

**10.2** Thêm `"MacroRecorder" => CreateMacroRecorderNode(x, y)` vào switch trong `TemplateFactory.Create()`.

**10.3** Đăng ký `services.AddScoped<MacroRecorderNodeRenderer>()` trong `ServiceCollectionExtensions.cs`.

**10.4** Thêm `MacroRecorderNodeRenderer` vào `_nodeRenderers` dictionary trong `_NodeRenderer.cs` với key `typeof(MacroRecorderNode)`.

**10.5** Thêm `new MacroRecorderNodeExecutor()` vào `_nodeExecutors` list trong `WorkflowExecutionService`.

**10.6** Thêm entry palette cho node vào group "Events" trong `Views/WorkflowEditorWindow.xaml` với `Background="{DynamicResource MangoTangoBrush}"` và `Tag="MacroRecorder"`.

## Glossary

| Thuật ngữ | Định nghĩa |
|-----------|-----------|
| MacroAction | Một thao tác đơn lẻ được ghi lại (click, phím, move) |
| MacroDataJson | Chuỗi JSON chứa array các MacroAction |
| PlaybackMode | Chế độ phát lại: Once (1 lần) hoặc Repeat (lặp lại N lần) |
| Overlay | Cửa sổ toàn màn hình trong suốt dùng để ghi thao tác |
| Low-level hook | Windows API hook bắt sự kiện toàn hệ thống (WH_KEYBOARD_LL, WH_MOUSE_LL) |
| Scoped store | Kho dữ liệu runtime của workflow, scoped theo executionId |
| Delta timestamp | Hiệu thời gian giữa 2 action liên tiếp, dùng để replay đúng timing |
