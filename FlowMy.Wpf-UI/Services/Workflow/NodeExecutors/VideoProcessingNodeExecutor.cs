// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using FlowMy.Helpers;
using FlowMy.Services.Workflow;
using FlowMy.Services.Workflow.Audio;

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
            var globalSubtitleCleanup = new List<string>();

            try
            {
                videoNode.EnsureStandardDynamicOutputs();

                // Nếu là lần chạy nhận diện AI (đã được VideoProcessingNodeContentControl chuẩn bị xong audio chunks và executionId)
                if (!string.IsNullOrWhiteSpace(videoNode.LastExecutionId) &&
                    string.Equals(videoNode.LastExecutionId, env.ExecutionId, StringComparison.OrdinalIgnoreCase))
                {
                    var chunksJson = videoNode.DynamicOutputs?.FirstOrDefault(o => o.Key == "audio_chunks_json")?.UserValueOverride ?? string.Empty;
                    var chunks = videoNode.DynamicOutputs?.FirstOrDefault(o => o.Key == "audio_chunks")?.UserValueOverride ?? string.Empty;
                    var count = videoNode.DynamicOutputs?.FirstOrDefault(o => o.Key == "audio_chunk_count")?.UserValueOverride ?? "0";
                    var baseAudio = videoNode.DynamicOutputs?.FirstOrDefault(o => o.Key == "base_audio_path")?.UserValueOverride ?? string.Empty;

                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, videoNode.Id, "audio_chunks_json", chunksJson);
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, videoNode.Id, "audio_chunks", chunks);
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, videoNode.Id, "audio_chunk_count", count);
                    if (!string.IsNullOrWhiteSpace(baseAudio))
                    {
                        env.Service.SetScopedNodeStringOutput(env.ExecutionId, videoNode.Id, "base_audio_path", baseAudio);
                    }
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, videoNode.Id, "executionId", env.ExecutionId);

                    sw.Stop();
                    env.OnNodeCompleted?.Invoke(node, sw.Elapsed);
                    await env.TraverseOutputsAsync(node).ConfigureAwait(false);
                    return;
                }

                ClearStandardOutputs(videoNode);

                var videoInput = ResolveFromMapping(env, videoNode.VideoSourceNodeId, videoNode.VideoSourceOutputKey);
                if (string.IsNullOrWhiteSpace(videoInput))
                    videoInput = videoNode.VideoPath;
                if (string.IsNullOrWhiteSpace(videoInput))
                    throw new InvalidOperationException("VideoProcessingNode: thiếu input video.");
                var videoSubfolderName = BuildOutputSubfolderNameFromVideoPath(videoInput);
                var (codecArgs, extension) = await BuildOutputArgsAsync(videoNode, env.CancellationToken).ConfigureAwait(false);

                var downloadsRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");

                var defaultFrameOutputFolder = Path.Combine(downloadsRoot, "flow-frame", videoSubfolderName);
                var defaultVideoOutputFolder = Path.Combine(downloadsRoot, "flow-video", videoSubfolderName);
                var defaultAudioOutputFolder = Path.Combine(downloadsRoot, "flow-audio", videoSubfolderName);

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
                if (string.IsNullOrWhiteSpace(frameOutputFolder))
                    frameOutputFolder = defaultFrameOutputFolder;

                string? videoOutputDestination;
                if (videoNode.UseDialogVideoConfig)
                {
                    videoOutputDestination = ResolveFromMapping(env, videoNode.VideoOutputFolderSourceNodeId, videoNode.VideoOutputFolderSourceOutputKey);
                    if (string.IsNullOrWhiteSpace(videoOutputDestination))
                        videoOutputDestination = videoNode.DefaultOutputVideoPath;

                    if (string.IsNullOrWhiteSpace(videoOutputDestination))
                        videoOutputDestination = defaultVideoOutputFolder;
                }
                else
                {
                    videoOutputDestination = videoNode.DefaultOutputVideoPath;
                }
                if (videoNode.ExtractFramesEnabled && !videoNode.OutputBase64)
                {
                    if (string.IsNullOrWhiteSpace(frameOutputFolder))
                        throw new InvalidOperationException("VideoProcessingNode: Output Base64 tắt nhưng chưa có folder output.");
                    Directory.CreateDirectory(frameOutputFolder);
                    foreach (var existingFile in Directory.GetFiles(frameOutputFolder, "frame_*.*"))
                    {
                        try { File.Delete(existingFile); } catch { }
                    }
                }

                string? audioOutputFolder;
                if (videoNode.UseDialogVideoConfig)
                {
                    audioOutputFolder = ResolveFromMapping(env, videoNode.AudioOutputFolderSourceNodeId, videoNode.AudioOutputFolderSourceOutputKey);
                    if (string.IsNullOrWhiteSpace(audioOutputFolder))
                        audioOutputFolder = videoNode.AudioOutputFolderPath;

                    if (string.IsNullOrWhiteSpace(audioOutputFolder))
                        audioOutputFolder = defaultAudioOutputFolder;
                }
                else
                {
                    audioOutputFolder = videoNode.AudioOutputFolderPath;
                }

                var tempRoot = Path.Combine(Path.GetTempPath(), "FlowMy_VideoProcessing");
                Directory.CreateDirectory(tempRoot);

                if (videoNode.ConcatEnabled && videoNode.ConcatVideos.Count > 0)
                {
                    var concatList = new List<string> { videoInput };
                    foreach (var item in videoNode.ConcatVideos)
                    {
                        if (!string.IsNullOrWhiteSpace(item.SourcePath) && File.Exists(item.SourcePath))
                        {
                            concatList.Add(item.SourcePath);
                        }
                    }

                    if (concatList.Count > 1)
                    {
                        var concatenatedVideoPath = Path.Combine(tempRoot, $"video_concat_{Guid.NewGuid():N}.mp4");
                        var concatTxtPath = Path.Combine(tempRoot, $"concat_list_{Guid.NewGuid():N}.txt");
                        var lines = concatList.Select(p => $"file '{p.Replace("'", "'\\''")}'");
                        await File.WriteAllLinesAsync(concatTxtPath, lines, env.CancellationToken).ConfigureAwait(false);

                        var concatArgs = new[]
                        {
                            "-y", "-hide_banner", "-loglevel", "error",
                            "-f", "concat", "-safe", "0",
                            "-i", concatTxtPath,
                            "-c", "copy",
                            concatenatedVideoPath
                        };
                        await RunFfmpegAsync(concatArgs, env.CancellationToken).ConfigureAwait(false);

                        if (File.Exists(concatenatedVideoPath))
                        {
                            videoInput = concatenatedVideoPath;
                            LogLine?.Invoke(videoNode, $"✅ [GHÉP VIDEO] Đã ghép thành công {concatList.Count} file video thành 1.");
                        }
                    }
                }

                var sourceFps = await ProbeSourceFpsAsync(videoInput, env.CancellationToken).ConfigureAwait(false);
                if (sourceFps > 0) videoNode.SourceFps = sourceFps;
                var sourceHeight = await ProbeSourceHeightAsync(videoInput, env.CancellationToken).ConfigureAwait(false);
                var sourceWidth = await ProbeSourceWidthAsync(videoInput, env.CancellationToken).ConfigureAwait(false);
                var sourceFpsClamped = Math.Max(0.001, videoNode.SourceFps);
                var totalDuration = await ProbeDurationSecondsAsync(videoInput, env.CancellationToken).ConfigureAwait(false);
                var effectiveStart = videoNode.TrimEnabled ? Math.Max(0, videoNode.TrimStartSec) : 0;
                var effectiveEnd = videoNode.TrimEnabled && videoNode.TrimEndSec > effectiveStart
                    ? Math.Min(totalDuration, videoNode.TrimEndSec)
                    : totalDuration;
                var effectiveDurationTrim = Math.Max(0.01, effectiveEnd - effectiveStart);
                var targetFrameCount = Math.Max(1, videoNode.ExtractFrameCount);
                var calculatedExtractFps = (double)targetFrameCount / effectiveDurationTrim;

                var extractFps = videoNode.ExtractAllFrames
                    ? sourceFpsClamped
                    : (videoNode.ExtractByFpsEnabled
                        ? Math.Max(0.001, videoNode.ExtractFps)
                        : Math.Max(0.001, calculatedExtractFps));

                var hwaccel = await ResolveHwAccelAsync(videoNode.PreferGpu, env.CancellationToken).ConfigureAwait(false);
                videoNode.PreferredHwAccel = hwaccel;

                string? effectiveSubtitlePath = null;

                var hasSubtitles = videoNode.SubtitleStyle != null && videoNode.SubtitleStyle.Enabled && videoNode.Subtitles.Count > 0;
                if (hasSubtitles)
                {
                    var srcW = sourceWidth > 0 ? sourceWidth : 1920;
                    var srcH = sourceHeight > 0 ? sourceHeight : 1080;
                    if (videoNode.RotationDegrees == 90 || videoNode.RotationDegrees == 270)
                    {
                        var tmp = srcW;
                        srcW = srcH;
                        srcH = tmp;
                    }

                    var assContent = FlowMy.Core.Models.Media.SubtitleAssBuilder.BuildAssFileContent(
                        videoNode.Subtitles,
                        videoNode.SubtitleStyle,
                        srcW,
                        srcH);

                    var tempAss = Path.Combine(tempRoot, $"sub_{Guid.NewGuid():N}.ass");
                    await File.WriteAllTextAsync(tempAss, assContent, Encoding.UTF8, env.CancellationToken).ConfigureAwait(false);
                    globalSubtitleCleanup.Add(tempAss);
                    effectiveSubtitlePath = tempAss;
                    LogLine?.Invoke(videoNode, $"🔤 [PHỤ ĐỀ] Đã tạo file phụ đề ASS và gắn vào video ({videoNode.Subtitles.Count} câu, font: {videoNode.SubtitleStyle?.FontFamily ?? "Segoe UI"}, {videoNode.SubtitleStyle?.FontSize ?? 24}px)");
                }
                else if (videoNode.BurnSubtitleEnabled && !string.IsNullOrWhiteSpace(videoNode.SubtitlePath) && File.Exists(videoNode.SubtitlePath))
                {
                    effectiveSubtitlePath = videoNode.SubtitlePath;
                    LogLine?.Invoke(videoNode, $"🔤 [PHỤ ĐỀ] Gắn file phụ đề: {videoNode.SubtitlePath}");
                }

                var frameFilter = BuildVideoFilterChain(videoNode, extractFps, includeTextOverlay: true, sourceHeight, effectiveSubtitlePath);
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

                var producedFrames = new List<string>();
                if (videoNode.ExtractFramesEnabled)
                {
                    var (trimInputArgs, _) = BuildTrimArgs(videoNode);
                    var frameArgs = new List<string> { "-y", "-hide_banner", "-loglevel", "error" };
                    frameArgs.AddRange(trimInputArgs);
                    frameArgs.AddRange(new[] { "-an", "-sn", "-i", videoInput });
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
                            effectiveDurationTrim,
                            (pct, status) => ProgressChanged?.Invoke(videoNode, pct, status),
                            line => LogLine?.Invoke(videoNode, line),
                            env.CancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        TryDeleteOverlayRasterFiles(overlayFrameCleanup);
                    }

                producedFrames = Directory.GetFiles(
                        Path.GetDirectoryName(framePattern)!,
                        Path.GetFileName(framePattern).Replace("%06d", "*"))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!videoNode.ExtractAllFrames && !videoNode.ExtractByFpsEnabled && producedFrames.Count > 0)
                {
                    if (producedFrames.Count > targetFrameCount)
                    {
                        var extraFiles = producedFrames.Skip(targetFrameCount).ToList();
                        producedFrames = producedFrames.Take(targetFrameCount).ToList();
                        foreach (var extraFile in extraFiles)
                        {
                            try { File.Delete(extraFile); } catch { }
                        }
                    }
                }

                // Loại bỏ các frame bị excluded theo timestamp
                if (videoNode.ExcludedFrameTimestamps.Count > 0 && producedFrames.Count > 0)
                {
                    var frameDuration = extractFps > 0 ? 1.0 / extractFps : 1.0;
                    var trimStart = videoNode.TrimEnabled ? Math.Max(0, videoNode.TrimStartSec) : 0;
                    var kept = new List<string>();
                    for (int fi = 0; fi < producedFrames.Count; fi++)
                    {
                        var frameTs = Math.Round((trimStart + fi * frameDuration) * 10000.0) / 10000.0;
                        if (videoNode.IsFrameExcluded(frameTs))
                        {
                            try { File.Delete(producedFrames[fi]); } catch { }
                            LogLine?.Invoke(videoNode, $"[EXCLUDE] Loại frame #{fi} tại {frameTs:0.###}s");
                        }
                        else
                        {
                            kept.Add(producedFrames[fi]);
                        }
                    }
                    producedFrames = kept;
                }

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
                }

                if (videoNode.GridCollageEnabled && producedFrames.Count > 0)
                {
                    var rawCount = producedFrames.Count;
                    var collageFolder = Path.GetDirectoryName(framePattern)!;
                    var sourceAspect = sourceWidth > 0 && sourceHeight > 0 ? (double)sourceWidth / sourceHeight : 16.0 / 9.0;

                    LogLine?.Invoke(videoNode, $"🧩 [TÁCH FRAME GHÉP] Đang ghép {rawCount} frame vào các ảnh cha ({videoNode.GridCollageWidth}x{videoNode.GridCollageHeight}, {videoNode.GridCollageFrameCount} frame/ảnh)...");

                    producedFrames = await VideoFrameCollageComposer.CreateCompositeGridSheetsAsync(
                        videoNode,
                        producedFrames,
                        collageFolder,
                        videoNode.OutputBase64,
                        sourceAspect,
                        env.CancellationToken).ConfigureAwait(false);

                    LogLine?.Invoke(videoNode, $"✅ [TÁCH FRAME GHÉP] Hoàn tất: Đã tạo {producedFrames.Count} ảnh ghép từ {rawCount} frame gốc.");
                }

                var framePathsJson = JsonSerializer.Serialize(producedFrames);
                var frameBase64Json = videoNode.OutputBase64 && producedFrames.Count > 0
                    ? JsonSerializer.Serialize(producedFrames.Select(File.ReadAllBytes).Select(Convert.ToBase64String).ToList())
                    : string.Empty;
                // Only set frames_output when NOT in Base64 mode to avoid duplicating heavy data
                // (frames_base64 already carries the base64 payload)
                if (!videoNode.OutputBase64)
                    SetOutput(videoNode, "frames_output", framePathsJson);
                SetOutput(videoNode, "frames_paths", framePathsJson);
                SetOutput(videoNode, "frames_base64", frameBase64Json);
                SetOutput(videoNode, "frame_folder", producedFrames.Count > 0 ? Path.GetDirectoryName(producedFrames[0]) ?? string.Empty : string.Empty);

                if (producedFrames.Count > 0)
                {
                    var count = producedFrames.Count;
                    var frameRangeText = count > 0 ? (count == 1 ? "frame #1" : $"frame #1 -> #{count}") : "0 frame";
                    var modeText = videoNode.OutputBase64 ? "Base64" : "File Link";
                    var itemType = videoNode.GridCollageEnabled ? "ảnh ghép" : "frame";

                    LogLine?.Invoke(videoNode, $"✅ [TÁCH FRAME] Đã tách thành công {count} {itemType} ({frameRangeText}) [{modeText}]");
                }

                if (ShouldSkipVideoEncode(videoNode))
                {
                    SetOutput(videoNode, "video_output", string.Empty);
                    SetOutput(videoNode, "video_path", string.Empty);
                }
                else
                {
                    var outputBasePath = Path.Combine(tempRoot, $"video_base_{Guid.NewGuid():N}{extension}");
                    var mainFilter = BuildVideoFilterChain(videoNode, extractFps: null, includeTextOverlay: true, sourceHeight, effectiveSubtitlePath);
                    var (trimInputArgs, trimOutputArgs) = BuildTrimArgs(videoNode);
                    var mainArgs = new List<string>
                    {
                        "-y", "-hide_banner", "-loglevel", "error"
                    };
                    mainArgs.AddRange(trimInputArgs);
                    mainArgs.AddRange(new[]
                    {
                        "-i", videoInput,
                        "-sn",
                        "-an",
                    });

                    string? lblEncDir = null;
                    var overlayEncodeCleanup = new List<string>();
                    try
                    {
                        FrameLabelSequenceFfmpegInput? labelFf = null;
                        if (videoNode.FrameLabelEnabled && !videoNode.ExportVideoEnabled)
                        {
                            lblEncDir = videoNode.FrameLabelDebugSamplesEnabled
                                ? CreateFrameLabelDebugFolder("enc_seq", Path.GetDirectoryName(framePattern))
                                : Path.Combine(tempRoot, $"enc_lbl_{Guid.NewGuid():N}");
                            var videoEncodeFps = sourceFpsClamped;
                            var nLbl = (int)Math.Ceiling(effectiveDurationTrim * videoEncodeFps) + 64;
                            var srcFpsForSeq = videoNode.SourceFps > 0 ? videoNode.SourceFps : 30;
                            await RunWpfCompositorAsync(
                                () => FrameLabelRasterComposer.WriteLabelSequencePngs(
                                    videoNode,
                                    lblEncDir,
                                    nLbl,
                                    effectiveStart,
                                    videoEncodeFps,
                                    srcFpsForSeq,
                                    sourceWidth > 0 ? sourceWidth : 1920,
                                    sourceHeight > 0 ? sourceHeight : 1080,
                                    Math.Max(sourceHeight, 1)),
                                env.CancellationToken).ConfigureAwait(false);

                            if (videoNode.FrameLabelDebugSamplesEnabled)
                                LogLine?.Invoke(videoNode, $"[DBG] FrameLabel sequence (encode): {lblEncDir}");

                            labelFf = new FrameLabelSequenceFfmpegInput(
                                Path.Combine(lblEncDir, "label_%06d.png").Replace('\\', '/'),
                                videoEncodeFps);
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
                            overlayProbeSrcHForFontScale: Math.Max(sourceHeight, 1),
                            isVideoExport: true);
                        mainArgs.AddRange(new[] { "-r", sourceFpsClamped.ToString("0.###", CultureInfo.InvariantCulture) });
                        mainArgs.AddRange(codecArgs);
                        mainArgs.AddRange(trimOutputArgs);
                        mainArgs.Add(outputBasePath);

                        if (videoNode.TwoPassEnabled && (videoNode.OutputFormat == "mp4_h264" || videoNode.OutputFormat == "mp4_h265"))
                        {
                            await RunTwoPassEncodeAsync(videoNode, mainArgs, outputBasePath, hwaccel, effectiveDurationTrim, env.CancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            await RunFfmpegWithProgressAsync(
                                WithHwaccel(mainArgs, hwaccel),
                                effectiveDurationTrim,
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

                var mixedVideo = await MergeAudioTracksAsync(videoNode, env, videoInput, postStabilizedPath, videoOutputDestination).ConfigureAwait(false);
                SetOutput(videoNode, "video_output", mixedVideo);
                SetOutput(videoNode, "video_path", mixedVideo);
                if (!string.IsNullOrWhiteSpace(mixedVideo) && File.Exists(mixedVideo))
                {
                    var videoFolder = Path.GetDirectoryName(mixedVideo) ?? string.Empty;
                    LogLine?.Invoke(videoNode, $"✅ [SUCCESS] Lưu video thành công vào thư mục: {videoFolder}");
                    LogLine?.Invoke(videoNode, $"🎬 [INFO] Đường dẫn file video: {mixedVideo}");
                }
                }

                var extractedAudioPath = string.Empty;
                if (videoNode.ExtractAudioEnabled)
                {
                    extractedAudioPath = await TryExtractAudioOutputAsync(
                            videoNode,
                            videoInput,
                            ResolveOutputDirectory(audioOutputFolder, defaultAudioOutputFolder),
                            env.CancellationToken)
                        .ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(extractedAudioPath) && File.Exists(extractedAudioPath))
                    {
                        var audioFolder = Path.GetDirectoryName(extractedAudioPath) ?? string.Empty;
                        LogLine?.Invoke(videoNode, $"✅ [SUCCESS] Trích xuất audio thành công vào thư mục: {audioFolder}");
                    }
                }

                SetOutput(videoNode, "audio_output", extractedAudioPath);
                SetOutput(videoNode, "linkAudio", extractedAudioPath);
                SetOutput(videoNode, "output_manifest", BuildOutputManifest(videoNode, producedFrames, extractedAudioPath));
                ProgressChanged?.Invoke(videoNode, 100, "Completed");
            }
            catch (Exception ex)
            {
                LogLine?.Invoke(node as VideoProcessingNode ?? videoNode, $"❌ [ERROR] Thao tác thất bại với lỗi: {ex.Message}");
                env.OnNodeFailed?.Invoke(node, ex.Message);
                throw;
            }
            finally
            {
                TryDeleteOverlayRasterFiles(globalSubtitleCleanup);
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

        private static void ClearStandardOutputs(VideoProcessingNode node)
        {
            foreach (var key in new[]
            {
                "frames_output",
                "frames_paths",
                "frames_base64",
                "frame_folder",
                "video_output",
                "video_path",
                "audio_output",
                "output_manifest"
            })
            {
                SetOutput(node, key, string.Empty);
            }
        }

        private static string ResolveVideoOutputPath(VideoProcessingNode node, string? mappedDestination, string extension)
        {
            foreach (var candidate in new[] { node.OutputPathOverride, node.DefaultOutputVideoPath, mappedDestination })
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                return BuildOutputPathFromDestination(candidate, "video_processed", extension);
            }

            return Path.Combine(Path.GetTempPath(), $"video_processed_{Guid.NewGuid():N}{extension}");
        }

        private static string ResolveOutputDirectory(string? configuredPath, string fallbackDirectory)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                return fallbackDirectory;

            var cleaned = configuredPath.Trim().Trim('"');
            if (LooksLikeFileDestination(cleaned))
            {
                var parent = Path.GetDirectoryName(cleaned);
                if (!string.IsNullOrWhiteSpace(parent))
                    return parent;
            }

            return cleaned;
        }

        private static string BuildOutputPathFromDestination(string destination, string filePrefix, string extension)
        {
            var cleaned = destination.Trim().Trim('"');
            if (LooksLikeFileDestination(cleaned))
                return cleaned;

            var folder = string.IsNullOrWhiteSpace(cleaned)
                ? Path.GetTempPath()
                : cleaned;
            return Path.Combine(folder, $"{filePrefix}_{DateTime.Now:yyyyMMddHHmmss}{extension}");
        }

        private static bool LooksLikeFileDestination(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                return !string.IsNullOrWhiteSpace(Path.GetExtension(path));
            }
            catch
            {
                return false;
            }
        }

        private static string BuildOutputManifest(VideoProcessingNode node, IReadOnlyList<string> framePaths, string? audioPath)
        {
            var videoPath = node.DynamicOutputs?
                .FirstOrDefault(o => string.Equals(o.Key, "video_output", StringComparison.OrdinalIgnoreCase))
                ?.UserValueOverride ?? string.Empty;

            return JsonSerializer.Serialize(new
            {
                framesMode = node.OutputBase64 ? "base64" : "paths",
                framesCount = framePaths.Count,
                framesPaths = framePaths,
                frameFolder = framePaths.Count > 0 ? Path.GetDirectoryName(framePaths[0]) ?? string.Empty : string.Empty,
                videoPath,
                audioPath = audioPath ?? string.Empty,
                exportedVideo = !string.IsNullOrWhiteSpace(videoPath),
                extractedAudio = !string.IsNullOrWhiteSpace(audioPath)
            });
        }

        private static async Task<string> TryExtractAudioOutputAsync(
            VideoProcessingNode node,
            string sourceVideoInput,
            string outputFolder,
            CancellationToken ct)
        {
            try
            {
                if (!await ProbeHasAudioStreamAsync(sourceVideoInput, ct).ConfigureAwait(false))
                {
                    LogLine?.Invoke(node, "Audio extract skipped: source video has no audio stream.");
                    return string.Empty;
                }

                Directory.CreateDirectory(outputFolder);
                var extension = VideoAudioFilterGraphBuilder.ResolveAudioFileExtension(node.AudioExportFormat);
                var outputPath = Path.Combine(outputFolder, $"audio_extracted_{DateTime.Now:yyyyMMddHHmmss}{extension}");
                var duration = await ProbeDurationSecondsAsync(sourceVideoInput, ct).ConfigureAwait(false);
                var (codec, extraArgs) = VideoAudioFilterGraphBuilder.ResolveAudioExportArgs(
                    node.AudioExportFormat, node.AudioExportBitrate, node.AudioExportSampleRate, node.AudioExportChannels);

                var args = new List<string>
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", sourceVideoInput,
                    "-map", "0:a:0",
                    "-vn"
                };

                var afFilter = BuildSourceAudioFilterGraph(node, duration);
                if (!string.IsNullOrWhiteSpace(afFilter))
                {
                    args.AddRange(new[] { "-af", afFilter });
                }

                args.AddRange(new[] { "-c:a", codec });
                args.AddRange(extraArgs);
                args.Add(outputPath);

                await RunFfmpegWithProgressAsync(
                    args,
                    duration,
                    (pct, status) => ProgressChanged?.Invoke(node, pct, $"Extract audio... {status}"),
                    line => LogLine?.Invoke(node, line),
                    ct).ConfigureAwait(false);

                return File.Exists(outputPath) ? outputPath : string.Empty;
            }
            catch (Exception ex)
            {
                LogLine?.Invoke(node, $"Audio extract failed: {ex.Message}");
                return string.Empty;
            }
        }

        private static string BuildSourceAudioFilterGraph(VideoProcessingNode node, double duration)
        {
            return VideoAudioFilterGraphBuilder.BuildSourceAudioFilterGraph(node, duration, applyTrim: node.AudioTrimEnabled);
        }

        private static string ResolveAudioOutputExtension(string? codec)
            => (codec ?? "aac").Trim().ToLowerInvariant() switch
            {
                "mp3" => ".mp3",
                "opus" => ".opus",
                "copy" => ".m4a",
                _ => ".m4a"
            };

        private static async Task<string> MergeAudioTracksAsync(
            VideoProcessingNode node,
            NodeExecutionEnvironment env,
            string sourceVideoInput,
            string baseVideoPath,
            string? outputDestination)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "FlowMy_VideoProcessing");
            Directory.CreateDirectory(tempRoot);

            var (_, extension) = await BuildOutputArgsAsync(node, env.CancellationToken).ConfigureAwait(false);
            var outputVideo = ResolveVideoOutputPath(node, outputDestination, extension);
            var outputDir = Path.GetDirectoryName(outputVideo);
            if (!string.IsNullOrWhiteSpace(outputDir))
                Directory.CreateDirectory(outputDir);

            var audioInputs = new List<(string path, VideoAudioTrackConfig cfg)>();
            if (node.SourceAudioEnabled && node.SourceAudioVolumePercent > 0 && await ProbeHasAudioStreamAsync(sourceVideoInput, env.CancellationToken).ConfigureAwait(false))
            {
                var sourceAudioPath = Path.Combine(tempRoot, $"audio_source_{Guid.NewGuid():N}.wav");
                var duration = await ProbeDurationSecondsAsync(sourceVideoInput, env.CancellationToken).ConfigureAwait(false);
                var afFilter = BuildSourceAudioFilterGraph(node, duration);

                var extractArgs = new List<string>
                {
                    "-y", "-hide_banner", "-loglevel", "error"
                };

                double effStart = 0;
                double effEnd = duration;
                if (node.AudioTrimEnabled && node.AudioTrimEndSec > node.AudioTrimStartSec)
                {
                    effStart = Math.Max(0, node.AudioTrimStartSec);
                    effEnd = Math.Min(duration, node.AudioTrimEndSec);
                }
                else if (node.TrimEnabled && node.TrimEndSec > node.TrimStartSec)
                {
                    effStart = Math.Max(0, node.TrimStartSec);
                    effEnd = Math.Min(duration, node.TrimEndSec);
                }

                if (effStart > 0 || effEnd < duration)
                {
                    var effDur = Math.Max(0.01, effEnd - effStart);
                    extractArgs.AddRange(new[]
                    {
                        "-ss", effStart.ToString("0.###", CultureInfo.InvariantCulture),
                        "-t", effDur.ToString("0.###", CultureInfo.InvariantCulture)
                    });
                }

                extractArgs.AddRange(new[]
                {
                    "-i", sourceVideoInput,
                    "-map", "0:a:0",
                    "-vn"
                });

                if (!string.IsNullOrWhiteSpace(afFilter))
                    extractArgs.AddRange(new[] { "-af", afFilter });

                extractArgs.AddRange(new[] { "-c:a", "pcm_s16le", "-avoid_negative_ts", "make_zero", sourceAudioPath });
                await RunFfmpegAsync(extractArgs, env.CancellationToken).ConfigureAwait(false);

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
            else if (node.SourceAudioEnabled && node.SourceAudioVolumePercent > 0)
            {
                LogLine?.Invoke(node, "Audio source stream not found; exporting video without source audio.");
            }

            if (node.MultiTrackBgmEnabled)
            {
                foreach (var t in node.AudioTracks)
                {
                    if (t.IsMuted) continue;
                    var path = ResolveFromMapping(env, t.SourceNodeId, t.SourceOutputKey);
                    if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(t.SourceNodeId))
                        path = t.SourceOutputKey;

                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        audioInputs.Add((path, t));
                    else if (!string.IsNullOrWhiteSpace(path))
                        LogLine?.Invoke(node, $"⚠ Audio track not found: {path}");
                }
            }

            if (audioInputs.Count == 0)
            {
                await RunFfmpegAsync(new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", baseVideoPath,
                    "-c:v", "copy",
                    "-movflags", "+faststart",
                    "-avoid_negative_ts", "make_zero",
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
            var amixInputs = new List<string>();
            var trackIndex = 1;

            foreach (var (_, cfg) in preparedAudio)
            {
                var tag = $"[a{trackIndex}]";
                var inputTag = $"[{trackIndex}:a]";
                var chain = new List<string>();

                if (Math.Abs(cfg.VolumePercent - 100) > 0.01)
                {
                    var vol = (Math.Max(0, cfg.VolumePercent) / 100.0).ToString("0.###", CultureInfo.InvariantCulture);
                    chain.Add($"volume={vol}");
                }

                if (cfg.StartAtSec > 0.001)
                {
                    var delayMs = (int)Math.Round(cfg.StartAtSec * 1000.0);
                    chain.Add($"adelay={delayMs}|{delayMs}");
                }

                if (cfg.FadeInSec > 0.001)
                {
                    var d = cfg.FadeInSec.ToString("0.###", CultureInfo.InvariantCulture);
                    chain.Add($"afade=t=in:ss=0:d={d}");
                }

                if (cfg.FadeOutSec > 0.001 && videoDuration > cfg.FadeOutSec)
                {
                    var st = (videoDuration - cfg.FadeOutSec).ToString("0.###", CultureInfo.InvariantCulture);
                    var d = cfg.FadeOutSec.ToString("0.###", CultureInfo.InvariantCulture);
                    chain.Add($"afade=t=out:st={st}:d={d}");
                }

                if (chain.Count > 0)
                {
                    filterChains.Add($"{inputTag}{string.Join(",", chain)}{tag}");
                    amixInputs.Add(tag);
                }
                else
                {
                    amixInputs.Add(inputTag);
                }

                trackIndex++;
            }

            if (amixInputs.Count == 1)
            {
                if (filterChains.Count > 0)
                {
                    args.AddRange(new[] { "-filter_complex", filterChains[0] });
                    args.AddRange(new[] { "-map", "0:v:0", "-map", amixInputs[0] });
                }
                else
                {
                    args.AddRange(new[] { "-map", "0:v:0", "-map", "1:a:0" });
                }
            }
            else
            {
                var mixChain = $"{string.Join(string.Empty, amixInputs)}amix=inputs={amixInputs.Count}:duration=first:dropout_transition=2[aout]";
                filterChains.Add(mixChain);
                args.AddRange(new[] { "-filter_complex", string.Join(";", filterChains) });
                args.AddRange(new[] { "-map", "0:v:0", "-map", "[aout]" });
            }

            args.AddRange(new[]
            {
                "-c:v", "copy",
                "-c:a", "aac",
                "-b:a", "320k",
                "-ar", "48000",
                "-ac", "2",
                "-movflags", "+faststart",
                "-avoid_negative_ts", "make_zero",
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
            var sourceAudio = inputAudioPath;
            if (cfg.TrimStartSec > 0 || cfg.TrimEndSec > cfg.TrimStartSec)
            {
                var trimmedPath = Path.Combine(tempRoot, $"audio_trimmed_{Guid.NewGuid():N}.wav");
                var trimArgs = new List<string> { "-y", "-hide_banner", "-loglevel", "error" };
                if (cfg.TrimStartSec > 0)
                    trimArgs.AddRange(new[] { "-ss", cfg.TrimStartSec.ToString("0.###", CultureInfo.InvariantCulture) });
                if (cfg.TrimEndSec > cfg.TrimStartSec)
                    trimArgs.AddRange(new[] { "-to", cfg.TrimEndSec.ToString("0.###", CultureInfo.InvariantCulture) });
                trimArgs.AddRange(new[] { "-i", inputAudioPath, "-c:a", "pcm_s16le", trimmedPath });
                await RunFfmpegAsync(trimArgs, ct).ConfigureAwait(false);
                if (File.Exists(trimmedPath)) sourceAudio = trimmedPath;
            }

            var audioDuration = await ProbeDurationSecondsAsync(sourceAudio, ct).ConfigureAwait(false);
            if (audioDuration <= 0) return sourceAudio;

            var startAt = Math.Max(0, cfg.StartAtSec);
            var slotDurationSec = Math.Max(0.001, videoDurationSec - startAt);
            var isShorter = audioDuration < slotDurationSec - 0.0001;
            var mode = isShorter ? cfg.ShorterMode : cfg.LongerMode;
            var output = Path.Combine(tempRoot, $"audio_sync_{Guid.NewGuid():N}.wav");
            var slotTrim = slotDurationSec.ToString("0.###", CultureInfo.InvariantCulture);

            switch (mode)
            {
                case AudioSyncMode.Loop:
                    await RunFfmpegAsync(new[]
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-stream_loop", "-1",
                        "-i", sourceAudio,
                        "-t", slotTrim,
                        "-c:a", "pcm_s16le",
                        output
                    }, ct).ConfigureAwait(false);
                    return output;

                case AudioSyncMode.PadSilence:
                    if (audioDuration > slotDurationSec)
                    {
                        await RunFfmpegAsync(new[]
                        {
                            "-y", "-hide_banner", "-loglevel", "error",
                            "-i", sourceAudio,
                            "-af", $"atrim=0:{slotTrim}",
                            "-c:a", "pcm_s16le",
                            output
                        }, ct).ConfigureAwait(false);
                        return output;
                    }
                    return sourceAudio;

                case AudioSyncMode.Stretch:
                {
                    var factor = Math.Max(0.01, audioDuration / slotDurationSec);
                    var atempo = BuildAtempoChain(factor);
                    await RunFfmpegAsync(new[]
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-i", sourceAudio,
                        "-af", $"{atempo},atrim=0:{slotTrim}",
                        "-c:a", "pcm_s16le",
                        output
                    }, ct).ConfigureAwait(false);
                    return output;
                }

                case AudioSyncMode.Trim:
                case AudioSyncMode.Compress:
                    if (audioDuration > slotDurationSec)
                    {
                        await RunFfmpegAsync(new[]
                        {
                            "-y", "-hide_banner", "-loglevel", "error",
                            "-i", sourceAudio,
                            "-af", $"atrim=0:{slotTrim}",
                            "-c:a", "pcm_s16le",
                            output
                        }, ct).ConfigureAwait(false);
                        return output;
                    }
                    return sourceAudio;

                default:
                    return sourceAudio;
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

        private static string BuildVideoFilterChain(
            VideoProcessingNode node,
            double? extractFps,
            bool includeTextOverlay,
            int? sourceHeightOverride = null,
            string? subtitlePathOverride = null)
        {
            var filters = new List<string>();

            // Loại bỏ excluded frames bằng FFmpeg select filter CHỈ KHI xuất VIDEO (không phải khi tách frame stills)
            // Khi tách frame stills, việc loại bỏ frame sẽ do C# xóa file chính xác theo timestamp sau khi trích xuất
            // để tránh làm lệch timeline/PTS của bộ lọc fps.
            if (!extractFps.HasValue && node.ExcludedFrameTimestamps.Count > 0)
            {
                var fps = node.SourceFps > 0 ? node.SourceFps : 30;
                var halfFrame = 0.5 / fps;
                var betweens = node.ExcludedFrameTimestamps
                    .Select(t => $"between(t\\,{(t - halfFrame):0.###}\\,{(t + halfFrame):0.###})")
                    .ToArray();
                filters.Add($"select='not({string.Join("+", betweens)})',setpts=N/FRAME_RATE/TB");
            }

            if (extractFps.HasValue && extractFps.Value > 0)
            {
                var fpsVal = extractFps.Value.ToString("0.####", CultureInfo.InvariantCulture);
                filters.Add($"fps=fps={fpsVal}");
            }
            if (node.ColorGradingEnabled)
            {
                filters.Add(VideoColorGrading.BuildEqFilter(node));
                var hueF = VideoColorGrading.BuildHueFilter(node.Hue);
                if (hueF != null) filters.Add(hueF);
                var gammaF = VideoColorGrading.BuildGammaLutRgbFilter(node.Gamma);
                if (gammaF != null) filters.Add(gammaF);
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

            if (node.TransformEnabled)
            {
                var rot = ((int)node.RotationDegrees / 90) % 4;
                if (rot == 1) filters.Add("transpose=1");
                else if (rot == 2) filters.Add("transpose=2,transpose=2");
                else if (rot == 3) filters.Add("transpose=2");
                if (node.FlipH) filters.Add("hflip");
                if (node.FlipV) filters.Add("vflip");
            }

            if (node.SpeedAdjustEnabled && Math.Abs(node.SpeedFactor - 1) > 0.01)
            {
                var pts = (1.0 / node.SpeedFactor).ToString("0.######", CultureInfo.InvariantCulture);
                filters.Add($"setpts={pts}*PTS");
            }
            if (includeTextOverlay && node.CanvasOverlayEnabled && node.TextOverlayEnabled && !string.IsNullOrWhiteSpace(node.OverlayText))
            {
                var escapedText = node.OverlayText.Replace("\\", "\\\\").Replace(":", "\\:").Replace("'", "\\'");
                var (xExpr, yExpr) = BuildTextPositionExpression(node.TextPosition, 10);
                var fontPath = ResolveFontPath(node.OverlayFont);
                var sourceScale = ComputeFrameLabelSourceScale(sourceHeightOverride);
                var textSize = Math.Max(10, (int)Math.Round(node.OverlayFontSize * sourceScale));
                filters.Add($"drawtext=text='{escapedText}':fontfile='{fontPath}':fontsize={textSize}:fontcolor={node.OverlayFontColor}:x={xExpr}:y={yExpr}");
            }

            var subToBurn = !string.IsNullOrWhiteSpace(subtitlePathOverride)
                ? subtitlePathOverride
                : ((node.BurnSubtitleEnabled && !string.IsNullOrWhiteSpace(node.SubtitlePath)) ? node.SubtitlePath : null);

            if (!string.IsNullOrWhiteSpace(subToBurn) && File.Exists(subToBurn))
            {
                var subPath = EscapeFfmpegFilterPath(subToBurn);
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
            int overlayProbeSrcHForFontScale,
            bool isVideoExport = false)
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
                var (xExprLabel, yExprLabel) = BuildFrameLabelOverlayExpression(node, estW, estH, phf);
                filterChains.Add($"[{currentLabel}][1:v]overlay=x='{xExprLabel}':y='{yExprLabel}':repeatlast=1:format=auto[{nextLabel}]");
                currentLabel = nextLabel;
            }

            if (node.WatermarkEnabled && !string.IsNullOrWhiteSpace(node.WatermarkImagePath) && File.Exists(node.WatermarkImagePath))
            {
                var overlayAlpha = node.WatermarkOpacity.ToString("0.###", CultureInfo.InvariantCulture);
                var wScaled = $"wms{stageIndex}";
                var wRgba = $"wmrgba{stageIndex}";
                var nextLabel = $"v{++stageIndex}";
                var wmPixelW = Math.Max(1, (int)Math.Round(estW * Math.Clamp(node.WatermarkWidthFraction, 0.01, 1)));
                var xy = VideoWatermarkGeometry.BuildOverlayPositionExpression(node.WatermarkPosition, node.WatermarkInsetFraction);
                imageInputs.Add(node.WatermarkImagePath!);
                // Direct scale on image input prevents scale2ref from disrupting main video stream timebase/framerate.
                filterChains.Add($"[{imageInputIndex}:v]scale=w={wmPixelW}:h=-2[{wScaled}]");
                filterChains.Add($"[{wScaled}]format=rgba,colorchannelmixer=aa={overlayAlpha}[{wRgba}]");
                filterChains.Add($"[{currentLabel}][{wRgba}]overlay={xy}:repeatlast=1:format=auto[{nextLabel}]");
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
                    filterChains.Add($"[{imageInputIndex}:v]scale=w={overlayPixelW}:h={overlayPixelH}:force_original_aspect_ratio=decrease[{ovScaled}]");
                    filterChains.Add($"[{ovScaled}]format=rgba,colorchannelmixer=aa={opacity}[{ovRgba}]");
                    filterChains.Add($"[{currentLabel}][{ovRgba}]overlay=x='{xExpr}':y='{yExpr}':repeatlast=1:format=auto[{nextLabel}]");
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
                    filterChains.Add($"[{currentLabel}][{ovRgba}]overlay=x='{xExpr}':y='{yExpr}':repeatlast=1:format=auto[{nextLabel}]");
                    imageInputIndex++;
                    currentLabel = nextLabel;
                }
            }

            // Apply all output-size transforms at the very end:
            // watermark/text/overlays are first composed on source frame, then resized once.
            var tailScale = BuildTailScaleFilter(node, isVideoExport);
            if (!string.IsNullOrWhiteSpace(tailScale))
            {
                var nextLabelTail = $"v{++stageIndex}";
                filterChains.Add($"[{currentLabel}]{tailScale}[{nextLabelTail}]");
                currentLabel = nextLabelTail;
            }

            return (string.Join(";", filterChains), imageInputs, $"[{currentLabel}]");
        }

        private static string BuildVideoFilterChainWithoutFps(
            VideoProcessingNode node,
            bool includeTextOverlay = false,
            double? sourceFpsOverride = null,
            int? sourceHeightOverride = null,
            string? subtitlePathOverride = null)
        {
            var filters = new List<string>();

            if (node.ColorGradingEnabled)
            {
                filters.Add(VideoColorGrading.BuildEqFilter(node));
                var hueNoFps = VideoColorGrading.BuildHueFilter(node.Hue);
                if (hueNoFps != null) filters.Add(hueNoFps);
                var gammaNoFps = VideoColorGrading.BuildGammaLutRgbFilter(node.Gamma);
                if (gammaNoFps != null) filters.Add(gammaNoFps);
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

            if (node.TransformEnabled)
            {
                var rot = ((int)node.RotationDegrees / 90) % 4;
                if (rot == 1) filters.Add("transpose=1");
                else if (rot == 2) filters.Add("transpose=2,transpose=2");
                else if (rot == 3) filters.Add("transpose=2");
                if (node.FlipH) filters.Add("hflip");
                if (node.FlipV) filters.Add("vflip");
            }

            if (includeTextOverlay && node.CanvasOverlayEnabled && node.TextOverlayEnabled && !string.IsNullOrWhiteSpace(node.OverlayText))
            {
                var escapedText = node.OverlayText.Replace("\\", "\\\\").Replace(":", "\\:").Replace("'", "\\'");
                var (xExpr, yExpr) = BuildTextPositionExpression(node.TextPosition, 10);
                var fontPath = ResolveFontPath(node.OverlayFont);
                var sourceScale = ComputeFrameLabelSourceScale(sourceHeightOverride);
                var textSize = Math.Max(10, (int)Math.Round(node.OverlayFontSize * sourceScale));
                filters.Add($"drawtext=text='{escapedText}':fontfile='{fontPath}':fontsize={textSize}:fontcolor={node.OverlayFontColor}:x={xExpr}:y={yExpr}");
            }

            var subToBurn = !string.IsNullOrWhiteSpace(subtitlePathOverride)
                ? subtitlePathOverride
                : ((node.BurnSubtitleEnabled && !string.IsNullOrWhiteSpace(node.SubtitlePath)) ? node.SubtitlePath : null);

            if (!string.IsNullOrWhiteSpace(subToBurn) && File.Exists(subToBurn))
            {
                var subPath = EscapeFfmpegFilterPath(subToBurn);
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
            int overlayProbeSrcHForFontScale = 0,
            bool isVideoExport = false)
        {
            var hasWatermark = (node.CanvasOverlayEnabled || node.WatermarkEnabled) &&
                               node.WatermarkEnabled &&
                               !string.IsNullOrWhiteSpace(node.WatermarkImagePath) &&
                               File.Exists(node.WatermarkImagePath);
            var hasNonDeferredCanvasLayer = node.CanvasOverlayEnabled && node.Overlays.Any(o =>
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
                    overlayProbeSrcHForFontScale,
                    isVideoExport);
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
                var (xExprLbl, yExprLbl) = BuildFrameLabelOverlayExpression(node, overlayProbeSrcW, overlayProbeSrcH, overlayProbeSrcHForFontScale);
                var tailScale = BuildTailScaleFilter(node, isVideoExport);
                string chain;
                if (string.IsNullOrWhiteSpace(tailScale))
                {
                    chain = $"[0:v]{baseFilter}[m1];[m1][1:v]overlay=x='{xExprLbl}':y='{yExprLbl}':repeatlast=1:format=auto[outv]";
                }
                else
                {
                    chain = $"[0:v]{baseFilter}[m1];[m1][1:v]overlay=x='{xExprLbl}':y='{yExprLbl}':repeatlast=1:format=auto[flb];[flb]{tailScale}[outv]";
                }

                args.AddRange(new[]
                {
                    "-filter_complex", chain,
                    "-map", "[outv]"
                });
                return;
            }

            var finalFilter = baseFilter;
            var tail = BuildTailScaleFilter(node, isVideoExport);
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

        /// <summary>
        /// Builds FFmpeg overlay x/y expressions for raster frame label, matching the preset logic
        /// used by preview (UpdateFrameLabelPreviewLayout) and still export (CompositeLabelOntoStillFile).
        /// When a preset is active (portrait/landscape), the label is right-aligned at top with padding.
        /// Otherwise, raw FrameLabelX/Y fractions are used.
        /// </summary>
        private static (string xExpr, string yExpr) BuildFrameLabelOverlayExpression(
            VideoProcessingNode node,
            int probeSrcW,
            int probeSrcH,
            int probeSrcHForFontScale)
        {
            var (estW, estH) = FrameLabelRasterComposer.GetEstimatedSourceFrameSize(
                probeSrcW > 0 ? probeSrcW : 1920,
                probeSrcH > 0 ? probeSrcH : 1080,
                node);
            var usePreset = FrameLabelRasterComposer.TryGetLabelPresetFractions(estW, estH, out _, out _);
            if (!usePreset)
            {
                // Manual positioning: same as before
                var fx = node.FrameLabelX.ToString("0.######", CultureInfo.InvariantCulture);
                var fy = node.FrameLabelY.ToString("0.######", CultureInfo.InvariantCulture);
                return ($"W*{fx}", $"H*{fy}");
            }

            // Preset mode: right-aligned at top, with padding.
            // Matches CompositeLabelOntoStillFile: boxX = wf - boxW - padX, boxY = padY
            // In FFmpeg overlay: W = main width, H = main height, w = overlay width, h = overlay height
            var sourceScale = ComputeFrameLabelSourceScale(probeSrcHForFontScale > 0 ? probeSrcHForFontScale : (int?)null);
            var padVidX = Math.Max(0, (int)Math.Round(node.FrameLabelHorizontalPadding * sourceScale));
            var padVidY = Math.Max(0, (int)Math.Round(node.FrameLabelVerticalPadding * sourceScale));
            return ($"W-w-{padVidX}", $"{padVidY}");
        }

        private static string BuildTailScaleFilter(VideoProcessingNode node, bool isVideoExport = false)
        {
            var parts = new List<string>();
            var requiresEvenDimensions = string.Equals(node.OutputFormat, "mp4_h264", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(node.OutputFormat, "mp4_h265", StringComparison.OrdinalIgnoreCase);
            if (node.FixedResolutionHeight.HasValue)
            {
                parts.Add($"scale=-2:{node.FixedResolutionHeight.Value}");
            }
            else if (Math.Abs(node.ResolutionScale - 1) > 0.01)
            {
                var sc = node.ResolutionScale.ToString("0.###", CultureInfo.InvariantCulture);
                if (requiresEvenDimensions)
                {
                    parts.Add($"scale=trunc(iw*{sc}/2)*2:trunc(ih*{sc}/2)*2");
                }
                else
                {
                    parts.Add($"scale=iw*{sc}:ih*{sc}");
                }
            }
            // FrameResizeScale (from Tab "Tổng quan") applies ONLY when extracting STILL frames, NOT for Video Export!
            if (!isVideoExport && node.FrameResizeScale < 0.999)
            {
                var sc = node.FrameResizeScale.ToString("0.###", CultureInfo.InvariantCulture);
                if (requiresEvenDimensions)
                {
                    parts.Add($"scale=trunc(iw*{sc}/2)*2:trunc(ih*{sc}/2)*2");
                }
                else
                {
                    parts.Add($"scale=iw*{sc}:ih*{sc}");
                }
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

        private static (List<string> inputArgs, List<string> outputArgs) BuildTrimArgs(VideoProcessingNode node)
        {
            var inputArgs = new List<string>();
            var outputArgs = new List<string>();
            if (node.TrimEnabled && node.TrimEndSec > node.TrimStartSec)
            {
                var dur = node.TrimEndSec - node.TrimStartSec;
                inputArgs.AddRange(new[]
                {
                    "-ss", node.TrimStartSec.ToString("0.###", CultureInfo.InvariantCulture),
                    "-t", dur.ToString("0.###", CultureInfo.InvariantCulture)
                });
                outputArgs.AddRange(new[] { "-avoid_negative_ts", "make_zero" });
            }
            return (inputArgs, outputArgs);
        }

        private static string? _cachedH264Encoder;
        private static string? _cachedHevcEncoder;

        public static async Task<string> ResolveH264EncoderAsync(bool preferGpu, CancellationToken ct)
        {
            if (!preferGpu) return "libx264";
            if (_cachedH264Encoder != null) return _cachedH264Encoder;

            foreach (var enc in new[] { "h264_nvenc", "h264_qsv", "h264_amf" })
            {
                var ok = await RunProcessExitCodeAsync(ResolveBinary("ffmpeg"), new[]
                {
                    "-hide_banner", "-loglevel", "error",
                    "-f", "lavfi", "-i", "nullsrc=s=64x64:d=0.1",
                    "-c:v", enc,
                    "-f", "null", "-"
                }, ct).ConfigureAwait(false);
                if (ok == 0)
                {
                    _cachedH264Encoder = enc;
                    return enc;
                }
            }

            _cachedH264Encoder = "libx264";
            return "libx264";
        }

        private static async Task<string> ResolveHevcEncoderAsync(bool preferGpu, CancellationToken ct)
        {
            if (!preferGpu) return "libx265";
            if (_cachedHevcEncoder != null) return _cachedHevcEncoder;

            foreach (var enc in new[] { "hevc_nvenc", "hevc_qsv", "hevc_amf" })
            {
                var ok = await RunProcessExitCodeAsync(ResolveBinary("ffmpeg"), new[]
                {
                    "-hide_banner", "-loglevel", "error",
                    "-f", "lavfi", "-i", "nullsrc=s=64x64:d=0.1",
                    "-c:v", enc,
                    "-f", "null", "-"
                }, ct).ConfigureAwait(false);
                if (ok == 0)
                {
                    _cachedHevcEncoder = enc;
                    return enc;
                }
            }

            _cachedHevcEncoder = "libx265";
            return "libx265";
        }

        private static async Task<(string[] codecArgs, string extension)> BuildOutputArgsAsync(VideoProcessingNode node, CancellationToken ct)
        {
            var format = node.OutputFormat ?? "mp4_h264";
            var crfInt = ((int)node.Crf).ToString();
            var preset = node.EncoderPreset ?? "veryfast";

            if (format == "mp4_h264")
            {
                var encoder = await ResolveH264EncoderAsync(node.PreferGpu, ct).ConfigureAwait(false);
                if (encoder == "h264_nvenc")
                {
                    var nvPreset = preset switch
                    {
                        "ultrafast" => "p1",
                        "veryfast" => "p2",
                        "fast" => "p3",
                        "medium" => "p4",
                        "slow" => "p6",
                        _ => "p4"
                    };
                    return (new[] { "-c:v", "h264_nvenc", "-preset", nvPreset, "-cq", crfInt, "-pix_fmt", "yuv420p", "-movflags", "+faststart" }, ".mp4");
                }
                if (encoder == "h264_qsv")
                {
                    return (new[] { "-c:v", "h264_qsv", "-global_quality", crfInt, "-movflags", "+faststart" }, ".mp4");
                }
                if (encoder == "h264_amf")
                {
                    return (new[] { "-c:v", "h264_amf", "-rc", "cqp", "-qp_i", crfInt, "-qp_p", crfInt, "-pix_fmt", "yuv420p", "-movflags", "+faststart" }, ".mp4");
                }
                return (new[] { "-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", preset, "-crf", crfInt, "-movflags", "+faststart" }, ".mp4");
            }

            if (format == "mp4_h265")
            {
                var encoder = await ResolveHevcEncoderAsync(node.PreferGpu, ct).ConfigureAwait(false);
                if (encoder == "hevc_nvenc")
                {
                    var nvPreset = preset switch
                    {
                        "ultrafast" => "p1",
                        "veryfast" => "p2",
                        "fast" => "p3",
                        "medium" => "p4",
                        "slow" => "p6",
                        _ => "p4"
                    };
                    return (new[] { "-c:v", "hevc_nvenc", "-preset", nvPreset, "-cq", crfInt, "-pix_fmt", "yuv420p", "-tag:v", "hvc1", "-movflags", "+faststart" }, ".mp4");
                }
                if (encoder == "hevc_qsv")
                {
                    return (new[] { "-c:v", "hevc_qsv", "-global_quality", crfInt, "-tag:v", "hvc1", "-movflags", "+faststart" }, ".mp4");
                }
                if (encoder == "hevc_amf")
                {
                    return (new[] { "-c:v", "hevc_amf", "-rc", "cqp", "-qp_i", crfInt, "-qp_p", crfInt, "-tag:v", "hvc1", "-pix_fmt", "yuv420p", "-movflags", "+faststart" }, ".mp4");
                }
                return (new[] { "-c:v", "libx265", "-pix_fmt", "yuv420p", "-preset", preset, "-crf", crfInt, "-tag:v", "hvc1", "-movflags", "+faststart" }, ".mp4");
            }

            return format switch
            {
                "webm_vp9" => (new[] { "-c:v", "libvpx-vp9", "-crf", crfInt, "-b:v", "0" }, ".webm"),
                "mov_prores" => (new[] { "-c:v", "prores_ks", "-profile:v", "3", "-movflags", "+faststart" }, ".mov"),
                "gif" => (new[] { "-loop", "0" }, ".gif"),
                _ => (new[] { "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart" }, ".mp4")
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
                "-ss", positionSec,
                "-i", node.VideoPath
            };
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
            var videoSubfolderName = BuildOutputSubfolderNameFromVideoPath(node.VideoPath);
            var outputFolder = string.IsNullOrWhiteSpace(outputFolderOverride)
                ? (node.UseDialogVideoConfig
                    ? (string.IsNullOrWhiteSpace(node.FrameOutputFolderPath)
                        ? Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Downloads",
                            "flow-frame",
                            videoSubfolderName)
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

            var targetFrameCount = Math.Max(1, node.ExtractFrameCount);
            var calculatedExtractFps = (double)targetFrameCount / effectiveDuration;
            var effectiveExtractFps = node.ExtractAllFrames
                ? Math.Max(0.001, sourceFps)
                : (node.ExtractByFpsEnabled
                    ? Math.Max(0.001, node.ExtractFps)
                    : Math.Max(0.001, calculatedExtractFps));

            string vfArg;
            var useVsync0 = false;
            if (node.ExtractAllFrames)
            {
                vfArg = BuildVideoFilterChain(node, Math.Max(0.001, sourceFps), includeTextOverlay: true, sourceHeight);
            }
            else
            {
                vfArg = BuildVideoFilterChain(node, effectiveExtractFps, includeTextOverlay: true, sourceHeight);
            }

            Directory.CreateDirectory(outputFolder);
            foreach (var existingFile in Directory.GetFiles(outputFolder, "frame_*.*"))
            {
                try { File.Delete(existingFile); } catch { }
            }

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

            if (!node.ExtractAllFrames && !node.ExtractByFpsEnabled && orderedStills.Count > 0)
            {
                var targetCount = Math.Max(1, node.ExtractFrameCount);
                if (orderedStills.Count > targetCount)
                {
                    var extraFiles = orderedStills.Skip(targetCount).ToList();
                    orderedStills = orderedStills.Take(targetCount).ToList();
                    foreach (var extraFile in extraFiles)
                    {
                        try { File.Delete(extraFile); } catch { }
                    }
                }
            }

            // Loại bỏ các frame bị excluded theo danh sách ExcludedFrameTimestamps
            if (node.ExcludedFrameTimestamps.Count > 0 && orderedStills.Count > 0)
            {
                var frameDuration = effectiveExtractFps > 0 ? 1.0 / effectiveExtractFps : 1.0;
                var trimStart = node.TrimEnabled ? Math.Max(0, node.TrimStartSec) : 0;
                var kept = new List<string>();
                for (int fi = 0; fi < orderedStills.Count; fi++)
                {
                    var frameTs = Math.Round((trimStart + fi * frameDuration) * 10000.0) / 10000.0;
                    if (node.IsFrameExcluded(frameTs))
                    {
                        try { File.Delete(orderedStills[fi]); } catch { }
                        onLog($"[EXCLUDE] Loại bỏ frame #{fi} ({frameTs:0.###}s)");
                    }
                    else
                    {
                        kept.Add(orderedStills[fi]);
                    }
                }
                orderedStills = kept;
            }

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

            var extractedFiles = Directory.GetFiles(outputFolder, $"frame_*.{extension}")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (node.GridCollageEnabled && extractedFiles.Count > 0)
            {
                var rawCount = extractedFiles.Count;
                var sourceAspect = sourceWidth > 0 && sourceHeight > 0 ? (double)sourceWidth / sourceHeight : 16.0 / 9.0;
                onLog($"🧩 [TÁCH FRAME GHÉP] Đang ghép {rawCount} frame vào các ảnh cha ({node.GridCollageWidth}x{node.GridCollageHeight}, {node.GridCollageFrameCount} frame/ảnh)...");

                extractedFiles = await VideoFrameCollageComposer.CreateCompositeGridSheetsAsync(
                    node,
                    extractedFiles,
                    outputFolder,
                    node.OutputBase64,
                    sourceAspect,
                    ct).ConfigureAwait(false);

                onLog($"✅ [TÁCH FRAME GHÉP] Hoàn tất: Đã tạo {extractedFiles.Count} ảnh ghép từ {rawCount} frame gốc.");
            }

            var count = extractedFiles.Count;
            var pathsJson = JsonSerializer.Serialize(extractedFiles);
            var base64Json = node.OutputBase64 && extractedFiles.Count > 0
                ? JsonSerializer.Serialize(extractedFiles.Select(File.ReadAllBytes).Select(Convert.ToBase64String).ToList())
                : string.Empty;
            // Only set frames_output when NOT in Base64 mode to avoid duplicating heavy data
            if (!node.OutputBase64)
                SetOutput(node, "frames_output", pathsJson);
            SetOutput(node, "frames_paths", pathsJson);
            SetOutput(node, "frames_base64", base64Json);
            SetOutput(node, "frame_folder", outputFolder);
            var frameRangeText = count > 0 ? (count == 1 ? "frame #1" : $"frame #1 -> #{count}") : "0 frame";
            var modeText = node.OutputBase64 ? "Base64" : "File Link";
            var itemType = node.GridCollageEnabled ? "ảnh ghép" : "frame";
            onLog($"✅ Đã tách thành công {count} {itemType} ({frameRangeText}) [{modeText}]");
            onProgress(100, $"Done: {count} {itemType}");
        }

        public static async Task<string> RunExtractAudioOnlyAsync(
            VideoProcessingNode node,
            Action<string> onLog,
            Action<double, string> onProgress,
            string? outputFolderOverride,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(node.VideoPath))
                throw new InvalidOperationException("VideoProcessingNode: missing source video for audio extract.");

            node.EnsureStandardDynamicOutputs();
            var videoSubfolderName = BuildOutputSubfolderNameFromVideoPath(node.VideoPath);
            var fallbackFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "flow-audio",
                videoSubfolderName);
            var configuredFolder = string.IsNullOrWhiteSpace(outputFolderOverride)
                ? node.AudioOutputFolderPath
                : outputFolderOverride;
            var outputFolder = ResolveOutputDirectory(configuredFolder, fallbackFolder);

            onLog($"Audio output: {outputFolder}");
            var outputPath = await TryExtractAudioOutputAsync(node, node.VideoPath, outputFolder, ct).ConfigureAwait(false);
            SetOutput(node, "audio_output", outputPath);
            onProgress(100, string.IsNullOrWhiteSpace(outputPath) ? "No audio extracted" : "Audio extract done");
            return outputPath;
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
                "-an", "-sn",
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

        private static string EscapeFfmpegFilterPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Replace("\\", "/").Replace(":", "\\:").Replace("'", "'\\''");
        }

        private static async Task StabilizeVideoAsync(string inputPath, string outputPath, CancellationToken ct)
        {
            var tempVectors = Path.Combine(Path.GetTempPath(), $"vidstab_{Guid.NewGuid():N}.trf");
            var escapedVectors = EscapeFfmpegFilterPath(tempVectors);
            try
            {
                await RunFfmpegAsync(new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", inputPath,
                    "-vf", $"vidstabdetect=stepsize=6:shakiness=8:accuracy=9:result='{escapedVectors}'",
                    "-f", "null", "-"
                }, ct).ConfigureAwait(false);

                await RunFfmpegAsync(new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", inputPath,
                    "-vf", $"vidstabtransform=input='{escapedVectors}':zoom=1:smoothing=30,unsharp=5:5:0.8",
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

        public static async Task<double> ProbeDurationSecondsAsync(string inputPath, CancellationToken ct)
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

        public static async Task<bool> ProbeHasAudioStreamAsync(string inputPath, CancellationToken ct)
        {
            var args = new[]
            {
                "-v", "error",
                "-select_streams", "a:0",
                "-show_entries", "stream=index",
                "-of", "default=nokey=1:noprint_wrappers=1",
                inputPath
            };
            var output = await RunProcessCaptureAsync(ResolveBinary("ffprobe"), args, ct).ConfigureAwait(false);
            return !string.IsNullOrWhiteSpace(output.Trim());
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
            foreach (var accel in new[] { "auto", "d3d11va", "cuda" })
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

        public static async Task RunFfmpegAsync(IEnumerable<string> args, CancellationToken ct)
        {
            var (exit, stderr) = await RunProcessExitCodeWithStderrAsync(ResolveBinary("ffmpeg"), args, ct).ConfigureAwait(false);
            if (exit != 0)
                throw new InvalidOperationException($"VideoProcessingNode: FFmpeg xử lý thất bại. {BuildCompactProcessError(stderr)}");
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

        private static async Task<(int ExitCode, string StandardError)> RunProcessExitCodeWithStderrAsync(
            string fileName,
            IEnumerable<string> args,
            CancellationToken ct)
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
            if (p == null) return (-1, "Cannot start process.");
            var stderrTask = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            return (p.ExitCode, (await stderrTask.ConfigureAwait(false)).Trim());
        }

        private static string BuildCompactProcessError(string? stderr)
        {
            if (string.IsNullOrWhiteSpace(stderr))
                return "Không có stderr từ FFmpeg.";

            var lines = stderr
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .TakeLast(4)
                .ToArray();

            if (lines.Length == 0)
                return "Không có stderr từ FFmpeg.";

            return string.Join(" | ", lines);
        }

        private static string ResolveBinary(string binary)
        {
            return FfmpegPathPreferencesStore.ResolveBinaryPath(binary);
        }

        private static string BuildOutputSubfolderNameFromVideoPath(string? videoPath)
        {
            var stem = string.Empty;
            if (!string.IsNullOrWhiteSpace(videoPath))
            {
                try
                {
                    stem = Path.GetFileNameWithoutExtension(videoPath.Trim());
                }
                catch
                {
                    stem = string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(stem))
                stem = "video";

            foreach (var c in Path.GetInvalidFileNameChars())
                stem = stem.Replace(c, '_');

            return stem;
        }

        private static bool ShouldSkipVideoEncode(VideoProcessingNode node)
        {
            return !node.ExportVideoEnabled;
        }

        private static void SetOutput(VideoProcessingNode node, string key, string value)
        {
            node.EnsureStandardDynamicOutputs();
            var port = node.DynamicOutputs?.FirstOrDefault(o =>
                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            if (port != null) port.UserValueOverride = value ?? string.Empty;
        }
    }
}
