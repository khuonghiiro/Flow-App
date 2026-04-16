using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Windows.Media;

namespace FlowMy.Services.Rendering
{
    /// <summary>
    /// Helper class để nhận diện GPU trên hệ thống
    /// Kiểm tra hardware acceleration và GPU availability để quyết định có nên dùng GPU optimization hay không
    /// </summary>
    public static class GpuDetectionHelper
    {
        private static bool? _isGpuAvailable = null;
        private static string _gpuName = null;
        private static int _renderTier = -1;

        /// <summary>
        /// Kiểm tra xem GPU có khả dụng không
        /// </summary>
        public static bool IsGpuAvailable
        {
            get
            {
                if (_isGpuAvailable == null)
                {
                    _isGpuAvailable = DetectGpuAvailability();
                }
                return _isGpuAvailable.Value;
            }
        }

        /// <summary>
        /// Lấy tên GPU (nếu có)
        /// </summary>
        public static string GpuName
        {
            get
            {
                if (_gpuName == null)
                {
                    _gpuName = GetGpuName();
                }
                return _gpuName ?? "Unknown";
            }
        }

        /// <summary>
        /// Lấy Render Tier của WPF
        /// Tier 0 = Software rendering (CPU only)
        /// Tier 1 = Partial hardware acceleration
        /// Tier 2 = Full hardware acceleration (GPU)
        /// </summary>
        public static int RenderTier
        {
            get
            {
                if (_renderTier == -1)
                {
                    _renderTier = RenderCapability.Tier >> 16;
                }
                return _renderTier;
            }
        }

        /// <summary>
        /// Kiểm tra GPU availability bằng nhiều phương pháp
        /// </summary>
        private static bool DetectGpuAvailability()
        {
            try
            {
                // Phương pháp 1: Kiểm tra WPF Render Tier
                int tier = RenderCapability.Tier >> 16;
                if (tier >= 1)
                {
                    // Tier 1 hoặc 2 = có hardware acceleration
                    return true;
                }

                // Phương pháp 2: Kiểm tra Pixel Shader support
                if (RenderCapability.IsPixelShaderVersionSupported(2, 0))
                {
                    return true;
                }

                // Phương pháp 3: Kiểm tra GPU qua WMI (Windows Management Instrumentation)
                if (HasGpuViaWmi())
                {
                    return true;
                }

                // Nếu không có GPU, dùng CPU
                return false;
            }
            catch (Exception)
            {
                // Nếu có lỗi, mặc định dùng CPU để đảm bảo ứng dụng vẫn chạy được
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra GPU qua WMI để lấy thông tin chi tiết
        /// </summary>
        private static bool HasGpuViaWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString();
                        var status = obj["Status"]?.ToString();
                        var availability = obj["Availability"]?.ToString();

                        // Bỏ qua các GPU ảo hoặc không hoạt động
                        if (string.IsNullOrEmpty(name) || 
                            name.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Basic", StringComparison.OrdinalIgnoreCase) ||
                            (status != null && !status.Equals("OK", StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        // Nếu có GPU thật và đang hoạt động
                        if (!string.IsNullOrEmpty(name) && 
                            (status == null || status.Equals("OK", StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // WMI có thể không khả dụng trên một số hệ thống, bỏ qua lỗi
            }

            return false;
        }

        /// <summary>
        /// Lấy tên GPU từ WMI
        /// </summary>
        private static string GetGpuName()
        {
            try
            {
                var gpuNames = new List<string>();

                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString();
                        var status = obj["Status"]?.ToString();

                        // Chỉ lấy GPU thật, bỏ qua GPU ảo
                        if (!string.IsNullOrEmpty(name) && 
                            !name.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Basic", StringComparison.OrdinalIgnoreCase) &&
                            (status == null || status.Equals("OK", StringComparison.OrdinalIgnoreCase)))
                        {
                            gpuNames.Add(name);
                        }
                    }
                }

                return gpuNames.Any() ? string.Join(", ", gpuNames) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Reset cache để kiểm tra lại GPU (hữu ích khi GPU được cắm/tháo)
        /// </summary>
        public static void ResetCache()
        {
            _isGpuAvailable = null;
            _gpuName = null;
            _renderTier = -1;
        }

        /// <summary>
        /// Lấy thông tin chi tiết về GPU và Render Tier để debug
        /// </summary>
        public static string GetGpuInfo()
        {
            return $"GPU Available: {IsGpuAvailable}, GPU Name: {GpuName}, Render Tier: {RenderTier}";
        }
    }
}

