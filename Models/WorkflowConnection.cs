using System;
using System.Windows.Controls;
using System.Windows;
using ShapesPath = System.Windows.Shapes.Path;

namespace FlowMy.Models
{
    /// <summary>
    /// Model đại diện cho đường kết nối giữa các Node
    /// </summary>
    public class WorkflowConnection
    {
        public WorkflowNode FromNode { get; set; } = null!;
        public WorkflowNode ToNode { get; set; } = null!;

        // Ports (có thể null để backward compatibility)
        public NodePort? FromPort { get; set; }
        public NodePort? ToPort { get; set; }

        // UI elements
        public ShapesPath? LineUI { get; set; }
        public ShapesPath? EnergyUI { get; set; } // Overlay hiệu ứng "năng lượng" (chỉ khi đang chạy)
        public System.Windows.Shapes.Ellipse? EnergyBallUI { get; set; } // Quả bóng năng lượng chạy dọc theo line
        public FrameworkElement? EnergyTextUI { get; set; } // Text chạy dọc theo line (nếu user nhập)
        public ShapesPath? HitArea { get; set; } // Invisible hit area để hover mượt mà
        public Button? DeleteButton { get; set; }

        /// <summary>
        /// Chỉ dùng cho UI khi đang chạy workflow: connection đang được "kích hoạt"
        /// để hiển thị hiệu ứng truyền năng lượng vào node đích.
        /// </summary>
        public bool IsExecutionActive { get; set; }

        /// <summary>
        /// Khi true: connection sẽ được "ghim" hiệu ứng năng lượng và không bị tắt
        /// khi ActiveExecutionConnection chuyển sang connection khác.
        /// Dùng cho trường hợp Loop -> LoopBody: muốn energy giữ nguyên trong suốt body execution.
        /// </summary>
        public bool IsExecutionPinned { get; set; }

        // Backward compatibility
        public bool IsFromInput { get; set; } = false;

        public bool IsDeleteVisible { get; set; } = true;

        // Windy line (gió thổi) state – chỉ dùng cho style ConnectionLineStyle.Windy
        public double WindOffset { get; set; }          // biên độ lệch hiện tại (px) - cho elastic effect
        public double WindVelocity { get; set; }        // vận tốc "gió" (px/frame-ish) - cho elastic effect
        public bool IsWindActive { get; set; }          // còn đang đung đưa hay đã dừng
        public Point? WindBaseControlPoint1 { get; set; } // control point gốc 1 (Bezier)
        public Point? WindBaseControlPoint2 { get; set; } // control point gốc 2 (Bezier)
        public Vector WindNormal { get; set; }          // vector pháp tuyến chuẩn hóa của đoạn chính
        public DateTime LastWindUpdate { get; set; }    // thời điểm frame update gần nhất
        public double WindTime { get; set; }            // thời gian tổng (giây)
        public double NextWaveTime { get; set; }        // thời gian đến lần wave tiếp theo (giây)
        public double WaveProgress { get; set; }        // vị trí wave (0.0 = đầu trái, 1.0 = đầu phải)
        public bool IsWaveActive { get; set; }          // wave đang chạy hay không
        public double WaveAmplitude { get; set; }       // biên độ của wave (px)
        
        // ✅ PERFORMANCE: Path geometry cache để tránh recalculate khi nodes di chuyển nhỏ
        public FlowMy.Services.Rendering.ConnectionRenderer.ConnectionPathCache? PathCache { get; set; }
    }
}

