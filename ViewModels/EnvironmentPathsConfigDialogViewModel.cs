using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Services.Utilities;
using System;
using System.IO;

namespace FlowMy.ViewModels
{
    public partial class EnvironmentPathsConfigDialogViewModel : ObservableObject
    {
        [ObservableProperty] private string _ffmpegPath = string.Empty;
        [ObservableProperty] private string _ffprobePath = string.Empty;
        [ObservableProperty] private string _gitPath = string.Empty;
        [ObservableProperty] private string _pythonPath = string.Empty;
        [ObservableProperty] private string _customBinariesPath = string.Empty;

        // Status Badges
        [ObservableProperty] private string _ffmpegStatusText = "Chưa kiểm tra";
        [ObservableProperty] private bool _isFfmpegValid;

        [ObservableProperty] private string _gitStatusText = "Chưa kiểm tra";
        [ObservableProperty] private bool _isGitValid;

        [ObservableProperty] private string _pythonStatusText = "Chưa kiểm tra";
        [ObservableProperty] private bool _isPythonValid;

        public EnvironmentPathsConfigDialogViewModel()
        {
            LoadPreferences();
        }

        public void LoadPreferences()
        {
            var prefs = EnvironmentPathPreferencesStore.Load();
            FfmpegPath = prefs.FfmpegPath;
            FfprobePath = prefs.FfprobePath;
            GitPath = prefs.GitPath;
            PythonPath = prefs.PythonPath;
            CustomBinariesPath = prefs.CustomBinariesPath;

            ValidateAll();
        }

        public void SavePreferences()
        {
            var prefs = new EnvironmentPathPreferences
            {
                FfmpegPath = EnvironmentPathPreferencesStore.NormalizeUserInput(FfmpegPath, "ffmpeg.exe"),
                FfprobePath = EnvironmentPathPreferencesStore.NormalizeUserInput(FfprobePath, "ffprobe.exe"),
                GitPath = EnvironmentPathPreferencesStore.NormalizeUserInput(GitPath, "git.exe"),
                PythonPath = EnvironmentPathPreferencesStore.NormalizeUserInput(PythonPath, "python.exe"),
                CustomBinariesPath = CustomBinariesPath?.Trim() ?? string.Empty
            };

            EnvironmentPathPreferencesStore.Save(prefs);
            FfmpegPath = prefs.FfmpegPath;
            FfprobePath = prefs.FfprobePath;
            GitPath = prefs.GitPath;
            PythonPath = prefs.PythonPath;

            ValidateAll();
        }

        [RelayCommand]
        private void ValidateAll()
        {
            // Validate FFmpeg
            var resolvedFfmpeg = EnvironmentPathPreferencesStore.ResolveBinaryPath("ffmpeg");
            if (File.Exists(resolvedFfmpeg))
            {
                IsFfmpegValid = true;
                FfmpegStatusText = $"✓ Đã tìm thấy: {Path.GetFileName(resolvedFfmpeg)} ({resolvedFfmpeg})";
            }
            else
            {
                IsFfmpegValid = false;
                FfmpegStatusText = "❌ Chưa tìm thấy ffmpeg.exe trong đường dẫn đã cấu hình";
            }

            // Validate Git
            var resolvedGit = EnvironmentPathPreferencesStore.ResolveBinaryPath("git");
            if (File.Exists(resolvedGit))
            {
                IsGitValid = true;
                GitStatusText = $"✓ Đã tìm thấy: git.exe ({resolvedGit})";
            }
            else
            {
                IsGitValid = false;
                GitStatusText = "❌ Chưa tìm thấy git.exe";
            }

            // Validate Python
            var resolvedPython = EnvironmentPathPreferencesStore.ResolveBinaryPath("python");
            if (File.Exists(resolvedPython))
            {
                IsPythonValid = true;
                PythonStatusText = $"✓ Đã tìm thấy: python.exe ({resolvedPython})";
            }
            else
            {
                IsPythonValid = false;
                PythonStatusText = "⚠️ Chưa cấu hình hoặc chưa tìm thấy python.exe";
            }
        }

        [RelayCommand]
        private void AutoDetectAll()
        {
            AutoDetectFfmpeg();
            AutoDetectGit();
            AutoDetectPython();
            SavePreferences();
        }

        [RelayCommand]
        private void AutoDetectFfmpeg()
        {
            var detected = EnvironmentPathPreferencesStore.AutoDetectFfmpeg();
            if (!string.IsNullOrWhiteSpace(detected))
            {
                FfmpegPath = detected;
            }
            ValidateAll();
        }

        [RelayCommand]
        private void AutoDetectGit()
        {
            var detected = EnvironmentPathPreferencesStore.AutoDetectGit();
            if (!string.IsNullOrWhiteSpace(detected))
            {
                GitPath = detected;
            }
            ValidateAll();
        }

        [RelayCommand]
        private void AutoDetectPython()
        {
            var detected = EnvironmentPathPreferencesStore.AutoDetectPython();
            if (!string.IsNullOrWhiteSpace(detected))
            {
                PythonPath = detected;
            }
            ValidateAll();
        }
    }
}
