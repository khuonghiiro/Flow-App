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
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using FlowMy.Helpers;
using FlowMy.Services.Workflow;

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
                var sourceHeight = await ProbeSourceHeightAsync(videoInput, env.CancellationToken).ConfigureAwait(false);
                var sourceWidth = await ProbeSourceWidthAsync(videoInput, env.CancellationToken).ConfigureAwait(false);
                var sourceFpsClamped = Math.Max(0.001, videoNode.SourceFps);
                var extractFps = videoNode.ExtractAllFrames
                    ? sourceFpsClamped
                    : Math.Max(0.001, Math.Min(videoNode.ExtractFps, sourceFpsClamped));

                var hwaccel = await ResolveHwAccelAsync(videoNode.PreferGpu, env.CancellationToken).ConfigureAwait(false);
                videoNode.PreferredHwAccel = hwaccel;

                var frameFilter = BuildVideoFilterChain(videoNode, extractFps, includeTextOverlay: true, sourceHeight);
                LogLine?.Invoke(videoNode, $"[DBG] FilterGraph: {frameFilter}");
                LogLine?.Invoke(videoNode, $"[DBG] WatermarkExpr: {(videoNode.WatermarkEnabled ? VideoWatermarkGeometry.BuildOverlayPositionExpression(videoNode.WatermarkPosition, videoNode.WatermarkInsetFraction) : "disabled")}");
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
                var effectiveStart = videoNode.TrimEnabled ? Math.Max(0, videoNode.TrimStartSec) : 0;
                var effectiveEnd = videoNode.TrimEnabled && videoNode.TrimEndSec > effectiveStart
                    ? Math.Min(totalDuration, videoNode.TrimEndSec)
                    : totalDuration;
                var effectiveDurationTrim = Math.Max(0.01, effectiveEnd - effectiveStart);

                var frameArgs = new List<string>(BuildTrimAwareArgs(videoNode, new[] { "-y", "-hide_banner", "-loglevel", "error", "-i", videoInput }));
                if (frameExt == "jpg") frameArgs.AddRange(new[] { "-q:v", Math.Max(1, 31 - (videoNode.JpegQuality / 4)).ToString(CultureInfo.InvariantCulture) });
                var overlayFrameCleanup = new List<string>();
                AppendVisualFilterArgs(
                    frameArgs,
                    videoNode,
                    frameFilter,
                    frameLabels: null,
                    overlayFrameCleanup,
                    deferCanvasTextOverlayToWpfRaster: HasVisibleCanvasTextOverlays(videoNode),
                    overlayProbeSrcW: sourceWidth > 0 ? sourceWidth : 1920,
                    overlayProbeSrcH: sourceHeight > 0 ? sourceHeight : 1080,
                    overlayProbeSrcHForFontScale: Math.Max(sourceHeight, 1));
                frameArgs.Add(framePattern);
                try
                {
                    await RunFfmpegWithProgressAsync(
                        WithHwaccel(frameArgs, hwaccel),
                        totalDuration,
                        (pct, status) => ProgressChanged?.Invoke(videoNode, pct, status),
                        line => LogLine?.Invoke(videoNode, line),
                        env.CancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    TryDeleteOverlayRasterFiles(overlayFrameCleanup);
                }

                var producedFrames = Directory.GetFiles(
                        Path.GetDirectoryName(framePattern)!,
                        Path.GetFileName(framePattern).Replace("%06d", "*"))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (HasVisibleCanvasTextOverlays(videoNode) && producedFrames.Count > 0)
                {
                    await ApplyCanvasTextOverlaysToStillFilesAsync(
                            videoNode,
                            producedFrames,
                            sourceWidth > 0 ? sourceWidth : 1920,
                            sourceHeight > 0 ? sourceHeight : 1080,
                            Math.Max(sourceHeight, 1),
                            env.CancellationToken)
                        .ConfigureAwait(false);
                }

                if (videoNode.FrameLabelEnabled && producedFrames.Count > 0)
                {
                    if (videoNode.FrameLabelDebugSamplesEnabled)
                    {
                        var dbgDir = CreateFrameLabelDebugFolder("frames", Path.GetDirectoryName(framePattern));
                        var dbgCount = Math.Min(24, producedFrames.Count);
                        var srcFpsForDbg = videoNode.SourceFps > 0 ? videoNode.SourceFps : 30;
                        await RunWpfCompositorAsync(
                            () => FrameLabelRasterComposer.WriteLabelSequencePngs(
                                videoNode,
                                dbgDir,
                                dbgCount,
                                effectiveStart,
                                extractFps,
                                srcFpsForDbg,
                                sourceWidth > 0 ? sourceWidth : 1920,
                                sourceHeight > 0 ? sourceHeight : 1080,
                                Math.Max(sourceHeight, 1)),
                            env.CancellationToken).ConfigureAwait(false);
                        LogLine?.Invoke(videoNode, $"[DBG] FrameLabel samples: {dbgDir}");
                    }

                    await ApplyRasterFrameLabelsToStillFilesAsync(
                            videoNode,
                            producedFrames,
                            effectiveStart,
                            extractFps,
                            sourceWidth,
                            sourceHeight,
                            Math.Max(sourceHeight, 1),
                            env.CancellationToken)
                        .ConfigureAwait(false);
                }

                var framesOutput = videoNode.OutputBase64
                    ? JsonSerializer.Serialize(producedFrames.Select(File.ReadAllBytes).Select(Convert.ToBase64String).ToList())
                    : JsonSerializer.Serialize(producedFrames);
                SetOutput(videoNode, "frames_output", framesOutput);

                var (codecArgs, extension) = BuildOutputArgs(videoNode);
                var outputBasePath = Path.Combine(tempRoot, $"video_base_{Guid.NewGuid():N}{extension}");
                var mainFilter = BuildVideoFilterChain(videoNode, extractFps, includeTextOverlay: true, sourceHeight);
                var mainArgs = BuildTrimAwareArgs(videoNode, new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", videoInput,
                    "-sn",
                    "-an",
                }).ToList();

                string? lblEncDir = null;
                var overlayEncodeCleanup = new List<string>();
                try
                {
                    FrameLabelSequenceFfmpegInput? labelFf = null;
                    if (videoNode.FrameLabelEnabled)
                    {
                        lblEncDir = videoNode.FrameLabelDebugSamplesEnabled
                            ? CreateFrameLabelDebugFolder("enc_seq", Path.GetDirectoryName(framePattern))
                            : Path.Combine(tempRoot, $"enc_lbl_{Guid.NewGuid():N}");
                        var nLbl = Math.Max(producedFrames.Count + 48, (int)Math.Ceiling(effectiveDurationTrim * extractFps) + 64);
                        var srcFpsForSeq = videoNode.SourceFps > 0 ? videoNode.SourceFps : 30;
                        await RunWpfCompositorAsync(
                            () => FrameLabelRasterComposer.WriteLabelSequencePngs(
                                videoNode,
                                lblEncDir,
                                nLbl,
                                effectiveStart,
                                extractFps,
                                srcFpsForSeq,
                                sourceWidth > 0 ? sourceWidth : 1920,
                                sourceHeight > 0 ? sourceHeight : 1080,
                                Math.Max(sourceHeight, 1)),
                            env.CancellationToken).ConfigureAwait(false);

                        if (videoNode.FrameLabelDebugSamplesEnabled)
                            LogLine?.Invoke(videoNode, $"[DBG] FrameLabel sequence (encode): {lblEncDir}");

                        labelFf = new FrameLabelSequenceFfmpegInput(
                            Path.Combine(lblEncDir, "label_%06d.png").Replace('\\', '/'),
                            extractFps);
                    }

                    AppendVisualFilterArgs(
                        mainArgs,
                        videoNode,
                        mainFilter,
                        labelFf,
                        overlayEncodeCleanup,
                        deferCanvasTextOverlayToWpfRaster: false,
                        overlayProbeSrcW: sourceWidth > 0 ? sourceWidth : 1920,
                        overlayProbeSrcH: sourceHeight > 0 ? sourceHeight : 1080,
                        overlayProbeSrcHForFontScale: Math.Max(sourceHeight, 1));
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
                }
                finally
                {
                    TryDeleteOverlayRasterFiles(overlayEncodeCleanup);
                    if (!string.IsNullOrWhiteSpace(lblEncDir) && !videoNode.FrameLabelDebugSamplesEnabled)
                    {
                        try
                        {
                            if (Directory.Exists(lblEncDir))
                                Directory.Delete(lblEncDir, recursive: true);
                        }
                        catch
                        {
                            /* temp cleanup best-effort */
                        }
                    }
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

        private static string BuildVideoFilterChain(VideoProcessingNode node, double extractFps, bool includeTextOverlay, int? sourceHeightOverride = null)
        {
            var filters = new List<string>();

            filters.Add($"fps={extractFps:0.###}");
            filters.Add(VideoColorGrading.BuildEqFilter(node));
            var hueF = VideoColorGrading.BuildHueFilter(node.Hue);
            if (hueF != null) filters.Add(hueF);
            var gammaF = VideoColorGrading.BuildGammaLutRgbFilter(node.Gamma);
            if (gammaF != null) filters.Add(gammaF);
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

            if (Math.Abs(node.SpeedFactor - 1) > 0.01)
            {
                var pts = (1.0 / node.SpeedFactor).ToString("0.######", CultureInfo.InvariantCulture);
                filters.Add($"setpts={pts}*PTS");
            }
            if (includeTextOverlay && node.TextOverlayEnabled && !string.IsNullOrWhiteSpace(node.OverlayText))
            {
                var escapedText = node.OverlayText.Replace("\\", "\\\\").Replace(":", "\\:").Replace("'", "\\'");
                var (xExpr, yExpr) = BuildTextPositionExpression(node.TextPosition, 10);
                var fontPath = ResolveFontPath(node.OverlayFont);
                var sourceScale = ComputeFrameLabelSourceScale(sourceHeightOverride);
                var textSize = Math.Max(10, (int)Math.Round(node.OverlayFontSize * sourceScale));
                filters.Add($"drawtext=text='{escapedText}':fontfile='{fontPath}':fontsize={textSize}:fontcolor={node.OverlayFontColor}:x={xExpr}:y={yExpr}");
            }
            if (node.BurnSubtitleEnabled && !string.IsNullOrWhiteSpace(node.SubtitlePath))
            {
                var subPath = node.SubtitlePath!.Replace("\\", "/").Replace(":", "\\:");
                filters.Add($"subtitles='{subPath}'");
            }
            return string.Join(",", filters);
        }

        private sealed record FrameLabelSequenceFfmpegInput(string AbsolutePatternPath, double Fps);

        /// <summary>
        /// Debug PNGs go under <c>{frameOutputDirectory}\debug-frame-label\</c> when a frame output folder is known; otherwise %TEMP%\FlowMy_VideoProcessing\debug-frame-label\.
        /// </summary>
        private static string CreateFrameLabelDebugFolder(string modeTag, string? frameOutputDirectory)
        {
            string root;
            if (!string.IsNullOrWhiteSpace(frameOutputDirectory))
            {
                root = Path.Combine(frameOutputDirectory.Trim(), "debug-frame-label");
            }
            else
            {
                root = Path.Combine(Path.GetTempPath(), "FlowMy_VideoProcessing", "debug-frame-label");
            }

            Directory.CreateDirectory(root);
            var dir = Path.Combine(root, $"{DateTime.Now:yyyyMMdd_HHmmss}_{modeTag}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static bool HasVisibleCanvasTextOverlays(VideoProcessingNode node) =>
            node.Overlays.Any(o =>
                o.IsVisible &&
                string.Equals((o.Type ?? string.Empty).Trim(), "text", StringComparison.OrdinalIgnoreCase));

        private static void TryDeleteOverlayRasterFiles(IEnumerable<string>? paths)
        {
            if (paths == null) return;
            foreach (var p in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
                        File.Delete(p);
                }
                catch
                {
                    /* temp cleanup best-effort */
                }
            }
        }

        private static (string filterComplex, List<string> imageInputs, string outputLabel) BuildOverlayFilterComplex(
            VideoProcessingNode node,
            string baseFilter,
            bool rasterFrameLabelUsesInput1,
            List<string>? collectDisposableOverlayRasters,
            bool deferCanvasTextOverlayToWpfRasterOnStills,
            int overlayProbeSrcW,
            int overlayProbeSrcH,
            int overlayProbeSrcHForFontScale)
        {
            var pw = overlayProbeSrcW > 0 ? overlayProbeSrcW : 1920;
            var ph = overlayProbeSrcH > 0 ? overlayProbeSrcH : 1080;
            var phf = overlayProbeSrcHForFontScale > 0 ? overlayProbeSrcHForFontScale : ph;
            var (estW, estH) = FrameLabelRasterComposer.GetEstimatedSourceFrameSize(pw, ph, node);

            var filterChains = new List<string> { $"[0:v]{baseFilter}[v0]" };
            var imageInputs = new List<string>();
            var currentLabel = "v0";
            var imageInputIndex = rasterFrameLabelUsesInput1 ? 2 : 1;
            var stageIndex = 0;

            if (rasterFrameLabelUsesInput1)
            {
                var nextLabel = $"v{++stageIndex}";
                var fx = node.FrameLabelX.ToString("0.######", CultureInfo.InvariantCulture);
                var fy = node.FrameLabelY.ToString("0.######", CultureInfo.InvariantCulture);
                filterChains.Add($"[{currentLabel}][1:v]overlay=x='W*{fx}':y='H*{fy}':shortest=1:format=auto[{nextLabel}]");
                currentLabel = nextLabel;
            }

            if (node.WatermarkEnabled && !string.IsNullOrWhiteSpace(node.WatermarkImagePath) && File.Exists(node.WatermarkImagePath))
            {
                var overlayAlpha = node.WatermarkOpacity.ToString("0.###", CultureInfo.InvariantCulture);
                var wScaled = $"wms{stageIndex}";
                var wRgba = $"wmrgba{stageIndex}";
                var vRef = $"vref{stageIndex}";
                var nextLabel = $"v{++stageIndex}";
                var wExpr = VideoWatermarkGeometry.BuildScaleWidthExpression(node.WatermarkWidthFraction);
                var xy = VideoWatermarkGeometry.BuildOverlayPositionExpression(node.WatermarkPosition, node.WatermarkInsetFraction);
                imageInputs.Add(node.WatermarkImagePath!);
                // scale2ref: input0 = stream to resize (logo), input1 = ref (main); out0=scaled logo, out1=unchanged main
                filterChains.Add($"[{imageInputIndex}:v][{currentLabel}]scale2ref=w={wExpr}:h=-2[{wScaled}][{vRef}]");
                filterChains.Add($"[{wScaled}]format=rgba,colorchannelmixer=aa={overlayAlpha}[{wRgba}]");
                filterChains.Add($"[{vRef}][{wRgba}]overlay={xy}[{nextLabel}]");
                currentLabel = nextLabel;
                imageInputIndex++;
            }

            foreach (var item in node.Overlays.Where(o => o.IsVisible))
            {
                var type = (item.Type ?? string.Empty).Trim().ToLowerInvariant();
                if (deferCanvasTextOverlayToWpfRasterOnStills && type == "text")
                    continue;

                var xExpr = $"(W*{item.X.ToString("0.######", CultureInfo.InvariantCulture)})";
                var yExpr = $"(H*{item.Y.ToString("0.######", CultureInfo.InvariantCulture)})";
                var opacity = Math.Clamp(item.Opacity, 0, 1).ToString("0.###", CultureInfo.InvariantCulture);
                var nextLabel = $"v{++stageIndex}";
                var wfStr = Math.Clamp(item.Width, 0.01, 1).ToString("0.######", CultureInfo.InvariantCulture);
                var hfStr = Math.Clamp(item.Height, 0.01, 1).ToString("0.######", CultureInfo.InvariantCulture);
                // Use explicit pixel scaling for overlay assets to avoid fragile scale2ref behavior/crashes.
                var overlayPixelW = Math.Max(1, (int)Math.Round(estW * Math.Clamp(item.Width, 0.01, 1)));
                var overlayPixelH = Math.Max(1, (int)Math.Round(estH * Math.Clamp(item.Height, 0.01, 1)));
                var ovScaled = $"ovscl{stageIndex}";
                var ovRgba = $"ovrgba{stageIndex}";

                if ((type == "image" || type == "logo") && !string.IsNullOrWhiteSpace(item.Source) && File.Exists(item.Source))
                {
                    imageInputs.Add(item.Source);
                    filterChains.Add($"[{imageInputIndex}:v]scale=w={overlayPixelW}:h={overlayPixelH}[{ovScaled}]");
                    filterChains.Add($"[{ovScaled}]format=rgba,colorchannelmixer=aa={opacity}[{ovRgba}]");
                    filterChains.Add($"[{currentLabel}][{ovRgba}]overlay=x='{xExpr}':y='{yExpr}':format=auto[{nextLabel}]");
                    imageInputIndex++;
                    currentLabel = nextLabel;
                }
                else if (type == "text")
                {
                    var rasterTextPath = RenderOverlayTextRasterPng(item, node, pw, ph, phf);
                    collectDisposableOverlayRasters?.Add(rasterTextPath);

                    imageInputs.Add(rasterTextPath);
                    // Text PNG is already rendered to the target box pixel size (matches OverlayItemControl AutoFit).
                    // Overlay directly to avoid any resampling that would drift font size/metrics from UI.
                    filterChains.Add($"[{imageInputIndex}:v]format=rgba,colorchannelmixer=aa={opacity}[{ovRgba}]");
                    filterChains.Add($"[{currentLabel}][{ovRgba}]overlay=x='{xExpr}':y='{yExpr}':alpha=1:format=auto[{nextLabel}]");
                    imageInputIndex++;
                    currentLabel = nextLabel;
                }
            }

            // Apply all output-size transforms at the very end:
            // watermark/text/overlays are first composed on source frame, then resized once.
            var tailScale = BuildTailScaleFilter(node);
            if (!string.IsNullOrWhiteSpace(tailScale))
            {
                var nextLabelTail = $"v{++stageIndex}";
                filterChains.Add($"[{currentLabel}]{tailScale}[{nextLabelTail}]");
                currentLabel = nextLabelTail;
            }

            return (string.Join(";", filterChains), imageInputs, $"[{currentLabel}]");
        }

        private static string BuildVideoFilterChainWithoutFps(VideoProcessingNode node, bool includeTextOverlay = false, double? sourceFpsOverride = null, int? sourceHeightOverride = null)
        {
            var filters = new List<string>();

            filters.Add(VideoColorGrading.BuildEqFilter(node));
            var hueNoFps = VideoColorGrading.BuildHueFilter(node.Hue);
            if (hueNoFps != null) filters.Add(hueNoFps);
            var gammaNoFps = VideoColorGrading.BuildGammaLutRgbFilter(node.Gamma);
            if (gammaNoFps != null) filters.Add(gammaNoFps);
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

            if (includeTextOverlay && node.TextOverlayEnabled && !string.IsNullOrWhiteSpace(node.OverlayText))
            {
                var escapedText = node.OverlayText.Replace("\\", "\\\\").Replace(":", "\\:").Replace("'", "\\'");
                var (xExpr, yExpr) = BuildTextPositionExpression(node.TextPosition, 10);
                var fontPath = ResolveFontPath(node.OverlayFont);
                var sourceScale = ComputeFrameLabelSourceScale(sourceHeightOverride);
                var textSize = Math.Max(10, (int)Math.Round(node.OverlayFontSize * sourceScale));
                filters.Add($"drawtext=text='{escapedText}':fontfile='{fontPath}':fontsize={textSize}:fontcolor={node.OverlayFontColor}:x={xExpr}:y={yExpr}");
            }
            if (node.BurnSubtitleEnabled && !string.IsNullOrWhiteSpace(node.SubtitlePath))
            {
                var subPath = node.SubtitlePath!.Replace("\\", "/").Replace(":", "\\:");
                filters.Add($"subtitles='{subPath}'");
            }

            return filters.Count > 0 ? string.Join(",", filters) : string.Empty;
        }

        private static void AppendVisualFilterArgs(
            List<string> args,
            VideoProcessingNode node,
            string baseFilter,
            FrameLabelSequenceFfmpegInput? frameLabels = null,
            List<string>? collectDisposableOverlayRasters = null,
            bool deferCanvasTextOverlayToWpfRaster = false,
            int overlayProbeSrcW = 0,
            int overlayProbeSrcH = 0,
            int overlayProbeSrcHForFontScale = 0)
        {
            var hasWatermark = node.WatermarkEnabled &&
                               !string.IsNullOrWhiteSpace(node.WatermarkImagePath) &&
                               File.Exists(node.WatermarkImagePath);
            var hasNonDeferredCanvasLayer = node.Overlays.Any(o =>
            {
                if (!o.IsVisible) return false;
                var t = (o.Type ?? string.Empty).Trim();
                if (string.Equals(t, "text", StringComparison.OrdinalIgnoreCase))
                    return !deferCanvasTextOverlayToWpfRaster;
                return true;
            });
            var useRasterLabels = frameLabels != null && node.FrameLabelEnabled;

            if (useRasterLabels)
            {
                args.AddRange(new[]
                {
                    "-framerate", frameLabels!.Fps.ToString("0.###", CultureInfo.InvariantCulture),
                    "-start_number", "1",
                    "-i", frameLabels.AbsolutePatternPath
                });
            }

            if (hasNonDeferredCanvasLayer || hasWatermark)
            {
                var (overlayFilter, imageInputs, outputLabel) = BuildOverlayFilterComplex(
                    node,
                    baseFilter,
                    useRasterLabels,
                    collectDisposableOverlayRasters,
                    deferCanvasTextOverlayToWpfRaster,
                    overlayProbeSrcW,
                    overlayProbeSrcH,
                    overlayProbeSrcHForFontScale);
                foreach (var inputPath in imageInputs)
                {
                    args.AddRange(new[] { "-i", inputPath });
                }

                args.AddRange(new[]
                {
                    "-filter_complex", overlayFilter,
                    "-map", outputLabel
                });
                return;
            }

            if (useRasterLabels)
            {
                var fx = node.FrameLabelX.ToString("0.######", CultureInfo.InvariantCulture);
                var fy = node.FrameLabelY.ToString("0.######", CultureInfo.InvariantCulture);
                var tailScale = BuildTailScaleFilter(node);
                string chain;
                if (string.IsNullOrWhiteSpace(tailScale))
                {
                    chain = $"[0:v]{baseFilter}[m1];[m1][1:v]overlay=x='W*{fx}':y='H*{fy}':shortest=1:format=auto[outv]";
                }
                else
                {
                    chain = $"[0:v]{baseFilter}[m1];[m1][1:v]overlay=x='W*{fx}':y='H*{fy}':shortest=1:format=auto[flb];[flb]{tailScale}[outv]";
                }

                args.AddRange(new[]
                {
                    "-filter_complex", chain,
                    "-map", "[outv]"
                });
                return;
            }

            var finalFilter = baseFilter;
            var tail = BuildTailScaleFilter(node);
            if (!string.IsNullOrWhiteSpace(tail))
                finalFilter = string.IsNullOrWhiteSpace(finalFilter) ? tail : $"{finalFilter},{tail}";
            args.AddRange(new[] { "-vf", finalFilter });
        }

        private static (string x, string y) BuildTextPositionExpression(string? position, int paddingPx)
        {
            var pos = string.IsNullOrWhiteSpace(position) ? "BR" : position.Trim().ToUpperInvariant();
            var x = pos.EndsWith('L') ? $"{paddingPx}" :
                    pos.EndsWith('C') ? "(w-tw)/2" :
                    $"w-tw-{paddingPx}";
            var y = pos.StartsWith('T') ? $"{paddingPx}" :
                    pos.StartsWith('M') ? "(h-th)/2" :
                    $"h-th-{paddingPx}";
            return (x, y);
        }

        /// <summary>Same scaling as FFmpeg drawtext for frame label (based on probed/source height).</summary>
        internal static double ComputeFrameLabelSourceScale(int? sourceHeightPx) =>
            sourceHeightPx.HasValue && sourceHeightPx.Value > 0
                ? Math.Clamp(sourceHeightPx.Value / 720.0, 0.6, 3.0)
                : 1.0;

        internal static int ComputeFrameLabelDrawtextFontPixelSize(VideoProcessingNode node, int? sourceHeightPx)
        {
            var sourceScale = ComputeFrameLabelSourceScale(sourceHeightPx);
            return Math.Max(8, (int)Math.Round((node.FrameLabelFontSize + 2) * sourceScale));
        }

        private static string BuildTailScaleFilter(VideoProcessingNode node)
        {
            var parts = new List<string>();
            if (node.FixedResolutionHeight.HasValue)
            {
                parts.Add($"scale=-2:{node.FixedResolutionHeight.Value}");
            }
            else if (Math.Abs(node.ResolutionScale - 1) > 0.01)
            {
                var sc = node.ResolutionScale.ToString("0.###", CultureInfo.InvariantCulture);
                parts.Add($"scale=iw*{sc}:ih*{sc}");
            }
            if (node.FrameResizeScale < 0.999)
            {
                var sc = node.FrameResizeScale.ToString("0.###", CultureInfo.InvariantCulture);
                parts.Add($"scale=iw*{sc}:ih*{sc}");
            }
            return string.Join(",", parts);
        }

        private static string RenderOverlayTextRasterPng(OverlayItem item, VideoProcessingNode node, int probeSrcW, int probeSrcH, int probeSrcHForFont)
        {
            var (estW, estH) = FrameLabelRasterComposer.GetEstimatedSourceFrameSize(probeSrcW, probeSrcH, node);
            var boxW = Math.Max(16, (int)Math.Round(estW * Math.Clamp(item.Width, 0.01, 1)));
            var boxH = Math.Max(16, (int)Math.Round(estH * Math.Clamp(item.Height, 0.01, 1)));
            // Match OverlayItemControl.AutoFitTextContent(): base font size scales with parent surface height (/1080).
            // For video-encode overlays we approximate parent surface height using estimated source height.
            var parentSurfaceH = estH > 0 ? estH : (probeSrcH > 0 ? probeSrcH : 1080);

            var bmp = FrameLabelRasterComposer.RenderOverlayTextStripBitmap(item, boxW, boxH, parentSurfaceHeightPx: parentSurfaceH);

            var path = Path.Combine(Path.GetTempPath(), $"flow_overlay_text_{Guid.NewGuid():N}.png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = File.Create(path);
            enc.Save(fs);
            return path;
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
                "-i", videoPath,
                "-ss", positionSec,
                "-frames:v", "1",
                "-q:v", "2",
                outputPath
            }, ct).ConfigureAwait(false);
        }

        public static async Task RunSnapshotAsync(
            VideoProcessingNode node, string positionSec, string outputPath, CancellationToken ct)
        {
            var sourceFps = node.SourceFps > 0 ? node.SourceFps : await ProbeSourceFpsAsync(node.VideoPath, ct).ConfigureAwait(false);
            if (sourceFps <= 0) sourceFps = 30;
            var sourceHeight = await ProbeSourceHeightAsync(node.VideoPath, ct).ConfigureAwait(false);
            var sourceWidth = await ProbeSourceWidthAsync(node.VideoPath, ct).ConfigureAwait(false);
            var vf = BuildVideoFilterChainWithoutFps(node, includeTextOverlay: true, sourceFpsOverride: sourceFps, sourceHeightOverride: sourceHeight);

            var args = new List<string>
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", node.VideoPath
            };
            args.AddRange(new[] { "-ss", positionSec });
            var overlayRasterCleanup = new List<string>();
            AppendVisualFilterArgs(
                args,
                node,
                vf,
                frameLabels: null,
                overlayRasterCleanup,
                deferCanvasTextOverlayToWpfRaster: HasVisibleCanvasTextOverlays(node),
                overlayProbeSrcW: sourceWidth > 0 ? sourceWidth : 1920,
                overlayProbeSrcH: sourceHeight > 0 ? sourceHeight : 1080,
                overlayProbeSrcHForFontScale: Math.Max(sourceHeight, 1));
            if (string.Equals(Path.GetExtension(outputPath), ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(outputPath), ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                args.AddRange(new[] { "-q:v", "2" });
            }
            args.AddRange(new[] { "-frames:v", "1" });
            args.Add(outputPath);
            try
            {
                await RunFfmpegAsync(args, ct).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteOverlayRasterFiles(overlayRasterCleanup);
            }

            if (HasVisibleCanvasTextOverlays(node) && File.Exists(outputPath))
            {
                await RunWpfCompositorAsync(
                    () => FrameLabelRasterComposer.CompositeCanvasTextOverlaysOntoStillFile(
                        node,
                        outputPath,
                        sourceWidth > 0 ? sourceWidth : 1920,
                        sourceHeight > 0 ? sourceHeight : 1080,
                        Math.Max(sourceHeight, 1)),
                    ct).ConfigureAwait(false);
            }

            if (!node.FrameLabelEnabled || !File.Exists(outputPath))
                return;

            if (!double.TryParse(positionSec.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var posSec))
                posSec = 0;
            var effStart = node.TrimEnabled ? Math.Max(0, node.TrimStartSec) : 0;
            var effFps = node.ExtractAllFrames
                ? Math.Max(0.001, sourceFps)
                : Math.Max(0.001, Math.Min(node.ExtractFps, sourceFps));
            var rel = Math.Max(0, posSec - effStart);
            var idx0 = Math.Max(0, (int)Math.Floor(rel * effFps));

            if (node.FrameLabelDebugSamplesEnabled)
            {
                var dbgDir = CreateFrameLabelDebugFolder("snapshot", Path.GetDirectoryName(outputPath));
                await RunWpfCompositorAsync(
                    () => FrameLabelRasterComposer.WriteLabelSequencePngs(
                        node,
                        dbgDir,
                        1,
                        effStart,
                        effFps,
                        sourceFps,
                        sourceWidth > 0 ? sourceWidth : 1920,
                        sourceHeight > 0 ? sourceHeight : 1080,
                        Math.Max(sourceHeight, 1)),
                    ct).ConfigureAwait(false);
            }

            await RunWpfCompositorAsync(
                    () => FrameLabelRasterComposer.CompositeLabelOntoStillFile(
                        node,
                        outputPath,
                        idx0,
                        effStart,
                        effFps,
                        sourceFps,
                        sourceWidth > 0 ? sourceWidth : 1920,
                        sourceHeight > 0 ? sourceHeight : 1080,
                        Math.Max(sourceHeight, 1)),
                    ct)
                .ConfigureAwait(false);
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
            var sourceHeight = await ProbeSourceHeightAsync(node.VideoPath, ct).ConfigureAwait(false);
            var sourceWidth = await ProbeSourceWidthAsync(node.VideoPath, ct).ConfigureAwait(false);
            if (sourceFps <= 0) sourceFps = 30;
            var effectiveStart = node.TrimEnabled ? Math.Max(0, node.TrimStartSec) : 0;
            var effectiveEnd = node.TrimEnabled && node.TrimEndSec > effectiveStart ? Math.Min(duration, node.TrimEndSec) : duration;
            var effectiveDuration = Math.Max(0.01, effectiveEnd - effectiveStart);

            string vfArg;
            var useVsync0 = false;
            if (node.ExtractAllFrames)
            {
                vfArg = BuildVideoFilterChain(node, Math.Max(0.001, sourceFps), includeTextOverlay: true, sourceHeight);
            }
            else if (node.ExtractFps >= sourceFps)
            {
                vfArg = BuildVideoFilterChain(node, sourceFps, includeTextOverlay: true, sourceHeight);
            }
            else
            {
                // Allow fractional FPS (e.g. 0.333 fps) directly in the fps filter.
                // This avoids rounding extractFps to an integer frame-per-second.
                vfArg = BuildVideoFilterChain(node, Math.Max(0.001, node.ExtractFps), includeTextOverlay: true, sourceHeight);
            }

            var effectiveExtractFps = node.ExtractAllFrames
                ? Math.Max(0.001, sourceFps)
                : (node.ExtractFps >= sourceFps ? sourceFps : Math.Max(0.001, node.ExtractFps));

            onLog($"📁 Output: {outputFolder}");
            onLog($"🎞 Mode: {(node.ExtractAllFrames ? "All frames" : $"{node.ExtractFps:0.###} frame/s với offset")}");
            onLog($"🧵 Parallel jobs: {node.ExtractParallelJobs}");
            onLog($"⚙ Filter: {vfArg}");

            var maxJobs = Math.Clamp(node.ExtractParallelJobs, 1, 8);
            var probeW = sourceWidth > 0 ? sourceWidth : 1920;
            var probeH = sourceHeight > 0 ? sourceHeight : 1080;
            var probeHFont = Math.Max(sourceHeight, 1);

            if (maxJobs <= 1 || effectiveDuration < 20)
            {
                var overlayRasterCleanup = new List<string>();
                try
                {
                    var baseArgs = BuildExtractArgs(
                        node,
                        vfArg,
                        pattern,
                        extension,
                        useVsync0,
                        effectiveStart,
                        effectiveEnd,
                        overlayRasterCleanup,
                        deferCanvasTextOverlayToWpfRaster: HasVisibleCanvasTextOverlays(node),
                        overlayProbeSrcW: probeW,
                        overlayProbeSrcH: probeH,
                        overlayProbeSrcHForFontScale: probeHFont);
                    await RunFfmpegWithProgressAsync(baseArgs, effectiveDuration, onProgress, onLog, ct).ConfigureAwait(false);
                }
                finally
                {
                    TryDeleteOverlayRasterFiles(overlayRasterCleanup);
                }
            }
            else
            {
                var jobCount = Math.Min(maxJobs, Math.Max(1, (int)Math.Ceiling(effectiveDuration / 8.0)));
                var chunkDuration = effectiveDuration / jobCount;
                var chunkProgress = new double[jobCount];
                var chunkFolders = new List<string>(jobCount);
                var tasks = new List<Task>(jobCount);

                for (var i = 0; i < jobCount; i++)
                {
                    var chunkStart = effectiveStart + i * chunkDuration;
                    var chunkEnd = i == jobCount - 1 ? effectiveEnd : Math.Min(effectiveEnd, chunkStart + chunkDuration);
                    var chunkFolder = Path.Combine(outputFolder, $"__chunk_{i:D2}");
                    Directory.CreateDirectory(chunkFolder);
                    chunkFolders.Add(chunkFolder);
                    var chunkPattern = Path.Combine(chunkFolder, $"frame_%06d.{extension}");
                    var idx = i;

                    tasks.Add(Task.Run(async () =>
                    {
                        var overlayRasterCleanup = new List<string>();
                        try
                        {
                            var args = BuildExtractArgs(
                                node,
                                vfArg,
                                chunkPattern,
                                extension,
                                useVsync0,
                                chunkStart,
                                chunkEnd,
                                overlayRasterCleanup,
                                deferCanvasTextOverlayToWpfRaster: HasVisibleCanvasTextOverlays(node),
                                overlayProbeSrcW: probeW,
                                overlayProbeSrcH: probeH,
                                overlayProbeSrcHForFontScale: probeHFont);
                            onLog($"▶ Chunk {idx + 1}/{jobCount}: {chunkStart:0.##}s -> {chunkEnd:0.##}s");
                            await RunFfmpegWithProgressAsync(
                                args,
                                Math.Max(0.01, chunkEnd - chunkStart),
                                (pct, status) =>
                                {
                                    chunkProgress[idx] = pct;
                                    onProgress(chunkProgress.Average(), $"Extracting chunks... {status}");
                                },
                                line => onLog($"[C{idx + 1}] {line}"),
                                ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            TryDeleteOverlayRasterFiles(overlayRasterCleanup);
                        }
                    }, ct));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                var outIndex = 1;
                foreach (var folder in chunkFolders)
                {
                    var files = Directory.GetFiles(folder, $"frame_*.{extension}")
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    foreach (var file in files)
                    {
                        var destination = Path.Combine(outputFolder, $"frame_{outIndex:D6}.{extension}");
                        if (File.Exists(destination)) File.Delete(destination);
                        File.Move(file, destination);
                        outIndex++;
                    }
                    Directory.Delete(folder, true);
                }
            }

            var orderedStills = Directory.GetFiles(outputFolder, $"frame_*.{extension}")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (orderedStills.Count > 0)
            {
                await ApplyCanvasTextOverlaysToStillFilesAsync(
                        node,
                        orderedStills,
                        probeW,
                        probeH,
                        probeHFont,
                        ct)
                    .ConfigureAwait(false);
            }

            if (node.FrameLabelEnabled)
            {
                if (node.FrameLabelDebugSamplesEnabled && orderedStills.Count > 0)
                {
                    var dbgDir = CreateFrameLabelDebugFolder("extract", outputFolder);
                    var dbgCount = Math.Min(24, Math.Max(1, (int)Math.Ceiling(effectiveDuration * effectiveExtractFps)));
                    await RunWpfCompositorAsync(
                        () => FrameLabelRasterComposer.WriteLabelSequencePngs(
                            node,
                            dbgDir,
                            dbgCount,
                            effectiveStart,
                            effectiveExtractFps,
                            sourceFps,
                            probeW,
                            probeH,
                            probeHFont),
                        ct).ConfigureAwait(false);
                    onLog($"[DBG] FrameLabel samples: {dbgDir}");
                }

                if (orderedStills.Count > 0)
                {
                    await ApplyRasterFrameLabelsToStillFilesAsync(
                            node,
                            orderedStills,
                            effectiveStart,
                            effectiveExtractFps,
                            sourceWidth,
                            sourceHeight,
                            Math.Max(sourceHeight, 1),
                            ct)
                        .ConfigureAwait(false);
                }
            }

            var count = Directory.GetFiles(outputFolder, $"frame_*.{extension}").Length;
            onLog($"✅ Extracted {count} frames → {outputFolder}");
            onProgress(100, $"Done: {count} frames");
        }

        private static List<string> BuildExtractArgs(
            VideoProcessingNode node,
            string vfArg,
            string outputPattern,
            string extension,
            bool useVsync0,
            double startSec,
            double endSec,
            List<string>? collectDisposableOverlayRasters,
            bool deferCanvasTextOverlayToWpfRaster,
            int overlayProbeSrcW,
            int overlayProbeSrcH,
            int overlayProbeSrcHForFontScale)
        {
            var args = new List<string>
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-ss", startSec.ToString("0.###", CultureInfo.InvariantCulture),
                "-to", endSec.ToString("0.###", CultureInfo.InvariantCulture),
                "-i", node.VideoPath
            };

            AppendVisualFilterArgs(
                args,
                node,
                vfArg,
                frameLabels: null,
                collectDisposableOverlayRasters,
                deferCanvasTextOverlayToWpfRaster,
                overlayProbeSrcW,
                overlayProbeSrcH,
                overlayProbeSrcHForFontScale);
            if (extension == "jpg")
            {
                var qv = Math.Max(1, 31 - (int)(node.JpegQuality / 3.35));
                args.AddRange(new[] { "-q:v", qv.ToString(CultureInfo.InvariantCulture) });
            }
            if (useVsync0) args.AddRange(new[] { "-vsync", "0" });
            args.Add(outputPattern);
            return args;
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

        private static async Task<int> ProbeSourceHeightAsync(string inputPath, CancellationToken ct)
        {
            var args = new[]
            {
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "stream=height",
                "-of", "default=nokey=1:noprint_wrappers=1",
                inputPath
            };
            var output = await RunProcessCaptureAsync(ResolveBinary("ffprobe"), args, ct).ConfigureAwait(false);
            return int.TryParse(output.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) ? h : 0;
        }

        private static async Task<int> ProbeSourceWidthAsync(string inputPath, CancellationToken ct)
        {
            var args = new[]
            {
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "stream=width",
                "-of", "default=nokey=1:noprint_wrappers=1",
                inputPath
            };
            var output = await RunProcessCaptureAsync(ResolveBinary("ffprobe"), args, ct).ConfigureAwait(false);
            return int.TryParse(output.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) ? w : 0;
        }

        private static Task RunWpfCompositorAsync(Action work, CancellationToken ct)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                return dispatcher.InvokeAsync(work, DispatcherPriority.Normal, ct).Task;
            return Task.Run(work, ct);
        }

        private static async Task ApplyCanvasTextOverlaysToStillFilesAsync(
            VideoProcessingNode node,
            IReadOnlyList<string> framePathsOrdered,
            int probeSrcW,
            int probeSrcH,
            int probeSrcHForFont,
            CancellationToken ct)
        {
            if (!HasVisibleCanvasTextOverlays(node) || framePathsOrdered.Count == 0) return;

            var wFall = probeSrcW > 0 ? probeSrcW : 1920;
            var hFall = probeSrcH > 0 ? probeSrcH : 1080;
            var hFontProbe = probeSrcHForFont > 0 ? probeSrcHForFont : hFall;

            await RunWpfCompositorAsync(() =>
            {
                foreach (var path in framePathsOrdered)
                {
                    ct.ThrowIfCancellationRequested();
                    FrameLabelRasterComposer.CompositeCanvasTextOverlaysOntoStillFile(node, path, wFall, hFall, hFontProbe);
                }
            }, ct).ConfigureAwait(false);
        }

        private static async Task ApplyRasterFrameLabelsToStillFilesAsync(
            VideoProcessingNode node,
            IReadOnlyList<string> framePathsOrdered,
            double timelineStartSec,
            double extractFps,
            int probeSrcW,
            int probeSrcH,
            int probeSrcHForFont,
            CancellationToken ct)
        {
            if (!node.FrameLabelEnabled || framePathsOrdered.Count == 0) return;

            var srcFps = node.SourceFps > 0 ? node.SourceFps : 30;
            var wFall = probeSrcW > 0 ? probeSrcW : 1920;
            var hFall = probeSrcH > 0 ? probeSrcH : 1080;
            var hFontProbe = probeSrcHForFont > 0 ? probeSrcHForFont : hFall;

            await RunWpfCompositorAsync(() =>
            {
                for (var i = 0; i < framePathsOrdered.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    FrameLabelRasterComposer.CompositeLabelOntoStillFile(
                        node, framePathsOrdered[i], i, timelineStartSec, extractFps, srcFps, wFall, hFall, hFontProbe);
                }
            }, ct).ConfigureAwait(false);
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
            static string QuoteArg(string a)
            {
                if (string.IsNullOrEmpty(a)) return "\"\"";
                if (a.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"', '\'' }) >= 0)
                    return "\"" + a.Replace("\"", "\\\"") + "\"";
                return a;
            }

            var psi = new ProcessStartInfo
            {
                FileName = ResolveBinary("ffmpeg"),
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-nostats");
            psi.ArgumentList.Add("-progress");
            psi.ArgumentList.Add("pipe:2");
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) throw new InvalidOperationException("Cannot start ffmpeg.");

            // Print the command once for debugging.
            try
            {
                var cmd = string.Join(" ", new[] { psi.FileName }.Concat(psi.ArgumentList).Select(QuoteArg));
                onLogLine?.Invoke($"[FFMPEG] {cmd}");
            }
            catch
            {
                /* best-effort */
            }

            var ring = new Queue<string>(256);
            void Remember(string line)
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                if (ring.Count >= 200) ring.Dequeue();
                ring.Enqueue(line);
            }

            p.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                Remember(e.Data);
                onLogLine?.Invoke(e.Data);
            };
            p.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                Remember(e.Data);
                if (!e.Data.StartsWith("frame=", StringComparison.OrdinalIgnoreCase) &&
                    !e.Data.StartsWith("fps=", StringComparison.OrdinalIgnoreCase) &&
                    !e.Data.StartsWith("progress=", StringComparison.OrdinalIgnoreCase) &&
                    !e.Data.StartsWith("out_time_ms=", StringComparison.OrdinalIgnoreCase) &&
                    !e.Data.StartsWith("speed=", StringComparison.OrdinalIgnoreCase))
                {
                    onLogLine?.Invoke(e.Data);
                }

                var outTimeMatch = Regex.Match(e.Data, @"out_time_ms=(\d+)");
                if (outTimeMatch.Success && totalDurationSec > 0)
                {
                    var outTimeSec = double.Parse(outTimeMatch.Groups[1].Value, CultureInfo.InvariantCulture) / 1000000d;
                    var pct2 = Math.Min(100, outTimeSec / totalDurationSec * 100);
                    onProgress?.Invoke(pct2, $"Processing... {outTimeSec:0}s / {totalDurationSec:0}s");
                    return;
                }

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
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            if (p.ExitCode != 0)
            {
                onLogLine?.Invoke($"[FFMPEG] ExitCode={p.ExitCode}");
                foreach (var line in ring.TakeLast(80))
                    onLogLine?.Invoke(line);
                throw new InvalidOperationException("FFmpeg failed. Xem Log tab để biết chi tiết.");
            }
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
