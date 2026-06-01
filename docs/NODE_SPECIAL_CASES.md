# Các Trường hợp Đặc biệt — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích các trường hợp đặc biệt khi tạo node.

---

## 12. Các trường hợp đặc biệt

### 12.1 Node có embedded title (không dùng floating canvas title)

Ví dụ: `ScreenCaptureNodeControl` — title nằm trong border, không phải TextBlock riêng trên Canvas.

```csharp
// Dùng dummy TextBlock thay vì title thật
var dummyTitle = new TextBlock { Visibility = Visibility.Collapsed, IsHitTestVisible = false };

BaseNodeControlHelper
    .Initialize(border, dummyTitle, node, host)
    .WithHoverBehavior()
    .WithKeyboardPorts()
    .WithPropertySync(customPropertyHandlers)
    .WithDialogSupport(ctx => new YourNodeDialog(node, host, ownerWindow))
    .WithCleanup()
    // ⚠️ KHÔNG gọi WithTitleManagement(), WithVisibilitySync(), WithCanvasIntegration()
    .Build();
```

### 12.2 Node cần cleanup thêm (WebView2, subscriptions)

```csharp
BaseNodeControlHelper
    .Initialize(border, titleTextBlock, node, host)
    // ... fluent chain ...
    .WithCleanup()  // BaseNodeControlHelper cleanup chạy trước
    .Build();

// Cleanup bổ sung sau Build()
border.Unloaded += (s, e) =>
{
    webView?.Dispose();
    // unsubscribe other events...
};
```

### 12.3 Node có resize handles

Giữ resize handle logic trong NodeControl class — đây là logic đặc thù của node.

```csharp
// Sau Build(), thêm resize logic
AttachResizeHandles(border, node, host);
```

### 12.4 Node diamond/conditional (transparent background)

Diamond cần transparent background đặc biệt — không dùng `WithHoverBehavior()` vì nó sẽ set background khi hover.

```csharp
BaseNodeControlHelper
    .Initialize(border, titleTextBlock, node, host)
    .WithDialogSupport(ctx => new ConditionalNodeDialog(node, host, ownerWindow))
    .WithCleanup()
    .WithCanvasIntegration()
    .Build();
// Hover + keyboard được xử lý thủ công để giữ transparent background
```

### 12.5 Dialog không có Inputs/Outputs

```csharp
protected override Panel? GetInputsPanel() => null;
protected override Panel? GetOutputsPanel() => null;
// Base tự bỏ qua khi trả về null
```

### 12.6 Node không dùng ReuseRoutes

```csharp
// Trong ViewModel — chỉ cần 1 dòng này
protected override bool SupportsReuseRoutes => false;
// KHÔNG cần override LoadReuseRoutes()
```

### 12.7 Files KHÔNG dùng BaseNodeControlHelper

Các file sau là container/content controls — **không migrate**, giữ nguyên:

| File | Lý do |
|------|-------|
| `BodyContainerControl.cs` | Container cho loop/conditional body |
| `LoopContainerControl.cs` | Container cho loop body |
| `ImageProcessingNodeContentControl.xaml.cs` | XAML UserControl code-behind |
| `VideoProcessingNodeContentControl.cs` | Content control |
