using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    /// <summary>ViewModel cho một dòng async data source (Node + Key + Key nhận) trong HTML UI dialog.</summary>
    public partial class AsyncDataSourceItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? _sourceNodeId;

        [ObservableProperty]
        private string? _sourceOutputKey;

        [ObservableProperty]
        private string _receiverKey = string.Empty;

        /// <summary>Lưu AsyncTask ID được chọn trong combo (khác với SourceNodeId là body node ID).</summary>
        public string? Tag_AsyncTaskId { get; set; }

        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

        /// <summary>Phân tích compound key "nodeId|outputKey" → chỉ lấy phần outputKey.</summary>
        public string ActualOutputKey
        {
            get
            {
                var key = SourceOutputKey ?? string.Empty;
                var idx = key.IndexOf('|');
                return idx >= 0 ? key.Substring(idx + 1) : key;
            }
        }

        /// <summary>Phân tích compound key "nodeId|outputKey" → lấy phần nodeId.</summary>
        public string ActualSourceNodeId
        {
            get
            {
                var key = SourceOutputKey ?? string.Empty;
                var idx = key.IndexOf('|');
                return idx >= 0 ? key.Substring(0, idx) : (SourceNodeId ?? string.Empty);
            }
        }

        /// <summary>Key thực tế sẽ dùng trong JS: ReceiverKey > ActualOutputKey > "asyncData".</summary>
        public string EffectiveReceiverKeyDisplay =>
            !string.IsNullOrWhiteSpace(ReceiverKey) ? ReceiverKey.Trim()
            : (string.IsNullOrWhiteSpace(ActualOutputKey) ? "asyncData" : ActualOutputKey.Trim());

        partial void OnSourceOutputKeyChanged(string? value) => OnPropertyChanged(nameof(EffectiveReceiverKeyDisplay));
        partial void OnReceiverKeyChanged(string value) => OnPropertyChanged(nameof(EffectiveReceiverKeyDisplay));
    }

    /// <summary>ViewModel cho dialog cấu hình HtmlUiNode (inputs/outputs + 4 tab Html/Js/Css/Params).</summary>
    public partial class HtmlUiNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly HtmlUiNode _node;
        private bool _isSyncingFromNode;

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<CodeInputMappingItemViewModel> InputMappingsList { get; } = new();
        public ObservableCollection<OutputKeyItemViewModel> OutputKeysList { get; } = new();

        /// <summary>Danh sách offline assets (JS/CSS thư viện). Bind vào tab "📦 Thư viện offline".</summary>
        public ObservableCollection<HtmlOfflineAssetItemViewModel> OfflineAssetsList { get; } = new();

        /// <summary>Danh sách nguồn dữ liệu async (kiểu API push từ AsyncTask). Bind vào tab "📡 Async Data Receiver".</summary>
        public ObservableCollection<AsyncDataSourceItemViewModel> AsyncDataSourcesList { get; } = new();

        /// <summary>Danh sách node available cho Async Data Sources (chỉ node connected).</summary>
        public ObservableCollection<WorkflowDataSourceOption> AsyncAvailableNodeOptions { get; } = new();

        // ✅ Auto-sync về node khi collection thay đổi (add/remove) hoặc khi item property thay đổi
        private void HookOfflineAssetsList()
        {
            OfflineAssetsList.CollectionChanged += (_, e) =>
            {
                // subscribe PropertyChanged cho items mới
                if (e.NewItems != null)
                    foreach (HtmlOfflineAssetItemViewModel item in e.NewItems)
                        item.PropertyChanged += OfflineAssetItem_PropertyChanged;
                // unsubscribe khi remove
                if (e.OldItems != null)
                    foreach (HtmlOfflineAssetItemViewModel item in e.OldItems)
                        item.PropertyChanged -= OfflineAssetItem_PropertyChanged;
                SyncOfflineAssetsToNode();
            };
        }

        private void OfflineAssetItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Chỉ sync lại model khi các property cần persist thay đổi
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

        /// <summary>Tùy chọn đơn vị thời gian cho auto-refresh ComboBox.</summary>
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
        private int _codeFontSize = 13;

        /// <summary>
        /// Checkbox: khi bật thì đóng dialog sẽ tự reload lại WebView2 cho HtmlUiNode này.
        /// </summary>
        [ObservableProperty]
        private bool _autoReloadOnDialogClose;

        [ObservableProperty]
        private bool _enableSleepMode = true;

        [ObservableProperty]
        private int _sleepIdleTimeoutValue = 5;

        [ObservableProperty]
        private string _sleepIdleTimeoutUnit = "s";

        public System.Collections.Generic.List<string> SleepIdleTimeoutUnitOptions { get; } = new() { "ms", "s", "min", "phút" };

        /// <summary>Bật/tắt 2-tab mode (Tab1=Web, Tab2=HTML UI).</summary>
        [ObservableProperty]
        private bool _useWebTab;

        /// <summary>Cookie text để load vào Tab1. Runtime only, không persist.</summary>
        [ObservableProperty]
        private string _cookieText = string.Empty;

        /// <summary>Node nguồn cung cấp cookie cho Tab1.</summary>
        [ObservableProperty]
        private string? _webTabCookieSourceNodeId;

        /// <summary>Output key của node nguồn cookie.</summary>
        [ObservableProperty]
        private string? _webTabCookieSourceOutputKey;

        /// <summary>Bật tự động lấy cookie từ source node theo kỳ.</summary>
        [ObservableProperty]
        private bool _webTabAutoRefreshEnabled;

        /// <summary>Khoảng thời gian auto-refresh (theo đơn vị WebTabAutoRefreshUnit).</summary>
        [ObservableProperty]
        private int _webTabAutoRefreshInterval = 5000;

        /// <summary>Đơn vị: ms / s / min.</summary>
        [ObservableProperty]
        private string _webTabAutoRefreshUnit = "ms";

        /// <summary>Output keys available from cookie source node.</summary>
        public ObservableCollection<WorkflowOutputKeyOption> WebTabCookieSourceKeyOptions { get; } = new();

        public HtmlUiNodeDialogViewModel(HtmlUiNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));

            _htmlCode = node.HtmlCode ?? string.Empty;
            _jsCode = node.JsCode ?? string.Empty;
            _cssCode = node.CssCode ?? string.Empty;
            _paramsCode = node.ParamsCode ?? string.Empty;
            _autoReloadOnDialogClose = node.AutoReloadOnDialogClose;
            _enableSleepMode = node.EnableSleepMode;
            _sleepIdleTimeoutValue = node.SleepIdleTimeoutValue;
            _sleepIdleTimeoutUnit = node.SleepIdleTimeoutUnit ?? "s";
            _useWebTab = node.UseWebTab;
            _cookieText = string.Empty;
            _webTabCookieSourceNodeId = node.WebTabCookieSourceNodeId;
            _webTabCookieSourceOutputKey = node.WebTabCookieSourceOutputKey;
            _webTabAutoRefreshEnabled = node.WebTabAutoRefreshEnabled;
            _webTabAutoRefreshInterval = node.WebTabAutoRefreshInterval;
            _webTabAutoRefreshUnit = node.WebTabAutoRefreshUnit ?? "ms";

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

            // Load offline assets từ node
            foreach (var a in node.OfflineAssets ?? new List<HtmlOfflineAsset>())
            {
                var vm = HtmlOfflineAssetItemViewModel.FromModel(a);
                vm.PropertyChanged += OfflineAssetItem_PropertyChanged;
                OfflineAssetsList.Add(vm);
            }

            // Hook collection changes sau khi load xong (tránh sync trong lúc init)
            HookOfflineAssetsList();

            // Load async data sources từ node
            foreach (var ads in node.AsyncDataSources ?? new List<AsyncDataSource>())
            {
                // Model lưu: SourceNodeId = body node ID, SourceOutputKey = plain key
                // UI cần: SourceNodeId = AsyncTask ID (cho combo), SourceOutputKey = compound "bodyNodeId|plainKey"
                var bodyNodeId = ads.SourceNodeId ?? string.Empty;
                var plainKey = ads.SourceOutputKey ?? string.Empty;
                var compoundKey = string.IsNullOrWhiteSpace(bodyNodeId) ? plainKey : $"{bodyNodeId}|{plainKey}";

                var item = new AsyncDataSourceItemViewModel
                {
                    ReceiverKey = ads.ReceiverKey ?? string.Empty
                };

                // Tìm AsyncTask parent chứa body node này
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
            RefreshWebTabCookieKeyOptions();

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) => OnNodePropertyChanged(e.PropertyName ?? string.Empty);
            }
        }

        protected override string GetDefaultTitle() => "HTML UI";

        // ✅ Lưu trực tiếp vào node mỗi khi code thay đổi (giống CodeNodeDialogViewModel)
        // Đảm bảo nội dung được lưu ngay cả khi binding với UpdateSourceTrigger=LostFocus không được trigger
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

        protected override void OnSaveTitle()
        {
            // ✅ Code đã được lưu tự động trong OnXxxCodeChanged khi binding update
            // Chỉ cần đảm bảo code cuối cùng được sync (nếu binding chưa update)
            // và lưu các thông tin khác
            
            // Đảm bảo code cuối cùng được sync (nếu binding chưa update do UpdateSourceTrigger=LostFocus)
            _node.HtmlCode = HtmlCode ?? string.Empty;
            _node.JsCode = JsCode ?? string.Empty;
            _node.CssCode = CssCode ?? string.Empty;
            _node.ParamsCode = ParamsCode ?? string.Empty;

            // Lưu trạng thái auto reload
            _node.AutoReloadOnDialogClose = AutoReloadOnDialogClose;
            _node.EnableSleepMode = EnableSleepMode;
            _node.SleepIdleTimeoutValue = SleepIdleTimeoutValue;
            _node.SleepIdleTimeoutUnit = SleepIdleTimeoutUnit ?? "s";
            _node.UseWebTab = UseWebTab;
            _node.WebTabCookieSourceNodeId = WebTabCookieSourceNodeId;
            _node.WebTabCookieSourceOutputKey = WebTabCookieSourceOutputKey;
            _node.WebTabAutoRefreshEnabled = WebTabAutoRefreshEnabled;
            _node.WebTabAutoRefreshInterval = WebTabAutoRefreshInterval;
            _node.WebTabAutoRefreshUnit = WebTabAutoRefreshUnit ?? "ms";
            // CookieText không lưu vào node - dialog sẽ xử lý khi đóng

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
                OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
                OnPropertyChanged(nameof(FirstInputVariableName));
                return;
            }

            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.SourceOutputKey))
            {
                if (!string.IsNullOrWhiteSpace(item.SourceOutputKey) && string.IsNullOrWhiteSpace(item.InputKeyOverride))
                    item.InputKeyOverride = item.SourceOutputKey.Trim();
                OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
                OnPropertyChanged(nameof(FirstInputVariableName));
                return;
            }

            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.InputKeyOverride))
            {
                OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
                OnPropertyChanged(nameof(FirstInputVariableName));
            }

            // Sync ngay về node khi user thay đổi auto-refresh settings (không chờ dialog đóng)
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

        private void OnNodePropertyChanged(string propertyName)
        {
            if (propertyName == nameof(HtmlUiNode.HtmlCode)) HtmlCode = _node.HtmlCode ?? string.Empty;
            if (propertyName == nameof(HtmlUiNode.JsCode)) JsCode = _node.JsCode ?? string.Empty;
            if (propertyName == nameof(HtmlUiNode.CssCode)) CssCode = _node.CssCode ?? string.Empty;
            if (propertyName == nameof(HtmlUiNode.ParamsCode)) ParamsCode = _node.ParamsCode ?? string.Empty;
            if (propertyName == nameof(HtmlUiNode.EnableSleepMode)) EnableSleepMode = _node.EnableSleepMode;
            if (propertyName == nameof(HtmlUiNode.SleepIdleTimeoutValue)) SleepIdleTimeoutValue = _node.SleepIdleTimeoutValue;
            if (propertyName == nameof(HtmlUiNode.SleepIdleTimeoutUnit)) SleepIdleTimeoutUnit = _node.SleepIdleTimeoutUnit ?? "s";
            if (propertyName == nameof(HtmlUiNode.InputMappings))
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
                    OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
                    OnPropertyChanged(nameof(FirstInputVariableName));
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
                AvailableNodeOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
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

        public void RefreshWebTabCookieKeyOptions()
        {
            WebTabCookieSourceKeyOptions.Clear();
            if (string.IsNullOrWhiteSpace(WebTabCookieSourceNodeId) || _host.ViewModel?.Nodes == null) return;

            var sourceNode = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, WebTabCookieSourceNodeId, StringComparison.OrdinalIgnoreCase));
            if (sourceNode?.DynamicOutputs == null) return;

            foreach (var o in sourceNode.DynamicOutputs)
            {
                WebTabCookieSourceKeyOptions.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key ?? string.Empty,
                    Type = o.OutputType ?? o.ConvertType,
                    DisplayName = o.DisplayName ?? o.Key
                });
            }
        }

        partial void OnWebTabCookieSourceNodeIdChanged(string? value)
        {
            RefreshWebTabCookieKeyOptions();
        }

        [RelayCommand]
        private void AddInputMapping()
        {
            var item = new CodeInputMappingItemViewModel();
            item.PropertyChanged += InputMappingItem_PropertyChanged;
            InputMappingsList.Add(item);
            OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
            OnPropertyChanged(nameof(FirstInputVariableName));
        }

        [RelayCommand]
        private void RemoveInputMapping(CodeInputMappingItemViewModel? item)
        {
            if (item != null && InputMappingsList.Contains(item) && InputMappingsList.Count > 1)
            {
                item.PropertyChanged -= InputMappingItem_PropertyChanged;
                InputMappingsList.Remove(item);
                OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
                OnPropertyChanged(nameof(FirstInputVariableName));
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

                // Bỏ qua các key có khoảng trắng để tránh key lỗi do user nhập sai.
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
        // Async Data Sources (API-style push từ AsyncTask)
        // ─────────────────────────────────────────────────────────────────────

        private void AsyncDataSourceItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromNode) return;
            if (sender is not AsyncDataSourceItemViewModel item) return;

            if (e.PropertyName == nameof(AsyncDataSourceItemViewModel.SourceNodeId))
            {
                // SourceNodeId từ combo = AsyncTask ID → lưu làm Tag
                item.Tag_AsyncTaskId = item.SourceNodeId;
                RefreshAsyncOutputKeyOptionsFor(item);
                SyncAsyncDataSourcesToNode();
                return;
            }

            if (e.PropertyName == nameof(AsyncDataSourceItemViewModel.SourceOutputKey))
            {
                // Tự điền ReceiverKey nếu trống (dùng actual output key, không dùng compound)
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
                // SourceOutputKey có thể là compound key "nodeId|outputKey"
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

        /// <summary>Refresh danh sách node available cho Async Data Sources — chỉ hiện AsyncTask nodes.</summary>
        public void RefreshAsyncAvailableNodes()
        {
            AsyncAvailableNodeOptions.Clear();
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Connections == null) return;

            // Chỉ hiện AsyncTask nodes trong workflow
            foreach (var n in vm.Nodes)
            {
                if (n is not AsyncTaskNode asyncTask) continue;
                if (ReferenceEquals(n, _node)) continue;
                AsyncAvailableNodeOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
            }

            // Đảm bảo các node đã chọn trong AsyncDataSources cũng nằm trong danh sách
            foreach (var ads in AsyncDataSourcesList)
            {
                if (string.IsNullOrWhiteSpace(ads.SourceNodeId)) continue;
                // SourceNodeId lưu ID body node → tìm AsyncTask parent để hiện trong combo
                var asyncTaskForItem = FindAsyncTaskContainingBodyNode(ads.SourceNodeId);
                if (asyncTaskForItem != null)
                {
                    ads.Tag_AsyncTaskId = asyncTaskForItem.Id;
                    if (!AsyncAvailableNodeOptions.Any(o => string.Equals(o.NodeId, asyncTaskForItem.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        AsyncAvailableNodeOptions.Add(new WorkflowDataSourceOption
                        {
                            NodeId = asyncTaskForItem.Id,
                            Title = string.IsNullOrWhiteSpace(asyncTaskForItem.Title) ? asyncTaskForItem.Id : asyncTaskForItem.Title
                        });
                    }
                }
            }
        }

        /// <summary>Tìm AsyncTask node chứa body node có ID đã cho.</summary>
        private AsyncTaskNode? FindAsyncTaskContainingBodyNode(string bodyNodeId)
        {
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Connections == null) return null;

            foreach (var n in vm.Nodes)
            {
                if (n is not AsyncTaskNode asyncTask) continue;
                var bodyNode = asyncTask.AsyncTaskBodyNode;
                if (bodyNode == null) continue;

                // Kiểm tra node nằm trong body (connected từ body)
                var bodyNodes = GetAllBodyOutputNodes(asyncTask);
                if (bodyNodes.Any(bn => string.Equals(bn.Id, bodyNodeId, StringComparison.OrdinalIgnoreCase)))
                    return asyncTask;
            }
            return null;
        }

        /// <summary>Lấy tất cả output nodes trong body của AsyncTask (các node cuối nối vào LoopBodyRight).</summary>
        private List<WorkflowNode> GetAllBodyOutputNodes(AsyncTaskNode asyncTask)
        {
            var result = new List<WorkflowNode>();
            var vm = _host.ViewModel;
            if (vm?.Connections == null) return result;

            var bodyNode = asyncTask.AsyncTaskBodyNode;
            if (bodyNode == null) return result;

            // Tìm node cuối cùng nối về LoopBodyRight (return path)
            var bodyRightPort = bodyNode.Ports?.FirstOrDefault(p => string.Equals(p.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase));
            if (bodyRightPort == null) return result;

            // Các node nối vào LoopBodyRight
            var returnNodes = vm.Connections
                .Where(c => c.ToNode == bodyNode && c.ToPort != null &&
                           string.Equals(c.ToPort.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase) &&
                           c.FromNode != null)
                .Select(c => c.FromNode!)
                .Distinct()
                .ToList();

            result.AddRange(returnNodes);

            // Thêm các node khác trong body chain (nối từ LoopBodyLeft hoặc LoopBodyTop)
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rn in returnNodes) visited.Add(rn.Id);

            void CollectUpstream(WorkflowNode node)
            {
                var inConns = vm.Connections.Where(c => c.ToNode == node && c.FromNode != null && c.FromNode != bodyNode);
                foreach (var ic in inConns)
                {
                    if (ic.FromNode == null || visited.Contains(ic.FromNode.Id)) continue;
                    if (ic.FromNode is AsyncTaskNode) continue; // đừng đi ngược ra ngoài body
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

            // Tìm AsyncTask được chọn (SourceNodeId có thể là AsyncTask ID hoặc body node ID)
            var asyncTaskId = item.Tag_AsyncTaskId;
            if (string.IsNullOrWhiteSpace(asyncTaskId))
                asyncTaskId = item.SourceNodeId;
            if (string.IsNullOrWhiteSpace(asyncTaskId)) return;

            var asyncTask = vm.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, asyncTaskId, StringComparison.OrdinalIgnoreCase)) as AsyncTaskNode;
            if (asyncTask == null) return;

            // Lấy tất cả body output nodes
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
                        Key = $"{bodyNode.Id}|{keyName}", // compound key: nodeId|outputKey
                        Type = o.OutputType ?? o.ConvertType,
                        DisplayName = $"{nodeTitle} → {o.DisplayName ?? keyName}"
                    });
                }
            }

            if (item.AvailableOutputKeyOptions.Count > 0 &&
                !item.AvailableOutputKeyOptions.Any(k => string.Equals(k.Key, item.SourceOutputKey, StringComparison.Ordinal)))
            {
                // Tự động chọn key đầu tiên
                var firstKey = item.AvailableOutputKeyOptions[0].Key;
                item.SourceOutputKey = firstKey;
            }
        }

        /// <summary>Hiển thị danh sách key nhận async (dùng cho hướng dẫn).</summary>
        public string AsyncReceiverKeysDisplay => AsyncDataSourcesList.Count > 0
            ? string.Join(", ", AsyncDataSourcesList.Select(x => x.EffectiveReceiverKeyDisplay))
            : "(chưa cấu hình)";

        /// <summary>Hiển thị tên key đầu tiên (ví dụ trong guide).</summary>
        public string FirstAsyncReceiverKey => AsyncDataSourcesList.Count > 0
            ? AsyncDataSourcesList[0].EffectiveReceiverKeyDisplay
            : "myAsyncKey";

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

        private void SyncOfflineAssetsToNode()
        {
            _node.OfflineAssets = OfflineAssetsList
                .Select(x => x.ToModel())
                .ToList();
        }

        /// <summary>Apply cookie text vào Tab1 ngay lập tức (giống WebNodeDialog's ▶ Chạy).</summary>
        [RelayCommand]
        private void ApplyCookie()
        {
            if (string.IsNullOrWhiteSpace(CookieText)) return;
            if (_node == null) return;

            // Đảm bảo UseWebTab = true trên node → trigger RebuildMiddleContent → tạo Tab1
            // Set trực tiếp lên _node để fire PropertyChanged (không chờ dialog đóng)
            if (!_node.UseWebTab)
            {
                _node.UseWebTab = true;
                UseWebTab = true; // Sync ViewModel UI
            }

            // Set PendingCookieText → fires PropertyChanged → HtmlUiNodeControl handler:
            //  - Nếu Tab1 đã sẵn sàng: apply ngay
            //  - Nếu Tab1 đang khởi tạo (có thể vì UseWebTab vừa được set): giữ PendingCookieText
            //    → EnsureCoreWebView2Async completion sẽ pick up và apply (line ~1130)
            _node.PendingCookieText = CookieText;
        }

        /// <summary>Hiển thị danh sách tên biến input (dùng cho hướng dẫn).</summary>
        public string EffectiveInputKeyDisplay => string.Join(", ", InputMappingsList.Select(x => x.EffectiveInputKeyDisplay));

        /// <summary>Tên biến input đầu tiên (dùng cho ví dụ trong hướng dẫn).</summary>
        public string FirstInputVariableName => InputMappingsList.Count > 0 ? InputMappingsList[0].EffectiveInputKeyDisplay : "input";
    }
}

