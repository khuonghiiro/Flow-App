// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS & DEVELOPERS:
// DO NOT ALLOW ANY FILE IN THIS COMPONENT TO EXCEED ~1500 LINES OF CODE!
// To maintain readability, ease of testing, and modularity:
// - If a file grows larger than ~1500 lines, you MUST split/separate the logic into a new
//   partial class file (e.g., ImageProcessingNodeControl.<FeatureName>.cs).
// - Always place distinct features, tools, or event groupings in their respective files.
// - Ensure comments and documentation remain clean and structured.
// ========================================================================================
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Globalization;
using System.Collections.Specialized;
using System.Text.Json;
using WinForms = System.Windows.Forms;
using System.Linq;
using System;
namespace FlowMy.Views.NodeControls
{
    public static partial class ImageProcessingNodeControl
    {
        private static string? ResolveFromNodeIfAny(IWorkflowEditorHost host, string? nodeId, string? key)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key)) return null;
            var src = host.ViewModel?.Nodes?.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (src == null) return null;
            var value = NodeDataPanelService.ResolveDynamicValueByKey(src, key, forDisplay: false);
            if (string.IsNullOrWhiteSpace(value) || value == "—") return null;
            return value;
        }

        private static BitmapImage? CreateBitmapFromUrlOrFile(string value)
        {
            try
            {
                value = value.Trim();
                if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    value = new Uri(value).LocalPath;
                }

                if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(value, UriKind.Absolute);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = uri;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }

                // Assume local path
                if (!File.Exists(value)) return null;
                using var fs = File.OpenRead(value);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = fs;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage? CreateBitmapFromBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;
            base64 = base64.Trim();
            // Strip data URI prefix if present
            var comma = base64.IndexOf(',');
            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
                base64 = base64.Substring(comma + 1);

            // Remove whitespace/newlines
            base64 = new string(base64.Where(c => !char.IsWhiteSpace(c)).ToArray());
            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64); }
            catch { return null; }

            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static int NextPreviewVersion(ImageProcessingNode node)
        {
            lock (_previewVersion)
            {
                if (!_previewVersion.TryGetValue(node, out var v)) v = 0;
                v++;
                _previewVersion[node] = v;
                return v;
            }
        }

        private static bool IsLatestPreview(ImageProcessingNode node, int version)
        {
            lock (_previewVersion)
            {
                return _previewVersion.TryGetValue(node, out var v) && v == version;
            }
        }

        /// <summary>Chrome node ảnh: lớp nền có DropShadow.
        internal static Border? TryGetImageWorkflowShadowPlate(Border chromeBorder)
        {
            if (chromeBorder?.Child is not Grid top)
                return null;

            const string nodeChromeRootTag = "NodeChromeRoot";
            if (string.Equals(top.Tag as string, nodeChromeRootTag, StringComparison.Ordinal)
                && top.Children.Count > 0
                && top.Children[0] is Grid chromeFill
                && chromeFill.Children.Count > 0
                && chromeFill.Children[0] is Border wrappedPlate)
                return wrappedPlate;

            if (top.Children.Count > 0 && top.Children[0] is Border plate)
                return plate;

            return null;
        }

        internal static void SyncWorkflowChromeNodeBrush(Border chromeBorder, Brush brush)
        {
            if (chromeBorder == null) return;
            if (TryGetImageWorkflowShadowPlate(chromeBorder) is { } plate)
            {
                chromeBorder.Background = Brushes.Transparent;
                plate.Background = brush;
                return;
            }
            chromeBorder.Background = brush;
        }

        internal static void RefreshImageWorkflowChromeDropShadow(Border chromeBorder)
        {
            if (TryGetImageWorkflowShadowPlate(chromeBorder) is not { } plate) return;
            plate.Effect = GpuOptimizationHelper.CreateDropShadowEffect();
            GpuOptimizationHelper.ApplyToElement(plate);
        }

    }

    /// <summary>Converter: int Order → "#N" label (ví dụ 2 → "#2").</summary>
    public sealed class IntToHashLabelConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int n)
                return $"#{n}";
            return $"#{value}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
