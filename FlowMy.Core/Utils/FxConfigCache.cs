using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FlowMy.Utils
{
    /// <summary>
    /// Cache cấu hình FX params đã dùng gần nhất cho mỗi effect.
    /// Sử dụng binary JSON (UTF-8) cho tốc độ save/load nhanh.
    /// Thread-safe qua ConcurrentDictionary + lazy init.
    /// </summary>
    public static class FxConfigCache
    {
        private static readonly ConcurrentDictionary<string, Dictionary<string, double>> _cache = new();
        private static string? _filePath;
        private static volatile bool _loaded;

        private static string GetFilePath()
        {
            if (_filePath != null) return _filePath;

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowMy", "Cache");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "fx_params.json");
            return _filePath;
        }

        /// <summary>Đảm bảo cache đã load từ file (lazy, thread-safe).</summary>
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            LoadFromFile();
        }

        /// <summary>Lấy params đã lưu cho effect. Trả về null nếu chưa có.</summary>
        public static Dictionary<string, double>? Get(string effectName)
        {
            EnsureLoaded();
            if (_cache.TryGetValue(effectName, out var cached))
                return new Dictionary<string, double>(cached);
            return null;
        }

        /// <summary>Lưu params cho effect vào cache (in-memory).</summary>
        public static void Set(string effectName, Dictionary<string, double> parameters)
        {
            _cache[effectName] = new Dictionary<string, double>(parameters);
        }

        /// <summary>Persist cache ra file (gọi khi nhấn Save).</summary>
        public static void SaveToFile()
        {
            try
            {
                var path = GetFilePath();
                var data = new Dictionary<string, Dictionary<string, double>>(_cache);
                var json = JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                File.WriteAllBytes(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FxConfigCache] Save failed: {ex.Message}");
            }
        }

        /// <summary>Load cache từ file. Thread-safe, chỉ load 1 lần.</summary>
        public static void LoadFromFile()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                var path = GetFilePath();
                if (!File.Exists(path)) return;

                var bytes = File.ReadAllBytes(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(bytes);
                if (data == null) return;

                foreach (var kv in data)
                    _cache[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FxConfigCache] Load failed: {ex.Message}");
            }
        }
    }
}
