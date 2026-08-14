using System;
using System.Windows;
using System.Windows.Shapes;

namespace FlowMy.Models
{
    /// <summary>
    /// Enum cho vị trí port trên node
    /// </summary>
    public enum PortPosition
    {
        Left,
        Right,
        Top,
        Bottom
    }

    /// <summary>
    /// Enum xác định cách thực thi các output ports
    /// </summary>
    public enum PortExecutionMode
    {
        Sequential, // Thực thi tuần tự (port này hoàn thành mới chạy port tiếp theo)
        Parallel    // Thực thi song song (tất cả ports cùng lúc)
    }

    /// <summary>
    /// Model đại diện cho một Port
    /// </summary>
    public class NodePort
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public bool IsInput { get; set; } // True = input port, False = output port
        public PortPosition Position { get; set; } = PortPosition.Left;
        public Point PositionPoint { get; set; } // Vị trí trên canvas
        /// <summary>UI của port - có thể là Ellipse (tròn) hoặc Rectangle (chữ nhật dọc).</summary>
        public FrameworkElement? PortUI { get; set; }
        public bool IsVisible { get; set; } = true; // Có hiển thị hay không
        public bool CanDeleteConnection { get; set; } = true; // Có cho phép xóa connection từ port này không
        public string? ColorKey { get; set; } // Key màu từ Resource (nếu có)
        
        /// <summary>
        /// Thứ tự thực thi của port này (chỉ áp dụng cho output ports).
        /// Port có ExecutionOrder nhỏ hơn sẽ chạy trước.
        /// Mặc định: 0 (chạy đầu tiên)
        /// </summary>
        public int ExecutionOrder { get; set; } = 0;
        
        /// <summary>
        /// Chế độ thực thi của port này (chỉ áp dụng cho output ports).
        /// Sequential: Port này hoàn thành mới chạy port tiếp theo.
        /// Parallel: Port này chạy song song với các ports khác cùng ExecutionMode.
        /// Mặc định: Sequential
        /// </summary>
        public PortExecutionMode ExecutionMode { get; set; } = PortExecutionMode.Sequential;
    }
}

