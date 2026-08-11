using System;
using System.IO;
using System.Text.Json;

namespace FlowMy.Services.Utilities
{
    public sealed class EnvironmentPathPreferences
    {
        public string FfmpegPath { get; set; } = string.Empty;
        public string FfprobePath { get; set; } = string.Empty;
        public string GitPath { get; set; } = string.Empty;
        public string PythonPath { get; set; } = string.Empty;
        public string CustomBinariesPath { get; set; } = string.Empty;
    }

    public static class EnvironmentPathPreferencesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static string GetFilePath()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(baseDir, "FlowMy", "environment_paths.json");
        }

        private static string GetLegacyFfmpegFilePath()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(baseDir, "FlowMy", "ffmpeg.preferences.json");
        }

        public static EnvironmentPathPreferences Load()
        {
            try
            {
                var file = GetFilePath();
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var prefs = JsonSerializer.Deserialize<EnvironmentPathPreferences>(json, JsonOptions);
                    if (prefs != null) return prefs;
                }

                // Check legacy ffmpeg.preferences.json for backward compatibility
                var legacyFile = GetLegacyFfmpegFilePath();
                if (File.Exists(legacyFile))
                {
                    var legacyJson = File.ReadAllText(legacyFile);
                    using var doc = JsonDocument.Parse(legacyJson);
                    if (doc.RootElement.TryGetProperty("FfmpegPath", out var prop))
                    {
                        var legacyPath = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(legacyPath))
                        {
                            var prefs = new EnvironmentPathPreferences { FfmpegPath = legacyPath };
                            Save(prefs);
                            return prefs;
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return new EnvironmentPathPreferences();
        }

        public static void Save(EnvironmentPathPreferences preferences)
        {
            try
            {
                var file = GetFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                var payload = preferences ?? new EnvironmentPathPreferences();
                File.WriteAllText(file, JsonSerializer.Serialize(payload, JsonOptions));

                // Also update legacy file so old code remains compatible
                try
                {
                    var legacyFile = GetLegacyFfmpegFilePath();
                    var legacyPayload = new { FfmpegPath = payload.FfmpegPath };
                    File.WriteAllText(legacyFile, JsonSerializer.Serialize(legacyPayload, JsonOptions));
                }
                catch { }
            }
            catch
            {
                // Best-effort
            }
        }

        public static string NormalizeUserInput(string? rawPath, string defaultExeName)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;
            var input = rawPath.Trim().Trim('"');

            try
            {
                if (Directory.Exists(input))
                {
                    var exePath = Path.Combine(input, defaultExeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? defaultExeName : $"{defaultExeName}.exe");
                    return File.Exists(exePath) ? exePath : input;
                }

                if (File.Exists(input))
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
            var cleanName = binaryName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

            try
            {
                var prefs = Load();

                // 1. Check user-configured path in Store
                if (cleanName.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(prefs.FfmpegPath))
                {
                    var norm = NormalizeUserInput(prefs.FfmpegPath, "ffmpeg.exe");
                    if (File.Exists(norm)) return norm;
                    if (Directory.Exists(norm))
                    {
                        var candidate = Path.Combine(norm, "ffmpeg.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
                else if (cleanName.Equals("ffprobe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(prefs.FfprobePath))
                    {
                        var norm = NormalizeUserInput(prefs.FfprobePath, "ffprobe.exe");
                        if (File.Exists(norm)) return norm;
                    }
                    if (!string.IsNullOrWhiteSpace(prefs.FfmpegPath))
                    {
                        var folder = Directory.Exists(prefs.FfmpegPath) ? prefs.FfmpegPath : Path.GetDirectoryName(prefs.FfmpegPath);
                        if (!string.IsNullOrWhiteSpace(folder))
                        {
                            var sibling = Path.Combine(folder, "ffprobe.exe");
                            if (File.Exists(sibling)) return sibling;
                        }
                    }
                }
                else if (cleanName.Equals("git", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(prefs.GitPath))
                {
                    var norm = NormalizeUserInput(prefs.GitPath, "git.exe");
                    if (File.Exists(norm)) return norm;
                }
                else if (cleanName.Equals("python", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(prefs.PythonPath))
                {
                    var norm = NormalizeUserInput(prefs.PythonPath, "python.exe");
                    if (File.Exists(norm)) return norm;
                }

                // 2. Check CustomBinariesPath
                if (!string.IsNullOrWhiteSpace(prefs.CustomBinariesPath) && Directory.Exists(prefs.CustomBinariesPath))
                {
                    var customExe = Path.Combine(prefs.CustomBinariesPath, $"{cleanName}.exe");
                    if (File.Exists(customExe)) return customExe;
                }

                // 3. Check App BaseDirectory local folders
                var localFfmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ffmpeg", $"{cleanName}.exe");
                if (File.Exists(localFfmpeg)) return localFfmpeg;

                var localRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{cleanName}.exe");
                if (File.Exists(localRoot)) return localRoot;
            }
            catch
            {
                // Fallback below
            }

            return binaryName;
        }

        // --- AUTO-DETECTION HELPERS ---
        public static string AutoDetectFfmpeg()
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ffmpeg", "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\ffmpeg\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", "ffmpeg.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            // Check PATH environment
            return FindInSystemPath("ffmpeg.exe");
        }

        public static string AutoDetectGit()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "cmd", "git.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            return FindInSystemPath("git.exe");
        }

        public static string AutoDetectPython()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python311", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python312", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python310", "python.exe"),
                @"C:\Python311\python.exe",
                @"C:\Python312\python.exe",
                @"C:\Python310\python.exe"
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            return FindInSystemPath("python.exe");
        }

        private static string FindInSystemPath(string exeName)
        {
            try
            {
                var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var dir in paths)
                {
                    var full = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(full)) return full;
                }
            }
            catch { }
            return string.Empty;
        }
    }
}
