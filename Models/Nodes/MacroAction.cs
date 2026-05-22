namespace FlowMy.Models
{
    /// <summary>
    /// DTO đại diện cho một thao tác đơn lẻ được ghi lại (click chuột, nhấn phím, di chuyển chuột, scroll).
    /// </summary>
    public sealed class MacroAction
    {
        /// <summary>
        /// Số thứ tự của action, tăng dần bắt đầu từ 1.
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Loại thao tác: "MouseClick" | "KeyPress" | "MouseMove" | "MouseScroll"
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Unix timestamp milliseconds tại thời điểm sự kiện xảy ra.
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// Tọa độ X của con trỏ chuột tại thời điểm sự kiện.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Tọa độ Y của con trỏ chuột tại thời điểm sự kiện.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Nút chuột được nhấn: "Left" | "Right" | "ShiftLeft" | null.
        /// </summary>
        public string? Button { get; set; }

        /// <summary>
        /// Tên phím được nhấn (ví dụ: "A", "Enter", "F5") hoặc null.
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// Số notch scroll (dương = lên, âm = xuống). Chỉ dùng với MouseScroll.
        /// </summary>
        public int ScrollDelta { get; set; }
    }
}
