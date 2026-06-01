# Dynamic Input/Output — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách tạo node cho phép user thêm/xóa inputs và outputs.

---

## 15. Dynamic Input/Output — Node cho phép user thêm/xóa inputs và outputs

> Dùng khi node cần user cấu hình nhiều nguồn dữ liệu (như CodeNode, HtmlUiNode) hoặc nhiều output keys (như CodeNode).
> **Reference**: `CodeNode.cs`, `CodeNodeDialogViewModel.cs`, `CodeNodeDialog.xaml`

### 15.1 Node Model — List thay vì DynamicInputs cố định

```csharp
// Tạo class mapping item riêng (implement INotifyPropertyChanged nếu cần reactive)
public sealed class YourInputMapping : INotifyPropertyChanged
{
    private string? _sourceNodeId;
    private string? _sourceOutputKey;
    private string? _inputKeyOverride;

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
    // Tên biến tùy chỉnh (trống = dùng SourceOutputKey)
    public string? InputKeyOverride
    {
        get => _inputKeyOverride;
        set { if (_inputKeyOverride != value) { _inputKeyOverride = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class YourNode : WorkflowNode
{
    private List<YourInputMapping> _inputMappings = new();
    private List<string> _outputKeys = new() { "result" };

    public YourNode()
    {
        Type = NodeType.YourType;
        Title = "Your Node";
        _inputMappings.Add(new YourInputMapping()); // 1 dòng mặc định
        RebuildDynamicOutputs();
        Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = true,  Position = PortPosition.Left,  IsVisible = true, ColorKey = "Info" });
        Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = false, Position = PortPosition.Right, IsVisible = true, ColorKey = "SunsetOrange" });
    }

    // List input mappings (user có thể thêm/xóa)
    public List<YourInputMapping> InputMappings
    {
        get => _inputMappings;
        set { _inputMappings = value ?? new(); OnPropertyChanged(); }
    }

    // List output keys (user có thể thêm/xóa)
    public List<string> OutputKeys
    {
        get => _outputKeys;
        set { _outputKeys = value ?? new(); OnPropertyChanged(); RebuildDynamicOutputs(); }
    }

    // Đồng bộ OutputKeys → DynamicOutputs (gọi sau mỗi lần thay đổi OutputKeys)
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
}
```

### 15.2 ViewModel — ObservableCollection + Add/Remove Commands

```csharp
// Sub-ViewModel cho mỗi dòng input
public partial class YourInputMappingItemViewModel : ObservableObject
{
    [ObservableProperty] private string? _sourceNodeId;
    [ObservableProperty] private string? _sourceOutputKey;
    [ObservableProperty] private string _inputKeyOverride = string.Empty;

    // Output keys của node đã chọn (mỗi dòng có list riêng)
    public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

    // Khi user đổi node → refresh output keys
    partial void OnSourceNodeIdChanged(string? value) => RefreshOutputKeys(value);

    private void RefreshOutputKeys(string? nodeId)
    {
        // Sẽ được gọi từ ViewModel cha — xem bên dưới
    }
}

// Sub-ViewModel cho mỗi output key
public partial class OutputKeyItemViewModel : ObservableObject
{
    [ObservableProperty] private string _key = "result";
}

public partial class YourNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly YourNode _yourNode;
    private bool _isSyncingFromNode; // Guard tránh StackOverflow khi sync 2 chiều

    public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
    public ObservableCollection<YourInputMappingItemViewModel> InputMappingsList { get; } = new();
    public ObservableCollection<OutputKeyItemViewModel> OutputKeysList { get; } = new();

    public YourNodeDialogViewModel(YourNode node, IWorkflowEditorHost host)
        : base(node, host)
    {
        _yourNode = node;

        // Load input mappings từ node → VM
        foreach (var m in node.InputMappings)
        {
            var item = new YourInputMappingItemViewModel
            {
                SourceNodeId = m.SourceNodeId,
                SourceOutputKey = m.SourceOutputKey,
                InputKeyOverride = m.InputKeyOverride ?? string.Empty
            };
            item.PropertyChanged += InputMappingItem_PropertyChanged;
            InputMappingsList.Add(item);
            RefreshOutputKeyOptionsFor(item); // load keys cho node đã chọn
        }

        // Load output keys từ node → VM
        foreach (var k in node.OutputKeys)
            OutputKeysList.Add(new OutputKeyItemViewModel { Key = k });

        // Load danh sách node có thể chọn
        RefreshAllNodesWithOutputs(AvailableNodeOptions);
    }

    protected override string GetDefaultTitle() => "Your Node";

    // Khi user thay đổi bất kỳ field nào trong 1 dòng input → sync về node
    private void InputMappingItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncingFromNode) return;
        if (sender is not YourInputMappingItemViewModel item) return;

        if (e.PropertyName == nameof(YourInputMappingItemViewModel.SourceNodeId))
        {
            RefreshOutputKeyOptionsFor(item); // refresh keys khi đổi node
        }
        SyncInputMappingsToNode();
    }

    // Refresh output keys cho 1 dòng input (gọi khi user đổi node)
    public void RefreshOutputKeyOptionsFor(YourInputMappingItemViewModel item)
    {
        item.AvailableOutputKeyOptions.Clear();
        if (string.IsNullOrWhiteSpace(item.SourceNodeId) || _host.ViewModel?.Nodes == null) return;

        var node = _host.ViewModel.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, item.SourceNodeId, StringComparison.OrdinalIgnoreCase));
        if (node?.DynamicOutputs == null) return;

        foreach (var o in node.DynamicOutputs)
        {
            item.AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
            {
                Key = o.Key ?? string.Empty,
                Type = o.OutputType ?? o.ConvertType,
                DisplayName = o.DisplayName ?? o.Key
            });
        }

        // Nếu key đang chọn không còn trong list → chọn key đầu tiên
        if (item.AvailableOutputKeyOptions.Count > 0 &&
            !item.AvailableOutputKeyOptions.Any(k =>
                string.Equals(k.Key, item.SourceOutputKey, StringComparison.OrdinalIgnoreCase)))
        {
            item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key;
        }
    }

    // Sync VM → node (gọi sau mỗi thay đổi)
    private void SyncInputMappingsToNode()
    {
        _yourNode.InputMappings = InputMappingsList.Select(x => new YourInputMapping
        {
            SourceNodeId = x.SourceNodeId,
            SourceOutputKey = x.SourceOutputKey,
            InputKeyOverride = string.IsNullOrWhiteSpace(x.InputKeyOverride) ? null : x.InputKeyOverride.Trim()
        }).ToList();
    }

    private void SyncOutputKeysToNode()
    {
        _yourNode.OutputKeys = OutputKeysList
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => x.Key.Trim())
            .Distinct()
            .ToList();
        _yourNode.RebuildDynamicOutputs();
    }

    protected override void OnSaveTitle()
    {
        SyncInputMappingsToNode();
        SyncOutputKeysToNode();
        _yourNode.NotifyTitleChanged();
        _host.RequestSyncDataPanels(immediate: true);
    }

    // Commands Add/Remove input
    [RelayCommand]
    private void AddInputMapping()
    {
        var item = new YourInputMappingItemViewModel();
        item.PropertyChanged += InputMappingItem_PropertyChanged;
        InputMappingsList.Add(item);
        SyncInputMappingsToNode();
    }

    [RelayCommand]
    private void RemoveInputMapping(YourInputMappingItemViewModel? item)
    {
        if (item == null || InputMappingsList.Count <= 1) return; // giữ ít nhất 1 dòng
        item.PropertyChanged -= InputMappingItem_PropertyChanged;
        InputMappingsList.Remove(item);
        SyncInputMappingsToNode();
    }

    // Commands Add/Remove output key
    [RelayCommand]
    private void AddOutputKey()
    {
        OutputKeysList.Add(new OutputKeyItemViewModel { Key = "result" });
        SyncOutputKeysToNode();
    }

    [RelayCommand]
    private void RemoveOutputKey(OutputKeyItemViewModel? item)
    {
        if (item == null) return;
        OutputKeysList.Remove(item);
        SyncOutputKeysToNode();
    }
}
```

### 15.3 Dialog XAML — ItemsControl với Add/Remove

```xml
<!-- Input mappings: nhiều dòng, mỗi dòng = Node + Key + Tên biến -->
<TextBlock Text="Input (node nguồn + key):"
           Foreground="{DynamicResource TextBrush}" FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
<ItemsControl x:Name="InputMappingsItemsControl"
              ItemsSource="{Binding InputMappingsList}" Margin="0,0,0,6">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Grid Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="8"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="8"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Cột 1: Chọn Node nguồn -->
                <StackPanel Grid.Column="0">
                    <TextBlock Text="Node" Foreground="{DynamicResource TextMuted}" FontSize="11" Margin="0,0,0,4"/>
                    <controls:NodeSearchComboBoxUserControl Height="36"
                        ItemsSource="{Binding DataContext.AvailableNodeOptions,
                                     RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                        SelectedValuePath="NodeId"
                        DisplayMemberPath="Title"
                        SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"/>
                </StackPanel>

                <!-- Cột 2: Chọn Output Key của node đã chọn -->
                <StackPanel Grid.Column="2">
                    <TextBlock Text="Key" Foreground="{DynamicResource TextMuted}" FontSize="11" Margin="0,0,0,4"/>
                    <ComboBox Height="36" Style="{DynamicResource BaseComboBox}"
                              ItemsSource="{Binding AvailableOutputKeyOptions}"
                              SelectedValuePath="Key"
                              DisplayMemberPath="DisplayName"
                              IsSynchronizedWithCurrentItem="False"
                              SelectedValue="{Binding SourceOutputKey, Mode=TwoWay}"/>
                </StackPanel>

                <!-- Cột 3: Tên biến tùy chỉnh (optional) -->
                <StackPanel Grid.Column="4">
                    <TextBlock Text="Tên biến (trống = dùng key)" Foreground="{DynamicResource TextMuted}" FontSize="11" Margin="0,0,0,4"/>
                    <TextBox Text="{Binding InputKeyOverride, UpdateSourceTrigger=PropertyChanged}"
                             Style="{DynamicResource BaseTextBoxV2}" Height="36"/>
                </StackPanel>

                <!-- Nút xóa dòng -->
                <Button Grid.Column="5" Content="-" Width="28" Height="28" Margin="8,0,0,0"
                        VerticalAlignment="Bottom"
                        Command="{Binding DataContext.RemoveInputMappingCommand,
                                  RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                        CommandParameter="{Binding}"
                        Background="{DynamicResource DangerBrush}" Foreground="White"
                        BorderThickness="0" Cursor="Hand"/>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
<Button Content="+ Thêm input" Height="32" Width="120"
        Command="{Binding AddInputMappingCommand}"
        Style="{DynamicResource PrimaryButton}" HorizontalAlignment="Left" Margin="0,0,0,16"/>

<!-- Output keys: nhiều dòng, mỗi dòng = 1 TextBox key + nút xóa -->
<TextBlock Text="Output keys:" Foreground="{DynamicResource TextBrush}"
           FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
<Border Background="{DynamicResource WindowBackground}"
        BorderBrush="{DynamicResource ControlBorderBrush}"
        BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,8">
    <StackPanel>
        <ItemsControl ItemsSource="{Binding OutputKeysList}">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Grid Margin="0,0,0,6">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <TextBox Text="{Binding Key, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                 Style="{DynamicResource BaseTextBoxV2}" Height="32"/>
                        <Button Grid.Column="1" Content="-" Width="28" Height="28" Margin="8,0,0,0"
                                Command="{Binding DataContext.RemoveOutputKeyCommand,
                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                CommandParameter="{Binding}"
                                Background="{DynamicResource DangerBrush}" Foreground="White"
                                BorderThickness="0" Cursor="Hand"/>
                    </Grid>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
        <Button Content="+ Thêm output key" Height="32" Width="140"
                Command="{Binding AddOutputKeyCommand}"
                Style="{DynamicResource PrimaryButton}" HorizontalAlignment="Left" Margin="0,8,0,0"/>
    </StackPanel>
</Border>
```

**⚠️ Điểm quan trọng trong XAML:**
- `NodeSearchComboBoxUserControl` bind `ItemsSource` qua `RelativeSource AncestorType=ItemsControl` để lấy `AvailableNodeOptions` từ parent ViewModel — **không bind trực tiếp `{Binding AvailableNodeOptions}`** vì DataContext của DataTemplate là item VM, không phải parent VM.
- `ComboBox` Key dùng `IsSynchronizedWithCurrentItem="False"` để tránh các dòng đồng bộ selection với nhau.
- Mỗi dòng có `AvailableOutputKeyOptions` riêng (trong `YourInputMappingItemViewModel`) — không dùng chung 1 list.

### 15.4 Persistence — Serialize/Deserialize list

```csharp
// SERIALIZE — trong GetNodeProperties():
if (node is YourNode yn)
{
    // Serialize InputMappings thành JSON array
    if (yn.InputMappings?.Count > 0)
    {
        var arr = yn.InputMappings.Select(m => new Dictionary<string, object?>
        {
            ["SourceNodeId"]    = m.SourceNodeId,
            ["SourceOutputKey"] = m.SourceOutputKey,
            ["InputKeyOverride"] = m.InputKeyOverride
        }).ToList();
        dict["InputMappings"] = JsonSerializer.Serialize(arr);
    }

    // Serialize OutputKeys thành JSON array
    if (yn.OutputKeys?.Count > 0)
        dict["OutputKeys"] = JsonSerializer.Serialize(yn.OutputKeys);
}

// DESERIALIZE — trong RestoreNodeProperties():
if (node is YourNode yn)
{
    // Deserialize InputMappings — phải handle cả string và JsonElement
    if (properties.TryGetValue("InputMappings", out var imObj) && imObj != null)
    {
        var list = new List<YourInputMapping>();
        try
        {
            string? json = imObj is string s ? s
                : imObj is JsonElement je && je.ValueKind == JsonValueKind.String ? je.GetString()
                : imObj is JsonElement je2 && je2.ValueKind == JsonValueKind.Array ? je2.GetRawText()
                : null;

            if (!string.IsNullOrWhiteSpace(json))
            {
                var raw = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
                if (raw != null)
                {
                    foreach (var item in raw)
                    {
                        list.Add(new YourInputMapping
                        {
                            SourceNodeId    = item.TryGetValue("SourceNodeId", out var sid) ? sid.GetString() : null,
                            SourceOutputKey = item.TryGetValue("SourceOutputKey", out var sok) ? sok.GetString() : null,
                            InputKeyOverride = item.TryGetValue("InputKeyOverride", out var iko) ? iko.GetString() : null
                        });
                    }
                }
            }
        }
        catch { }
        if (list.Count > 0) yn.InputMappings = list;
    }

    // Deserialize OutputKeys
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
                if (keys?.Count > 0)
                {
                    yn.OutputKeys = keys;
                    yn.RebuildDynamicOutputs();
                }
            }
        }
        catch { }
    }
}
```

### 15.5 Copy/Paste — Deep copy lists

```csharp
// Trong CreateDuplicateNodeInstance():
else if (source is YourNode srcYour && node is YourNode dstYour)
{
    // ⚠️ Deep copy — KHÔNG gán trực tiếp reference
    dstYour.InputMappings = srcYour.InputMappings
        .Select(m => new YourInputMapping
        {
            SourceNodeId     = m.SourceNodeId,
            SourceOutputKey  = m.SourceOutputKey,
            InputKeyOverride = m.InputKeyOverride
        }).ToList();

    dstYour.OutputKeys = new List<string>(srcYour.OutputKeys);
    dstYour.RebuildDynamicOutputs();
    dstYour.NotifyTitleChanged();
}

// Trong RemapNodeReferenceIds():
case YourNode yourNode:
    foreach (var m in yourNode.InputMappings ?? new())
        m.SourceNodeId = RemapNodeId(m.SourceNodeId, sourceToNewNodeMap);
    break;
```

### 15.6 Checklist Dynamic Input/Output

```yaml
Node Model:
  - [ ] Tạo class YourInputMapping (INotifyPropertyChanged nếu cần reactive)
  - [ ] List<YourInputMapping> InputMappings với 1 item mặc định trong constructor
  - [ ] List<string> OutputKeys với default keys
  - [ ] RebuildDynamicOutputs() đồng bộ OutputKeys → DynamicOutputs
  - [ ] Gọi RebuildDynamicOutputs() trong constructor và khi OutputKeys thay đổi

ViewModel:
  - [ ] Sub-VM YourInputMappingItemViewModel với AvailableOutputKeyOptions riêng
  - [ ] Sub-VM OutputKeyItemViewModel
  - [ ] bool _isSyncingFromNode guard tránh StackOverflow
  - [ ] InputMappingItem_PropertyChanged: khi đổi SourceNodeId → RefreshOutputKeyOptionsFor
  - [ ] RefreshOutputKeyOptionsFor: Clear + Add keys, giữ selection nếu key còn tồn tại
  - [ ] SyncInputMappingsToNode() + SyncOutputKeysToNode() gọi sau mỗi thay đổi
  - [ ] AddInputMappingCommand / RemoveInputMappingCommand (giữ ít nhất 1 dòng)
  - [ ] AddOutputKeyCommand / RemoveOutputKeyCommand
  - [ ] OnSaveTitle: gọi cả 2 Sync methods

XAML:
  - [ ] ItemsControl bind ItemsSource="{Binding InputMappingsList}"
  - [ ] NodeSearchComboBoxUserControl bind qua RelativeSource AncestorType=ItemsControl
  - [ ] ComboBox Key dùng IsSynchronizedWithCurrentItem="False"
  - [ ] Nút "-" dùng CommandParameter="{Binding}" + RelativeSource để gọi Remove command

Persistence:
  - [ ] Serialize InputMappings thành JSON array (Dictionary per item)
  - [ ] Serialize OutputKeys thành JSON array
  - [ ] Deserialize handle cả string và JsonElement
  - [ ] Gọi RebuildDynamicOutputs() sau khi restore OutputKeys

Copy/Paste:
  - [ ] Deep copy InputMappings (Select + new item)
  - [ ] Deep copy OutputKeys (new List<string>)
  - [ ] Gọi RebuildDynamicOutputs() sau copy
  - [ ] Remap SourceNodeId trong RemapNodeReferenceIds
```
