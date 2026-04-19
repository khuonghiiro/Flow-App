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
            if (string.IsNullOrEmpty(iconName)) return null;
            // Ưu tiên manifest (extracted icons) — path động từ project root,
            // fallback AvailableIcons (dictionary tĩnh biên dịch vào binary).
            var manifest = ExtractedIcons;
            if (manifest != null && manifest.TryGetValue(iconName, out var mPath)) return mPath;
            return AvailableIcons.TryGetValue(iconName, out var path) ? path : null;
        }

        public static bool IconExists(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return false;
            var manifest = ExtractedIcons;
            if (manifest != null && manifest.ContainsKey(iconName)) return true;
            return AvailableIcons.ContainsKey(iconName);
        }

        public static IEnumerable<string> GetAllIconNames()
            => AvailableIcons.Keys;

        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _svgCache.Clear();
            }
        }

        // ── Manifest động từ ExtractIcons.ps1 ──────────────────────────────
        // File manifest "Assets/Icons/available_icons.txt" liệt kê các icon đã được
        // extract thực tế (thay vì dùng toàn bộ AvailableIcons dictionary — vốn tham
        // chiếu cả icon chưa copy vào output). Format mỗi dòng:
        //     <icon-key>=<relative-path-from-project-root>
        //     # dòng comment bắt đầu bằng dấu #
        // Path được resolve động qua AppDomain.CurrentDomain.BaseDirectory.

        private static readonly object _manifestLock = new();
        private static Dictionary<string, string>? _manifestIcons; // lazy

        public const string ManifestRelativePath = "Assets/Icons/available_icons.txt";

        /// <summary>
        /// Danh sách icon đã extract thực tế (đọc từ available_icons.txt).
        /// Nếu file không tồn tại thì trả về null — caller nên fallback về <see cref="AvailableIcons"/>.
        /// </summary>
        public static IReadOnlyDictionary<string, string>? ExtractedIcons
        {
            get
            {
                lock (_manifestLock)
                {
                    if (_manifestIcons != null) return _manifestIcons;
                    _manifestIcons = TryLoadManifest();
                    return _manifestIcons;
                }
            }
        }

        /// <summary>
        /// Danh sách hiệu dụng: ưu tiên manifest (runtime), fallback <see cref="AvailableIcons"/>.
        /// </summary>
        public static IReadOnlyDictionary<string, string> EffectiveIcons
            => ExtractedIcons ?? AvailableIcons;

        /// <summary>Buộc reload manifest từ disk (ví dụ sau khi chạy ExtractIcons.ps1).</summary>
        public static void ReloadManifest()
        {
            lock (_manifestLock) { _manifestIcons = null; }
        }

        private static Dictionary<string, string>? TryLoadManifest()
        {
            try
            {
                string norm = ManifestRelativePath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.Combine(baseDir, norm);

                if (!File.Exists(fullPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[IconResources] manifest not found: {fullPath}");
                    return null;
                }

                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var rawLine in File.ReadAllLines(fullPath))
                {
                    var line = rawLine?.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("#")) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0 || eq >= line.Length - 1) continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) continue;

                    dict[key] = val;
                }
                return dict.Count > 0 ? dict : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconResources] TryLoadManifest error: {ex.Message}");
                return null;
            }
        }
    }
}