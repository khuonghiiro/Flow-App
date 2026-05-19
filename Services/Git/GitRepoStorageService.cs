using FlowMy.Models;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.Services.Git
{
    /// <summary>
    /// Lưu/đọc danh sách Git repos đã cấu hình vào file JSON.
    /// File: Documents\FlowMy\FlowMy-CmdGit\git_repos.json
    /// </summary>
    public static class GitRepoStorageService
    {
        /// <summary>Thư mục lưu trữ: Documents\FlowMy\FlowMy-CmdGit</summary>
        private static string GetStorageFolder()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "FlowMy", "FlowMy-CmdGit");
        }

        private static string GetFilePath()
        {
            var folder = GetStorageFolder();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, "git_repos.json");
        }

        /// <summary>
        /// Migrate dữ liệu cũ sang vị trí mới (chạy 1 lần).
        /// Thứ tự ưu tiên: AppRoot → Documents\FlowMy-CmdGit (cũ) → Documents\FlowMy\FlowMy-CmdGit (mới).
        /// Gọi khi app khởi động hoặc trước khi Load().
        /// </summary>
        public static void MigrateFromLegacyIfNeeded()
        {
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var newFile = GetFilePath();
                var newFolder = GetStorageFolder();

                // 1. Migrate từ AppRoot (legacy v1)
                var legacyAppRootFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "git_repos.json");
                if (File.Exists(legacyAppRootFile) && !File.Exists(newFile))
                {
                    if (!Directory.Exists(newFolder)) Directory.CreateDirectory(newFolder);
                    File.Copy(legacyAppRootFile, newFile, overwrite: false);
                }

                // 2. Migrate từ Documents\FlowMy-CmdGit (legacy v2) → Documents\FlowMy\FlowMy-CmdGit (v3)
                var legacyDocsFolder = Path.Combine(docs, "FlowMy-CmdGit");
                var legacyDocsFile = Path.Combine(legacyDocsFolder, "git_repos.json");
                if (File.Exists(legacyDocsFile) && !File.Exists(newFile))
                {
                    if (!Directory.Exists(newFolder)) Directory.CreateDirectory(newFolder);
                    File.Copy(legacyDocsFile, newFile, overwrite: false);
                }

                // 3. Migrate cmd_git folder từ AppRoot
                var legacyCmdFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cmd_git");
                var newCmdFolder = Path.Combine(newFolder, "cmd_git");
                if (Directory.Exists(legacyCmdFolder) && !Directory.Exists(newCmdFolder))
                {
                    Directory.CreateDirectory(newCmdFolder);
                    foreach (var file in Directory.GetFiles(legacyCmdFolder, "*.cmd"))
                    {
                        var destFile = Path.Combine(newCmdFolder, Path.GetFileName(file));
                        if (!File.Exists(destFile))
                            File.Copy(file, destFile);
                    }
                }

                // 4. Migrate cmd_git folder từ Documents\FlowMy-CmdGit (legacy v2)
                var legacyDocsCmdFolder = Path.Combine(legacyDocsFolder, "cmd_git");
                if (Directory.Exists(legacyDocsCmdFolder) && !Directory.Exists(newCmdFolder))
                {
                    Directory.CreateDirectory(newCmdFolder);
                    foreach (var file in Directory.GetFiles(legacyDocsCmdFolder, "*.cmd"))
                    {
                        var destFile = Path.Combine(newCmdFolder, Path.GetFileName(file));
                        if (!File.Exists(destFile))
                            File.Copy(file, destFile);
                    }
                }
            }
            catch { /* Silent — không block app nếu migrate fail */ }
        }

        /// <summary>Lưu danh sách repos ra file.</summary>
        public static void Save(IEnumerable<GitSourceNode> repos)
        {
            try
            {
                var dtos = repos.Select(r => new GitRepoDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    RepoUrl = r.RepoUrl,
                    LocalPath = r.LocalPath,
                    Branch = r.Branch,
                    DisplayName = r.DisplayName,
                    IconKey = r.IconKey,
                    IconColorKey = r.IconColorKey,
                    ColorKey = r.ColorKey,
                    TooltipText = r.TooltipText,
                    ContextMenuDescription = r.ContextMenuDescription,
                    VscodiumPath = r.VscodiumPath,
                    CommandText = r.CommandText,
                    LastCommitHash = r.LastCommitHash,
                    LastPullTime = r.LastPullTime,
                    AutoOpenOnExecute = r.AutoOpenOnExecute
                }).ToList();

                var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GetFilePath(), json);
            }
            catch { /* Silent fail — không block app */ }
        }

        /// <summary>Đọc danh sách repos từ file.</summary>
        public static List<GitSourceNode> Load()
        {
            try
            {
                // Auto-migrate từ vị trí cũ nếu cần
                MigrateFromLegacyIfNeeded();

                var path = GetFilePath();
                if (!File.Exists(path)) return new List<GitSourceNode>();

                var json = File.ReadAllText(path);
                return DeserializeRepos(json);
            }
            catch
            {
                return new List<GitSourceNode>();
            }
        }

        /// <summary>
        /// Import danh sách repos từ file JSON bên ngoài (merge vào danh sách hiện tại).
        /// Trả về số lượng repos đã import thành công.
        /// </summary>
        public static int ImportFromJson(string jsonFilePath)
        {
            try
            {
                if (!File.Exists(jsonFilePath)) return 0;

                var json = File.ReadAllText(jsonFilePath);
                var importedRepos = DeserializeRepos(json);
                if (importedRepos.Count == 0) return 0;

                // Load danh sách hiện tại
                var existingRepos = Load();
                var existingIds = new HashSet<string>(existingRepos.Select(r => r.Id));

                int count = 0;
                foreach (var repo in importedRepos)
                {
                    if (!existingIds.Contains(repo.Id))
                    {
                        existingRepos.Add(repo);
                        existingIds.Add(repo.Id);
                        count++;
                    }
                }

                if (count > 0)
                    Save(existingRepos);

                return count;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Export danh sách repos hiện tại ra file JSON tại đường dẫn chỉ định.
        /// </summary>
        public static bool ExportToJson(string outputFilePath)
        {
            try
            {
                var repos = Load();
                var dtos = repos.Select(r => new GitRepoDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    RepoUrl = r.RepoUrl,
                    LocalPath = r.LocalPath,
                    Branch = r.Branch,
                    DisplayName = r.DisplayName,
                    IconKey = r.IconKey,
                    IconColorKey = r.IconColorKey,
                    ColorKey = r.ColorKey,
                    TooltipText = r.TooltipText,
                    ContextMenuDescription = r.ContextMenuDescription,
                    VscodiumPath = r.VscodiumPath,
                    CommandText = r.CommandText,
                    LastCommitHash = r.LastCommitHash,
                    LastPullTime = r.LastPullTime,
                    AutoOpenOnExecute = r.AutoOpenOnExecute
                }).ToList();

                var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(outputFilePath, json);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Trả về đường dẫn thư mục lưu trữ (để hiển thị cho user).</summary>
        public static string GetStoragePath() => GetStorageFolder();

        private sealed class GitRepoDto
        {
            public string? Id { get; set; }
            public string? Title { get; set; }
            public string? RepoUrl { get; set; }
            public string? LocalPath { get; set; }
            public string? Branch { get; set; }
            public string? DisplayName { get; set; }
            public string? IconKey { get; set; }
            public string? IconColorKey { get; set; }
            public string? ColorKey { get; set; }
            public string? TooltipText { get; set; }
            public string? ContextMenuDescription { get; set; }
            public string? VscodiumPath { get; set; }
            public string? CommandText { get; set; }
            public string? LastCommitHash { get; set; }
            public string? LastPullTime { get; set; }
            public bool AutoOpenOnExecute { get; set; } = true;
        }

        /// <summary>Deserialize JSON string thành danh sách GitSourceNode.</summary>
        private static List<GitSourceNode> DeserializeRepos(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<GitRepoDto>>(json);
            if (dtos == null) return new List<GitSourceNode>();

            return dtos.Select(dto =>
            {
                var node = new GitSourceNode
                {
                    Id = dto.Id ?? Guid.NewGuid().ToString(),
                    Title = dto.Title ?? dto.DisplayName ?? "Git Source",
                    RepoUrl = dto.RepoUrl ?? string.Empty,
                    LocalPath = dto.LocalPath ?? string.Empty,
                    Branch = dto.Branch ?? "main",
                    DisplayName = dto.DisplayName ?? dto.Title ?? "Git Source",
                    IconKey = dto.IconKey ?? "git-alt brands",
                    IconColorKey = dto.IconColorKey ?? "White",
                    ColorKey = dto.ColorKey ?? "Indigo",
                    TooltipText = dto.TooltipText ?? string.Empty,
                    ContextMenuDescription = dto.ContextMenuDescription ?? string.Empty,
                    VscodiumPath = dto.VscodiumPath ?? "vscodium",
                    CommandText = dto.CommandText ?? string.Empty,
                    LastCommitHash = dto.LastCommitHash ?? string.Empty,
                    LastPullTime = dto.LastPullTime ?? string.Empty,
                    AutoOpenOnExecute = dto.AutoOpenOnExecute
                };

                // Resolve NodeBrush from ColorKey
                node.NodeBrush = ResolveBrushFromKey(node.ColorKey);
                node.IconColorKey = dto.IconColorKey ?? "White";
                node.Type = NodeType.GitSource;
                return node;
            }).ToList();
        }

        /// <summary>Resolve ColorKey thành Brush — hỗ trợ hex (#RRGGBB) và named resource key.</summary>
        private static Brush ResolveBrushFromKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Brushes.Transparent;

            // Hex color
            if (key.StartsWith("#"))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(key);
                    return new SolidColorBrush(color);
                }
                catch { return Brushes.Transparent; }
            }

            // Named system colors
            if (key.Equals("White", System.StringComparison.OrdinalIgnoreCase))
                return Brushes.White;
            if (key.Equals("Black", System.StringComparison.OrdinalIgnoreCase))
                return Brushes.Black;

            // Resource lookup: try "{key}Brush" first, then "{key}"
            var brush = Application.Current?.TryFindResource($"{key}Brush") as Brush
                     ?? Application.Current?.TryFindResource(key) as Brush;
            return brush ?? Brushes.Transparent;
        }
    }
}
