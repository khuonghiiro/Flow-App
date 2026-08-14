// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;

namespace FlowMy.ViewModels
{
    public sealed partial class DynamicUiNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly DynamicUiNode _nodeTyped;
        private bool _isSyncingFromNode = false;

        [ObservableProperty] private string _htmlCode = "";
        [ObservableProperty] private string _cssCode = "";
        [ObservableProperty] private string _jsCode = "";
        [ObservableProperty] private string _paramsCode = "";
        [ObservableProperty] private int _codeFontSize = 13;


        public ObservableCollection<OutputKeyItemViewModel> OutputKeysList { get; } = new();
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<CodeInputMappingItemViewModel> InputMappingsList { get; } = new();
        public ObservableCollection<AsyncDataSourceItemViewModel> AsyncDataSourcesList { get; } = new();
        public ObservableCollection<WorkflowDataSourceOption> AsyncAvailableNodeOptions { get; } = new();
        public List<string> AutoRefreshUnitOptions { get; } = new() { "ms", "s", "min" };

        public double WindowWidth
        {
            get => _nodeTyped.WindowWidth;
            set { if (_nodeTyped.WindowWidth != value) { _nodeTyped.WindowWidth = value; OnPropertyChanged(); } }
        }

        public double WindowHeight
        {
            get => _nodeTyped.WindowHeight;
            set { if (_nodeTyped.WindowHeight != value) { _nodeTyped.WindowHeight = value; OnPropertyChanged(); } }
        }

        public double NodeWidth
        {
            get => _nodeTyped.Width;
            set { if (_nodeTyped.Width != value) { _nodeTyped.Width = value; OnPropertyChanged(); } }
        }

        public double NodeHeight
        {
            get => _nodeTyped.Height;
            set { if (_nodeTyped.Height != value) { _nodeTyped.Height = value; OnPropertyChanged(); } }
        }

        public DynamicUiNodeDialogViewModel(DynamicUiNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _nodeTyped = node ?? throw new ArgumentNullException(nameof(node));

            _htmlCode = node.HtmlCode ?? string.Empty;
            _cssCode = node.CssCode ?? string.Empty;
            _jsCode = node.JsCode ?? string.Empty;
            _paramsCode = node.ParamsCode ?? string.Empty;

            // Load existing input mappings
            var mappings = _nodeTyped.InputMappings ?? new List<CodeInputMapping>();
            if (mappings.Count == 0) mappings.Add(new CodeInputMapping());
            foreach (var m in mappings)
            {
                var item = new CodeInputMappingItemViewModel
                {
                    SourceNodeId = m.SourceNodeId,
                    SourceOutputKey = m.SourceOutputKey,
                    InputKeyOverride = m.InputKeyOverride ?? string.Empty,
                    AutoRefreshEnabled = m.AutoRefreshEnabled,
                    AutoRefreshInterval = m.AutoRefreshInterval,
                    AutoRefreshUnit = m.AutoRefreshUnit ?? "ms"
                };
                item.PropertyChanged += InputMappingItem_PropertyChanged;
                InputMappingsList.Add(item);
                RefreshOutputKeyOptionsFor(item);
            }

            // Load output keys
            foreach (var k in _nodeTyped.OutputKeys)
            {
                OutputKeysList.Add(new OutputKeyItemViewModel { Key = k });
            }

            // Load async data sources từ node
            foreach (var ads in _nodeTyped.AsyncDataSources ?? new List<AsyncDataSource>())
            {
                var bodyNodeId = ads.SourceNodeId ?? string.Empty;
                var plainKey = ads.SourceOutputKey ?? string.Empty;
                var compoundKey = string.IsNullOrWhiteSpace(bodyNodeId) ? plainKey : $"{bodyNodeId}|{plainKey}";

                var item = new AsyncDataSourceItemViewModel
                {
                    ReceiverKey = ads.ReceiverKey ?? string.Empty
                };

                var asyncTaskParent = FindAsyncTaskContainingBodyNode(bodyNodeId);
                if (asyncTaskParent != null)
                {
                    item.Tag_AsyncTaskId = asyncTaskParent.Id;
                    item.SourceNodeId = asyncTaskParent.Id;
                }
                else
                {
                    item.SourceNodeId = bodyNodeId;
                }

                item.PropertyChanged += AsyncDataSourceItem_PropertyChanged;
                AsyncDataSourcesList.Add(item);
                RefreshAsyncOutputKeyOptionsFor(item);
                item.SourceOutputKey = compoundKey;
            }

            RefreshAllNodesWithOutputs(AvailableNodeOptions);
            RefreshAsyncAvailableNodes();

            // Load offline assets
            foreach (var asset in _nodeTyped.OfflineAssets ?? new List<HtmlOfflineAsset>())
            {
                var vm = new HtmlOfflineAssetItemViewModel
                {
                    Title = asset.Title ?? string.Empty,
                    Description = asset.Description ?? string.Empty,
                    SourceUrl = asset.SourceUrl ?? string.Empty,
                    LocalFileName = asset.LocalFileName ?? string.Empty,
                    AssetType = asset.AssetType ?? "js",
                    IsEnabled = asset.IsEnabled
                };
                OfflineAssetsList.Add(vm);
            }
            HookOfflineAssetsList();
        }

        public ObservableCollection<HtmlOfflineAssetItemViewModel> OfflineAssetsList { get; } = new();

        public ObservableCollection<PresetDisplayItem> PopularJsPresets { get; } = new(
            HtmlOfflineAssetItemViewModel.WellKnownPresets
                .Where(p => p.Type?.ToLower() == "js")
                .Select(p => new PresetDisplayItem(p)));

        public ObservableCollection<PresetDisplayItem> PopularCssPresets { get; } = new(
            HtmlOfflineAssetItemViewModel.WellKnownPresets
                .Where(p => p.Type?.ToLower() == "css")
                .Select(p => new PresetDisplayItem(p)));

        private void HookOfflineAssetsList()
        {
            OfflineAssetsList.CollectionChanged += (_, e) =>
            {
                if (_isSyncingFromNode) return;
                SyncOfflineAssetsToNode();
            };
        }

        private void SyncOfflineAssetsToNode()
        {
            _nodeTyped.OfflineAssets = OfflineAssetsList.Select(x => x.ToModel()).ToList();
        }

        [RelayCommand]
        private void AddOfflineAsset()
        {
            OfflineAssetsList.Add(new HtmlOfflineAssetItemViewModel
            {
                Title = "Thư viện mới",
                AssetType = "js",
                IsEnabled = true
            });
        }

        [RelayCommand]
        private void RemoveOfflineAsset(HtmlOfflineAssetItemViewModel? item)
        {
            if (item != null && OfflineAssetsList.Contains(item))
                OfflineAssetsList.Remove(item);
        }

        [RelayCommand]
        private void AddOfflineAssetFromPreset(AssetPreset? preset)
        {
            if (preset == null) return;
            if (OfflineAssetsList.Any(a => string.Equals(a.SourceUrl, preset.Url, StringComparison.OrdinalIgnoreCase))) return;

            OfflineAssetsList.Add(new HtmlOfflineAssetItemViewModel
            {
                Title = preset.Title,
                Description = preset.Description,
                SourceUrl = preset.Url,
                LocalFileName = preset.FileName,
                AssetType = preset.Type,
                IsEnabled = true,
                StatusMessage = FlowMy.Services.Utils.HtmlOfflineAssetService.AssetExists(preset.FileName) ? "✓ Có sẵn" : "✗ Chưa tải về"
            });
        }

        protected override string GetDefaultTitle() => "Dynamic UI Form (Sciter)";

        partial void OnHtmlCodeChanged(string value)
        {
            // Do not mutate _nodeTyped live while typing/importing in dialog.
        }

        partial void OnCssCodeChanged(string value)
        {
            // Do not mutate _nodeTyped live while typing/importing in dialog.
        }

        partial void OnJsCodeChanged(string value)
        {
            // Do not mutate _nodeTyped live while typing/importing in dialog.
        }

        partial void OnParamsCodeChanged(string value)
        {
            // Do not mutate _nodeTyped live while typing/importing in dialog.
        }

        protected override void OnSaveTitle()
        {
            _nodeTyped.HtmlCode = HtmlCode ?? string.Empty;
            _nodeTyped.JsCode = JsCode ?? string.Empty;
            _nodeTyped.CssCode = CssCode ?? string.Empty;
            _nodeTyped.ParamsCode = ParamsCode ?? string.Empty;

            SyncInputMappingsToNode();
            SyncOutputKeysToNode();
            SyncOfflineAssetsToNode();

            _nodeTyped.NotifyTitleChanged();
        }

        private void InputMappingItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromNode) return;
            if (sender is not CodeInputMappingItemViewModel item) return;

            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.SourceNodeId))
            {
                RefreshOutputKeyOptionsFor(item);
                SyncInputMappingsToNode();
                return;
            }

            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.SourceOutputKey))
            {
                if (!string.IsNullOrWhiteSpace(item.SourceOutputKey) && string.IsNullOrWhiteSpace(item.InputKeyOverride))
                    item.InputKeyOverride = item.SourceOutputKey.Trim();
                SyncInputMappingsToNode();
                return;
            }

            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.InputKeyOverride) ||
                e.PropertyName == nameof(CodeInputMappingItemViewModel.AutoRefreshEnabled) ||
                e.PropertyName == nameof(CodeInputMappingItemViewModel.AutoRefreshInterval) ||
                e.PropertyName == nameof(CodeInputMappingItemViewModel.AutoRefreshUnit))
            {
                SyncInputMappingsToNode();
            }
        }

        private void SyncInputMappingsToNode()
        {
            _nodeTyped.InputMappings = InputMappingsList.Select(x => new CodeInputMapping
            {
                SourceNodeId = x.SourceNodeId,
                SourceOutputKey = x.SourceOutputKey,
                InputKeyOverride = string.IsNullOrWhiteSpace(x.InputKeyOverride) ? null : x.InputKeyOverride.Trim(),
                AutoRefreshEnabled = x.AutoRefreshEnabled,
                AutoRefreshInterval = x.AutoRefreshInterval > 0 ? x.AutoRefreshInterval : 1000,
                AutoRefreshUnit = x.AutoRefreshUnit ?? "ms"
            }).ToList();
        }

        private void SyncOutputKeysToNode()
        {
            _nodeTyped.OutputKeys = OutputKeysList
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .Select(x => x.Key.Trim())
                .Distinct()
                .ToList();
            _nodeTyped.RebuildDynamicPorts();
            _host.RequestSyncDataPanels(immediate: true);
        }

        public void RefreshOutputKeyOptionsFor(CodeInputMappingItemViewModel item)
        {
            item.AvailableOutputKeyOptions.Clear();
            if (string.IsNullOrWhiteSpace(item.SourceNodeId) || _host.ViewModel?.Nodes == null) return;

            var node = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, item.SourceNodeId, StringComparison.OrdinalIgnoreCase));
            if (node?.DynamicOutputs == null) return;

            foreach (var o in node.DynamicOutputs)
            {
                item.AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key ?? string.Empty,
                    Type = o.OutputType ?? o.ConvertType,
                    DisplayName = o.DisplayName ?? o.Key
                });
            }

            if (item.AvailableOutputKeyOptions.Count > 0 &&
                !item.AvailableOutputKeyOptions.Any(k => string.Equals(k.Key, item.SourceOutputKey, StringComparison.Ordinal)))
            {
                item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key;
            }
        }

        [RelayCommand]
        private void AddInputMapping()
        {
            var item = new CodeInputMappingItemViewModel();
            item.PropertyChanged += InputMappingItem_PropertyChanged;
            InputMappingsList.Add(item);
            SyncInputMappingsToNode();
        }

        [RelayCommand]
        private void RemoveInputMapping(CodeInputMappingItemViewModel? item)
        {
            if (item != null && InputMappingsList.Contains(item) && InputMappingsList.Count > 1)
            {
                item.PropertyChanged -= InputMappingItem_PropertyChanged;
                InputMappingsList.Remove(item);
                SyncInputMappingsToNode();
            }
        }

        [RelayCommand]
        private void AddOutputKey()
        {
            OutputKeysList.Add(new OutputKeyItemViewModel { Key = "result" });
            SyncOutputKeysToNode();
        }

        [RelayCommand]
        private void RemoveOutputKey(OutputKeyItemViewModel? item)
        {
            if (item != null && OutputKeysList.Contains(item))
            {
                OutputKeysList.Remove(item);
                SyncOutputKeysToNode();
            }
        }

        [RelayCommand]
        private void SyncOutputKeysFromParams()
        {
            var raw = ParamsCode ?? string.Empty;
            var parsedKeys = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var lines = raw.Replace("\r\n", "\n").Split('\n');
            foreach (var lineRaw in lines)
            {
                var line = lineRaw.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal)) continue;

                var colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) continue;

                var key = line[..colonIndex].Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (key.Any(char.IsWhiteSpace)) continue;

                if (seen.Add(key))
                    parsedKeys.Add(key);
            }

            if (parsedKeys.Count == 0) return;

            OutputKeysList.Clear();
            foreach (var key in parsedKeys)
            {
                OutputKeysList.Add(new OutputKeyItemViewModel { Key = key });
            }
            SyncOutputKeysToNode();
        }

        [RelayCommand]
        private void IncreaseCodeFontSize()
        {
            if (CodeFontSize < 24) CodeFontSize++;
        }

        [RelayCommand]
        private void DecreaseCodeFontSize()
        {
            if (CodeFontSize > 10) CodeFontSize--;
        }

        private void AsyncDataSourceItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromNode) return;
            if (sender is not AsyncDataSourceItemViewModel item) return;

            if (e.PropertyName == nameof(AsyncDataSourceItemViewModel.SourceNodeId))
            {
                item.Tag_AsyncTaskId = item.SourceNodeId;
                RefreshAsyncOutputKeyOptionsFor(item);
                SyncAsyncDataSourcesToNode();
                return;
            }

            if (e.PropertyName == nameof(AsyncDataSourceItemViewModel.SourceOutputKey))
            {
                if (!string.IsNullOrWhiteSpace(item.SourceOutputKey) && string.IsNullOrWhiteSpace(item.ReceiverKey))
                    item.ReceiverKey = item.ActualOutputKey.Trim();
                SyncAsyncDataSourcesToNode();
                return;
            }

            if (e.PropertyName == nameof(AsyncDataSourceItemViewModel.ReceiverKey))
            {
                SyncAsyncDataSourcesToNode();
            }
        }

        private void SyncAsyncDataSourcesToNode()
        {
            _nodeTyped.AsyncDataSources = AsyncDataSourcesList.Select(x =>
            {
                var actualNodeId = x.ActualSourceNodeId;
                var actualKey = x.ActualOutputKey;
                return new AsyncDataSource
                {
                    SourceNodeId = actualNodeId,
                    SourceOutputKey = actualKey,
                    ReceiverKey = string.IsNullOrWhiteSpace(x.ReceiverKey) ? null : x.ReceiverKey.Trim()
                };
            }).ToList();
        }

        [RelayCommand]
        private void AddAsyncDataSource()
        {
            var item = new AsyncDataSourceItemViewModel();
            item.PropertyChanged += AsyncDataSourceItem_PropertyChanged;
            AsyncDataSourcesList.Add(item);
            SyncAsyncDataSourcesToNode();
        }

        [RelayCommand]
        private void RemoveAsyncDataSource(AsyncDataSourceItemViewModel? item)
        {
            if (item != null && AsyncDataSourcesList.Contains(item))
            {
                item.PropertyChanged -= AsyncDataSourceItem_PropertyChanged;
                AsyncDataSourcesList.Remove(item);
                SyncAsyncDataSourcesToNode();
            }
        }

        public void RefreshAsyncAvailableNodes()
        {
            AsyncAvailableNodeOptions.Clear();
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Connections == null) return;

            foreach (var n in vm.Nodes)
            {
                if (n is not AsyncTaskNode asyncTask) continue;
                if (ReferenceEquals(n, _nodeTyped)) continue;
                AsyncAvailableNodeOptions.Add(CreateDataSourceOption(asyncTask));
            }

            foreach (var ads in AsyncDataSourcesList)
            {
                if (string.IsNullOrWhiteSpace(ads.SourceNodeId)) continue;
                var asyncTaskForItem = FindAsyncTaskContainingBodyNode(ads.SourceNodeId);
                if (asyncTaskForItem != null)
                {
                    ads.Tag_AsyncTaskId = asyncTaskForItem.Id;
                    if (!AsyncAvailableNodeOptions.Any(o => string.Equals(o.NodeId, asyncTaskForItem.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        AsyncAvailableNodeOptions.Add(CreateDataSourceOption(asyncTaskForItem));
                    }
                }
            }
        }

        private AsyncTaskNode? FindAsyncTaskContainingBodyNode(string bodyNodeId)
        {
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Connections == null) return null;

            foreach (var n in vm.Nodes)
            {
                if (n is not AsyncTaskNode asyncTask) continue;
                var bodyNode = asyncTask.AsyncTaskBodyNode;
                if (bodyNode == null) continue;

                var bodyNodes = GetAllBodyOutputNodes(asyncTask);
                if (bodyNodes.Any(bn => string.Equals(bn.Id, bodyNodeId, StringComparison.OrdinalIgnoreCase)))
                    return asyncTask;
            }
            return null;
        }

        private List<WorkflowNode> GetAllBodyOutputNodes(AsyncTaskNode asyncTask)
        {
            var result = new List<WorkflowNode>();
            var vm = _host.ViewModel;
            if (vm?.Connections == null) return result;

            var bodyNode = asyncTask.AsyncTaskBodyNode;
            if (bodyNode == null) return result;

            var bodyRightPort = bodyNode.Ports?.FirstOrDefault(p => string.Equals(p.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase));
            if (bodyRightPort == null) return result;

            var returnNodes = vm.Connections
                .Where(c => c.ToNode == bodyNode && c.ToPort != null &&
                           string.Equals(c.ToPort.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase) &&
                           c.FromNode != null)
                .Select(c => c.FromNode!)
                .Distinct()
                .ToList();

            result.AddRange(returnNodes);

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rn in returnNodes) visited.Add(rn.Id);

            void CollectUpstream(WorkflowNode node)
            {
                var inConns = vm.Connections.Where(c => c.ToNode == node && c.FromNode != null && c.FromNode != bodyNode);
                foreach (var ic in inConns)
                {
                    if (ic.FromNode == null || visited.Contains(ic.FromNode.Id)) continue;
                    if (ic.FromNode is AsyncTaskNode) continue;
                    visited.Add(ic.FromNode.Id);
                    if (ic.FromNode.DynamicOutputs != null && ic.FromNode.DynamicOutputs.Count > 0)
                        result.Add(ic.FromNode);
                    CollectUpstream(ic.FromNode);
                }
            }

            foreach (var rn in returnNodes.ToList())
                CollectUpstream(rn);

            return result;
        }

        public void RefreshAsyncOutputKeyOptionsFor(AsyncDataSourceItemViewModel item)
        {
            item.AvailableOutputKeyOptions.Clear();
            var vm = _host.ViewModel;
            if (vm?.Nodes == null) return;

            var asyncTaskId = item.Tag_AsyncTaskId;
            if (string.IsNullOrWhiteSpace(asyncTaskId))
                asyncTaskId = item.SourceNodeId;
            if (string.IsNullOrWhiteSpace(asyncTaskId)) return;

            var asyncTask = vm.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, asyncTaskId, StringComparison.OrdinalIgnoreCase)) as AsyncTaskNode;
            if (asyncTask == null) return;

            var bodyNodes = GetAllBodyOutputNodes(asyncTask);

            foreach (var bodyNode in bodyNodes)
            {
                if (bodyNode.DynamicOutputs == null) continue;
                var nodeTitle = string.IsNullOrWhiteSpace(bodyNode.Title) ? bodyNode.Id : bodyNode.Title;
                foreach (var o in bodyNode.DynamicOutputs)
                {
                    var keyName = o.Key ?? string.Empty;
                    item.AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
                    {
                        Key = $"{bodyNode.Id}|{keyName}",
                        Type = o.OutputType ?? o.ConvertType,
                        DisplayName = $"{nodeTitle} → {o.DisplayName ?? keyName}"
                    });
                }
            }

            if (item.AvailableOutputKeyOptions.Count > 0 &&
                !item.AvailableOutputKeyOptions.Any(k => string.Equals(k.Key, item.SourceOutputKey, StringComparison.Ordinal)))
            {
                item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key;
            }
        }
    }
}
