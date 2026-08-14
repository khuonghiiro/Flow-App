namespace FlowMy.Models
{
    /// <summary>
    /// Node điều kiện (If-Else, Switch, ...)
    /// </summary>
    public class ConditionalNode : WorkflowNode
    {
        public ConditionalNode()
        {
            Type = NodeType.IfElse;
        }

        public override bool IsConditionalNode => true;
    }
}

