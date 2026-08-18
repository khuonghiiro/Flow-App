// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowMy.Controls;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow.NodeExecutors;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl
    {
        private void WireAudioMasterToggles()
        {
            // 1. Module 2: Voice Changer & Wave Shaper Master Toggle
            if (VoiceChangerToggle != null)
            {
                VoiceChangerToggle.Checked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.VoiceChangerEnabled = true;
                    if (VoiceChangerContainer != null) VoiceChangerContainer.Visibility = Visibility.Visible;
                    if (VoiceChangerStatusText != null) VoiceChangerStatusText.Text = "ĐANG BẬT";
                    TriggerDspRegenIfActive();
                };
                VoiceChangerToggle.Unchecked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.VoiceChangerEnabled = false;
                    if (VoiceChangerContainer != null) VoiceChangerContainer.Visibility = Visibility.Collapsed;
                    if (VoiceChangerStatusText != null) VoiceChangerStatusText.Text = "TẮT (Gốc)";
                    TriggerDspRegenIfActive();
                };
            }

            // 2. Module 3: Equalizer & Studio FX Master Toggle
            if (EqualizerFxToggle != null)
            {
                EqualizerFxToggle.Checked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.EqualizerFxEnabled = true;
                    if (EqualizerFxContainer != null) EqualizerFxContainer.Visibility = Visibility.Visible;
                    if (EqualizerFxStatusText != null) EqualizerFxStatusText.Text = "ĐANG BẬT";
                    TriggerDspRegenIfActive();
                };
                EqualizerFxToggle.Unchecked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.EqualizerFxEnabled = false;
                    if (EqualizerFxContainer != null) EqualizerFxContainer.Visibility = Visibility.Collapsed;
                    if (EqualizerFxStatusText != null) EqualizerFxStatusText.Text = "TẮT (Bỏ qua FX)";
                    TriggerDspRegenIfActive();
                };
            }

            // 3. Module 4: Dynamics & Mastering Master Toggle
            if (DynamicsMasteringToggle != null)
            {
                DynamicsMasteringToggle.Checked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.DynamicsMasteringEnabled = true;
                    if (DynamicsMasteringContainer != null) DynamicsMasteringContainer.Visibility = Visibility.Visible;
                    if (DynamicsMasteringStatusText != null) DynamicsMasteringStatusText.Text = "ĐANG BẬT";
                    TriggerDspRegenIfActive();
                };
                DynamicsMasteringToggle.Unchecked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.DynamicsMasteringEnabled = false;
                    if (DynamicsMasteringContainer != null) DynamicsMasteringContainer.Visibility = Visibility.Collapsed;
                    if (DynamicsMasteringStatusText != null) DynamicsMasteringStatusText.Text = "TẮT (Bỏ qua)";
                    TriggerDspRegenIfActive();
                };
            }

            // 4. Module 5: Multi-Track BGM Master Toggle
            if (MultiTrackBgmToggle != null)
            {
                MultiTrackBgmToggle.Checked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.MultiTrackBgmEnabled = true;
                    if (MultiTrackBgmContainer != null) MultiTrackBgmContainer.Visibility = Visibility.Visible;
                    if (MultiTrackBgmStatusText != null) MultiTrackBgmStatusText.Text = $"{_node.AudioTracks.Count} Track(s)";
                };
                MultiTrackBgmToggle.Unchecked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.MultiTrackBgmEnabled = false;
                    if (MultiTrackBgmContainer != null) MultiTrackBgmContainer.Visibility = Visibility.Collapsed;
                    if (MultiTrackBgmStatusText != null) MultiTrackBgmStatusText.Text = "TẮT";
                };
            }

            // 5. Module 6: Audio Export Config Master Toggle
            if (AudioExportConfigToggle != null)
            {
                AudioExportConfigToggle.Checked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioExportConfigEnabled = true;
                    if (AudioExportConfigContainer != null) AudioExportConfigContainer.Visibility = Visibility.Visible;
                    if (AudioExportConfigStatusText != null) AudioExportConfigStatusText.Text = "ĐANG BẬT";
                };
                AudioExportConfigToggle.Unchecked += (_, _) =>
                {
                    if (_suppressControlSync) return;
                    _node.AudioExportConfigEnabled = false;
                    if (AudioExportConfigContainer != null) AudioExportConfigContainer.Visibility = Visibility.Collapsed;
                    if (AudioExportConfigStatusText != null) AudioExportConfigStatusText.Text = "TẮT (Mặc định)";
                };
            }
        }

        private void SyncAudioMasterTogglesFromModel()
        {
            if (VoiceChangerToggle != null)
            {
                VoiceChangerToggle.IsChecked = _node.VoiceChangerEnabled;
                if (VoiceChangerContainer != null)
                    VoiceChangerContainer.Visibility = _node.VoiceChangerEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (VoiceChangerStatusText != null)
                    VoiceChangerStatusText.Text = _node.VoiceChangerEnabled ? "ĐANG BẬT" : "TẮT (Gốc)";
            }

            if (EqualizerFxToggle != null)
            {
                EqualizerFxToggle.IsChecked = _node.EqualizerFxEnabled;
                if (EqualizerFxContainer != null)
                    EqualizerFxContainer.Visibility = _node.EqualizerFxEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (EqualizerFxStatusText != null)
                    EqualizerFxStatusText.Text = _node.EqualizerFxEnabled ? "ĐANG BẬT" : "TẮT (Bỏ qua FX)";
            }

            if (DynamicsMasteringToggle != null)
            {
                DynamicsMasteringToggle.IsChecked = _node.DynamicsMasteringEnabled;
                if (DynamicsMasteringContainer != null)
                    DynamicsMasteringContainer.Visibility = _node.DynamicsMasteringEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (DynamicsMasteringStatusText != null)
                    DynamicsMasteringStatusText.Text = _node.DynamicsMasteringEnabled ? "ĐANG BẬT" : "TẮT (Bỏ qua)";
            }

            if (MultiTrackBgmToggle != null)
            {
                MultiTrackBgmToggle.IsChecked = _node.MultiTrackBgmEnabled;
                if (MultiTrackBgmContainer != null)
                    MultiTrackBgmContainer.Visibility = _node.MultiTrackBgmEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (MultiTrackBgmStatusText != null)
                    MultiTrackBgmStatusText.Text = _node.MultiTrackBgmEnabled ? $"{_node.AudioTracks.Count} Track(s)" : "TẮT";
            }

            if (AudioExportConfigToggle != null)
            {
                AudioExportConfigToggle.IsChecked = _node.AudioExportConfigEnabled;
                if (AudioExportConfigContainer != null)
                    AudioExportConfigContainer.Visibility = _node.AudioExportConfigEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (AudioExportConfigStatusText != null)
                    AudioExportConfigStatusText.Text = _node.AudioExportConfigEnabled ? "ĐANG BẬT" : "TẮT (Mặc định)";
            }
        }

        #region Multi-Track BGM Waveform Visualizer Handlers

        private void TrackWaveformScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            // Optional: attach any scroll viewer specific event listeners if needed
        }

        private void TrackWaveformVisualizer_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not AudioTrimWaveformVisualizer visualizer || visualizer.Tag is not VideoAudioTrackConfig track)
                return;

            // Find parent ScrollViewer
            var scrollViewer = FindVisualParent<ScrollViewer>(visualizer);
            if (scrollViewer != null)
            {
                visualizer.AttachScrollViewer(scrollViewer);
            }

            var videoDuration = GetNaturalDurationSeconds();
            var targetDur = videoDuration > 0.05 ? videoDuration : 10.0;

            visualizer.IsRangeDurationLocked = track.LockToVideoDuration;
            visualizer.LockedDurationSec = targetDur;
            visualizer.TrimStartSec = track.TrimStartSec;
            visualizer.TrimEndSec = track.TrimEndSec > 0 ? track.TrimEndSec : (track.TrimStartSec + targetDur);

            visualizer.TrimRangeChanged -= Visualizer_TrimRangeChanged;
            visualizer.TrimRangeChanged += Visualizer_TrimRangeChanged;

            // Asynchronously load waveform for the track audio
            if (!string.IsNullOrWhiteSpace(track.SourceOutputKey) && File.Exists(track.SourceOutputKey))
            {
                _ = LoadTrackWaveformAsync(track, visualizer);
            }
        }

        private void Visualizer_TrimRangeChanged(object? sender, EventArgs e)
        {
            if (sender is AudioTrimWaveformVisualizer v && v.Tag is VideoAudioTrackConfig track)
            {
                track.TrimStartSec = v.TrimStartSec;
                track.TrimEndSec = v.TrimEndSec;
            }
        }

        private void TrackLockDurationToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el || el.Tag is not VideoAudioTrackConfig track) return;

            var parentContainer = FindVisualParent<Border>(el);
            if (parentContainer == null) return;

            var visualizer = FindVisualChild<AudioTrimWaveformVisualizer>(parentContainer);
            if (visualizer != null)
            {
                var videoDuration = GetNaturalDurationSeconds();
                var targetDur = videoDuration > 0.05 ? videoDuration : 10.0;
                visualizer.IsRangeDurationLocked = track.LockToVideoDuration;
                visualizer.LockedDurationSec = targetDur;

                if (track.LockToVideoDuration)
                {
                    visualizer.TrimEndSec = visualizer.TrimStartSec + targetDur;
                    track.TrimEndSec = visualizer.TrimEndSec;
                }

                visualizer.InvalidateVisual();
            }
        }

        private void LoadTrackWaveform_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el || el.Tag is not VideoAudioTrackConfig track) return;

            var parentContainer = FindVisualParent<Border>(el);
            if (parentContainer == null) return;

            var visualizer = FindVisualChild<AudioTrimWaveformVisualizer>(parentContainer);
            if (visualizer != null)
            {
                _ = LoadTrackWaveformAsync(track, visualizer, forceReload: true);
            }
        }

        private async Task LoadTrackWaveformAsync(VideoAudioTrackConfig track, AudioTrimWaveformVisualizer visualizer, bool forceReload = false)
        {
            var audioPath = track.SourceOutputKey;
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath)) return;

            if (!forceReload && _waveformCache.TryGetValue(audioPath, out var cachedData) && cachedData.IsValid)
            {
                ApplyTrackWaveformData(visualizer, track, cachedData);
                return;
            }

            try
            {
                var duration = await VideoProcessingNodeExecutor.ProbeDurationSecondsAsync(audioPath, CancellationToken.None).ConfigureAwait(false);
                if (duration <= 0) return;

                var extractedData = await ExtractWaveformPeaksAsync(audioPath, duration, CancellationToken.None).ConfigureAwait(false);
                if (extractedData != null && extractedData.IsValid)
                {
                    _waveformCache[audioPath] = extractedData;
                    await Dispatcher.InvokeAsync(() => ApplyTrackWaveformData(visualizer, track, extractedData));
                }
            }
            catch (Exception ex)
            {
                AppendLog($"⚠ Không thể trích xuất sóng âm track BGM: {ex.Message}");
            }
        }

        private void ApplyTrackWaveformData(AudioTrimWaveformVisualizer visualizer, VideoAudioTrackConfig track, AudioWaveformData data)
        {
            if (visualizer == null || data == null) return;

            var videoDuration = GetNaturalDurationSeconds();
            var targetDur = videoDuration > 0.05 ? videoDuration : 10.0;

            visualizer.TotalDurationSec = data.DurationSec;
            visualizer.WaveformData = data;
            visualizer.IsRangeDurationLocked = track.LockToVideoDuration;
            visualizer.LockedDurationSec = targetDur;

            var total = Math.Max(0.1, data.DurationSec);
            var st = Math.Clamp(track.TrimStartSec, 0.0, total);
            var ed = track.TrimEndSec > 0 ? Math.Clamp(track.TrimEndSec, st, total) : (track.LockToVideoDuration ? Math.Min(total, st + targetDur) : total);

            visualizer.TrimStartSec = st;
            visualizer.TrimEndSec = ed;
            visualizer.UpdateVisualizerWidth();
            visualizer.InvalidateVisual();
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        #endregion
    }
}
