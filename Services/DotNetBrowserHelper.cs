using System;
using System.IO;
using System.Reflection;
using DotNetBrowser.Browser;
using DotNetBrowser.Engine;
using DotNetBrowser.Wpf;

namespace FlowMy.Services
{
    public static class DotNetBrowserHelper
    {

        /// <summary>
        /// Gets the underlying IBrowser from a DotNetBrowser WPF BrowserView.
        /// </summary>
        public static IBrowser? GetBrowser(BrowserView? bv)
        {
            if (bv == null) return null;
            try
            {
                var prop = bv.GetType().GetProperty("Browser", BindingFlags.Public | BindingFlags.Instance);
                return prop?.GetValue(bv) as IBrowser;
            }
            catch
            {
                return null;
            }
        }

        public static System.Threading.Tasks.Task<string> ExecuteJavaScriptAsync(BrowserView? bv, string script)
        {
            var b = GetBrowser(bv);
            if (b == null) return System.Threading.Tasks.Task.FromResult(string.Empty);
            try
            {
                var task = b.MainFrame.ExecuteJavaScript<object>(script);
                return task.ContinueWith(t => t.IsCompletedSuccessfully ? (t.Result?.ToString() ?? string.Empty) : string.Empty);
            }
            catch
            {
                return System.Threading.Tasks.Task.FromResult(string.Empty);
            }
        }

        public static void InitializeBrowserView(BrowserView? bv, IBrowser? browser)
        {
            if (bv == null || browser == null) return;
            try
            {
                var prop = bv.GetType().GetProperty("Browser", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(bv, browser);
                }
                else
                {
                    var field = bv.GetType().GetField("_browser", BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? bv.GetType().GetField("browser", BindingFlags.NonPublic | BindingFlags.Instance);
                    field?.SetValue(bv, browser);
                }
            }
            catch { }
        }
    }
}
