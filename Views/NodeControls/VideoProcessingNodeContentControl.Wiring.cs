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
        private void InitializeInteractiveControls()
        {
            WireNavigationAndActions();
            WirePreviewAndFileEvents();
            WireTransportAndGeneralSettings();
            WireGradingAndTransformEvents();
            WireTrimAndOverlayEvents();
            WireExtractionAndWatermarkEvents();
            WireFrameLabelAndExportEvents();
        }

        private void WireNavigationAndActions()
        {
            TabNavList.SelectionChanged += TabNavList_SelectionChanged;
            TabNavList.SelectedIndex = 0;
            /* Trước đây: hover vào từng BottomBarGroup* → bung label các nút trong nhóm đó (kèm đo width dòng trong UpdateActionButtonLabelVisibility).
               Giữ lại chỉ bung theo tab active — không cần MouseEnter/MouseLeave. */
            /*
            var actionGroups = new[]
            {
                BottomBarGroupGeneral, BottomBarGroupGrading, BottomBarGroupFilters,
                BottomBarGroupAudio, BottomBarGroupExport, BottomBarGroupOutputs, BottomBarGroupSettings
            };
            for (var i = 0; i < actionGroups.Length; i++)
            {
                var idx = i;
                var group = actionGroups[i];
                group.MouseEnter += (_, _) =>
                {
                    _hoveredActionGroupIndex = idx;
                    UpdateActionButtonLabelVisibility();
                };
            }
            ActionButtonsBorder.MouseLeave += (_, _) =>
            {
                _hoveredActionGroupIndex = -1;
                UpdateActionButtonLabelVisibility();
            };
            */
            SizeChanged += (_, _) =>
            {
                SyncUserControlRoundedClip();
                RefreshLargeNodeUiScale();
                UpdatePreviewAspectRatio();
                UpdateVideoLogColumnLayout();
            };
            VideoViewbox.SizeChanged += (_, _) =>
            {
                SyncVideoViewportClip();
                UpdateOverlayCanvasBounds();
                UpdateWatermarkPreviewUi();
            };
            VideoViewportClipBorder.SizeChanged += (_, _) =>
            {
                SyncVideoViewportClip();
                UpdateOverlayCanvasBounds();
                UpdateWatermarkPreviewUi();
            };

            OpenVideoButtonHeader.Click += (_, _) => SelectVideo();
            //OpenVideoInPlaceholderButton.Click += (_, _) => SelectVideo();
            Aspect169.Checked += (_, _) => SetAspectRatio(16, 9, false);
            Aspect916.Checked += (_, _) => SetAspectRatio(9, 16, false);
            Aspect11.Checked += (_, _) => SetAspectRatio(1, 1, false);
            Aspect32.Checked += (_, _) => SetAspectRatio(3, 2, false);
            Aspect23.Checked += (_, _) => SetAspectRatio(2, 3, false);
            AspectAuto.Checked += (_, _) => SetAspectRatio(0, 0, true);
            ThemeModeButton.Click += (_, _) =>
            {
                _isLightTheme = !_isLightTheme;
                ApplyLocalTheme();
            };
            ToggleNodeSizeButton.Click += (_, _) => ToggleNodeZoom();
            RunProcessingButton.Click += (_, _) =>
            {
                SwitchToLogView();
                RunProcessingFlow(singleNodeOnly: false);
            };
            SaveEditedVideoButton.Click += (_, _) =>
            {
                SwitchToLogView();
                _node.ExportVideoEnabled = true;
                _node.ExtractFramesEnabled = false;
                _node.ExtractAudioEnabled = false;
                AppendLog($"💾 [LƯU VIDEO] Đang xử lý và xuất video vào thư mục: {GetCurrentVideoOutputFolder()}");
                RunProcessingFlow(singleNodeOnly: true);
            };
            SaveTrimConcatTabButton.Click += (_, _) =>
            {
                SwitchToLogView();
                _node.ExportVideoEnabled = true;
                _node.ExtractFramesEnabled = false;
                _node.ExtractAudioEnabled = false;
                AppendLog($"💾 [LƯU VIDEO] Đang xử lý và xuất video vào thư mục: {GetCurrentVideoOutputFolder()}");
                RunProcessingFlow(singleNodeOnly: true);
            };
            LoadTrimConcatPreviewButton.Click += async (_, _) =>
            {
                await LoadTrimConcatToPreviewAsync();
            };
            ExtractFramesBase64Button.Click += (_, _) =>
            {
                _node.OutputBase64 = true;
                _node.ExtractFramesEnabled = true;
                _node.ExportVideoEnabled = false;
                _node.ExtractAudioEnabled = false;
                RefreshOutputsSummaryUi();
                AppendLog("📷 [TÁCH FRAME] Đang trích xuất frame (Base64)...");
                RunProcessingFlow(singleNodeOnly: true);
            };
            ExtractFramesLinkButton.Click += (_, _) =>
            {
                _node.OutputBase64 = false;
                _node.ExtractFramesEnabled = true;
                _node.ExportVideoEnabled = false;
                _node.ExtractAudioEnabled = false;
                RefreshOutputsSummaryUi();
                AppendLog("📷 [TÁCH FRAME] Đang trích xuất frame (Đường dẫn file)...");
                RunProcessingFlow(singleNodeOnly: true);
            };
            ExtractAudioTabButton.Click += (_, _) =>
            {
                SwitchToLogView();
                _node.ExtractAudioEnabled = true;
                _node.ExportVideoEnabled = false;
                _node.ExtractFramesEnabled = false;
                AppendLog("🎵 [TRÍCH XUẤT AUDIO] Đang trích xuất file audio từ video...");
                RunProcessingFlow(singleNodeOnly: true);
            };
            RunFlowAudioTabButton.Click += (_, _) =>
            {
                SwitchToLogView();
                _node.ExtractAudioEnabled = true;
                _node.ExportVideoEnabled = false;
                _node.ExtractFramesEnabled = false;
                AppendLog("🚀 [CHẠY WORKFLOW - AUDIO] Đang trích xuất audio và kích hoạt chạy workflow...");
                RunProcessingFlow(singleNodeOnly: false);
            };
            SnapshotButton.Click += (_, _) =>
            {
                SwitchToLogView();
                TakeSnapshot();
            };
            ResetGradingButton2.Click += (_, _) => ResetGradingTabToDefaults();
            ResetFiltersTabButton.Click += (_, _) => ResetFiltersTabToDefaults();
            ResetAudioTabButton.Click += (_, _) => ResetAudioTabToDefaults();
            ResetTrimConcatTabButton.Click += (_, _) => ResetTrimConcatTabToDefaults();
            ResetSettingsTabButton.Click += (_, _) => ResetSettingsTabToDefaults();
            SaveSettingsTabButton.Click += (_, _) => SaveSettingsTabConfig();
            ConcatToggle.Checked += (_, _) => _node.ConcatEnabled = true;
            ConcatToggle.Unchecked += (_, _) => _node.ConcatEnabled = false;
            AddConcatVideoButton.Click += (_, _) => _node.ConcatVideos.Add(new VideoConcatItemConfig());
            ToggleQuickGradeButton.Click += (_, _) =>
            {
                QuickGradingPanel.Visibility = QuickGradingPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };
            QuickSnapshotButton.Click += (_, _) => TakeSnapshot();
            QuickSetTrimButton.Click += (_, _) =>
            {
                _node.TrimStartSec = PreviewMedia.Position.TotalSeconds;
                _node.TrimEnabled = true;
                TabNavList.SelectedIndex = 4;
                TrimToggle.IsChecked = true;
                RefreshInfoText();
                AppendLog($"✂ Trim start đặt tại: {FormatTime(PreviewMedia.Position)}");
            };
        }

        private void WirePreviewAndFileEvents()
        {
            QuickBrightnessSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _previewEffectTemporarilyDisabled = false;
                _node.Brightness = e.NewValue;
                QuickBrightnessLabel.Text = $"{e.NewValue:0.#}";
                BrightnessLabel.Text = $"{e.NewValue:0.##}";
                _suppressControlSync = true;
                BrightnessSlider.Value = e.NewValue;
                _suppressControlSync = false;
                ApplyPreviewColorTransform();
            };
            QuickContrastSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _previewEffectTemporarilyDisabled = false;
                _node.Contrast = e.NewValue;
                QuickContrastLabel.Text = $"{e.NewValue:0.#}";
                ContrastLabel.Text = $"{e.NewValue:0.##}";
                _suppressControlSync = true;
                ContrastSlider.Value = e.NewValue;
                _suppressControlSync = false;
                ApplyPreviewColorTransform();
            };
            QuickSaturationSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _previewEffectTemporarilyDisabled = false;
                _node.Saturation = e.NewValue;
                QuickSaturationLabel.Text = $"{e.NewValue:0.#}";
                SaturationLabel.Text = $"{e.NewValue:0.##}";
                _suppressControlSync = true;
                SaturationSlider.Value = e.NewValue;
                _suppressControlSync = false;
                ApplyPreviewColorTransform();
            };
            VideoAreaGrid.MouseEnter += (_, _) =>
            {
                if (PreviewMedia.Source != null)
                    FrameInfoOverlay.Visibility = Visibility.Visible;
            };
            VideoAreaGrid.MouseLeave += (_, _) => FrameInfoOverlay.Visibility = Visibility.Collapsed;
            VideoAreaGrid.MouseLeftButtonUp += (_, e) =>
            {
                if (PreviewMedia.Source != null &&
                    !IsClickFromInteractiveElement(e.OriginalSource))
                {
                    TogglePlayPause();
                    e.Handled = true;
                }
            };

            PreviewContainerBorder.AllowDrop = true;
            PreviewContainerBorder.DragOver += (_, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            };
            PreviewContainerBorder.Drop += (_, e) =>
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                var video = files?.FirstOrDefault(f =>
                    new[] { ".mp4", ".mov", ".mkv", ".avi", ".webm" }
                        .Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()));
                if (video == null) return;
                StopComparePreviewMode();
                _node.VideoPath = video;
                _node.RaisePropertyChanged(nameof(VideoProcessingNode.VideoPath));
            };
            OpenOutputVideoButton.Click += (_, _) => OpenPathFromText(OutputVideoPathText.Text);
            OpenOutputVideoActionButton.Click += (_, _) => OpenPathFromText(OutputVideoPathText.Text);
            OpenFramesFolderButton.Click += (_, _) => OpenPathFromText(OutputFramesFolderText.Text);
            OpenFramesFolderActionButton.Click += (_, _) => OpenPathFromText(OutputFramesFolderText.Text);
            CopyLogButton.Click += (_, _) =>
            {
                if (LogRichTextBox.Document != null)
                {
                    var range = new TextRange(LogRichTextBox.Document.ContentStart, LogRichTextBox.Document.ContentEnd);
                    if (!string.IsNullOrWhiteSpace(range.Text))
                        Clipboard.SetText(range.Text);
                }
            };

            OpenGlobalEnvironmentPathsButton.Click += (_, _) =>
            {
                var owner = Window.GetWindow(this);
                var dlg = new EnvironmentPathsConfigDialog(owner);
                dlg.ShowDialog();
            };
            BrowseFrameOutputFolderButton.Click += (_, _) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Chọn thư mục lưu frame ảnh"
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    FrameOutputFolderText.Text = dlg.SelectedPath;
            };
            BrowseAudioOutputFolderButton.Click += (_, _) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Chon thu muc luu audio"
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    AudioOutputFolderText.Text = dlg.SelectedPath;
            };
            BrowseDefaultOutputVideoButton.Click += (_, _) =>
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "MP4|*.mp4|WebM|*.webm|All|*.*",
                    Title = "Chọn đường dẫn video đầu ra mặc định"
                };
                if (dlg.ShowDialog() == true)
                    DefaultOutputVideoPathText.Text = dlg.FileName;
            };
            DefaultOutputVideoPathText.TextChanged += (_, _) =>
            {
                var value = DefaultOutputVideoPathText.Text?.Trim() ?? string.Empty;
                _node.DefaultOutputVideoPath = value;
                if (!_node.UseDialogVideoConfig)
                    _node.OutputPathOverride = value;
                OutputPathText.Text = value;
                RefreshOutputsSummaryUi();
            };
            FrameOutputFolderText.TextChanged += (_, _) =>
            {
                _node.FrameOutputFolderPath = FrameOutputFolderText.Text?.Trim();
                RefreshOutputsSummaryUi();
            };
            AudioOutputFolderText.TextChanged += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.AudioOutputFolderPath = AudioOutputFolderText.Text?.Trim();
                RefreshOutputsSummaryUi();
            };
            OutputPathText.TextChanged += (_, _) => RefreshOutputsSummaryUi();

            PreviewMedia.MediaOpened += async (_, _) =>
            {
                // Do NOT auto-play here - wait for user click.
                // Just seek to frame 0 and prepare UI.
                try
                {
                    // Force first-frame render so user sees preview immediately.
                    PreviewMedia.Play();
                    PreviewMedia.Pause();
                    PreviewMedia.Position = TimeSpan.FromMilliseconds(1);
                }
                catch
                {
                    PreviewMedia.Position = TimeSpan.Zero;
                }
                _isPlaying = false;
                LiveDot.Visibility = Visibility.Collapsed;
                _timelineTimer.Start();
                UpdatePlaybackUi();
                ApplyPreviewColorTransform();
                EmitAutoFitSizeSuggestion();
                RebuildPreviewQualityOptions(PreviewMedia.NaturalVideoHeight);
                ApplyAspectRatioToMedia();
                RefreshFrameResizeLabel();
                await ProbeSourceFpsAndRefreshUiAsync();
            };
            PreviewMedia.MediaEnded += (_, _) =>
            {
                _isPlaying = false;
                LiveDot.Visibility = Visibility.Collapsed;
                PreviewMedia.Position = TimeSpan.Zero;
                UpdatePlaybackUi();
            };

            PlayPauseButton.Click += (_, _) => TogglePlayPause();
            StopButton.Click += (_, _) => StopPlayback();
            SkipBackButton.Click += (_, _) => SeekRelativeSeconds(-5);
            SkipForwardButton.Click += (_, _) => SeekRelativeSeconds(5);
            VolumeSlider.ValueChanged += (_, e) =>
            {
                _node.PreviewVolume = e.NewValue;
                PreviewMedia.Volume = e.NewValue;
                _isMuted = e.NewValue <= 0;
                if (!_isMuted) _lastVolume = e.NewValue;
                UpdateVolumeIcon();
            };
            MuteButton.Click += (_, _) =>
            {
                VolumeSlider.Value = _isMuted ? (_lastVolume > 0 ? _lastVolume : 0.7) : 0;
            };
            SetTransportIcons();
        }

        private void WireTransportAndGeneralSettings()
        {
            SecondsPerFrameSlider.ValueChanged += (_, e) =>
            {
                if (_isFrameControlSync) return;
                _isFrameControlSync = true;
                var secondsInt = Math.Clamp((int)Math.Round(e.NewValue), (int)SecondsPerFrameSlider.Minimum, (int)SecondsPerFrameSlider.Maximum);
                _node.SecondsPerFrame = secondsInt;
                SecondsPerFrameValueText.Text = $"{secondsInt}s";
                SyncFrameCountFromSeconds();
                _isFrameControlSync = false;
                UpdateFrameExtractionPreview();
            };
            FpsSlider.ValueChanged += (_, e) =>
            {
                if (_isFrameControlSync) return;
                var framesPerWindow = Math.Max(1, (int)Math.Round(e.NewValue));
                _node.ExtractFrameCount = framesPerWindow;
                FpsValueText.Text = $"{framesPerWindow}";
                UpdateFrameExtractionPreview();
            };

            UseDialogVideoConfigCheckBox.Checked += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.UseDialogVideoConfig = true;
                ApplyConfigSourceMode();
            };
            UseDialogVideoConfigCheckBox.Unchecked += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.UseDialogVideoConfig = false;
                ApplyConfigSourceMode();
            };
            PreferGpuCheckBox.Checked += (_, _) => _node.PreferGpu = true;
            PreferGpuCheckBox.Unchecked += (_, _) => _node.PreferGpu = false;
            PreviewQualityCombo.SelectionChanged += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.PreviewQualityMode = GetSelectedPreviewQualityTag();
                ApplyAspectRatioToMedia();
                ApplyPreviewQualitySettings();
            };
            PreviewVisualStrengthCombo.SelectionChanged += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.PreviewVisualStrengthMode = GetSelectedPreviewVisualStrengthTag();
                ApplyPreviewColorTransform();
            };
            SourceAudioToggle.Checked += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.SourceAudioEnabled = true;
                SourceAudioVolumeGrid.IsEnabled = true;
                UpdatePreviewAudioVolume();
            };
            SourceAudioToggle.Unchecked += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.SourceAudioEnabled = false;
                SourceAudioVolumeGrid.IsEnabled = false;
                UpdatePreviewAudioVolume();
            };
            SourceAudioVolumeSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                var vol = Math.Round(e.NewValue);
                _node.SourceAudioVolumePercent = vol;
                SourceAudioVolumeLabel.Text = $"{vol:0}%";
                UpdatePreviewAudioVolume();
            };
            AudioFadeInSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                var val = Math.Round(e.NewValue * 2) / 2.0;
                _node.AudioFadeInSec = val;
                AudioFadeInLabel.Text = $"{val:0.#}s";
            };
            AudioFadeOutSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                var val = Math.Round(e.NewValue * 2) / 2.0;
                _node.AudioFadeOutSec = val;
                AudioFadeOutLabel.Text = $"{val:0.#}s";
            };
            AudioNormalizeCheckBox.Click += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.AudioNormalizeEnabled = AudioNormalizeCheckBox.IsChecked == true;
            };
            AudioDenoiseCheckBox.Click += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.AudioDenoiseEnabled = AudioDenoiseCheckBox.IsChecked == true;
            };
        }

        private void WireGradingAndTransformEvents()
        {
            BrightnessSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _previewEffectTemporarilyDisabled = false; _node.Brightness = e.NewValue; BrightnessLabel.Text = $"{e.NewValue:0.##}"; ApplyPreviewColorTransform(); };
            ContrastSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _previewEffectTemporarilyDisabled = false; _node.Contrast = e.NewValue; ContrastLabel.Text = $"{e.NewValue:0.##}"; ApplyPreviewColorTransform(); };
            SaturationSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _previewEffectTemporarilyDisabled = false; _node.Saturation = e.NewValue; SaturationLabel.Text = $"{e.NewValue:0.##}"; ApplyPreviewColorTransform(); };
            HueSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _previewEffectTemporarilyDisabled = false; _node.Hue = e.NewValue; HueLabel.Text = $"{e.NewValue:0.##}"; ApplyPreviewColorTransform(); };
            GammaSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _previewEffectTemporarilyDisabled = false; _node.Gamma = e.NewValue; GammaLabel.Text = $"{e.NewValue:0.##}"; ApplyPreviewColorTransform(); };

            PresetNeutralButton.Click += (_, _) => ApplyGradingPreset(0, 1, 1, 0, 1);
            PresetVividButton.Click += (_, _) => ApplyGradingPreset(0.05, 1.15, 1.35, 8, 1);
            PresetCinematicButton.Click += (_, _) => ApplyGradingPreset(-0.08, 1.22, 0.82, -12, 1);
            PresetBwButton.Click += (_, _) => ApplyGradingPreset(0, 1.1, 0, 0, 1);
            PresetWarmButton.Click += (_, _) => ApplyGradingPreset(0.03, 1.05, 1.1, 15, 1.05);
            PresetCoolButton.Click += (_, _) => ApplyGradingPreset(-0.02, 1.0, 0.95, -20, 0.98);
            PresetFadeButton.Click += (_, _) => ApplyGradingPreset(0.1, 0.85, 0.75, 0, 1.1);
            ResetGradingButton.Click += (_, _) => ApplyGradingPreset(0, 1, 1, 0, 1);
            ApplyPreviewEffectButton.Click += (_, _) =>
            {
                _previewEffectTemporarilyDisabled = false;
                ApplyPreviewColorTransform();
                AppendLog("ℹ Preview effect applied lại theo thông số hiện tại.");
            };
            ResetPreviewEffectButton.Click += (_, _) =>
            {
                _previewEffectTemporarilyDisabled = true;
                ApplyPreviewColorTransform();
                AppendLog("ℹ Preview effect reset (không thay đổi thông số node).");
            };

            SharpenToggle.Checked += (_, _) => { if (_suppressControlSync) return; _node.SharpenEnabled = true; SharpenSlider.IsEnabled = true; };
            SharpenToggle.Unchecked += (_, _) => { if (_suppressControlSync) return; _node.SharpenEnabled = false; SharpenSlider.IsEnabled = false; };
            DenoiseToggle.Checked += (_, _) => { if (_suppressControlSync) return; _node.DenoiseEnabled = true; DenoiseSlider.IsEnabled = true; };
            DenoiseToggle.Unchecked += (_, _) => { if (_suppressControlSync) return; _node.DenoiseEnabled = false; DenoiseSlider.IsEnabled = false; };
            BlurToggle.Checked += (_, _) => { if (_suppressControlSync) return; _node.BlurEnabled = true; BlurSlider.IsEnabled = true; ApplyPreviewTransformEffects(); };
            BlurToggle.Unchecked += (_, _) => { if (_suppressControlSync) return; _node.BlurEnabled = false; BlurSlider.IsEnabled = false; ApplyPreviewTransformEffects(); };
            StabilizeToggle.Checked += (_, _) => { if (_suppressControlSync) return; _node.StabilizeEnabled = true; };
            StabilizeToggle.Unchecked += (_, _) => { if (_suppressControlSync) return; _node.StabilizeEnabled = false; };
            SharpenSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _node.SharpenStrength = e.NewValue; SharpenLabel.Text = $"{e.NewValue:0.#}"; };
            DenoiseSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _node.DenoiseStrength = e.NewValue; DenoiseLabel.Text = $"{e.NewValue:0.#}"; };
            BlurSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _node.BlurRadius = e.NewValue; BlurLabel.Text = $"{e.NewValue:0.#}"; ApplyPreviewTransformEffects(); };
            SpeedSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _node.SpeedFactor = e.NewValue; SpeedLabel.Text = $"{e.NewValue:0.##}x"; ApplyPreviewTransformEffects(); };

            Rotate0Button.Click += (_, _) => SetRotate(0, Rotate0Button);
            Rotate90Button.Click += (_, _) => SetRotate(90, Rotate90Button);
            Rotate180Button.Click += (_, _) => SetRotate(180, Rotate180Button);
            Rotate270Button.Click += (_, _) => SetRotate(270, Rotate270Button);
            FlipHButton.Click += (_, _) => ToggleFlip(FlipHButton, true);
            FlipVButton.Click += (_, _) => ToggleFlip(FlipVButton, false);

            CrfSlider.ValueChanged += (_, e) => { _node.Crf = e.NewValue; CrfLabel.Text = $"{(int)e.NewValue}"; };
            Scale100Button.Click += (_, _) => SetScale(1.0, null, Scale100Button);
            Scale75Button.Click += (_, _) => SetScale(0.75, null, Scale75Button);
            Scale50Button.Click += (_, _) => SetScale(0.5, null, Scale50Button);
            Scale25Button.Click += (_, _) => SetScale(0.25, null, Scale25Button);
            Scale1080Button.Click += (_, _) => SetScale(1.0, 1080, Scale1080Button);
            Scale720Button.Click += (_, _) => SetScale(1.0, 720, Scale720Button);
        }

        private void WireTrimAndOverlayEvents()
        {
            TrimToggle.Checked += (_, _) =>
            {
                _node.TrimEnabled = true;
                if (PreviewMedia.Source != null)
                {
                    var duration = GetNaturalDurationSeconds();
                    if (_node.TrimEndSec <= _node.TrimStartSec || _node.TrimEndSec <= 0)
                    {
                        _node.TrimEndSec = duration > 0 ? duration : Math.Max(_node.TrimStartSec + 1, 1);
                    }
                }
                TrimReviewHitArea.Visibility = TrimReviewCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                if (TrimReviewFramesPanel != null)
                    TrimReviewFramesPanel.Visibility = TrimReviewCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                UpdatePlaybackUi();
                RefreshInfoText();
            };
            TrimToggle.Unchecked += (_, _) =>
            {
                _node.TrimEnabled = false;
                TrimReviewHitArea.Visibility = Visibility.Collapsed;
                if (TrimReviewFramesPanel != null) TrimReviewFramesPanel.Visibility = Visibility.Collapsed;
                UpdatePlaybackUi();
                RefreshInfoText();
            };
            TrimReviewCheckBox.Checked += (_, _) =>
            {
                if (_node.TrimEnabled)
                {
                    TrimReviewHitArea.Visibility = Visibility.Visible;
                    if (TrimReviewFramesPanel != null) TrimReviewFramesPanel.Visibility = Visibility.Visible;
                }
                _trimUiInitialized = false;
                ProgressBarHitArea.IsEnabled = false;
                ProgressBarHitArea.Opacity = 0.45;
                UpdateTrimReviewUi();
            };
            TrimReviewCheckBox.Unchecked += (_, _) =>
            {
                TrimReviewHitArea.Visibility = Visibility.Collapsed;
                if (TrimReviewFramesPanel != null) TrimReviewFramesPanel.Visibility = Visibility.Collapsed;
                _trimUiInitialized = false;
                ProgressBarHitArea.IsEnabled = true;
                ProgressBarHitArea.Opacity = 1.0;
            };
            BrowseOutputButton.Click += (_, _) => BrowseOutputPath();
            ClearLogButton.Click += (_, _) => LogRichTextBox.Document?.Blocks?.Clear();
            CopyLogButton.Click += (_, _) =>
            {
                if (LogRichTextBox.Document != null)
                {
                    var range = new TextRange(LogRichTextBox.Document.ContentStart, LogRichTextBox.Document.ContentEnd);
                    Clipboard.SetText(range.Text);
                }
            };
            AddAudioTrackButton.Click += (_, _) => _node.AudioTracks.Add(new VideoAudioTrackConfig());
            AddTextOverlayItemButton.Click += (_, _) => AddOverlayItem("text");
            AddImageOverlayItemButton.Click += (_, _) => AddOverlayItem("image");
            RemoveSelectedOverlayItemButton.Click += (_, _) => RemoveSelectedOverlayItem();
            MoveOverlayUpButton.Click += (_, _) => MoveSelectedOverlay(-1);
            MoveOverlayDownButton.Click += (_, _) => MoveSelectedOverlay(1);
            ApplyOverlayToVideoButton.Click += (_, _) => ApplyOverlaysToVideo();
            OverlayLayerList.SelectionChanged += OverlayLayerList_SelectionChanged;
            OverlayCanvasControl.SelectionChanged += OverlayCanvasControl_SelectionChanged;
            ToggleBeforeAfterButton.Click += (_, _) => ToggleBeforeAfterPreview();
            OverlayTypeCombo.SelectionChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlaySourcePathTextBox.TextChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlaySourceTextArea.TextChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayXSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayYSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayWidthSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayHeightSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayOpacitySlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayRotationSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayFontFamilyCombo.SelectionChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayFontFamilyCombo.LostKeyboardFocus += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayFontColorTextBox.TextChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayFontSizeSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayAlignLeftRadio.Checked += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayAlignCenterRadio.Checked += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayAlignRightRadio.Checked += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayVisibleCheckBox.Checked += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayVisibleCheckBox.Unchecked += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayLockedCheckBox.Checked += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayLockedCheckBox.Unchecked += (_, _) => ApplyOverlayPropertyEditorChanges();

            // Populate font families (once).
            if (OverlayFontFamilyCombo.Items.Count == 0)
            {
                foreach (var ff in Fonts.SystemFontFamilies.OrderBy(f => f.Source, StringComparer.OrdinalIgnoreCase))
                    OverlayFontFamilyCombo.Items.Add(ff.Source);
            }
        }

        private void WireExtractionAndWatermarkEvents()
        {
            FrameFormatCombo.SelectionChanged += (_, _) =>
            {
                var selected = FrameFormatCombo.SelectedItem as ComboBoxItem;
                _node.FrameOutputFormat = selected?.Tag as string ?? "png";
                JpegQualitySlider.Visibility = _node.FrameOutputFormat == "jpg" ? Visibility.Visible : Visibility.Collapsed;
                if (JpegQualityLabel != null)
                {
                    JpegQualityLabel.Visibility = _node.FrameOutputFormat == "jpg" ? Visibility.Visible : Visibility.Collapsed;
                    JpegQualityLabel.Text = $"Quality: {_node.JpegQuality}/100";
                }
            };
            JpegQualitySlider.ValueChanged += (_, e) =>
            {
                _node.JpegQuality = (int)e.NewValue;
                if (JpegQualityLabel != null)
                    JpegQualityLabel.Text = $"Quality: {(int)e.NewValue}/100";
            };
            FrameResizeSlider.ValueChanged += (_, e) =>
            {
                _frameResizeScale = e.NewValue;
                _node.FrameResizeScale = _frameResizeScale;

                var w = (int)(PreviewMedia.NaturalVideoWidth * e.NewValue);
                var h = (int)(PreviewMedia.NaturalVideoHeight * e.NewValue);
                if (w <= 0 || h <= 0)
                {
                    FrameResizeLabel.Text = $"{e.NewValue:0.##}x";
                    return;
                }
                FrameResizeLabel.Text = $"{w}×{h}";
            };
            ExtractAllFramesCheckBox.Checked += (_, _) => { _node.ExtractAllFrames = true; UpdateFrameExtractionPreview(); };
            ExtractAllFramesCheckBox.Unchecked += (_, _) => { _node.ExtractAllFrames = false; UpdateFrameExtractionPreview(); };
            ExtractParallelJobsCombo.SelectionChanged += (_, _) =>
            {
                var selected = ExtractParallelJobsCombo.SelectedItem as ComboBoxItem;
                if (int.TryParse(selected?.Tag?.ToString(), out var jobs))
                    _node.ExtractParallelJobs = jobs;
            };

            WatermarkToggle.Checked += (_, _) => { _node.WatermarkEnabled = true; UpdateWatermarkPreviewUi(); };
            WatermarkToggle.Unchecked += (_, _) => { _node.WatermarkEnabled = false; UpdateWatermarkPreviewUi(); };
            BrowseWatermarkButton.Click += (_, _) =>
            {
                var dlg = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.gif|All|*.*" };
                if (dlg.ShowDialog() == true)
                {
                    WatermarkPathText.Text = dlg.FileName;
                    _node.WatermarkImagePath = dlg.FileName;
                    UpdateWatermarkPreviewUi();
                }
            };
            WatermarkOpacitySlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _node.WatermarkOpacity = e.NewValue;
                WatermarkOpacityLabel.Text = $"{e.NewValue:0.##}";
                UpdateWatermarkPreviewUi();
            };
            WatermarkWidthPercentSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _node.WatermarkWidthFraction = e.NewValue / 100.0;
                WatermarkWidthPercentLabel.Text = $"{e.NewValue:0.#}% video";
                UpdateWatermarkPreviewUi();
            };
            WatermarkInsetPercentSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _node.WatermarkInsetFraction = e.NewValue / 100.0;
                WatermarkInsetPercentLabel.Text = $"{e.NewValue:0.#}% mép";
                UpdateWatermarkPreviewUi();
            };
            ApplyWatermarkToVideoButton.Click += (_, _) =>
            {
                AppendLog("🎬 Áp dụng watermark lên video...");
                RunProcessingFlow();
            };
            WatermarkPositionCombo.SelectionChanged += (_, _) =>
            {
                var selected = WatermarkPositionCombo.SelectedItem as ComboBoxItem;
                if (selected?.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
                    _node.WatermarkPosition = tag;
                RefreshWatermarkPositionHint();
                UpdateWatermarkPreviewUi();
            };
        }

        private void WireFrameLabelAndExportEvents()
        {
            TextOverlayToggle.Checked += (_, _) => _node.TextOverlayEnabled = true;
            TextOverlayToggle.Unchecked += (_, _) => _node.TextOverlayEnabled = false;
            OverlayTextBox.TextChanged += (_, _) => _node.OverlayText = OverlayTextBox.Text;
            TextFontCombo.SelectionChanged += (_, _) => _node.OverlayFont = (TextFontCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Arial";
            TextSizeSlider.ValueChanged += (_, e) =>
            {
                _node.OverlayFontSize = (int)e.NewValue;
                TextSizeLabel.Text = $"{(int)e.NewValue}px";
            };
            FrameLabelToggle.Checked += (_, _) => { _node.FrameLabelEnabled = true; UpdateFrameLabelPreviewUi(); };
            FrameLabelToggle.Unchecked += (_, _) => { _node.FrameLabelEnabled = false; UpdateFrameLabelPreviewUi(); };
            FrameLabelTemplateTextBox.TextChanged += (_, _) => { _node.FrameLabelTemplate = FrameLabelTemplateTextBox.Text; UpdateFrameLabelPreviewUi(); };
            FrameLabelXSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _node.FrameLabelX = e.NewValue; UpdateFrameLabelPreviewUi(); };
            FrameLabelYSlider.ValueChanged += (_, e) => { if (_suppressControlSync) return; _node.FrameLabelY = e.NewValue; UpdateFrameLabelPreviewUi(); };
            FrameLabelPaddingLeftSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _node.FrameLabelPaddingLeft = (int)e.NewValue;
                FrameLabelPaddingLeftLabel.Text = $"{(int)e.NewValue}px";
                UpdateFrameLabelPreviewUi();
            };
            FrameLabelPaddingTopSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _node.FrameLabelPaddingTop = (int)e.NewValue;
                FrameLabelPaddingTopLabel.Text = $"{(int)e.NewValue}px";
                UpdateFrameLabelPreviewUi();
            };
            FrameLabelPaddingRightSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _node.FrameLabelPaddingRight = (int)e.NewValue;
                FrameLabelPaddingRightLabel.Text = $"{(int)e.NewValue}px";
                UpdateFrameLabelPreviewUi();
            };
            FrameLabelPaddingBottomSlider.ValueChanged += (_, e) =>
            {
                if (_suppressControlSync) return;
                _node.FrameLabelPaddingBottom = (int)e.NewValue;
                FrameLabelPaddingBottomLabel.Text = $"{(int)e.NewValue}px";
                UpdateFrameLabelPreviewUi();
            };
            FrameLabelTimeFormatCombo.SelectionChanged += (_, _) =>
            {
                _node.FrameLabelTimeFormat = (FrameLabelTimeFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "MMSS";
                UpdateFrameLabelPreviewUi();
            };
            FrameLabelTextColorTextBox.TextChanged += (_, _) => { _node.FrameLabelTextColor = FrameLabelTextColorTextBox.Text; UpdateFrameLabelPreviewUi(); };
            FrameLabelBackgroundColorTextBox.TextChanged += (_, _) => { _node.FrameLabelBackgroundColor = FrameLabelBackgroundColorTextBox.Text; UpdateFrameLabelPreviewUi(); };
            FrameLabelFontSizeSlider.ValueChanged += (_, e) =>
            {
                _node.FrameLabelFontSize = (int)e.NewValue;
                FrameLabelFontSizeLabel.Text = $"{(int)e.NewValue}px";
                UpdateFrameLabelPreviewUi();
            };
            FrameLabelDebugSamplesCheckBox.Checked += (_, _) => { _node.FrameLabelDebugSamplesEnabled = true; };
            FrameLabelDebugSamplesCheckBox.Unchecked += (_, _) => { _node.FrameLabelDebugSamplesEnabled = false; };

            OutputFormatCombo.SelectionChanged += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.OutputFormat = (OutputFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "mp4_h264";
                RefreshOutputsSummaryUi();
            };
            EncoderPresetCombo.SelectionChanged += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.EncoderPreset = (EncoderPresetCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "medium";
            };
            TwoPassToggle.Checked += (_, _) => _node.TwoPassEnabled = true;
            TwoPassToggle.Unchecked += (_, _) => _node.TwoPassEnabled = false;
            AudioCodecCombo.SelectionChanged += (_, _) => _node.AudioCodec = (AudioCodecCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "aac";
            AudioBitrateCombo.SelectionChanged += (_, _) => _node.AudioBitrate = (AudioBitrateCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "192k";
        }
    }
}
