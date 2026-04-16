using System;

namespace FlowMy.Services.Rendering
{
    public enum ConnectionColorMode
    {
        NodeColor,
        CustomColor
    }

    public enum ConnectionEnergyColorMode
    {
        /// <summary>
        /// Màu năng lượng lấy theo màu line hiện tại (sau khi áp dụng ConnectionColorMode).
        /// </summary>
        FollowLineColor,

        /// <summary>
        /// Màu năng lượng do user chọn riêng.
        /// </summary>
        CustomColor
    }

    public enum ConnectionLineStyle
    {
        /// <summary>Mặc định cong mượt (cubic bezier).</summary>
        Bezier,

        /// <summary>Vuông góc, có bo góc nhẹ.</summary>
        Orthogonal,

        /// <summary>Đường thẳng đơn giản.</summary>
        Straight,

        /// <summary>Vuông góc nhưng bo tròn mạnh hơn, cảm giác “flowy” hơn.</summary>
        SmoothOrthogonal,

        /// <summary>Cung tròn / đường cong mềm giữa 2 node.</summary>
        Arc,

        /// <summary>Đường cong chữ S, ưu tiên tỏa quạt từ node nguồn sang nhiều node đích.</summary>
        RadialFanout,

        /// <summary>Kiểu “gió thổi” – line cong mềm, đung đưa khi có tác động.</summary>
        Windy,

        /// <summary>Vuông góc thông minh – tránh node bằng A* obstacle avoidance.</summary>
        OrthogonalV2
    }
}

