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
        public bool CacheNodeEnabled { get; set; } = false;
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
        public bool AdvancedFlowStyleEnabled { get; set; } = false;
        
        // Glow Pulse effect configuration
        public double MeteorTailLength { get; set; } = 15.0;
        public double MeteorTailBlur { get; set; } = 10.0;
        public double MeteorTailOpacity { get; set; } = 1.5;
        public double MeteorTailThickness { get; set; } = 4.0;
        
        public int ApplyDebounceMs { get; set; } = 70;

        // Node execution spinner style
        public bool NodeSpinnerArcMode { get; set; } = true;
        public bool NodeSpinnerMultiColor { get; set; } = false;
        public double NodeSpinnerSize { get; set; } = 26.0;
        public bool NodeSpinnerScaleWithNode { get; set; } = false;
        public double NodeSpinnerSizeRatio { get; set; } = 0.32;
        public string NodeSpinnerShape { get; set; } = "Circle"; // Circle|Diamond|Square|RoundedSquare|FollowNodeShape
        public string NodeSpinnerPosition { get; set; } = "TopRight"; // TopRight|TopLeft|BottomRight|BottomLeft|Center
        public double NodeSpinnerStrokeThickness { get; set; } = 3.2;
        public double NodeSpinnerSpinSeconds { get; set; } = 1.1;
        public bool NodeSpinnerBlinkBackground { get; set; } = false;
        public string NodeSpinnerBlinkBackgroundColorKey { get; set; } = "WarningBrush";
        public string NodeSpinnerBlinkMode { get; set; } = "Soft"; // Soft|Hard
        public double NodeSpinnerBlinkIntensity { get; set; } = 0.65; // 0..1
        public double NodeSpinnerBlinkBaseOpacity { get; set; } = 0.16; // 0..1
        public double NodeSpinnerBlinkPeakOpacity { get; set; } = 0.60; // 0..1

        // UI Theme animations
        /// <summary>
        /// Chế độ giao diện node: "Solid" (màu đặc mặc định) hoặc "LiquidGlass" (kính lỏng trong suốt).
        /// </summary>
        public string NodeAppearanceMode { get; set; } = "Solid";

        /// <summary>
        /// Bật/tắt hiệu ứng animation trên các control giao diện (hover, checked transitions, etc.)
        /// Tắt để tăng hiệu năng trên máy yếu.
        /// </summary>
        public bool UiAnimationsEnabled { get; set; } = true;

        /// <summary>
        /// Bật để ép đồng bộ trạng thái UI ngay khi runtime kết thúc
        /// và flush dữ liệu cuối cho result badge/widget.
        /// </summary>
        public bool StrictFinalSyncEnabled { get; set; } = true;
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
