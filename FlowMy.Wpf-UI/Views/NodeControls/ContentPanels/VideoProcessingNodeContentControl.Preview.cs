// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Effects;
using FlowMy.Helpers;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow;
using FlowMy.Services.Workflow.NodeExecutors;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using DrawingBitmap = System.Drawing.Bitmap;
using WinForms = System.Windows.Forms;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl : UserControl
    {
        private void SelectVideo()
        {
            if (_isSelectingVideoDialog) return;
            _isSelectingVideoDialog = true;
            var dlg = new OpenFileDialog
            {
                Title = "Chon video",
                Filter = "Video Files|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files|*.*",
                CheckFileExists = true
            };
            try
            {
                if (dlg.ShowDialog() == true)
                {
                    StopComparePreviewMode();
                    _node.AudioTrimStartSec = 0;
                    _node.AudioTrimEndSec = 0;
                    _node.TrimStartSec = 0;
                    _node.TrimEndSec = 0;
                    _node.ExcludedFrameTimestamps.Clear();
                    _realTimeAudioEngine?.ClearRamBuffers();
                    UpdateDubbingRamUsage();
                    _node.VideoPath = dlg.FileName;
                    _node.RaisePropertyChanged(nameof(VideoProcessingNode.VideoPath));
                }
            }
            finally
            {
                _isSelectingVideoDialog = false;
            }
        }

        private void OpenVideoButton_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;
            e.Handled = true;
            SelectVideo();
        }

        private void OpenVideoInPlaceholderButton_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;
            e.Handled = true;
            SelectVideo();
        }

        private void SyncFrameCountFromSeconds()
        {
            var duration = Math.Max(0.1, GetNaturalDurationSeconds());
            var sourceFps = Math.Max(1, _node.SourceFps);
            var totalFramesInVideo = Math.Max(1, (int)Math.Floor(duration * sourceFps));

            FpsSlider.Maximum = totalFramesInVideo;
            var targetCount = Math.Clamp(_node.ExtractFrameCount, 1, totalFramesInVideo);
            _node.ExtractFrameCount = targetCount;
            _node.ExtractFps = (double)targetCount / duration;

            _isFrameControlSync = true;
            try
            {
                FpsSlider.Value = targetCount;
                FpsValueText.Text = $"{targetCount}";
            }
            finally
            {
                _isFrameControlSync = false;
            }
        }

        private void SyncSecondsFromFrameCount()
        {
            var duration = Math.Max(0.1, GetNaturalDurationSeconds());
            var sourceFps = Math.Max(1, _node.SourceFps);
            var totalFramesInVideo = Math.Max(1, (int)Math.Floor(duration * sourceFps));

            FpsSlider.Maximum = totalFramesInVideo;
            var targetCount = Math.Clamp(_node.ExtractFrameCount, 1, totalFramesInVideo);
            _node.ExtractFrameCount = targetCount;
            _node.ExtractFps = (double)targetCount / duration;

            _isFrameControlSync = true;
            try
            {
                FpsSlider.Value = targetCount;
                FpsValueText.Text = $"{targetCount}";
            }
            finally
            {
                _isFrameControlSync = false;
            }
        }

        private void ApplyConfigSourceMode()
        {
            var useDialog = UseDialogVideoConfigCheckBox.IsChecked == true;
            _node.UseDialogVideoConfig = useDialog;

            // Requirement: only disable controls inside the "Settings" tab.
            // The "General" tab (sliders & trim-review) must stay interactive.
            var settingsEnabled = !useDialog;
            FrameOutputFolderText.IsEnabled = settingsEnabled;
            BrowseFrameOutputFolderButton.IsEnabled = settingsEnabled;
            DefaultOutputVideoPathText.IsEnabled = settingsEnabled;
            BrowseDefaultOutputVideoButton.IsEnabled = settingsEnabled;
        }

        private void BrowseOutputPath()
        {
            var dlg = new SaveFileDialog { Filter = "MP4|*.mp4|WebM|*.webm|All|*.*" };
            if (dlg.ShowDialog() == true)
            {
                OutputPathText.Text = dlg.FileName;
                EnsureParentDirectoryExists(dlg.FileName);
                _node.OutputPathOverride = dlg.FileName;
                DefaultOutputVideoPathText.Text = dlg.FileName;
                RefreshOutputsSummaryUi();
            }
        }

        private void RefreshInfoText()
        {
            var path = _node.VideoPath?.Trim() ?? string.Empty;
            VideoPathText.Text = string.IsNullOrWhiteSpace(path) ? "Chưa chọn file video" : path;
            UpdateHwBadgeUi();
            StatFpsText.Text = _isProbingFps ? "⏳" : $"{_node.SourceFps:0.##}";
            StatResolutionText.Text = PreviewMedia.NaturalVideoWidth > 0 ? $"{PreviewMedia.NaturalVideoWidth}x{PreviewMedia.NaturalVideoHeight}" : "--";
            StatDurationText.Text = FormatTime(TimeSpan.FromSeconds(GetNaturalDurationSeconds()));
            CodecInfoText.Text = $"HW: {_node.PreferredHwAccel} | Extract: {(_node.ExtractByFpsEnabled ? $"{_node.ExtractFps:0.##}/s" : $"{_node.ExtractFrameCount} frames")}";
            AudioSummaryText.Text = $"Audio tracks: {_node.AudioTracks.Count} | Output: {(_node.OutputBase64 ? "base64" : "file")}";
            FpsValueText.Text = $"{Math.Max(1, _node.ExtractFrameCount)}";
            var secondsInt = (int)Math.Round(_node.SecondsPerFrame);
            secondsInt = Math.Clamp(secondsInt, (int)SecondsPerFrameSlider.Minimum, (int)SecondsPerFrameSlider.Maximum);
            SecondsPerFrameValueText.Text = $"{secondsInt}s";
            TrimStartText.Text = FormatTime(TimeSpan.FromSeconds(_node.TrimStartSec));
            TrimEndText.Text = FormatTime(TimeSpan.FromSeconds(_node.TrimEndSec));
            var trimDurationSec = Math.Max(0, _node.TrimEndSec - _node.TrimStartSec);
            if (TrimDurationText != null)
            {
                TrimDurationText.Text = $"{FormatTime(TimeSpan.FromSeconds(trimDurationSec))} ({trimDurationSec:0.#}s)";
            }
            var duration = GetNaturalDurationSeconds();
            _ = duration;
            UpdateFrameExtractionPreview();
            RefreshOutputsSummaryUi();
        }

        private void SyncControlValuesFromModel()
        {
            _suppressControlSync = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(_node.VideoPath))
                {
                    _ = ProbeSourceFpsAndRefreshUiAsync();
                }
                var duration = Math.Max(0.1, GetNaturalDurationSeconds());
                var sourceFps = Math.Max(1, _node.SourceFps);

                var totalFrames = Math.Max(1, (int)Math.Floor(duration * sourceFps));
                var windowSec = (int)Math.Round(_node.SecondsPerFrame);
                windowSec = Math.Clamp(windowSec, (int)SecondsPerFrameSlider.Minimum, (int)SecondsPerFrameSlider.Maximum);
                var maxInWindow = Math.Max(1, (int)Math.Round(windowSec * sourceFps));
                if (ExtractByFpsToggle != null)
                    ExtractByFpsToggle.IsChecked = _node.ExtractByFpsEnabled;
                if (ExtractByFpsContainer != null)
                    ExtractByFpsContainer.Visibility = _node.ExtractByFpsEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (ExtractByCountContainer != null)
                    ExtractByCountContainer.Visibility = !_node.ExtractByFpsEnabled ? Visibility.Visible : Visibility.Collapsed;

                var maxFps = Math.Max(1, (int)Math.Round(sourceFps));
                if (ExtractFpsRateSlider != null)
                {
                    ExtractFpsRateSlider.Maximum = maxFps;
                    ExtractFpsRateSlider.Value = Math.Clamp(_node.ExtractFps, 1, maxFps);
                }
                if (ExtractFpsRateBox != null)
                    ExtractFpsRateBox.Text = ((int)Math.Round(_node.ExtractFps)).ToString();
                if (ExtractFpsRateLabel != null)
                    ExtractFpsRateLabel.Text = $"{Math.Round(_node.ExtractFps)} frame/s";

                FpsSlider.Maximum = _node.ExtractAllFrames ? totalFrames : maxInWindow;
                FpsSlider.Value = Math.Clamp(_node.ExtractFrameCount, 1, (int)FpsSlider.Maximum);
                if (FpsValueBox != null)
                    FpsValueBox.Text = _node.ExtractFrameCount.ToString();
                if (FpsValueText != null)
                    FpsValueText.Text = $"{_node.ExtractFrameCount}";
                SecondsPerFrameSlider.Value = windowSec;
                UseDialogVideoConfigCheckBox.IsChecked = _node.UseDialogVideoConfig;
                PreferGpuCheckBox.IsChecked = _node.PreferGpu;
                SyncAudioTabFromModel();
                UpdatePreviewAudioVolume();
                VolumeSlider.Value = _node.PreviewVolume;
                _node.PreviewQualityMode = "high";
                RebuildPreviewQualityOptions(PreviewMedia.NaturalVideoHeight);
                PreviewVisualStrengthCombo.SelectedIndex = _node.PreviewVisualStrengthMode switch
                {
                    "fast" => 0,
                    "strong" => 2,
                    _ => 1
                };
                FrameFormatCombo.SelectedIndex = _node.FrameOutputFormat switch { "jpg" => 1, "webp" => 2, _ => 0 };
                JpegQualitySlider.Value = _node.JpegQuality;
                ExtractAllFramesCheckBox.IsChecked = _node.ExtractAllFrames;
                JpegQualitySlider.Visibility = _node.FrameOutputFormat == "jpg" ? Visibility.Visible : Visibility.Collapsed;
                if (JpegQualityLabel != null)
                {
                    JpegQualityLabel.Visibility = _node.FrameOutputFormat == "jpg" ? Visibility.Visible : Visibility.Collapsed;
                    JpegQualityLabel.Text = $"Quality: {_node.JpegQuality}/100";
                }

                _frameResizeScale = Math.Clamp(_node.FrameResizeScale, FrameResizeSlider.Minimum, FrameResizeSlider.Maximum);
                FrameResizeSlider.Value = _frameResizeScale;
                var w = (int)(PreviewMedia.NaturalVideoWidth * _frameResizeScale);
                var h = (int)(PreviewMedia.NaturalVideoHeight * _frameResizeScale);
                FrameResizeLabel.Text = (w <= 0 || h <= 0) ? $"{_frameResizeScale:0.##}x" : $"{w}×{h}";

                if (ColorGradingToggle != null)
                    ColorGradingToggle.IsChecked = _node.ColorGradingEnabled;
                if (ColorGradingContainer != null)
                    ColorGradingContainer.Visibility = _node.ColorGradingEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (ColorGradingStatusText != null)
                    ColorGradingStatusText.Text = _node.ColorGradingEnabled ? "ĐANG BẬT" : "TẮT (Gốc)";

                BrightnessSlider.Value = _node.Brightness;
                ContrastSlider.Value = _node.Contrast;
                SaturationSlider.Value = _node.Saturation;
                HueSlider.Value = _node.Hue;
                GammaSlider.Value = _node.Gamma;
                BrightnessLabel.Text = $"{_node.Brightness:0.##}";
                ContrastLabel.Text = $"{_node.Contrast:0.##}";
                SaturationLabel.Text = $"{_node.Saturation:0.##}";
                HueLabel.Text = $"{_node.Hue:0.##}";
                GammaLabel.Text = $"{_node.Gamma:0.##}";
                QuickBrightnessSlider.Value = _node.Brightness;
                QuickContrastSlider.Value = _node.Contrast;
                QuickSaturationSlider.Value = _node.Saturation;
                QuickBrightnessLabel.Text = $"{_node.Brightness:0.#}";
                QuickContrastLabel.Text = $"{_node.Contrast:0.#}";
                QuickSaturationLabel.Text = $"{_node.Saturation:0.#}";

                SharpenToggle.IsChecked = _node.SharpenEnabled;
                SharpenSlider.IsEnabled = _node.SharpenEnabled;
                SharpenSlider.Value = _node.SharpenStrength;
                SharpenLabel.Text = $"{_node.SharpenStrength:0.#}";
                if (SharpenContainer != null)
                    SharpenContainer.Visibility = _node.SharpenEnabled ? Visibility.Visible : Visibility.Collapsed;

                DenoiseToggle.IsChecked = _node.DenoiseEnabled;
                DenoiseSlider.IsEnabled = _node.DenoiseEnabled;
                DenoiseSlider.Value = _node.DenoiseStrength;
                DenoiseLabel.Text = $"{_node.DenoiseStrength:0.#}";
                if (DenoiseContainer != null)
                    DenoiseContainer.Visibility = _node.DenoiseEnabled ? Visibility.Visible : Visibility.Collapsed;

                BlurToggle.IsChecked = _node.BlurEnabled;
                BlurSlider.IsEnabled = _node.BlurEnabled;
                BlurSlider.Value = _node.BlurRadius;
                BlurLabel.Text = $"{_node.BlurRadius:0.#}";
                if (BlurContainer != null)
                    BlurContainer.Visibility = _node.BlurEnabled ? Visibility.Visible : Visibility.Collapsed;

                StabilizeToggle.IsChecked = _node.StabilizeEnabled;

                if (TransformToggle != null)
                    TransformToggle.IsChecked = _node.TransformEnabled;
                if (TransformContainer != null)
                    TransformContainer.Visibility = _node.TransformEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (TransformStatusText != null)
                    TransformStatusText.Text = _node.TransformEnabled ? "ĐANG BẬT" : "TẮT (Gốc)";

                if (SpeedAdjustToggle != null)
                    SpeedAdjustToggle.IsChecked = _node.SpeedAdjustEnabled;
                if (SpeedAdjustContainer != null)
                    SpeedAdjustContainer.Visibility = _node.SpeedAdjustEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (SpeedAdjustStatusText != null)
                    SpeedAdjustStatusText.Text = _node.SpeedAdjustEnabled ? $"{_node.SpeedFactor:0.##}x" : "TẮT (1.0x)";

                SpeedSlider.Value = _node.SpeedFactor;
                SpeedLabel.Text = $"{_node.SpeedFactor:0.##}x";

                CrfSlider.Value = _node.Crf;
                CrfLabel.Text = $"{(int)_node.Crf}";
                OutputPathText.Text = _node.OutputPathOverride ?? string.Empty;
                DefaultOutputVideoPathText.Text = _node.DefaultOutputVideoPath ?? string.Empty;
                FrameOutputFolderText.Text = _node.FrameOutputFolderPath ?? string.Empty;
                AudioOutputFolderText.Text = _node.AudioOutputFolderPath ?? string.Empty;

                var defaultVideoFolder = GetDefaultVideoOutputFolder();
                var defaultFrameFolder = GetDefaultFrameOutputFolder();
                var defaultAudioFolder = GetDefaultAudioOutputFolder();
                var videoStem = GetVideoFileNameStem();

                if (OutputPathHintText != null) OutputPathHintText.Text = $"📍 Mặc định (khi để trống): {defaultVideoFolder}\\{videoStem}.mp4";
                if (DefaultVideoFolderHintText != null) DefaultVideoFolderHintText.Text = $"📍 Mặc định (khi để trống): {defaultVideoFolder}";
                if (DefaultFrameFolderHintText != null) DefaultFrameFolderHintText.Text = $"📍 Mặc định (khi để trống): {defaultFrameFolder}";
                if (DefaultAudioFolderHintText != null) DefaultAudioFolderHintText.Text = $"📍 Mặc định (khi để trống): {defaultAudioFolder}";

                TrimToggle.IsChecked = _node.TrimEnabled;
                if (TrimContainer != null)
                    TrimContainer.Visibility = _node.TrimEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (TrimStatusText != null)
                    TrimStatusText.Text = _node.TrimEnabled ? "ĐANG BẬT" : "TẮT (Toàn bộ)";

                ConcatToggle.IsChecked = _node.ConcatEnabled;
                if (ConcatContainer != null)
                    ConcatContainer.Visibility = _node.ConcatEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (ConcatStatusText != null)
                    ConcatStatusText.Text = _node.ConcatEnabled ? $"{_node.ConcatVideos.Count} video" : "TẮT";
                ConcatVideosList.ItemsSource = _node.ConcatVideos;

                TrimReviewCheckBox.IsChecked = _node.TrimEnabled;
                TrimReviewHitArea.Visibility = _node.TrimEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (TrimReviewFramesPanel != null) TrimReviewFramesPanel.Visibility = _node.TrimEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (_node.TrimEnabled)
                {
                    _ = LoadTrimFramePreviewAsync(isStart: true);
                    _ = LoadTrimFramePreviewAsync(isStart: false);
                }
                _fixedResolutionHeight = _node.FixedResolutionHeight;
                WatermarkToggle.IsChecked = _node.WatermarkEnabled;
                WatermarkPathText.Text = _node.WatermarkImagePath ?? string.Empty;
                WatermarkPositionCombo.SelectedIndex = _node.WatermarkPosition switch
                {
                    "TL" => 0,
                    "TC" => 1,
                    "TR" => 2,
                    "ML" => 3,
                    "MC" => 4,
                    "MR" => 5,
                    "BL" => 6,
                    "BC" => 7,
                    _ => 8
                };
                RefreshWatermarkPositionHint();
                WatermarkOpacitySlider.Value = _node.WatermarkOpacity;
                WatermarkOpacityLabel.Text = $"{_node.WatermarkOpacity:0.##}";
                WatermarkWidthPercentSlider.Value = Math.Clamp(_node.WatermarkWidthFraction * 100.0, WatermarkWidthPercentSlider.Minimum, WatermarkWidthPercentSlider.Maximum);
                WatermarkWidthPercentLabel.Text = $"{WatermarkWidthPercentSlider.Value:0.#}% video";
                WatermarkInsetPercentSlider.Value = Math.Clamp(_node.WatermarkInsetFraction * 100.0, WatermarkInsetPercentSlider.Minimum, WatermarkInsetPercentSlider.Maximum);
                WatermarkInsetPercentLabel.Text = $"{WatermarkInsetPercentSlider.Value:0.#}% mép";
                UpdateWatermarkPreviewUi();

                if (CanvasOverlayToggle != null)
                    CanvasOverlayToggle.IsChecked = _node.CanvasOverlayEnabled;
                if (CanvasOverlayContainer != null)
                    CanvasOverlayContainer.Visibility = _node.CanvasOverlayEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (CanvasOverlayStatusText != null)
                    CanvasOverlayStatusText.Text = _node.CanvasOverlayEnabled ? "ĐANG BẬT" : "TẮT";

                TextOverlayToggle.IsChecked = _node.TextOverlayEnabled;
                OverlayTextBox.Text = _node.OverlayText;
                TextSizeSlider.Value = _node.OverlayFontSize;
                TextSizeLabel.Text = $"{_node.OverlayFontSize}px";
                FrameLabelToggle.IsChecked = _node.FrameLabelEnabled;
                if (FrameLabelContainer != null)
                    FrameLabelContainer.Visibility = _node.FrameLabelEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (FrameLabelStatusText != null)
                    FrameLabelStatusText.Text = _node.FrameLabelEnabled ? "ĐANG BẬT" : "TẮT";

                var subEnabled = _node.SubtitleStyle?.Enabled ?? _node.BurnSubtitleEnabled;
                if (SubtitleEnabledToggle != null)
                    SubtitleEnabledToggle.IsChecked = subEnabled;
                if (SubtitlesMasterContainer != null)
                    SubtitlesMasterContainer.Visibility = subEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (SubtitleStatusText != null)
                    SubtitleStatusText.Text = subEnabled ? "ĐANG BẬT" : "TẮT";

                if (DubbingEnabledToggle != null)
                    DubbingEnabledToggle.IsChecked = _node.DubbingEnabled;
                if (DubbingMasterContainer != null)
                    DubbingMasterContainer.Visibility = _node.DubbingEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (DubbingStatusText != null)
                    DubbingStatusText.Text = _node.DubbingEnabled ? "ĐANG BẬT" : "TẮT";

                var duckEnabled = _node.AutoDucking?.Enabled ?? true;
                if (AutoDuckingToggle != null)
                    AutoDuckingToggle.IsChecked = duckEnabled;
                if (AutoDuckingParamsContainer != null)
                    AutoDuckingParamsContainer.Visibility = duckEnabled ? Visibility.Visible : Visibility.Collapsed;

                FrameLabelTemplateTextBox.Text = _node.FrameLabelTemplate;
                FrameLabelXSlider.Value = _node.FrameLabelX;
                FrameLabelYSlider.Value = _node.FrameLabelY;
                FrameLabelPaddingLeftSlider.Value = _node.FrameLabelPaddingLeft;
                FrameLabelPaddingLeftLabel.Text = $"{_node.FrameLabelPaddingLeft}px";
                FrameLabelPaddingTopSlider.Value = _node.FrameLabelPaddingTop;
                FrameLabelPaddingTopLabel.Text = $"{_node.FrameLabelPaddingTop}px";
                FrameLabelPaddingRightSlider.Value = _node.FrameLabelPaddingRight;
                FrameLabelPaddingRightLabel.Text = $"{_node.FrameLabelPaddingRight}px";
                FrameLabelPaddingBottomSlider.Value = _node.FrameLabelPaddingBottom;
                FrameLabelPaddingBottomLabel.Text = $"{_node.FrameLabelPaddingBottom}px";
                FrameLabelTimeFormatCombo.SelectedIndex = string.Equals(_node.FrameLabelTimeFormat, "HHMMSS", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                FrameLabelTextColorTextBox.Text = _node.FrameLabelTextColor;
                FrameLabelBackgroundColorTextBox.Text = _node.FrameLabelBackgroundColor;
                FrameLabelFontSizeSlider.Value = _node.FrameLabelFontSize;
                FrameLabelFontSizeLabel.Text = $"{_node.FrameLabelFontSize}px";
                FrameLabelDebugSamplesCheckBox.IsChecked = _node.FrameLabelDebugSamplesEnabled;
                ExtractParallelJobsCombo.SelectedIndex = _node.ExtractParallelJobs switch
                {
                    2 => 1,
                    4 => 2,
                    6 => 3,
                    8 => 4,
                    _ => 0
                };
                TwoPassToggle.IsChecked = _node.TwoPassEnabled;
                AudioCodecCombo.SelectedIndex = _node.AudioCodec switch { "mp3" => 1, "opus" => 2, "copy" => 3, _ => 0 };
                AudioBitrateCombo.SelectedIndex = _node.AudioBitrate switch { "128k" => 0, "256k" => 2, "320k" => 3, _ => 1 };
                SyncGridCollageControlsFromModel();
            }
            finally
            {
                _suppressControlSync = false;
            }

            ApplyPreviewQualitySettings();
            ApplyConfigSourceMode();
            UpdateFrameLabelPreviewUi();
            UpdateGridCollagePreviewUi();
            ApplyPreviewTransformEffects();
        }

        private void RefreshVideoPreview()
        {
            var path = _node.VideoPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                PreviewMedia.Stop();
                _realTimeAudioEngine?.Stop();
                _realTimeAudioEngine?.Dispose();
                _realTimeAudioEngine = null;
                PreviewMedia.Source = null;
                PreviewMedia.Visibility = Visibility.Collapsed;
                PreviewPlaceholder.Visibility = Visibility.Visible;
                FrameLabelPreviewOverlay.Visibility = Visibility.Collapsed;
                LiveDot.Visibility = Visibility.Collapsed;
                _isPlaying = false;
                _timelineTimer.Stop();
                UpdatePlaybackUi();
                RebuildPreviewQualityOptions(0);
                return;
            }

            try
            {
                PreviewMedia.Stop();
                _realTimeAudioEngine?.Stop();
                _realTimeAudioEngine?.Dispose();
                _realTimeAudioEngine = null;
                PreviewMedia.Source = null;
                _timelineTimer.Stop();
                _isPlaying = false;
                LiveDot.Visibility = Visibility.Collapsed;
                UpdatePlaybackUi();
                PreviewMedia.Source = new Uri(path, UriKind.Absolute);
                PreviewMedia.Visibility = Visibility.Visible;
                PreviewPlaceholder.Visibility = Visibility.Collapsed;
                PreviewMedia.Volume = _node.PreviewVolume;
                _isPlaying = false;
                LiveDot.Visibility = Visibility.Collapsed;
                AspectAuto.IsChecked = true;
                SetAspectRatio(0, 0, true);
                if (_isDspAudioPreviewActive)
                {
                    EnsureRealTimeAudioEngineLoaded();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Preview error: {ex.Message}");
                PreviewMedia.Stop();
                _realTimeAudioEngine?.Stop();
                _realTimeAudioEngine?.Dispose();
                _realTimeAudioEngine = null;
                PreviewMedia.Source = null;
                PreviewMedia.Visibility = Visibility.Collapsed;
                PreviewPlaceholder.Visibility = Visibility.Visible;
                FrameLabelPreviewOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshFrameResizeLabel()
        {
            if (PreviewMedia.NaturalVideoWidth <= 0 || PreviewMedia.NaturalVideoHeight <= 0) return;
            var scale = _node.FrameResizeScale;
            var w = (int)(PreviewMedia.NaturalVideoWidth * scale);
            var h = (int)(PreviewMedia.NaturalVideoHeight * scale);
            if (w <= 0 || h <= 0)
            {
                FrameResizeLabel.Text = $"{scale:0.##}x";
                return;
            }
            FrameResizeLabel.Text = $"{w}×{h}";
        }

        private void UpdateFrameLabelPreviewUi()
        {
            if (FrameLabelPreviewOverlay == null || FrameLabelPreviewText == null) return;

            if (!_node.FrameLabelEnabled || PreviewMedia.Source == null)
            {
                FrameLabelPreviewOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            var currentFrame = Math.Max(0, (int)Math.Round(PreviewMedia.Position.TotalSeconds * Math.Max(1, _node.SourceFps)));
            var currentTime = string.Equals(_node.FrameLabelTimeFormat, "HHMMSS", StringComparison.OrdinalIgnoreCase)
                ? PreviewMedia.Position.ToString(@"hh\:mm\:ss")
                : PreviewMedia.Position.ToString(@"mm\:ss");

            var template = string.IsNullOrWhiteSpace(_node.FrameLabelTemplate)
                ? "Frame {index} - {time}"
                : _node.FrameLabelTemplate;
            var effectiveOutputFps = _node.ExtractAllFrames ? Math.Max(0.001, _node.SourceFps) : Math.Max(0.001, _node.ExtractFps);
            var outputIndex = Math.Max(1, (int)Math.Floor(PreviewMedia.Position.TotalSeconds * effectiveOutputFps) + 1);

            FrameLabelPreviewText.Text = template
                .Replace("{index}", outputIndex.ToString())
                .Replace("{frame}", currentFrame.ToString())
                .Replace("{time}", currentTime);

            var natW = PreviewMedia.NaturalVideoWidth;
            var natH = PreviewMedia.NaturalVideoHeight;
            var rect = GetDisplayedVideoRect();
            var areaH = Math.Max(1, rect.Height);
            // Use GetEstimatedSourceFrameSize (accounts for rotation/crop/resize) to match
            // CompositeLabelOntoStillFile font scaling: fontPx * (boxH / labelBoxSrcH).
            var (_, estH) = FrameLabelRasterComposer.GetEstimatedSourceFrameSize(
                natW > 0 ? natW : 1280, natH > 0 ? natH : 720, _node);
            var drawtextPx = VideoProcessingNodeExecutor.ComputeFrameLabelDrawtextFontPixelSize(_node, natH > 0 ? natH : (int?)null);
            var previewFontDip = drawtextPx * (areaH / (double)Math.Max(1, estH));
            FrameLabelPreviewText.FontFamily = FrameLabelPreviewFontFamily;
            FrameLabelPreviewText.FontWeight = FontWeights.Normal;
            FrameLabelPreviewText.FontSize = Math.Max(4, previewFontDip);
            FrameLabelPreviewText.Foreground = ParseBrushOrDefault(_node.FrameLabelTextColor, Brushes.Black);
            FrameLabelPreviewOverlay.Background = ParseBrushOrDefault(_node.FrameLabelBackgroundColor, Brushes.White);
            FrameLabelPreviewOverlay.Visibility = Visibility.Visible;
            UpdateFrameLabelPreviewLayout();
            UpdateColorPreviews();
        }

        private void UpdateFrameLabelPreviewLayout()
        {
            if (FrameLabelPreviewOverlay == null || VideoAreaGrid == null || PreviewMedia == null) return;
            var rect = GetDisplayedVideoRect();
            var areaW = Math.Max(1, rect.Width);
            var areaH = Math.Max(1, rect.Height);
            var natW = PreviewMedia.NaturalVideoWidth;
            var natH = PreviewMedia.NaturalVideoHeight;
            var srcW = Math.Max(1, natW);
            var srcH = Math.Max(1, natH);
            var isPortrait = srcH > srcW;
            var defaultWFrac = isPortrait ? (2.0 / 3.0) : 0.20;
            var usePreset = FrameLabelRasterComposer.TryGetLabelPresetFractions(srcW, srcH, out var labelWFrac, out var labelHFrac);
            if (!usePreset)
            {
                labelWFrac = (_node.FrameLabelW <= 0.05 || Math.Abs(_node.FrameLabelW - 0.18) < 0.001 || Math.Abs(_node.FrameLabelW - 0.20) < 0.001)
                    ? defaultWFrac
                    : _node.FrameLabelW;
                labelHFrac = _node.FrameLabelH;
            }

            var sourceScale = VideoProcessingNodeExecutor.ComputeFrameLabelSourceScale(natH > 0 ? natH : (int?)null);
            var padVidLeft = Math.Max(0, (int)Math.Round(_node.FrameLabelPaddingLeft * sourceScale));
            var padVidTop = Math.Max(0, (int)Math.Round(_node.FrameLabelPaddingTop * sourceScale));
            var padVidRight = Math.Max(0, (int)Math.Round(_node.FrameLabelPaddingRight * sourceScale));
            var padVidBottom = Math.Max(0, (int)Math.Round(_node.FrameLabelPaddingBottom * sourceScale));

            var scaleX = areaW / srcW;
            var scaleY = areaH / srcH;

            var padPl = padVidLeft * scaleX;
            var padPt = padVidTop * scaleY;
            var padPr = padVidRight * scaleX;
            var padPb = padVidBottom * scaleY;

            var fontSize = FrameLabelPreviewText.FontSize > 0 ? FrameLabelPreviewText.FontSize : 14;

            var dpi = VisualTreeHelper.GetDpi(FrameLabelPreviewText);
            var typeface = new Typeface(
                FrameLabelPreviewText.FontFamily,
                FrameLabelPreviewText.FontStyle,
                FrameLabelPreviewText.FontWeight,
                FrameLabelPreviewText.FontStretch);

            var ft = new FormattedText(
                FrameLabelPreviewText.Text ?? string.Empty,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                dpi.PixelsPerDip);

            var capsH = typeface.CapsHeight * fontSize;
            var fontTopLeading = Math.Max(0, ft.Baseline - capsH);
            var visibleTextH = Math.Max(4, capsH + 0.5);

            FrameLabelPreviewText.Margin = new Thickness(0, -fontTopLeading, 0, 0);
            FrameLabelPreviewOverlay.Padding = new Thickness(padPl, padPt, padPr, padPb);

            var boxW = Math.Max(10, Math.Ceiling(ft.Width) + padPl + padPr);
            var boxH = Math.Max(10, visibleTextH + padPt + padPb);

            FrameLabelPreviewOverlay.HorizontalAlignment = HorizontalAlignment.Left;
            FrameLabelPreviewOverlay.VerticalAlignment = VerticalAlignment.Top;
            FrameLabelPreviewOverlay.Width = boxW;
            FrameLabelPreviewOverlay.Height = boxH;

            var left = rect.X + (_node.FrameLabelX * areaW);
            var top = rect.Y + (_node.FrameLabelY * areaH);
            FrameLabelPreviewOverlay.Margin = new Thickness(Math.Max(0, left), Math.Max(0, top), 0, 0);
            FrameLabelPosLabel.Text = $"X {_node.FrameLabelX:0.###} | Y {_node.FrameLabelY:0.###}";
        }



        /// <summary>
        /// Rectangle in <see cref="VideoAreaGrid"/> coordinates that matches the actual decoded video pixels
        /// (after MediaElement Uniform letterboxing inside the Viewbox). Using the full Viewbox size would
        /// desync watermark / overlay preview from FFmpeg, which composites on real frame dimensions.
        /// </summary>
        private Rect GetDisplayedVideoRect()
        {
            if (VideoAreaGrid == null || VideoViewbox == null || PreviewMedia == null)
                return new Rect(0, 0, 1, 1);

            var mediaW = PreviewMedia.ActualWidth;
            var mediaH = PreviewMedia.ActualHeight;
            if (mediaW <= 0 || mediaH <= 0)
            {
                mediaW = double.IsNaN(PreviewMedia.Width) || PreviewMedia.Width <= 0 ? 1280 : PreviewMedia.Width;
                mediaH = double.IsNaN(PreviewMedia.Height) || PreviewMedia.Height <= 0 ? 720 : PreviewMedia.Height;
            }

            Rect mediaBounds;
            try
            {
                var toArea = PreviewMedia.TransformToVisual(VideoAreaGrid);
                mediaBounds = toArea.TransformBounds(new Rect(0, 0, mediaW, mediaH));
            }
            catch
            {
                var viewboxW = Math.Max(1, VideoViewbox.ActualWidth);
                var viewboxH = Math.Max(1, VideoViewbox.ActualHeight);
                var containerW = Math.Max(1, VideoAreaGrid.ActualWidth);
                var containerH = Math.Max(1, VideoAreaGrid.ActualHeight);
                mediaBounds = new Rect(
                    Math.Max(0, (containerW - viewboxW) / 2),
                    Math.Max(0, (containerH - viewboxH) / 2),
                    viewboxW,
                    viewboxH);
            }

            if (PreviewMedia.Source == null || PreviewMedia.NaturalVideoWidth <= 0 || PreviewMedia.NaturalVideoHeight <= 0)
                return mediaBounds;

            var natW = (double)PreviewMedia.NaturalVideoWidth;
            var natH = (double)PreviewMedia.NaturalVideoHeight;
            var mediaRatio = mediaBounds.Width / Math.Max(1d, mediaBounds.Height);
            var natRatio = natW / Math.Max(1d, natH);

            double contentW;
            double contentH;
            if (natRatio >= mediaRatio)
            {
                contentW = mediaBounds.Width;
                contentH = contentW / natRatio;
            }
            else
            {
                contentH = mediaBounds.Height;
                contentW = contentH * natRatio;
            }

            var contentX = mediaBounds.X + (mediaBounds.Width - contentW) / 2d;
            var contentY = mediaBounds.Y + (mediaBounds.Height - contentH) / 2d;
            return new Rect(contentX, contentY, Math.Max(1, contentW), Math.Max(1, contentH));
        }

        private void UpdateOverlayCanvasBounds()
        {
            if (VideoAreaGrid == null || PreviewMedia.Source == null) return;
            var rect = GetDisplayedVideoRect();
            if (OverlayCanvasControl != null)
            {
                OverlayCanvasControl.HorizontalAlignment = HorizontalAlignment.Left;
                OverlayCanvasControl.VerticalAlignment = VerticalAlignment.Top;
                OverlayCanvasControl.Margin = new Thickness(rect.X, rect.Y, 0, 0);
                OverlayCanvasControl.Width = Math.Max(1, rect.Width);
                OverlayCanvasControl.Height = Math.Max(1, rect.Height);
            }
            if (SubtitleLiveOverlayContainer != null)
            {
                SubtitleLiveOverlayContainer.HorizontalAlignment = HorizontalAlignment.Left;
                SubtitleLiveOverlayContainer.VerticalAlignment = VerticalAlignment.Top;
                SubtitleLiveOverlayContainer.Margin = new Thickness(rect.X, rect.Y, 0, 0);
                SubtitleLiveOverlayContainer.Width = Math.Max(1, rect.Width);
                SubtitleLiveOverlayContainer.Height = Math.Max(1, rect.Height);
            }
            ApplySubtitleStylesToLiveOverlay();
        }

        private void SyncVideoViewportClip()
        {
            if (VideoViewportClipBorder == null || !VideoViewportClipBorder.IsLoaded)
                return;

            var w = Math.Max(1d, VideoViewportClipBorder.ActualWidth);
            var h = Math.Max(1d, VideoViewportClipBorder.ActualHeight);
            VideoViewportClipBorder.Clip = new RectangleGeometry(new Rect(0, 0, w, h));
        }

        /// <summary>Clip toàn bộ UserControl (nền vuông từ ApplyLocalTheme) khớp bo góc node + XAML designer.</summary>
        private void SyncUserControlRoundedClip()
        {
            if (!IsLoaded) return;
            var w = Math.Max(1d, ActualWidth);
            var h = Math.Max(1d, ActualHeight);
            var maxR = Math.Min(w, h) / 2 - 0.001;
            var r = Math.Min(VideoNodeCornerRadius, Math.Max(0, maxR));
            Clip = r <= 0.25
                ? new RectangleGeometry(new Rect(0, 0, w, h))
                : new RectangleGeometry(new Rect(0, 0, w, h), r, r);
        }

        private void UpdateWatermarkPreviewUi()
        {
            if (WatermarkPreviewImage == null)
                return;

            if (PreviewMedia.Source == null ||
                !_node.WatermarkEnabled ||
                string.IsNullOrWhiteSpace(_node.WatermarkImagePath) ||
                !File.Exists(_node.WatermarkImagePath))
            {
                WatermarkPreviewImage.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                var imagePath = _node.WatermarkImagePath!;
                var source = new BitmapImage();
                source.BeginInit();
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.UriSource = new Uri(imagePath, UriKind.Absolute);
                source.EndInit();
                source.Freeze();
                WatermarkPreviewImage.Source = source;
                WatermarkPreviewImage.Opacity = Math.Clamp(_node.WatermarkOpacity, 0, 1);

                var rect = GetDisplayedVideoRect();
                var srcVideoW = Math.Max(1, PreviewMedia.NaturalVideoWidth);
                var srcVideoH = Math.Max(1, PreviewMedia.NaturalVideoHeight);
                var uScale = Math.Min(rect.Width / srcVideoW, rect.Height / srcVideoH);

                int wmPixelW;
                int wmPixelH;
                using (var bmp = new DrawingBitmap(imagePath))
                {
                    wmPixelW = Math.Max(1, bmp.Width);
                    wmPixelH = Math.Max(1, bmp.Height);
                }

                var wf = VideoWatermarkGeometry.ClampWidthFraction(_node.WatermarkWidthFraction);
                var inf = VideoWatermarkGeometry.ClampInsetFraction(_node.WatermarkInsetFraction);
                double wmWVideo = srcVideoW * wf;
                double wmHVideo = wmWVideo * (wmPixelH / (double)wmPixelW);
                double padXV = srcVideoW * inf;
                double padYV = srcVideoH * inf;

                double wmW = Math.Max(1, wmWVideo * uScale);
                double wmH = Math.Max(1, wmHVideo * uScale);
                double padXs = padXV * uScale;
                double padYs = padYV * uScale;

                double x;
                double y;
                switch ((_node.WatermarkPosition ?? "BR").Trim().ToUpperInvariant())
                {
                    case "TL": x = rect.X + padXs; y = rect.Y + padYs; break;
                    case "TC": x = rect.X + (rect.Width - wmW) / 2d; y = rect.Y + padYs; break;
                    case "TR": x = rect.Right - wmW - padXs; y = rect.Y + padYs; break;
                    case "ML": x = rect.X + padXs; y = rect.Y + (rect.Height - wmH) / 2d; break;
                    case "MC": x = rect.X + (rect.Width - wmW) / 2d; y = rect.Y + (rect.Height - wmH) / 2d; break;
                    case "MR": x = rect.Right - wmW - padXs; y = rect.Y + (rect.Height - wmH) / 2d; break;
                    case "BL": x = rect.X + padXs; y = rect.Bottom - wmH - padYs; break;
                    case "BC": x = rect.X + (rect.Width - wmW) / 2d; y = rect.Bottom - wmH - padYs; break;
                    default: x = rect.Right - wmW - padXs; y = rect.Bottom - wmH - padYs; break;
                }

                WatermarkPreviewImage.Width = wmW;
                WatermarkPreviewImage.Height = wmH;
                WatermarkPreviewImage.Margin = new Thickness(Math.Max(0, x), Math.Max(0, y), 0, 0);
                WatermarkPreviewImage.Visibility = Visibility.Visible;
            }
            catch
            {
                WatermarkPreviewImage.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshWatermarkPositionHint()
        {
            if (WatermarkPositionHintText == null) return;
            WatermarkPositionHintText.Text = _node.WatermarkPosition switch
            {
                "TL" => "Vị trí: Top Left",
                "TC" => "Vị trí: Top Center",
                "TR" => "Vị trí: Top Right",
                "ML" => "Vị trí: Middle Left",
                "MC" => "Vị trí: Middle Center",
                "MR" => "Vị trí: Middle Right",
                "BL" => "Vị trí: Bottom Left",
                "BC" => "Vị trí: Bottom Center",
                _ => "Vị trí: Bottom Right"
            };
        }

        private static Brush ParseBrushOrDefault(string? value, Brush fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value)) return fallback;
                var converted = new BrushConverter().ConvertFromString(value.Trim());
                return converted is Brush brush ? brush : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private void PickFrameLabelTextColor_Click(object sender, RoutedEventArgs e)
        {
            var picked = ShowColorPicker(_node.FrameLabelTextColor);
            if (string.IsNullOrWhiteSpace(picked)) return;
            _node.FrameLabelTextColor = picked;
            FrameLabelTextColorTextBox.Text = picked;
            UpdateFrameLabelPreviewUi();
        }

        private void PickFrameLabelBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            var picked = ShowColorPicker(_node.FrameLabelBackgroundColor);
            if (string.IsNullOrWhiteSpace(picked)) return;
            _node.FrameLabelBackgroundColor = picked;
            FrameLabelBackgroundColorTextBox.Text = picked;
            UpdateFrameLabelPreviewUi();
        }

        private void PickOverlayFontColor_Click(object sender, RoutedEventArgs e)
        {
            var picked = ShowColorPicker(OverlayFontColorTextBox.Text);
            if (string.IsNullOrWhiteSpace(picked)) return;
            OverlayFontColorTextBox.Text = picked;
            ApplyOverlayPropertyEditorChanges();
            UpdateColorPreviews();
        }

        private static string? ShowColorPicker(string? currentHex)
        {
            try
            {
                using var dialog = new WinForms.ColorDialog { FullOpen = true };
                if (!string.IsNullOrWhiteSpace(currentHex) && currentHex.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                {
                    try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(currentHex); } catch { }
                }

                return dialog.ShowDialog() == WinForms.DialogResult.OK
                    ? $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private void UpdateColorPreviews()
        {
            if (FrameLabelTextColorPreview != null)
                FrameLabelTextColorPreview.Background = ParseBrushOrDefault(_node.FrameLabelTextColor, Brushes.Black);
            if (FrameLabelBackgroundColorPreview != null)
                FrameLabelBackgroundColorPreview.Background = ParseBrushOrDefault(_node.FrameLabelBackgroundColor, Brushes.White);
        }

        private async Task ProbeSourceFpsAndRefreshUiAsync()
        {
            var path = _node.VideoPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                _isProbingFps = false;
                return;
            }

            _sourceFpsProbeCts?.Cancel();
            _sourceFpsProbeCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var ct = _sourceFpsProbeCts.Token;

            _isProbingFps = true;
            // Update UI immediately if we're on the UI thread, otherwise queue it
            if (Dispatcher.CheckAccess())
                StatFpsText.Text = "⏳";
            else
                await Dispatcher.InvokeAsync(() => StatFpsText.Text = "⏳");

            try
            {
                var (fps, duration) = await ProbeSourceMetadataAsync(path, ct);
                // Clear flag and update SourceFps & probed duration atomically on the UI thread
                // to prevent race where RefreshInfoText sees _isProbingFps=false but old SourceFps
                await Dispatcher.InvokeAsync(() =>
                {
                    _isProbingFps = false;
                    if (duration > 0)
                    {
                        _probedDurationSeconds = duration;
                    }
                    if (fps > 0)
                    {
                        _node.SourceFps = fps;
                    }
                    RefreshInfoText(); // includes UpdateFrameExtractionPreview()
                    UpdateGridCollagePreviewUi();
                    InitTimeSliderRange();
                }, DispatcherPriority.Loaded);
            }
            catch
            {
                // best-effort: don't block UI if probing fails.
                await Dispatcher.InvokeAsync(() =>
                {
                    _isProbingFps = false;
                    RefreshInfoText();
                });
            }
        }

        private static async Task<(double fps, double duration)> ProbeSourceMetadataAsync(string inputPath, CancellationToken ct)
        {
            var ffprobeExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffprobe");
            if (string.IsNullOrWhiteSpace(ffprobeExe)) return (0, 0);

            var psi = new ProcessStartInfo
            {
                FileName = ffprobeExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            var args = new[]
            {
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "stream=r_frame_rate:format=duration",
                "-of", "default=nokey=0:noprint_wrappers=1",
                inputPath
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p == null) return (0, 0);

            var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);

            double fps = 0;
            double duration = 0;

            foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("r_frame_rate=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = line.Substring("r_frame_rate=".Length).Trim();
                    if (val.Contains('/'))
                    {
                        var parts = val.Split('/');
                        if (parts.Length == 2 &&
                            double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) &&
                            double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) &&
                            d > 0)
                        {
                            fps = n / d;
                        }
                    }
                    else if (double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                    {
                        fps = f;
                    }
                }
                else if (line.StartsWith("duration=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = line.Substring("duration=".Length).Trim();
                    if (double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dur))
                    {
                        duration = dur;
                    }
                }
            }

            return (fps, duration);
        }

        public void ToggleVideoTrimSegmentPlayback()
        {
            if (PreviewMedia.Source == null) return;
            if (_isPlaying && _isVideoTrimPreviewing)
            {
                PreviewMedia.Pause();
                _realTimeAudioEngine?.Pause();
                _isPlaying = false;
                _isVideoTrimPreviewing = false;
                if (LiveDot != null) LiveDot.Visibility = Visibility.Collapsed;
                UpdatePlaybackUi();
                return;
            }

            var duration = GetNaturalDurationSeconds();
            var startSec = Math.Max(0, _node.TrimStartSec);
            var endSec = _node.TrimEndSec > startSec ? Math.Min(duration, _node.TrimEndSec) : duration;
            if (endSec <= startSec) return;

            _isVideoTrimPreviewing = true;
            _videoTrimPreviewStartTime = DateTime.UtcNow;
            var startPos = TimeSpan.FromSeconds(startSec);
            PreviewMedia.Position = startPos;
            _realTimeAudioEngine?.Seek(startPos);

            EnsureRealTimeAudioEngineLoaded();
            if (_realTimeAudioEngine != null && _realTimeAudioEngine.IsLoaded)
            {
                PreviewMedia.IsMuted = true;
                _realTimeAudioEngine.Seek(startPos);
                _realTimeAudioEngine.ApplyParameters(_node, _node.PreviewVolume, _isDspAudioPreviewActive);
                _realTimeAudioEngine.Play();
            }
            else
            {
                UpdatePreviewAudioVolume();
            }

            PreviewMedia.Play();
            _isPlaying = true;
            if (LiveDot != null) LiveDot.Visibility = Visibility.Visible;
            UpdatePlaybackUi();
        }

        private void TogglePlayPause()
        {
            if (PreviewMedia.Source == null) return;
            if (_isPlaying)
            {
                PreviewMedia.Pause();
                _realTimeAudioEngine?.Pause();
                _audioTrackPreviewPlayer.Pause();
                _isPlaying = false;
                LiveDot.Visibility = Visibility.Collapsed;
            }
            else
            {
                var dur = GetNaturalDurationSeconds();
                if (dur > 0 && PreviewMedia.Position.TotalSeconds >= (dur - 0.25))
                {
                    PreviewMedia.Position = TimeSpan.Zero;
                    _realTimeAudioEngine?.Seek(TimeSpan.Zero);
                }

                EnsureRealTimeAudioEngineLoaded();
                if (_realTimeAudioEngine != null && _realTimeAudioEngine.IsLoaded)
                {
                    PreviewMedia.IsMuted = true;
                    _realTimeAudioEngine.Seek(PreviewMedia.Position);
                    _realTimeAudioEngine.ApplyParameters(_node, _node.PreviewVolume, _isDspAudioPreviewActive);
                    _realTimeAudioEngine.Play();
                }
                else
                {
                    UpdatePreviewAudioVolume();
                }
                PreviewMedia.Play();
                _isPlaying = true;
                LiveDot.Visibility = Visibility.Visible;
            }
            UpdatePlaybackUi();
        }

        private void StopPlayback()
        {
            if (PreviewMedia.Source == null) return;
            PreviewMedia.Stop();
            _realTimeAudioEngine?.Stop();
            _audioTrackPreviewPlayer.Close();
            PreviewMedia.Position = TimeSpan.Zero;
            _isPlaying = false;
            LiveDot.Visibility = Visibility.Collapsed;
            UpdatePlaybackUi();
        }

        private void SeekRelativeSeconds(double deltaSeconds)
        {
            if (PreviewMedia.Source == null) return;
            var target = PreviewMedia.Position + TimeSpan.FromSeconds(deltaSeconds);
            var duration = TimeSpan.FromSeconds(GetNaturalDurationSeconds());
            if (target < TimeSpan.Zero) target = TimeSpan.Zero;
            if (target > duration) target = duration;
            PreviewMedia.Position = target;
            _realTimeAudioEngine?.Seek(target);
            UpdatePlaybackUi();
        }

        private double GetSeekRatioByMousePosition(MouseEventArgs e)
        {
            var pos = e.GetPosition(ProgressBarHitArea);
            if (ProgressBarHitArea.ActualWidth <= 0) return 0;
            return Math.Clamp(pos.X / ProgressBarHitArea.ActualWidth, 0, 1);
        }

        private void SeekToRatio(double ratio)
        {
            if (PreviewMedia.Source == null) return;
            var duration = GetNaturalDurationSeconds();
            var targetSec = ratio * duration;
            _lastSeekRequestAtUtc = DateTime.UtcNow;
            _lastSeekTargetSeconds = targetSec;
            _isSeekLatencyPending = true;
            PreviewMedia.Position = TimeSpan.FromSeconds(targetSec);
            _realTimeAudioEngine?.Seek(TimeSpan.FromSeconds(targetSec));
        }

        public void SeekVideoPlayerTo(double seconds)
        {
            if (PreviewMedia.Source == null) return;
            var duration = GetNaturalDurationSeconds();
            var targetSec = Math.Clamp(seconds, 0, duration > 0 ? duration : double.MaxValue);
            _lastSeekRequestAtUtc = DateTime.UtcNow;
            _lastSeekTargetSeconds = targetSec;
            _isSeekLatencyPending = true;
            PreviewMedia.Position = TimeSpan.FromSeconds(targetSec);
            _realTimeAudioEngine?.Seek(TimeSpan.FromSeconds(targetSec));
            UpdatePlaybackUi();
        }

        private void UpdateProgressVisualByRatio(double ratio)
        {
            var barWidth = ProgressBarHitArea.ActualWidth;
            var availableWidth = Math.Max(1, barWidth - 14);
            ProgressBarFill.Width = availableWidth * ratio;
            Canvas.SetLeft(ProgressThumb, Math.Clamp(availableWidth * ratio, 0, availableWidth));
            TimeCurrentText.Text = FormatTime(TimeSpan.FromSeconds(ratio * GetNaturalDurationSeconds()));
        }

        private void SeekByMousePosition(MouseEventArgs e, bool forceSeek = false)
        {
            if (PreviewMedia.Source == null) return;
            var ratio = GetSeekRatioByMousePosition(e);
            _pendingSeekRatio = ratio;

            if (forceSeek)
            {
                UpdateProgressVisualByRatio(ratio);
                return;
            }

            // During drag, update only visuals; commit seek on mouse up.
            UpdateProgressVisualByRatio(ratio);
        }

        private void ProgressBarHitArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (TrimReviewCheckBox.IsChecked == true) return;
            if (PreviewMedia.Source == null) return;
            _isProgressDragging = true;
            _dragReleaseBoostUntilUtc = DateTime.MinValue;
            ApplyPreviewQualitySettings();
            ProgressBarHitArea.CaptureMouse();
            _timelineDragMode = TimelineDragMode.Scrub;
            HandleTimelineDrag(e, commitSeekImmediately: false);
            e.Handled = true;
        }

        private void ProgressBarHitArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (TrimReviewCheckBox.IsChecked == true) return;
            if (PreviewMedia.Source == null) return;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                HandleTimelineDrag(e, commitSeekImmediately: false);
                e.Handled = true;
            }
        }

        private void ProgressBarHitArea_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TrimReviewCheckBox.IsChecked == true) return;
            if (PreviewMedia.Source == null) return;
            _isProgressDragging = false;
            _dragReleaseBoostUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
            ApplyPreviewQualitySettings();
            HandleTimelineDrag(e, commitSeekImmediately: true);
            _timelineDragMode = TimelineDragMode.None;
            if (ProgressBarHitArea.IsMouseCaptured)
            {
                ProgressBarHitArea.ReleaseMouseCapture();
            }
            ProgressThumb.Visibility = Visibility.Visible;
            e.Handled = true;
        }

        private void ProgressBarHitArea_MouseEnter(object sender, MouseEventArgs e)
        {
            ProgressThumb.Visibility = Visibility.Visible;
            ApplyTimelineThumbHoverVisual(true);
        }

        private void ProgressBarHitArea_MouseLeave(object sender, MouseEventArgs e)
        {
            ProgressThumb.Visibility = Visibility.Visible;
            ApplyTimelineThumbHoverVisual(false);
            e.Handled = true;
        }

        private void ApplyTimelineThumbHoverVisual(bool hover)
        {
            if (ProgressThumbScale != null)
            {
                ProgressThumbScale.ScaleX = hover ? 1.12 : 1.0;
                ProgressThumbScale.ScaleY = hover ? 1.12 : 1.0;
            }

            if (!hover)
            {
                ProgressThumb.Effect = null;
                return;
            }

            Color glow = Color.FromRgb(239, 68, 68);
            if (TryFindResource("ThemeSliderActiveBrush") is SolidColorBrush ab && ab.Color.A > 0)
                glow = ab.Color;
            ProgressThumb.Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.5,
                Color = glow
            };
        }

        private static bool IsClickFromInteractiveElement(object? source)
        {
            DependencyObject? current = source as DependencyObject;
            if (current == null && source is TextElement te)
                current = te.Parent as DependencyObject;

            while (current != null)
            {
                if (current is ButtonBase || current is Slider || current is ToggleButton ||
                    current is TextBox || current is ComboBox || current is ListBox)
                {
                    return true;
                }

                current = current switch
                {
                    Visual v => VisualTreeHelper.GetParent(v),
                    Visual3D v3 => VisualTreeHelper.GetParent(v3),
                    _ => null
                };
            }

            return false;
        }

        private void HandleTimelineDrag(MouseEventArgs e, bool commitSeekImmediately)
        {
            var ratio = GetSeekRatioByMousePosition(e);
            _pendingSeekRatio = ratio;

            // Scrub mode: only seek on mouse-up for smoother drag.
            UpdateProgressVisualByRatio(ratio);
            if (commitSeekImmediately)
            {
                SeekToRatio(ratio);
                _pendingSeekRatio = -1;
                UpdatePlaybackUi();
            }
        }

        private int GetDragSeekThrottleMs()
        {
            return GetEffectivePreviewQualityMode() switch
            {
                PreviewQualityMode.Low => DragSeekThrottleLowMs,
                PreviewQualityMode.High => DragSeekThrottleHighMs,
                _ => DragSeekThrottleNormalMs
            };
        }

        private void ApplyPreviewQualitySettings()
        {
            var mode = GetEffectivePreviewQualityMode();
            PreviewMedia.ScrubbingEnabled = mode == PreviewQualityMode.High;
            var scaling = mode switch
            {
                PreviewQualityMode.Low => BitmapScalingMode.LowQuality,
                PreviewQualityMode.High => BitmapScalingMode.HighQuality,
                _ => BitmapScalingMode.Linear
            };
            RenderOptions.SetBitmapScalingMode(PreviewMedia, scaling);
        }

        private PreviewQualityMode GetPreviewQualityMode()
        {
            var raw = (_node.PreviewQualityMode ?? "auto").ToLowerInvariant();
            if (int.TryParse(raw, out var qh))
            {
                if (qh <= 240) return PreviewQualityMode.Low;
                if (qh >= 720) return PreviewQualityMode.High;
                return PreviewQualityMode.Normal;
            }

            return raw switch
            {
                "auto" => PreviewQualityMode.Auto,
                "144" or "240" or "low" => PreviewQualityMode.Low,
                "720" or "1080" or "1440" or "2160" or "high" => PreviewQualityMode.High,
                _ => PreviewQualityMode.Normal
            };
        }

        private PreviewQualityMode GetEffectivePreviewQualityMode()
        {
            var configured = GetPreviewQualityMode();
            if (configured != PreviewQualityMode.Auto)
            {
                return configured;
            }

            if (_isProgressDragging)
            {
                return PreviewQualityMode.Low;
            }

            if (DateTime.UtcNow <= _dragReleaseBoostUntilUtc)
            {
                return PreviewQualityMode.High;
            }

            return PreviewQualityMode.Normal;
        }

        private string GetSelectedPreviewQualityTag()
        {
            if (PreviewQualityCombo.SelectedItem is ComboBoxItem selected &&
                selected.Tag is string tag &&
                !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            return "auto";
        }

        private int? GetConfiguredPreviewMaxHeight()
        {
            if (int.TryParse(_node.PreviewQualityMode, out var h) && h > 0)
                return h;
            return null;
        }

        private void RebuildPreviewQualityOptions(int sourceHeight)
        {
            if (PreviewQualityCombo == null) return;
            var selectedTag = _node.PreviewQualityMode ?? "auto";
            var maxHeight = sourceHeight > 0 ? sourceHeight : PreviewQualityLevelHeights[^1];

            _suppressControlSync = true;
            try
            {
                PreviewQualityCombo.Items.Clear();
                PreviewQualityCombo.Items.Add(new ComboBoxItem { Content = "Auto", Tag = "auto" });

                foreach (var q in PreviewQualityLevelHeights)
                {
                    if (q <= maxHeight)
                        PreviewQualityCombo.Items.Add(new ComboBoxItem { Content = $"{q}p", Tag = q.ToString() });
                }

                if (sourceHeight > 0 && !PreviewQualityLevelHeights.Contains(sourceHeight))
                    PreviewQualityCombo.Items.Add(new ComboBoxItem { Content = $"{sourceHeight}p (Native)", Tag = sourceHeight.ToString() });

                if (!SelectPreviewQualityByTag(selectedTag))
                {
                    // If previously selected quality is no longer available, clamp to highest supported.
                    if (sourceHeight > 0)
                    {
                        var clamped = PreviewQualityLevelHeights.Where(x => x <= sourceHeight).DefaultIfEmpty(sourceHeight).Max();
                        if (!SelectPreviewQualityByTag(clamped.ToString()))
                            SelectPreviewQualityByTag("auto");
                    }
                    else
                    {
                        SelectPreviewQualityByTag("auto");
                    }
                }
            }
            finally
            {
                _suppressControlSync = false;
            }
        }

        private bool SelectPreviewQualityByTag(string tag)
        {
            for (var i = 0; i < PreviewQualityCombo.Items.Count; i++)
            {
                if (PreviewQualityCombo.Items[i] is ComboBoxItem cbi &&
                    string.Equals(cbi.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    PreviewQualityCombo.SelectedIndex = i;
                    _node.PreviewQualityMode = cbi.Tag?.ToString() ?? "auto";
                    return true;
                }
            }

            return false;
        }

        private string GetSelectedPreviewVisualStrengthTag()
        {
            if (PreviewVisualStrengthCombo.SelectedItem is ComboBoxItem selected &&
                selected.Tag is string tag &&
                !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            return "balanced";
        }

        private void UpdatePlaybackUi()
        {
            if (GetPreviewQualityMode() == PreviewQualityMode.Auto && !_isProgressDragging)
            {
                ApplyPreviewQualitySettings();
            }

            // Skip excluded frames khi đang play
            if (_isPlaying && !_isProgressDragging && _node.ExcludedFrameTimestamps.Count > 0)
            {
                var curSec = PreviewMedia.Position.TotalSeconds;
                if (_node.IsFrameExcluded(curSec))
                {
                    var frameDur = _node.SourceFps > 0 ? 1.0 / _node.SourceFps : 0.04;
                    PreviewMedia.Position = TimeSpan.FromSeconds(curSec + frameDur);
                }
            }

            // Stop if previewing trimmed audio segment (with grace period to avoid seek race conditions)
            if (_isPlaying && _isAudioTrimPreviewing && _node.AudioTrimEndSec > _node.AudioTrimStartSec)
            {
                var curPos = PreviewMedia.Position.TotalSeconds;
                var elapsedMs = (DateTime.UtcNow - _audioTrimPreviewStartTime).TotalMilliseconds;
                if (elapsedMs > 250 && curPos >= (_node.AudioTrimEndSec - 0.05) && curPos > (_node.AudioTrimStartSec + 0.05))
                {
                    PreviewMedia.Pause();
                    _realTimeAudioEngine?.Pause();
                    _isPlaying = false;
                    _isAudioTrimPreviewing = false;
                    var startPos = TimeSpan.FromSeconds(Math.Max(0, _node.AudioTrimStartSec));
                    PreviewMedia.Position = startPos;
                    _realTimeAudioEngine?.Seek(startPos);
                    if (LiveDot != null) LiveDot.Visibility = Visibility.Collapsed;
                    UpdatePlaybackUi();
                }
            }

            // Stop if previewing trimmed video segment (starts from TrimStartSec and stops at TrimEndSec)
            if (_isPlaying && _isVideoTrimPreviewing && _node.TrimEndSec > _node.TrimStartSec)
            {
                var curPos = PreviewMedia.Position.TotalSeconds;
                var elapsedMs = (DateTime.UtcNow - _videoTrimPreviewStartTime).TotalMilliseconds;
                if (elapsedMs > 200 && curPos >= (_node.TrimEndSec - 0.05) && curPos > (_node.TrimStartSec + 0.05))
                {
                    PreviewMedia.Pause();
                    _realTimeAudioEngine?.Pause();
                    _isPlaying = false;
                    _isVideoTrimPreviewing = false;
                    var endPos = TimeSpan.FromSeconds(_node.TrimEndSec);
                    PreviewMedia.Position = endPos;
                    _realTimeAudioEngine?.Seek(endPos);
                    if (LiveDot != null) LiveDot.Visibility = Visibility.Collapsed;
                    UpdatePlaybackUi();
                }
            }

            // Check if video reached end during continuous playback
            var duration = TimeSpan.FromSeconds(GetNaturalDurationSeconds());
            var position = PreviewMedia.Position;
            if (_isPlaying && duration.TotalSeconds > 0.4 && position.TotalSeconds >= (duration.TotalSeconds - 0.12))
            {
                if (_isVideoLooping)
                {
                    PreviewMedia.Position = TimeSpan.Zero;
                    _realTimeAudioEngine?.Seek(TimeSpan.Zero);
                    _realTimeAudioEngine?.Play();
                    position = TimeSpan.Zero;
                }
                else
                {
                    PreviewMedia.Stop();
                    PreviewMedia.Position = TimeSpan.Zero;
                    _isPlaying = false;
                    _realTimeAudioEngine?.Stop();
                    _realTimeAudioEngine?.Seek(TimeSpan.Zero);
                    if (LiveDot != null) LiveDot.Visibility = Visibility.Collapsed;
                    position = TimeSpan.Zero;
                }
            }

            var ratio = duration.TotalSeconds > 0 ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1) : 0;
            if (_isProgressDragging && _pendingSeekRatio >= 0)
            {
                ratio = _pendingSeekRatio;
                position = TimeSpan.FromSeconds(ratio * duration.TotalSeconds);
            }
            var barWidth = ProgressBarHitArea.ActualWidth;
            var availableWidth = Math.Max(1, barWidth - 14);
            ProgressBarFill.Width = availableWidth * ratio;
            Canvas.SetLeft(ProgressThumb, Math.Clamp(availableWidth * ratio, 0, availableWidth));
            TimeCurrentText.Text = FormatTime(position);
            TimeTotalText.Text = FormatTime(duration);
            _currentPlayheadSec = position.TotalSeconds;
            UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            if (PreviewMedia.Source != null && _node.SourceFps > 0)
            {
                var currentSec = PreviewMedia.Position.TotalSeconds;
                var currentFrame = (int)(currentSec * _node.SourceFps);
                FrameInfoText.Text =
                    $"Frame #{currentFrame:N0}  |  {_node.SourceFps:0.##} fps  |  " +
                    $"{(PreviewMedia.NaturalVideoWidth > 0 ? $"{PreviewMedia.NaturalVideoWidth}x{PreviewMedia.NaturalVideoHeight}" : "--")}";
            }

            SyncTrimWaveformPlayhead(position.TotalSeconds);

            if (_isSeekLatencyPending && _lastSeekTargetSeconds >= 0)
            {
                var acceptedDiff = GetEffectivePreviewQualityMode() switch
                {
                    PreviewQualityMode.Low => 0.32,
                    PreviewQualityMode.High => 0.08,
                    _ => 0.16
                };

                if (Math.Abs(position.TotalSeconds - _lastSeekTargetSeconds) <= acceptedDiff)
                {
                    _lastSeekLatencyMs = Math.Max(0, (DateTime.UtcNow - _lastSeekRequestAtUtc).TotalMilliseconds);
                    _isSeekLatencyPending = false;
                }
            }

            var effectiveModeText = GetEffectivePreviewQualityMode() switch
            {
                PreviewQualityMode.Low => "LOW",
                PreviewQualityMode.High => "HIGH",
                _ => "NORMAL"
            };
            var configuredModeText = GetPreviewQualityMode() switch
            {
                PreviewQualityMode.Auto => "AUTO",
                PreviewQualityMode.Low => "LOW",
                PreviewQualityMode.High => "HIGH",
                _ => "NORMAL"
            };
            var latencyText = _lastSeekLatencyMs >= 0 ? $"{_lastSeekLatencyMs:0} ms" : "-- ms";
            SetTextIfExists("SeekPerfText", $"Preview: {configuredModeText}/{effectiveModeText} | Seek: {latencyText}");
            PlayPauseButton.Content = CreateTransportIcon(_isPlaying ? "pause regular" : "play regular");
            UpdateFrameLabelPreviewUi();
            UpdateTrimReviewUi();
        }

        private void TrimReviewHitArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_node.TrimEnabled || PreviewMedia.Source == null) return;
            _isTrimReviewDragging = true;
            _trimReviewDragMode = ResolveTrimReviewDragMode(e);
            TrimReviewHitArea.CaptureMouse();
            HandleTrimReviewDrag(e, commitPreviewSeek: true);
            e.Handled = true;
        }

        private void TrimReviewHitArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_node.TrimEnabled || PreviewMedia.Source == null) return;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                HandleTrimReviewDrag(e, commitPreviewSeek: false);
                e.Handled = true;
            }
        }

        private void TrimReviewHitArea_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_node.TrimEnabled || PreviewMedia.Source == null) return;
            var draggedMode = _trimReviewDragMode;
            HandleTrimReviewDrag(e, commitPreviewSeek: true);
            _isTrimReviewDragging = false;
            _trimReviewDragMode = TimelineDragMode.None;
            if (TrimReviewHitArea.IsMouseCaptured) TrimReviewHitArea.ReleaseMouseCapture();
            e.Handled = true;

            // Load preview frames immediately on release.
            if (draggedMode == TimelineDragMode.TrimStart)
            {
                _ = LoadTrimFramePreviewAsync(isStart: true);
            }
            else if (draggedMode == TimelineDragMode.TrimEnd)
            {
                _ = LoadTrimFramePreviewAsync(isStart: false);
            }
            else
            {
                _ = LoadTrimFramePreviewAsync(isStart: true);
                _ = LoadTrimFramePreviewAsync(isStart: false);
            }
        }

        private void TrimReviewHitArea_MouseLeave(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed && TrimReviewHitArea.IsMouseCaptured)
            {
                TrimReviewHitArea.ReleaseMouseCapture();
                _trimReviewDragMode = TimelineDragMode.None;
                _isTrimReviewDragging = false;
            }

            if (TrimReviewTrackBorder != null) TrimReviewTrackBorder.Opacity = 0.7;
            if (TrimReviewRangeFill != null) TrimReviewRangeFill.Opacity = 0.95;
        }

        private void TrimReviewHitArea_MouseEnter(object sender, MouseEventArgs e)
        {
            // Provide visual cue when interacting with trim slider.
            if (TrimReviewTrackBorder != null) TrimReviewTrackBorder.Opacity = 1.0;
            if (TrimReviewRangeFill != null) TrimReviewRangeFill.Opacity = 1.0;
        }

        public async System.Threading.Tasks.Task LoadTrimFramePreviewAsync(bool isStart)
        {
            var requestId = isStart ? ++_trimStartFrameRequestId : ++_trimEndFrameRequestId;

            if (string.IsNullOrWhiteSpace(_node.VideoPath) || !System.IO.File.Exists(_node.VideoPath)) return;

            var duration = GetNaturalDurationSeconds();
            if (duration <= 0) duration = 1;

            var t = isStart
                ? Math.Clamp(_node.TrimStartSec, 0, duration)
                : Math.Clamp((_node.TrimEndSec > 0 ? _node.TrimEndSec : duration), 0, duration);

            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"FlowMy_trim_{(isStart ? "start" : "end")}_{Guid.NewGuid():N}.jpg");

            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (isStart)
                    {
                        if (TrimStartFrameHintText != null && TrimStartFrameImage?.Source == null)
                        {
                            TrimStartFrameHintText.Text = "⏳ Đang load...";
                            TrimStartFrameHintText.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        if (TrimEndFrameHintText != null && TrimEndFrameImage?.Source == null)
                        {
                            TrimEndFrameHintText.Text = "⏳ Đang load...";
                            TrimEndFrameHintText.Visibility = Visibility.Visible;
                        }
                    }
                });

                await VideoProcessingNodeExecutor.RunSnapshotAsync(
                    _node.VideoPath,
                    t.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    tmp,
                    System.Threading.CancellationToken.None).ConfigureAwait(false);

                if (isStart ? (requestId != _trimStartFrameRequestId) : (requestId != _trimEndFrameRequestId))
                {
                    try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); } catch { }
                    return;
                }

                if (System.IO.File.Exists(tmp))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new System.Uri(tmp, System.UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    try { System.IO.File.Delete(tmp); } catch { }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (isStart)
                        {
                            TrimStartFrameImage.Source = bmp;
                            TrimStartFrameImage.Visibility = Visibility.Visible;
                            TrimStartFrameHintText.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            TrimEndFrameImage.Source = bmp;
                            TrimEndFrameImage.Visibility = Visibility.Visible;
                            TrimEndFrameHintText.Visibility = Visibility.Collapsed;
                        }
                    });
                }
            }
            catch
            {
                // best-effort
            }
        }

        private TimelineDragMode ResolveTrimReviewDragMode(MouseEventArgs e)
        {
            var duration = GetNaturalDurationSeconds();
            var barWidth = TrimReviewHitArea.ActualWidth;
            if (duration <= 0 || barWidth <= 1) return TimelineDragMode.Scrub;

            var availableWidth = Math.Max(1, barWidth - 14);
            var pos = e.GetPosition(TrimReviewHitArea);
            var startX = Math.Clamp(_node.TrimStartSec / duration, 0, 1) * availableWidth + 7;
            var endSec = _node.TrimEndSec > 0 ? _node.TrimEndSec : duration;
            var endX = Math.Clamp(endSec / duration, 0, 1) * availableWidth + 7;
            const double handleHitRange = 24;

            var distStart = Math.Abs(pos.X - startX);
            var distEnd = Math.Abs(pos.X - endX);

            if (distStart <= handleHitRange && distStart <= distEnd) return TimelineDragMode.TrimStart;
            if (distEnd <= handleHitRange) return TimelineDragMode.TrimEnd;

            if (pos.X < startX) return TimelineDragMode.TrimStart;
            if (pos.X > endX) return TimelineDragMode.TrimEnd;

            return TimelineDragMode.Scrub;
        }

        private void HandleTrimReviewDrag(MouseEventArgs e, bool commitPreviewSeek)
        {
            var duration = GetNaturalDurationSeconds();
            var barWidth = TrimReviewHitArea.ActualWidth;
            if (duration <= 0 || barWidth <= 1) return;
            var availableWidth = Math.Max(1, barWidth - 14);
            var clickX = e.GetPosition(TrimReviewHitArea).X - 7;
            var ratio = Math.Clamp(clickX / availableWidth, 0, 1);
            var targetSec = ratio * duration;

            if (_trimReviewDragMode == TimelineDragMode.TrimStart)
            {
                var end = _node.TrimEndSec > 0 ? _node.TrimEndSec : duration;
                _node.TrimStartSec = Math.Clamp(targetSec, 0, Math.Max(0, end - 0.05));
                if (commitPreviewSeek) PreviewMedia.Position = TimeSpan.FromSeconds(_node.TrimStartSec);
            }
            else if (_trimReviewDragMode == TimelineDragMode.TrimEnd)
            {
                var start = Math.Max(0, _node.TrimStartSec + 0.05);
                _node.TrimEndSec = Math.Clamp(targetSec, start, duration);
                if (commitPreviewSeek) PreviewMedia.Position = TimeSpan.FromSeconds(_node.TrimEndSec);
            }
            else
            {
                var start = _node.TrimStartSec;
                var end = _node.TrimEndSec > 0 ? _node.TrimEndSec : duration;
                targetSec = Math.Clamp(targetSec, start, end);
                if (commitPreviewSeek) PreviewMedia.Position = TimeSpan.FromSeconds(targetSec);
            }

            RefreshInfoText();
            UpdateTrimReviewUi();
        }

        private void UpdateTrimReviewUi()
        {
            if (TrimReviewHitArea == null || TrimReviewRangeFill == null || PreviewMedia == null) return;
            if (!_node.TrimEnabled || TrimReviewHitArea.Visibility != Visibility.Visible)
            {
                return;
            }

            var duration = GetNaturalDurationSeconds();
            var width = TrimReviewHitArea.ActualWidth;
            if (duration <= 0 || width <= 1) return;

            var availableWidth = Math.Max(1, width - 14);
            var startRatio = Math.Clamp(_node.TrimStartSec / duration, 0, 1);
            var endSec = _node.TrimEndSec > 0 ? _node.TrimEndSec : duration;
            var endRatio = Math.Clamp(endSec / duration, 0, 1);
            if (endRatio < startRatio) (startRatio, endRatio) = (endRatio, startRatio);

            var targetStartX = startRatio * availableWidth;
            var targetEndX = endRatio * availableWidth;
            var playRatio = Math.Clamp(PreviewMedia.Position.TotalSeconds / duration, 0, 1);
            var targetPlayX = playRatio * availableWidth;

            const double ease = 0.38;
            if (!_trimUiInitialized)
            {
                _trimUiStartX = targetStartX;
                _trimUiEndX = targetEndX;
                _trimUiPlayX = targetPlayX;
                _trimUiInitialized = true;
            }
            else if (_isTrimReviewDragging)
            {
                _trimUiStartX = targetStartX;
                _trimUiEndX = targetEndX;
                _trimUiPlayX = targetPlayX;
            }
            else
            {
                // Ease marker movement to reduce jitter while preserving responsiveness.
                _trimUiStartX += (targetStartX - _trimUiStartX) * ease;
                _trimUiEndX += (targetEndX - _trimUiEndX) * ease;
                _trimUiPlayX += (targetPlayX - _trimUiPlayX) * ease;
            }

            var left = Math.Min(_trimUiStartX, _trimUiEndX) + 7.0;
            var right = Math.Max(_trimUiStartX, _trimUiEndX) + 7.0;
            TrimReviewRangeFill.Width = Math.Max(0, right - left);
            TrimReviewRangeFill.Margin = new Thickness(left, 0, 0, 0);
            Canvas.SetLeft(TrimReviewStartThumb, _trimUiStartX);
            Canvas.SetLeft(TrimReviewEndThumb, _trimUiEndX);
            Canvas.SetLeft(TrimReviewPlayheadThumb, _trimUiPlayX);
        }

        private double _probedDurationSeconds = 0;

        private double GetNaturalDurationSeconds()
        {
            if (_probedDurationSeconds > 0) return _probedDurationSeconds;
            return PreviewMedia.NaturalDuration.HasTimeSpan ? PreviewMedia.NaturalDuration.TimeSpan.TotalSeconds : 0;
        }

    }
}
