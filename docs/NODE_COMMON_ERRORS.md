# Lỗi Thường Gặp — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích các lỗi thường gặp và cách tránh.

---

## 13. Lỗi thường gặp

### NodeControl

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| 1 | `ArgumentException` khi đổi workflow/theme | `DependencyPropertyDescriptor.AddValueChanged` không được remove | Dùng `.WithVisibilitySync()` — tự track và remove handler |
| 2 | Title không hiện sau load workflow | Quên `.WithTitleManagement()` hoặc `.WithCanvasIntegration()` | Luôn gọi cả hai nếu node có floating title |
| 3 | Memory leak sau nhiều lần đổi workflow | Quên `.WithCleanup()` | `.WithCleanup()` là bắt buộc — không bao giờ bỏ qua |
| 4 | Icon fill không cập nhật khi đổi ColorKey | Không khai báo `ColorKey` handler trong `customPropertyHandlers` | Luôn thêm `[nameof(WorkflowNode.ColorKey)] = ctx => { iconSvg.Fill = ... }` |
| 5 | Port position không đổi khi nhấn arrow key | Quên `.WithKeyboardPorts()` | `.WithHoverBehavior()` tự set `Focusable=true`; `.WithKeyboardPorts()` đăng ký handler |
| 6 | `node.TitleTextBlockUI` là null sau render | Quên `node.TitleTextBlockUI = titleTextBlock` trước `Build()` | Gán trước khi gọi `Initialize` |
| 7 | Dialog mở nhưng node vẫn bị drag | Không gọi `.WithDialogSupport()` đúng cách | `WithDialogSupport` tự xử lý `ReleaseMouseCapture`, `DraggedNode = null` |

### Dialog Code-behind

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| D1 | Title color preview không cập nhật khi mở | Quên gọi `UpdateTitleColorPreview()` trong constructor | Gọi sau `InitializeBase()` |
| D2 | Title color preview không cập nhật khi đổi combobox | XAML không wire `SelectionChanged="TitleColorComboBox_SelectionChanged"` | Thêm vào XAML |
| D3 | `TitleColorPreview` không tìm thấy | `x:Name` sai hoặc nằm trong DataTemplate | Đặt đúng `x:Name="TitleColorPreview"` ở cấp trực tiếp |
| D4 | Inputs/Outputs không load | `GetInputsPanel()` trả về null hoặc sai panel | Trả về đúng `StackPanel` có `x:Name="InputsPanel"` |
| D5 | Binding mất khi đóng bằng Alt+F4 | Chỉ override `CloseButton_Click`, không override `BeforeSaveOnClose` | Override `BeforeSaveOnClose()` |
| D6 | Dialog không snap vào cạnh phải màn hình | Tự set `WindowStartupLocation` sau `InitializeBase` | Không set — base đã set `Manual` |

### ViewModel

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| V1 | `TitleDisplayModeOptions` binding không hoạt động | Khai báo lại local với tên khác | Xóa local copy — base đã có |
| V2 | `GetOutputKeysForNode` không tìm thấy | Tự viết lại với `private` | Dùng `protected` method từ base |
| V3 | `CreateDataSourceOption` tạo option thiếu icon/brush | Tự tạo `new WorkflowDataSourceOption { NodeId, Title }` | Dùng `CreateDataSourceOption(node)` từ base |
| V4 | `LoadReuseRoutes()` override thừa | Override cả `SupportsReuseRoutes => false` lẫn `LoadReuseRoutes()` | Chỉ cần `SupportsReuseRoutes => false` |
| V5 | Inputs không load khi mở dialog | Quên gọi `base(node, host)` | Base ctor tự gọi `LoadInputs()` |

### Node Model

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| M1 | Compiler error: ambiguous `PropertyChanged` | Derived class khai báo lại event | Xóa — base đã có |
| M2 | Compiler error: ambiguous `OnPropertyChanged` | Derived class khai báo lại method | Xóa — base đã có |
| M3 | `TitleDisplayMode` luôn là `Always` dù muốn `Hidden` | Quên set trong constructor | Thêm `TitleDisplayMode = TitleDisplayMode.Hidden;` trong constructor |
| M4 | `if node is XxxNode` chain trong service | Không biết base đã có property | Dùng `node.TitleDisplayMode` trực tiếp |
| M5 | `if (node is INotifyPropertyChanged npc)` | Không biết WorkflowNode đã implement | Dùng `node.PropertyChanged +=` trực tiếp |
