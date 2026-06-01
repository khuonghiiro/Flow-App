# Renderer — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách tạo file Renderer cho node mới.

---

## 8. Renderer

Renderer là class thực sự đặt node lên canvas. Interface thực tế có 4 methods:

```csharp
public interface INodeRenderer
{
    void RenderNode(WorkflowNode node, Canvas canvas);
    void UpdateNodePosition(WorkflowNode node, double x, double y);
    void RemoveNode(WorkflowNode node, Canvas canvas);
    void RemoveAllNodeVisuals(Canvas canvas);
}
```

### 8.1 Template chuẩn

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class YourNodeRenderer : INodeRenderer
    {
        private readonly PortRenderer _portRenderer;
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private IWorkflowEditorHost Host => _hostAccessor.GetRequiredHost();

        public YourNodeRenderer(PortRenderer portRenderer, IWorkflowEditorHostAccessor hostAccessor)
        {
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is not YourNode yourNode) return;

            // 1. Tạo border từ NodeControl
            yourNode.Border = YourNodeControl.CreateBorder(
                yourNode,
                Host as Window ?? throw new InvalidOperationException("Host must be a Window."),
                Host);
            yourNode.Border.Tag = yourNode;

            // 2. Apply chrome (execution badge, GPU optimization)
            NodeChrome.Apply(yourNode.Border, yourNode, Host);

            // 3. Attach mouse handlers
            yourNode.Border.MouseDown  += Host.NodeMouseDown;
            yourNode.Border.MouseMove  += Host.NodeMouseMove;
            yourNode.Border.MouseUp    += Host.NodeMouseUp;
            yourNode.Border.MouseEnter += Host.NodeBorderMouseEnter;
            yourNode.Border.MouseLeave += Host.NodeBorderMouseLeave;
            // ⚠️ QUAN TRỌNG: set null vì dialog dùng right-click (WithDialogSupport)
            // Nếu set ContextMenu, WPF sẽ ưu tiên mở menu thay vì dialog
            yourNode.Border.ContextMenu = null;

            // 4. Đặt vị trí và thêm vào canvas
            Canvas.SetLeft(yourNode.Border, yourNode.X);
            Canvas.SetTop(yourNode.Border, yourNode.Y);
            canvas.Children.Add(yourNode.Border);
            Host.ZIndexManager.InitializeNodeZIndex(yourNode, yourNode.Border);

            // 5. Render ports
            foreach (var port in yourNode.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);

                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is Ellipse ellipse)
                {
                    // ⚠️ CRITICAL: ALWAYS update color
                    ellipse.Fill = new SolidColorBrush(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(yourNode, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(yourNode, port.PortUI);
            }
        }

        public void UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            node.X = x;
            node.Y = y;

            if (node.Border != null)
            {
                Canvas.SetLeft(node.Border, x);
                Canvas.SetTop(node.Border, y);
            }

            // ⚠️ CRITICAL: Update title TextBlock position
            if (node is YourNode yourNode && yourNode.TitleTextBlockUI != null && Host.WorkflowCanvas != null)
            {
                var title = yourNode.TitleTextBlockUI;
                if (!Host.WorkflowCanvas.Children.Contains(title))
                {
                    Host.WorkflowCanvas.Children.Add(title);
                    Panel.SetZIndex(title, 20000);
                }
                if (node.Border != null)
                {
                    if (title.ActualWidth == 0 || title.ActualHeight == 0)
                    {
                        title.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        title.Arrange(new Rect(title.DesiredSize));
                    }
                    Canvas.SetLeft(title, x + (node.Border.ActualWidth / 2) - (title.ActualWidth / 2));
                    Canvas.SetTop(title, y - title.ActualHeight - 4);
                }
            }

            // Update ports
            foreach (var port in node.Ports.Where(p => p.IsVisible))
            {
                var portColor = ResolvePortColor(port);
                if (port.PortUI == null)
                {
                    port.PortUI = _portRenderer.CreatePort(portColor);
                    port.PortUI.Tag = port;
                }
                else if (port.PortUI is Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(portColor);
                }
                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                Host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            Host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            // ⚠️ CRITICAL: Remove title TextBlock và clear reference
            if (node is YourNode yourNode && yourNode.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(yourNode.TitleTextBlockUI))
                    canvas.Children.Remove(yourNode.TitleTextBlockUI);
                yourNode.TitleTextBlockUI = null;
            }

            if (node.Border != null && canvas.Children.Contains(node.Border))
                canvas.Children.Remove(node.Border);

            foreach (var port in node.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    canvas.Children.Remove(port.PortUI);
            }
        }

        public void RemoveAllNodeVisuals(Canvas canvas)
        {
            var borders = canvas.Children.OfType<Border>()
                .Where(b => b.Tag is WorkflowNode).ToList();
            foreach (var b in borders) canvas.Children.Remove(b);

            var ports = canvas.Children.OfType<Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18)).ToList();
            foreach (var p in ports) canvas.Children.Remove(p);
        }

        private static Color ResolvePortColor(NodePort port)
        {
            if (!string.IsNullOrWhiteSpace(port.ColorKey))
            {
                var c = GetColorFromTheme($"{port.ColorKey}Brush") ?? GetColorFromTheme(port.ColorKey);
                if (c.HasValue) return c.Value;
            }
            return port.IsInput
                ? (GetColorFromTheme("InfoBrush") ?? Colors.Orange)
                : (GetColorFromTheme("SunsetOrangeBrush") ?? Colors.Cyan);
        }

        private static Color? GetColorFromTheme(string key)
        {
            try { return (Application.Current.TryFindResource(key) as SolidColorBrush)?.Color; }
            catch { return null; }
        }
    }
}
```

### 8.2 Đăng ký Renderer

Renderer dùng DI — phải đăng ký ở **2 chỗ**:

**Bước 1**: Thêm field + constructor parameter vào `NodeRenderer` (`Services/Rendering/_NodeRenderer.cs`):

```csharp
// Thêm field
private readonly YourNodeRenderer _yourNodeRenderer;

// Thêm vào constructor parameter list
YourNodeRenderer yourNodeRenderer,

// Thêm vào constructor body
_yourNodeRenderer = yourNodeRenderer ?? throw new ArgumentNullException(nameof(yourNodeRenderer));
```

**Bước 2**: Thêm `if` branch vào `NodeRenderer.RenderNode()` (trước đoạn fallback cuối):

```csharp
if (node is YourNode yourNode)
{
    _yourNodeRenderer.RenderNode(yourNode, canvas);
    return;
}
```

Tương tự thêm branch vào `UpdateNodePosition()` và `RemoveNode()`.

**Bước 3**: Đăng ký trong DI container (thường là `App.xaml.cs` hoặc `ServiceRegistration.cs`):

```csharp
services.AddSingleton<YourNodeRenderer>();
```

### 8.3 Checklist Renderer

```yaml
- [ ] Implement INodeRenderer (4 methods: RenderNode, UpdateNodePosition, RemoveNode, RemoveAllNodeVisuals)
- [ ] Constructor nhận PortRenderer + IWorkflowEditorHostAccessor (DI)
- [ ] RenderNode: gọi NodeChrome.Apply() sau CreateBorder()
- [ ] RenderNode: attach 5 mouse handlers (MouseDown/Move/Up/Enter/Leave)
- [ ] RenderNode: set ContextMenu = null (nếu dùng right-click dialog)
- [ ] RenderNode: gọi ZIndexManager.InitializeNodeZIndex()
- [ ] RenderNode: render ports với màu từ port.ColorKey
- [ ] UpdateNodePosition: update cả border, title TextBlock, ports
- [ ] UpdateNodePosition: gọi Host.SyncAllPortsZIndex(node) ở cuối
- [ ] RemoveNode: remove title TextBlock + set null để tránh memory leak
- [ ] RemoveNode: remove border + tất cả ports
- [ ] Đăng ký vào NodeRenderer constructor + RenderNode/UpdateNodePosition/RemoveNode branches
- [ ] Đăng ký DI container
```

---

## Reference Implementations

Xem các file sau làm mẫu thực tế:
- `Services/Rendering/StorageNodeRenderer.cs` - Renderer mẫu chuẩn
