// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
using System.Windows.Threading;
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
        private DateTime _audioTrimPreviewStartTime;
        private RealTimeVideoAudioEngine? _realTimeAudioEngine;
        private bool _isDspAudioPreviewActive = true;
        private DispatcherTimer? _waveVisTimer;
        private readonly float[] _waveVisBuffer = new float[256];
        private System.Windows.Shapes.Polyline? _wavePolyline;

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
                // EQ presets
                if (PresetAudioNeutralButton != null) PresetAudioNeutralButton.IsEnabled = enabled;
                if (PresetAudioVocalButton != null) PresetAudioVocalButton.IsEnabled = enabled;
                if (PresetAudioBassButton != null) PresetAudioBassButton.IsEnabled = enabled;
                if (PresetAudioTrebleButton != null) PresetAudioTrebleButton.IsEnabled = enabled;
                if (PresetAudioPodcastButton != null) PresetAudioPodcastButton.IsEnabled = enabled;

                // Voice gender presets
                if (PresetVoiceMaleDeepButton != null) PresetVoiceMaleDeepButton.IsEnabled = enabled;
                if (PresetVoiceMaleButton != null) PresetVoiceMaleButton.IsEnabled = enabled;
                if (PresetVoiceNaturalButton != null) PresetVoiceNaturalButton.IsEnabled = enabled;
                if (PresetVoiceFemaleButton != null) PresetVoiceFemaleButton.IsEnabled = enabled;
                if (PresetVoiceAnimeButton != null) PresetVoiceAnimeButton.IsEnabled = enabled;
                if (PresetVoiceChipmunkButton != null) PresetVoiceChipmunkButton.IsEnabled = enabled;
                if (PresetVoiceMonsterButton != null) PresetVoiceMonsterButton.IsEnabled = enabled;

                // Tone clarity presets
                if (PresetToneDeepButton != null) PresetToneDeepButton.IsEnabled = enabled;
                if (PresetToneWarmButton != null) PresetToneWarmButton.IsEnabled = enabled;
                if (PresetToneNaturalButton != null) PresetToneNaturalButton.IsEnabled = enabled;
                if (PresetToneBrightButton != null) PresetToneBrightButton.IsEnabled = enabled;
                if (PresetToneCrispButton != null) PresetToneCrispButton.IsEnabled = enabled;

                // Reverb presets
                if (PresetReverbRoomButton != null) PresetReverbRoomButton.IsEnabled = enabled;
                if (PresetReverbStageButton != null) PresetReverbStageButton.IsEnabled = enabled;
                if (PresetReverbHallButton != null) PresetReverbHallButton.IsEnabled = enabled;
                if (PresetReverbCaveButton != null) PresetReverbCaveButton.IsEnabled = enabled;

                // Sliders
                if (AudioPitchSlider != null) AudioPitchSlider.IsEnabled = enabled;
                if (AudioToneClaritySlider != null) AudioToneClaritySlider.IsEnabled = enabled;
                if (AudioBassGainSlider != null) AudioBassGainSlider.IsEnabled = enabled;
                if (AudioLowMidGainSlider != null) AudioLowMidGainSlider.IsEnabled = enabled;
                if (AudioMidGainSlider != null) AudioMidGainSlider.IsEnabled = enabled;
                if (AudioHighMidGainSlider != null) AudioHighMidGainSlider.IsEnabled = enabled;
                if (AudioTrebleGainSlider != null) AudioTrebleGainSlider.IsEnabled = enabled;
                if (AudioStereoWidthSlider != null) AudioStereoWidthSlider.IsEnabled = enabled;
                if (AudioWarmthSlider != null) AudioWarmthSlider.IsEnabled = enabled;
                if (AudioReverbSlider != null) AudioReverbSlider.IsEnabled = enabled;
                if (AudioVocalBalanceSlider != null) AudioVocalBalanceSlider.IsEnabled = enabled;
                if (AudioEchoDelaySlider != null) AudioEchoDelaySlider.IsEnabled = enabled;
                if (AudioEchoFeedbackSlider != null) AudioEchoFeedbackSlider.IsEnabled = enabled;
                if (AudioEchoMixSlider != null) AudioEchoMixSlider.IsEnabled = enabled;
                if (Audio8DSpeedSlider != null) Audio8DSpeedSlider.IsEnabled = enabled;
                if (AudioCompressorSlider != null) AudioCompressorSlider.IsEnabled = enabled;
                if (AudioDeEsserSlider != null) AudioDeEsserSlider.IsEnabled = enabled;
                if (AudioNoiseGateSlider != null) AudioNoiseGateSlider.IsEnabled = enabled;
                if (AudioHighpassCutoffSlider != null) AudioHighpassCutoffSlider.IsEnabled = enabled;
                if (AudioLowpassCutoffSlider != null) AudioLowpassCutoffSlider.IsEnabled = enabled;
                if (AudioFadeInSlider != null) AudioFadeInSlider.IsEnabled = enabled;
                if (AudioFadeOutSlider != null) AudioFadeOutSlider.IsEnabled = enabled;

                // Toggles & CheckBoxes
                if (AudioEchoToggle != null) AudioEchoToggle.IsEnabled = enabled;
                if (Audio8DToggle != null) Audio8DToggle.IsEnabled = enabled;
                if (AudioRobotVoiceCheckBox != null) AudioRobotVoiceCheckBox.IsEnabled = enabled;
                if (AudioRadioVoiceCheckBox != null) AudioRadioVoiceCheckBox.IsEnabled = enabled;
                if (AudioChorusCheckBox != null) AudioChorusCheckBox.IsEnabled = enabled;
                if (AudioNormalizeCheckBox != null) AudioNormalizeCheckBox.IsEnabled = enabled;
                if (AudioDenoiseCheckBox != null) AudioDenoiseCheckBox.IsEnabled = enabled;
                if (AudioHighpassCheckBox != null) AudioHighpassCheckBox.IsEnabled = enabled;
                if (AudioLowpassCheckBox != null) AudioLowpassCheckBox.IsEnabled = enabled;
                if (AudioTargetLufsCombo != null) AudioTargetLufsCombo.IsEnabled = enabled;

                // Wave Shaper & Visualizer
                if (PresetWaveCleanButton != null) PresetWaveCleanButton.IsEnabled = enabled;
                if (PresetWaveTapeButton != null) PresetWaveTapeButton.IsEnabled = enabled;
                if (PresetWaveSoftButton != null) PresetWaveSoftButton.IsEnabled = enabled;
                if (PresetWaveHardButton != null) PresetWaveHardButton.IsEnabled = enabled;
                if (PresetWaveFoldButton != null) PresetWaveFoldButton.IsEnabled = enabled;
                if (AudioWaveShaperToggle != null) AudioWaveShaperToggle.IsEnabled = enabled;
                if (AudioWaveShaperDriveSlider != null) AudioWaveShaperDriveSlider.IsEnabled = enabled;
                if (AudioTransientPunchSlider != null) AudioTransientPunchSlider.IsEnabled = enabled;
                if (AudioSubHarmonicsSlider != null) AudioSubHarmonicsSlider.IsEnabled = enabled;
                if (AudioHarmonicExciterSlider != null) AudioHarmonicExciterSlider.IsEnabled = enabled;
                if (AudioPhaseInvertLToggle != null) AudioPhaseInvertLToggle.IsEnabled = enabled;
                if (AudioPhaseInvertRToggle != null) AudioPhaseInvertRToggle.IsEnabled = enabled;

                if (QuickPreviewDsp15sButton != null) QuickPreviewDsp15sButton.IsEnabled = enabled;
                if (ToggleDspAudioPreviewButton != null) ToggleDspAudioPreviewButton.IsEnabled = enabled;
                if (LaunchFfplayPreviewButton != null) LaunchFfplayPreviewButton.IsEnabled = enabled;
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
                        var total = GetNaturalDurationSeconds();
                        if (total > 0.05)
                        {
                            ClampAndSyncTrimRangesToVideoDuration(total);
                        }

                        var start = Math.Max(0, _node.AudioTrimStartSec);
                        var end = _node.AudioTrimEndSec;
                        if (end <= start)
                        {
                            end = total > start ? total : (start + 5.0);
                            _node.AudioTrimEndSec = end;
                            SyncAudioTabFromModel();
                        }

                        EnsureRealTimeAudioEngineLoaded();
                        PreviewMedia.Position = TimeSpan.FromSeconds(start);
                        if (_realTimeAudioEngine != null && _realTimeAudioEngine.IsLoaded)
                        {
                            PreviewMedia.IsMuted = true;
                            _realTimeAudioEngine.Seek(TimeSpan.FromSeconds(start));
                            _realTimeAudioEngine.ApplyParameters(_node, _node.PreviewVolume, _isDspAudioPreviewActive);
                            _realTimeAudioEngine.Play();
                        }
                        else
                        {
                            UpdatePreviewAudioVolume();
                        }

                        _audioTrimPreviewStartTime = DateTime.UtcNow;
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

            WireVoiceAndToneEvents();
            WireEqualizer5BandEvents();
            WireCreativeAudioFxEvents();
            WireDynamicsAndCutoffEvents();
            WireWaveformShaperEvents();
            InitWaveformVisualizerTimer();

            // 8. DSP Live Preview Controls (NAudio Real-time Engine)
            if (ToggleDspAudioPreviewButton != null)
            {
                ToggleDspAudioPreviewButton.IsChecked = _isDspAudioPreviewActive;
                ToggleDspAudioPreviewButton.Checked += (_, _) =>
                {
                    _isDspAudioPreviewActive = true;
                    _realTimeAudioEngine?.SetDspBypass(false);
                    OnDspFilterParameterChanged();
                    AppendLog($"🎧 [DSP ACTIVE] Đã kích hoạt bộ lọc âm thanh ({GetActiveFilterSummary()}).");
                };
                ToggleDspAudioPreviewButton.Unchecked += (_, _) =>
                {
                    _isDspAudioPreviewActive = false;
                    _realTimeAudioEngine?.SetDspBypass(true);
                    UpdateDspStatusText();
                    AppendLog("🔊 [DSP BYPASS] Đã chuyển về nghe âm thanh gốc (Raw Audio).");
                };
            }

            if (QuickPreviewDsp15sButton != null)
            {
                QuickPreviewDsp15sButton.Click += (_, _) =>
                {
                    if (ToggleDspAudioPreviewButton != null)
                        ToggleDspAudioPreviewButton.IsChecked = true;
                    if (!_isPlaying && PreviewMedia != null && PreviewMedia.Source != null)
                    {
                        TogglePlayPause();
                    }
                };
            }

            // 8b. FFplay Hardware Accelerated Direct Player
            if (LaunchFfplayPreviewButton != null)
            {
                LaunchFfplayPreviewButton.Click += (_, _) => LaunchFfplayPreview(audioOnly: false);
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

        private void WireVoiceAndToneEvents()
        {
            if (AudioPitchSlider != null)
            {
                AudioPitchSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioPitchSemitones = e.NewValue;
                    if (AudioPitchLabel != null)
                        AudioPitchLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0} st";
                    if (AudioPitchStatusLabel != null)
                        AudioPitchStatusLabel.Text = FormatPitchStatus(e.NewValue);
                    OnDspFilterParameterChanged();
                };
            }

            if (PresetVoiceMaleDeepButton != null) PresetVoiceMaleDeepButton.Click += (_, _) => ApplyVoiceGenderPreset(-6, "Nam Trầm (-6st)");
            if (PresetVoiceMaleButton != null) PresetVoiceMaleButton.Click += (_, _) => ApplyVoiceGenderPreset(-3, "Nam Chuẩn (-3st)");
            if (PresetVoiceNaturalButton != null) PresetVoiceNaturalButton.Click += (_, _) => ApplyVoiceGenderPreset(0, "Tự Nhiên (0st)");
            if (PresetVoiceFemaleButton != null) PresetVoiceFemaleButton.Click += (_, _) => ApplyVoiceGenderPreset(4, "Nữ Chuẩn (+4st)");
            if (PresetVoiceAnimeButton != null) PresetVoiceAnimeButton.Click += (_, _) => ApplyVoiceGenderPreset(8, "Nữ Cao / Anime (+8st)");
            if (PresetVoiceChipmunkButton != null) PresetVoiceChipmunkButton.Click += (_, _) => ApplyVoiceGenderPreset(12, "Sóc Chuột (+12st)");
            if (PresetVoiceMonsterButton != null) PresetVoiceMonsterButton.Click += (_, _) => ApplyVoiceGenderPreset(-12, "Quái Vật (-12st)");

            if (AudioToneClaritySlider != null)
            {
                AudioToneClaritySlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioToneClarity = e.NewValue;
                    if (AudioToneClarityLabel != null)
                        AudioToneClarityLabel.Text = $"{e.NewValue:0}%";
                    if (AudioToneClarityStatusLabel != null)
                        AudioToneClarityStatusLabel.Text = FormatToneClarityStatus(e.NewValue);
                    OnDspFilterParameterChanged();
                };
            }

            if (PresetToneDeepButton != null) PresetToneDeepButton.Click += (_, _) => ApplyTonePreset(-80, "Rất Trầm (-80%)");
            if (PresetToneWarmButton != null) PresetToneWarmButton.Click += (_, _) => ApplyTonePreset(-40, "Trầm Ấm (-40%)");
            if (PresetToneNaturalButton != null) PresetToneNaturalButton.Click += (_, _) => ApplyTonePreset(0, "Tự Nhiên (0%)");
            if (PresetToneBrightButton != null) PresetToneBrightButton.Click += (_, _) => ApplyTonePreset(50, "Trong Trẻo (+50%)");
            if (PresetToneCrispButton != null) PresetToneCrispButton.Click += (_, _) => ApplyTonePreset(80, "Trong Vắt (+80%)");
        }

        private void WireEqualizer5BandEvents()
        {
            if (AudioBassGainSlider != null)
            {
                AudioBassGainSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioBassGain = e.NewValue;
                    if (AudioBassGainLabel != null)
                        AudioBassGainLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0.#}dB";
                    OnDspFilterParameterChanged();
                };
            }

            if (AudioLowMidGainSlider != null)
            {
                AudioLowMidGainSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioLowMidGain = e.NewValue;
                    if (AudioLowMidGainLabel != null)
                        AudioLowMidGainLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0.#}dB";
                    OnDspFilterParameterChanged();
                };
            }

            if (AudioMidGainSlider != null)
            {
                AudioMidGainSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioMidGain = e.NewValue;
                    if (AudioMidGainLabel != null)
                        AudioMidGainLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0.#}dB";
                    OnDspFilterParameterChanged();
                };
            }

            if (AudioHighMidGainSlider != null)
            {
                AudioHighMidGainSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioHighMidGain = e.NewValue;
                    if (AudioHighMidGainLabel != null)
                        AudioHighMidGainLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0.#}dB";
                    OnDspFilterParameterChanged();
                };
            }

            if (AudioTrebleGainSlider != null)
            {
                AudioTrebleGainSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioTrebleGain = e.NewValue;
                    if (AudioTrebleGainLabel != null)
                        AudioTrebleGainLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0.#}dB";
                    OnDspFilterParameterChanged();
                };
            }
        }

        private void WireCreativeAudioFxEvents()
        {
            if (AudioEchoToggle != null)
            {
                AudioEchoToggle.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioEchoEnabled = true; OnDspFilterParameterChanged(); } };
                AudioEchoToggle.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioEchoEnabled = false; OnDspFilterParameterChanged(); } };
            }
            if (AudioEchoDelaySlider != null)
            {
                AudioEchoDelaySlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioEchoDelayMs = e.NewValue;
                    if (AudioEchoDelayLabel != null) AudioEchoDelayLabel.Text = $"{e.NewValue:0}ms";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioEchoFeedbackSlider != null)
            {
                AudioEchoFeedbackSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioEchoFeedbackPercent = e.NewValue;
                    if (AudioEchoFeedbackLabel != null) AudioEchoFeedbackLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioEchoMixSlider != null)
            {
                AudioEchoMixSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioEchoMixPercent = e.NewValue;
                    if (AudioEchoMixLabel != null) AudioEchoMixLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }

            if (Audio8DToggle != null)
            {
                Audio8DToggle.Checked += (_, _) => { if (!_suppressControlSync) { _node.Audio8DEnabled = true; OnDspFilterParameterChanged(); } };
                Audio8DToggle.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.Audio8DEnabled = false; OnDspFilterParameterChanged(); } };
            }
            if (Audio8DSpeedSlider != null)
            {
                Audio8DSpeedSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.Audio8DSpeedHz = e.NewValue;
                    if (Audio8DSpeedLabel != null) Audio8DSpeedLabel.Text = $"{e.NewValue:0.###} Hz";
                    OnDspFilterParameterChanged();
                };
            }

            if (AudioRobotVoiceCheckBox != null)
            {
                AudioRobotVoiceCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioRobotVoiceEnabled = true; OnDspFilterParameterChanged(); } };
                AudioRobotVoiceCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioRobotVoiceEnabled = false; OnDspFilterParameterChanged(); } };
            }
            if (AudioRadioVoiceCheckBox != null)
            {
                AudioRadioVoiceCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioRadioVoiceEnabled = true; OnDspFilterParameterChanged(); } };
                AudioRadioVoiceCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioRadioVoiceEnabled = false; OnDspFilterParameterChanged(); } };
            }
            if (AudioChorusCheckBox != null)
            {
                AudioChorusCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioChorusEnabled = true; OnDspFilterParameterChanged(); } };
                AudioChorusCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioChorusEnabled = false; OnDspFilterParameterChanged(); } };
            }

            if (AudioStereoWidthSlider != null)
            {
                AudioStereoWidthSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioStereoWidthPercent = e.NewValue;
                    if (AudioStereoWidthLabel != null) AudioStereoWidthLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioWarmthSlider != null)
            {
                AudioWarmthSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioWarmthPercent = e.NewValue;
                    if (AudioWarmthLabel != null) AudioWarmthLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioReverbSlider != null)
            {
                AudioReverbSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioReverbPercent = e.NewValue;
                    if (AudioReverbLabel != null) AudioReverbLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }
            if (PresetReverbRoomButton != null) PresetReverbRoomButton.Click += (_, _) => ApplyReverbPreset(15, "Phòng nhỏ (15%)");
            if (PresetReverbStageButton != null) PresetReverbStageButton.Click += (_, _) => ApplyReverbPreset(35, "Sân khấu (35%)");
            if (PresetReverbHallButton != null) PresetReverbHallButton.Click += (_, _) => ApplyReverbPreset(60, "Nhà hát (60%)");
            if (PresetReverbCaveButton != null) PresetReverbCaveButton.Click += (_, _) => ApplyReverbPreset(85, "Hang động (85%)");

            if (AudioVocalBalanceSlider != null)
            {
                AudioVocalBalanceSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioVocalBalance = e.NewValue;
                    if (AudioVocalBalanceLabel != null)
                    {
                        if (e.NewValue < -1) AudioVocalBalanceLabel.Text = $"Nhạc {e.NewValue:0}%";
                        else if (e.NewValue > 1) AudioVocalBalanceLabel.Text = $"Lời +{e.NewValue:0}%";
                        else AudioVocalBalanceLabel.Text = "Gốc (0%)";
                    }
                    OnDspFilterParameterChanged();
                };
            }
        }

        private void WireDynamicsAndCutoffEvents()
        {
            if (AudioCompressorSlider != null)
            {
                AudioCompressorSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioCompressorPercent = e.NewValue;
                    if (AudioCompressorLabel != null) AudioCompressorLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioDeEsserSlider != null)
            {
                AudioDeEsserSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioDeEsserPercent = e.NewValue;
                    if (AudioDeEsserLabel != null) AudioDeEsserLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioNoiseGateSlider != null)
            {
                AudioNoiseGateSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioNoiseGatePercent = e.NewValue;
                    if (AudioNoiseGateLabel != null) AudioNoiseGateLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioHighpassCutoffSlider != null)
            {
                AudioHighpassCutoffSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioHighpassCutoffHz = e.NewValue;
                    if (AudioHighpassCutoffLabel != null) AudioHighpassCutoffLabel.Text = $"{e.NewValue:0}Hz";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioLowpassCutoffSlider != null)
            {
                AudioLowpassCutoffSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioLowpassCutoffHz = e.NewValue;
                    if (AudioLowpassCutoffLabel != null)
                        AudioLowpassCutoffLabel.Text = e.NewValue >= 1000 ? $"{e.NewValue / 1000.0:0.#}kHz" : $"{e.NewValue:0}Hz";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioFadeInSlider != null)
            {
                AudioFadeInSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioFadeInSec = e.NewValue;
                    if (AudioFadeInLabel != null) AudioFadeInLabel.Text = $"{e.NewValue:0.#}s";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioFadeOutSlider != null)
            {
                AudioFadeOutSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioFadeOutSec = e.NewValue;
                    if (AudioFadeOutLabel != null) AudioFadeOutLabel.Text = $"{e.NewValue:0.#}s";
                    OnDspFilterParameterChanged();
                };
            }
            if (AudioNormalizeCheckBox != null)
            {
                AudioNormalizeCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioNormalizeEnabled = true; OnDspFilterParameterChanged(); } };
                AudioNormalizeCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioNormalizeEnabled = false; OnDspFilterParameterChanged(); } };
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
                        OnDspFilterParameterChanged();
                    }
                };
            }
            if (AudioDenoiseCheckBox != null)
            {
                AudioDenoiseCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioDenoiseEnabled = true; OnDspFilterParameterChanged(); } };
                AudioDenoiseCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioDenoiseEnabled = false; OnDspFilterParameterChanged(); } };
            }
            if (AudioHighpassCheckBox != null)
            {
                AudioHighpassCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioHighpassFilter = true; OnDspFilterParameterChanged(); } };
                AudioHighpassCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioHighpassFilter = false; OnDspFilterParameterChanged(); } };
            }
            if (AudioLowpassCheckBox != null)
            {
                AudioLowpassCheckBox.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioLowpassFilter = true; OnDspFilterParameterChanged(); } };
                AudioLowpassCheckBox.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioLowpassFilter = false; OnDspFilterParameterChanged(); } };
            }
        }

        private void WireWaveformShaperEvents()
        {
            if (AudioWaveShaperToggle != null)
            {
                AudioWaveShaperToggle.Checked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioWaveShaperEnabled = true;
                    if (AudioWaveShaperContainer != null) AudioWaveShaperContainer.Visibility = Visibility.Visible;
                    InitWaveformVisualizerTimer();
                    OnDspFilterParameterChanged();
                };
                AudioWaveShaperToggle.Unchecked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioWaveShaperEnabled = false;
                    if (AudioWaveShaperContainer != null) AudioWaveShaperContainer.Visibility = Visibility.Collapsed;
                    OnDspFilterParameterChanged();
                };
            }

            if (PresetWaveCleanButton != null) PresetWaveCleanButton.Click += (_, _) => ApplyWaveformProfile("clean", 0, "Sin Gốc (Clean)");
            if (PresetWaveTapeButton != null) PresetWaveTapeButton.Click += (_, _) => ApplyWaveformProfile("tape", 40, "Bão hòa Băng (Tape)");
            if (PresetWaveSoftButton != null) PresetWaveSoftButton.Click += (_, _) => ApplyWaveformProfile("soft", 60, "Uốn Sóng Mềm (Soft)");
            if (PresetWaveHardButton != null) PresetWaveHardButton.Click += (_, _) => ApplyWaveformProfile("hard", 80, "Cắt Gọt Sóng (Hard)");
            if (PresetWaveFoldButton != null) PresetWaveFoldButton.Click += (_, _) => ApplyWaveformProfile("fold", 90, "Đa Hài (Wave Fold)");

            if (AudioWaveShaperDriveSlider != null)
            {
                AudioWaveShaperDriveSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioWaveShaperDrivePercent = e.NewValue;
                    if (AudioWaveShaperDriveLabel != null) AudioWaveShaperDriveLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }

            if (AudioTransientPunchSlider != null)
            {
                AudioTransientPunchSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioTransientPunchPercent = e.NewValue;
                    if (AudioTransientPunchLabel != null) AudioTransientPunchLabel.Text = $"{e.NewValue:0}%";
                    if (AudioTransientPunchStatusLabel != null)
                    {
                        if (e.NewValue < -5) AudioTransientPunchStatusLabel.Text = $"Mềm mại ({e.NewValue:0}%)";
                        else if (e.NewValue > 5) AudioTransientPunchStatusLabel.Text = $"Đanh chắc (+{e.NewValue:0}%)";
                        else AudioTransientPunchStatusLabel.Text = "Cân bằng (0%)";
                    }
                    OnDspFilterParameterChanged();
                };
            }

            if (AudioSubHarmonicsSlider != null)
            {
                AudioSubHarmonicsSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioSubHarmonicsPercent = e.NewValue;
                    if (AudioSubHarmonicsLabel != null) AudioSubHarmonicsLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }

            if (AudioHarmonicExciterSlider != null)
            {
                AudioHarmonicExciterSlider.ValueChanged += (_, e) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioHarmonicExciterPercent = e.NewValue;
                    if (AudioHarmonicExciterLabel != null) AudioHarmonicExciterLabel.Text = $"{e.NewValue:0}%";
                    OnDspFilterParameterChanged();
                };
            }

            if (AudioPhaseInvertLToggle != null)
            {
                AudioPhaseInvertLToggle.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioPhaseInvertLeft = true; OnDspFilterParameterChanged(); } };
                AudioPhaseInvertLToggle.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioPhaseInvertLeft = false; OnDspFilterParameterChanged(); } };
            }
            if (AudioPhaseInvertRToggle != null)
            {
                AudioPhaseInvertRToggle.Checked += (_, _) => { if (!_suppressControlSync) { _node.AudioPhaseInvertRight = true; OnDspFilterParameterChanged(); } };
                AudioPhaseInvertRToggle.Unchecked += (_, _) => { if (!_suppressControlSync) { _node.AudioPhaseInvertRight = false; OnDspFilterParameterChanged(); } };
            }
        }

        private void ApplyWaveformProfile(string curve, double drive, string title)
        {
            _node.AudioWaveShaperCurve = curve;
            _node.AudioWaveShaperDrivePercent = drive;
            SyncAudioTabFromModel();
            AppendLog($"🌊 [SÓNG ÂM] Đã áp dụng mẫu định hình sóng: {title}.");
            TriggerDspRegenIfActive();
        }

        private void InitWaveformVisualizerTimer()
        {
            if (_waveVisTimer != null) return;
            _waveVisTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
            };
            _waveVisTimer.Tick += WaveVisTimer_Tick;
            _waveVisTimer.Start();
        }

        private void WaveVisTimer_Tick(object? sender, EventArgs e)
        {
            if (AudioWaveShaperToggle?.IsChecked != true || AudioWaveformCanvas == null)
                return;

            RenderWaveformFrame();
        }

        private void RenderWaveformFrame()
        {
            if (AudioWaveformCanvas == null) return;

            var canvasW = AudioWaveformCanvas.ActualWidth > 10 ? AudioWaveformCanvas.ActualWidth : 300;
            var canvasH = AudioWaveformCanvas.ActualHeight > 10 ? AudioWaveformCanvas.ActualHeight : 100;
            var midY = canvasH * 0.5;

            var rmsL = 0f;
            var rmsR = 0f;
            var peak = 0f;

            if (_realTimeAudioEngine != null && _isPlaying)
            {
                _realTimeAudioEngine.GetLatestWaveformData(_waveVisBuffer, out rmsL, out rmsR, out peak);
            }
            else
            {
                // Idle resting wave
                Array.Clear(_waveVisBuffer, 0, _waveVisBuffer.Length);
            }

            // Draw polyline
            if (_wavePolyline == null || !AudioWaveformCanvas.Children.Contains(_wavePolyline))
            {
                _wavePolyline = new System.Windows.Shapes.Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
                    StrokeThickness = 1.5
                };
                AudioWaveformCanvas.Children.Clear();
                AudioWaveformCanvas.Children.Add(_wavePolyline);
            }

            var points = new PointCollection(_waveVisBuffer.Length);
            var stepX = canvasW / Math.Max(1, _waveVisBuffer.Length - 1);
            var ampScale = (canvasH * 0.45);

            for (var i = 0; i < _waveVisBuffer.Length; i++)
            {
                var s = _waveVisBuffer[i];
                var x = i * stepX;
                var y = midY - (s * ampScale);
                points.Add(new Point(x, Math.Clamp(y, 2, canvasH - 2)));
            }

            _wavePolyline.Points = points;

            // Update VU meters
            if (AudioVuMeterBarL != null)
                AudioVuMeterBarL.Height = Math.Clamp(rmsL * canvasH * 1.5, 0, canvasH);
            if (AudioVuMeterBarR != null)
                AudioVuMeterBarR.Height = Math.Clamp(rmsR * canvasH * 1.5, 0, canvasH);

            // Update stats
            if (AudioWaveformRmsLabel != null)
                AudioWaveformRmsLabel.Text = rmsL > 0.001f ? $"{(20.0 * Math.Log10(rmsL)):0.#} dB" : "-inf dB";
            if (AudioWaveformPeakLabel != null)
                AudioWaveformPeakLabel.Text = peak > 0.001f ? $"{(20.0 * Math.Log10(peak)):0.#} dB" : "-inf dB";
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
                _realTimeAudioEngine?.Pause();
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

        public async Task EnsureRealTimeAudioEngineLoadedAsync()
        {
            if (string.IsNullOrWhiteSpace(_node.VideoPath) || !File.Exists(_node.VideoPath)) return;
            _realTimeAudioEngine ??= new RealTimeVideoAudioEngine();
            if (_realTimeAudioEngine.CurrentFilePath != _node.VideoPath)
            {
                var dur = GetNaturalDurationSeconds();
                await _realTimeAudioEngine.LoadMediaAsync(_node.VideoPath, dur);
            }
            _realTimeAudioEngine?.ApplyParameters(_node, _node.PreviewVolume, _isDspAudioPreviewActive);
        }

        public void EnsureRealTimeAudioEngineLoaded()
        {
            _ = EnsureRealTimeAudioEngineLoadedAsync();
        }

        private void OnDspFilterParameterChanged()
        {
            EnsureRealTimeAudioEngineLoaded();
            _realTimeAudioEngine?.ApplyParameters(_node, _node.PreviewVolume, _isDspAudioPreviewActive);
            UpdateDspStatusText();
        }

        private void TriggerDspRegenIfActive() => OnDspFilterParameterChanged();

        private void UpdateDspStatusText()
        {
            if (AudioDspStatusText == null) return;
            if (_isDspAudioPreviewActive)
            {
                AudioDspStatusText.Text = $"🎧 Live DSP: {GetActiveFilterSummary()}";
                AudioDspStatusText.Foreground = (Brush)FindResource("ThemeAccentBrush");
            }
            else
            {
                AudioDspStatusText.Text = "🔊 Đang nghe audio gốc";
                AudioDspStatusText.Foreground = (Brush)FindResource("ThemeTextSecondaryBrush");
            }
        }

        private void CommitTrimSeekPosition(double sec)
        {
            if (PreviewMedia != null && PreviewMedia.Source != null && !_isPlaying)
            {
                PreviewMedia.Position = TimeSpan.FromSeconds(sec);
                UpdatePlaybackUi();
            }
            _realTimeAudioEngine?.Seek(TimeSpan.FromSeconds(sec));
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

        private string GetActiveFilterSummary()
        {
            var parts = new List<string>();
            if (Math.Abs(_node.AudioPitchSemitones) > 0.1) parts.Add($"Pitch {(_node.AudioPitchSemitones > 0 ? "+" : "")}{_node.AudioPitchSemitones:0}st");
            if (Math.Abs(_node.AudioToneClarity) > 1.0) parts.Add($"Tone {(_node.AudioToneClarity > 0 ? "+" : "")}{_node.AudioToneClarity:0}%");
            if (_node.AudioBassGain != 0) parts.Add($"Bass {(_node.AudioBassGain > 0 ? "+" : "")}{_node.AudioBassGain:0.#}dB");
            if (_node.AudioLowMidGain != 0) parts.Add($"L-Mid {(_node.AudioLowMidGain > 0 ? "+" : "")}{_node.AudioLowMidGain:0.#}dB");
            if (_node.AudioMidGain != 0) parts.Add($"Mid {(_node.AudioMidGain > 0 ? "+" : "")}{_node.AudioMidGain:0.#}dB");
            if (_node.AudioHighMidGain != 0) parts.Add($"H-Mid {(_node.AudioHighMidGain > 0 ? "+" : "")}{_node.AudioHighMidGain:0.#}dB");
            if (_node.AudioTrebleGain != 0) parts.Add($"Treble {(_node.AudioTrebleGain > 0 ? "+" : "")}{_node.AudioTrebleGain:0.#}dB");
            if (_node.AudioEchoEnabled) parts.Add($"Echo {_node.AudioEchoDelayMs:0}ms");
            if (_node.Audio8DEnabled) parts.Add($"8D {_node.Audio8DSpeedHz:0.###}Hz");
            if (_node.AudioRobotVoiceEnabled) parts.Add("Robot");
            if (_node.AudioRadioVoiceEnabled) parts.Add("Radio");
            if (_node.AudioChorusEnabled) parts.Add("Chorus");
            if (Math.Abs(_node.AudioStereoWidthPercent - 100.0) > 1.0) parts.Add($"Stereo {_node.AudioStereoWidthPercent:0}%");
            if (_node.AudioWarmthPercent > 0) parts.Add($"Warmth {_node.AudioWarmthPercent:0}%");
            if (_node.AudioReverbPercent > 0) parts.Add($"Reverb {_node.AudioReverbPercent:0}%");
            if (_node.AudioVocalBalance < -1) parts.Add($"Karaoke {_node.AudioVocalBalance:0}%");
            if (_node.AudioVocalBalance > 1) parts.Add($"Vocal +{_node.AudioVocalBalance:0}%");
            if (_node.AudioCompressorPercent > 0) parts.Add($"Comp {_node.AudioCompressorPercent:0}%");
            if (_node.AudioDeEsserPercent > 0) parts.Add($"De-Ess {_node.AudioDeEsserPercent:0}%");
            if (_node.AudioNoiseGatePercent > 0) parts.Add($"Gate {_node.AudioNoiseGatePercent:0}%");
            if (_node.AudioDenoiseEnabled) parts.Add("Denoise");
            if (_node.AudioHighpassFilter) parts.Add($"HP {_node.AudioHighpassCutoffHz:0}Hz");
            if (_node.AudioLowpassFilter) parts.Add($"LP {(_node.AudioLowpassCutoffHz >= 1000 ? _node.AudioLowpassCutoffHz / 1000.0 : _node.AudioLowpassCutoffHz):0.#}kHz");
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
                if (totalDuration > 0.05)
                {
                    ClampAndSyncTrimRangesToVideoDuration(totalDuration);
                }
                var maxSec = totalDuration > 0 ? totalDuration : 100;
                if (AudioTrimStartSlider != null)
                {
                    AudioTrimStartSlider.Maximum = maxSec;
                    AudioTrimStartSlider.Value = _node.AudioTrimStartSec;
                }
                if (AudioTrimEndSlider != null)
                {
                    AudioTrimEndSlider.Maximum = maxSec;
                    AudioTrimEndSlider.Value = _node.AudioTrimEndSec;
                }

                if (AudioTrimStartBox != null) AudioTrimStartBox.Text = FormatTimeSec(_node.AudioTrimStartSec);
                if (AudioTrimEndBox != null) AudioTrimEndBox.Text = FormatTimeSec(_node.AudioTrimEndSec);
                if (AudioTrimStartSecLabel != null) AudioTrimStartSecLabel.Text = $"{_node.AudioTrimStartSec:0.##}s";
                if (AudioTrimEndSecLabel != null) AudioTrimEndSecLabel.Text = $"{_node.AudioTrimEndSec:0.##}s";
                UpdateAudioTrimDurationLabel();

                if (AudioPitchSlider != null)
                {
                    AudioPitchSlider.Value = _node.AudioPitchSemitones;
                    if (AudioPitchLabel != null) AudioPitchLabel.Text = $"{(_node.AudioPitchSemitones >= 0 ? "+" : "")}{_node.AudioPitchSemitones:0} st";
                    if (AudioPitchStatusLabel != null) AudioPitchStatusLabel.Text = FormatPitchStatus(_node.AudioPitchSemitones);
                }
                if (AudioToneClaritySlider != null)
                {
                    AudioToneClaritySlider.Value = _node.AudioToneClarity;
                    if (AudioToneClarityLabel != null) AudioToneClarityLabel.Text = $"{_node.AudioToneClarity:0}%";
                    if (AudioToneClarityStatusLabel != null) AudioToneClarityStatusLabel.Text = FormatToneClarityStatus(_node.AudioToneClarity);
                }
                if (AudioBassGainSlider != null)
                {
                    AudioBassGainSlider.Value = _node.AudioBassGain;
                    if (AudioBassGainLabel != null) AudioBassGainLabel.Text = $"{(_node.AudioBassGain >= 0 ? "+" : "")}{_node.AudioBassGain:0.#}dB";
                }
                if (AudioLowMidGainSlider != null)
                {
                    AudioLowMidGainSlider.Value = _node.AudioLowMidGain;
                    if (AudioLowMidGainLabel != null) AudioLowMidGainLabel.Text = $"{(_node.AudioLowMidGain >= 0 ? "+" : "")}{_node.AudioLowMidGain:0.#}dB";
                }
                if (AudioMidGainSlider != null)
                {
                    AudioMidGainSlider.Value = _node.AudioMidGain;
                    if (AudioMidGainLabel != null) AudioMidGainLabel.Text = $"{(_node.AudioMidGain >= 0 ? "+" : "")}{_node.AudioMidGain:0.#}dB";
                }
                if (AudioHighMidGainSlider != null)
                {
                    AudioHighMidGainSlider.Value = _node.AudioHighMidGain;
                    if (AudioHighMidGainLabel != null) AudioHighMidGainLabel.Text = $"{(_node.AudioHighMidGain >= 0 ? "+" : "")}{_node.AudioHighMidGain:0.#}dB";
                }
                if (AudioTrebleGainSlider != null)
                {
                    AudioTrebleGainSlider.Value = _node.AudioTrebleGain;
                    if (AudioTrebleGainLabel != null) AudioTrebleGainLabel.Text = $"{(_node.AudioTrebleGain >= 0 ? "+" : "")}{_node.AudioTrebleGain:0.#}dB";
                }
                if (AudioEchoToggle != null) AudioEchoToggle.IsChecked = _node.AudioEchoEnabled;
                if (AudioEchoDelaySlider != null)
                {
                    AudioEchoDelaySlider.Value = _node.AudioEchoDelayMs;
                    if (AudioEchoDelayLabel != null) AudioEchoDelayLabel.Text = $"{_node.AudioEchoDelayMs:0}ms";
                }
                if (AudioEchoFeedbackSlider != null)
                {
                    AudioEchoFeedbackSlider.Value = _node.AudioEchoFeedbackPercent;
                    if (AudioEchoFeedbackLabel != null) AudioEchoFeedbackLabel.Text = $"{_node.AudioEchoFeedbackPercent:0}%";
                }
                if (AudioEchoMixSlider != null)
                {
                    AudioEchoMixSlider.Value = _node.AudioEchoMixPercent;
                    if (AudioEchoMixLabel != null) AudioEchoMixLabel.Text = $"{_node.AudioEchoMixPercent:0}%";
                }
                if (Audio8DToggle != null) Audio8DToggle.IsChecked = _node.Audio8DEnabled;
                if (Audio8DSpeedSlider != null)
                {
                    Audio8DSpeedSlider.Value = _node.Audio8DSpeedHz;
                    if (Audio8DSpeedLabel != null) Audio8DSpeedLabel.Text = $"{_node.Audio8DSpeedHz:0.###} Hz";
                }
                if (AudioRobotVoiceCheckBox != null) AudioRobotVoiceCheckBox.IsChecked = _node.AudioRobotVoiceEnabled;
                if (AudioRadioVoiceCheckBox != null) AudioRadioVoiceCheckBox.IsChecked = _node.AudioRadioVoiceEnabled;
                if (AudioChorusCheckBox != null) AudioChorusCheckBox.IsChecked = _node.AudioChorusEnabled;
                if (AudioStereoWidthSlider != null)
                {
                    AudioStereoWidthSlider.Value = _node.AudioStereoWidthPercent;
                    if (AudioStereoWidthLabel != null) AudioStereoWidthLabel.Text = $"{_node.AudioStereoWidthPercent:0}%";
                }
                if (AudioWarmthSlider != null)
                {
                    AudioWarmthSlider.Value = _node.AudioWarmthPercent;
                    if (AudioWarmthLabel != null) AudioWarmthLabel.Text = $"{_node.AudioWarmthPercent:0}%";
                }
                if (AudioReverbSlider != null)
                {
                    AudioReverbSlider.Value = _node.AudioReverbPercent;
                    if (AudioReverbLabel != null) AudioReverbLabel.Text = $"{_node.AudioReverbPercent:0}%";
                }
                if (AudioVocalBalanceSlider != null)
                {
                    AudioVocalBalanceSlider.Value = _node.AudioVocalBalance;
                    if (AudioVocalBalanceLabel != null)
                    {
                        if (_node.AudioVocalBalance < -1)
                            AudioVocalBalanceLabel.Text = $"Nhạc {_node.AudioVocalBalance:0}%";
                        else if (_node.AudioVocalBalance > 1)
                            AudioVocalBalanceLabel.Text = $"Lời +{_node.AudioVocalBalance:0}%";
                        else
                            AudioVocalBalanceLabel.Text = "Gốc (0%)";
                    }
                }
                if (AudioCompressorSlider != null)
                {
                    AudioCompressorSlider.Value = _node.AudioCompressorPercent;
                    if (AudioCompressorLabel != null) AudioCompressorLabel.Text = $"{_node.AudioCompressorPercent:0}%";
                }
                if (AudioDeEsserSlider != null)
                {
                    AudioDeEsserSlider.Value = _node.AudioDeEsserPercent;
                    if (AudioDeEsserLabel != null) AudioDeEsserLabel.Text = $"{_node.AudioDeEsserPercent:0}%";
                }
                if (AudioNoiseGateSlider != null)
                {
                    AudioNoiseGateSlider.Value = _node.AudioNoiseGatePercent;
                    if (AudioNoiseGateLabel != null) AudioNoiseGateLabel.Text = $"{_node.AudioNoiseGatePercent:0}%";
                }
                if (AudioHighpassCutoffSlider != null)
                {
                    AudioHighpassCutoffSlider.Value = _node.AudioHighpassCutoffHz;
                    if (AudioHighpassCutoffLabel != null) AudioHighpassCutoffLabel.Text = $"{_node.AudioHighpassCutoffHz:0}Hz";
                }
                if (AudioLowpassCutoffSlider != null)
                {
                    AudioLowpassCutoffSlider.Value = _node.AudioLowpassCutoffHz;
                    if (AudioLowpassCutoffLabel != null)
                        AudioLowpassCutoffLabel.Text = _node.AudioLowpassCutoffHz >= 1000 ? $"{_node.AudioLowpassCutoffHz / 1000.0:0.#}kHz" : $"{_node.AudioLowpassCutoffHz:0}Hz";
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

                // Wave Shaper & Visualizer Sync
                if (AudioWaveShaperToggle != null)
                {
                    AudioWaveShaperToggle.IsChecked = _node.AudioWaveShaperEnabled;
                    if (AudioWaveShaperContainer != null)
                        AudioWaveShaperContainer.Visibility = _node.AudioWaveShaperEnabled ? Visibility.Visible : Visibility.Collapsed;
                }
                if (AudioWaveShaperDriveSlider != null)
                {
                    AudioWaveShaperDriveSlider.Value = _node.AudioWaveShaperDrivePercent;
                    if (AudioWaveShaperDriveLabel != null) AudioWaveShaperDriveLabel.Text = $"{_node.AudioWaveShaperDrivePercent:0}%";
                }
                if (AudioWaveShaperCurveStatusLabel != null)
                {
                    AudioWaveShaperCurveStatusLabel.Text = _node.AudioWaveShaperCurve switch
                    {
                        "tape" => "Bão hòa Băng (Tape)",
                        "soft" => "Uốn Sóng Mềm (Soft)",
                        "hard" => "Cắt Gọt Sóng (Hard)",
                        "fold" => "Đa Hài (Wave Fold)",
                        _ => "Sin Gốc (Clean)"
                    };
                }
                if (AudioTransientPunchSlider != null)
                {
                    AudioTransientPunchSlider.Value = _node.AudioTransientPunchPercent;
                    if (AudioTransientPunchLabel != null) AudioTransientPunchLabel.Text = $"{_node.AudioTransientPunchPercent:0}%";
                    if (AudioTransientPunchStatusLabel != null)
                    {
                        if (_node.AudioTransientPunchPercent < -5) AudioTransientPunchStatusLabel.Text = $"Mềm mại ({_node.AudioTransientPunchPercent:0}%)";
                        else if (_node.AudioTransientPunchPercent > 5) AudioTransientPunchStatusLabel.Text = $"Đanh chắc (+{_node.AudioTransientPunchPercent:0}%)";
                        else AudioTransientPunchStatusLabel.Text = "Cân bằng (0%)";
                    }
                }
                if (AudioSubHarmonicsSlider != null)
                {
                    AudioSubHarmonicsSlider.Value = _node.AudioSubHarmonicsPercent;
                    if (AudioSubHarmonicsLabel != null) AudioSubHarmonicsLabel.Text = $"{_node.AudioSubHarmonicsPercent:0}%";
                }
                if (AudioHarmonicExciterSlider != null)
                {
                    AudioHarmonicExciterSlider.Value = _node.AudioHarmonicExciterPercent;
                    if (AudioHarmonicExciterLabel != null) AudioHarmonicExciterLabel.Text = $"{_node.AudioHarmonicExciterPercent:0}%";
                }
                if (AudioPhaseInvertLToggle != null) AudioPhaseInvertLToggle.IsChecked = _node.AudioPhaseInvertLeft;
                if (AudioPhaseInvertRToggle != null) AudioPhaseInvertRToggle.IsChecked = _node.AudioPhaseInvertRight;

                if (ToggleDspAudioPreviewButton != null) ToggleDspAudioPreviewButton.IsChecked = _isDspAudioPreviewActive;
                UpdateDspStatusText();

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

        private static string FormatPitchStatus(double semitones)
        {
            if (semitones <= -10) return $"👹 Quái vật ({semitones:0} st)";
            if (semitones <= -5) return $"👨 Nam trầm ({semitones:0} st)";
            if (semitones < 0) return $"👨‍💼 Nam chuẩn ({semitones:0} st)";
            if (Math.Abs(semitones) < 0.1) return "🧑 Tự nhiên (0 st)";
            if (semitones <= 5) return $"👩 Nữ chuẩn (+{semitones:0} st)";
            if (semitones <= 9) return $"👧 Nữ cao / Anime (+{semitones:0} st)";
            return $"🐿️ Sóc chuột (+{semitones:0} st)";
        }

        private static string FormatToneClarityStatus(double tone)
        {
            if (tone <= -60) return $"📻 Rất Trầm ({tone:0}%)";
            if (tone < -5) return $"☕ Trầm Ấm ({tone:0}%)";
            if (Math.Abs(tone) <= 5) return "⚖️ Tự Nhiên (0%)";
            if (tone <= 60) return $"✨ Trong Trẻo (+{tone:0}%)";
            return $"🎙️ Trong Vắt (+{tone:0}%)";
        }

        private void ApplyVoiceGenderPreset(double semitones, string title)
        {
            _node.AudioPitchSemitones = semitones;
            SyncAudioTabFromModel();
            AppendLog($"🎙️ [ĐỔI GIỌNG] Đã áp dụng preset: {title}.");
            TriggerDspRegenIfActive();
        }

        private void ApplyTonePreset(double toneClarity, string title)
        {
            _node.AudioToneClarity = toneClarity;
            SyncAudioTabFromModel();
            AppendLog($"✨ [ÂM SẮC] Đã áp dụng preset: {title}.");
            TriggerDspRegenIfActive();
        }

        private void ApplyReverbPreset(double reverbPercent, string title)
        {
            _node.AudioReverbPercent = reverbPercent;
            SyncAudioTabFromModel();
            AppendLog($"🏛️ [VANG REVERB] Đã áp dụng preset: {title}.");
            TriggerDspRegenIfActive();
        }

        private void ApplyAudioEqPreset(string preset)
        {
            _node.AudioEqPreset = preset;
            switch (preset.ToLowerInvariant())
            {
                case "neutral":
                    _node.AudioBassGain = 0;
                    _node.AudioLowMidGain = 0;
                    _node.AudioMidGain = 0;
                    _node.AudioHighMidGain = 0;
                    _node.AudioTrebleGain = 0;
                    _node.AudioToneClarity = 0;
                    _node.AudioStereoWidthPercent = 100;
                    _node.AudioWarmthPercent = 0;
                    _node.AudioReverbPercent = 0;
                    _node.AudioPitchSemitones = 0;
                    _node.AudioVocalBalance = 0;
                    _node.AudioEchoEnabled = false;
                    _node.Audio8DEnabled = false;
                    _node.AudioRobotVoiceEnabled = false;
                    _node.AudioRadioVoiceEnabled = false;
                    _node.AudioChorusEnabled = false;
                    _node.AudioCompressorPercent = 0;
                    _node.AudioDeEsserPercent = 0;
                    _node.AudioNoiseGatePercent = 0;
                    _node.AudioHighpassFilter = false;
                    _node.AudioLowpassFilter = false;
                    _node.AudioNormalizeEnabled = false;
                    _node.AudioDenoiseEnabled = false;
                    break;
                case "vocal":
                    _node.AudioBassGain = -2;
                    _node.AudioLowMidGain = -1;
                    _node.AudioMidGain = 3;
                    _node.AudioHighMidGain = 3.5;
                    _node.AudioTrebleGain = 4;
                    _node.AudioToneClarity = 40;
                    _node.AudioStereoWidthPercent = 105;
                    _node.AudioWarmthPercent = 10;
                    _node.AudioVocalBalance = 25;
                    _node.AudioCompressorPercent = 35;
                    _node.AudioDeEsserPercent = 30;
                    _node.AudioNoiseGatePercent = 20;
                    _node.AudioHighpassFilter = true;
                    _node.AudioHighpassCutoffHz = 90;
                    _node.AudioLowpassFilter = false;
                    _node.AudioDenoiseEnabled = true;
                    break;
                case "bass":
                    _node.AudioBassGain = 7;
                    _node.AudioLowMidGain = 3;
                    _node.AudioMidGain = 0;
                    _node.AudioHighMidGain = 0;
                    _node.AudioTrebleGain = 1;
                    _node.AudioToneClarity = -30;
                    _node.AudioStereoWidthPercent = 120;
                    _node.AudioWarmthPercent = 20;
                    _node.AudioCompressorPercent = 20;
                    _node.AudioHighpassFilter = false;
                    _node.AudioLowpassFilter = false;
                    break;
                case "treble":
                    _node.AudioBassGain = -1;
                    _node.AudioLowMidGain = -2;
                    _node.AudioMidGain = 1;
                    _node.AudioHighMidGain = 4;
                    _node.AudioTrebleGain = 6;
                    _node.AudioToneClarity = 60;
                    _node.AudioStereoWidthPercent = 115;
                    _node.AudioDeEsserPercent = 25;
                    _node.AudioHighpassFilter = false;
                    _node.AudioLowpassFilter = false;
                    break;
                case "podcast":
                    _node.AudioBassGain = -2;
                    _node.AudioLowMidGain = 1;
                    _node.AudioMidGain = 2.5;
                    _node.AudioHighMidGain = 3;
                    _node.AudioTrebleGain = 3;
                    _node.AudioToneClarity = 30;
                    _node.AudioStereoWidthPercent = 100;
                    _node.AudioWarmthPercent = 15;
                    _node.AudioReverbPercent = 5;
                    _node.AudioVocalBalance = 20;
                    _node.AudioCompressorPercent = 45;
                    _node.AudioDeEsserPercent = 35;
                    _node.AudioNoiseGatePercent = 30;
                    _node.AudioHighpassFilter = true;
                    _node.AudioHighpassCutoffHz = 80;
                    _node.AudioLowpassFilter = true;
                    _node.AudioLowpassCutoffHz = 14000;
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
