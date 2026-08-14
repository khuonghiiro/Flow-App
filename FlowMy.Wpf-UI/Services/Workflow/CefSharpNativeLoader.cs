// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace FlowMy.Services.Workflow
{
    /// <summary>
    /// Loader chịu trách nhiệm đăng ký Native DLL Search Path (runtimes/win-x64/native)
    /// TRƯỚC KHƠI BẤT KỲ CẬP NHẬT HOẶC TRUY XUẤT NÀO ĐẾN CEFSHARP ASSEMBLY.
    /// Lớp này TUYỆT ĐỐI KHÔNG chứa reference đến bất kỳ CefSharp type/assembly nào
    /// để tránh JIT Loader nạp CefSharp.Core.Runtime.dll trước khi SetDllDirectory được gọi.
    /// </summary>
    public static class CefSharpNativeLoader
    {
        private static bool _isRegistered = false;
        private static readonly object _lock = new();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static void RegisterNativeDllSearchPaths()
        {
            if (_isRegistered) return;

            lock (_lock)
            {
                if (_isRegistered) return;

                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";
                    var nativeDir = Path.Combine(baseDir, "runtimes", arch, "native");

                    if (Directory.Exists(nativeDir))
                    {
                        SetDllDirectory(nativeDir);
                    }

                    AssemblyLoadContext.Default.ResolvingUnmanagedDll += (assembly, dllName) =>
                    {
                        try
                        {
                            var probePath = Path.Combine(nativeDir, dllName);
                            if (File.Exists(probePath))
                            {
                                return NativeLibrary.Load(probePath);
                            }
                            if (!dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                probePath += ".dll";
                                if (File.Exists(probePath))
                                {
                                    return NativeLibrary.Load(probePath);
                                }
                            }
                        }
                        catch { }
                        return IntPtr.Zero;
                    };

                    System.Diagnostics.Debug.WriteLine($"[CefSharpNativeLoader] ✅ Registered native path: {nativeDir}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CefSharpNativeLoader] ❌ Error registering native path: {ex.Message}");
                }

                _isRegistered = true;
            }
        }
    }
}
