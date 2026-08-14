// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System.Collections.Generic;
using System.Threading.Tasks;
using CefSharp;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Hàng đợi cookie snapshot từ import .webpkg.zip cho CefSharp.
/// </summary>
public static class WebCookiePortableBridge
{
    private static readonly object _lock = new();
    private static string? _pendingJson;
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

    public static Task TryConsumeAndApplyAsync(ICookieManager cookieManager, string profileName = "Shared")
    {
        string? json;
        lock (_lock)
        {
            if (_pendingJson == null) return Task.CompletedTask;

            var pn = string.IsNullOrWhiteSpace(profileName) ? "Shared" : profileName.Trim();
            if (_consumedProfiles.Contains(pn)) return Task.CompletedTask;

            json = _pendingJson;
            _consumedProfiles.Add(pn);
        }

        if (json == null) return Task.CompletedTask;
        var effectiveProfile = string.IsNullOrWhiteSpace(profileName) ? "Shared" : profileName.Trim();
        return WebCookieSnapshotService.ApplySnapshotJsonForProfileAsync(cookieManager, json, effectiveProfile);
    }

    public static void ClearAll()
    {
        lock (_lock)
        {
            _pendingJson = null;
            _consumedProfiles.Clear();
        }
    }
}
