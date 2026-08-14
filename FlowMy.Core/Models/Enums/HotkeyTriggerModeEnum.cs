namespace FlowMy.Models.Enums
{
    /// <summary>
    /// Enum cho các chế độ kích hoạt hotkey
    /// </summary>
    public enum HotkeyTriggerModeEnum
    {
        /// <summary>
        /// Chế độ gửi phím: workflow tự động nhấn phím/tổ hợp phím (logic hiện tại)
        /// </summary>
        Send,

        /// <summary>
        /// Chế độ nghe phím: workflow chờ người dùng nhấn phím/tổ hợp phím đúng rồi mới tiếp tục
        /// </summary>
        Listen
    }
}
