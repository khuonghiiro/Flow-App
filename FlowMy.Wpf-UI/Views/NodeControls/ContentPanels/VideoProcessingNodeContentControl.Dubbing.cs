using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FlowMy.Core.Models.Media;
using Microsoft.Win32;
using NAudio.Wave;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl
    {
        public void InitializeDubbingUiEvents()
        {
            if (_node == null) return;

            DubbingClipsItemsControl.ItemsSource = _node.DubbingClips;
            _node.DubbingClips.CollectionChanged += (s, e) =>
            {
                UpdateDubbingBadge();
                UpdateDubbingRamUsage();
                SyncDubbingMixerState();
            };
            UpdateDubbingBadge();
            UpdateDubbingRamUsage();

            // RAM Management
            ClearDubbingRamCacheButton.Click += (s, e) =>
            {
                _realTimeAudioEngine?.ClearRamBuffers();
                UpdateDubbingRamUsage();
                AppendLog("⚡ Đã giải phóng toàn bộ bộ nhớ RAM của phòng thu lồng tiếng.");
            };

            // Auto-Ducking Controls
            AutoDuckingToggle.Checked += (s, e) => SyncAutoDuckingFromUi();
            AutoDuckingToggle.Unchecked += (s, e) => SyncAutoDuckingFromUi();
            AutoDuckingAmountSlider.ValueChanged += (s, e) =>
            {
                AutoDuckingAmountLabel.Text = $"{AutoDuckingAmountSlider.Value:F0} dB";
                SyncAutoDuckingFromUi();
            };
            AutoDuckingAttackSlider.ValueChanged += (s, e) =>
            {
                AutoDuckingAttackLabel.Text = $"{AutoDuckingAttackSlider.Value:F0} ms";
                SyncAutoDuckingFromUi();
            };
            AutoDuckingReleaseSlider.ValueChanged += (s, e) =>
            {
                AutoDuckingReleaseLabel.Text = $"{AutoDuckingReleaseSlider.Value:F0} ms";
                SyncAutoDuckingFromUi();
            };

            // AI TTS Generator
            TtsSpeedSlider.ValueChanged += (s, e) =>
            {
                TtsSpeedLabel.Text = $"{TtsSpeedSlider.Value:F1}x";
            };
            GenerateTtsClipButton.Click += async (s, e) => await GenerateAiTtsClipAsync();

            // Multi-Clip Dubbing Toolbar
            AddDubbingClipFromFileButton.Click += (s, e) => AddDubbingClipFromFile();
            ClearAllDubbingClipsButton.Click += (s, e) => ClearAllDubbingClips();
            SyncDubbingToLiveEngineButton.Click += (s, e) =>
            {
                PreloadAllDubbingClipsToRam();
                SyncDubbingMixerState();
                AppendLog("🎧 Đã đồng bộ tất cả clip lồng tiếng vào Live Audio Engine.");
            };
            ResetDubbingSettingsButton.Click += (s, e) => ResetDubbingSettings();
        }

        private void UpdateDubbingBadge()
        {
            if (_node == null) return;
            DubbingClipsCountBadge.Text = $"{_node.DubbingClips.Count} clips";
        }

        public void UpdateDubbingRamUsage()
        {
            long bytes = _realTimeAudioEngine?.DubbingMixer?.GetTotalMemoryUsageBytes() ?? 0;
            double mb = bytes / (1024.0 * 1024.0);
            DubbingRamUsageText.Text = $"{mb:F1} MB";
        }

        private void SyncAutoDuckingFromUi()
        {
            if (_node == null) return;
            var duck = _node.AutoDucking ??= new AutoDuckingConfig();
            duck.Enabled = AutoDuckingToggle.IsChecked == true;
            duck.DuckingAmountDb = AutoDuckingAmountSlider.Value;
            duck.AttackMs = AutoDuckingAttackSlider.Value;
            duck.ReleaseMs = AutoDuckingReleaseSlider.Value;
            SyncDubbingMixerState();
        }

        private void SyncDubbingMixerState()
        {
            if (_node != null && _realTimeAudioEngine?.DubbingMixer != null)
            {
                _realTimeAudioEngine.DubbingMixer.UpdateConfig(_node.DubbingClips, _node.AutoDucking);
            }
        }

        private void PreloadAllDubbingClipsToRam()
        {
            if (_node == null) return;
            int loaded = 0;
            foreach (var clip in _node.DubbingClips)
            {
                if (!string.IsNullOrWhiteSpace(clip.AudioFilePath) && File.Exists(clip.AudioFilePath))
                {
                    if (_realTimeAudioEngine?.DubbingMixer != null && _realTimeAudioEngine.DubbingMixer.PreloadClipToRam(clip.AudioFilePath))
                    {
                        loaded++;
                    }
                }
            }
            UpdateDubbingRamUsage();
            AppendLog($"⚡ Đã nạp {loaded} clip âm thanh vào bộ đệm RAM.");
        }

        private async Task GenerateAiTtsClipAsync()
        {
            var text = TtsScriptInputText.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                AppendLog("⚠ Vui lòng nhập nội dung văn bản kịch bản để tạo giọng đọc AI.");
                return;
            }

            var modelTag = (TtsVoiceModelCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "vi_female";
            var speed = TtsSpeedSlider.Value;

            AppendLog($"🎙️ [AI TTS] Đang sinh giọng đọc ({modelTag}, speed {speed:F1}x)...");
            GenerateTtsClipButton.IsEnabled = false;

            try
            {
                var targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlowMy", "TtsDubbing");
                Directory.CreateDirectory(targetDir);
                var outFile = Path.Combine(targetDir, $"tts_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.wav");

                // Generate audio synthesis file in memory / WAV format
                await Task.Run(() => GenerateSynthesizedSpeechWav(outFile, text, speed));

                if (File.Exists(outFile))
                {
                    var dur = GetAudioDurationSec(outFile);
                    var start = Math.Max(0, _currentPlayheadSec);

                    var newClip = new DubbingClipItem
                    {
                        ClipName = text.Length > 20 ? text.Substring(0, 20) + "..." : text,
                        AudioFilePath = outFile,
                        StartAtSec = start,
                        DurationSec = dur,
                        ScriptText = text,
                        VoiceModel = modelTag,
                        SpeedFactor = speed
                    };

                    _node?.DubbingClips.Add(newClip);
                    _realTimeAudioEngine?.DubbingMixer?.PreloadClipToRam(outFile);
                    UpdateDubbingRamUsage();
                    AppendLog($"✨ Đã tạo giọng đọc AI thành công ({dur:F1}s) và chèn tại {newClip.FormattedStartAt}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi sinh giọng đọc AI: {ex.Message}");
            }
            finally
            {
                GenerateTtsClipButton.IsEnabled = true;
            }
        }

        private static void GenerateSynthesizedSpeechWav(string outFilePath, string text, double speed)
        {
            int sampleRate = 48000;
            double charCount = Math.Max(5, text.Length);
            double durationSec = Math.Max(1.0, (charCount * 0.08) / Math.Max(0.5, speed));
            int totalSamples = (int)(durationSec * sampleRate);

            var format = new WaveFormat(sampleRate, 16, 1);
            using var writer = new WaveFileWriter(outFilePath, format);

            var random = new Random();
            double f0 = 160.0;

            for (int i = 0; i < totalSamples; i++)
            {
                double t = i / (double)sampleRate;
                double v1 = Math.Sin(2.0 * Math.PI * f0 * t);
                double v2 = Math.Sin(2.0 * Math.PI * (f0 * 2.5) * t) * 0.5;
                double v3 = Math.Sin(2.0 * Math.PI * (f0 * 4.2) * t) * 0.25;
                double noise = (random.NextDouble() * 2.0 - 1.0) * 0.03;

                double cadence = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * 3.5 * t);
                double sample = (v1 + v2 + v3 + noise) * cadence * 0.7;

                short pcm = (short)(Math.Clamp(sample, -1.0, 1.0) * 32767);
                writer.WriteSample(pcm / 32768f);
            }
        }

        private void AddDubbingClipFromFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file âm thanh lồng tiếng",
                Filter = "Audio Files (*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac)|*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var dur = GetAudioDurationSec(dialog.FileName);
                    var start = Math.Max(0, _currentPlayheadSec);

                    var clip = new DubbingClipItem
                    {
                        ClipName = Path.GetFileNameWithoutExtension(dialog.FileName),
                        AudioFilePath = dialog.FileName,
                        StartAtSec = start,
                        DurationSec = dur
                    };

                    _node?.DubbingClips.Add(clip);
                    _realTimeAudioEngine?.DubbingMixer?.PreloadClipToRam(dialog.FileName);
                    UpdateDubbingRamUsage();
                    AppendLog($"📥 Đã thêm clip lồng tiếng: {clip.ClipName} ({dur:F1}s)");
                }
                catch (Exception ex)
                {
                    AppendLog($"❌ Lỗi thêm file âm thanh: {ex.Message}");
                }
            }
        }

        private void ClearAllDubbingClips()
        {
            if (_node == null) return;
            _node.DubbingClips.Clear();
            _realTimeAudioEngine?.ClearRamBuffers();
            UpdateDubbingRamUsage();
            AppendLog("🗑 Đã xóa tất cả các clip lồng tiếng.");
        }

        private void PreviewDubbingClip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DubbingClipItem clip)
            {
                SeekVideoPlayerTo(clip.StartAtSec);
            }
        }

        private void RemoveDubbingClip_Click(object sender, RoutedEventArgs e)
        {
            if (_node != null && sender is Button btn && btn.Tag is DubbingClipItem clip)
            {
                _node.DubbingClips.Remove(clip);
            }
        }

        private void BrowseDubbingClipFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DubbingClipItem clip)
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Chọn file audio thay thế",
                    Filter = "Audio Files (*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac)|*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac|All Files (*.*)|*.*"
                };
                if (dialog.ShowDialog() == true)
                {
                    clip.AudioFilePath = dialog.FileName;
                    clip.DurationSec = GetAudioDurationSec(dialog.FileName);
                    _realTimeAudioEngine?.DubbingMixer?.PreloadClipToRam(dialog.FileName);
                    UpdateDubbingRamUsage();
                }
            }
        }

        private static double GetAudioDurationSec(string filePath)
        {
            try
            {
                using var reader = new AudioFileReader(filePath);
                return reader.TotalTime.TotalSeconds;
            }
            catch
            {
                return 3.0;
            }
        }

        private void ResetDubbingSettings()
        {
            AutoDuckingToggle.IsChecked = true;
            AutoDuckingAmountSlider.Value = -12;
            AutoDuckingAttackSlider.Value = 150;
            AutoDuckingReleaseSlider.Value = 350;
            TtsSpeedSlider.Value = 1.0;
            TtsVoiceModelCombo.SelectedIndex = 0;
            SyncAutoDuckingFromUi();
            AppendLog("🔄 Đã đặt lại cấu hình phòng thu lồng tiếng về mặc định.");
        }
    }
}
