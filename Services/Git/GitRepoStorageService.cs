using FlowMy.Models;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.Services.Git
{
    /// <summary>
    /// Lưu/đọc danh sách Git repos đã cấu hình vào file JSON.
    /// File: {AppRoot}/git_repos.json
    /// </summary>
    public static class GitRepoStorageService
    {
        private static string GetFilePath()
        {
            var appRoot = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appRoot, "git_repos.json");
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
                var path = GetFilePath();
                if (!File.Exists(path)) return new List<GitSourceNode>();

                var json = File.ReadAllText(path);
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

                    // Ensure IconColorKey is set so IconBrushResolved works
                    // (already set above, just trigger property for binding)
                    node.IconColorKey = dto.IconColorKey ?? "White";

                    node.Type = NodeType.GitSource;
                    return node;
                }).ToList();
            }
            catch
            {
                return new List<GitSourceNode>();
            }
        }

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
