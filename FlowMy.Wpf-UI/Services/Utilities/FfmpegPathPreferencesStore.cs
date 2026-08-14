using System;

namespace FlowMy.Services.Utilities
{
    public sealed class FfmpegPathPreferences
    {
        public string FfmpegPath { get; set; } = string.Empty;
    }

    public static class FfmpegPathPreferencesStore
    {
        public static FfmpegPathPreferences Load()
        {
            var envPrefs = EnvironmentPathPreferencesStore.Load();
            return new FfmpegPathPreferences { FfmpegPath = envPrefs.FfmpegPath };
        }

        public static void Save(FfmpegPathPreferences preferences)
        {
            var envPrefs = EnvironmentPathPreferencesStore.Load();
            envPrefs.FfmpegPath = preferences?.FfmpegPath ?? string.Empty;
            EnvironmentPathPreferencesStore.Save(envPrefs);
        }

        public static string NormalizeUserInput(string? rawPath)
        {
            return EnvironmentPathPreferencesStore.NormalizeUserInput(rawPath, "ffmpeg.exe");
        }

        public static string ResolveBinaryPath(string binaryName)
        {
            return EnvironmentPathPreferencesStore.ResolveBinaryPath(binaryName);
        }
    }
}
