using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class FolderFilePathsNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly FolderFilePathsNode _ffp;
        private bool _isLoading;

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> FolderOutputKeyOptions { get; } = new();
        public ObservableCollection<string> ExtensionTags { get; } = new();

        [ObservableProperty] private string _folderPath = string.Empty;
        [ObservableProperty] private string? _folderSourceNodeId;
        [ObservableProperty] private string? _folderSourceOutputKey;
        [ObservableProperty] private bool _refreshFolderSourceNodeBeforeUse;

        [ObservableProperty] private bool _includeSubfolders;
        [ObservableProperty] private string _extensionFilterText = string.Empty;
        [ObservableProperty] private string _extensionTagDraft = string.Empty;

        [ObservableProperty] private bool _readFileContents;
        [ObservableProperty] private string _readContentExtensionsText = ".txt";

        [ObservableProperty] private string _lastRunSummary = "(chưa chạy)";

        public FolderFilePathsNodeDialogViewModel(FolderFilePathsNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _ffp = node;
            _isLoading = true;
            try
            {
                FolderPath = _ffp.FolderPath;
                FolderSourceNodeId = _ffp.FolderSourceNodeId;
                FolderSourceOutputKey = _ffp.FolderSourceOutputKey;
                RefreshFolderSourceNodeBeforeUse = _ffp.RefreshFolderSourceNodeBeforeUse;
                IncludeSubfolders = _ffp.IncludeSubfolders;
                ExtensionFilterText = _ffp.ExtensionFilterText;
                ReadFileContents = _ffp.ReadFileContents;
                ReadContentExtensionsText = string.IsNullOrWhiteSpace(_ffp.ReadContentExtensionsText)
                    ? ".txt"
                    : _ffp.ReadContentExtensionsText;

                ExtensionTags.Clear();
                foreach (var t in _ffp.ExtensionTags.Where(x => !string.IsNullOrWhiteSpace(x)))
                    ExtensionTags.Add(t.Trim());

                RefreshAvailableNodes();
                FillOutputKeys(FolderSourceNodeId, FolderOutputKeyOptions);
                UpdateLastRunSummary();
                _ffp.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(FolderFilePathsNode.ResolvedOutputs))
                        UpdateLastRunSummary();
                };
            }
            finally
            {
                _isLoading = false;
            }
        }

        protected override string GetDefaultTitle() => "File trong thư mục";

        public void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;
            foreach (var n in _host.ViewModel.Nodes)
            {
                if (string.Equals(n.Id, _ffp.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                AvailableNodeOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
            }
        }

        private void FillOutputKeys(string? nodeId, ObservableCollection<WorkflowOutputKeyOption> target)
        {
            target.Clear();
            if (string.IsNullOrWhiteSpace(nodeId) || _host.ViewModel?.Nodes == null) return;
            var src = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (src?.DynamicOutputs == null) return;
            foreach (var o in src.DynamicOutputs)
            {
                var key = o.Key ?? string.Empty;
                target.Add(new WorkflowOutputKeyOption
                {
                    Key = key,
                    DisplayName = o.DisplayName ?? key,
                    Type = o.OutputType ?? o.ConvertType
                });
            }
        }

        partial void OnFolderSourceNodeIdChanged(string? value)
        {
            _ffp.FolderSourceNodeId = value;
            if (!_isLoading)
            {
                FolderSourceOutputKey = null;
                _ffp.FolderSourceOutputKey = null;
            }
            FillOutputKeys(value, FolderOutputKeyOptions);
        }

        partial void OnFolderSourceOutputKeyChanged(string? value) => _ffp.FolderSourceOutputKey = value;

        partial void OnRefreshFolderSourceNodeBeforeUseChanged(bool value) => _ffp.RefreshFolderSourceNodeBeforeUse = value;

        partial void OnFolderPathChanged(string value) => _ffp.FolderPath = value ?? string.Empty;
        partial void OnIncludeSubfoldersChanged(bool value) => _ffp.IncludeSubfolders = value;
        partial void OnExtensionFilterTextChanged(string value) => _ffp.ExtensionFilterText = value ?? string.Empty;
        partial void OnReadFileContentsChanged(bool value) => _ffp.ReadFileContents = value;
        partial void OnReadContentExtensionsTextChanged(string value) =>
            _ffp.ReadContentExtensionsText = string.IsNullOrWhiteSpace(value) ? ".txt" : value!;

        [RelayCommand]
        private void AddExtensionTag()
        {
            var raw = (ExtensionTagDraft ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw)) return;
            if (!raw.StartsWith('.')) raw = "." + raw.TrimStart('.');
            if (ExtensionTags.Contains(raw, StringComparer.OrdinalIgnoreCase)) return;
            ExtensionTags.Add(raw);
            ExtensionTagDraft = string.Empty;
            SyncTagsToNode();
        }

        [RelayCommand]
        private void RemoveExtensionTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            var match = ExtensionTags.FirstOrDefault(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                ExtensionTags.Remove(match);
                SyncTagsToNode();
            }
        }

        [RelayCommand]
        private void AddPresetTag(string? preset)
        {
            if (string.IsNullOrWhiteSpace(preset)) return;
            var p = preset.Trim();
            if (!p.StartsWith('.')) p = "." + p.TrimStart('.');
            if (ExtensionTags.Contains(p, StringComparer.OrdinalIgnoreCase)) return;
            ExtensionTags.Add(p);
            SyncTagsToNode();
        }

        private void SyncTagsToNode()
        {
            _ffp.ExtensionTags.Clear();
            foreach (var t in ExtensionTags)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    _ffp.ExtensionTags.Add(t.Trim());
            }
        }

        private void UpdateLastRunSummary()
        {
            lock (_ffp.ResolvedOutputsSyncRoot)
            {
                var err = _ffp.ResolvedOutputs.TryGetValue("errorMessage", out var e) ? e?.ToString() : null;
                var cnt = _ffp.ResolvedOutputs.TryGetValue("count", out var c) ? c?.ToString() : null;
                if (!string.IsNullOrWhiteSpace(err))
                    LastRunSummary = "Lỗi: " + err;
                else if (!string.IsNullOrWhiteSpace(cnt))
                    LastRunSummary = $"Đã liệt kê {cnt} mục (xem output paths).";
                else
                    LastRunSummary = "(chưa chạy)";
            }
        }

        protected override void OnSaveTitle()
        {
            _ffp.FolderPath = FolderPath ?? string.Empty;
            _ffp.FolderSourceNodeId = FolderSourceNodeId;
            _ffp.FolderSourceOutputKey = FolderSourceOutputKey;
            _ffp.RefreshFolderSourceNodeBeforeUse = RefreshFolderSourceNodeBeforeUse;
            _ffp.IncludeSubfolders = IncludeSubfolders;
            _ffp.ExtensionFilterText = ExtensionFilterText ?? string.Empty;
            _ffp.ReadFileContents = ReadFileContents;
            _ffp.ReadContentExtensionsText = string.IsNullOrWhiteSpace(ReadContentExtensionsText)
                ? ".txt"
                : ReadContentExtensionsText;
            SyncTagsToNode();
            _ffp.NotifyTitleChanged();
            UpdateLastRunSummary();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }
}
