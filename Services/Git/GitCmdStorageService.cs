using System.IO;
using System.Text.Json;

namespace FlowMy.Services.Git
{
    /// <summary>
    /// Lưu/đọc command text + cấu hình Git repos vào folder Documents của user.
    /// Path: {Documents}/FlowMy-CmdGit/git_config.json
    /// Mục đích: tách riêng khỏi git repo (không bị pull/push ảnh hưởng),
    /// dễ backup/import để khôi phục danh sách + cấu hình.
    /// </summary>
    public static class GitCmdStorageService
    {
        private static string GetFolder()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "FlowMy-CmdGit");
        }

        private static string GetFilePath()
        {
            return Path.Combine(GetFolder(), "git_config.json");
        }

        /// <summary>Lưu command text cho 1 repo (merge vào file chung).</summary>
        public static void Save(string repoId, string commandText)
        {
            try
            {
                var all = LoadAll();
                var existing = all.FirstOrDefault(x => x.RepoId == repoId);
                if (existing != null)
                    existing.CommandText = commandText ?? string.Empty;
                else
                    all.Add(new GitCmdEntry { RepoId = repoId, CommandText = commandText ?? string.Empty });

                WriteAll(all);
            }
            catch { /* Silent fail */ }
        }

        /// <summary>Lưu toàn bộ cấu hình (command + thông tin git) cho 1 repo.</summary>
        public static void SaveFull(GitCmdEntry entry)
        {
            try
            {
                var all = LoadAll();
                var idx = all.FindIndex(x => x.RepoId == entry.RepoId);
                if (idx >= 0)
                    all[idx] = entry;
                else
                    all.Add(entry);

                WriteAll(all);
            }
            catch { /* Silent fail */ }
        }

        /// <summary>Đọc command text cho 1 repo. Trả về empty nếu chưa có.</summary>
        public static string Load(string repoId)
        {
            try
            {
                var all = LoadAll();
                return all.FirstOrDefault(x => x.RepoId == repoId)?.CommandText ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>Đọc toàn bộ entry cho 1 repo.</summary>
        public static GitCmdEntry? LoadEntry(string repoId)
        {
            try
            {
                var all = LoadAll();
                return all.FirstOrDefault(x => x.RepoId == repoId);
            }
            catch { return null; }
        }

        /// <summary>Đọc toàn bộ danh sách entries (dùng cho import/export).</summary>
        public static List<GitCmdEntry> LoadAll()
        {
            try
            {
                var filePath = GetFilePath();
                if (!File.Exists(filePath)) return new List<GitCmdEntry>();

                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<GitCmdEntry>>(json) ?? new List<GitCmdEntry>();
            }
            catch { return new List<GitCmdEntry>(); }
        }

        /// <summary>Xóa entry khi xóa repo.</summary>
        public static void Delete(string repoId)
        {
            try
            {
                var all = LoadAll();
                all.RemoveAll(x => x.RepoId == repoId);
                WriteAll(all);
            }
            catch { /* Silent fail */ }
        }

        /// <summary>Import từ file JSON bên ngoài (user chọn file) → merge vào danh sách hiện tại.</summary>
        public static List<GitCmdEntry> ImportFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return new List<GitCmdEntry>();
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<GitCmdEntry>>(json) ?? new List<GitCmdEntry>();
            }
            catch { return new List<GitCmdEntry>(); }
        }

        /// <summary>Export toàn bộ config ra file (user chọn nơi lưu).</summary>
        public static void ExportToFile(string filePath)
        {
            try
            {
                var all = LoadAll();
                var json = JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch { /* Silent fail */ }
        }

        private static void WriteAll(List<GitCmdEntry> entries)
        {
            var folder = GetFolder();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetFilePath(), json);
        }
    }

    /// <summary>
    /// Entry lưu cấu hình 1 Git repo (command + thông tin hiển thị).
    /// Dùng cho import/export khôi phục danh sách.
    /// </summary>
    public sealed class GitCmdEntry
    {
        public string RepoId { get; set; } = string.Empty;
        public string CommandText { get; set; } = string.Empty;
        public bool ShowCmdWindow { get; set; } = true;
        public bool RunAsBatch { get; set; } = false;

        // Thông tin git repo (để import khôi phục)
        public string RepoUrl { get; set; } = string.Empty;
        public string LocalPath { get; set; } = string.Empty;
        public string Branch { get; set; } = "main";
        public string DisplayName { get; set; } = string.Empty;
        public string IconKey { get; set; } = "git-alt brands";
        public string IconColorKey { get; set; } = "White";
        public string ColorKey { get; set; } = "Indigo";
        public string TooltipText { get; set; } = string.Empty;
    }
}
