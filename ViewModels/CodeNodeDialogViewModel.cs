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
    public partial class OutputKeyItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _key = "result";
    }

    /// <summary>ViewModel cho một dòng input (Node + Key + Key tùy chỉnh) trong Code node.</summary>
    public partial class CodeInputMappingItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? _sourceNodeId;

        [ObservableProperty]
        private string? _sourceOutputKey;

        [ObservableProperty]
        private string _inputKeyOverride = string.Empty;

        [ObservableProperty]
        private bool _shouldReExecute = false;

        // Auto-refresh fields
        [ObservableProperty]
        private bool _autoRefreshEnabled = false;

        [ObservableProperty]
        private int _autoRefreshInterval = 1000;

        [ObservableProperty]
        private string _autoRefreshUnit = "ms";

        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

        public string EffectiveInputKeyDisplay => !string.IsNullOrWhiteSpace(InputKeyOverride)
            ? InputKeyOverride.Trim()
            : (string.IsNullOrWhiteSpace(SourceOutputKey) ? "input" : SourceOutputKey.Trim());

        partial void OnSourceOutputKeyChanged(string? value) => OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
        partial void OnInputKeyOverrideChanged(string value) => OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
    }

    public partial class CodeNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly CodeNode _codeNode;
        /// <summary>Đang đồng bộ từ node sang list – tránh gọi SyncInputMappingsToNode() để không gây StackOverflow.</summary>
        private bool _isSyncingFromNode;

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<CodeInputMappingItemViewModel> InputMappingsList { get; } = new();
        public ObservableCollection<OutputKeyItemViewModel> OutputKeysList { get; } = new();

        [ObservableProperty]
        private string _scriptCode = "// Nhập code JavaScript. Biến từ input dùng tên key.\n// return { key1: value1 }; để trả về outputs.\nreturn {};";

        [ObservableProperty]
        private int _codeFontSize = 13;

        public CodeNodeDialogViewModel(CodeNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _codeNode = node ?? throw new ArgumentNullException(nameof(node));

            _scriptCode = node.ScriptCode ?? string.Empty;

            var mappings = node.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
            if (mappings.Count == 0) mappings.Add(new CodeInputMapping());
            foreach (var m in mappings)
            {
                var item = new CodeInputMappingItemViewModel
                {
                    SourceNodeId = m.SourceNodeId,
                    SourceOutputKey = m.SourceOutputKey,
                    InputKeyOverride = m.InputKeyOverride ?? string.Empty,
                    ShouldReExecute = m.ShouldReExecute,
                    AutoRefreshEnabled = m.AutoRefreshEnabled,
                    AutoRefreshInterval = m.AutoRefreshInterval,
                    AutoRefreshUnit = m.AutoRefreshUnit
                };
                item.PropertyChanged += InputMappingItem_PropertyChanged;
                InputMappingsList.Add(item);
                RefreshOutputKeyOptionsFor(item);
            }

            foreach (var k in node.OutputKeys)
                OutputKeysList.Add(new OutputKeyItemViewModel { Key = k });

            RefreshAvailableNodes();

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }
        }

        protected override string GetDefaultTitle() => "Code";

        private void InputMappingItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromNode) return;
            if (sender is not CodeInputMappingItemViewModel item) return;
            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.SourceNodeId))
            {
                RefreshOutputKeyOptionsFor(item);
                SyncInputMappingsToNode();
                OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
                OnPropertyChanged(nameof(FirstInputVariableName));
                return;
            }
            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.SourceOutputKey))
            {
                if (!string.IsNullOrWhiteSpace(item.SourceOutputKey) && string.IsNullOrWhiteSpace(item.InputKeyOverride))
                    item.InputKeyOverride = item.SourceOutputKey.Trim();
                SyncInputMappingsToNode();
                OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
                OnPropertyChanged(nameof(FirstInputVariableName));
                return;
            }
            if (e.PropertyName == nameof(CodeInputMappingItemViewModel.InputKeyOverride) ||
                e.PropertyName == nameof(CodeInputMappingItemViewModel.ShouldReExecute))
            {
                SyncInputMappingsToNode();
                OnPropertyChanged(nameof(EffectiveInputKeyDisplay));
                OnPropertyChanged(nameof(FirstInputVariableName));
            }
        }

        /// <summary>Tùy chọn đơn vị thời gian cho auto-refresh ComboBox.</summary>
        public System.Collections.Generic.List<string> AutoRefreshUnitOptions { get; } = new() { "ms", "s", "min" };

        private void SyncInputMappingsToNode()
        {
            _codeNode.InputMappings = InputMappingsList.Select(x => new CodeInputMapping
            {
                SourceNodeId = x.SourceNodeId,
                SourceOutputKey = x.SourceOutputKey,
                ShouldReExecute = x.ShouldReExecute,
                InputKeyOverride = string.IsNullOrWhiteSpace(x.InputKeyOverride) ? null : x.InputKeyOverride.Trim(),
                AutoRefreshEnabled = x.AutoRefreshEnabled,
                AutoRefreshInterval = x.AutoRefreshInterval > 0 ? x.AutoRefreshInterval : 1000,
                AutoRefreshUnit = x.AutoRefreshUnit ?? "ms"
            }).ToList();
        }

        partial void OnScriptCodeChanged(string value)
        {
            _codeNode.ScriptCode = value ?? string.Empty;
        }

        protected override void OnSaveTitle()
        {
            _codeNode.NotifyTitleChanged();
            SyncOutputKeysToNode();
            SyncInputMappingsToNode();
        }

        private void SyncOutputKeysToNode()
        {
            _codeNode.OutputKeys = OutputKeysList.Where(x => !string.IsNullOrWhiteSpace(x.Key)).Select(x => x.Key.Trim()).Distinct().ToList();
        }

        private void OnNodePropertyChanged(string propertyName)
        {
            if (propertyName == nameof(CodeNode.ScriptCode)) ScriptCode = _codeNode.ScriptCode ?? string.Empty;
            if (propertyName == nameof(CodeNode.InputMappings))
            {
                _isSyncingFromNode = true;
                try
                {
                    var mappings = _codeNode.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
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
                            // Không gọi RefreshOutputKeyOptionsFor ở đây: Clear/Add lại Key list sẽ làm ComboBox Key mất selection.
                            // Chỉ refresh khi user đổi Node (SourceNodeId).
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
                }
                finally
                {
                    _isSyncingFromNode = false;
                }
            }
        }

        /// <summary>
        /// Hiển thị các node đã kết nối đến port IN của Code node (trực tiếp hoặc qua trung gian).
        /// Duyệt upstream giống WebNodeDialogViewModel/BaseNodeDialogViewModel: A -> B -> CodeNode thì thấy cả A và B.
        /// - Cho phép InputNode luôn xuất hiện nếu có connection, kể cả khi DynamicOutputs trống.
        /// - Đảm bảo các node đã chọn trong InputMappings cũng có trong danh sách để tránh ComboBox mất selection.
        /// </summary>
        public void RefreshAvailableNodes()
        {
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Connections == null) return;

            var connections = vm.Connections;
            var upstream = new HashSet<WorkflowNode>();
            var listOutBarriers = new HashSet<ListOutNode>();
            var stack = new Stack<WorkflowNode>();
            stack.Push(_codeNode);
            var parentLoops = new HashSet<LoopNode>();

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                var incoming = connections
                    .Where(c => c.ToNode == current && c.FromNode != null)
                    .Where(c => !(current is LoopBodyNode &&
                                  c.ToPort != null &&
                                  string.Equals(c.ToPort.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var conn in incoming)
                {
                    var src = conn.FromNode;
                    if (src == null) continue;

                    if (src is ListOutNode listOutNode)
                    {
                        if (upstream.Add(src)) listOutBarriers.Add(listOutNode);
                        continue;
                    }

                    if (src is LoopBodyNode body &&
                        conn.FromPort != null &&
                        string.Equals(conn.FromPort.Id, "LoopBodyLeft", StringComparison.OrdinalIgnoreCase) &&
                        body.ParentLoopNode != null)
                    {
                        parentLoops.Add(body.ParentLoopNode);
                    }

                    if (upstream.Add(src)) stack.Push(src);
                }
            }

            var producerNodes = upstream
                .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                .ToList();

            if (listOutBarriers.Count > 0)
            {
                producerNodes = producerNodes.Where(n => n is ListOutNode).ToList();
            }

            foreach (var loop in parentLoops)
            {
                if (loop.DynamicOutputs != null && loop.DynamicOutputs.Count > 0 && !producerNodes.Contains(loop) && listOutBarriers.Count == 0)
                    producerNodes.Add(loop);
            }

            producerNodes = producerNodes.Where(n => !ReferenceEquals(n, _codeNode)).ToList();

            var newOptions = new List<WorkflowDataSourceOption>();
            foreach (var n in producerNodes)
            {
                newOptions.Add(CreateDataSourceOption(n));
            }
            // InputNode luôn cho phép xuất hiện (DynamicOutputs có thể build lại sau khi load workflow)
            foreach (var n in upstream)
            {
                if (ReferenceEquals(n, _codeNode)) continue;
                if (n is not InputNode) continue;
                if (newOptions.Any(o => string.Equals(o.NodeId, n.Id, StringComparison.OrdinalIgnoreCase))) continue;
                if (listOutBarriers.Count > 0) continue; // Khi có barrier, chỉ ListOutNode
                newOptions.Add(CreateDataSourceOption(n));
            }

            var mappedNodeIds = (_codeNode.InputMappings ?? new List<CodeInputMapping>())
                .Where(m => !string.IsNullOrWhiteSpace(m.SourceNodeId))
                .Select(m => m.SourceNodeId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var nodeId in mappedNodeIds)
            {
                if (newOptions.Any(o => string.Equals(o.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))) continue;
                var node = vm.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                if (node == null) continue;
                newOptions.Add(CreateDataSourceOption(node));
            }

            AvailableNodeOptions.Clear();
            foreach (var option in newOptions)
                AvailableNodeOptions.Add(option);
        }

        /// <summary>Làm mới danh sách Key (giống OutputNodeDialog: Clear + Add, rồi đảm bảo SourceOutputKey khớp một item).</summary>
        public void RefreshOutputKeyOptionsFor(CodeInputMappingItemViewModel item)
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
            // Giống OutputNodeDialog: nếu SourceOutputKey không còn trong danh sách thì chọn key đầu tiên để ComboBox hiển thị đúng.
            if (item.AvailableOutputKeyOptions.Count > 0 &&
                !item.AvailableOutputKeyOptions.Any(k => string.Equals(k.Key, item.SourceOutputKey, StringComparison.Ordinal)))
            {
                item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key;
            }
        }

        /// <summary>Danh sách tên biến trong code (các input), cách nhau bằng dấu phẩy.</summary>
        public string EffectiveInputKeyDisplay => string.Join(", ", InputMappingsList.Select(x => x.EffectiveInputKeyDisplay));

        /// <summary>Tên biến đầu tiên (dùng cho hint output js).</summary>
        public string FirstInputVariableName => InputMappingsList.Count > 0 ? InputMappingsList[0].EffectiveInputKeyDisplay : "prompt";

        [RelayCommand]
        private void AddInputMapping()
        {
            var item = new CodeInputMappingItemViewModel();
            item.PropertyChanged += InputMappingItem_PropertyChanged;
            InputMappingsList.Add(item);
            SyncInputMappingsToNode();
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
                SyncInputMappingsToNode();
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
        private void IncreaseCodeFontSize()
        {
            if (CodeFontSize < 24) CodeFontSize++;
        }

        [RelayCommand]
        private void DecreaseCodeFontSize()
        {
            if (CodeFontSize > 10) CodeFontSize--;
        }

        [RelayCommand]
        private void InsertExampleSnippet()
        {
            var varName = EffectiveInputKeyDisplay;
            ScriptCode = "// Biến từ input: " + varName + " (chuỗi từ node nguồn)\n" +
                "var data = JSON.parse(" + varName + ");\n" +
                "var count = data.items ? data.items.length : 0;\n" +
                "var first = (data.items && data.items[0]) ? data.items[0] : '';\n" +
                "return { count: count, first: first };";
        }
    }
}
