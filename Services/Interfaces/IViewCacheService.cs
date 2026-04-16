using System;
using System.Threading.Tasks;
using System.Windows;

namespace FlowMy.Services.Interfaces
{
    // Interface cho View Cache Service - Thread-Safe và WPF-Aware
    public interface IViewCacheService
    {
        /// <summary>
        /// Lấy hoặc tạo item async với thread safety
        /// </summary>
        Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory) where T : class;

        /// <summary>
        /// Lấy hoặc tạo item sync với thread safety
        /// </summary>
        T? GetOrCreate<T>(string key, Func<T?> factory) where T : class;

        /// <summary>
        /// Lấy hoặc tạo WPF UserControl với UI thread safety
        /// </summary>
        Task<T?> GetOrCreateUIAsync<T>(string key, Func<T?> uiFactory) where T : class;

        /// <summary>
        /// Kiểm tra xem key có tồn tại trong cache không
        /// </summary>
        bool Contains(string key);

        /// <summary>
        /// Lấy item từ cache (không tạo mới)
        /// </summary>
        T? Get<T>(string key) where T : class;

        /// <summary>
        /// Xóa item khỏi cache
        /// </summary>
        void Remove(string key);

        /// <summary>
        /// Xóa tất cả items khỏi cache
        /// </summary>
        void Clear();

        /// <summary>
        /// Đặt kích thước tối đa cho cache
        /// </summary>
        void SetMaxCacheSize(int maxSize);

        /// <summary>
        /// Lấy thống kê cache
        /// </summary>
        CacheStatistics GetStatistics();

        /// <summary>
        /// Dọn dẹp memory (remove unused items)
        /// </summary>
        void OptimizeMemory();
    }

    /// <summary>
    /// Thống kê cache
    /// </summary>
    public class CacheStatistics
    {
        public int TotalItems { get; set; }
        public int MaxSize { get; set; }
        public double HitRate { get; set; }
        public long TotalHits { get; set; }
        public long TotalMisses { get; set; }
        public DateTime LastOptimized { get; set; }
    }
}