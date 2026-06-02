# Đăng ký Node vào Hệ thống — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách đăng ký node mới vào hệ thống.

---

## 11. Đăng ký node vào hệ thống

Có **11 chỗ** bắt buộc khi thêm node mới. Thiếu bất kỳ chỗ nào sẽ gây fallback icon/màu sai hoặc node không tạo được.

### 11.1 NodeType enum

```csharp
// Models/Nodes/NodeType.cs
public enum NodeType
{
    // ... existing types ...
    YourType,  // ← thêm vào đây
}
```

### 11.2 Palette XAML — `Views/WorkflowEditorWindow.xaml`

Thêm vào `NodeTemplatesPanel` (trong `WrapPanel` của nhóm phù hợp). **Mỗi node palette phải có cả `ToolTip` lẫn `ContextMenu`** — thiếu `ContextMenu` thì chuột phải vào icon không hiện thông tin:

```xml
<Border Style="{StaticResource PaletteIconNodeStyle}"
        Background="{DynamicResource ForestPineBrush}"
        Tag="YourNodeTypeName">

    <!-- ToolTip: hiện khi hover — title in đậm/nghiêng + gạch đầu dòng -->
    <Border.ToolTip>
        <ToolTip>
            <StackPanel MaxWidth="240">
                <!-- Title: in đậm + in nghiêng -->
                <TextBlock FontWeight="Bold" FontStyle="Italic">
                    <Run Text="Tên Node"/>
                </TextBlock>
                <!-- Mô tả ngắn (1 dòng) -->
                <TextBlock Text="Mô tả ngắn gọn về chức năng."
                           TextWrapping="Wrap" Margin="0,4,0,0" Opacity="0.9"/>
                <!-- Gạch đầu dòng: mỗi tính năng / output là 1 dòng -->
                <TextBlock Margin="0,6,0,0">
                    <Run Text="• "/>
                    <Run Text="Tính năng / output 1" FontWeight="SemiBold"/>
                </TextBlock>
                <TextBlock>
                    <Run Text="• "/>
                    <Run Text="Tính năng / output 2"/>
                </TextBlock>
                <TextBlock>
                    <Run Text="• "/>
                    <Run Text="Tính năng / output 3"/>
                </TextBlock>
            </StackPanel>
        </ToolTip>
    </Border.ToolTip>

    <!-- ContextMenu: hiện khi chuột phải (chi tiết hơn) — BẮT BUỘC -->
    <Border.ContextMenu>
        <ContextMenu Placement="MousePoint" StaysOpen="False">
            <MenuItem IsHitTestVisible="False">
                <MenuItem.Header>
                    <Border Background="{DynamicResource ForestPineBrush}"
                            CornerRadius="10" Padding="10"
                            BorderBrush="{DynamicResource BorderColor}" BorderThickness="1">
                        <StackPanel>
                            <TextBlock Text="Tên Node"
                                       Foreground="{DynamicResource TextOnForestPineBrush}"
                                       FontWeight="Bold" FontSize="13"/>
                            <TextBlock Text="Mô tả chi tiết hơn tooltip."
                                       Foreground="{DynamicResource TextOnForestPineBrush}"
                                       Opacity="0.9" TextWrapping="Wrap" Margin="0,4,0,0"/>
                            <!-- Gạch đầu dòng cho outputs / tính năng quan trọng -->
                            <TextBlock Text="• Tính năng / output 1"
                                       Foreground="{DynamicResource TextOnForestPineBrush}"
                                       Opacity="0.85" Margin="0,4,0,0"/>
                            <TextBlock Text="• Tính năng / output 2"
                                       Foreground="{DynamicResource TextOnForestPineBrush}"
                                       Opacity="0.85"/>
                        </StackPanel>
                    </Border>
                </MenuItem.Header>
            </MenuItem>
        </ContextMenu>
    </Border.ContextMenu>

    <Grid>
        <controls:SvgViewboxEx Style="{StaticResource PaletteSvgIconStyle}"
                              Source="{Binding Source={x:Static sys:String.Empty},
                                       Converter={StaticResource IconKeyToPathConverter},
                                       ConverterParameter='your-icon-key duotone-regular'}"
                              Fill="{DynamicResource TextOnForestPineBrush}"/>
    </Grid>
</Border>
```

**Quy tắc format ToolTip:**

| Phần | Format | Ghi chú |
|------|--------|---------|
| Title | `FontWeight="Bold" FontStyle="Italic"` | Dùng `<Run Text="..."/>` bên trong `<TextBlock>` |
| Mô tả ngắn | `TextWrapping="Wrap" Opacity="0.9"` | 1 câu, không quá 2 dòng |
| Gạch đầu dòng | `<Run Text="• "/>` + `<Run Text="..."/>` | Mỗi output/tính năng 1 dòng; key quan trọng dùng `FontWeight="SemiBold"` |
| MaxWidth | `240` | Đủ rộng cho 2–3 từ/dòng |

> ⚠️ **Bắt buộc**: Mọi node trong palette phải có **cả `ToolTip` lẫn `ContextMenu`**.
> - `ToolTip`: hiện khi hover — title in đậm/nghiêng + gạch đầu dòng tính năng/output
> - `ContextMenu`: hiện khi chuột phải — mô tả chi tiết hơn, có thể thêm bullet points
> - Thiếu `ContextMenu` → chuột phải vào icon không hiện gì (lỗi UX)
> - Thiếu format title → tooltip trông như plain text, khó đọc
>
> `Tag` phải là **string** khớp chính xác với switch case trong TemplateFactory.
> Thay `ForestPine` bằng ColorKey thực của node.

### 11.3 TemplateFactory — `Services/Workflow/TemplateFactory.cs`

```csharp
// Thêm vào switch trong Create(string nodeType, double x, double y):
"YourNodeTypeName" => CreateYourNode(x, y),

// Thêm method:
private static WorkflowNode CreateYourNode(double x, double y)
{
    var node = new YourNode
    {
        Id = Guid.NewGuid().ToString(),
        X = x, Y = y,
        ColorKey = "ForestPine",  // ⚠️ Khớp với palette Background
        NodeBrush = Application.Current.TryFindResource("ForestPineBrush") as Brush ?? Brushes.Green
    };

    node.Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = true,
        Position = PortPosition.Left, IsVisible = true, ColorKey = "Info" });
    node.Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = false,
        Position = PortPosition.Right, IsVisible = true, ColorKey = "SunsetOrange" });

    return node;
}
```

### 11.4 Icon trên canvas — `Views/WorkflowEditors/WorkflowEditorWindow.TemplateNodeHandler.cs`

```csharp
// Trong GetIconNameForNodeType(string nodeType):
"YourNodeTypeName" => "your-icon-key duotone-regular",
```

### 11.5 Icon trong Execution Trace — `ViewModels/WorkflowEditorViewModel.cs`

```csharp
// Trong ResolveNodeIconKey(NodeType type):
NodeType.YourType => "your-icon-key duotone-regular",
```

### 11.6 Đăng ký Renderer — `Services/Rendering/_NodeRenderer.cs`

```csharp
// Thêm field
private readonly YourNodeRenderer _yourNodeRenderer;

// Thêm vào constructor parameter list + body
_yourNodeRenderer = yourNodeRenderer ?? throw new ArgumentNullException(nameof(yourNodeRenderer));

// Thêm if branch vào RenderNode() (trước fallback cuối):
if (node is YourNode yourNode)
{
    _yourNodeRenderer.RenderNode(yourNode, canvas);
    return;
}
// Tương tự cho UpdateNodePosition() và RemoveNode()
```

Đăng ký DI (thường `App.xaml.cs` hoặc `ServiceRegistration.cs`):
```csharp
services.AddSingleton<YourNodeRenderer>();
```

### 11.7 Đăng ký Executor (nếu có) — `Services/Workflow/WorkflowExecutionService.cs`

```csharp
// Thêm vào _nodeExecutors list trong constructor:
new NodeExecutors.YourNodeExecutor(),
```

### 11.8 Copy/Paste — `Services/Interaction/WorkflowEditorEventService.cs`

```csharp
// Ctrl+C: thêm YourNode vào điều kiện copy
if (vm.SelectedNode is YourNode || vm.SelectedNode is InputNode || ...)
{ _copiedNode = vm.SelectedNode; e.Handled = true; return; }

// Ctrl+V: thêm tương tự cho paste
```

### 11.9 Copy/Paste properties — `Views/WorkflowEditors/WorkflowEditorWindow.NodeActions.cs`

```csharp
// Trong CreateDuplicateNodeInstance():
else if (source is YourNode srcYour && node is YourNode dstYour)
{
    dstYour.SomeProperty    = srcYour.SomeProperty;
    dstYour.SomeCount       = srcYour.SomeCount;
    // Title properties (bắt buộc cho mọi node)
    dstYour.TitleDisplayMode = srcYour.TitleDisplayMode;
    dstYour.TitleColorMode   = srcYour.TitleColorMode;
    dstYour.TitleColorKey    = srcYour.TitleColorKey;
    // ⚠️ Clone lists — KHÔNG gán trực tiếp reference
    dstYour.Items = srcYour.Items.Select(i => new YourItem { Key = i.Key, Value = i.Value }).ToList();
    dstYour.NotifyTitleChanged();
}
```

### 11.10 Remap NodeId sau multi-paste — `Views/WorkflowEditors/WorkflowEditorWindow.MultiNodeClipboard.cs`

```csharp
// Trong RemapNodeReferenceIds(node, sourceToNewNodeMap):
case YourNode yourNode:
    yourNode.SourceNodeId = RemapNodeId(yourNode.SourceNodeId, sourceToNewNodeMap);
    foreach (var m in yourNode.InputMappings ?? new())
        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);
    break;
```

> ⚠️ Mọi field dạng `*NodeId` phải remap. Không remap → combobox chọn sai node sau paste.
> Quy ước: dùng hậu tố `NodeId` cho mọi field giữ Id của node khác.

### 11.11 Checklist đăng ký

```yaml
- [ ] NodeType enum value thêm vào NodeType.cs
- [ ] Palette XAML: Border + Tag + ColorKey + iconKey + TextOnColorKeyBrush + **cả ToolTip lẫn ContextMenu**
- [ ] TemplateFactory: string switch case + CreateYourNode()
- [ ] WorkflowEditorWindow.TemplateNodeHandler.cs: GetIconNameForNodeType
- [ ] WorkflowEditorViewModel.cs: ResolveNodeIconKey
- [ ] BaseNodeDialogViewModel.cs: ResolveNodeIconKey method (cho icon hiển thị trong dialog)
- [ ] NodeSearchComboBoxUserControl.xaml.cs: ResolveIconKey method (cho icon hiển thị trong NodeSearchComboBox)
- [ ] _NodeRenderer.cs: field + constructor + if branch trong 3 methods
- [ ] DI container: services.AddSingleton<YourNodeRenderer>()
- [ ] WorkflowExecutionService: thêm executor vào _nodeExecutors (nếu có)
- [ ] WorkflowEditorEventService.cs: Ctrl+C + Ctrl+V
- [ ] WorkflowEditorWindow.NodeActions.cs: CreateDuplicateNodeInstance
- [ ] WorkflowEditorWindow.MultiNodeClipboard.cs: RemapNodeReferenceIds (nếu có *NodeId fields)
```

---

## 11.A IconKey / ColorKey — 6 chỗ phải khớp nhau

> Thiếu bất kỳ chỗ nào → node bị fallback icon `circle-question`, nền `AccentBrush`, icon màu sai.

| # | File | Khai báo gì |
|---|------|-------------|
| 1 | `Views/WorkflowEditorWindow.xaml` | `Tag="NodeTypeName"` + `Background="{DynamicResource <ColorKey>Brush}"` + `ConverterParameter='<iconKey>'` + `Fill="{DynamicResource TextOn<ColorKey>Brush}"` |
| 2 | `Views/WorkflowEditors/WorkflowEditorWindow.TemplateNodeHandler.cs` | `case "NodeTypeName" => "<iconKey>"` trong `GetIconNameForNodeType` |
| 3 | `Services/Workflow/TemplateFactory.cs` | `ColorKey = "<ColorKey>"` trong `CreateYourNode()` |
| 4 | `ViewModels/WorkflowEditorViewModel.cs` | `NodeType.YourType => "<iconKey>"` trong `ResolveNodeIconKey` |
| 5 | `Controls/NodeSearchComboBoxUserControl.xaml.cs` | `"YourNodeType" => "<iconKey>"` trong `ResolveIconKey` method |
| 6 | `ViewModels/BaseNodeDialogViewModel.cs` | `NodeType.YourType => "<iconKey>"` trong `ResolveNodeIconKey` method |

**Quy tắc ColorKey:**
- Nền node/palette: `{DynamicResource <ColorKey>Brush}` — brush phải có trong `Themes/Base/Colors/Common.xaml`
- Màu chữ/icon: `{DynamicResource TextOn<ColorKey>Brush}` — **KHÔNG hardcode Black/White**
- Nếu ColorKey mới chưa có: thêm `<SolidColorBrush x:Key="NeonLimeBrush" Color="#C6FF00"/>` và `<SolidColorBrush x:Key="TextOnNeonLimeBrush" Color="#1F2937"/>` vào `Common.xaml`

---

## 11.B Theme System — DynamicResource bắt buộc

**KHÔNG hardcode màu trong XAML dialog.** Dùng DynamicResource để dialog tự đổi màu theo theme:

| Resource Key | Dùng cho | Thay cho |
|---|---|---|
| `{DynamicResource DialogOuterBorder}` | Style outer border dialog | `Background="#FF1E293B"` |
| `{DynamicResource DialogHeaderBorder}` | Style header dialog | `Background="#FF0F172A"` |
| `{DynamicResource TextBrush}` | Foreground text chính | `Foreground="White"` |
| `{DynamicResource TextSecondary}` | Text phụ / mô tả | `Foreground="#CCCCCC"` |
| `{DynamicResource WindowBackground}` | Background card/panel | `Background="#FF1E293B"` |
| `{DynamicResource ControlBorderBrush}` | BorderBrush controls | `BorderBrush="#33FFFFFF"` |
| `{DynamicResource BaseTextBoxV2}` | Style TextBox | — |
| `{DynamicResource BaseComboBox}` | Style ComboBox | — |
| `{DynamicResource PrimaryButton}` | Style nút Play | — |
| `{DynamicResource DangerButton}` | Style nút Close | — |
| `{StaticResource HttpTabItemStyle}` | Style TabItem | — (**StaticResource**, không phải Dynamic) |

**Dialog sizing:**
```xml
<local:BaseNodeDialog Width="460" MinWidth="350" MaxWidth="900" MinHeight="350">
<!-- ⚠️ KHÔNG đặt Height cứng — NodeDialogManager auto-size 90% screen -->
<!-- ⚠️ KHÔNG đặt MaxHeight cố định -->
```

---

## 11.C ExecutionId & Scoped Outputs — bắt buộc khi viết Executor

Nhiều workflow có thể chạy **đồng thời**. Nếu executor đọc output node khác qua `NodeDataPanelService`, nó sẽ đọc nhầm kết quả của run khác.

| API | Dùng khi nào |
|-----|-------------|
| `env.Service.ResolveDynamicValueForExecution(sourceNode, key, env)` | Đọc output node khác trong executor |
| `env.Service.ResolveValueByNodeIdAndKeyForExecution(connections, nodeId, key, env)` | Đọc theo nodeId + key |
| `env.TraverseOutputsAsync(node)` | Chuyển sang node tiếp theo — **LUÔN gọi ở cuối** |
| `MirrorRuntimeOutputsToScopedStore(node, executionId)` | Tự động gọi bởi service sau executor — không cần gọi thủ công |
| `PublishStorageOutputsToScoped(storage, executionId)` | StorageNode: gọi **trước** `TraverseOutputsAsync` |
| `IWorkflowScopedOutputContributor` | Implement trên node model nếu output không nằm trong switch mirror |

**Quy tắc:**
- Trong Executor: **LUÔN** dùng `*ForExecution` APIs có `env`
- **KHÔNG** dùng `NodeDataPanelService.ResolveDynamicValueByKey` trong executor
- Output mới sau khi node chạy → đảm bảo có trong `MirrorRuntimeOutputsToScopedStore` hoặc `IWorkflowScopedOutputContributor`

**Checklist nhanh khi thêm Executor:**
```yaml
- [ ] Mọi chỗ đọc output node khác → dùng ResolveDynamicValueForExecution (có env)
- [ ] Gọi env.TraverseOutputsAsync(node) ở cuối ExecuteAsync
- [ ] Gọi env.OnNodeCompleted?.Invoke() sau khi xong
- [ ] Gọi env.OnNodeFailed?.Invoke() + throw khi có lỗi
- [ ] Nếu node có output chuỗi mới → đảm bảo có trong MirrorRuntimeOutputsToScopedStore
- [ ] Nếu là StorageNode → PublishStorageOutputsToScoped trước TraverseOutputsAsync
```
