using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Models;
using FlowMy.Services.Git;
using FlowMy.Services.Interaction;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace FlowMy.ViewModels
{
    public partial class GitManagerDialogViewModel : ObservableObject
    {
        private readonly GitSourceNode _gitNode;
        private readonly GitService _gitService = new();
        private readonly IWorkflowEditorHost _host;
        private CancellationTokenSource? _operationCts;

        // Clone tab
        [ObservableProperty] private string _repoUrl = string.Empty;
        [ObservableProperty] private string _localPath = string.Empty;
        [ObservableProperty] private string _branch = "main";

        // Hiển thị tab
        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private string _iconKey = "git-alt brands";
        [ObservableProperty] private string _iconColorKey = "White";
        [ObservableProperty] private string _nodeColorKey = "Indigo";
        [ObservableProperty] private string _tooltipText = string.Empty;

        // Cài đặt tab
        [ObservableProperty] private string _commandText = string.Empty;
        [ObservableProperty] private string _commandOutput = string.Empty;

        // Preview (cập nhật từ code-behind khi đổi màu)
        [ObservableProperty] private System.Windows.Media.Brush _previewNodeBrush = System.Windows.Media.Brushes.Indigo;
        [ObservableProperty] private System.Windows.Media.Brush _previewIconBrush = System.Windows.Media.Brushes.White;

        // Status
        [ObservableProperty] private bool _isOperating;
        [ObservableProperty] private string _operationLog = string.Empty;
        [ObservableProperty] private string _progressStatusText = string.Empty;
        [ObservableProperty] private bool _showSaveSuccess;

        public ObservableCollection<string> AvailableBranches { get; } = new();

        /// <summary>Danh sách repos đã lưu (hiển thị ở tab Tổng hợp).</summary>
        public ObservableCollection<GitSourceNode> SavedRepos { get; } = new();

        /// <summary>Node kết quả (dùng khi đóng dialog).</summary>
        public GitSourceNode? ResultNode { get; private set; }

        public List<TitleColorOption> TitleColorOptions { get; } = new()
        {
            new TitleColorOption("White", "Trắng"),
            new TitleColorOption("Black", "Đen"),
            new TitleColorOption("Indigo", "Indigo"),
            new TitleColorOption("PrimaryBrush", "Primary Blue"),
            new TitleColorOption("SuccessBrush", "Success Green"),
            new TitleColorOption("DangerBrush", "Danger Red"),
            new TitleColorOption("WarningBrush", "Warning Orange"),
            new TitleColorOption("InfoBrush", "Info Cyan"),
            new TitleColorOption("CoralBrush", "Coral"),
            new TitleColorOption("OceanBrush", "Ocean"),
            new TitleColorOption("LavenderBrush", "Lavender"),
            new TitleColorOption("ForestPine", "Forest Pine"),
            new TitleColorOption("CharcoalMist", "Charcoal"),
            new TitleColorOption("BerryPurple", "Berry Purple"),
        };

        public GitManagerDialogViewModel(GitSourceNode node, IWorkflowEditorHost host)
        {
            _gitNode = node ?? throw new ArgumentNullException(nameof(node));
            _host = host;

            var appRoot = AppDomain.CurrentDomain.BaseDirectory;
            LocalPath = Path.Combine(appRoot, "Project_Git");

            // Sync from node if already configured
            if (!string.IsNullOrWhiteSpace(node.RepoUrl)) RepoUrl = node.RepoUrl;
            if (!string.IsNullOrWhiteSpace(node.LocalPath)) LocalPath = node.LocalPath;
            if (!string.IsNullOrWhiteSpace(node.Branch)) Branch = node.Branch;
            if (!string.IsNullOrWhiteSpace(node.DisplayName)) DisplayName = node.DisplayName;
            if (!string.IsNullOrWhiteSpace(node.IconKey)) IconKey = node.IconKey;
            if (!string.IsNullOrWhiteSpace(node.IconColorKey)) IconColorKey = node.IconColorKey;
            if (!string.IsNullOrWhiteSpace(node.ColorKey)) NodeColorKey = node.ColorKey;
            if (!string.IsNullOrWhiteSpace(node.TooltipText)) TooltipText = node.TooltipText;
            if (!string.IsNullOrWhiteSpace(node.CommandText)) CommandText = node.CommandText;

            // Load saved repos from ViewModel (if available)
            if (host.ViewModel is ViewModels.WorkflowEditorViewModel vm)
            {
                foreach (var r in vm.GitRepoNodes)
                    SavedRepos.Add(r);
            }
        }

        // ═══════════════════════════════════════════
        // CLONE / PULL
        // ═══════════════════════════════════════════

        [RelayCommand]
        private async Task CloneRepoAsync()
        {
            if (string.IsNullOrWhiteSpace(RepoUrl))
            { AppendLog("❌ Nhập URL repository."); return; }

            var repoName = ExtractRepoNameFromUrl(RepoUrl);
            if (string.IsNullOrWhiteSpace(repoName))
            { AppendLog("❌ URL không hợp lệ."); return; }

            if (string.IsNullOrWhiteSpace(LocalPath))
            { AppendLog("❌ Chọn thư mục đích."); return; }

            if (!Directory.Exists(LocalPath))
                Directory.CreateDirectory(LocalPath);

            var clonePath = Path.Combine(LocalPath, repoName);
            if (Directory.Exists(clonePath) && Directory.GetFileSystemEntries(clonePath).Length > 0)
            {
                if (_gitService.IsGitRepository(clonePath))
                { AppendLog($"⚠️ '{repoName}' đã tồn tại. Dùng Pull."); LocalPath = clonePath; return; }
                else
                { AppendLog($"❌ '{repoName}' không rỗng và không phải Git repo."); return; }
            }

            _operationCts?.Cancel();
            _operationCts = new CancellationTokenSource();
            var ct = _operationCts.Token;
            IsOperating = true;
            ProgressStatusText = "Đang kết nối...";
            AppendLog($"🔄 Clone {RepoUrl} → {repoName}/");

            try
            {
                var result = await Task.Run(() => _gitService.CloneRepository(RepoUrl, clonePath, Branch,
                    onProgress: msg => { if (!ct.IsCancellationRequested) Application.Current?.Dispatcher.Invoke(() => ProgressStatusText = msg); }), ct);

                if (ct.IsCancellationRequested) return;
                if (result.Success)
                {
                    LocalPath = clonePath;
                    _gitNode.LastCommitHash = result.LastCommitHash;
                    _gitNode.LastPullTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    Branch = result.Branch;
                    if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = repoName;
                    _gitNode.RefreshCloneStatus();
                    AppendLog($"✅ Clone OK! {repoName} ({result.Branch})");
                }
                else AppendLog($"❌ {result.ErrorMessage}");
            }
            catch (OperationCanceledException) { AppendLog("⚠️ Đã huỷ."); }
            catch (Exception ex) { AppendLog($"❌ {ex.Message}"); }
            finally { await Task.Delay(600); IsOperating = false; ProgressStatusText = string.Empty; }
        }

        [RelayCommand]
        private async Task PullRepoAsync()
        {
            var path = LocalPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            { AppendLog("❌ Thư mục chưa tồn tại."); return; }

            _operationCts?.Cancel();
            _operationCts = new CancellationTokenSource();
            var ct = _operationCts.Token;
            IsOperating = true;
            ProgressStatusText = "Fetching...";
            AppendLog("🔄 Pull...");

            try
            {
                var result = await Task.Run(() => _gitService.PullRepository(path,
                    onProgress: msg => { if (!ct.IsCancellationRequested) Application.Current?.Dispatcher.Invoke(() => ProgressStatusText = msg); }), ct);
                if (ct.IsCancellationRequested) return;
                if (result.Success)
                {
                    _gitNode.LastCommitHash = result.LastCommitHash;
                    AppendLog($"✅ Pull OK — {result.Status}");
                }
                else AppendLog($"❌ {result.ErrorMessage}");
            }
            catch (OperationCanceledException) { AppendLog("⚠️ Đã huỷ."); }
            catch (Exception ex) { AppendLog($"❌ {ex.Message}"); }
            finally { await Task.Delay(600); IsOperating = false; ProgressStatusText = string.Empty; }
        }

        [RelayCommand] private void CancelOperation() => CancelCurrentOperation();
        public void CancelCurrentOperation()
        {
            if (_operationCts is { IsCancellationRequested: false }) { _operationCts.Cancel(); AppendLog("⚠️ Huỷ."); }
        }

        [RelayCommand]
        private void BrowseLocalPath()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Chọn thư mục" };
            if (dlg.ShowDialog() == true) LocalPath = dlg.FolderName;
        }

        // ═══════════════════════════════════════════
        // LƯU VÀO TỔNG HỢP
        // ═══════════════════════════════════════════

        [RelayCommand]
        private async Task SaveRepoAsync()
        {
            _gitNode.RepoUrl = RepoUrl;
            _gitNode.LocalPath = LocalPath;
            _gitNode.Branch = Branch;
            _gitNode.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? "Git Source" : DisplayName;
            _gitNode.Title = _gitNode.DisplayName;
            _gitNode.IconKey = IconKey;
            _gitNode.IconColorKey = IconColorKey;
            _gitNode.ColorKey = NodeColorKey;
            _gitNode.TooltipText = TooltipText;
            _gitNode.CommandText = CommandText;

            var brush = ResolveBrushFromKey(NodeColorKey);
            _gitNode.NodeBrush = brush;

            // Thêm vào danh sách tổng hợp (nếu chưa có)
            if (!SavedRepos.Any(r => r.Id == _gitNode.Id))
                SavedRepos.Add(_gitNode);

            // Sync lên ViewModel chính
            if (_host.ViewModel is WorkflowEditorViewModel vm)
            {
                if (!vm.GitRepoNodes.Any(r => r.Id == _gitNode.Id))
                    vm.GitRepoNodes.Add(_gitNode);
                vm.HasGitRepos = vm.GitRepoNodes.Count > 0;
            }

            AppendLog($"💾 Đã lưu: {_gitNode.DisplayName}");

            // Persist ra file
            GitRepoStorageService.Save(SavedRepos);

            // Hiện thông báo thành công
            ShowSaveSuccess = true;
            await Task.Delay(2500);
            ShowSaveSuccess = false;
        }

        // ═══════════════════════════════════════════
        // TỔNG HỢP ACTIONS
        // ═══════════════════════════════════════════

        [RelayCommand]
        private void RunRepoCommand(GitSourceNode? repo)
        {
            if (repo == null || string.IsNullOrWhiteSpace(repo.CommandText)) return;
            if (string.IsNullOrWhiteSpace(repo.LocalPath) || !Directory.Exists(repo.LocalPath)) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c cd /d \"{repo.LocalPath}\" && {repo.CommandText}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { AppendLog($"❌ {ex.Message}"); }
        }

        [RelayCommand]
        private async Task PullSavedRepoAsync(GitSourceNode? repo)
        {
            if (repo == null || string.IsNullOrWhiteSpace(repo.LocalPath)) return;
            AppendLog($"🔄 Pull {repo.DisplayName}...");
            var result = await Task.Run(() => _gitService.PullRepository(repo.LocalPath));
            AppendLog(result.Success ? $"✅ {repo.DisplayName} pulled." : $"❌ {result.ErrorMessage}");
        }

        [RelayCommand]
        private async Task CloneSavedRepoAsync(GitSourceNode? repo)
        {
            if (repo == null || string.IsNullOrWhiteSpace(repo.RepoUrl)) return;

            var targetPath = repo.LocalPath;
            if (string.IsNullOrWhiteSpace(targetPath))
            { AppendLog("❌ Chưa cấu hình thư mục local."); return; }

            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);

            AppendLog($"⬇ Clone {repo.DisplayName}...");
            try
            {
                var result = await Task.Run(() => _gitService.CloneRepository(repo.RepoUrl, targetPath, repo.Branch));
                if (result.Success)
                {
                    repo.LastCommitHash = result.LastCommitHash;
                    repo.LastPullTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    repo.RefreshCloneStatus();
                    AppendLog($"✅ Clone OK: {repo.DisplayName}");
                    GitRepoStorageService.Save(SavedRepos);
                }
                else AppendLog($"❌ {result.ErrorMessage}");
            }
            catch (Exception ex) { AppendLog($"❌ {ex.Message}"); }
        }

        [RelayCommand]
        private void OpenVsCodeForRepo(GitSourceNode? repo)
        {
            if (repo == null || string.IsNullOrWhiteSpace(repo.LocalPath)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c cd /d \"{repo.LocalPath}\" && code .",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch { AppendLog("❌ Không mở được VS Code. Cài đặt: https://code.visualstudio.com/"); }
        }

        [RelayCommand]
        private void EditRepo(GitSourceNode? repo)
        {
            if (repo == null) return;
            // Load thông tin repo vào form để chỉnh sửa
            RepoUrl = repo.RepoUrl;
            LocalPath = repo.LocalPath;
            Branch = repo.Branch;
            DisplayName = repo.DisplayName;
            IconKey = repo.IconKey;
            IconColorKey = repo.IconColorKey ?? "White";
            NodeColorKey = repo.ColorKey ?? "Indigo";
            TooltipText = repo.TooltipText;
            CommandText = repo.CommandText ?? string.Empty;
        }

        [RelayCommand]
        private void RemoveRepo(GitSourceNode? repo)
        {
            if (repo == null) return;
            SavedRepos.Remove(repo);
            if (_host.ViewModel is WorkflowEditorViewModel vm)
            {
                var item = vm.GitRepoNodes.FirstOrDefault(r => r.Id == repo.Id);
                if (item != null) vm.GitRepoNodes.Remove(item);
                vm.HasGitRepos = vm.GitRepoNodes.Count > 0;
            }
            GitRepoStorageService.Save(SavedRepos);
        }

        // ═══════════════════════════════════════════
        // CÀI ĐẶT — TEST RUN
        // ═══════════════════════════════════════════

        [RelayCommand]
        private async Task TestRunCommandAsync()
        {
            if (string.IsNullOrWhiteSpace(CommandText)) { CommandOutput = "❌ Chưa nhập lệnh."; return; }
            var workDir = LocalPath;
            if (string.IsNullOrWhiteSpace(workDir) || !Directory.Exists(workDir)) { CommandOutput = "❌ Thư mục không tồn tại."; return; }

            CommandOutput = $"⏳ Chạy: {CommandText}\n";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {CommandText}",
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) { CommandOutput += "❌ Không khởi tạo được."; return; }
                var stdout = await proc.StandardOutput.ReadToEndAsync();
                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();
                CommandOutput = stdout + (string.IsNullOrWhiteSpace(stderr) ? "" : $"\n[STDERR]\n{stderr}") + $"\n[Exit: {proc.ExitCode}]";
            }
            catch (Exception ex) { CommandOutput += $"\n❌ {ex.Message}"; }
        }

        // ═══════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════

        private void AppendLog(string msg)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            OperationLog = $"[{ts}] {msg}\n{OperationLog}";
        }

        private static string? ExtractRepoNameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim().TrimEnd('/');
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) url = url[..^4];
            var lastSep = Math.Max(url.LastIndexOf('/'), url.LastIndexOf(':'));
            if (lastSep < 0 || lastSep >= url.Length - 1) return null;
            var name = url[(lastSep + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Contains("..") || name.Contains("\\")) return null;
            return name;
        }

        /// <summary>Resolve ColorKey thành Brush — hỗ trợ hex (#RRGGBB) và named resource key.</summary>
        private static System.Windows.Media.Brush ResolveBrushFromKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return System.Windows.Media.Brushes.Transparent;

            // Hex color
            if (key.StartsWith("#"))
            {
                try
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(key);
                    return new System.Windows.Media.SolidColorBrush(color);
                }
                catch { return System.Windows.Media.Brushes.Transparent; }
            }

            // Named system colors
            if (key.Equals("White", StringComparison.OrdinalIgnoreCase))
                return System.Windows.Media.Brushes.White;
            if (key.Equals("Black", StringComparison.OrdinalIgnoreCase))
                return System.Windows.Media.Brushes.Black;

            // Resource lookup
            var brush = Application.Current?.TryFindResource($"{key}Brush") as System.Windows.Media.Brush
                     ?? Application.Current?.TryFindResource(key) as System.Windows.Media.Brush;
            return brush ?? System.Windows.Media.Brushes.Transparent;
        }
    }
}
