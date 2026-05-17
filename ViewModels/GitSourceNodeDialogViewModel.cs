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
    public partial class GitSourceNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly GitSourceNode _gitNode;
        private readonly GitService _gitService = new();

        [ObservableProperty] private string _repoUrl = string.Empty;
        [ObservableProperty] private string _localPath = string.Empty;
        [ObservableProperty] private string _branch = "main";
        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private string _iconKey = "code-branch duotone-regular";
        [ObservableProperty] private string _tooltipText = string.Empty;
        [ObservableProperty] private string _contextMenuDescription = string.Empty;
        [ObservableProperty] private string _vscodiumPath = "vscodium";
        [ObservableProperty] private bool _autoOpenOnExecute = true;

        // Status
        [ObservableProperty] private string _statusText = "Chưa kết nối";
        [ObservableProperty] private string _lastCommitDisplay = string.Empty;
        [ObservableProperty] private string _lastPullTimeDisplay = string.Empty;
        [ObservableProperty] private bool _isRepoValid;
        [ObservableProperty] private bool _isOperating;
        [ObservableProperty] private string _operationLog = string.Empty;
        [ObservableProperty] private string _progressStatusText = string.Empty;

        // Branches
        public ObservableCollection<string> AvailableBranches { get; } = new();

        // Commits
        public ObservableCollection<CommitInfo> RecentCommits { get; } = new();

        public GitSourceNodeDialogViewModel(GitSourceNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _gitNode = node ?? throw new ArgumentNullException(nameof(node));

            // Sync from node
            RepoUrl = node.RepoUrl;
            LocalPath = node.LocalPath;
            Branch = node.Branch;
            DisplayName = node.DisplayName;
            IconKey = node.IconKey;
            TooltipText = node.TooltipText;
            ContextMenuDescription = node.ContextMenuDescription;
            VscodiumPath = node.VscodiumPath;
            AutoOpenOnExecute = node.AutoOpenOnExecute;
            LastCommitDisplay = string.IsNullOrWhiteSpace(node.LastCommitHash)
                ? "—" : node.LastCommitHash[..Math.Min(7, node.LastCommitHash.Length)];
            LastPullTimeDisplay = string.IsNullOrWhiteSpace(node.LastPullTime)
                ? "Chưa pull" : node.LastPullTime;

            // Check repo status on load
            RefreshRepoStatus();
        }

        protected override string GetDefaultTitle() => "Git Source";

        protected override void OnSaveTitle()
        {
            _gitNode.RepoUrl = RepoUrl;
            _gitNode.LocalPath = LocalPath;
            _gitNode.Branch = Branch;
            _gitNode.DisplayName = DisplayName;
            _gitNode.IconKey = IconKey;
            _gitNode.TooltipText = TooltipText;
            _gitNode.ContextMenuDescription = ContextMenuDescription;
            _gitNode.VscodiumPath = VscodiumPath;
            _gitNode.AutoOpenOnExecute = AutoOpenOnExecute;
            _gitNode.NotifyTitleChanged();
        }

        [RelayCommand]
        private async Task CloneRepoAsync()
        {
            if (string.IsNullOrWhiteSpace(RepoUrl))
            {
                AppendLog("❌ Vui lòng nhập URL repository.");
                return;
            }
            if (string.IsNullOrWhiteSpace(LocalPath))
            {
                AppendLog("❌ Vui lòng chọn thư mục đích.");
                return;
            }

            IsOperating = true;
            ProgressStatusText = "Đang kết nối tới remote...";
            AppendLog($"🔄 Đang clone {RepoUrl} → {LocalPath}...");

            try
            {
                var result = await Task.Run(() => _gitService.CloneRepository(
                    RepoUrl, LocalPath, Branch,
                    onProgress: msg => Application.Current?.Dispatcher.Invoke(() =>
                    {
                        ProgressStatusText = msg;
                    })));

                if (result.Success)
                {
                    _gitNode.LastCommitHash = result.LastCommitHash;
                    _gitNode.LastPullTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    LastCommitDisplay = result.LastCommitHash[..Math.Min(7, result.LastCommitHash.Length)];
                    LastPullTimeDisplay = _gitNode.LastPullTime;
                    Branch = result.Branch;

                    ProgressStatusText = "✅ Clone hoàn tất!";
                    AppendLog($"✅ Clone thành công! Branch: {result.Branch}, Commit: {result.LastCommitHash[..7]}");
                    RefreshRepoStatus();
                }
                else
                {
                    ProgressStatusText = $"❌ {result.ErrorMessage}";
                    AppendLog($"❌ Clone thất bại: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ProgressStatusText = $"❌ {ex.Message}";
                AppendLog($"❌ Lỗi: {ex.Message}");
            }
            finally
            {
                await Task.Delay(1500); // Giữ status hiển thị 1.5s
                IsOperating = false;
                ProgressStatusText = string.Empty;
            }
        }

        [RelayCommand]
        private async Task PullRepoAsync()
        {
            if (string.IsNullOrWhiteSpace(LocalPath))
            {
                AppendLog("❌ Chưa có local path.");
                return;
            }

            IsOperating = true;
            ProgressStatusText = "Đang fetch từ remote...";
            AppendLog($"🔄 Đang pull từ remote...");

            try
            {
                var result = await Task.Run(() => _gitService.PullRepository(
                    LocalPath,
                    onProgress: msg => Application.Current?.Dispatcher.Invoke(() =>
                    {
                        ProgressStatusText = msg;
                    })));

                if (result.Success)
                {
                    _gitNode.LastCommitHash = result.LastCommitHash;
                    _gitNode.LastPullTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    LastCommitDisplay = result.LastCommitHash[..Math.Min(7, result.LastCommitHash.Length)];
                    LastPullTimeDisplay = _gitNode.LastPullTime;

                    ProgressStatusText = $"✅ Pull xong — {result.Status}";
                    AppendLog($"✅ Pull thành công! Status: {result.Status}, Commit: {result.LastCommitHash[..7]}");
                    RefreshRepoStatus();
                }
                else
                {
                    ProgressStatusText = $"❌ {result.ErrorMessage}";
                    AppendLog($"❌ Pull thất bại: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ProgressStatusText = $"❌ {ex.Message}";
                AppendLog($"❌ Lỗi: {ex.Message}");
            }
            finally
            {
                await Task.Delay(1500);
                IsOperating = false;
                ProgressStatusText = string.Empty;
            }
        }

        [RelayCommand]
        private void OpenInVsCodium()
        {
            if (string.IsNullOrWhiteSpace(LocalPath) || !Directory.Exists(LocalPath))
            {
                AppendLog("❌ Thư mục source không tồn tại.");
                return;
            }

            try
            {
                var exePath = VscodiumPath;
                if (string.IsNullOrWhiteSpace(exePath)) exePath = "vscodium";

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"\"{LocalPath}\"",
                    UseShellExecute = true
                });
                AppendLog($"✅ Đã mở VSCodium: {LocalPath}");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Không thể mở VSCodium: {ex.Message}");
            }
        }

        [RelayCommand]
        private void BrowseLocalPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Chọn thư mục clone/pull"
            };

            if (dialog.ShowDialog() == true)
            {
                LocalPath = dialog.FolderName;
                RefreshRepoStatus();
            }
        }

        [RelayCommand]
        private void BrowseVscodiumPath()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn VSCodium executable",
                Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                VscodiumPath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void CheckoutSelectedBranch()
        {
            if (string.IsNullOrWhiteSpace(LocalPath) || string.IsNullOrWhiteSpace(Branch))
                return;

            var success = _gitService.CheckoutBranch(LocalPath, Branch);
            if (success)
            {
                AppendLog($"✅ Đã checkout branch: {Branch}");
                RefreshRepoStatus();
            }
            else
            {
                AppendLog($"❌ Không thể checkout branch: {Branch}");
            }
        }

        private void RefreshRepoStatus()
        {
            if (string.IsNullOrWhiteSpace(LocalPath) || !Directory.Exists(LocalPath))
            {
                IsRepoValid = false;
                StatusText = "Thư mục chưa tồn tại";
                AvailableBranches.Clear();
                RecentCommits.Clear();
                return;
            }

            var status = _gitService.GetStatus(LocalPath);
            IsRepoValid = status.IsValid;

            if (!status.IsValid)
            {
                StatusText = status.ErrorMessage ?? "Không phải Git repository";
                AvailableBranches.Clear();
                RecentCommits.Clear();
                return;
            }

            var dirty = status.IsDirty ? " (có thay đổi)" : " (sạch)";
            StatusText = $"Branch: {status.Branch}{dirty} | Modified: {status.ModifiedCount} | Untracked: {status.UntrackedCount}";
            LastCommitDisplay = $"{status.LastCommitHash[..Math.Min(7, status.LastCommitHash.Length)]} - {status.LastCommitMessage}";

            // Refresh branches
            AvailableBranches.Clear();
            var branches = _gitService.GetBranches(LocalPath);
            foreach (var b in branches)
                AvailableBranches.Add(b);

            // Refresh commits
            RecentCommits.Clear();
            var commits = _gitService.GetRecentCommits(LocalPath, 15);
            foreach (var c in commits)
                RecentCommits.Add(c);
        }

        private void AppendLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            OperationLog = $"[{timestamp}] {message}\n{OperationLog}";
        }
    }
}
