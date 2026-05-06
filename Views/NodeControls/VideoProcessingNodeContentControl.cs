using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Effects;
using FlowMy.Helpers;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow.NodeExecutors;
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
        /// <summary>WPF preview font — export nhãn frame dùng cùng raster (Segoe UI Semibold → system UI).</summary>
        private static readonly FontFamily FrameLabelPreviewFontFamily = CreateFrameLabelPreviewFontFamily();

        private static FontFamily CreateFrameLabelPreviewFontFamily()
        {
            try { return new FontFamily("Segoe UI Semibold"); }
            catch { return SystemFonts.MessageFontFamily; }
        }

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

        private static readonly int[] PreviewQualityLevelHeights = { 144, 240, 480, 720, 1080, 1440, 2160 };

        private const double MinAutoFitNodeWidth = 540;
        private const double MinAutoFitNodeHeight = 340;
        private const double MaxAutoFitNodeWidth = 1280;
        private const double MaxAutoFitNodeHeight = 920;
        private const double MinPreviewHeight = 180;
        private const double MaxPreviewHeight = 620;
        private const double HorizontalPadding = 18;
        /// <summary>Half visual diameter of timeline scrub thumb (matches XAML ellipse).</summary>
        private const double ProgressThumbHalfWidth = 11;
        private const double NonPreviewContentHeight = 230;
        /// <summary>Bo góc node + khung preview — đồng bộ <c>NodeChromeCornerRadius</c> trong <see cref="VideoProcessingNodeControl"/>.</summary>
        private const double VideoNodeCornerRadius = 10;
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
        private double _frameResizeScale = 1.0;
        private bool _isLightTheme;
        private bool _isNodeZoomed;
        private double _prevNodeWidth;
        private double _prevNodeHeight;
        private double _prevNodeX;
        private double _prevNodeY;
        private double _pendingSeekRatio = -1;
        private DateTime _lastDragSeekAtUtc = DateTime.MinValue;
        private DateTime _lastSeekRequestAtUtc = DateTime.MinValue;
        private double _lastSeekTargetSeconds = -1;
        private double _lastSeekLatencyMs = -1;
        private bool _isSeekLatencyPending;
        private CancellationTokenSource? _sourceFpsProbeCts;
        private DateTime _dragReleaseBoostUntilUtc = DateTime.MinValue;
        private TimelineDragMode _timelineDragMode = TimelineDragMode.None;
        private TimelineDragMode _trimReviewDragMode = TimelineDragMode.None;
        private bool _previewEffectTemporarilyDisabled;
        private VideoEqEffect? _videoEqEffect;
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
        private bool _aspectAuto = true;
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
                SyncUserControlRoundedClip();
                RefreshLargeNodeUiScale();
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    SyncUserControlRoundedClip();
                    RefreshLargeNodeUiScale();
                    UpdatePreviewAspectRatio();
                }));
            };
            Unloaded += (_, _) => DetachSubscriptions();
        }

        private void InitializeInteractiveControls()
        {
            TabNavList.SelectionChanged += TabNavList_SelectionChanged;
            TabNavList.SelectedIndex = 0;
            foreach (var group in new[]
            {
                BottomBarGroupGeneral, BottomBarGroupGrading, BottomBarGroupFilters,
                BottomBarGroupAudio, BottomBarGroupExport, BottomBarGroupOutputs, BottomBarGroupSettings
            })
            {
                group.MouseEnter += (_, _) => UpdateActionButtonLabelVisibility();
                group.MouseLeave += (_, _) => UpdateActionButtonLabelVisibility();
            }
            SizeChanged += (_, _) =>
            {
                SyncUserControlRoundedClip();
                RefreshLargeNodeUiScale();
                UpdatePreviewAspectRatio();
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

            OpenVideoButton.Click += (_, _) => SelectVideo();
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
                RunProcessingFlow();
            };
            ExtractFramesButton.Click += (_, _) =>
            {
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
                ApplyAspectRatioToMedia();
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
            FrameLabelXSlider.ValueChanged += (_, e) => { _node.FrameLabelX = e.NewValue; UpdateFrameLabelPreviewUi(); };
            FrameLabelYSlider.ValueChanged += (_, e) => { _node.FrameLabelY = e.NewValue; UpdateFrameLabelPreviewUi(); };
            FrameLabelWSlider.ValueChanged += (_, e) => { _node.FrameLabelW = e.NewValue; UpdateFrameLabelPreviewUi(); };
            FrameLabelHSlider.ValueChanged += (_, e) => { _node.FrameLabelH = e.NewValue; UpdateFrameLabelPreviewUi(); };
            FrameLabelPaddingSlider.ValueChanged += (_, e) =>
            {
                _node.FrameLabelHorizontalPadding = (int)e.NewValue;
                FrameLabelPaddingLabel.Text = $"{(int)e.NewValue}px";
                UpdateFrameLabelPreviewUi();
            };
            FrameLabelPaddingVSlider.ValueChanged += (_, e) =>
            {
                _node.FrameLabelVerticalPadding = (int)e.NewValue;
                FrameLabelPaddingVLabel.Text = $"{(int)e.NewValue}px";
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

            TwoPassToggle.Checked += (_, _) => _node.TwoPassEnabled = true;
            TwoPassToggle.Unchecked += (_, _) => _node.TwoPassEnabled = false;
            AudioCodecCombo.SelectionChanged += (_, _) => _node.AudioCodec = (AudioCodecCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "aac";
            AudioBitrateCombo.SelectionChanged += (_, _) => _node.AudioBitrate = (AudioBitrateCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "192k";
        }

        private void ApplyThemeBrushes(Brush textBrush)
        {
            var headerBrush = _isLightTheme ? Brushes.Black : Brushes.White;
            TitleText.Foreground = headerBrush;
            IconView.Fill = headerBrush;
            VideoPathText.Foreground = textBrush;
            HwBadgeText.Foreground = textBrush;
        }

        private void InitializeIcon()
        {
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(string.Empty, typeof(Uri), "circle-video sharp-light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            if (iconUri != null) IconView.Source = iconUri;
            IconView.Fill = _isLightTheme ? Brushes.Black : Brushes.White;
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

            UpdateBottomBarGroupHighlight(Math.Max(0, idx));
        }

        private void UpdateBottomBarGroupHighlight(int tabIndex)
        {
            Border[] groups =
            {
                BottomBarGroupGeneral, BottomBarGroupGrading, BottomBarGroupFilters,
                BottomBarGroupAudio, BottomBarGroupExport, BottomBarGroupOutputs, BottomBarGroupSettings
            };

            var warmBrush = TryFindResource("ThemeWarmAccentBrush") as SolidColorBrush;
            var inactiveBorder = TryFindResource("ThemeBottomBarGroupInactiveBorderBrush") as Brush
                ?? TryFindResource("ThemeActionBarBorderBrush") as Brush ?? Brushes.Gray;
            var activeBg = TryFindResource("ThemeBottomBarActiveGroupBackgroundBrush") as Brush;

            for (var i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g == null) continue;

                var active = i == tabIndex;
                g.BorderBrush = active ? warmBrush ?? inactiveBorder : inactiveBorder;
                g.BorderThickness = new Thickness(active ? 2 : 1);

                if (active)
                    g.Background = activeBg ?? Brushes.Transparent;
                else
                    g.Background = Brushes.Transparent;
            }

            UpdateActionButtonLabelVisibility();
        }

        private void UpdateActionButtonLabelVisibility()
        {
            var activeIdx = Math.Max(0, TabNavList.SelectedIndex);
            Border[] groups =
            {
                BottomBarGroupGeneral, BottomBarGroupGrading, BottomBarGroupFilters,
                BottomBarGroupAudio, BottomBarGroupExport, BottomBarGroupOutputs, BottomBarGroupSettings
            };

            for (var i = 0; i < groups.Length; i++)
            {
                var showLabel = i == activeIdx || groups[i].IsMouseOver;
                ToggleLabelsInGroup(groups[i], showLabel);
            }
        }

        private static void ToggleLabelsInGroup(DependencyObject root, bool show)
        {
            if (root is TextBlock tb && tb.Name.EndsWith("Label", StringComparison.Ordinal))
            {
                tb.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
                ToggleLabelsInGroup(VisualTreeHelper.GetChild(root, i), show);
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
            if (propertyName is nameof(VideoProcessingNode.Width) or nameof(VideoProcessingNode.Height))
            {
                RefreshLargeNodeUiScale();
            }
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
            if (propertyName == nameof(VideoProcessingNode.SourceFps)) UpdateFrameExtractionPreview();
            if (propertyName == nameof(VideoProcessingNode.PreferredHwAccel)) HwBadgeText.Text = _node.PreferredHwAccel;
            if (propertyName == nameof(VideoProcessingNode.UseDialogVideoConfig))
            {
                _suppressControlSync = true;
                UseDialogVideoConfigCheckBox.IsChecked = _node.UseDialogVideoConfig;
                _suppressControlSync = false;
                ApplyConfigSourceMode();
            }
            if (propertyName is nameof(VideoProcessingNode.WatermarkEnabled) or nameof(VideoProcessingNode.WatermarkImagePath)
                or nameof(VideoProcessingNode.WatermarkPosition) or nameof(VideoProcessingNode.WatermarkOpacity)
                or nameof(VideoProcessingNode.WatermarkWidthFraction) or nameof(VideoProcessingNode.WatermarkInsetFraction))
                UpdateWatermarkPreviewUi();

            RefreshInfoText();
        }

        private void AttachSubscriptions()
        {
            if (_subscriptionsAttached) return;
            _node.AudioTracks.CollectionChanged += _audioTracksChangedHandler;
            _node.PropertyChanged += _propertyChangedHandler;
            AudioTracksList.ItemsSource = _node.AudioTracks;
            OverlayCanvasControl.ItemsSource = _node.Overlays;
            UpdateOverlayCanvasBounds();
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
            var windowSec = Math.Clamp(
                (int)Math.Round(_node.SecondsPerFrame),
                (int)SecondsPerFrameSlider.Minimum,
                (int)SecondsPerFrameSlider.Maximum);
            _node.SecondsPerFrame = windowSec;
            SecondsPerFrameValueText.Text = $"{windowSec}s";

            var sourceFps = Math.Max(1, _node.SourceFps);
            var maxInWindow = Math.Max(1, (int)Math.Round(windowSec * sourceFps));

            _isFrameControlSync = true;
            try
            {
                FpsSlider.Maximum = maxInWindow;
                var framesPerWindow = Math.Clamp(_node.ExtractFrameCount, 1, maxInWindow);
                _node.ExtractFrameCount = framesPerWindow;
                FpsSlider.Value = framesPerWindow;
                FpsValueText.Text = $"{framesPerWindow}";
                _node.ExtractFps = framesPerWindow / (double)windowSec;
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
                var duration = Math.Max(0.1, GetNaturalDurationSeconds());
                var sourceFps = Math.Max(1, _node.SourceFps);

                var totalFrames = Math.Max(1, (int)Math.Floor(duration * sourceFps));
                var windowSec = (int)Math.Round(_node.SecondsPerFrame);
                windowSec = Math.Clamp(windowSec, (int)SecondsPerFrameSlider.Minimum, (int)SecondsPerFrameSlider.Maximum);
                var maxInWindow = Math.Max(1, (int)Math.Round(windowSec * sourceFps));
                FpsSlider.Maximum = _node.ExtractAllFrames ? totalFrames : maxInWindow;

                FpsSlider.Value = Math.Clamp(_node.ExtractFrameCount, 1, (int)FpsSlider.Maximum);
                SecondsPerFrameSlider.Value = windowSec;
                UseDialogVideoConfigCheckBox.IsChecked = _node.UseDialogVideoConfig;
                OutputBase64CheckBox.IsChecked = _node.OutputBase64;
                PreferGpuCheckBox.IsChecked = _node.PreferGpu;
                SourceAudioToggle.IsChecked = _node.SourceAudioEnabled;
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
                TextOverlayToggle.IsChecked = _node.TextOverlayEnabled;
                OverlayTextBox.Text = _node.OverlayText;
                TextSizeSlider.Value = _node.OverlayFontSize;
                TextSizeLabel.Text = $"{_node.OverlayFontSize}px";
                FrameLabelToggle.IsChecked = _node.FrameLabelEnabled;
                FrameLabelTemplateTextBox.Text = _node.FrameLabelTemplate;
                FrameLabelXSlider.Value = _node.FrameLabelX;
                FrameLabelYSlider.Value = _node.FrameLabelY;
                FrameLabelWSlider.Value = _node.FrameLabelW;
                FrameLabelHSlider.Value = _node.FrameLabelH;
                FrameLabelPaddingSlider.Value = _node.FrameLabelHorizontalPadding;
                FrameLabelPaddingLabel.Text = $"{_node.FrameLabelHorizontalPadding}px";
                FrameLabelPaddingVSlider.Value = _node.FrameLabelVerticalPadding;
                FrameLabelPaddingVLabel.Text = $"{_node.FrameLabelVerticalPadding}px";
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
            }
            finally
            {
                _suppressControlSync = false;
            }

            ApplyPreviewQualitySettings();
            ApplyConfigSourceMode();
            UpdateFrameLabelPreviewUi();
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
            }
            catch (Exception ex)
            {
                AppendLog($"Preview error: {ex.Message}");
                PreviewMedia.Stop();
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

            var natH = PreviewMedia.NaturalVideoHeight;
            var rect = GetDisplayedVideoRect();
            var areaH = Math.Max(1, rect.Height);
            var srcPixelH = natH > 0 ? natH : 720;
            var drawtextPx = VideoProcessingNodeExecutor.ComputeFrameLabelDrawtextFontPixelSize(_node, natH > 0 ? natH : (int?)null);
            var previewFontDip = drawtextPx * (areaH / (double)srcPixelH);
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

            FrameLabelPreviewOverlay.HorizontalAlignment = HorizontalAlignment.Left;
            FrameLabelPreviewOverlay.VerticalAlignment = VerticalAlignment.Top;
            FrameLabelPreviewOverlay.Width = Math.Max(20, _node.FrameLabelW * areaW);
            FrameLabelPreviewOverlay.Height = Math.Max(18, _node.FrameLabelH * areaH);
            FrameLabelPreviewOverlay.Margin = new Thickness(rect.X + (_node.FrameLabelX * areaW), rect.Y + (_node.FrameLabelY * areaH), 0, 0);
            var sourceScale = VideoProcessingNodeExecutor.ComputeFrameLabelSourceScale(natH > 0 ? natH : (int?)null);
            var padVidX = Math.Max(0, (int)Math.Round(_node.FrameLabelHorizontalPadding * sourceScale));
            var padVidY = Math.Max(0, (int)Math.Round(_node.FrameLabelVerticalPadding * sourceScale));
            var padPx = padVidX * (areaW / srcW);
            var padPy = padVidY * (areaH / srcH);
            FrameLabelPreviewOverlay.Padding = new Thickness(padPx, padPy, padPx, padPy);
            AutoFitFrameLabelTextToBounds();
            FrameLabelPosLabel.Text = $"X {_node.FrameLabelX:0.###} | Y {_node.FrameLabelY:0.###}";
            FrameLabelSizeLabel.Text = $"W {_node.FrameLabelW:0.###} | H {_node.FrameLabelH:0.###}";
        }

        private void AutoFitFrameLabelTextToBounds()
        {
            if (FrameLabelPreviewOverlay == null || FrameLabelPreviewText == null)
                return;

            var availableW = Math.Max(1, FrameLabelPreviewOverlay.Width - FrameLabelPreviewOverlay.Padding.Left - FrameLabelPreviewOverlay.Padding.Right);
            var availableH = Math.Max(1, FrameLabelPreviewOverlay.Height - FrameLabelPreviewOverlay.Padding.Top - FrameLabelPreviewOverlay.Padding.Bottom);
            var text = FrameLabelPreviewText.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return;

            var dpi = VisualTreeHelper.GetDpi(FrameLabelPreviewText);
            var typeface = new Typeface(
                FrameLabelPreviewText.FontFamily,
                FrameLabelPreviewText.FontStyle,
                FrameLabelPreviewText.FontWeight,
                FrameLabelPreviewText.FontStretch);

            var originalSize = Math.Max(4, FrameLabelPreviewText.FontSize);
            var fitSize = originalSize;
            for (var size = originalSize; size >= 7; size -= 0.5)
            {
                var ft = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    size,
                    Brushes.Black,
                    dpi.PixelsPerDip);

                if (ft.Width <= availableW && ft.Height <= availableH)
                {
                    fitSize = size;
                    break;
                }
            }

            FrameLabelPreviewText.FontSize = fitSize;
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
            if (OverlayCanvasControl == null || VideoAreaGrid == null || PreviewMedia.Source == null) return;
            var rect = GetDisplayedVideoRect();
            OverlayCanvasControl.HorizontalAlignment = HorizontalAlignment.Left;
            OverlayCanvasControl.VerticalAlignment = VerticalAlignment.Top;
            OverlayCanvasControl.Margin = new Thickness(rect.X, rect.Y, 0, 0);
            OverlayCanvasControl.Width = Math.Max(1, rect.Width);
            OverlayCanvasControl.Height = Math.Max(1, rect.Height);
        }

        /// <summary>WPF <see cref="MediaElement"/> không luôn clip theo CornerRadius — ép clip hình chữ nhật bo góc.</summary>
        private void SyncVideoViewportClip()
        {
            if (VideoViewportClipBorder == null || !VideoViewportClipBorder.IsLoaded)
                return;

            var w = Math.Max(1d, VideoViewportClipBorder.ActualWidth);
            var h = Math.Max(1d, VideoViewportClipBorder.ActualHeight);
            var maxR = Math.Min(w, h) / 2 - 0.001;
            var r = Math.Min(VideoNodeCornerRadius, Math.Max(0, maxR));
            VideoViewportClipBorder.Clip = r <= 0.25
                ? new RectangleGeometry(new Rect(0, 0, w, h))
                : new RectangleGeometry(new Rect(0, 0, w, h), r, r);
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
            if (string.IsNullOrWhiteSpace(path)) return;

            _sourceFpsProbeCts?.Cancel();
            _sourceFpsProbeCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var ct = _sourceFpsProbeCts.Token;

            try
            {
                var fps = await ProbeSourceFpsAsync(path, ct);
                if (fps > 0)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _node.SourceFps = fps;
                        RefreshInfoText(); // includes UpdateFrameExtractionPreview()
                    }, DispatcherPriority.Loaded);
                }
            }
            catch
            {
                // best-effort: don't block UI if probing fails.
            }
        }

        private static async Task<double> ProbeSourceFpsAsync(string inputPath, CancellationToken ct)
        {
            // Keep ffprobe invocation consistent with VideoProcessingNodeExecutor.
            var ffprobeExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffprobe");
            if (string.IsNullOrWhiteSpace(ffprobeExe)) return 0;

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
                "-show_entries", "stream=r_frame_rate",
                "-of", "default=nokey=1:noprint_wrappers=1",
                inputPath
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p == null) return 0;

            var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);

            var value = output.Trim();
            if (string.IsNullOrWhiteSpace(value)) return 0;

            if (value.Contains('/'))
            {
                var parts = value.Split('/');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) &&
                    d > 0)
                {
                    return n / d;
                }
            }

            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fps)
                ? fps
                : 0;
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
            Canvas.SetLeft(ProgressThumb, Math.Max(0, (barWidth * ratio) - ProgressThumbHalfWidth));
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
            ProgressThumb.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private void ProgressBarHitArea_MouseEnter(object sender, MouseEventArgs e)
        {
            ProgressThumb.Visibility = Visibility.Visible;
            ApplyTimelineThumbHoverVisual(true);
        }

        private void ProgressBarHitArea_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isProgressDragging)
                ProgressThumb.Visibility = Visibility.Collapsed;
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

            Color glow = Color.FromRgb(99, 102, 241);
            if (TryFindResource("ThemeAccentBrush") is SolidColorBrush ab && ab.Color.A > 0)
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
            Canvas.SetLeft(ProgressThumb, Math.Max(0, (barWidth * ratio) - ProgressThumbHalfWidth));
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
            UpdateFrameLabelPreviewUi();
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
                PreviewMedia.Effect = null;
                GradingOverlay.Background = Brushes.Transparent;
                PreviewMedia.Opacity = 1.0;
                return;
            }

            var brightness = Math.Clamp(_node.Brightness, -1.0, 1.0);
            var contrast = Math.Clamp(_node.Contrast, 0.1, 3.0);
            var saturation = Math.Clamp(_node.Saturation, 0.0, 3.0);
            var hueDeg = Math.Clamp(_node.Hue, -180.0, 180.0);
            var gamma = Math.Clamp(_node.Gamma, 0.1, 3.0);

            if (VideoEqEffect.ShaderAvailable)
            {
                _videoEqEffect ??= new VideoEqEffect();
                var hueRad = hueDeg * (Math.PI / 180.0);
                _videoEqEffect.Bc = new System.Windows.Point(brightness, contrast);
                _videoEqEffect.Sg = new System.Windows.Point(saturation, gamma);
                _videoEqEffect.HueCs = new System.Windows.Point(Math.Cos(hueRad), Math.Sin(hueRad));
                PreviewMedia.Effect = _videoEqEffect;
                GradingOverlay.Background = Brushes.Transparent;
                PreviewMedia.Opacity = 1.0;
                return;
            }

            // Software fallback — approximate tint + opacity (legacy preview).
            var strength = (_node.PreviewVisualStrengthMode ?? "balanced").ToLowerInvariant();
            var strengthScale = strength switch
            {
                "fast" => 0.65,
                "strong" => 1.45,
                _ => 1.0
            };

            var tintStrength = Math.Min(0.45, (Math.Abs(hueDeg) / 180.0 * 0.28 + Math.Max(0, saturation - 1.0) * 0.06) * strengthScale);
            var hueColor = HsvToColor((hueDeg + 360.0) % 360.0, 0.9, 1.0);
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

            PreviewMedia.Effect = null;
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
                    TextAlignment = "Left",
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
                OverlaySourcePathTextBox.IsEnabled = has;
                OverlaySourceTextArea.IsEnabled = has;
                OverlayXSlider.IsEnabled = has;
                OverlayYSlider.IsEnabled = has;
                OverlayWidthSlider.IsEnabled = has;
                OverlayHeightSlider.IsEnabled = has;
                OverlayOpacitySlider.IsEnabled = has;
                OverlayRotationSlider.IsEnabled = has;
                OverlayFontFamilyCombo.IsEnabled = has;
                OverlayFontColorTextBox.IsEnabled = has;
                OverlayFontSizeSlider.IsEnabled = has;
                OverlayTextAlignRow.IsEnabled = has;
                OverlayVisibleCheckBox.IsEnabled = has;
                OverlayLockedCheckBox.IsEnabled = has;

                if (!has)
                {
                    OverlayTypeCombo.SelectedIndex = -1;
                    OverlaySourcePathTextBox.Text = string.Empty;
                    OverlaySourceTextArea.Text = string.Empty;
                    OverlayTextPropsPanel.Visibility = Visibility.Collapsed;
                    OverlayImageSourcePanel.Visibility = Visibility.Collapsed;
                    return;
                }

                OverlayTypeCombo.SelectedIndex = (item!.Type ?? "text").ToLowerInvariant() switch
                {
                    "image" => 1,
                    "logo" => 2,
                    _ => 0
                };

                var isText = string.Equals((item.Type ?? "text").Trim(), "text", StringComparison.OrdinalIgnoreCase);
                OverlayTextPropsPanel.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
                OverlayImageSourcePanel.Visibility = isText ? Visibility.Collapsed : Visibility.Visible;

                if (isText)
                    OverlaySourceTextArea.Text = item.Source;
                else
                    OverlaySourcePathTextBox.Text = item.Source;

                OverlayXSlider.Value = item.X;
                OverlayYSlider.Value = item.Y;
                OverlayWidthSlider.Value = item.Width;
                OverlayHeightSlider.Value = item.Height;
                OverlayOpacitySlider.Value = item.Opacity;
                OverlayRotationSlider.Value = item.Rotation;
                var desiredFont = string.IsNullOrWhiteSpace(item.FontFamily) ? "Arial" : item.FontFamily.Trim();
                var match = OverlayFontFamilyCombo.Items.OfType<string>()
                    .FirstOrDefault(s => string.Equals(s, desiredFont, StringComparison.OrdinalIgnoreCase));
                OverlayFontFamilyCombo.SelectedItem = match;
                OverlayFontFamilyCombo.Text = match ?? desiredFont;
                OverlayFontColorTextBox.Text = item.FontColor;
                OverlayFontSizeSlider.Value = item.FontSize;
                var align = (item.TextAlignment ?? "Left").Trim().ToLowerInvariant();
                OverlayAlignLeftRadio.IsChecked = align != "center" && align != "right";
                OverlayAlignCenterRadio.IsChecked = align == "center";
                OverlayAlignRightRadio.IsChecked = align == "right";
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
            var isText = string.Equals(selectedType, "text", StringComparison.OrdinalIgnoreCase);
            OverlayTextPropsPanel.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
            OverlayImageSourcePanel.Visibility = isText ? Visibility.Collapsed : Visibility.Visible;
            selected.Source = isText ? (OverlaySourceTextArea.Text ?? string.Empty) : (OverlaySourcePathTextBox.Text ?? string.Empty);
            selected.X = OverlayXSlider.Value;
            selected.Y = OverlayYSlider.Value;
            selected.Width = OverlayWidthSlider.Value;
            selected.Height = OverlayHeightSlider.Value;
            selected.Opacity = OverlayOpacitySlider.Value;
            selected.Rotation = OverlayRotationSlider.Value;
            if (isText)
            {
                var family = (OverlayFontFamilyCombo.SelectedItem as string)
                    ?? OverlayFontFamilyCombo.Text
                    ?? "Arial";
                selected.FontFamily = family;
                selected.FontColor = OverlayFontColorTextBox.Text;
                selected.FontSize = (int)OverlayFontSizeSlider.Value;
                selected.TextAlignment = OverlayAlignCenterRadio.IsChecked == true ? "Center"
                    : OverlayAlignRightRadio.IsChecked == true ? "Right"
                    : "Left";
            }
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
                return;
            }

            SyncRuntimeConfigFromUi();
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
                                line => AppendLog(line),
                                (pct, status) => UpdateProgress(pct, status),
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
                                line => AppendLog(line),
                                (pct, status) => UpdateProgress(pct, status),
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
            var outputPath = dlg.FileName;
            var position = PreviewMedia.Position.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    // Source of truth: FFmpeg pipeline (same as extract output).
                    await VideoProcessingNodeExecutor.RunSnapshotAsync(_node, position, outputPath, System.Threading.CancellationToken.None);
                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"✅ Snapshot saved (ffmpeg): {outputPath}")));
                }
                catch (Exception ex)
                {
                    try
                    {
                        // UI capture fallback if FFmpeg fails.
                        await Dispatcher.InvokeAsync(() =>
                        {
                            VideoContainerGrid.UpdateLayout();
                            VideoAreaGrid.UpdateLayout();
                            var dpi = VisualTreeHelper.GetDpi(VideoContainerGrid);
                            var containerW = Math.Max(1, (int)Math.Round(VideoContainerGrid.ActualWidth * dpi.DpiScaleX));
                            var containerH = Math.Max(1, (int)Math.Round(VideoContainerGrid.ActualHeight * dpi.DpiScaleY));
                            var rtb = new RenderTargetBitmap(containerW, containerH, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
                            rtb.Render(VideoContainerGrid);
                            var displayedRect = GetDisplayedVideoRect();
                            var topLeft = VideoAreaGrid.TranslatePoint(new Point(displayedRect.X, displayedRect.Y), VideoContainerGrid);
                            var cropX = Math.Max(0, (int)Math.Floor(topLeft.X * dpi.DpiScaleX));
                            var cropY = Math.Max(0, (int)Math.Floor(topLeft.Y * dpi.DpiScaleY));
                            var cropW = Math.Max(1, Math.Min(containerW - cropX, (int)Math.Round(displayedRect.Width * dpi.DpiScaleX)));
                            var cropH = Math.Max(1, Math.Min(containerH - cropY, (int)Math.Round(displayedRect.Height * dpi.DpiScaleY)));
                            var cropped = new CroppedBitmap(rtb, new Int32Rect(cropX, cropY, cropW, cropH));
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(cropped));
                            using var fs = new System.IO.FileStream(outputPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
                            encoder.Save(fs);
                        });
                        _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"✅ Snapshot saved (UI fallback): {outputPath}")));
                    }
                    catch (Exception fallbackEx)
                    {
                        _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"❌ Snapshot failed: {ex.Message} | fallback: {fallbackEx.Message}")));
                    }
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
            var shellBg = isLight ? Color.FromRgb(242, 245, 252) : Color.FromRgb(15, 15, 23);
            Background = new SolidColorBrush(shellBg);
            Foreground = new SolidColorBrush(SurfaceContrast.TextPrimaryOnSurface(shellBg));

            Color accentColor = Color.FromRgb(124, 107, 248);
            if (Application.Current?.TryFindResource("PrimaryBrush") is SolidColorBrush appPrimary && appPrimary.Color.A > 0)
                accentColor = appPrimary.Color;

            Color cardTop = isLight ? Color.FromArgb(245, 255, 255, 255) : Color.FromArgb(26, 255, 255, 255);
            Color cardEffective = SurfaceContrast.CompositeOver(cardTop, shellBg);
            Color innerTop = isLight ? Color.FromArgb(216, 242, 245, 250) : Color.FromArgb(24, 0, 0, 0);
            Color innerEffective = SurfaceContrast.CompositeOver(innerTop, shellBg);

            Color primaryText = SurfaceContrast.TextPrimaryOnSurface(cardEffective);
            Color secondaryText = SurfaceContrast.TextSecondaryOnSurface(innerEffective);

            Resources["ThemeTextPrimaryBrush"] = new SolidColorBrush(primaryText);
            Resources["ThemeTextSecondaryBrush"] = new SolidColorBrush(secondaryText);
            Resources["ThemeCardBackgroundBrush"] = new SolidColorBrush(cardTop);
            Resources["ThemeCardBorderBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x4A, 0x6B, 0x7A, 0x8A) : Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInnerCardBackgroundBrush"] = new SolidColorBrush(innerTop);
            Resources["ThemeInnerCardBorderBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x52, 0x9C, 0xAA, 0xBC) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(248, 251, 255) : Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(178, 191, 212) : Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputForegroundBrush"] = new SolidColorBrush(SurfaceContrast.TextPrimaryOnSurface(
                SurfaceContrast.CompositeOver(isLight ? Color.FromRgb(248, 251, 255) : Color.FromRgb(34, 36, 46), shellBg)));
            Resources["ThemeOverlayBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xCC, 0xEC, 0xF1, 0xF8) : Color.FromArgb(0xAA, 0x00, 0x00, 0x00));
            Resources["ThemeOverlayBorderBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x58, 0x95, 0xA4, 0xBA) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTimelinePanelBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xF0, 0xE9, 0xEF, 0xF8) : Color.FromArgb(0xEE, 0x0A, 0x0A, 0x18));
            Resources["ThemeTrackBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x60, 0x95, 0xA4, 0xBA) : Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTimelineTrackBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(208, 216, 228) : Color.FromRgb(52, 54, 66));
            Resources["ThemeTimelineProgressBrush"] = new SolidColorBrush(accentColor);
            Resources["ThemeTimelineThumbStrokeBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(72, 82, 98) : Color.FromRgb(226, 232, 245));
            Resources["ThemeAccentGlowColor"] = accentColor;
            Resources["ThemeAccentBrush"] = new SolidColorBrush(accentColor);

            Color warmAmber = Color.FromRgb(0xF5, 0x9E, 0x0B);
            Resources["ThemeWarmAccentBrush"] = new SolidColorBrush(warmAmber);
            Resources["ThemeWarmAccentBrushSoft"] = new SolidColorBrush(Color.FromArgb(0x66, warmAmber.R, warmAmber.G, warmAmber.B));
            Resources["ThemeBottomBarGroupInactiveBorderBrush"] = new SolidColorBrush(
                isLight ? Color.FromArgb(0x90, 0x9A, 0xAA, 0xBC) : Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF));
            Resources["ThemeBottomBarActiveGroupBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0x2A, warmAmber.R, warmAmber.G, warmAmber.B));

            Color chromePrimaryBg = isLight ? Color.FromRgb(226, 232, 246) : Color.FromRgb(48, 50, 64);
            Color chromePrimaryHover = isLight ? Color.FromRgb(210, 218, 238) : Color.FromRgb(58, 61, 78);
            Color chromeSecondaryBg = isLight ? Color.FromRgb(236, 240, 250) : Color.FromRgb(40, 42, 54);
            Color chromeSecondaryHover = isLight ? Color.FromRgb(220, 228, 244) : Color.FromRgb(50, 52, 68);
            Resources["ThemeVideoChromePrimaryBgBrush"] = new SolidColorBrush(chromePrimaryBg);
            Resources["ThemeVideoChromePrimaryHoverBgBrush"] = new SolidColorBrush(chromePrimaryHover);
            Resources["ThemeVideoChromePrimaryFgBrush"] = new SolidColorBrush(SurfaceContrast.TextPrimaryOnSurface(SurfaceContrast.CompositeOver(chromePrimaryBg, shellBg)));
            Resources["ThemeVideoChromePrimaryBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(160, 175, 200) : Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF));
            Resources["ThemeVideoChromeSecondaryBgBrush"] = new SolidColorBrush(chromeSecondaryBg);
            Resources["ThemeVideoChromeSecondaryHoverBgBrush"] = new SolidColorBrush(chromeSecondaryHover);
            Resources["ThemeVideoChromeSecondaryFgBrush"] = new SolidColorBrush(SurfaceContrast.TextPrimaryOnSurface(SurfaceContrast.CompositeOver(chromeSecondaryBg, shellBg)));
            Resources["ThemeVideoChromeSecondaryBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(150, 168, 192) : Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));
            Resources["ThemePresetChipBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(230, 234, 244) : Color.FromRgb(36, 37, 48));
            Resources["ThemePresetChipBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(160, 175, 198) : Color.FromRgb(58, 60, 76));
            Resources["ThemePresetChipHoverBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(212, 220, 238) : Color.FromRgb(48, 50, 66));
            Resources["ThemePresetChipPressedBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(198, 208, 230) : Color.FromRgb(44, 46, 60));
            Resources["ThemePresetChipResetBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(220, 224, 234) : Color.FromRgb(40, 44, 56));
            Resources["ThemePresetChipResetBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(140, 155, 180) : Color.FromRgb(70, 74, 90));
            Color transportPlayBg = isLight ? Color.FromRgb(86, 78, 220) : Color.FromRgb(99, 102, 241);
            Color transportPlayHoverBg = isLight ? Color.FromRgb(72, 64, 200) : Color.FromRgb(79, 82, 220);
            Resources["ThemeTransportPlayBgBrush"] = new SolidColorBrush(transportPlayBg);
            Resources["ThemeTransportPlayHoverBgBrush"] = new SolidColorBrush(transportPlayHoverBg);
            Resources["ThemeTransportPlayFgBrush"] = new SolidColorBrush(SurfaceContrast.TextPrimaryOnSurface(transportPlayBg));
            Resources["ThemeTransportIconHoverBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(210, 218, 235) : Color.FromRgb(48, 50, 64));
            Resources["ThemeQuickOverlayHoverBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(214, 222, 238) : Color.FromRgb(52, 54, 70));
            Resources["ThemeVideoOpenButtonFgBrush"] = new SolidColorBrush(SurfaceContrast.TextPrimaryOnSurface(Color.FromRgb(220, 38, 38)));
            Resources["ThemeValueBadgeBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(220, 228, 240) : Color.FromRgb(42, 43, 56));

            Color framePreviewBg = isLight ? Color.FromArgb(250, 255, 255, 255) : Color.FromArgb(235, 28, 30, 38);
            Color framePreviewFg = SurfaceContrast.TextPrimaryOnSurface(SurfaceContrast.CompositeOver(framePreviewBg, shellBg));
            Resources["ThemeFrameLabelPreviewBg"] = new SolidColorBrush(framePreviewBg);
            Resources["ThemeFrameLabelPreviewFg"] = new SolidColorBrush(framePreviewFg);
            Resources["ThemeTabNavBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xCC, 0xE8, 0xEE, 0xF7) : Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
            Resources["ThemeLogContainerBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xD8, 0xF5, 0xF8, 0xFD) : Color.FromArgb(0x0C, 0x00, 0x00, 0x00));
            Resources["ThemeActionBarBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xEF, 0xEA, 0xF1, 0xFB) : Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            Resources["ThemeActionBarBorderBrush"] = new SolidColorBrush(
                isLight ? Color.FromArgb(0xAA, warmAmber.R, warmAmber.G, warmAmber.B) : Color.FromArgb(0x5A, warmAmber.R, warmAmber.G, warmAmber.B));
            Resources["ThemeOnAccentTextBrush"] = new SolidColorBrush(SurfaceContrast.TextPrimaryOnSurface(accentColor));
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

            // Secondary button chips — contrast checked against real fill colors.
            Color secBgTop = isLight ? Color.FromArgb(221, 210, 220, 235) : Color.FromArgb(37, 255, 255, 255);
            Color secEffective = SurfaceContrast.CompositeOver(secBgTop, shellBg);
            Resources["SecondaryButtonBackground"] = new SolidColorBrush(secBgTop);
            Resources["SecondaryButtonForeground"] = new SolidColorBrush(SurfaceContrast.TextPrimaryOnSurface(secEffective));
            Resources["SecondaryButtonBorder"] = new SolidColorBrush(
                isLight ? Color.FromRgb(160, 175, 195) : Color.FromArgb(0x40, 255, 255, 255));

            var textPrimary = (Brush)Resources["ThemeTextPrimaryBrush"];
            var textSecondary = (Brush)Resources["ThemeTextSecondaryBrush"];
            var headerBrush = isLight ? Brushes.Black : Brushes.White;
            SetForegroundIfExists("TimeCurrentText", textSecondary);
            SetForegroundIfExists("TimeTotalText", textSecondary);
            SetForegroundIfExists("SeekPerfText", textSecondary);
            SetForegroundIfExists("FrameInfoText", textPrimary);
            SetForegroundIfExists("VideoPathText", textSecondary);
            SetForegroundIfExists("CodecInfoText", textSecondary);
            SetForegroundIfExists("AudioSummaryText", textSecondary);
            SetForegroundIfExists("ConfigMissingSummaryText", textPrimary);
            SetForegroundIfExists("TitleText", headerBrush);
            if (IconView != null)
                IconView.Fill = headerBrush;

            ThemeModeButton.Content = CreateThemeModeIcon(isLight ? "moon regular" : "sun-bright duotone-thin", isLight);
            SetTransportIcons();
            SyncUserControlRoundedClip();
            UpdateBottomBarGroupHighlight(Math.Max(0, TabNavList.SelectedIndex));
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
            if (_node == null || _host == null || _node.Border == null) return;

            var border = _node.Border;
            var minW = border.MinWidth > 0 ? border.MinWidth : 540;
            var minH = border.MinHeight > 0 ? border.MinHeight : 340;

            // Collapse back to pre-expand frame.
            if (_isNodeZoomed)
            {
                var restoreX = _prevNodeX;
                var restoreY = _prevNodeY;
                var restoreW = _prevNodeWidth > 0 ? _prevNodeWidth : minW;
                var restoreH = _prevNodeHeight > 0 ? _prevNodeHeight : minH;

                _node.X = restoreX;
                _node.Y = restoreY;
                _node.Width = Math.Max(minW, restoreW);
                _node.Height = Math.Max(minH, restoreH);
                border.Width = _node.Width;
                border.Height = _node.Height;
                _host.UpdateNodePosition(_node, restoreX, restoreY);
                _host.UpdateCanvasSize();
                if (_host is WorkflowEditorWindow win)
                    win.SetViewportExpandedUiHidden(false);

                _isNodeZoomed = false;
                ToggleNodeSizeButton.Content = new TextBlock { Text = "⤢", FontSize = 12 };
                return;
            }

            // Expand to current visible workflow viewport (same behavior idea as HtmlUi node).
            if (_host is WorkflowEditorWindow winExpand)
                winExpand.SetViewportExpandedUiHidden(true);
            var vp = GetWorkflowViewportCanvasRect();
            if (vp.IsEmpty || vp.Width < 1 || vp.Height < 1)
            {
                // Fallback to previous behavior if viewport rect can't be resolved.
                _prevNodeWidth = _node.Width;
                _prevNodeHeight = _node.Height;
                _prevNodeX = _node.X;
                _prevNodeY = _node.Y;
                _node.Width = Math.Max(1360, _node.Width);
                _node.Height = Math.Max(768, _node.Height);
                border.Width = _node.Width;
                border.Height = _node.Height;
                _isNodeZoomed = true;
                ToggleNodeSizeButton.Content = new TextBlock { Text = "⤡", FontSize = 12 };
                return;
            }

            _prevNodeX = _node.X;
            _prevNodeY = _node.Y;
            _prevNodeWidth = _node.Width;
            _prevNodeHeight = _node.Height;

            var nextW = Math.Max(minW, vp.Width);
            var nextH = Math.Max(minH, vp.Height);
            _node.X = vp.Left;
            _node.Y = vp.Top;
            _node.Width = nextW;
            _node.Height = nextH;
            border.Width = nextW;
            border.Height = nextH;
            _host.UpdateNodePosition(_node, vp.Left, vp.Top);
            _host.UpdateCanvasSize();

            _isNodeZoomed = true;
            ToggleNodeSizeButton.Content = new TextBlock { Text = "⤡", FontSize = 12 };
        }

        private Rect GetWorkflowViewportCanvasRect()
        {
            if (_host == null) return Rect.Empty;
            var sv = _host.ScrollViewer;
            if (sv == null) return Rect.Empty;
            try { sv.UpdateLayout(); } catch { /* ignore */ }

            var scrollX = sv.HorizontalOffset;
            var scrollY = sv.VerticalOffset;
            var viewportW = sv.ViewportWidth > 1 ? sv.ViewportWidth : sv.ActualWidth;
            var viewportH = sv.ViewportHeight > 1 ? sv.ViewportHeight : sv.ActualHeight;
            if (viewportW < 1 || viewportH < 1) return Rect.Empty;

            var z = _host.ScaleTransform?.ScaleX ?? 1.0;
            if (z <= 0.0001) z = 1.0;
            var tx = _host.TranslateTransform?.X ?? 0;
            var ty = _host.TranslateTransform?.Y ?? 0;

            var canvasLeft = (scrollX - tx) / z;
            var canvasTop = (scrollY - ty) / z;
            var canvasW = viewportW / z;
            var canvasH = viewportH / z;
            if (double.IsNaN(canvasLeft) || double.IsInfinity(canvasLeft) ||
                double.IsNaN(canvasTop) || double.IsInfinity(canvasTop))
                return Rect.Empty;

            return new Rect(canvasLeft, canvasTop, canvasW, canvasH);
        }

        private void RefreshLargeNodeUiScale()
        {
            if (RootContentGrid == null) return;

            const double baseH = 1080.0;
            const double minScaleActivation = 1.05;
            var nodeH = _node?.Height ?? 0;
            var factor = Math.Max(1.0, nodeH / baseH);
            if (factor < minScaleActivation)
                factor = 1.0;

            RootContentGrid.LayoutTransform = factor <= 1.001
                ? Transform.Identity
                : new ScaleTransform(factor, factor);
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

            SyncVideoViewportClip();
            UpdateOverlayCanvasBounds();
            UpdateWatermarkPreviewUi();
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
            double targetW;
            double targetH;
            if (_aspectAuto)
            {
                var natW = PreviewMedia.NaturalVideoWidth > 0 ? PreviewMedia.NaturalVideoWidth : 1280;
                var natH = PreviewMedia.NaturalVideoHeight > 0 ? PreviewMedia.NaturalVideoHeight : 720;
                targetW = natW;
                targetH = natH;
            }
            else if (_selectedAspectW > 0 && _selectedAspectH > 0)
            {
                var baseW = 1280.0;
                targetW = baseW;
                targetH = baseW * (_selectedAspectH / _selectedAspectW);
            }
            else
            {
                targetW = 1280;
                targetH = 720;
            }

            var qualityCap = GetConfiguredPreviewMaxHeight();
            if (qualityCap.HasValue && qualityCap.Value > 0 && targetH > qualityCap.Value)
            {
                var scale = qualityCap.Value / targetH;
                targetW *= scale;
                targetH = qualityCap.Value;
            }

            PreviewMedia.Width = targetW;
            PreviewMedia.Height = targetH;

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
            if (available <= 32) return;

            // Keep a stable 2/3 (video) + 1/3 (log) split so the log fills
            // all remaining height and stays visually balanced.
            var targetVideoH = Math.Max(16, available * (2.0 / 3.0));
            var targetLogH = Math.Max(16, available - targetVideoH);

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
            if (_node.ExtractAllFrames)
            {
                var durationAll = GetNaturalDurationSeconds();
                var sourceFpsAll = _node.SourceFps > 0 ? _node.SourceFps : 30;
                if (durationAll <= 0) durationAll = 1;
                var total = Math.Max(1, (int)Math.Floor(durationAll * sourceFpsAll));
                FpsSlider.Maximum = total;
                EstFramePerSecText.Text = $"{sourceFpsAll:0.##}";
                EstimatedFrameCountText.Text = $"{total:N0}";
                EstFrameIntervalText.Text = $"{(1000.0 / sourceFpsAll):0.#} ms";
                SetTextIfExists("FrameIndexPreviewText", $"All frames mode: 0..{Math.Max(0, (int)sourceFpsAll - 1)} mỗi giây");
                return;
            }

            var duration = GetNaturalDurationSeconds();
            var sourceFps = _node.SourceFps > 0 ? _node.SourceFps : 30;
            if (duration <= 0) duration = 1;

            // New semantics:
            // SecondsPerFrameSlider is the window size (seconds).
            // FpsSlider is how many frames to extract inside that window.
            var windowSec = Math.Max(1, (int)Math.Round(_node.SecondsPerFrame));
            windowSec = Math.Clamp(windowSec, (int)SecondsPerFrameSlider.Minimum, (int)SecondsPerFrameSlider.Maximum);
            var maxInWindow = Math.Max(1, (int)Math.Round(windowSec * sourceFps));
            FpsSlider.Maximum = maxInWindow;

            var framesPerWindow = Math.Clamp(_node.ExtractFrameCount, 1, maxInWindow);
            _node.ExtractFrameCount = framesPerWindow;

            var totalWindows = Math.Floor(duration / windowSec);
            var estimatedTotal = (int)(totalWindows * framesPerWindow);
            EstimatedFrameCountText.Text = $"{estimatedTotal:N0}";
            EstFramePerSecText.Text = $"{framesPerWindow}/{windowSec}s";

            _node.ExtractFps = framesPerWindow / (double)windowSec;

            // Keep slider value/text consistent.
            _isFrameControlSync = true;
            try
            {
                if (FpsSlider.Value != framesPerWindow) FpsSlider.Value = framesPerWindow;
                FpsValueText.Text = $"{framesPerWindow}";
            }
            finally
            {
                _isFrameControlSync = false;
            }

            var extractFps = _node.ExtractFps;
            var extractFpsSafe = Math.Max(0.001, extractFps);
            EstFrameIntervalText.Text = $"{(1000.0 / extractFpsSafe):0.#} ms";

            if (extractFpsSafe >= 1)
            {
                var framesPerSec = Math.Max(1, (int)Math.Round(extractFpsSafe));
                var indices = FrameExtractionCalculator.CalculateFrameIndicesPerSecond(sourceFps, framesPerSec);
                var indicesStr = string.Join(", ", indices.Take(4).Select(i => $"#{i}"));
                if (indices.Count > 4) indicesStr += "…";
                SetTextIfExists("FrameIndexPreviewText", $"Indices/giây: [{indicesStr}] | Mục tiêu/win: {framesPerWindow:N0}");
            }
            else
            {
                var secondsPerFrame = 1.0 / extractFpsSafe;
                SetTextIfExists("FrameIndexPreviewText", $"~1 frame mỗi {secondsPerFrame:0.##}s | Mục tiêu/win: {framesPerWindow:N0}");
            }
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
