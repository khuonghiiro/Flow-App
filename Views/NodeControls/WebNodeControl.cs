using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow;
using FlowMy.Views.Overlays;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
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
        private const int TitleUpdateThrottleMs = 50;
        private static readonly Dictionary<Border, bool> _titleUpdatedAfterZoom = new();
        private static readonly Dictionary<Border, (double x, double y, double w, double h)> _viewportExpandRestore = new();
        private static readonly FontFamily ViewportExpandIconFont = new("Segoe MDL2 Assets");
        private static readonly Dictionary<Border, double> _webViewZoomLevels = new(); // Lưu zoom level của từng WebView2
        // Tránh init đồng thời nhiều WebView2 khi mở workflow lớn (giảm khựng UI).
        private static readonly System.Threading.SemaphoreSlim _webView2InitGate = new(1, 1);
        private static int _webViewInitSequence;
        private const int WebViewInitStaggerMs = 120;
        private const int WebViewInitStaggerMaxMs = 2200;

        // Lưu zoom theo tên miền (domain) trong phạm vi workflow hiện tại
        private static readonly Dictionary<string, double> _domainZoomByHost = new(StringComparer.OrdinalIgnoreCase);

        // Cache request payloads để dùng cho Payload extraction (key = requestUrl|requestMethod)
        private static readonly Dictionary<string, string> _requestPayloadCache = new();

        // Cache "full" request headers/body captured from DevTools Protocol (CDP Network domain).
        // WebResourceRequested headers are often incomplete compared to browser DevTools.
        private sealed class CdpRequestInfo
        {
            public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string? PostData { get; set; }
            public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CdpRequestInfo> _cdpByUrlMethod
            = new(StringComparer.OrdinalIgnoreCase);

        private static bool TryGetCdpRequestInfo(string url, string method, out Dictionary<string, string> headers, out string? postData)
        {
            headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            postData = null;
            if (string.IsNullOrWhiteSpace(url)) return false;
            var m = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim();
            var key = $"{url}|{m}";
            if (!_cdpByUrlMethod.TryGetValue(key, out var info) || info == null) return false;
            try
            {
                headers = new Dictionary<string, string>(info.Headers ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
                postData = info.PostData;
                return headers.Count > 0 || !string.IsNullOrWhiteSpace(postData);
            }
            catch
            {
                return false;
            }
        }

        private static async Task EnsureCoreWebView2ThrottledAsync(WebView2 target, CoreWebView2Environment env)
        {
            await _webView2InitGate.WaitAsync();
            try
            {
                await target.EnsureCoreWebView2Async(env);
            }
            finally
            {
                _webView2InitGate.Release();
            }
        }

        private static int GetInitStaggerDelayMs()
        {
            var sequence = System.Threading.Interlocked.Increment(ref _webViewInitSequence);
            var delay = (sequence - 1) * WebViewInitStaggerMs;
            if (delay < 0) return 0;
            return Math.Min(delay, WebViewInitStaggerMaxMs);
        }

        private static bool ShouldUseViewportLazyInit()
        {
            try
            {
                var prefs = CanvasToolbarPreferencesStore.Load();
                return string.Equals(prefs.CanvasDisplayMode, "ViewportOnly", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parse cookie text và set vào WebView2. Hỗ trợ nhiều format:
        /// - JSON object format: {"url":"...", "cookies":[{name, value, domain, ...}]}
        /// - JSON array format: [{name, value, domain, path, ...}, ...]
        /// - Netscape format (# Netscape HTTP Cookie File hoặc tab-separated)
        /// - Raw cookie string (name=value; name2=value2)
        /// Trả về URL nếu tìm thấy trong cookie text, null nếu không có.
        /// </summary>
        private static async Task<string?> SetCookiesFromTextAsync(CoreWebView2 coreWebView2, string cookieText)
        {
            if (string.IsNullOrWhiteSpace(cookieText)) return null;

            string? extractedUrl = null;
            var cookieManager = coreWebView2.CookieManager;
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

                                        var cookie = cookieManager.CreateCookie(name, value ?? "", domain, path ?? "/");
                                        cookie.IsSecure = secure;
                                        cookie.IsHttpOnly = httpOnly;
                                        cookieManager.AddOrUpdateCookie(cookie);
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
                                    
                                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(domain)) continue;

                                    var cookie = cookieManager.CreateCookie(name, value ?? "", domain, path ?? "/");
                                    cookie.IsSecure = secure;
                                    cookie.IsHttpOnly = httpOnly;
                                    cookieManager.AddOrUpdateCookie(cookie);
                                    System.Diagnostics.Debug.WriteLine($"[Cookie] Added from JSON array: {name}={value?.Substring(0, Math.Min(20, value?.Length ?? 0))}... (domain: {domain})");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Cookie] Error parsing JSON cookie: {ex.Message}");
                                }
                            }
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

                                var cookie = cookieManager.CreateCookie(name, value, domain, path);
                                cookie.IsSecure = secure;
                                cookieManager.AddOrUpdateCookie(cookie);
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

                // Nếu không có domain, thử extract từ URL
                if (string.IsNullOrWhiteSpace(cookieDomain) && !string.IsNullOrWhiteSpace(extractedUrl))
                {
                    if (Uri.TryCreate(extractedUrl, UriKind.Absolute, out var uri))
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
                                        var cookie = cookieManager.CreateCookie(name, value, cookieDomain, "/");
                                        cookieManager.AddOrUpdateCookie(cookie);
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

            var webView = new WebView2
            {
                // Ẩn WebView2 ban đầu để tránh block UI thread khi khởi tạo
                Visibility = Visibility.Collapsed
            };
            var isDisposed = false;
            Grid.SetRow(webView, 1);

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
                if (webView.CoreWebView2 == null)
                {
                    pendingJsQueue = js;
                    return;
                }

                try
                {
                    // Reset flag trước mỗi lần chạy JS để tránh bị ảnh hưởng bởi lần chạy trước.
                    try
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync("window.__FlowMyWorkflowDone = false;");
                    }
                    catch { /* ignore */ }

                    // Ensure helper exists
                    await webView.CoreWebView2.ExecuteScriptAsync(BuildAutomationHelperScript());

                    // Execute user script (wrapped to support await)
                    var wrapped = WrapUserScript(js);
                    await webView.CoreWebView2.ExecuteScriptAsync(wrapped);

                    // Nếu JS có đặt cờ đặc biệt window.__FlowMyWorkflowDone = true
                    // thì coi như WebNode đã hoàn thành và cho phép workflow tiếp tục ngay,
                    // bỏ qua việc chờ ResponseOutputsWaitTimeoutMs.
                    try
                    {
                        var flagResult = await webView.CoreWebView2.ExecuteScriptAsync("window.__FlowMyWorkflowDone === true");
                        // ExecuteScriptAsync trả về JSON-stringified; với boolean true sẽ là "true"
                        if (string.Equals(flagResult, "true", StringComparison.OrdinalIgnoreCase))
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

                    var core = webView.CoreWebView2;
                    if (core != null)
                    {
                        suppressUrlSyncForSleepNav = true;
                        core.Navigate("about:blank");
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
                    var core = webView.CoreWebView2;
                    var currentUrl = core?.Source;
                    if (core != null && (string.IsNullOrWhiteSpace(currentUrl) || string.Equals(currentUrl, "about:blank", StringComparison.OrdinalIgnoreCase)))
                    {
                        var targetUrl = node.ExtractUrl?.Trim();
                        if (!string.IsNullOrWhiteSpace(targetUrl) && targetUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            core.Navigate(targetUrl);
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
                        var core = webView.CoreWebView2;
                        if (core != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AutoReload] Reloading page (interval={interval.TotalSeconds:0.##}s)...");
                            core.Reload();
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
                        // User clicked "Chạy" button - apply cookie now
                        webView.Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            MarkActivity();
                            await WakeRuntimeAsync();
                            if (webView.CoreWebView2 != null && !string.IsNullOrWhiteSpace(node.CookieText))
                            {
                                System.Diagnostics.Debug.WriteLine("[Cookie] User clicked 'Chạy' button - applying cookies...");
                                var extractedUrl = await SetCookiesFromTextAsync(webView.CoreWebView2, node.CookieText);
                                
                                if (!string.IsNullOrWhiteSpace(extractedUrl))
                                {
                                    try
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Cookie] Navigating to: {extractedUrl}");
                                        node.ExtractUrl = extractedUrl;
                                        webView.CoreWebView2.Navigate(extractedUrl);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Cookie] Error navigating: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("[Cookie] No URL found in cookie text - cookies applied but no navigation");
                                }
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

            // Tối ưu WebView2 cho GPU: disable software rendering, enable hardware acceleration
            // WebView2 mặc định đã dùng GPU nhưng có thể tối ưu thêm
            if (GpuDetectionHelper.IsGpuAvailable)
            {
                // Áp dụng GPU-friendly render options cho WebView2 container
                RenderOptions.SetBitmapScalingMode(webView, BitmapScalingMode.Unspecified);
                RenderOptions.SetCachingHint(webView, CachingHint.Unspecified);
                webView.CacheMode = null; // Tránh ghosting
            }

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
                    var coreLocal = webView.CoreWebView2;
                    if (coreLocal == null) return;

                    var script = $@"
                        (function() {{
                            document.body.style.zoom = '{zoomFactor.ToString(System.Globalization.CultureInfo.InvariantCulture)}';
                            if (!document.body.style.zoom) {{
                                document.body.style.transform = 'scale({zoomFactor.ToString(System.Globalization.CultureInfo.InvariantCulture)})';
                                document.body.style.transformOrigin = 'top left';
                            }}
                        }})();
                    ";
                    coreLocal.ExecuteScriptAsync(script);
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
                    var core = webView.CoreWebView2;
                    if (core == null) return;

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
                    
                    // Set zoom qua CSS
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
                    core.ExecuteScriptAsync(script);

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

            // ── TOP BAR (modernized) ─────────────────────────────────────────
            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
                Padding = new Thickness(6, 5, 6, 5),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            // 2 rows: row0 = toolbar controls, row1 = progress bar
            var topBarGrid = new Grid();
            topBarGrid.VerticalAlignment = VerticalAlignment.Top;
            topBarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            topBarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            // Columns: [back+fwd] [url pill] [F5] [Go] [clearRefresh] [viewport expand]
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // 0: nav btns
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1: url
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // 2: F5
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // 3: Go
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // 4: Clear+Refresh
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // 5: phóng to khung nhìn

            // Slim gradient progress bar (row 1, spans all)
            var progressBar = new ProgressBar
            {
                Height = 3,
                Margin = new Thickness(0, 3, 0, 0),
                Visibility = Visibility.Collapsed,
                IsIndeterminate = true,
                Foreground = node.NodeBrush ?? (Application.Current.TryFindResource("BurgundyWineBrush") as Brush)
            };
            Grid.SetRow(progressBar, 1);
            Grid.SetColumnSpan(progressBar, 6);
            topBarGrid.Children.Add(progressBar);

            // ── Back / Forward nav buttons (col 0) ──────────────────────────
            static Button MakeNavBtn(string content, string tip)
            {
                return new Button
                {
                    Content = content,
                    ToolTip = tip,
                    Width = 26,
                    Height = 26,
                    FontSize = 13,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 2, 0),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            var backBtn = MakeNavBtn("◀", "Quay lại");
            var fwdBtn  = MakeNavBtn("▶", "Tiến tới");
            backBtn.Click += (s, e) =>
            {
                try { if (webView.CoreWebView2?.CanGoBack == true) webView.CoreWebView2.GoBack(); } catch { }
            };
            fwdBtn.Click += (s, e) =>
            {
                try { if (webView.CoreWebView2?.CanGoForward == true) webView.CoreWebView2.GoForward(); } catch { }
            };

            var navPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            navPanel.Children.Add(backBtn);
            navPanel.Children.Add(fwdBtn);
            Grid.SetRow(navPanel, 0);
            Grid.SetColumn(navPanel, 0);
            topBarGrid.Children.Add(navPanel);

            // ── URL address bar pill (col 1) ─────────────────────────────────
            // Outer pill border wrapping lock icon + text box
            var urlPill = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 0, 6, 0),
                Margin = new Thickness(0, 0, 6, 0),
                Height = 28,
                VerticalAlignment = VerticalAlignment.Center
            };
            var urlPillInner = new Grid();
            urlPillInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            urlPillInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });


            var lockIcon = new TextBlock
            {
                Text = "🔒",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Opacity = 0.7
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
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 100, 180, 255))
            };
            Grid.SetColumn(urlBox, 1);
            urlPillInner.Children.Add(urlBox);
            urlPill.Child = urlPillInner;
            Grid.SetRow(urlPill, 0);
            Grid.SetColumn(urlPill, 1);
            topBarGrid.Children.Add(urlPill);

            // ── Google Suggest Autocomplete Popup (khai báo trước để dùng trong event handlers) ──
            var suggestListBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x28, 0x2C, 0x34)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                MaxHeight = 240,
                FontSize = 12,
                Padding = new Thickness(0)
            };
            // Attached property phải set riêng (không thể dùng trong object initializer)
            ScrollViewer.SetHorizontalScrollBarVisibility(suggestListBox, ScrollBarVisibility.Disabled);

            // Style từng item trong danh sách
            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 100, 180, 255))));
            itemStyle.Triggers.Add(hoverTrigger);
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(90, 100, 180, 255))));
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
                    Background = new SolidColorBrush(Color.FromRgb(0x28, 0x2C, 0x34)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(0, 0, 8, 8),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 3, Opacity = 0.5 },
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

            // PreviewKeyDown: popup navigation (↑↓/Escape/Enter item) + Enter navigate bình thường
            urlBox.PreviewKeyDown += (s, e) =>
            {
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
                    // Enter: chọn item đang focus
                    if (e.Key == Key.Enter && suggestListBox.SelectedItem is string sel)
                    {
                        e.Handled = true;
                        urlBox.Text = sel;
                        suggestPopup.IsOpen = false;
                        urlBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                        node.ExtractUrl = sel;
                        EnsureWebViewAndNavigate();
                        return;
                    }
                }
                // Enter bình thường khi popup đóng
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    suggestPopup.IsOpen = false;
                    urlBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    EnsureWebViewAndNavigate();
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

            // TextChanged → khởi động debounce 350ms
            urlBox.TextChanged += (s, e) =>
            {
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
                if (webView.CoreWebView2 == null) return;
                var urlFromTextBox = urlBox.Text?.Trim();
                var input = !string.IsNullOrEmpty(urlFromTextBox) ? urlFromTextBox : (node.ExtractUrl?.Trim());

                if (string.IsNullOrEmpty(input))
                {
                    try { webView.CoreWebView2.Navigate("about:blank"); } catch { }
                    return;
                }

                // ── Phân biệt URL vs từ khóa tìm kiếm ──────────────────────────
                // 1. Đã có protocol → navigate thẳng
                if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    try { webView.CoreWebView2.Navigate(input); node.ExtractUrl = input; }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Navigate error: {ex.Message}"); }
                    return;
                }

                // 2. Trông như domain (không có khoảng trắng, có dấu chấm) → thêm https://
                var looksLikeDomain = !input.Contains(' ') && input.Contains('.');
                if (looksLikeDomain)
                {
                    var withProtocol = "https://" + input;
                    try { webView.CoreWebView2.Navigate(withProtocol); node.ExtractUrl = withProtocol; } catch { }
                    return;
                }

                // 3. Từ khóa tìm kiếm → Google Search
                var searchUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
                try { webView.CoreWebView2.Navigate(searchUrl); node.ExtractUrl = searchUrl; } catch { }
            }

            // ── F5 quick reload button (col 2) ─────────────────────────────
            var f5Btn = new Button
            {
                Content = "⟳",
                Width = 28,
                Height = 28,
                FontSize = 14,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Tải lại trang (F5)",
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            f5Btn.Click += (s, e) =>
            {
                try { webView.CoreWebView2?.Reload(); } catch { }
            };
            Grid.SetRow(f5Btn, 0);
            Grid.SetColumn(f5Btn, 2);
            topBarGrid.Children.Add(f5Btn);

            // ── Go button (col 3) ───────────────────────────────────────────
            var goBtn = new Button
            {
                Content = "→",
                Width = 32,
                Height = 28,
                FontSize = 14,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Điều hướng tới URL",
                Style = Application.Current.TryFindResource("SuccessButton") as Style
            };
            goBtn.Click += (s, e) =>
            {
                urlBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                EnsureWebViewAndNavigate();
            };
            Grid.SetRow(goBtn, 0);
            Grid.SetColumn(goBtn, 3);
            topBarGrid.Children.Add(goBtn);

            // ── Clear-cache Refresh button (col 4) — logic giữ nguyên ─────
            var refreshBtn = new Button
            {
                Content = "🗑",
                Width = 32,
                Height = 28,
                FontSize = 12,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Làm mới (xóa cookies + cache + storage rồi load lại)",
                Style = Application.Current.TryFindResource("WarningButton") as Style
            };
            refreshBtn.Click += async (s, e) =>
            {
                if (webView.CoreWebView2 == null) return;

                // Hiển thị dialog xác nhận
                var result = MessageBox.Show(
                    "Bạn có muốn làm mới trang không? Tất cả dữ liệu (session, auth, cookies, cache) sẽ bị xóa.",
                    "Xác nhận làm mới",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var core = webView.CoreWebView2;
                        var currentUrl = core.Source;
                        
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
                                var cookieManager = core.CookieManager;
                                // Lấy tất cả cookies (không filter để lấy hết)
                                var cookies = await cookieManager.GetCookiesAsync(null);
                                
                                foreach (var cookie in cookies)
                                {
                                    try
                                    {
                                        // Xóa cookie nếu thuộc domain hiện tại hoặc subdomain
                                        var cookieDomain = cookie.Domain?.TrimStart('.') ?? "";
                                        var currentDomain = domain.TrimStart('.');
                                        
                                        if (string.Equals(cookieDomain, currentDomain, StringComparison.OrdinalIgnoreCase) ||
                                            cookieDomain.EndsWith("." + currentDomain, StringComparison.OrdinalIgnoreCase) ||
                                            currentDomain.EndsWith("." + cookieDomain, StringComparison.OrdinalIgnoreCase))
                                        {
                                            cookieManager.DeleteCookie(cookie);
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch (Exception cookieEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Lỗi clear cookies: {cookieEx.Message}");
                            }
                        }

                        // 2. Clear localStorage, sessionStorage, IndexedDB qua JavaScript
                        var clearStorageScript = @"
(function() {
    try {
        // Clear localStorage
        if (window.localStorage) {
            window.localStorage.clear();
        }
        // Clear sessionStorage
        if (window.sessionStorage) {
            window.sessionStorage.clear();
        }
        // Clear IndexedDB (nếu có)
        if (window.indexedDB) {
            indexedDB.databases().then(databases => {
                databases.forEach(db => {
                    if (db.name) {
                        indexedDB.deleteDatabase(db.name);
                    }
                });
            }).catch(() => {});
        }
        // Clear cookies qua document.cookie (cho domain hiện tại)
        if (document.cookie) {
            var cookies = document.cookie.split(';');
            cookies.forEach(function(c) {
                var cookieName = c.split('=')[0].trim();
                if (cookieName) {
                    // Xóa cookie với tất cả các path và domain có thể
                    document.cookie = cookieName + '=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;';
                    document.cookie = cookieName + '=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;domain=' + window.location.hostname + ';';
                    document.cookie = cookieName + '=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;domain=.' + window.location.hostname + ';';
                }
            });
        }
    } catch (e) {
        console.error('Error clearing storage:', e);
    }
})();";
                        try
                        {
                            await core.ExecuteScriptAsync(clearStorageScript);
                        }
                        catch (Exception jsEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Lỗi clear storage qua JS: {jsEx.Message}");
                        }

                        // 4. Navigate lại URL hiện tại (như mở lần đầu)
                        if (!string.IsNullOrEmpty(currentUrl))
                        {
                            core.Navigate(currentUrl);
                        }
                        else
                        {
                            // Nếu không có URL, reload
                            core.Reload();
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
            Grid.SetRow(refreshBtn, 0);
            Grid.SetColumn(refreshBtn, 4);
            topBarGrid.Children.Add(refreshBtn);

            var viewportExpandBtn = new Button
            {
                Content = "\uE740",
                FontFamily = ViewportExpandIconFont,
                ToolTip = "Phóng to vừa khung nhìn",
                Width = 28,
                Height = 28,
                FontSize = 14,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            viewportExpandBtn.Click += (_, _) => ToggleNodeViewportExpand(node, border, host, viewportExpandBtn);
            Grid.SetRow(viewportExpandBtn, 0);
            Grid.SetColumn(viewportExpandBtn, 5);
            topBarGrid.Children.Add(viewportExpandBtn);

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

            // Hàm inject JavaScript vào WebView2 (được định nghĩa trước để dùng trong NavigationCompleted)
            async void EnableElementInspector()
            {
                if (webView.CoreWebView2 == null) return;
                try
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(BuildElementInspectorScript());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Enable inspector error: {ex.Message}");
                }
            }

            // Hàm remove JavaScript khỏi WebView2
            async void DisableElementInspector()
            {
                if (webView.CoreWebView2 == null) return;
                try
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(@"
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

            var webViewForInit = webView;
            webViewForInit.Loaded += async (s, e) =>
            {
                try
                {
                    if (isDisposed || webViewForInit.CoreWebView2 != null || !border.IsLoaded)
                        return;

                    if (ShouldUseViewportLazyInit())
                    {
                        var waitCycles = 0;
                        while (!isDisposed &&
                               border.IsLoaded &&
                               border.Visibility != Visibility.Visible &&
                               waitCycles < 300)
                        {
                            await Task.Delay(50);
                            waitCycles++;
                        }
                    }

                    if (isDisposed || webViewForInit.CoreWebView2 != null || !border.IsLoaded || border.Visibility != Visibility.Visible)
                        return;

                    // Stagger init để tránh nhiều node WebView2 giành UI thread cùng lúc khi vừa load workflow.
                    var staggerDelayMs = GetInitStaggerDelayMs();
                    if (staggerDelayMs > 0)
                        await Task.Delay(staggerDelayMs);

                    if (isDisposed || webViewForInit.CoreWebView2 != null || !border.IsLoaded)
                        return;

                    CoreWebView2Environment? env = null;
                    try
                    {
                        // Ưu tiên dùng CoreWebView2Environment dùng chung (pre-init)
                        env = await WebView2EnvironmentManager.GetSharedEnvironmentAsync();
                    }
                    catch (Exception envEx)
                    {
                        // Nếu shared env lỗi (ví dụ warm-up fail), fallback về CreateAsync như cũ
                        System.Diagnostics.Debug.WriteLine($"Shared WebView2 env error, fallback per-node: {envEx.Message}");

                        var cachePathFallback = WebNodeCacheHelper.GetSharedRuntimeCachePath();
                        var optionsFallback = new CoreWebView2EnvironmentOptions();

                        if (GpuDetectionHelper.IsGpuAvailable)
                        {
                            var gpuArgs = new StringBuilder();
                            gpuArgs.Append("--enable-gpu-rasterization ");
                            gpuArgs.Append("--enable-zero-copy ");
                            gpuArgs.Append("--enable-features=VaapiVideoDecoder ");
                            gpuArgs.Append("--ignore-gpu-blacklist ");
                            gpuArgs.Append("--enable-accelerated-2d-canvas ");
                            gpuArgs.Append("--enable-accelerated-video-decode ");

                            optionsFallback.AdditionalBrowserArguments = gpuArgs.ToString();
                        }
                        else
                        {
                            optionsFallback.AdditionalBrowserArguments = "--disable-gpu";
                        }

                        env = await CoreWebView2Environment.CreateAsync(null, cachePathFallback, optionsFallback);
                    }

                    await EnsureCoreWebView2ThrottledAsync(webViewForInit, env);

                    // ⚠️ CRITICAL: Phải set filter và subscribe events TRƯỚC KHI navigate
                    // để đảm bảo bắt được TẤT CẢ requests bao gồm cả XHR từ JavaScript
                    var core = webViewForInit.CoreWebView2;
                    if (core == null)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: CoreWebView2 is null after EnsureCoreWebView2Async");
                        return;
                    }

                    // Cấu hình thêm cho CoreWebView2 để tối ưu GPU
                    if (GpuDetectionHelper.IsGpuAvailable)
                    {
                        try
                        {
                            // Enable hardware acceleration trong settings
                            var settings = core.Settings;
                            // WebView2 mặc định đã enable hardware acceleration
                            // Có thể thêm các setting khác nếu cần
                        }
                        catch { }
                    }

                    // ⚠️ CRITICAL: Đảm bảo WebResourceRequested được raise cho TẤT CẢ requests (mọi context, mọi URL)
                    // CoreWebView2WebResourceContext.All bao gồm cả XHR (XmlHttpRequest) requests từ JavaScript
                    // Phải set filter TRƯỚC KHI subscribe events và TRƯỚC KHI navigate
                    try
                    {
                        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                        System.Diagnostics.Debug.WriteLine("WebView2: Added filter for ALL resource contexts (including XHR) - BEFORE navigation");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"AddWebResourceRequestedFilter error: {ex.Message}");
                    }

                    // Enable DevTools Protocol Network events to capture "full" request headers/body (closer to browser DevTools).
                    // This is necessary because args.Request.Headers in WebResourceRequested is often incomplete (missing sec-*, accept*, priority...).
                    try
                    {
                        _ = core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");

                        CoreWebView2DevToolsProtocolEventReceiver? recvWillBeSent = null;
                        CoreWebView2DevToolsProtocolEventReceiver? recvExtraInfo = null;

                        try { recvWillBeSent = core.GetDevToolsProtocolEventReceiver("Network.requestWillBeSent"); } catch { }
                        try { recvExtraInfo = core.GetDevToolsProtocolEventReceiver("Network.requestWillBeSentExtraInfo"); } catch { }

                        if (recvWillBeSent != null)
                        {
                            recvWillBeSent.DevToolsProtocolEventReceived += (_, e) =>
                            {
                                try
                                {
                                    var json = e.ParameterObjectAsJson;
                                    if (string.IsNullOrWhiteSpace(json)) return;
                                    using var doc = JsonDocument.Parse(json);
                                    var root = doc.RootElement;
                                    if (!root.TryGetProperty("request", out var reqEl)) return;

                                    var url = reqEl.TryGetProperty("url", out var u) ? (u.GetString() ?? "") : "";
                                    var method = reqEl.TryGetProperty("method", out var m) ? (m.GetString() ?? "GET") : "GET";
                                    if (string.IsNullOrWhiteSpace(url)) return;

                                    var key = $"{url}|{method}";
                                    var info = _cdpByUrlMethod.GetOrAdd(key, _ => new CdpRequestInfo());
                                    info.UpdatedAt = DateTimeOffset.UtcNow;

                                    if (reqEl.TryGetProperty("postData", out var pd) && pd.ValueKind == JsonValueKind.String)
                                    {
                                        var postData = pd.GetString();
                                        if (!string.IsNullOrWhiteSpace(postData))
                                            info.PostData = postData;
                                    }

                                    if (reqEl.TryGetProperty("headers", out var headersEl) && headersEl.ValueKind == JsonValueKind.Object)
                                    {
                                        var hdr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                        foreach (var p in headersEl.EnumerateObject())
                                        {
                                            var hn = p.Name ?? "";
                                            if (hn.Length == 0) continue;
                                            var hv = p.Value.ValueKind == JsonValueKind.String ? (p.Value.GetString() ?? "") : p.Value.ToString();
                                            hdr[hn] = hv ?? "";
                                        }
                                        if (hdr.Count > 0) info.Headers = hdr;
                                    }
                                }
                                catch { }
                            };
                        }

                        if (recvExtraInfo != null)
                        {
                            recvExtraInfo.DevToolsProtocolEventReceived += (_, e) =>
                            {
                                try
                                {
                                    var json = e.ParameterObjectAsJson;
                                    if (string.IsNullOrWhiteSpace(json)) return;
                                    using var doc = JsonDocument.Parse(json);
                                    var root = doc.RootElement;

                                    // ExtraInfo has no URL, but it does include headers; we merge into latest matching URL|method when possible.
                                    if (!root.TryGetProperty("headers", out var headersEl) || headersEl.ValueKind != JsonValueKind.Object)
                                        return;

                                    var hdr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                    foreach (var p in headersEl.EnumerateObject())
                                    {
                                        var hn = p.Name ?? "";
                                        if (hn.Length == 0) continue;
                                        var hv = p.Value.ValueKind == JsonValueKind.String ? (p.Value.GetString() ?? "") : p.Value.ToString();
                                        hdr[hn] = hv ?? "";
                                    }
                                    if (hdr.Count == 0) return;

                                    // Heuristic: merge into most recently updated entry (good enough for "latest request matching pattern" use-case).
                                    CdpRequestInfo? mostRecent = null;
                                    string? mostRecentKey = null;
                                    foreach (var kv in _cdpByUrlMethod)
                                    {
                                        if (mostRecent == null || kv.Value.UpdatedAt > mostRecent.UpdatedAt)
                                        {
                                            mostRecent = kv.Value;
                                            mostRecentKey = kv.Key;
                                        }
                                    }

                                    if (mostRecent != null && mostRecentKey != null)
                                    {
                                        mostRecent.Headers = hdr;
                                        mostRecent.UpdatedAt = DateTimeOffset.UtcNow;
                                    }
                                }
                                catch { }
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"DevTools Network enable error: {ex.Message}");
                    }

                    await WebCookiePortableBridge.TryConsumeAndApplyAsync(core.CookieManager);
                    EnsureWebViewAndNavigate();

                    // If there's queued JS from workflow execution, run it as soon as CoreWebView2 is ready.
                    if (!string.IsNullOrWhiteSpace(pendingJsQueue))
                    {
                        var js = pendingJsQueue;
                        pendingJsQueue = null;
                        await TryExecutePendingJsAsync(js);
                    }

                    // Set zoom mặc định 10% (0.9) sau khi WebView2 đã sẵn sàng
                    // Trong WPF WebView2, dùng ExecuteScript để set zoom qua CSS
                    void SetWebViewZoom(double zoomFactor)
                    {
                        // Dùng helper chung để áp dụng zoom và update model/UI
                        ApplyWebViewZoom(zoomFactor);
                    }

                    webViewForInit.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // ✅ Set zoom dựa trên canvas zoom hiện tại (để giữ tỉ lệ)
                        UpdateWebViewZoomForCanvasZoom();
                    }), DispatcherPriority.Loaded);

                    // Đồng bộ thanh URL theo trang hiện tại (giống Chrome: click link YouTube, chuyển trang → URL cập nhật)
                    void SyncUrlBarFromWebView()
                    {
                        var coreView = webViewForInit.CoreWebView2;
                        if (coreView == null) return;
                        var uri = coreView.Source;
                        if (string.IsNullOrEmpty(uri)) return;
                        if (suppressUrlSyncForSleepNav && string.Equals(uri, "about:blank", StringComparison.OrdinalIgnoreCase))
                            return;
                        if (webViewForInit.Dispatcher.CheckAccess())
                            node.ExtractUrl = uri;
                        else
                            webViewForInit.Dispatcher.Invoke(() => node.ExtractUrl = uri);

                        // Cập nhật LastHost để dùng cho map zoom theo domain
                        try
                        {
                            if (Uri.TryCreate(uri, UriKind.Absolute, out var u) && !string.IsNullOrEmpty(u.Host))
                            {
                                node.LastHost = u.Host.ToLowerInvariant();

                                // Nếu đã có zoom cho domain này, áp dụng cho node hiện tại
                                if (_domainZoomByHost.TryGetValue(node.LastHost, out var domainZoom) &&
                                    domainZoom > 0)
                                {
                                    node.CssZoom = domainZoom;
                                }
                            }
                        }
                        catch { }
                    }

                    if (core != null)
                    {
                        // Reset blocking state khi navigation mới
                        node.ResponseOutputValues.Clear();

                        // Hiển thị progress bar khi bắt đầu navigation
                        core.NavigationStarting += (_, _) =>
                        {
                            if (webViewForInit.Dispatcher.CheckAccess())
                                progressBar.Visibility = Visibility.Visible;
                            else
                                webViewForInit.Dispatcher.Invoke(() => progressBar.Visibility = Visibility.Visible);
                        };

                        core.SourceChanged += (_, _) => SyncUrlBarFromWebView();
                        core.NavigationCompleted += (_, navArgs) =>
                        {
                            // Ẩn progress bar khi navigation hoàn thành
                            if (webViewForInit.Dispatcher.CheckAccess())
                                progressBar.Visibility = Visibility.Collapsed;
                            else
                                webViewForInit.Dispatcher.Invoke(() => progressBar.Visibility = Visibility.Collapsed);

                            if (navArgs.IsSuccess)
                            {
                                SyncUrlBarFromWebView();

                                // ✅ Set lại zoom level sau khi navigation hoàn thành dựa trên canvas zoom hiện tại
                                webViewForInit.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    try
                                    {
                                        // Update zoom để giữ tỉ lệ với canvas zoom
                                        UpdateWebViewZoomForCanvasZoom();
                                    }
                                    catch { }
                                }), DispatcherPriority.Loaded);

                                // ✅ Re-inject inspector script nếu đang bật
                                if (node.EnableElementInspector || node.EnableCssSelectorInspector)
                                {
                                    webViewForInit.Dispatcher.BeginInvoke(new Action(async () =>
                                    {
                                        try
                                        {
                                            await System.Threading.Tasks.Task.Delay(500); // Đợi page load xong
                                            if (webViewForInit.CoreWebView2 != null && (node.EnableElementInspector || node.EnableCssSelectorInspector))
                                            {
                                                EnableElementInspector();
                                            }
                                        }
                                        catch { }
                                    }), DispatcherPriority.Background);
                                }
                            }
                        };

                        // Helper function để extract URL từ cURL command
                        string ExtractUrlFromCurl(string curlCommand)
                        {
                            if (string.IsNullOrWhiteSpace(curlCommand)) return string.Empty;

                            // Prefer robust detection: find first absolute http(s) URL anywhere in the command.
                            // DevTools/Postman often generate: curl --location -X POST "https://..." ...
                            try
                            {
                                var m = System.Text.RegularExpressions.Regex.Match(
                                    curlCommand,
                                    @"https?://[^\s'\""]+",
                                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                if (m.Success)
                                    return m.Value.Trim();
                            }
                            catch { }

                            // Nếu đã là URL (bắt đầu bằng http/https), trả về trực tiếp
                            if (curlCommand.TrimStart().StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                curlCommand.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            {
                                return curlCommand.Trim();
                            }

                            // Parse cURL command: tìm URL sau 'curl' hoặc trong quotes
                            // Format: curl 'https://example.com' hoặc curl "https://example.com"
                            var trimmed = curlCommand.Trim();
                            if (trimmed.StartsWith("curl", StringComparison.OrdinalIgnoreCase))
                            {
                                var afterCurl = trimmed.Substring(4).TrimStart();
                                // Tìm URL trong quotes hoặc không có quotes
                                if (afterCurl.StartsWith("'"))
                                {
                                    var endQuote = afterCurl.IndexOf('\'', 1);
                                    if (endQuote > 0)
                                        return afterCurl.Substring(1, endQuote - 1);
                                }
                                else if (afterCurl.StartsWith("\""))
                                {
                                    var endQuote = afterCurl.IndexOf('"', 1);
                                    if (endQuote > 0)
                                        return afterCurl.Substring(1, endQuote - 1);
                                }
                                else
                                {
                                    // Không có quotes, lấy phần đầu (đến space hoặc -)
                                    var spaceIndex = afterCurl.IndexOfAny(new[] { ' ', '\t', '-' });
                                    if (spaceIndex > 0)
                                        return afterCurl.Substring(0, spaceIndex);
                                    return afterCurl;
                                }
                            }

                            return string.Empty;
                        }

                        // Xử lý request intercept: chặn request, thay request, lấy response outputs
                        // ⚠️ CRITICAL: Event này sẽ được trigger cho TẤT CẢ requests bao gồm cả XHR từ JavaScript
                        core.WebResourceRequested += (sender, args) =>
                        {
                            try
                            {
                                var requestUrl = args.Request?.Uri ?? "";
                                if (string.IsNullOrEmpty(requestUrl)) return;

                                var requestMethod = args.Request?.Method ?? "";
                                var originalRequestUrl = requestUrl;

                                // 1. FIRST: Cache request payload nếu có output cần Payload cho URL này
                                // ⚠️ CRITICAL: Phải cache TRƯỚC KHI blocking/intercept để blocked requests cũng có payload
                                if (node.ResponseOutputs != null && node.ResponseOutputs.Count > 0 && args.Request != null)
                                {
                                    try
                                    {
                                        // Helper inline: check if method matches
                                        bool CheckMethodMatch(string? expectedMethod, string actualMethod)
                                        {
                                            var exp = expectedMethod?.Trim();
                                            if (string.IsNullOrWhiteSpace(exp) || string.Equals(exp, "All", StringComparison.OrdinalIgnoreCase))
                                                return true;
                                            return string.Equals(actualMethod, exp, StringComparison.OrdinalIgnoreCase);
                                        }

                                        // Kiểm tra xem có output nào cần Payload cho URL này không
                                        bool needsPayload = false;
                                        // System.Diagnostics.Debug.WriteLine($"[Payload Check] Checking {node.ResponseOutputs.Count} outputs for {requestMethod} {requestUrl}");
                                        
                                        foreach (var ro in node.ResponseOutputs)
                                        {
                                            if (ro == null) continue;
                                            var methodMatch = CheckMethodMatch(ro.RequestMethod, requestMethod);
                                            var urlMatch = UrlMatchesPattern(requestUrl, ro.Url ?? ""); // Use proper UrlMatchesPattern
                                            var et = (ro.ExtractType ?? "Response").Trim();
                                            
                                            // System.Diagnostics.Debug.WriteLine($"  Output: URL pattern='{ro.Url}', Method={ro.RequestMethod}, ExtractType={et}, UrlMatch={urlMatch}, MethodMatch={methodMatch}");
                                            
                                            // Cache payload if output needs Payload OR CurlCmd (cURL needs body too)
                                            if (methodMatch && urlMatch && 
                                                (string.Equals(et, "Payload", StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(et, "CurlCmd", StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(et, "CurlBash", StringComparison.OrdinalIgnoreCase)))
                                            {
                                                needsPayload = true;
                                                System.Diagnostics.Debug.WriteLine($"  → Needs payload for {et}! Will try to cache.");
                                                break;
                                            }
                                        }

                                        if (needsPayload)
                                        {
                                            if (args.Request.Content == null)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[Payload Cache] Request has NO content/body for {requestMethod} {requestUrl}");
                                            }
                                            else
                                            {
                                                // Đọc request body và cache lại
                                                try
                                                {
                                                    var content = args.Request.Content;
                                                    using var reader = new StreamReader(content, Encoding.UTF8, false, 8192, leaveOpen: false);
                                                    var payload = reader.ReadToEnd();
                                                    
                                                    System.Diagnostics.Debug.WriteLine($"[Payload Cache] Cached payload for {requestMethod} {requestUrl}: {payload.Length} bytes");
                                                    if (payload.Length > 0 && payload.Length < 500)
                                                        System.Diagnostics.Debug.WriteLine($"[Payload Cache] Content: {payload}");
                                                    
                                                    // Cache với key = requestUrl|requestMethod
                                                    var cacheKey = $"{requestUrl}|{requestMethod}";
                                                    _requestPayloadCache[cacheKey] = payload;

                                                    // Recreate stream để request vẫn gửi được payload
                                                    if (!string.IsNullOrEmpty(payload))
                                                    {
                                                        var bytes = Encoding.UTF8.GetBytes(payload);
                                                        args.Request.Content = new MemoryStream(bytes);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"Cache payload error: {ex.Message}");
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Payload caching outer error: {ex.Message}");
                                    }
                                }

                                // 2. Xử lý chặn request: so sánh khớp URL pattern trong BlockingRules
                                // ⚠️ CRITICAL: BlockingRules luôn ưu tiên. Dù output có cấu hình URL đó, vẫn chặn.
                                if (node.BlockingRules != null && node.BlockingRules.Count > 0)
                                {
                                    // Nếu đã chặn ít nhất một request trước đó và cấu hình "chặn luôn các request phía sau" được bật
                                    // thì chặn tất cả các request tiếp theo trong lần chạy node này.
                                    if (node.BlockAllRequestsAfterFirstMatch && node.HasTriggeredBlockingChain)
                                    {
                                        if (core.Environment != null)
                                        {
                                            args.Response = core.Environment.CreateWebResourceResponse(null, 403, "Blocked", "");
                                        }
                                        var currentUrl = args.Request?.Uri ?? requestUrl;
                                        ExtractFromBlockedRequest(node, args.Request, currentUrl, requestMethod);
                                        return;
                                    }

                                    foreach (var rule in node.BlockingRules)
                                    {
                                        // 2.1. Nếu URL khớp với URL cha -> chặn như bình thường
                                        if (!string.IsNullOrWhiteSpace(rule.UrlPattern) && UrlMatchesPattern(requestUrl, rule.UrlPattern))
                                        {
                                            // Check method: "All" hoặc exact match với requestMethod
                                            var ruleMethod = rule.Method?.Trim() ?? "All";
                                            var methodMatches = string.Equals(ruleMethod, "All", StringComparison.OrdinalIgnoreCase) ||
                                                                string.Equals(ruleMethod, requestMethod, StringComparison.OrdinalIgnoreCase);
                                            
                                            if (methodMatches)
                                            {
                                                if (core.Environment != null)
                                                {
                                                    args.Response = core.Environment.CreateWebResourceResponse(null, 403, "Blocked", "");
                                                }
                                                // Nếu có output khớp URL này: vẫn lấy RequestHeaders, Params, Payload từ cache
                                                var currentUrl = args.Request?.Uri ?? requestUrl;
                                                ExtractFromBlockedRequest(node, args.Request, currentUrl, requestMethod);

                                                // Đánh dấu rule cha đã từng bị chặn trong lần chạy này
                                                rule.HasTriggeredParentInCurrentRun = true;

                                                // Đánh dấu đã bắt đầu chuỗi chặn nếu user bật chế độ chặn các request phía sau (toàn cục)
                                                if (node.BlockAllRequestsAfterFirstMatch)
                                                {
                                                    node.HasTriggeredBlockingChain = true;
                                                }

                                                return;
                                            }
                                        }

                                        // 2.2. Nếu URL KHÔNG khớp URL cha, nhưng rule này đã từng chặn URL cha
                                        //      và URL khớp một trong các URL con (có method riêng) -> chặn.
                                        if (rule.HasTriggeredParentInCurrentRun && rule.ChildRules != null && rule.ChildRules.Count > 0)
                                        {
                                            foreach (var child in rule.ChildRules)
                                            {
                                                if (string.IsNullOrWhiteSpace(child.UrlPattern))
                                                    continue;

                                                if (!UrlMatchesPattern(requestUrl, child.UrlPattern))
                                                    continue;

                                                var childMethod = child.Method?.Trim() ?? "All";
                                                var childMethodMatches =
                                                    string.Equals(childMethod, "All", StringComparison.OrdinalIgnoreCase) ||
                                                    string.Equals(childMethod, requestMethod, StringComparison.OrdinalIgnoreCase);

                                                if (!childMethodMatches)
                                                    continue;

                                                if (core.Environment != null)
                                                {
                                                    args.Response = core.Environment.CreateWebResourceResponse(null, 403, "Blocked (child)", "");
                                                }
                                                var currentUrl = args.Request?.Uri ?? requestUrl;
                                                ExtractFromBlockedRequest(node, args.Request, currentUrl, requestMethod);
                                                return;
                                            }
                                        }
                                    }
                                }

                                // 3. Xử lý thay request (intercept rules)
                                foreach (var rule in node.RequestInterceptRules)
                                {
                                    if (UrlMatchesPattern(requestUrl, rule.MatchUrlPattern))
                                    {
                                        // Thay URL nếu cần
                                        if (rule.ReplaceUrlWithNodeKey && !string.IsNullOrWhiteSpace(rule.ReplaceUrlSourceNodeId) && !string.IsNullOrWhiteSpace(rule.ReplaceUrlSourceOutputKey))
                                        {
                                            // Resolve value từ node+key (cURL) - real-time từ workflow
                                            try
                                            {
                                                if (host?.ViewModel != null && host.ViewModel.Nodes != null && host.ViewModel.Connections != null)
                                                {
                                                    var sourceNode = host.ViewModel.Nodes.FirstOrDefault(n => string.Equals(n.Id, rule.ReplaceUrlSourceNodeId, StringComparison.OrdinalIgnoreCase));
                                                    if (sourceNode != null)
                                                    {
                                                        // Resolve value từ source node
                                                        var resolvedValue = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, rule.ReplaceUrlSourceOutputKey);
                                                        if (!string.IsNullOrWhiteSpace(resolvedValue) && resolvedValue != "—" && args.Request != null)
                                                        {
                                                            // Parse cURL command để lấy URL
                                                            // Format cURL: curl 'https://example.com/api' -H 'header: value' ...
                                                            // Hoặc có thể là URL trực tiếp
                                                            var urlFromCurl = ExtractUrlFromCurl(resolvedValue);
                                                            if (!string.IsNullOrWhiteSpace(urlFromCurl))
                                                            {
                                                                args.Request.Uri = urlFromCurl;
                                                            }
                                                            else
                                                            {
                                                                // Nếu không phải cURL, dùng giá trị trực tiếp
                                                                args.Request.Uri = resolvedValue;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Lỗi thay request error: {ex.Message}");
                                            }
                                        }
                                        else if (!string.IsNullOrWhiteSpace(rule.ReplaceUrlValue) && args.Request != null)
                                        {
                                            args.Request.Uri = rule.ReplaceUrlValue;
                                        }

                                        // Thay params/body nếu cần (tương tự)
                                        // TODO: Implement replace params/body
                                    }
                                }

                                // If URL was replaced, make sure subsequent matching/caching uses the final URL.
                                // This prevents "CurlCmd" / "Payload" lookups using a stale pre-rewrite URL.
                                if (args.Request != null)
                                {
                                    var finalUrl = args.Request.Uri ?? requestUrl;
                                    if (!string.Equals(finalUrl, requestUrl, StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Mirror cached payload entry (if any) to new URL key.
                                        try
                                        {
                                            var oldKey = $"{requestUrl}|{requestMethod}";
                                            var newKey = $"{finalUrl}|{requestMethod}";
                                            if (_requestPayloadCache.TryGetValue(oldKey, out var payload) && !_requestPayloadCache.ContainsKey(newKey))
                                            {
                                                _requestPayloadCache[newKey] = payload;
                                            }
                                        }
                                        catch { }

                                        requestUrl = finalUrl;
                                    }
                                }


                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"WebResourceRequested error: {ex.Message}");
                            }
                        };

                        // Lấy response body cho outputs và xử lý real-time
                        // ⚠️ CRITICAL: WebResourceResponseReceived sẽ nhận TẤT CẢ responses bao gồm cả XHR
                        // (ResourceContext chỉ có trong WebResourceRequested, không có trong WebResourceResponseReceived)
                        core.WebResourceResponseReceived += async (sender, args) =>
                        {
                            try
                            {
                                var requestUrl = args.Request?.Uri ?? "";
                                if (string.IsNullOrEmpty(requestUrl)) return;

                                var requestMethod = args.Request?.Method ?? string.Empty;
                                bool shouldUpdateUI = false;



                                // 2. Lấy cookie từ CookieManager của WebView2 (real-time, đầy đủ, chuẩn)
                                var response = args.Response;
                                if (response != null)
                                {
                                    // Lấy cookies từ CookieManager – chính xác, đầy đủ thông tin
                                    try
                                    {
                                        var cookieManager = core?.CookieManager;
                                        if (cookieManager != null && Uri.TryCreate(requestUrl, UriKind.Absolute, out var reqUri))
                                        {
                                            var cookies = await cookieManager.GetCookiesAsync(requestUrl);
                                            if (cookies != null && cookies.Count > 0)
                                            {
                                                // Build JSON chuẩn: { url, cookies: [{name, value, domain, path, secure, httpOnly, expirationTime}] }
                                                var cookieList = new System.Text.Json.Nodes.JsonArray();
                                                foreach (var c in cookies)
                                                {
                                                    var obj = new System.Text.Json.Nodes.JsonObject
                                                    {
                                                        ["name"] = c.Name,
                                                        ["value"] = c.Value,
                                                        ["domain"] = c.Domain,
                                                        ["path"] = c.Path,
                                                        ["secure"] = c.IsSecure,
                                                        ["httpOnly"] = c.IsHttpOnly,
                                                        ["session"] = c.IsSession
                                                    };
                                                    if (!c.IsSession)
                                                        obj["expirationTime"] = ((DateTimeOffset)c.Expires).ToUnixTimeSeconds();
                                                    cookieList.Add(obj);
                                                }
                                                var cookieRoot = new System.Text.Json.Nodes.JsonObject
                                                {
                                                    ["url"] = $"{reqUri.Scheme}://{reqUri.Host}",
                                                    ["cookies"] = cookieList
                                                };
                                                var cookieJson = cookieRoot.ToJsonString();
                                                node.LastCookie = cookieJson;

                                                // Cập nhật output "cookie" (DynamicOutput)
                                                var cookieDynOut = node.DynamicOutputs?.FirstOrDefault(o =>
                                                    string.Equals(o.Key, "cookie", StringComparison.OrdinalIgnoreCase));
                                                if (cookieDynOut != null)
                                                    cookieDynOut.UserValueOverride = cookieJson;

                                                shouldUpdateUI = true;
                                                // System.Diagnostics.Debug.WriteLine($"[Cookie] Captured {cookies.Count} cookies for {reqUri.Host} → JSON {cookieJson.Length} chars");
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Lấy cookie từ CookieManager error: {ex.Message}");
                                    }

                                    // 3. Lấy response body cho outputs đã cấu hình (chỉ lấy từ key đã cấu hình)
                                    // ⚠️ CRITICAL: Xử lý TẤT CẢ responses bao gồm cả XHR (resourceContext có thể là XmlHttpRequest)
                                    if (node.ResponseOutputs != null && node.ResponseOutputs.Count > 0)
                                    {
                                        try
                                        {
                                            // Kiểm tra xem có output nào khớp với URL này không
                                            bool hasMatchingOutput = false;
                                            foreach (var responseOutput in node.ResponseOutputs)
                                            {
                                                var methodMatches = MethodMatches(responseOutput?.RequestMethod, requestMethod);
                                                var outputUrlPattern = responseOutput?.Url ?? string.Empty;
                                                if (UrlMatchesPattern(requestUrl, outputUrlPattern) && methodMatches)
                                                {
                                                    hasMatchingOutput = true;
                                                    break;
                                                }
                                            }

                                            // Xử lý từng output khớp theo ExtractType
                                            if (hasMatchingOutput)
                                            {
                                                foreach (var responseOutput in node.ResponseOutputs)
                                                {
                                                    if (!MethodMatches(responseOutput?.RequestMethod, requestMethod)) continue;
                                                    var outputUrlPattern = responseOutput?.Url ?? string.Empty;
                                                    if (!UrlMatchesPattern(requestUrl, outputUrlPattern)) continue;

                                                    var key = responseOutput?.Key?.Trim() ?? string.Empty;
                                                    if (string.IsNullOrWhiteSpace(key)) continue;

                                                    var extractType = (responseOutput?.ExtractType ?? "Response").Trim();
                                                    if (string.IsNullOrEmpty(extractType)) extractType = "Response";

                                                    string? extractedValue = null;

                                                     // Cookie: lay toan bo cookies tu CookieManager -> JSON chuan
                                                     // Format: {"url":"https://example.com","cookies":[{name,value,domain,path,secure,httpOnly,session,expirationTime}]}
                                                     if (string.Equals(extractType, "Cookie", StringComparison.OrdinalIgnoreCase))
                                                     {
                                                         try
                                                         {
                                                             var cookieMgr = core?.CookieManager;
                                                             if (cookieMgr != null && Uri.TryCreate(requestUrl, UriKind.Absolute, out var cookieUri))
                                                             {
                                                                 var allCookies = await cookieMgr.GetCookiesAsync(requestUrl);
                                                                 if (allCookies != null && allCookies.Count > 0)
                                                                 {
                                                                     var cl = new System.Text.Json.Nodes.JsonArray();
                                                                     foreach (var ck in allCookies)
                                                                     {
                                                                         var co = new System.Text.Json.Nodes.JsonObject
                                                                         {
                                                                             ["name"]     = ck.Name,
                                                                             ["value"]    = ck.Value,
                                                                             ["domain"]   = ck.Domain,
                                                                             ["path"]     = ck.Path,
                                                                             ["secure"]   = ck.IsSecure,
                                                                             ["httpOnly"] = ck.IsHttpOnly,
                                                                             ["session"]  = ck.IsSession
                                                                         };
                                                                         if (!ck.IsSession)
                                                                             co["expirationTime"] = ((DateTimeOffset)ck.Expires).ToUnixTimeSeconds();
                                                                         cl.Add(co);
                                                                     }
                                                                     var rootObj = new System.Text.Json.Nodes.JsonObject
                                                                     {
                                                                         ["url"]     = $"{cookieUri.Scheme}://{cookieUri.Host}",
                                                                         ["cookies"] = cl
                                                                     };
                                                                     extractedValue = rootObj.ToJsonString();
                                                                 }
                                                                 else
                                                                 {
                                                                     extractedValue = $"{{\"url\":\"{cookieUri.Scheme}://{cookieUri.Host}\",\"cookies\":[]}}"; 
                                                                 }
                                                             }
                                                         }
                                                         catch (Exception ex)
                                                         {
                                                             System.Diagnostics.Debug.WriteLine($"Cookie ExtractType error: {ex.Message}");
                                                             extractedValue = string.Empty;
                                                         }
                                                     }
                                                     
                                                    // Headers: response headers → JSON object { "key": "value" }
                                                    else if (string.Equals(extractType, "Headers", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        try
                                                        {
                                                            var dict = new Dictionary<string, string>();
                                                            if (response?.Headers != null)
                                                            {
                                                                foreach (var h in response.Headers)
                                                                    dict[h.Key] = h.Value;
                                                            }
                                                            extractedValue = JsonSerializer.Serialize(dict);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            System.Diagnostics.Debug.WriteLine($"Headers extraction error: {ex.Message}");
                                                        }
                                                    }
                                                    // Params: query string từ URL
                                                    else if (string.Equals(extractType, "Params", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        try
                                                        {
                                                            var idx = requestUrl.IndexOf('?');
                                                            extractedValue = idx >= 0 ? requestUrl.Substring(idx + 1) : string.Empty;
                                                        }
                                                        catch { }
                                                    }
                                                    // RequestHeaders: request headers → JSON object { "key": "value" }
                                                    else if (string.Equals(extractType, "RequestHeaders", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        try
                                                        {
                                                            var req = args.Request;
                                                            if (req != null)
                                                            {
                                                                var dict = new Dictionary<string, string>();
                                                                foreach (var h in req.Headers)
                                                                    dict[h.Key] = h.Value;
                                                                extractedValue = JsonSerializer.Serialize(dict);
                                                            }
                                                        }
                                                        catch { }
                                                    }
                                                    // CurlCmd: generate complete cURL command for Windows CMD (like browser F12)
                                                    else if (string.Equals(extractType, "CurlCmd", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        try
                                                        {
                                                            static string EscapeForCmdDoubleQuoted(string s)
                                                            {
                                                                if (string.IsNullOrEmpty(s)) return string.Empty;
                                                                // Escape CMD metacharacters inside a double-quoted string.
                                                                // DevTools style typically uses ^" ... ^" and escapes inner quotes as ^\^"
                                                                return s
                                                                    .Replace("^", "^^")
                                                                    .Replace("%", "%%")
                                                                    .Replace("&", "^&")
                                                                    .Replace("|", "^|")
                                                                    .Replace("<", "^<")
                                                                    .Replace(">", "^>")
                                                                    .Replace("\"", "^\\^\"");
                                                            }

                                                            static string WrapCmdQuoted(string s) => $"^\"{EscapeForCmdDoubleQuoted(s)}^\"";

                                                            static Dictionary<string, string> GetHeadersSafe(Microsoft.Web.WebView2.Core.CoreWebView2HttpRequestHeaders? headers)
                                                            {
                                                                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                                                if (headers == null) return dict;
                                                                try
                                                                {
                                                                    foreach (var h in headers)
                                                                    {
                                                                        var k = (h.Key ?? "").Trim();
                                                                        if (k.Length == 0) continue;
                                                                        // Keep last value for duplicate keys (DevTools typically shows one line per key anyway).
                                                                        dict[k] = h.Value ?? "";
                                                                    }
                                                                }
                                                                catch { }

                                                                // Some "auto" headers are not always present in enumeration; try GetHeader for common ones.
                                                                try
                                                                {
                                                                    var mustTry = new[]
                                                                    {
                                                                        "accept", "accept-language", "user-agent", "referer", "origin", "cookie"
                                                                    };
                                                                    foreach (var k in mustTry)
                                                                    {
                                                                        try
                                                                        {
                                                                            var v = headers.GetHeader(k);
                                                                            if (!string.IsNullOrWhiteSpace(v) && !dict.ContainsKey(k))
                                                                                dict[k] = v;
                                                                        }
                                                                        catch { }
                                                                    }
                                                                }
                                                                catch { }

                                                                return dict;
                                                            }

                                                            static string BuildCurlCmdForWindowsCmd(
                                                                string url,
                                                                string method,
                                                                Dictionary<string, string> headers,
                                                                string? payload)
                                                            {
                                                                var lines = new List<string>();

                                                                lines.Add($"curl {WrapCmdQuoted(url)} ^");

                                                                if (!string.IsNullOrWhiteSpace(method) &&
                                                                    !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                                                                {
                                                                    lines.Add($"  -X {method.ToUpperInvariant()} ^");
                                                                }

                                                                // Cookie: DevTools uses -b instead of -H "cookie: ..."
                                                                if (headers.TryGetValue("cookie", out var cookieVal) && !string.IsNullOrWhiteSpace(cookieVal))
                                                                {
                                                                    lines.Add($"  -b {WrapCmdQuoted(cookieVal)} ^");
                                                                }

                                                                foreach (var kv in headers)
                                                                {
                                                                    var headerName = kv.Key ?? "";
                                                                    if (headerName.Length == 0) continue;
                                                                    if (string.Equals(headerName, "cookie", StringComparison.OrdinalIgnoreCase)) continue;

                                                                    var headerValue = kv.Value ?? "";
                                                                    lines.Add($"  -H {WrapCmdQuoted($"{headerName}: {headerValue}")} ^");
                                                                }

                                                                if (!string.IsNullOrWhiteSpace(payload))
                                                                {
                                                                    lines.Add($"  --data-raw {WrapCmdQuoted(payload)} ^");
                                                                }

                                                                lines.Add("  --compressed");

                                                                if (!string.IsNullOrWhiteSpace(url) &&
                                                                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                                                {
                                                                    lines.Add("  --insecure");
                                                                }

                                                                // Remove trailing caret from last line if present (we keep it only for line-continuation).
                                                                for (var i = 0; i < lines.Count; i++)
                                                                {
                                                                    if (i == lines.Count - 1)
                                                                    {
                                                                        lines[i] = lines[i].TrimEnd();
                                                                        if (lines[i].EndsWith(" ^", StringComparison.Ordinal))
                                                                            lines[i] = lines[i].Substring(0, lines[i].Length - 2).TrimEnd();
                                                                    }
                                                                }

                                                                return string.Join(Environment.NewLine, lines);
                                                            }

                                                            var req = args.Request;
                                                            var hdrDict = GetHeadersSafe(req?.Headers);
                                                            if (TryGetCdpRequestInfo(requestUrl, requestMethod, out var cdpHdr, out var cdpPostData))
                                                            {
                                                                if (cdpHdr.Count > hdrDict.Count)
                                                                    hdrDict = cdpHdr;
                                                            }

                                                            // Ensure cookies are present (Cloudflare antibot often requires cf_clearance + __cf_bm).
                                                            // CDP/Request headers can omit Cookie; CookieManager is the most reliable source.
                                                            try
                                                            {
                                                                if (!hdrDict.ContainsKey("cookie"))
                                                                {
                                                                    var cookieMgr = core?.CookieManager;
                                                                    if (cookieMgr != null && Uri.TryCreate(requestUrl, UriKind.Absolute, out _))
                                                                    {
                                                                        var cookies = await cookieMgr.GetCookiesAsync(requestUrl);
                                                                        if (cookies != null && cookies.Count > 0)
                                                                        {
                                                                            var cookiePairs = new List<string>(cookies.Count);
                                                                            foreach (var ck in cookies)
                                                                            {
                                                                                if (string.IsNullOrWhiteSpace(ck?.Name)) continue;
                                                                                cookiePairs.Add($"{ck.Name}={ck.Value}");
                                                                            }
                                                                            if (cookiePairs.Count > 0)
                                                                                hdrDict["cookie"] = string.Join("; ", cookiePairs);
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            catch { }

                                                            // Add request body if available (from cache)
                                                            var cacheKey = $"{requestUrl}|{requestMethod}";
                                                            System.Diagnostics.Debug.WriteLine($"[CurlCmd] Looking for cached payload with key: {cacheKey}");
                                                            System.Diagnostics.Debug.WriteLine($"[CurlCmd] Cache currently has {_requestPayloadCache.Count} entries");
                                                            
                                                            string? cachedPayloadOrNull = null;
                                                            if (_requestPayloadCache.TryGetValue(cacheKey, out var cachedPayload) &&
                                                                !string.IsNullOrWhiteSpace(cachedPayload))
                                                            {
                                                                System.Diagnostics.Debug.WriteLine($"[CurlCmd] Found cached payload: {cachedPayload.Length} bytes");
                                                                cachedPayloadOrNull = cachedPayload;
                                                            }
                                                            else
                                                            {
                                                                System.Diagnostics.Debug.WriteLine($"[CurlCmd] No cached payload found for key: {cacheKey}");
                                                            }
                                                            if (string.IsNullOrWhiteSpace(cachedPayloadOrNull) &&
                                                                TryGetCdpRequestInfo(requestUrl, requestMethod, out _, out var cdpPostData2) &&
                                                                !string.IsNullOrWhiteSpace(cdpPostData2))
                                                            {
                                                                cachedPayloadOrNull = cdpPostData2;
                                                            }

                                                            extractedValue = BuildCurlCmdForWindowsCmd(
                                                                requestUrl,
                                                                requestMethod,
                                                                hdrDict,
                                                                cachedPayloadOrNull);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            System.Diagnostics.Debug.WriteLine($"CurlCmd generation error: {ex.Message}");
                                                            extractedValue = $"curl \"{requestUrl}\" # Error: {ex.Message}";
                                                        }
                                                    }
                                                    // CurlBash: generate cURL for bash/Postman import (no CMD carets)
                                                    else if (string.Equals(extractType, "CurlBash", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        try
                                                        {
                                                            static string EscapeBashSingleQuoted(string s)
                                                            {
                                                                if (string.IsNullOrEmpty(s)) return string.Empty;
                                                                // Close/open single quotes around an escaped single quote: 'foo'\''bar'
                                                                return s.Replace("'", "'\\''");
                                                            }

                                                            static Dictionary<string, string> GetHeadersSafe(Microsoft.Web.WebView2.Core.CoreWebView2HttpRequestHeaders? headers)
                                                            {
                                                                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                                                if (headers == null) return dict;
                                                                try
                                                                {
                                                                    foreach (var h in headers)
                                                                    {
                                                                        var k = (h.Key ?? "").Trim();
                                                                        if (k.Length == 0) continue;
                                                                        dict[k] = h.Value ?? "";
                                                                    }
                                                                }
                                                                catch { }

                                                                try
                                                                {
                                                                    var mustTry = new[]
                                                                    {
                                                                        "accept", "accept-language", "user-agent", "referer", "origin", "cookie"
                                                                    };
                                                                    foreach (var k in mustTry)
                                                                    {
                                                                        try
                                                                        {
                                                                            var v = headers.GetHeader(k);
                                                                            if (!string.IsNullOrWhiteSpace(v) && !dict.ContainsKey(k))
                                                                                dict[k] = v;
                                                                        }
                                                                        catch { }
                                                                    }
                                                                }
                                                                catch { }

                                                                return dict;
                                                            }

                                                            static string BuildCurlForBash(string url, string method, Dictionary<string, string> headers, string? payload)
                                                            {
                                                                var lines = new List<string>();
                                                                lines.Add($"curl --location --request {method.ToUpperInvariant()} '{EscapeBashSingleQuoted(url)}' \\");

                                                                if (headers.TryGetValue("cookie", out var cookieVal) && !string.IsNullOrWhiteSpace(cookieVal))
                                                                {
                                                                    lines.Add($"  --cookie '{EscapeBashSingleQuoted(cookieVal)}' \\");
                                                                }

                                                                foreach (var kv in headers)
                                                                {
                                                                    var k = kv.Key ?? "";
                                                                    if (k.Length == 0) continue;
                                                                    if (string.Equals(k, "cookie", StringComparison.OrdinalIgnoreCase)) continue;
                                                                    var v = kv.Value ?? "";
                                                                    lines.Add($"  --header '{EscapeBashSingleQuoted($"{k}: {v}")}' \\");
                                                                }

                                                                if (!string.IsNullOrWhiteSpace(payload))
                                                                {
                                                                    lines.Add($"  --data-raw '{EscapeBashSingleQuoted(payload)}' \\");
                                                                }

                                                                // remove trailing backslash from last line
                                                                if (lines.Count > 0)
                                                                {
                                                                    var last = lines[^1];
                                                                    if (last.EndsWith(" \\", StringComparison.Ordinal))
                                                                        lines[^1] = last.Substring(0, last.Length - 2);
                                                                }

                                                                return string.Join(Environment.NewLine, lines);
                                                            }

                                                            var req = args.Request;
                                                            var hdrDict = GetHeadersSafe(req?.Headers);
                                                            if (TryGetCdpRequestInfo(requestUrl, requestMethod, out var cdpHdr, out var cdpPostData))
                                                            {
                                                                if (cdpHdr.Count > hdrDict.Count)
                                                                    hdrDict = cdpHdr;
                                                            }

                                                            // Ensure cookies from CookieManager if missing.
                                                            try
                                                            {
                                                                if (!hdrDict.ContainsKey("cookie"))
                                                                {
                                                                    var cookieMgr = core?.CookieManager;
                                                                    if (cookieMgr != null && Uri.TryCreate(requestUrl, UriKind.Absolute, out _))
                                                                    {
                                                                        var cookies = await cookieMgr.GetCookiesAsync(requestUrl);
                                                                        if (cookies != null && cookies.Count > 0)
                                                                        {
                                                                            var cookiePairs = new List<string>(cookies.Count);
                                                                            foreach (var ck in cookies)
                                                                            {
                                                                                if (string.IsNullOrWhiteSpace(ck?.Name)) continue;
                                                                                cookiePairs.Add($"{ck.Name}={ck.Value}");
                                                                            }
                                                                            if (cookiePairs.Count > 0)
                                                                                hdrDict["cookie"] = string.Join("; ", cookiePairs);
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            catch { }

                                                            var cacheKey = $"{requestUrl}|{requestMethod}";
                                                            string? cachedPayloadOrNull = null;
                                                            if (_requestPayloadCache.TryGetValue(cacheKey, out var cachedPayload) &&
                                                                !string.IsNullOrWhiteSpace(cachedPayload))
                                                            {
                                                                cachedPayloadOrNull = cachedPayload;
                                                            }
                                                            if (string.IsNullOrWhiteSpace(cachedPayloadOrNull) &&
                                                                TryGetCdpRequestInfo(requestUrl, requestMethod, out _, out var cdpPostData2) &&
                                                                !string.IsNullOrWhiteSpace(cdpPostData2))
                                                            {
                                                                cachedPayloadOrNull = cdpPostData2;
                                                            }

                                                            extractedValue = BuildCurlForBash(requestUrl, string.IsNullOrWhiteSpace(requestMethod) ? "GET" : requestMethod, hdrDict, cachedPayloadOrNull);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            System.Diagnostics.Debug.WriteLine($"CurlBash generation error: {ex.Message}");
                                                            extractedValue = $"curl '{requestUrl}' # Error: {ex.Message}";
                                                        }
                                                    }
                                                    // Payload (request body): lấy từ cache đã lưu trong WebResourceRequested
                                                    else if (string.Equals(extractType, "Payload", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        // Lấy từ cache - thử current method trước
                                                        var cacheKey = $"{requestUrl}|{requestMethod}";
                                                        if (_requestPayloadCache.TryGetValue(cacheKey, out var cachedPayload))
                                                        {
                                                            extractedValue = cachedPayload;
                                                            System.Diagnostics.Debug.WriteLine($"[Payload Extract] Retrieved cached payload for {requestMethod} {requestUrl}: {cachedPayload.Length} bytes");
                                                            // DON'T remove from cache - CurlCmd output may need it too!
                                                        }
                                                        else
                                                        {
                                                            // Fallback: nếu không tìm thấy (ví dụ OPTIONS không có body), thử tìm POST/PUT/PATCH cùng URL
                                                            //System.Diagnostics.Debug.WriteLine($"[Payload Extract] No cached payload for {requestMethod} {requestUrl}, trying fallback methods...");
                                                            
                                                            string? fallbackPayload = null;
                                                            string? fallbackMethod = null;
                                                            
                                                            // Thử các method thường có payload
                                                            var methodsToTry = new[] { "POST", "PUT", "PATCH" };
                                                            foreach (var method in methodsToTry)
                                                            {
                                                                if (string.Equals(method, requestMethod, StringComparison.OrdinalIgnoreCase))
                                                                    continue; // Đã thử rồi
                                                                    
                                                                var fallbackKey = $"{requestUrl}|{method}";
                                                                if (_requestPayloadCache.TryGetValue(fallbackKey, out fallbackPayload))
                                                                {
                                                                    fallbackMethod = method;
                                                                    // DON'T remove - other outputs may need it
                                                                    break;
                                                                }
                                                            }
                                                            
                                                            if (fallbackPayload != null)
                                                            {
                                                                extractedValue = fallbackPayload;
                                                                //System.Diagnostics.Debug.WriteLine($"[Payload Extract] ✓ Found fallback payload from {fallbackMethod} {requestUrl}: {fallbackPayload.Length} bytes");
                                                            }
                                                            else
                                                            {
                                                                // Cuối cùng: thử tìm bất kỳ method nào cùng URL
                                                                var anyMatchingKey = _requestPayloadCache.Keys.FirstOrDefault(k => k.StartsWith(requestUrl + "|", StringComparison.OrdinalIgnoreCase));
                                                                if (anyMatchingKey != null && _requestPayloadCache.TryGetValue(anyMatchingKey, out fallbackPayload))
                                                                {
                                                                    extractedValue = fallbackPayload;
                                                                    //System.Diagnostics.Debug.WriteLine($"[Payload Extract] ✓ Found payload from any method for {requestUrl}: {fallbackPayload.Length} bytes");
                                                                    _requestPayloadCache.Remove(anyMatchingKey);
                                                                }
                                                                else
                                                                {
                                                                    extractedValue = string.Empty;
                                                                    //System.Diagnostics.Debug.WriteLine($"[Payload Extract] ✗ No payload found even with fallback for {requestUrl}");
                                                                }
                                                            }
                                                        }
                                                    }
                                                    // Response (body): mặc định
                                                    else
                                                    {
                                                        // Sẽ xử lý bên dưới trong block GetContentAsync
                                                        extractType = "Response";
                                                    }

                                                    if (extractedValue != null)
                                                    {
                                                        node.ResponseOutputValues[key] = extractedValue;
                                                        if (node.DynamicOutputs != null)
                                                        {
                                                            var dyn = node.DynamicOutputs.FirstOrDefault(o =>
                                                                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                                                            if (dyn != null) dyn.UserValueOverride = extractedValue;
                                                        }
                                                        shouldUpdateUI = true;

                                                        // Khi output được cập nhật (ví dụ CurlCmd), tự động trigger các node phụ thuộc
                                                        TryTriggerDependentNodes(host, node, key);
                                                    }
                                                }

                                                // Response (body): cần GetContentAsync
                                                var needsBody = node.ResponseOutputs.Any(ro =>
                                                {
                                                    if (ro == null) return false;
                                                    var m = MethodMatches(ro.RequestMethod, requestMethod);
                                                    var u = UrlMatchesPattern(requestUrl, ro.Url ?? "");
                                                    var et = (ro.ExtractType ?? "Response").Trim();
                                                    if (string.IsNullOrEmpty(et)) et = "Response";
                                                    return m && u && string.Equals(et, "Response", StringComparison.OrdinalIgnoreCase);
                                                });

                                                if (needsBody)
                                                {
                                                    string? contentType = null;
                                                    try { if (response.Headers.Contains("Content-Type")) contentType = response.Headers.GetHeader("Content-Type"); } catch { }
                                                    bool shouldGetContent = string.IsNullOrEmpty(contentType) ||
                                                        contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                                                        contentType.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                                                        contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
                                                        contentType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
                                                        contentType.Contains("protobuffer", StringComparison.OrdinalIgnoreCase) ||
                                                        contentType.Contains("application", StringComparison.OrdinalIgnoreCase);

                                                    if (shouldGetContent)
                                                    {
                                                        System.IO.Stream? content = null;
                                                        try { content = await response.GetContentAsync(); }
                                                        catch (System.Runtime.InteropServices.COMException comEx)
                                                        {
                                                            if (comEx.HResult != unchecked((int)0x800700E8) && comEx.HResult != unchecked((int)0xFFFF8300))
                                                                System.Diagnostics.Debug.WriteLine($"COMException khi lấy content: {comEx.HResult:X8} - {comEx.Message}");
                                                            return;
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            System.Diagnostics.Debug.WriteLine($"Lỗi khi lấy content: {ex.GetType().Name} - {ex.Message}");
                                                            return;
                                                        }

                                                        if (content != null)
                                                        {
                                                            try
                                                            {
                                                                using var stream = content;
                                                                using var reader = new System.IO.StreamReader(stream);
                                                                var body = await reader.ReadToEndAsync();
                                                                if (!string.IsNullOrWhiteSpace(body))
                                                                {
                                                                    foreach (var responseOutput in node.ResponseOutputs)
                                                                    {
                                                                        if (!MethodMatches(responseOutput?.RequestMethod, requestMethod)) continue;
                                                                        var outputUrlPattern = responseOutput?.Url ?? string.Empty;
                                                                        var et = (responseOutput?.ExtractType ?? "Response").Trim();
                                                                        if (string.IsNullOrEmpty(et)) et = "Response";
                                                                        if (!UrlMatchesPattern(requestUrl, outputUrlPattern) ||
                                                                            !string.Equals(et, "Response", StringComparison.OrdinalIgnoreCase)) continue;

                                                                        var key = responseOutput?.Key?.Trim() ?? string.Empty;
                                                                        if (string.IsNullOrWhiteSpace(key)) continue;

                                                                        node.ResponseOutputValues[key] = body;
                                                                        if (node.DynamicOutputs != null)
                                                                        {
                                                                            var dyn = node.DynamicOutputs.FirstOrDefault(o =>
                                                                                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                                                                            if (dyn != null) dyn.UserValueOverride = body;
                                                                        }
                                                                        shouldUpdateUI = true;
                                                                    }
                                                                }
                                                            }
                                                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi đọc stream: {ex.Message}"); }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (System.Runtime.InteropServices.COMException comEx)
                                        {
                                            // Pipe closed hoặc WebView2 đã bị dispose - bỏ qua, không log
                                            if (comEx.HResult != unchecked((int)0x800700E8) && comEx.HResult != unchecked((int)0xFFFF8300))
                                            {
                                                System.Diagnostics.Debug.WriteLine($"COMException: {comEx.HResult:X8} - {comEx.Message}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            // Chỉ log các lỗi không phải COMException
                                            if (!(ex is System.Runtime.InteropServices.COMException))
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Lấy content error: {ex.GetType().Name} - {ex.Message}");
                                            }
                                        }
                                    }
                                }

                                // Trigger sync data panels để cập nhật output UI real-time
                                if (shouldUpdateUI)
                                {
                                    if (host != null)
                                    {
                                        // 1) (hiện tại RequestSyncDataPanels là no-op, nhưng giữ lại cho tương lai)
                                        try
                                        {
                                            if (webViewForInit.Dispatcher.CheckAccess())
                                            {
                                                host.RequestSyncDataPanels(immediate: false);
                                            }
                                            else
                                            {
                                                webViewForInit.Dispatcher.Invoke(() =>
                                                {
                                                    host.RequestSyncDataPanels(immediate: false);
                                                });
                                            }
                                        }
                                        catch { }

                                        // 2) Nếu user bật SyncLiveOutputsToResults thì cập nhật luôn Execution Results cho node web này
                                        if (node.SyncLiveOutputsToResults)
                                        {
                                            try
                                            {
                                                var vm = host.ViewModel;
                                                if (vm != null)
                                                {
                                                    var field = typeof(FlowMy.ViewModels.WorkflowEditorViewModel)
                                                        .GetField("_executionVisualizer",
                                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                                    if (field?.GetValue(vm) is FlowMy.Services.Workflow.IWorkflowExecutionVisualizer visualizer)
                                                    {
                                                        visualizer.RefreshSavedOutputs(new[] { node });
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"SyncLiveOutputsToResults error: {ex.Message}");
                                            }
                                        }
                                    }

                                    // 3) Nếu WebNodeExecutor đang chờ outputs (PendingOutputsTcs) thì khi đã có đủ
                                    //    ResponseOutputs cho URL này, signal TCS để cho phép workflow tiếp tục.
                                    try
                                    {
                                        var tcs = node.PendingOutputsTcs;
                                        if (tcs != null && !tcs.Task.IsCompleted && node.ResponseOutputs != null && node.ResponseOutputs.Count > 0)
                                        {
                                            // Nếu có ít nhất một output được đánh dấu WaitForCompletion → chỉ đợi các key đó.
                                            // Ngược lại (không có flag) → giữ behavior cũ: đợi tất cả outputs có key.
                                            // WaitMode:
                                            // - All: đợi tất cả keys cần đợi
                                            // - Any: chỉ cần 1 key cần đợi xuất hiện là chạy tiếp
                                            var outputs = node.ResponseOutputs.Where(ro => ro != null).ToList();
                                            var explicitWaitKeys = outputs
                                                .Where(ro => ro.WaitForCompletion && !string.IsNullOrWhiteSpace(ro.Key))
                                                .Select(ro => ro.Key!.Trim())
                                                .ToList();

                                            var waitKeys = explicitWaitKeys.Count > 0
                                                ? explicitWaitKeys
                                                : outputs.Select(ro => ro.Key?.Trim())
                                                    .Where(k => !string.IsNullOrWhiteSpace(k))
                                                    .Select(k => k!)
                                                    .ToList();

                                            bool ready;
                                            if (waitKeys.Count == 0)
                                            {
                                                // Không có key nào để đợi -> coi như xong ngay
                                                ready = true;
                                            }
                                            else if (node.ResponseOutputsWaitMode == FlowMy.Models.Nodes.WebOutputsWaitMode.Any)
                                            {
                                                ready = waitKeys.Any(k => node.ResponseOutputValues.ContainsKey(k));
                                            }
                                            else
                                            {
                                                ready = waitKeys.All(k => node.ResponseOutputValues.ContainsKey(k));
                                            }

                                            if (ready)
                                            {
                                                System.Diagnostics.Debug.WriteLine("[WebNodeControl] ✓ Required ResponseOutputs populated, completing PendingOutputsTcs.");
                                                tcs.TrySetResult(true);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"PendingOutputsTcs signal error: {ex.Message}");
                                    }
                                }

                                // Clean up payload cache after all outputs have been processed for this request
                                var cleanupKey = $"{requestUrl}|{requestMethod}";
                                if (_requestPayloadCache.ContainsKey(cleanupKey))
                                {
                                    _requestPayloadCache.Remove(cleanupKey);
                                    System.Diagnostics.Debug.WriteLine($"[Response] Cleaned up payload cache for: {cleanupKey}");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"WebResourceResponseReceived error: {ex.Message}");
                            }
                        };
                    }

                    // Helper functions (phải nằm trong Loaded handler để có closure host, node)
                    bool MethodMatches(string? expectedMethod, string requestMethod)
                    {
                        var exp = expectedMethod?.Trim();
                        if (string.IsNullOrWhiteSpace(exp) || string.Equals(exp, "All", StringComparison.OrdinalIgnoreCase))
                            return true;
                        return string.Equals(requestMethod, exp, StringComparison.OrdinalIgnoreCase);
                    }

                    void ExtractFromBlockedRequest(WebNode n, CoreWebView2WebResourceRequest? req, string reqUrl, string reqMethod)
                    {
                        if (n.ResponseOutputs == null || n.ResponseOutputs.Count == 0) return;
                        foreach (var ro in n.ResponseOutputs)
                        {
                            if (!UrlMatchesPattern(reqUrl, ro?.Url ?? "")) continue;
                            if (!MethodMatches(ro?.RequestMethod, reqMethod)) continue;
                            var key = ro?.Key?.Trim() ?? "";
                            if (string.IsNullOrWhiteSpace(key)) continue;
                            var et = (ro?.ExtractType ?? "Response").Trim();
                            if (string.IsNullOrEmpty(et)) et = "Response";
                            string? val = null;
                            if (string.Equals(et, "Response", StringComparison.OrdinalIgnoreCase) || string.Equals(et, "Headers", StringComparison.OrdinalIgnoreCase))
                            {
                                val = string.Empty;
                            }
                            else if (string.Equals(et, "RequestHeaders", StringComparison.OrdinalIgnoreCase) && req != null)
                            {
                                try
                                {
                                    var dict = new Dictionary<string, string>();
                                    foreach (var h in req.Headers)
                                        dict[h.Key] = h.Value;
                                    val = JsonSerializer.Serialize(dict);
                                }
                                catch { val = "{}"; }
                            }
                            else if (string.Equals(et, "Params", StringComparison.OrdinalIgnoreCase))
                            {
                                var idx = reqUrl.IndexOf('?');
                                val = idx >= 0 ? reqUrl.Substring(idx + 1) : string.Empty;
                            }
                            else if (string.Equals(et, "Payload", StringComparison.OrdinalIgnoreCase) && req != null)
                            {
                                try
                                {
                                    // Thử lấy từ cache trước (stream đã được đọc trong WebResourceRequested)
                                    var cacheKey = $"{reqUrl}|{reqMethod}";
                                    if (_requestPayloadCache.TryGetValue(cacheKey, out var cachedPayload))
                                    {
                                        val = cachedPayload;
                                        System.Diagnostics.Debug.WriteLine($"[Blocked Request Payload] Using cached payload: {cachedPayload.Length} bytes");
                                        // DON'T remove from cache - other outputs (like CurlCmd) may need it too!
                                    }
                                    else
                                    {
                                        // Fallback: thử đọc trực tiếp từ stream (nếu chưa được cache)
                                        var content = req.Content;
                                        if (content != null)
                                        {
                                            using var reader = new StreamReader(content);
                                            val = reader.ReadToEnd();
                                            //System.Diagnostics.Debug.WriteLine($"[Blocked Request] Read payload from stream: {val.Length} bytes");
                                        }
                                        else
                                        {
                                            val = string.Empty;
                                            //System.Diagnostics.Debug.WriteLine($"[Blocked Request] No payload content");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Payload extraction error: {ex.Message}");
                                    val = string.Empty;
                                }
                            }
                            else if (string.Equals(et, "CurlCmd", StringComparison.OrdinalIgnoreCase) && req != null)
                            {
                                // Generate complete cURL command for blocked request (like browser F12)
                                try
                                {
                                    static string EscapeForCmdDoubleQuoted(string s)
                                    {
                                        if (string.IsNullOrEmpty(s)) return string.Empty;
                                        return s
                                            .Replace("^", "^^")
                                            .Replace("%", "%%")
                                            .Replace("&", "^&")
                                            .Replace("|", "^|")
                                            .Replace("<", "^<")
                                            .Replace(">", "^>")
                                            .Replace("\"", "^\\^\"");
                                    }

                                    static string WrapCmdQuoted(string s) => $"^\"{EscapeForCmdDoubleQuoted(s)}^\"";

                                    static Dictionary<string, string> GetHeadersSafe(Microsoft.Web.WebView2.Core.CoreWebView2HttpRequestHeaders? headers)
                                    {
                                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                        if (headers == null) return dict;
                                        try
                                        {
                                            foreach (var h in headers)
                                            {
                                                var k = (h.Key ?? "").Trim();
                                                if (k.Length == 0) continue;
                                                dict[k] = h.Value ?? "";
                                            }
                                        }
                                        catch { }

                                        try
                                        {
                                            var mustTry = new[]
                                            {
                                                "accept", "accept-language", "user-agent", "referer", "origin", "cookie"
                                            };
                                            foreach (var k in mustTry)
                                            {
                                                try
                                                {
                                                    var v = headers.GetHeader(k);
                                                    if (!string.IsNullOrWhiteSpace(v) && !dict.ContainsKey(k))
                                                        dict[k] = v;
                                                }
                                                catch { }
                                            }
                                        }
                                        catch { }

                                        return dict;
                                    }

                                    static string BuildCurlCmdForWindowsCmd(
                                        string url,
                                        string method,
                                        Dictionary<string, string> headers,
                                        string? payload)
                                    {
                                        var lines = new List<string>();

                                        lines.Add($"curl {WrapCmdQuoted(url)} ^");

                                        if (!string.IsNullOrWhiteSpace(method) &&
                                            !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                                        {
                                            lines.Add($"  -X {method.ToUpperInvariant()} ^");
                                        }

                                        if (headers.TryGetValue("cookie", out var cookieVal) && !string.IsNullOrWhiteSpace(cookieVal))
                                        {
                                            lines.Add($"  -b {WrapCmdQuoted(cookieVal)} ^");
                                        }

                                        foreach (var kv in headers)
                                        {
                                            var headerName = kv.Key ?? "";
                                            if (headerName.Length == 0) continue;
                                            if (string.Equals(headerName, "cookie", StringComparison.OrdinalIgnoreCase)) continue;

                                            var headerValue = kv.Value ?? "";
                                            lines.Add($"  -H {WrapCmdQuoted($"{headerName}: {headerValue}")} ^");
                                        }

                                        if (!string.IsNullOrWhiteSpace(payload))
                                        {
                                            lines.Add($"  --data-raw {WrapCmdQuoted(payload)} ^");
                                        }

                                        lines.Add("  --compressed");

                                        if (!string.IsNullOrWhiteSpace(url) &&
                                            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            lines.Add("  --insecure");
                                        }

                                        for (var i = 0; i < lines.Count; i++)
                                        {
                                            if (i == lines.Count - 1)
                                            {
                                                lines[i] = lines[i].TrimEnd();
                                                if (lines[i].EndsWith(" ^", StringComparison.Ordinal))
                                                    lines[i] = lines[i].Substring(0, lines[i].Length - 2).TrimEnd();
                                            }
                                        }

                                        return string.Join(Environment.NewLine, lines);
                                    }

                                    var hdrDict = GetHeadersSafe(req.Headers);
                                    if (TryGetCdpRequestInfo(reqUrl, reqMethod, out var cdpHdr, out var cdpPostData))
                                    {
                                        if (cdpHdr.Count > hdrDict.Count)
                                            hdrDict = cdpHdr;
                                    }

                                    // Add request body if available (from cache)
                                    var cacheKey = $"{reqUrl}|{reqMethod}";
                                    System.Diagnostics.Debug.WriteLine($"[CurlCmd Blocked] Looking for cached payload with key: {cacheKey}");
                                    
                                    string? cachedPayloadOrNull = null;
                                    if (_requestPayloadCache.TryGetValue(cacheKey, out var cachedPayload) &&
                                        !string.IsNullOrWhiteSpace(cachedPayload))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[CurlCmd Blocked] Found cached payload: {cachedPayload.Length} bytes");
                                        cachedPayloadOrNull = cachedPayload;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[CurlCmd Blocked] No cached payload found");
                                    }
                                    if (string.IsNullOrWhiteSpace(cachedPayloadOrNull) &&
                                        TryGetCdpRequestInfo(reqUrl, reqMethod, out _, out var cdpPostData2) &&
                                        !string.IsNullOrWhiteSpace(cdpPostData2))
                                    {
                                        cachedPayloadOrNull = cdpPostData2;
                                    }

                                    val = BuildCurlCmdForWindowsCmd(
                                        reqUrl,
                                        reqMethod,
                                        hdrDict,
                                        cachedPayloadOrNull);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"CurlCmd generation error for blocked request: {ex.Message}");
                                    val = $"curl \"{reqUrl}\" # Error: {ex.Message}";
                                }
                            }
                            else if (string.Equals(et, "CurlBash", StringComparison.OrdinalIgnoreCase) && req != null)
                            {
                                try
                                {
                                    static string EscapeBashSingleQuoted(string s)
                                    {
                                        if (string.IsNullOrEmpty(s)) return string.Empty;
                                        return s.Replace("'", "'\\''");
                                    }

                                    static Dictionary<string, string> GetHeadersSafe(Microsoft.Web.WebView2.Core.CoreWebView2HttpRequestHeaders? headers)
                                    {
                                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                        if (headers == null) return dict;
                                        try
                                        {
                                            foreach (var h in headers)
                                            {
                                                var k = (h.Key ?? "").Trim();
                                                if (k.Length == 0) continue;
                                                dict[k] = h.Value ?? "";
                                            }
                                        }
                                        catch { }

                                        try
                                        {
                                            var mustTry = new[]
                                            {
                                                "accept", "accept-language", "user-agent", "referer", "origin", "cookie"
                                            };
                                            foreach (var k in mustTry)
                                            {
                                                try
                                                {
                                                    var v = headers.GetHeader(k);
                                                    if (!string.IsNullOrWhiteSpace(v) && !dict.ContainsKey(k))
                                                        dict[k] = v;
                                                }
                                                catch { }
                                            }
                                        }
                                        catch { }

                                        return dict;
                                    }

                                    static string BuildCurlForBash(string url, string method, Dictionary<string, string> headers, string? payload)
                                    {
                                        var lines = new List<string>();
                                        lines.Add($"curl --location --request {method.ToUpperInvariant()} '{EscapeBashSingleQuoted(url)}' \\");

                                        if (headers.TryGetValue("cookie", out var cookieVal) && !string.IsNullOrWhiteSpace(cookieVal))
                                        {
                                            lines.Add($"  --cookie '{EscapeBashSingleQuoted(cookieVal)}' \\");
                                        }

                                        foreach (var kv in headers)
                                        {
                                            var k = kv.Key ?? "";
                                            if (k.Length == 0) continue;
                                            if (string.Equals(k, "cookie", StringComparison.OrdinalIgnoreCase)) continue;
                                            var v = kv.Value ?? "";
                                            lines.Add($"  --header '{EscapeBashSingleQuoted($"{k}: {v}")}' \\");
                                        }

                                        if (!string.IsNullOrWhiteSpace(payload))
                                        {
                                            lines.Add($"  --data-raw '{EscapeBashSingleQuoted(payload)}' \\");
                                        }

                                        if (lines.Count > 0)
                                        {
                                            var last = lines[^1];
                                            if (last.EndsWith(" \\", StringComparison.Ordinal))
                                                lines[^1] = last.Substring(0, last.Length - 2);
                                        }

                                        return string.Join(Environment.NewLine, lines);
                                    }

                                    var hdrDict = GetHeadersSafe(req.Headers);
                                    if (TryGetCdpRequestInfo(reqUrl, reqMethod, out var cdpHdr, out var cdpPostData))
                                    {
                                        if (cdpHdr.Count > hdrDict.Count)
                                            hdrDict = cdpHdr;
                                    }
                                    var cacheKey = $"{reqUrl}|{reqMethod}";
                                    string? cachedPayloadOrNull = null;
                                    if (_requestPayloadCache.TryGetValue(cacheKey, out var cachedPayload) &&
                                        !string.IsNullOrWhiteSpace(cachedPayload))
                                    {
                                        cachedPayloadOrNull = cachedPayload;
                                    }
                                    if (string.IsNullOrWhiteSpace(cachedPayloadOrNull) &&
                                        TryGetCdpRequestInfo(reqUrl, reqMethod, out _, out var cdpPostData2) &&
                                        !string.IsNullOrWhiteSpace(cdpPostData2))
                                    {
                                        cachedPayloadOrNull = cdpPostData2;
                                    }

                                    val = BuildCurlForBash(reqUrl, string.IsNullOrWhiteSpace(reqMethod) ? "GET" : reqMethod, hdrDict, cachedPayloadOrNull);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"CurlBash generation error for blocked request: {ex.Message}");
                                    val = $"curl '{reqUrl}' # Error: {ex.Message}";
                                }
                            }
                            else
                                val = string.Empty;
                            if (val != null)
                            {
                                n.ResponseOutputValues[key] = val;
                                if (n.DynamicOutputs != null)
                                {
                                    var dyn = n.DynamicOutputs.FirstOrDefault(o =>
                                        string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                                    if (dyn != null) dyn.UserValueOverride = val;
                                }
                                if (host != null)
                                    webViewForInit.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        host.RequestSyncDataPanels(immediate: false);
                                        if (n.SyncLiveOutputsToResults)
                                        {
                                            try
                                            {
                                                var vm = host.ViewModel;
                                                if (vm != null)
                                                {
                                                    var field = typeof(FlowMy.ViewModels.WorkflowEditorViewModel)
                                                        .GetField("_executionVisualizer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                                    if (field?.GetValue(vm) is FlowMy.Services.Workflow.IWorkflowExecutionVisualizer visualizer)
                                                        visualizer.RefreshSavedOutputs(new[] { n });
                                                }
                                            }
                                            catch { }
                                        }

                                        // Nếu executor đang chờ outputs cho node bị chặn thì cũng signal PendingOutputsTcs
                                        // khi tất cả ResponseOutputs đã được populate.
                                        try
                                        {
                                            var tcs = n.PendingOutputsTcs;
                                            if (tcs != null && !tcs.Task.IsCompleted && n.ResponseOutputs != null && n.ResponseOutputs.Count > 0)
                                            {
                                                var outputs = n.ResponseOutputs.Where(ro => ro != null).ToList();
                                                var explicitWaitKeys = outputs
                                                    .Where(ro => ro.WaitForCompletion && !string.IsNullOrWhiteSpace(ro.Key))
                                                    .Select(ro => ro.Key!.Trim())
                                                    .ToList();

                                                var waitKeys = explicitWaitKeys.Count > 0
                                                    ? explicitWaitKeys
                                                    : outputs.Select(ro => ro.Key?.Trim())
                                                        .Where(k => !string.IsNullOrWhiteSpace(k))
                                                        .Select(k => k!)
                                                        .ToList();

                                                bool ready;
                                                if (waitKeys.Count == 0)
                                                {
                                                    ready = true;
                                                }
                                                else if (n.ResponseOutputsWaitMode == FlowMy.Models.Nodes.WebOutputsWaitMode.Any)
                                                {
                                                    ready = waitKeys.Any(k => n.ResponseOutputValues.ContainsKey(k));
                                                }
                                                else
                                                {
                                                    ready = waitKeys.All(k => n.ResponseOutputValues.ContainsKey(k));
                                                }

                                                if (ready)
                                                {
                                                    System.Diagnostics.Debug.WriteLine("[WebNodeControl] ✓ Required ResponseOutputs populated (blocked request), completing PendingOutputsTcs.");
                                                    tcs.TrySetResult(true);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"PendingOutputsTcs signal (blocked) error: {ex.Message}");
                                        }
                                    }));
                            }
                        }
                        
                        // Clean up cache after all outputs have been processed for this request
                        var cleanupKey = $"{reqUrl}|{reqMethod}";
                        if (_requestPayloadCache.ContainsKey(cleanupKey))
                        {
                            _requestPayloadCache.Remove(cleanupKey);
                            System.Diagnostics.Debug.WriteLine($"[Blocked Request] Cleaned up payload cache for: {cleanupKey}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"lỖI WEB NODE error: {ex.Message}");
                }
                finally
                {
                    // Hiển thị WebView2 sau khi khởi tạo xong (hoặc lỗi) để tránh block UI
                    if (webViewForInit.Dispatcher.CheckAccess())
                    {
                        webViewForInit.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        webViewForInit.Dispatcher.Invoke(() =>
                        {
                            webViewForInit.Visibility = Visibility.Visible;
                        });
                    }
                }
            };

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
                Text = "WebView2  •  Chuột phải → cấu hình",
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
                Foreground = GetTitleBrush(node),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            border.Focusable = true;
            border.FocusVisualStyle = null;

            bool isHovering = false;
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = node.Title ?? "Web";
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitlePosition(titleTextBlock, border, host);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(WebNode.TitleDisplayMode))
                    {
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    }
                    else if (e.PropertyName == nameof(WebNode.TitleColorMode) || e.PropertyName == nameof(WebNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(WebNode.Width) || e.PropertyName == nameof(WebNode.Height))
                    {
                        if (s == node && !isResizing)
                        {
                            border.Width = node.Width;
                            border.Height = node.Height;
                        }
                        
                        // Scale UI elements ở topBar và bottomBar theo Height
                        // Dùng công thức tuyệt đối: scale = node.Height / heightBaseline
                        // Chạy cả khi đang resize (PropertyChanged do PreviewMouseMove trigger) để scale liên tục
                        if (e.PropertyName == nameof(WebNode.Height))
                        {
                            var heightBaseline = border.MinHeight > 0 ? border.MinHeight : 200.0;
                            
                            // Scale factor tuyệt đối: tỉ lệ height hiện tại / height baseline
                            // Dùng Math.Max(1.0, ...) để tránh items thu nhỏ khi node.Height < MinHeight
                            // (có thể xảy ra với dữ liệu cũ lưu height < MinHeight)
                            var rawScale = heightBaseline > 0 ? node.Height / heightBaseline : 1.0;
                            var topBottomScaleFactor = Math.Max(1.0, rawScale);
                            
                            // Scale topBarGrid để các item bên trong (urlBox, buttons, etc.) scale theo
                            var topBarScale = new ScaleTransform(topBottomScaleFactor, topBottomScaleFactor);
                            topBarGrid.LayoutTransform = topBarScale;
                            
                            // Scale bottomGrid để các item bên trong (zoomPanel, buttons, etc.) scale theo
                            var bottomBarScale = new ScaleTransform(topBottomScaleFactor, topBottomScaleFactor);
                            bottomGrid.LayoutTransform = bottomBarScale;

                            // Scale resize handles + ports để dễ thao tác khi node lớn.
                            UpdateInteractionVisualScale(handleOverlay, node, topBottomScaleFactor);
                        }
                    }
                    // Không tự động navigate khi ExtractUrl thay đổi
                    // Chỉ navigate khi người dùng nhấn Enter hoặc click Go button
                    // else if (e.PropertyName == nameof(WebNode.ExtractUrl))
                    // {
                    //     EnsureWebViewAndNavigate();
                    // }
                };
            }

            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (isHovering) border.Focus(); }));
            };
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            };

            // Keyboard Port Position: Arrow = Port IN, Shift+Arrow = Port OUT
            border.PreviewKeyDown += (s, e) =>
            {
                if (!isHovering) return;
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PortPosition? newPos = e.Key switch
                {
                    Key.Left  => PortPosition.Left,
                    Key.Up    => PortPosition.Top,
                    Key.Right => PortPosition.Right,
                    Key.Down  => PortPosition.Bottom,
                    _ => null
                };
                if (newPos == null) return;
                e.Handled = true;
                ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
            };

            var visibilityDescriptor = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                    titleTextBlock.Visibility = Visibility.Collapsed;
                else
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            });

            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                // Áp dụng scale ban đầu dựa trên node.Height (dùng trực tiếp để tránh ActualHeight = 0 khi load workflow)
                // Math.Max(1.0, ...) đảm bảo không thu nhỏ khi node.Height < MinHeight từ dữ liệu cũ
                var loadedBaseline = border.MinHeight > 0 ? border.MinHeight : 200.0;
                var loadedRawScale = loadedBaseline > 0 ? node.Height / loadedBaseline : 1.0;
                var loadedScale = Math.Max(1.0, loadedRawScale);
                topBarGrid.LayoutTransform = new ScaleTransform(loadedScale, loadedScale);
                bottomGrid.LayoutTransform = new ScaleTransform(loadedScale, loadedScale);
                UpdateInteractionVisualScale(handleOverlay, node, loadedScale);
            };
            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);
            border.Unloaded += (s, e) =>
            {
                isDisposed = true;
                try
                {
                    // Đóng WebView2 khi node bị xóa: dừng media (nhạc, video) và giải phóng tài nguyên
                    try
                    {
                        if (webView.CoreWebView2 != null)
                            webView.CoreWebView2.Navigate("about:blank");
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
                    if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
                        host.WorkflowCanvas.Children.Remove(titleTextBlock);
                    if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                        node.TitleTextBlockUI = null;
                }
                catch { }
            };

            border.LayoutUpdated += (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    // Ẩn WebView2 khi border không visible
                    if (webView.Visibility != Visibility.Collapsed)
                        webView.Visibility = Visibility.Collapsed;
                    return;
                }

                bool isZooming = NodeChrome.IsZooming;

                // Xử lý zoom: ẩn WebView2 và title để tránh nháy
                if (isZooming)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
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

                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    if (titleTextBlock.Visibility == Visibility.Visible)
                        UpdateTitlePosition(titleTextBlock, border, host);
                }

                // Xử lý panning/dragging: ẩn WebView2 khi đang di chuyển, hiển thị lại khi dừng
                // Khi di chuyển node, giữ nguyên zoom level mà user đã set (không thay đổi)
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

                if (titleTextBlock.Visibility == Visibility.Visible)
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
            };

            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
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

        private static Brush GetTitleBrush(WebNode node)
        {
            if (node.TitleColorMode != TitleColorMode.CustomColor || string.IsNullOrEmpty(node.TitleColorKey) || node.TitleColorKey == "NodeColor")
                return node.NodeBrush;
            if (node.TitleColorKey == "LimeGreen") return new SolidColorBrush(Colors.LimeGreen);
            var brush = Application.Current.TryFindResource(node.TitleColorKey) as Brush;
            return brush ?? node.NodeBrush;
        }

        private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering)
        {
            return mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };
        }

        private static void UpdateTitleVisibility(TextBlock tb, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
        {
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible) { tb.Visibility = Visibility.Collapsed; return; }
            tb.Visibility = GetTitleVisibility(mode, isHovering);
        }

        private static void ThrottledUpdateTitlePosition(TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (!_titleUpdateTimers.TryGetValue(border, out var timer))
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
                timer.Tick += (s, e) => { timer.Stop(); UpdateTitlePosition(tb, border, host); };
                _titleUpdateTimers[border] = timer;
            }
            timer.Stop();
            timer.Start();
        }

        private static void UpdateTitlePosition(TextBlock tb, Border border, IWorkflowEditorHost host)
        {
            if (host.WorkflowCanvas == null || !host.WorkflowCanvas.Children.Contains(tb)) return;
            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);
            if (double.IsNaN(left) && border.Tag is WorkflowNode n) left = n.X;
            if (double.IsNaN(top) && border.Tag is WorkflowNode n2) top = n2.Y;
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            if (tb.ActualWidth == 0 || tb.ActualHeight == 0)
            {
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                tb.Arrange(new Rect(tb.DesiredSize));
            }
            var titleLeft = left + (border.ActualWidth / 2) - (tb.ActualWidth / 2);
            var titleTop = top - tb.ActualHeight - 4;
            Canvas.SetLeft(tb, titleLeft);
            Canvas.SetTop(tb, titleTop);
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

        private static void OpenNodeDialog(WebNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                if (node.Border != null && node.Border.IsMouseCaptured)
                    node.Border.ReleaseMouseCapture();
                host.DraggedNode = null;
                if (host.ViewModel != null)
                    host.ViewModel.SelectedNode = null;
                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();
                var dialog = new WebNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            if (host is WorkflowEditorWindow window)
            {
                var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager) return manager;
            }
            return new NodeDialogManager();
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