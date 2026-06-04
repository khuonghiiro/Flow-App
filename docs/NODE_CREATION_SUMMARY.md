# Tóm tắt Hướng dẫn Tạo Node — FlowMy

> Cập nhật: 2026-05-16
> **Đây là file tổng hợp** - tham chiếu đến các file chi tiết cho từng phần.
> Khi tạo node mới, đọc file này trước để hiểu tổng quan, sau đó đọc file chi tiết tương ứng.

---

## Tổng quan nhanh

Tạo 1 node mới cần **6 file chính** + đăng ký ở **11 chỗ** trong hệ thống.

### 6 file cần tạo

| # | File | Vị trí | Mục đích |
|---|------|--------|---------|
| 1 | `YourNode.cs` | `Models/Nodes/` | Data model |
| 2 | `YourNodeControl.cs` | `Views/NodeControls/` | UI trên canvas |
| 3 | `YourNodeDialog.xaml` + `.xaml.cs` | `Views/Overlays/` | Dialog cấu hình |
| 4 | `YourNodeDialogViewModel.cs` | `ViewModels/` | ViewModel cho dialog |
| 5 | `YourNodeRenderer.cs` | `Services/Rendering/` | Render node lên canvas |
| 6 | `YourNodeExecutor.cs` | `Services/Workflow/NodeExecutors/` | Logic thực thi (nếu cần) |

### 11 chỗ phải đăng ký

1. **NodeType enum** - `Models/Nodes/NodeType.cs`
2. **Palette XAML** - `Views/WorkflowEditorWindow.xaml` (ToolTip + ContextMenu)
3. **TemplateFactory** - `Services/Workflow/TemplateFactory.cs`
4. **Icon canvas** - `Views/WorkflowEditors/WorkflowEditorWindow.TemplateNodeHandler.cs`
5. **Icon execution trace** - `ViewModels/WorkflowEditorViewModel.cs`
6. **Renderer registration** - `Services/Rendering/_NodeRenderer.cs`
7. **DI container** - Đăng ký renderer singleton
8. **Executor registration** - `Services/Workflow/WorkflowExecutionService.cs` (nếu có executor)
9. **Copy/Paste** - `Services/Interaction/WorkflowEditorEventService.cs`
10. **Copy/Paste properties** - `Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs`
11. **Remap NodeId** - `Views/WorkflowEditors/WorkflowEditorWindow.MultiNodeClipboard.cs` (nếu có *NodeId fields)

---

## Tài liệu chi tiết theo từng phần

### 1. Kiến trúc tổng quan
📄 **File**: `NODE_ARCHITECTURE.md`
- Cấu trúc file và folder
- Base classes và trách nhiệm
- Nguyên tắc cốt lõi

### 2. Node Model
📄 **File**: `NODE_MODEL.md`
- Kế thừa từ `WorkflowNode`
- Properties đặc thù
- Ports
- Checklist

### 3. NodeControl (UI trên canvas)
📄 **File**: `NODE_CONTROL.md`
- `BaseNodeControlHelper` fluent API
- Template chuẩn
- Custom property handlers
- Checklist

### 4. Dialog XAML
📄 **File**: `NODE_DIALOG_XAML.md`
- Template XAML chuẩn
- x:Name bắt buộc
- Theme system (DynamicResource)
- Dialog sizing

### 5. Dialog Code-behind
📄 **File**: `NODE_DIALOG_CODEBEHIND.md`
- `BaseNodeDialog` đã xử lý gì
- Template chuẩn
- Override methods
- Color picker
- Checklist

### 6. ViewModel
📄 **File**: `NODE_VIEWMODEL.md`
- `BaseNodeDialogViewModel` đã có sẵn
- Template chuẩn
- Collections và commands
- Checklist

### 7. Renderer
📄 **File**: `NODE_RENDERER.md`
- Interface `INodeRenderer`
- Template chuẩn
- Đăng ký renderer
- Checklist

### 8. Executor
📄 **File**: `NODE_EXECUTOR.md`
- Interface `INodeExecutor`
- `NodeExecutionEnvironment`
- Template chuẩn
- Đăng ký executor
- Checklist

### 9. Persistence
📄 **File**: `NODE_PERSISTENCE.md`
- Serialize/Deserialize
- Copy/Paste logic
- Deep copy lists

### 10. Đăng ký node vào hệ thống
📄 **File**: `NODE_REGISTRATION.md`
- 11 chỗ phải đăng ký
- IconKey / ColorKey — 4 chỗ phải khớp
- Theme System — DynamicResource
- ExecutionId & Scoped Outputs
- Checklist đăng ký

### 11. Các trường hợp đặc biệt
📄 **File**: `NODE_SPECIAL_CASES.md`
- Node có embedded title
- Node cần cleanup thêm
- Node có resize handles
- Node diamond/conditional
- Dialog không có Inputs/Outputs
- Node không dùng ReuseRoutes

### 12. Lỗi thường gặp
📄 **File**: `NODE_COMMON_ERRORS.md`
- Lỗi NodeControl
- Lỗi Dialog Code-behind
- Lỗi ViewModel
- Lỗi Node Model
- Cách tránh

### 13. Reference Implementations
📄 **File**: `NODE_REFERENCE_IMPLEMENTATIONS.md`
- Danh sách file mẫu thực tế
- Đặc điểm mỗi file

### 14. Dynamic Input/Output
📄 **File**: `NODE_DYNAMIC_IO.md`
- Node cho phép user thêm/xóa inputs và outputs
- Node Model với List
- ViewModel với ObservableCollection
- Dialog XAML với ItemsControl
- Persistence
- Copy/Paste
- Checklist

### 15. Multi-row NodeSearchComboBox
📄 **File**: `NODE_MULTIROW_COMBOBOX.md`
- Tránh lỗi đồng bộ khi có nhiều dòng
- Quy tắc bắt buộc
- Pattern hoàn chỉnh
- Checklist

### 16. Liquid Glass
📄 **File**: `NODE_LIQUID_GLASS.md`
- Hỗ trợ giao diện Kính Lỏng
- Tự động cho node thông thường
- Khi nào cần code thêm
- Node diamond/polygon
- Checklist

---

## Quy trình tạo node mới (Quick Start)

1. **Đọc `NODE_ARCHITECTURE.md`** - Hiểu cấu trúc tổng quan
2. **Đọc `NODE_MODEL.md`** - Tạo file Model
3. **Đọc `NODE_CONTROL.md`** - Tạo file NodeControl
4. **Đọc `NODE_DIALOG_XAML.md`** - Tạo file Dialog XAML
5. **Đọc `NODE_DIALOG_CODEBEHIND.md`** - Tạo file Dialog code-behind
6. **Đọc `NODE_VIEWMODEL.md`** - Tạo file ViewModel
7. **Đọc `NODE_RENDERER.md`** - Tạo file Renderer
8. **Nếu node có logic thực thi** - Đọc `NODE_EXECUTOR.md`
9. **Đọc `NODE_PERSISTENCE.md`** - Thêm serialize/deserialize
10. **Đọc `NODE_REGISTRATION.md`** - Đăng ký vào 11 chỗ
11. **Nếu node cần dynamic I/O** - Đọc `NODE_DYNAMIC_IO.md`
12. **Nếu node có multi-row combobox** - Đọc `NODE_MULTIROW_COMBOBOX.md`
13. **Nếu node dùng diamond shape** - Đọc `NODE_LIQUID_GLASS.md`
14. **Gặp lỗi** - Đọc `NODE_COMMON_ERRORS.md`

---

## Quy tắc quan trọng

### Base classes đã xử lý — KHÔNG viết lại

| Base class | Xử lý gì | File chi tiết |
|-----------|---------|---------------|
| `BaseNodeControlHelper` | Hover, keyboard, zoom, title, dialog, cleanup | `NODE_CONTROL.md` |
| `BaseNodeDialog` | Snap, resize, save title, load Inputs/Outputs | `NODE_DIALOG_CODEBEHIND.md` |
| `BaseNodeDialogViewModel` | Properties, collections, commands chung | `NODE_VIEWMODEL.md` |
| `WorkflowNode` | INotifyPropertyChanged, TitleDisplayMode, TitleColorMode | `NODE_MODEL.md` |

### 4 chỗ phải khớp — IconKey / ColorKey

| # | File | Khai báo gì |
|---|------|-------------|
| 1 | `Views/WorkflowEditorWindow.xaml` | Tag + Background + ConverterParameter + Fill |
| 2 | `WorkflowEditorWindow.TemplateNodeHandler.cs` | GetIconNameForNodeType |
| 3 | `TemplateFactory.cs` | ColorKey trong CreateYourNode() |
| 4 | `WorkflowEditorViewModel.cs` | ResolveNodeIconKey |

Chi tiết: `NODE_REGISTRATION.md` → §11.A

### Theme System — DynamicResource bắt buộc

KHÔNG hardcode màu trong XAML dialog. Dùng DynamicResource để dialog tự đổi màu theo theme.

Chi tiết: `NODE_REGISTRATION.md` → §11.B

### ExecutionId & Scoped Outputs

Khi viết Executor, LUÔN dùng APIs có `env` parameter để tránh đọc nhầm kết quả của run khác.

Chi tiết: `NODE_REGISTRATION.md` → §11.C

---

## Checklist nhanh

### Node Model
- [ ] Kế thừa WorkflowNode (KHÔNG thêm INotifyPropertyChanged)
- [ ] KHÔNG khai báo lại: PropertyChanged, OnPropertyChanged, TitleDisplayMode, TitleColorMode, TitleColorKey
- [ ] Thêm NodeType enum value
- [ ] KHÔNG thêm ports trong constructor (TemplateFactory sẽ tạo — tránh duplicate ports)
- [ ] Dùng OnPropertyChanged() trong mọi setter

### NodeControl
- [ ] public static class
- [ ] node.TitleTextBlockUI = titleTextBlock ← BẮT BUỘC
- [ ] border.Tag = node ← BẮT BUỘC
- [ ] customPropertyHandlers có ColorKey → update icon fill
- [ ] Gọi .WithCleanup() và .Build()
- [ ] KHÔNG tự viết: MouseEnter/Leave, PreviewKeyDown, PropertyChanged, etc.

### Dialog XAML
- [ ] Kế thừa BaseNodeDialog
- [ ] x:Name: TitleColorComboBox, TitleColorPreview, InputsPanel, OutputsPanel
- [ ] Dùng DynamicResource cho mọi màu/style
- [ ] KHÔNG đặt Height cứng
- [ ] Cả ToolTip lẫn ContextMenu trong palette

### Dialog Code-behind
- [ ] Constructor: InitializeComponent() → new ViewModel → InitializeBase(vm, owner)
- [ ] Gọi UpdateTitleColorPreview() nếu có TitleColorPreview
- [ ] Override GetInputsPanel() và GetOutputsPanel()
- [ ] KHÔNG tự viết: CloseButton_Click, TitleColorComboBox_SelectionChanged, etc.

### ViewModel
- [ ] Kế thừa BaseNodeDialogViewModel
- [ ] Constructor gọi base(node, host)
- [ ] Override GetDefaultTitle() ← BẮT BUỘC
- [ ] KHÔNG khai báo lại: TitleDisplayModeOptions, TitleColorOptions, etc.
- [ ] Gọi _node.NotifyTitleChanged() ở cuối OnSaveTitle()

### Renderer
- [ ] Implement INodeRenderer (4 methods)
- [ ] Constructor nhận PortRenderer + IWorkflowEditorHostAccessor
- [ ] RenderNode: gọi NodeChrome.Apply() sau CreateBorder()
- [ ] RenderNode: attach 5 mouse handlers
- [ ] RenderNode: set ContextMenu = null (nếu dùng right-click dialog)
- [ ] UpdateNodePosition: update border, title TextBlock, ports
- [ ] RemoveNode: remove title TextBlock + set null
- [ ] Đăng ký vào NodeRenderer + DI container

### Executor (nếu có)
- [ ] Implement INodeExecutor (CanExecute + ExecuteAsync)
- [ ] Dùng env.Service.ResolveDynamicValueForExecution() để lấy input
- [ ] Gọi env.OnNodeCompleted?.Invoke() sau khi xong
- [ ] Gọi env.OnNodeFailed?.Invoke() + throw khi có lỗi
- [ ] Gọi env.TraverseOutputsAsync(node) ở cuối
- [ ] Đăng ký vào _nodeExecutors list

### Đăng ký
- [ ] NodeType enum value
- [ ] Palette XAML (ToolTip + ContextMenu)
- [ ] TemplateFactory
- [ ] WorkflowEditorWindow.TemplateNodeHandler.cs
- [ ] WorkflowEditorViewModel.cs
- [ ] _NodeRenderer.cs (field + constructor + branches)
- [ ] DI container
- [ ] WorkflowExecutionService (nếu có executor)
- [ ] WorkflowEditorEventService.cs (Ctrl+C + Ctrl+V)
- [ ] WorkflowEditorWindow.NodeActions.cs (CreateDuplicateNodeInstance)
- [ ] WorkflowEditorWindow.MultiNodeClipboard.cs (RemapNodeReferenceIds)

---

## Khi gặp vấn đề

| Vấn đề | File chi tiết |
|--------|---------------|
| Không biết cấu trúc tổng quan | `NODE_ARCHITECTURE.md` |
| Model bị lỗi ambiguous PropertyChanged | `NODE_MODEL.md` → §3.4 |
| NodeControl không update UI | `NODE_CONTROL.md` → §4.6 |
| Dialog không load Inputs/Outputs | `NODE_DIALOG_CODEBEHIND.md` → §6.5 |
| ViewModel binding không hoạt động | `NODE_VIEWMODEL.md` → §7.4 |
| Renderer không render node | `NODE_RENDERER.md` → §8.3 |
| Executor không chạy đúng | `NODE_EXECUTOR.md` → §9.5 |
| Persistence không save/load | `NODE_PERSISTENCE.md` |
| Icon/màu sai sau đăng ký | `NODE_REGISTRATION.md` → §11.A |
| Dialog màu không đổi theo theme | `NODE_REGISTRATION.md` → §11.B |
| Executor đọc nhầm output | `NODE_REGISTRATION.md` → §11.C |
| Node cần đặc biệt (diamond, resize, etc.) | `NODE_SPECIAL_CASES.md` |
| Lỗi cụ thể | `NODE_COMMON_ERRORS.md` |
| Node cần dynamic I/O | `NODE_DYNAMIC_IO.md` |
| Multi-row combobox bị đồng bộ | `NODE_MULTIROW_COMBOBOX.md` |
| Liquid Glass không hoạt động | `NODE_LIQUID_GLASS.md` |
| Cần xem mẫu thực tế | `NODE_REFERENCE_IMPLEMENTATIONS.md` |

---

## Lưu ý quan trọng

1. **Base classes đã xử lý logic chung** — File của bạn chỉ chứa những gì đặc thù của node đó
2. **4 chỗ phải khớp IconKey/ColorKey** — Thiếu bất kỳ chỗ nào → node bị fallback icon/màu sai
3. **DynamicResource bắt buộc** — KHÔNG hardcode màu trong XAML dialog
4. **ExecutionId & Scoped Outputs** — LUÔN dùng APIs có `env` trong executor
5. **WithCleanup() là bắt buộc** — Không bao giờ bỏ qua để tránh memory leak
6. **Deep copy lists trong Copy/Paste** — KHÔNG gán trực tiếp reference
7. **Remap *NodeId fields** — Mọi field dạng *NodeId phải remap sau multi-paste

---

*Tài liệu này tổng hợp từ codebase thực tế sau khi hoàn thành refactor 38 NodeControl classes và các base classes.*
