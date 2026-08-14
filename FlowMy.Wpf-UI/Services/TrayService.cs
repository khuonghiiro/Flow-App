using FlowMy.Services.Interfaces;
using FlowMy.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Windows.Resources;

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
        private Forms.ContextMenuStrip? _menu;
        private Forms.ToolStripMenuItem? _pinnedRoot;
        private Action<string>? _onPinnedWidgetClick;

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
                    Icon = LoadAppTrayIcon() ?? SystemIcons.Application,
                    Visible = true
                };

                _menu = new Forms.ContextMenuStrip();

                // Mở/hiện cửa sổ chính
                var openItem = new Forms.ToolStripMenuItem("Mở cửa sổ chính");
                openItem.Click += (s, e) => ShowMainWindow();
                _menu.Items.Add(openItem);

                _menu.Items.Add(new Forms.ToolStripSeparator());

                _pinnedRoot = new Forms.ToolStripMenuItem("Widget đã ghim");
                _pinnedRoot.DropDownItems.Add(new Forms.ToolStripMenuItem("(chưa ghim widget nào)") { Enabled = false });
                _menu.Items.Add(_pinnedRoot);

                _menu.Items.Add(new Forms.ToolStripSeparator());

                // Thoát hẳn ứng dụng
                var exitItem = new Forms.ToolStripMenuItem("Thoát");
                exitItem.Click += (s, e) => ExitFromTray();
                _menu.Items.Add(exitItem);

                _trayIcon.ContextMenuStrip = _menu;

                // Double-click để mở lại cửa sổ chính
                _trayIcon.DoubleClick += (s, e) => ShowMainWindow();

                _logger.LogInformation("TrayService initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initialize tray icon error");
            }
        }

        public void SetPinnedWidgets(IEnumerable<(string NodeId, string Label)> widgets, Action<string> onWidgetClick)
        {
            try
            {
                Initialize();
                _onPinnedWidgetClick = onWidgetClick;
                if (_pinnedRoot == null) return;

                var items = (widgets ?? Enumerable.Empty<(string NodeId, string Label)>())
                    .Where(w => !string.IsNullOrWhiteSpace(w.NodeId))
                    .GroupBy(w => w.NodeId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(w => w.Label)
                    .ToList();

                _pinnedRoot.DropDownItems.Clear();
                if (items.Count == 0)
                {
                    _pinnedRoot.DropDownItems.Add(new Forms.ToolStripMenuItem("(chưa ghim widget nào)") { Enabled = false });
                    return;
                }

                foreach (var w in items)
                {
                    var mi = new Forms.ToolStripMenuItem(w.Label);
                    mi.Tag = w.NodeId;
                    mi.Click += (_, __) =>
                    {
                        try
                        {
                            var id = mi.Tag as string;
                            if (string.IsNullOrWhiteSpace(id)) return;
                            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                            {
                                try { _onPinnedWidgetClick?.Invoke(id); } catch { }
                            }));
                        }
                        catch { }
                    };
                    _pinnedRoot.DropDownItems.Add(mi);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetPinnedWidgets error");
            }
        }

        private static Icon? LoadAppTrayIcon()
        {
            try
            {
                // Ưu tiên load từ WPF Resource (đã embed vào assembly khi build).
                var resourceUri = new Uri("pack://application:,,,/Assets/Images/Auto_Click.ico", UriKind.Absolute);
                StreamResourceInfo? resourceInfo = Application.GetResourceStream(resourceUri);
                if (resourceInfo?.Stream != null)
                {
                    using var memory = new MemoryStream();
                    resourceInfo.Stream.CopyTo(memory);
                    memory.Position = 0;
                    return new Icon(memory);
                }
            }
            catch { }

            try
            {
                // Fallback: icon cạnh file exe (nếu có copy ra output).
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var icoPath = Path.Combine(baseDir, "Assets", "Images", "Auto_Click.ico");
                if (File.Exists(icoPath))
                    return new Icon(icoPath);
            }
            catch { }

            try
            {
                // Fallback cuối: lấy icon của executable.
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                    return Icon.ExtractAssociatedIcon(exePath);
            }
            catch { }
            return null;
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
            _menu?.Dispose();
            _menu = null;
            _pinnedRoot = null;
            _onPinnedWidgetClick = null;
        }
    }
}


