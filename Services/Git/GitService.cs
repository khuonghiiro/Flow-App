using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System.IO;

namespace FlowMy.Services.Git
{
    /// <summary>
    /// Service xử lý các thao tác Git: clone, pull, status, log.
    /// Dùng LibGit2Sharp để thao tác trực tiếp không cần git CLI.
    /// </summary>
    public sealed class GitService
    {
        /// <summary>
        /// Clone repo từ remote URL về local path.
        /// </summary>
        public CloneResult CloneRepository(string repoUrl, string localPath, string? branch = null,
            CredentialsHandler? credentialsProvider = null, Action<string>? onProgress = null)
        {
            try
            {
                // Tạo folder nếu chưa tồn tại
                if (!Directory.Exists(localPath))
                {
                    Directory.CreateDirectory(localPath);
                }
                else if (Directory.GetFileSystemEntries(localPath).Length > 0)
                {
                    return new CloneResult { Success = false, ErrorMessage = $"Thư mục '{localPath}' đã tồn tại và không rỗng." };
                }

                var options = new CloneOptions
                {
                    BranchName = branch ?? "main",
                    RecurseSubmodules = false
                };

                if (credentialsProvider != null)
                    options.FetchOptions.CredentialsProvider = credentialsProvider;

                // Progress callbacks
                options.FetchOptions.OnTransferProgress = progress =>
                {
                    var pct = progress.TotalObjects > 0
                        ? (int)((double)progress.ReceivedObjects / progress.TotalObjects * 100)
                        : 0;
                    onProgress?.Invoke($"Đang tải objects: {progress.ReceivedObjects}/{progress.TotalObjects} ({pct}%) — {progress.ReceivedBytes / 1024} KB");
                    return true;
                };

                options.OnCheckoutProgress = (path, completedSteps, totalSteps) =>
                {
                    if (totalSteps > 0)
                    {
                        var pct = (int)((double)completedSteps / totalSteps * 100);
                        onProgress?.Invoke($"Checkout files: {completedSteps}/{totalSteps} ({pct}%)");
                    }
                };

                onProgress?.Invoke("Đang kết nối tới remote...");

                var resultPath = Repository.Clone(repoUrl, localPath, options);

                onProgress?.Invoke("Đang đọc thông tin repository...");

                using var repo = new Repository(resultPath);
                var head = repo.Head.Tip;

                return new CloneResult
                {
                    Success = true,
                    LocalPath = resultPath,
                    Branch = repo.Head.FriendlyName,
                    LastCommitHash = head?.Sha ?? string.Empty,
                    LastCommitMessage = head?.MessageShort ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                return new CloneResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Pull latest changes từ remote.
        /// </summary>
        public PullResult PullRepository(string localPath, string? authorName = null, string? authorEmail = null,
            CredentialsHandler? credentialsProvider = null, Action<string>? onProgress = null)
        {
            try
            {
                if (!Repository.IsValid(localPath))
                    return new PullResult { Success = false, ErrorMessage = $"'{localPath}' không phải Git repository." };

                using var repo = new Repository(localPath);

                onProgress?.Invoke($"Đang fetch từ remote ({repo.Head.FriendlyName})...");

                var options = new PullOptions
                {
                    FetchOptions = new FetchOptions()
                };

                if (credentialsProvider != null)
                    options.FetchOptions.CredentialsProvider = credentialsProvider;

                options.FetchOptions.OnTransferProgress = progress =>
                {
                    var pct = progress.TotalObjects > 0
                        ? (int)((double)progress.ReceivedObjects / progress.TotalObjects * 100)
                        : 0;
                    onProgress?.Invoke($"Fetching: {progress.ReceivedObjects}/{progress.TotalObjects} ({pct}%) — {progress.ReceivedBytes / 1024} KB");
                    return true;
                };

                var signature = new Signature(
                    authorName ?? "FlowMy",
                    authorEmail ?? "flowmy@local",
                    DateTimeOffset.Now);

                onProgress?.Invoke("Đang merge changes...");

                var result = Commands.Pull(repo, signature, options);

                var head = repo.Head.Tip;
                return new PullResult
                {
                    Success = true,
                    Status = result.Status.ToString(),
                    Branch = repo.Head.FriendlyName,
                    LastCommitHash = head?.Sha ?? string.Empty,
                    LastCommitMessage = head?.MessageShort ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                return new PullResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Lấy thông tin trạng thái repo.
        /// </summary>
        public RepoStatusInfo GetStatus(string localPath)
        {
            try
            {
                if (!Repository.IsValid(localPath))
                    return new RepoStatusInfo { IsValid = false, ErrorMessage = "Không phải Git repository." };

                using var repo = new Repository(localPath);
                var status = repo.RetrieveStatus();
                var head = repo.Head.Tip;

                return new RepoStatusInfo
                {
                    IsValid = true,
                    Branch = repo.Head.FriendlyName,
                    LastCommitHash = head?.Sha ?? string.Empty,
                    LastCommitMessage = head?.MessageShort ?? string.Empty,
                    LastCommitTime = head?.Author.When.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    ModifiedCount = status.Modified.Count(),
                    AddedCount = status.Added.Count(),
                    RemovedCount = status.Removed.Count(),
                    UntrackedCount = status.Untracked.Count(),
                    IsDirty = status.IsDirty
                };
            }
            catch (Exception ex)
            {
                return new RepoStatusInfo { IsValid = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Lấy danh sách branches.
        /// </summary>
        public List<string> GetBranches(string localPath)
        {
            try
            {
                if (!Repository.IsValid(localPath))
                    return new List<string>();

                using var repo = new Repository(localPath);
                return repo.Branches
                    .Where(b => !b.IsRemote)
                    .Select(b => b.FriendlyName)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Checkout branch.
        /// </summary>
        public bool CheckoutBranch(string localPath, string branchName)
        {
            try
            {
                if (!Repository.IsValid(localPath))
                    return false;

                using var repo = new Repository(localPath);
                var branch = repo.Branches[branchName];
                if (branch == null) return false;

                Commands.Checkout(repo, branch);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lấy log commits gần nhất.
        /// </summary>
        public List<CommitInfo> GetRecentCommits(string localPath, int count = 20)
        {
            try
            {
                if (!Repository.IsValid(localPath))
                    return new List<CommitInfo>();

                using var repo = new Repository(localPath);
                return repo.Commits
                    .Take(count)
                    .Select(c => new CommitInfo
                    {
                        Hash = c.Sha[..7],
                        FullHash = c.Sha,
                        Message = c.MessageShort,
                        Author = c.Author.Name,
                        Date = c.Author.When.ToString("yyyy-MM-dd HH:mm")
                    })
                    .ToList();
            }
            catch
            {
                return new List<CommitInfo>();
            }
        }

        /// <summary>
        /// Lấy URL remote (origin) của repo local. Trả về null nếu không có.
        /// </summary>
        public string? GetRemoteUrl(string localPath, string remoteName = "origin")
        {
            try
            {
                if (!Repository.IsValid(localPath)) return null;
                using var repo = new Repository(localPath);
                var remote = repo.Network.Remotes[remoteName] ?? repo.Network.Remotes.FirstOrDefault();
                return remote?.Url;
            }
            catch { return null; }
        }

        /// <summary>
        /// Kiểm tra xem đường dẫn có phải Git repo không.
        /// </summary>
        public bool IsGitRepository(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && Repository.IsValid(path);
        }

        /// <summary>
        /// Clone repo với sparse-checkout (chỉ pull các file/folder được chọn).
        /// </summary>
        public CloneResult CloneRepositorySparse(string repoUrl, string localPath, string? branch = null,
            List<string>? sparsePaths = null, CredentialsHandler? credentialsProvider = null, Action<string>? onProgress = null)
        {
            try
            {
                // Tạo folder nếu chưa tồn tại
                if (!Directory.Exists(localPath))
                {
                    Directory.CreateDirectory(localPath);
                }
                else if (Directory.GetFileSystemEntries(localPath).Length > 0)
                {
                    return new CloneResult { Success = false, ErrorMessage = $"Thư mục '{localPath}' đã tồn tại và không rỗng." };
                }

                onProgress?.Invoke("Đang khởi tạo sparse checkout...");

                var options = new CloneOptions
                {
                    BranchName = branch ?? "main",
                    RecurseSubmodules = false
                };

                if (credentialsProvider != null)
                    options.FetchOptions.CredentialsProvider = credentialsProvider;

                // Progress callbacks
                options.FetchOptions.OnTransferProgress = progress =>
                {
                    var pct = progress.TotalObjects > 0
                        ? (int)((double)progress.ReceivedObjects / progress.TotalObjects * 100)
                        : 0;
                    onProgress?.Invoke($"Đang tải objects: {progress.ReceivedObjects}/{progress.TotalObjects} ({pct}%) — {progress.ReceivedBytes / 1024} KB");
                    return true;
                };

                onProgress?.Invoke("Đang kết nối tới remote...");

                // Clone với no-checkout trước
                options.Checkout = false;
                var resultPath = Repository.Clone(repoUrl, localPath, options);

                onProgress?.Invoke("Đang cấu hình sparse checkout...");

                using var repo = new Repository(resultPath);

                // Enable sparse checkout
                repo.Config.Set("core.sparseCheckout", true);

                // Thêm các paths vào sparse-checkout file
                if (sparsePaths != null && sparsePaths.Count > 0)
                {
                    var sparseCheckoutFile = Path.Combine(localPath, ".git", "info", "sparse-checkout");
                    var sparseDir = Path.GetDirectoryName(sparseCheckoutFile);
                    if (!Directory.Exists(sparseDir))
                        Directory.CreateDirectory(sparseDir);

                    File.WriteAllLines(sparseCheckoutFile, sparsePaths);
                }

                onProgress?.Invoke("Đang reset index để áp dụng sparse checkout...");

                // Reset index để áp dụng sparse checkout
                var resetOptions = new ResetOptions
                {
                    ResetMode = ResetMode.Hard
                };
                Commands.Reset(repo, repo.Head, resetOptions);

                onProgress?.Invoke("Đang checkout files...");

                // Checkout files
                var checkoutOptions = new CheckoutOptions
                {
                    OnCheckoutProgress = (path, completedSteps, totalSteps) =>
                    {
                        if (totalSteps > 0)
                        {
                            var pct = (int)((double)completedSteps / totalSteps * 100);
                            onProgress?.Invoke($"Checkout files: {completedSteps}/{totalSteps} ({pct}%)");
                        }
                    }
                };

                Commands.Checkout(repo, repo.Head, checkoutOptions);

                onProgress?.Invoke("Đang đọc thông tin repository...");

                var head = repo.Head.Tip;

                return new CloneResult
                {
                    Success = true,
                    LocalPath = resultPath,
                    Branch = repo.Head.FriendlyName,
                    LastCommitHash = head?.Sha ?? string.Empty,
                    LastCommitMessage = head?.MessageShort ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                return new CloneResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Cập nhật sparse-checkout paths cho repo đã tồn tại.
        /// </summary>
        public bool UpdateSparseCheckoutPaths(string localPath, List<string> sparsePaths)
        {
            try
            {
                if (!Repository.IsValid(localPath))
                    return false;

                using var repo = new Repository(localPath);

                // Enable sparse checkout
                repo.Config.Set("core.sparseCheckout", true);

                // Cập nhật sparse-checkout file
                var sparseCheckoutFile = Path.Combine(localPath, ".git", "info", "sparse-checkout");
                var sparseDir = Path.GetDirectoryName(sparseCheckoutFile);
                if (!Directory.Exists(sparseDir))
                    Directory.CreateDirectory(sparseDir);

                File.WriteAllLines(sparseCheckoutFile, sparsePaths);

                // Reset index để áp dụng sparse checkout
                var resetOptions = new ResetOptions
                {
                    ResetMode = ResetMode.Hard
                };
                Commands.Reset(repo, repo.Head, resetOptions);

                // Checkout để áp dụng sparse checkout paths
                Commands.Checkout(repo, repo.Head);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lấy danh sách sparse-checkout paths hiện tại.
        /// </summary>
        public List<string> GetSparseCheckoutPaths(string localPath)
        {
            try
            {
                var sparseCheckoutFile = Path.Combine(localPath, ".git", "info", "sparse-checkout");
                if (!File.Exists(sparseCheckoutFile))
                    return new List<string>();

                return File.ReadAllLines(sparseCheckoutFile)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Kiểm tra repo có đang dùng sparse-checkout không.
        /// </summary>
        public bool IsSparseCheckoutEnabled(string localPath)
        {
            try
            {
                if (!Repository.IsValid(localPath))
                    return false;

                using var repo = new Repository(localPath);
                var sparseConfig = repo.Config.Get<bool>("core.sparseCheckout");
                return sparseConfig != null && sparseConfig.Value;
            }
            catch
            {
                return false;
            }
        }
    }

    public sealed class CloneResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string LocalPath { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string LastCommitHash { get; set; } = string.Empty;
        public string LastCommitMessage { get; set; } = string.Empty;
    }

    public sealed class PullResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string LastCommitHash { get; set; } = string.Empty;
        public string LastCommitMessage { get; set; } = string.Empty;
    }

    public sealed class RepoStatusInfo
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string Branch { get; set; } = string.Empty;
        public string LastCommitHash { get; set; } = string.Empty;
        public string LastCommitMessage { get; set; } = string.Empty;
        public string LastCommitTime { get; set; } = string.Empty;
        public int ModifiedCount { get; set; }
        public int AddedCount { get; set; }
        public int RemovedCount { get; set; }
        public int UntrackedCount { get; set; }
        public bool IsDirty { get; set; }
    }

    public sealed class CommitInfo
    {
        public string Hash { get; set; } = string.Empty;
        public string FullHash { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;

        public override string ToString() => $"{Hash} - {Message} ({Author}, {Date})";
    }
}
