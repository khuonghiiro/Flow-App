using FlowMy.Converters;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Workflow.NodeExecutors;
using Microsoft.Win32;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl : UserControl
    {
        private const double MinAutoFitNodeWidth = 540;
        private const double MinAutoFitNodeHeight = 340;
        private const double MaxAutoFitNodeWidth = 1280;
        private const double MaxAutoFitNodeHeight = 920;
        private const double MinPreviewHeight = 180;
        private const double MaxPreviewHeight = 620;
        private const double HorizontalPadding = 18;
        private const double NonPreviewContentHeight = 230;

        private readonly VideoProcessingNode _node;
        private readonly IWorkflowEditorHost? _host;
        private readonly NotifyCollectionChangedEventHandler _audioTracksChangedHandler;
        private readonly PropertyChangedEventHandler _propertyChangedHandler;
        private readonly DispatcherTimer _timelineTimer;

        private bool _subscriptionsAttached;
        private bool _isTimelineDragging;
        private bool _isPlaying;
        private bool _isMuted;
        private double _lastVolume = 0.7;
        private int? _fixedResolutionHeight;
        private DateTime _lastRunStartedAtUtc = DateTime.UtcNow;

        public event Action<double, double>? SuggestedNodeSizeReady;
        public event Action<string>? LogLineReceived;

        public VideoProcessingNodeContentControl(VideoProcessingNode node, IWorkflowEditorHost? host = null)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host;

            InitializeComponent();
            ApplyThemeBrushes(GetTextBrush(_node.ColorKey));
            InitializeIcon();
            _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _timelineTimer.Tick += (_, _) => UpdatePlaybackUi();

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
            };
            Unloaded += (_, _) => DetachSubscriptions();
        }

        private void InitializeInteractiveControls()
        {
            TabNavList.SelectionChanged += TabNavList_SelectionChanged;
            TabNavList.SelectedIndex = 0;

            OpenVideoButton.Click += (_, _) => SelectVideo();
            RunProcessingButton.Click += (_, _) =>
            {
                TabNavList.SelectedIndex = 5;
                RunProcessingFlow();
            };

            PreviewMedia.MediaOpened += (_, _) =>
            {
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
            TimelineSlider.PreviewMouseDown += (_, _) => _isTimelineDragging = true;
            TimelineSlider.PreviewMouseUp += (_, _) =>
            {
                _isTimelineDragging = false;
                _node.TrimEndSec = TimelineSlider.Value;
                _node.TrimStartSec = Math.Max(0, TimelineSlider.Value - 10);
                SeekToSliderPosition();
            };

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

            FpsSlider.ValueChanged += (_, e) =>
            {
                _node.ExtractFps = e.NewValue;
                FpsValueText.Text = $"{e.NewValue:0.##}";
            };
            OutputBase64CheckBox.Checked += (_, _) => _node.OutputBase64 = true;
            OutputBase64CheckBox.Unchecked += (_, _) => _node.OutputBase64 = false;
            PreferGpuCheckBox.Checked += (_, _) => _node.PreferGpu = true;
            PreferGpuCheckBox.Unchecked += (_, _) => _node.PreferGpu = false;
            SourceAudioToggle.Checked += (_, _) => _node.SourceAudioEnabled = true;
            SourceAudioToggle.Unchecked += (_, _) => _node.SourceAudioEnabled = false;

            BrightnessSlider.ValueChanged += (_, e) => { _node.Brightness = e.NewValue; BrightnessLabel.Text = $"{e.NewValue:0.##}"; ApplyPreviewColorTransform(); };
            ContrastSlider.ValueChanged += (_, e) => { _node.Contrast = e.NewValue; ContrastLabel.Text = $"{e.NewValue:0.##}"; };
            SaturationSlider.ValueChanged += (_, e) => { _node.Saturation = e.NewValue; SaturationLabel.Text = $"{e.NewValue:0.##}"; };
            HueSlider.ValueChanged += (_, e) => { _node.Hue = e.NewValue; HueLabel.Text = $"{e.NewValue:0.##}"; };
            GammaSlider.ValueChanged += (_, e) => { _node.Gamma = e.NewValue; GammaLabel.Text = $"{e.NewValue:0.##}"; };

            PresetNeutralButton.Click += (_, _) => ApplyGradingPreset(0, 1, 1, 0, 1);
            PresetVividButton.Click += (_, _) => ApplyGradingPreset(0.05, 1.15, 1.35, 8, 1);
            PresetCinematicButton.Click += (_, _) => ApplyGradingPreset(-0.08, 1.22, 0.82, -12, 1);
            PresetBwButton.Click += (_, _) => ApplyGradingPreset(0, 1.1, 0, 0, 1);
            PresetWarmButton.Click += (_, _) => ApplyGradingPreset(0.03, 1.05, 1.1, 15, 1.05);
            PresetCoolButton.Click += (_, _) => ApplyGradingPreset(-0.02, 1.0, 0.95, -20, 0.98);
            PresetFadeButton.Click += (_, _) => ApplyGradingPreset(0.1, 0.85, 0.75, 0, 1.1);
            ResetGradingButton.Click += (_, _) => ApplyGradingPreset(0, 1, 1, 0, 1);

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
            TrimToggle.Checked += (_, _) => _node.TrimEnabled = true;
            TrimToggle.Unchecked += (_, _) => _node.TrimEnabled = false;
            BrowseOutputButton.Click += (_, _) => BrowseOutputPath();
            ClearLogButton.Click += (_, _) => LogTextBox.Clear();
            AddAudioTrackButton.Click += (_, _) => _node.AudioTracks.Add(new VideoAudioTrackConfig());
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
            var allTabs = new[] { GeneralTabContent, GradingTabContent, FiltersTabContent, AudioTabContent, ExportTabContent, LogTabContent };
            foreach (var tab in allTabs) tab.Visibility = Visibility.Collapsed;
            var idx = TabNavList.SelectedIndex;
            var target = idx >= 0 && idx < allTabs.Length ? allTabs[idx] : GeneralTabContent;
            target.Visibility = Visibility.Visible;
            target.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        }

        private void RunProcessingFlow()
        {
            if (_host == null) return;
            try
            {
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

        private void OnNodePropertyChanged(string propertyName)
        {
            if (propertyName == nameof(VideoProcessingNode.VideoPath)) RefreshVideoPreview();
            if (propertyName == nameof(VideoProcessingNode.SourceFps)) FpsSlider.Maximum = Math.Max(1, _node.SourceFps);
            if (propertyName == nameof(VideoProcessingNode.PreferredHwAccel)) HwBadgeText.Text = _node.PreferredHwAccel;
            RefreshInfoText();
        }

        private void AttachSubscriptions()
        {
            if (_subscriptionsAttached) return;
            _node.AudioTracks.CollectionChanged += _audioTracksChangedHandler;
            _node.PropertyChanged += _propertyChangedHandler;
            AudioTracksList.ItemsSource = _node.AudioTracks;
            VideoProcessingNodeExecutor.ProgressChanged += HandleExecutorProgress;
            VideoProcessingNodeExecutor.LogLine += HandleExecutorLog;
            _subscriptionsAttached = true;
        }

        private void DetachSubscriptions()
        {
            if (!_subscriptionsAttached) return;
            _node.AudioTracks.CollectionChanged -= _audioTracksChangedHandler;
            _node.PropertyChanged -= _propertyChangedHandler;
            VideoProcessingNodeExecutor.ProgressChanged -= HandleExecutorProgress;
            VideoProcessingNodeExecutor.LogLine -= HandleExecutorLog;
            _timelineTimer.Stop();
            PreviewMedia.Stop();
            _subscriptionsAttached = false;
        }

        private void HandleExecutorProgress(VideoProcessingNode node, double percent, string status)
        {
            if (!ReferenceEquals(node, _node)) return;
            UpdateProgress(percent, status);
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
            var dlg = new OpenFileDialog
            {
                Title = "Chon video",
                Filter = "Video Files|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == true)
            {
                _node.VideoPath = dlg.FileName;
                _node.RaisePropertyChanged(nameof(VideoProcessingNode.VideoPath));
            }
        }

        private void BrowseOutputPath()
        {
            var dlg = new SaveFileDialog { Filter = "MP4|*.mp4|WebM|*.webm|All|*.*" };
            if (dlg.ShowDialog() == true)
            {
                OutputPathText.Text = dlg.FileName;
                _node.OutputPathOverride = dlg.FileName;
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
            FpsValueText.Text = $"{_node.ExtractFps:0.##}";
            TrimStartText.Text = FormatTime(TimeSpan.FromSeconds(_node.TrimStartSec));
            TrimEndText.Text = FormatTime(TimeSpan.FromSeconds(_node.TrimEndSec));
        }

        private void SyncControlValuesFromModel()
        {
            FpsSlider.Maximum = Math.Max(1, _node.SourceFps);
            FpsSlider.Value = _node.ExtractFps;
            OutputBase64CheckBox.IsChecked = _node.OutputBase64;
            PreferGpuCheckBox.IsChecked = _node.PreferGpu;
            SourceAudioToggle.IsChecked = _node.SourceAudioEnabled;
            VolumeSlider.Value = _node.PreviewVolume;

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
            TrimToggle.IsChecked = _node.TrimEnabled;
            _fixedResolutionHeight = _node.FixedResolutionHeight;
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
                PreviewMedia.Source = new Uri(path, UriKind.RelativeOrAbsolute);
                PreviewMedia.Visibility = Visibility.Visible;
                PreviewPlaceholder.Visibility = Visibility.Collapsed;
                PreviewMedia.Volume = _node.PreviewVolume;
                PreviewMedia.Position = TimeSpan.Zero;
                PreviewMedia.Play();
                _isPlaying = true;
                LiveDot.Visibility = Visibility.Visible;
                _timelineTimer.Start();
                ApplyPreviewColorTransform();
                UpdatePlaybackUi();
            }
            catch
            {
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

        private void SeekToSliderPosition()
        {
            if (PreviewMedia.Source == null) return;
            PreviewMedia.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
            UpdatePlaybackUi();
        }

        private void UpdatePlaybackUi()
        {
            var duration = TimeSpan.FromSeconds(GetNaturalDurationSeconds());
            var position = PreviewMedia.Position;
            if (!_isTimelineDragging)
            {
                TimelineSlider.Maximum = Math.Max(1, duration.TotalSeconds <= 0 ? 1 : duration.TotalSeconds);
                TimelineSlider.Value = Math.Max(0, Math.Min(TimelineSlider.Maximum, position.TotalSeconds));
            }
            TimelinePositionText.Text = $"{FormatTime(position)} / {FormatTime(duration)}";
            PlayPauseButton.Content = new TextBlock { Text = _isPlaying ? "❚❚" : "▶", Foreground = Brushes.White };
        }

        private double GetNaturalDurationSeconds()
            => PreviewMedia.NaturalDuration.HasTimeSpan ? PreviewMedia.NaturalDuration.TimeSpan.TotalSeconds : 0;

        private void ApplyGradingPreset(double brightness, double contrast, double saturation, double hue, double gamma)
        {
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
            var strength = Math.Clamp(Math.Abs(_node.Brightness) * 0.4, 0, 0.55);
            GradingOverlay.Background = new SolidColorBrush(Color.FromArgb((byte)(strength * 255), 124, 107, 248));
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

        private static string FormatTime(TimeSpan value)
            => value.TotalHours >= 1 ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" : $"{value.Minutes:00}:{value.Seconds:00}";

        private static Brush GetTextBrush(string? colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey)) return new SolidColorBrush(Color.FromRgb(229, 231, 235));
            return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush ?? new SolidColorBrush(Color.FromRgb(229, 231, 235));
        }
    }
}
