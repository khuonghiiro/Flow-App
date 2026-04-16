using System.Windows.Media;

namespace FlowMy.Services.Rendering
{
    /// <summary>
    /// Enum định nghĩa các mức chất lượng render GPU
    /// </summary>
    public enum GpuRenderQuality
    {
        /// <summary>
        /// Thấp - Tối ưu hiệu suất, độ phân giải thấp
        /// </summary>
        Low = 0,
        
        /// <summary>
        /// Trung bình - Cân bằng giữa hiệu suất và chất lượng
        /// </summary>
        Medium = 1,
        
        /// <summary>
        /// Cao - Chất lượng tốt, hiệu suất ổn định
        /// </summary>
        High = 2,
        
        /// <summary>
        /// Cao nhất - Chất lượng tốt nhất, có thể ảnh hưởng hiệu suất
        /// </summary>
        Best = 3
    }
    
    /// <summary>
    /// Helper class để chuyển đổi GpuRenderQuality sang BitmapScalingMode
    /// </summary>
    public static class GpuRenderQualityHelper
    {
        /// <summary>
        /// Chuyển đổi GpuRenderQuality sang BitmapScalingMode
        /// </summary>
        public static BitmapScalingMode GetBitmapScalingMode(GpuRenderQuality quality)
        {
            return quality switch
            {
                GpuRenderQuality.Low => BitmapScalingMode.LowQuality,
                GpuRenderQuality.Medium => BitmapScalingMode.Linear,
                GpuRenderQuality.High => BitmapScalingMode.HighQuality,
                GpuRenderQuality.Best => BitmapScalingMode.Fant,
                _ => BitmapScalingMode.LowQuality
            };
        }
        
        /// <summary>
        /// Lấy tên hiển thị cho quality level
        /// </summary>
        public static string GetDisplayName(GpuRenderQuality quality)
        {
            return quality switch
            {
                GpuRenderQuality.Low => "Thấp - Máy yếu (Tối ưu hiệu suất, dành cho PC cũ)",
                GpuRenderQuality.Medium => "Trung bình - Máy TB (Cân bằng, dành cho PC phổ thông)",
                GpuRenderQuality.High => "Cao - Máy mạnh (Đẹp + mượt, dành cho PC gaming)",
                GpuRenderQuality.Best => "Tốt nhất - Máy siêu mạnh (Ultra, dành cho PC cao cấp)",
                _ => "Thấp"
            };
        }
        
        /// <summary>
        /// Lấy mô tả ngắn cho quality level
        /// </summary>
        public static string GetShortDescription(GpuRenderQuality quality)
        {
            return quality switch
            {
                GpuRenderQuality.Low => "Tối ưu",
                GpuRenderQuality.Medium => "Ổn định",
                GpuRenderQuality.High => "Cân bằng",
                GpuRenderQuality.Best => "Đẹp nhất",
                _ => "Tối ưu"
            };
        }
        
        /// <summary>
        /// Lấy EdgeMode dựa trên quality level
        /// Unspecified = WPF tự động dùng anti-aliasing (mịn màng)
        /// Aliased = Không anti-aliasing (răng cưa, nhưng nhanh hơn)
        /// </summary>
        public static EdgeMode GetEdgeMode(GpuRenderQuality quality)
        {
            return quality switch
            {
                GpuRenderQuality.Low => EdgeMode.Aliased, // Tối ưu hiệu suất
                GpuRenderQuality.Medium => EdgeMode.Unspecified, // Cân bằng
                GpuRenderQuality.High => EdgeMode.Unspecified, // Chất lượng tốt
                GpuRenderQuality.Best => EdgeMode.Unspecified, // Đẹp nhất - luôn dùng anti-aliasing
                _ => EdgeMode.Aliased
            };
        }
        
        /// <summary>
        /// Kiểm tra xem có nên dùng layout rounding không (dựa trên quality)
        /// Layout rounding có thể gây răng cưa ở quality cao
        /// </summary>
        public static bool ShouldUseLayoutRounding(GpuRenderQuality quality)
        {
            return quality switch
            {
                GpuRenderQuality.Low => true, // Tối ưu hiệu suất
                GpuRenderQuality.Medium => true, // Cân bằng
                GpuRenderQuality.High => false, // Chất lượng tốt - không dùng để tránh răng cưa
                GpuRenderQuality.Best => false, // Đẹp nhất - không dùng để đảm bảo mịn màng
                _ => true
            };
        }
        
        /// <summary>
        /// Kiểm tra xem có nên dùng snaps to device pixels không (dựa trên quality)
        /// </summary>
        public static bool ShouldSnapToDevicePixels(GpuRenderQuality quality)
        {
            return quality switch
            {
                GpuRenderQuality.Low => true, // Tối ưu hiệu suất
                GpuRenderQuality.Medium => true, // Cân bằng
                GpuRenderQuality.High => false, // Chất lượng tốt
                GpuRenderQuality.Best => false, // Đẹp nhất - không snap để đảm bảo mịn màng
                _ => true
            };
        }
        
        /// <summary>
        /// Kiểm tra xem có nên bật drop shadow effects không (dựa trên quality)
        /// Drop shadow tốn nhiều tài nguyên, chỉ bật ở quality cao
        /// </summary>
        public static bool ShouldEnableDropShadows(GpuRenderQuality quality)
        {
            return quality switch
            {
                GpuRenderQuality.Low => false, // Tắt để tối ưu hiệu suất cho máy yếu
                GpuRenderQuality.Medium => false, // Tắt để cân bằng cho máy TB
                GpuRenderQuality.High => true, // Bật cho máy mạnh
                GpuRenderQuality.Best => true, // Bật cho máy siêu mạnh
                _ => false
            };
        }
    }
}

