# Kiến trúc Tổng quan — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cấu trúc tổng quan của hệ thống node và các file cần tạo.

---

## 1. Tổng quan kiến trúc

```
Models/Nodes/YourNode.cs                          ← Data model
Views/NodeControls/YourNodeControl.cs             ← UI trên canvas
Views/Overlays/YourNodeDialog.xaml                ← Dialog cấu hình
Views/Overlays/YourNodeDialog.xaml.cs             ← Code-behind
ViewModels/YourNodeDialogViewModel.cs             ← ViewModel
Services/Rendering/YourNodeRenderer.cs            ← Render node lên canvas
Services/Workflow/NodeExecutors/YourNodeExecutor.cs ← Logic thực thi (nếu cần)
```

**Nguyên tắc cốt lõi**: Mọi logic chung đã nằm trong base classes. File của bạn chỉ chứa những gì **đặc thù** của node đó.

| Base class | Xử lý gì |
|-----------|---------|
| `BaseNodeControlHelper` | Hover, keyboard port, zoom, title position, visibility sync, dialog, cleanup |
| `BaseNodeDialog` | Vị trí dialog, lưu title, load Inputs/Outputs, color picker, brush resolver |
| `BaseNodeDialogViewModel` | NodeTitle, TitleDisplayMode, TitleColorMode, collections, commands |
| `WorkflowNode` | INotifyPropertyChanged, TitleDisplayMode, TitleColorMode, TitleColorKey |

---

## 2. Danh sách file cần tạo

| # | File | Vị trí |
|---|------|--------|
| 1 | `YourNode.cs` | `Models/Nodes/` |
| 2 | `YourNodeControl.cs` | `Views/NodeControls/` |
| 3 | `YourNodeDialog.xaml` + `.xaml.cs` | `Views/Overlays/` |
| 4 | `YourNodeDialogViewModel.cs` | `ViewModels/` |
| 5 | `YourNodeRenderer.cs` | `Services/Rendering/` |
| 6 | `YourNodeExecutor.cs` | `Services/Workflow/NodeExecutors/` (nếu cần) |

Ngoài ra cần đăng ký ở 11 chỗ — xem `NODE_REGISTRATION.md`.

---

## Cấu trúc thư mục

```
FlowMy/
├── Models/
│   └── Nodes/
│       ├── WorkflowNode.cs (base class)
│       ├── NodeType.cs (enum)
│       └── YourNode.cs (node mới)
├── Views/
│   ├── NodeControls/
│   │   ├── Helpers/
│   │   │   └── BaseNodeControlHelper.cs (base class)
│   │   └── YourNodeControl.cs (node mới)
│   ├── Overlays/
│   │   ├── BaseNodeDialog.xaml/.cs (base class)
│   │   └── YourNodeDialog.xaml/.cs (node mới)
│   └── WorkflowEditors/
│       ├── WorkflowEditorWindow.xaml (palette)
│       ├── WorkflowEditorWindow.TemplateNodeHandler.cs
│       └── WorkflowEditorWindow.NodeActions.cs
├── ViewModels/
│   ├── Base/
│   │   └── BaseNodeDialogViewModel.cs (base class)
│   └── YourNodeDialogViewModel.cs (node mới)
├── Services/
│   ├── Rendering/
│   │   ├── _NodeRenderer.cs (dispatcher)
│   │   ├── PortRenderer.cs
│   │   ├── NodeChrome.cs
│   │   └── YourNodeRenderer.cs (node mới)
│   └── Workflow/
│       ├── WorkflowExecutionService.cs
│       └── NodeExecutors/
│           └── YourNodeExecutor.cs (node mới, nếu cần)
```

---

## Flow dữ liệu

1. **User kéo node từ palette** → `TemplateFactory.Create()` tạo `YourNode` instance
2. **Renderer** → `YourNodeRenderer.RenderNode()` gọi `YourNodeControl.CreateBorder()` tạo UI
3. **User right-click node** → `BaseNodeControlHelper` mở `YourNodeDialog`
4. **Dialog** → `YourNodeDialogViewModel` bind properties từ `YourNode`
5. **User save** → `OnSaveTitle()` sync VM → node → request sync data panels
6. **Workflow chạy** → `YourNodeExecutor.ExecuteAsync()` (nếu có) thực thi logic
7. **Save/Load workflow** → `FileWorkflowPersistenceService` serialize/deserialize node

---

## Base Classes Summary

### WorkflowNode (Models/Nodes/WorkflowNode.cs)

Base class cho mọi node model.

**Đã có sẵn:**
- `INotifyPropertyChanged` implementation
- `PropertyChanged` event
- `OnPropertyChanged()` method
- `TitleDisplayMode` property (default: `Always`)
- `TitleColorMode` property (default: `NodeColor`)
- `TitleColorKey` property (default: `null`)
- `NotifyTitleChanged()` method
- `Title` property
- `Type` property (NodeType enum)
- `Id` property (string GUID)
- `X`, `Y` properties (position)
- `ColorKey` property
- `NodeBrush` property
- `Ports` collection
- `DynamicInputs`, `DynamicOutputs` collections

**KHÔNG khai báo lại trong derived class:**
- `INotifyPropertyChanged` interface
- `PropertyChanged` event
- `OnPropertyChanged()` method
- `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey` properties
- `NotifyTitleChanged()` method (chỉ override nếu cần thêm logic)

---

### BaseNodeControlHelper (Views/NodeControls/Helpers/BaseNodeControlHelper.cs)

Helper class để tạo UI node trên canvas với fluent API.

**Đã xử lý:**
- `MouseEnter` / `MouseLeave` + hover state
- `PreviewKeyDown` + arrow key port positioning
- `PropertyChanged` thread-safe cho: `NodeBrush`, `Title`, `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey`
- `Loaded` / `SizeChanged` / `LayoutUpdated` / `Unloaded`
- `DependencyPropertyDescriptor` visibility sync (tránh memory leak)
- `DispatcherTimer` throttling cho title position update
- `NodeDialogManager` dialog management
- Canvas ZIndex = 20000 cho title TextBlock

**Fluent API methods:**
- `.Initialize(border, titleTextBlock, node, host)` - Khởi tạo
- `.WithTitleManagement()` - Loaded, SizeChanged, LayoutUpdated + zoom
- `.WithHoverBehavior()` - MouseEnter/Leave + focus
- `.WithKeyboardPorts()` - Arrow keys → port position
- `.WithPropertySync(handlers)` - PropertyChanged thread-safe
- `.WithDialogSupport(factory)` - MouseRightButtonUp → mở dialog
- `.WithCleanup()` - Unloaded → stop timer, remove title, dispose
- `.WithVisibilitySync()` - DependencyPropertyDescriptor visibility
- `.WithCanvasIntegration()` - Loaded → add title to WorkflowCanvas
- `.Build()` - Áp dụng tất cả + lưu context

---

### BaseNodeDialog (Views/Overlays/BaseNodeDialog.xaml/.cs)

Base class cho dialog cấu hình node.

**Đã xử lý:**
- Snap phải màn hình (SourceInitialized)
- Clamp khi resize (SizeChanged)
- Lưu title khi đóng (Closing → SaveTitleCommand)
- `CloseButton_Click` - SaveTitleCommand + Close()
- `TitleColorComboBox_SelectionChanged` - update preview
- `UpdateTitleColorPreview()` - set preview background
- `ShowColorPicker(string? hex)` - trả về `#RRGGBB` hoặc null
- `ResolveBrush(string? key, Brush fallback)` - hỗ trợ hex, named color, resource key
- `GetThemeBrush(string key, Brush fallback)` - instance method
- `GetThemeColor(string key, Color fallback)` - instance method
- `BindThemeResource(element, dp, key)` - bind DynamicResource cho code-behind control
- Load Inputs/Outputs (Loaded → GetInputsPanel() / GetOutputsPanel())

**Phải override:**
- `GetInputsPanel()` - trả về StackPanel chứa inputs (null nếu không có)
- `GetOutputsPanel()` - trả về StackPanel chứa outputs (null nếu không có)

**Có thể override:**
- `OnLoaded()` - logic sau Loaded
- `BeforeSaveOnClose()` - flush binding trước khi đóng (xử lý cả Alt+F4)
- `CloseButton_Click()` - logic thêm trước khi đóng

---

### BaseNodeDialogViewModel (ViewModels/Base/BaseNodeDialogViewModel.cs)

Base class cho ViewModel dialog.

**Observable Properties đã có sẵn:**
- `NodeTitle` - sync từ `node.Title`
- `TitleDisplayMode` - sync từ node
- `TitleColorMode` - sync từ node
- `TitleColorKey` - sync từ node; update canvas ngay khi thay đổi
- `InputPortPosition` - từ port IN đầu tiên
- `OutputPortPosition` - từ port OUT đầu tiên
- `Inputs` - ObservableCollection<InputItemViewModel> load từ DynamicInputs
- `Outputs` - ObservableCollection<OutputItemViewModel> load từ DynamicOutputs
- `ReuseRoutes` - ObservableCollection<ReuseRouteItemViewModel> load từ connections

**Collections tĩnh (KHÔNG khai báo lại):**
- `TitleDisplayModeOptions` - Hidden / Hover / Always
- `TitleColorOptions` - NodeColor / LimeGreen / PrimaryBrush / ...
- `PortPositionOptions` - Left / Top / Right / Bottom
- `ConnectionLineStyleOptions` - WorkflowDefault / Bezier / Orthogonal / ...

**Commands đã có sẵn:**
- `RunSingleNodeCommand`
- `RunWorkflowFromNodeCommand`
- `SaveTitleCommand`

**Protected helpers (gọi trực tiếp):**
- `RefreshAllNodesWithOutputs(target)` - lấy tất cả nodes có DynamicOutputs
- `GetOutputKeysForNode(nodeId)` - lấy output keys của node theo ID
- `FillOutputKeys(nodeId, target)` - điền output keys vào collection
- `CreateDataSourceOption(node)` - tạo option đầy đủ icon/brush
- `ResolveNodeTypeDisplayName(type)` - NodeType → display name
- `ResolveNodeIconKey(type)` - NodeType → icon key
- `ResolveTextOnNodeBrush(colorKey)` - ColorKey → TextOnXxxBrush

**Phải override:**
- `GetDefaultTitle()` - trả về default title string

**Có thể override:**
- `OnSaveTitle()` - lưu thêm properties ngoài Title/TitleDisplayMode/TitleColorMode
- `LoadInputs()` - filter inputs
- `LoadOutputs()` - filter outputs
- `LoadReuseRoutes()` - load reuse routes
- `SupportsReuseRoutes` - return false nếu node không dùng ReuseRoutes

---

## Quy trình tạo node mới

1. Đọc `NODE_MODEL.md` - Tạo file Model
2. Đọc `NODE_CONTROL.md` - Tạo file NodeControl
3. Đọc `NODE_DIALOG_XAML.md` - Tạo file Dialog XAML
4. Đọc `NODE_DIALOG_CODEBEHIND.md` - Tạo file Dialog code-behind
5. Đọc `NODE_VIEWMODEL.md` - Tạo file ViewModel
6. Đọc `NODE_RENDERER.md` - Tạo file Renderer
7. Nếu node có logic thực thi - Đọc `NODE_EXECUTOR.md`
8. Đọc `NODE_PERSISTENCE.md` - Thêm serialize/deserialize
9. Đọc `NODE_REGISTRATION.md` - Đăng ký vào 11 chỗ

Xem checklist nhanh trong `NODE_CREATION_SUMMARY.md`.
