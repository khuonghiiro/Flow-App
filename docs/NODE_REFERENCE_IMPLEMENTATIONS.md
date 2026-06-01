# Reference Implementations — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này liệt kê các file mẫu thực tế để tham khảo.

---

## 14. Reference Implementations

Xem các file sau làm mẫu thực tế:

| File | Đặc điểm |
|------|----------|
| `Views/NodeControls/OutputNodeControl.cs` | Standard pattern, 1 custom handler (ColorKey) |
| `Views/NodeControls/StorageNodeControl.cs` | 2 custom handlers (ColorKey + IsInputMode) |
| `Views/NodeControls/MouseEventNodeControl.cs` | 2 custom handlers (ColorKey + MouseButton → đổi icon) |
| `Views/NodeControls/ScreenCaptureNodeControl.cs` | Embedded title, partial fluent API (dummy title) |
| `Views/NodeControls/VideoProcessingNodeControl.cs` | Resizable border, extra Unloaded cleanup |
| `Views/NodeControls/WebNodeControl.cs` | Extra Unloaded cleanup (WebView2 dispose) |
| `Views/Overlays/DelayNodeDialog.xaml` | Dialog mẫu chuẩn theme + responsive |
| `Views/Overlays/AssignDataNodeDialog.xaml` | Dialog với custom property handlers |
| `Views/Overlays/CodeNodeDialog.xaml` | Dynamic Input mapping + Dynamic Output keys |
| `ViewModels/DelayNodeDialogViewModel.cs` | ViewModel chuẩn với OnSaveTitle |
| `ViewModels/StorageNodeDialogViewModel.cs` | ViewModel với filter đặc biệt |
| `Services/Workflow/NodeExecutors/StorageNodeExecutor.cs` | Executor mẫu với scoped output |
| `Services/Workflow/NodeExecutors/DelayNodeExecutor.cs` | Executor đơn giản |
| `Services/Rendering/StorageNodeRenderer.cs` | Renderer mẫu chuẩn |

---

*Tài liệu này tổng hợp từ codebase thực tế sau khi hoàn thành refactor 38 NodeControl classes và các base classes. Cập nhật: 2026-05-16.*
