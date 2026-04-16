using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FlowMy.Helpers
{
    public static class VersionHelper
    {
        /// <summary>
        /// Lấy version từ AssemblyVersion (1.0.3)
        /// </summary>
        public static string GetVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString(3) ?? "1.0.0";
        }

        /// <summary>
        /// Lấy full version bao gồm revision (1.0.3.0)
        /// </summary>
        public static string GetFullVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "1.0.0.0";
        }

        /// <summary>
        /// Lấy version hiển thị (v1.0.3)
        /// </summary>
        public static string GetDisplayVersion()
        {
            return $"v{GetVersion()}";
        }

        /// <summary>
        /// Lấy thông tin chi tiết về assembly
        /// </summary>
        public static AssemblyInfo GetAssemblyInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();

            return new AssemblyInfo
            {
                Version = GetVersion(),
                FullVersion = GetFullVersion(),
                Title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "",
                Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "",
                Product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "",
                Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "",
                Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? ""
            };
        }
    }

    public class AssemblyInfo
    {
        public string Version { get; set; } = "";
        public string FullVersion { get; set; } = "";
        public string Title { get; set; } = "";
        public string Company { get; set; } = "";
        public string Product { get; set; } = "";
        public string Copyright { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
