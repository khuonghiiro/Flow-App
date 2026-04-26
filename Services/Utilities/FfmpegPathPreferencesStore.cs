using System;
using System.IO;
using System.Text.Json;

namespace FlowMy.Services.Utilities
{
    public sealed class FfmpegPathPreferences
    {
        public string FfmpegPath { get; set; } = string.Empty;
    }

    public static class FfmpegPathPreferencesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static string GetFilePath()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(baseDir, "FlowMy", "ffmpeg.preferences.json");
        }

        public static FfmpegPathPreferences Load()
        {
            try
            {
                var file = GetFilePath();
                if (!File.Exists(file)) return new FfmpegPathPreferences();
                var json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<FfmpegPathPreferences>(json, JsonOptions)
                       ?? new FfmpegPathPreferences();
            }
            catch
            {
                return new FfmpegPathPreferences();
            }
        }

        public static void Save(FfmpegPathPreferences preferences)
        {
            try
            {
                var file = GetFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                var payload = preferences ?? new FfmpegPathPreferences();
                File.WriteAllText(file, JsonSerializer.Serialize(payload, JsonOptions));
            }
            catch
            {
                // best-effort
            }
        }

        public static string NormalizeUserInput(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;
            var input = rawPath.Trim().Trim('"');

            try
            {
                if (Directory.Exists(input))
                {
                    var ffmpegExe = Path.Combine(input, "ffmpeg.exe");
                    return File.Exists(ffmpegExe) ? ffmpegExe : string.Empty;
                }

                if (File.Exists(input) &&
                    string.Equals(Path.GetFileName(input), "ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return input;
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        public static string ResolveBinaryPath(string binaryName)
        {
            if (string.IsNullOrWhiteSpace(binaryName)) return binaryName;

            try
            {
                var prefs = Load();
                var configuredFfmpeg = NormalizeUserInput(prefs.FfmpegPath);
                if (!string.IsNullOrWhiteSpace(configuredFfmpeg))
                {
                    if (string.Equals(binaryName, "ffmpeg", StringComparison.OrdinalIgnoreCase))
                        return configuredFfmpeg;

                    var sibling = Path.Combine(Path.GetDirectoryName(configuredFfmpeg) ?? string.Empty, $"{binaryName}.exe");
                    if (File.Exists(sibling)) return sibling;
                }

                var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ffmpeg", $"{binaryName}.exe");
                if (File.Exists(local)) return local;
            }
            catch
            {
                // fallback below
            }

            return binaryName;
        }
    }
}
