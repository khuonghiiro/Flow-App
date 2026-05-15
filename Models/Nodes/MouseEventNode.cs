namespace FlowMy.Models
{
    public sealed class MouseEventNode : WorkflowNode
    {
        private string _mouseButton = "Left"; // Left, Right, Middle, ScrollUp, ScrollDown
        private int _repeatCount = 1;
        private double _holdDuration = 0; // Giây
        private int _scrollSpeed = 1; // Chỉ dùng cho scroll

        public MouseEventNode()
        {
            Type = NodeType.MouseEvent;
            Title = "Mouse Event";

            // Dynamic input: Repeat Count (giống pattern keyboard guide)
            // DisplayName = "Số lần" để hiển thị trong data panel chrome
            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "repeatCount",
                DisplayName = "Số lần",
                IsMultiple = false,
                ConvertType = WorkflowDataType.Number
            });

            // Note: holdDuration và scrollSpeed chỉ là properties, không phải dynamic inputs
            // Chúng được set trực tiếp trong node control, không cần data panel
        }

        /// <summary>
        /// Loại nút chuột: Left, Right, Middle, ScrollUp, ScrollDown
        /// </summary>
        public string MouseButton
        {
            get => _mouseButton;
            set
            {
                if (_mouseButton == value) return;
                _mouseButton = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Số lần nhấn/lăn
        /// </summary>
        public int RepeatCount
        {
            get => _repeatCount;
            set
            {
                if (_repeatCount == value) return;
                _repeatCount = value < 1 ? 1 : value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Thời gian giữ chuột (giây) - chỉ áp dụng cho Left/Right/Middle
        /// </summary>
        public double HoldDuration
        {
            get => _holdDuration;
            set
            {
                if (_holdDuration == value) return;
                _holdDuration = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Tốc độ lăn chuột - chỉ áp dụng cho ScrollUp/ScrollDown
        /// </summary>
        public int ScrollSpeed
        {
            get => _scrollSpeed;
            set
            {
                if (_scrollSpeed == value) return;
                _scrollSpeed = value < 1 ? 1 : value;
                OnPropertyChanged();
            }
        }
    }
}
