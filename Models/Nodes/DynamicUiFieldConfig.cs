using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    public sealed class DynamicUiFieldConfig : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _key = "variable_name";
        private string _label = "Label";
        private WorkflowDataType _dataType = WorkflowDataType.String;
        private string _defaultValue = "";
        private bool _bindToInput = false;
        private string? _sourceNodeId;
        private string? _sourceOutputKey;

        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        public string Key
        {
            get => _key;
            set { if (_key != value) { _key = value; OnPropertyChanged(); } }
        }

        public string Label
        {
            get => _label;
            set { if (_label != value) { _label = value; OnPropertyChanged(); } }
        }

        public WorkflowDataType DataType
        {
            get => _dataType;
            set { if (_dataType != value) { _dataType = value; OnPropertyChanged(); } }
        }

        public string DefaultValue
        {
            get => _defaultValue;
            set { if (_defaultValue != value) { _defaultValue = value; OnPropertyChanged(); } }
        }

        public bool BindToInput
        {
            get => _bindToInput;
            set { if (_bindToInput != value) { _bindToInput = value; OnPropertyChanged(); } }
        }

        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public DynamicUiFieldConfig Clone()
        {
            return new DynamicUiFieldConfig
            {
                Id = Guid.NewGuid().ToString(),
                Key = this.Key,
                Label = this.Label,
                DataType = this.DataType,
                DefaultValue = this.DefaultValue,
                BindToInput = this.BindToInput,
                SourceNodeId = this.SourceNodeId,
                SourceOutputKey = this.SourceOutputKey
            };
        }
    }
}
