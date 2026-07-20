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

            RefreshAllNodesWithOutputs(AvailableNodeOptions);
        }

        protected override string GetDefaultTitle() => "Dynamic UI Form (Sciter)";

        partial void OnHtmlCodeChanged(string value)
        {
            if (_nodeTyped != null)
                _nodeTyped.HtmlCode = value ?? string.Empty;
        }

        partial void OnCssCodeChanged(string value)
        {
            if (_nodeTyped != null)
                _nodeTyped.CssCode = value ?? string.Empty;
        }

        partial void OnJsCodeChanged(string value)
        {
            if (_nodeTyped != null)
                _nodeTyped.JsCode = value ?? string.Empty;
        }

        partial void OnParamsCodeChanged(string value)
        {
            if (_nodeTyped != null)
                _nodeTyped.ParamsCode = value ?? string.Empty;
        }

        protected override void OnSaveTitle()
        {
            _nodeTyped.HtmlCode = HtmlCode ?? string.Empty;
            _nodeTyped.JsCode = JsCode ?? string.Empty;
            _nodeTyped.CssCode = CssCode ?? string.Empty;
            _nodeTyped.ParamsCode = ParamsCode ?? string.Empty;

            SyncInputMappingsToNode();
            SyncOutputKeysToNode();

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
    }

}
