# Dialog Code-behind — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách tạo file Dialog code-behind cho node mới.

---

## 6. Dialog Code-behind

`BaseNodeDialog` đã xử lý toàn bộ logic chung. Code-behind chỉ cần constructor + 2 override bắt buộc.

### 6.1 BaseNodeDialog đã có sẵn — KHÔNG viết lại

| Tính năng | Ghi chú |
|-----------|---------|
| Snap phải màn hình | Tự động qua `SourceInitialized` |
| Clamp khi resize | Tự động qua `SizeChanged` |
| Lưu title khi đóng | Tự động qua `Closing` → `SaveTitleCommand` |
| `CloseButton_Click` | Tự động: `SaveTitleCommand + Close()` |
| `TitleColorComboBox_SelectionChanged` | Wire trong XAML, không cần override |
| `UpdateTitleColorPreview()` | Gọi trong constructor sau `InitializeBase()` |
| `ShowColorPicker(string? hex)` | Trả về `#RRGGBB` hoặc null |
| `ResolveBrush(string? key, Brush fallback)` | Hỗ trợ hex, "LimeGreen", resource key |
| `GetThemeBrush(string key, Brush fallback)` | Instance method |
| `GetThemeColor(string key, Color fallback)` | Instance method |
| `BindThemeResource(element, dp, key)` | Bind DynamicResource cho code-behind control |
| Load Inputs/Outputs | Tự động qua `Loaded` → `GetInputsPanel()` / `GetOutputsPanel()` |

### 6.2 Template chuẩn

```csharp
public partial class YourNodeDialog : BaseNodeDialog
{
    private readonly YourNodeDialogViewModel _viewModel;

    public YourNodeDialog(YourNode node, IWorkflowEditorHost host, Window? owner)
        : base()
    {
        InitializeComponent();
        _viewModel = new YourNodeDialogViewModel(node, host);
        InitializeBase(_viewModel, owner);

        // Gọi nếu XAML có TitleColorPreview + TitleColorComboBox
        UpdateTitleColorPreview();
    }

    // BẮT BUỘC: trả về panel chứa inputs (null nếu không có)
    protected override Panel? GetInputsPanel() => InputsPanel;

    // BẮT BUỘC: trả về panel chứa outputs (null nếu không có)
    protected override Panel? GetOutputsPanel() => OutputsPanel;

    // CHỈ override nếu cần logic sau Loaded
    // protected override void OnLoaded() { base.OnLoaded(); ... }

    // CHỈ override nếu cần flush binding khi đóng bằng Alt+F4 hoặc X taskbar
    // protected override void BeforeSaveOnClose() { FlushMyBindings(); }

    // CHỈ override CloseButton_Click nếu cần làm thêm gì trước khi đóng
    // protected override void CloseButton_Click(object sender, RoutedEventArgs e)
    // {
    //     FlushMyBindings();
    //     base.CloseButton_Click(sender, e); // ← gọi base để SaveTitle + Close
    // }
}
```

### 6.3 Flush binding trước khi đóng

```csharp
// Ưu tiên BeforeSaveOnClose — xử lý cả Alt+F4 và nút X
protected override void BeforeSaveOnClose()
{
    MyComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
    MyTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
}
```

### 6.4 Color picker tùy chỉnh

```csharp
private void PickMyColor_Click(object sender, RoutedEventArgs e)
{
    // ✅ Dùng base.ShowColorPicker — KHÔNG tự viết lại
    var hex = ShowColorPicker(_viewModel.MyColorHex);
    if (!string.IsNullOrWhiteSpace(hex))
    {
        _viewModel.MyColorHex = hex;
        // ✅ Dùng base.ResolveBrush — KHÔNG tự viết lại
        MyColorPreview.Background = ResolveBrush(hex, Brushes.Gray);
    }
}
```

### 6.5 Checklist Dialog Code-behind

```yaml
- [ ] Kế thừa BaseNodeDialog (KHÔNG kế thừa Window trực tiếp)
- [ ] Constructor: InitializeComponent() → new ViewModel → InitializeBase(vm, owner)
- [ ] Gọi UpdateTitleColorPreview() nếu XAML có TitleColorPreview
- [ ] Override GetInputsPanel() và GetOutputsPanel()
- [ ] KHÔNG tự viết: CloseButton_Click (chỉ SaveTitle+Close),
      TitleColorComboBox_SelectionChanged, UpdateTitleColorPreview,
      ShowColorPicker, ResolveBrush, GetThemeBrush, GetThemeColor,
      ViewModel_PropertyChanged rỗng
- [ ] Nếu cần flush binding: override BeforeSaveOnClose() (xử lý cả Alt+F4)
- [ ] KHÔNG set WindowStartupLocation sau InitializeBase — base đã set Manual
```

---

## Lỗi thường gặp

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| D1 | Title color preview không cập nhật khi mở | Quên gọi `UpdateTitleColorPreview()` trong constructor | Gọi sau `InitializeBase()` |
| D2 | Title color preview không cập nhật khi đổi combobox | XAML không wire `SelectionChanged="TitleColorComboBox_SelectionChanged"` | Thêm vào XAML |
| D3 | `TitleColorPreview` không tìm thấy | `x:Name` sai hoặc nằm trong DataTemplate | Đặt đúng `x:Name="TitleColorPreview"` ở cấp trực tiếp |
| D4 | Inputs/Outputs không load | `GetInputsPanel()` trả về null hoặc sai panel | Trả về đúng `StackPanel` có `x:Name="InputsPanel"` |
| D5 | Binding mất khi đóng bằng Alt+F4 | Chỉ override `CloseButton_Click`, không override `BeforeSaveOnClose` | Override `BeforeSaveOnClose()` |
| D6 | Dialog không snap vào cạnh phải màn hình | Tự set `WindowStartupLocation` sau `InitializeBase` | Không set — base đã set `Manual` |

---

## Reference Implementations

Xem các file sau làm mẫu thực tế:
- `Views/Overlays/DelayNodeDialog.xaml.cs` - Dialog code-behind chuẩn
- `Views/Overlays/AssignDataNodeDialog.xaml.cs` - Dialog với custom logic
