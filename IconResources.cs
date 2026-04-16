using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.IO;
using System.Windows.Media;

namespace FlowMy
{
    // partial class cho phép tách ra nhiều file
    public static partial class IconResources
    {
        // Cache SVG đã load
        private static readonly Dictionary<string, DrawingImage> _svgCache = new();
        private static readonly object _cacheLock = new object();

        // Load SVG on-demand với cache
        public static DrawingImage GetSvgImage(string iconName)
        {
            if (string.IsNullOrEmpty(iconName) || !AvailableIcons.ContainsKey(iconName))
                return null;

            lock (_cacheLock)
            {
                if (_svgCache.TryGetValue(iconName, out var cachedImage))
                    return cachedImage;

                try
                {
                    string svgPath = AvailableIcons[iconName];
                    string pathNormalized = svgPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pathNormalized);

                    if (!File.Exists(fullPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"SVG file not found: {fullPath}");
                        return null;
                    }

                    var settings = new WpfDrawingSettings
                    {
                        IncludeRuntime = true,
                        TextAsGeometry = false,
                        OptimizePath = true
                    };

                    var converter = new FileSvgReader(settings);
                    var drawing = converter.Read(fullPath);
                    var image = new DrawingImage(drawing);

                    image.Freeze();
                    _svgCache[iconName] = image;

                    return image;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading SVG {iconName}: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>Load SVG và tô màu icon (thay mọi brush bằng màu chỉ định).</summary>
        public static DrawingImage GetSvgImage(string iconName, Color tintColor)
        {
            var baseImage = GetSvgImage(iconName);
            if (baseImage?.Drawing == null) return null;
            var brush = new SolidColorBrush(tintColor);
            brush.Freeze();
            var tintedDrawing = CloneDrawingWithBrush(baseImage.Drawing, brush);
            if (tintedDrawing == null) return baseImage;
            var image = new DrawingImage(tintedDrawing);
            image.Freeze();
            return image;
        }

        private static Drawing CloneDrawingWithBrush(Drawing drawing, Brush brush)
        {
            if (drawing is DrawingGroup group)
            {
                var newGroup = new DrawingGroup();
                foreach (var child in group.Children)
                {
                    var cloned = CloneDrawingWithBrush(child, brush);
                    if (cloned != null) newGroup.Children.Add(cloned);
                }
                return newGroup.Children.Count > 0 ? newGroup : null;
            }
            if (drawing is GeometryDrawing gd)
            {
                return new GeometryDrawing(brush, gd.Pen?.Clone(), gd.Geometry?.Clone());
            }
            if (drawing is GlyphRunDrawing grd)
            {
                return new GlyphRunDrawing(brush, grd.GlyphRun);
            }
            if (drawing is ImageDrawing id)
            {
                return new ImageDrawing(id.ImageSource?.Clone(), id.Rect);
            }
            return null;
        }

        public static string GetIconPath(string iconName)
        {
            return AvailableIcons.TryGetValue(iconName, out var path) ? path : null;
        }

        public static bool IconExists(string iconName)
            => AvailableIcons.ContainsKey(iconName);

        public static IEnumerable<string> GetAllIconNames()
            => AvailableIcons.Keys;

        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _svgCache.Clear();
            }
        }
    }
}