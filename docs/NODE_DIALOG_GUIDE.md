# Node Dialog Implementation Guide - For AI Code Generation

## ⚠️ QUAN TRỌNG: ĐỌC TRƯỚC KHI BẮT ĐẦU

Tài liệu này hướng dẫn tạo node dialog **theo đúng trình tự** để tránh các lỗi phổ biến. 

**LƯU Ý QUAN TRỌNG:**
- ✅ **Làm theo đúng thứ tự** các bước trong checklist
- ✅ **Đọc kỹ phần "Common Errors"** trước khi implement để tránh lỗi
- ✅ **Kiểm tra lại** sau mỗi bước để đảm bảo không thiếu sót
- ❌ **KHÔNG bỏ qua** các bước có dấu ⚠️ CRITICAL
- ✅ **Chạy workflow đồng thời / ExecutionId:** Trước khi viết hoặc sửa **Executor** (hoặc `WorkflowExecutionService`), đọc **[AI_NODE_FLOW_GUIDE.md §4.1 — ExecutionId & scoped outputs](./AI_NODE_FLOW_GUIDE.md#41-executionid--scoped-outputs-chạy-song-song)**. Trong executor phải dùng `ResolveDynamicValueForExecution` / `ResolveValueByNodeIdAndKeyForExecution` (có `env`), không đọc output run khác qua `NodeDataPanelService.ResolveDynamicValueByKey`; output mới cần vào `MirrorRuntimeOutputsToScopedStore` hoặc `IWorkflowScopedOutputContributor`; Storage cần `PublishStorageOutputsToScoped` đúng thời điểm.

---

## Quick Start Checklist

### Luồng hội thoại tổng quát khi AI tạo node mới

Khi AI tạo **node mới hoàn toàn** (chưa có trong codebase), nên đi đúng thứ tự sau với user, sau đó mới áp dụng các Phase chi tiết:

1. **Hỏi trước về màu + icon của node**
   - Hỏi user:  
     _"Bạn muốn tạo node mới với **colorKey** (màu nền/node brush) nào và **icon** nào?  
     Ví dụ colorKey: `PrimaryBrush`, `SuccessBrush`, `DangerBrush`…  
     Ví dụ icon: `Code`, `Http`, `Folder`, `Keyboard`, `Mouse`…"_  
   - Từ câu trả lời:
     - Chọn **NodeBrush / TitleColorKey mặc định** phù hợp (Phase 2 + Phase 3).
     - Chọn **icon** cho `YourNodeControl` (đặt trong `Views/NodeControls/YourNodeControl.cs`).

2. **Tạo skeleton node theo Phase 1**
   - Tạo đủ 4 file: `YourNodeDialog.xaml`, `YourNodeDialog.xaml.cs`, `YourNodeDialogViewModel.cs`, `YourNodeControl.cs`.
   - Tạo `Models/Nodes/YourNode.cs` (nếu chưa có) với:
     - Thuộc tính `Title`, `NodeBrush`, các input/output mặc định, v.v.
     - Nếu node sử dụng TitleDisplayMode/TitleColor → chuẩn bị luôn property, để sau kết nối Phase 2 + 3.

3. **Thiết kế dialog với 2 tab: Logic + Cấu hình (Tái sử dụng flow)**
   - **Tab 1 – "Logic"**:
     - Chứa toàn bộ UI logic chính của node (URL, code editor, input/output binding…).  
     - Với các node kiểu Code/HTTP, nên có:
      - **Phần Input mapping** giống `CodeNodeDialog.xaml (84-127)`:
        - `ItemsControl` mỗi dòng: `NodeSearchComboBoxUserControl Node`, `ComboBox Key`, `TextBox override` tên biến:
          - `Node`: `ItemsSource="{Binding DataContext.AvailableNodeOptions, RelativeSource={RelativeSource AncestorType=ItemsControl}}"`, `SelectedValuePath="NodeId"`, `DisplayMemberPath="Title"`, `SelectedValue="{Binding SourceNodeId}"`.
           - `Key`: `ItemsSource="{Binding AvailableOutputKeyOptions}"`, `SelectedValue="{Binding SourceOutputKey}"`.
           - `TextBox` cho `InputKeyOverride` (trống = dùng key mặc định).
       - **Phần Output keys động (nếu user yêu cầu)** giống `CodeNodeDialog.xaml (248-277)`:
         - `ItemsControl` với mỗi dòng là 1 `TextBox` `Key` + nút xóa.
         - Nút `+ Thêm output key` để add dòng mới.
     - Lưu ý: **nếu user không yêu cầu output động**, giữ nguyên **output mặc định** của node (không cần `ItemsControl` như CodeNode).
   - **Tab 2 – "Tái sử dụng flow" / "Cấu hình"**:
     - Luôn gồm (tùy node có thể ẩn/bỏ bớt):
       - **Cấu hình vị trí port IN/OUT** (Phase 4b – PortPosition):  
         2 `ComboBox` bind `InputPortPosition` / `OutputPortPosition` (hoặc chỉ `InputPortPosition` cho Loop/If/Async).
      - **Cấu hình ReuseRoutes + kiểu line** (Phase 4):  
        `ItemsControl` mỗi dòng: `IncomingNodeTitle`, `NodeSearchComboBoxUserControl Node OUT`, `ComboBox LineStyle`.
       - (Tùy node) Các cấu hình nâng cao khác nếu có.

4. **Sinh code ViewModel + NodeControl + Persistence theo các Phase**
   - **Phase 1**: Basic Dialog + SaveTitle, RunSingleNodeCommand, v.v.
   - **Phase 2**: `TitleDisplayMode` (nếu node hỗ trợ).
   - **Phase 3**: `TitleColorMode` + `TitleColorKey` (dùng colorKey user đã chọn ở bước 1).
   - **Phase 4**: `ReuseRoutes` + `ConnectionLineStyleOptions` + `GetLineStyleForConnection`.
   - **Phase 4b**: `PortPositionOptions`, `InputPortPosition`/`OutputPortPosition`, `SavePortPositions`.
   - **Input/Output động**:
     - Input mapping: model có list `InputMappings`, ViewModel có `InputMappingsList` + command Add/Remove, lưu/restore trong `FileWorkflowPersistenceService`.
     - Output động: model có list `OutputKeys`/`DynamicOutputs`, ViewModel có `OutputKeysList` + command Add/Remove, đồng bộ với ports và persistence.

5. **Đảm bảo đầy đủ lưu/load cho tất cả cấu hình**
   - **Title / TitleDisplayMode / TitleColorMode / TitleColorKey**:
     - Serialize/deserialize trong `FileWorkflowPersistenceService` (Phase 2 + 3).
   - **ReuseRoutes + LineStyleKey per-route**:
     - Serialize `"ReuseRoutes"` trong `GetNodeProperties`, deserialize trong `RestoreNodeProperties` (Phase 4).
   - **PortPosition của từng `NodePort`**:
     - Lưu `Position` trong `PortDto`, và khi `Restore` phải gán lại `NodePort.Position` (Phase 4b).
   - **InputMappings / OutputKeys động**:
     - Serialize toàn bộ list vào properties của node (thường là JSON string) và khôi phục đầy đủ khi load/import.
   - Sau khi load/import:
     - Gọi lại render: `_connectionRenderer.RenderAllConnections(...)`, `UpdateAllConnectionPaths/Animations(...)`, `PortRenderer.UpdatePortsPositionOnSide(...)` để **UI khớp hoàn toàn với dữ liệu**.

> Tóm lại: **trước khi đụng vào các Phase chi tiết**, AI nên:
> 1) Hỏi user về colorKey + icon,  
> 2) Tạo node + dialog 2 tab,  
> 3) Thiết kế luôn Input/Output động (nếu cần) theo pattern của CodeNode,  
> 4) Sau đó nối từng phần với các Phase 1 → 4b bên dưới.

### Chuẩn Combobox Node mới (bắt buộc)

- Dùng `controls:NodeSearchComboBoxUserControl` cho mọi UI chọn node (không dùng `ComboBox` thường).
- `ItemsSource` phải là `ObservableCollection<WorkflowDataSourceOption>`.
- Khi build options từ node thật, dùng `BaseNodeDialogViewModel.CreateDataSourceOption(node)` để có đủ `IconKey`, `NodeBrush`, `NodeTextBrush`.
- Binding chuẩn:
  - `SelectedValuePath="NodeId"`
  - `DisplayMemberPath="Title"`
  - `SelectedValue="{Binding ..., Mode=TwoWay}"`
- Với UI tạo bằng code-behind, khi lắng nghe đổi chọn node dùng `DependencyPropertyDescriptor` trên `NodeSearchComboBoxUserControl.SelectedValueProperty`.

### Phase 1: Basic Dialog (Bắt buộc cho mọi node)

```yaml
Step 1: Create Dialog Files
  - [ ] Views/Overlays/YourNodeDialog.xaml
  - [ ] Views/Overlays/YourNodeDialog.xaml.cs  
  - [ ] ViewModels/YourNodeDialogViewModel.cs
  - [ ] Views/NodeControls/YourNodeControl.cs

Step 2: Node Model
  - [ ] Models/Nodes/YourNode.cs (nếu chưa có)
  - [ ] ⚠️ Nếu node implement INotifyPropertyChanged: Thêm NotifyTitleChanged() method

Step 3: Renderer
  - [ ] Services/Rendering/YourNodeRenderer.cs
  - [ ] ⚠️ CRITICAL: Always update port colors in RenderNode() và UpdateNodePosition()

Step 4: Dialog Integration
  - [ ] ⚠️ CRITICAL: Release mouse capture, clear DraggedNode, deselect node in OpenNodeDialog()
  - [ ] ⚠️ CRITICAL: Call SaveTitleCommand in Closing event và CloseButton_Click
  - [ ] Header: Thêm nút Play (▶) cạnh nút X — Command="{Binding RunSingleNodeCommand}" (chạy logic node này). Chi tiết xem mục "Header: Nút Play + Đóng" bên dưới.

Step 5: Copy/Paste Support (REQUIRED)
  - [ ] Services/Interaction/WorkflowEditorEventService.cs: Add Ctrl+C/Ctrl+V
  - [ ] Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs: Copy ALL properties
  - [ ] ⚠️ CRITICAL: Trigger NotifyTitleChanged() after setting Title (nếu node có INotifyPropertyChanged)

Step 6: Persistence Support (REQUIRED - Save/Export JSON)
  - [ ] Services/Workflow/FileWorkflowPersistenceService.cs: Add serialize logic trong GetNodeProperties()
  - [ ] Services/Workflow/FileWorkflowPersistenceService.cs: Add deserialize logic trong RestoreNodeProperties()
  - [ ] ⚠️ CRITICAL: Serialize TẤT CẢ custom properties (bao gồm List/Array properties)
  - [ ] ⚠️ CRITICAL: Deserialize và restore TẤT CẢ properties khi load/import
  - Lưu ý: **Ctrl+S / nút Save** lưu đầy đủ logic + output đã chạy của từng node; **nút Export** chỉ export logic (dùng để chia sẻ/import). Chi tiết xem mục 7.3 bên dưới.
```

### Phase 2: TitleDisplayMode Support (Optional - chỉ thêm nếu node hỗ trợ)

```yaml
Step 1: Node Model
  - [ ] Add TitleDisplayMode property
  - [ ] Add TitleTextBlockUI property
  - [ ] ⚠️ CRITICAL: Add NotifyTitleChanged() method (nếu chưa có)

Step 2: ViewModel
  - [ ] Add TitleDisplayMode ObservableProperty
  - [ ] Add TitleDisplayModeOptions ObservableCollection
  - [ ] ⚠️ CRITICAL: Initialize TitleDisplayMode từ node trong constructor
  - [ ] ⚠️ CRITICAL: Sync TitleDisplayMode trong PropertyChanged handler
  - [ ] ⚠️ CRITICAL: Save TitleDisplayMode trong SaveTitle()
  - [ ] ⚠️ CRITICAL: Call NotifyTitleChanged() sau khi set Title

Step 3: Dialog XAML
  - [ ] Uncomment TitleDisplayMode ComboBox section
  - [ ] Verify binding: ItemsSource, SelectedValuePath, DisplayMemberPath, SelectedValue

Step 4: NodeControl
  - [ ] Create titleTextBlock với Visibility dựa trên TitleDisplayMode
  - [ ] Add titleTextBlock vào WorkflowCanvas (không phải vào border)
  - [ ] ⚠️ CRITICAL: Add static dictionaries cho throttling (_titleUpdateTimers, _titleUpdatedAfterZoom)
  - [ ] ⚠️ CRITICAL: Add hover handling (MouseEnter/MouseLeave)
  - [ ] ⚠️ CRITICAL: Add PropertyChanged handler để sync Title và TitleDisplayMode
  - [ ] ⚠️ CRITICAL: Add DependencyPropertyDescriptor để sync với border visibility
  - [ ] ⚠️ CRITICAL: Add LayoutUpdated handler với zoom handling và throttling
  - [ ] ⚠️ CRITICAL: Add Unloaded handler để cleanup (tránh memory leak)
  - [ ] ⚠️ CRITICAL: Add helper methods: GetTitleVisibility, UpdateTitleVisibility, ThrottledUpdateTitlePosition, UpdateTitlePosition

Step 5: Renderer (CRITICAL - nhiều người quên!)
  - [ ] ⚠️ CRITICAL: Implement UpdateNodePosition() để sync title position
  - [ ] ⚠️ CRITICAL: Implement RemoveNode() để cleanup titleTextBlock
```

### Phase 3: TitleColorMode Support (Optional - cho phép thay đổi màu tiêu đề)

```yaml
Step 1: Node Model
  - [ ] Add TitleColorMode property (enum: NodeColor, CustomColor)
  - [ ] Add TitleColorKey property (string? - key của màu tùy chọn)
  - [ ] ⚠️ CRITICAL: Add OnPropertyChanged() cho cả 2 property

Step 2: ViewModel (đã có sẵn trong BaseNodeDialogViewModel)
  - [ ] TitleColorMode và TitleColorKey ObservableProperty đã có trong BaseNodeDialogViewModel
  - [ ] TitleColorOptions ObservableCollection đã có sẵn
  - [ ] ⚠️ CRITICAL: OnTitleColorKeyChanged() sẽ tự động sync về node

Step 3: Dialog XAML
  - [ ] Add TitleColorMode section sau TitleDisplayMode section
  - [ ] Add ComboBox với binding TitleColorOptions và TitleColorKey
  - [ ] Add Border color preview

Step 4: Dialog Code-Behind
  - [ ] Add TitleColorComboBox_SelectionChanged handler
  - [ ] Add UpdateTitleColorPreview() method để update preview border

Step 5: NodeControl
  - [ ] Add GetTitleBrush() method để lấy brush dựa trên TitleColorMode/Key
  - [ ] ⚠️ CRITICAL: Khi tạo title TextBlock dùng Foreground = GetTitleBrush(node), KHÔNG dùng node.NodeBrush (nếu dùng NodeBrush thì sau Save/Load hoặc import màu title sẽ hiển thị sai)
  - [ ] ⚠️ CRITICAL: Subscribe PropertyChanged cho TitleColorMode và TitleColorKey
  - [ ] ⚠️ CRITICAL: Trong handler NodeBrush: set titleTextBlock.Foreground = GetTitleBrush(node) (để khi đổi màu node mà mode là NodeColor thì title cập nhật đúng)

Step 6: Persistence (REQUIRED)
  - [ ] Serialize TitleColorMode và TitleColorKey trong GetNodeProperties() (FileWorkflowPersistenceService)
  - [ ] Deserialize TitleColorMode và TitleColorKey trong RestoreNodeProperties() cho đúng node type (if node is YourNodeType { ... })
```

### Phase 4: Tab "Tái sử dụng flow" + cấu hình style line per-node (Optional, nâng cao)

> Mục tiêu: tạo **2 tab trong dialog node** – tab 1 giữ nguyên logic cũ, tab 2 cho phép:
> - Chọn **Node IN** (các node nối trực tiếp vào input node hiện tại),
> - Chọn **Node OUT** (các node nối trực tiếp ra từ output node hiện tại),
> - Chọn **kiểu line OUT** cho riêng route đó (Bezier / Orthogonal / Straight hoặc theo global).
>
> Khi `LineStyleKey` không set (hoặc "WorkflowDefault") thì connection dùng style global từ nút  
> `Connection Line Style Button` ở `Views/WorkflowEditorWindow.xaml (1470-1496)`.  
> Khi `LineStyleKey` set (Bezier/Orthogonal/Straight) thì connection đó dùng style riêng, giống như `TitleColorKey` khác `"NodeColor"`.

```yaml
Step 1: Node Model (Models/Nodes/WorkflowNode.cs)
  - [ ] Add class NodeReuseRoute:
        public sealed class NodeReuseRoute
        {
            /// Id của node nối trực tiếp vào input port của node hiện tại.
            public string? IncomingNodeId { get; set; }

            /// Id của node nối trực tiếp ra từ output port của node hiện tại.
            public string? OutgoingNodeId { get; set; }

            /// Kiểu line cho connection từ node hiện tại sang OutgoingNodeId.
            /// "Bezier" | "Orthogonal" | "Straight" | null (null = theo workflow / ConnectionLineStyleButton).
            public string? LineStyleKey { get; set; }
        }

  - [ ] Trong class WorkflowNode:
        /// Cấu hình "tái sử dụng" logic & style line theo node IN/OUT trực tiếp.
        public List<NodeReuseRoute> ReuseRoutes { get; set; } = new();

Step 2: ViewModel base cho dialog (ViewModels/BaseNodeDialogViewModel.cs)
  - [ ] Add:
        public ObservableCollection<ReuseRouteItemViewModel> ReuseRoutes { get; } = new();

  - [ ] Add options cho combobox kiểu line:
        public ObservableCollection<ConnectionLineStyleOption> ConnectionLineStyleOptions { get; } = new()
        {
            new ConnectionLineStyleOption("WorkflowDefault", "Theo cấu hình workflow (nút kiểu đường kết nối)"),
            new ConnectionLineStyleOption("Bezier",         "Bezier (Cong mượt)"),
            new ConnectionLineStyleOption("Orthogonal",     "Vuông góc (Orthogonal)"),
            new ConnectionLineStyleOption("Straight",       "Thẳng (Straight)")
        };

  - [ ] Implement LoadReuseRoutes():
        protected virtual void LoadReuseRoutes()
        {
            ReuseRoutes.Clear();
            if (_host.ViewModel == null) return;
            var vm          = _host.ViewModel;
            var connections = vm.Connections;
            if (connections == null || connections.Count == 0) return;

            // Node IN: các node nối TRỰC TIẾP vào input của _node
            var previousNodes = connections
                .Where(c => c.ToNode == _node && c.FromNode != null)
                .Select(c => c.FromNode!)
                .Distinct()
                .ToList();

            // Node OUT: các node nối TRỰC TIẾP ra từ output của _node
            var nextNodes = connections
                .Where(c => c.FromNode == _node && c.ToNode != null)
                .Select(c => c.ToNode!)
                .Distinct()
                .ToList();

            if (previousNodes.Count == 0 || nextNodes.Count == 0)
                return;

            // Chuẩn bị options cho Node OUT
            var outgoingOptions = nextNodes
                .Select(n => new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title  = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                })
                .ToList();

            // Tạo 1 item cho mỗi Node IN
            foreach (var prev in previousNodes)
            {
                var existing = _node.ReuseRoutes
                    .FirstOrDefault(r =>
                        string.Equals(r.IncomingNodeId, prev.Id, StringComparison.OrdinalIgnoreCase));

                var item = new ReuseRouteItemViewModel
                {
                    IncomingNodeId    = prev.Id,
                    IncomingNodeTitle = string.IsNullOrWhiteSpace(prev.Title) ? prev.Id : prev.Title
                };

                foreach (var opt in outgoingOptions)
                    item.OutgoingOptions.Add(opt);

                if (existing != null)
                {
                    if (!string.IsNullOrWhiteSpace(existing.OutgoingNodeId))
                        item.SelectedOutgoingNodeId = existing.OutgoingNodeId;

                    item.SelectedLineStyleKey = string.IsNullOrWhiteSpace(existing.LineStyleKey)
                        ? "WorkflowDefault"
                        : existing.LineStyleKey;
                }
                else
                {
                    item.SelectedLineStyleKey = "WorkflowDefault";
                }

                ReuseRoutes.Add(item);
            }
        }

  - [ ] Trong SaveTitle() (BaseNodeDialogViewModel):
        - Sau khi xử lý Title/TitleDisplayMode/TitleColor*:

        bool hasChanges = false;
        // ... (Title, TitleDisplayMode, TitleColorMode/Key như cũ) ...

        bool reuseChanged = false;
        if (_node.ReuseRoutes != null)
        {
            _node.ReuseRoutes.Clear();
            foreach (var routeVm in ReuseRoutes)
            {
                if (string.IsNullOrWhiteSpace(routeVm.IncomingNodeId) ||
                    string.IsNullOrWhiteSpace(routeVm.SelectedOutgoingNodeId))
                {
                    continue;
                }

                var lineStyleKey = routeVm.SelectedLineStyleKey;
                if (string.Equals(lineStyleKey, "WorkflowDefault", StringComparison.OrdinalIgnoreCase))
                {
                    // null => follow workflow (ConnectionLineStyleButton)
                    lineStyleKey = null;
                }

                _node.ReuseRoutes.Add(new NodeReuseRoute
                {
                    IncomingNodeId = routeVm.IncomingNodeId,
                    OutgoingNodeId = routeVm.SelectedOutgoingNodeId,
                    LineStyleKey   = lineStyleKey
                });
            }

            reuseChanged = true;
        }

        if (hasChanges)
        {
            if (_node.TitleTextBlockUI != null)
            {
                _node.TitleTextBlockUI.Text = NodeTitle;
                UpdateTitleColor(_node.TitleTextBlockUI);
            }
            _host.RequestSyncDataPanels(immediate: true);
        }

        // Áp dụng ngay cấu hình ReuseRoutes cho line & animation sau khi đóng dialog
        if (reuseChanged && _host.ViewModel?.Connections != null && _host.ViewModel.Connections.Count > 0)
        {
            try
            {
                var cons = _host.ViewModel.Connections;
                _host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                _host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
            }
            catch { /* best-effort */ }
        }

        OnSaveTitle();

Step 3: ViewModels phụ trợ
  - [ ] ViewModels/ReuseRouteItemViewModel.cs:
        public partial class ReuseRouteItemViewModel : ObservableObject
        {
            [ObservableProperty] private string  _incomingNodeId    = string.Empty;
            [ObservableProperty] private string  _incomingNodeTitle = string.Empty;
            [ObservableProperty] private string? _selectedOutgoingNodeId;
            public ObservableCollection<WorkflowDataSourceOption> OutgoingOptions { get; } = new();

            /// "WorkflowDefault" | "Bezier" | "Orthogonal" | "Straight"
            [ObservableProperty] private string? _selectedLineStyleKey;
        }

  - [ ] ViewModels/ConnectionLineStyleOption.cs:
        public sealed class ConnectionLineStyleOption
        {
            public string Key         { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;

            public ConnectionLineStyleOption() { }
            public ConnectionLineStyleOption(string key, string displayName)
            {
                Key = key;
                DisplayName = displayName;
            }

            public override string ToString() => DisplayName ?? Key;
        }

Step 4: Dialog XAML – tạo 2 tab (giữ nguyên Logic, thêm Tab "Tái sử dụng flow")
  - [ ] Thay phần Content (Grid.Row="1") bằng TabControl:
        - TabItem Header="Logic": bọc **toàn bộ nội dung logic cũ** (ScrollViewer + StackPanel + controls).
        - TabItem Header="Tái sử dụng flow": dùng template `ItemsControl` + 3 cột như ví dụ ở trên.

Step 5: ConnectionRenderer – override style line từ ReuseRoutes
  - [ ] Add phương thức GetLineStyleForConnection(WorkflowConnection connection) như mô tả ở trên.
  - [ ] Trong:
        - RenderConnection
        - UpdateConnectionPath
        - UpdateConnectionColor
        - GetConnectionMidpoint
    thay vì dùng trực tiếp `Host.ConnectionLineStyle`, luôn gọi `GetLineStyleForConnection(connection)` để chọn `_bezier/_orthogonal/_straight` và để tính midpoint.
  - [ ] Trong RenderAllConnections():
        Sau vòng lặp RenderConnection, gọi:
        `UpdateAllConnectionAnimations(connectionList);`

Step 6: Persistence – lưu/khôi phục ReuseRoutes (bao gồm LineStyleKey)
  - [ ] Services/Workflow/FileWorkflowPersistenceService.cs:
        - Trong GetNodeProperties:
```csharp
if (node.ReuseRoutes != null && node.ReuseRoutes.Count > 0)
{
    try
    {
        var routesJson = JsonSerializer.Serialize(node.ReuseRoutes);
        dict["ReuseRoutes"] = routesJson;
    }
    catch { }
}
```
        - Trong RestoreNodeProperties:
```csharp
if (properties.TryGetValue("ReuseRoutes", out var rrObj) && rrObj != null)
{
    try
    {
        List<NodeReuseRoute>? parsed = null;

        if (rrObj is string s && !string.IsNullOrWhiteSpace(s))
        {
            parsed = JsonSerializer.Deserialize<List<NodeReuseRoute>>(s);
        }
        else if (rrObj is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                var s2 = je.GetString();
                if (!string.IsNullOrWhiteSpace(s2))
                    parsed = JsonSerializer.Deserialize<List<NodeReuseRoute>>(s2);
            }
            else if (je.ValueKind == JsonValueKind.Array)
            {
                parsed = JsonSerializer.Deserialize<List<NodeReuseRoute>>(je.GetRawText());
            }
        }

        if (parsed != null)
        {
            node.ReuseRoutes = parsed;
        }
    }
    catch { }
}
```

Step 7: Hành vi kết hợp với Connection Line Style Button & TitleColor
  - Nút `ConnectionLineStyleButton` ở `WorkflowEditorWindow.xaml (1470-1496)` set `ViewModel.ConnectionLineStyle`
    và `_connectionLineStyle` trong host, được expose qua `IWorkflowEditorHost.ConnectionLineStyle`.
  - `GetLineStyleForConnection` sẽ:
    - Nếu **Route không có LineStyleKey** (`WorkflowDefault`) → dùng `Host.ConnectionLineStyle` (giống TitleColorKey = "NodeColor").
    - Nếu **Route có LineStyleKey** (Bezier/Orthogonal/Straight) → ưu tiên style này, không bị ảnh hưởng bởi thay đổi global sau đó (giống TitleColorKey = "LimeGreen"/"PrimaryBrush"...).
  - Khi load workflow (ComboBox / Import JSON):
    - Nodes được tạo, `RestoreNodeProperties` gán lại `ReuseRoutes`.
    - EventService gọi `_connectionRenderer.RenderAllConnections(vm.Connections, ...)` → `RenderConnection` + `GetLineStyleForConnection` áp dụng đúng style cho từng connection.
    - Sau đó `UpdateAllConnectionAnimations(...)` được gọi để bật lại hiệu ứng line (nét đứt chạy / energy).
```


### Phase 4b: Cấu hình vị trí cổng IN/OUT (PortPosition) cho từng node

> Mục tiêu: Cho phép **đổi vị trí port IN/OUT** (Left / Top / Right / Bottom) của node ngay trong dialog (tab "Tái sử dụng flow"),  
> lưu lại vào workflow và **render lại port + connection** đúng vị trí mỗi lần mở workflow.

#### 1. Model: NodePort & PortPosition

- `Models/NodePort.cs` đã có:

```csharp
public enum PortPosition
{
    Left,
    Top,
    Right,
    Bottom
}

public sealed class NodePort
{
    public bool IsInput { get; set; }
    public PortPosition Position { get; set; }
    // ...
}
```

- **Yêu cầu**: Mọi logic mới **chỉ dùng lại** `NodePort.Position` (không tạo thêm field khác),  
  để khi đổi vị trí port, renderer chỉ cần đọc `Position` là đủ.

#### 2. Base ViewModel cho dialog: cấu hình InputPortPosition/OutputPortPosition

File: `ViewModels/BaseNodeDialogViewModel.cs`

- **Thêm options cho combobox**:

```csharp
public ObservableCollection<PortPosition> PortPositionOptions { get; } =
    new ObservableCollection<PortPosition>
    {
        PortPosition.Left,
        PortPosition.Top,
        PortPosition.Right,
        PortPosition.Bottom
    };
```

- **Thuộc tính bind 2-way với combobox**:

```csharp
[ObservableProperty] private PortPosition _inputPortPosition;
[ObservableProperty] private PortPosition _outputPortPosition;
```

- **Khởi tạo từ `_node.Ports` trong constructor** (sau khi `_node` đã được gán):

```csharp
var inputPort  = _node.Ports.FirstOrDefault(p => p.IsInput);
var outputPort = _node.Ports.FirstOrDefault(p => !p.IsInput);

_inputPortPosition  = inputPort?.Position  ?? PortPosition.Left;
_outputPortPosition = outputPort?.Position ?? PortPosition.Right;
```

> Lưu ý: Với các node **chỉ có IN** (Loop, If-Else/Conditional, AsyncTask...),  
> `outputPort` có thể null → `_outputPortPosition` vẫn được set default nhưng không show UI chọn ở XAML.

#### 3. Lưu vị trí port khi bấm Save trong dialog

- Trong `BaseNodeDialogViewModel.SaveTitle()` (nơi đã xử lý Title, ReuseRoutes...),  
  **gọi thêm** method:

```csharp
SavePortPositions();
```

- Implement method này trong `BaseNodeDialogViewModel`:

```csharp
protected virtual void SavePortPositions()
{
    if (_node == null || _host?.ViewModel == null) return;

    var inputPort  = _node.Ports.FirstOrDefault(p => p.IsInput);
    var outputPort = _node.Ports.FirstOrDefault(p => !p.IsInput);

    bool changed = false;

    if (inputPort != null && inputPort.Position != InputPortPosition)
    {
        inputPort.Position = InputPortPosition;
        changed = true;
    }

    if (outputPort != null && outputPort.Position != OutputPortPosition)
    {
        outputPort.Position = OutputPortPosition;
        changed = true;
    }

    if (!changed)
        return;

    // Cập nhật lại vị trí render của toàn bộ port trên từng side
    var portsOnLeft   = _node.Ports.Where(p => p.Position == PortPosition.Left).ToList();
    var portsOnTop    = _node.Ports.Where(p => p.Position == PortPosition.Top).ToList();
    var portsOnRight  = _node.Ports.Where(p => p.Position == PortPosition.Right).ToList();
    var portsOnBottom = _node.Ports.Where(p => p.Position == PortPosition.Bottom).ToList();

    _host.PortRenderer.UpdatePortsPositionOnSide(_node, PortPosition.Left,   portsOnLeft);
    _host.PortRenderer.UpdatePortsPositionOnSide(_node, PortPosition.Top,    portsOnTop);
    _host.PortRenderer.UpdatePortsPositionOnSide(_node, PortPosition.Right,  portsOnRight);
    _host.PortRenderer.UpdatePortsPositionOnSide(_node, PortPosition.Bottom, portsOnBottom);

    // Redraw toàn bộ connections để line bám theo port mới
    var cons = _host.ViewModel.Connections;
    _host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
    _host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
}
```

> `UpdatePortsPositionOnSide` trong `PortRenderer` đã **tự chia đều các port trên cùng 1 cạnh**,  
> nên khi IN/OUT nằm chung 1 phía, khoảng cách giữa chúng sẽ được **tự tính lại đều nhau**.

#### 4. Persistence: lưu/khôi phục NodePort.Position

File: `Services/Workflow/FileWorkflowPersistenceService.cs`

- Trong phần lưu `Ports` (PortDto) đã có field `Position` → không cần đổi.
- Phần khôi phục (Restore) cần đảm bảo **gán lại `Position` vào NodePort`**:

```csharp
// Trong vòng foreach (var portDto in nodeDto.Ports)
var pos = (PortPosition)portDto.Position;

if (portDto.Index >= 0 && portDto.Index < portsSameDirection.Count)
{
    var targetPort = portsSameDirection[portDto.Index];
    targetPort.Id       = portDto.Id;
    targetPort.Position = pos; // ⚠️ QUAN TRỌNG: khôi phục vị trí đã lưu
}
else
{
    var anySameDirection = portsSameDirection.FirstOrDefault();
    if (anySameDirection != null)
    {
        anySameDirection.Id       = portDto.Id;
        anySameDirection.Position = pos;
    }
}
```

> Nếu không gán lại `Position`, node sẽ dùng vị trí mặc định từ TemplateFactory mỗi lần load workflow.

#### 5. Renderer: tính vị trí port & spacing khi nhiều port trên cùng 1 cạnh

File: `Services/Rendering/PortRenderer.cs`

- `UpdatePortsPositionOnSide(WorkflowNode node, PortPosition side, IReadOnlyList<NodePort> portsOnSide)`:
  - Lấy kích thước node (Width/Height).
  - Nếu số port \(n > 0\) → chia cạnh tương ứng thành \(n + 1\) đoạn.
  - Đặt mỗi port vào vị trí **segment thứ i** (bỏ 2 mép ngoài), nhờ đó:
    - Nếu có **2 port cùng phía** (ví dụ IN và OUT đều ở Left):
      - Chúng sẽ nằm **cân đối** nhau, không dính sát nhau hay sát mép.
      - Logic tự đúng cho mọi trường hợp nhiều port.

> Nhờ vậy, yêu cầu "nếu port in/out cùng phía thì 2 port nằm đều nhau, in nằm trên/out nằm dưới"  
> được đảm bảo mà không cần code tay trong từng dialog.

#### 6. XAML: bind combobox chọn vị trí port trong tab "Tái sử dụng flow"

- Trong các `*NodeDialog.xaml` (Input, Mouse, KeyPress, Hotkey, Output, StringSplit, ListOut, Web, MediaGallery, Folder, AssignData, HttpRequest, Code, HtmlUi...):
- Ở TabItem `Header="Tái sử dụng flow"`, thêm block cấu hình:

```xml
<!-- Cấu hình vị trí cổng IN/OUT -->
<TextBlock Text="Vị trí cổng IN/OUT:"
           Foreground="White"
           FontSize="14"
           FontWeight="SemiBold"
           Margin="0,0,0,8"/>

<Grid Margin="0,0,0,12">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- Port IN -->
    <StackPanel Grid.Column="0" Margin="0,0,8,0">
        <TextBlock Text="Port IN (cổng vào)"
                   Foreground="White"
                   FontSize="12"
                   Opacity="0.8"
                   Margin="0,0,0,4"/>
        <ComboBox Height="32"
                  Style="{DynamicResource BaseComboBox}"
                  ItemsSource="{Binding PortPositionOptions}"
                  SelectedItem="{Binding InputPortPosition, Mode=TwoWay}"/>
    </StackPanel>

    <!-- Port OUT -->
    <StackPanel Grid.Column="1" Margin="8,0,0,0">
        <TextBlock Text="Port OUT (cổng ra)"
                   Foreground="White"
                   FontSize="12"
                   Opacity="0.8"
                   Margin="0,0,0,4"/>
        <ComboBox Height="32"
                  Style="{DynamicResource BaseComboBox}"
                  ItemsSource="{Binding PortPositionOptions}"
                  SelectedItem="{Binding OutputPortPosition, Mode=TwoWay}"/>
    </StackPanel>
</Grid>
```

- Với các node **chỉ cho phép chỉnh IN** (Loop, Conditional/If-Else, AsyncTask):
  - Hoặc **ẩn hoàn toàn** cột OUT, hoặc dùng layout chỉ có một combobox:

```xml
<TextBlock Text="Vị trí cổng IN:"
           Foreground="White"
           FontSize="14"
           FontWeight="SemiBold"
           Margin="0,0,0,8"/>

<ComboBox Height="32"
          Style="{DynamicResource BaseComboBox}"
          ItemsSource="{Binding PortPositionOptions}"
          SelectedItem="{Binding InputPortPosition, Mode=TwoWay}"/>
```

- Các node **không áp dụng** (LoopBody, Delay, Screen, ...) → **không thêm** block này.

#### 7. Flow tổng thể khi người dùng đổi vị trí port

1. Người dùng mở dialog node → tab "Tái sử dụng flow":
   - Combobox IN/OUT đọc giá trị từ `_inputPortPosition/_outputPortPosition` (sync với `_node.Ports`).
2. Người dùng chọn vị trí mới (ví dụ: IN=Left, OUT=Bottom) rồi bấm Save:
   - `SaveTitle()` gọi `SavePortPositions()` → cập nhật `NodePort.Position`.
   - Gọi `_host.PortRenderer.UpdatePortsPositionOnSide(...)` → tính lại toạ độ port.
   - Gọi `_host.ConnectionRenderer.UpdateAllConnectionPaths/Animations(...)` → line bám theo port mới.
3. Khi Save workflow:
   - `FileWorkflowPersistenceService` serialize `Ports` kèm `Position`.
4. Khi Load/Import workflow:
   - `Restore` gán lại `Position` cho từng `NodePort`.
   - Event render run lại → port + connection hiển thị đúng như lần cuối người dùng đã chỉnh.

#### 8. Keyboard Port Position — Phím mũi tên đổi vị trí port khi hover (không cần mở dialog)

> Mục tiêu: Cho phép người dùng **đổi vị trí port nhanh** bằng phím mũi tên khi hover chuột trên node,
> **không cần mở dialog** → UX nhanh hơn nhiều so với cách cũ (chuột phải → context menu).

##### 8.1 Cách hoạt động (Node thường)

| Phím | Tác dụng |
|------|----------|
| **Arrow** (←↑→↓) | Đổi vị trí **Port IN** theo hướng phím |
| **Shift + Arrow** | Đổi vị trí **Port OUT** theo hướng phím |

##### 8.2 Implementation trong NodeControl

```csharp
// 1. Cho phép border nhận focus
border.Focusable = true;
border.FocusVisualStyle = null;

// 2. Focus khi hover — PHẢI dùng Dispatcher.BeginInvoke
bool isHovering = false;
border.MouseEnter += (s, e) =>
{
    isHovering = true;
    // ⚠️ CRITICAL: Dùng Dispatcher — border.Focus() trực tiếp bị WPF bỏ qua
    Application.Current.Dispatcher.BeginInvoke(
        System.Windows.Threading.DispatcherPriority.Input,
        new Action(() => { if (isHovering) border.Focus(); }));
};
border.MouseLeave += (s, e) => { isHovering = false; };

// 3. Xử lý phím
border.PreviewKeyDown += (s, e) =>
{
    if (!isHovering) return;
    bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
    PortPosition? newPos = e.Key switch
    {
        Key.Left  => PortPosition.Left,
        Key.Up    => PortPosition.Top,
        Key.Right => PortPosition.Right,
        Key.Down  => PortPosition.Bottom,
        _ => null
    };
    if (newPos == null) return;
    e.Handled = true;
    ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
};
```

##### 8.3 ChangePortPosition helper

```csharp
private static void ChangePortPosition(
    WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
{
    if (node.Ports == null || node.Ports.Count == 0) return;
    var port = isInputPort
        ? node.Ports.FirstOrDefault(p => p.IsInput)
        : node.Ports.FirstOrDefault(p => !p.IsInput);
    if (port == null || port.Position == newPosition) return;
    port.Position = newPosition;
    host.UpdatePortsPositionOnSide(node, newPosition);
    var cons = host.ViewModel?.Connections;
    if (cons != null && cons.Count > 0)
    {
        try
        {
            host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
            host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
        }
        catch { }
    }
}
```

##### 8.4 ConditionalNode — Xử lý đặc biệt

ConditionalNode có **nhiều output ports** (if/else if/else). Logic khác node thường:

**Classic mode** (`ConditionalNodeControl`):
- Arrow: đổi Port IN (1 port)
- Shift+Arrow: đổi **TẤT CẢ** output ports cùng lúc + gọi `ReRenderConditionalNode` + `RenderConditionalNodePorts`

**Diamond mode** (`ConditionalDiamondControl`):

Có 2 loại element nhận phím, hoạt động **ĐỘC LẬP**:

| Element | Arrow | Shift+Arrow |
|---------|-------|-------------|
| **Diamond border** (hình thoi chính) | Port IN của diamond | TẤT CẢ Port OUT (hướng output từ diamond) |
| **Satellite circles** (hình tròn nhánh) | `SatelliteInputPosition` (hướng line vào circle) | `branch.Port.Position` (port OUT riêng của nhánh) |

⚠️ **Guard chống xung đột**: Dùng `IsKeyboardFocusWithin` (KHÔNG phải `IsKeyboardFocused`):

```csharp
// Diamond border
border.PreviewKeyDown += (s, e) =>
{
    if (!isHovering) return;
    if (!border.IsKeyboardFocusWithin) return; // ⚠️ Guard
    // ... xử lý Arrow/Shift+Arrow cho diamond
};

// Satellite circle
satelliteBorder.PreviewKeyDown += (s, e) =>
{
    if (!satHovering) return;
    if (!satelliteBorder.IsKeyboardFocusWithin) return; // ⚠️ Guard
    // ... xử lý Arrow/Shift+Arrow cho satellite
};
```

> **Giải thích**: Diamond border và satellite borders là **siblings trên Canvas** (không phải parent-child).
> `IsKeyboardFocusWithin` trả về true khi element hoặc child của nó có focus.
> Vì satellite KHÔNG nằm trong visual tree của diamond, khi satellite có focus thì
> `diamond.IsKeyboardFocusWithin == false` → diamond không xử lý phím → không xung đột.
>
> `IsKeyboardFocused` quá strict — focus có thể ở child element bên trong border (Grid, shapes...)
> khiến border.IsKeyboardFocused == false dù border đang được hover.


#### TitleColorMode XAML Template (thêm sau TitleDisplayMode section):

```xml
<!-- TitleColorMode Section -->
<TextBlock Text="Màu tiêu đề:"
           Foreground="White"
           FontSize="14"
           FontWeight="SemiBold"
           Margin="0,0,0,8"/>

<Grid Margin="0,0,0,16">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>
    
    <ComboBox x:Name="TitleColorComboBox"
              Grid.Column="0"
              Height="36"
              Style="{DynamicResource BaseComboBox}"
              ItemsSource="{Binding TitleColorOptions}"
              SelectedValuePath="Key"
              DisplayMemberPath="DisplayName"
              SelectedValue="{Binding TitleColorKey, Mode=TwoWay}"
              SelectionChanged="TitleColorComboBox_SelectionChanged"/>
    
    <!-- Color Preview -->
    <Border Grid.Column="1" 
            x:Name="TitleColorPreview"
            Width="36" Height="36" 
            CornerRadius="6"
            Margin="8,0,0,0"
            BorderBrush="#33FFFFFF"
            BorderThickness="1"/>
</Grid>
```

#### TitleColorMode Code-Behind Template:

```csharp
// Trong constructor, sau InitializeComponent():
UpdateTitleColorPreview();

// Event handler cho TitleColorComboBox:
private void TitleColorComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
{
    UpdateTitleColorPreview();
}

private void UpdateTitleColorPreview()
{
    if (TitleColorPreview == null || TitleColorComboBox?.SelectedValue == null) return;

    var colorKey = TitleColorComboBox.SelectedValue.ToString();
    System.Windows.Media.Brush? brush = null;

    if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
    {
        // Màu theo node - lấy từ node hiện tại
        if (_viewModel?.Node != null)
        {
            brush = _viewModel.Node.NodeBrush;
        }
    }
    else if (colorKey == "LimeGreen")
    {
        brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
    }
    else
    {
        brush = Application.Current.TryFindResource(colorKey) as System.Windows.Media.Brush;
    }

    TitleColorPreview.Background = brush ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
}
```

#### TitleColorMode NodeControl Template (thêm vào PropertyChanged handler):

```csharp
// Khi TẠO title TextBlock - BẮT BUỘC dùng GetTitleBrush(node), không dùng node.NodeBrush:
var titleTextBlock = new TextBlock
{
    Text = node.Title ?? "Your Title",
    Foreground = GetTitleBrush(node),  // ⚠️ CRITICAL: Để sau Save/Load hoặc import màu title hiển thị đúng
    // ... các thuộc tính khác
};

// Trong PropertyChanged handler:
else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
{
    border.Background = node.NodeBrush;
    titleTextBlock.Foreground = GetTitleBrush(node);  // Cập nhật title theo mode (NodeColor hoặc CustomColor)
}
else if (e.PropertyName == nameof(YourNode.TitleColorMode) || 
         e.PropertyName == nameof(YourNode.TitleColorKey))
{
    titleTextBlock.Foreground = GetTitleBrush(node);
}

// Helper method:
private static Brush GetTitleBrush(YourNode node)
{
    if (node.TitleColorMode == TitleColorMode.CustomColor && !string.IsNullOrEmpty(node.TitleColorKey))
    {
        if (node.TitleColorKey == "LimeGreen")
            return new SolidColorBrush(Colors.LimeGreen);
        var brush = Application.Current.TryFindResource(node.TitleColorKey) as Brush;
        if (brush != null) return brush;
    }
    return node.NodeBrush;
}
```

#### Xử lý màu title khi Save/Load (Persistence)

Để màu tiêu đề **được lưu khi Save/Export** và **hiển thị đúng khi Import hoặc chọn workflow khác**, cần:

1. **Persistence** (`FileWorkflowPersistenceService.cs`):
   - **Serialize**: Trong `GetNodeProperties()`, thêm block cho node type tương ứng (ví dụ `else if (node is YourNodeType yourNode)`):  
     `dict["TitleColorMode"] = yourNode.TitleColorMode.ToString();`  
     `if (!string.IsNullOrEmpty(yourNode.TitleColorKey)) dict["TitleColorKey"] = yourNode.TitleColorKey;`
   - **Deserialize**: Trong `RestoreNodeProperties()`, thêm block cho node type: dùng `properties.TryGetValue("TitleColorMode", out var tcmObj)` và `Enum.TryParse<TitleColorMode>(...)`, tương tự cho `TitleColorKey` với `tckObj?.ToString()`.

2. **NodeControl**:
   - Khi tạo title TextBlock phải set **`Foreground = GetTitleBrush(node)`** (không set `node.NodeBrush`). Sau khi load workflow, node đã có đúng `TitleColorMode`/`TitleColorKey`; nếu Control không dùng `GetTitleBrush(node)` lúc tạo thì title vẫn hiển thị màu node, trong khi dialog mở ra vẫn thấy màu đã cấu hình.

**Lưu ý**: Nếu sau Save/Import mà dialog hiển thị đúng màu nhưng title trên node vẫn là màu node → kiểm tra NodeControl đã dùng `Foreground = GetTitleBrush(node)` khi tạo title TextBlock và đã serialize/deserialize TitleColorMode, TitleColorKey cho đúng node type.

### Bulk Title Color Feature (Đổi màu tiêu đề hàng loạt)

Tính năng này cho phép thay đổi màu tiêu đề của **tất cả nodes** cùng lúc thông qua một button trên toolbar.

#### Vị trí Button
Button "Đổi màu tiêu đề tất cả nodes" nằm cạnh button "Chọn màu kết nối" trong `WorkflowEditorWindow.xaml`.

#### Cách hoạt động
1. Click vào button (icon "type") để mở context menu
2. Chọn "Màu theo node" để reset về màu mặc định theo node
3. Hoặc chọn một màu cụ thể để áp dụng cho tất cả nodes

#### Implementation
Handlers nằm trong `WorkflowEditorWindow.MinimapManager.cs`:
- `BulkTitleColorButton_Click()` - Hiển thị context menu
- `BulkTitleColor_NodeColor_Click()` - Reset về màu theo node
- `BulkTitleColor_Click()` - Áp dụng màu tùy chọn
- `ApplyBulkTitleColor()` - Logic chính để update tất cả nodes
- `UpdateNodeTitleColor()` - Update màu cho từng node

---

## Theme System & Responsive Screen

> ⚠️ **BẮT BUỘC**: Tất cả dialog node phải dùng `{DynamicResource ...}` thay vì màu hardcode (`#FF1E293B`, `White`, v.v.)  
> Màu hardcode sẽ bị sai khi người dùng đổi sang theme Dark/Dracula hoặc theme tùy chỉnh.

---

### 1. Hệ thống Theme

App tải theme qua `App.xaml`:

```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="Themes/LightTheme.xaml"/>
    <!-- Đổi sang Themes/DarkTheme.xaml nếu cần dark theme -->
</ResourceDictionary.MergedDictionaries>
```

Theme được merge theo thứ tự: `LightTheme.xaml` → `Base/Common.xaml` → `Base/Colors/Common.xaml` → `Base/Colors/Light.xaml` → `Controls/*.xaml`.

Khi bạn đổi theme, tất cả `{DynamicResource}` trong dialogs tự cập nhật – không cần restart.

---

### 2. Bảng DynamicResource Keys — Dùng trong Dialog XAML

#### 2.1 Background / Border

| Resource Key | Dùng cho | Mô tả |
|---|---|---|
| `DialogOuterBorder` | `<Border Style>` | Style hoàn chỉnh cho outer border của dialog (background + border + corner radius) |
| `DialogHeaderBorder` | `<Border Style>` | Style hoàn chỉnh cho header của dialog |
| `WindowBackground` | `Background` | Màu nền chính của cửa sổ / background card |
| `WindowBodyBackground` | `Background` | Màu nền body (nhạt hơn WindowBackground một chút) |
| `HeaderBackgroundBrush` | `Background` | Màu nền header |
| `CardBackgroundBrush` | `Background` | Background của card / section bên trong dialog |
| `ControlBorderBrush` | `BorderBrush` | Màu viền mặc định cho controls và panel |

#### 2.2 Text / Foreground

| Resource Key | Dùng cho | Mô tả |
|---|---|---|
| `TextBrush` | `Foreground` | Màu text chính — **LUÔN dùng thay cho `Foreground="White"`** |
| `TextSecondary` | `Foreground` | Text phụ / mô tả (mờ hơn TextBrush) |
| `TextMuted` | `Foreground` | Text rất mờ, gợi ý placeholder |
| `TextOnPrimaryBrush` | `Foreground` | Text trên nền primary (button, highlight) |

#### 2.3 Accent / Status

| Resource Key | Dùng cho | Mô tả |
|---|---|---|
| `PrimaryBrush` | `Background/Fill` | Màu primary (xanh dương mặc định) |
| `SuccessBrush` | `Background/Fill` | Màu thành công (xanh lá) |
| `DangerBrush` | `Background/Fill` | Màu nguy hiểm (đỏ) |
| `WarningBrush` | `Background/Fill` | Màu cảnh báo (vàng cam) |
| `InfoBrush` | `Background/Fill` | Màu thông tin (xanh nhạt) |
| `AccentColor` | — | Color (không phải Brush) của accent |

#### 2.4 Button Styles

| Resource Key | Dùng trên `<Button Style>` | Mô tả |
|---|---|---|
| `PrimaryButton` | Nút chọn / confirm / play | Background = PrimaryBrush |
| `SecondaryButton` | Nút phụ | Background nhạt |
| `DangerButton` | Nút đóng / xóa | Background = DangerBrush |
| `SuccessButton` | Nút lưu / ok | Background = SuccessBrush |
| `WarningButton` | Nút cảnh báo | Background = WarningBrush |
| `InfoButton` | Nút thông tin | Background = InfoBrush |
| `TransparentButtonStyle` | Nút không có nền | Transparent background |

#### 2.5 Control Styles

| Resource Key | Dùng trên | Mô tả |
|---|---|---|
| `BaseTextBoxV2` | `<TextBox Style>` | TextBox chuẩn — tự follow theme |
| `BaseComboBox` | `<ComboBox Style>` | ComboBox chuẩn |
| `ModernCheckBox` | `<CheckBox Style>` | CheckBox chuẩn |
| `HttpTabItemStyle` | `<TabItem Style>` | Tab item trong dialog (dùng `StaticResource` — không phải `DynamicResource`) |

> **Lưu ý**: `HttpTabItemStyle` dùng **`{StaticResource}`**, không phải `{DynamicResource}` (nó được define trong theme controls layer, resolve ở load-time).

---

### 3. Quy tắc viết XAML theo Theme

#### ✅ ĐÚNG

```xml
<!-- Outer border dialog -->
<Border CornerRadius="12" Padding="0" Style="{DynamicResource DialogOuterBorder}">
    <!-- Header -->
    <Border Grid.Row="0" Style="{DynamicResource DialogHeaderBorder}" Padding="16,12,12,12">

    <!-- Text label -->
    <TextBlock Text="Nhãn:" Foreground="{DynamicResource TextBrush}" FontSize="14"/>

    <!-- Mô tả / gợi ý -->
    <TextBlock Text="Gợi ý..." Foreground="{DynamicResource TextSecondary}" FontSize="12"/>

    <!-- Card/panel nội dung -->
    <Border Background="{DynamicResource WindowBackground}"
            BorderBrush="{DynamicResource ControlBorderBrush}"
            BorderThickness="1" CornerRadius="8" Padding="12">

    <!-- Tab item -->
    <TabItem Header="Logic" Style="{StaticResource HttpTabItemStyle}">
```

#### ❌ SAI — Không được làm

```xml
<!-- SAI: màu hardcode không follow theme -->
<Border Background="#FF1E293B" BorderBrush="#33FFFFFF">
<Border Background="#FF0F172A">
<TextBlock Foreground="White">
<TextBlock Foreground="#CCCCCC">
```

---

### 4. Responsive Screen — NodeDialogManager Auto-Sizing

`NodeDialogManager.OpenDialog()` tự động:

| Hành vi | Giá trị |
|---|---|
| **Vị trí** | Góc phải màn hình, căn dưới (không đè taskbar) |
| **Chiều cao** | `90%` chiều cao `WorkingArea` (tôn trọng `MinHeight`/`MaxHeight` của dialog) |
| **Chiều rộng** | Lấy từ `dialog.Width` (tôn trọng `MaxWidth` của dialog) |
| **Scale** | Áp dụng `UIScaleFactor` qua `LayoutTransform = new ScaleTransform(uiScale, uiScale)` |

#### 4.1 UIScaleFactor — Scale tự động theo màn hình

`UIScaleFactor` là resource (`double`) trong `Application.Current.Resources`, được tính từ độ phân giải màn hình khi app khởi động. Dialogs tự nhận scale mà **không cần code thêm** — `NodeDialogManager` gán `LayoutTransform` trước khi `Show()`.

Quy tắc khi thiết kế dialog:
- Đặt kích thước tự nhiên (không scale — `Width="480"`)
- Đặt `MinWidth`, `MinHeight` để dialog không bị quá nhỏ
- Đặt `MaxWidth` nếu dialog không cần rộng hơn giới hạn (ví dụ `MaxWidth="900"`)
- **Không** đặt `MaxHeight` cứng — để `NodeDialogManager` tự tính

#### 4.2 Template kích thước đề xuất cho dialog

```xml
<local:BaseNodeDialog ...
    Width="460"        <!-- Chiều rộng mặc định -->
    MinWidth="350"     <!-- Tối thiểu -->
    MaxWidth="900"     <!-- Giới hạn tối đa -->
    MinHeight="350"    <!-- MinHeight bắt buộc để tránh bị quá nhỏ -->
    <!-- KHÔNG đặt Height cứng vì NodeDialogManager auto-size 90% screen -->
    <!-- KHÔNG đặt MaxHeight cố định -->
>
```

#### 4.3 Tại sao không đặt Height cứng?

`NodeDialogManager.PositionDialog()` override `dialog.Width` và `dialog.Height` sau khi dialog hiển thị:
- `dialog.Height = Min(90% workingArea, MaxHeight)`
- `dialog.Left = workingArea.Right - finalWidth`
- `dialog.Top = workingArea.Bottom - dialogHeight`

Nếu bạn đặt `Height="600"` nhưng màn hình chỉ có 768px, dialog bị cắt. Để trống `Height` và chỉ dùng `MinHeight`.

---

### 5. Ví dụ thực tế: AssignDataNodeDialog (chuẩn)

File [`AssignDataNodeDialog.xaml`](../Views/Overlays/AssignDataNodeDialog.xaml) là **dialog mẫu chuẩn** nhất:

```xml
<!-- Width/MinWidth/MaxWidth đặt --- KHÔNG đặt Height hay MaxHeight -->
<local:BaseNodeDialog Width="480" MinWidth="350" MaxWidth="900" MinHeight="350">

    <!-- Outer border dùng Style (DynamicResource) -->
    <Border CornerRadius="12" Padding="0" Style="{DynamicResource DialogOuterBorder}">

        <!-- Header dùng Style (DynamicResource) -->
        <Border Grid.Row="0" Style="{DynamicResource DialogHeaderBorder}" Padding="16,12,12,12">

        <!-- Text dùng DynamicResource TextBrush -->
        <TextBlock Foreground="{DynamicResource TextBrush}"/>
        <TextBlock Foreground="{DynamicResource TextSecondary}"/>

        <!-- Panel background dùng DynamicResource -->
        <Border Background="{DynamicResource WindowBackground}"
                BorderBrush="{DynamicResource ControlBorderBrush}"
                BorderThickness="1" CornerRadius="10" Padding="16">

        <!-- TabItem dùng StaticResource HttpTabItemStyle -->
        <TabItem Header="Logic" Style="{StaticResource HttpTabItemStyle}">
        <TabItem Header="Tái sử dụng flow" Style="{StaticResource HttpTabItemStyle}">
```

---

## Architecture Overview

```
NodeDialogManager (Singleton Service)
├── Manages dialog open/close lifecycle
├── Ensures only 1 dialog open at a time
└── Auto-positions dialog (right side, bottom-aligned)

Dialog Components:
├── YourNodeDialog.xaml (Window)
├── YourNodeDialog.xaml.cs (Code-behind)
├── YourNodeDialogViewModel.cs (ViewModel)
└── YourNodeControl.cs (Node UI builder)

Renderer Components (CRITICAL for TitleDisplayMode):
└── YourNodeRenderer.cs
    ├── RenderNode() - Create node UI
    ├── UpdateNodePosition() - ⚠️ CRITICAL: Sync title position
    └── RemoveNode() - ⚠️ CRITICAL: Cleanup titleTextBlock
```

---

## Implementation Guide - Step by Step

### Step 1: Node Model (Models/Nodes/YourNode.cs)

**⚠️ CRITICAL RULES:**
1. **DO NOT override Title property** với `new` - sẽ break copy logic
2. **Nếu node implement INotifyPropertyChanged**: Phải thêm `NotifyTitleChanged()` method
3. **Nếu hỗ trợ TitleDisplayMode**: Phải thêm `TitleDisplayMode` property và `TitleTextBlockUI` property

#### Template cho Node WITHOUT TitleDisplayMode:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    public sealed class YourNode : WorkflowNode
    {
        // Your properties here
        // public string SomeProperty { get; set; }
        
        public YourNode()
        {
            Type = NodeType.YourType;
            Title = "Your Node";
            // Initialize your properties
        }
    }
}
```

#### Template cho Node WITH TitleDisplayMode và INotifyPropertyChanged:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    public sealed class YourNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        
        // Your properties here
        // public string SomeProperty { get; set; }
        
        public YourNode()
        {
            Type = NodeType.YourType;
            Title = "Your Node";
            // Initialize your properties
        }

        /// <summary>
        /// Chế độ hiển thị tiêu đề của node (mặc định Always).
        /// </summary>
        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set
            {
                if (_titleDisplayMode != value)
                {
                    _titleDisplayMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Reference đến TextBlock hiển thị title trên canvas (được tạo trong YourNodeControl).
        /// </summary>
        public TextBlock? TitleTextBlockUI { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Method helper để notify PropertyChanged khi Title thay đổi từ bên ngoài
        /// (ví dụ: từ ViewModel hoặc khi copy node)
        /// </summary>
        public void NotifyTitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}
```

**⚠️ CRITICAL**: 
- `NotifyTitleChanged()` phải được gọi trong:
  - `SaveTitle()` trong ViewModel (sau khi set `_node.Title = NodeTitle`)
  - `CreateDuplicateNodeInstance()` trong NodeActions.cs (sau khi set `node.Title = newTitle`)
  - `RequestEditNodeTitle()` trong NodeActions.cs (sau khi set `node.Title = input`)

---

### Step 2: Dialog XAML (Views/Overlays/YourNodeDialog.xaml) – sử dụng `BaseNodeDialog`

File: `Views/Overlays/YourNodeDialog.xaml`

```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:controls="clr-namespace:FlowMy.Controls"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:FlowMy.Views.Overlays"
        Title="Your Node Dialog"
        WindowStyle="None"
        ResizeMode="CanResize"
        AllowsTransparency="True"
        Background="Transparent"
        ShowInTaskbar="False"
        Topmost="True"
        Width="400"
        Height="600"
        MinWidth="350"
        MinHeight="400">
    <!-- ✅ ĐÚNG: Dùng Style DynamicResource thay vì Background hardcode -->
    <Border CornerRadius="12" Padding="0" Style="{DynamicResource DialogOuterBorder}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- Header: Title + Play + Close -->
            <Border Grid.Row="0" Style="{DynamicResource DialogHeaderBorder}" Padding="16,12,12,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <TextBox x:Name="TitleTextBox"
                          Grid.Column="0"
                          Text="{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}"
                          Style="{DynamicResource BaseTextBoxV2}"
                          FontSize="16"
                          Padding="0,4,0,4"
                          VerticalContentAlignment="Center"
                          Cursor="IBeam"/>

                    <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                        <Button x:Name="PlayButton" Padding="0,0,0,0"  Width="24" Height="24" Content="▶" FontSize="12"
                                Style="{DynamicResource PrimaryButton}"
                                Cursor="Hand"
                                Margin="8,0,0,0" ToolTip="Chạy logic node này"
                                Command="{Binding RunSingleNodeCommand}"/>

                        <Button x:Name="CloseButton" Padding="0,0,0,0"  Width="24" Height="24"
                                Style="{DynamicResource DangerButton}"
                                Content="×"
                                FontSize="12"
                                FontWeight="Bold"
                                Cursor="Hand"
                                Margin="8,0,0,0" Click="CloseButton_Click"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- Content: Tab Logic + Tab Tái sử dụng flow -->
            <TabControl Grid.Row="1" Background="Transparent" BorderThickness="0">

                <!-- Tab 1: Logic (nội dung chính của node) -->
                <TabItem Header="Logic" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>
                            <!-- TitleDisplayMode (nếu node hỗ trợ) -->
                            <TextBlock Text="Hiển thị tiêu đề:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14"
                                       FontWeight="SemiBold"
                                       Margin="0,0,0,8"/>

                            <ComboBox x:Name="TitleDisplayModeComboBox"
                                      Height="36"
                                      Style="{DynamicResource BaseComboBox}"
                                      Margin="0,0,0,16"
                                      ItemsSource="{Binding TitleDisplayModeOptions}"
                                      SelectedValuePath="Value"
                                      DisplayMemberPath="DisplayName"
                                      SelectedValue="{Binding TitleDisplayMode, Mode=TwoWay}"/>

                            <!-- Custom Properties Section - THÊM CONTROLS CỦA NODE VÀO ĐÂY -->
                            <!-- Ví dụ:
                            <TextBlock Text="Your Property:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <TextBox Text="{Binding YourProperty, Mode=TwoWay}"
                                     Style="{DynamicResource BaseTextBoxV2}"
                                     Height="36" Margin="0,0,0,16"/>
                            -->

                            <!-- Inputs Section -->
                            <TextBlock Text="Inputs:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <StackPanel x:Name="InputsPanel"/>
                            </Border>

                            <!-- Outputs Section -->
                            <TextBlock Text="Outputs:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"
                                       x:Name="TextBlockOutputPanel"/>
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16"
                                    x:Name="BorderOutputPanel">
                                <StackPanel x:Name="OutputsPanel"/>
                            </Border>
                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

                <!-- Tab 2: Tái sử dụng flow (Port position + ReuseRoutes) -->
                <TabItem Header="Tái sử dụng flow" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>
                            <!-- Vị trí cổng IN/OUT -->
                            <TextBlock Text="Vị trí cổng IN/OUT:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Grid Margin="0,0,0,12">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" Margin="0,0,8,0">
                                    <TextBlock Text="Port IN (cổng vào)"
                                               Foreground="{DynamicResource TextBrush}"
                                               FontSize="12" Opacity="0.8" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding InputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                                <StackPanel Grid.Column="1" Margin="8,0,0,0">
                                    <TextBlock Text="Port OUT (cổng ra)"
                                               Foreground="{DynamicResource TextBrush}"
                                               FontSize="12" Opacity="0.8" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding OutputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                            </Grid>

                            <!-- Cấu hình ReuseRoutes -->
                            <ItemsControl ItemsSource="{Binding ReuseRoutes}">
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate>
                                        <Border Margin="0,0,0,12"
                                                Background="{DynamicResource WindowBackground}"
                                                BorderBrush="{DynamicResource ControlBorderBrush}"
                                                BorderThickness="1" CornerRadius="8" Padding="10">
                                            <Grid>
                                                <Grid.ColumnDefinitions>
                                                    <ColumnDefinition Width="2*"/>
                                                    <ColumnDefinition Width="2*"/>
                                                    <ColumnDefinition Width="2*"/>
                                                </Grid.ColumnDefinitions>
                                                <StackPanel Grid.Column="0">
                                                    <TextBlock Text="Node IN"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="12" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <TextBlock Text="{Binding IncomingNodeTitle}"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="13" FontWeight="SemiBold"
                                                               TextTrimming="CharacterEllipsis"/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="1">
                                                    <TextBlock Text="Node OUT"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="12" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <controls:NodeSearchComboBoxUserControl Height="32"
                                                              ItemsSource="{Binding OutgoingOptions}"
                                                              SelectedValuePath="NodeId"
                                                              DisplayMemberPath="Title"
                                                              SelectedValue="{Binding SelectedOutgoingNodeId, Mode=TwoWay}"
                                                              PlaceholderText="Chọn node OUT..."/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="2">
                                                    <TextBlock Text="Kiểu line OUT"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="12" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                                              ItemsSource="{Binding DataContext.ConnectionLineStyleOptions, RelativeSource={RelativeSource AncestorType=Window}}"
                                                              SelectedValuePath="Key"
                                                              DisplayMemberPath="DisplayName"
                                                              SelectedValue="{Binding SelectedLineStyleKey, Mode=TwoWay}"/>
                                                </StackPanel>
                                            </Grid>
                                        </Border>
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>
                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

            </TabControl>
        </Grid>
    </Border>
</local:BaseNodeDialog>
```

#### Header: Nút Play + Đóng (RunSingleNode)

Mọi dialog node đều có **nút Play (▶)** cạnh **nút đóng (×)** trên header:

- **Play**: chạy logic của chính node đó (cập nhật output), không chạy các node tiếp theo.
- **Command**: `RunSingleNodeCommand` (có sẵn trong `BaseNodeDialogViewModel`) → gọi `_host.RequestRunSingleNode(_node)`.
- **Host**: `IWorkflowEditorHost.RequestRunSingleNode(node)` → `WorkflowEditorViewModel.RunSingleNodeAsync(node)` → `WorkflowExecutionService.ExecuteNodeLogicOnlyAsync(node, connections, token, allNodesForLookup: Nodes)`. Truyền **toàn bộ nodes** (`allNodesForLookup`) để executor (ví dụ MediaGalleryNode) có thể resolve node nguồn theo Id (`JsonSourceNodeId` / `ReachableToEnd`).
- **Close**: đóng dialog; gọi `SaveTitleCommand` trước khi `Close()` trong `CloseButton_Click` (và trong `Closing`).

**XAML template header (Play + Close) — dùng style PrimaryButton / DangerButton:**

```xml
<StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
    <Button x:Name="PlayButton" Padding="0,0,0,0"  Width="24" Height="24" Content="▶" FontSize="12"
            Style="{DynamicResource PrimaryButton}"
            Cursor="Hand"
            Margin="8,0,0,0" ToolTip="Chạy logic node này"
            Command="{Binding RunSingleNodeCommand}"/>

    <Button x:Name="CloseButton" Padding="0,0,0,0"  Width="24" Height="24"
            Style="{DynamicResource DangerButton}"
            Content="×"
            FontSize="12"
            FontWeight="Bold"
            Cursor="Hand"
            Margin="8,0,0,0" Click="CloseButton_Click"/>
</StackPanel>
```

Không cần code-behind cho Play: binding `Command="{Binding RunSingleNodeCommand}"` đủ. `CloseButton_Click` vẫn gọi `ViewModel.SaveTitleCommand.Execute(null)` rồi `Close()`.

---

### Step 3: Dialog Code-Behind (Views/Overlays/YourNodeDialog.xaml.cs) – kế thừa `BaseNodeDialog`

File: `Views/Overlays/YourNodeDialog.xaml.cs`

```csharp
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class YourNodeDialog : BaseNodeDialog
    {
        private readonly YourNodeDialogViewModel _viewModel;

        public YourNodeDialog(YourNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            // Initialize ViewModel
            _viewModel = new YourNodeDialogViewModel(node, host);

            // Kết nối với BaseNodeDialog
            InitializeBase(_viewModel, owner);
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
            // Thêm xử lý riêng (nếu cần)
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }
    }
}
```

**⚠️ CRITICAL POINTS:**
1. **Closing event**: Phải gọi `SaveTitleCommand.Execute(null)` để lưu thay đổi
2. **CloseButton_Click**: Phải gọi `SaveTitleCommand.Execute(null)` trước khi `Close()`
3. **outputKeyCombo.ItemsSource**: Phải dùng `SetBinding()` thay vì assignment trực tiếp để auto-refresh

---

### Step 4: Dialog ViewModel (ViewModels/YourNodeDialogViewModel.cs) – kế thừa `BaseNodeDialogViewModel`

File: `ViewModels/YourNodeDialogViewModel.cs`

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly YourNode _yourNode;

        // ADD YOUR NODE PROPERTIES HERE
        // [ObservableProperty]
        // private string _someProperty;

        // Options cho ComboBox TitleDisplayMode (nếu node hỗ trợ)
        public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
        {
            new TitleDisplayModeOption(TitleDisplayMode.Hidden, "Ẩn tiêu đề"),
            new TitleDisplayModeOption(TitleDisplayMode.Hover, "Hiện khi hover"),
            new TitleDisplayModeOption(TitleDisplayMode.Always, "Luôn hiện")
        };

        public YourNodeDialogViewModel(YourNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _yourNode = node ?? throw new ArgumentNullException(nameof(node));

            // Sync thêm properties riêng nếu cần
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    // Ví dụ:
                    // if (e.PropertyName == nameof(YourNode.SomeProperty))
                    //     SomeProperty = _yourNode.SomeProperty;

                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }
        }

        protected override string GetDefaultTitle() => "Your Node";

        // Override OnSaveTitle để lưu thêm properties riêng (ngoài Title/TitleDisplayMode đã xử lý trong base)
        protected override void OnSaveTitle()
        {
             _yourNode.NotifyTitleChanged();

            // Ví dụ:
            // if (_yourNode.SomeProperty != SomeProperty)
            // {
            //     _yourNode.SomeProperty = SomeProperty;
            //     _host.RequestSyncDataPanels(immediate: true);
            // }
        }
    }
}
```

**⚠️ CRITICAL POINTS:**
1. **PropertyChanged handlers**: Phải combine TẤT CẢ trong MỘT block `if (node is INotifyPropertyChanged npc)` để tránh lỗi "variable already defined"
2. **SaveTitle()**: Phải gọi `NotifyTitleChanged()` sau khi set Title nếu node có INotifyPropertyChanged
3. **TitleDisplayModeOption**: Kiểm tra xem class này đã được định nghĩa trong ViewModel khác chưa (ví dụ: KeyPressEventNodeDialogViewModel.cs). Nếu đã có, KHÔNG định nghĩa lại.
4. **Refresh AvailableSources**: ⚠️ **CRITICAL** - Phải gọi `RefreshAvailableSourcesForInputs()` trong `LoadInputs()` trước khi tạo `InputItemViewModel` để đảm bảo combobox source node hiển thị tiêu đề mới nhất khi mở dialog. Nếu không refresh, combobox sẽ chỉ hiển thị tiêu đề cũ từ cache.

---

### Step 5: Node Control (Views/NodeControls/YourNodeControl.cs)

File: `Views/NodeControls/YourNodeControl.cs`

#### Template cho Node WITHOUT TitleDisplayMode:

```csharp
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Views.Overlays;
using FlowMy.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.NodeControls
{
    public static class YourNodeControl
    {
        public static Border CreateBorder(YourNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

            // RECOMMENDED: Use SvgViewboxEx
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "your-icon-key", System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri ?? new Uri("/Assets/Icons/default.svg", UriKind.RelativeOrAbsolute),
                Width = 32, Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetTextBrush(node.ColorKey)
            };
            grid.Children.Add(iconSvg);

            var border = new Border
            {
                Child = grid,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Direction = 270, ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node
            };

            // ⚠️ BẮT BUỘC: Cho phép border nhận focus keyboard khi hover
            border.Focusable = true;
            border.FocusVisualStyle = null; // Ẩn focus visual mặc định (nét đứt)

            // ─── KEYBOARD PORT POSITION (Arrow = Port IN, Shift+Arrow = Port OUT) ───
            bool isHovering = false;
            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                border.Focus(); // Focus border để nhận keyboard events
            };
            border.MouseLeave += (s, e) => { isHovering = false; };

            border.PreviewKeyDown += (s, e) =>
            {
                if (!isHovering) return;

                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PortPosition? newPos = e.Key switch
                {
                    Key.Left  => PortPosition.Left,
                    Key.Up    => PortPosition.Top,
                    Key.Right => PortPosition.Right,
                    Key.Down  => PortPosition.Bottom,
                    _ => null
                };

                if (newPos == null) return;

                e.Handled = true;
                ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
            };

            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            return border;
        }

        private static void OpenNodeDialog(YourNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                // ⚠️ CRITICAL: Release mouse capture và clear drag state để tránh node nhảy đến vị trí chuột
                if (node.Border != null && node.Border.IsMouseCaptured)
                {
                    node.Border.ReleaseMouseCapture();
                }
                
                // ⚠️ CRITICAL: Clear DraggedNode để đảm bảo dialog có thể đóng ngay lập tức
                host.DraggedNode = null;
                
                // ⚠️ CRITICAL: Deselect node ngay khi click chuột phải để tránh node nhảy đến vị trí chuột
                if (host.ViewModel != null)
                {
                    host.ViewModel.SelectedNode = null;
                }

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new YourNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            if (host is WorkflowEditorWindow window)
            {
                var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager) return manager;
            }
            return new NodeDialogManager();
        }

        private static Brush GetTextBrush(string colorKey)
        {
            return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        // ===== KEYBOARD PORT POSITION =====
        /// <summary>
        /// Đổi vị trí port IN hoặc OUT bằng phím mũi tên khi hover.
        /// Arrow keys (không Shift) → đổi Port IN.
        /// Shift + Arrow keys → đổi Port OUT.
        /// </summary>
        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;

            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);

            if (port == null || port.Position == newPosition) return;

            port.Position = newPosition;

            // Cập nhật vị trí port trên canvas
            host.UpdatePortsPositionOnSide(node, newPosition);

            // Redraw connections để line bám theo port mới
            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                }
                catch { /* best-effort */ }
            }
        }
    }
}
```

#### Template cho Node WITH TitleDisplayMode (Complete Implementation):

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Views.Overlays;
using FlowMy.Views;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowMy.Views.NodeControls
{
    public static class YourNodeControl
    {
        // ⚠️ CRITICAL: Throttle title position updates để tránh giật khi pan/zoom
        private static readonly System.Collections.Generic.Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50;
        
        // ⚠️ CRITICAL: Track xem đã update sau khi zoom kết thúc chưa để tránh update nhiều lần không cần thiết
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();

        public static Border CreateBorder(YourNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

            // RECOMMENDED: Use SvgViewboxEx
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "your-icon-key", System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri ?? new Uri("/Assets/Icons/default.svg", UriKind.RelativeOrAbsolute),
                Width = 32, Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetTextBrush(node.ColorKey)
            };
            grid.Children.Add(iconSvg);

            // ⚠️ CRITICAL: Tạo titleTextBlock với Visibility dựa trên TitleDisplayMode
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Your Node",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = node.NodeBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false // Không block mouse events
            };

            // Lưu reference để có thể cập nhật sau
            node.TitleTextBlockUI = titleTextBlock;

            // Xử lý hover để hiển thị/ẩn tiêu đề
            bool isHovering = false;

            var border = new Border
            {
                Child = grid,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Direction = 270, ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node
            };

            // ⚠️ CRITICAL: Combine ALL PropertyChanged handlers in ONE block
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.ColorKey))
                    {
                        iconSvg.Fill = GetTextBrush(node.ColorKey);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleTextBlock.Foreground = node.NodeBrush;
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = node.Title ?? "Your Node";
                        // ⚠️ Chỉ update position nếu node visible
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        {
                            UpdateTitlePosition(titleTextBlock, border, host);
                        }
                    }
                    else if (e.PropertyName == nameof(YourNode.TitleDisplayMode))
                    {
                        // ⚠️ Chỉ update visibility nếu node visible
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                        {
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                        }
                    }
                };
            }

            // ⚠️ BẮT BUỘC: Cho phép border nhận focus keyboard khi hover
            border.Focusable = true;
            border.FocusVisualStyle = null; // Ẩn focus visual mặc định (nét đứt)

            // ⚠️ CRITICAL: Hover handling để hiển thị/ẩn title khi TitleDisplayMode = Hover
            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                // ⚠️ Chỉ update visibility nếu node border đang visible (trong viewport)
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                // ⚠️ Focus border để nhận keyboard events (Arrow keys đổi port)
                border.Focus();
            };
            
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                // ⚠️ Chỉ update visibility nếu node border đang visible (trong viewport)
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                }
            };

            // ─── KEYBOARD PORT POSITION (Arrow = Port IN, Shift+Arrow = Port OUT) ───
            // Khi chuột hover trên node + nhấn phím mũi tên → đổi vị trí port ngay lập tức
            // KHÔNG cần mở dialog → UX nhanh hơn
            border.PreviewKeyDown += (s, e) =>
            {
                if (!isHovering) return;

                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PortPosition? newPos = e.Key switch
                {
                    Key.Left  => PortPosition.Left,
                    Key.Up    => PortPosition.Top,
                    Key.Right => PortPosition.Right,
                    Key.Down  => PortPosition.Bottom,
                    _ => null
                };

                if (newPos == null) return;

                e.Handled = true;
                ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
            };

            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            // ⚠️ CRITICAL: Sync title visibility với border visibility khi border visibility thay đổi (viewport culling)
            var visibilityDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                }
            });

            // ⚠️ CRITICAL: Đảm bảo titleTextBlock được thêm vào Canvas sau khi border được render
            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            // Update position khi border di chuyển hoặc resize
            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);

            // ⚠️ CRITICAL: Cleanup để tránh memory leak khi node bị remove/unload
            border.Unloaded += (s, e) =>
            {
                try
                {
                    // Stop & remove throttling timer
                    if (_titleUpdateTimers.TryGetValue(border, out var timer))
                    {
                        timer.Stop();
                        _titleUpdateTimers.Remove(border);
                    }
                    _titleUpdatedAfterZoom.Remove(border);

                    // Remove titleTextBlock khỏi canvas (nếu còn)
                    if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
                    {
                        host.WorkflowCanvas.Children.Remove(titleTextBlock);
                    }

                    // Clear reference để tránh giữ UI element
                    if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                    {
                        node.TitleTextBlockUI = null;
                    }
                }
                catch
                {
                    // Best-effort cleanup; ignore to avoid crashing unload path
                }
            };

            // ⚠️ CRITICAL: LayoutUpdated handler với zoom handling và throttling
            border.LayoutUpdated += (s, e) =>
            {
                // ⚠️ Sync visibility với border trước
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    return;
                }
                
                bool isZooming = NodeChrome.IsZooming;
                
                // ⚠️ CRITICAL: Nếu đang zoom, ẩn title để tránh xử lý và đánh dấu chưa update
                // Chỉ set Visibility.Collapsed khi chưa phải Collapsed để tránh property change overhead
                if (isZooming)
                {
                    if (titleTextBlock.Visibility != Visibility.Collapsed)
                    {
                        titleTextBlock.Visibility = Visibility.Collapsed;
                    }
                    _titleUpdatedAfterZoom[border] = false;
                    return;
                }
                
                // ⚠️ CRITICAL: Nếu zoom vừa kết thúc (không còn zooming) và chưa update -> update ngay lập tức
                bool hasUpdatedAfterZoom = _titleUpdatedAfterZoom.TryGetValue(border, out var updated) && updated;
                if (!hasUpdatedAfterZoom && border.Visibility == Visibility.Visible)
                {
                    // Đánh dấu đã update để tránh update nhiều lần
                    _titleUpdatedAfterZoom[border] = true;
                    
                    // Update visibility theo TitleDisplayMode
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    
                    // Nếu title visible, update position ngay lập tức (không throttle)
                    if (titleTextBlock.Visibility == Visibility.Visible)
                    {
                        UpdateTitlePosition(titleTextBlock, border, host);
                    }
                }
                
                // Skip updates khi đang pan hoặc drag để tránh giật
                if (host.IsPanning || host.DraggedNode == node)
                {
                    return;
                }
                
                // Throttle updates bằng DispatcherTimer cho các trường hợp khác (node di chuyển, resize, etc.)
                if (titleTextBlock.Visibility == Visibility.Visible)
                {
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            return border;
        }

        private static void OpenNodeDialog(YourNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                // ⚠️ CRITICAL: Release mouse capture và clear drag state để tránh node nhảy đến vị trí chuột
                if (node.Border != null && node.Border.IsMouseCaptured)
                {
                    node.Border.ReleaseMouseCapture();
                }
                
                // ⚠️ CRITICAL: Clear DraggedNode để đảm bảo dialog có thể đóng ngay lập tức
                host.DraggedNode = null;
                
                // ⚠️ CRITICAL: Deselect node ngay khi click chuột phải để tránh node nhảy đến vị trí chuột
                if (host.ViewModel != null)
                {
                    host.ViewModel.SelectedNode = null;
                }

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new YourNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            if (host is WorkflowEditorWindow window)
            {
                var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager) return manager;
            }
            return new NodeDialogManager();
        }

        private static Brush GetTextBrush(string colorKey)
        {
            return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        // ⚠️ CRITICAL: Helper methods cho TitleDisplayMode
        private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering)
        {
            return mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };
        }

        private static void UpdateTitleVisibility(TextBlock titleTextBlock, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
        {
            // ⚠️ CRITICAL: Nếu node border bị ẩn (không trong viewport), ẩn title luôn
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible)
            {
                titleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }
            
            // Nếu node visible, áp dụng TitleDisplayMode
            titleTextBlock.Visibility = GetTitleVisibility(mode, isHovering);
        }

        private static void ThrottledUpdateTitlePosition(TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
        {
            if (!_titleUpdateTimers.TryGetValue(border, out var timer))
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    UpdateTitlePosition(titleTextBlock, border, host);
                };
                _titleUpdateTimers[border] = timer;
            }

            timer.Stop();
            timer.Start();
        }

        private static void UpdateTitlePosition(TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
        {
            if (host.WorkflowCanvas == null || border == null || !host.WorkflowCanvas.Children.Contains(titleTextBlock)) return;

            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);

            // Fallback to node position nếu Canvas position chưa được set
            if (double.IsNaN(left) && border.Tag is WorkflowNode node)
            {
                left = node.X;
            }
            if (double.IsNaN(top) && border.Tag is WorkflowNode node2)
            {
                top = node2.Y;
            }

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            // Đảm bảo ActualWidth và ActualHeight đã được tính toán
            if (titleTextBlock.ActualWidth == 0 || titleTextBlock.ActualHeight == 0)
            {
                titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
            }

            // Đặt titleTextBlock phía trên border (center horizontally)
            var titleLeft = left + (border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
            var titleTop = top - titleTextBlock.ActualHeight - 4; // 4px spacing

            Canvas.SetLeft(titleTextBlock, titleLeft);
            Canvas.SetTop(titleTextBlock, titleTop);
        }

        // ===== KEYBOARD PORT POSITION =====
        /// <summary>
        /// Đổi vị trí port IN hoặc OUT bằng phím mũi tên khi hover.
        /// Arrow keys (không Shift) → đổi Port IN.
        /// Shift + Arrow keys → đổi Port OUT.
        /// </summary>
        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;

            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);

            if (port == null || port.Position == newPosition) return;

            port.Position = newPosition;

            // Cập nhật vị trí port trên canvas
            host.UpdatePortsPositionOnSide(node, newPosition);

            // Redraw connections để line bám theo port mới
            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                }
                catch { /* best-effort */ }
            }
        }
    }
}
```

**⚠️ CRITICAL POINTS:**
1. **Static dictionaries**: Phải khai báo `_titleUpdateTimers` và `_titleUpdatedAfterZoom` ở class level
2. **PropertyChanged handlers**: Phải combine TẤT CẢ trong MỘT block
3. **Hover handling**: Phải track `isHovering` state và update visibility trong MouseEnter/MouseLeave
4. **Viewport culling**: Phải sync với border visibility qua DependencyPropertyDescriptor
5. **LayoutUpdated**: Phải có zoom handling và throttling để tránh performance issues
6. **TitleTextBlockUI**: Phải được thêm vào WorkflowCanvas, không phải vào border
7. **Keyboard Port Position**: Phải có `border.Focusable = true`, `border.FocusVisualStyle = null`, `border.Focus()` trong MouseEnter, và `PreviewKeyDown` handler gọi `ChangePortPosition()`. Arrow keys đổi Port IN, Shift+Arrow đổi Port OUT

---

### Step 6: Renderer (Services/Rendering/YourNodeRenderer.cs)

**⚠️ CRITICAL**: Nếu node hỗ trợ TitleDisplayMode, Renderer PHẢI implement 2 methods sau:

#### 1. RenderNode() - Hide Header Buttons

```csharp
public void RenderNode(WorkflowNode node, Canvas canvas)
{
    if (node is not YourNode yourNode)
        throw new InvalidOperationException("YourNodeRenderer can only render YourNode.");

    yourNode.Border = YourNodeControl.CreateBorder(
        yourNode,
        Host as System.Windows.Window ?? throw new InvalidOperationException("Host must be a Window."),
        Host
    );
    NodeChrome.Apply(yourNode.Border, yourNode, Host);

    // ... rest of rendering code ...
    
    // ⚠️ CRITICAL: Always update port colors (see Rule 2.5)
    foreach (var port in yourNode.Ports.Where(p => p.IsVisible))
    {
        // Determine port color
        Color portColor;
        if (!string.IsNullOrWhiteSpace(port.ColorKey))
        {
            var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush") 
                            ?? GetColorFromTheme(port.ColorKey);
            portColor = colorFromKey ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
        }
        else
        {
            portColor = port.IsInput
                ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
        }
        
        if (port.PortUI == null)
        {
            port.PortUI = _portRenderer.CreatePort(portColor);
            port.PortUI.Tag = port;
        }
        
        // ⚠️ CRITICAL: ALWAYS update color, even if port already exists
        if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
        {
            ellipse.Fill = new SolidColorBrush(portColor);
        }

        _portRenderer.UpdatePortsPositionOnSide(yourNode, port.Position);
        _portRenderer.EnsurePortAddedToCanvas(port);
        Host.ZIndexManager.SetPortZIndex(yourNode, port.PortUI);
    }
}
```

#### 2. UpdateNodePosition() - Sync Title Position (REQUIRED if TitleDisplayMode supported)

```csharp
public void UpdateNodePosition(WorkflowNode node, double x, double y)
{
    node.X = x;
    node.Y = y;

    if (node.Border != null)
    {
        Canvas.SetLeft(node.Border, x);
        Canvas.SetTop(node.Border, y);
    }

    // ⚠️ CRITICAL: Update titleTextBlock position nếu có
    if (node is YourNode yourNode && yourNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
    {
        var titleTextBlock = yourNode.TitleTextBlockUI;
        
        // Đảm bảo titleTextBlock được thêm vào canvas nếu chưa có
        if (!Host.WorkflowCanvas.Children.Contains(titleTextBlock))
        {
            Host.WorkflowCanvas.Children.Add(titleTextBlock);
            Panel.SetZIndex(titleTextBlock, 20000);
        }
        
        if (node.Border != null)
        {
            // Đảm bảo ActualWidth và ActualHeight đã được tính toán
            if (titleTextBlock.ActualWidth == 0 || titleTextBlock.ActualHeight == 0)
            {
                titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
            }

            // Đặt titleTextBlock phía trên border (center horizontally)
            var titleLeft = x + (node.Border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
            var titleTop = y - titleTextBlock.ActualHeight - 4; // 4px spacing
            Canvas.SetLeft(titleTextBlock, titleLeft);
            Canvas.SetTop(titleTextBlock, titleTop);
        }
    }

    // ⚠️ CRITICAL: Also update port colors in UpdateNodePosition()
    foreach (var port in node.Ports.Where(p => p.IsVisible))
    {
        // Same color logic as RenderNode()
        Color portColor;
        if (!string.IsNullOrWhiteSpace(port.ColorKey))
        {
            var colorFromKey = GetColorFromTheme($"{port.ColorKey}Brush") 
                            ?? GetColorFromTheme(port.ColorKey);
            portColor = colorFromKey ?? (port.IsInput ? Colors.Orange : Colors.Cyan);
        }
        else
        {
            portColor = port.IsInput
                ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
        }
        
        if (port.PortUI == null)
        {
            port.PortUI = _portRenderer.CreatePort(portColor);
            port.PortUI.Tag = port;
        }
        else
        {
            // ⚠️ CRITICAL: ALWAYS update color, even if port already exists
            if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
            {
                ellipse.Fill = new SolidColorBrush(portColor);
            }
        }

        _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
        _portRenderer.EnsurePortAddedToCanvas(port);
        Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
    }
}
```

#### 3. RemoveNode() - Cleanup Title (REQUIRED if TitleDisplayMode supported)

```csharp
public void RemoveNode(WorkflowNode node, Canvas canvas)
{
    if (node.Border != null && canvas.Children.Contains(node.Border))
    {
        canvas.Children.Remove(node.Border);
    }

    // ⚠️ CRITICAL: Remove titleTextBlock nếu có
    if (node is YourNode yourNode && yourNode.TitleTextBlockUI != null)
    {
        var titleTextBlock = yourNode.TitleTextBlockUI;
        if (canvas != null && canvas.Children.Contains(titleTextBlock))
        {
            canvas.Children.Remove(titleTextBlock);
        }
        // Clear reference để tránh memory leak và hiển thị lại
        yourNode.TitleTextBlockUI = null;
    }

    // Remove ports
    foreach (var port in node.Ports)
    {
        if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
        {
            canvas.Children.Remove(port.PortUI);
        }
    }
}
```

**⚠️ CRITICAL POINTS:**
1. **Port colors**: Phải update trong CẢ `RenderNode()` và `UpdatePosition()` (kể cả khi `port.PortUI` đã tồn tại)
2. **UpdateNodePosition()**: Phải sync title position khi node di chuyển
3. **RemoveNode()**: Phải cleanup titleTextBlock khỏi Canvas và clear reference

#### 4. Rectangular Ports (Port hình chữ nhật) - Optional

Một số node đặc biệt như `WebNode` và `HtmlUiNode` sử dụng port hình chữ nhật (rectangular ports) thay vì port tròn (circular ports) để có giao diện khác biệt và dễ nhận biết hơn.

**Khi nào nên dùng port chữ nhật:**
- Node có UI đặc biệt (như WebView2, HTML UI)
- Cần port lớn hơn để dễ nhận biết và kết nối
- Node có nhiều ports và cần phân biệt rõ ràng

**Cách sử dụng:**

Trong `RenderNode()` và `UpdateNodePosition()`, thay vì dùng `_portRenderer.CreatePort()`, dùng `_portRenderer.CreateRectangularPortWithMargin()`:

```csharp
// Trong RenderNode() hoặc UpdateNodePosition()
foreach (var port in node.Ports.Where(p => p.IsVisible))
{
    var portColor = GetColorFromTheme($"{node.ColorKey}Brush") ?? Colors.Gray;
    
    if (port.PortUI == null)
    {
        // ✅ Tạo port chữ nhật với margin wrapper
        // Margin khác nhau tùy vị trí để dễ nhìn khi bị khuất
        var margin = GetPortMarginForPosition(port.Position);
        port.PortUI = _portRenderer.CreateRectangularPortWithMargin(
            portColor, 
            margin, 
            width: 12,   // Chiều rộng port (mặc định: 10)
            height: 25   // Chiều cao port (mặc định: 18)
        );
        port.PortUI.Tag = port;
    }
    else
    {
        // ⚠️ CRITICAL: Update màu port nếu đã tồn tại
        var shape = PortRenderer.GetActualPortShape(port.PortUI);
        if (shape != null)
            shape.Fill = new SolidColorBrush(portColor);
    }
    
    _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
    _portRenderer.EnsurePortAddedToCanvas(port);
    Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
}
```

**Helper method để tính margin theo vị trí:**

```csharp
/// <summary>Lấy margin cho port dựa trên vị trí để dễ nhìn khi bị khuất.</summary>
private static Thickness GetPortMarginForPosition(PortPosition position)
{
    return position switch
    {
        PortPosition.Left => new Thickness(6, 2, 15, 2),   // Margin phải lớn hơn để port sang trái nhiều hơn
        PortPosition.Right => new Thickness(15, 2, 6, 2),  // Margin trái lớn hơn để port sang phải nhiều hơn
        PortPosition.Top => new Thickness(2, 3, 2, 1),   // Margin trên lớn hơn
        PortPosition.Bottom => new Thickness(2, 1, 2, 3), // Margin dưới lớn hơn
        _ => new Thickness(2)
    };
}
```

**Lưu ý quan trọng:**

1. **Kích thước mặc định**: Port chữ nhật có kích thước mặc định là `10x18` (width x height). Với WebNode và HtmlUiNode, kích thước được tùy chỉnh thành `12x25` để dễ nhận biết hơn.

2. **Margin wrapper**: `CreateRectangularPortWithMargin()` tự động wrap port trong một `Border` với margin để port dễ nhìn khi bị khuất bởi node border. Border này có:
   - `IsHitTestVisible = true`: Cho phép hit-test trên toàn bộ vùng margin để dễ kết nối
   - `ClipToBounds = false`: Không clip để port có thể phóng to khi highlight
   - Mouse events được forward từ Border đến Rectangle bên trong

3. **Highlight effect**: Khi kéo dây kết nối đến port, port sẽ tự động phóng to (+2px mỗi chiều) để hiển thị hiệu ứng highlight. Kích thước gốc được lưu trong `Rectangle.Tag` (kiểu `Size`) để có thể reset đúng sau khi highlight.

4. **Hit-test area**: Port chữ nhật có hit-test area lớn hơn nhờ Border wrapper, giúp dễ kết nối hơn so với port tròn. Threshold distance cho distance-based detection là `30px` (thay vì `20px` cho port tròn).

5. **Color update**: Giống như port tròn, phải update màu trong CẢ `RenderNode()` và `UpdateNodePosition()`, sử dụng `PortRenderer.GetActualPortShape()` để lấy shape thực tế (có thể là Rectangle hoặc Rectangle trong Border wrapper).

**Ví dụ hoàn chỉnh:**

Xem `Services/Rendering/WebNodeRenderer.cs` và `Services/Rendering/HtmlUiNodeRenderer.cs` để tham khảo cách implement đầy đủ.

---

### Step 7: Persistence Support (REQUIRED for all node types)

**⚠️ CRITICAL**: Khi tạo node mới hoặc thêm properties mới, bạn PHẢI implement serialize/deserialize logic để các properties được lưu khi save/export JSON và được khôi phục khi load/import.

#### 7.1: Serialize Properties (GetNodeProperties)

File: `Services/Workflow/FileWorkflowPersistenceService.cs`

Thêm logic serialize trong method `GetNodeProperties()`:

```csharp
private static Dictionary<string, object> GetNodeProperties(WorkflowNode node)
{
    var dict = new Dictionary<string, object>();

    // ... existing code for other node types ...

    else if (node is YourNode yourNode)
    {
        // Serialize simple properties
        if (!string.IsNullOrWhiteSpace(yourNode.SomeProperty))
            dict["SomeProperty"] = yourNode.SomeProperty;
        
        // Serialize enum properties
        dict["SomeEnumProperty"] = yourNode.SomeEnumProperty.ToString();
        
        // Serialize List/Array properties (CRITICAL - nhiều người quên!)
        if (yourNode.OutputMappings != null && yourNode.OutputMappings.Count > 0)
        {
            var mappingsJson = JsonSerializer.Serialize(yourNode.OutputMappings.Select(m => new
            {
                NewKey = m.NewKey,
                SourceNodeId = m.SourceNodeId,
                SourceOutputKey = m.SourceOutputKey
            }).ToList());
            dict["OutputMappings"] = mappingsJson;
        }
        
        // Serialize TitleDisplayMode (nếu node hỗ trợ)
        dict["TitleDisplayMode"] = yourNode.TitleDisplayMode.ToString();
    }

    // ... rest of method ...
}
```

**⚠️ CRITICAL POINTS:**
1. **List/Array properties**: Phải serialize thành JSON string (tương tự `InputArrayValues`)
2. **Complex objects**: Phải serialize thành JSON string hoặc dictionary
3. **Enum properties**: Serialize thành string (`.ToString()`)
4. **Null checks**: Chỉ serialize nếu giá trị không null/empty

#### 7.2: Deserialize Properties (RestoreNodeProperties)

File: `Services/Workflow/FileWorkflowPersistenceService.cs`

Thêm logic deserialize trong method `RestoreNodeProperties()`:

```csharp
private static void RestoreNodeProperties(WorkflowNode node, Dictionary<string, object> properties)
{
    if (properties == null) return;

    // ... existing code for other node types ...

    else if (node is YourNode yourNode)
    {
        // Deserialize simple properties
        if (properties.TryGetValue("SomeProperty", out var somePropObj))
            yourNode.SomeProperty = somePropObj?.ToString() ?? string.Empty;
        
        // Deserialize enum properties
        if (properties.TryGetValue("SomeEnumProperty", out var enumObj))
        {
            var enumStr = enumObj?.ToString();
            if (!string.IsNullOrWhiteSpace(enumStr) &&
                Enum.TryParse<YourEnumType>(enumStr, out var parsedEnum))
            {
                yourNode.SomeEnumProperty = parsedEnum;
            }
        }
        
        // Deserialize List/Array properties (CRITICAL - nhiều người quên!)
        if (properties.TryGetValue("OutputMappings", out var mappingsObj))
        {
            List<OutputMapping>? parsedMappings = null;

            if (mappingsObj is string jsonMappings)
            {
                try
                {
                    var mappingData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonMappings);
                    if (mappingData != null)
                    {
                        parsedMappings = mappingData.Select(m => new OutputMapping
                        {
                            NewKey = m.TryGetValue("NewKey", out var nk) ? nk?.ToString() ?? string.Empty : string.Empty,
                            SourceNodeId = m.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                            SourceOutputKey = m.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                        }).ToList();
                    }
                }
                catch
                {
                    // Try alternative format (direct deserialize)
                    try
                    {
                        parsedMappings = JsonSerializer.Deserialize<List<OutputMapping>>(jsonMappings);
                    }
                    catch { }
                }
            }
            else if (mappingsObj is JsonElement jsonElement)
            {
                try
                {
                    if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        var jsonString = jsonElement.GetString();
                        if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            var mappingData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonString);
                            if (mappingData != null)
                            {
                                parsedMappings = mappingData.Select(m => new OutputMapping
                                {
                                    NewKey = m.TryGetValue("NewKey", out var nk) ? nk?.ToString() ?? string.Empty : string.Empty,
                                    SourceNodeId = m.TryGetValue("SourceNodeId", out var sni) ? sni?.ToString() ?? string.Empty : string.Empty,
                                    SourceOutputKey = m.TryGetValue("SourceOutputKey", out var sok) ? sok?.ToString() ?? string.Empty : string.Empty
                                }).ToList();
                            }
                        }
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        parsedMappings = JsonSerializer.Deserialize<List<OutputMapping>>(jsonElement.GetRawText());
                    }
                }
                catch { }
            }

            if (parsedMappings != null)
            {
                yourNode.OutputMappings = parsedMappings;
                // ⚠️ CRITICAL: Rebuild dependent properties nếu cần
                yourNode.RebuildDynamicOutputs();
            }
        }
        
        // Deserialize TitleDisplayMode (nếu node hỗ trợ)
        if (properties.TryGetValue("TitleDisplayMode", out var tdmObj))
        {
            var tdmStr = tdmObj?.ToString();
            if (!string.IsNullOrWhiteSpace(tdmStr) &&
                Enum.TryParse<TitleDisplayMode>(tdmStr, out var titleDisplayMode))
            {
                yourNode.TitleDisplayMode = titleDisplayMode;
            }
        }
    }

    // ... rest of method ...
}
```

**⚠️ CRITICAL POINTS:**
1. **Multiple format support**: Phải hỗ trợ cả string JSON và JsonElement
2. **Error handling**: Dùng try-catch để tránh crash khi deserialize
3. **Rebuild logic**: Sau khi deserialize List/Array, phải gọi rebuild methods (ví dụ: `RebuildDynamicOutputs()`)
4. **Null safety**: Luôn check null trước khi assign

#### 7.3: Save (Ctrl+S) vs Export — Runtime output

Hai luồng lưu workflow:

| Hành động | Nội dung lưu | Dùng khi |
|-----------|--------------|----------|
| **Ctrl+S** / **nút Save** | Logic (nodes, connections, properties) **+** output đã chạy của từng node (`OutputValues` per node) | Lưu trạng thái đầy đủ; khi mở workflow từ ComboBox sẽ load lại cả output đã lưu. |
| **Nút Export** | Chỉ logic (nodes, connections, properties), **không** có output/runtime | Chia sẻ file JSON, import vào máy khác; file nhẹ, không mang dữ liệu chạy. |

- **Lưu đầy đủ (Ctrl+S)**: `FileWorkflowPersistenceService.Save()` dùng `BuildWorkflowDto(..., includeRuntimeOutput: true)` → mỗi `NodeDto` có thêm `OutputValues` (key = output key, value = giá trị đã resolve). Khi **chọn workflow từ ComboBox**, `Load` → `ImportFromJson` khôi phục `OutputValues` vào `DynamicOutputs.UserValueOverride` và gọi `RefreshSavedOutputs` để cập nhật UI (toggle "Có X kết quả", panel output).
- **Export**: `ExportToJson()` dùng `BuildWorkflowDto(..., includeRuntimeOutput: false)` → không có `OutputValues`; `ImportFromJson` vẫn đọc được file cũ có/không có `OutputValues`.

Node có **DynamicOutputs**: giá trị hiển thị sau khi chạy workflow được lưu khi Ctrl+S và hiển thị lại khi mở workflow từ ComboBox. Không cần thêm code trong GetNodeProperties/RestoreNodeProperties cho output — persistence service tự thu thập/khôi phục qua `NodeDataPanelService.ResolveDynamicValueByKey` và `OutputValues`.

#### 7.3.1: Không lưu Output Values cho Node (Khi cần)

**Khi nào cần không lưu output values?**

Một số node có **property trực tiếp** mà user có thể sửa (ví dụ: `InputNode.Value`, `InputNode.ArrayValues`). Khi save workflow, nếu lưu `UserValueOverride` từ execution, giá trị cũ sẽ override giá trị mới mà user đã sửa khi load lại workflow.

**Vấn đề:**
- User sửa giá trị trong InputNode (ví dụ: `Value = "new value"`)
- Save workflow → lưu `UserValueOverride` từ execution cũ (ví dụ: `"old value"`)
- Load workflow → restore `UserValueOverride` → override giá trị mới → user thấy giá trị cũ

**Giải pháp:** Không lưu và không restore output values cho các node có property trực tiếp.

**Implementation:**

File: `Services/Workflow/FileWorkflowPersistenceService.cs`

**1. Sửa `GetNodeOutputValues()` để không lưu output values:**

```csharp
private static Dictionary<string, string>? GetNodeOutputValues(WorkflowNode node)
{
    if (node.DynamicOutputs == null || node.DynamicOutputs.Count == 0)
        return null;

    // ⚠️ CRITICAL: Không lưu output values cho InputNode và các node có property trực tiếp
    // để tránh tình trạng giá trị cũ (từ execution) override giá trị mới (từ user edit)
    if (node is InputNode)
    {
        // InputNode có property Value/ArrayValues mà user có thể sửa trực tiếp
        // Không lưu UserValueOverride để tránh conflict với giá trị mới
        return null;
    }

    // Đặc biệt xử lý WebNode: không serialize output values khi WebView2 đang chạy
    // vì có thể có các giá trị lớn hoặc phức tạp không thể serialize
    if (node is WebNode)
    {
        // Bỏ qua serialize output values cho WebNode để tránh lỗi
        return null;
    }

    // ... rest of method để lưu output values cho các node khác ...
}
```

**2. Sửa phần restore để không restore output values:**

Trong method `ImportFromJson()`, tìm phần restore output values và thêm check:

```csharp
// Khôi phục output đã lưu (Ctrl+S) để hiển thị lại khi mở workflow từ ComboBox
// ⚠️ CRITICAL: Không restore output values cho InputNode và các node có property trực tiếp
// để tránh override giá trị mới (từ user edit) bằng giá trị cũ (từ execution)
if (nodeDto.OutputValues != null && nodeDto.OutputValues.Count > 0 && node.DynamicOutputs != null)
{
    // Bỏ qua InputNode vì nó có property Value/ArrayValues mà user có thể sửa trực tiếp
    if (!(node is InputNode))
    {
        foreach (var output in node.DynamicOutputs)
        {
            var key = output.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (nodeDto.OutputValues.TryGetValue(key, out var savedVal) && !string.IsNullOrWhiteSpace(savedVal))
                output.UserValueOverride = savedVal;
        }
    }
}
```

**Khi nào áp dụng pattern này:**

- ✅ **InputNode**: Có `Value`/`ArrayValues` property mà user sửa trực tiếp trong dialog
- ✅ **Các node khác có property trực tiếp**: Nếu node có property mà user có thể sửa trực tiếp (không phải từ execution), nên không lưu output values
- ❌ **Các node khác**: Vẫn lưu output values bình thường để hiển thị kết quả execution

**Lưu ý:**
- Pattern này chỉ áp dụng cho các node có **property trực tiếp** mà user sửa (ví dụ: InputNode.Value)
- Các node khác vẫn lưu output values để hiển thị kết quả execution (ví dụ: HttpRequestNode, CodeNode, etc.)
- WebNode đã được xử lý riêng vì lý do kỹ thuật (giá trị lớn, phức tạp)

#### 7.4: Reference Implementation

Xem implementation đầy đủ trong:
- `Services/Workflow/FileWorkflowPersistenceService.cs`:
  - `GetNodeProperties()` - Serialize cho `ListOutNode`, `InputNode`, `LoopNode`, etc.
  - `RestoreNodeProperties()` - Deserialize cho `ListOutNode`, `InputNode`, `LoopNode`, etc.

**Ví dụ thực tế:**
- `ListOutNode.OutputMappings` - List<OutputMapping> được serialize/deserialize
- `InputNode.ArrayValues` - List<string> được serialize/deserialize
- `LoopNode.LoopType`, `LoopNode.RepeatCount`, etc. - Các properties được serialize/deserialize

---

### Step 8: Copy/Paste Support (REQUIRED for all node types)

#### 8.1: Add Keyboard Shortcuts

File: `Services/Interaction/WorkflowEditorEventService.cs`

```csharp
// Handle Ctrl+C (Copy)
if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
{
    if (vm.SelectedNode != null)
    {
        // ⚠️ CRITICAL: Add your node type to the condition
        if (vm.SelectedNode is YourNode || vm.SelectedNode is InputNode || vm.SelectedNode is KeyPressEventNode)
        {
            _copiedNode = vm.SelectedNode;
            e.Handled = true;
            return;
        }
    }
}

// Handle Ctrl+V (Paste)
if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
{
    if (_copiedNode != null)
    {
        // ⚠️ CRITICAL: Add your node type to the condition
        if (_copiedNode is YourNode || _copiedNode is InputNode || _copiedNode is KeyPressEventNode)
        {
            var mousePos = Mouse.GetPosition(Host.WorkflowCanvas);
            Host.DuplicateNodeAtPosition(_copiedNode, mousePos.X, mousePos.Y);
            e.Handled = true;
            return;
        }
    }
}
```

#### 8.2: Copy ALL Properties in CreateDuplicateNodeInstance()

File: `Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs`

```csharp
// In CreateDuplicateNodeInstance() - MUST COPY ALL PROPERTIES INCLUDING TitleDisplayMode
if (source is YourNode srcNode && node is YourNode dstNode)
{
    // Copy ALL properties
    dstNode.SomeProperty = srcNode.SomeProperty;
    dstNode.TitleDisplayMode = srcNode.TitleDisplayMode; // ⚠️ CRITICAL: Don't forget this!
    // Copy ALL other properties...
    
    // If node has list/array properties, clone them to avoid reference sharing
    // if (srcNode.ArrayValues != null && srcNode.ArrayValues.Count > 0)
    // {
    //     dstNode.ArrayValues = new List<string>(srcNode.ArrayValues);
    // }
    // else
    // {
    //     dstNode.ArrayValues = new List<string>();
    // }
}

// After setting Title, trigger PropertyChanged for nodes with INotifyPropertyChanged:
var baseTitle = source.Title ?? string.Empty;
var newTitle = GenerateUniqueTitle(baseTitle);
node.Title = newTitle;

// ⚠️ CRITICAL: Trigger PropertyChanged cho YourNode để cập nhật UI
if (node is YourNode yourNode)
{
    yourNode.NotifyTitleChanged();
}
```

**⚠️ CRITICAL POINTS:**
1. **Ctrl+C/Ctrl+V**: Phải add node type vào cả 2 conditions
2. **Copy properties**: Phải copy TẤT CẢ properties bao gồm TitleDisplayMode
3. **List/Array properties**: Phải clone list để tránh reference sharing
4. **NotifyTitleChanged()**: Phải gọi sau khi set Title nếu node có INotifyPropertyChanged

#### 8.3: Multi-node Clipboard - Remap Node References (BẮT BUỘC)

File: `Views/WorkflowEditors/WorkflowEditorWindow.MultiNodeClipboard.cs`

Khi paste từ clipboard nhiều node, mọi field tham chiếu node bằng Id phải đổi từ Id cũ sang Id mới.
Nếu bỏ qua bước này, UI vẫn hiện được node nhưng combobox `Node/Key/Value` sẽ chọn sai nguồn.

```csharp
// Sau khi clone node xong và có map oldId -> newNode:
RemapPastedNodeReferences(nodeMap);
```

Checklist bắt buộc:
- Remap toàn bộ field dạng `SourceNodeId` / `TargetNodeId` trong model node.
- Bao phủ cả cấu trúc lồng nhau: `DynamicInputs`, `ReuseRoutes`, `ConditionalBranches`, `SubConditions`.
- Bao phủ node đặc thù có mapping list: `InputMappings`, `Assignments`, `RequestInterceptRules`, `Headers`, `QueryParams`, `FormData`, ...
- Chỉ remap Id nằm trong tập node đã copy; Id ngoài selection giữ nguyên để không phá link ngoài phạm vi.
- Chạy remap trước bước refresh layout/dialog để binding combobox lấy đúng node mới.

---

## Common Errors & Solutions

### Error 1: "A local variable named 'npc' is already defined"

**Cause**: Multiple `if (node is INotifyPropertyChanged npc)` blocks in same scope

**Solution**: Combine all handlers into ONE block:

```csharp
// ✅ CORRECT: Combine all PropertyChanged handlers in ONE block
if (node is INotifyPropertyChanged npc)
{
    npc.PropertyChanged += (s, e) =>
    {
        if (e.PropertyName == nameof(WorkflowNode.Title))
        {
            // Handle Title
        }
        else if (e.PropertyName == nameof(YourNode.TitleDisplayMode))
        {
            // Handle TitleDisplayMode
        }
        else if (e.PropertyName == nameof(YourNode.SomeProperty))
        {
            // Handle other properties
        }
    };
}

// ❌ WRONG: Multiple separate blocks will cause compilation error
if (node is INotifyPropertyChanged npc) { /* handler 1 */ }
if (node is INotifyPropertyChanged npc) { /* handler 2 - ERROR */ }
```

---

### Error 2: Toggle button still shows despite hiding

**Cause**: `HiddenHeaderButtons.Add()` called after `NodeChrome.Apply()`

**Solution**: Add to HiddenHeaderButtons BEFORE NodeChrome.Apply():

```csharp
// ✅ CORRECT
yourNode.HiddenHeaderButtons.Add("duplicate");
yourNode.HiddenHeaderButtons.Add("editTitle");
yourNode.HiddenHeaderButtons.Add("dataToggle");
NodeChrome.Apply(yourNode.Border, yourNode, Host);

// ❌ WRONG
NodeChrome.Apply(yourNode.Border, yourNode, Host);
yourNode.HiddenHeaderButtons.Add("duplicate"); // Too late!
```

---

### Error 3: OutputKeyCombo doesn't refresh when source changes

**Cause**: ItemsSource assigned directly instead of using binding

**Solution**: Use `SetBinding()` instead of direct assignment:

```csharp
// ✅ CORRECT
outputKeyCombo.SetBinding(ComboBox.ItemsSourceProperty,
    new Binding(nameof(InputItemViewModel.AvailableOutputKeyOptions)) { Source = inputVm });

// ❌ WRONG
outputKeyCombo.ItemsSource = inputVm.AvailableOutputKeyOptions;
```

---

### Error 4: Copy/Paste xong NodeSearchComboBox bị lệch source

**Cause**: Node được clone nhưng các field `SourceNodeId`/`TargetNodeId` vẫn giữ Id cũ.

**Solution**:
- Sau khi tạo `nodeMap` (oldId -> new node), gọi `RemapPastedNodeReferences(nodeMap)`.
- Đảm bảo remap cả nested mappings (đặc biệt `ConditionalBranches` và `DynamicInputs`).

Quick verify:
1. Copy cụm node có ít nhất 2 combobox source mapping.
2. Paste sang vùng mới.
3. Mở dialog node đích, combobox Node phải trỏ tới node bản copy (`- copy n`), không trỏ node cũ.

#### 8.4: Template remap cho node mới (copy dùng ngay)

Dùng template này mỗi khi thêm node type mới có combobox chọn node/key/value:

```csharp
// 1) Thêm case trong RemapNodeReferenceIds(...)
case YourNode yourNode:
    // Single reference fields
    yourNode.SourceNodeId = RemapNodeId(yourNode.SourceNodeId, sourceToNewNodeMap);
    yourNode.TargetNodeId = RemapNodeId(yourNode.TargetNodeId, sourceToNewNodeMap);

    // List mappings
    foreach (var m in yourNode.InputMappings ?? new List<YourInputMapping>())
        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);

    // Nested object/list
    if (yourNode.Routes != null)
    {
        foreach (var route in yourNode.Routes)
        {
            route.FromNodeId = RemapNodeId(route.FromNodeId, sourceToNewNodeMap);
            route.ToNodeId = RemapNodeId(route.ToNodeId, sourceToNewNodeMap);
        }
    }
    break;
```

Checklist trước khi merge:
- Đã remap mọi field hậu tố `NodeId` có semantics tham chiếu node.
- Đã remap nested list/object (không chỉ field top-level).
- Đã test copy/paste trong cùng canvas và workflow mới.
- Đã mở dialog để confirm combobox Node/Key/Value bám node bản copy.

#### 8.5: Naming convention cho field tham chiếu node

Để tránh quên remap khi mở rộng node mới, chuẩn hóa naming như sau:
- Field giữ id node phải kết thúc bằng `NodeId` (`SourceNodeId`, `TargetNodeId`, `IncomingNodeId`, ...).
- Field trong object lồng nhau/list mapping cũng tuân theo `*NodeId`.
- Không đặt tên mơ hồ cho node-id string (`Source`, `NodeRef`, `From`) vì khó grep khi review.
- Quy trình review nhanh: sau khi thêm model mới, grep `NodeId` trong class -> kiểm tra tất cả field đó đã được remap trong `RemapNodeReferenceIds(...)`.

#### 8.6: Quick runbook (copy 1 thể trước khi merge)

Làm theo thứ tự này để đỡ phải tự check lại nhiều lần:

1) `Model + Save/Load`
- Thêm field `*NodeId` đúng naming.
- Đảm bảo serialize/deserialize đủ (không mất field khi mở lại workflow).

2) `Duplicate + Clipboard paste`
- Copy ALL properties trong `CreateDuplicateNodeInstance()`.
- Sau khi tạo `nodeMap`, gọi `RemapPastedNodeReferences(nodeMap)`.
- Bổ sung case remap cho node mới trong `RemapNodeReferenceIds(...)` (bao gồm nested list/object).

3) `Dialog/UI validation`
- Mở dialog node sau paste, check combobox Node/Key/Value trỏ đúng node bản copy.
- Với node đặc thù (loop body/diamond), kiểm tra line và vị trí cổng sau paste.

4) `Smoke test bắt buộc`
- Copy/paste cùng canvas.
- Copy/paste sang workflow mới.
- Copy cụm node có nested mappings (để bắt lỗi khó).

5) `Build`
- Chạy `dotnet build /p:UseAppHost=false` khi exe bị lock.

Pass tất cả bước trên thì mới merge.

---

### Error 4: Node jumps to mouse position when opening dialog

**Cause**: Node still has mouse capture or is selected when dialog opens

**Solution**: Release mouse capture, clear DraggedNode, and deselect node in `OpenNodeDialog()`:

```csharp
private static void OpenNodeDialog(YourNode node, IWorkflowEditorHost host, Window? ownerWindow)
{
    try
    {
        // ⚠️ CRITICAL: Release mouse capture và clear drag state
        if (node.Border != null && node.Border.IsMouseCaptured)
        {
            node.Border.ReleaseMouseCapture();
        }
        
        // ⚠️ CRITICAL: Clear DraggedNode để dialog có thể đóng ngay
        host.DraggedNode = null;
        
        // ⚠️ CRITICAL: Deselect node để tránh node nhảy đến vị trí chuột
        if (host.ViewModel != null)
        {
            host.ViewModel.SelectedNode = null;
        }

        // ... rest of dialog opening code
    }
}
```

---

### Error 5: Must click canvas twice to close dialog

**Cause**: `DraggedNode` is not cleared when opening dialog

**Solution**: Clear `DraggedNode` in `OpenNodeDialog()` (see Error 4 solution above)

---

### Error 6: Title doesn't move with node

**Cause**: Missing UpdateNodePosition() implementation in Renderer to sync title position

**Solution**: Implement UpdateNodePosition() in YourNodeRenderer (see Step 6.2 above)

**⚠️ CRITICAL**: 
- Phải implement UpdateNodePosition() trong Renderer, không chỉ trong NodeControl
- NodeControl.LayoutUpdated chỉ xử lý throttled updates
- Renderer.UpdateNodePosition() được gọi trực tiếp khi `host.UpdateNodePosition()` được gọi

---

### Error 7: Title doesn't show after zoom ends (TitleDisplayMode.Always)

**Cause**: Missing zoom handling in LayoutUpdated handler

**Solution**: Add zoom handling logic in LayoutUpdated handler (see Step 5 template above)

---

### Error 8: Title still visible when node is outside viewport

**Cause**: Missing viewport culling support in UpdateTitleVisibility

**Solution**: Add viewport culling check in UpdateTitleVisibility (see Step 5 template above)

---

### Error 9: Title doesn't update in UI after editing in dialog

**Cause**: Node implements INotifyPropertyChanged but PropertyChanged is not triggered when Title changes

**Solution**: 

**Part 1: Add NotifyTitleChanged() method to Node Model:**

```csharp
public class YourNode : WorkflowNode, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Method helper để notify PropertyChanged khi Title thay đổi từ bên ngoài
    /// </summary>
    public void NotifyTitleChanged()
    {
        OnPropertyChanged(nameof(Title));
    }
}
```

**Part 2: Call NotifyTitleChanged() in ViewModel SaveTitle():**

```csharp
[RelayCommand]
private void SaveTitle()
{
    if (_node.Title != NodeTitle)
    {
        _node.Title = NodeTitle;
        // ⚠️ CRITICAL: Trigger PropertyChanged để cập nhật UI
        if (_node is YourNode yourNode)
        {
            yourNode.NotifyTitleChanged();
        }
    }
}
```

**Part 3: Call NotifyTitleChanged() in Copy Logic:**

```csharp
// In CreateDuplicateNodeInstance() - after setting Title:
node.Title = newTitle;

// ⚠️ CRITICAL: Trigger PropertyChanged cho YourNode để cập nhật UI
if (node is YourNode yourNode)
{
    yourNode.NotifyTitleChanged();
}
```

**⚠️ IMPORTANT**: Do NOT override Title property with `new`. This breaks copy logic. Use the helper method approach instead.

---

### Error 10: Port colors not applying - still showing default colors

**Cause**: Port color is only set when `port.PortUI == null`, but not updated when port already exists

**Solution**: Always update port color in both `RenderNode()` and `UpdateNodePosition()`:

```csharp
// ⚠️ CRITICAL: ALWAYS update color, even if port already exists
if (port.PortUI is System.Windows.Shapes.Ellipse ellipse)
{
    ellipse.Fill = new SolidColorBrush(portColor);
}
```

See Step 6.1 and 6.2 for complete implementation.

---

### Error 11: Title not removed from canvas when node is deleted

**Cause**: Missing cleanup logic in Renderer.RemoveNode() to remove titleTextBlock from canvas

**Solution**: Add titleTextBlock cleanup in RemoveNode() (see Step 6.3 above)

**⚠️ CRITICAL**: 
- Phải remove titleTextBlock khỏi Canvas và clear reference
- Nếu không cleanup, title sẽ vẫn hiển thị trên canvas sau khi node bị xóa

---

### Error 12: Delete key doesn't work after implementing TitleDisplayMode

**Cause**: LayoutUpdated handler gọi UpdateTitlePosition() quá nhiều lần, ảnh hưởng đến keyboard handling

**Solution**: Add throttling và skip logic trong LayoutUpdated handler:

```csharp
border.LayoutUpdated += (s, e) =>
{
    // Skip updates khi đang pan hoặc drag để tránh giật
    if (host.IsPanning || host.DraggedNode == node)
    {
        return;
    }
    
    // Throttle updates bằng DispatcherTimer
    if (titleTextBlock.Visibility == Visibility.Visible)
    {
        ThrottledUpdateTitlePosition(titleTextBlock, border, host);
    }
};
```

See Step 5 template for complete implementation with throttling.

---

### Error 16: Memory leak khi node bị xóa (TitleDisplayMode)

**Cause**: Không cleanup `titleTextBlock`, `DispatcherTimer`, và các static dictionaries khi node bị unload

**Solution**: Add `border.Unloaded` handler để cleanup:

```csharp
border.Unloaded += (s, e) =>
{
    try
    {
        // Stop & remove throttling timer
        if (_titleUpdateTimers.TryGetValue(border, out var timer))
        {
            timer.Stop();
            _titleUpdateTimers.Remove(border);
        }
        _titleUpdatedAfterZoom.Remove(border);

        // Remove titleTextBlock khỏi canvas (nếu còn)
        if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
        {
            host.WorkflowCanvas.Children.Remove(titleTextBlock);
        }

        // Clear reference để tránh giữ UI element
        if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
        {
            node.TitleTextBlockUI = null;
        }
    }
    catch
    {
        // Best-effort cleanup; ignore to avoid crashing unload path
    }
};
```

**⚠️ CRITICAL**: 
- Phải cleanup static dictionaries (`_titleUpdateTimers`, `_titleUpdatedAfterZoom`)
- Phải remove `titleTextBlock` khỏi canvas
- Phải clear `node.TitleTextBlockUI` reference

---

### Error 17: Properties bị mất khi save/export JSON

**Cause**: Không implement serialize/deserialize logic trong `FileWorkflowPersistenceService`

**Symptom**: 
- Khi save workflow, các custom properties (ví dụ: `ListOutNode.OutputMappings`) không được lưu vào JSON
- Khi load/import workflow, các properties bị mất hoặc về giá trị mặc định
- User cấu hình properties trong dialog nhưng khi save/load lại thì mất hết

**Solution**: 
- ⚠️ **CRITICAL**: Phải implement serialize trong `GetNodeProperties()` và deserialize trong `RestoreNodeProperties()`
- Xem Step 7 (Persistence Support) ở trên để biết cách implement đầy đủ

**Example Error Case:**
- `ListOutNode.OutputMappings` không được serialize → khi save, mappings bị mất
- Khi load lại, `OutputMappings` rỗng dù user đã cấu hình đầy đủ trong dialog

**Correct Implementation:**
- Xem `Services/Workflow/FileWorkflowPersistenceService.cs`:
  - `GetNodeProperties()` - Serialize `ListOutNode.OutputMappings`, `InputNode.ArrayValues`, etc.
  - `RestoreNodeProperties()` - Deserialize và restore các properties khi load

**Checklist:**
- [ ] Đã thêm serialize logic trong `GetNodeProperties()` cho TẤT CẢ custom properties
- [ ] Đã thêm deserialize logic trong `RestoreNodeProperties()` cho TẤT CẢ custom properties
- [ ] Đã test save/load để đảm bảo properties được khôi phục đúng

---

### Feature Recipe: Checkbox bật/tắt “Live sync” để cập nhật luôn ô Result (Execution Results) khi dữ liệu runtime thay đổi

**Mục tiêu**  
Một số node có dữ liệu **runtime cập nhật liên tục** (ví dụ bắt response của WebView2, websocket, file watcher, stream…).  
Mặc định, UI “Result toggle” của node thường chỉ update khi node được **Execute** (Run/Test).  
Recipe này giúp bạn thêm 1 checkbox trong dialog để user chọn:

- `false` (default): chỉ update data panel / values runtime (downstream vẫn resolve được)  
- `true`: mỗi khi có dữ liệu mới, update luôn UI “Execution Results” (toggle result) như khi node vừa chạy xong.

#### 1) Node Model: thêm bool property (default = false)

Trong `Models/Nodes/{YourNode}.cs`:

```csharp
private bool _syncLiveOutputsToResults; // default false

public bool SyncLiveOutputsToResults
{
    get => _syncLiveOutputsToResults;
    set { if (_syncLiveOutputsToResults != value) { _syncLiveOutputsToResults = value; OnPropertyChanged(); } }
}
```

#### 2) Persistence: serialize + deserialize property này

Trong `Services/Workflow/FileWorkflowPersistenceService.cs`:

- **Serialize** (GetNodeProperties / nhánh node type):
```csharp
dict["SyncLiveOutputsToResults"] = yourNode.SyncLiveOutputsToResults;
```

- **Deserialize** (RestoreNodeProperties / nhánh node type):
```csharp
if (properties.TryGetValue("SyncLiveOutputsToResults", out var obj) && obj != null &&
    bool.TryParse(obj.ToString(), out var enabled))
{
    yourNode.SyncLiveOutputsToResults = enabled;
}
```

#### 3) Dialog XAML: thêm CheckBox (TwoWay binding)

Trong `Views/Overlays/{YourNode}Dialog.xaml`:

```xml
<CheckBox Content="Cập nhật luôn ô Result của node khi dữ liệu thay đổi (live)"
          Foreground="White" FontSize="12"
          IsChecked="{Binding SyncLiveOutputsToResults, Mode=TwoWay}"
          Margin="0,0,0,16"/>
```

#### 4) Dialog ViewModel: sync property từ node ↔ vm ↔ node (SaveTitle)

Trong `ViewModels/{YourNode}DialogViewModel.cs`:

```csharp
[ObservableProperty]
private bool _syncLiveOutputsToResults;

// ctor:
SyncLiveOutputsToResults = _yourNode.SyncLiveOutputsToResults;

// OnNodePropertyChanged:
else if (propertyName == nameof(YourNode.SyncLiveOutputsToResults))
    SyncLiveOutputsToResults = _yourNode.SyncLiveOutputsToResults;

// OnSaveTitle():
_yourNode.SyncLiveOutputsToResults = SyncLiveOutputsToResults;
```

#### 5) Runtime hook: khi có dữ liệu mới, nếu checkbox bật thì refresh Execution Results

Tại nơi bạn “bắt” dữ liệu runtime (ví dụ event handler, callback…), sau khi bạn đã cập nhật `DynamicOutputs.UserValueOverride` hoặc nguồn dữ liệu mà `NodeDataPanelService.ResolveDynamicValueByKey` đọc:

```csharp
// Pseudo-code: runtime update handler
yourNode.SomeRuntimeValue = newValue;

// ✅ đảm bảo downstream resolve được:
var dyn = yourNode.DynamicOutputs.FirstOrDefault(o => o.Key == "yourKey");
if (dyn != null) dyn.UserValueOverride = newValue;

// ✅ nếu bật live sync -> update UI Result toggle
if (yourNode.SyncLiveOutputsToResults)
{
    // Cách gợi ý: gọi visualizer.RefreshSavedOutputs(new[]{yourNode})
    // Nơi lấy visualizer tùy kiến trúc:
    // - Nếu bạn đang ở ViewModel/Host thì gọi trực tiếp
    // - Nếu đang ở Control layer có host.ViewModel, có thể dùng reflection (như WebNodeControl) để lấy _executionVisualizer
    visualizer.RefreshSavedOutputs(new[] { yourNode });
}
```

**Lưu ý quan trọng**
- `Execution Results` UI được build bởi `WorkflowExecutionVisualizer.UpdateNodeExecutionResults()` và đọc giá trị qua `NodeDataPanelService.ResolveDynamicValueByKey(node, key)`.  
  Vì vậy, muốn Result toggle hiển thị đúng thì phải đảm bảo `ResolveDynamicValueByKey` trả về value mới (thường là cập nhật `DynamicOutputs.UserValueOverride` hoặc dictionary runtime mà service đọc).
- Nếu dữ liệu update rất nhiều (spam), cân nhắc throttle (vd 200–500ms) để tránh lag UI.

---

### Error 13: TitleDisplayModeOption duplicate definition

**Cause**: Class `TitleDisplayModeOption` được định nghĩa trong nhiều ViewModel files

**Solution**: 
- Kiểm tra xem class này đã được định nghĩa trong ViewModel khác chưa (ví dụ: KeyPressEventNodeDialogViewModel.cs)
- Nếu đã có, KHÔNG định nghĩa lại trong YourNodeDialogViewModel.cs
- Chỉ cần sử dụng class đã có vì nó trong cùng namespace

---

### Error 14: ComboBox source node không hiển thị tiêu đề mới nhất khi mở dialog

**Cause**: `InputItemViewModel` copy `AvailableSources` từ `input.AvailableSources` trong constructor, nhưng `input.AvailableSources` có thể chứa tiêu đề cũ từ cache. Khi node title được sửa đổi, `AvailableSources` không được refresh tự động.

**Symptom**: 
- Khi mở dialog, combobox "Source Node" hiển thị tiêu đề cũ của node
- Chỉ khi kết nối lại hoặc refresh thì mới hiển thị tiêu đề mới

**Solution**: 
- ⚠️ **CRITICAL**: Phải gọi `RefreshAvailableSourcesForInputs()` trong `LoadInputs()` **TRƯỚC KHI** tạo `InputItemViewModel`
- Sau khi refresh `input.AvailableSources`, phải cập nhật `inputVm.AvailableSources` để combobox hiển thị tiêu đề mới nhất

**Correct Implementation:**

```csharp
private void LoadInputs()
{
    
    Inputs.Clear();
    if (_node.DynamicInputs == null || _node.DynamicInputs.Count == 0) return;
    
    // ⚠️ CRITICAL: Refresh AvailableSources với tiêu đề node mới nhất trước khi tạo InputItemViewModel
    RefreshAvailableSourcesForInputs();
    
    foreach (var input in _node.DynamicInputs)
    {
        var inputVm = new InputItemViewModel(_node, input, _host);
        
        // ⚠️ CRITICAL: Cập nhật AvailableSources trong InputItemViewModel sau khi refresh input.AvailableSources
        if (input.AvailableSources != null)
        {
            inputVm.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(input.AvailableSources);
        }
        
        Inputs.Add(inputVm);
    }
}

/// <summary>
/// Refresh AvailableSources cho tất cả inputs với tiêu đề node mới nhất.
/// ⚠️ LƯU Ý QUAN TRỌNG (tránh NullReferenceException):
/// - Base constructor của BaseNodeDialogViewModel sẽ gọi LoadInputs() (và từ đó gọi RefreshAvailableSourcesForInputs())
///   TRƯỚC KHI các field riêng của ViewModel (ví dụ: _stringSplitNode, _loopNode, ...) được gán trong ctor của lớp con.
/// - Vì vậy, bên trong method này CẤM dùng trực tiếp các field như _stringSplitNode, _loopNode... để truy cập DynamicInputs,
///   mà phải luôn dùng _node và cast (if (_node is YourNodeType yourNode) ...) giống template bên dưới.
/// - Nếu dùng field riêng, rất dễ bị NullReferenceException ngay lần đầu dialog khởi tạo.
/// </summary>
private void RefreshAvailableSourcesForInputs()
{
    if (_host.ViewModel == null) return;

    // ⚠️ Luôn cast từ _node thay vì dùng field riêng (ví dụ: _stringSplitNode)
    if (_node is not YourNode yourNode) return;
    if (yourNode.DynamicInputs == null || yourNode.DynamicInputs.Count == 0) return;

    // Tìm tất cả connections đến node này
    var connections = _host.ViewModel.Connections
        .Where(c => c.ToNode == yourNode && c.FromNode != null)
        .ToList();

    if (connections.Count == 0)
    {
        // Không có connections, clear AvailableSources
        foreach (var input in yourNode.DynamicInputs)
        {
            input.AvailableSources = new System.Collections.Generic.List<WorkflowDataSourceOption>();
        }
        return;
    }

    // Tìm các producer nodes (nodes có DynamicOutputs)
    var producerNodes = connections
        .Select(c => c.FromNode)
        .Where(n => n != null && n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
        .Distinct()
        .ToList();

    // Tạo options với tiêu đề mới nhất từ node (n.Title được lấy trực tiếp từ node, không từ cache)
    var options = producerNodes
        .Select(n => new WorkflowDataSourceOption
        {
            NodeId = n.Id,
            Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
        })
        .ToList();

    // Update AvailableSources cho tất cả inputs
    foreach (var input in yourNode.DynamicInputs)
    {
        input.AvailableSources = options;
    }
}
```

**Reference Implementations:**
- `ViewModels/LoopNodeDialogViewModel.cs` - Có implementation đầy đủ với `RefreshAvailableSourcesForInputs()`
- `ViewModels/KeyPressEventNodeDialogViewModel.cs` - Có implementation đầy đủ với `RefreshAvailableSourcesForInputs()`
- `ViewModels/HotkeyPressEventNodeDialogViewModel.cs` - Có implementation đầy đủ với `RefreshAvailableSourcesForInputs()`

---

### Error 15: OutputType bị null dù ConvertType đã set đúng

**Cause**: Chỉ set `ConvertType` cho `DynamicOutputs`, nhưng không set `OutputType`.  
Ví dụ trong `StringSplitNode`:

```csharp
// ❌ SAI: chỉ set ConvertType, OutputType vẫn null
DynamicOutputs.Add(new WorkflowDynamicDataPort
{
    Key = "ListItems",
    DisplayName = "List Items",
    ConvertType = WorkflowDataType.ArrayString,
    // OutputType == null  → UI không nhận đây là ArrayString
});
```

Khi `OutputType` bị null:

- Combobox "Output key" không hiển thị type (ví dụ: `ListItems (ArrayString)`).
- `NodeChrome` và `WorkflowEditorViewModel.UpdateNodeExecutionResults()` không coi đây là array → không dùng toggle "Có X kết quả / items", chỉ render như text thường.

**Solution**: Với mọi output (đặc biệt là Array*), luôn set **cả `ConvertType` lẫn `OutputType`**:

```csharp
// ✅ ĐÚNG: ConvertType và OutputType đồng bộ (ví dụ cho mảng string)
DynamicOutputs.Add(new WorkflowDynamicDataPort
{
    // Key và DisplayName chỉ là ví dụ minh hoạ.
    // Trong node thật, hãy dùng key/display name phù hợp với output của bạn.
    Key = "yourArrayOutputKey",
    DisplayName = "Your Array Output",

    // Mảng string
    ConvertType = WorkflowDataType.ArrayString,
    // Metadata type cho combobox, DataPanel, NodeChrome, v.v.
    OutputType = WorkflowDataType.ArrayString,

    IsUserAdded = false
});
```

**Notes**:

- `ConvertType` quyết định cách hiển thị/convert value trong DataPanel.
- `OutputType` là metadata type cho output, được dùng ở:
  - Combobox chọn output key (`WorkflowOutputKeyOption.Type`).
  - DataPanel array preview trong `NodeChrome`.
  - Logic parse mảng trong `WorkflowEditorViewModel.UpdateNodeExecutionResults()`.

---

### Error 16: ComboBox SelectedValue bị null khi mở dialog (mỗi lần mở lại bị null thêm 1 item)

**Cause**: Gọi `RefreshAvailableNodes()` trong `OnLoaded()` sau khi constructor đã gọi, làm `AvailableNodeOptions.Clear()` được gọi khi ComboBox đã bind với `SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"`. Khi `ItemsSource` bị clear tạm thời, ComboBox không tìm thấy item tương ứng → WPF tự động set `SelectedValue = null`.

**Symptom**: 
- Mỗi lần mở dialog, một `NodeSearchComboBoxUserControl` bị null (item đầu tiên lần đầu, item tiếp theo lần sau, ...)
- Cứ mở dialog nhiều lần thì tất cả `NodeSearchComboBoxUserControl` sẽ bị null hết
- Chỉ xảy ra với ComboBox có binding `SelectedValue` với `Mode=TwoWay`

**Root Cause**:
1. Constructor của ViewModel gọi `RefreshAvailableNodes()` và load mappings vào `InputMappingsList`
2. Khi dialog load, `OnLoaded()` được gọi và gọi lại `RefreshAvailableNodes()`
3. `RefreshAvailableNodes()` gọi `AvailableNodeOptions.Clear()` → ComboBox mất `ItemsSource` tạm thời
4. Trong lúc đó, ComboBox đã bind với `SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"` và không tìm thấy item tương ứng
5. WPF với TwoWay binding tự động set `SourceNodeId = null` khi không tìm thấy item trong `ItemsSource`

**Solution**: 

#### 1. ⚠️ **CRITICAL**: Không gọi `RefreshAvailableNodes()` trong `OnLoaded()` nếu đã gọi trong constructor

```csharp
// ❌ SAI: Gọi RefreshAvailableNodes() trong OnLoaded()
protected override void OnLoaded()
{
    base.OnLoaded();
    if (_viewModel is WebNodeDialogViewModel vm)
    {
        vm.RefreshAvailableNodes(); // ❌ KHÔNG NÊN GỌI Ở ĐÂY
        foreach (var item in vm.InputMappingsList)
        {
            vm.RefreshOutputKeyOptionsFor(item);
        }
    }
}

// ✅ ĐÚNG: Chỉ refresh output key options, KHÔNG refresh available nodes
protected override void OnLoaded()
{
    base.OnLoaded();
    if (_viewModel is WebNodeDialogViewModel vm)
    {
        // ⚠️ CRITICAL: KHÔNG gọi RefreshAvailableNodes() ở đây vì đã gọi trong constructor.
        //   Việc gọi lại sẽ clear AvailableNodeOptions và làm ComboBox mất ItemsSource tạm thời,
        //   khiến TwoWay binding tự động set SourceNodeId = null.
        //   Chỉ refresh output key options để đảm bảo combobox Key có đúng options.
        foreach (var item in vm.InputMappingsList)
        {
            vm.RefreshOutputKeyOptionsFor(item);
        }
    }
}
```

#### 2. ⚠️ **CRITICAL**: Đảm bảo `RefreshAvailableNodes()` bao gồm cả các node đã được chọn trong InputMappings

Khi implement `RefreshAvailableNodes()`, phải đảm bảo các node đã được chọn trong `InputMappings` cũng được thêm vào `AvailableNodeOptions`, ngay cả khi không có connection:

```csharp
public void RefreshAvailableNodes()
{
    var vm = _host.ViewModel;
    if (vm?.Nodes == null || vm.Connections == null) return;

    // Build danh sách mới trước (không clear collection cũ ngay)
    var newOptions = new List<WorkflowDataSourceOption>();

    // 1. Thêm các node có connection vào WebNode
    var inputPort = _webNode.Ports?.FirstOrDefault(p => p.IsInput);
    var connectedNodeIds = vm.Connections
        .Where(c => c.ToNode == _webNode && c.FromNode != null &&
                    (inputPort == null || c.ToPort == inputPort || (c.ToPort != null && c.ToPort.IsInput)))
        .Select(c => c.FromNode!.Id)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var n in vm.Nodes)
    {
        if (ReferenceEquals(n, _webNode)) continue;
        if (!connectedNodeIds.Contains(n.Id)) continue;

        if (n is not InputNode && (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0))
            continue;

        newOptions.Add(new WorkflowDataSourceOption
        {
            NodeId = n.Id,
            Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
        });
    }

    // 2. ⚠️ CRITICAL: Đảm bảo các node đã được chọn trong InputMappings cũng có trong danh sách
    //    (ngay cả khi không có connection) để tránh ComboBox set SourceNodeId = null
    var mappedNodeIds = (_webNode.InputMappings ?? new List<WebInputMapping>())
        .Where(m => !string.IsNullOrWhiteSpace(m.SourceNodeId))
        .Select(m => m.SourceNodeId!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    foreach (var nodeId in mappedNodeIds)
    {
        // Nếu đã có trong newOptions thì bỏ qua
        if (newOptions.Any(o =>
                string.Equals(o.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
            continue;

        // Tìm node tương ứng trong workflow
        var node = vm.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));

        if (node == null)
            continue; // node thực sự không còn tồn tại

        // Thêm node vào danh sách (ngay cả khi không có connection)
        newOptions.Add(new WorkflowDataSourceOption
        {
            NodeId = node.Id,
            Title = string.IsNullOrWhiteSpace(node.Title) ? node.Id : node.Title
        });
    }

    // 3. ⚠️ CRITICAL: Replace collection một lần để tránh ComboBox mất ItemsSource trong lúc Clear()
    //    Sử dụng Clear() + Add() thay vì tạo collection mới để giữ reference
    AvailableNodeOptions.Clear();
    foreach (var option in newOptions)
    {
        AvailableNodeOptions.Add(option);
    }
}
```

#### 3. Best Practices để tránh lỗi tương tự:

- ✅ **Chỉ gọi `RefreshAvailableNodes()` trong constructor** của ViewModel, không gọi trong `OnLoaded()`
- ✅ **Đảm bảo `RefreshAvailableNodes()` bao gồm cả các node đã được chọn** trong InputMappings/DataMappings
- ✅ **Build danh sách mới trước**, rồi replace collection một lần để giảm thời gian mất `ItemsSource`
- ✅ **Nếu cần refresh khi workflow được load sau khi dialog đã mở**, dùng `RefreshUI()` method riêng với Dispatcher.BeginInvoke

**Reference Implementations**:
- `ViewModels/WebNodeDialogViewModel.cs` - Có implementation đầy đủ với `RefreshAvailableNodes()` đúng cách
- `Views/Overlays/WebNodeDialog.xaml.cs` - `OnLoaded()` không gọi `RefreshAvailableNodes()`

**When to Apply**: 
- Khi tạo node mới có dialog với ComboBox chọn source node (Node + Key pattern)
- Khi sửa dialog có ComboBox với `SelectedValue` binding `Mode=TwoWay`
- Khi có pattern tương tự: `NodeSearchComboBoxUserControl` + `ComboBox Key` + `TextBox Value`

### Multi-row `ItemsControl`: nhiều NodeSearchComboBox + Key (không bị đồng bộ / mất selection)

Áp dụng khi dialog có **nhiều dòng** (mỗi dòng: ComboBox chọn node nguồn + ComboBox chọn output key), ví dụ `CodeNodeDialog`, `HtmlUiNodeDialog`, `FlowOverwriteNodeDialog`.

**Triệu chứng thường gặp**

- Mở lại dialog hoặc thêm/xóa connection → **tất cả các dòng** nhảy cùng một node/key.
- Mỗi lần mở dialog, `SelectedValue` bị `null` hoặc lệch dần (liên quan Error 16).
- Hai dòng khác nhau nhưng chọn xong lại **dính** cùng một lựa chọn.

**Nguyên nhân & cách xử lý (tóm tắt)**

1. **Không refresh full danh sách node trong `Loaded` sau khi ViewModel ctor đã load mapping**  
   Cùng nguyên lý Error 16: khi `ItemsSource` bị làm rỗng / thay thế không an toàn trong lúc ComboBox đã bind `SelectedValue` TwoWay, WPF có thể ghi đè binding → mất hoặc đồng bộ sai selection.  
   - Constructor: build `AvailableNodeOptions` + load từng dòng mapping.  
   - Code-behind `Loaded`: **không** gọi lại kiểu `RefreshAvailableNodes()` / `Clear()` + bind đang sống; nếu cần cập nhật sau khi graph đổi, dùng method riêng (ví dụ defer `Dispatcher.BeginInvoke`) và chỉ refresh phần an toàn (thường là **key options** theo từng dòng).

2. **Một collection node chung ở parent ViewModel, bind qua `RelativeSource` (giống `CodeNodeDialog.xaml`)**  
   - ComboBox **Node**: `ItemsSource="{Binding DataContext.AvailableNodeOptions, RelativeSource={RelativeSource AncestorType=ItemsControl}}"` (tên property có thể là `AvailableSourceOptions`, v.v.).  
   - Mỗi dòng chỉ giữ `SourceNodeId` / `SelectedSourceNodeId` và list **key riêng** (`AvailableOutputKeyOptions` / `AvailableOutputKeys`) build từ node đã chọn.  
   Tránh mỗi row tự `Clear()`/`Add()` lặp lại cùng một `ObservableCollection` reference nếu pattern đó dễ gây race với binding.

3. **Thay danh sách node một lần — không để `ItemsSource` “trống tạm thời”**  
   - Ưu tiên: gán lại property = `new ObservableCollection<WorkflowDataSourceOption>(builtList)` (một lần), thay vì `Clear()` rồi `Add()` từng phần tử khi ComboBox đang TwoWay bind `SelectedValue`.  
   - Đảm bảo `RefreshAvailableNodes()` **luôn** đưa vào list cả các `NodeId` đã lưu trong mapping (kể cả tạm thời không còn connection), để ComboBox luôn resolve được item — xem mục **#### 2** của Error 16.

4. **Nhiều ComboBox dùng chung `ItemsSource` → tắt đồng bộ CurrentItem**  
   Trên `NodeSearchComboBoxUserControl` (và thường cả Key nếu cùng pattern): `IsSynchronizedWithCurrentItem="False"` để tránh WPF đồng bộ current item làm các dòng “kéo” selection lẫn nhau.

5. **So khớp `NodeId` không phân biệt hoa thường**  
   Khi kiểm tra “node đã chọn còn trong list không”, dùng `StringComparer.OrdinalIgnoreCase` / `string.Equals(..., OrdinalIgnoreCase)` để không fallback nhầm sang item đầu.

6. **ComboBox Key chỉ hiển thị key thật của node đang chọn**  
   Không inject item giả kiểu `key (không còn khả dụng)` trong `ItemsSource` — khi đổi node, nếu key cũ không tồn tại trên node mới thì chọn key hợp lệ đầu tiên (hoặc để trống nếu không có key).

**Tham chiếu code**

- `Views/Overlays/CodeNodeDialog.xaml` + `ViewModels/CodeNodeDialogViewModel.cs` (pattern chuẩn nhiều dòng input).  
- `ViewModels/FlowOverwriteNodeDialogViewModel.cs` + `Views/Overlays/FlowOverwriteNodeDialog.xaml` (multi-row Node + Key, áp dụng các điểm trên).

---

## Implementation Checklist Summary

### ✅ Phase 1: Basic Dialog (Required)

- [ ] Create 4 files: XAML, Code-behind, ViewModel, NodeControl
- [ ] Node Model: Add NotifyTitleChanged() if INotifyPropertyChanged
- [ ] Renderer: Hide header buttons BEFORE NodeChrome.Apply()
- [ ] Renderer: Always update port colors in RenderNode() và UpdateNodePosition()
- [ ] NodeControl: Release mouse capture, clear DraggedNode, deselect node in OpenNodeDialog()
- [ ] Dialog: Call SaveTitleCommand in Closing và CloseButton_Click
- [ ] Persistence: Add serialize logic trong GetNodeProperties() (FileWorkflowPersistenceService.cs)
  - [ ] Persistence: Add deserialize logic trong RestoreNodeProperties() (FileWorkflowPersistenceService.cs)
  - [ ] ⚠️ CRITICAL: Serialize TẤT CẢ custom properties (bao gồm List/Array properties)
  - [ ] ⚠️ CRITICAL: Deserialize và restore TẤT CẢ properties khi load/import
- [ ] Copy/Paste: Add Ctrl+C/Ctrl+V in WorkflowEditorEventService.cs
  - [ ] Copy/Paste: Copy ALL properties in CreateDuplicateNodeInstance()
  - [ ] Copy/Paste: Call NotifyTitleChanged() after setting Title
- [ ] ⚠️ **CRITICAL**: ViewModel: Add `RefreshAvailableSourcesForInputs()` method và gọi nó trong `LoadInputs()` để đảm bảo combobox source node hiển thị tiêu đề mới nhất khi mở dialog
- [ ] ⚠️ **CRITICAL**: Nếu node có ComboBox chọn source node (Node + Key pattern): 
  - [ ] ViewModel: Implement `RefreshAvailableNodes()` method và gọi nó **CHỈ trong constructor**, KHÔNG gọi trong `OnLoaded()`
  - [ ] ViewModel: `RefreshAvailableNodes()` phải bao gồm cả các node đã được chọn trong InputMappings/DataMappings (ngay cả khi không có connection)
  - [ ] Dialog: `OnLoaded()` KHÔNG được gọi `RefreshAvailableNodes()` (xem Error 16 để biết chi tiết)
  - [ ] Xem Error 16: ComboBox SelectedValue bị null khi mở dialog để biết chi tiết và best practices
- [ ] Nếu có **nhiều dòng** Node + Key trong `ItemsControl`: làm theo mục **Multi-row `ItemsControl`** (ngay sau Error 16): collection node ở parent, thay list một lần, `IsSynchronizedWithCurrentItem=False`, không refresh full trong `Loaded`.
- [ ] ⚠️ **Execution (nếu node có “work” thực thi)**:
  - [ ] Tạo executor: `Services/Workflow/NodeExecutors/{NodeName}Executor.cs` implement `INodeExecutor`
  - [ ] Trong `ExecuteAsync`, dùng `NodeExecutionEnvironment` để truy cập:
    - `env.Service` (WorkflowExecutionService + helpers)
    - `env.Connections`, `env.CancellationToken`
    - callbacks `env.OnEnteringNode`, `env.OnNodeStarted`, `env.OnNodeCompleted`, `env.OnNodeFailed`
    - điều hướng sang node tiếp theo qua `await env.ExecuteNextAsync(nextNode, viaConnection)`
  - [ ] **Toggle lỗi (hiển thị lỗi trên node, dừng execution)**:
    - Khi node thực thi bị exception: gọi `env.OnNodeFailed?.Invoke(node, ex.Message)` rồi `throw` (không nuốt exception).
    - UI sẽ hiển thị trên node: status "❌ Lỗi", toggle "▸ Có lỗi" với nội dung lỗi (Copy / Xem thêm). Execution dừng tại node đó, các node tiếp theo không chạy.
    - Trong executor: bọc logic có thể lỗi trong `try/catch (Exception ex)`; trong `catch`: ghi log (tuỳ chọn), set output fallback (tuỳ chọn), `env.OnNodeFailed?.Invoke(node, ex.Message); throw;`
    - Ví dụ: `CodeNodeExecutor`, `HttpRequestNodeExecutor`, `FolderNodeExecutor`, `StringSplitNodeExecutor`, v.v. (xem `Services/Workflow/NodeExecutors/`).
  - [ ] Đăng ký executor trong ctor `WorkflowExecutionService` (thêm vào `_nodeExecutors`, trước `DefaultNodeExecutor`)
  - [ ] **Không** thêm `if (node is ...)` mới vào `ExecuteNodeAsync` – method này chỉ điều phối sang executors
  - [ ] **Loop/LoopBody special (Return port + hard stop)**:
    - [ ] Nếu workflow dùng `LoopBodyNode` (node ảo) và yêu cầu “kết thúc body” theo kiểu Start → … → End, hãy dùng pattern **return-to-right**:
      - [ ] 1) **Trong mọi executor**: thay `if (IsLoopBodyReturnConnection(conn)) continue;` thành:
        - [ ] `env.Service.SignalLoopBodyReturn(conn);` rồi `continue;` (để báo “đã return” cho Loop executor + không traverse vào `LoopBodyNode`)
      - [ ] 2) **Trong `LoopNodeExecutor`**: bắt buộc phải nhận được return về `LoopBodyRight` mới coi iteration hoàn tất; nếu không thì `throw` lỗi rõ ràng.
      - [ ] 3) **Hard stop**: khi chạm `LoopBodyRight`, cancel ngay CTS của iteration để dừng các nhánh khác trong body.
  - [ ] **DynamicInputs + SelectedSourceNodeId (đặc biệt trong LoopBody)**:
    - [ ] Khi resolve giá trị từ `DynamicInputs` (ví dụ: inputString của `StringSplitNode`), **KHÔNG** giả định luôn tồn tại direct connection `SelectedSourceNodeId -> currentNode`.
    - [ ] Pattern khuyến nghị:
      - [ ] 1) Thử tìm direct connection: `connections.FirstOrDefault(c => c.ToNode == currentNode && c.FromNode.Id == SelectedSourceNodeId)`.
      - [ ] 2) Nếu không tìm được (thường xảy ra trong LoopBody vì flow đi qua `LoopBodyLeft` và các node trung gian), fallback: tìm node có `Id == SelectedSourceNodeId` trong toàn bộ graph và đọc value trực tiếp từ node đó (giống `StringSplitNodeExecutor`). 


---

## Execution Pattern: LoopBody “return-to-right” + hard stop (Advanced)

### Mục tiêu
- Loop chạy vào body, body chạy từ `LoopBodyLeft` qua các node trung gian, và **chỉ khi có đường về `LoopBodyRight`** thì mới coi iteration “xong”.
- Khi “chạm `LoopBodyRight`” thì **dừng ngay** các nhánh/đường khác trong body (hard stop), giống semantics Start → … → End.

### 1) Signal thay vì bỏ qua im lặng
Trong các executor (ví dụ `DefaultNodeExecutor`, `DelayNodeExecutor`, …) nếu gặp connection return-to-right thì **signal**:

```csharp
if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
{
    env.Service.SignalLoopBodyReturn(conn);
    continue;
}
```

### 2) Loop executor: bắt buộc return + token riêng cho mỗi iteration
Trong `LoopNodeExecutor`, tạo token riêng cho từng iteration (linked với token tổng), rồi chạy body với token đó.
Khi return được signal, `SignalLoopBodyReturn` sẽ cancel iteration token ⇒ hard stop.

```csharp
using var iterationCts = CancellationTokenSource.CreateLinkedTokenSource(env.CancellationToken);
var (returnTask, cleanup) = env.Service.BeginAwaitLoopBodyReturn(loopBodyNode, env.CancellationToken, hardStopCts: iterationCts);
try
{
    await env.Service.ExecuteNodeAsync(loopBodyNode, connections, iterationCts.Token, ...);
}
catch (OperationCanceledException)
{
    if (env.CancellationToken.IsCancellationRequested) throw; // user cancelled
    if (!returnTask.IsCompletedSuccessfully) throw;           // cancelled but not due to return
}
finally { cleanup.Dispose(); }

if (!returnTask.IsCompletedSuccessfully)
    throw new InvalidOperationException("LoopBody chưa có đường return về 'LoopBodyRight'.");
```

### 3) WorkflowExecutionService: signal + cancel CTS (hard stop)
`SignalLoopBodyReturn(conn)` nên:
- `TrySetResult(conn)` để “đánh dấu đã return”
- `Cancel()` iteration CTS (nếu được truyền vào waiter) để dừng các nhánh còn lại trong body.

### 4) Dialog nguồn dữ liệu trong LoopBody (ví dụ StringSplit)
Trong dialog của các node nằm trong LoopBody (ví dụ: `StringSplitNode`):
- Khi refresh danh sách "Source Node" cho DynamicInputs, nên:
  - Duyệt **toàn bộ upstream graph** từ node hiện tại để tìm các producer nodes có `DynamicOutputs` (A, E, G, …).
  - Trong khi duyệt, nếu phát hiện có connection đi **từ `LoopBodyLeft` của một `LoopBodyNode`** vào body:
    - Lưu lại `LoopBodyNode` đó và `ParentLoopNode` (Loop cha).
  - Sau khi gom `producerNodes`, nếu có `ParentLoopNode` và nó có `DynamicOutputs`:
    - Thêm `ParentLoopNode` vào `producerNodes`.
  - Kết quả: combobox Source Node bên trong LoopBody sẽ hiển thị:
    - Tất cả producers upstream nằm trong body (Delay, StringSplit khác, v.v.).
    - **LoopNode cha** (nếu luồng thực sự bắt nguồn từ `LoopBodyLeft`), để người dùng chọn key như `item`, `index`, ...


### 5) StorageNode: ưu tiên thực thi & auto-mirror outputs (kể cả trong LoopBody)

> Áp dụng cho các node kiểu “Storage” / “Global store” – node dùng để cache output của node khác và cho phép các node khác đọc lại sau này (kể cả sau Save/Load).

**Mục tiêu**:

- Khi một node `X` chạy xong và có nhiều connection out (ví dụ `X → Storage` + `X → Loop`), **luôn chạy mọi `StorageNode` nối trực tiếp từ `X` trước** các node khác, để Storage kịp lấy dữ liệu mới nhất.
- Hành vi này phải đúng:
  - **Ngoài Loop** (ví dụ: `ScreenCapture → Storage + Loop`).
  - **Bên trong LoopBody** (ví dụ: `Output → Storage + Delay → LoopBodyRight`), kể cả khi LoopBody dùng cơ chế `return-to-right` và `reachableToEnd` không chứa đầy đủ nodes.

#### 5.1. Ưu tiên StorageNode trong traversal (TraverseOutputsAsync)

- Tất cả executor chuẩn nên dùng `env.TraverseOutputsAsync(node)` để đi node tiếp theo.
- Bên trong `NodeExecutionEnvironment.TraverseOutputsAsync`:

```csharp
// 1) Lấy tất cả connections từ outputPort
var baseNextConnections = Service.GetConnectionsFromPort(outputPort, node, connections)
                                 .ToList();

// 2) Nếu có ReuseRoute match thì áp dụng như bình thường (không đổi).

// 3) Không có ReuseRoute match → ưu tiên mọi StorageNode trước:
var storageFirst = baseNextConnections
    .Where(c => c.ToNode is StorageNode)
    .ToList();
var nonStorage = baseNextConnections
    .Where(c => c.ToNode is not StorageNode)
    .ToList();

var orderedConnections = storageFirst.Concat(nonStorage).ToList();

foreach (var conn in orderedConnections)
{
    if (conn.ToNode != null)
    {
        if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
        {
            Service.SignalLoopBodyReturn(conn);
            continue;
        }
        await ExecuteNextAsync(conn.ToNode, conn);
    }
}
```

- Với **legacy connections** (không có `FromPort`) cũng áp dụng cùng pattern:

```csharp
var legacyNext = connections
    .Where(c => c.FromNode == node && c.FromPort == null)
    .ToList();

var legacyStorageFirst = legacyNext
    .Where(c => c.ToNode is StorageNode)
    .ToList();
var legacyNonStorage = legacyNext
    .Where(c => c.ToNode is not StorageNode)
    .ToList();
var orderedLegacy = legacyStorageFirst.Concat(legacyNonStorage).ToList();

foreach (var conn in orderedLegacy)
{
    if (conn.ToNode != null)
    {
        if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
        {
            Service.SignalLoopBodyReturn(conn);
            continue;
        }
        await ExecuteNextAsync(conn.ToNode, conn);
    }
}
```

> Kết quả: với cùng một node nguồn `X`, mọi nhánh đi tới `StorageNode` sẽ luôn được execute trước, sau đó mới là các node khác (`Loop`, `Output`, `Delay`, ...). Điều này đảm bảo Storage luôn chứa snapshot mới nhất của node nguồn.

#### 5.2. Auto-mirror outputs sang StorageNode (kể cả trong LoopBody)

- Sau khi **bất kỳ node nào** chạy xong (trong `WorkflowExecutionService.ExecuteNodeAsync`), nên gọi helper:

```csharp
await executor.ExecuteAsync(node, env);

// Sau khi node chạy xong, tự động mirror outputs sang các StorageNode trỏ tới node này
MirrorOutputsToStorageNodes(node, connectionList, reachableToEnd);
```

- Implementation khuyến nghị cho `MirrorOutputsToStorageNodes`:

```csharp
/// <summary>
/// Tự động đồng bộ outputs của node nguồn sang tất cả StorageNode có SourceNodeId trỏ tới node đó.
/// </summary>
private void MirrorOutputsToStorageNodes(
    WorkflowNode sourceNode,
    List<WorkflowConnection> connections,
    HashSet<WorkflowNode> reachableToEnd)
{
    // Không phụ thuộc vào reachableToEnd (đặc biệt trong LoopBody, reachableToEnd có thể rỗng/noPrune).
    // Thay vào đó, build tập node từ toàn bộ connections hiện tại.
    var allNodes = new HashSet<WorkflowNode>();
    foreach (var c in connections)
    {
        if (c.FromNode != null) allNodes.Add(c.FromNode);
        if (c.ToNode != null) allNodes.Add(c.ToNode);
    }

    var storageNodes = allNodes
        .OfType<StorageNode>()
        .Where(sn =>
            !string.IsNullOrWhiteSpace(sn.SourceNodeId) &&
            string.Equals(sn.SourceNodeId, sourceNode.Id, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (storageNodes.Count == 0) return;

    foreach (var storageNode in storageNodes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(storageNode.SourceOutputKey))
            {
                // Copy tất cả outputs
                storageNode.StoredOutputs.Clear();
                storageNode.DynamicOutputs.Clear();

                if (sourceNode.DynamicOutputs != null)
                {
                    foreach (var o in sourceNode.DynamicOutputs)
                    {
                        if (string.IsNullOrWhiteSpace(o.Key)) continue;
                        var key = o.Key.Trim();
                        var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key);
                        if (string.Equals(value?.Trim(), "—", StringComparison.Ordinal))
                            value = string.Empty;

                        storageNode.StoredOutputs[key] = value;
                        storageNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                        {
                            Key = key,
                            DisplayName = o.DisplayName ?? key,
                            IsMultiple = false,
                            OutputType = o.OutputType ?? o.ConvertType,
                            UserValueOverride = value
                        });
                    }
                }
            }
            else
            {
                // Chỉ copy một key duy nhất
                var key = storageNode.SourceOutputKey.Trim();
                var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key);
                if (string.Equals(value?.Trim(), "—", StringComparison.Ordinal))
                    value = string.Empty;

                storageNode.StoredOutputs.Clear();
                storageNode.DynamicOutputs.Clear();

                storageNode.StoredOutputs[key] = value;
                storageNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = key,
                    DisplayName = key,
                    IsMultiple = false,
                    OutputType = WorkflowDataType.String,
                    UserValueOverride = value
                });
            }
        }
        catch
        {
            // best-effort, không để lỗi mirror làm hỏng workflow
        }
    }
}
```

**Lưu ý quan trọng**:

- Không dùng `reachableToEnd` để filter StorageNode, vì trong LoopBody `reachableToEnd` có thể rỗng hoặc không bao gồm toàn bộ nodes của body ⇒ Storage trong body sẽ không được mirror.
- Pattern trên hoạt động tốt cho:
  - Storage nằm **ngoài loop** (kết nối trực tiếp từ bất kỳ node nào).
  - Storage nằm **bên trong loop body**, tham chiếu tới:
    - Node trong body.
    - Hoặc node ở ngoài body (ví dụ Storage ngoài, Input, v.v.).
- Đảm bảo `StorageNode` có:
  - `SourceNodeId` (node nguồn được chọn từ combobox).
  - `SourceOutputKey` trống = copy toàn bộ outputs; không trống = copy đúng một key.


### ✅ Phase 2: TitleDisplayMode Support (Optional)

- [ ] Node Model: Add TitleDisplayMode property và TitleTextBlockUI property
- [ ] ViewModel: Add TitleDisplayMode ObservableProperty và TitleDisplayModeOptions
- [ ] ViewModel: Call NotifyTitleChanged() in SaveTitle()
- [ ] Dialog XAML: Uncomment TitleDisplayMode ComboBox section
- [ ] NodeControl: Add static dictionaries (_titleUpdateTimers, _titleUpdatedAfterZoom)
- [ ] NodeControl: Create titleTextBlock với Visibility dựa trên TitleDisplayMode
- [ ] NodeControl: Add titleTextBlock vào WorkflowCanvas
- [ ] NodeControl: Add hover handling (MouseEnter/MouseLeave)
- [ ] NodeControl: Add PropertyChanged handler để sync Title và TitleDisplayMode
- [ ] NodeControl: Add DependencyPropertyDescriptor để sync với border visibility
- [ ] NodeControl: Add LayoutUpdated handler với zoom handling và throttling
- [ ] NodeControl: Add helper methods (GetTitleVisibility, UpdateTitleVisibility, etc.)
- [ ] ⚠️ CRITICAL: NodeControl: Add Unloaded handler để cleanup (tránh memory leak)
- [ ] Renderer: Implement UpdateNodePosition() để sync title position
- [ ] Renderer: Implement RemoveNode() để cleanup titleTextBlock

---

## Reference Implementations

Study these existing implementations for complete examples:

**Complete TitleDisplayMode Implementation:**
- `Views/NodeControls/InputNodeControl.cs` - Complete với viewport culling và zoom handling
- `Views/NodeControls/KeyPressEventNodeControl.cs` - Complete với viewport culling và zoom handling
- `Views/NodeControls/MouseEventNodeControl.cs` - Complete với viewport culling và zoom handling
- `Views/NodeControls/LoopNodeControl.cs` - Complete với diamond shape và TitleDisplayMode

**Renderer Implementations:**
- `Services/Rendering/InputNodeRenderer.cs` - Complete với UpdateNodePosition() và RemoveNode()
- `Services/Rendering/MouseEventNodeRenderer.cs` - Complete với UpdateNodePosition() và RemoveNode()
- `Services/Rendering/LoopNodeRenderer.cs` - Complete với diamond shape positioning

**Dialog Implementations:**
- `Views/Overlays/InputNodeDialog.xaml` và `.xaml.cs` - Complete với TitleDisplayMode
- `Views/Overlays/KeyPressEventNodeDialog.xaml` và `.xaml.cs` - Complete với TitleDisplayMode
- `Views/Overlays/LoopNodeDialog.xaml` và `.xaml.cs` - Complete với TitleDisplayMode

**ViewModel Implementations:**
- `ViewModels/InputNodeDialogViewModel.cs` - Complete với TitleDisplayMode và NotifyTitleChanged()
- `ViewModels/KeyPressEventNodeDialogViewModel.cs` - Complete với TitleDisplayMode
- `ViewModels/LoopNodeDialogViewModel.cs` - Complete với TitleDisplayMode và NotifyTitleChanged()

**Node Model Implementations:**
- `Models/Nodes/InputNode.cs` - Complete với TitleDisplayMode và NotifyTitleChanged()
- `Models/Nodes/LoopNode.cs` - Complete với TitleDisplayMode và NotifyTitleChanged()

---

## Icon Keys Available

Common icon keys in `IconResources.cs`:
- e.g., "abacus", "acorn", "alt", "repeat", "key", "mouse", etc.

Use with `IconKeyToPathConverter` to get SVG path.

### Palette icon ↔ node type mapping — **Bộ 4 phải khớp nhau**

Khi thêm node mới **hoặc** phát hiện node cũ bị thiếu icon/color (đặc biệt là trong Execution Trace
panel), đảm bảo đủ **4 khai báo** dưới đây. Thiếu bất kỳ dòng nào → node rơi vào fallback
(`circle-nodes`, `AccentBrush`, icon đen trên nền tối — rất khó nhìn).

| # | File | Khai báo |
|---|------|----------|
| (1) | `Views/WorkflowEditorWindow.xaml` palette | `Tag="<NodeType>"` + `Background="{DynamicResource <ColorKey>Brush}"` + `ConverterParameter='<iconKey>'` + `Fill="{DynamicResource TextOn<ColorKey>Brush}"` |
| (2) | `Views/WorkflowEditors/WorkflowEditorWindow.TemplateNodeHandler.cs` | `"<NodeType>" => "<iconKey>"` trong `GetIconNameForNodeType` |
| (3) | `Workflow/TemplateFactory.cs` | `ColorKey = "<ColorKey>"` khi tạo model |
| (4) | `ViewModels/WorkflowEditorViewModel.cs` | `NodeType.<X> => "<iconKey>"` trong `ResolveNodeIconKey` (dùng cho Execution Trace / Run Log) |

Ví dụ với node `StringSplit` (ColorKey `OceanBlue`, iconKey `scissors light`):

```xml
<!-- (1) palette -->
<Border Style="{StaticResource PaletteIconNodeStyle}"
        Background="{DynamicResource OceanBlueBrush}"
        Tag="StringSplit">
    <controls:SvgViewboxEx
        Source="{Binding Source={x:Static sys:String.Empty},
                 Converter={StaticResource IconKeyToPathConverter},
                 ConverterParameter='scissors light'}"
        Fill="{DynamicResource TextOnOceanBlueBrush}"/>
</Border>
```

```csharp
// (2) GetIconNameForNodeType
"StringSplit" => "scissors light",

// (3) TemplateFactory.CreateStringSplit(...)
ColorKey = "OceanBlue",

// (4) WorkflowEditorViewModel.ResolveNodeIconKey(...)
NodeType.StringSplit => "scissors light",
```

**ColorKey → màu chữ/icon tương phản**: luôn lấy từ `DynamicResource TextOn<ColorKey>Brush`.
Brush này đã định nghĩa sẵn cho tất cả ColorKey trong `Themes/Base/Colors/Common.xaml`. Execution
Trace card tự dùng chính `TextOn{ColorKey}Brush` cho icon fill → bạn **không cần** hardcode màu
trong code behind. Nếu thêm ColorKey mới, nhớ thêm cả cặp brush này.

> Xem thêm checklist đầy đủ ở `docs/NODE_CREATION_SPEC.md` §11 (IconKey / ColorKey — Checklist bắt
> buộc cho mọi node).

---

## Final Notes

**⚠️ CRITICAL REMINDERS:**

1. **Always check for existing classes**: `TitleDisplayModeOption` có thể đã được định nghĩa trong ViewModel khác
2. **Renderer is CRITICAL**: Nếu hỗ trợ TitleDisplayMode, PHẢI implement UpdateNodePosition() và RemoveNode() trong Renderer
3. **NotifyTitleChanged()**: Phải gọi trong SaveTitle(), CreateDuplicateNodeInstance(), và RequestEditNodeTitle()
4. **Port colors**: Phải update trong CẢ RenderNode() và UpdateNodePosition()
5. **PropertyChanged handlers**: Phải combine TẤT CẢ trong MỘT block
6. **Throttling**: Phải implement throttling trong LayoutUpdated để tránh performance issues
7. **Persistence is CRITICAL**: ⚠️ **PHẢI implement serialize/deserialize cho TẤT CẢ custom properties** - Nếu không, properties sẽ bị mất khi save/export JSON (ví dụ: ListOutNode.OutputMappings)

**Follow the checklist step by step, and you'll avoid all common errors!**
