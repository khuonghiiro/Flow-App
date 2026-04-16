using FlowMy.Models;
using FlowMy.Models.Enums;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class InputNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly InputNode _inputNode;
        private bool _isUpdatingFromNode = false; // Flag để tránh vòng lặp

        [ObservableProperty]
        private string _key;

        [ObservableProperty]
        private WorkflowDataType _dataType;

        [ObservableProperty]
        private string _value;

        [ObservableProperty]
        private List<string> _arrayValues;

        // Cho phép toggle giữa Text (1 dòng) và TextArea (đa dòng) cho kiểu String
        [ObservableProperty]
        private bool _isMultilineString = true;

        public ObservableCollection<DataTypeOption> DataTypeOptions { get; } = new()
        {
            new DataTypeOption(WorkflowDataType.String, "String"),
            new DataTypeOption(WorkflowDataType.Integer, "Integer"),
            new DataTypeOption(WorkflowDataType.Number, "Number"),
            new DataTypeOption(WorkflowDataType.Boolean, "Boolean"),
            new DataTypeOption(WorkflowDataType.DateTime, "DateTime"),
            new DataTypeOption(WorkflowDataType.Time, "Time"),
            new DataTypeOption(WorkflowDataType.ArrayString, "Array String"),
            new DataTypeOption(WorkflowDataType.ArrayNumber, "Array Number"),
            new DataTypeOption(WorkflowDataType.ArrayDynamic, "Array Dynamic")
        };

        public bool IsArrayType => DataType == WorkflowDataType.ArrayString
            || DataType == WorkflowDataType.ArrayNumber
            || DataType == WorkflowDataType.ArrayDynamic;

        public bool IsDateTimeOrTime => DataType == WorkflowDataType.DateTime || DataType == WorkflowDataType.Time;

        // Dùng cho binding hiển thị nút text/textarea trong dialog
        public bool IsStringType => DataType == WorkflowDataType.String;

        public InputNodeDialogViewModel(InputNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _inputNode = node;
            _key = node.Key ?? string.Empty;
            _dataType = node.DataType;
            _value = node.Value ?? string.Empty;
            _arrayValues = node.ArrayValues?.ToList() ?? new List<string>();
            // _isMultilineString = true;

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (_isUpdatingFromNode) return; // Tránh vòng lặp

                    if (e.PropertyName == nameof(InputNode.Key))
                        Key = node.Key ?? string.Empty;
                    else if (e.PropertyName == nameof(InputNode.DataType))
                    {
                        if (DataType != node.DataType)
                            DataType = node.DataType;
                    }
                    else if (e.PropertyName == nameof(InputNode.Value))
                        Value = node.Value ?? string.Empty;
                    else if (e.PropertyName == nameof(InputNode.ArrayValues))
                    {
                        var newArray = node.ArrayValues?.ToList() ?? new List<string>();
                        if (ArrayValues == null || !ArrayValues.SequenceEqual(newArray))
                            ArrayValues = newArray;
                    }
                    OnNodePropertyChanged(e.PropertyName);
                };
            }
        }

        protected override string GetDefaultTitle() => "Input";

        partial void OnKeyChanged(string value)
        {
            if (_isUpdatingFromNode) return;
            _isUpdatingFromNode = true;
            try
            {
                _inputNode.Key = value;
                // Reload outputs để hiển thị key mới ngay lập tức
                LoadOutputs();
                // Refresh tất cả các node khác đang sử dụng InputNode này để hiển thị key mới
                // _host.RequestSyncDataPanels(immediate: false);
            }
            finally
            {
                _isUpdatingFromNode = false;
            }
        }

        partial void OnDataTypeChanged(WorkflowDataType value)
        {
            if (_isUpdatingFromNode) return;

            _isUpdatingFromNode = true;
            try
            {
                var oldDataType = _inputNode.DataType;
                _inputNode.DataType = value;

                // Giữ nguyên value nếu chuyển giữa String/Integer/Number hoặc giữa Array types
                // Reset value nếu chuyển sang loại khác
                var keepValueTypes = new[] { WorkflowDataType.String, WorkflowDataType.Integer, WorkflowDataType.Number };
                var keepArrayTypes = new[] { WorkflowDataType.ArrayString, WorkflowDataType.ArrayNumber, WorkflowDataType.ArrayDynamic };

                bool wasKeepValueType = keepValueTypes.Contains(oldDataType);
                bool isKeepValueType = keepValueTypes.Contains(value);
                bool wasArrayType = keepArrayTypes.Contains(oldDataType);
                bool isArrayType = keepArrayTypes.Contains(value);

                // Nếu chuyển từ non-array sang array hoặc ngược lại
                if (wasArrayType != isArrayType)
                {
                    if (isArrayType)
                    {
                        // Chuyển sang array type
                        if (ArrayValues == null || ArrayValues.Count == 0)
                        {
                            ArrayValues = _inputNode.ArrayValues?.ToList() ?? new List<string> { string.Empty };
                        }
                        Value = string.Empty;
                    }
                    else
                    {
                        // Chuyển từ array sang non-array
                        Value = string.Empty;
                        ArrayValues = new List<string>();
                    }
                }
                // Nếu chuyển giữa các non-array types
                else if (!isArrayType)
                {
                    // Chỉ giữ nguyên nếu cả hai đều là String/Integer/Number
                    if (!wasKeepValueType || !isKeepValueType)
                    {
                        Value = string.Empty;
                    }
                    ArrayValues = new List<string>();
                }
                // Nếu chuyển giữa các array types, giữ nguyên ArrayValues
                else
                {
                    if (ArrayValues == null || ArrayValues.Count == 0)
                    {
                        ArrayValues = _inputNode.ArrayValues?.ToList() ?? new List<string> { string.Empty };
                    }
                }

                // Notify các computed property phụ thuộc vào DataType
                OnPropertyChanged(nameof(IsArrayType));
                OnPropertyChanged(nameof(IsDateTimeOrTime));
                OnPropertyChanged(nameof(IsStringType));
            }
            finally
            {
                _isUpdatingFromNode = false;
            }
        }

        partial void OnValueChanged(string value)
        {
            if (_isUpdatingFromNode) return;
            if (!IsArrayType)
            {
                _isUpdatingFromNode = true;
                try
                {
                    _inputNode.Value = value;
                    // Reload outputs để hiển thị value mới ngay lập tức
                    LoadOutputs();
                }
                finally
                {
                    _isUpdatingFromNode = false;
                }
            }
        }

        partial void OnArrayValuesChanged(List<string> value)
        {
            if (_isUpdatingFromNode) return;
            if (IsArrayType)
            {
                _isUpdatingFromNode = true;
                try
                {
                    _inputNode.ArrayValues = value?.ToList() ?? new List<string>();
                    // Reload outputs để hiển thị array values mới ngay lập tức
                    LoadOutputs();
                }
                finally
                {
                    _isUpdatingFromNode = false;
                }
            }
        }

        // Override OnSaveTitle để thêm logic riêng thay vì override SaveTitle
        protected override void OnSaveTitle()
        {
            // Base SaveTitle() đã gán _node.Title = NodeTitle, ở đây chỉ cần notify để UI cập nhật
            _inputNode.NotifyTitleChanged();

            if (_inputNode.Key != Key)
            {
                _inputNode.Key = Key;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_inputNode.DataType != DataType)
            {
                _inputNode.DataType = DataType;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (!IsArrayType && _inputNode.Value != Value)
            {
                _inputNode.Value = Value;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (IsArrayType && (_inputNode.ArrayValues == null || !_inputNode.ArrayValues.SequenceEqual(ArrayValues ?? new List<string>())))
            {
                _inputNode.ArrayValues = ArrayValues?.ToList() ?? new List<string>();
                _host.RequestSyncDataPanels(immediate: true);
            }
        }
    }

    public class DataTypeOption
    {
        public WorkflowDataType Value { get; set; }
        public string DisplayName { get; set; }
        public DataTypeOption(WorkflowDataType value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }
}
