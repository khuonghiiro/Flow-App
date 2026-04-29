using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class VideoProcessingNodeExecutor : INodeExecutor
    {
        public static event Action<VideoProcessingNode, double, string>? ProgressChanged;
        public static event Action<VideoProcessingNode, string>? LogLine;

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

                var downloadsRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");

                var defaultFrameOutputFolder = Path.Combine(downloadsRoot, "flow-frame");
                var defaultVideoOutputFolder = Path.Combine(downloadsRoot, "flow-video");

                string? frameOutputFolder;
                if (videoNode.UseDialogVideoConfig)
                {
                    frameOutputFolder = ResolveFromMapping(env, videoNode.OutputFolderSourceNodeId, videoNode.OutputFolderSourceOutputKey);
                    if (string.IsNullOrWhiteSpace(frameOutputFolder))
                        frameOutputFolder = videoNode.FrameOutputFolderPath;

                    if (string.IsNullOrWhiteSpace(frameOutputFolder))
                        frameOutputFolder = defaultFrameOutputFolder;
                }
                else
                {
                    frameOutputFolder = videoNode.FrameOutputFolderPath;
                }

                string? videoOutputFolder;
                if (videoNode.UseDialogVideoConfig)
                {
                    videoOutputFolder = ResolveFromMapping(env, videoNode.VideoOutputFolderSourceNodeId, videoNode.VideoOutputFolderSourceOutputKey);
                    if (string.IsNullOrWhiteSpace(videoOutputFolder))
                        videoOutputFolder = videoNode.DefaultOutputVideoPath;

                    if (string.IsNullOrWhiteSpace(videoOutputFolder))
                        videoOutputFolder = defaultVideoOutputFolder;
                }
                else
                {
                    videoOutputFolder = videoNode.DefaultOutputVideoPath;
                }
                if (!string.IsNullOrWhiteSpace(videoOutputFolder) &&
                    !Directory.Exists(videoOutputFolder) &&
                    !string.IsNullOrWhiteSpace(Path.GetExtension(videoOutputFolder)))
                {
                    var dir = Path.GetDirectoryName(videoOutputFolder);
                    if (!string.IsNullOrWhiteSpace(dir)) videoOutputFolder = dir;
                }

                if (!videoNode.OutputBase64)
                {
                    if (string.IsNullOrWhiteSpace(frameOutputFolder))
                        throw new InvalidOperationException("VideoProcessingNode: Output Base64 tắt nhưng chưa có folder output.");
                    Directory.CreateDirectory(frameOutputFolder);
                }

                var tempRoot = Path.Combine(Path.GetTempPath(), "FlowMy_VideoProcessing");
                Directory.CreateDirectory(tempRoot);

                var sourceFps = await ProbeSourceFpsAsync(videoInput, env.CancellationToken).ConfigureAwait(false);
                if (sourceFps > 0) videoNode.SourceFps = sourceFps;
                var extractFps = videoNode.ExtractAllFrames
                    ? Math.Max(1, videoNode.SourceFps)
                    : Math.Max(1, Math.Min(videoNode.ExtractFps, Math.Max(1, videoNode.SourceFps)));

                var hwaccel = await ResolveHwAccelAsync(videoNode.PreferGpu, env.CancellationToken).ConfigureAwait(false);
                videoNode.PreferredHwAccel = hwaccel;

                var frameFilter = BuildVideoFilterChain(videoNode, extractFps, includeTextOverlay: false);
                var frameExt = videoNode.FrameOutputFormat switch
                {
                    "jpg" => "jpg",
                    "webp" => "webp",
                    _ => "png"
                };
                var framePattern = videoNode.OutputBase64
                    ? Path.Combine(tempRoot, $"frames_{Guid.NewGuid():N}_%06d.{frameExt}")
                    : Path.Combine(frameOutputFolder!, $"frame_%06d.{frameExt}");

                var totalDuration = await ProbeDurationSecondsAsync(videoInput, env.CancellationToken).ConfigureAwait(false);
                var frameArgs = new List<string>(BuildTrimAwareArgs(videoNode, new[] { "-y", "-hide_banner", "-loglevel", "error", "-i", videoInput }));
                if (frameExt == "jpg") frameArgs.AddRange(new[] { "-q:v", Math.Max(1, 31 - (videoNode.JpegQuality / 4)).ToString(CultureInfo.InvariantCulture) });
                frameArgs.AddRange(new[] { "-vf", frameFilter, framePattern });
                await RunFfmpegWithProgressAsync(
                    WithHwaccel(frameArgs, hwaccel),
                    totalDuration,
                    (pct, status) => ProgressChanged?.Invoke(videoNode, pct, status),
                    line => LogLine?.Invoke(videoNode, line),
                    env.CancellationToken).ConfigureAwait(false);

                var producedFrames = Directory.GetFiles(
                        Path.GetDirectoryName(framePattern)!,
                        Path.GetFileName(framePattern).Replace("%06d", "*"))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var framesOutput = videoNode.OutputBase64
                    ? JsonSerializer.Serialize(producedFrames.Select(File.ReadAllBytes).Select(Convert.ToBase64String).ToList())
                    : JsonSerializer.Serialize(producedFrames);
                SetOutput(videoNode, "frames_output", framesOutput);

                var (codecArgs, extension) = BuildOutputArgs(videoNode);
                var outputBasePath = Path.Combine(tempRoot, $"video_base_{Guid.NewGuid():N}{extension}");
                var mainFilter = BuildVideoFilterChain(videoNode, extractFps, includeTextOverlay: true);
                var hasCanvasOverlays = videoNode.Overlays.Any(o => o.IsVisible);
                var mainArgs = BuildTrimAwareArgs(videoNode, new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", videoInput,
                    "-sn",
                    "-an",
                }).ToList();

                if (hasCanvasOverlays)
                {
                    var (overlayFilter, imageInputs, outputLabel) = BuildOverlayFilterComplex(videoNode, mainFilter);
                    foreach (var inputPath in imageInputs)
                    {
                        mainArgs.AddRange(new[] { "-i", inputPath });
                    }

                    mainArgs.AddRange(new[]
                    {
                        "-filter_complex", overlayFilter,
                        "-map", outputLabel
                    });
                }
                else if (videoNode.WatermarkEnabled && !string.IsNullOrWhiteSpace(videoNode.WatermarkImagePath))
                {
                    mainArgs.AddRange(new[] { "-i", videoNode.WatermarkImagePath! });
                    var overlayExpr = BuildOverlayExpression(videoNode);
                    var overlayAlpha = videoNode.WatermarkOpacity.ToString("0.###", CultureInfo.InvariantCulture);
                    mainArgs.AddRange(new[]
                    {
                        "-filter_complex",
                        $"[0:v]{mainFilter}[base];[1:v]format=rgba,colorchannelmixer=aa={overlayAlpha}[wm];[base][wm]overlay={overlayExpr}[vout]",
                        "-map", "[vout]"
                    });
                }
                else
                {
                    mainArgs.AddRange(new[] { "-vf", mainFilter });
                }
                mainArgs.AddRange(codecArgs);
                mainArgs.Add(outputBasePath);

                if (videoNode.TwoPassEnabled && (videoNode.OutputFormat == "mp4_h264" || videoNode.OutputFormat == "mp4_h265"))
                {
                    await RunTwoPassEncodeAsync(videoNode, mainArgs, outputBasePath, hwaccel, totalDuration, env.CancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await RunFfmpegWithProgressAsync(
                        WithHwaccel(mainArgs, hwaccel),
                        totalDuration,
                        (pct, status) => ProgressChanged?.Invoke(videoNode, pct, status),
                        line => LogLine?.Invoke(videoNode, line),
                        env.CancellationToken).ConfigureAwait(false);
                }

                /*
                    totalDuration,
                    (pct, status) => ProgressChanged?.Invoke(videoNode, pct, status),
                    line => LogLine?.Invoke(videoNode, line),
                    env.CancellationToken).ConfigureAwait(false);*/

                var postStabilizedPath = outputBasePath;
                if (videoNode.StabilizeEnabled)
                {
                    var stabilizedPath = Path.Combine(tempRoot, $"video_stabilized_{Guid.NewGuid():N}{extension}");
                    await StabilizeVideoAsync(outputBasePath, stabilizedPath, env.CancellationToken).ConfigureAwait(false);
                    postStabilizedPath = stabilizedPath;
                }

                var mixedVideo = await MergeAudioTracksAsync(videoNode, env, videoInput, postStabilizedPath, videoOutputFolder).ConfigureAwait(false);
                SetOutput(videoNode, "video_output", mixedVideo);
                ProgressChanged?.Invoke(videoNode, 100, "Completed");
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

            var (_, extension) = BuildOutputArgs(node);
            var outputVideo = !string.IsNullOrWhiteSpace(node.OutputPathOverride)
                ? node.OutputPathOverride!
                : !string.IsNullOrWhiteSpace(node.DefaultOutputVideoPath)
                    ? node.DefaultOutputVideoPath!
                : string.IsNullOrWhiteSpace(outputFolder)
                    ? Path.Combine(Path.GetTempPath(), $"video_processed_{Guid.NewGuid():N}{extension}")
                    : Path.Combine(outputFolder, $"video_processed_{DateTime.Now:yyyyMMddHHmmss}{extension}");

            var audioInputs = new List<(string path, VideoAudioTrackConfig cfg)>();
            if (node.SourceAudioEnabled)
            {
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
            }

            foreach (var t in node.AudioTracks)
            {
                var path = ResolveFromMapping(env, t.SourceNodeId, t.SourceOutputKey);
                if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(t.SourceNodeId))
                    path = t.SourceOutputKey;

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    audioInputs.Add((path, t));
                else if (!string.IsNullOrWhiteSpace(path))
                    LogLine?.Invoke(node, $"⚠ Audio track not found: {path}");
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
                "-c:a", ResolveAudioCodecArg(node.AudioCodec),
                "-b:a", node.AudioBitrate,
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
                    var factor = Math.Max(0.01, audioDuration / Math.Max(0.001, videoDurationSec));
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
                    var factor = Math.Max(0.01, audioDuration / Math.Max(0.001, videoDurationSec));
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
            return $"eq=brightness={node.Brightness:0.###}:contrast={node.Contrast:0.###}:saturation={node.Saturation:0.###}";
        }

        private static string BuildVideoFilterChain(VideoProcessingNode node, double extractFps, bool includeTextOverlay)
        {
            var filters = new List<string> { $"fps={extractFps:0.###}", BuildEqFilter(node) };
            if (Math.Abs(node.Hue) > 0.01)
            {
                // Keep hue transform in dedicated filter for broader FFmpeg compatibility.
                var hueRadians = (node.Hue * Math.PI / 180d).ToString("0.######", CultureInfo.InvariantCulture);
                filters.Add($"hue=h={hueRadians}");
            }
            if (Math.Abs(node.Gamma - 1) > 0.01)
            {
                var g = node.Gamma.ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"lutrgb=r='pow(val/255,1/{g})*255':g='pow(val/255,1/{g})*255':b='pow(val/255,1/{g})*255'");
            }
            if (node.SharpenEnabled && node.SharpenStrength > 0)
            {
                var s = (node.SharpenStrength * 0.3).ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"unsharp=5:5:{s}:5:5:0");
            }
            if (node.DenoiseEnabled && node.DenoiseStrength > 0)
            {
                var d = node.DenoiseStrength.ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"hqdn3d={d}:{d}:{d}:{d}");
            }
            if (node.BlurEnabled && node.BlurRadius > 0)
            {
                var r = node.BlurRadius.ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"gblur=sigma={r}");
            }

            var rot = ((int)node.RotationDegrees / 90) % 4;
            if (rot == 1) filters.Add("transpose=1");
            else if (rot == 2) filters.Add("transpose=2,transpose=2");
            else if (rot == 3) filters.Add("transpose=2");
            if (node.FlipH) filters.Add("hflip");
            if (node.FlipV) filters.Add("vflip");

            if (node.FixedResolutionHeight.HasValue) filters.Add($"scale=-2:{node.FixedResolutionHeight.Value}");
            else if (Math.Abs(node.ResolutionScale - 1) > 0.01)
            {
                var sc = node.ResolutionScale.ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"scale=iw*{sc}:ih*{sc}");
            }
            if (Math.Abs(node.SpeedFactor - 1) > 0.01)
            {
                var pts = (1.0 / node.SpeedFactor).ToString("0.######", CultureInfo.InvariantCulture);
                filters.Add($"setpts={pts}*PTS");
            }
            if (includeTextOverlay && node.TextOverlayEnabled && !string.IsNullOrWhiteSpace(node.OverlayText))
            {
                var escapedText = node.OverlayText.Replace("\\", "\\\\").Replace(":", "\\:").Replace("'", "\\'");
                var xExpr = node.TextPosition.Contains('C') ? "(w-tw)/2" : node.TextPosition.EndsWith('L') ? "10" : "w-tw-10";
                var yExpr = node.TextPosition.StartsWith('T') ? "10" : node.TextPosition.StartsWith('M') ? "(h-th)/2" : "h-th-10";
                var fontPath = ResolveFontPath(node.OverlayFont);
                filters.Add($"drawtext=text='{escapedText}':fontfile='{fontPath}':fontsize={node.OverlayFontSize}:fontcolor={node.OverlayFontColor}:x={xExpr}:y={yExpr}");
            }
            if (node.BurnSubtitleEnabled && !string.IsNullOrWhiteSpace(node.SubtitlePath))
            {
                var subPath = node.SubtitlePath!.Replace("\\", "/").Replace(":", "\\:");
                filters.Add($"subtitles='{subPath}'");
            }
            return string.Join(",", filters);
        }

        private static (string filterComplex, List<string> imageInputs, string outputLabel) BuildOverlayFilterComplex(VideoProcessingNode node, string baseFilter)
        {
            var filterChains = new List<string> { $"[0:v]{baseFilter}[v0]" };
            var imageInputs = new List<string>();
            var currentLabel = "v0";
            var imageInputIndex = 1;
            var stageIndex = 0;

            foreach (var item in node.Overlays.Where(o => o.IsVisible))
            {
                var type = (item.Type ?? string.Empty).Trim().ToLowerInvariant();
                var xExpr = $"main_w*{item.X.ToString("0.######", CultureInfo.InvariantCulture)}";
                var yExpr = $"main_h*{item.Y.ToString("0.######", CultureInfo.InvariantCulture)}";
                var opacity = Math.Clamp(item.Opacity, 0, 1).ToString("0.###", CultureInfo.InvariantCulture);
                var nextLabel = $"v{++stageIndex}";

                if ((type == "image" || type == "logo") && !string.IsNullOrWhiteSpace(item.Source) && File.Exists(item.Source))
                {
                    var wExpr = $"max(1,main_w*{item.Width.ToString("0.######", CultureInfo.InvariantCulture)})";
                    var hExpr = $"max(1,main_h*{item.Height.ToString("0.######", CultureInfo.InvariantCulture)})";
                    imageInputs.Add(item.Source);
                    filterChains.Add($"[{imageInputIndex}:v][{currentLabel}]scale2ref=w='{wExpr}':h='{hExpr}'[ov{stageIndex}][base{stageIndex}]");
                    filterChains.Add($"[base{stageIndex}][ov{stageIndex}]overlay=x='{xExpr}':y='{yExpr}':alpha={opacity}[{nextLabel}]");
                    imageInputIndex++;
                    currentLabel = nextLabel;
                }
                else if (type == "text")
                {
                    var text = (item.Source ?? string.Empty).Replace("\\", "\\\\").Replace(":", "\\:").Replace("'", "\\'");
                    var fontFamily = string.IsNullOrWhiteSpace(item.FontFamily) ? "Arial" : item.FontFamily;
                    var fontPath = ResolveFontPath(fontFamily).Replace("\\", "/").Replace(":", "\\:");
                    var fontColor = string.IsNullOrWhiteSpace(item.FontColor) ? "white" : item.FontColor;
                    filterChains.Add($"[{currentLabel}]drawtext=text='{text}':x={xExpr}:y={yExpr}:fontsize={item.FontSize}:fontcolor={fontColor}:fontfile='{fontPath}':alpha={opacity}[{nextLabel}]");
                    currentLabel = nextLabel;
                }
            }

            return (string.Join(";", filterChains), imageInputs, $"[{currentLabel}]");
        }

        private static string BuildVideoFilterChainWithoutFps(VideoProcessingNode node)
        {
            var filters = new List<string> { BuildEqFilter(node) };
            if (Math.Abs(node.Hue) > 0.01)
            {
                var hueRadians = (node.Hue * Math.PI / 180d).ToString("0.######", CultureInfo.InvariantCulture);
                filters.Add($"hue=h={hueRadians}");
            }
            if (Math.Abs(node.Gamma - 1) > 0.01)
            {
                var g = node.Gamma.ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"lutrgb=r='pow(val/255,1/{g})*255':g='pow(val/255,1/{g})*255':b='pow(val/255,1/{g})*255'");
            }
            if (node.SharpenEnabled && node.SharpenStrength > 0)
                filters.Add($"unsharp=5:5:{(node.SharpenStrength * 0.3).ToString("0.###", CultureInfo.InvariantCulture)}:5:5:0");
            if (node.DenoiseEnabled && node.DenoiseStrength > 0)
            {
                var d = node.DenoiseStrength.ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"hqdn3d={d}:{d}:{d}:{d}");
            }
            if (node.BlurEnabled && node.BlurRadius > 0)
                filters.Add($"gblur=sigma={node.BlurRadius.ToString("0.###", CultureInfo.InvariantCulture)}");

            var rot = ((int)node.RotationDegrees / 90) % 4;
            if (rot == 1) filters.Add("transpose=1");
            else if (rot == 2) filters.Add("transpose=2,transpose=2");
            else if (rot == 3) filters.Add("transpose=2");
            if (node.FlipH) filters.Add("hflip");
            if (node.FlipV) filters.Add("vflip");

            if (node.FixedResolutionHeight.HasValue) filters.Add($"scale=-2:{node.FixedResolutionHeight.Value}");
            else if (Math.Abs(node.ResolutionScale - 1) > 0.01)
            {
                var sc = node.ResolutionScale.ToString("0.###", CultureInfo.InvariantCulture);
                filters.Add($"scale=iw*{sc}:ih*{sc}");
            }

            return filters.Count > 0 ? string.Join(",", filters) : string.Empty;
        }

        private static string BuildOverlayExpression(VideoProcessingNode node)
        {
            return node.WatermarkPosition switch
            {
                "TL" => $"x={node.WatermarkPaddingPx}:y={node.WatermarkPaddingPx}",
                "TC" => $"x=(W-w)/2:y={node.WatermarkPaddingPx}",
                "TR" => $"x=W-w-{node.WatermarkPaddingPx}:y={node.WatermarkPaddingPx}",
                "ML" => $"x={node.WatermarkPaddingPx}:y=(H-h)/2",
                "MC" => "x=(W-w)/2:y=(H-h)/2",
                "MR" => $"x=W-w-{node.WatermarkPaddingPx}:y=(H-h)/2",
                "BL" => $"x={node.WatermarkPaddingPx}:y=H-h-{node.WatermarkPaddingPx}",
                "BC" => $"x=(W-w)/2:y=H-h-{node.WatermarkPaddingPx}",
                _ => $"x=W-w-{node.WatermarkPaddingPx}:y=H-h-{node.WatermarkPaddingPx}"
            };
        }

        private static string ResolveFontPath(string? font)
        {
            var f = string.IsNullOrWhiteSpace(font) ? "Arial" : font.Trim();
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", $"{f}.ttf");
        }

        private static IEnumerable<string> BuildTrimAwareArgs(VideoProcessingNode node, IEnumerable<string> remainingArgs)
        {
            var trimArgs = new List<string>();
            if (node.TrimEnabled && node.TrimEndSec > node.TrimStartSec)
            {
                trimArgs.AddRange(new[]
                {
                    "-ss", node.TrimStartSec.ToString("0.###", CultureInfo.InvariantCulture),
                    "-to", node.TrimEndSec.ToString("0.###", CultureInfo.InvariantCulture)
                });
            }
            return trimArgs.Concat(remainingArgs);
        }

        private static (string[] codecArgs, string extension) BuildOutputArgs(VideoProcessingNode node)
        {
            return (node.OutputFormat ?? "mp4_h264") switch
            {
                "mp4_h264" => (new[] { "-c:v", "libx264", "-preset", node.EncoderPreset ?? "medium", "-crf", ((int)node.Crf).ToString() }, ".mp4"),
                "mp4_h265" => (new[] { "-c:v", "libx265", "-preset", node.EncoderPreset ?? "medium", "-crf", ((int)node.Crf).ToString(), "-tag:v", "hvc1" }, ".mp4"),
                "webm_vp9" => (new[] { "-c:v", "libvpx-vp9", "-crf", ((int)node.Crf).ToString(), "-b:v", "0" }, ".webm"),
                "mov_prores" => (new[] { "-c:v", "prores_ks", "-profile:v", "3" }, ".mov"),
                "gif" => (new[] { "-loop", "0" }, ".gif"),
                _ => (new[] { "-c:v", "copy" }, ".mp4")
            };
        }

        private static string ResolveAudioCodecArg(string codec)
        {
            return codec switch
            {
                "mp3" => "libmp3lame",
                "opus" => "libopus",
                "copy" => "copy",
                _ => "aac"
            };
        }

        private static async Task RunTwoPassEncodeAsync(
            VideoProcessingNode node,
            List<string> fullArgs,
            string outputPath,
            string hwaccel,
            double totalDurationSec,
            CancellationToken ct)
        {
            var tempOut = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
            var pass1 = new List<string>(fullArgs.Where(a => !string.Equals(a, outputPath, StringComparison.OrdinalIgnoreCase)));
            pass1.AddRange(new[] { "-pass", "1", "-an", "-f", "null", tempOut });
            await RunFfmpegWithProgressAsync(WithHwaccel(pass1, hwaccel), totalDurationSec, null, null, ct).ConfigureAwait(false);
            var pass2 = new List<string>(fullArgs);
            pass2.Insert(pass2.Count - 1, "-pass");
            pass2.Insert(pass2.Count - 1, "2");
            await RunFfmpegWithProgressAsync(WithHwaccel(pass2, hwaccel), totalDurationSec, null, null, ct).ConfigureAwait(false);
        }

        public static async Task RunSnapshotAsync(
            string videoPath, string positionSec, string outputPath, CancellationToken ct)
        {
            await RunFfmpegAsync(new[]
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-ss", positionSec,
                "-i", videoPath,
                "-frames:v", "1",
                "-q:v", "2",
                outputPath
            }, ct).ConfigureAwait(false);
        }

        public static async Task RunExtractFramesOnlyAsync(
            VideoProcessingNode node,
            Action<string> onLog,
            Action<double, string> onProgress,
            string? outputFolderOverride,
            CancellationToken ct)
        {
            var outputFolder = string.IsNullOrWhiteSpace(outputFolderOverride)
                ? (node.UseDialogVideoConfig
                    ? (string.IsNullOrWhiteSpace(node.FrameOutputFolderPath)
                        ? Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Downloads",
                            "flow-frame")
                        : node.FrameOutputFolderPath!)
                    : (string.IsNullOrWhiteSpace(node.OutputPathOverride)
                        ? Path.Combine(Path.GetTempPath(), $"FlowMy_Frames_{DateTime.Now:yyyyMMddHHmmss}")
                        : Path.GetDirectoryName(node.OutputPathOverride)!))
                : outputFolderOverride.Trim();
            Directory.CreateDirectory(outputFolder);

            var extension = node.FrameOutputFormat switch { "jpg" => "jpg", "webp" => "webp", _ => "png" };
            var pattern = Path.Combine(outputFolder, $"frame_%06d.{extension}");
            var duration = await ProbeDurationSecondsAsync(node.VideoPath, ct).ConfigureAwait(false);
            var sourceFps = node.SourceFps > 0 ? node.SourceFps : await ProbeSourceFpsAsync(node.VideoPath, ct).ConfigureAwait(false);
            if (sourceFps <= 0) sourceFps = 30;

            string vfArg;
            var useVsync0 = false;
            if (node.ExtractAllFrames)
            {
                vfArg = BuildVideoFilterChain(node, Math.Max(1, sourceFps), includeTextOverlay: false);
            }
            else if (node.ExtractFps >= sourceFps)
            {
                vfArg = BuildVideoFilterChain(node, sourceFps, includeTextOverlay: false);
            }
            else
            {
                var framesPerSec = Math.Max(1, (int)Math.Round(node.ExtractFps));
                var selectExpr = FrameExtractionCalculator.BuildSelectFilterExpression(duration, sourceFps, framesPerSec);
                var otherFilters = BuildVideoFilterChainWithoutFps(node);
                vfArg = string.IsNullOrEmpty(otherFilters) ? selectExpr : $"{selectExpr},{otherFilters}";
                useVsync0 = true;
            }

            var baseArgs = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-i", node.VideoPath };
            if (extension == "jpg")
            {
                var qv = Math.Max(1, 31 - (int)(node.JpegQuality / 3.35));
                baseArgs.AddRange(new[] { "-q:v", qv.ToString(CultureInfo.InvariantCulture) });
            }
            baseArgs.AddRange(new[] { "-vf", vfArg });
            if (useVsync0) baseArgs.AddRange(new[] { "-vsync", "0" });
            baseArgs.Add(pattern);

            onLog($"📁 Output: {outputFolder}");
            onLog($"🎞 Mode: {(node.ExtractAllFrames ? "All frames" : $"{(int)Math.Round(node.ExtractFps)} frame/s với offset")}");
            onLog($"⚙ Filter: {vfArg}");

            await RunFfmpegWithProgressAsync(
                BuildTrimAwareArgs(node, baseArgs),
                duration, onProgress, onLog, ct).ConfigureAwait(false);

            var count = Directory.GetFiles(outputFolder, $"frame_*.{extension}").Length;
            onLog($"✅ Extracted {count} frames → {outputFolder}");
            onProgress(100, $"Done: {count} frames");
        }

        public static async Task RunBurnSubtitleAsync(
            VideoProcessingNode node,
            Action<string> onLog,
            Action<double, string> onProgress,
            CancellationToken ct)
        {
            var ext = Path.GetExtension(node.VideoPath);
            var output = Path.Combine(
                Path.GetDirectoryName(node.VideoPath)!,
                $"{Path.GetFileNameWithoutExtension(node.VideoPath)}_subtitled{ext}");

            var subPath = node.SubtitlePath!.Replace("\\", "\\\\").Replace(":", "\\:");
            var duration = await ProbeDurationSecondsAsync(node.VideoPath, ct).ConfigureAwait(false);
            onLog($"🔤 Burning subtitle: {node.SubtitlePath}");

            await RunFfmpegWithProgressAsync(new[]
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", node.VideoPath,
                "-vf", $"subtitles='{subPath}'",
                "-c:a", "copy",
                output
            }, duration, onProgress, onLog, ct).ConfigureAwait(false);

            onLog($"✅ Output: {output}");
            onProgress(100, "Burn subtitle done");
        }

        private static async Task StabilizeVideoAsync(string inputPath, string outputPath, CancellationToken ct)
        {
            var tempVectors = Path.Combine(Path.GetTempPath(), $"vidstab_{Guid.NewGuid():N}.trf");
            try
            {
                await RunFfmpegAsync(new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", inputPath,
                    "-vf", $"vidstabdetect=stepsize=6:shakiness=8:accuracy=9:result={tempVectors}",
                    "-f", "null", "-"
                }, ct).ConfigureAwait(false);

                await RunFfmpegAsync(new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", inputPath,
                    "-vf", $"vidstabtransform=input={tempVectors}:zoom=1:smoothing=30,unsharp=5:5:0.8",
                    outputPath
                }, ct).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(tempVectors)) File.Delete(tempVectors);
            }
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

        private static async Task RunFfmpegWithProgressAsync(
            IEnumerable<string> args,
            double totalDurationSec,
            Action<double, string>? onProgress,
            Action<string>? onLogLine,
            CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ResolveBinary("ffmpeg"),
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) throw new InvalidOperationException("Cannot start ffmpeg.");

            p.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                onLogLine?.Invoke(e.Data);
                var match = Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+(?:\.\d+)?)");
                if (match.Success && totalDurationSec > 0)
                {
                    var h = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    var m = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    var s = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                    var elapsed = h * 3600 + m * 60 + s;
                    var pct = Math.Min(100, elapsed / totalDurationSec * 100);
                    onProgress?.Invoke(pct, $"Processing... {elapsed:0}s / {totalDurationSec:0}s");
                }
            };
            p.BeginErrorReadLine();
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            if (p.ExitCode != 0) throw new InvalidOperationException("FFmpeg failed. Xem Log tab để biết chi tiết.");
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
