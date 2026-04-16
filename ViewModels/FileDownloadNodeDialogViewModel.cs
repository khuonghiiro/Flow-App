using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    /// <summary>Một dòng: Node + Key + mẫu tên file + định dạng lưu (txt, csv…).</summary>
    public partial class FileDownloadAdditionalSaveRowViewModel : ObservableObject
    {
        [ObservableProperty] private string? _sourceNodeId;
        [ObservableProperty] private string? _sourceOutputKey;
        [ObservableProperty] private string _nameTemplate = string.Empty;
        [ObservableProperty] private string _saveFormat = string.Empty;

        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();
    }

    public partial class FileDownloadNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly FileDownloadNode _fd;
        private bool _isLoading;
        /// <summary>True khi đang clear+refill một ObservableCollection key option — WPF binding sẽ fire
        /// SelectedValue=null tạm thời trong lúc đó; flag này ngăn ghi sai vào model.</summary>
        private bool _isUpdatingKeys;

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

        public ObservableCollection<WorkflowOutputKeyOption> FileNameOutputKeyOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> UrlOutputKeyOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> FolderOutputKeyOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> CurlOutputKeyOptions { get; } = new();

        public ObservableCollection<FileDownloadAdditionalSaveRowViewModel> AdditionalSaveRows { get; } = new();

        [ObservableProperty] private bool _saveAdditionalOutputFiles;

        [ObservableProperty] private string _fileNameTemplate = "download_{datetime}";
        [ObservableProperty] private int _maxFileNameLength = 200;
        [ObservableProperty] private bool _autoIncrementIfExists = true;
        [ObservableProperty] private bool _removeDiacriticsFromFileName = false;

        [ObservableProperty] private string _downloadUrl = string.Empty;
        [ObservableProperty] private string? _urlSourceNodeId;
        [ObservableProperty] private string? _urlSourceOutputKey;

        [ObservableProperty] private string _curlCommand = string.Empty;
        [ObservableProperty] private string? _curlSourceNodeId;
        [ObservableProperty] private string? _curlSourceOutputKey;

        [ObservableProperty] private string _downloadFolderPath = string.Empty;
        [ObservableProperty] private string? _folderSourceNodeId;
        [ObservableProperty] private string? _folderSourceOutputKey;

        [ObservableProperty] private string? _fileNameSourceNodeId;
        [ObservableProperty] private string? _fileNameSourceOutputKey;

        [ObservableProperty] private string _lastRunSummary = "(chưa chạy)";

        public FileDownloadNodeDialogViewModel(FileDownloadNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _fd = node;
            _isLoading = true;
            try
            {
                FileNameTemplate = _fd.FileNameTemplate;
                MaxFileNameLength = _fd.MaxFileNameLength;
                AutoIncrementIfExists = _fd.AutoIncrementIfExists;
                RemoveDiacriticsFromFileName = _fd.RemoveDiacriticsFromFileName;
                DownloadUrl = _fd.DownloadUrl;
                UrlSourceNodeId = _fd.UrlSourceNodeId;
                UrlSourceOutputKey = _fd.UrlSourceOutputKey;
                CurlCommand = _fd.CurlCommand;
                CurlSourceNodeId = _fd.CurlSourceNodeId;
                CurlSourceOutputKey = _fd.CurlSourceOutputKey;
                DownloadFolderPath = _fd.DownloadFolderPath;
                FolderSourceNodeId = _fd.FolderSourceNodeId;
                FolderSourceOutputKey = _fd.FolderSourceOutputKey;
                FileNameSourceNodeId = _fd.FileNameSourceNodeId;
                FileNameSourceOutputKey = _fd.FileNameSourceOutputKey;

                SaveAdditionalOutputFiles = _fd.SaveAdditionalOutputFiles;
                LoadAdditionalSaveRowsFromNode();

                RefreshAvailableNodes();
                RefreshAllKeyOptions();
                UpdateLastRunSummary();
                _fd.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(FileDownloadNode.ResolvedOutputs))
                        UpdateLastRunSummary();
                };
            }
            finally
            {
                _isLoading = false;
            }
        }

        protected override string GetDefaultTitle() => "Tải file";

        private void LoadAdditionalSaveRowsFromNode()
        {
            foreach (var r in AdditionalSaveRows)
                r.PropertyChanged -= AdditionalSaveRow_PropertyChanged;
            AdditionalSaveRows.Clear();
            var list = _fd.AdditionalOutputSaves ?? new System.Collections.Generic.List<FileDownloadAdditionalOutputSaveEntry>();
            if (list.Count == 0)
            {
                AddAdditionalSaveRow();
                return;
            }
            foreach (var e in list)
            {
                var row = new FileDownloadAdditionalSaveRowViewModel
                {
                    SourceNodeId = e.SourceNodeId,
                    SourceOutputKey = e.SourceOutputKey,
                    NameTemplate = e.NameTemplate ?? string.Empty,
                    SaveFormat = e.SaveFormat ?? string.Empty
                };
                row.PropertyChanged += AdditionalSaveRow_PropertyChanged;
                AdditionalSaveRows.Add(row);
                RefreshOutputKeyOptionsFor(row);
            }
        }

        private void AdditionalSaveRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading || _isUpdatingKeys) return;
            if (sender is not FileDownloadAdditionalSaveRowViewModel row) return;
            if (e.PropertyName == nameof(FileDownloadAdditionalSaveRowViewModel.SourceNodeId))
            {
                row.SourceOutputKey = null;
                RefreshOutputKeyOptionsFor(row);
            }
            if (e.PropertyName == nameof(FileDownloadAdditionalSaveRowViewModel.SourceNodeId) ||
                e.PropertyName == nameof(FileDownloadAdditionalSaveRowViewModel.SourceOutputKey) ||
                e.PropertyName == nameof(FileDownloadAdditionalSaveRowViewModel.NameTemplate) ||
                e.PropertyName == nameof(FileDownloadAdditionalSaveRowViewModel.SaveFormat))
            {
                SyncAdditionalSavesToNode();
            }
        }

        public void RefreshOutputKeyOptionsFor(FileDownloadAdditionalSaveRowViewModel item)
        {
            // Dùng flag để ngăn WPF binding ghi null vào SourceOutputKey
            // trong lúc clear+refill AvailableOutputKeyOptions.
            var savedKey = item.SourceOutputKey;
            _isUpdatingKeys = true;
            try
            {
                item.AvailableOutputKeyOptions.Clear();
                if (!string.IsNullOrWhiteSpace(item.SourceNodeId) && _host.ViewModel?.Nodes != null)
                {
                    var node = _host.ViewModel.Nodes.FirstOrDefault(n =>
                        string.Equals(n.Id, item.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                    if (node?.DynamicOutputs != null)
                    {
                        foreach (var o in node.DynamicOutputs)
                        {
                            item.AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
                            {
                                Key = o.Key ?? string.Empty,
                                DisplayName = o.DisplayName ?? o.Key,
                                Type = o.OutputType ?? o.ConvertType
                            });
                        }
                    }
                }
            }
            finally
            {
                _isUpdatingKeys = false;
            }

            // Sau khi refill: restore key cũ nếu còn tồn tại trong list,
            // ngược lại auto-chọn key đầu tiên (chỉ khi user đã đổi node, không khi loading).
            if (item.AvailableOutputKeyOptions.Count > 0)
            {
                var keyStillValid = item.AvailableOutputKeyOptions.Any(k =>
                    string.Equals(k.Key, savedKey, StringComparison.Ordinal));
                if (!keyStillValid)
                    item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key;
                else if (!string.Equals(item.SourceOutputKey, savedKey, StringComparison.Ordinal))
                    item.SourceOutputKey = savedKey; // restore nếu binding đã xóa tạm
            }
        }

        private void SyncAdditionalSavesToNode()
        {
            _fd.AdditionalOutputSaves = AdditionalSaveRows
                .Where(r => !string.IsNullOrWhiteSpace(r.SourceNodeId) && !string.IsNullOrWhiteSpace(r.SourceOutputKey))
                .Select(r => new FileDownloadAdditionalOutputSaveEntry
                {
                    SourceNodeId = r.SourceNodeId,
                    SourceOutputKey = r.SourceOutputKey,
                    NameTemplate = string.IsNullOrWhiteSpace(r.NameTemplate) ? null : r.NameTemplate.Trim(),
                    SaveFormat = string.IsNullOrWhiteSpace(r.SaveFormat) ? null : r.SaveFormat.Trim()
                })
                .ToList();
        }

        [RelayCommand]
        private void AddAdditionalSaveRow()
        {
            var row = new FileDownloadAdditionalSaveRowViewModel();
            row.PropertyChanged += AdditionalSaveRow_PropertyChanged;
            AdditionalSaveRows.Add(row);
            RefreshOutputKeyOptionsFor(row);
            if (!_isLoading)
                SyncAdditionalSavesToNode();
        }

        [RelayCommand]
        private void RemoveAdditionalSaveRow(FileDownloadAdditionalSaveRowViewModel? row)
        {
            if (row == null || !AdditionalSaveRows.Contains(row)) return;
            if (AdditionalSaveRows.Count <= 1)
            {
                row.SourceNodeId = null;
                row.SourceOutputKey = null;
                row.NameTemplate = string.Empty;
                row.SaveFormat = string.Empty;
                row.AvailableOutputKeyOptions.Clear();
                if (!_isLoading)
                    SyncAdditionalSavesToNode();
                return;
            }
            row.PropertyChanged -= AdditionalSaveRow_PropertyChanged;
            AdditionalSaveRows.Remove(row);
            if (!_isLoading)
                SyncAdditionalSavesToNode();
        }

        partial void OnSaveAdditionalOutputFilesChanged(bool value)
        {
            _fd.SaveAdditionalOutputFiles = value;
        }

        partial void OnFileNameTemplateChanged(string value) => _fd.FileNameTemplate = value ?? string.Empty;

        public void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;
            foreach (var n in _host.ViewModel.Nodes)
            {
                if (string.Equals(n.Id, _fd.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                AvailableNodeOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
            }
            foreach (var row in AdditionalSaveRows)
                RefreshOutputKeyOptionsFor(row);
        }

        private void RefreshAllKeyOptions()
        {
            var savedFileNameKey = FileNameSourceOutputKey ?? _fd.FileNameSourceOutputKey;
            var savedUrlKey = UrlSourceOutputKey ?? _fd.UrlSourceOutputKey;
            var savedFolderKey = FolderSourceOutputKey ?? _fd.FolderSourceOutputKey;
            var savedCurlKey = CurlSourceOutputKey ?? _fd.CurlSourceOutputKey;

            FillOutputKeys(FileNameSourceNodeId, FileNameOutputKeyOptions);
            FillOutputKeys(UrlSourceNodeId, UrlOutputKeyOptions);
            FillOutputKeys(FolderSourceNodeId, FolderOutputKeyOptions);
            FillOutputKeys(CurlSourceNodeId, CurlOutputKeyOptions);

            // Khi mở lại dialog / load workflow: ưu tiên restore key đã lưu thay vì mặc định key đầu.
            RestoreSelectedOutputKey(savedFileNameKey, FileNameOutputKeyOptions, v => FileNameSourceOutputKey = v, autoPickFirstWhenMissing: false);
            RestoreSelectedOutputKey(savedUrlKey, UrlOutputKeyOptions, v => UrlSourceOutputKey = v, autoPickFirstWhenMissing: false);
            RestoreSelectedOutputKey(savedFolderKey, FolderOutputKeyOptions, v => FolderSourceOutputKey = v, autoPickFirstWhenMissing: false);
            RestoreSelectedOutputKey(savedCurlKey, CurlOutputKeyOptions, v => CurlSourceOutputKey = v, autoPickFirstWhenMissing: false);
        }

        private void FillOutputKeys(string? nodeId, ObservableCollection<WorkflowOutputKeyOption> target)
        {
            _isUpdatingKeys = true;
            try
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
            finally
            {
                _isUpdatingKeys = false;
            }
        }

        partial void OnFileNameSourceNodeIdChanged(string? value)
        {
            _fd.FileNameSourceNodeId = value;
            var preferredKey = FileNameSourceOutputKey ?? _fd.FileNameSourceOutputKey;
            if (!_isLoading)
            {
                FileNameSourceOutputKey = null;
                _fd.FileNameSourceOutputKey = null;
            }
            FillOutputKeys(value, FileNameOutputKeyOptions);

            // Chỉ auto chọn key đầu khi user đổi node (không phải lúc loading mở dialog).
            RestoreSelectedOutputKey(preferredKey, FileNameOutputKeyOptions, v => FileNameSourceOutputKey = v, autoPickFirstWhenMissing: !_isLoading);
        }

        partial void OnFileNameSourceOutputKeyChanged(string? value)
        {
            // Ignore only transient null while refilling key collection.
            if ((_isUpdatingKeys || _isLoading) && string.IsNullOrWhiteSpace(value)) return;
            _fd.FileNameSourceOutputKey = value;

            // Khi user đã chọn source key cho tên file, mặc định mẫu tên dùng {filename}
            // để có thể kết hợp thêm placeholder khác ({filename}_{date}, ...).
            if (!string.IsNullOrWhiteSpace(value) &&
                (string.IsNullOrWhiteSpace(FileNameTemplate) ||
                 string.Equals(FileNameTemplate.Trim(), "download_{datetime}", StringComparison.OrdinalIgnoreCase)))
            {
                FileNameTemplate = "{filename}";
            }
        }

        partial void OnUrlSourceNodeIdChanged(string? value)
        {
            _fd.UrlSourceNodeId = value;
            var preferredKey = UrlSourceOutputKey ?? _fd.UrlSourceOutputKey;
            if (!_isLoading)
            {
                UrlSourceOutputKey = null;
                _fd.UrlSourceOutputKey = null;
            }
            FillOutputKeys(value, UrlOutputKeyOptions);
            RestoreSelectedOutputKey(preferredKey, UrlOutputKeyOptions, v => UrlSourceOutputKey = v, autoPickFirstWhenMissing: !_isLoading);
        }

        partial void OnUrlSourceOutputKeyChanged(string? value)
        {
            if (_isUpdatingKeys || _isLoading) return;
            _fd.UrlSourceOutputKey = value;
        }

        partial void OnFolderSourceNodeIdChanged(string? value)
        {
            _fd.FolderSourceNodeId = value;
            var preferredKey = FolderSourceOutputKey ?? _fd.FolderSourceOutputKey;
            if (!_isLoading)
            {
                FolderSourceOutputKey = null;
                _fd.FolderSourceOutputKey = null;
            }
            FillOutputKeys(value, FolderOutputKeyOptions);
            RestoreSelectedOutputKey(preferredKey, FolderOutputKeyOptions, v => FolderSourceOutputKey = v, autoPickFirstWhenMissing: !_isLoading);
        }

        partial void OnFolderSourceOutputKeyChanged(string? value)
        {
            if (_isUpdatingKeys || _isLoading) return;
            _fd.FolderSourceOutputKey = value;
        }

        partial void OnCurlSourceNodeIdChanged(string? value)
        {
            _fd.CurlSourceNodeId = value;
            var preferredKey = CurlSourceOutputKey ?? _fd.CurlSourceOutputKey;
            if (!_isLoading)
            {
                CurlSourceOutputKey = null;
                _fd.CurlSourceOutputKey = null;
            }
            FillOutputKeys(value, CurlOutputKeyOptions);
            RestoreSelectedOutputKey(preferredKey, CurlOutputKeyOptions, v => CurlSourceOutputKey = v, autoPickFirstWhenMissing: !_isLoading);
        }

        private void RestoreSelectedOutputKey(
            string? preferredKey,
            ObservableCollection<WorkflowOutputKeyOption> options,
            System.Action<string?> applySelection,
            bool autoPickFirstWhenMissing)
        {
            if (options.Count == 0)
            {
                applySelection(null);
                return;
            }

            if (!string.IsNullOrWhiteSpace(preferredKey) &&
                options.Any(k => string.Equals(k.Key, preferredKey, StringComparison.Ordinal)))
            {
                applySelection(preferredKey);
                return;
            }

            applySelection(autoPickFirstWhenMissing ? options[0].Key : null);
        }

        partial void OnCurlSourceOutputKeyChanged(string? value)
        {
            if (_isUpdatingKeys || _isLoading) return;
            _fd.CurlSourceOutputKey = value;
        }

        partial void OnMaxFileNameLengthChanged(int value) => _fd.MaxFileNameLength = value;
        partial void OnAutoIncrementIfExistsChanged(bool value) => _fd.AutoIncrementIfExists = value;
        partial void OnRemoveDiacriticsFromFileNameChanged(bool value) => _fd.RemoveDiacriticsFromFileName = value;
        partial void OnDownloadUrlChanged(string value) => _fd.DownloadUrl = value ?? string.Empty;
        partial void OnCurlCommandChanged(string value) => _fd.CurlCommand = value ?? string.Empty;
        partial void OnDownloadFolderPathChanged(string value) => _fd.DownloadFolderPath = value ?? string.Empty;

        private void UpdateLastRunSummary()
        {
            var ok = _fd.ResolvedOutputs.TryGetValue("completed", out var c) && string.Equals(c?.ToString(), "True", System.StringComparison.OrdinalIgnoreCase);
            var path = _fd.ResolvedOutputs.TryGetValue("filePath", out var p) ? p?.ToString() : null;
            var err = _fd.ResolvedOutputs.TryGetValue("errorMessage", out var e) ? e?.ToString() : null;
            if (ok && !string.IsNullOrWhiteSpace(path))
                LastRunSummary = path!;
            else if (!string.IsNullOrWhiteSpace(err))
                LastRunSummary = "Lỗi: " + err;
            else
                LastRunSummary = "(chưa chạy)";
        }

        protected override void OnSaveTitle()
        {
            _fd.FileNameTemplate = FileNameTemplate ?? string.Empty;
            _fd.MaxFileNameLength = MaxFileNameLength;
            _fd.AutoIncrementIfExists = AutoIncrementIfExists;
            _fd.RemoveDiacriticsFromFileName = RemoveDiacriticsFromFileName;
            _fd.DownloadUrl = DownloadUrl ?? string.Empty;
            _fd.UrlSourceNodeId = UrlSourceNodeId;
            _fd.UrlSourceOutputKey = UrlSourceOutputKey;
            _fd.CurlCommand = CurlCommand ?? string.Empty;
            _fd.CurlSourceNodeId = CurlSourceNodeId;
            _fd.CurlSourceOutputKey = CurlSourceOutputKey;
            _fd.DownloadFolderPath = DownloadFolderPath ?? string.Empty;
            _fd.FolderSourceNodeId = FolderSourceNodeId;
            _fd.FolderSourceOutputKey = FolderSourceOutputKey;
            _fd.FileNameSourceNodeId = FileNameSourceNodeId;
            _fd.FileNameSourceOutputKey = FileNameSourceOutputKey;
            _fd.SaveAdditionalOutputFiles = SaveAdditionalOutputFiles;
            SyncAdditionalSavesToNode();
            _fd.NotifyTitleChanged();
            UpdateLastRunSummary();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }
}
