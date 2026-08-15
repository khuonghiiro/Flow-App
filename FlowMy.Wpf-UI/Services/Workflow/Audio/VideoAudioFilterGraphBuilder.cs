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

            // 4. Equalizer & Tone (Bass & Treble & Vocal boost)
            var eq = BuildEqualizerChain(node.AudioEqPreset, node.AudioBassGain, node.AudioTrebleGain);
            if (!string.IsNullOrWhiteSpace(eq))
                filters.Add(eq);

            // 5. High-pass (low-cut rumble/wind) & Low-pass (high-cut hiss)
            if (node.AudioHighpassFilter)
                filters.Add("highpass=f=80");
            if (node.AudioLowpassFilter)
                filters.Add("lowpass=f=12000");

            // 6. Noise Reduction (Denoise)
            if (node.AudioDenoiseEnabled)
                filters.Add("afftdn=nf=-25");

            // 7. Fade In & Fade Out
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

            // 8. EBU R128 Loudness Normalization (applied at the very end of chain)
            if (node.AudioNormalizeEnabled)
            {
                var lufs = node.AudioTargetLufs < -30 ? -14.0 : (node.AudioTargetLufs > -6 ? -14.0 : node.AudioTargetLufs);
                var lufsStr = lufs.ToString("0.#", CultureInfo.InvariantCulture);
                filters.Add($"loudnorm=I={lufsStr}:TP=-1.5:LRA=11");
            }

            return string.Join(",", filters);
        }

        public static string BuildEqualizerChain(string? preset, double bassGain, double trebleGain)
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
                parts.Add($"bass=g={bg}:f=100:w=0.6");
            }
            if (Math.Abs(trebleGain) > 0.1 && p != "treble" && p != "treble_boost")
            {
                var tg = trebleGain.ToString("0.#", CultureInfo.InvariantCulture);
                parts.Add($"treble=g={tg}:f=3500:w=0.6");
            }

            return string.Join(",", parts);
        }

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
    }
}
