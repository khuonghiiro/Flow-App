using FlowMy.Models;
using System.Windows;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        /// <summary>
        /// Tính điểm rút ngắn dựa trên hướng port (để tạo khoảng cách giữa arrow và port)
        /// (đang dùng trong khi kéo temp connection)
        /// </summary>
        private Point ShortenPoint(Point point, PortPosition direction, double gap = 2)
        {
            // Tổng offset = bán kính port (9) + stroke/2 (1) + gap
            double totalOffset = gap + 9 + 1;

            return direction switch
            {
                PortPosition.Right => new Point(point.X - totalOffset, point.Y),
                PortPosition.Left => new Point(point.X + totalOffset, point.Y),
                PortPosition.Bottom => new Point(point.X, point.Y - totalOffset),
                PortPosition.Top => new Point(point.X, point.Y + totalOffset),
                _ => point
            };
        }
    }
}

