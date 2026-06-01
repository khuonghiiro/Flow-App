# Executor — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách tạo file Executor cho node mới.

---

## 9. Executor

Executor xử lý logic thực thi node khi workflow chạy. Chỉ cần tạo nếu node có logic thực thi riêng (không phải node chỉ pass-through).

### 9.1 Interface thực tế

```csharp
public interface INodeExecutor
{
    bool CanExecute(WorkflowNode node);
    Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env);
}
```

`NodeExecutionEnvironment` cung cấp:
- `env.Service` — `WorkflowExecutionService` để resolve values, get connections
- `env.Connections` — tất cả connections trong workflow
- `env.CancellationToken` — để cancel
- `env.ExecutionId` — id duy nhất của lần chạy (dùng cho scoped output)
- `env.ExecuteNextAsync(node, connection)` — chạy node tiếp theo
- `env.TraverseOutputsAsync(node)` — traverse tất cả output connections (dùng cho node thông thường)
- `env.IncomingConnection` — connection đến node này
- `env.OnNodeCompleted` / `env.OnNodeFailed` — callbacks

### 9.2 Template chuẩn

```csharp
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow.NodeExecutors;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class YourNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is YourNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var yourNode = (YourNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 1. Lấy input value từ node nguồn
                var inputValue = env.Service.ResolveDynamicValueForExecution(
                    sourceNode, "outputKey", env);

                // 2. Thực thi logic
                var result = DoYourLogic(yourNode, inputValue);

                // 3. Lưu output vào node (để downstream đọc)
                yourNode.SomeOutputProperty = result;

                // 4. Publish vào scoped store (bắt buộc nếu node có DynamicOutputs)
                if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                {
                    env.Service.SetScopedNodeStringOutput(
                        env.ExecutionId, yourNode.Id, "outputKey", result);
                }
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(yourNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(yourNode, sw.Elapsed);

            // 5. Traverse sang node tiếp theo
            await env.TraverseOutputsAsync(yourNode);
        }
    }
}
```

### 9.3 Resolve input từ node nguồn

```csharp
// Lấy node nguồn qua incoming connection
var sourceNode = env.IncomingConnection?.FromNode;
if (sourceNode != null)
{
    var value = env.Service.ResolveDynamicValueForExecution(sourceNode, "outputKey", env);
}

// Hoặc lấy theo nodeId đã cấu hình trong node
var value = env.Service.ResolveValueByNodeIdAndKeyForExecution(
    env.Connections, yourNode.SourceNodeId, yourNode.SourceOutputKey, env);
```

### 9.4 Đăng ký Executor

Thêm vào list `_nodeExecutors` trong constructor của `WorkflowExecutionService` (`Services/Workflow/WorkflowExecutionService.cs`):

```csharp
_nodeExecutors = new List<NodeExecutors.INodeExecutor>
{
    // ... existing executors ...
    new NodeExecutors.YourNodeExecutor(),  // ← thêm vào đây
};
```

### 9.5 Checklist Executor

```yaml
- [ ] Implement INodeExecutor (CanExecute + ExecuteAsync)
- [ ] CanExecute: return node is YourNode
- [ ] ExecuteAsync: dùng NodeExecutionEnvironment (không phải WorkflowExecutionContext)
- [ ] Dùng env.Service.ResolveDynamicValueForExecution() để lấy input
- [ ] Gọi env.OnNodeCompleted?.Invoke() sau khi xong
- [ ] Gọi env.OnNodeFailed?.Invoke() + throw khi có lỗi
- [ ] Gọi env.TraverseOutputsAsync(node) ở cuối để đi tiếp
- [ ] Đăng ký vào _nodeExecutors list trong WorkflowExecutionService constructor
- [ ] KHÔNG tạo NodeExecutorFactory riêng — dùng list trong WorkflowExecutionService
```

---

## Reference Implementations

Xem các file sau làm mẫu thực tế:
- `Services/Workflow/NodeExecutors/StorageNodeExecutor.cs` - Executor mẫu với scoped output
- `Services/Workflow/NodeExecutors/DelayNodeExecutor.cs` - Executor đơn giản
