using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlowMy.ViewModels;

namespace FlowMy.Services.Utils
{
    /// <summary>
    /// Service quản lý custom presets do user tự thêm.
    /// Lưu vào %AppData%\FlowMy\custom_presets.json.
    /// </summary>
    public static class CustomPresetService
    {
        private static readonly string _filePath;

        static CustomPresetService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "FlowMy");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "custom_presets.json");
        }

        public static string FilePath => _filePath;

        // ── DTO để serialize/deserialize ──────────────────────────
        private record PresetDto(string Title, string Description, string Url, string FileName, string Type);

        // ── Load ──────────────────────────────────────────────────
        public static List<AssetPreset> Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return new();
                var json = File.ReadAllText(_filePath);
                var dtos = JsonSerializer.Deserialize<List<PresetDto>>(json);
                return dtos?.Select(d => new AssetPreset(d.Title, d.Description, d.Url, d.FileName, d.Type))
                           .ToList() ?? new();
            }
            catch
            {
                return new();
            }
        }

        // ── Save ──────────────────────────────────────────────────
        public static void Save(IEnumerable<AssetPreset> presets)
        {
            try
            {
                var dtos = presets.Select(p => new PresetDto(p.Title, p.Description, p.Url, p.FileName, p.Type))
                                  .ToList();
                var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { /* Không throw, lỗi lưu không nên crash app */ }
        }

        // ── Helpers ───────────────────────────────────────────────
        public static void AddPreset(AssetPreset preset)
        {
            var list = Load();
            list.Add(preset);
            Save(list);
        }

        public static void RemovePreset(AssetPreset preset)
        {
            var list = Load();
            list.RemoveAll(p => string.Equals(p.FileName, preset.FileName, StringComparison.OrdinalIgnoreCase)
                             && string.Equals(p.Type, preset.Type, StringComparison.OrdinalIgnoreCase));
            Save(list);
        }

        public static void UpdatePreset(AssetPreset original, AssetPreset updated)
        {
            var list = Load();
            var idx = list.FindIndex(p =>
                string.Equals(p.FileName, original.FileName, StringComparison.OrdinalIgnoreCase)
             && string.Equals(p.Type, original.Type, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) list[idx] = updated;
            else list.Add(updated);
            Save(list);
        }
    }
}
