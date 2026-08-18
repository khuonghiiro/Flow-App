// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Workflow.Audio
{
    /// <summary>
    /// Builds robust, high-fidelity FFmpeg audio filter chains for VideoProcessingNode.
    /// Handles EQ, tone adjustments, tempo/speed without pitch drift, EBU R128 loudness normalization,
    /// high/low pass filters, denoise, trimming, and multi-track mixing.
    /// </summary>
    public static class VideoAudioFilterGraphBuilder
    {
        public static string BuildSourceAudioFilterGraph(VideoProcessingNode node, double duration, bool applyTrim = false)
        {
            var filters = new List<string>();

            // 1. Independent Audio Trim (if requested)
            if (applyTrim && node.AudioTrimEnabled && node.AudioTrimEndSec > node.AudioTrimStartSec)
            {
                var start = node.AudioTrimStartSec.ToString("0.###", CultureInfo.InvariantCulture);
                var end = node.AudioTrimEndSec.ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"atrim={start}:{end},asetpts=PTS-STARTPTS");
            }

            // 2. Volume & Gain scaling
            if (Math.Abs(node.SourceAudioVolumePercent - 100.0) > 0.1)
            {
                var vol = (Math.Max(0, node.SourceAudioVolumePercent) / 100.0).ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"volume={vol}");
            }

            // 3. Audio Speed / Tempo (keeps voice pitch intact)
            if (Math.Abs(node.AudioSpeedFactor - 1.0) > 0.01)
            {
                var atempo = BuildAtempoChain(node.AudioSpeedFactor);
                if (!string.IsNullOrWhiteSpace(atempo))
                    filters.Add(atempo);
            }

            // =========================================================================
            // MODULE 2: VOICE CHANGER & WAVE SHAPER (Only when enabled)
            // =========================================================================
            if (node.VoiceChangerEnabled)
            {
                // Voice Pitch Shifter (Voice Gender Changer: Male ↔ Female with Formant Compensation)
                if (Math.Abs(node.AudioPitchSemitones) > 0.1)
                {
                    var pitchRatio = Math.Pow(2.0, node.AudioPitchSemitones / 12.0);
                    var rate = (48000.0 * pitchRatio).ToString("0.###", CultureInfo.InvariantCulture);
                    var tempoChain = BuildAtempoChain(1.0 / pitchRatio);
                    filters.Add($"asetrate={rate},aresample=48000,{tempoChain}");

                    if (node.AudioPitchSemitones > 1.0)
                    {
                        var femalePres = Math.Min(6.0, node.AudioPitchSemitones * 1.1).ToString("0.#", CultureInfo.InvariantCulture);
                        var chestCut = (-Math.Min(6.0, node.AudioPitchSemitones * 1.0)).ToString("0.#", CultureInfo.InvariantCulture);
                        var air = Math.Min(4.0, node.AudioPitchSemitones * 0.7).ToString("0.#", CultureInfo.InvariantCulture);
                        filters.Add($"equalizer=f=160:t=q:w=1.2:g={chestCut},equalizer=f=2600:t=q:w=1.2:g={femalePres},treble=g={air}:f=7500:w=0.8");
                    }
                    else if (node.AudioPitchSemitones < -1.0)
                    {
                        var maleChest = Math.Min(6.0, -node.AudioPitchSemitones * 1.1).ToString("0.#", CultureInfo.InvariantCulture);
                        var trebleCut = (-Math.Min(4.0, -node.AudioPitchSemitones * 0.7)).ToString("0.#", CultureInfo.InvariantCulture);
                        filters.Add($"equalizer=f=140:t=q:w=1.2:g={maleChest},treble=g={trebleCut}:f=4800:w=0.75");
                    }
                }

                // Voice Tone Clarity (Trầm ấm ↔ Trong trẻo)
                if (node.AudioToneClarity < -2.0)
                {
                    var bassBoost = (-node.AudioToneClarity / 100.0 * 6.0).ToString("0.#", CultureInfo.InvariantCulture);
                    var trebleSoft = (node.AudioToneClarity / 100.0 * 3.5).ToString("0.#", CultureInfo.InvariantCulture);
                    filters.Add($"bass=g={bassBoost}:f=180:w=0.8");
                    filters.Add($"treble=g={trebleSoft}:f=6000:w=0.7");
                }
                else if (node.AudioToneClarity > 2.0)
                {
                    var pres = (node.AudioToneClarity / 100.0 * 5.0).ToString("0.#", CultureInfo.InvariantCulture);
                    var air = (node.AudioToneClarity / 100.0 * 4.0).ToString("0.#", CultureInfo.InvariantCulture);
                    var mudCut = (-node.AudioToneClarity / 100.0 * 2.5).ToString("0.#", CultureInfo.InvariantCulture);
                    filters.Add($"equalizer=f=2400:t=q:w=1.2:g={pres}");
                    filters.Add($"treble=g={air}:f=6500:w=0.7");
                    filters.Add($"equalizer=f=220:t=q:w=1.0:g={mudCut}");
                }

                // Waveform Shaper, Transient & Spectral Harmonics
                if (node.AudioWaveShaperEnabled)
                {
                    if (node.AudioWaveShaperDrivePercent > 5.0)
                    {
                        var drive = (1.0 + (node.AudioWaveShaperDrivePercent / 100.0) * 2.0).ToString("0.##", CultureInfo.InvariantCulture);
                        filters.Add(node.AudioWaveShaperCurve switch
                        {
                            "tape" => $"asoftclip=type=atan:param={drive}",
                            "soft" => $"asoftclip=type=cubic:param={drive}",
                            "hard" => $"asoftclip=type=hard:param={drive}",
                            "fold" => $"asoftclip=type=sin:param={drive}",
                            _ => $"volume={drive}"
                        });
                    }
                    if (node.AudioSubHarmonicsPercent > 5.0)
                    {
                        var sub = (node.AudioSubHarmonicsPercent / 100.0 * 6.0).ToString("0.#", CultureInfo.InvariantCulture);
                        filters.Add($"bass=g={sub}:f=80:w=0.8");
                    }
                    if (node.AudioHarmonicExciterPercent > 5.0)
                    {
                        var exc = (node.AudioHarmonicExciterPercent / 100.0 * 5.0).ToString("0.#", CultureInfo.InvariantCulture);
                        filters.Add($"treble=g={exc}:f=7500:w=0.7");
                    }
                    if (node.AudioPhaseInvertLeft && !node.AudioPhaseInvertRight)
                    {
                        filters.Add("stereotools=phasel=1");
                    }
                    else if (!node.AudioPhaseInvertLeft && node.AudioPhaseInvertRight)
                    {
                        filters.Add("stereotools=phaser=1");
                    }
                    else if (node.AudioPhaseInvertLeft && node.AudioPhaseInvertRight)
                    {
                        filters.Add("stereotools=phasel=1:phaser=1");
                    }
                }
            }

            // =========================================================================
            // MODULE 3: EQ 5-BAND & STUDIO FX (Only when enabled)
            // =========================================================================
            if (node.EqualizerFxEnabled)
            {
                // 5-Band Equalizer & Tone (Bass, Low-Mid, Mid, High-Mid, Treble)
                var eq = BuildEqualizerChain(node.AudioEqPreset, node.AudioBassGain, node.AudioLowMidGain, node.AudioMidGain, node.AudioHighMidGain, node.AudioTrebleGain);
                if (!string.IsNullOrWhiteSpace(eq))
                    filters.Add(eq);

                // Dynamic High-pass & Low-pass Cutoff Filters
                if (node.AudioHighpassFilter)
                {
                    var hp = Math.Clamp(node.AudioHighpassCutoffHz, 20.0, 500.0).ToString("0.#", CultureInfo.InvariantCulture);
                    filters.Add($"highpass=f={hp}");
                }
                if (node.AudioLowpassFilter)
                {
                    var lp = Math.Clamp(node.AudioLowpassCutoffHz, 2000.0, 20000.0).ToString("0.#", CultureInfo.InvariantCulture);
                    filters.Add($"lowpass=f={lp}");
                }

                // Creative FX: Robot Voice
                if (node.AudioRobotVoiceEnabled)
                {
                    filters.Add("tremolo=f=50:d=0.9,equalizer=f=1200:t=q:w=2:g=5");
                }

                // Creative FX: Radio / Telephone / Megaphone Voice
                if (node.AudioRadioVoiceEnabled)
                {
                    filters.Add("highpass=f=350,lowpass=f=3400,equalizer=f=1200:t=q:w=2:g=5,acrusher=bits=8:mix=0.4");
                }

                // Spatial & Modulation FX: Chorus / Vocal Doubler
                if (node.AudioChorusEnabled)
                {
                    var mix = (Math.Clamp(node.AudioChorusMixPercent / 100.0, 0.1, 1.0) * 0.9).ToString("0.##", CultureInfo.InvariantCulture);
                    filters.Add($"chorus=0.7:{mix}:55:0.4:0.25:2");
                }

                // Spatial FX: 8D Binaural Panning
                if (node.Audio8DEnabled)
                {
                    var speed = Math.Clamp(node.Audio8DSpeedHz, 0.05, 0.5).ToString("0.###", CultureInfo.InvariantCulture);
                    filters.Add($"apulsator=hz={speed}:amount=1.0");
                }

                // Spatial FX: Echo / Delay
                if (node.AudioEchoEnabled)
                {
                    var outGain = (Math.Clamp(node.AudioEchoMixPercent / 100.0, 0.1, 1.0) * 0.85).ToString("0.##", CultureInfo.InvariantCulture);
                    var delay = Math.Clamp(node.AudioEchoDelayMs, 50, 1000).ToString("0", CultureInfo.InvariantCulture);
                    var decay = (Math.Clamp(node.AudioEchoFeedbackPercent / 100.0, 0.1, 0.9)).ToString("0.##", CultureInfo.InvariantCulture);
                    filters.Add($"aecho=0.8:{outGain}:{delay}:{decay}");
                }

                // Studio Reverb
                if (node.AudioReverbPercent > 2.0)
                {
                    var revMix = (node.AudioReverbPercent / 100.0 * 0.65).ToString("0.##", CultureInfo.InvariantCulture);
                    filters.Add($"aecho=0.8:{revMix}:40:0.35");
                }

                // Analog Warmth
                if (node.AudioWarmthPercent > 2.0)
                {
                    var warmGain = (node.AudioWarmthPercent / 100.0 * 3.5).ToString("0.#", CultureInfo.InvariantCulture);
                    filters.Add($"equalizer=f=250:t=q:w=1.2:g={warmGain}");
                }

                // Stereo Width Field Expansion
                if (Math.Abs(node.AudioStereoWidthPercent - 100.0) > 1.0)
                {
                    var m = (node.AudioStereoWidthPercent / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
                    filters.Add($"extrastereo=m={m}");
                }

                // Vocal Separation / Karaoke (Center channel suppression or vocal isolation)
                if (node.AudioVocalBalance < -5.0)
                {
                    var k = Math.Clamp((-node.AudioVocalBalance / 100.0) * 0.85, 0.2, 0.95).ToString("0.##", CultureInfo.InvariantCulture);
                    filters.Add($"pan=stereo|c0=c0-{k}*c1|c1=c1-{k}*c0");
                }
                else if (node.AudioVocalBalance > 5.0)
                {
                    var v = Math.Clamp(1.0 + (node.AudioVocalBalance / 100.0) * 0.6, 1.05, 1.6).ToString("0.##", CultureInfo.InvariantCulture);
                    filters.Add($"stereotools=mlev={v}:slev=0.3");
                }
            }

            // =========================================================================
            // MODULE 4: DYNAMICS, NOISE CONTROL & MASTERING (Only when enabled)
            // =========================================================================
            if (node.DynamicsMasteringEnabled)
            {
                // Voice Dynamic Compressor (Làm dày giọng, cân bằng to nhỏ)
                if (node.AudioCompressorPercent > 2.0)
                {
                    var ratio = (1.5 + (node.AudioCompressorPercent / 100.0) * 4.5).ToString("0.#", CultureInfo.InvariantCulture);
                    filters.Add($"acompressor=threshold=-15dB:ratio={ratio}:attack=15:release=120:makeup=1.4");
                }

                // De-Esser (Khử âm xì chói s/x)
                if (node.AudioDeEsserPercent > 2.0)
                {
                    var deInt = (node.AudioDeEsserPercent / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
                    filters.Add($"deesser=i={deInt}");
                }

                // Noise Gate (Ngắt tiếng thở, tiếng ồn khi im lặng)
                if (node.AudioNoiseGatePercent > 2.0)
                {
                    var thresh = (0.005 + (node.AudioNoiseGatePercent / 100.0) * 0.045).ToString("0.####", CultureInfo.InvariantCulture);
                    filters.Add($"agate=threshold={thresh}:range=0.03:attack=10:release=150");
                }

                // Noise Reduction (Denoise)
                if (node.AudioDenoiseEnabled)
                    filters.Add("afftdn=nf=-25");

                // Fade In & Fade Out
                if (node.AudioFadeInSec > 0.05)
                {
                    var d = node.AudioFadeInSec.ToString("0.###", CultureInfo.InvariantCulture);
                    filters.Add($"afade=t=in:ss=0:d={d}");
                }
                if (node.AudioFadeOutSec > 0.05 && duration > node.AudioFadeOutSec)
                {
                    var st = (duration - node.AudioFadeOutSec).ToString("0.###", CultureInfo.InvariantCulture);
                    var d = node.AudioFadeOutSec.ToString("0.###", CultureInfo.InvariantCulture);
                    filters.Add($"afade=t=out:st={st}:d={d}");
                }

                // EBU R128 Loudness Normalization (applied at the very end of chain)
                if (node.AudioNormalizeEnabled)
                {
                    var lufs = node.AudioTargetLufs < -30 ? -14.0 : (node.AudioTargetLufs > -6 ? -14.0 : node.AudioTargetLufs);
                    var lufsStr = lufs.ToString("0.#", CultureInfo.InvariantCulture);
                    filters.Add($"loudnorm=I={lufsStr}:TP=-1.5:LRA=11");
                }
            }

            return string.Join(",", filters);
        }

        public static string BuildEqualizerChain(string? preset, double bassGain, double lowMidGain, double midGain, double highMidGain, double trebleGain)
        {
            var p = (preset ?? "neutral").Trim().ToLowerInvariant();
            var parts = new List<string>();

            switch (p)
            {
                case "vocal":
                case "vocal_boost":
                    parts.Add("equalizer=f=250:t=q:w=1:g=-2"); // reduce mud
                    parts.Add("equalizer=f=1200:t=q:w=1.2:g=4"); // clarity
                    parts.Add("equalizer=f=3500:t=q:w=1.2:g=3.5"); // presence
                    break;

                case "bass":
                case "bass_boost":
                    parts.Add("bass=g=7:f=100:w=0.6");
                    break;

                case "treble":
                case "treble_boost":
                    parts.Add("treble=g=6:f=4000:w=0.6");
                    break;

                case "podcast":
                    parts.Add("highpass=f=75");
                    parts.Add("equalizer=f=300:t=q:w=1:g=-2.5");
                    parts.Add("equalizer=f=2800:t=q:w=1.5:g=3");
                    parts.Add("treble=g=2:f=8000:w=0.7");
                    break;
            }

            // Custom user slider gains
            if (Math.Abs(bassGain) > 0.1 && p != "bass" && p != "bass_boost")
            {
                var bg = bassGain.ToString("0.#", CultureInfo.InvariantCulture);
                parts.Add($"bass=g={bg}:f=100:w=0.7");
            }
            if (Math.Abs(lowMidGain) > 0.1)
            {
                var lmg = lowMidGain.ToString("0.#", CultureInfo.InvariantCulture);
                parts.Add($"equalizer=f=350:t=q:w=0.8:g={lmg}");
            }
            if (Math.Abs(midGain) > 0.1)
            {
                var mg = midGain.ToString("0.#", CultureInfo.InvariantCulture);
                parts.Add($"equalizer=f=1200:t=q:w=0.65:g={mg}");
            }
            if (Math.Abs(highMidGain) > 0.1)
            {
                var hmg = highMidGain.ToString("0.#", CultureInfo.InvariantCulture);
                parts.Add($"equalizer=f=3500:t=q:w=0.8:g={hmg}");
            }
            if (Math.Abs(trebleGain) > 0.1 && p != "treble" && p != "treble_boost")
            {
                var tg = trebleGain.ToString("0.#", CultureInfo.InvariantCulture);
                parts.Add($"treble=g={tg}:f=8000:w=0.7");
            }

            return string.Join(",", parts);
        }

        public static string BuildEqualizerChain(string? preset, double bassGain, double midGain, double trebleGain)
            => BuildEqualizerChain(preset, bassGain, 0, midGain, 0, trebleGain);

        public static string BuildEqualizerChain(string? preset, double bassGain, double trebleGain)
            => BuildEqualizerChain(preset, bassGain, 0, trebleGain);

        public static string BuildAtempoChain(double factor)
        {
            var value = factor <= 0 ? 1.0 : factor;
            var parts = new List<double>();
            while (value < 0.5)
            {
                parts.Add(0.5);
                value /= 0.5;
            }
            while (value > 2.0)
            {
                parts.Add(2.0);
                value /= 2.0;
            }
            parts.Add(value);
            return string.Join(",", parts.Select(p => $"atempo={p.ToString("0.######", CultureInfo.InvariantCulture)}"));
        }

        public static (string codecArg, string[] extraArgs) ResolveAudioExportArgs(string? format, string? bitrate, string? sampleRate, string? channels)
        {
            var fmt = (format ?? "mp3").Trim().ToLowerInvariant();
            var br = string.IsNullOrWhiteSpace(bitrate) ? "320k" : bitrate.Trim();
            var sr = string.IsNullOrWhiteSpace(sampleRate) ? "48000" : sampleRate.Trim();
            var ch = string.Equals(channels, "mono", StringComparison.OrdinalIgnoreCase) ? "1" : "2";

            var extra = new List<string> { "-ar", sr, "-ac", ch };

            switch (fmt)
            {
                case "mp3":
                    extra.AddRange(new[] { "-b:a", br });
                    return ("libmp3lame", extra.ToArray());

                case "wav":
                    return ("pcm_s16le", extra.ToArray());

                case "aac":
                case "m4a":
                    extra.AddRange(new[] { "-b:a", br });
                    return ("aac", extra.ToArray());

                case "flac":
                    return ("flac", extra.ToArray());

                case "ogg":
                    extra.AddRange(new[] { "-b:a", br });
                    return ("libvorbis", extra.ToArray());

                default:
                    extra.AddRange(new[] { "-b:a", br });
                    return ("libmp3lame", extra.ToArray());
            }
        }

        public static string ResolveAudioFileExtension(string? format)
        {
            return (format ?? "mp3").Trim().ToLowerInvariant() switch
            {
                "wav" => ".wav",
                "aac" => ".aac",
                "m4a" => ".m4a",
                "flac" => ".flac",
                "ogg" => ".ogg",
                _ => ".mp3"
            };
        }

        public static string BuildSubtitleFilter(string subtitleFilePath)
        {
            if (string.IsNullOrWhiteSpace(subtitleFilePath) || !File.Exists(subtitleFilePath))
                return string.Empty;

            var escaped = subtitleFilePath.Replace("\\", "/").Replace(":", "\\:").Replace("'", "\\'");
            var ext = Path.GetExtension(subtitleFilePath).ToLowerInvariant();
            return ext is ".ass" or ".ssa" ? $"ass='{escaped}'" : $"subtitles='{escaped}'";
        }
    }
}
