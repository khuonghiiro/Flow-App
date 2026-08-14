// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
namespace FlowMy.Models.ImageEditor
{
    /// <summary>Chế độ blend khi composite nhiều layer.</summary>
    public enum BlendMode
    {
        /// <summary>Alpha blend chuẩn.</summary>
        Normal = 0,

        /// <summary>Nhân pixel (darken).</summary>
        Multiply = 1,

        /// <summary>Nghịch nhân (lighten).</summary>
        Screen = 2,

        /// <summary>Kết hợp Multiply + Screen theo luminance.</summary>
        Overlay = 3,

        /// <summary>Giữ pixel tối hơn.</summary>
        Darken = 4,

        /// <summary>Giữ pixel sáng hơn.</summary>
        Lighten = 5
    }
}
