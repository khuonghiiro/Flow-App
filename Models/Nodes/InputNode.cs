using System.Linq;

namespace FlowMy.Models
{
    /// <summary>
    /// Model cho Input Node - node để nhập dữ liệu với key, value và type
    /// </summary>
    public class InputNode : WorkflowNode
    {
        private string _key = string.Empty;
        private string _value = string.Empty;
        private WorkflowDataType _dataType = WorkflowDataType.String;
        private List<string> _arrayValues = new List<string>();

        /// <summary>
        /// Key của input data
        /// </summary>
        public new string Key
        {
            get => _key;
            set
            {
                if (_key == value) return;
                _key = value;
                OnPropertyChanged();
                // Cập nhật DynamicOutputs.Key khi Key thay đổi
                UpdateDynamicOutputsKey();
            }
        }

        /// <summary>
        /// Value của input data (dùng cho non-array types)
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Array values (dùng cho ArrayString, ArrayNumber, ArrayDynamic)
        /// </summary>
        public List<string> ArrayValues
        {
            get => _arrayValues;
            set
            {
                if (_arrayValues == value) return;
                _arrayValues = value ?? new List<string>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Kiểm tra xem có phải array type không
        /// </summary>
        public bool IsArrayType => DataType == WorkflowDataType.ArrayString
            || DataType == WorkflowDataType.ArrayNumber
            || DataType == WorkflowDataType.ArrayDynamic;

        /// <summary>
        /// Loại dữ liệu (String, Integer, Number, DateTime, Time, Boolean, ArrayString, ArrayNumber, ArrayDynamic)
        /// </summary>
        public WorkflowDataType DataType
        {
            get => _dataType;
            set
            {
                if (_dataType == value) return;
                _dataType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDateTimeOrTime));
                OnPropertyChanged(nameof(IsArrayType));

                // Cập nhật OutputType của DynamicOutputs khi DataType thay đổi
                UpdateDynamicOutputsType();

                // Khởi tạo ArrayValues nếu chuyển sang array type
                if (IsArrayType && (_arrayValues == null || _arrayValues.Count == 0))
                {
                    ArrayValues = new List<string> { string.Empty };
                }
            }
        }

        /// <summary>
        /// Cập nhật OutputType của DynamicOutputs khi DataType thay đổi
        /// </summary>
        private void UpdateDynamicOutputsType()
        {
            if (DynamicOutputs == null || DynamicOutputs.Count == 0) return;

            foreach (var output in DynamicOutputs)
            {
                output.OutputType = DataType;
            }
        }

        /// <summary>
        /// Cập nhật Key của DynamicOutputs khi Key thay đổi
        /// </summary>
        private void UpdateDynamicOutputsKey()
        {
            if (DynamicOutputs == null || DynamicOutputs.Count == 0) return;

            // InputNode chỉ có 1 output duy nhất, luôn cập nhật output đầu tiên
            var output = DynamicOutputs.FirstOrDefault();

            if (output != null)
            {
                // Nếu Key rỗng hoặc null, đặt về "Input" để backward compatible
                // Nếu Key có giá trị, cập nhật output key
                if (string.IsNullOrWhiteSpace(_key))
                {
                    output.Key = "Input";
                }
                else
                {
                    output.Key = _key;
                }
            }
        }

        /// <summary>
        /// Kiểm tra xem có phải DateTime hoặc Time không (để disable value textbox)
        /// </summary>
        public bool IsDateTimeOrTime => DataType == WorkflowDataType.DateTime || DataType == WorkflowDataType.Time;

        public InputNode()
        {
            Type = NodeType.Input;
            Title = "Input";
        }
    }

}
