# Node Model — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách tạo file Model cho node mới.

---

## 3. Node Model

`WorkflowNode` đã implement `INotifyPropertyChanged` và chứa các property chung. **Không khai báo lại** những gì base đã có.

### 3.1 WorkflowNode đã có sẵn — KHÔNG khai báo lại

| Đã có trong base | Ghi chú |
|-----------------|---------|
| `PropertyChanged` event | Gọi `OnPropertyChanged()` trong setter là đủ |
| `OnPropertyChanged()` method | Có sẵn, gọi trực tiếp |
| `TitleDisplayMode` property | Default: `Always` — set lại trong constructor nếu muốn khác |
| `TitleColorMode` property | Default: `NodeColor` |
| `TitleColorKey` property | Default: `null` |
| `NotifyTitleChanged()` method | Gọi `OnPropertyChanged(nameof(Title))` — override nếu cần thêm logic |

### 3.2 Template chuẩn

```csharp
namespace FlowMy.Models.Nodes
{
    // ✅ KHÔNG thêm INotifyPropertyChanged — WorkflowNode đã implement
    // ✅ KHÔNG khai báo lại: PropertyChanged, OnPropertyChanged,
    //    TitleDisplayMode, TitleColorMode, TitleColorKey, NotifyTitleChanged
    public sealed class YourNode : WorkflowNode
    {
        private string _someProperty = string.Empty;
        private int _someCount;

        public YourNode()
        {
            Type = NodeType.YourType;   // thêm vào Models/Nodes/NodeType.cs
            Title = "Your Node";

            // Nếu muốn default TitleDisplayMode khác Always:
            TitleDisplayMode = TitleDisplayMode.Hidden;

            // Thêm ports
            Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = true,  Position = PortPosition.Left,  IsVisible = true, ColorKey = "Info" });
            Ports.Add(new NodePort { Id = Guid.NewGuid().ToString(), IsInput = false, Position = PortPosition.Right, IsVisible = true, ColorKey = "SunsetOrange" });
        }

        // ✅ Properties đặc thù — dùng OnPropertyChanged() từ base
        public string SomeProperty
        {
            get => _someProperty;
            set { if (_someProperty != value) { _someProperty = value; OnPropertyChanged(); } }
        }

        public int SomeCount
        {
            get => _someCount;
            set { if (_someCount != value) { _someCount = value; OnPropertyChanged(); } }
        }

        // ✅ CHỈ override nếu cần thêm logic sau khi title thay đổi
        // public override void NotifyTitleChanged() { base.NotifyTitleChanged(); /* extra */ }

        // ❌ KHÔNG khai báo: PropertyChanged event
        // ❌ KHÔNG khai báo: OnPropertyChanged method
        // ❌ KHÔNG khai báo: TitleDisplayMode property
        // ❌ KHÔNG khai báo: TitleColorMode property
        // ❌ KHÔNG khai báo: TitleColorKey property
        // ❌ KHÔNG khai báo: NotifyTitleChanged() nếu chỉ gọi OnPropertyChanged(nameof(Title))
    }
}
```

### 3.3 Tác động lên Services

Vì `WorkflowNode` có `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey` trực tiếp:

```csharp
// ✅ Persistence — dùng trực tiếp, không cần if node is XxxNode
dict["TitleDisplayMode"] = node.TitleDisplayMode.ToString();
dict["TitleColorMode"]   = node.TitleColorMode.ToString();
if (!string.IsNullOrEmpty(node.TitleColorKey))
    dict["TitleColorKey"] = node.TitleColorKey;

// ✅ PropertyChanged — không cần cast
node.PropertyChanged += (s, e) => { ... };

// ✅ NotifyTitleChanged — gọi trực tiếp
node.NotifyTitleChanged();
```

### 3.4 Checklist Node Model

```yaml
- [ ] Kế thừa WorkflowNode (KHÔNG thêm INotifyPropertyChanged)
- [ ] KHÔNG khai báo lại: PropertyChanged, OnPropertyChanged,
      TitleDisplayMode, TitleColorMode, TitleColorKey, NotifyTitleChanged
- [ ] Thêm NodeType enum value vào Models/Nodes/NodeType.cs
- [ ] Thêm ports trong constructor
- [ ] Dùng OnPropertyChanged() trong mọi setter
- [ ] Nếu muốn default TitleDisplayMode khác Always: set trong constructor
```

---

## Lỗi thường gặp

| # | Lỗi | Nguyên nhân | Cách tránh |
|---|-----|-------------|-----------|
| M1 | Compiler error: ambiguous `PropertyChanged` | Derived class khai báo lại event | Xóa — base đã có |
| M2 | Compiler error: ambiguous `OnPropertyChanged` | Derived class khai báo lại method | Xóa — base đã có |
| M3 | `TitleDisplayMode` luôn là `Always` dù muốn `Hidden` | Quên set trong constructor | Thêm `TitleDisplayMode = TitleDisplayMode.Hidden;` trong constructor |
| M4 | `if node is XxxNode` chain trong service | Không biết base đã có property | Dùng `node.TitleDisplayMode` trực tiếp |
| M5 | `if (node is INotifyPropertyChanged npc)` | Không biết WorkflowNode đã implement | Dùng `node.PropertyChanged +=` trực tiếp |

---

## Reference Implementations

Xem các file sau làm mẫu thực tế:
- `Models/Nodes/OutputNode.cs` - Standard pattern
- `Models/Nodes/StorageNode.cs` - Với properties đặc thù
- `Models/Nodes/DelayNode.cs` - Simple node
