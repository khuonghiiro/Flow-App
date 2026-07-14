using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using FlowMy.Extensions;

namespace FlowMy.Models.ImageEditor
{
    /// <summary>
    /// Hệ thống tối ưu hóa hiệu suất xử lý ảnh giống Adobe Photoshop.
    /// Bao gồm: Proxy/Preview system, Background threading, Tile processing, Caching, GPU acceleration.
    /// </summary>
    public static class ImageProcessingOptimization
    {
        // ═══════════════════════════════════════════════════════
        // 1. PROXY/PREVIEW SYSTEM
        // ═══════════════════════════════════════════════════════
        
        public const int MAX_PREVIEW_SIZE = 1024;
        public const int PROGRESSIVE_DRAFT_SIZE = 512;
        public const int PROGRESSIVE_QUICK_SIZE = 256;

        /// <summary>
        /// Tạo preview resolution thấp hơn cho hiển thị UI (giống Photoshop proxy).
        /// </summary>
        public static BitmapSource CreatePreview(BitmapSource source, int maxSize = MAX_PREVIEW_SIZE)
        {
            if (source == null) return null;
            
            int maxDimension = Math.Max(source.PixelWidth, source.PixelHeight);
            if (maxDimension <= maxSize)
            {
                source.Freeze();
                return source;
            }

            double scale = (double)maxSize / maxDimension;
            int newWidth = (int)(source.PixelWidth * scale);
            int newHeight = (int)(source.PixelHeight * scale);

            var transform = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            transform.Freeze();
            return transform;
        }

        /// <summary>
        /// Tạo preview với scale factor tùy chỉnh.
        /// </summary>
        public static BitmapSource CreateScaledPreview(BitmapSource source, double scaleFactor)
        {
            if (source == null || scaleFactor >= 1.0) return source;

            int newWidth = (int)(source.PixelWidth * scaleFactor);
            int newHeight = (int)(source.PixelHeight * scaleFactor);

            var transform = new TransformedBitmap(source, new ScaleTransform(scaleFactor, scaleFactor));
            transform.Freeze();
            return transform;
        }

        /// <summary>
        /// Convert MagickImage sang BitmapSource với tối ưu hóa.
        /// </summary>
        public static BitmapSource ConvertToOptimizedBitmap(MagickImage magick, bool freeze = true)
        {
            if (magick == null) return null;

            // Use extension method from BitmapSourceExtensions
            var bitmap = magick.ToBitmapSource();
            if (freeze && !bitmap.IsFrozen)
                bitmap.Freeze();
            return bitmap;
        }

        // ═══════════════════════════════════════════════════════
        // 2. LAYER CACHE SYSTEM
        // ═══════════════════════════════════════════════════════

        private static readonly ConcurrentDictionary<string, WeakReference<BitmapSource>> _layerCache 
            = new ConcurrentDictionary<string, WeakReference<BitmapSource>>();

        /// <summary>
        /// Lấy hoặc tạo bitmap từ cache (memory-efficient với WeakReference).
        /// </summary>
        public static BitmapSource GetOrCreateCached(string key, Func<BitmapSource> factory)
        {
            if (_layerCache.TryGetValue(key, out var weakRef) && weakRef.TryGetTarget(out var cached))
            {
                return cached;
            }

            var bitmap = factory();
            if (bitmap != null)
            {
                bitmap.Freeze();
                _layerCache[key] = new WeakReference<BitmapSource>(bitmap);
            }
            return bitmap;
        }

        /// <summary>
        /// Xóa cache entry cụ thể.
        /// </summary>
        public static void InvalidateCache(string key)
        {
            _layerCache.TryRemove(key, out _);
        }

        /// <summary>
        /// Dọn dẹp toàn bộ cache (gọi khi memory thấp).
        /// </summary>
        public static void ClearAllCache()
        {
            _layerCache.Clear();
            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced);
        }

        // ═══════════════════════════════════════════════════════
        // 3. TILE-BASED PROCESSING (cho ảnh lớn)
        // ═══════════════════════════════════════════════════════

        public const int DEFAULT_TILE_SIZE = 512;
        public const int LARGE_IMAGE_THRESHOLD = 2000; // >= 2000px sẽ dùng tile processing

        /// <summary>
        /// Xử lý ảnh lớn theo từng tile nhỏ để giảm memory pressure.
        /// </summary>
        public static async Task<MagickImage> ProcessLargeImageInTiles(
            MagickImage source,
            Action<MagickImage> operation,
            int tileSize = DEFAULT_TILE_SIZE,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            int width = (int)source.Width;
            int height = (int)source.Height;

            // Nếu ảnh nhỏ, xử lý trực tiếp
            if (width < LARGE_IMAGE_THRESHOLD && height < LARGE_IMAGE_THRESHOLD)
            {
                await Task.Run(() => operation(source), cancellationToken);
                return source;
            }

            var result = (MagickImage)source.Clone();

            await Task.Run(() =>
            {
                int totalTiles = ((width + tileSize - 1) / tileSize) * ((height + tileSize - 1) / tileSize);
                int processedTiles = 0;

                for (int y = 0; y < height; y += tileSize)
                {
                    for (int x = 0; x < width; x += tileSize)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int tileW = Math.Min(tileSize, width - x);
                        int tileH = Math.Min(tileSize, height - y);

                        using (var tile = (MagickImage)source.Clone(new MagickGeometry(x, y, (uint)tileW, (uint)tileH)))
                        {
                            operation(tile);
                            // Composite tile back to result at original position
                            result.Composite(tile, x, y, CompositeOperator.Over);
                        }

                        processedTiles++;
                        progress?.Report((processedTiles * 100) / totalTiles);
                    }
                }
            }, cancellationToken);

            return result;
        }

        // ═══════════════════════════════════════════════════════
        // 4. BACKGROUND THREADING HELPERS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Thực thi operation trên background thread và trả về kết quả.
        /// </summary>
        public static async Task<T> RunOnBackgroundAsync<T>(Func<T> operation, CancellationToken cancellationToken = default)
        {
            return await Task.Run(operation, cancellationToken);
        }

        /// <summary>
        /// Thực thi operation trên background thread (không return value).
        /// </summary>
        public static async Task RunOnBackgroundAsync(Action operation, CancellationToken cancellationToken = default)
        {
            await Task.Run(operation, cancellationToken);
        }

        // ═══════════════════════════════════════════════════════
        // 5. DEBOUNCE HELPER (cho realtime adjustments)
        // ═══════════════════════════════════════════════════════

        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens 
            = new ConcurrentDictionary<string, CancellationTokenSource>();

        /// <summary>
        /// Debounce action với delay (giống Photoshop chỉ xử lý sau khi user ngừng kéo slider).
        /// </summary>
        public static async Task DebounceAsync(string key, Func<Task> action, int delayMs = 150)
        {
            // Cancel previous debounce
            if (_debounceTokens.TryRemove(key, out var oldToken))
            {
                oldToken.Cancel();
                oldToken.Dispose();
            }

            var newToken = new CancellationTokenSource();
            _debounceTokens[key] = newToken;

            try
            {
                await Task.Delay(delayMs, newToken.Token);
                await action();
            }
            catch (TaskCanceledException) { }
            finally
            {
                if (_debounceTokens.TryGetValue(key, out var currentToken) && currentToken == newToken)
                {
                    _debounceTokens.TryRemove(key, out _);
                }
                newToken.Dispose();
            }
        }

        // ═══════════════════════════════════════════════════════
        // 6. PROGRESSIVE RENDERING
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Render progressive (quick → draft → full) giống Photoshop.
        /// </summary>
        public static async Task RenderProgressiveAsync(
            Func<double, Task<BitmapSource>> renderFunc,
            Action<BitmapSource> updateUI,
            CancellationToken cancellationToken = default)
        {
            // Stage 1: Quick preview (1/4 resolution)
            var quick = await renderFunc(0.25);
            if (!cancellationToken.IsCancellationRequested)
                updateUI(quick);

            await Task.Delay(50, cancellationToken);

            // Stage 2: Draft preview (1/2 resolution)
            var draft = await renderFunc(0.5);
            if (!cancellationToken.IsCancellationRequested)
                updateUI(draft);

            await Task.Delay(300, cancellationToken);

            // Stage 3: Full quality
            var full = await renderFunc(1.0);
            if (!cancellationToken.IsCancellationRequested)
                updateUI(full);
        }

        // ═══════════════════════════════════════════════════════
        // 7. MEMORY MANAGEMENT
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Dọn dẹp memory sau khi xử lý ảnh nặng.
        /// </summary>
        public static void CleanupMemory()
        {
            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced);
        }

        /// <summary>
        /// Kiểm tra xem có nên dùng tile processing không (dựa vào kích thước ảnh).
        /// </summary>
        public static bool ShouldUseTileProcessing(int width, int height)
        {
            return width >= LARGE_IMAGE_THRESHOLD || height >= LARGE_IMAGE_THRESHOLD;
        }
        
        /// <summary>
        /// Overload cho MagickImage.
        /// </summary>
        public static bool ShouldUseTileProcessing(MagickImage image)
        {
            return image != null && ((int)image.Width >= LARGE_IMAGE_THRESHOLD || (int)image.Height >= LARGE_IMAGE_THRESHOLD);
        }

        // ═══════════════════════════════════════════════════════
        // 8. BITMAP LOADING OPTIMIZATION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Load ảnh từ file với tối ưu hóa (giống Photoshop fast load).
        /// </summary>
        public static BitmapSource LoadOptimized(string path, bool decodeToPreview = false)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Load vào memory ngay
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; // Bỏ qua color profile để nhanh hơn
            
            if (decodeToPreview)
            {
                // Decode thẳng về kích thước nhỏ (nhanh hơn nhiều cho ảnh lớn)
                bitmap.DecodePixelWidth = MAX_PREVIEW_SIZE;
            }
            
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze(); // Immutable = faster + thread-safe
            
            return bitmap;
        }

        /// <summary>
        /// Load ảnh từ stream với tối ưu hóa.
        /// </summary>
        public static BitmapSource LoadOptimizedFromStream(System.IO.Stream stream, bool decodeToPreview = false)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            
            if (decodeToPreview)
            {
                bitmap.DecodePixelWidth = MAX_PREVIEW_SIZE;
            }
            
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }

        // ═══════════════════════════════════════════════════════
        // 9. GPU ACCELERATION HELPERS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Áp dụng filter pixel-by-pixel với Parallel processing (GPU-like speed).
        /// </summary>
        public static unsafe void ApplyFilterFast(WriteableBitmap bitmap, Func<Color, Color> filter)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

            bitmap.Lock();

            try
            {
                IntPtr buffer = bitmap.BackBuffer;
                int stride = bitmap.BackBufferStride;
                int height = bitmap.PixelHeight;
                int width = bitmap.PixelWidth;

                Parallel.For(0, height, y =>
                {
                    byte* row = (byte*)buffer + (y * stride);
                    for (int x = 0; x < width; x++)
                    {
                        int offset = x * 4;
                        Color c = Color.FromArgb(
                            row[offset + 3], // A
                            row[offset + 2], // R
                            row[offset + 1], // G
                            row[offset]);    // B

                        Color result = filter(c);

                        row[offset] = result.B;
                        row[offset + 1] = result.G;
                        row[offset + 2] = result.R;
                        row[offset + 3] = result.A;
                    }
                });

                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                bitmap.Unlock();
            }
        }

        // ═══════════════════════════════════════════════════════
        // 10. SMART DUPLICATE (Copy-on-Write optimization)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Clone BitmapSource một cách tối ưu (shallow copy khi có thể).
        /// </summary>
        public static BitmapSource SmartClone(BitmapSource source)
        {
            if (source == null) return null;
            
            // Nếu source đã frozen, return trực tiếp (immutable = an toàn)
            if (source.IsFrozen)
                return source;

            // Clone và freeze
            var cloned = source.Clone();
            cloned.Freeze();
            return cloned;
        }
    }
}
