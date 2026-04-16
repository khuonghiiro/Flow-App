namespace FlowMy.Services.Interaction
{
    public sealed class WorkflowEditorHostAccessor : IWorkflowEditorHostAccessor
    {
        public IWorkflowEditorHost? Host { get; set; }
    }
}

