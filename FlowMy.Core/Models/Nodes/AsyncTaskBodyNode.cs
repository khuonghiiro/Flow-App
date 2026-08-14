using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
namespace FlowMy.Models
{
    /// <summary>
    /// Vùng body cho AsyncTask ở chế độ giao diện giống Loop — node ảo, quản lý bởi AsyncTaskNode.
    /// </summary>
    public sealed class AsyncTaskBodyNode : WorkflowNode
    {
        public AsyncTaskNode ParentAsyncTaskNode { get; set; } = null!;

        public const double DefaultVerticalOffset = 400;
        public const double DefaultHorizontalOffset = -150;

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

        public AsyncTaskBodyNode()
        {
            Type = NodeType.Generic;
            Title = "Async Task Body";
            NodeBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50));
        }

        public void SyncPositionWithParent()
        {
            if (ParentAsyncTaskNode == null) return;
            double newX = ParentAsyncTaskNode.X + 50 - (_width / 2);
            double newY = ParentAsyncTaskNode.Y + DefaultVerticalOffset;
            X = newX;
            Y = newY;
        }
    }
}
