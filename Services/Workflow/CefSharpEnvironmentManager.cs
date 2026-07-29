using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
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

        /// <summary>
        /// Khởi tạo CefSharp toàn cục (gọi tại App.OnStartup).
        /// </summary>
        public static void Initialize()
        {
            if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(Initialize);
                return;
            }

            lock (_lock)
            {
                if (_isInitialized || Cef.IsInitialized == true)
                    return;

                var settings = new CefSettings();
                var cachePath = WebNodeCacheHelper.GetSharedRuntimeCachePath();

                try
                {
                    if (!Directory.Exists(cachePath))
                    {
                        Directory.CreateDirectory(cachePath);
                    }
                    settings.RootCachePath = cachePath;
                    settings.CachePath = Path.Combine(cachePath, "Default");
                }
                catch { }

                // Tính toán DPI scale factor của màn hình để Chromium OSR render sắc nét 1:1 pixel
                try
                {
                    double dpiScale = 1.0;
                    if (System.Windows.Application.Current?.MainWindow != null)
                    {
                        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(System.Windows.Application.Current.MainWindow);
                        if (dpi.DpiScaleX > 0) dpiScale = dpi.DpiScaleX;
                    }

                    if (dpiScale > 0)
                    {
                        settings.CefCommandLineArgs.Add("force-device-scale-factor", dpiScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
                catch { }

                // Tránh Chromium suspend/throttle khi ứng dụng ở background
                settings.CefCommandLineArgs.Add("disable-background-timer-throttling", "1");
                settings.CefCommandLineArgs.Add("disable-backgrounding-occluded-windows", "1");
                settings.CefCommandLineArgs.Add("disable-renderer-backgrounding", "1");
                settings.CefCommandLineArgs.Add("calculate-native-win-occlusion", "0");

                if (GpuDetectionHelper.IsGpuAvailable)
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

                // Cấu hình mã hóa / cookies / High DPI crisp rendering / DevTools Resources
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var nativeDir = Path.Combine(baseDir, "runtimes", Environment.Is64BitProcess ? "win-x64" : "win-x86", "native");
                if (Directory.Exists(nativeDir) && File.Exists(Path.Combine(nativeDir, "resources.pak")))
                {
                    settings.ResourcesDirPath = nativeDir;
                    settings.LocalesDirPath = Path.Combine(nativeDir, "locales");
                    var subProcessPath = Path.Combine(nativeDir, "CefSharp.BrowserSubprocess.exe");
                    if (File.Exists(subProcessPath))
                    {
                        settings.BrowserSubprocessPath = subProcessPath;
                    }
                }

                settings.RemoteDebuggingPort = 8088;
                settings.CefCommandLineArgs.Add("remote-allow-origins", "*");
                settings.CefCommandLineArgs.Add("enable-high-dpi-support", "1");
                settings.PersistSessionCookies = true;
                settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

                // Tắt performDependencyCheck để tránh quét đĩa chậm UI thread khi khởi động
                Cef.Initialize(settings, performDependencyCheck: false, browserProcessHandler: null);
                _isInitialized = true;
            }
        }

        public static Task InitializeAsync()
        {
            Initialize();
            return Task.CompletedTask;
        }

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
            if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(Shutdown);
                return;
            }

            lock (_lock)
            {
                if (Cef.IsInitialized == true)
                {
                    Cef.Shutdown();
                }
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Tạo RequestContext riêng cho từng profile (Isolated mode).
        /// </summary>
        public static RequestContext CreateProfileRequestContext(string profileName)
        {
            var profilePath = WebNodeCacheHelper.GetProfileCachePath(profileName);
            var options = new RequestContextSettings
            {
                CachePath = profilePath,
                PersistSessionCookies = true
            };
            return new RequestContext(options);
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
