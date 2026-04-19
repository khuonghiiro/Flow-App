using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ShapesPath = System.Windows.Shapes.Path;
using FlowMy.Properties;

namespace FlowMy.Services.Rendering
{
    /// <summary>
    /// Helper class để áp dụng GPU acceleration optimizations cho UI elements
    /// Giúp giảm lag và giật màn hình khi render nhiều nodes
    /// Tự động nhận diện GPU và chỉ áp dụng optimization khi có GPU, nếu không thì dùng CPU mặc định
    /// </summary>
    public static class GpuOptimizationHelper
    {
        /// <summary>
        /// Kiểm tra xem có nên áp dụng GPU optimization không
        /// </summary>
        private static bool ShouldApplyGpuOptimization()
        {
            // Kiểm tra GPU có sẵn và user đã bật GPU trong settings
            if (!GpuDetectionHelper.IsGpuAvailable) return false;
            
            try
            {
                // Lấy setting từ user preferences
                return Settings.Default.GpuEnabled;
            }
            catch
            {
                // Nếu không lấy được setting, mặc định là true nếu có GPU
                return true;
            }
        }
        
        /// <summary>
        /// Lấy GpuRenderQuality từ user settings.
        /// Khi GPU bị tắt trong settings, coerce về Low để đảm bảo các hiệu ứng
        /// nặng (drop shadow, edge smoothing cao) không được bật khi user đã
        /// chủ động tắt GPU — tránh mâu thuẫn logic giữa GPU=OFF và Quality=High/Best.
        /// </summary>
        public static GpuRenderQuality GetGpuRenderQuality()
        {
            try
            {
                var raw = (GpuRenderQuality)Settings.Default.GpuRenderQuality;

                if (!Settings.Default.GpuEnabled && raw > GpuRenderQuality.Low)
                {
                    return GpuRenderQuality.Low;
                }

                return raw;
            }
            catch
            {
                // Mặc định dùng Medium
                return GpuRenderQuality.Medium;
            }
        }

        /// <summary>
        /// Lấy GpuRenderQuality "thô" đúng như user đã chọn, không coerce.
        /// Dùng cho các trường hợp dialog cấu hình muốn hiển thị giá trị gốc.
        /// </summary>
        public static GpuRenderQuality GetRawGpuRenderQuality()
        {
            try
            {
                return (GpuRenderQuality)Settings.Default.GpuRenderQuality;
            }
            catch
            {
                return GpuRenderQuality.Medium;
            }
        }
        /// <summary>
        /// Safely remove UIElement from canvas
        /// </summary>
        public static void SafeRemoveFromCanvas(UIElement element, Panel canvas)
        {
            if (element == null || canvas == null || !canvas.Children.Contains(element)) return;
            
            // Clear cache nếu có (tương thích với code cũ)
            element.CacheMode = null;
            
            // Force invalidate visual để đảm bảo visual được xóa hoàn toàn
            element.InvalidateVisual();
            
            // Remove khỏi canvas
            canvas.Children.Remove(element);
        }
        /// <summary>
        /// Áp dụng GPU optimizations cho Border (nodes)
        /// Tránh ghost effects bằng cách không cache và invalidate đúng cách
        /// Đảm bảo coordinate mapping chính xác
        /// </summary>
        /// <summary>
        /// Áp dụng GPU optimizations cho Border (nodes)
        /// Sử dụng GPU rendering options (BitmapScalingMode, EdgeMode) để enable GPU rendering
        /// Không dùng BitmapCache vì nó gây overhead khi elements thay đổi thường xuyên
        /// </summary>
        public static void ApplyToBorder(Border border, bool isDragging = false, bool? forceCache = null)
        {
            if (border == null) return;

            // Luôn lấy preset quality từ settings để áp dụng cho cả CPU/GPU,
            // chỉ khác nhau ở việc có dùng cache GPU hay không.
            var quality = GetGpuRenderQuality();

            // Chia sẻ chung các thiết lập chất lượng cho cả CPU lẫn GPU
            border.UseLayoutRounding = GpuRenderQualityHelper.ShouldUseLayoutRounding(quality);
            border.SnapsToDevicePixels = GpuRenderQualityHelper.ShouldSnapToDevicePixels(quality);
            RenderOptions.SetBitmapScalingMode(border, GpuRenderQualityHelper.GetBitmapScalingMode(quality));
            RenderOptions.SetEdgeMode(border, GpuRenderQualityHelper.GetEdgeMode(quality));

            // - Nếu forceCache != null: cưỡng bức bật/tắt BitmapCache theo checkbox.
            // - Nếu forceCache == null: dùng logic cũ (chỉ cache khi GPU bật + quality <= Medium).
            var shouldCache = forceCache.HasValue
                ? forceCache.Value
                : (ShouldApplyGpuOptimization() && quality <= GpuRenderQuality.Medium);

            // Lưu ý: luôn tắt cache khi đang kéo để tránh ghost/artifacts.
            if (shouldCache && !isDragging)
            {
                // Snap Canvas.Left/Top về pixel nguyên TRƯỚC khi bật BitmapCache.
                // BitmapCache render bitmap ở offset subpixel sẽ bị resample (bilinear)
                // → node trông "mờ" sau khi di chuyển. Snap đảm bảo bitmap được paint
                // chính xác trên grid pixel, giữ độ sắc nét như lúc mới rơi vào canvas.
                var left = System.Windows.Controls.Canvas.GetLeft(border);
                var top = System.Windows.Controls.Canvas.GetTop(border);
                if (!double.IsNaN(left))
                {
                    var rl = System.Math.Round(left);
                    if (rl != left) System.Windows.Controls.Canvas.SetLeft(border, rl);
                }
                if (!double.IsNaN(top))
                {
                    var rt = System.Math.Round(top);
                    if (rt != top) System.Windows.Controls.Canvas.SetTop(border, rt);
                }

                // TỐI ƯU: Dùng BitmapCache cho static nodes (không đang drag) để tăng tốc GPU rendering
                RenderOptions.SetCachingHint(border, CachingHint.Cache);
                // Dùng BitmapCache với RenderAtScale = 1.0 để tối ưu hiệu suất
                // Không dùng EnableClearType để giảm overhead
                border.CacheMode = new BitmapCache
                {
                    EnableClearType = false, // Tắt ClearType để giảm overhead
                    RenderAtScale = 1.0,     // Render ở scale 1:1 để tối ưu
                    SnapsToDevicePixels = GpuRenderQualityHelper.ShouldSnapToDevicePixels(quality)
                };

                // Đảm bảo không có visual artifacts
                border.ClipToBounds = false;
            }
            else
            {
                // Nếu không có GPU hoặc user tắt GPU: vẫn áp dụng preset quality nhưng không cache
                RenderOptions.SetCachingHint(border, CachingHint.Unspecified);
                border.CacheMode = null;
            }
        }
        
        /// <summary>
        /// Invalidate node border để tránh ghost effects khi di chuyển
        /// Gọi method này khi di chuyển node để đảm bảo render mới
        /// </summary>
        public static void InvalidateNodeBorder(Border border)
        {
            if (border == null) return;
            
            // Clear cache nếu có
            if (border.CacheMode != null)
            {
                border.CacheMode = null;
            }
            
            // Invalidate để force re-render
            border.InvalidateVisual();
            border.InvalidateArrange();
            border.InvalidateMeasure();
            
            // Invalidate parent canvas nếu có
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(border) as UIElement;
            if (parent != null)
            {
                parent.InvalidateVisual();
            }
        }

        /// <summary>
        /// Áp dụng GPU optimizations cho UIElement (Grid, StackPanel, etc.)
        /// Chỉ áp dụng khi có GPU, nếu không thì dùng CPU mặc định
        /// </summary>
        public static void ApplyToElement(UIElement element)
        {
            if (element == null) return;

            var quality = GetGpuRenderQuality();

            if (element is FrameworkElement fe)
            {
                fe.UseLayoutRounding = GpuRenderQualityHelper.ShouldUseLayoutRounding(quality);
                fe.SnapsToDevicePixels = GpuRenderQualityHelper.ShouldSnapToDevicePixels(quality);
            }

            // Áp dụng preset chất lượng cho cả CPU/GPU
            RenderOptions.SetBitmapScalingMode(element, GpuRenderQualityHelper.GetBitmapScalingMode(quality));
            RenderOptions.SetEdgeMode(element, GpuRenderQualityHelper.GetEdgeMode(quality));

            // Không cache mặc định cho UIElement; nếu cần cache GPU sẽ xử lý riêng
            RenderOptions.SetCachingHint(element, CachingHint.Unspecified);
            if (element is FrameworkElement fe2)
            {
                fe2.CacheMode = null;
            }
        }

        /// <summary>
        /// Áp dụng GPU optimizations cho Path (connection lines, shapes)
        /// Tránh ghost effects bằng cách không cache khi có animation hoặc di chuyển
        /// </summary>
        public static void ApplyToPath(ShapesPath path, bool allowCache = true)
        {
            if (path == null) return;

            var quality = GetGpuRenderQuality();

            // Path: luôn giữ UseLayoutRounding = false để không phá anti-aliasing,
            // còn lại dùng preset quality cho cả CPU/GPU.
            path.UseLayoutRounding = false;
            path.SnapsToDevicePixels = GpuRenderQualityHelper.ShouldSnapToDevicePixels(quality);
            RenderOptions.SetBitmapScalingMode(path, GpuRenderQualityHelper.GetBitmapScalingMode(quality));
            RenderOptions.SetEdgeMode(path, GpuRenderQualityHelper.GetEdgeMode(quality));

            // Chỉ dùng BitmapCache khi:
            // - Có GPU,
            // - Cho phép cache (allowCache),
            // - Và quality không quá cao (Low/Medium). Với High/Best, giữ vector thuần để zoom max không bị mờ.
            if (ShouldApplyGpuOptimization() && allowCache && quality <= GpuRenderQuality.Medium)
            {
                RenderOptions.SetCachingHint(path, CachingHint.Cache);
                // Dùng BitmapCache với RenderAtScale = 1.0 và tắt ClearType để tối ưu
                path.CacheMode = new BitmapCache
                {
                    EnableClearType = false, // Tắt ClearType để giảm overhead
                    RenderAtScale = 1.0,      // Render ở scale 1:1 để tối ưu
                    SnapsToDevicePixels = GpuRenderQualityHelper.ShouldSnapToDevicePixels(quality)
                };
            }
            else
            {
                // Không cache khi không có GPU, không cho phép cache, hoặc đang ở quality High/Best
                RenderOptions.SetCachingHint(path, CachingHint.Unspecified);
                path.CacheMode = null;
            }
        }
        
        /// <summary>
        /// Invalidate connection path để tránh ghost effects và đảm bảo vị trí chính xác
        /// Tối ưu: Chỉ invalidate khi thực sự cần, không invalidate mỗi frame
        /// </summary>
        public static void InvalidateConnectionPath(ShapesPath path)
        {
            if (path == null) return;
            
            // Tối ưu: Không clear cache mỗi lần - chỉ invalidate visual
            // Cache sẽ được update tự động khi Data thay đổi
            path.InvalidateVisual();
        }
        
        /// <summary>
        /// Clear cache của connection path (chỉ dùng khi cần thiết)
        /// </summary>
        public static void ClearConnectionPathCache(ShapesPath path)
        {
            if (path == null) return;
            
            // Clear cache để tránh stale visuals
            if (path.CacheMode != null)
            {
                path.CacheMode = null;
            }
        }

        /// <summary>
        /// Áp dụng GPU optimizations cho Shape (Ellipse, Rectangle, etc.) - dùng cho ports
        /// Tránh ghost effects và đảm bảo vị trí chính xác
        /// </summary>
        public static void ApplyToShape(System.Windows.Shapes.Shape shape)
        {
            if (shape == null) return;

            var quality = GetGpuRenderQuality();

            // IMPORTANT: circles/rounded shapes need anti-aliasing
            // Không dùng UseLayoutRounding để giữ anti-aliasing cho hình tròn/bo góc
            shape.UseLayoutRounding = false;
            shape.SnapsToDevicePixels = GpuRenderQualityHelper.ShouldSnapToDevicePixels(quality);

            // Áp dụng preset chất lượng cho cả CPU/GPU
            RenderOptions.SetBitmapScalingMode(shape, GpuRenderQualityHelper.GetBitmapScalingMode(quality));
            RenderOptions.SetEdgeMode(shape, GpuRenderQualityHelper.GetEdgeMode(quality));

            // Không cache ports để tránh ghost effects khi di chuyển node
            RenderOptions.SetCachingHint(shape, CachingHint.Unspecified);
            shape.CacheMode = null;
            
            // Đảm bảo không clip để port hiển thị đúng
            if (shape is FrameworkElement fe)
            {
                fe.ClipToBounds = false;
            }
        }
        
        /// <summary>
        /// Invalidate port shape để tránh ghost effects và đảm bảo vị trí chính xác
        /// </summary>
        public static void InvalidatePortShape(System.Windows.Shapes.Shape shape)
        {
            if (shape == null) return;
            
            // Clear cache nếu có
            if (shape.CacheMode != null)
            {
                shape.CacheMode = null;
            }
            
            // Invalidate để force re-render với vị trí mới
            shape.InvalidateVisual();
            
            // Invalidate parent nếu có (Border wrapper)
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(shape) as UIElement;
            if (parent != null)
            {
                parent.InvalidateVisual();
            }
        }

        /// <summary>
        /// Tạo DropShadowEffect dựa trên GPU quality settings
        /// Trả về null cho Low/Medium để tối ưu hiệu suất
        /// </summary>
        public static System.Windows.Media.Effects.DropShadowEffect? CreateDropShadowEffect()
        {
            var quality = GetGpuRenderQuality();
            
            // Chỉ tạo drop shadow cho High và Best quality
            if (!GpuRenderQualityHelper.ShouldEnableDropShadows(quality))
            {
                return null;
            }
            
            // Tạo drop shadow với cấu hình mặc định
            return new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 3,
                Opacity = 0.5
            };
        }

        /// <summary>
        /// Áp dụng GPU optimizations cho toàn bộ visual tree của một element
        /// </summary>
        public static void ApplyToVisualTree(DependencyObject element)
        {
            if (element == null) return;

            // Apply to current element
            if (element is Border border)
            {
                ApplyToBorder(border);
            }
            else if (element is ShapesPath path)
            {
                ApplyToPath(path);
            }
            else if (element is Shape shape)
            {
                ApplyToShape(shape);
            }
            else if (element is UIElement uiElement)
            {
                ApplyToElement(uiElement);
            }

            // Recursively apply to children
            int childrenCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                ApplyToVisualTree(child);
            }
        }
    }
}
