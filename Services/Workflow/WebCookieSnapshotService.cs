using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Export/apply cookie snapshot cho gói portable (format 2 — file cookies.json nhỏ, không copy cả profile WebView2).
/// </summary>
public static class WebCookieSnapshotService
{
    public const int FormatVersion = 2;

    private static readonly SemaphoreSlim ExportWebViewGate = new(1, 1);

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

        /// <summary>ISO 8601 hoặc bỏ trống = session.</summary>
        [JsonPropertyName("expires")]
        public string? Expires { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>Phải gọi trên UI thread (WebView2 WPF).</summary>
    public static async Task<string> ExportSnapshotJsonAsync(IReadOnlyCollection<WorkflowNode> nodes, CancellationToken cancellationToken)
    {
        if (Application.Current?.Dispatcher.CheckAccess() != true)
            throw new InvalidOperationException("ExportSnapshotJsonAsync must run on the WPF UI thread.");

        cancellationToken.ThrowIfCancellationRequested();
        var lookupUris = CollectCookieLookupUris(nodes);
        var entries = new List<PortableCookieEntryDto>();

        await ExportWebViewGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        Window? host = null;
        WebView2? wv = null;
        try
        {
            host = new Window
            {
                Width = 1,
                Height = 1,
                Left = -32000,
                Top = 0,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false,
                ShowActivated = false,
                Visibility = Visibility.Hidden
            };
            wv = new WebView2();
            host.Content = wv;
            host.Show();

            var env = await WebView2EnvironmentManager.GetSharedEnvironmentAsync().ConfigureAwait(true);
            await wv.EnsureCoreWebView2Async(env).ConfigureAwait(true);
            var core = wv.CoreWebView2 ?? throw new InvalidOperationException("CoreWebView2 is null after init.");
            var mgr = core.CookieManager;

            foreach (var uri in lookupUris)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<CoreWebView2Cookie> batch;
                try
                {
                    batch = await mgr.GetCookiesAsync(uri).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebCookieSnapshotService.GetCookiesAsync({uri}): {ex.Message}");
                    continue;
                }

                var list = batch.Select(SerializeCookie).Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Domain)).ToList();
                if (list.Count > 0)
                    entries.Add(new PortableCookieEntryDto { RequestUri = uri, Cookies = list });
            }
        }
        finally
        {
            try { host?.Close(); } catch { /* ignore */ }
            ExportWebViewGate.Release();
        }

        var dto = new PortableCookieBundleDto { Format = FormatVersion, Entries = entries };
        return JsonSerializer.Serialize(dto, JsonOpts);
    }

    public static Task ApplySnapshotJsonAsync(CoreWebView2CookieManager mgr, string json)
    {
        if (mgr == null || string.IsNullOrWhiteSpace(json)) return Task.CompletedTask;

        PortableCookieBundleDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PortableCookieBundleDto>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebCookieSnapshotService.Apply: parse error: {ex.Message}");
            return Task.CompletedTask;
        }

        if (dto == null || dto.Format != FormatVersion || dto.Entries == null || dto.Entries.Count == 0)
            return Task.CompletedTask;

        try
        {
            foreach (var entry in dto.Entries)
            {
                if (entry.Cookies == null) continue;
                foreach (var c in entry.Cookies)
                {
                    var name = c.Name?.Trim();
                    var domain = c.Domain?.Trim();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(domain)) continue;

                    var path = string.IsNullOrWhiteSpace(c.Path) ? "/" : c.Path.Trim();
                    var cookie = mgr.CreateCookie(name, c.Value ?? string.Empty, domain, path);
                    cookie.IsSecure = c.Secure;
                    cookie.IsHttpOnly = c.HttpOnly;
                    if (c.SameSite is int si && Enum.IsDefined(typeof(CoreWebView2CookieSameSiteKind), si))
                        cookie.SameSite = (CoreWebView2CookieSameSiteKind)si;

                    if (!string.IsNullOrWhiteSpace(c.Expires) &&
                        DateTime.TryParse(c.Expires, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
                    {
                        cookie.Expires = exp;
                    }

                    mgr.AddOrUpdateCookie(cookie);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebCookieSnapshotService.Apply: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public static bool IsV2PortableCookieBundleJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("format", out var f) || f.ValueKind != JsonValueKind.Number)
                return false;
            return f.GetInt32() == FormatVersion;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>URI tuyệt đối dùng cho GetCookiesAsync (thêm cả origin để bắt cookie scope rộng hơn).</summary>
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

    private static PortableCookieItemDto SerializeCookie(CoreWebView2Cookie c)
    {
        string? expiresIso = null;
        try
        {
            var exp = c.Expires;
            if (exp != DateTime.MinValue && exp.Year > 1601)
                expiresIso = exp.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch { /* ignore */ }

        return new PortableCookieItemDto
        {
            Name = c.Name,
            Value = c.Value,
            Domain = c.Domain,
            Path = string.IsNullOrEmpty(c.Path) ? "/" : c.Path,
            Secure = c.IsSecure,
            HttpOnly = c.IsHttpOnly,
            SameSite = (int)c.SameSite,
            Expires = expiresIso
        };
    }
}
