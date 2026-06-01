# Persistence — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách thêm serialize/deserialize cho node mới.

---

## 10. Persistence

Thêm serialize/deserialize trong `FileWorkflowPersistenceService`:

```csharp
// SERIALIZE — trong SerializeNode(node, dict)
case NodeType.YourType:
    var yourNode = (YourNode)node;
    dict["SomeProperty"] = yourNode.SomeProperty;
    dict["SomeCount"]    = yourNode.SomeCount.ToString();
    // Title properties — dùng trực tiếp (không cần if-is chain)
    dict["TitleDisplayMode"] = node.TitleDisplayMode.ToString();
    dict["TitleColorMode"]   = node.TitleColorMode.ToString();
    if (!string.IsNullOrEmpty(node.TitleColorKey))
        dict["TitleColorKey"] = node.TitleColorKey;
    break;

// DESERIALIZE — trong DeserializeNode(type, properties)
case NodeType.YourType:
    var yourNode = new YourNode();
    if (properties.TryGetValue("SomeProperty", out var sp))
        yourNode.SomeProperty = sp?.ToString() ?? string.Empty;
    if (properties.TryGetValue("SomeCount", out var sc) &&
        int.TryParse(sc?.ToString(), out var count))
        yourNode.SomeCount = count;
    // Title properties — dùng trực tiếp
    if (properties.TryGetValue("TitleDisplayMode", out var tdm) &&
        Enum.TryParse<TitleDisplayMode>(tdm?.ToString(), out var tdmVal))
        yourNode.TitleDisplayMode = tdmVal;
    if (properties.TryGetValue("TitleColorMode", out var tcm) &&
        Enum.TryParse<TitleColorMode>(tcm?.ToString(), out var tcmVal))
        yourNode.TitleColorMode = tcmVal;
    if (properties.TryGetValue("TitleColorKey", out var tck))
        yourNode.TitleColorKey = tck?.ToString();
    return yourNode;
```

**Lưu ý**: Serialize **tất cả** properties — kể cả những gì có default value. Deserialize phải dùng `TryGetValue` để tương thích với file cũ không có key đó.

---

## 10.5 Copy/Paste

Node phải hỗ trợ copy/paste. Logic nằm trong `WorkflowEditorWindow.NodeActions.cs` — method `CreateDuplicateNodeInstance`.

Thêm một `else if` branch cho node mới vào method này:

```csharp
else if (source is YourNode srcYour && node is YourNode dstYour)
{
    // Copy tất cả properties đặc thù của node
    dstYour.SomeProperty = srcYour.SomeProperty;
    dstYour.SomeCount    = srcYour.SomeCount;

    // Copy title properties (bắt buộc cho mọi node)
    dstYour.TitleDisplayMode = srcYour.TitleDisplayMode;
    dstYour.TitleColorMode   = srcYour.TitleColorMode;
    dstYour.TitleColorKey    = srcYour.TitleColorKey;

    // Notify để renderer update UI
    dstYour.NotifyTitleChanged();
}
```

**Lưu ý**: Nếu node có properties chứa reference types (List, Dictionary, object), phải deep copy — không gán trực tiếp để tránh shared reference giữa node gốc và bản sao.

```csharp
// ❌ SAI — shared reference
dstYour.Items = srcYour.Items;

// ✅ ĐÚNG — deep copy
dstYour.Items = srcYour.Items.Select(i => new YourItem { Key = i.Key, Value = i.Value }).ToList();
```
