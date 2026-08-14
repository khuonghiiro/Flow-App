# FlowMy — WPF Workflow Automation Desktop App

**FlowMy** là ứng dụng desktop WPF (.NET 8) cho phép người dùng xây dựng và chạy workflow tự động hóa bằng canvas node-based. Mỗi node đại diện cho một hành động (HTTP request, chạy code JS, xử lý ảnh/video, Git, bàn phím/chuột, v.v.), nối với nhau bằng đường kết nối để tạo thành luồng thực thi. Workflow được lưu dưới dạng JSON tại `Documents\FlowMy\Workflow_Json\`.

---

## Mục lục

1. [Yêu cầu & Build](#1-yêu-cầu--build)
2. [Cấu trúc thư mục](#2-cấu-trúc-thư-mục)
3. [Kiến trúc tổng quan](#3-kiến-trúc-tổng-quan)
4. [Các thư mục chính](#4-các-thư-mục-chính)
5. [Hệ thống Node](#5-hệ-thống-node)
6. [Hệ thống Theme](#6-hệ-thống-theme)
7. [Quy ước đặt tên](#7-quy-ước-đặt-tên)
8. [Tài liệu bổ sung](#8-tài-liệu-bổ-sung)

---

## 1. Yêu cầu & Build

- **Visual Studio 2022+** (hoặc Rider)
- **.NET 8 SDK** (`net8.0-windows`)
- Mở `FlowMy.sln` → Set `FlowMy` làm Startup Project → `F5`

**Thư mục dữ liệu runtime** (tự tạo khi chạy lần đầu):
- Workflow JSON: `Documents\FlowMy\Workflow_Json\`
- Log: `Documents\FlowMy\logs\yyyy_MM_dd.log`
- Preferences: `%LocalAppData%\FlowMy\`

**Các dependency chính** (xem `FlowMy.csproj`):

| Package | Dùng cho |
|---------|---------|
| `CommunityToolkit.Mvvm` | MVVM base, `[ObservableProperty]`, `[RelayCommand]` |
| `Microsoft.Web.WebView2` | WebNode — nhúng trình duyệt Chromium |
| `Jint` | CodeNode — chạy JavaScript trong process |
| `LibGit2Sharp` | GitSourceNode — thao tác Git |
| `AvalonEdit` | Code editor trong dialog |
| `PropertyChanged.Fody` | Auto INotifyPropertyChanged |
| `Prism.DryIoc` | DI container |
| `Serilog` | Logging |
| `OxyPlot.Wpf` | Chart/graph |
| `Markdig` | Markdown rendering |
| `SharpVectors.Reloaded` | SVG rendering |
| `Aspose.Cells` | Excel (thư viện local tại `Library/`) |

---

## 2. Cấu trúc thư mục

```text
FlowMy/
│
├── App.xaml / App.xaml.cs          → Entry point WPF: DI bootstrap, theme, single-instance mutex,
│                                     WebView2 warm-up, tray icon, global exception handler
├── FlowMy.csproj                   → Project file (.NET 8 WPF)
├── appsettings.json                → Cấu hình app (API endpoints, settings)
├── IconResources.cs                → Icon key → SVG path mapping (dùng trong code)
├── FodyWeavers.xml                 → Cấu hình PropertyChanged.Fody
│
├── Assets/
│   ├── Fonts/                      → fa-solid-900.ttf (Font Awesome)
│   ├── Icons/                      → SVG icon files (copy to output)
│   └── Images/                     → Logo, .ico, .png
│
├── Behaviors/
│   └── AsyncUrlImageBehavior.cs    → Async image loading behavior cho XAML
│
├── Controls/                       → 28 UserControl tái sử dụng
│   ├── NodeSearchComboBoxUserControl   → ComboBox tìm kiếm node (dùng trong dialog)
│   ├── SyntaxHighlightCodeEditor       → Code editor với syntax highlight
│   ├── MarkdownTextBlock               → Render Markdown
│   ├── LoadingUserControl              → Loading spinner
│   ├── DataGridUserControl             → DataGrid có phân trang
│   ├── IconSelectorUserControl         → Chọn icon SVG
│   └── ... (CheckBoxListView, DateTimePicker, MultiSelectComboBox, v.v.)
│
├── Converters/                     → 27 IValueConverter / IMultiValueConverter cho XAML binding
│   ├── IconKeyToPathConverter          → Chuyển icon key → URI SVG (dùng khắp nơi)
│   ├── ColorThemeConverter             → Màu theo theme
│   └── ... (visibility, string format, layout math, v.v.)
│
├── Effects/
│   ├── VideoEqEffect.cs            → WPF ShaderEffect cho video EQ
│   └── video_eq.ps                 → HLSL pixel shader (compiled)
│
├── Extensions/
│   ├── ColorThemeExtensions.cs     → Extension methods cho màu sắc
│   ├── ThemeExtensions.cs          → Apply/load theme từ Settings
│   ├── ServiceCollectionExtensions.cs → Đăng ký DI (AddWorkflowEditorServices, v.v.)
│   └── VisualTreeExtensions.cs     → Traverse WPF visual tree
│
├── Helpers/                        → Static helpers dùng chung
│   ├── DynamicColorHelper.cs       → Tạo màu động từ ColorKey
│   ├── MessageBoxHelper.cs         → Wrapper MessageBox
│   ├── WindowHelper.cs             → BringToFront, center window
│   ├── AvalonEditMonokaiHelper.cs  → Monokai theme cho AvalonEdit
│   ├── OxyPlotColorHelper.cs       → Màu cho OxyPlot chart
│   └── ... (image/video processing helpers, pagination, JS syntax)
│
├── Interfaces/
│   ├── IDateTimePickerConfig.cs
│   └── IRowStyleData.cs
│
├── Models/
│   ├── WorkflowNode.cs             → Base class cho mọi node (INotifyPropertyChanged,
│   │                                 TitleDisplayMode, TitleColorMode, Ports, DynamicOutputs)
│   ├── WorkflowConnection.cs       → Kết nối giữa 2 node (FromNode/Port → ToNode/Port)
│   ├── NodePort.cs                 → Port IN/OUT của node
│   ├── FloatingWidgetConfig.cs     → Cấu hình floating widget overlay
│   ├── WorkflowDynamicDataPort.cs  → Dynamic output port (key, type, value)
│   ├── Nodes/                      → 45+ concrete node types (xem §5)
│   ├── Persistence/                → WorkflowDto, NodeDto, ConnectionDto (JSON serialization)
│   └── ListBoxs/                   → Model cho ListBox/TreeView chuyên biệt
│
├── Routers/
│   └── ManualViewMappings.cs       → Static View↔ViewModel route dictionary
│                                     (hiện để trống — routing qua DI/factory)
│
├── Services/
│   ├── FloatingWidgetManager.cs    → Quản lý floating widget overlay (open/close/track)
│   ├── ViewFactoryService.cs       → Tạo View theo ViewModel type
│   ├── ViewCacheService.cs         → Cache View instance
│   ├── TrayService.cs              → System tray icon + menu
│   ├── WidgetShortcutScanner.cs    → Quét workflow JSON tìm node có FloatingWidget
│   ├── RegisterAppToStartup.cs     → Đăng ký app chạy cùng Windows
│   ├── ServiceCollectionExtensions.cs → Đăng ký toàn bộ DI
│   │
│   ├── Geometry/                   → 4 kiểu vẽ đường kết nối
│   │   ├── BezierGeometryGenerator.cs
│   │   ├── OrthogonalGeometryGenerator.cs
│   │   ├── OrthogonalV2GeometryGenerator.cs
│   │   └── StraightGeometryGenerator.cs
│   │
│   ├── Git/
│   │   ├── GitService.cs           → LibGit2Sharp wrapper (clone, pull, commit, push)
│   │   ├── GitCmdStorageService.cs → Lưu/đọc Git repo config
│   │   ├── GitRepoStorageService.cs
│   │   └── GitCmdProcessManager.cs → Quản lý cmd.exe process cho Git
│   │
│   ├── Interaction/                → Xử lý tương tác canvas
│   │   ├── ConnectionHandler.cs    → Vẽ/xóa connection giữa nodes
│   │   ├── DragDropHandler.cs      → Kéo thả node trên canvas
│   │   ├── ZoomPanHandler.cs       → Zoom/pan canvas
│   │   ├── CollisionResolver.cs    → Tránh node chồng lên nhau
│   │   ├── NodeDialogManager.cs    → Quản lý dialog node (tránh mở 2 dialog cùng lúc)
│   │   ├── GlobalKeyboardHookService.cs → Hook bàn phím toàn hệ thống
│   │   ├── KeyboardInputService.cs → Gửi phím tắt
│   │   ├── MouseInputService.cs    → Gửi sự kiện chuột
│   │   ├── ToastNotificationService.cs → Hiển thị toast notification
│   │   ├── WorkflowEditorEventService.cs → Ctrl+C/V, Delete, keyboard shortcuts
│   │   ├── WorkflowNodeHelper.cs   → Utility cho node (find, filter)
│   │   └── IWorkflowEditorHost.cs  → Interface host cho canvas editor
│   │
│   ├── Layout/                     → Auto-layout thuật toán
│   │   ├── AutoLayoutService.cs
│   │   ├── GridLayout.cs
│   │   ├── HierarchicalLayout.cs
│   │   └── ILayoutAlgorithm.cs
│   │
│   ├── Rendering/                  → ~40 files renderer
│   │   ├── _NodeRenderer.cs        → Dispatcher: route đến renderer đúng theo NodeType
│   │   ├── NodeRendererFactory.cs  → Factory tạo renderer
│   │   ├── ConnectionRenderer.cs   → Vẽ đường kết nối
│   │   ├── PortRenderer.cs         → Vẽ port (ellipse) trên canvas
│   │   ├── NodeChrome.cs           → Apply execution badge, GPU opt, Liquid Glass
│   │   ├── NodeDataPanelService.cs → Đọc output value của node để hiển thị
│   │   ├── LiquidGlassHelper.cs    → Liquid Glass visual effect
│   │   ├── GpuDetectionHelper.cs   → Detect GPU cho video processing
│   │   ├── GpuOptimizationHelper.cs
│   │   ├── WebView2AirspaceClipper.cs → Fix WebView2 airspace issue
│   │   └── XxxNodeRenderer.cs      → Một renderer per node type (30+)
│   │
│   ├── Utilities/                  → Tiện ích canvas/editor
│   │   ├── ColorThemeService.cs    → Load/save theme preference, apply runtime
│   │   ├── CanvasSizeManager.cs    → Quản lý kích thước canvas
│   │   ├── MinimapService.cs       → Minimap overview
│   │   ├── ViewportCullingService.cs → Ẩn node ngoài viewport để tăng performance
│   │   ├── GridPatternService.cs   → Vẽ grid nền canvas
│   │   └── ...
│   │
│   ├── Utils/                      → Tiện ích độc lập
│   │   ├── CurlNativeExecutor.cs   → Chạy curl native (libcurl)
│   │   ├── CustomPresetService.cs  → Lưu/đọc preset tùy chỉnh
│   │   └── HtmlOfflineAssetService.cs → Quản lý JS/CSS offline assets
│   │
│   └── Workflow/                   → Core workflow engine
│       ├── WorkflowExecutionService.cs     → Orchestrate thực thi node theo graph
│       ├── WorkflowExecutionContext.cs     → Runtime state của 1 lần chạy
│       ├── WorkflowExecutionVisualizer.cs  → Visual feedback khi chạy (highlight node)
│       ├── FileWorkflowPersistenceService.cs → Save/Load JSON với file-write-time cache
│       │                                       Lưu tại: Documents\FlowMy\Workflow_Json\
│       ├── WorkflowKeyValueStore.cs        → Scoped key-value store cho inter-node data
│       ├── TemplateFactory.cs              → Tạo node instance theo NodeType string
│       ├── WebView2EnvironmentManager.cs   → Shared WebView2 environment
│       ├── PortableWebBundleZipService.cs  → Export/import web bundle
│       ├── WebNodeCacheHelper.cs           → Cache WebView2 state per workflow
│       └── NodeExecutors/                  → 30+ executor (xem §5)
│
├── Themes/                         → ResourceDictionary (xem §6)
│   ├── DarkTheme.xaml / LightTheme.xaml / ModernTheme.xaml / ...
│   ├── Base/
│   │   ├── SemanticTokens.xaml     → Token màu semantic (TextBrush, WindowBackground, v.v.)
│   │   ├── Common.xaml             → ColorKey brushes (ForestPineBrush, TextOnForestPineBrush, v.v.)
│   │   ├── Fonts.xaml
│   │   └── Colors/                 → Palette màu per theme (Dark.xaml, Monokai.xaml, v.v.)
│   └── Controls/                   → Style per control type (Buttons, ComboBoxes, DataGrids, v.v.)
│
├── Utils/
│   ├── CurlParser.cs               → Parse curl command string → HttpRequestNode config
│   └── HttpRequestCurlGenerator.cs → Generate curl string từ HttpRequestNode
│
├── ViewModels/
│   ├── MainViewModel.cs            → Launcher: quét widget shortcuts, headless window, tray pin
│   ├── WorkflowEditorViewModel.cs  → Canvas editor state (nodes, connections, zoom/pan, execution)
│   ├── Base/
│   │   ├── BaseViewModel.cs        → Base chung (INotifyPropertyChanged)
│   │   ├── BaseNodeDialogViewModel.cs → Base cho dialog VM (NodeTitle, TitleDisplayMode,
│   │   │                               TitleColorMode, Inputs/Outputs, ReuseRoutes, commands)
│   │   ├── BaseDataGridViewModel.cs
│   │   └── BaseDynamicViewModel.cs
│   └── XxxNodeDialogViewModel.cs   → Một ViewModel per node type (30+)
│
├── Views/
│   ├── MainWindow.xaml/.cs         → Launcher shell (widget shortcuts, workflow list)
│   ├── WorkflowEditorWindow.xaml/.cs → Canvas editor chính
│   ├── WorkflowEditors/            → 23 partial class của WorkflowEditorWindow
│   │   ├── WorkflowEditorWindow.Host.cs          → IWorkflowEditorHost implementation
│   │   ├── WorkflowEditorWindow.NodeActions.cs   → Copy/paste/delete node
│   │   ├── WorkflowEditorWindow.Persistence.cs   → Save/Load workflow
│   │   ├── WorkflowEditorWindow.ZoomPanHandler.cs
│   │   ├── WorkflowEditorWindow.ConnectionHandler.cs
│   │   ├── WorkflowEditorWindow.TemplateNodeHandler.cs → Drag node từ palette → canvas
│   │   ├── WorkflowEditorWindow.MultiNodeClipboard.cs  → Multi-node copy/paste + remap NodeId
│   │   ├── WorkflowEditorWindow.NodeUiHelpers.cs       → Hover, Liquid Glass hover
│   │   └── ... (AutoScopeChrome, GpuSettings, MinimapManager, v.v.)
│   │
│   ├── NodeControls/               → 40+ static class tạo UI node trên canvas
│   │   ├── Helpers/
│   │   │   └── BaseNodeControlHelper.cs  → Fluent builder: hover, keyboard ports,
│   │   │                                   property sync, dialog, cleanup, canvas integration
│   │   └── XxxNodeControl.cs       → Một file per node type, gọi BaseNodeControlHelper
│   │
│   └── Overlays/                   → 70+ dialog/window
│       ├── BaseNodeDialog.cs       → Base dialog: snap phải màn hình, lưu title,
│       │                             load Inputs/Outputs, color picker, brush resolver
│       ├── XxxNodeDialog.xaml/.cs  → Dialog cấu hình per node type (30+)
│       ├── WorkflowManagementDialog.xaml/.cs → Quản lý danh sách workflow
│       ├── GitManagerDialog.xaml/.cs         → Git UI (commit, push, pull, log)
│       ├── FloatingWidgetConfigDialog.xaml/.cs → Cấu hình floating widget
│       ├── FloatingWidgetWindow.xaml/.cs       → Floating widget overlay window
│       ├── ExecutionTraceDetachedWindow.xaml/.cs → Execution log detached
│       ├── ScreenCaptureOverlay.xaml/.cs       → Chụp màn hình
│       ├── ScreenPositionPickerOverlay.xaml/.cs → Chọn tọa độ màn hình
│       └── ToastWindow.xaml/.cs               → Toast notification
│
├── Workflow/
│   ├── TemplateFactory.cs          → Tạo node instance theo NodeType string (dùng khi load JSON)
│   └── ZIndexManager.cs            → Quản lý Z-order node/port trên canvas
│
├── docs/
│   ├── NODE_CREATION_GUIDE.md      → Hướng dẫn đầy đủ tạo node mới (bắt buộc đọc)
│   ├── WIDGET_AND_NODE_CONTENT_UI.md → Floating widget & node content UI
│   └── Callback_Node_Implementation_Summary.md → Reference implementation
│
├── Properties/
│   ├── Settings.settings           → User settings (Theme, v.v.)
│   └── Settings.Designer.cs
│
└── Library/
    └── Aspose.Cells.dll            → Thư viện Excel (local, không qua NuGet)
```

---

## 3. Kiến trúc tổng quan

**Pattern**: MVVM + DI (`Microsoft.Extensions.DependencyInjection`) + Service Layer

```
App.xaml.cs
  └── ServiceCollection
        ├── AddWorkflowEditorServices()   → đăng ký toàn bộ services
        └── AddViewModelsAndViews()       → đăng ký View + ViewModel theo Assembly scan

MainWindow (Launcher)
  └── MainViewModel
        ├── Quét workflow JSON → WidgetShortcuts
        ├── Mở WorkflowEditorWindow (normal hoặc headless)
        └── Quản lý tray icon + pinned widgets

WorkflowEditorWindow (Canvas Editor)
  └── WorkflowEditorViewModel
        ├── Nodes: ObservableCollection<WorkflowNode>
        ├── Connections: ObservableCollection<WorkflowConnection>
        ├── WorkflowExecutionService → chạy workflow
        └── FileWorkflowPersistenceService → save/load JSON
```

**Luồng thực thi workflow**:
```
WorkflowExecutionService.RunAsync()
  → Tìm StartNode
  → Với mỗi node: INodeExecutor.CanExecute() → ExecuteAsync()
  → NodeExecutionEnvironment.TraverseOutputsAsync() → node tiếp theo
  → WorkflowExecutionVisualizer cập nhật UI (highlight node đang chạy)
```

**Luồng tạo node trên canvas**:
```
Palette drag → TemplateFactory.Create(nodeType, x, y) → WorkflowNode
  → _NodeRenderer.RenderNode(node, canvas)
    → XxxNodeRenderer → XxxNodeControl.CreateBorder()
      → BaseNodeControlHelper fluent API (hover, dialog, cleanup, v.v.)
      → NodeChrome.Apply() (execution badge, Liquid Glass)
```

---

## 4. Các thư mục chính

### Controls/
UserControl tái sử dụng trong XAML. Quan trọng nhất:
- `NodeSearchComboBoxUserControl` — ComboBox tìm kiếm node, dùng trong mọi dialog có chọn node nguồn
- `SyntaxHighlightCodeEditor` — AvalonEdit wrapper với Monokai theme
- `MarkdownTextBlock` — render Markdown (dùng trong AI response nodes)

### Converters/
Converter quan trọng nhất: `IconKeyToPathConverter` — chuyển `"your-icon-key duotone-regular"` → URI SVG. Dùng khắp nơi trong palette và node control.

### Services/Rendering/
Mỗi node type có 1 renderer riêng (`XxxNodeRenderer.cs`). `_NodeRenderer.cs` là dispatcher trung tâm. `NodeChrome.Apply()` áp dụng execution badge và Liquid Glass effect sau khi tạo border.

### Services/Workflow/NodeExecutors/
Mỗi node type có 1 executor (`XxxNodeExecutor.cs`). `WorkflowExecutionService` giữ list tất cả executor và dispatch theo `CanExecute()`. Executor dùng `NodeExecutionEnvironment` để đọc input, ghi output, traverse sang node tiếp theo.

### Services/Utilities/ColorThemeService
Quản lý theme runtime. Khi user đổi theme → `ColorThemeService.ApplyTheme()` → swap `ResourceDictionary` → toàn bộ `DynamicResource` tự cập nhật.

---

## 5. Hệ thống Node

### Node types hiện có (45+)

| Nhóm | Node types |
|------|-----------|
| Flow control | `StartNode`, `EndNode`, `ConditionalNode`, `LoopNode`, `AsyncTaskNode`, `CallbackNode`, `BreakNode` |
| Data | `InputNode`, `OutputNode`, `StorageNode`, `AssignDataNode`, `ListOutNode`, `FlowOverwriteNode`, `KeyValueBridgeNode` |
| Code/Script | `CodeNode` (JS via Jint), `HtmlUiNode` (HTML+JS+CSS in WebView2) |
| Web | `WebNode` (WebView2 browser), `HttpRequestNode`, `DataFetcherNode` |
| File/Folder | `FolderNode`, `FolderFilePathsNode`, `FileDownloadNode` |
| Media | `ImageProcessingNode`, `VideoProcessingNode`, `MediaGalleryNode`, `ScreenCaptureNode` |
| Input events | `MouseEventNode`, `KeyPressEventNode`, `HotkeyPressEventNode`, `ScreenPositionPickerNode` |
| Git | `GitSourceNode` |
| UI | `NotificationNode`, `BodyContainerNode` |
| Utility | `DelayNode`, `StringSplitNode`, `AsyncTaskDispatchCollectNode` |

### Cấu trúc 1 node (7 file)

```
Models/Nodes/YourNode.cs                            ← Data model (kế thừa WorkflowNode)
Views/NodeControls/YourNodeControl.cs               ← UI trên canvas (static class)
Views/Overlays/YourNodeDialog.xaml + .xaml.cs       ← Dialog cấu hình (kế thừa BaseNodeDialog)
ViewModels/YourNodeDialogViewModel.cs               ← ViewModel dialog (kế thừa BaseNodeDialogViewModel)
Services/Rendering/YourNodeRenderer.cs              ← Render node lên canvas
Services/Workflow/NodeExecutors/YourNodeExecutor.cs ← Logic thực thi
```

**Đăng ký node mới cần cập nhật 9 chỗ** — xem chi tiết tại `docs/NODE_CREATION_GUIDE.md`.

### Persistence (save/load)
`FileWorkflowPersistenceService` serialize/deserialize từng node type qua `GetNodeProperties()` / `RestoreNodeProperties()`. Format: JSON với `Properties: { key: value }` per node. File cache theo `LastWriteTimeUtc` để tránh đọc lại disk không cần thiết.

---

## 6. Hệ thống Theme

**8 theme có sẵn**: `Dark`, `Dracula`, `Light`, `Modern`, `Monokai`, `Night`, `SoftDark`, `SoftLight`

**Cách hoạt động**:
1. `ColorThemeService.LoadThemePreference()` đọc từ `Properties.Settings`
2. Swap `ResourceDictionary` trong `App.xaml` → toàn bộ `DynamicResource` tự cập nhật
3. Khi đổi theme runtime → `ColorThemeService.ApplyTheme(themeName)` → re-render canvas nodes

**Quy tắc bắt buộc trong XAML**:
- Dùng `{DynamicResource TextBrush}` thay vì `Foreground="White"`
- Dùng `{DynamicResource WindowBackground}` thay vì `Background="#1E293B"`
- Dùng `{DynamicResource <ColorKey>Brush}` cho màu node (ví dụ: `ForestPineBrush`)
- Dùng `{DynamicResource TextOn<ColorKey>Brush}` cho icon/text trên nền màu đó
- **Không hardcode màu** trong XAML dialog hoặc node control

**Liquid Glass mode**: Khi user chọn `NodeAppearanceMode = "LiquidGlass"`, `NodeChrome.Apply()` tự động áp dụng glass gradient + glow shadow cho mọi node. Node thông thường không cần code thêm. Node dùng Polygon/diamond shape cần check `LiquidGlassHelper.IsLiquidGlassMode(host)` — xem `docs/NODE_CREATION_GUIDE.md §17`.

---

## 7. Quy ước đặt tên

| Loại | Quy ước | Ví dụ |
|------|---------|-------|
| Node model | `XxxNode.cs` trong `Models/Nodes/` | `DelayNode.cs` |
| Node control | `XxxNodeControl.cs` trong `Views/NodeControls/` | `DelayNodeControl.cs` |
| Node dialog | `XxxNodeDialog.xaml/.cs` trong `Views/Overlays/` | `DelayNodeDialog.xaml` |
| Node dialog VM | `XxxNodeDialogViewModel.cs` trong `ViewModels/` | `DelayNodeDialogViewModel.cs` |
| Node renderer | `XxxNodeRenderer.cs` trong `Services/Rendering/` | `DelayNodeRenderer.cs` |
| Node executor | `XxxNodeExecutor.cs` trong `Services/Workflow/NodeExecutors/` | `DelayNodeExecutor.cs` |
| Service | `XxxService.cs` + `IXxxService.cs` trong `Services/Interfaces/` | `TrayService.cs` |
| Converter | hậu tố `Converter` | `IconKeyToPathConverter.cs` |
| Helper | hậu tố `Helper` | `DynamicColorHelper.cs` |
| Extension | hậu tố `Extensions` | `ThemeExtensions.cs` |
| ColorKey | PascalCase, khớp với key trong `Common.xaml` | `ForestPine` → `ForestPineBrush` + `TextOnForestPineBrush` |
| Icon key | kebab-case + style | `"git-alt brands"`, `"circle-play duotone-regular"` |

---

## 8. Tài liệu bổ sung

| File | Nội dung |
|------|---------|
| `docs/NODE_CREATION_GUIDE.md` | **Bắt buộc đọc** khi thêm node mới — đầy đủ từ model đến executor, checklist 9 chỗ đăng ký, Liquid Glass, dynamic input/output, multi-row ComboBox |
| `docs/WIDGET_AND_NODE_CONTENT_UI.md` | Floating widget và node content UI |
| `docs/Callback_Node_Implementation_Summary.md` | Reference implementation cho CallbackNode |
| `Themes/README.md` | Hướng dẫn thêm theme mới |
| `AI_Coding_Guidelines.md` | Quy tắc coding cho AI assistant |
