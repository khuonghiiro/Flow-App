namespace FlowMy.Models
{
    public sealed class BreakNode : WorkflowNode
    {
        public BreakNode()
        {
            Type = NodeType.Break;
            Title = "Break";
            // ✅ Break chỉ có 1 flow input, nằm bên trái (đặc biệt dùng trong LoopBody)
            Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
        }
    }
}
