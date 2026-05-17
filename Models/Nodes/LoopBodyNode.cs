using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace FlowMy.Models
{
    /// <summary>
    /// Node đại diện cho Loop Body (vùng chứa logic bên trong loop)
    /// Đây là một node ảo, không hiển thị trong danh sách nodes chính,
    /// mà được quản lý hoàn toàn bởi LoopNode.
    /// 
    /// LoopBodyNode luôn nằm dưới LoopNode với offset cố định (trừ khi user kéo riêng).
    /// </summary>
    public sealed class LoopBodyNode : WorkflowNode
    {
        public LoopNode ParentLoopNode { get; set; } = null!;

        /// <summary>
        /// Khoảng cách dọc mặc định từ LoopNode đến LoopBodyNode
        /// </summary>
        public const double DefaultVerticalOffset = 400;

        /// <summary>
        /// Khoảng cách ngang mặc định (center aligned với LoopNode)
        /// </summary>
        public const double DefaultHorizontalOffset = -150; // Centered below diamond (width 400, diamond 100 -> offset -150)

        private double _width = 800;
        private double _height = 400;

        public double Width
        {
            get => _width;
            set
            {
                if (_width != value && value >= 200)
                {
                    _width = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                if (_height != value && value >= 200)
                {
                    _height = value;
                    OnPropertyChanged();
                }
            }
        }

        public LoopBodyNode()
        {
            Type = NodeType.Generic;
            Title = "Loop Body";
            NodeBrush = new SolidColorBrush(Colors.Orange);
        }

        /// <summary>
        /// Tính vị trí mặc định dựa trên vị trí của LoopNode cha
        /// </summary>
        public void SyncPositionWithParent()
        {
            if (ParentLoopNode == null) return;

            // Center the body below the diamond (diamond width = 100, body default width = 400)
            double newX = ParentLoopNode.X + 50 - (_width / 2); // 50 = diamond center
            double newY = ParentLoopNode.Y + DefaultVerticalOffset;

            X = newX;
            Y = newY;
        }

        /// <summary>
        /// Tính vị trí mặc định với offset tùy chỉnh
        /// </summary>
        public void SyncPositionWithParent(double horizontalOffset, double verticalOffset)
        {
            if (ParentLoopNode == null) return;

            X = ParentLoopNode.X + horizontalOffset;
            Y = ParentLoopNode.Y + verticalOffset;
        }
    }
}
