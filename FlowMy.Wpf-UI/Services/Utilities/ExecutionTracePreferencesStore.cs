using System;
using System.IO;
using System.Text.Json;

namespace FlowMy.Services.Utilities
{
    /// <summary>
    /// Cấu hình user-level cho Execution Trace panel (bật log run, cấu hình export...),
    /// tồn tại giữa các lần mở app để đỡ bấm lại mỗi lần.
    /// </summary>
    public sealed class ExecutionTracePreferences
    {
        public bool EnableExecutionTraceLog { get; set; } = false;
        public bool IsExecutionTracePanelExpanded { get; set; } = true;
        public double ExecutionTraceCardMaxWidth { get; set; } = 380;

        // DevTools-like docking config cho panel log.
        // DockMode: "Bottom" | "Left" | "Right" | "Detached"
        public string ExecutionTracePanelDockMode { get; set; } = "Bottom";
        public double ExecutionTracePanelDockHeight { get; set; } = 320;
        public double ExecutionTracePanelDockWidth { get; set; } = 480;
        // Detached window vị trí / kích thước (để mở lại đúng chỗ).
        public double ExecutionTracePanelDetachedLeft { get; set; } = double.NaN;
        public double ExecutionTracePanelDetachedTop { get; set; } = double.NaN;
        public double ExecutionTracePanelDetachedWidth { get; set; } = 820;
        public double ExecutionTracePanelDetachedHeight { get; set; } = 520;

        // Kiểu hiển thị UI log item: "Full" (đầy đủ) | "Relative" (tương đối) | "Compact" (thu gọn).
        public string ExecutionTraceDisplayStyle { get; set; } = "Full";

        // Export defaults
        public bool ExportIncludeInput { get; set; } = true;
        public bool ExportIncludeOutput { get; set; } = true;
        public bool ExportIncludeError { get; set; } = true;
        public int ExportMaxFieldLength { get; set; } = 0;
        public bool ExportIncludeTree { get; set; } = true;
        public bool ExportOnlyCurrentFilter { get; set; } = false;
        public bool ExportPrettyPrint { get; set; } = true;
    }

    public static class ExecutionTracePreferencesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static string GetSettingsFilePath(string? profileKey = null)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "FlowMy");
            Directory.CreateDirectory(folder);
            var suffix = string.IsNullOrWhiteSpace(profileKey)
                ? string.Empty
                : $".{profileKey.Trim().ToLowerInvariant()}";
            return Path.Combine(folder, $"execution-trace-preferences{suffix}.json");
        }

        public static ExecutionTracePreferences Load(string? profileKey = null)
        {
            try
            {
                var file = GetSettingsFilePath(profileKey);
                if (!File.Exists(file))
                    return new ExecutionTracePreferences();
                var json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<ExecutionTracePreferences>(json, JsonOptions)
                    ?? new ExecutionTracePreferences();
            }
            catch
            {
                return new ExecutionTracePreferences();
            }
        }

        public static void Save(ExecutionTracePreferences prefs, string? profileKey = null)
        {
            try
            {
                var file = GetSettingsFilePath(profileKey);
                var json = JsonSerializer.Serialize(prefs, JsonOptions);
                File.WriteAllText(file, json);
            }
            catch
            {
                // best-effort; never throw from settings persistence
            }
        }
    }
}
