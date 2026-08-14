using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace FlowMy.Services
{
    /// <summary>
    /// Service quản lý auto-startup cho FlowMy
    /// Đăng ký chính FlowMy.exe tự khởi động cùng Windows
    /// </summary>
    public static class RegisterAppToStartup
    {
        private const string APP_REGISTRY_NAME = "FlowMy";
        private const string APP_EXE_NAME = "FlowMy.exe";
        private const string STARTUP_REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        #region Public Methods - Đăng ký FlowMy

        /// <summary>
        /// Đăng ký FlowMy tự động khởi động cùng Windows
        /// </summary>
        public static bool Register()
        {
            try
            {
                string appPath = GetAppPath();

                if (string.IsNullOrEmpty(appPath))
                {
                    Debug.WriteLine("❌ Không tìm thấy đường dẫn ứng dụng");
                    return false;
                }

                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, true))
                {
                    if (key == null)
                    {
                        Debug.WriteLine("❌ Không thể mở registry key");
                        return false;
                    }

                    var currentValue = key.GetValue(APP_REGISTRY_NAME) as string;
                    string expectedValue = $"\"{appPath}\"";

                    if (currentValue != expectedValue)
                    {
                        key.SetValue(APP_REGISTRY_NAME, expectedValue);
                        Debug.WriteLine($"✅ Đã đăng ký FlowMy startup: {expectedValue}");
                    }
                    else
                    {
                        Debug.WriteLine("✅ FlowMy đã được đăng ký đúng");
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Lỗi đăng ký FlowMy: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Hủy đăng ký FlowMy khỏi auto-startup
        /// </summary>
        public static bool Unregister()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, true))
                {
                    if (key?.GetValue(APP_REGISTRY_NAME) != null)
                    {
                        key.DeleteValue(APP_REGISTRY_NAME, false);
                        Debug.WriteLine("✅ Đã hủy đăng ký FlowMy");
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Lỗi hủy đăng ký FlowMy: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra FlowMy có được đăng ký startup không
        /// </summary>
        public static bool IsRegistered()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, false))
                {
                    return key?.GetValue(APP_REGISTRY_NAME) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Đảm bảo FlowMy được đăng ký đúng (validate và fix nếu cần)
        /// ✅ Gọi method này khi app khởi động
        /// </summary>
        public static bool EnsureRegistered()
        {
            try
            {
                // Kiểm tra xem đã đăng ký chưa
                if (!IsRegistered())
                {
                    Debug.WriteLine("⚠️ FlowMy chưa đăng ký, đang đăng ký...");
                    return Register();
                }

                // Validate đường dẫn có đúng không
                string currentValue = GetCurrentRegistryValue();
                string correctPath = GetAppPath();

                if (string.IsNullOrEmpty(correctPath))
                {
                    Debug.WriteLine("❌ Không tìm thấy đường dẫn ứng dụng");
                    return false;
                }

                string expectedValue = $"\"{correctPath}\"";

                // Nếu đường dẫn không đúng, đăng ký lại
                if (currentValue != expectedValue)
                {
                    Debug.WriteLine($"⚠️ Registry không đúng: '{currentValue}' → '{expectedValue}'");
                    return Register();
                }

                // Kiểm tra file có tồn tại không
                string filePath = currentValue.Trim('"');
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"⚠️ File không tồn tại: {filePath}");
                    return Register();
                }

                Debug.WriteLine("✅ FlowMy registry hợp lệ");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Lỗi validate FlowMy: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Lấy đường dẫn tới FlowMy.exe
        /// </summary>
        private static string GetAppPath()
        {
            try
            {
                // Method 1: Environment.ProcessPath (tốt nhất cho .NET 6+)
                string? processPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                {
                    Debug.WriteLine($"✅ Tìm thấy app path: {processPath}");
                    return processPath;
                }

                // Method 2: Assembly location
                string? assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyLocation) && File.Exists(assemblyLocation))
                {
                    Debug.WriteLine($"✅ Tìm thấy app path: {assemblyLocation}");
                    return assemblyLocation;
                }

                // Method 3: Base directory + exe name
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string appPath = Path.Combine(baseDir, APP_EXE_NAME);
                if (File.Exists(appPath))
                {
                    Debug.WriteLine($"✅ Tìm thấy app path: {appPath}");
                    return appPath;
                }

                // Method 4: Process MainModule
                try
                {
                    using (var process = Process.GetCurrentProcess())
                    {
                        string? mainModulePath = process.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(mainModulePath) && File.Exists(mainModulePath))
                        {
                            Debug.WriteLine($"✅ Tìm thấy app path: {mainModulePath}");
                            return mainModulePath;
                        }
                    }
                }
                catch { }

                Debug.WriteLine($"❌ Không tìm thấy {APP_EXE_NAME}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Lỗi tìm app path: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Lấy giá trị registry hiện tại của FlowMy
        /// </summary>
        private static string GetCurrentRegistryValue()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, false))
                {
                    return key?.GetValue(APP_REGISTRY_NAME) as string ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion


        #region Debug & Utilities

        /// <summary>
        /// In ra thông tin debug về startup registration
        /// </summary>
        public static void DebugRegistrationInfo()
        {
            try
            {
                Debug.WriteLine("╔═══════════════════════════════════════════╗");
                Debug.WriteLine("║   STARTUP REGISTRATION DEBUG INFO         ║");
                Debug.WriteLine("╚═══════════════════════════════════════════╝");

                Debug.WriteLine($"📋 FlowMy registered: {IsRegistered()}");
                Debug.WriteLine($"📋 Registry value: {GetCurrentRegistryValue()}");
                Debug.WriteLine($"📋 App path: {GetAppPath()}");
                Debug.WriteLine($"📋 Base directory: {AppDomain.CurrentDomain.BaseDirectory}");

                try
                {
                    using (var process = Process.GetCurrentProcess())
                    {
                        Debug.WriteLine($"📋 Current process: {process.MainModule?.FileName ?? "N/A"}");
                    }
                }
                catch { }

                // Liệt kê files .exe trong thư mục
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                if (Directory.Exists(currentDir))
                {
                    var exeFiles = Directory.GetFiles(currentDir, "*.exe", SearchOption.TopDirectoryOnly);
                    Debug.WriteLine($"📂 EXE files found ({exeFiles.Length}):");
                    foreach (var file in exeFiles)
                    {
                        Debug.WriteLine($"   • {Path.GetFileName(file)}");
                    }
                }

                Debug.WriteLine("═══════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Debug error: {ex.Message}");
            }
        }

        /// <summary>
        /// Xóa tất cả các registry entry cũ (cleanup)
        /// </summary>
        public static bool CleanupOldRegistrations()
        {
            try
            {
                bool hasChanges = false;

                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, true))
                {
                    if (key == null) return false;

                    // Xóa các entry cũ có thể tồn tại (bao gồm Watchdog cũ)
                    string[] oldNames = new[]
                    {
                        "DailyTrainWatchdogTray",    // Watchdog cũ
                        "DailyTrainWatchdog",        // Watchdog với tên khác
                        "DailyTrain"                 // Các biến thể khác
                    };

                    foreach (var oldName in oldNames)
                    {
                        if (key.GetValue(oldName) != null)
                        {
                            key.DeleteValue(oldName, false);
                            Debug.WriteLine($"🗑️ Đã xóa registry cũ: {oldName}");
                            hasChanges = true;
                        }
                    }
                }

                if (hasChanges)
                {
                    Debug.WriteLine("✅ Cleanup hoàn tất");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Lỗi cleanup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Setup đầy đủ: Cleanup cũ + Đăng ký FlowMy mới
        /// ✅ CHỈ ĐĂNG KÝ 1 LẦN - Kiểm tra nếu đã đăng ký đúng thì không làm gì
        /// </summary>
        public static bool SetupStartupRegistration()
        {
            try
            {
                // Kiểm tra nhanh: nếu đã đăng ký đúng rồi thì không cần làm gì
                if (IsRegistered())
                {
                    string currentValue = GetCurrentRegistryValue();
                    string correctPath = GetAppPath();
                    
                    if (!string.IsNullOrEmpty(correctPath))
                    {
                        string expectedValue = $"\"{correctPath}\"";
                        
                        // Nếu đã đăng ký đúng và file tồn tại → không cần làm gì
                        if (currentValue == expectedValue)
                        {
                            string filePath = currentValue.Trim('"');
                            if (File.Exists(filePath))
                            {
                                Debug.WriteLine("✅ FlowMy đã được đăng ký startup, không cần đăng ký lại");
                                return true;
                            }
                        }
                    }
                }

                // Chỉ khi chưa đăng ký hoặc đăng ký sai → mới cleanup và đăng ký lại
                Debug.WriteLine("🚀 Bắt đầu setup startup registration...");

                // Bước 1: Cleanup các entry cũ (bao gồm Watchdog) - chỉ khi cần
                CleanupOldRegistrations();

                // Bước 2: Đảm bảo FlowMy được đăng ký đúng
                bool success = EnsureRegistered();

                if (success)
                {
                    Debug.WriteLine("✅ Setup startup registration thành công");
                }
                else
                {
                    Debug.WriteLine("❌ Setup startup registration thất bại");
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Lỗi setup: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}