using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FlowMy.ViewModels
{
    /// <summary>ViewModel cho một dòng JS source (Node + Key) – khi node đó chạy đến Web thì chạy JS.</summary>
    public partial class WebJsSourceItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? _sourceNodeId;

        [ObservableProperty]
        private string? _sourceOutputKey;

        /// <summary>Bật/tắt chế độ tự động chạy JS theo chu kỳ timer.</summary>
        [ObservableProperty]
        private bool _autoTimerEnabled;

        /// <summary>Giá trị khoảng thời gian timer.</summary>
        [ObservableProperty]
        private double _autoTimerIntervalValue = 30;

        /// <summary>Đơn vị thời gian: "ms", "s", "phút".</summary>
        [ObservableProperty]
        private string _autoTimerIntervalUnit = "s";

        /// <summary>Options cho ComboBox đơn vị thời gian của timer.</summary>
        public List<string> AutoTimerUnitOptions { get; } = new() { "ms", "s", "phút" };

        /// <summary>Mỗi item có ItemsSource riêng để tránh lỗi WPF: nhiều ComboBox dùng chung ItemsSource → cùng hiển thị 1 giá trị.</summary>
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();
    }

    /// <summary>ViewModel cho một dòng input (Node + Key + Key tùy chỉnh) trong Web node.</summary>
    public partial class WebInputMappingItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? _sourceNodeId;

        [ObservableProperty]
        private string? _sourceOutputKey;

        [ObservableProperty]
        private string _inputKeyOverride = string.Empty;

        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

        public string EffectiveInputKeyDisplay => !string.IsNullOrWhiteSpace(InputKeyOverride)
            ? InputKeyOverride.Trim()
            : (string.IsNullOrWhiteSpace(SourceOutputKey) ? "input" : SourceOutputKey.Trim());

        partial void OnSourceOutputKeyChanged(string? value) => OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
        partial void OnInputKeyOverrideChanged(string value) => OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
    }

    public partial class WebNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly WebNode _webNode;
        /// <summary>Đang đồng bộ từ node sang list – tránh gọi SyncInputMappingsToNode() để không gây StackOverflow.</summary>
        private bool _isSyncingFromNode;
        /// <summary>Đang update input mappings từ UI – tránh refresh JS nodes để không làm mất selection.</summary>
        private bool _isUpdatingInputMappings;

        [ObservableProperty]
        private string _extractUrl = "https://google.com";

        [ObservableProperty]
        private string _extractRequestMethod = "GET";

        [ObservableProperty]
        private string _extractStatusCode = "200";

        [ObservableProperty]
        private bool _syncLiveOutputsToResults;

        [ObservableProperty]
        private string? _cookieText;

        /// <summary>
        /// Nếu true: sau khi chặn được ÍT NHẤT MỘT request khớp BlockingRules thì tất cả các request
        /// tiếp theo cũng sẽ bị chặn luôn (trong cùng lần chạy node).
        /// </summary>
        [ObservableProperty]
        private bool _blockAllRequestsAfterFirstMatch;

        /// <summary>
        /// Timeout (ms) cho việc chờ WebView2 populate các ResponseOutputs (theo WaitForCompletion)
        /// trước khi WebNodeExecutor traverse sang các node tiếp theo.
        /// </summary>
        [ObservableProperty]
        private int _responseOutputsWaitTimeoutMs = 15000;

        [ObservableProperty]
        private WebOutputsWaitMode _responseOutputsWaitMode = WebOutputsWaitMode.All;

        [ObservableProperty]
        private bool _autoReloadEnabled;

        [ObservableProperty]
        private bool _enableSleepMode = true;

        [ObservableProperty]
        private int _sleepIdleTimeoutValue = 5;

        [ObservableProperty]
        private string _sleepIdleTimeoutUnit = "s";

        public List<string> SleepIdleTimeoutUnitOptions { get; } = new() { "ms", "s", "min", "phút" };

        [ObservableProperty]
        private double _autoReloadIntervalValue = 30;

        [ObservableProperty]
        private string _autoReloadIntervalUnit = "s";

        /// <summary>Options cho ComboBox đơn vị thời gian auto-reload.</summary>
        public List<string> AutoReloadUnitOptions { get; } = new() { "ms", "s", "phút" };

        // Sync ngay về node khi user thay đổi (không cần chờ OnSaveTitle / dialog đóng)
        partial void OnAutoReloadEnabledChanged(bool value) => _webNode.AutoReloadEnabled = value;
        partial void OnAutoReloadIntervalValueChanged(double value) => _webNode.AutoReloadIntervalValue = value;
        partial void OnAutoReloadIntervalUnitChanged(string value) => _webNode.AutoReloadIntervalUnit = value;
        partial void OnBlockAllRequestsAfterFirstMatchChanged(bool value) => _webNode.BlockAllRequestsAfterFirstMatch = value;

        public ObservableCollection<WebOutputsWaitModeOption> ResponseOutputsWaitModeOptions { get; } = new()
        {
            new WebOutputsWaitModeOption(WebOutputsWaitMode.All, "Đợi tất cả key cần đợi (ALL)"),
            new WebOutputsWaitModeOption(WebOutputsWaitMode.Any, "Chỉ cần 1 key cần đợi xuất hiện (ANY)")
        };

        [RelayCommand]
        private void ApplyCookie()
        {
            // Trigger event để WebNodeControl apply cookie
            _webNode.CookieText = CookieText;
            // Set flag để WebNodeControl biết cần apply cookie ngay
            _webNode.RaisePropertyChanged(nameof(WebNode.CookieText));
        }

        // JS injection: danh sách (Node+Key) – khi node đó chạy đến Web thì chạy JS
        public ObservableCollection<WebJsSourceItemViewModel> JsSourcesList { get; } = new();

        // ⚠️ CRITICAL: Tách collection riêng cho JS Source để tránh conflict với Input mappings
        public ObservableCollection<WorkflowDataSourceOption> JsAvailableNodeOptions { get; } = new();

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WebInputMappingItemViewModel> InputMappingsList { get; } = new();

        /// <summary>Rules thay request (sync với node.RequestInterceptRules).</summary>
        public ObservableCollection<WebRequestInterceptRule> RequestInterceptRules => _webNode.RequestInterceptRules;

        public ObservableCollection<WebBlockingRule> BlockingRules => _webNode.BlockingRules;

        [RelayCommand]
        private void AddBlockingRule()
        {
            BlockingRules.Add(new WebBlockingRule());
        }

        [RelayCommand]
        private void RemoveBlockingRule(WebBlockingRule rule)
        {
            if (rule != null && BlockingRules.Contains(rule))
            {
                BlockingRules.Remove(rule);
            }
        }

        [RelayCommand]
        private void AddChildBlockingRule(WebBlockingRule? parent)
        {
            if (parent == null) return;
            parent.ChildRules.Add(new WebBlockingChildRule());
        }

        [RelayCommand]
        private void RemoveChildBlockingRule(WebBlockingChildRule? child)
        {
            if (child == null) return;
            // Tìm parent chứa child và xóa khỏi đó
            foreach (var rule in BlockingRules)
            {
                if (rule.ChildRules.Contains(child))
                {
                    rule.ChildRules.Remove(child);
                    break;
                }
            }
        }

        public WebNodeDialogViewModel(WebNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _webNode = node ?? throw new ArgumentNullException(nameof(node));
            ExtractUrl = _webNode.ExtractUrl ?? "";
            ExtractRequestMethod = _webNode.ExtractRequestMethod ?? "GET";
            ExtractStatusCode = _webNode.ExtractStatusCode ?? "200";
            SyncLiveOutputsToResults = _webNode.SyncLiveOutputsToResults;
            CookieText = _webNode.CookieText;
            ResponseOutputsWaitTimeoutMs = _webNode.ResponseOutputsWaitTimeoutMs;
            ResponseOutputsWaitMode = _webNode.ResponseOutputsWaitMode;
            BlockAllRequestsAfterFirstMatch = _webNode.BlockAllRequestsAfterFirstMatch;
            AutoReloadEnabled = _webNode.AutoReloadEnabled;
            EnableSleepMode = _webNode.EnableSleepMode;
            SleepIdleTimeoutValue = _webNode.SleepIdleTimeoutValue;
            SleepIdleTimeoutUnit = _webNode.SleepIdleTimeoutUnit ?? "s";
            AutoReloadIntervalValue = _webNode.AutoReloadIntervalValue;
            AutoReloadIntervalUnit = _webNode.AutoReloadIntervalUnit;

            // ⚠️ CRITICAL: Refresh available nodes TRƯỚC KHI load mappings và JS options
            RefreshAvailableNodes();

            // ⚠️ CRITICAL: Refresh JS available nodes (collection riêng) trước
            RefreshJsAvailableNodes();

            // Load JS sources từ node
            var jsSources = _webNode.JsSources ?? new System.Collections.Generic.List<WebJsSourceMapping>();
            foreach (var js in jsSources)
            {
                var item = new WebJsSourceItemViewModel
                {
                    SourceNodeId = js.SourceNodeId,
                    SourceOutputKey = js.SourceOutputKey,
                    AutoTimerEnabled = js.AutoTimerEnabled,
                    AutoTimerIntervalValue = js.AutoTimerIntervalValue,
                    AutoTimerIntervalUnit = js.AutoTimerIntervalUnit
                };
                item.PropertyChanged += JsSourceItem_PropertyChanged;
                RefreshNodeOptionsForJsSourceItem(item);
                RefreshOutputKeyOptionsForJsSource(item);
                JsSourcesList.Add(item);
            }
            if (JsSourcesList.Count == 0)
            {
                var item = new WebJsSourceItemViewModel();
                item.PropertyChanged += JsSourceItem_PropertyChanged;
                RefreshNodeOptionsForJsSourceItem(item);
                JsSourcesList.Add(item);
            }
            // ⚠️ KHÔNG dùng trigger (set null rồi set lại) cho JsSources – gây PropertyChanged → Sync → corrupt data.

            // Load input mappings từ node
            var mappings = _webNode.InputMappings ?? new System.Collections.Generic.List<WebInputMapping>();
            if (mappings.Count == 0) mappings.Add(new WebInputMapping());
            foreach (var m in mappings)
            {
                var item = new WebInputMappingItemViewModel
                {
                    SourceNodeId = m.SourceNodeId,
                    SourceOutputKey = m.SourceOutputKey,
                    InputKeyOverride = m.InputKeyOverride ?? string.Empty
                };
                item.PropertyChanged += InputMappingItem_PropertyChanged;
                InputMappingsList.Add(item);
                // ⚠️ CRITICAL: Refresh output key options SAU KHI đã refresh available nodes
                RefreshOutputKeyOptionsFor(item);
            }
            // Sau khi tất cả bindings đã được thiết lập, trigger lại PropertyChanged cho SourceNodeId/SourceOutputKey
            // để WPF ComboBox SelectedValue nhận được giá trị ban đầu.
            foreach (var item in InputMappingsList)
            {
                if (!string.IsNullOrWhiteSpace(item.SourceNodeId))
                {
                    var sid = item.SourceNodeId;
                    item.SourceNodeId = null;
                    item.SourceNodeId = sid;
                }
                if (!string.IsNullOrWhiteSpace(item.SourceOutputKey))
                {
                    var sk = item.SourceOutputKey;
                    item.SourceOutputKey = null;
                    item.SourceOutputKey = sk;
                }
            }

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) => OnNodePropertyChanged(e.PropertyName ?? "");
            }
        }

        protected override void OnNodePropertyChanged(string propertyName)
        {
            base.OnNodePropertyChanged(propertyName);

            if (propertyName == nameof(WebNode.ExtractUrl)) ExtractUrl = _webNode.ExtractUrl ?? "";
            else if (propertyName == nameof(WebNode.ExtractRequestMethod)) ExtractRequestMethod = _webNode.ExtractRequestMethod ?? "GET";
            else if (propertyName == nameof(WebNode.ExtractStatusCode)) ExtractStatusCode = _webNode.ExtractStatusCode ?? "200";
            else if (propertyName == nameof(WebNode.SyncLiveOutputsToResults)) SyncLiveOutputsToResults = _webNode.SyncLiveOutputsToResults;
            else if (propertyName == nameof(WebNode.CookieText)) CookieText = _webNode.CookieText;
            else if (propertyName == nameof(WebNode.ResponseOutputsWaitTimeoutMs)) ResponseOutputsWaitTimeoutMs = _webNode.ResponseOutputsWaitTimeoutMs;
            else if (propertyName == nameof(WebNode.ResponseOutputsWaitMode)) ResponseOutputsWaitMode = _webNode.ResponseOutputsWaitMode;
            else if (propertyName == nameof(WebNode.BlockAllRequestsAfterFirstMatch)) BlockAllRequestsAfterFirstMatch = _webNode.BlockAllRequestsAfterFirstMatch;
            else if (propertyName == nameof(WebNode.AutoReloadEnabled)) AutoReloadEnabled = _webNode.AutoReloadEnabled;
            else if (propertyName == nameof(WebNode.EnableSleepMode)) EnableSleepMode = _webNode.EnableSleepMode;
            else if (propertyName == nameof(WebNode.SleepIdleTimeoutValue)) SleepIdleTimeoutValue = _webNode.SleepIdleTimeoutValue;
            else if (propertyName == nameof(WebNode.SleepIdleTimeoutUnit)) SleepIdleTimeoutUnit = _webNode.SleepIdleTimeoutUnit ?? "s";
            else if (propertyName == nameof(WebNode.AutoReloadIntervalValue)) AutoReloadIntervalValue = _webNode.AutoReloadIntervalValue;
            else if (propertyName == nameof(WebNode.AutoReloadIntervalUnit)) AutoReloadIntervalUnit = _webNode.AutoReloadIntervalUnit;
            else if (propertyName == nameof(WebNode.JsSources))
            {
                _isSyncingFromNode = true;
                try
                {
                    // ⚠️ KHÔNG gọi RefreshJsAvailableNodes() – Clear JsAvailableNodeOptions làm tất cả ComboBox Node
                    // mất ItemsSource, TwoWay binding set SourceNodeId = null, xóa dữ liệu source.
                    var jsSources = _webNode.JsSources ?? new System.Collections.Generic.List<WebJsSourceMapping>();
                    // ⚠️ KHÔNG remove items – jsSources ít hơn vì SyncJsSourcesToNode exclude item chưa có đủ Node+Key.
                    // Item đang được điền (vd chọn Node nhưng node chưa có output) sẽ bị xóa nếu remove.
                    for (var i = 0; i < jsSources.Count; i++)
                    {
                        var js = jsSources[i];
                        if (i < JsSourcesList.Count)
                        {
                            JsSourcesList[i].SourceNodeId = js.SourceNodeId;
                            JsSourcesList[i].SourceOutputKey = js.SourceOutputKey;
                            JsSourcesList[i].AutoTimerEnabled = js.AutoTimerEnabled;
                            JsSourcesList[i].AutoTimerIntervalValue = js.AutoTimerIntervalValue;
                            JsSourcesList[i].AutoTimerIntervalUnit = js.AutoTimerIntervalUnit;
                            RefreshOutputKeyOptionsForJsSource(JsSourcesList[i]);
                        }
                        else
                        {
                            var item = new WebJsSourceItemViewModel
                            {
                                SourceNodeId = js.SourceNodeId,
                                SourceOutputKey = js.SourceOutputKey,
                                AutoTimerEnabled = js.AutoTimerEnabled,
                                AutoTimerIntervalValue = js.AutoTimerIntervalValue,
                                AutoTimerIntervalUnit = js.AutoTimerIntervalUnit
                            };
                            item.PropertyChanged += JsSourceItem_PropertyChanged;
                            RefreshNodeOptionsForJsSourceItem(item);
                            JsSourcesList.Add(item);
                            RefreshOutputKeyOptionsForJsSource(item);
                        }
                    }
                }
                finally { _isSyncingFromNode = false; }
            }
            else if (propertyName == nameof(WebNode.InputMappings))
            {
                _isSyncingFromNode = true;
                try
                {
                    // ⚠️ CRITICAL: Refresh available nodes trước khi sync mappings
                    RefreshAvailableNodes();

                    var mappings = _webNode.InputMappings ?? new System.Collections.Generic.List<WebInputMapping>();
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
                            // ⚠️ CRITICAL: Refresh output key options sau khi set SourceNodeId để đảm bảo combobox có đúng options
                            RefreshOutputKeyOptionsFor(InputMappingsList[i]);
                        }
                        else
                        {
                            var item = new WebInputMappingItemViewModel { SourceNodeId = m.SourceNodeId, SourceOutputKey = m.SourceOutputKey, InputKeyOverride = m.InputKeyOverride ?? string.Empty };
                            item.PropertyChanged += InputMappingItem_PropertyChanged;
                            InputMappingsList.Add(item);
                            RefreshOutputKeyOptionsFor(item);
                        }
                    }
                }
                finally
                {
                    _isSyncingFromNode = false;
                }
            }
        }

        private void JsSourceItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromNode) return;
            if (sender is not WebJsSourceItemViewModel item) return;
            if (e.PropertyName == nameof(WebJsSourceItemViewModel.SourceNodeId))
            {
                RefreshOutputKeyOptionsForJsSource(item);
                SyncJsSourcesToNode();
                return;
            }
            if (e.PropertyName == nameof(WebJsSourceItemViewModel.SourceOutputKey) ||
                e.PropertyName == nameof(WebJsSourceItemViewModel.AutoTimerEnabled) ||
                e.PropertyName == nameof(WebJsSourceItemViewModel.AutoTimerIntervalValue) ||
                e.PropertyName == nameof(WebJsSourceItemViewModel.AutoTimerIntervalUnit))
                SyncJsSourcesToNode();
        }

        private void SyncJsSourcesToNode()
        {
            _webNode.JsSources = JsSourcesList
                .Where(x => !string.IsNullOrWhiteSpace(x.SourceNodeId) && !string.IsNullOrWhiteSpace(x.SourceOutputKey))
                .Select(x => new WebJsSourceMapping
                {
                    SourceNodeId = x.SourceNodeId,
                    SourceOutputKey = x.SourceOutputKey,
                    AutoTimerEnabled = x.AutoTimerEnabled,
                    AutoTimerIntervalValue = x.AutoTimerIntervalValue,
                    AutoTimerIntervalUnit = x.AutoTimerIntervalUnit
                })
                .ToList();
        }

        public void RefreshNodeOptionsForJsSourceItem(WebJsSourceItemViewModel item)
        {
            item.AvailableNodeOptions.Clear();
            foreach (var opt in JsAvailableNodeOptions)
            {
                item.AvailableNodeOptions.Add(new WorkflowDataSourceOption { NodeId = opt.NodeId, Title = opt.Title });
            }
        }

        public void RefreshOutputKeyOptionsForJsSource(WebJsSourceItemViewModel item)
        {
            var savedKey = item.SourceOutputKey;
            item.AvailableOutputKeyOptions.Clear();
            if (string.IsNullOrWhiteSpace(item.SourceNodeId) || _host.ViewModel?.Nodes == null) return;
            var node = _host.ViewModel.Nodes.FirstOrDefault(n => string.Equals(n.Id, item.SourceNodeId, StringComparison.OrdinalIgnoreCase));
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
            if (item.AvailableOutputKeyOptions.Count > 0)
            {
                var hasSaved = !string.IsNullOrWhiteSpace(savedKey) &&
                    item.AvailableOutputKeyOptions.Any(k => string.Equals(k.Key, savedKey, StringComparison.Ordinal));
                item.SourceOutputKey = hasSaved ? savedKey : item.AvailableOutputKeyOptions[0].Key;
            }
        }

        [RelayCommand]
        private void AddJsSource()
        {
            var item = new WebJsSourceItemViewModel();
            item.PropertyChanged += JsSourceItem_PropertyChanged;
            RefreshNodeOptionsForJsSourceItem(item);
            JsSourcesList.Add(item);
            // ⚠️ KHÔNG gọi SyncJsSourcesToNode() – item mới trống nên sync sẽ exclude nó,
            // node fire PropertyChanged → OnNodePropertyChanged xóa item vừa thêm.
            // Sync sẽ chạy khi user chọn Node+Key (JsSourceItem_PropertyChanged).
        }

        [RelayCommand]
        private void RemoveJsSource(WebJsSourceItemViewModel? item)
        {
            if (item != null && JsSourcesList.Contains(item) && JsSourcesList.Count > 1)
            {
                item.PropertyChanged -= JsSourceItem_PropertyChanged;
                JsSourcesList.Remove(item);
                SyncJsSourcesToNode();
            }
        }

        /// <summary>
        /// Refresh danh sách nodes có thể chọn cho JS Source ComboBox.
        /// ⚠️ CRITICAL: Collection riêng để tránh conflict với Input mappings ComboBox.
        /// </summary>
        private void RefreshJsAvailableNodes()
        {
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Connections == null) return;

            // Build danh sách mới
            var newOptions = new List<WorkflowDataSourceOption>();

            var inputPort = _webNode.Ports?.FirstOrDefault(p => p.IsInput);
            var connectedNodeIds = vm.Connections
                .Where(c => c.ToNode == _webNode && c.FromNode != null &&
                            (inputPort == null || c.ToPort == inputPort || (c.ToPort != null && c.ToPort.IsInput)))
                .Select(c => c.FromNode!.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Thêm các node có connection
            foreach (var n in vm.Nodes)
            {
                if (ReferenceEquals(n, _webNode)) continue;
                if (!connectedNodeIds.Contains(n.Id)) continue;

                if (n is not InputNode && (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0))
                    continue;

                newOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
            }

            // 2. ⚠️ CRITICAL: Đảm bảo các JS source nodes cũng có trong danh sách
            var jsSourceNodeIds = (_webNode.JsSources ?? new List<WebJsSourceMapping>())
                .Where(m => !string.IsNullOrWhiteSpace(m.SourceNodeId))
                .Select(m => m.SourceNodeId!)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var nodeId in jsSourceNodeIds)
            {
                if (newOptions.Any(o => string.Equals(o.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var node = vm.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                if (node != null)
                {
                    newOptions.Add(new WorkflowDataSourceOption
                    {
                        NodeId = node.Id,
                        Title = string.IsNullOrWhiteSpace(node.Title) ? node.Id : node.Title
                    });
                }
            }

            // 3. Replace collection một lần
            JsAvailableNodeOptions.Clear();
            foreach (var option in newOptions)
            {
                JsAvailableNodeOptions.Add(option);
            }
        }

        private void InputMappingItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromNode) return;
            if (sender is not WebInputMappingItemViewModel item) return;

            // ⚠️ CRITICAL: Set flag để OnNodePropertyChanged biết đang update input mappings từ UI
            _isUpdatingInputMappings = true;
            try
            {
                if (e.PropertyName == nameof(WebInputMappingItemViewModel.SourceNodeId))
                {
                    RefreshOutputKeyOptionsFor(item);
                    SyncInputMappingsToNode();
                    return;
                }
                if (e.PropertyName == nameof(WebInputMappingItemViewModel.SourceOutputKey))
                {
                    SyncInputMappingsToNode();
                    return;
                }
                if (e.PropertyName == nameof(WebInputMappingItemViewModel.InputKeyOverride))
                {
                    SyncInputMappingsToNode();
                }
            }
            finally
            {
                _isUpdatingInputMappings = false;
            }
        }

        private void SyncInputMappingsToNode()
        {
            _webNode.InputMappings = InputMappingsList.Select(x => new WebInputMapping
            {
                SourceNodeId = x.SourceNodeId,
                SourceOutputKey = x.SourceOutputKey,
                InputKeyOverride = string.IsNullOrWhiteSpace(x.InputKeyOverride) ? null : x.InputKeyOverride.Trim()
            }).ToList();
        }

        protected override string GetDefaultTitle() => "Web";

        /// <summary>
        /// Chỉ hiển thị các node đã kết nối đến port IN của Web node (không lấy node từ port OUT).
        /// - Cho phép InputNode luôn xuất hiện nếu có connection, kể cả khi DynamicOutputs trống (backward compatible).
        /// - ⚠️ CRITICAL: Đảm bảo các node đã được chọn trong InputMappings CÀ JsSourceNodeId cũng được thêm vào danh sách,
        ///   ngay cả khi không có connection, để tránh ComboBox set SourceNodeId = null khi không tìm thấy item.
        /// - ⚠️ CRITICAL: Build danh sách mới trước, rồi replace collection một lần để tránh ComboBox mất ItemsSource
        ///   trong lúc Clear() → gây ra SelectedValue bị set null.
        /// </summary>
        public void RefreshAvailableNodes()
        {
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Connections == null) return;

            // Build danh sách mới trước (không clear collection cũ ngay)
            var newOptions = new List<WorkflowDataSourceOption>();

            var inputPort = _webNode.Ports?.FirstOrDefault(p => p.IsInput);
            var connectedNodeIds = vm.Connections
                .Where(c => c.ToNode == _webNode && c.FromNode != null &&
                            (inputPort == null || c.ToPort == inputPort || (c.ToPort != null && c.ToPort.IsInput)))
                .Select(c => c.FromNode!.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Thêm các node có connection vào WebNode
            foreach (var n in vm.Nodes)
            {
                if (ReferenceEquals(n, _webNode)) continue;
                if (!connectedNodeIds.Contains(n.Id)) continue;

                // Với InputNode, luôn cho phép xuất hiện (DynamicOutputs có thể được build lại sau khi load workflow)
                if (n is not InputNode && (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0))
                    continue;

                newOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
            }

            // 2. ⚠️ CRITICAL: Đảm bảo các node đã được chọn trong InputMappings cũng có trong danh sách
            //    (ngay cả khi không có connection) để tránh ComboBox set SourceNodeId = null
            var mappedNodeIds = (_webNode.InputMappings ?? new List<WebInputMapping>())
                .Where(m => !string.IsNullOrWhiteSpace(m.SourceNodeId))
                .Select(m => m.SourceNodeId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // ✅ CRITICAL: Include JS source node ids as well (so JS combobox doesn't lose SelectedValue)
            foreach (var js in _webNode.JsSources ?? new List<WebJsSourceMapping>())
            {
                if (!string.IsNullOrWhiteSpace(js.SourceNodeId) &&
                    !mappedNodeIds.Any(id => string.Equals(id, js.SourceNodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    mappedNodeIds.Add(js.SourceNodeId);
                }
            }

            foreach (var nodeId in mappedNodeIds)
            {
                // Nếu đã có trong newOptions thì bỏ qua
                if (newOptions.Any(o =>
                        string.Equals(o.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Tìm node tương ứng trong workflow
                var node = vm.Nodes.FirstOrDefault(n =>
                    string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));

                if (node == null)
                    continue; // node thực sự không còn tồn tại

                // Thêm node vào danh sách (ngay cả khi không có connection)
                newOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = node.Id,
                    Title = string.IsNullOrWhiteSpace(node.Title) ? node.Id : node.Title
                });
            }

            // 3. ⚠️ CRITICAL: Replace collection một lần để tránh ComboBox mất ItemsSource trong lúc Clear()
            //    Sử dụng Clear() + Add() thay vì tạo collection mới để giữ reference
            AvailableNodeOptions.Clear();
            foreach (var option in newOptions)
            {
                AvailableNodeOptions.Add(option);
            }
        }

        /// <summary>Làm mới danh sách Key (giống CodeNodeDialogViewModel: Clear + Add, rồi đảm bảo SourceOutputKey khớp một item).</summary>
        public void RefreshOutputKeyOptionsFor(WebInputMappingItemViewModel item)
        {
            item.AvailableOutputKeyOptions.Clear();
            if (string.IsNullOrWhiteSpace(item.SourceNodeId) || _host.ViewModel?.Nodes == null) return;

            var node = _host.ViewModel.Nodes.FirstOrDefault(n => string.Equals(n.Id, item.SourceNodeId, StringComparison.OrdinalIgnoreCase));
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
            // Giống CodeNodeDialogViewModel: nếu SourceOutputKey không còn trong danh sách thì chọn key đầu tiên để ComboBox hiển thị đúng.
            if (item.AvailableOutputKeyOptions.Count > 0 &&
                !item.AvailableOutputKeyOptions.Any(k => string.Equals(k.Key, item.SourceOutputKey, StringComparison.Ordinal)))
            {
                item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key;
            }
        }

        [RelayCommand]
        private void AddInputMapping()
        {
            var item = new WebInputMappingItemViewModel();
            item.PropertyChanged += InputMappingItem_PropertyChanged;
            InputMappingsList.Add(item);
            SyncInputMappingsToNode();
        }

        [RelayCommand]
        private void RemoveInputMapping(WebInputMappingItemViewModel? item)
        {
            if (item != null && InputMappingsList.Contains(item) && InputMappingsList.Count > 1)
            {
                item.PropertyChanged -= InputMappingItem_PropertyChanged;
                InputMappingsList.Remove(item);
                SyncInputMappingsToNode();
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
                list.Add(new WorkflowOutputKeyOption { Key = o.Key ?? "", Type = o.OutputType ?? o.ConvertType, DisplayName = o.DisplayName ?? o.Key });
            }
            return list;
        }

        protected override void OnSaveTitle()
        {
            _webNode.NotifyTitleChanged();
            _webNode.ExtractUrl = ExtractUrl ?? "";
            _webNode.ExtractRequestMethod = string.IsNullOrWhiteSpace(ExtractRequestMethod) ? "GET" : ExtractRequestMethod;
            _webNode.ExtractStatusCode = string.IsNullOrWhiteSpace(ExtractStatusCode) ? "200" : ExtractStatusCode;
            _webNode.SyncLiveOutputsToResults = SyncLiveOutputsToResults;
            _webNode.CookieText = CookieText;
             // Không cho phép timeout âm; 0 = không chờ
            _webNode.ResponseOutputsWaitTimeoutMs = ResponseOutputsWaitTimeoutMs < 0 ? 0 : ResponseOutputsWaitTimeoutMs;
            _webNode.ResponseOutputsWaitMode = ResponseOutputsWaitMode;
            _webNode.BlockAllRequestsAfterFirstMatch = BlockAllRequestsAfterFirstMatch;
            _webNode.AutoReloadEnabled = AutoReloadEnabled;
            _webNode.EnableSleepMode = EnableSleepMode;
            _webNode.SleepIdleTimeoutValue = SleepIdleTimeoutValue;
            _webNode.SleepIdleTimeoutUnit = SleepIdleTimeoutUnit ?? "s";
            _webNode.AutoReloadIntervalValue = AutoReloadIntervalValue;
            // Map "phút" (display) → "min" (internal) nếu cần (nếu dng hướng display trong options)
            _webNode.AutoReloadIntervalUnit = AutoReloadIntervalUnit;

            SyncJsSourcesToNode();
            SyncInputMappingsToNode();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }

    public sealed class WebOutputsWaitModeOption
    {
        public WebOutputsWaitMode Value { get; set; }
        public string DisplayName { get; set; }

        public WebOutputsWaitModeOption(WebOutputsWaitMode value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}