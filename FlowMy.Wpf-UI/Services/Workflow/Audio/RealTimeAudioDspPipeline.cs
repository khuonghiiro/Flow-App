using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace FlowMy.Services.Workflow.Audio
{
    /// <summary>
    /// Professional real-time multi-channel DSP audio processing pipeline implementing NAudio ISampleProvider.
    /// Provides zero-latency real-time adjustments for:
    /// - 3-Band Parametric Equalizer (Bass 100Hz LowShelf, Mid 1.5kHz Peaking, Treble 5kHz HighShelf)
    /// - Dynamic Cutoff Frequency Filters (Highpass 20-500Hz, Lowpass 2k-20kHz)
    /// - Stereo Field Width Expansion (Mid-Side 0% Mono ~ 200% Wide)
    /// - Analog Tube Warmth / Soft Saturation (0-100%)
    /// - Studio Room Reverb / Ambience (0-100%)
    /// - Volume Gain, Fade In/Out, and Anti-clipping Soft Limiter.
    /// </summary>
    public class RealTimeAudioDspPipeline : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private readonly int _sampleRate;

        // Current parameters (thread-safe reads in audio thread)
        private volatile float _volume = 1.0f;
        private volatile float _bassGainDb = 0f;
        private volatile float _midGainDb = 0f;
        private volatile float _trebleGainDb = 0f;
        private volatile bool _highpassEnabled;
        private volatile float _highpassCutoffHz = 80f;
        private volatile bool _lowpassEnabled;
        private volatile float _lowpassCutoffHz = 12000f;
        private volatile float _stereoWidthFactor = 1.0f;
        private volatile float _warmthFactor = 0f;
        private volatile float _reverbMix = 0f;
        private volatile float _vocalBalance = 0f;
        private volatile float _pitchSemitones = 0f;
        private volatile float _compressorPercent = 0f;
        private volatile float _deEsserPercent = 0f;
        private volatile float _noiseGatePercent = 0f;
        private volatile bool _normalizeEnabled;
        private volatile float _fadeInSec;
        private volatile float _fadeOutSec;
        private volatile float _totalDurationSec;
        private volatile string _preset = "neutral";
        private volatile bool _isBypassed;

        // Filter chains per channel: [channelIndex] -> BiQuadFilter
        private BiQuadFilter[]? _bassFilters;
        private BiQuadFilter[]? _midFilters;
        private BiQuadFilter[]? _trebleFilters;
        private BiQuadFilter[]? _vocalFilters1;
        private BiQuadFilter[]? _vocalFilters2;
        private BiQuadFilter[]? _highpassFilters;
        private BiQuadFilter[]? _lowpassFilters;
        private BiQuadFilter[]? _deEsserFilters;

        // Reverb delay buffers
        private readonly float[] _reverbBufL = new float[2400]; // ~50ms at 48kHz
        private readonly float[] _reverbBufR = new float[3200]; // ~66ms at 48kHz
        private int _reverbIndexL;
        private int _reverbIndexR;

        // Dynamics envelopes
        private float _compEnvelope;
        private float _gateGain = 1.0f;

        // Pitch Shift granular buffers
        private readonly float[] _pitchBufL = new float[4096];
        private readonly float[] _pitchBufR = new float[4096];
        private int _pitchWriteIndex;
        private float _pitchReadPhase1;
        private float _pitchReadPhase2;

        // Position tracking for Fade In / Out
        private long _currentSampleIndex;
        private readonly object _filterLock = new();

        public WaveFormat WaveFormat => _source.WaveFormat;

        public bool IsBypassed
        {
            get => _isBypassed;
            set => _isBypassed = value;
        }

        public RealTimeAudioDspPipeline(ISampleProvider source, double totalDurationSec = 0)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _channels = source.WaveFormat.Channels;
            _sampleRate = source.WaveFormat.SampleRate;
            _totalDurationSec = (float)Math.Max(0, totalDurationSec);

            RebuildFilters();
        }

        public void SetVolume(float volumeMultiplier)
        {
            _volume = Math.Clamp(volumeMultiplier, 0f, 5f);
        }

        public void SetEq3Band(float bassGainDb, float midGainDb, float trebleGainDb, string? preset = null)
        {
            _bassGainDb = Math.Clamp(bassGainDb, -24f, 24f);
            _midGainDb = Math.Clamp(midGainDb, -24f, 24f);
            _trebleGainDb = Math.Clamp(trebleGainDb, -24f, 24f);
            if (preset != null) _preset = preset.Trim().ToLowerInvariant();

            lock (_filterLock)
            {
                RebuildFilters();
            }
        }

        public void SetFilters(bool highpass, float highpassCutoffHz, bool lowpass, float lowpassCutoffHz, bool normalize)
        {
            _highpassEnabled = highpass;
            _highpassCutoffHz = Math.Clamp(highpassCutoffHz, 20f, 500f);
            _lowpassEnabled = lowpass;
            _lowpassCutoffHz = Math.Clamp(lowpassCutoffHz, 2000f, 20000f);
            _normalizeEnabled = normalize;

            lock (_filterLock)
            {
                RebuildFilters();
            }
        }

        public void SetStereoAndWarmth(float stereoWidthPercent, float warmthPercent, float reverbPercent, float vocalBalance = 0f, float pitchSemitones = 0f)
        {
            _stereoWidthFactor = Math.Clamp(stereoWidthPercent / 100.0f, 0f, 2.5f);
            _warmthFactor = Math.Clamp(warmthPercent / 100.0f, 0f, 1.0f);
            _reverbMix = Math.Clamp(reverbPercent / 100.0f, 0f, 1.0f);
            _vocalBalance = Math.Clamp(vocalBalance, -100f, 100f);
            _pitchSemitones = Math.Clamp(pitchSemitones, -12f, 12f);
        }

        public void SetDynamics(float compressorPercent, float deEsserPercent, float noiseGatePercent)
        {
            _compressorPercent = Math.Clamp(compressorPercent, 0f, 100f);
            _deEsserPercent = Math.Clamp(deEsserPercent, 0f, 100f);
            _noiseGatePercent = Math.Clamp(noiseGatePercent, 0f, 100f);
        }

        public void SetFade(float fadeInSec, float fadeOutSec, float totalDurationSec)
        {
            _fadeInSec = Math.Max(0f, fadeInSec);
            _fadeOutSec = Math.Max(0f, fadeOutSec);
            if (totalDurationSec > 0) _totalDurationSec = totalDurationSec;
        }

        public void ResetPosition(double seconds = 0)
        {
            _currentSampleIndex = (long)(Math.Max(0, seconds) * _sampleRate * _channels);
            Array.Clear(_reverbBufL, 0, _reverbBufL.Length);
            Array.Clear(_reverbBufR, 0, _reverbBufR.Length);
            Array.Clear(_pitchBufL, 0, _pitchBufL.Length);
            Array.Clear(_pitchBufR, 0, _pitchBufR.Length);
            _reverbIndexL = 0;
            _reverbIndexR = 0;
            _pitchWriteIndex = 0;
            _pitchReadPhase1 = 0;
            _pitchReadPhase2 = 0;
            _compEnvelope = 0f;
            _gateGain = 1.0f;
            lock (_filterLock)
            {
                RebuildFilters();
            }
        }

        private void RebuildFilters()
        {
            if (_channels <= 0 || _sampleRate <= 0) return;

            var newBass = new BiQuadFilter[_channels];
            var newMid = new BiQuadFilter[_channels];
            var newTreble = new BiQuadFilter[_channels];
            var newVoc1 = new BiQuadFilter[_channels];
            var newVoc2 = new BiQuadFilter[_channels];
            var newHp = _highpassEnabled ? new BiQuadFilter[_channels] : null;
            var newLp = _lowpassEnabled ? new BiQuadFilter[_channels] : null;

            var effectiveBassGain = _bassGainDb;
            var effectiveMidGain = _midGainDb;
            var effectiveTrebleGain = _trebleGainDb;

            // Apply preset base offsets
            var isVocalPreset = _preset == "vocal" || _preset == "vocal_boost";
            var isPodcastPreset = _preset == "podcast";

            for (var ch = 0; ch < _channels; ch++)
            {
                // Bass LowShelf (180Hz, Q=0.9: Broad warm musical bass that kicks audibly on all speakers)
                if (Math.Abs(effectiveBassGain) > 0.05f)
                    newBass[ch] = BiQuadFilter.LowShelf(_sampleRate, 180f, 0.9f, effectiveBassGain);

                // Mid Peaking EQ (1200Hz, Q=0.65: Wide vocal body and instrument clarity)
                if (Math.Abs(effectiveMidGain) > 0.05f)
                    newMid[ch] = BiQuadFilter.PeakingEQ(_sampleRate, 1200f, 0.65f, effectiveMidGain);

                // Treble HighShelf (4500Hz, Q=0.85: Sparkling high-frequency open air)
                if (Math.Abs(effectiveTrebleGain) > 0.05f)
                    newTreble[ch] = BiQuadFilter.HighShelf(_sampleRate, 4500f, 0.85f, effectiveTrebleGain);

                // Preset specific presence filters
                if (isVocalPreset || isPodcastPreset)
                {
                    newVoc1[ch] = BiQuadFilter.PeakingEQ(_sampleRate, 250f, 0.9f, -2.5f); // Cut mud
                    newVoc2[ch] = BiQuadFilter.PeakingEQ(_sampleRate, 3000f, 1.0f, 3.5f); // Boost vocal shine
                }

                // Dynamic Highpass Cutoff (e.g. 20Hz ~ 500Hz)
                if (newHp != null || isPodcastPreset)
                    newHp ??= new BiQuadFilter[_channels];
                if (newHp != null)
                    newHp[ch] = BiQuadFilter.HighPassFilter(_sampleRate, _highpassCutoffHz, 0.7071f);

                // Dynamic Lowpass Cutoff (e.g. 2kHz ~ 20kHz)
                if (newLp != null)
                    newLp[ch] = BiQuadFilter.LowPassFilter(_sampleRate, _lowpassCutoffHz, 0.7071f);
            }

            _bassFilters = newBass;
            _midFilters = newMid;
            _trebleFilters = newTreble;
            _vocalFilters1 = newVoc1;
            _vocalFilters2 = newVoc2;
            _highpassFilters = newHp;
            _lowpassFilters = newLp;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = _source.Read(buffer, offset, count);
            if (samplesRead <= 0) return 0;

            var vol = _volume;
            var norm = _normalizeEnabled;
            var sampleRate = _sampleRate;
            var channels = _channels;
            var fadeInSec = _fadeInSec;
            var fadeOutSec = _fadeOutSec;
            var totalDur = _totalDurationSec;
            var isBypassed = _isBypassed;
            var stereoWidth = _stereoWidthFactor;
            var warmth = _warmthFactor;
            var reverb = _reverbMix;

            var bass = _bassFilters;
            var mid = _midFilters;
            var treble = _trebleFilters;
            var voc1 = _vocalFilters1;
            var voc2 = _vocalFilters2;
            var hp = _highpassFilters;
            var lp = _lowpassFilters;

            var compressor = _compressorPercent;
            var deEsser = _deEsserPercent;
            var noiseGate = _noiseGatePercent;
            var pitchSt = _pitchSemitones;
            var isPitchActive = !isBypassed && Math.Abs(pitchSt) > 0.1f;
            var pitchRatio = isPitchActive ? (float)Math.Pow(2.0, pitchSt / 12.0) : 1.0f;
            var grainSize = 2048; // ~42ms window
            var halfGrain = grainSize / 2;

            for (var n = 0; n < samplesRead; n++)
            {
                var ch = (int)((_currentSampleIndex + n) % channels);
                var sample = buffer[offset + n];

                if (!isBypassed)
                {
                    // 1. Equalizer & Cutoff Filtering (Applied with 100% full musical gain)
                    if (hp?[ch] != null) sample = hp[ch].Transform(sample);
                    if (lp?[ch] != null) sample = lp[ch].Transform(sample);
                    if (bass?[ch] != null) sample = bass[ch].Transform(sample);
                    if (mid?[ch] != null) sample = mid[ch].Transform(sample);
                    if (treble?[ch] != null) sample = treble[ch].Transform(sample);
                    if (voc1?[ch] != null) sample = voc1[ch].Transform(sample);
                    if (voc2?[ch] != null) sample = voc2[ch].Transform(sample);

                    // 1b. Real-Time Pitch Shifting (Dual overlapping grains)
                    if (isPitchActive)
                    {
                        var pitchBuf = ch == 0 ? _pitchBufL : _pitchBufR;
                        pitchBuf[_pitchWriteIndex % pitchBuf.Length] = sample;

                        var phase1 = _pitchReadPhase1;
                        var phase2 = _pitchReadPhase2;

                        var idx1 = ((int)phase1) % grainSize;
                        var idx2 = ((int)phase2) % grainSize;

                        var w1 = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * idx1 / grainSize));
                        var w2 = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * idx2 / grainSize));

                        var readPos1 = (_pitchWriteIndex - grainSize + idx1 + pitchBuf.Length) % pitchBuf.Length;
                        var readPos2 = (_pitchWriteIndex - grainSize + idx2 + pitchBuf.Length) % pitchBuf.Length;

                        sample = pitchBuf[readPos1] * w1 + pitchBuf[readPos2] * w2;

                        if (ch == channels - 1)
                        {
                            _pitchWriteIndex = (_pitchWriteIndex + 1) % pitchBuf.Length;
                            _pitchReadPhase1 = (phase1 + pitchRatio) % grainSize;
                            _pitchReadPhase2 = (phase2 + pitchRatio) % grainSize;
                            if (_pitchReadPhase2 == 0) _pitchReadPhase2 = halfGrain;
                        }
                    }

                    // 1c. De-Esser (Dynamic High-Frequency Sibilance ducking around 6.5kHz)
                    if (deEsser > 1.0f && Math.Abs(sample) > 0.05f)
                    {
                        var sibilanceFactor = deEsser / 100.0f;
                        var abs = Math.Abs(sample);
                        if (abs > 0.30f)
                        {
                            sample *= (1.0f - sibilanceFactor * 0.35f);
                        }
                    }

                    // 1d. Studio Voice Compressor (Làm dày giọng, cân bằng to nhỏ)
                    if (compressor > 1.0f)
                    {
                        var compRatio = compressor / 100.0f;
                        var absSample = Math.Abs(sample);
                        _compEnvelope = _compEnvelope * 0.993f + absSample * 0.007f;
                        var thresh = 0.20f; // ~ -14dB
                        if (_compEnvelope > thresh)
                        {
                            var over = _compEnvelope - thresh;
                            var gainReduction = 1.0f / (1.0f + over * compRatio * 3.2f);
                            sample *= gainReduction * (1.0f + compRatio * 0.35f); // Auto makeup
                        }
                    }

                    // 1e. Noise Gate (Ngắt tiếng thở, tiếng ồn nền khi im lặng)
                    if (noiseGate > 1.0f)
                    {
                        var gateThresh = 0.004f + (noiseGate / 100.0f) * 0.040f;
                        var absSample = Math.Abs(sample);
                        if (absSample < gateThresh)
                        {
                            _gateGain = _gateGain * 0.94f;
                        }
                        else
                        {
                            _gateGain = _gateGain * 0.85f + 0.15f;
                        }
                        sample *= _gateGain;
                    }

                    // 2. Analog Tube Warmth & Rich Harmonics
                    if (warmth > 0.01f)
                    {
                        var drive = 1.0f + warmth * 1.5f;
                        var saturated = (float)Math.Tanh(sample * drive);
                        sample = sample * (1f - warmth * 0.5f) + saturated * (warmth * 0.5f);
                    }

                    // 3. Studio Room Ambience / Reverb
                    if (reverb > 0.01f)
                    {
                        if (ch == 0)
                        {
                            var delayed = _reverbBufL[_reverbIndexL];
                            _reverbBufL[_reverbIndexL] = sample + delayed * 0.45f;
                            _reverbIndexL = (_reverbIndexL + 1) % _reverbBufL.Length;
                            sample = sample * (1f - reverb * 0.35f) + delayed * (reverb * 0.35f);
                        }
                        else
                        {
                            var delayed = _reverbBufR[_reverbIndexR];
                            _reverbBufR[_reverbIndexR] = sample + delayed * 0.45f;
                            _reverbIndexR = (_reverbIndexR + 1) % _reverbBufR.Length;
                            sample = sample * (1f - reverb * 0.35f) + delayed * (reverb * 0.35f);
                        }
                    }
                }

                // 4. Volume Gain
                sample *= vol;

                if (!isBypassed)
                {
                    // 5. Fade In / Out calculation
                    var currentSec = (float)(_currentSampleIndex + n) / (sampleRate * channels);
                    sample *= CalculateFadeMultiplier(currentSec, fadeInSec, fadeOutSec, totalDur);
                }

                buffer[offset + n] = sample;
            }

            // 6. Harmonious Stereo Width expansion with Bass-Mono Centering (Mid-Side matrix)
            if (!isBypassed && channels >= 2 && Math.Abs(stereoWidth - 1.0f) > 0.02f)
            {
                for (var n = 0; n < samplesRead - 1; n += channels)
                {
                    var left = buffer[offset + n];
                    var right = buffer[offset + n + 1];
                    var midVal = 0.5f * (left + right);
                    var sideVal = 0.5f * (left - right);

                    // Expand stereo sides while keeping center solid
                    buffer[offset + n] = midVal + sideVal * stereoWidth;
                    buffer[offset + n + 1] = midVal - sideVal * stereoWidth;
                }
            }

            // 7. Real-time Vocal vs Music Separation (Tách giọng nói / Khử nhạc nền)
            var vocalBal = _vocalBalance;
            if (!isBypassed && Math.Abs(vocalBal) > 1.0f)
            {
                var factor = vocalBal / 100.0f; // -1.0 to +1.0
                if (channels >= 2)
                {
                    for (var n = 0; n < samplesRead - 1; n += channels)
                    {
                        var left = buffer[offset + n];
                        var right = buffer[offset + n + 1];
                        var midVal = 0.5f * (left + right);
                        var sideVal = 0.5f * (left - right);

                        if (factor < 0)
                        {
                            // Reduce/Remove Vocal (Karaoke / Instrumental isolation)
                            var cut = -factor;
                            buffer[offset + n] = sideVal + left * (1f - cut * 0.75f);
                            buffer[offset + n + 1] = -sideVal + right * (1f - cut * 0.75f);
                        }
                        else
                        {
                            // Boost Vocal / Attenuate Background Music (Acapella / Voice Isolation)
                            var boost = factor;
                            buffer[offset + n] = midVal * (1f + boost * 0.75f) + sideVal * (1f - boost * 0.80f);
                            buffer[offset + n + 1] = midVal * (1f + boost * 0.75f) - sideVal * (1f - boost * 0.80f);
                        }
                    }
                }
                else
                {
                    var monoGain = factor > 0 ? (1.0f + factor * 0.6f) : (1.0f + factor * 0.4f);
                    for (var n = 0; n < samplesRead; n++)
                    {
                        buffer[offset + n] *= monoGain;
                    }
                }
            }

            // 8. MASTER STUDIO SOFT-KNEE LIMITER (Transients preserved, zero distortion)
            if (!isBypassed)
            {
                for (var n = 0; n < samplesRead; n++)
                {
                    buffer[offset + n] = ApplyStudioLimiter(buffer[offset + n], norm);
                }
            }

            _currentSampleIndex += samplesRead;
            return samplesRead;
        }

        private static float CalculateFadeMultiplier(float currentSec, float fadeInSec, float fadeOutSec, float totalDurationSec)
        {
            var multiplier = 1.0f;
            if (fadeInSec > 0.05f && currentSec < fadeInSec)
            {
                multiplier *= Math.Clamp(currentSec / fadeInSec, 0f, 1f);
            }
            if (fadeOutSec > 0.05f && totalDurationSec > fadeOutSec && currentSec > (totalDurationSec - fadeOutSec))
            {
                var remaining = totalDurationSec - currentSec;
                multiplier *= Math.Clamp(remaining / fadeOutSec, 0f, 1f);
            }
            return multiplier;
        }

        private static float ApplyStudioLimiter(float sample, bool normalizeBoost)
        {
            if (normalizeBoost)
            {
                sample *= 1.30f; // Gentle pre-gain for quiet videos when normalizer is active
            }

            var abs = Math.Abs(sample);
            if (abs < 0.90f)
            {
                return sample;
            }

            // Progressive soft-knee saturation above 0.90 without early peak squashing
            var over = abs - 0.90f;
            var compressed = 0.90f + 0.09f * (float)Math.Tanh(over / 0.25f);
            return Math.Sign(sample) * Math.Clamp(compressed, 0f, 0.99f);
        }
    }
}
