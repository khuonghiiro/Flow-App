// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using FlowMy.Models;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Export/apply cookie snapshot cho gói portable dùng CefSharp ICookieManager.
/// </summary>
public static class WebCookieSnapshotService
{
    public const int FormatVersion = 3;

    private sealed class PortableCookieBundleDto
    {
        [JsonPropertyName("format")]
        public int Format { get; set; }

        [JsonPropertyName("entries")]
        public List<PortableCookieEntryDto>? Entries { get; set; }
    }

    private sealed class PortableCookieEntryDto
    {
        [JsonPropertyName("requestUri")]
        public string? RequestUri { get; set; }

        [JsonPropertyName("profile")]
        public string? Profile { get; set; }

        [JsonPropertyName("cookies")]
        public List<PortableCookieItemDto>? Cookies { get; set; }
    }

    private sealed class PortableCookieItemDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("domain")]
        public string? Domain { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("secure")]
        public bool Secure { get; set; }

        [JsonPropertyName("httpOnly")]
        public bool HttpOnly { get; set; }

        [JsonPropertyName("sameSite")]
        public int? SameSite { get; set; }

        [JsonPropertyName("expires")]
        public string? Expires { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task<string> ExportSnapshotJsonAsync(
        IReadOnlyCollection<WorkflowNode> nodes,
        CancellationToken cancellationToken,
        string? selectedProfile = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lookupUris = CollectCookieLookupUris(nodes);
        var entries = new List<PortableCookieEntryDto>();

        var profilesToExport = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(selectedProfile) &&
            !selectedProfile.Equals("All", StringComparison.OrdinalIgnoreCase) &&
            !selectedProfile.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            profilesToExport.Add(selectedProfile.Trim());
        }
        else
        {
            foreach (var w in nodes.OfType<WebNode>())
            {
                var mode = w.CacheMode ?? "Shared";
                if (string.Equals(mode, "Isolated", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(w.CustomCacheName))
                {
                    profilesToExport.Add(w.CustomCacheName.Trim());
                }
                else
                {
                    profilesToExport.Add("Shared");
                }
            }

            foreach (var h in nodes.OfType<HtmlUiNode>())
            {
                if (h.UseWebTab)
                    profilesToExport.Add("Shared");
            }

            var diskProfiles = WebNodeCacheHelper.GetAvailableCacheProfiles();
            foreach (var p in diskProfiles)
            {
                if (!string.IsNullOrWhiteSpace(p))
                    profilesToExport.Add(p.Trim());
            }

            if (profilesToExport.Count == 0)
            {
                profilesToExport.Add("Shared");
            }
        }

        foreach (var profileName in profilesToExport)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var rc = CefSharpEnvironmentManager.CreateProfileRequestContext(profileName);
                ICookieManager? cookieManager = rc?.GetCookieManager(null);

                if (string.Equals(profileName, "Shared", StringComparison.OrdinalIgnoreCase) || rc == null)
                {
                    cookieManager ??= Cef.GetGlobalCookieManager();
                }

                if (cookieManager == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[CookieExport] CookieManager returned null for profile '{profileName}'");
                    continue;
                }

                // ── FlushStoreAsync to write live in-memory cookies to storage ──
                try
                {
                    await cookieManager.FlushStoreAsync();
                }
                catch { }

                // ── VisitAllCookiesAsync to capture ALL cookies in this profile ──
                var seenKeys = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    var allCookies = await cookieManager.VisitAllCookiesAsync();

                    // Fallback to Cef.GetGlobalCookieManager() if Shared returned 0
                    if ((allCookies == null || allCookies.Count == 0) && string.Equals(profileName, "Shared", StringComparison.OrdinalIgnoreCase))
                    {
                        var globalMgr = Cef.GetGlobalCookieManager();
                        if (globalMgr != null && globalMgr != cookieManager)
                        {
                            try { await globalMgr.FlushStoreAsync(); } catch { }
                            allCookies = await globalMgr.VisitAllCookiesAsync();
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[CookieExport] Profile '{profileName}': VisitAllCookiesAsync returned {allCookies?.Count ?? 0} cookies");

                    if (allCookies != null && allCookies.Count > 0)
                    {
                        // Group cookies by domain origin for organized entries
                        var byDomain = new Dictionary<string, List<PortableCookieItemDto>>(StringComparer.OrdinalIgnoreCase);
                        foreach (var c in allCookies)
                        {
                            var item = SerializeCookie(c);
                            if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Domain))
                                continue;

                            var key = $"{item.Name}|{item.Domain}|{item.Path}";
                            if (!seenKeys.Add(key)) continue;

                            var domainKey = item.Domain!.TrimStart('.');
                            if (!byDomain.TryGetValue(domainKey, out var domainList))
                            {
                                domainList = new List<PortableCookieItemDto>();
                                byDomain[domainKey] = domainList;
                            }
                            domainList.Add(item);
                        }

                        foreach (var (domain, cookieList) in byDomain)
                        {
                            if (cookieList.Count > 0)
                            {
                                entries.Add(new PortableCookieEntryDto
                                {
                                    RequestUri = $"https://{domain}/",
                                    Profile = profileName,
                                    Cookies = cookieList
                                });
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"[CookieExport] Profile '{profileName}': exported {seenKeys.Count} unique cookies");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebCookieSnapshotService.VisitAllCookiesAsync(profile '{profileName}'): {ex.Message}");

                    // Fallback: try per-URL approach if VisitAllCookiesAsync fails
                    foreach (var uri in lookupUris)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var cookies = await cookieManager.VisitUrlCookiesAsync(uri, includeHttpOnly: true);
                            if (cookies != null && cookies.Count > 0)
                            {
                                var list = cookies.Select(SerializeCookie)
                                    .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Domain))
                                    .Where(c => seenKeys.Add($"{c.Name}|{c.Domain}|{c.Path}"))
                                    .ToList();

                                if (list.Count > 0)
                                {
                                    entries.Add(new PortableCookieEntryDto
                                    {
                                        RequestUri = uri,
                                        Profile = profileName,
                                        Cookies = list
                                    });
                                }
                            }
                        }
                        catch (Exception urlEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"WebCookieSnapshotService.VisitUrlCookiesAsync({uri}, profile '{profileName}'): {urlEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting cookies for profile '{profileName}': {ex.Message}");
            }
        }

        var dto = new PortableCookieBundleDto { Format = FormatVersion, Entries = entries };
        return JsonSerializer.Serialize(dto, JsonOpts);
    }

    /// <summary>
    /// Nhập cookie snapshot cho tất cả các profile có trong JSON.
    /// Nếu trùng profile: ghi đè cookie.
    /// Nếu không trùng (chưa có profile): tự động tạo mới profile đó trên đĩa.
    /// Thực hiện TRƯỚC KHI load workflow nodes.
    /// </summary>
    public static async Task ImportSnapshotJsonAllProfilesAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        PortableCookieBundleDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PortableCookieBundleDto>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ImportSnapshotJsonAllProfilesAsync parse error: {ex.Message}");
            return;
        }

        if (dto == null || !IsSupportedFormatVersion(dto.Format) || dto.Entries == null || dto.Entries.Count == 0)
            return;

        var profileGroups = dto.Entries
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Profile) ? "Shared" : e.Profile.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in profileGroups)
        {
            var pName = group.Key;

            // 1. Tạo mới profile trên đĩa nếu chưa có (hoặc trùng thì đảm bảo tồn tại)
            WebNodeCacheHelper.EnsureProfileExists(pName);

            // 2. Lấy CookieManager của đúng profile đó
            var rc = CefSharpEnvironmentManager.CreateProfileRequestContext(pName);
            var cookieManager = rc?.GetCookieManager(null);
            if (pName.Equals("Shared", StringComparison.OrdinalIgnoreCase) || rc == null)
            {
                cookieManager ??= Cef.GetGlobalCookieManager();
            }

            if (cookieManager == null) continue;

            // 3. Ghi đè / Nạp cookie của profile này
            foreach (var entry in group)
            {
                if (entry.Cookies == null) continue;
                foreach (var c in entry.Cookies)
                {
                    var name = c.Name?.Trim();
                    var domain = c.Domain?.Trim();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(domain)) continue;

                    var path = string.IsNullOrWhiteSpace(c.Path) ? "/" : c.Path.Trim();
                    var cookieUrl = $"https://{domain.TrimStart('.')}{path}";

                    DateTime? expires = null;
                    if (!string.IsNullOrWhiteSpace(c.Expires) &&
                        DateTime.TryParse(c.Expires, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
                    {
                        expires = exp;
                    }

                    var cookie = new Cookie
                    {
                        Name = name,
                        Value = c.Value ?? string.Empty,
                        Domain = domain,
                        Path = path,
                        Secure = c.Secure,
                        HttpOnly = c.HttpOnly,
                        Expires = expires
                    };

                    await cookieManager.SetCookieAsync(cookieUrl, cookie);
                }
            }

            try { await cookieManager.FlushStoreAsync(); } catch { }
        }

        WebNodeCacheHelper.NotifyProfilesChanged();
    }

    public static Task ApplySnapshotJsonAsync(ICookieManager mgr, string json)
    {
        return ApplySnapshotJsonForProfileAsync(mgr, json, profileName: null);
    }

    public static async Task ApplySnapshotJsonForProfileAsync(ICookieManager mgr, string json, string? profileName)
    {
        if (mgr == null || string.IsNullOrWhiteSpace(json)) return;

        PortableCookieBundleDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PortableCookieBundleDto>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebCookieSnapshotService.Apply: parse error: {ex.Message}");
            return;
        }

        if (dto == null || !IsSupportedFormatVersion(dto.Format) || dto.Entries == null || dto.Entries.Count == 0)
            return;

        var reqProfile = string.IsNullOrWhiteSpace(profileName) ? "Shared" : profileName.Trim();

        var matchingEntries = dto.Entries
            .Where(e => string.Equals(string.IsNullOrWhiteSpace(e.Profile) ? "Shared" : e.Profile.Trim(), reqProfile, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingEntries.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var entry in matchingEntries)
            {
                if (entry.Cookies == null) continue;

                foreach (var c in entry.Cookies)
                {
                    var name = c.Name?.Trim();
                    var domain = c.Domain?.Trim();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(domain)) continue;

                    var path = string.IsNullOrWhiteSpace(c.Path) ? "/" : c.Path.Trim();
                    var cookieUrl = $"https://{domain.TrimStart('.')}{path}";

                    DateTime? expires = null;
                    if (!string.IsNullOrWhiteSpace(c.Expires) &&
                        DateTime.TryParse(c.Expires, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
                    {
                        expires = exp;
                    }

                    var cookie = new Cookie
                    {
                        Name = name,
                        Value = c.Value ?? string.Empty,
                        Domain = domain,
                        Path = path,
                        Secure = c.Secure,
                        HttpOnly = c.HttpOnly,
                        Expires = expires
                    };

                    await mgr.SetCookieAsync(cookieUrl, cookie);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebCookieSnapshotService.Apply: {ex.Message}");
        }
    }

    private static bool IsSupportedFormatVersion(int version) => version == 2 || version == 3;

    public static bool IsV2PortableCookieBundleJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("format", out var f) || f.ValueKind != JsonValueKind.Number)
                return false;
            return IsSupportedFormatVersion(f.GetInt32());
        }
        catch
        {
            return false;
        }
    }

    public static List<string> CollectCookieLookupUris(IEnumerable<WorkflowNode> nodes)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddUrl(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            var t = s.Trim();
            if (!t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return;
            if (!Uri.TryCreate(t, UriKind.Absolute, out var u) ||
                (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
                return;

            set.Add(u.ToString());
            var origin = $"{u.Scheme}://{u.Authority}/";
            set.Add(origin);
        }

        foreach (var n in nodes)
        {
            switch (n)
            {
                case WebNode w:
                    AddUrl(w.ExtractUrl);
                    if (w.ResponseOutputs != null)
                    {
                        foreach (var ro in w.ResponseOutputs)
                            AddUrl(ro?.Url);
                    }
                    break;
                case HtmlUiNode h when h.UseWebTab:
                    AddUrl(h.WebTabUrl);
                    break;
            }
        }

        return set.ToList();
    }

    private static PortableCookieItemDto SerializeCookie(Cookie c)
    {
        string? expiresIso = null;
        try
        {
            if (c.Expires.HasValue && c.Expires.Value != DateTime.MinValue)
                expiresIso = c.Expires.Value.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch { /* ignore */ }

        return new PortableCookieItemDto
        {
            Name = c.Name,
            Value = c.Value,
            Domain = c.Domain,
            Path = string.IsNullOrEmpty(c.Path) ? "/" : c.Path,
            Secure = c.Secure,
            HttpOnly = c.HttpOnly,
            SameSite = (int)c.SameSite,
            Expires = expiresIso
        };
    }
}
