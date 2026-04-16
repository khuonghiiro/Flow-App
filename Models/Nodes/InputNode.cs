using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models
{
    /// <summary>
    /// Model cho Input Node - node để nhập dữ liệu với key, value và type
    /// </summary>
    public class InputNode : WorkflowNode, INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _value = string.Empty;
        private WorkflowDataType _dataType = WorkflowDataType.String;
        private List<string> _arrayValues = new List<string>();
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;

        public event PropertyChangedEventHandler? PropertyChanged;

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

        /// <summary>
        /// Chế độ hiển thị tiêu đề của node (mặc định Hover).
        /// </summary>
        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set
            {
                if (_titleDisplayMode == value) return;
                _titleDisplayMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Chế độ màu sắc tiêu đề (mặc định NodeColor - theo màu node).
        /// </summary>
        public TitleColorMode TitleColorMode
        {
            get => _titleColorMode;
            set
            {
                if (_titleColorMode == value) return;
                _titleColorMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Key của màu tùy chọn cho tiêu đề (khi TitleColorMode = CustomColor).
        /// </summary>
        public string? TitleColorKey
        {
            get => _titleColorKey;
            set
            {
                if (_titleColorKey == value) return;
                _titleColorKey = value;
                OnPropertyChanged();
            }
        }

        public InputNode()
        {
            Type = NodeType.Input;
            Title = "Input";
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Method helper để notify PropertyChanged khi Title thay đổi từ bên ngoài
        /// (ví dụ: từ ViewModel hoặc khi copy node)
        /// </summary>
        public void NotifyTitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }
    }

}

