# Callback Node Implementation Summary

## Overview
Callback Node đã được triển khai thành công theo yêu cầu. Node này cho phép chạy lại workflow từ một node đã chọn với giới hạn số lần callback.

## Files Created/Modified

### 1. Model
**File**: `Models/Nodes/CallbackNode.cs`
- Properties:
  - `TargetNodeId`: ID của node cần callback
  - `MaxCallbackCount`: Số lần tối đa callback (mặc định 3)
  - `TitleDisplayMode`: Hiển thị tiêu đề (Hidden/Hover/Always)
  - `TitleColorMode`: Chế độ màu tiêu đề
  - `TitleColorKey`: Key màu tùy chỉnh
  - `TitleTextBlockUI`: Reference đến UI element

### 2. Dialog (View)
**File**: `Views/Overlays/CallbackNodeDialog.xaml`
- 2 tabs: Logic và Cấu hình
- Tab Logic:
  - TitleDisplayMode ComboBox
  - Target Node ComboBox (chọn node callback)
  - Max Callback Count TextBox
  - Hướng dẫn sử dụng
- Tab Cấu hình:
  - Vị trí cổng IN
  - Lưu ý về node không có port OUT

### 3. Dialog Code-behind
**File**: `Views/Overlays/CallbackNodeDialog.xaml.cs`
- Kế thừa BaseNodeDialog
- Implement GetInputsPanel và GetOutputsPanel
- CloseButton_Click handler

### 4. ViewModel
**File**: `ViewModels/CallbackNodeDialogViewModel.cs`
- Kế thừa BaseNodeDialogViewModel
- Properties:
  - `TargetNodeId`: Binding với combobox
  - `MaxCallbackCount`: Binding với textbox
  - `AvailableNodes`: Danh sách node có thể callback (trừ Start, End, và chính nó)
- Methods:
  - `RefreshAvailableNodes()`: Cập nhật danh sách node
  - `OnSaveTitle()`: Lưu properties về model

### 5. NodeControl
**File**: `Views/NodeControls/CallbackNodeControl.cs`
- Static class với CreateBorder method
- Features:
  - Icon: `arrows-turn-right regular` với màu `CrimsonRose`
  - TitleTextBlock với TitleDisplayMode support
  - 7 event handlers (MouseEnter/Leave, PropertyChanged, VisibilityDescriptor, Loaded, SizeChanged, Unloaded, LayoutUpdated)
  - Throttled title position updates
  - OpenNodeDialog với release mouse capture

### 6. Renderer
**File**: `Services/Rendering/CallbackNodeRenderer.cs`
- Implement INodeRenderer interface
- Methods:
  - `RenderNode()`: Render node trên canvas
  - `UpdateNodePosition()`: Cập nhật vị trí node và title
  - `RemoveNode()`: Cleanup khi xóa node
  - `ResolvePortColor()`: Xác định màu port

### 7. Executor
**File**: `Services/Workflow/NodeExecutors/CallbackNodeExecutor.cs`
- Logic callback với tracking số lần callback
- Features:
  - Dictionary tracking callback count per execution
  - Validation target node existence
  - Giới hạn số lần callback (MaxCallbackCount)
  - Tạo execution path mới khi callback
  - Cleanup counters sau execution
- Static methods:
  - `ResetCallbackCounters()`: Reset tất cả counters
  - `ResetCallbackCounters(executionId)`: Reset counter cho execution cụ thể

### 8. NodeType Enum
**File**: `Models/Nodes/NodeType.cs`
- Thêm `Callback` vào enum

### 9. TemplateFactory
**File**: `Workflow/TemplateFactory.cs`
- Thêm case "Callback" vào switch
- `CreateCallbackNode()` method:
  - ColorKey: CrimsonRose
  - NodeBrush: CrimsonRoseBrush
  - Chỉ có input port (left), không có output port

### 10. WorkflowEditorWindow Palette
**File**: `Views/WorkflowEditorWindow.xaml`
- Thêm Callback node vào palette
- Background: CrimsonRoseBrush
- Icon: arrows-turn-right regular
- Tooltip và ContextMenu với mô tả đầy đủ

### 11. Service Registration
**File**: `Services/ServiceCollectionExtensions.cs`
- Đăng ký `CallbackNodeRenderer` trong DI container

**File**: `Services/Rendering/_NodeRenderer.cs`
- Thêm `_callbackNodeRenderer` field
- Thêm vào constructor parameters
- Thêm render logic trong `RenderNode()`
- Thêm update logic trong `UpdateNodePosition()`
- Thêm remove logic trong `RemoveNode()`
- Thêm cleanup trong `RemoveAllNodeVisuals()`

**File**: `Services/Workflow/WorkflowExecutionService.cs`
- Đăng ký `CallbackNodeExecutor` trong executor list

### 12. Persistence
**File**: `Services/Workflow/FileWorkflowPersistenceService.cs`
- **Serialize** (GetNodeProperties):
  - TargetNodeId
  - MaxCallbackCount
  - TitleDisplayMode
  - TitleColorMode
  - TitleColorKey
- **Deserialize** (RestoreNodeProperties):
  - Khôi phục tất cả properties
  - Parse enum values
  - Validation

## Features

### 1. Callback Logic
- Chỉ có input port, không có output port
- Khi logic chạy vào node callback, nó sẽ jump về node đã chọn
- Chạy lại workflow từ node đó
- Tracking số lần callback trong mỗi execution flow

### 2. Giới Hạn Callback
- MaxCallbackCount default = 3
- Tracking per execution ID
- Error message khi vượt quá giới hạn
- Auto cleanup counters sau execution

### 3. Node Selection
- ComboBox cho phép chọn node callback
- Filter: loại bỏ Start, End, và chính node callback
- Hiển thị Title và ID của node

### 4. UI/UX
- ColorKey: CrimsonRose (màu đỏ hồng)
- Icon: arrows-turn-right regular
- TitleDisplayMode support (Hidden/Hover/Always)
- TitleColorMode support (NodeColor/CustomColor)
- Hướng dẫn sử dụng trong dialog

### 5. Error Handling
- Validate target node existence
- Validate target node ID
- Error message khi vượt quá MaxCallbackCount
- Try-catch trong executor

## Usage Example

### Workflow Flow
```
A → B → C → D → E → H
            ↓
            G (Callback Node)
```

- G chọn target node = C
- MaxCallbackCount = 3
- Khi E chạy vào G:
  1. G callback về C → C→D→E
  2. Nếu E lại vào G: G callback về C → C→D→E (lần 2)
  3. Nếu E lại vào G: G callback về C → C→D→E (lần 3)
  4. Nếu E lại vào G: Workflow dừng với error "đã đạt giới hạn 3 lần"

## Testing Checklist

- [x] Create node from palette
- [x] Node hiển thị đúng màu và icon
- [x] Open dialog bằng right-click
- [x] Chọn target node trong combobox
- [x] Thay đổi MaxCallbackCount
- [x] Save và load workflow
- [x] Properties được persist đúng
- [ ] Copy/Paste node (cần implement)
- [ ] Execute workflow với callback
- [ ] Test giới hạn MaxCallbackCount
- [ ] Test callback trong loop body
- [ ] Test callback với multiple branches

## Notes

1. **Executor Tracking**: Callback counter được tracking theo execution ID để tránh conflict giữa các workflow runs khác nhau

2. **Cleanup**: Counters được tự động cleanup sau khi execution hoàn thành hoặc fail

3. **Scope**: Callback node có thể gọi bất kỳ node nào trong phạm vi workflow (trừ Start và End)

4. **Loop Body**: Callback node có thể hoạt động trong loop body

5. **Copy/Paste**: Chưa implement copy/paste support. Cần thêm vào:
   - `WorkflowEditorEventService.cs` (Ctrl+C/V handlers)
   - `WorkflowEditorWindow.NodeActions.cs` (CreateDuplicateNodeInstance)

## Future Enhancements

1. **Copy/Paste Support**: Thêm support copy/paste cho CallbackNode
2. **Visual Feedback**: Hiển thị số lần callback hiện tại trong UI khi execute
3. **Callback History**: Track và hiển thị lịch sử callback
4. **Conditional Callback**: Cho phép callback dựa trên điều kiện
5. **Multiple Target Nodes**: Cho phép chọn nhiều target nodes với điều kiện

## Conclusion

Callback Node đã được implement đầy đủ theo yêu cầu:
- ✅ ColorKey: CrimsonRose
- ✅ Icon: arrows-turn-right regular
- ✅ Chỉ có input port, không có output port
- ✅ ComboBox chọn target node
- ✅ Giới hạn số lần callback (MaxCallbackCount)
- ✅ Callback logic với execution tracking
- ✅ Persistence (save/load)
- ✅ Full MVVM pattern
- ✅ TitleDisplayMode + TitleColorMode support

Node sẵn sàng để testing và sử dụng!
