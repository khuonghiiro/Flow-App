using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FlowMy.Models
{
    /// <summary>
    /// Model cho Screen Position Picker Node - node đặc biệt để chọn vị trí trên màn hình
    /// </summary>
    public class ScreenPositionPickerNode : WorkflowNode
    {
        private Point _selectedPosition;
        private bool _hasPosition;

        /// <summary>
        /// Vị trí đã chọn trên màn hình (tọa độ tuyệt đối)
        /// </summary>
        public Point SelectedPosition
        {
            get => _selectedPosition;
            set
            {
                if (_selectedPosition == value) return;
                _selectedPosition = value;
                HasPosition = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PositionText));
            }
        }

        /// <summary>
        /// Kiểm tra xem đã chọn vị trí chưa
        /// </summary>
        public bool HasPosition
        {
            get => _hasPosition;
            set
            {
                if (_hasPosition == value) return;
                _hasPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PositionText));
            }
        }

        /// <summary>
        /// Text hiển thị tọa độ (X: 123, Y: 456)
        /// </summary>
        public string PositionText => _hasPosition
            ? $"X: {(int)_selectedPosition.X}, Y: {(int)_selectedPosition.Y}"
            : "Chưa chọn vị trí";

        public ScreenPositionPickerNode()
        {
            Type = NodeType.ScreenPosition;
            Title = "Screen Position";
            _hasPosition = false;
            _selectedPosition = new Point(0, 0);
        }

        public void ClearPosition()
        {
            HasPosition = false;
            _selectedPosition = new Point(0, 0);
            OnPropertyChanged(nameof(SelectedPosition));
        }
    }
}
