using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Hàng đợi cookie snapshot từ import .webpkg.zip (format v2). WebView2 đầu tiên dùng profile chung sẽ consume và ghi vào cookie store.
/// </summary>
public static class WebCookiePortableBridge
{
    private static readonly object _lock = new();
    private static string? _pending;

    public static void Enqueue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        lock (_lock) { _pending = json; }
    }

    /// <summary>Gọi sau EnsureCoreWebView2, trước Navigate lần đầu.</summary>
    public static Task TryConsumeAndApplyAsync(CoreWebView2CookieManager cookieManager)
    {
        string? json;
        lock (_lock)
        {
            json = _pending;
            _pending = null;
        }

        if (json == null) return Task.CompletedTask;
        return WebCookieSnapshotService.ApplySnapshotJsonAsync(cookieManager, json);
    }
}
