namespace FlowMy.Models
{
    public sealed class ContinueNode : WorkflowNode
    {
        public ContinueNode()
        {
            Type = NodeType.Continue;
            Title = "Continue";
            // ✅ Continue chỉ có 1 flow input, nằm bên trái (đặc biệt dùng trong LoopBody)
            Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
        }
    }
}