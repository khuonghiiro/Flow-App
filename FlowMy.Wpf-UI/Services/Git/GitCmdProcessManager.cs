using System.Collections.Concurrent;
using System.Diagnostics;

namespace FlowMy.Services.Git
{
    /// <summary>
    /// Quản lý các process cmd được spawn bởi Git repos.
    /// Khi app tắt, tất cả process sẽ bị kill.
    /// </summary>
    public static class GitCmdProcessManager
    {
        private static readonly ConcurrentDictionary<string, List<Process>> _processes = new();

        /// <summary>
        /// Chạy danh sách lệnh tuần tự (mỗi dòng = 1 lệnh) trong workingDir.
        /// </summary>
        /// <param name="repoId">ID repo để track process.</param>
        /// <param name="commandText">Nhiều dòng, mỗi dòng = 1 lệnh cmd.</param>
        /// <param name="workingDir">Thư mục chạy lệnh (root folder git repo).</param>
        /// <param name="showWindow">True = hiện cmd window, False = chạy ngầm.</param>
        /// <param name="runAsBatch">True = gộp tất cả lệnh thành 1 file .bat chạy 1 lần.</param>
        /// <param name="onOutput">Callback nhận output (chỉ khi showWindow=false).</param>
        /// <param name="onCompleted">Callback khi tất cả lệnh chạy xong.</param>
        public static async Task RunCommandsAsync(string repoId, string commandText, string workingDir,
            bool showWindow, bool runAsBatch = false, Action<string>? onOutput = null, Action? onCompleted = null)
        {
            if (string.IsNullOrWhiteSpace(commandText) || string.IsNullOrWhiteSpace(workingDir))
                return;

            // Kill process cũ của repo này (nếu có)
            KillProcesses(repoId);

            var processList = new List<Process>();
            _processes[repoId] = processList;

            try
            {
                if (runAsBatch)
                {
                    // ── Chế độ Batch: gộp tất cả lệnh vào 1 file .bat, chạy 1 lần ──
                    await RunAsBatchFileAsync(repoId, commandText, workingDir, showWindow, processList, onOutput);
                }
                else
                {
                    // ── Chế độ tuần tự: mỗi dòng = 1 process riêng ──
                    await RunSequentialAsync(repoId, commandText, workingDir, showWindow, processList, onOutput);
                }
            }
            catch (Exception ex)
            {
                onOutput?.Invoke($"❌ Exception: {ex.Message}");
            }
            finally
            {
                onCompleted?.Invoke();
            }
        }

        /// <summary>Chạy tuần tự từng dòng lệnh (mỗi dòng = 1 process).</summary>
        private static async Task RunSequentialAsync(string repoId, string commandText, string workingDir,
            bool showWindow, List<Process> processList, Action<string>? onOutput)
        {
            var lines = commandText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            if (lines.Length == 0) return;

            // Thư mục hiện tại (có thể thay đổi bởi cd .root/...)
            var currentDir = workingDir;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // ── Xử lý lệnh đặc biệt: cd .root hoặc cd .root/subfolder ──
                if (line.StartsWith("cd .root", StringComparison.OrdinalIgnoreCase))
                {
                    var afterRoot = line.Substring("cd .root".Length).TrimStart('/', '\\').Trim();
                    if (string.IsNullOrEmpty(afterRoot))
                        currentDir = workingDir;
                    else
                        currentDir = System.IO.Path.Combine(workingDir, afterRoot.Replace('/', '\\'));

                    if (!System.IO.Directory.Exists(currentDir))
                    {
                        onOutput?.Invoke($"⚠️ Folder không tồn tại: {currentDir}");
                        currentDir = workingDir;
                    }
                    else
                    {
                        onOutput?.Invoke($"📂 cd → {currentDir}");
                    }
                    continue;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    // Khi hiện window: dùng /c với & pause để giữ cửa sổ mở cho user đọc output
                    Arguments = showWindow ? $"/c \"{line} & echo. & echo ─── Nhấn phím bất kỳ để tiếp tục ─── & pause >nul\"" : $"/c {line}",
                    WorkingDirectory = currentDir,
                    UseShellExecute = showWindow,
                    CreateNoWindow = !showWindow,
                    RedirectStandardOutput = !showWindow,
                    RedirectStandardError = !showWindow
                };

                var proc = Process.Start(psi);
                if (proc == null)
                {
                    onOutput?.Invoke($"❌ Không khởi tạo được: {line}");
                    continue;
                }

                processList.Add(proc);

                if (!showWindow)
                {
                    // Đọc output async
                    var stdout = await proc.StandardOutput.ReadToEndAsync();
                    var stderr = await proc.StandardError.ReadToEndAsync();
                    await proc.WaitForExitAsync();

                    if (!string.IsNullOrWhiteSpace(stdout))
                        onOutput?.Invoke(stdout.TrimEnd());
                    if (!string.IsNullOrWhiteSpace(stderr))
                        onOutput?.Invoke($"[STDERR] {stderr.TrimEnd()}");
                    onOutput?.Invoke($"[Exit: {proc.ExitCode}] {line}");

                    // Nếu lệnh fail (exit code != 0), dừng chuỗi
                    if (proc.ExitCode != 0)
                    {
                        onOutput?.Invoke($"⚠️ Dừng do lệnh trước fail (exit {proc.ExitCode}).");
                        break;
                    }
                }
                else
                {
                    // Khi hiện window: chờ process kết thúc trước khi chạy lệnh tiếp
                    await proc.WaitForExitAsync();

                    if (proc.ExitCode != 0)
                    {
                        onOutput?.Invoke($"⚠️ Lệnh '{line}' exit code {proc.ExitCode}. Dừng.");
                        break;
                    }
                }
            }
        }

        /// <summary>Gộp tất cả lệnh vào 1 file .bat và chạy 1 lần (giống chạy script).</summary>
        private static async Task RunAsBatchFileAsync(string repoId, string commandText, string workingDir,
            bool showWindow, List<Process> processList, Action<string>? onOutput)
        {
            // Tạo file .bat tạm
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FlowMy-Git");
            if (!System.IO.Directory.Exists(tempDir))
                System.IO.Directory.CreateDirectory(tempDir);

            var batFile = System.IO.Path.Combine(tempDir, $"{repoId.Replace("-", "").Substring(0, Math.Min(8, repoId.Length))}_{DateTime.Now:HHmmss}.bat");

            // Xử lý cd .root trong nội dung bat
            var lines = commandText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            var batContent = new System.Text.StringBuilder();
            batContent.AppendLine("@echo off");
            batContent.AppendLine($"cd /d \"{workingDir}\"");

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // Thay thế cd .root thành cd /d "workingDir\..."
                if (line.StartsWith("cd .root", StringComparison.OrdinalIgnoreCase))
                {
                    var afterRoot = line.Substring("cd .root".Length).TrimStart('/', '\\').Trim();
                    if (string.IsNullOrEmpty(afterRoot))
                        batContent.AppendLine($"cd /d \"{workingDir}\"");
                    else
                        batContent.AppendLine($"cd /d \"{System.IO.Path.Combine(workingDir, afterRoot.Replace('/', '\\'))}\"");
                }
                else
                {
                    batContent.AppendLine(line);
                }
            }

            // Thêm pause ở cuối nếu hiện window (để user đọc output)
            if (showWindow)
                batContent.AppendLine("pause");

            System.IO.File.WriteAllText(batFile, batContent.ToString(), System.Text.Encoding.Default);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batFile}\"",
                WorkingDirectory = workingDir,
                UseShellExecute = showWindow,
                CreateNoWindow = !showWindow,
                RedirectStandardOutput = !showWindow,
                RedirectStandardError = !showWindow
            };

            var proc = Process.Start(psi);
            if (proc == null)
            {
                onOutput?.Invoke($"❌ Không khởi tạo được batch file.");
                return;
            }

            processList.Add(proc);

            if (!showWindow)
            {
                var stdout = await proc.StandardOutput.ReadToEndAsync();
                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(stdout))
                    onOutput?.Invoke(stdout.TrimEnd());
                if (!string.IsNullOrWhiteSpace(stderr))
                    onOutput?.Invoke($"[STDERR] {stderr.TrimEnd()}");
                onOutput?.Invoke($"[Exit: {proc.ExitCode}] batch");
            }
            else
            {
                await proc.WaitForExitAsync();
            }

            // Xóa file .bat tạm sau khi chạy xong
            try { System.IO.File.Delete(batFile); } catch { /* ignore */ }
        }

        /// <summary>Kill tất cả process của 1 repo.</summary>
        public static void KillProcesses(string repoId)
        {
            if (_processes.TryRemove(repoId, out var list))
            {
                foreach (var p in list)
                {
                    try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
                    catch { /* ignore */ }
                    finally { p.Dispose(); }
                }
            }
        }

        /// <summary>Kill TẤT CẢ process đang chạy (gọi khi app tắt).</summary>
        public static void KillAll()
        {
            foreach (var kvp in _processes)
            {
                foreach (var p in kvp.Value)
                {
                    try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
                    catch { /* ignore */ }
                    finally { p.Dispose(); }
                }
            }
            _processes.Clear();
        }

        /// <summary>Kiểm tra repo có process đang chạy hay không.</summary>
        public static bool IsRunning(string repoId)
        {
            if (!_processes.TryGetValue(repoId, out var list)) return false;
            return list.Any(p => { try { return !p.HasExited; } catch { return false; } });
        }
    }
}
