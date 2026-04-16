using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public sealed class RootFolderPresetOption
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class FolderNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly FolderNode _folderNode;

        [ObservableProperty]
        private string _rootFolderPath;

        [ObservableProperty]
        private string _subPathTemplate;

        [ObservableProperty]
        private string _selectedRootFolderPresetKey = string.Empty;

        [ObservableProperty]
        private bool _isCustomRootFolderPath = true;

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<RootFolderPresetOption> RootFolderPresetOptions { get; } = new();

        public FolderNodeDialogViewModel(FolderNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _folderNode = node ?? throw new ArgumentNullException(nameof(node));
            _rootFolderPath = node.RootFolderPath ?? string.Empty;
            _subPathTemplate = node.SubPathTemplate ?? string.Empty;

            BuildRootFolderPresetOptions();
            var presetKey = string.IsNullOrWhiteSpace(node.RootFolderPresetKey)
                ? GuessPresetKeyFromPath(_rootFolderPath)
                : node.RootFolderPresetKey;
            SelectedRootFolderPresetKey = presetKey;
            IsCustomRootFolderPath = string.IsNullOrWhiteSpace(SelectedRootFolderPresetKey);
            if (!IsCustomRootFolderPath)
            {
                RootFolderPath = ResolveRootFolderFromPreset(SelectedRootFolderPresetKey);
            }

            RefreshAvailableNodes();
        }

        protected override string GetDefaultTitle() => "Folder";

        protected override void OnSaveTitle()
        {
            _folderNode.NotifyTitleChanged();
            _folderNode.RootFolderPath = RootFolderPath ?? string.Empty;
            _folderNode.RootFolderPresetKey = SelectedRootFolderPresetKey ?? string.Empty;
            _folderNode.SubPathTemplate = SubPathTemplate ?? string.Empty;
        }

        partial void OnSelectedRootFolderPresetKeyChanged(string value)
        {
            var key = value ?? string.Empty;
            IsCustomRootFolderPath = string.IsNullOrWhiteSpace(key);
            if (IsCustomRootFolderPath)
            {
                _folderNode.RootFolderPresetKey = string.Empty;
                return;
            }

            var resolved = ResolveRootFolderFromPreset(key);
            RootFolderPath = resolved;
            _folderNode.RootFolderPresetKey = key;
        }

        partial void OnRootFolderPathChanged(string value)
        {
            if (IsCustomRootFolderPath)
            {
                _folderNode.RootFolderPresetKey = string.Empty;
            }
        }

        public void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;

            foreach (var n in _host.ViewModel.Nodes)
            {
                if (ReferenceEquals(n, _folderNode)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;

                AvailableNodeOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
            }
        }

        public ObservableCollection<WorkflowOutputKeyOption> GetOutputKeysForNode(string? nodeId)
        {
            var list = new ObservableCollection<WorkflowOutputKeyOption>();
            if (string.IsNullOrWhiteSpace(nodeId) || _host.ViewModel?.Nodes == null) return list;

            var node = _host.ViewModel.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node?.DynamicOutputs == null) return list;

            foreach (var o in node.DynamicOutputs)
            {
                list.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key ?? string.Empty,
                    Type = o.OutputType ?? o.ConvertType,
                    DisplayName = o.DisplayName ?? o.Key
                });
            }
            return list;
        }

        private void BuildRootFolderPresetOptions()
        {
            RootFolderPresetOptions.Clear();
            RootFolderPresetOptions.Add(new RootFolderPresetOption { Key = string.Empty, DisplayName = "(Tùy chỉnh đường dẫn)" });
            RootFolderPresetOptions.Add(new RootFolderPresetOption { Key = "desktop", DisplayName = "Desktop" });
            RootFolderPresetOptions.Add(new RootFolderPresetOption { Key = "downloads", DisplayName = "Downloads" });
            RootFolderPresetOptions.Add(new RootFolderPresetOption { Key = "documents", DisplayName = "Documents" });
            RootFolderPresetOptions.Add(new RootFolderPresetOption { Key = "pictures", DisplayName = "Pictures" });
            RootFolderPresetOptions.Add(new RootFolderPresetOption { Key = "videos", DisplayName = "Videos" });
            RootFolderPresetOptions.Add(new RootFolderPresetOption { Key = "music", DisplayName = "Music" });
        }

        private static string ResolveRootFolderFromPreset(string? key)
        {
            var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "videos" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                "downloads" => ResolveDownloadsFolder(),
                _ => string.Empty
            };
        }

        private static string ResolveDownloadsFolder()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile))
            {
                var candidate = System.IO.Path.Combine(profile, "Downloads");
                return candidate;
            }
            return string.Empty;
        }

        private static string GuessPresetKeyFromPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            var input = path.Trim();
            bool MatchSpecial(Environment.SpecialFolder folder)
            {
                var fp = Environment.GetFolderPath(folder);
                return !string.IsNullOrWhiteSpace(fp) &&
                    string.Equals(fp.TrimEnd('\\', '/'), input.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            }

            if (MatchSpecial(Environment.SpecialFolder.DesktopDirectory)) return "desktop";
            if (MatchSpecial(Environment.SpecialFolder.MyDocuments)) return "documents";
            if (MatchSpecial(Environment.SpecialFolder.MyPictures)) return "pictures";
            if (MatchSpecial(Environment.SpecialFolder.MyVideos)) return "videos";
            if (MatchSpecial(Environment.SpecialFolder.MyMusic)) return "music";

            var downloads = ResolveDownloadsFolder();
            if (!string.IsNullOrWhiteSpace(downloads) &&
                string.Equals(downloads.TrimEnd('\\', '/'), input.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                return "downloads";
            }

            return string.Empty;
        }
    }
}
