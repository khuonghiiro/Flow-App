using System;

namespace FlowMy.Services.Interfaces
{
    /// <summary>
    /// Service quản lý System Tray (NotifyIcon) cho ứng dụng.
    /// Mục tiêu: tách toàn bộ logic tray ra khỏi App / View, dễ maintain & mở rộng.
    /// </summary>
    public interface ITrayService : IDisposable
    {
        /// <summary>
        /// Khởi tạo tray icon, menu, event handler.
        /// Gọi sau khi DI container đã sẵn sàng và MainWindow đã được hiển thị.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Cờ cho biết người dùng đang yêu cầu thoát ứng dụng từ tray.
        /// View có thể dùng để phân biệt ẩn xuống tray vs. thoát hẳn app.
        /// </summary>
        bool IsExitRequested { get; }

        /// <summary>
        /// Reset cờ thoát khi người dùng hủy confirm thoát.
        /// </summary>
        void ResetExitRequestFlag();
    }
}


