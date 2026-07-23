using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Hàng đợi cookie snapshot từ import .webpkg.zip (format v2/v3).
/// Mỗi profile WebView2 sẽ consume đúng phần cookie của mình khi khởi tạo.
/// </summary>
public static class WebCookiePortableBridge
{
    private static readonly object _lock = new();

    /// <summary>Toàn bộ JSON gốc (dùng cho các WebView2 consume theo profile).</summary>
    private static string? _pendingJson;

    /// <summary>Danh sách profile đã consume (tránh apply trùng khi nhiều node cùng profile).</summary>
    private static readonly HashSet<string> _consumedProfiles = new(System.StringComparer.OrdinalIgnoreCase);

    public static void Enqueue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        lock (_lock)
        {
            _pendingJson = json;
            _consumedProfiles.Clear();
        }
    }

    /// <summary>
    /// Gọi sau EnsureCoreWebView2, trước Navigate lần đầu.
    /// Chỉ apply cookie entries thuộc <paramref name="profileName"/> (mặc định "Shared").
    /// </summary>
    public static Task TryConsumeAndApplyAsync(CoreWebView2CookieManager cookieManager, string profileName = "Shared")
    {
        string? json;
        lock (_lock)
        {
            if (_pendingJson == null) return Task.CompletedTask;

            var pn = string.IsNullOrWhiteSpace(profileName) ? "Shared" : profileName.Trim();

            // Nếu profile này đã consume rồi, skip
            if (_consumedProfiles.Contains(pn)) return Task.CompletedTask;

            json = _pendingJson;
            _consumedProfiles.Add(pn);
        }

        if (json == null) return Task.CompletedTask;
        var effectiveProfile = string.IsNullOrWhiteSpace(profileName) ? "Shared" : profileName.Trim();
        return WebCookieSnapshotService.ApplySnapshotJsonForProfileAsync(cookieManager, json, effectiveProfile);
    }

    /// <summary>Xóa toàn bộ pending (gọi khi workflow đã load xong hoàn toàn).</summary>
    public static void ClearAll()
    {
        lock (_lock)
        {
            _pendingJson = null;
            _consumedProfiles.Clear();
        }
    }
}
