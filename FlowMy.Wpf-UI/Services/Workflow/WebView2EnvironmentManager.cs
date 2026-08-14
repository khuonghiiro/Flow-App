// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Forwarding compatibility stub for CefSharp.
/// </summary>
public static class WebView2EnvironmentManager
{
    public static Task WarmUpAsync() => CefSharpEnvironmentManager.InitializeAsync();
}

