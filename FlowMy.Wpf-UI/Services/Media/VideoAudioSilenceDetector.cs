// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow.Audio;

namespace FlowMy.Services.Media
{
    public sealed class SilenceInterval
    {
        public double StartSec { get; set; }
        public double EndSec { get; set; }
        public double DurationSec => Math.Max(0, EndSec - StartSec);
        public double MidSec => StartSec + (DurationSec / 2.0);
    }

    public sealed class AudioChunkInfo
    {
        public int ChunkIndex { get; set; }
        public string CodeId { get; set; } = string.Empty;
        public double StartSec { get; set; }
        public double EndSec { get; set; }
        public double DurationSec => Math.Max(0, EndSec - StartSec);
        public string AudioPath { get; set; } = string.Empty;
        public string? AudioBase64 { get; set; }
        public string ExecutionId { get; set; } = string.Empty;
    }

    public static class VideoAudioSilenceDetector
    {
        private static readonly Regex SilenceStartRegex = new(@"silence_start:\s*(?<time>[\d\.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SilenceEndRegex = new(@"silence_end:\s*(?<time>[\d\.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Quét và phát hiện tất cả các khoảng lặng giọng nói trong file audio bằng FFmpeg silencedetect.
        /// </summary>
        public static async Task<List<SilenceInterval>> DetectSilencesAsync(
            string audioFilePath,
            double thresholdDb = -30.0,
            double minSilenceSec = 0.3,
            Action<string>? logger = null,
            CancellationToken ct = default)
        {
            var results = new List<SilenceInterval>();
            if (!File.Exists(audioFilePath)) return results;

            var ffmpegExe = FlowMy.Services.Utilities.EnvironmentPathPreferencesStore.ResolveBinaryPath("ffmpeg.exe");
            if (string.IsNullOrWhiteSpace(ffmpegExe) || !File.Exists(ffmpegExe))
            {
                logger?.Invoke("⚠ Không tìm thấy ffmpeg.exe để phân tích nhịp giọng nghỉ.");
                return results;
            }

            var noiseStr = $"{thresholdDb.ToString("0.0", CultureInfo.InvariantCulture)}dB";
            var durStr = minSilenceSec.ToString("0.00", CultureInfo.InvariantCulture);
            var afArg = $"silencedetect=noise={noiseStr}:d={durStr}";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = $"-hide_banner -vn -i \"{audioFilePath}\" -af \"{afArg}\" -f null -",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            double? pendingStart = null;

            try
            {
                using var proc = new Process { StartInfo = psi };
                proc.ErrorDataReceived += (_, e) =>
                {
                    if (string.IsNullOrWhiteSpace(e.Data)) return;

                    var mStart = SilenceStartRegex.Match(e.Data);
                    if (mStart.Success && double.TryParse(mStart.Groups["time"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var st))
                    {
                        pendingStart = st;
                    }

                    var mEnd = SilenceEndRegex.Match(e.Data);
                    if (mEnd.Success && double.TryParse(mEnd.Groups["time"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ed))
                    {
                        var stVal = pendingStart ?? Math.Max(0, ed - minSilenceSec);
                        results.Add(new SilenceInterval { StartSec = stVal, EndSec = ed });
                        pendingStart = null;
                    }
                };

                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                await proc.WaitForExitAsync(ct).ConfigureAwait(false);
                logger?.Invoke($"🔍 [VAD] Đã phát hiện {results.Count} khoảng lặng/nhịp nghỉ giọng nói trong audio.");
            }
            catch (Exception ex)
            {
                logger?.Invoke($"⚠ Lỗi khi quét silencedetect: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Tính toán các mốc cắt audio thông minh dựa theo nhịp giọng nghỉ (tránh ngắt ngang câu nói).
        /// </summary>
        public static List<(double start, double end)> CalculateSmartSplitBoundaries(
            double totalDurationSec,
            double targetChunkDurationSec,
            List<SilenceInterval> silences,
            bool enableSmartSilence,
            double maxSearchWindowSec = 8.0)
        {
            var boundaries = new List<(double start, double end)>();
            if (totalDurationSec <= 0) return boundaries;

            var effectiveTarget = Math.Max(5.0, targetChunkDurationSec);
            double curStart = 0.0;

            while (curStart < totalDurationSec - 0.5)
            {
                double idealEnd = curStart + effectiveTarget;
                if (idealEnd >= totalDurationSec - 1.0)
                {
                    boundaries.Add((curStart, totalDurationSec));
                    break;
                }

                double cutPoint = idealEnd;

                if (enableSmartSilence && silences != null && silences.Count > 0)
                {
                    // Quét tìm khoảng nghỉ tốt nhất gần mốc idealEnd (ưu tiên trong khoảng [idealEnd - window, idealEnd + window])
                    double winStart = Math.Max(curStart + 3.0, idealEnd - maxSearchWindowSec);
                    double winEnd = Math.Min(totalDurationSec, idealEnd + maxSearchWindowSec);

                    // Tìm các silence nằm trong window
                    var candidateSilences = silences
                        .Where(s => s.EndSec >= winStart && s.StartSec <= winEnd)
                        .ToList();

                    if (candidateSilences.Count > 0)
                    {
                        // Ưu tiên khoảng nghỉ nằm trước hoặc gần idealEnd nhất
                        var bestSilence = candidateSilences
                            .OrderBy(s => Math.Abs(s.EndSec - idealEnd))
                            .First();

                        // Cắt tại điểm kết thúc khoảng nghỉ (để câu nói mới nằm trọn vẹn ở phân đoạn sau)
                        cutPoint = Math.Clamp(bestSilence.EndSec, curStart + 2.0, totalDurationSec);
                    }
                }

                boundaries.Add((curStart, cutPoint));
                curStart = cutPoint;
            }

            return boundaries;
        }
    }
}
