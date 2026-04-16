using System;

namespace FlowMy.Services.Interaction
{
    public interface IWorkflowEditorHostAccessor
    {
        IWorkflowEditorHost? Host { get; set; }

        IWorkflowEditorHost GetRequiredHost()
        {
            return Host ?? throw new InvalidOperationException("Workflow editor host has not been assigned yet.");
        }
    }
}

