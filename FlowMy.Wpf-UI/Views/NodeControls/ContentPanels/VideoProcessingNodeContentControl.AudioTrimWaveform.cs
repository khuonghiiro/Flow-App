// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FlowMy.Controls;
using FlowMy.Services.Workflow.NodeExecutors;
using Microsoft.Win32;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl
    {
        private static readonly ConcurrentDictionary<string, AudioWaveformData> _waveformCache = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _waveformExtractCts;
        private bool _isAudioWaveformInitialized;

        private void InitAudioTrimWaveformWiring()
        {
            if (_isAudioWaveformInitialized) return;
            _isAudioWaveformInitialized = true;

            if (AudioTrimWaveformControl != null)
            {
                if (AudioWaveformScrollViewer != null)
                {
                    AudioTrimWaveformControl.AttachScrollViewer(AudioWaveformScrollViewer);
                }

                AudioTrimWaveformControl.TrimRangeChanged += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _suppressControlSync = true;
                    try
                    {
                        var st = AudioTrimWaveformControl.TrimStartSec;
                        var ed = AudioTrimWaveformControl.TrimEndSec;

                        _node.AudioTrimStartSec = st;
                        _node.AudioTrimEndSec = ed;

                        if (AudioTrimStartSlider != null) AudioTrimStartSlider.Value = st;
                        if (AudioTrimEndSlider != null) AudioTrimEndSlider.Value = ed;

                        if (AudioTrimStartBox != null) AudioTrimStartBox.Text = FormatTimeSec(st);
                        if (AudioTrimEndBox != null) AudioTrimEndBox.Text = FormatTimeSec(ed);

                        if (AudioTrimStartSecLabel != null) AudioTrimStartSecLabel.Text = $"{st:0.##}s";
                        if (AudioTrimEndSecLabel != null) AudioTrimEndSecLabel.Text = $"{ed:0.##}s";

                        UpdateAudioTrimDurationLabel();
                    }
                    finally
                    {
                        _suppressControlSync = false;
                    }
                };

                AudioTrimWaveformControl.SeekRequested += sec =>
                {
                    CommitTrimSeekPosition(sec);
                };

                AudioTrimWaveformControl.ZoomChanged += zoom =>
                {
                    UpdateZoomUi(zoom);
                };
            }

            // Zoom Toolbar Controls
            if (AudioWaveformZoomSlider != null)
            {
                AudioWaveformZoomSlider.ValueChanged += (_, e) =>
                {
                    if (AudioTrimWaveformControl != null)
                    {
                        AudioTrimWaveformControl.ZoomLevel = e.NewValue;
                        UpdateZoomUi(e.NewValue);
                    }
                };
            }

            if (AudioWaveformZoomInButton != null)
            {
                AudioWaveformZoomInButton.Click += (_, _) =>
                {
                    if (AudioTrimWaveformControl != null)
                    {
                        AudioTrimWaveformControl.ZoomLevel = Math.Min(100.0, AudioTrimWaveformControl.ZoomLevel * 1.4);
                        if (AudioWaveformZoomSlider != null) AudioWaveformZoomSlider.Value = AudioTrimWaveformControl.ZoomLevel;
                    }
                };
            }

            if (AudioWaveformZoomOutButton != null)
            {
                AudioWaveformZoomOutButton.Click += (_, _) =>
                {
                    if (AudioTrimWaveformControl != null)
                    {
                        AudioTrimWaveformControl.ZoomLevel = Math.Max(1.0, AudioTrimWaveformControl.ZoomLevel / 1.4);
                        if (AudioWaveformZoomSlider != null) AudioWaveformZoomSlider.Value = AudioTrimWaveformControl.ZoomLevel;
                    }
                };
            }

            if (AudioWaveformFitButton != null)
            {
                AudioWaveformFitButton.Click += (_, _) =>
                {
                    if (AudioTrimWaveformControl != null)
                    {
                        AudioTrimWaveformControl.ZoomLevel = 1.0;
                        if (AudioWaveformZoomSlider != null) AudioWaveformZoomSlider.Value = 1.0;
                        if (AudioWaveformScrollViewer != null) AudioWaveformScrollViewer.ScrollToHorizontalOffset(0);
                    }
                };
            }

            // Button Xuất Audio đã cắt
            if (ExportAudioTrimButton != null)
            {
                ExportAudioTrimButton.Click += async (_, _) =>
                {
                    await ExecuteExportAudioTrimAsync();
                };
            }
        }

        private void UpdateZoomUi(double zoom)
        {
            if (AudioWaveformZoomLabel != null)
            {
                string resStr;
                if (zoom < 2.0) resStr = "5s/vạch";
                else if (zoom < 5.0) resStr = "1s/vạch";
                else if (zoom < 15.0) resStr = "0.2s/vạch";
                else if (zoom < 40.0) resStr = "50ms/vạch";
                else resStr = "10ms/vạch";

                AudioWaveformZoomLabel.Text = $"🔍 {zoom:0.0}x ({resStr})";
            }
        }

        public async Task LoadWaveformDataForCurrentVideoAsync(bool forceReload = false)
        {
            var videoPath = _node.VideoPath;
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                if (AudioTrimWaveformControl != null) AudioTrimWaveformControl.WaveformData = null;
                return;
            }

            if (!forceReload && _waveformCache.TryGetValue(videoPath, out var cachedData) && cachedData.IsValid)
            {
                ApplyWaveformData(cachedData);
                return;
            }

            _waveformExtractCts?.Cancel();
            _waveformExtractCts = new CancellationTokenSource();
            var ct = _waveformExtractCts.Token;

            try
            {
                var duration = GetNaturalDurationSeconds();
                if (duration <= 0)
                {
                    duration = await VideoProcessingNodeExecutor.ProbeDurationSecondsAsync(videoPath, ct).ConfigureAwait(false);
                }

                if (duration <= 0) return;

                var extractedData = await ExtractWaveformPeaksAsync(videoPath, duration, ct).ConfigureAwait(false);
                if (extractedData != null && extractedData.IsValid)
                {
                    _waveformCache[videoPath] = extractedData;
                    await Dispatcher.InvokeAsync(() => ApplyWaveformData(extractedData));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppendLog($"⚠ Không thể trích xuất sóng âm audio: {ex.Message}");
            }
        }

        private void ApplyWaveformData(AudioWaveformData data)
        {
            if (AudioTrimWaveformControl == null) return;
            if (AudioWaveformScrollViewer != null)
            {
                AudioTrimWaveformControl.AttachScrollViewer(AudioWaveformScrollViewer);
            }
            AudioTrimWaveformControl.TotalDurationSec = data.DurationSec;
            AudioTrimWaveformControl.WaveformData = data;

            var total = Math.Max(0.1, data.DurationSec);
            var st = Math.Clamp(_node.AudioTrimStartSec, 0.0, total);
            var ed = _node.AudioTrimEndSec > 0 ? Math.Clamp(_node.AudioTrimEndSec, st, total) : total;

            AudioTrimWaveformControl.TrimStartSec = st;
            AudioTrimWaveformControl.TrimEndSec = ed;
            AudioTrimWaveformControl.UpdateVisualizerWidth();
            AudioTrimWaveformControl.InvalidateVisual();
        }

        public static async Task<AudioWaveformData?> ExtractWaveformPeaksAsync(string mediaPath, double durationSec, CancellationToken ct)
        {
            var ffmpegExe = FlowMy.Services.Utilities.FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg") ?? "ffmpeg";
            const int sampleRate = 4000;
            const int pps = 100; // 10ms bucket

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = $"-nostats -hide_banner -loglevel error -i \"{mediaPath}\" -vn -ac 1 -ar {sampleRate} -f f32le pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var totalBuckets = (int)Math.Ceiling(durationSec * pps);
            if (totalBuckets <= 0) totalBuckets = 100;

            var minPeaks = new float[totalBuckets];
            var maxPeaks = new float[totalBuckets];
            var samplesPerBucket = sampleRate / pps; // 40 samples per 10ms bucket

            var buffer = new byte[8192];
            var floatBuffer = new float[2048];
            var currentBucketIdx = 0;
            var currentBucketSampleCount = 0;
            var curMin = 0f;
            var curMax = 0f;

            using (var stream = proc.StandardOutput.BaseStream)
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                {
                    var floatCount = bytesRead / 4;
                    Buffer.BlockCopy(buffer, 0, floatBuffer, 0, floatCount * 4);

                    for (var i = 0; i < floatCount; i++)
                    {
                        var sample = floatBuffer[i];
                        if (sample < curMin) curMin = sample;
                        if (sample > curMax) curMax = sample;

                        currentBucketSampleCount++;
                        if (currentBucketSampleCount >= samplesPerBucket)
                        {
                            if (currentBucketIdx < totalBuckets)
                            {
                                minPeaks[currentBucketIdx] = curMin;
                                maxPeaks[currentBucketIdx] = curMax;
                                currentBucketIdx++;
                            }
                            curMin = 0f;
                            curMax = 0f;
                            currentBucketSampleCount = 0;
                        }
                    }
                }
            }

            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            if (currentBucketIdx > 0 && currentBucketIdx < totalBuckets)
            {
                Array.Resize(ref minPeaks, currentBucketIdx);
                Array.Resize(ref maxPeaks, currentBucketIdx);
            }

            return new AudioWaveformData
            {
                DurationSec = durationSec,
                MinPeaks = minPeaks,
                MaxPeaks = maxPeaks,
                PeaksPerSecond = pps
            };
        }

        private async Task ExecuteExportAudioTrimAsync()
        {
            var srcPath = _node.VideoPath;
            if (string.IsNullOrWhiteSpace(srcPath) || !File.Exists(srcPath))
            {
                MessageBox.Show("Vui lòng nạp tệp video / audio hợp lệ trước khi xuất.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var startSec = Math.Max(0, _node.AudioTrimStartSec);
            var endSec = _node.AudioTrimEndSec > startSec ? _node.AudioTrimEndSec : GetNaturalDurationSeconds();
            var durSec = Math.Max(0.1, endSec - startSec);

            var defaultName = $"{Path.GetFileNameWithoutExtension(srcPath)}_trim_{startSec:0.##}s_{endSec:0.##}s.mp3";
            var sfd = new SaveFileDialog
            {
                Title = "Lưu file Audio phân đoạn đã cắt",
                FileName = defaultName,
                Filter = "MP3 Audio (*.mp3)|*.mp3|WAV Lossless (*.wav)|*.wav|AAC Audio (*.aac)|*.aac|M4A Audio (*.m4a)|*.m4a|All Files (*.*)|*.*"
            };

            if (sfd.ShowDialog() != true) return;

            var outPath = sfd.FileName;
            var ffmpegExe = FlowMy.Services.Utilities.FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg") ?? "ffmpeg";

            ShowVideoProcessing($"Đang xuất phân đoạn audio ({startSec:0.##}s ➔ {endSec:0.##}s)...");

            try
            {
                var ext = Path.GetExtension(outPath).ToLowerInvariant();
                var codecArgs = ext switch
                {
                    ".wav" => "-c:a pcm_s16le",
                    ".aac" => "-c:a aac -b:a 192k",
                    ".m4a" => "-c:a aac -b:a 192k",
                    _ => "-c:a libmp3lame -b:a 192k"
                };

                var args = $"-y -ss {startSec.ToString("F3", CultureInfo.InvariantCulture)} -t {durSec.ToString("F3", CultureInfo.InvariantCulture)} -i \"{srcPath}\" -vn {codecArgs} \"{outPath}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    if (proc.ExitCode == 0 && File.Exists(outPath))
                    {
                        AppendLog($"✅ [AUDIO TRIM] Đã xuất thành công: {outPath}");
                        var res = MessageBox.Show($"Đã xuất thành công phân đoạn audio!\n\nĐường dẫn: {outPath}\n\nBạn có muốn mở thư mục chứa tệp không?", "Xuất Audio thành công", MessageBoxButton.YesNo, MessageBoxImage.Information);
                        if (res == MessageBoxResult.Yes)
                        {
                            Process.Start("explorer.exe", $"/select,\"{outPath}\"");
                        }
                    }
                    else
                    {
                        AppendLog($"⚠ [AUDIO TRIM] Lỗi khi xuất audio (Exit code: {proc.ExitCode})");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"⚠ [AUDIO TRIM] Lỗi: {ex.Message}");
            }
            finally
            {
                HideVideoProcessing();
            }
        }

        private void SyncTrimWaveformPlayhead(double currentSec)
        {
            if (AudioTrimWaveformControl != null)
            {
                AudioTrimWaveformControl.CurrentPlayheadSec = currentSec;
            }
        }
    }
}
