using System.Collections.Generic;

namespace FlowMy.Models.Nodes
{
    public interface ISciterNode
    {
        string Id { get; }
        string HtmlCode { get; set; }
        string CssCode { get; set; }
        string JsCode { get; set; }
        string ParamsCode { get; set; }
        List<CodeInputMapping> InputMappings { get; set; }
        Dictionary<string, object?> ResolvedOutputs { get; set; }
        List<WorkflowDynamicDataPort> DynamicOutputs { get; }
        bool PendingReadDom { get; set; }
    }
}
