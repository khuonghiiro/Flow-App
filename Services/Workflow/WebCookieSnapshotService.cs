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

    public static async Task<string> ExportSnapshotJsonAsync(IReadOnlyCollection<WorkflowNode> nodes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lookupUris = CollectCookieLookupUris(nodes);
        var entries = new List<PortableCookieEntryDto>();

        var profilesToExport = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                // Shared hoặc không set → dùng profile Shared
                profilesToExport.Add("Shared");
            }
        }

        foreach (var profileName in profilesToExport)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ICookieManager cookieManager;
                if (string.Equals(profileName, "Shared", StringComparison.OrdinalIgnoreCase))
                {
                    cookieManager = Cef.GetGlobalCookieManager();
                }
                else
                {
                    var rc = CefSharpEnvironmentManager.CreateProfileRequestContext(profileName);
                    cookieManager = rc.GetCookieManager(null);
                }

                if (cookieManager == null) continue;

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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WebCookieSnapshotService.VisitUrlCookiesAsync({uri}, profile '{profileName}'): {ex.Message}");
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

        try
        {
            foreach (var entry in dto.Entries)
            {
                if (entry.Cookies == null) continue;

                if (profileName != null)
                {
                    var entryProfile = string.IsNullOrWhiteSpace(entry.Profile) ? "Shared" : entry.Profile.Trim();
                    if (!string.Equals(entryProfile, profileName, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

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
