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

                // Cấu hình mã hóa / cookies / remote debugging nếu cần
                settings.PersistSessionCookies = true;
                settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

                Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
                _isInitialized = true;
            }
        }

        public static Task InitializeAsync()
        {
            Initialize();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Dọn dẹp tài nguyên CefSharp khi thoát app.
        /// </summary>
        public static void Shutdown()
        {
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
