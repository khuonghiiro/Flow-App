namespace FlowMy.Models
{
    /// <summary>
    /// DTO đại diện cho một thao tác đơn lẻ được ghi lại (click chuột, nhấn phím, di chuyển chuột, scroll).
    /// </summary>
    public sealed class MacroAction
    {
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Loại thao tác: "MouseClick" | "MouseDown" | "MouseUp" | "KeyPress" | "MouseMove" | "MouseScroll"
        /// </summary>
        public string Type { get; set; } = string.Empty;

        public long Timestamp { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>
        /// Nút chuột: "Left" | "Right" | "Middle" | null.
        /// Modifier combos được lưu riêng trong CtrlHeld/ShiftHeld/AltHeld.
        /// </summary>
        public string? Button { get; set; }

        /// <summary>Tên phím (ví dụ: "A", "Enter", "F5") — chỉ dùng với KeyPress.</summary>
        public string? Key { get; set; }

        public int ScrollDelta { get; set; }

        // ─── Modifier key state tại thời điểm action ─────────────────────────────
        public bool ShiftHeld { get; set; }
        public bool CtrlHeld  { get; set; }
        public bool AltHeld   { get; set; }
    }
}
