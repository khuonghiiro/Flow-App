using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
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
                Interval = TimeSpan.FromMilliseconds(100)
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

            // Color
            if (BrightnessSlider != null) BrightnessSlider.Value = _node.Brightness;
            if (BrightnessValText != null) BrightnessValText.Text = _node.Brightness.ToString("0.0");
            if (ContrastSlider != null) ContrastSlider.Value = _node.Contrast;
            if (ContrastValText != null) ContrastValText.Text = _node.Contrast.ToString("0.0");
            if (SaturationSlider != null) SaturationSlider.Value = _node.Saturation;
            if (SaturationValText != null) SaturationValText.Text = _node.Saturation.ToString("0.0");
            if (GammaSlider != null) GammaSlider.Value = _node.Gamma;
            if (GammaValText != null) GammaValText.Text = _node.Gamma.ToString("0.0");

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

            // Pipeline text
            if (PipelineSourceInfoText != null)
                PipelineSourceInfoText.Text = $"Source Node: {(string.IsNullOrWhiteSpace(_node.SourceNodeId) ? "[Tự chọn / combobox]" : _node.SourceNodeId)} | Output Key: {(string.IsNullOrWhiteSpace(_node.SourceOutputKey) ? "[mặc định]" : _node.SourceOutputKey)}\nVideo path: {(string.IsNullOrWhiteSpace(_node.InputVideoUrl) ? "(chưa nạp)" : _node.InputVideoUrl)}";

            if (RuleTrimText != null) RuleTrimText.Text = _node.TrimEnabled ? $"✂️ Cắt: {_node.TrimStartTime} ➔ {_node.TrimEndTime}" : "✂️ Cắt: Tắt";
            if (RuleColorText != null) RuleColorText.Text = $"🎨 Màu: Br={_node.Brightness:0.0}, Co={_node.Contrast:0.0}, Sat={_node.Saturation:0.0}, Gam={_node.Gamma:0.0}";
            if (RuleWatermarkText != null) RuleWatermarkText.Text = _node.WatermarkEnabled ? $"💧 Watermark: \"{_node.WatermarkText}\" ({_node.WatermarkPosition})" : "💧 Watermark: Tắt";
            if (RuleSpeedText != null) RuleSpeedText.Text = $"⚡ Tốc độ: {_node.Speed:0.0}x | Scale: {(_node.ScaleEnabled ? $"{_node.TargetWidth}x{_node.TargetHeight}" : "Gốc")}";
            if (RuleExportText != null) RuleExportText.Text = $"📦 Export: {_node.ExportMode} ({_node.ExportFormat.ToUpper()}) - {_node.ExportFps} FPS";
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
                PreviewMediaElement.Play();
                _isPlaying = true;
                if (PlayPauseIconText != null) PlayPauseIconText.Text = "⏸";
                _positionTimer.Start();
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi mở video: {ex.Message}");
            }
        }

        private void PreviewMediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement != null && PreviewMediaElement.NaturalDuration.HasTimeSpan)
            {
                _videoDuration = PreviewMediaElement.NaturalDuration.TimeSpan;
                if (SeekSlider != null) SeekSlider.Maximum = _videoDuration.TotalSeconds;
                UpdateTimecodeText();
                _node.TrimEndTime = FormatTimeSpan(_videoDuration);
                if (TrimEndTextBox != null) TrimEndTextBox.Text = _node.TrimEndTime;
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
            if (TimecodeText == null || PreviewMediaElement == null) return;
            var cur = PreviewMediaElement.Position;
            TimecodeText.Text = $"{FormatTimeSpan(cur)} / {FormatTimeSpan(_videoDuration)}";
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

        private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isSeeking = true;
        }

        private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isSeeking = false;
            if (PreviewMediaElement != null && SeekSlider != null)
            {
                PreviewMediaElement.Position = TimeSpan.FromSeconds(SeekSlider.Value);
                UpdateTimecodeText();
            }
        }

        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSeeking && PreviewMediaElement != null)
            {
                PreviewMediaElement.Position = TimeSpan.FromSeconds(e.NewValue);
                UpdateTimecodeText();
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
                Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.webm;*.flv|All Files|*.*"
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
        }

        private void TrimStartTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || TrimStartTextBox == null) return;
            if (TrimStartTextBox.IsFocused) _node.TrimStartTime = TrimStartTextBox.Text;
        }

        private void TrimEndTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || TrimEndTextBox == null) return;
            if (TrimEndTextBox.IsFocused) _node.TrimEndTime = TrimEndTextBox.Text;
        }

        private void SetTrimStartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement == null || TrimStartTextBox == null) return;
            var cur = PreviewMediaElement.Position;
            _node.TrimStartTime = FormatTimeSpan(cur);
            TrimStartTextBox.Text = _node.TrimStartTime;
        }

        private void SetTrimEndBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewMediaElement == null || TrimEndTextBox == null) return;
            var cur = PreviewMediaElement.Position;
            _node.TrimEndTime = FormatTimeSpan(cur);
            TrimEndTextBox.Text = _node.TrimEndTime;
        }

        // --- COLOR CONTROLS ---
        private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (BrightnessSlider == null || ContrastSlider == null || SaturationSlider == null || GammaSlider == null) return;
            if (BrightnessValText == null || ContrastValText == null || SaturationValText == null || GammaValText == null) return;

            _node.Brightness = BrightnessSlider.Value;
            _node.Contrast = ContrastSlider.Value;
            _node.Saturation = SaturationSlider.Value;
            _node.Gamma = GammaSlider.Value;

            BrightnessValText.Text = _node.Brightness.ToString("0.0");
            ContrastValText.Text = _node.Contrast.ToString("0.0");
            SaturationValText.Text = _node.Saturation.ToString("0.0");
            GammaValText.Text = _node.Gamma.ToString("0.0");
        }

        private void PresetNormal_Click(object sender, RoutedEventArgs e)
        {
            ResetColor_Click(sender, e);
            _node.FilterPreset = "None";
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

        // --- SPEED & SCALE ---
        private void SpeedBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string strSpeed && double.TryParse(strSpeed, out var spd))
            {
                _node.Speed = spd;
                if (PreviewMediaElement != null) PreviewMediaElement.SpeedRatio = spd;
                SyncNodeToUi();
            }
        }

        private void ScaleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || ScaleEnableCheckBox == null) return;
            _node.ScaleEnabled = ScaleEnableCheckBox.IsChecked == true;
        }

        private void ScaleSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || TargetWidthTextBox == null || TargetHeightTextBox == null) return;
            if (int.TryParse(TargetWidthTextBox.Text, out var w)) _node.TargetWidth = w;
            if (int.TryParse(TargetHeightTextBox.Text, out var h)) _node.TargetHeight = h;
        }

        private void RotateFlipCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || RotateFlipCombo == null) return;
            if (RotateFlipCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _node.RotateFlip = tag;
            }
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
