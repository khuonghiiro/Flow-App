using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class VideoProcessingNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is VideoProcessingNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var videoNode = (VideoProcessingNode)node;
            var sw = Stopwatch.StartNew();

            try
            {
                var videoInput = ResolveFromMapping(env, videoNode.VideoSourceNodeId, videoNode.VideoSourceOutputKey);
                if (string.IsNullOrWhiteSpace(videoInput))
                    videoInput = videoNode.VideoPath;
                if (string.IsNullOrWhiteSpace(videoInput))
                    throw new InvalidOperationException("VideoProcessingNode: thiếu input video.");

                var outputFolder = ResolveFromMapping(env, videoNode.OutputFolderSourceNodeId, videoNode.OutputFolderSourceOutputKey);
                if (!videoNode.OutputBase64)
                {
                    if (string.IsNullOrWhiteSpace(outputFolder))
                        throw new InvalidOperationException("VideoProcessingNode: Output Base64 tắt nhưng chưa có folder output.");
                    Directory.CreateDirectory(outputFolder);
                }

                var tempRoot = Path.Combine(Path.GetTempPath(), "FlowMy_VideoProcessing");
                Directory.CreateDirectory(tempRoot);

                var sourceFps = await ProbeSourceFpsAsync(videoInput, env.CancellationToken).ConfigureAwait(false);
                if (sourceFps > 0) videoNode.SourceFps = sourceFps;
                var extractFps = Math.Max(1, Math.Min(videoNode.ExtractFps, Math.Max(1, videoNode.SourceFps)));

                var hwaccel = await ResolveHwAccelAsync(videoNode.PreferGpu, env.CancellationToken).ConfigureAwait(false);
                videoNode.PreferredHwAccel = hwaccel;

                var eqFilter = BuildEqFilter(videoNode);
                var frameFilter = $"fps={extractFps:0.###},{eqFilter}";
                var framePattern = videoNode.OutputBase64
                    ? Path.Combine(tempRoot, $"frames_{Guid.NewGuid():N}_%06d.png")
                    : Path.Combine(outputFolder!, $"frame_%06d.png");

                await RunFfmpegAsync(WithHwaccel(new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", videoInput,
                    "-vf", frameFilter,
                    framePattern
                }, hwaccel), env.CancellationToken).ConfigureAwait(false);

                var producedFrames = Directory.GetFiles(
                        Path.GetDirectoryName(framePattern)!,
                        Path.GetFileName(framePattern).Replace("%06d", "*"))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var framesOutput = videoNode.OutputBase64
                    ? JsonSerializer.Serialize(producedFrames.Select(File.ReadAllBytes).Select(Convert.ToBase64String).ToList())
                    : JsonSerializer.Serialize(producedFrames);
                SetOutput(videoNode, "frames_output", framesOutput);

                var baseVideoPath = Path.Combine(tempRoot, $"video_base_{Guid.NewGuid():N}.mp4");
                await RunFfmpegAsync(WithHwaccel(new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", videoInput,
                    "-vf", eqFilter,
                    "-an",
                    baseVideoPath
                }, hwaccel), env.CancellationToken).ConfigureAwait(false);

                var mixedVideo = await MergeAudioTracksAsync(videoNode, env, videoInput, baseVideoPath, outputFolder).ConfigureAwait(false);
                SetOutput(videoNode, "video_output", mixedVideo);
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(node, ex.Message);
                throw;
            }
            finally
            {
                sw.Stop();
                env.OnNodeCompleted?.Invoke(node, sw.Elapsed);
            }

            await env.TraverseOutputsAsync(node).ConfigureAwait(false);
        }

        private static string ResolveFromMapping(NodeExecutionEnvironment env, string? sourceNodeId, string? key)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(key)) return string.Empty;
            return env.Service.ResolveValueByNodeIdAndKeyForExecution(env.Connections, sourceNodeId, key, env);
        }

        private static async Task<string> MergeAudioTracksAsync(
            VideoProcessingNode node,
            NodeExecutionEnvironment env,
            string sourceVideoInput,
            string baseVideoPath,
            string? outputFolder)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "FlowMy_VideoProcessing");
            Directory.CreateDirectory(tempRoot);

            var outputVideo = string.IsNullOrWhiteSpace(outputFolder)
                ? Path.Combine(Path.GetTempPath(), $"video_processed_{Guid.NewGuid():N}.mp4")
                : Path.Combine(outputFolder, $"video_processed_{DateTime.Now:yyyyMMddHHmmss}.mp4");

            var audioInputs = new List<(string path, VideoAudioTrackConfig cfg)>();
            var sourceAudioPath = Path.Combine(tempRoot, $"audio_source_{Guid.NewGuid():N}.wav");
            await ExtractAudioTrackAsync(sourceVideoInput, sourceAudioPath, env.CancellationToken).ConfigureAwait(false);
            if (File.Exists(sourceAudioPath))
            {
                audioInputs.Add((sourceAudioPath, new VideoAudioTrackConfig
                {
                    VolumePercent = 100,
                    ShorterMode = AudioSyncMode.PadSilence,
                    LongerMode = AudioSyncMode.Trim
                }));
            }

            foreach (var t in node.AudioTracks)
            {
                var path = ResolveFromMapping(env, t.SourceNodeId, t.SourceOutputKey);
                if (!string.IsNullOrWhiteSpace(path))
                    audioInputs.Add((path, t));
            }

            if (audioInputs.Count == 0)
            {
                await RunFfmpegAsync(new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", baseVideoPath,
                    "-c:v", "copy",
                    outputVideo
                }, env.CancellationToken).ConfigureAwait(false);
                return outputVideo;
            }

            var videoDuration = await ProbeDurationSecondsAsync(baseVideoPath, env.CancellationToken).ConfigureAwait(false);
            if (videoDuration <= 0) videoDuration = 0.001;

            var preparedAudio = new List<(string path, VideoAudioTrackConfig cfg)>();
            foreach (var (path, cfg) in audioInputs)
            {
                var preparedPath = await PrepareAudioTrackBySyncModeAsync(path, cfg, videoDuration, tempRoot, env.CancellationToken).ConfigureAwait(false);
                preparedAudio.Add((preparedPath, cfg));
            }

            var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-i", baseVideoPath };
            foreach (var track in preparedAudio) args.AddRange(new[] { "-i", track.path });

            var filterChains = new List<string>();
            var mixInputs = new List<string>();
            for (var i = 0; i < preparedAudio.Count; i++)
            {
                var inputIndex = i + 1;
                var volume = Math.Max(0, preparedAudio[i].cfg.VolumePercent) / 100d;
                filterChains.Add($"[{inputIndex}:a]volume={volume:0.###}[a{i}]");
                mixInputs.Add($"[a{i}]");
            }
            filterChains.Add($"{string.Join(string.Empty, mixInputs)}amix=inputs={preparedAudio.Count}:dropout_transition=0:normalize=0[aout]");

            args.AddRange(new[]
            {
                "-filter_complex", string.Join(";", filterChains),
                "-map", "0:v:0",
                "-map", "[aout]",
                "-shortest",
                outputVideo
            });

            await RunFfmpegAsync(args, env.CancellationToken).ConfigureAwait(false);
            return outputVideo;
        }

        private static async Task ExtractAudioTrackAsync(string sourceVideo, string outputAudioPath, CancellationToken ct)
        {
            await RunFfmpegAsync(new[]
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", sourceVideo,
                "-vn",
                "-acodec", "pcm_s16le",
                outputAudioPath
            }, ct).ConfigureAwait(false);
        }

        private static async Task<string> PrepareAudioTrackBySyncModeAsync(
            string inputAudioPath,
            VideoAudioTrackConfig cfg,
            double videoDurationSec,
            string tempRoot,
            CancellationToken ct)
        {
            var audioDuration = await ProbeDurationSecondsAsync(inputAudioPath, ct).ConfigureAwait(false);
            if (audioDuration <= 0) return inputAudioPath;

            var isShorter = audioDuration < videoDurationSec - 0.0001;
            var mode = isShorter ? cfg.ShorterMode : cfg.LongerMode;
            var output = Path.Combine(tempRoot, $"audio_sync_{Guid.NewGuid():N}.wav");
            var trim = videoDurationSec.ToString("0.###", CultureInfo.InvariantCulture);

            switch (mode)
            {
                case AudioSyncMode.Loop:
                    await RunFfmpegAsync(new[]
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-stream_loop", "-1",
                        "-i", inputAudioPath,
                        "-t", trim,
                        "-c:a", "pcm_s16le",
                        output
                    }, ct).ConfigureAwait(false);
                    return output;

                case AudioSyncMode.PadSilence:
                    await RunFfmpegAsync(new[]
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-i", inputAudioPath,
                        "-af", $"apad,atrim=0:{trim}",
                        "-c:a", "pcm_s16le",
                        output
                    }, ct).ConfigureAwait(false);
                    return output;

                case AudioSyncMode.Stretch:
                {
                    var factor = Math.Max(0.01, audioDuration / videoDurationSec);
                    var atempo = BuildAtempoChain(factor);
                    await RunFfmpegAsync(new[]
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-i", inputAudioPath,
                        "-af", $"{atempo},atrim=0:{trim}",
                        "-c:a", "pcm_s16le",
                        output
                    }, ct).ConfigureAwait(false);
                    return output;
                }

                case AudioSyncMode.Trim:
                    await RunFfmpegAsync(new[]
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-i", inputAudioPath,
                        "-af", $"atrim=0:{trim}",
                        "-c:a", "pcm_s16le",
                        output
                    }, ct).ConfigureAwait(false);
                    return output;

                case AudioSyncMode.Compress:
                {
                    var factor = Math.Max(0.01, audioDuration / videoDurationSec);
                    var atempo = BuildAtempoChain(factor);
                    await RunFfmpegAsync(new[]
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-i", inputAudioPath,
                        "-af", $"{atempo},atrim=0:{trim}",
                        "-c:a", "pcm_s16le",
                        output
                    }, ct).ConfigureAwait(false);
                    return output;
                }

                default:
                    return inputAudioPath;
            }
        }

        private static string BuildAtempoChain(double factor)
        {
            var value = factor <= 0 ? 1 : factor;
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

        private static string BuildEqFilter(VideoProcessingNode node)
        {
            var hueRadians = node.Hue * Math.PI / 180d;
            return $"eq=brightness={node.Brightness:0.###}:contrast={node.Contrast:0.###}:saturation={node.Saturation:0.###}:hue={hueRadians:0.###}";
        }

        private static async Task<double> ProbeSourceFpsAsync(string inputPath, CancellationToken ct)
        {
            var args = new[]
            {
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "stream=r_frame_rate",
                "-of", "default=nokey=1:noprint_wrappers=1",
                inputPath
            };
            var output = await RunProcessCaptureAsync(ResolveBinary("ffprobe"), args, ct).ConfigureAwait(false);
            var value = output.Trim();
            if (string.IsNullOrWhiteSpace(value)) return 0;
            if (value.Contains('/'))
            {
                var parts = value.Split('/');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) &&
                    d > 0)
                    return n / d;
            }
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps) ? fps : 0;
        }

        private static async Task<double> ProbeDurationSecondsAsync(string inputPath, CancellationToken ct)
        {
            var args = new[]
            {
                "-v", "error",
                "-show_entries", "format=duration",
                "-of", "default=nokey=1:noprint_wrappers=1",
                inputPath
            };
            var output = await RunProcessCaptureAsync(ResolveBinary("ffprobe"), args, ct).ConfigureAwait(false);
            return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : 0;
        }

        private static async Task<string> ResolveHwAccelAsync(bool preferGpu, CancellationToken ct)
        {
            if (!preferGpu) return "none";
            foreach (var accel in new[] { "cuda", "d3d11va" })
            {
                var ok = await RunProcessExitCodeAsync(ResolveBinary("ffmpeg"), new[]
                {
                    "-hide_banner", "-loglevel", "error",
                    "-hwaccel", accel,
                    "-f", "lavfi", "-i", "nullsrc",
                    "-frames:v", "1",
                    "-f", "null", "-"
                }, ct).ConfigureAwait(false);
                if (ok == 0) return accel;
            }
            return "none";
        }

        private static IEnumerable<string> WithHwaccel(IEnumerable<string> args, string hwaccel)
        {
            if (string.IsNullOrWhiteSpace(hwaccel) || string.Equals(hwaccel, "none", StringComparison.OrdinalIgnoreCase))
                return args;
            return new[] { "-hwaccel", hwaccel }.Concat(args);
        }

        private static async Task RunFfmpegAsync(IEnumerable<string> args, CancellationToken ct)
        {
            var exit = await RunProcessExitCodeAsync(ResolveBinary("ffmpeg"), args, ct).ConfigureAwait(false);
            if (exit != 0)
                throw new InvalidOperationException("VideoProcessingNode: FFmpeg xử lý thất bại.");
        }

        private static async Task<int> RunProcessExitCodeAsync(string fileName, IEnumerable<string> args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p == null) return -1;
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            return p.ExitCode;
        }

        private static async Task<string> RunProcessCaptureAsync(string fileName, IEnumerable<string> args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p == null) return string.Empty;
            var stdout = p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            return await stdout.ConfigureAwait(false);
        }

        private static string ResolveBinary(string binary)
        {
            return FfmpegPathPreferencesStore.ResolveBinaryPath(binary);
        }

        private static void SetOutput(VideoProcessingNode node, string key, string value)
        {
            var port = node.DynamicOutputs?.FirstOrDefault(o =>
                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            if (port != null) port.UserValueOverride = value ?? string.Empty;
        }
    }
}
