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
        /// Cập nhật danh sách widget được ghim trong menu tray.
        /// Khi user click vào một widget trong tray, callback sẽ được gọi với NodeId.
        /// </summary>
        void SetPinnedWidgets(
            IEnumerable<(string NodeId, string Label)> widgets,
            Action<string> onWidgetClick);

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


