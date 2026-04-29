using FlowMy.Converters;
using FlowMy.Controls;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow.NodeExecutors;
using Microsoft.Win32;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl : UserControl
    {
        private enum TimelineDragMode
        {
            None,
            Scrub,
            TrimStart,
            TrimEnd
        }

        private enum PreviewQualityMode
        {
            Auto,
            Low,
            Normal,
            High
        }

        private const double MinAutoFitNodeWidth = 540;
        private const double MinAutoFitNodeHeight = 340;
        private const double MaxAutoFitNodeWidth = 1280;
        private const double MaxAutoFitNodeHeight = 920;
        private const double MinPreviewHeight = 180;
        private const double MaxPreviewHeight = 620;
        private const double HorizontalPadding = 18;
        private const double NonPreviewContentHeight = 230;
        private const int DragSeekThrottleLowMs = 140;
        private const int DragSeekThrottleNormalMs = 90;
        private const int DragSeekThrottleHighMs = 35;

        private readonly VideoProcessingNode _node;
        private readonly IWorkflowEditorHost? _host;
        private readonly NotifyCollectionChangedEventHandler _audioTracksChangedHandler;
        private readonly PropertyChangedEventHandler _propertyChangedHandler;
        private readonly DispatcherTimer _timelineTimer;
        private readonly DispatcherTimer _beforeAfterFlickerTimer;

        private bool _subscriptionsAttached;
        private bool _isProgressDragging;
        private bool _isPlaying;
        private bool _isMuted;
        private bool _suppressControlSync;
        private bool _isLightTheme;
        private bool _isNodeZoomed;
        private double _prevNodeWidth;
        private double _prevNodeHeight;
        private double _pendingSeekRatio = -1;
        private DateTime _lastDragSeekAtUtc = DateTime.MinValue;
        private DateTime _lastSeekRequestAtUtc = DateTime.MinValue;
        private double _lastSeekTargetSeconds = -1;
        private double _lastSeekLatencyMs = -1;
        private bool _isSeekLatencyPending;
        private DateTime _dragReleaseBoostUntilUtc = DateTime.MinValue;
        private TimelineDragMode _timelineDragMode = TimelineDragMode.None;
        private TimelineDragMode _trimReviewDragMode = TimelineDragMode.None;
        private bool _previewEffectTemporarilyDisabled;
        private bool _trimUiInitialized;
        private double _trimUiStartX;
        private double _trimUiEndX;
        private double _trimUiPlayX;
        private double _lastVolume = 0.7;
        private int? _fixedResolutionHeight;
        private DateTime _lastRunStartedAtUtc = DateTime.UtcNow;
        private bool _showAfterPreview;
        private bool _suppressOverlayEditorSync;
        private bool _pendingOverlayApply;
        private string? _beforePreviewPath;
        private string? _afterPreviewPath;
        private bool _isFlickerMode;
        private bool _isSwitchingComparePreview;
        private bool _isSelectingVideoDialog;
        private double _selectedAspectW = 16;
        private double _selectedAspectH = 9;
        private bool _aspectAuto = false;
        private bool _isTrimReviewDragging;
        private bool _isFrameControlSync;
        private int _trimFramePreviewRequestId;

        public event Action<double, double>? SuggestedNodeSizeReady;
        public event Action<string>? LogLineReceived;

        public VideoProcessingNodeContentControl(VideoProcessingNode node, IWorkflowEditorHost? host = null)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host;

            InitializeComponent();
            ApplyThemeBrushes(GetTextBrush(_node.ColorKey));
            InitializeIcon();
            _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timelineTimer.Tick += (_, _) => UpdatePlaybackUi();
            _beforeAfterFlickerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            _beforeAfterFlickerTimer.Tick += (_, _) =>
            {
                if (!_isFlickerMode) return;
                _showAfterPreview = !_showAfterPreview;
                var target = _showAfterPreview ? _afterPreviewPath : _beforePreviewPath;
                LoadPreviewFromPath(target, isAfterPath: _showAfterPreview);
            };

            _audioTracksChangedHandler = (_, _) => RefreshInfoText();
            _propertyChangedHandler = (_, e) => OnNodePropertyChanged(e.PropertyName ?? string.Empty);

            InitializeInteractiveControls();

            Loaded += (_, _) =>
            {
                AttachSubscriptions();
                SyncControlValuesFromModel();
                RefreshInfoText();
                RefreshVideoPreview();
                UpdatePlaybackUi();
                ApplyLocalTheme();
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdatePreviewAspectRatio));
            };
            Unloaded += (_, _) => DetachSubscriptions();
        }

        private void InitializeInteractiveControls()
        {
            TabNavList.SelectionChanged += TabNavList_SelectionChanged;
            TabNavList.SelectedIndex = 0;
            SizeChanged += (_, _) => UpdatePreviewAspectRatio();

            OpenVideoButton.Click += (_, _) => SelectVideo();
            OpenVideoInPlaceholderButton.Click += (_, _) => SelectVideo();
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
                TabNavList.SelectedIndex = 6;
                RunProcessingFlow();
            };
            ExtractFramesButton.Click += (_, _) =>
            {
                TabNavList.SelectedIndex = 0;
                RunSpecificOperation("extract_frames");
            };
            BurnSubtitleButton.Click += (_, _) =>
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Chọn file subtitle",
                    Filter = "Subtitle Files|*.srt;*.ass;*.vtt;*.sub|All|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    _node.SubtitlePath = dlg.FileName;
                    _node.BurnSubtitleEnabled = true;
                    RunSpecificOperation("burn_subtitle");
                }
            };
            QuickWatermarkButton.Click += (_, _) => TabNavList.SelectedIndex = 2;
            ConvertFormatButton.Click += (_, _) => TabNavList.SelectedIndex = 4;
            QuickTrimButton.Click += (_, _) =>
            {
                _node.TrimStartSec = PreviewMedia.Position.TotalSeconds;
                _node.TrimEnabled = true;
                TabNavList.SelectedIndex = 4;
                TrimToggle.IsChecked = true;
                RefreshInfoText();
            };
            SnapshotButton.Click += (_, _) => TakeSnapshot();
            RunAllButton.Click += (_, _) =>
            {
                TabNavList.SelectedIndex = 6;
                RunProcessingFlow();
            };
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
                    !IsClickFromInteractiveElement(e.OriginalSource as DependencyObject))
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
            OpenFramesFolderButton.Click += (_, _) => OpenPathFromText(OutputFramesFolderText.Text);
            OpenOutputVideoActionButton.Click += (_, _) => OpenPathFromText(OutputVideoPathText.Text);
            OpenFramesFolderActionButton.Click += (_, _) => OpenPathFromText(OutputFramesFolderText.Text);
            OpenOutputVideoFolderQuickButton.Click += (_, _) => OpenPathFromText(OutputVideoPathText.Text);
            OpenFramesFolderQuickButton.Click += (_, _) => OpenPathFromText(OutputFramesFolderText.Text);
            CopyLogButton.Click += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(LogTextBox.Text))
                    Clipboard.SetText(LogTextBox.Text);
            };

            BrowseFfmpegFolderButton.Click += (_, _) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Chọn thư mục chứa ffmpeg.exe và ffprobe.exe"
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FfmpegFolderText.Text = dlg.SelectedPath;
                    var prefs = FfmpegPathPreferencesStore.Load();
                    prefs.FfmpegPath = dlg.SelectedPath;
                    FfmpegPathPreferencesStore.Save(prefs);
                }
            };
            VerifyFfmpegButton.Click += (_, _) =>
            {
                var folder = FfmpegFolderText.Text?.Trim() ?? string.Empty;
                var ffmpegOk = File.Exists(System.IO.Path.Combine(folder, "ffmpeg.exe"));
                var ffprobeOk = File.Exists(System.IO.Path.Combine(folder, "ffprobe.exe"));

                if (ffmpegOk && ffprobeOk)
                {
                    FfmpegVerifyText.Text = "✓ ffmpeg.exe và ffprobe.exe tìm thấy";
                    FfmpegVerifyText.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
                    FfmpegVerifyBadge.Background = new SolidColorBrush(Color.FromArgb(0x1A, 74, 222, 128));
                }
                else
                {
                    var missing = (!ffmpegOk ? "ffmpeg.exe" : "") +
                                  (!ffmpegOk && !ffprobeOk ? ", " : "") +
                                  (!ffprobeOk ? "ffprobe.exe" : "");
                    FfmpegVerifyText.Text = $"✗ Không tìm thấy: {missing}";
                    FfmpegVerifyText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                    FfmpegVerifyBadge.Background = new SolidColorBrush(Color.FromArgb(0x1A, 248, 113, 113));
                }
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
            OutputPathText.TextChanged += (_, _) => RefreshOutputsSummaryUi();
            SaveSettingsButton.Click += (_, _) =>
            {
                var folder = FfmpegFolderText.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    var prefs = FfmpegPathPreferencesStore.Load();
                    prefs.FfmpegPath = folder;
                    FfmpegPathPreferencesStore.Save(prefs);
                }

                var frameFolder = FrameOutputFolderText.Text?.Trim() ?? string.Empty;
                EnsureDirectoryExists(frameFolder);
                _node.FrameOutputFolderPath = frameFolder;

                var outputPath = DefaultOutputVideoPathText.Text?.Trim() ?? string.Empty;
                EnsureParentDirectoryExists(outputPath);
                _node.DefaultOutputVideoPath = outputPath;
                if (!_node.UseDialogVideoConfig)
                    _node.OutputPathOverride = outputPath;
                OutputPathText.Text = outputPath;
                AppendLog("✓ Đã lưu cài đặt.");
                RefreshOutputsSummaryUi();
            };

            PreviewMedia.MediaOpened += (_, _) =>
            {
                ApplyAspectRatioToMedia();
                EmitAutoFitSizeSuggestion();
                _timelineTimer.Start();
                _isPlaying = true;
                LiveDot.Visibility = Visibility.Visible;
                UpdatePlaybackUi();
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
                var count = Math.Max(1, (int)Math.Round(e.NewValue));
                _node.ExtractFrameCount = count;
                FpsValueText.Text = $"{count}";
                SyncSecondsFromFrameCount();
                UpdateFrameExtractionPreview();
            };
            OutputBase64CheckBox.Checked += (_, _) => _node.OutputBase64 = true;
            OutputBase64CheckBox.Unchecked += (_, _) => _node.OutputBase64 = false;
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
                ApplyPreviewQualitySettings();
            };
            PreviewVisualStrengthCombo.SelectionChanged += (_, _) =>
            {
                if (_suppressControlSync) return;
                _node.PreviewVisualStrengthMode = GetSelectedPreviewVisualStrengthTag();
                ApplyPreviewColorTransform();
            };
            SourceAudioToggle.Checked += (_, _) => _node.SourceAudioEnabled = true;
            SourceAudioToggle.Unchecked += (_, _) => _node.SourceAudioEnabled = false;

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

            SharpenToggle.Checked += (_, _) => { _node.SharpenEnabled = true; SharpenSlider.IsEnabled = true; };
            SharpenToggle.Unchecked += (_, _) => { _node.SharpenEnabled = false; SharpenSlider.IsEnabled = false; };
            DenoiseToggle.Checked += (_, _) => { _node.DenoiseEnabled = true; DenoiseSlider.IsEnabled = true; };
            DenoiseToggle.Unchecked += (_, _) => { _node.DenoiseEnabled = false; DenoiseSlider.IsEnabled = false; };
            BlurToggle.Checked += (_, _) => { _node.BlurEnabled = true; BlurSlider.IsEnabled = true; };
            BlurToggle.Unchecked += (_, _) => { _node.BlurEnabled = false; BlurSlider.IsEnabled = false; };
            StabilizeToggle.Checked += (_, _) => _node.StabilizeEnabled = true;
            StabilizeToggle.Unchecked += (_, _) => _node.StabilizeEnabled = false;
            SharpenSlider.ValueChanged += (_, e) => { _node.SharpenStrength = e.NewValue; SharpenLabel.Text = $"{e.NewValue:0.#}"; };
            DenoiseSlider.ValueChanged += (_, e) => { _node.DenoiseStrength = e.NewValue; DenoiseLabel.Text = $"{e.NewValue:0.#}"; };
            BlurSlider.ValueChanged += (_, e) => { _node.BlurRadius = e.NewValue; BlurLabel.Text = $"{e.NewValue:0.#}"; };
            SpeedSlider.ValueChanged += (_, e) => { _node.SpeedFactor = e.NewValue; SpeedLabel.Text = $"{e.NewValue:0.##}x"; };

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
            ClearLogButton.Click += (_, _) => LogTextBox.Clear();
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
            OverlaySourceTextBox.TextChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayXSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayYSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayWidthSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayHeightSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayOpacitySlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayRotationSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayFontFamilyTextBox.TextChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayFontColorTextBox.TextChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayFontSizeSlider.ValueChanged += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayVisibleCheckBox.Checked += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayVisibleCheckBox.Unchecked += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayLockedCheckBox.Checked += (_, _) => ApplyOverlayPropertyEditorChanges();
            OverlayLockedCheckBox.Unchecked += (_, _) => ApplyOverlayPropertyEditorChanges();

            FrameFormatCombo.SelectionChanged += (_, _) =>
            {
                var selected = FrameFormatCombo.SelectedItem as ComboBoxItem;
                _node.FrameOutputFormat = selected?.Tag as string ?? "png";
                JpegQualitySlider.Visibility = _node.FrameOutputFormat == "jpg" ? Visibility.Visible : Visibility.Collapsed;
            };
            JpegQualitySlider.ValueChanged += (_, e) => _node.JpegQuality = (int)e.NewValue;
            ExtractAllFramesCheckBox.Checked += (_, _) => { _node.ExtractAllFrames = true; UpdateFrameExtractionPreview(); };
            ExtractAllFramesCheckBox.Unchecked += (_, _) => { _node.ExtractAllFrames = false; UpdateFrameExtractionPreview(); };

            WatermarkToggle.Checked += (_, _) => _node.WatermarkEnabled = true;
            WatermarkToggle.Unchecked += (_, _) => _node.WatermarkEnabled = false;
            BrowseWatermarkButton.Click += (_, _) =>
            {
                var dlg = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.gif|All|*.*" };
                if (dlg.ShowDialog() == true)
                {
                    WatermarkPathText.Text = dlg.FileName;
                    _node.WatermarkImagePath = dlg.FileName;
                }
            };
            WatermarkOpacitySlider.ValueChanged += (_, e) =>
            {
                _node.WatermarkOpacity = e.NewValue;
                WatermarkOpacityLabel.Text = $"{e.NewValue:0.##}";
            };
            WatermarkPositionCombo.SelectionChanged += (_, _) =>
            {
                var selected = WatermarkPositionCombo.SelectedItem as ComboBoxItem;
                _node.WatermarkPosition = selected?.Tag as string ?? "BR";
            };

            TextOverlayToggle.Checked += (_, _) => _node.TextOverlayEnabled = true;
            TextOverlayToggle.Unchecked += (_, _) => _node.TextOverlayEnabled = false;
            OverlayTextBox.TextChanged += (_, _) => _node.OverlayText = OverlayTextBox.Text;
            TextFontCombo.SelectionChanged += (_, _) => _node.OverlayFont = (TextFontCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Arial";
            TextSizeSlider.ValueChanged += (_, e) =>
            {
                _node.OverlayFontSize = (int)e.NewValue;
                TextSizeLabel.Text = $"{(int)e.NewValue}px";
            };

            TwoPassToggle.Checked += (_, _) => _node.TwoPassEnabled = true;
            TwoPassToggle.Unchecked += (_, _) => _node.TwoPassEnabled = false;
            AudioCodecCombo.SelectionChanged += (_, _) => _node.AudioCodec = (AudioCodecCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "aac";
            AudioBitrateCombo.SelectionChanged += (_, _) => _node.AudioBitrate = (AudioBitrateCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "192k";
        }

        private void ApplyThemeBrushes(Brush textBrush)
        {
            TitleText.Foreground = textBrush;
            VideoPathText.Foreground = textBrush;
            HwBadgeText.Foreground = textBrush;
        }

        private void InitializeIcon()
        {
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(string.Empty, typeof(Uri), "circle-video sharp-light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            if (iconUri != null) IconView.Source = iconUri;
            IconView.Fill = GetTextBrush(_node.ColorKey);
        }

        private void TabNavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SwitchTab();
        }

        private void SwitchTab()
        {
            var allTabs = new FrameworkElement[]
            {
                GeneralTabContent, GradingTabContent, FiltersTabContent,
                AudioTabContent, ExportTabContent, OutputsTabContent, SettingsTabContent
            };
            foreach (var t in allTabs) t.Visibility = Visibility.Collapsed;

            var allActions = new FrameworkElement[]
            {
                GeneralActionBar, GradingActionBar, FiltersActionBar,
                AudioActionBar, ExportActionBar, OutputsActionBar, SettingsActionBar
            };
            foreach (var a in allActions) a.Visibility = Visibility.Collapsed;

            var idx = TabNavList.SelectedIndex;
            var targetTab = idx switch
            {
                0 => GeneralTabContent,
                1 => GradingTabContent,
                2 => FiltersTabContent,
                3 => AudioTabContent,
                4 => ExportTabContent,
                5 => OutputsTabContent,
                6 => SettingsTabContent,
                _ => GeneralTabContent
            };
            targetTab.Visibility = Visibility.Visible;
            targetTab.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));

            var targetAction = idx switch
            {
                0 => GeneralActionBar,
                1 => GradingActionBar,
                2 => FiltersActionBar,
                3 => AudioActionBar,
                4 => ExportActionBar,
                5 => OutputsActionBar,
                6 => SettingsActionBar,
                _ => GeneralActionBar
            };
            targetAction.Visibility = Visibility.Visible;
        }

        private void RunProcessingFlow()
        {
            if (_host == null) return;
            try
            {
                SyncRuntimeConfigFromUi();
                _lastRunStartedAtUtc = DateTime.UtcNow;
                ProgressStatusText.Text = "Running...";
                if (!string.IsNullOrWhiteSpace(_node.VideoSourceNodeId))
                {
                    var sourceNode = _host.ViewModel?.Nodes?.FirstOrDefault(n =>
                        string.Equals(n.Id, _node.VideoSourceNodeId, StringComparison.OrdinalIgnoreCase));
                    if (sourceNode != null) _host.RequestRunSingleNode(sourceNode);
                }
                _host.RequestRunSingleNode(_node);
            }
            catch (Exception ex)
            {
                AppendLog($"Run error: {ex.Message}");
            }
        }

        private void SyncRuntimeConfigFromUi()
        {
            if (_node.UseDialogVideoConfig)
            {
                // When using dialog config, let executor resolve output paths from dialog fields.
                // Clear local override to avoid treating a folder path as an exact output file.
                _node.OutputPathOverride = string.Empty;
                return;
            }

            _node.OutputBase64 = OutputBase64CheckBox.IsChecked == true;
            _node.FrameOutputFolderPath = (FrameOutputFolderText.Text ?? string.Empty).Trim();
            var outputVideoPath = (OutputPathText.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputVideoPath))
                outputVideoPath = (DefaultOutputVideoPathText.Text ?? string.Empty).Trim();
            _node.OutputPathOverride = outputVideoPath;
            _node.DefaultOutputVideoPath = (DefaultOutputVideoPathText.Text ?? string.Empty).Trim();
        }

        private void OnNodePropertyChanged(string propertyName)
        {
            if (propertyName == nameof(VideoProcessingNode.VideoPath))
            {
                if (_isSwitchingComparePreview)
                {
                    _isSwitchingComparePreview = false;
                }
                else
                {
                    // User selected/changed video manually -> disable compare mode
                    // to avoid flicker timer overriding the new source path.
                    StopComparePreviewMode();
                    _beforePreviewPath = _node.VideoPath;
                }
                RefreshVideoPreview();
            }
            if (propertyName == nameof(VideoProcessingNode.FrameOutputFolderPath))
            {
                _suppressControlSync = true;
                FrameOutputFolderText.Text = _node.FrameOutputFolderPath ?? string.Empty;
                _suppressControlSync = false;
                RefreshOutputsSummaryUi();
            }
            if (propertyName == nameof(VideoProcessingNode.DefaultOutputVideoPath))
            {
                _suppressControlSync = true;
                DefaultOutputVideoPathText.Text = _node.DefaultOutputVideoPath ?? string.Empty;
                if (!_node.UseDialogVideoConfig)
                {
                    OutputPathText.Text = _node.DefaultOutputVideoPath ?? string.Empty;
                }
                _suppressControlSync = false;
                RefreshOutputsSummaryUi();
            }
            if (propertyName == nameof(VideoProcessingNode.OutputBase64))
            {
                OutputBase64CheckBox.IsChecked = _node.OutputBase64;
                RefreshOutputsSummaryUi();
            }
            if (propertyName == nameof(VideoProcessingNode.SourceFps)) FpsSlider.Maximum = Math.Max(1, _node.SourceFps);
            if (propertyName == nameof(VideoProcessingNode.PreferredHwAccel)) HwBadgeText.Text = _node.PreferredHwAccel;
            if (propertyName == nameof(VideoProcessingNode.UseDialogVideoConfig))
            {
                _suppressControlSync = true;
                UseDialogVideoConfigCheckBox.IsChecked = _node.UseDialogVideoConfig;
                _suppressControlSync = false;
                ApplyConfigSourceMode();
            }
            RefreshInfoText();
        }

        private void AttachSubscriptions()
        {
            if (_subscriptionsAttached) return;
            _node.AudioTracks.CollectionChanged += _audioTracksChangedHandler;
            _node.PropertyChanged += _propertyChangedHandler;
            AudioTracksList.ItemsSource = _node.AudioTracks;
            OverlayCanvasControl.ItemsSource = _node.Overlays;
            OverlayLayerList.ItemsSource = _node.Overlays;
            VideoProcessingNodeExecutor.ProgressChanged += HandleExecutorProgress;
            VideoProcessingNodeExecutor.LogLine += HandleExecutorLog;
            _subscriptionsAttached = true;
        }

        private void DetachSubscriptions()
        {
            if (!_subscriptionsAttached) return;
            _node.AudioTracks.CollectionChanged -= _audioTracksChangedHandler;
            _node.PropertyChanged -= _propertyChangedHandler;
            OverlayCanvasControl.ItemsSource = null;
            OverlayLayerList.ItemsSource = null;
            VideoProcessingNodeExecutor.ProgressChanged -= HandleExecutorProgress;
            VideoProcessingNodeExecutor.LogLine -= HandleExecutorLog;
            _timelineTimer.Stop();
            _beforeAfterFlickerTimer.Stop();
            PreviewMedia.Stop();
            _subscriptionsAttached = false;
        }

        private void HandleExecutorProgress(VideoProcessingNode node, double percent, string status)
        {
            if (!ReferenceEquals(node, _node)) return;
            UpdateProgress(percent, status);
            if (_pendingOverlayApply && percent >= 99)
            {
                Dispatcher.BeginInvoke(new Action(OnOverlayApplyCompleted));
            }
        }

        private void HandleExecutorLog(VideoProcessingNode node, string line)
        {
            if (!ReferenceEquals(node, _node)) return;
            AppendLog(line);
        }

        private void AppendLog(string line)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogTextBox.AppendText(line + Environment.NewLine);
                LogScrollViewer.ScrollToBottom();
                LogLineReceived?.Invoke(line);
            }));
        }

        private void UpdateProgress(double percent, string status)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ProgressPercentText.Text = $"{percent:0}%";
                ProgressStatusText.Text = status;
                var parentWidth = ((FrameworkElement)ProgressFill.Parent).ActualWidth;
                ProgressFill.Width = Math.Max(0, parentWidth * percent / 100);
                var elapsed = DateTime.UtcNow - _lastRunStartedAtUtc;
                ElapsedTimeText.Text = $"Elapsed: {elapsed:mm\\:ss}";
                EstimatedTimeText.Text = percent > 0 ? $"ETA: {TimeSpan.FromSeconds(elapsed.TotalSeconds * (100 - percent) / percent):mm\\:ss}" : "ETA: --";
            }));
        }

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
            var fpsInt = Math.Max(1, (int)Math.Round(sourceFps));

            var totalFrames = Math.Max(1, (int)Math.Floor(duration * sourceFps));
            var secondsInt = (int)Math.Round(_node.SecondsPerFrame);
            secondsInt = Math.Clamp(secondsInt, (int)SecondsPerFrameSlider.Minimum, (int)SecondsPerFrameSlider.Maximum);
            _node.SecondsPerFrame = secondsInt;

            // seconds slider defines an upper bound for frame-count.
            var maxCountBySeconds = Math.Max(1, secondsInt * fpsInt);
            maxCountBySeconds = Math.Min(maxCountBySeconds, totalFrames);

            // Update slider max in real-time.
            _isFrameControlSync = true;
            try
            {
                FpsSlider.Maximum = maxCountBySeconds;
                var current = Math.Clamp(_node.ExtractFrameCount, 1, totalFrames);
                if (current > maxCountBySeconds)
                {
                    current = maxCountBySeconds;
                    _node.ExtractFrameCount = current;
                }
                FpsSlider.Value = current;
                FpsValueText.Text = $"{current}";
                _node.ExtractFps = Math.Max(1, current / duration);
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
            var fpsInt = Math.Max(1, (int)Math.Round(sourceFps));

            var totalFrames = Math.Max(1, (int)Math.Floor(duration * sourceFps));
            var count = Math.Clamp(_node.ExtractFrameCount, 1, totalFrames);
            _node.ExtractFrameCount = count;
            FpsSlider.Maximum = totalFrames;
            _node.ExtractFps = Math.Max(1, count / duration);

            var secondsInt = (int)Math.Round((double)count / fpsInt);
            secondsInt = Math.Clamp(secondsInt, (int)SecondsPerFrameSlider.Minimum, (int)SecondsPerFrameSlider.Maximum);
            _node.SecondsPerFrame = secondsInt;

            _isFrameControlSync = true;
            try
            {
                SecondsPerFrameSlider.Value = secondsInt;
                SecondsPerFrameValueText.Text = $"{secondsInt}s";
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
            VideoPathText.Text = string.IsNullOrWhiteSpace(path) ? "Chua chon file video" : path;
            HwBadgeText.Text = (_node.PreferredHwAccel ?? "none").ToUpperInvariant();
            StatFpsText.Text = $"{_node.SourceFps:0.##}";
            StatResolutionText.Text = PreviewMedia.NaturalVideoWidth > 0 ? $"{PreviewMedia.NaturalVideoWidth}x{PreviewMedia.NaturalVideoHeight}" : "--";
            StatDurationText.Text = FormatTime(TimeSpan.FromSeconds(GetNaturalDurationSeconds()));
            CodecInfoText.Text = $"HW: {_node.PreferredHwAccel} | Extract: {_node.ExtractFps:0.##}/s";
            AudioSummaryText.Text = $"Audio tracks: {_node.AudioTracks.Count} | Output: {(_node.OutputBase64 ? "base64" : "file")}";
            FpsValueText.Text = $"{Math.Max(1, _node.ExtractFrameCount)}";
            var secondsInt = (int)Math.Round(_node.SecondsPerFrame);
            secondsInt = Math.Clamp(secondsInt, (int)SecondsPerFrameSlider.Minimum, (int)SecondsPerFrameSlider.Maximum);
            SecondsPerFrameValueText.Text = $"{secondsInt}s";
            TrimStartText.Text = FormatTime(TimeSpan.FromSeconds(_node.TrimStartSec));
            TrimEndText.Text = FormatTime(TimeSpan.FromSeconds(_node.TrimEndSec));
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
                var totalFrames = Math.Max(1, (int)Math.Floor(Math.Max(0.1, GetNaturalDurationSeconds()) * Math.Max(1, _node.SourceFps)));
                FpsSlider.Maximum = Math.Max(1, totalFrames);
                FpsSlider.Value = Math.Clamp(_node.ExtractFrameCount, 1, (int)FpsSlider.Maximum);
                var secondsInt = (int)Math.Round(_node.SecondsPerFrame);
                secondsInt = Math.Clamp(secondsInt, (int)SecondsPerFrameSlider.Minimum, (int)SecondsPerFrameSlider.Maximum);
                SecondsPerFrameSlider.Value = secondsInt;
                UseDialogVideoConfigCheckBox.IsChecked = _node.UseDialogVideoConfig;
                OutputBase64CheckBox.IsChecked = _node.OutputBase64;
                PreferGpuCheckBox.IsChecked = _node.PreferGpu;
                SourceAudioToggle.IsChecked = _node.SourceAudioEnabled;
                VolumeSlider.Value = _node.PreviewVolume;
                PreviewQualityCombo.SelectedIndex = _node.PreviewQualityMode switch
                {
                    "auto" => 0,
                    "low" => 1,
                    "high" => 3,
                    _ => 2
                };
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
                DenoiseToggle.IsChecked = _node.DenoiseEnabled;
                DenoiseSlider.IsEnabled = _node.DenoiseEnabled;
                DenoiseSlider.Value = _node.DenoiseStrength;
                BlurToggle.IsChecked = _node.BlurEnabled;
                BlurSlider.IsEnabled = _node.BlurEnabled;
                BlurSlider.Value = _node.BlurRadius;
                StabilizeToggle.IsChecked = _node.StabilizeEnabled;
                SpeedSlider.Value = _node.SpeedFactor;

                CrfSlider.Value = _node.Crf;
                CrfLabel.Text = $"{(int)_node.Crf}";
                OutputPathText.Text = _node.OutputPathOverride ?? string.Empty;
                DefaultOutputVideoPathText.Text = _node.DefaultOutputVideoPath ?? _node.OutputPathOverride ?? string.Empty;
                FrameOutputFolderText.Text = _node.FrameOutputFolderPath ?? string.Empty;
                TrimToggle.IsChecked = _node.TrimEnabled;
                TrimReviewCheckBox.IsChecked = false;
                TrimReviewHitArea.Visibility = Visibility.Collapsed;
                ProgressBarHitArea.IsEnabled = true;
                ProgressBarHitArea.Opacity = 1.0;
                _fixedResolutionHeight = _node.FixedResolutionHeight;
                WatermarkToggle.IsChecked = _node.WatermarkEnabled;
                WatermarkPathText.Text = _node.WatermarkImagePath ?? string.Empty;
                WatermarkPositionCombo.SelectedIndex = _node.WatermarkPosition switch
                {
                    "TL" => 0, "TC" => 1, "TR" => 2, "ML" => 3, "MC" => 4, "MR" => 5, "BL" => 6, "BC" => 7, _ => 8
                };
                WatermarkOpacitySlider.Value = _node.WatermarkOpacity;
                WatermarkOpacityLabel.Text = $"{_node.WatermarkOpacity:0.##}";
                TextOverlayToggle.IsChecked = _node.TextOverlayEnabled;
                OverlayTextBox.Text = _node.OverlayText;
                TextSizeSlider.Value = _node.OverlayFontSize;
                TextSizeLabel.Text = $"{_node.OverlayFontSize}px";
                TwoPassToggle.IsChecked = _node.TwoPassEnabled;
                AudioCodecCombo.SelectedIndex = _node.AudioCodec switch { "mp3" => 1, "opus" => 2, "copy" => 3, _ => 0 };
                AudioBitrateCombo.SelectedIndex = _node.AudioBitrate switch { "128k" => 0, "256k" => 2, "320k" => 3, _ => 1 };
            }
            finally
            {
                _suppressControlSync = false;
            }

            ApplyPreviewQualitySettings();
            ApplyConfigSourceMode();
        }

        private void RefreshVideoPreview()
        {
            var path = _node.VideoPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                PreviewMedia.Stop();
                PreviewMedia.Source = null;
                PreviewMedia.Visibility = Visibility.Collapsed;
                PreviewPlaceholder.Visibility = Visibility.Visible;
                LiveDot.Visibility = Visibility.Collapsed;
                _isPlaying = false;
                _timelineTimer.Stop();
                UpdatePlaybackUi();
                return;
            }

            try
            {
                PreviewMedia.Stop();
                PreviewMedia.Source = null;
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    PreviewMedia.Source = new Uri(path, UriKind.Absolute);
                    PreviewMedia.Visibility = Visibility.Visible;
                    PreviewPlaceholder.Visibility = Visibility.Collapsed;
                    PreviewMedia.Volume = _node.PreviewVolume;
                    PreviewMedia.Play();
                    PreviewMedia.Pause();
                    _isPlaying = false;
                    LiveDot.Visibility = Visibility.Collapsed;
                    _timelineTimer.Start();
                    ApplyPreviewColorTransform();
                    UpdatePlaybackUi();
                }));
            }
            catch (Exception ex)
            {
                AppendLog($"Preview error: {ex.Message}");
                PreviewMedia.Stop();
                PreviewMedia.Source = null;
                PreviewMedia.Visibility = Visibility.Collapsed;
                PreviewPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void TogglePlayPause()
        {
            if (PreviewMedia.Source == null) return;
            if (_isPlaying)
            {
                PreviewMedia.Pause();
                _isPlaying = false;
                LiveDot.Visibility = Visibility.Collapsed;
            }
            else
            {
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
        }

        private void UpdateProgressVisualByRatio(double ratio)
        {
            var barWidth = ProgressBarHitArea.ActualWidth;
            ProgressBarFill.Width = barWidth * ratio;
            Canvas.SetLeft(ProgressThumb, Math.Max(0, (barWidth * ratio) - 6));
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

        private void PreviewMedia_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => TogglePlayPause();

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
            ProgressThumb.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private void ProgressBarHitArea_MouseEnter(object sender, MouseEventArgs e)
            => ProgressThumb.Visibility = Visibility.Visible;

        private void ProgressBarHitArea_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isProgressDragging) ProgressThumb.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private static bool IsClickFromInteractiveElement(DependencyObject? source)
        {
            var current = source;
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
        }

        private PreviewQualityMode GetPreviewQualityMode()
        {
            return (_node.PreviewQualityMode ?? "normal").ToLowerInvariant() switch
            {
                "auto" => PreviewQualityMode.Auto,
                "low" => PreviewQualityMode.Low,
                "high" => PreviewQualityMode.High,
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

            return "normal";
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

            var duration = TimeSpan.FromSeconds(GetNaturalDurationSeconds());
            var position = PreviewMedia.Position;
            var ratio = duration.TotalSeconds > 0 ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1) : 0;
            if (_isProgressDragging && _pendingSeekRatio >= 0)
            {
                ratio = _pendingSeekRatio;
                position = TimeSpan.FromSeconds(ratio * duration.TotalSeconds);
            }
            var barWidth = ProgressBarHitArea.ActualWidth;
            ProgressBarFill.Width = barWidth * ratio;
            Canvas.SetLeft(ProgressThumb, Math.Max(0, (barWidth * ratio) - 6));
            TimeCurrentText.Text = FormatTime(position);
            TimeTotalText.Text = FormatTime(duration);
            if (PreviewMedia.Source != null && _node.SourceFps > 0)
            {
                var currentSec = PreviewMedia.Position.TotalSeconds;
                var currentFrame = (int)(currentSec * _node.SourceFps);
                FrameInfoText.Text =
                    $"Frame #{currentFrame:N0}  |  {_node.SourceFps:0.##} fps  |  " +
                    $"{(PreviewMedia.NaturalVideoWidth > 0 ? $"{PreviewMedia.NaturalVideoWidth}x{PreviewMedia.NaturalVideoHeight}" : "--")}";
            }

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
            PlayPauseButton.Content = CreateTransportIcon(_isPlaying ? "pause chisel-regular" : "play chisel-regular");
            UpdateTrimReviewUi();
        }

        private void TrimReviewHitArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_node.TrimEnabled || PreviewMedia.Source == null) return;
            _isTrimReviewDragging = true;
            _trimReviewDragMode = ResolveTrimReviewDragMode(e);
            TrimReviewHitArea.CaptureMouse();
            HandleTrimReviewDrag(e, commitPreviewSeek: false);
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

            // Load preview frames only after releasing mouse.
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
                // In scrub mode, load both frames.
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

        private async System.Threading.Tasks.Task LoadTrimFramePreviewAsync(bool isStart)
        {
            var requestId = ++_trimFramePreviewRequestId;

            if (string.IsNullOrWhiteSpace(_node.VideoPath)) return;
            if (PreviewMedia.Source == null) return;

            var duration = GetNaturalDurationSeconds();
            if (duration <= 0) duration = 1;

            var t = isStart
                ? Math.Clamp(_node.TrimStartSec, 0, duration)
                : Math.Clamp((_node.TrimEndSec > 0 ? _node.TrimEndSec : duration), 0, duration);

            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"FlowMy_trim_{(isStart ? "start" : "end")}_{Guid.NewGuid():N}.png");

            try
            {
                await VideoProcessingNodeExecutor.RunSnapshotAsync(
                    _node.VideoPath,
                    t.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    tmp,
                    System.Threading.CancellationToken.None).ConfigureAwait(false);

                if (requestId != _trimFramePreviewRequestId) return;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new System.Uri(tmp, System.UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                await Dispatcher.BeginInvoke(new Action(() =>
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
                }));
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

            var pos = e.GetPosition(TrimReviewHitArea);
            var startX = Math.Clamp(_node.TrimStartSec / duration, 0, 1) * barWidth;
            var endSec = _node.TrimEndSec > 0 ? _node.TrimEndSec : duration;
            var endX = Math.Clamp(endSec / duration, 0, 1) * barWidth;
            const double handleHitRange = 12;

            if (Math.Abs(pos.X - startX) <= handleHitRange) return TimelineDragMode.TrimStart;
            if (Math.Abs(pos.X - endX) <= handleHitRange) return TimelineDragMode.TrimEnd;
            return TimelineDragMode.Scrub;
        }

        private void HandleTrimReviewDrag(MouseEventArgs e, bool commitPreviewSeek)
        {
            var duration = GetNaturalDurationSeconds();
            if (duration <= 0 || TrimReviewHitArea.ActualWidth <= 1) return;
            var ratio = Math.Clamp(e.GetPosition(TrimReviewHitArea).X / TrimReviewHitArea.ActualWidth, 0, 1);
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

            var startRatio = Math.Clamp(_node.TrimStartSec / duration, 0, 1);
            var endSec = _node.TrimEndSec > 0 ? _node.TrimEndSec : duration;
            var endRatio = Math.Clamp(endSec / duration, 0, 1);
            if (endRatio < startRatio) (startRatio, endRatio) = (endRatio, startRatio);

            var targetStartX = Math.Max(0, (startRatio * width) - 5.5);
            var targetEndX = Math.Max(0, (endRatio * width) - 5.5);
            var playRatio = Math.Clamp(PreviewMedia.Position.TotalSeconds / duration, 0, 1);
            var targetPlayX = Math.Max(0, (playRatio * width) - 5.5);

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

            var left = Math.Min(_trimUiStartX, _trimUiEndX) + 5.5;
            var right = Math.Max(_trimUiStartX, _trimUiEndX) + 5.5;
            TrimReviewRangeFill.Width = Math.Max(0, right - left);
            TrimReviewRangeFill.Margin = new Thickness(left, 0, 0, 0);
            Canvas.SetLeft(TrimReviewStartThumb, _trimUiStartX);
            Canvas.SetLeft(TrimReviewEndThumb, _trimUiEndX);
            Canvas.SetLeft(TrimReviewPlayheadThumb, _trimUiPlayX);
        }

        private double GetNaturalDurationSeconds()
            => PreviewMedia.NaturalDuration.HasTimeSpan ? PreviewMedia.NaturalDuration.TimeSpan.TotalSeconds : 0;

        private void ApplyGradingPreset(double brightness, double contrast, double saturation, double hue, double gamma)
        {
            _previewEffectTemporarilyDisabled = false;
            _node.Brightness = brightness;
            _node.Contrast = contrast;
            _node.Saturation = saturation;
            _node.Hue = hue;
            _node.Gamma = gamma;
            SyncControlValuesFromModel();
            ApplyPreviewColorTransform();
        }

        private void ApplyPreviewColorTransform()
        {
            if (_previewEffectTemporarilyDisabled)
            {
                GradingOverlay.Background = Brushes.Transparent;
                PreviewMedia.Opacity = 1.0;
                return;
            }

            // MediaElement does not support full realtime color matrix in this control.
            // This block applies a stronger approximate preview so grading changes are visibly reflected.
            var brightness = Math.Clamp(_node.Brightness, -1.0, 1.0);
            var contrast = Math.Clamp(_node.Contrast, 0.1, 3.0);
            var saturation = Math.Clamp(_node.Saturation, 0.0, 3.0);
            var hue = Math.Clamp(_node.Hue, -180.0, 180.0);
            var gamma = Math.Clamp(_node.Gamma, 0.1, 3.0);
            var strength = (_node.PreviewVisualStrengthMode ?? "balanced").ToLowerInvariant();
            var strengthScale = strength switch
            {
                "fast" => 0.65,
                "strong" => 1.45,
                _ => 1.0
            };

            var tintStrength = Math.Min(0.45, (Math.Abs(hue) / 180.0 * 0.28 + Math.Max(0, saturation - 1.0) * 0.06) * strengthScale);
            var hueColor = HsvToColor((hue + 360.0) % 360.0, 0.9, 1.0);
            byte tintAlpha;
            Color tintRgb;

            if (brightness >= 0)
            {
                tintAlpha = (byte)Math.Clamp((int)((brightness * 90 + tintStrength * 100) * strengthScale), 0, 170);
                tintRgb = tintStrength > 0.01 ? hueColor : Color.FromRgb(255, 255, 255);
            }
            else
            {
                tintAlpha = (byte)Math.Clamp((int)((-brightness * 120 + tintStrength * 90) * strengthScale), 0, 190);
                if (tintStrength > 0.01)
                {
                    tintRgb = Color.FromRgb(
                        (byte)Math.Max(0, hueColor.R - 55),
                        (byte)Math.Max(0, hueColor.G - 55),
                        (byte)Math.Max(0, hueColor.B - 55));
                }
                else
                {
                    tintRgb = Color.FromRgb(0, 0, 0);
                }
            }

            GradingOverlay.Background = new SolidColorBrush(Color.FromArgb(tintAlpha, tintRgb.R, tintRgb.G, tintRgb.B));

            var contrastOpacityBoost = (contrast - 1.0) * 0.11 * strengthScale;
            var saturationPenalty = (1.0 - Math.Min(1.0, saturation)) * 0.18 * strengthScale;
            var gammaPenalty = Math.Max(0, 1.0 - gamma) * 0.12 * strengthScale;
            PreviewMedia.Opacity = Math.Clamp(1.0 + contrastOpacityBoost - saturationPenalty - gammaPenalty, 0.52, 1.0);
        }

        private static Color HsvToColor(double hue, double saturation, double value)
        {
            var c = value * saturation;
            var x = c * (1 - Math.Abs((hue / 60.0 % 2) - 1));
            var m = value - c;
            double r1, g1, b1;
            if (hue < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (hue < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (hue < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (hue < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (hue < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            return Color.FromRgb(
                (byte)Math.Clamp((int)((r1 + m) * 255), 0, 255),
                (byte)Math.Clamp((int)((g1 + m) * 255), 0, 255),
                (byte)Math.Clamp((int)((b1 + m) * 255), 0, 255));
        }

        private void RemoveAudioTrack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VideoAudioTrackConfig track)
                _node.AudioTracks.Remove(track);
        }

        private void AddOverlayItem(string type)
        {
            if (type == "image")
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Chọn ảnh overlay",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All|*.*"
                };
                if (dlg.ShowDialog() != true) return;
                _node.Overlays.Add(new OverlayItem
                {
                    Type = "image",
                    Source = dlg.FileName,
                    X = 0.08,
                    Y = 0.08,
                    Width = 0.24,
                    Height = 0.24,
                    Opacity = 1.0,
                    IsVisible = true
                });
            }
            else
            {
                _node.Overlays.Add(new OverlayItem
                {
                    Type = "text",
                    Source = "Double-click để sửa text",
                    X = 0.12,
                    Y = 0.12,
                    Width = 0.35,
                    Height = 0.15,
                    FontFamily = "Arial",
                    FontColor = "White",
                    FontSize = 28,
                    Opacity = 1.0,
                    IsVisible = true
                });
            }

            var selected = _node.Overlays.LastOrDefault();
            OverlayCanvasControl.SelectedItem = selected;
            OverlayLayerList.SelectedItem = selected;
        }

        private void RemoveSelectedOverlayItem()
        {
            if (OverlayLayerList.SelectedItem is not OverlayItem selected) return;
            _node.Overlays.Remove(selected);
            OverlayCanvasControl.SelectedItem = null;
            OverlayLayerList.SelectedItem = null;
        }

        private void MoveSelectedOverlay(int direction)
        {
            if (OverlayLayerList.SelectedItem is not OverlayItem selected) return;
            var currentIndex = _node.Overlays.IndexOf(selected);
            if (currentIndex < 0) return;
            var targetIndex = Math.Clamp(currentIndex + direction, 0, _node.Overlays.Count - 1);
            if (targetIndex == currentIndex) return;
            _node.Overlays.Move(currentIndex, targetIndex);
            OverlayLayerList.SelectedItem = selected;
        }

        private void OverlayLayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OverlayLayerList.SelectedItem is OverlayItem selected)
            {
                OverlayCanvasControl.SelectedItem = selected;
                SyncOverlayEditorFromSelection(selected);
            }
            else
            {
                SyncOverlayEditorFromSelection(null);
            }
        }

        private void OverlayCanvasControl_SelectionChanged(object? sender, OverlayItem? item)
        {
            OverlayLayerList.SelectedItem = item;
            SyncOverlayEditorFromSelection(item);
        }

        private void ApplyOverlaysToVideo()
        {
            var visibleCount = _node.Overlays.Count(o => o.IsVisible);
            if (visibleCount == 0)
            {
                AppendLog("⚠ Chưa có overlay nào đang hiển thị để áp dụng.");
                return;
            }

            _pendingOverlayApply = true;
            _beforePreviewPath = _node.VideoPath;
            _showAfterPreview = false;
            _isFlickerMode = false;
            _beforeAfterFlickerTimer.Stop();
            TabNavList.SelectedIndex = 6;
            AppendLog($"🎞 Bắt đầu áp dụng {visibleCount} overlay item lên video...");
            RunProcessingFlow();
        }

        private void OnOverlayApplyCompleted()
        {
            if (!_pendingOverlayApply) return;
            _pendingOverlayApply = false;

            var outputCandidate = (_node.OutputPathOverride ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputCandidate) || !File.Exists(outputCandidate))
            {
                outputCandidate = OutputVideoPathText.Text?.Trim() ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(outputCandidate) && File.Exists(outputCandidate))
            {
                _afterPreviewPath = outputCandidate;
                _showAfterPreview = true;
                _isFlickerMode = false;
                _beforeAfterFlickerTimer.Stop();
                LoadPreviewFromPath(_afterPreviewPath, isAfterPath: true);
                ToggleBeforeAfterButton.Content = "After";
                AppendLog("✅ Đã áp dụng overlay và chuyển preview sang bản After.");
            }
            else
            {
                AppendLog("ℹ Xử lý xong nhưng chưa tìm thấy file output để bật preview After.");
            }
        }

        private void ToggleBeforeAfterPreview()
        {
            if (string.IsNullOrWhiteSpace(_beforePreviewPath) || string.IsNullOrWhiteSpace(_afterPreviewPath))
            {
                AppendLog("ℹ Chưa có đủ before/after để so sánh.");
                return;
            }

            if (!_showAfterPreview && !_isFlickerMode)
            {
                _showAfterPreview = true;
                LoadPreviewFromPath(_afterPreviewPath, isAfterPath: true);
                ToggleBeforeAfterButton.Content = "After";
                return;
            }

            if (_showAfterPreview && !_isFlickerMode)
            {
                _isFlickerMode = true;
                _beforeAfterFlickerTimer.Start();
                ToggleBeforeAfterButton.Content = "Flicker";
                return;
            }

            _isFlickerMode = false;
            _beforeAfterFlickerTimer.Stop();
            _showAfterPreview = false;
            LoadPreviewFromPath(_beforePreviewPath, isAfterPath: false);
            ToggleBeforeAfterButton.Content = "Before";
        }

        private void LoadPreviewFromPath(string? path, bool isAfterPath)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            _showAfterPreview = isAfterPath;
            _isSwitchingComparePreview = true;
            _node.VideoPath = path;
            _node.RaisePropertyChanged(nameof(VideoProcessingNode.VideoPath));
        }

        private void StopComparePreviewMode()
        {
            _isFlickerMode = false;
            _beforeAfterFlickerTimer.Stop();
            _showAfterPreview = false;
            ToggleBeforeAfterButton.Content = "Before/After";
        }

        private void SyncOverlayEditorFromSelection(OverlayItem? item)
        {
            _suppressOverlayEditorSync = true;
            try
            {
                var has = item != null;
                OverlayTypeCombo.IsEnabled = has;
                OverlaySourceTextBox.IsEnabled = has;
                OverlayXSlider.IsEnabled = has;
                OverlayYSlider.IsEnabled = has;
                OverlayWidthSlider.IsEnabled = has;
                OverlayHeightSlider.IsEnabled = has;
                OverlayOpacitySlider.IsEnabled = has;
                OverlayRotationSlider.IsEnabled = has;
                OverlayFontFamilyTextBox.IsEnabled = has;
                OverlayFontColorTextBox.IsEnabled = has;
                OverlayFontSizeSlider.IsEnabled = has;
                OverlayVisibleCheckBox.IsEnabled = has;
                OverlayLockedCheckBox.IsEnabled = has;

                if (!has)
                {
                    OverlayTypeCombo.SelectedIndex = -1;
                    OverlaySourceTextBox.Text = string.Empty;
                    return;
                }

                OverlayTypeCombo.SelectedIndex = (item!.Type ?? "text").ToLowerInvariant() switch
                {
                    "image" => 1,
                    "logo" => 2,
                    _ => 0
                };
                OverlaySourceTextBox.Text = item.Source;
                OverlayXSlider.Value = item.X;
                OverlayYSlider.Value = item.Y;
                OverlayWidthSlider.Value = item.Width;
                OverlayHeightSlider.Value = item.Height;
                OverlayOpacitySlider.Value = item.Opacity;
                OverlayRotationSlider.Value = item.Rotation;
                OverlayFontFamilyTextBox.Text = item.FontFamily;
                OverlayFontColorTextBox.Text = item.FontColor;
                OverlayFontSizeSlider.Value = item.FontSize;
                OverlayVisibleCheckBox.IsChecked = item.IsVisible;
                OverlayLockedCheckBox.IsChecked = item.IsLocked;
            }
            finally
            {
                _suppressOverlayEditorSync = false;
            }
        }

        private void ApplyOverlayPropertyEditorChanges()
        {
            if (_suppressOverlayEditorSync) return;
            if (OverlayLayerList.SelectedItem is not OverlayItem selected) return;

            var selectedType = (OverlayTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "text";
            selected.Type = selectedType;
            selected.Source = OverlaySourceTextBox.Text ?? string.Empty;
            selected.X = OverlayXSlider.Value;
            selected.Y = OverlayYSlider.Value;
            selected.Width = OverlayWidthSlider.Value;
            selected.Height = OverlayHeightSlider.Value;
            selected.Opacity = OverlayOpacitySlider.Value;
            selected.Rotation = OverlayRotationSlider.Value;
            selected.FontFamily = OverlayFontFamilyTextBox.Text;
            selected.FontColor = OverlayFontColorTextBox.Text;
            selected.FontSize = (int)OverlayFontSizeSlider.Value;
            selected.IsVisible = OverlayVisibleCheckBox.IsChecked == true;
            selected.IsLocked = OverlayLockedCheckBox.IsChecked == true;
            OverlayLayerList.Items.Refresh();
        }

        private void BrowseAudioTrack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not VideoAudioTrackConfig track) return;
            var dlg = new OpenFileDialog
            {
                Title = "Chọn file audio",
                Filter = "Audio Files|*.mp3;*.wav;*.aac;*.flac;*.ogg;*.m4a|All|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            track.SourceOutputKey = dlg.FileName;
            AudioTracksList.Items.Refresh();
        }

        private void RunSpecificOperation(string operationType)
        {
            if (string.IsNullOrWhiteSpace(_node.VideoPath))
            {
                AppendLog("⚠ Chưa chọn video nguồn.");
                TabNavList.SelectedIndex = 6;
                return;
            }

            SyncRuntimeConfigFromUi();
            TabNavList.SelectedIndex = 6;
            _lastRunStartedAtUtc = DateTime.UtcNow;
            ProgressStatusText.Text = $"Running: {operationType}...";

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    switch (operationType)
                    {
                        case "extract_frames":
                            var configuredFrameFolder = (_node.FrameOutputFolderPath ?? string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(configuredFrameFolder))
                                EnsureDirectoryExists(configuredFrameFolder);
                            await VideoProcessingNodeExecutor.RunExtractFramesOnlyAsync(
                                _node,
                                line => Dispatcher.BeginInvoke(new Action(() => AppendLog(line))),
                                (pct, status) => Dispatcher.BeginInvoke(new Action(() => UpdateProgress(pct, status))),
                                configuredFrameFolder,
                                System.Threading.CancellationToken.None);
                            break;
                        case "burn_subtitle":
                            if (string.IsNullOrWhiteSpace(_node.SubtitlePath))
                            {
                                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("⚠ Chưa chọn file subtitle.")));
                                return;
                            }
                            await VideoProcessingNodeExecutor.RunBurnSubtitleAsync(
                                _node,
                                line => Dispatcher.BeginInvoke(new Action(() => AppendLog(line))),
                                (pct, status) => Dispatcher.BeginInvoke(new Action(() => UpdateProgress(pct, status))),
                                System.Threading.CancellationToken.None);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"❌ Error: {ex.Message}")));
                }
            });
        }

        private void TakeSnapshot()
        {
            if (PreviewMedia.Source == null) return;
            var dlg = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                FileName = $"snapshot_{DateTime.Now:HHmmss}.png"
            };
            if (dlg.ShowDialog() != true) return;
            var position = PreviewMedia.Position.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var outputPath = dlg.FileName;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await VideoProcessingNodeExecutor.RunSnapshotAsync(_node.VideoPath, position, outputPath, System.Threading.CancellationToken.None);
                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"✅ Snapshot saved: {outputPath}")));
                }
                catch (Exception ex)
                {
                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"❌ Snapshot failed: {ex.Message}")));
                }
            });
        }

        private void SetRotate(double deg, Button activeButton)
        {
            _node.RotationDegrees = deg;
            foreach (var b in new[] { Rotate0Button, Rotate90Button, Rotate180Button, Rotate270Button })
                b.Background = new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255));
            activeButton.Background = new SolidColorBrush(Color.FromRgb(0x7C, 0x6B, 0xF8));
        }

        private void ToggleFlip(Button button, bool isHorizontal)
        {
            if (isHorizontal) _node.FlipH = !_node.FlipH; else _node.FlipV = !_node.FlipV;
            var enabled = isHorizontal ? _node.FlipH : _node.FlipV;
            button.Background = enabled ? new SolidColorBrush(Color.FromRgb(0x7C, 0x6B, 0xF8)) : new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255));
        }

        private void SetScale(double scale, int? fixedHeight, Button activeButton)
        {
            _fixedResolutionHeight = fixedHeight;
            _node.ResolutionScale = scale;
            _node.FixedResolutionHeight = fixedHeight;
            foreach (var b in new[] { Scale100Button, Scale75Button, Scale50Button, Scale25Button, Scale1080Button, Scale720Button })
                b.Background = new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255));
            activeButton.Background = new SolidColorBrush(Color.FromRgb(0x7C, 0x6B, 0xF8));
        }

        private void UpdateVolumeIcon()
        {
            MuteButton.Content = new TextBlock { Text = _isMuted ? "MUTE" : (PreviewMedia.Volume > 0.5 ? "VOL+" : "VOL") };
        }

        private void SetTransportIcons()
        {
            SkipBackButton.Content = CreateTransportIcon("backward-fast sharp-regular");
            SkipForwardButton.Content = CreateTransportIcon("forward-fast sharp-regular");
            PlayPauseButton.Content = CreateTransportIcon("play chisel-regular");
            StopButton.Content = new TextBlock { Text = "⏹", Foreground = GetThemeIconBrush(), FontSize = 12 };
        }

        private SvgViewboxEx CreateTransportIcon(string iconKey)
        {
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(string.Empty, typeof(Uri), iconKey,
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            return new SvgViewboxEx
            {
                Width = 14,
                Height = 14,
                Source = iconUri!,
                Fill = GetThemeIconBrush()
            };
        }

        private Brush GetThemeIconBrush()
        {
            if (Resources["ThemeTextPrimaryBrush"] is Brush brush)
            {
                return brush;
            }

            return _isLightTheme
                ? new SolidColorBrush(Color.FromRgb(35, 42, 52))
                : new SolidColorBrush(Color.FromRgb(232, 240, 255));
        }

        private void ApplyLocalTheme()
        {
            var isLight = _isLightTheme;
            Background = isLight ? new SolidColorBrush(Color.FromRgb(242, 245, 252)) : new SolidColorBrush(Color.FromRgb(15, 15, 23));
            Foreground = isLight ? new SolidColorBrush(Color.FromRgb(34, 40, 49)) : new SolidColorBrush(Color.FromRgb(232, 232, 240));

            Resources["ThemeTextPrimaryBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(35, 42, 52) : Color.FromRgb(232, 240, 255));
            Resources["ThemeTextSecondaryBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(86, 96, 112) : Color.FromRgb(198, 211, 226));
            Resources["ThemeCardBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xF5, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
            Resources["ThemeCardBorderBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x4A, 0x6B, 0x7A, 0x8A) : Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInnerCardBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xD8, 0xF2, 0xF5, 0xFA) : Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            Resources["ThemeInnerCardBorderBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x52, 0x9C, 0xAA, 0xBC) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(248, 251, 255) : Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(178, 191, 212) : Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputForegroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(36, 44, 57) : Color.FromRgb(232, 240, 255));
            Resources["ThemeOverlayBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xCC, 0xEC, 0xF1, 0xF8) : Color.FromArgb(0xAA, 0x00, 0x00, 0x00));
            Resources["ThemeOverlayBorderBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x58, 0x95, 0xA4, 0xBA) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTimelinePanelBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xF0, 0xE9, 0xEF, 0xF8) : Color.FromArgb(0xEE, 0x0A, 0x0A, 0x18));
            Resources["ThemeTrackBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x60, 0x95, 0xA4, 0xBA) : Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF));
            Resources["ThemeAccentBrush"] = new SolidColorBrush(Color.FromRgb(124, 107, 248));
            Resources["ThemeTabNavBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xCC, 0xE8, 0xEE, 0xF7) : Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
            Resources["ThemeLogContainerBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xD8, 0xF5, 0xF8, 0xFD) : Color.FromArgb(0x0C, 0x00, 0x00, 0x00));
            Resources["ThemeActionBarBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xEF, 0xEA, 0xF1, 0xFB) : Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            Resources["ThemeActionBarBorderBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0x9A, 0xA9, 0xBE) : Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            Resources["ThemeOnAccentTextBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(248, 250, 255) : Color.FromRgb(255, 255, 255));
            Resources["ThemeSliderThumbBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(57, 69, 88) : Color.FromRgb(255, 255, 255));
            Resources["ThemeComboPopupBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(242, 246, 252) : Color.FromRgb(30, 30, 48));
            Resources["ThemeComboItemHoverBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(221, 232, 247) : Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTabHoverBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(221, 232, 247) : Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            Resources["ThemeActionExtractBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0x5B, 0x8F, 0xF9) : Color.FromArgb(0x20, 0x5B, 0x8F, 0xF9));
            Resources["ThemeActionSubtitleBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0x7C, 0x6B, 0xF8) : Color.FromArgb(0x20, 0x7C, 0x6B, 0xF8));
            Resources["ThemeActionWatermarkBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0x14, 0xB8, 0xA6) : Color.FromArgb(0x20, 0x14, 0xB8, 0xA6));
            Resources["ThemeActionConvertBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0xA7, 0x8B, 0xFA) : Color.FromArgb(0x20, 0xA7, 0x8B, 0xFA));
            Resources["ThemeActionTrimBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0xEF, 0x44, 0x44) : Color.FromArgb(0x20, 0xEF, 0x44, 0x44));
            Resources["ThemeActionSnapshotBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0xF5, 0x9E, 0x0B) : Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
            Resources["ThemeActionFolderVideoBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0x4A, 0xDE, 0x80) : Color.FromArgb(0x20, 0x4A, 0xDE, 0x80));
            Resources["ThemeActionFolderFramesBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0xF5, 0x9E, 0x0B) : Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
            Resources["ThemeActionExtractBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0x5B, 0x8F, 0xF9) : Color.FromArgb(0x20, 0x5B, 0x8F, 0xF9));
            Resources["ThemeActionSubtitleBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0x7C, 0x6B, 0xF8) : Color.FromArgb(0x20, 0x7C, 0x6B, 0xF8));
            Resources["ThemeActionWatermarkBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0x14, 0xB8, 0xA6) : Color.FromArgb(0x20, 0x14, 0xB8, 0xA6));
            Resources["ThemeActionConvertBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0xA7, 0x8B, 0xFA) : Color.FromArgb(0x20, 0xA7, 0x8B, 0xFA));
            Resources["ThemeActionTrimBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0xEF, 0x44, 0x44) : Color.FromArgb(0x20, 0xEF, 0x44, 0x44));
            Resources["ThemeActionSnapshotBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0xF5, 0x9E, 0x0B) : Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
            Resources["ThemeActionFolderVideoBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0x4A, 0xDE, 0x80) : Color.FromArgb(0x20, 0x4A, 0xDE, 0x80));
            Resources["ThemeActionFolderFramesBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x66, 0xF5, 0x9E, 0x0B) : Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));

            var textPrimary = (Brush)Resources["ThemeTextPrimaryBrush"];
            var textSecondary = (Brush)Resources["ThemeTextSecondaryBrush"];
            SetForegroundIfExists("TimeCurrentText", textSecondary);
            SetForegroundIfExists("TimeTotalText", textSecondary);
            SetForegroundIfExists("SeekPerfText", textSecondary);
            SetForegroundIfExists("FrameInfoText", textPrimary);
            SetForegroundIfExists("VideoPathText", textSecondary);
            SetForegroundIfExists("CodecInfoText", textSecondary);
            SetForegroundIfExists("AudioSummaryText", textSecondary);
            SetForegroundIfExists("ConfigMissingSummaryText", textPrimary);

            ThemeModeButton.Content = CreateThemeModeIcon(isLight ? "moon regular" : "sun-bright duotone-thin", isLight);
            SetTransportIcons();
        }

        private void SetForegroundIfExists(string elementName, Brush brush)
        {
            if (FindName(elementName) is TextBlock tb)
            {
                tb.Foreground = brush;
            }
        }

        private static SvgViewboxEx CreateThemeModeIcon(string iconKey, bool isLightMode)
        {
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(string.Empty, typeof(Uri), iconKey,
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            return new SvgViewboxEx
            {
                Width = 15,
                Height = 15,
                Source = iconUri!,
                Fill = isLightMode ? new SolidColorBrush(Color.FromRgb(56, 63, 74)) : new SolidColorBrush(Color.FromRgb(255, 219, 116))
            };
        }

        private void ToggleNodeZoom()
        {
            if (_node == null) return;

            if (!_isNodeZoomed)
            {
                _prevNodeWidth = _node.Width;
                _prevNodeHeight = _node.Height;
                _node.Width = Math.Max(1360, _node.Width);
                _node.Height = Math.Max(768, _node.Height);
                _isNodeZoomed = true;
                ToggleNodeSizeButton.Content = new TextBlock { Text = "⤡", FontSize = 12 };
            }
            else
            {
                _node.Width = _prevNodeWidth > 0 ? _prevNodeWidth : 1360;
                _node.Height = _prevNodeHeight > 0 ? _prevNodeHeight : 768;
                _isNodeZoomed = false;
                ToggleNodeSizeButton.Content = new TextBlock { Text = "⤢", FontSize = 12 };
            }
        }

        private void EmitAutoFitSizeSuggestion()
        {
            var naturalW = PreviewMedia.NaturalVideoWidth;
            var naturalH = PreviewMedia.NaturalVideoHeight;
            if (naturalW <= 0 || naturalH <= 0) return;
            var aspect = naturalW / (double)naturalH;
            if (aspect <= 0 || double.IsNaN(aspect) || double.IsInfinity(aspect)) return;

            var previewHeight = Math.Clamp(naturalH, MinPreviewHeight, Math.Min(MaxPreviewHeight, MaxAutoFitNodeHeight - NonPreviewContentHeight));
            var previewWidth = previewHeight * aspect;
            var suggestedWidth = Math.Clamp(previewWidth + HorizontalPadding, MinAutoFitNodeWidth, MaxAutoFitNodeWidth);
            var suggestedHeight = Math.Clamp(previewHeight + NonPreviewContentHeight, MinAutoFitNodeHeight, MaxAutoFitNodeHeight);
            SuggestedNodeSizeReady?.Invoke(suggestedWidth, suggestedHeight);
        }

        private void UpdatePreviewAspectRatio()
        {
            if (PreviewContainerBorder == null || VideoContainerGrid == null) return;

            var outerH = PreviewContainerBorder.ActualHeight;
            if (outerH <= 0) return;

            UpdateAdaptivePreviewRows(outerH);

            if (PreviewMedia.Source != null)
            {
                VideoViewbox.Visibility = Visibility.Visible;
                PreviewPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                VideoViewbox.Visibility = Visibility.Collapsed;
                PreviewPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void SetAspectRatio(double w, double h, bool auto)
        {
            _selectedAspectW = w;
            _selectedAspectH = h;
            _aspectAuto = auto;
            ApplyAspectRatioToMedia();
        }

        private void ApplyAspectRatioToMedia()
        {
            if (_aspectAuto)
            {
                var natW = PreviewMedia.NaturalVideoWidth > 0 ? PreviewMedia.NaturalVideoWidth : 1280;
                var natH = PreviewMedia.NaturalVideoHeight > 0 ? PreviewMedia.NaturalVideoHeight : 720;
                PreviewMedia.Width = natW;
                PreviewMedia.Height = natH;
            }
            else if (_selectedAspectW > 0 && _selectedAspectH > 0)
            {
                var baseW = 1280.0;
                PreviewMedia.Width = baseW;
                PreviewMedia.Height = baseW * (_selectedAspectH / _selectedAspectW);
            }

            UpdatePreviewAspectRatio();
        }

        private void UpdateAdaptivePreviewRows(double containerHeight)
        {
            if (VideoContainerGrid.RowDefinitions.Count < 4) return;

            var rowAspect = VideoContainerGrid.RowDefinitions[0];
            var rowVideo = VideoContainerGrid.RowDefinitions[1];
            var rowTimeline = VideoContainerGrid.RowDefinitions[2];
            var rowLog = VideoContainerGrid.RowDefinitions[3];

            var topH = rowAspect.ActualHeight > 0 ? rowAspect.ActualHeight : 44;
            var timelineH = rowTimeline.ActualHeight > 0 ? rowTimeline.ActualHeight : 120;
            var available = containerHeight - topH - timelineH - 12;
            if (available <= 80) return;

            var mediaW = PreviewMedia.Source != null && PreviewMedia.NaturalVideoWidth > 0
                ? PreviewMedia.NaturalVideoWidth
                : (_selectedAspectW > 0 ? _selectedAspectW : 16);
            var mediaH = PreviewMedia.Source != null && PreviewMedia.NaturalVideoHeight > 0
                ? PreviewMedia.NaturalVideoHeight
                : (_selectedAspectH > 0 ? _selectedAspectH : 9);
            if (mediaW <= 0 || mediaH <= 0) return;

            var ratio = mediaW / mediaH;
            var videoRatio = ratio >= 1.1 ? 0.56 : (ratio <= 0.9 ? 0.73 : 0.64);

            var targetVideoH = Math.Clamp(available * videoRatio, 120, available - 80);
            var targetLogH = Math.Clamp(available - targetVideoH, 80, 260);

            rowVideo.Height = new GridLength(targetVideoH, GridUnitType.Pixel);
            rowLog.Height = new GridLength(targetLogH, GridUnitType.Pixel);
        }

        private void RefreshOutputsSummaryUi()
        {
            SetTextIfExists("OutputModeSummaryText", _node.OutputBase64 ? "Base64" : "File");
            SetTextIfExists("OutputFormatSummaryText", (OutputFormatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "MP4 (H.264)");
            SetTextIfExists("OutputAudioSummaryText", $"{_node.AudioTracks.Count} track | codec: {_node.AudioCodec} | bitrate: {_node.AudioBitrate}");
            var estimatedFrames = Math.Round(GetNaturalDurationSeconds() * (_node.ExtractAllFrames ? _node.SourceFps : _node.ExtractFps));
            SetTextIfExists("OutputEstimatedFramesText", $"{estimatedFrames:0} frame");

            var outputVideoPath = (_node.UseDialogVideoConfig
                ? (_node.DefaultOutputVideoPath ?? string.Empty)
                : (OutputPathText.Text ?? string.Empty)).Trim();
            if (string.IsNullOrWhiteSpace(outputVideoPath))
                outputVideoPath = (DefaultOutputVideoPathText.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputVideoPath))
                outputVideoPath = (_node.OutputPathOverride ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(outputVideoPath))
                _node.OutputPathOverride = outputVideoPath;

            if (string.IsNullOrWhiteSpace(outputVideoPath))
            {
                OutputVideoPathText.Text = "Chưa đặt đường dẫn video đầu ra";
                OpenOutputVideoButton.IsEnabled = false;
                OpenOutputVideoActionButton.IsEnabled = false;
            }
            else
            {
                OutputVideoPathText.Text = outputVideoPath;
                var outputDir = System.IO.Path.GetDirectoryName(outputVideoPath) ?? string.Empty;
                var ready = File.Exists(outputVideoPath) || Directory.Exists(outputDir);
                OpenOutputVideoButton.IsEnabled = ready;
                OpenOutputVideoActionButton.IsEnabled = ready;
            }

            var sourceDir = !string.IsNullOrWhiteSpace(_node.VideoPath) ? (System.IO.Path.GetDirectoryName(_node.VideoPath) ?? string.Empty) : string.Empty;
            var framesDir = (_node.UseDialogVideoConfig
                ? (_node.FrameOutputFolderPath ?? string.Empty)
                : (FrameOutputFolderText.Text ?? string.Empty)).Trim();
            if (string.IsNullOrWhiteSpace(framesDir))
                framesDir = !string.IsNullOrWhiteSpace(sourceDir) ? System.IO.Path.Combine(sourceDir, "frames") : string.Empty;
            if (string.IsNullOrWhiteSpace(framesDir))
            {
                OutputFramesFolderText.Text = "Chưa xác định thư mục frame";
                OpenFramesFolderButton.IsEnabled = false;
                OpenFramesFolderActionButton.IsEnabled = false;
                OpenFramesFolderButton.Visibility = Visibility.Visible;
            }
            else
            {
                OutputFramesFolderText.Text = framesDir;
                var framesReady = Directory.Exists(framesDir) || !_node.OutputBase64;
                OpenFramesFolderButton.IsEnabled = framesReady;
                OpenFramesFolderActionButton.IsEnabled = framesReady;
                OpenFramesFolderButton.Visibility = Visibility.Visible;
            }

            var okVideo = !string.IsNullOrWhiteSpace(_node.VideoPath);
            var okOutput = !string.IsNullOrWhiteSpace(_node.OutputPathOverride);
            var okSubtitle = !_node.BurnSubtitleEnabled || !string.IsNullOrWhiteSpace(_node.SubtitlePath);
            var okWatermark = !_node.WatermarkEnabled || !string.IsNullOrWhiteSpace(_node.WatermarkImagePath);
            var okTextOverlay = !_node.TextOverlayEnabled || !string.IsNullOrWhiteSpace(_node.OverlayText);

            SetConfigCheck(ConfigCheckVideoText, okVideo, "Đã chọn video nguồn", "Thiếu video nguồn");
            SetConfigCheck(ConfigCheckOutputText, okOutput, "Đã đặt đường dẫn video đầu ra", "Chưa đặt đường dẫn video đầu ra");
            SetConfigCheck(ConfigCheckSubtitleText, okSubtitle, "Subtitle hợp lệ", "Đã bật burn subtitle nhưng chưa chọn file subtitle");
            SetConfigCheck(ConfigCheckWatermarkText, okWatermark, "Watermark hợp lệ", "Đã bật watermark nhưng chưa chọn ảnh");
            SetConfigCheck(ConfigCheckTextOverlayText, okTextOverlay, "Text overlay hợp lệ", "Đã bật chèn chữ nhưng nội dung chữ đang trống");

            var missingCount = new[] { okVideo, okOutput, okSubtitle, okWatermark, okTextOverlay }.Count(x => !x);
            if (missingCount == 0)
            {
                SetTextStyleIfExists("ConfigMissingSummaryText", "✓ Tất cả cấu hình đã đầy đủ", new SolidColorBrush(Color.FromRgb(74, 222, 128)));
            }
            else
            {
                SetTextStyleIfExists("ConfigMissingSummaryText", $"⚠ Còn thiếu {missingCount} cấu hình cần thiết", new SolidColorBrush(Color.FromRgb(248, 113, 113)));
            }
        }

        private void UpdateFrameExtractionPreview()
        {
            var duration = GetNaturalDurationSeconds();
            var sourceFps = _node.SourceFps > 0 ? _node.SourceFps : 30;
            if (duration <= 0)
            {
                duration = 1;
            }
            var totalFrames = Math.Max(1, (int)Math.Floor(duration * sourceFps));
            var fpsInt = Math.Max(1, (int)Math.Round(sourceFps));
            var secondsInt = Math.Clamp(
                (int)Math.Round(_node.SecondsPerFrame),
                (int)SecondsPerFrameSlider.Minimum,
                (int)SecondsPerFrameSlider.Maximum);
            var maxCountBySeconds = Math.Max(1, secondsInt * fpsInt);
            maxCountBySeconds = Math.Min(maxCountBySeconds, totalFrames);

            FpsSlider.Maximum = _node.ExtractAllFrames ? totalFrames : maxCountBySeconds;

            if (_node.ExtractAllFrames)
            {
                var total = (int)Math.Floor(duration * sourceFps);
                EstFramePerSecText.Text = $"{sourceFps:0.##}";
                EstimatedFrameCountText.Text = $"{total:N0}";
                EstFrameIntervalText.Text = $"{(1000.0 / sourceFps):0.#} ms";
                SetTextIfExists("FrameIndexPreviewText", $"All frames mode: 0..{Math.Max(0, (int)sourceFps - 1)} mỗi giây");
                return;
            }

            var requestedCount = Math.Clamp(_node.ExtractFrameCount, 1, (int)FpsSlider.Maximum);
            _node.ExtractFrameCount = requestedCount;
            _isFrameControlSync = true;
            if (FpsSlider.Value != requestedCount) FpsSlider.Value = requestedCount;
            FpsValueText.Text = $"{requestedCount}";
            _isFrameControlSync = false;

            // Avoid duplicate frame at second boundary by sampling in [0, totalFrames-framesPerStep].
            var samplingWindowFrames = Math.Max(requestedCount, totalFrames - Math.Max(1, totalFrames / Math.Max(1, requestedCount)));
            var framesPerSec = Math.Max(1, (int)Math.Round(requestedCount / duration));
            _node.ExtractFps = framesPerSec;
            var interval = samplingWindowFrames / (double)requestedCount;
            var offsetMs = (interval / 2.0 / sourceFps) * 1000.0;
            var timestamps = FrameExtractionCalculator.CalculateAllExtractTimestamps(duration, sourceFps, framesPerSec);
            EstFramePerSecText.Text = $"{framesPerSec}";
            EstimatedFrameCountText.Text = $"{requestedCount:N0}";
            EstFrameIntervalText.Text = $"{(1000.0 / framesPerSec):0.#} ms";

            var indices = FrameExtractionCalculator.CalculateFrameIndicesPerSecond(sourceFps, framesPerSec);
            var indicesStr = string.Join(", ", indices.Take(4).Select(i => $"#{i}"));
            if (indices.Count > 4) indicesStr += "…";
            SetTextIfExists("FrameIndexPreviewText", $"Indices/giây: [{indicesStr}] | Offset: ~{offsetMs:0.#} ms | Mục tiêu: {requestedCount:N0}");
        }

        private void SetTextIfExists(string elementName, string text)
        {
            if (FindName(elementName) is TextBlock tb)
            {
                tb.Text = text;
            }
        }

        private void SetTextStyleIfExists(string elementName, string text, Brush foreground)
        {
            if (FindName(elementName) is TextBlock tb)
            {
                tb.Text = text;
                tb.Foreground = foreground;
            }
        }

        private static void SetConfigCheck(TextBlock target, bool ok, string okText, string warningText)
        {
            target.Text = ok ? $"✓ {okText}" : $"⚠ {warningText}";
            target.Foreground = ok ? new SolidColorBrush(Color.FromRgb(74, 222, 128)) : new SolidColorBrush(Color.FromRgb(248, 113, 113));
            target.FontWeight = ok ? FontWeights.Normal : FontWeights.SemiBold;
        }

        private static void OpenPathFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                if (text.IndexOfAny(new[] { '*', '?' }) >= 0)
                    return;
                var extension = System.IO.Path.GetExtension(text);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    var fileParent = System.IO.Path.GetDirectoryName(text);
                    if (!string.IsNullOrWhiteSpace(fileParent) && !Directory.Exists(fileParent))
                        Directory.CreateDirectory(fileParent);
                }
                else if (!Directory.Exists(text))
                {
                    Directory.CreateDirectory(text);
                }
            }
            catch
            {
                // best-effort for opening path
            }

            if (Directory.Exists(text) || File.Exists(text))
            {
                Process.Start(new ProcessStartInfo { FileName = text, UseShellExecute = true });
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(text);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                Process.Start(new ProcessStartInfo { FileName = parent, UseShellExecute = true });
            }
        }

        private static void EnsureParentDirectoryExists(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            try
            {
                var parent = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
                    Directory.CreateDirectory(parent);
            }
            catch
            {
                // best-effort
            }
        }

        private static void EnsureDirectoryExists(string? directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)) return;
            try
            {
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);
            }
            catch
            {
                // best-effort
            }
        }

        private static string FormatTime(TimeSpan value)
            => value.TotalHours >= 1 ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" : $"{value.Minutes:00}:{value.Seconds:00}";

        private static Brush GetTextBrush(string? colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey)) return new SolidColorBrush(Color.FromRgb(229, 231, 235));
            return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush ?? new SolidColorBrush(Color.FromRgb(229, 231, 235));
        }
    }
}
