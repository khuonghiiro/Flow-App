# Tài liệu Tạo Node FlowMy — Chi tiết từng File

> Tài liệu gốc `NODE_CREATION_GUIDE.md` đã được tách nhỏ thành 17 file chuyên biệt để dễ tra cứu và AI có thể đọc từng phần riêng biệt.

---

## 📋 File Tổng Hợp

### [NODE_CREATION_SUMMARY.md](NODE_CREATION_SUMMARY.md)
- **Chức năng**: File tổng hợp với quick start, checklist nhanh, và tham chiếu đến tất cả file chi tiết
- **Khi nào dùng**: Bắt buộc đọc đầu tiên khi tạo node mới để hiểu tổng quan quy trình
- **Nội dung chính**:
  - Tổng quan nhanh về 6 file cần tạo
  - 11 chỗ phải đăng ký
  - Quy trình tạo node mới step-by-step
  - Quy tắc quan trọng (4 chỗ phải khớp IconKey/ColorKey, DynamicResource, ExecutionId)
  - Checklist nhanh cho từng phần
  - Bảng tra cứu khi gặp vấn đề

---

## 📁 File Chi Tiết Theo Chức Năng

### 1. [NODE_ARCHITECTURE.md](NODE_ARCHITECTURE.md) — Kiến Trúc Tổng Quan
- **Chức năng**: Giải thích cấu trúc tổng quan hệ thống, base classes, và flow dữ liệu
- **Khi nào dùng**: Đọc đầu tiên để hiểu kiến trúc trước khi bắt đầu code
- **Nội dung chính**:
  - Cấu trúc 6 file cần tạo
  - Cấu trúc thư mục chi tiết
  - Flow dữ liệu từ palette → render → dialog → executor
  - Base classes summary (WorkflowNode, BaseNodeControlHelper, BaseNodeDialog, BaseNodeDialogViewModel)
  - Quy trình tạo node mới

### 2. [NODE_MODEL.md](NODE_MODEL.md) — Node Model
- **Chức năng**: Hướng dẫn tạo file Model cho node (`Models/Nodes/YourNode.cs`)
- **Khi nào dùng**: Khi cần tạo hoặc sửa file Model
- **Nội dung chính**:
  - Template chuẩn cho Node Model
  - Properties đã có sẵn trong WorkflowNode (KHÔNG khai báo lại)
  - Cách dùng OnPropertyChanged() từ base
  - Checklist Node Model
  - Lỗi thường gặp và cách tránh

### 3. [NODE_CONTROL.md](NODE_CONTROL.md) — NodeControl (UI trên Canvas)
- **Chức năng**: Hướng dẫn tạo file NodeControl (`Views/NodeControls/YourNodeControl.cs`)
- **Khi nào dùng**: Khi cần tạo hoặc sửa file NodeControl
- **Nội dung chính**:
  - BaseNodeControlHelper đã xử lý gì (KHÔNG viết lại)
  - Template chuẩn với fluent API
  - Custom property handlers (ColorKey, etc.)
  - Fluent API reference (WithTitleManagement, WithHoverBehavior, etc.)
  - Checklist NodeControl
  - Lỗi thường gặp

### 4. [NODE_DIALOG_XAML.md](NODE_DIALOG_XAML.md) — Dialog XAML
- **Chức năng**: Hướng dẫn tạo file Dialog XAML (`Views/Overlays/YourNodeDialog.xaml`)
- **Khi nào dùng**: Khi cần tạo hoặc sửa Dialog XAML
- **Nội dung chính**:
  - Template XAML chuẩn
  - x:Name bắt buộc (TitleColorComboBox, TitleColorPreview, InputsPanel, OutputsPanel)
  - Theme System — DynamicResource bắt buộc (KHÔNG hardcode màu)
  - Dialog sizing (KHÔNG đặt Height cứng)
  - Reference implementations

### 5. [NODE_DIALOG_CODEBEHIND.md](NODE_DIALOG_CODEBEHIND.md) — Dialog Code-behind
- **Chức năng**: Hướng dẫn tạo file Dialog code-behind (`Views/Overlays/YourNodeDialog.xaml.cs`)
- **Khi nào dùng**: Khi cần tạo hoặc sửa Dialog code-behind
- **Nội dung chính**:
  - BaseNodeDialog đã có sẵn (KHÔNG viết lại)
  - Template chuẩn
  - Override methods bắt buộc (GetInputsPanel, GetOutputsPanel)
  - Flush binding trước khi đóng (BeforeSaveOnClose)
  - Color picker tùy chỉnh
  - Checklist Dialog Code-behind
  - Lỗi thường gặp

### 6. [NODE_VIEWMODEL.md](NODE_VIEWMODEL.md) — ViewModel
- **Chức năng**: Hướng dẫn tạo file ViewModel (`ViewModels/YourNodeDialogViewModel.cs`)
- **Khi nào dùng**: Khi cần tạo hoặc sửa ViewModel
- **Nội dung chính**:
  - BaseNodeDialogViewModel đã có sẵn (KHÔNG khai báo lại)
  - Observable Properties đã có sẵn
  - Collections tĩnh (KHÔNG khai báo lại)
  - Commands đã có sẵn
  - Protected helpers (gọi trực tiếp)
  - Template chuẩn
  - Khi nào cần RefreshAvailableNodes tùy chỉnh
  - Checklist ViewModel
  - Lỗi thường gặp

### 7. [NODE_RENDERER.md](NODE_RENDERER.md) — Renderer
- **Chức năng**: Hướng dẫn tạo file Renderer (`Services/Rendering/YourNodeRenderer.cs`)
- **Khi nào dùng**: Khi cần tạo hoặc sửa Renderer
- **Nội dung chính**:
  - Interface INodeRenderer (4 methods)
  - Template chuẩn
  - Đăng ký Renderer (3 bước: field, branch, DI)
  - Checklist Renderer
  - Reference implementations

### 8. [NODE_EXECUTOR.md](NODE_EXECUTOR.md) — Executor
- **Chức năng**: Hướng dẫn tạo file Executor (`Services/Workflow/NodeExecutors/YourNodeExecutor.cs`)
- **Khi nào dùng**: Khi node có logic thực thi riêng (không phải pass-through)
- **Nội dung chính**:
  - Interface INodeExecutor
  - NodeExecutionEnvironment (env.Service, env.Connections, env.TraverseOutputsAsync, etc.)
  - Template chuẩn
  - Resolve input từ node nguồn
  - Đăng ký Executor
  - Checklist Executor
  - Reference implementations

### 9. [NODE_PERSISTENCE.md](NODE_PERSISTENCE.md) — Persistence
- **Chức năng**: Hướng dẫn thêm serialize/deserialize và copy/paste cho node mới
- **Khi nào dùng**: Khi cần lưu/load workflow hoặc copy/paste node
- **Nội dung chính**:
  - **Kiến trúc Partial Class**: `FileWorkflowPersistenceService` chỉ là dispatcher, logic nằm trong `Services/Workflow/Persistence/*.cs`
  - Cấu trúc 12 file partial (NodeProperties_Shared, NodeProperties_Misc, NodeProperties_WebNode, etc.)
  - Cách tạo `RestoreYourNodeProperties()` và `GetYourNodeProperties()` trong file partial phù hợp
  - Cách thêm dispatch (else if) vào file chính
  - Shared properties tự động (RunMode, ReuseRoutes, DynamicInputs, Title — KHÔNG cần viết lại)
  - Template file partial mới
  - Copy/Paste logic trong CreateDuplicateNodeInstance
  - Deep copy lists (KHÔNG gán trực tiếp reference)

### 10. [NODE_REGISTRATION.md](NODE_REGISTRATION.md) — Đăng ký Node
- **Chức năng**: Hướng dẫn đăng ký node vào 11 chỗ trong hệ thống
- **Khi nào dùng**: Bắt buộc sau khi tạo xong tất cả file
- **Nội dung chính**:
  - 11 chỗ phải đăng ký (NodeType enum, Palette XAML, TemplateFactory, etc.)
  - **11.A IconKey/ColorKey — 4 chỗ phải khớp** (Palette, TemplateNodeHandler, TemplateFactory, WorkflowEditorViewModel)
  - **11.B Theme System — DynamicResource bắt buộc** (KHÔNG hardcode màu)
  - **11.C ExecutionId & Scoped Outputs** (LUÔN dùng APIs có env trong executor)
  - Checklist đăng ký

### 11. [NODE_SPECIAL_CASES.md](NODE_SPECIAL_CASES.md) — Các Trường Hợp Đặc Biệt
- **Chức năng**: Hướng dẫn xử lý các trường hợp đặc biệt
- **Khi nào dùng**: Khi node cần đặc biệt (embedded title, diamond, resize, cleanup, etc.)
- **Nội dung chính**:
  - Node có embedded title (không dùng floating canvas title)
  - Node cần cleanup thêm (WebView2, subscriptions)
  - Node có resize handles
  - Node diamond/conditional (transparent background)
  - Dialog không có Inputs/Outputs
  - Node không dùng ReuseRoutes
  - Files KHÔNG dùng BaseNodeControlHelper

### 12. [NODE_COMMON_ERRORS.md](NODE_COMMON_ERRORS.md) — Lỗi Thường Gặp
- **Chức năng**: Danh sách lỗi thường gặp và cách tránh
- **Khi nào dùng**: Khi gặp lỗi hoặc muốn tránh lỗi
- **Nội dung chính**:
  - Lỗi NodeControl (7 lỗi phổ biến)
  - Lỗi Dialog Code-behind (6 lỗi phổ biến)
  - Lỗi ViewModel (5 lỗi phổ biến)
  - Lỗi Node Model (5 lỗi phổ biến)
  - Bảng nguyên nhân và cách tránh cho từng lỗi

### 13. [NODE_REFERENCE_IMPLEMENTATIONS.md](NODE_REFERENCE_IMPLEMENTATIONS.md) — File Mẫu Thực Tế
- **Chức năng**: Danh sách file mẫu thực tế để tham khảo
- **Khi nào dùng**: Khi cần xem code mẫu thực tế
- **Nội dung chính**:
  - 17 file mẫu với đặc điểm từng file
  - OutputNodeControl, StorageNodeControl, MouseEventNodeControl, ScreenCaptureNodeControl, etc.
  - DelayNodeDialog, AssignDataNodeDialog, CodeNodeDialog
  - DelayNodeDialogViewModel, StorageNodeDialogViewModel
  - StorageNodeExecutor, DelayNodeExecutor
  - StorageNodeRenderer

### 14. [NODE_DYNAMIC_IO.md](NODE_DYNAMIC_IO.md) — Dynamic Input/Output
- **Chức năng**: Hướng dẫn tạo node cho phép user thêm/xóa inputs và outputs
- **Khi nào dùng**: Khi node cần user cấu hình nhiều nguồn dữ liệu hoặc nhiều output keys
- **Nội dung chính**:
  - Node Model với List thay vì DynamicInputs cố định
  - ViewModel với ObservableCollection + Add/Remove Commands
  - Dialog XAML với ItemsControl
  - Persistence — Serialize/Deserialize list
  - Copy/Paste — Deep copy lists
  - Checklist Dynamic Input/Output

### 15. [NODE_MULTIROW_COMBOBOX.md](NODE_MULTIROW_COMBOBOX.md) — Multi-row NodeSearchComboBox
- **Chức năng**: Hướng dẫn tránh lỗi đồng bộ khi có nhiều dòng NodeSearchComboBox
- **Khi nào dùng**: Khi dialog có nhiều dòng combobox (Node + Key)
- **Nội dung chính**:
  - Các lỗi thường gặp (tất cả dòng hiển thị cùng 1 node/key, selection bị reset, etc.)
  - 6 quy tắc bắt buộc
  - Pattern hoàn chỉnh cho multi-row với Node + Key
  - Checklist Multi-row NodeSearchComboBox

### 16. [NODE_LIQUID_GLASS.md](NODE_LIQUID_GLASS.md) — Liquid Glass
- **Chức năng**: Hướng dẫn hỗ trợ giao diện Kính Lỏng
- **Khi nào dùng**: Khi node cần hỗ trợ giao diện Kính Lỏng
- **Nội dung chính**:
  - Cách hoạt động — KHÔNG cần code thêm cho node thông thường
  - Khi nào CẦN code thêm — Node diamond/polygon
  - Satellite circles (ConditionalDiamondControl)
  - LiquidGlassHelper API Reference
  - NodeChrome.Apply — Logic tự động cho diamond nodes
  - Hover handlers — Tự động skip diamond nodes
  - Theme switch — Tự động refresh
  - Checklist Liquid Glass cho node mới

---

## 🚀 Quy Trình Sử Dụng

### Khi tạo node mới (Bắt buộc theo thứ tự):
1. Đọc **NODE_CREATION_SUMMARY.md** để hiểu tổng quan
2. Đọc **NODE_ARCHITECTURE.md** để hiểu cấu trúc
3. Đọc **NODE_MODEL.md** → Tạo file Model
4. Đọc **NODE_CONTROL.md** → Tạo file NodeControl
5. Đọc **NODE_DIALOG_XAML.md** → Tạo file Dialog XAML
6. Đọc **NODE_DIALOG_CODEBEHIND.md** → Tạo file Dialog code-behind
7. Đọc **NODE_VIEWMODEL.md** → Tạo file ViewModel
8. Đọc **NODE_RENDERER.md** → Tạo file Renderer
9. Nếu node có logic thực thi → Đọc **NODE_EXECUTOR.md**
10. Đọc **NODE_PERSISTENCE.md** → Thêm serialize/deserialize
11. Đọc **NODE_REGISTRATION.md** → Đăng ký vào 11 chỗ

### Khi cần logic cụ thể:
- **Cần tạo Model** → Đọc NODE_MODEL.md
- **Cần tạo NodeControl** → Đọc NODE_CONTROL.md
- **Cần tạo Dialog XAML** → Đọc NODE_DIALOG_XAML.md
- **Cần tạo Dialog code-behind** → Đọc NODE_DIALOG_CODEBEHIND.md
- **Cần tạo ViewModel** → Đọc NODE_VIEWMODEL.md
- **Cần tạo Renderer** → Đọc NODE_RENDERER.md
- **Cần tạo Executor** → Đọc NODE_EXECUTOR.md
- **Cần thêm persistence** → Đọc NODE_PERSISTENCE.md
- **Cần đăng ký node** → Đọc NODE_REGISTRATION.md

### Khi gặp vấn đề:
- **Gặp lỗi cụ thể** → Đọc NODE_COMMON_ERRORS.md
- **Node cần đặc biệt** → Đọc NODE_SPECIAL_CASES.md
- **Cần xem code mẫu** → Đọc NODE_REFERENCE_IMPLEMENTATIONS.md
- **Node cần dynamic I/O** → Đọc NODE_DYNAMIC_IO.md
- **Multi-row combobox bị lỗi** → Đọc NODE_MULTIROW_COMBOBOX.md
- **Liquid Glass không hoạt động** → Đọc NODE_LIQUID_GLASS.md

### Khi AI cần đọc:
- **Tạo node mới** → AI đọc NODE_CREATION_SUMMARY.md + NODE_ARCHITECTURE.md
- **Node thiếu logic Model** → AI chỉ đọc NODE_MODEL.md
- **Node thiếu logic NodeControl** → AI chỉ đọc NODE_CONTROL.md
- **Node thiếu logic Dialog** → AI chỉ đọc NODE_DIALOG_XAML.md + NODE_DIALOG_CODEBEHIND.md
- **Node thiếu logic ViewModel** → AI chỉ đọc NODE_VIEWMODEL.md
- **Node thiếu logic Renderer** → AI chỉ đọc NODE_RENDERER.md
- **Node thiếu logic Executor** → AI chỉ đọc NODE_EXECUTOR.md
- **Gặp lỗi** → AI đọc NODE_COMMON_ERRORS.md hoặc file tương ứng

---

## ⚠️ Quy Tắc Quan Trọng

### 4 chỗ phải khớp — IconKey/ColorKey:
1. Palette XAML (Views/WorkflowEditorWindow.xaml)
2. TemplateNodeHandler.cs (GetIconNameForNodeType)
3. TemplateFactory.cs (CreateYourNode)
4. WorkflowEditorViewModel.cs (ResolveNodeIconKey)

### Theme System — DynamicResource bắt buộc:
- KHÔNG hardcode màu trong XAML dialog
- Dùng DynamicResource để dialog tự đổi màu theo theme

### ExecutionId & Scoped Outputs:
- Trong Executor: LUÔN dùng APIs có `env` parameter
- KHÔNG dùng NodeDataPanelService.ResolveDynamicValueByKey trong executor

### Base classes đã xử lý — KHÔNG viết lại:
- WorkflowNode đã có INotifyPropertyChanged, OnPropertyChanged, TitleDisplayMode, TitleColorMode, TitleColorKey
- BaseNodeControlHelper đã xử lý hover, keyboard, zoom, title, dialog, cleanup
- BaseNodeDialog đã xử lý snap, resize, save title, load Inputs/Outputs
- BaseNodeDialogViewModel đã có properties, collections, commands chung

---

*Tài liệu này tổng hợp từ codebase thực tế sau khi hoàn thành refactor 38 NodeControl classes và các base classes. Cập nhật: 2026-05-16.*