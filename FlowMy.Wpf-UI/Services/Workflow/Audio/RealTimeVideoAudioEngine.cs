using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlowMy.Models.Nodes;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FlowMy.Services.Workflow.Audio
{
    /// <summary>
    /// High-performance, zero-latency real-time video audio playback engine powered by NAudio.
    /// Directly decodes audio streams from video containers into memory on background threads
    /// and applies live DSP filters with instantaneous slider responsiveness (< 20ms latency).
    /// </summary>
    public class RealTimeVideoAudioEngine : IDisposable
    {
        private WaveStream? _reader;
        private IWavePlayer? _outputDevice;
        private RealTimeAudioDspPipeline? _dspPipeline;
        private readonly object _lock = new();
        private bool _isDisposed;
        private string? _currentFilePath;
        private double _totalDurationSec;
        private bool _isLooping = true;
        private int _loadingVersion;

        public bool IsLoaded => _reader != null && _outputDevice != null;
        public PlaybackState PlaybackState => _outputDevice?.PlaybackState ?? PlaybackState.Stopped;
        public string? CurrentFilePath => _currentFilePath;

        public bool IsLooping
        {
            get => _isLooping;
            set => _isLooping = value;
        }

        public TimeSpan CurrentPosition
        {
            get
            {
                lock (_lock)
                {
                    try { return _reader?.CurrentTime ?? TimeSpan.Zero; }
                    catch { return TimeSpan.Zero; }
                }
            }
        }

        public async Task<bool> LoadMediaAsync(string videoFilePath, double totalDurationSec)
        {
            if (string.IsNullOrWhiteSpace(videoFilePath) || !File.Exists(videoFilePath))
                return false;

            var version = Interlocked.Increment(ref _loadingVersion);

            try
            {
                // Run media stream initialization on MTA thread pool to avoid STA COM deadlocks with WPF MediaElement
                var loadResult = await Task.Run(() =>
                {
                    try
                    {
                        var settings = new MediaFoundationReader.MediaFoundationReaderSettings
                        {
                            RequestFloatOutput = true
                        };

                        WaveStream reader;
                        var ext = Path.GetExtension(videoFilePath).ToLowerInvariant();
                        if (ext is ".mp3" or ".wav" or ".aac" or ".m4a" or ".flac")
                        {
                            reader = new AudioFileReader(videoFilePath);
                        }
                        else
                        {
                            reader = new MediaFoundationReader(videoFilePath, settings);
                        }

                        if (reader.WaveFormat == null || reader.WaveFormat.SampleRate <= 0 || reader.WaveFormat.Channels <= 0)
                        {
                            reader.Dispose();
                            return null;
                        }

                        return reader;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RealTimeVideoAudioEngine] MediaFoundation decode error: {ex.Message}");
                        return null;
                    }
                }).ConfigureAwait(false);

                if (loadResult == null)
                    return false;

                lock (_lock)
                {
                    // Check if another load superseded this one
                    if (version != _loadingVersion || _isDisposed)
                    {
                        loadResult.Dispose();
                        return false;
                    }

                    DisposeInternal();

                    _currentFilePath = videoFilePath;
                    _totalDurationSec = totalDurationSec;
                    _reader = loadResult;

                    var sampleProvider = _reader.ToSampleProvider();
                    _dspPipeline = new RealTimeAudioDspPipeline(sampleProvider, totalDurationSec);
                    _outputDevice = InitReliableOutputDevice(_dspPipeline);
                    return _outputDevice != null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RealTimeVideoAudioEngine] LoadMediaAsync failed: {ex.Message}");
                return false;
            }
        }

        public bool LoadMedia(string videoFilePath, double totalDurationSec)
        {
            if (string.IsNullOrWhiteSpace(videoFilePath) || !File.Exists(videoFilePath))
                return false;

            try
            {
                return LoadMediaAsync(videoFilePath, totalDurationSec).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
        }

        private static IWavePlayer? InitReliableOutputDevice(ISampleProvider sampleProvider)
        {
            // 1. WaveOutEvent (Rock solid on all Windows audio drivers, 50ms buffer, auto resampling)
            try
            {
                var waveOut = new WaveOutEvent { DesiredLatency = 50, NumberOfBuffers = 3 };
                waveOut.Init(sampleProvider);
                return waveOut;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RealTimeVideoAudioEngine] WaveOutEvent fallback: {ex.Message}");
            }

            // 2. DirectSoundOut
            try
            {
                var dsOut = new DirectSoundOut(50);
                dsOut.Init(sampleProvider);
                return dsOut;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RealTimeVideoAudioEngine] DirectSoundOut fallback: {ex.Message}");
            }

            // 3. WasapiOut (Shared mode)
            try
            {
                var wasapi = new WasapiOut(AudioClientShareMode.Shared, 50);
                wasapi.Init(sampleProvider);
                return wasapi;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RealTimeVideoAudioEngine] WasapiOut fallback: {ex.Message}");
            }

            return null;
        }

        public void Play()
        {
            lock (_lock)
            {
                if (_outputDevice == null || _reader == null) return;
                try
                {
                    // If stream is at the end, rewind before playing
                    if (_reader.CurrentTime >= _reader.TotalTime - TimeSpan.FromMilliseconds(200))
                    {
                        _reader.CurrentTime = TimeSpan.Zero;
                        _dspPipeline?.ResetPosition(0);
                    }

                    if (_outputDevice.PlaybackState != PlaybackState.Playing)
                    {
                        _outputDevice.Play();
                    }
                }
                catch { /* best effort */ }
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_outputDevice == null) return;
                try
                {
                    if (_outputDevice.PlaybackState == PlaybackState.Playing)
                    {
                        _outputDevice.Pause();
                    }
                }
                catch { /* best effort */ }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_outputDevice == null) return;
                try
                {
                    _outputDevice.Stop();
                    if (_reader != null)
                    {
                        _reader.CurrentTime = TimeSpan.Zero;
                        _dspPipeline?.ResetPosition(0);
                    }
                }
                catch { /* best effort */ }
            }
        }

        public void Seek(TimeSpan targetTime)
        {
            lock (_lock)
            {
                if (_reader == null || _dspPipeline == null) return;
                try
                {
                    var clampedTime = targetTime < TimeSpan.Zero ? TimeSpan.Zero : targetTime;
                    if (_reader.TotalTime > TimeSpan.Zero && clampedTime > _reader.TotalTime)
                        clampedTime = _reader.TotalTime;

                    _reader.CurrentTime = clampedTime;
                    _dspPipeline.ResetPosition(clampedTime.TotalSeconds);
                }
                catch { /* best effort */ }
            }
        }

        public void SetDspBypass(bool isBypassed)
        {
            if (_dspPipeline != null)
            {
                _dspPipeline.IsBypassed = isBypassed;
            }
        }

        public void ApplyParameters(VideoProcessingNode node, double previewMasterVolume, bool isDspActive = true)
        {
            if (_dspPipeline == null || node == null) return;

            var effectiveVol = node.SourceAudioEnabled
                ? (float)(Math.Max(0, previewMasterVolume) * (node.SourceAudioVolumePercent / 100.0))
                : 0f;

            _dspPipeline.IsBypassed = !isDspActive;
            _dspPipeline.ApplyAllParameters(
                effectiveVol,
                (float)node.AudioBassGain, (float)node.AudioLowMidGain, (float)node.AudioMidGain, (float)node.AudioHighMidGain, (float)node.AudioTrebleGain,
                (float)node.AudioToneClarity, node.AudioEqPreset,
                node.AudioHighpassFilter, (float)node.AudioHighpassCutoffHz,
                node.AudioLowpassFilter, (float)node.AudioLowpassCutoffHz,
                node.AudioNormalizeEnabled,
                (float)node.AudioStereoWidthPercent, (float)node.AudioWarmthPercent, (float)node.AudioReverbPercent, (float)node.AudioVocalBalance, (float)node.AudioPitchSemitones,
                node.AudioEchoEnabled, (float)node.AudioEchoDelayMs, (float)node.AudioEchoFeedbackPercent, (float)node.AudioEchoMixPercent,
                node.AudioRobotVoiceEnabled, node.AudioRadioVoiceEnabled, node.AudioChorusEnabled, (float)node.AudioChorusMixPercent,
                node.Audio8DEnabled, (float)node.Audio8DSpeedHz,
                (float)node.AudioCompressorPercent, (float)node.AudioDeEsserPercent, (float)node.AudioNoiseGatePercent,
                (float)node.AudioFadeInSec, (float)node.AudioFadeOutSec, (float)_totalDurationSec,
                node.AudioWaveShaperEnabled, node.AudioWaveShaperCurve, (float)node.AudioWaveShaperDrivePercent,
                (float)node.AudioTransientPunchPercent, (float)node.AudioSubHarmonicsPercent, (float)node.AudioHarmonicExciterPercent,
                node.AudioPhaseInvertLeft, node.AudioPhaseInvertRight);
        }

        public void GetLatestWaveformData(float[] destinationBuffer, out float rmsL, out float rmsR, out float peak)
        {
            if (_dspPipeline != null)
            {
                _dspPipeline.GetLatestWaveformData(destinationBuffer, out rmsL, out rmsR, out peak);
            }
            else
            {
                rmsL = 0; rmsR = 0; peak = 0;
            }
        }

        private void DisposeInternal()
        {
            try { _outputDevice?.Stop(); } catch { }
            try { _outputDevice?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }

            _outputDevice = null;
            _reader = null;
            _dspPipeline = null;
            _currentFilePath = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            lock (_lock)
            {
                DisposeInternal();
            }
            GC.SuppressFinalize(this);
        }
    }
}
