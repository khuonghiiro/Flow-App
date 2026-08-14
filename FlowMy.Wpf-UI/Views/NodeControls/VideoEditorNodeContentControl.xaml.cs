using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoEditorNodeContentControl : UserControl
    {
        private readonly VideoEditorNode _node;
        private readonly IWorkflowEditorHost _host;
        private readonly Border? _chromeBorder;
        private readonly Window? _ownerWindow;
        private readonly Func<bool>? _isResizing;

        private readonly DispatcherTimer _positionTimer;
        private bool _isSeeking;
        private bool _wasPlayingBeforeSeek;
        private bool _isPlaying;
        private bool _isInitializing = true;
        private TimeSpan _videoDuration = TimeSpan.Zero;

        public VideoEditorNodeContentControl(
            VideoEditorNode node,
            IWorkflowEditorHost host,
            Border? chromeBorder = null,
            Window? ownerWindow = null,
            Func<bool>? isResizing = null)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _chromeBorder = chromeBorder;
            _ownerWindow = ownerWindow;
            _isResizing = isResizing;

            InitializeComponent();
            _isInitializing = false;

            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            _positionTimer.Tick += PositionTimer_Tick;

            Loaded += VideoEditorNodeContentControl_Loaded;
            Unloaded += VideoEditorNodeContentControl_Unloaded;

            SyncNodeToUi();
            _node.PropertyChanged += Node_PropertyChanged;
        }

        private void VideoEditorNodeContentControl_Loaded(object sender, RoutedEventArgs e)
        {
            SyncNodeToUi();
            if (!string.IsNullOrWhiteSpace(_node.InputVideoUrl) && File.Exists(_node.InputVideoUrl))
            {
                LoadVideoFile(_node.InputVideoUrl);
            }
        }

        private void VideoEditorNodeContentControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _positionTimer.Stop();
            try { PreviewMediaElement?.Stop(); } catch { }
            _node.PropertyChanged -= Node_PropertyChanged;
        }

        private void Node_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.InvokeAsync(SyncNodeToUi);
        }

        private void SyncNodeToUi()
        {
            if (_isInitializing || HeaderTitleText == null) return;

            HeaderTitleText.Text = string.IsNullOrWhiteSpace(_node.Title) ? "Chỉnh sửa Video" : _node.Title;

            // Display Mode
            if (_node.DisplayMode == VideoEditorDisplayMode.InteractiveEditor)
            {
                InteractiveEditorPanel.Visibility = Visibility.Visible;
                AutomatedPipelinePanel.Visibility = Visibility.Collapsed;
                ModeEditorBtn.Opacity = 1.0;
                ModePipelineBtn.Opacity = 0.6;
            }
            else
            {
                InteractiveEditorPanel.Visibility = Visibility.Collapsed;
                AutomatedPipelinePanel.Visibility = Visibility.Visible;
                ModeEditorBtn.Opacity = 0.6;
                ModePipelineBtn.Opacity = 1.0;
            }

            // Trim
            if (TrimEnableCheckBox != null) TrimEnableCheckBox.IsChecked = _node.TrimEnabled;
            if (TrimStartTextBox != null) TrimStartTextBox.Text = _node.TrimStartTime;
            if (TrimEndTextBox != null) TrimEndTextBox.Text = _node.TrimEndTime;

            // Color Sliders
            if (BrightnessSlider != null) BrightnessSlider.Value = _node.Brightness;
            if (BrightnessValText != null) BrightnessValText.Text = _node.Brightness.ToString("0.0");
            if (ContrastSlider != null) ContrastSlider.Value = _node.Contrast;
            if (ContrastValText != null) ContrastValText.Text = _node.Contrast.ToString("0.0");
            if (SaturationSlider != null) SaturationSlider.Value = _node.Saturation;
            if (SaturationValText != null) SaturationValText.Text = _node.Saturation.ToString("0.0");
            if (GammaSlider != null) GammaSlider.Value = _node.Gamma;
            if (GammaValText != null) GammaValText.Text = _node.Gamma.ToString("0.0");
            if (HueSlider != null) HueSlider.Value = _node.Hue;
            if (HueValText != null) HueValText.Text = $"{_node.Hue:0}°";

            UpdateRealtimeColorPreviewOverlay();

            // Watermark
            if (WatermarkEnableCheckBox != null) WatermarkEnableCheckBox.IsChecked = _node.WatermarkEnabled;
            if (WatermarkTextBox != null) WatermarkTextBox.Text = _node.WatermarkText;
            if (WatermarkPreviewBorder != null)
                WatermarkPreviewBorder.Visibility = _node.WatermarkEnabled && !string.IsNullOrWhiteSpace(_node.WatermarkText)
                    ? Visibility.Visible : Visibility.Collapsed;
            if (WatermarkPreviewText != null) WatermarkPreviewText.Text = _node.WatermarkText;

            // Scale & Speed
            if (ScaleEnableCheckBox != null) ScaleEnableCheckBox.IsChecked = _node.ScaleEnabled;
            if (TargetWidthTextBox != null) TargetWidthTextBox.Text = _node.TargetWidth.ToString();
            if (TargetHeightTextBox != null) TargetHeightTextBox.Text = _node.TargetHeight.ToString();
            if (SpeedBadgeText != null) SpeedBadgeText.Text = $" ({_node.Speed:0.##}x)";

            // Metadata & Pipeline Info
            if (!string.IsNullOrWhiteSpace(_node.VideoMetadataInfo) && HeaderMetadataBadge != null)
            {
                HeaderMetadataBadge.Text = _node.VideoMetadataInfo;
            }

            if (PipelineSourceInfoText != null)
                PipelineSourceInfoText.Text = $"Source Node: {(string.IsNullOrWhiteSpace(_node.SourceNodeId) ? "[Tự chọn / combobox]" : _node.SourceNodeId)} | Output Key: {(string.IsNullOrWhiteSpace(_node.SourceOutputKey) ? "[mặc định]" : _node.SourceOutputKey)}\nVideo path: {(string.IsNullOrWhiteSpace(_node.InputVideoUrl) ? "(chưa nạp)" : _node.InputVideoUrl)}";

            if (RuleTrimText != null) RuleTrimText.Text = _node.TrimEnabled ? $"✂️ Cắt: {_node.TrimStartTime} ➔ {_node.TrimEndTime}" : "✂️ Cắt: Tắt";
            if (RuleColorText != null) RuleColorText.Text = $"🎨 Màu: Br={_node.Brightness:0.0}, Co={_node.Contrast:0.0}, Sat={_node.Saturation:0.0}, Gam={_node.Gamma:0.0}, Hue={_node.Hue:0}°";
            if (RuleWatermarkText != null) RuleWatermarkText.Text = _node.WatermarkEnabled ? $"💧 Watermark: \"{_node.WatermarkText}\" ({_node.WatermarkPosition})" : "💧 Watermark: Tắt";
            if (RuleSpeedText != null) RuleSpeedText.Text = $"⚡ Tốc độ: {_node.Speed:0.##}x | Scale: {(_node.ScaleEnabled ? $"{_node.TargetWidth}x{_node.TargetHeight}" : "Gốc")}";
            if (RuleExportText != null) RuleExportText.Text = $"📦 Export: {_node.ExportMode} ({_node.ExportFormat.ToUpper()}) - {_node.ExportFps} FPS";

            UpdateFfmpegCmdPreview();
        }

        // --- MEDIA PLAYBACK LOGIC ---
        public void LoadVideoFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || PreviewMediaElement == null) return;
            try
            {
                _node.InputVideoUrl = filePath;
                PreviewMediaElement.Source = new Uri(filePath, UriKind.Absolute);
                if (NoVideoPlaceholder != null) NoVideoPlaceholder.Visibility = Visibility.Collapsed;
                PreviewMediaElement.SpeedRatio = _node.Speed > 0 ? _node.Speed : 1.0;
                PreviewMediaElement.Play();
                _isPlaying = true;
                if (PlayPauseIconText != null) PlayPauseIconText.Text = "⏸";
                _positionTimer.Start();

                // Probe Metadata in Background
                _ = ProbeVideoMetadataAsync(filePath);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi mở video: {ex.Message}");
            }
        }

        private async Task ProbeVideoMetadataAsync(string filePath)
        {
            try
            {
                string ffmpegExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg");
                if (string.IsNullOrWhiteSpace(ffmpegExe)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = $"-hide_banner -i \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };

                using var p = Process.Start(psi);
                if (p == null) return;
                string output = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();

                int width = 0, height = 0;
                double fps = 30.0;
                string codec = "H.264";

                // Regex match resolution e.g. 1920x1080
                var resMatch = Regex.Match(output, @"(\d{3,5})x(\d{3,5})");
                if (resMatch.Success)
                {
                    int.TryParse(resMatch.Groups[1].Value, out width);
                    int.TryParse(resMatch.Groups[2].Value, out height);
                }

                // Regex match FPS e.g. 29.97 fps or 30 fps
                var fpsMatch = Regex.Match(output, @"(\d+(\.\d+)?)\s+fps");
                if (fpsMatch.Success)
                {
                    double.TryParse(fpsMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out fps);
                }

                // Regex match Video Codec e.g. Video: h264 or hevc
                var codecMatch = Regex.Match(output, @"Video:\s*([a-zA-Z0-9_-]+)");
                if (codecMatch.Success)
                {
                    codec = codecMatch.Groups[1].Value.ToUpper();
                }

                string durationStr = _videoDuration.TotalSeconds > 0 ? FormatTimeSpan(_videoDuration) : "";
                string metaInfo = $"{width}x{height} | {fps:0.#} FPS | {codec}{(string.IsNullOrEmpty(durationStr) ? "" : $" | {durationStr}")}";

                Dispatcher.Invoke(() =>
                {
                    _node.VideoMetadataInfo = metaInfo;
                    if (HeaderMetadataBadge != null) HeaderMetadataBadge.Text = metaInfo;
                });
            }
            catch { }
        }

        private void PreviewMediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement != null && PreviewMediaElement.NaturalDuration.HasTimeSpan)
            {
                _videoDuration = PreviewMediaElement.NaturalDuration.TimeSpan;
                if (SeekSlider != null) SeekSlider.Maximum = _videoDuration.TotalSeconds;
                UpdateTimecodeText();

                if (string.IsNullOrWhiteSpace(_node.TrimEndTime) || _node.TrimEndTime == "00:00:10.000")
                {
                    _node.TrimEndTime = FormatTimeSpan(_videoDuration);
                    if (TrimEndTextBox != null) TrimEndTextBox.Text = _node.TrimEndTime;
                }
            }
        }

        private void PreviewMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement != null) PreviewMediaElement.Stop();
            _isPlaying = false;
            if (PlayPauseIconText != null) PlayPauseIconText.Text = "▶";
            _positionTimer.Stop();
            if (SeekSlider != null) SeekSlider.Value = 0;
            UpdateTimecodeText();
        }

        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isSeeking && PreviewMediaElement != null && PreviewMediaElement.NaturalDuration.HasTimeSpan)
            {
                if (SeekSlider != null) SeekSlider.Value = PreviewMediaElement.Position.TotalSeconds;
                UpdateTimecodeText();
            }
        }

        private void UpdateTimecodeText()
        {
            if (PreviewMediaElement == null) return;
            var cur = _isSeeking && SeekSlider != null ? TimeSpan.FromSeconds(SeekSlider.Value) : PreviewMediaElement.Position;
            string curStr = FormatTimeSpan(cur);
            string durStr = FormatTimeSpan(_videoDuration);

            if (TimecodeText != null) TimecodeText.Text = $"{curStr} / {durStr}";
            if (ViewportInfoText != null) ViewportInfoText.Text = $"{curStr}{(_isSeeking ? " (Tua...)" : "")}";
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement == null || PreviewMediaElement.Source == null) return;
            if (_isPlaying)
            {
                PreviewMediaElement.Pause();
                _isPlaying = false;
                if (PlayPauseIconText != null) PlayPauseIconText.Text = "▶";
                _positionTimer.Stop();
            }
            else
            {
                PreviewMediaElement.SpeedRatio = _node.Speed > 0 ? _node.Speed : 1.0;
                PreviewMediaElement.Play();
                _isPlaying = true;
                if (PlayPauseIconText != null) PlayPauseIconText.Text = "⏸";
                _positionTimer.Start();
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement == null || PreviewMediaElement.Source == null) return;
            PreviewMediaElement.Stop();
            _isPlaying = false;
            if (PlayPauseIconText != null) PlayPauseIconText.Text = "▶";
            _positionTimer.Stop();
            if (SeekSlider != null) SeekSlider.Value = 0;
            UpdateTimecodeText();
        }

        // --- SMOOTH SEEKING LOGIC (NO LAG / STUTTER) ---
        private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isSeeking = true;
            _wasPlayingBeforeSeek = _isPlaying;
            if (_isPlaying && PreviewMediaElement != null)
            {
                PreviewMediaElement.Pause();
            }
        }

        private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSeeking)
            {
                _isSeeking = false;
                if (PreviewMediaElement != null && SeekSlider != null)
                {
                    PreviewMediaElement.Position = TimeSpan.FromSeconds(SeekSlider.Value);
                    UpdateTimecodeText();
                    if (_wasPlayingBeforeSeek)
                    {
                        PreviewMediaElement.Play();
                        _isPlaying = true;
                        if (PlayPauseIconText != null) PlayPauseIconText.Text = "⏸";
                        _positionTimer.Start();
                    }
                }
            }
        }

        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTimecodeText();
            if (!_isSeeking && PreviewMediaElement != null && Math.Abs(PreviewMediaElement.Position.TotalSeconds - e.NewValue) > 0.5)
            {
                // Single click on track
                PreviewMediaElement.Position = TimeSpan.FromSeconds(e.NewValue);
            }
        }

        // --- FAST FORWARD / REWIND & STEP FRAME CONTROLS ---
        private void Rewind5sBtn_Click(object sender, RoutedEventArgs e)
        {
            SeekRelative(-5.0);
        }

        private void Forward5sBtn_Click(object sender, RoutedEventArgs e)
        {
            SeekRelative(5.0);
        }

        private void StepBackFrameBtn_Click(object sender, RoutedEventArgs e)
        {
            SeekRelative(-0.04); // ~1 frame at 25fps
        }

        private void StepForwardFrameBtn_Click(object sender, RoutedEventArgs e)
        {
            SeekRelative(0.04);
        }

        private void SeekRelative(double secondsDelta)
        {
            if (PreviewMediaElement == null || _videoDuration == TimeSpan.Zero) return;
            double currentSec = PreviewMediaElement.Position.TotalSeconds;
            double targetSec = Math.Clamp(currentSec + secondsDelta, 0, _videoDuration.TotalSeconds);
            PreviewMediaElement.Position = TimeSpan.FromSeconds(targetSec);
            if (SeekSlider != null) SeekSlider.Value = targetSec;
            UpdateTimecodeText();
        }

        // --- SPEED SELECTOR ---
        private void SpeedSelectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || SpeedSelectCombo == null) return;
            if (SpeedSelectCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && double.TryParse(tag, CultureInfo.InvariantCulture, out var spd))
            {
                _node.Speed = spd;
                if (PreviewMediaElement != null) PreviewMediaElement.SpeedRatio = spd;
                if (SpeedBadgeText != null) SpeedBadgeText.Text = $" ({spd:0.##}x)";
                SyncNodeToUi();
            }
        }

        private void SpeedBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string strSpeed && double.TryParse(strSpeed, CultureInfo.InvariantCulture, out var spd))
            {
                _node.Speed = spd;
                if (PreviewMediaElement != null) PreviewMediaElement.SpeedRatio = spd;
                if (SpeedBadgeText != null) SpeedBadgeText.Text = $" ({spd:0.##}x)";
                SyncNodeToUi();
            }
        }

        private void MuteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement == null) return;
            PreviewMediaElement.IsMuted = !PreviewMediaElement.IsMuted;
            if (MuteIconText != null) MuteIconText.Text = PreviewMediaElement.IsMuted ? "🔇" : "🔊";
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || PreviewMediaElement == null) return;
            PreviewMediaElement.Volume = e.NewValue;
        }

        // --- REAL-TIME COLOR PREVIEW OVERLAY ---
        private void UpdateRealtimeColorPreviewOverlay()
        {
            if (ColorFilterPreviewOverlay == null) return;

            // Apply WPF Visual Color Overlay representing Brightness, Sepia, Grayscale, Invert & Tint
            double br = _node.Brightness;
            string preset = _node.FilterPreset ?? "None";

            if (preset.Equals("Grayscale", StringComparison.OrdinalIgnoreCase))
            {
                ColorFilterPreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80));
            }
            else if (preset.Equals("Sepia", StringComparison.OrdinalIgnoreCase))
            {
                ColorFilterPreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(0x50, 0x70, 0x42, 0x14));
            }
            else if (preset.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                ColorFilterPreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            }
            else if (preset.Equals("Warm", StringComparison.OrdinalIgnoreCase))
            {
                ColorFilterPreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0x8C, 0x00));
            }
            else if (preset.Equals("Cool", StringComparison.OrdinalIgnoreCase))
            {
                ColorFilterPreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(0x35, 0x00, 0xB4, 0xD8));
            }
            else if (preset.Equals("Vivid", StringComparison.OrdinalIgnoreCase))
            {
                ColorFilterPreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0x00, 0x7F));
            }
            else if (br > 0.05)
            {
                byte alpha = (byte)Math.Min(255, (int)(br * 100));
                ColorFilterPreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF));
            }
            else if (br < -0.05)
            {
                byte alpha = (byte)Math.Min(255, (int)(-br * 100));
                ColorFilterPreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(alpha, 0x00, 0x00, 0x00));
            }
            else
            {
                ColorFilterPreviewOverlay.Background = Brushes.Transparent;
            }
        }

        private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (BrightnessSlider == null || ContrastSlider == null || SaturationSlider == null || GammaSlider == null || HueSlider == null) return;

            _node.Brightness = BrightnessSlider.Value;
            _node.Contrast = ContrastSlider.Value;
            _node.Saturation = SaturationSlider.Value;
            _node.Gamma = GammaSlider.Value;
            _node.Hue = HueSlider.Value;

            if (BrightnessValText != null) BrightnessValText.Text = _node.Brightness.ToString("0.0");
            if (ContrastValText != null) ContrastValText.Text = _node.Contrast.ToString("0.0");
            if (SaturationValText != null) SaturationValText.Text = _node.Saturation.ToString("0.0");
            if (GammaValText != null) GammaValText.Text = _node.Gamma.ToString("0.0");
            if (HueValText != null) HueValText.Text = $"{_node.Hue:0}°";

            UpdateRealtimeColorPreviewOverlay();
            UpdateFfmpegCmdPreview();
        }

        private void PresetNormal_Click(object sender, RoutedEventArgs e)
        {
            ResetColor_Click(sender, e);
            _node.FilterPreset = "None";
        }

        private void PresetVivid_Click(object sender, RoutedEventArgs e)
        {
            _node.Brightness = 0.05;
            _node.Contrast = 1.25;
            _node.Saturation = 1.5;
            _node.Gamma = 1.0;
            _node.Hue = 0.0;
            _node.FilterPreset = "Vivid";
            SyncNodeToUi();
        }

        private void PresetWarm_Click(object sender, RoutedEventArgs e)
        {
            _node.Brightness = 0.0;
            _node.Contrast = 1.1;
            _node.Saturation = 1.2;
            _node.Gamma = 1.0;
            _node.Hue = 15.0;
            _node.FilterPreset = "Warm";
            SyncNodeToUi();
        }

        private void PresetCool_Click(object sender, RoutedEventArgs e)
        {
            _node.Brightness = 0.0;
            _node.Contrast = 1.1;
            _node.Saturation = 1.1;
            _node.Gamma = 1.0;
            _node.Hue = -20.0;
            _node.FilterPreset = "Cool";
            SyncNodeToUi();
        }

        private void PresetGrayscale_Click(object sender, RoutedEventArgs e)
        {
            _node.Saturation = 0.0;
            _node.FilterPreset = "Grayscale";
            SyncNodeToUi();
        }

        private void PresetSepia_Click(object sender, RoutedEventArgs e)
        {
            _node.FilterPreset = "Sepia";
            SyncNodeToUi();
        }

        private void PresetInvert_Click(object sender, RoutedEventArgs e)
        {
            _node.FilterPreset = "Invert";
            SyncNodeToUi();
        }

        private void ResetColor_Click(object sender, RoutedEventArgs e)
        {
            _node.Brightness = 0.0;
            _node.Contrast = 1.0;
            _node.Saturation = 1.0;
            _node.Gamma = 1.0;
            _node.Hue = 0.0;
            _node.FilterPreset = "None";
            SyncNodeToUi();
        }

        // --- QUICK FFMEG ACTIONS (FRAME, GIF, AUDIO) ---
        private void QuickExtractFrameBtn_Click(object sender, RoutedEventArgs e)
        {
            ExtractCurrentFrame_Click(sender, e);
        }

        private async void ExtractCurrentFrame_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_node.InputVideoUrl) || !File.Exists(_node.InputVideoUrl))
            {
                MessageBox.Show("Vui lòng mở một file Video trước!", "Trích xuất Frame", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string ffmpegExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg");
                if (string.IsNullOrWhiteSpace(ffmpegExe))
                {
                    MessageBox.Show("Không tìm thấy ffmpeg.exe trong hệ thống!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                TimeSpan currentTime = PreviewMediaElement != null ? PreviewMediaElement.Position : TimeSpan.Zero;
                string timeStr = FormatTimeSpan(currentTime);

                string outputDir = !string.IsNullOrWhiteSpace(_node.OutputFolderPath) && Directory.Exists(_node.OutputFolderPath)
                    ? _node.OutputFolderPath
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output", "Frames");
                Directory.CreateDirectory(outputDir);

                string framePath = Path.Combine(outputDir, $"frame_{DateTime.Now:yyyyMMdd_HHmmss}_{currentTime.TotalSeconds:F0}s.png");

                string args = $"-y -ss {timeStr} -i \"{_node.InputVideoUrl}\" -vframes 1 \"{framePath}\"";

                AppendLog($"📸 Đang trích xuất frame tại thời điểm {timeStr}...");

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };

                using var p = Process.Start(psi);
                if (p != null) await p.WaitForExitAsync();

                if (File.Exists(framePath))
                {
                    AppendLog($"✅ Đã trích xuất Frame thành công: {framePath}");
                    Process.Start(new ProcessStartInfo { FileName = framePath, UseShellExecute = true });
                }
                else
                {
                    AppendLog($"❌ Không thể tạo file Frame.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi trích xuất Frame: {ex.Message}");
            }
        }

        private async void ExtractFrameSequence_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_node.InputVideoUrl) || !File.Exists(_node.InputVideoUrl))
            {
                MessageBox.Show("Vui lòng mở một file Video trước!", "Trích xuất chuỗi Frame", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string ffmpegExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg");
                if (string.IsNullOrWhiteSpace(ffmpegExe)) return;

                double fps = 1.0;
                if (ExtractFpsTextBox != null && double.TryParse(ExtractFpsTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedFps))
                {
                    fps = Math.Max(0.1, parsedFps);
                }

                string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output", "FrameSequence", $"{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(outputDir);

                string framePattern = Path.Combine(outputDir, "frame_%04d.jpg");
                string args = $"-y -i \"{_node.InputVideoUrl}\" -r {fps:0.##} \"{framePattern}\"";

                AppendLog($"🎞️ Đang trích xuất chuỗi frame tại FPS {fps}...");

                var psi = new ProcessStartInfo { FileName = ffmpegExe, Arguments = args, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p != null) await p.WaitForExitAsync();

                AppendLog($"✅ Hoàn thành trích xuất chuỗi frame tại thư mục: {outputDir}");
                Process.Start(new ProcessStartInfo { FileName = outputDir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi trích xuất chuỗi Frame: {ex.Message}");
            }
        }

        private async void ExportGif_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_node.InputVideoUrl) || !File.Exists(_node.InputVideoUrl))
            {
                MessageBox.Show("Vui lòng mở một file Video trước!", "Xuất GIF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string ffmpegExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg");
                if (string.IsNullOrWhiteSpace(ffmpegExe)) return;

                string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output", "GIF");
                Directory.CreateDirectory(outputDir);

                string gifPath = Path.Combine(outputDir, $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.gif");

                string ssArg = _node.TrimEnabled && !string.IsNullOrWhiteSpace(_node.TrimStartTime) ? $"-ss {_node.TrimStartTime} " : "";
                string toArg = _node.TrimEnabled && !string.IsNullOrWhiteSpace(_node.TrimEndTime) ? $"-to {_node.TrimEndTime} " : "";

                string args = $"-y {ssArg}{toArg}-i \"{_node.InputVideoUrl}\" -vf \"fps=15,scale=480:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" \"{gifPath}\"";

                AppendLog($"🎬 Đang tạo ảnh động GIF...");

                var psi = new ProcessStartInfo { FileName = ffmpegExe, Arguments = args, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p != null) await p.WaitForExitAsync();

                if (File.Exists(gifPath))
                {
                    AppendLog($"✅ Xuất GIF thành công: {gifPath}");
                    Process.Start(new ProcessStartInfo { FileName = gifPath, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi tạo GIF: {ex.Message}");
            }
        }

        private async void ExtractAudioOnly_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_node.InputVideoUrl) || !File.Exists(_node.InputVideoUrl))
            {
                MessageBox.Show("Vui lòng mở một file Video trước!", "Tách Audio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string ffmpegExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg");
                if (string.IsNullOrWhiteSpace(ffmpegExe)) return;

                string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output", "Audio");
                Directory.CreateDirectory(outputDir);

                string audioPath = Path.Combine(outputDir, $"audio_{DateTime.Now:yyyyMMdd_HHmmss}.mp3");
                string ssArg = _node.TrimEnabled && !string.IsNullOrWhiteSpace(_node.TrimStartTime) ? $"-ss {_node.TrimStartTime} " : "";
                string toArg = _node.TrimEnabled && !string.IsNullOrWhiteSpace(_node.TrimEndTime) ? $"-to {_node.TrimEndTime} " : "";

                string args = $"-y {ssArg}{toArg}-i \"{_node.InputVideoUrl}\" -vn -c:a mp3 -b:a 192k \"{audioPath}\"";

                AppendLog($"🎵 Đang trích xuất file âm thanh MP3...");

                var psi = new ProcessStartInfo { FileName = ffmpegExe, Arguments = args, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p != null) await p.WaitForExitAsync();

                if (File.Exists(audioPath))
                {
                    AppendLog($"✅ Tách Audio thành công: {audioPath}");
                    Process.Start(new ProcessStartInfo { FileName = audioPath, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi tách Audio: {ex.Message}");
            }
        }

        // --- ASPECT RATIO PRESETS ---
        private void ScalePresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ScalePresetCombo == null) return;
            if (ScalePresetCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (tag == "Custom") return;
                var parts = tag.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                {
                    _node.TargetWidth = w;
                    _node.TargetHeight = h;
                    _node.ScaleEnabled = true;
                    if (TargetWidthTextBox != null) TargetWidthTextBox.Text = w.ToString();
                    if (TargetHeightTextBox != null) TargetHeightTextBox.Text = h.ToString();
                    if (ScaleEnableCheckBox != null) ScaleEnableCheckBox.IsChecked = true;
                }
            }
        }

        // --- MODE SWITCHER & BUTTON HANDLERS ---
        private void ModeEditorBtn_Click(object sender, RoutedEventArgs e)
        {
            _node.DisplayMode = VideoEditorDisplayMode.InteractiveEditor;
            SyncNodeToUi();
        }

        private void ModePipelineBtn_Click(object sender, RoutedEventArgs e)
        {
            _node.DisplayMode = VideoEditorDisplayMode.AutomatedPipeline;
            SyncNodeToUi();
        }

        private void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn File Video",
                Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.webm;*.flv;*.wmv|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadVideoFile(dialog.FileName);
                SyncNodeToUi();
            }
        }

        private void RunNodeBtn_Click(object sender, RoutedEventArgs e)
        {
            AppendLog($"🚀 Kích hoạt chạy FFmpeg cho Node {_node.Title}...");
            if (ExecutionStatusText != null) ExecutionStatusText.Text = "Trạng thái: Đang xử lý FFmpeg...";
            _host.RequestRunSingleNode(_node);
        }

        private void OpenDialogBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = _ownerWindow ?? Application.Current?.MainWindow;
            var dlg = new VideoEditorNodeDialog(_node, _host, window);
            dlg.Owner = window;
            dlg.ShowDialog();
            SyncNodeToUi();
        }

        private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = !string.IsNullOrWhiteSpace(_node.OutputFolderPath) && Directory.Exists(_node.OutputFolderPath)
                    ? _node.OutputFolderPath
                    : AppDomain.CurrentDomain.BaseDirectory;
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Không thể mở thư mục: {ex.Message}");
            }
        }

        // --- TRIM CONTROLS ---
        private void TrimEnableCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || TrimEnableCheckBox == null) return;
            _node.TrimEnabled = TrimEnableCheckBox.IsChecked == true;
            UpdateFfmpegCmdPreview();
        }

        private void TrimStartTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || TrimStartTextBox == null) return;
            if (TrimStartTextBox.IsFocused) _node.TrimStartTime = TrimStartTextBox.Text;
            UpdateFfmpegCmdPreview();
        }

        private void TrimEndTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || TrimEndTextBox == null) return;
            if (TrimEndTextBox.IsFocused) _node.TrimEndTime = TrimEndTextBox.Text;
            UpdateFfmpegCmdPreview();
        }

        private void SetTrimStartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement == null || TrimStartTextBox == null) return;
            var cur = PreviewMediaElement.Position;
            _node.TrimStartTime = FormatTimeSpan(cur);
            TrimStartTextBox.Text = _node.TrimStartTime;
            UpdateFfmpegCmdPreview();
        }

        private void SetTrimEndBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement == null || TrimEndTextBox == null) return;
            var cur = PreviewMediaElement.Position;
            _node.TrimEndTime = FormatTimeSpan(cur);
            TrimEndTextBox.Text = _node.TrimEndTime;
            UpdateFfmpegCmdPreview();
        }

        private void TrimPreset10s_Click(object sender, RoutedEventArgs e)
        {
            _node.TrimStartTime = "00:00:00.000";
            _node.TrimEndTime = "00:00:10.000";
            _node.TrimEnabled = true;
            SyncNodeToUi();
        }

        private void TrimPreset30s_Click(object sender, RoutedEventArgs e)
        {
            _node.TrimStartTime = "00:00:00.000";
            _node.TrimEndTime = "00:00:30.000";
            _node.TrimEnabled = true;
            SyncNodeToUi();
        }

        private void TrimReset_Click(object sender, RoutedEventArgs e)
        {
            _node.TrimStartTime = "00:00:00.000";
            _node.TrimEndTime = FormatTimeSpan(_videoDuration);
            _node.TrimEnabled = false;
            SyncNodeToUi();
        }

        // --- WATERMARK ---
        private void WatermarkCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || WatermarkEnableCheckBox == null) return;
            _node.WatermarkEnabled = WatermarkEnableCheckBox.IsChecked == true;
            SyncNodeToUi();
        }

        private void WatermarkTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || WatermarkTextBox == null) return;
            _node.WatermarkText = WatermarkTextBox.Text;
            if (WatermarkPreviewText != null) WatermarkPreviewText.Text = WatermarkTextBox.Text;
            if (WatermarkPreviewBorder != null)
                WatermarkPreviewBorder.Visibility = _node.WatermarkEnabled && !string.IsNullOrWhiteSpace(_node.WatermarkText)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void WatermarkPosCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || WatermarkPosCombo == null || WatermarkPreviewBorder == null) return;
            if (WatermarkPosCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _node.WatermarkPosition = tag;
                switch (tag)
                {
                    case "TopLeft":
                        WatermarkPreviewBorder.HorizontalAlignment = HorizontalAlignment.Left;
                        WatermarkPreviewBorder.VerticalAlignment = VerticalAlignment.Top;
                        break;
                    case "TopRight":
                        WatermarkPreviewBorder.HorizontalAlignment = HorizontalAlignment.Right;
                        WatermarkPreviewBorder.VerticalAlignment = VerticalAlignment.Top;
                        break;
                    case "BottomLeft":
                        WatermarkPreviewBorder.HorizontalAlignment = HorizontalAlignment.Left;
                        WatermarkPreviewBorder.VerticalAlignment = VerticalAlignment.Bottom;
                        break;
                    case "Center":
                        WatermarkPreviewBorder.HorizontalAlignment = HorizontalAlignment.Center;
                        WatermarkPreviewBorder.VerticalAlignment = VerticalAlignment.Center;
                        break;
                    default:
                        WatermarkPreviewBorder.HorizontalAlignment = HorizontalAlignment.Right;
                        WatermarkPreviewBorder.VerticalAlignment = VerticalAlignment.Bottom;
                        break;
                }
            }
        }

        // --- SCALE & ROTATE ---
        private void ScaleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || ScaleEnableCheckBox == null) return;
            _node.ScaleEnabled = ScaleEnableCheckBox.IsChecked == true;
            UpdateFfmpegCmdPreview();
        }

        private void ScaleSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || TargetWidthTextBox == null || TargetHeightTextBox == null) return;
            if (int.TryParse(TargetWidthTextBox.Text, out var w)) _node.TargetWidth = w;
            if (int.TryParse(TargetHeightTextBox.Text, out var h)) _node.TargetHeight = h;
            UpdateFfmpegCmdPreview();
        }

        private void RotateFlipCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || RotateFlipCombo == null) return;
            if (RotateFlipCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _node.RotateFlip = tag;
                UpdateFfmpegCmdPreview();
            }
        }

        // --- FFMPEG COMMAND PREVIEW ---
        private void UpdateFfmpegCmdPreview()
        {
            if (FfmpegCmdPreviewBox == null) return;
            string input = string.IsNullOrWhiteSpace(_node.InputVideoUrl) ? "input.mp4" : Path.GetFileName(_node.InputVideoUrl);
            string ss = _node.TrimEnabled && !string.IsNullOrWhiteSpace(_node.TrimStartTime) ? $"-ss {_node.TrimStartTime} " : "";
            string to = _node.TrimEnabled && !string.IsNullOrWhiteSpace(_node.TrimEndTime) ? $"-to {_node.TrimEndTime} " : "";
            string eq = $"eq=brightness={_node.Brightness:F2}:contrast={_node.Contrast:F2}:saturation={_node.Saturation:F2}:gamma={_node.Gamma:F2}";
            if (Math.Abs(_node.Hue) > 0.1) eq += $",hue=h={_node.Hue:F0}";
            if (_node.ScaleEnabled) eq += $",scale={_node.TargetWidth}:{_node.TargetHeight}";
            if (Math.Abs(_node.Speed - 1.0) > 0.01) eq += $",setpts={1.0 / Math.Max(_node.Speed, 0.1):F2}*PTS";

            FfmpegCmdPreviewBox.Text = $"ffmpeg -y {ss}-i \"{input}\" {to}-vf \"{eq}\" output.mp4";
        }

        // --- LOG LOGIC ---
        public void AppendLog(string message)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (LogTextBox == null) return;
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void ClearLogBtn_Click(object sender, RoutedEventArgs e)
        {
            if (LogTextBox != null) LogTextBox.Text = string.Empty;
        }
    }
}
