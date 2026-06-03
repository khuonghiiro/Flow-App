using FlowMy.Extensions;
using FlowMy.Helpers;
using FlowMy.Services;
using FlowMy.Services.Git;
using FlowMy.Services.Interfaces;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow;
using FlowMy.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace FlowMy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Lấy version từ Assembly, tự động đồng bộ với .csproj
        public static readonly string CurrentVersion = System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString(3) ?? "1.0.0"; // Format: Major.Minor.Build (1.0.3)

        #region Private Fields
        private bool _alreadyLoggedIn = false;
        private bool _isSessionEventRegistered = false;
        private ServiceProvider? _serviceProvider;
        private Mutex? _mutex;
        private CancellationTokenSource? _pipeServerCts;
        private ILogger<App>? _logger;

        // File logging
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FlowMy", "logs");
        private static readonly object LogFileLock = new object();

        #endregion

        #region Public Properties
        public static IServiceProvider? Services { get; private set; }
        #endregion

        #region Application Lifecycle
        protected override void OnStartup(StartupEventArgs e)
        {
            // ── Handle elevated sub-process for driver installation ──
            if (e.Args.Length > 0 && e.Args[0] == "--install-interception-driver")
            {
                int exitCode = 1;
                try
                {
                    bool ok = InputInterceptorNS.InputInterceptor.InstallDriver();
                    exitCode = ok ? 0 : 1;
                }
                catch { exitCode = 1; }
                Shutdown(exitCode);
                return;
            }

            try
            {
                SetupGlobalExceptionHandlers();
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;

                if (!CheckForSingleInstance())
                    return;

                InitializeApplication();

                ShowMainWindow();

                // ✅ Warm-up WebView2 trên UI thread (STA) sau khi main window đã hiển thị
                // Tránh gọi từ background thread để không bị lỗi RPC_E_CHANGED_MODE.
                _ = this.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await WebView2EnvironmentManager.WarmUpAsync();
                        _logger?.LogInformation("✅ WebView2 shared environment warmed up");
                    }
                    catch (Exception webViewEx)
                    {
                        _logger?.LogWarning(webViewEx, "⚠️ WebView2 warm-up failed, sẽ khởi tạo lazy khi cần");
                    }
                });

                // Không khởi tạo tray icon khi startup.
                try
                {
                    // Khởi tạo tray icon sớm để user có menu ghim widget.
                    var trayService = Services?.GetService<FlowMy.Services.Interfaces.ITrayService>();
                    trayService?.Initialize();
                }
                catch { }

                // Nếu driver đã cài từ trước → init hooks ngầm (không cần admin, không block UI)
                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        if (FlowMy.Helpers.InterceptionInputHelper.IsDriverInstalled())
                        {
                            FlowMy.Helpers.InterceptionInputHelper.EnsureDriverInstalled();
                            _logger?.LogInformation("Interception driver available: True");
                        }
                        // Nếu chưa cài → không làm gì, user sẽ cài khi chủ động chọn InterceptionDriver
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Interception driver init skipped");
                    }
                });
                base.OnStartup(e);

            }
            catch (Exception ex)
            {
                LogError("Startup error", ex);
                MessageBox.Show("Có lỗi xảy ra khi khởi động ứng dụng. Ứng dụng sẽ thoát.",
                               "Lỗi khởi động", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        // method check cập nhật phần mềm
        //private async Task CheckForUpdatesAsync()
        //{
        //    // ✅ Lấy thư mục của executable thay vì current directory
        //    // Current directory có thể là C:\WINDOWS\system32 khi chạy từ shortcut
        //    string appDirectory = GetApplicationDirectory();

        //    var config = new ConfigurationBuilder()
        //       .SetBasePath(appDirectory)
        //       .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        //       .Build();

        //}

        // ✅ SETUP GLOBAL EXCEPTION HANDLERS
        private void SetupGlobalExceptionHandlers()
        {
            // WPF UI thread exceptions
            Application.Current.DispatcherUnhandledException += (sender, e) =>
            {
                HandleGlobalException(e.Exception);
                e.Handled = true; // Prevent application crash
            };

            // Background thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    HandleGlobalException(ex);
                }
            };

            // Task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                HandleGlobalException(e.Exception);
                e.SetObserved(); // Prevent application crash
            };
        }

        // ✅ XỬ LÝ GLOBAL EXCEPTIONS
        private void HandleGlobalException(Exception ex)
        {
            try
            {
                // Ưu tiên log qua ILogger nếu có
                if (_logger != null)
                {
                    _logger.LogError(ex, "❌ Đã xảy ra ngoại lệ toàn cục chưa được xử lý");
                }
                else
                {
                    // Fallback khi logger chưa được khởi tạo (rất sớm trong vòng đời app)
                    System.Diagnostics.Debug.WriteLine($"[GLOBAL-EXCEPTION] {ex}");
                }

                // Log to file
                WriteLog("UNHANDLED EXCEPTION", ex.ToString());

                // Check if it's UnauthorizedAccessException from ApiService
                if (ex is UnauthorizedAccessException)
                {
                    _logger?.LogWarning("🔒 UnauthorizedAccessException in global handler");

                    MessageBox.Show(
                        "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại!",
                        "Hết phiên đăng nhập",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // For other exceptions, show generic error
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show(
                        "Có lỗi không mong muốn xảy ra. Vui lòng thử lại hoặc liên hệ bộ phận hỗ trợ.",
                        "Lỗi hệ thống",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
            catch (Exception handlerEx)
            {
                // Last resort logging
                System.Diagnostics.Debug.WriteLine($"❌ Error in global exception handler: {handlerEx}");
                WriteLog("ERROR IN EXCEPTION HANDLER", handlerEx.ToString());
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                CleanupResources();
            }
            catch (Exception ex)
            {
                LogError("Exit error", ex);
            }
            finally
            {
                base.OnExit(e);
            }
        }
        #endregion

        #region Initialization
        private void InitializeApplication()
        {
            RegisterViewModelAndView();
            InitTheme();
            RegisterEventAndShowLogin();
        }

        private bool CheckForSingleInstance()
        {
            bool createdNew;
            _mutex = new Mutex(true, "FlowMy_Singleton", out createdNew);

            if (!createdNew)
            {
                Shutdown();
                return false;
            }

            return true;
        }

        private void RegisterViewModelAndView()
        {
            var services = new ServiceCollection();

            services.AddRemoteServices();
            services.AddWorkflowEditorServices();
            services.AddViewModelsAndViews(typeof(App).Assembly);

            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider;

            _logger = _serviceProvider.GetRequiredService<ILogger<App>>();

            PreloadBasicServicesAsync();
        }


        private void InitTheme()
        {
            // Prefer the unified ColorThemeService preference (ThemePreference)
            // so MainWindow and WorkflowEditor keep the same selected theme.
            var colorThemeService = _serviceProvider?.GetService<ColorThemeService>();
            if (colorThemeService != null)
            {
                colorThemeService.LoadThemePreference();
                return;
            }

            ThemeExtensions.ApplyStoredTheme();
        }


        private void RegisterEventAndShowLogin()
        {
            ;

            // Setup startup registration (chỉ cần gọi 1 lần)
            RegisterAppToStartup.SetupStartupRegistration();

            // Debug info (optional)
#if DEBUG

            RegisterAppToStartup.DebugRegistrationInfo();

#endif
        }
        #endregion

        #region Session Events
        private void RegisterSessionSwitchEvent()
        {
            if (!_isSessionEventRegistered)
            {
                SystemEvents.SessionSwitch += OnSessionSwitch;
                _isSessionEventRegistered = true;
            }
        }

        private void UnregisterSessionSwitchEvent()
        {
            if (_isSessionEventRegistered)
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                _isSessionEventRegistered = false;
            }
        }

        private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock && !_alreadyLoggedIn)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    //ShowLoginSequence();
                });
            }
        }
        #endregion

        #region Window Management
        private void ShowLastActiveWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var lastWindow = GetLastActiveWindow();

                if (lastWindow != null)
                {
                    ShowAndBringToFront(lastWindow);
                }
                else
                {
                    ShowMainWindow();
                }
            });
        }

        private Window? GetLastActiveWindow()
        {
            return Current.Windows
                .Cast<Window>()
                .Where(w => w.IsLoaded)
                .OrderByDescending(w => w.IsActive)
                .ThenByDescending(w => w.Topmost)
                .FirstOrDefault();
        }

        private MainWindow? GetActiveMainWindow()
        {
            return Current.Windows
                .OfType<MainWindow>()
                .OrderByDescending(w => w.IsActive)
                .ThenByDescending(w => w.Topmost)
                .FirstOrDefault();
        }

        private void ShowMainWindow()
        {
            try
            {
                var mainWindow = GetActiveMainWindow();

                if (mainWindow == null || !mainWindow.IsLoaded)
                {
                    if (Services == null)
                        throw new InvalidOperationException("Services chưa khởi tạo");

                    mainWindow = Services.GetRequiredService<MainWindow>();
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();
                }
                else
                {
                    ShowAndBringToFront(mainWindow);
                }

// 🧪 DEBUG: Uncomment sau khi build thành công lần đầu
//#if DEBUG
//                // 🧪 DEBUG ONLY: Mở test windows
//                _ = Dispatcher.InvokeAsync(() =>
//                {
//                    try
//                    {
//                        // Background Input Test Window
//                        var testWindow = new Views.BackgroundInputTestWindow();
//                        testWindow.Show();
//                        testWindow.Left = mainWindow.Left + mainWindow.Width + 20;
//                        testWindow.Top = mainWindow.Top;
//                        _logger?.LogInformation("🧪 Background Input Test Window opened");
//
//                        // Embedded Window Test Dialog
//                        System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
//                        {
//                            Dispatcher.Invoke(() =>
//                            {
//                                var embedDialog = new Views.EmbeddedWindowTestDialog();
//                                embedDialog.Show();
//                                embedDialog.Left = mainWindow.Left;
//                                embedDialog.Top = mainWindow.Top + mainWindow.Height + 50;
//                                _logger?.LogInformation("🧪 Embedded Window Test Dialog opened");
//                            });
//                        });
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger?.LogWarning(ex, "Failed to open test windows");
//                    }
//                }, System.Windows.Threading.DispatcherPriority.Background);
//#endif
            }
            catch (Exception ex)
            {
                LogError("Show main window error", ex);
            }
        }

        private void ShowAndBringToFront(Window window)
        {
            if (window.Visibility != Visibility.Visible)
                window.Show();

            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            WindowHelper.BringToFront(window);
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Chỉ preload ViewCacheService, KHÔNG khởi tạo bất kỳ ViewModel nào
        /// </summary>
        private void PreloadBasicServicesAsync()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // ✅ CHỈ preload ViewCacheService - không động chạm gì đến ViewModel
                    var viewCache = _serviceProvider.GetRequiredService<IViewCacheService>();
                    _logger.LogInformation("✅ ViewCacheService initialized");

                    await Task.Delay(100); // Giảm delay
                    _logger.LogInformation("✅ Basic services preloaded (NO ViewModels initialized)");
                }
                catch (Exception ex)
                {
                    LogError("Preload basic services error", ex);
                }
            });
        }

        private void CleanupResources()
        {
            try
            {
                UnregisterSessionSwitchEvent();

                _pipeServerCts?.Cancel();
                _pipeServerCts?.Dispose();

                // Kill tất cả cmd process của Git repos
                GitCmdProcessManager.KillAll();

                // Dọn dẹp tray service
                var trayService = _serviceProvider?.GetService<ITrayService>();
                trayService?.Dispose();

                var viewCache = _serviceProvider?.GetService<IViewCacheService>();
                if (viewCache is IDisposable disposableCache)
                {
                    disposableCache.Dispose();
                    _logger?.LogInformation("ViewCacheService disposed");
                }

                _serviceProvider?.Dispose();
                _serviceProvider = null;
                Services = null;

                _mutex?.Dispose();
            }
            catch (Exception ex)
            {
                LogError("Cleanup resources error", ex);
            }
        }

        private void LogError(string message, Exception ex)
        {
            if (_logger != null)
            {
                _logger.LogError(ex, message);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] {message}: {ex}");
            }

            // Log to file
            WriteLog(message, ex.ToString());
        }

        /// <summary>
        /// Lấy thư mục thực tế của ứng dụng (thư mục chứa exe)
        /// </summary>
        private static string GetApplicationDirectory()
        {
            try
            {
                // Thử 1: ProcessPath (tốt nhất cho .NET 6+)
                string? processPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                {
                    return Path.GetDirectoryName(processPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                }

                // Thử 2: Assembly Location
                string? assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyLocation) && File.Exists(assemblyLocation))
                {
                    return Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
                }

                // Thử 3: BaseDirectory
                return AppDomain.CurrentDomain.BaseDirectory;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        /// <summary>
        /// Ghi log vào file trong thư mục Documents/FlowMy/logs
        /// File log được đặt tên theo ngày hiện tại (yyyy_MM_dd.log)
        /// </summary>
        /// <param name="category">Loại log (ví dụ: ERROR, INFO, WARNING)</param>
        /// <param name="message">Nội dung log</param>
        private static void WriteLog(string category, string message)
        {
            try
            {
                // Tạo thư mục nếu chưa tồn tại
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                // Tạo tên file theo format yyyy_MM_dd.log
                string fileName = $"{DateTime.Now:yyyy_MM_dd}.log";
                string filePath = Path.Combine(LogDirectory, fileName);

                // Format log entry
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [{category}] {message}{Environment.NewLine}{Environment.NewLine}";

                // Thread-safe file write
                lock (LogFileLock)
                {
                    File.AppendAllText(filePath, logEntry);
                }
            }
            catch
            {
                // Không làm gì nếu không ghi được log file
                // Tránh gây lỗi khi xử lý lỗi
            }
        }

        #endregion
    }
}