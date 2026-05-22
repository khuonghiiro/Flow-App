# Design Document

## Overview

Macro Recorder Node là một node mới trong FlowMy WPF cho phép ghi lại và phát lại thao tác chuột/bàn phím. Node tuân theo kiến trúc chuẩn của FlowMy với đầy đủ Model, NodeControl, Dialog, ViewModel, Renderer, Executor và Persistence.

## Architecture

```
Models/Nodes/MacroRecorderNode.cs                          ← Data model + MacroPlaybackMode enum
Models/Nodes/MacroAction.cs                                ← DTO cho từng action được ghi
Views/NodeControls/MacroRecorderNodeControl.cs             ← UI trên canvas (static class)
Views/Overlays/MacroRecorderNodeDialog.xaml + .cs          ← Dialog cấu hình (2 tab)
Views/Overlays/MacroRecorderOverlay.xaml + .cs             ← Overlay toàn màn hình ghi thao tác
ViewModels/MacroRecorderNodeDialogViewModel.cs             ← ViewModel cho dialog
Services/Rendering/MacroRecorderNodeRenderer.cs            ← Render node lên canvas
Services/Workflow/NodeExecutors/MacroRecorderNodeExecutor.cs ← Logic phát lại macro
```

Các file hiện có cần sửa đổi:
- `Models/Nodes/NodeType.cs` — thêm `MacroRecorder`
- `Workflow/TemplateFactory.cs` — thêm `CreateMacroRecorderNode`
- `Services/ServiceCollectionExtensions.cs` — đăng ký renderer
- `Services/Rendering/_NodeRenderer.cs` — thêm renderer vào dictionary
- `Services/Workflow/WorkflowExecutionService.cs` — thêm executor
- `Services/Workflow/FileWorkflowPersistenceService.cs` — serialize/deserialize
- `ViewModels/WorkflowEditorViewModel.cs` — serialize/deserialize
- `Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs` — copy/paste
- `Views/WorkflowEditorWindow.xaml` — palette entry

## Components and Interfaces

### MacroRecorderNode (Model)

```csharp
public enum MacroPlaybackMode { Once, Repeat }

public sealed class MacroRecorderNode : WorkflowNode
{
    // Properties: OutputKey, MacroDataJson, PlaybackMode, RepeatIntervalMs, RepeatCount
    // Constructor: Type = NodeType.MacroRecorder, ColorKey = "MangoTango"
    //              Ports: 1 IN Left (Info), 1 OUT Right (SunsetOrange)
    //              DynamicOutputs: "macroData" (String)
}
```

### MacroAction (DTO)

```csharp
public sealed class MacroAction
{
    public int SequenceNumber { get; set; }
    public string Type { get; set; }       // "MouseClick" | "KeyPress" | "MouseMove"
    public long Timestamp { get; set; }    // Unix ms
    public int X { get; set; }
    public int Y { get; set; }
    public string? Button { get; set; }    // "Left" | "Right" | null
    public string? Key { get; set; }       // tên phím hoặc null
}
```

### MacroRecorderNodeControl

Static class, `CreateBorder(MacroRecorderNode, Window?, IWorkflowEditorHost?)`. Icon `"chart-network light"`, grid 60×60. Fluent API đầy đủ với `WithDialogSupport` mở `MacroRecorderNodeDialog`.

### MacroRecorderNodeDialogViewModel

```csharp
public partial class MacroRecorderNodeDialogViewModel : BaseNodeDialogViewModel
{
    [ObservableProperty] string _outputKey;
    [ObservableProperty] string _macroDataJson;
    [ObservableProperty] string _selectedPlaybackMode;
    [ObservableProperty] int _repeatIntervalMs;
    [ObservableProperty] int _repeatCount;

    public bool IsRepeatVisible => SelectedPlaybackMode == "Lặp lại";
    public bool CanExportJson => !string.IsNullOrWhiteSpace(MacroDataJson);
    public ObservableCollection<string> PlaybackModeOptions { get; }
}
```

### MacroRecorderOverlay

Cửa sổ toàn màn hình trong suốt (Topmost, AllowsTransparency). State machine:

```
IDLE → (Ctrl+Alt+Shift) → RECORDING → (Ctrl+Alt+Shift | ESC với data) → DONE
                                     → (ESC không có data) → CANCELLED
```

Dùng `WH_KEYBOARD_LL` và `WH_MOUSE_LL` hooks. Canvas overlay hiển thị trail polyline, ellipse click, label phím. Property `RecordedJson` và `HasData` trả về kết quả.

### MacroRecorderNodeExecutor

```csharp
public bool CanExecute(WorkflowNode node) => node is MacroRecorderNode;

public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
{
    // 1. Parse MacroDataJson → List<MacroAction>
    // 2. Nếu rỗng: TraverseOutputsAsync và return
    // 3. Lặp cycles lần (Once=1, Repeat=RepeatCount)
    //    - Delay RepeatIntervalMs giữa các cycle (trừ cycle đầu)
    //    - Phát lại từng action với timing delta
    // 4. Publish output vào scoped store
    // 5. TraverseOutputsAsync
}
```

### MacroRecorderNodeRenderer

Implement `INodeRenderer`. Pattern giống `MouseEventNodeRenderer`: `RenderNode`, `UpdateNodePosition`, `RemoveNode`, `RemoveAllNodeVisuals`.

## Data Models

### MacroDataJson format

```json
[
  { "sequenceNumber": 1, "type": "MouseClick", "timestamp": 1000, "x": 100, "y": 200, "button": "Left", "key": null },
  { "sequenceNumber": 2, "type": "MouseMove",  "timestamp": 1050, "x": 150, "y": 220, "button": null,  "key": null },
  { "sequenceNumber": 3, "type": "KeyPress",   "timestamp": 1200, "x": 150, "y": 220, "button": null,  "key": "Enter" }
]
```

### Persistence keys

| Key | Type | Mô tả |
|-----|------|-------|
| `OutputKey` | string | Tên output key |
| `MacroDataJson` | string | JSON array actions |
| `PlaybackMode` | string | "Once" hoặc "Repeat" |
| `RepeatIntervalMs` | int | Delay giữa chu kỳ (ms) |
| `RepeatCount` | int | Số lần lặp |

## Error Handling

- **MacroDataJson rỗng khi execute**: executor bỏ qua, gọi `TraverseOutputsAsync` và tiếp tục — không throw.
- **JSON không hợp lệ khi import**: hiển thị `MessageBox` lỗi, không cập nhật `MacroDataJson`.
- **JSON không hợp lệ khi execute**: bắt `JsonException`, gọi `env.OnNodeFailed`, throw để workflow biết lỗi.
- **ESC overlay không có data**: hủy bỏ, restore app, không thay đổi `MacroDataJson` hiện tại.
- **Hook không thể đăng ký**: log lỗi, đóng overlay, restore app.

## Testing Strategy

Các property có thể test tự động:

- **P1**: Default values của `MacroRecorderNode` — tạo instance, kiểm tra từng property
- **P2**: `IsRepeatVisible` phụ thuộc `SelectedPlaybackMode` — test ViewModel
- **P3**: `CanExportJson` phụ thuộc `MacroDataJson` — test ViewModel
- **P4**: `SequenceNumber` tăng dần từ 1 — test overlay recording logic
- **P5**: Timing delta khi phát lại — test executor với mock service
- **P6**: Persistence round-trip — serialize → deserialize → so sánh
- **P7**: Executor không throw khi `MacroDataJson` rỗng
- **P8**: `RepeatCount >= 1` luôn đúng
- **P9**: `RepeatIntervalMs >= 0` luôn đúng

## Correctness Properties

### Property 1: Default values của MacroRecorderNode
Khi tạo `new MacroRecorderNode()`, các default values phải đúng: `OutputKey == "macroData"`, `MacroDataJson == ""`, `PlaybackMode == MacroPlaybackMode.Once`, `RepeatIntervalMs == 500`, `RepeatCount == 1`, `Ports.Count == 2` (1 IN Left, 1 OUT Right).
**Validates: Requirements 1.2, 1.3**

### Property 2: IsRepeatVisible phụ thuộc PlaybackMode
Với mọi giá trị `SelectedPlaybackMode`: khi `"Lặp lại"` thì `IsRepeatVisible == true`; khi `"Chạy 1 lần"` thì `IsRepeatVisible == false`.
**Validates: Requirements 3.4**

### Property 3: CanExportJson phụ thuộc MacroDataJson
`MacroDataJson` là null hoặc rỗng → `CanExportJson == false`. `MacroDataJson` có nội dung → `CanExportJson == true`.
**Validates: Requirements 3.5**

### Property 4: SequenceNumber tăng dần từ 1
Với N actions được ghi, `actions[i].SequenceNumber == i + 1` với mọi `i` trong `[0, N-1]`.
**Validates: Requirements 5.7**

### Property 5: Timing delta khi phát lại
Với array actions có timestamps `[t0, t1, t2, ...]`, delay trước action `i` (i > 0) phải bằng `actions[i].Timestamp - actions[i-1].Timestamp` (ms).
**Validates: Requirements 7.5**

### Property 6: Persistence round-trip
Với mọi `MacroRecorderNode` có properties hợp lệ, sau khi serialize → deserialize, tất cả properties phải bằng nhau (OutputKey, MacroDataJson, PlaybackMode, RepeatIntervalMs, RepeatCount).
**Validates: Requirements 9.1, 9.2**

### Property 7: Executor không throw khi MacroDataJson rỗng
Khi `MacroDataJson` là null hoặc rỗng, `ExecuteAsync` phải hoàn thành mà không throw exception.
**Validates: Requirements 7.2**

### Property 8: RepeatCount không nhỏ hơn 1
Với mọi giá trị gán cho `RepeatCount`, `node.RepeatCount >= 1` luôn đúng.
**Validates: Requirements 1.3**

### Property 9: RepeatIntervalMs không âm
Với mọi giá trị gán cho `RepeatIntervalMs`, `node.RepeatIntervalMs >= 0` luôn đúng.
**Validates: Requirements 1.3**
