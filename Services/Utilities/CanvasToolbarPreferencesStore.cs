using System;
using System.IO;
using System.Text.Json;

namespace FlowMy.Services.Utilities
{
    public sealed class CanvasToolbarPreferences
    {
        public string GridType { get; set; } = "Dots";
        public string CanvasDisplayMode { get; set; } = "ShowAll";
        public string CullingPerformanceProfile { get; set; } = "Normal";
        public string ConnectionLineStyle { get; set; } = "Bezier";
        public string ConnectionAnimationMode { get; set; } = "Animated";
        public string ConnectionColorMode { get; set; } = "NodeColor";
        public string CustomConnectionColorKey { get; set; } = "LimeGreen";
        public bool GpuEnabled { get; set; } = true;
        public string GpuRenderQuality { get; set; } = "Medium";
        public string BulkTitleColorMode { get; set; } = "NodeColor";
        public string? BulkTitleColorKey { get; set; }
        public string EnergyColorMode { get; set; } = "FollowLineColor";
        public string CustomEnergyColorKey { get; set; } = "Gold";
        public double EnergyDotGap { get; set; } = 8.0;
        public double EnergyDotThicknessExtra { get; set; } = 0.8;
        public string EnergyDotText { get; set; } = string.Empty;
        public bool EnergyDotTextRotate { get; set; } = true;
        public double EnergyRunSpeed { get; set; } = 1.0;
        public double EnergyTextSpinSeconds { get; set; } = 0.7;
        public bool EnergyMeteorMode { get; set; } = false;
        public int ApplyDebounceMs { get; set; } = 70;
    }

    public static class CanvasToolbarPreferencesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private static string GetSettingsFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "FlowMy");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "canvas-toolbar-preferences.json");
        }

        public static CanvasToolbarPreferences Load()
        {
            try
            {
                var file = GetSettingsFilePath();
                if (!File.Exists(file))
                {
                    return new CanvasToolbarPreferences();
                }

                var json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<CanvasToolbarPreferences>(json, JsonOptions) ?? new CanvasToolbarPreferences();
            }
            catch
            {
                return new CanvasToolbarPreferences();
            }
        }

        public static void Save(CanvasToolbarPreferences preferences)
        {
            try
            {
                var file = GetSettingsFilePath();
                var json = JsonSerializer.Serialize(preferences, JsonOptions);
                File.WriteAllText(file, json);
            }
            catch
            {
                // Best effort only. Never block UI for settings persistence failure.
            }
        }
    }
}
