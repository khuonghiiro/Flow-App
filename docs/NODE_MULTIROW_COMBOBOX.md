# Multi-row NodeSearchComboBox — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách tránh lỗi đồng bộ khi có nhiều dòng NodeSearchComboBox.

---

## 16. Multi-row NodeSearchComboBox trong ItemsControl — Tránh lỗi đồng bộ

> Đây là lỗi phổ biến nhất khi có nhiều dòng `NodeSearchComboBoxUserControl` + `ComboBox Key` trong `ItemsControl`.
> **Reference**: `CodeNodeDialog.xaml`, `FlowOverwriteNodeDialog.xaml`, `FlowOverwriteNodeDialogViewModel.cs`

### 16.1 Các lỗi thường gặp và nguyên nhân

| Lỗi | Nguyên nhân |
|-----|-------------|
| Tất cả dòng hiển thị cùng 1 node/key | Nhiều dòng dùng chung 1 `ObservableCollection` `ItemsSource` + `IsSynchronizedWithCurrentItem=True` (default) |
| Selection bị reset khi mở dialog lại | `Loaded` event gọi `Clear()` + `Add()` trên `ItemsSource` trong khi `SelectedValue` đang bind TwoWay |
| ComboBox Key trống sau khi chọn Node | `AvailableOutputKeyOptions` là list chung, bị clear khi dòng khác refresh |
| Selection lệch sau paste | `SourceNodeId` không được remap sang Id mới |

### 16.2 Quy tắc bắt buộc

**1. Mỗi dòng có `AvailableOutputKeyOptions` riêng**

```csharp
// ✅ ĐÚNG: mỗi item VM có collection riêng
public partial class YourInputMappingItemViewModel : ObservableObject
{
    // Collection này chỉ thuộc về dòng này — không share với dòng khác
    public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();
}

// ❌ SAI: dùng chung 1 collection ở parent VM
// public ObservableCollection<WorkflowOutputKeyOption> SharedOutputKeys { get; } = new();
```

**2. `IsSynchronizedWithCurrentItem="False"` trên ComboBox Key**

```xml
<!-- ✅ ĐÚNG -->
<ComboBox ItemsSource="{Binding AvailableOutputKeyOptions}"
          IsSynchronizedWithCurrentItem="False"
          SelectedValue="{Binding SourceOutputKey, Mode=TwoWay}"/>

<!-- ❌ SAI: thiếu IsSynchronizedWithCurrentItem="False" -->
<ComboBox ItemsSource="{Binding AvailableOutputKeyOptions}"
          SelectedValue="{Binding SourceOutputKey, Mode=TwoWay}"/>
```

**3. `NodeSearchComboBoxUserControl` bind qua `RelativeSource`, không bind trực tiếp**

```xml
<!-- ✅ ĐÚNG: lấy AvailableNodeOptions từ parent ViewModel qua RelativeSource -->
<controls:NodeSearchComboBoxUserControl
    ItemsSource="{Binding DataContext.AvailableNodeOptions,
                 RelativeSource={RelativeSource AncestorType=ItemsControl}}"
    SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"/>

<!-- ❌ SAI: DataContext của DataTemplate là item VM, không có AvailableNodeOptions -->
<controls:NodeSearchComboBoxUserControl
    ItemsSource="{Binding AvailableNodeOptions}"
    SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"/>
```

**4. Thay collection một lần, không Clear + Add**

```csharp
// ✅ ĐÚNG: thay toàn bộ collection trong 1 lần (tránh WPF reset SelectedValue khi ItemsSource tạm rỗng)
private void SyncAvailableSourceOptions(List<WorkflowDataSourceOption> options)
{
    AvailableSourceOptions = new ObservableCollection<WorkflowDataSourceOption>(options);
    // Dùng [ObservableProperty] để WPF nhận PropertyChanged và rebind
}

// ❌ SAI: Clear + Add từng item → WPF reset SelectedValue khi collection tạm rỗng
AvailableSourceOptions.Clear();
foreach (var opt in options) AvailableSourceOptions.Add(opt);
```

**5. So sánh NodeId dùng `OrdinalIgnoreCase`**

```csharp
// ✅ ĐÚNG
var node = nodes.FirstOrDefault(n =>
    string.Equals(n.Id, selectedId, StringComparison.OrdinalIgnoreCase));

// ❌ SAI: so sánh case-sensitive có thể không khớp
var node = nodes.FirstOrDefault(n => n.Id == selectedId);
```

**6. Refresh output keys: giữ selection nếu key còn tồn tại**

```csharp
public void RefreshOutputKeyOptionsFor(YourInputMappingItemViewModel item)
{
    var currentKey = item.SourceOutputKey; // lưu lại trước khi clear

    item.AvailableOutputKeyOptions.Clear();
    // ... thêm keys mới ...

    // Chỉ reset selection nếu key cũ không còn trong list mới
    if (!string.IsNullOrWhiteSpace(currentKey) &&
        item.AvailableOutputKeyOptions.Any(k =>
            string.Equals(k.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
    {
        item.SourceOutputKey = currentKey; // giữ nguyên
    }
    else if (item.AvailableOutputKeyOptions.Count > 0)
    {
        item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key; // chọn key đầu tiên
    }
}
```

### 16.3 Pattern hoàn chỉnh cho multi-row với Node + Key

Đây là pattern đã được kiểm chứng từ `FlowOverwriteNodeDialog`:

**ViewModel:**
```csharp
// Parent ViewModel
public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
{
    // ✅ Dùng [ObservableProperty] để thay collection 1 lần
    [ObservableProperty]
    private ObservableCollection<WorkflowDataSourceOption> _availableSourceOptions = new();

    public ObservableCollection<YourSourceItemViewModel> Sources { get; } = new();

    // Khi cần refresh: tạo collection mới, không Clear + Add
    private void RefreshSourceOptions()
    {
        var options = BuildOptions(); // build list mới
        AvailableSourceOptions = new ObservableCollection<WorkflowDataSourceOption>(options);

        // Sau khi thay collection, refresh output keys cho từng dòng
        foreach (var row in Sources)
            row.RefreshOutputKeys();
    }
}

// Item ViewModel
public partial class YourSourceItemViewModel : ObservableObject
{
    [ObservableProperty] private string? _selectedSourceNodeId;
    [ObservableProperty] private string? _selectedSourceOutputKey;

    // ✅ Collection riêng cho mỗi dòng
    [ObservableProperty]
    private ObservableCollection<WorkflowOutputKeyOption> _availableOutputKeys = new();

    private readonly IWorkflowEditorHost _host;

    public YourSourceItemViewModel(IWorkflowEditorHost host) { _host = host; }

    // Khi user đổi node → tự động refresh keys
    partial void OnSelectedSourceNodeIdChanged(string? value) => RefreshOutputKeys();

    public void RefreshOutputKeys()
    {
        var selectedId = SelectedSourceNodeId;
        var srcNode = _host.ViewModel?.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, selectedId, StringComparison.OrdinalIgnoreCase));

        var opts = srcNode?.DynamicOutputs?
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => new WorkflowOutputKeyOption
            {
                Key = x.Key.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? x.Key : x.DisplayName,
                Type = x.OutputType ?? x.ConvertType
            })
            .ToList() ?? new();

        // ✅ Thay collection 1 lần
        AvailableOutputKeys = new ObservableCollection<WorkflowOutputKeyOption>(opts);

        // Giữ selection nếu key còn tồn tại, không thì chọn key đầu tiên
        if (string.IsNullOrWhiteSpace(SelectedSourceOutputKey) ||
            !opts.Any(o => string.Equals(o.Key, SelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedSourceOutputKey = opts.FirstOrDefault()?.Key;
        }
    }
}
```

**XAML:**
```xml
<ItemsControl ItemsSource="{Binding Sources}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Grid Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Node: lấy AvailableSourceOptions từ parent VM qua RelativeSource -->
                <controls:NodeSearchComboBoxUserControl Grid.Column="0" Height="32" Margin="0,0,6,0"
                    ItemsSource="{Binding DataContext.AvailableSourceOptions,
                                 RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                    SelectedValuePath="NodeId"
                    DisplayMemberPath="Title"
                    SelectedValue="{Binding SelectedSourceNodeId, Mode=TwoWay}"/>

                <!-- Key: lấy từ item VM riêng, IsSynchronizedWithCurrentItem="False" -->
                <ComboBox Grid.Column="1" Height="32" Margin="0,0,6,0"
                          Style="{DynamicResource BaseComboBox}"
                          ItemsSource="{Binding AvailableOutputKeys}"
                          IsSynchronizedWithCurrentItem="False"
                          SelectedValuePath="Key"
                          DisplayMemberPath="DisplayName"
                          SelectedValue="{Binding SelectedSourceOutputKey, Mode=TwoWay}"/>

                <Button Grid.Column="2" Content="X" Width="28" Height="28"
                        Style="{DynamicResource DangerButton}"
                        Click="RemoveSource_Click"/>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
<Button Content="+ Thêm nguồn" Height="32"
        Style="{DynamicResource PrimaryButton}" Click="AddSource_Click"/>
```

**Code-behind (nếu dùng Click thay vì Command):**
```csharp
private void AddSource_Click(object sender, RoutedEventArgs e)
{
    _viewModel.AddSource();
}

private void RemoveSource_Click(object sender, RoutedEventArgs e)
{
    if (sender is FrameworkElement fe && fe.DataContext is YourSourceItemViewModel item)
        _viewModel.RemoveSource(item);
}
```

### 16.4 Checklist Multi-row NodeSearchComboBox

```yaml
- [ ] Mỗi item VM có AvailableOutputKeyOptions/AvailableOutputKeys riêng (không share)
- [ ] ComboBox Key dùng IsSynchronizedWithCurrentItem="False"
- [ ] NodeSearchComboBoxUserControl bind qua RelativeSource AncestorType=ItemsControl
- [ ] Thay collection 1 lần (new ObservableCollection) thay vì Clear + Add
- [ ] RefreshOutputKeys: giữ selection nếu key còn tồn tại
- [ ] So sánh NodeId dùng OrdinalIgnoreCase
- [ ] partial void OnSelectedSourceNodeIdChanged → tự động RefreshOutputKeys
- [ ] Remap SourceNodeId trong RemapNodeReferenceIds sau multi-paste
```
