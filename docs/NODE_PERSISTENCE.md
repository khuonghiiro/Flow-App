# Persistence — FlowMy Node Creation

> Cập nhật: 2026-06-12
> Phần này giải thích cách thêm serialize/deserialize cho node mới.

---

## 10. Persistence — Kiến trúc Partial Class

`FileWorkflowPersistenceService` sử dụng **partial class** pattern để tách logic persistence thành các file nhỏ theo nhóm node. File chính (`FileWorkflowPersistenceService.cs`) chỉ chứa **dispatcher** — gọi method từ các partial files.

### 10.1 Cấu trúc file

```
Services/Workflow/
├── FileWorkflowPersistenceService.cs          ← Dispatcher (chỉ chứa switch case gọi method)
└── Persistence/
    ├── NodeProperties_Shared.cs               ← Shared props cho TẤT CẢ node (RunMode, ReuseRoutes, DynamicInputs, Title...)
    ├── NodeProperties_Helpers.cs              ← Utility methods (GetStringFromJsonValue, etc.)
    ├── NodeProperties_InputEvents.cs          ← KeyPressEventNode, HotkeyPressEventNode, StringSplitNode
    ├── NodeProperties_LoopAsync.cs            ← LoopNode, LoopBodyNode, AsyncTask*, AsyncTaskDispatchCollect
    ├── NodeProperties_Conditional.cs          ← ConditionalNode branches
    ├── NodeProperties_ScreenCapture.cs        ← ScreenCaptureNode, TextScanNode, ScreenPositionPickerNode
    ├── NodeProperties_Media.cs                ← ImageProcessingNode, VideoProcessingNode, MediaGalleryNode, MouseEventNode
    ├── NodeProperties_DataFlow.cs             ← DataFetcherNode, AssignDataNode, ListOutNode, InputNode, DelayNode, CallbackNode
    ├── NodeProperties_FileFolder.cs           ← FileDownloadNode, FolderFilePathsNode, FolderNode, KeyValueBridgeNode
    ├── NodeProperties_CodeHtml.cs             ← CodeNode, HtmlUiNode
    ├── NodeProperties_WebNode.cs              ← WebNode (BlockingRules, JsSources, ResponseOutputs, etc.)
    └── NodeProperties_Misc.cs                 ← HttpRequestNode, EmbedApplicationNode, StorageNode, OutputNode, MacroRecorderNode, etc.
```

### 10.2 Cách thêm persistence cho node mới

**Bước 1**: Tạo 2 methods trong file partial phù hợp (hoặc tạo file mới nếu không phù hợp nhóm nào):

```csharp
// File: Services/Workflow/Persistence/NodeProperties_Misc.cs (hoặc file phù hợp)
// Namespace + partial class header đã có sẵn

// RESTORE (Deserialize) — khôi phục properties từ Dictionary
private static void RestoreYourNodeProperties(YourNode node, Dictionary<string, object> properties)
{
    if (properties.TryGetValue("SomeProperty", out var sp))
        node.SomeProperty = sp?.ToString() ?? string.Empty;
    if (properties.TryGetValue("SomeCount", out var sc) &&
        int.TryParse(sc?.ToString(), out var count))
        node.SomeCount = count;
}

// GET (Serialize) — lưu properties vào Dictionary
private static void GetYourNodeProperties(YourNode node, Dictionary<string, object> dict)
{
    dict["SomeProperty"] = node.SomeProperty;
    dict["SomeCount"]    = node.SomeCount.ToString();
}
```

**Bước 2**: Thêm dispatch vào `FileWorkflowPersistenceService.cs` — 2 chỗ:

```csharp
// Trong RestoreNodeProperties() — thêm case vào chuỗi switch
case YourNode yourNode:
    RestoreYourNodeProperties(yourNode, properties);
    break;

// Trong GetNodeProperties() — thêm case vào chuỗi switch
case YourNode yourNode:
    GetYourNodeProperties(yourNode, dict);
    break;
```

### 10.3 Quy tắc quan trọng

| Quy tắc | Giải thích |
|---------|-----------|
| **KHÔNG viết inline** | KHÔNG viết `properties.TryGetValue(...)` trực tiếp trong file chính — PHẢI tạo method trong Persistence/ |
| **Shared props tự động** | `RunMode`, `EndBehavior`, `DiamondSharpness`, `ReuseRoutes`, `DynamicInputs`, `TitleDisplayMode/ColorMode` đã được xử lý tự động bởi `NodeProperties_Shared.cs` — **KHÔNG cần viết lại** |
| **Method naming** | `RestoreXxxNodeProperties(node, properties)` và `GetXxxNodeProperties(node, dict)` |
| **Partial class** | Mọi file trong `Persistence/` đều là `public sealed partial class FileWorkflowPersistenceService` |
| **Static methods** | Dùng `private static void` cho tất cả Restore/Get methods |

### 10.4 Template file partial mới

Nếu cần tạo file partial mới cho nhóm node mới:

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreYourNodeProperties(YourNode node, Dictionary<string, object> properties)
    {
        // Deserialize properties đặc thù của node
        if (properties.TryGetValue("SomeProperty", out var sp))
            node.SomeProperty = sp?.ToString() ?? string.Empty;
    }

    // -- GET (Serialize) --

    private static void GetYourNodeProperties(YourNode node, Dictionary<string, object> dict)
    {
        // Serialize properties đặc thù của node
        dict["SomeProperty"] = node.SomeProperty;
    }
}
```

### 10.5 Shared properties (KHÔNG cần viết — đã tự động xử lý)

Các properties sau được `NodeProperties_Shared.cs` xử lý cho **MỌI** node type — **KHÔNG cần** viết trong Restore/Get riêng:

| Property | Restore method | Get method |
|----------|---------------|------------|
| `RunMode`, `AutoRunInterval*`, `AutoScope*` | `RestoreSharedNodeProperties()` | `GetSharedFooterProperties()` |
| `EndBehavior`, `DiamondSharpness`, `ConditionalVisualMode` | `RestoreSharedNodeProperties()` | `GetSharedFooterProperties()` |
| `ReuseRoutes` | `RestoreReuseRoutes()` | `GetReuseRoutes()` |
| `DynamicInputs` (DynIn_*) | `RestoreDynamicInputProperties()` | `GetDynamicInputProperties()` |
| `TitleDisplayMode`, `TitleColorMode`, `TitleColorKey` | `RestoreSharedTitleProperties()` | `GetSharedTitleProperties()` |
| `Condition`, `Key`, `MouseEvent`, `TargetElement`, `FloatingWidget` | switch block trong main | `GetSharedHeaderProperties()` |

### 10.6 Checklist Persistence

```yaml
- [ ] Tạo RestoreYourNodeProperties() trong Persistence/NodeProperties_Xxx.cs phù hợp
- [ ] Tạo GetYourNodeProperties() trong cùng file
- [ ] Thêm else if dispatch vào RestoreNodeProperties() trong file chính
- [ ] Thêm else if dispatch vào GetNodeProperties() trong file chính
- [ ] KHÔNG viết inline code trong file chính
- [ ] KHÔNG serialize lại shared properties (Title, RunMode, ReuseRoutes, DynamicInputs)
- [ ] Serialize TẤT CẢ properties đặc thù — kể cả default values
- [ ] Deserialize dùng TryGetValue — tương thích file cũ không có key
```

---

## 10.7 Copy/Paste

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
