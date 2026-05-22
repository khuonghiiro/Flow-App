# Implementation Plan: Macro Recorder Node

## Overview

Triển khai node Macro Recorder theo thứ tự từ model → control → dialog → overlay → executor → renderer → đăng ký hệ thống. Mỗi task độc lập hoặc phụ thuộc rõ ràng vào task trước.

## Tasks

- [x] 1. Thêm `MacroRecorder` vào enum `NodeType`
  - File: `Models/Nodes/NodeType.cs`
  - Thêm `MacroRecorder` vào cuối enum

- [x] 2. Tạo `MacroAction` DTO
  - File: `Models/Nodes/MacroAction.cs` (file mới)
  - Class `MacroAction` với properties: `SequenceNumber` (int), `Type` (string), `Timestamp` (long), `X` (int), `Y` (int), `Button` (string?), `Key` (string?)

- [x] 3. Tạo `MacroPlaybackMode` enum và `MacroRecorderNode` model
  - File: `Models/Nodes/MacroRecorderNode.cs` (file mới)
  - Enum `MacroPlaybackMode { Once, Repeat }`
  - Class `MacroRecorderNode : WorkflowNode` với đầy đủ properties và ports trong constructor
  - Requires: Task 1, Task 2

- [x] 4. Tạo `MacroRecorderNodeControl`
  - File: `Views/NodeControls/MacroRecorderNodeControl.cs` (file mới)
  - Static class, method `CreateBorder`, icon `"chart-network light"`, fluent API đầy đủ
  - Requires: Task 3

- [x] 5. Tạo `MacroRecorderNodeDialogViewModel`
  - File: `ViewModels/MacroRecorderNodeDialogViewModel.cs` (file mới)
  - Properties: `OutputKey`, `MacroDataJson`, `SelectedPlaybackMode`, `RepeatIntervalMs`, `RepeatCount`
  - Computed: `IsRepeatVisible`, `CanExportJson`
  - `PlaybackModeOptions` collection ("Chạy 1 lần", "Lặp lại")
  - `OnSaveTitle` sync về node
  - Requires: Task 3

- [x] 6. Tạo `MacroRecorderOverlay` (XAML + code-behind)
  - File: `Views/Overlays/MacroRecorderOverlay.xaml` + `.xaml.cs` (file mới)
  - Cửa sổ toàn màn hình trong suốt, Topmost, AllowsTransparency
  - State machine: IDLE → RECORDING → DONE/CANCELLED
  - Low-level keyboard hook (`WH_KEYBOARD_LL`) detect Ctrl+Alt+Shift
  - Low-level mouse hook (`WH_MOUSE_LL`) ghi click trái, click phải, mouse move
  - Visual feedback: trail polyline, ellipse click (xanh/đỏ), label phím + số thứ tự
  - Đồng hồ đếm giây khi đang ghi (DispatcherTimer)
  - Xử lý ESC: lưu nếu ≥1 action, hủy nếu 0 action
  - Property `RecordedJson` (string?) và `HasData` (bool) trả về kết quả
  - Requires: Task 2

- [x] 7. Tạo `MacroRecorderNodeDialog` (XAML + code-behind)
  - File: `Views/Overlays/MacroRecorderNodeDialog.xaml` + `.xaml.cs` (file mới)
  - Kế thừa `BaseNodeDialog`
  - Tab "Logic": TitleDisplayMode, TitleColorMode+preview, OutputKey TextBox, MacroDataJson TextArea readonly, Button "🔴 Ghi lại thao tác", PlaybackMode ComboBox, RepeatIntervalMs TextBox (ẩn/hiện theo IsRepeatVisible), RepeatCount TextBox (ẩn/hiện), Button "📤 Export JSON" (bind IsEnabled=CanExportJson), Button "📥 Import JSON", InputsPanel, OutputsPanel
  - Tab "Cấu hình": Port IN/OUT position, ReuseRoutes
  - Code-behind: xử lý "Ghi lại thao tác" (minimize app, show overlay, restore app, cập nhật MacroDataJson), Export JSON (SaveFileDialog), Import JSON (OpenFileDialog + validate JSON)
  - Requires: Task 5, Task 6

- [x] 8. Tạo `MacroRecorderNodeRenderer`
  - File: `Services/Rendering/MacroRecorderNodeRenderer.cs` (file mới)
  - Implement `INodeRenderer`
  - `RenderNode`, `UpdateNodePosition`, `RemoveNode`, `RemoveAllNodeVisuals`
  - Pattern giống `MouseEventNodeRenderer`
  - Requires: Task 4

- [x] 9. Tạo `MacroRecorderNodeExecutor`
  - File: `Services/Workflow/NodeExecutors/MacroRecorderNodeExecutor.cs` (file mới)
  - Implement `INodeExecutor`
  - `CanExecute`: `node is MacroRecorderNode`
  - `ExecuteAsync`: parse JSON, phát lại với timing delta (Task.Delay = delta timestamp), hỗ trợ Once/Repeat với delay giữa chu kỳ, mouse move dùng SetCursorPos P/Invoke, publish output vào scoped store, gọi TraverseOutputsAsync
  - Requires: Task 3

- [x] 10. Thêm `CreateMacroRecorderNode` vào `TemplateFactory`
  - File: `Workflow/TemplateFactory.cs`
  - Thêm `"MacroRecorder" => CreateMacroRecorderNode(x, y)` vào switch
  - Thêm private method `CreateMacroRecorderNode(double x, double y)` với `MangoTangoBrush`
  - Requires: Task 3

- [x] 11. Đăng ký renderer trong `ServiceCollectionExtensions`
  - File: `Services/ServiceCollectionExtensions.cs`
  - Thêm `services.AddScoped<MacroRecorderNodeRenderer>()`
  - Requires: Task 8

- [x] 12. Đăng ký renderer trong `_NodeRenderer.cs`
  - File: `Services/Rendering/_NodeRenderer.cs`
  - Thêm field `_macroRecorderNodeRenderer`, constructor param, và entry `[typeof(MacroRecorderNode)] = _macroRecorderNodeRenderer` vào dictionary
  - Requires: Task 8, Task 11

- [x] 13. Đăng ký executor trong `WorkflowExecutionService`
  - File: `Services/Workflow/WorkflowExecutionService.cs`
  - Thêm `new MacroRecorderNodeExecutor()` vào `_nodeExecutors` list
  - Requires: Task 9

- [x] 14. Thêm persistence — `GetNodeProperties` trong `FileWorkflowPersistenceService`
  - File: `Services/Workflow/FileWorkflowPersistenceService.cs`
  - Thêm `else if (node is MacroRecorderNode macroNode)` block serialize: OutputKey, MacroDataJson, PlaybackMode, RepeatIntervalMs, RepeatCount
  - Requires: Task 3

- [x] 15. Thêm persistence — `RestoreNodeProperties` trong `FileWorkflowPersistenceService`
  - File: `Services/Workflow/FileWorkflowPersistenceService.cs`
  - Thêm `else if (node is MacroRecorderNode macroNode)` block deserialize 5 properties
  - Requires: Task 3

- [x] 16. Thêm persistence trong `WorkflowEditorViewModel`
  - File: `ViewModels/WorkflowEditorViewModel.cs`
  - Thêm `else if (node is MacroRecorderNode)` block vào cả `GetNodeProperties` và `RestoreNodeProperties`
  - Requires: Task 3

- [x] 17. Thêm copy/paste support
  - File: `Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs`
  - Trong method clone node, thêm deep copy cho `MacroRecorderNode` properties (OutputKey, MacroDataJson, PlaybackMode, RepeatIntervalMs, RepeatCount)
  - Requires: Task 3

- [x] 18. Thêm entry palette vào `WorkflowEditorWindow.xaml`
  - File: `Views/WorkflowEditorWindow.xaml`
  - Thêm `Border` với `Tag="MacroRecorder"` vào `EventsWrapPanel` sau entry MouseEvent
  - Background: `MangoTangoBrush`, icon: `chart-network light`, Fill: `TextOnMangoTangoBrush`
  - ToolTip và ContextMenu theo pattern hiện có
  - Requires: Task 10

## Task Dependency Graph

```json
{
  "waves": [
    { "wave": 1, "tasks": [1, 2] },
    { "wave": 2, "tasks": [3] },
    { "wave": 3, "tasks": [4, 5, 6, 9, 10] },
    { "wave": 4, "tasks": [7, 8, 14, 15, 16, 17] },
    { "wave": 5, "tasks": [11, 13] },
    { "wave": 6, "tasks": [12, 18] }
  ]
}
```

## Notes

- `MangoTangoBrush` và `TextOnMangoTangoBrush` phải tồn tại trong theme. Kiểm tra `Themes/Base/Colors/` trước khi dùng; nếu chưa có thì thêm vào `SemanticTokens.xaml`.
- Overlay dùng P/Invoke `SetWindowsHookEx` — cần `using System.Runtime.InteropServices` và `[DllImport("user32.dll")]`, tương tự `ScreenPositionPickerOverlay.cs`.
- Mouse move events rất nhiều — cân nhắc throttle (chỉ ghi khi chuột di chuyển > 5px so với điểm trước) để tránh JSON quá lớn.
- `MacroDataJson` có thể lớn — không giới hạn độ dài khi serialize vào workflow JSON (khác với output values thông thường).
- Executor dùng `await Task.Delay(delta)` — nếu delta < 0 (do clock skew) thì bỏ qua delay.
