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

        /// <summary>Node đang được chỉnh sửa (null = tạo mới).</summary>
        private GitSourceNode? _editingNode;

        /// <summary>Cờ chặn checkout-loop khi cập nhật Branch từ code (clone, load).</summary>
        private bool _suppressBranchCheckout;

        /// <summary>Event yêu cầu code-behind chuyển sang tab Git.</summary>
        public event Action? RequestSwitchToGitTab;

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

            // Nếu LocalPath đã là git repo thì load branches sẵn
            TryLoadBranchesFromLocalPath();
        }

        // ═══════════════════════════════════════════
        // BRANCH LOADING / CHECKOUT
        // ═══════════════════════════════════════════

        /// <summary>Load danh sách branch local nếu LocalPath là git repo hợp lệ.</summary>
        private void TryLoadBranchesFromLocalPath()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(LocalPath) || !Directory.Exists(LocalPath))
                    return;
                if (!_gitService.IsGitRepository(LocalPath))
                    return;

                var branches = _gitService.GetBranches(LocalPath);
                if (branches.Count == 0) return;

                AvailableBranches.Clear();
                foreach (var b in branches)
                    AvailableBranches.Add(b);

                // Lấy nhánh hiện tại từ repo, ưu tiên hiển thị đúng
                var status = _gitService.GetStatus(LocalPath);
                if (status.IsValid && !string.IsNullOrWhiteSpace(status.Branch))
                {
                    _suppressBranchCheckout = true;
                    Branch = status.Branch;
                    _suppressBranchCheckout = false;
                }
                else if (!AvailableBranches.Contains(Branch))
                {
                    // Fallback: chọn main/master nếu có, không thì lấy branch đầu tiên
                    var preferred = AvailableBranches.FirstOrDefault(b => b.Equals("main", StringComparison.OrdinalIgnoreCase))
                                 ?? AvailableBranches.FirstOrDefault(b => b.Equals("master", StringComparison.OrdinalIgnoreCase))
                                 ?? AvailableBranches[0];
                    _suppressBranchCheckout = true;
                    Branch = preferred;
                    _suppressBranchCheckout = false;
                }
            }
            catch (Exception ex) { AppendLog($"⚠️ Không load được branches: {ex.Message}"); }
        }

        /// <summary>Khi user chọn / gõ branch khác trong ComboBox → tự checkout (nếu repo hợp lệ).</summary>
        partial void OnBranchChanged(string value)
        {
            if (_suppressBranchCheckout) return;
            if (string.IsNullOrWhiteSpace(value)) return;
            if (string.IsNullOrWhiteSpace(LocalPath) || !Directory.Exists(LocalPath)) return;
            if (!_gitService.IsGitRepository(LocalPath)) return;

            // Chỉ checkout nếu nhánh mới khác nhánh hiện tại
            var status = _gitService.GetStatus(LocalPath);
            if (status.IsValid && status.Branch.Equals(value, StringComparison.Ordinal)) return;

            // Chỉ checkout với nhánh đã tồn tại trong AvailableBranches để tránh
            // checkout nhầm khi user đang gõ dở
            if (!AvailableBranches.Contains(value)) return;

            try
            {
                var ok = _gitService.CheckoutBranch(LocalPath, value);
                AppendLog(ok ? $"🔀 Đã chuyển sang nhánh '{value}'" : $"❌ Không chuyển được sang '{value}'");
            }
            catch (Exception ex) { AppendLog($"❌ Checkout {value}: {ex.Message}"); }
        }

        /// <summary>Khi LocalPath thay đổi → thử load branches từ path mới.</summary>
        partial void OnLocalPathChanged(string value)
        {
            TryLoadBranchesFromLocalPath();
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

                    // Load danh sách branches local
                    var branches = _gitService.GetBranches(clonePath);
                    AvailableBranches.Clear();
                    foreach (var b in branches) AvailableBranches.Add(b);

                    // Set Branch theo nhánh thực tế đã checkout, ưu tiên main/master
                    _suppressBranchCheckout = true;
                    var resolved = !string.IsNullOrWhiteSpace(result.Branch) && AvailableBranches.Contains(result.Branch)
                        ? result.Branch
                        : AvailableBranches.FirstOrDefault(b => b.Equals("main", StringComparison.OrdinalIgnoreCase))
                          ?? AvailableBranches.FirstOrDefault(b => b.Equals("master", StringComparison.OrdinalIgnoreCase))
                          ?? AvailableBranches.FirstOrDefault()
                          ?? result.Branch;
                    Branch = resolved ?? string.Empty;
                    _suppressBranchCheckout = false;

                    if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = repoName;
                    _gitNode.RefreshCloneStatus();
                    AppendLog($"✅ Clone OK! {repoName} ({Branch})");
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
            // Xác định node đích: nếu đang edit thì dùng node đó, ngược lại dùng _gitNode mới
            var targetNode = _editingNode ?? _gitNode;

            targetNode.RepoUrl = RepoUrl;
            targetNode.LocalPath = LocalPath;
            targetNode.Branch = Branch;
            targetNode.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? "Git Source" : DisplayName;
            targetNode.Title = targetNode.DisplayName;
            targetNode.IconKey = IconKey;
            targetNode.IconColorKey = IconColorKey;
            targetNode.ColorKey = NodeColorKey;
            targetNode.TooltipText = TooltipText;
            targetNode.CommandText = CommandText;

            var brush = ResolveBrushFromKey(NodeColorKey);
            targetNode.NodeBrush = brush;

            // Thêm vào danh sách tổng hợp (nếu chưa có)
            if (!SavedRepos.Any(r => r.Id == targetNode.Id))
                SavedRepos.Add(targetNode);

            // Sync lên ViewModel chính
            if (_host.ViewModel is WorkflowEditorViewModel vm)
            {
                if (!vm.GitRepoNodes.Any(r => r.Id == targetNode.Id))
                    vm.GitRepoNodes.Add(targetNode);
                vm.HasGitRepos = vm.GitRepoNodes.Count > 0;
            }

            AppendLog(_editingNode != null
                ? $"✏️ Đã cập nhật: {targetNode.DisplayName}"
                : $"💾 Đã lưu: {targetNode.DisplayName}");

            // Persist ra file
            GitRepoStorageService.Save(SavedRepos);

            // Reset trạng thái edit
            _editingNode = null;

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

            // Ghi nhớ node đang edit
            _editingNode = repo;

            // Load thông tin repo vào form để chỉnh sửa
            // Chặn checkout-loop trong lúc set Branch (sẽ load lại branches sau)
            _suppressBranchCheckout = true;
            RepoUrl = repo.RepoUrl;
            LocalPath = repo.LocalPath; // OnLocalPathChanged sẽ load branches
            Branch = repo.Branch;
            _suppressBranchCheckout = false;

            DisplayName = repo.DisplayName;
            IconKey = repo.IconKey;
            IconColorKey = repo.IconColorKey ?? "White";
            NodeColorKey = repo.ColorKey ?? "Indigo";
            TooltipText = repo.TooltipText;
            CommandText = repo.CommandText ?? string.Empty;

            // Đảm bảo branches được load lại (trường hợp LocalPath không đổi)
            TryLoadBranchesFromLocalPath();

            // Chuyển sang tab Git
            RequestSwitchToGitTab?.Invoke();
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
