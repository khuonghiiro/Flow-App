// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System.Collections.Concurrent;
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
        List<AsyncDataSource>? AsyncDataSources { get; set; }
        ConcurrentDictionary<string, string> AsyncDataCache { get; }
        ConcurrentQueue<(string SessionId, string Key, string Value)> AsyncDataReplayBuffer { get; }
        ConcurrentQueue<(string SessionId, string Key, string Value)> PendingAsyncPushQueue { get; }
        bool PendingAsyncDataPush { get; set; }
        Dictionary<string, object?> ResolvedOutputs { get; set; }
        List<WorkflowDynamicDataPort> DynamicOutputs { get; }
        bool PendingReadDom { get; set; }
    }
}
