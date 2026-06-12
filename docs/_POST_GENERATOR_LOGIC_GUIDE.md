# Hướng dẫn hoàn thiện logic sau khi Node Generator tạo xong base

> Cập nhật: 2026-06-12
> Áp dụng khi đã dùng **NodeGeneratorWindow** tạo xong scaffold.
> Đọc cùng `_NODE_CREATION_GUIDE.md` để hiểu kiến trúc đầy đủ.

---

## Generator đã tạo gì / chưa tạo gì

| File | Generator tạo | Còn phải làm thủ công |
|------|---|---|
| `YourNode.cs` | Class + placeholder comments | Properties thực, `RebuildDynamicOutputs()`, `InputMapping` class |
| `YourNodeControl.cs` | Đầy đủ — dùng được ngay | Thêm custom handlers nếu node có properties đặc thù |
| `YourNodeDialog.xaml` | Layout đầy đủ | — |
| `YourNodeDialog.xaml.cs` | Constructor + GetInputsPanel/GetOutputsPanel | `BeforeSaveOnClose()` nếu cần |
| `YourNodeRenderer.cs` | Đầy đủ — dùng được ngay | — |
| `YourNodeDialogViewModel.cs` | Properties + TODO comments | **Bỏ comment TODO** để enable load/save, thêm `OnSaveTitle()` |
| Persistence | **KHÔNG tạo** | Phải viết thủ công |
| Copy/paste | **KHÔNG tạo** | Phải viết thủ công |
| Executor | **KHÔNG tạo** | Phải viết thủ công nếu node cần chạy |
| Đăng ký hệ thống | Chỉ thêm `NodeType` enum | 5 chỗ còn lại |

---

## BƯỚC 1 — Hoàn thiện Node Model

Generator tạo class rỗng với placeholder. Phải bổ sung:

### 1.1 Thêm properties thực

```csharp
public sealed class YourNode : WorkflowNode
{
    // ─── Properties đặc thù ───
    private string _sourceNodeId = string.Empty;
    private string _sourceOutputKey = string.Empty;
    private string _customKey = string.Empty;   // nếu HasCustomKeyOverride

    public string SourceNodeId
    {
        get => _sourceNodeId;
        set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
    }

    public string SourceOutputKey
    {
        get => _sourceOutputKey;
        set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
    }

    // ─── Nếu HasDynamicInputs: thêm class mapping + list ───
    // (xem §1.3 bên dưới)

    public YourNode()
    {
        Type = NodeType.YourType;   // ← đã tạo bởi Generator nếu AddNewNodeType=true
        Title = "Your Node";
        // ⚠️ KHÔNG thêm Ports ở đây — TemplateFactory sẽ tạo
    }
}
```

**Quy tắc:**
- KHÔNG khai báo lại: `PropertyChanged`, `OnPropertyChanged`, `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey`
- KHÔNG thêm Ports trong constructor

### 1.2 Nếu có OutputKeys (HasOutputsPanel)

```csharp
private List<string> _outputKeys = new() { "result" };

public List<string> OutputKeys
{
    get => _outputKeys;
    set { _outputKeys = value ?? new(); OnPropertyChanged(); RebuildDynamicOutputs(); }
}

public void RebuildDynamicOutputs()
{
    DynamicOutputs.Clear();
    foreach (var key in _outputKeys)
    {
        if (string.IsNullOrWhiteSpace(key)) continue;
        DynamicOutputs.Add(new WorkflowDynamicDataPort
        {
            Key = key.Trim(),
            DisplayName = key.Trim(),
            OutputType = WorkflowDataType.String,
            IsUserAdded = true
        });
    }
}

// Gọi trong constructor:
// RebuildDynamicOutputs();
```

### 1.3 Nếu có InputMappings (HasDynamicInputs)

```csharp
// Tạo class mapping (ngoài YourNode):
public sealed class YourInputMapping : INotifyPropertyChanged
{
    private string? _sourceNodeId;
    private string? _sourceOutputKey;
    private string? _inputKeyOverride;   // nếu HasCustomKeyOverride

    public string? SourceNodeId
    {
        get => _sourceNodeId;
        set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
    }
    public string? SourceOutputKey
    {
        get => _sourceOutputKey;
        set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
    }
    public string? InputKeyOverride  // nếu HasCustomKeyOverride
    {
        get => _inputKeyOverride;
        set { if (_inputKeyOverride != value) { _inputKeyOverride = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// Trong YourNode:
private List<YourInputMapping> _inputMappings = new();
public List<YourInputMapping> InputMappings
{
    get => _inputMappings;
    set { _inputMappings = value ?? new(); OnPropertyChanged(); }
}
// Thêm 1 item mặc định trong constructor:
// _inputMappings.Add(new YourInputMapping());
```

---

## BƯỚC 2 — Hoàn thiện ViewModel

Generator tạo properties nhưng tất cả phần sync còn là `// TODO`. Phải bỏ comment và điền logic.

### 2.1 Bỏ comment TODO trong `partial void OnXxxChanged`

Generator sinh:
```csharp
partial void OnSourceNodeIdChanged(string value)
{
    // TODO: Lưu SourceNodeId vào node nếu node có property này:
    // _yourNode.SourceNodeId = value;
    FillOutputKeys(value, SourceKeyOptions);
}
```

Phải sửa thành:
```csharp
partial void OnSourceNodeIdChanged(string value)
{
    _yourNode.SourceNodeId = value;         // ← bỏ comment
    FillOutputKeys(value, SourceKeyOptions);
}

partial void OnSourceOutputKeyChanged(string value)
{
    _yourNode.SourceOutputKey = value;      // ← bỏ comment
}

// Nếu HasCustomKeyOverride:
partial void OnCustomKeyChanged(string value)
{
    _yourNode.CustomKey = value;            // ← bỏ comment
}

// Tương tự cho CustomTextBoxes và CustomCheckBoxes:
partial void OnSomePropertyChanged(string value)
{
    _yourNode.SomeProperty = value;         // ← bỏ comment
}
```

### 2.2 Bỏ comment TODO trong constructor — load từ node

Generator sinh các dòng comment trong constructor:
```csharp
// TODO: nếu YourNode có SourceNodeId/SourceOutputKey thì bỏ comment:
// SourceNodeId = _yourNode.SourceNodeId;
// SourceOutputKey = _yourNode.SourceOutputKey;
```

Phải sửa thành:
```csharp
// Load input section
RefreshAllNodesWithOutputs(AvailableNodeOptions);
SourceNodeId = _yourNode.SourceNodeId;         // ← bỏ comment
FillOutputKeys(SourceNodeId, SourceKeyOptions);
SourceOutputKey = _yourNode.SourceOutputKey;   // ← bỏ comment

// Nếu HasCustomKeyOverride:
CustomKey = _yourNode.CustomKey;

// Load properties đặc thù:
SomeProperty = _yourNode.SomeProperty;
```

### 2.3 Bỏ comment TODO trong `SyncInputMappingsToNode` (HasDynamicInputs)

Generator sinh:
```csharp
private void SyncInputMappingsToNode()
{
    // TODO: Sync về node.InputMappings nếu node có property này:
    // _yourNode.InputMappings = InputMappingsList.Select(x => { ... }).ToList();
}
```

Phải sửa thành:
```csharp
private void SyncInputMappingsToNode()
{
    _yourNode.InputMappings = InputMappingsList.Select(x => new YourInputMapping
    {
        SourceNodeId = x.SourceNodeId,
        SourceOutputKey = x.SourceOutputKey,
        InputKeyOverride = string.IsNullOrWhiteSpace(x.InputKeyOverride)
            ? null : x.InputKeyOverride.Trim()  // nếu HasCustomKeyOverride
    }).ToList();
}
```

### 2.4 Bỏ comment TODO — restore InputMappings từ node (HasDynamicInputs)

Generator sinh block restore trong constructor dạng comment. Phải bỏ comment:
```csharp
// Load dynamic inputs từ node
RefreshAvailableNodes();
var mappings = _yourNode.InputMappings ?? new List<YourInputMapping>();
if (mappings.Count == 0) mappings.Add(new YourInputMapping());
foreach (var m in mappings)
{
    var item = new YourNodeInputMappingItemViewModel
    {
        SourceNodeId = m.SourceNodeId,
        SourceOutputKey = m.SourceOutputKey,
        InputKeyOverride = m.InputKeyOverride ?? string.Empty  // nếu HasCustomKeyOverride
    };
    item.PropertyChanged += InputMappingItem_PropertyChanged;
    InputMappingsList.Add(item);
    RefreshOutputKeyOptionsFor(item);
}
// Xóa dòng fallback empty bên dưới vì đã restore thật
```

### 2.5 Uncomment và viết `OnSaveTitle()`

Generator để `OnSaveTitle()` dạng comment. Phải override và điền:
```csharp
protected override void OnSaveTitle()
{
    // Lưu properties đặc thù về node:
    if (_yourNode.SourceNodeId != SourceNodeId)
        _yourNode.SourceNodeId = SourceNodeId;
    if (_yourNode.SourceOutputKey != SourceOutputKey)
        _yourNode.SourceOutputKey = SourceOutputKey;
    if (_yourNode.SomeProperty != SomeProperty)
    {
        _yourNode.SomeProperty = SomeProperty;
        _host.RequestSyncDataPanels(immediate: true);
    }

    // Nếu HasDynamicInputs:
    SyncInputMappingsToNode();

    // Nếu HasOutputsPanel + OutputKeys:
    _yourNode.OutputKeys = OutputKeysList
        .Where(x => !string.IsNullOrWhiteSpace(x.Key))
        .Select(x => x.Key.Trim()).Distinct().ToList();
    _yourNode.RebuildDynamicOutputs();

    _yourNode.NotifyTitleChanged();
}
```

---

## BƯỚC 3 — Save/Load Workflow (Persistence)

> ⚠️ Generator **KHÔNG tạo** phần này. Phải viết thủ công.
> 
> ⚠️ **KIẾN TRÚC MỚI**: `FileWorkflowPersistenceService` sử dụng **partial class** pattern.
> File chính chỉ chứa **dispatcher** (if/else if). Logic nằm trong `Services/Workflow/Persistence/*.cs`.

### 3.1 Tạo methods trong file partial

Chọn file partial phù hợp trong `Services/Workflow/Persistence/` (hoặc tạo file mới).
Mỗi file đều là `public sealed partial class FileWorkflowPersistenceService`.

```csharp
// File: Services/Workflow/Persistence/NodeProperties_Misc.cs (hoặc file phù hợp)

// -- RESTORE (Deserialize) --
private static void RestoreYourNodeProperties(YourNode node, Dictionary<string, object> properties)
{
    // Properties đặc thù — dùng TryGetValue để tương thích file cũ
    if (properties.TryGetValue("SourceNodeId", out var sid))
        node.SourceNodeId = sid?.ToString() ?? string.Empty;
    if (properties.TryGetValue("SourceOutputKey", out var sok))
        node.SourceOutputKey = sok?.ToString() ?? string.Empty;
    if (properties.TryGetValue("SomeProperty", out var sp))
        node.SomeProperty = sp?.ToString() ?? string.Empty;

    // Nếu HasCustomKeyOverride:
    if (properties.TryGetValue("CustomKey", out var ck))
        node.CustomKey = ck?.ToString() ?? string.Empty;

    // Nếu HasOutputsPanel + OutputKeys:
    if (properties.TryGetValue("OutputKeys", out var okObj) && okObj != null)
    {
        try
        {
            string? json = okObj is string s ? s
                : okObj is JsonElement je && je.ValueKind == JsonValueKind.String ? je.GetString()
                : okObj is JsonElement je2 && je2.ValueKind == JsonValueKind.Array ? je2.GetRawText()
                : null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                var keys = JsonSerializer.Deserialize<List<string>>(json);
                if (keys?.Count > 0) { node.OutputKeys = keys; node.RebuildDynamicOutputs(); }
            }
        }
        catch { }
    }

    // Nếu HasDynamicInputs + InputMappings:
    if (properties.TryGetValue("InputMappings", out var imObj) && imObj != null)
    {
        try
        {
            string? json = imObj is string s2 ? s2
                : imObj is JsonElement je3 && je3.ValueKind == JsonValueKind.String ? je3.GetString()
                : imObj is JsonElement je4 && je4.ValueKind == JsonValueKind.Array ? je4.GetRawText()
                : null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                var raw = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
                if (raw != null)
                {
                    var list = raw.Select(item => new YourInputMapping
                    {
                        SourceNodeId    = item.TryGetValue("SourceNodeId", out var a) ? a.GetString() : null,
                        SourceOutputKey = item.TryGetValue("SourceOutputKey", out var b) ? b.GetString() : null,
                        InputKeyOverride = item.TryGetValue("InputKeyOverride", out var c) ? c.GetString() : null
                    }).ToList();
                    if (list.Count > 0) node.InputMappings = list;
                }
            }
        }
        catch { }
    }
}

// -- GET (Serialize) --
private static void GetYourNodeProperties(YourNode node, Dictionary<string, object> dict)
{
    // Properties đặc thù
    dict["SourceNodeId"]    = node.SourceNodeId;
    dict["SourceOutputKey"] = node.SourceOutputKey;
    dict["SomeProperty"]    = node.SomeProperty;

    // Nếu HasCustomKeyOverride:
    dict["CustomKey"] = node.CustomKey;

    // Nếu HasOutputsPanel + OutputKeys:
    if (node.OutputKeys?.Count > 0)
        dict["OutputKeys"] = JsonSerializer.Serialize(node.OutputKeys);

    // Nếu HasDynamicInputs + InputMappings:
    if (node.InputMappings?.Count > 0)
    {
        var arr = node.InputMappings.Select(m => new Dictionary<string, object?>
        {
            ["SourceNodeId"]     = m.SourceNodeId,
            ["SourceOutputKey"]  = m.SourceOutputKey,
            ["InputKeyOverride"] = m.InputKeyOverride   // nếu HasCustomKeyOverride
        }).ToList();
        dict["InputMappings"] = JsonSerializer.Serialize(arr);
    }
}
```

### 3.2 Thêm dispatch vào file chính

File: `Services/Workflow/FileWorkflowPersistenceService.cs` — chỉ thêm **2 dòng dispatch**:

```csharp
// Trong RestoreNodeProperties() — thêm else if vào chuỗi
else if (node is YourNode yourNode)
{
    RestoreYourNodeProperties(yourNode, properties);
}

// Trong GetNodeProperties() — thêm else if vào chuỗi
else if (node is YourNode yourNode)
{
    GetYourNodeProperties(yourNode, dict);
}
```

### 3.3 Shared properties — KHÔNG cần viết

Các properties sau đã được `NodeProperties_Shared.cs` xử lý **tự động** cho mọi node — **KHÔNG viết trong method Restore/Get riêng**:

- `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey`
- `RunMode`, `AutoRunInterval*`, `AutoScope*`
- `EndBehavior`, `DiamondSharpness`, `ConditionalVisualMode`
- `ReuseRoutes`
- `DynamicInputs` (DynIn_*)
- `Condition`, `Key`, `MouseEvent`, `TargetElement`, `FloatingWidget`

> ⚠️ Luôn dùng `TryGetValue` — không dùng `properties["Key"]` để tránh crash với file cũ chưa có key đó.
> Deserialize handle cả `string` lẫn `JsonElement` vì format lưu có thể khác nhau tùy version.

---

## BƯỚC 4 — Copy/Paste

> ⚠️ Generator **KHÔNG tạo** phần này.

### 4.1 Cho phép Ctrl+C node (`Services/Interaction/WorkflowEditorEventService.cs`)

```csharp
// Thêm YourNode vào điều kiện copy:
if (vm.SelectedNode is YourNode || vm.SelectedNode is InputNode || ...)
{ _copiedNode = vm.SelectedNode; e.Handled = true; return; }
// Tương tự cho Ctrl+V
```

### 4.2 Copy properties (`Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs`)

```csharp
// Trong CreateDuplicateNodeInstance():
else if (source is YourNode srcYour && node is YourNode dstYour)
{
    // Properties đặc thù
    dstYour.SourceNodeId    = srcYour.SourceNodeId;
    dstYour.SourceOutputKey = srcYour.SourceOutputKey;
    dstYour.SomeProperty    = srcYour.SomeProperty;
    dstYour.CustomKey       = srcYour.CustomKey;   // nếu HasCustomKeyOverride

    // Title (bắt buộc mọi node)
    dstYour.TitleDisplayMode = srcYour.TitleDisplayMode;
    dstYour.TitleColorMode   = srcYour.TitleColorMode;
    dstYour.TitleColorKey    = srcYour.TitleColorKey;

    // ⚠️ Deep copy Lists — KHÔNG gán trực tiếp reference
    dstYour.InputMappings = srcYour.InputMappings
        .Select(m => new YourInputMapping
        {
            SourceNodeId    = m.SourceNodeId,
            SourceOutputKey = m.SourceOutputKey,
            InputKeyOverride = m.InputKeyOverride
        }).ToList();

    dstYour.OutputKeys = new List<string>(srcYour.OutputKeys);
    dstYour.RebuildDynamicOutputs();
    dstYour.NotifyTitleChanged();
}
```

### 4.3 Remap NodeId sau multi-paste (`WorkflowEditorWindow.MultiNodeClipboard.cs`)

```csharp
// Trong RemapNodeReferenceIds(node, sourceToNewNodeMap):
case YourNode yourNode:
    yourNode.SourceNodeId = RemapNodeId(yourNode.SourceNodeId, sourceToNewNodeMap);
    foreach (var m in yourNode.InputMappings ?? new())
        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);
    break;
```

> ⚠️ Mọi field dạng `*NodeId` phải remap. Không remap → combobox chọn sai node sau paste.

---

## BƯỚC 5 — Đăng ký vào hệ thống (6 chỗ)

Generator chỉ tạo `NodeType` enum. Còn 5 chỗ:

| # | File | Việc cần làm |
|---|------|---|
| 1 | `Views/WorkflowEditorWindow.xaml` | Thêm `<Border Tag="YourNodeTypeName">` + ToolTip + ContextMenu vào palette |
| 2 | `Services/Workflow/TemplateFactory.cs` | `"YourNodeTypeName" => CreateYourNode(x, y)` + method tạo node + ports |
| 3 | `Views/WorkflowEditors/WorkflowEditorWindow.TemplateNodeHandler.cs` | `"YourNodeTypeName" => "your-icon-key duotone-regular"` |
| 4 | `ViewModels/WorkflowEditorViewModel.cs` | `NodeType.YourType => "your-icon-key duotone-regular"` |
| 5 | `Controls/NodeSearchComboBoxUserControl.xaml.cs` | `"YourNodeType" => "your-icon-key duotone-regular"` |
| 6 | `ViewModels/BaseNodeDialogViewModel.cs` | `NodeType.YourType => "your-icon-key duotone-regular"` |

**Đăng ký Renderer:**
- `Services/Rendering/_NodeRenderer.cs`: thêm field + constructor + 3 if-branch (`RenderNode`, `UpdateNodePosition`, `RemoveNode`)
- DI container: `services.AddSingleton<YourNodeRenderer>()`

**Đăng ký Executor:**
- `Services/Workflow/WorkflowExecutionService.cs`: thêm `new YourNodeExecutor()` vào `_nodeExecutors` list

---

## BƯỚC 6 — Executor (nếu node có logic chạy)

```csharp
internal sealed class YourNodeExecutor : INodeExecutor
{
    public bool CanExecute(WorkflowNode node) => node is YourNode;

    public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
    {
        var yourNode = (YourNode)node;
        var sw = Stopwatch.StartNew();

        try
        {
            // 1. Đọc input — LUÔN dùng *ForExecution APIs (KHÔNG dùng NodeDataPanelService)
            var inputValue = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                env.Connections, yourNode.SourceNodeId, yourNode.SourceOutputKey, env);

            // 2. Logic đặc thù
            var result = DoYourLogic(yourNode, inputValue);

            // 3. Ghi output vào scoped store
            if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
            {
                env.Service.SetScopedNodeStringOutput(
                    env.ExecutionId, yourNode.Id, "result", result?.ToString() ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            env.OnNodeFailed?.Invoke(yourNode, ex.Message);
            throw;
        }

        sw.Stop();
        env.OnNodeCompleted?.Invoke(yourNode, sw.Elapsed);

        // 4. LUÔN gọi ở cuối
        await env.TraverseOutputsAsync(yourNode);
    }
}
```

---

## Quy tắc Multi-row NodeSearchComboBox (HasDynamicInputs)

Khi dialog có nhiều dòng input trong `ItemsControl`, phải tuân thủ:

### ✅ ĐÚNG / ❌ SAI

| Pattern | ❌ SAI | ✅ ĐÚNG |
|---|---|---|
| Output keys của mỗi dòng | Dùng chung 1 collection ở parent VM | Mỗi item VM có `AvailableOutputKeyOptions` riêng |
| ComboBox Key | Không có `IsSynchronizedWithCurrentItem` | `IsSynchronizedWithCurrentItem="False"` |
| NodeSearchComboBox ItemsSource | `{Binding AvailableNodeOptions}` | `{Binding DataContext.AvailableNodeOptions, RelativeSource={RelativeSource AncestorType=ItemsControl}}` |
| Refresh collection | `Clear() + Add(item)` từng cái | `AvailableOutputKeys = new ObservableCollection<...>(opts)` |
| So sánh NodeId | `n.Id == selectedId` | `string.Equals(n.Id, selectedId, StringComparison.OrdinalIgnoreCase)` |

### Refresh output keys đúng cách

```csharp
public void RefreshOutputKeyOptionsFor(YourNodeInputMappingItemViewModel item)
{
    var currentKey = item.SourceOutputKey; // lưu lại TRƯỚC khi clear

    // ✅ Thay collection 1 lần — tránh WPF reset SelectedValue khi tạm rỗng
    var node = _host.ViewModel?.Nodes.FirstOrDefault(n =>
        string.Equals(n.Id, item.SourceNodeId, StringComparison.OrdinalIgnoreCase));
    var opts = node?.DynamicOutputs?
        .Where(o => !string.IsNullOrWhiteSpace(o.Key))
        .Select(o => new WorkflowOutputKeyOption
        {
            Key = o.Key.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(o.DisplayName) ? o.Key : o.DisplayName,
            Type = o.OutputType ?? o.ConvertType
        }).ToList() ?? new();

    item.AvailableOutputKeyOptions.Clear();
    foreach (var o in opts) item.AvailableOutputKeyOptions.Add(o);

    // Giữ selection nếu key còn tồn tại
    if (!string.IsNullOrWhiteSpace(currentKey) &&
        opts.Any(k => string.Equals(k.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
        item.SourceOutputKey = currentKey;
    else if (opts.Count > 0)
        item.SourceOutputKey = opts[0].Key;
}
```

---

## Checklist tổng hợp

### Node Model
- [ ] Thêm properties thực (xóa placeholder comments)
- [ ] `NodeType` enum đã có (Generator tạo nếu AddNewNodeType=true)
- [ ] Nếu HasOutputsPanel: thêm `OutputKeys` + `RebuildDynamicOutputs()`, gọi trong constructor
- [ ] Nếu HasDynamicInputs: tạo `YourInputMapping` class + `InputMappings` property

### ViewModel
- [ ] **Bỏ comment TODO** trong tất cả `partial void OnXxxChanged` → thêm `_yourNode.Xxx = value`
- [ ] **Bỏ comment TODO** trong constructor → load properties từ node về VM
- [ ] **Bỏ comment TODO** restore InputMappings (HasDynamicInputs)
- [ ] Viết `OnSaveTitle()` — lưu toàn bộ về node, gọi `NotifyTitleChanged()`
- [ ] `SyncInputMappingsToNode()` thực sự sync về node (bỏ comment)

### Persistence — Partial Class (HOÀN TOÀN THỦ CÔNG)
- [ ] Tạo `RestoreYourNodeProperties()` trong `Persistence/NodeProperties_Xxx.cs` phù hợp
- [ ] Tạo `GetYourNodeProperties()` trong cùng file
- [ ] Thêm `else if` dispatch vào `RestoreNodeProperties()` trong file chính
- [ ] Thêm `else if` dispatch vào `GetNodeProperties()` trong file chính
- [ ] **KHÔNG viết inline** trong file chính — PHẢI tạo method trong `Persistence/`
- [ ] **KHÔNG serialize** shared props (Title, RunMode, ReuseRoutes, DynamicInputs) — đã tự động
- [ ] Dùng `TryGetValue` cho mọi key
- [ ] Handle cả `string` và `JsonElement` khi deserialize list
- [ ] Gọi `RebuildDynamicOutputs()` sau khi restore OutputKeys

### Copy/Paste (HOÀN TOÀN THỦ CÔNG)
- [ ] `WorkflowEditorEventService.cs`: Ctrl+C + Ctrl+V
- [ ] `WorkflowEditorWindow.NodeActions.cs`: deep copy properties + Lists
- [ ] `WorkflowEditorWindow.MultiNodeClipboard.cs`: remap mọi `*NodeId` field

### Đăng ký hệ thống
- [ ] Palette XAML: Border + Tag + ToolTip + ContextMenu
- [ ] TemplateFactory: string switch + `CreateYourNode()` + ports
- [ ] WorkflowEditorWindow.TemplateNodeHandler.cs: icon key
- [ ] WorkflowEditorViewModel.cs: icon key
- [ ] NodeSearchComboBoxUserControl.xaml.cs: icon key
- [ ] BaseNodeDialogViewModel.cs: icon key
- [ ] `_NodeRenderer.cs`: field + constructor + 3 branches
- [ ] DI: `services.AddSingleton<YourNodeRenderer>()`

### Executor (nếu node cần chạy)
- [ ] Implement `INodeExecutor` (CanExecute + ExecuteAsync)
- [ ] Dùng `*ForExecution` APIs — KHÔNG dùng `NodeDataPanelService`
- [ ] Gọi `env.OnNodeCompleted?.Invoke()` sau khi xong
- [ ] Gọi `env.OnNodeFailed?.Invoke() + throw` khi lỗi
- [ ] Gọi `env.TraverseOutputsAsync(node)` ở cuối (bắt buộc)
- [ ] Đăng ký vào `_nodeExecutors` trong `WorkflowExecutionService`

---

## Quy tắc không được vi phạm

| ❌ KHÔNG làm | ✅ Thay bằng |
|---|---|
| Đọc output node khác bằng `NodeDataPanelService` trong Executor | `env.Service.ResolveDynamicValueForExecution(...)` |
| Bỏ `env.TraverseOutputsAsync(node)` ở cuối Executor | LUÔN gọi ở cuối |
| `dstNode.Items = srcNode.Items` khi copy/paste | `.Select(i => new Item { ... }).ToList()` |
| `properties["Key"]` trong deserialize | `properties.TryGetValue("Key", out var v)` |
| Thêm ports trong `YourNode` constructor | Để TemplateFactory tạo |
| Khai báo lại `PropertyChanged`, `TitleDisplayMode`... | Xóa — base đã có |
| Serialize nhưng không deserialize (hoặc ngược lại) | Phải có cả 2 |
| `services.AddTransient<YourNodeRenderer>()` | `services.AddSingleton<YourNodeRenderer>()` |

---

## Thứ tự test

1. **Build** — 0 error
2. **Drag node từ palette** → node xuất hiện trên canvas, icon/màu đúng
3. **Chuột phải node** → dialog mở đúng, comboboxes có dữ liệu
4. **Thay đổi giá trị → đóng → mở lại** → giá trị còn nguyên (sync ViewModel↔Node)
5. **Save workflow → đóng app → mở lại** → node restore đúng (Persistence)
6. **Ctrl+C → Ctrl+V** → node mới có đúng properties, không chia sẻ reference
7. **Multi-paste** → NodeId combobox trỏ đúng node
8. **Run workflow** → executor chạy, output ghi đúng, node tiếp theo nhận được
