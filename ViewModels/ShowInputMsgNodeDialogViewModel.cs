using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class ShowInputMsgNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly ShowInputMsgNode _node;
        private bool _isSyncingFromNode;

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<CodeInputMappingItemViewModel> InputMappingsList { get; } = new();
        public ObservableCollection<OutputKeyItemViewModel> OutputKeysList { get; } = new();
        public ObservableCollection<HtmlOfflineAssetItemViewModel> OfflineAssetsList { get; } = new();
        public ObservableCollection<AsyncDataSourceItemViewModel> AsyncDataSourcesList { get; } = new();
        public ObservableCollection<WorkflowDataSourceOption> AsyncAvailableNodeOptions { get; } = new();

        public System.Collections.Generic.List<string> AutoRefreshUnitOptions { get; } = new() { "ms", "s", "min" };

        [ObservableProperty]
        private string _htmlCode = string.Empty;

        [ObservableProperty]
        private string _jsCode = string.Empty;

        [ObservableProperty]
        private string _cssCode = string.Empty;

        [ObservableProperty]
        private string _paramsCode = string.Empty;

        [ObservableProperty]
        private double _width = 450;

        [ObservableProperty]
        private double _height = 350;

        [ObservableProperty]
        private int _codeFontSize = 13;

        private void HookOfflineAssetsList()
        {
            OfflineAssetsList.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (HtmlOfflineAssetItemViewModel item in e.NewItems)
                        item.PropertyChanged += OfflineAssetItem_PropertyChanged;
                if (e.OldItems != null)
                    foreach (HtmlOfflineAssetItemViewModel item in e.OldItems)
                        item.PropertyChanged -= OfflineAssetItem_PropertyChanged;
                SyncOfflineAssetsToNode();
            };
        }

        private void OfflineAssetItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(HtmlOfflineAssetItemViewModel.IsEnabled)
                or nameof(HtmlOfflineAssetItemViewModel.Title)
                or nameof(HtmlOfflineAssetItemViewModel.Description)
                or nameof(HtmlOfflineAssetItemViewModel.SourceUrl)
                or nameof(HtmlOfflineAssetItemViewModel.LocalFileName)
                or nameof(HtmlOfflineAssetItemViewModel.AssetType))
            {
                SyncOfflineAssetsToNode();
            }
        }

        public ShowInputMsgNodeDialogViewModel(ShowInputMsgNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));

            _htmlCode = node.HtmlCode ?? string.Empty;
            _jsCode = node.JsCode ?? string.Empty;
            _cssCode = node.CssCode ?? string.Empty;
            _paramsCode = node.ParamsCode ?? string.Empty;
            _width = node.Width;
            _height = node.Height;

            var mappings = node.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
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

            foreach (var k in node.OutputKeys)
                OutputKeysList.Add(new OutputKeyItemViewModel { Key = k });

            // Load offline assets từ global list
            var offlineAssets = HtmlOfflineAssetService.LoadGlobalAssets() ?? new List<HtmlOfflineAsset>();

            // Đồng bộ ngược từ node.OfflineAssets vào global list nếu có asset mới chưa đăng ký
            var nodeAssets = node.OfflineAssets ?? new List<HtmlOfflineAsset>();
            bool hasNewGlobalAsset = false;
            foreach (var na in nodeAssets)
            {
                if (!offlineAssets.Any(x => string.Equals(x.LocalFileName, na.LocalFileName, StringComparison.OrdinalIgnoreCase)
                                           || (!string.IsNullOrWhiteSpace(x.SourceUrl) && string.Equals(x.SourceUrl, na.SourceUrl, StringComparison.OrdinalIgnoreCase))))
                {
                    var globalAsset = new HtmlOfflineAsset
                    {
                        Id = na.Id,
                        Title = na.Title,
                        Description = na.Description,
                        SourceUrl = na.SourceUrl,
                        LocalFileName = na.LocalFileName,
                        AssetType = na.AssetType,
                        IsEnabled = false
                    };
                    offlineAssets.Add(globalAsset);
                    hasNewGlobalAsset = true;
                }
            }
            if (hasNewGlobalAsset)
            {
                HtmlOfflineAssetService.SaveGlobalAssets(offlineAssets);
            }

            foreach (var a in offlineAssets)
            {
                var vm = HtmlOfflineAssetItemViewModel.FromModel(a);
                // Quyết định trạng thái checked dựa theo node.OfflineAssets
                var nodeAsset = nodeAssets.FirstOrDefault(x => string.Equals(x.LocalFileName, a.LocalFileName, StringComparison.OrdinalIgnoreCase)
                                                               || (!string.IsNullOrWhiteSpace(x.SourceUrl) && string.Equals(x.SourceUrl, a.SourceUrl, StringComparison.OrdinalIgnoreCase)));
                if (nodeAsset != null)
                {
                    vm.IsEnabled = nodeAsset.IsEnabled;
                }
                else
                {
                    vm.IsEnabled = false;
                }
                vm.PropertyChanged += OfflineAssetItem_PropertyChanged;
                OfflineAssetsList.Add(vm);
            }

            HookOfflineAssetsList();

            // Load async data sources từ node
            foreach (var ads in node.AsyncDataSources ?? new List<AsyncDataSource>())
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
                    item.SourceNodeId = asyncTaskParent.Id; // combo binding
                }
                else
                {
                    item.SourceNodeId = bodyNodeId; // fallback
                }

                item.PropertyChanged += AsyncDataSourceItem_PropertyChanged;
                AsyncDataSourcesList.Add(item);
                RefreshAsyncOutputKeyOptionsFor(item);

                // Set compound key SAU khi refresh options để không bị auto-reset
                item.SourceOutputKey = compoundKey;
            }

            RefreshAvailableNodes();
            RefreshAsyncAvailableNodes();

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) => OnNodePropertyChanged(e.PropertyName ?? string.Empty);
            }
        }

        protected override string GetDefaultTitle() => "Nhập dữ liệu";

        partial void OnHtmlCodeChanged(string value)
        {
            if (_node != null)
                _node.HtmlCode = value ?? string.Empty;
        }

        partial void OnJsCodeChanged(string value)
        {
            if (_node != null)
                _node.JsCode = value ?? string.Empty;
        }

        partial void OnCssCodeChanged(string value)
        {
            if (_node != null)
                _node.CssCode = value ?? string.Empty;
        }

        partial void OnParamsCodeChanged(string value)
        {
            if (_node != null)
                _node.ParamsCode = value ?? string.Empty;
        }

        partial void OnWidthChanged(double value)
        {
            if (_node != null)
                _node.Width = value;
        }

        partial void OnHeightChanged(double value)
        {
            if (_node != null)
                _node.Height = value;
        }

        protected override void OnSaveTitle()
        {
            _node.HtmlCode = HtmlCode ?? string.Empty;
            _node.JsCode = JsCode ?? string.Empty;
            _node.CssCode = CssCode ?? string.Empty;
            _node.ParamsCode = ParamsCode ?? string.Empty;
            _node.Width = Width;
            _node.Height = Height;

            SyncInputMappingsToNode();
            SyncOutputKeysToNode();
            SyncOfflineAssetsToNode();
            SyncAsyncDataSourcesToNode();
            _node.NotifyTitleChanged();
        }

        private void InputMappingItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromNode) return;
            if (sender is not CodeInputMappingItemViewModel item) return;

            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.SourceNodeId))
            {
                RefreshOutputKeyOptionsFor(item);
                return;
            }

            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.SourceOutputKey))
            {
                if (!string.IsNullOrWhiteSpace(item.SourceOutputKey) && string.IsNullOrWhiteSpace(item.InputKeyOverride))
                    item.InputKeyOverride = item.SourceOutputKey.Trim();
                return;
            }

            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.AutoRefreshEnabled) ||
                e.PropertyName == nameof(CodeInputMappingItemViewModel.AutoRefreshInterval) ||
                e.PropertyName == nameof(CodeInputMappingItemViewModel.AutoRefreshUnit))
            {
                SyncInputMappingsToNode();
            }
        }

        private void SyncInputMappingsToNode()
        {
            _node.InputMappings = InputMappingsList.Select(x => new CodeInputMapping
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
            _node.OutputKeys = OutputKeysList
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .Select(x => x.Key.Trim())
                .Distinct()
                .ToList();
        }

        private void SyncOfflineAssetsToNode()
        {
            var assets = OfflineAssetsList.Select(x => x.ToModel()).ToList();
            _node.OfflineAssets = assets;
            HtmlOfflineAssetService.SaveGlobalAssets(assets);
        }

        private void OnNodePropertyChanged(string propertyName)
        {
            if (propertyName == nameof(ShowInputMsgNode.HtmlCode)) HtmlCode = _node.HtmlCode ?? string.Empty;
            if (propertyName == nameof(ShowInputMsgNode.JsCode)) JsCode = _node.JsCode ?? string.Empty;
            if (propertyName == nameof(ShowInputMsgNode.CssCode)) CssCode = _node.CssCode ?? string.Empty;
            if (propertyName == nameof(ShowInputMsgNode.ParamsCode)) ParamsCode = _node.ParamsCode ?? string.Empty;
            if (propertyName == nameof(ShowInputMsgNode.Width)) Width = _node.Width;
            if (propertyName == nameof(ShowInputMsgNode.Height)) Height = _node.Height;
            if (propertyName == nameof(ShowInputMsgNode.InputMappings))
            {
                _isSyncingFromNode = true;
                try
                {
                    var mappings = _node.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
                    while (InputMappingsList.Count > mappings.Count && InputMappingsList.Count > 1)
                        RemoveInputMapping(InputMappingsList[InputMappingsList.Count - 1]);

                    for (var i = 0; i < mappings.Count; i++)
                    {
                        var m = mappings[i];
                        if (i < InputMappingsList.Count)
                        {
                            InputMappingsList[i].SourceNodeId = m.SourceNodeId;
                            InputMappingsList[i].SourceOutputKey = m.SourceOutputKey;
                            InputMappingsList[i].InputKeyOverride = m.InputKeyOverride ?? string.Empty;
                            InputMappingsList[i].AutoRefreshEnabled = m.AutoRefreshEnabled;
                            InputMappingsList[i].AutoRefreshInterval = m.AutoRefreshInterval;
                            InputMappingsList[i].AutoRefreshUnit = m.AutoRefreshUnit ?? "ms";
                        }
                        else
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
                            RefreshOutputKeyOptionsFor(InputMappingsList[i]);
                        }
                    }
                }
                finally
                {
                    _isSyncingFromNode = false;
                }
            }
        }

        public void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Connections == null) return;

            var inputPort = _node.Ports?.FirstOrDefault(p => p.IsInput);
            var connectedNodeIds = vm.Connections
                .Where(c => c.ToNode == _node && c.FromNode != null &&
                            (inputPort == null || c.ToPort == inputPort || (c.ToPort != null && c.ToPort.IsInput)))
                .Select(c => c.FromNode!.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var n in vm.Nodes)
            {
                if (ReferenceEquals(n, _node)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                if (!connectedNodeIds.Contains(n.Id)) continue;
                AvailableNodeOptions.Add(CreateDataSourceOption(n));
            }
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
        }

        [RelayCommand]
        private void RemoveInputMapping(CodeInputMappingItemViewModel? item)
        {
            if (item != null && InputMappingsList.Contains(item) && InputMappingsList.Count > 1)
            {
                item.PropertyChanged -= InputMappingItem_PropertyChanged;
                InputMappingsList.Remove(item);
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
                if (line.StartsWith("#", StringComparison.Ordinal)) continue;

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

        // ─────────────────────────────────────────────────────────────────────
        // Offline Assets
        // ─────────────────────────────────────────────────────────────────────

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

        /// <summary>Thêm nhanh từ preset (Chart.js, Moment.js,...)</summary>
        [RelayCommand]
        private void AddOfflineAssetFromPreset(AssetPreset? preset)
        {
            if (preset == null) return;
            // Kiểm tra đã có trùng URL chưa
            if (OfflineAssetsList.Any(a =>
                string.Equals(a.SourceUrl, preset.Url, StringComparison.OrdinalIgnoreCase))) return;

            OfflineAssetsList.Add(new HtmlOfflineAssetItemViewModel
            {
                Title = preset.Title,
                Description = preset.Description,
                SourceUrl = preset.Url,
                LocalFileName = preset.FileName,
                AssetType = preset.Type,
                IsEnabled = true,
                StatusMessage = FlowMy.Services.Utils.HtmlOfflineAssetService
                    .AssetExists(preset.FileName) ? "✓ Có sẵn" : "✗ Chưa tải về"
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Async Data Sources (API-style push từ AsyncTask)
        // ─────────────────────────────────────────────────────────────────────

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
            _node.AsyncDataSources = AsyncDataSourcesList.Select(x =>
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
                if (ReferenceEquals(n, _node)) continue;
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
                var firstKey = item.AvailableOutputKeyOptions[0].Key;
                item.SourceOutputKey = firstKey;
            }
        }
    }
}
