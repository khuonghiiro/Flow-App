// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.IO;

namespace FlowMy.Helpers
{
    public static class SciterWindowHelper
    {
        public static string GetSciterFolder()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var targetDll = Path.Combine(baseDir, "sciter.dll");

            if (File.Exists(targetDll))
            {
                return baseDir;
            }

            // Failsafe: check if we are in development mode and dll is in public/library
            var devLibDir = Path.Combine(baseDir, "..", "..", "..", "public", "library");
            if (Directory.Exists(devLibDir) && File.Exists(Path.Combine(devLibDir, "sciter.dll")))
            {
                try
                {
                    File.Copy(Path.Combine(devLibDir, "sciter.dll"), targetDll, true);
                    return baseDir;
                }
                catch { }
            }

            return baseDir; // Default fallback
        }
    }
}
