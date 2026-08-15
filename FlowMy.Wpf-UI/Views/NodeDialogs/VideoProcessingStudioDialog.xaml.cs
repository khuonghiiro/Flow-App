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
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Effects;
using FlowMy.Extensions;
using FlowMy.Helpers;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow.Audio;
using FlowMy.Services.Workflow.NodeExecutors;
using Microsoft.Win32;

namespace FlowMy.Views.NodeDialogs
{
    public partial class VideoProcessingStudioDialog : Window
    {
        private readonly VideoProcessingNode _node;
        private readonly IWorkflowEditorHost? _host;
        private readonly DispatcherTimer _playbackTimer;
        private MediaPlayer? _dspAudioPlayer;
        private VideoEqEffect? _videoEqEffect;
        private CancellationTokenSource? _dspCts;
        private Process? _activeFfplayProcess;
        private string? _lastDspAudioPath;
        private bool _isPlaying;
        private bool _isScrubbing;
        private bool _suppressSync;
        private bool _isLightTheme;
        private string _selectedPreset = "neutral";
        private TimeSpan _naturalDuration = TimeSpan.Zero;

        public VideoProcessingStudioDialog(VideoProcessingNode node, IWorkflowEditorHost? host, Window? owner, bool isLightTheme = false)
        {
            InitializeComponent();
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host;
            _isLightTheme = isLightTheme;
            Owner = owner;

            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;

            Loaded += VideoProcessingStudioDialog_Loaded;
            Closing += VideoProcessingStudioDialog_Closing;
        }

        private void VideoProcessingStudioDialog_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLocalTheme();
            LoadDataFromNode();
            LoadVideoMedia();
            ApplyStudioVisualEffects();
        }

        private void VideoProcessingStudioDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _playbackTimer.Stop();
            StudioPreviewMedia.Stop();
            StudioPreviewMedia.Close();

            try
            {
                if (_activeFfplayProcess != null && !_activeFfplayProcess.HasExited)
                {
                    _activeFfplayProcess.Kill();
                    _activeFfplayProcess.Dispose();
                    _activeFfplayProcess = null;
                }
            }
            catch { }

            try
            {
                _dspAudioPlayer?.Stop();
                _dspAudioPlayer?.Close();
                if (!string.IsNullOrEmpty(_lastDspAudioPath) && File.Exists(_lastDspAudioPath))
                    File.Delete(_lastDspAudioPath);
            }
            catch { }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        public void ApplyLocalTheme()
        {
            var isLight = _isLightTheme;
            var shellBg = isLight ? Color.FromRgb(235, 240, 248) : Color.FromRgb(15, 15, 23);

            Color accentColor = Color.FromRgb(124, 107, 248);
            if (Application.Current?.TryFindResource("PrimaryBrush") is SolidColorBrush appPrimary && appPrimary.Color.A > 0)
                accentColor = appPrimary.Color;

            Color cardTop = isLight ? Color.FromRgb(255, 255, 255) : Color.FromArgb(26, 255, 255, 255);
            Color innerTop = isLight ? Color.FromRgb(244, 247, 253) : Color.FromArgb(24, 0, 0, 0);

            Color primaryText = isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(236, 236, 244);
            Color secondaryText = isLight ? Color.FromRgb(51, 65, 85) : Color.FromRgb(156, 163, 184);

            Resources["ThemeTextPrimaryBrush"] = new SolidColorBrush(primaryText);
            Resources["ThemeTextSecondaryBrush"] = new SolidColorBrush(secondaryText);
            Resources["ThemeCardBackgroundBrush"] = new SolidColorBrush(cardTop);
            Resources["ThemeCardBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(196, 207, 224) : Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInnerCardBackgroundBrush"] = new SolidColorBrush(innerTop);
            Resources["ThemeInnerCardBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(204, 215, 232) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(255, 255, 255) : Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(168, 184, 208) : Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputForegroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(241, 245, 255));
            Resources["ThemeOverlayBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xF2, 238, 243, 252) : Color.FromArgb(0xAA, 0x00, 0x00, 0x00));
            Resources["ThemeOverlayBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(176, 192, 216) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTimelinePanelBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(226, 234, 246) : Color.FromArgb(0xEE, 0x0A, 0x0A, 0x18));
            Resources["ThemeTrackBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(148, 163, 184) : Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTimelineTrackBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(148, 163, 184) : Color.FromRgb(52, 54, 66));
            Resources["ThemeTimelineProgressBrush"] = new SolidColorBrush(accentColor);
            Resources["ThemeTimelineThumbStrokeBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(30, 41, 59) : Color.FromRgb(226, 232, 245));
            Resources["ThemeAccentGlowColor"] = accentColor;
            Resources["ThemeAccentBrush"] = new SolidColorBrush(accentColor);

            Resources["ThemePanelHeaderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(222, 230, 244) : Color.FromRgb(32, 35, 48));
            Resources["ThemePanelHeaderBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(172, 188, 214) : Color.FromRgb(53, 58, 77));
            Resources["ThemeVideoViewportBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(200, 210, 228) : Color.FromRgb(12, 15, 23));

            Color warmAmber = Color.FromRgb(0xF5, 0x9E, 0x0B);
            Resources["ThemeWarmAccentBrush"] = new SolidColorBrush(warmAmber);
            Resources["ThemeWarmAccentBrushSoft"] = new SolidColorBrush(Color.FromArgb(0x66, warmAmber.R, warmAmber.G, warmAmber.B));
            Resources["ThemeBottomBarGroupInactiveBorderBrush"] = new SolidColorBrush(
                isLight ? Color.FromRgb(164, 180, 206) : Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF));
            Resources["ThemeBottomBarActiveGroupBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(isLight ? (byte)0x35 : (byte)0x2A, warmAmber.R, warmAmber.G, warmAmber.B));

            Color chromePrimaryBg = isLight ? Color.FromRgb(79, 70, 229) : Color.FromRgb(48, 50, 64);
            Color chromePrimaryHover = isLight ? Color.FromRgb(67, 56, 202) : Color.FromRgb(58, 61, 78);
            Color chromeSecondaryBg = isLight ? Color.FromRgb(226, 232, 244) : Color.FromRgb(40, 42, 54);
            Color chromeSecondaryHover = isLight ? Color.FromRgb(210, 220, 238) : Color.FromRgb(50, 52, 68);
            Resources["ThemeVideoChromePrimaryBgBrush"] = new SolidColorBrush(chromePrimaryBg);
            Resources["ThemeVideoChromePrimaryHoverBgBrush"] = new SolidColorBrush(chromePrimaryHover);
            Resources["ThemeVideoChromePrimaryFgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeVideoChromePrimaryBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(164, 180, 206) : Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF));
            Resources["ThemeVideoChromeSecondaryBgBrush"] = new SolidColorBrush(chromeSecondaryBg);
            Resources["ThemeVideoChromeSecondaryHoverBgBrush"] = new SolidColorBrush(chromeSecondaryHover);
            Resources["ThemeVideoChromeSecondaryFgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(232, 236, 248));
            Resources["ThemeVideoChromeSecondaryBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(160, 176, 202) : Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));
            Resources["ThemePresetChipBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(226, 234, 246) : Color.FromRgb(36, 37, 48));
            Resources["ThemePresetChipBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(164, 180, 206) : Color.FromRgb(58, 60, 76));
            Resources["ThemePresetChipHoverBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(208, 218, 238) : Color.FromRgb(48, 50, 66));
            Resources["ThemePresetChipPressedBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(192, 204, 228) : Color.FromRgb(44, 46, 60));
            Resources["ThemePresetChipResetBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(216, 224, 238) : Color.FromRgb(40, 44, 56));
            Resources["ThemePresetChipResetBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(148, 164, 192) : Color.FromRgb(70, 74, 90));
            Color transportPlayBg = isLight ? Color.FromRgb(79, 70, 229) : Color.FromRgb(99, 102, 241);
            Color transportPlayHoverBg = isLight ? Color.FromRgb(67, 56, 202) : Color.FromRgb(79, 82, 220);
            Resources["ThemeTransportPlayBgBrush"] = new SolidColorBrush(transportPlayBg);
            Resources["ThemeTransportPlayHoverBgBrush"] = new SolidColorBrush(transportPlayHoverBg);
            Resources["ThemeTransportPlayFgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeTransportIconHoverBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(204, 214, 234) : Color.FromRgb(48, 50, 64));
            Resources["ThemeQuickOverlayHoverBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(208, 218, 236) : Color.FromRgb(52, 54, 70));
            Resources["ThemeVideoOpenButtonFgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeValueBadgeBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(212, 222, 238) : Color.FromRgb(42, 43, 56));

            Color sliderRedActive = isLight ? Color.FromRgb(220, 38, 38) : Color.FromRgb(239, 68, 68);
            Color sliderRedDisabled = isLight ? Color.FromRgb(248, 113, 113) : Color.FromRgb(252, 165, 165);
            Resources["ThemeSliderActiveBrush"] = new SolidColorBrush(sliderRedActive);
            Resources["ThemeSliderThumbBrush"] = new SolidColorBrush(sliderRedActive);
            Resources["ThemeSliderThumbStrokeBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeSliderDisabledBrush"] = new SolidColorBrush(sliderRedDisabled);

            Resources["ThemeOnAccentTextBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeComboPopupBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(255, 255, 255) : Color.FromRgb(30, 30, 48));
            Resources["ThemeComboItemHoverBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(214, 226, 246) : Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTabHoverBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(210, 222, 242) : Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTabSelectedBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(99, 102, 241) : Color.FromArgb(200, accentColor.R, accentColor.G, accentColor.B));
            Resources["ThemeComboSelectedItemBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(224, 231, 255) : Color.FromArgb(90, accentColor.R, accentColor.G, accentColor.B));

            if (StudioThemeIcon != null)
            {
                var iconKey = isLight ? "moon regular" : "sun-bright duotone-thin";
                var iconConverter = new IconKeyToPathConverter();
                if (iconConverter.Convert(string.Empty, typeof(Uri), iconKey, CultureInfo.CurrentCulture) is Uri iconUri)
                {
                    StudioThemeIcon.Source = iconUri;
                    StudioThemeIcon.Fill = isLight ? new SolidColorBrush(Color.FromRgb(56, 63, 74)) : new SolidColorBrush(Color.FromRgb(255, 219, 116));
                }
            }
        }

        private void LoadDataFromNode()
        {
            _suppressSync = true;
            try
            {
                StudioVideoPathText.Text = !string.IsNullOrWhiteSpace(_node.VideoPath) ? _node.VideoPath : "Chưa chọn video";
                _selectedPreset = !string.IsNullOrWhiteSpace(_node.AudioEqPreset) ? _node.AudioEqPreset : "neutral";

                // Audio
                StudioSourceAudioToggle.IsChecked = _node.SourceAudioEnabled;
                StudioSourceAudioVolumeSlider.Value = _node.SourceAudioVolumePercent;
                StudioAudioTrimToggle.IsChecked = _node.AudioTrimEnabled;
                StudioAudioTrimStartSlider.Value = _node.AudioTrimStartSec;
                StudioAudioTrimEndSlider.Value = _node.AudioTrimEndSec;

                StudioBassGainSlider.Value = _node.AudioBassGain;
                StudioBassGainLabel.Text = $"{(_node.AudioBassGain >= 0 ? "+" : "")}{_node.AudioBassGain:0.#}dB";

                StudioTrebleGainSlider.Value = _node.AudioTrebleGain;
                StudioTrebleGainLabel.Text = $"{(_node.AudioTrebleGain >= 0 ? "+" : "")}{_node.AudioTrebleGain:0.#}dB";

                StudioFadeInSlider.Value = _node.AudioFadeInSec;
                StudioFadeInLabel.Text = $"{_node.AudioFadeInSec:0.#}s";

                StudioFadeOutSlider.Value = _node.AudioFadeOutSec;
                StudioFadeOutLabel.Text = $"{_node.AudioFadeOutSec:0.#}s";

                StudioDenoiseCheckBox.IsChecked = _node.AudioDenoiseEnabled;
                StudioHighpassCheckBox.IsChecked = _node.AudioHighpassFilter;
                StudioLowpassCheckBox.IsChecked = _node.AudioLowpassFilter;
                StudioNormalizeCheckBox.IsChecked = _node.AudioNormalizeEnabled;

                SelectComboItemByTag(StudioTargetLufsCombo, _node.AudioTargetLufs.ToString("0.#", CultureInfo.InvariantCulture));
                SelectComboItemByTag(StudioSpeedCombo, _node.AudioSpeedFactor.ToString("0.##", CultureInfo.InvariantCulture));

                // Video Colors
                StudioBrightnessSlider.Value = _node.Brightness;
                StudioContrastSlider.Value = _node.Contrast;
                StudioSaturationSlider.Value = _node.Saturation;
                StudioFlipHCheckBox.IsChecked = _node.FlipH;
                StudioFlipVCheckBox.IsChecked = _node.FlipV;

                UpdateAudioTrimDurationLabel();
                WireLiveEvents();
            }
            finally
            {
                _suppressSync = false;
            }
        }

        private void WireLiveEvents()
        {
            StudioBassGainSlider.ValueChanged += (_, e) =>
            {
                if (_suppressSync) return;
                StudioBassGainLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0.#}dB";
                TriggerLiveDspIfActive();
            };
            StudioTrebleGainSlider.ValueChanged += (_, e) =>
            {
                if (_suppressSync) return;
                StudioTrebleGainLabel.Text = $"{(e.NewValue >= 0 ? "+" : "")}{e.NewValue:0.#}dB";
                TriggerLiveDspIfActive();
            };
            StudioFadeInSlider.ValueChanged += (_, e) =>
            {
                if (!_suppressSync) StudioFadeInLabel.Text = $"{e.NewValue:0.#}s";
            };
            StudioFadeOutSlider.ValueChanged += (_, e) =>
            {
                if (!_suppressSync) StudioFadeOutLabel.Text = $"{e.NewValue:0.#}s";
            };
            StudioAudioTrimStartSlider.ValueChanged += (_, _) =>
            {
                UpdateAudioTrimDurationLabel();
                TriggerLiveDspIfActive();
            };
            StudioAudioTrimEndSlider.ValueChanged += (_, _) =>
            {
                UpdateAudioTrimDurationLabel();
                TriggerLiveDspIfActive();
            };

            StudioBrightnessSlider.ValueChanged += (_, _) => ApplyStudioVisualEffects();
            StudioContrastSlider.ValueChanged += (_, _) => ApplyStudioVisualEffects();
            StudioSaturationSlider.ValueChanged += (_, _) => ApplyStudioVisualEffects();
            StudioFlipHCheckBox.Click += (_, _) => ApplyStudioVisualEffects();
            StudioFlipVCheckBox.Click += (_, _) => ApplyStudioVisualEffects();

            StudioDenoiseCheckBox.Click += (_, _) => TriggerLiveDspIfActive();
            StudioHighpassCheckBox.Click += (_, _) => TriggerLiveDspIfActive();
            StudioLowpassCheckBox.Click += (_, _) => TriggerLiveDspIfActive();
            StudioNormalizeCheckBox.Click += (_, _) => TriggerLiveDspIfActive();
            StudioTargetLufsCombo.SelectionChanged += (_, _) => TriggerLiveDspIfActive();
        }

        private void ApplyStudioVisualEffects()
        {
            if (_suppressSync) return;

            // 1. Rotation & Flip
            var scaleX = StudioFlipHCheckBox.IsChecked == true ? -1.0 : 1.0;
            var scaleY = StudioFlipVCheckBox.IsChecked == true ? -1.0 : 1.0;
            if (scaleX != 1.0 || scaleY != 1.0)
            {
                StudioPreviewMedia.RenderTransformOrigin = new Point(0.5, 0.5);
                StudioPreviewMedia.RenderTransform = new ScaleTransform(scaleX, scaleY);
            }
            else
            {
                StudioPreviewMedia.RenderTransform = null;
            }

            // 2. Real-time Color Grading
            var brightness = Math.Clamp(StudioBrightnessSlider.Value, -1.0, 1.0);
            var contrast = Math.Clamp(StudioContrastSlider.Value, 0.1, 3.0);
            var saturation = Math.Clamp(StudioSaturationSlider.Value, 0.0, 3.0);

            if (VideoEqEffect.ShaderAvailable)
            {
                _videoEqEffect ??= new VideoEqEffect();
                _videoEqEffect.Bc = new Point(brightness, contrast);
                _videoEqEffect.Sg = new Point(saturation, 1.0);
                _videoEqEffect.HueCs = new Point(1.0, 0.0);
                StudioPreviewMedia.Effect = _videoEqEffect;
                if (StudioGradingOverlay != null) StudioGradingOverlay.Background = Brushes.Transparent;
                StudioPreviewMedia.Opacity = 1.0;
            }
            else
            {
                // Software fallback tint
                if (StudioGradingOverlay != null)
                {
                    StudioPreviewMedia.Effect = null;
                    if (brightness >= 0)
                    {
                        var alpha = (byte)Math.Clamp((int)(brightness * 120), 0, 180);
                        StudioGradingOverlay.Background = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));
                    }
                    else
                    {
                        var alpha = (byte)Math.Clamp((int)(-brightness * 140), 0, 200);
                        StudioGradingOverlay.Background = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
                    }
                    var contrastOpacityBoost = (contrast - 1.0) * 0.11;
                    StudioPreviewMedia.Opacity = Math.Clamp(1.0 + contrastOpacityBoost, 0.5, 1.0);
                }
            }
        }

        private void LoadVideoMedia()
        {
            if (!string.IsNullOrWhiteSpace(_node.VideoPath) && File.Exists(_node.VideoPath))
            {
                StudioNoVideoText.Visibility = Visibility.Collapsed;
                StudioPreviewMedia.Source = new Uri(_node.VideoPath);
                StudioPreviewMedia.Volume = StudioVolumeSlider.Value;
                StudioPreviewMedia.Play();
                StudioPreviewMedia.Pause();
            }
            else
            {
                StudioNoVideoText.Visibility = Visibility.Visible;
            }
        }

        private void StudioPreviewMedia_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (StudioPreviewMedia.NaturalDuration.HasTimeSpan)
            {
                _naturalDuration = StudioPreviewMedia.NaturalDuration.TimeSpan;
                var totalSec = _naturalDuration.TotalSeconds;
                StudioScrubberSlider.Maximum = totalSec;
                StudioTimeTotalText.Text = FormatTimeSec(totalSec);

                StudioAudioTrimStartSlider.Maximum = Math.Max(totalSec, _node.AudioTrimStartSec);
                StudioAudioTrimEndSlider.Maximum = Math.Max(totalSec, _node.AudioTrimEndSec);
                UpdateAudioTrimDurationLabel();
            }
        }

        private void StudioPreviewMedia_MediaEnded(object sender, RoutedEventArgs e)
        {
            _isPlaying = false;
            _playbackTimer.Stop();
            StudioPreviewMedia.Position = TimeSpan.Zero;
            _dspAudioPlayer?.Stop();
            StudioScrubberSlider.Value = 0;
            StudioTimeElapsedText.Text = "00:00.00";
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isScrubbing && StudioPreviewMedia.Source != null)
            {
                var cur = StudioPreviewMedia.Position.TotalSeconds;
                StudioScrubberSlider.Value = cur;
                StudioTimeElapsedText.Text = FormatTimeSec(cur);
            }
        }

        private void StudioPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (StudioPreviewMedia.Source == null) return;
            if (_isPlaying)
            {
                StudioPreviewMedia.Pause();
                _dspAudioPlayer?.Pause();
                _isPlaying = false;
                _playbackTimer.Stop();
            }
            else
            {
                if (StudioDspPreviewButton.IsChecked == true && _dspAudioPlayer != null)
                {
                    StudioPreviewMedia.IsMuted = true;
                    _dspAudioPlayer.Position = StudioPreviewMedia.Position;
                    _dspAudioPlayer.Play();
                }
                else
                {
                    StudioPreviewMedia.IsMuted = false;
                }
                StudioPreviewMedia.Play();
                _isPlaying = true;
                _playbackTimer.Start();
            }
        }

        private void StudioStop_Click(object sender, RoutedEventArgs e)
        {
            if (StudioPreviewMedia.Source == null) return;
            StudioPreviewMedia.Stop();
            _dspAudioPlayer?.Stop();
            _isPlaying = false;
            _playbackTimer.Stop();
            StudioScrubberSlider.Value = 0;
            StudioTimeElapsedText.Text = "00:00.00";
        }

        private void StudioMute_Click(object sender, RoutedEventArgs e)
        {
            StudioPreviewMedia.IsMuted = !StudioPreviewMedia.IsMuted;
            if (_dspAudioPlayer != null) _dspAudioPlayer.IsMuted = StudioPreviewMedia.IsMuted;
        }

        private void StudioVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (StudioPreviewMedia != null)
                StudioPreviewMedia.Volume = e.NewValue;
            if (_dspAudioPlayer != null)
                _dspAudioPlayer.Volume = e.NewValue;
        }

        private void StudioSpeedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSync) return;
            if (StudioSpeedCombo.SelectedItem is ComboBoxItem item &&
                double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate))
            {
                try { StudioPreviewMedia.SpeedRatio = rate; } catch { }
                if (_dspAudioPlayer != null) _dspAudioPlayer.SpeedRatio = rate;
            }
        }

        private void StudioScrubberSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isScrubbing = true;
        }

        private void StudioScrubberSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isScrubbing = false;
            if (StudioPreviewMedia.Source != null)
            {
                var pos = TimeSpan.FromSeconds(StudioScrubberSlider.Value);
                StudioPreviewMedia.Position = pos;
                if (_dspAudioPlayer != null) _dspAudioPlayer.Position = pos;
                StudioTimeElapsedText.Text = FormatTimeSec(StudioScrubberSlider.Value);
            }
        }

        private void StudioPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string preset) return;
            _selectedPreset = preset;
            _suppressSync = true;
            try
            {
                switch (preset.ToLowerInvariant())
                {
                    case "neutral":
                        StudioBassGainSlider.Value = 0;
                        StudioTrebleGainSlider.Value = 0;
                        StudioHighpassCheckBox.IsChecked = false;
                        StudioLowpassCheckBox.IsChecked = false;
                        StudioDenoiseCheckBox.IsChecked = false;
                        StudioNormalizeCheckBox.IsChecked = false;
                        break;
                    case "vocal":
                        StudioBassGainSlider.Value = -2;
                        StudioTrebleGainSlider.Value = 4;
                        StudioHighpassCheckBox.IsChecked = true;
                        StudioLowpassCheckBox.IsChecked = false;
                        StudioDenoiseCheckBox.IsChecked = true;
                        break;
                    case "bass":
                        StudioBassGainSlider.Value = 7;
                        StudioTrebleGainSlider.Value = 0;
                        StudioHighpassCheckBox.IsChecked = false;
                        StudioLowpassCheckBox.IsChecked = false;
                        break;
                    case "treble":
                        StudioBassGainSlider.Value = -1;
                        StudioTrebleGainSlider.Value = 6;
                        StudioHighpassCheckBox.IsChecked = false;
                        StudioLowpassCheckBox.IsChecked = false;
                        break;
                    case "podcast":
                        StudioBassGainSlider.Value = -2;
                        StudioTrebleGainSlider.Value = 3;
                        StudioHighpassCheckBox.IsChecked = true;
                        StudioLowpassCheckBox.IsChecked = true;
                        StudioDenoiseCheckBox.IsChecked = true;
                        StudioNormalizeCheckBox.IsChecked = true;
                        SelectComboItemByTag(StudioTargetLufsCombo, "-16");
                        break;
                }
                StudioBassGainLabel.Text = $"{(StudioBassGainSlider.Value >= 0 ? "+" : "")}{StudioBassGainSlider.Value:0.#}dB";
                StudioTrebleGainLabel.Text = $"{(StudioTrebleGainSlider.Value >= 0 ? "+" : "")}{StudioTrebleGainSlider.Value:0.#}dB";
            }
            finally
            {
                _suppressSync = false;
            }

            // Auto-enable DSP preview so user hears preset in real-time
            if (StudioDspPreviewButton.IsChecked != true)
            {
                StudioDspPreviewButton.IsChecked = true;
            }
            GenerateAndPlayStudioDspAsync();
        }

        private void StudioDspPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (StudioDspPreviewButton.IsChecked == true)
            {
                GenerateAndPlayStudioDspAsync();
            }
            else
            {
                StudioPreviewMedia.IsMuted = false;
                _dspAudioPlayer?.Stop();
            }
        }

        private void TriggerLiveDspIfActive()
        {
            if (StudioDspPreviewButton.IsChecked == true)
            {
                GenerateAndPlayStudioDspAsync();
            }
        }

        private void GenerateAndPlayStudioDspAsync()
        {
            if (string.IsNullOrWhiteSpace(_node.VideoPath) || !File.Exists(_node.VideoPath)) return;

            CommitDialogToNode();
            _dspCts?.Cancel();
            _dspCts = new CancellationTokenSource();
            var ct = _dspCts.Token;

            var videoPath = _node.VideoPath;
            var totalDuration = _naturalDuration.TotalSeconds;
            var currentPos = StudioPreviewMedia.Position.TotalSeconds;
            var filterGraph = VideoAudioFilterGraphBuilder.BuildSourceAudioFilterGraph(_node, totalDuration, applyTrim: _node.AudioTrimEnabled);
            var tempWav = Path.Combine(Path.GetTempPath(), $"flowmy_dsp_studio_{Guid.NewGuid():N}.wav");

            Task.Run(async () =>
            {
                try
                {
                    var ffmpegExe = EnvironmentPathPreferencesStore.ResolveBinaryPath("ffmpeg");
                    if (string.IsNullOrWhiteSpace(ffmpegExe)) return;

                    var args = new List<string> { "-hide_banner", "-loglevel", "error", "-y", "-i", videoPath };
                    if (!string.IsNullOrWhiteSpace(filterGraph))
                    {
                        args.AddRange(new[] { "-af", filterGraph });
                    }
                    args.AddRange(new[] { "-c:a", "pcm_s16le", tempWav });

                    await VideoProcessingNodeExecutor.RunFfmpegAsync(args, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) return;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (File.Exists(tempWav))
                        {
                            try { if (!string.IsNullOrEmpty(_lastDspAudioPath) && File.Exists(_lastDspAudioPath)) File.Delete(_lastDspAudioPath); } catch { }
                            _lastDspAudioPath = tempWav;

                            _dspAudioPlayer ??= new MediaPlayer();
                            _dspAudioPlayer.Open(new Uri(tempWav));
                            _dspAudioPlayer.Volume = StudioVolumeSlider.Value;
                            _dspAudioPlayer.Position = TimeSpan.FromSeconds(currentPos);

                            if (StudioSpeedCombo.SelectedItem is ComboBoxItem item &&
                                double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate))
                            {
                                _dspAudioPlayer.SpeedRatio = rate;
                            }

                            if (_isPlaying)
                            {
                                StudioPreviewMedia.IsMuted = true;
                                _dspAudioPlayer.Play();
                            }
                        }
                    });
                }
                catch { }
            }, ct);
        }

        private void StudioChooseFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn file video nguồn",
                Filter = "Video Files (*.mp4;*.mov;*.mkv;*.avi;*.webm)|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _node.VideoPath = dlg.FileName;
                StudioVideoPathText.Text = dlg.FileName;
                LoadVideoMedia();
                TriggerLiveDspIfActive();
            }
        }

        private void StudioLaunchFfplay_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_node.VideoPath) || !File.Exists(_node.VideoPath)) return;
            var ffplayExe = EnvironmentPathPreferencesStore.ResolveBinaryPath("ffplay");
            if (string.IsNullOrWhiteSpace(ffplayExe) || !File.Exists(ffplayExe)) return;

            CommitDialogToNode();

            var currentPos = StudioPreviewMedia.Position.TotalSeconds;
            var totalDuration = _naturalDuration.TotalSeconds;
            var af = VideoAudioFilterGraphBuilder.BuildSourceAudioFilterGraph(_node, totalDuration, applyTrim: StudioAudioTrimToggle.IsChecked == true);

            var vfs = new List<string>();
            var hasColor = Math.Abs(_node.Brightness) > 0.01 || Math.Abs(_node.Contrast - 1.0) > 0.01 || Math.Abs(_node.Saturation - 1.0) > 0.01;
            if (hasColor)
            {
                var b = _node.Brightness.ToString("0.##", CultureInfo.InvariantCulture);
                var c = _node.Contrast.ToString("0.##", CultureInfo.InvariantCulture);
                var s = _node.Saturation.ToString("0.##", CultureInfo.InvariantCulture);
                vfs.Add($"eq=brightness={b}:contrast={c}:saturation={s}");
            }
            if (_node.FlipH && _node.FlipV) vfs.Add("hflip,vflip");
            else if (_node.FlipH) vfs.Add("hflip");
            else if (_node.FlipV) vfs.Add("vflip");
            var vf = vfs.Count > 0 ? string.Join(",", vfs) : string.Empty;

            if (_isPlaying)
            {
                StudioPreviewMedia.Pause();
                _dspAudioPlayer?.Pause();
                _isPlaying = false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = ffplayExe,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-window_title");
            psi.ArgumentList.Add($"Studio FFplay Preview - {Path.GetFileName(_node.VideoPath)}");
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
            if (!string.IsNullOrWhiteSpace(vf))
            {
                psi.ArgumentList.Add("-vf");
                psi.ArgumentList.Add(vf);
            }
            psi.ArgumentList.Add(_node.VideoPath);

            try
            {
                if (_activeFfplayProcess != null && !_activeFfplayProcess.HasExited)
                {
                    _activeFfplayProcess.Kill();
                    _activeFfplayProcess.Dispose();
                    _activeFfplayProcess = null;
                }
            }
            catch { }

            try { _activeFfplayProcess = Process.Start(psi); } catch { }
        }

        private void StudioThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _isLightTheme = !_isLightTheme;
            ApplyLocalTheme();
        }

        private void StudioCancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void StudioCloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CommitDialogToNode()
        {
            _node.AudioEqPreset = _selectedPreset;
            _node.SourceAudioEnabled = StudioSourceAudioToggle.IsChecked == true;
            _node.SourceAudioVolumePercent = StudioSourceAudioVolumeSlider.Value;
            _node.AudioTrimEnabled = StudioAudioTrimToggle.IsChecked == true;
            _node.AudioTrimStartSec = Math.Round(StudioAudioTrimStartSlider.Value, 2);
            _node.AudioTrimEndSec = Math.Round(StudioAudioTrimEndSlider.Value, 2);

            _node.AudioBassGain = StudioBassGainSlider.Value;
            _node.AudioTrebleGain = StudioTrebleGainSlider.Value;
            _node.AudioFadeInSec = StudioFadeInSlider.Value;
            _node.AudioFadeOutSec = StudioFadeOutSlider.Value;

            _node.AudioDenoiseEnabled = StudioDenoiseCheckBox.IsChecked == true;
            _node.AudioHighpassFilter = StudioHighpassCheckBox.IsChecked == true;
            _node.AudioLowpassFilter = StudioLowpassCheckBox.IsChecked == true;
            _node.AudioNormalizeEnabled = StudioNormalizeCheckBox.IsChecked == true;

            if (StudioTargetLufsCombo.SelectedItem is ComboBoxItem lufsItem &&
                double.TryParse(lufsItem.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lufs))
            {
                _node.AudioTargetLufs = lufs;
            }

            if (StudioSpeedCombo.SelectedItem is ComboBoxItem speedItem &&
                double.TryParse(speedItem.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
            {
                _node.AudioSpeedFactor = speed;
            }

            _node.Brightness = StudioBrightnessSlider.Value;
            _node.Contrast = StudioContrastSlider.Value;
            _node.Saturation = StudioSaturationSlider.Value;
            _node.FlipH = StudioFlipHCheckBox.IsChecked == true;
            _node.FlipV = StudioFlipVCheckBox.IsChecked == true;
        }

        private void StudioApplyButton_Click(object sender, RoutedEventArgs e)
        {
            CommitDialogToNode();
            _node.RaisePropertyChanged(string.Empty);
            DialogResult = true;
            Close();
        }

        private void UpdateAudioTrimDurationLabel()
        {
            var start = StudioAudioTrimStartSlider.Value;
            var end = StudioAudioTrimEndSlider.Value;
            if (end > start)
            {
                var dur = end - start;
                StudioAudioTrimDurationLabel.Text = $"{FormatTimeSec(start)} → {FormatTimeSec(end)} ({dur:0.##}s)";
            }
            else
            {
                StudioAudioTrimDurationLabel.Text = $"{FormatTimeSec(start)} (0.0s)";
            }
        }

        private static string FormatTimeSec(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture)
                : ts.ToString(@"mm\:ss\.ff", CultureInfo.InvariantCulture);
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
    }
}
