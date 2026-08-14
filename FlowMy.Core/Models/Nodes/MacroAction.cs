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

        /// <summary>Tọa độ màn hình tuyệt đối (screen pixels). Dùng khi ExecutionMode = Free.</summary>
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>
        /// Tọa độ tương đối so với client area của target app (0.0 – 1.0).
        /// Chỉ có giá trị khi ExecutionMode = TargetApp (RelX > 0 hoặc RelY > 0).
        /// Khi playback, executor convert về screen coords dựa trên kích thước thực tế của app.
        /// </summary>
        public double RelX { get; set; }
        public double RelY { get; set; }

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
