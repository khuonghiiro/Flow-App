// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FlowMy.Core.Models.Media;
using FlowMy.Models.Nodes;
using FlowMy.Services.Media;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using FlowMy.Services.Workflow.Audio;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl
    {
        private CancellationTokenSource? _subtitleAiCts;

        /// <summary>
        /// Xử lý chạy nhận diện AI phân đoạn phụ đề theo cấu hình tab Phụ đề trong dialog và điều phối workflow song song.
        /// </summary>
        private async void AutoGenerateSubtitlesFromAi()
        {
            if (_node == null) return;
            if (string.IsNullOrWhiteSpace(_node.VideoPath) || !File.Exists(_node.VideoPath))
            {
                AppendLog("⚠ Cần mở video trước khi thực hiện nhận diện phụ đề AI.");
                return;
            }

            _subtitleAiCts?.Cancel();
            _subtitleAiCts = new CancellationTokenSource();
            var ct = _subtitleAiCts.Token;

            var execId = Guid.NewGuid().ToString("N");
            AppendLog($"🚀 [AI CAPTION] Khởi động luồng nhận diện phụ đề AI (Execution ID: {execId[..8]})...");

            try
            {
                var duration = GetNaturalDurationSeconds();
                if (duration <= 0) duration = 60.0;

                // 1. Trích xuất file Audio Base (áp dụng toàn bộ chỉnh sửa âm thanh từ tab Audio nếu bật SubtitleUseEditedAudio)
                var tempDir = Path.Combine(Path.GetTempPath(), "FlowMy_VideoProcessing", "AudioChunks", execId);
                Directory.CreateDirectory(tempDir);

                var baseAudioPath = Path.Combine(tempDir, "base_audio.wav");
                AppendLog("🎵 [1/4] Đang trích xuất âm thanh nền (áp dụng hiệu ứng EQ/lọc âm/volume đã chỉnh)...");

                await ExtractEditedBaseAudioAsync(_node, _node.VideoPath, baseAudioPath, duration, ct);
                if (!File.Exists(baseAudioPath) || new FileInfo(baseAudioPath).Length == 0)
                {
                    AppendLog("❌ Không trích xuất được audio từ video để xử lý phụ đề.");
                    return;
                }
                AppendLog($"✅ [1/4] Đã trích xuất âm thanh nền thành công: {baseAudioPath}");

                // 2. Phân tích nhịp giọng nghỉ (VAD / Silence Detect)
                var silences = new List<SilenceInterval>();
                if (_node.SubtitleEnableSmartSilenceSplit)
                {
                    AppendLog($"🔍 [2/4] Đang quét nhịp giọng nghỉ (ngưỡng: {_node.SubtitleSilenceThresholdDb}dB, min: {_node.SubtitleMinSilenceSec}s)...");
                    silences = await VideoAudioSilenceDetector.DetectSilencesAsync(
                        baseAudioPath,
                        _node.SubtitleSilenceThresholdDb,
                        _node.SubtitleMinSilenceSec,
                        line => AppendLog(line),
                        ct);
                }

                // 3. Tính toán các mốc cắt và chia tách file audio thành các chunk song song
                var targetChunkDur = _node.SubtitleSplitMode == "EqualParts" && _node.SubtitleChunkCount > 0
                    ? duration / _node.SubtitleChunkCount
                    : _node.SubtitleChunkDurationSec;

                var boundaries = VideoAudioSilenceDetector.CalculateSmartSplitBoundaries(
                    duration,
                    targetChunkDur,
                    silences,
                    _node.SubtitleEnableSmartSilenceSplit,
                    _node.SubtitleMaxSearchWindowSec);

                AppendLog($"✂️ [3/4] Chia tách audio thành {boundaries.Count} phân đoạn song song...");

                var chunkList = await ExportAudioChunksAsync(_node, baseAudioPath, tempDir, boundaries, execId, ct);
                if (chunkList.Count == 0)
                {
                    AppendLog("❌ Không tạo được các phân đoạn audio chunk.");
                    return;
                }

                // Cập nhật Dynamic Outputs và manifest JSON trên node
                var manifestJson = JsonSerializer.Serialize(chunkList, new JsonSerializerOptions { WriteIndented = true });
                SetDynamicOutputsForSubtitleAi(chunkList, manifestJson, execId, baseAudioPath);

                // 4. Lắng nghe kết quả từ Return Subtitle Node (nếu có cấu hình)
                if (!string.IsNullOrWhiteSpace(_node.ReturnSubtitleNodeId) && !string.IsNullOrWhiteSpace(_node.ReturnSubtitleOutputKey))
                {
                    AppendLog($"🔗 [4/4] Đang lắng nghe kết quả từ Node: {_node.ReturnSubtitleNodeId} (Key: {_node.ReturnSubtitleOutputKey})...");
                    ListenForAiSubtitleResults(execId, chunkList, ct);
                }
                else
                {
                    AppendLog($"✨ Đã tạo xong {chunkList.Count} phân đoạn audio chunk tại: {tempDir}");
                }

                // 5. Kích hoạt chạy workflow để các node nhận dữ liệu và xử lý (chuẩn workflow engine có timer và visualizer như LayerAiDialog)
                AppendLog("⚡ Đang kích hoạt chạy workflow để các node nhận dữ liệu và xử lý...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            var vm = _host?.ViewModel;
                            if (vm != null)
                            {
                                await vm.StartTest(execId);
                            }
                        }).Task.Unwrap();
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"⚠ Lỗi khi thực thi workflow: {ex.Message}");
                    }
                }, ct);
            }
            catch (OperationCanceledException)
            {
                AppendLog("⏹ Đã hủy quá trình nhận diện phụ đề AI.");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi xử lý nhận diện phụ đề AI: {ex.Message}");
            }
        }

        private static async Task ExtractEditedBaseAudioAsync(VideoProcessingNode node, string sourceVideo, string outputPath, double duration, CancellationToken ct)
        {
            var ffmpegExe = FlowMy.Services.Utilities.EnvironmentPathPreferencesStore.ResolveBinaryPath("ffmpeg.exe");
            var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-i", sourceVideo, "-map", "0:a:0?", "-vn" };

            if (node.SubtitleUseEditedAudio)
            {
                var afFilter = VideoAudioFilterGraphBuilder.BuildSourceAudioFilterGraph(node, duration, applyTrim: node.AudioTrimEnabled);
                if (!string.IsNullOrWhiteSpace(afFilter))
                {
                    args.AddRange(new[] { "-af", afFilter });
                }
            }

            args.AddRange(new[] { "-c:a", "pcm_s16le", "-ar", "44100", "-ac", "2", outputPath });
            await RunFfmpegQuickAsync(ffmpegExe, args, ct);
        }

        private static async Task<List<AudioChunkInfo>> ExportAudioChunksAsync(
            VideoProcessingNode node,
            string baseAudioPath,
            string outputDir,
            List<(double start, double end)> boundaries,
            string execId,
            CancellationToken ct)
        {
            var chunks = new List<AudioChunkInfo>();
            var ffmpegExe = FlowMy.Services.Utilities.EnvironmentPathPreferencesStore.ResolveBinaryPath("ffmpeg.exe");
            var ext = (node.SubtitleAudioExportFormat ?? "mp3").ToLowerInvariant();
            var codec = ext switch { "wav" => "pcm_s16le", "m4a" => "aac", "ogg" => "libvorbis", "flac" => "flac", _ => "libmp3lame" };
            var fileExt = ext switch { "wav" => ".wav", "m4a" => ".m4a", "ogg" => ".ogg", "flac" => ".flac", _ => ".mp3" };

            for (int i = 0; i < boundaries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (st, ed) = boundaries[i];
                var codeId = $"chunk_{i + 1}";
                var chunkFileName = $"chunk_{i + 1}_{st:0.00}_{ed:0.00}{fileExt}";
                var chunkFilePath = Path.Combine(outputDir, chunkFileName);

                var cutArgs = new List<string>
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-ss", st.ToString("0.000", CultureInfo.InvariantCulture),
                    "-to", ed.ToString("0.000", CultureInfo.InvariantCulture),
                    "-i", baseAudioPath,
                    "-c:a", codec
                };

                if (codec == "libmp3lame" || codec == "aac")
                {
                    cutArgs.AddRange(new[] { "-b:a", node.SubtitleAudioExportBitrate ?? "128k" });
                }

                cutArgs.Add(chunkFilePath);
                await RunFfmpegQuickAsync(ffmpegExe, cutArgs, ct);

                if (File.Exists(chunkFilePath))
                {
                    string? b64 = null;
                    if (node.SubtitleOutputBase64)
                    {
                        try { b64 = Convert.ToBase64String(File.ReadAllBytes(chunkFilePath)); } catch { }
                    }

                    chunks.Add(new AudioChunkInfo
                    {
                        ChunkIndex = i + 1,
                        CodeId = codeId,
                        StartSec = st,
                        EndSec = ed,
                        AudioPath = chunkFilePath,
                        AudioBase64 = b64,
                        ExecutionId = execId
                    });
                }
            }

            return chunks;
        }

        private void SetDynamicOutputsForSubtitleAi(List<AudioChunkInfo> chunkList, string manifestJson, string execId, string baseAudioPath)
        {
            if (_node == null) return;

            _node.EnsureStandardDynamicOutputs();

            var paths = chunkList.Select(c => c.AudioPath).ToList();
            var pathsStr = string.Join(";", paths);

            _node.LastExecutionId = execId;
            SetPortValue("base_audio_path", baseAudioPath);
            SetPortValue("audio_chunks", pathsStr);
            SetPortValue("audio_chunks_json", manifestJson);
            SetPortValue("audio_chunk_count", chunkList.Count.ToString());

            var execSvc = _host?.ViewModel?.WorkflowExecutionService;
            if (execSvc != null)
            {
                execSvc.SetScopedNodeStringOutput(execId, _node.Id, "base_audio_path", baseAudioPath);
                execSvc.SetScopedNodeStringOutput(execId, _node.Id, "audio_chunks", pathsStr);
                execSvc.SetScopedNodeStringOutput(execId, _node.Id, "audio_chunks_json", manifestJson);
                execSvc.SetScopedNodeStringOutput(execId, _node.Id, "audio_chunk_count", chunkList.Count.ToString());
                execSvc.SetScopedNodeStringOutput(execId, _node.Id, "executionId", execId);
            }

            WorkflowExecutionService.ExecutionIdMapping[execId] = execId;

            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                _host?.RequestSyncDataPanels(immediate: true);
                if (_host?.ViewModel?.ExecutionVisualizer != null && _node != null)
                {
                    _host.ViewModel.ExecutionVisualizer.OnNodeCompleted(_node, TimeSpan.Zero, execId);
                }
            });
        }

        private void SetPortValue(string key, string val)
        {
            var port = _node?.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            if (port != null) port.UserValueOverride = val;
        }

        private void ListenForAiSubtitleResults(string execId, List<AudioChunkInfo> chunkList, CancellationToken ct)
        {
            var processedChunks = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            Action<string, string, string, string?> realtimeHandler = (runId, targetNodeId, targetKey, valStr) =>
            {
                if (string.IsNullOrWhiteSpace(valStr) || valStr == "—") return;
                if (!string.Equals(targetNodeId, _node.ReturnSubtitleNodeId, StringComparison.OrdinalIgnoreCase)) return;
                if (!string.Equals(targetKey, _node.ReturnSubtitleOutputKey, StringComparison.OrdinalIgnoreCase)) return;

                string actualRunId = execId;
                if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(execId, out var mappedRunId))
                {
                    actualRunId = mappedRunId;
                }

                bool isMatch = string.Equals(runId, execId, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(runId, actualRunId, StringComparison.OrdinalIgnoreCase) ||
                               runId.StartsWith(execId + ":", StringComparison.OrdinalIgnoreCase) ||
                               runId.StartsWith(actualRunId + ":", StringComparison.OrdinalIgnoreCase);

                if (!isMatch) return;

                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    ProcessIncomingAiSubtitlePayload(valStr, chunkList, processedChunks);
                });
            };

            WorkflowExecutionService.OnScopedOutputSetGlobal += realtimeHandler;

            _ = Task.Run(async () =>
            {
                try
                {
                    // Chờ tối đa 5 phút cho workflow hoàn tất
                    for (int s = 0; s < 300 && !ct.IsCancellationRequested; s++)
                    {
                        if (processedChunks.Count >= chunkList.Count && chunkList.Count > 0)
                            break;
                        await Task.Delay(1000, ct);
                    }
                }
                catch { }
                finally
                {
                    WorkflowExecutionService.OnScopedOutputSetGlobal -= realtimeHandler;

                    // Fallback kiểm tra output port của Return Node sau khi workflow kết thúc
                    try
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var returnNode = _host?.ViewModel?.Nodes?.FirstOrDefault(n => string.Equals(n.Id, _node.ReturnSubtitleNodeId, StringComparison.OrdinalIgnoreCase));
                            var outPort = returnNode?.DynamicOutputs?.FirstOrDefault(p => string.Equals(p.Key, _node.ReturnSubtitleOutputKey, StringComparison.OrdinalIgnoreCase));
                            if (outPort != null && !string.IsNullOrWhiteSpace(outPort.UserValueOverride) && outPort.UserValueOverride != "—")
                            {
                                ProcessIncomingAiSubtitlePayload(outPort.UserValueOverride, chunkList, processedChunks);
                            }
                        });
                    }
                    catch { }
                }
            }, ct);
        }

        private void ProcessIncomingAiSubtitlePayload(
            string payload,
            List<AudioChunkInfo> chunkList,
            ConcurrentDictionary<string, bool> processedChunks)
        {
            try
            {
                var parsedSubs = SubtitleAiPayloadParser.ParseSubtitles(
                    payload,
                    chunkList,
                    _node.ReturnSubtitleCodeIdKeys,
                    _node.ReturnSubtitleTextKeys,
                    _node.ReturnSubtitleStartKeys,
                    _node.ReturnSubtitleEndKeys,
                    _node.ReturnSubtitleListKeys);

                if (parsedSubs.Count > 0)
                {
                    SubtitleItemsControl.ItemsSource = null;
                    try
                    {
                        var sorted = parsedSubs.OrderBy(s => s.StartTimeSec).ToList();
                        _node.Subtitles.Clear();
                        foreach (var item in sorted) _node.Subtitles.Add(item);

                        UpdateSubtitleBadge();
                        RefreshAvailableLanguageTags(isInitialLoad: true);
                    }
                    finally
                    {
                        SubtitleItemsControl.ItemsSource = _node.Subtitles;
                    }

                    OnSubtitleStyleChanged();
                    AppendLog($"✅ [AI CAPTION] Đã gán thành công {parsedSubs.Count} câu phụ đề vào timeline.");

                    _host?.RequestSyncDataPanels(immediate: true);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"⚠ Lỗi phân tích dữ liệu phụ đề trả về: {ex.Message}");
            }
        }

        private static async Task RunFfmpegQuickAsync(string ffmpegExe, List<string> args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Bộ phân tích JSON / SRT / VTT thông minh cho phụ đề AI trả về.
    /// </summary>
    public static class SubtitleAiPayloadParser
    {
        public static List<SubtitleItem> ParseSubtitles(
            string payload,
            List<AudioChunkInfo> chunkList,
            string codeIdKeys,
            string textKeys,
            string startKeys,
            string endKeys,
            string listKeys)
        {
            var results = new List<SubtitleItem>();
            if (string.IsNullOrWhiteSpace(payload)) return results;

            var trimmed = payload.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        ParseJsonArray(root, chunkList, null, results, textKeys, startKeys, endKeys, listKeys);
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        var chunkId = FindMatchingStringProperty(root, codeIdKeys);
                        var matchedChunk = chunkList.FirstOrDefault(c =>
                            string.Equals(c.CodeId, chunkId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.ChunkIndex.ToString(), chunkId, StringComparison.OrdinalIgnoreCase));

                        var listProp = FindMatchingArrayProperty(root, listKeys);
                        if (listProp.HasValue && listProp.Value.ValueKind == JsonValueKind.Array)
                        {
                            ParseJsonArray(listProp.Value, chunkList, matchedChunk, results, textKeys, startKeys, endKeys, listKeys);
                        }
                        else
                        {
                            var text = FindMatchingStringProperty(root, textKeys);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                var st = FindMatchingDoubleProperty(root, startKeys) ?? 0.0;
                                var ed = FindMatchingDoubleProperty(root, endKeys) ?? (st + 3.0);
                                var offset = matchedChunk?.StartSec ?? 0.0;
                                results.Add(new SubtitleItem { StartTimeSec = offset + st, EndTimeSec = offset + ed, Text = text });
                            }
                        }
                    }
                }
                catch { }
            }

            return results;
        }

        private static void ParseJsonArray(
            JsonElement arrayElem,
            List<AudioChunkInfo> chunkList,
            AudioChunkInfo? inheritedChunk,
            List<SubtitleItem> results,
            string textKeys,
            string startKeys,
            string endKeys,
            string listKeys)
        {
            foreach (var item in arrayElem.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    var text = FindMatchingStringProperty(item, textKeys) ??
                               FindMatchingStringProperty(item, "text,content,translated_text,trans");
                    var origText = FindMatchingStringProperty(item, "original_text,orig_text,source_text,src_text,raw_text,origin");
                    var sourceLang = FindMatchingStringProperty(item, "source_language,source_lang,src_lang,lang") ?? "zh";

                    var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (item.TryGetProperty("translations", out var transProp) && transProp.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in transProp.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                                translations[prop.Name] = prop.Value.GetString() ?? string.Empty;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(origText) || translations.Count > 0)
                    {
                        var st = FindMatchingDoubleProperty(item, startKeys) ??
                                 FindMatchingDoubleProperty(item, "start,start_sec,start_time,st") ?? 0.0;
                        var ed = FindMatchingDoubleProperty(item, endKeys) ??
                                 FindMatchingDoubleProperty(item, "end,end_sec,end_time,ed") ?? (st + 2.5);
                        var offset = inheritedChunk?.StartSec ?? 0.0;

                        var subItem = new SubtitleItem
                        {
                            StartTimeSec = offset + st,
                            EndTimeSec = offset + ed,
                            SourceLanguage = sourceLang,
                            OriginalText = origText ?? string.Empty,
                            Text = text ?? string.Empty,
                            Translations = translations
                        };
                        subItem.EnsureLinesFromText();
                        results.Add(subItem);
                    }
                }
            }
        }

        private static string? FindMatchingStringProperty(JsonElement elem, string keysStr)
        {
            var keys = (keysStr ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var key in keys)
            {
                if (elem.TryGetProperty(key.Trim(), out var prop) && prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();
            }
            return null;
        }

        private static double? FindMatchingDoubleProperty(JsonElement elem, string keysStr)
        {
            var keys = (keysStr ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var key in keys)
            {
                if (elem.TryGetProperty(key.Trim(), out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d)) return d;
                    if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ds)) return ds;
                }
            }
            return null;
        }

        private static JsonElement? FindMatchingArrayProperty(JsonElement elem, string keysStr)
        {
            var keys = (keysStr ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var key in keys)
            {
                if (elem.TryGetProperty(key.Trim(), out var prop) && prop.ValueKind == JsonValueKind.Array)
                    return prop;
            }
            return null;
        }
    }
}
