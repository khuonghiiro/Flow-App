using System;
using System.IO;
using System.Threading.Tasks;
using FlowMy.Services.Rendering;
using Microsoft.Web.WebView2.Core;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Quản lý <see cref="CoreWebView2Environment"/> dùng chung cho toàn app.
/// - Chỉ khởi tạo 1 lần (lazy, thread-safe).
/// - Dùng chung cache folder (UserDataFolder) để webview node / html node khởi động nhanh hơn.
/// - Có hàm WarmUpAsync để pre-init khi app start.
/// </summary>
public static class WebView2EnvironmentManager
{
    private static readonly object _lock = new();
    private static Task<CoreWebView2Environment>? _sharedEnvTask;

    /// <summary>
    /// Lấy environment dùng chung cho toàn ứng dụng.
    /// Lần đầu sẽ khởi tạo (CreateAsync), các lần sau tái sử dụng lại.
    /// </summary>
    public static Task<CoreWebView2Environment> GetSharedEnvironmentAsync()
    {
        lock (_lock)
        {
            // Nếu chưa có task hoặc task trước đó bị faulted/cancelled thì tạo lại.
            if (_sharedEnvTask == null ||
                _sharedEnvTask.IsCanceled ||
                _sharedEnvTask.IsFaulted)
            {
                _sharedEnvTask = CreateEnvironmentAsync();
            }

            return _sharedEnvTask;
        }
    }

    /// <summary>
    /// Gọi sớm (ví dụ trong App startup) để pre-init WebView2.
    /// </summary>
    public static Task WarmUpAsync() => GetSharedEnvironmentAsync();

    private static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        // Dùng chung cache folder cho tất cả WebView2
        var cachePath = WebNodeCacheHelper.GetSharedRuntimeCachePath();
        try
        {
            Directory.CreateDirectory(cachePath);
        }
        catch
        {
            // Nếu tạo thư mục thất bại thì vẫn cố gắng tiếp tục với đường dẫn mặc định của WebView2
            cachePath = null!;
        }

        var options = new CoreWebView2EnvironmentOptions();

        if (GpuDetectionHelper.IsGpuAvailable)
        {
            // Bật GPU acceleration giống như trong WebNodeControl / HtmlUiNodeControl
            var gpuArgs = new System.Text.StringBuilder();
            gpuArgs.Append("--enable-gpu-rasterization ");
            gpuArgs.Append("--enable-zero-copy ");
            gpuArgs.Append("--enable-features=VaapiVideoDecoder ");
            gpuArgs.Append("--ignore-gpu-blacklist ");
            gpuArgs.Append("--enable-accelerated-2d-canvas ");
            gpuArgs.Append("--enable-accelerated-video-decode ");

            options.AdditionalBrowserArguments = gpuArgs.ToString();
        }
        else
        {
            options.AdditionalBrowserArguments = "--disable-gpu";
        }

        // Nếu cachePath null (tạo thư mục lỗi) thì cho WebView2 tự chọn UserDataFolder
        if (string.IsNullOrWhiteSpace(cachePath))
            return await CoreWebView2Environment.CreateAsync(null, null, options);

        return await CoreWebView2Environment.CreateAsync(null, cachePath, options);
    }
}

