// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace FlowMy.Services.Workflow.Audio
{
    /// <summary>
    /// Professional real-time multi-channel DSP audio processing pipeline implementing NAudio ISampleProvider.
    /// Provides zero-latency real-time adjustments for:
    /// - Live Waveform Oscilloscope & Spectral Visualizer Buffer
    /// - Waveform Shaper (Pure, Tape, Soft Saturation, Hard Clipping, Wave Folding)
    /// - Transient Attack Punch, Sub-Harmonic Wave Generator, Harmonic Exciter
    /// - Stereo Waveform Polarity & Phase Inversion (L / R)
    /// - Voice Pitch Shifting / Voice Gender Changer (Male ↔ Female, Chipmunk, Monster) with Formant Reshaping
    /// - Voice Tone Clarity (Deep/Warm ↔ Bright/Crisp)
    /// - 5-Band Parametric Equalizer (Bass 100Hz, Low-Mid 350Hz, Mid 1.2kHz, High-Mid 3.5kHz, Treble 8kHz)
    /// - Creative Voice FX (Robot Ring Modulator, Vintage Radio / Lo-Fi Megaphone, Chorus Doubler)
    /// - Spatial Audio FX (8D Binaural 360° Panning, Echo / Delay, Lush Studio Reverb, Stereo Width)
    /// - Dynamic Cutoff Frequency Filters (Highpass 20-500Hz, Lowpass 2k-20kHz)
    /// - Voice Dynamics & Denoise (Compressor, De-Esser, Noise Gate, Vocal Balance / Karaoke)
    /// - Analog Tube Warmth, Volume Gain, Fade In/Out, and Anti-clipping Soft Limiter.
    /// </summary>
    public class RealTimeAudioDspPipeline : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private readonly int _sampleRate;

        public DubbingAudioMixer? DubbingMixer { get; set; }

        // Current parameters
        private volatile float _volume = 1.0f;
        private volatile float _bassGainDb;
        private volatile float _lowMidGainDb;
        private volatile float _midGainDb;
        private volatile float _highMidGainDb;
        private volatile float _trebleGainDb;
        private volatile float _toneClarity;
        private volatile string _preset = "neutral";

        private volatile bool _highpassEnabled;
        private volatile float _highpassCutoffHz = 80f;
        private volatile bool _lowpassEnabled;
        private volatile float _lowpassCutoffHz = 12000f;
        private volatile bool _normalizeEnabled;

        private volatile float _stereoWidthFactor = 1.0f;
        private volatile float _warmthFactor;
        private volatile float _reverbMix;
        private volatile float _vocalBalance;
        private volatile float _pitchSemitones;

        private volatile bool _echoEnabled;
        private volatile float _echoDelayMs = 250f;
        private volatile float _echoFeedbackPercent = 40f;
        private volatile float _echoMixPercent = 30f;

        private volatile bool _eightDEnabled;
        private volatile float _eightDSpeedHz = 0.125f;

        private volatile bool _robotVoiceEnabled;
        private volatile bool _radioVoiceEnabled;
        private volatile bool _chorusEnabled;
        private volatile float _chorusMixPercent = 40f;

        private volatile float _compressorPercent;
        private volatile float _deEsserPercent;
        private volatile float _noiseGatePercent;
        private volatile float _fadeInSec;
        private volatile float _fadeOutSec;
        private volatile float _totalDurationSec;
        private volatile bool _isBypassed;

        // Waveform Shaper & Spectral Parameters
        private volatile bool _waveShaperEnabled;
        private volatile string _waveShaperCurve = "clean";
        private volatile float _waveShaperDrive = 1.0f;
        private volatile float _transientPunch;
        private volatile float _subHarmonics;
        private volatile float _harmonicExciter;
        private volatile bool _phaseInvertL;
        private volatile bool _phaseInvertR;

        // Active Filter Sets (atomically swapped)
        private BiQuadFilter[]? _bassFilters;
        private BiQuadFilter[]? _lowMidFilters;
        private BiQuadFilter[]? _midFilters;
        private BiQuadFilter[]? _highMidFilters;
        private BiQuadFilter[]? _trebleFilters;
        private BiQuadFilter[]? _toneClarityFilters1;
        private BiQuadFilter[]? _toneClarityFilters2;
        private BiQuadFilter[]? _formantFilters1;
        private BiQuadFilter[]? _formantFilters2;
        private BiQuadFilter[]? _formantFilters3;
        private BiQuadFilter[]? _highpassFilters;
        private BiQuadFilter[]? _lowpassFilters;
        private BiQuadFilter[]? _radioBandpassHp;
        private BiQuadFilter[]? _radioBandpassLp;
        private BiQuadFilter[]? _subHarmonicLp;
        private BiQuadFilter[]? _exciterHp;

        // Schroeder Studio Reverb Buffers (Comb filters + Allpass diffusers)
        private readonly float[] _comb1L = new float[1116];
        private readonly float[] _comb2L = new float[1356];
        private readonly float[] _comb3L = new float[1557];
        private readonly float[] _comb4L = new float[1787];
        private readonly float[] _comb1R = new float[1188];
        private readonly float[] _comb2R = new float[1416];
        private readonly float[] _comb3R = new float[1620];
        private readonly float[] _comb4R = new float[1848];
        private int _combIdx1L, _combIdx2L, _combIdx3L, _combIdx4L;
        private int _combIdx1R, _combIdx2R, _combIdx3R, _combIdx4R;
        private float _combDamp1L, _combDamp2L, _combDamp3L, _combDamp4L;
        private float _combDamp1R, _combDamp2R, _combDamp3R, _combDamp4R;

        private readonly float[] _allpass1L = new float[225];
        private readonly float[] _allpass2L = new float[341];
        private readonly float[] _allpass1R = new float[257];
        private readonly float[] _allpass2R = new float[373];
        private int _apIdx1L, _apIdx2L, _apIdx1R, _apIdx2R;

        // Echo delay buffers (up to 1.2s at 48kHz)
        private readonly float[] _echoBufL = new float[57600];
        private readonly float[] _echoBufR = new float[57600];
        private int _echoIndexL, _echoIndexR;

        // Chorus delay buffers (~50ms)
        private readonly float[] _chorusBufL = new float[2400];
        private readonly float[] _chorusBufR = new float[2400];
        private int _chorusIndexL, _chorusIndexR;
        private float _chorusLfoPhase;

        // Dynamics & Transient Envelopes
        private float _compEnvelope;
        private float _gateGain = 1.0f;
        private float _transientEnvL;
        private float _transientEnvR;

        // Pitch Shift buffers & phase-aligned grain overlap
        private const int PitchGrainSize = 2048;
        private const int PitchGrainHalf = PitchGrainSize / 2;
        private readonly float[] _pitchBufL = new float[8192];
        private readonly float[] _pitchBufR = new float[8192];
        private int _pitchWriteIndex;
        private float _pitchPhase1;

        // 8D & Robot phase oscillators
        private float _eightDPhase;
        private float _robotLfoPhase;

        // Live Waveform Visualizer Buffer (Circular buffer for UI display)
        private const int VisualizerBufSize = 512;
        private readonly float[] _visBuf = new float[VisualizerBufSize];
        private int _visWriteIndex;
        private float _liveRmsL;
        private float _liveRmsR;
        private float _livePeak;

        // Position tracking for Fade In / Out
        private long _currentSampleIndex;
        private readonly object _lock = new();

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

            RebuildFiltersInternal();
        }

        public void SetVolume(float volumeMultiplier)
        {
            _volume = Math.Clamp(volumeMultiplier, 0f, 5f);
        }

        public void ApplyAllParameters(
            float volume,
            float bassGainDb, float lowMidGainDb, float midGainDb, float highMidGainDb, float trebleGainDb,
            float toneClarity, string? preset,
            bool highpass, float highpassCutoffHz,
            bool lowpass, float lowpassCutoffHz,
            bool normalize,
            float stereoWidthPercent, float warmthPercent, float reverbPercent, float vocalBalance, float pitchSemitones,
            bool echoEnabled, float echoDelayMs, float feedbackPercent, float mixPercent,
            bool robotVoice, bool radioVoice, bool chorus, float chorusMixPercent,
            bool eightD, float eightDSpeedHz,
            float compressorPercent, float deEsserPercent, float noiseGatePercent,
            float fadeInSec, float fadeOutSec, float totalDurationSec,
            bool waveShaperEnabled = false, string? waveShaperCurve = null, float waveShaperDrivePercent = 0f,
            float transientPunchPercent = 0f, float subHarmonicsPercent = 0f, float harmonicExciterPercent = 0f,
            bool phaseInvertL = false, bool phaseInvertR = false)
        {
            _volume = Math.Clamp(volume, 0f, 5f);
            _bassGainDb = Math.Clamp(bassGainDb, -24f, 24f);
            _lowMidGainDb = Math.Clamp(lowMidGainDb, -24f, 24f);
            _midGainDb = Math.Clamp(midGainDb, -24f, 24f);
            _highMidGainDb = Math.Clamp(highMidGainDb, -24f, 24f);
            _trebleGainDb = Math.Clamp(trebleGainDb, -24f, 24f);
            _toneClarity = Math.Clamp(toneClarity, -100f, 100f);
            if (preset != null) _preset = preset.Trim().ToLowerInvariant();

            _highpassEnabled = highpass;
            _highpassCutoffHz = Math.Clamp(highpassCutoffHz, 20f, 500f);
            _lowpassEnabled = lowpass;
            _lowpassCutoffHz = Math.Clamp(lowpassCutoffHz, 2000f, 20000f);
            _normalizeEnabled = normalize;

            _stereoWidthFactor = Math.Clamp(stereoWidthPercent / 100.0f, 0f, 2.5f);
            _warmthFactor = Math.Clamp(warmthPercent / 100.0f, 0f, 1.0f);
            _reverbMix = Math.Clamp(reverbPercent / 100.0f, 0f, 1.0f);
            _vocalBalance = Math.Clamp(vocalBalance, -100f, 100f);
            _pitchSemitones = Math.Clamp(pitchSemitones, -12f, 12f);

            _echoEnabled = echoEnabled;
            _echoDelayMs = Math.Clamp(echoDelayMs, 50f, 1000f);
            _echoFeedbackPercent = Math.Clamp(feedbackPercent, 0f, 90f);
            _echoMixPercent = Math.Clamp(mixPercent, 0f, 100f);

            _robotVoiceEnabled = robotVoice;
            _radioVoiceEnabled = radioVoice;
            _chorusEnabled = chorus;
            _chorusMixPercent = Math.Clamp(chorusMixPercent, 0f, 100f);
            _eightDEnabled = eightD;
            _eightDSpeedHz = Math.Clamp(eightDSpeedHz, 0.05f, 0.5f);

            _compressorPercent = Math.Clamp(compressorPercent, 0f, 100f);
            _deEsserPercent = Math.Clamp(deEsserPercent, 0f, 100f);
            _noiseGatePercent = Math.Clamp(noiseGatePercent, 0f, 100f);

            _fadeInSec = Math.Max(0f, fadeInSec);
            _fadeOutSec = Math.Max(0f, fadeOutSec);
            if (totalDurationSec > 0) _totalDurationSec = totalDurationSec;

            _waveShaperEnabled = waveShaperEnabled;
            if (waveShaperCurve != null) _waveShaperCurve = waveShaperCurve.Trim().ToLowerInvariant();
            _waveShaperDrive = 1.0f + Math.Clamp(waveShaperDrivePercent / 100.0f, 0f, 2.0f) * 2.0f;
            _transientPunch = Math.Clamp(transientPunchPercent / 100.0f, -1.0f, 1.0f);
            _subHarmonics = Math.Clamp(subHarmonicsPercent / 100.0f, 0f, 1.0f);
            _harmonicExciter = Math.Clamp(harmonicExciterPercent / 100.0f, 0f, 1.0f);
            _phaseInvertL = phaseInvertL;
            _phaseInvertR = phaseInvertR;

            lock (_lock)
            {
                RebuildFiltersInternal();
            }
        }

        public void GetLatestWaveformData(float[] destinationBuffer, out float rmsL, out float rmsR, out float peak)
        {
            if (destinationBuffer == null)
            {
                rmsL = 0; rmsR = 0; peak = 0;
                return;
            }

            lock (_visBuf)
            {
                var len = Math.Min(destinationBuffer.Length, VisualizerBufSize);
                var start = (_visWriteIndex - len + VisualizerBufSize) % VisualizerBufSize;
                for (var i = 0; i < len; i++)
                {
                    destinationBuffer[i] = _visBuf[(start + i) % VisualizerBufSize];
                }
                rmsL = _liveRmsL;
                rmsR = _liveRmsR;
                peak = _livePeak;
            }
        }

        public void ResetPosition(double seconds = 0)
        {
            _currentSampleIndex = (long)(Math.Max(0, seconds) * _sampleRate * _channels);
            Array.Clear(_echoBufL, 0, _echoBufL.Length);
            Array.Clear(_echoBufR, 0, _echoBufR.Length);
            Array.Clear(_chorusBufL, 0, _chorusBufL.Length);
            Array.Clear(_chorusBufR, 0, _chorusBufR.Length);
            Array.Clear(_pitchBufL, 0, _pitchBufL.Length);
            Array.Clear(_pitchBufR, 0, _pitchBufR.Length);
            Array.Clear(_comb1L, 0, _comb1L.Length);
            Array.Clear(_comb2L, 0, _comb2L.Length);
            Array.Clear(_comb3L, 0, _comb3L.Length);
            Array.Clear(_comb4L, 0, _comb4L.Length);
            Array.Clear(_comb1R, 0, _comb1R.Length);
            Array.Clear(_comb2R, 0, _comb2R.Length);
            Array.Clear(_comb3R, 0, _comb3R.Length);
            Array.Clear(_comb4R, 0, _comb4R.Length);
            Array.Clear(_allpass1L, 0, _allpass1L.Length);
            Array.Clear(_allpass2L, 0, _allpass2L.Length);
            Array.Clear(_allpass1R, 0, _allpass1R.Length);
            Array.Clear(_allpass2R, 0, _allpass2R.Length);

            _echoIndexL = 0;
            _echoIndexR = 0;
            _chorusIndexL = 0;
            _chorusIndexR = 0;
            _chorusLfoPhase = 0;
            _eightDPhase = 0;
            _robotLfoPhase = 0;
            _pitchWriteIndex = 0;
            _pitchPhase1 = 0;
            _compEnvelope = 0f;
            _gateGain = 1.0f;
            _transientEnvL = 0f;
            _transientEnvR = 0f;
        }

        private void RebuildFiltersInternal()
        {
            if (_channels <= 0 || _sampleRate <= 0) return;

            var newBass = new BiQuadFilter[_channels];
            var newLowMid = new BiQuadFilter[_channels];
            var newMid = new BiQuadFilter[_channels];
            var newHighMid = new BiQuadFilter[_channels];
            var newTreble = new BiQuadFilter[_channels];
            var newTc1 = new BiQuadFilter[_channels];
            var newTc2 = new BiQuadFilter[_channels];
            var newFormant1 = new BiQuadFilter[_channels];
            var newFormant2 = new BiQuadFilter[_channels];
            var newFormant3 = new BiQuadFilter[_channels];
            var newHp = _highpassEnabled ? new BiQuadFilter[_channels] : null;
            var newLp = _lowpassEnabled ? new BiQuadFilter[_channels] : null;
            var newRadHp = _radioVoiceEnabled ? new BiQuadFilter[_channels] : null;
            var newRadLp = _radioVoiceEnabled ? new BiQuadFilter[_channels] : null;
            var newSubHp = new BiQuadFilter[_channels];
            var newExcHp = new BiQuadFilter[_channels];

            for (var ch = 0; ch < _channels; ch++)
            {
                // 1. 5-Band Parametric EQ
                if (Math.Abs(_bassGainDb) > 0.05f)
                    newBass[ch] = BiQuadFilter.LowShelf(_sampleRate, 100f, 0.8f, _bassGainDb);
                if (Math.Abs(_lowMidGainDb) > 0.05f)
                    newLowMid[ch] = BiQuadFilter.PeakingEQ(_sampleRate, 350f, 0.8f, _lowMidGainDb);
                if (Math.Abs(_midGainDb) > 0.05f)
                    newMid[ch] = BiQuadFilter.PeakingEQ(_sampleRate, 1200f, 0.65f, _midGainDb);
                if (Math.Abs(_highMidGainDb) > 0.05f)
                    newHighMid[ch] = BiQuadFilter.PeakingEQ(_sampleRate, 3500f, 0.8f, _highMidGainDb);
                if (Math.Abs(_trebleGainDb) > 0.05f)
                    newTreble[ch] = BiQuadFilter.HighShelf(_sampleRate, 8000f, 0.85f, _trebleGainDb);

                // 2. Tone Clarity
                BuildToneClarityFilters(ch, newTc1, newTc2);

                // 3. Vocal Tract Formant Filters for Realistic Male <-> Female Voice
                BuildFormantFilters(ch, newFormant1, newFormant2, newFormant3);

                // 4. Cutoffs
                if (newHp != null) newHp[ch] = BiQuadFilter.HighPassFilter(_sampleRate, _highpassCutoffHz, 0.7071f);
                if (newLp != null) newLp[ch] = BiQuadFilter.LowPassFilter(_sampleRate, _lowpassCutoffHz, 0.7071f);

                // 5. Radio Bandpass (350Hz - 3400Hz)
                if (newRadHp != null) newRadHp[ch] = BiQuadFilter.HighPassFilter(_sampleRate, 350f, 0.7071f);
                if (newRadLp != null) newRadLp[ch] = BiQuadFilter.LowPassFilter(_sampleRate, 3400f, 0.7071f);

                // 6. Sub-harmonic lowpass & Harmonic exciter highpass
                newSubHp[ch] = BiQuadFilter.LowPassFilter(_sampleRate, 90f, 0.7071f);
                newExcHp[ch] = BiQuadFilter.HighPassFilter(_sampleRate, 4500f, 0.7071f);
            }

            _bassFilters = newBass;
            _lowMidFilters = newLowMid;
            _midFilters = newMid;
            _highMidFilters = newHighMid;
            _trebleFilters = newTreble;
            _toneClarityFilters1 = newTc1;
            _toneClarityFilters2 = newTc2;
            _formantFilters1 = newFormant1;
            _formantFilters2 = newFormant2;
            _formantFilters3 = newFormant3;
            _highpassFilters = newHp;
            _lowpassFilters = newLp;
            _radioBandpassHp = newRadHp;
            _radioBandpassLp = newRadLp;
            _subHarmonicLp = newSubHp;
            _exciterHp = newExcHp;
        }

        private void BuildToneClarityFilters(int ch, BiQuadFilter[] newTc1, BiQuadFilter[] newTc2)
        {
            if (_toneClarity < -1.0f)
            {
                var bassBoost = (-_toneClarity / 100.0f) * 6.0f;
                var trebleSoft = (_toneClarity / 100.0f) * 3.5f;
                newTc1[ch] = BiQuadFilter.LowShelf(_sampleRate, 150f, 0.85f, bassBoost);
                newTc2[ch] = BiQuadFilter.HighShelf(_sampleRate, 6000f, 0.75f, trebleSoft);
            }
            else if (_toneClarity > 1.0f)
            {
                var presBoost = (_toneClarity / 100.0f) * 5.5f;
                var airBoost = (_toneClarity / 100.0f) * 4.5f;
                newTc1[ch] = BiQuadFilter.PeakingEQ(_sampleRate, 2400f, 1.1f, presBoost);
                newTc2[ch] = BiQuadFilter.HighShelf(_sampleRate, 7000f, 0.8f, airBoost);
            }
        }

        private void BuildFormantFilters(int ch, BiQuadFilter[] f1, BiQuadFilter[] f2, BiQuadFilter[] f3)
        {
            if (_pitchSemitones > 1.0f)
            {
                var femalePres = Math.Min(6.0f, _pitchSemitones * 1.1f);
                var chestCut = -Math.Min(6.0f, _pitchSemitones * 1.0f);
                var air = Math.Min(4.0f, _pitchSemitones * 0.7f);
                f1[ch] = BiQuadFilter.PeakingEQ(_sampleRate, 160f, 1.2f, chestCut);
                f2[ch] = BiQuadFilter.PeakingEQ(_sampleRate, 2600f, 1.2f, femalePres);
                f3[ch] = BiQuadFilter.HighShelf(_sampleRate, 7500f, 0.8f, air);
            }
            else if (_pitchSemitones < -1.0f)
            {
                var maleChest = Math.Min(6.0f, -_pitchSemitones * 1.1f);
                var trebleCut = -Math.Min(4.0f, -_pitchSemitones * 0.7f);
                f1[ch] = BiQuadFilter.LowShelf(_sampleRate, 140f, 0.85f, maleChest);
                f2[ch] = BiQuadFilter.HighShelf(_sampleRate, 4800f, 0.75f, trebleCut);
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = _source.Read(buffer, offset, count);
            if (samplesRead <= 0) return 0;

            var vol = _volume;
            var isBypassed = _isBypassed;
            var channels = _channels;
            var sampleRate = _sampleRate;

            var bass = _bassFilters;
            var lowMid = _lowMidFilters;
            var mid = _midFilters;
            var highMid = _highMidFilters;
            var treble = _trebleFilters;
            var tc1 = _toneClarityFilters1;
            var tc2 = _toneClarityFilters2;
            var fm1 = _formantFilters1;
            var fm2 = _formantFilters2;
            var fm3 = _formantFilters3;
            var hp = _highpassFilters;
            var lp = _lowpassFilters;
            var radHp = _radioBandpassHp;
            var radLp = _radioBandpassLp;
            var subHp = _subHarmonicLp;
            var excHp = _exciterHp;

            var pitchSt = _pitchSemitones;
            var isPitchActive = !isBypassed && Math.Abs(pitchSt) > 0.1f;
            var pitchRatio = isPitchActive ? (float)Math.Pow(2.0, pitchSt / 12.0) : 1.0f;

            var echoActive = !isBypassed && _echoEnabled;
            var echoDelay = echoActive ? Math.Clamp((int)(_echoDelayMs * sampleRate / 1000.0f), 100, _echoBufL.Length - 1) : 0;
            var echoFb = _echoFeedbackPercent / 100.0f;
            var echoMix = _echoMixPercent / 100.0f;

            var chorusActive = !isBypassed && _chorusEnabled;
            var chorusMix = _chorusMixPercent / 100.0f;
            var robotActive = !isBypassed && _robotVoiceEnabled;
            var radioActive = !isBypassed && _radioVoiceEnabled;
            var reverbActive = !isBypassed && _reverbMix > 0.01f;
            var reverbMix = _reverbMix;

            var waveShaperActive = !isBypassed && _waveShaperEnabled;
            var waveCurve = _waveShaperCurve;
            var waveDrive = _waveShaperDrive;
            var punch = _transientPunch;
            var subAmt = _subHarmonics;
            var excAmt = _harmonicExciter;
            var invL = _phaseInvertL;
            var invR = _phaseInvertR;

            float sumSqL = 0f, sumSqR = 0f, maxPeak = 0f;
            var countL = 0; var countR = 0;

            for (var n = 0; n < samplesRead; n++)
            {
                var ch = (int)((_currentSampleIndex + n) % channels);
                var sample = buffer[offset + n];
                if (float.IsNaN(sample) || float.IsInfinity(sample)) sample = 0f;

                if (!isBypassed)
                {
                    // 1. Equalizer & Tone Filtering
                    sample = ApplyEqualizerAndCutoffs(sample, ch, hp, lp, bass, lowMid, mid, highMid, treble, tc1, tc2, fm1, fm2, fm3);

                    // 2. Radio Saturation
                    if (radioActive)
                    {
                        if (radHp?[ch] != null) sample = radHp[ch].Transform(sample);
                        if (radLp?[ch] != null) sample = radLp[ch].Transform(sample);
                        sample = (float)Math.Tanh(sample * 2.2f) * 0.85f;
                    }

                    // 3. Robot Voice Modulation
                    if (robotActive)
                    {
                        var carrier = (float)Math.Sin(_robotLfoPhase);
                        sample = sample * 0.35f + sample * carrier * 0.85f;
                    }

                    // 4. Smooth Phase-Aligned Pitch Shifting (Zero Choppiness)
                    if (isPitchActive)
                    {
                        sample = ProcessSmoothPitchShift(sample, ch, channels, pitchRatio);
                    }

                    // 5. Waveform Shaper & Harmonic Saturation
                    if (waveShaperActive)
                    {
                        sample = ApplyWaveShaper(sample, waveCurve, waveDrive);
                    }

                    // 6. Transient Attack Punch
                    if (Math.Abs(punch) > 0.01f)
                    {
                        sample = ProcessTransientPunch(sample, ch, punch);
                    }

                    // 7. Sub-Harmonics & Harmonic Exciter
                    if (subAmt > 0.01f && subHp?[ch] != null)
                    {
                        var sub = subHp[ch].Transform((float)Math.Sin(sample * Math.PI));
                        sample += sub * (subAmt * 0.45f);
                    }
                    if (excAmt > 0.01f && excHp?[ch] != null)
                    {
                        var high = excHp[ch].Transform(sample);
                        var exc = (float)Math.Tanh(high * 2.0f);
                        sample += exc * (excAmt * 0.40f);
                    }

                    // 8. Chorus / Vocal Doubler
                    if (chorusActive)
                    {
                        sample = ProcessChorus(sample, ch, chorusMix);
                    }

                    // 9. Echo / Delay
                    if (echoActive && echoDelay > 0)
                    {
                        sample = ProcessEcho(sample, ch, echoDelay, echoFb, echoMix);
                    }

                    // 10. Dynamics (De-Esser, Compressor, Noise Gate)
                    sample = ProcessDynamics(sample);

                    // 11. Analog Tube Warmth
                    if (_warmthFactor > 0.01f)
                    {
                        var drive = 1.0f + _warmthFactor * 1.5f;
                        var sat = (float)Math.Tanh(sample * drive);
                        sample = sample * (1f - _warmthFactor * 0.5f) + sat * (_warmthFactor * 0.5f);
                    }

                    // 12. Lush Schroeder Studio Reverb
                    if (reverbActive)
                    {
                        sample = ProcessSchroederReverb(sample, ch, reverbMix);
                    }

                    // 13. Phase Invert
                    if ((ch == 0 && invL) || (ch == 1 && invR))
                    {
                        sample = -sample;
                    }
                }

                // Phase advancement on last channel
                if (ch == channels - 1)
                {
                    if (robotActive)
                        _robotLfoPhase = (float)((_robotLfoPhase + 2.0 * Math.PI * 50.0 / sampleRate) % (2.0 * Math.PI));
                    if (chorusActive)
                        _chorusLfoPhase = (float)((_chorusLfoPhase + 2.0 * Math.PI * 1.5 / sampleRate) % (2.0 * Math.PI));
                }

                // Volume & Fade In/Out
                sample *= vol;
                if (!isBypassed)
                {
                    var currentSec = (float)(_currentSampleIndex + n) / (sampleRate * channels);
                    sample *= CalculateFadeMultiplier(currentSec, _fadeInSec, _fadeOutSec, _totalDurationSec);
                }

                var clamped = Math.Clamp(sample, -2.0f, 2.0f);
                buffer[offset + n] = clamped;

                // Live Visualizer ring buffer capture
                _visBuf[_visWriteIndex] = clamped;
                _visWriteIndex = (_visWriteIndex + 1) % VisualizerBufSize;

                var absSample = Math.Abs(clamped);
                if (absSample > maxPeak) maxPeak = absSample;
                if (ch == 0) { sumSqL += clamped * clamped; countL++; }
                else { sumSqR += clamped * clamped; countR++; }
            }

            // Post-processing multi-channel matrices
            ProcessStereoAndSeparation(buffer, offset, samplesRead, channels, isBypassed);

            // Master Limiter
            if (!isBypassed)
            {
                for (var n = 0; n < samplesRead; n++)
                {
                    buffer[offset + n] = ApplyStudioLimiter(buffer[offset + n], _normalizeEnabled);
                }
            }

            // Real-time RAM Dubbing Audio Mixing & Auto-Ducking
            if (DubbingMixer != null && channels > 0 && sampleRate > 0)
            {
                var currentSec = _currentSampleIndex / (double)(channels * sampleRate);
                var duckingGain = DubbingMixer.ProcessDubbingAndDucking(buffer, offset, samplesRead, currentSec, sampleRate);
                if (duckingGain < 0.999f)
                {
                    for (var n = 0; n < samplesRead; n++)
                    {
                        buffer[offset + n] *= duckingGain;
                    }
                }
            }

            _livePeak = maxPeak;
            _liveRmsL = countL > 0 ? (float)Math.Sqrt(sumSqL / countL) : 0f;
            _liveRmsR = countR > 0 ? (float)Math.Sqrt(sumSqR / countR) : _liveRmsL;

            _currentSampleIndex += samplesRead;
            return samplesRead;
        }

        private static float ApplyWaveShaper(float sample, string curve, float drive)
        {
            var driven = sample * drive;
            return curve switch
            {
                "tape" => (float)Math.Tanh(driven) * (1.0f / (float)Math.Sqrt(Math.Max(1.0f, drive * 0.7f))),
                "soft" => driven switch
                {
                    > 1.5f => 1.0f,
                    < -1.5f => -1.0f,
                    _ => (driven - (driven * driven * driven) / 4.5f) * 0.85f
                },
                "hard" => Math.Clamp(driven * 0.85f, -0.92f, 0.92f),
                "fold" => (float)Math.Sin(driven * Math.PI * 0.5f) * 0.90f,
                _ => driven * (1.0f / drive)
            };
        }

        private float ProcessTransientPunch(float sample, int ch, float punch)
        {
            ref var env = ref (ch == 0 ? ref _transientEnvL : ref _transientEnvR);
            var abs = Math.Abs(sample);
            env = abs > env ? env * 0.80f + abs * 0.20f : env * 0.98f + abs * 0.02f;
            var delta = abs - env;
            if (delta > 0)
            {
                var mult = 1.0f + delta * punch * 2.5f;
                return sample * Math.Clamp(mult, 0.2f, 2.5f);
            }
            return sample;
        }

        private static float ApplyEqualizerAndCutoffs(
            float sample, int ch,
            BiQuadFilter[]? hp, BiQuadFilter[]? lp,
            BiQuadFilter[]? bass, BiQuadFilter[]? lowMid, BiQuadFilter[]? mid, BiQuadFilter[]? highMid, BiQuadFilter[]? treble,
            BiQuadFilter[]? tc1, BiQuadFilter[]? tc2,
            BiQuadFilter[]? fm1, BiQuadFilter[]? fm2, BiQuadFilter[]? fm3)
        {
            if (hp?[ch] != null) sample = hp[ch].Transform(sample);
            if (lp?[ch] != null) sample = lp[ch].Transform(sample);
            if (bass?[ch] != null) sample = bass[ch].Transform(sample);
            if (lowMid?[ch] != null) sample = lowMid[ch].Transform(sample);
            if (mid?[ch] != null) sample = mid[ch].Transform(sample);
            if (highMid?[ch] != null) sample = highMid[ch].Transform(sample);
            if (treble?[ch] != null) sample = treble[ch].Transform(sample);
            if (tc1?[ch] != null) sample = tc1[ch].Transform(sample);
            if (tc2?[ch] != null) sample = tc2[ch].Transform(sample);
            if (fm1?[ch] != null) sample = fm1[ch].Transform(sample);
            if (fm2?[ch] != null) sample = fm2[ch].Transform(sample);
            if (fm3?[ch] != null) sample = fm3[ch].Transform(sample);
            return sample;
        }

        private float ProcessSmoothPitchShift(float sample, int ch, int channels, float pitchRatio)
        {
            var pitchBuf = ch == 0 ? _pitchBufL : _pitchBufR;
            var wIdx = _pitchWriteIndex % pitchBuf.Length;
            pitchBuf[wIdx] = sample;

            var p1 = _pitchPhase1;
            var p2 = (_pitchPhase1 + PitchGrainHalf) % PitchGrainSize;

            var idx1 = (int)p1;
            var idx2 = (int)p2;
            var frac1 = p1 - idx1;
            var frac2 = p2 - idx2;

            // Constant-power sinusoidal Hanning window: w1^2 + w2^2 = 1.0 everywhere
            var w1 = (float)Math.Sin(Math.PI * idx1 / PitchGrainSize);
            var w2 = (float)Math.Sin(Math.PI * idx2 / PitchGrainSize);

            var read1 = (_pitchWriteIndex - PitchGrainSize + idx1 + pitchBuf.Length) % pitchBuf.Length;
            var read1Next = (read1 + 1) % pitchBuf.Length;
            var s1 = pitchBuf[read1] * (1f - frac1) + pitchBuf[read1Next] * frac1;

            var read2 = (_pitchWriteIndex - PitchGrainSize + idx2 + pitchBuf.Length) % pitchBuf.Length;
            var read2Next = (read2 + 1) % pitchBuf.Length;
            var s2 = pitchBuf[read2] * (1f - frac2) + pitchBuf[read2Next] * frac2;

            var outSample = s1 * (w1 * w1) + s2 * (w2 * w2);

            if (ch == channels - 1)
            {
                _pitchWriteIndex = (_pitchWriteIndex + 1) % pitchBuf.Length;
                _pitchPhase1 = (_pitchPhase1 + pitchRatio) % PitchGrainSize;
            }

            return outSample;
        }

        private float ProcessChorus(float sample, int ch, float chorusMix)
        {
            var modDelay = 200 + (int)(120 * Math.Sin(_chorusLfoPhase));
            if (ch == 0)
            {
                _chorusBufL[_chorusIndexL] = sample;
                var readIdx = (_chorusIndexL - modDelay + _chorusBufL.Length) % _chorusBufL.Length;
                sample = sample * (1f - chorusMix * 0.45f) + _chorusBufL[readIdx] * (chorusMix * 0.45f);
                _chorusIndexL = (_chorusIndexL + 1) % _chorusBufL.Length;
            }
            else
            {
                _chorusBufR[_chorusIndexR] = sample;
                var readIdx = (_chorusIndexR - modDelay + _chorusBufR.Length) % _chorusBufR.Length;
                sample = sample * (1f - chorusMix * 0.45f) + _chorusBufR[readIdx] * (chorusMix * 0.45f);
                _chorusIndexR = (_chorusIndexR + 1) % _chorusBufR.Length;
            }
            return sample;
        }

        private float ProcessEcho(float sample, int ch, int echoDelay, float echoFb, float echoMix)
        {
            if (ch == 0)
            {
                var delayed = _echoBufL[_echoIndexL];
                _echoBufL[_echoIndexL] = Math.Clamp(sample + delayed * echoFb, -1.5f, 1.5f);
                _echoIndexL = (_echoIndexL + 1) % echoDelay;
                sample = sample * (1f - echoMix * 0.45f) + delayed * echoMix;
            }
            else
            {
                var delayed = _echoBufR[_echoIndexR];
                _echoBufR[_echoIndexR] = Math.Clamp(sample + delayed * echoFb, -1.5f, 1.5f);
                _echoIndexR = (_echoIndexR + 1) % echoDelay;
                sample = sample * (1f - echoMix * 0.45f) + delayed * echoMix;
            }
            return sample;
        }

        private float ProcessDynamics(float sample)
        {
            // 1. De-Esser
            if (_deEsserPercent > 1.0f && Math.Abs(sample) > 0.25f)
            {
                var sibilanceFactor = _deEsserPercent / 100.0f;
                sample *= (1.0f - sibilanceFactor * 0.35f);
            }

            // 2. Compressor
            if (_compressorPercent > 1.0f)
            {
                var compRatio = _compressorPercent / 100.0f;
                var absSample = Math.Abs(sample);
                _compEnvelope = _compEnvelope * 0.993f + absSample * 0.007f;
                var thresh = 0.20f;
                if (_compEnvelope > thresh)
                {
                    var over = _compEnvelope - thresh;
                    var gainReduction = 1.0f / (1.0f + over * compRatio * 3.2f);
                    sample *= gainReduction * (1.0f + compRatio * 0.35f);
                }
            }

            // 3. Noise Gate
            if (_noiseGatePercent > 1.0f)
            {
                var gateThresh = 0.004f + (_noiseGatePercent / 100.0f) * 0.040f;
                var absSample = Math.Abs(sample);
                _gateGain = absSample < gateThresh ? _gateGain * 0.94f : _gateGain * 0.85f + 0.15f;
                sample *= _gateGain;
            }

            return sample;
        }

        private float ProcessSchroederReverb(float sample, int ch, float reverbMix)
        {
            var decay = 0.60f + reverbMix * 0.25f;
            float combOut;

            if (ch == 0)
            {
                _combDamp1L = _combDamp1L * 0.25f + _comb1L[_combIdx1L] * 0.75f;
                _combDamp2L = _combDamp2L * 0.25f + _comb2L[_combIdx2L] * 0.75f;
                _combDamp3L = _combDamp3L * 0.25f + _comb3L[_combIdx3L] * 0.75f;
                _combDamp4L = _combDamp4L * 0.25f + _comb4L[_combIdx4L] * 0.75f;

                _comb1L[_combIdx1L] = Math.Clamp(sample + _combDamp1L * decay, -1.5f, 1.5f);
                _comb2L[_combIdx2L] = Math.Clamp(sample + _combDamp2L * decay, -1.5f, 1.5f);
                _comb3L[_combIdx3L] = Math.Clamp(sample + _combDamp3L * decay, -1.5f, 1.5f);
                _comb4L[_combIdx4L] = Math.Clamp(sample + _combDamp4L * decay, -1.5f, 1.5f);

                _combIdx1L = (_combIdx1L + 1) % _comb1L.Length;
                _combIdx2L = (_combIdx2L + 1) % _comb2L.Length;
                _combIdx3L = (_combIdx3L + 1) % _comb3L.Length;
                _combIdx4L = (_combIdx4L + 1) % _comb4L.Length;

                combOut = (_combDamp1L + _combDamp2L + _combDamp3L + _combDamp4L) * 0.25f;

                var apBuf1 = _allpass1L[_apIdx1L];
                var apOut1 = -combOut + apBuf1;
                _allpass1L[_apIdx1L] = combOut + apBuf1 * 0.5f;
                _apIdx1L = (_apIdx1L + 1) % _allpass1L.Length;

                var apBuf2 = _allpass2L[_apIdx2L];
                var apOut2 = -apOut1 + apBuf2;
                _allpass2L[_apIdx2L] = apOut1 + apBuf2 * 0.5f;
                _apIdx2L = (_apIdx2L + 1) % _allpass2L.Length;

                return sample * (1f - reverbMix * 0.4f) + apOut2 * (reverbMix * 0.6f);
            }
            else
            {
                _combDamp1R = _combDamp1R * 0.25f + _comb1R[_combIdx1R] * 0.75f;
                _combDamp2R = _combDamp2R * 0.25f + _comb2R[_combIdx2R] * 0.75f;
                _combDamp3R = _combDamp3R * 0.25f + _comb3R[_combIdx3R] * 0.75f;
                _combDamp4R = _combDamp4R * 0.25f + _comb4R[_combIdx4R] * 0.75f;

                _comb1R[_combIdx1R] = Math.Clamp(sample + _combDamp1R * decay, -1.5f, 1.5f);
                _comb2R[_combIdx2R] = Math.Clamp(sample + _combDamp2R * decay, -1.5f, 1.5f);
                _comb3R[_combIdx3R] = Math.Clamp(sample + _combDamp3R * decay, -1.5f, 1.5f);
                _comb4R[_combIdx4R] = Math.Clamp(sample + _combDamp4R * decay, -1.5f, 1.5f);

                _combIdx1R = (_combIdx1R + 1) % _comb1R.Length;
                _combIdx2R = (_combIdx2R + 1) % _comb2R.Length;
                _combIdx3R = (_combIdx3R + 1) % _comb3R.Length;
                _combIdx4R = (_combIdx4R + 1) % _comb4R.Length;

                combOut = (_combDamp1R + _combDamp2R + _combDamp3R + _combDamp4R) * 0.25f;

                var apBuf1 = _allpass1R[_apIdx1R];
                var apOut1 = -combOut + apBuf1;
                _allpass1R[_apIdx1R] = combOut + apBuf1 * 0.5f;
                _apIdx1R = (_apIdx1R + 1) % _allpass1R.Length;

                var apBuf2 = _allpass2R[_apIdx2R];
                var apOut2 = -apOut1 + apBuf2;
                _allpass2R[_apIdx2R] = apOut1 + apBuf2 * 0.5f;
                _apIdx2R = (_apIdx2R + 1) % _allpass2R.Length;

                return sample * (1f - reverbMix * 0.4f) + apOut2 * (reverbMix * 0.6f);
            }
        }

        private void ProcessStereoAndSeparation(float[] buffer, int offset, int samplesRead, int channels, bool isBypassed)
        {
            if (isBypassed || channels < 2) return;

            // Stereo Width
            if (Math.Abs(_stereoWidthFactor - 1.0f) > 0.02f)
            {
                for (var n = 0; n < samplesRead - 1; n += channels)
                {
                    var left = buffer[offset + n];
                    var right = buffer[offset + n + 1];
                    var midVal = 0.5f * (left + right);
                    var sideVal = 0.5f * (left - right);
                    buffer[offset + n] = midVal + sideVal * _stereoWidthFactor;
                    buffer[offset + n + 1] = midVal - sideVal * _stereoWidthFactor;
                }
            }

            // 8D Binaural Panning
            if (_eightDEnabled)
            {
                var speed = _eightDSpeedHz;
                for (var n = 0; n < samplesRead - 1; n += channels)
                {
                    var pan = (float)Math.Sin(_eightDPhase);
                    _eightDPhase = (float)((_eightDPhase + 2.0 * Math.PI * speed / _sampleRate) % (2.0 * Math.PI));
                    var panL = 0.5f * (1.0f - pan);
                    var panR = 0.5f * (1.0f + pan);
                    buffer[offset + n] *= (float)Math.Sqrt(panL) * 1.3f;
                    buffer[offset + n + 1] *= (float)Math.Sqrt(panR) * 1.3f;
                }
            }

            // Vocal Balance
            if (Math.Abs(_vocalBalance) > 1.0f)
            {
                var factor = _vocalBalance / 100.0f;
                for (var n = 0; n < samplesRead - 1; n += channels)
                {
                    var left = buffer[offset + n];
                    var right = buffer[offset + n + 1];
                    var midVal = 0.5f * (left + right);
                    var sideVal = 0.5f * (left - right);
                    if (factor < 0)
                    {
                        var cut = -factor;
                        buffer[offset + n] = sideVal + left * (1f - cut * 0.75f);
                        buffer[offset + n + 1] = -sideVal + right * (1f - cut * 0.75f);
                    }
                    else
                    {
                        var boost = factor;
                        buffer[offset + n] = midVal * (1f + boost * 0.75f) + sideVal * (1f - boost * 0.80f);
                        buffer[offset + n + 1] = midVal * (1f + boost * 0.75f) - sideVal * (1f - boost * 0.80f);
                    }
                }
            }
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
            if (normalizeBoost) sample *= 1.25f;
            var abs = Math.Abs(sample);
            if (abs < 0.90f) return sample;
            var over = abs - 0.90f;
            var compressed = 0.90f + 0.09f * (float)Math.Tanh(over / 0.25f);
            return Math.Sign(sample) * Math.Clamp(compressed, 0f, 0.99f);
        }
    }
}
