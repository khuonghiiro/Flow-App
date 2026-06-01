# ViewModel — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách tạo file ViewModel cho node mới.

---

## 7. ViewModel

`BaseNodeDialogViewModel` đã có sẵn toàn bộ properties, collections và commands chung.

### 7.1 BaseNodeDialogViewModel đã có sẵn — KHÔNG khai báo lại

**Observable Properties:**

| Property | Kiểu | Ghi chú |
|----------|------|---------|
| `NodeTitle` | `string` | Sync từ `node.Title` |
| `TitleDisplayMode` | `TitleDisplayMode` | Sync từ node |
| `TitleColorMode` | `TitleColorMode` | Sync từ node |
| `TitleColorKey` | `string?` | Sync từ node; update canvas ngay khi thay đổi |
| `InputPortPosition` | `PortPosition` | Từ port IN đầu tiên |
| `OutputPortPosition` | `PortPosition` | Từ port OUT đầu tiên |
| `Inputs` | `ObservableCollection<InputItemViewModel>` | Load từ `DynamicInputs` |
| `Outputs` | `ObservableCollection<OutputItemViewModel>` | Load từ `DynamicOutputs` |
| `ReuseRoutes` | `ObservableCollection<ReuseRouteItemViewModel>` | Load từ connections |

**Collections tĩnh (KHÔNG khai báo lại — khai báo lại sẽ shadow base):**

| Collection | Nội dung |
|-----------|---------|
| `TitleDisplayModeOptions` | Hidden / Hover / Always |
| `TitleColorOptions` | NodeColor / LimeGreen / PrimaryBrush / ... |
| `PortPositionOptions` | Left / Top / Right / Bottom |
| `ConnectionLineStyleOptions` | WorkflowDefault / Bezier / Orthogonal / ... |

**Commands:** `RunSingleNodeCommand`, `RunWorkflowFromNodeCommand`, `SaveTitleCommand`

**Protected helpers (gọi trực tiếp — KHÔNG viết lại):**

| Method | Chức năng |
|--------|----------|
| `RefreshAllNodesWithOutputs(target)` | Lấy tất cả nodes có DynamicOutputs |
| `GetOutputKeysForNode(nodeId)` | Lấy output keys của node theo ID |
| `FillOutputKeys(nodeId, target)` | Điền output keys vào collection |
| `CreateDataSourceOption(node)` | Tạo option đầy đủ icon/brush |
| `ResolveNodeTypeDisplayName(type)` | NodeType → display name |
| `ResolveNodeIconKey(type)` | NodeType → icon key |
| `ResolveTextOnNodeBrush(colorKey)` | ColorKey → `TextOnXxxBrush` |

### 7.2 Template chuẩn

```csharp
public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly YourNode _yourNode;

    // Chỉ khai báo properties ĐẶC THÙ của node này
    [ObservableProperty] private string _someProperty = string.Empty;
    [ObservableProperty] private int _someCount;

    // Chỉ khai báo collections ĐẶC THÙ (KHÔNG khai báo lại TitleDisplayModeOptions!)
    public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

    public YourNodeDialogViewModel(YourNode node, IWorkflowEditorHost host)
        : base(node, host)  // base ctor tự gọi LoadInputs, LoadOutputs, LoadReuseRoutes
    {
        _yourNode = node ?? throw new ArgumentNullException(nameof(node));

        // Sync properties từ node → VM
        SomeProperty = _yourNode.SomeProperty;
        SomeCount    = _yourNode.SomeCount;

        // Dùng base helper để load node options
        RefreshAllNodesWithOutputs(AvailableNodeOptions);

        // Subscribe PropertyChanged cho properties ĐẶC THÙ
        node.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(YourNode.SomeProperty))
                SomeProperty = _yourNode.SomeProperty;
            OnNodePropertyChanged(e.PropertyName ?? string.Empty);
        };
    }

    // BẮT BUỘC
    protected override string GetDefaultTitle() => "Your Node";

    // CHỈ override nếu cần lưu thêm properties ngoài Title/TitleDisplayMode/TitleColorMode
    protected override void OnSaveTitle()
    {
        if (_yourNode.SomeProperty != SomeProperty)
        {
            _yourNode.SomeProperty = SomeProperty;
            _host.RequestSyncDataPanels(immediate: true);
        }
        _yourNode.NotifyTitleChanged();
    }

    // CHỈ override LoadInputs nếu cần filter inputs
    // protected override void LoadInputs() { ... }
}
```

### 7.3 Khi nào cần RefreshAvailableNodes tùy chỉnh

`RefreshAllNodesWithOutputs(target)` lấy **tất cả** nodes có DynamicOutputs. Chỉ viết logic riêng khi cần filter đặc biệt:

```csharp
// Chỉ lấy nodes kết nối trực tiếp
private void RefreshDirectlyConnectedNodes(ObservableCollection<WorkflowDataSourceOption> target)
{
    target.Clear();
    var directSources = _host.ViewModel?.Connections
        .Where(c => c.ToNode == _node && c.FromNode?.DynamicOutputs?.Count > 0)
        .Select(c => c.FromNode!)
        .Distinct();
    foreach (var n in directSources ?? Enumerable.Empty<WorkflowNode>())
        target.Add(CreateDataSourceOption(n));
}
```

### 7.4 Checklist ViewModel

```yaml
- [ ] Kế thừa BaseNodeDialogViewModel
- [ ] Constructor gọi base(node, host)
- [ ] Override GetDefaultTitle() — BẮT BUỘC
- [ ] KHÔNG khai báo lại: TitleDisplayModeOptions, TitleColorOptions,
      PortPositionOptions, ConnectionLineStyleOptions
- [ ] KHÔNG tự viết: GetOutputKeysForNode, FillOutputKeys, CreateDataSourceOption,
      ResolveNodeTypeDisplayName, ResolveNodeIconKey, ResolveTextOnNodeBrush
- [ ] Nếu node không dùng ReuseRoutes: override SupportsReuseRoutes => false
- [ ] Nếu cần lưu thêm: override OnSaveTitle()
- [ ] Nếu cần filter inputs: override LoadInputs()
- [ ] Gọi _node.NotifyTitleChanged() ở cuối OnSaveTitle()
```

---

## Lỗi thường gặp

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| V1 | `TitleDisplayModeOptions` binding không hoạt động | Khai báo lại local với tên khác | Xóa local copy — base đã có |
| V2 | `GetOutputKeysForNode` không tìm thấy | Tự viết lại với `private` | Dùng `protected` method từ base |
| V3 | `CreateDataSourceOption` tạo option thiếu icon/brush | Tự tạo `new WorkflowDataSourceOption { NodeId, Title }` | Dùng `CreateDataSourceOption(node)` từ base |
| V4 | `LoadReuseRoutes()` override thừa | Override cả `SupportsReuseRoutes => false` lẫn `LoadReuseRoutes()` | Chỉ cần `SupportsReuseRoutes => false` |
| V5 | Inputs không load khi mở dialog | Quên gọi `base(node, host)` | Base ctor tự gọi `LoadInputs()` |

---

## Reference Implementations

Xem các file sau làm mẫu thực tế:
- `ViewModels/DelayNodeDialogViewModel.cs` - ViewModel chuẩn với OnSaveTitle
- `ViewModels/StorageNodeDialogViewModel.cs` - ViewModel với filter đặc biệt
