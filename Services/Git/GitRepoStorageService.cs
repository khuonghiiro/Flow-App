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
                    AutoOpenOnExecute = r.AutoOpenOnExecute,
                    IsPartialClone = r.IsPartialClone,
                    SparsePaths = r.SparsePaths
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
                var path = GetFilePath();
                if (!File.Exists(path))
                {
                    // Tạo default repos nếu file chưa tồn tại
                    var defaultRepos = CreateDefaultRepos();
                    Save(defaultRepos);
                    return defaultRepos;
                }

                var json = File.ReadAllText(path);
                var repos = DeserializeRepos(json);

                // Nếu file rỗng hoặc không có repos, tạo default
                if (repos.Count == 0)
                {
                    var defaultRepos = CreateDefaultRepos();
                    Save(defaultRepos);
                    return defaultRepos;
                }

                return repos;
            }
            catch
            {
                return new List<GitSourceNode>();
            }
        }

        /// <summary>Tạo danh sách default repos mẫu.</summary>
        private static List<GitSourceNode> CreateDefaultRepos()
        {
            return new List<GitSourceNode>
            {
                new GitSourceNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "React App Template",
                    DisplayName = "React App Template",
                    RepoUrl = "https://github.com/facebook/react.git",
                    LocalPath = string.Empty,
                    Branch = "main",
                    IconKey = "react brands",
                    IconColorKey = "White",
                    ColorKey = "OceanBrush",
                    TooltipText = "React official repository - UI library for building user interfaces",
                    ContextMenuDescription = "Official React repository with examples and documentation",
                    CommandText = "npm install\nnpm run dev",
                    IsPartialClone = false,
                    SparsePaths = string.Empty
                },
                new GitSourceNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Vue.js",
                    DisplayName = "Vue.js",
                    RepoUrl = "https://github.com/vuejs/core.git",
                    LocalPath = string.Empty,
                    Branch = "main",
                    IconKey = "vuejs brands",
                    IconColorKey = "White",
                    ColorKey = "ForestPine",
                    TooltipText = "Vue.js core - Progressive JavaScript framework",
                    ContextMenuDescription = "Vue.js official repository",
                    CommandText = "npm install\nnpm run dev",
                    IsPartialClone = false,
                    SparsePaths = string.Empty
                },
                new GitSourceNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = ".NET Core",
                    DisplayName = ".NET Core",
                    RepoUrl = "https://github.com/dotnet/runtime.git",
                    LocalPath = string.Empty,
                    Branch = "main",
                    IconKey = "dot-net brands",
                    IconColorKey = "White",
                    ColorKey = "InfoBrush",
                    TooltipText = ".NET runtime - Cross-platform implementation of .NET",
                    ContextMenuDescription = "Official .NET runtime repository",
                    CommandText = "dotnet build\ndotnet run",
                    IsPartialClone = false,
                    SparsePaths = string.Empty
                }
            };
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
                    AutoOpenOnExecute = r.AutoOpenOnExecute,
                    IsPartialClone = r.IsPartialClone,
                    SparsePaths = r.SparsePaths
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
            public bool IsPartialClone { get; set; }
            public string? SparsePaths { get; set; }
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
                    AutoOpenOnExecute = dto.AutoOpenOnExecute,
                    IsPartialClone = dto.IsPartialClone,
                    SparsePaths = dto.SparsePaths ?? string.Empty
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
