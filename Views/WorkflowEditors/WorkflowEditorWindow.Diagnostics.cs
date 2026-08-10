using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private static readonly object _logFileLock = new();
        private static string? _logFilePath;

        // UI Watchdog monitor
        private Thread? _watchdogThread;
        private CancellationTokenSource? _watchdogCts;
        private DateTime _lastUiPingTime = DateTime.UtcNow;
        private DateTime _lastUiPongTime = DateTime.UtcNow;
        private volatile bool _isUiResponsive = true;
        private int _uiFreezeDetectedCount = 0;

        /// <summary>
        /// Đường dẫn tới file log chẩn đoán đứng UI canvas.
        /// </summary>
        public static string LogFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_logFilePath))
                {
                    try
                    {
                        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        var logDir = Path.Combine(appData, "FlowMy", "Logs");
                        if (!Directory.Exists(logDir))
                        {
                            Directory.CreateDirectory(logDir);
                        }
                        _logFilePath = Path.Combine(logDir, "canvas_ui_freeze_debug.log");
                    }
                    catch
                    {
                        _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "canvas_ui_freeze_debug.log");
                    }
                }
                return _logFilePath;
            }
        }

        private static EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs>? _firstChanceHandler;
        private static DateTime _lastExceptionLogTime = DateTime.MinValue;

        /// <summary>
        /// Khởi tạo Watchdog theo dõi UI thread latency & freeze và lắng nghe FirstChanceException.
        /// </summary>
        private void InitializeCanvasDiagnostics()
        {
            WriteCanvasLog("INIT", "WorkflowEditorWindow diagnostics logger initialized. Log file: " + LogFilePath);
            StartCanvasUiWatchdog();

            if (_firstChanceHandler == null)
            {
                _firstChanceHandler = (s, e) =>
                {
                    try
                    {
                        var ex = e.Exception;
                        if (ex == null) return;

                        var src = ex.Source ?? string.Empty;
                        // Bắt các exception thuộc WPF / UI / WindowsBase / PresentationCore / CollectionView
                        if (ex is InvalidOperationException ||
                            src.Contains("WindowsBase", StringComparison.OrdinalIgnoreCase) ||
                            src.Contains("PresentationCore", StringComparison.OrdinalIgnoreCase) ||
                            src.Contains("PresentationFramework", StringComparison.OrdinalIgnoreCase))
                        {
                            var now = DateTime.UtcNow;
                            if ((now - _lastExceptionLogTime).TotalMilliseconds > 300)
                            {
                                _lastExceptionLogTime = now;
                                WriteCanvasLog("FIRST_CHANCE_EX", $"Caught [{ex.GetType().Name}] in {src}: {ex.Message}\nStack: {ex.StackTrace}", isAlert: true);
                            }
                        }
                    }
                    catch { }
                };

                AppDomain.CurrentDomain.FirstChanceException += _firstChanceHandler;
            }
        }

        /// <summary>
        /// Bắt đầu watchdog giám sát UI thread freeze ở luồng riêng (Background Thread).
        /// </summary>
        private void StartCanvasUiWatchdog()
        {
            StopCanvasUiWatchdog();

            _watchdogCts = new CancellationTokenSource();
            var token = _watchdogCts.Token;

            _watchdogThread = new Thread(() => WatchdogLoop(token))
            {
                IsBackground = true,
                Name = "CanvasUiWatchdogThread",
                Priority = ThreadPriority.BelowNormal
            };
            _watchdogThread.Start();

            LogCanvasDiagnostic("WATCHDOG", "Started UI Thread Watchdog monitor (check interval: 1s, freeze threshold: 2s)");
        }

        /// <summary>
        /// Dừng watchdog khi window đóng.
        /// </summary>
        private void StopCanvasUiWatchdog()
        {
            if (_watchdogCts != null)
            {
                try { _watchdogCts.Cancel(); } catch { }
                _watchdogCts.Dispose();
                _watchdogCts = null;
            }
            _watchdogThread = null;
        }

        /// <summary>
        /// Vòng lặp theo dõi độ trễ UI thread ở luồng nền độc lập.
        /// </summary>
        private void WatchdogLoop(CancellationToken token)
        {
            const int checkIntervalMs = 1000;
            const double freezeThresholdSeconds = 2.0;

            while (!token.IsCancellationRequested)
            {
                try { Thread.Sleep(checkIntervalMs); } catch { break; }
                if (token.IsCancellationRequested) break;

                var now = DateTime.UtcNow;
                _lastUiPingTime = now;

                var pingSuccess = false;
                var sw = Stopwatch.StartNew();

                try
                {
                    // Gửi ping siêu nhẹ đến UI Dispatcher với ưu tiên Send
                    var dispatcher = Dispatcher;
                    if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    {
                        break;
                    }

                    dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                    {
                        _lastUiPongTime = DateTime.UtcNow;
                        _isUiResponsive = true;
                    }));

                    pingSuccess = true;
                }
                catch { }

                if (!pingSuccess) continue;

                // Kiểm tra khoảng cách từ lúc UI pong lần cuối
                var elapsedSinceLastPong = (now - _lastUiPongTime).TotalSeconds;

                if (elapsedSinceLastPong >= freezeThresholdSeconds)
                {
                    _isUiResponsive = false;
                    _uiFreezeDetectedCount++;

                    // Thu thập thông tin hệ thống khi UI bị đứng
                    var sb = new StringBuilder();
                    sb.AppendLine($"⚠️ [UI_FREEZE_ALERT #{_uiFreezeDetectedCount}] UI Thread is BLOCKED/FROZEN!");
                    sb.AppendLine($"   ⏱️ Hang Duration: {elapsedSinceLastPong:F2} seconds");

                    try
                    {
                        var process = Process.GetCurrentProcess();
                        var workingSetMb = process.WorkingSet64 / (1024.0 * 1024.0);
                        var gcMemMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                        sb.AppendLine($"   🧠 Memory: WorkingSet={workingSetMb:F1} MB | GCAllocated={gcMemMb:F1} MB");
                        sb.AppendLine($"   📊 GC Collections: G0={GC.CollectionCount(0)}, G1={GC.CollectionCount(1)}, G2={GC.CollectionCount(2)}");
                        sb.AppendLine($"   🧵 Threads: ProcessThreads={process.Threads.Count}");
                    }
                    catch { }

                    try
                    {
                        var dispatcher = Dispatcher;
                        if (dispatcher != null && !dispatcher.HasShutdownStarted)
                        {
                            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                            {
                                try
                                {
                                    var vm = ViewModel;
                                    if (vm != null)
                                    {
                                        var details = $"   🔄 Workflow State: IsExecuting={vm.IsExecuting} | ManualRunsInFlight={vm.ManualExecutionRunsInFlight} | ActiveConnection={vm.ActiveExecutionConnection != null}\n" +
                                                      $"   📌 Running Nodes Count: {vm.RunningNodes?.Count ?? 0}";
                                        WriteCanvasLog("FREEZE_DETAILS", details, isAlert: true);
                                    }
                                }
                                catch { }
                            }));
                        }
                    }
                    catch { }

                    WriteCanvasLog("FREEZE_ALERT", sb.ToString().TrimEnd(), isAlert: true);
                }
                else if (!_isUiResponsive && elapsedSinceLastPong < 1.0)
                {
                    // UI vừa khôi phục sau khi đứng
                    _isUiResponsive = true;
                    WriteCanvasLog("FREEZE_RECOVERY", $"✅ UI Thread RESPONDED and recovered after freeze! Latency now: {sw.ElapsedMilliseconds}ms");
                }
            }
        }

        /// <summary>
        /// Ghi log chẩn đoán canvas với thời gian millisecond chính xác.
        /// </summary>
        public void LogCanvasDiagnostic(string category, string message, bool isAlert = false)
        {
            var formatted = $"[{category}] {message}";
            WriteCanvasLog(category, formatted, isAlert);
        }

        /// <summary>
        /// Ghi trực tiếp xuống file log chẩn đoán.
        /// </summary>
        public static void WriteCanvasLog(string category, string message, bool isAlert = false)
        {
            var timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var threadId = Environment.CurrentManagedThreadId;
            var line = $"[{timeStamp}] [T{threadId:D2}] [{category}] {message}";

            // Output sang Debug console
            Debug.WriteLine(line);

            // Ghi file log an toàn với lock
            Task.Run(() =>
            {
                lock (_logFileLock)
                {
                    try
                    {
                        var filePath = LogFilePath;

                        // Tự động xoay file nếu log quá 10MB
                        if (File.Exists(filePath))
                        {
                            var fi = new FileInfo(filePath);
                            if (fi.Length > 10 * 1024 * 1024)
                            {
                                var oldPath = filePath + ".old";
                                if (File.Exists(oldPath)) File.Delete(oldPath);
                                File.Move(filePath, oldPath);
                            }
                        }

                        using var sw = new StreamWriter(filePath, append: true, Encoding.UTF8);
                        sw.WriteLine(line);
                        sw.Flush();
                    }
                    catch { }
                }
            });
        }
    }
}
