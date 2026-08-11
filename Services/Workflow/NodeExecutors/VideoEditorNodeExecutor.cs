using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class VideoEditorNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is VideoEditorNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            if (node is not VideoEditorNode videoEditorNode) return;
            var sw = Stopwatch.StartNew();

            try
            {
                // 1. Resolve Input Video URL/Path
                string videoPath = env.Service.ResolveValueByNodeIdAndKeyForExecution(
                    env.Connections, videoEditorNode.SourceNodeId, videoEditorNode.SourceOutputKey, env)?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(videoPath))
                {
                    videoPath = videoEditorNode.InputVideoUrl;
                }

                if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
                {
                    throw new FileNotFoundException($"[VideoEditor] File video đầu vào không tồn tại: '{videoPath}'");
                }

                // 2. Resolve Output Directory
                string outputDir = !string.IsNullOrWhiteSpace(videoEditorNode.OutputFolderPath) && Directory.Exists(videoEditorNode.OutputFolderPath)
                    ? videoEditorNode.OutputFolderPath
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output", "VideoEditor", $"{videoEditorNode.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");

                Directory.CreateDirectory(outputDir);

                // 3. Resolve FFmpeg Binary
                string ffmpegExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg");
                if (string.IsNullOrWhiteSpace(ffmpegExe) || (!File.Exists(ffmpegExe) && !CanRunCommand(ffmpegExe)))
                {
                    throw new FileNotFoundException("Không tìm thấy tệp ffmpeg.exe trong hệ thống hoặc thư mục ứng dụng.");
                }

                // 4. Build FFmpeg Filter Graphs & Arguments
                var args = BuildFfmpegArguments(videoEditorNode, videoPath, outputDir, out string primaryOutputPath, out string framesDir, out string audioPath, out string thumbPath);

                // 5. Run FFmpeg Process
                var (exitCode, logs) = await RunProcessAsync(ffmpegExe, args);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"FFmpeg xử lý video thất bại với exit code {exitCode}.\nLog:\n{logs}");
                }

                // 6. Save Scoped Outputs
                if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                {
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, videoEditorNode.Id, "video_path", primaryOutputPath);
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, videoEditorNode.Id, "frames_folder", framesDir);
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, videoEditorNode.Id, "audio_path", audioPath);
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, videoEditorNode.Id, "thumbnail_path", thumbPath);
                }

                sw.Stop();
                env.OnNodeCompleted?.Invoke(videoEditorNode, sw.Elapsed);
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(videoEditorNode, ex.Message);
                throw;
            }

            await env.TraverseOutputsAsync(videoEditorNode);
        }

        private string BuildFfmpegArguments(
            VideoEditorNode node,
            string inputVideoPath,
            string outputDir,
            out string primaryOutputPath,
            out string framesDir,
            out string audioPath,
            out string thumbPath)
        {
            var sb = new StringBuilder();
            sb.Append("-y ");

            // Trim Start
            if (node.TrimEnabled && !string.IsNullOrWhiteSpace(node.TrimStartTime))
            {
                sb.Append($"-ss {node.TrimStartTime.Trim()} ");
            }

            sb.Append($"-i \"{inputVideoPath}\" ");

            // Trim End
            if (node.TrimEnabled && !string.IsNullOrWhiteSpace(node.TrimEndTime))
            {
                sb.Append($"-to {node.TrimEndTime.Trim()} ");
            }

            // --- Video Filters (-vf) ---
            var vFilters = new List<string>();

            // Color / EQ
            vFilters.Add($"eq=brightness={node.Brightness:F2}:contrast={node.Contrast:F2}:saturation={node.Saturation:F2}:gamma={node.Gamma:F2}");

            // Hue Adjustment
            if (Math.Abs(node.Hue) > 0.1)
            {
                vFilters.Add($"hue=h={node.Hue:F0}");
            }

            // Filter Presets
            if (string.Equals(node.FilterPreset, "Grayscale", StringComparison.OrdinalIgnoreCase))
            {
                vFilters.Add("hue=s=0");
            }
            else if (string.Equals(node.FilterPreset, "Sepia", StringComparison.OrdinalIgnoreCase))
            {
                vFilters.Add("colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131");
            }
            else if (string.Equals(node.FilterPreset, "Invert", StringComparison.OrdinalIgnoreCase))
            {
                vFilters.Add("negate");
            }
            else if (string.Equals(node.FilterPreset, "Vivid", StringComparison.OrdinalIgnoreCase))
            {
                vFilters.Add("eq=saturation=1.4:contrast=1.2");
            }
            else if (string.Equals(node.FilterPreset, "Warm", StringComparison.OrdinalIgnoreCase))
            {
                vFilters.Add("hue=h=15");
            }
            else if (string.Equals(node.FilterPreset, "Cool", StringComparison.OrdinalIgnoreCase))
            {
                vFilters.Add("hue=h=-20");
            }

            // Speed
            if (Math.Abs(node.Speed - 1.0) > 0.01)
            {
                double setpts = 1.0 / Math.Max(node.Speed, 0.1);
                vFilters.Add($"setpts={setpts:F4}*PTS");
            }

            // Rotate / Flip
            switch (node.RotateFlip)
            {
                case "Rotate90": vFilters.Add("transpose=1"); break;
                case "Rotate180": vFilters.Add("transpose=2,transpose=2"); break;
                case "Rotate270": vFilters.Add("transpose=2"); break;
                case "FlipHorizontal": vFilters.Add("hflip"); break;
                case "FlipVertical": vFilters.Add("vflip"); break;
            }

            // Scale
            if (node.ScaleEnabled && node.TargetWidth > 0 && node.TargetHeight > 0)
            {
                vFilters.Add($"scale={node.TargetWidth}:{node.TargetHeight}");
            }

            // Watermark Text
            if (node.WatermarkEnabled && !string.IsNullOrWhiteSpace(node.WatermarkText))
            {
                string textEscaped = node.WatermarkText.Replace(":", "\\:").Replace("'", "\\'");
                string posStr = node.WatermarkPosition switch
                {
                    "TopLeft" => "x=10:y=10",
                    "TopRight" => "x=w-tw-10:y=10",
                    "BottomLeft" => "x=10:y=h-th-10",
                    "Center" => "x=(w-tw)/2:y=(h-th)/2",
                    _ => "x=w-tw-10:y=h-th-10"
                };
                vFilters.Add($"drawtext=text='{textEscaped}':{posStr}:fontsize=24:fontcolor=white");
            }

            if (vFilters.Count > 0)
            {
                sb.Append($"-vf \"{string.Join(",", vFilters)}\" ");
            }

            // --- Audio Filters (-af) ---
            if (string.Equals(node.AudioMode, "Mute", StringComparison.OrdinalIgnoreCase) || string.Equals(node.ExportMode, "Gif", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("-an ");
            }
            else
            {
                var aFilters = new List<string>();
                if (Math.Abs(node.Speed - 1.0) > 0.01)
                {
                    aFilters.Add($"atempo={Math.Clamp(node.Speed, 0.5, 2.0):F2}");
                }
                if (Math.Abs(node.AudioVolume - 1.0) > 0.01)
                {
                    aFilters.Add($"volume={node.AudioVolume:F2}");
                }
                if (aFilters.Count > 0)
                {
                    sb.Append($"-af \"{string.Join(",", aFilters)}\" ");
                }
            }

            // --- Output Modes ---
            string format = string.IsNullOrWhiteSpace(node.ExportFormat) ? "mp4" : node.ExportFormat.Trim().ToLower();
            framesDir = string.Empty;
            audioPath = string.Empty;
            thumbPath = string.Empty;

            if (string.Equals(node.ExportMode, "FrameSequence", StringComparison.OrdinalIgnoreCase))
            {
                framesDir = Path.Combine(outputDir, "frames");
                Directory.CreateDirectory(framesDir);
                primaryOutputPath = framesDir;
                sb.Append($"-r {Math.Max(node.ExportFps, 0.1):F2} \"{Path.Combine(framesDir, "frame_%04d.jpg")}\"");
            }
            else if (string.Equals(node.ExportMode, "SingleFrame", StringComparison.OrdinalIgnoreCase))
            {
                thumbPath = Path.Combine(outputDir, $"thumbnail.{format}");
                primaryOutputPath = thumbPath;
                sb.Append($"-vframes 1 \"{thumbPath}\"");
            }
            else if (string.Equals(node.ExportMode, "AudioOnly", StringComparison.OrdinalIgnoreCase))
            {
                audioPath = Path.Combine(outputDir, $"audio.{(format == "mp4" ? "mp3" : format)}");
                primaryOutputPath = audioPath;
                sb.Append($"\"{audioPath}\"");
            }
            else if (string.Equals(node.ExportMode, "Gif", StringComparison.OrdinalIgnoreCase))
            {
                primaryOutputPath = Path.Combine(outputDir, "output_animated.gif");
                sb.Append($"\"{primaryOutputPath}\"");
            }
            else
            {
                primaryOutputPath = Path.Combine(outputDir, $"output_video.{format}");
                sb.Append($"\"{primaryOutputPath}\"");
            }

            return sb.ToString();
        }

        private static bool CanRunCommand(string cmd)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = "-version",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                return p != null;
            }
            catch { return false; }
        }

        private static async Task<(int ExitCode, string Logs)> RunProcessAsync(string exe, string args)
        {
            var tcs = new TaskCompletionSource<(int, string)>();
            var logs = new StringBuilder();

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (s, e) => { if (e.Data != null) logs.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) logs.AppendLine(e.Data); };

            process.Exited += (s, e) =>
            {
                tcs.TrySetResult((process.ExitCode, logs.ToString()));
                process.Dispose();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return await tcs.Task;
        }
    }
}
