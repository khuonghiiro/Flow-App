using System;
using System.Diagnostics;
using System.IO;
using FlowMy.Models.Nodes;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FlowMy.Services.Workflow.Audio
{
    /// <summary>
    /// High-performance, zero-latency real-time video audio playback engine powered by NAudio.
    /// Directly decodes audio streams from video containers into memory and applies live DSP filters
    /// with instantaneous slider responsiveness (< 50ms latency).
    /// </summary>
    public class RealTimeVideoAudioEngine : IDisposable
    {
        private MediaFoundationReader? _reader;
        private IWavePlayer? _outputDevice;
        private RealTimeAudioDspPipeline? _dspPipeline;
        private readonly object _lock = new();
        private bool _isDisposed;
        private string? _currentFilePath;
        private double _totalDurationSec;
        private bool _isLooping = true;

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

        public bool LoadMedia(string videoFilePath, double totalDurationSec)
        {
            if (string.IsNullOrWhiteSpace(videoFilePath) || !File.Exists(videoFilePath))
                return false;

            lock (_lock)
            {
                try
                {
                    DisposeInternal();

                    _currentFilePath = videoFilePath;
                    _totalDurationSec = totalDurationSec;

                    // 1. Decode audio track directly from video container via MediaFoundation
                    _reader = new MediaFoundationReader(videoFilePath);
                    if (_reader.WaveFormat == null || _reader.WaveFormat.SampleRate <= 0 || _reader.WaveFormat.Channels <= 0)
                    {
                        DisposeInternal();
                        return false;
                    }

                    var sampleProvider = _reader.ToSampleProvider();

                    // 2. Wrap into real-time multi-channel DSP pipeline
                    _dspPipeline = new RealTimeAudioDspPipeline(sampleProvider, totalDurationSec);

                    // 3. Initialize resilient low-latency output device (WaveOutEvent / DirectSoundOut / WasapiOut)
                    _outputDevice = InitReliableOutputDevice(_dspPipeline);
                    return _outputDevice != null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RealTimeVideoAudioEngine] LoadMedia failed: {ex.Message}");
                    DisposeInternal();
                    return false;
                }
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
            _dspPipeline.SetVolume(effectiveVol);
            _dspPipeline.SetEq3Band((float)node.AudioBassGain, (float)node.AudioMidGain, (float)node.AudioTrebleGain, node.AudioEqPreset);
            _dspPipeline.SetFilters(node.AudioHighpassFilter, (float)node.AudioHighpassCutoffHz, node.AudioLowpassFilter, (float)node.AudioLowpassCutoffHz, node.AudioNormalizeEnabled);
            _dspPipeline.SetStereoAndWarmth((float)node.AudioStereoWidthPercent, (float)node.AudioWarmthPercent, (float)node.AudioReverbPercent, (float)node.AudioVocalBalance, (float)node.AudioPitchSemitones);
            _dspPipeline.SetDynamics((float)node.AudioCompressorPercent, (float)node.AudioDeEsserPercent, (float)node.AudioNoiseGatePercent);
            _dspPipeline.SetFade((float)node.AudioFadeInSec, (float)node.AudioFadeOutSec, (float)_totalDurationSec);
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
