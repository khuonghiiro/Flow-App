using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowMy.Core.Models.Media;
using NAudio.Wave;

namespace FlowMy.Services.Workflow.Audio
{
    /// <summary>
    /// High-performance in-memory RAM audio mixer for multi-segment voiceover and dubbing clips.
    /// Provides real-time mixing, zero-latency sample fetching, automatic ducking envelope follower,
    /// and strict RAM lifecycle management (cache clearing and memory cleanup).
    /// </summary>
    public sealed class DubbingAudioMixer : IDisposable
    {
        private sealed class CachedSoundClip
        {
            public float[] AudioData { get; }
            public int Channels { get; }
            public int SampleRate { get; }
            public double DurationSec { get; }

            public CachedSoundClip(float[] audioData, int channels, int sampleRate)
            {
                AudioData = audioData;
                Channels = channels;
                SampleRate = sampleRate;
                DurationSec = audioData.Length / (double)(channels * sampleRate);
            }
        }

        private readonly ConcurrentDictionary<string, CachedSoundClip> _clipCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _stateLock = new();
        private List<DubbingClipItem> _activeClips = new();
        private AutoDuckingConfig _duckingConfig = new();
        private float _currentDuckingGain = 1.0f;
        private bool _isDisposed;

        public float CurrentDuckingGain => _currentDuckingGain;

        public void UpdateConfig(IEnumerable<DubbingClipItem> clips, AutoDuckingConfig? ducking)
        {
            lock (_stateLock)
            {
                _activeClips = clips?.Where(c => !string.IsNullOrWhiteSpace(c.AudioFilePath) || !string.IsNullOrWhiteSpace(c.ScriptText)).ToList() ?? new List<DubbingClipItem>();
                _duckingConfig = ducking ?? new AutoDuckingConfig();
            }
        }

        public bool PreloadClipToRam(string audioFilePath)
        {
            if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
                return false;

            if (_clipCache.ContainsKey(audioFilePath))
                return true;

            try
            {
                using var reader = new AudioFileReader(audioFilePath);
                var sampleCount = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8));
                var buffer = new float[sampleCount];
                var read = reader.Read(buffer, 0, sampleCount);
                if (read < sampleCount)
                {
                    Array.Resize(ref buffer, read);
                }

                var clip = new CachedSoundClip(buffer, reader.WaveFormat.Channels, reader.WaveFormat.SampleRate);
                _clipCache[audioFilePath] = clip;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RegisterInMemoryClip(string key, float[] samples, int channels, int sampleRate)
        {
            if (string.IsNullOrWhiteSpace(key) || samples == null || samples.Length == 0)
                return;

            _clipCache[key] = new CachedSoundClip(samples, channels, sampleRate);
        }

        /// <summary>
        /// Mixes dubbing clips into target stereo sample buffer at the given video playback timestamp.
        /// Also computes real-time ducking gain for source audio.
        /// </summary>
        public float ProcessDubbingAndDucking(float[] buffer, int offset, int count, double currentPositionSec, int sampleRate)
        {
            if (_isDisposed) return 1.0f;

            bool isAnyVoiceoverActive = false;
            List<DubbingClipItem> clipsSnapshot;
            AutoDuckingConfig duckConfig;

            lock (_stateLock)
            {
                clipsSnapshot = _activeClips;
                duckConfig = _duckingConfig;
            }

            if (clipsSnapshot.Count == 0)
            {
                _currentDuckingGain = 1.0f;
                return 1.0f;
            }

            bool hasSolo = clipsSnapshot.Any(c => c.IsSolo && !c.IsMuted);

            for (int i = 0; i < clipsSnapshot.Count; i++)
            {
                var clip = clipsSnapshot[i];
                if (clip.IsMuted) continue;
                if (hasSolo && !clip.IsSolo) continue;

                if (string.IsNullOrWhiteSpace(clip.AudioFilePath)) continue;

                if (!_clipCache.TryGetValue(clip.AudioFilePath, out var cachedClip))
                {
                    PreloadClipToRam(clip.AudioFilePath);
                    _clipCache.TryGetValue(clip.AudioFilePath, out cachedClip);
                }

                if (cachedClip == null || cachedClip.AudioData.Length == 0) continue;

                double clipStart = clip.StartAtSec;
                double clipEnd = clipStart + cachedClip.DurationSec;

                if (currentPositionSec >= clipStart && currentPositionSec < clipEnd)
                {
                    isAnyVoiceoverActive = true;
                    MixSingleClip(buffer, offset, count, currentPositionSec, clip, cachedClip, sampleRate);
                }
            }

            // Auto-Ducking envelope computation
            if (duckConfig.Enabled && isAnyVoiceoverActive)
            {
                float targetGain = (float)Math.Pow(10.0, duckConfig.DuckingAmountDb / 20.0);
                float attackRate = (float)(1.0 / (Math.Max(10.0, duckConfig.AttackMs) * sampleRate * 0.001));
                _currentDuckingGain = Math.Max(targetGain, _currentDuckingGain - attackRate * (count / 2));
            }
            else
            {
                float releaseRate = (float)(1.0 / (Math.Max(50.0, duckConfig.ReleaseMs) * sampleRate * 0.001));
                _currentDuckingGain = Math.Min(1.0f, _currentDuckingGain + releaseRate * (count / 2));
            }

            return _currentDuckingGain;
        }

        private static void MixSingleClip(float[] buffer, int offset, int count, double currentPositionSec,
            DubbingClipItem item, CachedSoundClip clip, int targetSampleRate)
        {
            double relTimeSec = currentPositionSec - item.StartAtSec;
            if (relTimeSec < 0) return;

            long startSampleIndex = (long)(relTimeSec * clip.SampleRate * clip.Channels);
            float vol = (float)(item.VolumePercent / 100.0);
            float panL = item.Pan <= 0 ? 1.0f : (1.0f - (float)item.Pan);
            float panR = item.Pan >= 0 ? 1.0f : (1.0f + (float)item.Pan);

            int stereoFrames = count / 2;
            for (int f = 0; f < stereoFrames; f++)
            {
                long srcIdx = startSampleIndex + (long)(f * (clip.SampleRate / (double)targetSampleRate) * clip.Channels);
                if (srcIdx >= clip.AudioData.Length - 1) break;

                float clipSampleL;
                float clipSampleR;

                if (clip.Channels == 1)
                {
                    clipSampleL = clip.AudioData[srcIdx];
                    clipSampleR = clipSampleL;
                }
                else
                {
                    clipSampleL = clip.AudioData[srcIdx];
                    clipSampleR = clip.AudioData[srcIdx + 1];
                }

                int outIdxL = offset + (f * 2);
                int outIdxR = outIdxL + 1;

                if (outIdxR < buffer.Length)
                {
                    buffer[outIdxL] += clipSampleL * vol * panL;
                    buffer[outIdxR] += clipSampleR * vol * panR;
                }
            }
        }

        /// <summary>
        /// Clears all RAM-cached audio buffers and requests immediate garbage collection.
        /// </summary>
        public void ClearCache()
        {
            lock (_stateLock)
            {
                _clipCache.Clear();
                _activeClips.Clear();
                _currentDuckingGain = 1.0f;
            }

            GC.Collect(2, GCCollectionMode.Optimized, false);
        }

        public long GetTotalMemoryUsageBytes()
        {
            long total = 0;
            foreach (var kvp in _clipCache)
            {
                total += (long)kvp.Value.AudioData.Length * sizeof(float);
            }
            return total;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            ClearCache();
        }
    }
}
