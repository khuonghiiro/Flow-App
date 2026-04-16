using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class DataFetcherScanKeySelectionItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class DataFetcherNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly DataFetcherNode _fetcherNode;
        private bool _isLoadingFromNode = false;
        private bool _isNormalizingScanInterval = false;

        // ===== Node & Output Key Options =====
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();
        public ObservableCollection<DataFetcherScanKeySelectionItem> DataReadyScanKeyOptions { get; } = new();

        // ===== Observable Properties =====
        [ObservableProperty]
        private string? _selectedSourceNodeId;

        [ObservableProperty]
        private string? _selectedSourceOutputKey;

        [ObservableProperty]
        private bool _waitForWebNodeLoad;

        [ObservableProperty]
        private bool _isSourceWebNode;

        [ObservableProperty]
        private bool _hasSourceNode;

        [ObservableProperty]
        private bool _enableTimer;

        [ObservableProperty]
        private int _timerIntervalValue = 5;

        [ObservableProperty]
        private string _timerUnit = "s";

        [ObservableProperty]
        private bool _enableRealtime;

        [ObservableProperty]
        private bool _enableDataReadyScan;

        [ObservableProperty]
        private int _dataReadyScanIntervalValue = 1;

        [ObservableProperty]
        private string _dataReadyScanUnit = "s";

        [ObservableProperty]
        private bool _runSourceNodeFirst;

        [ObservableProperty]
        private string _fetchedValuePreview = "(chưa lấy)";

        // ===== Options =====
        public List<string> TimerUnitOptions { get; } = new() { "ms", "s", "m" };

        public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
        {
            new TitleDisplayModeOption(TitleDisplayMode.Hidden,  "Ẩn tiêu đề"),
            new TitleDisplayModeOption(TitleDisplayMode.Hover,   "Hiện khi hover"),
            new TitleDisplayModeOption(TitleDisplayMode.Always,  "Luôn hiện")
        };

        // ===== Constructor =====
        public DataFetcherNodeDialogViewModel(DataFetcherNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _fetcherNode = node;

            _isLoadingFromNode = true;
            try
            {
                WaitForWebNodeLoad    = _fetcherNode.WaitForWebNodeLoad;
                EnableTimer           = _fetcherNode.EnableTimer;
                TimerIntervalValue    = _fetcherNode.TimerIntervalValue;
                TimerUnit             = _fetcherNode.TimerUnit;
                EnableRealtime        = _fetcherNode.EnableRealtime;
                EnableDataReadyScan   = _fetcherNode.EnableDataReadyScan;
                DataReadyScanIntervalValue = _fetcherNode.DataReadyScanIntervalValue;
                DataReadyScanUnit          = _fetcherNode.DataReadyScanUnit;
                RunSourceNodeFirst    = _fetcherNode.RunSourceNodeFirst;

                RefreshAvailableNodes();

                if (!string.IsNullOrWhiteSpace(_fetcherNode.SourceNodeId))
                {
                    SelectedSourceNodeId = _fetcherNode.SourceNodeId;
                    RefreshOutputKeyOptions();
                    SelectedSourceOutputKey = _fetcherNode.SourceOutputKey ?? string.Empty; // empty = fetch all
                }
                else
                {
                    RefreshOutputKeyOptions();
                }

                // Preview last fetched value
                var firstOut = _fetcherNode.DynamicOutputs?.FirstOrDefault();
                FetchedValuePreview = string.IsNullOrEmpty(firstOut?.UserValueOverride)
                    ? "(chưa lấy)"
                    : firstOut.UserValueOverride!;
            }
            finally
            {
                _isLoadingFromNode = false;
            }
        }

        protected override string GetDefaultTitle() => "Data Fetcher";

        // ===== Public: called by dialog code-behind =====
        public void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;

            foreach (var n in _host.ViewModel.Nodes)
            {
                if (string.Equals(n.Id, _fetcherNode.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;

                AvailableNodeOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title  = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
            }
        }

        private void RefreshOutputKeyOptions()
        {
            AvailableOutputKeyOptions.Clear();
            DataReadyScanKeyOptions.Clear();

            if (string.IsNullOrWhiteSpace(SelectedSourceNodeId) || _host.ViewModel?.Nodes == null)
            {
                UpdateIsSourceWebNode(null);
                HasSourceNode = false;
                return;
            }

            var srcNode = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, SelectedSourceNodeId, StringComparison.OrdinalIgnoreCase));

            UpdateIsSourceWebNode(srcNode);
            HasSourceNode = srcNode != null;

            if (srcNode?.DynamicOutputs == null)
            {
                // Khi load workflow, một số node nguồn có thể chưa build DynamicOutputs.
                // Vẫn hiển thị các key đã lưu để user thấy cấu hình cũ.
                AppendSavedDataReadyScanKeysIfMissing();
                return;
            }

            // Option rỗng để user chọn "lấy tất cả outputs".
            AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
            {
                Key = string.Empty,
                DisplayName = "(Tất cả outputs)"
            });

            foreach (var output in srcNode.DynamicOutputs)
            {
                var key = output.Key ?? string.Empty;
                AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
                {
                    Key         = key,
                    DisplayName = output.DisplayName ?? key,
                    Type        = output.OutputType ?? output.ConvertType
                });

                var scanOpt = new DataFetcherScanKeySelectionItem
                {
                    Key = key,
                    DisplayName = string.IsNullOrWhiteSpace(output.DisplayName) ? key : output.DisplayName,
                    IsSelected = _fetcherNode.DataReadyScanKeys.Any(k =>
                        string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                };
                scanOpt.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DataFetcherScanKeySelectionItem.IsSelected))
                    {
                        ApplySelectedScanKeysToNode();
                    }
                };
                DataReadyScanKeyOptions.Add(scanOpt);
            }

            AppendSavedDataReadyScanKeysIfMissing();
            // Keep node state in sync even when workflow is saved while dialog is still open.
            ApplySelectedScanKeysToNode();
        }

        private void UpdateIsSourceWebNode(WorkflowNode? srcNode)
        {
            IsSourceWebNode = srcNode is WebNode;
        }

        // ===== Partial property changed =====
        partial void OnSelectedSourceNodeIdChanged(string? value)
        {
            _fetcherNode.SourceNodeId = value;
            if (!_isLoadingFromNode)
            {
                _fetcherNode.SourceOutputKey = null;
                SelectedSourceOutputKey = null;
                _fetcherNode.DataReadyScanKeys = new List<string>();
            }
            RefreshOutputKeyOptions();
        }

        partial void OnSelectedSourceOutputKeyChanged(string? value)
        {
            _fetcherNode.SourceOutputKey = string.IsNullOrWhiteSpace(value) ? null : value; // null = fetch all keys
        }

        partial void OnEnableTimerChanged(bool value)    => _fetcherNode.EnableTimer = value;
        partial void OnTimerIntervalValueChanged(int value)
        {
            _fetcherNode.TimerIntervalValue = value;
            NormalizeDataReadyScanInterval();
        }
        partial void OnTimerUnitChanged(string value)
        {
            _fetcherNode.TimerUnit = value ?? "s";
            NormalizeDataReadyScanInterval();
        }
        partial void OnEnableRealtimeChanged(bool value) => _fetcherNode.EnableRealtime = value;
        partial void OnEnableDataReadyScanChanged(bool value)
        {
            _fetcherNode.EnableDataReadyScan = value;
            if (value) NormalizeDataReadyScanInterval();
        }
        partial void OnDataReadyScanIntervalValueChanged(int value)
        {
            _fetcherNode.DataReadyScanIntervalValue = value;
            NormalizeDataReadyScanInterval();
        }
        partial void OnDataReadyScanUnitChanged(string value)
        {
            _fetcherNode.DataReadyScanUnit = value ?? "s";
            NormalizeDataReadyScanInterval();
        }
        partial void OnRunSourceNodeFirstChanged(bool value) => _fetcherNode.RunSourceNodeFirst = value;
        partial void OnWaitForWebNodeLoadChanged(bool value) => _fetcherNode.WaitForWebNodeLoad = value;

        // ===== FetchNow Command =====
        [RelayCommand]
        private void FetchNow()
        {
            if (string.IsNullOrWhiteSpace(_fetcherNode.SourceNodeId))
            {
                FetchedValuePreview = "(chưa cấu hình node nguồn)";
                return;
            }

            try
            {
                // Dùng RequestRunSingleNode để chạy đầy đủ DataFetcherNodeExecutor:
                // - Tìm source node qua tất cả connections (FindSourceNode mới)
                // - Gọi RefreshSavedOutputs → cập nhật toggle "Có X kết quả" + panel output
                // - FetchedValuePreview sẽ được cập nhật sau khi executor chạy xong
                _host.RequestRunSingleNode(_fetcherNode);

                // Hiển thị preview giá trị hiện tại trực tiếp từ source node
                var allNodes = _host.ViewModel?.Nodes?.ToList() ?? new List<WorkflowNode>();
                var srcNode  = allNodes.FirstOrDefault(n =>
                    string.Equals(n.Id, _fetcherNode.SourceNodeId, StringComparison.OrdinalIgnoreCase));

                if (srcNode == null)
                {
                    FetchedValuePreview = "(không tìm thấy node nguồn)";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_fetcherNode.SourceOutputKey))
                {
                    var val = NodeDataPanelService.ResolveDynamicValueByKey(srcNode, _fetcherNode.SourceOutputKey);
                    FetchedValuePreview = string.IsNullOrEmpty(val) || val == "—" ? "(rỗng)" : val;
                }
                else
                {
                    if (srcNode.DynamicOutputs == null || srcNode.DynamicOutputs.Count == 0)
                    {
                        FetchedValuePreview = "(node nguồn không có output)";
                        return;
                    }
                    var sb = new System.Text.StringBuilder();
                    foreach (var output in srcNode.DynamicOutputs)
                    {
                        var val = NodeDataPanelService.ResolveDynamicValueByKey(srcNode, output.Key);
                        sb.AppendLine($"{output.Key}: {val}");
                    }
                    FetchedValuePreview = sb.ToString().TrimEnd();
                }
            }
            catch (Exception ex)
            {
                FetchedValuePreview = $"(lỗi: {ex.Message})";
            }
        }

        /// <summary>Thêm hoặc cập nhật dynamic output port của DataFetcherNode với key/value từ node nguồn.</summary>
        private static void EnsureOutputPort(DataFetcherNode node, string? key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (node.DynamicOutputs == null) return;

            var port = node.DynamicOutputs.FirstOrDefault(p =>
                string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

            if (port == null)
            {
                port = new WorkflowDynamicDataPort
                {
                    Key         = key,
                    DisplayName = key,
                    IsMultiple  = false,
                    OutputType  = WorkflowDataType.String,
                    ConvertType = WorkflowDataType.String
                };
                node.DynamicOutputs.Add(port);
            }

            port.UserValueOverride = value ?? string.Empty;
        }

        // ===== OnSaveTitle =====
        protected override void OnSaveTitle()
        {
            _fetcherNode.SourceNodeId      = SelectedSourceNodeId;
            _fetcherNode.SourceOutputKey   = string.IsNullOrWhiteSpace(SelectedSourceOutputKey) ? null : SelectedSourceOutputKey; // null = fetch all
            _fetcherNode.WaitForWebNodeLoad = WaitForWebNodeLoad;
            _fetcherNode.EnableTimer        = EnableTimer;
            _fetcherNode.TimerIntervalValue = TimerIntervalValue;
            _fetcherNode.TimerUnit          = TimerUnit ?? "s";
            _fetcherNode.EnableRealtime     = EnableRealtime;
            _fetcherNode.EnableDataReadyScan      = EnableDataReadyScan;
            _fetcherNode.DataReadyScanIntervalValue = DataReadyScanIntervalValue;
            _fetcherNode.DataReadyScanUnit          = DataReadyScanUnit ?? "s";
            ApplySelectedScanKeysToNode();
            _fetcherNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }

        private void ApplySelectedScanKeysToNode()
        {
            _fetcherNode.DataReadyScanKeys = DataReadyScanKeyOptions
                .Where(o => o.IsSelected && !string.IsNullOrWhiteSpace(o.Key))
                .Select(o => o.Key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void AppendSavedDataReadyScanKeysIfMissing()
        {
            if (_fetcherNode.DataReadyScanKeys == null || _fetcherNode.DataReadyScanKeys.Count == 0)
                return;

            foreach (var savedKey in _fetcherNode.DataReadyScanKeys)
            {
                if (string.IsNullOrWhiteSpace(savedKey))
                    continue;

                if (DataReadyScanKeyOptions.Any(k => string.Equals(k.Key, savedKey, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var opt = new DataFetcherScanKeySelectionItem
                {
                    Key = savedKey.Trim(),
                    DisplayName = $"{savedKey.Trim()} (saved)",
                    IsSelected = true
                };
                opt.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DataFetcherScanKeySelectionItem.IsSelected))
                        ApplySelectedScanKeysToNode();
                };
                DataReadyScanKeyOptions.Add(opt);
            }
        }

        private void NormalizeDataReadyScanInterval()
        {
            if (_isLoadingFromNode || _isNormalizingScanInterval) return;
            if (!EnableDataReadyScan) return;

            var timerMs = _fetcherNode.GetTimerIntervalMs();
            var scanMs = _fetcherNode.GetDataReadyScanIntervalMs();
            if (scanMs <= timerMs) return;

            _isNormalizingScanInterval = true;
            try
            {
                // Không cho scan vượt quá chu kỳ timer chính.
                DataReadyScanUnit = TimerUnit;
                DataReadyScanIntervalValue = TimerIntervalValue;
            }
            finally
            {
                _isNormalizingScanInterval = false;
            }
        }
    }
}
