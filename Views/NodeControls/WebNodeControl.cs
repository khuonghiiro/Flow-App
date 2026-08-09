using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using CefSharp;
using CefSharp.Wpf;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public static class WebNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }
        private static readonly Dictionary<Border, DispatcherTimer> _titleUpdateTimers = new();
        private static readonly Dictionary<Border, bool> _titleUpdatedAfterZoom = new();
        private static readonly Dictionary<Border, (double x, double y, double w, double h)> _viewportExpandRestore = new();
        private static readonly FontFamily ViewportExpandIconFont = new("Segoe MDL2 Assets");
        private static readonly Dictionary<Border, double> _webViewZoomLevels = new();
        private static readonly System.Threading.SemaphoreSlim _webView2InitGate = new(1, 1);
        private static int _webViewInitSequence;
        private const int WebViewInitStaggerMs = 120;
        private const int WebViewInitStaggerMaxMs = 2200;

        private static readonly Dictionary<string, double> _domainZoomByHost = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _requestPayloadCache = new();

        private sealed class CdpRequestInfo
        {
            public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string? PostData { get; set; }
            public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CdpRequestInfo> _cdpByUrlMethod
            = new(StringComparer.OrdinalIgnoreCase);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Url, string Method)> _cdpRequestIdToUrlMethod
            = new(StringComparer.OrdinalIgnoreCase);

        private static int GetInitStaggerDelayMs()
        {
            var sequence = System.Threading.Interlocked.Increment(ref _webViewInitSequence);
            var delay = (sequence - 1) * WebViewInitStaggerMs;
            if (delay < 0) return 0;
            return Math.Min(delay, WebViewInitStaggerMaxMs);
        }

        private static bool ShouldUseViewportLazyInit(DependencyObject? associatedObject)
        {
            try
            {
                var isDebug = false;
                if (associatedObject != null)
                {
                    var parentWindow = Window.GetWindow(associatedObject) as WorkflowEditorWindow;
                    if (parentWindow != null)
                    {
                        isDebug = parentWindow.IsDebugReopenSession;
                    }
                }
                var prefs = CanvasToolbarPreferencesStore.Load(isDebug);
                return string.Equals(prefs.CanvasDisplayMode, "ViewportOnly", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string?> SetCookiesFromTextAsync(ICookieManager cookieManager, string cookieText, string? fallbackUrl = null)
        {
            if (string.IsNullOrWhiteSpace(cookieText)) return null;

            string? extractedUrl = null;
            var lines = cookieText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            try
            {
                // 1. Tìm URL trong cookie text (nếu có - plain text URL)
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                        {
                            extractedUrl = trimmed;
                            System.Diagnostics.Debug.WriteLine($"[Cookie] Extracted URL from plain text: {extractedUrl}");
                            break;
                        }
                    }
                }

                // 2. Detect format và parse cookies
                var trimmedText = cookieText.Trim();
                
                // Format 1: JSON object with url and cookies array: {"url":"...", "cookies":[...]}
                if (trimmedText.StartsWith("{"))
                {
                    try
                    {
                        var jsonRoot = JsonSerializer.Deserialize<JsonElement>(cookieText);
                        if (jsonRoot.ValueKind == JsonValueKind.Object)
                        {
                            // Extract URL from json if present
                            if (jsonRoot.TryGetProperty("url", out var urlProp))
                            {
                                var url = urlProp.GetString();
                                if (!string.IsNullOrWhiteSpace(url))
                                {
                                    extractedUrl = url;
                                    System.Diagnostics.Debug.WriteLine($"[Cookie] Extracted URL from JSON: {extractedUrl}");
                                }
                            }

                            // Parse cookies array
                            if (jsonRoot.TryGetProperty("cookies", out var cookiesProp) && 
                                cookiesProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var cookieObj in cookiesProp.EnumerateArray())
                                {
                                    try
                                    {
                                        var name = cookieObj.TryGetProperty("name", out var n) ? n.GetString() : null;
                                        var value = cookieObj.TryGetProperty("value", out var v) ? v.GetString() : null;
                                        var domain = cookieObj.TryGetProperty("domain", out var d) ? d.GetString() : null;
                                        var path = cookieObj.TryGetProperty("path", out var p) ? p.GetString() : "/";
                                        var secure = cookieObj.TryGetProperty("secure", out var s) && s.GetBoolean();
                                        var httpOnly = cookieObj.TryGetProperty("httpOnly", out var h) && h.GetBoolean();
                                        
                                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(domain)) continue;

                                        var cookie = new CefSharp.Cookie
                                        {
                                            Name = name,
                                            Value = value ?? "",
                                            Domain = domain,
                                            Path = path ?? "/",
                                            Secure = secure,
                                            HttpOnly = httpOnly
                                        };
                                        var cookieUrl = $"https://{domain.TrimStart('.')}{(path ?? "/")}";
                                        await cookieManager.SetCookieAsync(cookieUrl, cookie);
                                        System.Diagnostics.Debug.WriteLine($"[Cookie] Added from JSON object: {name}={value?.Substring(0, Math.Min(20, value?.Length ?? 0))}... (domain: {domain})");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Cookie] Error parsing JSON cookie: {ex.Message}");
                                    }
                                }
                                return extractedUrl;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Cookie] JSON object parse error: {ex.Message}");
                    }
                }

                // Lấy dòng đầu tiên (nếu có) để phục vụ cho việc detect format bên dưới
                var firstLine = lines.Length > 0 ? lines[0] : string.Empty;

                // Format 2: JSON array [{name, value, domain, path, ...}, ...]
                if (trimmedText.StartsWith("["))
                {
                    try
                    {
                        var cookiesJson = JsonSerializer.Deserialize<JsonElement>(cookieText);
                        if (cookiesJson.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cookieObj in cookiesJson.EnumerateArray())
                            {
                                try
                                {
                                    var name = cookieObj.TryGetProperty("name", out var n) ? n.GetString() : null;
                                    var value = cookieObj.TryGetProperty("value", out var v) ? v.GetString() : null;
                                    var domain = cookieObj.TryGetProperty("domain", out var d) ? d.GetString() : null;
                                    var path = cookieObj.TryGetProperty("path", out var p) ? p.GetString() : "/";
                                    var secure = cookieObj.TryGetProperty("secure", out var s) && s.GetBoolean();
                                    var httpOnly = cookieObj.TryGetProperty("httpOnly", out var h) && h.GetBoolean();
                                    var hostOnly = cookieObj.TryGetProperty("hostOnly", out var ho) && ho.GetBoolean();

                                    if (string.IsNullOrWhiteSpace(name)) continue;

                                    // Xử lý tiền tố __Host- và __Secure- theo tiêu chuẩn RFC 6265bis / Chromium:
                                    // Cookie bắt đầu bằng __Host- BẮT BUỘC: Secure=true, Path="/", và Domain BẮT BUỘC để trống (null)
                                    var isHostPrefix = name.StartsWith("__Host-", StringComparison.OrdinalIgnoreCase);
                                    var isSecurePrefix = name.StartsWith("__Secure-", StringComparison.OrdinalIgnoreCase);

                                    if (isHostPrefix)
                                    {
                                        secure = true;
                                        path = "/";
                                        hostOnly = true;
                                    }
                                    else if (isSecurePrefix)
                                    {
                                        secure = true;
                                    }

                                    var cookie = new CefSharp.Cookie
                                    {
                                        Name = name,
                                        Value = value ?? "",
                                        // Nếu là hostOnly (hoặc __Host-), Domain trên CefSharp.Cookie phải để null để Chromium lưu làm Host-Only cookie
                                        Domain = hostOnly ? null : domain,
                                        Path = string.IsNullOrWhiteSpace(path) ? "/" : path,
                                        Secure = secure,
                                        HttpOnly = httpOnly
                                    };

                                    // SameSite mapping
                                    if (cookieObj.TryGetProperty("sameSite", out var ss) && ss.ValueKind == JsonValueKind.String)
                                    {
                                        var ssStr = ss.GetString();
                                        if (!string.IsNullOrWhiteSpace(ssStr) && Enum.TryParse<CefSharp.Enums.CookieSameSite>(ssStr, true, out var parsedSs))
                                        {
                                            cookie.SameSite = parsedSs;
                                        }
                                    }

                                    // Expiration Date (Unix timestamp in seconds)
                                    if (cookieObj.TryGetProperty("expirationDate", out var exp) &&
                                        exp.ValueKind == JsonValueKind.Number &&
                                        exp.TryGetDouble(out var expSec) && expSec > 0)
                                    {
                                        try
                                        {
                                            cookie.Expires = DateTimeOffset.FromUnixTimeSeconds((long)expSec).LocalDateTime;
                                        }
                                        catch { }
                                    }

                                    var cleanDomain = (!string.IsNullOrWhiteSpace(domain) ? domain : (fallbackUrl ?? "")).TrimStart('.');
                                    if (cleanDomain.Contains('/')) cleanDomain = cleanDomain.Split('/')[0];
                                    if (cleanDomain.Contains(':')) cleanDomain = cleanDomain.Split(':')[0];

                                    var scheme = secure ? "https" : "http";
                                    var cookieUrl = $"{scheme}://{cleanDomain}{(path ?? "/")}";

                                    var setOk = await cookieManager.SetCookieAsync(cookieUrl, cookie);
                                    System.Diagnostics.Debug.WriteLine($"[Cookie] Added from JSON array: {name} (hostOnly: {hostOnly}, ok: {setOk}, url: {cookieUrl})");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Cookie] Error parsing JSON cookie: {ex.Message}");
                                }
                            }

                            // Thử tìm URL công cụ đích từ giá trị cookie (như callback-url)
                            if (string.IsNullOrWhiteSpace(extractedUrl))
                            {
                                foreach (var cookieObj in cookiesJson.EnumerateArray())
                                {
                                    var val = cookieObj.TryGetProperty("value", out var v) ? v.GetString() : null;
                                    if (!string.IsNullOrWhiteSpace(val) && val.Contains("http", StringComparison.OrdinalIgnoreCase))
                                    {
                                        try
                                        {
                                            var unescaped = Uri.UnescapeDataString(val);
                                            if (Uri.TryCreate(unescaped, UriKind.Absolute, out var valUri) && 
                                                (valUri.Scheme == Uri.UriSchemeHttp || valUri.Scheme == Uri.UriSchemeHttps))
                                            {
                                                extractedUrl = unescaped;
                                                System.Diagnostics.Debug.WriteLine($"[Cookie] Extracted target URL from cookie value: {extractedUrl}");
                                                break;
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }

                            // Nếu vẫn chưa có URL, tự tạo URL từ domain của cookie đầu tiên
                            if (string.IsNullOrWhiteSpace(extractedUrl))
                            {
                                foreach (var cookieObj in cookiesJson.EnumerateArray())
                                {
                                    var domain = cookieObj.TryGetProperty("domain", out var dd) ? dd.GetString() : null;
                                    if (!string.IsNullOrWhiteSpace(domain))
                                    {
                                        var cleanDomain = domain!.TrimStart('.');
                                        extractedUrl = $"https://{cleanDomain}";
                                        System.Diagnostics.Debug.WriteLine($"[Cookie] Extracted URL from JSON array domain: {extractedUrl}");
                                        break;
                                    }
                                }
                            }

                            try { await cookieManager.FlushStoreAsync(); } catch { }
                            return extractedUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Cookie] JSON parse error: {ex.Message}");
                    }
                }

                // Format 2: Netscape format (tab-separated hoặc có header)
                bool isNetscapeFormat = firstLine.Contains("Netscape HTTP Cookie File", StringComparison.OrdinalIgnoreCase) ||
                                       lines.Any(l => l.Contains('\t'));
                
                if (isNetscapeFormat)
                {
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                        if (trimmed.StartsWith("http://") || trimmed.StartsWith("https://")) continue;

                        var parts = trimmed.Split('\t');
                        if (parts.Length >= 7)
                        {
                            try
                            {
                                var domain = parts[0];
                                // parts[1] = flag (TRUE/FALSE)
                                var path = parts[2];
                                var secure = parts[3].Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                                // parts[4] = expiration
                                var name = parts[5];
                                var value = parts[6];

                                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(domain)) continue;

                                var cookie = new CefSharp.Cookie
                                {
                                    Name = name,
                                    Value = value,
                                    Domain = domain,
                                    Path = path,
                                    Secure = secure
                                };
                                var cookieUrl = $"https://{domain.TrimStart('.')}{(path ?? "/")}";
                                await cookieManager.SetCookieAsync(cookieUrl, cookie);
                                System.Diagnostics.Debug.WriteLine($"[Cookie] Added from Netscape: {name}={value} (domain: {domain})");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Cookie] Error parsing Netscape cookie: {ex.Message}");
                            }
                        }
                    }
                    return extractedUrl;
                }

                // Format 3: Raw cookie string (name=value; name2=value2; domain=.example.com)
                // Nếu có domain trong text, dùng nó; không thì cần extractedUrl
                string? cookieDomain = null;
                var cookieLines = new List<string>();
                
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("http://") || trimmed.StartsWith("https://")) continue;
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    
                    // Check for domain in line
                    if (trimmed.Contains("domain=", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(trimmed, @"domain\s*=\s*([^\s;]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            cookieDomain = match.Groups[1].Value;
                        }
                    }
                    
                    cookieLines.Add(trimmed);
                }

                // Nếu không có domain, thử extract từ URL (hoặc fallbackUrl)
                if (string.IsNullOrWhiteSpace(cookieDomain))
                {
                    var targetUrl = !string.IsNullOrWhiteSpace(extractedUrl) ? extractedUrl : fallbackUrl;
                    if (!string.IsNullOrWhiteSpace(targetUrl) && Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
                    {
                        cookieDomain = uri.Host;
                    }
                }

                if (!string.IsNullOrWhiteSpace(cookieDomain))
                {
                    foreach (var line in cookieLines)
                    {
                        // Parse name=value pairs
                        var pairs = line.Split(';');
                        foreach (var pair in pairs)
                        {
                            var trimmedPair = pair.Trim();
                            if (string.IsNullOrEmpty(trimmedPair)) continue;
                            if (trimmedPair.StartsWith("domain=", StringComparison.OrdinalIgnoreCase)) continue;
                            if (trimmedPair.StartsWith("path=", StringComparison.OrdinalIgnoreCase)) continue;
                            if (trimmedPair.StartsWith("expires=", StringComparison.OrdinalIgnoreCase)) continue;
                            if (trimmedPair.Equals("Secure", StringComparison.OrdinalIgnoreCase)) continue;
                            if (trimmedPair.Equals("HttpOnly", StringComparison.OrdinalIgnoreCase)) continue;

                            var eqIndex = trimmedPair.IndexOf('=');
                            if (eqIndex > 0)
                            {
                                try
                                {
                                    var name = trimmedPair.Substring(0, eqIndex).Trim();
                                    var value = trimmedPair.Substring(eqIndex + 1).Trim();
                                    
                                    if (!string.IsNullOrWhiteSpace(name))
                                    {
                                        var cookie = new CefSharp.Cookie
                                        {
                                            Name = name,
                                            Value = value,
                                            Domain = cookieDomain,
                                            Path = "/"
                                        };
                                        var cookieUrl = $"https://{cookieDomain.TrimStart('.')}/";
                                        await cookieManager.SetCookieAsync(cookieUrl, cookie);
                                        System.Diagnostics.Debug.WriteLine($"[Cookie] Added from raw: {name}={value} (domain: {cookieDomain})");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Cookie] Error parsing raw cookie: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Cookie] Error setting cookies: {ex.Message}");
            }

            return extractedUrl;
        }

        // Shared HttpClient cho Google Suggest API (reuse, không tạo mới mỗi lần)
        private static readonly System.Net.Http.HttpClient _suggestHttpClient = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        public static Border CreateBorder(WebNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var border = new Border
            {
                // Đảm bảo Width/Height luôn hợp lệ để tránh lỗi HwndHost khi chuyển workflow giữa các máy
                Width = Math.Max(600, node.Width),
                Height = Math.Max(600, node.Height),
                MinWidth = 600,
                MinHeight = 600,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node,
                // Tránh ghosting: không dùng BitmapCache khi có GPU
                CacheMode = null
            };

            // Áp dụng GPU optimization cho border (tự động kiểm tra GPU và chỉ áp dụng khi có GPU)
            GpuOptimizationHelper.ApplyToBorder(border);

            var grid = new Grid();
            // Top: Auto, Middle: *, Bottom: Auto — để WebView2 dãn tối đa, top/bottom chỉ cao theo nội dung.
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                    // Top: auto
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Middle (WebView2): *
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                    // Bottom: auto

            // Áp dụng GPU optimization cho grid (tự động kiểm tra GPU)
            GpuOptimizationHelper.ApplyToElement(grid);

            string activeCacheMode = node.CacheMode ?? "Shared";
            string activeCustomCacheName = node.CustomCacheName ?? "Shared";

            RequestContext? GetCurrentRequestContext()
            {
                var profileName = string.Equals(node.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(node.CustomCacheName)
                    ? node.CustomCacheName.Trim()
                    : "Shared";
                return CefSharpEnvironmentManager.CreateProfileRequestContext(profileName);
            }

            var webView = new ChromiumWebBrowser
            {
                Visibility = Visibility.Collapsed,
                RequestContext = GetCurrentRequestContext()
            };
            Grid.SetRow(webView, 1);
            var isDisposed = false;
            Action recreateWebView = null!;

            // JS injection bridge: when workflow runs WebNodeExecutor it sets node.PendingJavaScript.
            // WebNodeControl listens and executes the script into WebView2.
            string? pendingJsQueue = null;

            // Small automation helper available as: window.ac (or just ac)
            // Supports: sleep, waitForSelector, exists, scrollIntoView, click, clickText, retryClick,
            //           setValue, type, getText, waitNetworkIdle
            string BuildAutomationHelperScript()
            {
                // Use an IIFE to avoid leaking helper functions; store on window.__FlowMyHelperV1
                return @"
(function() {
  if (window.__FlowMyHelperV1) return;

  function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

  function _normText(s) { return (s == null ? '' : String(s)).replace(/\s+/g, ' ').trim().toLowerCase(); }

  function _isVisible(el) {
    if (!el) return false;
    const style = window.getComputedStyle(el);
    if (!style) return true;
    if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
    const r = el.getBoundingClientRect();
    return r.width > 0 && r.height > 0;
  }

  function scrollIntoView(selectorOrEl, center) {
    const el = typeof selectorOrEl === 'string' ? document.querySelector(selectorOrEl) : selectorOrEl;
    if (!el) return false;
    try {
      el.scrollIntoView({ block: center ? 'center' : 'nearest', inline: 'nearest', behavior: 'instant' });
    } catch {
      try { el.scrollIntoView(true); } catch { /* ignore */ }
    }
    return true;
  }

  async function waitForSelector(selector, timeoutMs) {
    timeoutMs = typeof timeoutMs === 'number' ? timeoutMs : 10000;
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const el = document.querySelector(selector);
      if (el) return el;
      await sleep(100);
    }
    return null;
  }

  function exists(selector) { return !!document.querySelector(selector); }

  async function click(selector, timeoutMs) {
    const el = await waitForSelector(selector, timeoutMs);
    if (!el) throw new Error('Element not found for click: ' + selector);
    scrollIntoView(el, true);
    el.click();
    return true;
  }

  async function clickText(text, options) {
    options = options || {};
    const timeoutMs = typeof options.timeoutMs === 'number' ? options.timeoutMs : 10000;
    const selector = options.selector || 'button, a, [role=button], input[type=button], input[type=submit]';
    const exact = !!options.exact;
    const start = Date.now();
    const target = _normText(text);

    while (Date.now() - start < timeoutMs) {
      const els = Array.from(document.querySelectorAll(selector));
      for (const el of els) {
        if (!_isVisible(el)) continue;
        const t = _normText(el.innerText != null ? el.innerText : el.textContent);
        if (!t) continue;
        const ok = exact ? (t === target) : (t.indexOf(target) >= 0);
        if (ok) {
          scrollIntoView(el, true);
          el.click();
          return true;
        }
      }
      await sleep(100);
    }
    throw new Error('Element not found for clickText: ' + text);
  }

  async function retryClick(selector, options) {
    options = options || {};
    const timeoutMs = typeof options.timeoutMs === 'number' ? options.timeoutMs : 15000;
    const intervalMs = typeof options.intervalMs === 'number' ? options.intervalMs : 250;
    const requireVisible = options.requireVisible !== false;
    const start = Date.now();

    let lastErr = null;
    while (Date.now() - start < timeoutMs) {
      try {
        const el = document.querySelector(selector);
        if (!el) throw new Error('Not found');
        if (requireVisible && !_isVisible(el)) throw new Error('Not visible');
        scrollIntoView(el, true);
        el.click();
        return true;
      } catch (e) {
        lastErr = e;
        await sleep(intervalMs);
      }
    }
    throw new Error('retryClick timeout for selector: ' + selector + (lastErr ? (' (' + lastErr.message + ')') : ''));
  }

  async function setValue(selector, value, timeoutMs) {
    const el = await waitForSelector(selector, timeoutMs);
    if (!el) throw new Error('Element not found for setValue: ' + selector);
    scrollIntoView(el, true);
    el.value = value == null ? '' : String(value);
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    return true;
  }

  async function type(selector, text, timeoutMs) {
    // Simple type: just set value + input events (reliable for most SPA forms)
    return await setValue(selector, text, timeoutMs);
  }

  async function getText(selector, timeoutMs) {
    const el = await waitForSelector(selector, timeoutMs);
    if (!el) return null;
    return (el.innerText != null ? el.innerText : el.textContent);
  }

  // Network idle tracker (fetch + XHR)
  const __net = { inflight: 0, lastActive: Date.now() };

  (function installNetHooks() {
    try {
      // fetch
      if (window.fetch && !window.__acFetchWrapped) {
        const origFetch = window.fetch.bind(window);
        window.fetch = function() {
          __net.inflight++;
          __net.lastActive = Date.now();
          try {
            const p = origFetch.apply(this, arguments);
            return Promise.resolve(p).finally(() => {
              __net.inflight = Math.max(0, __net.inflight - 1);
              __net.lastActive = Date.now();
            });
          } catch (e) {
            __net.inflight = Math.max(0, __net.inflight - 1);
            __net.lastActive = Date.now();
            throw e;
          }
        };
        window.__acFetchWrapped = true;
      }

      // XHR
      if (window.XMLHttpRequest && !window.__acXhrWrapped) {
        const OrigXHR = window.XMLHttpRequest;
        function WrappedXHR() {
          const xhr = new OrigXHR();
          let counted = false;
          function dec() {
            if (!counted) return;
            counted = false;
            __net.inflight = Math.max(0, __net.inflight - 1);
            __net.lastActive = Date.now();
          }
          const origOpen = xhr.open;
          xhr.open = function() {
            xhr.addEventListener('readystatechange', function() {
              if (xhr.readyState === 1 && !counted) {
                counted = true;
                __net.inflight++;
                __net.lastActive = Date.now();
              }
              if (xhr.readyState === 4) dec();
            });
            return origOpen.apply(xhr, arguments);
          };
          xhr.addEventListener('error', dec);
          xhr.addEventListener('abort', dec);
          return xhr;
        }
        window.XMLHttpRequest = WrappedXHR;
        window.__acXhrWrapped = true;
      }
    } catch { /* ignore */ }
  })();

  async function waitNetworkIdle(options) {
    options = options || {};
    const idleMs = typeof options.idleMs === 'number' ? options.idleMs : 800;
    const timeoutMs = typeof options.timeoutMs === 'number' ? options.timeoutMs : 15000;
    const start = Date.now();

    while (Date.now() - start < timeoutMs) {
      const now = Date.now();
      if (__net.inflight === 0 && (now - __net.lastActive) >= idleMs) return true;
      await sleep(100);
    }
    return false;
  }

  window.__FlowMyHelperV1 = {
    sleep, waitForSelector, exists,
    scrollIntoView, click, clickText, retryClick,
    setValue, type, getText,
    waitNetworkIdle
  };
  window.ac = window.__FlowMyHelperV1;
})();";
            }

            string WrapUserScript(string userScript)
            {
                // Run inside async IIFE so user can use 'await ac.xxx(...)'.
                // Also return something useful to debug (will be JSON-stringified by WebView2)
                return $@"
(async () => {{
  try {{
    {userScript}
    return 'ok';
  }} catch (e) {{
    return 'error: ' + (e && e.message ? e.message : String(e));
  }}
}})();";
            }

            async Task TryExecutePendingJsAsync(string? js)
            {
                if (string.IsNullOrWhiteSpace(js)) return;

                // If WebView2 not ready yet, queue it.
                if (webView == null)
                {
                    pendingJsQueue = js;
                    return;
                }

                try
                {
                    // Reset flag trước mỗi lần chạy JS để tránh bị ảnh hưởng bởi lần chạy trước.
                    try
                    {
                        await webView.EvaluateScriptAsync("window.__FlowMyWorkflowDone = false;");
                    }
                    catch { /* ignore */ }

                    // Ensure helper exists
                    await webView.EvaluateScriptAsync(BuildAutomationHelperScript());

                    // Execute user script (wrapped to support await)
                    var wrapped = WrapUserScript(js);
                    await webView.EvaluateScriptAsync(wrapped);

                    try
                    {
                        var flagResp = await webView.EvaluateScriptAsync("window.__FlowMyWorkflowDone === true");
                        if (flagResp.Success && string.Equals(flagResp.Result?.ToString(), "true", StringComparison.OrdinalIgnoreCase))
                        {
                            var tcs = node.PendingOutputsTcs;
                            if (tcs != null && !tcs.Task.IsCompleted)
                            {
                                System.Diagnostics.Debug.WriteLine("[WebNodeControl] ✓ JS flag __FlowMyWorkflowDone=true detected, signaling PendingOutputsTcs.");
                                tcs.TrySetResult(true);
                            }
                        }
                    }
                    catch (Exception tcsEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebNodeControl] PendingOutputsTcs signal from JS flag error: {tcsEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebNode ExecuteScriptAsync error: {ex.Message}");
                }
                finally
                {
                    // Luôn luôn clear PendingJavaScript nếu nó vẫn là đoạn JS vừa chạy
                    // (kể cả khi thành công hay lỗi) để lần JS mới vào không bị "kẹt" bởi giá trị cũ.
                    try
                    {
                        if (string.Equals(node.PendingJavaScript, js, StringComparison.Ordinal))
                        {
                            node.PendingJavaScript = null;
                        }
                    }
                    catch { /* ignore */ }
                }
            }

            // ── Auto-reload timer ──────────────────────────────────────────────
            // Khai báo TRƯỚC PropertyChanged để tránh lỗi "unassigned local variable"
            DispatcherTimer? autoReloadTimer = null;

            // Khai báo jsSourceTimers sớm để border.Unloaded có thể dùng (fix CS0841)
            var jsSourceTimers = new Dictionary<int, DispatcherTimer>();
            DispatcherTimer? sleepModeTimer = null;
            var isSleepModeActive = false;
            var suppressUrlSyncForSleepNav = false;
            Action loadProfileComboItems = null!;

            static int CalcSleepIdleMs(WebNode n)
            {
                var val = Math.Max(1, n.SleepIdleTimeoutValue);
                var unit = (n.SleepIdleTimeoutUnit ?? "s").Trim();
                return unit switch
                {
                    "ms" => Math.Max(50, val),
                    "min" or "phút" => Math.Max(1, val) * 60000,
                    _ => Math.Max(1, val) * 1000
                };
            }

            void StopSleepModeTimer()
            {
                sleepModeTimer?.Stop();
                sleepModeTimer = null;
            }

            // Track last UI/flow activity to determine "idle"
            var lastActivityUtc = DateTime.UtcNow;
            void MarkActivity()
            {
                lastActivityUtc = DateTime.UtcNow;
                if (node.EnableSleepMode && isSleepModeActive)
                {
                    // Wake on first activity after sleeping
                    _ = webView.Dispatcher.BeginInvoke(new Action(async () => await WakeRuntimeAsync()), DispatcherPriority.Background);
                }
            }

            async Task EnterSleepModeAsync()
            {
                if (!node.EnableSleepMode || isSleepModeActive) return;
                if (node.PendingOutputsTcs is { Task.IsCompleted: false }) return;
                if (!string.IsNullOrWhiteSpace(node.PendingJavaScript)) return;

                isSleepModeActive = true;
                StopSleepModeTimer();

                try
                {
                    autoReloadTimer?.Stop();
                    foreach (var timer in jsSourceTimers.Values)
                        timer.Stop();

                    if (webView != null)
                    {
                        suppressUrlSyncForSleepNav = true;
                        webView.LoadUrl("about:blank");
                    }
                }
                catch { }

                try
                {
                    webView.Visibility = Visibility.Collapsed;
                }
                catch { }
            }

            async Task WakeRuntimeAsync()
            {
                if (!node.EnableSleepMode)
                {
                    isSleepModeActive = false;
                    return;
                }

                StopSleepModeTimer();
                isSleepModeActive = false;
                suppressUrlSyncForSleepNav = false;

                try
                {
                    webView.Visibility = Visibility.Visible;
                }
                catch { }

                try
                {
                    var currentUrl = webView?.Address;
                    if (webView != null && (string.IsNullOrWhiteSpace(currentUrl) || string.Equals(currentUrl, "about:blank", StringComparison.OrdinalIgnoreCase)))
                    {
                        var targetUrl = node.ExtractUrl?.Trim();
                        if (!string.IsNullOrWhiteSpace(targetUrl) && targetUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            webView.LoadUrl(targetUrl);
                    }
                }
                catch { }

                UpdateAutoReloadTimer();
                UpdateJsSourceTimers();
                RestartSleepModeTimer();
            }

            void RestartSleepModeTimer()
            {
                if (!node.EnableSleepMode) return;

                StopSleepModeTimer();
                sleepModeTimer = new DispatcherTimer(DispatcherPriority.Background, webView.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(300)
                };
                sleepModeTimer.Tick += async (_, _) =>
                {
                    try
                    {
                        if (!node.EnableSleepMode) { StopSleepModeTimer(); return; }
                        var idleMs = CalcSleepIdleMs(node);
                        var idleFor = (DateTime.UtcNow - lastActivityUtc).TotalMilliseconds;
                        if (idleFor >= idleMs)
                            await EnterSleepModeAsync();
                    }
                    catch { }
                };
                sleepModeTimer.Start();
            }

            // Tính TimeSpan interval từ value + unit của node
            static TimeSpan CalcAutoReloadInterval(WebNode n)
            {
                double val = Math.Max(1, n.AutoReloadIntervalValue);
                return (n.AutoReloadIntervalUnit ?? "s") switch
                {
                    "ms"  => TimeSpan.FromMilliseconds(val),
                    "min" or "phút" => TimeSpan.FromMinutes(val),
                    _     => TimeSpan.FromSeconds(val)   // "s" hoặc mặc định
                };
            }

            void UpdateAutoReloadTimer()
            {
                // Dừng timer cũ nếu có
                if (autoReloadTimer != null)
                {
                    autoReloadTimer.Stop();
                    autoReloadTimer = null;
                }

                if (!node.AutoReloadEnabled) return;

                var interval = CalcAutoReloadInterval(node);
                autoReloadTimer = new DispatcherTimer(DispatcherPriority.Background, webView.Dispatcher)
                {
                    Interval = interval
                };
                autoReloadTimer.Tick += (_, _) =>
                {
                    try
                    {
                        if (webView != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AutoReload] Reloading page (interval={interval.TotalSeconds:0.##}s)...");
                            webView.Reload();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AutoReload] Reload error: {ex.Message}");
                    }
                };
                autoReloadTimer.Start();
                System.Diagnostics.Debug.WriteLine($"[AutoReload] Timer started with interval {interval.TotalMilliseconds}ms");
            }

            // Dọn dẹp timer khi border bị unload (tránh memory leak)
            border.Unloaded += (_, _) =>
            {
                autoReloadTimer?.Stop();
                autoReloadTimer = null;
                StopSleepModeTimer();
                // Dọn dẹp tất cả js source timers
                foreach (var t in jsSourceTimers.Values)
                    t.Stop();
                jsSourceTimers.Clear();
            };
            // ── End auto-reload timer ──────────────────────────────────────────

            // ── JS Source auto-timer ──────────────────────────────────────────
            // Mỗi JsSource có thể có timer riêng để tự chạy source node → lấy JS → inject WebView2
            // Không cần flow phải đi qua WebNode trước. Checked = tự chạy theo chu kỳ.

            // Tính TimeSpan từ value + unit của 1 JsSourceMapping
            static TimeSpan CalcJsTimerInterval(WebJsSourceMapping m)
            {
                double val = Math.Max(1, m.AutoTimerIntervalValue);
                return (m.AutoTimerIntervalUnit ?? "s") switch
                {
                    "ms"             => TimeSpan.FromMilliseconds(val),
                    "min" or "phút" => TimeSpan.FromMinutes(val),
                    _                => TimeSpan.FromSeconds(val)
                };
            }

            // Chạy source node → chờ output populate → lấy JS → inject WebView2.
            // Đây là tác vụ hoàn chỉnh cho mỗi timer tick.
            async Task RunJsSourceOnceAsync(WebJsSourceMapping m)
            {
                if (string.IsNullOrWhiteSpace(m.SourceNodeId) || string.IsNullOrWhiteSpace(m.SourceOutputKey))
                    return;
                try
                {
                    var sourceNode = host?.ViewModel?.Nodes?.FirstOrDefault(n =>
                        string.Equals(n.Id, m.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                    if (sourceNode == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[JsTimer] Source node not found: {m.SourceNodeId}");
                        return;
                    }

                    // Thử lấy JS từ output hiện tại của source node trước (nếu đã có kết quả từ lần trước)
                    var currentJs = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, m.SourceOutputKey);
                    if (!string.IsNullOrWhiteSpace(currentJs) && currentJs != "—")
                    {
                        // Đã có JS sẵn → chạy ngay, rồi kích hoạt source node chạy lại để cập nhật cho tick tiếp
                        System.Diagnostics.Debug.WriteLine($"[JsTimer] Running cached output JS (len={currentJs.Length}), then re-running source node for next tick");
                        await TryExecutePendingJsAsync(currentJs);

                        // Chạy source node background để output mới sẵn cho tick kế tiếp
                        host?.RequestRunSingleNode(sourceNode);
                    }
                    else
                    {
                        // Chưa có output → chạy source node trước, đợi một lúc rồi lấy output
                        System.Diagnostics.Debug.WriteLine($"[JsTimer] No current output, running source node first: {sourceNode.Title}");
                        host?.RequestRunSingleNode(sourceNode);

                        // Chờ source node hoàn thành (heuristic delay – source node thường nhẹ)
                        await Task.Delay(800);

                        var js = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, m.SourceOutputKey);
                        if (!string.IsNullOrWhiteSpace(js) && js != "—")
                        {
                            System.Diagnostics.Debug.WriteLine($"[JsTimer] Got JS after source run (len={js.Length}), injecting...");
                            await TryExecutePendingJsAsync(js);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[JsTimer] Source node ran but no JS output for key={m.SourceOutputKey}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[JsTimer] RunJsSourceOnceAsync error: {ex.Message}");
                }
            }

            void UpdateJsSourceTimers()
            {
                // Dừng và xóa tất cả timers cũ
                foreach (var t in jsSourceTimers.Values)
                    t.Stop();
                jsSourceTimers.Clear();

                var sources = node.JsSources;
                if (sources == null || sources.Count == 0) return;

                for (int idx = 0; idx < sources.Count; idx++)
                {
                    var mapping = sources[idx];
                    if (!mapping.AutoTimerEnabled) continue;
                    if (string.IsNullOrWhiteSpace(mapping.SourceNodeId) || string.IsNullOrWhiteSpace(mapping.SourceOutputKey)) continue;

                    var capturedMapping = mapping;
                    var interval = CalcJsTimerInterval(capturedMapping);

                    // Immediate first run: Chạy ngay 1 lần khi timer mới được tạo (không cần đợi interval đầu tiên)
                    // Dùng Dispatcher để tránh block UI thread trong UpdateJsSourceTimers()
                    webView.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[JsTimer] Immediate first run for node={capturedMapping.SourceNodeId}, key={capturedMapping.SourceOutputKey}");
                        await RunJsSourceOnceAsync(capturedMapping);
                    }), DispatcherPriority.Background);

                    // Tạo timer để chạy định kỳ
                    var timer = new DispatcherTimer(DispatcherPriority.Background, webView.Dispatcher)
                    {
                        Interval = interval
                    };
                    timer.Tick += (_, _) =>
                    {
                        _ = RunJsSourceOnceAsync(capturedMapping);
                    };
                    timer.Start();
                    jsSourceTimers[idx] = timer;
                    System.Diagnostics.Debug.WriteLine($"[JsTimer] Timer #{idx} started – node={mapping.SourceNodeId}, key={mapping.SourceOutputKey}, interval={interval.TotalMilliseconds}ms");
                }
            }
            // ── End JS Source auto-timer ──────────────────────────────────────





            // Listen model changes (may come from background execution thread) -> marshal to UI thread.
            if (node is INotifyPropertyChanged npcNode)
            {
                npcNode.PropertyChanged += (_, e) =>
                {
                    if (string.Equals(e.PropertyName, nameof(WebNode.PendingJavaScript), StringComparison.Ordinal))
                    {
                        var js = node.PendingJavaScript;
                        webView.Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            MarkActivity();
                            await WakeRuntimeAsync();
                            await TryExecutePendingJsAsync(js);
                            RestartSleepModeTimer();
                        }), DispatcherPriority.Normal);
                    }
                    else if (string.Equals(e.PropertyName, nameof(WebNode.CookieText), StringComparison.Ordinal))
                    {
                        var textToApply = node.CookieText;
                        if (string.IsNullOrWhiteSpace(textToApply)) return;

                        // Clear node.CookieText now that we captured textToApply locally
                        node.CookieText = null;

                        // User clicked "Chạy" button - apply cookie now
                        webView.Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            MarkActivity();
                            await WakeRuntimeAsync();
                            ICookieManager? cookieMgr = webView.RequestContext?.GetCookieManager(null) ?? Cef.GetGlobalCookieManager();
                            if (cookieMgr != null)
                            {
                                var fallbackUrl = !string.IsNullOrWhiteSpace(webView.Address) && webView.Address.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                    ? webView.Address
                                    : node.ExtractUrl;
                                var extractedUrl = await SetCookiesFromTextAsync(cookieMgr, textToApply, fallbackUrl);
                                webView.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (!string.IsNullOrWhiteSpace(extractedUrl))
                                    {
                                        try
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Cookie] Navigating to: {extractedUrl}");
                                            node.ExtractUrl = extractedUrl;

                                            if (webView.IsBrowserInitialized)
                                            {
                                                webView.Load(extractedUrl);
                                            }
                                            else
                                            {
                                                System.Windows.DependencyPropertyChangedEventHandler? initHandler = null;
                                                initHandler = (s, e) =>
                                                {
                                                    webView.IsBrowserInitializedChanged -= initHandler;
                                                    webView.Dispatcher.BeginInvoke(new Action(() =>
                                                    {
                                                        try { webView.Load(extractedUrl); } catch { }
                                                    }));
                                                };
                                                webView.IsBrowserInitializedChanged += initHandler;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Cookie] Error navigating: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine("[Cookie] No URL found in cookie text - reloading current page to apply cookies");
                                        try
                                        {
                                            var currentUrl = webView.Address;
                                            if (!string.IsNullOrWhiteSpace(currentUrl) && 
                                                !currentUrl.Equals("about:blank", StringComparison.OrdinalIgnoreCase) &&
                                                currentUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                            {
                                                webView.Reload();
                                            }
                                            else if (!string.IsNullOrWhiteSpace(node.ExtractUrl) &&
                                                     node.ExtractUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                            {
                                                if (webView.IsBrowserInitialized) webView.Load(node.ExtractUrl);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Cookie] Error reloading after cookie apply: {ex.Message}");
                                        }
                                    }
                                 }), DispatcherPriority.Normal);
                            }
                            RestartSleepModeTimer();
                        }), DispatcherPriority.Normal);
                    }
                    else if (string.Equals(e.PropertyName, nameof(WebNode.AutoReloadEnabled), StringComparison.Ordinal) ||
                             string.Equals(e.PropertyName, nameof(WebNode.AutoReloadIntervalValue), StringComparison.Ordinal) ||
                             string.Equals(e.PropertyName, nameof(WebNode.AutoReloadIntervalUnit), StringComparison.Ordinal))
                    {
                        webView.Dispatcher.BeginInvoke(new Action(() => UpdateAutoReloadTimer()), DispatcherPriority.Normal);
                    }
                    else if (string.Equals(e.PropertyName, nameof(WebNode.JsSources), StringComparison.Ordinal))
                    {
                        // JsSources thay đổi (có thể do user bật/tắt timer, đổi interval, thêm/xóa item)
                        webView.Dispatcher.BeginInvoke(new Action(() => UpdateJsSourceTimers()), DispatcherPriority.Normal);
                    }
                    else if (string.Equals(e.PropertyName, nameof(WebNode.CacheMode), StringComparison.Ordinal) ||
                             string.Equals(e.PropertyName, nameof(WebNode.CustomCacheName), StringComparison.Ordinal))
                    {
                        border.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var targetMode = node.CacheMode ?? "Shared";
                            var targetName = string.Equals(targetMode, "Isolated", StringComparison.OrdinalIgnoreCase)
                                ? (node.CustomCacheName ?? "Shared")
                                : "Shared";

                            if (!string.Equals(activeCacheMode, targetMode, StringComparison.Ordinal) ||
                                !string.Equals(activeCustomCacheName, targetName, StringComparison.Ordinal))
                            {
                                activeCacheMode = targetMode;
                                activeCustomCacheName = targetName;
                                loadProfileComboItems?.Invoke();
                                recreateWebView?.Invoke();
                            }
                            else
                            {
                                loadProfileComboItems?.Invoke();
                            }
                        }), DispatcherPriority.Normal);
                    }
                    else if (string.Equals(e.PropertyName, nameof(WebNode.WakeRequestToken), StringComparison.Ordinal))
                    {
                        webView.Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            MarkActivity();
                            await WakeRuntimeAsync();
                        }), DispatcherPriority.Normal);
                    }
                    else if (string.Equals(e.PropertyName, nameof(WebNode.EnableSleepMode), StringComparison.Ordinal) ||
                             string.Equals(e.PropertyName, nameof(WebNode.SleepIdleTimeoutValue), StringComparison.Ordinal) ||
                             string.Equals(e.PropertyName, nameof(WebNode.SleepIdleTimeoutUnit), StringComparison.Ordinal))
                    {
                        webView.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            MarkActivity();
                            RestartSleepModeTimer();
                        }), DispatcherPriority.Background);
                    }
                    else if (string.Equals(e.PropertyName, nameof(WebNode.CacheMode), StringComparison.Ordinal) ||
                             string.Equals(e.PropertyName, nameof(WebNode.CustomCacheName), StringComparison.Ordinal))
                    {
                        var targetMode = node.CacheMode ?? "Shared";
                        var targetName = node.CustomCacheName ?? "Shared";
                        if (!string.Equals(activeCacheMode, targetMode, StringComparison.Ordinal) ||
                            !string.Equals(activeCustomCacheName, targetName, StringComparison.Ordinal))
                        {
                            activeCacheMode = targetMode;
                            activeCustomCacheName = targetName;
                            border.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                recreateWebView?.Invoke();
                            }), DispatcherPriority.Normal);
                        }
                    }
                };
            }

            // Khởi tạo timer ngay nếu node đã được cấu hình bật sẵn
            UpdateAutoReloadTimer();

            // Khởi tạo js source timers ngay (nếu đã có cấu hình bật timer từ lần trước)
            UpdateJsSourceTimers();
            RestartSleepModeTimer();

            // Mark activity on common interactions so "idle" works as expected.
            border.MouseDown += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };
            border.MouseMove += (_, _) => { MarkActivity(); };
            border.MouseWheel += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };
            webView.PreviewMouseDown += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };
            webView.PreviewMouseWheel += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };
            webView.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.F12)
                {
                    e.Handled = true;
                    FlowNetworkRequestHandler.ToggleDevTools(webView, webView.GetBrowser());
                }
            };

            // Tối ưu CefSharp ChromiumWebBrowser: sắc nét 100%, không bị nhoè mờ dù bật hay tắt GPU
            RenderOptions.SetBitmapScalingMode(webView, BitmapScalingMode.LowQuality);
            RenderOptions.SetEdgeMode(webView, EdgeMode.Unspecified);
            webView.UseLayoutRounding = true;
            webView.SnapsToDevicePixels = true;
            webView.CacheMode = null; // Tránh ghosting

            // Đồng bộ kích thước web UI theo thời gian thực khi co dãn node
            border.SizeChanged += (s, e) =>
            {
                if (webView != null)
                {
                    webView.InvalidateMeasure();
                    webView.InvalidateArrange();
                    webView.InvalidateVisual();
                }
            };

            // Đồng bộ WebView2 (HwndHost) với node: khi zoom hoặc pan canvas ép WebView2 cập nhật vị trí theo thời gian thực
            void SyncWebViewPosition()
            {
                try
                {
                    // Nếu đang zoom, panning hoặc dragging, không sync để tránh nháy - sẽ sync sau khi dừng
                    if (NodeChrome.IsZooming || host.IsPanning || host.DraggedNode == node)
                        return;

                    // ✅ Đảm bảo WebView2 có kích thước hợp lệ trước khi UpdateLayout (tránh lỗi HwndHost)
                    if (webView.ActualWidth <= 0 || webView.ActualHeight <= 0)
                        return;

                    // Dùng Invalidate* thay vì UpdateLayout() (blocking) để tránh đứng UI thread
                    // WPF layout system sẽ tự schedule update ở frame tiếp theo
                    webView.InvalidateMeasure();
                    webView.InvalidateArrange();
                    webView.InvalidateVisual();

                    if (webView.Parent is FrameworkElement parent)
                    {
                        parent.InvalidateArrange();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"lỖI Đồng bộ WebView2 (HwndHost) với node error: {ex.Message}");
                }
            }

            // ✅ Lưu base zoom level (mặc định 0.9) để tính toán zoom ngược với canvas zoom
            const double baseWebViewZoom = 0.9;

            // ComboBox zoom ở bottom-left (khởi tạo sau)
            ComboBox? zoomComboBox = null;

            // Hàm apply zoom theo factor cụ thể (từ combo)
            void ApplyWebViewZoom(double zoomFactor)
            {
                // Giới hạn zoom khi apply từ UI: min 0.1, max 5.0
                zoomFactor = Math.Max(0.1, Math.Min(5.0, zoomFactor));
                if (zoomFactor <= 0) return;

                node.CssZoom = zoomFactor;
                if (!string.IsNullOrWhiteSpace(node.LastHost))
                {
                    _domainZoomByHost[node.LastHost] = zoomFactor;
                }

                try
                {
                    if (webView == null) return;

                    if (webView.CanExecuteJavascriptInMainFrame)
                    {
                        var script = $@"
                            (function() {{
                                document.body.style.zoom = '{zoomFactor.ToString(System.Globalization.CultureInfo.InvariantCulture)}';
                                if (!document.body.style.zoom) {{
                                    document.body.style.transform = 'scale({zoomFactor.ToString(System.Globalization.CultureInfo.InvariantCulture)})';
                                    document.body.style.transformOrigin = 'top left';
                                }}
                            }})();
                        ";
                        webView.EvaluateScriptAsync(script);
                    }
                    _webViewZoomLevels[border] = zoomFactor;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi ApplyWebViewZoom: {ex.Message}");
                }

                // Đồng bộ lại selection của combo + textbox (nếu có)
                if (zoomComboBox != null)
                {
                    foreach (var item in zoomComboBox.Items.OfType<ComboBoxItem>())
                    {
                        if (item.Tag is double d && Math.Abs(d - zoomFactor) < 0.0001)
                        {
                            zoomComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                // Cập nhật textbox hiển thị %
                var panel = zoomComboBox?.Parent as StackPanel;
                if (panel != null)
                {
                    var tb = panel.Children.OfType<TextBox>().LastOrDefault();
                    if (tb != null)
                    {
                        tb.Text = $"{zoomFactor * 100:0.#}%";
                    }
                }
            }
            
            // Function để set zoom cho WebView2 dựa trên canvas zoom + cấu hình per-domain
            void UpdateWebViewZoomForCanvasZoom()
            {
                try
                {
                    if (webView == null) return;

                    // Tính toán zoom
                    double canvasZoom = host.ZoomLevel;
                    double webViewZoom;

                    // Nếu node đã có cấu hình CssZoom (đã lưu theo domain hoặc node), dùng trực tiếp
                    if (node.CssZoom > 0)
                    {
                        webViewZoom = node.CssZoom;
                    }
                    else
                    {
                        // Nếu chưa có, thử lấy theo domain đã biết (nếu có)
                        if (!string.IsNullOrWhiteSpace(node.LastHost) &&
                            _domainZoomByHost.TryGetValue(node.LastHost, out var domainZoom) &&
                            domainZoom > 0)
                        {
                            webViewZoom = domainZoom;
                            node.CssZoom = domainZoom;
                        }
                        else
                        {
                            // Mặc định: zoom ngược với canvas để giữ tỉ lệ
                            // Nếu canvas zoom = 2.0 (phóng to) → WebView2 zoom = baseZoom / 2.0 = 0.45 (thu nhỏ)
                            // Nếu canvas zoom = 0.5 (thu nhỏ) → WebView2 zoom = baseZoom / 0.5 = 1.8 (phóng to)
                            webViewZoom = baseWebViewZoom / Math.Max(canvasZoom, 0.0001);
                        }
                    }
                    
                    if (webView.CanExecuteJavascriptInMainFrame)
                    {
                        var script = $@"
                            (function() {{
                                document.body.style.zoom = '{webViewZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}';
                                if (!document.body.style.zoom) {{
                                    // Fallback: dùng transform scale nếu zoom không được hỗ trợ
                                    document.body.style.transform = 'scale({webViewZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)})';
                                    document.body.style.transformOrigin = 'top left';
                                }}
                            }})();
                        ";
                        webView.EvaluateScriptAsync(script);
                    }

                    // Cập nhật cache zoom
                    _webViewZoomLevels[border] = webViewZoom;
                    node.CssZoom = webViewZoom;

                    // Nếu node đã biết domain, lưu vào map theo domain
                    if (!string.IsNullOrWhiteSpace(node.LastHost))
                    {
                        _domainZoomByHost[node.LastHost] = webViewZoom;
                    }

                    // Cập nhật ComboBox (nếu có)
                    if (zoomComboBox != null)
                    {
                        foreach (var item in zoomComboBox.Items.OfType<ComboBoxItem>())
                        {
                            if (item.Tag is double d && Math.Abs(d - webViewZoom) < 0.0001)
                            {
                                zoomComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi update WebView2 zoom: {ex.Message}");
                }
            }
            
            // Handler khi zoom thay đổi - ẩn WebView2 khi đang zoom và update zoom sau khi zoom xong
            EventHandler? scaleChangedHandler = (_, _) =>
            {
                if (isDisposed) return; // Guard: node đã unload, bỏ qua
                if (NodeChrome.IsZooming)
                {
                    // Ẩn WebView2 khi đang zoom để tránh nháy
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Hiển thị lại và sync WebView2 sau khi zoom xong
                    if (webView.Visibility != Visibility.Visible)
                        webView.Visibility = Visibility.Visible;
                    
                    // ✅ Update WebView2 zoom để giữ tỉ lệ với canvas zoom
                    UpdateWebViewZoomForCanvasZoom();
                    
                    SyncWebViewPosition();
                    // ✅ Clip WebView2 HWND để không render trên toolbar/sidebar/minimap
                    // WebView2AirspaceClipper.UpdateClipping(webView, host);
                }
            };
            var scaleDescriptor = DependencyPropertyDescriptor.FromProperty(ScaleTransform.ScaleXProperty, typeof(ScaleTransform));
            scaleDescriptor?.AddValueChanged(host.ScaleTransform, scaleChangedHandler);

            // Handler khi pan canvas - ẩn WebView2 khi đang pan để tránh nháy
            EventHandler? translateChangedHandler = (_, _) =>
            {
                if (isDisposed) return; // Guard: node đã unload, bỏ qua
                if (host.IsPanning)
                {
                    // Ẩn WebView2 khi đang pan để tránh nháy
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Hiển thị lại và sync WebView2 sau khi pan xong
                    if (webView.Visibility != Visibility.Visible)
                        webView.Visibility = Visibility.Visible;
                    SyncWebViewPosition();
                    // ✅ Clip WebView2 HWND để không render trên toolbar/sidebar/minimap
                    // WebView2AirspaceClipper.UpdateClipping(webView, host);
                }
            };
            var translateXDescriptor = DependencyPropertyDescriptor.FromProperty(TranslateTransform.XProperty, typeof(TranslateTransform));
            var translateYDescriptor = DependencyPropertyDescriptor.FromProperty(TranslateTransform.YProperty, typeof(TranslateTransform));
            translateXDescriptor?.AddValueChanged(host.TranslateTransform, translateChangedHandler);
            translateYDescriptor?.AddValueChanged(host.TranslateTransform, translateChangedHandler);

            // Đồng bộ WebView2 mỗi frame khi pan canvas hoặc kéo node — ẩn khi di chuyển, hiển thị khi dừng
            EventHandler? renderingHandler = (_, _) =>
            {
                if (isDisposed) return; // Guard: node đã unload, bỏ qua
                // Không sync khi đang zoom để tránh nháy
                if (NodeChrome.IsZooming)
                    return;

                // Ẩn WebView2 khi đang panning hoặc dragging node để tránh nháy
                if (host.IsPanning || host.DraggedNode == node)
                {
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Hiển thị lại và sync WebView2 sau khi dừng pan/drag
                    // UpdateClipping chỉ gọi khi transition từ Collapsed → Visible để tránh gọi 60fps mỗi frame
                    if (webView.Visibility != Visibility.Visible)
                    {
                        webView.Visibility = Visibility.Visible;
                        SyncWebViewPosition();
                        // Clip khi vừa hiển thị lại (sau khi pan/drag kết thúc)
                        // WebView2AirspaceClipper.UpdateClipping(webView, host);
                    }
                    // KHÔNG gọi UpdateClipping mỗi frame — quá tốn kém (visual tree walk + Win32 GDI mỗi 16ms)
                    // Clipping sẽ được cập nhật qua scaleChangedHandler và translateChangedHandler
                }
            };
            System.Windows.Media.CompositionTarget.Rendering += renderingHandler;

            // Determine theme/brightness aware colors to match the node background perfectly
            var overlayColor = Color.FromArgb(40, 255, 255, 255);
            var textBrush = Brushes.White;
            var pillBg = Color.FromArgb(50, 0, 0, 0);
            var pillBorder = Color.FromArgb(40, 255, 255, 255);

            if (node.NodeBrush is SolidColorBrush scb)
            {
                double brightness = (scb.Color.R * 299 + scb.Color.G * 587 + scb.Color.B * 114) / 1000.0;
                if (brightness > 130)
                {
                    overlayColor = Color.FromArgb(30, 0, 0, 0);
                    textBrush = Brushes.Black;
                    pillBg = Color.FromArgb(30, 255, 255, 255);
                    pillBorder = Color.FromArgb(40, 0, 0, 0);
                }
            }

            // ── TOP BAR (modernized to blend with the node background) ─────────
            var topBar = new Border
            {
                Background = new SolidColorBrush(overlayColor),
                Padding = new Thickness(6, 5, 6, 5),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            // 2 rows: row0 = toolbar controls, row1 = progress bar
            var topBarGrid = new Grid();
            topBarGrid.VerticalAlignment = VerticalAlignment.Top;
            topBarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            topBarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Columns: [0: navPanel] [1: urlPill] [2: profilePanel] [3: viewportExpandBtn] [4: refreshBtn]
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // 0: navPanel (◀ ▶ ⟳)
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });   // 1: url address bar
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // 2: profilePanel (Profile ComboBox + New + Delete)
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // 3: phóng to vừa khung
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // 4: làm mới/xóa cache (🗑)

            // Slim gradient progress bar (row 1, spans all)
            var progressBar = new ProgressBar
            {
                Height = 3,
                Margin = new Thickness(0, 3, 0, 0),
                Visibility = Visibility.Collapsed,
                IsIndeterminate = true,
                Foreground = node.NodeBrush ?? (Application.Current.TryFindResource("PrimaryBrush") as Brush 
                                                 ?? Application.Current.TryFindResource("BurgundyWineBrush") as Brush)
            };
            Grid.SetRow(progressBar, 1);
            Grid.SetColumnSpan(progressBar, 5);
            topBarGrid.Children.Add(progressBar);

            // Helper to create circular toolbar buttons with custom hover states
            Button CreateToolbarButton(string content, string tip, double width = 28, double height = 28, double fontSize = 13, FontFamily? fontFamily = null, CornerRadius? cornerRadius = null)
            {
                var btn = new Button
                {
                    Content = content,
                    ToolTip = tip,
                    Width = width,
                    Height = height,
                    FontSize = fontSize,
                    Padding = new Thickness(0),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = textBrush
                };
                if (fontFamily != null)
                {
                    btn.FontFamily = fontFamily;
                }
                
                var template = new ControlTemplate(typeof(Button));
                var factory = new FrameworkElementFactory(typeof(Border));
                factory.Name = "border";
                factory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                factory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                factory.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                factory.SetValue(Border.CornerRadiusProperty, cornerRadius ?? new CornerRadius(width / 2));
                
                var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                factory.AppendChild(contentFactory);
                template.VisualTree = factory;
                
                var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                var hoverBgColor = textBrush == Brushes.Black ? Color.FromArgb(25, 0, 0, 0) : Color.FromArgb(40, 255, 255, 255);
                trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBgColor), "border"));
                template.Triggers.Add(trigger);
                
                btn.Template = template;
                return btn;
            }

            // ── Back / Forward / Reload nav buttons (col 0) ──────────────────
            var backBtn = CreateToolbarButton("◀", "Quay lại", 26, 26, 11);
            var fwdBtn  = CreateToolbarButton("▶", "Tiến tới", 26, 26, 11);
            var f5Btn   = CreateToolbarButton("⟳", "Tải lại trang (F5)", 26, 26, 13);
            
            backBtn.Click += (s, e) =>
            {
                try { if (webView?.CanGoBack == true) webView.Back(); } catch { }
            };
            fwdBtn.Click += (s, e) =>
            {
                try { if (webView?.CanGoForward == true) webView.Forward(); } catch { }
            };
            f5Btn.Click += (s, e) =>
            {
                try { webView?.Reload(); } catch { }
            };

            var navPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            navPanel.Children.Add(backBtn);
            navPanel.Children.Add(fwdBtn);
            navPanel.Children.Add(f5Btn);
            Grid.SetRow(navPanel, 0);
            Grid.SetColumn(navPanel, 0);
            topBarGrid.Children.Add(navPanel);

            // ── URL address bar pill (col 1) ─────────────────────────────────
            var urlPill = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(pillBg),
                BorderBrush = new SolidColorBrush(pillBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 0, 4, 0),
                Margin = new Thickness(4, 0, 4, 0),
                Height = 28,
                VerticalAlignment = VerticalAlignment.Center
            };
            var urlPillInner = new Grid();
            urlPillInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0: lock
            urlPillInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1: urlBox
            urlPillInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 2: spinner
            urlPillInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 3: goBtn

            var lockIcon = new TextBlock
            {
                Text = "🔒",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = textBrush,
                Opacity = 0.6
            };
            Grid.SetColumn(lockIcon, 0);
            urlPillInner.Children.Add(lockIcon);

            var urlBox = new TextBox
            {
                Height = 26,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Padding = new Thickness(0, 1, 0, 1),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = textBrush,
                CaretBrush = textBrush,
                SelectionBrush = new SolidColorBrush(Color.FromRgb(254, 224, 138)), // Nền vàng nhẹ mờ mờ kiểu Google Chrome
                SelectionOpacity = 0.45
            };
            Grid.SetColumn(urlBox, 1);
            urlPillInner.Children.Add(urlBox);

            var urlLoadingSpinner = new Border
            {
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "⏳",
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            Grid.SetColumn(urlLoadingSpinner, 2);
            urlPillInner.Children.Add(urlLoadingSpinner);

            var goBtn = CreateToolbarButton("→", "Đi tới (Enter)", 22, 22, 13);
            Grid.SetColumn(goBtn, 3);
            urlPillInner.Children.Add(goBtn);

            urlPill.Child = urlPillInner;
            Grid.SetRow(urlPill, 0);
            Grid.SetColumn(urlPill, 1);
            topBarGrid.Children.Add(urlPill);

            // ── Profile Selector & Management (col 2) ─────────────────────────
            var profilePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0)
            };

            var cmbWebProfile = new ComboBox
            {
                Height = 26,
                Width = 95,
                FontSize = 10,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0),
                ToolTip = "Chọn profile trình duyệt độc lập hoặc dùng chung (Shared)"
            };

            bool isUpdatingProfileCombo = false;
            void LoadProfileComboItems()
            {
                isUpdatingProfileCombo = true;
                try
                {
                    cmbWebProfile.Items.Clear();
                    if (string.Equals(node.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(node.CustomCacheName) &&
                        !string.Equals(node.CustomCacheName, "Shared", StringComparison.OrdinalIgnoreCase))
                    {
                        WebNodeCacheHelper.EnsureProfileExists(node.CustomCacheName);
                    }
                    var profiles = WebNodeCacheHelper.GetAvailableCacheProfiles();
                    var activeProfile = string.Equals(node.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase) 
                        ? (node.CustomCacheName ?? "Shared") 
                        : "Shared";

                    foreach (var p in profiles)
                    {
                        cmbWebProfile.Items.Add(p);
                    }

                    cmbWebProfile.SelectedItem = activeProfile;
                }
                finally
                {
                    isUpdatingProfileCombo = false;
                }
            }
            loadProfileComboItems = LoadProfileComboItems;

            LoadProfileComboItems();

            cmbWebProfile.SelectionChanged += (s, e) =>
            {
                if (isUpdatingProfileCombo) return;
                if (cmbWebProfile.SelectedItem is string selected)
                {
                    var targetMode = selected.Equals("Shared", StringComparison.OrdinalIgnoreCase) ? "Shared" : "Isolated";
                    var targetName = selected;

                    if (!string.Equals(node.CacheMode, targetMode, StringComparison.Ordinal) ||
                        !string.Equals(node.CustomCacheName, targetName, StringComparison.Ordinal))
                    {
                        node.CacheMode = targetMode;
                        node.CustomCacheName = targetName;
                        activeCacheMode = targetMode;
                        activeCustomCacheName = targetName;
                        recreateWebView?.Invoke();
                    }
                }
            };

            var btnNewProfile = CreateToolbarButton("＋", "Tạo profile mới", 24, 24, 12);
            btnNewProfile.Click += (s, e) =>
            {
                var input = Microsoft.VisualBasic.Interaction.InputBox("Nhập tên profile mới (ví dụ: Acc_Gmail_1):", "Tạo Profile Mới", "");
                var name = input?.Trim();
                if (string.IsNullOrWhiteSpace(name)) return;

                var invalidChars = System.IO.Path.GetInvalidFileNameChars();
                foreach (var c in invalidChars) name = name.Replace(c, '_');
                name = name.Replace(' ', '_');

                if (name.Equals("Shared", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Không thể tạo profile trùng tên 'Shared'.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var profilePath = WebNodeCacheHelper.GetProfileCachePath(name);
                Directory.CreateDirectory(profilePath);
                
                node.CacheMode = "Isolated";
                node.CustomCacheName = name;
                activeCacheMode = "Isolated";
                activeCustomCacheName = name;
                
                WebNodeCacheHelper.NotifyProfilesChanged();
                recreateWebView?.Invoke();
            };

            var btnDeleteProfile = CreateToolbarButton("✕", "Xóa profile đã chọn", 24, 24, 10);
            btnDeleteProfile.Click += (s, e) =>
            {
                var current = cmbWebProfile.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(current) || current.Equals("Shared", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Không thể xóa profile 'Shared' dùng chung.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirm = MessageBox.Show($"Bạn có chắc chắn muốn xóa vĩnh viễn profile '{current}' khỏi đĩa không?",
                    "Xác nhận xóa Profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                {
                    node.CacheMode = "Shared";
                    node.CustomCacheName = "Shared";
                    node.RaisePropertyChanged(nameof(node.CacheMode));
                    node.RaisePropertyChanged(nameof(node.CustomCacheName));
                    activeCacheMode = "Shared";
                    activeCustomCacheName = "Shared";

                    WebNodeCacheHelper.DeleteProfileCache(current);
                    LoadProfileComboItems();
                    recreateWebView?.Invoke();
                }
            };

            profilePanel.Children.Add(cmbWebProfile);
            profilePanel.Children.Add(btnNewProfile);
            profilePanel.Children.Add(btnDeleteProfile);

            Grid.SetRow(profilePanel, 0);
            Grid.SetColumn(profilePanel, 2);
            topBarGrid.Children.Add(profilePanel);

            // Đăng ký sự kiện lắng nghe khi danh sách Profile thay đổi ứng dụng toàn hệ thống
            EventHandler onProfilesChanged = (s, e) =>
            {
                border.Dispatcher.BeginInvoke(new Action(() => LoadProfileComboItems()), DispatcherPriority.Normal);
            };
            WebNodeCacheHelper.ProfilesChanged += onProfilesChanged;
            border.Unloaded += (_, _) =>
            {
                WebNodeCacheHelper.ProfilesChanged -= onProfilesChanged;
            };

            // ── Viewport Expand button (col 3) ──────────────────────────────
            var viewportExpandBtn = CreateToolbarButton("\uE740", "Phóng to vừa khung nhìn", 26, 26, 13, ViewportExpandIconFont);
            Grid.SetRow(viewportExpandBtn, 0);
            Grid.SetColumn(viewportExpandBtn, 3);
            topBarGrid.Children.Add(viewportExpandBtn);

            // ── Clear-cache Refresh button (col 4) ──────────────────────────
            var refreshBtn = CreateToolbarButton("", "Làm mới (xóa cookies + cache + storage rồi load lại)", 26, 26, 12, cornerRadius: new CornerRadius(5));
            refreshBtn.BorderThickness = new Thickness(1);
            refreshBtn.BorderBrush = new SolidColorBrush(textBrush == Brushes.White ? Color.FromArgb(80, 255, 255, 255) : Color.FromArgb(80, 0, 0, 0));
            refreshBtn.Background = new SolidColorBrush(textBrush == Brushes.White ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0));

            var iconConverter = new FlowMy.Converters.IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "trash-xmark light", System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var trashIcon = new FlowMy.Controls.SvgViewboxEx
            {
                Source = iconUri,
                Width = 14,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = textBrush
            };
            refreshBtn.Content = trashIcon;

            Grid.SetRow(refreshBtn, 0);
            Grid.SetColumn(refreshBtn, 4);
            topBarGrid.Children.Add(refreshBtn);

            // ── Google Suggest Autocomplete Popup (khai báo trước để dùng trong event handlers) ──
            var popupBg = textBrush == Brushes.White ? Color.FromRgb(0x28, 0x2C, 0x34) : Color.FromRgb(0xFA, 0xFA, 0xFA);
            var popupFg = textBrush == Brushes.White ? Brushes.White : Brushes.Black;
            var popupBorder = textBrush == Brushes.White ? Color.FromArgb(100, 255, 255, 255) : Color.FromArgb(100, 0, 0, 0);
            var popupHoverBg = textBrush == Brushes.White ? Color.FromArgb(60, 100, 180, 255) : Color.FromArgb(40, 100, 180, 255);
            var popupSelectedBg = textBrush == Brushes.White ? Color.FromArgb(90, 100, 180, 255) : Color.FromArgb(70, 100, 180, 255);

            var suggestListBox = new ListBox
            {
                Background = new SolidColorBrush(popupBg),
                Foreground = popupFg,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                MaxHeight = 240,
                FontSize = 12,
                Padding = new Thickness(0)
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(suggestListBox, ScrollBarVisibility.Disabled);

            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, popupFg));
            itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(popupHoverBg)));
            itemStyle.Triggers.Add(hoverTrigger);
            
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(popupSelectedBg)));
            itemStyle.Triggers.Add(selectedTrigger);
            suggestListBox.ItemContainerStyle = itemStyle;

            var suggestPopup = new Popup
            {
                PlacementTarget = urlPill,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Child = new Border
                {
                    Background = new SolidColorBrush(popupBg),
                    BorderBrush = new SolidColorBrush(popupBorder),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(0, 0, 8, 8),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 3, Opacity = 0.3 },
                    Child = suggestListBox
                },
                HorizontalOffset = 0
            };

            // Bind width của popup theo urlPill
            urlPill.SizeChanged += (s, e) => suggestPopup.Width = urlPill.ActualWidth;

            // ── Binding và event handlers của urlBox ────────────────────────────
            // Binding với LostFocus để không tự động update khi đang gõ
            urlBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(WebNode.ExtractUrl))
            {
                Source = node,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus
            });

            // PreviewKeyDown: Ctrl+A bôi đen toàn bộ chữ + Enter navigate + ↑↓ navigation trong popup
            urlBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    e.Handled = true;
                    urlBox.SelectAll();
                    return;
                }
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    if (suggestPopup.IsOpen && suggestListBox.SelectedItem is string sel && !sel.StartsWith("🔄"))
                    {
                        urlBox.Text = sel;
                        node.ExtractUrl = sel;
                    }
                    suggestPopup.IsOpen = false;
                    urlBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    EnsureWebViewAndNavigate();
                    return;
                }
                if (suggestPopup.IsOpen)
                {
                    if (e.Key == Key.Down)
                    {
                        e.Handled = true;
                        var idx = suggestListBox.SelectedIndex;
                        if (idx < suggestListBox.Items.Count - 1) suggestListBox.SelectedIndex = idx + 1;
                        suggestListBox.ScrollIntoView(suggestListBox.SelectedItem);
                        return;
                    }
                    if (e.Key == Key.Up)
                    {
                        e.Handled = true;
                        var idx = suggestListBox.SelectedIndex;
                        if (idx > 0) suggestListBox.SelectedIndex = idx - 1;
                        else suggestListBox.SelectedIndex = -1;
                        return;
                    }
                    if (e.Key == Key.Escape)
                    {
                        suggestPopup.IsOpen = false;
                        e.Handled = true;
                        return;
                    }
                }
            };

            // Focus glow: khi focus vào urlBox thì làm sáng border pill
            urlBox.GotFocus  += (s, e) => urlPill.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 100, 180, 255));
            urlBox.LostFocus += (s, e) =>
            {
                urlPill.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                // Delay ẩn popup để kịp xử lý click vào item trước khi popup đóng
                webView.Dispatcher.BeginInvoke(new Action(() => { suggestPopup.IsOpen = false; }), DispatcherPriority.Background);
            };

            // ── Click vào item: dùng PreviewMouseLeftButtonDown để bắt click TRƯỚC khi LostFocus đóng popup ──
            suggestListBox.PreviewMouseLeftButtonDown += (s, e) =>
            {
                // Tìm ListBoxItem từ phần tử được click
                var element = e.OriginalSource as DependencyObject;
                while (element != null && !(element is ListBoxItem))
                    element = VisualTreeHelper.GetParent(element);

                if (element is ListBoxItem lbi && lbi.Content is string selected && !selected.StartsWith("🔄"))
                {
                    e.Handled = true; // ngăn focus chuyển sang ListBox → LostFocus không fire sớm
                    urlBox.Text = selected;
                    suggestPopup.IsOpen = false;
                    urlBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    node.ExtractUrl = selected;
                    urlBox.Focus();
                    EnsureWebViewAndNavigate();
                }
            };

            // Debounce timer: đợi 350ms sau lần gõ cuối mới gọi API
            DispatcherTimer? suggestDebounce = null;
            string lastSuggestQuery = string.Empty;

            void ShowLoadingInPopup()
            {
                suggestListBox.Items.Clear();
                var loadingItem = new ListBoxItem
                {
                    Content = "🔄  Đang tải gợi ý...",
                    IsEnabled = false,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C)),
                    FontSize = 11,
                    Padding = new Thickness(10, 5, 10, 5)
                };
                suggestListBox.Items.Add(loadingItem);
                suggestPopup.IsOpen = urlBox.IsFocused;
            }

            async void FetchSuggestionsAsync(string query)
            {
                if (!urlBox.IsFocused)
                {
                    suggestPopup.IsOpen = false;
                    return;
                }

                query = query.Trim();
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    suggestPopup.IsOpen = false;
                    return;
                }
                if (query == lastSuggestQuery) return;
                lastSuggestQuery = query;

                // Hiển thị trạng thái đang tải ngay lập tức
                ShowLoadingInPopup();

                try
                {
                    var encoded = Uri.EscapeDataString(query);
                    var apiUrl = $"https://suggestqueries.google.com/complete/search?client=firefox&q={encoded}";
                    var json = await _suggestHttpClient.GetStringAsync(apiUrl);

                    // Parse: ["query", ["s1","s2",...], ...]
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var suggestions = new System.Collections.Generic.List<string>();

                    // Nếu query trông như URL → thêm vào đầu danh sách
                    var q = query.Trim();
                    var looksLikeUrl = q.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                                       (q.Contains('.') && !q.Contains(' '));
                    if (looksLikeUrl)
                    {
                        var candidate = q.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? q : "https://" + q;
                        suggestions.Add(candidate);
                    }

                    if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() >= 2)
                    {
                        var arr = root[1];
                        foreach (var el in arr.EnumerateArray())
                        {
                            var sg = el.GetString();
                            if (!string.IsNullOrWhiteSpace(sg) && !suggestions.Contains(sg))
                                suggestions.Add(sg);
                            if (suggestions.Count >= 8) break;
                        }
                    }

                    await webView.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        suggestListBox.Items.Clear();
                        foreach (var sg in suggestions)
                            suggestListBox.Items.Add(sg);
                        suggestPopup.IsOpen = suggestions.Count > 0 && urlBox.IsFocused;
                    }), DispatcherPriority.Normal);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Suggest] Error: {ex.Message}");
                    await webView.Dispatcher.BeginInvoke(new Action(() => { suggestPopup.IsOpen = false; }), DispatcherPriority.Normal);
                }
            }

            // TextChanged → khởi động debounce 350ms (chỉ chạy khi user chủ động focus gõ text)
            urlBox.TextChanged += (s, e) =>
            {
                if (!urlBox.IsFocused) return;

                suggestDebounce?.Stop();
                suggestDebounce = new DispatcherTimer(DispatcherPriority.Background, webView.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(350)
                };
                suggestDebounce.Tick += (_, _) =>
                {
                    suggestDebounce.Stop();
                    FetchSuggestionsAsync(urlBox.Text);
                };
                suggestDebounce.Start();
            };
            // ── End autocomplete ───────────────────────────────────────────────

            // Định nghĩa function navigate (phải sau urlBox)
            void EnsureWebViewAndNavigate()
            {
                if (webView == null) return;
                var urlFromTextBox = urlBox.Text?.Trim();
                var input = !string.IsNullOrEmpty(urlFromTextBox) ? urlFromTextBox : (node.ExtractUrl?.Trim());

                if (string.IsNullOrEmpty(input))
                {
                    try { webView.Load("about:blank"); } catch { }
                    return;
                }

                string targetUrl;
                if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    targetUrl = input;
                }
                else if (!input.Contains(' ') && input.Contains('.'))
                {
                    targetUrl = "https://" + input;
                }
                else
                {
                    targetUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
                }

                node.ExtractUrl = targetUrl;
                urlBox.Text = targetUrl;

                try
                {
                    if (webView.IsBrowserInitialized)
                    {
                        webView.Load(targetUrl);
                    }
                    else
                    {
                        System.Windows.DependencyPropertyChangedEventHandler? initHandler = null;
                        initHandler = (s, e) =>
                        {
                            webView.IsBrowserInitializedChanged -= initHandler;
                            webView.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { webView.Load(targetUrl); } catch { }
                            }));
                        };
                        webView.IsBrowserInitializedChanged += initHandler;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigate error: {ex.Message}");
                }
            }

            goBtn.Click += (s, e) =>
            {
                suggestPopup.IsOpen = false;
                urlBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                EnsureWebViewAndNavigate();
            };



            // ── Clear-cache Refresh button (col 4) — logic giữ nguyên ─────

            refreshBtn.Click += async (s, e) =>
            {
                if (webView == null) return;

                var result = MessageBox.Show(
                    "Bạn có muốn làm mới trang không? Tất cả dữ liệu (session, auth, cookies, cache) sẽ bị xóa.",
                    "Xác nhận làm mới",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var currentUrl = webView.Address;
                        
                        // Lấy domain từ URL hiện tại
                        string? domain = null;
                        if (!string.IsNullOrEmpty(currentUrl) && Uri.TryCreate(currentUrl, UriKind.Absolute, out var uri))
                        {
                            domain = uri.Host;
                        }

                        // 1. Clear cookies cho domain hiện tại (qua CookieManager) - phải làm TRƯỚC khi navigate
                        if (!string.IsNullOrEmpty(domain))
                        {
                            try
                            {
                                var cookieMgr = webView.RequestContext?.GetCookieManager(null) ?? Cef.GetGlobalCookieManager();
                                cookieMgr?.DeleteCookies(domain, null);
                            }
                            catch (Exception cookieEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Lỗi clear cookies: {cookieEx.Message}");
                            }
                        }

                        var clearStorageScript = @"
(function() {
    try {
        if (window.localStorage) window.localStorage.clear();
        if (window.sessionStorage) window.sessionStorage.clear();
    } catch (e) {}
})();";
                        try
                        {
                            webView.ExecuteScriptAsync(clearStorageScript);
                        }
                        catch (Exception jsEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Lỗi clear storage qua JS: {jsEx.Message}");
                        }

                        if (!string.IsNullOrEmpty(currentUrl))
                        {
                            webView.LoadUrl(currentUrl);
                        }
                        else
                        {
                            webView.Reload();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Lỗi làm mới trang: {ex.Message}");
                        MessageBox.Show(
                            $"Không thể làm mới trang: {ex.Message}",
                            "Lỗi",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            };
            viewportExpandBtn.Click += (_, _) => ToggleNodeViewportExpand(node, border, host, viewportExpandBtn);

            topBar.Child = topBarGrid;
            Grid.SetRow(topBar, 0);
            grid.Children.Add(topBar);
            grid.Children.Add(webView);

            // JavaScript để bật element inspector
            string BuildElementInspectorScript()
            {
                return @"
(function() {
  if (window.__elementInspector) return; // Đã được inject rồi

  let currentHighlighted = null;
  let originalBorder = '';
  let originalOutline = '';
  let originalBackground = '';

  function getXPath(element) {
    if (!element || element.nodeType !== 1) return '';
    
    // Luôn tạo full XPath, không dùng ID shortcut
    const parts = [];
    let current = element;
    
    while (current && current.nodeType === 1) {
      let index = 1;
      let sibling = current.previousSibling;
      
      while (sibling) {
        if (sibling.nodeType === 1 && sibling.nodeName === current.nodeName) {
          index++;
        }
        sibling = sibling.previousSibling;
      }
      
      const tagName = current.nodeName.toLowerCase();
      const part = tagName + '[' + index + ']';
      parts.unshift(part);
      current = current.parentNode;
    }
    
    return '/' + parts.join('/');
  }

  function getCssSelector(element) {
    if (!element || element.nodeType !== 1) return '';
    
    // 1. Check for unique identifiable attributes
    if (element.hasAttribute('placeholder')) return element.nodeName.toLowerCase() + '[placeholder=""' + element.getAttribute('placeholder') + '""]';
    if (element.hasAttribute('name')) return element.nodeName.toLowerCase() + '[name=""' + element.getAttribute('name') + '""]';
    if (element.hasAttribute('aria-label') && element.getAttribute('aria-label').length < 30) return element.nodeName.toLowerCase() + '[aria-label=""' + element.getAttribute('aria-label') + '""]';
    if (element.hasAttribute('contenteditable') && element.getAttribute('contenteditable') === 'true') return element.nodeName.toLowerCase() + '[contenteditable=""true""]';
    if (element.id && !/^\d/.test(element.id) && !element.id.includes(':')) return '#' + element.id;

    // 2. Fallback to path traversal with smart class selection
    let path = [];
    let current = element;
    while (current && current.nodeType === 1) {
      let selector = current.nodeName.toLowerCase();
      
      if (current.id && !/^\d/.test(current.id) && !current.id.includes(':')) {
        selector = '#' + current.id;
        path.unshift(selector);
        break; // Stop at first valid ID
      }
      
      // Try to find a meaningful semantic class, avoiding Tailwind utility classes
      if (current.className && typeof current.className === 'string') {
        let classes = current.className.split(/\s+/).filter(c => 
          c && c.length > 2 && 
          !/^(w-|h-|flex|grid|p-|m-|text-|bg-|border-|rounded-|hover:|focus:|dark:|absolute|relative|fixed|z-|isolate)/.test(c) && 
          !c.includes('[') && !c.includes(':') && !c.includes('/')
        );
        if (classes.length > 0) {
          selector += '.' + classes[0]; // Just use the first good semantic class
        }
      }

      let sibling = current;
      let nth = 1;
      while (sibling = sibling.previousElementSibling) {
        if (sibling.nodeName.toLowerCase() === current.nodeName.toLowerCase()) nth++;
      }
      // Only add nth-of-type if it helps uniqueness and we don't safely have a class
      if (nth != 1 || current === element) selector += ':nth-of-type(' + nth + ')';
      
      path.unshift(selector);
      
      // Stop early at major layout boundaries to prevent extremely long paths
      if (['body', 'main', 'nav', 'header', 'footer', 'form', 'dialog'].includes(current.nodeName.toLowerCase())) {
        break;
      }
      
      current = current.parentNode;
    }
    
    return path.join(' > ');
  }

  function flashElement(element) {
    // Lưu style gốc
    const origBorder = element.style.border;
    const origOutline = element.style.outline;
    const origBg = element.style.backgroundColor;
    
    let flashCount = 0;
    const maxFlashes = 3;
    
    function flash() {
      if (flashCount >= maxFlashes * 2) {
        // Restore original styles
        element.style.border = origBorder;
        element.style.outline = origOutline;
        element.style.backgroundColor = origBg;
        return;
      }
      
      if (flashCount % 2 === 0) {
        // Flash on - bright green
        element.style.border = '3px solid #00ff00';
        element.style.outline = '3px solid rgba(0, 255, 0, 0.5)';
        element.style.backgroundColor = 'rgba(0, 255, 0, 0.2)';
      } else {
        // Flash off - restore
        element.style.border = origBorder;
        element.style.outline = origOutline;
        element.style.backgroundColor = origBg;
      }
      
      flashCount++;
      setTimeout(flash, 150);
    }
    
    flash();
  }

  function highlightElement(e) {
    if (currentHighlighted && currentHighlighted !== e.target) {
      currentHighlighted.style.border = originalBorder;
      currentHighlighted.style.outline = originalOutline;
      currentHighlighted.style.backgroundColor = originalBackground;
    }
    
    currentHighlighted = e.target;
    originalBorder = e.target.style.border;
    originalOutline = e.target.style.outline;
    originalBackground = e.target.style.backgroundColor;
    
    e.target.style.border = '2px solid #00ff00';
    e.target.style.outline = '2px solid rgba(0, 255, 0, 0.3)';
  }

  function unhighlightElement(e) {
    if (currentHighlighted === e.target) {
      e.target.style.border = originalBorder;
      e.target.style.outline = originalOutline;
      e.target.style.backgroundColor = originalBackground;
      currentHighlighted = null;
    }
  }

  function copyToClipboard(text, type) {
    const copySuccess = () => {
      console.log(type + ' copied:', text);
      flashElement(currentHighlighted);
    };
    const copyError = (err) => {
      console.error('Failed to copy ' + type + ':', err);
    };
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(copySuccess).catch(copyError);
    } else {
      const textarea = document.createElement('textarea');
      textarea.value = text;
      textarea.style.position = 'fixed';
      textarea.style.opacity = '0';
      document.body.appendChild(textarea);
      textarea.select();
      try {
        document.execCommand('copy');
        copySuccess();
      } catch (err) {
        copyError(err);
      }
      document.body.removeChild(textarea);
    }
  }

  function handleKeyDown(e) {
    // Alt + Shift -> XPath
    if (e.altKey && e.shiftKey && currentHighlighted) {
      const xpath = getXPath(currentHighlighted);
      copyToClipboard(xpath, 'XPath');
      e.preventDefault();
      e.stopPropagation();
    }
    // Alt + ` -> CSS Selector
    else if (e.altKey && (e.key === '`' || e.code === 'Backquote' || e.keyCode === 192) && currentHighlighted) {
      const css = getCssSelector(currentHighlighted);
      copyToClipboard(css, 'CSS Selector');
      e.preventDefault();
      e.stopPropagation();
    }
  }

  document.addEventListener('mouseover', highlightElement, true);
  document.addEventListener('mouseout', unhighlightElement, true);
  document.addEventListener('keydown', handleKeyDown, true);

  window.__elementInspector = {
    cleanup: function() {
      document.removeEventListener('mouseover', highlightElement, true);
      document.removeEventListener('mouseout', unhighlightElement, true);
      document.removeEventListener('keydown', handleKeyDown, true);
      if (currentHighlighted) {
        currentHighlighted.style.border = originalBorder;
        currentHighlighted.style.outline = originalOutline;
        currentHighlighted.style.backgroundColor = originalBackground;
        currentHighlighted = null;
      }
    }
  };
})();";
            }

            // Hàm inject JavaScript vào ChromiumWebBrowser
            async void EnableElementInspector()
            {
                if (webView == null) return;
                try
                {
                    await webView.EvaluateScriptAsync(BuildElementInspectorScript());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Enable inspector error: {ex.Message}");
                }
            }

            // Hàm remove JavaScript khỏi ChromiumWebBrowser
            async void DisableElementInspector()
            {
                if (webView == null) return;
                try
                {
                    await webView.EvaluateScriptAsync(@"
if (window.__elementInspector) {
  window.__elementInspector.cleanup();
  delete window.__elementInspector;
}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Disable inspector error: {ex.Message}");
                }
            }

            RoutedEventHandler loadedHandler = null!;
            loadedHandler = async (s, e) =>
            {
                var webViewForInit = (ChromiumWebBrowser)s;
                try
                {
                    if (isDisposed || !border.IsLoaded)
                        return;

                    webViewForInit.RequestHandler = new FlowNetworkRequestHandler(node, host);
                    webViewForInit.KeyboardHandler = new FlowKeyboardHandler();
                    webViewForInit.MenuHandler = new FlowContextMenuHandler();

                    webViewForInit.MouseEnter += (sf, ef) =>
                    {
                        try { webViewForInit.Focus(); } catch { }
                    };

                    webViewForInit.PreviewMouseWheel += (sf, ef) =>
                    {
                        if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
                        {
                            ef.Handled = true;
                            try { webViewForInit.ZoomLevel += ef.Delta > 0 ? 0.1 : -0.1; } catch { }
                        }
                    };

                    webViewForInit.MouseWheel += (sf, ef) =>
                    {
                        // Stop mouse wheel bubbling to parent WPF canvas after CefSharp has scrolled web page
                        ef.Handled = true;
                    };

                    // Apply enqueued cookies from imported .webpkg.zip bundle BEFORE first navigation
                    // so the browser loads the page with the login session already set.
                    try
                    {
                        var profileName = string.Equals(node.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(node.CustomCacheName)
                            ? node.CustomCacheName
                            : "Shared";
                        ICookieManager? cookieMgr = webViewForInit.RequestContext?.GetCookieManager(null)
                            ?? Cef.GetGlobalCookieManager();
                        if (cookieMgr != null)
                        {
                            await WebCookiePortableBridge.TryConsumeAndApplyAsync(cookieMgr, profileName);
                        }
                    }
                    catch (Exception cookieEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebNode] Cookie restore from portable bridge error: {cookieEx.Message}");
                    }

                    EnsureWebViewAndNavigate();

                    if (!string.IsNullOrWhiteSpace(pendingJsQueue))
                    {
                        var js = pendingJsQueue;
                        pendingJsQueue = null;
                        await TryExecutePendingJsAsync(js);
                    }

                    webViewForInit.LoadingStateChanged += (sf, ef) =>
                    {
                        if (webViewForInit.Dispatcher.CheckAccess())
                        {
                            progressBar.Visibility = ef.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                            urlLoadingSpinner.Visibility = ef.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                        }
                        else
                        {
                            webViewForInit.Dispatcher.Invoke(() =>
                            {
                                progressBar.Visibility = ef.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                                urlLoadingSpinner.Visibility = ef.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                            });
                        }
                    };

                    webViewForInit.AddressChanged += (sf, ef) =>
                    {
                        var uri = ef.NewValue as string;
                        if (!string.IsNullOrEmpty(uri))
                        {
                            if (webViewForInit.Dispatcher.CheckAccess())
                                node.ExtractUrl = uri;
                            else
                                webViewForInit.Dispatcher.Invoke(() => node.ExtractUrl = uri);
                        }
                    };

                    webViewForInit.FrameLoadEnd += (sf, ef) =>
                    {
                        if (ef.Frame.IsMain)
                        {
                            try
                            {
                                const string dragDropScript = @"
(function() {
    if (window.__dragDropInterceptorInjected) return;
    window.__dragDropInterceptorInjected = true;
    const resetFlag = function() {
        if (window.resetDragState) window.resetDragState();
    };
    document.addEventListener('mouseup', resetFlag, true);
    document.addEventListener('pointerup', resetFlag, true);
    document.addEventListener('dragend', resetFlag, true);
})();";
                                ef.Frame.ExecuteJavaScriptAsync(dragDropScript);
                            }
                            catch { }

                            if (webViewForInit.Dispatcher.CheckAccess())
                                progressBar.Visibility = Visibility.Collapsed;
                            else
                                webViewForInit.Dispatcher.Invoke(() => progressBar.Visibility = Visibility.Collapsed);

                            if (node.EnableElementInspector || node.EnableCssSelectorInspector)
                            {
                                webViewForInit.Dispatcher.BeginInvoke(new Action(async () =>
                                {
                                    try
                                    {
                                        await System.Threading.Tasks.Task.Delay(500);
                                        if (node.EnableElementInspector || node.EnableCssSelectorInspector)
                                        {
                                            EnableElementInspector();
                                        }
                                    }
                                    catch { }
                                }), DispatcherPriority.Background);
                            }
                        }
                    };

                    webViewForInit.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (isDisposed || !border.IsLoaded) return;
                        if (webViewForInit.Visibility != Visibility.Visible)
                            webViewForInit.Visibility = Visibility.Visible;
                        UpdateWebViewZoomForCanvasZoom();
                        SyncWebViewPosition();
                    }), DispatcherPriority.Loaded);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WEB NODE error: {ex.Message}");
                }
            };

            recreateWebView = () =>
            {
                if (webView != null)
                {
                    try { FlowMy.Services.Workflow.CefSharpEnvironmentManager.FlushAllCookiesSync(); } catch { }
                    grid.Children.Remove(webView);
                    try { webView.Dispose(); } catch { }
                    webView = null!;
                }

                webView = new ChromiumWebBrowser
                {
                    Visibility = Visibility.Collapsed,
                    RequestContext = GetCurrentRequestContext()
                };
                Grid.SetRow(webView, 1);
                grid.Children.Add(webView);

                webView.PreviewMouseDown += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };
                webView.PreviewMouseWheel += (_, _) => { MarkActivity(); RestartSleepModeTimer(); };

                RenderOptions.SetBitmapScalingMode(webView, BitmapScalingMode.LowQuality);
                RenderOptions.SetEdgeMode(webView, EdgeMode.Unspecified);
                webView.UseLayoutRounding = true;
                webView.SnapsToDevicePixels = true;
                webView.CacheMode = null;

                webView.Loaded += loadedHandler;
            };

            // Đăng ký Loaded event cho webView ban đầu
            webView.Loaded += loadedHandler;

            string ResolveUrlPattern(WebNode webNode, string pattern)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    return pattern;

                // Kiểm tra xem pattern có chứa {variable} không
                if (!pattern.Contains('{'))
                    return pattern; // Không có biến, trả về nguyên pattern

                // Resolve tất cả input mappings thành dictionary: variableName -> value
                var variableValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var mappings = webNode.InputMappings ?? new List<WebInputMapping>();
                foreach (var mapping in mappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.SourceNodeId) || string.IsNullOrWhiteSpace(mapping.SourceOutputKey))
                        continue;

                    var variableName = mapping.EffectiveInputKey;
                    if (string.IsNullOrWhiteSpace(variableName))
                        continue;

                    // Resolve giá trị từ node nguồn thông qua host.ViewModel
                    string value = string.Empty;
                    try
                    {
                        if (host?.ViewModel != null && host.ViewModel.Nodes != null && host.ViewModel.Connections != null)
                        {
                            var sourceNode = host.ViewModel.Nodes.FirstOrDefault(n =>
                                string.Equals(n.Id, mapping.SourceNodeId, StringComparison.OrdinalIgnoreCase));

                            if (sourceNode != null)
                            {
                                // Resolve value từ source node (giống cách resolve trong WebResourceRequested)
                                value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, mapping.SourceOutputKey);

                                // Xử lý giá trị "—" thành empty
                                if (value == "—")
                                    value = string.Empty;
                            }
                        }
                    }
                    catch { }

                    variableValues[variableName] = value;
                }

                // Thay thế {variable} trong pattern
                // Dùng giá trị raw (Trim) để match với request URL từ browser - không encode để tránh mismatch
                var regexPattern = @"\{([^}]+)\}";
                var result = Regex.Replace(pattern, regexPattern, match =>
                {
                    var variableName = match.Groups[1].Value.Trim();

                    if (variableValues.TryGetValue(variableName, out var varValue) && varValue != null)
                    {
                        return varValue.Trim();
                    }

                    return match.Value;
                });

                return result;
            }

            // Chuyển pattern có {variable} chưa resolve thành regex: {var} → [^/]+ để match path segment
            string PatternToRegexForMatching(string p)
            {
                if (string.IsNullOrWhiteSpace(p)) return p;
                var regexPattern = @"(\{[^}]+\})";
                var parts = Regex.Split(p, regexPattern);
                var sb = new System.Text.StringBuilder();
                foreach (var part in parts)
                {
                    if (part.StartsWith("{") && part.EndsWith("}"))
                        sb.Append("[^/]+");
                    else if (!string.IsNullOrEmpty(part))
                        sb.Append(Regex.Escape(part));
                }
                return sb.ToString();
            }

            // Helper function để so sánh khớp URL pattern (không cần khớp toàn bộ)
            // 1) Resolve {variable} từ InputMappings. Nếu không còn { } → dùng IndexOf
            // 2) Nếu vẫn còn {variable} chưa resolve → dùng regex [^/]+ để match (URL có param từ node khác)
            bool UrlMatchesPattern(string url, string pattern)
            {
                url = url?.Trim() ?? "";
                pattern = pattern?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(url)) return false;

                var resolvedPattern = ResolveUrlPattern(node, pattern)?.Trim() ?? "";

                // System.Diagnostics.Debug.WriteLine($"Url web: {url} \r\n Url Chặn: {resolvedPattern}");

                if (string.IsNullOrWhiteSpace(resolvedPattern)) return false;

                if (!resolvedPattern.Contains('{'))
                    return url.IndexOf(resolvedPattern, StringComparison.OrdinalIgnoreCase) >= 0;

                var regexStr = PatternToRegexForMatching(pattern);
                try
                {
                    return Regex.IsMatch(url, regexStr, RegexOptions.IgnoreCase);
                }
                catch
                {
                    return url.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }

            // ── BOTTOM BAR (modernized) ──────────────────────────────────────
            var bottomBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(65, 0, 0, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                CornerRadius = new CornerRadius(0, 0, 10, 10),
                VerticalAlignment = VerticalAlignment.Bottom
            };

            // Grid: cột trái = zoom control, cột phải = text mô tả
            var bottomGrid = new Grid();
            bottomGrid.VerticalAlignment = VerticalAlignment.Bottom;
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Zoom panel (bottom-left)
            var zoomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var zoomLabel = new TextBlock
            {
                Text = "🔍",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Zoom trang"
            };
            zoomComboBox = new ComboBox
            {
                Width = 65,
                Height = 20,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Chọn mức zoom"
            };

            void AddZoomItem(double factor)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{factor * 100:0}%",
                    Tag = factor
                };
                zoomComboBox.Items.Add(item);
            }

            // Thêm nhiều mức zoom preset, bao gồm cả mức nhỏ (5%, 10%, 20%, 40%)
            AddZoomItem(0.05);
            AddZoomItem(0.10);
            AddZoomItem(0.20);
            AddZoomItem(0.40);
            AddZoomItem(0.50);
            AddZoomItem(0.75);
            AddZoomItem(1.00);
            AddZoomItem(1.25);
            AddZoomItem(1.50);
            AddZoomItem(2.00);

            // TextBox hiển thị phần trăm hiện tại
            var zoomTextBox = new TextBox
            {
                Width = 55,
                Height = 20,
                Margin = new Thickness(0, 0, 4, 0),
                IsReadOnly = true,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            zoomComboBox.SelectionChanged += (s, e) =>
            {
                if (zoomComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is double z)
                {
                    ApplyWebViewZoom(z);
                }
            };

            // Nút "-" và "+"
            double GetCurrentZoom()
            {
                if (node.CssZoom > 0) return node.CssZoom;
                if (_webViewZoomLevels.TryGetValue(border, out var v) && v > 0) return v;
                return 1.0;
            }

            double GetNextPreset(double current, bool increase)
            {
                // Chỉ dùng preset trong khoảng [0.1, 5.0] cho nút +/-.
                var presets = zoomComboBox.Items.OfType<ComboBoxItem>()
                    .Select(i => i.Tag)
                    .OfType<double>()
                    .Where(v => v >= 0.1 && v <= 5.0)
                    .OrderBy(v => v)
                    .ToList();
                if (presets.Count == 0) return current;

                if (increase)
                {
                    foreach (var p in presets)
                        if (p - current > 0.0001) return p;
                    return presets.Last();
                }
                else
                {
                    for (int i = presets.Count - 1; i >= 0; i--)
                        if (current - presets[i] > 0.0001) return presets[i];
                    return presets.First();
                }
            }

            // Pill-style zoom buttons
            static Button MakeZoomBtn(string content, string tip)
            {
                return new Button
                {
                    Content = content,
                    ToolTip = tip,
                    Width = 24,
                    Height = 22,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 2, 0),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            var minusButton = MakeZoomBtn("−", "Thu nhỏ");
            minusButton.Click += (s, e) =>
            {
                var current = GetCurrentZoom();
                var next = GetNextPreset(current, increase: false);
                ApplyWebViewZoom(next);
            };

            var plusButton = MakeZoomBtn("+", "Phóng to");
            plusButton.Click += (s, e) =>
            {
                var current = GetCurrentZoom();
                var next = GetNextPreset(current, increase: true);
                ApplyWebViewZoom(next);
            };

            // Chọn giá trị khởi đầu dựa trên CssZoom / mặc định 100%
            double initialZoom = node.CssZoom > 0 ? node.CssZoom : 1.0;
            foreach (var item in zoomComboBox.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is double d && Math.Abs(d - initialZoom) < 0.0001)
                {
                    zoomComboBox.SelectedItem = item;
                    break;
                }
            }
            if (zoomComboBox.SelectedItem == null)
            {
                // Nếu không trùng preset, thêm option custom
                var customItem = new ComboBoxItem
                {
                    Content = $"{initialZoom * 100:0}%",
                    Tag = initialZoom
                };
                zoomComboBox.Items.Add(customItem);
                zoomComboBox.SelectedItem = customItem;
            }

            zoomPanel.Children.Add(zoomLabel);
            zoomPanel.Children.Add(minusButton);
            zoomPanel.Children.Add(zoomComboBox);
            zoomPanel.Children.Add(plusButton);
            zoomPanel.Children.Add(zoomTextBox);

            // Inspector checkbox - bật/tắt chế độ hover element với border và copy XPath khi Alt+Shift
            var inspectorCheckBox = new CheckBox
            {
                Content = "🔎 XPath",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 11,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = node.EnableElementInspector
            };
            var xpathToolTipTemplate = new TextBlock();
            xpathToolTipTemplate.Inlines.Add(new System.Windows.Documents.Run("Bật chế độ inspector: hover element → highlight, "));
            xpathToolTipTemplate.Inlines.Add(new System.Windows.Documents.Run("Alt + Shift") { FontWeight = FontWeights.Bold });
            xpathToolTipTemplate.Inlines.Add(new System.Windows.Documents.Run(" để copy XPath"));
            inspectorCheckBox.ToolTip = new ToolTip { Content = xpathToolTipTemplate };
            ToolTipService.SetInitialShowDelay(inspectorCheckBox, 100);

            // CSS Selector inspector checkbox
            var cssInspectorCheckBox = new CheckBox
            {
                Content = "🔎 CSS Selector",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 11,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = node.EnableCssSelectorInspector
            };
            var cssToolTipTemplate = new TextBlock();
            cssToolTipTemplate.Inlines.Add(new System.Windows.Documents.Run("Bật chế độ inspector: hover element → highlight, "));
            cssToolTipTemplate.Inlines.Add(new System.Windows.Documents.Run("Alt + `") { FontWeight = FontWeights.Bold });
            cssToolTipTemplate.Inlines.Add(new System.Windows.Documents.Run(" để copy CSS Selector"));
            cssInspectorCheckBox.ToolTip = new ToolTip { Content = cssToolTipTemplate };
            ToolTipService.SetInitialShowDelay(cssInspectorCheckBox, 100);

            // Event handlers cho xpath checkbox
            inspectorCheckBox.Checked += (s, e) =>
            {
                node.EnableElementInspector = true;
                if (cssInspectorCheckBox.IsChecked == true) cssInspectorCheckBox.IsChecked = false;
                EnableElementInspector();
            };
            inspectorCheckBox.Unchecked += (s, e) =>
            {
                node.EnableElementInspector = false;
                if (!node.EnableCssSelectorInspector) DisableElementInspector();
            };

            // Event handlers cho css checkbox
            cssInspectorCheckBox.Checked += (s, e) =>
            {
                node.EnableCssSelectorInspector = true;
                if (inspectorCheckBox.IsChecked == true) inspectorCheckBox.IsChecked = false;
                EnableElementInspector();
            };
            cssInspectorCheckBox.Unchecked += (s, e) =>
            {
                node.EnableCssSelectorInspector = false;
                if (!node.EnableElementInspector) DisableElementInspector();
            };

            // Sync checkbox khi property thay đổi từ code
            if (node is INotifyPropertyChanged npcInspector)
            {
                npcInspector.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WebNode.EnableElementInspector))
                    {
                        inspectorCheckBox.IsChecked = node.EnableElementInspector;
                        if (node.EnableElementInspector || node.EnableCssSelectorInspector) EnableElementInspector();
                        else DisableElementInspector();
                    }
                    else if (e.PropertyName == nameof(WebNode.EnableCssSelectorInspector))
                    {
                        cssInspectorCheckBox.IsChecked = node.EnableCssSelectorInspector;
                        if (node.EnableElementInspector || node.EnableCssSelectorInspector) EnableElementInspector();
                        else DisableElementInspector();
                    }
                };
            }

            zoomPanel.Children.Add(inspectorCheckBox);
            zoomPanel.Children.Add(cssInspectorCheckBox);
            Grid.SetColumn(zoomPanel, 0);
            bottomGrid.Children.Add(zoomPanel);

            var bottomText = new TextBlock
            {
                Text = "CefSharp  •  Chuột phải → cấu hình",
                Foreground = new SolidColorBrush(Color.FromArgb(160, 0xB0, 0xBE, 0xC5)),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(bottomText, 1);
            bottomGrid.Children.Add(bottomText);

            bottomBar.Child = bottomGrid;
            Grid.SetRow(bottomBar, 2);
            grid.Children.Add(bottomBar);

            var handleOverlay = new Grid();
            AddResizeHandle(handleOverlay, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 2, 0, 0));

            var outerGrid = new Grid();
            outerGrid.Children.Add(grid);
            outerGrid.Children.Add(handleOverlay);

            // Áp dụng GPU optimization cho outerGrid
            GpuOptimizationHelper.ApplyToElement(outerGrid);

            border.Child = outerGrid;

            bool isResizing = false;
            ResizeDirection currentDir = ResizeDirection.None;
            Point resizeStart = default;
            double origW = 0, origH = 0, origX = 0, origY = 0;

            border.PreviewMouseDown += (s, e) =>
                    {
                        if (e.OriginalSource is Ellipse handle && handle.Tag is ResizeDirection dir)
                        {
                            isResizing = true;
                            currentDir = dir;
                            resizeStart = e.GetPosition(border.Parent as UIElement);
                            // Dùng ActualWidth/ActualHeight (kích thước render thực tế) thay vì node.Width/Height
                            // Tránh trường hợp node.Height < MinHeight (từ dữ liệu cũ) gây dead zone khi drag
                            origW = border.ActualWidth > 0 ? border.ActualWidth : Math.Max(border.MinWidth, node.Width);
                            origH = border.ActualHeight > 0 ? border.ActualHeight : Math.Max(border.MinHeight, node.Height);
                            origX = node.X;
                            origY = node.Y;
                            border.CaptureMouse();
                            e.Handled = true;
                        }
                    };

            border.PreviewMouseMove += (s, e) =>
            {
                if (!isResizing) return;
                var pos = e.GetPosition(border.Parent as UIElement);
                var dx = pos.X - resizeStart.X;
                var dy = pos.Y - resizeStart.Y;
                double newX = origX, newY = origY, newW = origW, newH = origH;
                var minH = border.MinHeight > 0 ? border.MinHeight : 200.0;
                var minW = border.MinWidth > 0 ? border.MinWidth : 280.0;
                switch (currentDir)
                {
                    case ResizeDirection.BottomRight:
                        newW = Math.Max(minW, origW + dx);
                        newH = Math.Max(minH, origH + dy);
                        break;
                    case ResizeDirection.TopLeft:
                        newW = Math.Max(minW, origW - dx);
                        newH = Math.Max(minH, origH - dy);
                        newX = origX + (origW - newW);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.TopRight:
                        newW = Math.Max(minW, origW + dx);
                        newH = Math.Max(minH, origH - dy);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.BottomLeft:
                        newW = Math.Max(minW, origW - dx);
                        newH = Math.Max(minH, origH + dy);
                        newX = origX + (origW - newW);
                        break;
                    case ResizeDirection.Right:
                        newW = Math.Max(minW, origW + dx);
                        break;
                    case ResizeDirection.Left:
                        newW = Math.Max(minW, origW - dx);
                        newX = origX + (origW - newW);
                        break;
                    case ResizeDirection.Bottom:
                        newH = Math.Max(minH, origH + dy);
                        break;
                    case ResizeDirection.Top:
                        newH = Math.Max(minH, origH - dy);
                        newY = origY + (origH - newH);
                        break;
                }
                node.Width = newW;
                node.Height = newH;
                node.X = newX;
                node.Y = newY;
                border.Width = newW;
                border.Height = newH;
                if (host.WorkflowCanvas != null)
                {
                    Canvas.SetLeft(border, newX);
                    Canvas.SetTop(border, newY);
                }
                e.Handled = true;
            };

            border.PreviewMouseUp += (s, e) =>
            {
                if (isResizing) { isResizing = false; border.ReleaseMouseCapture(); e.Handled = true; }
            };

            // WebView2 (HwndHost) có thể gọi SetCapture() trên Win32 HWND của nó, làm mất WPF mouse capture
            // Khi đang resize, recapture ngay lập tức để đảm bảo PreviewMouseMove tiếp tục nhận events
            border.LostMouseCapture += (s, e) =>
            {
                if (isResizing)
                    border.CaptureMouse();
            };

            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Web",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode,
                    node.TitleColorKey,
                    node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always
                    ? Visibility.Visible
                    : Visibility.Collapsed,
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            // --- Node-specific property handlers for WebNode-specific properties ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                // Width/Height: sync border size and scale UI elements
                [nameof(WebNode.Width)] = ctx =>
                {
                    if (!isResizing)
                    {
                        border.Width = node.Width;
                        border.Height = node.Height;
                    }
                },
                [nameof(WebNode.Height)] = ctx =>
                {
                    if (!isResizing)
                    {
                        border.Width = node.Width;
                        border.Height = node.Height;
                    }

                    // Scale UI elements ở topBar và bottomBar theo Height
                    var heightBaseline = border.MinHeight > 0 ? border.MinHeight : 200.0;
                    var rawScale = heightBaseline > 0 ? node.Height / heightBaseline : 1.0;
                    var topBottomScaleFactor = Math.Max(1.0, rawScale);

                    topBarGrid.LayoutTransform = new ScaleTransform(topBottomScaleFactor, topBottomScaleFactor);
                    bottomGrid.LayoutTransform = new ScaleTransform(topBottomScaleFactor, topBottomScaleFactor);
                    UpdateInteractionVisualScale(handleOverlay, node, topBottomScaleFactor);
                }
            };

            // --- Initialize with fluent API (replaces duplicated title/hover/keyboard/property/visibility/canvas event handlers) ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new WebNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            // --- WebView2-specific Unloaded cleanup (in addition to BaseNodeControlHelper cleanup) ---
            border.Unloaded += (s, e) =>
            {
                isDisposed = true;
                try
                {
                    // Đóng WebView2 khi node bị xóa: dừng media (nhạc, video) và giải phóng tài nguyên
                    try
                    {
                        if (webView != null)
                            webView.LoadUrl("about:blank");
                    }
                    catch { }
                    try { webView.Dispose(); } catch { }

                    // Cleanup zoom level tracking
                    _webViewZoomLevels.Remove(border);

                    if (_titleUpdateTimers.TryGetValue(border, out var t)) { t.Stop(); _titleUpdateTimers.Remove(border); }
                    _titleUpdatedAfterZoom.Remove(border);
                    _viewportExpandRestore.Remove(border);
                    scaleDescriptor?.RemoveValueChanged(host.ScaleTransform, scaleChangedHandler);
                    translateXDescriptor?.RemoveValueChanged(host.TranslateTransform, translateChangedHandler);
                    translateYDescriptor?.RemoveValueChanged(host.TranslateTransform, translateChangedHandler);
                    System.Windows.Media.CompositionTarget.Rendering -= renderingHandler;
                }
                catch { }
            };

            // --- WebView2-specific LayoutUpdated handler (manages WebView2 visibility during zoom/pan/drag) ---
            border.LayoutUpdated += (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                {
                    // Ẩn WebView2 khi border không visible
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    return;
                }

                bool isZooming = NodeChrome.IsZooming;

                // Xử lý zoom: ẩn WebView2 và title để tránh nháy
                if (isZooming)
                {
                    _titleUpdatedAfterZoom[border] = false;
                    // Ẩn WebView2 khi đang zoom để tránh nháy
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    return;
                }

                // Sau khi zoom xong, hiển thị lại WebView2 và sync
                if (!_titleUpdatedAfterZoom.TryGetValue(border, out var up) || !up)
                {
                    _titleUpdatedAfterZoom[border] = true;
                    // Hiển thị lại WebView2 sau khi zoom xong
                    if (webView.Visibility != Visibility.Visible)
                        webView.Visibility = Visibility.Visible;
                    // Sync WebView2 sau khi zoom xong
                    SyncWebViewPosition();
                }

                // Xử lý panning/dragging: ẩn WebView2 khi đang di chuyển, hiển thị lại khi dừng
                bool isPanningOrDragging = host.DraggedNode == node || host.IsPanning;

                if (isPanningOrDragging)
                {
                    // Ẩn WebView2 khi đang pan/drag để tránh nháy
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    return; // Return sớm để tránh update title khi đang pan/drag
                }
                else
                {
                    // Hiển thị lại và sync WebView2 sau khi dừng pan/drag
                    if (webView.Visibility != Visibility.Visible)
                    {
                        webView.Visibility = Visibility.Visible;
                        SyncWebViewPosition();
                    }
                }
            };

            // --- Extra Loaded initialization (scale UI elements based on node height) ---
            border.Loaded += (s, e) =>
            {
                var loadedBaseline = border.MinHeight > 0 ? border.MinHeight : 200.0;
                var loadedRawScale = loadedBaseline > 0 ? node.Height / loadedBaseline : 1.0;
                var loadedScale = Math.Max(1.0, loadedRawScale);
                topBarGrid.LayoutTransform = new ScaleTransform(loadedScale, loadedScale);
                bottomGrid.LayoutTransform = new ScaleTransform(loadedScale, loadedScale);
                UpdateInteractionVisualScale(handleOverlay, node, loadedScale);
            };

            return border;
        }

        private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = direction,
                Cursor = GetCursorForResizeDirection(direction),
                CacheMode = null // Tránh ghosting
            };

            // Áp dụng GPU optimization cho resize handle
            GpuOptimizationHelper.ApplyToShape(handle);

            grid.Children.Add(handle);
        }

        private static void UpdateInteractionVisualScale(Grid handleOverlay, WorkflowNode node, double rawScale)
        {
            // Tăng nhẹ để resize handles/ports dễ nhìn khi node được phóng to.
            var visualScale = Math.Max(1.0, Math.Min(2.8, rawScale * 1.2));

            if (handleOverlay != null)
            {
                foreach (var child in handleOverlay.Children)
                {
                    if (child is Ellipse handle && handle.Tag is ResizeDirection)
                    {
                        handle.RenderTransformOrigin = new Point(0.5, 0.5);
                        handle.RenderTransform = new ScaleTransform(visualScale, visualScale);
                    }
                }
            }

            if (node?.Ports != null)
            {
                foreach (var p in node.Ports)
                {
                    if (p?.PortUI is FrameworkElement portUi)
                    {
                        portUi.RenderTransformOrigin = new Point(0.5, 0.5);
                        portUi.RenderTransform = new ScaleTransform(visualScale, visualScale);
                    }
                }
            }
        }

        private static Cursor GetCursorForResizeDirection(ResizeDirection direction)
        {
            return direction switch
            {
                ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
                ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
                _ => Cursors.Arrow
            };
        }

        private static Rect GetWorkflowViewportCanvasRect(IWorkflowEditorHost host)
        {
            var sv = host.ScrollViewer;
            if (sv == null) return Rect.Empty;
            try { sv.UpdateLayout(); } catch { /* ignore */ }
            double scrollX = sv.HorizontalOffset;
            double scrollY = sv.VerticalOffset;
            double vw = sv.ViewportWidth > 1 ? sv.ViewportWidth : sv.ActualWidth;
            double vh = sv.ViewportHeight > 1 ? sv.ViewportHeight : sv.ActualHeight;
            if (vw < 1 || vh < 1) return Rect.Empty;
            double z = host.ScaleTransform?.ScaleX ?? 1.0;
            if (z <= 0.0001) z = 1.0;
            double tx = host.TranslateTransform?.X ?? 0;
            double ty = host.TranslateTransform?.Y ?? 0;
            double canvasLeft = (scrollX - tx) / z;
            double canvasTop = (scrollY - ty) / z;
            double canvasW = vw / z;
            double canvasH = vh / z;
            if (double.IsNaN(canvasLeft) || double.IsInfinity(canvasLeft) ||
                double.IsNaN(canvasTop) || double.IsInfinity(canvasTop)) return Rect.Empty;
            return new Rect(canvasLeft, canvasTop, canvasW, canvasH);
        }

        private static void SetViewportExpandButtonState(Button btn, Border border)
        {
            bool expanded = _viewportExpandRestore.ContainsKey(border);
            btn.FontFamily = ViewportExpandIconFont;
            btn.Content = expanded ? "\uE73F" : "\uE740";
            btn.ToolTip = expanded
                ? "Thu nhỏ về kích thước và vị trí ban đầu"
                : "Phóng to vừa khung nhìn";
        }

        private static void ToggleNodeViewportExpand(WebNode node, Border border, IWorkflowEditorHost host, Button btn)
        {
            // Nếu đang phóng to → thu nhỏ về kích thước trước khi phóng
            if (_viewportExpandRestore.TryGetValue(border, out var saved))
            {
                node.X = saved.x;
                node.Y = saved.y;
                node.Width = saved.w;
                node.Height = saved.h;
                border.Width = saved.w;
                border.Height = saved.h;
                _viewportExpandRestore.Remove(border);
                host.UpdateNodePosition(node, saved.x, saved.y);
                host.UpdateCanvasSize();
                node.IsViewportExpanded = false;
                if (host is WorkflowEditorWindow win)
                    win.SetViewportExpandedUiHidden(false);
                SetViewportExpandButtonState(btn, border);
                return;
            }

            // Khi phóng to: tắt chế độ nghỉ để tránh node bị sleep khi đang focus/đang xem.
            if (node.EnableSleepMode)
            {
                node.EnableSleepMode = false;
                node.RequestWake();
            }

            node.IsViewportExpanded = true;
            if (host is WorkflowEditorWindow win0)
                win0.SetViewportExpandedUiHidden(true);

            bool TryExpandToViewport()
            {
                var r = GetWorkflowViewportCanvasRect(host);
                if (r.IsEmpty || r.Width < 1 || r.Height < 1) return false;
                _viewportExpandRestore[border] = (node.X, node.Y, node.Width, node.Height);
                var minW = border.MinWidth > 0 ? border.MinWidth : 1;
                var minH = border.MinHeight > 0 ? border.MinHeight : 1;
                var w = Math.Max(r.Width, minW);
                var h = Math.Max(r.Height, minH);
                node.X = r.Left;
                node.Y = r.Top;
                node.Width = w;
                node.Height = h;
                border.Width = w;
                border.Height = h;
                host.UpdateNodePosition(node, r.Left, r.Top);
                host.UpdateCanvasSize();
                SetViewportExpandButtonState(btn, border);
                return true;
            }

            if (TryExpandToViewport()) return;
            host.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (_viewportExpandRestore.ContainsKey(border)) return;
                if (!TryExpandToViewport())
                {
                    if (host is WorkflowEditorWindow win1)
                        win1.SetViewportExpandedUiHidden(false);
                }
            }));
        }

        /// <summary>
        /// Khi WebNode bắt được một output mới (ví dụ CurlCmd với key LoginCurl),
        /// tự động trigger RunSingleNode cho các node phụ thuộc (HttpRequestNode, OutputNode...)
        /// đang bind tới WebNode.Id + outputKey tương ứng.
        /// </summary>
        private static void TryTriggerDependentNodes(IWorkflowEditorHost? host, WebNode sourceNode, string outputKey)
        {
            if (host?.ViewModel == null) return;
            var vm = host.ViewModel;

            // ⚠️ QUAN TRỌNG: Khi Workflow đang trong quá trình chạy tự động (vm.IsExecuting == true),
            // việc luân chuyển các node tiếp theo do WorkflowExecutionService quản lý theo đúng thứ tự.
            // TUYỆT ĐỐI KHÔNG trigger RunSingleNode ở đây vì sẽ khiến node tiếp theo chạy vượt thứ tự trước khi WebNode kết thúc!
            if (vm.IsExecuting)
            {
                return;
            }

            var nodes = vm.Nodes;
            if (nodes == null || nodes.Count == 0) return;

            foreach (var n in nodes)
            {
                if (n is FlowMy.Models.Nodes.HttpRequestNode http)
                {
                    if (string.Equals(http.CurlSourceNodeId, sourceNode.Id, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(http.CurlSourceOutputKey, outputKey, StringComparison.OrdinalIgnoreCase))
                    {
                        host.RequestRunSingleNode(http);
                    }
                }
                else if (n is FlowMy.Models.Nodes.OutputNode outputNode)
                {
                    if (outputNode.InputVariables != null &&
                        outputNode.InputVariables.Any(v =>
                            string.Equals(v.SourceNodeId, sourceNode.Id, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(v.SourceOutputKey, outputKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        host.RequestRunSingleNode(outputNode);
                    }
                }
            }
        }

        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;
            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);
            if (port == null || port.Position == newPosition) return;
            port.Position = newPosition;
            host.UpdatePortsPositionOnSide(node, newPosition);
            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                }
                catch { }
            }
        }
    }
}