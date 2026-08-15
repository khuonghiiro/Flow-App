using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow.Audio;
using FlowMy.Services.Workflow.NodeExecutors;
using Microsoft.Win32;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl
    {
        private bool _isAudioTrimPreviewing;
        private MediaPlayer? _audioDspPlayer;
        private bool _isDspAudioPreviewActive;
        private string? _lastDspAudioPreviewPath;
        private CancellationTokenSource? _dspPreviewCts;

        public void ShowVideoProcessing(string message)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (VideoTopLoadingBar != null) VideoTopLoadingBar.Visibility = Visibility.Visible;
                if (VideoProcessingStatusPill != null) VideoProcessingStatusPill.Visibility = Visibility.Visible;
                if (VideoProcessingStatusText != null) VideoProcessingStatusText.Text = message;
            });
        }

        public void HideVideoProcessing()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (VideoTopLoadingBar != null) VideoTopLoadingBar.Visibility = Visibility.Collapsed;
                if (VideoProcessingStatusPill != null) VideoProcessingStatusPill.Visibility = Visibility.Collapsed;
            });
        }

        private void SetDspControlsEnabled(bool enabled)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (PresetAudioNeutralButton != null) PresetAudioNeutralButton.IsEnabled = enabled;
                if (PresetAudioVocalButton != null) PresetAudioVocalButton.IsEnabled = enabled;
                if (PresetAudioBassButton != null) PresetAudioBassButton.IsEnabled = enabled;
                if (PresetAudioTrebleButton != null) PresetAudioTrebleButton.IsEnabled = enabled;
                if (PresetAudioPodcastButton != null) PresetAudioPodcastButton.IsEnabled = enabled;

                if (AudioBassGainSlider != null) AudioBassGainSlider.IsEnabled = enabled;
                if (AudioTrebleGainSlider != null) AudioTrebleGainSlider.IsEnabled = enabled;
                if (AudioFadeInSlider != null) AudioFadeInSlider.IsEnabled = enabled;
                if (AudioFadeOutSlider != null) AudioFadeOutSlider.IsEnabled = enabled;

                if (AudioNormalizeCheckBox != null) AudioNormalizeCheckBox.IsEnabled = enabled;
                if (AudioDenoiseCheckBox != null) AudioDenoiseCheckBox.IsEnabled = enabled;
                if (AudioHighpassCheckBox != null) AudioHighpassCheckBox.IsEnabled = enabled;
                if (AudioLowpassCheckBox != null) AudioLowpassCheckBox.IsEnabled = enabled;
                if (AudioTargetLufsCombo != null) AudioTargetLufsCombo.IsEnabled = enabled;

                if (QuickPreviewDsp15sButton != null) QuickPreviewDsp15sButton.IsEnabled = enabled;
                if (ToggleDspAudioPreviewButton != null) ToggleDspAudioPreviewButton.IsEnabled = enabled;
                if (LaunchFfplayPreviewButton != null) LaunchFfplayPreviewButton.IsEnabled = enabled;
                if (LaunchFfplayAudioButton != null) LaunchFfplayAudioButton.IsEnabled = enabled;
            });
        }

        private void WireAudioTabEvents()
        {
            // 1. Source Audio & Volume
            if (SourceAudioToggle != null)
            {
                SourceAudioToggle.Checked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.SourceAudioEnabled = true;
                    if (SourceAudioVolumeGrid != null) SourceAudioVolumeGrid.Visibility = Visibility.Visible;
                    UpdatePreviewAudioVolume();
                };
                SourceAudioToggle.Unchecked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.SourceAudioEnabled = false;
                    if (SourceAudioVolumeGrid != null) SourceAudioVolumeGrid.Visibility = Visibility.Collapsed;
                    UpdatePreviewAudioVolume();
                };
            }

            if (SourceAudioVolumeSlider != null)
            {
                SourceAudioVolumeSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.SourceAudioVolumePercent = e.NewValue;
                    if (SourceAudioVolumeLabel != null)
                        SourceAudioVolumeLabel.Text = $"{e.NewValue:0}%";
                    UpdatePreviewAudioVolume();
                };
            }

            // 2. Audio Speed Slider (Applied strictly ON-RELEASE to prevent DirectShow audio stall)
            if (AudioSpeedSlider != null)
            {
                AudioSpeedSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioSpeedLabel != null)
                        AudioSpeedLabel.Text = $"{e.NewValue:0.##}x";
                };

                AudioSpeedSlider.PreviewMouseLeftButtonUp += (_, _) => CommitAudioSpeedFromSlider();
                AudioSpeedSlider.LostMouseCapture += (_, _) => CommitAudioSpeedFromSlider();
                AudioSpeedSlider.TouchUp += (_, _) => CommitAudioSpeedFromSlider();
            }

            // 2b. Player Speed ComboBox (Synchronized with AudioSpeedSlider & Transport Bar)
            if (PlayerSpeedCombo != null)
            {
                PlayerSpeedCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    if (PlayerSpeedCombo.SelectedItem is ComboBoxItem item &&
                        double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate))
                    {
                        _node.AudioSpeedFactor = rate;
                        _suppressControlSync = true;
                        if (AudioSpeedSlider != null) AudioSpeedSlider.Value = rate;
                        if (AudioSpeedLabel != null) AudioSpeedLabel.Text = $"{rate:0.##}x";
                        _suppressControlSync = false;

                        ApplyLivePlaybackSpeed(rate);
                    }
                };
            }

            // 3. Audio Trimming (Applied on release / box enter)
            if (AudioTrimToggle != null)
            {
                AudioTrimToggle.Checked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioTrimEnabled = true;
                    if (AudioTrimContainer != null) AudioTrimContainer.Visibility = Visibility.Visible;
                    TriggerDspRegenIfActive();
                };
                AudioTrimToggle.Unchecked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioTrimEnabled = false;
                    if (AudioTrimContainer != null) AudioTrimContainer.Visibility = Visibility.Collapsed;
                    TriggerDspRegenIfActive();
                };
            }

            if (AudioTrimStartSlider != null)
            {
                AudioTrimStartSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    var val = Math.Round(e.NewValue, 2);
                    _node.AudioTrimStartSec = val;
                    if (AudioTrimStartBox != null) AudioTrimStartBox.Text = FormatTimeSec(val);
                    if (AudioTrimStartSecLabel != null) AudioTrimStartSecLabel.Text = $"{val:0.##}s";
                    UpdateAudioTrimDurationLabel();
                };
                AudioTrimStartSlider.PreviewMouseLeftButtonUp += (_, _) => CommitTrimSeekPosition(_node.AudioTrimStartSec);
                AudioTrimStartSlider.LostMouseCapture += (_, _) => CommitTrimSeekPosition(_node.AudioTrimStartSec);
            }

            if (AudioTrimEndSlider != null)
            {
                AudioTrimEndSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    var val = Math.Round(e.NewValue, 2);
                    _node.AudioTrimEndSec = val;
                    if (AudioTrimEndBox != null) AudioTrimEndBox.Text = FormatTimeSec(val);
                    if (AudioTrimEndSecLabel != null) AudioTrimEndSecLabel.Text = $"{val:0.##}s";
                    UpdateAudioTrimDurationLabel();
                };
                AudioTrimEndSlider.PreviewMouseLeftButtonUp += (_, _) => CommitTrimSeekPosition(_node.AudioTrimEndSec);
                AudioTrimEndSlider.LostMouseCapture += (_, _) => CommitTrimSeekPosition(_node.AudioTrimEndSec);
            }

            if (SetAudioTrimStartFromPlayheadButton != null)
            {
                SetAudioTrimStartFromPlayheadButton.Click += (_, _) =>
                {
                    if (PreviewMedia != null && PreviewMedia.Source != null)
                    {
                        var pos = Math.Round(PreviewMedia.Position.TotalSeconds, 2);
                        _node.AudioTrimStartSec = pos;
                        SyncAudioTabFromModel();
                        TriggerDspRegenIfActive();
                    }
                };
            }

            if (SetAudioTrimEndFromPlayheadButton != null)
            {
                SetAudioTrimEndFromPlayheadButton.Click += (_, _) =>
                {
                    if (PreviewMedia != null && PreviewMedia.Source != null)
                    {
                        var pos = Math.Round(PreviewMedia.Position.TotalSeconds, 2);
                        _node.AudioTrimEndSec = pos;
                        SyncAudioTabFromModel();
                        TriggerDspRegenIfActive();
                    }
                };
            }

            if (AudioTrimStartBox != null)
            {
                AudioTrimStartBox.LostFocus += (_, _) => CommitAudioTrimStartBox();
                AudioTrimStartBox.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        CommitAudioTrimStartBox();
                        Keyboard.ClearFocus();
                    }
                };
            }

            if (AudioTrimEndBox != null)
            {
                AudioTrimEndBox.LostFocus += (_, _) => CommitAudioTrimEndBox();
                AudioTrimEndBox.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        CommitAudioTrimEndBox();
                        Keyboard.ClearFocus();
                    }
                };
            }

            if (PreviewAudioTrimButton != null)
            {
                PreviewAudioTrimButton.Click += (_, _) =>
                {
                    if (PreviewMedia != null && PreviewMedia.Source != null)
                    {
                        var start = Math.Max(0, _node.AudioTrimStartSec);
                        PreviewMedia.Position = TimeSpan.FromSeconds(start);
                        _isAudioTrimPreviewing = true;
                        PreviewMedia.Play();
                        _isPlaying = true;
                        if (LiveDot != null) LiveDot.Visibility = Visibility.Visible;
                        UpdatePlaybackUi();
                    }
                };
            }

            if (ResetAudioTrimButton != null)
            {
                ResetAudioTrimButton.Click += (_, _) =>
                {
                    var total = GetNaturalDurationSeconds();
                    _node.AudioTrimStartSec = 0;
                    _node.AudioTrimEndSec = total > 0 ? total : 0;
                    SyncAudioTabFromModel();
                    TriggerDspRegenIfActive();
                };
            }

            // 4. Equalizer Presets
            if (PresetAudioNeutralButton != null) PresetAudioNeutralButton.Click += (_, _) => ApplyAudioEqPreset("neutral");
            if (PresetAudioVocalButton != null) PresetAudioVocalButton.Click += (_, _) => ApplyAudioEqPreset("vocal");
            if (PresetAudioBassButton != null) PresetAudioBassButton.Click += (_, _) => ApplyAudioEqPreset("bass");
            if (PresetAudioTrebleButton != null) PresetAudioTrebleButton.Click += (_, _) => ApplyAudioEqPreset("treble");
            if (PresetAudioPodcastButton != null) PresetAudioPodcastButton.Click += (_, _) => ApplyAudioEqPreset("podcast");

            // 5. Bass & Treble Sliders (Applied strictly on release)
            if (AudioBassGainSlider != null)
            {
                AudioBassGainSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioBassGainLabel != null)
                        AudioBassGainLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0.#}dB";
                };
                AudioBassGainSlider.PreviewMouseLeftButtonUp += (_, _) => CommitAudioBassGainFromSlider();
                AudioBassGainSlider.LostMouseCapture += (_, _) => CommitAudioBassGainFromSlider();
            }

            if (AudioTrebleGainSlider != null)
            {
                AudioTrebleGainSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioTrebleGainLabel != null)
                        AudioTrebleGainLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0.#}dB";
                };
                AudioTrebleGainSlider.PreviewMouseLeftButtonUp += (_, _) => CommitAudioTrebleGainFromSlider();
                AudioTrebleGainSlider.LostMouseCapture += (_, _) => CommitAudioTrebleGainFromSlider();
            }

            // 6. Fade In & Fade Out (Applied on release)
            if (AudioFadeInSlider != null)
            {
                AudioFadeInSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioFadeInLabel != null)
                        AudioFadeInLabel.Text = $"{e.NewValue:0.#}s";
                };
                AudioFadeInSlider.PreviewMouseLeftButtonUp += (_, _) =>
                {
                    _node.AudioFadeInSec = AudioFadeInSlider.Value;
                    TriggerDspRegenIfActive();
                };
            }

            if (AudioFadeOutSlider != null)
            {
                AudioFadeOutSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioFadeOutLabel != null)
                        AudioFadeOutLabel.Text = $"{e.NewValue:0.#}s";
                };
                AudioFadeOutSlider.PreviewMouseLeftButtonUp += (_, _) =>
                {
                    _node.AudioFadeOutSec = AudioFadeOutSlider.Value;
                    TriggerDspRegenIfActive();
                };
            }

            // 7. Normalization & Filters
            if (AudioNormalizeCheckBox != null)
            {
                AudioNormalizeCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioNormalizeEnabled = true; TriggerDspRegenIfActive(); } };
                AudioNormalizeCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioNormalizeEnabled = false; TriggerDspRegenIfActive(); } };
            }

            if (AudioTargetLufsCombo != null)
            {
                AudioTargetLufsCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioTargetLufsCombo.SelectedItem is ComboBoxItem item &&
                        double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lufs))
                    {
                        _node.AudioTargetLufs = lufs;
                        TriggerDspRegenIfActive();
                    }
                };
            }

            if (AudioDenoiseCheckBox != null)
            {
                AudioDenoiseCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioDenoiseEnabled = true; TriggerDspRegenIfActive(); } };
                AudioDenoiseCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioDenoiseEnabled = false; TriggerDspRegenIfActive(); } };
            }

            if (AudioHighpassCheckBox != null)
            {
                AudioHighpassCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioHighpassFilter = true; TriggerDspRegenIfActive(); } };
                AudioHighpassCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioHighpassFilter = false; TriggerDspRegenIfActive(); } };
            }

            if (AudioLowpassCheckBox != null)
            {
                AudioLowpassCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioLowpassFilter = true; TriggerDspRegenIfActive(); } };
                AudioLowpassCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioLowpassFilter = false; TriggerDspRegenIfActive(); } };
            }

            // 8. DSP Live Preview Controls
            if (ToggleDspAudioPreviewButton != null)
            {
                ToggleDspAudioPreviewButton.Checked += (_, _) =>
                {
                    _isDspAudioPreviewActive = true;
                    if (AudioDspStatusText != null)
                    {
                        AudioDspStatusText.Text = "🎧 Đang tạo bộ lọc DSP...";
                        AudioDspStatusText.Foreground = (Brush)FindResource("ThemeAccentBrush");
                    }
                    _ = GenerateAndLoadDspAudioPreviewAsync(playImmediately: _isPlaying);
                };
                ToggleDspAudioPreviewButton.Unchecked += (_, _) =>
                {
                    _isDspAudioPreviewActive = false;
                    _audioDspPlayer?.Pause();
                    UpdatePreviewAudioVolume();
                    if (AudioDspStatusText != null)
                    {
                        AudioDspStatusText.Text = "🔊 Đang nghe audio gốc";
                        AudioDspStatusText.Foreground = (Brush)FindResource("ThemeTextSecondaryBrush");
                    }
                    HideVideoProcessing();
                    AppendLog("🔊 Đã chuyển về nghe âm thanh gốc (Raw Audio).");
                };
            }

            if (QuickPreviewDsp15sButton != null)
            {
                QuickPreviewDsp15sButton.Click += (_, _) =>
                {
                    var curSec = PreviewMedia?.Position.TotalSeconds ?? 0;
                    _isDspAudioPreviewActive = true;
                    if (ToggleDspAudioPreviewButton != null)
                        ToggleDspAudioPreviewButton.IsChecked = true;
                    _ = GenerateAndLoadDspAudioPreviewAsync(playImmediately: true, startSec: curSec, maxDuration: 15);
                };
            }

            // 8b. FFplay Hardware Accelerated Direct Player
            if (LaunchFfplayPreviewButton != null)
            {
                LaunchFfplayPreviewButton.Click += (_, _) => LaunchFfplayPreview(audioOnly: false);
            }

            if (LaunchFfplayAudioButton != null)
            {
                LaunchFfplayAudioButton.Click += (_, _) => LaunchFfplayPreview(audioOnly: false);
            }

            // 9. Export Configuration & Actions
            if (AudioExportFormatCombo != null)
            {
                AudioExportFormatCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioExportFormatCombo.SelectedItem is ComboBoxItem item)
                        _node.AudioExportFormat = item.Tag?.ToString() ?? "mp3";
                };
            }

            if (AudioExportBitrateCombo != null)
            {
                AudioExportBitrateCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioExportBitrateCombo.SelectedItem is ComboBoxItem item)
                        _node.AudioExportBitrate = item.Tag?.ToString() ?? "320k";
                };
            }

            if (AudioExportSampleRateCombo != null)
            {
                AudioExportSampleRateCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioExportSampleRateCombo.SelectedItem is ComboBoxItem item)
                        _node.AudioExportSampleRate = item.Tag?.ToString() ?? "48000";
                };
            }

            if (AudioExportChannelsCombo != null)
            {
                AudioExportChannelsCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    if (AudioExportChannelsCombo.SelectedItem is ComboBoxItem item)
                        _node.AudioExportChannels = item.Tag?.ToString() ?? "stereo";
                };
            }

            if (BrowseAudioTabOutputFolderButton != null)
            {
                BrowseAudioTabOutputFolderButton.Click += (_, _) =>
                {
                    var dlg = new OpenFolderDialog { Title = "Chọn thư mục lưu file Audio trích xuất" };
                    if (dlg.ShowDialog() == true)
                    {
                        _node.AudioOutputFolderPath = dlg.FolderName;
                        if (AudioOutputFolderPathText != null)
                            AudioOutputFolderPathText.Text = dlg.FolderName;
                    }
                };
            }

            if (OpenAudioTabOutputFolderButton != null)
            {
                OpenAudioTabOutputFolderButton.Click += (_, _) =>
                {
                    var folder = ResolveAudioOutputDirectory();
                    if (Directory.Exists(folder))
                        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                    else
                        AppendLog($"⚠ Thư mục chưa tồn tại: {folder}");
                };
            }

            if (AddAudioTrackButton != null) AddAudioTrackButton.Click += (_, _) => _node.AudioTracks.Add(new VideoAudioTrackConfig());
            if (ExtractFullAudioButton != null) ExtractFullAudioButton.Click += (_, _) => TriggerExtractAudio(trimmedOnly: false);
            if (ExtractTrimmedAudioButton != null) ExtractTrimmedAudioButton.Click += (_, _) => TriggerExtractAudio(trimmedOnly: true);
        }

        public void LaunchFfplayPreview(bool audioOnly = false)
        {
            if (string.IsNullOrWhiteSpace(_node.VideoPath) || !File.Exists(_node.VideoPath))
            {
                AppendLog("⚠ Chưa có file video nguồn hợp lệ để mở FFplay.");
                return;
            }

            var ffplayExe = EnvironmentPathPreferencesStore.ResolveBinaryPath("ffplay");
            if (string.IsNullOrWhiteSpace(ffplayExe) || !File.Exists(ffplayExe))
            {
                AppendLog("⚠ Chưa cấu hình hoặc không tìm thấy ffplay.exe trên hệ thống.");
                return;
            }

            var currentPos = PreviewMedia != null ? PreviewMedia.Position.TotalSeconds : 0;
            var totalDuration = GetNaturalDurationSeconds();
            var af = VideoAudioFilterGraphBuilder.BuildSourceAudioFilterGraph(_node, totalDuration, applyTrim: _node.AudioTrimEnabled);
            var vf = BuildFfplayVideoFilters();

            // Pause internal preview to avoid audio echo
            if (PreviewMedia != null && PreviewMedia.Source != null && _isPlaying)
            {
                PreviewMedia.Pause();
                _audioDspPlayer?.Pause();
                _isPlaying = false;
                UpdatePlaybackUi();
            }

            if (totalDuration > 0 && currentPos >= totalDuration - 0.5)
            {
                currentPos = 0;
            }

            var psi = new ProcessStartInfo
            {
                FileName = ffplayExe,
                UseShellExecute = false,
                CreateNoWindow = audioOnly
            };

            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-window_title");
            psi.ArgumentList.Add($"FlowMy Preview - {Path.GetFileName(_node.VideoPath)}");

            if (currentPos > 0.1)
            {
                psi.ArgumentList.Add("-ss");
                psi.ArgumentList.Add(currentPos.ToString("0.###", CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(af))
            {
                psi.ArgumentList.Add("-af");
                psi.ArgumentList.Add(af);
            }

            if (audioOnly)
            {
                psi.ArgumentList.Add("-nodisp");
            }
            else if (!string.IsNullOrWhiteSpace(vf))
            {
                psi.ArgumentList.Add("-vf");
                psi.ArgumentList.Add(vf);
            }

            psi.ArgumentList.Add(_node.VideoPath);

            try
            {
                Process.Start(psi);
                AppendLog($"🎬 [FFPLAY] Đã mở cửa sổ phát phần cứng FFplay tại {FormatTimeSec(currentPos)}.");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ [FFPLAY] Không thể khởi chạy FFplay: {ex.Message}");
            }
        }

        private string BuildFfplayVideoFilters()
        {
            var vfs = new List<string>();

            // 1. Color Adjustment (Brightness, Contrast, Saturation, Gamma)
            var hasColor = Math.Abs(_node.Brightness) > 0.01 ||
                           Math.Abs(_node.Contrast - 1.0) > 0.01 ||
                           Math.Abs(_node.Saturation - 1.0) > 0.01 ||
                           Math.Abs(_node.Gamma - 1.0) > 0.01;
            if (hasColor)
            {
                var b = _node.Brightness.ToString("0.##", CultureInfo.InvariantCulture);
                var c = _node.Contrast.ToString("0.##", CultureInfo.InvariantCulture);
                var s = _node.Saturation.ToString("0.##", CultureInfo.InvariantCulture);
                var g = _node.Gamma.ToString("0.##", CultureInfo.InvariantCulture);
                vfs.Add($"eq=brightness={b}:contrast={c}:saturation={s}:gamma={g}");
            }

            // 2. Flip & Rotate
            if (_node.FlipH && _node.FlipV) vfs.Add("hflip,vflip");
            else if (_node.FlipH) vfs.Add("hflip");
            else if (_node.FlipV) vfs.Add("vflip");

            if ((int)_node.RotationDegrees == 90) vfs.Add("transpose=1");
            else if ((int)_node.RotationDegrees == 180) vfs.Add("transpose=1,transpose=1");
            else if ((int)_node.RotationDegrees == 270) vfs.Add("transpose=2");

            return string.Join(",", vfs);
        }

        private void CommitAudioSpeedFromSlider()
        {
            if (AudioSpeedSlider == null) return;
            var speed = AudioSpeedSlider.Value;
            _node.AudioSpeedFactor = speed;
            SyncPlayerSpeedCombo(speed);
            ApplyLivePlaybackSpeed(speed);
        }

        private void CommitAudioBassGainFromSlider()
        {
            if (AudioBassGainSlider == null) return;
            _node.AudioBassGain = AudioBassGainSlider.Value;
            TriggerDspRegenIfActive();
        }

        private void CommitAudioTrebleGainFromSlider()
        {
            if (AudioTrebleGainSlider == null) return;
            _node.AudioTrebleGain = AudioTrebleGainSlider.Value;
            TriggerDspRegenIfActive();
        }

        private void CommitTrimSeekPosition(double sec)
        {
            if (PreviewMedia != null && PreviewMedia.Source != null && !_isPlaying)
            {
                PreviewMedia.Position = TimeSpan.FromSeconds(sec);
                UpdatePlaybackUi();
            }
            TriggerDspRegenIfActive();
        }

        private void ApplyLivePlaybackSpeed(double speed)
        {
            if (speed < 0.1 || speed > 4.0) return;
            try
            {
                if (PreviewMedia != null && PreviewMedia.Source != null)
                {
                    PreviewMedia.SpeedRatio = speed;
                }
                if (_audioDspPlayer != null)
                {
                    _audioDspPlayer.SpeedRatio = speed;
                }
            }
            catch { /* best effort */ }
        }

        private void SyncPlayerSpeedCombo(double speed)
        {
            if (PlayerSpeedCombo == null) return;
            _suppressControlSync = true;
            try
            {
                var speedStr = speed.ToString("0.##", CultureInfo.InvariantCulture);
                foreach (var item in PlayerSpeedCombo.Items.OfType<ComboBoxItem>())
                {
                    if (string.Equals(item.Tag?.ToString(), speedStr, StringComparison.OrdinalIgnoreCase))
                    {
                        PlayerSpeedCombo.SelectedItem = item;
                        return;
                    }
                }
            }
            finally
            {
                _suppressControlSync = false;
            }
        }

        private void InitDspAudioPlayer()
        {
            if (_audioDspPlayer == null)
            {
                _audioDspPlayer = new MediaPlayer();
                _audioDspPlayer.MediaEnded += (_, _) =>
                {
                    if (_isPlaying)
                    {
                        _audioDspPlayer.Position = TimeSpan.Zero;
                        _audioDspPlayer.Play();
                    }
                };
            }
        }

        private void TriggerDspRegenIfActive()
        {
            if (!_isDspAudioPreviewActive) return;
            _ = GenerateAndLoadDspAudioPreviewAsync(playImmediately: _isPlaying);
        }

        private async Task GenerateAndLoadDspAudioPreviewAsync(bool playImmediately, double startSec = -1, double maxDuration = 45)
        {
            if (string.IsNullOrWhiteSpace(_node.VideoPath) || !File.Exists(_node.VideoPath))
            {
                if (AudioDspStatusText != null) AudioDspStatusText.Text = "⚠ Chưa có file video nguồn";
                return;
            }

            _dspPreviewCts?.Cancel();
            _dspPreviewCts = new CancellationTokenSource();
            var ct = _dspPreviewCts.Token;

            // Capture all UI thread variables up-front safely on the UI thread
            var videoPath = _node.VideoPath;
            var currentPlayPos = PreviewMedia != null ? PreviewMedia.Position.TotalSeconds : 0;
            var isCurrentlyPlaying = _isPlaying;
            var previewVol = _node.PreviewVolume;
            var srcVolPercent = _node.SourceAudioVolumePercent;
            var speedFactor = _node.AudioSpeedFactor;
            var trimEnabled = _node.AudioTrimEnabled;
            var totalDuration = GetNaturalDurationSeconds();

            var seekPos = startSec >= 0 ? startSec : currentPlayPos;
            if (seekPos < 0) seekPos = 0;

            var filterGraph = VideoAudioFilterGraphBuilder.BuildSourceAudioFilterGraph(_node, totalDuration, applyTrim: trimEnabled);
            var activeFilterSummary = GetActiveFilterSummary();

            SetDspControlsEnabled(false);
            ShowVideoProcessing("Đang xử lý âm thanh DSP...");

            try
            {
                var tempWav = Path.Combine(Path.GetTempPath(), $"flowmy_dsp_{Guid.NewGuid():N}.wav");

                // Ultra fast audio extraction (< 0.05s) by placing -ss before -i and limiting duration to 45s
                var args = new List<string>
                {
                    "-y", "-hide_banner", "-loglevel", "error"
                };

                if (seekPos > 0)
                {
                    args.AddRange(new[] { "-ss", seekPos.ToString("0.###", CultureInfo.InvariantCulture) });
                }
                if (maxDuration > 0)
                {
                    args.AddRange(new[] { "-t", maxDuration.ToString("0.###", CultureInfo.InvariantCulture) });
                }

                args.AddRange(new[] { "-i", videoPath, "-vn" });

                if (!string.IsNullOrWhiteSpace(filterGraph))
                {
                    args.AddRange(new[] { "-af", filterGraph });
                }

                args.AddRange(new[] { "-c:a", "pcm_s16le", tempWav });

                await VideoProcessingNodeExecutor.RunFfmpegAsync(args, ct).ConfigureAwait(false);

                if (ct.IsCancellationRequested)
                {
                    try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (File.Exists(tempWav))
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(_lastDspAudioPreviewPath) && File.Exists(_lastDspAudioPreviewPath))
                                File.Delete(_lastDspAudioPreviewPath);
                        }
                        catch { }

                        _lastDspAudioPreviewPath = tempWav;
                        InitDspAudioPlayer();
                        _audioDspPlayer!.Open(new Uri(tempWav));
                        _audioDspPlayer.Volume = Math.Clamp((previewVol * (srcVolPercent / 100.0)), 0, 1);
                        _audioDspPlayer.SpeedRatio = Math.Clamp(speedFactor, 0.1, 4.0);

                        if (seekPos > 0)
                        {
                            _audioDspPlayer.Position = TimeSpan.Zero;
                        }
                        else if (PreviewMedia != null)
                        {
                            _audioDspPlayer.Position = PreviewMedia.Position;
                        }

                        if (playImmediately)
                        {
                            if (PreviewMedia != null) PreviewMedia.IsMuted = true;
                            _audioDspPlayer.Play();
                            _isPlaying = true;
                            if (PreviewMedia != null && PreviewMedia.Source != null) PreviewMedia.Play();
                            if (LiveDot != null) LiveDot.Visibility = Visibility.Visible;
                            UpdatePlaybackUi();
                        }

                        if (AudioDspStatusText != null)
                        {
                            AudioDspStatusText.Text = $"🎧 Đang nghe DSP ({activeFilterSummary})";
                            AudioDspStatusText.Foreground = (Brush)FindResource("ThemeAccentBrush");
                        }
                        AppendLog($"🎧 Đã nạp bộ lọc âm thanh DSP ({activeFilterSummary}) vào trình phát.");
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (AudioDspStatusText != null) AudioDspStatusText.Text = "⚠ Lỗi tạo DSP audio";
                    AppendLog($"❌ Không thể tạo DSP preview: {ex.Message}");
                });
            }
            finally
            {
                SetDspControlsEnabled(true);
                HideVideoProcessing();
            }
        }

        private string GetActiveFilterSummary()
        {
            var parts = new List<string>();
            if (_node.AudioBassGain != 0) parts.Add($"Bass {(_node.AudioBassGain > 0 ? "+" : "")}{_node.AudioBassGain:0.#}dB");
            if (_node.AudioTrebleGain != 0) parts.Add($"Treble {(_node.AudioTrebleGain > 0 ? "+" : "")}{_node.AudioTrebleGain:0.#}dB");
            if (_node.AudioDenoiseEnabled) parts.Add("Denoise");
            if (_node.AudioHighpassFilter) parts.Add("HP 80Hz");
            if (_node.AudioLowpassFilter) parts.Add("LP 12kHz");
            if (_node.AudioNormalizeEnabled) parts.Add($"Norm {_node.AudioTargetLufs:0}LUFS");
            if (_node.AudioSpeedFactor != 1.0) parts.Add($"{_node.AudioSpeedFactor:0.##}x");
            return parts.Count > 0 ? string.Join(", ", parts) : "Neutral";
        }

        private void CommitAudioTrimStartBox()
        {
            if (AudioTrimStartBox == null) return;
            if (TryParseTimeSec(AudioTrimStartBox.Text, out var sec))
            {
                _node.AudioTrimStartSec = Math.Round(sec, 2);
                AudioTrimStartBox.Text = FormatTimeSec(sec);
                if (AudioTrimStartSlider != null)
                {
                    AudioTrimStartSlider.Maximum = Math.Max(AudioTrimStartSlider.Maximum, sec);
                    AudioTrimStartSlider.Value = sec;
                }
                if (AudioTrimStartSecLabel != null) AudioTrimStartSecLabel.Text = $"{sec:0.##}s";
                UpdateAudioTrimDurationLabel();
                if (PreviewMedia != null && PreviewMedia.Source != null && !_isPlaying)
                {
                    PreviewMedia.Position = TimeSpan.FromSeconds(sec);
                    UpdatePlaybackUi();
                }
                TriggerDspRegenIfActive();
            }
        }

        private void CommitAudioTrimEndBox()
        {
            if (AudioTrimEndBox == null) return;
            if (TryParseTimeSec(AudioTrimEndBox.Text, out var sec))
            {
                _node.AudioTrimEndSec = Math.Round(sec, 2);
                AudioTrimEndBox.Text = FormatTimeSec(sec);
                if (AudioTrimEndSlider != null)
                {
                    AudioTrimEndSlider.Maximum = Math.Max(AudioTrimEndSlider.Maximum, sec);
                    AudioTrimEndSlider.Value = sec;
                }
                if (AudioTrimEndSecLabel != null) AudioTrimEndSecLabel.Text = $"{sec:0.##}s";
                UpdateAudioTrimDurationLabel();
                if (PreviewMedia != null && PreviewMedia.Source != null && !_isPlaying)
                {
                    PreviewMedia.Position = TimeSpan.FromSeconds(sec);
                    UpdatePlaybackUi();
                }
                TriggerDspRegenIfActive();
            }
        }

        private void SyncAudioTabFromModel()
        {
            _suppressControlSync = true;
            try
            {
                if (SourceAudioToggle != null)
                {
                    SourceAudioToggle.IsChecked = _node.SourceAudioEnabled;
                    if (SourceAudioVolumeGrid != null)
                        SourceAudioVolumeGrid.Visibility = _node.SourceAudioEnabled ? Visibility.Visible : Visibility.Collapsed;
                }
                if (SourceAudioVolumeSlider != null)
                {
                    SourceAudioVolumeSlider.Value = _node.SourceAudioVolumePercent;
                    if (SourceAudioVolumeLabel != null) SourceAudioVolumeLabel.Text = $"{_node.SourceAudioVolumePercent:0}%";
                }
                if (AudioSpeedSlider != null)
                {
                    AudioSpeedSlider.Value = _node.AudioSpeedFactor;
                    if (AudioSpeedLabel != null) AudioSpeedLabel.Text = $"{_node.AudioSpeedFactor:0.##}x";
                }
                SyncPlayerSpeedCombo(_node.AudioSpeedFactor);

                if (AudioTrimToggle != null)
                {
                    AudioTrimToggle.IsChecked = _node.AudioTrimEnabled;
                    if (AudioTrimContainer != null) AudioTrimContainer.Visibility = _node.AudioTrimEnabled ? Visibility.Visible : Visibility.Collapsed;
                }

                var totalDuration = GetNaturalDurationSeconds();
                var maxSec = totalDuration > 0 ? totalDuration : 100;
                if (AudioTrimStartSlider != null)
                {
                    AudioTrimStartSlider.Maximum = Math.Max(maxSec, _node.AudioTrimStartSec);
                    AudioTrimStartSlider.Value = _node.AudioTrimStartSec;
                }
                if (AudioTrimEndSlider != null)
                {
                    AudioTrimEndSlider.Maximum = Math.Max(maxSec, _node.AudioTrimEndSec);
                    AudioTrimEndSlider.Value = _node.AudioTrimEndSec;
                }

                if (AudioTrimStartBox != null) AudioTrimStartBox.Text = FormatTimeSec(_node.AudioTrimStartSec);
                if (AudioTrimEndBox != null) AudioTrimEndBox.Text = FormatTimeSec(_node.AudioTrimEndSec);
                if (AudioTrimStartSecLabel != null) AudioTrimStartSecLabel.Text = $"{_node.AudioTrimStartSec:0.##}s";
                if (AudioTrimEndSecLabel != null) AudioTrimEndSecLabel.Text = $"{_node.AudioTrimEndSec:0.##}s";
                UpdateAudioTrimDurationLabel();

                if (AudioBassGainSlider != null)
                {
                    AudioBassGainSlider.Value = _node.AudioBassGain;
                    if (AudioBassGainLabel != null) AudioBassGainLabel.Text = $"{(_node.AudioBassGain >= 0 ? "+" : "")}{_node.AudioBassGain:0.#}dB";
                }
                if (AudioTrebleGainSlider != null)
                {
                    AudioTrebleGainSlider.Value = _node.AudioTrebleGain;
                    if (AudioTrebleGainLabel != null) AudioTrebleGainLabel.Text = $"{(_node.AudioTrebleGain >= 0 ? "+" : "")}{_node.AudioTrebleGain:0.#}dB";
                }
                if (AudioFadeInSlider != null)
                {
                    AudioFadeInSlider.Value = _node.AudioFadeInSec;
                    if (AudioFadeInLabel != null) AudioFadeInLabel.Text = $"{_node.AudioFadeInSec:0.#}s";
                }
                if (AudioFadeOutSlider != null)
                {
                    AudioFadeOutSlider.Value = _node.AudioFadeOutSec;
                    if (AudioFadeOutLabel != null) AudioFadeOutLabel.Text = $"{_node.AudioFadeOutSec:0.#}s";
                }
                if (AudioNormalizeCheckBox != null) AudioNormalizeCheckBox.IsChecked = _node.AudioNormalizeEnabled;
                if (AudioDenoiseCheckBox != null) AudioDenoiseCheckBox.IsChecked = _node.AudioDenoiseEnabled;
                if (AudioHighpassCheckBox != null) AudioHighpassCheckBox.IsChecked = _node.AudioHighpassFilter;
                if (AudioLowpassCheckBox != null) AudioLowpassCheckBox.IsChecked = _node.AudioLowpassFilter;

                SelectComboItemByTag(AudioTargetLufsCombo, _node.AudioTargetLufs.ToString("0.#", CultureInfo.InvariantCulture));
                SelectComboItemByTag(AudioExportFormatCombo, _node.AudioExportFormat);
                SelectComboItemByTag(AudioExportBitrateCombo, _node.AudioExportBitrate);
                SelectComboItemByTag(AudioExportSampleRateCombo, _node.AudioExportSampleRate);
                SelectComboItemByTag(AudioExportChannelsCombo, _node.AudioExportChannels);

                if (AudioOutputFolderPathText != null)
                    AudioOutputFolderPathText.Text = _node.AudioOutputFolderPath ?? string.Empty;

                if (AudioTracksList != null)
                    AudioTracksList.ItemsSource = _node.AudioTracks;

                UpdatePreviewAudioVolume();
            }
            finally
            {
                _suppressControlSync = false;
            }
        }

        public void SyncAllTabsFromModel()
        {
            SyncAudioTabFromModel();

            // Color grading sliders & labels
            if (BrightnessSlider != null) BrightnessSlider.Value = _node.Brightness;
            if (ContrastSlider != null) ContrastSlider.Value = _node.Contrast;
            if (SaturationSlider != null) SaturationSlider.Value = _node.Saturation;
            if (QuickBrightnessSlider != null) QuickBrightnessSlider.Value = _node.Brightness;

            if (VideoPathText != null)
                VideoPathText.Text = !string.IsNullOrWhiteSpace(_node.VideoPath) ? _node.VideoPath : string.Empty;

            if (PreviewMedia != null && !string.IsNullOrWhiteSpace(_node.VideoPath) && File.Exists(_node.VideoPath))
            {
                if (PreviewMedia.Source == null || !string.Equals(PreviewMedia.Source.LocalPath, _node.VideoPath, StringComparison.OrdinalIgnoreCase))
                {
                    RefreshVideoPreview();
                }
            }

            ApplyPreviewColorTransform();
            ApplyPreviewTransformEffects();
            UpdatePreviewAudioVolume();
            UpdatePlaybackUi();
        }

        private void ApplyAudioEqPreset(string preset)
        {
            _node.AudioEqPreset = preset;
            switch (preset.ToLowerInvariant())
            {
                case "neutral":
                    _node.AudioBassGain = 0;
                    _node.AudioTrebleGain = 0;
                    _node.AudioHighpassFilter = false;
                    _node.AudioLowpassFilter = false;
                    _node.AudioNormalizeEnabled = false;
                    _node.AudioDenoiseEnabled = false;
                    break;
                case "vocal":
                    _node.AudioBassGain = -2;
                    _node.AudioTrebleGain = 4;
                    _node.AudioHighpassFilter = true;
                    _node.AudioLowpassFilter = false;
                    _node.AudioDenoiseEnabled = true;
                    break;
                case "bass":
                    _node.AudioBassGain = 7;
                    _node.AudioTrebleGain = 0;
                    _node.AudioHighpassFilter = false;
                    _node.AudioLowpassFilter = false;
                    break;
                case "treble":
                    _node.AudioBassGain = -1;
                    _node.AudioTrebleGain = 6;
                    _node.AudioHighpassFilter = false;
                    _node.AudioLowpassFilter = false;
                    break;
                case "podcast":
                    _node.AudioBassGain = -2;
                    _node.AudioTrebleGain = 3;
                    _node.AudioHighpassFilter = true;
                    _node.AudioLowpassFilter = true;
                    _node.AudioDenoiseEnabled = true;
                    _node.AudioNormalizeEnabled = true;
                    _node.AudioTargetLufs = -16.0;
                    break;
            }

            SyncAudioTabFromModel();
            AppendLog($"🎵 Đã áp dụng preset âm thanh: {preset.ToUpperInvariant()} (Bật '🎧 Nghe thử DSP' để nghe hiệu ứng).");
            TriggerDspRegenIfActive();
        }

        private void TriggerExtractAudio(bool trimmedOnly)
        {
            if (string.IsNullOrWhiteSpace(_node.VideoPath) || !File.Exists(_node.VideoPath))
            {
                AppendLog("⚠ Chưa có file video nguồn hợp lệ để trích xuất audio.");
                return;
            }

            SwitchToLogView();
            var modeName = trimmedOnly ? "Đoạn đã cắt (Trimmed Audio)" : "Toàn bộ âm thanh (Full Audio)";
            AppendLog($"🎵 [TRÍCH XUẤT AUDIO] Bắt đầu trích xuất: {modeName}...");

            var outputFolder = ResolveAudioOutputDirectory();
            Directory.CreateDirectory(outputFolder);

            ShowVideoProcessing("Đang trích xuất file audio...");

            Task.Run(async () =>
            {
                try
                {
                    var ext = VideoAudioFilterGraphBuilder.ResolveAudioFileExtension(_node.AudioExportFormat);
                    var outputAudioPath = Path.Combine(outputFolder, $"audio_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");

                    var duration = await ProbeVideoDurationAsync(_node.VideoPath).ConfigureAwait(false);
                    var filterGraph = VideoAudioFilterGraphBuilder.BuildSourceAudioFilterGraph(_node, duration, applyTrim: trimmedOnly);
                    var (codecArg, extraArgs) = VideoAudioFilterGraphBuilder.ResolveAudioExportArgs(
                        _node.AudioExportFormat, _node.AudioExportBitrate, _node.AudioExportSampleRate, _node.AudioExportChannels);

                    var args = new List<string>
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-i", _node.VideoPath,
                        "-map", "0:a:0",
                        "-vn"
                    };

                    if (!string.IsNullOrWhiteSpace(filterGraph))
                        args.AddRange(new[] { "-af", filterGraph });

                    args.AddRange(new[] { "-c:a", codecArg });
                    args.AddRange(extraArgs);
                    args.Add(outputAudioPath);

                    await VideoProcessingNodeExecutor.RunFfmpegAsync(args, CancellationToken.None).ConfigureAwait(false);

                    if (File.Exists(outputAudioPath))
                    {
                        AppendLog($"✅ [TRÍCH XUẤT AUDIO] Hoàn tất xuất file: {outputAudioPath}");
                        _node.EnsureStandardDynamicOutputs();
                        var port = _node.DynamicOutputs?.FirstOrDefault(o =>
                            string.Equals(o.Key, "audio_output", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(o.Key, "linkAudio", StringComparison.OrdinalIgnoreCase));
                        if (port != null) port.UserValueOverride = outputAudioPath;
                    }
                    else
                    {
                        AppendLog("⚠ [TRÍCH XUẤT AUDIO] Quá trình hoàn tất nhưng không tìm thấy file output.");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"❌ [TRÍCH XUẤT AUDIO THẤT BẠI]: {ex.Message}");
                }
                finally
                {
                    HideVideoProcessing();
                }
            });
        }

        private string ResolveAudioOutputDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_node.AudioOutputFolderPath))
                return _node.AudioOutputFolderPath;

            var stem = !string.IsNullOrWhiteSpace(_node.VideoPath)
                ? Path.GetFileNameWithoutExtension(_node.VideoPath)
                : "video";
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "flow-audio", stem);
        }

        private void UpdateAudioTrimDurationLabel()
        {
            if (AudioTrimDurationLabel == null) return;
            var start = _node.AudioTrimStartSec;
            var end = _node.AudioTrimEndSec;
            if (end > start)
            {
                var dur = end - start;
                AudioTrimDurationLabel.Text = $"{FormatTimeSec(start)} → {FormatTimeSec(end)} ({dur:0.##}s)";
            }
            else
            {
                AudioTrimDurationLabel.Text = $"{FormatTimeSec(start)} (0.0s)";
            }
        }

        private static string FormatTimeSec(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture)
                : ts.ToString(@"mm\:ss\.ff", CultureInfo.InvariantCulture);
        }

        private static bool TryParseTimeSec(string text, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                return true;
            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var ts))
            {
                seconds = ts.TotalSeconds;
                return true;
            }
            return false;
        }

        private static void SelectComboItemByTag(ComboBox? combo, string? tagValue)
        {
            if (combo == null || string.IsNullOrWhiteSpace(tagValue)) return;
            foreach (var item in combo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), tagValue, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        private static async Task<double> ProbeVideoDurationAsync(string videoPath)
        {
            try
            {
                return await VideoProcessingNodeExecutor.ProbeDurationSecondsAsync(videoPath, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                return 0;
            }
        }
    }
}
