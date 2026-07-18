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
        private bool _isSyncing = false;
        private bool _isSyncingFromNode = false;

        [ObservableProperty] private DynamicUiFieldConfig? _selectedField;

        // Form inputs for adding a new output variable
        [ObservableProperty] private string _newFieldKey = "";
        [ObservableProperty] private string _newFieldLabel = "";
        [ObservableProperty] private WorkflowDataType _selectedNewDataType = WorkflowDataType.String;

        public ObservableCollection<DynamicUiFieldConfig> FieldsList { get; } = new();
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOptionsWrapper> DynamicFieldOutputKeyOptions { get; } = new(); // For individual fields
        
        // Input Mappings List (from parent nodes)
        public ObservableCollection<CodeInputMappingItemViewModel> InputMappingsList { get; } = new();
        public List<string> AutoRefreshUnitOptions { get; } = new() { "ms", "s", "min" };

        public List<WorkflowDataType> DataTypes { get; } = Enum.GetValues(typeof(WorkflowDataType)).Cast<WorkflowDataType>().ToList();

        public string HtmlCode
        {
            get => _nodeTyped.HtmlCode;
            set { if (_nodeTyped.HtmlCode != value) { _nodeTyped.HtmlCode = value; OnPropertyChanged(); } }
        }

        public string CssCode
        {
            get => _nodeTyped.CssCode;
            set { if (_nodeTyped.CssCode != value) { _nodeTyped.CssCode = value; OnPropertyChanged(); } }
        }

        public string JsCode
        {
            get => _nodeTyped.JsCode;
            set { if (_nodeTyped.JsCode != value) { _nodeTyped.JsCode = value; OnPropertyChanged(); } }
        }

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

            // Load existing fields
            foreach (var field in _nodeTyped.Fields)
            {
                var clone = field.Clone();
                clone.Id = field.Id;
                clone.PropertyChanged += Field_PropertyChanged;
                FieldsList.Add(clone);
            }

            if (FieldsList.Count > 0)
            {
                SelectedField = FieldsList[0];
            }

            // Load input mappings
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

            RefreshAllNodesWithOutputs(AvailableNodeOptions);
        }

        protected override string GetDefaultTitle() => "Dynamic UI Form (Sciter)";

        partial void OnSelectedFieldChanging(DynamicUiFieldConfig? oldValue, DynamicUiFieldConfig? newValue)
        {
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= SelectedField_PropertyChanged;
            }
        }

        partial void OnSelectedFieldChanged(DynamicUiFieldConfig? value)
        {
            if (value != null)
            {
                value.PropertyChanged += SelectedField_PropertyChanged;
                RefreshSelectedFieldOutputKeys();
            }
            else
            {
                DynamicFieldOutputKeyOptions.Clear();
            }
        }

        private void SelectedField_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DynamicUiFieldConfig.SourceNodeId))
            {
                RefreshSelectedFieldOutputKeys();
                SyncFieldsToNode();
            }
            else if (e.PropertyName == nameof(DynamicUiFieldConfig.SourceOutputKey))
            {
                SyncFieldsToNode();
            }
            else
            {
                if (_isSyncing) return;
                SyncFieldsToNode();
            }
        }

        private void RefreshSelectedFieldOutputKeys()
        {
            DynamicFieldOutputKeyOptions.Clear();
            if (SelectedField == null || string.IsNullOrWhiteSpace(SelectedField.SourceNodeId)) return;

            var options = GetOutputKeysForNode(SelectedField.SourceNodeId);
            foreach (var opt in options)
            {
                DynamicFieldOutputKeyOptions.Add(new WorkflowOutputKeyOptionsWrapper { Option = opt });
            }
        }

        private void Field_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncing) return;
            SyncFieldsToNode();
        }

        private void SyncFieldsToNode()
        {
            _isSyncing = true;
            try
            {
                _nodeTyped.Fields.Clear();
                foreach (var vmField in FieldsList)
                {
                    var modelField = new DynamicUiFieldConfig
                    {
                        Id = vmField.Id,
                        Key = vmField.Key,
                        Label = vmField.Label,
                        DataType = vmField.DataType,
                        DefaultValue = vmField.DefaultValue,
                        BindToInput = vmField.BindToInput,
                        SourceNodeId = vmField.SourceNodeId,
                        SourceOutputKey = vmField.SourceOutputKey
                    };
                    _nodeTyped.Fields.Add(modelField);
                }

                // Sync ports
                _nodeTyped.RebuildDynamicPorts();
                
                // Request host to sync data panels / refresh canvas
                _host.RequestSyncDataPanels(immediate: true);
            }
            finally
            {
                _isSyncing = false;
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
        private void AddField()
        {
            var key = string.IsNullOrWhiteSpace(NewFieldKey) ? $"port_{FieldsList.Count + 1}" : NewFieldKey.Trim();
            var label = string.IsNullOrWhiteSpace(NewFieldLabel) ? $"Port {FieldsList.Count + 1}" : NewFieldLabel.Trim();

            var newField = new DynamicUiFieldConfig
            {
                Key = key,
                Label = label,
                DataType = SelectedNewDataType
            };

            newField.PropertyChanged += Field_PropertyChanged;
            FieldsList.Add(newField);
            SelectedField = newField;

            // Clear inputs
            NewFieldKey = "";
            NewFieldLabel = "";

            SyncFieldsToNode();
        }

        [RelayCommand]
        private void RemoveField()
        {
            if (SelectedField == null) return;

            var toRemove = SelectedField;
            var index = FieldsList.IndexOf(toRemove);
            
            toRemove.PropertyChanged -= Field_PropertyChanged;
            FieldsList.Remove(toRemove);

            if (FieldsList.Count > 0)
            {
                SelectedField = FieldsList[Math.Min(index, FieldsList.Count - 1)];
            }
            else
            {
                SelectedField = null;
            }

            SyncFieldsToNode();
        }

        [RelayCommand]
        private void MoveFieldUp()
        {
            if (SelectedField == null) return;
            var index = FieldsList.IndexOf(SelectedField);
            if (index <= 0) return;

            var item = SelectedField;
            FieldsList.RemoveAt(index);
            FieldsList.Insert(index - 1, item);
            SelectedField = item;

            SyncFieldsToNode();
        }

        [RelayCommand]
        private void MoveFieldDown()
        {
            if (SelectedField == null) return;
            var index = FieldsList.IndexOf(SelectedField);
            if (index < 0 || index >= FieldsList.Count - 1) return;

            var item = SelectedField;
            FieldsList.RemoveAt(index);
            FieldsList.Insert(index + 1, item);
            SelectedField = item;

            SyncFieldsToNode();
        }
    }

    public class WorkflowOutputKeyOptionsWrapper
    {
        public WorkflowOutputKeyOption? Option { get; set; }
        public string Key => Option?.Key ?? string.Empty;
        public string DisplayName => Option?.DisplayName ?? string.Empty;
    }
}
