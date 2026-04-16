using FlowMy.Services.Interfaces;
using FlowMy.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Drawing;

namespace FlowMy.Services
{
    /// <summary>
    /// Triển khai ITrayService: quản lý NotifyIcon + menu tray.
    /// Toàn bộ logic tray tập trung ở đây để dễ mở rộng & test.
    /// </summary>
    public class TrayService : ITrayService
    {
        private readonly ILogger<TrayService> _logger;
        private Forms.NotifyIcon? _trayIcon;

        public bool IsExitRequested { get; private set; }

        public TrayService(ILogger<TrayService> logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            try
            {
                if (_trayIcon != null)
                {
                    // Đã khởi tạo rồi
                    return;
                }

                _trayIcon = new Forms.NotifyIcon
                {
                    Text = "FlowMy",
                    Icon = SystemIcons.Application, // TODO: thay bằng icon riêng nếu có
                    Visible = true
                };

                var menu = new Forms.ContextMenuStrip();

                // Mở/hiện cửa sổ chính
                var openItem = new Forms.ToolStripMenuItem("Mở cửa sổ chính");
                openItem.Click += (s, e) => ShowMainWindow();
                menu.Items.Add(openItem);

                // Ví dụ 1 action nền — sau này bạn có thể thay thế/gắn vào service thật
                var actionItem = new Forms.ToolStripMenuItem("Chạy tác vụ mẫu");
                actionItem.Click += (s, e) => ExecuteTrayAction("SampleAction");
                menu.Items.Add(actionItem);

                menu.Items.Add(new Forms.ToolStripSeparator());

                // Thoát hẳn ứng dụng
                var exitItem = new Forms.ToolStripMenuItem("Thoát");
                exitItem.Click += (s, e) => ExitFromTray();
                menu.Items.Add(exitItem);

                _trayIcon.ContextMenuStrip = menu;

                // Double-click để mở lại cửa sổ chính
                _trayIcon.DoubleClick += (s, e) => ShowMainWindow();

                _logger.LogInformation("TrayService initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initialize tray icon error");
            }
        }

        /// <summary>
        /// Mở/hiện lại MainWindow từ tray.
        /// </summary>
        private void ShowMainWindow()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = Application.Current.Windows
                        .OfType<MainWindow>()
                        .OrderByDescending(w => w.IsActive)
                        .ThenByDescending(w => w.Topmost)
                        .FirstOrDefault();

                    if (mainWindow == null || !mainWindow.IsLoaded)
                    {
                        // Nếu vì lý do nào đó MainWindow chưa được set, fallback dùng Services
                        if (App.Services == null)
                        {
                            throw new InvalidOperationException("Services chưa khởi tạo");
                        }

                        mainWindow = App.Services.GetRequiredService<MainWindow>();
                        Application.Current.MainWindow = mainWindow;
                        mainWindow.Show();
                    }
                    else
                    {
                        if (mainWindow.Visibility != Visibility.Visible)
                            mainWindow.Show();

                        if (mainWindow.WindowState == WindowState.Minimized)
                            mainWindow.WindowState = WindowState.Normal;

                        mainWindow.Activate();
                        mainWindow.Topmost = true;  // Bring to front
                        mainWindow.Topmost = false;
                        mainWindow.Focus();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Show main window from tray error");
            }
        }

        /// <summary>
        /// Nơi tập trung xử lý các action từ tray theo key.
        /// Sau này chỉ cần thêm case mới là xong.
        /// </summary>
        private void ExecuteTrayAction(string actionKey)
        {
            try
            {
                switch (actionKey)
                {
                    case "SampleAction":
                        // TODO: thay thế bằng logic thật (gọi service, chạy auto-click, v.v.)
                        MessageBox.Show("Tác vụ mẫu từ tray đang chạy...", "FlowMy",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        break;

                    // case "YourNewActionKey":
                    //     // TODO: xử lý logic mới ở đây
                    //     break;

                    default:
                        _logger.LogWarning("Unknown tray action: {ActionKey}", actionKey);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Execute tray action error ({ActionKey})", actionKey);
            }
        }

        /// <summary>
        /// Thoát app từ icon tray: đặt cờ, đóng MainWindow.
        /// </summary>
        private void ExitFromTray()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsExitRequested = true;
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.Close();
                    }
                    else
                    {
                        // Không có MainWindow thì shutdown luôn
                        Application.Current.Shutdown();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exit from tray error");
                Application.Current.Shutdown();
            }
        }

        public void ResetExitRequestFlag()
        {
            IsExitRequested = false;
        }

        public void Dispose()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
    }
}


