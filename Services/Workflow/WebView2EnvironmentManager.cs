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

