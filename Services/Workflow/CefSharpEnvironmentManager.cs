using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;

namespace FlowMy.Services.Workflow
{
    /// <summary>
    /// Manager quản lý CefSharp lifecycle (Initialize / Shutdown) & settings cho toàn bộ ứng dụng.
    /// Thay thế WebView2EnvironmentManager.
    /// </summary>
    public static class CefSharpEnvironmentManager
    {
        private static bool _isInitialized = false;
        private static readonly object _lock = new();
        private static TaskCompletionSource<bool>? _initTcs;

        /// <summary>
        /// Cho biết CefSharp đã được khởi tạo chưa.
        /// </summary>
        public static bool IsInitialized => _isInitialized || Cef.IsInitialized == true;

        /// <summary>
        /// Kiểm tra NodeType có phải là loại dùng CefSharp hay không (Web, HtmlUi).
        /// </summary>
        public static bool IsCefSharpNodeType(NodeType type)
        {
            return type == NodeType.Web || type == NodeType.HtmlUi;
        }

        /// <summary>
        /// Kiểm tra nodeType string (từ Template palette) có phải loại dùng CefSharp hay không.
        /// </summary>
        public static bool IsCefSharpNodeTypeString(string? nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;
            return string.Equals(nodeType, "Web", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(nodeType, "HtmlUi", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kiểm tra danh sách nodes có chứa ít nhất 1 node dùng CefSharp hay không.
        /// </summary>
        public static bool RequiresCefSharp(IEnumerable<WorkflowNode>? nodes)
        {
            return nodes != null && nodes.Any(n => IsCefSharpNodeType(n.Type));
        }

        /// <summary>
        /// Đảm bảo CefSharp đã được khởi tạo (KHÔNG chặn UI).
        /// Phase 1: Background thread — chuẩn bị CefSettings (I/O đĩa, GPU detection).
        /// Phase 2: UI thread @ DispatcherPriority.Background — gọi Cef.Initialize(settings).
        /// Nhiều caller gọi đồng thời sẽ chia sẻ cùng 1 Task.
        /// </summary>
        public static Task EnsureInitializedAsync()
        {
            if (IsInitialized) return Task.CompletedTask;

            lock (_lock)
            {
                if (IsInitialized) return Task.CompletedTask;
                // Nếu đang init rồi, trả về Task hiện tại
                if (_initTcs != null) return _initTcs.Task;

                _initTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                StartInitializationPipeline(_initTcs);
                return _initTcs.Task;
            }
        }

        /// <summary>
        /// Đảm bảo CefSharp đã được khởi tạo (sync fallback — CHỈ dùng khi bắt buộc phải sync).
        /// Nếu đang ở UI thread, sẽ chặn UI thread. Ưu tiên dùng EnsureInitializedAsync().
        /// </summary>
        public static void EnsureInitialized()
        {
            if (IsInitialized) return;
            Initialize();
        }

        /// <summary>
        /// Pipeline khởi tạo 2 pha bất đồng bộ.
        /// Phase 1: Background thread — chuẩn bị CefSettings (I/O, paths, GPU config).
        /// Phase 2: UI thread @ Background priority — Cef.Initialize (bắt buộc gọi trên UI thread để match thread ID với Cef.Shutdown).
        /// </summary>
        private static void StartInitializationPipeline(TaskCompletionSource<bool> tcs)
        {
            Task.Run(() =>
            {
                try
                {
                    var settings = PrepareSettings();

                    var app = System.Windows.Application.Current;
                    if (app == null || app.Dispatcher == null)
                    {
                        lock (_lock) _initTcs = null;
                        tcs.TrySetResult(false);
                        return;
                    }

                    // Phase 2: Dispatcher.BeginInvoke lên UI Thread (ManagedThreadId 1) ở Normal priority
                    // Đảm bảo khởi tạo hoàn tất ngay trong quá trình mở ứng dụng trước khi MainWindow hiển thị
                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            lock (_lock)
                            {
                                if (_isInitialized || Cef.IsInitialized == true)
                                {
                                    _isInitialized = true;
                                    _initTcs = null;
                                    tcs.TrySetResult(true);
                                    return;
                                }

                                // Lấy DPI scale từ Application MainWindow nếu có
                                try
                                {
                                    if (app.MainWindow != null)
                                    {
                                        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(app.MainWindow);
                                        if (dpi.DpiScaleX > 0)
                                        {
                                            settings.CefCommandLineArgs["force-device-scale-factor"] =
                                                dpi.DpiScaleX.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                        }
                                    }
                                }
                                catch { }

                                // KHỞI TẠO CEF TRÊN UI THREAD (ManagedThreadId 1)
                                Cef.Initialize(settings, performDependencyCheck: false, browserProcessHandler: null);
                                _isInitialized = true;
                                _initTcs = null;
                            }

                            System.Diagnostics.Debug.WriteLine("[CefSharp] ✅ Initialized on UI thread (App Startup)");

                            // Pre-warm subprocess
                            PreWarmBrowserSubprocess();

                            tcs.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            lock (_lock) _initTcs = null;
                            System.Diagnostics.Debug.WriteLine($"[CefSharp] ❌ Init error: {ex.Message}");
                            tcs.TrySetException(ex);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Normal);
                }
                catch (Exception ex)
                {
                    lock (_lock) _initTcs = null;
                    tcs.TrySetException(ex);
                }
            });
        }

        /// <summary>
        /// Chuẩn bị CefSettings trên background thread (I/O đĩa, GPU detection, paths).
        /// Hàm này KHÔNG gọi Cef.Initialize() — chỉ tạo đối tượng CefSettings.
        /// </summary>
        private static CefSettings PrepareSettings()
        {
            var settings = new CefSettings();
            var cefRootDir = WebNodeCacheHelper.GetCefRootDir();
            var userProfilesDir = WebNodeCacheHelper.GetUserProfilesDir();
            var globalCachePath = Path.Combine(cefRootDir, "GlobalEngineCache");

            try
            {
                if (!Directory.Exists(cefRootDir))
                    Directory.CreateDirectory(cefRootDir);
                if (!Directory.Exists(userProfilesDir))
                    Directory.CreateDirectory(userProfilesDir);
                if (!Directory.Exists(globalCachePath))
                    Directory.CreateDirectory(globalCachePath);

                settings.RootCachePath = cefRootDir;
                settings.CachePath = globalCachePath;
            }
            catch { }

            // GPU detection (WMI query — chậm, chạy ở background)
            bool gpuAvailable = GpuDetectionHelper.IsGpuAvailable;

            // Tránh Chromium suspend/throttle khi ứng dụng ở background
            settings.CefCommandLineArgs.Add("disable-background-timer-throttling", "1");
            settings.CefCommandLineArgs.Add("disable-backgrounding-occluded-windows", "1");
            settings.CefCommandLineArgs.Add("disable-renderer-backgrounding", "1");
            settings.CefCommandLineArgs.Add("calculate-native-win-occlusion", "0");

            if (gpuAvailable)
            {
                settings.CefCommandLineArgs.Add("enable-gpu-rasterization", "1");
                settings.CefCommandLineArgs.Add("enable-zero-copy", "1");
                settings.CefCommandLineArgs.Add("ignore-gpu-blocklist", "1");
                settings.CefCommandLineArgs.Add("enable-accelerated-2d-canvas", "1");
            }
            else
            {
                settings.CefCommandLineArgs.Add("disable-gpu", "1");
            }

            // Cấu hình paths / resources / DevTools
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var nativeDir = Path.Combine(baseDir, "runtimes", Environment.Is64BitProcess ? "win-x64" : "win-x86", "native");
            if (Directory.Exists(nativeDir) && File.Exists(Path.Combine(nativeDir, "resources.pak")))
            {
                settings.ResourcesDirPath = nativeDir;
                settings.LocalesDirPath = Path.Combine(nativeDir, "locales");
                var subProcessPath = Path.Combine(nativeDir, "CefSharp.BrowserSubprocess.exe");
                if (File.Exists(subProcessPath))
                    settings.BrowserSubprocessPath = subProcessPath;
            }

            settings.RemoteDebuggingPort = 8088;
            settings.CefCommandLineArgs.Add("remote-allow-origins", "*");
            settings.CefCommandLineArgs.Add("enable-high-dpi-support", "1");
            settings.PersistSessionCookies = true;
            settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

            return settings;
        }

        /// <summary>
        /// Khởi tạo CefSharp đồng bộ (sync fallback).
        /// Ưu tiên dùng EnsureInitializedAsync().
        /// </summary>
        public static void Initialize()
        {
            if (IsInitialized) return;
            EnsureInitializedAsync().GetAwaiter().GetResult();
        }

        public static Task InitializeAsync() => EnsureInitializedAsync();

        /// <summary>
        /// Pre-warm browser subprocess bằng cách tạo 1 ChromiumWebBrowser ẩn.
        /// Khi user kéo WebNode vào canvas lần đầu, subprocess đã sẵn sàng → không bị đơ.
        /// </summary>
        private static void PreWarmBrowserSubprocess()
        {
            if (!_isInitialized) return;

            var app = System.Windows.Application.Current;
            if (app == null) return;

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // Tạo cửa sổ ẩn off-screen chứa ChromiumWebBrowser tạm
                    var tempWindow = new System.Windows.Window
                    {
                        Width = 1,
                        Height = 1,
                        WindowStyle = System.Windows.WindowStyle.None,
                        ShowInTaskbar = false,
                        ShowActivated = false,
                        AllowsTransparency = true,
                        Opacity = 0,
                        Left = -32000,
                        Top = -32000
                    };

                    var tempBrowser = new ChromiumWebBrowser("about:blank")
                    {
                        Visibility = System.Windows.Visibility.Collapsed
                    };
                    tempWindow.Content = tempBrowser;
                    tempWindow.Show();

                    System.Windows.DependencyPropertyChangedEventHandler? initHandler = null;
                    initHandler = (s, e) =>
                    {
                        if (tempBrowser.IsBrowserInitialized)
                        {
                            tempBrowser.IsBrowserInitializedChanged -= initHandler;
                            // Đợi idle rồi dọn dẹp
                            tempBrowser.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { tempBrowser.Dispose(); } catch { }
                                try { tempWindow.Close(); } catch { }
                            }), System.Windows.Threading.DispatcherPriority.SystemIdle);
                        }
                    };
                    tempBrowser.IsBrowserInitializedChanged += initHandler;
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.SystemIdle);
        }

        /// <summary>
        /// Dọn dẹp tài nguyên CefSharp khi thoát app.
        /// </summary>
        public static void Shutdown()
        {
            var app = System.Windows.Application.Current;
            if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                try { app.Dispatcher.Invoke(Shutdown); } catch { }
                return;
            }

            lock (_lock)
            {
                try
                {
                    foreach (var kvp in _profileContexts)
                    {
                        try
                        {
                            if (kvp.Value != null && !kvp.Value.IsDisposed)
                            {
                                var cm = kvp.Value.GetCookieManager(null);
                                cm?.FlushStore(null);
                            }
                        }
                        catch { }
                    }
                    try
                    {
                        var globalCm = Cef.GetGlobalCookieManager();
                        globalCm?.FlushStore(null);
                    }
                    catch { }

                    System.Threading.Thread.Sleep(400);

                    if (Cef.IsInitialized == true)
                    {
                        Cef.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CefSharp] Shutdown info: {ex.Message}");
                }
                finally
                {
                    _isInitialized = false;
                }
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RequestContext> _profileContexts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tạo hoặc lấy lại RequestContext riêng cho từng profile (Isolated mode).
        /// </summary>
        public static RequestContext CreateProfileRequestContext(string profileName)
        {
            var key = string.IsNullOrWhiteSpace(profileName) ? "Shared" : profileName.Trim();

            lock (_lock)
            {
                if (_profileContexts.TryGetValue(key, out var existing))
                {
                    if (existing != null && !existing.IsDisposed)
                    {
                        return existing;
                    }
                    _profileContexts.TryRemove(key, out _);
                }

                var profilePath = WebNodeCacheHelper.GetProfileCachePath(key);
                var options = new RequestContextSettings
                {
                    CachePath = profilePath,
                    PersistSessionCookies = true
                };
                var newRc = new RequestContext(options);
                _profileContexts[key] = newRc;
                return newRc;
            }
        }

        /// <summary>
        /// Hủy và đóng RequestContext của profile khi bị xóa.
        /// </summary>
        public static void DisposeProfileRequestContext(string profileName)
        {
            var key = string.IsNullOrWhiteSpace(profileName) ? "Shared" : profileName.Trim();
            if (_profileContexts.TryRemove(key, out var rc))
            {
                try { rc.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Chuyển đổi CefSharp IRequest thành lệnh cURL đầy đủ.
        /// </summary>
        public static string ConvertToCurlCommand(IRequest request)
        {
            if (request == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append($"curl -X {request.Method} \"{request.Url}\"");

            if (request.Headers != null)
            {
                foreach (string key in request.Headers)
                {
                    var val = request.Headers[key];
                    sb.Append($" -H \"{key}: {val.Replace("\"", "\\\"")}\"");
                }
            }

            if (request.PostData?.Elements != null && request.PostData.Elements.Count > 0)
            {
                foreach (var element in request.PostData.Elements)
                {
                    if (element.Type == PostDataElementType.Bytes && element.Bytes != null && element.Bytes.Length > 0)
                    {
                        var bodyText = Encoding.UTF8.GetString(element.Bytes);
                        sb.Append($" --data {JsonSerializer.Serialize(bodyText)}");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
