using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FlowMy.Services.Interfaces;

namespace FlowMy.Services
{
    /// <summary>
    /// Thread-safe và WPF-aware View Cache Service
    /// Giải quyết vấn đề "Must create DependencySource on same Thread as the DependencyObject"
    /// </summary>
    public class ViewCacheService : IViewCacheService, IDisposable
    {
        #region Fields
        private readonly ConcurrentDictionary<string, object> _cache;
        private readonly ConcurrentDictionary<string, CacheMetadata> _metadata;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores;
        private int _maxCacheSize;
        private long _totalHits;
        private long _totalMisses;
        private DateTime _lastOptimized;
        private bool _disposed;
        #endregion

        #region Constructor
        public ViewCacheService(int maxCacheSize = 20)
        {
            _cache = new ConcurrentDictionary<string, object>();
            _metadata = new ConcurrentDictionary<string, CacheMetadata>();
            _semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
            _maxCacheSize = Math.Max(1, maxCacheSize);
            _lastOptimized = DateTime.Now;
        }
        #endregion

        #region Public Methods
        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory) where T : class
        {
            if (_disposed) return null;

            // Kiểm tra cache trước
            if (TryGetFromCache<T>(key, out var cachedValue))
            {
                Interlocked.Increment(ref _totalHits);
                return cachedValue;
            }

            Interlocked.Increment(ref _totalMisses);

            // Sử dụng semaphore để tránh tạo multiple instances của cùng key
            var semaphore = _semaphores.GetOrAdd(key, k => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
            try
            {
                // Double-check sau khi acquire lock
                if (TryGetFromCache<T>(key, out var stillCached))
                {
                    return stillCached;
                }

                // Tạo item mới
                var newItem = await factory();
                if (newItem != null)
                {
                    await AddToCacheAsync(key, newItem);
                }

                return newItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetOrCreateAsync: {ex.Message}");
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public T? GetOrCreate<T>(string key, Func<T?> factory) where T : class
        {
            if (_disposed) return null;

            // Kiểm tra cache trước
            if (TryGetFromCache<T>(key, out var cachedValue))
            {
                Interlocked.Increment(ref _totalHits);
                return cachedValue;
            }

            Interlocked.Increment(ref _totalMisses);

            var semaphore = _semaphores.GetOrAdd(key, k => new SemaphoreSlim(1, 1));

            semaphore.Wait();
            try
            {
                // Double-check
                if (TryGetFromCache<T>(key, out var stillCached))
                {
                    return stillCached;
                }

                var newItem = factory();
                if (newItem != null)
                {
                    // Sync version - không async
                    AddToCache(key, newItem);
                }

                return newItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetOrCreate: {ex.Message}");
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<T?> GetOrCreateUIAsync<T>(string key, Func<T?> uiFactory) where T : class
        {
            if (_disposed) return null;

            // Kiểm tra cache trước
            if (TryGetFromCache<T>(key, out var cachedValue))
            {
                Interlocked.Increment(ref _totalHits);
                return cachedValue;
            }

            Interlocked.Increment(ref _totalMisses);

            var semaphore = _semaphores.GetOrAdd(key, k => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
            try
            {
                // Double-check
                if (TryGetFromCache<T>(key, out var stillCached))
                {
                    return stillCached;
                }

                // QUAN TRỌNG: Tạo UI elements trên UI thread
                T? newItem = null;
                if (Application.Current?.Dispatcher != null)
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        // Đã ở UI thread
                        newItem = uiFactory();
                    }
                    else
                    {
                        // Invoke lên UI thread
                        newItem = await Application.Current.Dispatcher.InvokeAsync(() => uiFactory());
                    }
                }
                else
                {
                    // Fallback nếu không có Application.Current
                    newItem = uiFactory();
                }

                if (newItem != null)
                {
                    await AddToCacheAsync(key, newItem);
                }

                return newItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetOrCreateUIAsync: {ex.Message}");
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public bool Contains(string key)
        {
            return !_disposed && _cache.ContainsKey(key);
        }

        public T? Get<T>(string key) where T : class
        {
            if (_disposed) return null;

            TryGetFromCache<T>(key, out var value);
            return value;
        }

        public void Remove(string key)
        {
            if (_disposed) return;

            if (_cache.TryRemove(key, out var item))
            {
                _metadata.TryRemove(key, out _);

                // Dispose item nếu cần thiết
                DisposeItem(item);

                // Cleanup semaphore
                if (_semaphores.TryRemove(key, out var semaphore))
                {
                    semaphore.Dispose();
                }
            }
        }

        public void Clear()
        {
            if (_disposed) return;

            var keys = _cache.Keys.ToList();
            foreach (var key in keys)
            {
                Remove(key);
            }

            // Cleanup remaining semaphores
            foreach (var semaphore in _semaphores.Values)
            {
                semaphore.Dispose();
            }
            _semaphores.Clear();
        }

        public void SetMaxCacheSize(int maxSize)
        {
            _maxCacheSize = Math.Max(1, maxSize);

            // Remove excess items nếu cache vượt quá limit
            Task.Run(() => OptimizeMemory());
        }

        public CacheStatistics GetStatistics()
        {
            var totalRequests = _totalHits + _totalMisses;
            return new CacheStatistics
            {
                TotalItems = _cache.Count,
                MaxSize = _maxCacheSize,
                HitRate = totalRequests > 0 ? (double)_totalHits / totalRequests * 100 : 0,
                TotalHits = _totalHits,
                TotalMisses = _totalMisses,
                LastOptimized = _lastOptimized
            };
        }

        public void OptimizeMemory()
        {
            if (_disposed) return;

            try
            {
                // Remove items vượt quá maxCacheSize
                while (_cache.Count > _maxCacheSize)
                {
                    RemoveOldestItem();
                }

                // Remove expired items (nếu có logic expiration)
                var expiredKeys = _metadata
                    .Where(kvp => IsExpired(kvp.Value))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    Remove(key);
                }

                _lastOptimized = DateTime.Now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OptimizeMemory: {ex.Message}");
            }
        }
        #endregion

        #region Private Methods
        private bool TryGetFromCache<T>(string key, out T? value) where T : class
        {
            if (_cache.TryGetValue(key, out var cached) && cached is T typedValue)
            {
                // Update last accessed time
                if (_metadata.TryGetValue(key, out var metadata))
                {
                    metadata.LastAccessed = DateTime.Now;
                    metadata.HitCount++;
                }

                value = typedValue;
                return true;
            }

            value = null;
            return false;
        }

        private async Task AddToCacheAsync<T>(string key, T item) where T : class
        {
            // Kiểm tra cache size limit
            if (_cache.Count >= _maxCacheSize)
            {
                await Task.Run(() => RemoveOldestItem());
            }

            _cache[key] = item;
            _metadata[key] = new CacheMetadata
            {
                CreatedAt = DateTime.Now,
                LastAccessed = DateTime.Now,
                HitCount = 0,
                ItemType = typeof(T)
            };
        }

        private void AddToCache<T>(string key, T item) where T : class
        {
            // Sync version
            if (_cache.Count >= _maxCacheSize)
            {
                RemoveOldestItem();
            }

            _cache[key] = item;
            _metadata[key] = new CacheMetadata
            {
                CreatedAt = DateTime.Now,
                LastAccessed = DateTime.Now,
                HitCount = 0,
                ItemType = typeof(T)
            };
        }

        private void RemoveOldestItem()
        {
            var oldest = _metadata
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .ThenBy(kvp => kvp.Value.HitCount)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(oldest.Key))
            {
                Remove(oldest.Key);
            }
        }

        private void DisposeItem(object item)
        {
            try
            {
                if (item is IDisposable disposable)
                {
                    // Nếu là WPF DependencyObject, dispose trên UI thread
                    if (item is DependencyObject)
                    {
                        if (Application.Current?.Dispatcher != null)
                        {
                            if (Application.Current.Dispatcher.CheckAccess())
                            {
                                disposable.Dispose();
                            }
                            else
                            {
                                Application.Current.Dispatcher.BeginInvoke(() => disposable.Dispose());
                            }
                        }
                        else
                        {
                            disposable.Dispose();
                        }
                    }
                    else
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing item: {ex.Message}");
            }
        }

        private bool IsExpired(CacheMetadata metadata)
        {
            // Implement expiration logic nếu cần
            // Ví dụ: items không được access trong 1 giờ sẽ bị expired
            var expireAfter = TimeSpan.FromHours(1);
            return DateTime.Now - metadata.LastAccessed > expireAfter;
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            Clear();

            GC.SuppressFinalize(this);
        }
        #endregion

        #region Helper Classes
        private class CacheMetadata
        {
            public DateTime CreatedAt { get; set; }
            public DateTime LastAccessed { get; set; }
            public long HitCount { get; set; }
            public Type ItemType { get; set; } = typeof(object);
        }
        #endregion
    }
}